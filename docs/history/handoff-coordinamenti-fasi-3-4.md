# HANDOFF — Coordinamenti/trasferimenti: Fasi 3-4 (resa documento)

> ⚠️ **Storico (point-in-time).** L'editor non è più `Pages/XferEditorPage.razor` (rimosso): è stato sostituito dalla pagina admin globale `AdminTrasferimentiPage.razor` (`/vsop/admin/trasferimenti`). Resto del documento invariato.

Contesto minimo per riprendere. Fasi 1-2 DONE (modello+editor, build+test verdi). Restano **Fase 3 (resa Estesa)** e **Fase 4 (resa Ridotta live)**. Progetto Blazor .NET 8, dir `src/`. Build per-progetto (Host gira e blocca i DLL): `dotnet build src/Vipi.Xxx/Vipi.Xxx.csproj`. Migrazioni: `dotnet ef migrations add NOME --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure --output-dir Persistence/Migrations` (usa DesignTimeDbContextFactory, non serve fermare Host). Dev resetta `vipi.db`.

## Modello già in piedi (Fase 1-2)

- `TransferFlow` (`Domain/Entities/Support.cs`): `AccId`, `OwningSectorId→Sector`, `Kind` (`TransferFlowKind` Arrival/Departure/Overflight/Vfr/Other), `AirportIcao?`, `Description?`, `Order`, `Points`.
- `TransferPoint`: `Cop` (string), livello strutturato `LevelValue?`+`LevelUnit`(Fl/Feet)+`LevelConstraint`(AtOrAbove↑/AtOrBelow↓/Exact/Special)+`LevelSpecial?`, `NextSectorId?→Sector`, `Fallback`(`TransferFallback` Unicom/BorderRelease/Keep), `ManualChainJson?` (override = JSON di Sector.Id ordinati), `Order`.
- Formatter: `Vipi.Domain.LevelFormatting.Format(value,unit,constraint,special)` → `"FL130↓"`/`"2500 ft"`/`"per aerovia"`/`"—"`.
- DTO (`Application/Content/TransferModels.cs`): `TransferFlowRow{Id,AccCode,OwningSectorId,OwningSectorCallsign,Kind,AirportIcao?,Description?,Order,Points}`, `TransferPointRow{Id,Cop,Level*,LevelText,NextSectorId?,NextSectorCallsign?,Fallback,ManualChain:int[],Order}`, input `TransferFlowInput`/`TransferPointInput`, `ResolvedTransferPoint{Point,ResolvedHandler,IsOnline}`.
- Service `ITransferService` (`Content/TransferEditingService.cs`): `ListFlowsByAccAsync` + CRUD flow/point (ACC-gated, validazione soft). **NON** ha più resolved/live.
- Resolver puro `Content/TransferOnlineResolver.cs`: `Resolve(IReadOnlyList<string> candidates, TransferFallback, IReadOnlySet<string> online) → (Handler,IsOnline)` + `FallbackLabel`. Euristica callsign↔candidato (esatto / segmento split '_' / sottostringa ≥4).
- Repo `EfTransferRepository`, seed `Seed/RomaTransferSeed.cs` (flussi NE arrivi/partenze + EW sorvoli su settori seedati). Editor `Pages/XferEditorPage.razor` (DONE, funzionante).
- Vista globale settori per i picker: `IStructureEditingService.ListSectorNodesAsync()` → `GlobalSectorRow{Id,Callsign,AccCode,CountryPrefix,Type,Kind,ApproachKind?,ParentSectorId?,DocumentId?}` — **ATTENZIONE: admin-gated** (`EnsureAdmin`), non usabile da pagine pubbliche (vedi Fase 4).

## Target visuale (mockup v2 — file eliminato dal repo il 2026-08-01, vedi HANDOFF §8)

- Estesa ACC, righe ~970-1115 (`#doc`/`#s-coord`): **Coordinamenti** = gruppi (`Settori ACC` / `Settori APP` / `vLOA estere` / Aree) → per **settore proprio** (NE,EW,TW1,ASW) `details.coord-sub` → per **flusso** (`Traffico Dest LIRF`/`DEP LIRF`/`OVF`/`VFR`) `details.coord-sub2` → **prosa** + tabella `CoP|FL|Next` (+ immagine/tip opzionali). Stessa struttura per APP non remot. (screen `#appn`, righe ~1437+ no — vedi `Pages/AppnPage.razor` reale: già ha sezioni "Trasferimenti verso ACC/torri" hardcoded).
- Ridotta, righe ~1213-1276 (`#reduced`): blocco "🔄 Trasferimenti · dove passo il traffico": chip "Online nel tuo intorno" + per relazione una card con CoP/FL/**Next risolto live** (settore nominale se online, altrimenti chi lo copre).

## FASE 3 — Resa Estesa (ACC + APP non remot.)

Obiettivo: generare la sezione **Coordinamenti** del documento dai `TransferFlow`/`TransferPoint`, struttura gruppi→settore→flusso→[prosa + tabella CoP/FL/Next].

Punti d'integrazione da investigare prima:
- Pipeline vIPI ACC: `Application/Content/VipiViewService.cs` (`BuildAccVipiAsync`) costruisce `DocumentView`/`SectionView`/`BlockView`. Capire se le sezioni vengono da `ContentBlock` del documento (DB) o sono assemblate. La sezione Coordinamenti va resa dai flussi: o (a) generata come `SectionView`/tabelle al volo nel ViewService, o (b) un componente dedicato in `Pages/VipiDocument.razor` che legge i flussi (`ITransferService.ListFlowsByAccAsync`).
- Componenti riusabili: `Components/SectionNode.razor` (accordion depth 0/1/2 → `block`/`coord-sub`/`coord-sub2`), `Components/Blocks/TableBlock.razor` (tabella JSON-driven), `BlockRenderer.razor`. Classi CSS `coord-sub`/`coord-sub2`/`coord-group` già esistenti.
- Doc APP non remot.: `Pages/AppnPage.razor` (oggi MOCKUP statico LIRP). Stessa resa: i flussi il cui `OwningSector` è l'APP standalone.

Approccio consigliato: rendere la sezione Coordinamenti **da dati** (non ContentBlock) via un componente che raggruppa `ListFlowsByAccAsync` per `OwningSectorCallsign` (gruppo «Settori ACC» se Kind d'area / «Settori APP» se l'owning è un APP) → flusso (`FlowTitle` = Kind+Airport) → `Description` (prosa) + tabella `CoP | LevelText | NextSectorCallsign`. Riusare le classi `coord-sub`/`coord-sub2`. NB: la prosa/immagini/tip extra del mockup sono contenuto editoriale separato (fuori scope strutturato; eventuale follow-up).

Decisione aperta da chiarire con l'utente: la sezione Coordinamenti è **interamente generata dai flussi** o **coesiste** con eventuali ContentBlock manuali (prosa/immagini)? Probabile: generata dai flussi + descrizione del flusso come unica prosa.

## FASE 4 — Resa Ridotta + risoluzione live

Obiettivo: nella vista Ridotta (`Pages/RidottaPage.razor`, oggi DISABILITATA, già adattata a resa nominale dei flussi), risolvere il **Next live**: per ogni punto, candidati = `ManualChain` (id→callsign) se valorizzata, **altrimenti** `[NextSector, …antenati risalendo ParentSectorId fino alla radice ACC]`; primo online (via `TransferOnlineResolver.Resolve`), poi `FallbackLabel(Fallback)`.

Serve:
1. Metodo di servizio nuovo, es. `ITransferService.ListResolvedFlowsByAccAsync(accCode, online)` o helper, che per ogni punto costruisce la catena candidati. Richiede mappa `Sector.Id → (Callsign, ParentSectorId)` **NON admin-gated** (la Ridotta è pubblica/operativa). NON usare `ListSectorNodesAsync` (admin-gated): aggiungere una lettura leggera non gated (es. nuovo metodo repo `ListSectorTreeAsync` o riusare `IStationDirectory`/topology). Valutare `ITopologyProvider`/`Topology` (già iniettato in RidottaPage: ha `Sectors`, `Parent`, `DomainOf`) — probabilmente sufficiente per risalire i padri per callsign.
2. Online ATC: `IOnlineAtcProvider.GetCurrent().Callsigns` (già in RidottaPage `_snapshot`).
3. UI: colonna "Next" risolta (chip verde se online, muted+fallback se no) + chip "online nel tuo intorno". Riusare struttura già presente in RidottaPage.
4. Collasso morbido handoff non necessari (settore già covered) — pattern `tag-covered`/`collapsed-strip` del mockup.

Nota: il resolver lavora su callsign; gli antenati si ottengono per callsign via topology (`Topology.Parent` mappa callsign→padre callsign). Quindi i candidati possono essere costruiti **interamente per callsign** senza toccare i Sector.Id, sfruttando `NextSectorCallsign` del DTO + risalita `Topology.Parent`. Questo evita query extra e il problema dell'admin-gating.

## Verifica finale attesa

- `dotnet build` (Application/Infrastructure/Ui) + `dotnet test` verdi.
- Estesa: vIPI ACC mostra Coordinamenti → settore → flusso → tabella CoP/Livello/Next dai dati seed (NE: VALMA FL130↓ → LIRF_TWR ecc.). Idem doc APP.
- Ridotta: con LIRF_TWR offline e LIRR_NE_CTR online, il Next di "Dest LIRF" risale a LIRR_NE_CTR; tutti offline → fallback; override manuale rispettato.
- Aggiornare `docs/spec/modello-dati.md` §7.4 (rimodella `Transfer`→`TransferFlow`/`TransferPoint`) e `mappa-pagine.md` se serve.
