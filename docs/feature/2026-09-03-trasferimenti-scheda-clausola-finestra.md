# Feature — Trasferimenti: la scheda della clausola diventa una finestra, e quattro difetti di collegamento

Data: 2026-09-03 · Stato: **CHIUSO — suite verde (Vipi.Ui 1147×2), Release 0 warning su entrambi i TFM,
✅ verifica live eseguita** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Sostituisce in parte [editor trasferimenti a tre colonne](2026-08-12-editor-trasferimenti-tre-colonne.md) —
le colonne restano **due**.

## Da dove nasce

Il committente guarda `/services/vsop/admin/transfers` e dice tre cose: in notturna ci sono **barre bianche**
nella scheda «Edit row», le **✕ dei chip d'aeroporto hanno il fondo grigio**, e la scheda «com'è messa lì è
scomoda». Propone una finestra.

Le prime due sono difetti. La terza è una domanda di disegno, e la risposta è stata **misurata prima di
scriverla** — due volte, perché la prima stima era sbagliata.

## I quattro difetti (misurati a schermo, non dedotti)

| # | Difetto | Causa | Dove |
|---|---|---|---|
| 1 | Due lastre bianche da 24px dentro la scheda, in notturna | le due falde-**coperchio** dello scroll shadow erano dipinte con `--on-brand` (`#fff`, definito **una volta** e mai ribaltato nei blocchi scuri: è un token di **brand**, non di tema) | `vipi-theme.css` `.xt-panel-body` |
| 1-bis | …e si vedevano **sempre**, anche senza niente da scorrere | è proprio il coperchio a doverle nascondere agli estremi. Del colore sbagliato non copre: si mostra. Misurato: `scrollHeight == clientHeight` e le barre c'erano lo stesso | idem |
| 2 | La ✕ dentro un chip esce **bottone di sistema**: `background rgb(107,107,107)`, `border 2px outset`, 27×23px dentro una pastiglia alta 20 | `.xt-chipx` è nel markup in **tre** punti e nel foglio di stile in **zero** | razor 1412 · 1712 · 1782 |
| 3 | Il piede della scheda senza riga di separazione, e l'**elimina ✕ appiccicato al duplica ⧉** | markup `xt-panel-f`, CSS `.xt-panel-foot`: regola morta → `display:block`, quindi lo spaziatore `.ln.xt-grow` non spingeva niente. ⚠️ Anche `vipiRevealPanel` cercava `.xt-panel-foot` e non lo trovava | razor 2072 vs css 2643 |
| 4 | La testata della scheda a **due righe** (57px misurati), con la ✕ da sola sulla seconda | `flex-wrap` + un sottotitolo che non si accorcia | `.xt-panel-h` |

Trovati incrociando le classi del DOM con il testo di **tutte** le regole caricate: due orfane vere
(`xt-chipx`, `xt-panel-f`) e tre innocenti (`xt-flip`, `xt-in`, `xt-dir`, che sono agganci, non stili
mancanti). ⚠️ Il primo controllo, con un confronto per sottostringa, **non aveva visto `xt-panel-f`**: lo
copriva `.xt-panel-foot`. Serve il confine di parola — un controllo che non trova niente non prova niente.

## La domanda di disegno, e la misura che ha ribaltato la prima risposta

Nella colonna da 380px il corpo della scheda ne aveva **348** per **896px di contenuto** in 502 visibili:
due schermate di scorrimento per scrivere una riga.

La prima proposta diceva «la finestra è larga il doppio, quindi ci sta tutto». **Falso, e misurato:**

| contenitore | corpo | contenuto |
|---|---|---|
| colonna 380 | 348 | **896** |
| finestra 720 | 688 | 878 |
| finestra 860 | 828 | **860** |
| a tutta pagina | 1528 | 860 |

Il 4%. I campi sono impilati in una colonna sola e le righe a tre campi ci stavano **già** a 348: la
larghezza in più finiva in campi più larghi, non in righe in meno.

Quello che paga è la larghezza **spesa in colonne**: 896 → **666** a due colonne. Da lì la scelta fra le due
proposte diventa aritmetica.

| | contenuto | utile | esito |
|---|---|---|---|
| **A — finestra 88vh** | 666 | ~636 | entra, o quasi |
| **B — cassetto 40vh** | 592 (a tutta larghezza) | ~365 | scorre ancora, quasi il doppio |

Il committente ha chiesto se B si salvasse recuperando il cromo (nav + testata: **~150px** su 259 misurati
sopra la griglia). No, e anche questo è aritmetica: il cassetto ne chiede **676** e alla tabella ne
resterebbero 111. Perché funzionino tutti e due servono ~1076px di griglia contro i ~787 disponibili — un
viewport da 1200 di altezza. **Il cromo recuperato darebbe gli stessi 150px a tutte e due, ma B li deve
dividere in due e A no.**

Scelta del committente: **A**, e il lock resta dov'è.

## Il layout

```
┌───────────────┬─────────────────────────────────────────────┐
│ NAVIGATORE    │ RIQUADRO DI LAVORO                          │
│               │                    ┌───────────────────────┐│
│ ▾ LGGG        │  ARRIVI …          │ Edit row   ‹ 3/4 › ✕  ││
│   LIBB ⇄ LGGG │  ┌──────────────┐  ├───────────────────────┤│
│ ▸ LIRR        │  │ PAPIZ  FL150 │  │ PUNTI                 ││
│               │  │ DINOB  dispari  │ [vincolo][val][unità] ││
│ [+ Accordo]   │  └──────────────┘  │ [parità] [stato vert.]││
│               │                    ├───────────────────────┤│
│               │                    │ 💾 Salva ↑ ↓ ⧉     ✕ ││
│               │                    └───────────────────────┘│
└───────────────┴─────────────────────────────────────────────┘
    260px                    1fr              finestra, fuori dalla griglia
                                              min(920px, 94vw) · max 88vh
```

La scheda sta **fuori** da `.xfe-layout3`: là dentro occuperebbe una traccia, ed è la traccia che le abbiamo
tolto. I 380px tornano alla tabella in **tutte e due** le viste — anche in elenco, dove le colonne sono dieci
e prima `xt-noPanel` doveva restituirglieli a mano.

**Il corpo va a due colonne.** Attraversano tutta la riga solo le cose che parlano del blocco intero:
intestazioni di gruppo, avvisi, anteprima della frase, e il campo dei **punti** (che è un elenco di gettoni e
si marca con `Class="xt-wide"`). ⚠️ L'elenco dell'**area** invece **non** attraversa: a tutta larghezza
lasciava un buco alla destra della casella delle piste — e rimetterlo in mezza riga ha portato il contenuto
da 741 a **666**.

## Quello che la finestra toglieva, e come è ripagato

La colonna dava per costruzione una cosa: si cliccava la ✎ della riga accanto senza chiudere niente.

- **‹ N/M ›** nella testata cammina fra le clausole della **sezione**. Il perimetro è la sezione e non
  l'accordo: un passo che scavalca il verso cambierebbe mittente e ricevente sotto le mani di chi scrive.
- ⚠️ **Non ↑↓**: quelle nel piede **spostano** la clausola. Due gesti opposti con la stessa freccia sono un
  errore che si scopre solo dopo averlo commesso.
- **Esc** chiude, e arriva per risalita dai campi. L'unico che se lo tiene è `TypeaheadPicker` **mentre la
  tendina è giù** (`@onkeydown:stopPropagation="_open"`): lì Esc vuol dire «chiudi la tendina», non «butta
  via quello che sto scrivendo». Al secondo Esc la tendina non c'è più, il tasto risale, e chiude.
- **Il velo** chiude come la ✕. È la via d'uscita che si trova per prima quando non si sa cosa fare.
- La finestra si prende il **fuoco** all'apertura: senza, la tastiera resta sulla riga dietro il velo.

## Propagazione (domanda 4 del gate)

| Cosa sparisce | Chi lo citava | Fatto |
|---|---|---|
| `vipiRevealPanel` | `vipi-ui.js`, la chiamata in `OnAfterRenderAsync`, e due doc di feature dell'agosto | funzione rimossa; `_revealPanel` → `_focusPanel` (fa il fuoco, non lo scorrimento); pointer nei due doc |
| `.xt-noPanel` | `vipi-theme.css`, il markup della griglia | rimosse entrambe |
| la terza colonna a 1200 (`.wrap.pw-1200 .xfe-layout3 …`) | serviva solo a far scendere il pannello | rimossa; lo scaglione che conta ora è **900** |
| `xt-panel-f` | markup | rinominato nel nome che il foglio stila davvero |
| «il pannello di **destra**» | `<summary>` di `ClausePanel`, memoria `trasferimenti-acc-app-carta` | riscritti |

Il ramo «scheda vuota» **resta**, ma come guardia: ci si arriva solo se la clausola sparisce sotto le mani
(l'elenco si ricarica). Porta il suo tasto per uscire, che dentro una finestra non è un di più.

## Una rifinitura trovata guardando

`RUNWAY IN USE(NO RUNWAY IMPORTED)`, senza spazio. Lo spazio sta accanto a un blocco condizionale e non a un
tag, e Razor se lo mangia. La cura è `.xt-pickhint`, la classe che esiste apposta — non uno spazio nel
markup: la spaziatura è presentazione.

## Verifica live (traccia)

Guidata su `localhost:5034`, accordo `LIBB_ES_CTR ⇄ LGGG_W_CTR` e sezione sorvoli `LGGG_W_CTR → LIBB_ES_CTR`
(4 clausole), in **notturna e in chiaro**, viste **albero ed elenco**:

- barre bianche: **sparite** in notturna; in chiaro nessun artefatto nuovo;
- ✕ dei chip: piatta, senza fondo, si accende al passaggio;
- piede: riga di separazione, e l'**elimina** spinto a destra, lontano dal duplica;
- testata su **una riga**, con `‹ 3/4 ›` e la ✕ in coda;
- `›` porta da `LATAN` a `DINOB` e la riga evidenziata sotto **segue**;
- Esc chiude (provato anche in vista a elenco);
- altezze: **come si apre 378 in 378 — non scorre**; forzando aperte tutte e tre le sezioni 666 in 636, cioè
  **30px fuori invece di 394**.

⚠️ Il test `StructureAccessibilityTests.Nessun_comando_raggiungibile_col_solo_mouse` è diventato **rosso**, e
aveva ragione: il velo è un `<div @onclick>`. Sta in whitelist con la ragione scritta accanto, come quello di
`DeleteDialog` — non perché sia tollerabile, ma perché **duplica** due comandi che la tastiera raggiunge già.

## Coda: la fascia «lock in scadenza» era un falso allarme

Misurando avevo visto quella fascia entrare nel flusso e spingere giù tutto di **76px** mentre si scrive, e
l'avevo segnalata come cosa da rifare. La domanda del committente — *«ma il lock non si rinnova da solo?»* —
ha trovato la causa vera, che non era la forma dell'avviso ma il fatto che **non doveva esserci**.

Il conto: `ResourceLockService.LockTtlMinutes = 3`, `EditLockBar` batte ogni **60 s**. Il residuo oscilla fra
**180 e 120 secondi**, e la soglia dell'avviso è **60**: irraggiungibile finché il battito è vivo.

Il difetto stava in `HeartbeatLoop`: `NotifyAsync()` — l'unica cosa che pubblica `ExpiresChanged` verso la
pagina — era **dentro** l'`if (_lock.IsMine != was)`. Cioè la scadenza si comunicava solo quando cambiava il
**proprietario** del lock; il rinnovo la aggiornava nel campo del componente e non la diceva a nessuno. La
pagina restava con la scadenza della presa, e dopo ~2 minuti si credeva in scadenza.

⚠️ La `<summary>` del parametro **prometteva già** «a ogni battito»: il documento era giusto e il codice no.
E la prova stava negli screenshot della verifica precedente — barra *«until 23:41Z»*, avviso *«expires at
23:38Z»*: tre minuti esatti di scarto, cioè un rinnovo avvenuto e mai detto.

Ora la scadenza si pubblica quando **si muove**, e il ri-render serve anche alla barra: su una pagina
tranquilla — dove il genitore non ridisegna da sé — l'ora che mostra restava indietro quanto l'avviso.

**Verifica live**: preso il lock e guardato per **3 minuti e mezzo**. Tre rinnovi pubblicati (01:05:59 →
01:06:59 → 01:07:59 → 01:08:59) e la fascia **mai comparsa**, anche oltre i 120 secondi, che è il punto in
cui prima scattava. ⚠️ Non è coperto da un test: il difetto vive dentro un `PeriodicTimer` da 60 secondi, e
metterlo alla prova vorrebbe dire o aspettare un minuto per asserto, o iniettare un orologio finto in un
componente che oggi non ce l'ha. Per questo la traccia è la misura qui sopra.
