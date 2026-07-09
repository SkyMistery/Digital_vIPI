# 05 — Import ACC + settori confinanti (punti 5+6) 🟢🟡

> ACC esteri geometricamente adiacenti + i loro settori di confine, e generazione
> vLOA per coppia. Punti 5 e 6 sono **una sola run**. Dipende da: doc 01, doc 02
> (settori domestici per l'adiacenza).
>
> **NB** ref `IvaoApiClient.cs:162` storico → ora `IvaoAccClient`. **Nessun test diretto**
> sul flusso confinanti (solo `PolygonGeometry` coperto) → split **test-first** (invariante #8).

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

> ✅ APPROVATA — Fase 0, 2026-07-09. **Split completo, test-first** (invariante #8).

- **Spezzare `NeighbourImportService`** isolando prima il cuore deterministico:
  - **`NeighbourAdjacencyComputer` (PURO, nessun IO)** — filtro confine (`IsAccBoundaryPosition`),
    adiacenza domestici×esteri → hit, aggregazione per coppia → `NeighbourCandidateUpsert`; e il
    calcolo adiacenze+shape della coppia (`GetPairDetail`). Input/output espliciti →
    **unit-testabile**: qui vanno i test di caratterizzazione (robustezza vera del giro).
  - **`ForeignAccFetcher`** (dep `IAccDirectory`) — scarica ACC esteri per paese + subcenter
    (parallelismo throttled `RunBoundedAsync`). Testabile con fake directory.
  - **`NeighbourImportService`** resta orchestratore **sottile**: authz + scope DI dedicato +
    fetcher + computer + persist (`PersistForeignCatalogAsync` + `Sync`) + upsert candidati;
    conserva `List/SetStatus/SetPolygon/AddManual/GenerateVloa`.
- **Estrarre i 5 record** (`NeighbourCandidateRow`, `NeighbourImportResult`, `NeighbourAdjacency`,
  `NeighbourMapShape`, `NeighbourPairDetail`) + la classe `Aggregate` in file singoli.
- **`NeighbourDebugLog`**: logging conservato (invariante #7); `Warnings` continua a risalire alla UI.
- **Rimandato a doc 08/09**: spostare `MaterializeAndCreateVloaAsync`/`GenerateVloaAsync` verso il
  pipeline documenti (P4, accoppiamento dati↔documenti). Per ora resta invariato.
- Prerequisito **import domestico prima** documentato (già segnalato via `Warnings`).

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico → test-first → logica.

**Meccanico (commit separato):**
1. Estrarre i 5 record + `Aggregate` da `NeighbourImportService.cs` in file singoli.

**Test-first + logica (1 commit per passo, build verde):**
2. Estrarre **`NeighbourAdjacencyComputer`** (puro): filtro confine, adiacenza+aggregazione
   import, calcolo pair-detail. Scrivere **test di caratterizzazione**
   (`NeighbourAdjacencyComputerTests`): confine CTR/FSS incluso e TWR/APP escluso; solo settori
   adiacenti diventano hit; aggregazione min-distanza/conteggio settori distinti per coppia.
3. Estrarre **`ForeignAccFetcher`** (fetch ACC + subcenter esteri via `IAccDirectory`); test con
   fake directory (esteri filtrati dai domestici; warning su fetch fallita).
4. Riscrivere `NeighbourImportService` come orchestratore sottile su fetcher + computer.

**Rimandato:** `MaterializeAndCreateVloaAsync` → doc 08/09.

## 5. Impatto

- **Dipende da** doc 01, doc 02. **A valle**: doc 06 (gerarchia consuma i confinanti),
  doc 08/09 (generazione vLOA).
- **Verifica** (Fase 3): nuovi test caratterizzazione verdi; conteggio ≥ baseline (199 + nuovi);
  comportamento invariato (solo settori adiacenti persistiti; vLOA per coppia ancora generabile;
  mappa di verifica ok); `ListConfiningForeignCallsignsAsync` coerente con l'output.
