using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vipi.Application.Abstractions;
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
        // Edizione militare (carta vSOP militari §4): rotte proprie, non la stessa pagina con un
        // parametro -- le due edizioni hanno release e contenuti indipendenti, e un collegamento salvato
        // deve portare sempre allo stesso documento.
        services.AddSingleton<Routing.IDocKindRoutes, Routing.AirportMilDocRoutes>();
        services.AddSingleton<Routing.IDocKindRoutes, Routing.AppMilDocRoutes>();
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
        // Da posizione staff IVAO a livello: funzione PURA, e singleton perché i pattern si compilano una
        // volta sola (otto regex per l'admin, due per prefisso ICAO) e la domanda arriva a ogni richiesta.
        services.AddSingleton<Auth.RoleResolver>();
        // Le promozioni a mano, tenute INTERE in memoria. ⚠️ Singleton non è una comodità: leggerle con una
        // SELECT per richiesta rimetterebbe nel layout la query che questa funzione toglie, cioè la causa
        // prima delle corse sul DbContext di circuito. Lo scope per il database se lo apre la ricarica.
        services.AddSingleton<Auth.IRoleOverrides, Auth.RoleOverrideCache>();
        // Confronta i pattern admin coi codici staff realmente osservati dai login: se non combaciano,
        // in produzione nessuno può editare e non lo si scopre in altro modo.
        services.AddScoped<Auth.IAdminCoverageService, Auth.AdminCoverageService>();
        services.AddScoped<Auth.IStaffRosterService, Auth.StaffRosterService>();
        // La gestione dei livelli: promuove, declassa, e RICARICA la cache — senza l'ultima cosa una
        // promozione non farebbe effetto fino al riavvio.
        services.AddScoped<Auth.IRoleAdminService, Auth.RoleAdminService>();
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
        // ⚠️ Lo STESSO provider una seconda volta, per l'edizione MILITARE dello scalo (carta vSOP militari
        // §2): stesso motore di proiezione, cattura separata. Senza questa riga `FrozenSectionRegistry`
        // non trova un provider per `AirportMil` e torna `Empty` IN SILENZIO — pubblicare un vSOP militare
        // non congelerebbe niente, e le sue tabelle derivate resterebbero appese alla release CIVILE.
        services.AddScoped<IFrozenSectionProvider>(sp => new AirportFrozenSectionProvider(
            sp.GetRequiredService<IAirportProfileReader>(),
            sp.GetRequiredService<IAirportSectorService>(),
            sp.GetRequiredService<IAirportSidDerivationService>(),
            Vipi.Domain.ReleaseTargetType.AirportMil,
            // ⚠️ Solo qui: «Radioassistenze» è una sezione del profilo MILITARE, e il civile non ce l'ha.
            sp.GetRequiredService<Abstractions.INavaidCatalog>(),
            sp.GetRequiredService<Abstractions.IAirportNameLookup>()));
        services.AddScoped<IFrozenSectionRegistry, FrozenSectionRegistry>();
        services.AddScoped<IFrozenSectionReader, FrozenSectionReader>();   // doc 10 §3d: lettura frozen al view
        services.AddScoped<IAccViewDerivationService, AccViewDerivationService>();
        services.AddScoped<IAppViewDerivationService, AppViewDerivationService>();
        services.AddScoped<IVloaViewDerivationService, VloaViewDerivationService>();
        services.AddScoped<IAirportSidDerivationService, AirportSidDerivationService>();
        services.AddScoped<IAirportViewDerivationService, AirportViewDerivationService>();
        // Il timbro di «Validità e revisione»: ciclo, data e chi ha pubblicato. Vale per tutte e quattro le
        // famiglie, quindi sta con i servizi documentali e non dentro una di loro.
        services.AddScoped<IDocumentValidityService, DocumentValidityService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEditorTaskService, EditorTaskService>();
        services.AddScoped<IEditorTaskLinksService, EditorTaskLinksService>();
        services.AddScoped<IDocumentImpactService, DocumentImpactService>();
        services.AddScoped<IOrphanSectorService, OrphanSectorService>();
        services.AddScoped<IDeletionService, DeletionService>();

        // La porta verso «chiedi alla sorgente se c'è ancora» ha qui il suo null-object: l'adapter vero lo
        // registra AddVipiIvao. TryAdd e non Add perché l'ordine fra i due blocchi non deve contare — se
        // l'Host registra prima IVAO questa non fa niente, se lo registra dopo la sua vince essendo l'ultima.
        // Senza questa riga, un Host senza sorgente esterna non riuscirebbe a costruire DeletionService.
        services.TryAddScoped<ISourcePresenceProbe, SorgenteNonInterrogabile>();
        services.AddScoped<IPendingOverviewService, PendingOverviewService>();

        // «Da fare»: il read-model che legge le segnalazioni del sistema e gli incarichi delle persone e ne
        // fa una lista sola. Non è un terzo meccanismo — non salva niente.
        services.AddScoped<IWorkListService, WorkListService>();
        services.AddScoped<IImpactDriftUseCase, ImpactDriftUseCase>();
        services.AddScoped<IDocumentAdminService, DocumentAdminService>();
        services.AddScoped<IAirportSectorImporter, AirportSectorImporter>();
        services.AddScoped<IAirportImportUseCase, AirportImportUseCase>();
        services.AddScoped<IAirportDataImportUseCase, AirportDataImportUseCase>();
        services.AddScoped<ISidImporter, SidImporter>();
        services.AddScoped<INavaidImporter, NavaidImporter>();
        // Il perimetro dei ripieghi shape (solo enti della divisione): uno solo, condiviso dai tre.
        services.AddSingleton<ShapeFallbackScope>();
        services.AddScoped<IGithubTowerShapeService, GithubTowerShapeService>();
        services.AddScoped<ITowerShapeFallbackService, TowerShapeFallbackService>();
        // Il gemello per CTR/APP/MIL/FSS: le TWR hanno GitHub + cerchio, gli altri enti il sectorfile.
        services.AddScoped<ISectorShapeFallbackService, SectorShapeFallbackService>();
        // L'avviso a chi pubblica un'area non ancora in vigore, e l'interruttore che la forza.
        services.AddScoped<IShapeGateNoticeService, ShapeGateNoticeService>();
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
