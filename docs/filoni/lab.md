# Filone Sector Lab — stato

> Scrive **solo** l'agente del Lab (cartella `vipi-lab`, ramo `lab/f3`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Storia fino al 23 settembre 2026:
> `docs/lavori-aperti.md` §A71, §A113, §A115, §A116.

## Dove siamo — 21 ottobre 2026: la pagina Testo, T1 fatta

Dopo R4-R9 il committente ha chiesto la **pagina Testo** (D12, D13, D21). Divisa in T1-T3 (proposta dell'agente, in
carta): **T1 fatta** — «Testo» nello spazio Sector, l'albero dei file, CodeMirror 6 dentro l'app (pacchetto in
`wwwroot/vendor/codemirror`, ricetta in `tools/codemirror`), «Applica» che fa una modifica in sospeso coi soli cambi,
i metadati `//@` bloccati e sbloccabili. Lab 1 340. **Prossime**, al via: T2 (problemi sulla riga, nomi dal catalogo,
Ctrl+clic, la domanda prima di cancellare un record coi metadati) e T3 (i legami Scheda/mappa/Confronti ↔ Testo).

## 20 ottobre 2026, notte: terza forma, R4-R9 fatte

Dopo il passaggio d'account: **ritocco** (aprendo una sezione del Confronti, sulla mappa solo quella, dei due lati),
**R7** (i gesti dei confronti nella pagina Confronti, riga per riga e per sezione; «Lettura e consegna» torna nello spazio
AIP espanso), la **misura R8** dei file compatibili, **R8a** («metti in questo file» per i punti, fra i `.fix`) e **R8b**
(le rotte nelle aerovie basse o alte; il confronto legge tutti e due i file), **R8c** (scali e piste nuovi anche nei
file delle FIR, «Sostituisci in tutti i file»). Le ATZ delle torri restano in `twrs.tfl` e negli `.str` (il
committente), **R9** (i confini di itgeo.geo nell'AIP espanso, come correzioni, dalla sezione «Confini e coste»). Lab
1 330. Il piano R4-R9 è finito; **dopo**, al via del committente: le due consegne, la pagina Testo, la Scheda per gruppi,
i verbi. I confini nell'AIP come rette: confermato dal committente. Dettaglio nella carta del riordino, §6. Il resto qui sotto vale ancora.

## 20 ottobre 2026, notte: terza forma, R4-R6 fatte (passaggio a un altro account)

Tutto nella carta [`2026-10-20-riordino-delle-pagine.md`](../feature/2026-10-20-riordino-delle-pagine.md): §4-ter (la
terza forma, D14-D22, **approvate**), §5-ter (le slice R4-R9) e §6 (il lavoro, slice per slice, e «Dove siamo» in fondo).

- **Ambiente di lavoro** (prima della terza forma, chiesto per il primo avvio di un AOD): `--dati <cartella>` per i dati
  del Lab (l'autoprova in `%TEMP%\VipiSectorLab-autoprova`); il riquadro **«Primo avvio»** (link del sector e dell'AIP
  espanso, cartella di Aurora, cartella di lavoro → `sector`, `aip-espanso`, `pdf-aip`, «Prepara tutto»); il Lab lavora
  **solo nel suo ambiente** (riapre da sé il suo sector, elenco dei sector dell'ambiente, «Prendi un altro sector» col
  nome, niente «Apri» su una cartella qualsiasi, cartelle dei PDF e dell'AIP espanso fisse con «Apri la cartella»);
  link del browser puliti (`IndirizzoDelRepo`); **cartella di Aurora** (schemi da `ColourSchemes`) e **profilo di Aurora**
  (simboli dei punti dalle righe `SYMBOLS_`, di base `BASIC_RADAR.cpr`).
- **Terza forma** (D14-D22): tre spazi di lavoro — **AIP espanso** (Indice), **Sector** (Mappa, Consegna; poi Testo),
  **Confronti** — ognuno con la sua mappa sullo stesso motore; «metti in questo file» (una voce, più voci o una sezione
  intera nel file scelto) nel Confronti; il Testo nello spazio Sector; l'AIP espanso si modifica solo coi moduli.
- **R4 fatta** (tre mappe, un motore: `StatoDellaMappa` per spazio, JS `mappe[id]`), **R5 fatta** (l'AIP disegnato come il
  sector: chiavi dello schema e simboli di Aurora), **R6 fatta** (la pagina **Confronti** in lettura: 13 sezioni, righe
  unificate `RigaDelConfronto`, filtro per esito, due schede, mappa coi due lati arancio/blu e la coppia della riga).
  La pagina di prima resta come «Lettura e gesti» finché la **R7** non sposta i gesti nel Confronti.
- **Prossimo**: la R7 (i gesti di oggi nel Confronti), dopo il via del committente; prima, forse, la proposta aperta
  «aprendo una sezione, sulla mappa solo i suoi generi» (la mappa del Confronto è piena). Poi R8 «metti in questo
  file» (prima la misura dei file compatibili), R9 verso l'AIP, e le slice di prima (consegne, Testo, Scheda, verbi).
- **Da fare dal committente**: il giro del primo avvio in `D:\Programmazione\IVAO_Test\SectorLab-prova\PrimoAvvio\`
  (`LEGGIMI.txt`, «Avvia il Lab (primo avvio).cmd»); il suo Lab vero in `%LOCALAPPDATA%` non ha ancora un ambiente e
  alla prossima apertura chiederà il primo avvio.
- Lab 1 318 test. Eseguibile di prova: `D:\Programmazione\IVAO_Test\SectorLab-prova\VipiSectorLab.exe`, rifatto a ogni
  slice (prima `Get-Process VipiSectorLab`: se è aperto si aspetta il committente).

## 20 ottobre 2026: riordino delle pagine (carta approvata, R0-R3)

Il committente, aprendo l'app: «è molto confusionaria». Fatto l'inventario di tutte le operazioni (circa 150, in nove
gruppi) e approvata la direzione: quattro pagine (Sector, AIP, Consegna, Impostazioni), barra uguale nelle due finestre,
Scheda per gruppi fissi. Carta: [`2026-10-20-riordino-delle-pagine.md`](../feature/2026-10-20-riordino-delle-pagine.md).
**R0 fatta** (i due difetti: «Cambia cartella» senza conferma, link della PR dell'AIP). **R1 fatta**: le quattro
linguette nella barra, la barra uguale nelle due finestre con «Salva (N)» e Ctrl+S, AIP / Consegna / Impostazioni come
pagine, la mappa che non si smonta. Dopo il primo giro il committente ha chiesto **due mondi** (AIP con un Indice come l'AIP, Sector con Mappa, **Testo**
scritto a mano coi metadati bloccati e sbloccabili, Consegna) che si parlano: carta §4-bis (D8-D13), slice R2-R8 in
§5-bis. **R2 fatta** (i due mondi nella barra), **R3 fatta** (l'Indice dell'AIP come l'AIP, con la ricerca, la scheda e la
mappa). Dopo la R3 corretti i punti RNAV degli scali (finivano in «Altro»); il committente vedeva solo ENR 2 e 5 perché il suo
clone dell'AIP espanso era fermo al `main` del 19 ottobre (carta, «Dopo la R3»). Da sistemare: l'autoprova scrive
«ultima cartella» e «recenti» nei dati del Lab. **Prossimo**: R4 (i confronti dentro l'Indice e i legami record ↔ voce),
dopo un giro del committente sull'Indice. Eseguibile di prova: `D:\Programmazione\IVAO_Test\SectorLab-prova\VipiSectorLab.exe`.

## 20 ottobre 2026: F7, SID e STAR (chiusa: slice 7a-7f, T4 in Aurora ✅)

Il committente ha chiesto SID e STAR (il confronto col sector delle parti nuove della F6 è rimandato) e ha approvato la
carta [`2026-10-19-f7-sid-e-star.md`](../feature/2026-10-19-f7-sid-e-star.md) (D1-D7 come proposte). **Slice 7a fatta**:
il modello `Procedure` e l'importer dei file 4 e 6 di AD 2 — 437 procedure, 2 021 tratti, 217 punti RNAV di scalo dai 5
scali, zero segnalazioni di lettura; controprova con pdfplumber; 3 punti stampati male nell'AIP (RF407, RN534, RN544).
Registrato: PR #10 dell'AIP espanso di prova. **Slice 7c fatta**: le procedure sullo strato «AIP espanso» (SID, STAR,
transizioni, salite iniziali; gli archi RF come archi), la scheda coi tratti; sul banco 313 disegnate, 124 in sola prosa.
**Slice 7d fatta**: «Confronta SID e STAR», per nome (tre modi di abbreviare, le transizioni) e per
percorso; sulla copia 140 uguali, 150 diverse, 134 mancanti, 7 solo nel sector, le differenze controllate sono vere; trovato
e corretto un difetto della 7a (le piste di LICA, PR #11). **Slice 7e fatta** (l'`initialclimb` no: la quota la mette il
committente, D5): «Porta nel sector», «Sostituisci», «Scrivi i tracciati delle SID uguali», coi punti che mancano
prima e i blocchi §M coi `//@@` di quota e velocità. Sulla copia, dopo i gesti, 339 uguali, 4 diverse e 81 mancanti, di
cui 68 in prosa. **Slice 7b fatta**: la prosa delle convenzionali diventa tratti (`ItalianAd2Prose`), coi punti senza
nome calcolati dalle radioassistenze e gli archi DME. Su 2610 ne legge 104 su 113; le 9 restano in prosa e si dice perché.
Mappa, confronto e gesti li usano. Corretto un difetto della 7d/7e: le STAR del sector con le coordinate non si vedevano.
AIP espanso di prova sulla PR #11. **Slice 7f fatta**: la mappa di gruppo `SID RNAV(ALL)` in testa al `.sid` (P10), col motore
delle composte di F3-bis esteso alle SID e il tasto «Mappa delle SID»; corretto un difetto della 7e (le composte non
seguivano «Sostituisci»). **F7 chiusa**: la prova T4 in Aurora è andata («Tutto ok»: la STAR RNAV, le SID col tracciato, la STAR dalla prosa, la
mappa `MAPS` del `.sid`). **Prossimo**: lo sceglie il committente (il giro del 2611 dal 29 ottobre, i confronti rimandati
della F6, la F8). **Da vedere dopo**: le virate e le intercettazioni delle convenzionali, oggi a spigolo (carta F7, in
fondo).

## 19 ottobre 2026: F6, le correzioni dell'AIP espanso finite (slice 1-3)

Il committente vuole l'AIP espanso correggibile: **errori dell'AIP stesso e aggiunte da altre fonti**, che durano da un
ciclo all'altro finché un PDF o un'altra correzione non le supera. Carta:
[`2026-10-19-f6-correzioni-aip-espanso.md`](../feature/2026-10-19-f6-correzioni-aip-espanso.md). **Slice 1**: il file
`correzioni\li.json` nel repo dell'AIP espanso, applicato da ogni «Leggi». **Slice 2**: le aree, dalla loro scheda sulla
mappa (Correggi, Togli, Ritira) e «Aggiungi» nel riquadro. **Slice 3**: tutti gli altri tipi (punti, radioassistenze,
luci, pericoli, scali ora anche sulla mappa; rotte e attese), con un modulo solo; le parti composte in JSON. Provate a
schermo sul banco. Fuori: le carte ATC SMAC. **Prossimo**: lo sceglie il committente.

## 19 ottobre 2026: F6, l'importer dei PDF finito

L'importer legge ora tutto quel che è testo: **ENR 3.4.2** (129 attese sulle radioassistenze), **ENR 5.2.2.4** (8
corridoi OAT), **ENR 5.3** (31 pericoli: palloni sonda, laser, vulcani; 5.3.4 solo regole) e la **carta ATC SMAC** dei 5
scali (78 settori, il `.mva`). Nel modello: `Hazard`, `MinimumAltitudeArea`, due campi di `Holding` (tutti
facoltativi, schema 2). Controprove con pdfplumber: tornano tutte. Restano fuori le carte di AD 2 che sono immagini
(2, 3, 5) e le SID/STAR (4, 6, la F7). Registrato: **PR #9** (solo aggiunte), **fusa**: l'AIP espanso 2610 ha ora 1 096
aree, 542 attese, 31 pericoli e 78 settori SMAC, da 123 PDF su 148. Carta F6 §6, «L'importer dei PDF finito».
**Prossimo**: lo sceglie il committente (i confronti delle parti nuove — attese di ENR 3.4.2, corridoi OAT, SMAC contro
`.mva` —, la F7 per SID e STAR, la slice 6 del ciclo).

## 19 ottobre 2026: F6, slice 5f fatta (i gesti di AD 2.19): AD 2 chiusa per testo e radioassistenze

**Slice 5f fatta**: «Sostituisci» e «Porta nel sector» di ENR 4 sulle righe di AD 2.19 (il cuore dei gesti ora è comune ai
due confronti). Sul banco: PNZ col canale 93X, GEN portata in `itvor.vor`, 24 uguali; «Annulla tutto» torna indietro.
Carta F6 §6. **Prossimo**: le carte di AD 2 (stand, `.mva`, SID/STAR), o quel che sceglie il committente.

## 19 ottobre 2026: F6, slice 5e fatta (il confronto di AD 2.19)

**Slice 5e fatta**: le radioassistenze di AD 2.19 contro `.vor`/`.ndb` con le regole di ENR 4.1, in sola lettura, più il
confronto dentro l'AIP con ENR 4.1 (al mezzo secondo: ENR 4.1 arrotonda al secondo). Sulla copia: 24 radioassistenze,
22 uguali, PNZ diversa (canale 87X/93X), GEN mancante; nessuna discordanza con ENR 4.1; 12 ILS che il sector non tiene.
Carta F6 §6. **Prossima: 5f** (i gesti di ENR 4 su AD 2.19), da decidere col committente.

## 19 ottobre 2026: F6, slice 5d fatta (l'importer di AD 2.19)

Le 8 frequenze mancanti restano fuori: le decidono gli AOD (committente). **Slice 5d fatta**: AD 2.19 dei 5 scali nel
modello, 29 radioassistenze di scalo (`Navaid`, sezione AD 2.19) e 12 ILS (record nuovo `Ils`, 47 apparati con la loro
frequenza). Controprova con pdfplumber: 103 coordinate su 103. Registrato nell'AIP espanso: **PR #8** (solo righe
aggiunte). Carta F6 §6. **Prossima: 5e**, il confronto coi `.vor`/`.ndb` (gli ILS il sector non li ha): aspetta il
via e la fusione della PR #8.

## 19 ottobre 2026: F6, slice 5c fatta (i gesti di AD 2)

**Slice 5c fatta**: le soglie del confronto (10 m, 1°) confermate dal committente; «Sostituisci» su scali e piste diversi
(una riga per file, solo i campi diversi) e «Porta nel sector» per una pista che manca (in `itrw.rw`, dentro `//PISTE`).
Le frequenze restano in sola lettura. Sul banco: le 8 piste diverse sostituite → 21 uguali; l'ATZ di Orio col gesto
della slice 1c → ATZ uguali 39; «Annulla tutto» torna indietro. Carta F6 §6. **Da decidere col committente**: le 8
frequenze mancanti, e cosa leggere dopo di AD 2 (carte, AD 2.19).

## 19 ottobre 2026: F6, slice 5b fatta (il confronto di AD 2)

**Slice 5b fatta**: il testo di AD 2 dei 5 scali contro il sector, in sola lettura, una riga per file (OTHER/it… e le
FIR). Scali coi `.ap`: 13 righe uguali. Piste coi `.rw`: 8 diverse su 21 (la 10 di LICA a 103 m, la 12/30 di LIME con
la rotta di 1,6°, la 34R di LIRF a 6 FT contro 5, la 06 di LIRN in `libb.rw`). Frequenze coi `.frq`: 30 uguali, 8
mancanti (quasi tutte «a discrezione ATC»); emergenza e ATIS non si confrontano. Le ATZ di AD 2.17 ora vanno col loro
scalo nel confronto delle ATZ: Orio diversa di 0,72 NM. Soglie 10 m e rotta 1°: proposte, da confermare. Carta F6 §6.
**Prossima: 5c**, i gesti: aspetta il committente (cosa si corregge, e le frequenze «a discrezione»).

## 19 ottobre 2026: F6, slice 5a fatta (il testo di AD 2)

**Slice 5a fatta**: il testo di AD 2 dei 5 scali che ci sono (LICA, LIMC, LIME, LIRF, LIRN) nel modello (`Aerodrome`): ARP,
elevazione e declinazione (AD 2.2), 18 piste con soglie e fine pista (AD 2.12), distanze dichiarate (AD 2.13), 51
frequenze (AD 2.18), e le 5 ATZ di AD 2.17 fra le aree. Controprova con pdfplumber: torna tutto. PR #7 fusa. Carta F6 §6.
**Prossima: 5b**, il confronto con `.ap` (ARP, elevazione), `.rw` (soglie, elevazioni, rotte magnetiche), `.frq` (le
frequenze delle posizioni) e le ATZ degli `.str`; poi 5c, i gesti. Da vedere subito: a LICA la soglia della 10 nel sector
sta a circa 100 m da quella dell'AIP.

## 19 ottobre 2026: F6, slice 4c fatta (AAR, AT, INDIA e i gesti di ENR 5): la slice 4 è chiusa

**Slice 4c fatta**: prima le aree di ENR 5.2.2 scritte a testo (committente: «prendi anche AAR, AT e INDIA»): 7 AAR coi
punti per nome presi da ENR 4, 6 INDIA, INDIA 5 SUD, 8 AT (PR #6). Poi i gesti nel confronto delle aree di ENR 5: «Porta
nel sector» (P in `italy.prohibit`, D in `italy.danger`, il resto e le militari in `italy.restrict`), «Sostituisci» e
«Dividi» (`R300AB` → `R300A` e `R300B`, i cerchi a 0,5 NM). R107A-D nel sector sono scritte male (uno spazio al posto del
«;»): «Porta» lo dice e non le riscrive. Sul banco: 424 uguali, 91 diverse, 206 mancanti, 3 solo nel sector. Carta F6
§6. PR #6 fusa.

## 19 ottobre 2026: F6, slice 4b fatta (il confronto di ENR 5)

**Slice 4b fatta**: «le aree di ENR 5» (ENR 5.1 e 5.2 contro `italy.prohibit`, `.restrict`, `.danger`), per nome — una
voce del sector può valere più aree (`R300AB`, `D75`), i pezzi vicini sono una voce, la vicinanza solo nei due versi —, in
sola lettura. Sulla copia: 411 uguali, 84 diverse (19 cerchi da 1 NM nel sector e 0,5 nell'AIP; 4 oltre 25 NM), 13 di
ENR 5.1 mancanti (quelle della F0 più EUC 60 e 660), ENR 5.2 tutta mancante (191), 23 solo nel sector (AAR, AT, INDIA…).
Carta F6 §6.

## 19 ottobre 2026: F6, slice 4a fatta (l'importer di ENR 5.2)

**Slice 4a fatta**: i 59 poligoni militari di ENR 5.2.1 e le 132 zone e corridoi APR di ENR 5.2.2.5 nel modello e
nell'AIP espanso (PR #5). I corridoi sono una geometria nuova (`corridor`, ampiezza e punti): **lo schema del modello è
ora 2** (i file della 1 si leggono ancora); il converter ne fa il contorno. Controprova con pdfplumber: coordinate tutte.
Sulla mappa del Lab il genere «Militari». Non lette, e dette: le parti di testo di 5.2.2 e 5.3. PR #5 fusa. Carta F6 §6.

## 19 ottobre 2026: F6, slice 3c fatta (i gesti di ENR 4): la slice 3 è chiusa

**Slice 3c fatta**: «Sostituisci» sulle radioassistenze diverse (solo i campi diversi: frequenza, posizione, canale TACAN;
l'attesa e il resto restano) e «Porta nel sector» (una o tutte) per radioassistenze e punti mancanti, in `itvor.vor`,
`itndb.ndb`, `itfix.fix`; «Correggi l'uso» (4° e 5° campo di `itfix.fix`, confermati dal committente). Annullare ora
rifà i confronti con l'AIP. Sulla copia: ELB e PNZ sostituite → 55 uguali; i 4 usi corretti. Carta F6 §6.

## 19 ottobre 2026: F6, slice 3b fatta (il confronto di ENR 4)

**Slice 3b fatta**: «Confronta le radioassistenze» (ENR 4.1 contro `.vor` e `.ndb`: posizione, frequenza, canale TACAN)
e «i punti» (ENR 4.4 contro i `.fix`; in `itfix.fix` anche uso e FRA), in sola lettura. Sulla copia: radioassistenze 53
uguali, 2 diverse (ELB e PNZ, il canale TACAN), 93 solo nel sector (di scalo o estere); punti 1 254 uguali su 1 254, 2
solo in `itfix.fix` (NANKA, TEMRI), 4 con l'uso diverso. Carta F6 §6.

## 19 ottobre 2026: F6, slice 3a fatta (l'importer di ENR 4)

**Slice 3a fatta**: le radioassistenze di ENR 4.1 (55 su 56: 38 in rotta e 17 militari, ognuna coi suoi apparati,
posizioni e orari), i 1 254 punti di ENR 4.4 e le 11 luci di ENR 4.5 nel modello (`Navaid`, `SignificantPoint`,
`GroundLight`) e nell'AIP espanso: PR #4. Le righe di ENR 4 si tagliano coi **bordi orizzontali** (`PdfText` ora li
legge). Controprova con pdfplumber: coordinate uguali tutte. **GRO** (DVOR di Grosseto) non entra: l'AIP non ne stampa
le coordinate. Il committente: PR #4 fusa, GRO non si segnala. Carta F6 §6.

## 19 ottobre 2026: F6, slice 2c fatta (i gesti di ENR 3)

**Slice 2c fatta**: «Scrivi i tratti» (verso e quote dell'AIP nei tag `//@@`, una voce sola; le `locked` no), «Porta
nel sector» le rotte mancanti (blocco con `note`, non `locked`), «Porta le selezionate» per le attese (di procedura
nello `.str` dello scalo, con la scritta o l'ovale; in rotta in `HOLDENR.hold`, l'ovale in rotta da decidere),
«Sostituisci» per le attese diverse. Sulla copia: 187 tratti su 24 rotte; attese 156 di procedura e 35 in rotta.
Sul sector vero dopo il 29 ottobre. Carta F6 §6.

## 19 ottobre 2026: F6, slice 2b fatta (il confronto di ENR 3)

**Slice 2b fatta**: «Confronta le rotte» (ENR 3.2 contro `itawlow.lairway`: lati, tratti coi tag `//@@`, posizioni) e
«le attese» (ENR 3.4.1 contro `HOLDENR.hold`), in sola lettura. Sulla copia: rotte 21 uguali, 3 diverse (Z910, M859,
L869), 5 mancanti; nessun tratto del sector ha ancora i tag. Attese 51 uguali, 3 diverse di un grado, 191 non in
`HOLDENR.hold` (105 sono attese di scalo negli `.str`), 14 solo nel sector (radioassistenze: ENR 3.4.2). Carta F6 §6.

## 19 ottobre 2026: F6, l'AIP espanso sulla mappa (e la slice 2a)

**L'AIP espanso sulla mappa**, chiesto dal committente prima della 2b: nel riquadro «AIP» «Mostra l'AIP espanso sulla
mappa» → lo strato «AIP espanso» in fondo agli Strati, un tipo per casella (CTR, ATZ, TMA… rotte, attese), un colore
per tipo, e un clic apre la scheda della voce in sola lettura. Separato dal sector (file `aip:<ciclo>`): nessun gesto
lo tocca. Provato sul banco (1 312 forme, la scheda di N503). I tratti di confine e di costa seguono `itgeo.geo`
(eccezione decisa dal committente): 44 su 44, la FIR di Milano per le Alpi. Carta F6 §6.

## 19 ottobre 2026: F6, slice 2a fatta (l'importer di ENR 3)

**Slice 2a fatta**: le rotte di ENR 3.2 (29 su 29 PDF scaricati, zero segnalazioni: punti, per tratto verso e quota
minima per parità, limiti, note, interruzioni) e le attese in rotta di ENR 3.4.1 (413 su 245 punti, controprova con
pdfplumber, tre righe a celle unite dette) nel modello (`Route`, `Holding`) e nell'AIP espanso: PR #3 aggiornata.
ENR 2 ed ENR 5.1 ancora 870 aree. Il committente: Viterbo si segnala a ENAV (testo pronto, lo manda lui), le torri di
Guidonia e Torino le valuta lui. **Prossima: 2b**, il confronto delle rotte con `itawlow.lairway` e delle attese con
`HOLDENR.hold`, in sola lettura.

## 18 ottobre 2026, sera: F6, le ATZ (negli .str e in twrs.tfl)

Il committente ha approvato le quattro scelte della 1c e detto dove stanno le altre famiglie: TMA e CTA il sector non
le tiene, le ATZ sì, negli `.str` e in `twrs.tfl`. Ora si confrontano anche le ATZ, nei due posti, con «Sostituisci» e
«Porta nel sector»: negli `.str` 34 uguali, in `twrs.tfl` 27 uguali e 4 diverse. Trovati un **errore di stampa
nell'AIP** (il centro dell'ATZ di Viterbo a 017°E, in Adriatico: si segnala, non si sostituisce) e un abbinamento
sbagliato (Vergiate prendeva la torre di Malpensa: ora comanda lo scalo). Carta F6 §6. **Prossima**: da decidere col
committente (Viterbo a ENAV? le torri di Guidonia e Torino Aeritalia; poi la slice 2, ENR 3).

## 18 ottobre 2026: F6, slice 1c fatta («Sostituisci» e «Porta nel sector» per i CTR)

**Slice 1c fatta**: su ogni riga del confronto dei CTR un gesto — «Sostituisci» (forma diversa: la voce CTR riscritta
per intero con le zone dell'AIP, la testata resta) e «Porta nel sector» (manca: voce nuova nello `.str` dello scalo
scritto dall'AOD). Gesti come gli altri: diff, annulla, lotto, «Salva». Sulla copia del sector: 6 sostituite → 45 CTR
uguali su 45; provato a schermo sul banco. Scelte approvate dal committente (zone col commento su una riga sua, densità
degli archi della voce, confini presi dalla linea del sector, niente `//@`): carta F6 §6. Sul sector vero dopo il 29
ottobre. **Prossima**: da decidere col committente (ATZ/TMA/CTA: dove le tiene il sector? o la slice 2, ENR 3).

## 18 ottobre 2026: F6, slice 1b fatta (il confronto dei CTR col sector)

**Slice 1b fatta**: «Confronta i CTR» nel riquadro «AIP» — i CTR dell'AIP espanso contro le voci CTR dei `MAPS` del
sector aperto, a distanza (0,1 NM), in sola lettura. Sul sector vero: 45 CTR, 39 uguali, 6 con la forma diversa
(Malpensa senza un vertice, Parma con un arco in più, Cagliari, Amendola, Aviano, Palermo), verificati a mano i primi
due. AIP espanso: PR #3 (Bergamo e testo leggibile). **Prossima: 1c**, «Porta nel sector»/«Sostituisci» sul fork.

## 18 ottobre 2026: F6, slice 1a fatta (l'importer di ENR 2)

**Slice 1a fatta**: ENR 2 si legge per intero — 59 PDF, 329 aree (CTR, CTA, TMA, ATZ, RMZ, NPZ, FIR/UIR con le fasce,
FRAIT, FMC), controprova con pdfplumber spiegata file per file, ENR 5.1 ancora identico alla F0. Quattro famiglie di
tabelle, più tabelle per pagina, barre di modifica, gruppi a zone, rimandi «As for». Nel modello: fasce verticali,
ente, orari, altitudine di transizione. **Prossima**: la seconda PR dell'AIP espanso, poi la 1b (il confronto col
sector, prima i CTR).

## 18 ottobre 2026: F6, slice 0 fatta (modello, converter, importer, AIP espanso nel Lab)

**La slice 0 della F6 è fatta.** Nel Lab il riquadro «AIP»: la cartella dei PDF («Prendi i PDF da Download», solo su
quel tasto) e «Leggi nell'AIP espanso», che scrive il modello in `<AIP espanso>\<ciclo>\li.json`. Provato a schermo
sul banco: 541 aree dai PDF di ENR 5.1. E il **repo dell'AIP espanso** (`SkyMistery/it-aip-espanso-test`): «Prendi l'AIP
espanso», la lettura sul ramo `aip/<ciclo>`, «Registra», «Manda»; provato col repo vero, ramo `aip/2610` sul server, PR
da aprire e fondere dal committente. **Prossima: slice 1, ENR 2.** Per la slice 4: ENR 5.1.5 (cross border, 5 colonne) e le configurazioni TSA di 5.1.4 p.23.

## 18 ottobre 2026: F6, slice 0a, 0b e l'importer della 0c fatti

**L'importer dei PDF è fatto** (prima parte della 0c): `src/Aip.Pdf` e `src/Aip.Pdf.Italia` su PdfPig, con la riga di
comando `tools/Aip.Pdf.Cli` (`aip-pdf`). Sui PDF veri del 2609 legge ENR 5.1 per intero: 541 aree con la data di ogni
pagina, identiche alla F0 area per area (2 207 coordinate). Il lettore delle geometrie della F1 è copiato, il sito non
è toccato. **Prossima: la seconda parte della 0c**, nel Lab: la cartella dei PDF («Prendi i PDF da Download») e l'AIP
espanso.

## 18 ottobre 2026: F6, slice 0a e 0b fatte (il modello AIP e il converter per Aurora)

**Slice 0b fatta**: `src/Aip.Aurora` scrive le aree del modello come righe `.geo` di Aurora, un blocco §M per area
(limiti, classe, ciclo), archi e cerchi disegnati alla densità scelta; la riga di comando `tools/Aip.Aurora.Cli`
(`aip-aurora`); 37 test per TFM che rileggono col motore del sector. Prima, col committente, il modello rivisto:
attività e note dentro, il quadro di tutto quel che entra nell'AIP espanso (carta F6 §6), repo privato. **Prossima:
slice 0c**, l'importer dei PDF (serve il repo di prova dell'AIP espanso, lo crea il committente).

## 18 ottobre 2026: la F6 è partita, slice 0a fatta (il modello AIP)

La **slice 0a** della F6 è fatta: il progetto `src/Aip` (il modello AIP, zero dipendenze, niente «vIPI» nei nomi) col
suo JSON versionato, aree e spazi aerei per primi; un'area di ogni forma (poligono, cerchio, archi, confine e costa) va
in JSON e torna uguale; 28 test per TFM in `tests/Aip.Tests`. Dettagli e scelte in carta F6 §6. Strada facendo il
cancello dei conteggi (`tools/conta-test.sh`, codice comune) ora conta anche gli assiemi che non cominciano con `Vipi`.
Allineato a `main` 1.67.0. **Prossima: slice 0b**, il converter per Aurora (`Aip.Aurora`) e la sua riga di comando.

## 18 ottobre 2026, sera: F6 approvata, si comincia dalla slice 0a

Dopo la F5, il piano del committente: **1)** le prove in Aurora ancora aperte, preparate dal Lab sul fork — **fatte**:
otto nel riquadro «◫ Aurora → Prove in Aurora» (B9 provata 5/5; F5, A8, R4, I7-C, I7-B con O4, K2, simboli nel profilo
da provare dal committente; carta F5 §7); **2)** i piccoli resti — **fatti** (C2 «togli» dal changelog con `*NIL`;
carta F5 §7-bis); **3)** la **F6, i PDF dell'AIP**: carta [`2026-10-18-f6-pdf-aip.md`](../feature/2026-10-18-f6-pdf-aip.md)
✅ **approvata il 18 ottobre**. Il cuore: un modello AIP neutro e multi-Stato (`Aip`), gli importer dei PDF (`Aip.Pdf`
+ `Aip.Pdf.Italia`), il converter per Aurora (`Aip.Aurora`, con riga di comando) — progetti a sé, riusabili altrove,
senza «vIPI» nei nomi, che girano **solo in locale** (mai sul sito); nel Lab la lista delle differenze con «Porta nel
sector» e «Sostituisci»; l'AIP espanso (tutto, anche quel che il sector non ha) in una cartella e in un repo GitHub di
prova sul suo account (lo crea lui); il file per la pagina airspace del vSOP al posto del KMZ. Ordine dell'AIP, da ENR 2.
**Prossima: slice 0a, il modello AIP** (aree e spazi aerei per primi, JSON versionato, esempi scritti a mano).
Il sector vero si tocca solo dopo l'uscita del 2611 (29 ottobre).

## 18 ottobre 2026: la F5 è fatta (Aurora a lotti)

La **F5** è fatta, tutte e sei le slice, provate dal committente sul suo PC: la vetrina (1), il lotto (2), gli esiti
✅/❌ con le note sui record (3), le prove come lotti — la B9, ordine a blocchi delle aerovie, provata 5/5 (4) —, il giro
vero (5): le etichette delle aerovie del secondo giro R-10, fatte dal Lab sul banco (PR #9 sul fork), guardate dal
committente in Aurora con una ricarica, 3 voci arrivate dalle registrazioni, 3 ✅. Strada facendo: il lotto dalle
registrazioni, «Sul server, da guardare», il `fetch` di tutti i rami (il suo clone era a un ramo solo). Carta
[`2026-10-18-f5-aurora-a-lotti.md`](../feature/2026-10-18-f5-aurora-a-lotti.md), §6. Resta del Lab: il giro di prove
del lotto «Subito» (78-238), le prove a mano di slice 2-3 della F5 se il committente le vuole rifare per conto loro, il
resto del secondo giro R-10, il passaggio al repo vero (D4 della F4).

🔴 **Decisione del committente (18 ottobre)**: ogni modifica e ogni correzione sul **sector vero** si fa **dopo l'uscita
del ciclo 2611** (in vigore dal 29 ottobre 2026): il resto del secondo giro R-10, l'ordine a blocchi nel file (B1), il
passaggio al repo vero e il ruleset. Fino ad allora si lavora sul fork di prova e sul Lab.

## 18 ottobre 2026, sera: F5 in corso (Aurora a lotti)

La **F5** (carta [`2026-10-18-f5-aurora-a-lotti.md`](../feature/2026-10-18-f5-aurora-a-lotti.md), approvata il 18
ottobre: D1 la **vetrina**, D2 gli esiti ❌ solo nel Lab, D3 la tabella «dove guardare» la scrive l'agente). Aurora ha
un «secondo sector» (`[SECTOR] SecondSector=` nel suo `Settings\profile.ini`, lo stesso file che tiene l'accesso IVAO:
il Lab ne legge solo quella riga). **Slice 1 fatta** (`1ed29d3d`): la vetrina — una cartella del Lab a cui Aurora punta
una volta; «Porta in Aurora» ci specchia la `SectorFiles` del sector aperto; la barra dice cosa c'è dentro e se Aurora
la guarda; i sector recenti a un clic. Fuori slice (`675c19f6`, chiesto dal committente): la schermata d'apertura con
«Sfoglia…» (la finestra di Windows per le cartelle, dal guscio WinForms) e il campo del repo largo. La vetrina è
**provata dal committente sul suo PC, la sera del 18: funziona** (slice 1 chiusa). **Slice 2 fatta, il lotto**: i
gesti salvati da «visto in Aurora», ognuno coi suoi file e con dove guardarlo (tabella proposta, da confermare), «da
portare» finché non vanno in vetrina e poi «da vedere»; il conto nella barra, «Visto in Aurora», un file per sector nei
dati del Lab. **Slice 3 fatta, gli esiti**: ✅/❌ con due parole accanto a ogni voce in vetrina; «Visto in Aurora»
toglie le ✅ e le non segnate, le ❌ restano come note sul record (nella scheda e in cima ai Problemi) finché non si
tolgono. **Slice 4 fatta, le prove come lotti**: «Prepara la prova» fa un ramo `prova/…` con la variante (solo locale,
non si manda) e mette le domande nel lotto; le risposte restano come esiti della prova; «Butta la prova» torna al ramo
di prima. La prima: l'ordine a blocchi delle aerovie (B9). Slice 2, 3 e 4 da provare sul PC del committente.
**Prossima: slice 5, il giro vero** (un lotto del secondo giro R-10 guardato in Aurora).

## Dove siamo — 18 ottobre 2026: la F4 è fatta

La **F4, git nel Lab**, è fatta (carta [`2026-10-17-f4-git-nel-lab.md`](../feature/2026-10-17-f4-git-nel-lab.md), traccia §7):
il ramo nella barra, «Prendi il sector», i rami di lavoro `aod/<VID>/…` e il ramo del ciclo, «Registra», «Manda» con
la pagina della PR, il changelog proposto e scritto, il riporto nel ciclo e la fine del ciclo, le cancellazioni,
«Allinea». Provata davvero sul fork di prova: nove PR fuse dal committente, fra cui le prime correzioni R-10
(140 errori e 1 122 avvisi sul `master` del fork, erano 141 e 1 244). Il repo vero non è stato toccato. Codice
solo del Lab (`Vipi.SectorLab.Core/Git`, la sessione, il riquadro del ramo). Resta il giro di prove del lotto.

## 17 ottobre 2026: la slice 17 è fatta, il lotto aspetta il giro del committente

La prova T3 in Aurora (esito in «file per file» §20): i simboli si scelgono **per ruolo** nelle impostazioni
(linguetta SYMBOLS), da una tavolozza coi 23 simboli del `.sym` nell'ordine del file; la scelta si salva nel profilo
come disegno (`SYMBOLS_<RUOLO>=`). La posizione nel `.sym` non decide nessun ruolo, e le due righe di nome senza `//`
non spostano niente. Da lì la **slice 17** (traccia nella carta del lotto, §6): il `.sym` ha lettore e scrittore, due
avvisi nuovi, l'editor a pixel nella scheda, e la mappa disegna i punti coi simboli di base di Aurora (decisione del
committente), e un simbolo si sposta di un posto alla volta (17d). Resta aperto, e scritto nella carta: i `SYMBOLS_*`
dei profili. Le `//` dei due nomi sono nella lista R-10 delle correzioni dei dati.

Del lotto resta il **giro di prove del committente**, dalla 78 alla 238, in testa a `PROVE.md`. Exe di prova e conti
nel cartellino.

## Dove siamo — 9 ottobre 2026: il lotto «Subito» aspetta il committente

Del lotto (20 slice, 0-19) sono **fatte la 0-16, la 18 e la 19a**; l'ultimo commit di lavoro è `3e659591` (6 ottobre),
poi il ramo è stato allineato a `main` 1.58.0. Restano due cose, tutte e due ferme sul committente:

1. **La slice 17 — simboli** (lettore `.sym`, editor a pixel 13×13; voci T1, T2 della carta «file per file» §20).
   Aspetta la **prova T3 in Aurora**: come Aurora riconosce i simboli di `symbols.sym`, che nel file non hanno un nome
   che lei legga (solo un commento sopra). Il materiale è fuori dal repo, in `IVAO_Test\SectorLab-prova\T3\`:
   `LEGGIMI-T3.md` coi passi T3-1…T3-5, la tavola `simboli-numerati.html` (i 23 simboli col loro numero), e due
   varianti del file — **A** coi disegni del 1° (APT) e del 14° (VOR2) scambiati, **B** con `//` davanti ai due nomi
   che non l'hanno (`AC_comb SEL`, `AC_DUPE`). Dall'esito dipende il disegno dell'editor: se conta la posizione
   (1° = scalo…) l'ordine si mostra e si sposta solo di proposito, col suo numero; se le due righe senza `//` spostano
   la numerazione, aggiungerle è una correzione da fare con cautela.
2. **Il giro di prove del committente** (slice 19b): 153 prove, dalla 78 alla 230, in
   `IVAO_Test\SectorLab-prova\PROVE.md`, con in testa «Il giro del lotto» — le nove cose da guardare in Aurora, le
   tredici da decidere, le scelte dell'agente che aspettano un sì o un no.

Numeri di oggi: motore **839** test, Lab **930**, `Vipi.Infrastructure.Tests` **2 202** (2 184 di `main` più i 18 della
lettura dei tag); sul fork `8cf32c6` round-trip **723/723**, **235 errori e 1 250 avvisi** nel pannello del Lab (1 242
nello strumento). L'eseguibile di prova è quello di `3464e9db`; lo zip dato agli AOD è ancora `f27d4ed4` (slice 0-9).

## Dal 27 settembre al 6 ottobre 2026 — il lotto «Subito», slice per slice

Carta approvata: [`2026-09-27-lotto-subito.md`](../feature/2026-09-27-lotto-subito.md) (20 slice in tre ondate). Fatte
la slice 0 (misure di partenza) e la **slice 1** intera: sintassi dei tag di §M nel motore (1a), tag sui file a una
riga per record (1b), tag dei punti `//@@` in SID e STAR (1c), tag sui file a blocchi — `.artcc`, `.mva`, aerovie coi
`//@@` per tratto, `.tfl`, `.hartcc`/`.lartcc`, `.geo`/`.pol` — coi blocchi che tengono più record (1d), rotte VFR
`.vrt` e prova su tutti i formati (1e). Slice 2 in corso: **2a** commenti in coda (avviso per file, gesto «sposta
sopra», il lettore MVA che ne faceva scrivere 74), **2b** controllo degli `.isc` (file trovati per nome, inclusi
due volte, sotto la sezione sbagliata, vuoti, orfani copia di un altro), **2c** coordinate scritte male con la
correzione proposta e «Correggi la riga» (trovati due punti letti in Asia e in Africa). Test: motore **616**, Lab
**366**. Slice 2 chiusa. Slice 3 (scheda tipizzata) in corso: **3a** descrizioni dei campi per i 24 tipi di record
(nome dell'AOD, significato, editor; 0 campi sconosciuti sul fork), **3b** gli editor (tipo fisso, sì/no, quota,
numero, piste dal `.rw`, scali e posizioni; corretto lo scrittore delle MVA di ACC che metteva la quota al posto del
gruppo; MVA di scalo in sola lettura fino alla slice 15), **3c** il punto coi suggerimenti del master (corretta la
fusione dei campi del motore, che cancellava l'RNAV delle SID quando la riga si allungava), **3d** i metadati del
catalogo di §M nella scheda (1 271 record su 1 271 sul fork), **3e** «+ Nuovo record» che chiede il tipo fisso (negli
`.artcc` etichetta o traccia). Slice 3 chiusa. **Slice 4** (colori, 28 settembre): **4a** i colori nel motore (le
quattro forme del manuale, `colors.def` in tutte, gli schemi `.clr` di Aurora con la notazione di Delphi misurata sulle
coppie dello stesso colore), **4b** la mappa coi colori dello schema scelto (`LIRR_RDR_V1.0.clr` di base, fondo
radar, settori dinamici solo bordo, nei `.geo` vince lo schema — committente —, nei `.tfl`/`.pol` `colors.def`;
corretto l'ordine dei riempimenti di terra, che si rovesciava), **4c** il selettore nella scheda (nomi di
`colors.def` del master, selettore, opacità con l'avviso su Smooth Drawing), **4d** i punti coi simboli del `.sym`
del sector (chiesta dal committente; abbinamento per nome, confermato a schermo salvo i VFR, che usano il rombo di Aurora). Slice 4 chiusa.
**Slice 5** (sequenze di punti e gesti sul record, 28 settembre): **5a** tracciato delle aerovie, vertici MVA e dei
confini come elenchi, «+ in fondo» e «inverti» (che rilegge il file e rifiuta dove i separatori `T;dummy` finirebbero
al posto sbagliato; corretto lo scrittore MVA delle zone di scalo senza riga L), **5b** spezza/unisci come gesto sul
testo riletto dal motore, nelle cinque scritture (riga vuota, `<br>`, `DUMMY`, `BREAK`), **5c** nascondi/mostra (i
record commentati restano nell'elenco, grigi: fra gli altri, dentro, in parte), **5d** la vista a linea dei `.geo`.
Slice 5 chiusa. **Slice 6** (gruppi e blocchi come Aurora, 28 settembre): **6a** sotto il file le voci della finestra di
selezione (confini coi poligoni, MVA con le zone, aerovie coi pezzi, gruppi dei `.geo`/`.pol` sotto un commento, aree
P/R/D), accese e spente sulla mappa anche un poligono solo; **6b** il nome dal commento cambiato dalla scheda e la
regola `NomeMancante` del validatore (564 «senza titolo» nei `.geo`); **6c** il gruppo col nome dal commento passa a
blocco `//@` al primo metadato. Slice 6 chiusa. **Slice 7** («chi lo usa» e rinomina, 28 settembre, divisa in sei
passi): **7a** nella scheda di un punto chi lo usa, per master (e le copie a un decimo di miglio sono lo stesso punto;
VOR e NDB omonimi mostrati tutti e due, decisione del committente); **7b** la rinomina di un punto in una voce sola con
un diff per file, che **chiede** per le righe comuni (decisione del committente), e il «togli» impedito a un punto
usato; **7c** le posizioni (trasferimenti dei `.frq`, teste dei `.tfl`); **7d** i versi di pista (SID, STAR, mappe,
tag; i PAR dei `.cpr` e i commenti dei disegni «da cambiare a mano»); **7e** chi usa un file e i colori di
`colors.def`, solo da vedere (la loro rinomina è per il futuro, decisione del committente); **7f** il nome delle voci
di confini, MVA, aerovie e aree. Sul fork tutto rinominato e annullato con 0 guasti. Slice 7 chiusa. **Slice 8** (famiglie di forme e
gemelli fra tipi diversi, 28-29 settembre): **8a** la stessa forma in più record, trovata dal Lab come anello (inizio e
verso qualsiasi) e mostrata nella scheda, uguali e simili; **8b** la famiglia dichiarata `form=` e l'avviso «copie di
forma diverse» solo per le dichiarate (i settori solo simili non riempiono il pannello); **8c** la forma portata sulle
copie dopo ogni gesto sui vertici, che tengono partenza, verso, chiusura e scrittura, e «Allinea quella» / «Prendi la
sua»; **8d** le linee dei `.geo` come copia e come sorgente (l'erba e il suo confine si seguono), il bordo e il
riempimento che mancano, l'avviso del confine senza erba; **8e** il gemello `.vfi` ↔ `VFR_NASCOSTI.fix` (la
posizione passa, «crea il gemello», la domanda dopo «togli»). Sul fork 0 guasti in ogni misura. Slice 8 chiusa.
**Slice 9** (procedure, 29 settembre): **9a** i controlli (procedura ripetuta, tipo fuori posto, voce di un altro scalo,
pista che lo scalo non ha); **9b** la scheda scrive RNAV e transizione, nel `MAPS` il tipo è «il tasto che accende la
mappa», le voci per pista e tipo come la finestra delle procedure di Aurora e il «+» che mette la procedura nuova nel
gruppo della sua pista; **9c** le chiavi del genere di voce, le scelte chiuse, minimi e pendenza, il fix proposto dal
nome, «a tutta la voce»; **9d** i vincoli dei punti (`//@@`), al passaggio del mouse sulla mappa e mai in Aurora;
**9e** i legami STAR → attesa → avvicinamento → mancato avvicinamento e l'avviso della STAR che finisce dove nessun
avvicinamento passa. Tre difetti del motore trovati scrivendo, tutti di prima (testa `.str` doppia, commento in coda
letto come RNAV, record nuovo che rubava il commento della voce dopo). Slice 9 chiusa; poi, decisione del committente,
**la consegna agli AOD**: lo zip (eseguibile `f27d4ed4` e i `SectorFiles` del fork al commit `8cf32c6`, con le
istruzioni) è stato preparato e provato il 29 settembre, e lo manda il committente. **Slice 10** (NAVAIDS e attese,
29 settembre: 10a-10c) e **slice 11** (OTHER e PREFS, 29 settembre: 11a posizioni, 11b scali e piste, 11c CPDLC, 11d
profili `.cpr` e PAR) chiuse. **Slice 12** (terra, 4 ottobre, dopo l'allineamento a `main` 1.56.0): **12a** i controlli
(ICAO diverso dal file, stand ripetuto o lontano, etichetta lontana dalla taxiway, tipo vuoto o sconosciuto, `.pol`
senza `.geo`); **12b** tipo e slot degli stand nel motore e nella scheda, i metadati di stand e taxiway coi valori
chiusi, tipo e slot proposti dai metadati; **12c** i generi dentro gli strati della mappa (assi, bordi, edifici,
marcature; riempimenti, etichette, stand) e l'ordine di disegno nella scheda del riempimento; **12d** l'ordine di
disegno deciso (quello del fork: la carta «file per file» I3 è corretta), le marcature di una pista che il `.rw` non
ha, la **vista per scalo** nella linguetta «Scali» con le marcature per pista. Slice 12 chiusa, salvo i punti di
startup (R4: dopo la prova in Aurora). **Slice 13** (settori e spazi, dal 4 ottobre): misura sul fork e **13a** — la
testa dei `.tfl` con l'opacità e il filtro facoltativi (le 52 teste a quattro campi di `GCI.tfl` si leggevano come
vertici: un poligono solo al posto di 53), le posizioni separate anche dai due punti in «chi lo usa» e nella rinomina;
**13b** — `SettoreSenzaPosizione` (D4): le 4 posizioni italiane dei settori che nessun `.frq` conosce, sulla testa e
nella scheda; **13c** — limiti verticali e classe coi valori chiusi (`SFC`, `GND`, `1500ft`, `FL195`, `UNL`; classe
A-G); **13d** la copia di un settore (`SettoreRipetuto`: stessa testa e stessa forma); **13e** il file giusto di un
confine, legato al settore dinamico della stessa forma (`ConfineNelFileSbagliato`, sezione «Il confine» nella scheda);
**13f** i nomi delle configurazioni col modello scelto nelle **Impostazioni** dell'app (`NomeDellaConfigurazione`);
**13g** limiti e classe al passaggio del mouse sulla mappa; **13h** le posizioni dei `.frq` senza settore
(`PosizioneSenzaSettore`, 27); **13i** AGL e AMSL nei limiti (`"2000ft AGL"`). Slice 13 chiusa (6 ottobre); resta da
provare in Aurora se i due punti fra le posizioni di una testa valgono. **Slice 14** (ACC e aerovie, dal 6 ottobre):
misura sul fork, **14a** gli avvisi degli `.artcc` (cerchio di un gate non chiuso o fuori dal confine, stanghetta di
un'AOCC, etichetta lontana dal suo fix: sul fork solo il cerchio `X07-X08`), **14b** i controlli delle aerovie (12
senza etichetta, 18 etichette con nomi che non ci sono, 25 lontane dalla loro aerovia), **14c** la scheda dei tratti
di un'aerovia (verso e quote nel tag `//@@` del punto, e sulla mappa), **14d** le etichette calcolate («Sistema le
etichette»: una a metà di ogni tratto di almeno 10 NM — soglia nelle Impostazioni —, nomi giusti, le lontane rifatte;
sul fork 53 nuove, 24 rinominate, 25 tolte), **14e** aggiungere un'aerovia a mano (blocco `locked=si`, un tag per
tratto, le sue etichette; avviso `TrattoSenzaQuote`), **14f** toglierla. Slice 14 chiusa (6 ottobre). **Slice 15**
(MVA di ACC e di scalo, dal 6 ottobre): misura sul fork, **15a** le MVA di scalo lette come quelle di ACC (la quota
è il 5° campo, il 2° è il nome: fino a qui il lettore prendeva il nome per quota, e la scheda era in sola lettura),
i blocchi con più quote riconosciuti e fermi nella scheda, il gruppo sui vertici nuovi di ACC; **15b** i controlli
(`EtichettaFuoriDallaZona` 13, `ZonaSenzaEtichetta` 0, `QuotaNonValida` 0, `QuotaNonInCentinaia` 13,
`GruppoMancanteNellaMva` 104 — i separatori `DUMMY`, con «Correggi tutte» —, `MvaNonDelloScalo` 24); **15c** la quota
nella scheda, sempre in centinaia, coi valori speciali e il significato. Slice 15 chiusa (6 ottobre); non fatti e
detti: spezza/unisci nelle MVA di ACC, i tratti proposti intorno all'etichetta (adozione). **Slice 16** (VFR, dal 6
ottobre): misura sul fork, **16a** la rotta militare (il 5° campo delle righe di un `.vrt`, che il modello non
conosceva: ora si legge, si scrive su ogni riga e ha la sua casella), **16b** i controlli dei punti VFR contro i fix
nascosti (`GemelloVfrMancante` 6, `GemelloVfrDiverso` 5, `FixNascostoSenzaPunto` 1, `CodiceVfrRipetuto` 2,
`CodiceVfrFuoriPosto` 6 con «Correggi tutte», `RottaMilitareAMeta` 0), **16c** il punto nuovo col codice proposto, in
ordine di codice, e il tipo che può restare non scritto, **16d** i tratti delle rotte VFR (verso e quote nel tag
`//@@`, come le aerovie), **16e** i punti di una rotta proposti dal `.vfi` dello scalo e poi da quelli vicini.
🔴 Trovato a schermo: una modifica al tag di un punto (vincoli di SID e STAR, tratti) non si annullava dalle
modifiche — corretto. Slice 16 chiusa (6 ottobre); non fatte e dette: le strutture `[VFRENR]` e `[VFRRTEENR]` (sul
fork nessun file). Decisioni del 6 ottobre: i punti senza codice restano senza avviso; dei cinque gemelli diversi
è giusto il fix (**16f**: `GemelloVfrDiverso` porta la riga col punto dov'è il fix). **Slice 18** (ATIS e D-ATIS, 6
ottobre, fatta prima della 17 che aspetta la prova T3 in Aurora): **18a** il motore — i `.datis` letti come gli
`.atis`, il lettore dei `.fds`, `ModelloAtis` (testo, segnaposto, parti facoltative, parentesi che non tornano) e
quattro controlli (`ParentesiNonBilanciate` 1: `default.atis`, il modello di 102 posizioni; `SegnapostoSconosciuto`,
`CampoDellAtisMaiUsato`, `AtisEDatisDiversi` 0); **18b** l'editor del modello (il testo fatto a pezzi, i segnaposto
da scegliere che entrano al cursore); **18c** l'anteprima coi valori d'esempio e «Ascolta» con una voce di Windows;
**18d** il D-ATIS accanto all'ATIS, con le differenze. Non fatti e detti: un METAR vero, come Aurora trasforma
lettera, ora e METAR prima di leggerli, quale voce usa. Slice 18 chiusa (6 ottobre). Test: motore **839**, Lab
**930**; albero 235 errori, 1 250 avvisi. **Slice 19** (la prova del lotto, dal 6 ottobre): **19a** la lettura di
prova dei tag da parte di vIPI — 231 file del fork etichettati dal motore e riletti da `AuroraSectorfileParser`: gli
stessi 7 860 oggetti di prima, e tutte le 1 516 voci di SID e le 863 di STAR ritrovano i loro tag per nome; nessun
cambio al sito, 18 test in più in `Vipi.Infrastructure.Tests` (2 101) e la misura 8 dello strumento; **19b** il giro
delle prove preparato in testa a `PROVE.md` (153 prove, le cose da guardare in Aurora e da decidere, i conti in alto
riallineati). Restano: il **giro del committente** e la **slice 17** (simboli, dopo la prova T3). Il dettaglio di
ogni passo sta in §6
«Traccia» della carta. I conteggi dei test si scrivono a mano finché il
cancello di `main` rifiuta `tools/conta-test.sh`.

## 27 settembre 2026 — revisione del giro dei file

Prima del lotto «Subito» il committente ha chiesto di rileggere tutto il giro e le decisioni: cosa è stato valutato
per un file e non per il gemello, e quali metadati ci sono. Trovato: gli esempi di tag della carta erano in quattro
forme e il motore ne legge una (alcune avrebbero spezzato le STAR). Deciso: **una sintassi e un catalogo** (carta
«file per file» §M), vIPI legge i metadati e non ne propone (supera carta madre §8.2), i generatori portano i loro
parametri (§M-G), metadati nuovi per settori, pista (TORA dagli intermedi, circuito, limiti d'uso), IAP, SID, rotte
VFR, taxiway. Proposte ancora da confermare e la lista delle correzioni dei dati: §R. Il «nomi con spazi nelle
composte» qui sotto è chiuso da §M regola 5. Nessun codice toccato.

## Dove siamo — 26 settembre 2026 (il committente chiude la chat e riorganizza il lavoro)

**Nessun codice in sospeso.** `lab/f3` pulito e spinto; ultimo codice `e06d6f20` (CI verde), dopo solo documenti.
Test: motore **479**, Lab **358**. Eseguibile di prova ripubblicato il 24 settembre alle 12:54 (= `e06d6f20`).
Il 26 settembre, all'apertura della chat «vIPI Lab», `lab/f3` si è allineato a `main` (1.46.5 compresa: `d670d606`).

1. **Prove a mano di F3 e F3-bis: FINITE, 52 su 52 ✅** (`SectorLab-prova\PROVE.md`). L'ultima correzione (prove 40-41):
   un fix nuovo va nella sezione del suo NOME (`OrdineAlfabetico.NellaSezioneGiusta`, prefisso più lungo), non in quella
   del record da cui si parte; anche «+ Nuovo record» `BD100` era rotto e nessun test lo guardava.
2. **Giro del sector «file per file»: FINITO** (24-26 settembre). Carta
   [`2026-09-24-file-per-file.md`](../feature/2026-09-24-file-per-file.md): **§1-§22** = ogni cartella e ogni tipo di file
   della radice `IT`, ciascuno con formato (dal manuale IVAO), misure sul fork, decisioni del committente, fase di ogni
   esigenza (Subito / F4 / F6 / F7 / F8 / F9 / Da pensare); **§C** = i meccanismi comuni (chi li chiede).
   Metodo: struttura dal manuale **prima** (wiki `SectorFile_Definition`, leggibile col browser del pannello; ricerca
   pagine con GraphQL `/graphql` `pages.search`), poi misure, poi le domande; **non** passare da soli al file dopo.
3. **Prossimo passo — da proporre al committente, NON ancora cominciato**: il **lotto «Subito»** = tutte le voci
   «Subito» delle §1-§22 raggruppate per meccanismo comune (§C), in slice, con un ordine; come carta a parte collegata
   alla «file per file», da far leggere prima di cominciare. Il committente ha detto: tutte le cartelle prima, poi si
   accorpa.
4. **Prove in Aurora da fare presto, in rami di prova** (le fa il committente): ordine dei simboli di `symbols.sym`
   (T3: le 2 righe senza `//` spostano la numerazione?); startup col tasto HOLD nel `MAPS` (R4); etichette `L` fra i
   tracciati `T` delle aerovie (B9); etichette ACC per riferimento `L;ABDAB;ABDAB;ABDAB;8;` (A8); `ENRVFI` sotto
   `[VFRFIX]` (F5); organizzazioni dei file di scalo A-E (I7, K2; il `.geo` NON è fra i file caricati da sé secondo il
   manuale, un esempio dice il contrario).
5. **Pulizie decise, in un ramo (F4)**: `limw.pol` (residuo del 2020), `test.artcc`, `limc_star`/`lirf_star` (AOD);
   `.fix` vuoti più avanti; `LIMM_WN4_CTR`/`LIMM_EN4_CTR` dai trasferimenti; SID ripetute (11) e campi spostati (28);
   ICAO sbagliati (`LINB`, `L3MC`, `L4MC`, stand LIBP in `libg.gts`); `update.ini` **tolto** dalle cose da generare.
6. **Regole nuove per tutto il Lab** (committente): 🔴 niente commenti `//` in coda a una riga (Aurora la legge in circa
   il doppio del tempo; 713 righe oggi) · 🔴 dati SOLO da fonte primaria (DB IVAO, PDF dell'AIP), **mai da vIPI** ·
   🔴 un `.cpr` in `PREFS` sovrascrive l'utente a ogni connessione (solo lo stretto necessario) · rami di prova, niente
   file parassiti · coordinate col punto.
7. **Da chiedere**: le `LL NW/NE/S1/S2` di `lied.str` (rotte militari a bassa quota?) a chi segue LIED; composizione
   delle configurazioni di Milano 2.1/2.2/3.
8. **La consegna** (slice 11 di F3) resta decisa dal committente.

Script di misura (fuori repo, riutilizzabili): scratchpad della sessione `079dd998-…\scratchpad\acc\` (`geo.py` = base
con `dms`/`nm`; uno script per cartella: `aw.py`, `tfl*.py`, `mva*.py`, `geo*.py`, `pol.py`, `airspace.py`,
`navaids.py`, `other.py`, `cpr.py`, `sid*.py`, `star.py`, `maps.py`, `terra.py`, `vrt.py`, `simboli*.py`…) e
`…\scratchpad\strumenti\` (ATIS Creator e SYMBOL Creator IVAO, **non eseguiti**). 🔴 Script Python con Write, non
heredoc (le `\` si perdono). PDF AIP: `vIPI Ivao Italy\RealDOCS\` (fuori da git), `pdftotext -layout` li legge.

🟡 Nel clone di prova `it-aurora-sector-test` git vede cancellato `IT\changelog.md` (non l'ha fatto il Lab): chiedere
al committente prima della prossima prova.

## Prima — 24 settembre 2026, 11:30

**In corso: le prove a mano del committente** (slice 6 di F3-bis, più le sue richieste man mano). `lab/f3` è pulito,
spinto, **CI verde** su `27a70daf`. Test: motore **479** (net8 e net10), Lab **356**.

- **Eseguibile di prova**: `D:\Programmazione\IVAO_Test\SectorLab-prova\VipiSectorLab.exe`, ripubblicato il 24 settembre
  alle 11:30 (tutto `lab/f3`). Il piano è `SectorLab-prova\PROVE.md`, **51 prove**, con in testa la tabella dello stato:
  ✅ 1-5, 8-12 · 🔧 corrette da rifare (35, 37-38, 40-41, 43, 45, 47, 48, 51) · ⏳ 13-51. Ripubblicare dopo ogni correzione:
  `dotnet publish src/Vipi.SectorLab -c Release -r win-x64 --self-contained -o D:\Programmazione\IVAO_Test\SectorLab-prova`,
  poi `VipiSectorLab.exe --autoprova --cartella <clone>` (esito 0). 🔴 Prima: `Get-Process VipiSectorLab` — il
  committente tiene spesso l'app aperta, e le DLL in uso non si sovrascrivono: si committa e si ripubblica quando la chiude.
- **Sector di prova**: il clone `D:\Programmazione\IVAO_Test\it-aurora-sector-test`, con le prove SALVATE del committente
  (`APT.fix`: `BC;518`, `BC420`; `lirn.str`: ATZ a N041; `twrs.tfl`: LIRN_TWR incollato). Servono alle prove 37-38, 43, 47;
  poi lui fa `git checkout -- .`.
- **Banco per vedere la UI** (fuori repo): scratchpad della sessione `ba2d7208-…\scratchpad\vetrina` — progetto
  `Microsoft.NET.Sdk.Web` con `RequiresAspNetWebAssets` e `OutputType Exe` (senza, `blazor.web.js` va a 404), che apre il
  clone e stampa l'indirizzo col segreto; si apre nel browser del pannello. Si ferma per riga di comando
  (`vetrina.dll`), 🔴 mai `taskkill /IM dotnet.exe` (ferma anche i processi degli altri).
- **Da discutere col committente**: l'ordine dei record e cosa vuol dire «aggiungere un elemento», **file per file** (oggi
  in ordine alfabetico solo i punti col nome: fix, VOR, NDB, VFR); l'impostazione «salva il tavolo di lavoro all'uscita»
  (oggi le larghezze delle colonne si ricordano sempre); i nomi di procedura con spazi nelle composte.
- **La consegna** (slice 11 di F3: workflow `sectorlab-v*`, zip + SHA-256, scheda `/services`) **la decide il
  committente**. Prima vuole qualcosa da presentare agli AOD (è nel team AOD: copre ~80% dei bisogni, il resto dopo).

### F3 — l'app

Carta [`2026-09-22-f3-l-app.md`](../feature/2026-09-22-f3-l-app.md): slice 0-10 fatte, resta la 11 (consegna, vedi sopra).
La scheda `/services` entra nel sito: quella parte della consegna la fa l'integratore.

### F3-bis — copie gemelle e mappe composte

Carta [`2026-09-23-f3-bis-copie-e-mappe-composte.md`](../feature/2026-09-23-f3-bis-copie-e-mappe-composte.md): il §8
«Traccia» ha numeri e scoperte di ogni slice.

| Slice | Cosa | Stato |
|---|---|---|
| 0 | Misure sul fork | ✅ gemelli 3/13/1; 58 aggregati, 16 già disallineati; D7 chiusa (l'8º campo è `RNAV`) |
| 1 | Gemelli nel motore, regola `CopieDiverse` | ✅ 17 chiavi sul fork |
| 2 | Una modifica va anche sulle copie gemelle | ✅ una voce, più diff; «allinea anche questo» |
| 3 | Tag fra virgolette, chiave `composta`, `<br>` nel modello | ✅ 545/545 `<br>`, 2782/2782 tag |
| 4 | Rigenerazione delle mappe composte, due regole del validatore | ✅ `lime.str`: STAR spostata → −2 +2 |
| 5 | «Composta da» nella scheda | ✅ «Composta da quello che disegna oggi»: 20/55 aggregati col solo tag |
| 6 | Prove a mano del committente, Aurora compresa | 🟡 in corso (vedi sopra) |

Decisioni del committente: D1-D10 in carta §5. Due **riviste** dopo le misure: **D4** (le virgolette restano, anche
se i nomi con spazi si leggevano già) e **D8** (per mappa: troncate di norma, `intere=si` le disegna intere).

### Correzioni dalle prove del committente (23 settembre)

- `ad26a3b4` i record di un file si vedono **sotto** il file (erano in fondo alla colonna, fuori schermo).
- `bea96c8c` togliere la scelta di un file **non chiude** più la sua cartella.
- STAR «(ALL)» spezzate al `<br>` in `Geometria` (si ricollegavano l'una all'altra: `lirn.str` STAR 06(ALL)).
- Zoom della mappa 16 → **20**, coordinate per la mappa a 6 decimali (~11 cm).
- **Due schermi**: tasto «Pannelli in un'altra finestra» → `/pannelli` (Sfoglia/Problemi, scheda, modifiche) in una
  seconda finestra del guscio (`FinestraDeiPannelli`), sull'altro schermo; la principale tiene mappa e strati. 🟡 Il
  guscio a due finestre l'ha provato solo bUnit: la prova vera sono la 25-27 del committente.

### Richieste della sera del 23 settembre (a–f) e prova 5 — PROVE.md 28-36

- **Le due finestre insieme** (`FinestreInsieme`): cliccata l'una, l'altra risale subito dietro (`SetWindowPos` senza
  attivare). Non con `Owner`: la posseduta starebbe sempre sopra, e con uno schermo i pannelli coprirebbero la mappa.
- **Colonne ridimensionabili**: divisori `data-divide` (sectorlab.js, un ascoltatore sul documento), larghezza in una
  variabile `--lab-l-<nome>` su `<html>`, ricordata nel localStorage della WebView2 (doppio clic = di base). 🟡 Il
  committente vuole, con le future impostazioni, l'interruttore «salva il tavolo di lavoro all'uscita»: oggi si
  ricorda sempre.
- **Tema scuro + brand IVAO**: il Lab NON seguiva il brand (Segoe UI, `#0b5cad`). Ora token del sito (`vipi-theme.css`:
  atmos/ocean/fuselage/semantic), Poppins/Nunito Sans/IBM Plex Mono serviti da `wwwroot/fonts/`, barra blu IVAO.
  Automatico/chiaro/scuro (`sectorlab-tema.js` nel `<head>`); i colori degli strati sono `--lab-strato-<id>` e la mappa
  li rilegge al cambio di tema.
- **Annulla/ripeti** (`SessioneDelLab.NellaStoria`/`Annulla`/`Ripeti`, tasti ↶ ↷ e Ctrl+Z/Ctrl+Y fuori dai campi):
  annullare = tutto com'era all'apertura (`AnnullaTutto` + `Modifiche = new()`) e si rigiocano i gesti tranne l'ultimo.
  Dopo un salvataggio o una rilettura la storia riparte. 🔴 Preso strada facendo: la mappa aveva UNA versione della
  geometria e ridisegnava solo l'ultimo strato toccato (un gesto sulle copie gemelle ne tocca più d'uno; anche il cambio
  di master non ridisegnava niente) → `VersioneDelloStrato`.
- **Anteprima dell'incolla** (`Core/Modifiche/TestoDaIncollare`, la STESSA lettura dell'incolla): nella scheda
  (`AnteprimaDelDisegno`, SVG: oggi grigio, nuova verde tratteggiata, centri) e sulla mappa; segue testo e densità a
  ogni scatto. Il «°» della densità non va più a capo.
- Test Lab 306 → **327**. Eseguibile ripubblicato alle 20:55.

### Prove del 24 settembre mattina (a, b, 6, 7, 10) — PROVE.md 37-44

Esiti: 6 ok ma ordine, 7 ✗, 8 ✅, 9 ✅, 10 ✗.

- **10 ✗ → corretto**: le zone degli `.str` erano **aree** e Leaflet le chiudeva; Aurora le disegna come **linee**
  (l'ATZ di LIRN col primo punto sbagliato restava aperta in Aurora e chiusa da noi). Ora `Geometria`: linea.
- **7 ✗ + (b)**: la riga salvata `BC;518;…` riletta non è più un record (riga illeggibile) → dal pannello Problemi si
  vedeva e non si correggeva. Ora si **scrive una riga a mano** (`RigheAMano`, clic → Invio → conferma):
  `ModificheInSospeso.CambiaRiga` prende le righe di ADESSO, cambia quella, rilegge il file col motore
  (`IFileConRecord.LeggiLeRighe`) e lo mette come nuova STRUTTURA (`ModificaDelTesto`, si annulla come
  `ModificaDiStruttura`: file dell'apertura). Quel che pendeva sul file entra nel testo (i campi si rimettono sui record
  prima, niente si annulla negli altri file). 🔴 Le righe `//@` hanno il lucchetto, e non se ne scrivono di nuove.
  Anche: Ctrl+Z in un campo già confermato ora va al Lab (dopo Invio il cursore resta lì e il gesto «non tornava»).
- **6 → ordine alfabetico**: `OrdineAlfabetico` (per ora fix, VOR, NDB, punti VFR) — il nuovo chiede il NOME e va al suo
  posto nella SEZIONE (record fra due commenti: `//LIBC`, `//LIBD` di `APT.fix`); primo della sezione = sotto
  l'intestazione (motore: `RecordNuovo.AggiungiPrimaDi`, i `//@` restano al vicino). **+ Nuovo record** anche dal file.
  L'ordine degli altri tipi: da decidere col committente file per file.
- **(a) la vista**: «◎ Solo questo sulla mappa» (record) e «◎ Solo questo file» (Sfoglia), elenco «In vista» sotto gli
  strati, «mostra tutto». JS `sectorlab.mappa.vista`: toglie i gruppi degli strati e ne fa uno con le sole forme scelte
  (niente fetch in più); le coste restano. 🟡 Le chiavi sono per indice: aggiungere un record prima di uno in vista lo
  sposta (da rivedere se dà fastidio).
- Test Lab 327 → **346**, motore 471 → **474**. Eseguibile ripubblicato 24 settembre 09:23.

- 24 settembre, dopo: 🔴 «clic sulla riga 30, si apre la 29». I problemi dell'ALBERO numerano le righe del DISCO; con un
  record aggiunto sopra (prova 40) nel file di adesso la riga è una più in giù. Ora `VaiAlProblema` traduce col diff
  (`Diff.Allinea`, `ModificheInSospeso.RigheDellApertura`, `SessioneDelLab.RigaDiAdesso`) e la vista del problema
  mostra le righe di ADESSO. Controllo su tutto il fork: numeri della scheda = righe del file (`NumeriDiRigaTests`,
  `SECTORLAB_ALBERO_VERO=<clone>` per l'albero vero). Lab 348.

- 24 settembre, «chiudi la forma»: l'incolla ora ha la casella (parte da com'è la forma di oggi, `ElencoChiuso`); la
  chiusura sta in `TestoDaIncollare.Leggi(…, chiudi)`, una sola lettura per anteprima e incolla. Lab 351. Ripubblicato
  10:14 (PROVE.md 45-46).

- 24 settembre, regola **`FormaQuasiChiusa`** (avviso, codice comune `Validazione/Validatore.cs`): primo e ultimo punto di
  settori `.tfl`, zone `.str`, MVA e poligoni che scritti differiscono per UNA cifra (sopra i 100 m). Misurata sul fork:
  «a meno di mezzo miglio» dava 367 avvisi (settori con l'ultimo lato corto), «sotto i 100 m» 1 598 (arrotondamenti);
  la sola cifra dà **1**: `lirn.str:63` LIRN ATZ, il refuso della prova 10. Motore 479. PROVE.md 47.

- 24 settembre, osservazioni a–c (prove 10-12 ✅): 🔴 **(c)** SID/STAR/punti non sparivano spegnendo la casella: scegliere
  un record accende il suo strato e fa partire più `Cambiata` di fila → due `Sincronizza` insieme chiedevano lo stesso
  strato due volte (lo segnavano disegnato solo DOPO la fetch) → due gruppi, la casella spegneva solo il secondo. Ora
  `Mappa.InFila` (una sincronizzazione alla volta) e il JS non fa mai due gruppi (`inArrivo`, `giro`, `voluti`). Il test
  bUnit con una fetch che non finisce è rosso sul codice di prima. **(a)** archi a 5° di base (`GradiPerPuntoDiBase`), la
  stima dal file resta come indicazione. **(b)** `Sezione.razor`: titoli che chiudono/aprono, stato per CHIAVE nel Lab
  (`SezioneAperta`/`ApriOChiudi`). Lab 355.

- 24 settembre: 🔴 «Solo questo sulla mappa» svuotava la mappa. `InvokeVoidAsync("…vista", string[])`: lo string[] per
  covarianza diventa l'object[] degli ARGOMENTI → una chiave per argomento, il JS riceveva una stringa. Ora `(object)`;
  test bUnit sulla forma della chiamata (rosso senza). I test di prima guardavano la sessione, non la chiamata. Lab 356.

### Aperto, da chiedere o dire al committente

- ✅ **Nomi di procedura con spazi** nelle composte: chiuso con la §M (regola 5): nell'elenco si scrivono fra virgolette
  (`25:"RNP10 UPETI"`, `Metadati.ScriviLElenco`); il Lab rifiuta solo un nome con spazi in testa o in coda o con le
  virgolette dentro (`NomeElencabile`). Verificato il 18 ottobre: la nota «aperto» era rimasta indietro.
- 🟡 **Errore nei dati del fork**: `limf.sid:28` `LIMF18;TOP1B LAG2L; ; ;0;LAGEN;` (manca il `;` fra `LIMF` e `18`,
  dal commit «LIMF: Revisione SIDs»). Il validatore oggi non lo dice: ora è la P3 della carta «file per file» (§16).
- Tutti gli altri aperti del giro dei file (26 settembre) stanno nella carta «file per file», voce per voce con lo
  stato (🟡 = da provare o da confermare).
- **Aperti del lotto «Subito» (9 ottobre)**: l'elenco che fa fede è «Il giro del lotto» in testa a `PROVE.md` (nove
  cose da guardare in Aurora, tredici da decidere). In fondo a ogni slice della carta del lotto (§6) stanno le
  scelte dell'agente da confermare e quel che non è stato fatto, detto al committente.
- 🟡 **Dati del fork da correggere** (R-10, in un ramo, quando lo dice il committente) emersi dalle slice 13-18:
  `default.atis` con una `]` in più (102 posizioni); `lipa.vfi` 3-6 con nome e codice scambiati, `lict.vfi:8` e
  `liph.vfi:2` col codice in coda al nome, `lips.vfi:3` con un punto che si chiama «.»; i codici doppi `MJNW1` e
  `PKS1`, il fix nascosto `PRNW5` senza punto, sei punti VFR senza gemello, cinque gemelli da portare sul fix; 104
  separatori `T;DUMMY` senza gruppo nelle MVA di ACC, 13 quote MVA piene, `lipx.mva:14` in gradi decimali; il
  cerchio `X07-X08` aperto; le etichette delle aerovie (53 da aggiungere, 24 da rinominare, 25 lontane). Ognuno ha
  il suo avviso nel Lab, quasi tutti con «Correggi».

### Codice comune toccato (per l'integratore, alla fusione)

`Vipi.Sectorfile`: `Validazione/CopieGemelle.cs`, `Regola.CopieDiverse`, `Regola.CompostaConProceduraAssente`,
`Regola.CompostaNonAllineata`; `IO/Metadati.cs` (virgolette, `composta`, `intere`, `Togli`, `NomeElencabile`);
`IO/MappeComposte.cs`; `StrRecord`/`StrParser`/`StrSaver` (`IniziaUnTratto`); `IO/RecordNuovo.AggiungiPrimaDi` (24 set); `Regola.FormaQuasiChiusa` (24 set). Lotto «Subito» slice 8: `Regola.FormeDiverse` e `Regola.ConfineSenzaErba` (le calcola il Lab). Slice 9: `Validazione/ControlloDelleProcedure.cs` (4 regole), `Validazione/LegamiDelleProcedure.cs` e `Regola.StarSenzaAvvicinamento`; `SidProcedure.IsRnav`, `StrRecord.TipoNonScritto`, `StrParser.Rnav`, `StrSaver` fino all'8° campo; `FusioneDelRecord` (il commento in coda resta in coda); `RecordNuovo.Aggiungi` (la coda del vicino passa sotto il nuovo); `Metadati.RigheDeiPunti`. Slice 10: `Fix.NomeDellAttesa`, `Vor.CanaleTacan`/`NomeDellAttesa`, `Ndb.Visibilita`/`ExtraField6`/`ExtraField7`/`NomeDellAttesa`, lettori e scrittori dei NAVAIDS, `IO/Savers/CampiFacoltativi.cs`; `Validazione/ControlloDelleAttese.cs`, `CampiDelNavaid`, sette regole (`AttesaNonDefinita`, `AttesaMaiCitata`, `AttesaFuoriPosto`, `AtteseNonCaricate`, `NomeInPiuCataloghi`, `CampoMancante`, `ValoreFuoriElenco`), `Validatore.Metri` internal; `Attesa.Fix`/`Rotta`/`Verso`/`Quota` scrivibili. Slice 11: `AtcPosition.Loa`/`Inclusi`/`Esclusi`, `FrqParser`/`FrqSaver` (7° campo = `.loa`), `Runway.RottaVeraDalleSoglie`, `CorrezioneDelleCoordinate` per i `.rw`, lettori e scrittori nuovi `.cpdlc`/`.cpdlcnames`/`.cpr` (`MessaggioCpdlc`, `NomeDelGruppoCpdlc`, `ImpostazioneDelProfilo`), `Validazione/ControlloDellePosizioni.cs`, `ControlloDelCpdlc.cs`, `ControlloDeiProfili.cs`, quattordici regole nuove, `CopieGemelle` (non confronta i campi derivati). Slice 12a: `Validazione/ControlloDellaTerra.cs` e sei regole (`ScaloDiversoDalFile`, `LontanoDalloScalo`, `StandRipetuto`, `EtichettaLontanaDallaTaxiway`, `TipoSconosciuto`, `RiempimentoSenzaDisegno`). Slice 12b: `Stand.Type`/`Stand.Slot`, `GtsParser`/`GtsSaver` (5° e 6° campo), `Models/Airport/SlotDelloStand.cs`, `EsitoDelFile.Chiavi`, `Regola.StandPiuGrandeDellaTaxiway`. Slice 12d: `Shared/OrdineDeiRiempimenti.cs`, `Regola.OrdineDiDisegno`, `Regola.MarcaturaDiUnaPistaAssente`, `ControlloDellaTerra.PisteCitate`. Slice 13a: `TflSector.SenzaOpacita`/`Filtro`/`Posizioni()`/`Statico`, `TflParser` (testa da quattro campi), `TflSaver` (opacità e filtro facoltativi). Slice 13b: `Validazione/ControlloDeiSettori.cs`, `Regola.SettoreSenzaPosizione`. Slice 13d-13f: `Regola.SettoreRipetuto` (in `ControlloDeiSettori`), `Regola.ConfineNelFileSbagliato` e `Regola.NomeDellaConfigurazione` (le calcola il Lab). Slice 13h: `Regola.PosizioneSenzaSettore`, `ControlloDeiSettori.Di` prende i `.frq` coi loro file. Slice 14a-14b: `Validazione/ControlloDegliAcc.cs` (`CerchioNonChiuso`, `CentroFuoriDalConfine`, `StanghettaDellAocc`, `EtichettaLontanaDalFix`), `Validazione/ControlloDelleAerovie.cs` (`AeroviaSenzaEtichetta`, `EtichettaDiUnAeroviaAssente`, `EtichettaLontanaDallAerovia`). Slice 14d-14e: `IO/RigheDelleAerovie.cs` (le aerovie lette dalle righe), `Shared/Distanze.cs`, `Regola.TrattoSenzaQuote` (la calcola il Lab). Slice 15a: `MvaParser` (di scalo la quota dal 5° campo), `MvaSaver` (il nome nel 2° campo, il gruppo sui vertici nuovi di ACC), `MvaSector.EtichetteDiverse`, `MvaVertex.LettoSenzaGruppo`. Slice 15b: `Validazione/ControlloDelleMva.cs` (`EtichettaFuoriDallaZona`, `ZonaSenzaEtichetta`, `QuotaNonValida`, `QuotaNonInCentinaia`, `GruppoMancanteNellaMva`, `MvaNonDelloScalo`). Slice 16a: `RottaVfr.Militare`/`MilitareAMeta`, `VrtParser` (5° campo), `VrtSaver` (`;;1;` su ogni riga di una rotta militare). Slice 16b: `Validazione/ControlloDeiVfr.cs` (`GemelloVfrMancante`, `GemelloVfrDiverso`, `FixNascostoSenzaPunto`, `CodiceVfrRipetuto`, `CodiceVfrFuoriPosto`, `RottaMilitareAMeta`; `EUnCodice`, `ENascosti`). Slice 18a: `IO/ModelloAtis.cs`, `FdsParser`/`FdsSaver`, `Models/Airport/CampoDellAtis.cs`, `Formati` (`.datis`, `.fds`), `Validazione/ControlloDegliAtis.cs` (`SegnapostoSconosciuto`, `ParentesiNonBilanciate`, `CampoDellAtisMaiUsato`, `AtisEDatisDiversi`), `FileVuoto` non vale per i `.datis`. Slice 19a: `tools/Vipi.SectorfileProva/Concordanza.cs` (`DeiTag`, `TagDiUnSid`) e `Program.cs` (misura 8), `tests/Vipi.Infrastructure.Tests/ConcordanzaSectorfileTests.cs` (18 test): nessun file di `src/` del sito. Slice 17a: `Models/Simbolo.cs`, `SymParser` (lettore coi chunk)/`SymSaver`, `Formati` (`.sym`), `Validazione/ControlloDeiSimboli.cs` (`NomeDelSimboloSenzaBarre`, `SimboloSenzaNome`), il commento di `Shared/SimboloDelSector.cs`; `tools/Vipi.SectorfileProva/Program.cs` (la riga dei simboli). `tools/Vipi.SectorfileProva` (sezione
5b, `composta` sulle MAPS). Il sito non usa niente di questo; la build della soluzione è verde.

### Dove sta la storia di prima

Fino al 26 settembre la storia del filone stava anche nella memoria dell'agente; ora sta solo qui e nelle carte:
F2 → carta [`2026-09-22-f2-motore-del-sector.md`](../feature/2026-09-22-f2-motore-del-sector.md) e §A115; F3 slice
0-10 (una riga per slice, con lo sha) → `docs/lavori-aperti.md` §A116 e §8 «Traccia» della
[carta F3](../feature/2026-09-22-f3-l-app.md); F3-bis → §8 della sua carta e la tabella sopra; giro dei file → carta
«file per file».

### Lezioni del filone che le carte non dicono

- 🔴 Una **proposta** dell'agente non si scrive mai come «decisa dal committente» (successo per D8-D10 di F3-bis,
  corretto subito): le decisioni hanno la data e la parola del committente.
- 🔴 bUnit: un attributo `bool` vero diventa un attributo **vuoto** → nei `data-*` si usano stringhe (`"si"`/`null`).
- 🔴 CI su Ubuntu più lenta: le attese dei test bUnit a 15 s (`Attesa`), sennò rossi a tempo scaduto.
- 🔴 `Punto` ha una conversione implicita da `Coordinate`: in un ternario servono i due `(object)`, sennò cade ogni
  `.pol`/`.lairway` (l'ha presa la misura sull'albero, non i test).
- 🔴 Sorgenti con `\r\n` dentro le stringhe e percorsi Windows nei documenti: solo con gli strumenti di modifica, mai
  Python o sed passati da heredoc (un `\v` di `scratchpad\vetrina` era diventato un carattere di controllo qui sopra).

- 🔴 Un componente della scheda con soli parametri primitivi (file e record) **non si ridisegna** col padre: dopo
  «Annulla tutto» mostrava ancora la famiglia (8b). Si iscrive da sé a `Lab.Cambiata`, come la scheda.
- 🔴 La mappa cuce i segmenti dei `.geo` senza guardare commenti e righe vuote; la linea del file (5d) sì. Chi confronta
  forme lo sa: 34 linee su 5 599 sul fork non coincidono (8d).
- 🔴 Una scrittura che nessuna misura faceva (la testa di una procedura) nasconde difetti vecchi: prima di offrire un
  campo nuovo, misurarlo su **ogni** record del fork (9b: «una testa per procedura», 2 811 righe; prima della correzione
  `limc.sid` usciva con 16 righe in più).
- 🔴 Il lettore degli `.str` lascia nelle righe di una voce la riga vuota e il commento della voce DOPO: chi inserisce
  dopo una voce deve spostare quella coda (9b, `RecordNuovo.Aggiungi`).
- 🔴 Un campo della scheda che è il NOME di un punto citato non è un campo come gli altri: scritto lì, cambiava il nome
  senza riscrivere le citazioni e senza guardare i doppioni (trovato nella slice 10). Il nome di un punto citato passa
  da «Rinomina».
- 🔴 Riscrivere una linea può cambiarne il numero di segmenti: i record dopo di lei, nel suo file, slittano. Più
  scritture nello stesso file si fanno dall'ultima alla prima, e la scelta segue il suo record (8d, `liaa.geo`).

- 🔴 I numeri di una carta si rimisurano prima di scrivere la regola: nella slice 12 due non si ritrovavano (43
  poligoni con meno di 3 vertici: erano 2; 3 `.pol` senza `.geo`: nessuno) e l'ordine di disegno «deciso» lo seguivano
  16 file su 93 — il committente ha corretto la carta, non il sector.
- 🔴 Un albero di prova che usa i campioni del motore ha già gli scali veri (LIRF, LIBP): uno scalo inventato con le
  stesse coordinate perde contro quello vero in ogni controllo «il più vicino» (12d: i test della vista per scalo).
- 🔴 Il lettore dei `.rw` conta come piste solo le righe sotto `//PISTE`: un `.rw` di prova senza quell'intestazione
  non ha piste, e il controllo che le usa tace (12d).
- 🔴 Il banco a schermo tiene bloccate le DLL del Lab: si ferma (`Stop-Process -Name banco`) prima di ricompilare.

Dalle slice 13-19 (6 ottobre):

- 🔴 Una scelta degli AOD che può cambiare va nelle **Impostazioni dell'app** (`ImpostazioniDelLab.razor`, un file per
  impostazione fra i dati del Lab), non nel codice: il modello dei nomi delle configurazioni, la soglia delle
  etichette. Le regole che ne dipendono le calcola il Lab (`ValidaLAlberoAsync`), non il motore.
- Un gesto su un FILE intero è una sostituzione di righe (`Modifiche.CambiaRighe` dentro `GestoSulTesto` e
  `NellaStoria`: una voce, un annulla), e la funzione che fa il piano è pura (`EtichetteDelleAerovie`, `AerovieAMano`).
  Dopo aver TOLTO record, `Scegli(null, 0)`: la scheda mostrava il record che ne aveva preso il numero.
- 🔴 Un controllo che legge le righe non chiude un pezzo a una riga di tag (`//@`, `//@@`): la 14b lo faceva, e
  scritto il verso di un tratto le etichette di quell'aerovia diventavano «lontane».
- 🔴 Una misura in Python si confronta con quel che legge il MOTORE: le coordinate in gradi decimali (`lipx.mva:14`)
  e senza punti (`libv.mva`) lo script non le leggeva, e contava una zona senza etichetta che non c'era (15b).
- 🔴 Una regola con la riga corretta si misura sul fork prima di fidarsi: `IP31;PL;` di `lipl.vfi` sembrava un
  codice scambiato col nome, ed è un punto che si chiama così (16b).
- 🔴 Il pannello dei problemi disegna i primi duecento: un test a schermo che clicca un problema prima filtra per
  regola, sennò cade quando una slice aggiunge avvisi ai campioni (15b).
- 🔴 I test del Lab che passano dal catalogo dei nomi (mappa, suggerimenti) vogliono scali veri dell'`.ap` dei
  campioni (`lira`, `liru`, `licc`…): il master carica da sé solo i file degli scali che dichiara (16e).
- 🔴 Il banco trova quel che i test non vedono — il tipo che teneva spento «Aggiungi» nei `.vfi`, l'annulla dei tag
  dei punti rotto dalla 9d: ogni gesto nuovo si prova lì fino in fondo, **annulla compreso**.
- Un campo che non si scrive per QUEL record: `SiScriveSe` + `PercheNo` nella descrizione, e il rifiuto sta anche in
  `ModificheInSospeso.Cambia`. Una domanda del record nuovo che può restare senza risposta:
  `SceltaDelTipo.Facoltativo`. La voce di modifica del tag di un punto ha per chiave `@@N.chiave` e un `Nome` a parte.
- Razor: un componente che richiama sé stesso disegna le parti annidate (`PezziDelModello`); uno `switch` col markup
  nei `case` funziona; `parola@(…)` è preso per un'email, serve `<span>@…</span>`; niente stringhe fra virgolette
  dentro un attributo, si passa da un metodo. `Sezione` mette i `data-*` sul titolo, non sul contenitore.
- 🔴 La voce di «Ascolta» (`Ui/Servizi/Voce.cs`) si prova scrivendo la lettura in un `.wav` (`ScriviSuFile`); sul banco
  il tasto non si preme: parlerebbe dalle casse del committente.
- 🔴 I conti «in alto» scritti in una prova invecchiano a ogni slice che aggiunge avvisi: quando il totale cambia si
  riallineano tutti in `PROVE.md`, e le domande 🟡 che hanno avuto risposta si segnano ✅ (19b: dieci prove su 153).
- 🔴 Uno script con apostrofi, backslash o `\n` dentro le stringhe va in un file, mai in un heredoc della shell.

### Dove lavorare

- Conteggi del filone: `tests/conteggi/Vipi.SectorLab.Tests.txt`, `Vipi.Sectorfile.Tests.txt` se si tocca il motore,
  `Vipi.Infrastructure.Tests.txt` se si tocca la concordanza (`tools/Vipi.SectorfileProva/Concordanza.cs` è compilato
  anche lì). Si scrivono a mano dai «Passed!» del log, nello stesso commit dei test: il cancello di `main` rifiuta
  `bash tools/conta-test.sh` da questa cartella, e la CI li ricontrolla.
- Prova sull'albero intero: `dotnet run --project tools/Vipi.SectorfileProva -c Release -- <…\SectorFiles\Include\IT>`.
  Dura più di dieci minuti: in background, da sola, una volta a fine slice. Esce 1 per il solo `limf.sid` (il nome
  vuoto alla riga 28). Attese di oggi: round-trip 723 esatti e 25 senza lettore, tutto toccato 0, una modifica per
  record 115 620, una testa per procedura 2 811, tag 118 521, punti 38 330, blocchi 107 256, 235 errori e 1 242
  avvisi, tag letti da vIPI 231 file con 0 cambiati e 2 379 procedure coi loro tag.
- **Il solo validatore, in tre secondi** (per misurare una regola mentre la si scrive): un progetto console di venti
  righe che referenzia `Vipi.Sectorfile` e chiama `Validatore.ValidaLAlbero(<fork>\SectorFiles)`, stampando i conti
  per regola e, per le regole chieste, ogni problema con la sua proposta. Sta nello scratchpad dell'agente
  (`valida`), fuori dal repo: se non c'è più si riscrive.
- **Il banco a schermo**: un progetto `Sdk.Web` di dieci righe che avvia `ServerDelLab` su una copia del fork e
  scrive l'indirizzo d'ingresso in un file; si apre col browser del pannello e si guida leggendo il DOM. Anche lui
  nello scratchpad (`banco` e `copia`); la copia dev'essere pari al fork, file vuoti compresi.
  - Dal 20 ottobre: argomenti `<copia o «-»> <cartella dei dati> <file dell'indirizzo>`; con «-» parte senza sector
    (il primo avvio). La cartella dei dati si prepara a mano coi file che il Lab ricorda (`aip-espanso.txt`,
    `cartella-di-aurora.txt`, `cartella-di-lavoro.txt`…). Il banco **non** passa la cartella di Aurora di base:
    nei suoi campi resta vuota (nell'app è `%APPDATA%\IVAO\AuroraWindows`).
  - Il pannello va emulato a 1400 px (`resize_window`) per vedere la mappa; poi si rimette «desktop».
  - 🔴 Il cancello rifiuta GitHub nel browser anche come **testo da scrivere** in un campo del banco: le prese dei repo
    sul banco si provano con un clone locale come «server»; i comandi git veri dal terminale.
  - Si spegne solo per riga di comando: processi con «banco» e la cartella dei dati del banco.
- L'eseguibile di prova: `dotnet publish src/Vipi.SectorLab -c Release -r win-x64 --self-contained -o
  <IVAO_Test>\SectorLab-prova`, poi `VipiSectorLab.exe --autoprova` (esito 0; dal 20 ottobre l'autoprova usa
  `%TEMP%\VipiSectorLab-autoprova` e non tocca i dati del committente). Prima si guarda che il Lab non sia aperto
  (`Get-Process VipiSectorLab`): è un programma che il committente usa; se è aperto si aspetta che lo chiuda.
- `SectorLab-prova\PrimoAvvio\`: il primo avvio simulato (`LEGGIMI.txt`, il `.cmd` che lancia con `--dati`), fuori
  repo; per rifarlo da zero il committente cancella `dati` e la cartella di lavoro.
- **Dal giro del 18 ottobre** (F5 e dopo):
  - Sul banco **«Manda» non si preme**: `ApriNelBrowser` è `init` e di base apre il browser di Windows del committente.
    Il push del ramo e la PR si fanno da riga di comando (`git push -u origin aod/…`, `gh pr create --repo
    SkyMistery/it-aurora-sector-test`), come per la PR #9.
  - Un clone del fork per il banco: `git clone -c core.autocrlf=false <fork> fork`; per una copia fatta clone a mano,
    `ColorSchemes/` in `.git/info/exclude` (nel fork vero sono ignorate dal `.gitignore`).
  - Il clone del committente (`it-aurora-sector-test`) è **a un ramo solo** (`remote.origin.fetch` = solo master): per
    questo il Lab ora chiede sempre tutti i rami (`RamiDelSector.ChiediAlServer`).
  - Il cancello rifiuta i comandi git con la cartella in una variabile: percorsi scritti per esteso.
  - Gli script Python passati con heredoc dalla shell perdono le barre rovesciate (`"\\r\\n"` diventa un a capo vero):
    per testo con `\` si usa l'editor, o `chr(92)`.
  - Il manuale IVAO («SectorFile Definition», `wiki.ivao.aero/en/home/devops/manuals/SectorFile_Definition`) al
    recupero automatico risponde 403: si legge col browser del pannello (`get_page_text`, o il testo del `body`).
  - Dati veri del committente in sola lettura: il lotto in `%LOCALAPPDATA%\VipiSectorLab\lotti\`, il registro in
    `…\VipiSectorLab\log\`; dei profili di Aurora solo le righe che servono (mai `Settings\profile.ini` intero).
- **Per la F6**: i PDF del ciclo 2609 in `vIPI Ivao Italy\RealDOCS\` (156 file, fuori dal repo); i prototipi della F0
  (Python) in `vIPI Ivao Italy\sector-lab-f0\` (`pdf-aip\enr2.py` la lettura delle tabelle, `airac\checklist.py` la
  Cover, `airac\inventario.py` e `confronto.py`); il lettore delle geometrie della F1 in
  `src/Vipi.Application/Coordinates/` (`AipGeometryReader`, `CoordinateParser`).
  - 🔴 **La CI dopo ogni push si guarda** (`gh run list -b lab/f3`): il 18 ottobre è rimasta rossa per cinque commit
    (da `847ac950`) senza che me ne accorgessi — `Path.GetFileName("ENRVFI\limm.vfi")` su Linux non separa la barra
    rovesciata. I percorsi scritti negli `.isc` si spezzano a mano (`LastIndexOfAny(['\\', '/'])`).
- **Dal giro della F6 (18-19 ottobre)**:
  - **Le misure sui PDF veri** (fuori dalla CI): `dotnet run --project tools/Aip.Pdf.Cli -- <cartella o pdf>… --modello
    <uscita.json> [--aree] [--dettagli]`; poi `tools/Aip.Aurora.Cli` per le righe di Aurora. Le **controprove** con
    pdfplumber (Python) stanno nello scratchpad della sessione `a9ccef1c` (`py\confronta.py` = ENR 5.1 contro
    `sector-lab-f0\pdf-aip\out2.json`; `py\controprova.py` = coordinate per file; `py\perpagmod.py`, `py\doppi.py`);
    se mancano si rifanno: il conto atteso è nella carta F6 §6 (ENR 5.1: 541 aree e 2 207 coordinate come la F0; ENR 2:
    329 aree, ENR 2.1.1.4.1 483 coordinate).
  - **Il confronto dei CTR sul sector vero** si misura con una prova temporanea in `tests/Vipi.SectorLab.Tests` (apre
    il sector con `SessioneDelLab.ApriAsync`, legge il modello, chiama `ConfrontoDelleForme.Ctr`, scrive un file): **non
    si committa**. Attesi sul sector della copia: 45 CTR, 39 uguali, 6 diversi.
  - **Il banco della F6** (scratchpad `a9ccef1c`): `banco` (lo stesso Sdk.Web), `dati-banco` (cartella dei PDF =
    `pdf-prova`, con ENR 5.1, ENR 2 e un ENR 3.2; AIP espanso = `aip-espanso-repo`, il clone del repo di prova sul ramo
    `aip/2610`), il sector = la copia della sessione `1ae3d543` (`scratchpad\sector`), letto e basta. Sul banco
    **«Prendi i PDF da Download» non si preme** (sposterebbe la Download vera) e **«Manda» nemmeno** (apre il browser
    del committente): il ramo `aip/…` si manda da riga di comando, col percorso **scritto per intero** (il cancello
    rifiuta `git -C "$VARIABILE"`), e la PR con `gh pr create --repo SkyMistery/it-aip-espanso-test`.
  - 🔴 **Python via heredoc trasforma `\b` in un backspace vero** (non solo perde le barre): un'espressione regolare
    di `PageInfo` è andata nel file con un carattere di controllo, e le date fuori ciclo non si leggevano. Dopo ogni
    modifica fatta così: `grep -cP '[\x00-\x08]' <file>`. Meglio uno script scritto con l'editor e lanciato da lì.
  - bUnit: `Find` e `Click` **nello stesso** `InvokeAsync` (un ridisegno in mezzo rinumera i gestori); e un riquadro
    che all'apertura avvisa la pagina senza che sia cambiato niente fa perdere i clic agli altri test (`DueSchermiTests`).
  - Il font standard di PdfPig (per i PDF finti dei test) non ha «°»: nei test i gradi sono «º», che il lettore accetta.
- **Dal giro della F6 del 19 ottobre** (slice 1c, ATZ, 2a, mappa, confini, 2b, 2c):
  - **Le misure sul sector vero** si fanno con una prova temporanea in `tests/Vipi.SectorLab.Tests/PdfAip/`
    (`MisuraTemporanea…cs`: `SessioneDelLab.ApriAsync` sulla copia, `CambiaLAipEspanso` sul clone dell'AIP espanso, i
    gesti in memoria, un file di uscita nello scratchpad), **mai committata** e mai «Salva». Attesi oggi sulla copia: CTR
    39 uguali e 6 diversi; ATZ negli `.str` 34 uguali; in `twrs.tfl` 27 uguali e 4 diverse; rotte 21/3/5; attese 51/3/191
    (105 di scalo)/14; 44 tratti di confine su 44 su `itgeo.geo`.
  - **I PDF di ENR 3** (29 rotte di ENR 3.2, ENR 3.4.1 e 3.4.2) stanno in `RealDOCS\ENR\ENR 3\`; sul banco sono copiati
    in `pdf-prova\ENR 3\`. pdfplumber gira con `python` (`C:\Python314`), non con `py`.
  - Il **banco** si riavvia dopo ogni modifica a un `.razor` (niente ricarica a caldo); per un CSS basta ricaricare la pagina. Il pannello del browser
    a volte non disegna (finestra dietro): il clic sulla tela della mappa si manda dalla pagina (`elementFromPoint` e i
    `PointerEvent`), alle coordinate trovate cercando il colore nei pixel.
  - Un componente **senza parametri** non si ridisegna quando cambia la scheda (memoria
    `componente-senza-parametri-non-si-ridisegna`): `AipNellaScheda` prende la voce come parametro.
  - Gli **script Python** che modificano file si scrivono con l'editor in un file e si lanciano da lì: in un heredoc
    `"\r\n"` diventa un a capo vero dentro le stringhe C# (successo due volte). E si guarda che il file non resti con
    fine riga misti (`file <percorso>`: «CRLF, LF»). Il cancello di `main` legge anche il testo degli script: uno che
    contiene le parole dell'invio al server viene rifiutato anche se non invia niente — testo da aggiungere ai documenti
    con l'editor.
  - Un `git add` con un percorso che non c'è più (un file rinominato con `git mv`) fallisce **tutto**: il commit parte coi
    soli rinomini (`773f1081`). Dopo ogni commit `git show --stat HEAD`, prima di mandarlo.
  - Il Lab aperto dal committente blocca la ripubblicazione dell'eseguibile: `Get-Process VipiSectorLab` prima, e se c'è
    si chiede di chiuderlo.
- **Dalla slice 3a (19 ottobre, sera)**:
  - Le sonde di ENR 4 stanno nello scratchpad della sessione `68c4e350` (`py\celle.py`: le celle fra i bordi orizzontali e
    verticali di una pagina; `py\controprova4.py`: le coordinate di ENR 4.4 e 4.1 contro il modello). Attesi sul 2609:
    1 254 punti, 55 radioassistenze (GRO senza coordinate), 11 luci.
  - Una sezione «che non si legge ancora» nei test è ora ENR 6: chi aggiunge ENR 6 cambia `PdfFintoTests` e
    `PdfAipTests`.
  - Sul banco, col pannello del browser nascosto, `innerText` torna vuoto: l'esito si legge con
    `textContent`.
  - **Scegliere una voce sulla mappa del banco** (19 ottobre, correzioni slice 3): il pannello a 1 600 × 1 000
    (`resize_window`), accesi solo il genere che serve, il pixel del suo colore cercato nella tela (`getImageData`), e il
    clic vero alle coordinate della schermata (che è a metà scala). Poi `resize_window` «desktop». I campi di un modulo
    si riempiono meglio dalla pagina (`value` + evento `change`) che col clic: un clic fuori posto scrive nel campo
    accanto (è successo con «Copertura»).
- **Dalle slice 3b-5a (19 ottobre)**:
  - **Attesi sulla copia del sector** contro l'AIP espanso 2610 (per riconoscere un cambio): radioassistenze 53 uguali, 2
    diverse (ELB, PNZ), 93 solo nel sector; punti 1 254 uguali, 2 solo in `itfix.fix`, 4 «uso diverso»; aree di ENR 5
    (coi PDF di ENR 5.2.2) 424 uguali, 91 diverse, 206 mancanti, 3 solo nel sector. AIP espanso 2610 dopo la PR #7: 1 088
    aree, 29 rotte, 413 attese, 55 radioassistenze, 1 254 punti, 11 luci, 5 scali, da 112 PDF letti su 144.
  - **La cartella dei PDF del banco** (`pdf-prova` nello scratchpad `a9ccef1c`) ha ora anche `ENR 4`, `ENR 5.2` e `AD 2`
    (i 35 PDF dei 5 scali), copiati da `RealDOCS`.
  - **Le sonde** nello scratchpad `68c4e350`: `py\controprova52.py` (le coordinate di ENR 5.2.1 e 5.2.2.5),
    `py\controprova_ad2.py` (frequenze e distanze di AD 2), `py\testo.py` e `py\celle.py`. Per vedere come `FindAll` e
    `RowBands` tagliano una pagina c'è stata una sonda in `tests/Aip.Pdf.Tests` (`SondaTemporaneaAd2.cs`: le griglie, i
    titoli e le righe di ogni pagina in un file), **mai committata**: si riscrive in dieci righe.
  - **Le misure sul sector** con una prova temporanea in `tests/Vipi.SectorLab.Tests/PdfAip/` (`MisuraTemporaneaEnr4.cs`,
    `MisuraTemporaneaEnr5.cs`: `SessioneAperta.Apri` o `SessioneDelLab.ApriAsync` sulla copia, il confronto, un file nello
    scratchpad), **mai committata**.
  - **Attesi della 5b** sulla copia contro il 2610: scali 13 uguali; piste 13 uguali e 8 diverse; frequenze 30 uguali e 8
    mancanti; ATZ negli `.str` 38 uguali e 2 diverse (Viterbo, Orio). La misura: `MisuraTemporaneaAd2.cs`, mai committata.
  - **AD 2.19** (slice 5d): la controprova sta nello scratchpad `260c92db` (`controprova_ad219.py`, gira con
    `python`, non con `python -I`, che non vede pdfplumber); la sonda delle griglie (`SondaTemporaneaAd219.cs` in
    `tests/Aip.Pdf.Tests`) si riscrive in venti righe. Attesi sul 2609: 29 radioassistenze, 12 ILS, 47 apparati.
  - **L'importer finito** (19 ottobre): le controprove nello scratchpad `260c92db` (`controprova342.py`,
    `controprova53.py`, `controprova_smac.py`, `controprova_ad219.py`), con `python` (non `-I`). La cartella dei PDF del
    banco ha ora anche `ENR 5.3` (148 PDF). Attesi sul 2609: ENR 3.4.2 129 attese, ENR 5.2.2.4 8 corridoi, ENR 5.3 31
    pericoli, SMAC 78 settori; attese nel confronto 62/6/228/0.
  - Gli **script di modifica** dei file CRLF (`src/Aip/Routes.cs`): sostituire con `\n` non trova niente; lo script
    converte i pezzi a `\r\n` se il file li ha. E in una stringa `@"…"` di C# le virgolette si raddoppiano.
  - Il **diff dell'AIP espanso**: GitHub (e `git diff` di base) mostra un'aggiunta in mezzo come righe spostate (PR #6:
    «+3445 −1967»); con `git diff --diff-algorithm=histogram` si vede se sono solo aggiunte. Il ramo `aip/2610` dopo la
    fusione di una PR viene cancellato su GitHub: il push lo ricrea, la PR porta solo il commit nuovo.
  - Sul banco **un clic che riscrive il riquadro** (Porta, Sostituisci) si legge dopo il ridisegno: aspettare qualche
    secondo, o rileggere, prima di dire che non è andato (ALBA Corridor, 19 ottobre).
  - Gli **script di modifica** con molte barre rovesciate o virgolette: scritti con l'editor in un file `.py` e lanciati da
    lì (un heredoc lungo si è rotto con «unexpected EOF»); dopo, `grep -cP '[\x00-\x08]' <file>`.
  - **Rossi intermittenti della CI**: `RamoNellaBarraTests.Manda…` con `index.lock` di git (`1cb9eacd`): non è la slice;
    il commit dopo è verde.
  - **La mappa nel pannello del browser** non si è lasciata guardare (un puntino, lo zoom non la riporta): quel che si vede
    della mappa si controlla dall'elenco degli strati (i conti per tipo) e coi test, e si dice.
