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

        // Annota chi c'era già quando una seconda operazione trova il contesto occupato: è la metà della
        // storia che lo stack dell'eccezione non contiene (docs/lavori-aperti.md §E9). Senza stato proprio,
        // quindi una sola istanza per tutti e tre i provider.
        var Tracciante = new Persistence.TracciaCollisioniInterceptor();

        switch (provider)
        {
            case Persistence.PersistenceProvider.Sqlite:
                // Tampone concorrenza SQLite (A1): WAL + busy_timeout a ogni apertura connessione. Vedi SqliteTuningInterceptor.
                services.AddDbContext<VipiDbContext>(o => o
                    // Query con >1 Include di collection: split in più SELECT (default consigliato MS) invece del
                    // JOIN cartesiano di SingleQuery. Toglie il warning EF 20504 e migliora la perf su tali query.
                    .UseSqlite(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .AddInterceptors(new Persistence.SqliteTuningInterceptor(), Tracciante));
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
                        .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null))
                    .AddInterceptors(Tracciante));
                break;

            case Persistence.PersistenceProvider.MySql:
#if NET8_0
                // Produzione su atc.it.ivao.aero, che è MariaDB 11.4 (ADR-0007 §D4-ter). Provider Pomelo,
                // l'unico che supporta MariaDB davvero — e che esiste solo per EF Core 8, da cui questo #if
                // e il fatto che Vipi.Host sia net8.
                //
                // La versione del server è FISSATA, non auto-rilevata: ServerVersion.AutoDetect apre una
                // connessione mentre si costruiscono le opzioni, quindi con il database ancora giù l'app non
                // parte per un motivo che non somiglia a quello vero. Fissandola, l'avvio è deterministico e
                // il guasto arriva alla prima query, dove si legge.
                var versioneServer = Persistence.MySqlSchema.ResolveServerVersion(
                    configuration?[Persistence.MySqlSchema.ServerVersionConfigKey]);

                services.AddDbContext<VipiDbContext>(o => o
                    .UseMySql(connectionString, versioneServer, my => my
                        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                        // Senza questa riga EF cercherebbe le migrazioni in Vipi.Infrastructure e ci
                        // troverebbe le 68 SQLite-flavored, applicandole a MariaDB. È il modo silenzioso in
                        // cui questa configurazione può sbagliare: non manca niente, c'è la cosa sbagliata.
                        .MigrationsAssembly(Persistence.MySqlSchema.MigrationsAssemblyName)
                        // Ritenta i guasti transitori. La ragione NON è quella di Neon — lì il compute si
                        // sospende e la prima query dopo l'inattività fallisce — perché MariaDB su
                        // atc.it.ivao.aero è un server dedicato e sempre acceso. Sono gli altri modi in cui
                        // una connessione del pool muore senza che l'app abbia sbagliato niente: il riavvio
                        // di mariadb.service dopo un aggiornamento, il wait_timeout del server che chiude le
                        // idle, un KILL da pannello di gestione. Senza retry, ognuno di questi diventa una
                        // pagina di errore per chi stava editando.
                        //
                        // Retry-safe come su Npgsql, e per lo stesso motivo: l'unico punto che apre
                        // transazioni esplicite è EfUnitOfWork, che le avvolge in CreateExecutionStrategy()
                        // e azzera il change-tracker a ogni tentativo. Prima di aprire una transazione
                        // altrove, rileggere quel file.
                        .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null))
                    .AddInterceptors(Tracciante));
                break;
#else
                // Su net10 il provider non esiste: Pomelo non ha una build per EF Core 10 e non l'avrà a
                // breve (quattro tentativi di porting, nessuno approdato). Meglio un errore che lo dice che
                // un `default:` generico, che manderebbe a cercare un errore di battitura nella config.
                throw new InvalidOperationException(
                    "Persistence:Provider=MySql è supportato solo sul target net8.0, perché il provider " +
                    "Pomelo — l'unico che regge MariaDB — non ha una build per EF Core 10. L'host di " +
                    "produzione (Vipi.Host) è net8 apposta. Vedi ADR-0007 §D4-ter e " +
                    "docs/design/piano-supporto-mysql.md.");
#endif

            default:
                throw new InvalidOperationException($"Provider di persistenza non gestito: {provider}.");
        }
        services.AddScoped<Vipi.Application.Abstractions.IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.ITopologyProvider, TopologyBuilder>();
        services.AddScoped<Vipi.Application.Abstractions.IContentRepository, EfContentRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditingRepository, EfEditingRepository>();
        services.AddScoped<Vipi.Application.Content.IDocumentMaintenance, EfDocumentMaintenance>();
        services.AddScoped<Vipi.Application.Content.ISpecialAreaMaintenance, EfSpecialAreaMaintenance>();
        services.AddScoped<Vipi.Application.Abstractions.IResourceLockRepository, EfResourceLockRepository>();
        // Immagini dei blocchi: i byte stanno nel DB. Spostarli altrove (object storage) = cambiare questa riga.
        services.AddScoped<Vipi.Application.Abstractions.IMediaStore, EfMediaStore>();
        services.AddScoped<Vipi.Application.Media.IMediaMaintenance, EfMediaMaintenance>();
        services.AddScoped<Vipi.Application.Abstractions.IStructureEditingRepository, EfStructureEditingRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportRepository, EfAirportRepository>();
        // La porta di sola lettura del profilo aeroporto è LO STESSO oggetto: chi deriva una vista non deve poter
        // scrivere, ma non esiste una seconda implementazione da tenere allineata.
        services.AddScoped<Vipi.Application.Abstractions.IAirportProfileReader>(
            sp => sp.GetRequiredService<Vipi.Application.Abstractions.IAirportRepository>());
        services.AddScoped<Vipi.Application.Abstractions.IAppDerivationRepository, EfAppDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAccDerivationRepository, EfAccDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.ISpecialAreaRepository, EfSpecialAreaRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStationDirectory, EfStationDirectory>();
        // I flussi storici restano leggibili finche' il travaso non e' stato eseguito ovunque: la
        // migrazione che droppa le due tabelle arriva DOPO, in una release sua.
        services.AddScoped<Vipi.Application.Abstractions.IAgreementRepository, EfAgreementRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IStaffRosterRepository, EfStaffRosterRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAuditLogReader, EfAuditLogReader>();
        services.AddScoped<Vipi.Application.Abstractions.ISearchRepository, EfSearchRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IChangesRepository, EfChangesRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IImportPolicyStore, EfImportPolicyStore>();
        services.AddScoped<Vipi.Application.Abstractions.INavaidCatalog, EfNavaidCatalog>();
        services.AddScoped<Vipi.Application.Abstractions.IAttachmentLibrary, EfAttachmentLibrary>();
        services.AddScoped<Vipi.Application.Abstractions.IAttachmentTextSource, EfAttachmentTextSource>();
        services.AddScoped<Vipi.Application.Airspace.IAirspaceCatalog, EfAirspaceCatalog>();
        services.AddScoped<Vipi.Application.Airspace.ISectorAirspaceBindings, EfSectorAirspaceBindings>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportNameLookup, EfAirportNameLookup>();
        services.AddScoped<Vipi.Application.Abstractions.IImportStateStore, EfImportStateStore>();
        // Cadenza dei giri automatici, dalle opzioni della sorgente: la pagina admin la legge da qui
        // perche' Vipi.Ui non vede IvaoOptions (ne' deve: la sorgente e' sostituibile).
        services.AddSingleton<Vipi.Application.Abstractions.IImportSchedule, ImportSchedule>();
        services.AddScoped<Vipi.Application.Abstractions.IConsistencyReportRepository, EfConsistencyReportRepository>();
        // Drift di schema: registrato sempre, si disattiva da sé fuori da Npgsql (dove le migrazioni EF girano
        // davvero e il drift non si accumula). Confluisce nel report di consistenza. Vedi ADR-0007.
        services.AddScoped<Vipi.Application.Diagnostics.ISchemaDriftProbe, Persistence.PostgresSchemaDriftProbe>();
        // Impostazioni del server che l'app assume e non può imporre (sql_mode, max_allowed_packet).
        // Registrata sempre e no-op fuori da MySQL, come la sonda di drift qui sopra.
        services.AddScoped<Vipi.Application.Diagnostics.IServerSettingsProbe, Persistence.MySqlServerSettingsProbe>();
        services.AddScoped<Vipi.Application.Abstractions.IAccAdminRepository, EfAccAdminRepository>();
        services.AddScoped<Vipi.Application.Abstractions.INeighbourRepository, EfNeighbourRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IVloaDerivationRepository, EfVloaDerivationRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentProfileRepository, EfDocumentProfileRepository>();
        // Descrittori per-tipo del flusso di pubblicazione (doc 09 §3a): i motori generici consultano il registry.
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.VloaReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AppReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AccVipiReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AirportReleaseTarget>();
        // Edizione militare (carta vSOP militari §4). L'ordine di REGISTRAZIONE non conta: a decidere chi
        // viene interrogato prima e' DescribeOrder, e questi due l'hanno a zero -- vedi l'avviso sulla
        // classe per il perche' servano DUE difese e non una.
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AirportMilReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTarget, Persistence.ReleaseTargets.AppMilReleaseTarget>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseTargetRegistry, Vipi.Application.Content.ReleaseTargetRegistry>();
        services.AddScoped<Vipi.Application.Abstractions.IReleaseRepository, EfReleaseRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IEditorTaskRepository, EfEditorTaskRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentImpactRepository, EfDocumentImpactRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IOrphanSectorRepository, EfOrphanSectorRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IDeletionRepository, EfDeletionRepository>();
        services.AddScoped<Vipi.Application.Content.ISectorCatalogMaintenance, EfSectorCatalogMaintenance>();
        services.AddScoped<Vipi.Application.Abstractions.IDocumentAdminRepository, EfDocumentAdminRepository>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportSectorRepository, EfAirportSectorRepository>();
        // La rinomina: un motore solo, come l'eliminazione. Lo chiamano i due upsert di catalogo, in cima.
        services.AddScoped<Vipi.Application.Content.ICallsignRenameService, EfCallsignRenameService>();
        services.AddScoped<Vipi.Application.Content.ISectorShapeRepository, EfSectorShapeRepository>();
        services.AddScoped<Vipi.Application.Content.IShapeGateRepository, EfShapeGateRepository>();
        // Il contesto del congelamento: SCOPED come il DbContext, quindi vale per una richiesta sola.
        services.AddScoped<Vipi.Application.Content.ShapeReleaseContext>();
        // In che lingua comporre la prosa GENERATA (frasi di coordinamento). Scoped come il contesto
        // sopra e per la stessa ragione: vale per una richiesta sola, non e' uno stato globale.
        services.AddScoped<Vipi.Application.Content.ReadingLanguageContext>();
        // Statistiche ATC: archivio delle sessioni e delle tratte scritte dal poller, più la mappa dei
        // settori (albero proiettato + volumi dai cataloghi) su cui si attribuisce il traffico.
        services.AddScoped<Vipi.Application.Abstractions.IAtcSessionStore, EfAtcSessionStore>();
        services.AddScoped<Vipi.Application.Abstractions.IAtcTrafficStore, EfAtcTrafficStore>();
        services.AddScoped<Vipi.Application.Abstractions.ISectorVolumeCatalog, EfSectorVolumeCatalog>();
        services.AddScoped<Vipi.Application.Abstractions.IAtcStatsQueries, EfAtcStatsQueries>();
        // Lettura grezza dell'archivio (divisione + resto del mondo): la usano la pagina staff e l'endpoint
        // macchina. Porta separata da quella delle statistiche apposta — quella conta, questa mostra.
        services.AddScoped<Vipi.Application.Abstractions.IAtcArchiveQueries, EfAtcArchiveQueries>();
        services.AddScoped<Vipi.Application.Abstractions.IStatsSettingsStore, EfStatsSettingsStore>();
        services.AddScoped<Vipi.Application.Abstractions.IStatsAccessLog, EfStatsAccessLog>();
        // Traffico d'aeroporto consolidato: quanto ce n'era e quanto ha trovato un controllore acceso.
        services.AddScoped<Vipi.Application.Abstractions.IAirportTrafficRollupStore, EfAirportTrafficRollupStore>();
        services.AddScoped<Vipi.Application.Abstractions.IAirportCoverageQueries, EfAirportCoverageQueries>();
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

        // Documenti bilingue (carta 2026-08-27): il motore di traduzione automatica.
        // ⚠️ Si registra SEMPRE, anche senza chiave: `IsConfigured` è falso e ogni chiamata risponde
        // `NotConfigured`. È voluto — un sito senza chiave non è rotto, semplicemente non traduce, e chi
        // dipende dalla porta non deve avere due percorsi di codice a seconda della configurazione.
        // ⚠️ La chiave NON sta in un appsettings versionato: user-secrets in sviluppo, variabile d'ambiente
        // o cartella dei segreti in produzione, come le credenziali IVAO.
        if (configuration is not null)
            services.Configure<Vipi.Application.Translation.TranslationOptions>(
                configuration.GetSection(Vipi.Application.Translation.TranslationOptions.SectionName));
        services.AddHttpClient(Translation.DeepLTranslationEngine.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);   // un lotto di 50 testi non torna in dieci secondi
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddHttpClient(Translation.AzureTranslationEngine.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        // ⚠️ Si registrano ENTRAMBI, e l'ordine di preferenza lo decide `Translation:Order`, non l'ordine di
        // queste righe: un motore aggiunto in fondo al file non deve diventare il primario per sbaglio.
        // Azure e' il primario dal 27 agosto 2026; DeepL resta pronto e subentra da solo quando Azure non
        // risponde o esaurisce la franchigia.
        services.AddSingleton<Vipi.Application.Abstractions.ITranslationEngine, Translation.AzureTranslationEngine>();
        services.AddSingleton<Vipi.Application.Abstractions.ITranslationEngine, Translation.DeepLTranslationEngine>();
        services.AddScoped<Vipi.Application.Abstractions.ITranslationMemory, EfTranslationMemory>();
        services.AddScoped<Vipi.Application.Abstractions.IGlossaryStore, EfGlossaryStore>();
        services.AddScoped<Vipi.Application.Abstractions.IRoleOverrideStore, EfRoleOverrideStore>();
        services.AddScoped<Vipi.Application.Abstractions.ITranslatableCorpus, EfTranslatableCorpus>();
        services.AddScoped<Vipi.Application.Translation.DocumentTranslator>();
        // La vIPI ACC non arriva alla pagina come DocumentView (vive a blocchi): stessa memoria, stessa
        // copertura, solo un'altra passeggiata sull'albero.
        services.AddScoped<Vipi.Application.Translation.AccVipiTranslator>();
        // Traduttore dei testi dell'anagrafica dentro le sezioni derivate. Scoped: carica la coppia di
        // lingue una volta per richiesta, perche' chi proietta scopre i testi che gli servono strada facendo.
        services.AddScoped<Vipi.Application.Translation.TranslationLookup>();
        // Le frasi di UN documento con la loro resa: il correttore dentro l'editor. Il Registro admin
        // resta dov'e' — quello elenca tutta la divisione, questo il documento che si sta scrivendo.
        // La faccia stretta dell'editing: il correttore legge il documento e basta.
        services.AddScoped<Vipi.Application.Content.IDocumentForReview>(sp =>
            sp.GetRequiredService<Vipi.Application.Content.IEditingService>());
        services.AddScoped<Vipi.Application.Translation.IDocumentTranslationReview,
                           Vipi.Application.Translation.DocumentTranslationReview>();
        services.AddScoped<Vipi.Application.Content.IMilitaryDocumentService, EfMilitaryDocumentService>();



        // Import SID dal sectorfile Aurora su GitHub (repo pubblico raw, no auth). Ortogonale a DataSource:Provider.
        services.AddScoped<Vipi.Application.Abstractions.ISidFixAliasRepository, EfSidFixAliasRepository>();
        if (configuration is not null)
            services.Configure<Sectorfile.SectorfileOptions>(configuration.GetSection("Sectorfile"));
        // Cache dei file di sectorfile (navaid, poligoni TWR, carte MRVA). DEVE essere singleton:
        // gli adapter sotto sono transient (AddHttpClient<,>), quindi una cache in campo d'istanza sarebbe
        // per-risoluzione e il suo lock non sincronizzerebbe nulla. Vedi SectorfileCache.
        services.AddSingleton<Sectorfile.SectorfileCache>();
        // Catalogo dei punti (itvor/itndb/itfix): unico posto che scarica i navaid. Lo usano l'import SID per
        // completare il fix troncato E gli editor per suggerire/validare i punti scritti a mano.
        services.AddHttpClient<Vipi.Application.Abstractions.INavaidSource, Sectorfile.AuroraNavaidSource>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddHttpClient<Vipi.Application.Abstractions.ISidProvider, Sectorfile.AuroraSidProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        services.AddHostedService<Sectorfile.SidImportHostedService>();
        // Le radioassistenze escono dagli STESSI file delle SID (§12b): stessa cadenza, chiave di stato sua.
        services.AddHostedService<Sectorfile.NavaidImportHostedService>();

        // Shape TWR reali dal file poligoni Aurora (twrs.tfl) su GitHub: stesso repo raw pubblico dell'import SID.
        services.AddHttpClient<Vipi.Application.Abstractions.ITowerShapeSource, Sectorfile.AuroraTowerShapeProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });

        // Shape di SETTORE (CTR/APP/MIL/FSS) dai file DYNAMIC_SEC/*.tfl: stesso repo, e quali file leggere
        // lo dice ITALY.isc, l'indice che carica Aurora stessa.
        services.AddHttpClient<Vipi.Application.Abstractions.ISectorShapeSource, Sectorfile.AuroraSectorShapeProvider>(c =>
        {
            // Piu' lungo degli altri: l'indice piu' una ventina di file, in sequenza.
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });

        // Carte MRVA (ENRMVA/{acc}.mva, {icao}.mva): stesso repo raw. Nessun hosted service — la sezione «minime»
        // è derivata a view-time e congelata alla release, quindi non c'è niente da importare in tabella.
        services.AddHttpClient<Vipi.Application.Abstractions.IVectoringMinimaSource, Sectorfile.AuroraMvaProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("vIPI-IVAO-Italy/1.0");
        });
        return services;
    }
}
