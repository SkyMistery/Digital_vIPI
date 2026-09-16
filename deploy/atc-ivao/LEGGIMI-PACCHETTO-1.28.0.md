# Pacchetto 1.28.0 — solo i file cambiati

> ✅ **CARICATO il 16 settembre 2026.** Controlli da fuori (soli GET anonimi): pacchetto tutto verde con la Ricerca;
> i sei file di `wwwroot` serviti hanno lo **sha256 identico** a quelli spediti; gli otto vSOP militari pubblicati
> mostrano la tabella delle aree e nessuna scheda. Restano da fare **col login**: riga `Schema` = `0`, un elenco
> annidato che sopravvive al ricarico, «+ Sottosezione» sul SOD di Decimomannu.

> **Timbro:** `1.28.0 · 4fd8ed7` (16 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.27.0** (`f6cbea3`, online dal 16 settembre). **MINOR, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP.
> **30 file**: 11 in **radice**, 1 in **`en/`**, 18 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **Il file che sembra di troppo e non lo è: `Vipi.Infrastructure.dll`.** Il suo codice non è cambiato, ma
> porta dentro una copia del limite di profondità delle sezioni — 3 in quello che c'è sul server, 5 in questo.
> Senza, la correzione per Decimomannu («la sottosezione non appare») **non arriverebbe**: il tasto sarebbe
> acceso e il server rifiuterebbe lo stesso.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Sezioni più profonde** (dal campo, SOD di Decimomannu): le sotto-sezioni arrivano a **cinque** livelli
  invece di tre. Sul fondo il tasto «+ Sottosezione» si spegne e dice perché.
- **«L'ultimo comando potrebbe non essere arrivato»**: dopo una ricarica causata dalla connessione caduta,
  una striscia avvisa che il gesto appena fatto potrebbe non essere stato salvato.
- **vSOP militari — Aree di lavoro e Bassa quota (BOAT)**: sotto la mappa c'è **una tabella sola**. L'elenco a
  schede è sparito; tipo e colore dell'area stanno nella riga, e la freccetta **▸** apre attivazione e
  descrizione. Le chip della mappa accendono e spengono anche le **righe**. In stampa i dettagli sono aperti.
  Vale subito anche per i vSOP già pubblicati: è un cambio di **impaginazione**, non di contenuto.
- **Editor, campi di testo**:
  - **elenchi dentro elenchi**, fino a cinque livelli: `- voce`, `-- sotto-voce`… e `1) voce`, `-1) sotto-voce`…,
    mescolabili; tasti **⇤ ⇥**, **Tab / Maiusc+Tab** su una voce, **Invio** che continua l'elenco. Nel
    documento ogni livello ha il suo simbolo (1 · a · I · i · A, e • – ◦ ▪ ·);
  - il campo **cresce col testo** fino a poco più di metà schermo; la maniglia resta.
  - Nella traduzione automatica i segni degli elenchi non vengono più mandati al traduttore.
- **Regole piste, editor**: il riquadro dell'esito del banco di prova dice **Tailwind e Vento traverso sulle
  piste in uso**; senza regola vincente, il perché è tradotto (prima usciva in italiano anche in inglese).

## I 30 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (18)** — si caricano e rinominano **per primi**, tutti:

```
vipi-aor.js             vipi-aor.js.br             vipi-aor.js.gz
vipi-editor.js          vipi-editor.js.br          vipi-editor.js.gz
vipi-print.css          vipi-print.css.br          vipi-print.css.gz
vipi-riconnessione.js   vipi-riconnessione.js.br   vipi-riconnessione.js.gz
vipi-theme.css          vipi-theme.css.br          vipi-theme.css.gz
vipi-ui.js              vipi-ui.js.br              vipi-ui.js.gz
```

**In radice e in `en/` (12)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll                    ← 🔴 anche se «non è cambiato»: vedi sopra
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinomina nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` e il suo `.dll`,
   `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Hosting.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`, `Vipi.AuroraProfiles.dll` e
`Vipi.AuroraBridge.Contracts.dll` (codice non cambiato), e `Vipi.Host.deps.json` /
`Vipi.Host.runtimeconfig.json`, identici a quelli sul server.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

- **Un vSOP militare pubblicato che abbia delle aree di lavoro scelte** (l'elenco è `/services/vsop/mil`; ⚠️
  non tutti ne hanno — una sezione senza aree mostra solo la tabella vuota): sotto la mappa una **tabella**,
  nessun elenco a schede; la freccetta ▸ apre una riga; spegnendo una chip la sua
  riga sparisce e il conteggio «ne vedi N su M» cambia.

Col login da amministratore:

- il timbro in barra: **`1.28.0 · 4fd8ed7`**;
- `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`** (nessuna migrazione: deve restare com'era);
- in un editor, un **campo di testo**: sopra ci sono i tasti ⇤ ⇥; scrivendo `- uno`, Invio, Tab, `due` si
  ottiene `-- due`; salvato e **ricaricata la pagina**, il testo c'è ancora;
- in un documento con sezioni profonde (il SOD di Decimomannu): «+ Sottosezione» sotto «Procedure di partenza
  › VFR» ora **funziona**.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da sapere

- **Gli elenchi già scritti** non cambiano: `- voce` e `1. voce` restano elenchi di primo livello. Una riga
  che comincia con `-- ` seguita da testo ora è una voce di secondo livello (nell'archivio di sviluppo non ce
  n'era nessuna).
- **Il processo del server viene spento ~10 secondi dopo l'ultima richiesta** (`diagnostica/avvii.txt`): è la
  causa del «gesto perso». La striscia nuova lo **segnala**, non lo evita. ▶ Lato pannello:
  `passenger_min_instances` e il timeout di inattività di Passenger.
- **Da fare, senza fretta** (non dipendono da questo pacchetto): ripubblicare gli APP non remotizzati rimasti
  col vecchio indice (LIBA_APP, LICT_APP, LIRP_APP).

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Come per 1.27.0: il pacchetto è parziale e non può aggiornare il runtime. Lo sistemerà il passaggio a
**.NET 10**, che ora sarà **1.29.0** (carico completo): il numero 1.28.0 lo prende questo pacchetto, partito
prima.

# Il pannello: come da 1.25.4

- ✅ **Le direttive nginx** per i file statici.
- ✅ **`passenger_min_instances ≥ 1`** — ⚠️ ma `avvii.txt` del 16 settembre dice che il processo si ferma
  ancora entro pochi secondi: va ricontrollato.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).
- ⚠️ Il server risulta **Debian 11**, fuori dal supporto LTS dal 31 agosto 2026.
