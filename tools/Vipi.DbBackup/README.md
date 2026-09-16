# Vipi.DbBackup

La copia di sicurezza del database, fuori dal sito (§A47, carta
`docs/feature/2026-09-16-copia-del-database.md`). Il file lo scarica un Admin da **Diagnostica → Copia del
database**; questo strumento serve a **controllarlo** e, a chi ha accesso diretto a MariaDB, a **scriverlo**.

Scrittore e verificatore sono quelli del sito (`Vipi.Infrastructure/DatabaseCopy`): il file che controlla questo
strumento è lo stesso che esce dal tasto, non un'imitazione.

## Controllare una copia scaricata — nessun database

```sh
dotnet run --project tools/Vipi.DbBackup -- verifica vipi-copia-2026-09-16-2105Z-1.28.1-abcdef0.sql.gz
```

Esce **0** se il file è intero e l'impronta torna, **1** altrimenti, e dice perché:

| Esito | Che cosa vuol dire |
|---|---|
| `INCOMPLETA` | manca la riga di chiusura: download interrotto, o la copia si è fermata a metà sul server |
| `L'impronta non torna` | il file è cambiato dopo essere stato scritto (anche solo i fine riga) |
| `I conti non tornano` | la riga di chiusura è stata riscritta |
| `Non è una copia della vIPI` | la prima riga non è `-- vipi-backup formato=1` |

⚠️ **L'impronta non è una firma**: prende i guasti (download interrotto, disco, un editor che cambia i fine
riga), non chi modifica il file apposta e la ricalcola. Contro quello, l'impronta stampata si confronta con quella
che il **registro di audit** ha annotato alla fine del download (`/services/vsop/admin/audit`).

`Istruzione max` è l'`INSERT` più lungo del file: il server su cui si ripristina deve avere un
`max_allowed_packet` almeno così grande (16 MB di default; lo strumento avvisa se non basta).

## Ripristinare — lo fa chi ha accesso a MariaDB

In quest'ordine, e nessun passo è facoltativo:

1. **`verifica`** deve dire INTERA. Un file troncato può essere un gzip valido quanto uno intero, e
   `gunzip | mariadb` eseguirebbe la metà che c'è.
2. **In un database VUOTO**, col sito alla **stessa versione** della copia (riga «Ultima migrazione»). Il file
   cancella e ricrea solo le tabelle che porta: una tabella nata da una migrazione successiva resterebbe lì, e
   all'avvio il sito proverebbe a ricrearla e non ripartirebbe.
3. Col pacchetto grande e `pipefail`, così un gunzip che si interrompe ferma tutto:

```sh
set -o pipefail
gunzip -c vipi-copia-….sql.gz | mariadb --max-allowed-packet=1G -u <utente> -p <database>
```

Il file gira in `STRICT_ALL_TABLES`: un valore che non ci sta è un errore, non un avviso che il client non
stampa. `DataProtectionKeys` non è nella copia: il sito la ricrea all'avvio, e chi era entrato rientra.
⚠️ Su un MariaDB **Windows** (`lower_case_table_names=1`) i nomi delle tabelle diventerebbero minuscoli: si
ripristina su Linux, o con `lower_case_table_names=2` (vedi `deploy/mariadb/README.md`).

## Scrivere una copia direttamente dal database

```sh
dotnet run --project tools/Vipi.DbBackup -- scrivi "Server=…;Database=itivao_atc;User Id=…;Password=…" copia.sql.gz
```

La stessa copia del tasto, **senza** cancello né registro. La usa la CI.

## L'andata e ritorno (`andata-e-ritorno.sh`)

Scrive `valori-scomodi.sql` nel database di partenza (apici, barre, `\0`, emoji, `TIME` oltre 24 ore, Id 0,
`bigint unsigned` massimo, `double` a 17 cifre, un blob da 3 MB, 2 MB di righe per spezzare gli `INSERT`), fa la
copia, la verifica, la reimporta col client `mariadb` in un database vuoto e confronta `CHECKSUM TABLE` **e**
`SHOW CREATE TABLE` tabella per tabella. Gira nel job `mariadb-schema` della CI contro MariaDB 11.4.10.
⚠️ Scrive nella sorgente — anche una riga finta in `DataProtectionKeys`, che rompe i cookie di un sito acceso su
quel database: solo su un database di prova, e a sito fermo.

```sh
MARIADB="mariadb -h 127.0.0.1 -P 3399 -u root" \
CONN="Server=127.0.0.1;Port=3399;Database=vipi_prova_copia;User Id=root;" \
SORGENTE=vipi_prova_copia bash tools/Vipi.DbBackup/andata-e-ritorno.sh
```

Provata il 16 settembre 2026 sul MariaDB locale (`deploy/mariadb/README.md`) con i dati di sviluppo travasati
da `Vipi.DbSeed`: 63 tabelle, 62 390 righe, `CHECKSUM` e definizioni identiche dopo il ripristino. E provata
**al contrario**: togliendo `NO_AUTO_VALUE_ON_ZERO` dalla testata, cade su `ProvaCopia`.
