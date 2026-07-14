# 08 — Modello documento + editing (punti 9+12) ✅ COMPLETO (08a–08i mergiati, 2026-07-10)

> **CHIUSO (2026-07-10).** Tutti e 4 i tipi (vLOA, APP, ACC, Airport) su modello unificato `Document`+
> `SectionCatalog`; overlay editoriale unico `DocumentProfile`; storage profile (`AccProfile`/`AppProfile`/
> `VloaProfile`) eliminato con migrazioni di drop. Il dolore del punto 12 (due modelli documento) è risolto.
> Baseline test = 252. Opzionale residuo: creazione airport via use-case unico (ex-08h). Prossimo: doc 09.

> **DECISIONE Fase 0 (2026-07-09)**: unificazione greenfield su `Document` + `SectionCatalog`,
> test-first. Decomposto in sotto-giri (§4).
>
> **STRATEGIA B ABBANDONATA — ritorno a strategia A greenfield completo (2026-07-10, owner).**
> La strategia B (adozione incrementale del catalogo, storage profile invariato) è stata provata su
> ACC/APP con `08d-app+acc` e **bocciata in verifica**: lascia divergenze inaccettabili tra i tipi —
> config in APP degradata a editoriale generico (l'editor ricco è ACC-only), corpo custom divergente
> (ACC solo prosa, APP prosa+tabella) e **niente Callout né sotto-sezioni** (il modello `DocSection`/
> `DocBlock` restava scollegato). Si torna alla **strategia A** (§3 originale): migrare lo storage di
> ACC/APP/Airport sul modello `Document` classic — lo stesso su cui vLOA gira già da 08d-vloa.
>
> **Design locks (2026-07-10):**
> 1. **Editor editoriale unico** su `DocumentSection`+`ContentBlock`: blocchi `Prose`/`Table`/`Callout`
>    + **sotto-sezioni** (albero `DocumentSection`, `MaxDepth`=3). Un solo componente editor + un solo
>    viewer, condivisi da tutti i tipi. Il modello `Document` regge già tutto questo (verificato).
> 2. **Config ricca anche in APP**, pool = **settori dell'aeroporto dell'APP** (es. `LIRP_APP` →
>    settori di `LIRP`); accorpamento calcolato come per l'ACC. `configurations` resta nella membership APP.
>
> **Cosa si tiene di `08d-app+acc` (branch `refactor/08d-app`)**: l'adozione di `SectionCatalog` e la
> rimozione di `AppSections`/`AccSections` sono **fondamenta valide anche per A** → restano. Il default
> editoriale generico (storage `AppCustomBlock`) è un **ponte temporaneo**: sostituito in 08f/08g.
>
> Stato: **08a·08b·08c·08d-vloa mergiati** + **08d-app+acc** (catalogo adottato, ponte editoriale);
> ACC/APP/Airport storage ancora profile → §4 (08e→08i).

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
  - **08d-app+acc 🟠 SUPERATO (ponte)** (fatto 2026-07-10, 242 test): tentativo strategia B su
    ACC+APP. Migrati editor/viewer APP+ACC da `AppSections`/`AccSections` a `SectionCatalog`,
    `SectionShell.Kind`→`SectionKind`, default editoriale generico, registry eliminati. **Bocciato
    in verifica** (vedi header): config APP degradata, corpo custom divergente, no Callout/sotto-sezioni.
    Si tiene l'adozione catalogo; il ponte editoriale (`AppCustomBlock`) è rimpiazzato in 08f/08g.

> **STRATEGIA A — sotto-giri (2026-07-10).** Migrazione storage ACC/APP/Airport → `Document`.
> Rischio crescente; ogni sotto-giro branch/PR a sé, verificato **guidando l'app** (editor/viewer
> Blazor non coperti da test — vincolo confermato in 08d-app+acc). vLOA è il **template** (già su `Document`).

- **08e ✅ — storage ACC/APP/Airport su `Document`** (il cuore): mappare `AccProfile`/`AppProfile`/
  `AirportProfile` → `Document`(Type vIPI) + `DocumentVersion` + `DocumentSection`(tree, `SectionKey`
  da catalogo) + `ContentBlock`. **Architettura di storage (lock 2026-07-10, dal pattern vLOA provato):**
  - **Import-derivate** (aor/frequencies/coordination/minima): `DocumentSection` keyed, **Blocks vuoti**,
    rese live dal renderer per key (come vLOA oggi). Gli **override per-doc** (ordine freq, link freq,
    settori/freq nascosti, hidden-sezioni, template coord) vivono in una **side-entity 1:1 col Document**
    — generalizzazione di `VloaProfile` (candidata: `DocumentProfile` unica per tutti i tipi).
  - **Strutturate-editoriali** (separations, vfr, config, regulated): `DocumentSection` keyed + dato
    strutturato in `ContentBlock.BodyJson` (schema per key), editate da un **editor keyed specializzato**
    (riusa gli editor esistenti AppSeparations/AppVfr/ConfigEditor/RegulatedEditor, ora leggono/scrivono
    il blocco). Config: pool = settori dell'aeroporto (APP) o CTR (ACC); accorpamento calcolato.
  - **Editoriali generiche** (custom/operationaltechnique/validity/free): `DocumentSection`+`ContentBlock`
    Prose/Table/Callout + sotto-sezioni, via `DocumentSectionsEditor` (08f).
  - Servizi: `IAccProfileService`/`IAppProfileService` editing → assorbiti nell'editing unico su `Document`
    (estende quello vLOA/`IEditingService`). **Greenfield**: migrazione EF che DROPpa le tabelle profile;
    derivato rigenerato dagli import; editoriale a mano perso (pre-prod). Test-first sul mapping.
  - Decomposto: **08e-app** (template, più semplice) → **08e-acc** → **08e-airport**.
- **08f ✅ — editor+viewer editoriale unico** (design lock #1): un solo componente editor che edita un
  sottoalbero `DocumentSection` con blocchi `Prose`/`Table`/`Callout` + **sotto-sezioni** (depth ≤3),
  reorder/hide; + un solo viewer. Sostituisce `VloaEditor` inline, `CustomEditor`/`CustomBody` (ACC/APP)
  e il ponte generico di 08d. Elimina `AppCustomBlock`/`AppCustomSection` e `AppProfileModels` editoriali.
- **08g ✅ — sezioni derivate keyed condivise + config ricca**: renderer per key (aor/frequencies/
  coordination/minima/regulated) unici per tutti i tipi; **editor config ricco condiviso** portato
  anche in APP (design lock #2, pool = settori dell'aeroporto dell'APP; accorpamento come ACC).
- **08h ✅ — Airport su `Document`**: il documento aeroporto (già generato con chiavi catalogo) finisce
  la migrazione; creazione via use-case unico, non più speciale in `StructureEditingService` (doc 03).
  **Residuo aperto**: creazione airport via use-case unico ancora da unificare (ex-08h, opzionale).
- **08e-bis ✅ — creazione uniforme** `CreateDocumentUseCase` su `ReleaseTargetType` + fix routing `?doc`
  ([[vloa-editor-routing-todo]]). (Può accorparsi a 08e/08h secondo convenienza.)
- **08i ✅ — cleanup**: entità/tabelle profile droppate (migrazioni `DropAppProfile`/`DropAccProfile`/
  `DropVloaProfile`, 2026-07-10) e repository rewired su `Document`. **Rename semantico** (2026-07-11): i tipi
  Application non erano codice morto ma **mal chiamati** (storage sparito, nome `*Profile*` rimasto) → rinominati
  per ruolo reale: `AccProfileService`→`AccDerivationService`, `VloaProfileService`→`VloaDerivationService`,
  `AirportProfileService`→`AirportEditingService`; repo `I*ProfileRepository`→`*DerivationRepository`/`IAirportRepository`;
  data `AccProfileData`→`AccVipiData`, `AirportProfileData`→`AirportData`; file `*ProfileModels.cs`→`AccVipiModels`/
  `AppModels`/`AirportModels`. `DocumentProfile` (overlay unificato) e `SectionProfile` (membership catalogo) **restano**
  (nomi legittimi). Commenti stale corretti. Build 0/0, 271 test verdi. **Residuo opzionale**: create airport via use-case unico.

## 5. Impatto

- **Precede** doc 09 (la pubblicazione consuma il modello documento) — 09 dopo 08.
- **Sblocca i rimandati**: doc 03 (ensure-documento aeroporto), doc 04 (merge SID su profilo),
  doc 05 (generazione vLOA) — tutti dipendenti dal modello documento, si chiudono con/dopo 08.
- **Verifica** (per sotto-giro): 08a modifica singola AoR visibile ovunque + "Nuova sezione"
  completa; 08c/08d ogni tipo creabile/editabile sul modello unico; publish/search ancora ok;
  routing `?doc` risolto. Nuovi test per ogni sotto-giro (baseline cresce).
