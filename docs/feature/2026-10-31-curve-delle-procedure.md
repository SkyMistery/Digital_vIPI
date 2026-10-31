# Le curve delle procedure (31 ottobre 2026)

Filone Sector Lab. Segue [«Le segnalazioni del 2611»](2026-10-23-segnalazioni-2611.md) (giro del 26 ottobre: le «SID
dalla soglia») e il «Da vedere dopo» di [F7, SID e STAR](2026-10-19-f7-sid-e-star.md) (le virate delle convenzionali).
**Carta da approvare**: le regole del §3 sono proposte; le domande del §4 sono del committente.

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
