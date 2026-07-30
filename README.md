# vIPI / vLOA Interactive

Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) della divisione **IVAO Italia**.
Trasforma i Word statici in contenuto strutturato con due livelli (Estesa/Ridotta), logica di visibilità
live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

## Documentazione
- **`HANDOFF.md`** — stato corrente e come riprendere il lavoro (leggere per primo).
- **`docs/index.md`** — indice di tutta la documentazione (specifiche, guide, ADR, storia).
- **`docs/history/rounds.md`** — changelog cronologico dei round.
- Specifiche: `docs/spec/modello-dati.md`, `docs/spec/logica-aor.md`, `docs/spec/mappa-pagine.md`.
- Config & integrazione: `docs/guide/config.md`, `docs/guide/integration.md`. Decisioni: `docs/adr/`.

## Architettura (Clean Architecture — ADR-0001 D2, ADR-0002)

| Progetto | Ruolo | Dipende da |
|---|---|---|
| `src/Vipi.Domain` | Entità, enum, regole pure (`AiracService`). Nessuna dipendenza. | — |
| `src/Vipi.Application` | Use case e porte: `IAorService`, `IContentService`, `ICurrentUserProvider`. Logica AoR pura. | Domain |
| `src/Vipi.Infrastructure` | EF Core + SQLite (`VipiDbContext`), `TopologyBuilder`, migrazioni. | Application, Domain |
| `src/Vipi.Ui` | **RCL Blazor** montabile in-process nel sito host. Stili confinati in `.vipi-root`. | Application, Domain |
| `src/Vipi.Hosting` | **Superficie del modulo**: `AddVipiModule`/`UseVipiModule`/`MapVipiModule`/`MigrateVipiDatabase`, identità host, middleware, SSE, health. | Ui, Infrastructure, Application, Domain |
| `src/Vipi.Host` | Host Blazor Server di **sviluppo/esempio** che aggancia il modulo. | tutti |
| `tests/*` | xUnit: AIRAC, scenari AoR S1–S10, editing, proiezione settori, import. | — |

Regola di dipendenza verso l'interno: `Host → Infrastructure → Application → Domain`. La RCL e la logica
**non dipendono da tipi specifici dell'host** (ADR-0002 D5): l'identità arriva solo da `ICurrentUserProvider`.
In sviluppo (`useDevIdentity:true`) è attivo `DevCurrentUserProvider` (admin `IT-AOC`). Integrazione in `docs/guide/integration.md`.

## Build & run

```bash
dotnet build Vipi.slnx
dotnet test  Vipi.slnx            # 128 test (AoR S1–S10, editing, proiezione settori + gerarchia per callsign, ...)
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```

Il DB SQLite viene creato/migrato all'avvio dell'host (`Data Source=vipi.db`, override via `ConnectionStrings:Vipi`).

### Migrazioni EF Core
```bash
dotnet ef migrations add <Nome> \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure \
  -o Persistence/Migrations
```
(usa `DesignTimeDbContextFactory`; a runtime la connection string la fornisce l'host)

## Stato in breve
Solution .NET 10 a 4 layer + Host Blazor Server, **663 test verdi**. Consultazione + editing + sicurezza dal DB;
live IVAO (polling + SSE); sorgente dati disaccoppiata; pagine su prefisso `/vsop`; **fonte unica = cataloghi**
(i `Sector` sono una proiezione, gerarchia di copertura per callsign cross-ACC, Round 20). Dettaglio completo
e prossimi passi in **`HANDOFF.md`**; storia in **`docs/history/rounds.md`**.
