# Le curve delle procedure (31 ottobre 2026)

Filone Sector Lab. Segue [«Le segnalazioni del 2611»](2026-10-23-segnalazioni-2611.md) (giro del 26 ottobre: le «SID
dalla soglia») e il «Da vedere dopo» di [F7, SID e STAR](2026-10-19-f7-sid-e-star.md) (le virate delle convenzionali).
**Approvata** il 31 ottobre sera (§4, D1-D4). R6 e R7 (§7) approvate e fatte il 1 novembre sera (§8).

## §0 — La domanda

Il committente, 31 ottobre, fra i lavori aperti del Lab: «Curva dopo il decollo». Sono due cose che si toccano:
- le **25 SID «dalla soglia»** del giro del 26 ottobre: dopo il decollo c'è una virata grande che non si disegna;
- le **virate delle convenzionali** (F7, «Da vedere dopo», 20 ottobre): fra un tratto e l'altro c'è uno spigolo, mentre il
  sector disegnato a mano arrotonda.

La voce «466 frasi senza tratti» del 22 ottobre non c'è più: dopo i giri del 23-26 ottobre la prosa legge 970 procedure e
ne restano non lette 12, tutte per AJO (un dato che manca, non il lettore).

## §1 — Com'è oggi (misurato sul 2611 completo, 31 ottobre)

Sonda della prosa su `lab/f3` @ `9c9e0146`, 851 PDF: segnalazioni **287** (di procedura 232), procedure lette dalla
prosa **970**, non lette **12** (AJO). Come il 26 ottobre: le S1-S5 non hanno cambiato la prosa.

**Il disegno.** Le procedure si disegnano in due posti, con la stessa regola (i punti in fila, a segmenti dritti; curve
solo per gli archi DME `AF` e i raccordi `RF`, un punto ogni 5°):
- la mappa del Lab: `AipSullaMappa.Percorso`;
- le righe di `.str`/`.sid` di «Porta nel sector»: `ProcedureNelSector.Corpo`.

Nei tratti letti dalla prosa: CF 884, IF 762, CD 289, TF 252, FD 250, AF 161, CR 144, DF 99, CA 53, VA 45, VR 24, VD 24,
RF 8. Hanno il verso della virata (`Turn`) 1 080 tratti.

**Le 25 SID dalla soglia** sono tutte virate vere dopo il decollo, fra 64° e 180° dalla pista; non c'è un errore di
lettura. Oggi il lettore della prosa (`ItalianAd2Prose`, B6) fa partire la rotta dalla **soglia** della pista, come se il
decollo fosse già su quella rotta, e lo dice:

| Scalo | Pista | SID | Virata | Velocità della prosa |
|---|---|---|---|---|
| LICR | 33/29 | BERMI 6A, RCA 1R, RCA 2Y (dalla salita: TR 164°); PAZZE 2A (TR 014°) | 85°-165° | IAS MAX 200 kt |
| LIEA | 20 | SUPUX 7A (TR 268°, «Initial climb procedure completed turn right») | 64° | IAS MAX 210 kt |
| LIMF | 36 | SIRLO 8A (TR 137°) | 135° | — |
| LIML | 17 | TOVSA 8C, TREVI 9D (TR 108°) | 67° | — |
| LIPH | 07 | ALBET 7F, INBUT 5A, ROKIB 5A, ROVIG 5A, VIC 7F (prua 281°) | 144° | IAS MAX 200 kt |
| LIPO | 32 | CIBFU 6A, OSKOR 7W (TR 124°), LICFE 6B (TR 109°), OSKOR 6X (TR 132°), UVNIB 1X (TR 216°) | 96°-180° | — |
| LIRQ | 05 | BIBNO 1H, GAVRA 7H, KUGIX 7H, LOMED 7H, MAREL 8H, OKMAP 7H (dalla salita: «as soon as possible turn left on TR 237°», «within 3 NM PRT DME») | 168° | IAS MAX 140 kt |
| LIRS | 03L | GIKIN 5Z (prua 160°) | 131° | — |

Esempio, LIRQ dalla 05: la salita dice «as soon as possible turn left on TR 237° until intercepting and following RDL 267
PRT VOR», la virata «completely performed within 3 NM PRT DME», «IAS MAX 140 KT». Oggi la rotta 237° parte dalla soglia
della 05, cioè passa sopra la pista all'indietro; nella realtà l'aereo decolla verso 050°, vira a sinistra di 168° e la
rotta 237° comincia dove la virata finisce, spostata di circa due raggi.

Restano fuori, per altri motivi: le salite di LIPR («then leave RIM VOR on RDL 269 until …») e il ramo 4 di LIEE RWY 32
(il PDF mescola il testo con l'italiano).

## §2 — Che cosa cambia e che cosa no

- **I punti dell'AIP non cambiano.** Un punto col nome, o calcolato dal testo (un incrocio di radiali, una distanza DME),
  resta dove l'AIP lo mette. Il confronto col sector li usa come oggi.
- **Cambia il disegno**: le virate fra due tratti diventano curve (R1-R2), nella mappa del Lab e nelle righe portate nel
  sector. Tocca solo il Lab (`Vipi.SectorLab.Core`).
- **Cambia il lettore comune** solo per le 25 SID (R3): il primo tratto non parte più dalla soglia ma dalla fine della
  virata. Si sposta il punto calcolato dove la rotta incrocia la prima radiale, quando c'è. È `Aip.Pdf.Italia`, che usa
  anche ATCSim: lo dice il cartellino e il messaggio del commit.

## §3 — Le regole (proposte)

**R1 — Il raggio.** Il raggio della virata viene dalla velocità e dall'inclinazione: r = v² / (g · tan φ).
- La velocità è la «IAS MAX» della prosa sul tratto, o nella sua nota «during turn», quando c'è; altrimenti 210 kt per le
  SID e 220 kt per le STAR e gli avvicinamenti.
- L'inclinazione è 25°. Fa circa 1,4 NM a 210 kt, 0,6 NM a 140 kt (LIRQ). Senza vento: la IAS si prende come velocità
  al suolo, come per le prue (decisione del 23 ottobre); si dice una volta sola, nella nota della lettura.

**R2 — Le virate fra due tratti (fly-by).** Dove due tratti dritti si incontrano a un punto, l'arco tangente ai due con
il raggio di R1, nel verso del secondo tratto (`Turn`) o, se manca, nel verso più corto.
- Un punto **fly-over** (`FlyOver`) si sorvola: la virata comincia sul punto e poi si va diritti al punto dopo.
- Sotto i 5° non si arrotonda niente.
- Una virata **oltre i 120°** non è più un raccordo: l'arco tangente starebbe lontano dal punto (a 150° oltre 4 NM). Si
  disegna come un fly-over (si sorvola, poi si vira).
- Un'**intercettazione** («turn right to join RDL 273 … bound to SUGEP»): si vira fino alla rotta della radiale, poi la si
  segue fino al punto. Se l'arco non ci sta (la radiale è troppo vicina) si va diritti, come oggi, e si dice.
- Gli archi DME (`AF`) e i raccordi `RF` restano come sono: l'arco dell'AIP comanda.

**R3 — La virata dopo il decollo (le 25).** Il decollo si disegna lungo la pista fino alla **fine della pista** (la soglia
opposta, già usata dal passo 5). Da lì la virata, nel verso detto dalla prosa e col raggio di R1, fino alla rotta. La
rotta comincia dove la virata finisce, e da lì il lettore calcola l'incrocio con la prima radiale (oggi lo calcola dalla
soglia).
- Se la prosa dice un limite («within 3 NM PRT DME»), si controlla: se la virata disegnata ne esce, si dice.
- La nota della lettura «la virata dopo il decollo non è disegnata» non serve più: resta la nota del raggio (R1).

**R4 — Quanti punti.** Una curva è un punto ogni 5°, come gli archi DME. Nel sector sono righe di coordinate, come le
convenzionali disegnate a mano.

**R5 — Il confronto col sector.** Oggi il confronto accetta che il sector passi fino a 2 NM da un punto calcolato
(`SogliaDeiCalcolati`), perché il sector arrotonda: su LICA le virate verso l'arco DME anticipano di 1,1-1,7 NM. Con R2
anche le procedure portate dal Lab arrotondano, e una virata di 120° col raggio di 1,4 NM passa a 1,4 NM dallo spigolo:
ci sta. Oltre i 120° R2 sorvola il punto, quindi la soglia regge. Da misurare sui sector del banco (§5); se non regge, il
confronto misura la distanza dalla curva disegnata invece che dal punto.

## §4 — Domande per il committente

1. **Il raggio**: 25° di inclinazione e 210/220 kt quando la prosa non dice la velocità (la proposta di F7)? O un raggio
   unico per tutte?
2. **Oltre i 120° come un fly-over** (R2): va bene, o si arrotonda comunque?
3. **R3 nel lettore comune**: va bene spostare i punti calcolati delle 25 SID (cambia anche per ATCSim)? L'alternativa è
   solo il disegno: la curva dalla fine pista fino alla rotta, e la rotta che resta agganciata alla soglia (allora la
   curva e la rotta non si toccano, e si vede uno scalino di circa due raggi).
4. **Le SID e le STAR già nel sector**: R2 vale anche per le RNAV portate dal Lab (TF fra punti con nome), o solo per le
   convenzionali lette dalla prosa? La proposta: solo la prosa. Le RNAV hanno i loro `RF` dove serve e il sector le
   disegna a punti.

**Le risposte del committente (31 ottobre, sera)**: «1) Si, va bene 2) Sorvoliamo 3) Si 4) Solo quelle dalla prosa, le
RNAV lasciamole come sono». Quindi:
- **D1**: raggio da 25° di inclinazione; IAS MAX della prosa, altrimenti 210 kt (SID) e 220 kt (STAR, avvicinamenti).
- **D2**: oltre i 120° si sorvola il punto, poi si vira.
- **D3**: R3 nel lettore comune: i punti calcolati delle 25 SID si spostano (anche per ATCSim).
- **D4**: le curve solo per le procedure lette dalla prosa (`LegsFromDescription`); le RNAV restano come sono.

## §4-bis — Fatto (31 ottobre, notte)

**Decisioni mie, prese facendo** (si possono cambiare):
- **Un punto col nome si sorvola sempre**, anche sotto i 120°: la linea ci passa e poi vira. Nel sector la riga del punto
  (`SUGEP;SUGEP;`) deve restare, perché Aurora ne scrive il nome, le quote `//@@` stanno sopra e il confronto ritrova i punti
  per nome; e una convenzionale vira sulla stazione. Il raccordo (fly-by) vale per i punti calcolati.
- **L'intercettazione** (R2) quando il tratto dopo è un `CF` con la sua rotta vera e il punto dopo non sta diritto davanti
  (oltre 3°): virata nel verso detto fino alla prua a 45° dalla linea, diritti, raccordo di 45° sulla linea. Se la linea si
  prenderebbe dopo il punto, si va diritti come prima. Per saperlo il lettore ora scrive la rotta vera nei `CF` e nei
  punti calcolati (`TrueCourse`, campo che il modello aveva già; prima vuoto per la prosa).
- **Un tratto corto stringe il raggio**: l'arco non comincia prima di metà del tratto più corto.

**Dove sta**
- `Core/PdfAip/CurveDelleProcedure.cs`: raggio (R1), velocità (colonna, nota «IAS MAX … during turn», 210/220), raccordo,
  sorvolo, intercettazione; un punto ogni 5°. La usano `AipSullaMappa.Percorso` (mappa) e `ProcedureNelSector.Corpo` (righe
  del sector); le procedure che non sono della prosa passano com'erano (D4).
- `Aip.Pdf.Italia/ItalianAd2Prose.cs`, `VirataDalDecollo` (R3): `CA` (o `VA`) alla fine della pista, `RF` col centro
  calcolato e il raggio della prima IAS MAX del testo, poi la rotta da lì. Se il testo dice «within N NM XXX DME», si
  controlla che la virata ci stia. **Codice comune** (lo usa ATCSim): due tratti nuovi in testa alle 24 SID, e i loro punti
  calcolati spostati; `TrueCourse` nei `CF`.

**Misurato sul 2611** (sonda, `prosa-c2.json` contro `prosa-c1.json`)
- Procedure lette dalla prosa 970 → **971** (la salita iniziale di LIRQ RWY 05 ora ha i suoi tratti); non lette 12 (AJO);
  segnalazioni 287 (le 24 note «non è disegnata» diventano «è disegnata dalla fine della pista a … kt»).
- **24 SID su 25** partono dalla fine pista con la virata: LICR 33/29 (3), LIEA 20, LIMF 36, LIML 17 (2), LIPH 07 (5),
  LIPO 32 (5), LIRQ 05 (6), LIRS 03L. Cinque prima avevano un punto solo (LIMF SIRLO 8A, LIPO LICFE 6B e UVNIB 1X, LIRS
  GIKIN 5Z, LIPO CIBFU 6A dall'incrocio): ora hanno il percorso.
- Resta **LICR PAZZE 2A RWY 29**: per questa SID il lettore non ha la fine della pista (il perché non l'ho ancora
  guardato), quindi parte dalla soglia come prima, con la sua nota.
- **LIRQ dalla 05**: la IAS MAX 140 kt e «within 3 NM PRT DME» stanno nelle REMARKS della salita, fuori dal testo del
  percorso: la virata è disegnata a 210 kt (1,4 NM). Da guardare con il committente.

**Test**: importer 354 → **356** (la SID che vira dopo il decollo, con e senza IAS MAX); Lab 1 515 → **1 527** (le curve:
raggio, velocità, raccordo, sorvolo, oltre 120°, verso della prosa, tratto corto, punto dentro il cerchio,
intercettazione, rotta diritta; la STAR portata con la curva che torna «uguale» al confronto). Aip 54, Aip.Aurora 38
invariati.

**A schermo sul banco**: LICA SUGEP 3G (lo screenshot della T4): la virata di ritorno sul punto RDL 115/16 NM LMT è un
arco, non uno spigolo. Il banco ha l'AIP espanso letto prima di R3: le 24 SID nuove si vedono dopo «Rileggi».

## §5 — Finita quando

- Sul 2611: le 25 SID partono dalla fine pista con la virata; nessuna nota «virata dopo il decollo non disegnata»; le
  procedure lette restano 970.
- Sulla mappa del Lab e nelle righe portate: curve al posto degli spigoli nelle convenzionali, con la regola di R2. Il
  conto delle curve disegnate, di quelle fatte come fly-over e delle intercettazioni rimaste diritte, scritto qui.
- Il confronto col sector: sui sector del banco, nessuna procedura che era «uguale» diventa «diversa».
- A schermo sul banco: LIRQ dalla 05, LIPO dalla 32, LICR dalla 33, LIPH dalla 07, e la SUGE3G di LIRN accanto alla
  SUGE2E del sector (lo screenshot della T4).

## §6 — Fette

1. R1 e R2 nel disegno (mappa e righe del sector), coi test della geometria: fly-by, fly-over, oltre 120°,
   intercettazione.
2. R5, il confronto con la curva.
3. R3, la virata dopo il decollo nel lettore della prosa; misura sul 2611.
4. Prova a schermo sul banco e prove a mano nuove per il committente.

## §7 — Dopo le prove del 1 novembre

**Fatto: LICR PAZZE 2A RWY 29.** Il perché: AD 2.12 di LICR stampa la quota della soglia 11 come «NIL / NIL», e
`DecolloDi` senza quella quota rinunciava alla fine della pista della 29 e partiva dallo scalo (`FromAerodrome`), dove R3
non vira. Ora la quota della fine pista, se manca, è quella della soglia di partenza, se no dello scalo: la posizione c'è.
Codice comune (`Aip.Pdf.Italia`).

**Proposta R6 — la velocità dal resto del testo.** La virata si disegna con la velocità della colonna o della nota del
tratto, se no 210/220 kt. Ma la IAS MAX sta spesso nel testo della procedura (o nelle sue REMARKS) e non in un tratto:
sul 2611, di 213 procedure della prosa con una «IAS MAX» nel testo, **48** hanno una velocità che nessun tratto porta —
LIPH 200 kt (10), LIEE 220 (8), LIPQ 185/240 (7), LIMJ 185 (4), LIBD 220-250 (4), LIPO 220 (2), LIPX 250 (2), LIPR
180/210 (2), una a LIBP, LIBR, LICG 150, LICR 200, LIMC, LIMP, LIPB 160, LIRQ 140, LIRZ. Proposta: se il testo dice **una
sola** IAS MAX (o una sola «during turn»), quella vale per le virate della procedura senza velocità propria; con due
velocità diverse e nessuna «during turn» (LIPQ 185/240, LIBD 240/250) si resta a 210/220 e si dice. LIRQ dalla 05 a
140 kt: raggio 0,6 NM invece di 1,4. Da decidere: anche le REMARKS della **salita iniziale** valgono per le SID della
stessa pista (LIRQ: la 140 sta lì)?

**Proposta R7 — le salite iniziali a rami.** Le salite di LIPR e il ramo 4 di LIEE RWY 32 non sono un problema di curve:
sono salite divise per SID («if cleared via SID AMLON 9B: … b) SID ANC 8B: …»), che il lettore non legge. Sul 2611 sono
**20 salite su 100**, in 13 scali (LIBF, LIBP, LIBR, LICB, LIEE 5, LIMC, LIML 2, LIMP, LIPE, LIPR 2, LIRN, LIRP 2): 8
senza tratti, le altre con il solo primo tratto. Proposta: un ramo diventa una salita a sé, col nome della SID («LIPR RWY
12 per AMLON 9B»), e la SID la usa come oggi usa la salita della sua pista. LIEE RWY 32 (4) ha in più il testo italiano
mescolato all'inglese nel PDF («rotta», «su», «105°. 300°»): va pulito prima.

## §8 — R6 e R7 fatte (1 novembre, sera)

Il committente: «Sì, R6 e R7», e alla domanda aperta di R6: **le REMARKS della salita iniziale valgono anche per le SID
della stessa pista**. Misurato col lettore della prosa rifatto sul modello del 2611 (copia dell'ambiente di prova).

**R6 — la velocità dal testo** (`Aip.Pdf.Italia`, `ItalianAd2ProseVelocita.cs`). La velocità delle virate di una procedura
della prosa è la sola IAS MAX del suo testo o delle sue REMARKS, o la sola «during turn» fra più velocità; anche «Turns at
IAS MAX 220 kt» e «Departure turn limited to 210 KT». Due velocità diverse senza una «during turn» (LIPQ 185/240, LIBD
240/250, LIMP 185/210, LIPR RWY 30 180/210 fra due rami) non valgono: si resta a 210/220 kt e la nota della lettura lo
dice (25 procedure). Una SID che non ne dice prende quella della salita che continua, se no della sola salita della sua
pista. La **virata dopo il decollo** è della salita iniziale: per una SID vale prima la velocità della salita, poi la sua
(LIRQ dalla 05: la salita dice 140 kt, BIBNO 1H dice 210 kt per le virate dopo). La velocità sta nel modello,
`Procedure.TurnSpeed` (**codice comune**, `Aip`: un campo nuovo, facoltativo), e il Lab la usa per le curve dove il tratto
non ne ha una sua (`CurveDelleProcedure.Velocita`, mappa e «Porta»).
- 308 procedure con la velocità della procedura, 124 senza una velocità in un tratto (prima disegnate a 210/220 kt).
- LIRQ RWY 05 e le sei SID «H» dalla 05: virata dopo il decollo a 140 kt, raggio 0,6 NM (era 1,4).
- LIPH SID dalla 07 a 200 kt (1,25 NM, era 1,38); LIPO UVNIB 1X a 220 kt (1,51 NM).

**R7 — le salite a rami** (`ItalianAd2Prose.DecodeAll`). Una salita iniziale coi rami per SID che hanno un percorso diventa
una salita per ramo, col nome delle sue SID: «LIPR RWY 12 per AMLON 9B», «LIRN RWY 06 per altre SID». Si legge la parte
comune col ramo, senza dividerla di nuovo (dividerla tagliava «turn left as soon as possible after take-off» al decollo,
LICR RWY 33); il testo stampato resta quello intero. I rami che dicono solo una pendenza (LIPX RWY 04) non sono salite: la
salita resta una. Un ramo che non si legge tiene la sola parte comune, come prima, e lo dice (LIPR RWY 12 AMLON 9B e BOA
7Z, RWY 30 AMLON 9A e ANC 8A: arco DME senza radiale, «leave … on RDL 269», «for aircraft CAT»; LIRF RWY 07: «expect ATC
instruction»). Un ramo che finisce su una rotta senza un punto è aperto e lo continua la SID (LIML RWY 17, due rami). Le
SID continuavano già il loro ramo (SC1): non cambiano.
- Salite iniziali: 127 (erano 100), con tratti 101 (erano 67).
- LIEE RWY 32 (4): «on su track 300°» e «track 105°. 300° until» (il testo italiano mescolato nel PDF) si leggono «on
  track 300°» e «track 300°», come gli altri due rami, e si dicono come errori di stampa.

Test: `Ad2ProseVelocitaERamiTests` (17), `CurveDelleProcedureTests`. Prove a mano A107-A109.
