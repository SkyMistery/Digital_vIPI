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
        services.AddScoped<IAccImportUseCase, AccImportUseCase>();
        services.AddScoped<ISpecialAreaImportUseCase, SpecialAreaImportUseCase>();
        services.AddScoped<IAccAdminService, AccAdminService>();
        services.AddScoped<ForeignAccFetcher>();
        services.AddSingleton<NeighbourAdjacencyComputer>();   // puro, senza stato
        services.AddScoped<INeighbourImportService, NeighbourImportService>();
        services.AddScoped<INeighbourReader>(sp => sp.GetRequiredService<INeighbourImportService>());   // stessa istanza, porta di sola lettura (ISP)
        services.AddScoped<IAirportProfileService, AirportProfileService>();
        services.AddScoped<IAppDocumentService, AppDocumentService>();
        services.AddScoped<IAccProfileService, AccProfileService>();
        services.AddScoped<IVloaProfileService, VloaProfileService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEditorTaskService, EditorTaskService>();
        services.AddScoped<IDocumentAdminService, DocumentAdminService>();
        services.AddScoped<IAirportSectorImporter, AirportSectorImporter>();
        services.AddScoped<IAirportImportUseCase, AirportImportUseCase>();
        services.AddScoped<ISidImporter, SidImporter>();
        services.AddScoped<ITowerShapeFallbackService, TowerShapeFallbackService>();
        services.AddScoped<IAirportSectorService, AirportSectorService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChangesService, ChangesService>();
        services.AddScoped<IImportPolicyService, ImportPolicyService>();
        return services;
    }
}
