# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 21 luglio 2026 (asse refactor 01→10 concluso + retention pubblicazione + fix off-by-one cap Archived)
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.

> **⚠️ Stato corrente (2026-07-21) — leggere prima.** Dopo il Round 34 il progetto è passato per l'**asse di refactor strutturale `docs/refactor/01→10` (tutti eseguiti)**: modello **`Document`+`DocumentVersion` unificato** per tutti e 4 i tipi (vIPI ACC / APP / Airport / vLOA), editing e storage su documento (doc 08); **flusso di pubblicazione generico** via registry `IReleaseTarget`/`IDocKindRoutes` (doc 09); **snapshot totale al publish + `RenderMode` per sezione** con **visibilità pubblica = release effettiva** (doc 10, merged). Aggiunta **retention pubblicazione** (anti-bloat: pota release `Superseded` oltre 13 cicli e versioni `Archived` oltre 3/documento; per-publish + boot sweep `PruneVipiReleases`). **Fix 2026-07-21:** off-by-one del cap `Archived` su **entrambi** i path publish (release-publish `ReleaseService.PublishNowAsync` e version-publish `EditingService.PublishAsync`) — ora il prune gira dopo l'archiviazione. Suite **358 verde**. Dettagli in `docs/history/rounds.md` (in coda), `docs/refactor/00-overview.md` e memoria `publication-retention-plan`. **NB:** le sezioni §4→§8 qui sotto descrivono lo stato a Round 34 e NON riflettono ancora l'asse 08→10 (modello/pubblicazione): in caso di conflitto valgono i doc `refactor/` + `spec/modello-dati.md`.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 8 a 4 layer + Host Blazor Server, consultazione+editing+sicurezza dal DB. **Import SID da GitHub** (sectorfile Aurora `ivao-italy/it-aurora-sector`): parser + completion fix/VOR + alias, merge preserva-manuali, priorità per punto persistente (StableKey), pubblicazione differita al ciclo AIRAC N+1 (round 34, `AddSidImport`). **Import periodici gated** (`ImportState`, `AddImportState`): niente più fetch-all a ogni riavvio (round 34). **Vista operativa ACC** rifatta sul mockup `#reduced` + vista rapida aeroporto inline (`AirportQuickPanel`); QoL admin `sectorstructure`/`trasferimenti` (round 34). **Versioning AIRAC**: release schedulate per ciclo su TUTTI i tipi (`DocRelease`; round 29, §9.17) + **task management editor**. **Anteprime unificate `?as=`** nei viewer tipizzati (round 33). **vLOA data-driven** + **ACC esteri confinanti** (round 27-28, §9.16). **vIPI ACC/APP data-driven a blocchi** (round 21/23). **Live IVAO** (polling + cache + SSE). **Sorgente dati disaccoppiata** + **policy di import opt-out** (categorie: TA/Runways/Sectors/**Sids**). Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia per callsign cross-ACC (Round 20).

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R34). **Indice doc:** `docs/index.md`. Ultimo round: **34** — vista operativa + QoL admin + import SID GitHub + gating import; modello in `docs/spec/modello-dati.md` §9.8 (migrazioni). (R33: anteprime `?as=`; R30: QoL Bozze & versioni §9.18; R29: versioning AIRAC + task §9.17.)

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 447 test (Domain 19 · App 210 · Infra 188 · Hosting 13 · Ui/bUnit 13 · E2E 4)
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
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

**Solution (Clean Architecture, net8.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

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
2. **Dati reali:** METAR/TAF ✅ (NOAA). Shape AoR ✅ (poligono IVAO). **SID ✅** (sectorfile Aurora GitHub, round 34, sez. config `Sectorfile`). Restano: **shape reali TWR dal sectorfile GitHub** (rimpiazza le sintetiche `IsShapeSynthetic`), **minime MVA** (`<icao>.mva` stesso repo — riusa il pattern SID: parser + import gated + pubblicazione differita), AoR 3D (Three.js).
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
