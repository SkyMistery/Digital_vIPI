# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 29 giugno 2026 (Round 20)
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato:** progetto **in sviluppo attivo**. Design UI completo (mockup v2, 17 schermate) **e** codice avanzato: solution .NET 8 a 4 layer + Host Blazor Server, **test verdi (128)**, consultazione+editing+sicurezza funzionanti dal DB. **Live IVAO (F3) implementato**: polling + cache + SSE, Ridotta live (AoR reattivo, "primo online", online nel dominio), auto-elenco CH permessi. **Sorgente dati esterna disaccoppiata** (interfacce neutre + `DataSource:Provider`) e **policy di import opt-out** (dati di sorgente in sola lettura). **Struttura pagine rifatta su prefisso `/vsop`** (Round 12): vedi `MAPPA_PAGINE.md`. **ACC/settori importati dalla sorgente** (Round 13). **Fonte unica = cataloghi: i `Sector` sono una proiezione, gerarchia di copertura per callsign cross-ACC** (Round 20, SPEC §9.12).

> 🪵 **Round 20 — Fonte unica dei settori (cataloghi) + gerarchia per callsign (29 giu, 128 test).** I **cataloghi importati** (`AccSector`/`AirportSector`) sono ora la **fonte autoritativa unica** dei settori; i `Sector` operativi diventano una **proiezione** rigenerata dai cataloghi (`ISectorProjectionService.SyncFromCatalogsAsync`). Risolve la doppia rappresentazione del Round 19 (gerarchia su `Sector` operativi, che non includevano i CTR/APP granulari del catalogo). Piano completo: `PIANO_Round20_FonteUnica.md`.
> - **Gerarchia per callsign, cross-ACC.** Nuovi `ParentCallsign` (string?) su **`AccSector`**, **`AirportSector`** (solo posizioni APP), **`Airport`** (foglia) — **sostituiscono** `Airport.ParentSectorId` (Round 19, rimosso). Albero unico per la divisione (caso Crotone: aeroporto sotto APP/CTR di un altro ACC). Migrazione **`AddHierarchyParentCallsign`** (additiva: 3 col TEXT + indici + `Sectors.IsProjected`; `AddAirportHierarchy` rimossa, mai applicata).
> - **`Sector` = proiezione.** Sync idempotente (hook negli import ACC/aeroporto + `AccAdminService`/`AirportSectorService` + dopo ogni `SetParent`): **upsert per `Callsign`** che preserva `Sector.Id` + i legami documento (`DocumentId`/`IsPrimary`/`FeaturedRank`) → FK doc intatte; deriva `Type`/`Kind`/`AccId`/`DefaultFrequency`/`AirportId`/`ParentSectorId` (da `ParentCallsign`). Flag **`Sector.IsProjected`**: i proiettati spariti/nascosti nel catalogo → `IsActive=false` (non cancellati); i seed/manuali (`IsProjected=false`) **mai toccati** → **test AoR S1–S10 intatti**. `TopologyBuilder` invariato (legge ancora `Sector`).
> - **Editor globale.** `IHierarchyEditingService.LoadTreeAsync()`/`SetParentAsync(kind,id,parentCallsign?)` (anti-ciclo nodi interni, cross-ACC, ACC-gated, poi riproietta). La sezione "Alberi per ACC" di **`/vsop/admin/sectorstructure`** (`StrutturaPage`) è **globale** (cross-ACC, indipendente dal selettore ACC che resta per "Nuovo documento"): **UI a card per ACC** (ogni card = gli alberi con radice in quell'ACC, discendenti cross-ACC inclusi; comprimi/espandi card+rami + ricerca dell'intera gerarchia del CS) + pannello **Dettaglio sticky** (catena di fallback, picker padre ricercabile, bottone **Applica**).
> - **Rimosso** (Round 19): `Airport.ParentSectorId`/`ParentSector`, `ITopologyEditingService.SetAirportParentAsync` (+ repo), `AirportRow.ParentSectorId` (ora `ParentCallsign`). I 4 test airport-hierarchy operativi sostituiti da `SectorProjectionTests` (sync + gerarchia per callsign + cross-ACC + anti-ciclo + LoadTree esclude DEL/GND/TWR). **128 verdi** (Domain 5 · Application 60 · Infrastructure 63).
> - **Fuori ambito (follow-up):** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi; eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" = fase live. SPEC §9.12.
> - **Da fare al deploy:** reset `vipi.db` in dev (o applica `AddHierarchyParentCallsign`) → riavvia Host → `/vsop/admin/acc` importa ACC+settori (la sync popola i `Sector`, CTR inclusi) → `/vsop/admin/sectorstructure` mostra l'albero globale.

> 🌳 **Round 19 — Gerarchia di copertura aeroporto‑foglia + editor grafico (29 giu, 126 test). ⚠️ SUPERATO dal Round 20** (era su `Sector` operativi; ora su cataloghi per callsign). Modellato l'albero di fallback **Aeroporto → APP → settore ACC** a **padre unico** (un nodo = un albero), profondità/ramificazione libere; risoluzione = primo antenato online (riuso `AorService.NearestOnlineAncestor`). Casi reali coperti (LIRN, LIRF con APP‑sotto‑APP a 5 livelli, LIRF_AEL_APP ramo di soli settori, LIRP, LIRJ aeroporto diretto sotto CTR).
> - **Modello**: nuovo **`Airport.ParentSectorId`** (int? FK→`Sector`, `OnDelete SetNull`, nav `ParentSector`, indice; migrazione **`AddAirportHierarchy`**). L'**aeroporto è la foglia**: DEL/GND/TWR **non** sono nodi (condividono la sua vista rapida). Padre ammesso = `Sector` `Type ∈ {Ctr, App}` stesso ACC. I `Sector` APP/CTR continuano a usare `Sector.ParentSectorId` (catena APP→CTR), **invariato** (AoR ACC e relativi test intatti).
> - **Editor**: la sezione "Settori" di **`/vsop/admin/sectorstructure`** (`StrutturaPage`) sostituita da un **editor grafico a due colonne** (albero appiattito a profondità libera con indentazione dinamica + pannello dettaglio con catena di fallback e selettore padre). **Rimosse** da lì creazione/eliminazione settori e modifica frequenza (gestione solo dalla pagina ACC). `AirportRow.ParentSectorId` in `StructureData`; scrittura via **`ITopologyEditingService.SetAirportParentAsync`** (ACC‑gated, valida padre APP/CTR stesso ACC; aeroporto foglia → niente anti‑ciclo). CSS `.gerarchia-2col`/`.htree-flat`/`.node-badge`/`.fallback-chain`.
> - **Fuori ambito (follow‑up)**: risoluzione **live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `Airport.ParentSectorId`→`Sector.ParentSectorId`) e instradamento callsign DEL/GND/TWR alla vista rapida aeroporto. SPEC §9.12.

> 🗓️ **Round 18 — Regole pista in ora locale (LT) + finestra stagionale, sezioni extra (29 giu, 122 test).** Tre rifiniture sull'editor/viewer aeroporto.
> - **Orari «Avanzate» in ora LOCALE (LT), non più Z.** Gli orari AIP sono in ora locale: `AirportRunwayRule.TimeFromUtcMin/TimeToUtcMin` **rinominati** `TimeFromLocalMin/TimeToLocalMin` (anche in `RunwayRuleRow`/`RunwayRuleEval`, repo, viewer). `RunwaySuggestion.EvaluateRules` converte l'istante UTC in **ora italiana** (CET/CEST con DST, `TimeZoneInfo` risolto `Europe/Rome`→`W. Europe Standard Time`→UTC) **prima** di valutare orario/giorni/parità/stagione. UI: «da/a **(LT)**»; documento: «08:00–20:00 **LT**». Migrazione **`RenameRunwayRuleTimeToLocal`** (`RenameColumn`, dati preservati). ⚠️ I valori orari già inseriti pensandoli come Z ora valgono come LT (numeri invariati).
> - **Finestra di validità STAGIONALE ricorrente per regola.** Nuovi `int?` **`DateFromMonthDay`/`DateToMonthDay`** in **MMDD** (mese×100+giorno, es. `101`=1 gen, `331`=31 mar), estremi inclusi, **anno ignorato** (si ripete ogni anno), **wrap di fine anno** gestito (`RunwaySuggestion.DateInWindow`). Editor: selettori **giorno+mese** (no anno; un estremo conta solo se entrambi valorizzati). Migrazione **`AddRunwayRuleDateWindow`** (2 colonne INTEGER nullable). +2 test (finestra normale + wrap).
> - **Sezioni extra editoriali (`AirportExtraSection`).** Nuova entità del profilo strutturato (`Title` obbligatorio + `Body` testo libero + `Order`, FK→`Airport` cascade; collezione `Airport.ExtraSections`). Editor: pannello **«Sezioni extra»** (add/rimuovi/riordina). Viewer `AeroportoPage`: rese **dal profilo** (come Piste/Frequenze) nella **colonna libera di destra** (`aside.doc-rail`, desktop ≥1500px) + **copia inline sotto le SID** (`.extra-inline`, nascosta via CSS ≥1500px). `SaveExtraSectionsAsync` ACC-gated. Migrazione **`AddAirportExtraSection`** (tabella + indice). **Non** scritte in `RebuildDocumentAsync` (composte dal profilo dal viewer) — inclusione nel documento pubblicato = follow-up. SPEC §9.10–9.11.

> 🛬 **Regole pista a SOGLIE OPERATIVE (28 giu, 120 test).** Ridisegnate le «regole pista»: non più condizioni vento-arco/velocità/pioggia-neve (first-match macchinoso), ma **soglie operative per-regola**. Ogni `AirportRunwayRule` ora: *quando le piste indicate hanno **coda ≤ `MaxTailwindKt`** (def 5), **traverso ≤ `MaxCrosswindKt`** (opz.) e **superficie** = `Surface` (enum **`RunwaySurface{Any,Dry,Wet}`**, Wet = pioggia/neve METAR), sono preferenziali per DEP/ARR*. Coda/traverso **calcolati dal vento** (l'editor imposta solo le soglie, non la direzione); valutate in **ordine**, la prima che si applica vince; se nessuna → fallback `RunwaySuggestion.Suggest` (miglior testa-vento). `Order/Name/DepRunways/ArrRunways/Note` + filtro temporale (orario/giorni/parità) come **«Avanzate»** opzionale (Malpensa). Logica in **`RunwaySuggestion.EvaluateRules(rules, windDir, windKt, wet, now)`** (rimosso il vecchio modello vento-arco). Editor `AeroportoEditorPage` pannello «Regole piste» (chip DEP/ARR + coda/traverso/superficie + Avanzate); viewer `AeroportoPage` evidenzia la regola attiva. Doc: sezione «Regole piste», colonna «Condizione» = «coda ≤ 5 kt, traverso ≤ 15 kt, pista asciutta». Migrazione **`RunwayRuleThresholds`** (drop `WindDir*`/`WindSpeed*`/`Rain`/`Snow`, add `Name`/`MaxTailwindKt`/`MaxCrosswindKt`/`Surface`, svuota le vecchie righe). SPEC §9.9. ⚠️ **Lezione deploy:** una migrazione già applicata NON va rimossa (in dev: reset `vipi.db`; in prod: nuovo step sopra).

> 🙈 **Hide aeroporti (28 giu).** Nuovo `Airport.IsHidden` (migrazione **`AddAirportHidden`**): in `/vsop/admin/airports` (tabella «Assegnati a una ACC») un aeroporto si **nasconde** (colonna «Stato» + toggle) → pagina pubblica `/vsop/{acc}/airports?icao=` inaccessibile + escluso da elenchi/landing. Gli aeroporti **senza alcun settore** sono **nascosti di default**: `AirportRow.IsPublic`/`AirportAdminRow.IsPublic` = `!IsHidden && Sectors>0`. Enforcement: `EfContentRepository.LoadAirportVipiAsync` esclude i nascosti; `AccLanding`/`AeroportoPage` filtrano `IsPublic`. Servizio `IStructureEditingService.SetAirportHiddenAsync` (ACC-gated).

> ✅ **DOC ALLINEATA (28 giu, sessione hide+doc).** I Round 13→17 + hide aeroporti sono stati riportati nei documenti di riferimento:
> - **`SPEC_Modello_Dati.md`** — nuova **§9 (round 13–17)** che prevale sulle §3/§4: `Fir`→`Acc`, `Frequency`/`SectorGeometry`/`Airport.AtisFrequency` eliminate, cataloghi `AccSector`/`AirportSector` (con `IsHidden`/limiti/`IsPrimary`), `AirportFrequencyLink.SourceSectorId`, `Airport.IsHidden`, `ImportCategory` senza ATIS, lista migrazioni fino a `AddAirportHidden`.
> - **`README.md`** — banner Round 12 + Round 13–17 + hide; conteggio test **120**; run su `/vsop`.
> - **`MAPPA_PAGINE.md`** — colonna Stato/hide in `/vsop/admin/airports` + nota aeroporti nascosti.
> - **`docs/CONFIG.md`** — chiavi IVAO `AtcPositionDetailPathFormat`/`AirportSectorImportHours`; categoria ATIS rimossa dalla policy.
> - **`docs/adr/ADR-0006`** — nota round 16: categoria ATIS rimossa, frequenza = attributo del settore.
> - **Resta da ripulire (minore):** §4/§5 di questo HANDOFF citano ancora `Frequency`/`Position` nei testi storici dei round vecchi (non bloccante; i banner in cima + SPEC §9 sono autorevoli).

> 🧹 **Round 17 — Eliminata la tabella `Frequency`.** La frequenza è ora **solo un attributo del settore** (`Sector.DefaultFrequency`, una per settore). Rimossi entità `Frequency` + `Sector.Frequencies` + `IStructureEditing*.AddFrequencyAsync`/`DeleteFrequencyAsync` (→ nuovo `SetSectorFrequencyAsync`), `StructureData.Frequencies`/`FrequencyRow`; in `/vsop/admin/sectorstructure` la frequenza del settore si edita inline (niente più pannello «Frequenze»). I **link frequenza** (`AirportFrequencyLink`) ora puntano a un **`Sector`** (`SourceSectorId`, risolve `Sector.DefaultFrequency`+`Callsign`) invece che a una `Frequency`; aggiornati `FrequencyLinkRow`/`LinkableFrequencyRow`, `EfAirportProfileRepository` (Load/ListLinkable/SaveFrequencyLinks/Rebuild) e il picker nell'editor aeroporto. Migrazione **`DropFrequencyTable`** (drop `Frequencies`, rename `SourceFrequencyId`→`SourceSectorId`). `RomaStructureSeed` non semina più `Frequency` (i settori hanno già `DefaultFrequency`). **116 test verdi.**

> 🧹 **Round 16 — Semplificazione dati (L0+L1+L2).** **L0**: rimossi `SectorGeometry` (+`Sector.GeometryId/Geometry`, enum `GeometryFormat`) mai usati; geometria futura = `RegionMapPolygon` sui cataloghi. **L1 — frequenze a fonte unica = catalogo `AirportSector`**: editor «Frequenze» e generazione documento (`EfAirportProfileRepository.RebuildDocumentAsync`) leggono dal catalogo (ordine ATIS·DEL·GND·TWR·APP, ★ per tipo); rimossi `Airport.AtisFrequency`, `AirportProfileData.OwnFrequencies`/`AtisFrequency` + `OwnFrequencyRow`, e la categoria **ATIS** dalla policy (`ImportPolicy.ImportAtis`, `ImportCategory.Atis`, toggle in `SorgentiAdminPage`); `MergeFromSourceAsync` non ha più il param `atisFrequency`. `Frequency`(tabella) resta per i settori ACC + bersaglio link. **L2 — import settori unico**: nuovo `IAirportSectorImporter` (fetch+enrich+upsert) riusato da `AirportSectorService` + hosted service (rimossa duplicazione enrich); `GenerateAirportDocumentCoreAsync` deriva i `Sector` operativi dal **catalogo** (no doppio fetch), `EnsureAirportSectorsAsync` crea anche l'**APP** (`ApproachKind=Remotized`, gerarchia APP⊃TWR⊃GND⊃DEL). Migrazione **`SimplifyDataModel`** (drop `SectorGeometries`, `Sectors.GeometryId`, `Airports.AtisFrequency`, `ImportPolicies.ImportAtis`). Fuori scope: fusione `AccSector`+`AirportSector` (L3) e rimozione `VectoringMinima`. Test aggiornati, **116 verdi** (AoR S1–S10 inclusi).

> 🛫 **Round 15 — Doc aeroporto automatici + tabella frequenze da catalogo + evidenza pista nel viewer.** All'import dei settori (hosted service giornaliero + bottone editor) ora si **crea/aggiorna in automatico il documento** di ogni aeroporto con ACC: `IStructureEditingService.EnsureAirportDocumentSystemAsync` (variante senza authz di `GenerateAirportDocumentAsync`, refactor su `GenerateAirportDocumentCoreAsync`). Nuovo campo **`AirportSector.IsPrimary`** (migrazione **`AddAirportSectorPrimary`**) = frequenza principale (★), unica per aeroporto; default all'import **TWR→APP→GND** (`PickDefaultPrimary`), override nell'editor (radio «Princ.» nel pannello Settori → `SetPrimaryAsync`, esclusiva). Viewer `AeroportoPage` (`/vsop/{acc}/airports?icao=`): la **tabella Frequenze** ora viene dal **catalogo `AirportSector`** non nascosto, ordine **ATIS · GND · TWR · APP/DEP** (nome derivato dalla position, ★ sul primario); **rimossa** la sezione «Pista suggerita dal vento» (+ input vento manuali); la sezione **Piste** è resa custom da `_profile.Runways` con **evidenza pista consigliata** (verde=arrivi, blu=partenze, alternanza se entrambe) + colonna **Uso** (🛫 dep / 🛬 arr / entrambe). La selezione pista è invariata (regole→headwind METAR): cambia solo la resa. CSS `.rwy-table/.rwy-arr/.rwy-dep/.rwy-both/.rwy-key` in `vipi-theme.css`. Test +1 (default primario + esclusiva + preservato a re-import) → **116**.

> 🛬 **Round 14 — Settori aeroporto importati + rename pagina `airports` + filtro ACC.** Nuova entità **`AirportSector`** (catalogo a parte dai `Sector` operativi; `ComposePosition` unica, FK→`Airport.Icao` e FK→`Acc.Code`, freq/shape/`Position`/`MiddleIdentifier`, `LowerLimit`/`UpperLimit`, `IsHidden`, `ImportedAtUtc`; migrazione **`AddAirportSector`** additiva + alt key `AK_Airports_Icao`). Import **di TUTTI i settori, incl. APP** (prima scartati) da `/v2/airports/{ICAO}/ATCPositions` (lista) + `/v2/ATCPositions/{compose}` (dettaglio: freq/shape/limiti). Default limiti **inf=GND(0), sup=19500**, preserva IsHidden+limiti admin su re-import. Porta `IAirportSectorService`/`IAirportSectorRepository` (ACC-gated); **hosted service giornaliero** `AirportSectorImportHostedService`. Pagina admin rinominata **`/vsop/admin/aeroporti`→`/vsop/admin/airports`** (alias legacy conservato) con **filtro per ACC** sulla tabella di sinistra. Editor aeroporto: rotta **`/vsop/{acc}/airports/editor`** (alias legacy `…/aeroporto/editor`) + nuovo pannello **«Settori»** (tabella callsign·ACC·pos·mid·freq·shape·limiti·stato con import/mostra-nascondi/limiti, stile pannello «Settori ACC»); i 5 pannelli esistenti invariati. Test +4 (incl. APP, default 0/19500, idempotenza+hide/limiti) → **115**.

> 🛰️ **Round 13 — Rename `Fir`→`Acc` + import ACC/settori dalla sorgente.** `Fir` è stato rinominato **`Acc`** in tutto il progetto (entità/proprietà `AccId`/`AccCode`, servizi `ListAccsAsync`…, claim `AccClaim`, tabella `Accs`; migrazione **`RenameFirToAcc`** non distruttiva). Gli ACC **non si creano più a mano**: si **importano dalla sorgente** (IVAO `/v2/centers`) nella nuova pagina **`/vsop/admin/acc`**, con militare + mostra/nascondi. I **settori ACC** (subcenter) si importano da `/v2/centers/{icao}/subcenters` + `/v2/subcenters/{compose}` (freq + regionMapPolygon), con **limiti quota** impostati dall'admin (default **GND→UNL**, FSS **GND→19000**). Import **manuale + automatico giornaliero** (`AccImportHostedService`). La pagina struttura è ora **`/vsop/admin/sectorstructure`** (redirect 301 da `/struttura`) e NON crea più ACC. Migrazione **`AddAccSector`**. Dettagli sotto.

> 🗺️ **Round 12 — Rebuild pagine `/vsop`.** Prefisso rotte `/sop`→`/vsop` (redirect 301 dai vecchi URL); Home e Landing ACC snellite; aeroporti su `/vsop/{acc}/airports` (elenco/doc con `?icao=`) e APP su `/vsop/{acc}/apps` + `/apps/vipi`; "3 in evidenza" (`FeaturedRank`) scelti dall'editor ACC. Pagine fuori scope **disabilitate, non cancellate** (Ridotta/Ridotta-APP/AoR3D/Export/vLOA/Stati). **Fonte di verità rapida: `MAPPA_PAGINE.md` + `PAGINE_DISABILITATE.md`.** Editor non toccati (prossimo giro).

> 🔀 **Round 5 — Fusione Settore/Posizione.** `Position` e `Sector` sono ora **un'unica entità `Sector`** (callsign apribile + volume di spazio aereo); contenimento ad albero via `Sector.ParentSectorId` (sostituisce `HierarchyRelation`/`PositionSector`). Scope documenti **uno-a-molti** (`Sector.DocumentId` + `IsPrimary`): un documento descrive N settori, ogni settore ha un solo documento. Enum `PositionType`/`PositionKind` → `SectorType`/`SectorKind`. Migrazioni rigenerate da zero (greenfield). Dettagli in `SPEC_Modello_Dati.md` (banner Round 5).

---

## 🛰️ Round 13 — Rename Fir→Acc + import ACC/settori dalla sorgente ✅ COMPLETATO (28 giu, build verde, 111 test)

**1. Rename `Fir` → `Acc` (FIR sparito dal progetto).** Entità `Acc` (`Domain/Entities/Anagrafica.cs`), proprietà `AccId`/`AccCode`/`CountryPrefix`, nav `Acc`; servizi `ListAccsAsync`/`CreateAccAsync`/`EnsureCanEditAccAsync`…; `FirRow`→`AccRow`; claim `HostIdentityOptions.AccClaim`; DbSet/tabella `Accs`. Fatto con regex sicura (escluse `First`/`confirm`/`fire`, cartella Migrations esclusa). Migrazione **`RenameFirToAcc`** riscritta a `RenameTable`/`RenameColumn`/`RenameIndex` + `AddColumn` → **non distruttiva** (verificata via `migrations script`: `ALTER TABLE "Firs" RENAME TO "Accs"`).

**2. ACC = entità importata dalla sorgente** (non più creata a mano). Nuovi campi su `Acc`: `IsMilitary`, `IsHidden`, `ImportedAtUtc`. La pagina **`/vsop/admin/acc`** (`AccAdminPage`, admin) importa da sorgente e mostra la tabella ACC (Codice·Nome·Militare·Stato) con ricerca + mostra/nascondi. ACC nascosti esclusi dalla navigazione pubblica (`EfStationDirectory.ListAccs()` filtra `!IsHidden`).

**3. Settori ACC (subcenter).** Nuova entità **`AccSector`** (`ComposePosition` unique = chiave naturale; `CenterId` FK→`Acc.Code`; `Position`, `MiddleIdentifier`, `Frequency`, `RegionMapPolygon`, `LowerLimit`/`UpperLimit`, `IsHidden`, `ImportedAtUtc`). Migrazione **`AddAccSector`** (FK su `Acc.Code` come chiave alternata `AK_Accs_Code`). Seconda tabella in `/vsop/admin/acc` con ricerca + mostra/nascondi + **limiti quota editabili** dall'admin. **Limiti**: `GND`=0 (mostrato "GND"), `UNL`=`UpperLimit` null (illimitato); default **GND→UNL**, settori **`Position=FSS` → GND→19000**. I settori di un ACC nascosto sono **effettivamente nascosti** (derivato `IsHidden || Acc.IsHidden`, reversibile; toggle settore disabilitato finché l'ACC è nascosto).

**4. Sorgente (porta neutra, ADR-0006).** `IAccDirectory` (`Application/Abstractions/IAccDirectory.cs`): `GetCentersAsync` (`/v2/centers?page&countryId`) + `GetSubcentersAsync(icao)` (`/v2/centers/{icao}/subcenters` per la lista, poi `/v2/subcenters/{compose}` per freq+polygon). DTO `SourceCenter`/`SourceSubcenter` (con `LowerLimit`/`UpperLimit` **predisposti** ma oggi null: se la sorgente li esporrà, l'import li userà). Impl IVAO in `IvaoApiClient` con **parsing JSON tollerante** (array o `{items,pages}`, nomi campo alternativi); se 0 elementi → eccezione con risposta grezza (diagnostica). Opzioni in `IvaoOptions`: `CentersPath`, `SubcentersPathFormat`, `SubcenterDetailPathFormat`, `AccImportHours` (default 24).

**5. Service/repo.** `IAccAdminService`/`AccAdminService` (admin-gated) + `IAccAdminRepository`/`EfAccAdminRepository`: `ImportFromSourceAsync` (ACC poi, per ogni ACC, i subcenter), `ListAccsAsync`/`ListSubcentersAsync`, `SetHiddenAsync`/`SetSubcenterHiddenAsync`/`SetSubcenterLimitsAsync`. Upsert idempotente che **preserva `IsHidden` e i limiti admin** (aggiorna i limiti solo se la sorgente li fornisce; FSS senza superiore → 19000).

**6. Import automatico giornaliero.** `AccImportHostedService` (BackgroundService): primo run ~15s dopo l'avvio, poi ogni `AccImportHours` (24h). Job di sistema (usa direttamente porta+repo, **niente authz utente**); se mancano le credenziali sorgente salta in silenzio.

**7. Pagina struttura.** Rinominata rotta `/vsop/admin/struttura` → **`/vsop/admin/sectorstructure`** (redirect 301 in `Program.cs`). La sezione "crea/elimina ACC" è **rimossa**: resta solo il selettore ACC (pill) + link «Gestione ACC / Aeroporti / Sorgenti». Breadcrumb pagina ACC: `Home › Area AOD / DIR › Struttura › ACC` (come Aeroporti).

**Test** (`AccImportTests`, +5 → 111): dedup ACC per centerId, idempotenza + hide preservato, subcenter skip ACC ignoto + preserva limiti admin, default GND/UNL e FSS GND→19000, hide ACC ⇒ settori `AccHidden`, nav esclude ACC nascosti.

**Da fare al deploy:** riavviare il Host → applica `RenameFirToAcc` + `AddAccSector`. Poi `/vsop/admin/acc` → «Importa da sorgente» (serve `Ivao:ClientId/Secret` + scope `configuration`).

**Aperti / note:**
- Schema `/v2/centers` e `/v2/subcenters` **dedotto** (`composePosition`/`centerId`/`position`/`middleIdentifier`/`atcCallsign`/`military`/`frequency`/`regionMapPolygon`). `GetCentersAsync` funziona live; se i subcenter tornassero 0, allineare i nomi campo in `IvaoApiClient` su una risposta reale.
- Import **additivo** (non cancella ACC/settori spariti dalla sorgente: si nascondono).
- `RegionMapPolygon` salvato grezzo: non ancora collegato alla mappa AoR.
- Eventuale dato sporco: un import precedente (versione intermedia) poteva creare `Sector` con `Kind=Acc`; non più. Greenfield (`vipi.db`) consigliato in dev.

---

## 🗺️ Round 12 — Rebuild struttura pagine + prefisso `/vsop` ✅ COMPLETATO (27 giu, build verde, 106 test)

Piano: `C:\Users\cgran\.claude\plans\wiggly-toasting-muffin.md`. Indicazioni: `Indicazione/Indicazioni_rebuild.txt`.
**Leggi prima `MAPPA_PAGINE.md`** (gerarchia + tabella rotte) e **`PAGINE_DISABILITATE.md`** (pagine spente).

**1. Prefisso `/sop` → `/vsop`** su tutte le pagine tenute (rotte + href interni + SSE/health + URL generati in
`EfSearchRepository`/`EfChangesRepository` + `vipi-live.js`). **Redirect 301** `/sop*`→`/vsop*` (con query string)
in `Program.cs`. Corretto un bug latente in `SopLayout`/`AorBlock` (controllavano il segmento `"sop"`).

**2. Home `/vsop`** (`SopHome`): card ACC con **conteggio ATC online** (one-shot, primo token = codice ACC;
torri/APP aeroporto non ancora contate), una card "Cosa è cambiato", blocco staff (Bozze&Versioni · Tutte le
schermate · Struttura · Permessi) solo CH/AOD/DIR.

**3. Landing ACC `/vsop/{acc}`** (`AccLanding`): sezione **Documenti** = vIPI + **card Aeroporti** e **card APP**
coi **3 in evidenza** (titolo→elenco, voce→documento). **Rimossa** la sezione "Strumenti". Sezione **Admin invariata**.

**4. Aeroporti/APP:**
- `/vsop/{acc}/airports` (`AeroportoPage`): **una sola rotta** — senza `?icao=` mostra l'**elenco** della ACC,
  con `?icao=` il **documento** (ex `/aeroporto`, contenuto invariato).
- `/vsop/{acc}/apps` (`AppsListPage`, nuova) = elenco APP non remotizzati (`Sector` con `Type=App`).
- `/vsop/{acc}/apps/vipi?icao=` (`AppnPage`, ex `/app`) = documento APP (mockup, contenuto invariato).

**5. "3 in evidenza":** campo **`FeaturedRank`** (1..3) su `Airport` e `Sector` (migrazione **`AddFeaturedRank`**);
proiezioni `AirportRow`/`SectorRow` estese. Setter ACC-gated `IStructureEditingService.SetFeaturedAirportsAsync`/
`SetFeaturedAppsAsync` (+ repo). UI: pannello **"In evidenza nella landing ACC"** nell'editor vIPI ACC (`EditorPage`).
Fallback landing = primi 3 per ICAO/callsign.

**6. Pagine disabilitate** (`@page` → commento, codice intatto): `RidottaPage`, `RidottaAppPage`, `Aor3dPage`,
`ExportPage`, `VloaPage`, `StatiPage`. Link relativi rimossi (Strumenti, toggle Estesa/Ridotta nel chrome,
bottone Vista 3D in `AorBlock`, voci in `ScreensIndex`). Dettagli e modo di riattivarle in `PAGINE_DISABILITATE.md`.

**Aperti:** vLOA da ricollocare (i link search/changed verso `/vloa` ora vanno a 404); estendere il conteggio
online della Home a torri/APP; gli **editor** non sono stati rivisti (prossimo giro).

**Da fare al deploy:** riavviare il Host (applica `AddFeaturedRank`).

---

## 🔌 Round 11 — Indipendenza dalla sorgente + policy di import + `Vid`→`UserId` ✅ COMPLETATO (27 giu, build verde, 106 test)

Piano: `C:\Users\cgran\.claude\plans\atomic-stirring-nygaard.md`. Memoria: `source-decoupling-and-import-policy`.

**1. Indipendenza dalla sorgente esterna.** Le porte dati esterne sono **interfacce neutre** in `Application/Abstractions`: `IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory` (DTO **`Source*`**: `SourceAirport`/`SourceRunway`/`SourceAtcPosition`/`SourceUserStaff`). L'adapter IVAO concreto resta in `Infrastructure/Ivao/*` (mantiene i nomi `Ivao*`, è UNA implementazione). Nuovo **seam di selezione**: `DataSourceOptions` (`Infrastructure/DataSourceOptions.cs`) + sezione appsettings **`DataSource:Provider`** (oggi `"Ivao"`); `VipiModuleExtensions.AddVipiModule` fa branch sul provider (valore sconosciuto → eccezione di avvio). Domani per cambiare network o usare un DB interno basta un nuovo adapter. Metodi rinominati: `ReimportFromIvaoAsync`→`ReimportFromSourceAsync`, `MergeFromIvaoAsync`→`MergeFromSourceAsync`.

**2. `Vid`→`UserId` ovunque** (codice + colonne DB). Migrazione **`Rename_Vid_To_UserId`** (native `RENAME COLUMN`, incl. PK `StaffMembers`; verificata su copia DB = zero perdita dati). Rinominati: `Document.LockedByUserId`, `DocumentVersion.CreatedByUserId`, `EditGrant.UserId`/`GrantedByUserId`, `StaffMember.UserId` (PK), `AuditLog.UserId`, `CurrentUser.UserId`, `HostIdentityOptions.UserIdClaim` (valore default resta `"id"`). **Le label a video restano "VID"** (termine che i controllori usano): solo gli identificatori di codice sono `UserId`.

**3. Policy di import globale (opt-out).** Tutto ciò che la sorgente può fornire è **importato e in sola lettura**; il gestore esclude categorie per renderle manuali. Entità **`ImportPolicy`** (riga singola, default tutto `true`), enum `ImportCategory { TransitionAltitude, Atis, Runways, Sectors }`; store `IImportPolicyStore`/`EfImportPolicyStore`; servizio admin `IImportPolicyService`; migrazione **`AddImportPolicy`**. Enforcement a 3 livelli: **editor read-only + badge 🔒** (`AeroportoEditorPage`: TA e Ident/lunghezza/bearing pista), **guard nei service** (`AirportProfileService.SetTransitionAltitudeAsync`/`SaveRunwaysAsync` → `ValidationException`; `StructureEditingService.AddSectorAsync` kind=Airport), **import policy-aware** (`ReimportFromSourceAsync` salta le categorie escluse passando `null`/lista vuota al merge). Nuova pagina admin **`/sop/admin/sorgenti`** (`SorgentiAdminPage`, linkata da Struttura). Risolve i conflitti reali TA + ident pista (prima editabili, poi sovrascritti dal re-import). I campi editoriali (regole pista, SID, livelli TL, link freq, gerarchia settori) NON sono categorie: sempre dell'utente.

**Test** (`ImportPolicyTests`): store default+round-trip, guard TA (blocca default / passa se escluso), guard piste (rifiuta cambio ident/geometria, consente editoriale), reimport rispetta la policy. **106 totali.**

**Da fare al deploy:** riavviare il Host (applica `Rename_Vid_To_UserId` + `AddImportPolicy`). Nota cosmetica: il rename ha reso PascalCase alcuni parametri/locali (`int UserId`) — compila ed è coerente (i parametri posizionali dei record lo richiedono).

---

## 🗼 Round 10 — Torre informativa (I_TWR) + invariante torre + quote di transizione di default ✅ COMPLETATO (27 giu, build verde)

**I_TWR (AFIS).** Nuovo valore enum `SectorType.ITwr` (dopo `Twr`; enum salvati come stringa → nessuna migration). Trattato come torre dove conta (frequenza primaria ★, etichetta "Tower (informazioni)", scelta del settore primario): helper `IsTower()` in `EfAirportProfileRepository`. Nel dropdown Struttura appare come "I_TWR".

**Invariante "ogni aeroporto ha sempre una torre".** `AirportAdminRow.HasTower`: badge **⚠ no TWR** in gestione aeroporti; `EfStructureEditingRepository.DeleteSectorAsync` **blocca** la rimozione dell'unica TWR/I_TWR di un aeroporto. La risalita resta derivata dall'albero `ParentSectorId` (non duplicata sull'aeroporto). Test `Airport_Must_Keep_At_Least_One_Tower`.

**Quote di transizione di default** (`EfAirportProfileRepository`): tabella `TL = TA + margine` per fascia QNH — `<977→+2500`, `977–994→+2000`, `995–1012→+1500`, `≥1013→+1000` — arrotondata al FL superiore multiplo di 5 (es. TA 6000 → FL85/80/75/70). Se la TA è ignota mostra la formula `TA + N ft`. `DefaultTlBands` + `TransitionLevelFor`/`DefaultBandOffset`; `EnsureDefaultTransitionLevels` idempotente chiamato sia da `MergeFromSourceAsync` sia da `RebuildDocumentAsync` (garantisce la tabella anche su aeroporti generati senza import). Salvando la TA le righe di default si ricalcolano; le fasce personalizzate restano intatte. Test `Default_Transition_Levels_Follow_Ta`.

---

## 🧭 Round 9 — Regole pista temporali + multi-pista + L/R ✅ COMPLETATO (26 giu, build verde, 100 test)

**Cosa:** le regole di scelta pista (`AirportRunwayRule`) ora condizionano anche su **tempo**, l'editor usa **multi-select** delle piste reali (niente più CSV a mano) con **validazione**, e il fallback headwind distingue **DEP/ARR su piste parallele** (es. 35L arrivi / 35R partenze). Migrazione `AddRunwayRuleSchedule`.

**Modello** (`Domain/Entities/Anagrafica.cs` · enum `DateParity` in `Enums.cs`): su `AirportRunwayRule` aggiunti `TimeFromUtcMin`/`TimeToUtcMin` (minuti UTC, finestra con wrap notturno), `DaysOfWeekMask` (bit0=Lun…bit6=Dom, null/0=tutti), `DateParity` (`Any`/`Even`/`Odd` = parità giorno del **mese**, caso Malpensa pari/dispari). `RunwayRuleRow`/`RunwayRuleEval` estesi (parametri **opzionali in coda** → chiamate vecchie restano valide).

**Logica** (`Application/Weather/RunwaySuggestion.cs`):
- `EvaluateRules(...)` prende ora `DateTime? nowUtc` (default `UtcNow`); dopo i check vento/precip valuta orario/giorno-settimana/parità. **Sempre first-match** = regole esclusive per ordine.
- `Suggest(...)`: `RunwaySuggestionResult` espone `DepIdent`/`ArrIdent`. Se ci sono **parallele nello stesso heading** del vento → split (sinistra=arrivi, destra=partenze); altrimenti coincidono con `Best`.

**UI editor** (`AeroportoEditorPage.razor`): pannello "Regole piste" riscritto da tabella a **card**. DEP/ARR = **chip toggle** delle piste reali (`RwIdents()`); giorni = 7 chip bitmask; orario = `<input type="time">` (UTC); parità = select. Riordino ↑/↓ (l'ordine = priorità). **Validazione** `RuleIssues()`: errore se regola senza piste (blocca salvataggio); avvisi per pista inesistente, finestra orario incompleta, **catch-all non ultima** (regole successive irraggiungibili). CSS card in `vipi-theme.css`.

**Viewer** (`AeroportoPage.razor`): passa `DateTime.UtcNow` a `EvaluateRules`; fallback usa `DepIdent`/`ArrIdent`. Doc rebuild (`EfAirportProfileRepository.RuleCondition`) rende le condizioni temporali (es. "22:00–06:00Z, Lun/Mer, giorni pari").

**Test** (`WeatherParsingTests`): finestra oraria wrap, giorno-settimana, parità, split L/R, pista singola. **100 totali.**

**Follow-up:** parità su **settimana ISO** (oggi solo giorno del mese); DEP/ARR L/R configurabile (oggi convenzione sinistra=arrivi); preview "regola attiva ora" nell'editor.

---

## 🛠️ Round 8 — Profilo strutturato aeroporto + editor dedicato ✅ COMPLETATO (26 giu, build verde, 95 test)

**Cambio architetturale:** i dati dell'aeroporto (quote transizione, frequenze, piste, SID, **regole pista**)
non vivono più solo come JSON nei `ContentBlock`: sono **entità strutturate** (sorgente di verità) da cui si
**rigenera** il documento. L'editor edita le entità (atomico, ACC-gated, **no lock**); il documento resta la
proiezione per viewer/ricerca/PDF. Piano: `C:\Users\cgran\.claude\plans\ancient-waddling-wind.md`.

**Entità** (`Domain/Entities/Anagrafica.cs`, FK→Airport cascade, indice `(AirportId,Order)`):
`AirportTransitionLevel` (QnhFrom/To numerici + Level), `AirportRunway` (Ident/LengthM/Bearing IVAO +
ToraM/LdaM/AppProcedures/Patterns/Circling editoriali), `AirportRunwayRule` (WindDirFrom/To, WindSpeedMin/Max,
Rain?/Snow?, DepRunways/ArrRunways CSV, Note), `AirportSid`, `AirportFrequencyLink` (SourceFrequencyId →
riferimento vivo). Su `Airport`: **`TransitionAltitudeFt`** + **`AtisFrequency`**. Migrazione `AddAirportProfile`.
`IvaoRunway` ora espone `LengthM`/`Bearing` (era `Dimensions` stringa).

**Application:** `AirportProfileModels.cs` (record `*Row` + `AirportProfileData`), porta
`IAirportProfileRepository`, service `IAirportProfileService`/`AirportProfileService`
(`LoadForView` senza authz per il viewer · `LoadForEdit`/Save*/Reimport/Rebuild ACC-gated · `ListLinkableFrequencies`).
- `Weather/MetarParser.cs`: `ParsedMetar.HasRain/HasSnow` (da codici RA/DZ, SN/SG).
- `Weather/RunwaySuggestion.cs`: `EvaluateRules(rules, windDir, windKt, rain, snow)` → DEP/ARR (prima regola
  applicabile, arco vento con wrap); il viewer prova le regole, **fallback** a `Suggest` (headwind).

**Infrastructure:** `EfAirportProfileRepository` (load/save per-area = replace-list; `MergeFromIvaoAsync` upsert piste
per ident sovrascrivendo solo Length/Bearing + seed TL standard se vuote; **`RebuildDocumentAsync`** rigenera
*in-place* le sole sezioni gestite — `Regole piste/Quote di transizione/Frequenze/Piste/SID` — **preservando** le
altre, risolve i link al valore corrente). `EfStructureEditingRepository.GenerateAirportDocumentAsync` rimosso →
`EnsureAirportSectorsAsync`; l'orchestrazione (ensure-sectors → merge → rebuild) è in `StructureEditingService`.
Il bottone «📄 Genera documenti» di `AeroportiPage` ora **rigenera sempre** (niente più skip "già esistente").

**UI:** `AeroportoEditorPage` (`/sop/{acc}/aeroporto/editor?icao=`, InteractiveServer, ACC-gated) — 5 pannelli
(regole, quote+TA, frequenze+picker link, piste, SID) + «↻ Re-importa da IVAO» + «📄 Rigenera e pubblica».
Link «✎ Editor» da `AeroportoPage` (testata) e da `AeroportiPage` (riga). `AeroportoPage` (viewer): il widget pista
usa le **regole** (prevalgono) poi fallback headwind, mostra chip pioggia/neve; la sezione **Frequenze** è resa
custom dal profilo (link **vivi**); `QnhRowMatches` gestisce ≥/≤/>/< (TL range numerici → `≥/≤/–`).

**Test:** `WeatherParsingTests` (+rain/snow, +EvaluateRules wrap/no-match), `StructureEditingTests`
(ensure+merge+rebuild, re-import preserva editoriali, rebuild rende regole/link e preserva sezioni manuali).

**Follow-up:** import SID da sectorfile GitHub (merge con le manuali); DEP/ARR distinti L/R nel calcolo headwind;
toggle manuale pioggia/neve nel viewer (oggi solo da METAR live).

---

## 🛫 Round 6 — Aeroporti come entità + pagina dedicata ✅ COMPLETATO (26 giu, build verde, 80 test)

**Modello — entità `Airport`** (`Domain/Entities/Anagrafica.cs`): `Id, Icao(unico), Name, AccId→Acc, Sectors`. `Acc.Airports`. Su `Sector`: `AirportId?→Airport (OnDelete SetNull)`, `AirportIcao` **resta come denormalizzazione** (letta da `EfContentRepository`, `EfEditingRepository` fallback, ecc. — non rimuovere).
- ⚠️ **L'aeroporto NON ha gerarchia propria.** Il `ParentSectorId` su `Airport` (ex Round 6 intermedio) è stato **rimosso**: la gerarchia si **ricostruisce dai settori che puntano all'aeroporto** (`Sector.AirportId`). `Sector.ParentSectorId` (contenimento settori) è cosa diversa e resta.
- **Migrazioni:** `AddAirport`, `AddAirportParentSector`, `RemoveAirportParentSector` (in `Infrastructure/Persistence/Migrations`). Generate con `--startup-project src/Vipi.Infrastructure` (c'è `DesignTimeDbContextFactory`; Host non referenzia EF.Design). Applicate all'avvio dell'Host; per greenfield cancella `src/Vipi.Host/vipi.db*`.

**Anagrafica aeroporti IVAO** (`IIvaoAirportDirectory` / record `IvaoAirport(Icao,Name,AccCode,City)`; impl. in `IvaoApiClient`; cache di processo singleton `IvaoAirportCache`, TTL `Ivao:AirportsCacheHours`=12). Endpoint `/v2/airports?page=N&countryId=IT` — **scope `configuration`**. **`countryId=IT`**; paginato (`pages`, ~221 IT); ogni item ha **`centerId` = codice ACC di competenza** → `IvaoAirport.AccCode` (es. LIRF→LIRR; null per campi minori). Vedi memoria `ivao-api-app-token-limits`.

**Service/Repo** (`IStructureEditingService`/`EfStructureEditingRepository`):
- `CreateAirportAsync(accCode,icao,name)`, `DeleteAirportAsync` (blocca se settori puntano), `MoveAirportAsync(id,fromAcc,toAcc)` (sposta aeroporto + suoi settori, stacca i padri fuori ACC), `ListAllAirportsAsync()→AirportAdminRow(Id,Icao,Name,AccCode,Sectors)`, `ListAllSectorsAsync()→SectorBriefRow` (oggi non usato dalla pagina ma tenuto per la futura ricostruzione settori→aeroporto).
- **`AutoAssignKnownAirportsAsync()`** (admin): scarica la directory IVAO e delega a `EfStructureEditingRepository.AutoAssignAirportsAsync(candidates)` che crea in blocco gli aeroporti il cui `centerId` è una ACC esistente e l'ICAO è libero (verità esistenza nel DB, una `SaveChanges`); ritorna il conteggio creati. Test: `AutoAssign_Creates_Only_Known_Acc_And_Skips_Existing`.
- `AddSectorAsync` prende `int? airportId` (non più stringa ICAO); il campo aeroporto nel form settore compare **solo se Kind=Airport** (dropdown degli aeroporti della ACC).

**UI:**
- **`AeroportiPage`** (`/sop/admin/aeroporti`, admin) = **unico** punto di gestione aeroporti. Sinistra = assegnati (cambia ACC, rimuovi); destra = anagrafica IVAO (riga **verde** se già assegnato) con **«⟳ Auto-assegna noti»** e assegnazione **per-riga** (`Dictionary<string,string>` keyed per ICAO — niente più bug del select condiviso). **Ricerca** ICAO/Nome client-side su entrambe le tabelle. Stile brand (`doc-head`/`section-title`/`block`/`pill`).
- **`StrutturaPage`** (`/sop/admin/struttura`): **niente più gestione aeroporti** — solo ACC, settori, frequenze, documenti, + il dropdown aeroporto nel form settore (`Kind=Airport`, usa `_data.Airports`). Link «Gestione aeroporti →».
- **CSS:** controlli dentro `.struct .res-table` (select/input/btn + header) tematizzati in `vipi-theme.css`.

**Note operative:** `AeroportiPage` è admin-only e l'Host **non semina** (DB parte vuoto) → servono prima le ACC (poi «Auto-assegna noti» popola gli aeroporti di competenza). Credenziali IVAO dev in user-secrets (`Ivao:ClientId/Secret`) → la directory gira live; senza, le tabelle IVAO mostrano l'errore "non disponibile" (gestito).

**Follow-up aperti:** ricostruzione/visualizzazione gerarchia aeroporto **dai settori** (`Sector.AirportId`) — il dato c'è, manca la UI.

**Fix (26 giu):** `EfStructureEditingRepository.DeleteAccAsync` ora **elimina in cascata gli aeroporti** della ACC (FK `Sector.AirportId`=SetNull → sicuro); prima falliva con `FOREIGN KEY constraint failed` perché il guard controllava solo i settori. I settori restano un blocco esplicito (portano documenti).

---

## 🛬 Round 7 — Documento aeroporto da import IVAO ✅ + viewer widget ✅ (editor → vedi Round 8)

**FATTO (26 giu, build verde, 81 test):** bottone **«📄 Genera documenti»** in `AeroportiPage` (bulk bar assegnati). Per ogni aeroporto selezionato crea automaticamente settori + vIPI aeroporto **Published**.
- **Port `IIvaoAirportDetailProvider`** (Abstractions; record `IvaoAtcPosition`/`IvaoRunway`), impl in `IvaoApiClient`:
  - `/v2/airports/{ICAO}/ATCPositions` → campi reali `composePosition` (=callsign, **non** `atcCallsign` che è il nome), `position`, `frequency` (MHz). 
  - `/v2/airports/{ICAO}/runways` → `runway` ("RW06"→"06"), `length` in **piedi** → metri (×0.3048), `width`/`bearing`.
  - `transitionAltitude` aggiunto al record `IvaoAirport` (dall'anagrafica `/v2/airports`).
- **Settori d'aeroporto** creati da ATCPositions: **DEL/GND/TWR** (`Kind=Airport`, freq, contenimento DEL→GND→TWR). **APP rimandato** (`LIRN_US0_APP` ignorato — serve ragionamento dedicato). **ATIS** non è settore: solo la sua **frequenza** entra in tabella Frequenze.
- **Service** `StructureEditingService.GenerateAirportDocumentAsync(icao)` (admin): classifica per suffisso callsign, estrae freq ATIS + TA, delega al repo.
- **Repo** `EfStructureEditingRepository.GenerateAirportDocumentAsync(...)` + `DocBuilder`: crea i settori mancanti, costruisce il doc con le sezioni del mockup (**Quote di transizione · Frequenze · Piste · SID**; METAR/TAF restano **live**, non nel doc), aggancia il doc ai settori (TWR primario), pubblica. **Idempotente** (se l'aeroporto ha già un doc, salta); **fallback** `{ICAO}_TWR` se nessuna postazione. Test: `GenerateAirportDocument_...`.
- ⚠️ Le colonne editoriali Piste (TORA/LDA reali, APP procedures, Patterns, Circling) restano da completare a mano: IVAO dà solo ident + lunghezza.

**FATTO viewer (26 giu, build verde, 89 test) — widget mockup 3b in `AeroportoPage`:**
- **Parser METAR/TAF** in `Application/Weather/` (puro, testato): `MetarParser.ParseMetar/ParseTaf` + modelli `ParsedMetar`/`ParsedTaf`/`TafSegment`/`ParsedWind`/`CloudLayer`. Decodifica vento (VRB/calm/gust/MPS), visibilità (9999→">10 km", CAVOK, metri), nubi (FEW/SCT/BKN/OVC + base ft + CB/TCU), QNH (Q hPa **e** A inHg→hPa), temp/DP (M negativi), tempo presente→italiano, trend. TAF splittato in segmenti Base/BECMG/TEMPO/FM/PROB col periodo grezzo.
- **`RunwaySuggestion.Suggest(idents, windDir, windKt)`** (`Application/Weather/`): massimo componente di testa-vento; gestisce calmo/vento-in-coda con nota. Ident pista→heading = `num×10`.
- **`AeroportoPage` riscritta** (`@rendermode InteractiveServer`): **tab METAR/TAF** (riga raw `.metar` + griglia `.metar-parse`; TAF timeline `.taf-tl` con `taf-kind`/`taf-when`/`taf-raw`/parse per segmento). **Widget «pista suggerita dal vento»**: input °/kt (default dal METAR, bottone «↻ usa METAR») → card DEP/ARR + nota. Gli ident pista si estraggono dalla tabella «Piste» del doc (`ExtractRunways`, prima colonna del `BodyJson`). Sezioni doc rese sotto via `SectionNode`.
- **Badge TA + highlight QNH + `apt-2col`** (fatto): `AeroportoPage.SplitSections` separa la sezione «Quote di transizione» (resa custom: `.ta-badge` con TA estratta dalla prose via regex `Altitude…(\d{3,5})` + tabella `.tl-table` con riga `.tl-now`+tag «QNH attuale» quando il QNH del METAR ricade nell'intervallo — `QnhRowMatches` parsa "≥/</–") e «Frequenze» (resa con `SectionNode`), affiancate in `<div class="apt-2col">`. Piste/SID restano sotto (generiche).
- Test: `tests/Vipi.Application.Tests/WeatherParsingTests.cs` (8: METAR campi/gust/calm/inHg, TAF segmenti, pista headwind/calmo/coda). **89 test totali.**

**ANCORA DA FARE — viewer:**
- **Filtro SID per pista** (`.sid-bar`/`.sid-pill`): **rimandato** — dipende dal parsing sectorfile GitHub (ancora stub, §5 punto 2). La sezione SID resta il callout placeholder.
- **DEP/ARR distinti** per piste parallele (L/R): oggi il widget propone la stessa estremità per entrambi.

**Editor aeroporto:** ✅ FATTO in **Round 8** (profilo strutturato + `AeroportoEditorPage`). Vedi sezione Round 8 in cima.
- La vista si raggiunge da `/vsop/{acc}/airports?icao=LIRN` (Round 12; `BuildAirportVipiAsync`: doc Published con un settore `Kind=Airport` + `AirportIcao`).

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 128 test
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
- ⚠️ **AZIONE PENDENTE (Round 20):** **reset `src/Vipi.Host/vipi.db`** in dev (o applica la migrazione **`AddHierarchyParentCallsign`**, additiva) → riavvia il Host. Poi `/vsop/admin/acc` → «Importa da sorgente»: la **sync** popola automaticamente i `Sector` (CTR inclusi) dai cataloghi; in `/vsop/admin/sectorstructure` compare l'**albero di copertura globale** (cross-ACC). Il Host viene **fermato** a fine sessione (blocca le DLL in build).
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`). **Nessun seed**: si parte da DB **vuoto**. Flusso dati reale: `/vsop/admin/acc` importa ACC+settori dalla sorgente → la sync proietta i `Sector` → la **gerarchia** (padri per callsign) si imposta in `/vsop/admin/sectorstructure` → «Crea nuovo documento» (vIPI = N settori di scope, uno primario) → editor. **I settori NON si creano più a mano** (sono proiezione dei cataloghi, Round 20). Cancella `vipi.db*` per ripartire da zero. I `*Seed.cs` di Roma restano solo come fixture nei test.
- In dev l'utente è `DevCurrentUserProvider` (VID 704798, staff `IT-AOC` → **admin**, può tutto).
- Migrazioni: `dotnet ef migrations add <Nome> --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations`. ⚠️ Per i **rename** di proprietà/colonna EF scaffolda `RENAME COLUMN` solo se i campi combaciano: **verificare a mano** la migrazione generata (no Drop+Add che perde dati).

## 3. Mappa documenti
| File | Contenuto | Stato |
|---|---|---|
| `README.md` | **Stato del codice** (architettura, capability, prossimi passi). | ⭐ fonte verità codice |
| `HANDOFF.md` | Questo file: contesto per riprendere. | ⭐ leggere per primo |
| `MAPPA_PAGINE.md` | **Gerarchia pagine `/vsop`** + tabella rotte→file→accesso. | ⭐ mappa rapida (Round 12) |
| `PAGINE_DISABILITATE.md` | Pagine spente (rotta rimossa, codice intatto) + come riattivarle. | Round 12 |
| `PIANO_vIPI_Tool.md` | Piano/architettura di design. | design ref (vedi banner) |
| `SPEC_Modello_Dati.md` | Schema dati di design. | design ref (entità aggiornate, vedi §5) |
| `SPEC_Logica_AoR.md` | Logica visibilità + scenari S1–S10. | design ref; **S1–S10 implementati+testati** |
| `REVIEW_Flusso_e_Gap.md` | Confronto flusso vs documenti. | storico |
| `docs/CONFIG.md` | **Riferimento configurazione** completo (Division/**DataSource**/Ivao/Auth/secrets/**policy import**). | ⭐ config |
| `docs/INTEGRATION.md` | Come agganciare il modulo a un sito host (claim, DataSource, deploy). | ⭐ integrazione |
| `docs/adr/ADR-0001/0002` | Decisioni fondanti + integrazione/auth (ADR-0002 aggiornato: `UserId`). | valide |
| `docs/adr/ADR-0003` | Trasporto live = **SSE** (F3). | valida |
| `docs/adr/ADR-0004` | Configurazione divisione + codici admin. | valida |
| `docs/adr/ADR-0005` | Superficie del modulo e isolamento. | valida |
| `docs/adr/ADR-0006` | **Indipendenza sorgente dati + policy import** (Round 11). | ⭐ nuova |
| `mockups/vipi-ui-mockup-v2.html` | Mockup canonico 17 schermate. | storico/riferimento UI |
| `Esempi documenti/*.docx` | Esempi reali (riferimento contenuto, non importati). | riferimento |

---

## 4. STATO CODICE — cosa è implementato (e dove)

**Solution (Clean Architecture, net8.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

**Cuore AoR/visibilità (✅ testato S1–S10):** `Application/Aor/AorService.cs` (ownership/stato settori, top-down, unificazioni), `Topology.cs`, `Infrastructure/Aor/TopologyBuilder.cs` (implementa la porta `ITopologyProvider`). Tabella di verità visibilità in `Application/Content/ContentService.cs`.

**Consultazione dal DB (✅):** pipeline `IContentRepository` → `IVipiViewService` → `SectionNode`/`BlockRenderer`. Rotte sotto `/sop`:
- `/{acc}/vipi` (Estesa ACC) · `/{acc}/ridotta` (proiezione tier Reduced + sezione Trasferimenti) · `/{acc}/aeroporto` (vIPI aeroporto LIRF) · `/{acc}/vloa` (LIRR↔DTTC).
- `/search` (ricerca full-text reale), `/changed` (cosa è cambiato nel ciclo AIRAC), `/{acc}/export` (Estesa → stampa/PDF browser).
- Stub dichiarati: METAR/TAF, SID, mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico).

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}` per qualunque documento), `VersioniPage` (`/sop/versioni`).
- Editor specializzati: `TopologiaPage` (simulatore live riusa `IAorService` + CRUD regole/gerarchia), `XferEditorPage` (trasferimenti), `VloaEditorPage` (redirect all'editor generico).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position derivati dal **codice divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes` (pattern completi). **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code` (prefisso staff + id API membri), `IcaoPrefixes` (filtro ATC online), `AdminRolePatterns`. Per IT→DE basta la sezione `Division` in appsettings. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant; copre tutti i tipi (vIPI/aeroporto/vLOA/topologia/trasferimenti). Schermata `/sop/admin/permessi` (solo admin): aggiungi/revoca per VID manuale.
- **Lock** documento esclusivo (30 min sliding, acquisizione atomica via `ExecuteUpdateAsync`, release su publish/abbandono, **force admin**) → impedisce editing concorrente. `EditConflictException`.
- **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`) → conflitto gestito.
- **Validazione**: `UnificationRule` hard (sectorKey/callsign devono esistere), trasferimenti soft (catena non vuota/no duplicati).
- Verifiche **sempre server-side**. Security review fatta: **XSS in `AorBlock` corretto** (SVG hand-built ora HTML-encoded).

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; migrazioni (greenfield da Round 5, **lista autoritativa = SPEC §9.8**): `InitialCreate` → `AddAirport` → `AddAirportParentSector` → `RemoveAirportParentSector` → `AddAirportProfile` → `AddRunwayRuleSchedule` → `Rename_Vid_To_UserId` → `AddImportPolicy` → `AddFeaturedRank` → `AddVloaFeaturedRank` → `RenameFirToAcc` → `AddAccSector` → `AddAirportSector` → `AddAirportSectorPrimary` → `SimplifyDataModel` → `DropFrequencyTable` → `AddAirportHidden` → `RunwayRuleThresholds` → `AddRunwayRuleDateWindow` → `RenameRunwayRuleTimeToLocal` → `AddAirportExtraSection` → **`AddHierarchyParentCallsign`** (round 20). Seed (solo fixture di test, **non** seminato all'avvio): `RomaStructureSeed`, `RomaContentSeed`, `RomaAirportSeed`, `RomaVloaSeed`, `RomaTransferSeed`. ⚠️ **In produzione i `Sector` sono una proiezione dei cataloghi** (round 20): non si creano a mano, vedi SPEC §9.12.

**Modello dati — aggiunte rispetto a SPEC §3:** `Transfer` (+enum `TransferPhase`; catena handler = array JSON), `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅ F3):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, evento `Changed`, impl. `IOnlineAtcProvider`), `IvaoApiClient` (typed HttpClient → `/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, serve solo per i membri divisione: il tracker è pubblico), `AtcPollingHostedService` (`BackgroundService`, 60s), `IvaoOptions` (sezione `Ivao`), DI via `AddVipiIvao(config)`. Transport **SSE** `/sop/live/atc` (`Program.cs`) + `Vipi.Ui/wwwroot/vipi-live.js` (`EventSource`→JS interop). `VipiViewService` calcola AoR reale quando `live=true`. `RidottaPage` ora `InteractiveServer` (selettore P, badge, online-nel-dominio, refresh SSE). `TransferOnlineResolver` + `ITransferService.ListResolvedByAccAsync` (primo-online). `IDivisionMembersProvider` per dropdown CH in `AdminGrantsPage`. Decisione in **ADR-0003**.

**Note implementative / hardening F3:** SSE con `DisableBuffering()` (consegna immediata dietro proxy). `UseHttpsRedirection` solo in prod (in dev l'host è http → niente warning). `TransferOnlineResolver`: match esatto/segmento + sottostringa **solo per token ≥4 char** (evita falsi positivi su token corti). "Online nel mio dominio" ha empty-state esplicito ("copri tutti i settori"). Cache vuota prima del primo poll = `OnlineAtcSnapshot.Empty` (viste sicure).

**Indipendenza dalla sorgente (✅ Round 11, ADR-0006):** le porte dati esterne sono **neutre** — `IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IOnlineAtcProvider` (DTO `Source*`). L'adapter IVAO (`Infrastructure/Ivao/IvaoApiClient` ecc.) è UNA implementazione, selezionata da **`DataSource:Provider`** (`DataSourceOptions`; branch in `AddVipiModule`). Per cambiare network/usare un DB interno: nuovo adapter + un branch, senza toccare Application/UI. `Vid`→`UserId` ovunque (codice + DB, migr. `Rename_Vid_To_UserId`); a video resta "VID".

**Policy di import (✅ Round 11):** entità `ImportPolicy` (riga unica, default tutto importato/bloccato), `IImportPolicyStore`/`EfImportPolicyStore`, servizio admin `IImportPolicyService`, pagina **`/sop/admin/sorgenti`**. Categorie `ImportCategory { TransitionAltitude, Atis, Runways, Sectors }`. Enforcement: editor read-only+badge (`AeroportoEditorPage`), guard nei service (`AirportProfileService.SetTransitionAltitudeAsync`/`SaveRunwaysAsync`, `StructureEditingService.AddSectorAsync`), reimport che salta le categorie escluse. I campi editoriali non sono categorie.

**Aeroporti — torre & transizione (✅ Round 10):** `SectorType.ITwr` (torre informativa AFIS, trattata come torre via `IsTower()`); invariante «almeno una torre per aeroporto» (badge ⚠ no TWR + blocco delete unica TWR/I_TWR); tabella **TL di default** `TA + margine` per fascia QNH (`EnsureDefaultTransitionLevels`, idempotente, chiamata da merge e rebuild).

---

## 5. PROSSIMI PASSI (ordinati per valore)

1. **✅ Polling IVAO (F3) — FATTO.** Rifiniture aperte:
   - **Identità "P"**: oggi selettore manuale in Ridotta (default prima radice). Va legato al **callsign connesso del CH loggato** (richiede che `ICurrentUserProvider` esponga il callsign).
   - **Mapping token-handler → callsign** trasferimenti: oggi euristica match-segmento (`WS2`↔`LIMM_WS2_CTR`). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare; il `rating` non è nel summary tracker.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali (placeholder dichiarati):** ✅ **METAR/TAF FATTO** (NOAA aviationweather.gov, `IWeatherProvider`/`NoaaWeatherClient`, reso in `AeroportoPage`, cache TTL — sezione `Weather`). Restano: shape AoR (GeoJSON/WKT — ADR formato), SID + minime MVA (parsing **sectorfile GitHub**), AoR 3D (Three.js).
   - **Authoring da zero (FATTO):** DB parte vuoto. ACC/settori si **importano** dalla sorgente (`/vsop/admin/acc`) → la **sync** proietta i `Sector` (Round 20); la **gerarchia** (padri per callsign, cross-ACC) si imposta in `/vsop/admin/sectorstructure`; `EditingService.CreateDocumentAsync` (vIPI/vLOA vuoti) con entry "Crea nuovo documento". `StationResolver` DB-driven (`IStationDirectory`/`EfStationDirectory`). Token IVAO divisione: solo config (`Ivao:ClientId/Secret`, vedi `docs/CONFIG.md`).
   - **Fonte unica (Round 20):** doc+AoR girano ancora sui `Sector` (ora proiezione dei cataloghi), non direttamente sui cataloghi. **Follow-up:** eliminazione totale di `Sector` (doc+AoR sui cataloghi per callsign) + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`). SPEC §9.12.
3. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (scenari A/B, claim del sito `Ivao.It`) e OIDC (scenario C); mappare gli **staff code reali** (vedi §6 nodo aperto). Integrazione: montare la RCL nel sito host.
4. **Copertura/rifiniture:** seed altre ACC (LIMM/LIPP/LIBB), viewer **audit log**, "scarta bozza", editor visuale mappe AoR (oggi JSON grezzo), test property-based AoR, **rifinitura UI** (rimandata di proposito finché il live non gira).
5. **Deploy Round 20 (PENDENTE):** reset `vipi.db` in dev (o applica `AddHierarchyParentCallsign`) → riavvia il Host → `/vsop/admin/acc` importa (la sync popola i `Sector`) → verifica l'albero globale in `/vsop/admin/sectorstructure`.
6. **Housekeeping:** **niente è committato** (tutto in working tree) — valutare commit logici: decoupling sorgente · policy import · fonte unica/proiezione settori (Round 20) · doc.

---

## 6. Nodi aperti / decisioni
**Risolte in questa sessione:** modello editing persistente; modello autorizzazione (admin via staff code + grant per-ACC); lock esclusivo 30 min + force admin; validazione hard regole/soft trasferimenti; export = stampa browser; "cosa è cambiato" = lista+note+conteggi; catena handler trasferimenti = array JSON.

**Risolte F3 (sessione 23 giu):** trasporto live = **SSE** (ADR-0003); polling cache singleton 60s; token solo per membri divisione (tracker pubblico).

**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`), da confermare col sito host. Il codice "CH" non è gate: i permessi passano **solo** dai grant per-ACC; l'auto-elenco CH popola il dropdown via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/CONFIG.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **ACC pilota:** Roma (LIRR). Validare su una sola ACC prima di estendere.
- **Brand:** palette §15.1 PIANO (blu `#0D2C99`…), font Nunito Sans + Poppins; tema in `Vipi.Ui/wwwroot/vipi-theme.css` (contiene anche le regole `@media print`).
- **Parte più rischiosa:** logica AoR/visibilità → già coperta da test S1–S10; mantenerla testata ad ogni modifica.
- **Pagine interattive** usano `@rendermode InteractiveServer` (editor, topologia, trasferimenti, ricerca, changed, admin permessi).
- **Sicurezza:** ogni nuova operazione di scrittura deve passare per i service Application (guardia authz + lock), mai bypassare dal repo/UI.
- **Sorgente dati (ADR-0006):** non reintrodurre nomi IVAO in Application/UI — usa le porte neutre (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`); l'adapter IVAO resta in `Infrastructure/Ivao/*`, selezionato da `DataSource:Provider`.
- **VID vs UserId:** nel **codice** è `UserId` (campi, DTO, colonne); a **video** resta "VID" (termine d'uso). Non rinominare le label.
- **Dati di sorgente = sola lettura:** se aggiungi un campo che la sorgente può fornire, trattalo come categoria `ImportPolicy` e rispetta la policy nei punti di scrittura/import (vedi `source-decoupling-and-import-policy` in memoria).

---

## 8. Mockup v2 — storico UI (sessioni 19–20 giu)
Il mockup `mockups/vipi-ui-mockup-v2.html` (17 schermate) resta il riferimento visivo. Le schermate sono state derivate in componenti Blazor reali (vedi §4). Note: SCCAM e Aree regolamentate sono sezioni top-level; la vLOA ha due AoR e due tabelle frequenze; gli APP non remotizzati separano i trasferimenti verso ACC e verso torre. L'interattività del mockup era simulata; ora i dati vengono dal DB (tranne gli stub di §5 punto 2 — live/meteo/sectorfile/3D).
