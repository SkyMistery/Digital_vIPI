# 08 — Modello documento + editing (punti 9+12) 🟢🟡 (programma: 08a–08f)

> **DECISIONE Fase 0 (2026-07-09)**: unificazione greenfield su `Document` + `SectionCatalog`,
> profile eliminato, dati vecchi cancellati (no migrazione conversione), test-first.
> Decomposto in 6 sotto-giri 08a–08f (§4), ognuno branch/PR con la sua Fase 0–4.

> **Il cuore del casino.** Convivono due modelli di documento incompatibili, e le
> definizioni di sezione (AoR/Freq/Coord…) sono legate al singolo tipo invece di
> essere condivise. Qui vive il dolore del punto 12 dell'utente. Dipende da: —
> (asse B, isolato dall'asse A a design-time).

## 1. Stato attuale

### I due modelli che coesistono

| Modello | Struttura | Usato da | Definizione sezioni |
|---|---|---|---|
| **Classic** | `Document → DocumentVersion → DocumentSection → ContentBlock`, enum **condiviso** `BlockSection` | **vLOA** | enum `BlockSection` (parzialmente condiviso) + `VloaSections.Canonical` |
| **Profile** | JSON blob (`AccProfile`, `AppProfile`, `AirportProfile`) | **ACC vIPI, APP vIPI, Airport** | registry **per-tipo** in codice (`AppSections`, `AccSections`) |

### Creazione documento (punto 9)

| File:riga | Classe/membro | Ruolo |
|---|---|---|
| `Vipi.Domain/Enums.cs:13` | `DocumentType { Vipi, Vloa }` | Tipo classic doc. |
| `Vipi.Domain/Enums.cs:19` | `ReleaseTargetType { Vloa, AccVipi, App, Airport }` | La vera tassonomia a 4 vie. |
| `Vipi.Domain/Entities/Documents.cs:4` | `Document` | Classic doc: Type, Title, Language, Status, versioni, edit lock (`:22-25`). |
| `Vipi.Domain/Entities/Anagrafica.cs:439,378` | `AccProfile`, `AppProfile` | Profile ACC/APP (blocchi JSON). |
| `Vipi.Application/Content/EditingService.cs:28` | `IEditingService.CreateDocumentAsync` | Entry creazione classic Document (vIPI o vLOA Home/Neighbour); impl `:103`. |
| `Vipi.Ui/Pages/NewDocumentPage.razor:1` | route `/vsop/editor/newdoc` | Entry principale; 3 kind `AccVipi`/`AppVipi`/`Vloa` (`:165`); `CreateDocumentAsync` (`:337`). |
| `StructureEditingService.cs:193-245` | `GenerateAirportDocumentAsync` / `EnsureAirportDocumentSystemAsync` | Airport doc **generato da sorgente**, non da NewDocumentPage. |

### Editing e definizioni di sezione (punto 12)

| File:riga | Classe/membro | Ruolo |
|---|---|---|
| `Vipi.Domain/Enums.cs:49` | `BlockSection` enum | Semantica sezione **condivisa** del modello classic (Aor, Frequencies, Coordination…). |
| `Vipi.Application/Content/EditingService.cs:34,176` | `AddSectionAsync(...)` | Aggiunge sezione classic; multi-classe (anche `EditNotAllowedException` `:49`, `EditConflictException` `:55`). |
| `Vipi.Application/Content/VloaSections.cs:19` | `VloaSections` | Struttura canonica vLOA `Canonical()` (`:22`) + `MandatoryTitles()` (`:64`); multi-classe (`VloaBlockSpec` `:7`, `VloaSectionSpec` `:10`). |
| `Vipi.Application/Content/AppSections.cs:11,14,17` | `AppSectionDescriptor`, `AppSections`, `.All` | Registry **APP** fisso 6 sezioni; `AppSectionKind {Editorial,Derived}` (`:4`); `Reconcile` (`:42`); multi-classe. |
| `Vipi.Application/Content/AccSections.cs:9,12,24,35` | `AccSections`, `Aerovia`, `AppBlock`, `For(AccBlockKind)` | Registry **ACC** — due set per-block-kind; riusa `AppSectionDescriptor`; `Reconcile` (`:49`). |
| `Vipi.Application/Content/AccProfileModels.cs:7,38,110-131` | `AccBlockKind`, `AccBlock`, `AccAorView`/`AccSectorAor`/`AccConfigAor` | Modello blocco ACC + custom section + view AoR; multi-classe. |
| `Vipi.Application/Content/AppProfileModels.cs:10,68,73` | `AppSeparationRow`, `AppCustomBlock`, `AppCustomSection` | Righe editoriali + custom section (condivisi APP/ACC). |
| `Vipi.Ui/Components/App/SectionShell.razor:17,53` | `SectionShell`, `Kind` | Chrome sezione condiviso (Editorial vs Derived), reorder/hide/delete — usato da ACC & APP. |
| `Vipi.Ui/Pages/AppEditorPage.razor:1,80,512` | route `/vsop/{Acc}/apps/editor` | Editor APP; kind da `AppSections.Find`; "Nuova sezione" → `AppCustomSection`. |
| `Vipi.Ui/Pages/AccEditorPage.razor:1,101,357` | route `/vsop/{Acc}/editor` | Editor ACC; `AccSections.For(b.Kind)`; render via `SectionShell`; "Nuova sezione". |
| `Vipi.Ui/Components/VloaEditor.razor:199` | `AddSection` → `BlockSection.Other` | Editor vLOA; lock sezioni mandatory; AoR/Freq/Coord derivati per `SectionKind`. |

### AoR (derivata, mai salvata)
Definita come entry di sezione in ogni registry (`AppSections` key `"aor"`, `AccSections`
key `"aor"`, `BlockSection.Aor` per vLOA). Derivazione/render: `Vipi.Application/Aor/AorService.cs`,
`AorPolygonProjector.cs`, `CircleShapeBuilder.cs`, `PolygonGeometry.cs`; view
`AccAorView`/`AccSectorAor`/`AccConfigAor`; componenti `AorBlock.razor`, `AppAor.razor`, `AccAor.razor`.

## 2. Problemi

1. **Due modelli documento incompatibili** (classic vs profile) → root cause. vLOA/Airport
   usano `Document`+`DocumentVersion`; ACC/APP usano JSON blob. Ogni feature va implementata
   due volte.
2. **Definizioni di sezione per-tipo, non condivise** (il punto 12 esplicito): `AppSections.All`
   (solo APP), `AccSections.Aerovia`/`AppBlock` (solo ACC), `BlockSection`+`VloaSections`
   (solo vLOA). Cambiare "AoR mostra CS invece dei nomi" richiede modifiche in N posti.
3. **"Nuova sezione" dipende dal documento**: ogni editor offre solo le proprie opzioni
   (`AccEditorPage` → `AppCustomSection`, `VloaEditor` → `BlockSection.Other`). L'utente vuole
   un catalogo unico di sezioni disponibili per tutti.
4. **File multi-classe**: `EditingService.cs`, `VloaSections.cs`, `AppSections.cs`,
   `AccProfileModels.cs`, `AppProfileModels.cs`, `Documents.cs` (8 tipi).
5. **Airport doc creato in modo diverso** (da sorgente, dentro `StructureEditingService`)
   vs gli altri (da `NewDocumentPage`) → percorso di creazione non uniforme.
6. **Link orfano** (memoria `vloa-editor-routing-todo`): `NewDocumentPage` naviga a
   `/vsop/{acc}/editor?doc={id}` ma `AccEditorPage` edita l'`AccProfile` per codice e
   ignora `?doc` → i due sistemi non sono giuntati.

## 3. Architettura target

> ✅ APPROVATA — Fase 0, 2026-07-09. **Decisione strategica: UNIFICAZIONE greenfield su
> modello classic + `SectionCatalog`.** Programma decomposto in sotto-giri (08a–08f).

### Contesto reale (verificato sul codice)
Il modello **classic** (`Document`/`DocumentVersion`/`ContentBlock`/`BlockSection`) NON è
"solo vLOA": è la **spina dorsale** di versioning, release AIRAC, edit-lock, **search** e
**publish**. `AppSections` e `AccSections` sono quasi identici (`AppSectionDescriptor` condiviso,
`Reconcile` **duplicato verbatim**). I profile (JSON blob) sono un modello di *editing* recente
che si proietta comunque in un `DocumentView` classic al render/publish.

### Decisioni approvate
- **Unificare su `Document` generalizzato** con le sezioni guidate da un **`SectionCatalog`
  condiviso** (chiavi stringa + descrittore + `SectionKind` Editorial/Derived), sostituendo
  l'enum rigido `BlockSection`. Versioning/publish/search restano nativi del classic. ACC/APP/
  Airport editano direttamente `Document`+sezioni; il modello **profile viene eliminato**.
- **Greenfield sui dati**: i documenti esistenti si **cancellano** (migrazione di *rimozione*,
  non di conversione) → nessuna migrazione dati rischiosa. Il contenuto derivato si rigenera
  dagli import; l'editoriale a mano è accettabilmente perso (fase pre-produzione).
- **Test-first** (invariante #8): la rete di test oggi manca → si scrive **prima** di riscrivere.
- **Creazione uniforme**: un unico `CreateDocumentUseCase` su `ReleaseTargetType` (incl. Airport;
  rimuove la creazione speciale da `StructureEditingService`, ripresa da doc 03).
- **`SectionCatalog` risolve il punto 12**: sezione definita una volta (AoR uniforme) + "Nuova
  sezione" offre il catalogo completo. Con un solo storage, aggiungere una sezione **editoriale**
  nuova = un solo percorso (il vantaggio chiave dell'unificazione sull'astrazione).

## 4. Passi di migrazione — sotto-giri (ognuno con la sua Fase 0–4)

> ✅ APPROVATA — Fase 0, 2026-07-09. Rischio crescente; ogni sotto-giro è un branch/PR a sé.

- **08a ✅ — `SectionCatalog` condiviso** (fatto 2026-07-09, +13 test). `SectionCatalog` (natura
  per key, membership per profilo App/AccAerovia/AccAppBlock/Vloa, `Reconcile` unificato) +
  modello sezione ricorsivo `DocSection`/`DocBlock`. Membership rivista con l'utente: 6 sezioni
  universali (aor/frequencies/coordination/regulated/operationaltechnique/validity); `purpose`
  e Military-areas rimosse (fuse in `regulated`); sezione editoriale generica ricorsiva
  (titolo + blocchi Testo/Tabella/Callout + sotto-sezioni). Non ancora wired (08c/08d).
- **08b ✅ — estrazioni multi-classe** (fatto 2026-07-09). Solo file **superstiti**: `Documents.cs`
  → 8 entità in file singoli; `EditingService.cs` → `IEditingService` + `EditNotAllowedException`
  + `EditConflictException`. **Saltati** `AccProfileModels`/`AppProfileModels`/`VloaSections`:
  vengono rimossi in 08d, estrarli ora = churn buttato.
- **08c ✅ — ponte** `SectionCatalogBridge` (fatto 2026-07-09, +11 test): `BlockSection` legacy →
  chiavi catalogo, usato dalle migrazioni per-tipo.
- **08d — migrazione editing per tipo** (67 file profile → un tipo alla volta; **no** migrazione
  dati, solo reset schema greenfield; il rewire è codice, non DB):
  - **08d-vloa ✅** (fatto 2026-07-09, 246 test): `DocumentSection.SectionKind` (enum) →
    `SectionKey` (chiave catalogo) su tutto il modello classic; DTO/viewer/editor vLOA su chiavi;
    seed/builder convertono via bridge; migrazione EF `SectionKeyCatalog`. vLOA completamente
    migrata; il documento GENERATO di ACC/Airport usa già le chiavi.
  - **08d-acc / 08d-app / 08d-airport ⏳** — migrare l'EDITING (oggi profile JSON) su `Document`;
    per l'aeroporto i dati strutturati (piste/SID/TA-TL) restano come **sorgente**, il `Document`
    ne deriva. Poi drop del modello profile.
- **08e — creazione uniforme** `CreateDocumentUseCase` + fix routing `?doc`
  ([[vloa-editor-routing-todo]]).
- **08f — cleanup**: rimozione codice morto profile, repo/tabelle obsolete.

## 5. Impatto

- **Precede** doc 09 (la pubblicazione consuma il modello documento) — 09 dopo 08.
- **Sblocca i rimandati**: doc 03 (ensure-documento aeroporto), doc 04 (merge SID su profilo),
  doc 05 (generazione vLOA) — tutti dipendenti dal modello documento, si chiudono con/dopo 08.
- **Verifica** (per sotto-giro): 08a modifica singola AoR visibile ovunque + "Nuova sezione"
  completa; 08c/08d ogni tipo creabile/editabile sul modello unico; publish/search ancora ok;
  routing `?doc` risolto. Nuovi test per ogni sotto-giro (baseline cresce).
