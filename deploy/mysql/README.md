# MySQL locale per provare il ramo di produzione

Serve a tre cose, tutte quelle che non si possono verificare senza un database vero: applicare davvero le
migrazioni, travasare i dati da Neon, e guidare l'app con la skill `verifica-live`. Il database di
produzione (`atc.it.ivao.aero`) ascolta su `localhost:3306` del loro server, quindi **da qui non è
raggiungibile**: tutto ciò che si prova, si prova qui.

Nessuna installazione di sistema: niente servizio Windows, niente registro, nessun privilegio di
amministratore. Si cancella cancellando la cartella.

---

## 1. Procurarsi i binari

Oracle **blocca i download non-browser**: `curl`, `Invoke-WebRequest` e le HEAD su `dev.mysql.com`,
`cdn.mysql.com` e `downloads.mysql.com/archives` rispondono `403` o `404` anche con uno user-agent
finto. La strada che funziona senza aprire un browser è `winget`, che sa risolvere il link vero — e con
`download` **scarica soltanto**, senza installare:

```powershell
winget download --id Oracle.MySQL --download-directory <cartella> `
  --accept-source-agreements --accept-package-agreements --disable-interactivity
```

Scarica `MySQL_8.4.9_Machine_X64_wix_en-US.msi` (~130 MB). È l'MSI, non lo ZIP, ma non serve installarlo:
un'**installazione amministrativa** estrae i file e basta.

```powershell
msiexec /a "<cartella>\MySQL_8.4.9_Machine_X64_wix_en-US.msi" /qn TARGETDIR="<destinazione>"
```

I binari finiscono in `<destinazione>\PFiles64\MySQL\MySQL Server 8.4\bin`.

### Quale versione

**8.4.x**, che è quella che offre `winget`. Ivao.It ha risposto «8.0+» senza precisare, e provare sulla
versione **più vecchia plausibile** sarebbe più prudente — ciò che funziona su 8.0 funziona anche dopo,
non viceversa — ma gli archivi 8.0 non sono scaricabili da riga di comando. 8.4 è la LTS corrente e
condivide con 8.0 tutto ciò che ci riguarda, `utf8mb4_0900_as_cs` compresa.

⚠️ Quando arriverà la loro versione esatta (`SELECT VERSION();`, §1.1 del piano), va usata quella.

## 2. Inizializzare e avviare

```ini
# my.ini
[mysqld]
basedir=<destinazione>/PFiles64/MySQL/MySQL Server 8.4
datadir=<scratchpad>/mysqldata
port=3399
mysqlx=0
bind-address=127.0.0.1
log-error=<scratchpad>/mysql-error.log
```

⚠️ **Percorsi con `/`, non con `\`.** In un file di opzioni MySQL il backslash è un carattere di escape e
`\s` significa **spazio**: un path come `...\scratchpad\...` diventa `... cratchpad ...` e l'avvio muore
con «Can't create directory» su un percorso che a occhio sembra giusto.

La porta è **3399** e non 3306 per non litigare con nient'altro sulla macchina.

```powershell
& "<bin>\mysqld.exe" --defaults-file="<scratchpad>\my.ini" --initialize-insecure   # root senza password
Start-Process "<bin>\mysqld.exe" -ArgumentList "--defaults-file=`"<scratchpad>\my.ini`"" -WindowStyle Hidden
```

## 3. Ricreare le condizioni loro, non condizioni comode

Questo è il punto che rende la prova utile. Il database va creato **senza specificare la collation**, così
eredita quella di default del server (`utf8mb4_0900_ai_ci`, case- e accent-**insensitive**) esattamente come
`itivao_atc` da loro; e l'utente va creato **con permessi solo su quel database**, così se mancasse un
privilegio lo si scopre adesso e non al cutover.

```sql
CREATE DATABASE itivao_atc;                                  -- niente COLLATE: come il loro
CREATE USER 'itivao_atc'@'%' IDENTIFIED BY '<password>';
GRANT ALL PRIVILEGES ON itivao_atc.* TO 'itivao_atc'@'%';
FLUSH PRIVILEGES;
```

## 4. Applicare lo schema

```sh
dotnet ef database update \
  --project src/Vipi.Infrastructure.MySqlMigrations \
  --startup-project src/Vipi.Infrastructure.MySqlMigrations \
  --framework net10.0 \
  --connection "Server=127.0.0.1;Port=3399;Database=itivao_atc;User Id=itivao_atc;Password=<password>"
```

⚠️ **Se fallisce a metà, il database resta sporco.** La DDL di MySQL non è transazionale: un `CREATE TABLE`
già eseguito non torna indietro, e il tentativo successivo muore con «Table 'accs' already exists» — che
sembra un altro problema e invece è il residuo del primo. Prima di riprovare: `DROP DATABASE itivao_atc;`
e si rifà il punto 3. È lo stesso motivo per cui su MySQL non usiamo il pattern `EnsureCreated`+reconciler
che usiamo su Neon (ADR-0007 §D4-bis).

## 5. Verificare che sia andata davvero

Tre controlli, in ordine di quanto sono convincenti:

```sql
-- 1. il database ha cambiato collation (l'ALTER DATABASE della migrazione iniziale ha funzionato)
SELECT DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='itivao_atc';
-- atteso: utf8mb4_0900_as_cs

-- 2. le colonne stringa l'hanno ereditata
SELECT COLLATION_NAME, COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA='itivao_atc' AND COLLATION_NAME IS NOT NULL GROUP BY COLLATION_NAME;
-- atteso: 163 as_cs, più 2 ai_ci che sono __EFMigrationsHistory — creata da EF PRIMA della nostra
-- riga, quindi resta col default vecchio. Innocuo: quei valori li genera e li confronta EF.

-- 3. la prova vera: due valori che differiscono solo per le maiuscole convivono in un indice unico
INSERT INTO Accs (Code,Name,CountryPrefix,IsMilitary,IsForeign,IsHidden) VALUES ('LIRF','x','LI',0,0,0);
INSERT INTO Accs (Code,Name,CountryPrefix,IsMilitary,IsForeign,IsHidden) VALUES ('lirf','y','LI',0,0,0);
SELECT Code FROM Accs WHERE Code = 'lirf';   -- deve tornare SOLO 'lirf'
DELETE FROM Accs;
```

Il terzo è l'unico che conta: i primi due si possono soddisfare con uno schema che poi si comporta male.
Su questo provider la regola è **verificare sul database, non sul modello** — la collation risultava
presente e corretta nei metadati EF anche quando nella DDL non c'era affatto (vedi `MySqlCollation`).

---

## Cosa questo ambiente NON dice

- **`sql_mode`.** Qui è `STRICT_TRANS_TABLES`, quindi un valore troppo lungo per la colonna **lancia**. Su
  un hosting condiviso lo strict mode è spesso disattivato, e lì la stessa scrittura **tronca in silenzio**.
  Da confermare con loro.
- **La versione.** 8.4.9 contro un «8.0+» non meglio precisato (§1 del piano).
- **Le prestazioni.** Datadir su disco locale senza tuning, un client solo. Non è un banco di prova di carico.
