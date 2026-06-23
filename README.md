# vIPI / vLOA Interactive

Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) della divisione **IVAO Italia**.
Trasforma i Word statici in contenuto strutturato con due livelli (Estesa/Ridotta), logica di visibilità
live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

> Pianificazione completa: `PIANO_vIPI_Tool.md`, `HANDOFF.md`, `SPEC_*.md`, `docs/adr/`.
> Configurazione runtime: **`docs/CONFIG.md`**.

## Architettura (Clean Architecture — ADR-0001 D2, ADR-0002)

| Progetto | Ruolo | Dipende da |
|---|---|---|
| `src/Vipi.Domain` | Entità, enum, regole pure (`AiracService`). Nessuna dipendenza. | — |
| `src/Vipi.Application` | Use case e porte: `IAorService`, `IContentService`, `ICurrentUserProvider`. Logica AoR pura. | Domain |
| `src/Vipi.Infrastructure` | EF Core + SQLite (`VipiDbContext`), `TopologyBuilder`, migrazioni. | Application, Domain |
| `src/Vipi.Ui` | **RCL Blazor** montabile in-process nel sito host (rotta `/sop`). | Application, Domain |
| `src/Vipi.Host` | Host Blazor Server di **sviluppo** (scenario C minimo). | tutti |
| `tests/Vipi.Domain.Tests` · `tests/Vipi.Application.Tests` | xUnit: AIRAC + scenari AoR S1–S10. | — |

Regola di dipendenza verso l'interno: `Host → Infrastructure → Application → Domain`. La RCL e la logica
**non dipendono da tipi specifici dell'host** (ADR-0002 D5): l'identità arriva solo da `ICurrentUserProvider`.

### Portabilità identità (ADR-0002 D3)
- **A** sito attuale `Ivao.It` · **B** sito nuovo (stesso stack) → adapter che legge il `ClaimsPrincipal`.
- **C** app autonoma → adapter IVAO OIDC proprio.

In sviluppo è attivo `DevCurrentUserProvider` (utente CH fittizio, `CanEdit = true`).

## Build & run

```bash
dotnet build Vipi.slnx
dotnet test  Vipi.slnx            # 56 test (AoR S1–S10, editing, lock/authz/concorrenza, ricerca, changed, AIRAC, polling IVAO, primo-online)
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

**Editing persistente (CH/AOD):** porta `IEditingRepository` + `EditingService` (autorizzazione FIR-scoped, vedi sotto), workflow
**bozza→pubblicato** con clonazione versione + audit; UI `EditorPage` (`/sop/{acc}/editor`, anche `?doc={id}` per
qualunque documento) con CRUD blocchi **e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli) e `VersioniPage`
(`/sop/versioni`), entrambe `InteractiveServer`.

**vLOA:** seed bilaterale LIRR↔DTTC (`DocumentParty` Home/Neighbour); consultazione `/sop/{acc}/vloa` dal DB,
editing con l'editor generico (`VloaEditorPage` reindirizza).

**Topologia (`/sop/{acc}/topologia`):** **simulatore** che riusa `ITopologyProvider`+`IAorService` (ownership/stato
reali, preset S1–S6) + **CRUD** regole di unificazione e relazioni gerarchiche (`ITopologyEditingService`).

**Trasferimenti:** entità `Transfer` (riga strutturata: relazione·fase·CoP·FL·catena handler JSON·fallback),
editor `XferEditorPage`, e sezione **Trasferimenti** nella Ridotta con risoluzione **"primo online"**
(`TransferOnlineResolver` + `ListResolvedByFirAsync`, F3).

**Live IVAO (F3):** `AtcPollingHostedService` interroga l'API IVAO (`/v2/tracker/now/atc/summary`) ogni 60 s,
filtra i **prefissi ICAO della divisione** (`Division:IcaoPrefixes`), aggiorna `OnlineAtcCache` (singleton) letta via porta `IOnlineAtcProvider`.
La Ridotta gira `live=true`: AoR reale (selettore "la mia posizione" P, collasso AoR), lista "online nel mio
dominio", "primo online" dei trasferimenti, badge Live. Push al browser via **SSE** (`/sop/live/atc` + `vipi-live.js`,
**ADR-0003**). Token `client_credentials` (`IvaoTokenProvider`) solo per l'elenco membri divisione; il tracker è pubblico.
Config in sezione `Ivao` di `appsettings.json`; segreti in user-secrets (`Ivao:ClientId/ClientSecret`).

**Sicurezza & permessi:** autorizzazione FIR-scoped (`IEditAuthorizationService`): **admin** = staff `{DIV}-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>` derivati dal **codice divisione** (`Division:Code`, default `IT`); override esplicito opzionale in `Auth:AdminStaffCodes`
(editano tutto + gestiscono i permessi); gli altri editano una FIR solo con un `EditGrant` (VID→FIR), concesso da
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
