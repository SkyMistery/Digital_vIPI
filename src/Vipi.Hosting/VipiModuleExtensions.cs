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

        // Rotte viewer/editor per tipo di documento (doc 09 §3b): i consumatori UI consultano il registry.
        services.AddSingleton<Vipi.Ui.Shared.Routing.IDocKindRoutes, Vipi.Ui.Shared.Routing.VloaDocRoutes>();
        services.AddSingleton<Vipi.Ui.Shared.Routing.IDocKindRoutes, Vipi.Ui.Shared.Routing.AppDocRoutes>();
        services.AddSingleton<Vipi.Ui.Shared.Routing.IDocKindRoutes, Vipi.Ui.Shared.Routing.AccVipiDocRoutes>();
        services.AddSingleton<Vipi.Ui.Shared.Routing.IDocKindRoutes, Vipi.Ui.Shared.Routing.AirportDocRoutes>();
        services.AddSingleton<Vipi.Ui.Shared.Routing.IDocRoutesRegistry, Vipi.Ui.Shared.Routing.DocRoutesRegistry>();

        // Tracking dei login staff per il roster permessi.
        services.AddSingleton<StaffLoginThrottle>();

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
                sp.GetRequiredService<Vipi.Application.Abstractions.ITransferRepository>(),
                topologia,
                sp.GetRequiredService<Vipi.Application.Content.IStationResolver>(),
                sp.GetRequiredService<Vipi.Application.Abstractions.IOnlineAtcProvider>(),
                opt.ToMatchOptions());
        });

        // Health check del modulo, in due tagli. «ready» è la sonda economica per l'orchestratore (due query);
        // «full» aggiunge il report di consistenza, che fa scansioni complete → solo su richiesta di un umano.
        // VipiReadinessCheck è anche un servizio a sé perché VipiHealthCheck lo riusa per le condizioni critiche.
        services.AddScoped<VipiReadinessCheck>();
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

        // F3: transport live SSE. Emette un evento a ogni cambio della cache ATC (+ heartbeat anti-timeout).
        // ADR-0003. Read-only, nessun dato sensibile.
        endpoints.MapGet("/vsop/live/atc", async (HttpContext ctx, OnlineAtcCache cache, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var signal = new SemaphoreSlim(0);
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
            finally { cache.Changed -= OnChanged; }
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

    /// <summary>Riconciliazioni documentali one-shot (doc 11): chiavi univoche per le sezioni libere nate con la
    /// chiave storica <c>"custom"</c>. Idempotente: sicuro a ogni avvio.</summary>
    public static IHost ReconcileVipiDocuments(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IDocumentMaintenance>();
        var log = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()
            ?.CreateLogger("Vipi.DocumentMaintenance");

        var keys = maintenance.ReconcileCustomSectionKeysAsync().GetAwaiter().GetResult();
        if (keys > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Riconciliate {Count} sezioni libere con chiave storica «custom».", keys);

        // DOPO la riconciliazione delle chiavi: le voci storiche "custom" non identificano più una sola sezione.
        var hidden = maintenance.MigrateHiddenSectionsAsync().GetAwaiter().GetResult();
        if (hidden > 0 && log is not null)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                log, "Migrate {Count} sezioni nascoste sul flag versionato della sezione.", hidden);

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
