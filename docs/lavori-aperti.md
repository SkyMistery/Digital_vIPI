# Lavori aperti — elenco unico

**Aggiornato:** 26 agosto 2026, notte (**§K: la vIPI d'aeroporto entra nel catalogo delle sezioni — ramo
`aeroporto-a-sezioni`, il TERZO in fila. Era l'ultima famiglia con un documento COTTO, e per questo l'unica
senza riordino, senza «nascondi» e senza sotto-sezioni**) · **Aggiornato:** 26 agosto 2026, sera tardi (**§J7 chiusa: anche i blocchi della vIPI ACC si riordinano, con
i settori di aerovia fissi in testa; con §J6 la sezione J non ha più voci di UI aperte**) · **Aggiornato:** 26 agosto 2026, sera tardi (**§J6: l'ordine delle sezioni è una scelta editoriale — anche le
sezioni di catalogo si spostano dentro il loro gruppo e dicono di quanto si sono allontanate dallo standard;
nasce §J7, i blocchi della vIPI ACC che non si riordinano**) · **Aggiornato:** 26 agosto 2026, notte (**§E11 chiusa: la casella degli impatti, la sezione Orfani, il giro notturno della deriva e il rilevatore delle RINOMINE dal timbro d'import; nascono §C6 (chiave di release derivata da un callsign) e §C7 (i tre resti dell'analisi sulla cancellazione dei dati importati)**) · **Aggiornato:** 25 agosto 2026, tarda sera (**§B12: il ramo `statistiche-atc` porta anche le otto richieste
del committente sul servizio statistiche — §16 della carta — fra cui la sezione Aeroporti, la potatura e il
capitolo di Guida che mancava; restano aperte le stesse due voci UI**) · vecchia testata: (**§B12 aperta: il ramo `statistiche-atc` è completo e NON fuso** — la fusione è una decisione del committente, non un passo tecnico; il 24: §B10 e §B11 fuse e cancellate. La sera del 25 si chiudono **§H5** — il VID è un link al profilo IVAO, verifica live fatta — e **§H2**, il «rosso intermittente», che erano due difetti. Della sezione UI restano aperte **H1** e **H3**) · **Scopo:** una cosa alla volta, senza rileggere la cronologia.

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
> Da qui **`main` è il posto dove si lavora, e non c'è nessun ramo con lavoro fuori**: la frase ha vacillato
> il 24 agosto con `coordinamenti-lato-ricevente`, fuso e cancellato in giornata (**B10**). Gli altri undici sono
> tutti a zero commit di distanza — e il 22 agosto sono stati **cancellati tutti e undici**, locale e origin.
> ⚠️ Cancellandoli è saltata fuori una cosa: **`refactor/13-tre-documenti` (B5) non aspettava nessun ok**, era
> in `main` dal 15 agosto, portato dentro dal merge dei trasferimenti. Vedi B5.
>
> ⚠️ La frase ha vacillato due volte nella stessa giornata — `catalogo-punti-suggerimenti` (**B7**) e
> `coordinamenti-lettura` (**B8**) — ed è di nuovo vera: **tutti e due fusi e cancellati**, locale e origin.
> Carte: [servizi ATC](feature/2026-08-22-servizi-atc-e-profile-swapper.md),
> [brand](feature/2026-08-22-brand-atmosphere.md), [topbar misurata](feature/2026-08-22-topbar-misurata.md).
>
> ⚠️ **Fuso non vuol dire consegnabile** — lo è diventato il **23 agosto**. Il blocco (sezione E, punto 9: le
> migrazioni degli accordi girano all'avvio e `AgreementSectionsFinalize` fallisce finché la MariaDB di
> produzione non è convertita) non è stato risolto ma **aggirato**: la consegna sostituisce il database
> invece di migrarlo. Vedi **A11**.
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

> ### ✅ 23 agosto 2026 — CHIUSI: i due difetti visti mentre si chiudeva la topbar
> Carta del giro: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md).
> Erano stati messi qui senza toccarli. Chiusi tutti e due, e **il primo aveva il colpevole sbagliato**.
>
> **1. Le tabelle del viewer sforano a zoom alto** — chiuso. La diagnosi di ieri diceva `table.sid-table`
> col suo `min-width:720px`: **non era lui**, la SID sta già dentro un `div` che scorre. Rimisurato a 1280
> con zoom 1.4 (**914 unità di layout**): sforo di **35 unità**, e il colpevole è `table.rwy-table`, che non
> dichiara nessun minimo e ne pretende **570** in una `.cb-body` che ne ha 497.
> ⚠️ Confermato invece il **meccanismo**: lo zoom qui è `zoom` sull'`<html>` (`vipi-zoom.js`) e **le media
> query non lo vedono** — misurano la finestra (1280) mentre il layout ne ha 914. Per questo la cura non è
> spostare la soglia dei 900 ma **toglierla**: il contenitore diretto di una tabella scorre sempre,
> `.apt-2col` passa a `minmax(0,1fr)` + `min-width:0`, e l'`overflow-wrap` dei titoli esce dalla media query
> (il minimo di un titolo è la sua parola più lunga, che in unità di layout non si accorcia mai).
> Verificato guidando Edge su **144 combinazioni** (6 pagine × 6 larghezze × 4 zoom): il viewer aeroporto va
> da 48px di sforo a **0 su tutti gli zoom fino a 1.8**, e a zoom 1 la pagina è identica a prima.
> ⚠️ **Restano fuori**, e sono **preesistenti**: i **390px con zoom ≥ 1.25** (elenco aeroporti, landing,
> ricerca, «cosa è cambiato»). Là il layout ha 312 unità o meno, sotto il pavimento dichiarato di 375
> (`docs/design/regole-ui-pagine-admin.md`, perimetro d'uso). Se un giorno contano, si riparte da qui.
>
> **2. La cultura non arriva al circuito** — chiuso. In Blazor Server le richieste sono **due**: il documento,
> che porta `?culture=it` e vince con la stringa di query, e la connessione `/_blazor` che apre il circuito,
> che quella stringa non ce l'ha e ricade su `Accept-Language`. Il circuito nasce con quella cultura e la
> tiene per tutta la vita. `CultureCookieMiddleware` (in `Vipi.Hosting`, montato **dopo**
> `UseRequestLocalization`) scrive il cookie standard di `CookieRequestCultureProvider` quando — e **solo**
> quando — la lingua è stata chiesta esplicitamente nell'indirizzo.
> ⚠️ **Solo su richiesta esplicita**: scriverlo anche per `Accept-Language` congelerebbe per un anno una
> scelta che l'utente non ha mai fatto, e cambiare lingua al browser non avrebbe più effetto. Due test E2E,
> uno per verso; verificato anche guidando Edge con `Accept-Language: en-US`.

## Dove siamo, in cinque righe

⚠️ **Questo blocco era rimasto indietro per due giri** (dava «24 commit» e la sezione E in uno stato
precedente). Riscritto il **26 agosto a notte**, con le cifre **contate**.

Il **cutover MariaDB è in `main`** e verificato (A1–A8). Le sezioni **B** (branch), **C** (debito, tranne C3
e le tre voci C6/C7 aperte il 25-26), **D** (verifiche live arretrate) e **G** (audit del database, lato
codice) sono chiuse o chiuse-con-la-ragione-scritta. La **E** è stata sfoltita: metà delle voci erano già
fatte o non avevano più senso.

🟡 **La decisione sul tavolo è UNA, ed è B12: fondere.** ⚠️ Ma non è più un ramo solo: sono **TRE, in fila**.

| Ramo | Commit oltre `main` | Cosa porta |
|---|---|---|
| `statistiche-atc` | **82** | il terzo servizio, gli aeroporti militari, la vIPI d'aeroporto legata allo scalo, l'**eliminazione con le protezioni**, «chiedi alla sorgente», la lista «Da fare» |
| `identita-settori` (sopra il primo) | **110** | l'identità dei settori per **id IVAO**, l'assenza che non cancella, le **shape dal sectorfile** col gate AIRAC, l'ordine delle sezioni, l'avviso a chi pubblica |
| `aeroporto-a-sezioni` (sopra il secondo) | **11** | la vIPI d'aeroporto entra nel **catalogo delle sezioni**: si riordina, si nasconde, prende sotto-sezioni e sezioni libere ovunque; e la sua release **congela** davvero (§K) |

⚠️ **L'ordine di fusione è quello della tabella**: ognuno è costruito sopra il precedente, non lo sostituisce.
⚠️ Le cifre si contano: `git rev-list --count main..<ramo>`.
⚠️ Insieme portano **DICIASSETTE** migrazioni — è la coda più lunga da mesi per il cutover MariaDB — e **un
passo d'avvio nuovo**, `LinkAirportDocumentsAsync`, che collega gli aeroporti alla loro vIPI al primo avvio
(idempotente, lo scrive nei log). Senza, i documenti d'aeroporto già pubblicati risulterebbero inesistenti
alla strada nuova.

⚠️ **Quel che resta fuori dai due rami è quasi tutto fuori dal codice**: le risposte di Ivao.It (A9/A13) —
fra cui **chi fa il backup**, domanda a cui oggi nessuno sa rispondere — la **rotazione** dei segreti esposti
il 24-25 agosto, e le decisioni di contenuto che aspettano il committente.

🔵 **Deciso il 26 agosto sera: il database si ripulisce un'ultima volta prima di popolarlo.** Da qui **I1**
(le radici orfane di LIRR) resta **sospesa di proposito**: non si sistema un albero che sta per essere rifatto.

**Sezioni con lavoro aperto, oggi**: **C6** (⚠️ da rileggere: metà del problema è caduta da sé), **C7a/b/c**,
**H1** e **H3**. Le sezioni **I** (sospesa), **J** e **K** (chiuse tutte) non chiedono niente.

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
  ⚠️⚠️ **Dal 23 agosto è la domanda più urgente delle sei**, perché la procedura di aggiornamento (A11)
  comincia con un `DROP DATABASE`: il passo 1 del foglio è un dump fatto da loro, ed è l'unica rete sotto a
  quel comando. Se non sono in grado di farlo, l'aggiornamento non va cominciato.
  🟡 **23 agosto, sera — una mezza risposta**: il committente riferisce che **il backup lo fanno loro**
  (Ivao.It), «a quanto ho capito». È la prima notizia in nove giorni ed è verosimile — un hosting Plesk
  gestito di norma fa backup di vhost e database. Ma resta **riferita, non confermata**, e la regola scritta
  qui sopra vale ancora: finché non è confermata si pianifica come se non ci fosse. **Tre domande la
  chiudono**, e sono corte: (1) con che **frequenza** e quanta **retention**; (2) copre anche i **file del
  vhost** — cioè la cartella `public_atc/vipi-keys`, che **non sta nel database** e che un backup del solo
  MySQL non prenderebbe (perderla slogga tutti); (3) è mai stato provato un **ripristino**, o è solo
  configurato. La terza è quella che di solito sorprende.
  ℹ️ Da oggi la posta è più alta: fino a ieri in produzione c'erano i contenuti del 14 agosto, che erano una
  copia. Dal 23 c'è il contenuto **vero e corrente**, e la copia di sviluppo (`vipi.db`) diverge da lì in poi.
- ⚠️ **`itivao_atc` può fare `DROP DATABASE`?** `GRANT ALL ON itivao_atc.*` lo comprende, ma su Plesk
  l'utente lo crea il pannello e nessuno ha verificato la lista vera. Se non può, si svuota tabella per
  tabella o si consegna un `.sql` con i `DROP TABLE` in testa — è scritto nel foglio, ma è meglio saperlo
  prima che scoprirlo a metà.
  Il database tiene **tutto** lo stato dell'app, immagini comprese (ADR-0007): non c'è una seconda copia
  altrove da cui ricostruire. La `ReleaseRetention` **non** è un backup — pota di proposito. Se la risposta
  tarda, il paracadute è uno script `mariadb-dump` in cron, da concordare con loro.
- ⚠️ **Nei backup vanno due cose, non una**: il database e la **cartella delle chiavi** Data Protection,
  `/var/lib/vipi/keys` (§G). Perderla slogga tutti una volta sola, ma è un file solo ed è banale includerlo.
- Sulla macchina: **WebSocket** sul reverse proxy (senza, Blazor Server apre le pagine e resta muto),
  header inoltrati, supervisione del processo, e il **percorso persistente** per il key-ring (che dal 14
  agosto non sta più sul database: §G).

### A10 ✅ Redirect OIDC sul portale IVAO — chiusa dai fatti il 16 agosto 2026
`https://atc.it.ivao.aero/signin-oidc` e `/signout-callback-oidc` **sono registrati**: non perché qualcuno
l'abbia confermato, ma perché **il login in produzione funziona** dal 16 agosto — e senza quei due redirect
non tornerebbe indietro affatto. Stessa cosa per `VipiAuth:ClientSecret`, che nel file di produzione c'è.

ℹ️ La voce è rimasta 🔴 per una settimana dopo essere diventata vera: **una domanda in sospeso non si chiude
da sola quando la risposta arriva sotto forma di fatto** invece che di messaggio. Vale la pena rileggere le
🔴 dopo ogni deploy riuscito, e chiedersi quali abbia già risposto l'esercizio.

---

### A11 ✅ Consegna del 23 agosto 2026 — pacchetto **e** database, prodotti insieme
Il pacchetto e il `.sql` sono **due metà della stessa consegna** e non sono separabili: il codice del 23
agosto non sa leggere l'archivio del 15, perché in mezzo c'è il modello degli accordi a sezioni.

| | Cosa | Dove | sha256 |
|---|---|---|---|
| **Sito** | `vipi-linux-x64-mariadb-20260823.zip` — 48,7 MB, 421 file (418 da caricare: `deploy/` è riferimento), self-contained net8 | `artifacts/publish/` | `FE7F9FC8…E3FC4025` |
| **Database** | `vipi-atc-it-ivao-aero-2026-08-23.sql` — 3,1 MB, schema + dati + `__EFMigrationsHistory` | `_mariadb/dump/` (**fuori dal repo**) | `0861BE6A…C20969A` |

**La strategia è la sostituzione, non la migrazione**, ed è ciò che scioglie il blocco di E6-bis §9: il
`.sql` porta con sé lo schema già convertito e la storia delle migrazioni, quindi all'avvio l'applicazione
**non applica niente** e `AgreementSectionsFinalize` non ha modo di fallire. Il prezzo è dichiarato in testa
al foglio di aggiornamento: **si perde ciò che è stato scritto in produzione dal 16 agosto in poi**, e il
committente ha confermato che non c'è.

**Il foglio nuovo è [`../deploy/atc-ivao/LEGGIMI-AGGIORNAMENTO.md`](../deploy/atc-ivao/LEGGIMI-AGGIORNAMENTO.md)**:
finora esisteva solo la procedura di **prima installazione**, e per systemd. Questo è per un sito **già in
piedi** su Plesk+Passenger, e mette per iscritto le tre cose che l'FTP cancellerebbe senza chiedere —
`appsettings.Production.json`, `vipi-keys/`, `tmp/` — più l'ordine dei passi: **prima i file, poi il
database, poi `tmp/restart.txt`**, perché Passenger serve la versione vecchia finché non gli si dice di
ripartire e così la finestra di disallineamento dura secondi.

⚠️ **La consegna si divide in due canali, e il foglio di istruzioni sta nel canale sbagliato.** I file
dell'applicazione li carica il committente via FTP; il `.sql` va a chi in Ivao.It ha accesso al database.
Ma `LEGGIMI-AGGIORNAMENTO.md` sta **dentro lo zip**, che a loro non arriva. Per questo c'è
`artifacts/CONSEGNA-DB-20260823.md`: la sola parte del database, che si manda insieme al `.sql` — backup,
`DROP DATABASE`, import, e le due domande mai risposte (chi fa il backup, `max_allowed_packet`).

⚠️ Per la stessa ragione nel pacchetto **non c'è** `appsettings.Production.json`, ma
`appsettings.Production.json.esempio`: un file con quel nome, caricato via FTP, cancellerebbe la password
del database e le credenziali IVAO che stanno solo sul loro server — e il sito ripartirebbe **su SQLite
vuoto**, che è il modo peggiore di sbagliare (sembra che i dati siano spariti).

⚠️ **Ma il `.esempio` non va in produzione, e la domanda l'ha fatta il committente.** Rinominando per non
sovrascrivere si era prodotto un file che **non finisce più per `.json`**, quindi la regola che nega
`appsettings*.json` — quella che fa rispondere 403 a `/appsettings.json` — **non lo copre**. Non contiene
segreti, ma descrive nome del database, nome dell'utente e percorso del key-ring. Spostato in `deploy/`
insieme all'unit systemd e alla conf nginx, cioè fra le cose che **non si caricano**: sono tutti file di
riferimento, e su Passenger nessuno dei tre serve a qualcosa.

ℹ️ La regola generale che ne resta: **rinominare un file per proteggerlo da una sovrascrittura non lo
protegge da tutto il resto** — cambiando estensione si esce anche dalle deny list scritte su quella. Se un
file non è di runtime, la risposta giusta non è rinominarlo ma **non metterlo lì**.

**Cosa è stato verificato, e come:**
- catena `vipi.db → Vipi.DbSeed → MariaDB 11.4.10 locale → mariadb-dump → .sql`, **38 tabelle su 38
  riconciliate** dal tool, 4151 righe lette;
- il `.sql` **reimportato in un database vuoto** e confrontato **tabella per tabella**: 39/39, 4162 righe,
  **zero differenze**;
- l'host avviato su quel database: `/services/vsop` **200** con LIRR/LIMM/LIBB a schermo e **zero**
  `Applying migration`;
- collation `utf8mb4_uca1400_as_cs` su **168 colonne** (le 2 rimanenti sono `__EFMigrationsHistory`), e la
  prova che conta — `ZZZZ` e `zzzz` convivono nell'indice unico e il `WHERE` li distingue;
- primi quattro byte `2f 2a 4d 21` (`/*M!`) e **nessun CRLF**: la trappola del BOM di A3 non si è ripetuta.

⚠️ **Due trappole ripagate, entrambe già scritte e comunque incontrate.** `Vipi.DbSeed` non è in
`Vipi.slnx`, quindi il suo `packages.lock.json` era rimasto a EF 8.0.29 mentre `Vipi.Infrastructure` è a
8.0.30: `CS1705` **il giorno del travaso**, cioè l'unico giorno in cui il tool si usa. E il `publish` con
RID ha toccato **nove** `packages.lock.json`, che vanno rimessi a posto prima di committare — è la nota di
[[stato-9-agosto-2026]], e vale ancora.

⚠️ **Non verificato, come sempre**: il pacchetto non è mai stato eseguito su Linux (compilazione
incrociata da Windows). Il `.sql` invece sì, contro una MariaDB 11.4.10 vera.

⚠️ **Rifatto la sera del 23** per portare dentro la correzione di **H4** (l'intestazione della tabella ACC
che non si appiccicava). Il committente aveva già finito di caricare la versione precedente: fra i due
pacchetti differiscono **21 file**, ma **19 sono DLL ricompilate** — stessa funzione, identità di build
diversa. Quelli che cambiano davvero sono **due**: `wwwroot/_content/Vipi.Ui/vipi-theme.css` e
`Vipi.Host.staticwebassets.endpoints.json`, che porta impronta e `integrity` dell'asset e **va aggiornato
insieme al CSS** — da solo, il vecchio manifesto continuerebbe a chiedere la versione di prima.
ℹ️ Il confronto si fa a **sha256 file per file** contro una copia del pacchetto già caricato, non a occhio:
è ciò che ha distinto i 2 file veri dai 19 rumorosi.

ℹ️ **Il contenuto consegnato non è quello del 18 agosto**: gli accordi sono 16 come allora, ma le sezioni
sono **34** e le clausole **50** (erano 38 e 60 il giorno della conversione). È lavoro editoriale fatto in
mezzo, non una perdita del travaso — il tool riconcilia riga per riga ed esce in errore se una tabella non
combacia.

### A12 ✅ 23 agosto, sera — la produzione si è aggiornata da sola, e il `.sql` è stato importato
> ⚠️ **Correzione, e vale più della voce.** Questa voce ha sostenuto per un'ora che i coordinamenti di
> produzione fossero **perduti**. **Non lo sono**: il committente ha guardato `/services/vsop/admin/trasferimenti`
> e gli accordi ci sono. Ivao.It ha importato il `.sql`, e la consegna è **completa** — sito e database.
>
> L'errore non è stato nella misura ma nell'**inferenza**: dal fatto che le migrazioni fossero girate ho
> concluso che *nient'altro* fosse successo, e da lì che il `DROP` fosse l'ultima parola sui dati. Era una
> deduzione su una cosa che non potevo vedere — lo stato del loro database — spacciata per constatazione.
> ⚠️ **Il committente aveva l'unica prova che contava** («c'è la quota corretta da 90 a 9000, e nell'ultima
> release del DB non c'era») e io ho continuato a cercarla da fuori, dove non era raggiungibile. Quando
> qualcuno che *guarda il sistema da dentro* riferisce un fatto, quello è un dato, non un'impressione da
> verificare prima di crederci.

<details><summary>Quel che resta vero, e serve al prossimo giro di migrazioni</summary>


Il committente ha caricato i file via FTP e **Passenger ha rigenerato il processo** senza che nessuno
toccasse `tmp/restart.txt`. Misurato dall'esterno, non dedotto: `/services/vsop` risponde **200** (quella
rotta nella build del 15 agosto non esiste), `/vsop` dà **301**, `/vsop/health/ready` dice **Healthy**, e
lo `scope` dentro un `code` OIDC del 23 alle 20:31 UTC è `openid profile email` — quello del 15 agosto era
`openid email tracker`.

Quindi in produzione gira **il codice del 23 sui contenuti del 14**, e le migrazioni sono state applicate
all'avvio.

⚠️⚠️ **E lì c'è la perdita.** L'archivio del 14 agosto è sul modello **legacy**: il suo `.sql` contiene
`TransferFlows` e `TransferPoints` **con dati**. Salendo alla testa, `20260817090137_DropLegacyTransferTables`
li **droppa** (`Up` = due `DropTable`; il `Down` li ricrea **vuoti**, e lo dice il commento della migrazione
stessa), mentre `AddCoordinationAgreements` non travasa niente — **zero** `InsertData`, **zero**
`migrationBuilder.Sql`. La conversione flussi→accordi non è mai stata una migrazione: è stata un passo a
parte, eseguito **solo in sviluppo**.

**Quindi un archivio ancora sul modello legacy che sale alla testa perde i coordinamenti in silenzio**, e le
tabelle nuove restano vuote: nessun errore, nessun avviso. In produzione **non è successo** — l'import del
`.sql` ha rimesso tutto — ma la proprietà della catena di migrazioni è questa, ed è verificata nel codice.

⚠️ **La protezione su cui contavamo non copriva questo caso, e va detto per esteso.** `AgreementSectionsFinalize`
fallisce rumorosamente su un archivio che **ha già gli accordi** in forma vecchia — quello era il caso previsto.
Un archivio ancora sul modello **legacy** non lo incontra mai: passa dal `DROP`, e arriva in fondo pulito e
vuoto. Avevamo scritto «un deploy fatto adesso non parte»; il vero rischio era «parte, e non dice niente».

**Cosa ne consegue:** la sostituzione del database (**A11**) è stata **fatta**, e ha portato in produzione sia
i contenuti aggiornati sia gli accordi. Se non fosse stata fatta, quella catena avrebbe lasciato un sito senza
coordinamenti — motivo per cui la regola qui sotto resta.

</details>

ℹ️ Da qui una regola per il prossimo set di migrazioni: quando una migrazione **droppa** una tabella che
altrove è stata *convertita* da un passo esterno, la migrazione deve o portarsi dentro la conversione, o
**rifiutarsi di girare** se trova righe. Un `DROP` silenzioso su dati veri non è reversibile e non si accorge
di nulla.

### A13 ✅ CHIUSO DALL'ESTERNO il 25 agosto 2026 (sera) — resta solo la rotazione

> **Aggiornamento 25 agosto 2026 (sera).** Il committente ha riferito che **l'hosting ha cambiato le
> impostazioni di accesso**. Rimisurato dal vivo con `curl` (solo status code, non i corpi dei file
> segreti):
>
> | URL | esito ORA |
> |---|---|
> | `/appsettings.Production.json`, `/appsettings.json`, `/appsettings.Development.json` | **404** |
> | `/Vipi.Host.dll`, `/Vipi.Host.pdb` | **404** |
> | `/diagnostica/avvio-diagnostica.txt`, `/diagnostica/errori-richieste.txt` | **404** |
> | 7 varianti di aggiramento (`.JSON`, `//`, `/./`, `?x=1`, `%61`, maiuscole, `…/.`) | **404 tutte** |
> | `/services/vsop` (GET reale) | **200** `text/html` |
> | `/_content/Vipi.Ui/vipi-theme.css` | **200** — gli asset dell'app si servono ancora |
>
> **Il 404 nasce dall'ORIGINE, non dal CDN** (`cf-cache-status: DYNAMIC`, nessuna block-page Cloudflare):
> è l'applicazione a rispondere «non esiste». Firma del fix: `/_content/…` dà 200 mentre i file alla radice
> danno 404 ⇒ **ora tutte le richieste passano all'applicazione** invece di essere servite dal filesystem —
> è la strada giusta («document root ≠ cartella app»), non il cerotto per oscurità. Novità: davanti al sito
> ora c'è **Cloudflare** (`Server: cloudflare`), prima assente. (Questo chiude anche la vecchia **A13-bis**:
> il criterio «devono diventare 403/404 …» è soddisfatto.)
>
> ⚠️ **RESTA APERTO — rotazione dei segreti, IN CORSO.** Chiudere l'accesso oggi non annulla l'esposizione
> 24→25 agosto: password DB e `ClientId`/`ClientSecret` IVAO sono stati pubblicamente scaricabili (e il repo
> GitHub è pubblico). Il committente **ha chiesto le credenziali nuove il 25 agosto**; vanno considerati
> compromessi finché non arrivano e non sono in opera.
>
> ⚠️ **Igiene con la nuova architettura**: ora che c'è un CDN davanti, restringere l'ORIGINE ad accettare
> solo il traffico Cloudflare (Authenticated Origin Pulls o whitelist IP CF), o chi conosce l'IP origine
> aggira il WAF andando diretto. Oggi non sfruttabile (il 404 nasce dall'app), ma è la mossa corretta.

**Storia — trovato il 24 agosto 2026** mentre si indagava il 500 di E8, con `curl -I` sulla produzione. Il
front server serviva i file **direttamente dalla cartella dell'applicazione**: `public_atc` non era solo la
radice dell'app, era anche il **document root** del sito. *(Non è più così dal 25 agosto — vedi sopra.)*

| URL | esito misurato |
|---|---|
| `/appsettings.Production.json` | **200**, `application/json` — dentro ci sono **password del database** e **ClientSecret IVAO** |
| `/appsettings.json`, `/appsettings.Development.json` | 200 |
| `/diagnostica/avvio-diagnostica.txt` | 200 — configurazione vista all'avvio, percorsi, quali segreti sono valorizzati |
| `/Vipi.Host`, `/Vipi.Host.dll`, `/Vipi.Host.pdb`, `/Vipi.Infrastructure.dll` | 200 — l'applicazione intera, coi simboli di debug |
| `/web.config`, `/vipi.db.bak` | 403 — Plesk nega **alcuni nomi**, non la cartella |
| `/vipi-keys/`, `/diagnostica/`, `/deploy/` | 404 — **niente elenco cartelle**: i file si prendono solo per nome esatto |

⚠️ **Il commento dentro `deploy/atc-ivao/appsettings.Production.json` dice «Che non sia scaricabile via HTTP
è stato verificato: /appsettings.json risponde 403». Oggi non è più vero.** Quella misura è del 16 agosto,
prima del passaggio a Plesk+Passenger, ed è invecchiata **in silenzio**: è il caso da tenere a mente ogni
volta che si scrive «verificato» accanto a un fatto che dipende dall'hosting.

⚠️ **`deploy/atc-ivao/nginx-vipi.conf` nega `^/diagnostica/`, ma su quel server non è la nostra conf a
girare**: è un file di riferimento per un deploy systemd+nginx che lì non esiste. Una regola scritta in un
file che nessuno carica non protegge niente.

**Il key-ring si salva per il rotto della cuffia.** `public_atc/vipi-keys/key-<guid>.xml` non è elencabile e
il nome è un GUID; ma è sicurezza per oscurità, e chi lo indovina **fabbrica un cookie di autenticazione
valido per qualunque VID, admin compresi** — è scritto nel commento `DataProtection` di appsettings.

**Le due strade giuste erano chiuse** al 24 agosto — **ENTRAMBE si sono riaperte** con la segnalazione a chi
supervisiona it.ivao.aero (25 agosto):
1. ~~Ruotare i segreti~~ → **IN CORSO**: il committente ha chiesto le credenziali nuove il 25 agosto (prima
   «non si può fare», perché la password DB e l'app IVAO non erano nostre da cambiare — ora c'è un
   interlocutore).
2. ~~Chiedere a chi ha il pannello~~ → **FATTO**: chi ha segnalato il problema supervisiona il dominio e ha
   cambiato le impostazioni di accesso (i file non si scaricano più — vedi l'aggiornamento in testa).

**Il rimedio che resta, ed è quello messo in opera (pacchetto «f»).** Se il file non si può nascondere, si
svuota: `SegretiFuoriDalWeb` unisce alla configurazione ogni `*.json` dentro la cartella `segreti/` accanto
all'eseguibile, **dopo** tutto il resto, quindi quei valori vincono su `appsettings.Production.json`. Il
**nome del file lo sceglie chi installa** e non è scritto da nessuna parte: il server non elenca le
cartelle, quindi un file si prende solo indovinandone il nome esatto. Istruzioni in
[`../deploy/atc-ivao/LEGGIMI-SEGRETI.md`](../deploy/atc-ivao/LEGGIMI-SEGRETI.md).

⚠️ **È sicurezza per oscurità, ed è giusto chiamarla col suo nome.** Non chiude il buco: sposta i segreti da
«scaricabili con un indirizzo scritto nel nostro repository» a «scaricabili da chi indovina un nome che
nessuno conosce». È esattamente la protezione che regge oggi il key-ring, e che il progetto ha già
accettato per quello. La riparazione vera resta la 2, quando ci sarà un canale per chiederla.

⚠️ **Il passo che chiude davvero è il quarto del foglio: togliere i valori da `appsettings.Production.json`.**
Finché la password sta anche là, spostarla non è servito a niente. Per questo l'avvio **si ferma** se la
connection string è vuota o porta ancora il segnaposto: senza quella guardia, la configurazione a metà
ripiegherebbe su uno SQLite vuoto e il sito tornerebbe su con l'aria di aver perso tutti i dati.

⚠️ **Il nome dei file di `segreti/` non entra in `avvio-diagnostica.txt`** — quel riepilogo è a sua volta
scaricabile, e scriverci il nome vanificherebbe l'unica protezione che c'è. Si riporta quanti, mai quali.

**Quel che resta esposto, e non si può chiudere da qui:** `*.dll`, `*.pdb`, `appsettings.json`, i file di
`diagnostica/` (che da E8 contengono stack trace e VID). Nessuna credenziale, ma una mappa del server.
E i segreti già scaricati nelle settimane scorse restano scaricati: **questo rimedio ferma l'emorragia, non
la ripara**.

## B. Branch non fusi — decisioni, non lavoro

### B12 🟡 NON FUSO — `statistiche-atc`: la decisione è del committente

**Una sessantina di commit** oltre `main`, spinti su `origin/statistiche-atc`. ⚠️ **La cifra esatta si conta,
non si legge da qui** — ed è scritta così di proposito: ogni volta che la si fissava a un numero, il commit
che la aggiornava la faceva sbagliare di uno. Qui
c'è stata scritta «24» per due giri di fila mentre il ramo era già a 27, ed è il motivo per cui accanto c'è
il comando — `git rev-list --count main..statistiche-atc`.
**Niente lo blocca sul piano tecnico**: build a **0 avvisi** e suite **tutta verde** su tutti e due i TFM —
**2368 su net8, 2130 su net10**, rimisurati il 25 agosto a tarda sera dopo le otto richieste e la
correzione delle chip (§16 della carta). Fondere è una decisione, non un passo rimasto indietro.
⚠️ Prima di credere a un conteggio: `grep "error MSB"`. Con `Vipi.Host` acceso (la verifica live) i suoi DLL
sono bloccati, mezzo albero non compila e il totale cala di centinaia senza che il comando diventi rosso in
modo visibile.

ℹ️ Per qualche ora del 25 sera su net10 c'era **un rosso**, ed era del ramo: due difetti nelle proprietà
CsCheck dell'AoR, chiusi in giornata. La storia sta in **§H2**, e vale la pena leggerla prima di rilanciare
una proprietà che cade.

ℹ️ **L'ultimo commit non è delle statistiche.** È
[il VID che diventa un link al profilo IVAO](feature/2026-08-25-vid-porta-sul-profilo-ivao.md) (`03463bf`),
chiesto dal committente il 25 sera: è finito qui perché qui si stava lavorando, e due dei suoi quindici
punti (`StatsHome`, `StatsDivisionPage`) sono file che **esistono solo su questo ramo**. Non aggiunge
migrazioni e non tocca il modello. Cosa gli resta: **§H5**.

Carta con tutto: [`feature/2026-08-24-servizio-statistiche-atc.md`](feature/2026-08-24-servizio-statistiche-atc.md)
— **§12** è l'elenco vivo di cosa resta, **§13** la veste del 25 agosto, **§14** le statistiche di un altro,
**§15** i due modi di leggere gli aeroporti.

**Le otto cose chieste dal committente a tarda sera del 25** stanno in **§16** della carta, e sono tutte
dentro. La sola davvero nuova: **Aeroporti: traffico e copertura** — quanto traffico c'è stato su ogni campo
italiano e quanto ha trovato un controllore acceso — dentro `/services/stats/division`, **solo staff**,
raggruppabile per ACC (`?g=LIRR`).

⚠️ **La carta diceva una cosa falsa**, e chi legge §3 la deve leggere corretta:
`/v2/airports/{icao}/stats` **non** dà conteggi giornalieri di movimenti — è una fotografia al minuto dello
stato corrente, con `limit` sotto 100. Quel che serve lo dà `/traffics`, che **regge trenta giorni in una
chiamata** (LIRF: 981 KB, 1,3 s) e porta gli **istanti** che il nostro client buttava. Zero endpoint nuovi.

ℹ️ Provato con **dati veri**: durante la verifica live il consolidamento ha girato contro IVAO e ha misurato
**3 525 giorni-aeroporto** su 75 campi. Il totale di quella finestra: **16 374 movimenti, 3 307 con ATC — il
20%**. Estremi misurati: LIEO 52%, LIRP 0%.

**Le quattro cose chieste dal committente la prima parte della sera del 25**, tutte già dentro:

1. **Il numero nel buco della ciambella non ci stava** (§13.8). Il buco è largo 69 unità del viewBox e il
   corpo era fisso a 19: cinque cifre ci stanno, sei no. Si vedeva **solo su `/division`** perché il
   componente era stato provato con le ore di **una persona**, mai con quelle di una divisione.
2. **Lo staff può aprire le statistiche di un altro** (§14): `/services/stats/user/{vid}`, tutto lo staff
   `IT-`. ⚠️ La guardia sta **prima di ogni query**, e un test lo verifica con un `IAtcStatsQueries` che
   esplode a ogni metodo. L'accesso lascia **una riga di audit** (`AuditAction.View`, valore nuovo e
   additivo: gli enum sono stringhe, nessuna migrazione), accorpata a mezz'ora perché i chip di periodo
   ricaricano la pagina. **Chi viene guardato non viene avvisato** — deciso, non rinviato.
3. **Aeroporti gestiti accanto ad aeroporti visti** (§15). Sono due domande opposte: i campi che coprivi,
   e i capi del piano di volo che ti passano davanti. ⚠️ **Un sorvolo vettorato non è traffico «di» un
   aeroporto** ma resta nei totali — quindi la somma della colonna dei gestiti **non** è il totale dei voli.
   Per i settori d'area il campo lo dice la **geometria** (`PolygonGeometry.Contains` sul poligono del
   settore), non l'albero: `Airport.ParentCallsign` è compilato a mano e ce l'hanno **31 aeroporti su 93**,
   con **12 CTR su 140** che abbiano qualcosa sotto. ⚠️ Il poligono è quello di **oggi**: una
   risettorizzazione cambia i numeri dei turni **passati**, ed è stato accettato sapendolo.
4. **Il salvataggio finale del poller non usava il gettone giusto.** Allo spegnimento il log diceva
   «salvataggio finale del traffico fallito» con una `TaskCanceledException` e **sembrava un guasto del
   database**: non lo era, `StopAsync` passava alla scrittura il proprio gettone di arresto. E `FlushAsync`
   chiama `TakeAll`, che **svuota** il registro prima di salvare — quei minuti non erano più né su disco né
   in RAM. Ora la scrittura ha un gettone suo con cinque secondi di tetto.

**Cosa porta.** Il **terzo servizio** dell'hub, `/services/stats`: ore, turni, traffico gestito, copertura
della divisione. ⚠️ IVAO dà le **connessioni**, non il traffico: chi hai gestito lo costruiamo campionando
l'AoR a ogni giro del poller che esisteva già — **stessa cadenza, stesso numero di chiamate**, in più i
piloti. Dal 25 agosto ogni volo porta le sue **targhette** (in partenza / in arrivo / sorvolo · decollato ·
**atterrato** · al parcheggio · consegnato a X · uscito in volo · solo rullaggio · fermo), la sessione ha una
**striscia del turno**, e c'è la **costanza** in settimane di fila.

⚠️ **La regola che governa le targhette, e va tenuta se qualcuno ci lavora sopra: si dice quel che si è
VISTO.** Un volo diretto al tuo campo che esce dall'area ancora in volo **non è «atterrato»**. La regola sta
in `TrafficStory` (puro, con test) e la usa **anche il filtro** della pagina: una seconda copia nel markup si
scollerebbe dalla prima al primo cambiamento.

**Le tre cose da fare quando si decide di fonderlo** (dettaglio in §12 della carta):

| | cosa | quando |
|---|---|---|
| ✅ | ~~**La Guida**~~ — **fatta** il 25 sera: capitolo `statistiche` in `GuidaPage.razor`, IT ed EN. ⚠️ La diagnosi qui scritta era **sbagliata a metà**: la voce in `GuideSearchCatalog` c'**era già**, e puntava a un'ancora che nella Guida non esisteva — chi cercava «statistiche» trovava un risultato, lo apriva e finiva su una pagina senza quel capitolo. Un collegamento morto è peggio di nessun collegamento, perché nessuno lo denuncia. Ora c'è `GuidaAncoreTests`, che verifica che **ogni** voce del catalogo abbia il suo capitolo. | fatto |
| 🔴 | **La `UPDATE` dei tetti TWR** (§4.5-bis) è stata eseguita **solo sul `vipi.db` di sviluppo**. Senza, in produzione le torri rivendicano fino a FL195 e il traffico in crociera finisce a loro. Stessa guardia: `Position='TWR' AND LimitsFromSource=0 AND UpperLimit=19500`. | al primo deploy |
| ✅ | ~~**La potatura del dettaglio traffico**~~ — **scritta** il 25 sera: `TrafficRetentionUseCase` + `TrafficRetentionHostedService`, a scaglioni e con tetto per giro. ⚠️ Tocca **solo** `AtcSessionTraffic`: le sessioni e i loro contatori denormalizzati restano, ed è precisamente il motivo per cui quei contatori esistono (c'è un test che lo verifica). ⚠️ `RemoveRange`, **non** `ExecuteDelete`. | fatto |

⚠️ **Due cose non ancora viste dal vivo**, e sono l'una il seguito dell'altra:

- **la sequenza delle piste in uso**: coperta da test contro un database vero, ma in tutt'e due i momenti in
  cui si poteva provare non c'era **nessun** ATC italiano collegato (0 su 444 piloti, poi 0 su 422);
- **le targhette di fase e le consegne**: le otto colonne nascono con la migrazione `FasiQuoteConsegne` e si
  riempiono **dal primo turno campionato dal vivo**. Sulle righe già in archivio restano vuote — e in quel
  caso la pagina **non scrive** targhette di fase, che è la stessa regola delle righe ricostruite.

Verifica per tutt'e due: aprire `/services/stats/session/{id}` di una sessione registrata **dopo** il deploy.

⚠️ **Sette migrazioni** del servizio statistiche (più le due del giro aeroporti = **nove** in tutto sul ramo), tutte a doppia emissione: `StatisticheAtc`, `PolicyStatisticheAtc`,
`TrafficoRiempitoAPosteriori`, `ImpostazioniStatistiche`, `PisteInUso`, `FasiQuoteConsegne` e
`TrafficoAeroportoGiornaliero` (25 sera, §16.3). Il ramo
**allunga la coda del cutover MariaDB** — a differenza di B10, che non aveva migrazioni.

### B11 ✅ FUSO — `login-utente-nuovo`, fuso in `main` il 24 agosto 2026

Undici commit più il merge `1d43767`. Nessun conflitto: il ramo era nato da `main` a `1883446` e nessuno
l'ha toccata nel frattempo. Dopo il merge: **Release verde su entrambi i TFM (0 avvisi)**, **1952 test
verdi** su net8.

⚠️ **Il merge è arrivato DOPO la verifica sul campo, non prima**: il codice era già in produzione dalle
16:19 UTC (pacchetto «g», commit `e8fc4a2`) e la cartella `segreti/` in opera dalle 16:43. Fondere prima
avrebbe voluto dire scrivere in `main` una cosa che nessuno aveva ancora visto funzionare sul server vero.

Contenuto: **E8** (la barra che non affonda la pagina, la pagina d'errore e il registro degli errori, la
versione in barra) e **A13** (i segreti fuori dal file che si scarica).

### B10 ✅ FUSO — `coordinamenti-lato-ricevente`, fuso in `main` il 24 agosto 2026

Sei commit più il merge `84f741b`, ramo cancellato (locale e origin). **Nessun conflitto**: il ramo era nato
da `main` a `f03cd57` e nessun altro ha toccato quei file dopo. Dopo il merge: build Release verde su
entrambi i TFM (0 avvisi), **1925 test verdi** su net8.

Carta con tutto: [`feature/2026-08-24-coordinamenti-lato-ricevente.md`](feature/2026-08-24-coordinamenti-lato-ricevente.md).

| commit | cosa |
|---|---|
| `2ed4a52` | la frase dal lato di chi riceve (`AppCoordRow.IsIncoming` + 3 template nuovi) + fixture |
| `54b4cc9` | `CoordTable`: il corpo diventa una sezione — **meccanico**, nessun cambio di reso |
| `8c7b49b` | due tabelle quando il nodo porta i due versi; via `LastColHeader` |
| `6ad66df` | carta con l'esito e la verifica live |
| `265b882` | anche la frase **uscente** con faccetta cambia forma (secondo giro, chiesto dal committente) |
| `f0a0088` | il ramo entra nei lavori aperti e nell'indice |

**In breve.** Un accordo si scrive una volta sola, dal lato di chi cede, e il documento di chi **riceve**
mostrava quelle stesse parole — «Zagreb Radar trasferisce a Brindisi Radar CS0…» dentro la vIPI di Brindisi —
con la colonna della controparte intestata «Prossimo» mentre porta chi **consegna**. Ora la riga porta il
verso, la frase si gira («X **riceve da** Y…») e un nodo che contiene i due versi si **spezza in due tabelle**
(«Arrivi · che cediamo» / «Arrivi · che riceviamo»).

⚠️ **Il fatto che decide qualunque lavoro futuro lì dentro: le tabelle dei coordinamenti sono MISTE.**
`BuildAccTree` raggruppa per `settore → ACC della controparte → aeroporto/tipo` e **la direzione non è una
chiave di raggruppamento**: il nodo `ES › Zagreb-LDZO › Sorvoli` porta **8 righe entranti e 6 uscenti**.
Qualunque disegno che metta la direzione sulla tabella — o la deduca dal tipo di flusso — è sbagliato e sembra
giusto.

⚠️ **Il secondo giro tocca anche le righe USCENTI.** `TemplateCleared` girava il verbo principale («{owner}
autorizza … e lo trasferisce a {target}») mentre la forma breve dice «{owner} trasferisce a {target} …»:
nella stessa tabella due righe dello stesso accordo si aprivano in due modi diversi. Ora le quattro forme
(× direzione, × faccetta) hanno la stessa testa e la stessa coda.

⚠️ **Nessuna entità nuova e nessuna migrazione**: non allunga la coda del cutover MariaDB. `IsIncoming` è un
campo **additivo** sul DTO serializzato dentro le release congelate, che lo deserializzano `false`.

### 🔴 B10-bis — resta da fare: ripubblicare **un** documento

`Sentence` e `LeadSentence` sono **stringhe già scritte** dentro la release: i documenti pubblicati
continueranno a dire «Zagreb Radar trasferisce a…» finché non esce una release nuova. Misurato fianco a
fianco sulla stessa vIPI ACC di Brindisi: **viewer pubblico 33 tabelle, tutte «PROSSIMO», zero «riceve da»**;
**editor (derivato live) 39 tabelle, 13 «DA», 4 nodi tagliati**. La differenza è tutta la ripubblicazione.

**Quanti documenti sono davvero, misurato in produzione la sera del 24 agosto 2026: uno.** Sono stati
interrogati **tutti** i documenti pubblici del sito, contando le occorrenze delle due frasi:

| Documento | tabelle | «riceve» | «trasferisce» |
|---|---|---|---|
| `/services/vsop/libb/vipi` | 39 | **0** | **55** |
| `limm/vipi` | 4 | 0 | 0 |
| 5 aeroporti (LIBC, LIBD, LIBR, LIBA, LIRN) | 4 ciascuno | 0 | 0 |
| 2 APP non remotizzati (LIBP, LICC) | 1 ciascuno | 0 | 0 |
| 3 vLoA di LIBB (LYBA, LDZO, LGGG) | 8÷9 | 0 | 0 |

Solo la **vIPI ACC di Brindisi** porta la prosa dei coordinamenti; gli altri documenti pubblicati non ne
hanno affatto, quindi per loro la ripubblicazione non cambierebbe una parola. ⚠️ La voce diceva
«ripubblicare i documenti», al plurale e senza numero, e per questo sembrava un lavoro: **è un documento,
e sono due clic**. Il modo di saperlo era interrogare il sito, non ricordare.

⚠️ **Non è un lavoro che si può fare da qui**: pubblicare significa scrivere nel database di produzione, e
si fa dall'editor con un'identità admin. Chi lo esegue: il committente. La verifica dopo, invece, si fa da
fuori in dieci secondi — la pagina pubblica deve smettere di dire zero «riceve».


### B9 ✅ FUSO — `sorgenti-giro-ta-piste`, fuso in `main` il 22 agosto 2026

Cinque commit più il merge `9be2200`, ramo cancellato. Nessun conflitto: il ramo era nato dopo l'ultimo merge.

Contenuto e decisioni: [`feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md`](feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md).
In breve: **tutti** gli import di `/services/vsop/admin/sources` girano ogni 24 ore. **Transition Altitude** e
**Piste** avevano solo i bottoni (`AirportDataImportUseCase`, chiave `AirportData`); l'**anagrafica aeroporti**
non compariva affatto nell'elenco e ora è un giro (`AirportDirectoryImportHostedService`, chiave
`AirportDirectory`). Da qui **nessuna riga resta «su richiesta»**, e un test lo pretende.

⚠️ **Due cose da tenere a mente per la produzione.**

1. L'anagrafica aeroporti è l'**unico giro che crea entità** — era stata lasciata a mano proprio per questo, ed
   è stata automatizzata su decisione del committente. È **additiva**: uno scalo tolto dalla sorgente resta in
   archivio e si toglie a mano. Al primo giro dopo il deploy comparirà **LIDS (Parco Livenza)**, che IVAO ha
   aggiunto e che il `vipi.db` non aveva.
2. I 21 aeroporti senza `TransitionAltitudeFt` si popoleranno da soli, e `RecomputeDefaultBandLevels`
   ricalcolerà i TL delle fasce **default**. Prima del deploy conviene guardare la policy vera in
   `/services/vsop/admin/sources`: in sviluppo la tabella `ImportPolicies` è **vuota**, quindi i valori a video
   vengono dai default delle colonne e non da una decisione.

Nessuna entità nuova e nessuna migrazione: non allunga la coda del cutover MariaDB.

⚠️ **Trappola di verifica pagata qui, e riutilizzabile.** La pagina sembrava non aggiornata: l'app girava da
un `dotnet run` avviato **dodici minuti prima** del commit che accendeva il giro. Il `.dll` in `bin/Debug`
aveva una data *più recente* (l'avevano riscritto i `dotnet test`), ma il processo tiene in memoria
l'assembly caricata all'avvio. Prima di dare la colpa al codice si guarda l'**ora di avvio del processo**,
non la data del file.

### B8 ✅ FUSO — `coordinamenti-lettura`, fuso in `main` il 22 agosto 2026

Cinque commit più il merge `1d74246`, ramo cancellato (locale e origin). Nessun conflitto: il ramo era nato
dopo l'ultimo merge e nessun altro ha toccato quei file.

Contenuto e decisioni: [`feature/2026-08-22-coordinamenti-lettura.md`](feature/2026-08-22-coordinamenti-lettura.md).
In breve, la **lettura** della sezione Coordinamenti: la prosa nasce chiusa in un blocco per tabella, con
l'invito ad aprirla sulla stessa riga del titolo («Arrivi · Testo esteso (2 frasi)»); le decine di tabelle di
un documento ACC si stringono (`10345 → 8423 px` sul blocco Aerovia di LIBB, **−18,6%**); la FIR porta il suo
ICAO (`Greece-LGGG`); e dentro un settore gli ACC si ordinano per **distanza da chi legge** — casa, italiani,
esteri — invece che per alfabeto.

Suite **1 695** verde su net8 **anche dopo il merge**, `Release --no-incremental` **0 avvisi** su due TFM,
verifica live guidata con Edge sulla bozza LIBB.

⚠️ **`dotnet test --artifacts-path` non si usa alla leggera**: sposta l'output, e i progetti che leggono
**fixture accanto all'assembly** ne scoprono di meno — `Vipi.AuroraProfiles.Tests` è passata da 63 casi a 13,
con 11 rossi che sembravano una regressione del merge e non lo erano. Serve solo dove i `bin` sono davvero
bloccati (il progetto E2E, che referenzia `Vipi.Host`); gli altri si lanciano normalmente.

**Non tocca la coda del cutover:** nessuna entità nuova, **nessuna migrazione**. L'unico cambio di forma è la
sostituzione di `GetSectorAccNameMapAsync` con `GetSectorAccRefMapAsync` (→ `AccRef`), propagata nello stesso
giro ai due chiamanti (vIPI ACC e vLOA) — nessuno resta indietro.

⚠️ **Due cose da sapere prima di fondere.** I punti «ICAO» e «ordine» cambiano il **derivato**, e le pagine
pubbliche leggono lo snapshot congelato: sui documenti già pubblicati compaiono alla **prossima release**, non
al merge. E lo scaglione **«casa»** dell'ordinamento non è riproducibile sui dati di sviluppo di oggi (né LIBB,
né LIRR, né LIMM hanno in bozza un coordinamento interno alla propria ACC): è coperto da due test unitari,
**non** dallo schermo.

### B7 ✅ FUSO — `catalogo-punti-suggerimenti`, fuso in `main` la sera del 22 agosto 2026
Sette commit più il merge `2b4480d`, ramo cancellato (locale e origin).
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

### C6 🟢 La chiave di release ACC/APP è **derivata da un callsign**, e se il callsign si sposta il documento pubblicato va muto

Trovato il **25 agosto 2026** scrivendo la carta [documenti da
rivedere](feature/2026-08-25-documenti-da-rivedere.md) §3a. **Non** è un difetto introdotto da quel lavoro:
c'è oggi, in produzione.

`AccVipiReleaseTarget.TryDescribe` compone la chiave di release come `{acc}|{callsign del settore primario}`
(`AccVipiReleaseTarget.cs:57`); per l'APP non remotizzato la chiave **è** il callsign. Quelle chiavi non sono
stabili: le cambiano un settore riparentato, un primario che cambia, una rinomina in sorgente.

**Cosa succede quando si sposta.** Le `DocRelease` restano scritte sotto la chiave **vecchia**; `ManagedDoc`
descrive il documento con quella **nuova**; `PublicDocumentGate` chiede le release della nuova, non ne trova,
e il documento — pubblicato, con release valide in archivio — **sparisce dal pubblico**. Nessun errore,
nessun rilievo: la stessa famiglia di guasto del §0 di quella carta.

⚠️ **Latente, non manifesto**: nel `vipi.db` del 18 agosto le chiavi in archivio (`LIBB|LIBB_ES_CTR`,
`LIMM|LIMM_WS2_CTR`) **combaciano** con quelle vive. Va aperto perché è a un rename di distanza, non perché
stia bruciando.

**Rilevatore, già previsto**: il giro di deriva della casella impatti apre `ReleaseKeyMoved` quando il
bersaglio di oggi non ha release ma il documento ne ha sotto un'altra chiave (E11, slice 6). **Rilevare non
è riparare**: la riparazione — migrare le release alla chiave nuova, oppure rendere la chiave stabile — è
una decisione a sé, e va presa sapendo che una chiave stabile per l'ACC vorrebbe dire un identificativo
proprio del documento al posto del callsign.

### C7 🟢 I tre resti dell'analisi del 25 agosto sulla cancellazione dei dati importati

Analisi completa in
[`history/audit-2026-08-25-cancellazione-dati-importati.md`](history/audit-2026-08-25-cancellazione-dati-importati.md).
Sette rilievi: quattro chiusi con **E11**, uno è diventato **C6**, questi tre restano. Sono piccoli,
indipendenti fra loro, e ognuno ha il suo punto esatto nel codice.

**C7a — la policy di import cancellata torna «tutto importato» in silenzio.**
`EfImportPolicyStore.GetAsync` (riga 28) su riga assente ritorna `ImportPolicySnapshot.AllImported`: una
`DELETE` sulla tabella riporta il regime a «la sorgente può scrivere tutto», e il primo giro dopo
**sovrascrive TA e piste messe a mano**. Il dato per accorgersene c'è già — `GetInfoAsync` distingue «decisa
da qualcuno» da «nata dai default» (`UpdatedUtc == null`) — quindi basta un rilievo di diagnostica quando
almeno una categoria risulta manuale e nessuno l'ha decisa. ⚠️ Non è teorico: la riga è **una sola** in tutto
il database.

**C7b — le cancellazioni strutturali non lasciano traccia.**
`StructureEditingService.cs:127` (ACC), `:144` (aeroporto), `:297` (settore) non scrivono nel registro,
mentre l'eliminazione di un **documento** ci finisce dal 22 agosto. Serve `AuditAction.Delete` con
ICAO/callsign nei dettagli, scritto **prima** della cancellazione (dopo, il nome non è più leggibile — è la
lezione già pagata su `EliminaBozzaAsync`). È il **buco 5** dell'audit del 22 agosto, chiuso solo per
`SetParentAsync`. ⚠️ Da fare **dopo** E11, così riusa le stesse chiavi di frase.

**C7c — un ACC estero nuovo nasce con le aree accese.**
`EfNeighbourRepository.cs:53` e `:258` creano l'`Acc` estero senza toccare `SpecialAreasEnabled`, il cui
default d'entità è **`true`**: il giro delle 24h si porta dentro tutte le sue aree regolamentate. Il tappo
del 3 agosto (`OptOutForeignAreasAsync`) è **one-shot** e vale solo per gli esteri che c'erano allora. Una
riga: `SpecialAreasEnabled = false` alla creazione.

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
- ✅ ~~**Minime MVA da GitHub**~~ — **fatte il 22 agosto 2026, come CARTA e non come tabella.** Verificate
  live sulla copia del `vipi.db` reale.

  L'obiezione del 9 agosto era giusta e resta in piedi, ma riguardava **la tabella**: `area → quota` non si
  può ricostruire dal formato. L'etichetta è un testo piazzato a una coordinata sua, indipendente dai
  poligoni (in `liph.mva` le dieci `L;` stanno tutte in cima al file); il legame etichetta↔area va indovinato
  geometricamente e su 345 etichette **70 cadono dentro più aree annidate e 13 dentro nessuna**; il testo non
  è un numero (`TRL`, `NO MINIMA`, `80/TRL`, `*30/40`) e nessun campo dice le unità (`110` sono centinaia di
  piedi, `1500` sono piedi); e **92 tracciati su 315 sono aperti**, quindi non sono aree.

  Quello che il formato **dichiara** è un'altra cosa: il proprietario del file. `ENRMVA/{acc}.mva` è
  l'enroute di un ACC, `{icao}.mva` è un aeroporto — e a quella granularità l'attribuzione non si indovina,
  si legge. Misurato: i 24 file per-aeroporto corrispondono tutti a un APP esistente, zero orfani. Perciò la
  sezione mostra **una carta per file**, disegnata verbatim su fondo topografico (tracciati aperti compresi,
  etichette col testo originale). È esattamente ciò che il controllore vede in Aurora, e non asserisce nulla
  che il sectorfile non dica.

  Sezione `minima` da `Editorial` a `Derived`: riprende il toggle Live/Congelata e si congela nello snapshot
  di release. Nessuno storage — le tabelle `VectoringMinimaSet/Row`, che descrivevano la strada scartata,
  sono state droppate nello stesso giro (modello dati §7.5).

  ✅ **Chiuso il 23 agosto 2026 dal committente**: i 25 APP su 49 senza file (fra cui LIRF, LIMC, LIML,
  LIME, LIPS) sono **procedurali**, e una carta di minime di vettoramento **non ce l'hanno**. Quindi il file
  che manca non è una lacuna dell'archivio: è la risposta giusta, e la sezione che non compare è corretta.
  Non c'è nessuna richiesta da mandare all'AOD.
- 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso. Si sistemano
  dalla pagina: il filtro «solo da agganciare» li raccoglie.
- ⚠️ **La SID `BANA8A` di LIBD è GIÀ a `9000`** nel `vipi.db` di sviluppo (verificato il 23 agosto 2026):
  qualcuno l'ha corretta e nessun documento se n'era accorto. **Da rifare in produzione**, dove nessuno l'ha
  guardata.
  ⚠️ **Ma nello stesso aeroporto ce n'è un'altra, e non era in elenco:** `BANA5Z` (pista 25) ha
  `InitialClimb = "500"` → resa «500 ft», mentre tutte le altre BANAV stanno a 5000 o 9000. È quasi
  certamente uno zero perduto, ma **correggerla è una decisione editoriale** e la prende chi conosce la
  procedura. (Nel DB di sviluppo c'è anche `TESTE8A` a `80`, che però è una riga di prova, `IsImported = 0`.)
  ℹ️ Il valore non arriva dal sectorfile: `libd.sid` **non porta la quota iniziale**, la scrivono a mano gli
  editori — quindi è un dato che nessun import ricontrolla, e nessun import sovrascrive.
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

✅ **DECISA il 22 agosto 2026 (sera) dal committente: lo staff di divisione è admin, tutto.** Il default di
`Division:AdminRolePatterns` è ora il jolly `[A-Z0-9]+`, cioè `^IT-[A-Z0-9]+$` (carta: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md) §1): i quattro codici scoperti
entrano, e soprattutto **un ruolo nuovo della divisione non nasce più escluso** — l'elenco puntuale
sbagliava in silenzio, e se ne accorgeva solo chi restava fuori. Il jolly **non allarga oltre la divisione**:
un codice `IT-…` lo assegna il portale IVAO solo allo staff di divisione, e il prefisso resta la barriera
(`DE-DIR` non è admin qui). Il lato chief ACC non cambia e **resta l'unica ipotesi non verificata**.

⚠️ **Il rilievo «nessun admin fra gli staffisti conosciuti» ora suona molto più raramente**, ed è voluto: con
un jolly, per non avere nessun admin serve che i codici siano *malformati* o di un'altra divisione — cioè il
guasto vero, non una lista incompleta.

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
- ✅ **Test property-based sull'AoR** — **fatti il 23 agosto 2026** (`AorProiezioneProperties`, CsCheck 4.8,
  pacchetto senza dipendenze). Sei proprietà sulla proiezione: nessun punto fuori dal riquadro, lato lungo
  sempre 400 (cioè scala uniforme), invarianza alla traslazione in longitudine, rapporto fra i lati uguale a
  quello dell'estensione proiettata, `ProjectShared` di un poligono solo uguale a `Project`, e meno di tre
  punti ⇒ `null`.
  ⚠️ **Scrivendoli è uscito un difetto di documentazione**: il commento di `AorPolygonProjector` diceva coppie
  `[lat,lon]`, mentre il formato IVAO `regionMapPolygon` mette la **longitudine prima** (lo fa
  `ParsePoints`, e i test esistenti lo sapevano). Chi ne avesse ricavato una fixture avrebbe scritto un
  poligono **ruotato di 90°**, e la proiezione non se ne lamenta: disegna. Commento corretto.
  ⚠️ **Sono test non deterministici per costruzione**: i casi cambiano a ogni giro, quindi un rosso può
  comparire su un codice fermo. Non si rilancia finché passa — è un controesempio nuovo: il seed sta nel
  messaggio, si riproduce con `-e CsCheck_Seed=…` e si congela in un test a esempio.
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
9. ✅ **Il blocco al deploy è sciolto — aggirandolo, non risolvendolo** (23 agosto). Resta vero che le
   migrazioni girano all'avvio e che `AgreementSectionsFinalize` fallisce su un archivio non convertito; la
   consegna del 23 agosto (**A11**) però **sostituisce** il database invece di migrarlo, e un `.sql` che
   porta con sé la storia delle migrazioni non lascia niente da applicare. La conversione in posto — backup
   → additiva → `tools/Vipi.AgreementsToSections --mysql` → finale — resta scritta in
   [`history/handoff-accordi-coordinamento.md`](history/handoff-accordi-coordinamento.md) e **torna
   necessaria** il giorno in cui l'archivio di produzione conterrà qualcosa che non si può ributtare via.

⚠️ **Due difetti trovati eseguendo, e che nessun test vedeva** — valgono fuori da quest'area: fra le due
migrazioni lo schema è **misto**, e cancellare un guscio si portava via clausole già riappese correttamente (60
→ 23, in silenzio); e lo scaffolding EF ha proposto, per la seconda volta su quest'area, un `RenameColumn` che
avrebbe prodotto dati **validi e sbagliati** (`AgreementId` spacciato per `SectionId`). Le migrazioni si
leggono, non si accettano.

### E6-ter ✅ CHIUSA il 23 agosto 2026 — non era un test ballerino, era un **difetto del client**
Carta: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md) §4.
Inseguita dall'11 al 22 agosto come un problema di tempi — «il thread-pool sotto carico», «la prova dipende
dai tempi del socket» — e per due volte la cura proposta è stata allargare l'attesa. Non era quello.

`AuroraClient.SendAsync` **si connetteva prima di prendere il turno**. Due invii lanciati insieme trovavano
entrambi «non connesso» — l'assegnazione avviene dopo `ConnectAsync`, che cede il controllo — e aprivano
**un socket a testa**; il secondo chiudeva il primo. E peggio: `stream` e canale delle righe si leggevano in
**due istruzioni separate**, quindi un invio poteva scrivere su un socket e aspettare la risposta sul canale
dell'altro. Nessuna delle due arrivava a destinazione: **silenzio fino alla scadenza**, cioè esattamente
«Nessuna risposta a #TRPOS entro 15000 ms» — che sembrava lentezza e non lo era.

Adesso la connessione è **un oggetto solo** (socket + flusso + canale + ciclo di lettura), letta in un colpo,
e si apre **dentro** il turno. Visto fallire e visto passare: col client di prima **200 giri su 200**
aprivano due connessioni, e la prova ci metteva 3 minuti e 10; col client nuovo, **133 ms**.

⚠️ **La lezione, che vale oltre questo caso:** il test lo vede solo se i due invii partono *davvero* insieme
(due thread e un cancelletto che li rilascia). Chiamati in sequenza sullo stesso thread, su loopback la prima
connessione fa in tempo e **il difetto non si vede** — ed è per questo che per undici giorni è sembrato un
problema di tempi. Un rosso a intermittenza merita di essere letto nel codice prima che nel calendario.

---

### E7 ✅ Login — **chiusa il 24 agosto 2026**: `OnRemoteFailure` c'è, e il guasto ora si vede

**Cosa è stato fatto.** `oidc.Events.OnRemoteFailure` è registrato in `VipiStandaloneAuthExtensions`:
logga la ragione sotto la categoria fissa **`Vipi.Auth.Ivao`** (motivo, errore del portale, se lo stato del
giro si è recuperato, se c'era già una sessione, dove si stava andando — mai il `code` né i token), poi
decide invece di rilanciare. Due esiti: **sessione già attiva ⇒ si torna al `returnUrl`** (è il caso del
23 agosto, dove l'utente vedeva `Error.` ed era dentro); **nessuna sessione ⇒
`/services/vsop/auth/accesso-non-riuscito`**, una pagina che dice cosa è successo e offre un «riprova».

⚠️ **La pagina non rimanda da sola al login, ed è una scelta.** Se il guasto è stabile — il portale che
risponde `access_denied` — il rimbalzo automatico diventa un anello infinito, perché IVAO ha già la sessione
aperta e rispedisce indietro subito. Il secondo tentativo lo chiede l'utente, con un clic.

⚠️ **`context.HttpContext.User` è vuoto dentro `OnRemoteFailure`**, e crederci sarebbe stato un errore
silenzioso: il gestore del callback gira dentro `UseAuthentication` **prima** che il middleware monti
l'utente dello schema di default. La sessione esistente va chiesta a mano con
`AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)`.

**Il motivo è un insieme chiuso** — `portale`, `correlazione`, `nonce`, `sconosciuto` — perché serve a due
padroni: finisce nel log **e** sceglie la frase in pagina. Niente testo di provenienza esterna arriva allo
schermo; il `returnUrl` passa comunque da `SafeReturn` ed è codificato per attributo.

ℹ️ **`Unable to unprotect the message.State.` sta in `correlazione`, e vale la pena saperlo**: è il messaggio
misurato sul flusso vero, ed è anche il sintomo di un **key-ring perso** (`public_atc/vipi-keys`, una delle
tre cose che un FTP distratto cancella). Se *ogni* login esce con quel motivo, si guarda lì prima che ai
browser degli utenti.

**Verificato**: 20 test nuovi (`tests/Vipi.E2E.Tests/LoginFailureTests.cs`), e soprattutto un guasto **vero**
provocato in locale — `GET /signin-oidc?state=abc&code=def` senza cookie di stato — che prima finiva su
`/Error` e ora esce **302** verso la pagina, lasciandosi dietro la riga di log che il 23 agosto non c'era.
Suite intera: **3621 verdi**, 0 avvisi.

<details><summary>Il testo originale della voce, com'era prima della chiusura</summary>

### E7 🟢 Login: manca `OnRemoteFailure`, e il cookie della build vecchia fa fallire il primo accesso
Trovato in produzione la sera del 23 agosto, segnalato dal committente: dopo il login compare la pagina
`Error.` generica, ma **al refresh risulta loggato**.

**Cosa succede davvero.** L'URL della pagina d'errore è il **callback** (`/signin-oidc?state=…&code=…`),
quindi su quella richiesta l'accesso **non si è completato**. Sembrava riuscito perché il cookie `vipi.auth`
è **persistente, 7 giorni, sliding**: era già loggato da prima.

**La causa, e si consuma da sé.** In **incognito il login fila liscio** — quindi i login nuovi non sono
rotti, e la differenza fra le due finestre è solo il cookie. La build in produzione fino a quella sera
(15 agosto) usava `oidc.ClaimActions.MapAll()`: nel cookie finiva l'**intero profilo IVAO** — `hours[]`,
`rating{}`, `groups`, `userStaffDetails`, `userStaffPositions` (~1,5 kB) — cioè un cookie che ASP.NET spezza
in più pezzi (`vipi.authC1`, `C2`, …). La build del 23 ne mappa **sei campi**. Chi era loggato da prima si
porta dietro il cookie grasso, e il callback fallisce; chi entra per la prima volta no. **Rimedio per
l'utente: uscire e rientrare** (o cancellare i cookie del sito). Colpisce solo chi era loggato prima del
23 agosto, e sparisce da sé al primo logout o alla scadenza dei 7 giorni.

**Il difetto da chiudere, che è un altro.** In tutta la storia del repo **non è mai esistito un
`OnRemoteFailure`** (`git log -S` su tutti i commit: zero). Qualunque guasto dentro il flusso IVAO —
correlazione fallita, cookie del `nonce` mancante, errore restituito dal portale — esce come **eccezione non
gestita** e finisce su `UseExceptionHandler("/Error")`: una pagina che non dice niente all'utente e non
lascia niente a noi. Il costo è stato pagato: la causa è stata ricostruita **dagli `scope` dentro il `code`
OIDC**, non da un log.

**Cosa fare:** registrare `OnRemoteFailure` — logga la ragione, e se l'utente è già autenticato reindirizza
al `returnUrl` invece di lanciare; altrimenti rimanda al login con un messaggio leggibile. ⚠️ Tocca
`Vipi.Host.dll`, quindi vuole un giro di ripacchettamento: da fare **fuori** da un deploy a metà.

ℹ️ Sospettato secondario, non escluso: il `nonce` è stato **acceso il 22 agosto** e la sera del 23 è la
**prima volta che gira in produzione**. Se il cookie del nonce manca al ritorno, `base.ValidateNonce` lancia
— e lancia esattamente così. Il `OnRemoteFailure` è anche ciò che renderebbe distinguibili i due casi,
perché oggi la ragione non la vede nessuno.

</details>

### E8 ✅ «Error.» a chi entra per la prima volta — chiusa il 24 agosto 2026, ramo `login-utente-nuovo`

**Segnalato dal committente** il 24 agosto: un socio **senza incarichi** vede la pagina `Error.` su
`/services` — l'elenco degli strumenti, che non legge una riga di database — mentre lo stesso indirizzo,
**da non collegato, risponde 200** (verificato con curl sulla produzione, mentre la segnalazione arrivava).

**La deduzione, che vale più della causa.** Su quella pagina l'unica cosa che un utente **loggato** fa in
più di un anonimo era, in `SopLayout`:

```csharp
_canEdit = _user is not null && (_isAdmin || (await Editing.ListEditableDocumentsAsync()).Count > 0);
```

L'anonimo esce dalla condizione **prima** del database; l'admin esce su `IsAdmin`, che guarda codici staff
in memoria; **paga il giro solo il socio qualunque**. E il giro era **N+1**: tutti i documenti, più due
query di autorizzazione per ognuno, a **ogni** pagina, per accendere un tasto. Conseguenza: qualunque
intoppo del database — connessione caduta, pool esaurito, comando scaduto, riavvio a freddo di Passenger —
non spegneva un tasto, **buttava giù tutta la pagina, per i soli utenti collegati**, mentre ogni sonda
anonima continuava a dire che il sito era su.

**Chiuso così:**
- `IEditAuthorizationService.CanEditAnythingAsync` — **una query sola** (admin, oppure almeno una
  concessione). `ListEditableDocumentsAsync` non aveva altri chiamanti ed è stato rimosso.
  ⚠️ La semantica si sposta di un filo: chi ha una concessione su una ACC **senza documenti** ora vede il
  tasto e trova un elenco vuoto. È scritto nel contratto.
- Se quella domanda **fallisce**, il tasto resta spento e la pagina esce. È l'eccezione motivata alla regola
  «non si ingoiano gli errori»: il **contorno non decide se la pagina esiste**. Non è muta — va nel log.
- `PaginaErrore` prende il posto della `Error.razor` del modello: HTML scritto a mano, come
  `IvaoLoginFailurePage` e per lo stesso motivo — deve reggere anche quando a lanciare è stato il **layout
  condiviso**, che è esattamente il caso di oggi.
- `DiagnosticaErrori` scrive ogni richiesta fallita in `diagnostica/errori-richieste.txt` con **lo stesso
  codice mostrato in pagina**, il percorso vero, il **VID** e lo stack trace: dalla fotografia che arriva
  su WhatsApp si risale all'eccezione. Niente query string (su `/signin-oidc` è una credenziale), niente
  cookie, niente intestazioni. Lo stato resta **500**.

⚠️ **Tre trappole trovate scrivendolo:**
1. Dentro un `IExceptionHandler`, `ctx.Request.Path` vale **già** `/Error`: il middleware riscrive il
   percorso prima di chiamare i gestori. Il percorso vero sta in `IExceptionHandlerPathFeature.Path`, e
   senza quello il registro direbbe sempre «/Error» — inutile, e in modo silenzioso.
2. Il **VID** nel registro è ciò che rende leggibile un guasto «che si vede solo da loggati». Si legge dai
   claim e non da `ICurrentUserProvider`: dentro la gestione di un guasto, risolvere un servizio è un modo
   in più di fallire.
3. I test stanno in ambiente **Staging**: `UseExceptionHandler` è montato solo fuori da Development.

> ### ⚠️ CORREZIONE del 24 agosto sera — la causa era un'altra, e l'ha detta il registro
>
> Poche ore dopo il deploy il difetto è **ricapitato**, e questa volta ha lasciato le sue righe:
> **92 richieste fallite fra le 16:55 e le 17:07**, tutte con lo stesso stack.
>
> ```
> System.InvalidOperationException: A second operation was started on this context instance
>    at Vipi.Infrastructure.Persistence.EfStationDirectory.ListAccs()
>    at Vipi.Application.Content.StationResolver.get_Accs()
>    at Vipi.Ui.Shared.SopLayout.BuildRenderTree()
> ```
>
> **Il colpevole è il CATALOGO, non `_canEdit`.** `Stations.Accs` è a caricamento pigro e il markup lo
> leggeva **dentro** `BuildRenderTree`; Blazor disegna l'albero mentre `OnParametersSetAsync` è ancora in
> volo, quindi quella query partiva sullo stesso `DbContext` su cui era già in corso quella di `_canEdit`.
> `_canEdit` **non fallisce**: apre la finestra. La guardia messa al mattino non poteva prenderlo, ed è
> esattamente per questo che è ricapitato.
>
> ⚠️ **Non era «a intermittenza»: era sistematica per una classe di utenti**, e la prima stesura di questa
> voce sbagliava a dire «solo a freddo, dopo ogni riavvio». Il catalogo è **Scoped**, quindi per una pagina
> SSR la sua cache è fredda a **ogni** richiesta: non è lì la variabile. La variabile è se c'è un `await`
> **in volo** quando parte il render — e `ComponentBase` il render lo fa comunque, subito dopo
> `OnParametersSetAsync`, aspettando il task solo se non è già completato.
>
> | Chi | `_canEdit` | Task | Esito |
> |---|---|---|---|
> | anonimo | non chiesto | — | nessuna corsa |
> | **admin** | risposto dai claim (`IsAdmin`) | **già completato** | nessuna corsa |
> | **socio senza incarichi** | query vera sul database | **in volo** | **corsa, ogni volta** |
>
> **I numeri lo confermano**: delle 92 righe, le **78 della corsa vengono tutte dallo stesso VID
> non-admin**; l'admin che navigava nella stessa finestra ne ha prodotte **zero** (le sue 4 sono della
> Fase 2, i timeout). Ecco perché il difetto lo vedeva un socio e a noi non capitava mai — e perché
> «riprova, a me funziona» non l'avrebbe mai smentito.
>
> **Chiuso** chiamando `Prewarm()` prima di qualunque `await` e facendo leggere al markup un campo: il
> render non tocca più il database. `Prewarm()` era già scritto per questo — «chiamata dal ciclo di vita
> async, context libero e sequenziale» — e non lo chiamava nessuno.
>
> ℹ️ **La seconda metà di quella finestra è un'altra cosa**: dalle 16:59 alle 17:07 le 11 righe rimaste sono
> `RetryLimitExceededException`/Timeout, e colpiscono **anche gli anonimi**. Lì il database non rispondeva
> più. Le due fasi sono consecutive e la prima è una causa plausibile della seconda — 78 richieste fallite
> in due minuti e mezzo, ognuna con una query abbandonata a metà — ma **plausibile non è misurato**, e senza
> i log del server MariaDB resta un'ipotesi.
>
> ℹ️ Trovato scrivendo i test: `StaffLoginTrackingMiddleware` proteggeva solo la scrittura, non
> `users.Get()`. Un guasto lì usciva, il gestore rieseguiva `/Error`, il middleware girava **di nuovo**
> sulla richiesta rieseguita e lanciava una seconda volta: non usciva nemmeno la pagina d'errore. Un pezzo
> che gira prima del routing gira anche sulla via di fuga. Ora è protetto per intero.

**Non era provato che fosse questa la causa di quel preciso 500** — e infatti non lo era. — sul server non c'era una riga da leggere,
ed è il motivo per cui la seconda metà della voce esiste. È provato il **meccanismo**: era l'unica strada
per cui una pagina senza dati potesse morire per un utente collegato e non per un anonimo. Contesto della
giornata: il pacchetto «e» era stato caricato quel pomeriggio, e Passenger riavvia il processo per
inattività — i **riavvii a freddo sono frequenti**, ed è la finestra in cui un intoppo del database è più
probabile.

**Chiesto dal committente mentre si preparava il pacchetto, e sta bene qui**: la **versione in barra**, ai
soli admin. La domanda era «che versione del sito è online?», e non aveva risposta — `AssemblyVersion` è
`1.0.0` in ogni pacchetto, e la data in `avvio-diagnostica.txt` dice quando è *ripartito*, non *che cosa*:
per giunta si rinfresca da sé, perché Passenger riavvia il processo per inattività. Ora la build si timbra
col **commit** (non con l'ora di compilazione: ricompilare lo stesso codice deve dare la stessa versione) e
con la **lettera del pacchetto**, passata al publish come `-p:VipiPacchetto=g`. In barra `g · e8fc4a2`, il
resto nel `title`; la stessa riga apre `avvio-diagnostica.txt`.

⚠️ Tre scelte, tutte a difesa di qualcosa: **solo admin** (a un socio non dice niente, a chi passa dice con
quale build sta parlando); **prima cosa a uscire** dalla barra quando lo spazio manca, che è già a corto;
e senza timbro si scrive **«sviluppo»** invece di inventare un numero — a una versione si crede.
⚠️ Niente marcatore «albero sporco»: `git status` in forma breve elenca anche i file che differiscono per i
soli fine riga, e un allarme che suona sempre non è un allarme.

**Verificato**: `BarraNonAffondaLaPaginaTests` (7), `PaginaErroreTests` (2), `VersioneBuildTests` (6) e due
sull'HTML servito (l'admin vede la targhetta, il socio no); senza la guardia i due casi del guasto tornano
500, cioè lo screenshot. Propagata `CanEditAnythingAsync` ai 12 finti
`IEditAuthorizationService` dei test. Suite intera verde su entrambi i TFM.

### E9 🔴 APERTA — la corsa sul `DbContext` c'è ancora, e non so ancora chi sia l'altra operazione

**Misurato in produzione alle 17:44 del 24 agosto 2026**, con il pacchetto «h» già online (quindi con la
correzione del catalogo dentro): il registro, che il committente aveva appena azzerato, si è riempito di
nuovo. **Sette richieste**, tutte dello stesso VID **non-admin**, tutte «A second operation was started on
this context instance», ma questa volta **dentro le pagine**:

| Percorso | Dove muore |
|---|---|
| `/services/vsop/libb`, `/lirr` (×3) | `AccLanding.OnParametersSetAsync` → `EditAuthorizationService.CanEditAccAsync` |
| `/services/vsop/libb/airports` (×2) | `AeroportoPage.OnParametersSetAsync` → `AirportPresidencyService.ResolveAsync` |
| `/services/vsop/limm/airports` (×2) | `AeroportoPage.OnParametersSetAsync` → `EfStructureEditingRepository.LoadAsync` |

**Quello che si sa**: la seconda operazione è quella della pagina. **Quello che NON si sa**: quale sia la
prima, cioè chi tenesse occupato il contesto in quel momento. Lo stack dell'eccezione mostra solo chi è
morto, non chi stava già correndo.

⚠️ **L'ipotesi naturale — il layout che lascia `_canEdit` in volo mentre il `@Body` viene disegnato — NON è
stata riprodotta.** Tentativi fatti, tutti falliti nel senso che il test resta verde anche col difetto
dentro:

1. intercettore EF che rallenta ogni comando ⇒ **non scatta**: gli `IInterceptor` registrati in DI non
   arrivano al contesto in questo assetto (da capire perché);
2. `HasAnyGrantAsync` sostituita con una query **lenta davvero** (ricorsiva SQLite da tre milioni di giri)
   sullo stesso `DbContext` ⇒ la pagina non si è mai sovrapposta;
3. ⚠️ e il primo tentativo era sbagliato in modo istruttivo: avevo sostituito **tutto** il repository, così
   anche la query della pagina diventava finta e non toccava il contesto. Un test che non può fallire.

Quindi: o in SSR il render del corpo aspetta davvero il task del layout (e allora l'altra operazione è
qualcun altro), o la finestra si apre solo sotto condizioni che il locale non riproduce.

**Cosa è stato fatto comunque** (`SopLayout.SetParametersAsync`): il layout conclude il proprio lavoro
asincrono **prima** di far disegnare l'albero, così non può essere lui la prima operazione. È corretto in
sé e toglie una fonte possibile — **ma non è provato che sia LA causa**, e va scritto così finché non lo è.

**Fatto entrambi, pacchetto «i»:**

1. **Scope proprio** (`OwningComponentBase`) per `AccLanding` e `AeroportoPage`, le due che compaiono nel
   registro — il rimedio già adottato da sei componenti dopo l'audit del 30 luglio. Chiude la **classe** del
   guasto senza dipendere da chi sia l'altra operazione, che è il punto: quello non si è capito.
   ⚠️ `IStationResolver` NON si sposta: il layout l'ha già scaldato, e riprenderlo dallo scope nuovo
   vorrebbe dire ripagare la stessa query a freddo.
2. **Il registro dirà chi c'era già**: un intercettore EF annota inizio e fine di ogni comando col
   chiamante, e all'istante del lancio (`FirstChanceException`) si fotografa che cosa è aperto.

⚠️ **Tre cose imparate costruendo il punto 2, tutte contro-intuitive:**
- il **rilevatore di concorrenza di EF scatta prima dell'esecuzione**, quindi la seconda query non arriva
  mai all'intercettore: cercare lì la collisione non avrebbe mai visto niente;
- **aspettare il gestore d'errore è tardi**: mentre l'eccezione risale, la prima operazione fa in tempo a
  chiudersi e la lista torna vuota. `FirstChanceException` è l'unico istante in cui la scena è intatta;
- il rilevatore **non copre i comandi grezzi** (`ExecuteSqlRawAsync`): il modo ovvio di provocare una corsa
  in un test non fa scattare niente, ed è per questo che il test costruisce la scena a mano.

**Resta da chiudere la voce**: serve un socio senza incarichi che apra `/services/vsop/{acc}` e la pagina di
un aeroporto dopo il caricamento di «i». ⚠️ Da admin non prova niente.

### E10 📋 CARTA — Biblioteca allegati: i PDF su Drive, linkati dai documenti

Chiesto dal committente il **25 agosto 2026**. Carta scritta e approvata nelle decisioni,
**nessuna slice avviata**, nessuna riga di codice: `docs/feature/2026-08-25-biblioteca-allegati.md`.

**Il vincolo che ha deciso il deposito.** I byte **non stanno da noi**: il piano di hosting **non ammette il
formato PDF** — ⚠️ vincolo **contrattuale**, quindi *non* si aggira mettendoli in MariaDB come blob, sarebbe
elusione — e IVAO HQ indica di tenere i documenti sul **Drive di divisione**. Il deposito è quindi Drive;
noi teniamo metadati, organizzazione, versioni e registro dei link.

⚠️ **Conseguenza**: il file Drive è «chiunque abbia il link», quindi **tutto ciò che entra in biblioteca è
pubblico**. Allegati riservati allo staff **non sono supportati** — un controllo di accesso davanti a un URL
Drive pubblico sarebbe teatro. Confermato dal committente che non servono.

**Le sette decisioni prese** (tutte approvate):

1. modello a **due livelli**: `Allegato`(slug stabile) → `AllegatoVersione` → file Drive. I documenti citano
   lo **slug**, mai il file: altrimenti sostituire un PDF vuol dire riaprire tutti i documenti che lo citano;
2. **l'identità del link è nostra**: `/vsop/files/{slug}` → 302 verso Drive. Cambiare deposito domani (o
   riportarli in casa, se l'hosting cambia) non tocca **un solo documento** — è una colonna in una tabella;
3. il registro «usato in N» si **ricava** dalle quattro fonti che `EfMediaMaintenance.ReferencedShasAsync`
   già scansiona, **mai** una tabella di join: quella si desincronizza e mente proprio quando serve;
4. il link segue **sempre la versione corrente**, release comprese. La regola di casa è già in
   `DocRelease.cs` (la release congela le *scelte editoriali*, non i cataloghi esterni), e congelare avrebbe
   un difetto pratico grave: una scansione sbagliata già pubblicata si correggerebbe solo **ripubblicando
   tutti** i documenti che la citano;
5. biblioteca a **due assi** (tipo × ambito) + ricerca, non cartelle: un albero a 50+ file si riempie di roba
   archiviata male;
6. **due** modi di linkare: blocco «Allegato» e `[testo](allegato:slug)` inline, con **un token solo** in
   entrambi → una sola regex per lo scanner;
7. v1 = caricamento **a mano** su Drive; l'API Drive (service account sul drive condiviso) è rimandata.

**Le due trappole trovate leggendo il codice:**

- ⚠️ **`MarkdownLite.cs` non ha link di NESSUN tipo.** Il link inline va aperto **solo** allo schema
  `allegato:`: il renderer fa HTML-encode e poi regex, quindi aprirlo a `[testo](url)` qualunque farebbe
  entrare `javascript:` e link esterni arbitrari nel contenuto editoriale.
- ⚠️ `/vsop/files/{slug}` **non può essere `immutable`** come `/vsop/media/{sha}`: sostituisci il PDF e il
  browser tiene il vecchio per un anno. Va `no-cache`. È ciò che renderebbe la sostituzione «non
  funzionante» in modo intermittente e inspiegabile.

**Debito annotato, non aperto**: i punti di dispatch su `BlockFormat` sono **9 file**, e questa è la
**seconda** feature che vi aggiunge un `case` (la prima furono le immagini). La regola del 2 del gate è
superata da un pezzo, ma aprire il registry dei formati dentro una feature sarebbe il refactor trasversale
che il gate vieta. **Alla terza volta si apre.**

**Verifiche fatte il 25 agosto, prima di sospendere:**

- ✅ **Il reconciler Postgres crea le tabelle**: R3 della carta era copiato dal doc immagini del 31 luglio ed
  **era già chiuso** dal commit `eac14fd`. `CreateTableStatements` genera la DDL dal diff del modello EF, con
  tre test in `PostgresSchemaReconcilerTests`. Le tabelle nuove nascono da sole. Riguarda comunque solo
  Render/Neon: SQLite e MariaDB vanno di migrazioni versionate.
- ✅ **Revisioni Drive**: le purgabili durano **~30 giorni**, o meno con 100 revisioni non marcate; fino a
  **200** si marcano «Keep Forever» e occupano quota. ⚠️ Ma **la revisione di testa non è mai purgata**: la
  versione *corrente* — l'unica che i documenti servono — è al sicuro senza spuntare niente. A scadere sono
  solo i byte delle versioni passate, già fuori perimetro (`AllegatoVersione` registra **chi, quando e
  perché**, non promette di riscaricare la v1).

**Resta da chiedere a Ivao.It**: il drive condiviso ha politiche di **pulizia periodica**?

**Da dove si riparte**: slice 1 della carta — entità `Allegato` + `AllegatoVersione` e migrazioni **×2**
(SQLite + MySQL). ⚠️ Additive: una colonna nuova non la riempie nessuno da sola.

**Fuori perimetro, deciso**: `RealDOCS/IPI Roma ACC.pdf` (**180 MB**) e gli altri monoliti — sono i documenti
che il sito **sostituisce**, non allegati.

### E11 ✅ FATTA il 25 agosto 2026 — Documenti da rivedere: la casella degli impatti

Chiesto dal committente il **25 agosto 2026**, dopo l'analisi su *cosa succede se un dato importato viene
eliminato dal DB*. Carta (v2, rivista dopo revisione avversariale):
`docs/feature/2026-08-25-documenti-da-rivedere.md`.

**La domanda**: quando un settore sparisce, un'area cambia o un admin nasconde una postazione, **quali
documenti vanno rivisti o ripubblicati?** Oggi la risposta esiste per **un** caso su venti — `AccAdminService.cs:101`,
subcenter ACC nascosto — e per gli altri diciannove il sistema tace.

**Sei decisioni** (§3 della carta): tabella `DocumentImpact` a molte righe aperte per documento; rivelatore
per **deriva** calcolata (giro notturno, non solo eventi); il legame al documento **si tiene** finché
l'admin conferma; perimetro settori + aree; ancora sul **`DocumentId`** e non sul bersaglio di release
(la chiave è instabile, vedi **C6**); sezioni `Live` gestite alzando la **severità**, non con un watermark.

⚠️ **Tre cose che la revisione ha ribaltato** rispetto alla prima stesura, e che vale la pena non riscoprire:

- il reverse-lookup esistente (`EfDocumentReviewRepository.cs:31-37`) **sovra-segnala** — `IsPrimary || Type == App`
  dentro l'ACC significa *ogni* documento primario e *ogni* APP dell'ACC — e **sotto-segnala**: non guarda
  `Airport.DocumentId`, quindi uno scalo come LIBG non produce nessuna riga. Va riscritto **prima** di tutto (slice 0);
- `ProjectVipiSectors` gira a **ogni avvio** (`VipiModuleExtensions.cs:480`): con un catalogo vuoto o
  parziale — DB appena sostituito, import fallito — ogni settore proiettato risulta orfano. Serve la guardia
  «catalogo vuoto → nessun impatto» + soglia di massa, come già fa l'import aree;
- `ImportSpecialAreasAsync` fa `updated++` **senza confrontare niente** (`EfAccAdminRepository.cs:128-137`):
  «aggiornata» ≠ «cambiata», e senza confronto campo per campo la casella si riempie ogni notte.

**Misure che hanno deciso** (`vipi.db` del 18 agosto): 15 documenti, 34 release, **5** sezioni `Live` in
tutto — quattro `sids` (default, e il loro cambio è cadenzato dall'AIRAC via `SidRow.IsPublicAt`) e **una**
`coordination` manuale. È il dato che ha tolto di mezzo il watermark.

**Da dove nasce**: l'analisi
[«cosa succede se un dato importato viene eliminato dal DB»](history/audit-2026-08-25-cancellazione-dati-importati.md),
sette rilievi. Questo giro ne chiude **quattro** (documento sganciato, `BrokenTarget`, pre-check dei
`Restrict`, e la rinomina di §16); uno è diventato **C6**; gli altri due, più il terzo lasciato fuori dal
perimetro, stanno in **C7** — con il loro punto esatto nel codice, pronti da prendere.

**Che cosa c'è adesso**

| | Dove si vede |
|---|---|
| Banner **multi-riga** nell'editor, un rigo per fatto, col ✓ solo sulle righe non calcolate | i 4 editor |
| Pill di riepilogo | `/services/vsop/versions` |
| Sezione **«Orfani»**: elenco, documenti toccati, **riaggancia** e **rimuovi** | `/services/vsop/admin/sector-structure` |
| Conteggio per tipo + ultimo giro della deriva | `/services/vsop/admin/diagnostics` |
| Giro notturno (`ImpactDriftHostedService`, 24h, parte 100s dopo il boot) | — |

**Verificato sui dati veri** (copia del `vipi.db` di sviluppo, §14 della carta): settore cancellato → legame
al documento **conservato** + 2 segnalazioni; callsign che torna → righe **chiuse dal calcolo**; area
cambiata → 5 documenti avvisati; aeroporto scollegato → `BrokenTarget`; rimozione di un orfano bloccato →
**rifiutata con la frase**. ⚠️ Per copiare quel DB servono anche i file `-wal` e `-shm`, o SQLite lo dichiara
*malformed*.

**Trovato eseguendo, e non era nel piano**: la sentinella «riga aperta» **non può essere `0001-01-01`** — il
`DATETIME` di MariaDB parte dal 1000 e in `sql_mode` stretto lo rifiuta, mentre SQLite lo accetta: suite
verde e produzione rotta. È l'epoca Unix. Vedi §13.1 della carta.

**⚠️ 25 agosto, sera — la RINOMINA** (carta §16). Domanda del committente: «se `LIRN_US0_APP` diventa
`LIRN_US1_APP`, che succede?». Misurato: **peggio** della cancellazione. L'import fa upsert del nome nuovo, la
riga vecchia **resta** (i cataloghi non potano mai), e quindi restano **due settori attivi** con la stessa
shape: il documento è sul fantasma, chi controlla si connette col nome nuovo che non ha documentazione, e
**nessuna** delle otto famiglie di impatto se ne accorge — non è sparito niente.

Non era teoria: `LIED_G_APP`, l'APP primario di Decimomannu, aveva il timbro del **5 agosto** contro il **24**
delle altre tre posizioni dello stesso scalo. Diciannove giorni da fantasma.

Fatto, senza potare i cataloghi: il segnale è il **timbro** `ImportedAtUtc`, che il giro giornaliero riscrive
su tutto ciò che la sorgente elenca ancora. Nasce `ImpactKind.SectorStale`, la sezione «Orfani» mostra il
motivo **«non più elencato»** con la data, e — quando il candidato è **uno solo** — propone *«forse rinominato
in …»*. ⚠️ Proposta e mai automatismo: la cifra in `US0`/`US1` di solito vuol dire **sdoppiamento**, e i due
casi sono indistinguibili dai dati.

Tre guardie: righe **aggiunte a mano** escluse (colonne `IsManual` nuove + backfill one-shot che le riconosce
dal prefisso), niente segnalazioni senza l'ultimo giro riuscito di entrambe le famiglie, e **silenzio se gli
stantìi superano un quarto del catalogo** — quest'ultima l'ha imposta la prova sui dati veri, dove una
simulazione storta ha fatto comparire trenta settori esteri in blocco.

Verificato sull'archivio vero simulando un giro d'import completo: **una sola riga**, `LIBD_CS0_APP`, con
«forse rinominato in `LIBD_CS1_APP`».

**Restano fuori, dichiarati**: policy di import cancellata → «tutto importato» muto; ACC estero nuovo che
nasce con le aree accese; **audit delle cancellazioni strutturali**; notifiche (la casella è passiva) e il
legame con la scadenza AIRAC; famiglie oltre settori e aree (TA, piste, SID, shape); **watermark** delle
sezioni Live, con la soglia scritta in §3b. E dal giro della rinomina: il gesto «sposta i legami» **non**
sposta le citazioni per Id (accordi, parti di vLOA, blocchi) né ripunta i `ParentCallsign` dei figli — le
prime restano come bloccanti della rimozione, i secondi si sistemano a mano dalla Struttura. Sotto tutto
questo resta la domanda vera: **il callsign non è un'identità stabile** e la sorgente non ne espone un'altra
(`Sector.FacilityId` esiste dal primo giorno e non lo scrive nessuno).

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

---

## H. Frontend/UI — l'audit del 23 agosto, e ciò che è arrivato dopo

Carta ed esito per esteso in [history/audit-2026-08-23-frontend-ui.md](history/audit-2026-08-23-frontend-ui.md).
Quindici voci, tredici chiuse in giornata sul ramo `audit-frontend-ui` (sei commit, 3.595 test verdi,
verifica live fatta). ✅ **Il ramo è stato fuso in `main` la sera del 23 agosto**, ed è il codice della
consegna (**A11**). Qui restano le due che **non** sono state chiuse, e il perché — più **H3**, che
dell'audit non fa parte: è saltata fuori verificando il lavoro sui coordinamenti live dello stesso giorno
([feature/2026-08-23-live-coordinamenti-a-colonne.md](feature/2026-08-23-live-coordinamenti-a-colonne.md)),
ed è un difetto che stava lì da prima.

**La sezione è diventata il posto dove finisce l'UI aperta**, non solo l'audit di quel giorno. Stato al 25
agosto, sera: **H1** e **H3** aperte come allora · **H2** ✅ **chiusa** — erano due difetti, non uno, e il
secondo l'ha trovato il martello a 2 milioni di giri · **H4** chiusa · **H5** ✅ **chiusa** — il VID è un
link, verifica live fatta, e il buco che ha trovato (nove VID muti nel Registro) è chiuso anche quello ·
**H6** ✅ **chiusa** — il numero che sbordava dalla ciambella.
**Aperte: H1 e H3.**

### H6 ✅ CHIUSA — il totale sbordava dalla ciambella, e si vedeva solo su una pagina

Segnalata dal committente il 25 agosto sera su `/services/stats/division`.

Il buco della ciambella è largo **69 unità** del viewBox (r 42 meno mezza traccia da 15 per parte) e il corpo
del numero era **fisso a 19**: cinque cifre ci stanno («1234,5»), sei no — «12345,6» misura circa 80 unità e
finisce **sopra l’anello**. `StatsDonut.FontCentro` ora ricava il corpo dalla lunghezza (mai oltre 19, mai
sotto 11), coi casi limite fissati in un `[Theory]`.

⚠️ **La lezione non è «rimpicciolire il testo».** Il componente era stato provato — anche dalla verifica
live — solo con le ore di **una persona**, che sono sempre corte. Le ore di una **divisione** non lo sono, e
i due usi vivevano nello stesso file senza che niente lo dicesse. Un componente provato su un solo ordine di
grandezza non è provato.

Dettaglio in §13.8 della carta delle statistiche.

### H1 🟢 `.ed-layout` e le altre dieci `@media` degli editor

**Cos'è.** La stessa malattia curata sul viewer (A3 della carta): una `@media` misura la **finestra**, mentre
lo zoom di questa applicazione è `zoom` sull'`<html>` e la finestra non lo vede. Sul viewer pubblico il
danno era misurato e grave — il documento scendeva a **161px a zoom 1.8** fra due barre laterali a larghezza
fissa — ed è stato chiuso con una `@container`.

Restano **`.ed-layout`** più **dieci regole `.struct`**, tutte sulle pagine di editor e admin.

**Perché non è stata fatta insieme.** Le pagine admin hanno un perimetro d'uso **dichiarato** da 1024px in su
(`design/regole-ui-pagine-admin.md`): lì l'assetto attuale è quello voluto, non un incidente. E ogni regola va
decisa e **vista a schermo** una per una — non è un travaso meccanico come lo era per il viewer, dove il
layout è uno solo e ripetuto su quattro pagine.

**Come si riprende.** Gli attrezzi ci sono già, nello scratchpad della verifica live: `zoom2.js` interroga
`matchMedia` a cinque livelli di zoom e stampa le colonne effettive, `zoom3.js` fa la controprova a finestra
stretta e verifica che il contenimento non tocchi chi non deve.

> ⚠️ **La trappola da conoscere prima di misurare.** In **Edge 151** `documentElement.clientWidth` **non è
> più in unità di layout** sotto `zoom`: restituisce i px di finestra. Una misura dedotta da lì dice che non
> succede niente. Si chiede a `matchMedia`, che è ciò che decide davvero se la regola vale.

> ⚠️ **E la trappola del rimedio.** `container-type:inline-size` porta con sé `contain:layout`, che rende
> l'elemento contenitore anche per i discendenti `position:fixed`. Le pagine di editor hanno un
> `.editor-toast` fisso **dentro** il `.wrap`: mettere il contenimento sul `.wrap` glielo incolla dentro. Sul
> viewer è stato aggirato con `.wrap:has(> .doc-layout)`; per gli editor servirà una soluzione propria.

### H2 ✅ CHIUSA — il rosso di `Vipi.Application.Tests` era **due** difetti, non uno (25 agosto 2026, sera)

**Come stava scritto** (23 agosto): «in uno dei giri completi la suite ha segnato **1 fallimento su 625**, e
il nome non è stato catturato; in sei esecuzioni successive non si è più presentato». La voce diceva anche
come riprenderla — catturare il nome con `grep "\[FAIL\]"` invece di filtrare il riepilogo. È bastato farlo.

**Il nome.** `Vipi.Application.Tests.AorProiezioneProperties.Il_rapporto_fra_i_lati_e_quello_vero`, caduto
nella corsa completa in Release del 25 sera **su net10 e non su net8**. ⚠️ Il TFM non c'entra: è una
proprietà **CsCheck**, e i due TFM sorteggiano poligoni diversi.

**Il seme, che la rende riproducibile a comando:**

```
CsCheck_Seed=bxKC4K6PiVz6 dotnet test tests/Vipi.Application.Tests -c Release -f net10.0 --filter "FullyQualifiedName~Il_rapporto_fra_i_lati_e_quello_vero"
```

Fallisce sempre, in 21 ms: `Expected 374.00806170184399, Actual 371.7`.

**La causa, e non è nel proiettore.** La proprietà ricalcola per conto suo la proiezione per confrontarla
con quella vera, e per farlo prende `k = cos(latitudine media)` dai punti **generati**. Ma
`AorPolygonProjector` lavora sui punti **parsati**, e dal 25 agosto `PolygonGeometry.ParsePoints` passa per
`SenzaPuntiGemelli`, che **toglie i punti ripetuti di fila** (i lati a lunghezza zero: li ha il 29% dei
poligoni reali, ed erano il sospetto numero uno sulle facce degeneri dell'estrusione 3D).

Il campione che fallisce comincia con `(36, 13), (36, 13)` — due gemelli. Tolto il doppione, la media delle
latitudini sale da 40,3296 a 40,7626, `k` scende da 0,76245 a 0,75758, e la larghezza del viewBox scende di
2,3 unità. Rifatto il conto a mano fuori dal test:

| conto | larghezza |
|---|---|
| con i gemelli (quel che si aspetta la proprietà) | **374,00806170184** |
| senza i gemelli (quel che fa il proiettore) | **371,70117732413** → arrotondato `371.7` |

Sono esattamente i due numeri dell'asserzione, all'ultima cifra. Non è tolleranza in virgola mobile: lo
scarto è 2,3 su una soglia di 0,1.

⚠️ **Quindi il rosso è del ramo `statistiche-atc`**, non un fantasma del 23 agosto: `SenzaPuntiGemelli`
nasce lì, il 25. Se il rosso di due giorni prima fosse stato lo stesso test la causa era per forza un'altra
— la famiglia però è quella, «una proprietà che cade solo per certi sorteggi», ed è il motivo per cui in
sei giri non si era più vista.

**Il primo difetto, chiuso.** La proprietà ora modella il proiettore con **gli stessi punti che il
proiettore vede**: `PolygonGeometry.ParsePoints(json)` invece di `punti`. Col seme `bxKC4K6PiVz6` passa.

**Le altre cinque, guardate una per una** — perché «chi parte dai punti generati ha lo stesso difetto» era
un sospetto, non una misura. Esito: **nessun'altra rifà il conto**. Le altre cinque confrontano fra loro due
*uscite* del proiettore (o due proiezioni dello stesso ingresso), e per costruzione il parsing lo attraversano
tutt'e due allo stesso modo.

**Il secondo difetto, trovato col martello e chiuso anche quello.** Non bastava guardarle: sono state
rilanciate a **200 000** giri invece dei 100 di default, ed è caduta subito una proprietà diversa —
`Spostare_la_longitudine_non_cambia_il_disegno`, sui **punti del path**. Nulla a che vedere con i gemelli:
è il **mezzo decimale**. Il proiettore emette valori già arrotondati a un decimale (`R()`), e
`Assert.Equal(a, b, 0)` — «uguali arrotondati a **zero** decimali» — non è una tolleranza, è un **secondo**
arrotondamento con un **secondo** mezzo su cui cadere: 223,5 e 223,4 distano un passo di `R()` e diventano
224 e 223.

⚠️ Ed è un difetto che il file **aveva già curato una volta**, sul viewBox, dimenticando il path: il
commento a fianco cita il seme `bryagYjiWP_m` e mette tolleranza `0,11`. Le due righe del path erano rimaste
a `0`. ⚠️ **È il candidato più probabile per il rosso visto il 23 agosto**, che invece NON poteva essere il
conto della latitudine media: `SenzaPuntiGemelli` è nato due giorni dopo.

ℹ️ La correzione **stringe**, non allenta: `0,11` è una tolleranza assoluta, mentre «arrotondati a zero
decimali» tollerava fino a quasi un'unità intera nei casi non di confine.

**Il controesempio è congelato**, come prescrive il commento della classe:
`AorPolygonProjectorTests.Un_Punto_Ripetuto_Di_Fila_Non_Cambia_La_Proiezione` — un poligono con un gemello
consecutivo disegna **identico** a quello senza, viewBox compreso.

**Come è stata verificata.** `CsCheck_Iter=2000000` su **entrambi** i TFM, cioè ventimila volte la copertura
di un giro normale: verdi. Poi Release `--no-incremental` **0 avvisi** e suite completa **tutta verde** —
2243 net8 / 2005 net10.

⚠️ **La lezione, ed è generale.** «Rosso intermittente» era una diagnosi sbagliata due volte: una proprietà
CsCheck non è ballerina, cade **per certi sorteggi** — e il modo di trovarli non è rilanciare finché passa,
è **alzare le iterazioni**. Cento giri al giorno non sono una rete: sono un campione. Se un giorno una di
queste torna rossa, il primo gesto è `CsCheck_Iter=2000000`, non `dotnet test` un'altra volta.

### H4 ✅ CHIUSA il 23 agosto — l'intestazione della tabella ACC non si appiccicava affatto
Segnalata dal committente («l'header appare come una colonna normale») mentre preparava il deploy, e non era
un difetto di aspetto: **la `thead` non era sticky per niente**. Misurato: dopo 1200px di scorrimento
l'intestazione stava a `y = -812`, cioè fuori dallo schermo come una riga qualunque.

**La causa.** `.wrap *:has(> table):not(.st-scroll){overflow-x:auto}` (commit `a3b60d5`, la mattina del 23:
«le tabelle scorrono a qualunque zoom») dava `overflow-x:auto` a **ogni** contenitore diretto di una
tabella dentro `.wrap` — compreso il `<div class="block">` della pagina ACC. E un `overflow` diverso da
`visible` rende quel contenitore il **riferimento** dello `position:sticky` che sta dentro: l'intestazione
si appiccicava a un contenitore che non scorre, cioè a niente.

⚠️ **Non è aggirabile con `overflow-y:visible`**: per specifica, se un asse non è `visible` l'altro calcola
`auto`. Le due cose — «questo contenitore scorre in orizzontale» e «l'intestazione si appiccica alla
finestra» — **si escludono per costruzione**, e la scelta va fatta.

**Cos'ha deciso la misura.** Quelle tabelle non sfiorano il loro contenitore a nessuna larghezza:
1486 in 1534 (a 1600px), 1261 in 1309, 1179 in 1227, 933 in 981 (a 1024px). La barra orizzontale **non
sarebbe mai comparsa**, quindi la regola su quella pagina non comprava niente e costava il difetto.
Aggiunto `:not(:has(> table.sticky-head))`. Dopo: `y = 133` dopo 1200px di scorrimento — esattamente il
`top` calcolato — e nessun contenitore che scorra fra l'intestazione e la finestra.

**Raggio, misurato e non dedotto.** Delle sei pagine admin con tabelle, solo ACC era rotta: Aeroporti e
Registro tengono le loro in `.st-scroll`, che è il contenitore che scorre **apposta** e dove l'intestazione
sta a `top:0`; le altre tre non rendono tabelle appiccicate. Build Release 0 avvisi su due TFM, 3595 test
verdi.

ℹ️ **La regola diceva «del viewer» nel commento e `.wrap *` nel selettore.** È lì che è passata: il commento
descriveva l'intenzione, il selettore ha preso anche le pagine admin. Vale come promemoria — quando una
regola nasce per una famiglia di pagine, il perimetro va **nel selettore**, non nella prosa accanto.

### H3 🟢 `/services/vsop/admin/acc` sfora di 24px in orizzontale — ed è a OGNI larghezza, non solo a 1600

**Cos'è.** A 1600px di finestra la pagina ACC chiede 1624: la testata appiccicata
(`.doc-head.st-head.sticky`) misura **1648** dentro un contenuto da 1536.

⚠️ **23 agosto, sera — misurato più largo di com'era scritto**: non è un difetto dei 1600px, c'è a **ogni**
larghezza provata. Testata contro contenuto: **1648/1600** · **1407/1366** · **1318/1280** · **1055/1024**;
lo sforo di pagina vale 24 · 20 · 19 · 15 px. Cioè la testata è sistematicamente ~30÷48px più larga del
contenuto che la ospita, e la larghezza della finestra non c'entra. Trovato misurando la pagina per H4. Non c'entra la potatura del foglio
di stile del 23 agosto: ⚠️ **misurato con il foglio di PRIMA e con quello di DOPO, il numero è lo stesso**
(1624 in tutt'e due), quindi il difetto stava lì da prima e nessuno l'aveva visto.

**Perché non è stato chiuso subito.** Perché è un difetto a sé, e sistemarlo dentro un giro di pulizia
avrebbe mescolato due cose. Il colpevole è uno solo e si trova in una riga
(`node sfora.js http://localhost:5099/services/vsop/admin/acc` nella skill `verifica-live`).

### H5 ✅ CHIUSA — il VID è un link al profilo IVAO, e la verifica live ha trovato un buco vero

Chiesto dal committente e **fatto**: cliccando un VID, in qualsiasi pagina, si apre
`https://ivao.aero/Member.aspx?Id=<VID>`. Quindici punti in dieci file, un componente solo
(`Components/VidLink.razor`), sul ramo `statistiche-atc` (`03463bf`). Carta con tutto:
[`feature/2026-08-25-vid-porta-sul-profilo-ivao.md`](feature/2026-08-25-vid-porta-sul-profilo-ivao.md).

**La verifica live è stata fatta** (Edge + puppeteer-core su una copia del `vipi.db`, nove pagine guidate) e
ha detto due cose.

**Quel che funziona, misurato.** La **risalita del clic** in Permessi è ferma: il clic arriva all'ancora
(`clic: 1`) e la selezione non si muove — con la **controprova** che cliccando la riga lontano dal VID la
selezione cambia, altrimenti «non è successo niente» poteva voler dire «il clic non è arrivato». Le pagine
**SSR statiche** portano i link senza circuito (53 nella classifica). Nei **due temi** il colore del link è
identico a quello della cella, e col mouse sopra arriva il blu. In **stampa** la punteggiatura sparisce.

**Il buco, ed era vero.** Nel Registro **nove VID a schermo e zero link**: la colonna «cosa» porta le frasi
del narratore («Granted VID 704798 permission on LIRR»), e lì il VID non è un campo ma una **parola**.
⚠️ Nessuna prova sbagliava — nessuna guardava quella colonna, perché quella colonna non era stata toccata.
**Solo lo schermo poteva dirlo.**

**Chiuso con un secondo componente**, `VidText`: prende la frase già composta e la taglia sulla forma che
scriviamo noi (`Audit_VidN`, «VID 1234567»), emettendo i pezzi in mezzo come **testo** — niente
`MarkupString`, perché quelle frasi portano dentro titoli e note scritti da persone. Aggancia quattro punti:
Registro, la stessa frase nella riga di storia di Versioni, il «Deciso da …» di Sorgenti e l'«Assegnato da …»
di Incarichi — cioè **anche i due che questa voce dava per irrisolvibili**. Riverificato: 9 su 9 nel
Registro; le altre due frasi si sono viste **seminando** un incarico e una policy nella copia, perché su
questi dati non c'erano.

⚠️ **La forma tagliata dipende da una risorsa tradotta.** Se qualcuno ritraduce `Audit_VidN`, `VidText`
smette di trovare qualunque cosa **in silenzio**: per questo `VidTextTests` legge i due `.resx` dal disco e
fa fallire la suite invece.

**Cosa resta davvero, e sono due limiti del formato**: le **chip** di Incarichi (un `<a>` dentro un
`<button>` non è HTML valido) e le **tendine** di assegnazione (un `<option>` è solo testo). In tutt'e due
il VID compare solo come ripiego.

🟢 **E una voce aperta, piccola: la Guida non nomina il gesto.** `GuidaPage` parla di VID nella sezione
Permessi ma non dice che il numero si può premere. Da mettere insieme al capitolo sulle statistiche, che è
già una voce aperta di **B12** — così la Guida si tocca una volta sola.

ℹ️ **Due cose viste e non toccate**, con il perché nella carta (§8): «Carmine (704798)» nella colonna «chi»
non è un link perché quel numero sta dentro un **nome** (il `publicNickname` di IVAO), e «VID 0» non è un
link perché zero non è una persona — è la prima versione dei documenti generati dal profilo aeroporto, e la
sua esistenza nei dati veri conferma che quel ramo di `VidLink` serviva.

---

## I. Dopo la pulizia del database (26 agosto 2026)

Il committente ripulisce il database **un'ultima volta** prima di iniziare a popolare i dati veri. Queste
voci nascono dall'inventario dell'archivio fatto il 26 sera e **si guardano dopo**, non prima: sistemare
oggi un albero che sta per essere rifatto sarebbe lavoro buttato.

### I1 🔵 SOSPESA — le sette radici orfane di LIRR

`LIRR` ha **otto radici CTR** e una sola (`LIRR_EW_CTR`) porta il documento; `LIPP` ne ha due. Un albero così
scollegato è la ragione per cui un residuo si era formato: quando un import cambia il padre di un settore,
ciò che ci stava appeso si stacca — ed è esattamente com'è nata la «vIPI Roma» fantasma
([§17 della carta](feature/2026-08-26-eliminare-con-le-protezioni.md)).

Da rifare **dopo** la pulizia, e su dati veri: agganciare le radici superflue sotto quella buona dalla
pagina Struttura. ⚠️ Il riaggancio va scritto nel **catalogo** — è quello che la proiezione rilegge — e
adesso l'eliminazione lo fa già da sola (§14).

### I2 🔵 SOSPESA — lo stato dell'archivio, com'era il 26 agosto

La fotografia da cui ripartire per capire **cosa c'è davvero** (misurata sul `vipi.db` di sviluppo, prima
della pulizia):

| | |
|---|---|
| Documenti | 19, poi **18**: la «vIPI Roma» fantasma è stata eliminata |
| Pubblicati e visibili | **14** bersagli: 2 ACC (Brindisi 61 KB, Milano 5 KB), 3 APP, 5 aeroporti, 4 vLOA |
| Mai pubblicati | 4: **vIPI Roma** e **vIPI Padova** (scheletri nudi), Bologna Radar, vIPI LIBA |
| ACC italiane | Brindisi finita (21 sezioni, 9 blocchi pieni, 15 versioni) · Milano a metà · **Roma e Padova da scrivere** |
| Aeroporti | **6 documenti su 93**; 78 scali hanno settori e nessuna vIPI |
| Da ripubblicare | 4 bozze più avanti della copia pubblicata: Brindisi v15, Pescara v2, vLOA LGGG v2, vLOA LDZO v3 |

⚠️ Nell'archivio **due documenti diversi possono avere lo stesso titolo**: dove si elencano documenti, il
numero va accanto al nome quando il nome si ripete.

### I3 🟡 APERTA — gli orfani non sono tutti orfani, e ora si può sapere quali

Provando «chiedi alla sorgente adesso» ([carta](feature/2026-08-26-chiedere-alla-sorgente.md)) contro IVAO
vero, i **nove** orfani della Struttura si sono divisi così:

| | |
|---|---|
| la sorgente li **manda ancora** | LIBB_EU_CTR, LIRO_CRC_CTR, LIVK_CRC_CTR, LIVK_RCC_CTR, LIZZ_AAR_CTR, LIZZ_AEW_CTR, LIZZ_JTA_CTR, LIZZ_NVY_CTR — **otto** |
| **sparito davvero** | LIED_G_APP (Decimo Precision): `LIED` ne elenca 3 e questo non è fra loro |

⚠️ Otto su nove sono orfani perché qualcuno li ha **nascosti nel nostro catalogo**, non perché IVAO li abbia
tolti — e la sezione «Orfani» li mostra tutti uguali. Sono due situazioni diverse con due rimedi diversi:
uno si **rimostra**, l'altro si **elimina**. Da decidere dopo la pulizia, uno per uno; il tasto per
distinguerli adesso c'è.

### I4 🟡 APERTA — l'azione di gruppo sugli aeroporti non offre la domanda

`AeroportiPage.razor:619` elimina in blocco chiamando `EliminaAsync` senza verifica alla sorgente: chi la usa
passa dalla regola dei due giri come prima. È **voluto** — una raffica di verifiche puntuali su N scali è
esattamente ciò che la carta §3/P7 evita — ma il tasto singolo e quello di gruppo si comportano
diversamente sullo stesso oggetto, e va deciso se dirlo a schermo o dare al gruppo una verifica sola.

---

## J. Identità dei settori e shape — 26 agosto 2026, ramo `identita-settori`

Ramo aperto **da `statistiche-atc`** (non da `main`), **non fuso**, spinto su origin. Porta **dieci lavori
chiusi e nessuna voce aperta**. Carte:
[identità dei settori](feature/2026-08-26-identita-dei-settori.md),
[l'assenza non cancella](feature/2026-08-26-lassenza-non-cancella.md),
[le shape dal sectorfile](feature/2026-08-26-shape-dal-sectorfile.md),
[l'ordine delle sezioni](feature/2026-08-26-ordine-sezioni-personalizzato.md),
[il riordino trascinando](feature/2026-08-26-riordino-sezioni-trascinando.md).

⚠️ Il quarto lavoro (**J6**) non c'entra con i settori: è arrivato dal committente mentre il ramo era aperto,
e sta qui perché sta qui il ramo. **Non aggiunge migrazioni.**

⚠️ **Le migrazioni in coda passano da quattordici a DICIASSETTE**: `IdentitaDeiSettori`, `ShapeVuoteANull`,
`GateAiracShape`. Conta per §B12 e per il cutover MariaDB.

⚠️ **Questo ramo si somma a `statistiche-atc`, non lo sostituisce.** La decisione B12 (fondere) adesso
riguarda **due** rami in fila, e questo va fuso **dopo** quello.

### J1 ✅ FATTA il 26 agosto — l'avviso a chi pubblica una shape non ancora in vigore

Il gate AIRAC faceva già la cosa giusta da solo, ma **in silenzio**: chi pubblica vedeva a schermo il confine
nuovo e nel documento ne trovava un altro. Ora l'avviso sta **nel pannello release**, sopra i due tasti che
pubblicano, con l'interruttore accanto.

- **`ShapeGateNoticeService`** (`src/Vipi.Application/Content/ShapeGateNotice.cs`) — dice quali aree del
  perimetro resterebbero indietro, e le forza. ⚠️ **Nessuna regola nuova**: la domanda «è differita?» la fa
  `ShapeAiracGate.IsDeferredAt`, la stessa del congelamento. Se le due divergessero, l'avviso mentirebbe.
- **`EfShapeGateRepository`** (`src/Vipi.Infrastructure/Persistence/`) — il perimetro per bersaglio di
  release: `AccVipi`/`Vloa` → i settori della ACC (subcenter + posizioni d'aeroporto), `Airport`/`App` → le
  posizioni di quell'ICAO (la chiave dell'APP è un callsign: l'aeroporto sono le prime quattro lettere).
- **`ReleasePanel.razor`** — callout `warning` con callsign, nome e ciclo d'entrata, più il tasto
  «Pubblica comunque le aree nuove». Chiavi `Rel_Shape*` in italiano e inglese.

⚠️ **I cicli in gioco sono DUE**, perché i tasti sono due: «pubblica ora» usa il ciclo corrente, «pubblica al
ciclo» quello scelto nella tendina. Si avvisa per l'**unione**: sbagliare per eccesso costa una riga di
troppo, sbagliare per difetto costa un confine vecchio pubblicato senza che nessuno l'abbia saputo. L'avviso
si ricalcola anche quando si cambia il ciclo nella tendina (`@bind:after`).

⚠️ **Il perimetro è quello dell'ENTE, non l'elenco esatto delle configurazioni AoR.** Ricavare quello vorrebbe
dire rieseguire la derivazione del documento — cioè il congelamento — solo per decidere se mostrare un
avviso. L'imprecisione è dalla parte giusta: si può avvisare per un settore che quella mappa non disegna, mai
tacere per uno che disegna davvero.

⚠️ **Forzare non vuol dire «è in vigore»**: `ShapeAiracCycle` resta scritto. Quando il ciclo arriva davvero, la
promozione notturna chiude la pratica e **spegne la forzatura da sé**. Il permesso è quello del documento che
si sta pubblicando (ACC-scoped, o del documento per la vLOA): forzare è un atto editoriale.

Test: `ShapeGateNoticeTests` (8), `ShapeGateScopeTests` (6), più 4 in `ReleasePanelTests`.

### J2 ✅ DECISA il 26 agosto — i ripieghi valgono **solo per gli enti della divisione**

**Decisione del committente**: *le aree degli ATC esteri le dà IVAO, se ce le dà*. Un ente straniero senza
poligono resta senza poligono — né dal sectorfile, né da GitHub, né col cerchio sintetico. La ragione è che
quei confini non sono nostri: prenderli da una fonte che non è l'anagrafica del titolare vuol dire pubblicare
come vera un'area che nessuno di competente ha approvato.

La regola sta in **un posto solo** (`ShapeFallbackScope`, `src/Vipi.Application/Content/`), perché i ripieghi
sono tre e tre copie della stessa condizione sono tre racconti che prima o poi divergono. Riusa la stessa
domanda della gerarchia (`HierarchyRules.IsForeignCode` sui prefissi di `DivisionOptions`): «estero» ha una
definizione sola. Applicata a:

| Ripiego | File | Cosa cambia |
|---|---|---|
| Sectorfile (CTR/APP/MIL/FSS) | `SectorShapeFallbackService` | esteri fuori dai bersagli **e dal conteggio** |
| GitHub `twrs.tfl` (TWR) | `GithubTowerShapeService` | esteri fuori, anche col bottone manuale per ICAO |
| Cerchio 5 NM (TWR) | `TowerShapeFallbackService` | mai un cerchio finto su un campo estero |

⚠️ **Vale per i ripieghi, non per l'anagrafica**: le shape che IVAO manda si scrivono per tutti, esteri
compresi, esattamente come prima.

⚠️ Misurato in archivio: dei 118 settori esteri, **116 hanno già la shape da IVAO** (`ShapeSource = Source`) e
gli aeroporti in `AirportSectors` sono **tutti** italiani — la decisione oggi non toglie niente a nessuno, ma
chiude la porta prima che si apra.

⚠️ **Il caso che aveva aperto questa voce non era una decisione di divisione: era un difetto nostro.** I
punti `GODRA` e `GIGUS` **ci sono** — in `ESTERNI.fix`, un file che non leggevamo. Vedi **J8**.

### J3 ✅ DECISA il 26 agosto — quei settori **non devono avere** un'area

Contati sul `vipi.db` di lavoro il 26 agosto (non ricordati): **11 righe su 153** in `AccSectors`. In
`AirportSectors` le 51 senza poligono non contano — sono tutte ATIS/GND/DEL più una TWR (`LIED_TWR`):
posizioni che un'area non ce l'hanno per natura.

| Callsign | Nome | Che roba è |
|---|---|---|
| `LIPP_PLN_CTR` | Padova CE1 Planner | pianificazione |
| `LIRR_PLN_FSS` | Roma FSS Planner | pianificazione |
| `LIRO_CRC_CTR` | Barca Radar | militare |
| `LIVK_CRC_CTR` | Pioppo Radar | militare |
| `LIVK_RCC_CTR` | RCC | militare |
| `LIZZ_AAR_CTR` | Boom | militare (rifornimento in volo) |
| `LIZZ_AEW_CTR` | Legion | militare (AEW) |
| `LIZZ_JTA_CTR` | Gladiator | militare |
| `LIZZ_NVY_CTR` | Navy | militare |
| `DTTC_FMP_CTR` | Tunis ATFM | **estero** — per §J2 non lo tocchiamo più |
| `LOVV_EXA_CTR` | Vienna | **estero** — per §J2 non lo tocchiamo più |

Sette di questi sono **inattivi** in `Sectors` (`IsActive = 0`): i due `LIVK_*`, `LIRO_CRC_CTR` e i quattro
`LIZZ_*`. Gli altri quattro sono attivi.

**Decisione del committente**: vanno bene senza area, e non c'è niente da disegnare. Non sono settori con un
volume proprio: sono **postazioni operative in più** sullo stesso cielo di qualcun altro — guidacaccia,
planner, coordinamento — e un poligono per loro sarebbe una finzione. Non è un dato mancante: è un dato che
non esiste perché non ha senso.

Con §J2 i due esteri escono comunque dal conto per conto loro.

⚠️ L'elenco di questa voce nella stesura precedente era **sbagliato** (citava `LOVV_FSS`, `LSAS_EXA_FSS`,
`DTTC_FSS`, `LMMM_FSS`): quei quattro l'area ce l'hanno. Contato, non ricordato.

### J4 ✅ FATTO il 26 agosto — ripristino dei poligoni persi

`tools/ripristino-shape/ripristina-poligoni.sql` **eseguito** sul `vipi.db` di lavoro, con backup preso
prima (`src/Vipi.Host/vipi.db.bak-pre-ripristino-shape-20260826`) e host fermo.

```
AccSectors con poligono      5 → 142
AirportSectors con poligono 83 → 141   (58 APP)
righe                      153 → 153 · 192 → 192
TWR reali/sintetiche        66 / 17    (intatte)
```

Verifica: **283 poligoni in archivio, tutti e 283 si proiettano**; dei 211 settori che possono avere un'area
ne hanno **200**, contro i 5 di partenza.

⚠️ Lo script vale per **SQLite**. In produzione (MariaDB) il travaso è un'altra cosa: si esporta dal backup
e si applica per `UPDATE`.

### J5 ✅ CHIUSA — IVAO ha confermato che è un guasto loro

Il campo `regionMapPolygon` è vuoto su **tutta** l'API (misurato su 237 risorse, tre tipi, sei paesi, incluse
le forme `/all` e la chiamata pubblica esatta di webeye). **IVAO ha confermato il 26 agosto**, su richiesta
del committente, che è un **guasto loro** e che lo sistemeranno.

Quindi il ripiego dal sectorfile è una rete, non una sostituzione — e il rientro **è già provato**: quando
l'anagrafica torna a mandare una shape vera, riprende il comando per intero (provenienza a `Source`,
differimento chiuso). Vedi §2-ter della carta.

ⓘ **Una cosa da ricordare comunque**: il **tracker** (`/v2/tracker/now/atc/summary`, pubblico, senza token)
porta i poligoni **pieni** annidati in `subcenter`/`atcPosition`, ma solo per gli ATC connessi in quel
momento. Se il guasto dovesse durare, è la sorgente di riserva — e **non ha bisogno del gate AIRAC**, perché
è quel che IVAO serve ai controllori adesso.

### J6 ✅ CHIUSA — l'ordine delle sezioni è una scelta editoriale

Richiesta del committente, 26 agosto sera: «le sezioni devono poter essere spostate sopra o sotto all'interno
dello stesso gruppo, e ognuna deve riportare quante posizioni è sopra o sotto quella standard».

**Il motore c'era già.** `MoveSectionAsync` scambia `DocumentSection.Order` fra **fratelli** — che è già «lo
stesso gruppo»: il blocco per la vIPI ACC, la radice per APP e vLOA — e l'`Order` è versionato, copiato in
bozza e catturato nello snapshot di release. Mancava **il tasto**: `DocumentSectionsEditor` legava
`IsMandatory` a tre divieti insieme (rinomina + elimina + **sposta**). Le prime due restano: **è l'ordine a
non essere del catalogo, non l'identità della sezione**.

Lo scostamento dall'ordine standard lo calcola `SectionOrdering.OffsetsFromStandard` (funzione pura), e
l'editor lo scrive come pill accanto al titolo: `↑2`, `↓1`. Si legge **sempre**, non solo in modifica.

⚠️ **Si contano solo le sezioni FISSE, e solo quelle PRESENTI.** Una sezione libera non ha una posizione
standard: contarla farebbe apparire `↓1` su tutte le fisse che la seguono appena qualcuno ne infila una in
testa — scostamenti che nessuno ha prodotto. E una sezione di catalogo assente (il VFR su un blocco Aerovia)
non lascia un buco.

⚠️ **Difetto trovato strada facendo, ed è la trappola del doc 11 §8**: il viewer della vLOA rendeva le due
**direzioni** dei coordinamenti in una sequenza scritta nel codice (uscente, poi entrante), pur
riconoscendole per chiave. Spostarle nell'editor avrebbe cambiato l'editor e **non** il documento pubblicato.
Ora segue l'ordine delle sotto-sezioni, con l'ordine canonico come ripiego per gli **snapshot storici**, dove
entrambe portano ancora la chiave del padre e si distinguono solo per posizione.

Nove test nuovi, suite intera verde, Release **0 avvisi** sui due TFM. Verifica live sulle **tre famiglie**
(§7 della carta): vIPI ACC, vIPI APP e vLOA — i due documenti che il database di sviluppo non aveva sono
stati creati al volo sulla copia. La prova che vale di più è la vLOA: spostata una delle due **direzioni**
dei coordinamenti, **l'anteprima bozza del documento la rende nell'ordine nuovo**. Sull'editor ACC
(`/services/vsop/libb/editor`) le sezioni «obbligatoria» mostrano `↑ ↓`, *Frequenze* portata sopra *AOR*
persiste al ricarico e le due sezioni portano `↑1` e `↓1` anche fuori dalla modifica. Commit `30dad4e`.

### J7 ✅ CHIUSA — anche i blocchi della vIPI ACC si riordinano, ma l'Aerovia resta in testa

Seguito naturale di J6, chiuso il 26 agosto sera tardi. Due decisioni del committente: le frecce stanno
**nell'intestazione del blocco** (dentro il `<summary>`, solo in modifica, accanto al campo del titolo e a
«✕ Gruppo»), e **i settori di aerovia restano primi** — i gruppi APP si riordinano fra loro, nessuno passa
sopra l'Aerovia.

Il motore è sempre lo stesso: i blocchi sono le **sezioni radice** del documento, quindi `MoveSectionAsync`
va bene com'è. Quel che si aggiunge è la **regola**, e sta in `AccDocumentService.MoveGroupAsync`: legge i
blocchi nell'ordine del documento con `AccDocumentAssembler` — così l'Aerovia si riconosce dal **blockmeta**
e non dal posto che occupa — e rifiuta in silenzio la mossa che uscirebbe dall'elenco, che partirebbe
dall'Aerovia o che le passerebbe sopra.

⚠️ **La regola sta in due posti apposta, e non è una duplicazione da togliere**: l'editor **spegne** il tasto
(`CanMoveGroup`), il servizio **rifiuta** la mossa. Il primo è quello che si vede, il secondo è quello che
tiene se qualcuno arriva per un'altra strada — o se l'elenco è cambiato sotto mentre la pagina era ferma.

Due test nuovi in `AccDocumentServiceTests`. Verifica live su `/services/vsop/libb/editor`: l'Aerovia non ha
frecce, un gruppo solo le ha tutt'e due spente, con due gruppi il primo ha `↑` spenta e il secondo sale
davvero — e il passo successivo in su non c'è.

### J8 ✅ FATTA il 26 agosto — il catalogo punti leggeva **tre file su otto**

`GODRA` e `GIGUS` non mancavano: stavano in `NAVAIDS/ESTERNI.fix`, che non scaricavamo. La configurazione
elencava **tre** file a mano (`itfix.fix`, `itvor.vor`, `itndb.ndb`) mentre `ITALY.isc` ne cita **otto** —
`ESTERNI.fix`, `MIL.fix`, `APT.fix`, `VFR_NASCOSTI.fix`, `secsi.fix` oltre ai tre.

Ora `AuroraNavaidSource` legge l'elenco dall'indice, **stessa regola già usata per i file di settore**
(`AuroraSectorShapeProvider`): quali file leggere lo dice Aurora, non una lista scritta da noi.

Misurato sui file veri del repo sectorfile:

```
nomi in catalogo   1385 → 3732   (+2347)
GODRA / GIGUS      assenti → presenti, con le coordinate
LIMM_WS2/WS5/ES2   3 punti irrisolti a testa → ZERO
```

⚠️ Gli irrisolti erano **tre**, non due: c'era anche `GEMLA`. Cercarli a uno a uno avrebbe chiuso metà del
buco — la lista scritta a mano era il difetto, non i due nomi.

⚠️ **L'ordine di lettura non è estetico**: a parità di nome il catalogo tiene la **prima** occorrenza e con
essa la natura del punto, quindi VOR e NDB si leggono **prima** dei fix. Seguendo l'ordine dell'indice, un
omonimo cambierebbe natura ogni volta che qualcuno riordina `ITALY.isc`.

⚠️ I tre percorsi in configurazione restano come **ripiego**, per quando l'indice non risponde: un catalogo
ridotto è meglio di nessun catalogo (e nessun catalogo vuol dire **nessun poligono di settore**, perché i
vertici per nome non si risolvono più).

⚠️ Il catalogo dei punti serve anche ai **suggerimenti dei CoP** e alla completion delle SID: da qui in avanti
quei campi non segnano più come inesistente un punto d'oltreconfine scritto giusto.

Test: `NavaidIndiceTests` (7).

### J9 ✅ CHIUSA — le sezioni si riordinano anche **trascinandole** nel menu Navigazione

Richiesta del committente, 26 agosto: «se nell'editor le sezioni si potessero spostare anche trascinando nel
pannello Navigazione… per ora fallo per la vIPI di ACC, di avvicinamento e la vLOA».
[Carta](feature/2026-08-26-riordino-sezioni-trascinando.md).

**Perché lì.** Le frecce di J6 stanno sulla card della sezione, in una pagina alta migliaia di pixel: portare
*Validità e revisione* in cima sono otto pressioni, e a ogni pressione la sezione esce dallo schermo. Il
menu-sezioni è l'unico posto dove l'ordine si vede **tutto insieme**. Le frecce restano — sono la strada da
tastiera, e il trascinamento HTML5 non esiste sul tocco.

**La regola è una sola**: *la sezione lasciata prende il posto di quella su cui la si lascia*. Dalla stessa
frase escono due riferimenti diversi secondo il verso, e il conto lo fa una funzione pura
(`SectionOrdering.TryDropOnto`); su fratelli **adiacenti** dà esattamente l'esito della freccia.

Il motore serviva nuovo, perché lo scambio `±1` non salta N posti:
`MoveSectionBeforeAsync(sezione, prima-di?)` reinserisce e **rinumera il gruppo**.

⚠️ **Il vincolo «solo dentro il suo gruppo» sta nel MOTORE, non nella UI**: il riferimento dev'essere un
**fratello**, altrimenti la mossa non avviene. Non è ridondanza — è ciò che rende impossibile trasformare un
riordino in una **riparentazione silenziosa**, che cambierebbe il significato di una sezione e non la sua
posizione.

⚠️ **`draggable="false"` va scritto esplicitamente**: un `<a href>` nasce trascinabile per conto suo, e senza
quell'attributo la voce del pannello Release si lascia prendere per poi non andare da nessuna parte.

⚠️ **Il trascinamento è opt-in dell'host** (`EditorToc.OnReorder`): non passarlo lascia il pannello identico a
prima, **senza gestori registrati sul circuito**. È così che l'editor aeroporto — che di sezioni-documento
non ne ha — resta fuori senza una condizione dedicata, e fuori dalla modifica l'indice resta un indice.

15 test nuovi (5 puri, 2 sul repository, 8 bUnit sul pannello), suite intera verde, Release **0 avvisi** sui
due TFM. Verifica live sulle **tre famiglie** con browser vero: ACC `/services/vsop/libb/editor`, APP
`?app=LIBG_APP`, vLOA `?acc=LDZO` — lo spostamento persiste al ricarico e le pill `↑2 ↓1` si aggiornano.
Le due prove che i test non danno: trascinata una sezione **fra due blocchi** della vIPI ACC il menu è
identico prima e dopo, e **l'anteprima bozza** della vLOA rende l'ordine nuovo.

**Non fa** (deciso, non dimenticato): i **blocchi** ACC non si trascinano — nel menu sono intestazioni, e il
loro riordino ha la regola propria dell'Aerovia in testa (J7); le **sotto-sezioni** nemmeno, il menu mostra
solo il primo livello.

## K. La vIPI d'aeroporto entra nel catalogo — 26 agosto 2026, ramo `aeroporto-a-sezioni`

Ramo aperto **da `identita-settori`** (non da `main`), **non fuso**. Una voce sola, chiusa. Carta:
[l'aeroporto entra nel catalogo](feature/2026-08-26-aeroporto-a-sezioni.md).

Richiesta del committente: «rendere la struttura del documento d'aeroporto uguale a quella degli altri,
compreso il meccanismo di riorganizzazione».

**Non mancava un tasto: mancava il documento.** Il documento d'aeroporto era una **proiezione cotta** —
`RebuildDocumentAsync` riconosceva le sezioni **per titolo**, le cancellava e le riscriveva a ogni
rigenerazione, con chiavi **casuali** (`BlockSection.Airport` non ha una chiave di catalogo, e il builder
ricadeva su `SectionKeys.NewCustom()`). Ordine, «nascondi», sotto-sezioni e Live/Frozen stanno **sulla
sezione**, e la sezione veniva distrutta: per questo l'aeroporto era l'unica famiglia che non li aveva.

Ora ha un profilo suo nel catalogo (otto chiavi), l'editor monta `DocumentSectionsEditor` e il viewer itera
`_view.Sections`. Le sezioni fisse sono **ancore senza corpo**: il contenuto si deriva a view-time dalle
tabelle del profilo e si **congela** alla release, come per l'APP.

⚠️ **Nessuna migrazione**: non cambia lo schema, cambia chi scrive. Le diciassette in coda restano diciassette.

⚠️ **Un passo d'avvio nuovo**: `ReconcileAirportSectionKeysAsync`, che porta i documenti già scritti sulle
chiavi del catalogo e **trasloca** le sezioni libere dalla tabella `AirportExtraSection` dentro il documento.
Idempotente, lo scrive nei log («Riconciliate N sezioni d'aeroporto sulle chiavi del catalogo»). Gira **prima**
di `AddMissingCatalogSectionsAsync`, che ora copre anche gli aeroporti.

⚠️ **`AirportExtraSection` non si droppa in questo giro.** Le migrazioni girano all'avvio **prima** delle
riconciliazioni: una migrazione che la cancellasse porterebbe via il contenuto un istante prima che il
trasloco lo sposti. Nessuno ci scrive più; si toglie un rilascio dopo. **È l'unica voce che questa sezione
lascia in eredità.**

⚠️ **Due conseguenze volute, da dire al committente:**
1. L'editor aeroporto adotta **bozza + lock** (✎Modifica): obbligato, perché ogni mutazione del motore
   condiviso passa da `IEditingService`, che pretende il lock. Cade la scelta di luglio.
2. La pagina pubblica **smette** di mostrare piste, frequenze e sezioni libere prese dal profilo **live**:
   d'ora in poi vede lo stato **pubblicato**. Il passaggio è morbido — chi ha già una release non ha un
   payload congelato per le chiavi nuove e continua a leggersi live finché non ripubblica.

Verifica live su LIBD guidando Edge: lock e bozza v2, riordino con le pill `↑1`/`↓1`, «nascondi», sezione
libera **in mezzo** alle fisse, ordine che tiene al ricarico, anteprima bozza coerente. **La prova che
conta**: pubblicato e poi cambiato il TORA di una pista, la pagina pubblica resta a 3000 e la bozza dice il
valore nuovo — la release congela davvero ciò che prima non era congelabile perché era cotto.

⚠️ **Una correzione, subito dopo** (carta §8-§9): la sezione METAR/TAF c'era nell'editor e **non** nella pagina
pubblica, perché il pubblico legge lo **snapshot di release** e quello — per ogni scalo non ancora ripubblicato
— è anteriore alla carta e non la conosce. Con lo stesso difetto, `transition` e `runways` uscivano come
tabelle generiche. Chiuso con `AirportLegacySections`, **una** mappa titolo→chiave con **due** lettori (la
riconciliazione d'avvio e il viewer), e con la regola generale: *una sezione **sempre live** non è mai parte
della verità di uno snapshot*. Verificato mettendo in piedi il codice **pre-carta** in un worktree su una copia
del DB **pre-migrazione**, accanto a quello nuovo: le due pagine coincidono. Restano tre differenze volute —
niente più due colonne affiancate (una griglia non si riordina), titoli in italiano, «Nota» sui callout.
