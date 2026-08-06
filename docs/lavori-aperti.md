# Lavori aperti — elenco unico

**Aggiornato:** 6 agosto 2026 · **Scopo:** una cosa alla volta, senza rileggere la cronologia.

Ogni voce è pensata per essere presa da sola in una sessione nuova. Dove serve contesto, il rimando è al
documento che ce l'ha per esteso. L'ordine dentro ogni sezione è quello in cui conviene affrontarle.

**Legenda del blocco:** 🟢 si può fare subito · 🟡 dipende da un'altra voce · 🔴 dipende da qualcun altro
(Ivao.It, il portale IVAO, l'owner).

---

## A. Cutover su `atc.it.ivao.aero` — la strada critica

Branch `feat/persistenza-mysql`. Contesto: [`design/piano-supporto-mysql.md`](design/piano-supporto-mysql.md),
decisioni in ADR-0007 §D4/§D4-bis (⚠️ entrambe **superate**, vedi A8).

Stato: il server è **MariaDB 11.4.10**, non MySQL. `Vipi.Host` è passato a **net8** e il provider è
**Pomelo**; suite verde su net8 (309) e net10 (300). **Dal 6 agosto 2026 il ramo è provato contro una
MariaDB 11.4.10 vera**: schema, collation, case-sensitivity, avvio dell'applicazione (A1), key-ring
Data Protection che sopravvive a un riavvio (A4) e **travaso dei dati veri da Neon fino al `.sql`
reimportabile** (A2/A3). Restano i flussi editoriali (A6) e la CI (A5).

### A1 ✅ MariaDB 11.4 in locale, e rifare le verifiche — eseguita il 6 agosto 2026
MariaDB **11.4.10** portable in `D:\Programmazione\IVAO_Test\_mariadb` (fuori dal repo), porta 3399,
default di server `utf8mb4_uca1400_ai_ci` come il pacchetto Debian loro, database `itivao_atc` creato
**senza** `COLLATE` e utente con permessi **solo** su quel database. Ricetta completa e trappole:
[`../deploy/mariadb/README.md`](../deploy/mariadb/README.md).

Le quattro verifiche, tutte passate:
1. **Migrazioni da zero su database vuoto** — sia da `dotnet ef database update`, sia **all'avvio
   dell'host** (`MigrateVipiDatabase`), che è il percorso vero del cutover. 38 tabelle, una sola riga in
   `__EFMigrationsHistory`.
2. **Collation** — **163** colonne stringa su 163 con `utf8mb4_uca1400_as_cs`, dichiarata sulla colonna
   nella DDL vera (`SHOW CREATE TABLE`). Le 2 eccezioni sono `__EFMigrationsHistory`, che EF crea prima
   della nostra migrazione: innocue, quei valori li genera e li confronta EF.
3. **`LIRF` e `lirf` convivono** nell'indice unico di `Accs.Code` e `WHERE Code='lirf'` ne torna uno solo.
   È la verifica che conta: il default del database è ai_ci, quindi senza la collation sulla colonna il
   secondo INSERT sarebbe stato un duplicate key.
4. **Avvio con `Persistence__Provider=MySql`** — `/vsop` 200, `/vsop/health` Healthy, `/vsop/health/ready`
   200, log senza un solo `warn`. E ha **scritto davvero**: import ACC (7 ACC, 36 settori) e aree speciali
   (230 create, 17 aggiornate) andati a buon fine su MariaDB, con `LastSuccessUtc` valorizzato e
   `LastError` vuoto in `ImportStates`.

Tre cose imparate, che valgono per il cutover:
- **Pomelo emette `ALTER DATABASE CHARACTER SET utf8mb4;`** come prima istruzione della migrazione. Passa
  con un `GRANT ALL ON itivao_atc.*` (è ALTER *sul database*), ma se il loro utente avesse una lista di
  privilegi ritagliata è la riga che si pianta per prima. → domanda per A9.
- **`lower_case_table_names`** è 1 su Windows e sarà 0 sul loro Linux: col default le tabelle si salvano
  come `accs`, là esisterebbero solo come `Accs`. Sembrava riguardare solo le `TRUNCATE` scritte a mano;
  in A3 si è scoperto che avvelena il **dump**, e il `my.ini` ora porta `lower-case-table-names=2`.
- `mariadb-install-db` su Windows crea **utenti anonimi** che dirottano le connessioni da 127.0.0.1: il
  sintomo è `Access denied ... (using password: YES)`, che accusa la password mentre il problema è l'utente.

Restano **non** verificati e non verificabili qui: `sql_mode` del loro server, e la loro
`DEFAULT_COLLATION_NAME` vera (mai letta). Vedi «Cosa questo ambiente NON dice» nel README.

ℹ️ Non è un guasto: l'import settori-aeroporto ha scritto **0 aeroporti** perché parte dal catalogo
aeroporti, che su un database appena creato è vuoto finché non si passa da `/vsop/admin/acc` → «Importa da
sorgente». Da riprendere in **A6**, che è dove i flussi si guidano.

ℹ️ Osservazione di modello, non urgente: `Accs.Code` porta **due** indici unici, `AK_Accs_Code` (chiave
alternativa, bersaglio della FK da `AccSector.AccCode`) e `IX_Accs_Code` (`HasIndex(...).IsUnique()`,
`VipiDbContext.cs:66`). Ridondanza vecchia, presente anche su SQLite: costa una scrittura d'indice in più,
non un difetto. Toglierla vorrebbe dire toccare entrambi i set di migrazioni.

### A2 ✅ `Vipi.DbSeed` a net8 — fatta il 6 agosto 2026
Tool su **net8** (come l'host, e per lo stesso motivo: Pomelo), sorgente **SQLite o Postgres**, destinazione
**Postgres o MariaDB**. I due punti Postgres-specifici sono sostituiti come da §S8: `TRUNCATE … RESTART
IDENTITY CASCADE` → `SET FOREIGN_KEY_CHECKS=0` + `TRUNCATE` per tabella (**riacceso subito dopo**, così gli
INSERT restano verificati), e `setval` → `ALTER TABLE … AUTO_INCREMENT = max+1` sulle sole colonne che un
contatore ce l'hanno davvero, chieste a `information_schema`. Conservati il trucco a due fasi per
`Document↔DocumentVersion` e la normalizzazione `DateTimeKind.Utc`, che su MariaDB serve per un motivo
diverso: `DATETIME` non porta fuso, quindi senza normalizzare a monte lo deciderebbe la macchina.

Tre aggiunte non chieste ma che il travaso vero (A3) userà:
- **Riconciliazione riga per riga** in fondo, con **uscita in errore** se una tabella non combacia. A3 chiede
  di riconciliare per conteggio: ora lo fa il tool, invece che l'occhio di chi guarda il log.
- **Verifica dello schema di destinazione prima del wipe**: su MariaDB si ferma elencando le tabelle mancanti
  e il comando da lanciare, invece di svuotare e poi fallire l'INSERT.
- **Riga di comando a flag** (`--from-postgres … --to-mysql …`): con due capi variabili e un TRUNCATE in
  mezzo, due connection string posizionali si invertono e l'errore si scopre a database svuotato.

**Eseguito**: `--from-sqlite src/Vipi.Host/vipi.db --to-mysql <MariaDB 11.4.10 locale>` → 4578 righe lette,
4588 scritte, 36 contatori risincronizzati, **37 tabelle su 37 combaciano**, e l'host avviato su quel
database serve le pagine con i dati veri (`/vsop` mostra LIRR/LIMM/LIBB).

⚠️ **Il percorso sorgente-Postgres non è ancora stato eseguito**: serve la connection string di Neon, cioè
A3. È l'unico pezzo del tool che nessuno ha visto girare.

ℹ️ `/vsop/health` su quel database dice **Degraded**. Non è il travaso né MariaDB: l'host avviato sullo
**stesso `vipi.db` via SQLite** dice Degraded uguale. Sono le incongruenze soft-ref già note (E2: gerarchia
`ParentCallsign` dangling), che viaggiano coi dati.

### A3 ✅ Travaso dei dati veri — catena eseguita il 6 agosto 2026
`Neon → DbSeed → MariaDB locale → mariadb-dump → .sql`, tutta. Da Neon: **4807 righe** lette, 4818 scritte,
**37 tabelle su 37 riconciliate** dal tool. Dump: `_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-06.sql`,
4,7 MB, sha256 `B4989D63…A475A296`. Procedura e opzioni: §6 di
[`../deploy/mariadb/README.md`](../deploy/mariadb/README.md).

**Verificato reimportandolo**, non guardandolo: database vuoto → import → 38 tabelle, 4808 righe, conteggi
**identici** all'origine; host avviato su quel database → `/vsop` 200 con LIRR/LIMM/LIBB a schermo e
**nessuna migrazione riapplicata** (`__EFMigrationsHistory` viaggia nel dump apposta).

⚠️ **Il primo dump era inutilizzabile e sembrava perfetto.** Windows nasce con
`lower_case_table_names=1`: i nomi di tabella si salvano in minuscolo e `mariadb-dump` li riemette così, per
cui sul loro Linux (`=0`) sarebbero nate `accs` mentre EF cerca `Accs`. Rifatto tutto con
`lower-case-table-names=2` nel `my.ini` — lo 0 su Windows corrompe gli indici — da mettere **prima** di
creare lo schema. È la trappola annotata in A1, materializzatasi al primo tentativo.

Il `.sql` **non è nel repository**: contiene contenuto reale, VID dello staff e audit log. Sta in
`_mariadb/dump/`, fuori dall'albero.

**Cosa resta, e non è lavoro tecnico:**
- **Consegnarlo**, per il canale che concorderanno (A9) — con 4,7 MB va verificato che phpMyAdmin regga.
- **Rifarlo poco prima del cutover**: fra oggi e il passaggio, Render continua a essere modificato. Stesso
  comando, due minuti.
- ⚠️ **Prima del travaso definitivo va deciso B4**: se entra `feature/aree-speciali-hardening` i dati delle
  aree cambiano, e allora questo dump si rifà **dopo** il merge, non prima.
- Escluso di proposito dal dump: `DataProtectionKeys`. Sono le chiavi che decifrano i cookie della nostra
  installazione locale, non un dato da consegnare; l'host se le ricrea al primo avvio.

### A4 ✅ Data Protection su MariaDB — fatta il 6 agosto 2026
`VipiDataProtection` montava il key-store su DB **solo se il provider era Postgres**; sotto MariaDB
ricadeva sul file-store, cioè antiforgery rotto e utenti sloggati a ogni riavvio su disco effimero. Ora la
decisione «questo provider tiene le chiavi nel database» sta in
`Vipi.Infrastructure/Persistence/DataProtectionSchema.cs` — funzione pura, un caso per provider — e l'host
fa solo il wiring. Su MariaDB il context usa Pomelo con la **versione fissata** (come `DependencyInjection`:
`AutoDetect` aprirebbe una connessione mentre si costruiscono le opzioni) e **senza** retry, che su Neon
serve per il risveglio del compute e qui non avrebbe motivo.

**Verificato sopravvivendo a un riavvio, non per ispezione**: primo avvio → tabella creata e chiave
`key-454f958a…` scritta in `DataProtectionKeys`; riavvio → **la stessa chiave, una sola riga**, nessuna
chiave nuova, `AUTO_INCREMENT` fermo. Se il key-ring fosse tornato sul file-store ne sarebbe nata una seconda.

+7 test per target (`DataProtectionSchemaTests`, Infra **316** su net8 e **307** su net10): coprono il set
di provider, l'idempotenza della DDL, il **nome della tabella con le maiuscole** — su Linux
`lower_case_table_names=0`, e una `dataprotectionkeys` minuscola non sarebbe quella che EF cerca — e la
collation, che qui va dichiarata a mano perché la tabella non è nel modello e `MySqlCollation.Apply` non la
raggiunge.

⚠️ Non verificato: che un **cookie** emesso prima del riavvio venga ancora decifrato dopo. La prova richiede
un login vero (`VipiAuth:Enabled=true`), quindi va con A6 o col primo login su `atc.it.ivao.aero`.

### A5 ✅ CI con MariaDB — fatta il 6 agosto 2026
Job nuovo **`mariadb-schema`**: servizio `mariadb:11.4.10` — la versione esatta loro, non il tag mobile
`11.4` — con database creato **senza `COLLATE`** e utente con permessi **solo** su quello, come da loro.
Applica le migrazioni e poi verifica **sul database**: nessuna colonna stringa fuori da
`utf8mb4_uca1400_as_cs` (attese esattamente 2, quelle di `__EFMigrationsHistory`), `LIRF` e `lirf` che
convivono nell'indice unico mentre il `WHERE` li distingue, e le tabelle nate con le maiuscole giuste.

**La terza verifica si può fare solo lì.** Su Windows `lower_case_table_names` vale 1 o 2 e la differenza
non è osservabile: la CI su Linux è l'unico posto dove il guasto che ha rovinato il primo dump di A3 può
essere colto prima di arrivare da loro.

**Due job erano rossi da prima, e non per MariaDB:**
- `docker-image` falliva da quando l'host è net8: il Dockerfile pubblicava un'applicazione net8 dentro
  `aspnet:10.0`. Build e publish riescono lo stesso — il container muore all'avvio con
  «Microsoft.NETCore.App version 8.0.0 not found». Immagine finale portata a **`aspnet:8.0`**; lo stage di
  build resta su `sdk:10.0`. ⚠️ **Vale anche per il deploy su Render**, che usa questo Dockerfile: senza
  questa correzione il primo deploy dopo il merge sarebbe morto all'avvio.
- I test del ramo net8 giravano **in roll-forward sul runtime 10**, perché in CI c'era il solo SDK 10:
  assembly giusta, runtime sbagliato, proprio sul ramo che va in produzione. Aggiunto il runtime 8.

Tre inciampi di CI, tutti nel job nuovo e tutti risolti: `dotnet ef` non si risolve dalla dispatch della
CLI su un runner senza manifest di tool (si invoca `~/.dotnet/tools/dotnet-ef` per percorso), e `dotnet ef`
compila ma **non restora** (senza `dotnet restore` esplicito muore con `NETSDK1004`, che parla di NuGet e
non di migrazioni).

Esito: **quattro job su quattro verdi**.

### A6 🟢 Verifica live sui flussi editoriali *(A1 fatta; skill `verifica-live`)*
È la slice che valida la scelta del provider, non una rifinitura. Flussi obbligatori: import
ACC/settori/aeroporti, import SID (lock per-ICAO), pubblicazione dei tre tipi di documento, lock di editing
(`EditResourceLock`, heartbeat 60s/TTL 3min), upload immagine (`MediaAsset`, blob nel DB), ricerca globale,
vista live.

⚠️ Fuori da `sql_mode` strict un CAST non numerico dà **warning e 0** invece di lanciare: la classe di bug
che su Postgres crashava (`(int)` su enum-stringa) qui torna silenziosa. Da confermare qual è l'`sql_mode`
del loro server.

### A7 🟡 Nuovo pacchetto di deploy *(dopo A6)*
Ripubblicare `dotnet publish -c Release -r linux-x64 --self-contained true`, rigenerare lo zip con
`appsettings.Production.json`, `deploy/vipi.service` e `deploy/nginx-vipi.conf`, e aggiornare il
`LEGGIMI-DEPLOY.md`.

⚠️ **Dire a Ivao.It che il pacchetto che hanno in mano non funzionerà mai su quel server**: è compilato
contro un provider che non supporta MariaDB. Tanto vale che smettano di provarci.

### A8 🟡 Riscrivere le decisioni, che oggi dicono il falso *(dopo A6)*
- **ADR-0007 §D4** dice «MySQL solo su net8, provider Pomelo»; **§D4-bis** dice «provider Oracle, su
  net10». Serve un **§D4-ter** che registri la realtà — MariaDB, Pomelo, host su net8 — e marchi §D4-bis
  come superata, com'è già stato fatto per §D4.
- Il **piano MySQL** va riletto per intero: parla di MySQL 8.0+ e del provider Oracle quasi ovunque.
- `guide/config.md` (tabella dei tre provider), `guide/integration.md`, `HANDOFF.md`.
- Memorie da correggere: `mysql-embedding-plan`, `multitarget-net8-embedding`, `deploy-hosting-options`.

### A9 🔴 Domande e conferme da Ivao.It
Messaggio pronto in appendice al piano, **da aggiornare** perché parla ancora di MySQL. Aperte:
- **Come raggiungiamo il database** (SSH? phpMyAdmin? IP autorizzato?) — decide se il travaso lo facciamo
  noi o gli consegniamo un file, e con quale limite di dimensione.
- **`sql_mode`** del server: strict o no (vedi A6).
- **I privilegi dell'utente `itivao_atc`**, in dettaglio: la migrazione iniziale apre con
  `ALTER DATABASE CHARACTER SET utf8mb4;` (lo emette Pomelo, non noi). Con `GRANT ALL ON itivao_atc.*`
  passa — verificato in locale il 6 agosto — ma con una lista ritagliata è la prima riga che si pianta.
- Che il database `itivao_atc` entri nel loro **piano di backup**.
- Sulla macchina: **WebSocket** sul reverse proxy (senza, Blazor Server apre le pagine e resta muto),
  header inoltrati, supervisione del processo, percorso persistente o key-ring su DB.

### A10 🔴 Redirect OIDC sul portale IVAO
`https://atc.it.ivao.aero/signin-oidc` e `/signout-callback-oidc`, esatti. E recuperare
**`VipiAuth:ClientSecret`**, l'unico dei quattro segreti che non è nei user-secrets locali — anche se il
flusso funziona senza, in modalità client pubblico con PKCE (verificato il 5 agosto).

---

## B. Branch non fusi — decisioni, non lavoro

### B1 ✅ `feature/aree-speciali-hardening` — verificata sull'app vera il 6 agosto 2026
I quattro punti sono stati guidati con la skill `verifica-live` su una copia del `vipi.db` reale, con import
veri contro l'API IVAO. Esito per esteso nella carta,
[`feature/2026-08-03-aree-regolamentate-hardening.md`](feature/2026-08-03-aree-regolamentate-hardening.md).

- **Riconciliazione al boot**: 993 → 230 aree, zero orfane, colonna `CenterId` sparita. Come previsto.
- **1. Interruttore** ✅ con la categoria spenta l'import aggiorna ACC e settori e **lascia le aree intatte**
  (230 aree / 247 legami, conteggi per ACC identici); a video «❄ Congelate». E dura **24 secondi** contro i
  minuti dell'import pieno: la fetch non parte davvero.
- **2. Dangling** ✅ cancellata a mano l'area `1131` citata dalla vIPI Brindisi: «⚠ 1131 non più disponibile»
  nell'editor, rilievo in `/vsop/admin/diagnostica`, `/vsop/health` a **Degraded**.
- **3. R49 «Zita»** ⚠️ la **meccanica multi-ACC funziona** — 7 aree con più enti, fra cui `WEST/EAST SARDINIA`
  e `Donald`/`Eolia` che ora appartengono anche a LIRR — ma **l'esempio è invecchiato**: oggi la sorgente
  elenca la 8870 sotto LIPP, LIRO, LIVK, LIZZ e non più sotto LIRR. Non è l'import: nello stesso giro la
  fetch di LIRR ha portato 105 legami. Sono cambiati gli elenchi IVAO fra il 3 e il 6 agosto.
- **4. Aree estere** ✅ «Importa aree» su LFMM → 162 aree e l'ente si accende; «Escludi aree» → 162 legami
  rimossi, torna «non importate», archivio di nuovo a 230/247 senza orfane (le 4 aree condivise restano).

⚠️ **Il ramo contiene anche B2**: `feature/aurora-bridge` è interamente dentro questi 18 commit (i 7 del
bridge, da `b5f1f58` a `7e7e406`, sono i suoi antenati). Fondere B1 porta dentro anche il bridge Aurora — e
l'endpoint `POST /vsop/api/v1/transfers/resolve` con esso. Va deciso in B4, non scoperto al merge.

Resta non provato un solo dettaglio, perché serve un ACC estero già citato da un documento: che riaccendere
l'ente faccia **rientrare** un'area diventata dangling.

### B2 🟡 `feature/aurora-bridge` — messo in sicurezza il 6 agosto 2026, resta la decisione
Il tool desktop funziona **solo** contro un host locale finché l'endpoint
`POST /vsop/api/v1/transfers/resolve` non è acceso in produzione. Chiuse per decisione: i sorvoli LIBB senza
livello (lacuna redazionale, il tool non deve indovinare) e il pacchetto macOS.

⚠️ **Non è un ramo a sé**: sta per intero dentro B1. Fondere B1 porta dentro anche questo.

**Rivisto prima di considerarlo mergiabile** — era l'unica superficie pubblica, anonima e interrogabile da
fuori del sito, e aveva tre difetti nella protezione (nessuno nella logica, che è pura e testata):
- **`AuroraBridge:Enabled`, default `false`**: spento, la rotta non si registra affatto. È ciò che rende
  reversibile la decisione di B4 — fondere B1 non aggiunge superficie pubblica finché nessuno la accende.
- **Tetto complessivo** (600/min) accanto a quello per IP: dietro il reverse proxy l'indirizzo arriva da
  `X-Forwarded-For` e `UseForwardedHeaders` gira senza proxy noti, quindi **la chiave del tetto per IP la
  sceglie il chiamante**. Aggiunto anche un tetto alle chiavi tracciate: ruotare l'header faceva crescere il
  dizionario dei contatori senza limite — esaurimento di memoria a colpi di richieste da 200 byte.
- **Cache di 30 s della topologia globale**, solo per il bridge: ogni richiesta rileggeva tutti i settori
  attivi, e su `atc.it.ivao.aero` quel costo lo pagherebbe il database condiviso col sito che ci ospita.
- `MaxRequestBytes` era un'opzione morta: il tetto del corpo era una costante.

Osservato scrivendo il test invece di darlo per buono: a endpoint spento la risposta è **405**, non 404 — il
catch-all delle pagine risponde al GET di qualunque percorso, a mancare è il verbo. Il tool desktop traduce
404/405 in «su questo sito il bridge non è attivo» invece del codice nudo.

Suite **944 verde**, e le correzioni sono state fuse anche in B1, che altrimenti se le sarebbe perse.

**Cosa resta**: decidere se accenderlo e quando, cioè B4. Il tool non è mai stato esercitato contro un host
remoto vero: se si accende, la prima sessione con Aurora va guidata.

### B3 ✅ `fix/dataprotection-retry` — fuso il 6 agosto 2026
Fuso in `feat/persistenza-mysql` e ramo cancellato. Il commit aggiunge
`EnableRetryOnFailure` al context del key-ring Data Protection, che apriva la connessione senza, a
differenza di `VipiDbContext`: su Neon un transient sul key-ring uccideva antiforgery, cookie di auth e
state OIDC (i «Correlation failed» del 3 agosto). Il passaggio a net8/Pomelo non aveva toccato il file, e
il ramo Postgres resta in piedi perché Neon resta l'ambiente di prova ⇒ fusione pulita, suite verde
(net8 309 · net10 300 + gli altri progetti).

⚠️ Vive dentro A4: quando il key-store passerà a MariaDB, la stessa resilienza va rifatta lì — Pomelo ha il
proprio `EnableRetryOnFailure`, e questa registrazione oggi è nel ramo `Persistence:Provider=Postgres`.

### B4 🟡 Cosa mandare in produzione
Decisione a monte del cutover, e ora è **binaria**: `main`, oppure `main` + B1 — che si porta dentro B2,
perché il ramo del bridge sta per intero dentro quello delle aree. L'opzione «B1 senza B2» non esiste senza
riscrivere la storia dei rami.

Va deciso **prima** del travaso dati, perché B1 cambia i dati delle aree (archivio 993 → 230): il `.sql` di
A3 andrebbe rifatto **dopo** il merge.

Cosa pesa, ora che B1 è stata guidata e B2 messo in sicurezza:
- **a favore**: le quattro verifiche di B1 sono passate sull'app vera; il ramo porta l'interruttore che
  impedisce a un import storto di potare le aree buone, e l'alleggerimento dell'archivio;
- **contro**: entra anche il bridge Aurora, il cui tool desktop non è mai stato esercitato contro un host
  remoto — ma l'endpoint ora **nasce spento**, quindi entra come codice, non come superficie.

---

## C. Debito noto — non urgente, ma non dimenticabile

### C1 🟡 Il percorso Npgsql di `ISchemaDriftProbe` non è mai stato eseguito
L'analizzatore è coperto dai test e la query è la stessa `information_schema` del reconciler, ma la prima
conferma vera sarà il primo deploy. Se la diagnostica mostra righe di drift inattese, quasi certamente è un
**falso positivo di tipo**: si estende la mappa alias in `SchemaDriftAnalyzer.Canonical`.

### C2 🔴 `ImportSids` potrebbe essere spento in produzione senza che nessuno l'abbia deciso
La migration dell'8 luglio creò la colonna con `defaultValue: false` e il reconciler la backfillava a
`false`: su un DB dove la riga `ImportPolicies` esisteva già, la categoria è **nata spenta**. Non è
ribaltabile da codice — `false` è indistinguibile da una scelta dell'admin. **Da guardare in
`/vsop/admin/sorgenti`** e rimettere a mano. Memoria: `bool-column-default-trap`.

### C3 🟡 ADR-0007 punto (b): migrazioni Postgres versionate
Il `PostgresSchemaReconciler` copre **solo le aggiunte di colonna**: il primo rename, drop o cambio di tipo
su Neon va applicato a mano. Il punto duro non è il lavoro corrente ma il **baseline**: lo schema su Neon è
il prodotto di `EnsureCreated` più le toppe del reconciler, e va riprodotto in uno snapshot iniziale da
timbrare come applicato. Meno urgente ora che la produzione è MariaDB e Neon resta ambiente di prova.

### C4 🟡 Cache-busting degradato dal passaggio a net8
`MapStaticAssets` (.NET 9+) è stato sostituito da `UseStaticFiles` + un suffisso `?v=` unico per tutti gli
asset (`AssetVersion`). Dopo ogni deploy il browser riscarica **tutti** gli asset invece dei soli cambiati.
Accettato consapevolmente; da rivedere solo se Pomelo pubblicasse un giorno una build per EF Core 10.

### C5 🟢 Audit 22 luglio — voci ALTE ancora aperte
`history/audit-2026-07-22-criticita-full-stack.md`: **A2** scala Blazor (backplane), e la parte di **D1**
che riguarda il provisioning. La Fase 1 e la Fase 2 sono eseguite.

---

## D. Verifiche live pendenti da sessioni passate

Tutte con la skill `verifica-live`. Sono lavori già scritti e testati, che nessuno ha ancora **guidato**.

- ✅ **Aree regolamentate** — fatta il 6 agosto 2026: esito in B1.
- 🟢 **Settori esteri aggiunti a mano** (es. `LGKR_APP` su coppia confinante confermata): verifica IVAO +
  `AccSector` + riproiezione, e la guardia anti-hijack. Memoria: `foreign-sector-manual-add`.
- 🟢 **Coordinamenti/sorvoli rielaborati**: sorvoli senza aeroporto, parità di livello, CoP `ALL`/`ALL-to-X`,
  vLOA in stile ACC+EN, lookup aeroporto IVAO fuori DB. Memoria: `transfers-overflight-rework`.
- 🟢 **Retention pubblicazione**: resta solo il riscontro del conteggio righe sul DB.
  Memoria: `publication-retention-plan`.

---

## E. Funzionalità aperte (da `HANDOFF.md` §5)

Ordinate per valore, come lì.

### E1 Live IVAO — rifiniture
- **Identità «P»** legata al callsign connesso del CH loggato (oggi selettore manuale in Ridotta).
- **Mapping token-handler → callsign** nei trasferimenti: oggi è un'euristica match-segmento, valutare una
  tabella esplicita.
- **Endpoint membri divisione** `/v2/divisions/IT/members` da confermare.
- Estendere `live=true` a **vIPI aeroporto** e **vLOA** (oggi solo ACC Ridotta).

### E2 Dati reali che mancano
- **Shape reali delle TWR** dal sectorfile GitHub, a rimpiazzare i cerchi sintetici da 5 NM
  (`IsShapeSynthetic`).
- **Minime MVA** (`<icao>.mva`, stesso repo): riusa il pattern delle SID — parser, import gated,
  pubblicazione differita al ciclo AIRAC successivo.
- 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso. Si sistemano
  dalla pagina: il filtro «solo da agganciare» li raccoglie.
- La SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft», quota implausibile. Da
  correggere nell'editor: è un dato, non un bug.

### E3 Fonte unica — follow-up del Round 20
Documenti e AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Resta da estendere
la risalita della gerarchia alla «presidenza aeroporto» generale — chi controlla l'aeroporto adesso —
com'è già stato fatto per i trasferimenti.

### E4 Auth di produzione
Confermare gli **staff code reali** IVAO: ruoli di divisione (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`) e
ruoli chief ACC-scoped (`{ACC}-CH`, `{ACC}-ACH`). Oggi sono ipotesi in configurazione.

### E5 Copertura e rifiniture
Viewer dell'**audit log**, «scarta bozza», editor visuale delle mappe AoR, test property-based sull'AoR.

---

## F. Rimandato, non cancellato

**Embedding nel sito `Ivao.It.Website`.** Il sito definitivo è il nostro host standalone, ma le cinque
librerie restano multi-target `net8.0;net10.0` proprio per questo — e ora che `Vipi.Host` è net8, la
distanza fra i due scenari è minima. Lavoro aperto in
[`guide/integrazione-ivao-it-da-fare.md`](guide/integrazione-ivao-it-da-fare.md): runtime EF Core 8 mai
eseguito (⚠️ ora lo sarà, in produzione), doppia localizzazione, Bootstrap del sito che sbava dentro
`.vipi-root`.
