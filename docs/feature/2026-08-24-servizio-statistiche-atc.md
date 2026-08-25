# Statistiche ATC: il terzo servizio (carta, 24 agosto 2026)

> Nuovo figlio dell'hub `/services`, accanto a `vsop` (documentazione) e `profile-swapper` (strumento).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Regola d'ingaggio con la sorgente:
> [sorgenti](2026-08-22-sorgenti-giro-automatico-ta-piste.md) — interfaccia neutra in Application,
> adapter `Ivao*` in Infrastructure, riga nella policy di import.
> **Stato al 25 agosto 2026 (sera): SERVIZIO COMPLETO sul ramo `statistiche-atc`, non ancora fuso in `main`.**
> Le otto richieste del committente del 25 agosto sera stanno in **§16**, e con loro la sezione
> **Aeroporti** (traffico di ogni campo e quanto ne copriamo), la potatura del dettaglio e il capitolo di
> Guida che mancava. ⚠️ §3 conteneva un'affermazione **falsa** su `/airports/{icao}/stats`, corretta lì.
> Tutte e otto le slice di §9 sono chiuse, più le aggiunte di §11. Suite verde e Release pulita su entrambi
> i TFM: **2176 test su net8, 1938 su net10** — la differenza non è un buco, `Vipi.E2E.Tests` e
> `Vipi.AuroraBridge.Tests` girano **solo su net8** (⚠️ un solo numero, come si scriveva prima, fa sembrare
> che su net8 manchi qualcosa). Il ramo parte da `bda3294` e arriva a `d523037` (19 commit).
> Cosa resta prima e dopo la fusione: **§12**.

## 1. Perché

Il controllore che si collega su un callsign italiano oggi non ha nessun posto dove vedere quanto e cosa ha
controllato: il tracker IVAO mostra le ultime 5 connessioni e basta. Lo staff di divisione non ha nessun
modo di rispondere a «chi copre cosa, e quando restano i buchi».

Il pezzo che nessuno ha, e che noi **abbiamo già in archivio**, sono i poligoni dei settori con i loro limiti
verticali: da lì si ricava non solo *quanto* si è stati connessi, ma *quale traffico è passato dentro la
propria area* — che è la domanda vera.

## 2. Le quattro scelte del committente (24 agosto)

| | scelta |
|---|---|
| **Perimetro** | callsign `LI*` (chiunque li usi, visitor inclusi) **+** le sessioni ovunque nel mondo dell'utente che ha fatto login (filtro `userId` sul suo VID) |
| **Traffico gestito** | **entrambi** i metodi: campionamento AoR dal vivo *e* movimenti d'aeroporto dall'API per il retroattivo |
| **Visibilità** | le proprie a chiunque sia loggato; lo staff vede tutti; **classifiche pubbliche con interruttore in mano allo staff** (default: spente) |
| **Backfill** | 12 mesi |

⚠️ Sul traffico il committente ha posto un vincolo esplicito, che è la regola centrale di tutto il servizio:
**l'AoR è un volume, non una superficie.** Un traffico che sorvola a FL260 uno spazio che finisce a FL195
**non è stato gestito** da quello spazio. L'attribuzione è 3D o non è.

## 3. Cosa dà IVAO — misurato il 24 agosto, non dedotto

Spec scaricate: `https://api.ivao.aero/docs/tracker-json` e `/docs/core-json` (Cloudflare risponde 403 allo
user-agent di urllib: serve un UA da browser, come già annotato per le altre sonde).

| Endpoint | Cosa dà | Auth |
|---|---|---|
| `/v2/tracker/whazzup` | **pubblico, nessun token.** ATC: `id` sessione, `callsign`, `userId`, `rating`, `createdAt`, `time` (secondi connesso), `atcSession{position,frequency}`, `atis`. Piloti: `lastTrack{latitude,longitude,altitude,groundSpeed,state,onGround}` + `flightPlan{departureId,arrivalId,aircraftId}` | — |
| `/v2/tracker/sessions?connectionType=ATC&userId=&callsign=&now=&from=&to=&page=&perPage=` | **storico connessioni**, paginato (max 100/pagina). Item: `callsign`, `userId`, `time`, `createdAt`, `completedAt`, `softwareType`, `user{firstName,lastName,divisionId,rating}` | token app, scope `tracker` ✅ **verificato 200** |
| `/v2/tracker/sessions/{id}` | come sopra **+ `atcSession{position,frequency}`** (la lista non ce l'ha: lì la posizione si ricava dal callsign) | idem |
| `/v2/users/{vid}` | `hours[] = {type: pilot\|atc\|staff, hours: secondi}` → totale di carriera, e `rating` | app ✅ (già usato) |
| `/v2/airports/{icao}/traffics?from&to` | movimenti dell'aeroporto nella finestra: **inbound / outbound / flightover**, ciascuno con callsign, `flightPlan` e `lastTrack`. ⚠️ Porta anche **gli istanti** (`createdAt`, `lastTrack.timestamp`) e regge finestre lunghe: misurato su LIRF il 25 agosto, **30 giorni = 863 in / 926 out / 3 sorvoli in 981 KB e 1,3 s** (7 gg: 254 KB; 1 gg: 60 KB) | app |
| ~~`/v2/airports/{icao}/stats?from&to&limit`~~ | ⚠️ **NON è quello che questa riga diceva.** Rimisurato il 25 agosto 2026 col token vero: è una **fotografia al minuto** dello stato corrente (`{icao, in, out, total, timestamp}` ripetuto ogni ~60 s), non un contatore di movimenti; `limit` deve stare **sotto 100** (`limit=400` → `400 Should be lower than 100`), cioè al massimo un'ora e mezza di storia. Chi cerca i movimenti di un campo usa `traffics` | app |
| `/v2/tracker/sessions/{id}/tracks` | traccia completa di un volo (per la mappa del dettaglio sessione) | app |

**La chiave di giunzione**: l'`id` della sessione ATC in whazzup **è lo stesso** id dello storico
`/v2/tracker/sessions/{id}`. Il campionamento dal vivo e il backfill scrivono quindi sulla **stessa riga**,
senza euristiche di accoppiamento su (callsign, ora).

**Costo misurato di whazzup** (24 ago, ~13:10Z): 705 749 byte grezzi, **119 328 byte sul filo**
(`Content-Encoding: br`), **0,21 s**, `Age: 5`. Contenuto in quell'istante: 468 piloti, 71 ATC, di cui **3**
con callsign `LI*`. Un giro al minuto = ~170 MB/giorno, **zero chiamate autenticate**. Da abilitare Brotli
nell'`HttpClient` (`AutomaticDecompression`), altrimenti si scaricano 705 KB per niente.

## 4. Cosa IVAO NON dà, e come lo costruiamo

Non esiste nessun endpoint «quali aerei ha gestito questo ATC». Lo deriviamo noi.

### 4.1 Attribuzione dal vivo (il metodo primario)

Il poller che gira già (`AtcPollingHostedService`, 60 s) passa da `now/atc/summary` al whazzup completo —
stessa cadenza, stesso numero di chiamate di adesso. A ogni giro:

1. ATC `LI*` online → upsert della riga sessione (`id` IVAO come chiave, start da `createdAt`, durata da `time`);
2. per ogni sessione, **volume di competenza** = poligono del settore più i settori figli non coperti da nessun
   altro online (§4.3), intersecato con la fascia `[LowerLimit, UpperLimit]`;
3. ogni pilota il cui `lastTrack` cade **dentro il poligono E dentro la fascia** → upsert di una riga
   «aereo visto», con `FirstSeenUtc`/`LastSeenUtc`.

Il cuore è **puro e testabile**: punto-in-poligono + fascia verticale + risoluzione della copertura.

⚠️ **Correzione alla prima stesura di questa carta: metà del motore esiste già, e va riusata, non riscritta.**
In `Vipi.Application/Aor` ci sono `PolygonGeometry` (parsing di `RegionMapPolygon`, `Ring` con bounding box,
adiacenza) e `AorFlBand` (normalizzazione dei limiti). Manca **solo** il punto-in-poligono, che nasce dentro
`PolygonGeometry` — stessa classe, non una gemella.

⚠️ **E soprattutto: NON è vero che le quote sono in piedi «per schema».** `AorFlBand` documenta la regola
vera: **l'unità non è tracciata**, e il valore si interpreta con un'euristica — `> 660` = piedi (÷100),
`≤ 660` = già FL. Oggi nel `vipi.db` reale tutti i limiti sono in piedi (`19000`, `19500`, `32500`, `2000`,
`3000`: zero valori ≤ 660), ma niente lo garantisce domani. Quindi il confronto si fa **in FL**:

- limiti settore → `AorFlBand.Normalize(lower, upper)` (già scritto, già coperto da test);
- quota pilota → `lastTrack.altitude` in piedi ÷ 100.

`UpperLimit = null` significa «senza tetto» (FL660 convenzionale), non «zero»: è il caso di `LIBB_ES_CTR`.

⚠️ **Limite noto, da dire in pagina.** Misura sul `vipi.db` reale (24 ago):

| catalogo | righe | con poligono | con limiti |
|---|---|---|---|
| `AccSectors` | 153 | **142** | 148 |
| `AirportSectors` | 193 | **143** | 143 |

I settori senza poligono non producono attribuzione, e le TWR hanno il cerchio sintetico di 5 NM, che è una
stima (`IsShapeSynthetic`). Il conteggio del traffico è quindi **una misura, non un registro**: la pagina lo
deve dichiarare, non lasciarlo intuire.

### 4.2 Attribuzione retroattiva (il metodo di riempimento)

Per le sessioni **d'aeroporto** già passate:
`/v2/airports/{icao}/traffics?from={sessione.start}&to={sessione.end}` dà i movimenti reali della finestra.
Copre TWR/GND/DEL/APP d'aeroporto, **non gli ACC** (per i quali il passato resta senza traffico: si popola
vivendo). Un giro, di notte, per le sessioni ancora prive di traffico.

### 4.3 Copertura top-down

Chi è online eredita i settori sottostanti scoperti. Serve la **discesa**: «quali settori sono miei adesso?»
= i discendenti per cui nessun antenato più vicino di me è online. Funzione pura nuova; la risalita esistente
(`TransferOnlineResolver`, vista live) e la sua euristica di matching callsign si riusano com'è.

⚠️ **L'albero su cui scendere è `Sector`, non `AccSector.ParentCallsign`.** Nei cataloghi il padre lo hanno
solo gli APP e gli ACC: DEL/GND/TWR **non sono nodi** (`ParentCallsign` resta null) e il loro padre si deriva
dalla scaletta DEL→GND→TWR→APP (`AirportPositionLadder`, in `Vipi.Domain/Services`). Quella derivazione è già
stata fatta una volta: la proiezione la scrive in `Sector.ParentSectorId` (321 righe, fonte unica del
Round 20). Scendere sui cataloghi significherebbe rifare la scaletta a mano — e sbagliarla in modo diverso.

Il volume (poligono + limiti) invece **sta nei cataloghi**, cercato per callsign: `Sector.Callsign` ==
`AccSector`/`AirportSector.ComposePosition`. Verificato sui callsign online del 24 agosto (`LIRF_TWR`,
`LIRF_TW1_APP`, `LIRF_GND`, `LIPZ_TWR`, `LIRR_NE1_CTR`): tutti presenti in catalogo con match esatto.

### 4.4 Un aereo, UNA sessione (scoperto provando il motore sul dato vero)

⚠️ **I settori italiani si sovrappongono pesantemente**, e nessuno se n'era accorto finché il motore non è
girato sui poligoni veri. Snapshot whazzup del 24 agosto (467 piloti, 171 settori italiani con poligono): un
singolo volo su Roma cadeva dentro **sei** settori — `LIRR_NE_CTR`, `LIRR_NE1_CTR`, `LIRR_OV_CTR`,
`LIRR_MIL_CTR`, `LIRR_FSS` e un APP di Fiumicino. Le sovrapposizioni sono legittime (configurazioni
alternative di sector split, militare, FSS), ma contare «tutti i settori che contengono il punto» gonfierebbe
le ore gestite di **cinque o sei volte**.

La copertura del §4.3 risolve gran parte del problema — ogni settore ha **un solo** proprietario online — ma
non tutto: due sessioni con settori sovrapposti (il caso `MIL`/`FSS` contro i civili, tutte radici) possono
rivendicare lo stesso aereo. Regola di scelta, in `TrafficAttribution`, dalla più forte alla più debole:

1. **la posizione dichiara la fase del volo** (§4.4-bis);
2. **profondità maggiore** nell'albero (la TWR batte il CTR);
3. **banda verticale più stretta**;
4. **poligono più piccolo** (area del bounding box);
5. **callsign alfabetico** — non è una preferenza, è la garanzia che due giri diano lo stesso esito.

### 4.4-bis DEL e GND non si distinguono con la geometria: si distinguono con la fase del volo

Osservazione del committente, e ha ragione: **la DEL gestisce solo le partenze ancora ferme, la GND tutto ciò
che è a terra.** Con la sola regola «vince il più profondo», una DEL in frequenza si prenderebbe l'intero
aeroporto perché è l'ultimo gradino della scaletta.

E la geometria non può aiutare, perché non c'è: misurato sul `vipi.db` reale, **DEL 0 poligoni su 5, GND 0 su
20** (APP 59/59, TWR 84/84 di cui 16 col cerchio sintetico). Le due posizioni esistono solo nella scaletta.
⚠️ Conseguenza per la slice 4: il volume di DEL e GND è quello della **TWR dello stesso aeroporto** — senza,
non rivendicherebbero mai niente.

La distinzione la portano i dati del tracciato. Fasi (`FlightPhases.Of`): **Parked** = a terra, fermo, stato
`Boarding`, entro 3 NM dal campo di partenza; **Ground** = a terra, tutto il resto; **Airborne** = in volo.
Competenze dichiarate: DEL `{Parked}`, GND `{Parked, Ground}`, TWR/ITWR `{Ground, Airborne}`, APP/CTR
`{Airborne}`. Non è un divieto ma una preferenza: se nessuno dei presenti dichiara la fase (una DEL sola in
frequenza e un aereo che rulla), vince la copertura — c'è lei sola, è sua.

⚠️ **Il controllo della distanza dalla partenza non è pedanteria**: `On Blocks` e `Boarding` sono entrambi
«fermo a terra», ma il primo di solito è un **arrivo**. Verificato sui quattro aerei fermi a Fiumicino nello
snapshot reale del 24 agosto:

| callsign | stato | rotta | dist. dalla partenza | fase |
|---|---|---|---|---|
| ITY081 | `On Blocks` | LEPA→**LIRF** | 453 NM | a terra (è arrivato) |
| AZA006 | `Boarding` | **LIRF**→LIRI | 1,1 NM | parcheggiato → DEL |
| HBIAX | `Boarding` | **LIRF**→LFTZ | 1,0 NM | parcheggiato → DEL |
| AZA9N5 | `Boarding` | **LIRF**→UUEE | 0,3 NM | parcheggiato → DEL |

### 4.4-ter Le disconnessioni: tre casi, e due erano buchi

Domanda del committente. Rispondere ha cambiato il modello dati **prima** che venisse scritto.

| caso | col solo callsign | rimedio |
|---|---|---|
| **il pilota cade e rientra nello stesso volo** | ✅ già corretto: la riga è per callsign, non per id di sessione del pilota (che alla riconnessione cambia) | nessuno — ma è un effetto della chiave, non una guardia: va scritto o il prossimo che «ottimizza» la chiave lo rompe |
| **il pilota fa più voli senza disconnettersi** | ❌ due movimenti contati come uno | `FlightLegResolver`: la **tratta** entra nella chiave — cambia `dep`/`arr` → tratta nuova; stessa rotta che riappare dopo 30 minuti di buco (navette, circuiti) → tratta nuova |
| **l'ATC cade e rientra** | ❌ IVAO apre una sessione nuova, lo stesso aereo compare in tutt'e due e sommando si conta doppio | `AtcShiftGrouper`: il **turno** raccoglie le sessioni consecutive dello stesso VID sullo stesso callsign entro 15 minuti; i traffici si contano distinti per turno |

Il turno **non è** una tabella nuova: è una colonna sulla sessione (`ShiftKey` = id della prima sessione del
gruppo). La sessione resta l'unità di scrittura, perché è la chiave che IVAO ci dà; il turno è l'unità con
cui si raccontano i numeri.

### 4.5 I limiti vuoti NON sono un buco: sono 0 ft e 66 000 ft

⚠️ **Correzione (committente, 24 agosto sera).** Questa sezione diceva il contrario, ed era un errore mio di
lettura del dato: avevo contato i campi vuoti come «limiti non compilati» e messo in conto un lavoro
editoriale sui settori ACC. Non esiste. **Il vuoto è il valore, e vuol dire una cosa precisa**: inferiore
vuoto = **0 ft** (suolo), superiore vuoto = **66 000 ft**, che è poi lo stesso che scrivere UNL.

| | tetto vuoto (= 66 000 ft) | pavimento vuoto (= 0 ft) |
|---|---|---|
| `AccSectors` (153) | 138 | 151 |
| `AirportSectors` (193) | 50 | 193 |

Quei 138 settori ACC che «arrivano senza limite e partono da terra» sono **corretti così**, non incompleti.
E il motore li legge già esattamente in questo modo: `AorFlBand` normalizza il pavimento vuoto a `Ground`
(FL 0) e il tetto vuoto a `Unlimited` (FL 660 = 66 000 ft) — la stessa convenzione, scritta prima e per
un'altra ragione, dell'estrusione 3D dell'AoR.

Quindi l'aereo a FL390 sopra Fiumicino **è** di `LIRR_NE1_CTR`, e lo è per una decisione presa, non per un
campo lasciato in bianco. La regola del committente («FL260 sopra uno spazio che finisce a FL195 non è
gestito») morde dove i limiti sono scritti — le TWR a 3000 ft (§4.5-bis), gli ACC che dichiarano un tetto
come `LIMM_WS2_CTR` a FL325 — e negli altri casi non deve mordere, perché non c'è niente da tagliare.

### 4.4-quater Rilettura della slice 1: altri quattro difetti di conteggio

Riletto il conteggio a mente fredda su richiesta del committente, **misurando** su 1316 sessioni ATC italiane
vere degli ultimi 30 giorni e sullo snapshot whazzup del 24 agosto.

**1. Gli aerei a terra dentro i volumi ACC** (misurati: cinque nei settori di Roma, **tre fermi al gate di
Fiumicino**, perché 138 settori su 153 hanno il pavimento a terra).

⚠️ **Qui avevo sbagliato la cura, e il committente ha corretto**: avevo messo un divieto secco — un CTR non
riceve traffico fermo o in rullaggio nemmeno se è l'unico online. È falso operativamente: **in top-down APP e
CTR gestiscono anche il traffico a terra quando sotto non c'è nessuno**. Divieto rimosso, torna a valere
l'eredità del §4.3: quell'aereo è dell'ACC, e la riga si scrive.

Il conteggio resta onesto in un altro modo — **due numeri distinti invece di uno**:
- **movimenti gestiti**: tratte che si sono mosse almeno una volta (`FlightPhases.IsMovement`). È il numero
  in evidenza;
- **presenze**: tutte le tratte viste, parcheggiati compresi.

Così un ACC che sta tre ore in frequenza non si vede accreditare come «movimenti» il piazzale di Fiumicino,
ma il traffico che ha davvero seguito resta suo.

**2. ⚠️ Un fermo del poller spezzava un volo in due.** Riavvio, deploy o rete giù per più di 30 minuti, e al
ritorno lo stesso aeroplano nello stesso volo apriva una tratta nuova: **un deploy contava doppio ogni aereo
in volo in quel momento**. Il buco è nostro, non suo. Rimedio: l'identità della tratta ora è l'**id del piano
di volo** quando c'è (uguale = stessa tratta anche dopo ore; diverso = tratta nuova anche dopo un minuto,
che è il caso di chi rifila per la gamba successiva). La regola dei 30 minuti resta solo come ripiego per
chi vola senza piano di volo.

⚠️ **E il buco si dichiara accanto a quel volo** (richiesta del committente): `FlightLegResolver.HasObservationGap`
marca la riga della tratta, e il dettaglio sessione mostra il segnale **sulla riga di quel volo** — non in una
nota generale a fondo pagina. I minuti e la traccia di quella tratta sono incompleti e chi legge lo deve
sapere lì.

**3. Il 32% delle connessioni dura meno di cinque minuti.** Su 1316 sessioni italiane in 30 giorni: **419
sotto i 5 minuti, di cui 231 sotto il minuto** (in tutto 10 ore). Contare «quante sessioni» senza soglia
gonfia di un terzo. Le sessioni-lampo si **memorizzano** (sono il dato che IVAO dà) ma vanno tenute fuori dai
conteggi. **Soglia fissata dal committente: 60 secondi** (`StatsCounting.MinCountedSession`).

**4. I minuti gestiti non sono `ultimo − primo` avvistamento.** Se un aereo esce dal settore e rientra nella
stessa tratta, la differenza fra primo e ultimo conta anche il tempo in cui non c'era. I minuti si
**accumulano contando i giri** in cui l'aeroplano risulta dentro (`SeenMinutes` += 1 per giro), non si
sottraggono.

**Verificato e NON problematico**: le connessioni ATIS non compaiono fra le sessioni ATC (0 su 1316 in 30
giorni, e nel whazzup le posizioni sono solo TWR/APP/GND/CTR/DEL) — non serve nessun filtro.

**Numero che giustifica il turno da solo**: **501 sessioni su 1316 (38%) riprendono entro 15 minuti dalla
precedente**. Senza `ShiftKey` i due quinti delle righe sarebbero doppioni.

### 4.5-bis Il tetto delle torri: 3000 ft, non FL195

Il default di sistema era **19500 ft per tutte** le posizioni con volume — cioè una TWR che rivendicava fino
a FL195 e che, stando più in basso nella scaletta, batteva l'APP sul traffico in avvicinamento. Il
committente ha fissato la regola vera: **le torri arrivano a 3000 ft**. Cambiato in
`EfAirportSectorRepository.DefaultUpperFor` (TWR → 3000, APP/DEP/CTR/FSS → 19500).

✅ **Dato in archivio corretto il 24 agosto**, su autorizzazione del committente, con guardia stretta —
`Position = 'TWR'` **e** `LimitsFromSource = 0` **e** `UpperLimit = 19500`:

| | |
|---|---|
| righe aggiornate | **81** |
| tetti TWR prima | 19500 ×81, 3000 ×1, 2000 ×2 |
| tetti TWR dopo | **3000 ×82**, 2000 ×2 |
| posizioni non-TWR toccate | 0 |

Le tre torri con un tetto scelto a mano (`LIBC_I_TWR` e `LIBD_TWR` a 2000, `LIBR_TWR` a 3000) sono rimaste
come stavano: la guardia serviva a questo. Copia di sicurezza in `src/Vipi.Host/vipi.db.bak-pre-tetti-twr-20260824`.
Il re-import non le rovina: preserva i limiti già scritti (`row.UpperLimit ??= default`).

⚠️ **La produzione è un altro database** (MariaDB): la stessa `UPDATE`, con la stessa guardia, va eseguita là
come passo di dati della slice 2 — non è coperta da questa correzione.

### 4.6 Padre e figlio insieme in frequenza: chi conta il traffico

Domanda del committente (24 agosto sera): con `LIRR_NE_CTR` e il suo figlio `LIRR_TS_CTR` tutti e due in
frequenza, e la shape del padre che copre anche quella del figlio, un aereo che sta solo dentro TS viene
contato **a tutti e due**?

**No.** `TrafficAttribution` sceglie **una** sessione per aeroplano, e dopo la fase di volo il criterio più
forte è la **profondità nell'albero**: il figlio vince. Verificato sul dato reale — nel `vipi.db`
l'annidamento c'è davvero (`LIRR_TS_CTR` è figlio di `LIRR_NE_CTR`, e `LIRR_OV_CTR`/`LIRR_US_CTR` lo sono di
TS) e l'area di TS è **al 100% dentro** quella di NE, cioè il caso peggiore possibile.

**Vale identico per gli avvicinamenti**: 21 APP su 64 pendono da un altro APP (`LIMC_ANW_APP` e
`LIMC_ASW_APP` da `LIMC_ANE_APP`; le sei APP di Fiumicino da `LIRF_TW1_APP`, e `LIRF_PS1_APP` da
`LIRF_PN1_APP`). Stesso meccanismo, stesso esito.

⚠️ E se un domani la gerarchia **non** fosse compilata, i due settori sarebbero due radici: la scelta scende
al criterio successivo — banda più stretta, poi **poligono più piccolo** — e il figlio vince lo stesso. C'è
un test che tiene ferma anche questa rete di sicurezza.

### 4.7 ⚠️ Il difetto che quella domanda ha scoperto: un anello ripetuto annulla il poligono

Andando a verificare, `LIRR_TS_CTR` non attribuiva **mai** niente. Non per la gerarchia: perché la sua shape
contiene **lo stesso anello due volte** (i 66 punti sono 33 ripetuti). Col test pari/dispari un contorno
doppio si annulla — ogni attraversamento è contato due volte, la parità torna sempre pari — e il settore non
contiene nulla, mai.

| | poligono vero di `LIRR_TS_CTR` |
|---|---|
| punti campionati dentro, **prima** | **0** su 4000 |
| punti campionati dentro, **dopo** | **1860** su 4000 |

Nel `vipi.db` reale succede a **2 poligoni su 283** (`LIRR_TS_CTR` e `LATI_APP`), ma il primo è un settore di
Roma: senza questa correzione le sue ore avrebbero avuto traffico zero per sempre, e **sulla mappa non si
sarebbe visto niente**, perché un contorno disegnato due volte è identico a uno solo.

La correzione sta in `PolygonGeometry.ParsePoints`, cioè dove la geometria nasce: la ripetizione si toglie
una volta per tutti i consumatori (attribuzione, adiacenza, proiezione SVG, visore 3D).

**Di chi è il difetto? Della sorgente.** Chiesto direttamente a IVAO il 25 agosto col token app:

| endpoint | punti | anello |
|---|---|---|
| `/v2/subcenters/LIRR_TS_CTR` | 66 | **ripetuto ×2** |
| `/v2/subcenters/LIRR_NE_CTR` | 56 | normale |
| `/v2/ATCPositions/LATI_APP` | 18 | **ripetuto ×2** |

Arriva doppio da lì, in **tutt'e due** i campi (`regionMap` e `regionMapPolygon`), e noi lo memorizziamo
verbatim — che è la cosa giusta per un dato di sorgente. Quindi la correzione va dove sta: in lettura, non
riscrivendo l'archivio.

### 4.8 La seconda anomalia della stessa famiglia: i punti gemelli

Cercando la prima è saltata fuori una compagna, molto più diffusa: **punti ripetuti di fila**, cioè lati di
lunghezza zero. Li ha il **29% dei poligoni** (81 su 283), per **1547 lati** in tutto, con punte di 489 su un
solo settore (`DTTC_FSS`).

Per il punto-in-poligono sono innocui — un lato degenere non attraversa mai il raggio — ma per la
**triangolazione** dell'estrusione 3D no: un vertice doppio produce facce degeneri, ed è il sospetto numero
uno quando una shape «si vede strana» a schermo. Tolti nello stesso punto, conservando il punto di chiusura
finale (che è legittimo).

⚠️ **E hanno fregato me prima ancora che il codice**: il mio primo controllo di auto-intersezione denunciava
un incrocio su `LIRR_TS_CTR` e `LIRR_NE_CTR`. Non c'era: i due lati «incrociati» condividevano un estremo,
perché quel punto era ripetuto. Il difetto era nello strumento, non nel dato — la regola di sempre.

## 5. Modello dati — due tabelle, non tre

```
AtcSession         SessionId (PK, id IVAO)  UserId  Callsign  Position  Frequency
                   StartUtc  EndUtc  DurationSec  Rating  Source (Live|Backfill)
                   ShiftKey            -- id della prima sessione del turno (§4.4-ter)
                   TrafficCount  DistinctAircraft  TrafficMinutes   -- contatori (§5.1 punto 2)

AtcSessionTraffic  PK (SessionId, PilotCallsign, LegOrdinal)        -- la tratta, non il solo callsign
                   PilotUserId  DepIcao  ArrIcao  AircraftIcao
                   FirstSeenUtc  LastSeenUtc  SeenMinutes  Origin (Aor|AirportApi)
```

**Una riga per aereo, non per campione**: una TWR di 3 ore fa ~40 righe, non 180. Niente tabella dei totali
(si aggrega a query): sarebbe il «modello gemello» che il pre-flight vieta.

### 5.1 Il bilancio dello spazio, in byte

Punto di partenza misurato: il `vipi.db` reale è **4,87 MB per 4246 righe**. Le statistiche portano ~500 000
righe l'anno: **due ordini di grandezza in più**, quindi il conto si fa prima.

| | anno 1 | a regime (dettaglio potato a 12 mesi) |
|---|---|---|
| `AtcSessionTraffic` (~75 B/riga su InnoDB) | ~40 MB | ~40 MB, stabile |
| `AtcSession` (21 231/anno, mai potate) | ~2 MB | +2 MB/anno |
| **totale** | ~50 MB | ~60-70 MB dopo cinque anni |

**Quota del piano: 1 GB** (confermata dal committente il 24 agosto). Siamo al 5-7%: largo, ma le quattro
regole qui sotto restano, perché è il momento in cui costano zero.

1. **Chiave primaria composita `(SessionId, PilotCallsign)`**: niente `Id` surrogato, niente secondo albero
   d'indice su mezzo milione di righe.
2. **Contatori denormalizzati sulla riga sessione** (`TrafficCount`, `DistinctAircraft`, minuti gestiti): due
   `int` per sessione, e sono ciò che permette di **potare il dettaglio senza perdere le statistiche**. Non è
   la «tabella dei totali» vietata dal §5: è la condizione perché la potatura sia reversibile nei numeri.
   Senza, il giorno della prima potatura le ore di un anno fa diventerebbero zero.
3. **Scrittura bufferizzata**: il poll gira ogni minuto, il DB no. Lo stato delle sessioni vive in memoria e
   si scrive quando **cambia** (aereo nuovo), più un checkpoint ogni ~10 minuti e la chiusura di sessione.
   ~10× scritture in meno; il rischio massimo di un riavvio è 10 minuti di `LastSeenUtc`.
4. **Zero campioni grezzi**: nessuna tabella di snapshot whazzup. Si consuma al volo e si butta.

⚠️ **Aggiornamento del 25 agosto**: le targhette del traffico (§13) hanno aggiunto otto colonne alla riga —
due fasi (enum→stringa, 32 caratteri di dichiarazione ma valori da 6-8), un `bool`, tre `int` di quota e due
`bigint` di consegna. Misura a occhio sulla riga InnoDB: **~50 B in più**, cioè da ~75 a ~125 B/riga e dal
~4% al **~6-7% della quota** a regime. È una crescita nota e accettata: quel che comprano è l'unica risposta
onesta a «l'ho visto atterrare?», e nessuna di quelle colonne costa una chiamata in più alla sorgente.

Volume **misurato** (§8.1), non stimato: **21 231 sessioni ATC italiane negli ultimi 12 mesi**. Con ~25 aerei
per sessione fanno ~500 000 righe di traffico l'anno. MariaDB regge senza pensarci; la retention si decide
comunque adesso, non dopo (lezione della retention di pubblicazione): **dettaglio traffico 12 mesi, sessioni
per sempre** (sono poche, pesano nulla, e sono il dato di valore — vedi §8.3: dopo l'anno IVAO le cancella).

## 6. Perimetro, permessi, privacy

- **Raccolta**: solo callsign `LI*`. Il roster dei membri IT **non è ottenibile** col token app
  (`/v2/divisions/{id}/users` → 500, già misurato) → il perimetro naturale è il callsign, non l'anagrafica.
- **Le mie sessioni ovunque**: su richiesta, per il VID di chi ha fatto login,
  `/v2/tracker/sessions?userId={vid}` — una chiamata, on-demand, non un giro di fondo.
- **Nomi**: roster nostro → `publicNickname` → VID nudo, in quest'ordine (§8.2). Mai inventare un nome, mai
  mostrare il segnaposto `User {vid}` come se fosse un nome.
- **Visibilità**: proprie = a chi è loggato; tutte = staff (`AdminStaffCodes`, come il resto);
  **classifiche = interruttore admin**, default spento, con la scelta registrata in audit (`AuditScribe`)
  come per la policy sorgenti — «chi ha deciso e quando».
- ⚠️ Il flag «classifiche pubbliche» è un **bool nuovo NOT NULL**: nasce `false` ovunque, e per una volta è
  il default giusto — ma va scritto qui, perché la trappola è nota e ha già morso una volta.

## 7. Rotte e ingressi

- `/services/stats` — la mia pagina (ore totali, per posizione, per mese, elenco sessioni, e le DUE tabelle
  degli aeroporti: gestiti e visti, §15);
- `/services/stats/user/{vid}` — **la stessa pagina**, coi numeri di un altro. Solo staff, con fascia e
  audit (§14, 25 agosto);
- `/services/stats/session/{id}` — dettaglio: durata, frequenza, aerei gestiti, **sequenza delle piste in
  uso**. ⚠️ La *mappa* delle tracce era in questa riga fin dalla prima stesura: il committente l'ha **rinviata**
  il 25 agosto (§11, «da ragionare»). Qui resta scritto che non c'è, o la prossima lettura la dà per fatta;
- ⚠️ `/services/stats/export.csv` — **non esiste più**: tolto il 25 agosto 2026 su richiesta del committente,
  tasto *e* meccanismo. Resta scritto qui perché tre punti di questa carta lo davano per fatto, e una carta
  che promette un endpoint che non c'è è il modo in cui qualcuno lo rimette fra due mesi;
- `/services/stats/division` — vista staff: copertura per aeroporto/ACC, buchi orari, classifica mensile;
- ingressi obbligatori (lezione dell'hub): **card in `ServicesHome.razor`**, voce nel menù ☰, sezione nella
  Guida, voce nella ricerca globale (`GuideSearchCatalog`).
- ⚠️ Gli orari si scrivono **UTC col suffisso Z**, l'ora locale la aggiunge il browser: `ToLocalTime()` in UI
  è vietato e c'è una guardia che lo sorveglia.

## 8. Le verifiche: fatte il 24 agosto, col token app vero

Sonde usa-e-getta in scratchpad (`probe_stats.py`, `probe2.py`, `probe3.py`), token `client_credentials`
scope `tracker configuration`.

| Domanda | Risposta misurata |
|---|---|
| `/v2/tracker/sessions` col token app? | ✅ **200**. Nessun 403/500 come su `divisions/{id}/users` |
| Nomi veri nello storico? | ⚠️ **dipende dall'utente, non dal token** (§8.2) |
| `from` quanto indietro? | **366–367 giorni**: finestra scorrevole di 12 mesi esatti (§8.3) |

### 8.1 Il filtro `callsign` è un PREFISSO, e vuole almeno 3 caratteri

La scoperta che cambia il piano di raccolta. Misurato sugli ultimi 30 giorni:

| filtro | sessioni |
|---|---|
| `callsign=L` | 0 |
| `callsign=LI` | **0** |
| `callsign=LIR` | **342** (LIRF_GND, LIRR_NE1_CTR, …) |
| `callsign=LIRR` | 80 |
| `callsign=EDD` | 2015 |

Quindi non serve né scorrere il mondo intero né interrogare i 346 callsign del catalogo uno a uno:
**23 query a prefisso di tre lettere** (`LIA`…`LIZ`) coprono tutta l'Italia lato server. Funziona anche
insieme a `now=true`: `?connectionType=ATC&now=true&callsign=LIR` → i soli italiani online adesso.

⚠️ `LIMM_CTR` e `LIRR_CTR` danno **0**: in Italia i CTR hanno sempre il pezzo di mezzo
(`LIRR_NE1_CTR`). Un test che cercasse il callsign «pulito» dell'ACC passerebbe a vuoto senza fallire.

**Costo del backfill 12 mesi, misurato prefisso per prefisso:**

| | |
|---|---|
| sessioni ATC su callsign italiani, 12 mesi | **21 231** |
| pagine da 100 | **~220 chiamate**, una volta sola |
| prefissi con traffico | LIB 1771 · LIC 2271 · LID 19 · LIE 2427 · LIM 4768 · LIP 4524 · LIQ 3 · LIR 5223 · LIV 82 · LIZ 143 |

Il numero conferma la stima del §5 (~20 000 sessioni/anno) **con il dato vero, non per analogia**.

### 8.2 I nomi: la privacy è dell'utente, e il servizio deve conviverci

| VID | `/v2/users/{vid}` | `user{}` dentro la sessione |
|---|---|---|
| 704798 (IT) | `Carmine` `Granato`, nick `Carmine (704798)` | nome pieno |
| 734962 (CU) | `Luis Angel` `Roque Subiadur` | nome pieno |
| 762032 (IT) | `null` `null`, nick **`User 762032`** | `null` |
| 727049 (IT) | `null` `null`, nick **`User 727049`** | `null` |

Non è un limite del token app (la memoria diceva «solo publicNickname»: **è più sfumato**, il token app i
nomi li dà — quando l'utente li rende pubblici). Regola di prodotto, a tre livelli:

1. se il VID ha fatto login da noi → nome dal nostro roster (lo scope `profile` ce l'ha dato);
2. altrimenti `publicNickname` se non è il segnaposto `User {vid}`;
3. altrimenti **il VID nudo**.

⚠️ Conseguenza sulle classifiche pubbliche: finché la gente non fa login da noi, una parte della classifica
sarà «User 762032». È accettabile, ma va saputo prima di accendere l'interruttore, non dopo.

### 8.3 La retention IVAO è di 12 mesi, e questa è la ragione per farlo adesso

| finestra | sessioni ATC (mondo) |
|---|---|
| −365 giorni, 7 gg | 11 000 |
| −366 giorni, 24 h | 67 (il bordo) |
| −368 giorni e oltre | **0** |

Oltre l'anno **IVAO non conserva nulla**. Il backfill da 12 mesi non è una scelta di comodo: è *tutto quello
che esiste*. Da qui in poi la storia più lunga dell'anno esiste solo se cominciamo a tenerla noi — che è
l'argomento più forte per fare il servizio subito.

### 8.4 I 503 sono transitori

Due chiamate su nove della prima sonda hanno risposto `503 upstream connect error` (Envoy davanti all'API);
le stesse identiche URL, ritentate, hanno dato 200 al **primo** ritento. Il `TransientRetryHandler` che
c'è già in `Vipi.Infrastructure/Ivao` copre il caso: **il backfill non deve trattare un 503 come «zero
sessioni»** — sarebbe un buco silenzioso nello storico.

### 8.5 Due dettagli per il motore geometrico

- I poligoni in archivio sono **JSON di coppie `[lon, lat]`** (`[[14.788611,41.231944],…]`, verificato su
  `AccSectors` e `AirportSectors`): anello semplice, nessun envelope GeoJSON.
- `lastTrack.altitude` è in **piedi** e concorda col piano di volo (Concorde a 60 119 ft con `level: "F600"`).
- ⚠️ `lastTrack` può essere **null** (1 pilota su 468 nel campione): guardia obbligatoria.

## 9. Slice (un commit per passo, build verde a ogni passo)

1. ✅ **FATTA** (24 agosto). Cuore puro, test-first, in `Vipi.Application` accanto a ciò che c'è già:
   `PolygonGeometry.Contains` (punto-in-poligono, ray casting con prefiltro bbox), `Stats/SectorVolume`
   (poligono + banda di `AorFlBand`), `Stats/CoverageResolver` (discesa sull'albero `Sector`),
   `Stats/TrafficAttribution` (§4.4, nata dalla prova sul dato vero), `Stats/FlightPhase` (§4.4-bis),
   `Stats/FlightLegResolver` e `Stats/AtcShiftGrouper` (§4.4-ter). **58 test nuovi**,
   `dotnet build Vipi.slnx -c Release --no-incremental` = 0 warning 0 errori.
   Prova sul dato reale: whazzup del 24 agosto contro i 171 poligoni italiani → l'ITA a terra a Fiumicino
   finisce in `LIRF_TWR`, i voli in crociera nei settori `LIRR_*`, il traffico su Milano nei `LIMM_*`.
2. ✅ **FATTA** (24 agosto). Entità `AtcSession`/`AtcSessionTraffic` in `Vipi.Domain/Entities/StatisticheAtc.cs`,
   configurazione in `VipiDbContext`, migrazione `StatisticheAtc` emessa **due volte** (SQLite e
   `Vipi.Infrastructure.MySqlMigrations`) e **applicata a una copia del `vipi.db` reale**: tabelle e cinque
   indici creati, 153 `AccSectors` e 193 `AirportSectors` intatti. 6 test di schema nuovi.

   ⚠️ **Le lunghezze stanno nel modello, non in `MySqlStringLengths.Map`.** La mappa esiste perché su
   Postgres una lunghezza sarebbe un cambio di tipo su colonne già popolate; qui le tabelle **nascono
   adesso**, quindi la lunghezza si dichiara una volta per tutti i provider (come `MediaAsset.Sha256`) e
   non c'è niente da convertire da nessuna parte. Il primo tentativo le aveva messe nella mappa e il test
   `La_mappa_non_contiene_colonne_che_non_esistono_o_non_sono_indicizzate` l'ha respinto: la mappa accetta
   solo colonne indicizzate o con un DEFAULT, e `DepIcao`/`ArrIcao`/`AircraftIcao` non sono né l'una né
   l'altra. La guardia aveva ragione.

   ⚠️ `dotnet ef migrations remove` sul progetto MySQL **prova a connettersi al database** e fallisce dove
   MySQL non c'è: per rifare una migrazione si cancellano i due file e si ripristina
   `VipiDbContextModelSnapshot.cs` da git.
3. ✅ **FATTA** (24 agosto). Porta neutra `IAtcActivitySource` (+ DTO `SourceAtcConnection`/`SourcePilotFix`/
   `NetworkSnapshot`), adapter `IvaoWhazzupClient`, archivio `IAtcSessionStore`/`EfAtcSessionStore`, e il
   cuore puro `AtcSessionSync` che decide cosa aprire, aggiornare e chiudere (11 test) — turno assegnato
   alla nascita della sessione. Il poller che c'era già passa al whazzup completo: **stessa cadenza, stesso
   numero di chiamate**, in più i piloti.

   **Verifica live, 24 agosto ore 19:00Z**: host avviato in Release su porta libera contro IVAO vero, con
   una copia del `vipi.db`. Due giri: «26 ATC divisione online, 537 piloti nella fotografia», **26 sessioni
   scritte** con posizione, frequenza e durata (`LIMF_TWR` 171 min, `LIMM_WS2_CTR` 108 min, `LIRF_TWR`
   64 min…), 26 turni distinti, zero traffico (è la slice 4). Migrazione applicata da sola all'avvio.

   ⚠️ Tre scelte da ricordare: (a) il ramo dei **callsign finti** non scrive statistiche — un id inventato
   sporcherebbe l'archivio con connessioni mai esistite; (b) la registrazione ha un `try` suo, perché un
   archivio che non risponde non deve spegnere il pallino «in frequenza» di tutte le pagine; (c) la
   decompressione **Brotli** va accesa sull'`HttpClient` o si scaricano 705 KB invece di 119, ogni minuto.
4. ✅ **FATTA** (24 agosto). `SectorVolumeMap` (da «chi è online» a «quali volumi rivendica», copertura
   top-down + volumi dai cataloghi), `TrafficLedger` (registro delle tratte in memoria), `AtcTrafficRecorder`
   (il giro), `ISectorVolumeCatalog`/`IAtcTrafficStore` con le loro implementazioni EF. 28 test nuovi.

   **Verifica live contro IVAO vero**: «25 ATC divisione online, 536 piloti» → **40 aerei attribuiti a 25
   sessioni**. Esempi veri: `THY5ZV` (LTFM→LFPG) finito a `LIPP_CE1_CTR`, cioè un sorvolo che taglia
   l'Italia; `AFR7316` fermo a Torino contato come **presenza ma non movimento**; `LICJ_GND` con 4 presenze
   e 2 movimenti. Nessun aereo attribuito a due sessioni nello stesso istante.

   **La bufferizzazione si vede nei numeri**: primo giro 40 righe scritte, giri successivi **1-3**. Non è il
   ×10 stimato nella carta, è di più — perché in un minuto cambia quasi nulla.

   ⚠️ **Due difetti trovati scrivendo i test, non dopo**: (a) il giro usciva prima di salvare le sessioni
   sparite quando gli unici online erano settori senza poligono — l'ultimo tratto di chi aveva appena
   staccato restava in memoria; (b) `TakeAll` segnava «salvate» anche le sessioni ancora in frequenza, che
   così perdevano il proprio checkpoint. Ora la chiusura prende **solo** le sessioni che sta scrivendo
   (`TakeOnly`).

   ⚠️ **Il patto della bufferizzazione, da ricordare quando si scriveranno le pagine**: chi legge le
   statistiche di una sessione **in corso** vede l'ultimo checkpoint (≤10 minuti), non l'istante. Allo
   spegnimento il poller versa tutto (`StopAsync`), verificato su un arresto vero: quattro giri, quattro
   minuti in archivio.
5. ✅ **Motore fatto** (24 agosto); resta la riga nella policy sorgenti + audit, che è un passo suo perché
   tocca il modello della policy. `IAtcHistorySource` + `IvaoAtcHistoryClient` (paginato, 23 prefissi),
   `AtcHistoryImportUseCase`, `AtcHistoryImportHostedService` (giro giornaliero, bootDelay 70s: ultimo della
   fila), e due metodi nuovi sull'archivio: `UpsertHistoryAsync` e `RecomputeShiftsAsync`. 7 test nuovi.

   **Un corpo solo per due usi**: il primo giro recupera i dodici mesi che la sorgente conserva; i successivi
   ripassano due giorni, per due ragioni — mettere la fine **vera** alle sessioni che il poller ha chiuso a
   occhio («non c'era più al giro delle 21:03»), e recuperare quel che non ha visto perché l'applicazione era
   giù.

   ⚠️ **Sulle righe già viste dal vivo lo storico corregge solo la coda** (fine e durata): non declassa la
   riga a `Backfill`, non tocca frequenza e posizione — che nella *lista* dello storico non ci sono affatto —
   e non tocca il traffico già attribuito.

   ⚠️ **I turni si ricalcolano dopo**, non riga per riga: lo storico arriva alla rinfusa e il turno si vede
   solo sulla sequenza completa. Stesso `AtcShiftGrouper` del poller, così i due percorsi non possono dare
   risposte diverse.

   **Verifica live contro IVAO vero** (finestra di 7 giorni per non aspettare i 12 mesi — stesso codice):
   «334 sessioni lette da 23 prefissi, 311 create, 23 aggiornate, 113 turni corretti». In archivio:
   334 sessioni → **221 turni**, 312 chiuse, e le prime statistiche vere della divisione — `LIPZ_TWR` 24,0 ore
   su 23 sessioni, `LIRF_TW1_APP` 22,9 ore su 10, il controllore più attivo con 33,9 ore in 15 turni. Il 24%
   delle sessioni sta sotto i cinque minuti, coerente col 31,8% misurato sull'API su trenta giorni.
6. ✅ **FATTA** (24 agosto). `IAirportTrafficSource` + `IvaoAirportTrafficClient`,
   `AirportBackfillPlanner` (puro), `AirportTrafficBackfillUseCase`, giro notturno con bootDelay 90s (dopo lo
   storico, che gli crea le sessioni da riempire), colonna `AtcSession.TrafficFilledUtc` con migrazione
   doppia. 20 test nuovi.

   **Il problema che il pianificatore risolve**: la sorgente racconta i movimenti di uno *scalo* in una
   finestra, non a chi hanno parlato. Se in quella finestra c'erano TWR e GND insieme, darli a tutt'e due
   raddoppierebbe i numeri della divisione. Vince la posizione più titolata sul movimento — TWR, poi APP,
   poi GND, poi DEL — e le altre si marcano «provate» senza chiamare la sorgente.

   ⚠️ **`SeenMinutes` resta 0 per queste righe**: la sorgente dice *che* il volo c'è stato, non per quanti
   minuti fosse in frequenza. Un numero inventato sarebbe peggio di un numero assente — e le pagine dovranno
   distinguere `Origin = AirportApi` da `Aor` quando mostrano i minuti.

   ⚠️ **Costa una chiamata per sessione** (la finestra è quella della singola connessione), quindi c'è un
   tetto per giro (200): l'arretrato dell'anno si smaltisce in più notti. Alzarlo accelera e pesa sulla
   sorgente nella stessa misura.

   **Verifica live contro IVAO vero**: «33 sessioni, 25 riempite con 70 movimenti, 8 lasciate a una posizione
   più titolata» — otto doppi conteggi evitati davvero. Voli ricostruiti veri: `EZY2FV` LIRP→LICA su
   `LIRP_APP`, `AZA1430` LIRF→LICJ su `LICJ_TWR`.

   ⚠️ **Difetto trovato guardando il dato vero, non i test**: al primo giro `AZA1430 LIRF→LICJ` compariva
   **due volte** nella stessa sessione. Avevo identificato la tratta con l'id del piano di volo, ma alla
   riconnessione il pilota ne deposita uno nuovo — dal vivo il caso lo copre la finestra temporale, qui non
   c'è. Ora la tratta la identificano **rotta e verso**: 76 movimenti sono diventati 70, e i doppioni sono
   zero.
7. ✅ **FATTA** (24 agosto). `IAtcStatsQueries`/`EfAtcStatsQueries` (letture), `IStatsSettingsStore` +
   entità a riga singola con audit e migrazione doppia, e le tre pagine: `StatsHome`, `StatsSessionPage`,
   `StatsDivisionPage`. 14 test nuovi.

   Le prime due sono **SSR statico** (sono elenchi di numeri, non c'è niente da aggiornare da solo); la terza
   è interattiva solo perché ospita l'interruttore delle classifiche.

   **Verifica live guidando le pagine vere**, con l'archivio delle 334 sessioni reali:
   - da **anonimo** le tre guardie tengono: «entra col tuo account», «la classifica non è pubblica», e il
     dettaglio di una sessione altrui risponde «è di un altro controllore» — l'id indovinato non apre niente;
   - da **loggato** (identità di sviluppo, VID 704798, che in archivio ha cinque sessioni vere): «7,1 ore,
     4 turni, 24 movimenti», per postazione `LIRR_NE1_CTR` 3 sessioni / 7,0 ore / 24 movimenti;
   - **divisione**: 365,3 ore, 214 turni, 172 movimenti, classifica guidata da 727049 con 34,8 ore in 15 turni.

   ⚠️ **Difetto trovato aprendo la pagina, non dai test — perché i test non c'erano**: `ByPositionAsync`
   proiettava il `GroupBy` dentro un `record`, che EF non sa tradurre. Errore a runtime, 500 in faccia.
   Ora la proiezione passa da un tipo anonimo **e** la classe ha dieci test contro un database vero. La
   lezione, scritta perché non si ripeta: *una query non provata contro un database vero è una query non
   scritta.*

   Due dettagli di onestà nella resa: le righe ricostruite (`Origin = AirportApi`) mostrano «—» al posto dei
   minuti, non «0»; e una sessione di venti minuti scrive «<0,1» invece di «0,0 ore», che sembrava «niente».

   **Larghezza (24 agosto sera, su richiesta del committente).** Le pagine avevano un `max-width:1100px`
   copiato dall'hub — su tabelle di dati è spazio buttato. Ora usano `.wrap.stats` (nessun tetto), come le
   pagine di lavoro.

   ⚠️ **Ma larghezza piena non vuol dire tabelle stirate**, e si è visto solo guardando la foto: con quattro
   colonne su 2560px il numero finisce lontanissimo dalla sua etichetta e l'occhio deve viaggiare. Uno
   schermo largo deve mostrare **più cose**, non le stesse allargate. Le tabelle corte (per postazione, per
   mese, classifica) si affiancano in `.stats-cols`; quella lunga delle sessioni resta sotto a tutta
   larghezza. Misurato con Edge su tre viewport: a 2560 la pagina di divisione mostra classifica e postazioni
   **fianco a fianco, 40 righe in una schermata**; a 1366 le due tabelle stanno ancora affiancate (642px
   ciascuna) e i riquadri dei totali vanno a capo da soli; **nessuno sforamento orizzontale** a nessuna misura.

8. ✅ **FATTA** (24 agosto). Card nell'hub `/services` (terzo servizio, allo stesso livello degli altri due),
   card nella home della documentazione, voce nel menù ☰, voce nella ricerca globale/guida
   (`GuideSearchCatalog`). Verificato servendo le pagine: la card compare nell'hub, la ricerca di
   «statistiche» trova la voce.

   ⚠️ Nel file di lingua **italiano** era finita la frase **inglese** della card: visto solo guardando la
   pagina resa, perché a compilare non dà nessun errore. Corretto, e passato in rassegna tutto il blocco
   nuovo di stringhe per lo stesso scambio (nessun altro caso).

## 10. Trappole già note da non ripetere

- `dotnet build Vipi.slnx -c Release --no-incremental` su **entrambi i TFM**: gli avvisi sono errori e
  `dotnet test` non lo vede (net8 = niente C#13+/API .NET9+).
- I servizi con cache registrati `AddScoped` in Blazor vivono **quanto il circuito**: una cache di sessioni
  che invecchia per ore è il difetto classico.
- Un `DbContext` condiviso dal circuito più un render intermedio = «second operation»: le pagine che leggono
  in `OnInitializedAsync` usano `OwningComponentBase`.
- Attributo componente di tipo `string` senza `@` è un **letterale**: `Key="x"` ≠ `Key="@x"`.
- Se si aggiunge un pacchetto: `packages.lock.json` rigenerato e committato, o la CI si ferma.
- **Un `FirstOrDefault` su una tabella «a riga singola» va ORDINATO** (25 agosto, commit `d523037`). EF lo
  segnala da solo — `Microsoft.EntityFrameworkCore.Query[10103]`, evento
  `CoreEventId.FirstWithoutOrderByAndFilterWarning` — e ha ragione: la riga singola è una **convenzione, non
  un vincolo**, e «la prima riga» senza ordine la sceglie il motore, con MariaDB libera di cambiare idea fra
  una chiamata e l'altra. Nel caso di `ImportPolicies` quella riga decide il **regime di scrittura di tutta
  l'applicazione**. ⚠️ Si ordina per `Id`, **non** si filtra `Id == 1`: una riga nata in produzione con un Id
  diverso sparirebbe, e l'app tornerebbe ai default senza dirlo a nessuno. (`EfStatsSettingsStore` non ha mai
  stampato l'avviso perché lì il filtro c'è: è il confronto che ha identificato il colpevole nel log.)
- **Un test che non diventa rosso quando si rimette il difetto non è una prova.** Stesso giro: il test scritto
  per inchiodare l'avviso puntava `RowLimitingOperationWithoutOrderByWarning` — che parla di `Skip`/`Take` —
  e passava verde anche col `FirstOrDefault` nudo rimesso apposta. Il nome giusto è
  `FirstWithoutOrderByAndFilterWarning`. **Rimettere il difetto e vedere il rosso** è l'unico modo di sapere
  che il test guarda dove crede.
- ⚠️ **`Vipi.Application.Tests` su net10 ha segnato un rosso irriproducibile** il 25 agosto (763/764) al primo
  di quattro giri pieni; i tre successivi e il progetto lanciato da solo sono verdi. Il nome del test è
  **perso**, perché il filtro sull'output teneva solo le righe di riepilogo. Se ricapita: lanciare la suite
  salvando l'output intero (`> giro.log 2>&1`), non filtrandolo al volo.

## 11. Dopo la consegna: cosa aggiungere (elenco vivo)

Elenco tenuto qui perché la domanda «cosa manca?» torna, e la risposta cambia con quel che si impara.
Ordinato per **quanto costa il dato**, non per quanto è bella l'idea.

### ✅ Fatto

- **La diagnostica delle shape** (25 agosto). Tre rilievi nell'area nuova `Sorgente`: contorno ripetuto,
  settore senza poligono, cerchio sintetico. Sul `vipi.db` reale accende **29 righe** — 16 shape sintetiche,
  11 settori senza poligono, 2 contorni ripetuti (`LIRR_TS_CTR` e `LATI_APP`). È la riga che sarebbe servita
  ieri: un settore che non attribuisce traffico ora lo dice, invece di aspettare un occhio umano su una
  vista 3D.

### ✅ Fatto (seguito, 25 agosto)

- **La griglia ora × giorno**, per la divisione (copertura) e per la persona (quando controlli).
  ⚠️ Due cose che sembrano dettagli: gli intervalli si **uniscono** prima di contare (tre controllori insieme
  fanno un'ora coperta, non tre) e una sessione **occupa tutte le ore che attraversa** (20:40→23:10 non è
  «alle 20»). E ⚠️ **la finestra si stringe al periodo di cui abbiamo davvero dati**: chiedere dodici mesi a
  un archivio che ne contiene uno dava «2%» in ogni casella — vero, inutile e scoraggiante. Sul dato reale la
  griglia dice quel che deve: coperto quasi al 100% dalle 9 alle 22 UTC, **vuoto dalle 0 alle 7**.
- **I tuoi aeroporti e i tuoi aeroplani**: sul dato vero LIRF 6 · LIRN 5 · LICJ 4 · LIMF 4 · LFLL 3, e
  A320 8 · B738 5. Un volo conta per **tutti e due** gli scali: la domanda è «quali aeroporti ti passano
  davanti», non «da dove partivano».
- ~~**Esportazione CSV**~~ — ⚠️ **RIMOSSA il 25 agosto 2026** (richiesta del committente): tasto, endpoint,
  chiavi di risorsa e riga della mappa delle pagine. Il test che sorvegliava «niente esportazione altrui»
  è rimasto e ora sorveglia «niente esportazione, punto».
- **Le piste in uso, come sequenza.** 48 ATC su 71 la nominano nel testo dell'ATIS, che la fotografia già
  porta: nessuna chiamata in più. `AtisRunways` legge la frase
  («*Arrival runway 16L 16R departure runway 25*», «*Runway in use 04R*»); `AtcSessionRunway` conserva **una
  riga per cambio**, non un valore. Una configurazione che torna (16 → 34 → 16) sono tre righe: la sequenza
  racconta il turno. La **lettera** ATIS non si conserva: cambia a ogni bollettino e non dice niente sul
  lavoro fatto. ⚠️ Verificato con i test contro il database vero e **non** dal vivo: a quell'ora non c'era
  **nessun** ATC italiano online (0 su 444 piloti nella fotografia), quindi non c'era niente da registrare.

### ✅ Fatto (la veste, 25 agosto — vedi §13)

- **Le targhette del traffico**, la costanza, la striscia del turno, i grafici, il periodo scelto
  dall'indirizzo, il podio, le tabelle che diventano schede sul piccolo. Tutto in §13.

### Segnato, da ragionare (non ora)

- **La mappa del traffico gestito** nel dettaglio sessione: `/v2/tracker/sessions/{id}/tracks` dà la traccia
  completa di un volo, da mettere sopra il poligono del settore. Una chiamata per volo, quindi su richiesta e
  con cache. Deciso il 25 agosto di **non** farla adesso.

### Aperte, ma non tecniche

- **I nomi nelle classifiche** restano il VID finché la gente non fa login da noi (§8.2).
- **«Chi non controlla da tre mesi»**: già calcolabile, utile al tutoraggio, ma è una lista di persone e la
  decisione è del committente.

## 12. Cosa resta (stato al 25 agosto 2026)

Il servizio funziona e i numeri sono veri. Quel che segue è **tutto** ciò che non è chiuso, verificato riga
per riga il 25 agosto: se un giorno questa sezione risulta vuota, il servizio è finito davvero.

### Prima di fondere in `main`

- **Niente lo blocca sul piano tecnico**: suite verde e `dotnet build Vipi.slnx -c Release
  --no-incremental` pulita su entrambi i TFM. La fusione è una decisione del committente, non un passo
  tecnico rimasto indietro.

  ⚠️ Conteggi misurati il **25 agosto, dopo §13**: **2237 su net8, 1999 su net10**.

  ⚠️ E la trappola che ci sta dietro, perché costa un quarto d'ora ogni volta: **un progetto che non
  COMPILA non compare nel totale, e `dotnet test -v q` non diventa rosso in modo visibile.** Con
  `Vipi.Host` acceso (la verifica live!) i suoi DLL sono bloccati, la build di mezzo albero fallisce con
  `MSB3021`/`MSB3027`, e il totale risulta più basso di qualche centinaio senza che manchi un test.
  Prima di credere a un conteggio: `grep "error MSB"`. I numeri della prima consegna (2176/1938) sono di un
  altro giro e non tornano con questi: non inseguirli.
- ✅ **La Guida c'è** (25 agosto sera, §16). Il capitolo `statistiche` in `GuidaPage.razor`, IT ed EN.
  ⚠️ E la diagnosi di prima era **sbagliata a metà**: la voce in `GuideSearchCatalog` c'**era** già, e
  puntava a un'ancora che nella Guida non esisteva — chi cercava «statistiche» trovava un risultato, lo
  apriva e finiva su una pagina senza quel capitolo. Un collegamento morto è peggio di nessun collegamento,
  perché nessuno lo denuncia. C'è ora un test (`GuidaAncoreTests`) che verifica che **ogni** voce del
  catalogo abbia il suo capitolo.

- ✅ **La potatura del dettaglio traffico è scritta** (§16): `TrafficRetentionUseCase` +
  `TrafficRetentionHostedService`, a scaglioni e con tetto per giro.

### Al primo deploy in produzione (MariaDB)

- ⚠️ **La `UPDATE` dei tetti TWR** (§4.5-bis) è stata eseguita **solo sul `vipi.db` di sviluppo**. La stessa,
  con la stessa guardia (`Position='TWR' AND LimitsFromSource=0 AND UpperLimit=19500`), va data là: senza,
  in produzione le torri continuano a rivendicare fino a FL195 e il traffico in crociera finisce a loro.
- Le **sei migrazioni** del servizio, tutte a doppia emissione: `StatisticheAtc`, `PolicyStatisticheAtc`,
  `TrafficoRiempitoAPosteriori`, `ImpostazioniStatistiche`, `PisteInUso`, `FasiQuoteConsegne` (§13).

### Non ancora provato dal vivo

- **Le targhette di fase e le consegne** (§13). Il motore è coperto da test contro un database vero — fasi e
  quote fino all'archivio, consegna scritta su tutt'e due le sessioni, poller fermo che non la inventa — ma
  **nessuna riga vera le ha ancora**: le colonne nascono adesso e si riempiono dal primo turno campionato dal
  vivo. Sulle righe già in archivio restano vuote, e la pagina in quel caso non scrive targhette di fase (è
  la stessa regola delle righe ricostruite: se non l'abbiamo visto, non si dice). Verifica: aprire una
  sessione registrata dopo il deploy e controllare che i voli portino «decollato»/«atterrato»/«consegnato a».

- **La sequenza delle piste in uso.** Coperta dai test contro un database vero, ma **mai vista girare su un
  ATIS reale**: in tutt'e due i momenti in cui si poteva provare non c'era **nessun** ATC italiano collegato
  (0 su 444 piloti, poi 0 su 422). Si riempie da sé al primo turno vero; la verifica è aprire
  `/services/stats/session/{id}` di una sessione con ATIS e vedere le righe in ordine di orario.

### Deciso sulla carta, non ancora scritto nel codice

- ~~La potatura del dettaglio traffico~~ — ✅ **scritta il 25 agosto sera**, vedi §16.4.

## 13. La veste (25 agosto 2026)

La prima consegna aveva l'aspetto di quel che era: quattro riquadri di numeri e sei tabelle. Il committente
ha chiesto di renderla presentabile e, nello stesso giro, «una targhetta che dica chi è atterrato e chi no».
La seconda richiesta ha cambiato anche il modello dati; la prima no.

### 13.1 La regola che governa tutte le targhette

**Una targhetta dice quel che abbiamo VISTO, non quel che doveva succedere.** Un volo con arrivo `LIRF` che
esce dalla nostra area ancora in volo **non è «atterrato»**: è «uscito in volo», o «consegnato a LIRR_NE1_CTR»
se sappiamo chi l'ha preso. È la stessa regola dei minuti contati per giro e del trattino al posto dello zero
sulle righe ricostruite: il servizio misura, non racconta.

Le voci, e come si ricavano (`TrafficStory`, puro e con 13 test):

| targhetta | condizione |
|---|---|
| in partenza / in arrivo | il piano di volo tocca il campo della postazione (`LIRF_TWR` → `LIRF`) |
| sorvolo | né partenza né arrivo dentro i prefissi di divisione |
| decollato | prima fase a terra, poi visto in volo |
| **atterrato** | visto **in volo** e poi **al suolo** |
| al parcheggio | ha volato ed è finito fermo (arrivato ai blocchi) |
| consegnato a X | uscito in volo, e al giro dopo era di X |
| uscito in volo | uscito in volo, e dopo di noi nessuno |
| solo rullaggio | si è mosso ma non l'abbiamo mai visto volare |
| fermo | non si è mai mosso (era già lì) |

⚠️ **Il prefisso di un ACC non è un aeroporto.** `LIRR_NE1_CTR` comincia per `LIRR`, che è un codice di FIR:
se lo si trattasse come ICAO nascerebbero «arrivi a LIRR» che non esistono. `TrafficStory.StationIcao`
riconosce solo TWR/GND/DEL/APP/AFIS.

### 13.2 Che cosa è servito nel dato (e che cosa no)

Niente di tutto questo era derivabile da quel che c'era: sulla riga restava il solo `SawMovement`, che non
distingue una partenza da un arrivo da un sorvolo. La fase, però, il recorder **la calcolava già a ogni
giro** e la buttava. Quindi:

- `FirstPhase`, `LastPhase`, `SawAirborne` — la fase vista, non dedotta;
- `EntryAltitudeFt`, `ExitAltitudeFt`, `MaxAltitudeFt` — per un CTR è il numero che racconta il volo;
- `HandoffToSessionId`, `HandoffFromSessionId` — chi l'ha preso dopo di noi, e da chi l'abbiamo avuto.

**Zero chiamate in più alla sorgente**: erano tutti dati già in memoria al momento del giro.

⚠️ **La consegna si scrive solo fra due giri consecutivi** (finestra 2,5 minuti, `AtcTrafficRecorder.HandoffWindow`).
Senza quella finestra, un poller fermo un'ora scriverebbe «consegnato a…» ogni volta che, tornando su, un
aeroplano si trova sotto un altro controllore: «prima era tuo e ora è suo» è un buco di osservazione, non un
passaggio. ⚠️ E **nessuna chiave esterna** sui due id: la potatura del dettaglio (§5.1) cancellerà righe
vecchie, e una FK farebbe cadere la consegna insieme alla riga dell'altro. Un id che non risolve più si
mostra senza collegamento — c'è un test apposta.

### 13.3 La striscia del turno, e perché la punta è «stimata»

Il dettaglio sessione ora apre con una **striscia**: il tempo da sinistra a destra, un volo per barra, i
cambi di pista come linee tratteggiate. Le corsie le assegna `TrafficTimeline` con l'algoritmo dei binari di
stazione (la prima libera), quindi una TWR da quaranta voli in tre ore occupa una manciata di righe, non
quaranta.

⚠️ **La barra è la finestra fra primo e ultimo avvistamento, non la presenza.** Chi esce dal settore e
rientra ha una barra continua e minuti contati per giro: la punta di traffico simultaneo che ne esce è
**stimata**, e la pagina lo scrive accanto al numero invece di lasciarlo credere esatto. Contare la presenza
vera vorrebbe dire conservare un campione al minuto per volo — mezzo milione di righe l'anno che ne
diventerebbero trenta.

⚠️ Le righe **ricostruite** dai movimenti d'aeroporto non entrano nella striscia: non hanno una finestra
vera (primo = ultimo avvistamento) e sarebbero puntini a caso su un disegno che racconta il campionamento
dal vivo.

### 13.4 La costanza (settimane di fila)

`ControllerStreak`, puro. Settimana = giorni dal lunedì dell'epoca, non settimana ISO: due settimane
consecutive differiscono di uno e il capodanno non spezza niente (la settimana 1 dopo la 52 di un altro anno
è una trappola classica, e c'è un test che ci passa sopra).

⚠️ **La striscia resta viva anche se in questa settimana non si è ancora controllato.** Senza quella regola
ogni lunedì mattina la striscia di tutti tornerebbe a zero — falso, e per giunta scoraggiante.

### 13.5 Le scelte visive che non sono gusto

- **Il periodo sta nell'indirizzo** (`?p=30`), non in un componente interattivo. È ciò che tiene la pagina
  personale **SSR statica**: un filtro con `@rendermode` vorrebbe un circuito Blazor per ogni lettore di una
  pagina che è un elenco di numeri fermi. In più il tasto «indietro» funziona e un periodo si può mandare a
  qualcuno. Stessa scelta per il filtro del traffico (`?f=arr`).
- **Il confronto è con un periodo lungo uguale**, che finisce dove comincia quello mostrato. Confrontare
  trenta giorni con dodici mesi darebbe un «−96%» che non vuol dire niente. Quando il periodo prima è vuoto
  la variazione **non si mostra**: chi comincia adesso non ha un «prima», e «+100%» sarebbe inventato.
- **La variazione non è un voto.** Freccia per il verso, colore che accompagna, nessun rosso: «hai
  controllato meno del mese scorso» non è un errore.
- **Un turno per riga, non una connessione.** Il 38% delle connessioni sono spezzoni di una caduta di linea
  (§ modello dati): in elenco sembravano turni distinti. Ora la riga è il turno e gli spezzoni stanno dentro,
  aperti da chi li vuole.
- **Colore E testo, sempre.** La ciambella ha la legenda con le percentuali in cifre, la griglia ora×giorno
  ha ora anche la **legenda della scala** (senza, l'intensità era decorazione).
- ⚠️ **`@container`, non `@media`.** Sotto i 700px le tabelle diventano schede — ma la soglia guarda la
  larghezza del **contenitore**, non della finestra: lo zoom di questo prodotto è `zoom` sull'`<html>`, e una
  media query non lo vede (a zoom 1.8 su 1280px il documento sta in 711px e la media query legge ancora
  1280). È la stessa trappola già pagata sul viewer.
- **Nessuna libreria di grafici.** Sparkline e ciambella sono SVG inline, le barre per mese sono HTML (così
  le etichette restano testo vero: ricerca nella pagina, lettori di schermo, stampa). Una dipendenza esterna
  non passerebbe la CSP, e dodici punti non la meritano.
- ⚠️ **I numeri dentro un attributo SVG si scrivono con `StatsView.Svg`**: a cultura italiana una virgola
  decimale dentro `points` spezza il disegno in silenzio.
- **Un colore per tipo di postazione** (`--pos-del/gnd/twr/app/ctr`), come le categorie di navigazione: sono
  un insieme categoriale, il loro mestiere è distinguersi fra loro.

### 13.6 Sette difetti che solo lo schermo ha detto

La suite era verde e la Release pulita **prima** di guardare le pagine. Poi la verifica live
(`.claude/skills/verifica-live/`, con una sessione di prova seminata nella copia del `vipi.db`) ne ha tirati
fuori sette in un colpo — tutti invisibili ai test, sei su sette di sostanza e non di gusto:

1. l'etichetta del **cambio pista** cadeva **sopra** la prima barra della striscia (illeggibili tutt'e due);
2. la barra dentro la cella, col 13% e gli angoli tondi, sembrava un **campo di testo** dentro la tabella;
3. la quota di un aeroplano mai decollato diceva **«0 ft»** invece di un trattino;
4. **FSS aveva il colore del CTR**: due voci, un colore, nella stessa legenda;
5. sul piccolo, un callsign con «ricevuto da» sotto si spezzava **a metà parola** (due colonne di flex);
6. la **ciambella** si calcolava sulle prime venti postazioni invece che su tutte: percentuali di una
   ripartizione che non era quella del tempo — e nessuno se ne sarebbe accorto;
7. il quarto riquadro della divisione contava le **righe della classifica** (tagliate a cinquanta) e le
   chiamava «controllori»; ora è il totale vero.

Più due targhette («consegnato a…», «al parcheggio») che sul **tema chiaro** avevano un fondo così pallido
da sembrare testo nudo. La battuta sul tema scuro (`sweep.js`) non ha invece trovato niente sulle tre pagine
nuove: due soli sospetti, quelli noti e attesi.

### 13.7 Il ritocco che non c'entrava con la grafica

Le stringhe italiane del servizio erano state scritte **senza accenti** («non e pubblica», «Visibilita»,
«c'e un buco»): nove valori corretti in `SharedResource.resx`. Non è cosmesi di contorno — era italiano
sbagliato a schermo, nella stessa pagina che si stava rifacendo.

### 13.8 L'ottavo, e l'ha detto il committente

Il numero nel **buco della ciambella** finiva **sopra l'anello** — ma solo su `/division`, e per questo la
verifica live non l'aveva preso: si erano guardate le pagine con le ore di **una persona**.

Il buco è largo **69 unità** del viewBox (r 42 meno mezza traccia da 15 per parte) e il corpo era **fisso a
19**: cinque cifre ci stanno («1234,5»), sei no. Le ore di una persona sono sempre corte; quelle di una
divisione no — «12345,6» misura circa 80 unità. `StatsDonut.FontCentro` ora ricava il corpo dalla lunghezza
del testo (mai oltre 19, mai sotto 11), con i casi limite fissati in un `[Theory]`.

⚠️ La lezione non è «rimpicciolire il testo»: è che un componente provato **solo coi numeri di una persona**
non è provato per la divisione, e che i due usi vivevano nello stesso file senza che nulla lo dicesse.

## 14. Le statistiche di un altro (25 agosto 2026)

Chiesto dal committente: «può lo staff accedere alle statistiche personali di una persona?». Sì, e **tutto
lo staff IT-** — cioè `Authz.IsAdmin`, che col jolly `^IT-[A-Z0-9]+$` è l'intera struttura di divisione, non
un sottoinsieme di cariche.

### 14.1 Perché non era «solo una pagina in più»

Il §6 aveva già deciso che le classifiche sono un **interruttore**, spento finché lo staff non decide: le ore
degli altri non si mostrano a nessuno per default. La pagina personale è però **più** della classifica, e la
differenza va detta perché non si vede dall'elenco delle rotte:

| La classifica dà | La pagina personale aggiunge |
|---|---|
| ore, turni, movimenti | la **griglia ora × giorno**: *quando* quella persona è di solito online |
| | la costanza (settimane di fila), la postazione preferita |
| | l'elenco dei singoli turni con gli orari |

La classifica dice quanto una persona ha controllato; questa dice **le sue abitudini**. Da qui le tre cose
che accompagnano il permesso, e senza le quali non sarebbe stato fatto.

### 14.2 Le tre cose che accompagnano il permesso

1. **La guardia sta prima di ogni query.** ⚠️ Non davanti al markup: una guardia messa dopo le letture
   nasconde i numeri a schermo e li ha comunque **già tirati fuori dal database**. Il test lo verifica con
   un `IAtcStatsQueries` che **esplode a ogni metodo** — se la guardia scivola in basso, il test scoppia.
   Vale anche per l'anonimo, per cui il divieto è la risposta giusta: «entra e poi vedrai» prometterebbe a
   chiunque una pagina che quasi nessuno può aprire.
2. **La fascia in testa** (`callout info`, icona `eye`) dice a chi guarda che sta guardando un altro, e che
   l'accesso è registrato. Non è cortesia: è la sola cosa che rende la regola visibile a chi la applica.
3. **Una riga di audit per consultazione.** `AuditAction.View` — l'unico valore dell'enum che descrive una
   **lettura** — su `EntityType = "StatsProfile"`, con `EntityId` = il VID **guardato** e l'attore nel campo
   `UserId`. Porta `IStatsAccessLog`, impl. `EfStatsAccessLog`.
   ⚠️ **Accorpata per trenta minuti.** La pagina è SSR statica e si ricarica a ogni chip di periodo e a ogni
   F5: senza finestra, una consultazione diventava venti righe identiche a mezzo minuto l'una dall'altra, e
   un registro così non si legge — che è come non averlo. L'accorpamento è per **coppia** attore→soggetto:
   due staffisti che guardano la stessa persona restano due accessi da spiegare.

Nell'enum `AuditAction` il valore è **additivo e senza migrazione**: gli enum sono salvati come stringa
(§SPEC 6), quindi «View» non sposta nessun numero già scritto.

### 14.3 Una pagina sola per due indirizzi

`StatsHome.razor` porta due `@page`. Due pagine gemelle si sarebbero **scollate al primo grafico** aggiunto a
una sola delle due — che è esattamente il modo in cui il pannello «storia» e la pagina Audit erano finiti a
raccontare lo stesso evento in due modi (§AuditNarrator). Qui cambia il **VID di partenza**, non il
contenuto; a cambiare sono solo le frasi in prima persona («Quando controlli» → «Quando controlla») e il
titolo.

⚠️ I **chip del periodo** devono ripartire dalla persona guardata: con l'indirizzo fisso `/services/stats`
il primo chip premuto riportava lo staffista sulle **proprie** statistiche, in silenzio. C'è un test.

### 14.4 Quel che è rimasto fuori, e perché

- **L'esportazione CSV resta la sola propria.** L'endpoint legge il VID dall'identità e non prende parametri
  (§7): là non ci sarebbero né la guardia né l'audit. Chi vorrà l'esportazione altrui deve portarsi dietro
  entrambe, non aggiungere un parametro. Sulle statistiche di un altro il link **sparisce**.
- **Il nome del soggetto.** Lo sa solo il roster staff, popolato ai login: di un controllore qualunque
  abbiamo il **solo VID** — che è già un link al profilo IVAO, dove il nome c'è. Nessun ripiego inventato,
  come dice il §6.
- **Avvisare chi viene guardato: NO, e non è un rinvio.** Deciso dal committente il 25 agosto: su IVAO lo
  staff guarda le statistiche dei soci senza doverlo annunciare, e questo servizio non introduce una regola
  che altrove non c'è. Nessun avviso all'interessato, né in pagina né altrove.
  ⚠️ **La riga di audit resta**, e non è in contraddizione: serve alla divisione — «chi ha guardato chi e
  quando» davanti a una contestazione — non a informare il guardato. Chi un giorno volesse togliere anche
  quella starebbe togliendo una cosa diversa da quella che è stata decisa qui.

### 14.5 Come ci si arriva

Dalla classifica di divisione: una lente (`StatsPeekLink`, icona `activity`) accanto al VID, nel podio e in
tabella, **visibile solo agli admin**. Il numero continua a portare al profilo IVAO — due destinazioni
diverse per due domande diverse. La lente non è una guardia: la guardia è nella pagina di destinazione, e
scrivere l'indirizzo a mano non la aggira.

## 15. Aeroporti gestiti e aeroporti visti (25 agosto 2026)

Il committente ha letto «I tuoi aeroporti» e ha chiesto: non dovrebbero essere **quelli che copri**, per
capire quanto traffico si è fatto sui campi gestiti? La domanda ha scoperto che il pannello ne rispondeva
un'altra — e che tutt'e due servono.

### 15.1 Due domande opposte, due tabelle affiancate

| | Chiave | Risposta a |
|---|---|---|
| **Aeroporti gestiti** | l'ICAO del **proprio callsign** (torre/avvicinamento) o gli aeroporti **dentro il poligono** (area) | «quanto traffico ho fatto sui campi che coprivo» |
| **Aeroporti visti** | i **due capi** del piano di volo di ogni traffico attribuito | «quali campi mi passano davanti» |

Stanno **affiancate**, non su due righe della pagina: separate sarebbero state lette come «la stessa tabella
due volte, con numeri diversi». Ognuna porta la sua riga di spiegazione sotto il titolo, perché due tabelle
gemelle con le stesse tre colonne non si distinguono dal solo titolo.

Il vecchio titolo «I tuoi aeroporti» è diventato **«Aeroporti visti»**: con la tabella nuova accanto,
«tuoi» non diceva più quale delle due.

### 15.2 Cosa conta come traffico «di» un campo

Le tratte **da o per** quel campo, non tutte quelle attribuite alla sessione.

⚠️ Un **sorvolo vettorato** mentre si copriva LIRF non è traffico *di* LIRF, e fuori di lì non lo è per
nessuno: resta però nei **totali** in cima alla pagina e nei movimenti della sessione, perché gestito lo è
stato. È la distinzione chiesta esplicitamente dal committente, e le due letture non vanno riconciliate: la
somma della colonna «Voli» degli aeroporti gestiti **non** è il totale dei voli gestiti, e non deve esserlo.

⚠️ Un **LIRF→LIRF** (circuito, rientro) conta **una** volta: il controllo è uno per tratta, non uno per capo.
Nella tabella degli aeroporti visti la stessa tratta conta invece due volte, una per capo — ed è voluto lì.

### 15.3 Il campo di un settore d’area lo dice la geometria

Per le postazioni d’aeroporto il campo sta nel **callsign**, via `TrafficStory.StationIcao`: solo `_TWR`,
`_GND`, `_DEL`, `_APP`, `_DEP`, `_AFIS`. `LIRR_NE1_CTR` comincia per `LIRR`, che è una **FIR** — prenderla
per un aeroporto farebbe nascere «arrivi a LIRR» che non esistono (stessa trappola del §13.1).

Per i settori d’area la risposta è **quali aeroporti cadono dentro il loro poligono**, con
`PolygonGeometry.Contains` — la stessa funzione che usa l’attribuzione del traffico, non una seconda regola
che si scollerebbe dalla prima.

#### Perché la geometria e non l’albero dei settori

L’alternativa era la catena `Airport.ParentCallsign` → `Sector.ParentSectorId`. È stata **misurata sul
`vipi.db` vero**, non stimata, il 25 agosto 2026:

| | Albero | Geometria |
|---|---|---|
| copertura | **31 aeroporti su 93** hanno un padre | **84 su 93** hanno le coordinate |
| | **12 CTR su 140** hanno qualcosa sotto | **153 poligoni ACC su 153** |

`Airport.ParentCallsign` è un campo che l’admin compila **a mano** in `/services/vsop/admin/sector-structure`,
e a oggi è compilato per un terzo degli aeroporti: avrebbe dato un elenco vuoto a quasi tutte le sessioni
d’area, cioè uno **zero che sembra un dato**. I nove aeroporti senza coordinate non sono invece una perdita:
tre sono voci di FIR/TMA («Roma TMA», «Milano TMA», «Apulia») che aeroporti non sono, e i sei restanti sono
campi minuscoli (Volterra, Piacenza, Classe, Casarsa, Tortolì, Parco Livenza).

#### ⚠️ Il prezzo: i numeri passati possono cambiare

Il poligono è quello di **oggi**. Una risettorizzazione sposta un confine e un turno di marzo guadagna o
perde aeroporti. La tabella è quindi **stabile per torre e avvicinamento** (l’ICAO sta nel callsign, che è
storia) e **rivedibile per l’area**.

L’alternativa — due colonne su `AtcSessionTraffic` scritte al poll, che congelano il fatto — è stata
**scartata dal committente il 25 agosto 2026**: vale solo da lì in avanti, lascia vuoto tutto lo storico già
raccolto, e chiede una migrazione per un dato che cambia una volta ogni risettorizzazione. Chi la volesse in
futuro trova qui il perché non c’è.

#### Quando l’elenco resta vuoto

Un settore d’area **senza poligono** non porta aeroporti, e non se li inventa dal prefisso. Stessa cosa se
nessun capo del traffico gestito cade in area. In tutt’e due i casi la pagina lo **dice a parole**: un vuoto
muto sembrerebbe un dato mancante.

### 15.4 Una nota sul costo

`ManagedAirportsAsync` legge **prima le sessioni**, poi il traffico. Il punto-nel-poligono si calcola una
volta per **settore** e non per tratta: sono un centinaio di aeroporti per una manciata di callsign, mentre
le tratte di un anno sono decine di migliaia. Con l’ordine sbagliato — un `Contains` dentro il ciclo delle
tratte — la stessa risposta costerebbe due ordini di grandezza in più.

## 16. Otto richieste del committente (25 agosto 2026, sera)

Il committente ha guardato il servizio a schermo e ha chiesto otto cose. Sei sono ritocchi, una è una
funzione nuova (§16.3) e una era una domanda a cui bastava rispondere (§16.7). Più quattro aggiunte
proposte da qui e approvate.

### 16.1 La sparkline non diceva di che cosa fosse la forma

«Sulle ore in alto a sinistra, dove c'è il grafico, non si capisce cosa indichi.» Era vero: dodici punti
senza asse, senza etichette e senza un titolo per punto. Ora `StatsSpark` porta una **linea di base**, una
**tacca per punto**, le **due estremità scritte sotto** («08/25 … 08/26») e una didascalia che dice che cosa
misura («per mese»). Ogni punto ha una fascia invisibile col suo titolo, che è il solo bersaglio che il
mouse possa prendere.

⚠️ **Le etichette sono HTML, non testo SVG**, e i punti sono **trattini verticali, non pallini**: il disegno
è stirato (`preserveAspectRatio=none`) perché deve riempire la card, e lì dentro un cerchio diventa
un'ellisse e le lettere si allargano. Per la stessa ragione il tratto vuole `vector-effect`.

⚠️ Metà di questa modifica è stata **invisibile fino allo schermo**: le tacche erano disegnate con
`--line`, che sul tema scuro è a un soffio dal fondo della card. Disegnate e invisibili.

⚠️ Il modello dei punti è **un elenco solo** (`StatsPoint(Value, Label, Title)`) e non tre paralleli: tre
liste da tenere allineate a mano si scollano al primo filtro applicato a una sola.

### 16.2 «Quando controlli»: via la griglia, due grafici

La griglia 7×24 con le percentuali dentro non piaceva. Fra quattro alternative il committente ha scelto le
**due domande separate**: «a che ora» (24 barre) e «che giorno» (7 barre), più una frase in testa che dice
l'orario tipico. `HourDayProfileBuilder` è puro e ricava tutto dalle stesse 168 celle.

⚠️ **Si perde l'incrocio** — «il giovedì sera» non si legge più — e per questo la griglia **resta sulla
pagina della divisione**, dove la domanda vera è proprio quella incrociata: dove manca qualcuno.

⚠️ La **fascia tipica è circolare**: chi controlla dalle 22 all'una ha un'abitudine sola, e una finestra non
circolare gliela spezzerebbe ai due bordi del giorno facendo scrivere «di solito fra le 00 e le 23» — vero e
inutile. Si contano i **minuti**, non le caselle accese.

⚠️ Due difetti che solo lo schermo ha detto, tutti e due nella modalità densa a 24 colonne:

1. l'etichetta c'è solo ogni tre colonne, e una `<span>` vuota è **alta zero**: le colonne senza etichetta
   scendevano tredici pixel più in basso delle altre e **le barre non erano più confrontabili**;
2. lo spazio riservato al numero sopra la barra (il 18% dell'altezza) restava vuoto, perché in modalità
   densa il numero non si scrive: un quinto del riquadro sprecato e le barre schiacciate in fondo.

### 16.3 Aeroporti: traffico e copertura (la funzione nuova)

«È possibile vedere per aeroporto, o per gruppo di aeroporti, il traffico e quanto di questo sia stato
coperto da ATC?» Sì, ed è la sezione **Aeroporti** dentro `/services/stats/division`, **solo staff**.

#### ⚠️ La carta diceva una cosa falsa sulla sorgente

§3 dava `/v2/airports/{icao}/stats` per «conteggi giornalieri di movimenti». **Rimisurato il 25 agosto col
token vero: non lo è.** È una fotografia al minuto dello stato corrente, e `limit` deve stare **sotto 100**
(`limit=400` → `400 Should be lower than 100`): al massimo un'ora e mezza di storia.

Quel che serve ce l'aveva già `/traffics`, che usiamo dal 24 agosto e **regge finestre lunghe**:

| finestra su LIRF | esito |
|---|---|
| 1 giorno | 41 in · 69 out · 0 sorvoli — 60 KB, 0,5 s |
| 7 giorni | 216 · 248 · 1 — 254 KB, 0,6 s |
| **30 giorni** | **863 · 926 · 3 — 981 KB, 1,3 s** |

E ogni volo porta gli **istanti** (`createdAt`, `lastTrack.timestamp`) che il nostro client **buttava**.
Quindi la funzione non ha voluto **nessun endpoint nuovo**: si è smesso di scartare due campi.

#### La regola dell'istante, che va detta e non nascosta

La sorgente non dichiara l'istante del movimento. Quindi: **arrivo → l'ultimo avvistamento** (era su quel
campo); **partenza → il collegamento del pilota** (l'istante più vicino al decollo che esista). È
un'**approssimazione**, e la pagina la scrive: «con ATC» vuol dire *c'era un controllore su quel campo in
quell'istante*, non *quel volo è stato lavorato* — la seconda non è misurabile senza campionare ogni volo
ogni minuto, cioè mezzo milione di righe l'anno che ne diventerebbero trenta.

#### Come è fatto

- `AirportCoverage` (puro): conta i movimenti e quanti cadono in un'apertura. Ricerca binaria sugli
  intervalli — con una scansione lineare per ognuno di decine di migliaia di movimenti costerebbe due ordini
  di grandezza in più. Il **sorvolo non è un movimento del campo**; un LIRF→LIRF conta **due volte** (una
  partenza e un arrivo), e il verso fa parte dell'identità.
- `AirportRollupPlanner` (puro): che cosa chiedere stanotte. **A blocchi di trenta giorni** — giorno per
  giorno il recupero di dodici mesi sarebbe **34 000** chiamate invece di ~1 100 — e **dal più recente**,
  perché l'arretrato si smaltisce in settimane e ieri interessa oggi. ⚠️ L'ordinamento è **globale**: dentro
  ogni aeroporto, il tetto per giro si sarebbe speso tutto sui primi in ordine alfabetico e LIRF non sarebbe
  mai arrivato.
- `AirportDayTraffic` (una riga per campo e giorno) + due migrazioni a doppia emissione. ~34 000 righe
  l'anno, poche decine di byte l'una.
- `AirportTrafficRollupUseCase` + servizio notturno col tetto (`Ivao:AirportTrafficRollupPerRun`, 120).
- ⚠️ **Gate condiviso** con le sessioni (`ImportCategory.AtcSessions`) e **non** una categoria nuova: una
  categoria nuova avrebbe voluto un `bool NOT NULL` in più, che nasce `false` su ogni riga esistente — cioè
  spento a chi non ha chiesto niente. È la trappola che ha già morso con `ImportSids`.

#### Il difetto che i test hanno preso, e non lo schermo

La finestra delle aperture ATC era quella chiesta dal chiamante e non quella dei **giorni interi**: con
`from == to` restava un intervallo di ampiezza zero e **ogni campo risultava chiuso**. Il piano consolida da
mezzanotte a mezzanotte, e le aperture vanno chieste sullo stesso arco.

#### La prova, con dati veri

Durante la verifica live il consolidamento **ha girato davvero contro IVAO**: **3 525 giorni-aeroporto**
misurati su 75 campi, dal 28 maggio al 25 luglio. Nessun vincolo violato (coperti ≤ movimenti, minuti ≤
1440). Il totale della divisione su quella finestra: **16 374 movimenti, 3 307 con ATC — il 20%**, 1 272 ore
di posizioni aperte. I due estremi: **LIEO 52%** (263 ore aperte) e **LIRP 0%** (0,9 ore).

⚠️ E un falso allarme da ricordare: la riga «il consolidamento è ancora in corso: 30 giorni su 31» sembrava
un errore di conto sul `+1`. **Non lo era**: mancava davvero la data più vecchia. Chi togliesse quel `+1`
per far sparire il messaggio nasconderebbe un giorno mancante invece di riempirlo.

### 16.4 Le quattro aggiunte proposte, e approvate

- **Guida e ricerca globale.** ⚠️ La diagnosi di §12 era sbagliata a metà: la voce nel catalogo di ricerca
  c'**era**, e puntava a un'ancora che nella Guida **non esisteva**. Un collegamento morto è peggio di
  nessun collegamento, perché nessuno lo denuncia. Ora c'è il capitolo (IT/EN) e un test che verifica che
  **ogni** voce del catalogo abbia il suo.
- **I nomi dei giorni tradotti.** Erano scritti a mano in italiano (`lun`, `mar`…) dentro
  `CoverageHeatmap`, e restavano italiani con l'applicazione in inglese. Ora vengono dalle risorse, e le
  **stesse chiavi** le usa il grafico «che giorno» — o i due disegni chiamerebbero lo stesso giorno in due
  modi.
- **La potatura del dettaglio traffico.** `TrafficRetentionUseCase`, a scaglioni, con tetto per giro.
  ⚠️ Tocca **solo** `AtcSessionTraffic`: le sessioni e i loro contatori denormalizzati restano, ed è
  esattamente il motivo per cui quei contatori esistono. C'è un test che lo verifica.
  ⚠️ `RemoveRange` e **non** `ExecuteDelete`: desincronizzerebbe il change-tracker (audit del 30 luglio).
- **L'andamento della divisione**: le barre per mese, che la pagina non aveva. ⚠️ Difetto visto solo a
  schermo: con **due** mesi in elenco (periodo di 30 giorni) due colonne che si dividono la pagina diventano
  due **lastre larghe mezzo schermo**. `max-width` sulla colonna; il caso normale non se ne accorge.

### 16.5 Le tre cose piccole

- **Il tasto per la divisione sta sulla riga del titolo** (`.sh-title`, `.btn.ghost` con l'icona), e il link
  grigio in coda al disclaimer è **sparito**: due strade per lo stesso posto erano una di troppo. Gemello
  «torna alle mie» sulla pagina di divisione. Il gate è quello della pagina di destinazione — staff sempre,
  gli altri a classifica accesa — e **non è una guardia**: la guardia sta là.
- **L'esportazione CSV non c'è più**: tasto, endpoint, chiavi di risorsa e riga della mappa delle pagine. Il
  test che sorvegliava «niente esportazione altrui» è rimasto e ora sorveglia «niente esportazione, punto».
- **Cerca un controllore per VID**, solo staff, sulla pagina di divisione. `VidInput.Parse` è puro e accetta
  quel che la gente **incolla**: il numero nudo, «VID 704798», l'indirizzo del profilo, il nickname IVAO
  («Carmine (704798)»), il numero con lo spazio delle migliaia. ⚠️ Prende la prima sequenza di cifre **lunga
  abbastanza** (5-8): in `Member.aspx?p=3&Id=704798` la prima sequenza qualunque aprirebbe il profilo di un
  altro, e nessuno se ne accorgerebbe perché una pagina si aprirebbe lo stesso.

### 16.6 La sezione «Aeroporti» ha una guardia sua

La pagina di divisione la aprono **anche i soci** quando la classifica è pubblica. Il traffico coperto e
scoperto di ogni scalo è uno strumento di pianificazione dello staff, quindi ha una guardia **propria**, e
sta **prima della query** — non davanti al markup, o i numeri sarebbero già usciti dal database. C'è un test
con un `IAirportCoverageQueries` che **esplode a ogni metodo**.

### 16.7 «Controllers» in divisione: sì, con tre precisazioni

Domanda del committente: sono i VID distinti visti controllare in Italia nell'ultimo anno? **Sì**
(`RankAsync().Total`), ma:

1. **«ultimo anno»** solo se è attiva la chip *12 mesi*, che è il default: il numero segue il periodo;
2. **«in Italia»** vuol dire **callsign `LI*`, visitor inclusi** — non «i membri della divisione IT»;
3. le **connessioni sotto il minuto non contano** (il 32% del totale vero: entrate e uscite).

### 16.8 Le chip della divisione non facevano niente (segnalato subito dopo)

Il committente ha premuto le chip del periodo sulla pagina di divisione e **non succedeva nulla** — né quelle
del periodo né quelle del gruppo. Riprodotto dal vivo: **l'indirizzo cambiava, la chip si accendeva, i numeri
restavano quelli di prima**.

⚠️ **La causa, e vale per ogni pagina interattiva di questo prodotto.** `/services/stats/division` è l'unica
delle tre **interattiva** (`@rendermode InteractiveServer`, perché l'interruttore della classifica sta lì), e
su un componente interattivo **`OnInitializedAsync` gira una volta sola**. Le chip sono link che cambiano la
sola stringa di query: Blazor aggiorna i parametri e ridisegna, ma non reinizializza niente — e il
caricamento stava nell'inizializzazione. Sulle altre due pagine il difetto non esiste perché sono **SSR
statiche** e ogni navigazione le ricostruisce da capo. Il caricamento è passato a
**`OnParametersSetAsync`**, con la chiave dell'ultimo caricamento per non rifare le query a ogni render (la
pagina ne provoca parecchi quando si tocca l'interruttore).

Il test (`Premere_una_chip_fa_rileggere_i_numeri`) è stato **provato contro il codice vecchio** prima di
tenerlo: fallisce con `OnInitializedAsync`, passa con `OnParametersSetAsync`. Un test che non sa fallire non
sorveglia niente.

Nello stesso giro, chiesto dal committente: la sezione **Aeroporti è salita** subito sotto i quattro
riquadri. È la domanda per cui uno staffista apre quella pagina, e in coda a una classifica da cinquanta
righe non la trovava nessuno.

### 16.9 Fuori tema, nello stesso giro: la linguetta

Chiesto dal committente subito dopo: l'icona nella linguetta del browser era ancora la **«@» viola di
Blazor** (`favicon.png` del template). Ora è il simbolo di IVAO Italia,
`src/Vipi.Host/wwwroot/favicon.ico`, multi-misura 16/32/48.

⚠️ **Sta nel wwwroot dell'HOST e non nella RCL**: la favicon è del **sito**, non del modulo — quando la vIPI
viene inserita in ivao.it è il loro `<head>` a portare la propria (`docs/guide/ivao-it-wiring.patch`), e
dentro `_content/Vipi.Ui/` non servirebbe a nessuno dei due. Si chiama `favicon.ico` e non col nome
originale del file perché è il percorso che browser e crawler chiedono **da soli** quando nessun tag
corrisponde, e passa da `AssetVersion` come ogni altro asset — senza impronta, chi ha già visitato il sito
continuerebbe a vedere quella di prima per giorni.

⚠️ Sul browser di chi guarda la linguetta può restare quella vecchia comunque: Chrome ed Edge tengono una
cache di favicon loro che l'impronta nell'URL non sempre scavalca. Si sblocca con un ricaricamento forzato o
in InPrivate. Non è il sito.

#### E poi «si può fare più grande?»

Sì, ma **non era il quadrato a essere piccolo: era il disegno dentro il quadrato**. Misurato con una soglia
sull'opacità (il bordo antialiasato non conta):

| | 16×16 | 32×32 | 48×48 |
|---|---|---|---|
| come stava | **31%** | 50% | 50% |
| `it.ivao.aero`, il metro chiesto dal committente | 81% | 81% | — |
| come sta adesso | **88%** | **94%** | **92%** |

Metà dell'icona era margine trasparente, ed è un margine che viene **dall'SVG**: dentro il suo viewBox
`0 0 1991 1993` il simbolo occupa il 52%. Quindi non bastava riesportare — bisognava **ritagliare al
contenuto e ricomporre**.

Come è stata rifatta, che serve se un giorno cambia il simbolo:

1. l'SVG si rasterizza a **2048×2048** con Edge headless (`puppeteer-core`, `omitBackground`) — ⚠️ né PIL né
   il resto di quel che c'è su questa macchina sa leggere un SVG, il browser sì;
2. si ritaglia al riquadro dell'alfa (1054×1056 su 2048);
3. per ogni misura si compone una tela **8× più grande**, ci si mette il disegno al **92%**, e si riduce
   **una volta sola** con LANCZOS: meno passaggi, bordi più puliti;
4. ⚠️ `Image.thumbnail` **non ingrandisce**, quindi le misure grandi restavano piccole. Ci vuole `resize`
   con la scala calcolata — è il primo tentativo, ed era sbagliato.

L'icona ha ora **quattro** misure, 16/32/48 e **64** (la linguetta su schermo ad alta densità disegna 32
logici = 64 fisici).

### 16.10 Conti

`dotnet build Vipi.slnx -c Release --no-incremental` pulita, suite verde su tutti e due i TFM:
**2368 su net8, 2130 su net10** (la differenza sono `Vipi.E2E.Tests` e `Vipi.AuroraBridge.Tests`, che
girano solo su net8). ⚠️ Prima di credere a un conteggio: `grep "error MSB"` — con `Vipi.Host` acceso i suoi
DLL sono bloccati e mezzo albero non compila senza diventare rosso in modo visibile. È successo anche
stavolta, a metà verifica live.

⚠️ **E una trappola nuova della verifica live**, che è costata un giro a vuoto: un `Vipi.Host` di un lancio
precedente può sopravvivere al suo `dotnet run`. Il nuovo lancio esce con **codice 82** («address already in
use»), ma `until curl … :5034` si accontenta del **vecchio** processo e il browser viene guidato contro il
binario di prima — il difetto «non si riproduce». Si aspetta la riga `Now listening` **nel log**, non la
porta; e prima di lanciare si controlla che la 5034 sia libera. Scritto nella skill `verifica-live`.
