# vIPI / vLOA Interactive

Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) della divisione **IVAO Italia**.
Trasforma i Word statici in contenuto strutturato con due livelli (Estesa/Ridotta), logica di visibilità
live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

> Pianificazione completa: `PIANO_vIPI_Tool.md`, `HANDOFF.md`, `SPEC_*.md`, `docs/adr/`.

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
dotnet test  Vipi.slnx            # 19 test (AoR S1–S10, tabella visibilità, AIRAC)
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

## Stato (F0/F1 — scaffold)
✅ Solution 4 layer + Host + test · ✅ modello di dominio (SPEC §3–4, §7) · ✅ schema EF Core + prima migration
· ✅ logica AoR/visibilità con test S1–S9 · ✅ `AiracService` · ✅ tema brand (`Vipi.Ui/wwwroot/vipi-theme.css`)
· ✅ home `/sop` a 4 ACC.

### Prossimi passi (HANDOFF §5)
- Seed strutturale FIR Roma (anagrafica + gerarchia + regole di unificazione).
- Componenti Blazor dalle schermate mockup v2 collegati al dominio.
- Polling IVAO + cache (`AtcPollingHostedService`) + modalità live (SSE vs circuito Blazor — ADR futuro).
- Editor CH/AOD (workflow bozza→pubblicato, audit, diff).
