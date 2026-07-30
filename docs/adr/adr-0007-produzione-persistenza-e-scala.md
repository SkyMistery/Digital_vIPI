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
