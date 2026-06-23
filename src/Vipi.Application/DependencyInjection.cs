using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain.Services;

namespace Vipi.Application;

/// <summary>Registra i servizi puri dell'Application/Domain layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVipiApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAiracService, AiracService>();
        services.AddSingleton<IAorService, AorService>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IStationResolver, StationResolver>();
        services.AddScoped<IVipiViewService, VipiViewService>();
        services.AddScoped<Auth.IEditAuthorizationService, Auth.EditAuthorizationService>();
        services.AddScoped<IEditingService, EditingService>();
        services.AddScoped<Aor.ITopologyEditingService, Aor.TopologyEditingService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChangesService, ChangesService>();
        return services;
    }
}
