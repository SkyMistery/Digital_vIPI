# La topbar si misura da sola (carta, 22 agosto 2026)

> Ramo `feature/services-hub-profile-swapper`, componente `SopLayout` — il **chrome**, cioè ogni pagina.
> Seguito diretto di [`2026-08-22-topbar-larghezza-e-lingua.md`](2026-08-22-topbar-larghezza-e-lingua.md),
> che ha introdotto i tre scaglioni a media query. Questa carta li **toglie**.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Perché adesso

Il committente ha riferito la barra che **sfora e si taglia** a larghezze che la taratura del 22 agosto
dichiarava sane: rotta sotto i **~1940**, a posto solo dal **1300**, di nuovo rotta dal **~1168** fino ai 900
del telefono. È lo stesso difetto già scritto in [`lavori-aperti.md`](../lavori-aperti.md) («la topbar
sfonda fra 1301 e ~1410px»), ma **più largo di come l'avevamo misurato** — e questo è il dato che conta,
perché dice che il difetto non è la soglia: è il metodo.

## La diagnosi, in una riga

⚠️ **Una media query misura la finestra; il problema è la larghezza della BARRA, e le due cose non sono la
stessa.**

La larghezza che la barra pretende dipende da almeno sei cose che una `@media` non può vedere:

| Da cosa dipende | Quanto pesa |
|---|---|
| login sì/no, admin sì/no | il tasto Permessi, la `user-chip`, «Editor» e «Incarichi» |
| **lunghezza della stringa staff** | 148px per «IT-AOA1 · IT-T03», ma è `string.Join(" · ", …)`: chi ha quattro incarichi ne ha il doppio |
| numero di ACC | 262px per quattro, e il catalogo non promette che restino quattro |
| lingua | 1385 in italiano, **1395** in inglese — già misurato il 22 agosto |
| **zoom di pagina** | `vipi-zoom.js` scrive `zoom` su `<html>` e arriva a **1.8**: la barra pretende 1385 × 1.8, la media query continua a leggere la finestra e non scatta |
| stato della ricerca | aperta o chiusa sono due larghezze diverse (lezione 3 della carta precedente) |

I 1940 del committente sono il pavimento **della sua configurazione**, non un numero universale: la scaletta
1500/1300/900 è tarata su **una** configurazione sola, ed è giusta soltanto lì.

E i due buchi che ha visto sono, uno per uno, le fasce fra un pavimento e lo scaglione dopo:

| Fascia | Cosa c'è sotto |
|---|---|
| ~1940 → 1500 | non scatta **niente**: sopra i 1500 non esiste alcuna regola |
| 1500 → 1300 | scattano solo badge staff e tasto «Servizi»: −162px, non bastano |
| ~1168 → 900 | pavimento dello scaglione 2 (1161px, misurato allora) contro la forma telefono che arriva solo a 900 |

## E perché il blu si TAGLIA invece di scorrere

Domanda distinta, e vale la pena scriverla perché è la parte che si vede. `.topbar` è un flex senza
`overflow`: quando i pezzi non ci stanno **escono dal contenitore**, e a scorrere è la **pagina**. Ma lo
sfondo blu è dipinto sul contenitore, largo quanto il viewport, non quanto il contenuto. Si scorre a destra e
il blu **finisce**, con i tasti appoggiati sul bianco.

⚠️ Non è la barra che sparisce: è il fondo che non la segue. Le due cose vanno riparate insieme, perché la
seconda è la rete di sicurezza della prima.

## La decisione: niente soglie

Le tre strade erano: **alzare** le soglie (le rompe la configurazione successiva), **recuperare 110px** dentro
la fascia (compra una fascia sola), oppure **smettere di indovinare**. Prendo la terza.

La barra **si misura** e sceglie da sé il suo scaglione. Gli scaglioni restano quelli decisi allora — la
scaletta di priorità è una decisione di prodotto e non si tocca — ma non li sceglie più una larghezza scritta
a mano: li sceglie il confronto fra quanto la barra **pretende** e quanto **ha**.

| Livello | Cosa cede | Chi lo decideva prima | Chi lo decide adesso |
|---|---|---|---|
| `tb-1` | spazi più stretti, badge staff a icona | `@media (max-width:1500px)` | la misura |
| `tb-2` | marchio senza sottotitolo, «Editor»/«Incarichi» a icone, badge live a pallino | `@media (max-width:1300px)` | la misura |
| `tb-3` | la ricerca si chiude in un'icona e si riapre a piena riga | idem, **nello stesso scaglione** | la misura |
| `tb-4` | forma telefono: ACC e comandi dentro il «☰» | `@media (max-width:900px)` | la misura |

Le classi sono **cumulative** (`tb-4` implica `tb-1 tb-2 tb-3`): così ogni blocco di regole resta scritto
**una volta sola**, e il foglio non cresce.

⚠️ `tb-3` **non era in questa carta**: l'ha aggiunto la verifica, ed è la lezione principale del giro — vedi
«Com'è andata». Le etichette e la ricerca stavano nello stesso scaglione, e insieme facevano un gradino da
500px.

### Cosa vuol dire «ci sta», esattamente

Non «non sfora». ⚠️ **`scrollWidth == clientWidth` da solo mente**, ed è la lezione 3 della carta precedente
riscritta in codice: la ricerca ha `flex-shrink:1`, quindi cede fino al suo minimo **prima** che la barra
sfori. A quel punto la barra «sta» e il segnaposto dice «Cerca Co…». Il difetto si è solo spostato.

Il criterio ha quindi **due addendi**:

```
deficit = (scrollWidth − clientWidth) + max(0, --tb-search-min − larghezza del campo)
```

Il secondo termine vale solo dove la ricerca è ancora in riga (livelli 0 e 1); da `tb-2` in poi è un'icona e
non ha un minimo da difendere. Il minimo sta in una **variabile CSS**, non in una costante JavaScript: è una
decisione di forma e vive nel foglio di stile, accanto alla regola che la usa.

### Le tre trappole che il metodo si porta dietro

1. ⚠️ **Non si rimisura mentre la ricerca ha il fuoco.** A `tb-2` il campo aperto è `position:fixed`: esce dal
   flusso, la barra sembra più stretta di quanto sarà quando si richiude, e i conti tornerebbero un assetto
   più largo — cioè il campo salterebbe sotto le dita di chi sta scrivendo. È la lezione 3 di nuovo, ma
   stavolta in tempo reale.
2. ⚠️ **Si riparte sempre dal livello 0 e si sale.** Misurare lo scaglione corrente e provare a indovinare il
   prossimo è lo stesso errore di prima con un altro vestito: l'unico stato di cui si può dire qualcosa di
   vero è quello **applicato**. Costa tre riflow, una volta per ridimensionamento.
3. ⚠️ **Isteresi sul verso che mostra di più.** Salire di scaglione appena serve; scendere solo con margine.
   Senza, la barra sbatte fra due assetti sul pixel di confine mentre si trascina il bordo.

### E quando cambia il contenuto, non la finestra

Il badge live è un'**isola interattiva**: quando ti colleghi in frequenza il suo testo cambia, la barra si
allarga, e **nessun `resize` viene emesso**. Serve un `MutationObserver` sul sottoalbero della barra. Gli
attributi restano fuori dall'osservazione, o le classi che scriviamo noi rientrerebbero da sole.

Le occasioni di rimisura sono quindi: all'esecuzione dello script, a ogni `resize` (⚠️ e lo zoom ne emette uno
apposta, `vipi-zoom.js` lo fa già), a ogni `enhancedload`, a `document.fonts.ready` — i font web cambiano le
misure, e il primo giro le prende dal ripiego — quando la barra perde il fuoco, e a ogni mutazione.

## La rete di sicurezza

Anche col metodo giusto resta un caso: **zoom 1.8 su 375px** è meno di 210 unità di layout, e sotto `tb-3` non
c'è nient'altro da togliere. Serve un fondo che non ceda mai:

- `.topbar{overflow-x:auto;overflow-y:hidden}` — se proprio avanza, scorre **dentro la barra** e non nella
  pagina. È la regola già scritta per le tabelle strette (§28): meglio far scorrere il contenitore che mozzare
  dieci campi. La barra di scorrimento si nasconde: non è un contenuto da esplorare, è un fondo.
- ⚠️ **Il popup della ricerca diventa `position:fixed`**, e non è cosmesi: un discendente `absolute` verrebbe
  **tagliato** da quell'`overflow`, e il difetto sarebbe peggiore di quello che curiamo. Il pannello del «☰»
  era già `fixed` e sta bene dov'è.
- L'altezza della barra smette di essere il numero `62px` ripetuto in tre punti e diventa `--tb-h`, che è ciò
  che quei tre punti stavano già dicendo.

⚠️ **Non** metto `overflow-x:clip` su `.vipi-root`: nasconderebbe *qualsiasi* sforo di pagina, comprese le
regressioni vere che vogliamo continuare a vedere. Il chrome si ripara; il resto deve restare visibile.

## Il costo che accetto, dichiarato

Tolte le media query, **fra il primo disegno e la prima misura la barra è al livello 0**. Lo script sta in
fondo al `<body>`, dopo il markup della barra, quindi in pratica la misura arriva prima della prima pittura —
ma su una rete lenta, con disegno progressivo, un fotogramma di barra larga è possibile. Lo accetto perché
l'alternativa è **scrivere ogni blocco di regole due volte** (una per la classe, una per la media query), e un
foglio che dice la stessa cosa in due posti è il debito che questo giro sta pagando. Se in verifica il salto si
vede, la cura è una media query di solo pavimento, e si scriverà allora.

## Il marchio: «Servizi ATC», e porta ai servizi

Richiesta del committente, e ha ragione sul perché: il marchio portava a `/services/vsop`, cioè a **uno solo**
degli strumenti, mentre il posto che il marchio dovrebbe indicare è la **porta d'ingresso**.

- `href` → `/services`; il testo → `Services_Title`, la chiave che **già esiste** ed è quella che intitola
  l'hub. ⚠️ Nessuna stringa gemella: se fra sei mesi si cerca «dove si chiama così», il posto è uno
  (pre-flight §1).
- Il quadratino del logo dice `ATC` invece di `vIPI`: diceva il nome di uno strumento stando su tutti.
- `Chrome_BrandSubtitle` era «/ vLOA · IVAO Italia» — il seguito del nome vecchio. Diventa «IVAO Italia».
- **Il tasto «Servizi» nella barra sparisce**: adesso il marchio fa quel mestiere, e due link identici a 100px
  di distanza sono ingombro. Resta nel «☰», dove il marchio è ridotto al solo logo. ⚠️ Vale anche come sconto:
  sono 52px con il suo gap, proprio nella fascia dove la barra era già al limite. Nel menu del telefono la voce
  resta, quindi **niente diventa irraggiungibile** (regola 198).

## Fuori ambito, dichiarato

- **Il tema chiaro/scuro non è di questo giro.** Il tasto sta su `brand-atmosphere`, ramo chiuso e mai fuso
  (commit `f197fc3`), insieme a una riscrittura di 1483 righe dello stesso foglio. Fonderlo è un lavoro suo, e
  il committente ha scelto di farlo **dopo**. Nessun pezzo di quel tema entra qui.
- **`acc-nav` non si comprime** più di com'è: a `tb-3` va nel menu, sopra resta intera. Invariata dalla carta
  precedente.

## Come si verifica

⚠️ La topbar sta su **ogni** pagina, e il punto del giro è che la finestra **non basta** a descrivere un caso.
Quindi la griglia ha una dimensione in più rispetto al 22 agosto: **lo zoom**.

- larghezze **1920 / 1600 / 1440 / 1366 / 1280 / 1024 / 768 / 375**
- zoom **0.7 / 1.0 / 1.4 / 1.8**
- **IT ed EN**, con e senza login, e ⚠️ almeno un giro con una **stringa staff lunga** (quattro incarichi): è
  la variabile che ha rotto la taratura vecchia, e va provata quella, non quella comoda.
- a ogni combinazione: `scrollWidth == clientWidth`, la pagina non scorre in orizzontale, la barra resta su
  **una riga**, il segnaposto della ricerca **non è troncato**, e ⚠️ nessun comando è diventato irraggiungibile
  — quello non lo dice una misura, si guarda.
- il marchio porta a `/services` da una pagina qualsiasi, e il tasto «Servizi» non c'è più in riga ma c'è nel «☰».

## Slice

1. Questa carta.
2. Le tre media query della topbar diventano classi cumulative; rete di sicurezza (`overflow`, popup
   `fixed`, `--tb-h`, `--tb-search-min`).
3. La misura in `vipi-ui.js`: `vipiFitTopbar`, agganciata a resize/enhancedload/fonts/fuoco/mutazioni.
4. Il marchio: «Servizi ATC» verso `/services`, il tasto duplicato via, chiavi IT+EN.
5. Verifica guidata sulla griglia, e chiusura della voce in `lavori-aperti.md`.

## Com'è andata

**256 combinazioni** (8 larghezze × 4 zoom × 4 famiglie di pagina × 2 lingue), guidate con Edge+puppeteer.
`scrollWidth == clientWidth` sulla barra in **256 su 256**, barra a 62px e su una riga ovunque, **nessun
comando perso** a nessun assetto, nessun errore JavaScript. Andata e ritorno simmetrici sia stringendo la
finestra (0→2→4→4→4→2→0) sia sullo zoom (1→3→4→3→1→0→1).

Gli scaglioni sono diventati **quattro**, e non tre come diceva la carta. Il quarto è nato da uno screenshot,
non da un numero: a 1440 la barra **stava** — misura verde — ed era **mezza vuota**, con un buco di 700px in
mezzo e tutti i comandi ridotti a icone. ⚠️ Tenere «la ricerca si chiude» insieme a «le etichette spariscono»
faceva un gradino da 500px: la barra passava dallo sfondare all'essere spoglia senza niente in mezzo. **Se un
gradino è più alto di quanto serva, non è una scaletta.** Separati, la ricerca resta **aperta e intera a
1366 e a 1440** — cioè proprio le due larghezze che `lavori-aperti` dava per perdute («alzare la soglia a
1410 richiude la ricerca su tutti i portatili 1366: è esattamente ciò che quella taratura voleva evitare»).
La terza strada non era un compromesso migliore fra i due: era non doverlo fare.

Dove cade oggi la scaletta a zoom 1: **1920** livello 0 · **1600** livello 1 · **1440/1366/1280** livello 2
(ricerca aperta) · **1024 e sotto** livello 4. A 1024 il livello 3 non basta e si va direttamente alla forma
«☰»: prima lì la barra sforava di 137px.

### Tre difetti presi dalla verifica, due miei

1. ⚠️ **Il cricchetto dello zoom.** L'isteresi confrontava `documentElement.clientWidth` con una misura di
   fit presa su `bar.clientWidth`: sotto zoom i due numeri **divergono** (a 1920 con zoom 1.4 la barra ha
   1371 unità di layout mentre `documentElement` continua a dire 1920), e il confronto fra unità diverse
   faceva salire di scaglione senza mai far scendere — a 1440 la barra era già in forma telefono. **La misura
   del fit e quella dell'isteresi devono stare nella stessa unità.**
2. ⚠️ **L'isteresi frenava anche ciò che non si stava trascinando.** Allungata la stringa staff a larghezza
   ferma, la barra saliva a `tb-1` e non tornava più giù: la larghezza non era cambiata, quindi il margine
   non poteva maturare. Un calo dovuto al **contenuto** non ha nessun bordo da frenare. Ora l'isteresi vale
   solo quando la barra si sta allargando *di poco*.
3. E uno che era del **driver**, non del prodotto: contavo le «righe» confrontando i `top` dei figli, ma la
   barra è `align-items:center` e pezzi di altezza diversa hanno `top` diversi **stando perfettamente in
   riga** — 3 righe su una barra sana. E contavo 11 link «muti» che erano solo dentro il `<details>` chiuso,
   dove Chrome usa `content-visibility` e `innerText` torna vuoto mentre il rettangolo non è nullo.
   ⚠️ Un attrezzo di misura sbagliato **denuncia**, e per mezz'ora sembra che il prodotto sia rotto.

### Due cose trovate guardando, che non sono di questo giro

- **La tabella SID sfora a zoom alto.** A 1280 con zoom 1.4 (cioè 914 unità di layout) il viewer aeroporto
  sfora di 58px, e il colpevole è `table.sid-table` col suo `min-width:720px`. **Non è il chrome**: tolta la
  topbar dal DOM lo sforo resta identico, 58px. La regola che fa scorrere le tabelle dentro di sé è
  `@media (max-width:900px)` — ⚠️ **la stessa malattia che questo giro ha curato sulla barra**: una soglia
  di viewport che non vede lo zoom. 914 > 900, quindi non scatta.
- **La cultura non arriva al circuito.** Su `/services/vsop?culture=it` il *prerender* scrive «‹ Servizi
  ATC» e subito dopo il circuito `InteractiveServer` ri-renderizza **«ATC Services»**, in pagina italiana.
  Il chrome resta giusto perché è SSR statico — ed è per questo che non se n'era accorto nessuno: sbaglia
  solo la parte interattiva. Preesistente e trasversale a ogni pagina `InteractiveServer`; merita una carta
  sua, non una riga in questa.
