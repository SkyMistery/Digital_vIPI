# HANDOFF — Round 5: Fusione Settore/Posizione

**Data:** 25 giugno 2026
**Per:** la prossima chat che lavora su questo progetto.
**Stato:** refactor **completato e compilante**; **79 test verdi** (Domain 5 · Application 39 · Infrastructure 35). Verifiche end-to-end nell'app **a carico dell'utente** (non ancora fatte).

---

## 1. Cosa è stato chiesto e perché

Su IVAO non esiste separazione tra "posizione" e "settore": si parla sempre di **settore**. Il modello precedente aveva due entità distinte (`Position` = callsign apribile, `Sector` = volume di spazio aereo) legate da `PositionSector` (ownership) e `HierarchyRelation` (gerarchia). Decisione dell'utente: **fonderle in un'unica entità `Sector`**.

Decisioni prese (vincolanti):
1. **Tutto è un `Sector`** (CTR/APP/TWR/GND/DEL). Alcuni legati a un aeroporto via `AirportIcao`.
2. Contenimento = **albero a padre singolo** (`Sector.ParentSectorId`, self-FK). Logica top-down.
3. **`UnificationRule` mantenuta** (riassegnazioni arbitrarie non esprimibili dall'albero).
4. **DB greenfield**: migrazioni cancellate e rigenerate da zero.
5. **Scope documenti uno-a-molti**: un documento descrive N settori, ogni settore ha **un solo** documento di riferimento (`Sector.DocumentId` + flag `Sector.IsPrimary`). Le menzioni in altri documenti (trasferimenti/frequenze) restano semplici riferimenti via `ContentBlock`. Le vLOA tengono `DocumentParty` a parte.

Piano completo (se serve rileggerlo): `C:\Users\cgran\.claude\plans\eager-napping-gosling.md`.

---

## 2. Modello nuovo (già implementato)

`Sector` (`src/Vipi.Domain/Entities/Anagrafica.cs`) assorbe `Position`:
- Identificatore = **`Callsign`** (univoco). **`Sector.Key` ELIMINATA.**
- Campi ex-Position: `Type` (`SectorType`), `Kind` (`SectorKind`), `ApproachKind?`, `DefaultFrequency`, `CoverageOrder`, `FacilityId?`, `ImportedAtUtc?`, `IsActive`, `AirportIcao?`.
- Contenimento: `ParentSectorId?` + nav `ParentSector`/`Children`.
- Scope doc: `DocumentId?` + `IsPrimary` + nav `Document`.

**Eliminate:** `Position`, `PositionSector`, `HierarchyRelation`.
**Enum rinominati:** `PositionType`→`SectorType`, `PositionKind`→`SectorKind` (`src/Vipi.Domain/Enums.cs`).
**Document** (`Documents.cs`): rimosso `ScopePositionId`; aggiunto inverso `ICollection<Sector> Sectors`. `DocumentParty.PositionId`→`SectorId`. `Frequency.PositionId`→`SectorId`.

---

## 3. Motore AoR (semplificato)

- `Topology` (`src/Vipi.Application/Aor/Topology.cs`): **rimosso `DefaultSectors`**; aggiunto `IReadOnlyCollection<string> Sectors` (tutti i callsign). `Parent` (childCallsign→parentCallsign) ora deriva da `ParentSectorId`. `DomainOf`/`Ancestors` invariati.
- `AorService.Resolve` (`AorService.cs`): ownership di default = **identità** (ogni settore possiede sé stesso). Passi 2 (rules), 3 (top-down), 4 (stato) invariati. Chiavi di `AorResult.Ownership/State` = **callsign**.
- `TopologyBuilder` (`src/Vipi.Infrastructure/Aor/TopologyBuilder.cs`): costruisce da `Sector` (parent da `ParentSectorId`), niente più join `PositionSector`/`HierarchyRelation`.
- `ContentService`/`DocumentModels`: `BlockInput.ScopeSectorKey` **mantenuto come nome** ma ora contiene il **callsign** del settore (mappato in `EfContentRepository` da `ScopeSector.Callsign`).

---

## 4. Persistenza

- `VipiDbContext`: rimossi DbSet/config di `Position`/`PositionSector`/`HierarchyRelation`. `Sector`: `Callsign` UNIQUE, self-FK `ParentSectorId` (Restrict), FK `DocumentId` (SetNull). `Frequency`→`Sector`. `DocumentParty.Sector` (Restrict).
- Repository riscritti: `EfStructureEditingRepository` (CRUD settori unificati + `SetParent` lato topologia), `EfTopologyEditingRepository` (`AddHierarchy/DeleteHierarchy` → **`SetParentAsync`** con anti-ciclo; vocab = solo `Callsigns`), `EfEditingRepository` (`CreateDocumentAsync` con `scopeSectorIds`+`primarySectorId`; `GetAccCodeBySectorAsync`; `ScopeOf` da settore primario), `EfContentRepository`/`EfSearchRepository`/`EfChangesRepository`/`EfEditGrantRepository` (risolvono ACC/airport dai settori di scope).
- **Porte (Abstractions)** aggiornate di conseguenza: `IStructureEditingRepository`, `ITopologyEditingRepository`, `IEditingRepository`; servizi `StructureEditingService`/`TopologyEditingService`/`EditingService` idem.
- **Migrazioni**: tutte le vecchie cancellate, rigenerata un'unica **`InitialCreate`** in `src/Vipi.Infrastructure/Persistence/Migrations` (via design-time factory già presente).
- **Seed** (`Persistence/Seed/`): `RomaStructureSeed` ricreato (settori con `ParentSectorId`; split SU/ES = gerarchia, nessuna rule); `RomaContentSeed` (vIPI ACC ora descrive NE+EW+SU+ES+TS, NE primario); `RomaAirportSeed`/`RomaVloaSeed` agganciano `Sector.DocumentId`/parties via settore.

---

## 5. UI

- `StrutturaPage.razor`: **riscritta** — un solo elenco "Settori" (callsign, tipo, padre con `<select>`→`SetParent`, freq, ICAO), niente più ownership/posizioni. Creazione documento vIPI con **multi-select settori + radio primario** (chip); settori già assegnati a un altro doc disabilitati.
- `TopologiaPage.razor`: gerarchia ora via `SetParentAsync` (`_edit.Sectors`, `HierarchyRow.ChildSectorId/ParentSectorId`); simulatore usa `topology.Sectors`.
- `RidottaPage.razor`: `topo.Sectors` al posto di `DefaultSectors.Keys`.
- Helper `ChipStyle(...)` in StrutturaPage per evitare ternari annidati nello `style` (Razor non li parsava).

---

## 6. Come verificare (per l'utente)

```bash
cd "vIPI Ivao Italy"
dotnet build Vipi.slnx
dotnet test  Vipi.slnx     # atteso: 79 verdi
```
1. **Ferma l'app Host se in esecuzione** (bloccava i DLL durante il lavoro — solo lock di copia, non errori).
2. **Cancella `src/Vipi.Host/vipi.db`** (schema cambiato: greenfield).
3. `dotnet run --project src/Vipi.Host` → `/sop/admin/struttura`: crea settori con padre, poi una vIPI multi-settore (scegli più settori + primario).
4. Simulatore `/sop/{acc}/topologia`: S1/S2/S4/S5/S6 (S4 ora è puro contenimento).

---

## 7. Punti aperti / possibili follow-up

- **Validazione anti-ciclo `ParentSectorId`**: implementata in `EfTopologyEditingRepository.SetParentAsync` (e check banale in `TopologyEditingService`). Non c'è un vincolo DB; affidata all'app.
- **Invariante "un solo `IsPrimary` per documento"**: garantita dal codice di creazione, **non** da un indice DB. Se in futuro si edita lo scope di un documento esistente, serve una UI/logica dedicata (oggi lo scope si imposta solo alla creazione).
- **Import IVAO**: non esiste ancora codice che popola i settori dalle API (era così anche prima). Quando arriverà, scriverà su `Sector` (callsign + Kind/Type) invece che su due entità.
- **`docs/INTEGRATION.md` / `docs/CONFIG.md`**: non rivisti in questo round (non toccano il modello). Verificare se citano `Position`.
- Documenti aggiornati col banner "Round 5": `SPEC_Modello_Dati.md`, `SPEC_Logica_AoR.md`, `docs/sector-map.md`, `README.md`, `HANDOFF.md`.
