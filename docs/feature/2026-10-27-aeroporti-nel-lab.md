# Gli aeroporti nel Lab (27 ottobre 2026)

Filone Sector Lab. Segue le carte [«L'AIP intero e gli emendamenti»](2026-10-21-aip-intero-e-emendamenti.md),
[«Lettura e consegna»](2026-10-22-lettura-e-consegna.md) e [«Le segnalazioni del 2611»](2026-10-23-segnalazioni-2611.md).

## §0 — La domanda

Il committente, dopo le prove A63-A67 (27 ottobre):

> «Io vorrei un visualizzatore funzionale e completo, com'è fatto ora è parziale e incompleto o a pezzi, soprattutto lato
> aeroporti (e so che è conseguenza di come è fatto l'AIP). ENR 2, 3, 4, 5 mi vanno benissimo come sono. Sono le
> procedure degli aeroporti che non mi convincono proprio, in particolare le IAP. E poi agli aeroporti manca la parte di
> ground, che mi sa dobbiamo fare con delle regole speciali e con un metodo misto tra sector file e PDF sovrapposto.»
>
> «Dobbiamo continuare con la revisione grafica, come abbiamo fatto per Lettura e consegna: ora anche per l'Indice.»

## §1 — Com'è oggi (misurato sul 2611 completo)

- **Le carte degli aeroporti sono immagini.** Nel PDF di una carta di avvicinamento (file 5) o di aerodromo (file 2) il
  testo è solo la testata: «AIP - Italia AD 2 LIRF 5 - 3 AIRAC effective date …», 19 parole. La vista in pianta, il
  profilo, le quote, il mancato: tutto disegnato. Dal testo il Lab non li ricaverà mai.
- **Le IAP.** AD 2.24 elenca 433 carte di avvicinamento in 54 scali (ILS/LOC 133, RNP/RNAV 179, VOR/NDB 121). Danno
  almeno una procedura solo 126: quasi tutte le pagine «DESCRIPTION TABLES» delle RNP (le tabelle di codifica) e le
  FIX/POINT di qualche ILS (RNP 83, ILS/LOC 30, VOR/NDB 13). LIRF ha 23 carte e ne legge 6, tutte RNP; LIRN 9 e 1;
  LICC 13 e nessuna.
- **Le IAP lette sono a pezzi.** Una procedura per segmento stampato (transizioni, finale, mancato: A30): 240 voci
  «Avvicinamenti», 93 col segmento, 47 senza tratti. Nell'Indice non si vede di quale carta sono, né quali carte mancano.
- **Il ground non c'è.** Il Lab legge AD 2.12 (piste: soglie, fine pista, dimensioni), AD 2.13 (distanze dichiarate),
  AD 2.18-2.19 (frequenze, radioassistenze, ILS) e il testo delle sottosezioni; le carte di aerodromo, dei parcheggi e
  dei movimenti al suolo sono immagini.
- **L'Indice.** AD 2 è un albero di 3 898 voci, scalo per scalo e genere per genere. Lo scalo è una voce fra le altre,
  con una scheda che è il testo di AD 2.1-2.25.

## §2 — Le quattro parti

### A · La pagina dello scalo

Lo scalo diventa una pagina, non una voce. Si apre dall'Indice (che in AD 2 elenca gli scali) o col clic sullo scalo
sulla mappa, e sta al posto della scheda. Ha in testa i dati dello scalo e sotto cinque schede:

- **Piste**: per pista il designatore, la rotta vera, le dimensioni, le soglie con le coordinate, le distanze dichiarate,
  l'ILS (categoria, frequenza, pendenza); per pista il numero delle sue procedure.
- **Procedure**: per pista (e «tutte le piste»), SID, STAR, transizioni, salite iniziali, avvicinamenti, con lo stato di
  ognuna: «letta, N tratti», «solo il punto finale», «con una nota». Una casella accende la procedura sulla mappa, una per
  gruppo accende tutte quelle della pista.
- **Carte**: le carte di AD 2.24 con la pagina e la data, e lo stato: **letta** (quante procedure ha dato), **solo
  immagine**; poi (parte C) **posata sulla mappa**. «Apri il PDF» su ogni carta.
- **Frequenze e radioassistenze**: AD 2.18 e AD 2.19.
- **Testo AD 2**: le sottosezioni AD 2.1-2.25 com'erano nella scheda.

In testa anche «Solo lo scalo sulla mappa»: le coste, lo scalo e le sue procedure accese (le voci una per una del 26
ottobre).

### B · Le IAP per carta

Un avvicinamento è una **carta**, come lo pensa chi lo vola: «ILS (Cat.II) or LOC X RWY 16L». Le voci di oggi (una per
segmento, A30) restano, ma diventano le **parti** della carta: transizioni, finale, mancato, FIX/POINT. La carta è nella
pagina dello scalo sotto «Procedure → Avvicinamenti» e sotto «Carte», con lo stato. Una carta solo immagine c'è lo
stesso, con «solo immagine: posala sulla mappa (C)».

Si lega alla carta di AD 2.24 la pagina della procedura (`Procedure.Page`) e, per le «DESCRIPTION TABLES», la carta
prima (stesso titolo senza «DESCRIPTION TABLES»).

### C · La carta posata sulla mappa

La pagina del PDF diventa un'immagine **posata sulla mappa**, al suo posto, sotto le forme: si guarda la carta vera
accanto all'AIP espanso e al sector.

- **L'immagine** si fa dal PDF col motore di Chrome, PDFium (licenza BSD), in un pacchetto .NET. Resta nei dati del Lab
  (una cache, si rifà): **mai** nel repo dell'AIP espanso (peso, copyright ENAV).
- **La posa** si fa con due o tre punti noti che il Lab ha già dal testo: le soglie (AD 2.12), le radioassistenze
  (AD 2.19, ENR 4.1), i punti con le coordinate (FIX/POINT, ENR 4.4). L'AOD clicca il punto sulla carta e sceglie quale è;
  con tre punti il Lab dice di quanti metri sbaglia la posa (da misurare in S3 su qualche carta: serve per guardare,
  non per prendere coordinate).
- **I punti della posa** sono dati: vanno nell'AIP espanso, `posa\<ICAO>.json` (riferimento della pagina, punto della
  carta in pixel, punto noto), così la posa resta da un ciclo all'altro finché la pagina non cambia data.
- **Dalla carta all'AIP espanso.** Sopra la carta posata si ridisegna quello che manca (il mancato di un ILS, un
  arco) e si salva come **Aggiunta**, con la fonte «carta AD 2 LIRF 5-3 (25/12/2025)»: il meccanismo delle correzioni c'è
  già, e una IAP si completa carta per carta con la sua fonte.

### D · Il ground

Il metodo misto: **sotto** la carta di aerodromo o dei parcheggi, posata sulle soglie delle piste (due piste, quattro
punti: il Lab dice l'errore); **sopra** il ground del sector (assi e bordi delle taxiway, terra, stand) con la carta
trasparente quanto si vuole. Le regole speciali, controlli che il Lab fa da sé dove il testo lo permette:

1. **Le soglie del sector contro AD 2.12**, in metri, pista per pista.
2. **I nomi delle taxiway** del sector contro quelli di AD 2.8.
3. **Gli stand**: dove AD 2 stampa le coordinate come testo, il sector contro l'AIP in metri; dove no, a occhio sulla
   carta posata. Da misurare: quali scali le stampano. LIRF no: la sua «PARKING DOCKING CHART DESCRIPTION TABLES»
   (AD 2 LIRF 2-9) è testo, ma sono note — gli stand col VDGS, quelli fuori dalla vista della torre, gli ingressi solo da
   una taxiway («Stands 401-405 entrance only via TWY NZ»). Anche queste sono regole che il Lab può leggere e mostrare.
4. Le regole di uno scalo che il PDF non dice (una taxiway che il sector disegna diversa per scelta) si scrivono come
   eccezione dello scalo, col perché: il controllo le salta e lo dice.

## §3 — Il disegno della pagina dello scalo

    ┌ Indice ───────────────┬ LIRF · ROMA Fiumicino ───────────────────────────────┬ Strati ┬ Mappa ─────────────┐
    │ Cerca una voce…       │ ARP 41°48'N 012°15'E · 15 ft · 3° E · IFR/VFR        │        │                    │
    │ ▸ ENR 2  Spazi…   330 │ ciclo 2611 · AD 2 LIRF 1-1 [Apri il PDF]             │        │                    │
    │ ▸ ENR 3  Rotte…   773 │ [Solo lo scalo sulla mappa]                          │        │                    │
    │ ▸ ENR 4  …      1 299 │ ──────────────────────────────────────────────────── │        │                    │
    │ ▸ ENR 5  …      7 067 │ Piste · Procedure · Carte 6/23 · Frequenze · Testo   │        │                    │
    │ ▾ AD 2  Aeroporti  107│ ──────────────────────────────────────────────────── │        │                    │
    │   LIBD  Bari       IAP 2/9  │ 16L/34R  3 902 × 60 m · ILS 16L III · ILS 34R  │        │                    │
    │ ▸ LICA  Lamezia    IAP 2/8  │   ☑ SID 16L 12 · ☐ STAR 16L 20 · ☐ IAP 16L 7   │        │                    │
    │ ▸ LIRF  Fiumicino  IAP 6/23 │   ILS (Cat.II) or LOC X RWY 16L  ▣ solo imm.  │        │                    │
    │ ▸ LIRN  Napoli     IAP 1/9  │   RNP RWY 16L        ● letta · 2 parti, 14 t. │        │                    │
    │   …                   │ 16C/34C  …                                           │        │                    │
    │ ▸ AD 3  Eliporti   156│ 07/25    …                                           │        │                    │
    └───────────────────────┴──────────────────────────────────────────────────────┴────────┴────────────────────┘

- A sinistra l'Indice resta per le parti ENR; in AD 2 e AD 3 una riga per scalo, col nome e quante carte IAP danno
  procedure su quante ce ne sono. Con una ricerca («ELKAP») si vedono anche le voci dentro gli scali, come oggi.
- Al centro la pagina dello scalo, più larga della scheda di oggi. Una voce scelta dalla pagina (una SID, una carta)
  apre la sua scheda sotto la testata dello scalo, con «‹ LIRF» per tornare alla pagina.
- La mappa e gli Strati come oggi.

## §4 — Le slice

1. **S1 — La pagina dello scalo** (A): l'Indice di AD 2 e AD 3 per scalo con la copertura delle IAP; la pagina con le
   cinque schede, coi dati che il modello ha già; «Solo lo scalo sulla mappa». Nessun cambio al lettore.
2. **S2 — Le IAP per carta** (B): le parti legate alla loro carta; la scheda di una carta con le parti, lo stato e
   «Apri il PDF».
3. **S3 — La carta posata** (C): PDFium, l'immagine nella cache, la posa con due o tre punti salvata in `posa\`, la
   trasparenza; per una carta di avvicinamento e per una di aerodromo.
4. **S4 — Ridisegnare sulla carta** (C): una procedura o un tratto tracciato sopra la carta, salvato come Aggiunta.
5. **S5 — Il ground** (D): la carta di aerodromo sotto il ground del sector, i controlli 1-4.

Ogni slice si prova a mano (pagina degli esiti) prima della dopo.

**S1 fatta il 27 ottobre.** `PagineDegliScali` (Core): la pagina dallo strato e dal modello — le piste accoppiate dalla
fine di un lato che è la soglia dell'altro (o dal numero, senza coordinate), le procedure per lato e per genere (una SID
di due piste sta sotto tutte e due, quelle senza pista in fondo), le carte di AD 2.24 con le procedure lette dalla loro
pagina, la copertura delle IAP per scalo. `PaginaDelloScaloVista.razor` al posto della scheda quando la voce scelta è uno
scalo; la scheda di prima (AD 2.1-2.25 e «Correggi») sta nella scheda «Testo AD 2». Nell'Indice, AD 2 e AD 3 una riga per
scalo («LIRF ROMA Fiumicino · IAP 6/23»); cercando, l'albero di prima. Una voce di uno scalo ha «‹ LIRF» sopra la scheda.
Provato sul banco a 1400 px sul 2611: LIRF 3 piste, 159 procedure, IAP 6/23; «Solo lo scalo sulla mappa»; il ritorno.

**S2 fatta il 27 ottobre.** `PagineDegliScali.CarteDi` (Core) lega le procedure di avvicinamento alle carte:
- **Le pagine.** AD 2.24 dà solo la prima pagina di una carta; la procedura sta di solito sulla seconda (LIRN «ILS Z RWY
  24»: carta 5-3, procedura 5-4; LIRF «RNP RWY 16L DESCRIPTION TABLES» 5-9, blocco SBAS 5-10). Una carta occupa quindi le
  pagine dalla sua fino a quella prima della carta seguente dello stesso file. Una procedura sulle pagine di una carta che
  non è di avvicinamento (la VAC) resta fuori.
- **Le DESCRIPTION TABLES** si legano alla carta prima col titolo uguale senza il suffisso, togliendo i difetti di stampa
  del 2611: il trattino morbido («ILS OR LOC­Z», U+00AD) e il «.pdf» in coda (LIBC, LICA). Senza la loro carta sarebbero
  una carta.
- **Le parti** si chiamano col segmento stampato («RNAV to ILS TRANSITION - APPROACH FROM BENTO»), «FIX/POINT» e «SBAS FAS
  DATA BLOCK» per le tabelle senza tratti, altrimenti col designatore («RNP RWY16L via SUVOK», «RNP Z RWY24 (2)»).
- **Lo stato**: *letta* se c'è almeno una parte coi tratti, ognuna ha i tratti o è una tabella, e i punti si trovano tutti;
  *in parte* se ci sono solo tabelle («in parte · solo FIX/POINT»), una parte senza tratti o con un punto solo, o un punto
  che non si trova; *solo immagine* senza parti.
- **Le piste** dal titolo della carta («RWY 24»), più quelle delle parti: i LOC di LIRN, che nella procedura non hanno la
  pista, stanno sotto la RWY 24.

Nella pagina dello scalo, scheda Procedure, il gruppo Avvicinamenti di ogni lato elenca le carte con lo stato (e una
casella per accenderne le parti), poi quelle fuori dalle carte. Il clic su una carta la apre al posto delle schede: pagine
e date (il clic apre il PDF), piste, «Accendi/Spegni le parti sulla mappa», le parti con la loro casella; da una parte, «‹
LIRN» riporta alla carta (la scheda e la carta aperte stanno nella sessione, `SchedaDellaPaginaDelloScalo`). Nella scheda
Carte una carta di avvicinamento dice il suo stato, le sue tabelle «tabelle di AD 2 LIRF 5-7»; tutte e due aprono la carta.
Misurato sul 2611: 302 carte di avvicinamento, **141 con parti**; 131 DESCRIPTION TABLES, tutte legate; 5 avvicinamenti su
240 fuori dalle carte (LICR RNP G RWY33 a 5-17, carta che AD 2.24 non elenca; LIMG, «break clouds»). LIRN 7/7, LIRF 6/17,
LIMC 10/15, LICC 4/13. Provato sul banco a 1400 px: LIRN, la carta ILS Z RWY 24 con le sue cinque parti, andata e ritorno
da una parte; LIRF, le tabelle 5-9 che aprono la carta 5-7.

## §5 — Deciso e da decidere

- **D1** (committente, 27 ottobre): si scrive questa carta col disegno, e si parte dalla pagina dello scalo (S1).
- **D2** (proposta, fatta in S2): le voci di oggi per segmento restano e diventano parti della carta: la A30 cambia così.
  Sulla mappa e nell'Indice le voci sono quelle di prima; la carta le raccoglie nella pagina dello scalo.
- 🟡 **Da decidere** (S3): PDFium in un pacchetto .NET (l'immagine si fa nel Lab, funziona anche senza rete), oppure
  pdf.js nella pagina (niente di nativo, ma un pezzo di JavaScript grande). Proposta: PDFium.
- 🟡 **Da decidere** (S3): i punti della posa nel repo dell'AIP espanso (proposta: sì, sono dati e servono a tutti),
  le immagini mai.
