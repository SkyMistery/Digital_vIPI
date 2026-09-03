# Il Worker `atc-archiver` su Cloudflare — quota D1 e keep-alive di vIPI

> **3 settembre 2026.** Questo file esiste perché fino a oggi di quel Worker non c'era traccia in nessun
> repository: viveva solo sulla dashboard di Cloudflare, e quel che faceva lo sapeva chi l'aveva scritto.

## Che cos'è

`atc-archiver` è un Cloudflare Worker dell'account `Carmine.granato@ivao.aero`, **non fa parte di vIPI**:
serve il **validatore dei tour** della divisione, e risponde alla domanda «quel volo ha trovato un ATC
aperto?». Scarica il whazzup di IVAO ogni minuto e lo archivia su **D1** (`atc-archiver-db`).

| | |
|---|---|
| Worker | `atc-archiver` — handler `fetch` **e** `scheduled` |
| cron | `* * * * *` (ogni minuto) |
| database | D1 `atc-archiver-db` — tabelle `atc_sessions`, `archiver_state` |
| API | `GET /health` · `GET /sessions?callsign=…` · `GET /sessions?from=…&to=…` |
| segreti | `ALERT_EMAIL`, `ALERT_FROM`, `RESEND_API_KEY` (avvisi via Resend dopo 5 fallimenti) |
| ritenzione | 90 giorni (`CLEANUP_DAYS`) |

## 🔴 Il difetto che saturava la quota D1

Il piano gratuito di D1 dà **5 milioni di righe lette al giorno**. Erano finite, e l'ipotesi di partenza —
«è l'API del validatore che legge troppo» — **era sbagliata**. I numeri misurati con `wrangler d1 info`:

| | 24 h |
|---|---|
| righe lette | **5.473.070** ← la quota |
| query di lettura | 121 |
| righe scritte | 10.385 |
| query di scrittura | 4.531 |

⚠️ **5,4 milioni di righe in 121 query** non è un'API interrogata da fuori: è una manciata di query che
scansionano tutto. Il colpevole sta nel **cron**, non nel `fetch`:

```sql
SELECT id, callsign FROM atc_sessions WHERE ended_at IS NULL
```

Gli indici erano `idx_callsign(callsign)` e `idx_time(started_at, ended_at)`: **nessuno dei due serve per
`ended_at IS NULL`**. Quella riga scansionava la tabella intera — 73.587 righe — **ogni minuto**. Dopo ~74
giri la quota era finita, e da lì falliva **tutto**, comprese le letture del validatore.

🔴 **Confermato dallo stack trace**, non dedotto: `wrangler tail` mostrava
`Error: D1_ERROR … at async runPoller (index.js:110)`, che è esattamente quella `SELECT`.

### La cura: un indice parziale

```sql
CREATE INDEX IF NOT EXISTS idx_open ON atc_sessions(ended_at) WHERE ended_at IS NULL;
```

Applicato il 3 settembre 2026. Contiene le sole sessioni **aperte**: alla creazione erano **49**.

| | prima | dopo (atteso) |
|---|---|---|
| righe lette dal cron, per giro | ~73.587 | ~50 |
| al giorno (1440 giri) | 106 M (di fatto tagliate a 5,4 M dal blocco) | **~72.000** |
| quota usata | 100% in ~2 ore | **~1,4%** |

⚠️ **Il «dopo» non è stato misurato**: la quota era già bloccata quel giorno e le letture erano rifiutate.
Il `CREATE INDEX` è passato lo stesso (DDL), ma la verifica va fatta **dopo il reset di mezzanotte UTC**,
con `wrangler d1 info atc-archiver-db`.

### ⚠️ Un secondo punto debole, non risolto

La query dell'API del validatore è

```sql
WHERE started_at <= ? AND (ended_at IS NULL OR ended_at >= ?)
```

e quell'`OR` è ostile a qualunque indice: `started_at <= ?` da solo seleziona quasi tutta la tabella. Con
poche decine di chiamate al giorno non è lui a bruciare la quota, **ma lo diventerebbe** se il validatore
aumentasse le interrogazioni. Da guardare con i numeri veri sotto gli occhi, non prima.

## Il keep-alive di vIPI

Al `scheduled` è stata aggiunta una chiamata a vIPI, **prima** di qualunque accesso a D1:

```js
async scheduled(_event, env, ctx) {
    await pingVipi();                                  // ⚠️ PRIMA di D1
    for (const delayMs of PING_EXTRA_MS) {             // ⚠️ in waitUntil, mai atteso qui
      ctx.waitUntil(pingVipiTraUnPo(delayMs));
    }
    await runPoller(env);
}

async function pingVipi() {
  try {
    await fetch(`https://atc.it.ivao.aero/vsop/health/ready?t=${Date.now()}`, {
      headers: { "Cache-Control": "no-cache" },
      cf: { cacheTtl: 0, cacheEverything: false }
    });
  } catch {
  }
}
```

**Perché serve.** vIPI gira su Plesk + Passenger, che **spegne il processo per inattività** appena il
traffico si ferma: vite misurate sul server, **1:00 / 1:49 / 4:52**. Con il processo muoiono il
campionamento ATC (un giro al minuto) e i giri periodici con ritardo d'avvio oltre i 60 secondi.

### 🔴 Un ping al minuto NON basta — misurato il 3 settembre 2026, sera

`diagnostica/avvii.txt` del server vero, l'ora fra le **19:21 e le 20:25**: **58 avvii**. Il processo parte
a `hh:mm:59` (è il cron), vive **7-15 secondi**, si spegne **in modo ordinato** — inattività, non un crash —
e al minuto dopo ricomincia. **Il ping sveglia il processo, non lo tiene su.**

🔴 **La conseguenza vera non è il carico, sono i giri periodici**: il `bootDelay` più corto dei tredici è
**15 s** (`AccImportHostedService`) e il più lungo **150 s** (`TrafficRetentionHostedService`). Su vite di
7-15 secondi **non ne arriva in fondo nessuno**. Prima del keep-alive capitavano avvii lunghi (1h50, 2h25,
3h00) e per il gate delle 24 ore ne bastava **uno**; adesso non capitano più.

⚠️ E ogni giro ripaga l'avvio intero: **8 433 ms**, di cui **2 992 ms di migrazioni** e **4 903 ms di
manutenzioni d'avvio**, contro MariaDB, 58 volte l'ora.

**Il rimedio provato: pingare più spesso del cron.** Il cron di Cloudflare non scende sotto il minuto,
quindi la frequenza vera la fa una lista di ritardi **dentro** la stessa invocazione:

```js
var PING_EXTRA_MS = [3e4];   // un ping extra a +30 s, oltre a quello a t=0
```

⚠️ **I ping extra vanno in `ctx.waitUntil` e non si aspettano in linea**: aspettarli sposterebbe di mezzo
minuto il campionamento ATC, che è il lavoro vero di questo cron. Verificato con una simulazione locale a
`fetch` e D1 stubbati: ping a `+1 ms`, poller a `+17 ms`, `scheduled` ritornato a `+18 ms`, secondo ping a
`+30 026 ms`.

⚠️ **Trenta secondi è un TENTATIVO, non una cura dimostrata**: le vite misurate stanno fra 7 e 15 secondi,
quindi può non bastare. **La prova è `avvii.txt`, non una `curl`** — anche quella è traffico. Se continua a
contare un avvio al minuto, il processo muore prima dei 30 s e la lista va infittita, p.es.
`[1e4, 2e4, 3e4, 4e4, 5e4]`. La strada pulita resta l'altra: alzare l'inattività di Passenger dal pannello
Plesk, a cui però il committente non ha accesso.

Le tre scelte, e il perché di ognuna:

- ⚠️ **`pingVipi()` sta PRIMA di `runPoller`**, e non è ordine estetico: quando la quota D1 è esaurita
  `runPoller` lancia subito, e il ping non partirebbe mai — proprio nei giorni in cui serve di più.
  Verificato dal vivo: con D1 bloccato l'eccezione arriva a `runPoller`, cioè **dopo** il ping.
- ⚠️ **`/vsop/health/ready` e non `/vsop/health`**: la sonda piena include il report di consistenza, che
  costa e fa I/O di rete. Farlo girare 1440 volte al giorno sarebbe uno spreco.
- ⚠️ **Cache-buster nell'URL**: davanti a vIPI c'è Cloudflare. Una risposta servita dalla cache
  tornerebbe `200` e **non sveglierebbe nessun processo** — il ping sembrerebbe funzionare senza fare niente.

### 🔴 Il sorgente vero non è questo

La modifica è stata fatta sul **bundle** scaricato da Cloudflare con `wrangler init --from-dash`, perché il
Worker è scritto in TypeScript e **quel sorgente non sta in questo repository** (il bundle contiene
`// src/index.ts`).

**Chi ripubblicherà dal TypeScript vero cancellerà il ping senza accorgersene.** La stessa modifica va
riportata là, e questo file è l'unico posto in cui la cosa è scritta.

## Come si verifica che il ping funzioni

Non dalle `curl` a mano — **anche quelle sono traffico** e tengono su il sito da sé. La prova è
`diagnostica/avvii.txt` sul server di vIPI, che registra avvii e arresti:

- **se le righe di avvio smettono di comparire**, il processo non muore più: il ping funziona;
- se ne compaiono ancora, un minuto non basta e l'intervallo va accorciato.

ℹ️ E il segno indiretto: la prima richiesta dopo l'inattività passava da **18,2 s** (avvio a freddo) a
**0,2 s**. Se resta sempre sotto il secondo, il processo è vivo.

## I comandi utili

```powershell
wrangler whoami
wrangler d1 info atc-archiver-db          # la quota: rows_read_24h e' il numero che conta
wrangler d1 list
wrangler deployments list --name atc-archiver
wrangler tail atc-archiver --format pretty
wrangler init --from-dash atc-archiver    # riscarica il bundle pubblicato
```

⚠️ **`wrangler d1 execute` consuma quota**: anche una `SELECT` da 14 righe. Nei giorni in cui la quota è
al limite, `d1 info` non costa niente e dice quel che serve.
