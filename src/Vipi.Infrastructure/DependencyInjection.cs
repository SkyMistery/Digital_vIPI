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
        services.AddScoped<Vipi.Application.Abstractions.ITransferRepository, EfTransferRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditGrantRepository, EfEditGrantRepository>();
        services.AddScoped<Vipi.Application.Abstractions.ISearchRepository, EfSearchRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IChangesRepository, EfChangesRepository>();
        return services;
    }
}
