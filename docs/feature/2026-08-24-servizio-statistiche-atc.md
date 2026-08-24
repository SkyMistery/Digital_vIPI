# Statistiche ATC: il terzo servizio (carta, 24 agosto 2026)

> Nuovo figlio dell'hub `/services`, accanto a `vsop` (documentazione) e `profile-swapper` (strumento).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Regola d'ingaggio con la sorgente:
> [sorgenti](2026-08-22-sorgenti-giro-automatico-ta-piste.md) — interfaccia neutra in Application,
> adapter `Ivao*` in Infrastructure, riga nella policy di import.
> **Stato: carta approvata e le tre verifiche aperte sono state fatte sul campo il 24 agosto (§8): tutte
> passate, con due sorprese che cambiano il piano (prefisso callsign, nomi). Via libera alla slice 1.**

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
| `/v2/airports/{icao}/traffics?from&to` | movimenti dell'aeroporto nella finestra: **inbound / outbound / flightover**, ciascuno con callsign, `flightPlan` e `lastTrack` | app |
| `/v2/airports/{icao}/stats?from&to&limit` | conteggi giornalieri `{in, out, total, timestamp}` | app |
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

### 4.5 ⚠️ I limiti verticali oggi in archivio sono quasi tutti nominali

La regola del committente («FL260 sopra uno spazio che finisce a FL195 non è gestito») è nel motore, ma sui
dati di oggi **non morde quasi mai**:

| | tetto = UNL (null) | pavimento = 0/null |
|---|---|---|
| `AccSectors` (153) | **138** | 151 |
| `AirportSectors` (193) | 50 | 193 |

Cioè: 138 settori ACC su 153 arrivano «senza limite» e partono da terra. Finché restano così, un aereo a
FL390 sopra Fiumicino risulta dentro il volume di `LIRR_NE1_CTR` — che *può* anche essere giusto, ma non
perché qualcuno l'abbia deciso: perché il campo è vuoto. **Il motore è pronto, i dati no.** Compilare i
limiti dei settori ACC è un lavoro editoriale da mettere in conto (già possibile dall'editor struttura), e
la pagina deve dire che l'attribuzione verticale vale quanto i limiti inseriti.

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

- `/services/stats` — la mia pagina (ore totali, per posizione, per mese, elenco sessioni);
- `/services/stats/session/{id}` — dettaglio: durata, frequenza, aerei gestiti, mappa AoR con le tracce;
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
3. Porta neutra `IAtcActivitySource` + adapter whazzup; il poller esistente passa al whazzup completo e scrive le sessioni.
4. Attribuzione del traffico nel poller (usa il punto 1).
5. Backfill storico: **23 query a prefisso** (`LIA`…`LIZ`) × pagine da 100 ≈ 220 chiamate una tantum
   (§8.1), con **ritenti sui 503** (§8.4) + riga nella policy sorgenti + audit.
6. Riempimento retroattivo del traffico d'aeroporto (`/airports/{icao}/traffics`).
7. UI: `/services/stats` (mia) → dettaglio sessione → vista staff + interruttore classifiche.
8. Ingressi: card hub, menù, guida, ricerca. Verifica live guidando il flusso reale.

## 10. Trappole già note da non ripetere

- `dotnet build Vipi.slnx -c Release --no-incremental` su **entrambi i TFM**: gli avvisi sono errori e
  `dotnet test` non lo vede (net8 = niente C#13+/API .NET9+).
- I servizi con cache registrati `AddScoped` in Blazor vivono **quanto il circuito**: una cache di sessioni
  che invecchia per ore è il difetto classico.
- Un `DbContext` condiviso dal circuito più un render intermedio = «second operation»: le pagine che leggono
  in `OnInitializedAsync` usano `OwningComponentBase`.
- Attributo componente di tipo `string` senza `@` è un **letterale**: `Key="x"` ≠ `Key="@x"`.
- Se si aggiunge un pacchetto: `packages.lock.json` rigenerato e committato, o la CI si ferma.
