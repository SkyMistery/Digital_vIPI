# Pagina ACC (admin) — testata, intestazioni che restano, limiti che non si perdono (19 agosto 2026)

Terzo giro della stessa famiglia, dopo
[accordi](2026-08-19-accordi-densita-ui.md) e [struttura](2026-08-19-struttura-densita-ui.md):
`/vsop/admin/acc`. Qui però non è solo forma — c'è **una perdita di lavoro** (§D) trovata leggendo il codice
per il resto del giro.

## Il difetto misurato

Pagina alta **8878px**: 28 ACC e 152 settori srotolati uno sotto l'altro, con l'intestazione delle colonne
fuori schermo dopo dieci righe. Nella tabella dei settori si **scrive dentro le celle** (due caselle identiche
da 70px, «Lim. inf.» e «Lim. sup.»): senza nomi di colonna a schermo si scrive alla cieca.

## Cosa cambia

1. **Testata in una riga** (`.st-head`, la classe nata per Struttura): `ACC · «?» · ⟳ Importa da sorgente ·
   Struttura settori → ····· lock`. I due tasti salgono dalla riga-titolo alla testata perché sono comandi di
   **pagina**, non della tabella. `AccAdmin_Subtitle` rimossa da **entrambi** i resx: quello che diceva lo dice
   il «?» al clic (`AccAdmin_HelpBody`).
2. **La nota dei settori diventa il «?»** accanto a «Settori ACC»: `AccAdmin_SubsNote` riusata **identica**.
3. **Intestazioni che restano** (`.res-table.sticky-head`): `thead` appiccicato sotto la topbar (`top:62px`,
   `z-index:6` — la topbar è a 45 e deve stargli sopra). Con `border-collapse:collapse` il bordo di una `th`
   appiccicata si perde durante lo scorrimento: si rifà con `box-shadow:inset`.
4. **⚠️ I limiti non si perdono più.** `SaveLimits` chiamava `ReloadAsync`, che fa `_lower.Clear()` e ricarica
   dal DB: **ogni altra cella toccata e non salvata tornava indietro in silenzio**. Chi compilava dieci settori
   e salvava il primo perdeva gli altri nove senza un avviso. Ora:
   - `_dirtyLimits` tiene gli id con modifiche pendenti, e la cella si vede (`.lim-dirty`);
   - `ReloadAsync` **riapplica** i valori pendenti dopo il ricarico invece di scartarli;
   - **«Salva limiti (N)»** nella riga-titolo dei settori salva tutto in un giro.
   Il salvataggio per riga resta: è quello che si usa quando si tocca un settore solo.
5. **Si sceglie un ACC e sotto restano i suoi settori.** La **riga intera** della tabella di sopra è il
   comando: cliccata sceglie l'ACC, ricliccata lo lascia, e un altro ACC **sposta** la scelta invece di
   sommarsi — uno alla volta, perché è una scelta e non un insieme di spunte. Il chip «N settori» resta il
   contatore; il filo blu a sinistra e il fondo pieno dicono qual è la riga scelta (il fondo deve battere
   l'hover della tabella, che altrimenti la spegneva al passaggio del mouse); Invio e Spazio fanno lo stesso
   da tastiera. Dopo la scelta la pagina porta alla tabella di sotto (`vipiScrollToId`).
   Due dettagli che sembrano piccoli e non lo sono:
   - i tasti dell'ultima colonna **fermano la propagazione**: chi preme «Nascondi» sta facendo un'altra cosa;
   - la scelta è un **filtro a sé** (`_pickedAcc`), non il codice scritto nella casella di ricerca. Chi sceglie
     un ACC non deve ritrovarselo nel campo di testo, né perdere la scelta scrivendoci sopra.
   Nella barra dei settori compare il chip **«solo LIBB ✕»**: un filtro nato da un clic in un'**altra** tabella
   va detto dove se ne vedono le conseguenze — altrimenti restano 4 righe su 152 e non si sa perché — ed è
   anche il modo di toglierlo senza risalire.
6. **Contatori onesti**: i pill dei titoli dicono «12 di 28» quando un filtro è attivo. Prima dicevano sempre
   il totale mentre la tabella mostrava altro.
7. **Emoji colorate → `Icon`** (regola già presa nel progetto): 👁/🚫 → `eye`/`eye-off`, 💾 → `check-circle`,
   ⟳ → `refresh`, il 🚫 di «Escludi aree» → `x`. I filtri prendono `.htree-search`/`.htree-select` come in
   Struttura (e perdono il 🔎 dentro il placeholder: la lente è disegnata nel campo). Resta `🪖` della colonna
   Militare: nel set non c'è un equivalente.

8. **L'esito dell'ultima operazione sale in testata**, fra i comandi che l'hanno provocata e il lock
   (`.st-msg`, verde o giallo, con la ✕ per chiuderlo). Da fascia sotto la testata **spingeva in giù la
   tabella** mentre ci si lavorava: misurato, la prima riga adesso resta a `y=306` con e senza messaggio.
   Il chip si stringe e manda a capo il proprio testo invece di spingere via il lock, e non tronca mai — un
   messaggio troncato è mezza informazione. Stesso trattamento in **Struttura**, dove una fascia in più erano
   nodi in meno (là l'altezza del riquadro è misurata).

9. **La testata resta in cima e il «Salva limiti» sta lì.** La pagina è lunga 8878px: chi compila le celle in
   fondo alla tabella dei settori doveva risalire tutto per confermare. Ora `.st-head.sticky` si ferma sotto la
   topbar (62px, stesse quote della testata della vista live) e porta con sé il tasto — che resta spento a zero,
   come i chip.
   ⚠️ **La quota del `thead` si MISURA**: con una testata appiccicata sopra, il `thead` delle tabelle le
   passava sotto. `vipiStickyOffset` (nuova, in `vipi-ui.js`) misura la testata e scrive `--st-head-h`
   **sul `.wrap` della pagina** — non su `<html>`: cambiando pagina l'elemento sparisce e con lui il valore,
   invece di restare buono per una pagina che non c'entra. Si rimisura a ogni render **e** con un
   `ResizeObserver`, perché quell'altezza cambia da sola: 72px in riga, **120px a 1000px di larghezza** quando
   la testata va a capo. È la misura che i Trasferimenti avevano rinunciato a fare.

10. **⚠️ Lo zoom di pagina misura in un'unità e scrive in un'altra.** Segnalato a schermo: scorrendo,
    **sopra** l'intestazione della tabella compariva una striscia da cui si vedevano passare le righe.
    Causa: `vipi-zoom.js` applica `zoom` su `<html>`, e da lì in poi convivono due spazi —
    `getBoundingClientRect()` e `window.innerHeight` parlano in **pixel di finestra**, mentre tutto ciò che si
    **scrive** in CSS (`top:`, `height:`) è in **unità di layout** = pixel di finestra / zoom. Misurato:
    a zoom **1.2** il buco era di **17,6px**; a **0.8** l'intestazione finiva **sotto** la fascia (−12px).
    Corretto in `rootZoom()` (`vipi-ui.js`), usata da `vipiStickyOffset` **e** da `vipiFitViewport` — stesso
    difetto, stessa famiglia. L'arrotondamento è **per difetto**: un pixel di sovrapposizione non si vede (la
    fascia sta sopra), un pixel di buco lascia passare le righe.
    Due corollari: `vipiApplyZoom` ora emette un `resize` (cambiare zoom non fa scattare né un render Blazor né
    un resize di suo, e chi misura deve rifare i conti); e la `min-height` di `.gerarchia-2col` scende da 420 a
    **320**, cioè al `fitMin` del JS — due pavimenti diversi per la stessa cosa sono un pavimento sbagliato, e
    a zoom 1.2 vinceva quello del CSS facendo scorrere la pagina di 21px.

## Cosa NON è cambiato, e perché

- **La select nazione resta duplicata** nelle due tabelle: è lo **stesso** filtro (`_country`), muoverla in una
  muove l'altra. È voluto — separarle vorrebbe dire due stati da ricordare — e il `title` lo dice.
- **Niente altezza misurata alla Struttura**: là le colonne sono affiancate, qui le tabelle sono **impilate**.
  Incastonarle vorrebbe dire due riquadri che scorrono dentro una pagina che scorre: la trappola già pagata.

## Verifica

`dotnet build Vipi.slnx -c Release --no-incremental`: **0 avvisi, 0 errori** su entrambi i TFM. `dotnet test`:
**2570 verdi**. Guidata con Edge+puppeteer su copia del DB (porta 5035), in italiano:

- **Testata**: 51px, una riga sola — titolo+conteggio, «?», i due comandi (296px), lock a destra (289px).
  Nessuna traccia del sottotitolo né della nota dei settori nel testo della pagina; «?» chiusi al passaggio del
  mouse, aperti al clic (popover 360px).
- **Intestazioni che restano**: dopo `scrollTo(0, 3000)` la `th` «CALLSIGN» è a `y=62` — esattamente sotto la
  topbar — e visibile. Quella della prima tabella è fuori campo perché la sua tabella è tutta sopra: giusto così.
- **Scelta dell'ACC**: clic sulla riga LIBB → **4 righe** su 152, tutte `LIBB`, pill «4 di 152», chip «solo
  LIBB ✕», pagina portata a `y=1154`. Clic su LIMM → la scelta si **sposta** (6 righe, nessuna somma). Clic di
  nuovo su LIMM → tornano tutte e 152 e il chip sparisce. Premuto «Nascondi» su una riga: la scelta **non**
  scatta (`scelte []`). Invio sulla riga a fuoco: sceglie. Il campo di ricerca dei settori è rimasto vuoto in
  tutti i passaggi.
- **La prova della perdita** (il punto del giro): preso il lock, scritti `1600` in `LIBB_ES_CTR` e `2600` in
  `LIBB_EU_CTR` — due celle sporche, «Salva limiti (2)». Salvata **solo la prima** con il tasto di riga:
  `['1600','2600']`, sporca solo la seconda, «Salva limiti (1)». Prima di questo giro la seconda tornava a `GND`
  senza dire niente. Poi «Salva limiti» chiude tutto: «Salva limiti (0)», nessuna cella sporca.
- Nessun errore di console, nessuna risposta HTTP ≥400, nessuna emoji rimasta nei comandi (`data-icon` presenti:
  `refresh`, `grid`, `eye`, `eye-off`, `x`, `check-circle`).

Un ritocco nato dalla verifica: il salva-tutti diceva «**1 settori** salvati». A uno solo vale la frase del
salvataggio per riga, che è già giusta.

Sulla testata appiccicata, guidata a parte: a `scrollY 7714` (fondo pagina) la testata è ferma a `y=62` con
«Salva limiti (2)» acceso, il `thead` subito sotto a `y=134` (= 62+72), e il salvataggio parte **senza risalire**
— «2 settori salvati.» compare nella testata stessa. A 1000px di larghezza la testata va a capo (120px) e il
`thead` la segue a `y=182`.

Sullo zoom, dopo la correzione (ACC scorsa a 3000px, Struttura a riposo):

| Zoom | Buco sopra il `thead` (ACC) | Struttura: altezza scritta | La pagina scorre? |
|---|---|---|---|
| 0.8 | −0,78px (sovrapposizione invisibile) | `717px` | no |
| 1.0 | 0 | `517px` | no |
| 1.2 | −0,39px | `384px` | no (prima scorreva di 21px) |
| 1.5 | 0 | nessuna (sotto `fitMin`) | sì — sotto i 320px di riquadro la pagina scorre per scelta |

Un secondo ritocco, dallo screenshot: il titolo di sezione «ACC» ripeteva il titolo della pagina tre righe più
sotto. È sparito e il suo conteggio è salito accanto al titolo (`ACC 28`, «12 di 28» quando filtri). Il primo
riquadro **è** l'elenco; il secondo tiene il suo titolo perché lì comincia un'altra cosa.
