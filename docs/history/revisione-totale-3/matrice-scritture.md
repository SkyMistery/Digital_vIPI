# Matrice delle scritture — revisione 3, dimensione d01 (identità e accesso)

Repository `vipi-sito` @ `8247e1ea` (src = 1.46.5, `e24557e`). Sola lettura, nessuna build.

Metodo di lavoro: uno scanner (in questa cartella, `scan2.py`) ha elencato tutti i metodi pubblici `Task…` di
`Vipi.Application` e `Vipi.Infrastructure` con la **chiusura transitiva** dei cancelli dentro lo stesso file
(`EnsureAtLeast`, `EnsureAdmin`, `DocumentLockGuard/AirportLockGuard.Ensure*`, `ResourceLockService.EnsureHeldAsync`,
`EnsureLockAsync`…). `callers.py` ha trovato i punti d'ingresso nella UI (`callers.txt`). Ogni riga senza cancello
nel servizio è stata **riletta a mano**. I numeri di riga dello scanner possono essere spostati di una riga; quelli
citati nei finding sono stati riletti.

Legenda
- **Livello** = cancello nel SERVIZIO (U = nessuno, Ed = `EnsureAtLeast(Editor)`, Ad = `EnsureAdmin`, DS = DivisionStaff,
  Emit = `IEmittentiChiaviApi.EnsurePuoEmettere`, «dinamico» = dipende dal bersaglio).
- **Lock** = lock verificato nel SERVIZIO: `Doc-mio` (`IsLockHeldBy`/`DocumentLockGuard.EnsureMineAsync`/`EditingService.EnsureLockAsync`),
  `Doc-non-altrui` (`EnsureNotLockedByOther*`), `Scalo-mio`/`Scalo-non-altrui` (`AirportLockGuard`), `Struttura`
  (`EnsureHeldAsync(admin:structure)`), `—` = nessuno.
- **Esito**: ✅ cancello nel servizio adeguato · ⚠️ candidato (vedi finding) · ℹ️ scelta dichiarata / non raggiungibile dalla UI.

## 1. Identità: le risposte alle domande del tema

| Domanda | Risposta | Dove |
|---|---|---|
| Il cookie è riconvalidato (T-002 regge)? | **Sì**: `OnValidatePrincipal` rilegge `/v2/users/{vid}` ogni 4 h, 15 min di ritento su guasto, «isStaff senza posizioni» ignorato. Nessuna regressione. Restano: (a) un circuito già aperto tiene la metà-claim del livello per tutta la sua vita (dichiarato nel commento, T-018 copre solo la metà-promozione); (b) un 404 di IVAO (account cancellato) è trattato come guasto → il cookie tiene le posizioni vecchie per sempre; (c) durante un guasto IVAO la richiesta che scatta la riconvalida aspetta il timeout HTTP (15 s) ogni 15 min per utente (finding d01-10). | `src/Vipi.Host/Auth/RiconvalidaPosizioniStaff.cs:47-81`, `VipiStandaloneAuthExtensions.cs:87` |
| SafeReturn/redirect aperti? | **No**: `/`+secondo carattere diverso da `/` e `\`, niente controlli; usato al login, alla pagina di guasto e in `OnRemoteFailure`. Logout con destinazione fissa. | `VipiStandaloneAuthExtensions.cs:470-484`, `:219-242`, `:262` |
| Identità di sviluppo spenta in produzione? | **Sì, ma per l'espressione di `VipiStartup.cs:274`, non per la guardia**: `ProductionIdentityGuard.EnsureSafe(IsDevelopment(), IsDevelopment() && !authEnabled)` non può mai lanciare (finding d01-07). Il rischio reale — un host pubblico avviato in `Development` con `VipiAuth:Enabled=false` — non è coperto: ogni visitatore sarebbe VID 704798 = fondatore = Admin. | `VipiStartup.cs:274-277`, `ProductionIdentityGuard.cs:16-24`, `DevCurrentUserProvider.cs:23` |
| ProfileSwapper spento in produzione? | **Non è uno strumento d'identità**: è lo scambio di sezioni dei profili Aurora `.cpr`, `@page "/services/profile-swapper"`, **anonimo e acceso**. Nessun impatto sull'identità, ma è una porta anonima di esaurimento memoria/CPU dell'unico processo (finding d01-01). | `src/Vipi.Ui/Pages/ProfileSwapperPage.razor:1-3, 371-399, 203`; `src/Vipi.Ui/LineDiff.cs:26` |
| Fondatori, override, roster: ci si alza il livello? | **No** dalla UI né dal servizio: `RoleAdminService` fa `EnsureAdmin`, rifiuta sé stessi, i fondatori, VID ≤ 0 e i livelli sotto il pavimento; il pavimento viene dal roster, che si scrive solo dai claim del login/verifica. Un Admin può promuovere altri ad Admin (voluto). Un `VipiRole` fuori enum (es. 99) passato da un messaggio SignalR fabbricato da un Admin darebbe «Admin», non di più (le chiavi API chiedono anche la posizione). `For(0)` dell'anonimo non trova righe perché `SetAsync` rifiuta VID ≤ 0 ed è l'unico scrittore di `RoleOverrides`. | `Application/Auth/RoleAdminService.cs:105-143`, `EditAuthorizationService.cs:117-124`, `Infrastructure/Persistence/EfRoleOverrideStore.cs` |
| Chiavi API: chi emette, chi revoca, come sono conservate? | Emissione/elenco/revoca: `EnsurePuoEmettere` in ogni metodo (fondatori, oppure Admin **con** posizione IT-DIR/ADIR/WM/AWM); chiave `vipi_`+32 byte casuali, in tabella solo SHA-256 + prefisso 12 caratteri; revoca = `RevocataUtc`, effetto immediato (lookup a ogni chiamata, niente cache); audit su creazione e revoca. Mostrata una volta sola nel circuito. Punti deboli minori: doppio clic su «Crea» (finding d01-09). | `Application/Auth/ChiaviApi.cs:25-37, 93-100, 128-171, 206-223`, `Infrastructure/Persistence/EfApiClientStore.cs`, `Hosting/PortaDelleApi.cs:59-93` |
| Copia del DB e key-ring | La copia esclude `DataProtectionKeys` (chi la scarica non può fabbricare cookie). Solo Admin, 404 agli altri, rifiuto cross-site via `Sec-Fetch-Site`. | `Infrastructure/DatabaseCopy/MySqlDumpSource.cs:22-26`, `Hosting/VipiModuleExtensions.cs:619-696` |
| Singleton che catturano l'identità? | Nessuno: controllati tutti i `AddSingleton` di Application/Infrastructure/Hosting; nessuno riceve `IEditAuthorizationService`/`ICurrentUserProvider`. | DI in `Application/DependencyInjection.cs:43-66` |

## 2. Matrice — servizi di `Vipi.Application`

### 2.1 Documenti (motore)

| Servizio · metodo | Livello | Lock | Ingresso UI | Esito |
|---|---|---|---|---|
| `EditingService.CreateDocumentAsync` | Ed | — (crea) | `NewDocumentPage.razor:341` (+ `EditLockBar` `editor:newdoc` solo scheda vLOA) | ✅ (le ValidationException sulla vLOA duplicata arrivano prima del cancello: fuga d'esistenza trascurabile) |
| `EditingService.CreateDraftAsync` / `AcquireLockAsync` | Ed | acquisisce Doc | `DocumentEditorShell.cs:341-342`, `LockDellaLista.cs:21` | ✅ |
| `EditingService.UpdateBlock/AddBlock/DeleteBlock/RenameSection/SetSection*/SetBodyOrder/AddSection/DeleteSection/MoveSection*/MoveBlock` (15) | Ed (`Authorize*Async`) | Doc-mio + rinnovo (`EnsureLockAsync:413`) + bozza (`RequireDraftAsync` nel repo) | `DocumentSectionsEditor.razor:432-622`, `AccEditorPage.razor:332,481` | ✅ |
| `EditingService.ApplicaSezioniComuniAsync` | Ed su tutti i documenti | Doc-mio su tutti prima di scrivere (T-026) | `UnionPanel.razor:410` | ✅ |
| `EditingService.PublishAsync` / `DiscardDraftAsync` | Ed | Doc-mio + lock di tutti i membri dell'unione | `VersioniPage.razor:1118,1157` | ✅ |
| `EditingService.ReleaseLockAsync` | U | filtra `LockedByUserId == me` nel repo | `DocumentEditorShell.cs:374`, `LockDellaLista.cs:27` | ✅ (rilascia solo il proprio) |
| `EditingService.ForceUnlockAsync` | Ed | — (è lo sblocco) | `VersioniPage.razor:759` | ✅ scelta del 28-ago (Editor forza), con audit |
| `ReleaseService.PublishAsync` / `PublishNowAsync` | Ed per ogni membro | Doc-non-altrui per ogni membro | `ReleasePanel.razor:597,605`, `VersioniPage.razor:1167,1176` | ✅ |
| `ReleaseService.CancelReleaseAsync` | Ed per ogni sorella | — (storia, non bozza) | `ReleasePanel.razor:613`, `VersioniPage.razor:1188` | ✅ |
| `ReleaseService.BackfillMissingReleasesAsync`, `PruneAllAsync` | U | — | nessuno (solo avvio) | ℹ️ |
| `ProssimoAiracService.ProgrammaMancantiAsync` | U, ma ogni scrittura passa da `ReleaseService.PublishAsync` | idem | `VersioniPage.razor:583` | ✅ (un non-editor ottiene solo «saltati») |
| `DocumentAdminService.SetLanguageAsync` / `SetHiddenAsync` | Ed | Doc-non-altrui | `ReleasePanel.razor:486`, `VersioniPage.razor:1219` | ✅ |
| `DocumentAdminService.DeleteAsync` / `DeletionService.EliminaAsync` | Ad | ricalcolo del piano in transazione | `VersioniPage.razor:1239`, `DeleteDialog.razor:225`, `AeroportiPage.razor:664` | ✅ (T-012) |
| `DocumentUnionService.UniscoAsync/RimuoviMembroAsync/SciogliAsync/SpostaAsync` | Ed | — | `UnionPanel.razor:339-426` | ℹ️ niente lock: unire un documento che un collega sta editando cambia a lui la regola di pubblicazione. Non promosso (regola editoriale, nessuna perdita dimostrata) |
| `DocumentImpactService.ClearAsync` | Ed / Ad se ACC ignota | — | `DocReviewBar.razor:135`, `ReleasePanel.razor:565`, `PendingPage.razor:323,338`, `TasksPage.razor:414,431` | ✅ |
| `DocumentImpactService.Raise*/ClearBySource/PruneClearedBefore` | U | — | nessuno (chiamati da servizi/giri) | ℹ️ |
| `ShapeGateNoticeService.ForcePublishAsync` | Ed (se il perimetro si risolve) | — | `ReleasePanel.razor:583` | ✅ |
| `DocumentTranslationReview.CorreggiAsync` | Ed | — (memoria di traduzione, non il documento) | `TranslationReviewPanel.razor:505` | ✅ — ma la stessa scrittura ha una **seconda porta** senza cancello (§4) |
| `TraduciOraService.EseguiAsync` (Infrastructure) | Ed | — | `TranslationReviewPanel.razor:436` | ✅ |

### 2.2 Editor strutturati (APP, ACC, vSOP militare, vLOA, aeroporto)

| Servizio · metodo | Livello | Lock | Ingresso UI | Esito |
|---|---|---|---|---|
| `AppDocumentService.SaveAorCustomization/SaveSeparations/SaveRegulated/SaveConfigurations/SaveFrequencyOrder/SaveFrequencyLinks` | Ed | Doc-mio (`EnsureWritableAsync:161`) | `AppSectionsEditor.razor:511-598` | ✅ (T-004) |
| `AppDocumentService.EnsureAsync` | Ed (prima dell'uscita anticipata) | — (crea il documento) | `AppSectionsEditor.razor:445` | ✅ |
| `AccDocumentService.SaveBlockMeta/SaveConfigurations/SaveRegulated/SaveAorCustomization/SaveSeparations/AddGroup/RemoveGroup/MoveGroup` | Ed | Doc-mio + «la sezione è di questa ACC» (`DocumentoScrivibileAsync:101`) | `AccEditorPage.razor:455-616` | ✅ (T-004, T-063) |
| `AccDocumentService.EnsureAsync` | Ed solo se deve creare | — | `AccEditorPage.razor:383` | ✅ (sul già creato ritorna l'id senza scrivere) |
| `EfMilitaryDocumentService.CreaAsync` | Ed | — (crea) | `MilListPage.razor:185`, `NewDocumentPage.razor:429`, editor | ✅ |
| `EfMilitaryDocumentService.SaveNavaids/SaveDiversions/SaveFixedTable/SaveAreaActivity/SaveAreaNote/SaveRegulated` | Ed | Doc-mio (`ScrivibileAsync:54`) | `MilSectionsEditor.razor:1204-1649` | ✅ |
| **`VloaDerivationService.ToggleAorSectorAsync` / `ToggleFrequencyAsync` / `SaveFrequencyOrderAsync`** | Ed | **—** | `VloaEditor.razor:262, 268, 320` | ⚠️ **d01-02** (gemello di T-004 non coperto) |
| `AirportEditingService.SetMetarStation/SetTransitionAltitude/SaveTransitionLevels/SaveRunways/SaveRunwayRules/SaveLvp/SaveSids/UpdateImportedSid/SetImportedSidsHidden/SetImportedSidOverrides/SaveFrequencyLinks` | Ed | Scalo-mio (`EnsureLockMineAsync`) | `AirportSectionsEditor.razor:762-1137`, `MilSectionsEditor.razor:955-1043` | ✅ |
| `AirportEditingService.ReimportFromSourceAsync` | Ed | Scalo-non-altrui | editor + `AeroportiPage.razor:698` | ✅ (scelta dichiarata: comando in blocco) |
| `AirportEditingService.EnsureDocumentAsync` | Ed | — | `AirportSectionsEditor.razor:704` | ✅ |
| `AirportSectorService.ImportFromSource/ApplyGithubTwrShapes/SetHidden/SetLimits/SetPrimary/SetAccApp` | Ed | Scalo-mio | `AirportSectionsEditor.razor:847-977`, `MilSectionsEditor.razor:1067-1100` | ✅ |
| **`ProcedureImporter.ImportForCurrentUserAsync`** | Ed | **—** (né mio né non-altrui) | `AirportSectionsEditor.razor:851, 875`, `MilSectionsEditor.razor:1101, 1130` | ⚠️ **d01-06** |
| `ProcedureImporter.ImportAsync` | U | — | nessuno dalla UI (solo `ProcedureImportHostedService`) | ℹ️ (porta del giro, dichiarata; la UI usa l'altra) |

### 2.3 Struttura, anagrafiche, import

| Servizio · metodo | Livello | Lock | Ingresso UI | Esito |
|---|---|---|---|---|
| `StructureEditingService.CreateAcc/DeleteAcc/CreateAirport/DeleteAirport/MoveAirport/SetAirportHidden/SetAirportCategory/AutoAssignKnownAirports/GenerateAirportDocument/AddSector/DeleteSector/SetFeatured*/SetSectorFrequency` | Ed | Struttura | `AeroportiPage.razor:546-682`, `StrutturaPage.razor` | ✅ (T-025) |
| `AgreementService` (24 scritture: accordi, sezioni, clausole, varianti, ripristini) | Ed | Struttura | `AdminTrasferimentiPage.razor:935-3736` | ✅ |
| `AccAdminService.ImportFromSource/ImportSpecialAreas/SetSpecialAreasEnabled/SetHidden/SetSubcenterHidden/SetSubcenterLimits` | Ed | Struttura | `AccAdminPage.razor:581-692` | ✅ |
| `NeighbourImportService.ImportAndCompute/SetStatus/SetPolygon/AddManual/GenerateVloa/AddForeignSector` | Ed | Struttura | `ConfinantiAdminPage.razor:647-746` | ✅ |
| `NeighbourImportService.RecomputeFromArchiveAsync` | Ed | — (dichiarato: la chiama la pagina spazi aerei) | pagina spazi aerei | ℹ️ |
| `OrphanSectorService.ReattachAsync` | Ed / Ad se ACC ignota | Struttura | `StrutturaPage.razor:643` | ✅ |
| `EfHierarchyEditingService.SetParentAsync` | Ed | Struttura | `StrutturaPage.razor:691, 986` | ✅ |
| `EfSectorFallbackService.ReplaceAsync` | Ed | Struttura | `StrutturaPage.razor:1271` | ✅ |
| `ImportPolicyService.SaveAsync` | Ad | — | `SorgentiAdminPage.razor:366` | ✅ |
| `NavaidImporter.RunNowAsync` | Ed | — | `AdminNavaidsPage.razor:426` | ✅ (`RunAsync` = giro notturno, U, dichiarato in T-060) |
| `EfNavaidCatalog.Create/Delete/SetType/SetFrequency/SetChannel/SetCoordinates` | Ed | — | `AdminNavaidsPage.razor:459-554`, `MilSectionsEditor.razor:1198-1258` | ✅ (T-060) |
| `EfAirspaceCatalog.Save/SetCurrent/Delete`, `EfSectorAirspaceBindings.SetAsync` | Ed | — | `AdminAirspacePage.razor:502-639` | ✅ (T-060) |
| `EfSidFixAliasRepository.UpsertAsync` / `DeleteAsync` | Ed / Ad | — | `AirportSectionsEditor.razor:888`, `MilSectionsEditor.razor:1032`, `SorgentiAdminPage.razor:402` | ✅ (T-060) |
| `EfGlossaryStore.UpsertAsync` / `DeleteAsync` | Ed | — | `GlossarioPage.razor:1055, 1096, 1125` | ✅ (T-060; `SeminaVoceAsync` = seme, dichiarato) |
| `EfStatsSettingsStore.SaveAsync` | DS | — | `StatsDivisionPage.razor:500` | ✅ (T-060) |
| `EfPageIntroStore.SalvaAsync` | Ed | **—** (la pagina tiene `editor:page-intro:*`, il deposito non lo guarda) | `PageIntroZone.razor:282` | ⚠️ **d01-03** (gemello di T-025 su codice nuovo) |
| `AttachmentCurationService.Create/Replace/Delete` | Ed | — | `AdminAttachmentsPage.razor:545-603` | ✅ (R-023) |

### 2.4 Persone, incarichi, chiavi, lock di risorsa

| Servizio · metodo | Livello | Lock | Ingresso UI | Esito |
|---|---|---|---|---|
| `RoleAdminService.SetAsync` / `RemoveAsync` | Ad + non sé stessi + non fondatori + non sotto pavimento | — | `AdminRolesPage.razor:377, 392` | ✅ (doppio clic: d01-09) |
| `ApiClientService.CreaAsync` / `RevocaAsync` / `ListAsync` | Emit | — | `AdminApiKeysPage.razor:179, 194, 210` | ✅ (doppio clic: d01-09) |
| **`EditorTaskService.CreateAsync`** | **nessuno per l'incarico «libero» a sé stessi** (Ed solo se legato a documento; Ad per assegnare ad altri) | — | `TasksPage.razor:271` (qualunque utente loggato), `AdminTasksPage.razor:492` | ⚠️ **d01-05** |
| `EditorTaskService.UpdateStatusAsync` / `DeleteAsync` | Ad, oppure assegnatario / creatore | — | `TasksPage.razor:285, 294, 413, 445`, `DocReviewBar.razor:134, 154`, `AdminTasksPage.razor:506, 531` | ✅ |
| `EditorTaskService.AssignAsync`, `WorkListService.PrendiInCaricoAsync` | Ad | — | `AdminTasksPage.razor:518`, `TasksPage.razor:465` | ✅ |
| `ResourceLockService.AcquireAsync` | Ed per `admin:structure`; **qualunque loggato** per `editor:newdoc` e `editor:page-intro:*` | — | `EditLockBar.razor:108` | ℹ️ raggiungibile solo dove la barra è resa (Editor); commenti falsi → d01-08 |
| `ResourceLockService.HeartbeatAsync` / `ReleaseAsync` | U | filtra `LockedByUserId == me` | `EditLockBar.razor:120, 183` | ✅ |
| `ResourceLockService.ForceUnlockAsync` | Ed | — | `EditLockBar.razor:127` | ✅ (scelta 28-ago; commenti dicono «admin» → d01-08) |
| `StaffRosterService.RecordLoginAsync` | — (middleware, dai claim) | — | `StaffLoginTrackingMiddleware` | ✅ |
| `StaffRosterService.VerifyAllAsync` | — | — | solo giro (`StaffRosterVerificationService`) | ✅ |

## 3. Endpoint HTTP che scrivono

| Endpoint | Chi | Cancello | Esito |
|---|---|---|---|
| `GET /services/vsop/auth/login`, `/logout`, `/auth/accesso-non-riuscito` | chiunque | `SafeReturn`; logout a destinazione fissa | ✅ |
| `PUT /api/rfo/events/{id}/state` | chiave RFO da configurazione | confronto a tempo costante di SHA-256, `If-Match` obbligatorio, tetto del corpo | ✅ |
| `GET /vsop/api/v1/atc/sessions` | chiave facoltativa finché `Api:RichiediChiave=false` | `PortaDelleApi` | ℹ️ T-017 (noto, configurazione) |
| `POST /vsop/api/v1/transfers/resolve` | chiave obbligatoria, montato solo se `AuroraBridge:Enabled` | `PortaDelleApi` | ✅ (sola lettura) |
| `GET /services/vsop/admin/diagnostics/database-backup` (`DatabaseBackupRoute.Path`) | Admin | 404 agli altri, `Sec-Fetch-Site`, `DataProtectionKeys` escluse | ✅ |
| `VerificaChiaveApi.SegnaUsoAsync` | chi porta una chiave valida | passo di 5 min | ✅ |

## 4. Scritture che la UI fa DIRETTAMENTE sull'Infrastructure (nessun servizio in mezzo)

| Chiamata | Cancello nel metodo | Cancello nella pagina | Esito |
|---|---|---|---|
| `GlossarioPage.razor:1206` → `ITranslationMemory.SaveHumanAsync` (`EfTranslationMemory.cs:85`) | **nessuno**; `userId` passato dal chiamante | `Authz.IsEditor` (render) | ⚠️ d01-04 — è la **seconda porta** di `DocumentTranslationReview.CorreggiAsync` (che ha cancello, normalizzazione e controllo dei riferimenti nel servizio) |
| `GlossarioPage.razor:1113` → `DimenticaAutomaticheConLaFormulaAsync` (`EfTranslationMemory.cs:432`) | nessuno | `Authz.IsEditor` | ⚠️ d01-04 |
| `MediaCleanupCard.razor:123` → `IMediaMaintenance.DeleteOrphansAsync` (`EfMediaMaintenance.cs:36`) | nessuno (ricontrolla solo l'uso) | la card sta nel ramo Admin di `DiagnosticaPage` | ⚠️ d01-04 |
| `ImageBlockEditor.razor:187` → `IMediaStore.SaveAsync` (`EfMediaStore.cs:31`) | nessuno; quota controllata solo in pagina (`GuardQuotaAsync`) | editor in modifica | ⚠️ d01-04 |
| `DiagnosticaPage.razor:558` → `IImportStateStore.MarkSuccessAsync` (`EfImportStateStore.cs:26`) | nessuno | Admin | ⚠️ d01-04 |
| `StatsHome.razor:454` → `IStatsAccessLog.RecordProfileViewAsync` | — (è l'audit) | DS, dopo la guardia | ✅ |
| `AdminRolesPage`/`AuditPage`/`AeroportiPage` → repository in sola lettura | — | Admin/Editor | ✅ letture |

## 5. Letture riservate con cancello solo in pagina (non scritture, annotate per completezza)

- `IAtcStatsQueries` / `IAtcArchiveQueries` non hanno cancello: la guardia sta in `StatsHome.razor:430`,
  `StatsSessionPage.razor:167`, `AtcWorldArchivePage.razor:34/158/173`. Il §14.2 della carta statistiche vuole una
  riga di audit per ogni consultazione altrui: c'è in `StatsHome`, **manca** in `StatsSessionPage` e nella ricerca per
  VID di `AtcWorldArchivePage` → d01-11.
- `IAuditLogReader`, `IStaffRosterRepository.ListActiveAsync`: pagina Admin/Editor.

## 6. Finding che escono dalla matrice

| id | G | Titolo |
|---|---|---|
| d01-01 | S2 | ProfileSwapper anonimo: memoria e CPU dell'unico processo esauribili con file `.cpr` costruiti |
| d01-02 | S3 | vLOA: interruttori e ordine frequenze scrivono senza lock del documento (gemello di T-004) |
| d01-03 | S3 | Intro di pagina: il deposito non verifica il lock `editor:page-intro:*` (gemello di T-025) |
| d01-04 | S4 | Cinque scritture della UI vanno dritte all'Infrastructure senza cancello nel metodo (classe R-023/T-060) |
| d01-05 | S3 | Qualunque membro IVAO loggato crea incarichi «liberi» senza tetto, che finiscono nell'elenco admin |
| d01-06 | S4 | Reimport SID/STAR dall'editor senza nessun controllo di lock |
| d01-07 | S4 | `ProductionIdentityGuard` non può scattare: la guardia di D1 è tautologica |
| d01-08 | S4 | Commenti che dicono «solo admin» su sblocco e lock di struttura (il codice dice Editor) |
| d01-09 | S4 | Doppio clic su Permessi e Chiavi API: due operazioni sullo stesso DbContext, circuito giù |
| d01-10 | S4 | Riconvalida posizioni: durante un guasto IVAO una richiesta per utente ogni 15 min aspetta 15 s |
| d01-11 | S4 | Sessioni altrui viste dallo staff senza la riga di audit del §14.2 (dettaglio turno, archivio per VID) |
