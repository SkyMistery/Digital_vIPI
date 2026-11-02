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

## §7 — Fetta 3, fatta (2 novembre)

- **La calamita** (R5) in due metà. Nella pagina (`sectorlab-mappa.js`): all'inizio del trascinamento si raccolgono i
  punti delle forme che sono sulla mappa adesso (strati accesi, vista, voci spente già tolte), dentro la vista allargata,
  tranne il record stesso — un fix o un VOR è un punto, di una linea o di un'area ogni vertice — in una griglia di celle
  da 16 px sul piano dei layer di Leaflet (non cambia quando la mappa scorre); a ogni movimento il più vicino entro 8 px
  (a pari distanza un punto vince sul vertice di una forma) ha un anello, e i tratti tratteggiati vanno lì. `Alt` tenuto
  la stacca. Nel Lab (`AgganciaA`): la pagina manda solo il record agganciato, e la coordinata si prende dalla forma
  **esatta** (`FormaDellaMappa`, quella del file), non da quella arrotondata che la mappa ha disegnato: così due forme che
  si toccano hanno la stessa riga. Anche la maniglia di mezzo si aggancia.
- **Il vertice col nome** (R6, D1): ora si trascina, ma lo sposta solo la calamita su un **punto** (non un vertice di
  un'altra forma), e diventa il nome di quel punto se il catalogo del master scelto lo risolve proprio lì; lasciato
  altrove torna dov'era e la Scheda dice perché. Lo stesso per gli elenchi fatti solo di nomi (punti di una procedura
  `.str`, tracciato di un'aerovia). Un vertice per coordinate agganciato a un fix resta una coordinata (R5: ne prende le
  coordinate).
- **Prove**: 6 test nuovi (19), suite del Lab 1551; i due della coordinata esatta falliscono se si scrive il punto del
  rilascio. Sul banco, col mouse: LIBB_FSS, il vertice 20 lasciato a 3 px dalla maniglia del 26 → prende
  `N039.17.36.000 E019.00.00.000`, quella del 26, dalla copia gemella (il record stesso non si aggancia); LIBB_ES_CTR,
  AMSOR lasciato nel vuoto → torna, con «lascialo su un altro punto col nome…»; con lo strato Punti acceso, lasciato fra
  LUNAR e VEGAN → diventa `VEGAN` (il più vicino), portato anche su LIBB_MIL_CTR. Non provato col mouse: `Alt` (il banco
  non tiene un tasto durante il trascinamento).

## §8 — Fetta 4, fatta (2 novembre): dopo un gesto, il solo file (R9)

- **Misurato prima** (banco, sector vero, «Disegni .geo», Punti e Aeroporti a terra accesi: 4 strati, 17 776 forme): un
  vertice di `liap.geo` cambiato → la mappa aggiornata **504 ms** dopo il gesto, di cui 195 ms per riprendere lo strato
  dei `.geo` (8832 forme) e 77 ms quello della terra (4501), dove il gesto ha portato la copia nel `.pol`. Più della
  metà: valeva la fetta.
- **Il Lab** ricorda per strato quali file `RifaiLaGeometria` ha rifatto e a che versione (`FileRifattiDa`, gli ultimi 64);
  quando uno strato cambia tutto (salvataggio, master cambiato, AIP riletto, un sector aperto) lo segna
  (`StratoTuttoCambiato`) e chi aveva disegnato prima lo riprende intero. All'apertura di un sector le versioni prima non
  si alzavano: ora sì, perché i file rifatti della sessione di prima non valgono per quella nuova.
- **La mappa**: `/mappa/strato/{id}?file=` dà le sole forme di quel file; `rifaiIlFile` (JS) le mette al posto delle sue
  di prima nello stesso punto dell'elenco (l'ordine dei `.pol` conta), con spente, ordine della terra, evidenza e vista
  rifatti come dopo uno strato intero. `Mappa.razor` lo fa fino a 8 file cambiati dall'ultimo disegno; oltre, o se il
  JavaScript non può (strato in arrivo, ripreso intero nel frattempo), riprende lo strato intero come prima.
- **Misurato dopo**, stesso gesto: la mappa aggiornata **157 ms** dopo il gesto («liap.geo: 72 forme in 12 ms», «ap_ad_gnd.pol:
  13 forme in 11 ms»); annulla 144 ms. La forma scelta resta evidenziata.
- **Prove**: 3 test (22), suite del Lab 1554. Non visto: un lotto di più di 8 file (va per lo strato intero, come prima).

**Con la fetta 4 la carta è fatta.** Restano, se il committente le vuole, D3 (disegnare una forma nuova a clic) e D4
(trascinare i record di un punto solo: fix, VOR, VFR coi gemelli), e la saldatura dei bordi della F8.

## §9 — D3: disegnare una forma a clic (2 novembre; approvata come proposta: D5 sì, D6 al posto, D7 sì)

Il committente ha scelto D3. **Com'è oggi**: un record nuovo nasce sempre come copia di un modello («+ Record come
questo» copia il record scelto, «+ Nuovo record» il vicino, slice 8), coi vertici del modello; per dargli la sua forma
c'è «Incolla da testo» (un gesto solo che mette i vertici al posto di tutti, con «Chiudi la forma»), o le maniglie.
Quello che manca è dare i vertici **cliccandoli** sulla mappa.

- **R10 — «✎ Disegna sulla mappa», per elenco.** Accanto a «Incolla da testo» di ogni elenco della Geometria (anche la
  Linea di un `.geo`). Acceso, il puntatore della mappa è una croce; ogni clic mette un punto, con la calamita di R5
  (anello, `Alt` la stacca); la forma che esce si vede tratteggiata, come l'anteprima di «Incolla». Le maniglie del
  record, se accese, si nascondono finché si disegna.
- **R11 — Un gesto solo, alla fine.** **Invio** o **doppio clic** finisce: i punti cliccati diventano i vertici
  dell'elenco al posto di quelli di prima, col gesto «vertici incollati» che c'è già (un annulla, copie uguali, diff).
  **Backspace** toglie l'ultimo punto; **Esc** lascia stare e non cambia niente. Servono almeno 2 punti (3 per una
  forma chiusa).
- **R12 — Chiusa o aperta** come «Incolla»: la casella «Chiudi la forma», che parte da com'è l'elenco di oggi.
- **R13 — Un record del tutto nuovo** si fa come oggi: «+ Record come questo» (o «+ Nuovo record»), poi «✎ Disegna
  sulla mappa» sul suo elenco. Niente modo nuovo di creare record.

**Domande**
- **D5** Un punto agganciato a un fix in un elenco che sa scrivere i nomi (`.tfl`, `.sid`, `.vrt`): si scrive il
  **nome** del fix (proposta: sì, come fa l'AOD a mano nei settori; in un elenco solo di coordinate resta coordinata), o
  sempre la coordinata?
- **D6** Il disegno prende il posto dei vertici di prima (proposta, come «Incolla»), o serve anche «continua»: i punti
  cliccati vanno **in coda** a quelli che ci sono (una linea da allungare)?
- **D7** Negli elenchi fatti solo di nomi (punti di una procedura `.str`, tracciato di un'aerovia) si disegna cliccando
  solo sui punti col nome (proposta: sì, gli altri clic non mettono niente e lo dicono), o lì il disegno non c'è?

## §10 — D3, fatto (2 novembre)

- **Motore** (`ModificheInSospeso.DisegnaIVertici`, del solo Lab): i punti al posto di tutti i vertici, ognuno letto come
  un campo della Scheda (`LeggiIlPunto`: coordinata, o nome dove l'elenco lo sa scrivere) — così D5 e D7 vengono dalla
  stessa regola dei campi: un elenco di soli nomi rifiuta una coordinata, dicendo quale punto. Chiusa: il primo ripetuto
  in fondo. Almeno 2 punti, 3 se chiusa.
- **Servizio**: il gesto `Disegna` nel giro dei gesti sui vertici (storia, copie uguali, chiusura fissata al gesto);
  `DisegnaSullaMappa` / `ChiudiIlDisegno` / `LasciaIlDisegno` / `FormaDisegnata` (`SessioneDelLab.Maniglie.cs`). Un punto
  agganciato a un punto col nome, in un elenco che sa scrivere i nomi, diventa il nome (D5); gli altri la coordinata esatta
  del punto agganciato, o del clic. Rifiutato, il disegno resta aperto coi suoi punti. La Linea di un `.geo` si rifà col
  gesto «incolla» della linea (solo coordinate). Mentre si disegna le maniglie si tolgono; un altro record lascia il disegno.
- **Mappa**: `disegna` (JS) — croce, clic anche sopra una forma (non la sceglie), calamita con l'anello (`Alt` la stacca;
  in un elenco di soli nomi solo i punti, e un clic altrove lo dice in un riquadrino), anteprima tratteggiata con
  l'elastico fino al puntatore, Invio o doppio clic (lo zoom del doppio clic si spegne; il suo secondo clic non è un punto),
  Backspace, Esc.
- **Scheda**: «✎ Disegna sulla mappa» sotto «Incolla da testo»; acceso, un riquadro con le istruzioni, «Chiudi la forma»
  e «Lascia stare».
- **Prove**: 7 test (29), suite del Lab 1561. Sul banco, col mouse, LIBB_FSS: 4 clic e Invio → «forma disegnata (48 → 5
  vertici)», chiusa, portata sulle due copie uguali; due punti agganciati a vertici vicini (coordinate tonde); Ctrl+Z la
  rimette. Backspace ed Esc: niente cambiato. 3 clic con l'ultimo doppio → 4 vertici, niente zoom. Non visti a schermo: un
  elenco di soli nomi e la Linea di un `.geo` (nei test).

## §11 — D4: trascinare i punti (2 novembre, da approvare)

Il committente ha scelto D4. **Com'è oggi**: un fix, un VOR, un NDB o un punto VFR si sposta cambiando il campo
«Posizione» nella Scheda (`CambiaCampo`): il gesto va nella storia e porta il cambio sulle copie gemelle (il punto del
`.vfi` e il suo fix nascosto in `VFR_NASCOSTI.fix`, F3-bis e slice 8e). Ma dopo lo spostamento si ridisegna solo il file
del punto. **Trovato guardando**: i settori, le procedure e le aerovie che lo citano **per nome** si risolvono nel catalogo
dei punti del master, che si rifà solo dopo una rinomina, un annulla di testo o un salvataggio. Quindi oggi, spostato un
fix dalla Scheda, chi lo cita resta disegnato dov'era finché non si salva, e la calamita non riconosce più il suo nome.

- **R14 — La maniglia del punto.** Con «✎ Modifica sulla mappa» acceso su un fix, un VOR, un NDB o un punto VFR, una
  maniglia sola sul punto. Trascinata e lasciata, fa il gesto di oggi della Scheda (`CambiaCampo` della Posizione): storia,
  annulla, gemelli. Con la calamita (R5: la coordinata esatta di un altro punto o vertice; `Alt` la stacca).
- **R15 — Chi lo cita lo segue, anche dalla Scheda.** Dopo lo spostamento di un punto (dalla mappa o dal campo della
  Scheda, e dopo annulla e ripeti) il Lab rifà il catalogo dei punti e ridisegna i file che lo citano per nome («Chi lo
  usa»): sulla mappa i settori e le procedure si piegano subito verso il punto nuovo. Il ridisegno per file della fetta 4
  lo tiene svelto (oltre 8 file, lo strato intero).
- **R16 — Si vede chi lo cita.** La maniglia dice, al passaggio del mouse, «citato da N record»; la voce nelle Modifiche
  lo dice dopo lo spostamento.

**Domande**
- **D8** Un punto citato da altri record: si sposta senza chiedere (proposta: sì, un annulla lo rimette, e R16 lo dice)
  o chiede conferma quando lo citano?
- **D9** Quali punti: fix, VOR, NDB e punti VFR coi gemelli (proposta), o anche l'ARP degli scali (`.ap`) e gli altri
  record di un punto solo? Proposta: questi quattro adesso, gli altri dopo.
