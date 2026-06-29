# PIANO Round 20 — Fonte unica dei settori (cataloghi) + gerarchia per callsign

**Data:** 29 giugno 2026
**Sostituisce:** il tentativo Round 19 (`Airport.ParentSectorId` su `Sector` operativi) e il plan `greedy-prancing-cat.md` (di cui assorbe e amplia lo scope).
**Decisione utente:** i **cataloghi importati** (`AccSector` + `AirportSector`) diventano la **fonte autoritativa unica** dei settori. I `Sector` operativi **non si editano più a mano**: diventano una **proiezione** rigenerata/sincronizzata dai cataloghi, che porta solo i legami documento + l'albero AoR.

---

## 1. Problema

Oggi i settori vivono in **due rappresentazioni scollegate**:
- **`Sector` operativi** (tab. `Sectors`): alimentano **AoR/Topology** (`TopologyBuilder` per `AccId`, albero da `ParentSectorId`) e **documenti** (`Sector.DocumentId`/`IsPrimary`, `ContentBlock.ScopeSector`, `DocumentParty.Sector`, `AirportFrequencyLink.SourceSectorId`). Creati a mano / da `EnsureAirportSectorsAsync` (un solo `{ICAO}_APP` generico, nessun CTR).
- **Cataloghi** (`AccSector`/`AirportSector`): import da sorgente per callsign (`ComposePosition`), con shape/freq/limiti. Sono i settori **veri** che l'utente gestisce in `/vsop/admin/acc`.

Gli insiemi non coincidono → l'editor gerarchia non vede i CTR; generare `Sector` dai cataloghi creerebbe duplicati.

## 2. Obiettivo

**Una sola fonte autoritativa = cataloghi.** `Sector` degrada a **proiezione derivata** (mai editata a mano):
- chiave stabile = **`Callsign`** (= `ComposePosition`), così upsert preserva `Sector.Id` e le FK documento non si rompono;
- `Type`/`Kind`/`AccId`/`DefaultFrequency`/`AirportId`/`ParentSectorId` derivati dai cataloghi;
- `ParentSectorId` derivato dal nuovo **`ParentCallsign`** sui cataloghi (gerarchia per callsign, cross-ACC).

I legami documento (`DocumentId`/`IsPrimary`, scope block, parties, freq link) **restano su `Sector`** e cavalcano la proiezione → **pipeline documenti invariata**, **AoR invariato** (`TopologyBuilder` continua a leggere `Sector`), **test S1–S10 verdi**.

> Eliminazione totale di `Sector` (doc+AoR direttamente sui cataloghi) = **fuori scope**, punto d'arrivo dopo la fase live.

## 3. Modello dati

### 3.1 Gerarchia per callsign sui cataloghi (assorbe `greedy-prancing-cat`)
In `src/Vipi.Domain/Entities/Anagrafica.cs`:
- **`AccSector.ParentCallsign`** (string?) — padre (callsign) del subcenter ACC.
- **`AirportSector.ParentCallsign`** (string?) — padre della posizione **APP** (DEL/GND/TWR: null, non sono nodi).
- **`Airport.ParentCallsign`** (string?) — padre dell'aeroporto-foglia. **Sostituisce** `Airport.ParentSectorId`/`ParentSector` (Round 19, da rimuovere).
- Nessuna FK (callsign cross-tabella); indici su `ParentCallsign` per i tre.

### 3.2 Marcatura proiezione su `Sector`
- `Sector.Kind` resta (`Acc`/`Airport`); aggiungo **`Sector.SourceCallsign`**? **No**: `Callsign` È già la chiave naturale = `ComposePosition`. Sufficiente.
- (Opz.) flag `Sector.IsProjected` per documentare che è derivato — **non necessario** ora; tutti i Sector diventano proiezione.

### 3.3 Migrazioni
1. **Rimuovere** `AddAirportHierarchy` (Round 19, **non applicata in dev**): `dotnet ef migrations remove`.
2. **`AddHierarchyParentCallsign`**: 3 colonne TEXT nullable (`AccSectors.ParentCallsign`, `AirportSectors.ParentCallsign`, `Airports.ParentCallsign`) + indici; drop colonna/FK `Airports.ParentSectorId`.
   - In dev: reset `vipi.db`. In prod: additiva tranne il drop di `ParentSectorId` (mai usato in prod → sicuro).

## 4. Application — servizi

### 4.1 Sync proiezione: `ISectorProjectionService` (nuovo)
`SyncFromCatalogsAsync(CancellationToken)` — **idempotente**, di sistema (no authz utente):
1. Legge `AccSector` (non nascosti) ∪ `AirportSector` (non nascosti). Nodi = tutti tranne… in realtà servono **tutti** i settori operativi: i CTR (AccSector) e le posizioni d'aeroporto (AirportSector). DEL/GND/TWR/APP restano `Sector` come oggi (servono ai documenti aeroporto), **ma** ora derivati dal catalogo invece che da `EnsureAirportSectorsAsync`.
2. Per ogni voce catalogo visibile → **upsert `Sector` per `Callsign`**:
   - crea se manca (preserva Id se esiste); aggiorna `Type` (da `Position`), `Kind` (Acc se da `AccSector`, Airport se da `AirportSector`), `AccId` (da `CenterId`/`AccCode`), `DefaultFrequency` (da `Frequency`), `AirportId` (da `AirportIcao`).
   - **non** tocca `DocumentId`/`IsPrimary` (legami editoriali).
3. Risolve **`ParentSectorId`** dal `ParentCallsign` del catalogo (lookup callsign→Sector.Id nella mappa unita).
4. **Settori orfani** (Sector il cui callsign non è più in nessun catalogo visibile): **disattiva** (`IsActive=false`) invece di cancellare (preserva FK documento + audit). Riattiva se ricompare.
5. `SaveChanges`.

Hook: chiamata al termine di **`AccImportHostedService`** e **`AirportSectorImportHostedService`** (e dai bottoni import manuali in `/vsop/admin/acc`).

### 4.2 Gerarchia editor: `IHierarchyEditingService` (nuovo, da `greedy-prancing-cat`)
- **`LoadTreeAsync()`** → grafo globale `HierarchyNode { Kind(Acc|App|Airport), Id, Callsign, Label, AccCode, ParentCallsign, IsHidden }`. Sorgenti: `AccSector` (tutti non nascosti) + `AirportSector` con `Position=="APP"` non nascosti + `Airport`. DEL/GND/TWR esclusi.
- **`SetParentAsync(kind, nodeId, parentCallsign?)`** → set `ParentCallsign` sull'entità giusta. Valida: padre esiste ed è nodo APP/ACC; **anti-ciclo** sui nodi interni (cammino `ParentCallsign` nella mappa unita; Airport è foglia → no ciclo); cross-ACC ammesso. Authz `EnsureCanEditAccAsync(ACC del figlio)`. Dopo il set → invoca `SyncFromCatalogsAsync` (riproietta l'albero su `Sector`). null = stacca.

## 5. Revert Round 19
- `Airport.ParentSectorId`/`ParentSector` (entità) → rimossi (sost. da `ParentCallsign`).
- `AirportRow.ParentSectorId` in `StructureData`/`StructureEditModels` + `EfStructureEditingRepository` → rimosso.
- `ITopologyEditingService.SetAirportParentAsync` (operativo) + metodo repo → rimosso (sost. da `IHierarchyEditingService.SetParentAsync`).
- `EnsureAirportSectorsAsync` + `AddSectorAsync`/`DeleteSectorAsync` manuali: i `Sector` non si creano/eliminano più a mano → rimossi dalla UI; `EnsureAirportSectorsAsync` **sostituito** dalla sync (la generazione documento aeroporto continua a funzionare perché i Sector aeroporto ci sono, ma vengono dalla sync).

## 6. Infrastructure / Persistence
- `VipiDbContext`: config `ParentCallsign` (indici), rimuovere config FK `Airport→Sector` (Round 19).
- `TopologyBuilder`: **invariato** (legge ancora `Sector` per `AccId`; ora il `ParentSectorId` arriva dalla sync). ✅ niente rischio AoR.
- `EfHierarchyEditingRepository` (nuovo) per `IHierarchyEditingService`.
- `EfSectorProjectionRepository` (o dentro il service): l'upsert per callsign.

## 7. UI
- `StrutturaPage` (`/vsop/admin/sectorstructure`): sezione "Gerarchia settori" → **editor grafico globale** (cross-ACC) alimentato da `IHierarchyEditingService.LoadTreeAsync()`. Riuso shell 2 colonne + CSS Round 19 (`.gerarchia-2col`/`.htree-flat`/`.node-badge`/`.fallback-chain`) + badge ACC. Rimosse da qui creazione/eliminazione settori e modifica frequenza (già fatto in R19).
- Pagina ACC (`/vsop/admin/acc`): invariata (gestione cataloghi + limiti + hide). È lì che "nascono" i settori.

## 8. Test
- `Vipi.Infrastructure.Tests`:
  - **Sync**: upsert per callsign preserva `Sector.Id` e `DocumentId`; orfano → `IsActive=false`; riattivazione; `ParentSectorId` derivato da `ParentCallsign`; cross-ACC.
  - **Hierarchy**: `SetParentAsync` per callsign sui 3 tipi; cross-ACC (Crotone sotto Roma); anti-ciclo; null stacca; `LoadTree` include i 3 tipi, esclude DEL/GND/TWR.
- `Vipi.Application.Tests`: **S1–S10 invariati e verdi** (Topology costruita in-memory, non tocca i cataloghi).
- Fixture: catena profonda LIRF (5 livelli) + caso Crotone.

## 9. Documentazione
- `SPEC_Modello_Dati.md` §9.12: riscrivere su **cataloghi + callsign**; `Sector` = proiezione; lista migrazioni (sostituire `AddAirportHierarchy` con `AddHierarchyParentCallsign`).
- `README.md`/`HANDOFF.md`: banner Round 20 (fonte unica + gerarchia callsign), aggiorna conteggio test.
- `MAPPA_PAGINE.md`: nota gerarchia globale cross-ACC.

## 10. Ordine di esecuzione
1. Modello: `ParentCallsign` ×3, rimuovi `Airport.ParentSectorId`. Build.
2. Migrazione: remove `AddAirportHierarchy`, add `AddHierarchyParentCallsign`. Reset `vipi.db` dev.
3. `ISectorProjectionService` + repo + hook negli hosted/import. Test sync.
4. `IHierarchyEditingService` + repo. Test hierarchy.
5. Revert Round 19 (entità/StructureData/SetAirportParentAsync/Ensure/Add/Delete manuali).
6. UI `StrutturaPage` editor gerarchia globale.
7. Build + `dotnet test` tutti verdi.
8. Doc.

## 11. Verifica end-to-end
1. `dotnet build` + `dotnet test` verdi.
2. Riavvia Host (applica `AddHierarchyParentCallsign`).
3. `/vsop/admin/acc` → importa ACC + settori → la sync popola i `Sector` (CTR inclusi).
4. `/vsop/admin/sectorstructure` → gerarchia mostra **tutti** i settori (AccSector+APP) + aeroporti; DEL/GND/TWR no.
5. Crotone sotto APP/CTR Roma → catena fallback cross-ACC; persistente.
6. vIPI ACC esistenti continuano a rendersi (Sector→Document intatti).
