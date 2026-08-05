# ADR-0007 — Produzione: persistenza, concorrenza e scala

**Stato:** Accettato (piano) — attuazione parziale
**Data:** 22 luglio 2026
**Decisori:** Carmine + assistente
**Riferimenti:** `adr-0001-scelte-architetturali-fondanti.md` (SQLite di partenza), `adr-0002-integrazione-e-autenticazione-portabile.md` (identità host), `adr-0003-trasporto-live-sse.md` (SSE), `../history/audit-2026-07-22-criticita-full-stack.md` (A1/A2/D1), memoria `audit-2026-07-22-criticita`.

---

## Contesto

L'audit full-stack del 22 lug ha fissato il **target di prodotto: pubblico di divisione (scala)**. Tre criticità ALTE riguardano la messa in produzione:

- **A1 — SQLite multi-utente.** `vipi.db` è un file singolo: SQLite serializza le scritture (lock a livello file). Con editing staff concorrente + job di import periodici + polling live si rischia contesa sul write-lock (`database is locked`).
- **A2 — Blazor Server e scala.** Ogni utente = circuito SignalR stateful con affinità di server; la rotta SSE (`/vsop/live/atc`) è una connessione persistente per utente. Senza backplane lo scale-out orizzontale non è banale e il drop di rete perde lo stato dell'editor.
- **D1 — Identità di produzione.** In sviluppo l'identità è `DevCurrentUserProvider` (admin onnipotente, fallback statico). Il path di produzione (`HostIdentityCurrentUserProvider`) esiste ed è config-driven, ma va **montato e configurato** nel sito host reale.

---

## Decisione

### D1 — Concorrenza SQLite: mitigazione ora, Postgres come cutover pianificato

**Ora (attuato):** `SqliteTuningInterceptor` abilita **WAL** (i lettori non bloccano lo scrittore) + **`busy_timeout=5000ms`** (lo scrittore attende il lock invece di fallire subito) a ogni apertura di connessione. Registrato nel path `UseSqlite`. È una **mitigazione**, non la soluzione: alza la soglia di contesa ma resta un solo scrittore.

**Postgres — due livelli distinti:**

**(a) Deploy preview/test collaboratori (attuato):** per far provare la vIPI ai collaboratori su host free senza disco persistente (Render + Neon), selezionare `Persistence:Provider=Postgres` ora registra `UseNpgsql` e crea lo schema **da modello via `EnsureCreated`** in `MigrateVipiDatabase` (nessuna cronologia migrazioni). Adeguato a un DB fresco/di prova; `RowVersion` usa `.IsConcurrencyToken()` con assegnazione manuale → mappa `bytea`, nessun conflitto `xmin`. Pacchetto `Npgsql.EntityFrameworkCore.PostgreSQL` aggiunto. **Default resta `Sqlite`** (dev locale + migrazioni versionate intatti). Vedi `deploy/render/README.md`. Limite: **niente migrazioni incrementali** su Postgres in questo path — un cambio di modello richiede drop schema + riavvio.

**(b) Cutover Postgres di produzione (pianificato, NON ancora attuato):** per il carico pubblico servono migrazioni versionate anche su Postgres. Passi residui rispetto ad (a):
1. ✅ **Fatto:** selezione provider via config (`Persistence:Provider` = `Sqlite` | `Postgres`), branch in `AddVipiInfrastructure` — stesso pattern di `DataSource:Provider` (ADR-0006 D2). `PersistenceProviderResolver` puro e testato; **default `Sqlite`**. Ora entrambi i provider registrano il DbContext (Postgres via `EnsureCreated`, punto (a)).
2. **Migrations per-provider**: le 60 migrazioni attuali sono SQLite-flavored. La produzione Postgres richiede un **assembly di migrazioni dedicato** (o cartelle separate con `MigrationsAssembly`) al posto di `EnsureCreated` — non si riusano le stesse, non convertibili in automatico.
3. Rivedere i punti provider-specifici: `RowVersion` (SQLite = BLOB manuale; Postgres = `bytea`, già ok con `IsConcurrencyToken`), tipi `TEXT`, default enum→stringa (già portabile).
4. Validare su un'istanza Postgres reale prima del cutover di produzione (il path (a) su Neon fornisce già una prima validazione runtime dello schema).

> ⚠️ Il path (a) `EnsureCreated` è per anteprima/test, non per produzione: senza cronologia migrazioni non evolve lo schema in modo incrementale. La produzione resta su SQLite+WAL finché il cutover (b) con migrazioni dedicate non è eseguito e validato.

### D2 — Scala Blazor Server: backplane + separazione viewer/editor

**Direzione (pianificata):**
- **Viewer pubblici read-only** (consultazione vIPI/vLOA/aeroporti): candidati a render mode statico/WASM per non tenere un circuito per lettore. L'editing e il live restano `InteractiveServer`.
- **Backplane** (es. Azure SignalR Service) per lo scale-out orizzontale del circuito, con sticky-session dove serve.
- La rotta SSE resta com'è (ADR-0003); dimensionare le connessioni persistenti attese.

**Ora:** nessun cambio di render mode; decisione registrata. Il dimensionamento reale dipende dal numero di utenti concorrenti attesi (da stimare col gestore).

### D3 — Guardia identità di produzione (attuato)

`ProductionIdentityGuard.EnsureSafe` fa **fallire l'avvio** se l'identità dev fittizia è attiva fuori da Development (`useDevIdentity && !isDevelopment`) — impedisce il bypass totale dell'autorizzazione (admin onnipotente) in un deploy pubblico. Il montaggio reale della RCL nel sito host e la conferma di claim/staff-code IVAO restano **attività esterne** (vedi Aperti).

---

## Conseguenze

**Positive:** contesa SQLite mitigata subito e verificabile; deploy pubblico non può partire con identità dev (fail-fast); il percorso Postgres/scala è tracciato e non improvvisato.

**Negative / costi:** WAL non elimina il collo di scrittura (resta un solo scrittore) — è un tampone a tempo; il cutover Postgres è lavoro non banale (migrations dedicate + validazione su istanza reale); la separazione viewer/editor richiederà rifattorizzazione dei render mode.

**Aperti (esterni, non code-verificabili qui):**
- Montaggio della RCL vIPI nel sito host + configurazione `HostIdentity` coi claim reali IVAO e `Auth:AdminStaffCodes` / ruoli divisione confermati.
- Esecuzione e validazione del **cutover Postgres di produzione** (migrazioni dedicate) su istanza reale — il path preview `EnsureCreated` (Render+Neon) è attuato ma non copre l'evoluzione incrementale dello schema.
- Provisioning backplane + topologia di deploy; stima utenti concorrenti.

---

## Aggiornamento (30 luglio 2026)

Tre cose che l'ADR non poteva sapere a luglio e che cambiano la lettura di **D1**, non la decisione.

- **Il limite del path (a) è più stretto di come è scritto.** L'ADR dice che con `EnsureCreated` «un cambio di
  modello richiede drop schema + riavvio». Dal 29 luglio non è più così: `PostgresSchemaReconciler`
  (`Vipi.Infrastructure.Persistence`, chiamato da `MigrateVipiDatabase` dopo `EnsureCreated` e dal tool
  `Vipi.DbSeed` prima del TRUNCATE) confronta il modello relazionale EF con `information_schema.columns` e fa
  `ADD COLUMN IF NOT EXISTS`. Idempotente, no-op fuori da Npgsql.
  **Copre solo le aggiunte**: rename, drop e cambi di tipo restano scoperti e richiedono ancora un intervento
  manuale sullo schema. È la ragione per cui (b) resta aperto — ma la soglia di dolore si è spostata: finora le
  modifiche sono state additive e il reconciler ha retto.
- **«Anteprima» e «produzione» sono la stessa istanza.** L'ADR conclude che «la produzione resta su SQLite+WAL
  finché il cutover (b) non è eseguito». Descrive una produzione che non è mai esistita: non c'è nessun deploy
  SQLite pubblico, e l'istanza Render+Neon del path (a) è ciò che i controllori usano davvero. Dal 30 luglio
  segue `main` (vedi `deploy/render/README.md`). La distinzione utile oggi non è «test vs produzione» ma
  **«schema additivo, coperto dal reconciler» vs «schema che evolve, che richiede (b)»**.
- **Piattaforma: .NET 10.** Solution e deploy migrati da `net8.0` a `net10.0` (EF Core 10, Npgsql 10). Non
  cambia nulla di questo ADR — le migrazioni restano SQLite-flavored e su Postgres continua a girare
  `EnsureCreated` — ma il contesto tecnico citato nel testo è quello di .NET 8. Vedi `docs/history/rounds.md`,
  sessioni del 30 luglio.

**Cosa resta vero senza modifiche:** D2 (scala Blazor Server, backplane, separazione viewer/editor) e D3
(guardia identità). Il punto aperto (b) resta aperto, con la precisazione sopra su quando diventerà bloccante.

### D1-bis — Il drift non correggibile va almeno *visto* (attuato)

Riformulando il rischio residuo di (a): il pericolo non è che l'app si rompa, è che **taccia**. Rinominando una
colonna nel modello, il reconciler crea la nuova (vuota) e lascia la vecchia coi dati dentro; l'app non lancia
nulla e mostra un campo vuoto. Un `42703` in faccia sarebbe stato meglio. Stesso schema per un cambio di tipo:
`ADD COLUMN IF NOT EXISTS` non scatta, la colonna resta com'era, il conto arriva dopo e altrove.

**Attuato:** `ISchemaDriftProbe` (porta in Application, implementata da `PostgresSchemaDriftProbe`) confronta il
modello EF con `information_schema` **nel verso opposto** al reconciler e produce finding:

| Rilevato | Severità | Significato |
|---|---|---|
| Colonna orfana nello schema | Warning | Nel DB ma non nel modello → rinomina/rimozione mai applicata. **I dati sono lì.** |
| Tipo colonna divergente | Warning | Il reconciler non cambia i tipi: serve un `ALTER` a mano |
| Colonna mancante nello schema | Error | Attesa dal modello e assente: il reconcile è best-effort, può aver fallito |

I finding confluiscono nel report di consistenza esistente, quindi si vedono in `/vsop/admin/diagnostica` e
mandano `/vsop/health` a **Degraded** senza modifiche a valle. Fuori da Npgsql è un no-op: dove le migrazioni EF
girano davvero il drift non si accumula. Non sta in `/vsop/health/ready`, che l'orchestratore ripete di continuo.

**Cosa deliberatamente NON fa: correggere.** Guardando solo modello e schema, una rinomina è indistinguibile da
«togli la vecchia, aggiungi la nuova»: automatizzarla significherebbe autorizzare un `DROP COLUMN` deciso da
un'euristica sul database di produzione. Il probe segnala, la correzione la applica una persona.

**Come si applica la correzione, quando il probe segnala (D1-bis).** Non c'è ancora un runner di script versionati: la
DDL si esegue a mano su Neon (`ALTER TABLE ... RENAME COLUMN`, o `ADD` + `UPDATE` di travaso + `DROP`). Va bene
finché i casi sono rari — finora **zero**, tutte le modifiche sono state additive. Se dovessero diventare
ricorrenti, il passo successivo è una cartella di `.sql` ordinati con una tabella che traccia quelli applicati,
eseguiti all'avvio dopo il reconciler (l'advisory lock c'è già): copre rinomine e cambi di tipo a una frazione
del costo di (b). Le migrazioni EF per-provider di (b) restano la risposta completa, ma il loro punto duro non è
il lavoro corrente — è il **baseline**: lo schema su Neon oggi è il prodotto di `EnsureCreated` più le toppe del
reconciler, e va riprodotto esattamente in uno snapshot iniziale da timbrare come applicato, altrimenti la prima
migrazione vera fallisce in produzione.

---

## Aggiornamento (1 agosto 2026) — D4: MySQL entra, ma solo sul TFM net8 — ⚠️ **SUPERATO da D4-bis**

> La conclusione di questa sezione («MySQL solo su net8», provider Pomelo) è stata **ribaltata il 5 agosto
> 2026** da D4-bis, qui sotto: il sito definitivo è il nostro host standalone net10. Il testo resta perché
> l'analisi dei provider è ancora la prova del perché non si aspetta Pomelo — ma non prenderne la
> decisione finale.

**Contesto nuovo.** L'embedding nel sito `Ivao.It.Website` (net8, Blazor Server) ha chiuso la domanda sul
database: la divisione può offrire **solo MySQL**. Cadono entrambe le opzioni previste per l'host
(PostgreSQL affiancato, o SQLite su disco persistente), quindi il supporto MySQL passa da *opzionale* a
*strada obbligata* per l'integrazione — restando **irrilevante per il deploy autonomo**, che continua su
Render+Neon.

### Decisione

**MySQL è supportato solo sul target `net8.0`.** Il ramo `net10.0` resta SQLite + PostgreSQL.

Non è una scelta di gusto, è una constatazione verificata l'1 agosto 2026:
`Pomelo.EntityFrameworkCore.MySql` è fermo alla **9.0.0** (EF Core 9, pubblicata 17-ago-2025); il repo non
ha commit su `main` da quella data; l'issue «EF Core 10 support» (#2007) e la PR #2019 sono aperte da mesi,
mentre le PR #2031, #2032 e #2042 — tutte tentativi di porting a EF Core 10 — sono state **chiuse senza
merge**. Non esiste alcun pacchetto EF Core 10, nemmeno in preview. Per net8 serve la **8.0.3**, che è
stabile ed è il connector che il sito già usa.

L'alternativa `MySql.EntityFrameworkCore` di Oracle (10.0.9, copre entrambi i TFM) resta la scelta di
ripiego, non la prima: introdurrebbe un secondo connector ADO (`MySql.Data`) nello stesso processo del
sito, che usa `MySqlConnector`.

**Questo è un limite noto e duraturo, non temporaneo.** Se e quando porteremo l'embedding a EF Core 10, il
ramo MySQL non seguirà finché Pomelo non riprende. Va riletto qui prima di pianificare quel salto.

### Conseguenza su D1

D1 diceva «SQLite ora, Postgres come cutover pianificato», con i due path (a) `EnsureCreated` + reconciler
e (b) migrazioni versionate. MySQL **non riusa (a)**: per il database di produzione di un partner scegliamo
migrazioni dedicate, cioè la forma (b), che il piano dettaglia. Ragione: il reconciler copre **solo le
aggiunte di colonna**, e la DDL di MySQL **non è transazionale** — un reconcile interrotto lascerebbe uno
schema parziale senza rollback. Il compromesso che accettiamo su Neon, che è casa nostra, non lo esportiamo
da loro.

### Rimane aperto (e ora ha un costo misurabile)

Il modello ha `HasMaxLength` su **6 sole** colonne. Su SQLite e Postgres le stringhe diventano `text`,
indicizzabile senza limiti; su MySQL diventano `longtext`, che InnoDB **non indicizza** senza prefix
length. Circa venti colonne indicizzate — inclusi gli **enum salvati come stringa** — vanno dimensionate.
È lavoro dovuto a MySQL ma **valido su tutti i provider**, e va fatto attenzione a un dettaglio che ricade
proprio su D1-bis: applicare le lunghezze anche a Postgres sarebbe un **cambio di tipo colonna**, che il
reconciler non sa fare e che il drift probe segnalerebbe. Per questo il piano mappa le lunghezze **solo
quando il provider è MySQL**, lasciando Neon intatto.

**Piano operativo completo, slice e stime:** [`../design/piano-supporto-mysql.md`](../design/piano-supporto-mysql.md).

---

## Aggiornamento (5 agosto 2026) — D4-bis: MySQL è il provider di **produzione**, su net10

**Questa sezione ribalta la decisione di D4 sopra.** Il testo di D4 resta perché la sua analisi del
provider è ancora la prova del perché non si aspetta Pomelo — ma la sua conclusione («MySQL solo su
net8») **non vale più**.

**Cosa è cambiato.** Il sito definitivo è `atc.it.ivao.aero` e a servirlo è **`Vipi.Host`, il nostro host
standalone (net10)**, non la RCL montata dentro `Ivao.It.Website`. La premessa di D4 — «MySQL serve solo
all'embedding, che è net8» — è caduta. L'embedding non è cancellato, è rimandato: il multi-target
`net8.0;net10.0` delle cinque librerie resta in piedi per quando si farà.

### Decisione

1. **MySQL è il provider di produzione e deve funzionare su `net10.0`.** SQLite resta il default di
   sviluppo; PostgreSQL resta il deploy Render+Neon, che **non si spegne**: diventa ambiente di prova e
   sorgente del travaso dati.
2. **Il provider è `MySql.EntityFrameworkCore` di Oracle, 10.0.9** — non Pomelo. Su net10 Pomelo non
   esiste, quindi non è una preferenza fra due opzioni: è l'unica che produce un ramo eseguibile.
   Ri-verificato il 5 agosto 2026 **nel nuspec** del pacchetto, non solo sull'elenco versioni: gruppi di
   dipendenze per `net8.0` (EF 8.0.28), `net9.0` (EF 9.0.17) e `net10.0` (**EF 10.0.9**), tutti su
   `MySql.Data` 26.7.0.
3. **Collation `utf8mb4_0900_as_cs`**, applicata solo quando il provider è MySQL. Il server è MySQL 8.0+
   (confermato da Ivao.It), quindi si prende la strada pulita: una riga in `OnModelCreating`, ereditata da
   tutte le colonne, nessun audit colonna per colonna.

### Cosa migliora e cosa peggiora rispetto a D4

**Migliora la copertura.** D4 accettava un ramo di produzione **mai eseguito da nessun test**, perché la
suite gira su net10 e Pomelo vive su net8: era il difetto più grave di quella decisione, e ricalcava la
condizione in cui si trova ancora oggi il percorso Npgsql di `ISchemaDriftProbe`. Con Oracle il ramo MySQL
entra nella suite esistente **senza multi-target dei progetti di test e senza `#if NET8_0`**. Cade anche
un problema pratico che D4 non aveva visto: `tools/Vipi.DbSeed` è net10, e con Pomelo non avrebbe potuto
scrivere su MySQL — cioè il travaso dei dati sarebbe stato impossibile con il tool che già esiste.

**Peggiora la maturità del provider.** Oracle è storicamente più debole di Pomelo nella *query
translation*, che è esattamente la superficie dove il piano si aspetta i bug. È un costo accettato
consapevolmente, e ha una conseguenza operativa: la **verifica live su MySQL reale non è negoziabile**.
Non è la rifinitura finale del piano, è la slice che valida questa decisione.

**Decade** invece l'obiezione «un secondo connector ADO nello stesso processo»: valeva quando saremmo
stati ospiti del processo del loro sito, che carica `MySqlConnector`. In standalone il processo è nostro e
`MySql.Data` è l'unico connector presente. Se un giorno l'embedding si farà, i due connector coesisteranno
— sono librerie indipendenti — ma va detto a loro prima, non scoperto dopo.

### Conseguenza su D1 — invariata, e ora più stringente

Resta valido quanto scritto in D4: MySQL **non riusa** il path (a) `EnsureCreated` + reconciler, ma
migrazioni dedicate, cioè la forma (b). La ragione è la stessa e vale di più ora che il database è di
produzione: il reconciler copre **solo le aggiunte di colonna**, e la DDL di MySQL **non è transazionale**
(`ADD COLUMN IF NOT EXISTS` non esiste, e un reconcile interrotto lascia lo schema parziale senza
rollback). Il compromesso che accettiamo su Neon, che è casa nostra, non lo esportiamo da loro.

**Nuovo, nato col deploy standalone:** il key-ring Data Protection era «responsabilità dell'host
ospitante» finché l'host era il loro. Ora `Vipi.Host` *è* il processo di produzione, quindi è nostra —
vedi §S7 del piano.
