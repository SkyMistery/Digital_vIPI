# 04 — Import SID da GitHub (punto 11) 🟢✅

> Import delle SID dal sectorfile Aurora IT (`ivao-italy/it-aurora-sector`, raw GitHub).
> Sorgente propria, indipendente dalle altre. Dipende da: doc 01 (policy/loop).
>
> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/04-import-sid`, 199 test).
> Estratta `ISidImporter` (§4.1). §4.2 (loop `GatedImportLoop`) era già a posto; logging
> auto+manuale già conforme (invariante #7). **Parte rimandata a doc 08**: punto di scrittura
> del merge SID (accoppiamento `AirportProfile`). Vedi `../history/rounds.md` «Refactor 04».

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

> ✅ APPROVATA — Fase 0, 2026-07-09. Verifica sez.1+2 vs codice: pipeline già pulito;
> `SidImportHostedService` **usa già** `GatedImportLoop` (§4.2 già fatto); logging OK
> (auto: debug per-ICAO + summary; manuale via `Guarded` nell'editor) → invariante #7 ok.

- Mantenere la buona separazione porta/adapter/parser esistente (è il pipeline più pulito).
- **Estrarre l'interfaccia da `SidImporter.cs`** (§4.1) — unico residuo meccanico.
- **Rimandato a doc 08**: valutare se il merge SID debba passare per un service di dominio
  «profilo aeroporto» invece di scrivere il repo direttamente (`ReplaceImportedSidsAsync`).
  È l'accoppiamento con `AirportProfile`, decisione del modello documento (doc 08).
- Loop ICAO: **già** allineato a `GatedImportLoop` — nulla da fare.
- Navaid cache nel provider: lasciata (minore, non giustifica churn).

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09.

**Meccanico:**
1. Estrarre `ISidImporter` da `SidImporter.cs` in file dedicato.

*(§4.2 loop condiviso: già fatto. §4.3 punto di scrittura merge SID: rimandato a doc 08.)*

## 5. Impatto

- **Dipende da** doc 01. **Accoppiato con** doc 08 (`AirportProfile`) — parte rimandata.
- **Verifica** (Fase 3): import SID manuale e auto → stesse SID importate; SID manuali e
  priorità preservate; base URL vuoto disattiva l'import senza errori; logging preservato;
  conteggio test = baseline (199).

## 6. Audit correttezza (2026-07-24)

Rifiniture sul re-import (idempotenza) e sul parsing, oltre al refactor §4. Dettaglio in memoria `sid-import-mechanism`.

- **Gate AIRAC = data di PRIMO prelievo.** `ReplaceImportedSidsAsync` conserva `SourceAiracCycle`
  originale se il contenuto è invariato (`ContentUnchanged` = Name+Transition+Type per `StableKey`
  coincidente); solo una revisione reale riparte dal ciclo corrente. Prima ri-timbrava il ciclo a
  ogni run 24h → `IsPublicAt` mai vero → SID importate sempre nascoste.
- **Fix manuale conservato tra reimport.** Se la sorgente ripropone il prefisso grezzo
  (`NeedsFixReview`) ma quel fix era già stato risolto a mano, la risoluzione viene ri-applicata per
  `StableKey` (snapshot `PriorSid`).
- **Concorrenza.** Nessun indice unico su `StableKey` → `SidImporter` serializza le scritture per-ICAO
  con `SemaphoreSlim` statico (job 24h + bottone editor insieme non duplicano righe da delete+add).
- **Parser navaid = solo NOMI** (`IReadOnlySet<string>`): le coordinate non servono alla completion
  fix, rimossi `ParseCoord`/`ParseNavaidLines`; `ResolveFix` fa match esatto O(1) → alias → prefisso unico.
- **Test**: `SidImportRepositoryTests` (re-import ciclo invariato, fix manuale conservato),
  `AuroraSectorfileParserTests` (completion nome-based). Suite Infra verde.
