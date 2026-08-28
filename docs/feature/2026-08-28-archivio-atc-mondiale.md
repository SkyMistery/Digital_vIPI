# Archivio ATC mondiale: il poller smette di buttare le altre postazioni (28 agosto 2026)

> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Estende il servizio statistiche
> ([carta del 24 agosto](2026-08-24-servizio-statistiche-atc.md)) senza toccarne i conti.
> **Stato: slice chiuse, suite verde, verificato dal vivo.**

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

La ritenzione è stata scelta sui numeri, non a sentimento. Misurato sull'archivio vero (`vipi.db`,
21 133 sessioni italiane = esattamente dodici mesi, quindi **58 sessioni/giorno**), la tabella
`AtcSessions` isolata coi suoi indici e compattata pesa **240 byte/riga**; su MariaDB/InnoDB, contando
l'overhead di riga e la PK ripetuta in ogni indice secondario, la stima è **~400 byte/riga**.

| finestra | righe stimate | SQLite (240 B) | MariaDB (400 B) |
|---|---:|---:|---:|
| 90 giorni | ~54 000 | 13 MB | 22 MB |
| 12 mesi | ~219 000 | 53 MB | **88 MB** |
| 3 anni | ~657 000 | 158 MB | 263 MB |

⚠️ Il rapporto mondo/Italia (8–12×, cioè ~500-700 sessioni al giorno) è **stimato, non misurato**: il
campione preso alle 08:14Z del 28 agosto — 15 ATC nel mondo, **zero** italiani, 298 piloti, 76 KB — non dice
niente sul rapporto a quell'ora. Il numero vero lo può dare l'archiviatore del validatore, che quelle righe
le ha già.

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
- **Live**: vedi §10.

## 10. Cosa resta

- Il rapporto mondo/Italia è stimato: quando serve il numero vero, sta nell'archiviatore del validatore.
- L'archiviatore del validatore continua a girare: unificarlo su questo endpoint è un lavoro suo, non di qui.
