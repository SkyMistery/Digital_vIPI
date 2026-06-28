# vIPI / vLOA Interactive

Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) della divisione **IVAO Italia**.
Trasforma i Word statici in contenuto strutturato con due livelli (Estesa/Ridotta), logica di visibilità
live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

> Pianificazione completa: `PIANO_vIPI_Tool.md`, `HANDOFF.md`, `SPEC_*.md`, `docs/adr/`.
> Configurazione runtime: **`docs/CONFIG.md`**. Integrazione in un sito esistente: **`docs/INTEGRATION.md`**.

> 🔀 **Round 5:** posizione e settore sono **un'unica entità `Sector`** (callsign + spazio aereo), con contenimento ad albero (`ParentSectorId`) e scope documenti uno-a-molti (`Sector.DocumentId`/`IsPrimary`). Vedi `SPEC_Modello_Dati.md` (banner Round 5) e `docs/sector-map.md`.

> 🛫 **Round 6:** **`Airport`** è entità di prima classe sotto una ACC (`Icao` univoco, `Name`, `AccId`). I settori d'aeroporto vi puntano via `Sector.AirportId` (`Sector.AirportIcao` resta denormalizzato); **l'aeroporto non ha gerarchia propria** — si ricostruisce dai settori che lo referenziano. Anagrafica reale dalla sorgente (`IAirportDirectory`, oggi adapter IVAO → `/v2/airports?countryId=IT`, `centerId`=ACC di competenza, cache 12h). Gestione interamente in **`AeroportiPage`** (`/sop/admin/aeroporti`, admin): assegna/sposta/rimuovi + ricerca + selezione multipla + **«Auto-assegna noti»** (`AutoAssignKnownAirportsAsync`: crea gli aeroporti il cui `centerId` è una ACC già presente). `StrutturaPage` tiene solo il picker aeroporto nel form settore (`Kind=Airport`).

> 🛬 **Round 7:** **«Genera documenti»** (`AeroportiPage`) crea dalla sorgente i settori **DEL/GND/TWR** (`/v2/airports/{ICAO}/ATCPositions`, APP rimandato; ATIS solo come frequenza) e la **vIPI aeroporto Published** con le sezioni del mockup (Quote di transizione · Frequenze · Piste da `/v2/airports/{ICAO}/runways` · SID). METAR/TAF restano **live** sulla pagina. Idempotente.

> 🗼 **Round 10:** torre informativa **`SectorType.ITwr`** (AFIS, trattata come torre per frequenza/etichetta); invariante **«ogni aeroporto ha almeno una torre»** (badge ⚠ no TWR + blocco eliminazione unica torre). **Quote di transizione di default** `TL = TA + margine` per fascia QNH (`<977→+2500`…`≥1013→+1000`, arrotondate al FL), garantite a ogni rebuild.

> 🔌 **Round 11 — Indipendenza dalla sorgente + policy di import.** Le porte dati esterne sono **interfacce neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`, DTO `Source*`); l'adapter IVAO è UNA implementazione scelta via **`DataSource:Provider`**. Tutto ciò che la sorgente fornisce è **importato e in sola lettura** (policy globale **opt-out**, entità `ImportPolicy`, pagina admin **`/sop/admin/sorgenti`**): TA e piste non sono più editabili dagli utenti se importate. `Vid`→`UserId` nel codice e nel DB (migrazione `Rename_Vid_To_UserId`; a video resta "VID"). Vedi `SPEC_Modello_Dati.md` e `HANDOFF.md` (Round 10/11).

## Architettura (Clean Architecture — ADR-0001 D2, ADR-0002)

| Progetto | Ruolo | Dipende da |
|---|---|---|
| `src/Vipi.Domain` | Entità, enum, regole pure (`AiracService`). Nessuna dipendenza. | — |
| `src/Vipi.Application` | Use case e porte: `IAorService`, `IContentService`, `ICurrentUserProvider`. Logica AoR pura. | Domain |
| `src/Vipi.Infrastructure` | EF Core + SQLite (`VipiDbContext`), `TopologyBuilder`, migrazioni. | Application, Domain |
| `src/Vipi.Ui` | **RCL Blazor** montabile in-process nel sito host (rotta `/sop`). Stili confinati in `.vipi-root`. | Application, Domain |
| `src/Vipi.Hosting` | **Superficie del modulo**: `AddVipiModule`/`UseVipiModule`/`MapVipiModule`/`MigrateVipiDatabase`, identità host (`HostIdentityCurrentUserProvider`), middleware, SSE, health. | Ui, Infrastructure, Application, Domain |
| `src/Vipi.Host` | Host Blazor Server di **sviluppo/esempio** che aggancia il modulo. | tutti |
| `tests/Vipi.Domain.Tests` · `tests/Vipi.Application.Tests` | xUnit: AIRAC + scenari AoR S1–S10. | — |

Regola di dipendenza verso l'interno: `Host → Infrastructure → Application → Domain`. La RCL e la logica
**non dipendono da tipi specifici dell'host** (ADR-0002 D5): l'identità arriva solo da `ICurrentUserProvider`.

### Portabilità identità (ADR-0002 D3, ADR-0005)
- **A** sito attuale `Ivao.It` · **B** sito nuovo (stesso stack) → `HostIdentityCurrentUserProvider`
  legge il `ClaimsPrincipal` dell'host (mappa claim config-driven, sezione `HostIdentity`). **Implementato.**
- **C** app autonoma → adapter IVAO OIDC proprio passato a `AddVipiModule`.

In sviluppo (`useDevIdentity:true`) è attivo `DevCurrentUserProvider` (utente CH fittizio, `CanEdit = true`).
Integrazione passo-passo in **`docs/INTEGRATION.md`**.

## Build & run

```bash
dotnet build Vipi.slnx
dotnet test  Vipi.slnx            # 106 test (AoR S1–S10, editing, lock/authz/concorrenza, ricerca, changed, AIRAC, polling IVAO, primo-online, profilo aeroporto, policy import)
dotnet run --project src/Vipi.Host   # poi apri /sop
```

Il DB SQLite viene creato/migrato all'avvio dell'host (`Data Source=vipi.db`, override via
`ConnectionStrings:Vipi`).

### Migrazioni EF Core
```bash
dotnet ef migrations add <Nome> \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure \
  -o Persistence/Migrations
```
(usa `DesignTimeDbContextFactory`; a runtime la connection string la fornisce l'host)

## Stato
✅ Solution 4 layer + Host + test · ✅ modello di dominio (SPEC §3–4, §7) · ✅ schema EF Core + prima migration
· ✅ logica AoR/visibilità con test **S1–S10** · ✅ `AiracService` · ✅ tema brand (`Vipi.Ui/wwwroot/vipi-theme.css`)
· ✅ home `/sop` a 4 ACC · ✅ seed Roma **ACC + aeroporto LIRF**.

**Consultazione dal DB:** vIPI ACC Estesa (`/sop/{acc}/vipi`), **Ridotta** proiezione tier Reduced (`/sop/{acc}/ridotta`),
**vIPI Aeroporto** (`/sop/{acc}/aeroporto`), **vLOA** (`/sop/{acc}/vloa`) — tutte tramite `IVipiViewService` + `BlockRenderer`.
**Ricerca full-text** (`/sop/search`, `ISearchService`), **Cosa è cambiato** (`/sop/changed`, `IChangesService`, per ciclo AIRAC),
**Export** (`/sop/{acc}/export`, Estesa → stampa/PDF browser via `@media print`).

**Editing persistente (CH/AOD):** porta `IEditingRepository` + `EditingService` (autorizzazione ACC-scoped, vedi sotto), workflow
**bozza→pubblicato** con clonazione versione + audit; UI `EditorPage` (`/sop/{acc}/editor`, anche `?doc={id}` per
qualunque documento) con CRUD blocchi **e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli) e `VersioniPage`
(`/sop/versioni`), entrambe `InteractiveServer`.

**vLOA:** seed bilaterale LIRR↔DTTC (`DocumentParty` Home/Neighbour); consultazione `/sop/{acc}/vloa` dal DB,
editing con l'editor generico (`VloaEditorPage` reindirizza).

**Topologia (`/sop/{acc}/topologia`):** **simulatore** che riusa `ITopologyProvider`+`IAorService` (ownership/stato
reali, preset S1–S6) + **CRUD** regole di unificazione e relazioni gerarchiche (`ITopologyEditingService`).

**Trasferimenti:** entità `Transfer` (riga strutturata: relazione·fase·CoP·FL·catena handler JSON·fallback),
editor `XferEditorPage`, e sezione **Trasferimenti** nella Ridotta con risoluzione **"primo online"**
(`TransferOnlineResolver` + `ListResolvedByAccAsync`, F3).

**Live IVAO (F3):** `AtcPollingHostedService` interroga l'API IVAO (`/v2/tracker/now/atc/summary`) ogni 60 s,
filtra i **prefissi ICAO della divisione** (`Division:IcaoPrefixes`), aggiorna `OnlineAtcCache` (singleton) letta via porta `IOnlineAtcProvider`.
La Ridotta gira `live=true`: AoR reale (selettore "la mia posizione" P, collasso AoR), lista "online nel mio
dominio", "primo online" dei trasferimenti, badge Live. Push al browser via **SSE** (`/sop/live/atc` + `vipi-live.js`,
**ADR-0003**). Token `client_credentials` (`IvaoTokenProvider`) solo per l'elenco membri divisione; il tracker è pubblico.
Config in sezione `Ivao` di `appsettings.json`; segreti in user-secrets (`Ivao:ClientId/ClientSecret`).
La sorgente attiva si sceglie con **`DataSource:Provider`** (oggi `"Ivao"`): l'app dipende solo dalle interfacce neutre (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`), così cambiare network o usare un DB interno richiede solo un nuovo adapter.

**Sicurezza & permessi:** autorizzazione ACC-scoped (`IEditAuthorizationService`): **admin** = staff `{DIV}-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>` derivati dal **codice divisione** (`Division:Code`, default `IT`); override esplicito opzionale in `Auth:AdminStaffCodes`
(editano tutto + gestiscono i permessi); gli altri editano una ACC solo con un `EditGrant` (VID→ACC), concesso da
`/sop/admin/permessi`. **Lock** esclusivo del documento (30 min sliding, force admin) impedisce editing concorrente.
**Concorrenza ottimistica** (`RowVersion` su blocchi/sezioni) + **validazione** (regole hard, trasferimenti soft).
Verifica sempre server-side. In dev `DevCurrentUserProvider` è admin (`IT-AOC`).

### Cambiare divisione (es. IT → DE)
Sezione `Division` di `appsettings.json` (o env var):
```json
"Division": { "Code": "DE", "Name": "Germania", "IcaoPrefixes": [ "ED", "ET" ] }
```
`Code` sposta i codici staff admin (`DE-DIR`…) e l'id nell'API membri; `IcaoPrefixes` filtra gli ATC online.
Centralizzato in `DivisionOptions`. **Nota:** il contenuto seed (Roma/LIRR) è dato, non config — va riseedato a parte.

### Prossimi passi
- ✅ **Polling IVAO + live (F3)** — fatto: cache+SSE, Ridotta live, primo-online, auto-elenco CH. Da rifinire:
  conferma endpoint membri divisione, mapping esplicito token-handler trasferimenti, estensione live a vIPI
  aeroporto/vLOA (oggi solo ACC Ridotta), identità "P" dal callsign connesso del CH loggato.
- Auth di produzione: adapter reali `ICurrentUserProvider` (HostIdentity A/B, OIDC C).
- Placeholder dati reali (§5.1 HANDOFF): shape AoR (GeoJSON), METAR/TAF, SID/MVA da sectorfile GitHub, AoR 3D.
