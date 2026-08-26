# Le sezioni si riordinano trascinandole nel menu — carta (26 agosto 2026)

> **Stato: ✅ ESEGUITA il 26 agosto 2026**, ramo `identita-settori`.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Seguito diretto di [l'ordine delle sezioni è una scelta editoriale](2026-08-26-ordine-sezioni-personalizzato.md):
> stesso `DocumentSection.Order`, stesso gruppo, **nessuno storage nuovo**.

## La domanda

> «Nell'editor le sezioni si potrebbero spostare anche trascinandole nel pannello Navigazione? Per ora fallo
> per la vIPI di ACC, di avvicinamento e la vLOA.»

## §0 — Perché il menu, e non la card

Le frecce `↑ ↓` di J6 stanno sull'intestazione della **sezione**, cioè dentro un editor alto migliaia di
pixel: portare *Validità e revisione* in cima significa premere `↑` otto volte, e dopo ogni pressione la
sezione si sposta **fuori dallo schermo**. Il menu-sezioni è l'unico posto dove l'ordine si vede **tutto
insieme**: undici righe in 400 pixel, appiccicate a lato. È lì che il gesto ha senso.

Le frecce **restano**: sono l'unica strada da tastiera, e su un tocco il trascinamento HTML5 non esiste.
Gli editor sono comunque pagine da 1024px in su ([densità](../design/regole-ui-pagine-admin.md)).

## §1 — La regola, una sola

> **La sezione lasciata prende il posto di quella su cui la si lascia.**

Non «si inserisce prima», non «si inserisce dopo»: *prende il posto*. È la frase che si legge nel pannello
(`Toc_DragHint`) e nel `title` di ogni voce, ed è l'unica che chi scrive deve tenere a mente. Dalla stessa
frase escono due riferimenti diversi a seconda del verso, e quel conto lo fa **una funzione pura**:

`SectionOrdering.TryDropOnto(fratelli, spostata, bersaglio, out prima)` → `Vipi.Application.Content`

| verso | esempio (A B C D) | riferimento |
|---|---|---|
| in giù | A lasciata su C | «prima di **D**» (il fratello **dopo** il bersaglio) |
| in giù, sull'ultima | A lasciata su D | «in coda» (`null`) |
| in su | D lasciata su B | «prima di **B**» (il bersaglio stesso) |

Su fratelli **adiacenti** il trascinamento dà esattamente l'esito della freccia: è la stessa mossa,
generalizzata a N posti. Un test lo fissa (`Drop_of_adjacent_sections_is_the_arrow_move`).

## §2 — Il motore

`MoveSectionAsync(id, ±1)` scambia due fratelli: per saltare N posti serviva la mossa completa.

```
IEditingRepository.MoveSectionBeforeAsync(sectionId, beforeSectionId?)   // null = in coda
```

Toglie la sezione dall'elenco dei fratelli, la reinserisce davanti al riferimento e **rinumera il gruppo**
`0..n-1`. L'`Order` è una posizione, non un identificativo: l'indice su `(DocumentVersionId, ParentSectionId,
Order)` non è unico e nessuno confronta l'`Order` fra gruppi diversi. Passa dal servizio come le altre
mutazioni — autorizzazione sul documento, `EnsureLockAsync`, bozza obbligatoria (`RequireDraftAsync`).

⚠️ **Il vincolo «solo dentro il suo gruppo» sta nel motore, non nella UI.** Il riferimento deve essere un
**fratello**: se non lo è, la mossa non avviene. Non è una difesa ridondante — è ciò che rende impossibile
trasformare un riordino in una **riparentazione silenziosa**, che cambierebbe il *significato* di una
sezione e non la sua posizione (vedi §1 della carta di J6). La UI lo dice prima, il motore lo tiene.

## §3 — Il pannello

`EditorToc` è condiviso da **quattro** editor. Il trascinamento è **opt-in dell'host**, e l'interruttore è il
parametro stesso: `OnReorder` non passato ⇒ `HasDelegate` falso ⇒ ancore normali, **nessun gestore di
trascinamento registrato sul circuito**. Così l'editor aeroporto — che di sezioni-documento non ne ha
([suo modello a blocchi](2026-07-29-toc-editor.md)) — resta fuori senza una condizione dedicata, e fuori
dalla modifica l'indice resta un indice.

Ogni voce porta due campi nuovi (`EditorTocItem`):

| campo | cosa dice |
|---|---|
| `SectionId` | la voce **è** una sezione (e quindi si trascina). Null sul pannello Release e sul blocco ACC senza figli |
| `DragGroup` | il gruppo di riordino: `"root"` per APP/vLOA, `"blk-{id}"` per ogni blocco della vIPI ACC |

⚠️ **`draggable="false"` va scritto esplicitamente.** Un `<a href>` nasce trascinabile per conto suo: senza,
la voce del pannello Release si lascia prendere e poi non va da nessuna parte — un gesto che non fa niente e
non lo dice. Un test lo fissa.

I fratelli si leggono **dalle voci stesse** (stesso `DragGroup`, nell'ordine in cui sono rese): il menu è la
proiezione del documento, non un secondo elenco da tenere allineato.

## §4 — Quel che si vede

- la voce presa si attenua (`.toc-dragging`), la destinazione si illumina con la **barra gialla** a sinistra
  (`.toc-drop`) — lo stesso bordo di `.toc a.active`, in un altro colore, così «dove sono» e «dove cade»
  non si confondono;
- la destinazione si illumina **solo se accetta**: una voce di un altro blocco non si accende;
- la riga d'aiuto sotto l'intestazione compare **solo in modifica**;
- le pill `↑2 ↓1` di J6 si aggiornano da sole: sono ricalcolate dallo stesso ordine.

## §5 — Verifica live (skill `verifica-live`, copia del DB)

Le tre famiglie, con l'app vera e il browser vero (Edge + puppeteer-core, `drag.js` nello scratchpad):

| documento | rotta | gesto | esito |
|---|---|---|---|
| vIPI ACC | `/services/vsop/libb/editor` | *Configurazioni* sopra *Separazioni radar* (in giù, 2 posti) | AOR · Separazioni radar · Configurazioni; pill `↑2 ↓1 ↓1`; **persiste al ricarico** |
| vIPI APP | `…/apps/editor?app=LIBG_APP` | *Separazioni* su *AOR* | Configurazioni · AOR · Separazioni; pill `↑1 ↑1 ↓2`; persiste |
| vLOA | `…/vloa/editor?acc=LDZO` | *Purpose* su *Frequencies* | AoR · Frequencies · Purpose; persiste |

Due prove che i test non danno:

1. **Fra blocchi diversi non si sposta niente.** Trascinata `#s-512` (blocco Aerovia) su `#s-513` (gruppo
   BRINDISI CS0): il menu è **identico** prima e dopo, nessun errore in pagina.
2. **L'anteprima bozza rende l'ordine nuovo.** `/services/vsop/libb/vloa?acc=LDZO&as=draft` elenca
   *Areas of Responsibility · Frequencies · Purpose* — è la trappola del doc 11 §8, dove l'editor diceva una
   cosa e il pubblicato un'altra; qui non c'è perché il gesto scrive lo stesso `Order` che il viewer legge.

Schermate del pannello in trascinamento nei **due temi**: sorgente attenuata e destinazione gialla si
leggono in tutt'e due.

## §6 — Test

| dove | quanti | cosa fissano |
|---|---|---|
| `SectionOrderingTests` | 5 | i due versi, la coda, l'equivalenza con la freccia, il rifiuto fuori gruppo |
| `EditingRepositoryTests` | 2 | il salto di N posti + la rinumerazione; il bersaglio non-fratello non sposta niente |
| `EditorTocDragTests` (bUnit) | 8 | attributo `draggable`, i due versi, la coda **di gruppo**, i due rifiuti, il segno di destinazione |

Suite intera verde (5 314 casi sui due TFM), `dotnet build Vipi.slnx -c Release --no-incremental` **0 avvisi**.

## §7 — Cosa NON fa (e perché)

- **I blocchi della vIPI ACC non si trascinano**: nel menu sono intestazioni di gruppo, non voci, e il loro
  riordino ha una regola propria (l'Aerovia in testa, `AccDocumentService.MoveGroupAsync`, J7). Le frecce
  nell'intestazione del blocco restano l'unica strada. Da valutare se serve davvero.
- **Le sotto-sezioni non si trascinano**: il menu mostra solo il primo livello.
- **Nessun riordino fra gruppi** (§2), e **nessuna versione da tocco**: HTML5 drag non esiste su touch, e le
  frecce coprono quel caso.
