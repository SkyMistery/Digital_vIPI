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
    .AddInteractiveServerComponents();

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

// Persistenza chiavi Data Protection su DB (solo Postgres): antiforgery/cookie sopravvivono ai redeploy
// sul container effimero di Render. No-op in dev (SQLite → file-store di default). Vedi VipiDataProtection.cs.
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
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Crea la tabella delle chiavi Data Protection se manca (idempotente; no-op se il modulo non è attivo).
app.UseVipiDataProtection();
// Crea/migra il DB del modulo. Nessun seed: i dati reali si inseriscono dall'app (editor/struttura).
app.MigrateVipiDatabase();
// Riconciliazioni documentali (doc 11): chiavi univoche per le sezioni libere storiche (idempotente).
app.ReconcileVipiDocuments();
// Riallinea i settori proiettati ai cataloghi: fa entrare in vigore i cambi alla regola di derivazione della
// gerarchia senza aspettare il prossimo import (idempotente).
app.ProjectVipiSectors();
// Migrazione A (doc 10 §3f): garantisce una release effettiva per i documenti pubblicati (idempotente).
app.BackfillVipiReleases();
// Retention pubblicazione: pota release Superseded oltre soglia e versioni Archived oltre N (idempotente).
app.PruneVipiReleases();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    // Solo in prod: in dev l'host ascolta su http e il redirect logga un warning inutile.
    app.UseHttpsRedirection();
}

// Compressione delle risposte dinamiche (HTML del prerender, JSON, SignalR). I file statici NON passano
// più di qui: MapStaticAssets serve i .br/.gz precompilati a build-time, senza spendere CPU per richiesta.
app.UseResponseCompression();

// I .woff2 sono referenziati da DENTRO vipi-fonts.css, quindi non passano da @Assets: MapStaticAssets li
// serve col profilo non-impronta (max-age 1h + must-revalidate), più corto dei 7 giorni di prima. I nomi
// arrivano da Google Fonts, sono già content-addressed e i file non cambiano: si riporta la cache lunga.
// Va fatto riscrivendo l'header e non con UseStaticFiles: il font lo serve un ENDPOINT di MapStaticAssets, e
// StaticFileMiddleware si tira indietro quando il routing ha già selezionato un endpoint (non servirebbe nulla).
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.Value?.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) == true)
    {
        // OnStarting: l'header va riscritto dopo che l'endpoint ha impostato il suo, ma prima del flush.
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers.CacheControl = app.Environment.IsDevelopment()
                ? "no-cache, no-store, must-revalidate"
                : "public,max-age=604800"; // 7 giorni, invariato rispetto a prima di MapStaticAssets
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseAntiforgery();

// Auth standalone (scenario C): serve il ClaimsPrincipal alle richieste. Prima di UseVipiModule,
// così lo StaffLoginTrackingMiddleware vede già l'utente autenticato. Montato solo se attivo.
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapVipiStandaloneAuth();
}

// Middleware del modulo (registrazione login staff nel roster).
app.UseVipiModule();

// Compat: i vecchi URL /sop* (pre-rebuild Round 12) redirigono al nuovo prefisso /vsop*,
// preservando la query string (es. ?icao=LIRF). Tutti gli endpoint reali sono ora su /vsop.
app.MapGet("/sop", (HttpContext ctx) => Results.Redirect($"/vsop{ctx.Request.QueryString}", permanent: true));
app.MapGet("/sop/{*rest}", (HttpContext ctx, string rest) => Results.Redirect($"/vsop/{rest}{ctx.Request.QueryString}", permanent: true));

// Compat: la pagina struttura è stata rinominata in /vsop/admin/sectorstructure.
app.MapGet("/vsop/admin/struttura", (HttpContext ctx) => Results.Redirect($"/vsop/admin/sectorstructure{ctx.Request.QueryString}", permanent: true));

// Compat: le due viste operative per-ACC sono diventate UNA vista per callsign (doc refactor 12).
//   /vsop/{acc}/operativa · /vsop/{acc}/live               → /vsop/live            (o /vsop/live/{p} se c'era ?p=)
//   /vsop/{acc}/operativa-app · /vsop/{acc}/live-app?app=X → /vsop/live/x
// Un solo salto per ciascun URL storico: sono pagine che finiscono nei preferiti di chi controlla, e una
// catena di redirect si paga a ogni apertura.
static IResult LiveRedirect(HttpContext ctx, string? callsign)
{
    var cs = (callsign ?? "").Trim().ToLowerInvariant();
    return Results.Redirect(cs.Length > 0 ? $"/vsop/live/{Uri.EscapeDataString(cs)}" : "/vsop/live", permanent: true);
}

foreach (var legacy in new[] { "operativa", "live" })
    app.MapGet($"/vsop/{{acc}}/{legacy}", (HttpContext ctx) => LiveRedirect(ctx, ctx.Request.Query["p"]));

foreach (var legacy in new[] { "operativa-app", "live-app" })
    app.MapGet($"/vsop/{{acc}}/{legacy}", (HttpContext ctx) => LiveRedirect(ctx, ctx.Request.Query["app"]));

// File statici (wwwroot dell'host + wwwroot della RCL vIPI). Sostituisce UseStaticFiles: gli asset sono
// impronta-per-contenuto (`@Assets[...]` in App.razor) e serviti con `immutable`, quindi un deploy rifà
// scaricare solo i file davvero cambiati; le varianti brotli/gzip sono precompilate a build-time.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(VipiModuleExtensions.UiAssembly)    // monta la RCL vIPI
    .WithStaticAssets();

// Endpoint del modulo (SSE live ATC).
app.MapVipiModule();

app.Run();

// Punto d'ingresso esposto per i test d'integrazione in-process (WebApplicationFactory<Program>).
// I top-level statement generano una classe Program internal: questa partial la rende raggiungibile dai test.
public partial class Program { }
