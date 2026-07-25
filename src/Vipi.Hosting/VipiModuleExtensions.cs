using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

        // Health check del modulo (DB + freschezza cache ATC).
        services.AddHealthChecks().AddCheck<VipiHealthCheck>("vipi");

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
        endpoints.MapHealthChecks("/vsop/health");

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

        return endpoints;
    }

    /// <summary>Crea/migra il database del modulo all'avvio. SQLite: migrazioni versionate (Migrate). Postgres
    /// (deploy hostato Render+Neon): le migrazioni sono SQLite-flavored ⇒ schema creato da modello (EnsureCreated).</summary>
    public static IHost MigrateVipiDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
        // ProviderName evita di referenziare Npgsql da Vipi.Hosting (lo conosce solo Infrastructure).
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            db.Database.EnsureCreated();
        else
            db.Database.Migrate();
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
