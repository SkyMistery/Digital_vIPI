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
        services.AddScoped<IResourceLockService, ResourceLockService>();
        services.AddScoped<IStructureEditingService, StructureEditingService>();
        services.AddScoped<IAccImportUseCase, AccImportUseCase>();
        services.AddScoped<ISpecialAreaImportUseCase, SpecialAreaImportUseCase>();
        services.AddScoped<IAccAdminService, AccAdminService>();
        services.AddScoped<ForeignAccFetcher>();
        services.AddSingleton<NeighbourAdjacencyComputer>();   // puro, senza stato
        services.AddScoped<INeighbourImportService, NeighbourImportService>();
        services.AddScoped<INeighbourReader>(sp => sp.GetRequiredService<INeighbourImportService>());   // stessa istanza, porta di sola lettura (ISP)
        services.AddScoped<IAirportEditingService, AirportEditingService>();
        services.AddScoped<IAppDocumentService, AppDocumentService>();
        services.AddScoped<IAccDerivationService, AccDerivationService>();
        services.AddScoped<IAccDocumentService, AccDocumentService>();
        services.AddScoped<IVloaDerivationService, VloaDerivationService>();
        // doc 10 §3b: cattura Frozen delle sezioni derivate. Un provider per famiglia; il registry li risolve per tipo.
        services.AddScoped<IFrozenSectionProvider, VloaFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionProvider, AppFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionProvider, AccFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionRegistry, FrozenSectionRegistry>();
        services.AddScoped<IFrozenSectionReader, FrozenSectionReader>();   // doc 10 §3d: lettura frozen al view
        services.AddScoped<IAccViewDerivationService, AccViewDerivationService>();
        services.AddScoped<IAppViewDerivationService, AppViewDerivationService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEditorTaskService, EditorTaskService>();
        services.AddScoped<IDocumentReviewService, DocumentReviewService>();
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
