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
