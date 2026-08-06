# MariaDB locale per provare il ramo di produzione

Il server di `atc.it.ivao.aero` è **MariaDB 11.4.10-deb11**, non MySQL. Questa è la ricetta per averne una
identica in locale, che serve a tre cose: applicare davvero le migrazioni, travasare i dati da Neon (A2/A3),
e guidare l'app con la skill `verifica-live` (A6). Il 3306 loro è su `localhost` del loro server, quindi da
qui non è raggiungibile: tutto ciò che si prova, si prova qui.

> Sostituisce [`../mysql/README.md`](../mysql/README.md), che descrive un MySQL 8.4 di Oracle e un provider
> che **non regge MariaDB**. Quel file resta come storia, non come istruzione.

Nessuna installazione di sistema: niente servizio Windows, niente registro, nessun privilegio di
amministratore. Si cancella cancellando la cartella.

Eseguita per intero il **6 agosto 2026** (voce A1 di `docs/lavori-aperti.md`): tutti i risultati qui sotto
sono osservati, non attesi.

---

## 1. Procurarsi i binari

MariaDB non ha il gating di Oracle: lo ZIP si scarica da riga di comando, senza browser e senza `winget`.

```bash
curl -o mariadb-11.4.10-winx64.zip \
  https://archive.mariadb.org/mariadb-11.4.10/winx64-packages/mariadb-11.4.10-winx64.zip   # ~95 MB
```

```powershell
Expand-Archive -Path mariadb-11.4.10-winx64.zip -DestinationPath <destinazione>
```

I binari stanno in `<destinazione>\mariadb-11.4.10-winx64\bin` e si chiamano `mariadbd.exe`, `mariadb.exe`,
`mariadb-admin.exe`, `mariadb-dump.exe` (i vecchi nomi `mysqld`/`mysql` esistono ancora come alias).

**La versione è `11.4.10` esatta**, la stessa di `SELECT VERSION()` sul loro server. È anche il default di
`MySqlSchema.DefaultMariaDbVersion`, che Pomelo usa per decidere quale SQL generare.

Percorso usato per la prova: `D:\Programmazione\IVAO_Test\_mariadb` — **fuori dal repo**, perché serve anche
alle sessioni successive (A2÷A6) e non deve finire in git.

## 2. Inizializzare e avviare

```ini
# my.ini
[mysqld]
basedir=<destinazione>/mariadb-11.4.10-winx64
datadir=<destinazione>/data
port=3399
bind-address=127.0.0.1
log-error=<destinazione>/mariadb-error.log
# Come il pacchetto Debian di MariaDB 11.x (50-server.cnf), che è ciò che gira da loro:
character-set-server=utf8mb4
collation-server=utf8mb4_uca1400_ai_ci
```

⚠️ **Percorsi con `/`, non con `\`**: in un file di opzioni il backslash è un escape e `\s` significa
**spazio**, quindi l'avvio muore con «Can't create directory» su un percorso che a occhio sembra giusto.

La porta è **3399** e non 3306 per non litigare con nient'altro sulla macchina.

⚠️ **Il default di server è `latin1_swedish_ci`** se non lo si scrive: lo ZIP di Windows non ha il
`50-server.cnf` di Debian. Lasciarlo lì renderebbe la prova *più severa* del vero, ma non *uguale* al vero.
La condizione che vogliamo riprodurre è quella loro: default utf8mb4 **ai_ci**, cioè insensibile a maiuscole
e accenti — è il default che le nostre colonne devono sovrascrivere per conto proprio.

```powershell
& "<bin>\mariadb-install-db.exe" --datadir="<destinazione>\data" --port=3399 --default-user
Start-Process "<bin>\mariadbd.exe" -ArgumentList "--defaults-file=`"<destinazione>\my.ini`"" -WindowStyle Hidden
```

## 3. Ricreare le condizioni loro, non condizioni comode

Il database va creato **senza specificare la collation**, così eredita il default del server esattamente come
`itivao_atc` da loro; e l'utente va creato **con permessi solo su quel database**, così un privilegio mancante
si scopre adesso e non al cutover.

```sql
CREATE DATABASE itivao_atc;                                  -- niente COLLATE: come il loro
CREATE USER 'itivao_atc'@'%' IDENTIFIED BY '<password>';
GRANT ALL PRIVILEGES ON itivao_atc.* TO 'itivao_atc'@'%';
FLUSH PRIVILEGES;
```

⚠️ **`mariadb-install-db` su Windows crea due utenti anonimi** (`''@'localhost'` e `''@'<nomepc>'`). Il
matching degli utenti preferisce l'host più specifico, quindi una connessione a `127.0.0.1` viene risolta
come *anonima* e l'applicazione muore con `Access denied for user 'itivao_atc'@'localhost' (using password:
YES)` — un messaggio che accusa la password mentre il problema è l'utente. Su un server vero non ci sono, e
qui si tolgono:

```sql
DELETE FROM mysql.global_priv WHERE User=''; FLUSH PRIVILEGES;
```

## 4. Applicare lo schema

Due strade, **ed entrambe vanno provate**, perché in produzione conta la seconda:

```sh
# a) esplicita, dagli strumenti EF — il progetto è net8 soltanto, niente --framework
dotnet ef database update \
  --project src/Vipi.Infrastructure.MySqlMigrations \
  --startup-project src/Vipi.Infrastructure.MySqlMigrations \
  --connection "Server=127.0.0.1;Port=3399;Database=itivao_atc;User Id=itivao_atc;Password=<password>"
```

```powershell
# b) all'avvio dell'host, che è ciò che succederà al cutover (MigrateVipiDatabase)
$env:Persistence__Provider="MySql"
$env:ConnectionStrings__Vipi="Server=127.0.0.1;Port=3399;Database=itivao_atc;User Id=itivao_atc;Password=<password>"
dotnet run --project src/Vipi.Host --no-build --urls http://localhost:5034
```

⚠️ **Se fallisce a metà, il database resta sporco**: la DDL di MySQL/MariaDB non è transazionale, e il
tentativo successivo muore con «Table 'accs' already exists», che sembra un altro problema. Prima di
riprovare: `DROP DATABASE itivao_atc;` e si rifà il punto 3.

⚠️ Pomelo emette **`ALTER DATABASE CHARACTER SET utf8mb4;`** come prima istruzione della migrazione. Con un
`GRANT ALL ON itivao_atc.*` passa (è ALTER *sul database*, non globale), ma se il loro utente avesse una
lista di privilegi ritagliata, questa è la riga che si pianta per prima.

## 5. Verificare che sia andata davvero

```sql
-- 1. tutte le colonne stringa hanno la collation nostra
SELECT COLLATION_NAME, COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA='itivao_atc' AND COLLATION_NAME IS NOT NULL GROUP BY COLLATION_NAME;
-- osservato: 163 utf8mb4_uca1400_as_cs + 2 utf8mb4_general_ci, che sono __EFMigrationsHistory —
-- creata da EF PRIMA della nostra migrazione. Innocuo: quei valori li genera e li confronta EF.

-- 2. la prova vera: due valori che differiscono solo per le maiuscole convivono nell'indice unico
INSERT INTO Accs (Code,Name,CountryPrefix,IsMilitary,IsForeign,IsHidden) VALUES ('LIRF','x','LI',0,0,0);
INSERT INTO Accs (Code,Name,CountryPrefix,IsMilitary,IsForeign,IsHidden) VALUES ('lirf','y','LI',0,0,0);
SELECT Code FROM Accs WHERE Code = 'lirf';   -- deve tornare SOLO 'lirf'
DELETE FROM Accs;
```

Il secondo è l'unico che conta: il primo si può soddisfare con uno schema che poi si comporta male. Su questi
provider la regola è **verificare sul database, non sul modello** — la collation risultava presente e corretta
nei metadati EF anche quando nella DDL non c'era affatto (vedi `MySqlCollation`).

## 6. Fermare e ripartire da zero

```powershell
& "<bin>\mariadb-admin.exe" -u root -h 127.0.0.1 -P 3399 shutdown
```

Per rifare la prova pulita basta `DROP DATABASE itivao_atc; CREATE DATABASE itivao_atc;` — non serve
reinizializzare il datadir.

---

## Cosa questo ambiente NON dice

- **`sql_mode`.** Qui è `STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION`,
  quindi un valore troppo lungo **lancia**. Su un hosting condiviso lo strict mode è spesso spento, e lì la
  stessa scrittura **tronca in silenzio** e un CAST non numerico dà **warning e 0** invece di fallire. È la
  domanda A9 al loro indirizzo.
- **`lower_case_table_names`.** Su Windows è **1** (le tabelle esistono come `accs`), sul loro Linux sarà
  **0** (`Accs`, e `accs` non esiste). EF genera sempre i nomi con le maiuscole giuste, quindi non ci
  riguarda finché non si scrive SQL a mano — e `Vipi.DbSeed` (A2) ne scriverà, per le `TRUNCATE`.
- **Il default del database.** Qui è ricostruito per somiglianza col pacchetto Debian; la loro
  `DEFAULT_COLLATION_NAME` vera non l'abbiamo mai letta. Non cambia le colonne, che dichiarano la propria.
- **Le prestazioni.** Datadir su disco locale senza tuning, un client solo. Non è un banco di prova di carico.
