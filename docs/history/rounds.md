# Changelog dei round (cronologico)

Storia incrementale del progetto, un blocco per round. **Per lo stato corrente** vedi `../../HANDOFF.md` e `../../README.md`; per il modello dati `../spec/modello-dati.md`. Lo storico dettagliato di ogni round (passi implementativi, test) è nella cronologia git e nei piani in questa cartella (`handoff-round5.md`, `piano-round20.md`).

> Da qui in avanti ogni nuovo round si annota **qui**, non più come banner accumulato in README/HANDOFF.

---

## Round 5 — Fusione Settore/Posizione (25 giu 2026)
Posizione e settore diventano **un'unica entità `Sector`** (callsign apribile + volume di spazio aereo); contenimento ad albero via `Sector.ParentSectorId` (sostituisce `HierarchyRelation`/`PositionSector`). Scope documenti **uno-a-molti** (`Sector.DocumentId` + `IsPrimary`). Enum `PositionType`/`PositionKind` → `SectorType`/`SectorKind`. Migrazioni rigenerate da zero (greenfield). Dettaglio: `handoff-round5.md`, `../spec/modello-dati.md` (banner Round 5).

## Round 6 — Aeroporto entità di prima classe (26 giu 2026)
**`Airport`** sotto una ACC (`Icao` univoco, `Name`, `AccId`); i settori d'aeroporto vi puntano via `Sector.AirportId` (`Sector.AirportIcao` denormalizzato). **L'aeroporto non ha gerarchia propria.** Anagrafica reale dalla sorgente (`IAirportDirectory`, adapter IVAO `/v2/airports?countryId=IT`, cache 12h). Gestione in `AeroportiPage` (assegna/sposta/rimuovi + «Auto-assegna noti»).

## Round 7 — Documento aeroporto da import (26 giu 2026)
«Genera documenti» crea dalla sorgente i settori **DEL/GND/TWR** (`/v2/airports/{ICAO}/ATCPositions`) e la **vIPI aeroporto Published** (Quote di transizione · Frequenze · Piste · SID). METAR/TAF **live**. Idempotente.

## Round 8 — Profilo strutturato aeroporto + editor dedicato (26 giu 2026)
I dati aeroporto (quote, frequenze, piste, SID, regole) sono **entità strutturate** (sorgente di verità) da cui si **rigenera** il documento. `AeroportoEditorPage` atomico, ACC-gated, no lock. Migrazione `AddAirportProfile`.

## Round 9 — Regole pista temporali + multi-pista + L/R (26 giu 2026)
Le `AirportRunwayRule` condizionano anche su tempo (orario/giorni/parità), multi-select piste reali, fallback headwind DEP/ARR su parallele. Migrazione `AddRunwayRuleSchedule`.

## Round 10 — Torre informativa + invariante torre + quote di transizione di default (27 giu 2026)
`SectorType.ITwr` (AFIS, trattata come torre); invariante «ogni aeroporto ha almeno una torre» (badge ⚠ no TWR + blocco delete unica torre). Tabella **TL di default** `TL = TA + margine` per fascia QNH, garantita a ogni rebuild.

## Round 11 — Indipendenza dalla sorgente + policy di import + `Vid`→`UserId` (27 giu 2026)
Porte dati esterne = **interfacce neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`, DTO `Source*`); adapter IVAO scelto via **`DataSource:Provider`**. Tutto ciò che la sorgente fornisce è **importato e in sola lettura** (policy globale **opt-out**, entità `ImportPolicy`, pagina `/vsop/admin/sorgenti`). `Vid`→`UserId` in codice e DB (migrazione `Rename_Vid_To_UserId`; a video resta "VID"). Migrazione `AddImportPolicy`. Vedi `../adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md`.

## Round 12 — Rebuild pagine, prefisso `/sop` → `/vsop` (27 giu 2026)
Redirect 301 dai vecchi URL; Home/landing ACC snellite; aeroporti su `/vsop/{acc}/airports`, APP su `/vsop/{acc}/apps`; «3 in evidenza» (`FeaturedRank`) dall'editor ACC; pagine fuori scope disabilitate (codice intatto). Migrazione `AddFeaturedRank`. Fonte rapida: `../spec/mappa-pagine.md` + `../spec/pagine-disabilitate.md`.

## Round 13–17 — ACC/settori importati + semplificazione dati (28 giu 2026)
ACC e settori si **importano dalla sorgente** (`/vsop/admin/acc`: `/v2/centers` + subcenter); nuovi cataloghi **`AccSector`**/**`AirportSector`** (chiave `ComposePosition`, mostra/nascondi + limiti quota, `IsPrimary` per la frequenza ★). Documenti aeroporto rigenerati in automatico. **Semplificazione:** frequenza = attributo del settore (`Sector.DefaultFrequency`), entità **`Frequency` eliminata** (`DropFrequencyTable`); rimossi `SectorGeometry`, `Airport.AtisFrequency`, categoria policy ATIS (`SimplifyDataModel`). `Fir`→`Acc` ovunque (`RenameFirToAcc`). Migrazioni: `AddAccSector`, `AddAirportSector`, `AddAirportSectorPrimary`. Dettagli: `../spec/modello-dati.md` §9.

## Hide aeroporti (28 giu 2026)
`Airport.IsHidden` (migrazione `AddAirportHidden`): un aeroporto si può **nascondere** in `/vsop/admin/airports`. Gli aeroporti **senza alcun settore** sono nascosti di default (`IsPublic = !IsHidden && Sectors>0`).

## Regole pista a soglie operative (28 giu 2026)
Le `AirportRunwayRule` passano a **soglie operative**: coda ≤ X kt, traverso ≤ Y kt (opz.), superficie (`RunwaySurface{Any,Dry,Wet}`). Coda/traverso calcolati dal vento; valutate in ordine, fallback miglior vento di testa. Migrazione `RunwayRuleThresholds`. Vedi `../spec/modello-dati.md` §9.9.

## Round 18 — Regole pista in ora locale + finestra stagionale, sezioni extra (29 giu 2026)
Orari «Avanzate» in **ora locale (LT)** (migrazione `RenameRunwayRuleTimeToLocal`); **finestra stagionale ricorrente** per regola (`DateFromMonthDay`/`DateToMonthDay` MMDD, migrazione `AddRunwayRuleDateWindow`); nuove **`AirportExtraSection`** (titolo + testo libero, migrazione `AddAirportExtraSection`). Dettagli: `../spec/modello-dati.md` §9.10–9.11.

## Round 19 — Gerarchia di copertura aeroporto‑foglia + editor grafico (29 giu 2026) — ⚠️ SUPERATO dal Round 20
Albero di fallback **Aeroporto → APP → settore ACC** a padre unico; nuovo `Airport.ParentSectorId` (FK→`Sector`, migrazione `AddAirportHierarchy`), editor grafico in `/vsop/admin/sectorstructure`, scrittura via `SetAirportParentAsync`. **Costruito sui `Sector` operativi** (che non includevano CTR/APP granulari del catalogo) → sostituito dal Round 20. La migrazione `AddAirportHierarchy` è stata **rimossa prima dell'applicazione**.

## Round 20 — Fonte unica dei settori (cataloghi) + gerarchia per callsign (29 giu 2026, 128 test)
I **cataloghi importati** (`AccSector`/`AirportSector`) diventano la **fonte autoritativa unica**; i `Sector` operativi sono una **proiezione** rigenerata dai cataloghi (`ISectorProjectionService.SyncFromCatalogsAsync`), risolvendo la doppia rappresentazione del Round 19.
- Gerarchia di copertura **per callsign, cross-ACC**: `ParentCallsign` su `AccSector`/`AirportSector`(solo APP)/`Airport` (sostituisce `Airport.ParentSectorId`). Migrazione `AddHierarchyParentCallsign` + `Sector.IsProjected`.
- Sync idempotente (hook negli import + dopo ogni `SetParent`): upsert per callsign che preserva `Sector.Id` + i legami documento; orfani/nascosti → `IsActive=false`; settori seed/manuali (`IsProjected=false`) mai toccati → **test AoR S1–S10 intatti**. `TopologyBuilder` invariato.
- Editor globale `IHierarchyEditingService` in `/vsop/admin/sectorstructure`: UI a **card per ACC** (alberi con radice in quell'ACC), comprimi/espandi card+rami, ricerca dell'intera gerarchia, dettaglio sticky con picker padre e **Applica**.
- **Fuori ambito (follow-up):** doc+AoR girano ancora sui `Sector` (proiezione); eliminazione totale di `Sector` + risoluzione live "chi controlla l'aeroporto adesso" = fase live.
- Piano esecutivo: `piano-round20.md`. Modello: `../spec/modello-dati.md` §9.12.

## Round 21 — Editor + viewer APP non remotizzati (29 giu 2026)
Documentazione propria per gli **APP standalone** (non remotizzati): entità **`AppProfile`**/**`AppFrequencyLink`** ancorate 1:1 al `Sector` APP. Editor WYSIWYG `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`) con 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + sezioni custom, riordino drag-and-drop + tasti, nascondi sezioni; viewer `AppnPage` data-driven. **Frequenze/Coordinamenti/AOR derivati live** (`IAppProfileService`, `AorPolygonProjector`, mappa Leaflet `vipi-aor.js`). Migrazioni `AddAppProfile`, `AddAppCustomSections`, `AddAppHiddenSections`. Instradamento via `DocumentSummary.IsStandaloneApp`. Dettagli: `../design/piano-editor-appn.md` + `../spec/modello-dati.md` §9.13.

## Round 22 — Shape tonda TWR + coord aeroporto + rifiniture trasferimenti/AOR (30 giu 2026, 157 test)
- **Shape tonda 5 NM di fallback per le TWR senza poligono.** IVAO espone le TWR con `regionMapPolygon = "[]"` (vuoto) → non disegnabili. `TowerShapeFallbackService` genera un cerchio (`CircleShapeBuilder`, formato `[[lng,lat],…]`) solo sulle TWR **vuote/degeneri** (decise col `AorPolygonProjector`), marcate **`IsShapeSynthetic=true`**, mai sovrascrivendo shape reali. Job in `AirportSectorImportHostedService` (import isolato in try → il fallback gira anche senza credenziali).
- **Coord aeroporto** (`Airport.Latitude/Longitude`) popolate all'import dal blocco `airport` del dettaglio **`/v2/ATCPositions/{compose}`** (`SourceAtcPosition.AirportLatitude/Longitude`); ripiego = centro del poligono di un settore fratello. Migrazione **`AddAirportCoordsAndTwrSyntheticShape`**.
- **AOR APP:** il componente `AppAor` mostra anche le shape delle TWR dell'aeroporto come overlay Leaflet con toggle «Shape torre».
- **Trasferimenti editabili** in-place (`AdminTrasferimentiPage`); **Coordinamenti APP** «verso ACC» suddivisi in **Partenze/Arrivi**. **TODO futuro:** shape reali TWR dal sectorfile GitHub (`DataSource:Provider`). Dettagli: `../spec/modello-dati.md` §9.14.

## Round 23 — vIPI ACC data-driven a blocchi (2 lug 2026, 164 test)
La vIPI a livello **ACC** resa **data-driven**, specchio dell'editor APP (round 21). Documento a **blocchi** ancorato 1:1 all'`Acc`: **Aerovia** (settori CTR) + **gruppi-APP** (settori APP scelti). Nuovi `AccProfile*` (models/service `AccProfileService`/registry `AccSections`) + `EfAccProfileRepository` + migrazione additiva **`AddAccProfile`** (tabella `AccProfiles`, `BlocksJson`, unique su `AccId`).
- Solo lo stato **editoriale** è persistito (JSON monolitico via `SaveBlocksAsync`); **AoR/frequenze/coordinamenti derivati live**. `AccConfiguration` (settori aperti) guida l'AoR (unione poligoni); `AccRegulatedArea` (aree regolamentate strutturate). Coordinamenti = flussi posseduti dai membri + **entranti** (arrivi da CTR vicini).
- **Freq-link ACC per callsign**: editor `FreqLinkEditor` (chip rimovibili + picker) su `FreqLinkCallsigns` dentro il blocco (no save separato come l'APP).
- Pagine: viewer `AccVipiPage` (`/vsop/{acc}/vipi`), editor `AccEditorPage` (`/vsop/{acc}/editor`), componenti `AccAor`/`AccCoordinationView`. Vecchia Estesa a prosa → `/vsop/{acc}/vipi-doc` (editor generico `/editor-doc`). **Niente lock/RowVersion** (coerente col data-driven), authz server-side. Dettagli: `../spec/modello-dati.md` §9.15.

## Round 24 — Rework editor ACC (multi-albero + config + aree speciali) (3 lug 2026)
Otto rifiniture alla vIPI ACC data-driven: **multi-albero** per ACC (una vIPI per radice CTR, query `?tree=`; `AccProfile` ora ha chiave composita `AccId`+`RootCallsign`), **mappa AoR singola** con chip toggle per settore + selettore configurazione, **tabella configurazioni** (settore aperto → assorbiti, CP/Range), **aree speciali IVAO** importate (`SpecialArea` per ACC, picker editor, shape proiettata). Dettagli in [[round24-acc-rework]] (memoria).

## Round 25 — Frase coordinamenti + pulizia pagine + QoL editor (3 lug 2026)
- **Frase di coordinamento** per riga CoP (albero Settore(NE)→ACC→Aeroporto→Arrivi/Partenze): `CoordinationSentenceComposer` + template editabile **content-su-file** (`content/coordination-sentence.json`, hot-reload) con override per-documento (`AccBlock`/`AppProfile.CoordinationSentenceTemplate`, migrazione `AddAppCoordinationTemplate`).
- **Pulizia pagine**: rimosse `VipiDocument`/`EditorPage`/`Aor3dPage`/`ExportPage`/`StatiPage` legacy/orfane.
- **QoL editor ACC**: barra sticky, sezioni comprimibili, anteprima, conferme cancellazioni, Ctrl+E, duplica config/gruppo (`vipi-editor.js`).

## Round 26 — Fix nomi proiezione + limiti verticali read-only (3 lug 2026, commit `008f32e`)
- **Nomi proiezione**: `EfSectorProjectionService.FriendlyName` usa `AtcCallsign` IVAO come primario (fallback `"{ICAO} {Tipo}"`), riarmonizzando i segnaposto senza clobberare i nomi custom admin.
- **Limiti verticali = verità primaria della sorgente**: `AirportSector.LimitsFromSource` (flag + migrazione `AddAirportSectorLimitsFromSource`) → input limiti read-only nell'editor quando la sorgente li fornisce; guardia server in `SetLimitsAsync`. Il commit `008f32e` (branch `round20-fonte-unica-settori`) include tutto il WIP round 23-26.

## Round 27 — vLOA da ACC confinanti (import esteri + adiacenza + generazione) (3 lug 2026)
Feature «vLOA da ACC confinanti»: import ACC esteri da IVAO per i paesi vicini (`Neighbours:CountryIds`), calcolo di **adiacenza geometrica** dei settori (`PolygonGeometry.AreAdjacent`, soglia 8 NM, min-edge distance), staging delle coppie candidate (`NeighbourCandidate`, migrazione `AddNeighbourCandidate`) con override admin, generazione **1 vLOA per coppia** (`EfNeighbourRepository.MaterializeAndCreateVloaAsync`). Pagina `/vsop/admin/confinanti` con verifica coppia (elenco + mappa shared-viewBox Leaflet). **Editor vLOA documentale** (primo su `IEditingService`): `VloaEditor.razor` su `/vsop/{acc}/apps/editor?vloa=<estero>`, struttura obbligatoria a 7 sezioni (`VloaSections`/`VloaStructureSeeder`). Dettagli: [[round27-vloa-confinanti]].

## Round 28 — vLOA data-driven + esteri persistiti (4 lug 2026)
- **AoR/Frequenze/Coordinamenti della vLOA derivati** dai dati (unione lato IT + estero), come le vIPI: `VloaProfile` (1:1 Document, migrazione) tiene lo stato editoriale (settori/frequenze/sezioni nascosti); `VloaProfileService` deriva le viste; editor+viewer con toggle e tabelle per direzione.
- **Subcenter esteri persistiti** durante l'import confinanti come `AccSector` (`Acc.IsForeign`, migrazione `AddForeignSubcentersAndAdjacency`) → proiettati in `Sector`, con **gerarchie estere** editabili (`/vsop/admin/sectorstructure`, gate admin) e **trasferimenti da/verso esteri** (mittente estero nell'editor trasferimenti). Frequenze in due tabelle (IT / `{nazione}-{ACC}`); AoR mostra solo i settori **effettivamente** confinanti (calcolo geometrico al volo). ACC esteri esclusi da home/header (`ListAccs` filtra per prefisso divisione). Dettagli: `../spec/modello-dati.md` §9.16, [[round28-vloa-datadriven]].

## Round 29 — Versioning AIRAC (release schedulate) + task editor (4 lug 2026)
- **Release AIRAC per ciclo** su TUTTI i tipi (vLOA/vIPI ACC/APP/Aeroporto): entità unica `DocRelease` (snapshot **solo scelte editoriali**; poligoni/frequenze/trasferimenti restano live), migrazione `AddDocRelease`. Modello «working live = bozza, pubblica snapshot»: `ReleaseService.PublishAsync(ciclo)`/`PublishNowAsync`; il pubblico vede la release con data efficace ≤ adesso più recente (selezione per data, non per stringa AIRAC). `AiracService` esteso (`EffectiveUtcForCycle`/`NextCycles`). Viewer intercettano la release effettiva; editor ACC/APP col pannello riutilizzabile `ReleasePanel.razor`.
- **Task management editor**: `EditorTask` (legato a documento o libero; stati Todo→InProgress→InReview→Done+Blocked; scadenza AIRAC; migrazione `AddEditorTask`). Pagine `/vsop/tasks` (i miei, kanban-lite) e `/vsop/admin/tasks` (dashboard admin: assegna + avanzamento + ritardi). Editor auto-assegnano su doc editabili o task liberi. Dettagli: `../spec/modello-dati.md` §9.17, [[round29-versioning-airac-tasks]].

## Round 30 — QoL pagina Bozze & versioni (4 lug 2026, 190 test)
Rework di `/vsop/versioni`: **elenco unificato** dei documenti (Document vLOA/aeroporto + profili ACC/APP) via `IDocumentAdminService` (una query per fonte, no N+1; versioni/release lazy on-expand); **ricerca** + **filtri** per tipo e stato (pubblici/bozza/nascosti). **Nascondi reversibile** (`Document`/`AccProfile`/`AppProfile.IsHidden`, migrazione `AddDocumentHideFlags`; loader pubblici e profile-service escludono i nascosti) + **elimina definitivo** (con conferma, admin; pulisce release orfane). **Annulla release** (`ReleaseService.CancelReleaseAsync` → promuove la precedente). **Anteprima completa** (`/vsop/release/{id}`, rende il payload come sarà) + **riepilogo differenze** vs release in vigore (`DiffAsync`, conteggi sezioni/blocchi). Dettagli: `../spec/modello-dati.md` §9.18, [[round30-versioni-qol]].

## Round 31 — Unificazione editor/versioni + ridisegno route vLOA (4 lug 2026)
- **Editor hub assorbito in `/vsop/versioni`**: eliminata `EditorHubPage.razor` (route `/vsop/editor`). La pagina Bozze & versioni è ora l'unico hub documenti: tasto **«Nuovo documento»** (→ `/vsop/editor/newdoc`), «Apri editor» per riga, accessibile a **staff/editori** (admin o grant, entry dal tasto ✏️ in topbar). Lista completa a chiunque acceda (nessun filtro per grant; azioni hide/elimina/annulla-release restano admin). Link ripuntati: `SopLayout`, `AdminGrantsPage`, `NewDocumentPage`, `TaskDocLink`, `SopHome`.
- **Route vLOA keyed su codice ACC vicino** (non più docId): view `/vsop/{acc}/vloa?acc=YYYY` (merge in `VloaListPage`; `VloaPage.razor` eliminata), editor `/vsop/{acc}/vloa/editor?acc=YYYY` (`VloaEditorPage` riscritta: host di `VloaEditor` + chooser senza `?acc`). Rimossi lo stub `/vsop/{acc}/editor-vloa` e l'host hack `apps/editor?vloa=` (branch tolto da `AppEditorPage`). Nuovo campo `VloaRow.NeighbourCode` (codice ACC vicino da `Party.Neighbour.Sector.Acc.Code`); deep-link ricerca/changed → `vloa?acc={vicino}`. Link ripuntati: `AccLanding`, `ConfinantiAdminPage`, `VersioniPage`, `ScreensIndex`. **Assunzione**: subcenter esteri con `Acc` (round 28) e coppia unica per ACC; righe legacy senza code non compaiono. Vedi `../spec/mappa-pagine.md`.

## Round 32 — Rework UI pagine /vsop (5 lug 2026, 87 infra test)
Rifacimento pagina-per-pagina di `/vsop`. **Home** (`SopHome`/`SopLayout`): ricerca in header (presente ovunque, `/` focussa via `vipi-ui.js`), badge posizione staff, sezione Staff collassabile (`data-persist` localStorage). **Landing ACC**: card `.nav-card`/`.apt-card` neutre, badge conteggio reale, **solo doc pubblicati** (aeroporti su `ManagedDoc.IsPublished`, APP standalone non-hidden). **Aeroporti elenco**: solo vIPI **pubblicate**; `RebuildDocumentAsync` ora crea il doc in **Draft** (era Published) → lo staff pubblica a mano; card con meteo mini non-bloccante (`LoadMeteoAsync` + `_wxGen` anti-corsa); `VersioniPage.Publish` acquisisce il lock prima di `PublishAsync`. **Apps/vLOA**: card ridisegnate, fix DocId passato a `VloaDocumentView` (derivate live). **Uniformazione 4 documenti**: tutte con `.doc-layout` (TOC sticky + card + rail Riepilogo/Collegamenti), tasto ✎ Editor gated `_canEdit`, breadcrumb coerenti. **AoR**: chip per-torre anche in APP pubblica (`GetAorViewAsync`). **Coordinamenti**: Espandi/Comprimi per (sotto)sezione (`window.vipiDetails`). Dettagli: [[round32-ui-pages-rework]].

## Round 33 — Anteprime documenti unificate (5–6 lug 2026, 87+91 test)
Unificate le anteprime dei 4 tipi (ACC/APP/Aeroporto/vLOA) in un solo schema, reso **dentro il viewer tipizzato** di ciascun tipo (prima due sistemi incoerenti: bozza-live solo APP `?live=1`; anteprima release admin-only `/vsop/release/{id}` che rendeva solo Aeroporto/vLOA).
- **Parametro `?as=`** uniforme su ogni viewer: assente → **pubblica** (release effettiva o live); `?as=draft` → **bozza live** (gated can-edit ACC, fallback sicuro a pubblica); `?as=rel:{id}` → **snapshot congelato** di una release. Nuovi `Vipi.Ui/Shared/PreviewMode.cs` (parser, alias legacy `live=1`) + `Components/PreviewBanner.razor` (banner condiviso Draft/Release, titoli `[Bozza]`/`[Anteprima]`).
- **Servizi**: `ReleaseService.GetLocationAsync` (redirect); `AppProfileService.LoadForReleaseAsync`→`AppReleaseView`, `AccProfileService.LoadForReleaseAsync`→`AccReleaseView` (dati+ciclo, refactor privato `LoadAsync(... overrideBlocks)`); Aeroporto/vLOA riusano `GetPreviewAsync`+`BuildFromRawAsync`. Flag infra `ignoreRelease` propagato (`IContentRepository.LoadAirport/VloaById/VloaByPair` → `EfContentRepository.LoadVipiAsync` → `IVipiViewService`): la bozza raw-doc bypassa lo swap release.
- **`/vsop/release/{id}` ritirata** → `ReleasePreviewPage` ora **redirect** al viewer tipizzato con `?as=rel:{id}` (vLOA: docId→vicino via `IDocumentAdminService`). Editor «Anteprima» → `?as=draft`; «👁 Anteprima» per-release → `?as=rel:{id}` (aggiunto in `ReleasePanel` per ACC); `VersioniPage.PreviewLink` costruisce URL tipizzati diretti.
- **Gating fail-safe**: non-editor/mismatch identità/URL forgiato → degrada a pubblica senza banner (mai fuga di bozza). Ciclo banner dalla release, non da `now`.
- **Review-fix (coerenza bozza)**: sezioni nascoste ACC/APP mostrate in `?as=draft` (pill «🚫 nascosta»); bozza raw-doc = stato **working** (vLOA via nuovo `preferWorking` = versione di lavorazione più recente, bozza inclusa anche se mai pubblicata; Aeroporto: TA/TL dal profilo strutturato live via `ApplyProfileTransition`); anteprima **release** Aeroporto congela il profilo (`_profile=null` → piste/TA/TL dal DocumentView del ciclo). Limite residuo: le sezioni testuali "altre" dell'aeroporto in bozza restano dall'ultima pubblicazione (il DocumentView si rigenera solo al rebuild). Dettagli: [[round33-unified-previews]].

## Round 34 — Vista operativa + QoL admin + import SID GitHub (8 lug 2026, 96 infra test)
Sessione ampia. **Vista operativa ACC** (`/vsop/{acc}/operativa`, `AccOperativaPage`) rifatta sul mockup `#reduced`: full-width, badge live, selettore postazione = **solo CTR** (default = CTR primario), `red-top-grid` (Frequenze + strisce delegate `collapsed-strip`), Trasferimenti masonry `xpair`/`xt-card` (riscritto `TransfersLive.razor`). **Vista rapida aeroporto inline**: chip `apt-quick` = aeroporti **discendenti del CTR aperto** nell'albero (diretti prima, delegati in fondo) → nuovo componente `AirportQuickPanel.razor` (TA/TL·QNH/piste suggerite/SID, **coerente col documento pubblico**: TA/TL da `BuildAirportVipiAsync` release-aware, piste/SID dal profilo; click server-side, no navigazione).
- **QoL `/vsop/admin/sectorstructure`** (`StrutturaPage`): strip completezza (aeroporti/APP orfani, filtro solo-orfani), anteprima copertura «Copre (dominio)» nel dettaglio, **re-parent drag&drop** (validato server-side), badge facility CTR/FSS + dot online + conteggio ricerca/scroll-al-match.
- **QoL `/vsop/admin/trasferimenti`** (`AdminTrasferimentiPage`): filtro testo/tipo + conteggi, **riordino punti** (`ITransferRepository.MovePointAsync` swap Order), **modifica mittente** del gruppo (già supportato dal repo), conferma inline su elimina gruppo.
- **Import SID da GitHub** (sectorfile **Aurora** `ivao-italy/it-aurora-sector`, repo pubblico raw): file per-aeroporto `<icao>.sid` + navaid `itfix.fix`/`itvor.vor`. `AirportSid` +campi (`IsImported`/`Priority`/`StableKey`/`SourceAiracCycle`/`ForcePublished`/`NeedsFixReview`) + entità `SidFixAlias` (migrazione `AddSidImport`). Parser puro `AuroraSectorfileParser` (completion prefisso→fix→VOR→alias, ambiguo/irrisolto→review; StableKey esclude la revisione numerica → priorità persiste tra import). Porta neutra `ISidProvider`/`SourceSid` + adapter `AuroraSidProvider` (`SectorfileOptions`, sez. `"Sectorfile"`). `SidImporter` (policy categoria `Sids`) + `SidImportHostedService`. Merge `ReplaceImportedSidsAsync`: cancella solo importate, preserva manuali, riapplica priorità/forzatura. **Pubblicazione differita**: importate pubbliche solo dal ciclo AIRAC **N+1** (o `ForcePublished`), filtro in `AirportQuickPanel`/`RebuildDocument`. Editor aeroporto: tabella importate (priorità/pubblica-ora/fix-review+crea-alias) separata dalle manuali. Verificato E2E: 55 aeroporti / 1477 SID reali.
- **Import gated con stato persistente**: entità `ImportState(Category, LastSuccessUtc)` + `IImportStateStore` (migrazione `AddImportState`) + helper `GatedImportLoop` — i 4 hosted service (Acc/AirportSector/SpecialArea/Sid) all'avvio **saltano il fetch se ancora fresco** (entro `*ImportHours`); stamp solo su successo; retry 1h su errore. Manuali sempre bypass, polling live 60s intatto. Fine dei fetch-all a ogni riavvio.
- **Fix «Auto-assegna noti»** (`/vsop/admin/airports`): oltre ad assegnare aeroporto→ACC ora **importa il catalogo settori** dei nuovi aeroporti + proietta (`SyncFromCatalogs`) → la colonna Sectori si popola subito (`AutoAssignAirportsAsync` ritorna gli ICAO creati). La generazione documenti resta separata. Dettagli: [[round34-sid-import-github]].

## Refactor 01 — Infra import condivisa (9 lug 2026, 199 test)
Primo giro del refactor strutturale (vedi `../refactor/00-overview.md` + `../refactor/REFACTOR-PROCESS.md`). Solo forma, comportamento invariato (199 test verdi = baseline).
- **DTO estratti**: gli 11 record DTO privati di `IvaoApiClient` in file singoli `internal` sotto `Vipi.Infrastructure/Ivao/Dtos/` (un tipo per file).
- **`IvaoApiClient` (5 porte, 565 righe) spezzato in un client per porta**: `IvaoAccClient` (`IAccDirectory`), `IvaoAirportClient` (`IAirportDirectory`), `IvaoAirportDetailClient` (`IAirportDetailProvider`), `IvaoUserClient` (`IUserDirectory`), `IvaoDivisionClient` (`IDivisionMembersProvider`) + `IvaoOnlineAtcClient` (fetch riepilogo ATC per il poller, nessuna porta). Plumbing HTTP condiviso (Combine/Authorize/GetString/GetJson/parser JSON/FormatFrequency) estratto in `IvaoHttp` iniettato (composizione, non eredità). DI e `AtcPollingHostedService` ricablati.
- **`AccImportUseCase` (`IAccImportUseCase`)**: corpo import ACC (fetch centers → upsert → subcenter → `SyncFromCatalogsAsync`), prima duplicato tra `AccAdminService.ImportFromSourceAsync` e `AccImportHostedService`, ora unico. Il manual applica `EnsureAdmin()` poi delega; l'auto delega senza guard. `Sync` d'import centralizzata (unica per import). ACC = pilota; aeroporti/SID/confinanti seguiranno nei doc 03/04/05.

## Refactor 02 — Import ACC + aree speciali (9 lug 2026, 199 test)
Secondo giro (`../refactor/02-import-acc-e-settori.md`). I passi 2-4 del doc erano già coperti da Refactor 01; residuo:
- **Record estratti** da `AccAdminService.cs` in file singoli: `AccAdminRow`, `AccSectorRow`, `AccImportResult`, `IAccAdminService`.
- **`SpecialAreaImportUseCase` (`ISpecialAreaImportUseCase`)**: loop import aree speciali per-ACC (fetch → upsert → prune, isolamento errori per-ACC) estratto da `SpecialAreaImportHostedService` in un use-case separato (non assorbito in `AccImportUseCase`: semantica/scheduling propri). Application non logga → il use-case ritorna i fallimenti per-ACC (`SpecialAreaImportResult.Failures`), l'hosted service li logga. Hosted service → thin wrapper delegante.
- **Manual = auto**: `AccAdminService.ImportFromSourceAsync` ora esegue anche `SpecialAreaImportUseCase` (gated `EnsureAdmin`) → il bottone «Importa da sorgente» produce lo stesso stato DB del job automatico (prima le aree speciali erano solo auto).

## Refactor 03 — Import aeroporti + scollegamento documento (9 lug 2026, 199 test)
Terzo giro (`../refactor/03-import-aeroporti-e-settori.md`). Contiene un **cambio di comportamento approvato** (non solo forma).
- **Record/interfacce estratti** in file singoli: `AirportSectorRow`, `AirportSectorImportResult`, `IAirportSectorService` (da `AirportSectorService.cs`), `IAirportSectorImporter` (da `AirportSectorImporter.cs`).
- **`AirportImportUseCase` (`IAirportImportUseCase`)**: import anagrafica aeroporti (punto 3) estratto da `StructureEditingService.AutoAssignKnownAirportsAsync`. Ritorna `AirportImportResult { Assigned, Failures }`; i fallimenti import per-aeroporto (prima scartati in silenzio) sono ritornati e **loggati dalla UI** (`AeroportiPage`, `ILogger`) — direttiva logging. `AutoAssignKnownAirportsAsync` = `EnsureAdmin` + delega (ritorno cambiato `int`→`AirportImportResult`).
- **Generazione documento scollegata dall'import (scelta B — CAMBIO COMPORTAMENTO)**: importare i settori **non genera più** il documento aeroporto. Rimosse le chiamate `EnsureAirportDocumentSystemAsync` da `AirportSectorService.ImportFromSourceAsync` e dal loop di `AirportSectorImportHostedService`; metodo `EnsureAirportDocumentSystemAsync` (no-authz) rimosso (morto). Import = solo catalogo + `Sync` (+ fallback shape nel job auto). Il documento si genera **solo** via «📄 Genera documenti» (`GenerateAirportDocumentAsync`, admin). `AirportSectorService` non dipende più da `IStructureEditingService`. La generazione documento verrà ripresa nel doc 08.

## Refactor logging + 04 — Criterio logging e import SID (9 lug 2026, 199 test)
- **Criterio logging (invariante #7 in `../refactor/REFACTOR-PROCESS.md`)**: il programma deve loggare ciò che fa; nessun swallow silenzioso; use-case Application ritorna i `Failures`, Infra/UI li logga; check aggiunto in Fase 3. **Fix gap doc 02**: `AccAdminService.ImportFromSourceAsync` scartava i fallimenti aree speciali nel manuale → ora ritorna `AccImportOutcome { Acc, SpecialAreaFailures }`, `AccAdminPage` li logga (`ILogger`). Audit doc 01: nessun gap.
- **Refactor 04 (import SID GitHub)**: estratta l'interfaccia `ISidImporter` da `SidImporter.cs` in file dedicato. Il resto del pipeline era già pulito (`GatedImportLoop` già in uso, logging conforme). Il punto di scrittura del merge SID (accoppiamento `AirportProfile` via `ReplaceImportedSidsAsync`) è rimandato al doc 08.

## Refactor 05 — Split confinanti test-first (9 lug 2026, 214 test: 199+15)
Quinto giro (`../refactor/05-import-confinanti.md`). `NeighbourImportService` era un file monstre (9 tipi, molte responsabilità) **senza test diretti** → split completo **test-first** (invariante #8 del runbook: irrobustire, non solo riordinare).
- **Record estratti**: 5 record (`NeighbourCandidateRow`/`NeighbourImportResult`/`NeighbourAdjacency`/`NeighbourMapShape`/`NeighbourPairDetail`) + `Aggregate`→`NeighbourPairAggregate` (internal) in file singoli.
- **`NeighbourAdjacencyComputer` (puro, nessun IO)**: cuore deterministico — filtro confine CTR/FSS, adiacenza domestici×esteri, aggregazione per coppia, catalogo estero confinante, e calcolo pair-detail (adiacenze+shape mappa). Isolato → **unit-testabile**: +14 test di caratterizzazione (`NeighbourAdjacencyComputerTests`).
- **`ForeignAccFetcher`**: fetch ACC+subcenter esteri via `IAccDirectory` (parallelismo throttled), esclude domestici, dedup, warning su fetch fallita. +1 test con fake directory.
- **`NeighbourImportService`** ridotto a orchestratore sottile: authz + scope DI + fetcher + computer + persist + upsert; `List/SetStatus/SetPolygon/AddManual/GenerateVloa` invariati. Logging `NeighbourDebugLog` conservato (HIT/PAIR/summary). **Rimandato a doc 08**: `MaterializeAndCreateVloaAsync` (generazione vLOA dentro l'import, accoppiamento dati↔documenti).

## Refactor 06 — Regole gerarchia in Application (9 lug 2026, 222 test: 214+8)
Sesto giro (`../refactor/06-gerarchia.md`). Gerarchia senza test diretti → estrazione regole **test-first** (#8). Scelta: estrarre le regole pure, NON migrare l'intero service (la maggior parte di `EfHierarchyEditingService` è data-access EF legittimo).
- **Enum/record estratti**: `HierarchyNodeKind`, `HierarchyNode` da `IHierarchyEditingService.cs` in file singoli.
- **`HierarchyRules` (Vipi.Application.Aor, puro, statico come `PolygonGeometry`)**: `IsForeignCode` (estero da prefissi divisione), `EnsureNoCycle` (anti-ciclo), `ComputeConfiningForeignCallsigns` (adiacenza estero↔domestico). +8 test di caratterizzazione (`HierarchyRulesTests`).
- **`EfHierarchyEditingService`** ora delega a `HierarchyRules` (rimossa la logica di business inline) e tiene solo il data-access EF (`LoadTree`, parent-map, save, cache confinanti). Comportamento invariato.

## Refactor 07 — Trasferimenti: DTO + ISP (9 lug 2026, 222 test)
Settimo giro (`../refactor/07-trasferimenti.md`). Giro piccolo; `TransferOnlineResolver` (risoluzione live) era già testato.
- **Estrazione**: `ITransferService` da `TransferEditingService.cs`; i 6 DTO (`TransferFlowRow`/`TransferPointRow`/`TransferFlowInput`/`TransferPointInput`/`ResolvedTransferPoint`/`ResolvedTransferFlow`) da `TransferModels.cs` (rimosso), in file singoli.
- **Porta di lettura `INeighbourReader { ListAsync }`** (ISP): `NeighbourImportService` la implementa (`INeighbourImportService : INeighbourReader`); `AdminTrasferimentiPage` inietta la sola porta di lettura invece del service import completo (la usava solo per leggere gli ACC esteri confinanti = mittenti estero→home). `ConfinantiAdminPage` resta sul service completo. Il problema originale del doc («la pagina triggera l'import») era mal descritto: era una lettura. Validazione già conforme a `Aor.ValidationException`.

## Refactor 08 — Modello documento (programma 08a–08i, ✅ completo 2026-07-10, 271 test)
**Decisione Fase 0**: unificazione **greenfield** dei due modelli documento su `Document` (classic) generalizzato con `SectionCatalog` condiviso; modello profile eliminato, documenti esistenti cancellati (no migrazione conversione); test-first. Decomposto in 6 sotto-giri.
- **08a (fatto, +13 test)** — `SectionCatalog` unificato (natura per key, membership per profilo, `Reconcile` unificato che sostituisce quello duplicato in `AppSections`/`AccSections`) + modello sezione ricorsivo `DocSection`/`DocBlock` (Testo/Tabella/Callout + sotto-sezioni). Membership rivista con l'utente: 6 sezioni universali; `purpose` e Military-areas rimosse (fuse in `regulated`). Aeroporto escluso (documento a struttura propria).
- **08b (fatto)** — estratti i tipi multi-classe dai file che sopravvivono all'unificazione: `Documents.cs` → 8 entità in file singoli; `EditingService.cs` → interfaccia + 2 eccezioni. Saltati i file profile (rimossi in 08d).
- **08c (fatto, +11 test)** — `SectionCatalogBridge`: mappa l'enum legacy `BlockSection` alle chiavi del catalogo, additivo (nessuna rottura), usato dalle migrazioni per-tipo.
- **08d-vloa (fatto, 246 test)** — `DocumentSection.SectionKind` (enum `BlockSection`) → `SectionKey` (chiave `SectionCatalog`) su tutto il modello classic: DTO (`RawSection`/`SectionView`/`EditableSection`) espongono `SectionKey`; viewer/editor vLOA + `AeroportoPage` confrontano per chiave; seed/builder convertono via bridge; migrazione EF `SectionKeyCatalog`. vLOA completamente migrata al catalogo. Approccio greenfield: nessuna migrazione dati (i test usano `EnsureCreated`).
- **Strategia B bocciata → ritorno a strategia A greenfield** (2026-07-10, owner): B (adozione incrementale del `SectionCatalog` senza droppare lo storage profile) lasciava divergenze inaccettabili (config APP degradata, corpo custom divergente, no Callout/sotto-sezioni). Ritorno ad A: migrare lo storage ACC/APP/Airport sul modello `Document` classic (template = vLOA).
- **08e–08i (fatto, 271 test)** — storage ACC/APP/Airport → `Document`+`DocumentSection`(keyed)+`ContentBlock`; override per-doc su side-entity unica `DocumentProfile`; editor+viewer editoriale unico (Prose/Table/Callout + sotto-sezioni); renderer derivati keyed condivisi + config ricca anche in APP; Airport migrato. Migrazioni **drop** delle tabelle profile: `DropAppProfile`/`DropAccProfile`/`DropVloaProfile` + `AddDocumentProfile`. Repository rewired su `Document`. **08i rename (2026-07-11)**: i tipi Application `*Profile*` (mal chiamati dopo il drop dello storage, non morti) rinominati per ruolo — `*ProfileService`→`AccDerivationService`/`VloaDerivationService`/`AirportEditingService`, repo→`*DerivationRepository`/`IAirportRepository`, data→`AccVipiData`/`AirportData`, file→`AccVipiModels`/`AppModels`/`AirportModels`; `DocumentProfile`/`SectionProfile` restano (legittimi). Build 0/0, 271 test. **Residuo opzionale**: creazione airport via use-case unico (ex-08h).

## Refactor 09 — Flusso di pubblicazione (registry polimorfico, 264 test)
**Fase 0**: ri-mappato lo stato reale post-08 (il cuore release era già collassato: tutti e 4 i tipi su `DocReleasePayload`, switch snapshot/signature/preview/hide/delete a ramo unico). Residuo per-tipo genuino = sola identità+routing. **Target approvato (owner): opzione B — registry polimorfico pieno**, stratificato in 2 porte per non violare i layer.
- **§3d (fatto)** — split file multi-classe: `ReleasePayload.cs`→`DocReleasePayload`+`VloaOverlaySnapshot`; `ManagedDoc.cs`→4 file; record di `ReleaseService.cs`→`ReleaseDiffRow`/`ReleaseDiff`/`ReleasePreview`/`ReleaseLocation`.
- **rete test-first (+9)** — caratterizzazione identità per-tipo (`ReleaseRepositoryTests` AuthAccCode+Snapshot App/Airport; nuovo `DocumentAdminRepositoryTests` ListAsync kind/key/acc) prima di spostare la logica.
- **§3a (fatto)** — `IReleaseTarget` (porta Application, impl Infrastructure) + `ReleaseTargetRegistry`; 4 impl (`Vloa/App/AccVipi/Airport ReleaseTarget`) risolvono `key→docId`, `key→accCode`, overlay flag, `TryDescribe` shape→`ManagedDoc`. `EfReleaseRepository` (snapshot/auth) e `EfDocumentAdminRepository` (list/getacc) **delegano al registry**; rimosso lo switch 4-vie + il duplicato `key→accCode`.
- **§3b (fatto)** — `IDocKindRoutes` (porta UI) + `DocRoutesRegistry`; 4 impl sotto `Vipi.Ui/Shared/Routing`. `VersioniPage.PreviewLink/EditorLink` e `ReleasePreviewPage` fanno lookup; rimossi 3 switch URL + il duplicato rotta-viewer.
- **§3c (fatto)** — pulizia switch vestigiali (`Signature`/`GetPreviewAsync`/`SetHidden`/`Delete`) a ramo unico.
- **§5 (fatto, +3)** — `ReleaseGenericFlowTests`: un tipo con enum fuori intervallo (99) è pubblicabile/preview/diff/elencabile/authz registrando **solo un descrittore**, zero modifiche ai motori → obiettivo utente ("nuovo tipo senza reimplementare la pubblicazione") provato.
- **Verifica live (CDP)** — EditorLink ACC/APP, `ViewerUrl(?as=rel)` snapshot congelato, redirect `/vsop/release/{id}`, hide/unhide via descrittore. Baseline test **252→264**.

## Manutenzione 08i + hardening processo (2026-07-11, 271 test)
Sessione di chiusura del debito post-08, senza cambi di comportamento.
- **Audit coerenza 08**: il codice era completo (tabelle profile droppate, repo su `Document`) ma il tracciamento mentiva — `00-overview.md` e `rounds.md` davano 08 ancora `🟡 strategia B ⏳`, l'header del doc 08 diceva `✅ COMPLETO`. Riallineati overview/§4/rounds.
- **08i rename** (dettaglio nella sezione Refactor 08): i tipi `*Profile*` Application, mal chiamati dopo il drop dello storage, rinominati per ruolo (`*DerivationService`/`AirportEditingService`/`*DerivationRepository`/`AccVipiData`/`AirportData`) + file. 28 `.cs` + 9 `.razor` + 15 file rinominati. `DocumentProfile`/`SectionProfile` tenuti (legittimi). Build 0/0, 271 test. Sorpresa: gli editor Blazor usavano i nomi (grep `.razor` diede falso negativo) → beccati dal compilatore.
- **Commenti stale corretti**: `<see cref>` a entità morte, riferimenti a `AppProfileService`/tabelle droppate.
- **Memorie riallineate**: `acc-profile-design` e `appn-editor-design` descrivevano lo storage pre-08 (`AccProfile.BlocksJson`, `AppSections`, `SaveBlocksAsync`) → riscritte a post-08 (parti di derivazione tenute, storage corretto).
- **Hardening `FEATURE-PROCESS.md`** (anti-vibecoding): la falla emersa (codice giusto, *record* rimasto vero a metà) non era coperta. Aggiunta **domanda 4 pre-flight «Propagazione»** (rimuovi/rinomini → aggiorna nomi/commenti/doc/memorie nello stesso giro) + 3 righe DoD (verify con traccia, tracciamento coerente header==indice==rounds, nessun nome/commento morto) + fix label «Regola del 3»→«del 2» (scattava a ≥2).

## Feature: configurazioni APP multi-settore (2026-07-12)
APP standalone con >1 settore (es. LIPE_APP → LIPE_W/LIPE_E) ora ha le **configurazioni** come l'ACC (settori aperti →
accorpamento → guida l'AoR). Nessuna rotta/migrazione nuova.
- **Modello**: riuso `AccConfiguration`/`AccConfigOpen` (no gemello). Storage nel blocco keyed `configurations` del Document.
- **Derivazione condivisa**: estratto `ConfigTableProjector` (puro); `AccDerivation.DeriveConfigTableAsync` ora copre anche i
  blocchi gruppo-APP (prima usciva vuoto per non-Aerovia); nuovo `AppDocumentService.DeriveConfigTableAsync` (inietta `IAorService`).
- **UI condivisa**: `Components/App/AppConfigurations.razor` (edit+view, `MapScope` per il link config↔mappa `data-cfgblock`),
  usato da `AppEditorPage`+`AccEditorPage`+`AppnPage`. Tolto il `ConfigEditor` duplicato in `AccEditorPage` (Regola del 2).
- **Bug fixato**: `configurations` era nel catalogo APP ma mancava nei Special/Live set di editor/viewer → sezione vuota.
- Test: `AppDocumentServiceTests.Configurations_Roundtrip_And_Derive_Accorpamento_Table`. Baseline 259 verdi (Application+Infrastructure).

## Feature: lock di editing esclusivo su pagine admin (2026-07-12)
Le pagine senza `Document` sottostante (sectorstructure/acc/trasferimenti/airports + editor/newdoc) hanno ora un lock
«una persona alla volta». Vedi memoria `edit-resource-lock-design`.
- **Nuovo modello per-risorsa** (non gemello del lock Document, che è per documentId): entità `EditResourceLock` (migr.
  `AddEditResourceLock`), `IResourceLockRepository`/`Ef*`, `IResourceLockService` + `ResourceLockKeys`.
- **2 chiavi**: `admin:structure` condivisa dalle 4 pagine catalogo (stessa topologia), `editor:newdoc` separata.
- **UI condivisa** `Components/EditLockBar.razor`: «Inizia/Fine modifica», banner lock altrui, forza sblocco admin. Le pagine
  disabilitano i controlli mutanti quando non tengono il lock; StrutturaPage guarda anche il drag-drop in codice.
- **Tab-close**: TTL corto 3min + heartbeat 60s → chiusa la scheda il lock si libera da sé. `DisposeAsync` **non** rilascia
  (navigando fra le 4 pagine che condividono `admin:structure` si perderebbe il lock ad ogni cambio pagina); rilascio immediato
  solo su «Fine modifica» / «Forza sblocco».
- Test `ResourceLockTests` (4). Baseline 272→276 verdi.

## Fix & rifiniture UI (2026-07-12, seguito)
- **Ruoli admin ACC-scoped**: i chief hanno codici col prefisso ICAO dell'ACC (`LIRR-CH`, `LIMM-ACH`), non `IT-`. Aggiunto
  `DivisionOptions.AdminAccRolePatterns = ["CH","ACH"]`; `EditAuthorizationService` deriva anche `^{prefissoIcao}[A-Z0-9]+-{ruolo}$`.
  Doc: `guide/config.md`, `adr-0004`. Test `AuthLockTests` (+5).
- **Rename UI ruoli → «Staff»**: «Area AOD / DIR» → «Area Staff», «staff AOC/AOAC/AOA» → «staff», «CH/AOD» → «staff» (10 file razor).
- **Lock editing pagine admin**: barra `EditLockBar` su sectorstructure/acc/trasferimenti/airports + newdoc (vedi sopra). Il lock
  `editor:newdoc` gate solo la creazione vLOA; i bottoni «Apri editor» (navigazione) NON sono gatati.
- **Sezioni collassabili ovunque**: nuovo `Components/CollapsibleBlock.razor` usato da `DocumentSectionsEditor` (editor ACC/APP/vLOA)
  + viewer (`AppnPage`/`AccVipiPage`/`VloaDocumentView`/`AeroportoPage`/`RidottaPage`/`SectionNode`). Le sotto-sezioni erano già
  collassabili. Fix collegato: `DocumentSectionsEditor` usa profondità **relativa** (le sezioni ACC — figlie di un blocco —
  tornano card `.block` di primo livello, non `.coord-sub`).
- **Config = callsign, non nome**: la tabella accorpamento mostra il callsign (`LIRR_NE_CTR`) invece del nome («Roma Radar»);
  rimosso `AccConfigTableRow.UnifiedName` e il parametro `names` da `ConfigTableProjector` (Absorbed = callsign).
- **Filtro nazione** nelle pagine admin ACC/struttura (select tematizzato, un filtro per entrambe le tabelle ACC/settori).

## Fix: direzione frasi coordinamenti + identifier APP consolidato (2026-07-13)
Due bug nella sezione Coordinamenti derivata (`AccDerivationService.DeriveCoordinationAsync` + `CoordinationSentenceComposer`), emersi su un flusso reale LIBB (Brindisi ES, arrivi LIRN):
- **Direzione invertita**: `AccDerivationService.cs` invertiva mittente/destinatario per gli arrivi con `next` di tipo CTR (`invert = Arrival && next è Ctr`). Euristica sbagliata: **tutti** i flussi arrivo nel dato reale sono `owner=cede → next=riceve` (come la pagina trasferimenti); il caso «vicino consegna a noi» è già coperto dal ciclo *entranti*. Rimosso l'invert → sempre `sender=owner, receiver=next`. Es. NILTO ora «Brindisi Radar ES trasferisce a Roma Radar TS», non il contrario.
- **Identifier APP perso**: un APP consolidato fornito dall'ACC (es. `LIRN_US0_APP`, Napoli su «Roma Radar», `MiddleIdentifier=US0`) mostrava solo il nome generico «Roma Radar». `CoordinationSentences.Compose` ometteva **sempre** il codice per APP/TWR → ora lo omette **solo se manca** il MiddleIdentifier. Es. «…trasferisce a Roma Radar US0…».
- Propagazione: aggiornati commenti + test `AccDerivationTests` (rinominato `Owned_Flow_Sentence_Reads_Owner_As_Sender`) e `CoordinationSentenceComposerTests` (`App_target_with_identifier_includes_code` + `App_target_without_identifier_omits_code`). Baseline verde: Application 138, Infrastructure 134.
- Backlog collegato: rework trasferimenti per **sorvoli senza aeroporto** (memoria `transfers-overflight-rework`, come/quando da decidere).

## Feature: sorvoli senza aeroporto nella sezione Coordinamenti (2026-07-13)
I flussi **Sorvolo/VFR/Altro senza aeroporto** ora compaiono nella sezione Coordinamenti Estesa (ACC + APP
non remotizzati). Prima venivano persi: la frase richiedeva un aeroporto e l'albero raggruppava solo
Arrivi/Partenze per aeroporto. Il modello/editor/DB già li accettavano (`AirportIcao` nullable) — nessuna
migrazione. Pre-flight FEATURE-PROCESS + carta approvata (`plans/linear-giggling-map.md`).
- **Frase kind-aware** (`CoordinationSentenceComposer.cs`): l'aeroporto resta obbligatorio per Arrivi/Partenze
  (senza → nessuna frase, «con destinazione» orfano), **opzionale** per Sorvoli/VFR/Altro (relazione neutra
  `tpl.Airport`). Es. «Roma Radar NE trasferisce a Brindisi Radar ES il traffico per aerovia su ELB.» ~~Vale
  anche per vLOA~~ (corretto nel seguito: vLOA non passava `flow.Kind` — vedi round «Fix sorvoli end-to-end»).
- **Cuore condiviso** `CoordinationDerivation` (nuovo, puro): estrae i due cicli owned+entranti + la direzione
  `owner→next` (niente invert) + la composizione, prima **duplicati** in `AccDerivationService` e
  `AppDocumentService`. Questo **corregge anche l'invert residuo dell'APP** (l'ACC era già stato corretto il
  2026-07-13). Test di caratterizzazione `CoordinationDerivationTests`.
- **Collocazione**: ACC → nodo «Sorvoli» sotto l'ACC accanto agli aeroporti (`AccAccAirports.Extras` +
  `AccExtraFlows`); APP → gruppo `AppCoordination.Overflights`. Bucket per `TransferFlowKind` (arr/dep →
  aeroporto; ovf/vfr/altro → sorvoli). Viewer `AccCoordinationView`/`AppCoordinationView` aggiornati.
- **Regola del 2**: centralizzato `TransferFlowKindLabels.Label` (era duplicato in `TransfersLive`,
  `AppCoordinationView`, `VloaDerivationService`).
- Test: Application 138→144, Infrastructure 134→137 (nuovi: cuore, frase sorvolo, nodo Sorvoli ACC,
  regressione invert APP + gruppo Overflights). Verifica live dei viewer: da fare guidando il flusso reale.

## Fix sorvoli end-to-end + parità livello (2026-07-13, seguito)
Seguito al round precedente, dopo che i sorvoli senza aeroporto **non comparivano** guidando il flusso reale.
Pre-flight FEATURE-PROCESS (`plans/encapsulated-hatching-stardust.md`). Il modello era già corretto (nessun
modello gemello aggiunto); i difetti erano a valle.
- **Bug propagazione vLOA (kind-aware mancato)**: `VloaDerivationService.DeriveCoordinationAsync` chiamava
  `CoordinationSentences.Compose` **senza `flow.Kind`** → default `Arrival` → un sorvolo senza aeroporto
  ricadeva nel ramo `return null` (frase vLOA nulla). Il round precedente aveva scritto «vale anche per vLOA»
  ma il consumer non era stato aggiornato. **Fix**: passare `flow.Kind`.
- **Causa radice «non compare»**: `CoordinationDerivation.Build` scarta i punti con ricevente non risolto
  (null o non in `types`) → un sorvolo con riga UNICOM non produce entry. Policy confermata (serve un
  ricevente), ma reso **visibile in editor**: badge «⚠ nessun ricevente» sul flusso, hint sul «+ Gruppo»
  disabilitato (manca il settore mittente), help chiarito (è il ricevente — interno o estero confinante — a
  far comparire i sorvoli). Il picker ricevente usa già tutti i settori attivi (esteri confermati inclusi).
  Caratterizzazione `CoordinationDerivationTests` (`Point_with_unresolved_next_is_dropped`,
  `Overflight_to_foreign_confining_ctr_stays_owner_to_next`).
- **Aeroporto obbligatorio per Arrivi/Partenze**: `TransferService.ValidateFlow` ora rifiuta un flusso
  Arrivo/Partenza senza `AirportIcao` (`ValidationException`); l'editor disabilita «+ Gruppo»/«Salva» con hint.
  Simmetrico alla frase kind-aware (arr/dep senza aeroporto = orfano). Sorvoli/VFR/Altro restano opzionali.
- **Parità livello (regola semicircolare)**: nuovo enum `LevelParity { Any, Even, Odd }` + campo
  `TransferPoint.Parity` (migrazione `AddTransferPointParity`, default `Any`). `LevelFormatting.Format` prende
  la parità e appende «(pari)»/«(dispari)» al `LevelText` → **propaga da solo** a viste ACC/APP/vLOA/live e
  alla frase (`{fl}`), senza rami per-tipo duplicati. Select nell'editor (add + edit riga). Seed EW ora mostra
  un sorvolo dispari/pari verso Roma TS (prima aveva ricevente null → sarebbe stato scartato).
- Test: Domain +`LevelFormattingTests`, Application 144→146, Infrastructure 137→139. Verifica live: app
  avviata, migrazione applicata pulita; da guidare i viewer per la conferma finale.

## Fraseologia coordinamenti: livello naturale + parità + «tutti i punti» (2026-07-13, seguito)
La frase infilava la parità nel `LevelText` («per — (dispari)», sgrammaticato). Ora la frase si compone dal
**livello strutturato**, non dalla stringa pre-formattata (che resta per le tabelle). Cambi in
`CoordinationSentenceComposer` (`CoordinationSentences.Compose` prende `value/unit/special/parity` al posto di
`levelText`; `BuildFl` riscritto) e call site allineati (`CoordinationDerivation`, `VloaDerivationService`).
- **Livello a parole**: `per FL150` → `a livello 150 o livello inferiore` (≤) / `o livello superiore` (≥);
  Exact → `a livello 240`; Special (`per aerovia`) invariato. Parità come parola finale (`… dispari`/`… pari`).
  Senza valore numerico + parità → `per un livello dispari`.
- **Punto**: CoP `ALL` (case-insensitive) o vuoto → `su tutti i punti` (`FallbackMissingPoint`).
  Seguito (2026-07-14): CoP `ALL to X` → `su tutti i punti verso X` (`FallbackAllToward`, placeholder `{dest}`,
  `X` = nazione/FIR come scritto, nessuna mappa codice→nome). Parse in `CoordinationSentenceComposer.ResolvePoint`
  (regex `^ALL(\s+to\s+…)?$`); campi template aggiunti anche a `CoordinationSentenceOptions`/provider (hot-reload).
  **Separati i tre casi** (bug reale: la frase usciva `su —` per CoP `ALL`): il file `content/coordination-sentence.json`
  override `FallbackMissingPoint = "—"` per il CoP VUOTO, e `ALL`/`ALL to X` lo ereditavano. Ora `ALL` usa
  `FallbackAllPoints` («tutti i punti»), `ALL to X` usa `FallbackAllToward`, solo il CoP vuoto usa `FallbackMissingPoint`.
  Diagnosi: dato (`Cop='ALL'`) e codice erano giusti, la regressione era il default globale nel json.
- `LevelText` (tabelle) invariato («FL150↓ (dispari)»). Test aggiornati: Application 146→151 (nuove attese +
  casi parità/tutti-i-punti); Domain 19, Infrastructure 139 invariati.

## Nascondi singolo settore: regole gerarchia + revisione documenti (2026-07-13)
Il tasto «🚫 nascondi settore» in `/vsop/admin/acc` c'era ma non applicava le regole di dominio (e falliva in
silenzio). Pre-flight FEATURE-PROCESS (`plans/luminous-hugging-river.md`). Nessun modello gemello: riuso della
catena `IsHidden`/`IsActive`; unico fatto nuovo = flag di revisione su `Document`.
- **Regola 1 (blocco radice)**: `AccAdminService.SetSubcenterHiddenAsync` rifiuta con `ValidationException`
  l'occultamento d'un settore **radice** con figli visibili (li orfanerebbe). Nuovo
  `IAccAdminRepository.GetSubcenterHideContextAsync` (+ DTO `SubcenterHideContext`) con **una** query che unisce
  `AccSector`+`AirportSector` sui `ParentCallsign`.
- **Regola 2 (reparent al nonno)**: `EfSectorProjectionService` step 4 — se il `ParentCallsign` d'un figlio punta
  a un settore nascosto, il figlio risale al primo antenato **visibile** (`NearestVisibleAncestor`, guard
  anti-ciclo), invece di restare agganciato a un padre disattivato. Un solo code-path (settore/ACC nascosto,
  orfano). Vedi `modello-dati.md §9.12`.
- **Regola 3 (revisione documenti)**: nuovi campi `Document.NeedsReviewUtc`/`ReviewReason` (migrazione
  **`AddDocumentReviewSignal`**). `IDocumentReviewService`/`EfDocumentReviewRepository`: reverse-lookup dei
  documenti ACC vIPI + APP + vLOA confinanti dove il settore compare (via `Sector.DocumentId` e
  `NeighbourCandidate.AdjacentHomeCallsigns`), set del flag + **1 incarico/doc** (`IEditorTaskService`,
  idempotente per titolo). Banner `DocReviewBar` nei tre editor (ACC/APP/vLOA) con «✓ Segna come rivista»
  (`ClearReviewAsync`, ACC-gated). Le CONFIGURAZIONI: il pool esclude già i settori inattivi (nessuna riga); il
  preset `OpenCallsigns` non si auto-modifica → lo cura l'editor guidato dal banner.
- **Fix bug UI**: `AccAdminPage.SetSubHidden`/`SetHidden` allargano il `catch` (`ValidationException` +
  `Exception` con log) → niente più fallimento silenzioso.
- **Fix «resta nel documento»**: `TopologyBuilder.BuildAsync(accId)` leggeva `Sectors` per `AccId` **senza**
  filtrare `IsActive` → un settore nascosto (disattivato dalla proiezione) restava nella topologia del doc ACC
  (AoR/coordinamenti/config) e non si poteva rimuovere. Aggiunto `&& s.IsActive` (coerente con `BuildGlobalAsync`
  e con `EfAccDerivationRepository`). Regressione `Topology_Excludes_Deactivated_Hidden_Sector`; AoR S1–S10 invariati.
- Test: Domain 19, Application 151, Infrastructure 139→146 (2 proiezione reparent + topologia + 4 `HideSectorReviewTests`).
  Verifica live: da guidare (tasto in `/vsop/admin/acc` → banner negli editor).

## Feature: nome aeroporto per aeroporti fuori DB nei trasferimenti (2026-07-14)
Additiva. In `/vsop/admin/trasferimenti`, quando si indica un aeroporto **non a catalogo** (nuovo/estero) si
può ora dare anche il **nome**, così nelle sezioni trasferimenti (editor, live/Ridotta, coordinamenti ACC/APP/vLOA,
frasi) compare «Nome ICAO» e non il solo ICAO. Nessun modello gemello: campo `TransferFlow.AirportName` (nullable)
accanto ad `AirportIcao` (migrazione **`AddTransferFlowAirportName`**), propagato a `TransferFlowInput`/`TransferFlowRow`.
- **Punto di fusione unico** (Regola del 2): `CoordinationDerivation.MergeAirportNames(airportMap, flows)` sovrappone
  i nomi dei flussi alla mappa ICAO→nome del catalogo (il catalogo vince, i nomi-flusso riempiono i buchi). Chiamato
  dai 4 consumatori (`AccDerivationService`, `AppDocumentService`, `VloaDerivationService`; il live legge il nome dal
  `Flow`). Così frase + etichette aeroporto beneficiano senza duplicare la logica.
- **UI**: campo «Nome aeroporto (fuori DB)» che compare solo quando l'ICAO digitato non è nel catalogo (form nuovo
  gruppo + edit gruppo); header aeroporto «✈ ICAO — Nome» (`AptHeader`, nome da catalogo o da flusso). Il nome viene
  salvato solo per aeroporti fuori DB (per quelli a catalogo il nome resta la fonte unica del catalogo).
  Per un aeroporto **fuori DB il nome è obbligatorio**: `NewAptMissingName` disabilita «+ Gruppo»/«Salva» con hint
  «Aeroporto fuori DB: indica il nome» (non si aggiunge un aeroporto a mano senza nome).
- Build Infrastructure + Ui verdi. **Verifica live: da guidare** (riavvio app → `Database.Migrate()` applica la colonna).

## Feature: coordinamenti vLOA in stile ACC + frasi in inglese (2026-07-14)
La sezione Coordinamenti delle vLOA era una **tabella piatta** (CoP/Flusso/FL/Da→A/Frase) in italiano. Ora è
**identica alla vIPI ACC** (gerarchia Settore→ACC→Aeroporto→Arrivi/Partenze + Sorvoli, `AccCoordinationView`) ma
**in inglese**, coerente col documento bilaterale. Nessun modello gemello: la vLOA riusa `AccCoordination`.
- **Grouping condiviso** (Regola del 2): estratto `AccDerivationService.BuildTree` → `CoordinationDerivation.BuildAccTree`
  (funzione pura, la lingua delle etichette di tipo arriva da un `Func<TransferFlowKind,string>`). Usato da ACC (IT,
  `TransferFlowKindLabels.Label`) e vLOA (EN, `LabelEn`).
- **Frase bilingue**: la fraseologia del livello/parità era hardcoded IT nel composer → spostata nel template
  (`CoordinationSentenceLevel`: `FlBody`/`OrBelow`/`OrAbove`/`ForLevel`/`ParityEven`/`ParityOdd`, default IT invariati).
  Nuovo `CoordinationSentenceTemplate.English` (stato/aeroporto/livello/punto in EN). `BuildFl(tpl, d)` legge dal template.
  Le vLOA compongono con `CoordinationSentenceTemplate.English` invece di `_sentence.Current`.
- **Modello**: `VloaCoordination` ora porta `AccCoordination HomeToForeign/ForeignToHome` (era `IReadOnlyList<VloaCoordRow>`);
  **rimosso `VloaCoordRow`**. `AccCoordinationView` ha il flag `English` (etichette/tabella localizzate). `VloaDocumentView`
  e `VloaEditor` rendono via `AccCoordinationView English="true"` (rimossi i `ReadCoord`/`CoordView` a tabella piatta).
- Test: Application 156 (nuovo `English_template_composes_english_sentence`), Domain 19, Infrastructure 146. Build completa verde.
  **Verifica live: da guidare** (aprire una vLOA → sezione Coordinamenti in stile ACC, frasi EN).

## Feature: lookup nome aeroporto fuori DB su IVAO nei trasferimenti (2026-07-14)
Additiva. In `/vsop/admin/trasferimenti`, per un aeroporto **fuori catalogo** (nuovo/estero) si può ora premere
«🔍 IVAO» accanto al campo «Nome aeroporto» per cercarne il nome sull'API IVAO invece di digitarlo a mano. Il
risultato riempie il campo nome libero del flusso; **non** viene inserito nel catalogo aeroporti (così NON compare
nei picker «Nuovo documento» — vincolo esplicito: niente documenti su scali non italiani).
- **Porta**: `IAirportDirectory.GetByIcaoAsync(icao)` — dettaglio di un SINGOLO aeroporto per ICAO (anche estero),
  sola lettura. Impl `IvaoAirportClient` via `GET /v2/airports/{ICAO}` (scope `configuration`), cache per-ICAO
  separata (`IvaoAirportCache.TryGetSingle/PutSingle`, non tocca il catalogo IT `Items`). Fake test aggiornato.
- **Use-case**: `IStructureEditingService.LookupExternalAirportAsync` → `ExternalAirportInfo(Icao,Name,City,AccCode)`
  (riusa la `IAirportDirectory` già iniettata). **Non** persiste nulla.
- **UI**: tasto «🔍 IVAO» nei form nuovo-gruppo e edit-gruppo (solo quando l'ICAO è fuori DB); esito accanto al campo
  (nome + città/FIR, o «non trovato»). Test: Infrastructure 146, Application 156. Build completa verde.
  **Verifica live: da guidare** (serve token IVAO configurato; provare un ICAO estero es. LGKR/LFPG).

## Audit correttezza + fonti-dati multiple (2026-07-14)
Revisione senior (bug/errori/duplicazioni di fonte). Fix confermati e verificati:
- **A1 — frequenza settore proiettato = sola lettura.** `EfStructureEditingRepository.SetSectorFrequencyAsync`
  rifiuta con `Vipi.Application.Aor.ValidationException` un settore `IsProjected`: la sua frequenza è di sorgente e
  `SyncFromCatalogsAsync` la riscrive a ogni sync (l'edit veniva perso in silenzio). Catalogo = fonte unica.
- **A2 — cache confinanti invalidata al choke point.** L'invalidazione (prima solo in `SetParentAsync`) ora avviene
  in `EfSectorProjectionService.SyncFromCatalogsAsync` (choke point di ogni mutazione catalogo: import/hide/neighbour):
  niente più set stantio fino al TTL 5 min. `InvalidateConfiningCache` è `internal static`.
- **A3 — orfani senza legami editoriali dangling.** Nel passo orfani del sync, disattivare un settore proiettato ora
  azzera anche `DocumentId`/`IsPrimary`/`FeaturedRank` (niente FK a documenti fantasma).
- **A4 — import confinanti atomico.** Nuova porta `IUnitOfWork` (impl `EfUnitOfWork`, transazione sul context scoped):
  `NeighbourImportService` avvolge persist catalogo estero + riproiezione in un'unica transazione.
- **Minori (B):** backoff esponenziale + jitter in `TransientRetryHandler`; margine token IVAO 120s (skew d'orologio);
  log esito import SID in `AuroraSidProvider`; `DevCurrentUserProvider` logga l'eccezione invece di ingoiarla;
  commento footgun su `IvaoOptions.DivisionMembersPathFormat` (non usabile col token app).
- **Falsi positivi scartati** (verificati): lock `EfResourceLockRepository` è corretto (acquisizione atomica
  `ExecuteUpdateAsync` + indice UNIQUE su `ResourceKey`); `MetarParser` `IndexOf('/')`/`t[4..]` guardati dalla regex/lunghezza.
- Test: Infrastructure 148 (+2: rifiuto edit su proiettato, orfano azzera i legami), Application 156, Domain 19.
  Build completa verde (a parte lock DLL del Vipi.Host in esecuzione). **Verifica live: da guidare** (import confinanti;
  tentativo edit frequenza su settore proiettato).

## Refactor doc 10: snapshot totale + RenderMode per sezione (2026-07-18/19)
Estende l'asse pubblicazione (doc 09): da freeze parziale (solo prose statiche; derivate sempre live) a **snapshot
totale** — al Pubblica si congela una fotografia completa (derivate incluse), con eccezioni **live per-sezione**
governate da un flag `RenderMode {Frozen,Live}` su `DocumentSection`. Branch `refactor/10-snapshot-totale`, 25 commit,
suite 353 verde, verify live confermata (boot+backfill su DB reale, visibilità pubblica, badge editor; flussi a click
confermati a mano dall'owner). Carta: `docs/refactor/10-snapshot-totale-e-rendermode.md`.
- **S2** `RenderMode` su `DocumentSection`+`RawSection` (migration `AddSectionRenderMode`, default DB Frozen) + `DocReleasePayload.FrozenSections` (sectionId→JSON view-model).
- **S3/S4** cattura al publish (`IFrozenSectionProvider`/`FrozenSectionRegistry`, provider vLOA/App/ACC/Airport; `ReleaseService` cattura le sole sezioni `Frozen`) + lettura al view (`IFrozenSectionReader`, `GetFrozenByKeyAsync` risolve l'Id dal `payload.Doc` per i doc a sezione unica) + resolver per famiglia (`Acc/App/Vloa/AirportViewDerivationService`: frozen se pubblica+release effettiva, sennò live).
- **S3b** SID aeroporto **de-cotta**: `RebuildDocumentAsync` non cuoce più la tabella SID → sezione keyed `sids` (default `RenderMode.Live`); merge editoriali+importate a **view-time** (`AirportSidDerivationService`). Le release pre-S3b (SID cotta, key `custom`) restano rese com'erano finché non si ripubblica.
- **S4c** toggle + badge Live/Frozen negli editor: `SetSectionRenderModeAsync` (draft-gated); `DocumentSectionsEditor` (condiviso ACC/App/vLOA) espone badge+toggle sulle sezioni derivabili; airport via `Get/SetSidsRenderModeAsync` + `RebuildDocumentAsync` **preserva** il RenderMode tra i rebuild.
- **S5** rimozione dell'overlay di visibilità morto (`VloaOverlaySnapshot`/`DocReleasePayload.Vloa`/`IReleaseTarget.IncludesVisibilityOverlay` + ramo in `SnapshotWorkingAsync`): la visibilità è dentro la fotografia congelata.
- **S6** **visibilità pubblica = release effettiva** (rimosso il fallback live alla versione pubblicata su tutte e 4 le famiglie; ACC senza guscio sintetico) + **migrazione A** (`BackfillMissingReleasesAsync`, al boot dopo `MigrateVipiDatabase`): copia statica per i Published senza release, idempotente → nessun buco pubblico.
- **Gate liste pubbliche** (AccLanding/AeroportoPage/AccOperativaPage): da `Status==Published` a `HasEffectiveRelease && !IsHidden` (nuovo `ManagedDoc.HasEffectiveRelease`, batch in `EfDocumentAdminRepository.ListAsync`) = stesso predicato del viewer.
- **Note:** overlay runtime (online settori, meteo/pista) sempre live sopra il congelato. Caveat accettato: AIRAC misto in pagina (sezione Live di ciclo diverso).

## Feature: retention pubblicazione (anti-bloat) (2026-07-20)
Il flusso di pubblicazione (doc 09/10) non potava mai nulla → crescita illimitata del DB su cicli AIRAC ricorrenti.
Due vettori: (1) ogni publish inserisce una riga `DocRelease` con lo snapshot JSON totale (post doc 10) e le vecchie
diventano solo `Superseded`, mai cancellate; (2) ogni publish archivia la versione pubblicata precedente
(`DocumentVersion` `Archived`) con tutte le sezioni/blocchi, tenute per sempre (ridondanti: la release porta già la
fotografia). Retention additiva, nessun cambio schema. Segue `FEATURE-PROCESS.md`.
- **`ReleaseRetentionOptions`** (sezione `ReleaseRetention` di appsettings, in `Vipi.Application`): `KeepSupersededWithinCycles`
  (default 13 ≈ 1 anno AIRAC), `KeepArchivedVersionsPerDocument` (default 3). Registrata in `VipiModuleExtensions`.
- **`IReleaseRepository.PruneReleasesAsync(type,key,keepFromUtc)`**: elimina le release `Superseded` con
  `ReleaseEffectiveUtc < keepFromUtc`. `Effective`/`Scheduled` mai toccate (stato diverso). La soglia la calcola
  `ReleaseService` via `IAiracService` (data efficace del ciclo corrente − N cicli), non il repo.
- **`IEditingRepository.PruneArchivedVersionsAsync(documentId,keepN)`**: pota le versioni `Archived` oltre le più recenti
  `keepN`, cancellando blocchi → sezioni (post-order) → versione in ordine esplicito per i FK `Restrict`
  (`Section`/`ParentSection` self-ref); `Current` (Published) e `Draft` intatte. Vedi memoria `ef-executedelete-tracker-constraint`.
- **Orchestrazione** in `ReleaseService`: potatura **per-publish** del bersaglio (in `SnapshotAndSaveAsync`, dopo lo snapshot)
  + `PruneAllAsync` (system op, enumera `IDocumentAdminRepository.ListAsync`). **Sweep al boot** `PruneVipiReleases`
  (`VipiModuleExtensions`/`Program.cs`, dopo `BackfillVipiReleases`): contiene l'accumulo storico, poi il per-publish lo mantiene.
- **Bonifica propagazione**: rimosso il testo bugiardo «il pubblico vede lo stato pubblicato/live corrente (fallback)»
  (rimosso in doc 10 §S6) da `AeroportoEditorPage`/`AppEditorPage`/`VersioniPage`/`ReleasePanel` → «il documento non è
  ancora visibile al pubblico. Pubblica per renderlo visibile.».
- Test: retention confini keep/delete per ciclo + versioni (cancellazione ordinata figli, Current/Draft preservati,
  idempotenza). Suite 356 verde.
- **Verifica live (2026-07-21) + fix off-by-one cap Archived.** Verify su DB reale: prune release Superseded oltre soglia
  (backdate + boot sweep, idempotente), visibilità pubblica = release effettiva (doc 10 S6, tutte 4 famiglie),
  freeze/ripubblica. **Trovato**: il cap versioni `Archived` restava a **N+1**. Due cause distinte:
  (1) *release-publish* (`ReleaseService.PublishNowAsync`) potava in `SnapshotAndSaveAsync` **prima** di
  `PublishWorkingVersionAsync` (che archivia la precedente); (2) *version-publish* (`EditingService.PublishAsync`, pulsante
  «Pubblica versione» di `VersioniPage`) **non potava affatto** (retention agganciata solo al release-publish).
  **Fix**: prune `Archived` spostato/aggiunto **dopo** l'archiviazione in **entrambi** i path
  (`ReleaseService.PruneArchivedVersionsForTargetAsync` dopo la promozione; `EditingService.PublishAsync` dopo
  `_repo.PublishAsync`, iniettando `ReleaseRetentionOptions`). In `SnapshotAndSaveAsync` resta solo il prune release
  Superseded (vale anche per lo schedulato, che non promuove). Sicuro: le release portano la fotografia, non referenziano
  le versioni. Test regressione `ReleaseGenericFlowTests.PublishNow_EnforcesArchivedCap_...` +
  `EditingRepositoryTests.EditingService_Publish_EnforcesArchivedCap_...` (rosso→verde). Suite **358** verde.

## Feature: aggiunta manuale di settori esteri a una coppia confinante (2026-07-21)
Un ACC può passare **direttamente a un settore estero** non catturato dall'import automatico (es. un avvicinamento
`LGKR_APP`). In `/vsop/admin/confinanti`, sulle righe **confermate**, un bottone «➕ Settore» apre un input: si digita
il callsign, il sistema lo **verifica sulla sorgente (IVAO)** e — se esiste — lo materializza come `AccSector` sotto
l'ACC estero della coppia (`CenterId = ForeignAccCode`) e **riproietta** come per gli altri settori. Da lì compare tra
i settori dell'ACC estero e nei picker dei coordinamenti.
- **Modello invariato**: il settore estero resta un `AccSector` proiettato in `Sector` da `SyncFromCatalogsAsync`
  (nessun modello gemello). Riuso di `PersistForeignCatalogAsync` (solo-upsert → sopravvive ai re-import) + proiezione,
  atomico via `IUnitOfWork`.
- **Verifica sorgente** in `ForeignSectorResolver` (isolato come `ForeignAccFetcher`), dispatch per natura del callsign:
  APP/DEP/TWR/GND/DEL → `IAirportDetailProvider` (`/v2/airports/{ICAO}/ATCPositions` + dettaglio per poligono/freq);
  CTR/FSS → `IAccDirectory.GetSubcentersAsync`. Parsing puro in `ForeignSectorCallsign.Parse` (ICAO+suffisso+natura).
- **Guard anti-collisione** (`INeighbourRepository.FindSectorOwnerAsync`): callsign già sotto lo stesso ACC → idempotente
  (solo avviso, con nota se nascosto → riattivalo da `/vsop/admin/acc`); già sotto un altro ACC (estero o italiano) →
  `ValidationException` (niente hijack/righe fantasma). Coppia non confermata → errore.
- API: `INeighbourImportService.AddForeignSectorAsync(candidateId, callsign)` → `AddForeignSectorResult`.
- Nessun cambio schema/rotta. Test: `ForeignSectorCallsignTests`, `ForeignSectorResolverTests`, `FindSectorOwnerTests`.
  Suite verde (19 dom + 194 app + 171 infra). **Verificato live** (2026-07-21): flusso su `/vsop/admin/confinanti` funzionante.
