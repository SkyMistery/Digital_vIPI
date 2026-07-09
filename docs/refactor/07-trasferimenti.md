# 07 — Trasferimenti (punto 8) 🟢🟡

> Flussi di trasferimento (CoP + livelli + settore successivo). Risoluzione live che
> cammina l'albero: un settore chiuso è assorbito dal primo antenato online.
> Dipende da: doc 06 (gerarchia/topologia).

## 1. Stato attuale

| File:riga | Classe/membro | Ruolo |
|---|---|---|
| `Vipi.Domain/Entities/Support.cs:87` | `TransferFlow` | Flusso: settore owner, `TransferFlowKind`, aeroporto, punti. |
| `Vipi.Domain/Entities/Support.cs:112` | `TransferPoint` | CoP + `LevelValue/Unit/Constraint/Special` + `NextSectorId`. |
| `Vipi.Domain/Enums.cs:70,73,77` | `TransferFlowKind`, `LevelUnit`, `LevelConstraint` | Enum flusso/livello. |
| `Vipi.Application/Content/TransferEditingService.cs:8` | `ITransferService` | Entry: list/resolve/add/update/delete flussi & punti, `MovePointAsync`. |
| `Vipi.Application/Content/TransferEditingService.cs:31` | `TransferService` | Impl; `ResolveForAccAsync` (`:47`) costruisce catene via `ITopologyProvider`; write ACC-gated; validazione soft (`:124-134`). |
| `Vipi.Application/Content/TransferOnlineResolver.cs` | `TransferOnlineResolver` | `FirstOnline`/`Resolve` — primo settore online nella catena. |
| `Vipi.Application/Content/TransferModels.cs:11,25,40,49,60,70` | `TransferFlowRow`, `TransferPointRow`, `TransferFlowInput`, `TransferPointInput`, `ResolvedTransferPoint`, `ResolvedTransferFlow` | DTO read/input/live. |
| `Vipi.Application/Abstractions/ITransferRepository.cs` | `ITransferRepository` | Porta persistenza. |
| `Vipi.Infrastructure/Persistence/EfTransferRepository.cs` | `EfTransferRepository` | CRUD EF + ordinamento flussi/punti. |
| `Vipi.Infrastructure/Persistence/Seed/RomaTransferSeed.cs` | `RomaTransferSeed` | Trasferimenti demo. |
| `Vipi.Ui/Pages/AdminTrasferimentiPage.razor` | route `/vsop/admin/trasferimenti` | Editor; inietta `ITransferService`, `IStationResolver`, `INeighbourImportService`. |
| `Vipi.Ui/Components/App/TransfersLive.razor` | `TransfersLive` | Vista live risolta. |

I trasferimenti compaiono come sezione **Coordinamenti** nei documenti ACC/APP/vLOA —
derivati live, non salvati nel payload editoriale.

## 2. Problemi

1. **`TransferEditingService.cs` multi-classe**: interfaccia + impl.
2. **`TransferModels.cs` multi-classe**: 6 DTO in un file.
3. **Pagina editor accoppiata con l'import confinanti** (`INeighbourImportService` in
   `AdminTrasferimentiPage`) → mescola editing trasferimenti e trigger import (doc 05).
4. **Risoluzione live** (`ResolveForAccAsync` + `TransferOnlineResolver`) dipende dalla
   topologia (doc 06): l'accoppiamento è corretto ma va documentato come contratto stabile.
5. **Validazione soft** inline (`:124-134`) — verificare che segua la convenzione
   `ValidationException` di Application (memoria: mai DataAnnotations).

## 3. Architettura target

> 🟡 BOZZA.

- Estrarre interfaccia e DTO in file singoli.
- **Separare editing dal trigger import** nella UII: la pagina trasferimenti non deve
  invocare l'import confinanti direttamente.
- Confermare `ITopologyProvider` come unico contratto verso la gerarchia (doc 06).
- Uniformare la validazione alla convenzione `Vipi.Application.*.ValidationException`.

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre `ITransferService` e i 6 DTO.
2. Rimuovere la dipendenza `INeighbourImportService` dalla pagina trasferimenti.
3. Verificare/uniformare la validazione.

## 5. Impatto

- **Dipende da** doc 06. **A valle**: doc 08 (sezione Coordinamenti derivata dai flussi).
- **Verifica**: risoluzione live corretta (settore chiuso → antenato online → UNICOM
  terminale); CRUD flussi/punti ACC-gated; sezione Coordinamenti coerente nei documenti.
