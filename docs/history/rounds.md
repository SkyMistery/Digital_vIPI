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

## Feature: rimozione frase di coordinamento editabile per-documento (2026-07-21)
Rimosso completamente l'**override per-documento** della frase di coordinamento (introdotto con la riga 75). Il template
**globale** content-su-file (`content/coordination-sentence.json`, `ICoordinationSentenceTemplate`/`CoordinationSentenceTemplate`,
incl. `English` per le vLOA) resta la **fonte unica**: i coordinamenti si compongono sempre da `_sentence.Current`.
- **UI**: tolto il campo «Frase di coordinamento — template» dalla sezione Coordinamenti in `AccEditorPage`/`AppEditorPage`
  (ora viewer-only). Rimosso anche il codice morto lato App (`SaveCoordTemplate`/`PreviewCoordTemplate`, anteprima live + debounce).
- **Backend/dati**: rimossi `DocumentProfile.CoordinationSentenceTemplate` (colonna DB, migrazione `DropCoordinationSentenceTemplate`),
  i gemelli `AccBlockMeta`/`AccBlock`/`DocumentProfileData.CoordinationSentenceTemplate`,
  `IDocumentProfileRepository.SaveCoordinationTemplateAsync`, `IAppDocumentService.SaveCoordinationTemplateAsync` +
  il parametro `templateOverride` di `DeriveCoordinationAsync`, e il metodo `CoordinationSentenceTemplate.WithTemplate`.
- Test aggiornati (round-trip override, assembler blockmeta, fake `IAppDocumentService`). Suite verde (174 app + 164 infra).

## Feature: aree regolamentate — proprio ACC automatico + extra altri-ACC (2026-07-21)
Nell'editor vIPI ACC la sezione **Aree regolamentate** del blocco **Aerovia** pre-seleziona **tutte le aree del proprio ACC
in automatico** (dinamico: seguono gli import) finché lo staff non personalizza; si possono aggiungere **aree di altri ACC**.
- **Modello**: nuovo `RegulatedSelection { OwnAuto, OwnIds, ExtraIds }` (in `AccVipiModels.cs`); `AccBlock.AttachedSpecialAreaIds`
  (`List<string>`) **rinominato** in `AccBlock.Regulated` (semantica cambiata → nome cambiato). `OwnAuto` (tutte le aree del
  proprio ACC) vale solo per Aerovia; `ExtraIds` = aree di altri ACC (indipendenti dal modo auto/manuale). Deselezionare
  un'area propria → passa a Manuale; toggle **«Torna ad automatico»** ripristina il dinamico. Aggiungere aree extra non tocca l'automatico.
- **Persistenza**: il `regulated` BodyJson passa da array a **oggetto** `RegulatedSelection`; «puro automatico senza extra»
  azzera il BodyJson (resta dinamico). Back-compat lettura in `AccDocumentAssembler.ParseRegulated`: `null`=automatico,
  array legacy=`{OwnAuto:false, OwnIds:[...]}`, oggetto=nativo. Nessun cambio schema DB.
- **Derivazione/viewer**: `GetAttachedSpecialAreasAsync(accCode, block)` (firma con accCode) risolve own (auto=tutte del proprio
  ACC / manuale=sottoinsieme) + extra in coda; nuova `ListSpecialAreasExcludingAccAsync`/`ListOtherAccSpecialAreasAsync` +
  `SpecialAreaPick.CenterId` per il picker cross-ACC. `AccVipiPage` passa `Acc`.
- **Editor** (`AccEditorPage`): toggle Automatico/Manuale + picker proprio ACC + sezione «Aree di altri ACC». Gruppi APP invariati.
- Test: assembler (array legacy/oggetto/unset), derivazione (auto→tutte, manuale→sottoinsieme+extra, gruppo APP→OwnIds,
  esclusione cross-ACC). Suite verde (175 app + 168 infra).

## Fix: AeroportoEditorPage.DisposeAsync — JS interop in prerender (2026-07-21)
Log Kestrel sporcati da `InvalidOperationException` («JavaScript interop calls cannot be issued at this time … statically
rendered») a fine richiesta. Causa: la pagina è **prerenderizzata staticamente** e dismessa prima del render interattivo →
`vipiAirportEditorInit` non parte, ma `DisposeAsync` chiama comunque `vipiSetDirty`; il catch copriva solo
`JSDisconnectedException`, non l'`InvalidOperationException` del prerender. Fix: flag `_jsReady` (settato in
`OnAfterRenderAsync(firstRender)`), `DisposeAsync` salta il JS se `!_jsReady` e cattura anche `InvalidOperationException`.
Innocuo (dispose) ma rumoroso. Le altre pagine con JS in `DisposeAsync` (`AccOperativaPage`/`AppOperativaPage`/`RidottaPage`)
erano già immuni (`catch { }` totale).

## Uniformare l'editor aeroporto agli altri editor (2026-07-21)
`AeroportoEditorPage` era l'unico editor divergente (tab + toast custom + timeline release duplicata). Reso uniforme
alla famiglia App/ACC/vLOA, **senza cambiare il modello di editing** (resta edit diretto ACC-gated + «Salva» per sezione).
- **Chrome**: header `editor-bar`/`doc-head` + badge `save-badge` (Salvataggio/Salvato) + pill stato (Pubblicata/Bozza da
  `_hasEffectiveRelease`) + `DocReviewBar` (nuovo `IAirportEditingService.GetDocumentIdAsync(icao)` → id del Document
  proiettato via settori). Toast di successo rimosso (ora nel badge); resta il callout d'errore.
- **Corpo**: da tab (`_panel`/`SelectPanel`/`?panel`) a **scroll unico** di sezioni **collassabili** (`<details.ed-sec>` +
  `<summary.ed-sec-head>`, default aperte, freccia ▸ che ruota) con **mini-nav** a chip-àncora (`#sec-…`, `Anchor()`) +
  pulsanti **⊞ Espandi / ⊟ Comprimi tutte**. Stato aperto/chiuso nativo del browser (nessuno stato C# da sincronizzare);
  il pallino dirty resta nel summary anche a sezione chiusa. JS: `vipiEditorSections(open)` espande/comprime tutte, e un
  listener `hashchange` apre la sezione target quando salti dalla mini-nav. Dirty per-sezione: `_dirtyPanel` (string) →
  `_dirtySections` (set); guardia beforeunload attiva se ≥1 sezione sporca; Ctrl+S (`SaveAllDirty`, era `SaveCurrentPanel`)
  salva tutte le sporche valide.
- **Guarded non ricarica più**: in scroll-unico un reload totale cancellerebbe gli edit non salvati di ALTRE sezioni. I
  save-sezione puliscono solo il proprio dirty (repo = replace totale → buffer già coerenti); le op che servono fresh
  ricaricano mirato (`ReloadSectorsAsync`, `IsPublicNow` locale per la SID importata). `Reimport`/`ReimportSids` fanno
  `LoadAsync` totale (azione deliberata).
- **Release consolidata**: `ReleasePanel` esteso con parametri **opt-in** (`BeforePublishAsync`, `ShowDiff`, `AllowCancel`,
  `PreviewUrlFactory`) — default invariati (ACC intatto). L'aeroporto usa il componente (BeforePublish = warning dirty +
  conferma + `RebuildDocument`), eliminando la timeline/diff/cancel duplicati nel code-behind.
- Nessun cambio schema/rotta (solo il query `?panel` non documentato sparisce). Suite verde (19 dom + 178 app + 168 infra).
  Verifica live da guidare dopo riavvio host.

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

## QoL: editor trasferimenti — feedback, clona, batch, tastiera (2026-07-21/22)
Rifiniture UX su `/vsop/admin/trasferimenti` (solo UI, `AdminTrasferimentiPage.razor`; nessun cambio schema/service).
- **Descrizione gruppo** spostata nell'header accanto al tipo (troncata, `title` pieno) invece che sotto all'espansione.
- **Warning UNICOM azionabile**: la pill «⚠ nessun ricevente» apre l'edit del 1° punto (`FixUnicom`).
- **Submit da tastiera**: Invio nei campi liberi conferma nuovo gruppo / nuova riga / **edit riga in-place** (guardato dai
  picker aperti).
- **Lookup IVAO automatico** on-blur dell'ICAO fuori DB con nome vuoto (guard anti-doppione per ICAO; bottone 🔍 resta).
- **Toast fisso** (bottom-right) per il feedback, sempre visibile a ogni scroll (rimpiazza i banner in cima).
- **Batch multi-CoP**: il campo CoP accetta una lista (`VALMA, ELB, TOP`) → N righe stessi parametri (dedup case-insensitive).
- **Clona**: riga (form precompilato) e gruppo con **righe incluse** su un altro settore mittente (`AddFlowAsync` +
  loop `AddPointAsync`). Suite verde invariata (build + 384 test).

## Feature: condizione operativa sui trasferimenti — livello variabile per pista/area (2026-07-22)
Per molti aeroporti i **livelli di trasferimento variano con la pista in uso o le aree attive**. Modello **editoriale**
(deciso su carta, `FEATURE-PROCESS`): le varianti sono più righe con la **stessa CoP** e livelli diversi, ognuna
etichettata dalla condizione; il controllore legge quella attiva (nessuna deduzione live da METAR — la pista in uso è
scelta del controllore).
- **Modello additivo** su `TransferPoint` (nessun gemello): `ConditionKind {None,Runway,Area,Custom}` +
  `ConditionLabel` (max 80, **verità per il display**, denormalizzata → sopravvive a rename/rimozione config e agli
  snapshot pubblicati) + `ConditionRefId` (**soft-ref** a `AirportRunwayRule`/`SpecialArea`, **no FK**). Migrazione
  `AddTransferPointCondition` (backfill `ConditionKind='None'`). Sorgente etichetta **ibrida**: datalist delle config
  pista dell'aeroporto + testo libero. Dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7.
- **Frase**: slot `Condition` del template (`CoordinationSentenceComposer`, IT «con pista {label} in uso»/«con {label}
  attiva»/«in condizione {label}», EN equivalenti), appesa a fine frase prima del punto — appesa dal composer (non via
  placeholder) così vale anche per i template custom da file. `AppCoordRow.ConditionLabel` → colonna condizionale nelle
  sezioni ACC/APP/vLOA e pill nella vista Ridotta.
- **Editor** (`AdminTrasferimentiPage`): selettore condizione (kind + label con datalist config pista via
  `IAirportEditingService.LoadForViewAsync`) nel form riga; batch multi-CoP e clona (riga/gruppo) propagano la
  condizione. Validazione soft: kind ≠ None richiede una label.
- Slice verticali (0 test-net → 1 domain/migration → 2 DTO/repo → 3 composer/derivation → 4 viste → 5 editor →
  6 doc). Additivo, nessun rename. Test: composer clausola (IT/EN), derivation (label su riga+frase), EF round-trip
  (`TransferRepositoryTests`: condizione/None-azzera/Custom-drop-ref). Suite verde (19 dom + 200 app + 174 infra).
  **Verifica live UI da guidare** (binding Blazor dell'editor): richiede sessione admin + lock struttura su `localhost:5034`.

### Follow-up condizione trasferimenti + import piste (2026-07-22, stessa sessione)
Iterazione sull'editor trasferimenti + comprensione dell'import piste, guidata da uso reale (LIBD).
- **Picker condizione vincolato al DB**: «Pista» e «Area» diventano **tendine** (niente più testo libero + datalist).
  Area = `SpecialArea` dell'ACC (`IAccDerivationService.ListSpecialAreasByAccAsync`).
- **Fix sorgente piste**: la condizione «Pista» leggeva le **config** `AirportRunwayRule` (`d.Rules`, editoriali,
  spesso 0) → per LIBD «nessuna pista» pur avendo piste. Ora legge le **piste reali** `AirportRunways` (`d.Runways`).
- **Come si prelevano le piste** (indagine): IVAO `GET /v2/airports/{ICAO}/runways` → `IvaoAirportDetailClient.GetRunwaysAsync`
  → `AirportRunways`. Import **per-aeroporto, non bulk**: solo via «Genera documenti»/«Re-importa» dell'editor. In
  `vipi.db` solo LIRN aveva piste → gli altri (LIBD…) 0 perché mai re-importati. Distinzione: **piste** (`AirportRunways`,
  importate) ≠ **regole pista** (`AirportRunwayRule`, editoriali). Vedi memoria [[transfer-condition-model]].
- **Bottone bulk** `/vsop/admin/airports` → «↻ Re-importa da IVAO (tutti)»: itera gli aeroporti assegnati chiamando
  `IAirportEditingService.ReimportFromSourceAsync` (best-effort, rispetta la policy import). NB: fa merge nella bozza,
  **non** rigenera il documento pubblicato.
- **Estensione modello condizione** (dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7.1):
  - **Multi-pista in una riga**: `ConditionLabel` con Runway elenca più piste («16R / 16L»); editor multi-select.
  - **Pista + area in AND**: nuovo `ConditionAreaLabel` (overlay solo con Kind=Runway) → frase «con pista X in uso e
    Y attiva» (slot `RunwayAndArea` IT/EN). Migrazione `AddTransferPointConditionArea`. Etichetta combinata display via
    `TransferConditionText.Display` (`TransferPointRow.ConditionDisplay`). `VloaDerivationService` ora include la condizione.
- **Condizioni indipendenti + area ricercabile** (redesign su richiesta, stessa sessione): pista/area/personalizzata
  diventano **tre dimensioni INDIPENDENTI** (una riga può averle tutte). **Rimosso `ConditionKind` + enum
  `TransferConditionKind`**; aggiunta colonna `ConditionCustomLabel` (migrazione `SplitTransferConditionColumns`, con
  backfill Area/Custom → colonne). Editor: la colonna «Condizione» → **tre colonne** (Pista multi-select · Area · Personalizzata);
  l'**area** è un **picker con ricerca a digitazione** (typeahead). Composer: unisce le clausole presenti con `Condition.Join`
  («e»/«and»), pista+area con la forma dedicata. Dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7.1-7.2.
  Suite verde (**19 dom + 205 app + 174 infra**). **Verifica live pendente**: riavvio Host (applica migrazioni) + prova su LIBD.

## Audit full-stack + Fase 1 «rete di sicurezza» (22 lug 2026)
Revisione senior dell'intero sito (back/front/DB) → `history/audit-2026-07-22-criticita-full-stack.md` (15 criticità, 4 ALTE). Avviata la **Fase 1 (osservabilità + rete di test)**, nessun cambio di comportamento del prodotto:
- **Health-check migrazioni pendenti** (`VipiHealthCheck`): `GetPendingMigrations()` non vuoto ⇒ **Unhealthy** (schema drift ⇒ 503 su `/vsop/health`).
- **Osservabilità import**: `ImportState` +`LastAttemptUtc`/`LastError` (migrazione `AddImportStateLastError`); `IImportStateStore.MarkFailureAsync`/`GetAllAsync`; `GatedImportLoop` registra i fallimenti nel choke point; report read-only in `/vsop/admin/sorgenti`.
- **Rete di test UI/E2E** (colmato il gap «regressioni Blazor silenziose coi test verdi»): nuovo progetto **bUnit `Vipi.Ui.Tests`** (dispatch `BlockRenderer`, classe/icona dinamica `CalloutBlock`, encode XSS — **dimostrati mordaci** rompendo `@Kind`); nuovo progetto **`Vipi.E2E.Tests`** (WebApplicationFactory in-process: boot + `/vsop/health` 200 + landing 200 + DB migrato + grafo di scrittura risolvibile, DB in file temp isolato). `Program` reso partial per l'entry-point dei test.
- Suite: **19 dom + 205 app + 177 infra + 7 ui + 3 e2e = 411 verde**, build 0 warning. **Verifica live:** boot in-process verde (E2E) → `/vsop` 200. Resta Fase 2 (consistenza soft-ref, encode MarkupString) e Fase 3 (auth prod, Postgres/scala).

## Audit Fase 2 «correttezza dati» (22 lug 2026)
Nessun cambio di schema (sola lettura), nessun auto-fix.
- **B1 — report consistenza soft-ref** in **`/vsop/admin/diagnostica`** (admin): rileva **pista orfana** (`ConditionRefId` senza pista), **label pista divergente** (ident cambiato dopo il salvataggio), **area fantasma** (`ConditionAreaLabel` non tra le `SpecialArea`), **gerarchia dangling** (`ParentCallsign` non risolve nei cataloghi). Architettura: `IConsistencyReportRepository`/`EfConsistencyReportRepository` (fotografia read-only) + `IConsistencyReportService.Analyze` (**logica pura testabile**). Se ci sono finding, `/vsop/health` → **Degraded**. Voce nella dashboard admin (`AdminGrantsPage`). *Rileva, non vincola* — coerente con la scelta soft-ref (sopravvive agli snapshot pubblicati).
- **C1 — XSS**: `System.Net.WebUtility.HtmlEncode` sui valori dinamici interpolati in `MarkupString` (`StrutturaPage` callsign/label/AccCode, `AeroportoPage` `FreqName`), pattern gemello già corretto in `SearchPage.Highlight`/`MarkdownLite`.
- Test: 5 unit su `Analyze` (i 4 controlli + dataset pulito, **dimostrati mordaci**) + E2E su `/vsop/admin/diagnostica` 200. Suite **19 dom + 210 app + 177 infra + 7 ui + 4 e2e = 417 verde**, build 0 warning.

## Audit Fase 3 «produzione» (22 lug 2026)
Parte code attuata; cutover Postgres + scala Blazor pianificati in **ADR-0007** (`docs/adr/adr-0007-produzione-persistenza-e-scala.md`), non attuati.
- **A1 — tampone concorrenza SQLite**: `SqliteTuningInterceptor` (`DbConnectionInterceptor`) abilita **WAL** + **`busy_timeout=5000ms`** a ogni apertura, registrato nel path `UseSqlite` di `AddVipiInfrastructure`. Mitiga `database is locked`; il cutover a Postgres resta pianificato (migrations dedicate + istanza di validazione).
- **D1 — guardia identità prod**: `ProductionIdentityGuard.EnsureSafe(isDev, useDevIdentity)` in `Program` fa **hard-fail** all'avvio se l'identità dev fittizia (admin onnipotente) è attiva fuori da Development. Path di produzione `HostIdentityCurrentUserProvider` ora **coperto da test** (mappatura claim: semplici, array/oggetti JSON, `sub` fallback, no-staff⇒no-edit) nel nuovo progetto **`Vipi.Hosting.Tests`**.
- **A2 — scala Blazor**: direzione (viewer read-only statici/WASM + backplane) registrata in ADR-0007, nessun cambio render mode ora.
- Test: 1 Infra (pragma WAL applicato) + 13 Hosting (4 guard + 9 mappatura claim). Suite **19 dom + 210 app + 178 infra + 13 hosting + 7 ui + 4 e2e = 431 verde**, build 0 warning.
- **Esterni residui (non code):** montaggio RCL nel sito host + config `HostIdentity`/staff-code reali; esecuzione cutover Postgres; provisioning backplane.

## Audit — minori B3/B4/C3/C4 (22 lug 2026)
- **C4** — estratti da `StrutturaPage` i due `RenderFragment` che costruivano HTML a mano (`RenderCoverage`/`RenderChain`) in **componenti dichiarativi** `StructureCoverage`/`StructureFallbackChain` (dati calcolati dalla pagina, markup con `@` auto-encoded): **chiude C1 alla radice** (niente più `MarkupString`+`HtmlEncode` manuale). 6 bUnit inclusi 2 di regressione XSS (`<script>`/`<img>` escaped).
- **B4** — `modello-dati.md` §3: header **[SUPERATO]** su §3.2/§3.3/§3.5/§3.6/§3.9 (modello pre-Round 5/13) per non implementare su modello morto (§9 resta autorevole).
- **B3** — nuovo `docs/guide/dev-bootstrap.md`: checklist unica «da DB vuoto a sito popolato» (prima sparsa in note HANDOFF). Coerente con la scelta **«Nessun seed»** (nessun codice di seed aggiunto).
- **C3** — **chiuso come non-issue**: `/aor3d` già disabilitata (Round 12); `AorBlock`/`AreaMapBlock` sono contenuto **editoriale schematico** (poligoni da `BodyJson`), non stub finti; la mappa live è Leaflet negli APP. Nessun badge aggiunto (sarebbe fuorviante).
- Suite **19 dom + 210 app + 178 infra + 13 hosting + 13 ui + 4 e2e = 437 verde**, build 0 warning. Chiude l'asse audit 22 lug (Fasi 1→3 + minori); restano solo gli **esterni** (host mount / Postgres / backplane).

## Scaffolding provider persistenza (22 lug 2026)
Primo passo del cutover Postgres (ADR-0007 D1, step 1/4), senza istanza reale:
- `Persistence:Provider` (config) risolto da `PersistenceProviderResolver` (puro): default **`Sqlite`** (path operativo intatto: file + WAL/busy_timeout). Branch in `AddVipiInfrastructure`.
- `Postgres` selezionabile ma **fallisce all'avvio** con rimando all'ADR (cutover non attuato: servono pacchetto Npgsql + assembly migrazioni dedicato + validazione istanza). Nessun path silenziosamente rotto, nessuna dipendenza Npgsql non validabile aggiunta.
- Test: resolver (default/case-insensitive/sconosciuto→errore) + branch reale (`AddVipiInfrastructure`: Sqlite registra il DbContext, Postgres lancia con «adr-0007»). Config documentata in `guide/config.md` §1c. Suite **19 dom + 210 app + 188 infra + 13 hosting + 13 ui + 4 e2e = 447 verde**, build 0 warning.

## U4 i18n IT+EN — rollout completo (23 lug 2026)
Chiusura di **U4** della carta `design/piano-ux-hardening.md` (decisione owner: «completa l'adozione» IT+EN). Solo la **chrome dell'app** (etichette/pulsanti/stati/messaggi) è localizzata; il **contenuto editoriale** (vIPI/vLOA/aeroporti dal DB) resta IT by-design, i **termini ATC standard** (Delivery/Ground/Tower/Approach) restano EN.
- **Infra** (già presente da giri precedenti): `IStringLocalizer<SharedResource>` + `Resources/SharedResource.resx` (it) / `.en.resx`, `AddLocalization` + `UseRequestLocalization` (it default / en), switch runtime `?culture=en` (o cookie / Accept-Language).
- **Copertura completa**: nav pubblica (4) + viewer (10) + **admin 12/12** + **editor 12/12**. Ultimi lotti: **admin restanti (5)** `AccAdminPage`(+nazioni `Country_*`)/`SorgentiAdminPage`/`ConfinantiAdminPage`/`AeroportiPage`/`AdminTrasferimentiPage`; **editor (12)** componenti condivisi (`PreviewBanner`/`EditLockBar`/`DocReviewBar`/`ReleasePanel`) + `NewDocumentPage`/`VersioniPage`/`DocumentSectionsEditor`/`VloaEditor`+`VloaEditorPage`/`AppEditorPage`/`AccEditorPage`/`AeroportoEditorPage` (~1082 righe).
- **Pattern**: chiavi `Common_*` (riusabili) + `Pagina_*` (specifiche); chrome editor condivisa in `Ed_*`, timeline release in `Rel_*`; interpolazione `L["Key", arg]` con `{0}`; plurali IT via chiavi `_1`/`_N` + helper.
- **Gotcha (registrati)**: (1) MAI `L["x"]` annidato in `$"..."` → calcolare in `@code`; (2) `RenderFragment`/enum-label che usano `L` NON `static` né field-initializer → property `=>`/metodo istanza (CS0236); (3) `L["x"]` è `LocalizedString` → serve `.Value` per array `string[]`; (4) le **stringhe usate come chiavi di logica** (`_dirtySections`/`_panels`/switch) restano IT stabili, display via helper `PanelLabel(key)` — non localizzare gli identificatori; (5) HTML nei valori resx XML-escaped + reso con `(MarkupString)`.
- **Verifica**: **1071 chiavi IT+EN allineate** (diff nomi vuoto), build 0 warning, `Vipi.Ui.Tests` **13/13 verde**; **verify live IT↔EN OK** su tutte le rotte admin + editor (HTTP 200, corpo commuta: «Editor vIPI»↔«vIPI editor», «Regole scelta pista»↔«Runway selection rules», ecc.). `RidottaPage`/`RidottaAppPage` **saltate** (disabilitate).
- **Follow-up aperto** (fuori scope i18n): `Release()` inline di `AppEditorPage` non ancora migrato a `ReleasePanel` (dedup, annotato in memoria [[editor-uniform-pattern]]); revisione EN madrelingua consigliata.

## Stato verticale trasferimenti disaccoppiato dal vincolo (24 lug 2026)
Su richiesta owner: la parola «in discesa/salita/stabile» nella frase di coordinamento **derivava dal `LevelConstraint`**
(≤→discesa, ≥→salita). Errato: `≥` è un **bound di livello** («a 130 o superiore»), non una salita. Ora lo stato verticale
è una **dimensione indipendente** scelta a mano.
- Nuovo enum `TransferVerticalState { Unspecified, Level, Descending, Climbing }` + campo `TransferPoint.VerticalState`.
  Composer: `{stato}` da questo campo (non dal constraint), `Unspecified`→nessuna parola; `{fl}` (bound «o livello inferiore»)
  resta dal constraint. Rinomina `CoordinationSentenceState`→{Descending,Climbing,Level} + chiavi JSON `stato.*`.
- Migrazione `AddTransferPointVerticalState` con **backfill da constraint** (le frasi esistenti restano identiche); seed idem.
  Editor: nuova `<select>` «Stato verticale» (form add/edit, risorse `Xfer_VState*`). Dettaglio: `refactor/07-trasferimenti.md` §7.3.
- Suite **450 verde**, build 0 warning. Verify live frase Brindisi (PISIP) **pendente**.

## Editor trasferimenti — QoL (24 lug 2026)
Migliorie di quality-of-life su `/vsop/admin/trasferimenti` (richiesta owner dopo studio della pagina):
- **Anteprima frase live**: nuovo `CoordinationPreviewContext` (mappe types/name/code/airport/atc + template, stesse fonti di
  `DeriveCoordinationAsync` → l'anteprima combacia con l'output) via `IAccDerivationService.GetPreviewContextAsync`. Composizione
  in locale (funzione pura, nessun round-trip per tasto). Mostrata sotto ogni riga in lettura (toggle «Anteprima frasi») e live
  nei form add/edit.
- **Espandi/comprimi tutto** in toolbar + **auto-espansione** del percorso al gruppo dopo add/clona (`ExpandTo`).
- **«Salva e aggiungi»** (bottone + Ctrl+Invio): il form nuova riga resta aperto e conserva livello/ricevente/condizioni, azzera
  solo il CoP (inserimento in serie).
- **Esc** annulla add/edit inline; **Ctrl+Invio** salva l'edit riga (anche col picker aperto).
- **Sposta in cima/fondo** (`⤒`/`⤓`): nuovo `MovePointToEndAsync` (repo/service) che ricompatta gli `Order`.
- Fix propagazione: `ConfirmCloneFlow` (clona gruppo) ora copia anche `VerticalState` (prima lo perdeva).
- Test: `TransferRepositoryTests.MovePointToEnd_Reorders_To_Top_And_Bottom`. Progetti core verdi (Domain 23 · App 211 · Infra 191 · Ui 13; +Hosting 13 +E2E 4 = **455 tot**), chiavi i18n IT/EN allineate (1088). Verify live pendente.

## Audit import SID — idempotenza + parser (24 lug 2026)
Rifiniture di correttezza sull'import SID (sectorfile Aurora), dettaglio in memoria `sid-import-mechanism` e `refactor/04-import-github.md` §6.
- **Gate AIRAC = PRIMO prelievo**: `ReplaceImportedSidsAsync` conserva `SourceAiracCycle` originale se il contenuto è invariato (`ContentUnchanged` per `StableKey`); solo una revisione riparte dal ciclo corrente. Prima ri-timbrava a ogni run 24h → `IsPublicAt` mai vero → SID sempre nascoste.
- **Fix manuale conservato** tra reimport (snapshot `PriorSid`): se la sorgente ripropone il grezzo `NeedsFixReview` ma era già risolto a mano, ri-applica la risoluzione.
- **Lock per-ICAO** (`SemaphoreSlim` statico in `SidImporter`): nessun indice unico su `StableKey` → serializza job 24h + bottone editor (no righe duplicate da delete+add concorrenti). ⚠️ **Completato il 30 lug:** l'indice unico non è solo assente, **non si può aggiungere** (la `StableKey` esclude la cifra di revisione ed è legittimamente ripetuta), e lo snapshot `PriorSid` costruito con `ToDictionaryAsync` faceva **fallire ogni reimport** sugli aeroporti con due revisioni della stessa SID. Vedi `audit-2026-07-30-concorrenza-e-ridondanze.md` §2.
- **Parser navaid = solo NOMI** (`IReadOnlySet<string>`): coordinate non necessarie alla completion fix, rimossi `ParseCoord`/`ParseNavaidLines`; `ResolveFix` match esatto O(1) → alias → prefisso unico.
- Test: `SidImportRepositoryTests` (re-import ciclo invariato + fix manuale), `AuroraSectorfileParserTests` (nome-based). Infra **191 verde**.

## Audit concorrenza, codice morto e ridondanze (30 lug 2026)
Revisione senior su race condition / codice morto / duplicazioni, a build già **0 warning** (quindi tutto invisibile al compilatore). Documento completo: `audit-2026-07-30-concorrenza-e-ridondanze.md`. Suite **505 → 631 verde**.
- **8 fix di concorrenza.** I due che contano: `EfUnitOfWork` non azzerava il change-tracker fra i tentativi dell'execution strategy (al retry su Neon le entità del tentativo fallito venivano riemesse → doppi insert); e le cache dei provider Aurora stavano in campi d'istanza di servizi **transient** (`AddHttpClient<I,T>`) → cache per-risoluzione e `SemaphoreSlim` che non sincronizzava nulla, estratte nel singleton `SectorfileCache`. Poi: ABA sul CTS dell'heartbeat in `EditLockBar`, TOCTOU in `StaffLoginThrottle`, stampede + TTL sui vuoti nel meteo NOAA, snapshot atomici in `IvaoTokenProvider`/`IvaoAirportCache`, advisory lock + reconcile **indici** nell'init schema Postgres.
- **Import SID rotto in silenzio** su LIRF/LIMC/LIME/LIBG/LIED/LIEO/LIPQ: `ReplaceImportedSidsAsync` indicizzava le righe precedenti con `ToDictionaryAsync(StableKey)`, che lancia sugli aeroporti con due revisioni della stessa SID → **ogni reimport falliva**, e il job logga il fallimento per-ICAO a `LogDebug`. Ora indicizzazione first-wins e log a `LogWarning`. ⚠️ **La `StableKey` non è unica per design: non aggiungere un indice unico.**
- **450 righe di codice morto** rimosse: verticali `IEditAuditWriter`/`EfEditAuditWriter` e `IDivisionMembersProvider`/`IvaoDivisionClient`, `SectionShell.razor` orfano, `AccConfigAor`, `AccRegulatedArea`+`RegulatedAreas`, `AorPalette`, `UndoHistory<T>`, 7 metodi d'interfaccia senza chiamanti, `SourceAirport.Latitude/Longitude`.
- **Ridondanze estratte**: `FrequencyPositions` (era triplicata e **già divergente**: cella bianca invece del trattino nel documento aeroporto), `AirportViewFormat`, `ReleaseDiffTable`, `SectionCatalog.IsRenderModeToggleable`; `AppEditorPage` passa al `ReleasePanel` condiviso. Catch-all aggiunta nei guard dei 4 editor (prima un'eccezione non di dominio abbatteva il circuito col badge fermo su «Salvataggio»). **Lezione: confrontare i corpi, non le firme** — `IsMandatory`/`IsHidden`/`IsDerived` condividono il nome ma sono regole di dominio diverse per tipo, e restano separate.
- **Verifica live** (nuova skill `.claude/skills/verifica-live/`): trovato `rel. v@r.VersionNumber` **letterale** a schermo — in Razor una `@` fra due caratteri non-spazio è letta come indirizzo email e non apre un'espressione, senza alcun warning. Corretto in 4 punti con la forma `v@(...)`.
- **Aperto (non codice)**: la SID `BANA8A` di LIBD ha `InitialClimb = "90"` → resa «90 ft», implausibile (le altre BANAV hanno `9000` → «FL90»): errore di contenuto da correggere nell'editor.

## Stampa dei documenti + fix pubblicazione (30 lug 2026, seconda sessione)
Schede complete: `../feature/2026-07-30-stampa-documenti.md` e `../feature/2026-07-30-pill-stato-dopo-publish.md`.
Suite **631 → 640 verde**, build 0 warning, 14 commit.
- **La stampa era rotta da sempre, in silenzio.** Il blocco `@media print` in `vipi-theme.css` faceva
  `body *{visibility:hidden}` mostrando solo `.printable` — classe che **nessun markup applicava**: Ctrl+P dava un
  foglio bianco su qualunque pagina, e nessun test lo vedeva. Nuovo foglio dedicato **`vipi-print.css`**: nasconde il
  chrome e lascia il contenuto nel flusso (niente opt-in per pagina), A4 verticale, `thead` ripetuto, colori
  informativi preservati, zoom inline azzerato. Più `PrintMeta` (intestazione di sola stampa) e tasto **Stampa** nei
  quattro viewer documento. Nessun endpoint di export: la stampa del browser copre RNF-6 (piano §10, §22.7).
- **Due trappole browser, entrambe trovate solo guidando il flusso.** Un `<details>` chiuso **non si apre col solo
  CSS** (Chrome lo nasconde da user-agent con `content-visibility` su `::details-content`) → serve l'hook
  `beforeprint`; e **Chrome segnala la stampa due volte** (`beforeprint` + cambio media `print`), quindi gli handler
  di stampa vanno resi **idempotenti** o il ripristino post-stampa non avviene. Leaflet, allo stesso modo, tiene la
  propria dimensione in memoria: ridurre l'altezza da CSS **ritaglia** la mappa invece di riadattarla.
- **Spazio recuperato sull'A4**: scala tipografica da carta (il tema parte da 16px con `h2` 32px — misure da monitor,
  enormi su A4), mappe AoR rimpicciolite e **inquadrate con le proporzioni dell'area** (`fitBounds` sceglie lo zoom
  che fa stare i bounds in *entrambe* le dimensioni: in una cornice larga e bassa un AoR alto e stretto usciva
  minuscolo e non centrato), separazioni radar compatte. Documento aeroporto 3 → 2 pagine, vIPI ACC 36 → 28.
- **Dati live fuori dalla carta** per decisione: blocco METAR/TAF e vista Ridotta (piano §22.7). Regola: su carta
  sarebbero un'istantanea già scaduta.
- **Tabelle dei coordinamenti**: le colonne cambiavano larghezza da una tabella all'altra (misurate **19**
  combinazioni su LIBB) perché senza larghezze `table-layout:auto` dimensiona ognuna sul proprio contenuto. Fissate
  per colonna **semantica** (classi `c-*`, non per posizione: «Flusso» e «Condizione» sono opzionali e la stessa
  colonna cade in posti diversi). Poi «Livello» 13% → 21%, misurando a runtime quante celle andavano a capo.
- **«Bozza vN» dopo «Pubblica ora»**: la pubblicazione funzionava (release `Effective`, audit, documento promosso) —
  era la pill, letta all'apertura dell'editor. `ReleasePanel` ricaricava solo le proprie release senza avvisare
  l'host: nuovo `EventCallback Published`, agganciato dai tre editor. E «rel. v12» non era la versione del documento
  ma il **progressivo della release** → ora «rilascio #N». ⚠️ `string.Format(L["chiave"].Value, n)` **non
  interpola**: serve l'overload `L["chiave", n]`.
- **Chiave di release ACC**: `AccVipiReleaseTarget.ResolveDocumentIdAsync` **scartava la parte `root`** di
  `"{acc}|{root}"` e prendeva il primo CTR radice per `CoverageOrder`. Innocuo sui dati attuali (una sola radice con
  documento), ma su una ACC **multi-albero** «Pubblica ora» avrebbe promosso la bozza del documento sbagliato, in
  silenzio. Ora risolve per callsign, **senza fallback** quando il root non risolve. Test-first con i due criteri
  deliberatamente in conflitto, così un fallback su uno dei due sbaglierebbe comunque.
- **Refuso di render**: la legenda piste usciva «recommended**from** the METAR wind». Razor **scarta il testo di sola
  spaziatura che precede un blocco di codice** — anche dentro `<text>`: lo spazio va scritto come entità `&#32;`.
  Stessa famiglia della trappola `v@r.Proprietà`.

## 2026-07-30 — Uniformità dei tre documenti (vIPI ACC · vIPI APP · vLOA) — doc [refactor/11](../refactor/11-uniformita-tre-documenti.md)

Audit dei tre documenti su viewer pubblico / editor / anteprima bozza (app reale su copia del `vipi.db`,
browser guidato): il modello è unico e l'editor è condiviso, ma **ogni famiglia rileggeva quel modello a modo
suo**. Sei passi, suite 640 → **657 verde**, verifica live **20/20**.

- **Chiave di sezione univoca** (`custom:{guid8}`): la costante `"custom"` faceva collidere tutte le sezioni
  libere di un documento. Nella vIPI ACC dalla seconda in poi **non compariva**; nell'APP «Nascondi» su una
  le nascondeva **tutte**. Riconciliazione idempotente al boot (`IDocumentMaintenance`).
- **Fallback a pubblica con derivate frozen**: `_useFrozen` era impostato solo nel ramo `default:`, quindi un
  `?as=draft` non autorizzato o un `?as=rel:{id}` sbagliato serviva la pubblica **con dati live** — il
  congelamento AIRAC era bypassabile dall'URL (vLOA LIBB↔LDZO: 3 tabelle in pubblica, 9 con `?as=rel:` altrui).
- **Contenuto editoriale condiviso**: la vIPI ACC appiattiva le sezioni libere a sola prosa
  (`AppCustomSection`, rimossa) — tabelle perse, callout senza riquadro, sotto-sezioni mai rese. Ora tutte e
  tre passano da `SectionNode`/**`SectionBody`** (nuovo). Le sotto-sezioni delle sezioni derivate si rendono
  ovunque; `VipiViewService.Map` non scarta più le sezioni vuote.
- **`DocumentSection.IsHidden`** (migrazione `AddSectionIsHidden`), gemello di `RenderMode`: uno stato solo,
  versionato, dentro lo snapshot di release. Prima stava in tre storage diversi e due non erano versionati:
  un click su «Nascondi» toglieva la sezione dalla **pagina pubblica senza pubblicare nulla**. Nascondi/mostra
  è ora interno a `DocumentSectionsEditor`; i tre viewer marcano allo stesso modo.
  ⚠️ Trovato di sponda: `CreateDraftAsync` **non copiava `RenderMode`** — aprire una bozza riportava a `Frozen`
  ogni sezione `Live` di doc 10 (le SID d'aeroporto smettevano di aggiornarsi, in silenzio).
- **Superficie APP = non remotizzati**: l'editor apriva e creava documenti anche per un APP `Remotized`
  (`LIBD_CS0_APP`, doc 16) che nessun viewer sa rendere. `EnsureAsync` ora autorizza **prima** dell'uscita
  anticipata. L'elenco APP usa lo stesso gate della pagina (release effettiva, non `Document.Status`).
- **Superficie uniforme**: `ReleasePanel` anche nell'editor vLOA (era l'unico senza, e l'unico con un
  «Pubblica» di versione); la sezione padre «Coordination» è derivata anche in editing; **una sola rotta
  viewer per la vLOA** (`apps/vipi?vloa=` rimossa, era pure quella linkata dall'editor).
- **P7 (§3g, richiesta owner in verifica live)**: le sotto-sezioni si rendevano **sempre dopo** il corpo della
  sezione. In «Aree regolamentate» significava leggere prima le mappe e poi le premesse. Nuova colonna
  `DocumentSection.BeforeParentBody` (migrazione `AddSectionBeforeParentBody`) — terzo flag per-sezione con
  `RenderMode` e `IsHidden`: il corpo diventa una **posizione in una sequenza di tre slot** (*figlie «prima» →
  corpo → figlie «dopo»*), resa identica nei tre viewer e nell'editor condiviso (`SectionBody` accetta lo slot;
  gli host che producono il corpo da sé lo invocano due volte). Toggle «⤒ Prima del contenuto / ⤓ Dopo il
  contenuto» sull'intestazione della sotto-sezione. Default `false` ⇒ nessuna migrazione dati.
- **P8 (§3h, richiesta owner in verifica live)**: i **coordinamenti** nascevano aperti a ogni livello. Su LIBB
  significava 34 sottolivelli espansi sotto l'unico settore «ES», col resto del documento seppellito. Ora è
  espanso il solo primo livello (settore per vIPI ACC/vLOA, gruppo per l'APP) e dentro tutto è compresso;
  «Espandi tutto» e la stampa (`beforeprint`) restano invariati. `CoordinationCollapseTests` (bUnit) presidia
  gli `open` del markup, che nessun altro test guardava.
- **P9 (§3i, richiesta owner in verifica live)**: «Aree regolamentate» nasce **collassata** nel documento (65 aree
  con mappa su LIBB). Quali sezioni si aprono chiuse lo dice `SectionCatalog.IsInitiallyCollapsed` — il catalogo è
  già la fonte unica della natura delle sezioni, così la regola vale per le tre famiglie senza ripeterla. Nessuno
  stato persistito: è una proprietà del tipo di sezione, non una scelta editoriale. Vale **ovunque, viewer ed
  editor** (11 contesti verificati): un primo giro aveva escluso gli editor, ma la regola a metà rendeva quella
  sezione l'unica a comportarsi in modo diverso fra documento ed editing.
- **Fix a valle della verifica live (P6)**: rendere `coordination` derivata *a qualsiasi profondità* aveva un
  effetto collaterale nell'editor vLOA — la sezione **padre** «Coordination» finiva a rendere una direzione
  (sempre `ForeignToHome`, perché il titolo non inizia col codice Home), quindi l'albero del vicino compariva
  **fuori** dalle sotto-sezioni *e* dentro «LDZO → LIBB». Ora il padre non ha corpo proprio: le direzioni sono
  le sue sotto-sezioni. Nel viewer la sequenza è opposta (il padre rende entrambe, le figlie no) ed era corretta.

## 2026-07-30 — Migrazione da .NET 8 a .NET 10 (terza sessione)

Bump di framework, non di comportamento: 13 progetti da `net8.0` a `net10.0`, pacchetti `8.0.*` → `10.0.*`
(EF Core 10, Npgsql 10.0.3, OIDC 10, `Components.Web` 10.0.10). Suite **663 verde**, identica alla baseline
net8 misurata prima di partire. Verifica live su copia del `vipi.db` reale.

- **Gate bUnit prima di tutto.** bUnit aggancia gli internals del Renderer Blazor: era il punto dove la
  migrazione poteva morire. Primo commit = **solo il TFM**, pacchetti ancora 8.0.\*, per isolare la domanda
  «il runtime 10 regge?» dalla domanda «i pacchetti 10 reggono?». I 92 test UI sono passati subito; bUnit poi
  è salito a **1.40.0** e resta su 1.x — la 2.x è una riscrittura di API, fuori dallo scopo di un bump.
- **Test infrastructure ferma al 2023**: xunit 2.5.3 → 2.9.3, `runner.visualstudio` 2.5.3 → 3.1.5,
  `Test.Sdk` 17.8.0 → 18.8.1, coverlet 6.0.0 → 10.0.1. bunit 1.40 toglie anche la dipendenza da
  `Microsoft.Extensions.Caching.Memory` 9.0.0-preview, che era segnalata NU1903.
- **Due warning nuovi, entrambi chiusi**: `ASPDEPR005` (`ForwardedHeadersOptions.KnownNetworks` deprecato in
  ASP.NET Core 10 → `KnownIPNetworks`) e `xUnit2031` (`Assert.Single(x.Where(p))` → `Assert.Single(x, p)`,
  5 occorrenze nei test Infrastructure).
- **`global.json` nuovo** (`10.0.100` + `rollForward: latestFeature`): finora non c'era e locale e CI
  sceglievano l'SDK per conto loro. Docker passa a `sdk:10.0`/`aspnet:10.0`; il restore resta sul csproj di
  Host, ora per non tirare i pacchetti dei test e non più perché `sdk:8.0` non leggeva `.slnx`.
- **Verifica live (copia del DB reale)**: le **due migrazioni pendenti si applicano sotto EF Core 10**
  (`ProductVersion` 10.0.10) e `/vsop/health` non segnala schema drift; viewer LIBD completo (transition level
  con `≤`/`–`/`≥`, 20 SID, remarks), editor con timeline a 3 release e **round-trip del circuito verificato**
  (click su «Differences» → il DOM cambia), elenco documenti e drafts a posto. `health` risponde `Degraded` per
  la cache ATC ferma: previsto senza credenziali IVAO, non una regressione.

## 2026-07-30 — Asset statici su MapStaticAssets (quarta sessione)

Sostituito il cache-busting fatto a mano (`?v=<mtime>` in `App.razor`) con `MapStaticAssets` di ASP.NET
Core 10. Motivo tecnico, non estetico: **il vecchio token era un massimo globale**. `ComputeAssetVersion`
prendeva l'mtime più recente fra 12 file e lo appiccicava a tutti, quindi una riga cambiata in
`vipi-theme.css` faceva riscaricare al browser tutti i CSS e tutti i JS.

- **Un buco che il fingerprint per contenuto non può avere.** `vendor/three.min.js` riceveva il `?v=` ma
  **non era nella lista `VersionedAssets`**: aggiornandolo da solo, il token non cambiava e il browser
  continuava a servire la versione vecchia. 592 KB, l'asset più pesante del progetto.
- **Cosa dà in cambio** (misurato sul pubblicato, non dedotto): `vipi-theme.css` 97 KB →
  **18,5 KB in brotli precompilato a build-time**, `Cache-Control: max-age=31536000, immutable` + ETag.
  Prima erano 7 giorni e la compressione la faceva `UseResponseCompression` a ogni richiesta — su Render
  free la CPU è la risorsa scarsa. Nel publish compaiono 15 `.br` e 15 `.gz`.
- **Regressione trovata e chiusa.** I `.woff2` sono referenziati da **dentro** `vipi-fonts.css`, quindi non
  passano da `@Assets` e `MapStaticAssets` li serviva col profilo non-impronta: `max-age=3600,
  must-revalidate`, **più corto** dei 7 giorni di prima. Header riportato a 7 giorni.
  Primo tentativo sbagliato e istruttivo: uno `UseStaticFiles` che conoscesse il solo `.woff2` **non serve
  nulla**, perché `StaticFileMiddleware` si tira indietro quando il routing ha già selezionato un endpoint —
  e ora il font *è* un endpoint. Funziona invece riscrivere l'header in `Response.OnStarting`.
- **Verifica**: 663 test verdi (non provano niente sugli asset), poi header controllati sul pubblicato in
  Production e browser guidato sulle 7 rotte — zero 404 sugli asset, zero errori console, round-trip del
  circuito ancora vivo, pagina renderizzata con font e stili al loro posto.

**Non toccato, ma emerso**: `three.min.js` (592 KB) è caricato in `App.razor` su **ogni** pagina, anche
quelle senza mappa 3D. Vale più di tutto il fingerprinting messo insieme ed è un lavoro diverso
(caricamento condizionale sulle rotte AoR).

## 2026-07-30 — Rifiniture post-migrazione (quinta sessione)

Tre interventi piccoli e indipendenti, emersi leggendo la CI e i due punti lasciati aperti dalla sessione
sugli asset. Ognuno sul suo branch, nessuno tocca il comportamento dei documenti.

- **Smoke E2E: VipiAuth non si spegneva davvero.** I 4 smoke fallivano in CI con «VipiAuth:Enabled=true ma
  ClientId mancante». La factory *provava* già a spegnere l'auth, ma con `ConfigureAppConfiguration`:
  `Program.cs` chiama `AddVipiStandaloneAuth` alla **registrazione**, prima di `builder.Build()`, mentre quei
  callback si applicano solo alla costruzione dell'host — la sorgente in-memory arrivava troppo tardi. Ora è
  una **variabile d'ambiente**, che sta già nella configurazione di default del builder e vince su
  `appsettings.Development.json`. **Non era una regressione di .NET 10**: verificato che lo stesso repro
  fallisce identico sul commit pre-migrazione. Restava verde in locale perché i user-secrets forniscono un
  ClientId vero e la guardia non scatta: il repro fedele è `VipiAuth__ClientId="" dotnet test tests/Vipi.E2E.Tests`.
- **Health check sdoppiato.** `/vsop/health` chiama anche il report di consistenza, che fa **7 scansioni
  complete** materializzate in memoria. Va bene per una pagina che apre un umano, non per una sonda che
  l'orchestratore ripete di continuo: su Neon (Postgres serverless) lo terrebbe sveglio a bruciare compute.
  Ora due endpoint distinti per tag: `/vsop/health/ready` (CanConnect + migrazioni, due query) e `/vsop/health`
  (la sonda **+** consistenza e freschezza ATC). `VipiHealthCheck` **riusa** `VipiReadinessCheck` invece di
  ripetere i controlli critici: una sola definizione di «critico». `render.yaml` e lo smoke del container in CI
  passano su `ready` — il beneficio vero non è il riavvio ma il **gate sul deploy**, perché `/vsop` rispondeva
  200 anche con lo schema disallineato.
  - Prima ancora: su Postgres il probe delle migrazioni dava **sempre** Unhealthy, perché lì lo schema lo fa
    `PostgresSchemaReconciler` (EnsureCreated) e `__EFMigrationsHistory` resta vuota. Nessuno l'aveva notato
    perché `healthCheckPath` puntava a `/vsop`, non a `/vsop/health`.
- **three.js caricato su richiesta.** 589 KB (118 in brotli) partivano da `App.razor` su **ogni** pagina, per
  servire il solo tab «Vista 3D». Ora l'URL — con l'impronta di `MapStaticAssets`, che non si perde — passa
  come `data-three-src` sul tag di `vipi-aor3d.js`, e `loadThree()` lo carica alla prima costruzione di uno
  stage. Il codice tollerava già l'assenza di `THREE`, quindi non è stato riscritto: è cambiato *quando*
  three.js arriva. `initOne` ha un terzo stato `pending`, altrimenti un secondo evento durante il caricamento
  costruirebbe due volte lo stesso stage.
  - Chiuso nello stesso punto un buco vecchio: l'intestazione del file prometteva un fallback «se manca
    WebGL/THREE» ma il codice copriva solo THREE — senza WebGL `new THREE.WebGLRenderer` lanciava e lo stage
    restava vuoto.
- **CI: `checkout` e `setup-dotnet` alla v5** (node24). Le v4 girano su Node 20, deprecato sui runner: già
  forzate su Node 24 e segnalate a ogni run. Verificato che le v5 dichiarino `using: node24` nel loro
  `action.yml`, così l'avviso sparisce invece di essere solo rimandato.

**Nota di metodo.** La prima verifica del lazy-load di three.js era **verde per il motivo sbagliato**: «three
non richiesto» su tutte le rotte provate, ma perché nessuna conteneva uno stage 3D — le rotte erano sbagliate
(`/vsop/{acc}/vipi`, non `/vsop/{acc}`). Un risultato negativo va sempre letto insieme alla prova che il caso
positivo sarebbe stato osservabile: qui, che gli stage ci fossero (2) e i bottoni pure.

## 2026-07-31 — Il drift di schema Postgres non correggibile diventa visibile

`PostgresSchemaReconciler` è additivo per scelta: aggiunge colonne e indici mancanti, non tocca il resto. Il buco
non era «mancano le migrazioni incrementali» — era che **il caso peggiore è silenzioso**. Rinominando una colonna
nel modello, il reconciler crea la nuova (vuota) e lascia la vecchia coi dati dentro: l'app non lancia niente e
mostra un campo vuoto. Un errore in faccia sarebbe stato meglio di un dato sbagliato che passa inosservato.

- **Diff nel verso opposto.** `ISchemaDriftProbe` (Application) / `PostgresSchemaDriftProbe` (Infrastructure)
  confronta `information_schema` col modello EF e segnala: colonna orfana nello schema (Warning, «i dati sono
  ancora QUI»), tipo divergente (Warning), colonna attesa e assente (Error — il reconcile è best-effort e può
  aver fallito in silenzio).
- **Nessun canale nuovo.** I finding entrano nel report di consistenza già esistente, quindi compaiono in
  `/vsop/admin/diagnostica` e mandano `/vsop/health` a Degraded **senza toccare né la pagina né l'health check**.
  L'aggancio sta in `ConsistencyReportService.RunAsync` — non in `Analyze`, che resta una funzione pura sul
  dataset di dominio: il drift è incongruenza di *schema*, non di *dati*.
- **Non corregge, di proposito.** Guardando solo modello e schema una rinomina è indistinguibile da «togli la
  vecchia, aggiungi la nuova»: automatizzarla vorrebbe dire autorizzare un `DROP COLUMN` deciso da un'euristica
  sul DB di produzione.
- **Il falso allarme era il rischio della feature**, non il bug che cerca: una diagnostica rumorosa non la legge
  più nessuno. Due difese. La normalizzazione dei tipi copre gli alias (`varchar` ↔ `character varying`,
  `timestamptz` ↔ `timestamp with time zone`, precisione ignorata). E un test costruisce il modello **con
  provider Npgsql** (senza connettersi: il model building non ha bisogno di un DB) ed elenca i tipi store
  realmente usati — oggi otto: `bigint, boolean, bytea, character varying, double precision, integer, text,
  timestamp with time zone`. Sono già i nomi che usa `information_schema`, quindi la mappa alias è
  un'assicurazione, non un'ipotesi su cui poggia tutto. Se qualcuno introduce un tipo esotico il test fallisce
  **prima** che la diagnostica si riempia di falsi positivi.
- Suite 670 → **686**. Verifica live su SQLite: `health` Degraded come prima, `ready` Healthy, diagnostica 200 e
  nessuna riga di drift — il probe è davvero no-op fuori da Npgsql.

**Quello che resta da fare a mano**, ed è scritto in ADR-0007 §D1-bis: quando il probe segnala, la DDL si esegue
a mano su Neon. Accettabile finché i casi sono rari (finora zero). Il passo successivo, se diventassero
ricorrenti, sono script `.sql` versionati eseguiti all'avvio — non le migrazioni EF per-provider, il cui punto
duro non è il lavoro corrente ma il baseline da riprodurre esattamente sullo schema che c'è già.

## 2026-07-31 — La vista operativa diventa `/vsop/{acc}/live` e smette di dipendere dal documento

Revisione della pagina che un controllore tiene aperta **mentre controlla**. Tre richieste esplicite
(path in inglese, sezioni collassate, tabella SID illeggibile) più un giro di QoL scelto insieme.

- **Rotta**: `/vsop/{acc}/operativa` → **`/vsop/{acc}/live`**, `/operativa-app` → `/live-app`
  (`AccLivePage`/`AppLivePage`, chiavi resx `Live_*`/`AppLive_*`). Redirect **301** con query preservata in
  `Program.cs`: sono pagine che finiscono nei preferiti. Le due rotte **non erano nella mappa pagine**: aggiunte.
  L'etichetta a schermo resta «Operativa» — cambia l'URL, non la lingua della UI.
- **Il documento non è più un prerequisito.** La pagina è legata all'**ente** che apri: trasferimenti e AoR non
  toccano il documento e le frequenze escono dai cataloghi (il blocco porta solo raggruppamento e ordine). Eppure
  senza vIPI pubblicata rispondeva «vista operativa non disponibile» e nascondeva anche le informazioni di
  handoff che nel DB c'erano. Ora è un banner: si rende tutto, e le frequenze usano **blocchi sintetici**
  (Aerovia a membri vuoti = tutti i CTR, più un gruppo con tutti gli APP) passati alla derivazione normale —
  nessun secondo percorso di calcolo. Stessa cosa per un APP remotizzato non ancora messo in un gruppo.
  Verificato su LIRR (nessuna vIPI): 86 frequenze e i trasferimenti resi, prima non si vedeva niente.
- **Sezioni collassate + memoria.** Frequenze e Trasferimenti partivano `open`: la vista si apriva su due muri di
  tabelle. Ora partono chiuse e `data-persist` ricorda la scelta. *Gotcha*: `wireCollapse` gira solo a
  load/enhancedload, ma la pagina è `InteractiveServer` e ricostruisce i `<details>` a ogni tick → esposto
  `window.vipiWireCollapse` (solo la persistenza; `vipiWireUi` rifarebbe `wireHashLanding`, che riscorre la pagina)
  e richiamato a ogni `OnAfterRenderAsync`. Il tag «aperto» in intestazione mentiva su una sezione chiusa:
  sostituito dal conteggio.
- **Tabella SID schiacciata**: `AirportQuickPanel` usava `sid-table cfg-table`, ma `.cfg-table` è la tabella
  *Configurazioni operative* — `table-layout:fixed` con larghezze cablate su **quattro** colonne (26/38/18/18).
  La tabella SID ne ha sei: **Cat. e WTC finivano a larghezza ~0**. Ora `res-table sid-table sid-quick` dentro un
  `.tbl-scroll`, larghezze **per classe semantica** (le colonne opzionali si nascondono: `nth-child` sarebbe
  sbagliato) e Transition/Cat./WTC omesse se vuote su tutte le righe mostrate.
- **QoL**: la postazione **segue la connessione IVAO** (prima `_myCallsign` si calcolava una volta sola: chi
  apriva la pagina e si connetteva dopo non veniva mai agganciato), con override manuale in `?p=CALLSIGN`;
  **striscia dei cambi** online/offline + evidenza sulle righe toccate (prima la pagina si riscriveva in
  silenzio a ogni tick SSE); testata **sticky**; orari in **Z** + orologio UTC lato browser; **modalità compatta**
  persistita fuori dal circuito (classe su `<html>`, come lo zoom); **rilasci a UNICOM nascosti di default** con
  tasto per mostrarli; **vento** accanto alla pista suggerita nella vista rapida.

Suite **686** verde. Verifica live su copia del DB (LIBB con documento, LIRR senza, gemella APP): redirect 301,
sezioni chiuse e ricordate dopo reload, tabella SID a colonne reali senza sfondare la pagina, compatta persistita,
`?p=` scritto dal selettore. **Non esercitata live** la striscia dei cambi: serve una transizione online/offline
fra due tick e il feed IVAO era a zero ATC.

> **Trappola nuova per la verifica**: `innerText` su un `<details>` **chiuso** torna stringa vuota (è
> layout-dependent). Un'asserzione che legge il testo di una sezione collassata sembra dire «elemento assente»
> mentre l'elemento c'è: interrogare il DOM (`querySelector`, conteggi) o aprire prima la sezione.

## 2026-07-31 — Vista live unificata per callsign — doc [refactor/12](../refactor/12-vista-live-unificata.md)

Secondo giro della stessa giornata: chiuso l'ultimo doppione strutturale dell'asse refactor.

- **Una pagina sola, keyed sul callsign**: `/vsop/live` (la tua postazione) e `/vsop/live/{callsign}`
  (consultazione). Sparisce `{acc}` dal path — era derivabile dal callsign, e tenerlo significava due fonti
  per la stessa informazione libere di contraddirsi.
- **Descrittore + registry** (`ILiveStationKind`), stessa tecnica di `IReleaseTarget`/`IDocKindRoutes` (doc 09):
  `AreaLiveStation` · `ApproachLiveStation` · `AirportLiveStation`. **Le torri, i ground e i delivery hanno una
  vista live** che prima non esisteva. Un test verifica che ogni `SectorType` abbia esattamente un descrittore.
- **Selettore postazione rimosso** (richiesta esplicita): la pagina dipende dalla postazione che hai aperto.
  Non connesso ⇒ stato d'attesa con gli ATC online cliccabili e **aggancio automatico** al tick SSE, senza
  reload; postazione altrui ⇒ banner esplicito.
- **Trasferimenti**: «i miei più quelli dei figli **chiusi**» = `ResolvedOwnerCallsign == postazione`, non
  `DomainOf` (un figlio online se li tiene). La vecchia pagina ACC mostrava i flussi di *tutta* l'ACC: per un
  sotto-settore l'elenco ora si stringe a ciò che è davvero suo.
- **Codice morto**: via `AccLivePage`, `AppLivePage` e le due `Ridotta*` spente dal Round 12 (mai riattivate;
  `RidottaAppPage` era per metà un mockup hardcoded), più 16 chiavi resx orfane.

Suite 686 → **702**. Verifica live su 12 postazioni.

> **Due trappole pagate.** (1) `/vsop/live/{callsign}` ricade sul prefisso dello stream SSE `/vsop/live/atc`:
> vince il segmento letterale, ma è una proprietà del routing che si rompe cambiando le rotte → uno smoke la
> verifica. (2) `DeriveFrequenciesForMembersAsync` espandeva **già** il catalogo d'aeroporto per qualsiasi membro,
> non solo per gli APP come diceva il commento: è ciò che ha reso i tipi nuovi quasi gratuiti.

**Follow-up di dato, non di codice:** nessun settore `Twr`/`Gnd`/`Del` ha un padre nella gerarchia (solo `App` e
`Ctr`), quindi proprio le postazioni per cui la catena di copertura è l'informazione principale non ne hanno una.
La pagina lo dichiara invece di lasciare un vuoto muto; l'aggancio va fatto in `/vsop/admin/sectorstructure`.

### Coda della stessa sessione — il padre dell'aeroporto non arrivava alle sue posizioni

Segnalazione dell'owner («nelle gerarchie gli aeroporti hanno dei padri, e tutte le postazioni di quell'aeroporto
riferiscono a quel padre»): la catena vuota di TWR/GND/DEL non era un dato mancante ma **un legame che nessuno
leggeva**. `Airport.ParentCallsign` (29 popolati, compilato dall'admin sul nodo Aeroporto in Struttura) non veniva
mai letto dalla proiezione, che guardava solo `AirportSector.ParentCallsign` — popolato per i soli APP.

Fix in `EfSectorProjectionService` (fonte unica, quindi vale per **tutti** i consumatori): scaletta interna
**DEL → GND → TWR → APP** e uscita sul padre dell'aeroporto; il `ParentCallsign` esplicito vince. Riproiezione
all'avvio (`ProjectVipiSectors`), altrimenti la nuova regola sarebbe entrata in vigore solo al prossimo import.
Sul DB reale: `Del` 0→5/5, `Gnd` 0→20/20, `Twr` 0→51/84. Suite **702 → 708**.

Toccava anche la **risoluzione dei trasferimenti** (stessa gerarchia): un punto verso una torre offline terminava
su UNICOM invece di salire all'avvicinamento. Latente (0 punti simili nel DB), ma stessa classe di errore.

**Scelta fra pari grado, coi dati e non a sorte** (secondo giro, su indicazione dell'owner: «se ha più APP si
deve vedere in sectorstructure qual è la gerarchia di questi APP»): la gerarchia fra le APP di un aeroporto **è
già configurata e visibile** in quella pagina, quindi la torre si aggancia alla **radice del sottoalbero APP**
(LIRF: `LIRF_TW1_APP`, non l'alfabetico `LIRF_AEM_APP`). Dove una gerarchia scritta non c'è — torri e ground non
sono nodi editabili — vale il **callsign senza infisso** (`LIRF_TWR` batte `LIRF_E_TWR`); se resta ambiguo si
**sale** di un gradino (a Malpensa i due ground sono entrambi sdoppiati e il delivery va alla torre).

**Torri, ground e delivery diventano nodi editabili** in `/vsop/admin/sectorstructure` (§8 del doc 12): erano
esclusi da un filtro `Position == "APP"`, non da una scelta di modello — sono già la stessa entità degli APP.
La scaletta diventa un servizio di dominio condiviso fra proiezione ed editor, e i nodi senza padre scritto
mostrano quello **ereditato** invece di un «da assegnare» che contraddirebbe la vista live. Guardia nuova: nessun
padre più in basso nella scaletta (un ground non copre una torre) — pari grado ammesso, che è il caso degli split.
Interruttore «Posizioni d'aeroporto» spento di default (+186 righe nell'albero). Suite **715**.

**Resta aperto, di dato:** 33 torri di aeroporti senza APP e senza padre configurato in Struttura.


### Coda: avvicinamento reso come l'area + trasferimenti verso figli chiusi (2026-07-31)

Due correzioni chieste dall'owner sulla vista live.

- **L'APP ora ha i chip degli aeroporti** come i tipi d'area: un avvicinamento ne copre spesso più d'uno
  (`LIBD_CS0_APP` tiene LIBD e LIBR) e il pannello fisso rendeva gli altri irraggiungibili. La funzione dei chip
  si sposta in `LiveStationParts`: una regola per due descrittori. Torri/ground/delivery tengono il pannello
  fisso — sono di un aeroporto solo.
- **I punti verso un proprio discendente CHIUSO non si mostrano più.** Se il figlio è chiuso lo sto coprendo io:
  non c'è niente da passare. Prima il punto restava con il destinatario risolto risalendo la gerarchia, che per
  un figlio chiuso è la postazione stessa — «passa a te stesso». Vale solo per i discendenti: verso un ente
  esterno la risalita resta informazione utile. Caso reale: `LIBB_ES_CTR` → `LIBD_CS0_APP`, 4 punti.

Suite **715 → 718**, verifica live su entrambi.


### Immagini nei blocchi editoriali (2026-07-31)

Ovunque si potesse aggiungere paragrafo, callout o tabella si può ora aggiungere un'**immagine**: scelta dal
dispositivo o trascinata sull'area. Carta: `docs/feature/2026-07-31-immagini-nei-blocchi.md`.

- `BlockFormat.Image` **esisteva già** dal principio e il viewer ne mostrava un segnaposto: la feature l'ha
  implementato, non aggiunto un tipo. Il riferimento sta nel `BodyJson` (`MediaRef`, fonte unica del formato),
  la didascalia nel `Body`; i byte in `MediaAssets`, content-addressed per sha256 e dietro la porta `IMediaStore`.
- Le righe media **non si cancellano mai** dall'editing: uno snapshot di release pubblicato cita lo sha.
- `Media:MaxUploadBytes` (3 MB) è il limite chiesto: un solo numero, letto da UI, stream, controllo e messaggio.
- Il reconciler Postgres ora crea anche le **tabelle** mancanti: `EnsureCreated` non tocca un DB che ha già
  tabelle, quindi `MediaAssets` non sarebbe mai nata su Neon. Vale per ogni entità futura.
- Verifica live: quattro difetti invisibili ai test (ridimensionamento mai eseguito, campi che si sovrascrivevano,
  due messaggi per lo stesso rifiuto, immagini vuote in stampa). Dettaglio nella carta.

Suite **774** verde.


### Pulizia delle immagini non più usate (2026-07-31)

Seguito immediato della feature immagini: togliere un blocco non libera lo spazio (di proposito — una release
pubblicata cita lo sha), quindi serviva un modo esplicito per recuperarlo.
Carta: `docs/feature/2026-07-31-pulizia-immagini-orfane.md`.

- Card in `/vsop/admin/diagnostica`, **due tempi**: «Analizza» mostra l'elenco e lo spazio recuperabile, solo
  allora compare «Elimina definitivamente». Mai automatica: un lavoro notturno farebbe il danno mentre nessuno
  guarda.
- «Non usata» = lo sha non compare in **quattro** posti: blocchi di ogni versione (bozze comprese), sezioni extra
  d'aeroporto, payload delle release, blocchi condivisi. L'ultimo (`SharedBlock`) è emerso rileggendo il modello a
  feature finita: nessuno li crea oggi, ma portano `Format`+`BodyJson` come i blocchi normali.
- Due difetti presi dai test prima che dal vivo: gli **escape JSON** nascondevano il riferimento dentro le sezioni
  extra (due cifre esadecimali incollate allo sha); e serviva l'anello esplicito fra **snapshot di release** e
  scanner, senza il quale la pulizia avrebbe cancellato le foto delle vIPI già pubblicate.
- `DeleteOrphansAsync` **ricontrolla** al momento della cancellazione: fra l'elenco e il clic passano minuti.

Suite **801 → 804** verde, verifica live con traccia (67,5 KB recuperati, l'immagine rimasta ancora servita).


### Immagini: anteprima, quota per documento, pulizia alla cancellazione (2026-07-31)

Estensione chiesta subito dopo la pulizia manuale: i suoi tre non-obiettivi diventano funzioni.

- **Anteprima** nell'elenco di pulizia: davanti a un nome come «immagine1.png» nessuno sa se quella foto serviva.
- **Quota per documento** (`Media:MaxBytesPerDocument`, 25 MB, 0 = illimitata), controllata prima di salvare per non
  lasciare nel deposito un asset che nessuno cita. Conta le righe: la stessa foto in due blocchi pesa una volta.
- **Pulizia alla cancellazione** su tutti e quattro i percorsi in cui una foto perde il suo blocco (blocco, sezione
  col sottoalbero, riscrittura degli extra d'aeroporto, potatura delle versioni archiviate). Non decide da sé:
  ripassa da `DeleteOrphansAsync`, quindi una foto citata altrove — o da una release pubblicata — resta dov'è.

I repository hanno un parametro in più: i call site dei test passano l'implementazione vera, così la suite
esistente esercita il percorso nuovo invece di aggirarlo.

Suite **804 → 814** verde, verifica live sui tre comportamenti.


### Aree regolamentate anche sull'APP non remotizzato (2026-08-02)

La sezione «Aree regolamentate» esisteva nel catalogo dell'APP ma era una sezione libera: si scriveva a mano ciò
che sulla vIPI ACC è un **picker** di aree importate da IVAO. Ora è la stessa sezione strutturata dell'ACC —
**senza aree di default**: sull'ACC il blocco Aerovia parte in automatico con tutte le aree del proprio ACC,
mentre un singolo avvicinamento ne tocca due o tre, quindi qui si scelgono a mano (proprie + extra di altri ACC).

- Picker e corpo del viewer erano scritti dentro `AccEditorPage`/`AccSectionBody`: estratti nei componenti
  condivisi **`RegulatedAreasEditor`** (parametri `AllowAuto`/`ShowExtra`) e **`RegulatedAreas`**. Il secondo uso
  li avrebbe duplicati (regola del 2 del runbook feature).
- Le tre query EF sulle aree speciali stavano in `IAccDerivationRepository`: spostate in **`ISpecialAreaRepository`**
  (`EfSpecialAreaRepository`), perché il dato è per-ACC ma il consumatore non è più solo l'ACC. Proiezione id →
  vista con shape condivisa in `SpecialAreaProjection`.
- Sull'APP `OwnAuto` è **sempre falso**, normalizzato in scrittura e in lettura: un JSON che lo portasse (copiato
  da un blocco ACC) farebbe comparire decine di aree mai scelte.
- Il viewer legge gli id dalla **versione che sta mostrando** (pubblica/bozza/release) e risolve dettagli e shape
  sui cataloghi correnti, come la vIPI ACC.

Suite **816 → 819** verde; verifica live su `LIPE_W_APP` (scelta di un'area LIPP + una extra LIRR, ricomparsa dopo
reload, rese nel viewer bozza con mappa, banda quota e note).

---

## 2026-08-03 — Bridge Aurora: dal tag di Aurora alla vIPI e ritorno — piano [design/piano-aurora-bridge.md](../design/piano-aurora-bridge.md)

Nuovo prodotto accanto al portale: un tool desktop che, selezionato un aeromobile in Aurora, legge la vIPI e
propone **a che livello va ceduto al prossimo ente**, scrivendolo nell'etichetta quota del tag su richiesta
dell'utente. Cinque fasi in una sessione (F0 sonda protocollo → F1 matching+API → F2 client → F3 UI → F4
rifinitura), branch `feature/aurora-bridge`, **suite 819 → 930 verde**.

- **F0 ha riscritto le premesse.** La sonda contro Aurora reale ha smentito la wiki IVAO in cinque punti; il più
  pesante: l'**XFL non è scrivibile** (`#LBXFL`/`#XFL`/`#TRXFL`/`#SETXFL` → `Unknown command`, mentre `#LBALT`
  nudo → `Incomplete data`: la sonda discrimina). Si scrive l'**etichetta quota**, e **solo su traffico assunto**
  — vincolo non documentato. In compenso `#LBALT` accetta **testo libero**, quindi la convenzione del tag è una
  scelta, non un limite. Regalo inatteso: `#TRPATHL` dà la rotta **già risolta da Aurora** con gli ETO, molto
  meglio del parsing della rotta del piano di volo.
- **Il matching è puro e motivato.** `TransferMatcher` non scarta mai in silenzio: ciò che non torna abbassa il
  punteggio e lascia una ragione leggibile («CoP ASPIR in rotta (ETO 0925)», «riga per livelli pari, il volo è a
  FL350»). Il punteggio si **normalizza** invece di essere troncato a 1, altrimenti due candidati forti finivano
  appaiati proprio dove serviva distinguerli. La copertura top-down non ha richiesto nuove API sulla topologia:
  un flusso è mio se `FirstOnline([proprietario, …antenati], online + me) == me`.
- **Un modello solo.** `Vipi.AuroraBridge.Contracts` è referenziato sia dall'host sia dal tool: niente DTO
  gemelli ricopiati nell'endpoint (FEATURE-PROCESS §1). Il limitatore per IP è scritto in casa perché il modulo
  gira anche **embedded** in Ivao.It e non deve toccare la pipeline dell'host.
- **Il tool non scrive mai da solo.** `RefreshAsync` non manda mai `#LBALT`; esiste solo `WriteAsync`, che
  rifiuta prima ancora di parlare con Aurora se il traffico non è assunto o il livello non è scrivibile. Anche
  la scorciatoia globale **non ripiega** su un altro candidato: se il primo non è scrivibile si ferma e spiega.
- **Cosa hanno trovato le verifiche live, e i test no:** che l'assunzione va confrontata col callsign
  **connesso** e non con l'override delle regole (il tool rifiutava scritture legittime), e che la finestra
  moriva all'avvio perché `InitializeComponent` riscritto a mano non popola i campi `x:Name`. Nessun test
  poteva vederle: la prima richiede Aurora vera, la seconda una finestra vera.
- **Scoperta sui dati, non sul codice:** 30 punti di sorvolo su 33 in LIBB hanno il vincolo ma **non il
  livello**. Deciso: è una lacuna redazionale, il tool si limita a non scrivere nulla e a dirlo.

Superficie nuova: `POST /vsop/api/v1/transfers/resolve` (anonimo, read-only, tetto per IP) ·
[guida utente](../guide/aurora-bridge.md) · [contratto API](../reference/api-aurora-bridge.md).

## Aree regolamentate — interruttore, import incrementale, dangling (3 ago 2026, 951 test)
Tre punti aperti dall'analisi del percorso «aree speciali», chiusi insieme
([carta](../feature/2026-08-03-aree-regolamentate-hardening.md), SPEC §9.21-9.22).
- **Categoria di import `SpecialAreas`** (`ImportPolicy.ImportSpecialAreas`, default true): erano l'unico dato di
  sorgente senza interruttore, e il prune per-ACC cancellava le righe buone senza che l'admin potesse fermarlo. Gate
  in `SpecialAreaImportUseCase` (corpo condiviso auto/manual), **prima della fetch e del prune** → esclusa =
  congelata. Riga in `/vsop/admin/sorgenti`.
- **Trappola del default trovata strada facendo**: `migrations add` genera `defaultValue: false` per un bool nuovo, e
  `PostgresSchemaReconciler` backfillava a `false` ogni colonna NOT NULL — per un flag opt-out significa spegnere la
  categoria in silenzio sul DB già popolato. Ora il default sta nel modello (`HasDefaultValue`) e il reconciler lo
  legge (`BackfillLiteral`). ⚠ **`ImportSids` ne era già vittima** (8 lug): da controllare in produzione, non è
  ribaltabile da codice perché `false` è indistinguibile da una scelta dell'admin.
- **Import incrementale della shape**: il dettaglio `/v2/specialAreas/{id}` si salta per le aree con shape già in
  archivio e recente (30 gg) — `skipDetailIds` dal DB via use-case, il client resta senza persistenza. Da N+1 per
  ACC a una chiamata per pagina.
- **Riferimenti dangling**: la selezione di aree di un documento cita gli `IvaoId` senza FK; il prune poteva
  cancellarne una e il viewer la saltava in silenzio. Nuovo rilievo «Area regolamentata dangling» in diagnostica
  (sola versione di lavoro) + «⚠ non più disponibile» nell'editor. Nessun guard nel prune: si rileva, non si vincola.
- Estratto `RegulatedSelectionJson`, unico lettore del `BodyJson` `regulated` (l'APP non leggeva l'array legacy).

**Seguito in giornata, partendo da «non trovo le aree di altri ACC» su una vIPI di APP:**
- **Il picker nascondeva ciò che aveva**: candidati solo digitando e taglio muto a 12 su ~800 aree. Ora tendina per
  ACC col conteggio, elenco anche senza cercare, contatore «Mostrate 20 di 99», lista scorrevole.
- **Un'area può appartenere a più ACC.** Caso rivelatore: `8870` «LI R49 Zita», su IVAO in LIRR *e* nel militare
  LIZZ; da noi risultava solo di LIZZ perché `IvaoId` è unico e `CenterId` era una colonna sola — ogni ACC che la
  elencava riscriveva l'appartenenza e **vinceva l'ultimo in ordine alfabetico**. Le 15 aree di LIZZ erano tutte
  così (R21 Sara, STAR1-10, Donald, Eolia, Sardinia). Nuova entità di legame `SpecialAreaCenter`, `CenterId`
  rimossa da `SpecialArea`, import additivo, prune per legame, area cancellata solo quando resta senza enti
  (SPEC §9.23).
- **Backfill doppio**: migration per SQLite + `ISpecialAreaMaintenance` al boot per Postgres (dove lo schema lo
  allinea il reconciler, che le migration non le esegue) — lì droppa anche la colonna storica, NOT NULL e fuori
  dal modello, che bloccherebbe gli inserimenti. Recupera una sola appartenenza per area: le altre le riporta il
  primo import, quindi dopo il deploy conviene premere «Importa da sorgente».
- Migration provata su copia del `vipi.db` reale: 993 aree → 993 legami, nessuna orfana.
- **Aree estere solo su richiesta** (`Acc.SpecialAreasEnabled`, default true): erano **763 legami su 993**, scaricati
  ogni 24h per gli ACC esteri materializzati dalle vLOA e usati quasi da nessuno. Il giro periodico tocca solo gli
  abilitati; «Importa aree» nella riga di `/vsop/admin/accs` fa il primo scarico e accende l'ente (solo se la fetch
  produce qualcosa), «Escludi aree» pota. Riconciliazione one-shot al boot per gli esteri già in archivio, con
  segnaposto in `ImportState` perché non si ripeta su un ente riabilitato a mano. Sul DB reale: **993 aree → 230**,
  le italiane invariate.

Chiusura: 9 commit sul branch `feature/aree-speciali-hardening`, suite 951 verde, build 0 warning, due migration
provate su copia del `vipi.db` reale. Resta la **verifica live** (quattro punti elencati in fondo alla carta).

## 2026-08-10 — Audit dei tre documenti (doc [13](../refactor/13-audit-tre-documenti.md))

Partiti da un'osservazione dell'owner — «la sezione delle versioni dovrebbe essere la stessa per tutti e
tre i documenti, e non lo è» — e da lì un audit completo di vIPI ACC, vIPI APP e vLOA. La radice era una
sola: il doc 11 aveva reso uguale **come** i tre viewer leggono il documento, ma non **chi decide** il
comportamento di una sezione. Quella risposta viveva in sei `HashSet` di pagina, tre implementazioni di
«obbligatoria» (una delle quali confrontava i **titoli**) e un registro parallelo per la vLOA, di cui il
catalogo non sapeva nulla.

**Fondamenta.** `SectionCatalog` diventa fonte unica anche di *chi rende il corpo*
(`SectionBodySource {Blocks, Host}`, per profilo: «regulated» è un picker sulla vIPI ACC/APP e testo
bilaterale sulla vLOA) e di *quali sezioni sono obbligatorie*. La vLOA nasce dal catalogo come le altre
due — `VloaSections` resta solo la sorgente dei contenuti iniziali — e le due direzioni dei coordinamenti
prendono una chiave per verso (`coordination:out`/`:in`) invece di ripetere quella del padre: da lì
sparisce la cattura frozen fatta tre volte e l'identificazione della direzione per titolo (editor) o per
posizione (viewer), due modi diversi per la stessa cosa.

**I due difetti che uscivano dal documento.** La pagina APP pubblica derivava la tabella «Configurazioni»
dalla **versione di lavoro**: le configurazioni di una bozza mai pubblicata erano pubbliche, contro
l'invariante del doc 10. E ricerca e «Cosa è cambiato» partivano da «ha una versione corrente» e basta:
uscivano documenti nascosti dall'admin, **sezioni** marcate nascoste col loro estratto, e contenuto di
versioni senza release effettiva — che nessuna pagina serve. Ora il gate è quello della pagina, in un
posto solo.

**Il resto, in breve.** «Minime di vettoramento» torna una sezione in cui si può scrivere (era dichiarata
derivata senza derivare nulla, e proprio per questo l'editor non offriva i blocchi); il viewer vLOA rende
le sotto-sezioni di «Coordination», l'unico ramo che le buttava via; il ciclo AIRAC scritto in pagina è
quello del documento e non quello di oggi; `IDocRoutesRegistry` diventa l'unica porta per «dove si
raggiunge questo documento» — la ricerca mandava ogni APP sulla vIPI di ACC — con l'ancora `#s-{id}`
uguale ovunque e un filtro APP suo; il pannello release si porta dietro il proprio involucro e i quattro
editor passano gli stessi parametri; la landing ACC non promette più una vIPI senza release. Localizzati
i testi rimasti indietro (`AppsListPage` non usava il localizer nemmeno una volta) e tolto il codice che
nessuno chiamava più (`BuildAccVipiAsync`, `BuildVloaByPairAsync`, `SectionCatalog.Reconcile`).

**La verifica live ha trovato due cose che i test non vedevano** — ed è il motivo per cui il runbook la
chiede. «Aree regolamentate» nasceva aperta sull'APP e chiusa sulle altre due famiglie (un ramo che non
chiedeva al catalogo). E soprattutto: sulle vLOA **già pubblicate** le due direzioni dei coordinamenti
comparivano due volte, perché il viewer pubblico legge lo snapshot della release — congelato *prima* della
riconciliazione, quindi con entrambe le figlie ancora sulla chiave del padre. Regressione introdotta dal
passo che rendeva le sotto-sezioni, invisibile finché quel ramo le buttava via. Da qui una regola:
**una riconciliazione sistema i documenti, mai le release già pubblicate**, e sono quelle che il pubblico
legge.

**Coda dell'11 agosto, da una domanda dell'owner:** «la sezione AoR è uguale in tutti, e se la modifico si
modifica ovunque?». La risposta — *definizione e resa condivise, contenuto per documento* — è quella
giusta, ma verificandola su **tutte** le sezioni comuni sono uscite due copie che il giro non aveva
toccato. «Configurazioni»: il viewer della vIPI ACC ripeteva riga per riga il componente che il suo stesso
editor già usava, e le due copie erano già divergenti (solo una diceva «nessun settore aperto», solo
l'altra aveva il proprio messaggio di elenco vuoto) — quindi nell'editor si vedeva una cosa e nel
documento pubblicato un'altra. «Frequenze»: la vLOA aveva una tabella tutta sua, ed è il motivo per cui le
intestazioni erano andate per conto loro. Ora `AppFrequencies` sa attenuare una riga e portare una colonna
di azioni, e la vLOA la invoca una volta per lato. Resta di proposito la doppia resa dei **coordinamenti**:
`AccCoordinationView` e `AppCoordinationView` servono due modelli di dati diversi, perché un avvicinamento
non ha settori sotto di sé.

Chiusura: 20 commit sul branch `refactor/13-tre-documenti`, suite **1335 → 1391** verde, build senza
errori, verifica live dei tre documenti su copia del `vipi.db` reale.

## Feature: trasferimenti ACC↔APP — autorizzazione e trasferimento separati (11 ago 2026)

Carta ed esito: [`../feature/2026-08-11-trasferimenti-acc-app.md`](../feature/2026-08-11-trasferimenti-acc-app.md);
schema `../spec/modello-dati.md` §9.20-bis; area [`../refactor/07-trasferimenti.md`](../refactor/07-trasferimenti.md) §8.

Il modello dei trasferimenti descriveva **un evento con un livello**: regge un accordo ACC↔ACC, non regge un
ACC→APP, dove autorizzazione e trasferimento sono due momenti diversi. «Padova Military autorizza il traffico
con destinazione Aviano LIPA via CHI a FL160 o superiore e lo trasferisce ad Aviano Approach al confine dell'AoR
passando FL110 in discesa» non era esprimibile — non per come veniva reso, per come era fatto il modello.

Chiuse cinque cose nello stesso giro, perché vivono tutte sulla stessa riga: i due livelli con il proprio punto
di trasferimento (e le **comunicazioni** su colonna distinta dal controllo), la **velocità**, il **gruppo di
varianti** con la riga «negli altri casi», la sezione estesa che mostra **tutto ciò che entra o esce** da un
ente, e il filtro «da rivedere» per le righe scritte prima.

**Quattro difetti veri trovati strada facendo**, tre invisibili ai test:

- Lo scaffolding EF proponeva `defaultValue: ""` per cinque colonne enum-su-testo: le 73 righe in archivio
  sarebbero nate con una stringa illeggibile e la **prima lettura sarebbe andata in eccezione**. Chiuso
  dichiarando i default nel modello, che copre anche il `PostgresSchemaReconciler` del deploy Render.
- Una guardia esistente (`IndexedStringLengthTests`) ha fermato il giro sul fatto che su MySQL una stringa con
  DEFAULT nasce `longtext`, e `longtext` un default non può averlo. La guardia era però cieca a metà: cercava le
  colonne solo dentro il `CREATE TABLE` e le nostre arrivano con un `ALTER TABLE ADD` — falliva sulla propria
  ricerca, non sul difetto che presidia. Estesa.
- Lo scioglimento di un gruppo rimasto con una riga sola interrogava il database **prima** della `SaveChanges`,
  quindi vedeva ancora la riga appena sfilata e non scioglieva mai niente.
- Il mittente perdeva il codice di posizione quando non era un CTR: da quando la sezione estesa mostra ciò che
  entra da un APP, la frase diventava «Roma Radar trasferisce a Roma Radar TS». Nessun test esistente si è
  rotto correggendolo, il che dice quanto era coperto quel ramo.

**Un rischio si è sgonfiato leggendo il codice**: `TransferMatcher` sembrava il punto più esposto, ma il livello
non entra in nessun criterio di punteggio — serve solo a comporre l'etichetta quota, che ora prende quella al
trasferimento. Era una riga, non una revisione.

**Duplicazioni chiuse**: `CoordTable.razor` (la tabella dei coordinamenti era due volte, quasi identica, in
`AccCoordinationView` e `AppCoordinationView`) e `CoordinationDerivation.ToRow` (la vLOA si costruiva la riga a
mano). ⚠️ Supera la nota del refactor 13 «resta di proposito la doppia resa dei coordinamenti»: restano due
**viste**, perché l'albero è diverso; la tabella è una.

Suite **2111 → 2173** verde, `dotnet build -c Release --no-incremental` 0 warning su entrambi i TFM.
Migrazione unica `AddTransferHandoffSpeedAndVariants`, additiva, provata su copia del `vipi.db` reale.

**✅ Verifica live eseguita nella stessa giornata**, su copia del `vipi.db` reale (il DB del progetto è rimasto
intatto). Confermati a schermo: la frase a due eventi («autorizza … via BIRSU … e lo trasferisce … al confine
dell'AoR passando FL110»), il gruppo di varianti col `rowspan` e «negli altri casi» in fondo, le colonne che
compaiono **solo** dove servono (2 tabelle su 35 nella vIPI ACC, 1 su 9 nella vLOA), la vLOA interamente in
inglese, i tre flussi di `LYTV_APP` che ora l'ACC vede, il gruppo APP nuovo «verso altri APP», e la stampa
misurata a **larghezza carta** (636px su 760 disponibili, nessuno scorrimento).

**Altri tre difetti presi lì, nessuno visibile alla suite:** il vincolo del livello di trasferimento partiva da
«o superiore» invece che da «passando» su una riga che la faccetta non aveva mai usato (l'editor caricava lo
zero dell'enum come se fosse una scelta); «negli altri casi» era detto in due lingue nella stessa schermata,
perché la cella veniva dalle risorse dell'interfaccia e la frase dal template (ora entrambe dal template: è
contenuto, non chrome); e una testata mezza tradotta, con «PROSSIMO» in mezzo alle colonne inglesi. Più la
tinta delle righe-variante, che al 14% di `--line` a schermo **non si vedeva**: portata al 55% e misurata.

⚠️ **Una correzione a questa stessa scheda.** La carta aveva sostenuto che le righe BIRSU 76/77 fossero *senza
condizione*, e quindi che il lettore non sapesse quale applicare: falso, l'avevo dedotto da un conteggio
aggregato senza guardare le colonne della riga. Le due condizioni del DB sono proprio le loro. Il caso resta il
buon esempio del **legame** fra varianti; non lo è mai stato di un'ambiguità. *Un aggregato non è una riga.*

⚠️ **Resta ai colleghi, non al codice:** le 15 righe con ricevente APP e faccetta vuota vanno riviste a mano —
il loro livello può voler dire «autorizzato» o «al trasferimento». Il filtro «Da rivedere» in
`/vsop/admin/trasferimenti` le elenca; il numero va rimisurato sulla produzione.
