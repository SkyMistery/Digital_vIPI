# Lavori aperti — elenco unico

**Aggiornato:** 22 agosto 2026 (sera) · **Scopo:** una cosa alla volta, senza rileggere la cronologia.

Ogni voce è pensata per essere presa da sola in una sessione nuova. Dove serve contesto, il rimando è al
documento che ce l'ha per esteso. L'ordine dentro ogni sezione è quello in cui conviene affrontarle.

**Legenda del blocco:** 🟢 si può fare subito · 🟡 dipende da un'altra voce · 🔴 dipende da qualcun altro
(Ivao.It, il portale IVAO, l'owner).

> ### 🆕 15 agosto 2026 — trasferimenti e audit database **fusi in `main`**, per la consegna
> `main` non è più fermo al 9 agosto: ci sono dentro sia i **trasferimenti ACC↔APP** (ex
> `feature/trasferimenti-acc-app`, PR #13 — B6, E6) sia l'**audit di database del 14 agosto** (§G). Il merge
> è stato fatto in quest'ordine apposta: le due migrazioni del 14 agosto esistevano in **due copie con lo
> stesso identificativo**, una per ramo, e al secondo merge si è tenuta quella che porta dentro il modello
> coi trasferimenti (l'altra ne aveva uno più povero, e lo `ModelSnapshot` deve descrivere il modello fuso).
>
> ✅ **Il 22 agosto sono stati fusi in `main` gli ultimi due rami che avevano roba dentro**, in quest'ordine e non
> a caso: prima **`brand-atmosphere`** (brand IVAO alla sua fonte, tema chiaro/scuro scelto dall'utente), poi
> **`feature/services-hub-profile-swapper`** (hub `/services`, prefisso `/services/vsop` con le rotte italiane
> tradotte, Aurora Profile Swapper, topbar misurata). Il primo **riscrive** `vipi-theme.css` (+937/−546), il
> secondo ci **aggiunge** poco (+130/−89): mettendo sotto il tema, il secondo merge ha riportato 130 righe sui
> token invece di 937 all'indietro. Otto file in conflitto, risolti con una regola sola — **struttura dal ramo
> dei servizi, colori dai token del tema**.
>
> Da qui **`main` è il posto dove si lavora, e non c'è più nessun ramo con lavoro fuori**: gli altri undici sono
> tutti a zero commit di distanza — e il 22 agosto sono stati **cancellati tutti e undici**, locale e origin.
> ⚠️ Cancellandoli è saltata fuori una cosa: **`refactor/13-tre-documenti` (B5) non aspettava nessun ok**, era
> in `main` dal 15 agosto, portato dentro dal merge dei trasferimenti. Vedi B5.
>
> ⚠️ La frase ha vacillato per mezza giornata — la sera del 22 agosto è nato `catalogo-punti-suggerimenti` —
> ma è di nuovo vera: quel ramo è **fuso e cancellato** (vedi **B7**).
> Carte: [servizi ATC](feature/2026-08-22-servizi-atc-e-profile-swapper.md),
> [brand](feature/2026-08-22-brand-atmosphere.md), [topbar misurata](feature/2026-08-22-topbar-misurata.md).
>
> ⚠️ **Fuso non vuol dire consegnabile**: il blocco al deploy resta dov'era — sezione E, punto 9: le migrazioni degli
> accordi girano all'avvio e `AgreementSectionsFinalize` fallisce finché la MariaDB di produzione non è convertita. Il merge non
> lo tocca e non lo risolve.
>
> - **11 agosto — audit full-stack, eseguito** (sta in B5). 34 voci, 23 chiuse, 3 ribaltate dalla misura.
>   Tre regole di build che cambiano: `TreatWarningsAsErrors` in `Directory.Build.props`, i test che
>   **girano davvero su net8** (da 347 a 1115) e i `packages.lock.json` committati con restore in locked
>   mode. ⚠️ `dotnet test` **non** applica il flag degli avvisi: suite verde e build di produzione rotta
>   possono convivere, quindi prima di un push serve `dotnet build Vipi.slnx -c Release --no-incremental`.
>   Esito in [`history/audit-2026-08-11-crepe-full-stack.md`](history/audit-2026-08-11-crepe-full-stack.md).
>
> ⚠️ **Il primo effetto da leggere prima di consegnare:** i dump del 6, 7 e 9 agosto (A3) sono **tutti
> inutilizzabili**, e sembravano perfetti. Vedi A3.
>
> ⚠️ **Un test intermittente non chiuso** (bridge Aurora): fallisce **solo** nella corsa completa in
> parallelo della soluzione, mai da solo — otto giri isolati e una seconda corsa completa sono verdi. Il
> sospetto è la **contesa fra progetti** (porta, file temporaneo, cartella condivisa), non il tempo dentro
> un test. Alla prossima occorrenza **tenere il log intero** (`dotnet test Vipi.slnx > log.txt 2>&1`): il
> nome del test sta nella riga sopra «Error Message».
>
> **22 agosto 2026 — il nome, preso.** `AuroraBridge.Tests.AuroraClientTests.Richieste_in_sequenza_non_si_mescolano`.
> Caduto nella corsa completa in Release (`la seconda richiesta non ha avuto risposta: Nessuna risposta a #TRPOS
> entro 15000 ms`), **verde da solo subito dopo in 65 ms** — cioè tre ordini di grandezza sotto la scadenza,
> il che esclude che sia lento e conferma che la risposta non arriva **affatto**. Il giro non toccava il bridge
> (rename delle rotte), quindi la causa resta la contesa, non una regressione. Il candidato ora ha un indirizzo:
> il client Aurora è a **porta/socket condivisa**, e in corsa parallela è l'altro progetto a occuparla.

> ### ✅ 22 agosto 2026 — CHIUSA: la topbar non sfonda più, perché non indovina più
> Chiusa lo stesso giorno, e **non** con nessuna delle due strade che questa voce proponeva. Il committente
> l'ha riaperta da un'altra parte: vedeva la barra rotta già a **1940**, cioè 530px sopra il numero misurato
> qui — perché la sua configurazione (zoom di pagina, stringa staff, login) non era quella su cui le soglie
> erano state tarate. Il difetto non era la soglia: **una media query misura la finestra, mentre il problema
> è la larghezza della barra**, che dipende da sei cose che una `@media` non vede.
>
> Adesso la barra si **misura** e sceglie da sé lo scaglione (`vipiFitTopbar` in `vipi-ui.js`, classi
> `tb-1…tb-4` in `vipi-theme.css`). Verificato su **256 combinazioni** — 8 larghezze × 4 zoom × 4 famiglie
> di pagina × 2 lingue — con `scrollWidth == clientWidth` su tutte e nessun comando perso.
>
> ⚠️ E il compromesso che questa voce dava per obbligato **non c'è stato**: a 1366 e 1440 la ricerca resta
> **aperta e intera**, perché separando «la ricerca si chiude» da «le etichette spariscono» il gradino da
> 500px è diventato due. Carta: [`feature/2026-08-22-topbar-misurata.md`](feature/2026-08-22-topbar-misurata.md).
>
> <details><summary>La misura di allora, per memoria</summary>

> ### 🆕 22 agosto 2026 — la topbar sfonda fra 1301 e ~1410px (**preesistente**, misurato)
> Trovato guidando la verifica live dei servizi ATC, e **non e' del giro nuovo**: si misura identico con e
> senza il tasto aggiunto quel giorno. Lo scaglione 2 della barra (`vipi-theme.css`, `@media (max-width:1300px)`
> — «Editor»/«Incarichi» a sole icone, ricerca richiusa) scatta **troppo tardi**:
>
> | larghezza | sforamento |
> |---|---|
> | 1420 e oltre | 0 |
> | 1400 | +10 |
> | 1380 | +30 |
> | 1350 | +60 |
> | 1320 | +90 |
> | 1301 | **+109** |
>
> Cioe' la soglia andrebbe a **~1410**, non a 1300: appena sopra i 1300 alla barra mancano 109px. Sotto i
> 1300 lo scaglione scatta e torna tutto a posto, quindi il difetto vive in una fascia sola — che pero'
> contiene **1366 e 1400**, due larghezze di portatile molto comuni.
>
> ⚠️ **Non l'ho corretto da solo perche' e' un compromesso, non un numero.** Il commento nel CSS racconta
> che quella soglia fu tarata *apposta* per tenere la **ricerca aperta** il piu' a lungo possibile, sbagliando
> due volte in direzioni opposte. Alzarla a 1410 ripara lo sforamento **e** richiude la ricerca su tutti i
> portatili 1366: e' esattamente la cosa che quella taratura voleva evitare. Le due strade sono alzare la
> soglia, oppure recuperare ~110px dentro la fascia (il candidato piu' grasso e' la ricerca, che a barra
> piena vale piu' di 200px).
>
> </details>

> ### 🆕 22 agosto 2026 — due difetti trovati guardando, mentre si chiudeva la topbar
> Nessuno dei due e' del chrome, e nessuno dei due e' stato toccato: sono qui perche' li ha visti la verifica
> della barra, e perche' il primo e' **la stessa malattia appena curata**.
>
> **1. La tabella SID sfora a zoom alto** (🟢 si puo' fare subito). Sul viewer aeroporto a 1280 con zoom 1.4
> — cioe' **914 unita' di layout** — la pagina sfora di 58px, e il colpevole e' `table.sid-table` col suo
> `min-width:720px`. Misurato che **non e' il chrome**: tolta la topbar dal DOM lo sforo resta identico.
> ⚠️ La regola che fa scorrere le tabelle dentro di se' e' `@media (max-width:900px)`, e 914 > 900: **una
> soglia di viewport che non vede lo zoom**, esattamente il difetto che la barra aveva. La cura e' la stessa
> in spirito: legare quella regola alla larghezza in unita' di layout, non a quella della finestra.
>
> **2. La cultura non arriva al circuito** (🟡 merita una carta sua). Su `/services/vsop?culture=it` il
> prerender scrive «‹ Servizi ATC» e subito dopo il circuito `InteractiveServer` ri-renderizza
> **«ATC Services»** — inglese in pagina italiana. Vale per **ogni** pagina `InteractiveServer`: il chrome
> resta giusto perche' e' SSR statico, ed e' per questo che non se n'era accorto nessuno. Si vede quando la
> lingua e' scelta dalla query (o dal cookie) e non coincide con quella del browser, quindi un utente
> italiano con browser italiano non lo incontra mai — ma un pilota straniero che sceglie l'italiano si'.

## Dove siamo, in cinque righe
Il **cutover MariaDB è in `main`** e verificato (A1–A8). Le sezioni **B** (branch), **C** (debito, tranne C3
tenuta aperta con la ragione scritta) e **D** (verifiche live arretrate) sono **chiuse**. La **E** è stata
sfoltita: metà delle voci erano già fatte o non avevano più senso — ricontrollare un elenco prima di
lavorarci si è rivelato più produttivo che eseguirlo. Dal 14 agosto c'è una sezione **G**: l'audit del
database, chiuso lato codice.

⚠️ **Quel che resta è quasi tutto fuori dal codice**: rifare e consegnare il `.sql` (i tre dump esistenti
hanno il BOM) e il pacchetto, le risposte di Ivao.It (A9/A10) — fra cui **chi fa il backup**, domanda a cui
oggi nessuno sa rispondere — la rotazione della password Neon, e quattro decisioni di contenuto: la SID
`BANA8A`, le 33 torri senza padre, **quali staff code valgono admin** (E4) e se pubblicare una *release*
debba scrivere audit.

✅ **Non resta più nessuna decisione di merge**: B6 fu presa il 15 agosto, e B5 si è rivelata già presa — il doc
13 era in `main` da allora senza che l'elenco lo sapesse.

---

## A. Cutover su `atc.it.ivao.aero` — la strada critica

✅ **Fuso in `main` il 9 agosto 2026**: il ramo `feat/persistenza-mysql` non è più il posto dove si lavora —
`main` è net8 + Pomelo + MariaDB. Contesto: [`design/piano-supporto-mysql.md`](design/piano-supporto-mysql.md),
decisioni in ADR-0007 §D4/§D4-bis (⚠️ entrambe **superate**, vedi A8).

Stato: il server è **MariaDB 11.4.10**, non MySQL. `Vipi.Host` è passato a **net8** e il provider è
**Pomelo**; suite verde su net8 (309) e net10 (300). **Dal 6 agosto 2026 il ramo è provato contro una
MariaDB 11.4.10 vera**: schema, collation, case-sensitivity, avvio dell'applicazione (A1), key-ring
Data Protection che sopravvive a un riavvio (A4) e **travaso dei dati veri da Neon fino al `.sql`
reimportabile** (A2/A3). Restano i flussi editoriali (A6) e la CI (A5).

**Dal 7 agosto 2026 il ramo porta anche B1+B2** (B4 deciso: in produzione va `main` + B1). Quel merge ha
richiesto tre correzioni che i test guardia hanno chiesto da soli, e che valgono come promemoria del costo
dichiarato in ADR-0007 — **ogni cambio di schema va emesso due volte**:
- migrazione MySQL **`20260807125819_SpecialAreasHardening`**, che copre le tre migrazioni SQLite delle aree
  in una sola (il set MySQL nasce il 5 agosto e non ha una storia da rispettare). ⚠️ Lo scaffold di EF metteva
  il `DropColumn` di `SpecialAreas.CenterId` **prima** del travaso: riordinato a mano e aggiunto il backfill,
  come nella gemella SQLite, o su un database con dati i legami sparivano in silenzio;
- `MySqlStringLengths`: `SpecialArea.CenterId` non esiste più, e le due colonne della PK composta di
  `SpecialAreaCenter` vanno lunghe **esattamente** come le principali (64 e 16) o è `errno 150`;
- lo smoke E2E del bridge spento pretendeva **405**: quel codice è di net10, dove il catch-all della pagina
  «non trovato» risponde al GET di qualunque path. Su net8 — l'host che va in produzione — è **404**.

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
4. **Avvio con `Persistence__Provider=MySql`** — `/services/vsop` 200, `/vsop/health` Healthy, `/vsop/health/ready`
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
aeroporti, che su un database appena creato è vuoto finché non si passa da `/services/vsop/admin/acc` → «Importa da
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
database serve le pagine con i dati veri (`/services/vsop` mostra LIRR/LIMM/LIBB).

✅ **Il percorso sorgente-Postgres è stato eseguito il 9 agosto 2026** (A3): 4303 righe da Neon, 4314 scritte
su MariaDB, 38/38 tabelle riconciliate. Era l'unico pezzo del tool che nessuno aveva visto girare.

ℹ️ `/vsop/health` su quel database dice **Degraded**. Non è il travaso né MariaDB: l'host avviato sullo
**stesso `vipi.db` via SQLite** dice Degraded uguale. Sono le incongruenze soft-ref già note (E2: gerarchia
`ParentCallsign` dangling), che viaggiano coi dati.

### A3 ✅ Travaso dei dati veri — catena eseguita il 6 agosto 2026
`Neon → DbSeed → MariaDB locale → mariadb-dump → .sql`, tutta. Da Neon: **4807 righe** lette, 4818 scritte,
**37 tabelle su 37 riconciliate** dal tool. Dump: `_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-06.sql`,
4,7 MB, sha256 `B4989D63…A475A296`. Procedura e opzioni: §6 di
[`../deploy/mariadb/README.md`](../deploy/mariadb/README.md).

**Verificato reimportandolo**, non guardandolo: database vuoto → import → 38 tabelle, 4808 righe, conteggi
**identici** all'origine; host avviato su quel database → `/services/vsop` 200 con LIRR/LIMM/LIBB a schermo e
**nessuna migrazione riapplicata** (`__EFMigrationsHistory` viaggia nel dump apposta).

⚠️ **Il primo dump era inutilizzabile e sembrava perfetto.** Windows nasce con
`lower_case_table_names=1`: i nomi di tabella si salvano in minuscolo e `mariadb-dump` li riemette così, per
cui sul loro Linux (`=0`) sarebbero nate `accs` mentre EF cerca `Accs`. Rifatto tutto con
`lower-case-table-names=2` nel `my.ini` — lo 0 su Windows corrompe gli indici — da mettere **prima** di
creare lo schema. È la trappola annotata in A1, materializzatasi al primo tentativo.

Il `.sql` **non è nel repository**: contiene contenuto reale, VID dello staff e audit log. Sta in
`_mariadb/dump/`, fuori dall'albero.

**Rifatto dopo il merge di B1 — 9 agosto 2026, ed è questo il dump buono.**
`_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-09.sql`, 4,0 MB, sha256
`1CD77F3A5428AA55ECB85F96DB9D8939224C4974D0DA2694F8BFA7801B562DFC`. Da Neon **4303 righe**, 4314 scritte,
38/38 tabelle riconciliate; riletto in un database vuoto: **39/39 tabelle combaciano**, 4305 righe, e i
legami multi-ACC sopravvivono al giro (223 aree con un ente, 4 con tre, 3 con quattro = 247). I due dump
precedenti (06 e 07 agosto) sono **superati**: il primo ha l'archivio vecchio da 993 legami, il secondo il
solo backfill da 230.

⚠️ **Il dump del 7 agosto sembrava a posto e non lo era.** Dopo il deploy di B1 l'archivio aveva 230 legami,
uno per area: la firma del **solo backfill**. Il motivo non era un guasto ma il **gate a 24h** di
`ImportState` — l'ultimo import aree era del 6 agosto 18:15, quindi al boot veniva saltato, e solo il
bottone manuale lo scavalca. Premuto quello, i legami sono tornati **247**. Il controllo che lo rivela è
`SpecialAreaCenters == SpecialAreas`: se i due numeri coincidono, l'import delle aree non è ancora girato.

L'import è stato lanciato **da un host locale puntato a Neon** (`Persistence__Provider=Postgres` +
connection string di Neon), non dal sito: usa il secret IVAO dei user-secrets locali, che funziona, mentre
quello su Render risultava stale il 5 agosto. Esito: «ACC: 0 create, 7 aggiornate · settori ACC: 0 create,
147 aggiornati».

ℹ️ La pagina è **`/services/vsop/admin/acc`**, al singolare — `/services/vsop/admin/accs` non esiste e risponde 404 (su net8
non c'è nemmeno il catch-all che su net10 darebbe altro). Il bottone è inoltre inerte finché non si prende
il **lock di risorsa** dalla barra in cima: `OnLockChanged(mine)` è ciò che accende `_canEdit`.

⚠️⚠️ **14 agosto: tutti e tre i dump — 6, 7 e 9 agosto — hanno il BOM, e sono da rifare.** La ricetta usava
la redirezione di PowerShell 5.1, che scrive UTF-8 **con BOM** e converte i fine riga in CRLF. Quei tre byte
finiscono davanti alla prima istruzione del file, e sul loro Linux `mariadb < file.sql` muore con un
`ERROR 1064` **alla riga 1** — un errore che parla di sintassi mentre il problema è la codifica, cioè il
modo peggiore di scoprirlo, in call con loro il giorno del cutover. Non era una svista di un giorno: era
nella ricetta, quindi in tutti i dump prodotti fin qui.

Si rifà da una shell che scrive byte grezzi (Git Bash, `cmd`) e **si controlla prima di consegnare**: i primi
quattro byte devono essere `2f 2a 4d 21` (`/*M!`) e non `ef bb bf`.
```sh
od -A n -t x1 -N 4 vipi-atc-it-ivao-aero-<data>.sql
```
Ricetta corretta e spiegazione in [`../deploy/mariadb/README.md`](../deploy/mariadb/README.md) §6.

**Cosa resta, e non è lavoro tecnico:**
- **Consegnarlo**, per il canale che concorderanno (A9) — con 4 MB va verificato che phpMyAdmin regga.
- **Rifarlo poco prima del cutover**: fra oggi e il passaggio, Render continua a essere modificato. Stesso
  comando, due minuti — e prima di rifarlo, premere «Importa da sorgente» e ricontrollare quei due conteggi.
  ⚠️ Va comunque rifatto **subito**, BOM o no: quello in mano oggi non è consegnabile.
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

### A6 ✅ Verifica live sui flussi editoriali — **chiusa il 9 agosto 2026**
Guidata con la skill `verifica-live` su `Vipi.Host` con `Persistence__Provider=MySql` contro la MariaDB
11.4.10 locale, caricata col **travaso vero da Neon** (A3), non con dati finti. `/vsop/health` e
`/vsop/health/ready` **Healthy**.

**Due bug trovati, entrambi corretti e con la loro rete di test.** Nessuno dei due è colpa del provider:
sono corse che MariaDB rende sistematiche, e che su SQLite e Postgres capitano solo con la tempistica
giusta — cioè nel modo peggiore.
- **`/services/vsop/admin/transfers` e `/services/vsop/admin/permissions` non si aprivano affatto**: leggono
  `IStationResolver` dal **markup**, e il lazy-load partiva durante il render sul `DbContext` del circuito
  ⇒ «A second operation was started», circuito morto (la pagina restava al prerender, che a occhio sembra
  viva). Sistemate con `Stations.Prewarm()` nel ciclo di vita, come già facevano `AccVipiPage`, `SopHome` e
  `VloaListPage` dal 29 luglio: queste due erano rimaste indietro. Guardia: `StationResolverPrewarmTests`
  cammina i `.razor` e pretende il Prewarm da **ogni** componente interattivo che legge il resolver nel
  render — il chrome statico (`SopLayout`) è escluso per costruzione, non per elenco.
- **`/services/vsop/live/{callsign}` uccideva il circuito**: `LoadAsync` ha **due** ingressi non coordinati — il
  ciclo di vita e il callback SSE `OnLiveUpdate`, che il poller invoca a ogni giro — e un aggiornamento che
  atterra a lettura in corso ne fa partire una seconda sullo stesso context. Serializzati con un
  `SemaphoreSlim`. Guardia: `LivePageConcurrencyTests` lancia i due ingressi in parallelo contro un servizio
  che si accorge della sovrapposizione. Entrambi i test sono stati **visti fallire** senza la correzione.

**Flussi esercitati e passati:** import ACC (7 aggiornati) e settori (147) — girati sia al boot sia dal
bottone manuale; import settori-aeroporto; **import SID per-ICAO** dall'editor («SID LIBC: 16 estratti»);
**pubblicazione** dall'editor aeroporto LIBC (release v6 ciclo 2608 `Effective`, la v5 archiviata a
`Superseded`, snapshot da 3355 byte); **lock di risorsa** (preso e rilasciato dalla barra); **ricerca
globale**; **vista live** per callsign; **blob**: le tre immagini in archivio escono dall'endpoint con byte
identici alla colonna `longblob` (179286 / 146102 / 280283).

⚠️ **La collation `as_cs` NON ha reso la ricerca sensibile alle maiuscole** — era il rischio dichiarato in
`MySqlCollation`: `LIBC`/`libc` danno 2 risultati, `Crotone`/`crotone` 1. Verificato, non dedotto.

**Seconda passata, 9 agosto: chiusi i tre buchi che restavano.**
- **Scrittura del blob** ✅ «+ Image» in una sezione extra dell'aeroporto: PNG da 694 byte caricato, salvato,
  e riletto dall'endpoint con **sha256 identico** a quello del file di partenza. `longblob` regge andata e
  ritorno.
- **Pubblicazione degli altri due tipi** ✅ vIPI **ACC** (release v16, payload 62 KB, con la derivazione
  pesante che gira al publish) e **vLOA** (release v2, payload **73 KB**). ⚠️ L'editor vLOA non si apre con
  `?doc=`: vuole `/services/vsop/{acc}/vloa/editor?acc={estero}` — con `?doc=8` si finisce sull'editor ACC e si
  pubblica quello, cosa che è successa al primo tentativo.
- **`sql_mode` non-strict** ✅ provato davvero, mettendo il server in `NO_ENGINE_SUBSTITUTION` e rifacendo la
  passata: **nessuna differenza** sulle pagine esercitate. Le uniche due che cambiavano erano quelle col
  METAR live, cioè dato che cambia da sé. Non è una dimostrazione — è l'assenza di sintomi sulla superficie
  provata — ma la domanda ad A9 resta per prudenza, non per ignoranza.

ℹ️ Osservazione, non guasto: **pubblicare una release non scrive audit**. L'audit lo scrive solo la
promozione di una bozza (`EfReleaseRepository.PromoteDraftAsync`). È una scelta di prodotto da confermare,
visto che il viewer dell'audit è fra i lavori aperti (E5).

### A7 ✅ Nuovo pacchetto di deploy — fatto il 9 agosto 2026
`artifacts/publish/vipi-linux-x64-mariadb-20260809.zip` — **47,8 MB**, self-contained **net8**
(sha256 `F17A0512E2D37AF7…`), con dentro `LEGGIMI-DEPLOY.md`, `appsettings.Production.json`,
`deploy/vipi.service` e `deploy/nginx-vipi.conf`. Rigenerato dopo la correzione C4, così il binario
consegnato contiene anche quella.

ℹ️ Il `LEGGIMI-DEPLOY.md` ora vive in **`deploy/atc-ivao/`**, cioè nel repository, e viene copiato nel
pacchetto: prima esisteva solo dentro `artifacts/`, che è gitignorata, e un `dotnet publish` che ripulisce
la cartella se lo portava via.

`LEGGIMI-DEPLOY.md` **riscritto**: diceva MySQL 8.4.9, provider Oracle e collation `utf8mb4_0900_as_cs` —
tre cose false su MariaDB. Ora dice MariaDB/Pomelo/`uca1400_as_cs`, aggiunge il passo «carica il `.sql`»
(che prima non esisteva: il documento dava per scontato un database vuoto), e chiede esplicitamente le due
impostazioni del loro server, `max_allowed_packet` ≥ 4 MB e `sql_mode`.

⚠️ **Dire a Ivao.It che il pacchetto del 5 agosto non funzionerà mai su quel server**: è compilato contro
un provider che non supporta MariaDB. Lo zip vecchio è ancora in `artifacts/publish/`: va tolto di mezzo
prima della consegna, o si consegna quello sbagliato.

⚠️ Il pacchetto **non è mai stato eseguito su Linux** — è compilato in modo incrociato da Windows. Il primo
avvio da loro è anche la prima prova su quel sistema, ed è scritto nel LEGGIMI.

### A8 ✅ Riscritte le decisioni che dicevano il falso — 9 agosto 2026
- **ADR-0007 §D4-ter** scritto: MariaDB 11.4.10, Pomelo 8.0.3, host net8, collation `utf8mb4_uca1400_as_cs`,
  migrazioni in assembly dedicato. Dice anche **perché** §D4-bis è caduta (era costruita su «il server è
  MySQL 8.0+», che era di seconda mano) e **quanto costa** questa scelta: schema doppio, ritorno del
  multi-target nei test, cache-busting degradato. §D4-bis è marcata superata in testa, com'era già §D4.
- **Piano MySQL**: avviso in cima che dichiara il documento superato, dice cosa resta valido (analisi dei
  rischi e catena del travaso §S8) e rimanda a questo elenco per lo stato reale.
- `guide/config.md`: tabella dei provider corretta, più l'avviso che `utf8mb4_0900_as_cs` su MariaDB **non
  esiste** e il requisito `max_allowed_packet` ≥ 4 MB.
- `guide/integration.md`: il ramo `MySql` esiste **solo su net8** (Pomelo); e non è più vero che il net8 non
  sia coperto dai test.
- `HANDOFF.md`: blocco di testa riscritto sullo stato verificato.
- Memorie aggiornate: `mysql-embedding-plan`, `multitarget-net8-embedding`, `deploy-hosting-options`.

### A9 🔴 Domande e conferme da Ivao.It
Messaggio pronto in appendice al piano, **da aggiornare** perché parla ancora di MySQL. Aperte:
- **Come raggiungiamo il database** (SSH? phpMyAdmin? IP autorizzato?) — decide se il travaso lo facciamo
  noi o gli consegniamo un file, e con quale limite di dimensione.
- **`sql_mode`** del server: strict o no (vedi A6). Da noi è `STRICT_TRANS_TABLES`; fuori da strict un CAST
  non numerico dà warning e 0 invece di lanciare, e quella classe di bug torna silenziosa.
- **`max_allowed_packet`**, che nessuno aveva ancora chiesto: le immagini dei blocchi sono `longblob` e
  viaggiano in un solo pacchetto. L'app taglia a **3 MB per immagine** (`MediaOptions.MaxUploadBytes`, 25 MB
  per documento), quindi basta che il loro valore sia **≥ 4 MB** — il default MariaDB è 16 MB, ma su hosting
  condiviso capita 1 MB, e allora gli upload sopra il mega fallirebbero al primo INSERT.
- **I privilegi dell'utente `itivao_atc`**, in dettaglio: la migrazione iniziale apre con
  `ALTER DATABASE CHARACTER SET utf8mb4;` (lo emette Pomelo, non noi). Con `GRANT ALL ON itivao_atc.*`
  passa — verificato in locale il 6 agosto — ma con una lista ritagliata è la prima riga che si pianta.
- ⚠️ **Chi fa il backup di `itivao_atc`, e con che frequenza.** Non è una conferma di cortesia: al 14 agosto
  2026 **nessuno sa rispondere**, e finché la risposta è «non lo so» va trattato come «il backup non esiste».
  Il database tiene **tutto** lo stato dell'app, immagini comprese (ADR-0007): non c'è una seconda copia
  altrove da cui ricostruire. La `ReleaseRetention` **non** è un backup — pota di proposito. Se la risposta
  tarda, il paracadute è uno script `mariadb-dump` in cron, da concordare con loro.
- ⚠️ **Nei backup vanno due cose, non una**: il database e la **cartella delle chiavi** Data Protection,
  `/var/lib/vipi/keys` (§G). Perderla slogga tutti una volta sola, ma è un file solo ed è banale includerlo.
- Sulla macchina: **WebSocket** sul reverse proxy (senza, Blazor Server apre le pagine e resta muto),
  header inoltrati, supervisione del processo, e il **percorso persistente** per il key-ring (che dal 14
  agosto non sta più sul database: §G).

### A10 🔴 Redirect OIDC sul portale IVAO
`https://atc.it.ivao.aero/signin-oidc` e `/signout-callback-oidc`, esatti. E recuperare
**`VipiAuth:ClientSecret`**, l'unico dei quattro segreti che non è nei user-secrets locali — anche se il
flusso funziona senza, in modalità client pubblico con PKCE (verificato il 5 agosto).

---

## B. Branch non fusi — decisioni, non lavoro

### B7 ✅ FUSO — `catalogo-punti-suggerimenti`, fuso in `main` la sera del 22 agosto 2026
Sette commit più il merge `2b4480d`, ramo cancellato (locale e origin). **Non resta nessun ramo con lavoro fuori.**
Suite **1 677** verde su net8, `Release --no-incremental` **0 avvisi**, verifica live guidata con Edge su
editor aeroporto (LIBD, LIRF), accordi (LIBB) e sorgenti.

Contenuto e decisioni: [`feature/2026-08-22-catalogo-punti-suggerimenti.md`](feature/2026-08-22-catalogo-punti-suggerimenti.md).
In breve: il catalogo di fix/VOR/NDB diventa una porta (`INavaidSource`), i campi punto suggeriscono e segnano
i nomi inesistenti, gli alias dei fix diventano visibili e cancellabili.

**Non ha toccato la coda del cutover:** niente entità nuove, **niente migrazioni**
— il catalogo vive in memoria. È la ragione per cui è stato progettato così: il deploy è fermo in attesa della
conversione MariaDB (§A) e una tabella in più avrebbe allungato quella coda.

⚠️ Modifiche fuori dal proprio perimetro, da sapere leggendo codice più vecchio: la classe CSS `.cop-unknown` è stata
**rinominata** in `.nav-unknown-txt` (serve a due cose ora), e `AuroraSectorfileParser.ParseNavaids` ha
**cambiato firma** (restituisce `NavaidCatalog`, prende anche il file NDB). Entrambe propagate nello stesso
giro — nessun chiamante resta indietro.


### B5 ✅ CHIUSA — il doc 13 era **già in `main`**, e nessuno se n'era accorto
⚠️ **Non c'era nessuna decisione da prendere.** Scoperto il 22 agosto cancellando i rami fusi: la punta di
`refactor/13-tre-documenti` (`90aa917`, 11 agosto) risultava **antenata di `main`**, cioè zero commit fuori.
Il lavoro è entrato il **15 agosto**, trasportato dal merge di `feature/trasferimenti-acc-app`, che ne
condivideva la storia — e la voce qui è rimasta a chiedere un ok per qualcosa di già fatto.

Verificato sul codice, non sul grafo: `EfDocumentMaintenance` in `main` porta `ReconcilePurposeKeyAsync`,
`MinimaKey` e le sezioni di catalogo mancanti — le tre riconciliazioni one-shot descritte qui sotto. E il
[doc 13](refactor/13-audit-tre-documenti.md) si dichiara **CHIUSO** in testa da allora.

⚠️ **La lezione**: un ramo che il grafo dice a zero commit non è «da fondere», è **già dentro** — e un
elenco di lavori aperti può restare vero a metà per una settimana senza che niente lo contraddica. Il ramo
è stato cancellato (locale e origin) insieme agli altri otto a zero.

<details><summary>La scheda di allora, per memoria</summary>

25 commit, suite **2111** verde su entrambi i TFM, **verifica live fatta** sui tre documenti (copia del
`vipi.db` reale). È il [doc 13](refactor/13-audit-tre-documenti.md): audit di vIPI ACC, vIPI APP e vLOA, nato
dall'osservazione che «la sezione delle versioni dovrebbe essere la stessa per tutti e tre».

Perché conviene farlo entrare: due difetti **uscivano dal documento** — la pagina APP pubblica derivava le
configurazioni dalla versione di lavoro (bozza in pubblico, contro l'invariante del doc 10), e ricerca e
«Cosa è cambiato» indicizzavano documenti nascosti, **sezioni** nascoste e contenuto senza release effettiva.
Il resto è uniformità: catalogo fonte unica anche di «chi rende il corpo» e «obbligatoria», ciclo AIRAC del
documento invece che di oggi, pannello release uguale nei quattro editor.

⚠️ **«Build senza errori» è stato falso fino all'11 agosto 2026.** Il ramo portava 14 chiavi duplicate nei
`.resx`: con `-warnaserror` la CI dava **28 errori**, e nessuno l'aveva visto perché il ramo non era mai
stato spinto e la suite locale resta verde (`dotnet test` non usa quel flag). Corretto, con tre guardie che
leggono i `.resx` dal disco.

Da sapere prima del merge: al primo avvio girano **tre riconciliazioni one-shot** (chiavi delle direzioni
vLOA + «Purpose», placeholder vuoti di «minima», sezioni di catalogo mancanti su APP/vLOA). Sul DB di
sviluppo hanno toccato 15 sezioni e rimosso 18 blocchi. Sono idempotenti e non toccano le release già
pubblicate.

**Decisione da prendere:** merge in `main` (serve l'ok esplicito, come per il doc 10) e push.

</details>

### B6 ✅ FUSA — `feature/trasferimenti-acc-app`, fusa in `main` il 15 agosto 2026
72 commit, suite **2403** verde su entrambi i TFM, `Release --no-incremental` 0 warning, verifica live su
copia del `vipi.db` reale in **tutti e sei** i giri (ventuno difetti trovati proprio lì, quasi nessuno
visibile alla suite). Contenuto in **E6**; sei schede in `docs/feature/`, l'ultima è
`2026-08-12-editor-trasferimenti-rifiniture.md`. Fusa perché la consegna a Ivao.It parte da `main` e il
committente ha chiesto che partisse **con tutto dentro**.

⚠️ Il ramo portava anche il proprio giro dell'audit database (§G) nella forma nata lì: le due migrazioni
avevano **lo stesso identificativo** di quelle rigenerate su `main`, apposta. Al merge si sono scontrate sullo
stesso percorso, e si è tenuta **la copia del ramo trasferimenti**, non quella di `main`: è l'unica il cui
`Designer` descrive il modello fuso (l'altra non conosceva le colonne dei trasferimenti). L'unica differenza
nel corpo delle due migrazioni era il commento.

⚠️ La **PR #13 resta aperta con un corpo vecchio** (descrive il primo giro, «autorizzazione e trasferimento
sono due eventi», quando i giri sono diventati sei). Va chiusa a mano dopo il push di `main`.

### B1 ✅ FUSA — `feature/aree-speciali-hardening`, verificata il 6 agosto e fusa il 7 agosto 2026
**Fusa in `main` in fast-forward** (21 commit, `bbbbf2b` → `7557ec4`) e da lì nel ramo del cutover
`feat/persistenza-mysql`. Il ramo può essere cancellato dopo il push di `main`. Sotto, l'esito della
verifica live che ha sbloccato la decisione.
I quattro punti sono stati guidati con la skill `verifica-live` su una copia del `vipi.db` reale, con import
veri contro l'API IVAO. Esito per esteso nella carta,
[`feature/2026-08-03-aree-regolamentate-hardening.md`](feature/2026-08-03-aree-regolamentate-hardening.md).

- **Riconciliazione al boot**: 993 → 230 aree, zero orfane, colonna `CenterId` sparita. Come previsto.
- **1. Interruttore** ✅ con la categoria spenta l'import aggiorna ACC e settori e **lascia le aree intatte**
  (230 aree / 247 legami, conteggi per ACC identici); a video «❄ Congelate». E dura **24 secondi** contro i
  minuti dell'import pieno: la fetch non parte davvero.
- **2. Dangling** ✅ cancellata a mano l'area `1131` citata dalla vIPI Brindisi: «⚠ 1131 non più disponibile»
  nell'editor, rilievo in `/services/vsop/admin/diagnostics`, `/vsop/health` a **Degraded**.
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

### B2 ✅ FUSO — `feature/aurora-bridge`, dentro B1, endpoint spento
È entrato in produzione **come codice, non come superficie**: `AuroraBridge:Enabled` nasce `false` e la
rotta non si registra affatto. Accenderlo resta una decisione separata, e il giorno in cui si accende la
prima sessione col tool va guidata — non è mai stato esercitato contro un host remoto vero.
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

### B4 ✅ DECISO il 7 agosto 2026 — in produzione va `main` + B1
Con B2 dentro, perché il ramo del bridge sta per intero in quello delle aree. Eseguito: B1 fusa in `main`,
`main` fusa in `feat/persistenza-mysql`, suite verde su entrambi i TFM.

**Cosa resta di questa decisione, in ordine:**
1. 🔴 **`git push origin main`** — 21 commit locali non ancora sul remoto. Fa partire il redeploy Render.
2. 🟡 **Dopo il deploy**: al primo boot Neon riconcilia l'archivio (993 → 230 legami, aree estere spente);
   poi premere **«Importa da sorgente»**, perché il backfill recupera un solo legame per area.
3. 🟡 **Rifare il travaso di A3** su quei dati: il `.sql` del 6 agosto non vale più.
4. 🟢 Cancellare i rami `feature/aree-speciali-hardening` e `feature/aurora-bridge`, ormai contenuti in `main`.

---

## C. Debito noto — non urgente, ma non dimenticabile

### C1 ✅ Il percorso Npgsql di `ISchemaDriftProbe` è stato eseguito — 9 agosto 2026
Host locale puntato a **Neon**, cioè l'unico modo di eseguirlo (in locale non c'è un Postgres). Non ci si è
fermati al «non segnala niente», che da solo non distingue *pulito* da *mai eseguito*: si è **introdotto un
drift finto** — una colonna `Accs.ZzSondaDrift` — e la sonda l'ha vista.

- a schema pulito: `/services/vsop/admin/diagnostics` «nessuna incongruenza», `/vsop/health` **Healthy**;
- con la colonna estranea: rilievo **«Colonna orfana nello schema — `Accs.ZzSondaDrift`»**, col messaggio
  che conta («se è una rinomina, i dati sono ancora QUI e la colonna nuova è vuota»), e `/vsop/health` a
  **Degraded**;
- rimossa la colonna: di nuovo pulito e Healthy. Neon è tornato esattamente com'era.

Nessun falso positivo di tipo sulle 39 tabelle reali, quindi la mappa alias di `SchemaDriftAnalyzer.Canonical`
non è stata toccata.

### C2 ✅ CHIUSA il 9 agosto 2026 — `ImportSids` non è spento da nessuna parte
Il timore era: la migration dell'8 luglio creò la colonna con `defaultValue: false` e il reconciler la
backfillava a `false`, quindi su un database dove la riga `ImportPolicies` **esisteva già** la categoria
sarebbe nata spenta, in modo indistinguibile da una scelta dell'admin.

**Quella riga non esiste.** Su Neon `ImportPolicies` ha **zero righe**, e senza riga
`EfImportPolicyStore.GetAsync` torna `ImportPolicySnapshot.AllImported` — tutto importato, SID comprese. Il
`.sql` del travaso porta la stessa situazione in produzione, quindi il trabocchetto non si materializza né
di qua né di là. Verificato leggendo i dati veri, non l'interfaccia.

⚠️ Resta vera la regola generale, ed è quella che vale la pena ricordare: un `bool NOT NULL` nuovo nasce
`false` ovunque, migration e reconciler compresi, ed è veleno per un flag **opt-out**. Memoria:
[[bool-column-default-trap]]. Dal branch delle aree il default sta nel modello (`HasDefaultValue`) e il
reconciler lo legge.

### C3 🟡 ADR-0007 punto (b): migrazioni Postgres versionate — **rischio accettato, con un rilevatore che ora funziona**
Il `PostgresSchemaReconciler` copre **solo le aggiunte di colonna**: il primo rename, drop o cambio di tipo
su Neon va applicato a mano. Resta vero.

**Perché non si costruisce adesso il terzo set di migrazioni.** Costerebbe **emettere ogni cambio di schema
tre volte** (SQLite, MySQL, Postgres) per sempre — e ADR-0007 §D4-ter dichiara quel costo già pesante a
due. Lo si spenderebbe per un ambiente che è **di prova** e che, a cutover riuscito, è candidato a essere
ritirato: la decisione su Neon è ancora aperta e va presa prima, non dopo.

**Cosa rende accettabile aspettare, e non era vero prima del 9 agosto:** il guasto temuto — una rinomina
che lascia i dati nella colonna vecchia mentre l'app legge la nuova, vuota, **senza lanciare niente** — ora
ha un rilevatore **provato sul campo** (C1): compare in `/services/vsop/admin/diagnostics` e porta `/vsop/health` a
Degraded. Il rischio passa da *silenzioso* a *rumoroso*, che è la differenza che conta.

**Cosa lo riaprirebbe, e allora va costruito:** se Neon smettesse di essere un ambiente di prova (dati che
non si possono ricreare), oppure se servisse un rename/drop/cambio-tipo — a quel punto il baseline si
genera dal modello **mentre la sonda dice che lo schema combacia**, che è esattamente lo stato verificato
oggi, e si timbra come applicato. È la stessa ricetta già usata per MariaDB.

#### C3-bis 🟡 Decisione su Neon — riesaminata il 9 agosto 2026, esito: **tenerlo fino a dopo il cutover**
«Ritirare Neon» e «chiudere il sito di prova» sono la stessa cosa: il servizio Render è senza stato, i dati
stanno lì. Le alternative non reggono — un MySQL gestito gratuito non parla `utf8mb4_uca1400_as_cs` (un
ambiente di prova che mente è peggio di nessun ambiente), e SQLite su disco effimero perde i dati a ogni
redeploy.

**Cosa è cambiato, e non basta a decidere adesso.** Come banco di prova del *database* Neon **non serve
più**: la MariaDB locale coi dati veri riproduce la produzione meglio, ed è lei ad aver trovato i tre bug di
A6. E C1 ha alleggerito C3, rendendo rumoroso un guasto che era silenzioso. Ma ciò che Neon dà **non è il
database**: è l'unico ambiente **hostato** — reverse proxy, WebSocket, TLS, redirect OIDC, key-ring senza
disco persistente — cioè proprio quello che A9/A10 devono ancora chiarire con loro. E il `.sql` del cutover
nasce da lì: fino al passaggio serve per definizione.

**Quando decidere, e con quale prova.** Dopo il cutover e **un ciclo AIRAC pubblicato dal server nuovo**
senza sorprese. La domanda diventa allora osservabile invece che opinabile: *in quelle settimane Neon è
stato aperto anche una sola volta?*
- **No** → si chiude. Spariscono C1, C3 e un intero dialetto: `PostgresSchemaReconciler`,
  `PostgresSchemaDriftProbe`, il ramo Postgres di `DataProtectionSchema` e `DependencyInjection`,
  `--from-postgres`/`--to-postgres` di `Vipi.DbSeed`. Da tre dialetti a due.
- **Sì** → è un ambiente che conta davvero, e **C3 va costruita**, non più rimandata.

⚠️ Indipendente dalla decisione: la password di Neon è passata in chat il 9 agosto 2026 e **va ruotata**.

### C4 ✅ Cache-busting rimesso a posto — 9 agosto 2026, senza aspettare EF Core 10
Era accettato come costo di net8: niente `@Assets[...]`, quindi un'unica impronta per tutti gli asset (il
MVID dell'assembly), e a ogni deploy il browser riscaricava **tutto**, anche i file identici byte per byte.

Ora `AssetVersion` calcola lo **SHA-256 del contenuto di ogni file**, letto dallo **stesso provider** che poi
lo serve — così le due cose non possono divergere — e ne mette 8 caratteri nell'URL. Un asset immutato
conserva il proprio URL e resta valido in cache; cambia solo ciò che è cambiato davvero. Le impronte si
calcolano una volta per percorso e restano in memoria.

Il ripiego è deliberato: se un file non si risolve si torna al MVID, **non** a un URL nudo — invalidare
troppo è innocuo, invalidare troppo poco lascia un CSS vecchio in cache dopo un aggiornamento.

Guardia: `Ogni_asset_ha_la_propria_impronta_di_contenuto` in `Vipi.E2E.Tests` guarda la pagina servita e
pretende impronte **diverse** fra asset diversi — fallisce sia se torna l'impronta unica sia se i file non
si risolvono e si sta usando il ripiego. Vista fallire sull'implementazione precedente.

### C5 ✅ Audit 22 luglio — le due voci residue sono risolte, 9 agosto 2026
`history/audit-2026-07-22-criticita-full-stack.md`. Fasi 1 e 2 erano già eseguite.

**A2 (scala Blazor) — deciso, non costruito.** La scala attesa è **una sola istanza**: un processo dietro
`proxy_pass` verso un solo indirizzo. Con un'istanza il backplane non serve a nulla, e aggiungerlo ora
sarebbe infrastruttura da mantenere per un problema che non abbiamo. La decisione ha però un vincolo che
va **detto a chi amministra la macchina**, perché il guasto è vistoso e la causa no: Blazor Server tiene lo
stato dell'utente nel processo che ha aperto il circuito, quindi **due processi dietro un bilanciatore
fanno cadere le pagine in riconnessione continua**. Se un domani serve scalare, prima il backplane, poi il
secondo processo. Scritto in `deploy/atc-ivao/nginx-vipi.conf`, dove lo legge chi tocca il proxy.

⚠️ **Trovato mentre si verificava questo: `nginx-vipi.conf` conteneva `proxy_read_send_timeout`, che non è
una direttiva nginx.** Non è un dettaglio di stile: nginx rifiuta di avviarsi con «unknown directive», e la
consegna si sarebbe fermata lì. Rimossa — le due valide (`proxy_read_timeout`, `proxy_send_timeout`) erano
già presenti sotto.

**D1 (provisioning) — non è più una voce di debito, è una dipendenza da loro.** La parte di codice è chiusa
dal 22 luglio (`ProductionIdentityGuard` fa hard-fail se l'identità di sviluppo è attiva fuori da
Development, con test sul percorso di produzione). Quel che resta — montare i claim e gli **staff-code IVAO
reali** — vive già come **A9/A10** (segreti e redirect) ed **E4** (conferma dei codici staff): tenerlo anche
qui era contarlo due volte.

---

## D. ✅ Verifiche live arretrate — **sezione chiusa il 9 agosto 2026**

Erano lavori già scritti e testati che nessuno aveva mai **guidato**. Tutte rifatte su MariaDB coi dati
veri, non su un database di comodo.

- ✅ **Aree regolamentate** — 6 agosto: esito in B1.
- ✅ **Settori esteri aggiunti a mano.** In `/services/vsop/admin/neighbours`, su coppia confermata, provati i tre
  esiti che contano: **aggiunta** di `LGRP_APP` a LGGG → verificato su IVAO e materializzato con dati veri
  (*Rodos Approach*, 127.250, poligono di 3378 caratteri), **riproiettato** come `Sector` attivo e presente
  nel **picker del ricevente** (`LGRP_APP LGGG`); **ri-aggiunta** dello stesso → «already present», non un
  errore; **dirottamento** di `LGKR_APP` su LAAA → rifiutato («appartiene già all'ACC LGGG»), e soprattutto
  **nessuna riga fantasma** sotto LAAA.
  ⚠️ Il dropdown del picker è governato da `@onfocus`: un click di automazione lo chiude. Va aperto con
  `page.focus` e riempito da tastiera, senza altri click — altrimenti sembra vuoto quando non lo è.
- ✅ **Coordinamenti/sorvoli rielaborati.** Sulla vIPI ACC di LIBB: CoP `ALL` → «su tutti i punti», `ALL to
  GR` → «su tutti i punti verso GR», **nessuna riga col vecchio «su —»**; sorvoli senza aeroporto presenti;
  parità resa («*stabile a livello 260 **pari** su tutti i punti verso GR*»). Sulla vLOA LIBB↔LGGG:
  coordinamenti in stile ACC e frasi in inglese. **Lookup IVAO**: scritto `LFPG`, il tasto compare e riempie
  «Paris Charles de Gaulle» — e l'aeroporto **non entra nel catalogo** (92 prima, 92 dopo), che era il
  vincolo.
  ⚠️ **Difetto trovato qui e corretto**: in inglese la parità era attaccata con l'ordine italiano —
  «at level 260 even», «for a level odd». Ora l'ordine sta nel template (`WithParity`, `ForLevelParity`):
  «at level 260 (even)», «for an odd level». Un test **fotografava il difetto** invece di impedirlo ed è
  stato corretto: è la ragione per cui non era mai emerso.
- ✅ **Retention pubblicazione.** Non solo conteggi: fatta scattare. Una pubblicazione in più su LIBB →
  `Superseded` 12→13 e versioni archiviate **ferme a 3** (il cap regge, era l'off-by-one del 21 luglio).
  Poi tre release retrodatate oltre soglia e riavvio: il boot sweep ne ha potate **esattamente tre**
  (53→50 release, `Effective` intatte), e un secondo riavvio è stato **no-op**.

---

## E. Funzionalità aperte (da `HANDOFF.md` §5)

Ordinate per valore, come lì.

### E1 ✅ Live IVAO — **chiusa il 9 agosto 2026**: tre voci su quattro erano già morte
L'elenco veniva da prima della riscrittura della vista live (doc 12, 31 luglio) e non era stato ricontrollato.

- ~~**Identità «P»** legata al callsign connesso~~ — **già fatto**: `/services/vsop/live` *è* la tua postazione, presa
  dalla connessione IVAO. Il selettore manuale non esiste più, e nemmeno la pagina Ridotta che lo ospitava
  (rimossa al Round 12).
- ~~**Endpoint membri divisione** da confermare~~ — **confermato da tempo, in negativo**:
  `/v2/divisions/{id}/members` risponde **404** e `/users` dà 500 col token app. È la ragione per cui esiste
  il roster costruito dai login ([[staff-roster-design]]). Non c'era niente da confermare, solo da cancellare.
- ~~Estendere **`live=true`** a vIPI aeroporto e vLOA~~ — **obsoleta**: quel parametro non esiste più. La
  vista live è una pagina unificata legata all'**ente**, non un livello di dettaglio dei documenti.
- **Mapping token-handler → callsign: valutato, e la tabella esplicita NON serve.** L'euristica di
  `TransferOnlineResolver` accetta match esatto, segmento e sottostringa ≥4. Provata sui **313 callsign
  reali**: le coppie che collidono sono **zero**, e non per caso — nessun callsign del catalogo è privo di
  underscore (quindi la regola «segmento» non può scattare) e nessuno è contenuto in un altro. Nella pratica
  l'euristica **si riduce al match esatto**: una tabella di mapping sarebbe manutenzione in più a parità di
  comportamento.

  La scelta è però resa **revocabile da sola**: nuova regola nella diagnostica, **«Callsign ambiguo
  (risoluzione live)»**, che riusa il resolver invece di ricopiarne le regole — se l'euristica cambia, la
  diagnosi cambia con lei. Il giorno che nasce un settore che collide si vede in `/services/vsop/admin/diagnostics`
  invece che in frequenza. Verificata sui dati veri: nessun rilievo, nessun rumore.

### E2 Dati reali che mancano
- ✅ ~~**Shape reali delle TWR** dal sectorfile GitHub~~ — **già fatto e verificato il 9 agosto 2026.**
  `GithubTowerShapeService` applica i poligoni di `DYNAMIC_SEC/twrs.tfl` **prima** del cerchio sintetico ed è
  agganciato all'import automatico più un bottone nell'editor. Sui dati veri: **68 TWR su 84 hanno un
  poligono reale**, 16 restano col cerchio. E i 16 non sono un buco: scaricato `twrs.tfl` e confrontato,
  **nessuno dei 16 callsign è presente nel file** — il cerchio copre esattamente le torri che nemmeno la
  sorgente ha.
- ❌ **Minime MVA da GitHub — scartato il 9 agosto 2026 (decisione del committente).** L'idea era riusare il
  pattern delle SID (parser, import gated, pubblicazione differita al ciclo successivo). Non si fa, e non per
  il nostro lato: **nel sectorfile la struttura dei file MVA non dice a quale settore appartiene un'area**.
  Un import dovrebbe indovinare quell'associazione, e una minima di vettoramento attribuita al settore
  sbagliato è peggio di una minima assente — è un dato operativo che qualcuno userebbe.

  Se un giorno le MVA serviranno davvero, la strada non è l'import ma quella **editoriale**: una sezione
  come le altre, compilata dallo staff e pubblicata col documento, dove l'associazione al settore la
  dichiara una persona invece di un'euristica. A quel punto è lavoro di editor, non di parser.
- 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso. Si sistemano
  dalla pagina: il filtro «solo da agganciare» li raccoglie.
- La SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft», quota implausibile. Da
  correggere nell'editor: è un dato, non un bug.
- Il CoP **`BESIV`** dell'accordo `LIBB_ES_CTR ⇄ LDZO_CTR` (sorvoli, verso LDZO→LIBB) **non esiste nel
  sectorfile**; a una lettera di distanza c'è `BEKIV`. Lo segnala da solo l'editor degli accordi dal giro del
  22 agosto ([feature/2026-08-22-catalogo-punti-suggerimenti.md](feature/2026-08-22-catalogo-punti-suggerimenti.md)),
  ma **correggerlo è una decisione editoriale** — può essere un typo o un punto estero non elencato — e la
  prende chi conosce l'accordo. Finché resta così, compare anche fra i «punti presenti in un verso solo» del
  cruscotto delle lacune, dove sembra un'asimmetria dell'archivio e non un errore di scrittura.
- Stessa cosa da rifare **sui CoP di produzione**: il conteggio (1 su 52) è del DB di sviluppo, che ha 52
  clausole. Aperta la pagina degli accordi, i nomi fuori catalogo si vedono sottolineati senza cercarli.

### E3 🟡 Fonte unica — «presidenza aeroporto» ✅ fatta il 9 agosto 2026; resta il distacco dai `Sector`
Documenti e AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi: **quella parte
resta aperta** ed è il grosso del follow-up del Round 20. La **risalita**, che era l'altra metà, è fatta.

**`AirportPresidencyResolver`** (Application/Live, puro) risponde a «chi controlla questo aeroporto adesso»
nella forma scelta dal committente: le posizioni **sue** online dal gate in su (DEL → GND → TWR → APP), più
**chi copre il resto** risalendo la gerarchia, e UNICOM se non c'è nessuno dei due. La risposta non è una
sola perché non lo è la domanda: al gate serve il ground, in avvicinamento la torre.

⚠️ **La regola di confronto è quella dei trasferimenti, riusata e non riscritta** (`TransferOnlineResolver`).
È il punto che conta: due logiche di risalita affiancate darebbero, prima o poi, risposte diverse sullo
stesso settore — e la sentinella dei callsign ambigui in diagnostica vale già per entrambe.

**Sostituisce una risposta binaria che aveva due difetti.** La vista live diceva «delegato» se esisteva un
callsign online che *cominciava* con l'ICAO: non diceva **chi** chiamare, e contava anche l'**ATIS**, che è
una frequenza e non una posizione che controlla. Ora si parte dalle posizioni note dell'aeroporto, quindi
l'ATIS non entra.

**Fatto:** risolutore + 7 test (compresi il caso «solo il ground online, il resto lo copre chi sta sopra» e
l'avvicinamento dell'aeroporto che non deve comparire due volte), innestato nelle **chip aeroporto** della
vista live, che ora nel tooltip dicono chi presiede invece di una stringa fissa. Verificato a schermo:
`LIPA`/`LIPI` → «Nobody online: UNICOM». ℹ️ Il ramo positivo non era osservabile in quel momento — i tre
ATC realmente online (`LIEO_EW0_APP`, `LIMC_ANE_APP`, `LIME_TWR`) non toccano nessuno degli aeroporti
pubblicati — ed è coperto dai test.

**Innestato in tutti e tre i punti** (9 agosto 2026), con due modi diversi e deliberati:
- **chip della vista live** — il tooltip dice chi presiede;
- **`AirportQuickPanel`** — riga «Adesso» accanto a TA/TL/vento/piste. La presidenza **arriva come parametro**
  dalla pagina live, che l'ha già risolta per le chip: ricalcolarla dentro il pannello vorrebbe dire rifare le
  query a ogni tick del feed, e rischiare che due parti della stessa schermata dicano cose diverse;
- **viewer dell'aeroporto** `/services/vsop/{acc}/airports?icao=` — riga nel riepilogo, risolta da
  `IAirportPresidencyService` perché quella pagina sta fuori dalla vista live e non ha un contesto pronto.
  Risolta nel ciclo di vita, mai nel render.

⚠️ **Difetto corretto strada facendo:** il conteggio «ATC online» del viewer contava anche l'**ATIS**, che è
una frequenza registrata e non qualcuno che risponde — un aeroporto deserto poteva mostrare «1 online».

Verificato a schermo su tutti e tre: chip → «Nobody online: UNICOM», vista rapida → «ADESSO nessuno online —
UNICOM», viewer → «Now Nobody online: UNICOM» accanto al ciclo AIRAC.

### E4 🟡 Auth di produzione — ora si **vede**, e i codici veri dicono già qualcosa
I pattern admin (`^IT-DIR$`, `^LI[A-Z0-9]+-CH$`, …) erano ipotesi. Due contromisure, fatte il 9 agosto 2026:
- **scheda «Chi può editare»** in `/services/vsop/admin/diagnostics`: i pattern in vigore a confronto con i codici
  staff **realmente osservati ai login** (IVAO non espone l'elenco degli staffisti — `/members` è 404 — quindi
  il roster costruito dagli accessi è l'unica fonte possibile);
- **rilievo grave** nel report di consistenza (quindi anche in `/vsop/health`) quando **nessuno** degli
  staffisti conosciuti risulta admin. Non scatta a roster vuoto: su un'installazione nuova nessuno ha ancora
  fatto login, e segnalarlo lì sarebbe solo rumore.

I pattern sono ora una **fonte sola** (`AdminStaffCodes`), condivisa fra chi decide e chi diagnostica: una
diagnosi che se li ricalcolasse potrebbe dire «tutto a posto» mentre l'autorizzazione ne usa altri.

**Cosa dicono i dati veri** (roster attuale, 5 staffisti):

| VID | codici osservati | vale admin |
|---|---|---|
| 201143 | `IT-AOC`, **`IT-SOC`** | `IT-AOC` |
| 286571 | **`IT-T01`** | — |
| 516571 | **`IT-FOC`** | — |
| 657465 | `IT-ADIR`, **`IT-FOAC`** | `IT-ADIR` |
| 704798 | `IT-AOA1`, **`IT-T03`** | `IT-AOA1` |

Quindi: **il formato è confermato** (`IT-XXX`), tre su cinque sono admin, e ci sono **quattro codici veri non
coperti** — `IT-SOC`, `IT-T01`, `IT-FOC`, `IT-FOAC`. Se debbano valere admin **non è una domanda tecnica**:
un coordinatore training o un flight-ops devono poter editare le vIPI? È la decisione che resta a voi.
Nessun codice chief `{ACC}-CH` è ancora comparso: quel pattern resta **non verificato**.

⚠️ **Trappola di configurazione trovata qui:** dalle liste della sezione `Division` si può solo **allargare**
l'insieme degli admin, mai restringerlo — il binder *aggiunge* ai default invece di sostituirli (era anche
la causa dei prefissi ICAO duplicati, ora deduplicati). Per restringere davvero si usa
`Auth:AdminStaffCodes`, che sostituisce tutto. Su un permesso di questo peso, la differenza conta.

### E5 Copertura e rifiniture — due voci chiuse il 9 agosto 2026
- ✅ **«Scarta bozza»** — fatto. Elimina la versione `Draft` col suo contenuto, scrive l'audit
  (`AuditAction.Discard`, nuovo) e libera il lock. La cancellazione di una versione esisteva già dentro la
  potatura: **estratta e riusata**, non ricopiata. Due regole nel servizio: si scarta solo una bozza, e solo
  se c'è una versione a cui tornare (su un documento mai pubblicato la bozza *è* il documento). Verificato
  sull'app: bozze 11 → 10, audit con documento e numero di versione, zero sezioni o blocchi orfani.
- ✅ ~~Viewer dell'**audit log**~~ — **esisteva già** (`AuditPage`, rotta `/services/vsop/admin/audit`): voce stantia.
  ⚠️ Resta la domanda aperta di prodotto: pubblicare una **release** non scrive audit (lo fa solo la
  promozione di una bozza), quindi il viewer non mostra quelle pubblicazioni.
- 🟢 **Test property-based sull'AoR** — non c'è ancora alcuna libreria property-based nel progetto.
- 🟡 **Editor visuale delle mappe AoR** — è una feature di interazione, non una rifinitura: va disegnata
  con chi la userà prima di essere scritta.

### E6 ✅ Trasferimenti ACC↔APP — chiuso l'11-12 agosto 2026, **in `main` dal 15 agosto** (B6)
Il modello descriveva **un evento con un livello**: basta per un accordo ACC↔ACC, non per un ACC→APP —
«autorizzo a FL160 via CHI, trasferisco al confine dell'AoR passando FL110» non era esprimibile. Sei giri
sullo stesso ramo, ognuno con la propria scheda in `docs/feature/`: due eventi separati con velocità e punto
di trasferimento propri; il gruppo di varianti diventato un **outline** (alternative pari grado, eccezioni
annidate); la pagina rifatta col pattern del progetto, poi a **tre colonne** con vista a elenco, stato in URL
e scrittura dentro la tabella; infine le rifiniture d'uso (costo per gesto da 8 query a 1, tastiera nei
picker, annulla che rimette l'outline, modifica in blocco).

⚠️ **Resta da fare dai colleghi, non dal codice:** le **15 righe** con ricevente APP che non dicono ancora
dove avviene il trasferimento vanno riviste **a mano** — il loro livello può voler dire «autorizzato» o «al
trasferimento», e solo chi le ha scritte lo sa. Le elenca il **cruscotto delle lacune** in
`/services/vsop/admin/transfers` (genere «da rivedere»). ⚠️ Il numero va **rimisurato sulla produzione MariaDB**: 15
è il conteggio sul DB di sviluppo.

### E6-bis 🟡 Accordi di coordinamento — un accordo per COPPIA, il traffico nelle sezioni (in `main` dal 18 ago 2026)
Carte, in ordine di vigore:
[`feature/2026-08-18-accordi-a-sezioni.md`](feature/2026-08-18-accordi-a-sezioni.md) **(il modello di adesso)** ·
[`feature/2026-08-16-accordi-di-coordinamento.md`](feature/2026-08-16-accordi-di-coordinamento.md) ·
[`feature/2026-08-17-editor-accordi-per-relazione.md`](feature/2026-08-17-editor-accordi-per-relazione.md).
Schema `spec/modello-dati.md` **§9.25-bis**; area `refactor/07-trasferimenti.md` **§11**.
**Per riprendere da freddo**: [`history/handoff-accordi-coordinamento.md`](history/handoff-accordi-coordinamento.md).

Tre giri, e ognuno ha tolto un asse del modello precedente. **Ferragosto**: `TransferFlow`+`TransferPoint`
lasciano il posto all'**accordo** fra due parti (droppate il 17 con `DropLegacyTransferTables`; l'ultima copia di
quei dati nella forma originale è `tests/Vipi.Application.Tests/Fixtures/real-flows.tsv`). **17 agosto**:
l'editor, che aveva ancora l'albero sul lato B e il verso come interruttore. **18 agosto**: l'accordo diventa la
**relazione fra due enti** — uno solo per coppia, un ente per lato — e il traffico scende nelle **sezioni**.

Le misure che hanno deciso il terzo giro, sul `vipi.db` vero: **40 accordi stavano in 16 coppie** (la sola
`LGGG ⇄ LIBB` ne teneva otto); il **verso** si esprimeva *orientando* l'accordo — 60 clausole su 60 `AtoB` —
quindi i due sensi finivano in accordi diversi; e **nessun accordo aveva più di un ente per lato**.

Conversione in tre passi — migrazione additiva → `tools/Vipi.AgreementsToSections` → migrazione distruttiva:
**40 accordi / 60 clausole → 16 accordi / 38 sezioni / 60 clausole**, con `real-coordination.approved.txt`
**invariato carattere per carattere**.

**Cosa resta, ed è il motivo per cui la voce è 🟡:**
1. ✅ **Verifica live fatta** (porta 5035, copia del DB convertita). Ha confermato albero a due livelli, ordine
   imposto, verso proposto dall'aeroporto, blocco fantasma del reciproco, gemelle e deep-link — e ha trovato
   **tre difetti invisibili ai test**, corretti: l'avviso «scalo non coperto» che urlava su 3 sezioni su 8, lo
   stesso avviso che dalla testata mandava i tasti a capo, e cinque etichette rimaste sull'operazione vecchia.
2. ✅ **Conversione eseguita sul `vipi.db` di sviluppo** (18 agosto): 40 accordi / 60 clausole →
   **16 accordi / 38 sezioni / 60 clausole**, `integrity_check` ok, zero orfani, zero violazioni di FK, e
   l'app ci gira sopra (vIPI ACC LIBB: 37 tabelle, 76 righe di coordinamento).
   ⚠️ **Da qui in poi quel DB vuole questo ramo**: il codice di `main` cerca ancora `AgreementParties`. Il
   backup sta **fuori dal repo** in `../vipi.db.bak-pre-sezioni-20260818`, ed è l'unica copia dello stato
   precedente perché il `vipi.db` non è tracciato in git.
   ⚠️ Resta da fare sulla **MariaDB di produzione**, con `--mysql` e le migrazioni gemelle.
3. ✅ **Suite completa 2569 verdi** (E2E inclusi) e `dotnet build -c Release --no-incremental` a **0 warning**
   su due TFM.
4. **Le due asimmetrie** — `LGGG ⇄ LIBB` (BELIX, OLGAT) e `LDZO ⇄ LIBB` (sei punti da un lato solo) — le
   decidono i colleghi. Adesso stanno nello **stesso accordo**, una sezione sotto l'altra, quindi si vedono.
5. ✅ **I tre reciproci separati** (`#13/#32`, `#17/#28`, `#23/#38`) e la **relazione spezzata** (`#26/#27`) si
   sono chiusi da soli: i due versi della stessa coppia **sono** lo stesso accordo, e le gemelle le ha unite la
   conversione. Anche i **due accordi senza ricevente** sono spariti — il lato è ora una colonna `NOT NULL`.
6. ✅ **I tre difetti di `LevelFormatting` sono chiusi** (18 agosto): `— (dispari)` diventa `dispari` (21
   clausole su 60 lo mostravano — non era un caso limite), la parità non si appende più a un livello *speciale*
   che la dice già a parole, e la colonna del documento prende le parole dal **template** come già facevano
   handoff e velocità — così una vLOA inglese non scrive più «FL260 (pari)». L'approvato è stato riapprovato
   **dopo aver letto le nove famiglie di differenza**: 82 righe, nessuna aggiunta o tolta, nessuna frase
   toccata. ⚠️ Le release **già pubblicate** conservano il testo vecchio: uno snapshot è una fotografia, e il
   testo nuovo compare alla prossima release.
7. ✅ **`InlineConfirm` localizzato** (18 agosto): i default di prompt, conferma e annulla passano dal
   localizer. Erano cablati in italiano e su 14 usi solo 3 passavano le proprie etichette — gli altri 11
   dicevano «Sì, elimina» anche in pagina inglese, e anche per azioni che non eliminano.
8. ✅ **Plurali dei conteggi** (18 agosto): «1 clause» invece di «1 clauses», in entrambe le lingue e in quattro
   punti. Un conteggio è la cosa che si legge più spesso nella pagina.
8. ✅ **Merge in `main` fatto** il 18 agosto (`06798a9`), autorizzato dal committente; main verificato dopo il
   merge (build Release 0 warning su due TFM, 2569 test verdi).
9. ⚠️ **Il deploy in produzione è BLOCCATO finché la MariaDB non è convertita**: le migrazioni girano all'avvio
   e `AgreementSectionsFinalize` fallisce su un archivio non convertito. È la protezione voluta, ma va saputa
   **prima** di pubblicare — non dopo.

⚠️ **Due difetti trovati eseguendo, e che nessun test vedeva** — valgono fuori da quest'area: fra le due
migrazioni lo schema è **misto**, e cancellare un guscio si portava via clausole già riappese correttamente (60
→ 23, in silenzio); e lo scaffolding EF ha proposto, per la seconda volta su quest'area, un `RenameColumn` che
avrebbe prodotto dati **validi e sbagliati** (`AgreementId` spacciato per `SectionId`). Le migrazioni si
leggono, non si accettano.

### E6-ter ⚪ `AuroraClientTests.Richieste_in_sequenza_non_si_mescolano` è instabile
Trovato il 17 agosto 2026 mentre si chiudeva E6-bis, e **slegato da quel lavoro** (nessun file del bridge
toccato). In isolamento passa due volte su tre. Il test lancia due `SendAsync` insieme contro un
`FakeAuroraServer` su socket TCP di loopback e pretende che ognuna riceva la propria risposta: la serializzazione
che verifica è giusta, ma la prova dipende dai tempi del socket. Un rosso qui **non** significa che il client
mescoli le richieste. Va reso deterministico (attesa esplicita invece che implicita nei tempi di rete) prima che
qualcuno impari a rilanciare la suite finché diventa verde — che è il vero danno di un test ballerino.

---

## F. Rimandato, non cancellato

**Embedding nel sito `Ivao.It.Website`.** Il sito definitivo è il nostro host standalone, ma le cinque
librerie restano multi-target `net8.0;net10.0` proprio per questo — e ora che `Vipi.Host` è net8, la
distanza fra i due scenari è minima. Lavoro aperto in
[`guide/integrazione-ivao-it-da-fare.md`](guide/integrazione-ivao-it-da-fare.md): runtime EF Core 8 mai
eseguito (⚠️ ora lo sarà, in produzione), doppia localizzazione, Bootstrap del sito che sbava dentro
`.vipi-root`.

---

## G. Audit del database — 14 agosto 2026, chiuso lato codice

Carta ed esito in [`history/audit-2026-08-14-database-mariadb.md`](history/audit-2026-08-14-database-mariadb.md).
Sei commit **in questo ramo**. Cinque cose cambiano il comportamento in esercizio:

- **La concorrenza ottimistica era dichiarata e inerte.** `IsConcurrencyToken()` su sette `RowVersion`,
  funzionante su **uno**. Ora la rotazione la fa `VipiDbContext.SaveChangesAsync` — cioè il modello, non un
  repository che deve ricordarsene — e quattro entità hanno perso token e colonna, perché lì il
  last-write-wins è voluto.
- **Le chiavi Data Protection escono dal database del committente**: `/var/lib/vipi/keys` +
  `StateDirectory=vipi`. Stavano in chiaro in `DataProtectionKeys`, e chi ha `SELECT` su quel database
  potrebbe fabbricare un cookie valido per qualunque VID, admin compresi.
- **Pool a 20 + `EnableRetryOnFailure`** sul ramo MySQL: il default era **100**, contro un
  `max_user_connections` tipico di 25÷50.
- **Le quattro manutenzioni d'avvio non critiche sono isolate** (`RunVipiStartupMaintenance`): con
  `Restart=always` un guasto lì non era un degrado ma un ciclo di riavvii. `MigrateVipiDatabase` resta fatale.
- **`MySqlServerSettingsProbe`** verifica `sql_mode` e `max_allowed_packet`, provata guastando il server vero.

**La misura che ha deciso le priorità:** il `vipi.db` reale ha **~4 800 righe** (tabella più grossa
`AirportSids` 1481, corpo totale dei `ContentBlocks` **20 KB**, `AuditLogs` 20 righe). A questa scala **non
esiste un problema di prestazioni**: indici, cache e denormalizzazioni sono elencate in §E della carta come
scartate apposta, così non vengono riscoperte come idee nuove.

**Cosa resta aperto:** nulla di codice. Restano il **backup** (A9) e la **consegna del `.sql`** (A3).

ℹ️ Una regola che vale oltre questo caso: *«nessuno ha ancora applicato quella migrazione» è un'affermazione
sul mondo, non sul repository*. La carta proponeva di rigenerare `InitialCreate` MySQL perché nessun database
l'aveva vista; la MariaDB locale ce l'aveva già in `__EFMigrationsHistory`, e rigenerarla l'avrebbe resa non
aggiornabile.
