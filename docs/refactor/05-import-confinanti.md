# 05 — Import ACC + settori confinanti (punti 5+6) 🟢🟡

> ACC esteri geometricamente adiacenti + i loro settori di confine, e generazione
> vLOA per coppia. Punti 5 e 6 sono **una sola run**. Dipende da: doc 01, doc 02
> (settori domestici per l'adiacenza).

## 1. Stato attuale

| Item | File:riga | Layer | Ruolo |
|---|---|---|---|
| Entry manuale | `Vipi.Ui/Pages/ConfinantiAdminPage.razor:322`; `AdminTrasferimentiPage.razor:8` | Web | `Neighbours.ImportAndComputeAsync()` |
| Use-case | `Vipi.Application/Content/NeighbourImportService.cs:65` | App | Admin-only. Per `Neighbours:CountryIds`: `GetCentersByCountryAsync` → ACC esteri, poi adiacenza geometrica vs settori domestici; stage `NeighbourCandidate`. |
| Fetch settori esteri | `NeighbourImportService.cs:148-178` | App | Per ACC estero: `GetSubcentersAsync` (stessa porta del doc 02), filtro CTR/FSS di confine, adiacenza. |
| Persist confinanti | `NeighbourImportService.cs:182-191` | App | `INeighbourRepository.PersistForeignCatalogAsync` (solo settori che confinano) → `SyncFromCatalogsAsync`. |
| Re-fetch on-demand | `NeighbourImportService.cs:267` `GetPairDetailAsync` | App | Riscarica i subcenter esteri per la mappa di verifica; non persistito. |
| Porta ACC esteri | `GetCentersByCountryAsync` → `IvaoApiClient.cs:162` | Infra | Stesso `/v2/centers` del doc 02 (overlap). |
| Repo | `EfNeighbourRepository` (`PersistForeignCatalogAsync`, `MaterializeAndCreateVloaAsync`) | Infra | Persiste solo catalogo estero confinante + genera vLOA. |
| Geometria | `Vipi.Application/Aor/PolygonGeometry.cs` (`ToRing`, `AreAdjacent`) | App | Adiacenza poligoni. |

Nessun job automatico — solo trigger admin manuale. `Acc.IsForeign` (`Anagrafica.cs:14`)
marca gli ACC esteri persistiti.

## 2. Problemi

1. **File monstre multi-classe**: `NeighbourImportService.cs` = 7 record + interfaccia +
   classe privata `Aggregate` (`:12,18,23,27,34,49,393`) = 9 tipi in un file.
2. **Servizio troppo grande**: fa fetch ACC + fetch settori + adiacenza geometrica +
   staging candidati + persistenza + generazione vLOA + re-fetch mappa. Troppe responsabilità.
3. **Overlap con doc 02**: riusa `GetCentersByCountryAsync` e `GetSubcentersAsync` ma con
   logica di filtro propria; la relazione tra import domestico ed estero non è esplicita.
4. **Generazione vLOA dentro l'import** (`MaterializeAndCreateVloaAsync`): l'import di
   dati confinanti crea documenti → accoppia asse A (dati) e asse B (documenti).
5. **6 alimenta 7**: l'output confinanti (callsign esteri adiacenti) è consumato dalla
   gerarchia (`ListConfiningForeignCallsignsAsync`, doc 06) → confermata la dipendenza.

## 3. Architettura target

> 🟡 BOZZA.

- **Spezzare `NeighbourImportService`** in responsabilità separate:
  - `ForeignAccFetcher` (fetch ACC + settori esteri, riusa porte doc 01/02);
  - `AdjacencyComputer` (geometria, già isolabile via `PolygonGeometry`);
  - `NeighbourStagingService` (candidati);
  - generazione vLOA spostata verso il pipeline documenti (doc 08/09), non dentro l'import.
- Estrarre i 7 record in file singoli.
- Rendere esplicita la dipendenza da doc 02 (i settori domestici devono essere importati prima).

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre i record da `NeighbourImportService.cs`.
2. Separare fetch / adiacenza / staging / persistenza.
3. Spostare `MaterializeAndCreateVloaAsync` verso il pipeline documenti (dopo doc 08).
4. Documentare il pre-requisito "import domestico prima".

## 5. Impatto

- **Dipende da** doc 01, doc 02. **A valle**: doc 06 (gerarchia consuma i confinanti),
  doc 08/09 (generazione vLOA).
- **Verifica**: run confinanti → solo settori realmente adiacenti persistiti; vLOA per
  coppia generata; mappa di verifica funzionante; `ListConfiningForeignCallsignsAsync`
  coerente con l'output.
