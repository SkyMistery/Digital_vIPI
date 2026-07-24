# 07 — Trasferimenti (punto 8) 🟢✅

> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/07-transfers`, 222 test).
> `ITransferService` + 6 DTO estratti in file singoli (§4.1); porta di lettura
> `INeighbourReader` (ISP) — `AdminTrasferimentiPage` non dipende più dal service import
> completo (§4.2). Validazione già conforme (`Aor.ValidationException`); `TransferOnlineResolver`
> già testato. Vedi `../history/rounds.md` «Refactor 07».

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

> ✅ APPROVATA — Fase 0, 2026-07-09. Verifica sez.2 vs codice:
> - **P3 mal descritto**: la pagina NON triggera l'import — fa `Neighbours.ListAsync()`
>   (lettura dei confinanti per i mittenti estero→home). È un problema di **ISP/decoupling
>   d'area**, non di trigger.
> - **P5 già conforme**: la validazione usa `Vipi.Application.Aor.ValidationException` — nessun cambio.
> - `TransferOnlineResolver` (risoluzione live) è **già testato** → nulla da irrobustire (#8).

- **Estrarre `ITransferService`** (da `TransferEditingService.cs`) e i **6 DTO** (da
  `TransferModels.cs`) in file singoli.
- **Porta di lettura dedicata `INeighbourReader`** (`{ ListAsync }`): `NeighbourImportService`
  la implementa (oltre a `INeighbourImportService`); `AdminTrasferimentiPage` inietta
  `INeighbourReader` invece del service import completo. `ConfinantiAdminPage` (usa
  import/generate/pair-detail) resta su `INeighbourImportService`. Decoupling d'area 05↔07.
- `ITopologyProvider` confermato come unico contratto verso la gerarchia (doc 06) — invariato.

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico → logica.

**Meccanico (commit separato):**
1. Estrarre `ITransferService` da `TransferEditingService.cs`; i 6 DTO
   (`TransferFlowRow`/`TransferPointRow`/`TransferFlowInput`/`TransferPointInput`/
   `ResolvedTransferPoint`/`ResolvedTransferFlow`) da `TransferModels.cs`. File singoli.

**Con logica:**
2. Introdurre `INeighbourReader { ListAsync }`; far estendere `INeighbourImportService : INeighbourReader`;
   registrare la porta in DI; `AdminTrasferimentiPage` inietta `INeighbourReader`.

*(§4.3 validazione: già conforme, nessuna azione.)*

## 5. Impatto

- **Dipende da** doc 06. **A valle**: doc 08 (sezione Coordinamenti derivata dai flussi).
- **Verifica** (Fase 3): risoluzione live invariata (test `TransferOnlineResolver` verdi);
  CRUD flussi/punti ACC-gated; la pagina trasferimenti mostra ancora i mittenti esteri
  confinanti; conteggio test = baseline (222).

## 6. Aggiornamenti successivi (2026-07-13)
- **Sorvoli senza aeroporto** derivati nei Coordinamenti (cuore condiviso `CoordinationDerivation`;
  frase kind-aware). I consumer ACC/APP/**vLOA** devono passare `flow.Kind` a `CoordinationSentences.Compose`
  (il fix vLOA chiudeva una falla di propagazione: prima ometteva il kind → sorvolo trattato come arrivo →
  frase nulla). Un punto con ricevente non risolto (null/non in `types`) viene scartato: policy intenzionale,
  segnalata in editor (badge «nessun ricevente»). Vedi `history/rounds.md` «Fix sorvoli end-to-end».
- **Parità livello** (`TransferPoint.Parity`, enum `LevelParity`): resa nel `LevelText` via
  `LevelFormatting.Format(...,parity)` → propaga a tutte le viste + frase senza rami duplicati.

## 7. Aggiornamenti successivi (2026-07-22) — condizione operativa (pista/area)
- **Livelli variabili per pista in uso / area attiva** (modello **editoriale**: varianti etichettate, non
  calcolate live). Campi additivi su `TransferPoint`: `ConditionKind {None,Runway,Area,Custom}` + `ConditionLabel`
  (verità display, denormalizzata) + `ConditionRefId` (soft-ref a `AirportRunwayRule`/`SpecialArea`, no FK).
  Migrazione `AddTransferPointCondition`. Dettaglio schema: `spec/modello-dati.md` §9.20.
- **Clausola frase** dallo slot `Condition` del template (`CoordinationSentenceComposer`, IT/EN), appesa a fine
  frase; `AppCoordRow.ConditionLabel` reso come colonna condizionale (ACC/APP/vLOA) e pill nella Ridotta.
- **Editor** (`AdminTrasferimentiPage`): selettore condizione nel form riga (kind + label con datalist delle
  config pista dell'aeroporto del flusso, via `IAirportEditingService.LoadForViewAsync`); batch multi-CoP e
  clona propagano la condizione. Validazione soft: kind ≠ None richiede una label.
- Additivo (nessun rename → nessuna propagazione distruttiva). Test: composer (clausola IT/EN), derivation
  (label su riga+frase), EF round-trip (`TransferRepositoryTests`). Suite verde (19 dom + 200 app + 174 infra).

### 7.1 Estensione condizione (2026-07-22, stessa sessione) — multi-pista + area in AND
- **Stessa condizione su più piste** in una sola riga: `ConditionLabel` con `Runway` può elencarle («16R / 16L»).
  Editor: **multi-select** delle **piste reali** (`AirportRunways`, non le config `AirportRunwayRule`) dell'aeroporto
  del flusso. Fix collegato: la condizione «Pista» ora legge `d.Runways` (prima leggeva `d.Rules` → vuoto senza
  regole editoriali; es. LIBD aveva piste ma 0 regole).
- **Pista + area in AND**: nuovo campo `ConditionAreaLabel` (overlay valido solo con `Kind=Runway`) → frase
  «… con pista X in uso **e** Y attiva» (slot template `RunwayAndArea`, IT/EN). Migrazione
  `AddTransferPointConditionArea`. Etichetta combinata per il display via `TransferConditionText.Display`
  (`TransferPointRow.ConditionDisplay`), usata da pill admin + `AppCoordRow.ConditionLabel` (ACC/APP/vLOA/Ridotta).
- vLOA ora porta anch'essa la condizione (prima `VloaDerivationService` la ometteva). Test: composer multi-pista +
  combinato IT/EN + degrado area-sola. Suite verde (204 app + 174 infra).

### 7.2 Condizioni indipendenti + area ricercabile (2026-07-22, stessa sessione)
Su richiesta: pista / area / personalizzata devono essere **indipendenti** (una riga può averle tutte), non un tipo
singolo. **Rimosso `ConditionKind` + enum `TransferConditionKind`**; la condizione è ora **tre colonne indipendenti**:
- `ConditionLabel` (pista/e) · `ConditionAreaLabel` (area) · **`ConditionCustomLabel`** (personalizzata, nuova). Migrazione
  `SplitTransferConditionColumns` (drop `ConditionKind`, add `ConditionCustomLabel`, backfill Area/Custom→colonne).
- **Editor**: la colonna «Condizione» diventa **tre colonne** (Pista multi-select · Area · Personalizzata). L'**area** è
  ora un **picker con ricerca a digitazione** (typeahead sul nome, stile picker settori), non più una `<select>`.
- **Frase**: il composer compone la clausola di ogni dimensione presente e le unisce con `Condition.Join` («e»/«and»);
  pista+area usano la forma dedicata `RunwayAndArea`. `ConditionDisplay` = «pista · area · personalizzata».
- Test: composer (tre insieme, area+custom, join), EF round-trip (colonne indipendenti, ref solo con pista). Suite verde
  (19 dom + 205 app + 174 infra).

### 7.3 Stato verticale disaccoppiato dal vincolo (2026-07-24)
Su richiesta: la parola `{stato}` («in discesa/salita/stabile») nella frase **derivava dal `LevelConstraint`**
(≤→discesa, ≥→salita, esatto→stabile). Sbagliato: `≥` è un **bound di livello** («a 130 o superiore»), non implica
una salita. Ora lo stato verticale è una **dimensione indipendente**.
- **Nuovo enum** `TransferVerticalState { Unspecified, Level, Descending, Climbing }` + campo `TransferPoint.VerticalState`.
  Il composer sorgente `{stato}` da questo campo (non più dal constraint); `Unspecified` → **nessuna parola** (frase col
  solo bound: «… con destinazione LIBD a livello 130 o livello inferiore su PISIP»). Il `{fl}` resta guidato dal constraint.
- **Rinomina** `CoordinationSentenceState.{AtOrBelow,AtOrAbove,Exact,Special}` → `{Descending,Climbing,Level}` (nomi che
  ora dicono lo stato, non il vincolo); propagata a `CoordinationSentenceOptions.StateWords` + chiavi JSON
  `content/coordination-sentence.json` (`descending/climbing/level`).
- **Migrazione** `AddTransferPointVerticalState` (colonna TEXT, default `Unspecified`) con **backfill da constraint**
  (≤→Descending, ≥→Climbing, esatto→Level) → le frasi esistenti restano identiche. Seed demo idem (`VStateFrom`).
- **Editor** (`AdminTrasferimentiPage`): nuova `<select>` «Stato verticale» nei form add/edit riga (risorse `Xfer_VState*`).
- **Convenzione display** (`LevelFormatting.Format`, usato in tabella trasferimenti + tutte le viste coordinamenti): le
  **frecce `↑`/`↓`** indicano ora lo **stato verticale** (salita/discesa), il **vincolo di livello** usa i segni
  **`+`** (≥) / **`-`** (≤). Es. `FL290+ ↑ (dispari)`. Prima le frecce indicavano il vincolo. Le opzioni del
  selettore vincolo mostrano `≤ (-)` / `≥ (+)`; le opzioni stato verticale mostrano `↓ In discesa` / `↑ In salita`.
- Test: composer (stato scelto a mano IT/EN, regressione «constraint senza stato»), EF round-trip. Suite verde
  (450 tot, 0 warning).
