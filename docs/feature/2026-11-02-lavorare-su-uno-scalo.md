# Lavorare su uno scalo o su un settore (2 novembre 2026)

Filone Sector Lab. **Da approvare** (proposta, §3; le scelte del committente in §4).

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
