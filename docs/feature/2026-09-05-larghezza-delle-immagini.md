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
