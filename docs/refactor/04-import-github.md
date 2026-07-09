# 04 — Import SID da GitHub (punto 11) 🟢🟡

> Import delle SID dal sectorfile Aurora IT (`ivao-italy/it-aurora-sector`, raw GitHub).
> Sorgente propria, indipendente dalle altre. Dipende da: doc 01 (policy/loop).

## 1. Stato attuale

| Item | File:riga | Layer | Ruolo |
|---|---|---|---|
| Entry manuale | `Vipi.Ui/Pages/AeroportoEditorPage.razor` (bottone import SID) | Web | via `ISidImporter`/editor. |
| Use-case | `Vipi.Application/Content/SidImporter.cs:16` | App | Check `IImportPolicyStore` (`ImportCategory.Sids`) → `ISidProvider.GetSidsAsync` → `IAirportProfileRepository.ReplaceImportedSidsAsync` (preserva manuali/priorità). |
| Adapter GitHub | `Vipi.Infrastructure/Sectorfile/AuroraSidProvider.cs:11` | Infra | Scarica `itfix.fix`/`itvor.vor` + `<icao>.sid` da raw GitHub (`SectorfileOptions.RawBaseUrl`, pubblico, no auth); cache navaid per processo; delega il parsing. |
| Parser | `Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:12` (static) | Infra | `ParseSids` / `ParseNavaids`. |
| Config | `Vipi.Infrastructure/Sectorfile/SectorfileOptions.cs:4` | Infra | `RawBaseUrl`, `FixPath`, `VorPath`, `ImportHours`. Base URL vuoto = import disattivato. |
| Job auto | `Vipi.Infrastructure/Sectorfile/SidImportHostedService.cs:15` | Infra | `BackgroundService`; loop ICAO → `ISidImporter.ImportAsync`. |
| DI | `Vipi.Infrastructure/DependencyInjection.cs:58-64` | Infra | Options + `AuroraSidProvider` via `AddHttpClient` + hosted service. |

**Nota merge**: `ReplaceImportedSidsAsync` preserva le SID manuali e la priorità per
punto (`StableKey`); la pubblicazione è differita al ciclo AIRAC N+1 (round 34).

## 2. Problemi

1. **Duplicazione manual-vs-auto** (più lieve): il manual e `SidImportHostedService`
   chiamano entrambi `ISidImporter.ImportAsync` — qui la centralizzazione è già buona,
   ma il loop ICAO è duplicato tra hosted service e chiamata manuale per-aeroporto.
2. **`SidImporter.cs` multi-classe**: interfaccia + classe.
3. **Accoppiamento con `AirportProfile`**: `SidImporter` scrive direttamente nel
   repository del profilo aeroporto (`ReplaceImportedSidsAsync`) → l'import GitHub
   conosce il modello documento (dolore trasversale con doc 08).
4. **Provider = adapter unico** che fa fetch + orchestrazione navaid + parsing;
   il parsing è già isolato (`AuroraSectorfileParser`), ma il caching navaid è dentro
   il provider.

## 3. Architettura target

> 🟡 BOZZA.

- Mantenere la buona separazione porta/adapter/parser esistente (è il pipeline più pulito).
- Estrarre l'interfaccia da `SidImporter.cs`.
- Valutare se il merge SID debba passare per un service di dominio "profilo aeroporto"
  invece che scrivere il repo direttamente (coordinare con doc 08).
- Uniformare il loop ICAO con il pattern `GatedImportLoop` del doc 01.

## 4. Passi di migrazione

> 🟡 BOZZA.

1. Estrarre `ISidImporter` in file dedicato.
2. Allineare `SidImportHostedService` al pattern loop condiviso (doc 01).
3. (Dopo doc 08) rivedere il punto di scrittura del merge SID.

## 5. Impatto

- **Dipende da** doc 01. **Accoppiato con** doc 08 (`AirportProfile`).
- **Verifica**: import SID manuale e auto → stesse SID importate; SID manuali e
  priorità preservate; base URL vuoto disattiva l'import senza errori.
