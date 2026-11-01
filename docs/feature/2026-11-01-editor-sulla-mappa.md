# L'editor sulla mappa — F9 (1 novembre 2026)

Filone Sector Lab. È la **F9** del piano della carta [«Aurora Sector Lab»](2026-09-18-aurora-sector-lab.md) §7
(«trascinare i vertici sulla mappa»), che finora non aveva una carta sua. **Da approvare**: regole proposte in §2,
domande in §3.

## §0 — La domanda

Il committente, 1 novembre: «andiamo con l'editor grafico?», nel Lab.

## §1 — Com'è oggi

- **La geometria si cambia solo nella Scheda.** Il gruppo «Geometria» ha un elenco per tratto, un campo di testo per
  vertice (coordinata, o il **nome** di un punto del catalogo dove il file lo ammette: `CampoPunto`) e i gesti +, −,
  «+ in fondo», «⇅ Inverti», «Incolla da testo» (archi compresi, con anteprima tratteggiata sulla mappa), ✂ e «Unisci».
  La Linea di un `.geo` ha lo stesso editor (D5): spostare un punto sposta la fine di un segmento e l'inizio del
  prossimo.
- **Ogni gesto è una modifica in sospeso** (`SessioneDelLab.GestoSuiVertici` → `ModificheInSospeso.CambiaVertice`,
  `AggiungiVertice`, `TogliVertice`…): ha la sua voce nella storia (annulla, ripeti), un rifiuto col motivo accanto
  all'elenco, e porta la forma sulle **copie uguali** fissate al momento del gesto (slice 8c). Il diff lo vede come
  qualunque altra riga cambiata.
- **La mappa sceglie e basta** (`sectorlab-mappa.js`): un clic su una forma la sceglie, la scelta si evidenzia; c'è un
  solo modo in cui la mappa dà coordinate al Lab, `prendi` (la posa della carta, S3: il clic dà il posto, su un punto
  dell'AIP ne prende coordinate e nome).
- **Dopo un gesto lo strato si riprende intero** (`Mappa.razor`: versione dello strato cambiata → fetch di
  `/mappa/strato/<id>`). Va bene per un gesto ogni tanto; durante un trascinamento no.
- Precisione: la mappa arriva a zoom 20, un pixel ≈ 11 cm, come i sei decimali delle coordinate.

## §2 — Le regole (proposte)

- **R1 — Un modo, un record.** Nella Geometria della Scheda (e nell'angolo della mappa quando c'è un record scelto) un
  tasto «✎ Modifica sulla mappa» mette le maniglie sui vertici del **solo record scelto**; il resto della mappa resta
  com'è (il clic sceglie). Lo spegne lo stesso tasto, `Esc`, o scegliere un altro record. Solo nella mappa dello spazio
  **Sector**: l'AIP espanso si corregge con le sue correzioni (F6), e il Confronto ha i suoi gesti di riga.
- **R2 — Trascinare = «vertice spostato».** Durante il trascinamento si muove solo il disegno, in JS, senza passare dal
  circuito; al rilascio parte **un** gesto `Cambia` con la coordinata nuova, scritta nella notazione del file. Annulla,
  ripeti, copie uguali, rifiuto e diff sono quelli di oggi: la mappa è un altro modo di fare lo stesso gesto, non un
  editor a parte.
- **R3 — Maniglia di mezzo = «vertice aggiunto».** Fra due vertici un puntino più piccolo; trascinarlo aggiunge lì un
  vertice (gesto `Aggiungi` con la coordinata).
- **R4 — Togliere.** Clic destro su una maniglia → «Togli questo vertice» (gesto `Togli`, con lo stesso rifiuto
  sull'ultimo vertice). Niente tasto `Canc` da solo: con la mappa sotto il dito si cancella troppo facilmente.
- **R5 — La calamita.** Lasciato vicino (8 px) a un punto acceso sulla mappa (fix, VOR, NDB, un vertice di un'altra
  forma) il vertice ne prende le coordinate **esatte**; `Alt` tenuto premuto la stacca. Così due forme che si toccano si
  toccano davvero, che è la base della saldatura dei bordi della F8.
- **R6 — Il vertice col nome.** Un vertice scritto col nome di un punto (una SID, un'aerovia) non si trascina «a
  caso»: si sposta solo con la calamita su un altro punto **col nome**, e diventa quel nome; lasciato altrove torna dov'era
  e il motivo compare accanto alla mappa. (Domanda D1.)
- **R7 — La Scheda segue.** Il vertice trascinato si evidenzia nell'elenco della Geometria (e l'elenco ci scorre), e
  passare col mouse su un campo dell'elenco evidenzia la sua maniglia sulla mappa.
- **R8 — Gli archi restano punti.** I punti di un arco già scritto sono vertici come gli altri: spostarne uno non
  «riapre» l'arco. Per rifare un arco c'è «Incolla da testo», come oggi.
- **R9 — Dopo il rilascio non si riprende tutto lo strato.** Si riprende **la sola forma** toccata (e le sue copie), con
  una richiesta piccola accanto a `/mappa/strato`; il tempo del ridisegno va nel diario, come per gli strati. Se il
  diario dice che lo strato intero basta, si tiene quello.

## §3 — Domande al committente

- **D1** Il vertice col nome (R6): solo la calamita su un altro punto col nome (proposta); oppure lasciato altrove
  diventa una coordinata, con un avviso; oppure non si trascina affatto.
- **D2** I bordi in comune coi settori accanto (oggi copie *parziali*, non uguali: non si muovono insieme): restano alla
  F8 (proposta: con la calamita di R5 si possono già far combaciare a mano), o in questo giro un vertice che ha la
  stessa coordinata in altre forme si sposta con loro?
- **D3** Disegnare una forma **nuova** a clic sulla mappa (un vertice per clic, doppio clic chiude): in questo giro o
  dopo (proposta: dopo, è il seguito naturale di R3).
- **D4** I record che sono un punto solo (fix, VOR, NDB, punti VFR con i loro gemelli): trascinabili in questo giro o
  dopo (proposta: dopo, perché toccano i gemelli `.vfi` ↔ `VFR_NASCOSTI.fix` e i nomi usati dalle procedure).

## §4 — Il lavoro, a fette

1. Maniglie del record scelto, trascinamento, gesto `Cambia` al rilascio, Scheda che segue (R1, R2, R7).
2. Maniglia di mezzo e clic destro (R3, R4).
3. Calamita e vertice col nome (R5, R6).
4. Ridisegno della sola forma, misurato (R9).

Ogni fetta: test bUnit dei gesti (la mappa vista dal circuito), prova sul banco a schermo, voci in `PROVE.md`.
