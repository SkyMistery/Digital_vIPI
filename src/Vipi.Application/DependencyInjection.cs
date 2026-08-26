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
        // Rotte pubbliche/editor per tipo di documento (doc 09 §3b). Stanno in Application dal doc 13 §3e: le
        // consultano anche ricerca e «Cosa è cambiato», che vivono in Infrastructure e non possono vedere la UI.
        services.AddSingleton<Routing.IDocKindRoutes, Routing.VloaDocRoutes>();
        services.AddSingleton<Routing.IDocKindRoutes, Routing.AppDocRoutes>();
        services.AddSingleton<Routing.IDocKindRoutes, Routing.AccVipiDocRoutes>();
        services.AddSingleton<Routing.IDocKindRoutes, Routing.AirportDocRoutes>();
        services.AddSingleton<Routing.IDocRoutesRegistry, Routing.DocRoutesRegistry>();

        services.AddSingleton<IAiracService, AiracService>();
        services.AddSingleton<IAorService, AorService>();
        services.AddSingleton<IContentService, ContentService>();
        // Singleton: dice a TUTTE le sessioni che il catalogo ACC e' cambiato. Vedi IStationCatalogVersion —
        // senza, la cache del resolver (scoped = per CIRCUITO in Blazor Server) invecchia per ore.
        services.AddSingleton<IStationCatalogVersion, StationCatalogVersion>();
        services.AddScoped<IStationResolver, StationResolver>();   // scoped: legge le ACC dal DB
        services.AddScoped<IVipiViewService, VipiViewService>();
        services.AddScoped<Auth.IEditAuthorizationService, Auth.EditAuthorizationService>();
        // Confronta i pattern admin coi codici staff realmente osservati dai login: se non combaciano,
        // in produzione nessuno può editare e non lo si scopre in altro modo.
        services.AddScoped<Auth.IAdminCoverageService, Auth.AdminCoverageService>();
        services.AddScoped<Auth.IStaffRosterService, Auth.StaffRosterService>();
        services.AddScoped<IEditingService, EditingService>();
        services.AddScoped<IResourceLockService, ResourceLockService>();
        services.AddScoped<IStructureEditingService, StructureEditingService>();
        services.AddScoped<IAccImportUseCase, AccImportUseCase>();
        services.AddScoped<ISpecialAreaImportUseCase, SpecialAreaImportUseCase>();
        services.AddScoped<IAccAdminService, AccAdminService>();
        services.AddScoped<ForeignAccFetcher>();
        services.AddScoped<ForeignSectorResolver>();
        services.AddSingleton<NeighbourAdjacencyComputer>();   // puro, senza stato
        services.AddScoped<INeighbourImportService, NeighbourImportService>();
        services.AddScoped<INeighbourReader>(sp => sp.GetRequiredService<INeighbourImportService>());   // stessa istanza, porta di sola lettura (ISP)
        services.AddScoped<IAirportEditingService, AirportEditingService>();
        services.AddScoped<IAppDocumentService, AppDocumentService>();
        services.AddScoped<IAccDerivationService, AccDerivationService>();
        services.AddScoped<IAccDocumentService, AccDocumentService>();
        services.AddScoped<IVloaDerivationService, VloaDerivationService>();
        // Vista live: un descrittore per tipo di ente, il registry li consulta in ordine di Priority
        // (FEATURE-PROCESS §2). Aggiungere un tipo = registrare qui, nessuno switch da toccare.
        services.AddScoped<Live.LiveStationParts>();
        services.AddScoped<Live.ILiveStationKind, Live.AreaLiveStation>();
        services.AddScoped<Live.ILiveStationKind, Live.ApproachLiveStation>();
        services.AddScoped<Live.ILiveStationKind, Live.AirportLiveStation>();
        services.AddScoped<Live.ILiveStationRegistry, Live.LiveStationRegistry>();
        services.AddScoped<Live.ILiveViewService, Live.LiveViewService>();
        // «Chi controlla l'aeroporto adesso» per le pagine fuori dalla vista live (vista rapida, viewer).
        services.AddScoped<Live.IAirportPresidencyService, Live.AirportPresidencyService>();
        // doc 10 §3b: cattura Frozen delle sezioni derivate. Un provider per famiglia; il registry li risolve per tipo.
        services.AddScoped<IFrozenSectionProvider, VloaFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionProvider, AppFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionProvider, AccFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionProvider, AirportFrozenSectionProvider>();
        services.AddScoped<IFrozenSectionRegistry, FrozenSectionRegistry>();
        services.AddScoped<IFrozenSectionReader, FrozenSectionReader>();   // doc 10 §3d: lettura frozen al view
        services.AddScoped<IAccViewDerivationService, AccViewDerivationService>();
        services.AddScoped<IAppViewDerivationService, AppViewDerivationService>();
        services.AddScoped<IVloaViewDerivationService, VloaViewDerivationService>();
        services.AddScoped<IAirportSidDerivationService, AirportSidDerivationService>();
        services.AddScoped<IAirportViewDerivationService, AirportViewDerivationService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEditorTaskService, EditorTaskService>();
        services.AddScoped<IEditorTaskLinksService, EditorTaskLinksService>();
        services.AddScoped<IDocumentImpactService, DocumentImpactService>();
        services.AddScoped<IOrphanSectorService, OrphanSectorService>();
        services.AddScoped<IDeletionService, DeletionService>();
        services.AddScoped<IPendingOverviewService, PendingOverviewService>();
        services.AddScoped<IImpactDriftUseCase, ImpactDriftUseCase>();
        services.AddScoped<IDocumentAdminService, DocumentAdminService>();
        services.AddScoped<IAirportSectorImporter, AirportSectorImporter>();
        services.AddScoped<IAirportImportUseCase, AirportImportUseCase>();
        services.AddScoped<IAirportDataImportUseCase, AirportDataImportUseCase>();
        services.AddScoped<ISidImporter, SidImporter>();
        services.AddScoped<IGithubTowerShapeService, GithubTowerShapeService>();
        services.AddScoped<ITowerShapeFallbackService, TowerShapeFallbackService>();
        services.AddScoped<IAirportSectorService, AirportSectorService>();
        services.AddScoped<IAgreementService, AgreementService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChangesService, ChangesService>();
        services.AddScoped<IImportPolicyService, ImportPolicyService>();
        services.AddScoped<IImportOverviewService, ImportOverviewService>();
        services.AddScoped<INewDocumentOptionsService, NewDocumentOptionsService>();
        services.AddScoped<Vipi.Application.Diagnostics.IConsistencyReportService, Vipi.Application.Diagnostics.ConsistencyReportService>();
        return services;
    }
}
