# 01 — Infra import condivisa (L0) 🟢✅

> Base di tutti i pipeline di import (punti 1-6, 11). Copre le porte neutre, il loop
> periodico comune e la proiezione post-import. Da rifattorizzare **per prima**:
> gli strati 02-05 vi si appoggiano.
>
> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/01-import-infra`, 199 test verdi).
> DTO estratti (§4.1), `IvaoApiClient` spezzato in 6 client per porta + `IvaoHttp` (§4.2),
> `AccImportUseCase` core condiviso manual/auto (§4.4-4.5). ACC = pilota; aeroporti/SID/
> confinanti applicano lo stesso pattern nei doc 03/04/05. Vedi `../history/rounds.md`
> «Refactor 01».

## 1. Stato attuale

### Pezzi condivisi

| Componente | File:riga | Layer | Ruolo |
|---|---|---|---|
| `GatedImportLoop` (static) | `Vipi.Infrastructure/GatedImportLoop.cs:13` | Infra | Loop periodico comune; salta il fetch se la categoria è ancora fresca (`IImportStateStore`), retry orario su errore. Usato dai 4 hosted service. |
| `IvaoApiClient` | `Vipi.Infrastructure/Ivao/IvaoApiClient.cs:14` | Infra | Adapter unico che implementa `IAccDirectory`, `IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IDivisionMembersProvider`. |
| `ISectorProjectionService.SyncFromCatalogsAsync` | porta in `Vipi.Application/Abstractions/ISectorProjectionService.cs` | App | Riproietta i `Sector` operativi dai cataloghi. Chiamata dopo ogni import. |
| DI import | `Vipi.Infrastructure/DependencyInjection.cs:56-64`; `AtcPollingHostedService.cs:103-118` | Infra | Bind porte→`IvaoApiClient`; registra i 4 hosted service + SID. |
| Policy/stato import | `IImportStateStore`, `IImportPolicyStore` (porte App) | App/Infra | Freshness per categoria + abilitazione import (ADR-0006). |

### Porte (Application) e endpoint (Infra)

| Porta | Metodo | Impl `IvaoApiClient` | Endpoint IVAO |
|---|---|---|---|
| `IAccDirectory` | `GetCentersAsync` | `:158` | `/v2/centers` (filtro country) |
| `IAccDirectory` | `GetCentersByCountryAsync` | `:162` | `/v2/centers` (per confinanti) |
| `IAccDirectory` | `GetSubcentersAsync` | `:233` | `/v2/centers/{icao}/subcenters` |
| `IAccDirectory` | `GetSpecialAreasAsync` | `:286` | aree speciali |
| `IAirportDirectory` | `GetAirportsAsync` | `:112` | `/v2/airports` (paginato) |
| `IAirportDetailProvider` | `GetAtcPositionsAsync` / `GetAtcPositionDetailAsync` | `:393` / `:410` | `/v2/ATCPositions/{pos}` |

### Hosted service (auto)
`AccImportHostedService`, `AirportSectorImportHostedService`, `SpecialAreaImportHostedService`,
`SidImportHostedService` — tutti `BackgroundService` guidati da `GatedImportLoop`.

## 2. Problemi

1. **Duplicazione manual-vs-auto.** Ogni categoria ha due entry point con corpo quasi
   identico: use-case Application (con authz, es. `AccAdminService.ImportFromSourceAsync`)
   e `*HostedService.ImportOnceAsync` (senza authz). La logica fetch+upsert+proiezione
   è copiata. → il "core import" non è centralizzato.
2. **`SyncFromCatalogsAsync` chiamata sparsa** in ~6 call-site (AccAdminService,
   AirportSectorService, i due hosted job, NeighbourImportService, StructureEditingService)
   invece di essere un passo garantito dopo ogni import.
3. **`IvaoApiClient.cs` multi-classe**: 1 classe + 11 record DTO privati (`:506-560`).
   Adapter unico che implementa 5 porte diverse → viola SRP, difficile da testare a pezzi.
4. **Endpoint condiviso `/v2/centers`** servito da due metodi (`GetCentersAsync` +
   `GetCentersByCountryAsync`) con overlap tra import domestico e confinanti (doc 05).
5. **`SpecialAreaImportHostedService` senza entry manuale** — asimmetria con gli altri.

## 3. Architettura target

> ✅ APPROVATA — Fase 0 chiusa 2026-07-09 (decisioni sotto). Verifica sez. 1+2 vs codice:
> confermate. Delta: `SyncFromCatalogsAsync` ora in **7** call-site (aggiunto
> `EfHierarchyEditingService`), non ~6 — deriva confermata.

- **Core import: classi per-categoria** in Application — `AccImportUseCase`,
  `AirportImportUseCase`, `SidImportUseCase`. Ognuna con corpo esplicito
  `fetch → upsert → SyncFromCatalogsAsync`. Manual (con authz-guard) e auto (hosted
  service) la **invocano**; nessuno la ri-scrive.
  - *Decisione (D1):* classi per-categoria, **non** un generico `IImportUseCase<T>`.
    Motivo: sole 3 categorie (DRY del generico rende poco) + SID viene da GitHub, non
    dall'API IVAO → non entra in uno stampo comune senza leaky abstraction. La sola
    ripetizione residua è lo scheletro (try/log/`Sync`), di forma non di logica; accettata.
- **`GatedImportLoop`** resta l'unico orchestratore periodico; ogni hosted service
  diventa un thin wrapper `while(loop) → useCase.RunAsync()`.
- **Proiezione garantita**: `SyncFromCatalogsAsync` chiamata solo dentro il core import,
  non nei call-site sparsi.
- **Spezzare `IvaoApiClient`** (565 righe, 5 porte, 11 DTO) per porta — `IvaoAccClient`,
  `IvaoAirportClient`, `IvaoAtcPositionClient`; DTO in file dedicati
  (`Vipi.Infrastructure/Ivao/Dtos/`).
  - *Decisione (D2):* **incluso in questo giro (doc 01)**, non rimandato. È infra L0:
    nessun altro doc di area lo possiede, rimandarlo lo renderebbe orfano. Split meccanico
    (sposta codice, nessuna logica) → rischio basso.
- **Authz** estratto in un guard riusabile, così manual e auto condividono il core ma
  solo il manual applica il guard.
- **Invariante: agnosticismo dal provider preservato.** Ogni sorgente esterna resta dietro
  la sua porta Application (`IAccDirectory`, `ISidProvider`, `IWeatherProvider`…);
  sostituire una fonte = nuovo adapter Infra, zero tocchi ad Application/UI. Lo split di
  `IvaoApiClient` *migliora* l'agnosticismo (1 client = 1 porta, non 1 classe = 5 porte).
  I nuovi `XxxImportUseCase` espongono un'interfaccia `IXxxImportUseCase` per coerenza con
  i service esistenti (mockabilità nei test); non sono un confine di provider, il provider
  è già dietro le porte-directory.

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico prima, logica dopo (regole invarianti
> in [REFACTOR-PROCESS.md](REFACTOR-PROCESS.md)).

**Meccanici (commit separati, nessuna logica):**
1. Estrarre gli 11 DTO di `IvaoApiClient` in file singoli sotto `Ivao/Dtos/`.
2. Splittare `IvaoApiClient` per porta → `IvaoAccClient` / `IvaoAirportClient` /
   `IvaoAtcPositionClient`; aggiornare i bind DI (`DependencyInjection.cs:56-64`).

**Con logica (1 commit per passo, build verde):**
3. Estrarre l'authz-guard riusabile dal manual entry point.
4. Introdurre `AccImportUseCase` (corpo unico fetch→upsert→`Sync`); riscrivere
   `AccImportHostedService.ImportOnceAsync:35` e `AccAdminService.ImportFromSourceAsync:57`
   per **delegare** al core (dettaglio pieno in doc 02).
5. Rimuovere la chiamata `SyncFromCatalogsAsync` dai call-site che migrano al core;
   obiettivo: unica invocazione dentro il core import.

**Fuori scope doc 01** (ripetono il pattern nei loro giri): aeroporti → doc 03,
SID → doc 04, confinanti → doc 05. Doc 01 stabilisce il pattern + l'infra condivisa e
usa **ACC come pilota**.

## 5. Impatto

- **A valle**: tocca tutti i doc 02-05; è la fondazione. Da fare per prima.
- **Verifica** (Fase 3): import manuale e auto devono produrre lo stesso stato DB;
  `SyncFromCatalogsAsync` deve girare una sola volta per import; i test esistenti sui
  `Sector` proiettati devono restare verdi (conteggio baseline registrato in Fase 1).
