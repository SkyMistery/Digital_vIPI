# La colonna destra delle «Quote di transizione» — 29 agosto 2026

> **Da dove nasce:** osservazione del committente — «la tabella con le transition altitude occupa solo mezza
> pagina, con l'altra metà vuota».
> **Ramo:** `quote-transizione-colonna-destra`. **Nessuna migrazione.** **7 test nuovi.**

## 1. Il difetto, misurato

La sezione «Quote di transizione» disegna un distintivo con la TA e una tabella di due colonne (fascia QNH →
livello). La tabella ha un tetto di 420px in `vipi-theme.css` — ed è giusto che l'abbia: due colonne di
numeri allargate a piena pagina non si leggono meglio, si leggono peggio.

Misurato a schermo su LIBD, viewport 1600: la sezione è larga **822px**, la tabella ne occupa 420, e restano
**402px vuoti** a destra. Metà sezione.

## 2. Le sei proposte, e quella scelta

Sul tavolo: (A) una scheda col verdetto del QNH di adesso; (B) uno schema verticale TA/TL in SVG; (C) una
scheda coi dati del campo; (D) una nota editoriale locale (serve un campo nuovo → migrazione); (E) affiancare
due sezioni con una griglia generale del documento; (F) solo cosmetica (togliere il tetto e centrare).

**Scelta del committente: A + C**, nella stessa colonna. Sono le due che usano dati **già in casa**, senza
migrazioni e senza toccare il layout degli altri documenti.

## 3. Cosa c'è nella colonna destra

**«Transition Level adesso»** — la stessa risposta che dà la riga accesa nella tabella, scritta grande:
`FL60`, e sotto `QNH 1013 hPa · METAR 291450Z`. L'ora del bollettino non è decorazione: dice a **quando**
risale il verdetto.

- ⚠️ È `noprint`, come il meteo. Nasce dal METAR, e su carta sarebbe un verdetto già scaduto quando lo si
  legge. La tabella accanto — che il QNH non ce l'ha dentro — si stampa come prima.
- Senza QNH la scheda non c'è. **Senza TA nemmeno**: lì la tabella scrive «N/A» su ogni riga, e un livello
  grande accanto sarebbe un numero che il documento non ha mai detto.
- QNH che non ricade in nessuna fascia scritta: la scheda **resta** e dice «nessuna fascia per questo QNH».
  Tacere lascerebbe pensare a un livello che nessuno ha scritto.

**«Dati del campo»** — elevazione (in piedi e in metri), variazione magnetica, IATA, coordinate. Sta in
**questa** sezione e non altrove perché la quota del campo è il motivo per cui il livello di transizione è
quello e non un altro.

- La variazione si scrive **con l'emisfero**, non col segno: «4° E» si legge, «4°» no. La sorgente la manda
  positiva a est (in archivio è fra 1° e 4° E su tutti e 93 gli aeroporti).
- Le coordinate le formatta `SexagesimalPair.Angolo`, che già serve le radioassistenze: nessun formattatore
  nuovo, e la longitudine resta su **tre** cifre di grado.
- Senza nessuno dei quattro dati la scheda non compare: un riquadro di trattini non è un'informazione.

## 4. Da dove arrivano i dati, e perché NON dallo snapshot

I quattro dati del campo erano **già nel database** (`Airport.ElevationFt`, `MagneticVariation`, `Iata`,
`Latitude`/`Longitude`, riempiti dal giro notturno dell'anagrafica) e **nessuna pagina pubblica li mostrava**.

Le due strade possibili erano lo snapshot di release (`AirportTransitionView`, congelato) o l'anagrafica in
cache (`AirportStation`, viva). Scelta l'anagrafica, per due ragioni:

1. **Non sono dati di release.** Li riscrive l'import, non un editor. È la stessa ragione per cui la presenza
   militare, in testata, non passa dallo snapshot.
2. **Lo snapshot li mostrerebbe vuoti.** Ogni documento già pubblicato ha un payload congelato senza i campi
   nuovi: le quattro righe sarebbero trattini finché qualcuno non ripubblica.

⚠️ Costo: **zero query in più**. `AirportStation` è la mappa ICAO → aeroporto che il layout scalda già a ogni
richiesta; sono cinque colonne in più su una lettura di 93 righe. Il vSOP **militare** ne beneficia allo
stesso prezzo, mentre passando per `AirportData` avrebbe dovuto leggere il profilo (sei query) che oggi non
legge affatto.

## 5. Come va a capo

Nessuna `@media` e nessuna `@container`: la sezione è un **flex che va a capo da sé**, con basi 380px
(tabella) e 260px (schede). La soglia la calcola il layout — che lo `zoom` dell'applicazione lo vede — mentre
una media query no (stessa malattia della topbar, 2026-08-22).

⚠️ Sotto i 900px di **finestra** entra in gioco una regola che c'era già (`.wrap table{display:block;
max-width:100%}`): lì la tabella diventa piena larghezza e le schede scendono sotto. È il comportamento di
tutte le tabelle del prodotto, non una scelta di questa sezione.

## 6. Verifica dal vivo

Host su copia del DB (`.claude/skills/verifica-live`), Edge headless, LIBD e LIML:

| misura | prima | dopo |
|---|---|---|
| vuoto a destra della tabella (1600px, sezione da 822) | **402px** | **0px** |
| schede a 760px | — | vanno a capo sotto la tabella, nessun taglio |

Verificati: tema chiaro e tema scuro (screenshot letti, non solo il DOM), riga accesa coerente con la scheda
(`≥ 1013` / `FL60` / «current QNH»), stringhe italiane e inglesi entrambe presenti, nessun errore in console
né richiesta ≥400, sezione resa anche sul **vSOP militare** (LIML).

## 7. Quel che si è visto e non si è toccato

- **LIRN** ha una sezione «Transition Altitude/Level» che **non** è quella derivata (nessun `.ta-grid` nel
  DOM): è un documento più vecchio, con la sezione scritta a mano. Non è una regressione di questo lavoro.
- `AirportViewFormat.QnhRowMatches` legge la fascia dal **testo**: su «1013.2 e oltre» conterebbe due numeri e
  la leggerebbe come intervallo 2–1013. In archivio quel testo non esiste — le fasce le scrive
  `AirportSectionProjection.QnhRange` dai numeri («≥ 1013», «995 – 1012», «≤ 976») — quindi resta una trappola
  latente, non un difetto vivo. È l'unica ragione per cui le prove di questa sezione usano quella forma lì.
