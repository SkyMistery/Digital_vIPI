# §A47 — La copia del database si scarica dalla Diagnostica

> 16 settembre 2026, sera. Carta scritta **prima** del codice (FEATURE-PROCESS). ✅ Fatta lo stesso giorno, in `main`,
> non in pacchetto: esito e prove in `docs/lavori-aperti.md` §A47.
> Richiesta del committente: *«dalla sezione admin posso scaricarmi tutto il DB in modo da avere una copia
> locale di sicurezza e non dover chiedere al webmaster ogni volta?»* — scelta la forma **A**: un `.sql.gz`
> che il webmaster importa così com'è.

## 0. Il vincolo che decide la forma

L'host è **Plesk + Passenger**, senza shell: `mysqldump` non si può lanciare. Il file lo scrive **il sito**,
leggendo MariaDB con la stessa libreria con cui già ci parla (MySqlConnector, sotto Pomelo). Da qui tutto il
resto.

## 1. Pre-flight

1. **Modello.** Nessuna entità nuova. Il registro di chi scarica è l'`AuditLog` che c'è già
   (`EntityType = "DatabaseBackup"`, azione `View` come le statistiche altrui: è una *lettura*).
2. **Dispatch.** Un provider solo sa fare la copia (MySql). Sugli altri il servizio risponde «non disponibile»
   e la scheda lo dice: niente switch per provider, un `IsSupported`.
3. **Ingressi + verifica.** Una scheda nella colonna destra di `/services/vsop/admin/diagnostics`, solo Admin.
   Verifica dal vivo sul MariaDB locale 11.4.10 (`deploy/mariadb/README.md`): scaricare dal tasto,
   reimportare col client `mariadb` in un database **vuoto**, confrontare `CHECKSUM TABLE` tabella per
   tabella con l'originale.
4. **Propagazione.** Additiva, niente da rinominare.

## 2. Il file

```
-- vipi-backup formato=1
-- vIPI - copia di sicurezza del database
-- Versione del sito: 1.28.0 · 0473cde
-- Creata: 2026-09-16T16:07:36Z
-- Server: 11.4.10-MariaDB
-- Ultima migrazione: 20260915194926_StazioneMeteoDiRiferimento
-- Escluse di proposito: DataProtectionKeys
-- Ripristino: gunzip -c <file> | mariadb -u <utente> -p <database>
-- ATTENZIONE: SOSTITUISCE le tabelle che contiene (DROP TABLE IF EXISTS).

SET NAMES utf8mb4;
SET @VIPI_OLD_FK=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;      -- e UNIQUE_CHECKS, SQL_MODE, TIME_ZONE
SET @VIPI_OLD_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO';

DROP TABLE IF EXISTS `Accs`;
CREATE TABLE `Accs` (…)                              -- da SHOW CREATE TABLE, com'è
INSERT INTO `Accs` (`Id`,`Code`,…) VALUES (…),(…);   -- a blocchi di ~1 MB, UNA riga di testo per istruzione
-- vipi-tabella `Accs` righe=28
…
SET FOREIGN_KEY_CHECKS=@VIPI_OLD_FK;                  -- le impostazioni tornano com'erano
-- vipi-backup-fine tabelle=62 righe=61227 sha256=ba4bfbf8…e191
```

- 🔴 **La riga di chiusura è la prova che il file è intero.** Lo `sha256` copre **tutti i byte prima** di
  quella riga. Un download interrotto non ha la riga; un file ritoccato non torna con l'impronta. Lo sa
  controllare `tools/Vipi.DbBackup verifica <file>`, senza nessun database.
- **Una transazione sola** `WITH CONSISTENT SNAPSHOT, READ ONLY`: chi salva durante il download non lascia la
  copia a metà fra due stati.
- **Si scrive mentre si legge**, compresso al volo: niente file temporanei sul server (la cartella dell'app è
  quella che l'FTP sovrascrive) e niente copia intera in memoria.
- **Valori fedeli al byte**: la connessione della copia chiede `GuidFormat=None` e `TreatTinyAsBoolean=false`,
  così un `CHAR(36)` torna la stringa com'è salvata e un `TINYINT(1)` il numero. I binari escono `0x…`.
  Un tipo che il formattatore non conosce **ferma la copia** col nome di tabella e colonna: una copia che
  inventa un valore è peggio di una copia che non c'è.

## 3. Che cosa resta fuori, di proposito

| Fuori | Perché |
|---|---|
| `DataProtectionKeys` | sono le chiavi che decifrano i cookie di accesso: scaricarle è solo rischio |
| `segreti/`, `vipi-keys/`, `appsettings.Production.json` | non stanno nel database; restano del backup del vhost |

## 4. Le guardie

- **Cancello in due sedi**: la pagina nasconde la scheda sotto Admin, il servizio chiama `EnsureAdmin()`, e
  l'indirizzo risponde **404** a chi non è Admin (non 403: non si annuncia che esiste).
- **Una copia alla volta** per processo: la seconda richiesta riceve 429. Una copia costa letture su tutto il
  database, e il sito è uno solo.
- **Registro**: una riga a inizio («copia richiesta») e una a fine con tabelle, righe, byte e `sha256`: chi
  ha il file può confrontarlo con quello che il server dice di aver spedito.
- `Cache-Control: no-store` — davanti c'è Cloudflare.

## 5. Ripristino (per il webmaster)

```sh
gunzip -c vipi-copia-….sql.gz | mariadb -u <utente> -p itivao_atc
```

🔴 **Sostituisce le tabelle** (`DROP TABLE IF EXISTS`): si fa a sito fermo, e su un database di prova se lo
scopo è guardare. Le tabelle che il file non porta (`DataProtectionKeys`) non vengono toccate.

## 6. Fette

1. Carta (questa).
2. Scrittore puro (`SqlDumpWriter`) + verificatore (`SqlDumpVerifier`), su tutti e due i TFM, coi test.
3. Sorgente MariaDB (`MySqlDumpSource`, solo net8) + servizio con cancello e registro + indirizzo.
4. Scheda in Diagnostica + narratore del registro + testi IT/EN.
5. `tools/Vipi.DbBackup` (`verifica`, `scrivi`) + andata e ritorno contro MariaDB vero nella CI.
6. Verifica dal vivo, documenti, memoria.
