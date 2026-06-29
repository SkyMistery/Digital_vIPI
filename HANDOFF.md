# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 29 giugno 2026 (Round 20)
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 8 a 4 layer + Host Blazor Server, **128 test verdi**, consultazione+editing+sicurezza dal DB. **Live IVAO** (polling + cache + SSE, Ridotta live). **Sorgente dati disaccoppiata** (interfacce neutre + `DataSource:Provider`) + **policy di import opt-out**. Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia di copertura per callsign cross-ACC (Round 20).

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R20). **Indice doc:** `docs/index.md`. Ultimo round: 20 — fonte unica dei settori + gerarchia per callsign; piano in `docs/history/piano-round20.md`, modello in `docs/spec/modello-dati.md` §9.12.

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 128 test
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
- ⚠️ **AZIONE PENDENTE (Round 20):** **reset `src/Vipi.Host/vipi.db`** in dev (o applica la migrazione **`AddHierarchyParentCallsign`**, additiva) → riavvia il Host. Poi `/vsop/admin/acc` → «Importa da sorgente»: la **sync** popola automaticamente i `Sector` (CTR inclusi) dai cataloghi; in `/vsop/admin/sectorstructure` compare l'**albero di copertura globale** (cross-ACC). Il Host viene **fermato** a fine sessione (blocca le DLL in build).
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
- Editor specializzati: `AdminTrasferimentiPage` (trasferimenti, pagina admin globale `/vsop/admin/trasferimenti`: selettore ACC + flussi/punti nidificati, Next cross-ACC; ex per-ACC `XferEditorPage` rimosso), `VloaEditorPage` (redirect all'editor generico). Gerarchia di copertura in `StrutturaPage` (`/vsop/admin/sectorstructure`). ⚠️ **`TopologiaPage` rimossa** (`/vsop/{acc}/topologia`): gerarchia → `sectorstructure`; le regole di unificazione + simulatore AoR erano legacy e non hanno più UI (motore `IAorService` + `UnificationRule` + test S1–S10 **restano**).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position derivati dal **codice divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes`. **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code`, `IcaoPrefixes`, `AdminRolePatterns`. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant. Schermata `/vsop/admin/permessi` (solo admin).
- **Lock** documento esclusivo (30 min sliding, atomico via `ExecuteUpdateAsync`, **force admin**) → `EditConflictException`. **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`).
- **Validazione**: `UnificationRule` hard, trasferimenti soft. Verifiche **sempre server-side**. Security review: XSS in `AorBlock` corretto.

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; **lista migrazioni autoritativa = `docs/spec/modello-dati.md` §9.8** (fino a **`SimplifyTransferResolution`**). Seed (solo fixture di test, **non** seminato all'avvio): `RomaStructureSeed`, `RomaContentSeed`, `RomaAirportSeed`, `RomaVloaSeed`, `RomaTransferSeed`. ⚠️ **In produzione i `Sector` sono una proiezione dei cataloghi** (round 20): non si creano a mano, vedi `docs/spec/modello-dati.md` §9.12.

**Modello dati — aggiunte rispetto a `docs/spec/modello-dati.md` §3:** **`TransferFlow`** (settore mittente + tipo + aeroporto) → **`TransferPoint`** (CoP/livello strutturato/settore ricevente `NextSector`); risoluzione live **risale la gerarchia globale** (`ParentCallsign`/`ParentSectorId`), terminale **UNICOM** (no enum fallback). `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, `IOnlineAtcProvider`), `IvaoApiClient` (`/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, solo per i membri divisione: tracker pubblico), `AtcPollingHostedService` (60s), `IvaoOptions`. Transport **SSE** `/vsop/live/atc` + `vipi-live.js`. `VipiViewService` calcola AoR reale quando `live=true`; `RidottaPage` `InteractiveServer`. Decisione in **ADR-0003**.

**Indipendenza dalla sorgente (✅, ADR-0006):** porte dati esterne **neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`, DTO `Source*`); adapter IVAO selezionato da **`DataSource:Provider`**. `Vid`→`UserId` ovunque (a video resta "VID"). **Policy di import** (`ImportPolicy`, categorie `{TransitionAltitude, Runways, Sectors}`, pagina `/vsop/admin/sorgenti`): dati di sorgente in sola lettura, enforcement a difesa in profondità.

**Fonte unica settori (✅ Round 20):** cataloghi `AccSector`/`AirportSector` = fonte autoritativa; `Sector` = proiezione (`ISectorProjectionService.SyncFromCatalogsAsync`). Gerarchia per callsign (`ParentCallsign`, cross-ACC) editata in `/vsop/admin/sectorstructure` (`IHierarchyEditingService`). Dettagli: `docs/spec/modello-dati.md` §9.12.

---

## 5. PROSSIMI PASSI (ordinati per valore)

1. **Live IVAO — rifiniture aperte:**
   - **Identità "P"** legata al callsign connesso del CH loggato (oggi selettore manuale in Ridotta).
   - **Mapping token-handler → callsign** trasferimenti (oggi euristica match-segmento). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali:** METAR/TAF ✅ (NOAA). Restano: shape AoR (GeoJSON/WKT — ADR formato), SID + minime MVA (parsing **sectorfile GitHub**), AoR 3D (Three.js).
3. **Fonte unica (Round 20) — follow-up:** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`) = fase live. ✅ **Fatto per i trasferimenti:** `ITransferService.ResolveForAccAsync` + `ITopologyProvider.BuildGlobalAsync` risolvono mittente e ricevente risalendo la gerarchia globale (terminale UNICOM); Ridotta li mostra nidificati Settore ▸ Aeroporto ▸ Tipo. Resta da estendere la stessa risalita alla "presidenza aeroporto" generale.
4. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (A/B, claim `Ivao.It`) e OIDC (C); mappare gli **staff code reali** (§6). Montare la RCL nel sito host.
5. **Copertura/rifiniture:** viewer **audit log**, "scarta bozza", editor visuale mappe AoR, test property-based AoR, rifinitura UI.

---

## 6. Nodi aperti / decisioni
**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`), da confermare col sito host. Il codice "CH" non è gate: i permessi passano **solo** dai grant per-ACC; l'auto-elenco CH popola il dropdown via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

**Risolte (storico):** modello editing persistente; autorizzazione (admin via staff code + grant per-ACC); lock 30 min + force admin; validazione hard/soft; export = stampa browser; trasporto live = **SSE** (ADR-0003); polling cache singleton 60s.

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
