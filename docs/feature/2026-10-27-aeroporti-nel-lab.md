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
   carta posata. Misurato il 30 ottobre sul 2611 (la prima misura, la mattina, diceva «nessuno»: non riconosceva il
   simbolo dei gradi): le sottosezioni di AD 2 non le stampano; le carte dei parcheggi di **LIPE, LIEE, LIPY e LIRP**
   stampano sul disegno la tabella dei punti d'arresto degli stand («110-stop2 44° 31' 47.16" 011° 17' 44.29"»), LIBP e
   LIRI pochi punti. Il confronto in metri col sector si può fare lì; altrove resta a occhio. LIRF no: la sua «PARKING DOCKING CHART DESCRIPTION TABLES»
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
Nella scheda Piste la riga «Procedure» di un lato conta le carte del lato, «IAP 1/3» (con parti su tutte): le prove sul
banco (A70) hanno trovato che dopo S2 non contava più gli avvicinamenti, che non stanno più nei gruppi.
Misurato sul 2611: 302 carte di avvicinamento, **141 con parti**; 131 DESCRIPTION TABLES, tutte legate; 5 avvicinamenti su
240 fuori dalle carte (LICR RNP G RWY33 a 5-17, carta che AD 2.24 non elenca; LIMG, «break clouds»). LIRN 7/7, LIRF 6/17,
LIMC 10/15, LICC 4/13. Provato sul banco a 1400 px: LIRN, la carta ILS Z RWY 24 con le sue cinque parti, andata e ritorno
da una parte; LIRF, le tabelle 5-9 che aprono la carta 5-7.

**S3 fatta il 30 ottobre.** La carta posata, per una carta di avvicinamento e per una qualsiasi di AD 2.24 (la carta
d'aerodromo, i parcheggi):
- **L'immagine.** `Pdfium` (Core) disegna una pagina su fondo bianco, con le annotazioni, a 200 dpi (un A4 è 1 653 × 2 339
  pixel; il lato più lungo al massimo 6 000). I binari sono quelli di `bblanchon.PDFium` (D3), il collegamento è nostro: una
  quindicina di funzioni, il PDF aperto dalla memoria (PDFium apre i percorsi con `fopen`, e un percorso accentato su
  Windows non si aprirebbe), un lucchetto solo perché PDFium non regge due fili. `Png` scrive l'immagine (zlib, filtro Sub
  per riga): il Lab non ha una libreria di immagini e non gliene serve una. L'immagine sta nella cache del Lab,
  `<dati>\cache\carte\<ciclo>\<file>-<posizione>-200.png`; la carta ILS Z RWY 24 di LIRN si fa in circa due secondi la prima
  volta, poi si legge dalla cache.
- **La posa.** Un punto è un clic sulla carta (in punti PDF dall'angolo in alto a sinistra) e il suo posto sulla Terra: un clic
  sulla mappa — su un punto dell'AIP la mappa manda le sue coordinate e il nome, così il punto si aggancia al fix —, oppure il
  nome scritto: un punto, una radioassistenza o uno scalo dell'AIP espanso, «RWY 24» (la soglia), «ARP», una radioassistenza
  di AD 2.19, e se no il punto del sector. `PosaSullaMappa` fa i conti sul piano di Mercatore, quello della mappa: con due
  punti una similitudine (scala, rotazione, spostamento; la pagina ha la y in giù), con tre una affine, dal quarto ai minimi
  quadrati con lo scarto più grande in miglia. Una carta d'avvicinamento è conforme, e su qualche decina di miglia differisce
  da Mercatore di poco: tre punti la mettono giù. Il pannello dice anche «1 cm della carta = 2,7 NM», che è la scala
  1:500 000 stampata: un colpo d'occhio sui punti sbagliati.
- **Sulla mappa.** L'immagine intera in un riquadro di Leaflet sotto le forme (z 350, sotto overlayPane), portata con una
  matrice CSS sui tre angoli della pagina: sul piano di Mercatore la posa è affine e i punti di livello di Leaflet sono quel
  piano in scala, quindi la matrice è esatta. Si rifà a ogni zoom e si nasconde durante l'animazione (`leaflet-zoom-hide`).
  L'opacità si sceglie (60% di base). Mentre un punto aspetta il suo posto il puntatore della mappa è una croce e il clic su
  una forma dà il posto invece di sceglierla.
- **Il file** (D4): `posa\<ICAO>.json` nel repo dell'AIP espanso, una posa per pagina («AD 2 LIRN 5-3»), col titolo e la data
  della pagina, i punti a sei decimali e in ordine di pagina: cambia solo dove cambia la posa. Si registra con «Registra
  l'AIP» come una correzione. Se la pagina ha poi un'altra data, il pannello lo dice: la carta è cambiata e i punti vanno
  guardati.
- **Dove.** Nella carta di avvicinamento aperta, sotto le parti, il riquadro «Sulla mappa»; nella scheda Carte una colonna
  «Sulla mappa» con «posa…» o «posata» per ogni carta (le DESCRIPTION TABLES no: sono tabelle della loro carta), che apre la
  carta di avvicinamento o, per le altre, la carta da sola col suo riquadro. Una posa aperta alla volta; chiusa, la carta
  lascia la mappa. Un altro ciclo sulla mappa chiude la posa.
Provato sul banco a 1100 px sul 2611: LIRN ILS Z RWY 24 posata con ESCOB, BENTO e URRUC scritti per nome; i fix dell'AIP
cadono sui simboli della carta (anche DIEZO, OLEVA, ISKIA e il mancato, che non erano punti della posa), e la scala torna.

**S4 fatta il 30 ottobre, notte.** Ridisegnare sulla carta posata quel che il Lab non ha, e salvarlo come correzione:
- **Dove.** Nella carta di avvicinamento aperta, sotto «Sulla mappa», il riquadro **«Ridisegna sulla carta»**, solo con la
  carta posata (almeno due punti). Si sceglie **una parte nuova** o **in coda a** una parte coi tratti (il mancato che la
  tabella non ha); per una carta solo immagine c'è solo la parte nuova. Una procedura che non è di una carta di
  avvicinamento (una SID) resta fuori: le carte delle SID non sono nella pagina dello scalo come carte con parti.
- **I punti.** Un clic sulla mappa, sopra la carta: su un punto dell'AIP il punto è quel fix (il nome dell'etichetta, «NAP»
  di «NAP VOR/DME»), altrove ha le coordinate del clic e la definizione **«dalla carta»** — come i punti calcolati della
  prosa (7b), si disegna dalla sua posizione e la scheda dice da dove viene. Oppure il nome scritto: un fix, una
  radioassistenza, «RWY 24» (la soglia). Per ogni punto il tratto che ci arriva (IF il primo di una parte nuova, poi TF;
  CF, DF; RF con il centro, un fix, e il verso L o R), la quota e la velocità com'è stampate. Un arco senza centro o senza
  verso non si salva. Sulla mappa l'anteprima tratteggiata (per una coda, dall'ultimo punto della parte); gli archi in
  anteprima sono corde, il disegno vero lo fa la mappa dopo il salvataggio.
- **La parte nuova** è un avvicinamento con il titolo della carta per designatore («ILS Z RWY 24»), le piste della carta, il
  titolo di AD 2.24 per sezione (senza il trattino morbido), il segmento scritto dall'AOD e la **pagina della carta**: così
  sta sotto di lei (la regola delle pagine di S2). La coda è la parte di prima coi tratti in più, il resto com'era.
- **Il salvataggio** è il meccanismo delle correzioni (carta F6): la parte nuova un'**aggiunta**, la coda una
  **correzione** della parte (`before` è la parte del PDF), con il perché proposto («La carta è un'immagine: il tratto è
  ridisegnato sopra la carta posata.») e la fonte «carta AD 2 LIRN 5-3 (02/10/2025)», tutti e due da cambiare se serve. Si
  manda con «Registra l'AIP». Se un ciclo dopo il PDF porta la stessa parte, l'aggiunta è superata e vale il PDF, come per
  ogni correzione.
- **Codice comune.** Il modello `Aip` ha il tipo di correzione `procedure` (`CorrectionKind.Procedure`, la chiave è
  `Procedure.Key`); il file delle correzioni resta allo schema 1 (un tipo in più si legge come gli altri). Nell'Indice,
  «Aggiungi» non offre le procedure: si ridisegnano sulla carta.
- **Dopo il salvataggio** la pagina resta sullo scalo con la carta aperta e posata (la lettura dello strato dopo una
  correzione non chiude la posa, se il ciclo è lo stesso), la parte si accende sulla mappa e fra le parti ha «✎ ridisegnata»
  (o «allungata») con **«ritira»**; la sua scheda dice la correzione.
Provato sul banco a 1100 px su LIRN ILS Z RWY 24: parte nuova DIEZO-ISKIA più un punto preso sulla carta, poi URRUC-ISKIA
(disegnata sulla mappa), in coda OLEVA (4 tratti), «ritira» (la carta torna a 5 parti); un arco senza centro rifiutato.

**I controlli 1 e 2 del ground fatti il 30 ottobre** (parte di S5, prima della carta d'aerodromo sotto il ground: leggono
solo il testo). Nella pagina dello scalo una scheda **Ground**, in sola lettura sul sector aperto (`GroundDelloScalo`, Core):
- **Le soglie.** Ogni testata di ogni `.rw` dello scalo, file per file (le FIR possono essere diverse da `itrw.rw`), coi
  metri dalla soglia di AD 2.12; oltre 10 m è diversa, la stessa soglia del confronto di AD 2, dove c'è «Sostituisci». Una
  testata che AD 2.12 ha senza coordinate (LIAQ, LICP) lo dice, e non sta fra le piste «che nessun `.rw` ha».
- **Le taxiway.** AD 2.8 non ha un campo nel modello: i nomi si leggono dalla sottosezione com'è stampata (la voce 2),
  senza toccare il lettore comune. Sul 2611 arriva in due forme: una voce numerata con una taxiway per riga
  (`A 15.0 M ASPH …`; `TWY 16.0 M` è una senza nome, `ALL` le dice tutte insieme), o una tabella con le colonne unite in
  una cella, dove negli scali grandi la colonna porta note, larghezze, rimandi e nomi composti (LIRF `AD-AE` e
  `(tra TWY BG`, LIMC `EM-DE-GE- GW-F-…`, LIME `HOLDING POSITION B5`, LIRP `H (4)`): i composti si spezzano, il resto si
  toglie. Contro le etichette dei `.txi` dello scalo: in tutti e due, mancano nel sector, solo nel sector; le etichette
  che non hanno la forma di una taxiway («APRON NORD», «MIL4», «100», «RAMP1») stanno a parte.
Misura sul 2611 col sector del banco: soglie oltre 10 m a LIDP (02 a 249 m, 20 a 62 m), LIPB (01 a 291 m, 19 a 89 m),
LIRI (05 a 367 m), LIBR (31 a 220 m), LICA (10 a 103 m), LICR (11 a 46 m), LILN (17 e 35 a 17 e 24 m); taxiway che
il sector non ha a LIMC (GS), LIRF (CM), LIRA (BF), LIPE (TK, TY), LIRL (L, W: la L è chiusa nelle note), e tutte quelle
degli scali senza `.txi` (LIRJ, LIRG, LIQL, LIPN…). Provato a schermo sul banco: LIRN e LIDP.

**Le note dei parcheggi, 30 ottobre** (controllo 3, la parte che si legge). Nella scheda Ground, sotto le taxiway, «Leggi
le note dei parcheggi» (`NoteDeiParcheggi`, Core): le pagine di testo delle carte dei parcheggi di AD 2.24, riga per riga
com'è stampate, con la pagina, la data e «Apri il PDF». Una carta occupa le pagine fino alla carta seguente dello stesso
file (come in S2). Una pagina entra se ha «REMARKS» o «DESCRIPTION TABLE» e non la legenda di un disegno («Bearings are
magnetic»), se continua una pagina di note con almeno 40 parole, o se stampa una tabella di almeno 10 coordinate (i punti
d'arresto di LIPE, LIEE, LIPY, LIRP, scritti sul disegno). Il campo **Stand** lascia le righe che citano lo stand, ognuna
con quella prima: il numero da solo, non come misura («36 m»), o dentro un intervallo («from 307 to 317», «Stands 11-16»,
non «0600-2200»).
- **Perché riga per riga e non stand per stand.** La tabella «STAND · ACFT CODE · DIMENSIONS RESTRICTIONS · SELF
  MANOEUVRING · PUSH BACK · REMARKS» (LIRN, LIBD, LIBR, LIPQ…), letta come testo, ha le colonne sfalsate: lo stand 201 di
  LIBD cade accanto alle restrizioni di un altro. Dividerla per stand vorrebbe i bordi delle celle, e non tutte le pagine li
  hanno: è un passo a parte, se servirà.
- **Si legge al clic.** Un file di carte si legge in un secondo circa (tutti i 97 scali in 17 s); le note restano per
  ciclo e scalo finché il Lab è aperto.
Misura sul 2611: **18 scali** hanno note — LIBD, LIBF, LIBG, LIBR, LIEE, LIMC, LIMF, LIML, LIPE, LIPQ, LIPX, LIPY, LIRA
(sei pagine), LIRF, LIRI, LIRN, LIRP; gli altri hanno solo il disegno. Provato a schermo sul banco: LIRF «401» trova la
riga del VDGS e «Stands 401, 402, 403, 404, 405 entrance only via TWY NZ»; LIPE «110» i due punti d'arresto.

**Gli stand in metri, 30 ottobre** (controllo 3, la parte che si confronta). Dove le note portano le coordinate degli
stand, sopra le righe la scheda mostra i `.gts` dello scalo contro l'AIP (`StandDelloScalo`, Core): per ogni riga del
sector il punto dell'AIP dello stesso stand più vicino (uno stand può avere più punti d'arresto, «110-stop1», «110-stop2»)
e a quanti metri; oltre 10 m in arancio, in testa. Sotto, gli stand dell'AIP che nessun `.gts` ha.
- **I punti.** Un nome seguito da latitudine e longitudine, nelle forme del 2611: «112-stop2 44° 31' 45.07" 011° 17'
  47.97"», «102-stop 1 …», «10 43°41'41.60" - 010°23'47.71"» (LIRP), «L7 …», «11A …» (LIPY), «21 40°37'25'',99 …» (LIRI),
  più punti sulla stessa riga. Una coordinata senza un nome davanti si salta: sul disegno il nome può stare su un'altra riga,
  e indovinarlo vorrebbe dire sbagliare stand. Per lo stesso motivo il nome letto può essere quello di un'etichetta vicina:
  la tabella dice il nome del punto, e in dubbio si guarda il PDF.
- **I nomi.** Senza zeri davanti («07» è «7»). Il sector di LIRP scrive i punti d'arresto «29S1», «29S2» dove l'AIP stampa
  lo stand «29»: senza un punto con lo stesso nome, si prova lo stand. Lì l'AIP ha un punto solo, e il secondo punto
  d'arresto del sector può stare a una decina di metri senza essere sbagliato.
Misura sul 2611 col sector del banco: **LIPE** 55 stand, quasi tutti a 0 m (il sector è copiato dall'AIP), 211-214 fra 13 e
24 m e D3 a 55 m; **LIEE** 16, 121-124 fra 11 e 36 m; **LIPY** 42 a 25 m e 44 a 19 m; **LIRI** 26 e 27 a 12-15 m (l'AIP stampa
solo 21-27); **LIRP** tutti sotto i 10 m salvo 29S1 (16 m), e gli stand «L7»-«L23» e 71-85 che nessun `.gts` ha. Provato a
schermo sul banco: LIPE.

**S5 fatta il 30 ottobre, notte: la carta sotto il ground e le eccezioni dello scalo** (controllo 4). Con i controlli 1-3
di prima, S5 è chiusa.
- **La carta sotto il ground.** In testa alla scheda Ground, «La carta sotto il ground»: le carte di AD 2.24 che disegnano
  il ground (`GroundDelloScalo.EDelGround`: «AERODROME CHART», «AERODROME GROUND MOVEMENT CHART», i parcheggi, le «HOT SPOT
  MAP», le «LOW VISIBILITY PROCEDURES CHART»; non gli ostacoli, l'atterraggio, le quote radar, le DESCRIPTION TABLES), ognuna
  con «posa…» o «posata», che apre la carta da sola col riquadro della posa e torna al Ground. Si posa sulle soglie per nome
  («RWY 06»), come in S3.
- **Il ground del sector sopra.** La casella «Il ground del sector sopra la carta» (nella scheda Ground e sotto la posa)
  mette sulla mappa dell'AIP espanso gli strati del sector aperto che fanno il ground — disegni `.geo`, terra (`.pol`,
  `.txi`, `.gts`), piste —, fra le coste e lo strato dell'AIP: la carta sotto, il sector sopra, l'AIP in cima. Negli Strati
  della mappa dell'AIP compaiono finché la casella è accesa, e restano con «Solo lo scalo sulla mappa». Un clic su una forma
  del sector lì non la sceglie (chiuderebbe la pagina dello scalo): il nome lo dice il suggerimento.
- **Lo scarto in metri.** Sotto il mezzo miglio la posa dice metri («1 cm della carta = 104 m», «scarto massimo 37 m»):
  una carta d'aerodromo sbaglia di decine di metri, e «0,02 NM» non diceva niente.
- **Le eccezioni dello scalo** (controllo 4, D5). Sotto ogni controllo «Un'eccezione…»: si sceglie una delle voci diverse
  (la testata, la taxiway, lo stand) e si scrive il perché; il controllo la salta — la riga non è più arancio e dice
  «eccezione», la taxiway o lo stand escono da «mancano» —, e sotto il controllo l'eccezione si legge col perché e la data,
  con «togli». Un'eccezione la cui differenza non c'è più lo dice. Il file è `eccezioni\<ICAO>.json` nel repo dell'AIP
  espanso, una per voce e controllo; «02» e «2» sono la stessa testata, «07» e «7» lo stesso stand.
- 🔴 **«Registra l'AIP» non prendeva le pose.** I confini del repo dell'AIP (`RepoDellAip.DentroIConfini`) erano i cicli e
  `correzioni\`: `posa\` (S3) restava fuori, e «Registra l'AIP» lasciava le pose nella cartella senza dirlo. Ora ci sono
  `posa\` ed `eccezioni\`.
Provato sul banco a 2400 px: LIRN, la carta d'aerodromo AD 2 LIRN 2-1 posata su RWY 06 e RWY 24 (1 cm = 104 m, i 2 039 m fra
le soglie), il ground del sector sopra, allineato sulla pista; LIDP, l'eccezione sulla soglia 20 (62 m) nei tre `.rw` che la
hanno, poi «togli». Banco rimesso com'era.

## §5 — Deciso e da decidere

- **D1** (committente, 27 ottobre): si scrive questa carta col disegno, e si parte dalla pagina dello scalo (S1).
- **D2** (proposta, fatta in S2): le voci di oggi per segmento restano e diventano parti della carta: la A30 cambia così.
  Sulla mappa e nell'Indice le voci sono quelle di prima; la carta le raccoglie nella pagina dello scalo.
- **D3** (committente, 30 ottobre): l'immagine della pagina la fa **PDFium** nel Lab, non pdf.js nella pagina. Si usano
  i binari di `bblanchon.PDFium.Win32` e `bblanchon.PDFium.Linux` (PDFium d'oggi, aggiornati a ogni versione di Chrome;
  Linux per la CI) con un collegamento nostro di poche funzioni, non un involucro .NET: quelli che ci sono (Docnet) sono
  fermi al 2023 e portano un PDFium vecchio.
- **D4** (committente, 30 ottobre): i punti della posa stanno nel repo dell'AIP espanso, `posa\<ICAO>.json`, accanto a
  `correzioni\`; le immagini mai (si rifanno dal PDF, nella cache del Lab).
- **D5** (proposta, fatta in S5): le eccezioni dello scalo stanno anche loro nel repo dell'AIP espanso,
  `eccezioni\<ICAO>.json`, accanto a `posa\`. Dicono il sector, non l'AIP, ma il sector è un repo di file di Aurora (un
  JSON lì finirebbe nel pacchetto) e il repo dell'AIP è dove il Lab tiene quel che sa degli scali, e lo registra con
  «Registra l'AIP». Se un giorno il sector avrà un posto per i dati del Lab, il file si sposta così com'è.
