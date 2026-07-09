# 02 — Import ACC + settori di ACC (punti 1+2) 🟢🟡

> ACC e i loro subcenter si importano in **una sola pipeline**. I due punti 1 e 2
> dell'utente sono lo stesso flusso. Dipende da: doc 01 (infra condivisa).

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

> 🟡 BOZZA.

- Un solo **`AccImportUseCase`** (doc 01) che importa ACC + subcenter + aree speciali
  in un'unica operazione idempotente, con `SyncFromCatalogsAsync` finale.
- Manual = `AccImportUseCase` preceduto da authz-guard; auto = hosted wrapper.
- Estrarre `AccAdminRow`/`AccSectorRow`/`AccImportResult`/`IAccAdminService` in file singoli.
- Unificare le aree speciali nello stesso use-case (aggiungere entry manuale coerente).

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre i record da `AccAdminService.cs`.
2. Creare `AccImportUseCase`, spostarvi il corpo (da manual).
3. `AccImportHostedService` → wrapper sul use-case.
4. `AccAdminService.ImportFromSourceAsync` → authz + use-case.
5. Assorbire le aree speciali nel use-case + entry manuale.

## 5. Impatto

- **Dipende da** doc 01. **A valle**: doc 05 (confinanti) riusa `GetCentersByCountryAsync`
  e `GetSubcentersAsync`; doc 06 (gerarchia) consuma `AccSector.ParentCallsign`.
- **Verifica**: import manuale e auto → stesso stato DB; `IsHidden` preservato;
  subcenter e aree speciali presenti dopo un singolo run.
