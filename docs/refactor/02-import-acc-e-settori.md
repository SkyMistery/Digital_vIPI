# 02 — Import ACC + settori di ACC (punti 1+2) 🟢✅

> ACC e i loro subcenter si importano in **una sola pipeline**. I due punti 1 e 2
> dell'utente sono lo stesso flusso. Dipende da: doc 01 (infra condivisa).
>
> **NB doc 01 ha già fatto i passi 2-4** (creato `AccImportUseCase`, hosted wrapper,
> admin che delega). Residuo doc 02: estrarre i record da `AccAdminService` (§4.1) e
> rifattorizzare le **aree speciali** in un use-case + entry manuale (§4.2-4.4).
> I riferimenti a `IvaoApiClient.cs` in sez.1 sono storici: dal refactor 01 le porte ACC
> sono implementate da `IvaoAccClient`.
>
> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/02-import-acc`, 199 test verdi).
> Record estratti (§4.1); `SpecialAreaImportUseCase`/`ISpecialAreaImportUseCase` separato
> (§4.2), hosted delega (§4.3), manual «Importa da sorgente» ora esegue anche le aree
> speciali → manual = auto stesso stato DB (§4.4). Vedi `../history/rounds.md` «Refactor 02».

## 1. Stato attuale

| Item | File:riga | Layer | Ruolo |
|---|---|---|---|
| Entry manuale | `Vipi.Ui/Pages/AccAdminPage.razor:198` | Web | `Acc.ImportFromSourceAsync()` |
| Use-case | `Vipi.Application/Content/AccAdminService.cs:57` | App | Admin-gated; `IAccDirectory.GetCentersAsync` → `IAccAdminRepository.ImportAsync`; poi loop subcenter `:65-71`. |
| Job auto | `Vipi.Infrastructure/Ivao/AccImportHostedService.cs:15` | Infra | `BackgroundService`; `ImportOnceAsync` `:35` — stesse chiamate senza authz, poi proiezione. |
| Porta ACC | `IAccDirectory.GetCentersAsync` → `IvaoApiClient.cs:158` | App/Infra | `/v2/centers` per country. |
| Porta subcenter | `IAccDirectory.GetSubcentersAsync` → `IvaoApiClient.cs:233` | App/Infra | `/v2/centers/{icao}/subcenters`. |
| Repo | `EfAccAdminRepository.ImportAsync` / `ImportSubcentersAsync` | Infra | Upsert preservando `IsHidden`. |
| Aree speciali (sibling) | `SpecialAreaImportHostedService.cs:14` → `GetSpecialAreasAsync` (`:286`) → `ImportSpecialAreasAsync` + prune | Infra | Solo auto, nessuna entry manuale. |

**Entità**: `Acc` (`Vipi.Domain/Entities/Anagrafica.cs:4`, con `IsForeign`, `IsHidden`,
`CountryPrefix`), `AccSector` (`:36`, con `ParentCallsign`, limiti quota, `IsHidden`).

## 2. Problemi

1. **Duplicazione manual-vs-auto**: `AccAdminService.ImportFromSourceAsync` (`:57`) e
   `AccImportHostedService.ImportOnceAsync` (`:35`) hanno corpi quasi identici. Solo
   il primo applica authz. → istanza concreta del problema generale del doc 01.
2. **`AccAdminService.cs` multi-classe**: record `AccAdminRow`, `AccSectorRow`,
   `AccImportResult` + interfaccia `IAccAdminService` + classe (`:7,14,19,26`).
3. **Aree speciali asimmetriche**: importate solo in auto, entry manuale assente;
   convivono nello stesso repo ma con pipeline separata.
4. **ACC + subcenter accoppiati** ma il codice li tratta come due chiamate separate
   ripetute in due punti (manual e auto) → 4 copie della logica di iterazione.

## 3. Architettura target

> ✅ APPROVATA — Fase 0, 2026-07-09.

- **`AccImportUseCase`** (già fatto in doc 01) resta ACC + subcenter con
  `SyncFromCatalogsAsync` finale. Manual = authz-guard + use-case; auto = hosted wrapper.
- **`SpecialAreaImportUseCase` separato** (+ `ISpecialAreaImportUseCase`) per le aree
  speciali — *decisione D1 doc 02*: NON assorbite in `AccImportUseCase`. Motivo: semantica
  propria (prune per-ACC `PruneSpecialAreasNotInAsync`, isolamento errori per-ACC,
  scheduling separato). Coerente con la scelta per-categoria di doc 01 (D1). Corpo:
  `foreach ACC → GetSpecialAreasAsync → ImportSpecialAreasAsync → Prune` (try/catch per-ACC).
  Nessuna `Sync` (le aree non producono `Sector` proiettati — comportamento attuale invariato).
- **`SpecialAreaImportHostedService`** → thin wrapper che delega al use-case.
- **Import manuale aree speciali** — *decisione D2 doc 02*: il bottone «Importa da sorgente»
  (`AccAdminService.ImportFromSourceAsync`) esegue anche `SpecialAreaImportUseCase` (gated
  authz) → manual e auto producono lo **stesso stato DB**. Nessuna UI nuova.
- **Estrarre `AccAdminRow`/`AccSectorRow`/`AccImportResult`/`IAccAdminService`** in file singoli.

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico prima, logica dopo.

**Meccanico (commit separato):**
1. Estrarre `AccAdminRow`, `AccSectorRow`, `AccImportResult`, `IAccAdminService` da
   `AccAdminService.cs` in file singoli (`Vipi.Application/Content/`).

**Con logica (1 commit per passo, build verde):**
2. Creare `SpecialAreaImportUseCase` (+ `ISpecialAreaImportUseCase`), spostarvi il corpo di
   `SpecialAreaImportHostedService.ImportOnceAsync` (loop ACC + import + prune per-ACC).
   Registrare in DI (`Vipi.Application/DependencyInjection.cs`).
3. `SpecialAreaImportHostedService` → wrapper che delega al use-case.
4. `AccAdminService.ImportFromSourceAsync` → dopo `AccImportUseCase`, invoca anche
   `SpecialAreaImportUseCase` (inietta `ISpecialAreaImportUseCase`); resta gated `EnsureAdmin`.

## 5. Impatto

- **Dipende da** doc 01. **A valle**: doc 05 (confinanti) riusa `GetCentersByCountryAsync`
  e `GetSubcentersAsync`; doc 06 (gerarchia) consuma `AccSector.ParentCallsign`.
- **Verifica** (Fase 3): import manuale e auto → stesso stato DB (ACC + subcenter + aree
  speciali); `IsHidden` preservato; prune aree speciali invariato; conteggio test = baseline (199).
