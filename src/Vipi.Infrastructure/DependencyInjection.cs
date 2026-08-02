using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure;

/// <summary>Registra la persistenza (provider selezionabile via <c>Persistence:Provider</c>, default SQLite) e i servizi infrastrutturali della vIPI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVipiInfrastructure(this IServiceCollection services, string connectionString)
        => services.AddVipiInfrastructure(connectionString, null);

    public static IServiceCollection AddVipiInfrastructure(this IServiceCollection services, string connectionString,
        Microsoft.Extensions.Configuration.IConfiguration? configuration)
    {
        // Selezione provider di persistenza (ADR-0007): default SQLite; Postgres pianificato (cutover non attuato).
        var provider = Persistence.PersistenceProviderResolver.Resolve(
            configuration?[Persistence.PersistenceProviderResolver.ProviderConfigKey]);

        switch (provider)
        {
            case Persistence.PersistenceProvider.Sqlite:
                // Tampone concorrenza SQLite (A1): WAL + busy_timeout a ogni apertura connessione. Vedi SqliteTuningInterceptor.
                services.AddDbContext<VipiDbContext>(o => o
                    // Query con >1 Include di collection: split in più SELECT (default consigliato MS) invece del
                    // JOIN cartesiano di SingleQuery. Toglie il warning EF 20504 e migliora la perf su tali query.
                    .UseSqlite(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .AddInterceptors(new Persistence.SqliteTuningInterceptor()));
                break;

            case Persistence.PersistenceProvider.Postgres:
                // Deploy hostato (Render + Neon): le 60 migrazioni sono SQLite-flavored e non girano su Postgres,
                // quindi lo schema si crea via EnsureCreated in MigrateVipiDatabase (no cronologia migrazioni).
                // Adeguato a un DB test/fresco; NON usare EnsureCreated e Migrate insieme sullo stesso DB.
                services.AddDbContext<VipiDbContext>(o => o
                    .UseNpgsql(connectionString, npg => npg
                        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                        // Neon (serverless) sospende il compute e chiude le connessioni idle: la prima query
                        // dopo l'inattività fallisce "transient". Ritenta in automatico (execution strategy).
                        // Retry-safe: EfUnitOfWork avvolge le transazioni in CreateExecutionStrategy() E azzera il
                        // change-tracker a ogni tentativo (il rollback non lo ripulisce). Vedi EfUnitOfWork.
                        .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)));
                break;

            default:
                throw new InvalidOperationException($"Provider di persistenza non gestito: {provider}.");
        }
        services.AddScoped<Vipi.Application.Abstractions.IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.ITopologyProvider, TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.IContentRepository, EfContentRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditingRepository, EfEditingRepository>();
        services.AddScoped<Vipi.Application.Content.IDocumentMaintenance, EfDocumentMaintenance>();
        services.AddScoped<Vipi.Application.Abstractions.IResourceLockRepository, EfResourceLockRepository>();
        // Immagini dei blocchi: i byte stanno nel DB. Spostarli altrove (object storage) = cambiare questa riga.
        services.AddScoped<Vipi.Application.Abstractions.IMediaStore, EfMediaStore>();
        services.AddScoped<Vipi.Application.Media.IMediaMaintenance, EfMediaMaintenance>();
        services.AddScoped<Vipi.Application.Abstractions.IStructureEditingRepository, EfStructureEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportRepository, EfAirportRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAppDerivationRepository, EfAppDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAccDerivationRepository, EfAccDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.ISpecialAreaRepository, EfSpecialAreaRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStationDirectory, EfStationDirectory>();
        services.AddScoped<Vipi.Application.Abstractions.ITransferRepository, EfTransferRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditGrantRepository, EfEditGrantRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStaffRosterRepository, EfStaffRosterRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAuditLogReader, EfAuditLogReader>();
        services.AddScoped<Vipi.Application.Abstractions.ISearchRepository, EfSearchRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IChangesRepository, EfChangesRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IImportPolicyStore, EfImportPolicyStore>();
        services.AddScoped<Vipi.Application.Abstractions.IImportStateStore, EfImportStateStore>();
        services.AddScoped<Vipi.Application.Abstractions.IConsistencyReportRepository, EfConsistencyReportRepository>();
        // Drift di schema: registrato sempre, si disattiva da sé fuori da Npgsql (dove le migrazioni EF girano
        // davvero e il drift non si accumula). Confluisce nel report di consistenza. Vedi ADR-0007.
        services.AddScoped<Vipi.Application.Diagnostics.ISchemaDriftProbe, Persistence.PostgresSchemaDriftProbe>();
        services.AddScoped<Vipi.Application.Abstractions.IAccAdminRepository, EfAccAdminRepository>();
        services.AddScoped<Vipi.Application.Abstractions.INeighbourRepository, EfNeighbourRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IVloaDerivationRepository, EfVloaDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentProfileRepository, EfDocumentProfileRepository>();
        // Descrittori per-tipo del flusso di pubblicazione (doc 09 §3a): i motori generici consultano il registry.
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.VloaReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AppReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AccVipiReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AirportReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTargetRegistry, Vipi.Application.Content.ReleaseTargetRegistry>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseRepository, EfReleaseRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditorTaskRepository, EfEditorTaskRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentReviewRepository, EfDocumentReviewRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentAdminRepository, EfDocumentAdminRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportSectorRepository, EfAirportSectorRepository>();
        // Proiezione settori operativi dai cataloghi (fonte autoritativa unica, Round 20).
        services.AddScoped<Vipi.Application.Abstractions.ISectorProjectionService, EfSectorProjectionService>();
        services.AddScoped<Vipi.Application.Abstractions.IHierarchyEditingService, EfHierarchyEditingService>();

        // Meteo reale (NOAA aviationweather.gov): HttpClient con UA + provider singleton (cache TTL per ICAO).
        services.AddHttpClient(Weather.NoaaWeatherClient.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddSingleton<Vipi.Application.Abstractions.IWeatherProvider, Weather.NoaaWeatherClient>();

        // Import SID dal sectorfile Aurora su GitHub (repo pubblico raw, no auth). Ortogonale a DataSource:Provider.
        services.AddScoped<Vipi.Application.Abstractions.ISidFixAliasRepository, EfSidFixAliasRepository>();
        if (configuration is not null)
            services.Configure<Sectorfile.SectorfileOptions>(configuration.GetSection("Sectorfile"));
        // Cache dei file di sectorfile indipendenti dall'aeroporto (navaid, poligoni TWR). DEVE essere singleton:
        // gli adapter sotto sono transient (AddHttpClient<,>), quindi una cache in campo d'istanza sarebbe
        // per-risoluzione e il suo lock non sincronizzerebbe nulla. Vedi SectorfileCache.
        services.AddSingleton<Sectorfile.SectorfileCache>();
        services.AddHttpClient<Vipi.Application.Abstractions.ISidProvider, Sectorfile.AuroraSidProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddHostedService<Sectorfile.SidImportHostedService>();

        // Shape TWR reali dal file poligoni Aurora (twrs.tfl) su GitHub: stesso repo raw pubblico dell'import SID.
        services.AddHttpClient<Vipi.Application.Abstractions.ITowerShapeSource, Sectorfile.AuroraTowerShapeProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        return services;
    }
}
