# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 23 giugno 2026
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato:** progetto **in sviluppo attivo**. Design UI completo (mockup v2, 17 schermate) **e** codice avanzato: solution .NET 8 a 4 layer + Host Blazor Server, **56 test verdi**, consultazione+editing+sicurezza funzionanti dal DB. **Live IVAO (F3) implementato**: polling + cache + SSE, Ridotta live (AoR reattivo, "primo online", online nel dominio), auto-elenco CH permessi.

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 49 test
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /sop
```
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`); cancellalo per ri-seedare da zero.
- In dev l'utente è `DevCurrentUserProvider` (VID 654321, staff `IT-AOC` → **admin**, può tutto).
- Migrazioni: `dotnet ef migrations add <Nome> --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations`.

## 3. Mappa documenti
| File | Contenuto | Stato |
|---|---|---|
| `README.md` | **Stato del codice** (architettura, capability, prossimi passi). | ⭐ fonte verità codice |
| `HANDOFF.md` | Questo file: contesto per riprendere. | ⭐ leggere per primo |
| `PIANO_vIPI_Tool.md` | Piano/architettura di design. | design ref (vedi banner) |
| `SPEC_Modello_Dati.md` | Schema dati di design. | design ref (entità aggiornate, vedi §5) |
| `SPEC_Logica_AoR.md` | Logica visibilità + scenari S1–S10. | design ref; **S1–S10 implementati+testati** |
| `REVIEW_Flusso_e_Gap.md` | Confronto flusso vs documenti. | storico |
| `docs/CONFIG.md` | **Riferimento configurazione** completo (Division/Ivao/Auth/secrets). | ⭐ config |
| `docs/adr/ADR-0001/0002` | Decisioni fondanti + integrazione/auth. | valide |
| `docs/adr/ADR-0003` | Trasporto live = **SSE** (F3). | valida |
| `docs/adr/ADR-0004` | Configurazione divisione + codici admin. | valida |
| `mockups/vipi-ui-mockup-v2.html` | Mockup canonico 17 schermate. | storico/riferimento UI |
| `Esempi documenti/*.docx` | Esempi reali (riferimento contenuto, non importati). | riferimento |

---

## 4. STATO CODICE — cosa è implementato (e dove)

**Solution (Clean Architecture, net8.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

**Cuore AoR/visibilità (✅ testato S1–S10):** `Application/Aor/AorService.cs` (ownership/stato settori, top-down, unificazioni), `Topology.cs`, `Infrastructure/Aor/TopologyBuilder.cs` (implementa la porta `ITopologyProvider`). Tabella di verità visibilità in `Application/Content/ContentService.cs`.

**Consultazione dal DB (✅):** pipeline `IContentRepository` → `IVipiViewService` → `SectionNode`/`BlockRenderer`. Rotte sotto `/sop`:
- `/{acc}/vipi` (Estesa ACC) · `/{acc}/ridotta` (proiezione tier Reduced + sezione Trasferimenti) · `/{acc}/aeroporto` (vIPI aeroporto LIRF) · `/{acc}/vloa` (LIRR↔DTTC).
- `/search` (ricerca full-text reale), `/changed` (cosa è cambiato nel ciclo AIRAC), `/{acc}/export` (Estesa → stampa/PDF browser).
- Stub dichiarati: METAR/TAF, SID, mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico).

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}` per qualunque documento), `VersioniPage` (`/sop/versioni`).
- Editor specializzati: `TopologiaPage` (simulatore live riusa `IAorService` + CRUD regole/gerarchia), `XferEditorPage` (trasferimenti), `VloaEditorPage` (redirect all'editor generico).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position derivati dal **codice divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes` (pattern completi). **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code` (prefisso staff + id API membri), `IcaoPrefixes` (filtro ATC online), `AdminRolePatterns`. Per IT→DE basta la sezione `Division` in appsettings. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-FIR** (`EditGrant`, VID→FIR): chi non è admin edita una FIR solo con grant; copre tutti i tipi (vIPI/aeroporto/vLOA/topologia/trasferimenti). Schermata `/sop/admin/permessi` (solo admin): aggiungi/revoca per VID manuale.
- **Lock** documento esclusivo (30 min sliding, acquisizione atomica via `ExecuteUpdateAsync`, release su publish/abbandono, **force admin**) → impedisce editing concorrente. `EditConflictException`.
- **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`) → conflitto gestito.
- **Validazione**: `UnificationRule` hard (sectorKey/callsign devono esistere), trasferimenti soft (catena non vuota/no duplicati).
- Verifiche **sempre server-side**. Security review fatta: **XSS in `AorBlock` corretto** (SVG hand-built ora HTML-encoded).

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; migrazioni `InitialCreate`, `Transfers`, `AuthLockConcurrency`. Seed: `RomaStructureSeed` (anagrafica/gerarchia/regole), `RomaContentSeed` (vIPI ACC), `RomaAirportSeed` (LIRF), `RomaVloaSeed` (vLOA + FIR/posizione DTTC), `RomaTransferSeed`.

**Modello dati — aggiunte rispetto a SPEC §3:** `Transfer` (+enum `TransferPhase`; catena handler = array JSON), `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅ F3):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, evento `Changed`, impl. `IOnlineAtcProvider`), `IvaoApiClient` (typed HttpClient → `/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, serve solo per i membri divisione: il tracker è pubblico), `AtcPollingHostedService` (`BackgroundService`, 60s), `IvaoOptions` (sezione `Ivao`), DI via `AddVipiIvao(config)`. Transport **SSE** `/sop/live/atc` (`Program.cs`) + `Vipi.Ui/wwwroot/vipi-live.js` (`EventSource`→JS interop). `VipiViewService` calcola AoR reale quando `live=true`. `RidottaPage` ora `InteractiveServer` (selettore P, badge, online-nel-dominio, refresh SSE). `TransferOnlineResolver` + `ITransferService.ListResolvedByFirAsync` (primo-online). `IDivisionMembersProvider` per dropdown CH in `AdminGrantsPage`. Decisione in **ADR-0003**.

**Note implementative / hardening F3:** SSE con `DisableBuffering()` (consegna immediata dietro proxy). `UseHttpsRedirection` solo in prod (in dev l'host è http → niente warning). `TransferOnlineResolver`: match esatto/segmento + sottostringa **solo per token ≥4 char** (evita falsi positivi su token corti). "Online nel mio dominio" ha empty-state esplicito ("copri tutti i settori"). Cache vuota prima del primo poll = `OnlineAtcSnapshot.Empty` (viste sicure).

---

## 5. PROSSIMI PASSI (ordinati per valore)

1. **✅ Polling IVAO (F3) — FATTO.** Rifiniture aperte:
   - **Identità "P"**: oggi selettore manuale in Ridotta (default prima radice). Va legato al **callsign connesso del CH loggato** (richiede che `ICurrentUserProvider` esponga il callsign).
   - **Mapping token-handler → callsign** trasferimenti: oggi euristica match-segmento (`WS2`↔`LIMM_WS2_CTR`). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare; il `rating` non è nel summary tracker.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali (placeholder dichiarati):** shape AoR (GeoJSON/WKT — ADR formato), METAR/TAF (API meteo), SID + minime MVA (parsing **sectorfile GitHub**), AoR 3D (Three.js).
3. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (scenari A/B, claim del sito `Ivao.It`) e OIDC (scenario C); mappare gli **staff code reali** (vedi §6 nodo aperto). Integrazione: montare la RCL nel sito host.
4. **Copertura/rifiniture:** seed altre FIR (LIMM/LIPP/LIBB), viewer **audit log**, "scarta bozza", editor visuale mappe AoR (oggi JSON grezzo), test property-based AoR, **rifinitura UI** (rimandata di proposito finché il live non gira).
5. **Housekeeping:** **niente è committato** (tutta la sessione è in working tree) — valutare commit logici: editor/topologia/trasferimenti · sicurezza/permessi · pagine-consultazione.

---

## 6. Nodi aperti / decisioni
**Risolte in questa sessione:** modello editing persistente; modello autorizzazione (admin via staff code + grant per-FIR); lock esclusivo 30 min + force admin; validazione hard regole/soft trasferimenti; export = stampa browser; "cosa è cambiato" = lista+note+conteggi; catena handler trasferimenti = array JSON.

**Risolte F3 (sessione 23 giu):** trasporto live = **SSE** (ADR-0003); polling cache singleton 60s; token solo per membri divisione (tracker pubblico).

**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`), da confermare col sito host. Il codice "CH" non è gate: i permessi passano **solo** dai grant per-FIR; l'auto-elenco CH popola il dropdown via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/CONFIG.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **FIR pilota:** Roma (LIRR). Validare su una sola FIR prima di estendere.
- **Brand:** palette §15.1 PIANO (blu `#0D2C99`…), font Nunito Sans + Poppins; tema in `Vipi.Ui/wwwroot/vipi-theme.css` (contiene anche le regole `@media print`).
- **Parte più rischiosa:** logica AoR/visibilità → già coperta da test S1–S10; mantenerla testata ad ogni modifica.
- **Pagine interattive** usano `@rendermode InteractiveServer` (editor, topologia, trasferimenti, ricerca, changed, admin permessi).
- **Sicurezza:** ogni nuova operazione di scrittura deve passare per i service Application (guardia authz + lock), mai bypassare dal repo/UI.

---

## 8. Mockup v2 — storico UI (sessioni 19–20 giu)
Il mockup `mockups/vipi-ui-mockup-v2.html` (17 schermate) resta il riferimento visivo. Le schermate sono state derivate in componenti Blazor reali (vedi §4). Note: SCCAM e Aree regolamentate sono sezioni top-level; la vLOA ha due AoR e due tabelle frequenze; gli APP non remotizzati separano i trasferimenti verso ACC e verso torre. L'interattività del mockup era simulata; ora i dati vengono dal DB (tranne gli stub di §5 punto 2 — live/meteo/sectorfile/3D).
