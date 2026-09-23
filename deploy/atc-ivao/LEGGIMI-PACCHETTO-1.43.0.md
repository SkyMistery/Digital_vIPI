# Pacchetto 1.43.0 — solo i file cambiati

> **Timbro:** `1.43.0 · 54355eb` (23 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.42.1** (`a66f25e`, online dal 22 settembre). È una **MINOR con UNA migrazione del database**,
> **additiva**: crea una **vista** (`v_share_atc_sessions`) e non tocca nessuna tabella né nessun dato. Il
> pacchetto si consegna da solo via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **8 file**, tutti in **radice**. Niente in `wwwroot`.
>
> 🔴 **Copia di sicurezza del database prima del carico** (Diagnostica → copia del database). La migrazione non
> cancella niente, ma è la regola per ogni consegna con migrazione.
>
> 🔴 **L'utente MariaDB della vIPI deve avere il privilegio `CREATE VIEW`** su `itivao_atc`. ✅ Verificato dal
> committente il 23 settembre. Senza, la migrazione fallisce e **il sito non parte**.
>
> 🔴 **`Vipi.Infrastructure.MySqlMigrations.dll` è DENTRO e non va dimenticato.** Senza, il sito parte lo stesso e
> sembra a posto, ma la vista non nasce. Nessun errore lo segnalerebbe.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Una vista per l'IVAO Division Hub.** Il database della vIPI espone una vista di sola lettura,
  `v_share_atc_sessions`, con dieci colonne delle sessioni ATC (chi, quale posizione, quando). L'hub la leggerà
  con un utente MariaDB **suo**, che vede solo quella vista e nessuna tabella. Per la vIPI non cambia niente.
- **La copia del database porta la vista.** Prima si sarebbe fermata davanti a qualunque vista; ora copia le
  viste `v_share_` dopo le tabelle.
- **Editor APP unito: «sezioni comuni» non ricarica più la pagina** (segnalato su LIRE). Aprire le sezioni comuni
  da un'APP unita ridisegnava tutto l'editor e si perdeva il punto in cui si era.

## Gli 8 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In radice (8)**, in quest'ordine:

```
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← la migrazione
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Application`, `Vipi.Domain`, `Vipi.Hosting` e gli altri assiemi: il loro codice non è
cambiato e differiscono solo perché ricompilati. Restano fuori anche `en/Vipi.Ui.resources.dll` (nessuna frase
cambiata), `deps.json`, `runtimeconfig.json`, `appsettings.json`, `Vipi.Host.staticwebassets.endpoints.json` e
tutto `wwwroot`: identici a 1.42.1, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro **`1.43.0 · 54355eb`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nel log del giorno (`diagnostica/log-*.txt`), all'avvio, la riga
  «*Applying migration '20260923100945_VistaCondivisaSessioniAtc'*»;
- facoltativo, da phpMyAdmin col database `itivao_atc`: la vista `v_share_atc_sessions` compare fra le tabelle
  (segnata come vista) e `SELECT * FROM v_share_atc_sessions LIMIT 5;` risponde;
- l'**editor** di un'APP unita (es. LIRE): aprire «sezioni comuni» **non** ricarica la pagina.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da fare (NON urgenti)

- **Il permesso per l'hub, quando l'hub c'è.** L'utente MariaDB dell'hub oggi **non esiste**: lo crea chi
  amministra il server quando l'IVAO Division Hub va in produzione, con un tetto di connessioni suo. Poi, da
  amministratore:

  ```sql
  GRANT SELECT ON itivao_atc.v_share_atc_sessions TO '<utente_hub>'@'<host_hub>';
  ```

  `<host_hub>` è la macchina da cui l'hub si collega: `localhost` se gira sullo stesso server, altrimenti il suo
  indirizzo.

  Finché non c'è, la vista esiste e nessuno la legge: **non si rompe niente**. Fino ad allora l'hub mostra la
  sua risposta di ripiego («dati vIPI non disponibili»).
