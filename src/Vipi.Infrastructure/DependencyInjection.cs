using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure;

/// <summary>Registra la persistenza SQLite e i servizi infrastrutturali della vIPI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVipiInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<VipiDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.ITopologyProvider, TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.IContentRepository, EfContentRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditingRepository, EfEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.ITopologyEditingRepository, EfTopologyEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStructureEditingRepository, EfStructureEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportProfileRepository, EfAirportProfileRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStationDirectory, EfStationDirectory>();
        services.AddScoped<Vipi.Application.Abstractions.ITransferRepository, EfTransferRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditGrantRepository, EfEditGrantRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStaffRosterRepository, EfStaffRosterRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAuditLogReader, EfAuditLogReader>();
        services.AddScoped<Vipi.Application.Abstractions.ISearchRepository, EfSearchRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IChangesRepository, EfChangesRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IImportPolicyStore, EfImportPolicyStore>();
        services.AddScoped<Vipi.Application.Abstractions.IAccAdminRepository, EfAccAdminRepository>();
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
        return services;
    }
}
