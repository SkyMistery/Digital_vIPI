# Feature — Stampa dei documenti (CSS `@media print`)

Data: 2026-07-30 · Stato: FATTO (build 0 warning, 633 test verdi, verifica live con printToPDF su aeroporto
LIBD, vIPI ACC LIBB, vLOA LIBB↔LDZO). Copre RNF-6 del piano (§10, §22.7): consultazione **offline in cabina**.

## Obiettivo
Rendere stampabili i documenti pubblici (`/vsop`) con la sola stampa del browser — nessun endpoint di export
server-side. La resa in PDF è quella di «Salva come PDF» del browser.

## Stato di partenza (rilevato)
`vipi-theme.css` aveva già un blocco `@media print`, **inservibile e dannoso**:

| Problema | Effetto |
|---|---|
| `body * {visibility:hidden}` + solo `.printable` visibile, ma **nessun markup applicava `.printable`** | Ctrl+P su qualunque pagina → **foglio bianco** |
| `details > .body` (la classe reale di `CollapsibleBlock` è `.cb-body`), `.chev` per il chevron di blocco (reale `.cb-chev`) | sezioni comunque vuote |
| `details { open: true }` | dichiarazione inesistente, no-op |
| contenuto in `position:absolute` | paginazione multi-pagina rotta |

Vincoli del contesto già presenti:
- lo zoom di pagina è **inline su `<html>`** (`App.razor`, persistito in localStorage) → in stampa va azzerato;
- `.topbar`/`.toc`/`.doc-rail` sono **sticky** e il TOC ha `max-height:100vh` + `overflow:auto` → sulla carta
  clippano;
- la tabella SID sta in un wrapper `overflow:auto` e ha `min-width:720px`;
- `CollapsibleBlock` è un `<details>` nativo col **collasso persistito** (`data-persist`);
- gli stili del modulo sono confinati in `.vipi-root` (il modulo non tocca `body`/`html` dell'host).

## Design
- **Foglio dedicato** `src/Vipi.Ui/wwwroot/vipi-print.css` (registrato in `App.razor` con cache-busting e in
  `VersionedAssets`), non un blocco in coda al tema da 1150 righe.
- **Nascondi il chrome, lascia il contenuto nel flusso**: nessun opt-in per pagina (`.printable` non esiste
  più), quindi *ogni* pagina `/vsop` è stampabile e la paginazione è quella naturale del browser.
- Regole confinate in `.vipi-root`; globali solo `@page` e il reset dello zoom, che per natura non sono
  scopabili.
- **A4 verticale** con tabelle a corpo 8,5pt e `overflow-wrap`: SID (9 colonne) e Piste (8) stanno nei 186 mm
  utili senza landscape (scelta esplicita: la prosa in landscape si legge male).
- **Scala tipografica di stampa**: a schermo il tema parte da 16px con `h2` 32px e `h3` 28px — misure da
  monitor, enormi su A4 (28px ≈ 21pt). In stampa: base 9,5pt, `h1` 15 / `h2` 13 / `h3` 11,5 / `h4` 10pt,
  tabelle 8,5pt, pill 7,5pt, card `.block` più compatte. **Non basta ridurre la base** su `.vipi-root`: gli
  elementi del tema hanno `font-size` espliciti in px e non erediterebbero, quindi i portatori di testo sono
  elencati uno per uno. I titoli del tema usano `:where()` (specificità 0) → nessun `!important` necessario.
  Effetto: documento aeroporto **3 → 2 pagine**, vIPI ACC 36 → 34, poi 28 con le mappe rimpicciolite.
- `thead { display: table-header-group }` → l'intestazione della tabella si ripete a ogni pagina.
- `print-color-adjust: exact`: le tinte **portano informazione** (piste ARR/DEP, riga TL col QNH corrente,
  callout, pill) e i browser di default le scartano.
- **`PrintMeta.razor`** (`.print-only`): titolo, ambito, ciclo AIRAC, ora, URL. Sostituisce l'identità che il
  chrome nascosto non dà più; il `.doc-head` che la segue è nascosto per non ripetere il titolo. Prima pagina
  solo — l'intestazione ripetuta per foglio non è ottenibile in CSS puro cross-browser; numero di pagina e
  totale li aggiunge il browser.
- **Tasto «Stampa»** (`btn ghost`, `onclick="window.print()"` — HTML puro, funziona anche in SSR statico) nel
  `doc-head` dei quattro viewer documento, visibile a **tutti**: serve al controllore, non allo staff.
- **Mappe AoR rimpicciolite per la carta** (`resizeMaps` in `wirePrint`): il CSS da solo non basta — Leaflet
  tiene la propria dimensione in memoria, quindi cambiare l'altezza da foglio di stile **ritaglia** la mappa
  invece di riadattarla. Serve `invalidateSize(false)` + il refit sui settori accesi, esposto da `vipi-aor.js`
  su `_leafletMap` / `_aorRefit` (il refit è stato aggiunto anche al percorso mappa-singola, così il contratto
  è uniforme fra i due tipi). Le miniature per-area (`.area-map`, decine su una ACC, in griglia accanto al
  testo) si toccano **solo in altezza** e solo verso il basso: 190 → 130px (≈34 mm).
- **Cornice dell'AoR con le proporzioni dell'area** (`frameWidth`): `fitBounds` sceglie lo zoom che fa stare i
  bounds in **entrambe** le dimensioni, quindi in una cornice larga e bassa (703 × 200) un AoR alto e stretto
  come LIBB è limitato dall'altezza → zoom basso, mezzo Mediterraneo attorno a un poligono minuscolo. In stampa
  la cornice prende la forma dell'AoR: altezza 260px e larghezza dedotta dal rapporto fra le due proiezioni
  Mercator dei bounds (`map.project(nw, z)` / `project(se, z)`, lo zoom è irrilevante perché serve solo il
  rapporto), con `margin: 0 auto` che la centra nel foglio. I bounds inquadrati sono esposti da `vipi-aor.js`
  come `el._aorBounds`. Le misure da ripristinare vivono sull'elemento (`_printPrevH`, `_printPrevW`), non in un
  array indicizzato che si disallineerebbe a un re-render Blazor.
- **Separazioni radar compatte**: a schermo sono un riquadro bordato 2px con griglia a 3 colonne (la terza
  vuota, le separazioni sono due) e valori a 16px — mezza pagina per dire «1000 ft / 5 NM». In stampa diventano
  due targhette in fila con etichetta e valore sulla stessa riga.
- **`wirePrint()` in `vipi-ui.js`**: apre i `<details>` su `beforeprint` e li richiude su `afterprint`, con la
  persistenza sospesa (le preferenze di collasso dell'utente non cambiano). Il CSS non basta: in Chrome il
  contenuto di un `<details>` chiuso è nascosto dallo user-agent (`content-visibility` su
  `::details-content`). Safari non emette quegli eventi → in parallelo si ascolta il cambio di media `print`.

## Cosa resta stampato per scelta
**PreviewBanner** (bozza / anteprima release) e **DocReviewBar**: una copia cartacea non deve poter passare per
pubblicata. Restano anche i chip-legenda dell'AoR, le mappe (Leaflet o l'SVG di fallback) e l'attribuzione
delle tile (licenza della sorgente cartografica).

## Fuori dalla carta per scelta (dati live)
- **METAR & TAF** del documento aeroporto (`CollapsibleBlock Id="a-meteo"` → `ExtraClass="noprint"`): è un dato
  live, su carta sarebbe un'istantanea scaduta; inoltre solo il tab attivo (METAR *o* TAF) è nel DOM.
- **Ridotta** (`RidottaPage`, `RidottaAppPage`): il piano §22.7 esclude la ridotta/kneeboard dall'export. Le due
  pagine sono disabilitate dal Round 12 (nessun `@page` → irraggiungibili), ma portano già `noprint` sul
  contenuto e un avviso `.print-only`, così la regola vale se la rotta rientra.

## Fuori scopo
- Endpoint di export PDF server-side (headless): la stampa del browser copre RNF-6.
- Editor e pagine admin: sono pagine di lavoro. Ereditano comunque le regole generiche.

## Passi (un commit per slice, build verde a ogni passo)
1. `vipi-print.css` + registrazione in `App.razor`, rimozione del blocco morto dal tema, regole base.
2. `wirePrint()` in `vipi-ui.js` (apertura/ripristino `<details>`), `.coord-tools` fuori dalla carta.
3. `PrintMeta.razor` + chiavi i18n (`Common_Print`, `Print_PrintedAtLabel`) + tasto Stampa nei quattro viewer.
4. Rifiniture dalla verifica live (sotto).

## Verifica live (Edge + CDP, skill `verifica-live`)
Driver dedicato: chiude una sezione (simula il collasso persistito), porta lo zoom a 1.3, emula il media
`print`, misura cosa resta visibile, dispatcha `beforeprint`/`afterprint`, poi produce il PDF A4 con
`Page.printToPDF` e ne rilegge il **testo pagina per pagina**.

Esito: chrome assente, `PrintMeta` presente, sezioni collassate aperte e **poi richiuse**, zoom neutralizzato,
nessuna riga di tabella tagliata, intestazione della tabella SID ripetuta a pagina 3, nessun errore in pagina.
Pagine: aeroporto **3**, vLOA **3**, vIPI ACC **36** (documento realmente lungo).

Tre correzioni nate dalla verifica:
- chevron dei `<details>` (`.cb-chev`, `.chev`) fuori dalla carta: l'apri/chiudi non esiste su un foglio;
- i testi che spiegano un'**interazione** («accendi/spegni i settori sopra la mappa», «apri una
  configurazione», la nota sui setting evidenziati) marcati `.noprint`;
- **BUG**: Chrome segnala la stampa **due volte** (`beforeprint` *e* passaggio a media `print`). La seconda
  apertura ripartiva da una pagina già espansa e raccoglieva un elenco vuoto → dopo la stampa le sezioni chiuse
  dall'utente restavano aperte. Risolto con una guardia di idempotenza su `expand`/`restore`.

## Limiti noti
- L'intestazione `PrintMeta` è sulla prima pagina, non su tutte (vedi Design).
- Restano stampati altri elementi **derivati dal METAR**, perché appartengono a sezioni documentali: la pista
  consigliata (evidenza verde/blu + «Wind 350° / 6 kt» nella legenda) e la pill «current QNH» nella tabella dei
  livelli di transizione. Se anche questi devono sparire dalla carta è una decisione editoriale, non un limite
  tecnico.
- **APP non remotizzato**: la modifica al `doc-head` è identica a quella dei fratelli, ma il DB di sviluppo non
  ha alcun documento APPn pubblicato né in bozza → non verificata a schermo, solo in build.

## File toccati
- Nuovi: `src/Vipi.Ui/wwwroot/vipi-print.css`, `src/Vipi.Ui/Components/PrintMeta.razor`,
  `tests/Vipi.Ui.Tests/PrintMetaTests.cs`, `docs/feature/2026-07-30-stampa-documenti.md`.
- Modificati: `src/Vipi.Host/Components/App.razor`, `src/Vipi.Ui/wwwroot/vipi-theme.css`,
  `src/Vipi.Ui/wwwroot/vipi-ui.js`, `src/Vipi.Ui/Pages/AeroportoPage.razor`, `AccVipiPage.razor`,
  `AppnPage.razor`, `src/Vipi.Ui/Components/VloaDocumentView.razor`, `Components/App/AccAor.razor`,
  `Components/App/AccSectionBody.razor`, `Components/Blocks/AorBlock.razor`,
  `src/Vipi.Ui/Resources/SharedResource{,.en}.resx`, `docs/design/piano-vipi-tool.md`, `HANDOFF.md`.

## Tabelle dei coordinamenti: larghezze uniformi (schermo **e** stampa)
Le colonne cambiavano larghezza da una tabella all'altra. Causa: `<table>` senza larghezze specificate →
`table-layout: auto`, quindi ogni tabella si dimensiona sul **proprio** contenuto, e i coordinamenti di una ACC
sono decine di tabelle (una per aeroporto/flusso). Misurato su LIBB: **19 combinazioni** di larghezze diverse.

Fix: classi per colonna **semantica** sulle intestazioni (`c-flow`, `c-cop`, `c-level`, `c-next`, `c-cond`) +
`.coord-table { table-layout: fixed }` con larghezze fisse in percentuale; «Prossimo» non ha larghezza e prende
il resto. Le classi servono perché «Flusso» e «Condizione» compaiono solo se ci sono dati: la stessa colonna
cade in **posizioni diverse** da una tabella all'altra, quindi regole per posizione non allineerebbero nulla.
Verificato live: 31 tabelle, **1 sola** combinazione di larghezze (schermo 124/95/510, stampa 114/87/469); +1
pagina sul vIPI ACC (28 → 29) perché le colonne fisse mandano a capo qualche cella in più.

## Refuso corretto strada facendo
La legenda piste usciva «recommended**from** the METAR wind» / «consigliati**dal** vento METAR», a schermo e in
stampa: **Razor scarta il testo di sola spaziatura che precede un blocco di codice** — e lo scarta anche se lo
si scrive dentro `<text>`. Lo spazio va messo come **entità**: `@((MarkupString)L["Airport_RwyLegend"].Value)&#32;`
prima dell'`@if` che sceglie fra `Airport_RwyByRule` e `Airport_RwyByWind` (`AeroportoPage.razor`). Verificato sul
testo reso dal browser, non sul sorgente HTML: «green for arrivals and blue for departures recommended from the
METAR wind. Wind 360° / 8 kt.»
