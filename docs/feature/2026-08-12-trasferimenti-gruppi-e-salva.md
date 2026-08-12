# Feature — Trasferimenti: il gruppo si vede, il Salva si raggiunge

Data: 2026-08-12 · Stato: **CHIUSO — suite 2203 verde, Release 0 warning su entrambi i TFM, ✅ verifica live** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue [editor trasferimenti UX](2026-08-12-editor-trasferimenti-ux.md), stesso branch.

> ⚠️ **Il collasso a colonna singola descritto al §«Il Salva» è cambiato lo stesso giorno**: la pagina è passata
> a tre colonne (`.xfe-layout3`) in
> [editor trasferimenti a tre colonne](2026-08-12-editor-trasferimenti-tre-colonne.md), dove l'altezza non è più
> un `calc` ma una misura, e `vipiRevealPanel` serve solo sotto le soglie di collasso. Il pannello
> testata · corpo · piede — il cuore di questa scheda — è rimasto quello.

## Obiettivo

Due difetti riportati guardando la pagina reale, dopo il giro di ieri:

1. **In tabella non si vede quali condizioni sono eccezioni/alternative di quali.**
2. **Il Salva del pannello non è raggiungibile** senza cercarlo: bisogna scorrere fin giù.

## Ricognizione — misurato, non supposto

Edge + puppeteer-core su istanza dedicata (copia del `vipi.db`, `VipiAuth__Enabled=false`, porta 5057),
lock preso, ACC **LIBB**, «espandi tutto». Numeri:

| Misura | Valore |
|---|---|
| Tabelle di flusso rese | 36 |
| Righe totali | 78 |
| Righe dentro un gruppo di varianti (`VariantGroup` non nullo) | **5** (2 gruppi) |
| Righe a profondità > 0 (eccezioni vere) | **0** |
| Altezza del contenuto del pannello (riga aperta) | **1426 px** |
| Altezza disponibile al pannello (viewport 1000) | **904 px** (`max-height:calc(100vh - 94px)`) |
| Posizione del bottone Salva | `top: 1239 px` → **fuori dal viewport in ogni posizione di scorrimento della pagina** |
| `position` effettiva del pannello | `sticky`, funziona (top 78 a pagina scorsa) |

### Difetto 1 — il gruppo non si legge

Nel gruppo misurato tre righe **TOPNO** portano condizione `25 · area Donald West`, `07`, `—`. A schermo si
distinguono dalle righe normali solo per una **velatura di fondo uguale per tutti i gruppi**, e:

- niente dice che quelle tre sono **lo stesso accordo** in tre casi;
- niente dice che la riga con condizione `—` è il **«negli altri casi»** del gruppo;
- due gruppi consecutivi hanno la stessa velatura: **dove finisce uno e comincia l'altro non si vede**;
- il rientro per profondità esiste (`.xt-d1..4`) ma **vive solo nella cella CoP**: la colonna Condizione,
  che è dove si legge l'annidamento, resta allineata;
- il contrario è ugualmente ambiguo: due righe **BIRSU** con condizioni `07` e `25 · area LI R403B` *non*
  sono un gruppo, ma a occhio sembrano alternative esattamente come le TOPNO;
- le righe di anteprima (`.xfer-prev`) si infilano **fra** le righe del gruppo e ne spezzano il blocco.

**Il modo giusto esiste già nel progetto**: la vista pubblica (`CoordTable.razor`) rende i gruppi come
**blocchi** — `rowspan` su CoP e ricevente, classi distinte per capofila / alternativa / annidata /
trasversale, rientro della **colonna condizione** proporzionale alla profondità. L'editor è l'unico posto
dove lo stesso dato si legge peggio della pagina che lo pubblica.

### Difetto 2 — il Salva sotto la linea di galleggiamento

Lo sticky **funziona**: il pannello resta agganciato a 78 px. Il problema è un altro, ed è aritmetico: il
contenuto è **1426 px** in **904** disponibili, e le azioni stanno **in fondo al contenuto**. Il pannello
scorre internamente (`overflow:auto`), quindi il Salva si raggiunge solo con una **seconda barra di
scorrimento**, dentro il riquadro, che non si vede finché non ci si passa sopra con la rotella. Scorrere la
pagina non lo porta mai in vista: misurato in cima (top 1918), a metà (1239) e in fondo (1185) — **mai
visibile**. Con «Azioni sulla riga» aperte sotto il Salva, il tratto da scorrere è ancora più lungo.

Il caso citato («se ho più voci aperte») è peggiore per una seconda ragione: **sotto i 1080 px** la griglia
`.xfe-layout` collassa a una colonna e il pannello finisce **dopo tutta la lista** — lì lo sticky non può
fare nulla, e la pagina va scorsa per intero davvero.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova, nessun campo nuovo. Il rapporto padre-figlio nell'outline **esiste già**
ed è posizionale (`CoordinationDerivation.ConditionChain`: si risale all'indietro fino alla prima riga meno
profonda dello stesso gruppo). Serve la stessa lettura anche all'editor: si **estrae** da `ConditionChain` un
`ParentOf(flowPoints, p)` che `ConditionChain` stessa userà — una regola, un posto. Mai una seconda copia
della risalita: due letture divergenti dell'outline direbbero due strutture diverse sullo stesso dato.

**2. Dispatch.** Nessuno `switch` nuovo. Il ruolo della riga nel gruppo (capofila · alternativa · eccezione ·
trasversale) è **calcolato**, non dichiarato, dagli stessi tre campi che già esistono.

**3. Ingressi + verifica.** Nessun ingresso nuovo: è la pagina che c'è. Verifica: la stessa ricognizione di
sopra rifatta a valle, con i numeri a confronto, **più** una misura che oggi manca — il Salva visibile in
tutte e tre le posizioni di scorrimento e a viewport stretta (1000 e 800 px di larghezza).

**4. Propagazione.** Additivo. `.xt-d1..4` (rientro nella cella CoP) viene **sostituito** dal rientro sulla
colonna condizione: le classi vanno rimosse dal tema insieme al loro uso, non lasciate orfane.

## Cosa cambia

### A · La tabella dice il gruppo (difetto 1)

1. **Blocco visibile.** Le righe dello stesso `(flusso, gruppo)` prendono una **guida verticale** a sinistra
   (`box-shadow: inset 3px 0`) e un bordo che **apre e chiude** il blocco. La guida cambia tinta fra un gruppo
   e il successivo (due tinte alternate) così due gruppi adiacenti non si fondono. Le righe di anteprima del
   gruppo portano la stessa guida: il blocco resta un blocco.
2. **Intestazione del gruppo**: sulla capofila, accanto al CoP, una pill «⑂ *n* varianti» con `title` che
   spiega la regola. Chi guarda sa che le righe che seguono sono **casi dello stesso accordo**, e — per
   differenza — che le righe senza pill non lo sono (il caso BIRSU).
3. **Ruolo nella colonna Condizione**, che è la colonna della domanda:
   | Riga | Cosa mostra |
   |---|---|
   | fuori gruppo | la condizione com'è oggi |
   | alternativa (prof. 0, con condizione) | `se <condizione>` |
   | alternativa senza condizione | pill **«negli altri casi»** (oggi è un `—` muto) |
   | eccezione (prof. > 0) | `↳ eccezione di: <condizione del padre>` + **rientro della cella** 30/48/66/84 px, come la vista pubblica. Senza condizione propria resta un `—`: non è il «negli altri casi» di niente, è una riga incompleta |
   | trasversale (`IsGroupWide`) | pill «vale per tutte» — resta dov'è, ma nella colonna condizione come nel viewer |
4. **Il gruppo aperto si illumina**: quando una riga è aperta nel pannello, le sue sorelle prendono una
   velatura leggera. Modificare una variante senza vedere le altre è il modo di scrivere due volte lo stesso caso.

Il rientro sulla cella CoP (`.xt-d1..4`) sparisce: l'annidamento si legge in **un** posto, quello dove il
viewer lo legge già.

### B · Il Salva sta sempre a schermo (difetto 2)

Il pannello diventa **testata · corpo · piede**:

- `.xt-panel` → `display:flex; flex-direction:column; overflow:hidden` (lo scorrimento esce dal contenitore);
- `.xt-panel-body` → `flex:1; min-height:0; overflow:auto` — scorre **solo** il corpo dei campi;
- `.xt-panel-foot` → piede fisso con **💾 Salva · ＋ Salva e nuova · Annulla**, bordo superiore e fondo pieno.

Le **azioni sulla riga** (sposta, duplica, sfila, elimina) salgono nel corpo, sopra il piede: sono secondarie e
non devono stare fra chi scrive e il bottone che scrive.

All'apertura di una riga il pannello viene **portato in vista quando il suo piede non lo è** (`vipiRevealPanel`,
accanto a `vipiScrollToId` in `vipi-ui.js`): serve sotto i 1080 px, dove il pannello sta dopo tutta la lista, e
serve anche a schermo largo con la pagina in cima. Se il piede è già a posto non muove niente. Sotto i 1080 il
pannello prende `max-height: 80vh`, così il piede resta agganciato anche a colonna singola (sotto i 900
`.detail-sticky` toglieva il tetto all'altezza).

La chiamata parte da `OnAfterRenderAsync`, non dal gestore del clic: dentro il gestore il pannello a schermo è
ancora quello di prima — spesso vuoto, senza piede — e la misura cadrebbe sul riquadro sbagliato.

### C · Piccolo, ma è la stessa ferita

L'avviso **«modifiche non salvate»** vive in cima alla pagina, a migliaia di pixel dal pannello che le
contiene. Va nella **testata del pannello**, dove sta la modifica.

## Test — tre, scritti prima

Il cuore nuovo è deterministico e senza IO: il **ruolo di una riga nell'outline**. In
`tests/Vipi.Application.Tests/CoordinationDerivationTests.cs` accanto a quelli della catena di condizioni:

| Test | Cosa tiene fermo |
|---|---|
| `ParentOf_Walks_Back_To_The_First_Shallower_Row_Of_The_Same_Group` | la risalita posizionale, e che si ferma al confine del gruppo |
| `ParentOf_Is_Null_For_Peers_And_For_Group_Wide_Rows` | pari-grado e trasversale non hanno padre: non sono eccezioni di nessuno |
| `ConditionChain_Still_Returns_The_Same_Chain_After_Extraction` | l'estrazione non cambia la catena (caratterizzazione: si scrive **prima**) |

Suite **2203 verde**, `dotnet build Vipi.slnx -c Release --no-incremental` **0 warning su entrambi i TFM**.

## ✅ Verifica live — eseguita il 12 agosto 2026

Stesso script della ricognizione, stessa istanza dedicata (copia del `vipi.db`, porta 5057), lock preso, LIBB.

| Misura | Prima | Dopo |
|---|---|---|
| Salva visibile **all'apertura di una riga** | no | **sì** (la pagina scorre da sé di 746 px, e solo se serve) |
| Salva visibile a pagina scorsa a metà / in fondo | no / no | **sì / sì** |
| Salva visibile a viewport 800 px (colonna singola) | no | **sì** |
| Righe di gruppo distinguibili da righe con sola condizione | no | **sì**: 2 pill «⑂ n varianti», 2 blocchi che aprono e 2 che chiudono |
| Riga «negli altri casi» riconoscibile | no (`—`) | **sì** (pill tratteggiata) |
| Eccezione: si legge di quale riga lo è | no | **sì**: «↳ exception to: 25 · area Donald West» + rientro `xt-ind1` |
| Sorelle del gruppo illuminate a riga aperta | no | **sì** (6 righe) |
| Classi di rientro `.xt-d1..4` residue | 4 | **0** |
| Stili inline nella pagina | 0 | **0** (invariato) |

L'unica posizione in cui il Salva resta fuori è la pagina **scorsa a mano fino in cima**: lì il pannello
comincia a 757 px e non ci sta, ma è il caso in cui l'editore ha deciso di guardare la testata, non il form.

**Il caso «eccezione» non esisteva nei dati**: su 78 righe le profondità > 0 erano **zero**, quindi la
resa nuova sarebbe rimasta non provata. È stata creata un'eccezione **vera** guidando l'editor sulla copia del
DB — ed è così che sono venuti fuori i tre difetti sotto.

### Coda — la scheda non ci stava, e il Salva non era tutto il problema

Rileggendo la pagina è arrivato un terzo difetto, **lo stesso in un altro punto**: per vedere **tutta** la
scheda di riga nuova bisognava scorrere. Misurato sul percorso «+ Riga», che prima non era stato misurato:

| | 1600 × 1000 | 1366 × 720 (portatile) |
|---|---|---|
| Contenuto della scheda | 885 px | 885 px |
| Spazio disponibile | 781 px | 501 px |
| **Fuori dalla vista** | **104 px** | **384 px** |

Il piede aveva risolto il Salva, non la scheda: il resto restava dietro la barra di scorrimento **interna**, che
non si vede finché non ci passi sopra con la rotella — quindi si scorreva la pagina, che non c'entra.

**Le sezioni del pannello si richiudono**, e partono chiuse **quando sono vuote**: chi apre una riga nuova
scrive CoP, ricevente e livello, mentre trasferimento e condizione servono su una riga su quattro (misurato ieri:
su 76 righe, pista 4 · area 2 · personalizzata 0). Una sezione chiusa **dice cosa contiene** con un riassunto
accanto al titolo — «al confine dell'AoR · passando FL110», «25 · area Donald West», o «vuota» — perché una
sezione che nasconde un dato senza dirlo è peggio di una scheda lunga. E una sezione che **ha** un dato resta
aperta: non si nasconde ciò che qualcuno ha scritto.

Risultato: contenuto **885 → 502 px**, **zero** fuori dalla vista su entrambi gli schermi, la scheda intera
dentro il viewport (piede e testata compresi). In più il corpo ha ora l'**ombra** che dice se sotto c'è altro, e
`vipiRevealPanel` mira alla scheda intera quando ci sta, non al solo piede — mirando al piede restavano fuori i
pixel di bordo sotto di lui.

### Seconda coda — «sono tutto giù e ci sono tasti che non vedo»

Segnalato di nuovo, e stavolta sul percorso **riga esistente**, che avevo misurato meno di quello della riga
nuova. Riprodotto censendo **ogni** tasto del pannello su tre schermi: una riga vera apre il pannello con
faccetta e condizione **già piene**, quindi le sezioni restano aperte (giustamente: non si nasconde un dato) e
il corpo arriva a **1324 px**. Sei tasti fuori dallo schermo, gli stessi ovunque:

| Schermo | Corpo visibile | Fuori | Tasti irraggiungibili |
|---|---|---|---|
| 1600 × 1000 | 781 px | 543 px | ▲ Sposta su · ▼ Sposta giù · ⧉ Duplica riga · ⇤ Sfila · ⧉ Duplica gruppo · ✕ Elimina |
| 1366 × 720 | 501 px | 823 px | idem |
| 1280 × 620 | 401 px | 923 px | idem |

Erano in fondo al **corpo**, che scorre per conto suo: scorrere la **pagina** — anche fino in fondo — non li
porta mai in vista, ed è esattamente ciò che si prova a fare. Ora sono un **menù del piede** («⋯ Azioni sulla
riga») che si apre verso l'alto: il piede non scorre, quindi le sei azioni si raggiungono sempre, e tengono le
loro parole invece di tornare icone. Misurato dopo: **zero** tasti fuori campo su tutti e tre gli schermi, a
menù chiuso e aperto.

Nel corpo restano i campi, e restano scorribili: quello è un dato che si legge, non un comando che si cerca.

### Tre difetti trovati guardando, che nessun test avrebbe visto

- **Un'eccezione appena creata nasceva marcata «negli altri casi».** La pill vale per le alternative
  pari-grado; un'eccezione senza condizione non è il caso che resta, è una riga incompleta. Ora resta il
  trattino, e la pill compare solo a profondità 0.
- **`vipiRevealPanel` mancava il bersaglio di 15 px.** Il pannello è sticky: scorrere la pagina sposta anche
  lui, quindi `scrollIntoView` insegue una posizione che cambia mentre scorre. Due-tre passate convergono. E il
  bersaglio è il **piede**, non il riquadro: un pannello che comincia a schermo ma finisce sotto la piega ha il
  Salva fuori campo lo stesso.
- **Il rientro delle eccezioni non si vedeva.** `.res-table td{padding:7px 10px}` batte una classe sola — la
  stessa trappola già annotata nel tema per `.sid-view` — e a 20 px il rientro valeva dieci pixel. Ora
  30/48/66/84 con `td` nel selettore.

Più gli screenshot aperti a occhio (§6 della skill `verifica-live`): i numeri non dicono se un blocco *sembra*
un blocco. È da lì che è venuta l'ultima correzione — le anteprime bianche in mezzo alle righe grigie
spezzavano in cinque un blocco solo, e ora prendono la velatura del gruppo.

## Fuori scopo

- **`rowspan` su CoP e ricevente come nel viewer**: in editor ogni riga è modificabile e ha i suoi bottoni;
  fondere le celle toglierebbe il bersaglio a chi deve cambiare il CoP di **una** variante.
- **Riordinare il gruppo per mettere la trasversale in fondo** come fa il viewer: in editor l'ordine salvato è
  la struttura, e mostrarne un altro renderebbe bugiardo il trascinamento.
- **Togliere campi dal pannello**: l'accordo ha quei campi, e nessuno è di troppo. Quelli che servono di rado
  ora partono **richiusi con il riassunto**, che è un'altra cosa dal toglierli.
