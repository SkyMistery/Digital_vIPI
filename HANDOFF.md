# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 30 giugno 2026 (Round 22 — shape tonda TWR + coord aeroporto + rifiniture trasferimenti/AOR)
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 8 a 4 layer + Host Blazor Server, **157 test verdi**, consultazione+editing+sicurezza dal DB. **Editor+viewer dedicati per gli APP non remotizzati** (round 21, vedi `docs/design/piano-editor-appn.md` + modello §9.13). **Shape tonda 5 NM di fallback per le TWR senza poligono** + overlay torre sulla mappa AOR (round 22, modello §9.14). **Live IVAO** (polling + cache + SSE, Ridotta live). **Sorgente dati disaccoppiata** (interfacce neutre + `DataSource:Provider`) + **policy di import opt-out**. Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia di copertura per callsign cross-ACC (Round 20).

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R22). **Indice doc:** `docs/index.md`. Ultimo round: **22** — shape tonda TWR + coord aeroporto + rifiniture trasferimenti/AOR; handoff di sessione in `docs/history/handoff-round22.md`, modello in `docs/spec/modello-dati.md` §9.14. (Round 21: editor APP non remotizzati, §9.13.)

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 157 test
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
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
- Stub dichiarati: SID, mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico). METAR/TAF = reale (NOAA).

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}`), `VersioniPage`.
- Editor specializzati: `AdminTrasferimentiPage` (trasferimenti, pagina admin globale `/vsop/admin/trasferimenti`: selettore ACC + flussi/punti nidificati, Next cross-ACC; ex per-ACC `XferEditorPage` rimosso) — **round 22:** flussi e punti **editabili in-place** (bottone ✎, oltre a ✕) via `ITransferService.UpdateFlowAsync`/`UpdatePointAsync`. `VloaEditorPage` (redirect all'editor generico). Gerarchia di copertura in `StrutturaPage` (`/vsop/admin/sectorstructure`).
- **Editor APP non remotizzati (✅ round 21):** `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`) WYSIWYG con 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + custom, riordino drag-and-drop+tasti, nascondi sezioni; viewer `AppnPage` data-driven. Entità `AppProfile`/`AppFrequencyLink` (modello §9.13), service `IAppProfileService` (freq/coord/AOR **derivate live**), `AorPolygonProjector`, registry `AppSections`, componenti `Vipi.Ui/Components/App/*`, mappa AOR Leaflet (`vipi-aor.js`). Instradamento via `DocumentSummary.IsStandaloneApp`. **Round 22:** «Trasferimenti verso ACC» suddiviso in sottosezioni **Partenze/Arrivi** (`AppCoordinationView`, split per `Kind`); **AOR** mostra anche le **shape delle TWR** dello stesso aeroporto come overlay Leaflet con toggle «Shape torre» (`GetTowerPolygonsAsync`). ⚠️ **`TopologiaPage` rimossa** (`/vsop/{acc}/topologia`): gerarchia → `sectorstructure`; le regole di unificazione + simulatore AoR erano legacy e non hanno più UI (motore `IAorService` + `UnificationRule` + test S1–S10 **restano**).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position derivati dal **codice divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes`. **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code`, `IcaoPrefixes`, `AdminRolePatterns`. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant. Schermata `/vsop/admin/permessi` (solo admin).
- **Lock** documento esclusivo (30 min sliding, atomico via `ExecuteUpdateAsync`, **force admin**) → `EditConflictException`. **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`).
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
2. **Dati reali:** METAR/TAF ✅ (NOAA). Shape AoR ✅ (poligono IVAO `[lng,lat]` su mappa Leaflet, APP + overlay TWR; le TWR senza poligono hanno il **cerchio 5 NM** sintetico, round 22). Restano: **shape reali TWR dal sectorfile GitHub** (provider `DataSource:Provider`, rimpiazza le sintetiche `IsShapeSynthetic`), SID + minime MVA (parsing sectorfile GitHub), AoR 3D (Three.js).
3. **Fonte unica (Round 20) — follow-up:** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`) = fase live. ✅ **Fatto per i trasferimenti:** `ITransferService.ResolveForAccAsync` + `ITopologyProvider.BuildGlobalAsync` risolvono mittente e ricevente risalendo la gerarchia globale (terminale UNICOM); Ridotta li mostra nidificati Settore ▸ Aeroporto ▸ Tipo. Resta da estendere la stessa risalita alla "presidenza aeroporto" generale.
4. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (A/B, claim `Ivao.It`) e OIDC (C); mappare gli **staff code reali** (§6). Montare la RCL nel sito host.
5. **Copertura/rifiniture:** viewer **audit log**, "scarta bozza", editor visuale mappe AoR, test property-based AoR, rifinitura UI.

---

## 6. Nodi aperti / decisioni
**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`), da confermare col sito host. Il codice "CH" non è gate: i permessi passano **solo** dai grant per-ACC; l'auto-elenco CH popola il dropdown via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
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
