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
| `tb-2` | marchio senza sottotitolo, «Editor»/«Incarichi» a icone, badge live a pallino, ricerca a icona | `@media (max-width:1300px)` | la misura |
| `tb-3` | forma telefono: ACC e comandi dentro il «☰» | `@media (max-width:900px)` | la misura |

Le classi sono **cumulative** (`tb-3` implica `tb-1 tb-2`): così ogni blocco di regole resta scritto **una
volta sola**, e il foglio non cresce.

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
2. Le tre media query della topbar diventano tre classi cumulative; rete di sicurezza (`overflow`, popup
   `fixed`, `--tb-h`, `--tb-search-min`).
3. La misura in `vipi-ui.js`: `vipiFitTopbar`, agganciata a resize/enhancedload/fonts/fuoco/mutazioni.
4. Il marchio: «Servizi ATC» verso `/services`, il tasto duplicato via, chiavi IT+EN.
5. Verifica guidata sulla griglia, e chiusura della voce in `lavori-aperti.md`.
