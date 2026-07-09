# 03 — Import aeroporti + settori di aeroporto (punti 3+4) 🟢🟡

> Aeroporti (anagrafica/assegnazione) e posizioni ATC di aeroporto (incl. APP).
> Il punto 3 **riusa** il punto 4. Dipende da: doc 01.
>
> **NB** i ref a `IvaoApiClient.cs` in sez.1 sono storici: dal refactor 01 le porte
> aeroporto sono `IvaoAirportClient` (anagrafica) e `IvaoAirportDetailClient` (posizioni).
> `AirportSectorImporter` è già l'orchestratore unico delle posizioni (P2 in gran parte
> già risolto); i call-site non duplicano il loop.

## 1. Stato attuale

### Punto 3 — aeroporti (anagrafica → assegnazione)
Non esiste un servizio di import aeroporti dedicato: gli aeroporti si tirano dalla
directory e si assegnano dentro `StructureEditingService`.

| Item | File:riga | Layer | Ruolo |
|---|---|---|---|
| Entry manuale | `Vipi.Ui/Pages/AeroportiPage.razor:306` | Web | `Struct.AutoAssignKnownAirportsAsync()` |
| Use-case | `Vipi.Application/Content/StructureEditingService.cs:170` | App | `IAirportDirectory.GetAirportsAsync` → repo `AutoAssignAirportsAsync`, poi per-aeroporto `IAirportSectorImporter.ImportAsync` (→ overlap con punto 4). |
| Cache | `Vipi.Infrastructure/Ivao/IvaoAirportCache.cs:9` | Infra | Cache singleton dell'anagrafica paginata. |
| Porta | `IAirportDirectory.GetAirportsAsync` → `IvaoApiClient.cs:112` | Infra | `/v2/airports` paginato per country. |
| Altri consumatori | `AirportProfileService.cs:174`, `StructureEditingService.cs:235` | App | Stesso `GetAirportsAsync` per Transition Altitude / profilo. |

Nessun job automatico per l'anagrafica aeroporti (solo "auto-assign" manuale).

### Punto 4 — settori di aeroporto (posizioni ATC incl. APP)

| Item | File:riga | Layer | Ruolo |
|---|---|---|---|
| Entry manuale | `Vipi.Ui/Pages/AeroportoEditorPage.razor:908` | Web | `Sectors.ImportFromSourceAsync(icao)` |
| Wrapper ACC-gated | `Vipi.Application/Content/AirportSectorService.cs:68` | App | Authz + importer + proiezione + ensure documento. |
| Orchestratore (no authz) | `Vipi.Application/Content/AirportSectorImporter.cs:18` | App | `GetAtcPositionsAsync` → per-posizione `GetAtcPositionDetailAsync` → `ImportForAirportAsync`. Riusato 3×. |
| Job auto | `Vipi.Infrastructure/Ivao/AirportSectorImportHostedService.cs:15` | Infra | Loop ICAO → importer → ensure doc → proiezione → fallback shape TWR. |
| Porte | `IvaoApiClient.cs:393` / `:410` | Infra | `/v2/ATCPositions/{pos}`. |
| Repo | `EfAirportSectorRepository.ImportForAirportAsync` | Infra | Upsert preservando limiti/hidden admin. |

**Entità**: `Airport` (`Anagrafica.cs:124`, leaf, `ParentCallsign`), `AirportSector`
(`:70`, `ParentCallsign` solo APP, `IsAccApp` `:105`).

## 2. Problemi

1. **Punto 3 non ha una pipeline propria**: vive dentro `StructureEditingService`
   (un servizio che fa molte altre cose) → responsabilità confuse.
2. **`AirportSectorImporter` riusato 3×** con call-site che duplicano l'orchestrazione:
   `AirportSectorService` (4-manual), `AirportSectorImportHostedService` (4-auto),
   `StructureEditingService.AutoAssignKnownAirportsAsync` (3 ri-esegue 4).
3. **Duplicazione manual-vs-auto** anche qui (`AirportSectorService.cs:68` vs
   `AirportSectorImportHostedService.cs:35`).
4. **`AirportSectorService.cs` multi-classe**: `AirportSectorRow`,
   `AirportSectorImportResult`, interfaccia + classe (`:11,17,25`).
5. **`AirportSectorImporter.cs` multi-classe**: interfaccia + classe.
6. **Ensure-documento dentro l'import**: l'import di posizioni innesca la creazione
   del documento aeroporto (`GenerateAirportDocumentCoreAsync`) → import e documento
   accoppiati (dolore che riemerge nel doc 08).
7. **`GetAirportsAsync` chiamato da 3 punti** per scopi diversi (assegnazione, TA, profilo).

## 3. Architettura target

> ✅ APPROVATA — Fase 0, 2026-07-09.

- **`AirportImportUseCase`** (+ `IAirportImportUseCase`) dedicato per il punto 3, fuori da
  `StructureEditingService` (P1). Corpo: `GetAirportsAsync → AutoAssignAirportsAsync →
  foreach import`. Ritorna `AirportImportResult { Assigned, Failures }`; i fallimenti import
  per-aeroporto (oggi scartati in silenzio a `StructureEditingService:185`) sono **raccolti
  e ritornati** → il chiamante li logga (*direttiva logging*). `StructureEditingService.
  AutoAssignKnownAirportsAsync` = `EnsureAdmin` + delega + `Sync`; la UI (`AeroportiPage`)
  logga/mostra i `Failures`.
- **`AirportSectorImporter`** resta l'orchestratore unico delle posizioni (già così);
  spostare la sua interfaccia in file proprio.
- **Scollegare ensure-documento dall'import — *decisione D-03: separazione reale (B)*.**
  L'import popola **solo** il catalogo; NON genera più il documento. Si rimuovono le
  chiamate `EnsureAirportDocumentSystemAsync` da `AirportSectorService.ImportFromSourceAsync`
  e dal loop di `AirportSectorImportHostedService`; il metodo *System* (no-authz) diventa
  morto → rimosso. La generazione documento resta **solo** manuale via
  `GenerateAirportDocumentAsync` (bottone admin «📄 Genera documenti», `AeroportiPage:63`).
  ⚠ **Cambio di comportamento intenzionale e approvato**: importare i settori non aggiorna
  più il documento (prima lo faceva). Il §5 originale già lo anticipava.
- Estrarre i record da `AirportSectorService.cs` (`AirportSectorRow`,
  `AirportSectorImportResult`, `IAirportSectorService`).

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico prima, logica dopo.

**Meccanico (commit separato):**
1. Estrarre da `AirportSectorService.cs` i record `AirportSectorRow`,
   `AirportSectorImportResult` e l'interfaccia `IAirportSectorService`; estrarre
   `IAirportSectorImporter` da `AirportSectorImporter.cs`. File singoli.

**Con logica (1 commit per passo, build verde):**
2. Creare `AirportImportUseCase` (+ `IAirportImportUseCase`, `AirportImportResult`,
   `AirportImportFailure`). Spostarvi il corpo anagrafica da
   `StructureEditingService.AutoAssignKnownAirportsAsync`; il metodo diventa
   `EnsureAdmin` + delega + `Sync` e ritorna i `Failures`; `AeroportiPage` li logga.
3. **Scollegamento (B)**: rimuovere `EnsureAirportDocumentSystemAsync` da
   `AirportSectorService.ImportFromSourceAsync` e dal loop di
   `AirportSectorImportHostedService`; rimuovere il metodo *System* (morto). Import =
   solo catalogo + `Sync` (+ fallback shape nel job auto).

## 5. Impatto

- **Dipende da** doc 01. **A valle**: doc 06 (gerarchia usa `AirportSector` APP come nodi,
  `Airport` come leaf); doc 08 riprende la generazione documento (ora completamente separata).
- **Verifica** (Fase 3): import (manuale editor + job auto) popola il catalogo settori
  **senza** generare documenti; il bottone «📄 Genera documenti» genera ancora; aeroporti
  assegnati correttamente da `AutoAssign`; fallimenti import per-aeroporto ora **loggati**;
  conteggio test = baseline (199). *Nota: questo giro contiene un cambio di comportamento
  approvato (import non genera doc) — la verifica controlla il comportamento nuovo, non l'invarianza.*
