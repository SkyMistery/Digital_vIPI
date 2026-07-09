# 08 — Modello documento + editing (punti 9+12) 🟢🟡

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

> 🟡 BOZZA — la decisione più importante dell'intero refactor, da discutere a fondo.

Direzione candidata (da validare):

- **Un registry di sezioni condiviso** (`SectionCatalog`): ogni tipo di sezione (AoR,
  Frequencies, Coordination, Separations, Minima, VFR, custom…) definito **una volta**
  con la sua struttura, il suo `SectionKind` (Editorial/Derived) e il suo renderer. Il
  singolo documento dichiara **quali** sezioni include e con quali dati, non **come sono fatte**.
  → risolve punti 12a (AoR uniforme) e 12b ("Nuova sezione" con tutte le opzioni).
- **Convergere sui due modelli**: decidere se unificare su un modello unico (profile
  generalizzato o classic generalizzato) o mantenere due modelli ma dietro un'astrazione
  comune (`IDocumentModel`) che il registry e la pubblicazione (doc 09) consumano in modo uniforme.
- **Creazione uniforme**: un unico `CreateDocumentUseCase` parametrico su `ReleaseTargetType`,
  incluso Airport (rimuovere la creazione speciale da `StructureEditingService`).
- Estrarre tutti i tipi dai file multi-classe.

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Inventariare tutte le sezioni esistenti nei 3 registry + `BlockSection` → tabella unica.
2. Definire `SectionCatalog` condiviso + descrittore comune.
3. Migrare un tipo alla volta a leggere dal catalogo (partire da AoR, la più duplicata).
4. Decidere e attuare la convergenza dei modelli documento.
5. Unificare la creazione documento.
6. Estrarre i tipi dai file multi-classe.

## 5. Impatto

- **Precede** doc 09 (la pubblicazione consuma il modello documento). Va fatto **prima** di 09.
- **Accoppiato con** doc 03 (ensure-documento aeroporto) e doc 05 (generazione vLOA).
- **Verifica**: modifica singola alla definizione AoR visibile su tutti i tipi; "Nuova
  sezione" offre il catalogo completo; ogni tipo di documento ancora creabile/editabile;
  routing `?doc` risolto.
