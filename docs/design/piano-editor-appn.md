# Editor vIPI — APP non remotizzati (mockup 3c)

> ⚠️ **Piano di progetto storico, non descrizione del codice attuale.** Il componente `SectionShell` previsto
> qui (§ "Componenti", "Resa WYSIWYG") è stato realizzato e poi **abbandonato**: gli editor hanno finito per
> portare il chrome di sezione al proprio interno, e il file è stato rimosso perché orfano nel cleanup del
> 2026-07-30. Il drag-and-drop e la toolbar di sezione vivono ora dentro i singoli editor.

## Context
Gli APP non remotizzati hanno una vIPI propria. Oggi `AppnPage.razor` (`/vsop/{acc}/apps/vipi`) è un **mockup statico hardcoded** (LIRP) e non esiste un editor dedicato: gli APPn ricadrebbero nell'editor generico per blocchi. Serve un **editor dedicato** (sul modello di `AeroportoEditorPage`) che produca esattamente la schermata 3c del `mockups/vipi-ui-mockup-v2.html`, con 6 sezioni fisse (sempre presenti, riordinabili) + sezioni custom libere. Alcune sezioni sono **derivate live** (si aggiornano da sole quando cambia l'albero/i transfer), altre editoriali.

Decisioni prese: **shape AoR reale** (parsing `RegionMapPolygon` IVAO ora) · **editor dedicato + sezioni derivate live** (no snapshot) · custom sezioni riusano il modello sezioni/blocchi generico · **editor in stile WYSIWYG** (sezioni rese come nel documento) · **riordino drag-and-drop + tasti** · **sezioni fisse definite via codice** (registry, facile aggiungerne di nuove obbligatorie).

## Sezioni richieste (ordine default, tutte sempre presenti, riordinabili)
1. **Separazioni** — box editabile (righe orizzontale/verticale, es. «Radar 3 NM», «Verticale 1000 ft»).
2. **AoR** — SVG della shape del settore APP da `Sector/AirportSector/AccSector.RegionMapPolygon` (poligono IVAO reale).
3. **Frequenze** — derivate LIVE dal **ramo dell'albero** dell'APP (sottoalbero = `Topology.DomainOf(appCallsign)`): ATIS degli aeroporti sottostanti · DEL · GND · TWR · APP (principale ★ evidenziata). Ordine modificabile per singola frequenza (override) + possibilità di **linkare** frequenze extra. Cambia l'albero → cambiano le frequenze.
4. **VFR** — prosa + tabelle (trasferimento VFR APP↔torre). Riusa blocchi Prose/Table.
5. **Minime di vettoramento** — **placeholder** (import da GitHub = follow-up, come oggi nel mockup).
6. **Coordinamenti** — derivati LIVE da `ITransferService` (flussi del settore APP), 2 sottosezioni: **verso ACC** (sub: Partenze + Arrivi) · **verso torre/i** (solo Arrivi, quota dipendente dalla IAP).

Inoltre: **sezioni custom** (riordinabili) con sottosezioni custom contenenti paragrafi o tabelle custom → riuso del modello generico `DocumentSection`/`ContentBlock` (3 livelli) già editato da `EditorPage`.

## Approccio
Editor dedicato `/vsop/{acc}/apps/editor?app={APP_CALLSIGN}`. Parti **editoriali** salvate in un nuovo profilo APPn; parti **derivate** calcolate live da hierarchy/transfers/polygon; **custom** sul modello sezioni/blocchi generico ancorato al Document dell'APPn.

**UX editor = WYSIWYG.** Ogni sezione è resa con gli **stessi componenti del viewer** (riuso `AppnPage`/`SectionNode`/`BlockRenderer`) racchiusa in un wrapper con toolbar per-sezione (modifica / ↑ / ↓ / drag-handle / elimina-se-custom). Niente doppia resa: editor e documento condividono i componenti di rendering.

**Riordino = drag-and-drop + tasti.** Wrapper `draggable="true"` con `@ondragstart`/`@ondragover:preventDefault`/`@ondrop` nativi Blazor (no/minimo JS interop); tasti ↑/↓ come fallback accessibile. L'ordine è persistito in `SectionOrderJson`.

**Sezioni fisse via codice (registry).** Le 6 sezioni fisse sono **descrittori in codice** (`AppSectionDescriptor`: `Key`, `Title`, `DefaultOrder`, tipo derivata/editoriale, render). Una lista statica `AppSections.All` è la fonte di verità. Al load, **riconciliazione**: ogni sezione registrata mancante nel `SectionOrderJson` del profilo viene inserita al suo `DefaultOrder` → aggiungere in futuro una nuova sezione obbligatoria = aggiungere un descrittore, senza migrazione dati. Le custom restano fuori dal registry (ordinate insieme alle fisse nell'ordine salvato).

## Esecuzione
Procedere **una fase alla volta**: a fine di ogni fase build+test verdi e fermarsi per la conferma prima di iniziare la successiva.

## Fasi
**Fase 1 — Modello dati + service**
- Nuova entità `AppProfile` (1:1 col `Sector` APP via `SectorId`): `SeparationsJson` (righe label/valore), `VfrIntroJson`/blocchi VFR, `SectionOrderJson` (ordine delle 6 fisse + custom), `FreqOrderJson` (override ordine per callsign) + link freq extra (riuso pattern `AirportFrequencyLink`, nuova tabella `AppFrequencyLink` o riuso generico). Migrazione additiva.
- `IAppProfileService` (Application): `LoadForEditAsync(appCallsign)` / save delle parti editoriali (ACC-gated via `IEditAuthorizationService`), + metodi derivati: `DeriveFrequenciesAsync` (da `ITopologyProvider.BuildGlobalAsync` + freq settori), `DeriveCoordinationAsync` (riusa/estende `ITransferService` filtrando per `OwningSectorId == APP` e classificando Next ACC vs TWR), `GetAorPolygon` (dal settore).
- Riuso: `ITransferService` (Fase trasferimenti già fatta), `ITopologyProvider.BuildGlobalAsync`, `IAirportSectorService`/`IStructureEditingService` per i settori.

**Fase 2 — Parser/proiezione poligono AoR (puro, testabile)**
- `AorPolygonProjector` (Application, statico/puro): `RegionMapPolygon` (JSON IVAO) → lista di punti → proiezione equirettangolare (lon→x, lat→y·cos(lat medio)) → `path` SVG normalizzato a un viewBox. Difensivo: JSON non parsabile/poligono vuoto → null (la UI mostra placeholder). Test unit con un poligono campione.

**Fase 3 — Registry sezioni + componenti di rendering condivisi**
- `AppSectionDescriptor` + lista statica `AppSections.All` (le 6 fisse) con `Key/Title/DefaultOrder/Kind`. Logica di **riconciliazione** ordine (inserisce le mancanti) — pura, testabile.
- Componenti Blazor di sezione (`Separations`, `Aor`, `Frequencies`, `Vfr`, `Minima`, `Coordination`) usati **sia** dal viewer **sia** dall'editor; nell'editor avvolti da un `SectionShell` (toolbar + drag-handle).

**Fase 4 — Editor page `AppEditorPage.razor`**
- Route `/vsop/{acc}/apps/editor?app=`, `@rendermode InteractiveServer`, breadcrumb + guardia come `AeroportoEditorPage`.
- Resa **WYSIWYG**: le sezioni (fisse nell'ordine salvato + custom) renderizzate coi componenti condivisi, ciascuna in `SectionShell` con modifica inline + riordino **drag-and-drop e tasti ↑/↓**.
- Editoriali: Separazioni (righe), VFR (prosa+tabella). Derivate: AoR (SVG Fase 2), Frequenze (derivate + riordino per riga + add-link, UI `sector-pick`), Coordinamenti (2 sottosezioni derivate), Minime (placeholder).
- **Custom sections**: aggiunta/modifica via modello generico (`IEditingService` section/block, Prose/Table) ancorato al Document dell'APPn; ordinate insieme alle fisse.

**Fase 5 — Viewer data-driven + instradamento**
- `AppnPage.razor` (viewer) reso dai dati reali (stesse sezioni/ordine, frequenze/coordinamenti/AoR live), non più hardcoded.
- Instradare gli APPn al nuovo editor (come fatto per gli aeroporti): da `VersioniPage`/`EditorHubPage`, se il doc è APPn → link a `/vsop/{acc}/apps/editor?app=`; `EditorPage` generico reindirizza se gli arriva un doc APPn (riuso del pattern `IsAirport`/redirect già introdotto → aggiungere flag `IsAppn`/`IsStandaloneApp`).

## File principali
- NUOVO `src/Vipi.Domain/Entities/` → `AppProfile` (+ eventuale `AppFrequencyLink`).
- NUOVO `src/Vipi.Application/Content/AppProfileService.cs` (+ modelli DTO) e porta repo `IAppProfileRepository`.
- NUOVO `src/Vipi.Infrastructure/Persistence/EfAppProfileRepository.cs` + mapping in `VipiDbContext.cs` + migrazione.
- NUOVO `src/Vipi.Application/Aor/AorPolygonProjector.cs` (+ test).
- NUOVO registry `AppSections` (descrittori + riconciliazione ordine, +test) in Application.
- NUOVO `src/Vipi.Ui/Pages/AppEditorPage.razor` + componenti sezione condivisi viewer/editor (`Components/App/*`) + `SectionShell` (toolbar + drag-and-drop).
- MODIFICA `src/Vipi.Ui/Pages/AppnPage.razor` (data-driven), `VersioniPage.razor`/`EditorHubPage.razor`/`EditorPage.razor` (instradamento APPn), `EditingModels.cs`+`EfEditingRepository.cs` (flag APPn su `DocumentSummary`).
- DI: `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs`.

## Riuso esistente
- `AeroportoEditorPage.razor` — pattern editor profilo (pannelli, save per-sezione, link freq, riordino, sezioni extra).
- `ITransferService.ResolveForAccAsync`/`ListFlowsByAccAsync` + `ITopologyProvider.BuildGlobalAsync` (gerarchia globale) — derivazione coordinamenti/frequenze.
- Modello `DocumentSection`/`ContentBlock` + `IEditingService` (`EditorPage`) — sezioni/sottosezioni/paragrafi/tabelle custom a 3 livelli.
- `RegionMapPolygon` su `AccSector`/`AirportSector` + `AirportSectorRow.HasPolygon`.
- Pattern dropdown ricerca `sector-pick` (da `AdminTrasferimentiPage`) per i picker.

## Rischi / note
- Formato esatto di `RegionMapPolygon` IVAO ignoto finché non se ne ispeziona uno reale: il parser sarà difensivo (più forme accettate: `[[lat,lon],…]` o `[{lat,lon},…]`), con fallback placeholder. Verificare con un settore reale dopo l'import.
- Ampiezza: feature multi-fase; implementare e verificare fase per fase (build+test verdi a ogni fase).

## Verifica
1. `dotnet test Vipi.slnx` verde a ogni fase; nuovi test: `AorPolygonProjector` (poligono campione → path) e derivazione coordinamenti per settore APP.
2. Stop Host, `dotnet run --project src/Vipi.Host --urls http://localhost:5034`.
3. `/vsop/{acc}/apps/editor?app=LIRP_APP`: le 6 sezioni rese **come nel documento** (preview WYSIWYG); separazioni editabili; AoR disegna il poligono; frequenze derivate nell'ordine ATIS·DEL·GND·TWR·APP con ★; cambiando l'albero (`/vsop/admin/sectorstructure`) cambiano; coordinamenti derivati dai transfer (verso ACC partenze/arrivi, verso torre arrivi); **riordino sia drag-and-drop sia tasti ↑/↓** (persistito); aggiunta sezione custom con tabella.
4. Registry: aggiungendo un descrittore in `AppSections.All`, la nuova sezione compare in tutti i profili esistenti senza migrazione (riconciliazione al load).
5. Da `/vsop/versioni` «Apri editor» su un doc APPn → apre il nuovo editor, non il generico.
6. Viewer `/vsop/{acc}/apps/vipi` riflette i dati reali.

## Stato implementazione — COMPLETATO (sessione 29 giu 2026)
Tutte le **5 fasi** implementate, **151 test verdi**. Entità `AppProfile`/`AppFrequencyLink` + migrazioni `AddAppProfile`, `AddAppCustomSections`, `AddAppHiddenSections` (additive). Editor `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`), viewer `AppnPage` data-driven, componenti condivisi in `Components/App/`.

**Scostamenti dal piano (deliberati) e dettagli emersi:**
- **Frequenze**: NON derivate da `DomainOf` dei `Sector` — nella proiezione Round 20 le posizioni DEL/GND/TWR hanno `ParentCallsign=null` → proiettate come **radici**, non figlie del Sector APP. Derivazione reale: aeroporti con `Airport.ParentCallsign ∈ DomainOf(appCallsign)` → tutte le posizioni dal **catalogo `AirportSector`** (ATIS·DEL·GND·TWR·APP★). In **coda** i **genitori** di copertura (`Topology.Ancestors`, es. `LIRR_NE_CTR` e CTR superiori). Riordino per riga (override) + link extra restano.
- **AoR**: il campo IVAO `regionMapPolygon` (salvato grezzo in `AirportSector.RegionMapPolygon`, da `/v2/ATCPositions/{compose}`) è in ordine **`[lng, lat]`** (GeoJSON-style). La sezione si chiama **«AOR»** e mostra il poligono **sovrapposto a una mappa minimal** (Leaflet + CartoDB Positron, `vipi-aor.js`), con SVG di fallback no-JS.
- **Sezioni custom**: storage self-contained in `AppProfile.CustomSectionsJson` (titolo + blocchi prosa/tabella con colonne/righe editabili), **non** il modello generico `Document`/`IEditingService`.
- **Separazioni**: due colonne fisse **Verticale | Laterale**; dalla 2ª riga un free-text **Applicabilità** mostrato **sopra** i valori (la 1ª è la predefinita).
- **Nascondi sezioni**: `AppProfile.HiddenSectionsJson` — escluse dal viewer pubblico, visibili/ripristinabili in editor (toggle 👁).
- **Instradamento**: `DocumentSummary.IsStandaloneApp` (settore primario `App`+`Standalone`) ha **precedenza** su `IsAirport`; `EditorHub`/`Versioni`/`EditorPage` reindirizzano a `/apps/editor?app=`. Tasto **✎ Editor** (gated da `CanEditAccAsync`) nel viewer.
- **Componenti**: `SectionShell` (toolbar + drag-and-drop solo sulla maniglia, per non bloccare gli input) + `AppSeparations`/`AppAor`/`AppFrequencies`/`AppVfr`/`AppMinima`/`AppCoordinationView` (nome ≠ DTO `AppCoordination`).
