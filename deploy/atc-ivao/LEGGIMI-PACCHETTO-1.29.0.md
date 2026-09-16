# Pacchetto 1.29.0 — solo i file cambiati

> **Timbro:** `1.29.0 · 49a2dd5` (16 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.28.0** (`4fd8ed7`, online dal 16 settembre). **MINOR, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP.
> **15 file**: 11 in **radice**, 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **Il file che sembra di troppo e non lo è: `Vipi.Host.dll`.** Il suo codice non è cambiato, ma porta il
> **timbro**: senza, la barra direbbe ancora `1.28.0` e non si saprebbe che cosa gira.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Copia del database** (nuova, solo amministratori): in `services/vsop/admin/diagnostics` c'è la scheda
  **«Copia del database»**. Il tasto «Scarica la copia» porta via tutto il database in un file
  `vipi-copia-<data>-1.29.0-49a2dd5.sql.gz`, senza chiedere al webmaster.
  - Lo scrive il sito, da una fotografia coerente del database: chi salva nel frattempo non la lascia a metà.
  - L'ultima riga del file porta un'impronta **sha256**; `tools/Vipi.DbBackup verifica <file>` dice se il file è
    **intero**. Ogni copia lascia due righe nel **registro** (richiesta, e fine con l'impronta).
  - Restano fuori, di proposito, le chiavi dei cookie di accesso (`DataProtectionKeys`) e tutto ciò che non sta
    nel database: `segreti/`, `vipi-keys/`, `appsettings.Production.json`.
  - 🔴 **Il file contiene VID e nomi dello staff**: va custodito come dato personale.
- **La frequenza principale (★) è blu in tutte le tabelle**: prima nelle vIPI ACC, negli APP, nelle vLOA e nella
  vista Live la riga con la stella non si evidenziava.
- **vSOP militari, pagina `/services/vsop/mil`**: le sezioni dell'introduzione partono **chiuse**, col titolo in
  vista. Chi le aveva aperte o chiuse a mano ritrova la sua scelta.
- Una correzione interna che non si vede ma conta: una parte comune delle pagine poteva **far cadere il
  processo** (stack overflow) se usata in un certo modo. Online nessuna pagina la usava così; la scheda nuova
  sì, ed è corretta prima di arrivare.

## I 15 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-theme.css          vipi-theme.css.br          vipi-theme.css.gz
```

**In radice e in `en/` (12)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
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

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`, `Vipi.AuroraProfiles.dll` e
`Vipi.AuroraBridge.Contracts.dll` (codice non cambiato), e `Vipi.Host.deps.json` /
`Vipi.Host.runtimeconfig.json`, identici a quelli sul server.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.29.0 · 49a2dd5`**;
- `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`** (nessuna migrazione: deve restare com'era);
- 🔴 **la pagina Diagnostica si apre** e a destra c'è la scheda «Copia del database» — è la pagina in cui viveva
  il guasto corretto, quindi si guarda che resti su anche dopo un ricarico;
- **«Scarica la copia»**: il browser salva un `.sql.gz` di qualche MB (in locale, con i dati di sviluppo, 6 MB in
  meno di un secondo). Poi, sul proprio computer:

  ```
  dotnet run --project tools/Vipi.DbBackup -- verifica <file scaricato>
  ```

  deve dire **«la copia è INTERA e l'impronta torna»** e `Versione del sito . 1.29.0 · 49a2dd5`. ⚠️ **Non basta che
  il file arrivi**: davanti ci sono Cloudflare e Passenger, ed è proprio questo controllo a dire se ci passa
  intero. Ricaricando la Diagnostica, la scheda dice «completata» con la stessa impronta.
- un APP o una vIPI ACC con una frequenza principale: la riga con la ★ è **blu**.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da sapere

- **Ripristinare una copia** lo fa chi ha accesso a MariaDB, e in quest'ordine (è scritto anche in testa al
  file): (1) `verifica` deve dire INTERA; (2) in un database **vuoto**, col sito alla **stessa versione** della
  copia; (3) `set -o pipefail; gunzip -c <file> | mariadb --max-allowed-packet=1G -u <utente> -p <database>`.
  Sopra un database esistente di un'altra versione il sito può non ripartire.
- **Se il download non parte**: un messaggio al posto del file vuol dire che la copia si è rifiutata di
  cominciare, e dice perché (per esempio un tipo di dato che non saprebbe riportare indietro). «C'è già una
  copia in corso» vuol dire aspettare un minuto.
- La copia **non sostituisce** il backup dell'hosting: `segreti/` e `vipi-keys/` non stanno nel database.
- **Il processo del server viene spento ~10 secondi dopo l'ultima richiesta** (`diagnostica/avvii.txt`): durante
  un download la richiesta è aperta, quindi non lo interrompe.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Come per 1.28.0: il pacchetto è parziale e non può aggiornare il runtime. Lo sistemerà il passaggio a
**.NET 10**, che ora sarà **1.30.0** (carico completo): il numero 1.29.0 lo prende questo pacchetto, partito
prima.

# Il pannello: come da 1.25.4

- ✅ **Le direttive nginx** per i file statici.
- ✅ **`passenger_min_instances ≥ 1`** — ⚠️ ma `avvii.txt` del 16 settembre dice che il processo si ferma
  ancora entro pochi secondi: va ricontrollato.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md). ⚠️ La copia del database risponde `Cache-Control: no-store`: una
  regola che forzasse la cache su `/services/vsop/admin/*` sarebbe un errore grave.
- ⚠️ Il server risulta **Debian 11**, fuori dal supporto LTS dal 31 agosto 2026.
