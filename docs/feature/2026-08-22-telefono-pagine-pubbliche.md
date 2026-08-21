# Il telefono, sulle pagine pubbliche (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`. Non una pagina e nemmeno il chrome: un **assetto** che il progetto non ha
> mai supportato, chiesto dal committente dopo il giro della topbar.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il perimetro, deciso

**Le pagine pubbliche, per intero** (decisione del committente): home, viewer aeroporto, vIPI ACC, APP, vLOA,
live, ricerca, guida. Le **admin e gli editor restano da desktop**, e lo si dichiara.

⚠️ La ragione non è la fatica: è che **nessuno scrive una vIPI dal telefono**. Chi apre la vIPI dal telefono
è un pilota o un controllore che **consulta** prima di collegarsi — e sono esattamente le pagine pubbliche.
Fare le admin responsive sarebbe lavoro vero per uno scenario che non esiste.

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

### ⚠️ M2 — Sul viewer è il CONTENUTO a sforare, e non è il TOC

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

Le altre pagine pubbliche — home, live — **si adattano già**: il `.wrap` misura esattamente il viewport.

## Cosa cambia

### La barra: marchio + ricerca + «☰», sotto i 700px

In riga restano **il marchio, l'icona di ricerca e un tasto menù**. Dentro il menù, in un pannello che scende
a piena larghezza: **gli ACC**, la guida, lo zoom, il badge live, i comandi staff e l'uscita.

- `<details>` **nativo**, come il menu «+ Blocco» e le card collassabili: si apre senza circuito Blazor, e il
  layout è SSR statico — un menù che dipendesse dal circuito non funzionerebbe sulle pagine pubbliche;
- ⚠️ **niente sparisce**: quello che esce dalla riga entra nel menù, e gli `aria-label` restano interi;
- la soglia è **700px**, non 768: il tablet verticale ci sta comodo con la barra in riga, ed è l'assetto in
  cui gli ACC visibili valgono ancora la loro larghezza.

### Le tabelle dei viewer scorrono, invece di allargare la pagina

Sotto la soglia, una tabella più larga dello schermo **scorre dentro il suo contenitore**. È la regola 63
già scritta per gli editor («se il minimo che non taglia niente supera il riquadro, meglio far scorrere il
riquadro che mozzare dieci campi»), applicata al viewer.

⚠️ **Solo dentro `.doc-layout`**: negli editor le tabelle stanno già dentro `.st-scroll`, e aggiungere un
secondo contenitore scorrevole darebbe due barre annidate — che è il difetto che la regola 15 vieta.

## Fuori ambito, dichiarato

- **Admin ed editor**: restano da desktop. Le tabelle da 700px e i layout a due colonne non diventano
  telefono senza riprogettarli, e non è quello che serve.
- **Sotto i 360px** non si va: è sotto ogni telefono in commercio.
- Le **mappe** (Leaflet) si ritagliano già da sole e non sono in questo giro.

## Come si verifica

⚠️ Con **`isMobile: true` e `hasTouch: true`** nel driver, non solo un viewport stretto: senza, il browser
non applica il comportamento mobile e la misura è di una finestra piccola su un desktop.

A **375 / 390 / 430 / 768**, IT ed EN, su tutte le pagine pubbliche:

- `scrollWidth == clientWidth`: la pagina **non scorre in orizzontale**, che è il difetto che si sta chiudendo;
- il menù si apre, contiene **tutto** quello che è uscito dalla riga, e si chiude;
- ⚠️ le tabelle scorrono **dentro di sé**, e la pagina no: due barre di scorrimento annidate sono peggio di
  una pagina che scorre (regola 15);
- ⚠️ **guardare gli screenshot**: a questa larghezza un difetto di sovrapposizione non lo trova nessuna misura.

## Slice

1. La barra: `<details>` col menù, sotto i 700px; gli `aria-label` interi.
2. Le tabelle dei viewer che scorrono dentro `.doc-layout`.
3. Verifica guidata sulle pagine pubbliche, quattro assetti, due lingue.
