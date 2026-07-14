# Audit correttezza + fonti-dati multiple (14 lug 2026) ⚪

Revisione senior dell'intero codice (~120k LOC, 6 progetti) a caccia di bug, errori e **fonti di dati
multiple** che possano generare incoerenze. Metodo: esplorazione a ventaglio (data-source / correttezza /
integrazione live) seguita da **verifica manuale** di ogni segnalazione (molti falsi positivi scartati).
Il tema reale si concentra su un punto: dato di **sorgente** e dato **editabile** che condividono lo stesso campo.

Stato: ✅ fix applicate, 148 test Infrastructure verdi (+2 nuovi), 156 Application, 19 Domain, build verde,
**boot live verificato** (DI risolve, `/vsop` → HTTP 200). Verifica live end-to-end dei singoli flussi applicativi
(edit frequenza su proiettato, import confinanti admin): da guidare con app avviata + token IVAO.

## A — Findings confermati e risolti

| # | Sev | Problema | Fix | File chiave |
|---|-----|----------|-----|-------------|
| A1 | ALTA | `Sector.DefaultFrequency` a doppia natura: editabile a mano **e** riscritto dalla proiezione → edit persi in silenzio | `SetSectorFrequencyAsync` rifiuta i settori `IsProjected` con `ValidationException` (catalogo = fonte unica) | `EfStructureEditingRepository.cs`, `IStructureEditingRepository.cs`, `StructureEditingService.cs` |
| A2 | MEDIA | Cache confinanti (`static`, TTL 5 min) invalidata solo da `SetParentAsync` → stantia dopo import/hide | Invalidazione spostata nel choke point `SyncFromCatalogsAsync`; `InvalidateConfiningCache` reso `internal static` | `EfSectorProjectionService.cs`, `EfHierarchyEditingService.cs` |
| A3 | MEDIA | Settore proiettato orfano lasciava `DocumentId`/`IsPrimary`/`FeaturedRank` dangling | Il passo orfani della sync azzera anche i legami editoriali oltre a `IsActive=false` | `EfSectorProjectionService.cs` |
| A4 | MEDIA | Import confinanti non atomico: persist estero senza riproiezione = ACC senza settori | Nuova porta `IUnitOfWork`/`EfUnitOfWork`: persist + riproiezione in una transazione | `IUnitOfWork.cs`, `EfUnitOfWork.cs`, `NeighbourImportService.cs`, `DependencyInjection.cs` |

### A1 — dettaglio (root cause del tema "fonti multiple")
`DefaultFrequency` è scritto dall'edit manuale **e** sovrascritto incondizionatamente da
`SyncFromCatalogsAsync` (`sector.DefaultFrequency = d.Frequency`) per ogni settore proiettato. Ogni import/hide/sync
riallineava il valore → l'edit spariva senza errore né audit. A valle, `EfAccDerivationRepository.DeriveFrequenciesForMembersAsync`
deriva la stessa frequenza da **due store** (`Sector.DefaultFrequency` e `AirportSectors.Frequency`): il dedup preferisce
la riga catalogo, ignorando comunque l'override. Fix scelto (owner, 14 lug): **sola lettura** sui proiettati — allineato
alla policy import opt-out (ADR-0006) e all'architettura "catalogo = fonte unica" (§9.12). Nessun chiamante UI usa il
metodo, quindi nessuna modifica UI necessaria.

## B — Findings minori risolti
- **B1** `TransientRetryHandler`: backoff lineare → **esponenziale (250ms·2^n) + jitter ±50%** (evita retry sincronizzati su 429/5xx).
- **B2** `IvaoTokenProvider`: margine rinnovo token 60s → **120s** (clamp a metà durata), assorbe lo skew d'orologio VM/NTP (prima rischio 401 a cascata).
- **B3** `AuroraSidProvider`: aggiunto `ILogger`, **logga l'esito import SID** per aeroporto (warn se file presente ma 0 SID estratti) — prima la degradazione da formato cambiato era invisibile (parser puro scarta righe in silenzio).
- **B4** `DevCurrentUserProvider`: `catch { }` nudo → **logga l'eccezione** invece di ingoiarla (nascondeva anche errori di programmazione). Solo path DEV.
- **B5** `IvaoOptions.DivisionMembersPathFormat`: commento **footgun** esplicito — endpoint non accessibile col token app (404/500); il roster staffisti si costruisce dai LOGIN, non da qui.

## C — Falsi positivi (segnalati dagli esploratori, verificati e SCARTATI)
Documentati così non li si reindaga:
- **Lock `EfResourceLockRepository`**: acquisizione **atomica** via `ExecuteUpdateAsync` (riesce solo se scaduto o già mio); l'insert è check-then-act ma protetto da **indice UNIQUE** su `ResourceKey` (`VipiDbContext.cs`), la `DbUpdateException` è gestita → cade su `InspectAsync`. `<=` su scaduto = libero è corretto.
- **`MetarParser` `t[(t.IndexOf('/')+1)..]`**: la regex `TempRe` (`^M?\d{2}/M?\d{2}$`) garantisce lo `/` → `IndexOf` mai -1.
- **`MetarParser` `int.Parse(t[4..])`**: guardato da `t.Length == 6` → sempre 2 char, mai vuoto.
- **`IvaoAccClient` `callsign.Split('_')[0]`**: `[0]` sempre presente.

## D — Da valutare in futuro (non toccato)
- `StationResolver` / `EfStructureEditingRepository` `.First()` su group teoricamente non vuoto: guardia difensiva opzionale se l'input sorgente fosse incompleto.
- SSE multi-client (`VipiModuleExtensions`): `SemaphoreSlim` clampa i release → possibili update intermedi persi. Accettabile per ADR-0003 (ping-to-refresh).

## Test aggiunti
- `StructureEditingTests.SetSectorFrequency_Rejects_Projected_Sector` (A1).
- `SectorProjectionTests.Sync_Clears_Editorial_Links_When_Projected_Sector_Becomes_Orphan` (A3).

## Propagazione
`spec/modello-dati.md` §9.12 (invarianti A1–A4), `history/rounds.md` (blocco round 14 lug), memoria di sessione
`sector-single-source-projection`. Nessuna migrazione DB (le fix usano campi esistenti + transazioni).
