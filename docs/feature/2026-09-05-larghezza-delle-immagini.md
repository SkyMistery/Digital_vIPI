# Larghezza delle immagini nei documenti (5 settembre 2026)

> Chi legge la vIPI vedeva ogni foto **a tutta colonna**: uno schema piccolo veniva ingrandito fino al bordo e una
> foto verticale spingeva il testo di una pagina intera. Ora chi scrive **trascina l'angolo** dell'immagine e ne
> decide la larghezza.

## La decisione (pre-flight)

1. **Modello — non un secondo posto dove si salva la stessa cosa.** La larghezza entra nel riferimento che gia'
   esiste (`MediaRef`, il JSON di `BodyJson` / `ExtraBlock.ImageJson`) come campo `scale`. **Nessuna migrazione,
   nessuna colonna nuova**: un documento salvato prima non ha il campo, lo legge come `0` e si rende come sempre.
   Per la stessa ragione **le release congelate non cambiano**: nel loro payload il campo non c'e'.
2. **Una PERCENTUALE, non dei pixel.** La stessa immagine si legge su un monitor, su un telefono e su un A4:
   solo un rapporto vale in tutti e tre. `0` = piena larghezza, `10` il minimo sotto cui non e' piu' guardabile,
   `100` si riscrive `0` (e' lo stesso stato di chi non ha mai scelto).
3. **Dispatch — niente switch nuovi.** La larghezza si applica in **un solo posto**: `ImageFigure`, la resa
   condivisa da viewer, anteprime dei due editor e stampa. Chi rende un'immagine la rende gia' cosi'.
4. **Ingresso e verifica.** L'ingresso e' la maniglia nell'angolo dell'immagine, nell'editor del blocco (quindi
   in tutti e due gli editor che lo montano, documento e sezioni extra d'aeroporto). Si verifica **guidando il
   trascinamento vero** in un browser: nessun test bUnit puo' vedere un `pointermove`.

## Come e' fatta

| pezzo | che cosa fa |
|---|---|
| `MediaRef.Scale` | il campo, con `ClampScale` che raddrizza qualunque numero e `ScaleOrFull` per mostrarne 100 invece di 0 |
| `ImageFigure` | scrive `style="width:N%"` sulla figura; i margini automatici la tengono **centrata** |
| `vipiMedia.ridimensionabile` | il trascinamento: durante il gesto la larghezza la scrive il **browser**, e .NET la sente **una volta sola**, a dito alzato |
| `ImageBlockEditor.ImpostaScalaAsync` | riceve la percentuale e la salva nello stesso JSON (sha, alt e misure native non si toccano) |
| `.img-handle` / `.img-size` | la maniglia e la pastiglia con la misura in cifre, che compare mentre si trascina |

Due scelte che sembrano dettagli e non lo sono:

- **Un salvataggio per gesto, non per pixel.** Un `pointermove` che passasse dal circuito Blazor manderebbe
  decine di scritture per un solo trascinamento. Il browser muove, il C# salva alla fine — e se la misura non e'
  cambiata (un clic sulla maniglia senza spostarla) **non salva niente**, o si sporcherebbe il documento e
  ripartirebbe una traduzione per nulla.
- **Le frecce funzionano come il trascinamento** (5 punti per volta): una funzione che si puo' usare solo col
  mouse non e' usabile da tutti. La maniglia e' un `<button>`, quindi ci si arriva col tab.

## Che cosa ha preso la verifica live

Guidata su `/services/vsop/libb/editor` con Edge+puppeteer (blocco immagine creato dall'interfaccia, foto
caricata davvero, maniglia trascinata col mouse del browser):

- ⚠️ **Il primo tentativo passava la maniglia anche come figura** (`_handle, _handle`): il JS stringeva il
  **bottone** mentre l'immagine restava intera. A schermo il difetto era invisibile — la larghezza finale
  arrivava lo stesso, perche' il salvataggio e il render successivo la scrivevano sulla figura giusta — e i test
  erano tutti verdi. Si vedeva solo guardando la misura **durante** il gesto: pastiglia ferma a «100%» e nessuna
  classe `sizing`. Rimedio: da .NET si passa **la sola maniglia**, la figura la trova il DOM
  (`closest('figure.doc-img')`); un `@ref` a un elemento reso da un altro componente non si puo' prendere.
- Trascinamento di 176px su una colonna di 750 → **77%**, pastiglia allineata, salvato e **ritrovato uguale dopo
  il ricarico**; due frecce → 87%; fuori dal modo modifica la maniglia **non c'e'**; in `print` la proporzione
  regge (l'immagine resta dentro il tetto in mm della carta).
- Nessun errore in console, e il trascinamento **non apre** la finestra «scegli un file» — la figura sta dentro
  il `<label>` del file input, e il clic sulla maniglia va fermato apposta.

## Verifiche

- Suite verde su entrambi i TFM (`dotnet build Vipi.slnx -c Release --no-incremental`, 0 avvisi).
- Prove nuove: `MediaRefTests` (giro completo, raddrizzamento, **riferimento scritto prima del campo**),
  `ImageBlockEditorTests` (la maniglia c'e' solo con l'immagine, la percentuale torna all'host nello stesso
  riferimento, «piena larghezza» cancella la scelta, una misura uguale non salva), `BlockRenderingTests`
  (la percentuale arriva al documento; senza scelta la figura **non porta nessuno stile**).

## Il seguito: il clic riapre la foto a dimensioni originali (8 settembre 2026)

Richiesta del committente subito dopo: *«ora le immagini caricate si possono ridimensionare, si può fare che
se l'utente clicca sull'immagine in un documento si apre tipo pop up e la mostra a dimensioni originali?»*.

⚠️ **È il prezzo della feature qui sopra, ed era prevedibile.** Da quando la figura si stringe, una carta di
avvicinamento nel documento può stare al 30% della colonna: misurata su LIBG, una foto **1912×1073** si
disegna a **555px**. Prima non c'era modo di leggerla.

**Come è fatta.** Un `<button class="img-zoom">` intorno all'`<img>` in `ImageFigure` — la resa condivisa,
quindi viewer, anteprime dei due editor e stampa non possono divergere — e una **delega sola** su `document`
in `vipi-ui.js` che costruisce il velo.

- ⚠️ **Nessun giro dal server.** Le pagine dei documenti sono SSR statico con isole interattive: una finestra
  che avesse bisogno del circuito Blazor non si aprirebbe **proprio sulle pagine pubbliche**, che sono quelle
  dove si legge. E l'immagine è già nella cache — l'URL è lo stesso, non esiste una versione ridotta: la
  finestra non chiede un byte in più.
- ⚠️ **Delega e non aggancio per figura**: i blocchi immagine nascono e muoiono a ogni render di Blazor.
- ⚠️ **Un `<button>` vero, non un `role="button"` sull'`<img>`**: apre una finestra, quindi deve stare nel
  giro del tabulatore e rispondere a Invio e Spazio senza che nessuno lo riscriva a mano. Stessa ragione per
  cui le chip sono diventate `<button>` il 23 agosto.
- ⚠️ **Le etichette viaggiano come attributi `data-`** (`data-lb-close`, `data-lb-fit`, `data-lb-full`): la
  finestra la costruisce il JS, e il JS non deve conoscere nessuna lingua.
- ⚠️ **Il velo si appende dentro `.vipi-root`, non al `<body>`**: `.btn` è dichiarato `:where(.vipi-root)
  .btn`, e fuori di lì i due tasti uscivano **nudi**, coi bordi di sistema. `.vipi-root` non ha `transform`
  né `filter`, quindi il `position:fixed` continua a riferirsi alla finestra.
- ⚠️ **NON si apre dove si scrive**, e la condizione non è un parametro che si può dimenticare di passare: è
  l'assenza dell'`Overlay`, cioè della maniglia. Nell'editor il gesto del mouse su quell'immagine **è** il
  trascinamento della larghezza — col gancio anche lì, chi ha appena stretto la foto se la vedrebbe
  spalancare in faccia a dito alzato.

**Che cosa vede chi legge**: la misura in pixel, un tasto «Adatta allo schermo» / «Dimensioni originali», e
«Chiudi». Si chiude con Esc, con la ✕ o cliccando sul velo; **non** cliccando sull'immagine, perché lì ci si
clicca sopra per scorrerla. Il fuoco torna da dov'è partito, la pagina sotto non scorre, e in `print` il velo
non esiste.

⚠️ `place-items:safe center` e non `center`: col centraggio normale un contenuto più largo del contenitore
finisce con **l'inizio fuori e irraggiungibile** — cioè proprio il caso per cui la finestra esiste.

⚠️ Adattata usa `max-width`/`max-height`, non `width`: con `width` un'immagine piccola verrebbe **ingrandita**
oltre il suo naturale, che è sfocatura pura.

**Che cosa ha preso la verifica live** (LIBG, Edge+puppeteer, tema chiaro e scuro):

| | |
|---|---|
| viewer | 1 tasto, cursore `zoom-in`, immagine 555px in pagina |
| aperta | dentro `.vipi-root`, `fixed`, z 1900, «1912 × 1073 px», immagine **1912×1073**, il piano scorre, fuoco sulla ✕, pagina bloccata |
| adattata | 1372×770, niente scorrimento, il tasto diventa «Dimensioni originali» |
| Esc / velo | chiude, scorrimento della pagina ripristinato |
| **editor** | **0 tasti zoom**, la maniglia al suo posto |

- ⚠️ **E ha trovato due regole CSS morte, scritte da me un'ora prima**: volevano i tasti bianchi sul velo e
  non dipingevano niente — `.btn.ghost` sta più in basso nel foglio e a parità di peso vinceva. Misurato con
  la finestra aperta: in tema chiaro pastiglia quasi bianca con testo blu `#0d2c99`, in scuro pastiglia blu
  notte con testo azzurro. Si leggono in tutt'e due, sono i tasti di casa: le due regole sono state **tolte**.
  Due regole morte con un commento che dice il contrario sono peggio di nessuna regola.

**Prove**: `ImmagineIngrandibileTests` — il gancio c'è dove si legge, **non** c'è dove si ridimensiona, le
etichette tradotte viaggiano come `data-`, la larghezza al 60% non si perde, e senza immagine non c'è niente
da aprire. Ui **1 375** verdi su net10, solution 0 avvisi.
