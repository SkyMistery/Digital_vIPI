# Archivio ATC mondiale: il poller smette di buttare le altre postazioni (28 agosto 2026)

> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Estende il servizio statistiche
> ([carta del 24 agosto](2026-08-24-servizio-statistiche-atc.md)) senza toccarne i conti.
> **Stato al 28 agosto 2026, sera: slice chiuse, suite verde su entrambi i TFM, verificato dal vivo contro
> IVAO vero.** Ramo `archivio-atc-mondiale` (il codice è tutto nel primo commit, `0ced074`; gli altri sono documenti) **spinto e NON fuso**: la fusione è una
> decisione del committente. ⚠️ **Consegna attesa entro il 1° settembre 2026** — è la data da cui deve
> partire la raccolta, e nel codice non c'è niente che la faccia rispettare (§3-bis).

## 1. Perché

Il poller chiede a IVAO una fotografia della rete **ogni minuto** — `/v2/tracker/whazzup`, una chiamata,
endpoint pubblico senza token — e in quella fotografia ci sono **tutte** le postazioni ATC del mondo.
Fino a ieri l'adattatore le filtrava ai prefissi della divisione e buttava le altre.

Buttarle è l'unica scelta irreversibile che si potesse fare: **il whazzup non ricorda il passato.** Quando
una connessione è finita, quella riga non la ridà più nessuno — lo storico `/v2/tracker/sessions` si
interroga per prefisso di callsign, non «tutto il mondo». Le postazioni scartate erano già pagate (stessa
richiesta, stessi byte) e diventavano definitivamente irrecuperabili.

Che il dato serva è già dimostrato altrove: il validatore dei tour della divisione si tiene un
**archiviatore proprio** (Cloudflare Worker + D1, cron al minuto) puntato sullo stesso whazzup, per
rispondere a «quel volo ha trovato un ATC aperto?». Due processi che chiedono lo stesso file allo stesso
server per riempire due tabelle quasi identiche.

## 2. Cosa cambia, in una riga

Il poller **archivia tutte le postazioni aperte**; tutto il resto del prodotto continua a guardare la sola
divisione.

## 3. Le decisioni del committente (28 agosto)

| | scelta |
|---|---|
| **Perimetro** | tutte le postazioni ATC aperte, non i soli prefissi di divisione |
| **Ritenzione** | **dodici mesi per tutto**, divisione compresa (era già dodici per le italiane) |
| **Uso** | archivio **+ pagina staff + endpoint macchina** |

La ritenzione è stata scelta sui numeri, non a sentimento.

**Il costo di una riga, misurato da noi.** Sull'archivio vero (`vipi.db`, 21 133 sessioni italiane =
esattamente dodici mesi, quindi **58 sessioni/giorno**), la tabella `AtcSessions` isolata coi suoi indici e
compattata pesa **240 byte/riga**; su MariaDB/InnoDB, contando l'overhead di riga e la PK ripetuta in ogni
indice secondario, la stima è **~400 byte/riga**.

**Quante righe fa il mondo, misurato da qualcun altro.** L'archiviatore del validatore dei tour archivia
tutte le postazioni del pianeta **dal 2 giugno 2026**, e al 28 agosto il suo database D1 pesa **13,35 MB**:
87 giorni, cioè **0,153 MB al giorno** sul suo schema. Convertito in righe con un costo per riga fra 200 e
280 byte (il suo schema è più magro del nostro nelle colonne e più grasso negli istanti, che sono testo ISO):

| costo per riga ipotizzato | sessioni/giorno nel mondo | rapporto sulle 58 italiane |
|---|---:|---:|
| 200 B | 805 | 13,9× |
| **240 B** (il nostro, misurato) | **670** | **11,6×** |
| 280 B | 575 | 9,9× |

⚠️ Il rapporto vero sta dunque fra **10× e 14×**, non fra 8× e 12× come si era stimato a occhio prima di
avere questo numero. Resta un'unica ipotesi, il costo per riga dello schema altrui; e resta il fatto che il
campione copre **giugno-agosto**, cioè mesi d'estate: un anno intero può essere più magro.

Prendendo il valore centrale (**670 sessioni/giorno**, italiane comprese):

| finestra | righe | SQLite (240 B) | MariaDB (400 B) |
|---|---:|---:|---:|
| 90 giorni | ~60 000 | 14 MB | 23 MB |
| 12 mesi | ~245 000 | 56 MB | **93 MB** |
| 3 anni | ~734 000 | 168 MB | 280 MB |

Agli estremi del rapporto, i dodici mesi stanno fra **81 e 113 MB** su MariaDB.

### 3-ter. E quanto pesa TUTTO il database, allora

Misurato il 28 agosto sul `vipi.db` reale, tabella per tabella (copia isolata coi suoi indici, `VACUUM`,
dimensione del file): il database intero è **10,05 MiB**, di cui **8,82 MiB** di tabelle e il resto pagine
libere. Le cinque più grosse:

| tabella | righe | MiB | B/riga |
|---|---:|---:|---:|
| `AtcSessions` | 21 133 | 4,82 | **239** |
| `AccSectors` | 153 | 0,89 | 6 104 (poligoni) |
| `DocReleases` | 38 | 0,88 | 24 253 (istantanee) |
| `AirportDayTraffic` | 6 450 | 0,77 | 126 |
| `AtcSessionTraffic` | 1 410 | 0,20 | 151 |

⚠️ **Le sessioni sono già oggi metà del database**, con le sole italiane. Proiezione a regime, dopo dodici
mesi di raccolta col mondo dentro:

| voce | righe/anno | MiB (SQLite) |
|---|---:|---:|
| `AtcSessions` (mondo + Italia, 670/giorno) | 244 550 | 55,7 |
| `AtcSessionTraffic` (dettaglio, **solo divisione**) | ~500 000 | 72,0 |
| `AirportDayTraffic` (93 scali × 365) | ~34 000 | 4,1 |
| `AtcSessionRunway` + `AtcMonthRollup` | ~27 000 | 2,2 |
| contenuto (documenti, release, settori, blocchi) | — | 3,2 |
| **totale** | | **~137** |

Su MariaDB/InnoDB, col solito ×1,67: **~230 MB**. Di questi, **~85 MB sono le sole righe fuori divisione**,
cioè il prezzo di questo giro; il resto c'era già in programma.

⚠️ **Il pezzo più grosso non è il mondo: è il dettaglio del traffico** (~72 MiB), che nasce **solo** dalle
sessioni di divisione e non cambia di una riga con questa modifica. Le 500 000 righe l'anno vengono dalla
carta del 24 agosto, non da una misura nostra: in sviluppo ce ne sono 1 410, quindi è l'unico numero grosso
di questa pagina che non poggia su dati veri.

### 3-bis. Si comincia da capo, il 1° settembre 2026

Decisione del committente (28 agosto): **lo storico del Worker non si travasa.** I due archivi restano
separati, il nostro comincia da zero, e il passaggio dal servizio vecchio a questo si fa **nel 2027** —
quando qui dentro ci sarà già più di un anno di dati, cioè quando il travaso non servirebbe più a niente.

La data d'inizio della raccolta è il **1° settembre 2026**.

⚠️ **Non c'è nessun cancello di data nel codice, ed è voluto.** Una data fissa scritta in una `if` può solo
fare danno: se il deploy arriva prima del 1° settembre, raccogliere qualche giorno in più non toglie niente
a nessuno; se arriva dopo, il cancello non recupera i giorni persi. La data d'inizio la decide **il
deploy**, e la dice l'archivio stesso (la prima riga fuori divisione).

⚠️ Il che vuol dire che la data del 1° settembre è una **scadenza di consegna**, non una riga di codice: se
il ramo non è in produzione entro quel giorno, la raccolta comincia dopo.

## 4. Cosa dà la sorgente — misurato, non dedotto

Campo per campo, dal whazzup vero delle 08:14Z del 28 agosto 2026:

```
id, userId, callsign, serverId, softwareTypeId, softwareVersion,
rating, createdAt, time, lastTrack, atcSession{frequency,position}, atis{lines,revision,timestamp}
```

⚠️ **Non esiste nessun campo «divisione»**. Il confine fra «nostra» e «del mondo» resta quello che era: il
**prefisso del callsign**, e lo decide l'adattatore, che è l'unico posto dove vive la configurazione della
divisione (`Division:IcaoPrefixes`).

## 5. Il modello: una colonna, e in negativo

`AtcSession.IsOutsideDivision` — **non** `IsDivision`.

Un `bool NOT NULL` nuovo nasce `false` su **tutti e tre** i percorsi che creano schema qui dentro:
migrazione EF, `EnsureCreated` e `PostgresSchemaReconciler`. Le righe già in archivio sono **tutte** di
divisione — verificato, non supposto: `SELECT count(*) FROM AtcSessions WHERE Callsign NOT LIKE 'LI%'` = **0**
su 21 133 righe. Con la forma positiva quel default avrebbe dichiarato straniero l'intero storico italiano,
che è la trappola dei flag opt-out già pagata una volta
(vedi [aree regolamentate, hardening](2026-08-03-aree-regolamentate-hardening.md)). In negativo, **il default coincide con la
verità** e non serve nessun UPDATE di rimedio.

Nessuna tabella gemella (pre-flight §1): una connessione ATC brasiliana è lo stesso concetto di una
italiana, e sta nella stessa tabella.

Indice nuovo: `(IsOutsideDivision, StartUtc)`. Gli altri tre reggono da soli perché partono da una colonna
selettiva (VID, callsign, turno); le finestre temporali no, e sono proprio quelle che le statistiche fanno
di continuo su una tabella diventata dieci volte più grande.

## 6. Dove il filtro è sceso di un piano

Prima filtrava `IvaoWhazzupClient`, e chi stava a valle non sapeva nemmeno che esistesse il resto del mondo.
Ora l'adattatore restituisce tutto, marcato, e **filtra chi sa a cosa serve la lista**:

| chi | cosa vede | perché |
|---|---|---|
| `OnlineAtcCache` (vista live, coordinamenti, pallino «in frequenza») | **divisione** | mettere il pianeta in cache vuol dire mostrare un APP brasiliano come vicino di casa |
| `AtcTrafficRecorder` | **divisione** | l'AoR che abbiamo è italiana: le altre sessioni non potranno mai avere una tratta, e idratarle costa letture |
| piste in uso dall'ATIS | **divisione** | è la sequenza del turno di un controllore nostro, non ogni cambio d'ATIS del pianeta |
| `EfAtcStatsQueries` (ore, classifica, copertura, griglia) | **divisione** | sono i conti della divisione |
| `EfAirportTrafficRollupStore` (copertura scali) | **divisione** | un callsign estero è ben formato: senza filtro nascerebbero righe di copertura per scali non nostri |
| `RecomputeShiftsAsync` (dopo il backfill) | **divisione** | lavora su finestre di dodici mesi, e caricherebbe in memoria dieci volte le righe |
| `GetOpenOrRecentAsync` (il poller che chiude) | **TUTTO** | ⚠️ l'unica lettura che non filtra, ed è apposta: una sessione straniera esclusa da qui resterebbe **aperta per sempre** |

Il filtro è un metodo solo, `AtcSessionScope.DiDivisione()`, e non un `HasQueryFilter` globale: il filtro
globale sarebbe invisibile a chi legge `_db.AtcSessions` e andrebbe **disattivato** proprio nei due punti
nuovi — cioè si dimenticherebbe al contrario, e sbagliando in quella direzione nessuna pagina se ne accorge.

## 7. Ritenzione: stessa scadenza, un solo riassunto

`RollupAndPruneSessionsAsync` **pota tutto** a dodici mesi, ma **riassume solo la divisione**:
`AtcMonthRollup` è la memoria lunga delle ore italiane (mese · persona · callsign) e regge la classifica —
riassumerci il mondo vorrebbe dire metterlo in gara.

Le sessioni fuori divisione, oltre l'anno, se ne vanno senza lasciare niente. È una perdita **dichiarata**,
non un effetto collaterale: è la scelta «12 mesi per tutto».

I tetti per giro non cambiano (`SessionRetentionPerRun` = 2000): a regime scadono ~660 righe al giorno.

## 8. La pagina e l'endpoint

- **`/services/stats/world`** — solo staff. Chi è aperto adesso, e la ricerca per callsign/VID/fetta.
  Ci si arriva dalla pagina di divisione (pre-flight §3: niente catch-22).
  ⚠️ Aggiunta a `SegmentiEsclusi` della cache delle letture anonime: vive sotto `/services/stats` e non
  porta la parola `admin` nell'indirizzo, quindi sarebbe stata l'unica schermata di staff di cui si teneva
  una copia.
- **`GET /vsop/api/v1/atc/sessions`** — anonimo e in sola lettura come `/vsop/live/atc`, e per lo stesso
  motivo: è la ripetizione di un dato che la sorgente pubblica già a chiunque senza token. Quel che si
  aggiunge è il **passato**. Parametri: `from`, `to`, `callsign`, `vid`, `open`, `scope`, `limit`, `offset`.
  Tetto duro a 500 righe, `total` sempre accanto alle righe (una pagina piena non deve poter sembrare tutto
  quel che c'è), tetto per IP (30/min) e complessivo (300/min) con lo stesso limitatore del bridge Aurora.

⚠️ La finestra `from`/`to` seleziona per **sovrapposizione**, non per inizio: chi ha aperto alle 19:50 e
chiuso alle 22:00 fa parte di «cosa c'era alle 21».

## 9. Verifica

- **Test**: `ArchivioAtcMondialeTests` (10) — una prova per ogni lettura che conta, più fetta/finestra/tetto
  dell'archivio; `WhazzupClientTests` (l'ucraino non si butta più, esce marcato); `AtcSessionSyncTests`
  (la marca viaggia; una postazione estera si chiude quando sparisce); `AtcTrafficRecorderTests`
  (si archivia ma non prende traffico); `CacheDelleLettureAnonimeTests` (la pagina staff non si tiene).
- **Live**, contro IVAO vero: «Poll IVAO: 0 ATC divisione online, **18 fuori divisione**, 307 piloti», 18
  righe scritte (EHAM_W_APP, WADD_TWR, LECB_CTR, NZAA_APP…) e le 21 133 italiane intatte; endpoint provato
  nei quattro casi (fetta, tetto duro, totale onesto, 400 sulla finestra rovesciata); pagina guidata in Edge
  nei due temi, ricerca `LIRF`+divisione → 1375 trovate e 200 mostrate, zero errori di console.
  ⚠️ Un difetto visto **solo a schermo**: «Open right now» andava a capo su tre righe, perché la colonna
  flex stringeva il testo alla larghezza della casella.

## 10. Cosa resta

- Il campione del rapporto mondo/Italia copre **giugno-agosto**: un anno intero può essere più magro.
  Si ricontrolla fra qualche mese sul nostro archivio, che a quel punto è la misura diretta.
- I due archiviatori girano **in parallelo fino al 2027**, per decisione del committente: due processi che
  chiedono lo stesso file allo stesso server. È il prezzo accettato per non dipendere da un travaso.
