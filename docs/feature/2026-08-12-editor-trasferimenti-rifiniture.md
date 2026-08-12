# Feature — Editor trasferimenti: il costo per gesto, la tastiera, l'annulla, il blocco

Data: 2026-08-12 · Stato: 🟡 **in corso** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue [editor trasferimenti a tre colonne](2026-08-12-editor-trasferimenti-tre-colonne.md), stesso branch
`feature/trasferimenti-acc-app`.

## Obiettivo

La scheda precedente ha cambiato la **forma** della pagina. Questa chiude gli attriti che restano, e il primo
è un debito che **ho contratto io** rendendo frequente il salvataggio.

**Misurato sul sorgente, non stimato:**

| # | Cosa | Dove |
|---|---|---|
| **A** | Ogni salvataggio costa **8 query**, e due caricano tutti i flussi dell'ACC | `Guarded` (16 chiamate) → `LoadAsync` → `ListFlowsByAccAsync` + `GetPreviewContextAsync`, che ricarica i flussi **una seconda volta** più sei mappe |
| **B** | Sei picker si usano **solo col mouse** | 9 `@onmousedown`, zero `↑`/`↓`/Invio; markup duplicato sei volte |
| **C** | Dall'elenco non si torna al gruppo della riga | nessuna azione che leghi le due viste |
| **D** | In elenco non si ordina per colonna | solo la tendina a tre voci |
| **E** | Dall'elenco non si aggiunge una riga | «+ Riga» esiste solo nel riquadro del gruppo |
| **F** | `Tab` si ferma a fine riga | `MoveCell`, ramo `columnDelta` |
| **G** | Nessun annulla dopo un'eliminazione | — |
| **H** | La modifica in blocco fa **solo** il ricevente | `SetReceiverAsync` è l'unica |
| **I** | Il pannello tiene 380 px anche vuoto | `.xfe-layout3`, terza colonna sempre presente |

**A è il più caro, e lo è diventato adesso.** Prima si salvava di rado, dal pannello; ora si salva a **ogni
Invio in cella**. Il costo per gesto non era un problema finché il gesto era raro.

## Pre-flight — 4 domande

### 1. Modello — «aggiungo un concetto o ne esiste già uno?»

**G e H toccano il servizio**, il resto è UI.

**G — il ripristino ha bisogno di una via propria, e non è un capriccio.** `TransferPointInput` **non porta**
gruppo, profondità e ordine, ed è una scelta scritta nel tipo:

> «La PROFONDITÀ non sta qui, come non ci sta il gruppo: entrambe descrivono la posizione della riga
> nell'outline, e la decide il repository. Un editor che potesse scriverle a mano potrebbe creare una riga
> orfana o un salto di profondità in un modo che nessuna validazione a valle saprebbe attribuire a
> un'intenzione.»

Quindi ricostruire un gruppo eliminato con `AddFlowAsync` + `AddPointAsync` **appiattirebbe l'outline in
silenzio** — è già documentato che la clonazione lo fa **apposta**. Un annulla che restituisce righe diverse da
quelle tolte non è un annulla: è un secondo danno con un nome rassicurante.

Serve un ingresso distinto, e la distinzione è di **intenzione**, non di comodità:

```
AddPointAsync      = qualcuno sta SCRIVENDO una riga   → la posizione la decide il repository
RestorePointsAsync = qualcuno sta RIMETTENDO ciò che c'era → la posizione viaggia col dato
```

Due tipi nuovi, entrambi *fotografie* e non entità:

```csharp
TransferPointSnapshot(TransferPointInput Data, int Order, int? VariantGroup, int VariantDepth)
TransferFlowSnapshot (TransferFlowInput  Data, IReadOnlyList<TransferPointSnapshot> Points)
```

e due metodi: `RestoreFlowAsync` (rimette un gruppo intero) e `RestorePointsAsync` (rimette righe in gruppi che
esistono ancora — copre l'eliminazione di una riga **e** quella in blocco). Gli invarianti dell'outline si
rivalidano al ripristino: una fotografia vecchia di un archivio cambiato non deve poter entrare rotta.

⚠️ **Gli id cambiano.** Le righe ripristinate sono righe nuove: un link `?riga=N` alla riga eliminata resta
morto. È onesto e va detto, non nascosto — l'alternativa (eliminazione morbida con una colonna «cancellato»)
costa una migrazione, un filtro in ogni lettura e una nuova classe di bug, per un annulla che dura un minuto.

**H — tre metodi espliciti, non un record con sei interruttori.** Le tre operazioni non si comportano allo
stesso modo, ed è la ragione per cui non collassano in una: il **ricevente** è identità dell'accordo e si
propaga al gruppo di varianti; il **livello** e la **condizione** sono della singola riga e non si propagano.
Un `ApplyBulkAsync(edit)` con campi opzionali nasconderebbe proprio questa differenza dietro una firma sola.

`SetLevelAsync` prende un `ParsedLevel` — il tipo introdotto per l'editing in cella. Così **il livello in blocco
si scrive come si scrive in cella e come si legge in tabella**: una sintassi sola in tutta la pagina.

⚠️ **La condizione in blocco non include le piste**, e non è una dimenticanza: le piste dipendono
dall'aeroporto del flusso, e la stessa sigla su aeroporti diversi è una pista diversa. Area e testo libero sì.

### 2. Dispatch — «sto per switchare su un tipo che switcho già altrove?»

Nessuno switch nuovo. **Un duplicato che invece va chiuso**: il picker a digitazione è scritto **sei volte**
nella pagina (settore mittente ×3, ricevente ×2, area) con differenze solo di etichetta. La regola del 2 è
superata di quattro: si estrae `TypeaheadPicker`, ed è anche il posto dove la tastiera va scritta **una volta**.

### 3. Ingressi + verifica — «come ci arriva l'utente e come lo verifico?»

Ingressi invariati. L'annulla si raggiunge dal messaggio che annuncia l'eliminazione — cioè dove si sta già
guardando quando ci si accorge dell'errore.

Verifica: `/verifica-live` su copia del `vipi.db`, e per **G** il caso che conta è quello che la carta dice
delicato — eliminare un gruppo **con un outline vero** (alternativa + eccezione annidata + trasversale) e
verificare che torni con la stessa struttura, non appiattito. Il conteggio delle query di **A** si misura sul
log EF, non a occhio.

### 4. Propagazione — «questa modifica rimuove o rinomina qualcosa?»

Sì, poco: il markup dei sei picker sparisce, sostituito dal componente. Nessun tipo pubblico rimosso.
`RowSort` **cresce** (nuove chiavi di ordinamento) e resta compatibile.

## A — il costo per gesto

`GetPreviewContextAsync` compone: tipi di settore, nomi, codici, nomi ATC, nomi aeroporto. Di tutto questo
**solo i nomi liberi degli aeroporti** dipendono dai flussi (`MergeAirportNames`), e quelli cambiano solo
quando cambia un **gruppo**. Le scritture di **riga** non li toccano.

Quindi `Guarded` guadagna un solo parametro — «questa scrittura tocca i gruppi?» — e il ricaricamento del
contesto anteprima segue lui. Salvataggio di una cella: **da 8 query a 1**.

> **Perché non una cache invalidata a tempo.** Perché non serve: la condizione esatta è nota e vale una riga.
> Una scadenza sarebbe un'approssimazione di qualcosa che sappiamo con precisione.

## G — la finestra dell'annulla

L'annulla **vive quanto il messaggio che lo propone**: finché quel messaggio è a schermo, «Annulla» funziona;
la prima operazione successiva, o la sua chiusura, se lo porta via.

Non un timer di dieci secondi: un timer va comunicato (serve un conto alla rovescia a schermo, altrimenti il
tasto sparisce mentre lo si sta guardando), va cancellato quando la pagina cambia, e sopravvive a un
salvataggio in corso creando corse. Legarlo al messaggio è **una regola sola, e visibile**: c'è il messaggio,
c'è l'annulla.

La fotografia vive **nel circuito**: se la pagina si ricarica, l'annulla non c'è più. Va detto, ed è la scelta
giusta — un annulla persistente è un cestino, cioè un'altra carta.

## D, E, I — l'elenco

- **D**: le intestazioni di colonna ordinano, in elenco. `RowSort` cresce con settore/aeroporto/tipo/ricevente
  più un verso; la tendina resta all'albero, dove quelle chiavi non significano niente (in un gruppo solo,
  settore e aeroporto sono costanti). Due comandi per la stessa cosa nello stesso posto sarebbero due verità.
- **E**: «+ Riga» compare nella testata dell'elenco **quando un gruppo è scelto** — la scelta si conserva
  passando da albero a elenco, ed è già così. Senza un gruppo scelto non compare: una riga deve appartenere a
  un gruppo, e chiederlo con un secondo selettore sarebbe un giro più lungo del tornare all'albero.
- **I**: il pannello vuoto si ritira **solo in elenco**, dove la tabella ha dieci colonne e ha bisogno della
  larghezza. In albero resta: lì lo spazio c'è, e una colonna che dice cosa fare vale più di 380 px.

## Test

- Repository: ripristino di un gruppo con outline (alternativa · eccezione · trasversale) che torna
  **identico**; ripristino di righe in un gruppo esistente; fotografia che violerebbe gli invarianti →
  respinta. Blocco: livello su più righe, condizione su più righe, eliminazione multipla, e che il **ricevente**
  continui a propagarsi al gruppo mentre **livello e condizione no**.
- Dominio: `ParsedLevel` è già coperto dal round-trip della scheda precedente.
- UI: verifica live.

## Fuori scopo

- **Cestino persistente** (annulla che sopravvive al ricaricamento): è un'altra carta, con una colonna e un
  filtro in ogni lettura.
- **Sposta in blocco fra gruppi**: muove l'outline, ed è la cosa più delicata di quest'area.
- **Piste nella condizione in blocco**: dipendono dall'aeroporto, vedi §1.
