# Audit frontend/UI — 23 agosto 2026

**Ramo:** `audit-frontend-ui` · **Stato:** ✅ **eseguito lo stesso giorno**, sei commit, verifica live fatta.
Suite dopo l'esecuzione: **3.595 test verdi** su net8 e net10 (14 assiemi), build a zero avvisi.

Revisione di tutta l'interfaccia: `src/Vipi.Ui` (100 `.razor`, 24k righe), `src/Vipi.Host/Components`, i 13
file JS e i 5 CSS di `wwwroot`. Non riparte da `lavori-aperti.md`: cerca quello che non si sa.

**Metodo.** Lettura del sorgente, poi **misura** — build reale, suite eseguita, e la verifica a schermo con
la skill `verifica-live` (Edge reale su copia del `vipi.db`). Dove una cosa è misurata lo dico; dove è
dedotta, lo dico lo stesso.

**Esito in una riga.** Il codice è di qualità alta e pesantemente documentato: quasi tutto ciò che sembra
strano ha accanto il commento che spiega perché, e in molti casi il commento è la prova che il problema era
già stato visto. Quello che è rimasto aperto ha però un filo comune che vale più del singolo difetto:

> ⚠️ **Tre difetti su tredici nascono da regole che il progetto aveva già scritto, applicate a metà.** La
> regola sui `<span>` cliccabili è scritta per esteso in `Chip.razor`; la regola «non caricare 592 KB su ogni
> pagina» è scritta in `App.razor` per three.js; la regola «il fuoco si deve vedere» è in `vipi-theme.css`.
> Ognuna aveva un punto del codice che le sfuggiva — e sfuggiva perché quel punto era arrivato da un'altra
> strada (JS puro invece di Blazor, un `<link>` invece di uno `<script>`, un campo che azzera l'outline).

---

## Riepilogo per gravità

| # | Difetto | Gravità | Verificato? |
|---|---|---|---|
| T1 | L'ACC corrente non è più evidenziato in topbar dal rename del 22 agosto | **Alta** (funzionale) | ✅ riprodotto e presidiato |
| A1 | I comandi AoR non esistono per la tastiera — **pagine pubbliche** | **Alta** | ✅ misurato a schermo |
| A2 | Nessuna pagina ha un `<h1>`; sulle vIPI titolo e sezioni allo stesso livello | **Alta** | ✅ 30 pagine su 32 |
| A3 | Il documento scende a **161px** a zoom 1.8: la soglia non scatta mai | **Alta** | ✅ misurato |
| A4 | Fuoco da tastiera invisibile in tre campi (`outline:0` senza sostituto) | Media | ✅ letto e misurato |
| A5 | `prefers-reduced-motion` non nominato: 42 transizioni, un'animazione infinita | Media | ✅ contato |
| A6 | Le live region nascono insieme al messaggio → non annunciate | Media | ✅ letto, ~10 pagine |
| A7 | Chip AoR spente a 3,3:1 (opacità sull'intero elemento, testo compreso) | Media | ✅ misurato |
| A8 | Salti di livello nei titoli (h1→h3, e cinque h1→h4) | Media | ✅ 20 pagine |
| R1 | `vipi-zoom.js` tocca `localStorage` senza protezione → spegne il riaggancio | Media | ✅ letto |
| R2 | `vipi-boot.js` è una catena senza try/catch: il primo errore spegne gli altri sei | Media | ✅ letto |
| R3 | `AorColorScheme.Resolve` promette una validazione che non fa | Bassa | ✅ letto |
| P1 | Leaflet (162 KB) caricato su ogni pagina, comprese quelle senza mappe | Media | ✅ misurato |
| P2 | `vipi-print.css` (13 KB) render-blocking su ogni pagina | Bassa | ✅ letto |
| P3 | ~25 stringhe italiane nel markup, in un modulo localizzato it/en | Bassa | ✅ contate |

---

## T1 — L'ACC corrente, perso col rename

`SopLayout.razor` leggeva il codice ACC così:

```csharp
// /services/vsop/{acc}/{vipi?}
_acc = segments.Length >= 2 && segments[0].Equals("vsop", …) ? segments[1] : null;
```

Il commento diceva già l'indirizzo giusto; era il codice a essere rimasto indietro. Dal 22 agosto
`segments[0]` vale `"services"`, quindi **`_acc` è sempre `null`**: nessun ACC prendeva la classe `active`,
e `aria-current="page"` non è più stato emesso — in entrambe le navigazioni, riga desktop e menù del
telefono. Chi guardava la vIPI di Roma non vedeva dov'era, e uno screen reader non aveva modo di dirlo.

⚠️ **Perché nessuno se n'era accorto per un anno:** una stringa sbagliata compila benissimo, i test unitari
non guardavano l'HTML servito, e a occhio «nessun ACC acceso» somiglia a «nessun ACC selezionato».

**Rimedio.** Il conto passa a `Vipi.Ui/VsopRoutes.AccFrom`, e il prefisso torna a stare in un posto solo:
`LegacyRoutes.Prefix` rimanda lì invece di ripetere la stringa. Un prossimo rename rompe la compilazione,
non il layout. Presidio: `TopbarAccNavTests` (E2E, guarda l'HTML servito — è lì che il difetto si vedeva) e
`VsopRoutesTests`. **Verificato che il primo fallisce sul codice di prima.**

> 💡 Prova indipendente, e piacevole: dopo il fix `sweep.js` ha ricominciato a segnalare i **2 falsi positivi
> noti** «pastiglia ACC attiva». Erano spariti insieme al bug — il falso positivo documentato nella skill era
> diventato invisibile perché nessuna pastiglia era più attiva.

---

## A1 — I comandi AoR esistono solo per il mouse

`Chip.razor` porta scritta la regola, per esteso:

> Uno `<span>` con un gestore di click è un comando che esiste solo per il mouse: non entra nel giro del
> tabulatore, non risponde a Invio o Spazio, e chi usa uno screen reader lo sente come testo.

Il blocco AoR la violava integralmente, **sui documenti pubblici**: chip per-settore `<span>`, «Tutti /
Nessuno / Azzera» `<a>` **senza href**, scelta configurazione `<span>`. Nessuno raggiungibile col
tabulatore, nessuno che risponde a Invio/Spazio, nessuno che espone il proprio stato acceso/spento.

⚠️ **Perché la regola l'aveva mancato:** `Chip.razor` non è utilizzabile lì. Quelle pagine sono SSR statiche
e l'interattività è **JS puro** (`vipi-aor.js`, `vipi-ui.js`), non Blazor — quindi il markup era rimasto
quello di prima e nessuno lo aveva ricollegato alla regola.

**Rimedio.** `<button type="button">` con `aria-pressed`: il tag porta Invio/Spazio e il ruolo, `aria-pressed`
porta lo **stato**, che altrimenti resterebbe solo il colore. Lo stato si scrive da un posto solo (`segna` in
`vipi-aor.js` e in `vipi-ui.js`, più il capo 3D in `vipi-aor3d.js`). Il reset che un `<button>` si porta
dietro si paga una volta sola nel foglio — è la ragione per cui `Chip.razor` aveva scelto lo `<span>`, e qui
è il prezzo giusto.

---

## A2 e A8 — I titoli: nessun `<h1>`, e venti pagine che saltano un gradino

30 pagine su 32 non avevano un `<h1>`. Il titolo di pagina era un `<h2>` — e sulle vIPI lo erano **anche i
blocchi**, quindi titolo e sezioni allo stesso livello: chi legge un documento operativo lungo navigando per
intestazioni non aveva una gerarchia da seguire.

Chiuso quello, restava il rovescio: **venti pagine saltavano un gradino** (h1→h3, e cinque h1→h4).

⚠️ **La causa era una sola, e vale più dei venti fix:** il tag veniva scelto per la **misura**, non per il
posto in gerarchia. Un titolo di sezione era `<h3>` perché 28px è la misura giusta, non perché stesse al
terzo livello. Le due cose sono ora separate — il tag dice la struttura, la misura la porta una classe
(`.page-h1`, `.h-sect`, `.h-card`) o il selettore di contesto che già c'era.

⚠️ **Venti regole del foglio erano legate al TAG** (`.apt-card h4`, `.nav-head h4`, `.section-title h3`,
`.guida-toc h3`, `.swap-card h4`…): ritaggare senza toccarle le avrebbe spente in silenzio — è la regola di
propagazione dei runbook. Quelle interessate sono ora **indifferenti al tag** (`:is(h2,h3,h4)`), il che è
meglio di una classe in più: il prossimo che ritagga non rompe niente e non deve sapere che quella riga esiste.

**Le intestazioni della barra laterale non sono state ritaggate.** Indice, «Riepilogo», «Collegamenti» sono
una regione a sé e ora lo dicono — gli `<aside>` hanno un `aria-label`. Farle sembrare sezioni del documento
per far contento un contatore sarebbe il contrario del vero, e il test le esclude scrivendo perché.

> ⚠️ **La prova che il disegno non cambia è MISURATA, non affermata.** 216 titoli su 15 pagine, in tema chiaro
> e in modalità compatta, fotografati prima e dopo (misura, peso, colore, famiglia, margini). **Il primo giro
> ha trovato sei regressioni vere** che sarebbero passate: un colore scivolato da `--brand-ink-2` a
> `--brand-ink` su 19 titoli, la compatta che rimpiccioliva a 23px chi non doveva, il peso da 700 a 800, e
> `ProfileSwapperPage` promossa **due volte** (15px → 28px, perché compariva in due liste di lavoro). Corrette;
> il secondo giro dice «nessuna differenza».

---

## A3 — Il documento a 161px, e la terza ricaduta della stessa malattia

Terza volta, dopo la topbar (22 agosto) e le tabelle del viewer (23 agosto mattina): **una `@media` misura la
FINESTRA**, mentre lo zoom di questa applicazione è `zoom` sull'`<html>` e la finestra non lo vede.

Misurato su `/services/vsop/libb/vipi` a 1600px di finestra: la soglia dei 1080px **non scattava a nessuno
zoom**, mentre le due barre laterali restavano larghe fisse (248 e 308) e tutto il costo cadeva sulla colonna
del documento.

| zoom | 1.0 | 1.2 | 1.4 | 1.6 | 1.8 |
|---|---|---|---|---|---|
| **prima** | 872 | 605 | 415 | 272 | **161** |
| **dopo** | 872 | 605 | 1031 | 888 | 777 |

Centosessantun pixel di documento fra due barre da 248 e 308. E chi zooma a 1.8 è **esattamente chi ha
bisogno di leggere meglio**: il rimedio peggiorava il caso che doveva servire.

**Rimedio.** Una `@container`, che misura il contenitore in **unità di layout** — cioè proprio la quantità
che lo zoom cambia. A zoom 1.0 e 1.2 non cambia niente: il disegno desktop è quello di prima.

⚠️ **Il contenimento sta su `.wrap:has(> .doc-layout)` e non su `.wrap`**, e non è pignoleria:
`container-type:inline-size` porta con sé `contain:layout`, che rende l'elemento contenitore anche per i
discendenti `position:fixed`. Le pagine di **editor** hanno un `.editor-toast` fisso dentro il `.wrap`, e
glielo avremmo incollato dentro. Verificato a schermo: i viewer hanno il contenimento, gli editor no e il
loro toast resta fisso alla finestra.

**Controprova a finestra stretta** (lo scopo per cui la soglia era nata): 1600→3 colonne, 1200→2,
1000/900/700/375→1, indice non più appiccicato, zero sforo orizzontale.

⚠️ **Nota di metodo, costata mezz'ora.** In **Edge 151** `documentElement.clientWidth` **non è più in unità di
layout**: sotto `zoom` restituisce i px di finestra. La prima passata di misure, che deduceva da lì, diceva
che non succedeva niente. La domanda va fatta a `matchMedia`, che è ciò che decide davvero se la regola vale.

---

## A4 — Il fuoco che non si vede

`vipi-theme.css` scrive la regola («Il fuoco si deve VEDERE: senza, chi naviga da tastiera non sa dove si
trova») e la applica a otto selettori. Ma `.searchbar input` e `.sid-search input` hanno `outline:0`
**senza alcun sostituto** — lì il fuoco è semplicemente invisibile — e non c'era nessuna regola
`:focus-visible` di base: tutto il resto dipendeva dall'anello predefinito del browser, che sul blu di brand
e sulle superfici scure si vede male.

**Rimedio.** Un pavimento `:focus-visible` per tutto il modulo (a specificità zero, così le otto regole
puntuali continuano a vincere), la variante chiara sul fondo di brand, e l'anello **sul contenitore** per i
campi che azzerano l'outline apposta: la cornice che si vede è quella, e un anello dentro un riquadro già
disegnato si legge come un difetto.

---

## A5 — Il movimento che non si può ridurre

Nell'intero `wwwroot`: 42 `transition`, 3 `@keyframes` — di cui `pulse` sul badge live, che è **infinita** —
e 4 scorrimenti `behavior:'smooth'`. **Zero** occorrenze di `prefers-reduced-motion`.

Non è una rifinitura: per una parte delle persone un contenuto che si muove da solo dà nausea o innesca
un'emicrania (WCAG 2.3.3).

**Rimedio.** Blocco in coda al foglio (durate azzerate, animazioni a un ciclo) e `vipiScorrimento()` per i
quattro punti che stanno nel JS, **dove nessuna media query arriva**: `behavior:'smooth'` è una stringa
scritta nel codice.

---

## A6 — Le live region che nascono col messaggio

Su ~10 pagine admin il messaggio d'esito era scritto così:

```razor
@if (_msg is not null) { <span role="status">…</span> }
```

Non funziona. Uno screen reader annuncia i cambiamenti che avvengono **dentro** una live region che stava
già lì; una regione che entra nel DOM nello stesso istante in cui compare il testo in genere non viene letta
affatto. L'esito di un salvataggio si vedeva e non si sentiva — cioè mancava a chi non ha altro modo di
sapere se il salvataggio è andato.

**Rimedio.** `LiveRegion.razor`: il contenitore è sempre reso e cambia solo il contenuto. `display:contents`,
perché le testate sono flex con `gap:10px` e un elemento in più — anche largo zero — ci lascerebbe un vuoto
permanente. ⚠️ Su `ConfinantiAdminPage` il tasto «Verifica» resta **fuori** dalla regione: dentro, ogni
ridisegno lo farebbe annunciare.

---

## R1 e R2 — Due modi di rompersi in silenzio, uno dentro l'altro

`vipi-theme-mode.js` spiega da mesi perché `localStorage` va protetto («in navigazione privata il solo
ACCESSO è un'eccezione»). `vipi-zoom.js`, il suo gemello, faceva lo stesso accesso **senza protezione**.

⚠️ **Il prezzo non era lo zoom.** `vipiApplyZoom` è la **prima riga** di `vipi-boot.js`, che era sette
chiamate in fila senza try/catch: l'eccezione spegneva tutto il riaggancio dopo ogni navigazione
«enhanced» — chip AoR, mappe, persistenza del collasso, misura della topbar. In navigazione privata, metà
dell'applicazione ferma.

**Rimedio.** Il caso è chiuso nel suo file (try/catch in lettura e scrittura, come il gemello); `vipi-boot.js`
mette ogni chiamata nel proprio try/catch e toglie **l'intera classe** di guasti a cascata, compresa la prossima.

---

## P1 — Leaflet su ogni pagina

162 KB (script + foglio) nel `<body>` di **ogni** pagina — ricerca, incarichi, elenchi admin, guida, login,
hub — mentre poche righe sotto, nello stesso `App.razor`, era scritta la regola opposta per three.js: «NON
caricato qui: sono 592 KB che servono al solo tab 3D».

**Rimedio.** Lo stesso schema, già collaudato: gli URL con impronta passano come `data-leaflet-*` e li carica
`vipi-aor.js` alla prima `.aor-leaflet`. Il ripiego SVG copre l'attesa. Misurato a schermo: zero richieste su
ricerca/hub/struttura, e sulla vIPI le **75 mappe** si disegnano tutte.

---

## Esito dell'esecuzione — 23 agosto 2026

Sei commit sul ramo `audit-frontend-ui`.

| Commit | Cosa chiude |
|---|---|
| `bb5c65b` | T1 — l'ACC corrente in topbar, `VsopRoutes`, due test |
| `991cefb` | A1, A2, A5, A4, A7 — tastiera sui comandi AoR, `<h1>`, movimento ridotto, fuoco, contrasto |
| `8f2b227` | A6, R1, R2, R3 — live region, i due modi di rompersi in silenzio, il colore validato |
| `60e05af` | P1, P2, P3 — Leaflet a richiesta, `media="print"`, 16 stringhe al resx |
| `13b3158` | A8 — i salti di livello, con la prova misurata a 216 titoli |
| `f9783c6` | A3 — la `@container` del viewer |

**Verifica live** (Edge reale, copia del `vipi.db`): ACC evidenziato con `aria-current="page"` e gli altri
no; un solo `h1` a 32px come prima; chip `BUTTON` che prende il fuoco e ribalta `aria-pressed`; chip spenta a
**5,89:1** in chiaro e **8,37:1** in scuro (era ~3,3:1); Leaflet assente dove non serve e le 75 mappe
disegnate dove serve; `vipiScorrimento()` → `auto` sotto reduced-motion; live region `display:contents` con
testata invariata; documento da 161 a 777px a zoom 1.8. `sweep.js`: solo i 2 falsi positivi noti. Zero errori
di pagina, console o HTTP.

### Rimandato, con la ragione

- **`.ed-layout` sulla `@media`.** Stessa malattia di A3 sugli editor. Il perimetro d'uso dichiarato è
  ≥1024px (`docs/design/regole-ui-pagine-admin.md`) e ogni regola va decisa e vista a schermo: è un giro a sé,
  non una dimenticanza. Restano **10 regole `.struct`** più `.ed-layout`.
- **`<td @onclick>` in `AeroportiPage`.** Segnalato in analisi, poi **verificato e ritirato**: il codice ha
  già la risposta scritta accanto e ha ragione — ogni cella duplica una checkbox etichettata nella stessa
  riga, e dare il fuoco anche alla cella farebbe tre fermate di tabulazione per un comando solo.
- **Potatura del CSS morto.** Un confronto grezzo dà ~60 classi non usate, ma il grosso sono falsi positivi
  (classi costruite in C#, passate come parametro `Class`, o scritte da `vipi-tour.js`). Serve uno strumento,
  non una grep.

### Due correzioni all'analisi, fatte in corso d'opera

⚠️ Vale la pena registrarle: in tutti e due i casi era **la grep a mentire**, non il codice.

1. **«Decine di righe di prosa italiana non localizzate in `AdminTrasferimentiPage`»** — falso. Quelle righe
   sono **commenti XML nel blocco `@code`**, cioè documentazione C#. Il markup ha 180 chiamate `@T(`/`@L[` e
   zero prosa nuda.
2. **«131 stringhe non localizzate in `GuidaPage`»** — falso. La pagina è bilingue **per costruzione**
   (record `Sec` con le coppie it/en): la grep contava tutte e due le metà.

### Un rosso non riproducibile

In uno dei giri completi `Vipi.Application.Tests` ha segnato **1 fallimento su 625**, il cui nome non è stato
catturato. In sei esecuzioni successive (tre mirate, tre complete) non si è più presentato. Non sembra legato
a queste modifiche — lì è stato toccato solo `AorColorScheme`, che ha test deterministici — ma è registrato
qui perché «era tutto verde» non sarebbe vero.
