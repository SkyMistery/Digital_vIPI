# 03 — Import aeroporti + settori di aeroporto (punti 3+4) 🟢🟡

> Aeroporti (anagrafica/assegnazione) e posizioni ATC di aeroporto (incl. APP).
> Il punto 3 **riusa** il punto 4. Dipende da: doc 01.

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

> 🟡 BOZZA.

- **`AirportImportUseCase`** dedicato per il punto 3, fuori da `StructureEditingService`.
- **`AirportSectorImporter`** resta l'unico orchestratore delle posizioni; i 3 call-site
  lo invocano senza duplicare l'iterazione.
- **Scollegare ensure-documento dall'import**: l'import popola i cataloghi; la creazione
  del documento è un passo separato (coordinato con doc 08).
- Estrarre i record dai file multi-classe.

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre record da `AirportSectorService.cs` e interfaccia da `AirportSectorImporter.cs`.
2. Creare `AirportImportUseCase`, spostarvi la parte di anagrafica da `StructureEditingService`.
3. Convergere i 3 call-site su `AirportSectorImporter`.
4. Separare `EnsureAirportDocument*` dall'import (dipende da decisioni doc 08).

## 5. Impatto

- **Dipende da** doc 01. **Accoppiato con** doc 08 (ensure-documento). **A valle**:
  doc 06 (gerarchia usa `AirportSector` APP come nodi, `Airport` come leaf).
- **Verifica**: import posizioni manuale e auto → stesso stato; aeroporti assegnati
  correttamente; documento aeroporto ancora generabile dopo lo scollegamento.
