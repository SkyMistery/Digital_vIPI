# Feature — Trasferimenti: il gruppo si vede, il Salva si raggiunge

Data: 2026-08-12 · Stato: **🟡 CARTA — da eseguire** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue [editor trasferimenti UX](2026-08-12-editor-trasferimenti-ux.md), stesso branch.

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
   | eccezione (prof. > 0) | `↳ eccezione di: <condizione del padre>` + **rientro della cella** `6 + 14·prof` px, come la vista pubblica |
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

Sotto i 1080 px, dove il pannello sta dopo la lista: all'apertura di una riga il pannello viene **portato in
vista** se non lo è (`vipiRevealPanel`, una funzione accanto a `vipiScrollToId` in `vipi-ui.js`, che non fa
nulla se il pannello è già visibile — sul monitor largo non si muove niente), e il pannello prende
`max-height: 80vh` così il piede resta agganciato anche lì.

### C · Piccolo, ma è la stessa ferita

L'avviso **«modifiche non salvate»** vive in cima alla pagina, a migliaia di pixel dal pannello che le
contiene. Va nella **testata del pannello**, dove sta la modifica.

## Test

Il cuore nuovo è deterministico e senza IO: il **ruolo di una riga nell'outline**. In
`tests/Vipi.Application.Tests` accanto a quelli della catena di condizioni:

| Test | Cosa tiene fermo |
|---|---|
| `ParentOf_Walks_Back_To_The_First_Shallower_Row_Of_The_Same_Group` | la risalita posizionale, e che si ferma al confine del gruppo |
| `ParentOf_Is_Null_For_Peers_And_For_Group_Wide_Rows` | pari-grado e trasversale non hanno padre: non sono eccezioni di nessuno |
| `ConditionChain_Still_Returns_The_Same_Chain_After_Extraction` | l'estrazione non cambia la catena (caratterizzazione: si scrive **prima**) |

## Verifica live

Rifare la ricognizione con lo stesso script e confrontare:

| Misura | Prima | Atteso dopo |
|---|---|---|
| Salva visibile a pagina in cima / a metà / in fondo | no / no / no | **sì / sì / sì** |
| Salva visibile a viewport 800 px di larghezza (colonna singola) | no | **sì**, dopo il richiamo del pannello |
| Righe di gruppo distinguibili dalle righe con sola condizione | no | **sì** (pill sul gruppo, guida verticale) |
| Riga «negli altri casi» riconoscibile | no (`—`) | **sì** (pill) |
| Classi di rientro `.xt-d1..4` residue | 4 | **0** |
| Stili inline nella pagina | 0 | **0** (invariato: tutto in classi) |

Più gli screenshot aperti a occhio (§6 della skill `verifica-live`): i numeri non dicono se un blocco *sembra*
un blocco.

## Fuori scopo

- **`rowspan` su CoP e ricevente come nel viewer**: in editor ogni riga è modificabile e ha i suoi bottoni;
  fondere le celle toglierebbe il bersaglio a chi deve cambiare il CoP di **una** variante.
- **Riordinare il gruppo per mettere la trasversale in fondo** come fa il viewer: in editor l'ordine salvato è
  la struttura, e mostrarne un altro renderebbe bugiardo il trascinamento.
- **Ridurre il numero di campi del pannello**: il pannello è lungo perché l'accordo ha quei campi; il difetto
  era non poter salvare, non averne troppi.
