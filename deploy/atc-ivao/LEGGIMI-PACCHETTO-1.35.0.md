# Pacchetto 1.35.0 — solo i file cambiati

> **Timbro:** `1.35.0 · b38359b` (21 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.34.3** (`f95d923`, online dal 19 settembre). **MINOR con UNA migrazione**: il pacchetto si consegna
> da solo via FTP, il database si aggiorna all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **17 file**: 13 in **radice**, 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **PRIMA CONSEGNA CON UNA MIGRAZIONE DAL 1.27.0 (16 settembre).** Due cose che le ultime sei consegne non
> chiedevano:
> 1. **una copia di sicurezza del database PRIMA del carico** — la scarica un Admin dalla Diagnostica;
> 2. **`Vipi.Infrastructure.MySqlMigrations.dll` DEVE essere fra i file caricati.** Senza, la tabella resta col
>    nome vecchio mentre il codice nuovo chiede quello nuovo: il sito si apre e la home funziona — è la lezione
>    della 1.19.0, «sembra funzionare» — ma ogni pagina che legge le procedure (editor di aeroporto, sezioni SID
>    e STAR dei documenti) va in errore. La prova col login è la riga **`Schema` = `0`** (sotto).
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Le STAR** (arrivi) entrano nella vIPI, dal sectorfile come le SID.
  - **Import** dai file `.str` nello stesso giro delle partenze, con lo stesso ciclo AIRAC.
  - **Editor**: sotto la tabella delle SID ce n'è una identica per le STAR (senza «Initial climb», che sugli
    arrivi non c'è, e senza il tasto di reimport, che vale per tutto lo scalo).
  - **Sezione «STAR»** nel documento, subito dopo «SID», nel profilo civile e in quello militare.
  - **`[[STAR LIRF ELKA3A]]`** nel testo, accanto a `[[SID …]]`: il nome segue la procedura quando si aggiorna.
- **I riferimenti ai dati nel testo.** Il tasto del campo di prosa si chiama ora **«Cita»** e apre **un
  selettore solo**, con sei chip:

  | Chip | Scrive | Che cosa mostra a chi legge |
  |---|---|---|
  | `SID` / `STAR` | `[[SID LIBD BANAV 5Z]]` | il nome di **oggi** della procedura |
  | `FREQ` | `[[FREQ LIBD_TWR]]` | la frequenza di **oggi** di quell'ente |
  | `ATC` | `[[ATC LIBD_TWR]]` | il nominativo radio di **oggi** |
  | `RWY` | `[[RWY LIBD 07]]` | la soglia, come è scritta |
  | `FIX` | `[[FIX BANAV]]` | il punto, come è scritto |

  In testata all'editor compare l'avviso per quel che **non si trova più** nell'archivio, con la sezione in cui
  sta. ⚠️ Una sorgente che non risponde **non** genera avvisi: «non lo so» non è «non c'è».
- **Il campo di prosa dell'editor si adatta al testo** che contiene, come doveva fare dal 16 settembre: una
  regola di foglio di stile ne scavalcava l'altezza.
- Più le **nove correzioni** della rilettura di tutto il lavoro, due delle quali sul percorso Postgres (che qui
  non gira) e sette sul comportamento visibile.

## La migrazione

| Migrazione | Che cosa fa |
|---|---|
| `20260920115024_ProcedureKindEStarNellaStessaTabella` | rinomina la tabella `AirportSids` in `AirportProcedures` e le aggiunge la colonna `Kind` (`Sid` / `Star`); le righe che ci sono diventano tutte `Sid` |

⚠️ **Non è una `AddColumn` e basta**: c'è anche un `RENAME TO`. Il corpo è stato **scritto a mano** — lo
scaffolding proponeva di buttare la tabella e rifarla, cioè di perdere le **1469 righe SID** che stanno in
produzione — ed è stato provato su una **copia** del database di produzione, in avanti e indietro.
Le righe restano tutte al loro posto: nessun dato si perde, nessuno si riscrive.

## I 17 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**, tutti e tre:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice e in `en/` (14)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← 🔴 la migrazione: senza questo la colonna non nasce
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. **prima** la copia di sicurezza del database (Diagnostica → copia del database);
2. si caricano **tutti** col nome finto, ognuno nella sua cartella;
3. si rinomina nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` e il suo `.dll`,
   `Vipi.Host.dll` per ultimo;
4. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Hosting.dll`, `Vipi.AuroraProfiles.dll` e `Vipi.AuroraBridge.Contracts.dll`: il loro
codice non è cambiato e differiscono solo per ricompilazione (controllato che nessuno dei tre nomini un tipo
cambiato né implementi una delle interfacce che hanno guadagnato un membro). Fuori anche `deps.json`,
`runtimeconfig.json`, `appsettings.json` e tutto il resto di `wwwroot`: identici a 1.34.3, controllato per
impronta — 466 file confrontati, 443 identici.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.35.0 · b38359b`**;
- 🔴 `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`**: vuol dire che la migrazione è entrata. Se
  non è `0`, manca quasi certamente `Vipi.Infrastructure.MySqlMigrations.dll` — e in quel caso la tabella ha
  ancora il nome vecchio e le pagine che leggono le procedure vanno in errore;
- in un **editor di aeroporto**, dopo Ctrl+F5: sotto la tabella delle SID c'è quella delle **STAR**, e il tasto
  del campo di prosa dice **«Cita»** e apre un selettore con **sei** chip;
- lo stesso campo di prosa, scrivendoci dentro molte righe, **si allunga** invece di restare alto tre righe.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da sapere e da fare

- 🔴 **La sezione «STAR» compare nei documenti PUBBLICATI solo dalla prossima release di ciascuno.** Non è un
  difetto: un documento pubblicato è una fotografia, e le sezioni nuove entrano quando si ripubblica. Nella
  **bozza** si vede subito.
- 🔴 **Le STAR appena importate aspettano il ciclo d'entrata** che la sorgente dichiara. Finché quel ciclo non
  arriva, la sezione dice «STAR non ancora inserite»: è il cancello AIRAC che fa il suo mestiere, non un guasto.
  (Su LIBD il ciclo dichiarato è `2610`.)
- **Le sezioni «STAR» dei documenti già scritti** le semina la manutenzione all'avvio: non c'è niente da fare a
  mano.
- ⚠️ Da tenere d'occhio, non risolto in questo pacchetto: `ConnectionError` di EF su MariaDB, **3 volte in 36
  ore** fra il 18 e il 19 settembre, senza nessuna richiesta fallita. Se cresce si guardano il pool
  (`MaximumPoolSize=20`) e `max_connections`.

# ⚠️ Il runtime .NET del server è ancora 8.0.28
