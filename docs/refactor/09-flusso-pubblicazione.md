# 09 — Flusso di pubblicazione (punto 10) 🟢

> Pubblicazione con sezioni live + sezioni che dipendono dalla versione, tramite
> snapshot AIRAC (`DocRelease`). Obiettivo utente: **flusso generico** — un nuovo tipo
> di documento deve poter usare questo meccanismo senza reimplementarlo. Dipende da: doc 08.

## 1. Stato attuale (ri-mappato post-08, 2026-07-10)

> **[Superato in parte — doc 10, 2026-07-18]** Il **doc 10** («Snapshot totale + RenderMode») ha reso lo
> snapshot **totale** (congela anche l'output delle sezioni derivate `Frozen` in `DocReleasePayload.FrozenSections`)
> e ha **rimosso l'overlay di visibilità separato** (`VloaOverlaySnapshot`, `DocReleasePayload.Vloa`,
> `IReleaseTarget.IncludesVisibilityOverlay` e il ramo overlay in `SnapshotWorkingAsync`, §S5). Dove sotto
> si legge «overlay `Vloa/App` da `DocumentProfile`» / «overlay opzionale», quel ramo **non esiste più**: la
> visibilità è dentro la fotografia congelata.

**Doc 08 ha già collassato il cuore.** Tutti e 4 i tipi (`Vloa`, `AccVipi`, `App`, `Airport`)
sono ora su modello unificato `Document`+`DocumentVersion`. Il ramo di snapshot/preview/firma
è **uniforme**: un solo `DocReleasePayload` (`RawDocument` congelato + overlay opzionale).
Quello che il §1 originale descriveva come «spina + 4 innesti» è ora **spina generica + switch
vestigiali (1 ramo) + un residuo per-tipo genuino di sola identità/routing**.

**Due strati di versioning** (non più «due modelli»): `DocumentVersion` (Draft/Published/Archived,
lifecycle bozza→pubblicato, UI "Pubblica versione") **sotto**, `DocRelease` (snapshot AIRAC per
ciclo) **sopra**. Ora entrambi universali su tutti e 4 i tipi (post-08). Design pulito a 2 livelli.

### 1a. Switch GIÀ collassati (post-08, 1 solo ramo Document — vestigiali)
| File:riga | Metodo | Stato |
|---|---|---|
| `EfReleaseRepository.cs:19` | `SnapshotWorkingAsync` | 4 case → 1 corpo. Unica branch residua: overlay `Vloa/App` da `DocumentProfile` (ACC=blockmeta, Airport=nessuno). |
| `ReleaseService.cs:169` | `Signature` | 1 case per tutti e 4 (`DocReleasePayload`→`FlattenSections`). |
| `ReleaseService.cs:143` | `GetPreviewAsync` | 1 condizione per tutti e 4. |
| `EfDocumentAdminRepository.cs:93` | `SetHiddenAsync` | tutti e 4 → `Document.IsHidden`. |
| `EfDocumentAdminRepository.cs:110` | `DeleteAsync` (corpo) | tutti e 4 → rimuovi `Document`. Solo il mapping `Kind→ReleaseTargetType` (`:113`) resta. |

### 1b. Residuo per-tipo GENUINO (chiavi + rotte diverse per tipo)
| File:riga | Metodo | Natura | Nota |
|---|---|---|---|
| `EfReleaseRepository.cs:183` | `ResolveDocumentIdAsync` | key→docId | 4 formati chiave diversi |
| `EfReleaseRepository.cs:129` | `GetAuthAccCodeAsync` | key→accCode | |
| `EfDocumentAdminRepository.cs:71` | `GetAccCodeAsync` | key→accCode | **DUPLICA `GetAuthAccCodeAsync`** |
| `EfDocumentAdminRepository.cs:15` | `ListAsync` | shape(Document)→`ManagedDoc` | identità inversa (doc→kind+key+scope) |
| `VersioniPage.razor:588` | `PreviewLink` | (kind,key,rel)→URL viewer | |
| `ReleasePreviewPage.razor:47` | url switch | (type,key,rel)→URL viewer | **DUPLICA la conoscenza rotta-viewer di `PreviewLink`** |
| `VersioniPage.razor:653` | `EditorLink` | (kind,key)→URL editor | |

**Chiave del payload** (`DocRelease.TargetKey`) per tipo: Vloa=`docId`; App=`callsign`;
Airport=`icao`; AccVipi=`{acc}|{root}`.

### 1c. Anagrafica tipi/servizi generici (invariati)
| File:riga | Tipo | Ruolo |
|---|---|---|
| `Vipi.Domain/Enums.cs` | `ReleaseTargetType {Vloa,AccVipi,App,Airport}`, `ReleaseStatus` | Discriminatore + lifecycle release. |
| `Vipi.Domain/Entities/Documents.cs:79` | `DocRelease` | Entità release unica (`TargetType`+`TargetKey`, `PayloadJson`, ecc.). |
| `Vipi.Domain/Services/AiracService.cs` | `AiracService` | Matematica AIRAC. |
| `Vipi.Application/Content/ReleaseService.cs:66` | `ReleaseService` | Core generico (publish/cancel/diff/preview/list/summaries). |
| `Vipi.Application/Content/ReleasePayload.cs` | `DocReleasePayload`, `VloaOverlaySnapshot` | Shape snapshot unificato. |
| `Vipi.Application/Content/ManagedDoc.cs` | `ManagedDoc`, `ManagedDocKind`, `ManagedDocRef`, `ReleaseSummary` | Astrazione lista `/vsop/versioni`. |
| `Vipi.Infrastructure/.../EfReleaseRepository.cs` | `EfReleaseRepository` | Save/List/Effective/Cancel/Summaries generici + i residui 1b. |
| `Vipi.Ui/Shared/PreviewMode.cs` | `PreviewMode`/`PreviewKind` | Parser `?as=` uniforme (già generico). |
| `Vipi.Ui/Components/ReleasePanel.razor` | `ReleasePanel` | Timeline+publish riusabile (param `Target`+`Key`, già generico). |

## 2. Problemi (residui reali)

1. **`key→accCode` duplicato in 2 file** (`EfReleaseRepository.GetAuthAccCodeAsync` e
   `EfDocumentAdminRepository.GetAccCodeAsync`): stessa logica per-tipo, due implementazioni.
2. **Conoscenza rotta-viewer duplicata**: `VersioniPage.PreviewLink` e `ReleasePreviewPage`
   sanno entrambi «tipo → URL viewer con `?as=rel:{id}`».
3. **Aggiungere un tipo = toccare più punti** (1b): resolve-docId, auth-acc (×2), list-shape,
   preview-link, editor-link. Obiettivo utente («nuovo tipo senza reimplementare») NON ancora
   pienamente raggiunto: il cuore è generico ma identità+routing è sparso a switch.
4. **File multi-classe**: `ReleasePayload.cs` (2 tipi), `ManagedDoc.cs` (4 tipi),
   `ReleaseService.cs` (record `ReleaseDiffRow`/`ReleaseDiff`/`ReleasePreview`/`ReleaseLocation`
   + interfaccia + impl).

## 3. Architettura target (APPROVATA — opzione B, registry polimorfico) 🟢

**Obiettivo: un tipo = un descrittore.** Nessun `switch (type)` residuo; motori generici che
**iterano/consultano un registry**. Poiché identità e routing vivono in layer diversi (DB vs UI),
il descrittore è **stratificato in due porte** (evita che una porta Application ritorni URL o che
la UI faccia accesso DB — invariante #6 di layering):

### 3a. `IReleaseTarget` — porta Application, impl in Infrastructure, una per `ReleaseTargetType`
Copre l'identità DB-side. Il motore generico (`EfReleaseRepository`) lo consulta invece di switchare.
```csharp
public interface IReleaseTarget
{
    ReleaseTargetType Type { get; }
    Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct);   // key → docId
    Task<string?> AuthAccCodeAsync(string key, CancellationToken ct);       // key → accCode (authz)
    VloaOverlaySnapshot? BuildOverlay(DocumentProfileData? profile);        // overlay o null (Vloa/App=sì, ACC/Airport=no)
    bool TryDescribe(Document doc, out ManagedDoc managed);                 // shape(Document) → ManagedDoc (per ListAsync)
}
```
- Le 4 impl (`VloaReleaseTarget`, `AppReleaseTarget`, `AirportReleaseTarget`, `AccVipiReleaseTarget`)
  vivono in Infrastructure (hanno bisogno di `VipiDbContext`), registrate in DI come
  `IEnumerable<IReleaseTarget>` + risolte per `Type` da un `ReleaseTargetRegistry` sottile.
- `EfReleaseRepository.SnapshotWorkingAsync` / `.GetAuthAccCodeAsync` → delegano al descrittore.
  `EfDocumentAdminRepository.GetAccCodeAsync` → **stesso** descrittore (elimina il duplicato).
  `EfDocumentAdminRepository.ListAsync` → itera i descrittori con `TryDescribe` (niente shape-switch).

### 3b. `IDocKindRoutes` — porta UI, una per `ManagedDocKind`
Copre il routing viewer/editor. `VersioniPage` e `ReleasePreviewPage` la consultano.
```csharp
public interface IDocKindRoutes
{
    ManagedDocKind Kind { get; }
    string ViewerUrl(string acc, ManagedDoc d, int releaseId);  // → "/vsop/.../?as=rel:{id}"
    string EditorUrl(string acc, ManagedDoc d);                 // → editor del tipo
}
```
- Registrate in DI UI come `IEnumerable<IDocKindRoutes>` + `DocRoutesRegistry`.
- `ReleasePreviewPage` mappa `ReleaseTargetType→ManagedDocKind` (1:1) e usa `ViewerUrl`
  (rimuove il duplicato di rotta-viewer). `VersioniPage.PreviewLink/EditorLink` → lookup.

### 3c. Cuore generico ripulito
- Rimossi gli `switch` vestigiali (1a) sostituendoli con il ramo unico (già di fatto tale) o
  con la delega al descrittore dove serve un dato per-tipo (overlay).
- `Kind↔ReleaseTargetType` = mappa unica (1:1), un solo posto.

### 3d. Split file multi-classe
`ReleasePayload.cs`, `ManagedDoc.cs`, i record di `ReleaseService.cs` → un tipo per file
(coerente con doc 01-08).

**Non-obiettivi**: NON si tocca `DocumentVersion` vs `DocRelease` (già 2 strati puliti post-08);
NON si cambia lo schema DB; NON si cambiano rotte pagine (solo dove sono costruite).

## 4. Passi di migrazione (APPROVATO) 🟢

Ordine: meccanico → registry DB-side → registry UI-side → verifica tipo fittizio.

1. **(meccanico)** Split `ReleasePayload.cs`/`ManagedDoc.cs`/record di `ReleaseService.cs` in
   file singoli. Commit puro, nessuna logica. `refactor(09): split tipi multi-classe — doc 09 §3d`.
2. **`IReleaseTarget` + registry + 4 impl** in Infrastructure; DI. `EfReleaseRepository` e
   `EfDocumentAdminRepository` delegano (`ResolveDocumentId`/`AuthAccCode`/`BuildOverlay`/`TryDescribe`).
   Rimuove i 4 switch DB-side + il duplicato `GetAccCodeAsync`. **Test-first**: caratterizzazione
   su `ResolveDocumentId`/`AuthAccCode` per i 4 tipi PRIMA dello spostamento (rete anti-regressione).
   `refactor(09): registry IReleaseTarget, delega snapshot/auth/list — doc 09 §3a`.
3. **`IDocKindRoutes` + registry + 4 impl** in UI; DI. `VersioniPage.PreviewLink/EditorLink` e
   `ReleasePreviewPage` → lookup. Rimuove i 3 switch UI + il duplicato rotta-viewer.
   `refactor(09): registry IDocKindRoutes, rotte viewer/editor — doc 09 §3b`.
4. **Pulizia switch vestigiali** (1a): `Signature`/`GetPreviewAsync`/`SetHiddenAsync`/`DeleteAsync`
   corpo → ramo unico esplicito; `Kind↔ReleaseTargetType` mappa unica.
   `refactor(09): rimuovi switch vestigiali release — doc 09 §3c`.
5. **Verifica obiettivo utente** (§5): tipo fittizio pubblicabile registrando solo 2 descrittori.

## 4bis. Esito implementazione (Fase 2, branch `refactor/09-flusso-pubblicazione`)

- **passo 1 ✅** (`1acc4ae`) split file multi-classe (§3d).
- **rete test-first ✅** (`052443c`) caratterizzazione identità release/admin (+9, 252→261).
- **passo 2 ✅** (`d497278`) `IReleaseTarget`+`ReleaseTargetRegistry`+4 impl; `EfReleaseRepository`
  (snapshot/auth) e `EfDocumentAdminRepository` (list/getacc) delegano; rimosso `ResolveDocumentIdAsync`
  privato e i duplicati key→acc (§3a).
- **passo 3 ✅** (`10b1b2a`) `IDocKindRoutes`+`DocRoutesRegistry`+4 impl (Vipi.Ui); `VersioniPage`
  `PreviewLink`/`EditorLink` e `ReleasePreviewPage` fanno lookup; rimossi 3 switch URL (§3b).
- **passo 4 ✅** pulizia switch vestigiali `Signature`/`GetPreviewAsync`/`SetHidden`/`Delete` (§3c).
- **passo 5 ✅** `ReleaseGenericFlowTests` (+3): tipo fittizio (enum 99) pubblica/preview/diff/list/authz
  registrando SOLO un descrittore, zero modifiche ai motori (§5). Baseline test = **264**.
- **Fase 3 live ✅** (2026-07-10, CDP su DB reale ACC+APP): EditorLink ACC/APP aprono la pagina giusta;
  publish-now ACC → release 2607; `ViewerUrl(?as=rel:1)` rende lo snapshot congelato (banner AIRAC 2607);
  redirect `/vsop/release/1`→`/vsop/libb/vipi?as=rel:1` (ReleasePreviewPage via registry); SetHidden
  hide+unhide (Document.IsHidden, verificato in DB). Airport/vLOA non in DB ma coperti dai char test
  (DocumentAdminRepositoryTests semina tutti e 4); delete non guidato (distruttivo) ma ramo unico coperto
  dal fake-type test. Release di test rimossa a fine verifica.
- **Fase 4 ✅** chiusura: overview/rounds aggiornati, merge su main.

## 5. Impatto

- **Dipende da** doc 08 (fatto). Ultimo della sequenza. Cima dell'asse B, nessuno a valle.
- **Verifica (test dell'obiettivo utente)**: aggiungere un `IReleaseTarget`+`IDocKindRoutes`
  fittizi e provare publish/preview/diff/cancel del tipo fittizio **senza modificare**
  `ReleaseService`, `EfReleaseRepository`, `EfDocumentAdminRepository` né gli switch UI —
  solo registrando i 2 descrittori. Un test di integrazione può coprire il ramo DB-side
  (snapshot/auth/list del tipo fittizio) senza UI.
- **Schema/rotte**: invariati → niente snapshot spec in Fase 1.
- **Logging** (#7): nessun logging esistente da perdere (release path non logga oggi); non
  introdurre swallow silenzioso nei descrittori.
