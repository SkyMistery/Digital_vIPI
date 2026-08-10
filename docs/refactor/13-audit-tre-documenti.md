# 13 — Audit dei tre documenti (vIPI ACC · vIPI APP · vLOA) 🟢

> **Stato: ESEGUITO** (2026-08-10, branch `refactor/13-tre-documenti`). §1-§2 rilevate sul codice del
> 2026-08-10; §3-§4 approvate dall'owner e portate a termine: 17 passi, 16 commit, suite **1335 → 1377**
> verde. Resta la **verifica live** (§5), obbligatoria su Blazor.
>
> §1 e §2 descrivono lo stato **prima** del lavoro e si leggono al passato: sono il referto, non la mappa
> del codice di oggi.
>
> Seguito di [11 — Uniformità dei tre documenti](11-uniformita-tre-documenti.md), che ha chiuso
> l'asse *storage/visibilità/resa del contenuto*. Questo giro guarda ciò che è rimasto fuori:
> **superficie dell'editor, catalogo delle sezioni, ricerca/indice, testi**.

---

## 1. Stato rilevato

### 1a. Le tre famiglie, riga per riga

| | vIPI ACC | vIPI APP | vLOA |
|---|---|---|---|
| Viewer | `AccVipiPage` → `AccSectionBody` | `AppnPage` (switch per chiave) + `SectionNode` | `VloaDocumentView` + `SectionNode` |
| Editor | `AccEditorPage` (n. `DocumentSectionsEditor`, uno per blocco) | `AppEditorPage` (1 `DocumentSectionsEditor`) | `VloaEditor` (1 `DocumentSectionsEditor`) |
| Struttura da | `SectionCatalog.For(AccAerovia/AccAppBlock)` | `SectionCatalog.For(App)` | **`VloaSections.Canonical`** (registro parallelo) |
| Storage vista pubblica | `AccDocumentService.LoadForViewAsync` (snapshot) | `EfContentRepository.LoadVipiAsync` (snapshot) | `EfContentRepository.LoadVipiAsync` (snapshot) |
| Anteprima release | `AccDoc.LoadForReleaseAsync` | `Releases.GetPreviewAsync` + `BuildFromRawAsync` | `Releases.GetPreviewAsync` + `BuildFromRawAsync` |
| «Sezione obbligatoria» | lista hardcoded nella pagina | `SectionCatalog.Find` | **per titolo** (`VloaSections.MandatoryTitles`) |
| Ancora di sezione | `p-{blockKey}-{sectionKey}` | `p-{sectionKey}` (speciali) / `s-{id}` | `s-{id}` |
| Pannello release | `ReleasePanel` nudo | `ReleasePanel` + involucro + aiuto + diff + annulla | `ReleasePanel` + involucro + diff |
| AIRAC mostrato | `GetCycle(UtcNow)` (sempre l'attuale) | `GetCycle(UtcNow)` (sempre l'attuale) | `View.AiracCycle` (quello della release) |

### 1b. Chi sa «che natura ha una sezione»

`SectionCatalog` è dichiarato fonte unica (`KindOf`, `IsRenderModeToggleable`, `IsInitiallyCollapsed`),
ma la domanda che i viewer/editor si fanno davvero è un'altra — **«questa sezione ha un corpo scritto a
mano dall'host?»** — e quella risposta vive in **sei copie**, fuori dal catalogo:

| Dove | Insieme |
|---|---|
| `AppnPage.Special` | 8 chiavi |
| `AppEditorPage.Special` | le stesse 8 |
| `AccSectionBody.Structured` | le stesse 8 |
| `AccEditorPage.Structured` | le stesse 8 |
| `VloaEditor.IsDerived` | 3 chiavi |
| `VloaDocumentView` (`if` in linea) | le stesse 3 |

---

## 2. Problemi

Ordinati per gravità. `⛔` = difetto funzionale · `⚠️` = incoerenza visibile · `🔸` = debito/pulizia.

### A — Superficie «Versioni & release»: tre editor, tre pannelli diversi

**A1 ⚠️ (è il caso segnalato dall'owner).** Lo stesso `ReleasePanel` è ospitato in quattro modi:

| Editor | Involucro `.block` + titolo | Testo d'aiuto | Voce nel TOC | `ShowDiff` | `AllowCancel` | `PreviewUrlFactory` |
|---|---|---|---|---|---|---|
| **ACC** (`AccEditorPage:110`) | ✗ | ✗ | ✗ | ✗ | ✗ | default per-target |
| **APP** (`AppEditorPage:461`) | ✓ | ✓ | ✓ | ✓ | ✓ admin | ✓ |
| **vLOA** (`VloaEditor:92`) | ✓ | ✗ | ✓ | ✓ | ✗ | ✓ |
| Aeroporto (`AeroportoEditorPage:598`) | ✓ | ✓ | ✓ (`<details>`) | ✓ | ✓ admin | ✓ |

Conseguenze concrete: dall'editor vIPI ACC **non si vedono le differenze** di una release e **non si può
annullarla** nemmeno da admin; nell'editor vLOA idem per l'annullo; nell'editor ACC il blocco non ha
ancora né voce di menu, quindi su un documento lungo lo si trova solo scorrendo.

**A2 🔸** La chiave di risorsa si chiama `App_VersionsReleases` / `App_ReleaseHelp` ma la usano tre
famiglie su quattro: il nome mente sul proprio ambito.

**A3 ⚠️** `VersioniPage:344` mostra il blocco «Storia modifiche» **solo per ACC e APP**, con la
motivazione scritta nel commento: *«non hanno version-list»*. Dopo il doc 08 **tutti** i tipi sono su
`Document` e hanno la lista versioni: il commento è falso e la vLOA resta senza storia modifiche senza
una ragione.

### B — Decisioni già prese e non applicate ovunque

**B1 ⚠️ `IsInitiallyCollapsed` ignorato dal viewer APP.** Doc 11 §3i (decisione owner): «vale **ovunque**,
viewer ed editor, tutte e tre le famiglie». `AccVipiPage:73` e `SectionNode:9` e `DocumentSectionsEditor:195`
la rispettano; **`AppnPage:70` no** (`CollapsibleBlock` senza `InitiallyOpen`). Risultato osservabile: su un
APP «Aree regolamentate» nasce **aperta**, sulla vIPI ACC e sulla vLOA nasce chiusa. Stesso difetto sul ramo
«nascosta in bozza» di `AppnPage:102` e `VloaDocumentView:50`.

**B2 ⛔ Le sotto-sezioni sotto «Coordination» non esistono, nella vLOA.** Doc 11, regola 5: *«se l'editor sa
creare una cosa, il viewer la deve rendere»*. In `VloaDocumentView` i rami `aor` e `frequencies` chiamano
`SubSections(s, Before/After)`; **il ramo `coordination` (righe 63-82) non lo fa**. L'editor offre
«+ sotto-sez» sulla sezione (è obbligatoria, quindi il pulsante c'è), la si compila, e nel documento
pubblicato non compare nulla. È esattamente il difetto B1 del doc 11, richiuso male su un ramo.

**B3 ⛔ Contenuto seminato che nessuno renderà mai.** `VloaSections.Canonical:46-50` semina un paragrafo in
ciascuna delle due figlie di «Coordination» (*«**{home} transfers** traffic to…»*). L'editor le tratta come
derivate (`IsDerived` include `coordination` a ogni profondità) e sostituisce i blocchi con la tabella; il
viewer rende le direzioni **dal padre** e le figlie non le rende affatto. Quei due paragrafi sono scritti nel
DB di ogni vLOA generata e invisibili in ogni vista.

**B4 ⚠️ «Minime di vettoramento» è dichiarata derivata ma non deriva niente — e non è scrivibile.**
`SectionCatalog` la marca `Derived`; nessun `IFrozenSectionProvider` la cattura (ACC/APP la ignorano nello
`switch`); il corpo è il callout fisso `AppMinima`. Effetti: (a) l'editor mostra il badge e il **toggle
Live/Congelata che non fa nulla**; (b) essendo derivata, l'editor rende `DerivedContent` e **non offre i
blocchi** → la sezione è la sola del documento in cui non si può scrivere. Contraddice la decisione del
9 agosto 2026 (`lavori-aperti.md` §E2, e il commento in cima ad `AppMinima.razor`): *«se le minime servono,
si scrivono qui come contenuto editoriale»*. Oggi non si può.

### C — Bug funzionali

**C1 ⛔ ALTA — la pagina APP pubblica mostra la bozza.** `AppnPage:242` chiama
`AppDoc.DeriveConfigTableAsync(_app)` anche nella vista pubblica; quella scende a
`EfEditingRepository.GetSectionBlockJsonAsync`, che risolve la **versione di lavoro (bozza se esiste)**
(`EfEditingRepository:549` → `ResolveWorkingVersionIdAsync:600`). Quindi la tabella «Configurazioni» del
documento pubblico riflette le configurazioni **non pubblicate**. Rompe l'invariante centrale del doc 10
(snapshot totale: «modifiche visibili solo nell'editor finché non si ripubblica»).
La vIPI ACC **non** ha il difetto: là il config-table si deriva dal blocco assemblato **dallo snapshot**
(`AccVipiPage:183` con `b` proveniente da `AccDocumentAssembler.Assemble(snapRaw)`).

**C2 ⛔ ALTA — la ricerca pubblica indicizza ciò che la pagina nasconde.**
`EfSearchRepository.SearchAsync` parte da `_db.Documents.Where(d => d.CurrentVersionId != null)`:
- nessun filtro `!d.IsHidden` → i documenti nascosti dall'admin sono cercabili;
- nessun filtro su `DocumentSection.IsHidden` → le **sezioni nascoste** compaiono con lo snippet;
- nessun gate su release effettiva → esce contenuto della versione pubblicata che la pagina non serve
  (doc 10 §S6b: visibilità pubblica = release effettiva).

**C3 ⛔ La ricerca manda gli APP sulla pagina sbagliata.** In `EfSearchRepository` il ramo non-vLOA
distingue solo *aeroporto* da *tutto il resto* → ogni Document di APP standalone ottiene
`urlBase = "vipi"`, cioè **`/vsop/{acc}/vipi`** (la vIPI di ACC) invece di
`/vsop/{acc}/apps/vipi?app={callsign}`. Inoltre `SearchScope` non ha una voce APP: gli APP finiscono nel
filtro «vIPI» insieme alle ACC.

**C4 ⚠️ Le ancore dei risultati non esistono.** La ricerca costruisce `#s-{sectionId}`, che è l'ancora di
`SectionNode`/`VloaDocumentView`. Non esiste nella vIPI ACC (usa `p-{blockKey}-{sectionKey}`) né sulle
sezioni speciali dell'APP (usa `p-{sectionKey}`). I deep-link cadono in cima alla pagina.

**C5 ⛔ «Cosa è cambiato» ripete C2+C3.** `EfChangesRepository.ListChangedAsync` ha la **quarta copia**
della risoluzione di rotta (dopo `VersioniPage`, `ReleasePreviewPage` e la ricerca) con lo stesso errore
sugli APP, e senza gate su `IsHidden`/release. `IDocRoutesRegistry` — nato in doc 09 §3b proprio per
questo — non è usato da nessuna delle due.

**C6 ⚠️ L'AIRAC scritto sul documento non è quello del documento.** `AccVipiPage:91` e `AppnPage:117`
mostrano nel riquadro laterale `Airac.GetCycle(DateTime.UtcNow)` — il ciclo di **oggi** — mentre il corpo
della pagina è lo snapshot congelato di un'altra release. `AppnPage` ha `_view.AiracCycle` (il ciclo fissato
allo snapshot) sotto mano e non lo usa; `AccVipi` non ce l'ha proprio, perché `AccDocumentModel` non porta
il ciclo. Solo la vLOA mostra il valore giusto (`VloaDocumentView:93`). Su un documento operativo firmato
per ciclo AIRAC, è un'informazione sbagliata, non solo un'incoerenza.

**C7 ⚠️ La landing ACC non applica alla vIPI il gate che applica agli altri tre.** `AccLanding` filtra
aeroporti, APP e vLOA su `HasEffectiveRelease && !IsHidden`; la card «vIPI di *ACC*» è **sempre** presente
e, senza release, porta a «vIPI non disponibile».

**C8 🔸 Cattura frozen dei coordinamenti vLOA ripetuta tre volte.** Le due figlie di «Coordination»
hanno `SectionKey = "coordination"` (mappato da `SectionCatalogBridge` in `VloaStructureSeeder:29`).
`FrozenSectionScan.FrozenDerived` restituisce quindi **tre** sezioni con quella chiave, e
`VloaFrozenSectionProvider` chiama `DeriveCoordinationAsync` tre volte salvando tre copie identiche.
In lettura, `GetFrozenByKeyAsync` prende la **prima corrispondenza**: funziona per caso (il padre viene
per primo nella visita), non per costruzione.

**C9 🔸 Fallback di rotta rotto.** `VloaDocRoutes.EditorUrl`, senza `neighbourCode`, ripiega su
`/vsop/{acc}/editor?doc={id}`, che è l'**editor della vIPI ACC** e ignora `?doc`.

### D — Il catalogo delle sezioni non governa tutte e tre le famiglie

**D1 ⚠️ `SectionProfile.Vloa` è morto in produzione.** L'unico consumatore di
`SectionCatalog.For(SectionProfile.Vloa)` è `SectionCatalogTests`. La struttura reale della vLOA nasce da
`VloaSections.Canonical`, un registro parallelo espresso in `BlockSection` (enum legacy). Da qui:
- il catalogo dichiara 6 sezioni, la vLOA reale ne ha **7** (in più «Purpose», che non ha chiave di
  catalogo → nasce `custom:{guid8}`);
- l'ordine diverge: nel catalogo `coordination`(7) precede `operationaltechnique`(9); nella vLOA reale
  «General procedures» viene **prima** di «Coordination»;
- il titolo diverge: catalogo «Regulated areas», documento reale «Military areas coordination and
  management».

**D2 🔸 `SectionCatalog.Reconcile` è morto** (solo test), ma il commento in testa alla classe lo presenta
come una delle tre responsabilità della fonte unica. Doc 11 §3b ha cambiato la regola («si itera la lista di
sezioni, non un elenco di chiavi riconciliato a view-time») senza rimuovere il metodo né correggere il
commento.

**D3 ⚠️ «Sezione obbligatoria» ha tre implementazioni.**
`AccEditorPage.IsMandatory` = lista hardcoded (`Structured` + `operationaltechnique` + `validity`);
`AppEditorPage.IsMandatory` = `SectionCatalog.Find` (l'unica giusta);
`VloaEditor.IsMandatory` = confronto **sul titolo**. Il doc 11 §3a aveva stabilito che una sezione non si
identifica mai per titolo, e ha migrato «nascosta» — ma «obbligatoria» è rimasta lì.

**D4 ⚠️ Il catalogo non ha il concetto che serve.** `SectionKind` ha solo `Derived`/`Editorial`, ma le
sezioni si comportano in **tre** modi: derivate (aor/frequencies/coordination), **strutturate** (dato
editoriale con editor dedicato: separations, configurations, vfr, regulated) e libere. Le strutturate sono
`Editorial` per il catalogo, e per questo l'insieme «ha corpo bespoke» è finito duplicato sei volte (§1b).

**D5 ⚠️ Aggiungere una sezione al catalogo ha effetti diversi nelle tre famiglie.**
`AccDocumentAssembler.SectionsOf:95` **accoda** al volo le sezioni di catalogo assenti dal documento
(vecchio o snapshot). APP e vLOA no: iterano quello che c'è nel `Document`. Quindi una chiave nuova compare
subito su tutte le vIPI ACC esistenti e **mai** sugli APP/vLOA già creati.

**D6 ⚠️ «Validità e revisione»: stessa sezione, tre contenuti iniziali.** Nella vLOA è seminata con una
tabella (`Effective from` / `Review cycle` / `Italian signatory`, `VloaSections:55`); in ACC e APP nasce
**vuota** — `EnsureVipiDocument*Async` crea il blocco placeholder solo per le chiavi in `LiveKeys`, che non
la comprendono. Aggiunta: la tabella vLOA ricopia **a mano** dati che il sistema già conosce (ciclo AIRAC,
firmatario) e non si aggiorna alla pubblicazione.

### E — Anteprime e percorsi non uniformi

**E1 🔸** Due strade per la stessa cosa: ACC passa da `AccDoc.LoadForReleaseAsync`, APP e vLOA da
`IReleaseService.GetPreviewAsync` + `BuildFromRawAsync`. Conseguenza pratica: nell'anteprima release ACC
non arrivano gli Id di sezione, quindi tutte le derivate si ricalcolano live anche quando la release le
aveva congelate.

**E2 🔸** `IReleaseService.GetPreviewAsync` documenta *«Doc null per ACC/APP»*: non è più vero dal doc 08
(il metodo deserializza per tutti i tipi).

**E3 🔸** L'alias legacy `?live=1` è parsato solo da `AppnPage`; ACC e vLOA non lo passano a
`PreviewMode.Parse`. O vale per tutti o si toglie.

### F — Testi e localizzazione

**F1 ⚠️ La vLOA è l'unico documento con testo cablato nel markup.** In `VloaDocumentView`: `Contents`,
`Bilateral document`, `Editable on the Italian side only (staff). The neighbour side is read-only.`,
`Letter of Agreement (EN)` — e nella tabella frequenze l'intestazione **`Frequenza`**, italiana, dentro un
documento dichiaratamente inglese.

**F2 ⚠️ Editor e viewer etichettano diversamente le stesse colonne.** Viewer vLOA:
`Callsign | Position | Frequenza`. Editor vLOA: `Nome | Callsign | Frequenza`. Stessi dati, stesse celle,
intestazioni diverse. (Il viewer è allineato ad `AppFrequencies`; è l'editor a divergere.)

**F3 ⚠️ Copertura di localizzazione a macchia di leopardo** fra pagine della stessa famiglia:
`AccVipiPage` 22 usi di `L[...]`, `AppnPage` 20, `AccLanding` 23, `VloaDocumentView` 10,
**`VloaListPage` 1**, **`AppsListPage` 0** (interamente in italiano cablato). I due `.resx` sono in parità
di chiavi (1186/1186): il problema non è la traduzione, è che quelle pagine non la usano.

**F4 🔸 Testo italiano cablato anche negli editor**: «Shape aggiuntive» e «Colori dei settori»
(`AccEditorPage:483,489` e `AppEditorPage:398,403`), «+ Callout» e «Colonna N» in
`DocumentSectionsEditor`, «Nessuna configurazione operativa.» in `AccSectionBody:59`. E in Application:
`ReleaseService.DiffAsync` produce le etichette `Aggiunta`/`Modificata`/`Rimossa` e «stato attuale (nessuna
release in vigore)» come stringhe italiane non localizzabili — mostrate anche nella diff di una vLOA.

**F5 🔸** Prefisso anteprima nel `<title>`: ACC e APP usano `L["Common_DraftTag"]`/`Common_PreviewTag`,
`VloaListPage:30` scrive `"[Bozza] "`/`"[Anteprima] "` a mano.

**F6 🔸** Etichetta del sommario laterale: `Common_Navigation` (ACC), `Common_Contents` (APP),
`"Contents"` cablato (vLOA).

**F7 🔸** Stato «non pubblicato»: la vIPI ACC dice «vIPI non disponibile», la vLOA «vLOA non disponibile»,
l'APP **«APP non trovato»** — che descrive una cosa diversa (inesistente vs non pubblicato) benché il corpo
del messaggio sia corretto. Il ramo APP, inoltre, è l'unico senza `<PageTitle>` e senza breadcrumb.

### G — Codice morto e commenti che dicono il falso

**G1 🔸** `IVipiViewService.BuildAccVipiAsync` + `IContentRepository.LoadAccVipiAsync` +
`EfContentRepository.LoadAccVipiAsync` non hanno **nessun** chiamante (né in `src` né in `tests`): la vIPI
ACC è passata ad `AccDocumentService` col doc 08e-acc. Con loro muoiono `VipiViewService.ResolveLiveAorAsync`,
`DefaultViewer` e tre dipendenze iniettate (`IAorService`, `ITopologyProvider`, `IOnlineAtcProvider`).

**G2 🔸** `BuildVloaByPairAsync` / `LoadVloaByPairAsync`: nessun chiamante (`VloaListPage` risolve per Id).

**G3 🔸** Il parametro `live` di `IVipiViewService` è **sempre `false`** in produzione (`live: true` compare
solo in `ContentServiceTests`): con esso è di fatto inerte tutta la logica di visibilità per stato AoR di
`ContentService`/`BlockVisibility`.

**G4 🔸 `mappa-pagine.md` non riflette il codice**: descrive la vIPI ACC come «multi-albero `?tree=`» (righe
31, 52, 66) mentre `AccVipiPage:142` dichiara di **ignorare** `?tree`; e attribuisce all'editor APP «6
sezioni fisse» quando il profilo del catalogo ne ha 10.

**G5 🔸 `ReleaseService.PublishNowAsync`** motiva la promozione della bozza con «le liste pubbliche (gate su
`Status==Published`) e il fallback del viewer»: entrambi rimossi in doc 10 §S6b (oggi il gate è
`HasEffectiveRelease`).

**G6 🔸** `VloaReleaseTarget.ResolveDocumentIdAsync` restituisce l'intero parsato senza verificare che quel
Document sia davvero una vLOA: una chiave sbagliata punta a un documento di un'altra famiglia.

---

## 3. Architettura target 🟢

> Approvata dall'owner il 2026-08-10 («tutti i gruppi e i bug, uno per volta»). Ordine di esecuzione
> scelto bottom-up: prima la fonte unica, poi chi la consuma, poi i testi, infine la pulizia.

**Principio.** Il doc 11 ha reso uguale *come i tre viewer leggono il documento*. Qui si rende uguale
*chi decide come si comporta una sezione*: la risposta è **una sola, nel `SectionCatalog`, per profilo**.
Tutto ciò che oggi è un `HashSet` in una pagina, una lista di titoli o un `if` in linea, diventa una
domanda al catalogo.

### 3a. Il catalogo dice anche **chi rende il corpo** (D4, D3, §1b)

`SectionDescriptor` guadagna `SectionBodySource { Blocks, Host }`:
- `Blocks` — il corpo sono i `ContentBlock` della sezione, resi da `SectionBody`/`BlockRenderer`;
- `Host` — il corpo lo produce la pagina (derivate + editoriali-**strutturate**: separazioni,
  configurazioni, VFR, aree regolamentate).

È una proprietà **per profilo**, non globale: nella vLOA «Military areas…» è testo bilaterale (`Blocks`),
sulla vIPI ACC/APP «Aree regolamentate» è un picker (`Host`). La *natura* (`SectionKind`) resta globale,
come da doc 08a — cambia solo chi disegna il corpo.

Nuove API, uniche consumatrici ammesse:
```csharp
SectionCatalog.IsHostRendered(SectionProfile profile, string key)   // sostituisce le 6 copie di §1b
SectionCatalog.IsFixed(SectionProfile profile, string key)          // già esistente: sostituisce i 3 IsMandatory
```
Le chiavi figlie che l'host rende ma che non sono sezioni di primo livello (le due direzioni dei
coordinamenti vLOA) stanno in un **insieme per-profilo separato** dal registro di membership, così
`For(profile)` continua a descrivere solo ciò che si crea alla nascita del documento.

### 3b. «Minime di vettoramento» torna una sezione come le altre (B4)

`KindOf("minima")` → `Editorial`, `BodySource` → `Blocks`. Sparisce il toggle Live/Congelata inerte e la
sezione diventa scrivibile, come deciso il 2026-08-09 (`lavori-aperti.md` §E2). Il componente
`AppMinima.razor` e le sue due chiavi di risorsa si **rimuovono**: il suo testo diventa il **contenuto
iniziale** della sezione (blocco `Callout` info), seminato alla creazione e backfillato una volta sui
documenti esistenti — che oggi hanno lì un blocco placeholder vuoto, invisibile in resa.

### 3c. La vLOA nasce dal catalogo come le altre due (D1, B3, C8)

- `SectionProfile.Vloa` allineato alla **realtà**: `purpose · aor · frequencies · operationaltechnique ·
  coordination · regulated · validity`, con i titoli e l'ordine del documento vero.
- `SectionCatalogBridge.KeyFor(BlockSection.Purpose)` → `"purpose"` (era `null` → chiave `custom:{guid8}`).
- `VloaSections` smette di essere un registro parallelo: resta la sola **sorgente dei contenuti** iniziali;
  chiavi, titoli e ordine li prende dal catalogo. `MandatoryTitles` sparisce (→ `IsFixed`).
- Le due direzioni dei coordinamenti hanno **chiavi proprie**, `coordination:out` (home→foreign) e
  `coordination:in` (foreign→home), invece di ripetere `coordination`. Da qui:
  - la cattura frozen dei coordinamenti torna **una** (C8);
  - editor e viewer identificano la direzione **per chiave**, non per titolo né per posizione;
  - i due paragrafi seminati e mai renderizzati (B3) non si scrivono più, e la riconciliazione al boot
    li rimuove dai documenti esistenti.
- Riconciliazione one-shot in `IDocumentMaintenance` (stesso pattern di `MigrateHiddenSectionsAsync`):
  chiavi delle due direzioni, chiave `purpose`, pulizia dei blocchi invisibili.

### 3d. Le sezioni di catalogo mancanti si aggiungono davvero (D5)

Oggi la vIPI ACC se le **inventa a view-time** (`AccDocumentAssembler.SectionsOf`) e APP/vLOA no. Target:
una riconciliazione al boot aggiunge alla versione di lavoro le sezioni fisse assenti, per tutte e tre le
famiglie. La rete a view-time dell'ACC **resta** — serve agli snapshot di release vecchi, che non si
riscrivono — con il commento che ne spiega il perché.

### 3e. Una sola rotta pubblica per documento (C3, C4, C5, C9)

`IDocRoutesRegistry` (doc 09 §3b) diventa l'**unico** posto che sa come si raggiunge un documento.
Le due copie in Infrastructure (ricerca, «Cosa è cambiato») spariscono a favore di una porta Application
che risolve *tipo → URL pubblico + ancora*. Con essa:
- gli APP standalone puntano a `/vsop/{acc}/apps/vipi?app={callsign}` invece che alla vIPI di ACC;
- l'ancora di sezione la fornisce il descrittore per-tipo, così i deep-link cadono dove devono anche
  sulla vIPI ACC (`p-{blockKey}-{sectionKey}`) e sulle sezioni host dell'APP (`p-{sectionKey}`);
- `SearchScope` guadagna la voce **App**;
- `VloaDocRoutes.EditorUrl` senza vicino ritorna `null` (il chiamante ha già il proprio fallback) invece
  di puntare all'editor di un'altra famiglia.

### 3f. Ricerca e «Cosa è cambiato» vedono ciò che vede il pubblico (C2)

Entrambe filtrano come le pagine: `Document.IsHidden` escluso, `DocumentSection.IsHidden` escluso,
e solo documenti con **release effettiva**. Il gate vive in un solo posto condiviso dai due repository.

### 3g. Snapshot totale davvero totale (C1)

La tabella «Configurazioni» dell'APP si deriva dalle configurazioni **del documento mostrato**, come già
fa la vIPI ACC: `AppnPage` legge il `BodyJson` della sezione `configurations` dal proprio `_view` e lo
passa alla derivazione, invece di richiederlo al service (che risolve la versione di lavoro).
`IAppDocumentService` espone `DeriveConfigTableFromAsync(app, configs)`; l'overload che legge dal
documento resta per l'editor.

### 3h. L'AIRAC è del documento, non dell'orologio (C6)

`AccDocumentModel` porta `AiracCycle` (dallo snapshot, come `RawDocument`); `AppnPage` usa
`_view.AiracCycle`. Riquadro laterale e `PrintMeta` mostrano lo stesso valore nelle tre famiglie: il
ciclo della release che si sta guardando, con fallback al corrente solo quando non c'è release.

### 3i. Un solo involucro per il pannello release (A1, A2, A3)

Il blocco «Versioni & release» (ancora `p-release`, titolo, aiuto, voce TOC) entra **dentro**
`ReleasePanel` come opzione, invece di essere ricostruito da ogni editor. I quattro editor passano gli
stessi parametri: `ShowDiff` sempre, `AllowCancel` = admin, `PreviewUrlFactory` sempre. Le chiavi di
risorsa `App_VersionsReleases`/`App_ReleaseHelp` si rinominano `Rel_SectionTitle`/`Rel_SectionHelp`.
`VersioniPage` mostra la «Storia modifiche» a tutte le famiglie che hanno un `DocumentId`.

### 3j. Superficie viewer uniforme (B1, B2, C7, E3, F1-F7)

- `AppnPage` rispetta `IsInitiallyCollapsed` (anche sul ramo «nascosta in bozza», come vLOA).
- `VloaDocumentView` rende le sotto-sezioni extra di «Coordination» negli slot Before/After.
- `AccLanding` applica alla card vIPI lo stesso gate release delle altre tre.
- `?live=1` resta un alias solo dell'APP (era suo) ma lo dichiara il commento, non l'omissione altrui.
- Tutto il testo visibile passa da `SharedResource`: `VloaDocumentView`, `VloaListPage`, `AppsListPage`,
  le due schede AoR degli editor, le etichette di `ReleaseService.DiffAsync` (che tornano al chiamante
  come chiavi, non come frasi), il prefisso `[Bozza]`, l'etichetta del sommario, il titolo «non
  disponibile» dell'APP. Le intestazioni della tabella frequenze diventano **una sola** definizione
  condivisa fra editor e viewer vLOA.

### 3k. Pulizia (D2, E1, E2, G1-G6)

Si rimuovono: `BuildAccVipiAsync`/`LoadAccVipiAsync` con `ResolveLiveAorAsync`/`DefaultViewer` e le tre
dipendenze che restano orfane; `BuildVloaByPairAsync`/`LoadVloaByPairAsync`; `SectionCatalog.Reconcile`.
Il parametro `live` di `IVipiViewService` resta (il suo motore, `ContentService`, è coperto da test e
serve alla vista live) ma il commento dice il vero. L'anteprima release dell'ACC passa da
`GetPreviewAsync` come le altre due. `mappa-pagine.md`, i commenti di `ReleaseService`,
`VersioniPage` e `SectionCatalog` si allineano al codice.

---

## 4. Passi di migrazione

Un passo = un commit (o più, se il passo è grosso), build verde e suite verde a ogni passo.

| # | Passo | Chiude | Tocca |
|---|---|---|---|
| **S1** ✅ | `SectionBodySource` nel catalogo + `IsHostRendered`; le 6 copie e i 3 `IsMandatory` chiamano il catalogo | D3, D4, §1b | `SectionDescriptor`, `SectionCatalog`, `AccSectionBody`, `AccEditorPage`, `AppnPage`, `AppEditorPage`, `VloaEditor`, `VloaDocumentView` |
| **S2** ✅ | «Minime» editoriale: `KindOf`→Editorial, contenuto iniziale + backfill, via `AppMinima` | B4 | `SectionCatalog`, `AppMinima.razor` (rm), 4 host, `IDocumentMaintenance`, `.resx` |
| **S3** ✅ | Profilo vLOA reale nel catalogo + `purpose` + `VloaSections` solo contenuti + `IsFixed` | D1 | `SectionCatalog`, `SectionCatalogBridge`, `VloaSections`, `VloaStructureSeeder`, `VloaEditor` |
| **S4** ✅ | Chiavi `coordination:out`/`:in` + identificazione per chiave + cattura unica + riconciliazione | B3, C8 | `SectionKeys`, `SectionCatalog`, `VloaSections`, `VloaEditor`, `VloaDocumentView`, `EfDocumentMaintenance` |
| **S5** ✅ | Sotto-sezioni extra di «Coordination» rese dal viewer | B2 | `VloaDocumentView` |
| **S6** ✅ | Sezioni di catalogo mancanti aggiunte al boot per tutte e tre | D5 | `IDocumentMaintenance`, `EfDocumentMaintenance`, `VipiModuleExtensions` |
| **S7** ✅ | Config-table APP dal documento mostrato | **C1** | `IAppDocumentService`, `AppDocumentService`, `AppnPage`, `AppEditorPage` |
| **S8** ✅ | AIRAC del documento in ACC e APP | C6 | `AccDocumentModel`, `AccDocumentService`, `AccVipiPage`, `AppnPage` |
| **S9** ✅ | Porta di rotta pubblica unica + ancore + scope App | C3, C4, C9 | `IDocRoutesRegistry` (→ Application), `EfSearchRepository`, `EfChangesRepository`, `SearchModels`, `SearchPage` |
| **S10** ✅ | Gate di visibilità su ricerca e «Cosa è cambiato» | **C2**, C5 | `EfSearchRepository`, `EfChangesRepository` |
| **S11** ✅ | `ReleasePanel` con involucro proprio; 4 editor allineati; storia modifiche per tutti | A1, A2, A3 | `ReleasePanel`, i 4 editor, `VersioniPage`, `.resx` |
| **S12** ✅ | Gate release sulla card vIPI della landing | C7 | `AccLanding` |
| **S13** ✅* | Anteprima release ACC via `GetPreviewAsync` | E1, E2 | `AccVipiPage`, `AccDocumentService`, `IReleaseService` |
| **S14** ✅ | Localizzazione: `AppsListPage`, `VloaListPage`, `VloaDocumentView`, schede AoR, `[Bozza]`, sommario, titolo APP; intestazioni frequenze condivise | F1-F7 | pagine + `.resx` |
| **S15** ✅ | Etichette diff dal chiamante (chiavi, non frasi) | F4 | `ReleaseService`, `ReleaseDiffRow`, `ReleaseDiffTable`, `.resx` |
| **S16** ✅ | Rimozione codice morto | D2, G1, G2 | `IVipiViewService`, `VipiViewService`, `IContentRepository`, `EfContentRepository`, `SectionCatalog` |
| **S17** ✅ | Propagazione documentale: `mappa-pagine.md`, commenti stantii, `rounds.md`, `00-overview`, memorie | G3-G6 | docs + memorie |

### Scostamenti dall'ordine e dalla portata dichiarati

- **S7 eseguito prima di S6.** La fuga della bozza in pubblico era il difetto più grave dell'elenco e non
  dipendeva da S6: chiuderlo prima costava nulla e valeva molto.
- **S13 ridotto, con ragione.** L'idea era far passare anche l'anteprima release della vIPI ACC da
  `IReleaseService.GetPreviewAsync`, come APP e vLOA. Guardandola da vicino non c'è niente da guadagnare:
  le due strade leggono lo **stesso** `DocReleasePayload`, sono gated allo stesso modo e verificano entrambe
  l'identità del bersaglio; quella dell'ACC in più ne **assembla i blocchi**, cioè fa il lavoro che alla
  pagina serve. Sostituirla significherebbe rimpiazzare un percorso tipizzato e coperto da test con uno
  generico più il riassemblaggio a mano nella pagina, senza un solo effetto per chi guarda il documento.
  È rimasta la parte che valeva: il commento di `GetPreviewAsync` diceva «Doc null per ACC/APP», falso dal
  doc 08, e ora dice come stanno le cose.

## 5. Impatto / Verifica

- **Baseline** (2026-08-10, pre-lavoro): build verde, **1335 test** verdi
  (Domain 23 · Hosting 46 · Application 396 · Ui 124 · AuroraBridge 77 · Infrastructure 324 net10 +
  333 net8 · E2E 12).
- Ogni passo aggiunge i test che presidiano la regola che introduce; in particolare:
  - S1: un test che fallisce se una pagina reintroduce un insieme di chiavi proprio;
  - S4: riconciliazione idempotente (due giri, stesso risultato);
  - S7: la pagina pubblica non vede una configurazione salvata solo in bozza;
  - S10: un documento nascosto e una sezione nascosta non compaiono nella ricerca.
- **Verifica live obbligatoria** a fine giro (skill `verifica-live`): i viewer sono Blazor, e le
  regressioni di resa non le vede `dotnet test` — vedi [[dev-process-gates]].
