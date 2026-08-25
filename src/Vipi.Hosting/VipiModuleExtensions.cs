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
using Vipi.Application;
using Vipi.Application.Abstractions;
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

        return services;
    }

    /// <summary>Culture supportate dal modulo (it default, en).</summary>
    private static readonly string[] SupportedCultures = { "it", "en" };

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

        // Esportazione delle proprie sessioni ATC. ⚠️ Solo le PROPRIE, e solo per chi è entrato: l'endpoint
        // legge il VID dall'identità, non da un parametro — un parametro qui vorrebbe dire «le statistiche di
        // chiunque a chi ne indovina il numero».
        // ⚠️ E resta così anche dopo che /services/stats/user/{vid} ha aperto allo staff le statistiche
        // altrui: là c'è una guardia (`Authz.IsAdmin`) e una riga di audit, qui non ci sarebbe nessuna delle
        // due. Chi volesse l'esportazione altrui deve portarsi dietro entrambe, non aggiungere un parametro.
        endpoints.MapGet("/services/stats/export.csv", async (
            HttpContext ctx,
            Vipi.Application.Abstractions.ICurrentUserProvider utenti,
            Vipi.Application.Abstractions.IAtcStatsQueries stats,
            CancellationToken ct) =>
        {
            var utente = utenti.Get();
            if (utente is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var a = DateTimeOffset.UtcNow;
            var righe = await stats.SessionsAsync(utente.UserId, a.AddDays(-366), a, limit: 5000, ct);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("inizio_utc;fine_utc;callsign;posizione;frequenza;durata_secondi;turno;movimenti;presenze;minuti_con_traffico");
            foreach (var r in righe)
                sb.AppendLine(string.Join(';', new[]
                {
                    r.StartUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    r.EndUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                    r.Callsign, r.Position ?? "", r.Frequency ?? "",
                    r.DurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    r.ShiftKey.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    r.MovementCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    r.TrafficCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    r.TrafficMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }));

            ctx.Response.ContentType = "text/csv; charset=utf-8";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename=statistiche-atc-{utente.UserId}.csv";
            // Il BOM non è un vezzo: senza, Excel apre un CSV UTF-8 con gli accenti rotti.
            await ctx.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetPreamble(), ct);
            await ctx.Response.WriteAsync(sb.ToString(), System.Text.Encoding.UTF8, ct);
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

        Isolata(host, log, report, "riconciliazioni documentali", h => h.ReconcileVipiDocuments());
        Isolata(host, log, report, "proiezione dei settori dai cataloghi", h => h.ProjectVipiSectors());
        Isolata(host, log, report, "backfill delle release effettive", h => h.BackfillVipiReleases());
        Isolata(host, log, report, "potatura delle release superate", h => h.PruneVipiReleases());

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

        // Sezioni fisse del catalogo assenti dai documenti APP/vLOA già creati (doc 13 §3d).
        var catalog = maintenance.AddMissingCatalogSectionsAsync().GetAwaiter().GetResult();
        if (catalog > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Aggiunte {Count} sezioni di catalogo mancanti ai documenti APP/vLOA.", catalog);

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

    /// <summary>Retention pubblicazione: pota una volta all'avvio release Superseded oltre soglia e versioni Archived
    /// oltre N (contiene l'accumulo storico; poi il per-publish lo mantiene limitato). Idempotente.</summary>
    public static IHost PruneVipiReleases(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IReleaseService>()
            .PruneAllAsync().GetAwaiter().GetResult();
        return host;
    }
}
