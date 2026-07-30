# 11 — Uniformità dei tre documenti (vIPI ACC · vIPI APP · vLOA) 🟢

> Chiude i disallineamenti fra le tre famiglie documentali emersi dall'**audit del 2026-07-30**
> (viewer pubblico / editor / anteprima bozza). L'asse 08→10 ha unificato **storage** (`Document`),
> **pubblicazione** (`IReleaseTarget`) e **congelamento** (`RenderMode`); restava non unificato ciò
> che sta **fra** editor e viewer: chiave di sezione, resa del contenuto editoriale, stato «nascosta»,
> e i fallback della vista pubblica.
>
> **Stato: carta approvata, esecuzione in corso 🟡 (P1→P6, dal 2026-07-30).**
> Branch `fix/uniformita-tre-documenti`. Baseline suite **640 verde**. Verifica live prevista su
> LIBB (ACC), LIBP_APP (APP), LIBB↔LDZO (vLOA) con Edge/puppeteer. Dipende da: doc 08, 09, 10.

## 1. Stato rilevato (audit 2026-07-30, verificato live)

Metodo: build + suite (640 verde), app avviata su **copia** del `vipi.db` reale, browser reale
(skill `verifica-live`), esperimenti editor → bozza → pubblica con riscontro sul DB.

Il modello è unico (`Document → DocumentVersion → DocumentSection → ContentBlock`) e l'editor è
condiviso (`DocumentSectionsEditor`), ma **ogni famiglia rilegge quel modello a modo suo**:

| | vIPI ACC | vIPI APP | vLOA |
|---|---|---|---|
| Viewer | `AccVipiPage` → `AccSectionBody` (per **chiave**, da `AccBlock`) | `AppnPage` → componenti per chiave + `SectionNode` | `VloaDocumentView` → componenti + `SectionNode` |
| Contenuto editoriale | `AppCustomSection` (solo prosa) | `BlockRenderer` (tutti i formati) | `BlockRenderer` (tutti i formati) |
| Sezioni nascoste | `AccBlockMeta.HiddenSections` (**versionato**, per `SectionKey`) | `DocumentProfile.HiddenSectionsJson` (**non** versionato, per `SectionKey`) | `VloaEditorialState.HiddenSections` (**non** versionato, per **titolo**) |
| Marcatura in bozza | pill `🚫 nascosta` su tutte | pill solo sulle sezioni speciali | nessuna: la sezione sparisce |
| Fallback a pubblica | `LoadViewModel` → frozen ✔ | `_useFrozen` resta `false` → live ✘ | `_useFrozen` resta `false` → live ✘ |
| Pannello release | `ReleasePanel` | `ReleasePanel` | **assente** |

## 2. Problemi (con prova live)

### P-A1 — ACC: tabelle e sotto-sezioni perse, callout declassato a prosa
`AccDocumentAssembler.CustomSectionsOf` tiene solo i blocchi con `Body` non vuoto e li marca tutti
`Prose`, e ignora `EditableSection.Children`; `AccSectionBody` rende quelle sezioni da lì.
**Prova:** blocco Aerovia di LIBB, sezione «ZZTEST2» → blocchi salvati `427 Table("CELLA-ZZ")`,
`428 Callout("CALLOUT-ZZ")`, `429 Prose("PROSA-ZZ2")` e sotto-sezione `514 Prose("SOTTO-ZZ")`;
la bozza rende `<p>CALLOUT-ZZ</p><p>PROSA-ZZ2</p>`. Tabella e sotto-sezione **assenti**, callout
senza riquadro/tipo. Colpisce anche «Procedure generali» e «Validità e revisione».

### P-A2 — ACC: dalla seconda sezione custom in poi, il documento non la mostra
`EfEditingRepository.AddSectionAsync` assegna `SectionKey = "custom"` a **ogni** sezione nuova
(`SectionCatalogBridge.KeyFor(BlockSection.Other)` è `null`). Il viewer ACC indicizza per chiave
(`SectionCatalog.Reconcile` + `CustomSections.FirstOrDefault(c => c.Key == key)`), l'assembler dedup
con `childIds.TryAdd`. **Prova:** editor con `ZZTEST2` **e** `ZZTEST3` → bozza con solo `ZZTEST2`.
Anche le anchor collidono (`#p-{blocco}-custom`).

### P-A3 — APP: «Nascondi» su una sezione custom le nasconde tutte
`AppEditorPage.IsHidden/ToggleHidden` e `AppnPage` chiavizzano su `SectionKey`, uguale per tutte.
**Prova:** nascosto `ZZAPP1` → editor marca `🙈` anche `ZZAPP2`; DB `["configurations","vfr","custom"]`.

### P-A4 — Stato editoriale non versionato che esce in pubblico senza pubblicare
`DocumentProfile` (APP) e `VloaEditorialState` (vLOA) sono side-entity per `documentId`, lette **live**
dai viewer pubblici. **Prova:** un click su «Nascondi» in editor (nessun publish, nessuna release)
toglie «VFR» dalla pagina pubblica dell'APP e «Purpose» da quella della vLOA.
Stessa classe: `AppnPage` chiama `DeriveConfigTableAsync` → `GetSectionBlockJsonAsync`, che legge la
**versione di lavoro** → configurazioni di bozza in pubblica.

### P-A5 — `?as=rel:` non valida degrada a «pubblica» ma con derivate LIVE
`_useFrozen` è inizializzato `false` e messo `true` **solo** nel ramo `default:`; i fallback dei rami
Draft/Release non lo impostano (`AppnPage` ×2, `VloaListPage`). **Prova:** `/vsop/libb/vloa?acc=LDZO`
→ 3 tabelle/11 righe (frozen); `…&as=rel:22` (release di un'altra vLOA) → **9 tabelle/31 righe**,
contenuto identico alla bozza, nessun banner. Il congelamento AIRAC è bypassabile dall'URL.

### P-A6 — Editor APP accetta callsign non standalone → documento irrenderizzabile
`EfAppDerivationRepository.ResolveForDocumentAsync` matcha qualunque `Type == App`;
`EfContentRepository.LoadAppVipiAsync` pretende `IsPrimary && ApproachKind == Standalone`.
**Prova:** `LIBD_CS0_APP` è **Remotized**, ha `DocumentId = 16`, l'editor si apre e salva, ma
`/vsop/libb/apps/vipi?app=LIBD_CS0_APP` risponde «APP not found» in pubblica **e** in bozza — dove
punta il tasto «Anteprima bozza» dell'editor.
Collaterale: `AppDocumentService.EnsureAsync` fa l'early-return sui documenti già migrati **prima**
di `EnsureCanEditAccAsync`.

### P-B — disallineamenti minori (stessa radice)
- **B1** sotto-sezioni sotto una sezione derivata: invisibili in tutte e tre le famiglie (prova: sezione
  `479 "asd"` sotto «Separazioni» di `LIBP_APP`). L'editor però offre «+ sotto-sez» proprio lì.
- **B2** marcatura «nascosta» in bozza: tre comportamenti diversi (tabella §1).
- **B3** vLOA: i blocchi editoriali della sezione padre «Coordination» non arrivano mai al viewer.
- **B4** vLOA: nascondi per **titolo** → una rinomina perde lo stato, titoli uguali collidono.
- **B5** editor vLOA senza `ReleasePanel`: la release AIRAC si fa solo da `/vsop/versioni`; ed è l'unico
  editor con un «Pubblica» di versione.
- **B6** doppia rotta viewer vLOA: `/vsop/{acc}/vloa?acc=` (documentata) e `/vsop/{acc}/apps/vipi?vloa=`
  (non in `mappa-pagine.md`), che è quella linkata dall'editor.
- **B7** elenco APP e pagina APP con gate diversi: elenco su `Document.Status`, pagina su release
  effettiva (prova: `LICC_APP` pubblicamente navigabile, in elenco marcato bozza).
- **B8** `VipiViewService.Map` scarta le sezioni senza blocchi né figli (salvo `Derived`): una sezione
  appena creata non appare in bozza. L'ACC invece le mostra sempre.

## 3. Architettura target (APPROVATA) 🟢

Principio: **il documento è la fonte unica; i tre viewer devono leggerlo con le stesse regole.**
Dove oggi ci sono tre meccanismi per la stessa decisione, ne resta uno — sul `Document`.

### 3a. Identità di sezione unica (P-A2, P-A3)
`SectionKey` diventa **univoca dentro il documento** anche per le sezioni libere: alla creazione,
`custom:{8 hex}` (stesso schema di `AccDocumentService.AddGroupAsync`, che già usa `grp:{guid8}`).
`SectionCatalog.KindOf` continua a ritornare `Editorial` per le chiavi ignote → nessun altro cambio.

- **Migrazione dati** (`EfEditingRepository` al boot, come le altre riconciliazioni): ogni
  `DocumentSection.SectionKey == "custom"` riceve una chiave nuova. I payload di release già scritti
  non si riscrivono: la resa dell'ACC non dipende più dalla chiave (§3b).

### 3b. L'ACC rende le sezioni per identità, non per chiave (P-A1, P-A2, B1, B8)
`AccBlock` espone `Sections: IReadOnlyList<AccBlockSection>` — `(SectionId, Key, Title, IsHidden)` —
nell'ordine dei figli. `AccVipiPage` itera **quella lista** (niente più `Reconcile` a view-time, che
comunque veniva alimentato dall'ordine dei figli). `SectionCatalog.Reconcile` resta per l'assembler,
che accoda le sezioni-catalogo eventualmente assenti nei documenti vecchi.

Il contenuto editoriale (chiavi non strutturate) è reso da **`SectionNode`**, come APP e vLOA:
stessi formati (prosa/callout/tabella/tip), stesse sotto-sezioni ricorsive. `AppCustomSection` /
`AppCustomBlock` e `AccSectionBody.CustomBody` spariscono (→ propagazione, §4 P3).

Le sotto-sezioni di una sezione **derivata** vengono rese dopo il corpo derivato in tutte e tre le
famiglie (`SectionView.Children` sempre percorso).

`VipiViewService.Map` non scarta più le sezioni vuote: una sezione esiste perché l'editore l'ha
creata (B8). Restano scartati solo i blocchi filtrati per `Tier`.

### 3c. `DocumentSection.IsHidden` — un solo stato «nascosta», versionato (P-A3, P-A4, B2, B4)
Gemello di `RenderMode` (doc 10 §3a): flag **per sezione**, sul `Document`, quindi versionato e
catturato nello snapshot di release senza codice aggiuntivo.

- Nuova colonna `DocumentSections.IsHidden` (migrazione `AddSectionIsHidden`), default `0`.
- Propagata su `RawSection` → `SectionView` → `EditableSection` (come `RenderMode`).
- I tre editor: `IsHiddenSection`/`OnToggleHidden` diventano un unico comportamento in
  `DocumentSectionsEditor` (`Editing.SetSectionHiddenAsync`); niente più callback per-famiglia.
- I tre viewer: `s.IsHidden` → in **pubblica/release** la sezione è omessa, in **bozza** è resa con la
  pill `🚫 nascosta` e opacità ridotta (comportamento ACC, il più informativo).
- **Migrazione dati** one-shot al boot: `AccBlockMeta.HiddenSections` (chiavi) +
  `DocumentProfile.HiddenSectionsJson` (chiavi) + `VloaEditorialState.HiddenSections` (titoli) →
  flag sulle sezioni della versione di lavoro. Regola **conservativa**: `"custom"` ambiguo espande a
  **tutte** le sezioni libere di quel documento (non si scopre in pubblico ciò che l'editore riteneva
  nascosto). Le tre sorgenti restano scritte-a-zero e i campi vengono rimossi.
  `HiddenAorSectors`/`HiddenFrequencies` (settori/frequenze, non sezioni) **restano** dove sono.

### 3d. Fallback a pubblica = pubblica davvero (P-A5)
Il degrado da bozza/release non autorizzata o non corrispondente deve produrre **esattamente** la vista
pubblica, derivate frozen incluse. Un solo punto di verità per viewer: la vista pubblica si costruisce
in un metodo `LoadPublicAsync()` che imposta insieme `_view` e `_useFrozen`, chiamato dai fallback
(pattern già corretto in `AccVipiPage.LoadViewModel`).

### 3e. Superficie APP = APP non remotizzati (P-A6, B7)
`ResolveForDocumentAsync` filtra `IsPrimary && ApproachKind == Standalone`: un callsign remotizzato non
ha più identità documentale → l'editor risponde «non è un APP non remotizzato» invece di creare un
documento orfano. `AppDocumentService.EnsureAsync` sposta `EnsureCanEditAccAsync` **prima**
dell'early-return.
`AppsListPage` allinea il gate del pubblico a quello della pagina (**release effettiva**, non
`Document.Status`), come già fanno aeroporti e vLOA (doc 10 §3f).

### 3f. Uniformità di superficie (B3, B5, B6)
- La sezione padre «Coordination» della vLOA è **derivata** (come nel viewer): l'editor non offre più
  blocchi lì (`IsDerived` include `coordination` a qualsiasi profondità).
- `ReleasePanel` (target `Vloa`, chiave = `docId`) entra in `VloaEditor`, come negli altri due editor.
- La rotta `/vsop/{acc}/apps/vipi?vloa=` viene **rimossa**: la vLOA ha una rotta sola,
  `/vsop/{acc}/vloa?acc=`. Link dell'editor e `PreviewBanner` puntano lì.

## 4. Passi di migrazione

| # | Passo | Tocca |
|---|---|---|
| **P1** | Chiave sezione univoca `custom:{guid8}` + riconciliazione al boot delle `"custom"` esistenti | `EfEditingRepository`, `VipiDbContext` bootstrap |
| **P2** | Fallback a pubblica con derivate frozen (`LoadPublicAsync`) | `AppnPage` (APP + vLOA), `VloaListPage` |
| **P3** | ACC per identità + `SectionNode`; sotto-sezioni sempre rese; `Map` non scarta le sezioni vuote | `AccDocumentAssembler`, `AccVipiPage`, `AccSectionBody`, `AccOperativaPage`, `AppnPage`, `VloaDocumentView`, `VipiViewService`, `AccVipiModels` |
| **P4** | `DocumentSection.IsHidden` versionato + migrazione dai 3 storage + resa uniforme in bozza | migrazione EF, `DocumentSection`, `RawSection`/`SectionView`/`EditableSection`, `IEditingService`, i 3 editor, i 3 viewer |
| **P5** | Gate standalone APP + authz prima dell'early-return + gate elenco su release effettiva | `EfAppDerivationRepository`, `AppDocumentService`, `AppsListPage`, `AppEditorPage` |
| **P6** | Coordination vLOA derivata, `ReleasePanel` nell'editor vLOA, rotta vLOA unica | `VloaEditor`, `AppnPage`, `VloaEditorPage`, `mappa-pagine.md` |

Ogni passo: commit proprio, `dotnet build` verde, test aggiunti dove il comportamento è deterministico.

## 5. Impatto / Verifica

**Test (nuovi, sul cuore deterministico):**
- `AccDocumentAssembler`: sezione libera con blocchi Table/Callout + sotto-sezione → preservati e
  distinti; due sezioni libere → due voci (non una).
- `SectionKeys.NewCustom()`: unicità/formato; `SectionCatalog.KindOf` su chiave generata → `Editorial`.
- `VipiViewService.Map`: sezione senza blocchi né figli → preservata.
- `DocumentSection.IsHidden`: round-trip editing → snapshot di release → viewer.
- `LoadAppVipiAsync`/`ResolveForDocumentAsync`: un APP `Remotized` non ha identità documentale.

**Verifica live (rifare gli esperimenti dell'audit, esito atteso):**
1. ACC: sezione libera con tabella + callout + sotto-sezione → **tutto** visibile in bozza, callout con
   riquadro; due sezioni libere → **entrambe** in bozza e in pubblica dopo publish.
2. APP: nascondere una sezione libera **non** tocca le altre; la pubblica **non** cambia finché non si
   ripubblica.
3. vLOA: sezione nascosta appare in bozza con pill `🚫 nascosta`; la pubblica non cambia senza publish.
4. `?as=rel:{id}` forgiata → pagina identica alla pubblica (stesso numero di tabelle/righe).
5. `/vsop/{acc}/apps/editor?app=<remotizzato>` → messaggio d'errore, nessun documento creato.
6. Editor vLOA: pannello release presente, «Pubblica ora» funziona; «Anteprima bozza» apre
   `/vsop/{acc}/vloa?acc=…&as=draft`.

**Propagazione (domanda 4 del pre-flight):** questo giro **rimuove** `AppCustomSection`/`AppCustomBlock`,
i campi `HiddenSections` dei tre storage e la rotta `apps/vipi?vloa=` → vanno aggiornati nello stesso
giro: `spec/mappa-pagine.md`, `spec/modello-dati.md`, `history/rounds.md`, `docs/index.md`, le memorie
`snapshot-totale-rendermode` e `public-list-visibility-gate`.
