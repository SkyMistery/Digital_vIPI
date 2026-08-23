# I coordinamenti della vista live entrano in una schermata (carta, 23 agosto 2026)

> Pagina `/services/vsop/live[/{callsign}]` (`LivePage.razor`), componente `TransfersLive.razor`.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); la pagina è **fuori** dal perimetro delle regole di
> densità admin ([regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md) §«Fuori ambito: le viste
> pubbliche») — quindi vale anche il telefono.

## Perché adesso

Il committente ha riferito che con `LIBB_ES_CTR` connesso e `LIBD_CS0_APP` sotto «si legge tutto su una riga
e devo scorrere per vedere». Riprodotto e **misurato** su `vipi.db` di sviluppo, 18 ATC online, viewport
1920×1080 (nessun dato finto: sono i 16 accordi veri di LIBB, resi visibili accendendo i vicini):

| | misura |
|---|---|
| altezza pagina | **2835px** = 2,6 schermate |
| blocco coordinamenti | 2230px, per **36 punti** in **19 card** |
| larghezza usata dal blocco | **483px su 1478 disponibili** → 995px vuoti a destra |
| tasto «Compatta» | 2835 → 2777 = **−2%** |

## La diagnosi, in una riga

⚠️ **La masonry di `TransfersLive` raggruppa per mittente, ma nella vista live il mittente è sempre uno solo:
per costruzione non ha niente da impaginare, e il suo unico riquadro non può spezzarsi.**

`LiveStationParts.TransfersAsync` filtra `ResolvedOwnerCallsign == callsign` (riga 72): la lista che arriva al
componente ha **un** `ResolvedOwnerCallsign`, sempre. Quindi `foreach (var owner in shown.GroupBy(...))` gira
una volta sola, e in `vipi-theme.css`:

- `#xfer-pairs{columns:360px}` apre tre colonne;
- `.xpair{break-inside:avoid}` vieta di spezzare l'unico riquadro;
- risultato: il riquadro sta nella **prima** colonna e le altre due restano vuote.

Il componente nasce dal mockup «#reduced», dove i mittenti erano davvero più d'uno (`vipi-screens.js`, di
cui si parla al punto 1 in coda — non era così innocuo come sembrava). Portato nella vista live, il
presupposto è caduto e nessuno se n'è accorto perché il difetto **non si vede finché i coordinamenti sono
pochi**.

Due cause si sommano alla prima:

2. `.xpair-body .xt-cards{grid-template-columns:1fr}` (riga 1131) annulla l'`auto-fill` della riga 1115:
   dentro il riquadro le card stanno comunque in colonna singola.
3. **Una card per aeroporto**, con testata «ICAO · nome · N pt», anche quando i punti sono **uno**: 19
   testate per 36 punti, cioè circa metà dell'altezza è cornice e non dato.

E il cartiglio blu `.xpair-h` scrive il **tuo** callsign in cima ai tuoi coordinamenti: in questa pagina non
distingue niente da niente.

## Le tre forme misurate (prototipi guidati nel browser vero)

| | forma | altezza pagina | blocco |
|---|---|---|---|
| — | com'è oggi | 2835 | 2230 |
| A | solo CSS: griglia `auto-fit` invece di `columns`, card `auto-fill` | 1739 (−39%) | 1135 |
| **B** | **una riga per punto, una colonna per tipo di traffico** | **1080 (zero scorrimento)** | **428** |
| C | righe piatte in colonne CSS, i tipi come separatori | 1080 | 327 |

Tenuta a **carico ×4** (144 punti, quattro volte il traffico reale): B con altezza a finestra sta in una
schermata (642px); com'è oggi sarebbero ~8900px.

**Scelta del committente: B.** C è il 25% più denso ma si legge a serpentina — l'ordine di lettura non è
ovvio, e questa è una pagina che si guarda **mentre** si lavora in frequenza.

## Cosa si è deciso

1. **Via il raggruppamento per mittente.** `.xpair` non c'è più: il mittente è sempre chi guarda. Se un
   giorno la vista dovesse mostrare i coordinamenti di **più** enti, il posto dove rimetterlo è
   `LiveStationParts`, non il foglio di stile.
2. **Una riga per punto**, in colonne per tipo di traffico (Arrivi · Partenze · Sorvoli · …), posizione
   stabile: gli arrivi stanno sempre a sinistra.
   `ICAO | CoP | livelli | condizione | → ricevente`. L'ICAO si scrive **solo sulla prima riga** del suo
   gruppo, e il **nome dell'aeroporto** passa nel `title` della cella (prima era una riga di testata).
   Il **totale per tipo** sta nella testata di colonna (prima era «N pt» per aeroporto).
3. **Tutta la finestra.** `.wrap.live` col `max-width` a 2100 invece di 1640, e le colonne alte quanto lo
   spazio che resta, con lo scorrimento **dentro** la colonna. Il tetto lo misura `vipiCapInner`, che è
   nuovo: accorcia solo se serve, si tira indietro quando lo spazio per riga scende sotto la soglia, e
   sotto i 900px di finestra non si prova nemmeno — sul telefono le colonne tornano una sotto l'altra
   (vedi i punti 2 e 5 in coda).
4. **Filtro per aeroporto**, con suggerimenti: gli aeroporti su cui c'è una **posizione aperta** stanno in
   cima all'elenco e portano un pallino (vedi il punto 6 in coda: la lettura ovvia era degenere).
   Filtrando, le colonne che restano vuote spariscono; senza filtro restano tutte, ferme al loro posto.
5. **«Compatta» torna a voler dire qualcosa qui**: le regole `.vipi-dense` non toccavano nessuna classe di
   questo blocco. Ora ci sono, e valgono su righe, testate e filtro.
6. **Via la fascia «Online nel tuo intorno»** (77px): ogni riga porta già la pastiglia del ricevente col suo
   stato. La frase che spiegava la regola («interni solo se c'è qualcuno; esterni sempre») diventa un «?»
   accanto al filtro — non un paragrafo sempre acceso.

## Come si è verificato (e come rifarlo)

Skill `verifica-live`, con una sola aggiunta che è **strumento, non prodotto**:

`IvaoOptions.FakeOnlineCallsigns` — elenco di callsign separati da virgola che il poller pubblica al posto
della chiamata al tracker IVAO. ⚠️ **Onorato solo in `Development`**: `AtcPollingHostedService` chiede
`IHostEnvironment` e in qualunque altro ambiente logga un errore e ignora il valore. Serve perché senza
vicini online **ogni punto risolve a UNICOM**, che è nascosto per default: la pagina si prova vuota e il
difetto non compare.

```powershell
$env:Ivao__FakeOnlineCallsigns = "LIBB_ES_CTR,LIBD_CS0_APP,LIRR_NC_CTR,LIRR_US_CTR,LIRR_TS_CTR," +
    "LIRR_ES_CTR,LGGG_W_CTR,LDZO_CTR,LYBA_CTR,LAAA_CTR,LATI_APP,LGKR_APP,LDSP_APP,LDDU_APP,LYTV_APP"
```

Poi `/services/vsop/live/libb_es_ctr`: 36 punti, 19 aeroporti, tre tipi di traffico.

Gate del runbook: `dotnet build Vipi.slnx -c Release --no-incremental` verde (0 avvisi, entrambi i TFM),
`dotnet test Vipi.slnx` verde. Foglio di stile toccato ⇒ `sweep.js` (2 sospetti, i due falsi positivi noti
della pastiglia ACC attiva) e contrasto misurato in tutt'e due i temi.

## Fatto — e cosa è saltato fuori strada facendo

| | prima | dopo |
|---|---|---|
| pagina a 1920×1080 | 2835px | **1080** (nessuno scorrimento) |
| pagina a 1440×900 | 2835px | **900** |
| pagina a 1280×800 | 2835px | **800** |
| blocco coordinamenti | 2230px | 428 |
| «Compatta» sull'elenco arrivi | −2% (non toccava niente qui) | **−26%** |

Telefono (375×812) e 1024×768: il tetto non si applica, le colonne vanno una sotto l'altra e la pagina
scorre — voluto. Nessuno scorrimento orizzontale a nessuna delle sei larghezze provate.

### 1. `vipi-screens.js` cancellava i coordinamenti veri

Il mockup v2 **era** caricato — `App.razor` lo include in ogni pagina — e la sua «vista ridotta» faceva
`document.getElementById('xfer-pairs').innerHTML = ''` al `DOMContentLoaded`. Quell'id esisteva davvero, ed
era il contenitore della vista live: fino al primo render interattivo di Blazor la pagina mostrava i
coordinamenti **finti** del mockup. In più agganciava un `onclick` a ogni `.xo-chip`, comprese quelle della
catena di copertura. Nessuna pagina montava più quel prototipo: la sezione è stata **rimossa** (resta la
sezione «Aeroporto», inerte ma innocua: cerca `#sid-body`, che non esiste in nessuna pagina).

### 2. Il tetto si divide per le RIGHE della griglia

`vipiCapInner` non poteva dare a ogni colonna l'altezza piena: `repeat(auto-fit, minmax(320px,1fr))` manda
le colonne a capo quando la finestra si stringe, e due righe di colonne avrebbero occupato il doppio —
saltando la promessa proprio dove serve di più. Le righe si **contano** (dall'`offsetTop` dei figli), e la
soglia `fitMin` vale per riga: se non ci sta, la funzione si tira indietro e la pagina torna a scorrere.

### 3. Il tetto lo misura chi RENDE l'elemento, non la pagina

Prima versione: la chiamata stava in `LivePage.OnAfterRenderAsync`. ⚠️ Il filtro cambia lo stato di
`TransfersLive`, e allora Blazor ridisegna **solo lui**: `OnAfterRenderAsync` della pagina non viene
chiamato. Misurato: togliendo il filtro la pagina tornava a scorrere di 48px, perché le colonne rinascevano
senza tetto. La chiamata è passata dentro il componente.

### 4. La zebra costava il contrasto (e `probe.js` mente sull'alfa)

Righe alterne su `--surface-muted` portavano `--brand-ink` da 5,81:1 a **3,77:1** nel tema scuro — e sono
proprio ICAO e livelli, il testo per cui si guarda la riga. La zebra è diventata un velo
(`color-mix(in srgb, var(--ink) 6%, transparent)`): dark 5,81 e 5,95, light 10 e 11,43.

⚠️ **Due volte di seguito lo strumento ha mentito, e in due modi diversi.** `probe.js` non compone l'alfa:
sul velo ha risposto 2,63:1 (dark) e 1,39:1 (light), numeri peggiori di quelli veri e tutti falsi. E uno
script scritto per rimediare ha letto `color(srgb 0.894 0.916 0.991)` con una regex da `rgb()`, prendendo
`0.894` per un valore su 255: annunciava 1,83:1 su `.cop` e `.xl-next`, cioè su una classe che tutta
l'applicazione usa da mesi. **Quando un numero accusa qualcosa che sta lì da mesi, il sospetto va prima
allo strumento.** Il contrasto vero si misura risalendo i fondi e componendoli, e il moderno `color()` va
letto o convertito.

### 5. Il fondo pagina non lo vede nessuno

`vipiCapInner` misura fin dove arriva il riquadro (regola 179): i 70px di `padding-bottom` del `.wrap`, i 16
del corpo del blocco e i 12 di margine erano 35px di scorrimento su una pagina che per il resto stava
dentro. Si azzerano su `.wrap.live`, e restano i 18px di respiro che la funzione si tiene da sé — che sono
anche il bianco sotto le colonne.

### 6. «Aperto» non è «il ricevente è online»

Il pallino nei suggerimenti doveva marcare gli aeroporti «con qualche ente aperto». La prima lettura — «ha
almeno un ricevente online» — è **degenere**: il ricevente risolto è per costruzione online oppure UNICOM,
e UNICOM la vista lo nasconde, quindi il pallino sarebbe stato su tutte le voci, sempre (misurato: 17 su
17). Vale invece una posizione **di quell'aeroporto** online (`LIBD_TWR`, `LIBD_CS0_APP`, …): il componente
riceve l'insieme online come parametro. Misurato con `LIRF_TWR` acceso: 8 in cima col pallino, 9 sotto in
ordine alfabetico.

### 7. I nomi: `xl-` e non `xt-`

`/services/vsop/admin/transfers` occupa già `.xt-bar` e `.xt-apt`. Due blocchi che si contendono lo stesso
nome si scoprono solo a schermo, quindi le classi nuove sono tutte `xl-` (x-live).

## Coda: il mockup se n'è andato tutto, e cosa resta davvero

Chiesto dal committente subito dopo. `vipi-screens.js` è stato **rimosso**, insieme al suo `<script>` in
`App.razor` (e alla riga nell'elenco di `docs/guide/integration.md`, che è la copia autorevole per l'host
di Ivao.It). La sezione «Aeroporto» rimasta era guardata su `#sid-body`, che nessuna pagina ha, quindi non
faceva niente — ma **non era innocua**: se quell'id fosse ricomparso, avrebbe agganciato i suoi `onclick`
a `.sid-pill`, `.wx-tab`, `#sidSearch`, `#windDir` e `#windKt`, che esistono tutti, veri, su
`AeroportoPage` / `AirportQuickPanel` / `AeroportoEditorPage`. Stessa forma del difetto che invece stava
già sparando: una pistola carica gentilmente puntata altrove.

Con lui se ne sono andate **39 righe di regola** che nessun `.razor` nominava più
(`.xfer-switch/.xfer-tab/.xfer-view/.xfer-grid`, `.xcard*`, `.xrow`, `.xtable*`, `.xdyn/.rtag/.target-cell`,
`.xtab*` con la sua `@keyframes xpop`, `.xstyle*`) più `.transfers`, l'elenco del mockup.

⚠️ **Due misure che non arrivavano a schermo.** `.cop` dichiarava 12,5px e `.fl` 22px, ma il loro unico
utente — la riga dei coordinamenti — le riscriveva subito dopo con 11,5 e 14. Ora i valori stanno in un
posto solo, accanto a chi li usa; `.xo-chip` è passata accanto alla catena di copertura, che è l'unica a
renderla (nel mockup era un interruttore cliccabile, qui è uno stato).

**Quel che NON si è toccato, ed è la parte importante.** Una passata su tutto il foglio dice **178 classi
su 944** che nessuna sorgente nomina. Il numero **non va preso per buono**: il metodo (cercare il nome nudo
in `.razor`/`.cs`/`.js`) non vede i nomi composti a pezzi, e infatti fra i «morti» ci sono `.xt-ind1…4` e
`.k-danger/.k-info/.k-success/.k-warning`, che hanno tutta l'aria di nascere da `"xt-ind" + n` e
`"k-" + tipo`. Va guardata famiglia per famiglia, con la sorgente accanto, e non è questo giro: qui si è
tolto solo ciò che *questo* lavoro ha reso orfano, verificato uno per uno.

La passata è ripetibile: `.claude/skills/verifica-live/classi-morte.py`, da lanciare con la radice del
repo come argomento. Serve a **aprire** la domanda, non a chiuderla.

## Coda 2: la passata famiglia per famiglia (chiesta subito dopo)

**249 selettori tolti, 268 righe.** Il foglio passa da 919 classi a 751; quelle senza alcuna citazione
scendono da 157 a **sette**, e sono sette che restano apposta (vedi sotto).

Il metodo, in tre setacci sempre più stretti — e ognuno ha ripescato qualcosa che il precedente dava per
morto:

1. **Il nome nudo nelle sorgenti.** Primo setaccio. ⚠️ Non basta: non vede i `.resx`, e due stringhe di
   risorsa portano HTML con classi (`guida-kbd`, `rwy-key`) che finiscono a schermo via `MarkupString`.
   Aggiunti i `.resx`, 162 → 159.
2. **Il prefisso dentro una stringa.** Secondo setaccio, per i nomi composti: `$"xt-ind{Depth}"`,
   `$"blk-{k}"`, `$"extra-{k}"`, `class="lvl@(it.Level)"`. Sette classi salvate, e sono le sette che
   restano nell'elenco — ora con un commento accanto che spiega perché non vanno tolte.
3. **La regola può mai applicarsi?** Non si ragiona per classe ma per REGOLA: una regola muore solo se
   *ogni* parte del suo selettore (le virgole contano) nomina almeno una classe che nessuno rende. Dieci
   regole avevano una parte viva e una morta: si è riscritto il selettore, non cancellata la regola.

### ⚠️ E poi il setaccio che conta: `nessun-bersaglio.js`

I tre setacci sopra guardano il TESTO. Non possono vedere una classe costruita **interamente** da una
variabile. Così è passata `.node-badge.fss`, che nasce da `FacilityBadge(...)` — un `switch` che restituisce
`"FSS"` — più un `.ToLowerInvariant()`: la stringa `fss` non esiste in nessun file del repo.

L'ha ripescata la prova finale: prendere i selettori che il `git diff` dice rimossi e chiedere al DOM VERO,
su 29 pagine con tutti i `<details>` aperti, se ne trovano ancora qualcuno. Su 249, **uno**. Rimesso, con
il commento che dice da dove viene.

Da lì anche una correzione allo strumento: `classi-morte.py` ora confronta **in minuscolo**. Costa qualche
falso negativo, evita un falso positivo che a schermo si vede.

### Cosa se n'è andato

Residui di prototipo mai montati (`.rap`/`.rap-t`/`.rap-rwy`/`.rap-pill` della vista rapida, `.sim*` del
simulatore, `.online-list`, `.rail-jump`, `.paper`, `.states-grid`, `.prev-*`, `.chain-build`); le minime di
vettoramento nella forma vecchia (`.mva-grid`/`.mva-card`/`.mva-title`, superate da `.mva-chart` e
`.mva-leaflet`); ventuno `.xt-*` dell'editor trasferimenti mai resi; otto `.app-sec*` — ⚠️ **ma non**
`.app-sec-drag`, che è viva e sta a due righe di distanza; `.editor-toc` orizzontale (resta
`.editor-toc-side`, che è un'altra cosa); `.htree` nuda (restano `.htree-search`/`.htree-select`/`.htree-toggle`);
`.k-info/.k-success/.k-warning/.k-danger`; `.save-badge.lock-badge` e `.lock-mine`, che quel badge non ha
mai portato — porta solo `saving` e `saved`.

E **dodici commenti** che descrivevano regole non più esistenti: un commento rimasto vero a metà è il debito
peggiore di tutti.

### Cosa NON se n'è andato

- ⚠️ **`/services/vsop/admin/acc` sfora di 24px in orizzontale.** Trovato durante la verifica, ma **non è
  di questo giro**: misurato col foglio di prima e con quello di dopo, il numero è lo stesso (1624 su 1600).
  Sta in [`lavori-aperti.md` §H3](../lavori-aperti.md).
- Le sette classi composte, ora marcate nel foglio.

### Verifica

Release verde su entrambi i TFM · `dotnet test` verde · 29 pagine guidate senza una riga di errore in
console · `sweep.js` invariato (i due falsi positivi noti) · vista live invariata (900px su 900, 36 righe).
