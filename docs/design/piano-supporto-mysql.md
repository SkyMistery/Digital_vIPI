# Piano — supporto MySQL per l'embedding in Ivao.It 🟣

**Stato:** design approvato, **esecuzione non avviata** · **Aggiornato:** 1 agosto 2026
**Branch previsto:** `feat/persistenza-mysql` (da `integrazione/ivao-it`)
**Gate d'ingresso:** ci serve la **versione del server MySQL** di Ivao.It (§1). Senza quella risposta le
slice del §4 non si possono nemmeno iniziare: decide la strategia di collation, che a sua volta decide
lo schema.

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

### 0.1 Stato del provider — verificato il 1 agosto 2026

Il suggerimento di Pomelo è corretto **per il contesto in cui gli serve**, ma con un limite preciso da
mettere a verbale, perché è la ragione per cui MySQL resterà confinato al TFM net8.

| Fatto | Valore verificato |
|---|---|
| Ultima versione di `Pomelo.EntityFrameworkCore.MySql` su NuGet | **9.0.0** (EF Core 9), pubblicata 17-ago-2025 |
| Build per EF Core 10 | **non esiste**, nemmeno preview o rc |
| Ultimo commit su `main` del repo Pomelo | **17-ago-2025** (`Update branding to 9.0.1`) |
| Issue «EF Core 10 support» (#2007) | **aperta**, ultimo movimento 07-lug-2026 |
| PR #2019 «Upgrade to EF Core 10.0.0» | **aperta** dal 15-nov-2025, mai mergiata |
| PR #2031, #2032, #2042 (porting EF Core 10) | **chiuse senza merge** (#2032 chiusa il 29-mag-2026, 758 file) |
| Versione per EF Core 8 | **8.0.3**, pubblicata 02-mar-2025 — stabile, è quella che serve a loro |
| Alternativa Oracle `MySql.EntityFrameworkCore` | **10.0.9** su NuGet; copre sia net8 sia net10 |

Come ri-verificare questi numeri senza fidarsi di questo documento:

```sh
curl -s https://api.nuget.org/v3-flatcontainer/pomelo.entityframeworkcore.mysql/index.json | tail -5
gh api repos/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/pulls/2019 --jq '{state,merged}'
gh api repos/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/commits --jq '.[0].commit.author.date'
```

**Conseguenza architetturale:** MySQL sarà supportato **solo sul TFM `net8.0`**, quello dell'embedding.
Il ramo `net10.0` — che è il nostro deploy autonomo su Render+Neon — resta Postgres + SQLite e non viene
toccato. Non è una limitazione temporanea da rimuovere «quando esce Pomelo 10»: il repo Pomelo è fermo da
quasi un anno e il porting EF10 è stato tentato quattro volte senza approdare. Va scritto come **limite
noto**, non scoperto fra sei mesi.

---

## 1. Domande bloccanti da porre a Ivao.It

La prima è l'unica che blocca davvero. Le altre tre vanno chieste nello stesso messaggio per non fare
tre giri di mail.

### 1.1 Versione del server MySQL — **bloccante**

| Risposta | Conseguenza sul piano |
|---|---|
| **MySQL 8.0+** | Collation `utf8mb4_0900_as_cs` a livello di database, ereditata da tutte le colonne. Strada pulita, una riga in `OnModelCreating`. |
| **MySQL 5.7** | `utf8mb4_0900_*` non esiste. Si ripiega su `utf8mb4_bin`, che è *binary*: cambia anche il confronto di ordinamento, non solo la sensibilità a maiuscole. Va rivisto ogni `OrderBy` su stringa e ogni confronto case-insensitive **voluto** (ricerca globale, lookup ICAO digitati dall'utente). **Costo: una slice in più.** |
| **MariaDB** (capita, e non è MySQL) | Da trattare come caso a sé: Pomelo la supporta, ma la matrice delle collation è diversa. Ri-aprire questa tabella prima di stimare. |

### 1.2 Database dedicato con utente proprietario

Serve un **database separato** (es. `vipi`) sullo stesso server, con un utente che abbia **DDL**
(`CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX`). Non tabelle dentro il database del sito: il modulo ha
il proprio `DbContext` e la propria connection string (`ConnectionStrings:Vipi`), e mescolare i due
schemi rende impossibile capire chi possiede cosa al primo problema.

### 1.3 Libertà sulla collation di quel database

Se il loro hosting impone una collation case-insensitive a livello di server e non è sovrascrivibile per
database, il piano cambia in peggio: la sensibilità va forzata **colonna per colonna**, con audit di ogni
confronto stringa nel codice. Da sapere prima, non dopo.

### 1.4 Il DB del modulo entra nel loro backup

Il dump attuale copre il database del sito. Quello del modulo è un database diverso sullo stesso server:
va aggiunto esplicitamente. Vale già oggi per Postgres e SQLite (§3.3 del documento di integrazione), qui
è solo più facile darlo per scontato perché «è lo stesso MySQL».

---

## 2. Decisione aperta — quale provider MySQL

Non è la decisione che sembra. Il punto non è la qualità dei due provider, è **chi copre il codice con i test**.

**Vincolo che decide:** i sei progetti di test sono `net10.0` **soli** (verificato nei `.csproj`). Le
cinque librerie sono `net8.0;net10.0`.

| | **A — Pomelo 8.0.3** | **B — Oracle `MySql.EntityFrameworkCore` 10.0.x** |
|---|---|---|
| TFM coperti | solo net8 | net8 **e** net10 |
| Serve `#if NET8_0` attorno al ramo MySQL | **sì** | no |
| Copertura dalla suite attuale | **zero** (la suite gira su net10) | piena, subito |
| Connector ADO | `MySqlConnector` — **lo stesso già in uso nel sito** | `MySql.Data` — secondo stack di connessione nello stesso processo |
| Maturità del provider EF | alta, è lo standard de facto | storicamente più debole su query translation |
| Manutenzione upstream | ferma da ago-2025 | attiva |

**Raccomandazione: A (Pomelo), a condizione di multi-targettare `Vipi.Infrastructure.Tests` a
`net8.0;net10.0`** e far girare i test MySQL solo sotto net8. Motivi: è il provider che loro già usano e
conoscono, evita un secondo connector ADO nello stesso processo, ed è quello con meno sorprese in query
translation — che è esattamente la superficie dove ci aspettiamo i bug (§5).

Senza il multi-target dei test, la scelta A produce un ramo di produzione **mai eseguito da nessun test**:
la stessa condizione in cui si trova oggi il percorso Npgsql di `ISchemaDriftProbe`, che infatti è ancora
non verificato. Non ripetiamola su un database di un partner.

Se il multi-target dei test si rivelasse troppo costoso (dipendenze di test non disponibili su net8), la
scelta di ripiego è **B**, accettando il secondo connector.

---

## 3. Lavoro indipendente dal provider — **si può fare subito**

Queste due slice migliorano il modello su **tutti e tre** i provider e non vanno buttate se MySQL saltasse.
Sono le uniche cose da fare prima di conoscere la versione del server.

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
  `Resolve` che lo accetta (già case-insensitive, già con eccezione parlante sui valori validi).
- Ramo `case MySql:` in `DependencyInjection.AddVipiInfrastructure`, con `UseQuerySplittingBehavior(SplitQuery)`
  come gli altri due, `EnableRetryOnFailure` e `ServerVersion.AutoDetect` (o versione fissata da config —
  meglio fissata: l'auto-detect apre una connessione all'avvio).
- Con la scelta A, tutto il ramo va sotto `#if NET8_0`, **e il `Resolve` deve fallire con un messaggio
  esplicito** su net10 («MySQL è supportato solo sull'embedding net8, vedi docs/design/piano-supporto-mysql.md»),
  non con un `default:` generico. Un errore muto qui costa un pomeriggio a chi lo incontra.

### S4 — Collation

Con MySQL 8.0+: `b.UseCollation("utf8mb4_0900_as_cs")` in `OnModelCreating`, applicata **solo** quando il
provider è MySQL. Tutto eredita, niente audit colonna per colonna.

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

⚠️ Questo file sta in `Vipi.Host` (il nostro host standalone, net10), non nel modulo. In embedded il
key-ring è responsabilità del **loro** host — che già lo gestisce per il resto del sito. Va **verificato**,
non assunto: se il sito usa il file-store di default e gira in container, il problema esiste già oggi per
loro ed è solo invisibile.

### S8 — `tools/Vipi.DbSeed` verso MySQL

Il tool oggi è SQLite → Postgres (`net10.0`, eseguito con successo il 29-lug-2026, 4506 righe). Serve il
target MySQL per travasare i contenuti reali (§3.2 del documento di integrazione). Attenzione: il tool è
`net10.0` e con la scelta A il provider MySQL vive solo su net8 → **il tool va multi-targettato o
riscritto per usare Oracle solo lì**. È un dettaglio che si scopre tardi se non è scritto qui.

### S9 — CI

- Servizio **MySQL in docker** nel workflow (o Testcontainers), della **stessa versione** del loro server.
- `Vipi.Infrastructure.Tests` multi-target `net8.0;net10.0`, con i test MySQL sotto `#if NET8_0`.
- Il job `build-net8` esiste già: va esteso a `dotnet test -f net8.0`.

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
| Collation case-insensitive non corretta | violazione di unique in import su dati legali; hash fusi | S4, test integrazione |
| `longtext` indicizzato | `CREATE TABLE` fallisce | S1, subito al primo avvio |
| FK con lunghezze/charset diversi | `errno 150` alla creazione della FK | S1 |
| `(int)<enum-stringa>` residui | fuori strict mode: **nessun errore**, ordinamento sbagliato | S10, solo guidando l'app |
| Precisione `DATETIME` | lock che scadono male, ordinamento release instabile | S3 — verificare `datetime(6)` |
| `RowVersion` `byte[]` → `varbinary` | concorrenza ottimistica che non scatta | test dedicato |
| DDL non transazionale | schema parziale dopo un reconcile fallito | S5, scegliendo (a) si evita |
| Ramo MySQL non coperto dai test | qualsiasi cosa, in produzione da loro | S9 — è la ragione della raccomandazione del §2 |

---

## 6. Cosa questo piano deliberatamente NON fa

- **Non porta MySQL su net10.** Il provider non esiste e il nostro deploy non ne ha bisogno.
- **Non mette le tabelle del modulo dentro il database del sito.** DbContext e connection string restano
  separati: è la premessa di ADR-0002 e di tutta la portabilità del modulo.
- **Non rimpiazza SQLite né Postgres.** Restano i provider di default e del deploy autonomo.
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

- [ ] Versione MySQL confermata e scritta in questo documento (§1.1).
- [ ] `HasMaxLength` su tutte le colonne stringa indicizzate + test guardia sul modello (S1, S2).
- [ ] Provider scelto e motivato in ADR-0007 (§2).
- [ ] Ramo MySQL nel resolver, nella DI e nel dispatch dello schema — con `default` che **lancia**, non indovina.
- [ ] Collation verificata con un test di integrazione su MySQL reale, non per ispezione.
- [ ] Schema creato dal set di migration MySQL su un database vuoto, da zero, in un colpo.
- [ ] `dotnet test` verde su net10 (baseline attuale) **e** su net8 con MySQL in CI.
- [ ] Verifica live sui flussi del §S10, con traccia scritta.
- [ ] Documenti aggiornati: ADR-0007, `guide/integration.md`, `guide/integrazione-ivao-it-da-fare.md`,
      `ivao-it-wiring.patch`, `guide/config.md`, memorie.
- [ ] Limite «MySQL solo su net8» scritto in ADR-0007 come limite noto, con la data della verifica su Pomelo.

---

## 9. Ordine di esecuzione e stima

| # | Slice | Dipende da | Stima |
|---|---|---|---|
| 1 | S1 + S2 — lunghezze e test guardia | niente — **fattibile subito** | mezza sessione |
| 2 | S3 — provider, resolver, DI | §1.1 + decisione §2 | mezza sessione |
| 3 | S4 — collation | §1.1 | mezza sessione |
| 4 | S5 + S6 — schema e dispatch | S3 | 1 sessione |
| 5 | S9 — CI con MySQL | S3 | mezza sessione |
| 6 | S7 + S8 — Data Protection, DbSeed | S3 | mezza sessione |
| 7 | S10 — verifica live | tutte | **1-2 sessioni, non stimabile con precisione** |

**Totale realistico: 4-5 sessioni di lavoro concentrato**, di cui l'ultima è quella che decide se il piano
ha funzionato. Su MySQL 5.7 aggiungere una sessione per la revisione degli ordinamenti (§1.1).

---

## Appendice — messaggio pronto per Ivao.It

> Per far girare il modulo vIPI sul vostro MySQL ci servono quattro conferme:
>
> 1. **Versione del server MySQL** (8.0+, 5.7, o MariaDB?). È quella che ci blocca: decide la strategia di
>    collation, e MySQL è case-insensitive di default sulle stringhe mentre il modulo ha indici unici su
>    callsign, ICAO e hash dei file.
> 2. Un **database dedicato** (es. `vipi`) sullo stesso server, con un utente proprietario che abbia i
>    permessi DDL (`CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX`). Il modulo ha DbContext e connection
>    string propri, separati dal database del sito.
> 3. Che possiamo **impostare la collation** di quel database (`utf8mb4_0900_as_cs` su MySQL 8).
> 4. Che quel database entri nel vostro **piano di backup**: il dump attuale non lo comprende.
>
> Sul connector: avete ragione, Pomelo è quello giusto — la 8.0.3 per EF Core 8 è stabile ed è quella che
> useremo, visto che il vostro sito gira su .NET 8.

