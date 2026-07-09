# 06 — Gerarchia (albero di copertura) (punto 7) 🟢🟡

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

> 🟡 BOZZA.

- **Regole di business della gerarchia in Application** (anti-ciclo, vincoli parent,
  detection estero); Infrastructure solo persistenza.
- **Un service di proiezione** come unico punto che traduce albero-catalogo →
  albero-operativo, chiamato solo dai core import (doc 01).
- Estrarre enum/record da `IHierarchyEditingService.cs`.
- Dipendenza da confinanti esplicita (porta dedicata, non conoscenza implicita).

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre `HierarchyNodeKind`/`HierarchyNode`.
2. Spostare anti-ciclo e vincoli parent in Application.
3. Centralizzare la proiezione (coordinato con doc 01).

## 5. Impatto

- **Dipende da** doc 02, 03, 05 (cataloghi). **A valle**: doc 07 (trasferimenti usano
  `ITopologyProvider`).
- **Verifica**: `LoadTreeAsync` coerente; `SetParentAsync` rifiuta cicli; proiezione
  `Sector` invariata; edit esteri restano admin-only.
