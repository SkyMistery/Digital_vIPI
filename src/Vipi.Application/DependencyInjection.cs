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
        services.AddScoped<IStationResolver, StationResolver>();   // scoped: legge le ACC dal DB
        services.AddScoped<IVipiViewService, VipiViewService>();
        services.AddScoped<Auth.IEditAuthorizationService, Auth.EditAuthorizationService>();
        services.AddScoped<Auth.IStaffRosterService, Auth.StaffRosterService>();
        services.AddScoped<IEditingService, EditingService>();
        services.AddScoped<IStructureEditingService, StructureEditingService>();
        services.AddScoped<IAccAdminService, AccAdminService>();
        services.AddScoped<IAirportProfileService, AirportProfileService>();
        services.AddScoped<IAppProfileService, AppProfileService>();
        services.AddScoped<IAirportSectorImporter, AirportSectorImporter>();
        services.AddScoped<ITowerShapeFallbackService, TowerShapeFallbackService>();
        services.AddScoped<IAirportSectorService, AirportSectorService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChangesService, ChangesService>();
        services.AddScoped<IImportPolicyService, ImportPolicyService>();
        return services;
    }
}
