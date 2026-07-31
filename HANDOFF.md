# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 31 luglio 2026 (vista live: revisione + **unificazione per callsign** — doc 12)
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.

> **📄 Sessione 2026-07-30 (3) — uniformità dei tre documenti (vIPI ACC · vIPI APP · vLOA).** Branch
> `fix/uniformita-tre-documenti`, 17 commit, suite **640 → 663 verde**, verifica live confermata dall'owner.
> Carta completa: `docs/refactor/11-uniformita-tre-documenti.md`. Le cose da sapere subito:
> - **Il modello era unico, la rilettura no.** Ogni famiglia interpretava lo stesso `Document` a modo suo:
>   chiave di sezione, resa del contenuto editoriale, stato «nascosta», fallback della vista pubblica.
>   Sei difetti alti, tutti **invisibili ai test verdi** e trovati guidando l'app reale.
> - **Stato per-sezione ⇒ colonna su `DocumentSection`.** `IsHidden` (migrazione `AddSectionIsHidden`) e
>   `BeforeParentBody` (`AddSectionBeforeParentBody`) si aggiungono a `RenderMode` di doc 10: versionati e dentro
>   lo snapshot. Prima «nascondi» viveva in tre storage, due non versionati → **cambiava la pagina pubblica senza
>   pubblicare**. ⚠️ `CreateDraftAsync` non copiava i flag: aprire una bozza resettava `RenderMode` a `Frozen`.
> - **Chiavi di sezione univoche** (`custom:{guid8}`): la costante `"custom"` faceva collidere le sezioni libere.
>   Migrazione dati al boot (`IDocumentMaintenance`), non EF: le migration del repo sono SQLite-flavored.
> - **`?as=` non valido ⇒ pubblica CON derivate frozen.** Prima il fallback lasciava `_useFrozen=false`: il
>   congelamento AIRAC era bypassabile dall'URL.
> - **P7–P9 chiesti dall'owner in verifica live**: sotto-sezioni collocabili **prima** del corpo; coordinamenti
>   con il solo primo livello espanso; «Aree regolamentate» che nasce collassata (viewer **ed** editor).
> - ⚠️ **Viewer ed editor possono avere sequenze opposte per la stessa sezione** (vLOA/coordinamenti: il viewer
>   rende le direzioni nel padre, l'editor nelle figlie). Toccarne una sola ha prodotto un albero duplicato.
> - **§3bis del doc 11: «non-problemi verificati»** — due apparenti duplicazioni nei coordinamenti che sono dato
>   corretto. Leggerlo prima di «aggiustarle».

> **🖨️ Sessione 2026-07-30 (2) — stampa dei documenti + fix pubblicazione.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **631 → 640 verde**, build 0 warning. Schede complete:
> `docs/feature/2026-07-30-stampa-documenti.md` e `docs/feature/2026-07-30-pill-stato-dopo-publish.md`.
> Le cose da sapere subito:
> - **La stampa era rotta da sempre e in silenzio**: il blocco `@media print` in `vipi-theme.css` nascondeva
>   tutto e mostrava solo `.printable`, classe che **nessun markup applicava** → Ctrl+P dava un foglio bianco su
>   qualunque pagina. Ora c'è il foglio dedicato **`vipi-print.css`** (nasconde il chrome, contenuto nel flusso
>   normale, A4 verticale, `thead` ripetuto, colori informativi preservati, scala tipografica da carta) +
>   `PrintMeta` + tasto «Stampa» sui quattro viewer. Nessun endpoint di export: la stampa del browser copre
>   RNF-6 (piano §10, §22.7 aggiornati). **Dati live fuori dalla carta** per decisione: METAR/TAF e Ridotta.
> - **Tre trappole del browser, tutte invisibili ai test.** Un `<details>` chiuso **non si apre col solo CSS**
>   (Chrome lo nasconde da user-agent con `content-visibility` su `::details-content`) → serve l'hook
>   `beforeprint` (`wirePrint` in `vipi-ui.js`). **Chrome segnala la stampa due volte** (`beforeprint` + cambio
>   media `print`) → gli handler di stampa vanno resi **idempotenti**, o il ripristino post-stampa non avviene.
>   **Leaflet** tiene la propria dimensione in memoria: ridurre l'altezza da CSS **ritaglia** la mappa invece di
>   riadattarla (serve `invalidateSize` + refit).
> - **«Bozza vN» dopo «Pubblica ora» era solo la pill**, non la pubblicazione (release `Effective`, audit e
>   documento promosso erano corretti): `ReleasePanel` ricaricava solo le proprie release senza avvisare l'host.
>   Ora ha un `EventCallback Published` che i tre editor agganciano al proprio `LoadAsync`. ⚠️
>   `string.Format(L["chiave"].Value, n)` **non interpola** — serve l'overload `L["chiave", n]`.
> - **⚠️ Chiave di release ACC**: `"{acc}|{root}"` — la parte `root` sceglie *quale* albero/documento si
>   pubblica e **va rispettata**. `AccVipiReleaseTarget` la scartava (primo CTR radice per `CoverageOrder`): su
>   una ACC multi-albero avrebbe promosso la bozza del documento sbagliato, in silenzio. Corretto.
> - **Razor scarta il testo di sola spaziatura che precede un blocco di codice**, anche dentro `<text>`: la
>   legenda piste usciva «recommended**from** the METAR wind». Lo spazio va scritto come entità `&#32;`.
>   Stessa famiglia della trappola `v@r.Proprietà` (sessione precedente).

> **⚠️ Sessione 2026-07-30 — audit concorrenza / codice morto / ridondanze.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **505 → 631 verde**, build 0 warning. Documento completo:
> `docs/history/audit-2026-07-30-concorrenza-e-ridondanze.md`. Le tre cose da sapere subito:
> - **Import SID era rotto in silenzio** su LIRF/LIMC/LIME/LIBG/LIED/LIEO/LIPQ (ogni *reimport* falliva: snapshot
>   costruito con `ToDictionaryAsync(StableKey)` su chiave legittimamente ripetuta; il job logga a `LogDebug`).
>   Fixato. ⚠️ **La `StableKey` NON è unica per design** — non aggiungere un indice unico, fallisce sui dati veri.
> - **Le migration si provano su una copia di `src/Vipi.Host/vipi.db`**, non solo su DB vuoti da `EnsureCreated`:
>   i test partono sempre da vuoto e non vedono questa classe di problemi.
> - **Nuova skill `.claude/skills/verifica-live/`** per lanciare e guidare l'app in locale (la procedura non era
>   scritta: `dev-bootstrap.md` si fermava a `dotnet run`, e serve `VipiAuth__Enabled=false` per entrare).
>   Guidandola è uscito `rel. v@r.VersionNumber` **letterale** a schermo: in Razor una `@` fra due caratteri
>   non-spazio è letta come **indirizzo email** e non apre un'espressione, senza alcun warning → usare `v@(...)`.
>
> Aperto, **non di codice**: la SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft», quota
> implausibile (le altre BANAV hanno `9000` → «FL90»). Da correggere nell'editor.

> **⚠️ Sessione 2026-07-29 — hardening deploy Render+Neon (leggere se si lavora sul deploy hostato).** Il sito test gira su Render+Neon Postgres (vedi `deploy/render/README.md` e memoria [[deploy-hosting-options]]). Fix di questa sessione, tutti su branch `fix/airport-weather-tl-draft-preview`:
> - **Login IVAO ricordato 7 giorni** (`VipiStandaloneAuthExtensions.cs`): cookie `ExpireTimeSpan=7gg` sliding + `IsPersistent=true` sul challenge → un solo login, sopravvive a chiusura browser.
> - **Retry-on-failure Neon** (`Infrastructure/DependencyInjection.cs`, ramo Postgres): `EnableRetryOnFailure` — Neon serverless chiude le connessioni idle, la prima query dava 500 `transient failure`. ⚠️ **Corretto il 30 lug:** questa nota diceva «retry-safe perché `EfUnitOfWork` avvolge già le transazioni in `CreateExecutionStrategy()`» — **necessario ma non sufficiente.** Al retry la strategy rigira la lambda sullo stesso context scoped e il rollback non ripulisce il change-tracker, quindi le entità del tentativo fallito venivano riemesse (doppi insert). Ora `EfUnitOfWork` azzera il tracker a ogni tentativo.
> - **DataProtection su Postgres** (`src/Vipi.Host/VipiDataProtection.cs`, modulo staccabile): su Render il container è effimero → il key-ring di default si perdeva a ogni redeploy (antiforgery rotto + logout). Ora le chiavi vanno su un `DbContext` dedicato (tabella `DataProtectionKeys` su Neon). ⚠️ **NON** `EnsureCreated()` (verifica il *database*, non la tabella → non creava nulla sul DB esistente): la tabella si crea con `CREATE TABLE IF NOT EXISTS`. Attivo solo se `Persistence:Provider=Postgres`; in dev SQLite resta il file-store.
> - **StationResolver.Prewarm()** (fix crash `A second operation was started`, memoria [[blazor-dbcontext-concurrency]]): `OnlineCount()` faceva lazy-load DB **durante il render** su `AccVipiPage`/`SopHome`/`VloaListPage`. Nuovo `IStationResolver.Prewarm()` scalda le cache nel ciclo di vita async. **Regola: nessuna I/O DB durante il render, nemmeno lazy via service scoped.**
> - **Tool `Vipi.DbSeed`** (copia SQLite locale→Neon): fix ciclo `Document↔DocumentVersion` (insert a 2 fasi con `CurrentVersionId=null`). Uso: `dotnet run --project tools/Vipi.DbSeed -- <vipi.db> "<connstring-postgres>"` (fa TRUNCATE+reseed).
> - **`IvaoTokenProvider`**: logga il body d'errore sui token 400 (prima `EnsureSuccessStatusCode()` lo scartava).
>
> **⏳ APERTO — token app IVAO (400):** il polling tracker + import ACC falliscono con `POST /v2/oauth/token → 400`. Diagnosi: **NON è codice** (endpoint/grant/scope validati col discovery OIDC IVAO). È il **secret/app sul portale**: o `Ivao:ClientSecret` stale nei user-secrets, o l'app `fc95c992…` non ha grant `client_credentials`/scope `tracker`+`configuration` abilitati. Il nuovo log mostra l'`error` esatto nel body. Nota: `Ivao:ClientId == VipiAuth:ClientId` (stessa app IVAO per login utente + token app). Aggiornare il secret sia in user-secrets locali sia in `Ivao__ClientSecret` su Render.
>
> **NB dev locale:** per testare login/logout in locale serve `VipiAuth:Enabled=true` in `appsettings.Development.json` (spegne l'utente dev fittizio → login IVAO vero) + redirect `http://localhost:5034/signin-oidc` e `/signout-callback-oidc` registrati sul portale IVAO. Questo flag è tenuto **fuori dai commit** (preferenza locale).

> **⚠️ Stato corrente (2026-07-21) — leggere prima.** Dopo il Round 34 il progetto è passato per l'**asse di refactor strutturale `docs/refactor/01→10` (tutti eseguiti)**: modello **`Document`+`DocumentVersion` unificato** per tutti e 4 i tipi (vIPI ACC / APP / Airport / vLOA), editing e storage su documento (doc 08); **flusso di pubblicazione generico** via registry `IReleaseTarget`/`IDocKindRoutes` (doc 09); **snapshot totale al publish + `RenderMode` per sezione** con **visibilità pubblica = release effettiva** (doc 10, merged). Aggiunta **retention pubblicazione** (anti-bloat: pota release `Superseded` oltre 13 cicli e versioni `Archived` oltre 3/documento; per-publish + boot sweep `PruneVipiReleases`). **Fix 2026-07-21:** off-by-one del cap `Archived` su **entrambi** i path publish (release-publish `ReleaseService.PublishNowAsync` e version-publish `EditingService.PublishAsync`) — ora il prune gira dopo l'archiviazione. Suite **358 verde**. Dettagli in `docs/history/rounds.md` (in coda), `docs/refactor/00-overview.md` e memoria `publication-retention-plan`. **NB:** le sezioni §4→§8 qui sotto descrivono lo stato a Round 34 e NON riflettono ancora l'asse 08→10 (modello/pubblicazione): in caso di conflitto valgono i doc `refactor/` + `spec/modello-dati.md`.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 10 a 4 layer + Host Blazor Server, consultazione+editing+sicurezza dal DB. **Import SID da GitHub** (sectorfile Aurora `ivao-italy/it-aurora-sector`): parser + completion fix/VOR + alias, merge preserva-manuali, priorità per punto persistente (StableKey), pubblicazione differita al ciclo AIRAC N+1 (round 34, `AddSidImport`). **Import periodici gated** (`ImportState`, `AddImportState`): niente più fetch-all a ogni riavvio (round 34). **Vista live UNIFICATA** (`/vsop/live[/{callsign}]`, doc refactor 12): una pagina per callsign, descrittori per tipo di ente (CTR/APP/**TWR/GND/DEL**), postazione dalla connessione IVAO senza selettore, **non richiede una vIPI pubblicata** (è legata all'ente, non al documento) + vista rapida aeroporto inline (`AirportQuickPanel`); QoL admin `sectorstructure`/`trasferimenti` (round 34). **Versioning AIRAC**: release schedulate per ciclo su TUTTI i tipi (`DocRelease`; round 29, §9.17) + **task management editor**. **Anteprime unificate `?as=`** nei viewer tipizzati (round 33). **vLOA data-driven** + **ACC esteri confinanti** (round 27-28, §9.16). **vIPI ACC/APP data-driven a blocchi** (round 21/23). **Live IVAO** (polling + cache + SSE). **Sorgente dati disaccoppiata** + **policy di import opt-out** (categorie: TA/Runways/Sectors/**Sids**). Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia per callsign cross-ACC (Round 20).

> **📡 Sessione 2026-07-31 — vista live.** Branch `feat/vista-live`, 14 commit, suite **631 → 702 verde**,
> verifica live su 12 postazioni. Carta: `docs/refactor/12-vista-live-unificata.md`. Da sapere subito:
> - **Una pagina sola, keyed sul callsign**: `/vsop/live` (la tua postazione, dalla connessione IVAO —
>   **nessun selettore**) e `/vsop/live/{callsign}` (consultazione). Via `AccLivePage`/`AppLivePage` e le due
>   `Ridotta*` morte. Le rotte storiche fanno **301 a un salto solo**.
> - **La vista è legata all'ENTE, non al documento**: senza vIPI pubblicata degrada a banner e continua a
>   rendere trasferimenti, AoR e frequenze dai cataloghi. Non reintrodurre early-return sul documento.
> - **Descrittori per tipo** (`ILiveStationKind`, come `IReleaseTarget`): **torri, ground e delivery hanno una
>   vista live** che prima non esisteva. Un test verifica che ogni `SectorType` abbia un descrittore.
> - ⚠️ `/vsop/live/{callsign}` ricade sul prefisso dello stream SSE `/vsop/live/atc`: vince il segmento
>   letterale, ma è una proprietà del routing che si rompe cambiando le rotte → smoke dedicato.
> - ⚠️ In verifica: `innerText` su un `<details>` **chiuso** torna stringa vuota — un'asserzione ingenua la
>   legge come «elemento assente».
>
> - **Il padre dell'aeroporto non arrivava alle sue posizioni** (segnalato dall'owner, fixato): la proiezione
>   leggeva solo `AirportSector.ParentCallsign` (solo APP) e ignorava `Airport.ParentCallsign`, che è il campo
>   che l'admin compila in Struttura → torri/ground/delivery orfani. Ora scaletta **DEL→GND→TWR→APP** + uscita
>   sul padre dell'aeroporto, riproiettata all'avvio (`ProjectVipiSectors`). Reggeva anche la risalita dei
>   trasferimenti: un punto verso una torre offline finiva su UNICOM invece che all'APP.
>
>   Fra pari grado si sceglie **coi dati**: la radice del sottoalbero APP (gerarchia scritta dall'admin, es. le
>   sei APP di LIRF pendono da `LIRF_TW1_APP`), poi il callsign senza infisso (`LIRF_TWR` vs `LIRF_E_TWR`), e se
>   resta ambiguo si **sale** invece di tirare a sorte.
>
> Aperto, **di dato**: 33 torri di aeroporti senza APP e senza padre configurato in Struttura. Aperto, **di UI**:
> TWR/GND/DEL non sono nodi editabili in `/vsop/admin/sectorstructure` (lo sono ACC, APP e Aeroporto), quindi i
> gradini ambigui — due ground entrambi sdoppiati, come a Malpensa — non si correggono a mano.

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R34). **Indice doc:** `docs/index.md`. Ultimo round: **34** — vista operativa + QoL admin + import SID GitHub + gating import; modello in `docs/spec/modello-dati.md` §9.8 (migrazioni). (R33: anteprime `?as=`; R30: QoL Bozze & versioni §9.18; R29: versioning AIRAC + task §9.17.)

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 631 test (Domain 23 · App 273 · Infra 228 · Hosting 18 · Ui/bUnit 85 · E2E 4)
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
- 🔎 **Per verificare una modifica UI a schermo** (non solo coi test): skill **`.claude/skills/verifica-live/`** —
  avvio su una copia del DB, driver Edge+puppeteer-core, bersagli e trappole già mappate. Le regressioni Blazor
  sono silenziose coi test verdi, quindi il runbook chiede di guidare il flusso reale.
- ⚠️ **AZIONE PENDENTE (2026-07-22, audit Fase 1):** **RIAVVIARE il Host** per applicare `AddImportStateLastError` (additiva: `ImportState.LastAttemptUtc`/`LastError`). Poi `/vsop/admin/sorgenti` mostra il **report stato import** (ultimo successo/tentativo/errore per categoria). Nota: da questa sessione `/vsop/health` è **Unhealthy (503)** se ci sono migrazioni pendenti (schema drift). Audit completo: `docs/history/audit-2026-07-22-criticita-full-stack.md`. Nuova rete di test: `Vipi.Ui.Tests` (bUnit) + `Vipi.E2E.Tests` (WebApplicationFactory in-process).
- ℹ️ **FASE 2 audit ESEGUITA (2026-07-22, nessun cambio schema):** **B1** report consistenza soft-ref in **`/vsop/admin/diagnostica`** (pista orfana · label pista divergente · area fantasma · gerarchia `ParentCallsign` dangling) — solo diagnosi, nessun auto-fix; `IConsistencyReportService`/`Analyze` (logica pura) + `IConsistencyReportRepository` (EF read-only); se ci sono finding, `/vsop/health` → **Degraded**. **C1** XSS: `HtmlEncode` dei valori dinamici in `StrutturaPage`/`AeroportoPage` (pattern gemello `SearchPage`/`MarkdownLite`).
- ℹ️ **FASE 3 audit ESEGUITA (2026-07-22) — parte code, resto pianificato in ADR-0007:** **A1** tampone concorrenza SQLite `SqliteTuningInterceptor` (WAL + `busy_timeout`) nel path `UseSqlite`; **D1** `ProductionIdentityGuard.EnsureSafe` in `Program` fa **hard-fail** all'avvio se l'identità dev è attiva fuori da Development (no admin-onnipotente in prod); test path prod `HostIdentityCurrentUserProvider` (nuovo progetto `Vipi.Hosting.Tests`). **A1 cutover Postgres + A2 scala Blazor = pianificati in `docs/adr/adr-0007-produzione-persistenza-e-scala.md`** (non attuati: servono migrations Postgres dedicate + istanza di validazione + backplane). **ESTERNI residui:** montare la RCL nel sito host + configurare `HostIdentity` coi claim/staff-code IVAO reali; eseguire il cutover Postgres; provisioning backplane.
- ℹ️ **MINORI audit ESEGUITI (2026-07-22):** **C4** `StrutturaPage` — estratti i `RenderFragment` HTML-a-mano in componenti dichiarativi `StructureCoverage`/`StructureFallbackChain` (chiude C1 alla radice, +6 bUnit con regressione XSS). **B4** spec §3 marcata `[SUPERATO]` (usa §9). **B3** nuova checklist `docs/guide/dev-bootstrap.md` (coerente «Nessun seed»). **C3** chiuso come non-issue (aor3d già off; AoR block = editoriale, non stub). Onboarding dev: vedi `docs/guide/dev-bootstrap.md`.
- ⚠️ **AZIONE PENDENTE (2026-07-22):** **RIAVVIARE il Host** per applicare le migrazioni pendenti dei trasferimenti — `AddTransferPointConditionArea` poi **`SplitTransferConditionColumns`** (backfilla e droppa `ConditionKind`). Sessione 22 lug: condizione trasferimenti = **tre colonne indipendenti** (pista multi-select · area con **ricerca a digitazione** · personalizzata), enum `TransferConditionKind` **rimosso**; fix condizione «Pista» che legge le **piste reali** `AirportRunways` (non le config); bottone **«Re-importa da IVAO (tutti)»** su `/vsop/admin/airports`. Verifica live su LIBD. Suite **19 dom + 205 app + 174 infra** verde. Dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7-7.2, memorie `transfer-condition-model` / `airport-runway-import`.
- ⚠️ **NOTA (Round 34):** il **`vipi.db` dev è stato resettato** a fine sessione (testando il gating import). Al primo avvio ripopola da zero (ACC → settori → aree → SID) e stampa lo stato in `ImportStates`; i riavvii successivi **saltano** i fetch finché non scadono i 24h (o via bottoni manuali). Le SID importate sono pubbliche solo dal ciclo AIRAC successivo.
- ⚠️ **AZIONE PENDENTE (Round 22):** **fermare e RIAVVIARE il Host** per applicare la migrazione **`AddAirportCoordsAndTwrSyntheticShape`** (additiva) e far girare il job che (a) popola `Airport.Latitude/Longitude` dal dettaglio ATCPositions e (b) genera le **shape tonde 5 NM** per le TWR vuote (`/v2/ATCPositions/{compose}.regionMapPolygon = "[]"`). Il job parte ~30s dopo l'avvio. Poi su `/vsop/{acc}/apps/vipi?app={APP}` l'AOR mostra il cerchio della torre col toggle «Shape torre». ⚠️ Credenziali IVAO in **user secrets** (`Ivao:ClientId/ClientSecret`), scope `tracker` basta per il dettaglio postazione. Il Host viene **fermato** a fine sessione (blocca le DLL in build).
- ⚠️ **AZIONE PENDENTE (Round 20):** se il DB è ancora pre-round-20: **reset `src/Vipi.Host/vipi.db`** in dev (o applica `AddHierarchyParentCallsign`) → riavvia. Poi `/vsop/admin/acc` → «Importa da sorgente»: la **sync** popola i `Sector` dai cataloghi; in `/vsop/admin/sectorstructure` compare l'**albero di copertura globale** (cross-ACC).
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`). **Nessun seed**: si parte da DB **vuoto**. Flusso dati reale: `/vsop/admin/acc` importa ACC+settori dalla sorgente → la sync proietta i `Sector` → la **gerarchia** (padri per callsign) si imposta in `/vsop/admin/sectorstructure` → «Crea nuovo documento» (vIPI = N settori di scope, uno primario) → editor. **I settori NON si creano più a mano** (sono proiezione dei cataloghi, Round 20). Cancella `vipi.db*` per ripartire da zero. I `*Seed.cs` di Roma restano solo come fixture nei test.
- In dev l'utente è `DevCurrentUserProvider` (VID 704798, staff `IT-AOC` → **admin**, può tutto).
- Migrazioni: `dotnet ef migrations add <Nome> --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations`. ⚠️ Per i **rename** di proprietà/colonna EF scaffolda `RENAME COLUMN` solo se i campi combaciano: **verificare a mano** la migrazione generata (no Drop+Add che perde dati).

## 3. Mappa documenti
Indice completo con scopo e stato di ogni documento: **`docs/index.md`**. In sintesi:
- `README.md` (cos'è + architettura + build) · **questo `HANDOFF.md`** (leggere per primo per riprendere).
- `docs/history/rounds.md` (changelog dei round) · `docs/spec/` (modello dati, logica AoR, mappa pagine) · `docs/guide/` (config, integrazione) · `docs/adr/` (decisioni) · `docs/design/` (piano) · `docs/reference/sector-map.md`.

---

## 4. STATO CODICE — cosa è implementato (e dove)

**Solution (Clean Architecture, net10.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

**Cuore AoR/visibilità (✅ testato S1–S10):** `Application/Aor/AorService.cs` (ownership/stato settori, top-down, unificazioni), `Topology.cs`, `Infrastructure/Aor/TopologyBuilder.cs` (implementa la porta `ITopologyProvider`). Tabella di verità visibilità in `Application/Content/ContentService.cs`.

**Consultazione dal DB (✅):** pipeline `IContentRepository` → `IVipiViewService` → `SectionNode`/`BlockRenderer`. Rotte sotto `/vsop`:
- `/{acc}/vipi` (Estesa ACC) · `/{acc}/ridotta` (proiezione tier Reduced + sezione Trasferimenti) · `/{acc}/airports?icao=` (vIPI aeroporto) · `/{acc}/vloa`.
- `/search` (ricerca full-text reale), `/changed` (cosa è cambiato nel ciclo AIRAC), `/{acc}/export` (Estesa → stampa/PDF browser).
- **SID ✅ reali** (round 34): importate dal sectorfile Aurora GitHub, editor aeroporto + `AirportQuickPanel`. Stub residui: mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico). METAR/TAF = reale (NOAA).

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}`), `VersioniPage`.
- Editor specializzati: `AdminTrasferimentiPage` (trasferimenti, pagina admin globale `/vsop/admin/trasferimenti`: selettore ACC + flussi/punti nidificati, Next cross-ACC; ex per-ACC `XferEditorPage` rimosso) — **round 22:** flussi e punti **editabili in-place** (bottone ✎, oltre a ✕) via `ITransferService.UpdateFlowAsync`/`UpdatePointAsync`. `VloaEditorPage` (redirect all'editor generico). Gerarchia di copertura in `StrutturaPage` (`/vsop/admin/sectorstructure`).
- **Editor APP non remotizzati (✅ round 21):** `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`) WYSIWYG con 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + custom, riordino drag-and-drop+tasti, nascondi sezioni; viewer `AppnPage` data-driven. Entità `AppProfile`/`AppFrequencyLink` (modello §9.13), service `IAppProfileService` (freq/coord/AOR **derivate live**), `AorPolygonProjector`, registry `AppSections`, componenti `Vipi.Ui/Components/App/*`, mappa AOR Leaflet (`vipi-aor.js`). Instradamento via `DocumentSummary.IsStandaloneApp`. **Round 22:** «Trasferimenti verso ACC» suddiviso in sottosezioni **Partenze/Arrivi** (`AppCoordinationView`, split per `Kind`); **AOR** mostra anche le **shape delle TWR** dello stesso aeroporto come overlay Leaflet con toggle «Shape torre» (`GetTowerPolygonsAsync`). ⚠️ **`TopologiaPage` rimossa** (`/vsop/{acc}/topologia`): gerarchia → `sectorstructure`; le regole di unificazione + simulatore AoR erano legacy e non hanno più UI (motore `IAorService` + `UnificationRule` + test S1–S10 **restano**).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position da due set: **ruoli di divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) **e ruoli ACC-scoped/chief** (`AdminAccRolePatterns` → `^{prefissoIcao}[A-Z0-9]+-{ruolo}$`, es. `LIRR-CH`/`LIMM-ACH`) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes`. **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code`, `IcaoPrefixes`, `AdminRolePatterns`, `AdminAccRolePatterns`. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant. Schermata `/vsop/admin/permessi` (solo admin).
- **Lock** documento esclusivo (30 min sliding, atomico via `ExecuteUpdateAsync`, **force admin**) → `EditConflictException`. **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`).
- **Lock risorsa** per le pagine admin senza documento (`EditResourceLock`, `IResourceLockService`): le 4 pagine di struttura condividono `admin:structure`, newdoc ha `editor:newdoc`; una persona alla volta (barra `EditLockBar`, TTL 3min + heartbeat 60s + force admin).
- **Validazione**: `UnificationRule` hard, trasferimenti soft. Verifiche **sempre server-side**. Security review: XSS in `AorBlock` corretto.

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; **lista migrazioni autoritativa = `docs/spec/modello-dati.md` §9.8** (fino a **`AddAirportCoordsAndTwrSyntheticShape`**, round 22). Seed (solo fixture di test, **non** seminato all'avvio): `RomaStructureSeed`, `RomaContentSeed`, `RomaAirportSeed`, `RomaVloaSeed`, `RomaTransferSeed`. ⚠️ **In produzione i `Sector` sono una proiezione dei cataloghi** (round 20): non si creano a mano, vedi `docs/spec/modello-dati.md` §9.12.

**Modello dati — aggiunte rispetto a `docs/spec/modello-dati.md` §3:** **`TransferFlow`** (settore mittente + tipo + aeroporto) → **`TransferPoint`** (CoP/livello strutturato/settore ricevente `NextSector`); risoluzione live **risale la gerarchia globale** (`ParentCallsign`/`ParentSectorId`), terminale **UNICOM** (no enum fallback). `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, `IOnlineAtcProvider`), `IvaoApiClient` (`/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, solo per i membri divisione: tracker pubblico), `AtcPollingHostedService` (60s), `IvaoOptions`. Transport **SSE** `/vsop/live/atc` + `vipi-live.js`. `VipiViewService` calcola AoR reale quando `live=true`; `RidottaPage` `InteractiveServer`. Decisione in **ADR-0003**.

**Indipendenza dalla sorgente (✅, ADR-0006):** porte dati esterne **neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`, DTO `Source*`); adapter IVAO selezionato da **`DataSource:Provider`**. `Vid`→`UserId` ovunque (a video resta "VID"). **Policy di import** (`ImportPolicy`, categorie `{TransitionAltitude, Runways, Sectors}`, pagina `/vsop/admin/sorgenti`): dati di sorgente in sola lettura, enforcement a difesa in profondità.

**Fonte unica settori (✅ Round 20):** cataloghi `AccSector`/`AirportSector` = fonte autoritativa; `Sector` = proiezione (`ISectorProjectionService.SyncFromCatalogsAsync`). Gerarchia per callsign (`ParentCallsign`, cross-ACC) editata in `/vsop/admin/sectorstructure` (`IHierarchyEditingService`). Dettagli: `docs/spec/modello-dati.md` §9.12.

**Shape tonda TWR + coord aeroporto (✅ Round 22):** le TWR senza poligono reale (IVAO le espone come `"[]"`) ricevono una **shape circolare 5 NM** sintetica così da poterle disegnare. `CircleShapeBuilder` (puro, formato `[[lng,lat],…]`), `TowerShapeFallbackService` (genera solo sulle vuote — decise col `AorPolygonProjector` —, marca `IsShapeSynthetic=true`, mai sovrascrive shape reali). Centro = `Airport.Latitude/Longitude`, popolate all'import dal blocco `airport` del dettaglio `/v2/ATCPositions/{compose}` (`SourceAtcPosition.AirportLatitude/Longitude`); ripiego = centro del poligono di un settore fratello. Job in `AirportSectorImportHostedService` (import isolato in try: il fallback gira anche senza credenziali). **TODO futuro:** shape reali TWR dal **sectorfile GitHub** via `DataSource:Provider` → rimpiazzano solo le sintetiche. Dettagli: `docs/spec/modello-dati.md` §9.14.

---

## 5. PROSSIMI PASSI (ordinati per valore)

1. **Live IVAO — rifiniture aperte:**
   - **Identità "P"** legata al callsign connesso del CH loggato (oggi selettore manuale in Ridotta).
   - **Mapping token-handler → callsign** trasferimenti (oggi euristica match-segmento). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali:** METAR/TAF ✅ (NOAA). Shape AoR ✅ (poligono IVAO). **SID ✅** (sectorfile Aurora GitHub, round 34, sez. config `Sectorfile`). **AoR 3D ✅** (Three.js r128 vendorizzato: tab 2D/3D nel blocco AoR + pagina `/vsop/aor3d/{Kind}/{Key}`; settori estrusi per banda FL, con **basemap geografica CartoDB come pavimento** — proiezione Web Mercator, toggle «Mappa base» — e rendering leggibile: altezza adattiva/opacità/etichette). Restano: **shape reali TWR dal sectorfile GitHub** (rimpiazza le sintetiche `IsShapeSynthetic`), **minime MVA** (`<icao>.mva` stesso repo — riusa il pattern SID: parser + import gated + pubblicazione differita). Nota AoR 3D: i settori senza limiti admin estrudono GND→UNL (banda piatta) → il rilievo 3D emerge solo coi `LowerLimit`/`UpperLimit` valorizzati.
3. **Fonte unica (Round 20) — follow-up:** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`) = fase live. ✅ **Fatto per i trasferimenti:** `ITransferService.ResolveForAccAsync` + `ITopologyProvider.BuildGlobalAsync` risolvono mittente e ricevente risalendo la gerarchia globale (terminale UNICOM); Ridotta li mostra nidificati Settore ▸ Aeroporto ▸ Tipo. Resta da estendere la stessa risalita alla "presidenza aeroporto" generale.
4. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (A/B, claim `Ivao.It`) e OIDC (C); mappare gli **staff code reali** (§6). Montare la RCL nel sito host.
5. **Copertura/rifiniture:** viewer **audit log**, "scarta bozza", editor visuale mappe AoR, test property-based AoR, rifinitura UI.

---

## 6. Nodi aperti / decisioni
**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli di divisione (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`) **e** ruoli chief ACC-scoped (`{ACC}-CH`/`{ACC}-ACH`, es. `LIRR-CH`), da confermare col sito host. I chief (CH/ACH) ora **sono** admin completi (`AdminAccRolePatterns`); l'auto-elenco per il dropdown grant resta via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

**Risolte (storico):** modello editing persistente; autorizzazione (admin via staff code + grant per-ACC); lock 30 min + force admin; validazione hard/soft; export = stampa browser; trasporto live = **SSE** (ADR-0003); polling cache singleton 60s.

**Fix collaterali round 21:** `NewDocumentPage` naviga all'editor con **`forceLoad:true`** dopo la creazione (evitava lo stale read «documento non esiste»). `AdminTrasferimentiPage` — i dropdown sector-pick selezionano su **`@onmousedown`** (non `@onclick`): in Blazor Server il `@onblur` chiudeva il dropdown prima del click.

**Nota tecnica round 22 (importante per il debug):** la sorgente IVAO espone le TWR con **`regionMapPolygon = "[]"`** (array vuoto), **non** null — il «vuoto» NON si rileva in SQL (`null`/`''`) ma **provando a proiettare** col `AorPolygonProjector` (`Project(raw) is null` ⇒ vuoto/degenere). Il centro del cerchio viene dal blocco **`airport`** del dettaglio **`/v2/ATCPositions/{compose}`** (NON da `/v2/airports`, che richiede scope `configuration`). Credenziali IVAO reali in **user secrets** (id `79756a9b-…`), `appsettings.json` le ha **vuote**. Le coordinate si popolano solo all'**import** (job all'avvio), quindi serve **riavviare il Host** per vederle.

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/guide/config.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **ACC pilota:** Roma (LIRR). Validare su una sola ACC prima di estendere.
- **Brand:** palette §15.1 di `docs/design/piano-vipi-tool.md` (blu `#0D2C99`…), font Nunito Sans + Poppins; tema in `Vipi.Ui/wwwroot/vipi-theme.css` (include `@media print`).
- **Parte più rischiosa:** logica AoR/visibilità → coperta da test S1–S10; mantenerla testata ad ogni modifica.
- **Pagine interattive** usano `@rendermode InteractiveServer` (editor, trasferimenti, ricerca, changed, admin).
- **Sicurezza:** ogni nuova operazione di scrittura deve passare per i service Application (guardia authz + lock), mai bypassare dal repo/UI.
- **Sorgente dati (ADR-0006):** non reintrodurre nomi IVAO in Application/UI — usa le porte neutre; l'adapter IVAO resta in `Infrastructure/Ivao/*`, selezionato da `DataSource:Provider`.
- **VID vs UserId:** nel **codice** è `UserId`; a **video** resta "VID". Non rinominare le label.
- **Dati di sorgente = sola lettura:** se aggiungi un campo che la sorgente può fornire, trattalo come categoria `ImportPolicy` (vedi `source-decoupling-and-import-policy` in memoria). I settori sono proiezione dei cataloghi (Round 20).

---

## 8. Mockup v2 — storico UI
Il mockup `mockups/vipi-ui-mockup-v2.html` (17 schermate) resta il riferimento visivo. Le schermate sono state derivate in componenti Blazor reali (vedi §4). Note: SCCAM e Aree regolamentate sono sezioni top-level; la vLOA ha due AoR e due tabelle frequenze; gli APP non remotizzati separano i trasferimenti verso ACC e verso torre.
