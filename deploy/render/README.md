# Deploy vIPI su Render (free) + Neon (Postgres free)

Runbook autorevole. Nessuna carta di credito. Risultato: `https://<nome>.onrender.com` con TLS
automatico, DB Postgres condiviso e persistente su Neon, login IVAO OIDC.

Caratteristiche/limiti del piano free:
- Il web service **dorme dopo 15 min** di inattività → primo accesso lento (~1 min cold start), poi normale.
- **Blazor Server**: ogni click è un round-trip al server → la **regione conta** (vedi §2, scegli Frankfurt).
- Lo schema Postgres si crea da modello (`EnsureCreated`) al primo avvio: **nessuna migrazione incrementale**.

---

## 1. Database Neon (Postgres)

1. Registrati su https://neon.tech (login Google/GitHub, no carta).
2. Crea un progetto → un database, **region EU (Frankfurt, `eu-central-1`)** — vicino all'Italia e alla
   regione Render (§2). Region diversa = latenza DB alta.
3. Dashboard → **Connection string** → formato **`.NET`** (Npgsql), NON l'URL `postgresql://` (Npgsql non
   lo accetta). Sarà tipo:
   ```
   Host=ep-xxx.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=xxx;SSL Mode=Require;Trust Server Certificate=true
   ```
   È il valore di `ConnectionStrings__Vipi`.

## 2. Web service su Render

Usa **New → Web Service**. Il servizio esistente è stato creato così, non da Blueprint: di conseguenza
`render.yaml` nel repo **documenta** la configurazione ma non la applica — i valori che contano (branch,
health check path, variabili) stanno nella dashboard, e un cambio nel file va replicato lì a mano.

1. Registrati su https://render.com (login GitHub, no carta).
2. **New → Web Service** → repo `SkyMistery/Digital_vIPI`.
3. **Branch**: `main`. Dal 2026-07-30 la produzione segue `main`: prima puntava a un branch di lavoro
   (`fix/airport-weather-tl-draft-preview`, ora cancellato) perché `main` era indietro di decine di commit.
   Con tutto mergiato quella deviazione non serve più, e faceva divergere «cosa è online» da «cosa è mergiato».
4. **Runtime**: Docker (usa il `Dockerfile` in root).
5. **Region**: **Frankfurt (EU Central)** ← selezionabile SOLO alla creazione (immutabile dopo; se sbagli
   devi ricreare il servizio). Oregon/USA = ~1s di latenza per-click dall'Italia.
6. **Plan**: Free.
7. **Environment** → aggiungi TUTTE queste variabili (i valori dei segreti vanno solo qui, mai nel repo):

   | Chiave | Valore |
   |--------|--------|
   | `Persistence__Provider` | `Postgres` (SENZA questa → default SQLite effimero, dati persi!) |
   | `ConnectionStrings__Vipi` | stringa Neon `.NET` del §1 |
   | `PORT` | `8080` (Render instrada qui; l'app ascolta su 8080 nel Dockerfile) |
   | `VipiAuth__Enabled` | `true` |
   | `VipiAuth__ClientId` / `VipiAuth__ClientSecret` | client OIDC IVAO |
   | `Ivao__ClientId` / `Ivao__ClientSecret` | token app IVAO (polling tracker) |
   | `Auth__AdminStaffCodes__0` | regex staff admin, es. `IT-.*` |

8. **Create** → Render builda il Docker e pubblica. Segna l'URL `https://<nome>.onrender.com`.

Verifica nei **Logs** che parta su Postgres: query con virgolette doppie (`"Documents"`) e NIENTE
`PRAGMA`/`AUTOINCREMENT` (quelle = SQLite = env `Persistence__Provider` mancante).

## 3. Redirect OIDC su IVAO

Portale sviluppatori IVAO → la tua app → redirect URI (identici all'URL Render):
```
https://<nome>.onrender.com/signin-oidc
https://<nome>.onrender.com/signout-callback-oidc
```
Devono combaciare esatti. Se cambi URL (es. ricreando il servizio) vanno aggiornati.

---

## Note architetturali

- **DB condiviso/persistente**: vive su Neon, non su Render → sopravvive a redeploy e allo sleep del web service.
- **Schema**: `EnsureCreated` al primo avvio. Per ricreare dopo un cambio di modello: azzera lo schema su Neon
  (drop) e riavvia. Nessuna migrazione incrementale su Postgres in questo setup (vedi ADR-0007).
- **Dev locale invariato**: senza `Persistence__Provider` resta SQLite con le migrazioni versionate.
- **Proxy TLS**: `Program.cs` usa `UseForwardedHeaders` → dietro il proxy Render l'app vede `https`
  (necessario per OIDC e per non andare in loop su `UseHttpsRedirection`).
- **Tenere sveglio** (opzionale): ping esterno ogni ~10 min (es. cron-job.org su `/vsop`) evita lo sleep.
- **Login ricordato 7 giorni**: cookie auth `ExpireTimeSpan=7gg` sliding + `IsPersistent=true` (`VipiStandaloneAuthExtensions`) → un solo login IVAO, sopravvive a chiusura browser. Le chiavi che firmano il cookie persistono su Neon (vedi DataProtection sopra), quindi il cookie resta valido anche dopo un redeploy.
- **Caricare dati sul DB Neon** (es. dal DB SQLite locale): `dotnet run --project tools/Vipi.DbSeed -- <vipi.db> "<connstring-postgres-.NET-o-URL>"`. Fa **TRUNCATE di tutte le tabelle** e reinserisce preservando gli ID (schema già creato da `EnsureCreated`). `--dry-run` al posto della connstring per contare solo le righe lette.

---

## Troubleshooting (problemi incontrati e risolti)

| Sintomo | Causa | Fix |
|---------|-------|-----|
| Build fallisce: `MSB4068 <Solution> unrecognized` su `Vipi.slnx` | Era l'immagine `sdk:8.0`, che non supportava il formato `.slnx`. Con `sdk:10.0` il formato è supportato | Dockerfile fa comunque `dotnet restore` del **csproj di Host**, non della soluzione: evita di scaricare i pacchetti dei progetti di test |
| Deploy senza le ultime modifiche | Il servizio punta a un branch diverso da quello atteso | Settings → Build & Deploy → **Branch** = `main` (fino al 2026-07-30 puntava a un branch di lavoro, vedi §2.3) |
| Cambio in `render.yaml` che non ha effetto | Il servizio non nasce da Blueprint: il file documenta, la dashboard decide | Replicare il valore a mano in Settings. È successo con `healthCheckPath` quando è passato a `/vsop/health/ready` |
| Gira su SQLite (log con `AUTOINCREMENT`/`PRAGMA`), dati persi al redeploy | Manca `Persistence__Provider=Postgres` (+ connection string) | Aggiungi gli env var (§2.7) |
| Latenza ~1s per click | Regione Oregon/USA (Blazor Server = round-trip a ogni azione) | Ricrea il servizio in **Frankfurt** |
| 500 `A second operation was started on this context instance` | Blazor Server: DbContext del circuito condiviso + render intermedio che monta figli DB-driven; latenza Postgres apre la finestra di overlap | Componenti figli DB-driven ereditano `OwningComponentBase` e risolvono i service DB dal proprio scope (EditLockBar, DocReviewBar, VloaEditor, VloaDocumentView, DocumentSectionsEditor, AirportQuickPanel). **Eccezione ReleasePanel**: NON isolato (publish composto col `BeforePublishAsync` della pagina → deve condividere il context) |
| Publish aeroporto si blocca (tasti freeze) | `22P02 invalid input syntax for integer: 'Twr'`: `.OrderBy(s => (int)s.Type)` su enum salvato come stringa → `CAST('Twr' AS integer)`; Postgres è stretto, SQLite tornava 0 | Ordinare in memoria, non in SQL. **Classe di bug**: mai `(int)<enum-stringa>`/CAST impliciti in query EF su Postgres |
| Warn `Failed to determine the https port for redirect` | `UseHttpsRedirection` dietro proxy (TLS al bordo) | Innocuo: Render forza già https, i ForwardedHeaders bastano |
| 500 `An exception has been raised that is likely due to a transient failure` | Neon serverless chiude le connessioni idle; EF non ritenta di default | `EnableRetryOnFailure` sul ramo Postgres (`Infrastructure/DependencyInjection.cs`). Retry-safe: `EfUnitOfWork` avvolge già le transazioni in `CreateExecutionStrategy()` |
| 500 `relation "DataProtectionKeys" does not exist` (`42P01`) su ogni render | Modulo DataProtection su DB ma tabella mancante; `EnsureCreated()` verifica il *database* (già creato), non la tabella | `CREATE TABLE IF NOT EXISTS "DataProtectionKeys"` all'avvio (`VipiDataProtection.UseVipiDataProtection`) |
| 500 `A second operation was started on this context instance` su pagine pubbliche (home, vIPI ACC, vLOA) | `OnlineCount()`→`ResolveByCallsign` faceva lazy-load DB **durante il render** in overlap col context del circuito | `IStationResolver.Prewarm()` scalda le cache nel ciclo di vita async delle pagine (`AccVipiPage`/`SopHome`/`VloaListPage`). Regola: mai I/O DB durante il render |
| Token app IVAO `POST /v2/oauth/token → 400` (polling tracker / import ACC KO) | NON è codice (grant/scope validati col discovery OIDC): secret stale o app senza grant `client_credentials`/scope `tracker`+`configuration` | Rigenera/allinea `Ivao__ClientSecret` (Render) e `Ivao:ClientSecret` (user-secrets locali); abilita grant+scope sull'app sul portale IVAO. Il body d'errore è ora loggato (`IvaoTokenProvider`) |
| DataProtection keys in `/root/.aspnet/...`, utenti sloggati a ogni redeploy | Chiavi in container effimero | **RISOLTO**: modulo `VipiDataProtection` persiste il key-ring su Neon (tabella `DataProtectionKeys`) quando `Persistence__Provider=Postgres` |
