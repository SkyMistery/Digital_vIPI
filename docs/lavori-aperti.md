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
MariaDB 11.4.10 vera** (A1): schema, collation, case-sensitivity e avvio dell'applicazione. Resta da
provarci sopra il travaso (A2/A3) e i flussi editoriali (A6).

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
- **`lower_case_table_names`** è 1 su Windows e sarà 0 sul loro Linux: qui le tabelle esistono come `accs`,
  lì solo come `Accs`. EF genera i nomi giusti, ma `Vipi.DbSeed` scriverà `TRUNCATE` a mano → **A2 usi i
  nomi con le maiuscole**, o funzionerà solo in locale.
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

### A2 🟢 `Vipi.DbSeed` a net8 *(A1 fatta)*
Il tool è `net10.0` e con Pomelo — che esiste solo per EF Core 8 — non può scrivere su MariaDB. Va portato
a net8. Poi vanno fatte le modifiche già progettate: lettura da **Postgres** (oggi legge solo SQLite),
scrittura su MariaDB, e i due punti Postgres-specifici da sostituire (`TRUNCATE … RESTART IDENTITY CASCADE`
→ `SET FOREIGN_KEY_CHECKS=0` + `TRUNCATE` per tabella; `setval` → `ALTER TABLE … AUTO_INCREMENT`).
Conservare il trucco a due fasi per il ciclo `Document↔DocumentVersion` e la normalizzazione
`DateTimeKind.Utc`. Dettagli: §S8 del piano.

### A3 🔴 Travaso dei dati veri *(serve la connection string di Neon)*
`Neon → DbSeed → MariaDB locale → mysqldump → .sql`. Il 3306 loro è su `localhost`, quindi da qui non ci si
scrive: il deliverable è un file. **Riconciliare per conteggio riga per tabella**, non a occhio.
Va rifatto **poco prima del cutover**: fra la prova e il passaggio in produzione il sito su Render continua
a essere modificato.

⚠️ `mysqldump` con un utente ristretto richiede `--no-tablespaces`.

### A4 🟢 Data Protection su MariaDB *(A1 fatta)*
`src/Vipi.Host/VipiDataProtection.cs` monta il key-store su DB **solo se il provider è Postgres**. Sotto
MariaDB torna al file-store: su filesystem effimero significa antiforgery rotto e utenti sloggati a ogni
riavvio. Con il deploy standalone il key-ring è responsabilità nostra, non dell'host ospitante.
Trappola già pagata una volta su Postgres: `EnsureCreated()` verifica il *database*, non la tabella —
serve `CREATE TABLE IF NOT EXISTS`. Da verificare **sopravvivendo a un riavvio**, non per ispezione.

### A5 🟢 CI con MariaDB *(A1 fatta)*
Servizio MariaDB della stessa versione nel workflow. I test del ramo MariaDB girano sotto **net8**
(`Vipi.Infrastructure.Tests` è già multi-target apposta). Estendere il job esistente a
`dotnet test -f net8.0`.

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

### B1 🔴 `feature/aree-speciali-hardening` (18 commit avanti)
Chiuso come codice, suite verde, ma **mai verificato sull'app vera**, e `HANDOFF.md` dice esplicitamente di
non fonderlo prima. Quattro punti da guidare, nell'ordine, con la skill `verifica-live`:
1. `/vsop/admin/sorgenti` → togli «Aree regolamentate», lancia l'import: le aree devono **restare**.
2. Editor con un'area cancellata a mano dal DB → «⚠ non più disponibile» nel picker e il rilievo in
   `/vsop/admin/diagnostica`.
3. Dopo un import, la **R49 «Zita»** (id 8870) dev'essere fra le aree **proprie** sia di LIRR sia di LIZZ.
4. `/vsop/admin/accs` → «Importa aree» su un ACC estero lo accende; «Escludi aree» lo spegne.

Due cose da sapere prima di avviare: al primo boot una riconciliazione one-shot **spegne gli ACC esteri**
(763 legami su 993, restano le 230 italiane); e dopo il deploy va premuto «Importa da sorgente», perché il
backfill recupera una sola appartenenza per area.

Carta: `feature/2026-08-03-aree-regolamentate-hardening.md`.

### B2 🔴 `feature/aurora-bridge` (7 commit avanti)
Il tool desktop funziona **solo** contro un host locale finché l'endpoint
`POST /vsop/api/v1/transfers/resolve` non è rilasciato. Da rivedere e unire. Chiuse per decisione: i
sorvoli LIBB senza livello (lacuna redazionale, il tool non deve indovinare) e il pacchetto macOS.

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
Decisione a monte del cutover: il sito definitivo nasce da `main`, da `main` + B1, o da `main` + B1 + B2?
Va deciso **prima** del travaso dati, perché B1 cambia i dati delle aree.

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

- 🟢 **Aree regolamentate** — i quattro punti di B1.
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
