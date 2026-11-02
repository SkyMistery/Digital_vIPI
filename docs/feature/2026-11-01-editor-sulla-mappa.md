# L'editor sulla mappa — F9 (1 novembre 2026)

Filone Sector Lab. È la **F9** del piano della carta [«Aurora Sector Lab»](2026-09-18-aurora-sector-lab.md) §7
(«trascinare i vertici sulla mappa»), che finora non aveva una carta sua. **Approvata il 1 novembre come proposta**
(«Sì, vai»): regole R1-R9 di §2, e le quattro domande di §3 con le risposte proposte (D1 solo la calamita, D2 alla F8,
D3 e D4 dopo).

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

## §5 — Fetta 1, fatta (1 novembre, sera)

- **Servizio** (`SessioneDelLab.Maniglie.cs`): `ModificaSullaMappaDi` accende le maniglie sul record scelto (solo se ha
  vertici); `Maniglie()` le dà con la posizione di adesso — i vertici col nome al posto del loro punto nel catalogo del
  master scelto, un nome che il catalogo non conosce senza maniglia; della Linea di un `.geo` solo i punti intorno al
  segmento scelto, come la Scheda (`IntornoDellaLinea`, ora uno solo). `VerticeDallaMappa` fa il gesto `Cambia` di
  `GestoSuiVertici` con la coordinata scritta da `CoordinateConverter.ToDottedDms`: storia, copie uguali e diff sono
  quelli della Scheda. Rifiuta un vertice col nome (R6, finché non c'è la calamita) e un posto fuori dal mondo.
  Scegliere un altro record spegne le maniglie, e tornandoci restano spente.
- **Mappa** (`sectorlab-mappa.js`, `Mappa.razor`): marcatori di Leaflet trascinabili (DOM sopra la tela: sono i
  vertici di un record), i due tratti vicini tratteggiati durante il trascinamento, un gesto al rilascio; rifiutato, la
  maniglia torna dov'era. Il componente rimanda le maniglie quando cambiano (firma di campo, posizione e coordinate):
  un gesto, annulla e ripeti le fanno seguire da sole. Maniglie che arrivano a metà trascinamento aspettano il rilascio.
  Esc le spegne.
- **Scheda**: il tasto «✎ Modifica sulla mappa» in testa alla Geometria; il vertice mosso dalla mappa segnato
  nell'elenco e portato in vista (`sectorlab.inVista`); il mouse su un vertice accende la sua maniglia; il rifiuto
  accanto all'elenco, come per i tasti.
- **Prove**: 8 test (`EditorSullaMappaTests`), suite del Lab 1540. Sul banco: LIBB_FSS 48 maniglie, trascinato col
  mouse il vertice 20 → «vertice spostato» e la forma portata sulle due copie uguali (l'altro LIBB_FSS, BB FIC);
  Ctrl+Z la rimette; Esc spegne; LIBB_ES_CTR 52 maniglie, 36 col nome tratteggiate e ferme. Non visto: la finestra
  dei pannelli staccata (il mouse sulla Scheda dall'altra finestra), la Linea di un `.geo` a schermo.

## §6 — Fetta 2, fatta (2 novembre)

- **Maniglia di mezzo** (R3): fra due vertici di fila di un elenco che sa scrivere coordinate (non i punti di una
  procedura `.str` né il tracciato di un'aerovia: `ManigliaDelVertice.Aggiungibile`), una maniglia più piccola a metà;
  trascinata, `VerticeAggiuntoDallaMappa` fa il gesto `Aggiungi` della Scheda prima del secondo vertice, con la
  coordinata del rilascio. Una maniglia di mezzo vecchia (i due vertici non sono più di fila) si rifiuta. Mentre si
  trascina, le altre di mezzo si nascondono: starebbero a metà dei tratti vecchi.
- **La Linea di un `.geo`** sapeva aggiungere solo una copia del punto («+» della Scheda, da spostare poi): dalla mappa
  sarebbero stati due gesti, «aggiungi» e «sposta». Ora `LineeDelGeo.ConUnPuntoInPiu` e `GestoSullaLinea` del motore
  prendono il punto nuovo (`dove`, `nuovo`): un gesto solo, un annulla solo. Il «+» della Scheda resta una copia.
- **Clic destro** (R4): sulla maniglia un menu con «Togli il vertice N» (popup di Leaflet coi colori del Lab), che fa il
  gesto `Togli` della Scheda, col suo rifiuto sull'ultimo vertice. Anche un vertice col nome si toglie. Dopo, la Scheda
  segna il vertice che ha preso il suo posto.
- **Prove**: 5 test nuovi (`EditorSullaMappaTests`, 13), suite del Lab 1545. Sul banco, col mouse: LIBB_FSS 48 maniglie
  e 47 di mezzo; trascinata quella fra il 23 e il 24 → «vertice aggiunto (48 → 49)» e la forma portata sulle due copie
  uguali; clic destro sul vertice nuovo → «Togli il vertice 24» → la forma torna com'era e le Modifiche si svuotano.
  Non visto a schermo: la Linea di un `.geo` (solo nei test).
