# Ponte RFO Gate Manager — carta (18 settembre 2026)

> **Stato: ✅ ESEGUITA il 18 settembre 2026**, per il pacchetto **1.33.0** (migrazione additiva `PonteRfo`).
> Serve all'evento RFO di LIRN del **19 settembre 2026** (`lirn-20260919`).
> Il contratto non è nostro: è `docs/SYNC-API.md` della repo **SkyMistery/RFO-Stand-Manager**, con la forma
> eseguibile `FakeSyncServer` in `tests/RfoGateManager.Tests/SharedStateTests.cs`. Richiesta del committente:
> **seguirlo alla lettera**.
>
> Dove sta: `RfoSharedState` (Domain) · `IRfoSharedStateStore` (Application) · `EfRfoSharedStateStore`
> (Infrastructure, la scrittura condizionata) · `PonteRfo` (Hosting: opzioni, chiavi, GET/PUT) ·
> `RegistroRichieste.PollingVuoto` (Host) · prove in `tests/Vipi.E2E.Tests/PonteRfoTests.cs`.

## 1. Che cosa fa

Le postazioni ATC di un evento RFO (DEL, GND, TWR, APP) usano ognuna la propria copia di «RFO Gate Manager».
Per vedere le stesse decisioni (stand fissati, stand chiusi, piloti avvisati, partenze che hanno chiamato)
condividono **un documento JSON per evento**, con una versione. Il sito fa da ponte e **non interpreta** il
contenuto.

```
GET  /api/rfo/events/{eventId}/state
PUT  /api/rfo/events/{eventId}/state
```

| Situazione | Risposta |
|---|---|
| `eventId` fuori da `[a-z0-9-]{1,64}` | 400 |
| `x-api-key` mancante o sconosciuta | 401, corpo vuoto |
| chiave buona ma per un altro evento | 403, corpo vuoto |
| GET, documento mai scritto | 404, corpo vuoto |
| GET, `If-None-Match` = versione attuale | 304, corpo vuoto, `ETag` |
| GET | 200, busta, `ETag` |
| PUT senza `If-Match` | 428 |
| PUT, `If-Match` non è una versione (`*`, elenco, negativo) | 400 |
| PUT, documento inesistente e `If-Match` ≠ `"0"` | 404 |
| PUT, JSON rotto | 400 |
| PUT, `data` assente o non oggetto, `updatedBy` non testo | 422 |
| PUT, corpo oltre 1 MB (anche a blocchi, senza `Content-Length`) | 413 |
| PUT, `If-Match` ≠ versione attuale | **409 con la busta attuale**, `ETag` |
| PUT, `If-Match` = versione attuale | 200, busta nuova (versione +1), `ETag` |

Busta: `{"version":12,"updatedAt":"2026-09-19T09:41:07.512Z","updatedBy":"LIRN_GND","data":{…}}`.

## 2. Le regole su cui non si transige (committente)

1. **Controllo e scrittura atomici.** Una sola istruzione condizionata: `INSERT` per `If-Match: "0"` (la chiave
   primaria rifiuta il secondo), `UPDATE … SET version = version + 1 … WHERE event_id = @id AND version = @attesa`
   per il resto. Zero righe toccate = qualcuno è arrivato prima = 409. **Mai «leggo, poi aggiorno».**
2. Il 409 porta la busta attuale.
3. 428 senza `If-Match`; 404 se il documento non c'è e `If-Match` non è `"0"`.
4. `ETag: "<version>"` su ogni busta; `If-None-Match` uguale → 304 senza corpo.
5. Niente CORS.

## 3. Dove ci si scosta dall'implementazione di riferimento, e perché

Il contratto dice «da adattare allo stile del sito». Le differenze sono tutte di adattamento, nessuna di
comportamento:

- **Tre provider, non uno.** Il sito gira su SQLite (sviluppo, test), MariaDB (produzione) e Postgres. SQL scritto
  a mano con nomi minuscoli senza virgolette; l'ora la mette il processo, troncata al millisecondo
  (`UTC_TIMESTAMP(3)` esiste solo su MySQL); la chiave duplicata non si riconosce dal codice d'errore (1062, 19,
  23505) ma rileggendo.
- **Colonne**: `updated_at` è `datetime(6)` e non `(3)`, la collation è quella del progetto (`utf8mb4_uca1400_as_cs`)
  e non `unicode_ci`: sono le scelte di Pomelo per tutto lo schema, e `event_id` è validato `[a-z0-9-]`, quindi la
  collation non cambia nessun confronto. Il valore scritto è comunque al millisecondo.
- **`data` byte per byte**: si salva il testo esatto (`GetRawText`) e si restituisce con `WriteRawValue`. Il
  riferimento lo ripassa da `JsonNode`, che riscrive spazi ed escape.
- **ETag deboli**: `If-None-Match` e `If-Match` accettano anche `W/"12"`. Cloudflare e nginx, quando comprimono,
  possono indebolire l'ETag, e il client rimanda quello che ha ricevuto: senza, il 304 non scatterebbe mai.
- **Casi che nel riferimento diventano 500**: `updatedBy` non testo → 422; JSON rotto → 400; corpo a blocchi
  oltre 1 MB → 413 (il tetto si conta leggendo).
- **Retry dei guasti transitori** (MariaDB/Postgres): la transazione sta dentro `CreateExecutionStrategy()`. Se la
  connessione cade dopo un commit riuscito, il nuovo tentativo risponde 409 con la busta che contiene già la
  scrittura: il client riapplica la sua modifica su quella, innocuo.
- **Creazione su un documento che c'è già**: 409 subito, senza tentare l'INSERT. Il controllo non decide la gara (la
  decide la chiave primaria): serve a non far scrivere a EF un «Failed executing DbCommand» come ERRORE in
  `avvisi-log.txt` per una risposta normale. Trovato sul primo pacchetto 1.33.0 (`f43229b`, mai spedito); una gara
  vera fra due creazioni nello stesso istante lo scrive ancora, ed è giusto che si veda.
- **`Cache-Control: private, no-cache`** sulla busta: nessun proxy la tiene senza rivalidare.
- **Storia** (facoltativa nel contratto, scelta dal committente): `rfo_shared_state_history`, una riga per ogni
  scrittura riuscita, **nella stessa transazione**. Chiave `id` propria: se il documento si svuota a mano la
  versione riparte da 1 e la storia tiene tutte e due le vite.

## 4. Le chiavi

In configurazione, non nel codice e non nel database (decisione del committente, 18 settembre 2026). Si mettono
in un file `.json` dentro la cartella **`segreti/`** accanto all'eseguibile (`SegretiFuoriDalWeb`), con un nome
scelto da chi installa:

```json
{
  "Rfo": {
    "Chiavi": {
      "lirn-20260919": { "Chiave": "rfo_…", "Eventi": "lirn-20260919, prova-ponte-rfo" }
    }
  }
}
```

- **Dizionario per nome, `Eventi` testo con le virgole**: il binder somma gli array delle diverse sorgenti invece
  di sostituirli (§A62). `*` = tutti gli eventi; vuoto = nessuno (403).
- Una chiave più corta di 32 caratteri **non apre niente**: una «prova» dimenticata nei segreti non diventa una porta.
- Confronto a tempo costante (impronte SHA-256, `FixedTimeEquals`), percorrendo sempre tutte le chiavi.
- Si generano con 32 byte casuali in base64url: `rfo_` + 43 caratteri. **Non si scrivono nel repo.**
- La configurazione si rilegge a caldo (`IOptionsMonitor`): aggiungere o togliere una chiave non chiede un riavvio
  se il file cambia sul disco — ma su Passenger il processo si ricicla comunque spesso.
- 401 finisce in `log-*.txt` (Information, categoria `Vipi.Api`), 403 anche in `avvisi-log.txt` (Warning).

Lato programma, in `secrets/booking.json` di ogni postazione:

```json
"sharedStateUrl": "https://atc.it.ivao.aero/api/rfo/events/lirn-20260919/state",
"sharedStateToken": "<chiave>"
```

## 5. Fuori da `/services`, e il registro

- Il percorso `/api/rfo/…` sta fuori da `/services` di proposito: `CacheDelleLettureAnonime` vale solo lì, e una
  copia di sessanta secondi vorrebbe dire postazioni che non si vedono.
- `richieste-*.tsv` **non** scrive i 304 del ponte (committente, 18 settembre 2026): dieci postazioni ogni tre
  secondi sono ~12 000 righe l'ora tutte uguali, che avrebbero raggiunto il tetto del file a metà evento. Restano
  letture col corpo, scritture, 409 ed errori.

## 6. Verifica

- `PonteRfoTests` (E2E, server vero su SQLite): i cinque passi del contratto, 10 PUT simultanei sulla stessa
  versione (**1×200, 9×409**, storia 1-2), 6 creazioni simultanee, somma col 409, 404/422/400/413, `data` byte per
  byte, ETag deboli, chiavi 401/403, CORS assente, registro. **Mutazione**: tolto `AND version = …` dall'UPDATE, la
  prova di concorrenza diventa rossa.
- **Dal vivo su MariaDB 11.4** (copia di produzione del 17 settembre, `vipi_rfo` sulla porta 3399), 18 settembre:
  migrazione applicata; i curl del contratto 1-5 esatti; **50 coppie di PUT in parallelo con lo stesso `If-Match`:
  50 volte 200+409, mai due 200**; raffica di 10 → 1×200 + 9×409, tutte le buste alla versione giusta; storia 52
  righe per 52 versioni; risposta compressa `br` con ETag intatto; nessun 304 nel registro.
- ▶ **Dopo il carico**: gli stessi curl e la prova in parallelo, ma su un **evento di prova** —
  `https://atc.it.ivao.aero/api/rfo/events/prova-ponte-rfo/state` — e non su `lirn-20260919`: le postazioni
  leggerebbero come vere le decisioni finte lasciate dai test. Per questo la chiave dell'evento apre anche
  `prova-ponte-rfo` (`"Eventi": "lirn-20260919, prova-ponte-rfo"`). Dopo l'evento si può togliere.
- ⚠️ La prova da fuori dice anche se **Cloudflare** lascia passare un programma che non è un browser: una sfida
  anti-bot risponderebbe 403 con una pagina HTML, e le postazioni resterebbero col chip rosso.
