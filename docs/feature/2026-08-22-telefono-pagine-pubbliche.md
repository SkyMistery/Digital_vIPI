# Il telefono, sulle pagine pubbliche (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`. Non una pagina e nemmeno il chrome: un **assetto** che il progetto non ha
> mai supportato, chiesto dal committente dopo il giro della topbar.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il perimetro, deciso

**Le pagine pubbliche, per intero** (decisione del committente): home, viewer aeroporto, vIPI ACC, APP, vLOA,
live, ricerca, guida.

**Admin ed editor si usano da 1024 in su** — desktop o **tablet orizzontale** — e lì **vanno bene come
sono**: è la decisione del committente, ed è sostenuta dalle misure del giro precedente, dove tutte le pagine
admin e tutti gli editor sono stati verificati a **1600 / 1440 / 1280 / 1024**, IT ed EN. Non è un limite
subìto: è il perimetro d'uso dichiarato.

⚠️ La ragione non è la fatica: è che **nessuno scrive una vIPI dal telefono**. Chi apre la vIPI dal telefono
è un pilota o un controllore che **consulta** prima di collegarsi — e sono esattamente le pagine pubbliche.

## Cosa ho trovato, misurato

### ⚠️ M1 — La barra ha un pavimento a 959px e non scende, mai

Il giro precedente l'ha portata a stare a **1024**. Sotto quella soglia non si comprime più: 959px a 768, a
430, a 390, a 375. I comandi raggiungibili scendono a **4 su 13**.

| Assetto | Barra | Eccesso | Comandi |
|---|---:|---:|---:|
| 768 (tablet **verticale**) | 959 | +191 | 10/13 |
| 430 | 959 | +529 | 5/13 |
| 390 | 959 | +569 | 5/13 |
| 375 | 959 | **+584** | **4/13** |

⚠️ **Anche il tablet verticale sfora**: quello che sta è il tablet *orizzontale*. Gli scaglioni del giro
precedente arrivavano fino a 1300, e sotto non c'era più niente da togliere **in riga**: serve un cambio di
**forma**, non un altro scaglione. A 375px i soli codici ACC occupano **263px**, due terzi dello schermo.

### ⚠️ M2 — Sul viewer è il CONTENUTO a sforare, e non è il TOC (né solo la tabella)

A 390px il `.wrap` del viewer aeroporto pretende **894px**. Il primo sospetto era l'indice laterale
(`aside.toc`, 870px), ma è una **conseguenza**: la media query a 1080 collassa già `.doc-layout` a una
colonna, e il TOC largo 870 sta semplicemente riempiendo una colonna larga 870.

Seguendo la catena delle larghezze **min-content**, il colpevole è:

```
div.wrap                       min-content 918
 └ div.doc-layout                          870
    └ details.block.cb                     870
       └ table.res-table.sid-table         820   ← è lei
```

⚠️ **La tabella delle SID pretende 820px** e allarga tutto quello che la contiene, TOC compreso. Sul vIPI ACC
lo stesso in piccolo: `h2.acc-block-h` a 411 su un viewport da 390.

⚠️ **Ma questa era solo la prima delle quattro cause**, e le altre sono venute fuori una alla volta, ognuna
dopo aver chiuso la precedente: vedi «Il contenuto aveva QUATTRO cause» più sotto. La catena min-content
mostra **il colpevole di quel momento**, non l'elenco dei colpevoli.

Le altre pagine pubbliche — home, live — **si adattano già**: il `.wrap` misura esattamente il viewport.

## Cosa cambia

### La barra: marchio + ricerca + «☰», sotto i 900px

In riga restano **il marchio, l'icona di ricerca e un tasto menù**. Dentro il menù, in un pannello che scende
a piena larghezza: **gli ACC**, la guida, lo zoom, il badge live, i comandi staff e l'uscita.

- `<details>` **nativo**, come il menu «+ Blocco» e le card collassabili: si apre senza circuito Blazor, e il
  layout è SSR statico — un menù che dipendesse dal circuito non funzionerebbe sulle pagine pubbliche;
- ⚠️ **niente sparisce**: quello che esce dalla riga entra nel menù, e gli `aria-label` restano interi;
- ⚠️ la soglia è **900px**. L'avevo scritta 700 dando per buono che «il tablet verticale sta comodo con la
  barra in riga»: **misurato, non sta** — a 768 la barra restava a 959 e sforava di 191px, con 10 comandi su
  23 raggiungibili. Un'assunzione scritta in una carta resta un'assunzione finché non la si misura.

### Le tabelle dei viewer scorrono, invece di allargare la pagina

Sotto la soglia, una tabella più larga dello schermo **scorre dentro il suo contenitore**. È la regola 63
già scritta per gli editor («se il minimo che non taglia niente supera il riquadro, meglio far scorrere il
riquadro che mozzare dieci campi»), applicata al viewer.

⚠️ **Non solo dentro `.doc-layout`**: così l'avevo scritto, e la pagina di **ricerca** — che mostra le stesse
tabelle fuori da quel layout — restava larga. Si prende **tutto il contenuto** e si **esclude** dove non
serve: dentro `.st-scroll` la tabella scorre già, e due barre annidate sono peggio (regola 15).

## Fuori ambito, dichiarato

- **Admin ed editor**: si usano da **1024 in su** (desktop o tablet orizzontale), dove sono **verificati** —
  32 combinazioni nel giro della topbar, IT ed EN. Sotto quella soglia non si va: tabelle da 700px e layout a
  due colonne non diventano telefono senza riprogettarli, e non è quello che serve a nessuno.
- **Sotto i 360px** non si va: è sotto ogni telefono in commercio.
- Le **mappe** (Leaflet) si ritagliano già da sole e non sono in questo giro.

## Com'è andata

**56 combinazioni pulite**: 7 pagine pubbliche × 4 assetti (375 / 390 / 430 / 768) × 2 lingue, e nessuna
costringe il telefono a rimpicciolire. Il desktop non è cambiato: le 32 combinazioni del giro topbar restano
verdi.

Il menù contiene **10 voci** (i quattro ACC più guida, live, editor, incarichi, permessi, uscita), sta dentro
lo schermo e si chiude.

## Il contenuto aveva QUATTRO cause, non una

Ognuna trovata dopo aver chiuso la precedente — ed è il motivo per cui «una misura sola non basta»:

1. ⚠️ **`.doc-layout` collassa a `grid-template-columns:1fr`**, e una traccia `1fr` ha `min-width:auto`, cioè
   il **min-content** del suo contenuto: non scendeva sotto 592px **nemmeno imponendo `width:340px`** al
   layout. È la regola del `min-width:0` sui figli di griglia che il progetto ha già scritta per gli editor,
   mai applicata a questo collasso. Da sola vale il viewer: **894 → 375**.
2. Le **tabelle**, con due cause opposte: `.sid-table` ha un `min-width:720px` **dichiarato**, `.rwy-table`
   non dichiara niente e pretende 542 per il suo **contenuto**. Servono entrambe le cure — azzerare il minimo
   **e** far scorrere — e su **tutte** le tabelle del contenuto: ⚠️ la prima versione copriva solo
   `.doc-layout`, e la pagina di **ricerca** restava larga. *Un elenco di contenitori si dimentica sempre il
   prossimo: si prende tutto e si esclude dove non serve.*
3. La riga **«titolo · tasti»** (`.doc-head`) è un flex che non andava a capo: i tre tasti del vIPI ACC sono
   277px e col titolo superavano lo schermo.
4. Gli **estratti di ricerca**: riportano testo del documento, dove capitano sequenze senza spazi che con
   `overflow-wrap` normale non si spezzano.

## ⚠️ Due trappole dell'attrezzo, e sono la vera lezione del giro

**Il segnale su mobile non è `scrollWidth`.** Un browser mobile, quando il contenuto pretende più dello
schermo, **non fa scorrere**: allarga il layout viewport e **rimpicciolisce tutto**. Misurato:
`scrollLeftMax` era **0** e `innerWidth` **648** su uno schermo da 375. Il difetto non è «la pagina scorre»,
è «il telefono ha dovuto rimpicciolire» — e si guarda **`innerWidth`**.

**Cercare «chi sfora» dopo che il viewport si è allargato non trova niente.** Una volta allargato, tutti gli
elementi ci stanno dentro: sulla ricerca l'elenco degli elementi oltre il bordo era **vuoto** mentre il
layout era 569. Il colpevole si trova **confrontando la pagina con e senza contenuto** — `/vsop/search` da
solo misura 375, con i risultati 569 — non cercando chi sborda.

⚠️ E una cosa che **non** ha risolto niente, detta perché non se ne prenda il merito: i **16px sui campi**.
L'avevo introdotta convinto che lo zoom automatico al fuoco fosse la causa della ricerca larga; il numero non
si è mosso di un pixel. Resta perché evita comunque lo zoom al fuoco su iOS, ma la causa era un'altra.

## Come si verifica

⚠️ Con **`isMobile: true` e `hasTouch: true`** nel driver, non solo un viewport stretto: senza, il browser
non applica il comportamento mobile e la misura è di una finestra piccola su un desktop.

A **375 / 390 / 430 / 768**, IT ed EN, su tutte le pagine pubbliche:

- ⚠️ **`innerWidth == la larghezza dello schermo`** — e *non* `scrollWidth == clientWidth`, che su mobile non
  è il segnale giusto (vedi le trappole qui sopra);
- il menù si apre, contiene **tutto** quello che è uscito dalla riga, e si chiude;
- ⚠️ le tabelle scorrono **dentro di sé**, e la pagina no: due barre di scorrimento annidate sono peggio di
  una pagina che scorre (regola 15);
- ⚠️ **guardare gli screenshot**: a questa larghezza un difetto di sovrapposizione non lo trova nessuna misura.

## Slice

1. La barra: `<details>` col menù, sotto i 700px; gli `aria-label` interi.
2. Le tabelle dei viewer che scorrono dentro `.doc-layout`.
3. Verifica guidata sulle pagine pubbliche, quattro assetti, due lingue.
