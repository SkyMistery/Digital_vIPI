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

L'impronta sha256 stampata è la stessa che il **registro di audit** ha annotato alla fine del download: chi ha
il file la confronta con `/services/vsop/admin/audit`.

## Ripristinare — lo fa chi ha accesso a MariaDB

```sh
gunzip -c vipi-copia-….sql.gz | mariadb -u <utente> -p <database>
```

🔴 **Sostituisce** le tabelle che contiene (`DROP TABLE IF EXISTS` + `CREATE TABLE`): a sito fermo, oppure su
un database di prova se lo scopo è guardare. `DataProtectionKeys` non è nella copia e non viene toccata; se il
database è nuovo il sito la crea all'avvio, e chi era entrato rientra.

## Scrivere una copia direttamente dal database

```sh
dotnet run --project tools/Vipi.DbBackup -- scrivi "Server=…;Database=itivao_atc;User Id=…;Password=…" copia.sql.gz
```

La stessa copia del tasto, **senza** cancello né registro. La usa la CI.

## L'andata e ritorno (`andata-e-ritorno.sh`)

Scrive `valori-scomodi.sql` nel database di partenza, fa la copia, la verifica, la reimporta col client
`mariadb` in un database vuoto e confronta `CHECKSUM TABLE` tabella per tabella. Gira nel job
`mariadb-schema` della CI contro MariaDB 11.4.10. ⚠️ Scrive nella sorgente: solo su un database di prova.

```sh
MARIADB="mariadb -h 127.0.0.1 -P 3399 -u root" \
CONN="Server=127.0.0.1;Port=3399;Database=vipi_prova_copia;User Id=root;" \
SORGENTE=vipi_prova_copia bash tools/Vipi.DbBackup/andata-e-ritorno.sh
```

Provata il 16 settembre 2026 sul MariaDB locale (`deploy/mariadb/README.md`) con i dati di sviluppo travasati
da `Vipi.DbSeed`: 63 tabelle, 61 385 righe, tutte identiche dopo il ripristino. E provata **al contrario**:
togliendo `NO_AUTO_VALUE_ON_ZERO` dalla testata, cade su `ProvaCopia`.
