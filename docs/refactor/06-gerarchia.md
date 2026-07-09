# 06 — Gerarchia (albero di copertura) (punto 7) 🟢✅

> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/06-hierarchy`, 222 test: 214+8).
> `HierarchyNodeKind`/`HierarchyNode` estratti (§4.1); **`HierarchyRules` puro** (anti-ciclo,
> detection estero, adiacenza confinanti) con test di caratterizzazione (§4.2, +8);
> `EfHierarchyEditingService` ora delega le regole e tiene solo il data-access (§4.3).
> Migrazione completa del service a Application NON fatta (over-migration su poche regole).
> Vedi `../history/rounds.md` «Refactor 06».

> Albero globale cross-ACC ("Round 20") che lega settori/ACC/aeroporti via
> `ParentCallsign`. Consuma tutti i cataloghi 1-6. Dipende da: doc 02, 03, 05.

## 1. Stato attuale

Albero unico globale: nodi interni = subcenter ACC (`AccSector`) e posizioni APP di
aeroporto (`AirportSector` dove Position=APP); foglie = `Airport`. Editando un parent
si ri-proiettano i `Sector` operativi.

| File:riga | Classe/membro | Ruolo |
|---|---|---|
| `Vipi.Domain/Entities/Anagrafica.cs:36` | `AccSector` | Nodo subcenter; `ParentCallsign` (`:49-51`). |
| `Vipi.Domain/Entities/Anagrafica.cs:70` | `AirportSector` | Posizione ATC; `ParentCallsign` (`:85-87`, solo APP); `IsAccApp` (`:105-109`). |
| `Vipi.Domain/Entities/Anagrafica.cs:124` | `Airport` | Foglia; `ParentCallsign` (`:147-150`). |
| `Vipi.Domain/Entities/Anagrafica.cs:174` | `Sector` | Unità operativa; albero proprio `ParentSectorId` (`:202`); `IsProjected` (`:196`). |
| `Vipi.Application/Abstractions/IHierarchyEditingService.cs:34` | `IHierarchyEditingService` | Entry: `LoadTreeAsync`, `ListConfiningForeignCallsignsAsync`, `SetParentAsync`. |
| `Vipi.Application/Abstractions/IHierarchyEditingService.cs:4,18` | `HierarchyNodeKind` enum, `HierarchyNode` record | Modello nodo (Acc/App/Airport). |
| `Vipi.Application/Abstractions/ISectorProjectionService.cs` | `ISectorProjectionService` | `SyncFromCatalogsAsync` riproietta i `Sector`. |
| `Vipi.Application/Abstractions/ITopologyProvider.cs` | `ITopologyProvider` | Topologia globale (antenati) — consumata dai trasferimenti (doc 07). |
| `Vipi.Application/Aor/Topology.cs` | `Topology` | Risoluzione catena/antenati su `ParentCallsign`. |
| `Vipi.Application/Aor/PolygonGeometry.cs` | `PolygonGeometry` | `AreAdjacent` — adiacenza per confinanti. |
| `Vipi.Infrastructure/Persistence/EfHierarchyEditingService.cs:11` | `EfHierarchyEditingService` | Impl: `LoadTreeAsync` (`:34`), `ListConfiningForeignCallsignsAsync` (`:89`, cache 5min), `SetParentAsync` (`:122`), anti-ciclo `EnsureNoCycle` (`:213`); edit esteri admin-only (`:157-161`). |
| `Vipi.Infrastructure/Persistence/EfSectorProjectionService.cs` | `EfSectorProjectionService` | Materializza i `Sector` proiettati. |
| `Vipi.Infrastructure/Aor/TopologyBuilder.cs` | `TopologyBuilder` | Impl `ITopologyProvider`. |
| `Vipi.Ui/Pages/StrutturaPage.razor` | route `/vsop/admin/sectorstructure` | Editor gerarchia; filtro confinanti (`:240`); vincoli parent per-nazione (`:595-598`); `SetParentAsync` (`:259,530`). |

Foreign detection via `DivisionOptions.IcaoPrefixes` (`EfHierarchyEditingService.cs:31`).

## 2. Problemi

1. **Doppio albero**: catalogo (`ParentCallsign` su Acc/App/Airport) + operativo
   (`Sector.ParentSectorId`). La relazione tra i due passa da `SyncFromCatalogsAsync`
   ma la logica è distribuita tra `EfHierarchyEditingService` e `EfSectorProjectionService`.
2. **`EfHierarchyEditingService` in Infrastructure con logica di dominio** (anti-ciclo,
   vincoli parent per-nazione) → regole di business che dovrebbero stare in Application/Domain.
3. **`IHierarchyEditingService.cs` multi-tipo**: enum + record + interfaccia.
4. **Accoppiamento con confinanti** (`ListConfiningForeignCallsignsAsync`): la gerarchia
   conosce la nozione di "confinante" (doc 05) → dipendenza cross-area da rendere esplicita.
5. **Proiezione sparsa** (vedi doc 01): `SyncFromCatalogsAsync` invocata da molti punti.

## 3. Architettura target

> ✅ APPROVATA — Fase 0, 2026-07-09. **Estrai regole pure + test** (decisione A, invariante #8).
> Nessun test diretto sulla gerarchia. La maggior parte di `EfHierarchyEditingService` è
> data-access EF **legittimo** in Infra: NON si migra il service intero (over-migration).

- **Regole pure in Application** (`HierarchyRules`, statico come `PolygonGeometry`):
  `EnsureNoCycle` (anti-ciclo), `IsForeignCode` (detection estero da prefissi divisione),
  `ComputeConfiningForeignCallsigns` (adiacenza estero↔domestico). Input/output espliciti →
  **unit-testabili** (robustezza vera del giro). `EfHierarchyEditingService` tiene il
  data-access (`LoadTree`, parent-map, save, cache confinanti) e **delega** le regole.
- **Estrarre `HierarchyNodeKind`/`HierarchyNode`** da `IHierarchyEditingService.cs` in file singoli.
- **Proiezione**: `ISectorProjectionService` è già l'unico punto; la `Sync` in `SetParent` è
  una riproiezione legittima (non un import). Nessun cambio (P5 già coperto da doc 01).
- **Migrazione completa del service a Application** (porta `IHierarchyRepository`): NON in
  questo giro — poco valore (poche regole) vs alto churn su codice non testato.

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico → test-first → logica.

**Meccanico (commit separato):**
1. Estrarre `HierarchyNodeKind` + `HierarchyNode` da `IHierarchyEditingService.cs` in file singoli.

**Test-first + logica:**
2. Creare `HierarchyRules` (Application, puro): `EnsureNoCycle`, `IsForeignCode`,
   `ComputeConfiningForeignCallsigns`. Scrivere test di caratterizzazione (`HierarchyRulesTests`):
   ciclo rifiutato / catena valida ok; estero riconosciuto da prefisso; solo settori esteri
   adiacenti a un domestico entrano nel set confinante.
3. Far delegare `EfHierarchyEditingService` a `HierarchyRules` (rimuove la logica inline).

## 5. Impatto

- **Dipende da** doc 02, 03, 05 (cataloghi). **A valle**: doc 07 (trasferimenti usano
  `ITopologyProvider`).
- **Verifica** (Fase 3): nuovi test regole verdi; `LoadTreeAsync` coerente; `SetParentAsync`
  rifiuta cicli; proiezione `Sector` invariata; edit esteri restano admin-only; conteggio ≥ baseline (214).
