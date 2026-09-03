using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Auth;
using Vipi.Infrastructure;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Weather;

namespace Vipi.Hosting;

/// <summary>
/// Superficie d'integrazione del modulo vIPI: un sito host lo aggancia con poche chiamate
/// (<see cref="AddVipiModule"/> + <see cref="UseVipiModule"/> + <see cref="MapVipiModule"/> +
/// <see cref="MigrateVipiDatabase"/>) senza ricopiare il wiring interno. Vedi docs/INTEGRATION.md.
/// </summary>
public static class VipiModuleExtensions
{
    /// <summary>Assembly della RCL vIPI: passarlo a <c>AddAdditionalAssemblies(...)</c> nell'host.</summary>
    public static Assembly UiAssembly => typeof(Vipi.Ui.Pages.SopHome).Assembly;

    /// <summary>Tag della sonda economica (<c>/vsop/health/ready</c>): solo le condizioni critiche.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Tag del quadro completo (<c>/vsop/health</c>): include il report di consistenza, che costa.</summary>
    public const string FullTag = "full";

    /// <summary>
    /// Tetto alle connessioni SSE contemporanee su <c>/vsop/live/atc</c>. Costante e non configurazione:
    /// la scala è una divisione, il numero giusto non dipende dall'installazione, e un'opzione in più è
    /// un'opzione in più da spiegare. Oltre il tetto si risponde 503 con <c>Retry-After</c>.
    /// </summary>
    private const int MaxSseConcorrenti = 300;

    /// <summary>Connessioni SSE attualmente aperte. Vive quanto il processo, come l'endpoint.</summary>
    private static int _sseAperti;

    /// <summary>
    /// Registra tutti i servizi del modulo (Application, Infrastructure/EF, polling IVAO, opzioni,
    /// identità). <paramref name="useDevIdentity"/> = true monta l'utente fittizio di sviluppo;
    /// altrimenti l'identità è letta dal sito ospitante via <see cref="HostIdentityCurrentUserProvider"/>.
    /// </summary>
    public static IServiceCollection AddVipiModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useDevIdentity = false)
    {
        var connectionString = configuration.GetConnectionString("Vipi") ?? "Data Source=vipi.db";

        services.AddVipiApplication();
        services.AddVipiInfrastructure(connectionString, configuration);

        // Sorgente dati esterna selezionabile (DataSource:Provider). L'app dipende solo dalle interfacce neutre;
        // qui si sceglie l'adapter che le implementa. Oggi solo "Ivao"; future: "Static"/"Db"/altro network.
        services.Configure<DataSourceOptions>(configuration.GetSection(DataSourceOptions.SectionName));
        var dataSource = configuration.GetSection(DataSourceOptions.SectionName)["Provider"] ?? "Ivao";
        if (string.Equals(dataSource, "Ivao", StringComparison.OrdinalIgnoreCase))
            services.AddVipiIvao(configuration);
        else
            throw new InvalidOperationException(
                $"DataSource:Provider '{dataSource}' non supportato. Valori validi: Ivao.");

        services.Configure<DivisionOptions>(configuration.GetSection(DivisionOptions.SectionName));
        services.Configure<NeighboursOptions>(configuration.GetSection(NeighboursOptions.SectionName));
        services.Configure<VipiChromeOptions>(configuration.GetSection(VipiChromeOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<WeatherOptions>(configuration.GetSection(WeatherOptions.SectionName));
        services.Configure<Vipi.Application.Media.MediaOptions>(configuration.GetSection(Vipi.Application.Media.MediaOptions.SectionName));
        services.Configure<Vipi.Application.ReleaseRetentionOptions>(configuration.GetSection(Vipi.Application.ReleaseRetentionOptions.SectionName));
        services.Configure<HostIdentityOptions>(configuration.GetSection(HostIdentityOptions.SectionName));

        // Template (default globale) della frase di coordinamento: file editabile «content/coordination-sentence.json».
        services.Configure<CoordinationSentenceOptions>(configuration.GetSection(CoordinationSentenceOptions.SectionName));
        services.AddSingleton<Vipi.Application.Content.ICoordinationSentenceTemplate, CoordinationSentenceTemplateProvider>();

        // Identità: dal login dell'host (default) o utente fittizio in sviluppo.
        if (useDevIdentity)
        {
            services.Configure<DevIdentityOptions>(
                configuration.GetSection(DevIdentityOptions.SectionName));
            services.AddScoped<ICurrentUserProvider, DevCurrentUserProvider>();
        }
        else
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserProvider, HostIdentityCurrentUserProvider>();
        }

        // Tracking dei login staff per il roster permessi.
        services.AddSingleton<StaffLoginThrottle>();

        // Registro dei guasti delle manutenzioni d'avvio. Singleton: è la fotografia di QUESTO avvio, scritta
        // una volta da RunVipiStartupMaintenance e letta poi dal report di consistenza.
        services.AddSingleton<Vipi.Application.Diagnostics.IStartupMaintenanceReport,
            Vipi.Application.Diagnostics.StartupMaintenanceReport>();

        // Fotografia dell'ultimo confronto coi file del sectorfile Aurora. Singleton per la stessa ragione
        // del registro qui sopra: la scrive un giro periodico, la leggono più richieste insieme — e la
        // domanda a cui risponde («le due sorgenti concordano adesso?») non ha niente da conservare fra un
        // avvio e l'altro.
        services.AddSingleton<Vipi.Application.Diagnostics.ISectorfileComparisonReport,
            Vipi.Application.Diagnostics.SectorfileComparisonReport>();

        // Bridge Aurora (F1): matching read-only + limitatore dell'endpoint anonimo.
        services.Configure<AuroraBridgeOptions>(configuration.GetSection(AuroraBridgeOptions.SectionName));
        services.AddSingleton<RequestRateLimiter>();
        services.AddSingleton<GlobalTopologyCache>();
        services.AddScoped<Vipi.Application.Content.ITransferMatchService>(sp =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuroraBridgeOptions>>().Value;
            // La topologia globale passa dalla cache SOLO qui: gli altri consumatori (AoR, coordinamenti,
            // vista live) continuano a leggerla fresca. Vedi CachedGlobalTopology.
            var topologia = new CachedGlobalTopologyProvider(
                sp.GetRequiredService<Vipi.Application.Abstractions.ITopologyProvider>(),
                sp.GetRequiredService<GlobalTopologyCache>(),
                opt.TopologyCacheTtl);

            return new Vipi.Application.Content.TransferMatchService(
                sp.GetRequiredService<Vipi.Application.Content.IAgreementService>(),
                topologia,
                sp.GetRequiredService<Vipi.Application.Content.IStationResolver>(),
                sp.GetRequiredService<Vipi.Application.Abstractions.IOnlineAtcProvider>(),
                opt.ToMatchOptions());
        });

        // Health check del modulo, in due tagli. «ready» è la sonda economica per l'orchestratore (due query);
        // «full» aggiunge il report di consistenza, che fa scansioni complete → solo su richiesta di un umano.
        // VipiReadinessCheck è anche un servizio a sé perché VipiHealthCheck lo riusa per le condizioni critiche.
        services.AddScoped<VipiReadinessCheck>();
        // Singleton: la fotografia del report vale per tutte le richieste, non per una sola. Vedi la classe.
        services.AddSingleton<ConsistencyReportCache>();
        services.AddHealthChecks()
            .AddCheck<VipiReadinessCheck>("vipi-ready", tags: new[] { ReadinessTag })
            .AddCheck<VipiHealthCheck>("vipi", tags: new[] { FullTag });

        // Localizzazione: risorse condivise in Vipi.Ui/Resources (it default, en). Incrementale.
        services.AddLocalization(o => o.ResourcesPath = "Resources");

        // Le stesse stringhe, sempre in inglese: le chiede la BRICIOLA DI PANE, che per decisione del
        // committente non segue la lingua (docs/design/regole-lingua.md R3). Singleton: non ha stato, e il
        // ResourceManager che c'è dentro è già suo di natura.
        services.AddSingleton<Vipi.Ui.EnglishStrings>();

        // Le stesse stringhe nella lingua di CHI GUARDA, anche dentro un documento bloccato in un'altra:
        // le chiede il chrome che PARLA del documento («questo è pubblicato solo in inglese»), che se lo
        // dicesse in inglese non servirebbe a chi non lo legge.
        services.AddSingleton<Vipi.Ui.StringheDelSito>();

        // ⚠️ DOPO AddLocalization, e non è un dettaglio d'ordine: questa registrazione prende il posto del
        // localizzatore per SharedResource (il generico aperto resta per tutti gli altri), e vince perché
        // arriva dopo. È il modo di far seguire la lingua del documento alle 2.487 etichette dei resx —
        // intestazioni di tabella comprese — senza toccare i 126 razor che le chiedono.
        // Scoped: dipende dal contesto di lingua, che vale per una richiesta sola.
        services.AddScoped<IStringLocalizer<Vipi.Ui.SharedResource>>(sp => new Vipi.Ui.LocalizzatoreDiLingua(
            new StringLocalizer<Vipi.Ui.SharedResource>(sp.GetRequiredService<IStringLocalizerFactory>()),
            sp.GetRequiredService<ReadingLanguageContext>()));

        // I caricatori dei documenti da leggere: uno per famiglia. Portano fuori dalle pagine il carico che
        // stava dentro il loro OnParametersSetAsync, perche' lo stesso documento va reso anche nella pagina
        // UNITA (carta docs/feature/2026-09-03-documenti-uniti.md).
        // Scoped come i servizi che consultano: LEGGONO soltanto, quindi il contesto del circuito va bene —
        // e' esattamente cio' che le pagine facevano gia' con @inject.
        services.AddScoped<Vipi.Ui.Components.Doc.AppMemberLoader>();
        services.AddScoped<Vipi.Ui.Components.Doc.MilMemberLoader>();
        // Il caricatore dei MEMBRI di un'unione: dentro istanzia i caricatori di famiglia dal service
        // provider che riceve, quindi una pagina con scope proprio lo costruisce dal SUO (ActivatorUtilities).
        services.AddScoped<Vipi.Ui.Components.Doc.UnionLoader>();
        // ⚠⚠ AirportMemberLoader NON si registra qui, ed è voluto: AeroportoPage è OwningComponentBase e
        // lo costruisce dal PROPRIO scope (ActivatorUtilities), perché i nove servizi che interroga vanno
        // presi da lì e non dal circuito — vedi il commento in testa a quella classe.

        return services;
    }

    /// <summary>Culture supportate dal modulo. ⚠️ L'elenco NON sta qui: lo dice
    /// <see cref="LinguaDiLettura.Supportate"/>, perché lo stesso elenco serve al selettore di lingua in
    /// barra, e la UI non può vedere dentro l'hosting. Due elenchi divergerebbero in silenzio — un tasto che
    /// offre una lingua non servita non dà errore, ricarica la stessa pagina.</summary>
    private static readonly string[] SupportedCultures = LinguaDiLettura.Supportate;

    /// <summary>Middleware del modulo (localizzazione + registrazione login staff nel roster).</summary>
    public static IApplicationBuilder UseVipiModule(this IApplicationBuilder app)
    {
        app.UseRequestLocalization(o => o
            .SetDefaultCulture(SupportedCultures[0])
            .AddSupportedCultures(SupportedCultures)
            .AddSupportedUICultures(SupportedCultures));
        // ⚠️ DOPO la localizzazione, e non è un dettaglio d'ordine: legge la lingua già risolta e la ricorda,
        // o il circuito Blazor — che è una seconda richiesta, senza `?culture=` — ricadrebbe su Accept-Language
        // e ridisegnerebbe in inglese una pagina chiesta in italiano. Vedi CultureCookieMiddleware.
        app.UseMiddleware<CultureCookieMiddleware>();
        app.UseMiddleware<StaffLoginTrackingMiddleware>();
        return app;
    }

    /// <summary>Richieste al minuto per IP sull'archivio ATC: un cliente onesto sincronizza, non sfoglia.</summary>
    private const int ArchivioRichiesteAlMinutoPerIp = 30;

    /// <summary>Tetto complessivo dell'archivio ATC: è quello che regge davvero, l'IP dietro il proxy lo sceglie chi chiama.</summary>
    private const int ArchivioRichiesteAlMinutoTotali = 300;

    /// <summary>Quanti IP distinti il limitatore tiene in memoria per questo endpoint.</summary>
    private const int ArchivioClientiTracciati = 5000;

    /// <summary>Endpoint del modulo: stream live SSE dell'ATC online (read-only).</summary>
    public static IEndpointRouteBuilder MapVipiModule(this IEndpointRouteBuilder endpoints)
    {
        // Quadro completo (DB + schema + consistenza dati + freschezza ATC): lo apre un umano.
        endpoints.MapHealthChecks("/vsop/health", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(FullTag),
        });
        // Sonda per l'orchestratore (healthCheckPath di Render): solo le condizioni critiche, due query.
        // Ripetuta di continuo, quindi NON deve tirarsi dietro il report di consistenza.
        endpoints.MapHealthChecks("/vsop/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadinessTag),
        });

        // Il colpetto che tiene sveglio il processo. Lo chiama il browser di chi ha una scheda aperta, ogni
        // due minuti e mezzo (vipi-riconnessione.js).
        //
        // ⚠️ NON è una sonda e non deve diventarlo: non guarda il database, non guarda niente, risponde 204 e
        // basta. Su questo hosting (Plesk + Passenger) il processo viene spento quando nessuno lo usa, e con
        // lui muoiono tutti i circuiti Blazor: è il «Attempting to reconnect to the server…» che si vede
        // mentre si sta leggendo una pagina senza toccarla. A Passenger per non spegnere serve UNA richiesta
        // qualsiasi — non una risposta interessante — e questa costa quanto un 404.
        //
        // Chi volesse sapere se il sito sta bene usa /vsop/health, che per questo fa le query: se le facesse
        // anche questo, terremmo sveglio il processo pagandolo in interrogazioni al database ogni due minuti
        // e mezzo PER SCHEDA APERTA, che è il modo di risolvere un problema comprandone un altro.
        // ⚠️ Anche HEAD, e non è pignoleria: i servizi di sorveglianza esterni (UptimeRobot e simili, che è
        // il modo previsto di tenere caldo il processo anche quando non c'è nessuno) bussano in HEAD per
        // default. Con il solo GET risponderebbe 405 — un «non funziona» che somiglia a un guasto nostro.
        // Visto dal vivo il 31 agosto 2026, provando l'endpoint appena scritto.
        endpoints.MapMethods("/vsop/ping", new[] { "GET", "HEAD" }, (HttpContext ctx) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.NoContent();
        });

        // F3: transport live SSE. Emette un evento a ogni cambio della cache ATC (+ heartbeat anti-timeout).
        // ADR-0003. Read-only, nessun dato sensibile.
        endpoints.MapGet("/vsop/live/atc", async (HttpContext ctx, OnlineAtcCache cache, CancellationToken ct) =>
        {
            // Tetto alle connessioni contemporanee. Ogni stream è una richiesta che resta aperta finché il
            // browser la tiene, e l'endpoint è pubblico e anonimo: senza un tetto, il numero di richieste
            // aperte su un processo solo — la scala decisa è UNA istanza — lo sceglie chi chiama.
            // Il numero di persone che guardano la vista live è noto e piccolo (una divisione, decine di
            // controllori): superare di molto questa soglia significa che sta succedendo altro.
            if (Interlocked.Increment(ref _sseAperti) > MaxSseConcorrenti)
            {
                Interlocked.Decrement(ref _sseAperti);
                ctx.Response.Headers.RetryAfter = "30";
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            // `using`: il semaforo alloca il proprio handle di attesa alla prima WaitAsync con timeout, che
            // è esattamente quel che fa il ciclo qui sotto. Senza Dispose restava a ogni connessione chiusa.
            using var signal = new SemaphoreSlim(0);
            void OnChanged() => signal.Release();
            cache.Changed += OnChanged;

            async Task EmitAsync()
            {
                var snap = cache.GetCurrent();
                var payload = System.Text.Json.JsonSerializer.Serialize(
                    new { asOf = snap.AsOf, count = snap.Callsigns.Count });
                await ctx.Response.WriteAsync($"data: {payload}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
            }

            try
            {
                await EmitAsync();
                while (!ct.IsCancellationRequested)
                {
                    if (await signal.WaitAsync(TimeSpan.FromSeconds(25), ct))
                        await EmitAsync();
                    else
                    {
                        await ctx.Response.WriteAsync(": ping\n\n", ct);
                        await ctx.Response.Body.FlushAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) { /* client disconnesso */ }
            finally
            {
                cache.Changed -= OnChanged;
                Interlocked.Decrement(ref _sseAperti);
            }
        });

        // Archivio delle connessioni ATC, per le macchine (carta docs/feature/2026-08-28-archivio-atc-mondiale.md).
        // Dal 28 agosto 2026 il poller registra TUTTE le postazioni aperte, non le sole italiane: questo
        // endpoint è il modo di rileggerle da fuori — nasce perché altri strumenti della divisione (il
        // validatore dei tour) tenevano un archiviatore proprio sullo stesso whazzup.
        //
        // Anonimo e in sola lettura come /vsop/live/atc, e per lo stesso motivo: è la ripetizione di un
        // dato che la sorgente pubblica già a chiunque, senza token. Quel che si aggiunge è il PASSATO, che
        // il whazzup non conserva. Tetto per IP e tetto complessivo con lo stesso limitatore del bridge: qui
        // una richiesta costa una COUNT e una pagina di righe, non un file.
        endpoints.MapGet("/vsop/api/v1/atc/sessions", async (
            HttpContext ctx,
            IAtcArchiveQueries archivio,
            RequestRateLimiter limiter,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? callsign,
            int? vid,
            bool? open,
            string? scope,
            int? limit,
            int? offset,
            CancellationToken ct) =>
        {
            if (!limiter.TryAcquire(RequestRateLimiter.GlobalKey, ArchivioRichiesteAlMinutoTotali))
            {
                ctx.Response.Headers.RetryAfter = "60";
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
            if (!limiter.TryAcquire(ip, ArchivioRichiesteAlMinutoPerIp, ArchivioClientiTracciati))
            {
                ctx.Response.Headers.RetryAfter = "60";
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            // Una finestra rovesciata non è «zero righe», è una domanda sbagliata: dirlo evita che chi
            // integra passi mezz'ora a chiedersi perché l'archivio è vuoto.
            if (from is { } f && to is { } t && f > t)
                return Results.BadRequest(new { error = "from deve precedere to" });

            var fetta = scope?.ToLowerInvariant() switch
            {
                "division" or "divisione" => AtcArchiveScope.Division,
                "world" or "mondo" => AtcArchiveScope.World,
                _ => AtcArchiveScope.All,
            };

            var pagina = await archivio.SearchAsync(new AtcArchiveFilter(
                From: from, To: to, CallsignPrefix: callsign, UserId: vid,
                OnlyOpen: open ?? false, Scope: fetta,
                Limit: limit ?? 200, Offset: offset ?? 0), ct);

            // `total` accanto alle righe: il tetto è duro (500) e senza il totale chi integra non può
            // sapere se ha in mano tutto o la prima pagina di diecimila.
            return Results.Json(new
            {
                total = pagina.Total,
                count = pagina.Rows.Count,
                sessions = pagina.Rows,
            });
        });

        // Bridge Aurora (piano docs/design/piano-aurora-bridge.md §5): dato il contesto di un volo selezionato in
        // Aurora, restituisce i punti di trasferimento candidati col livello pronto da scrivere. Read-only e
        // anonimo come i documenti da cui deriva. Nessuna scrittura: è il tool desktop, non il server, a
        // toccare Aurora — e solo su azione esplicita dell'utente.
        //
        // MONTATO SOLO SE ACCESO (AuroraBridge:Enabled, default false). È superficie pubblica e anonima su un
        // sito servito a una divisione: accenderla dev'essere una decisione di chi distribuisce il tool, non
        // la conseguenza di aver fuso un ramo. Spento, la rotta non si registra affatto — meglio che un 403,
        // che direbbe comunque che c'è qualcosa. (Il codice che ne esce dipende dal TFM: 405 su net10, dove il
        // catch-all della pagina «non trovato» di MapRazorComponents risponde al GET di qualunque path e a
        // mancare è solo il verbo; 404 su net8, che quel catch-all non ce l'ha. Vedi lo smoke E2E.)
        var bridge = endpoints.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuroraBridgeOptions>>().Value;

        if (bridge.Enabled)
        {
            endpoints.MapPost("/vsop/api/v1/transfers/resolve", async (
                Vipi.AuroraBridge.Contracts.TransferResolveRequest request,
                HttpContext ctx,
                Vipi.Application.Content.ITransferMatchService service,
                RequestRateLimiter limiter,
                Microsoft.Extensions.Options.IOptions<AuroraBridgeOptions> options,
                CancellationToken ct) =>
            {
                var opt = options.Value;

                // Il tetto complessivo viene PRIMA di quello per IP, ed è quello che regge davvero: dietro il
                // reverse proxy l'IP arriva da X-Forwarded-For, che il chiamante sceglie. Il tetto per IP
                // resta perché protegge dal caso vero — un tool in polling stretto — non dall'avversario.
                if (!limiter.TryAcquire(RequestRateLimiter.GlobalKey, opt.RequestsPerMinuteTotal))
                {
                    ctx.Response.Headers.RetryAfter = "60";
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }

                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
                if (!limiter.TryAcquire(ip, opt.RequestsPerMinutePerIp, opt.MaxTrackedClients))
                {
                    ctx.Response.Headers.RetryAfter = "60";
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }

                if (request is null || string.IsNullOrWhiteSpace(request.OwnerCallsign))
                    return Results.BadRequest(new { error = "ownerCallsign obbligatorio" });

                var result = await service.ResolveAsync(request, ct);
                return Results.Json(result);
            })
            // Il tetto del corpo si legge dalla configurazione (prima era una costante, e MaxRequestBytes non
            // lo leggeva nessuno). Letto UNA VOLTA all'avvio: cambiarlo richiede un riavvio.
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(bridge.EffectiveMaxRequestBytes));
        }

        // Immagini dei blocchi editoriali. L'URL È il contenuto (sha256), quindi la risposta si può dichiarare
        // «immutable» senza rischio di stantio: cambiare immagine significa cambiare URL, non aggiornare questa.
        // Pubblico come i documenti che la citano; il tipo servito è quello dedotto dai byte al caricamento, con
        // nosniff perché il browser non provi a interpretarlo diversamente.
        endpoints.MapGet(Vipi.Application.Content.MediaRef.UrlPrefix + "{sha}", async (string sha, IMediaStore store, HttpContext ctx, CancellationToken ct) =>
        {
            var media = await store.GetAsync(sha, ct);
            if (media is null) return Results.NotFound();

            ctx.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            ctx.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(media.Bytes, media.ContentType,
                entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{media.Sha256}\""));
        });

        // Un allegato della biblioteca: 302 verso il deposito dove stanno i byte (oggi il Drive di divisione).
        //
        // ⚠️ È QUESTA la rotta che finisce dentro i documenti, mai l'indirizzo del deposito. Il documento cita
        // lo slug, e dove stiano i byte lo decide una colonna: cambiare deposito domani non tocca un solo
        // documento. È l'unica scelta che rende reversibile un vincolo che non controlliamo — i PDF non
        // possono stare da noi per contratto, non per tecnica.
        //
        // ⚠️ `no-cache` e NON `immutable` come /vsop/media/{sha}, e la differenza è tutta qui: quello è
        // content-addressed, cioè cambiare immagine significa cambiare URL. Qui l'URL è STABILE e il
        // contenuto cambia sotto — è il senso della sostituzione. Con una cache lunga si sostituirebbe il PDF
        // e il browser terrebbe il vecchio per un anno: la sostituzione «non funziona» in modo intermittente
        // e inspiegabile, perché a chi ha la pagina fresca funziona benissimo.
        //
        // Pubblico come i documenti che lo citano: tutto ciò che entra in biblioteca è pubblico per
        // costruzione, perché il file sul Drive è condiviso «chiunque abbia il link».
        endpoints.MapGet(Vipi.Application.Content.AttachmentRules.UrlPrefix + "{slug}",
            async (string slug, IAttachmentLibrary library, HttpContext ctx, CancellationToken ct) =>
        {
            var voce = await library.BySlugAsync(slug, ct);
            if (voce is null) return Results.NotFound();

            ctx.Response.Headers.CacheControl = "no-cache";
            // 302 e non 301: un permanente lo tiene il browser per sempre, e il giorno che cambia il deposito
            // ci sarebbero utenti mandati a un indirizzo morto senza modo di correggerli.
            return Results.Redirect(
                Vipi.Application.Content.AttachmentRules.UrlEsterno(voce.Provider, voce.ExternalId),
                permanent: false);
        });

        return endpoints;
    }

    /// <summary>
    /// Crea/migra il database del modulo all'avvio. Ogni provider ha una strategia diversa e non
    /// intercambiabile:
    /// <list type="bullet">
    ///   <item><description><b>SQLite</b> — migrazioni versionate del repo (<c>Migrate</c>).</description></item>
    ///   <item><description><b>PostgreSQL</b> (Render+Neon) — quelle migrazioni sono SQLite-flavored e non
    ///   girano, quindi lo schema lo crea e lo allinea <see cref="PostgresSchemaReconciler.InitializeSchema"/>,
    ///   che serializza l'operazione fra istanze e aggiunge tabelle, colonne e indici nuovi (<c>EnsureCreated</c>
    ///   da solo non tocca un database che ha già tabelle).</description></item>
    ///   <item><description><b>MySQL</b> (produzione) — set di migrazioni dedicato, indicato alla DI con
    ///   <c>MigrationsAssembly</c>: <c>Migrate</c> applica quelle, non le SQLite.</description></item>
    /// </list>
    ///
    /// <para>⚠️ <b>Il dispatch è esplicito sui tre provider e il ramo sconosciuto lancia.</b> Prima era
    /// <c>if (Npgsql) reconcile else Migrate()</c>, dove l'<c>else</c> significava «SQLite» per convenzione
    /// non scritta. Con MySQL configurato quel ramo avrebbe applicato le 68 migrazioni SQLite-flavored a
    /// MySQL — cioè la cosa peggiore possibile su un database di produzione, e senza che nulla lo
    /// annunciasse. Un provider nuovo deve fermare l'avvio, non ereditare la strategia di un altro.</para>
    /// </summary>
    public static IHost MigrateVipiDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();

        // Si guarda il ProviderName invece di rileggere la configurazione: evita di referenziare Npgsql e
        // MySql da Vipi.Hosting, che li conosce solo Infrastructure, e descrive il contesto REALE — se la
        // DI e la config divergessero, qui conta come il DbContext è stato costruito davvero.
        var provider = db.Database.ProviderName ?? "";

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var log = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()
                ?.CreateLogger(typeof(PostgresSchemaReconciler).FullName!);
            PostgresSchemaReconciler.InitializeSchema(db, log);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                 provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            // ⚠️ PRIMA di migrare: l'unico indice unico della coda che possa trovare dati già in conflitto
            // è quello dei numeri di rilascio. Senza questo controllo il guasto arriva da dentro una
            // migrazione a metà, come un «Duplicate entry ... for key ...» che dice la chiave e non le
            // righe — su un host dove l'unico canale è scaricare `avvio-errore.txt` via FTP.
            ReleaseNumberPreflight.Verifica(db);

            db.Database.Migrate();
        }
        else
        {
            throw new InvalidOperationException(
                $"Provider di persistenza '{provider}' senza una strategia di creazione dello schema. " +
                "Aggiungerne una esplicita in MigrateVipiDatabase: le migrazioni del repo sono " +
                "SQLite-flavored e applicarle a un provider diverso corrompe lo schema. " +
                "Vedi docs/adr/adr-0007-produzione-persistenza-e-scala.md.");
        }

        return host;
    }

    /// <summary>
    /// Esegue le quattro manutenzioni d'avvio <b>non critiche</b>, ognuna isolata dalle altre: se una
    /// fallisce viene registrata e l'avvio prosegue con le successive.
    ///
    /// <para><b>Perché non basta lasciarle esplodere.</b> Sono passate idempotenti che rigirano a ogni
    /// avvio, e sono l'ultimo pezzo di <c>Program.cs</c> prima che l'app cominci a servire. Con
    /// <c>Restart=always</c> e <c>RestartSec=10</c> in <c>vipi.service</c>, un guasto lì non è un degrado:
    /// è un <b>ciclo di riavvii</b>, cioè il sito giù per un difetto in una riconciliazione di dati storici.
    /// Un sito che parte con una riconciliazione saltata è sempre meglio di un sito che non parte.</para>
    ///
    /// <para><b>Perché proseguire è lecito.</b> Ognuna è idempotente e nessuna è un prerequisito
    /// dell'altra — le dipendenze d'ordine vere stanno <i>dentro</i>
    /// <see cref="ReconcileVipiDocuments"/>, che resta atomica dal punto di vista di chi chiama: se fallisce
    /// a metà, salta per intero. E un riavvio riuscito le rifà da capo.</para>
    ///
    /// <para>⚠️ <b><see cref="MigrateVipiDatabase"/> resta fuori, e deve restarci.</b> Lì un guasto è
    /// critico: proseguire significherebbe servire pagine su uno schema che non è quello che il codice si
    /// aspetta, e il difetto uscirebbe come colonna mancante a runtime, lontano dalla causa.</para>
    ///
    /// <para>Il guasto non finisce solo nel log: passa da <c>IStartupMaintenanceReport</c> al report di
    /// consistenza, quindi si vede in <c>/services/vsop/admin/diagnostics</c> e manda <c>/vsop/health</c> in
    /// Degraded. Un «logga e prosegui» che si ferma al log è un modo per non accorgersene mai.</para>
    /// </summary>
    public static IHost RunVipiStartupMaintenance(this IHost host)
    {
        var log = host.Services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()
            ?.CreateLogger("Vipi.StartupMaintenance");
        var report = host.Services.GetService<Vipi.Application.Diagnostics.IStartupMaintenanceReport>();

        // PRIMA delle riconciliazioni, perché è l'unica che serve a qualcuno appena il sito comincia a
        // servire: finché non è girata, chi è stato promosso a mano vale quanto dice la sua posizione staff.
        Isolata(host, log, report, "promozioni a mano in memoria", h => h.LoadVipiRoleOverrides());
        Isolata(host, log, report, "riconciliazioni documentali", h => h.ReconcileVipiDocuments());
        Isolata(host, log, report, "proiezione dei settori dai cataloghi", h => h.ProjectVipiSectors());
        Isolata(host, log, report, "backfill delle release effettive", h => h.BackfillVipiReleases());
        Isolata(host, log, report, "pulizia delle unioni di documenti", h => h.TidyVipiDocumentUnions());
        // ⚠️ La potatura delle release NON è più qui: dal 2 settembre 2026 la fa `ReleaseSweepHostedService`
        // ogni 24 ore (carta 2026-09-02-il-ciclo-entrante.md §AW4). All'avvio girava una volta sola, e gli
        // stati delle release invecchiano DA SOLI — al rollover AIRAC una schedulata entra in vigore senza
        // che nessuno scriva niente —, quindi su un processo che resta su per settimane non si potava più
        // niente. Tenerla anche qui sarebbe lo stesso lavoro fatto da due parti: il giro copre l'avvio
        // (parte a 130s) e tutti i giorni dopo.

        return host;
    }

    private static void Isolata(IHost host, Microsoft.Extensions.Logging.ILogger? log,
        Vipi.Application.Diagnostics.IStartupMaintenanceReport? report, string nome, Func<IHost, IHost> passata)
    {
        try
        {
            passata(host);
        }
        catch (Exception ex)
        {
            report?.Record(nome, ex);
            if (log is not null)
                Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                    log, ex,
                    "Manutenzione d'avvio «{Passata}» fallita: l'avvio prosegue e la segnalazione entra nella " +
                    "diagnostica. È idempotente: un riavvio riuscito la rifà.", nome);
        }
    }

    /// <summary>
    /// Legge le promozioni a mano e le mette in memoria (<c>IRoleOverrides</c>). Carta
    /// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §6.
    ///
    /// <para><b>Perché all'avvio e non alla prima domanda.</b> «Che livello ha questa persona?» si chiede
    /// dentro il markup, dove non si può attendere: la risposta dev'essere già in memoria quando arriva la
    /// prima richiesta. Ed è anche il motivo per cui non è una query per richiesta — quella la
    /// pagherebbe il layout di ogni pagina, che è il posto da cui sono già uscite due volte le corse sul
    /// <c>DbContext</c> di circuito.</para>
    ///
    /// <para><b>Perché può fallire senza fermare l'avvio.</b> Il fotogramma vuoto non nega niente a
    /// nessuno: chi ha un livello per posizione staff ce l'ha comunque, e a mancare sono solo le
    /// promozioni scritte a mano. Un fastidio, non un guasto — e la segnalazione finisce in diagnostica.
    /// Il contrario (fermare l'avvio) trasformerebbe una tabella illeggibile nel sito giù.</para>
    /// </summary>
    public static IHost LoadVipiRoleOverrides(this IHost host)
    {
        var overrides = host.Services.GetRequiredService<Vipi.Application.Auth.IRoleOverrides>();
        overrides.ReloadAsync().GetAwaiter().GetResult();
        return host;
    }

    /// <summary>Riconciliazioni documentali one-shot (doc 11): chiavi univoche per le sezioni libere nate con la
    /// chiave storica <c>"custom"</c>. Idempotente: sicuro a ogni avvio.</summary>
    public static IHost ReconcileVipiDocuments(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IDocumentMaintenance>();
        var log = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()
            ?.CreateLogger("Vipi.DocumentMaintenance");

        // PRIMA di tutto il resto: è il legame che tutte le letture del documento d'aeroporto useranno da qui in
        // avanti. Un passo che lo presupponesse, girando prima, lavorerebbe su aeroporti ancora scollegati.
        var collegati = maintenance.LinkAirportDocumentsAsync().GetAwaiter().GetResult();
        if (collegati > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Collegati {Count} aeroporti alla loro vIPI (il legame passa dall'aeroporto, non piu' dai settori).", collegati);

        var keys = maintenance.ReconcileCustomSectionKeysAsync().GetAwaiter().GetResult();
        if (keys > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Riconciliate {Count} sezioni libere con chiave storica «custom».", keys);

        // DOPO la riconciliazione delle chiavi: le voci storiche "custom" non identificano più una sola sezione.
        var hidden = maintenance.MigrateHiddenSectionsAsync().GetAwaiter().GetResult();
        if (hidden > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Migrate {Count} sezioni nascoste sul flag versionato della sezione.", hidden);

        // vLOA sulle chiavi del catalogo (doc 13 §3c): direzioni dei coordinamenti e «Purpose».
        var vloaKeys = maintenance.ReconcileVloaSectionKeysAsync().GetAwaiter().GetResult();
        if (vloaKeys > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Riconciliate {Count} sezioni vLOA sulle chiavi del catalogo.", vloaKeys);

        // Aeroporti sulle chiavi del catalogo (carta 2026-08-26 §3). ⚠️ PRIMA di AddMissingCatalogSections: le
        // sezioni cotte hanno chiavi casuali, e chi cercasse quelle mancanti prima di questo passo non ne
        // riconoscerebbe nessuna — le aggiungerebbe tutte e otto accanto a quelle che ci sono già.
        var airportKeys = maintenance.ReconcileAirportSectionKeysAsync().GetAwaiter().GetResult();
        if (airportKeys > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Riconciliate {Count} sezioni d'aeroporto sulle chiavi del catalogo.", airportKeys);

        // Sezioni fisse del catalogo assenti dai documenti APP/vLOA/aeroporto già creati (doc 13 §3d).
        var catalog = maintenance.AddMissingCatalogSectionsAsync().GetAwaiter().GetResult();
        if (catalog > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Aggiunte {Count} sezioni di catalogo mancanti ai documenti APP/vLOA/aeroporto.", catalog);

        // vLOA: via la riga «Effective from — AIRAC ####» seminata a mano (doc 14 §3b). ⚠️ DOPO
        // AddMissingCatalogSections: se la sezione «validity» mancasse ancora, non ci sarebbe la tabella da
        // ripulire e il passo girerebbe a vuoto proprio sui documenti che ne hanno bisogno.
        var airacRighe = maintenance.ClearVloaSeededAiracRowAsync().GetAwaiter().GetResult();
        if (airacRighe > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Tolte {Count} righe «Effective from — AIRAC» scritte a mano nelle vLOA: il ciclo lo dice il timbro della release.", airacRighe);

        // Il puntatore «versione pubblicata corrente» dove punta a una BOZZA (doc 14 §3i). ⚠️ DOPO le
        // riconciliazioni di struttura: se una di quelle creasse o promuovesse una versione, questo giro
        // dovrebbe vedere il risultato, non lo stato di prima.
        var puntatori = maintenance.ClearUnpublishedCurrentVersionAsync().GetAwaiter().GetResult();
        if (puntatori > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Azzerati {Count} puntatori «versione pubblicata» che indicavano una bozza: quel campo lo scrive la pubblicazione.", puntatori);

        // La sezione delle minime di vettoramento si chiama «MRVA», e uguale in tutte e due le lingue: il
        // titolo di una sezione di catalogo sta NEL DOCUMENTO, quindi cambiare il catalogo vale solo per i
        // documenti nuovi e questo passo porta avanti quelli già scritti.
        var mrva = maintenance.RenameMinimaSectionsAsync().GetAwaiter().GetResult();
        if (mrva > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Rinominate {Count} sezioni «minima» in MRVA.", mrva);

        // «Minime di vettoramento» è tornata editoriale (doc 13 §3b): via i blocchi placeholder vuoti che aveva
        // da derivata, o l'editor mostrerebbe una tabella senza colonne.
        var minima = maintenance.ClearMinimaPlaceholderBlocksAsync().GetAwaiter().GetResult();
        if (minima > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Rimossi {Count} blocchi placeholder dalle sezioni «minima».", minima);

        // Aree regolamentate: appartenenza agli ACC dalla vecchia colonna singola alla tabella dei legami.
        var areas = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.ISpecialAreaMaintenance>();
        var links = areas.BackfillAreaCentersAsync().GetAwaiter().GetResult();
        if (links > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Ricostruiti {Count} legami area regolamentata→ACC dalla colonna storica.", links);

        // Righe di catalogo aggiunte a mano: vanno marcate PRIMA che il controllo del timbro giri, o le
        // scambia per «sparite dalla sorgente» (una persona le ha messe, e la sorgente non le ristampa mai).
        var catalogo = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.ISectorCatalogMaintenance>();
        var manuali = catalogo.MarkManualCatalogRowsAsync().GetAwaiter().GetResult();
        if (manuali > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Cataloghi: marcate {Count} righe come aggiunte a mano (mai arrivate dalla sorgente).", manuali);

        // DOPO il backfill: la potatura degli esteri lavora sui legami, che prima non esistevano.
        var dropped = areas.OptOutForeignAreasAsync().GetAwaiter().GetResult();
        if (dropped > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Aree regolamentate: spenti gli ACC esteri e liberati {Count} legami (riabilitabili a mano).", dropped);
        return host;
    }

    /// <summary>
    /// Riallinea i <c>Sector</c> proiettati ai cataloghi all'avvio. La proiezione è idempotente e gira già dopo
    /// ogni import e ogni modifica alla gerarchia; qui serve a far entrare in vigore i cambi alla REGOLA di
    /// derivazione senza aspettare il prossimo import (es. 2026-07-31: la scaletta DEL→GND→TWR→APP che aggancia
    /// le posizioni d'aeroporto al padre dell'aeroporto — senza questa passata i settori già proiettati
    /// resterebbero orfani fino all'import automatico del giorno dopo).
    /// </summary>
    public static IHost ProjectVipiSectors(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Vipi.Application.Abstractions.ISectorProjectionService>()
            .SyncFromCatalogsAsync().GetAwaiter().GetResult();
        return host;
    }

    /// <summary>Migrazione A (doc 10 §3f): backfilla una release effettiva per ogni documento pubblicato senza copia
    /// congelata, così la visibilità pubblica = release effettiva non lascia buchi. Idempotente: sicuro a ogni avvio.</summary>
    public static IHost BackfillVipiReleases(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IReleaseService>()
            .BackfillMissingReleasesAsync().GetAwaiter().GetResult();
        return host;
    }

    /// <summary>
    /// Chiude le unioni di documenti rimaste con meno di due membri (carta
    /// <c>docs/feature/2026-09-03-documenti-uniti.md</c>). Idempotente: sicuro a ogni avvio.
    ///
    /// <para>⚠️ La cascata della FK toglie già la riga di appartenenza insieme al documento eliminato; quel
    /// che resta da chiudere è l'<b>unione</b> che quella riga teneva in piedi — una pagina unita che unisce
    /// sé stessa, con un redirect che non ha dove mandare. E un documento non sparisce solo dal tasto
    /// «elimina»: una riga tolta a mano dal database o un rollback lo fanno lo stesso.</para>
    /// </summary>
    public static IHost TidyVipiDocumentUnions(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Vipi.Application.Abstractions.IDocumentUnionRepository>()
            .TidyAsync().GetAwaiter().GetResult();
        return host;
    }
}
