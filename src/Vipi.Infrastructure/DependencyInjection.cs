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
                // Cutover Postgres NON ancora attuato (ADR-0007): servono un assembly di migrazioni dedicato
                // (le 60 migrazioni attuali sono SQLite-flavored) + validazione su istanza reale + revisione dei
                // punti provider-specifici (RowVersion, tipi). Per abilitarlo: aggiungere il pacchetto
                // Npgsql.EntityFrameworkCore.PostgreSQL e sostituire questo throw con:
                //   services.AddDbContext<VipiDbContext>(o => o.UseNpgsql(connectionString,
                //       npg => npg.MigrationsAssembly("Vipi.Infrastructure.Postgres")));
                throw new InvalidOperationException(
                    "Persistence:Provider 'Postgres' selezionato ma il cutover non è ancora attuato " +
                    "(mancano le migrazioni dedicate e la validazione su istanza). Vedi docs/adr/adr-0007-produzione-persistenza-e-scala.md.");

            default:
                throw new InvalidOperationException($"Provider di persistenza non gestito: {provider}.");
        }
        services.AddScoped<Vipi.Application.Abstractions.IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.ITopologyProvider, TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.IContentRepository, EfContentRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditingRepository, EfEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IResourceLockRepository, EfResourceLockRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStructureEditingRepository, EfStructureEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportRepository, EfAirportRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAppDerivationRepository, EfAppDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAccDerivationRepository, EfAccDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStationDirectory, EfStationDirectory>();
        services.AddScoped<Vipi.Application.Abstractions.ITransferRepository, EfTransferRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditGrantRepository, EfEditGrantRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStaffRosterRepository, EfStaffRosterRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAuditLogReader, EfAuditLogReader>();
        services.AddScoped<Vipi.Application.Abstractions.IEditAuditWriter, EfEditAuditWriter>();
        services.AddScoped<Vipi.Application.Abstractions.ISearchRepository, EfSearchRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IChangesRepository, EfChangesRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IImportPolicyStore, EfImportPolicyStore>();
        services.AddScoped<Vipi.Application.Abstractions.IImportStateStore, EfImportStateStore>();
        services.AddScoped<Vipi.Application.Abstractions.IConsistencyReportRepository, EfConsistencyReportRepository>();
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
        services.AddHttpClient<Vipi.Application.Abstractions.ISidProvider, Sectorfile.AuroraSidProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddHostedService<Sectorfile.SidImportHostedService>();
        return services;
    }
}
