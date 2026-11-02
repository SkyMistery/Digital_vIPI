# Lavorare su uno scalo o su un settore (2 novembre 2026)

Filone Sector Lab. **Approvata** il 2 novembre («Sì, vai»: R1-R8 e le scelte proposte in §4, D1-D4). Le fette fatte in §6.

## §0 — La domanda

Il committente, 2 novembre: «non riesco a lavorare bene se lavoro con singolo settore o singolo aeroporto, tipo per fare
l'editing a terra». Lo spazio **Sector** è fatto per l'albero intero: ci si muove per file e per record, e la mappa mostra
gli strati di tutta l'Italia. Chi ritocca il ground di uno scalo, o un settore, lavora invece su un pezzo solo, per
un'ora, e il resto gli sta in mezzo.

## §1 — Com'è oggi (guardato sul banco col sector vero, 2 novembre, `lab/f3` @ `84ff5707`)

Il giro per ritoccare il ground di LIRF:

1. **Scali** (linguetta della colonna di sinistra) → LIRF: l'elenco di quel che lo riguarda (2 righe dell'`.ap`, 6 piste,
   22 posizioni, 5 file dello scalo, `lirf.geo` 5 297 record e `rf_ad_gnd.pol`, le marcature per pista). **La mappa non si
   muove**: resta sull'Italia intera, e non si accende niente.
2. Clic su `lirf.geo`: la colonna **salta a Sfoglia**, nella cartella GEO coi 99 `.geo` d'Italia. LIRF non è più scelto da
   nessuna parte; per tornare all'elenco dello scalo si rifà il punto 1.
3. Clic su un record (una marcatura della 16L): la mappa **si stringe sul solo segmento**, e accende «Disegni .geo» con
   tutti i suoi generi, cioè **8 832 forme di tutta Italia**. Per vedere il piazzale intorno si torna indietro con la
   rotella a ogni record.
4. Il clic sulla mappa prende quel che sta sotto, di qualunque file: anche la costa o un disegno di un altro scalo.

Lo spazio sullo schermo, a 1 400 px: albero 266 px, Scheda circa 300, Strati 238; alla mappa ne restano **circa 590**, e la
pagina è larga 1 585 (si scorre di lato: la barra in alto non ci sta). Il ground si ritocca in un rettangolo di 590 px.

Per un **settore** non c'è niente del genere: un settore di LIRR sta in più file (`lirr_nc_ctr.tfl` in DYNAMIC_SEC, i
`.lartcc`/`.hartcc`, le posizioni in `itfreq.frq`), e si trovano solo con la ricerca, uno alla volta.

Cosa c'è già e serve: la **vista** («In vista», «Solo questo sulla mappa», «Solo questo file sulla mappa»: la mappa mostra
solo quei record o quei file, più le coste), l'elenco dello **scalo** (`VistaDelloScalo`), gli **Strati sopra la mappa**
dietro un tasto (R1 dell'Indice rifatto, 1 novembre), l'**editor sulla mappa** (F9) per i gesti.

## §2 — Che cosa manca, in una riga

Un modo di dire al Lab **«adesso lavoro su LIRF»** (o su un settore) che valga per tutto finché non si esce: mappa,
colonna, strati e clic.

## §3 — La proposta: il fuoco

Regole, come per le carte di prima (R1…):

- **R1 — «Lavora su…».** Nella linguetta Scali, scelto lo scalo, un tasto **«Lavora su LIRF»**. Si accende il **fuoco**: in
  testa allo spazio Sector una striscia «◎ LIRF FIUMICINO · esci». Il fuoco resta finché non si esce, anche passando a
  Testo o Consegna e dopo un riavvio del Lab.
- **R2 — La mappa sullo scalo.** Col fuoco la mappa va sullo scalo intero (i suoi disegni a terra, le piste, l'ATZ) e
  mostra **solo** i file dello scalo: `lirf.geo`, `rf_ad_gnd.pol`, `lirf.gts`, `lirf.txi`, le sue righe di `.ap` e `.rw`,
  più le coste. È la «vista» di oggi riempita da sola. Gli Strati contano le forme dello scalo («Edifici 42», non 882).
- **R3 — La colonna resta sullo scalo.** La colonna di sinistra mostra l'elenco dello scalo, e aprire `lirf.geo` apre i
  suoi record **lì**, sotto lo scalo, per genere (Edifici, Piazzali, Assi delle taxiway… come la selezione di Aurora), non
  in Sfoglia fra i 99 `.geo`. Sfoglia e Problemi restano nelle loro linguette.
- **R4 — Scegliere non sposta la mappa.** Col fuoco, un clic su un record dell'elenco lo evidenzia sulla mappa senza
  zoomare; si zooma solo se il record è fuori vista, o col tasto «⌖ Portami lì». Niente strati d'Italia accesi da soli.
- **R5 — Il clic sulla mappa prende solo lo scalo.** Sulla mappa si scelgono solo forme dei file del fuoco; la costa e gli
  scali vicini si vedono, non si prendono.
- **R6 — Più mappa.** Col fuoco gli Strati vanno sopra la mappa, dietro il tasto «Strati ▾», come nell'Indice: alla mappa
  vanno i loro 238 px (a 1 400 px da circa 590 a circa 830). La Scheda si può stringere a una striscia («‹ Scheda»), come
  l'Indice con una voce aperta.
- **R7 — Il settore.** Lo stesso per un settore: scelto per nome (es. «LIRR NC CTR»), il fuoco prende i suoi file
  (`.tfl`, i `.lartcc`/`.hartcc` col suo nome, le sue posizioni) e la mappa va sul suo poligono, coi bordi dei settori
  accanto attenuati, per vedere dove si toccano.
- **R8 — Le modifiche dello scalo.** In Consegna, col fuoco, in testa «N modifiche di LIRF» (le altre restano sotto);
  «Annulla tutto» chiede se solo quelle dello scalo o tutte.

Fuori dal fuoco il Lab resta com'è oggi.

## §4 — Le scelte del committente

| # | Domanda | Proposta |
|---|---|---|
| D1 | Il resto del sector col fuoco: **nascosto** o **attenuato** (grigio, non si prende)? | Nascosto, con una casella «mostra il resto attenuato» |
| D2 | Che cos'è «un settore» per il fuoco: una **posizione** ATC (LIRR_NC_CTR, coi suoi file), o un **file** (`lirr_nc_ctr.tfl`)? | La posizione: è come si pensa al lavoro |
| D3 | Prima lo scalo (R1-R6, R8) e poi il settore (R7), o insieme? | Prima lo scalo: è il caso del ground, e R7 riusa tutto |
| D4 | R6: la Scheda che si stringe, sì o no? | Sì, ma come seconda fetta, dopo aver provato R1-R5 |

## §5 — Le fette (se approvata)

1. R1 + R2: il fuoco e la mappa sullo scalo (riusa la vista).
2. R3 + R4 + R5: la colonna che resta, la scelta senza zoom, il clic solo sullo scalo.
3. R6: gli Strati sopra la mappa col fuoco (e, se D4, la Scheda a striscia).
4. R8: le modifiche dello scalo in Consegna.
5. R7: il settore.

Ogni fetta con i suoi test (bUnit e motore), provata sul banco col sector vero, e le sue prove a mano in `PROVE.md`.

## §6 — Fatto

**Fetta 1, R1 + R2 + D1** (2 novembre). Nella linguetta Scali, scelto lo scalo, «◎ Lavora su LIRF». Il fuoco sta in
`SessioneDelLab.Fuoco.cs`: le sue chiavi sono le righe dello scalo in `.ap` e `.rw` (`file#3`) e i suoi file e disegni a
terra interi (`file#*`), presi dalla vista per scalo; le posizioni no (il `.frq` non si disegna). La mappa del Sector
mostra la vista a mano se c'è, se no il fuoco (`VistaDaMostrare`): dentro il fuoco «Solo questo sulla mappa» stringe
ancora, e «torna allo scalo» (era «mostra tutto») svuota la vista a mano. In testa alla colonna di sinistra la striscia
«◎ LIRF FIUMICINO · ⌖ Lo scalo · esci» e la casella «il resto del sector in grigio» (D1): in grigio le forme fuori dal
fuoco restano sulla mappa, grigie, al 30%, senza simbolo e senza clic (`options.interactive`, che sulla tela vale anche per
il suggerimento). Il fuoco si ricorda in `fuoco.txt` dei dati del Lab e torna all'apertura del sector, se lo scalo c'è.
Gli Strati e i loro generi contano le forme dello scalo (le coste restano intere).

La mappa va sul riquadro del **ground** dello scalo (disegni, aeroporti a terra, piste), non di tutte le sue forme: al
primo giro sul banco ci entravano SID, STAR e rotte VFR di LIRF, e lo scalo diventava un puntino. Solo attenuate, le
forme in grigio tenevano i loro colori: le SID di tutta Italia restavano un groviglio verde; ora sono grigie.

Sul banco col sector vero (1 400 px): LIRF con la mappa sul sedime, «Disegni .geo 522» (Moli 149, Edifici 50…), «Aeroporti
a terra 325»; riavviato il banco il fuoco è tornato; in grigio Ciampino e gli altri scali grigi e tenui, un clic lì non
sceglie niente; un clic su una taxiway di LIRF la sceglie e la mappa resta ferma. Si vede ancora il difetto di R3: scelta
la taxiway, la colonna salta a Sfoglia, nella cartella GEO di tutta Italia (fetta 2). Test: `FuocoTests` (9), Lab 1 575.

**Fetta 2, R3 + R4 (+ R5)** (2 novembre). Col fuoco la colonna parte dalla linguetta Scali, e un file dello scalo si apre
lì, sotto la sua voce, coi suoi record (lo stesso `RecordDelFile` di Sfoglia: voci, filtro, «Solo questo file sulla
mappa»); un secondo clic lo chiude. Una scelta nuova in un file dello scalo, dalla mappa o dalla ricerca, riporta la
colonna sullo scalo col file aperto; chi passa a Sfoglia da sé ci resta fino alla scelta dopo, e una scelta fuori dallo
scalo non la muove. R4: col fuoco scegliere un record non sposta la mappa; si muove solo se il record è fuori vista (ci si
porta senza cambiare zoom, se ci sta) — e nella Scheda c'è «⌖ Portami lì», che lo inquadra per intero. R5 era già vero
con la fetta 1: fuori dal fuoco le forme sono nascoste o grigie senza clic.

Trovato sul banco: l'elenco di una sezione dello scalo è alto al più 224 px e scorre da sé; il file aperto ci restava
chiuso dentro (i 200 record di `lirf.geo` non si vedevano). Con un file aperto la sezione perde il tetto (`:has`), e
scorre la colonna. E due volte su sei, ricaricata la pagina, la mappa finiva sull'Italia: l'unica inquadratura
sull'Italia è quella delle coste quando arrivano, la prima volta, su una mappa nuova. Ora il riquadro del fuoco si ricorda
per mappa nel JavaScript, e le coste che arrivano ci portano lì; uscendo dal fuoco si scorda. Dopo, in quattro
ricariche, sempre su LIRF.

Da sapere: un record di `lirf.geo` che sulla mappa non ha una forma sua (il record 41, un pezzo di AD_BOUNDARY) non si
inquadra con «⌖ Portami lì», come fuori dal fuoco non si inquadrava scegliendolo. Sul banco: `lirf.geo` aperto sotto LIRF,
il record 41 scelto con la mappa ferma; «Runway 16L designator» dall'elenco con la mappa ferma, poi «⌖ Portami lì» sulla
sola designazione. Test: `FuocoTests` (12), Lab 1 578.

**Fetta 3, R6** (2 novembre). Col fuoco, nella pagina Sector, gli Strati stanno sopra la mappa dietro «Strati ▾», come
nell'Indice dell'AIP (stessa regola di stile): sul banco a 1 400 px la mappa passa da circa 590 a **822 px**. La Scheda a
striscia (D4) aspetta la prova del committente sulle fette 1-3, come deciso.

**Fetta 4, R8** (2 novembre). In Consegna, col fuoco, in testa «Di LIRF: N modifiche in K file (qui sotto per primi)», e
i file dello scalo sono i primi dell'elenco. Se ci sono anche modifiche fuori dallo scalo, accanto ad «Annulla tutto»
c'è «Annulla quelle di LIRF»: un gesto solo, che Ctrl+Z rimette. Due tasti, non una domanda (un `confirm()` del browser
nel Lab no). Le modifiche dello scalo sono quelle nei **suoi file** (i suoi file e i disegni a terra): le sue righe
dentro l'`.ap` e il `.rw` restano con le altre, perché annullare quei file interi toccherebbe anche gli altri scali.

**Il salto sull'Italia, trovato davvero.** Dopo la fetta 2 la mappa tornava ancora sull'Italia, a volte da sola e a volte
al primo clic, e una sonda sui movimenti della mappa non vedeva nessun `setView`. Il motivo è in Leaflet: uno zoom
animato si mette in coda al fotogramma dopo (`requestAnimationFrame`), mentre un salto di più di 4 livelli si fa subito,
senza animare. Le coste partivano animate verso l'Italia (zoom 6 → 4), LIRF (zoom 13) si metteva subito, e il fotogramma
in coda riportava la mappa sull'Italia; nel riquadro del browser, che rallenta i fotogrammi, quasi sempre. Ora
l'inquadratura di apertura e quella del fuoco sono senza animazione (`animate: false`), e prima di inquadrare si chiude
un'animazione già partita. Dopo, sul banco, tre giri su tre (carica, cambia misura, clic) sempre su LIRF. Il riquadro del
fuoco ricordato per mappa (fetta 2) resta: non era la causa, ma non costa niente.

Sul banco: «Di LIRF: 1 modifica in 1 file», `lirf.geo` primo, «Annulla tutto» ha tolto la modifica (non salvata). Test:
`FuocoTests` (14), Lab 1 580.

**Resta** (§5): R7, il settore — dopo la prova del committente sullo scalo (D3) — e la Scheda a striscia (D4).
