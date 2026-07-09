# 09 — Flusso di pubblicazione (punto 10) 🟢🟡

> Pubblicazione con sezioni live + sezioni che dipendono dalla versione, tramite
> snapshot AIRAC (`DocRelease`). Obiettivo utente: **flusso generico** — un nuovo tipo
> di documento deve poter usare questo meccanismo senza reimplementarlo. Dipende da: doc 08.

## 1. Stato attuale

**Risposta alla domanda chiave**: il meccanismo è **generico nel cuore, con adapter
per-tipo ai due estremi**. Un'unica entità `DocRelease` (una tabella) keyed su
`ReleaseTargetType {Vloa, AccVipi, App, Airport}`, un servizio generico
(`ReleaseService`) per schedule/effective/status/diff/cancel. La logica type-specific
è isolata in **2 dispatch**: snapshot/auth (`EfReleaseRepository`) e preview/load nei viewer.
→ né del tutto generico né del tutto duplicato: spina condivisa + 4 innesti.

**Nota**: convivono **due sistemi di versioning**:
- **Legacy per-documento**: `Document`+`DocumentVersion` (Draft/Published/Archived) + `CurrentVersionId`. Solo vLOA + Airport. UI "Pubblica versione".
- **Release AIRAC unificato**: `DocRelease` snapshot, sopra **tutti** e 4 i tipi. UI "Pubblica al ciclo / ora".

| File:riga | Classe/tipo | Ruolo |
|---|---|---|
| `Vipi.Domain/Enums.cs:19` | `ReleaseTargetType` | Discriminatore generico — i 4 tipi si innestano qui. |
| `Vipi.Domain/Enums.cs:22` | `ReleaseStatus {Scheduled,Effective,Superseded}` | Ciclo di vita release. |
| `Vipi.Domain/Entities/Documents.cs:79` | `DocRelease` | Entità release unica: `TargetType`+`TargetKey`, `VersionNumber`, `ReleaseAiracCycle`, `ReleaseEffectiveUtc`, `Status`, `PayloadJson` (shape per tipo). |
| `Vipi.Domain/Services/AiracService.cs` | `AiracService` | Matematica AIRAC: `GetCycle`, `NextCycles`, `EffectiveUtcForCycle`. |
| `Vipi.Application/Content/ReleaseService.cs:66` | `ReleaseService : IReleaseService` | Core generico: `PublishAsync`, `PublishNowAsync`, `CancelReleaseAsync`, `DiffAsync`, `GetPreviewAsync`, `GetLocationAsync`, `ListAsync`, `CurrentCycle`, `UpcomingCycles`. |
| `Vipi.Application/Content/ReleaseService.cs:169` | `Signature` (privato) | **switch per-tipo** per il diff. |
| `Vipi.Application/Content/ReleasePayload.cs:8,27` | `DocReleasePayload` (Vloa/Airport), `AppReleaseSnapshot` (App) | Shape snapshot. ACC non ha payload dedicato: `PayloadJson` = raw `List<AccBlock>`. |
| `Vipi.Application/Content/ManagedDoc.cs:14` | `ManagedDoc`, `ManagedDocKind`, `ManagedDocRef`, `ReleaseSummary` | Astrazione lista unificata per `/vsop/versioni`. |
| `Vipi.Application/Content/DocumentAdminService.cs:18` | `DocumentAdminService` | `ListAsync`/`SetHiddenAsync`/`DeleteAsync` su `ManagedDocRef` (hide/delete/annulla). |
| `Vipi.Application/Content/AccProfileService.cs:99` | `LoadForReleaseAsync` + `AccReleaseView` | Innesto ACC. |
| `Vipi.Application/Content/AppProfileService.cs:102` | `LoadForReleaseAsync` + `AppReleaseView` | Innesto APP. |
| `Vipi.Infrastructure/Persistence/EfReleaseRepository.cs:14` | `EfReleaseRepository` | **Dispatch principale**: `SnapshotWorkingAsync` (`:19`, switch 4-vie), `GetAuthAccCodeAsync` (`:164`, switch); `SaveReleaseAsync`/`RecomputeStatuses`/`CancelAsync` generici. |
| `Vipi.Infrastructure/Persistence/EfContentRepository.cs:73` | `LoadVipiAsync` | Live-vs-release per doc-based; `ignoreRelease`/`preferWorking` per draft preview. |
| `Vipi.Infrastructure/Persistence/EfDocumentAdminRepository.cs` | `EfDocumentAdminRepository` | **Altro dispatch**: `ListAsync` (4 rami), `SetHiddenAsync`/`GetAccCodeAsync`/`DeleteAsync` (switch). |
| `Vipi.Ui/Shared/PreviewMode.cs:19` | `PreviewMode` / `PreviewKind {Public,Draft,Release}` | Parser uniforme `?as=` (assente→Public, `as=draft`→Draft, `as=rel:{id}`→Release; alias legacy `?live=1`). |
| `Vipi.Ui/Components/PreviewBanner.razor` | `PreviewBanner` | Banner preview condiviso. |
| `Vipi.Ui/Pages/VersioniPage.razor:1` | `/vsop/versioni` (+ `/vsop/{Acc}/versioni`) | Entry admin; lista unificata 4 tipi; publish versione/release, diff, cancel, hide, delete; `PreviewLink` (`:588`)/`EditorLink` (`:653`) switch per-kind. |
| `Vipi.Ui/Components/ReleasePanel.razor` | `ReleasePanel` | Timeline+publish riusabile, param `ReleaseTargetType Target` + `Key`. |
| `Vipi.Ui/Pages/ReleasePreviewPage.razor:1` | `/vsop/release/{Id}` | Redirect legacy a viewer tipizzato con `?as=rel:{id}`; switch URL (`:47`). |
| Viewer | `AccVipiPage.razor:121`, `AppnPage.razor:177`, `AeroportoPage.razor:422`, `VloaListPage.razor:113` | Ognuno chiama `PreviewMode.Parse` + il proprio `LoadForRelease/View`. |

## 2. Problemi

1. **Stesso switch 4-vie duplicato in ~6 punti** (obiettivo generico dell'utente NON ancora
   raggiunto): (a) `EfReleaseRepository.SnapshotWorkingAsync`, (b) `.GetAuthAccCodeAsync`,
   (c) `ReleaseService.Signature`, (d) `EfDocumentAdminRepository` (4 switch), (e) URL builder
   UI (`VersioniPage.PreviewLink`/`EditorLink`, `ReleasePreviewPage`, `ReleasePanel`),
   (f) preview-load nei 4 viewer. Aggiungere un tipo = toccare tutti e 6.
2. **Due sistemi di versioning** convivono (`DocumentVersion` legacy + `DocRelease`) → confusione
   su "cosa è pubblicato".
3. **Asimmetria payload**: Vloa/Airport usano `DocReleasePayload`/`RawDocument` (sopra il
   legacy `DocumentVersion`); Acc/App sono profile-based con snapshot propri. I "4 tipi" sono
   in realtà **2 doc-based + 2 profile-based** → conseguenza diretta del doppio modello (doc 08).
4. **File multi-classe**: `Documents.cs` (8 tipi), `ReleaseService.cs` (record + interfaccia +
   impl), `ReleasePayload.cs` (3 payload), `ManagedDoc.cs` (4 tipi).

## 3. Architettura target

> 🟡 BOZZA — dipende dalle decisioni del doc 08.

Obiettivo: **flusso di pubblicazione polimorfico, un tipo = un descrittore**.

- Definire un contratto per-tipo unico — es. `IReleasableDocumentType` con i metodi che
  oggi sono gli innesti: `Snapshot(key)`, `AuthAccCode(key)`, `Signature(payload)`,
  `PreviewLocation(key, releaseId)`, `Load(key, previewMode)`, `ListManaged()`. Registrare
  un'implementazione per tipo; `ReleaseService`/repository/UI **iterano il registry** invece
  di switchare.
- Aggiungere un nuovo tipo di documento = registrare un descrittore, **zero switch toccati**.
- Convergere/astrarre i due sistemi di versioning (coordinato con doc 08): idealmente
  `DocRelease` come unico meccanismo, `DocumentVersion` assorbito o chiaramente relegato a draft.
- Estrarre i tipi dai file multi-classe.

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Definire `IReleasableDocumentType` coprendo i 6 innesti attuali.
2. Implementarlo per un tipo (es. App) e far consumare al `ReleaseService` il descrittore.
3. Migrare gli altri 3 tipi; rimuovere gli switch a uno a uno.
4. Sostituire gli switch UI con lookup sul registry.
5. Risolvere la convivenza `DocumentVersion`/`DocRelease`.

## 5. Impatto

- **Dipende da** doc 08 (modello documento). Ultimo della sequenza.
- **A valle**: nessuno — è la cima dell'asse B. Ma è il test dell'obiettivo utente:
  "nuovo tipo di documento senza reimplementare la pubblicazione".
- **Verifica**: aggiungere un tipo fittizio di documento e pubblicarlo/preview/diff/cancel
  senza modificare `ReleaseService`, i repository o gli switch UI (solo registrando un descrittore).
