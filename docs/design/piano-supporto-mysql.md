# Piano — supporto MySQL per il sito definitivo `atc.it.ivao.aero` 🟣

**Stato:** design approvato, **esecuzione avviata il 5 agosto 2026** · **Aggiornato:** 5 agosto 2026
**Branch:** `feat/persistenza-mysql` (da `main`)
**Gate d'ingresso:** ~~versione del server MySQL~~ → **sbloccato: MySQL 8.0+** (§1.1). Tutte le slice sono
eseguibili.

> ## Aggiornamento del 5 agosto 2026 — tre decisioni che riscrivono il piano
>
> **1. Il bersaglio non è più l'embedding: è il nostro host standalone.** `Vipi.Host` (net10) gira sul
> loro server dietro `atc.it.ivao.aero`, non la RCL montata dentro `Ivao.It.Website`. Conseguenza diretta:
> **MySQL deve funzionare su net10**, cioè esattamente ciò che il §2 di questo piano dava per escluso.
> L'embedding non è cancellato, è rimandato — il multi-target `net8.0;net10.0` delle cinque librerie resta.
>
> **2. Il provider è Oracle, non Pomelo** (§2 riscritto). Pomelo non ha e non avrà una build EF Core 10;
> con Pomelo il ramo net10 semplicemente non esiste, e `tools/Vipi.DbSeed` — che è net10 — non potrebbe
> nemmeno caricare i dati.
>
> **3. Il loro MySQL è su `localhost:3306`, quindi da qui non ci si scrive.** Il travaso non può essere una
> connessione diretta: vedi la catena in §S8. Come raggiungerlo (SSH? phpMyAdmin? IP autorizzato?) è la
> domanda aperta che ha preso il posto di quella sulla versione.
>
> Coordinate ricevute: database `itivao_atc`, utente `itivao_atc`, host `localhost:3306`, dominio
> `atc.it.ivao.aero`. Il §1.2 (database dedicato con utente proprietario) è quindi **soddisfatto**.

Documenti collegati: [ADR-0007](../adr/adr-0007-produzione-persistenza-e-scala.md) (persistenza e scala),
[guide/integrazione-ivao-it-da-fare.md](../guide/integrazione-ivao-it-da-fare.md) §1.3 e §4.1 (lavoro
aperto sull'integrazione), [FEATURE-PROCESS.md](../FEATURE-PROCESS.md) (gate di processo, §7 qui sotto).

---

## 0. Perché questo documento esiste

Il sito `Ivao.It.Website` gira su **net8, Blazor Server, MySQL**. Alla domanda «potete affiancare un
PostgreSQL, o in alternativa darci un disco persistente per SQLite?» la risposta è stata: **solo MySQL**,
con il suggerimento di usare il connector Pomelo.

Il modulo oggi supporta **SQLite** (default, path più testato) e **PostgreSQL** (deploy Render+Neon).
MySQL non è un flag da accendere: è un progetto. Questo documento lo dimensiona, lo spezza in slice e
distingue ciò che si può fare **subito** da ciò che dipende dalla loro risposta.

### 0.1 Stato del provider — verificato il 1 agosto, **ri-verificato il 5 agosto 2026**

Il suggerimento di Pomelo era corretto per il contesto in cui *a loro* serviva — il sito net8. Non è più
il nostro contesto: dal 5 agosto il bersaglio è `Vipi.Host` su net10, e su net10 Pomelo non esiste.
La tabella qui sotto resta perché è la prova di perché non lo si può aspettare.

| Fatto | Valore verificato |
|---|---|
| Ultima versione di `Pomelo.EntityFrameworkCore.MySql` su NuGet | **9.0.0** (EF Core 9), pubblicata 17-ago-2025 |
| Build per EF Core 10 | **non esiste**, nemmeno preview o rc |
| Ultimo commit su `main` del repo Pomelo | **17-ago-2025** (`Update branding to 9.0.1`) |
| Issue «EF Core 10 support» (#2007) | **aperta**, ultimo movimento 07-lug-2026 |
| PR #2019 «Upgrade to EF Core 10.0.0» | **aperta** dal 15-nov-2025, mai mergiata |
| PR #2031, #2032, #2042 (porting EF Core 10) | **chiuse senza merge** (#2032 chiusa il 29-mag-2026, 758 file) |
| Versione per EF Core 8 | **8.0.3**, pubblicata 02-mar-2025 — stabile, ma è il TFM che non ci serve più |
| Alternativa Oracle `MySql.EntityFrameworkCore` | **10.0.9** — ri-verificata il 5-ago-2026 **nel nuspec**, non solo sull'elenco versioni: tre gruppi di dipendenze, `net8.0`→EF 8.0.28, `net9.0`→EF 9.0.17, `net10.0`→**EF 10.0.9**, tutti su `MySql.Data` 26.7.0 |

Come ri-verificare questi numeri senza fidarsi di questo documento:

```sh
curl -s https://api.nuget.org/v3-flatcontainer/pomelo.entityframeworkcore.mysql/index.json | tail -5
gh api repos/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/pulls/2019 --jq '{state,merged}'
gh api repos/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/commits --jq '.[0].commit.author.date'
```

~~**Conseguenza architetturale:** MySQL sarà supportato solo sul TFM `net8.0`.~~ **Superata il 5 agosto
2026.** Quella conseguenza discendeva dalla premessa «MySQL serve solo all'embedding», che non vale più:
il sito definitivo è `Vipi.Host` su net10. Con Pomelo non esisterebbe alcun ramo MySQL da eseguire —
non un ramo limitato, proprio nessuno.

Resta valido, e va tenuto a verbale, il **motivo** per cui non si aspetta Pomelo: il repo è fermo da
quasi un anno e il porting a EF Core 10 è stato tentato quattro volte senza approdare (#2007 e #2019
aperte, #2031/#2032/#2042 chiuse senza merge). Non è una finestra da riaprire fra sei mesi.

I tre provider convivono così: **SQLite** in dev, **Postgres** su Render+Neon (che resta in piedi come
ambiente di prova, vedi §S8), **MySQL** in produzione su `atc.it.ivao.aero`.

---

## 1. Domande a Ivao.It — stato delle risposte

### 1.1 Versione del server MySQL — ✅ **RISPOSTO: 8.0+**

Era il gate. Con MySQL 8.0+ si prende la strada pulita: collation **`utf8mb4_0900_as_cs`** a livello di
database, ereditata da tutte le colonne, una riga in `OnModelCreating` applicata solo quando il provider è
MySQL. Nessun audit colonna per colonna, nessuna revisione degli `OrderBy`.

Le due strade non prese, per memoria di chi rileggerà: su **5.7** si sarebbe ripiegato su `utf8mb4_bin`
(*binary*, quindi cambia anche l'ordinamento e non solo la sensibilità alle maiuscole → revisione di ogni
`OrderBy` su stringa e di ogni confronto case-insensitive **voluto**, +1 slice); **MariaDB** sarebbe stato
un caso a sé, con matrice delle collation diversa e supporto meno battuto nel provider Oracle.

⚠️ La risposta «8.0+» va **verificata contro il server vero** (`SELECT VERSION()`) prima del cutover, e la
stessa versione esatta va usata per il container di CI (§S9) e per quello del travaso (§S8). «8.0+» non è
un numero: `8.0.x` e `8.4` differiscono su default che ci toccano.

### 1.2 Database dedicato con utente proprietario — ✅ **SODDISFATTO**

Database `itivao_atc`, utente `itivao_atc`, host `localhost:3306`. È un database separato da quello del
sito, che è ciò che serviva: il modulo ha il proprio `DbContext` e la propria connection string
(`ConnectionStrings:Vipi`), e mescolare i due schemi renderebbe impossibile capire chi possiede cosa al
primo problema.

**Da confermare** su quel database: che l'utente abbia i permessi **DDL** (`CREATE TABLE`, `ALTER TABLE`,
`CREATE INDEX`, `REFERENCES` per le FK). Un utente con le sole DML passa il primo test e fallisce al primo
avvio, quando le migration provano a creare lo schema.

### 1.2-bis Come ci si arriva — ⛔ **NUOVA DOMANDA APERTA, ora la più urgente**

`localhost:3306` significa che **dalla nostra macchina quel MySQL non è raggiungibile**. Prima di poter
travasare qualsiasi cosa serve sapere quale di questi vale:

| Strada | Cosa cambia per noi |
|---|---|
| **SSH / tunnel** sul server | Ottimale: `Vipi.DbSeed` gira contro il loro MySQL direttamente, il travaso è verificabile a colpo d'occhio e ripetibile. |
| **phpMyAdmin** o pannello web | Si importa il `.sql` prodotto dalla catena §S8. Attenzione al **limite di upload** del pannello: il dump porta anche i blob delle immagini (`MediaAsset`), quindi va previsto di spezzarlo. |
| **3306 aperto al nostro IP** | Come SSH ma senza tunnel. Raro sugli hosting condivisi. |
| **Nessuna delle tre** | Il `.sql` lo importano loro. Funziona, ma ogni iterazione costa un giro di mail: da evitare finché possibile. |

Finché non c'è risposta, il piano assume lo scenario peggiore (`.sql` da consegnare) — che è anche l'unico
che funziona in tutti e quattro i casi.

### 1.3 Libertà sulla collation di quel database

Se il loro hosting impone una collation case-insensitive a livello di server e non è sovrascrivibile per
database, il piano cambia in peggio: la sensibilità va forzata **colonna per colonna**, con audit di ogni
confronto stringa nel codice. Da sapere prima, non dopo.

### 1.4 Il DB del modulo entra nel loro backup

Il dump attuale copre il database del sito. Quello del modulo è un database diverso sullo stesso server:
va aggiunto esplicitamente. Vale già oggi per Postgres e SQLite (§3.3 del documento di integrazione), qui
è solo più facile darlo per scontato perché «è lo stesso MySQL».

### 1.5 Come gira il processo — ⛔ **NUOVE, nate col deploy standalone**

Queste non esistevano nella versione del 1 agosto perché l'embedding le risolveva tutte per costruzione:
era il *loro* processo a ospitarci. Con `Vipi.Host` standalone diventano nostre.

1. **Runtime .NET.** Il loro sito è net8; `Vipi.Host` è **net10**. O installano il runtime ASP.NET Core 10,
   o pubblichiamo **self-contained** (~100 MB, nessuna dipendenza dalla macchina). La seconda non richiede
   niente da loro ed è la strada da proporre come default.
2. **WebSocket sul reverse proxy.** Blazor Server **non funziona** senza: il circuito cade e la pagina
   resta muta. È la prima cosa che si rompe dietro un proxy configurato per un sito PHP.
3. **Supervisione del processo** (systemd, o quello che usano) e riavvio automatico.
4. **Percorso persistente** per il key-ring Data Protection — oppure lo mettiamo su MySQL (§S7). Da
   decidere, non da assumere.
5. **Redirect OIDC** `https://atc.it.ivao.aero/signin-oidc` e `/signout-callback-oidc` da registrare sul
   portale IVAO, altrimenti il login non torna indietro.

---

## 2. Decisione presa — provider Oracle `MySql.EntityFrameworkCore` 10.0.9

**Decisa il 5 agosto 2026.** La versione precedente di questa sezione raccomandava Pomelo; quella
raccomandazione poggiava sulla premessa «MySQL vive solo sull'embedding net8», caduta con la scelta del
deploy standalone.

**Il vincolo che decide non è più la copertura dei test: è che il ramo esista.** `Vipi.Host` è net10.
Pomelo non ha una build EF Core 10 e non ne avrà (§0.1). Con Pomelo non ci sarebbe un ramo MySQL poco
testato — non ci sarebbe proprio nessun ramo MySQL da eseguire in produzione.

| | ~~A — Pomelo 8.0.3~~ | **B — Oracle `MySql.EntityFrameworkCore` 10.0.9** ✅ |
|---|---|---|
| Gira su net10 (`Vipi.Host`) | **no** — non esiste la build | **sì**, EF 10.0.9 |
| Gira su net8 (embedding futuro) | sì | **sì**, EF 8.0.28 |
| Serve `#if NET8_0` attorno al ramo MySQL | sì | **no** |
| Copertura dalla suite attuale (net10) | **zero** | **piena, subito** |
| `tools/Vipi.DbSeed` (net10) può scrivere su MySQL | **no** → travaso impossibile senza riscrivere il tool | **sì** |
| Connector ADO | `MySqlConnector` | `MySql.Data` 26.7.0 |
| Maturità del provider EF | alta, standard de facto | storicamente più debole su query translation |
| Manutenzione upstream | **ferma da ago-2025** | attiva |

Le due righe che chiudono la questione sono la prima e la quinta. Le altre restano vere e sono il **costo**
della scelta, non un argomento contro: il provider Oracle è storicamente più debole in query translation,
ed è esattamente la superficie dove il §5 si aspetta i bug. Il che rende **§S9 (verifica live) non
negoziabile** — non è la rifinitura finale, è la slice che valida la decisione di questa sezione.

L'obiezione «un secondo connector ADO nello stesso processo» **decade**: valeva quando saremmo stati
ospiti del loro processo, che già carica `MySqlConnector`. In standalone il processo è nostro e
`MySql.Data` è l'unico connector presente.

Il rovescio da mettere a verbale: se un giorno l'embedding in `Ivao.It.Website` si farà davvero, il modulo
porterà `MySql.Data` dentro un processo che usa `MySqlConnector`. Due connector coesistono senza
conflitti — sono due librerie indipendenti — ma è una cosa che va detta a loro prima, non scoperta da loro
dopo.

---

## 3. Lavoro indipendente dal provider — **da fare per primo**

Queste due slice migliorano il modello su **tutti e tre** i provider e non vanno buttate se MySQL saltasse.
Vanno per prime non più perché sono le uniche sbloccate — ora lo sono tutte — ma perché senza di esse il
`CREATE TABLE` di MySQL fallisce e basta: sono il presupposto di ogni altra slice.

### S1 — `HasMaxLength` su tutte le colonne stringa indicizzate

**Problema.** Oggi `HasMaxLength` compare **6 volte in tutto il modello** (`VipiDbContext.cs`). Ogni altra
stringa è senza lunghezza: su SQLite e Postgres diventa `text`, indicizzabile senza limiti. Su MySQL
diventa **`longtext`**, che InnoDB **non indicizza** senza prefix length. Il limite è 3072 byte per indice,
e `utf8mb4` costa 4 byte per carattere → **768 caratteri** per colonna indicizzata.

**Colonne coinvolte** (indici dichiarati in `VipiDbContext.OnModelCreating`, tipo verificato in
`Vipi.Domain/Entities/`):

| Entità | Colonna | Indice | Lunghezza proposta |
|---|---|---|---|
| `Acc` | `Code` | unico + chiave alternata di 2 FK | 16 |
| `Airport` | `Icao` | unico + chiave alternata di 1 FK | 8 |
| `Airport` | `ParentCallsign` | non unico | 32 |
| `Sector` | `Callsign` | unico | 32 |
| `AccSector` | `ComposePosition` | **unico** | 32 |
| `AccSector` | `CenterId`, `ParentCallsign` | non unici | 16 / 32 |
| `AirportSector` | `ComposePosition` | **unico** | 32 |
| `AirportSector` | `AirportIcao`, `AccCode`, `ParentCallsign` | non unici | 8 / 16 / 32 |
| `SharedBlock` | `Key` | **unico** | 100 |
| `SidFixAlias` | `Prefix` | **unico** | 16 |
| `EditResourceLock` | `ResourceKey` | **unico** | 100 |
| `MediaAsset` | `Sha256` | unico (già 64) | — già fatto |
| `SpecialArea` | `IvaoId` | **unico** | 64 |
| `SpecialArea` | `CenterId` | non unico | 16 |
| `ImportState` | `Category` | **chiave primaria** | 32 |
| `DocRelease` | `TargetType`, `TargetKey` | indici compositi | 32 / 64 |
| `NavReference` | `Type`, `Ident`, `AiracCycle` | indice composito | 16 / 16 / 16 |
| `CoordinationPoint` | `Ident` | non unico | 16 |
| `EditorTask` | `Status` | non unico (enum→stringa) | 32 |

⚠️ **Gli enum sono salvati come stringa** (`SetProviderClrType(typeof(string))` in `OnModelCreating`):
anche `Document.Type`/`Status`, `DocRelease.TargetType`, `EditorTask.Status` sono colonne stringa
indicizzate e vanno dimensionate. È facile dimenticarsene guardando il tipo CLR.

Le lunghezze sopra sono **proposte**, da confermare contro i dati reali in `vipi.db` con un `MAX(LENGTH(...))`
prima di applicarle: una lunghezza troppo stretta tronca in silenzio su MySQL non-strict e lancia in strict.

**Attenzione FK:** MySQL rifiuta una foreign key fra colonne con lunghezza, charset o collation diversi.
Le FK su chiave alternata (`AccSector.CenterId` → `Acc.Code`, `AirportSector.AirportIcao` → `Airport.Icao`,
`AirportSector.AccCode` → `Acc.Code`, `SpecialArea.CenterId` → `Acc.Code`) devono avere **la stessa
lunghezza esatta** della colonna principale. È il primo errore che salta fuori al `CREATE TABLE`.

**Effetto sugli altri provider:** su SQLite nessuno (ignora le lunghezze); su Postgres `text` diventa
`varchar(n)`. **Questo è un cambio di tipo colonna**, che il `PostgresSchemaReconciler` **non applica** —
copre solo le aggiunte. Su Neon andrà eseguito a mano un `ALTER TABLE ... ALTER COLUMN ... TYPE varchar(n)`,
oppure si lascia deliberatamente Postgres su `text` mappando le lunghezze solo per MySQL. **Decisione da
prendere dentro la slice**, non dopo: è esattamente il caso «rename/cambio tipo» che ADR-0007 §D1-bis
segnala come non automatizzabile. La via più sicura è la seconda (lunghezze applicate solo quando il
provider è MySQL), che lascia Neon intatto.

### S2 — Test guardia sul modello

Un test in `Vipi.Infrastructure.Tests` che cammina il modello EF e asserisce: **ogni proprietà `string`
che partecipa a un indice o a una chiave ha `MaxLength` valorizzato e ≤ 768**. Gira su net10, contro il
modello, senza bisogno di un MySQL vivo — stesso pattern di `CreateTableStatements` del reconciler, che è
puro e testato senza Postgres.

Serve perché il vero rischio non è oggi: è la colonna indicizzata che qualcuno aggiungerà fra tre mesi
senza sapere che esiste un provider a cui serve la lunghezza. Il test la ferma in CI su net10, cioè dove
il ramo MySQL **non** viene compilato.

---

## 4. Lavoro provider-bound — parte dopo la risposta del §1.1

### S3 — Provider, resolver, DI

- `PersistenceProvider.MySql` in `src/Vipi.Infrastructure/Persistence/PersistenceProvider.cs`, con il
  `Resolve` che lo accetta (già case-insensitive, già con eccezione parlante sui valori validi) — e il
  commento XML del tipo, che oggi dice «MySQL non c'è», aggiornato **nello stesso commit** (§7 punto 4).
- Ramo `case MySql:` in `DependencyInjection.AddVipiInfrastructure`, con `UseQuerySplittingBehavior(SplitQuery)`
  come gli altri due ed `EnableRetryOnFailure`.
- **Versione del server fissata da config, non `AutoDetect`.** L'auto-detect apre una connessione al
  momento di costruire le opzioni: se il DB non è ancora su, l'app non parte per un motivo che non
  somiglia a quello vero. Con la versione in configurazione l'avvio è deterministico e il fallimento
  arriva alla prima query, dove si legge.
- **Niente `#if NET8_0`:** con il provider Oracle il ramo è unico e compila su entrambi i TFM. Era una
  complicazione della scelta Pomelo, sparita con essa.

### S4 — Collation ✅ *eseguita, ma non come previsto qui*

> **Aggiornamento 5 agosto 2026.** La ricetta scritta sotto — una riga di `UseCollation` sul modello — **non
> funziona**, per due motivi indipendenti scoperti generando la DDL. Vale la pena leggerli perché la stessa
> trappola si ripresenterà su ogni facet che affidiamo a questo provider.
>
> 1. **Il database esiste già.** `itivao_atc` l'hanno creato loro: la `CREATE DATABASE` in cui `UseCollation`
>    finirebbe non la eseguiamo mai.
> 2. **Il provider scarta la collation quando genera SQL.** `MySql.EntityFrameworkCore` 10.0.9 la porta fino
>    alle *operazioni* di migrazione — il file `.cs` generato contiene `collation: "utf8mb4_0900_as_cs"` su
>    163 colonne — ma nel `CREATE TABLE` non compare. Scarta anche l'annotazione `MySQL:Charset` della
>    `AlterDatabase()` che genera da sé, la quale infatti non produce **alcuno** statement.
>
> Anche il ripiego «metti la collation dentro il tipo di colonna», che è l'unica cosa emessa alla lettera,
> funziona solo a metà: passa per le colonne senza lunghezza (`longtext COLLATE …`) ma non per quelle con
> una, perché il provider ricostruisce il tipo come `varchar(n)` dalla dimensione e butta il resto — cioè
> fallisce **esattamente sulle colonne indicizzate**, le uniche che contano qui.
>
> **Quello che funziona:** `ALTER DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs` eseguito prima
> di ogni `CREATE TABLE`, aggiunto a mano in cima alla migrazione iniziale. Da lì in poi tabelle e colonne
> ereditano, comprese quelle delle migrazioni future. Il nome del database è omesso di proposito: MySQL
> applica l'istruzione a quello corrente, quindi la migrazione non ha `itivao_atc` cablato e gira identica
> sul container di prova.
>
> **Prezzo:** dipende da un permesso — l'utente deve poter fare `ALTER` sul proprio database (§1.2). Se non
> ce l'avesse, la migrazione iniziale fallisce a voce alta al primo avvio, che è il modo giusto di
> scoprirlo. Il ripiego sarebbe un `ALTER TABLE … CONVERT TO` per tabella.
>
> **La lezione, più importante del dettaglio:** niente di tutto questo si vedeva dal modello, dove la
> collation risultava presente e corretta. Un test sui metadati EF passava. **Su questo provider si verifica
> leggendo la DDL generata**, e in ultima istanza interrogando un MySQL vivo.

La ricetta originale, ora superata: `b.UseCollation("utf8mb4_0900_as_cs")` in `OnModelCreating`, applicata
**solo** quando il provider è MySQL. Tutto eredita, niente audit colonna per colonna.

**Perché è il punto più pericoloso del piano.** Il default `utf8mb4_0900_ai_ci` è case- **e**
accent-insensitive. Il modulo ha ~10 indici unici su stringa e confronti su callsign e hash. Con la
collation sbagliata:

- `lirf` e `LIRF` collidono → violazione di unique su dati oggi legali, in fase di import;
- `MediaAsset.Sha256` è **content-addressed**, lo sha *è* l'identità: due hash che differiscono solo per
  maiuscole verrebbero fusi;
- i lookup matchano case-insensitive **in silenzio** — nessuna eccezione, solo comportamento diverso.

Va verificato con un test di integrazione su MySQL reale, non per ispezione.

### S5 — Creazione dello schema

Le **65 migration** del repo sono SQLite-flavored e non girano su MySQL. Due strade:

**(a) Set di migration dedicato MySQL** — `Migrations/MySql/`, una `InitialCreate` generata dal modello
attuale più le successive. **Raccomandata.** Costa di più a regime (ogni cambio di schema va emesso due
volte) ma copre rename, drop e cambi di tipo.

**(b) `EnsureCreated` + un `MySqlSchemaReconciler`** speculare a quello Postgres. Più economico subito,
ma eredita il limite noto: **copre solo le aggiunte di colonna**. È la ferita aperta di ADR-0007 punto (b),
e su Postgres ce la teniamo perché è il *nostro* deploy. Sul database di produzione di un partner no.

Nota tecnica che pesa sulla (b): **la DDL di MySQL non è transazionale** (commit implicito). Un reconcile
che fallisce a metà lascia lo schema in stato parziale senza rollback — su Postgres il reconciler può
almeno contare su una transazione. E `ADD COLUMN IF NOT EXISTS` **non esiste** in MySQL: ogni aggiunta
richiede un controllo preventivo su `information_schema`, che introduce una finestra di corsa fra istanze.

### S6 — `MigrateVipiDatabase` — **bug latente già presente**

In `src/Vipi.Hosting/VipiModuleExtensions.cs:201`, oggi:

```csharp
if (db.Database.ProviderName?.Contains("Npgsql", ...) == true)
    PostgresSchemaReconciler.InitializeSchema(db, log);
else
    db.Database.Migrate();     // ← con MySQL: tenta di applicare 65 migration SQLite-flavored
```

Il ramo `else` è scritto assumendo che «non Npgsql» significhi «SQLite». Con MySQL configurato tenterebbe
di applicare le migration SQLite. Il dispatch va reso esplicito sui tre provider, con un `default` che
lancia invece di indovinare. Nota di processo: è un `switch` per-tipo in un secondo punto dopo quello di
`AddVipiInfrastructure` — con un terzo, scatta la «regola del 2» del FEATURE-PROCESS e va estratto un
descrittore per provider.

### S7 — Data Protection

`src/Vipi.Host/VipiDataProtection.cs` monta il key-store su DB **solo se il provider è Postgres**
(`UseNpgsql` esplicito). Sotto MySQL si torna al file-store di default: su filesystem effimero significa
**token antiforgery invalidi e utenti sloggati a ogni redeploy**. Serve il ramo MySQL, oppure la conferma
scritta che l'host ha un percorso persistente per il key-ring.

⚠️ **Con il deploy standalone questa slice smette di essere condizionale e diventa obbligatoria.** Il file
sta in `Vipi.Host`, che ora *è* il processo di produzione: il key-ring è responsabilità **nostra**, non del
loro sito. Nella versione precedente di questo piano si poteva sperare che se ne occupasse l'host ospitante
— ipotesi decaduta.

Da rifare com'è già fatto per Postgres, **con la stessa trappola già pagata una volta**: `EnsureCreated()`
verifica il *database*, non la tabella, quindi su un DB esistente non crea nulla. La tabella
`DataProtectionKeys` va creata con `CREATE TABLE IF NOT EXISTS`.

### S8 — Travaso dei dati reali: Neon → MySQL

Il contenuto vero del sito è oggi su **Neon** (il deploy Render), non più sul `vipi.db` locale. È quello
che va portato su `atc.it.ivao.aero`. Il tool `tools/Vipi.DbSeed` fa già il 90% del lavoro — cammina il
modello EF per reflection, quindi è quasi provider-agnostico — ma è cablato **SQLite → Postgres**
(`net10.0`, eseguito con successo il 29-lug-2026, 4506 righe lette / 4514 inserite).

Con il provider Oracle il tool resta `net10.0` e parla MySQL senza multi-target. Serve:

- **sorgente parametrica**: aggiungere la lettura `UseNpgsql` accanto a `UseSqlite`;
- **destinazione MySQL**;
- sostituire i due punti Postgres-specifici: `TRUNCATE … RESTART IDENTITY CASCADE` (su MySQL:
  `SET FOREIGN_KEY_CHECKS=0` + `TRUNCATE` per tabella) e il `setval` sulle sequence (su MySQL:
  `ALTER TABLE … AUTO_INCREMENT = …`);
- **conservare** il trucco a due fasi per il ciclo `Document↔DocumentVersion`, che non dipende dal
  provider, e la normalizzazione `DateTimeKind.Utc`, che qui serve per un motivo diverso (MySQL
  `DATETIME` non porta timezone: se non si normalizza a monte, il fuso lo decide la macchina).

**La catena, dato che il loro 3306 è su `localhost` (§1.2-bis):**

```
Neon (Render)  →  Vipi.DbSeed  →  MySQL 8 in Docker, versione loro  →  mysqldump  →  .sql da importare
```

Il MySQL in Docker **non è un passaggio sprecato**: è lo stesso ambiente che serve alla CI (§S9bis) e alla
verifica live (§S10). Uno solo, tre usi. E produce un `.sql` canonico che funziona in tutti e quattro gli
scenari di accesso del §1.2-bis, incluso il peggiore.

Se invece arriva un tunnel SSH, il `mysqldump` salta e il tool scrive diretto: più veloce e ripetibile.
Il codice è lo stesso — cambia solo la connection string.

⚠️ **Il travaso va rifatto poco prima del cutover**, non una volta sola: fra la prova e il passaggio in
produzione il sito su Render continua a essere modificato. La prima esecuzione serve a validare la catena,
l'ultima a portare i dati veri.

### S9bis — CI

- Servizio **MySQL in docker** nel workflow (o Testcontainers), della **stessa versione** del loro server.
- ~~`Vipi.Infrastructure.Tests` multi-target con i test MySQL sotto `#if NET8_0`~~ — **non serve più.** Con
  il provider Oracle i test MySQL girano nella suite net10 esistente, senza multi-target e senza `#if`.
  È il risparmio più concreto della scelta del §2.
- Il job `build-net8` resta com'è: continua a garantire che le cinque librerie compilino su net8 per
  l'embedding futuro.

### S10 — Verifica live end-to-end

Con la skill `verifica-live`, puntata a un'istanza MySQL locale. È **l'unica slice non stimabile**: serve
a far emergere la classe di bug che i test non vedono. Precedente concreto: il porting Postgres ha
prodotto `EfAirportRepository.RebuildDocumentAsync` che ordinava con `(int)s.Type` su un enum salvato come
stringa → `CAST("Type" AS integer)` → `22P02` su Postgres, mentre SQLite tornava `0` in silenzio. Il crash
si vedeva **solo pubblicando un aeroporto**. Oggi il grep su `(int)` nei repository è pulito, ma la stessa
classe di errore su MySQL cambia sintomo di nuovo: fuori da `sql_mode` strict, un CAST non numerico
produce un **warning** e `0`, cioè torna a essere silenzioso come su SQLite.

Flussi obbligatori da guidare: import ACC/settori/aeroporti, import SID (lock per-ICAO), pubblicazione dei
tre tipi di documento, lock di editing (`EditResourceLock`, heartbeat 60s/TTL 3min), upload immagine
(`MediaAsset`, blob nel DB), ricerca globale, vista live.

---

## 5. Rischi noti e come si manifestano

| Rischio | Sintomo | Dove si vede |
|---|---|---|
| **Facet del modello che il provider non emette** | il modello è giusto, la DDL no — e i test sui metadati passano | già capitato con la collation (§S4). Verificare **sempre** sulla DDL generata |
| **Migrazioni MySQL indietro rispetto al modello** | colonna mancante a runtime in produzione | test di allineamento snapshot↔modello in `MySqlMigrationsTests` |
| Collation case-insensitive non corretta | violazione di unique in import su dati legali; hash fusi | S4, test integrazione |
| `longtext` indicizzato | `CREATE TABLE` fallisce | S1, subito al primo avvio |
| FK con lunghezze/charset diversi | `errno 150` alla creazione della FK | S1 |
| `(int)<enum-stringa>` residui | fuori strict mode: **nessun errore**, ordinamento sbagliato | S10, solo guidando l'app |
| Precisione `DATETIME` | lock che scadono male, ordinamento release instabile | S3 — verificare `datetime(6)` |
| `RowVersion` `byte[]` → `varbinary` | concorrenza ottimistica che non scatta | test dedicato |
| DDL non transazionale | schema parziale dopo un reconcile fallito | S5, scegliendo (a) si evita |
| Query translation del provider Oracle | query che su SQLite/Postgres traducono e su MySQL no, o traducono male | S10 — è il costo accettato nel §2, e la ragione per cui la verifica live non è negoziabile |

---

## 6. Cosa questo piano deliberatamente NON fa

- ~~**Non porta MySQL su net10.**~~ **Ribaltato il 5 agosto 2026:** net10 è precisamente il TFM in cui
  MySQL deve funzionare, perché `Vipi.Host` è il sito definitivo. Il provider esiste (Oracle 10.0.9, §2).
- **Non mette le tabelle del modulo dentro il database del sito.** DbContext e connection string restano
  separati: è la premessa di ADR-0002 e di tutta la portabilità del modulo. Il database `itivao_atc` che
  ci hanno dato è già separato, quindi il punto è soddisfatto per costruzione.
- **Non rimpiazza SQLite né Postgres.** SQLite resta il default in sviluppo; Postgres resta il deploy
  Render+Neon, che **non si spegne al cutover**: diventa l'ambiente di prova e la sorgente del travaso.
- **Non automatizza la correzione del drift di schema.** Vale su MySQL la stessa ragione di ADR-0007 §D1-bis.

---

## 7. Pre-flight FEATURE-PROCESS

**1. Modello — aggiungo un concetto o ne esiste già uno?** Esiste: `PersistenceProvider` è già un enum a
due valori con un resolver. Si **estende**, non si affianca. Nessun modello gemello.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?** Sì, ed è il punto da sorvegliare.
Il provider è già switchato in `AddVipiInfrastructure` e implicitamente in `MigrateVipiDatabase` (§S6),
più il controllo in `VipiDataProtection`. Con MySQL diventano **tre** punti per-provider: scatta la
«regola del 2» e va estratto un descrittore per provider (registrazione DbContext + strategia schema +
key-store), invece di aggiungere il terzo `case` a mano in ogni sito.

**3. Ingressi + verifica.** Nessun ingresso UI nuovo: è persistenza. La verifica è S10, guidando i flussi
elencati su un MySQL reale — decisa **prima** di scrivere il codice, come vuole il runbook.

**4. Propagazione — rimuove o rinomina qualcosa?** Sì, due cose: il commento «Oggi solo `Sqlite` è
operativo» in `PersistenceProvider.cs` è già falso oggi e va corretto; e le frasi «il modulo non supporta
MySQL» in `guide/integration.md`, `guide/integrazione-ivao-it-da-fare.md` §1.3/§4.1 e dentro
`ivao-it-wiring.patch` vanno aggiornate **nello stesso giro** in cui il supporto entra, non dopo.

---

## 8. Definition of Done

- [x] Versione MySQL confermata e scritta in questo documento (§1.1) — **8.0+**, da riconfermare con
      `SELECT VERSION()` sul server vero prima del cutover.
- [x] Provider scelto e motivato (§2) — **Oracle `MySql.EntityFrameworkCore` 10.0.9**. Da registrare in ADR-0007.
- [ ] `HasMaxLength` su tutte le colonne stringa indicizzate + test guardia sul modello (S1, S2).
- [ ] Ramo MySQL nel resolver, nella DI e nel dispatch dello schema — con `default` che **lancia**, non indovina.
- [ ] Collation verificata con un test di integrazione su MySQL reale, non per ispezione.
- [ ] Schema creato dal set di migration MySQL su un database vuoto, da zero, in un colpo.
- [ ] `dotnet test` verde su net10 con MySQL in CI (niente ramo net8 da testare: §S9bis).
- [ ] Travaso Neon → MySQL eseguito e **riconciliato per conteggio riga per tabella**, non «sembra pieno».
- [ ] Verifica live sui flussi del §S10, con traccia scritta.
- [ ] Key-ring Data Protection su MySQL, verificato **sopravvivendo a un riavvio** (S7).
- [ ] Documenti aggiornati: ADR-0007, `guide/integration.md`, `guide/integrazione-ivao-it-da-fare.md`,
      `ivao-it-wiring.patch`, `guide/config.md`, `HANDOFF.md`, memorie.
- [ ] Ribaltamento «MySQL solo su net8» → «MySQL è il provider di produzione, su net10» registrato in
      ADR-0007 con la data e il motivo, perché la versione precedente diceva il contrario.

---

## 9. Ordine di esecuzione e stima

| # | Slice | Dipende da | Stima |
|---|---|---|---|
| 1 | S1 + S2 — lunghezze e test guardia | niente | mezza sessione |
| 2 | S3 + S4 — provider, DI, collation | decisione §2 ✅ | mezza sessione |
| 3 | S5 + S6 — schema e dispatch | S3 | 1 sessione |
| 4 | S8 — travaso Neon → MySQL | S5 | 1 sessione |
| 5 | S7 + S9bis — Data Protection, CI | S3 | mezza sessione |
| 6 | S10 — verifica live | tutte | **1-2 sessioni, non stimabile con precisione** |
| 7 | Cutover — runtime, proxy, OIDC, import | S10 + §1.5 | mezza sessione + i loro tempi |

**Totale realistico: 5-6 sessioni di lavoro concentrato**, di cui la sesta è quella che decide se il piano
ha funzionato. Nessuna slice è più bloccata: la versione del server (§1.1) è arrivata e la decisione sul
provider (§2) è presa.

L'unica dipendenza esterna residua è il **§1.2-bis** (come raggiungere il loro MySQL), e blocca soltanto
l'ultimo miglio del travaso — non il codice, non i test, non la verifica live, che girano tutti contro il
container Docker.

**Fuori da questo piano ma sulla stessa strada critica**, perché il sito definitivo non può nascere senza:
il **token app IVAO che dà 400** (senza fix: niente live ATC né roster), e la decisione su cosa mandare in
produzione dai due branch non fusi (`feature/aree-speciali-hardening`, non verificato sull'app vera;
`feature/aurora-bridge`, il cui endpoint serve al tool desktop).

---

## Appendice — messaggio pronto per Ivao.It (aggiornato al 5 agosto 2026)

> Grazie, con database dedicato e utente abbiamo quello che serviva. Per arrivare in fondo ci mancano
> ancora alcune cose, divise fra database e macchina.
>
> **Sul database `itivao_atc`:**
>
> 1. **La versione esatta** del server (`SELECT VERSION();`). Ci basta sapere se è `8.0.x` o `8.4`: usiamo
>    la stessa identica versione nei nostri test, così quello che proviamo è quello che gira da voi.
> 2. Che l'utente `itivao_atc` abbia i permessi **DDL** su quel database (`CREATE TABLE`, `ALTER TABLE`,
>    `CREATE INDEX`, `REFERENCES`). L'applicazione crea e aggiorna il proprio schema all'avvio.
> 3. Che possiamo **impostare la collation** del database a `utf8mb4_0900_as_cs`. Serve perché MySQL di
>    default ignora maiuscole e accenti nei confronti, mentre noi abbiamo indici unici su callsign, ICAO e
>    sugli hash dei file caricati: con la collation di default due valori diversi verrebbero considerati
>    uguali.
> 4. **Come possiamo raggiungerlo** per caricare i dati iniziali (circa 4500 righe più le immagini):
>    un accesso SSH, phpMyAdmin, o l'apertura del 3306 al nostro IP? In alternativa vi consegniamo un file
>    `.sql` da importare — diteci solo qual è il limite di dimensione per l'upload.
> 5. Che il database entri nel vostro **piano di backup**: il dump attuale non lo comprende.
>
> **Sulla macchina che ospiterà `atc.it.ivao.aero`:**
>
> 6. L'applicazione è **.NET 10** mentre il vostro sito è .NET 8. Possiamo consegnarla **self-contained**
>    (porta con sé il runtime, non dovete installare nulla) — ci va bene, volevamo solo dirvelo prima.
> 7. Il reverse proxy deve lasciar passare i **WebSocket**: l'applicazione è Blazor Server e senza quelli
>    le pagine si aprono ma restano bloccate. È il punto che si dimentica più spesso.
> 8. Serve un modo per **tenere il processo attivo** e riavviarlo (systemd o quello che usate di solito).
> 9. Ci serve **una cartella scrivibile e persistente**, oppure ci teniamo tutto sul database — fateci
>    sapere quale preferite.
> 10. Sul portale IVAO vanno registrati i redirect `https://atc.it.ivao.aero/signin-oidc` e
>     `https://atc.it.ivao.aero/signout-callback-oidc`, altrimenti il login non torna al sito.
>
> Sul connector: avevate suggerito Pomelo, che è la scelta giusta per un sito .NET 8. Noi useremo quello
> Oracle perché è l'unico che funziona anche su .NET 10 — cambia solo la libreria dentro la nostra
> applicazione, per il vostro MySQL è identico.

