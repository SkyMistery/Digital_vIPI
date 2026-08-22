using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Vipi.Host;
using Vipi.Host.Auth;
using Vipi.Host.Components;
using Vipi.Hosting;

// PRIMA di tutto: su host senza accesso ai log (niente journalctl, niente console) un avvio fallito è
// cieco da entrambe le parti. Questo scrive l'eccezione fatale accanto all'eseguibile, in un file che si
// scarica via FTP. Senza segreti dentro: vedi StartupDiagnostics.
StartupDiagnostics.HookFatalErrors();

var builder = WebApplication.CreateBuilder(args);

// Riepilogo della configurazione vista, riscritto a ogni avvio — anche riuscito. Sta qui, subito dopo il
// builder, perché serva anche quando l'avvio muore più avanti: dice con QUALE configurazione ci ha provato.
StartupDiagnostics.WriteConfigurationSummary(builder);

// File (default globale) della frase di coordinamento. reloadOnChange:false — il FileSystemWatcher esaurirebbe
// le istanze inotify su host con limite basso (es. Render); in container il file è comunque immutabile (baked nell'immagine).
builder.Configuration.AddJsonFile("content/coordination-sentence.json", optional: true, reloadOnChange: false);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        // Tetti espliciti invece dei default (100 circuiti trattenuti per 3 minuti). La scala decisa è UNA
        // istanza (audit 22 luglio, voce A2, e il vincolo è scritto in nginx-vipi.conf): il limite di memoria
        // di quel processo è quindi l'unica cosa che regge il sito, e un circuito disconnesso porta con sé
        // tutto lo stato della pagina editor che aveva aperta.
        // ⚠️ Numeri stimati sul traffico atteso (decine di persone, non migliaia): da rivedere dopo il primo
        // ciclo AIRAC pubblicato dal server nuovo, quando ci sarà una misura al posto di una stima.
        o.DisconnectedCircuitMaxRetained = 25;
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);

        // Scritto, non ereditato: con true le eccezioni non gestite arrivano al browser con lo stack.
        o.DetailedErrors = builder.Environment.IsDevelopment();
    });

// Compressione asset di testo (CSS/JS/SignalR). NIENTE text/event-stream: la rotta SSE /vsop/live/atc
// usa DisableBuffering() e dev'essere consegnata subito, non compressa/bufferizzata.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = new[] { "text/css", "text/javascript", "application/javascript", "application/json", "image/svg+xml", "text/html" };
});

// Modulo login IVAO standalone (scenario C). STACCABILE: attivo solo se VipiAuth:Enabled=true.
// Se attivo, il ClaimsPrincipal lo produce questo modulo e HostIdentityCurrentUserProvider lo legge.
var authEnabled = builder.AddVipiStandaloneAuth();

// Persistenza chiavi Data Protection su DB (Postgres e MariaDB, cioè i due deploy): antiforgery, cookie di
// auth e state OIDC sopravvivono a un riavvio su disco effimero. No-op in dev (SQLite → file-store di
// default). Vedi VipiDataProtection.cs.
builder.AddVipiDataProtection();

// Modulo vIPI: un'unica chiamata registra Application, Infrastructure/EF, polling IVAO, opzioni e identità.
// In sviluppo usa l'utente CH fittizio; in produzione l'identità è letta dal login del sito ospitante.
// Se il login IVAO standalone è attivo, esso vince sul dev identity anche in sviluppo (si prova il login vero).
var useDevIdentity = builder.Environment.IsDevelopment() && !authEnabled;
// Guardia di sicurezza (audit D1): mai identità dev fittizia (admin onnipotente) fuori da Development.
Vipi.Hosting.ProductionIdentityGuard.EnsureSafe(builder.Environment.IsDevelopment(), useDevIdentity);
builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: useDevIdentity);

var app = builder.Build();

// Dietro il proxy TLS di Fly.io/Render (TLS al bordo, HTTP interno): fidati di X-Forwarded-Proto/For così
// UseHttpsRedirection non entra in loop e OIDC costruisce il redirect_uri in https. KnownIPNetworks/Proxies
// svuotati perché l'IP del proxy non è fisso. Innocuo in locale (gli header non arrivano).
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};

// Su net8 la collezione si chiama KnownNetworks (KnownIPNetworks è .NET 9+).
//
// Svuotare entrambe significa «fidati di X-Forwarded-For da chiunque», ed è quel che serve su Render, dove
// l'IP del proxy non è fisso. Su atc.it.ivao.aero NON serve: nginx sta sulla stessa macchina e arriva da
// loopback. Lasciarle vuote lì vorrebbe dire che l'IP del chiamante lo sceglie il chiamante — e su
// quell'IP si regge il tetto per-IP del bridge Aurora, oltre a ogni riga di log che dice «da dove».
//
// Perciò: in Production ci si fida SOLO del loopback; altrove resta il comportamento di prima.
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
if (app.Environment.IsProduction())
{
    forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);       // 127.0.0.1
    forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);   // ::1
}
app.UseForwardedHeaders(forwardedOptions);

// Intestazioni di sicurezza. Non chiudono una falla nota — le due funzioni che costruiscono HTML a mano
// (MarkdownLite, AorBlock.BuildSvg) encodano prima e costruiscono dopo — ma rendono innocuo l'errore di
// domani, che è la difesa che costa meno di tutte. Prima di UseStaticFiles, così valgono anche per gli asset.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["X-Frame-Options"] = "DENY";                        // nessuna pagina vIPI va dentro un iframe
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

    // CSP ancora in sola SEGNALAZIONE, e il perché è scritto qui sotto — non è dimenticanza.
    //
    // `script-src` ha perso `'unsafe-inline'`: i due <script> inline di App.razor (zoom e riaggancio dopo
    // le navigazioni enhanced) sono diventati file, e una guardia E2E pretende che la pagina non ne
    // contenga altri. È il pezzo che conta davvero, perché è quello che rende innocuo uno script iniettato.
    //
    // ⚠️ COSA MANCA PER PASSARE A `Content-Security-Policy` VERO, misurato l'11 agosto 2026:
    //   • 17 gestori inline nel markup (`onclick="window.print()"`, `ondragover="event.preventDefault()"`,
    //     `onclick="location.href=…"`): sono attributi HTML, quindi `script-src` li blocca esattamente come
    //     uno <script> inline. Vanno convertiti in delega di eventi o in handler Blazor — e ognuno cambia il
    //     comportamento di una pagina, quindi va guardato, non riscritto a tappeto.
    //   • 554 attributi `style="…"` nel markup, che tengono in piedi `style-src 'unsafe-inline'`. Questa
    //     clausola è molto meno grave della gemella sugli script, e può restare a lungo.
    //   • una passata con un browser vero: una CSP che entra in vigore rompe in modo vistoso, e nessun test
    //     qui dentro apre una pagina davvero.
    //
    // Finché quei 17 non sono spariti, mettere `Content-Security-Policy` senza `unsafe-inline` romperebbe
    // la stampa, il drag&drop della struttura e tre elenchi. Con `unsafe-inline` non proteggerebbe da nulla.
    // Report-Only dice la verità su entrambe le cose senza rompere niente.
    //
    // ⚠️ `blazor.web.js` ha bisogno di `connect-src` verso il proprio origin (WebSocket): 'self' lo copre
    // per ws:// e wss:// sullo stesso host.
    headers["Content-Security-Policy-Report-Only"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        // Le tessere della mappa vengono da CARTO e non si vendorizzano: è l'unico host esterno rimasto,
        // ed è dato pubblico di sfondo — i poligoni, che sono il nostro dato, li disegna Leaflet in locale.
        "img-src 'self' data: blob: https://*.basemaps.cartocdn.com; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    await next();
});

// Crea la tabella delle chiavi Data Protection se manca (idempotente; no-op se il modulo non è attivo).
app.UseVipiDataProtection();
// Crea/migra il DB del modulo. Nessun seed: i dati reali si inseriscono dall'app (editor/struttura).
// CRITICA: un guasto qui deve fermare l'avvio. Servire pagine su uno schema che non è quello atteso dal
// codice significa scoprirlo a runtime, come colonna mancante, lontano dalla causa.
app.MigrateVipiDatabase();

// Le quattro manutenzioni non critiche (riconciliazioni documentali, proiezione settori, backfill e potatura
// delle release), ognuna isolata dalle altre: un guasto viene registrato — log + diagnostica, quindi
// /vsop/health in Degraded — e l'avvio prosegue. Prima erano quattro chiamate nude, e con Restart=always nel
// servizio systemd un difetto in una di esse non era un degrado ma un ciclo di riavvii.
app.RunVipiStartupMaintenance();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    // Solo in prod: in dev l'host ascolta su http e il redirect logga un warning inutile.
    app.UseHttpsRedirection();
}

// Compressione delle risposte. Su net8 ci passano ANCHE i file statici, perché UseStaticFiles serve i file
// così come sono: niente varianti .br/.gz precompilate a build-time, quelle le faceva MapStaticAssets (.NET 9+).
// Costo: la compressione di CSS e JS si paga a ogni richiesta invece che una volta in build.
app.UseResponseCompression();

// File statici: wwwroot dell'host + wwwroot della RCL vIPI (_content/Vipi.Ui/...).
// Rimpiazza MapStaticAssets, che è .NET 9+ (ADR-0007 §D4-ter). Il cache-busting lo fa AssetVersion, che
// legge i file da QUESTO stesso provider: l'impronta nell'URL è quella del contenuto servito, non della
// build, quindi un asset immutato conserva il proprio URL e resta valido in cache.
AssetVersion.Initialize(app.Environment.WebRootFileProvider);

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var dev = app.Environment.IsDevelopment();
        var percorso = ctx.File.Name;

        // I .woff2 sono referenziati da DENTRO vipi-fonts.css, quindi NON passano da AssetVersion e il
        // loro URL non cambia mai. I nomi però arrivano da Google Fonts, sono già content-addressed e i
        // file non cambiano: la cache lunga è sicura ed evita di riscaricarli a ogni deploy.
        var lunga = percorso.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);

        ctx.Context.Response.Headers.CacheControl = dev
            ? "no-cache, no-store, must-revalidate"
            : lunga ? "public,max-age=604800"    // 7 giorni
                    : "public,max-age=86400";    // 1 giorno: col ?v= per contenuto, un asset immutato
                                                 // conserva l'URL e alla scadenza si rivalida con un 304
    },
});

// Auth standalone (scenario C): serve il ClaimsPrincipal alle richieste. Prima di UseVipiModule,
// così lo StaffLoginTrackingMiddleware vede già l'utente autenticato. Montato solo se attivo.
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapVipiStandaloneAuth();
}

// DOPO UseAuthentication/UseAuthorization, come chiede la guida Blazor — e non prima, com'era fino
// all'11 agosto 2026. Il token antiforgery va legato all'identità che lo chiede: girando prima, il
// middleware lo emette quando l'utente non è ancora montato, e il token resta valido attraverso un
// cambio di utente sulla stessa sessione. Nessun exploit da mostrare; solo un ordine che non aveva
// motivo di essere quello.
app.UseAntiforgery();

// Middleware del modulo (registrazione login staff nel roster).
app.UseVipiModule();

// Compat: TUTTI gli URL storici passano da qui, e ne escono con l'indirizzo di oggi — quello finale, in UN
// salto solo. La tabella e il perché stanno in LegacyRoutes: qui resta il collegamento.
//   /sop*  (pre-Round 12)             → /services/vsop/*
//   /vsop* (pre-22 agosto 2026)       → /services/vsop/*, coi segmenti tradotti (guida → guide, …)
//   /vsop/{acc}/operativa|live[-app]  → /services/vsop/live[/{callsign}]  (il callsign stava in query)
//   /vsop/admin/struttura             → /services/vsop/admin/sector-structure
// ⚠️ Gli endpoint macchina (health, api, media, live/atc) NON passano di qui: hanno segmenti letterali, che
// nel routing battono queste catch-all, e LegacyRoutes.Resolve li rifiuta comunque.
static IResult RedirectLegacy(HttpContext ctx)
{
    var destinazione = LegacyRoutes.Resolve(ctx.Request);
    return destinazione is null ? Results.NotFound() : Results.Redirect(destinazione, permanent: true);
}

foreach (var storica in new[] { "/sop", "/sop/{*rest}", "/vsop", "/vsop/{*rest}" })
    app.MapGet(storica, RedirectLegacy);

// (I file statici li serve UseStaticFiles, più in alto: su net8 non esistono né MapStaticAssets né
//  WithStaticAssets, che sono .NET 9+.)

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(VipiModuleExtensions.UiAssembly);   // monta la RCL vIPI

// Endpoint del modulo (SSE live ATC).
app.MapVipiModule();

app.Run();

// Punto d'ingresso esposto per i test d'integrazione in-process (WebApplicationFactory<Program>).
// I top-level statement generano una classe Program internal: questa partial la rende raggiungibile dai test.
public partial class Program { }
