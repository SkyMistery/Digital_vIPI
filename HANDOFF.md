# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 27 giugno 2026
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato:** progetto **in sviluppo attivo**. Design UI completo (mockup v2, 17 schermate) **e** codice avanzato: solution .NET 8 a 4 layer + Host Blazor Server, **test verdi (106)**, consultazione+editing+sicurezza funzionanti dal DB. **Live IVAO (F3) implementato**: polling + cache + SSE, Ridotta live (AoR reattivo, "primo online", online nel dominio), auto-elenco CH permessi. **Sorgente dati esterna disaccoppiata** (interfacce neutre + `DataSource:Provider`) e **policy di import opt-out** (dati di sorgente in sola lettura). **Struttura pagine rifatta su prefisso `/vsop`** (Round 12): vedi `MAPPA_PAGINE.md`.

> 🗺️ **Round 12 — Rebuild pagine `/vsop`.** Prefisso rotte `/sop`→`/vsop` (redirect 301 dai vecchi URL); Home e Landing ACC snellite; aeroporti su `/vsop/{acc}/airports` (elenco/doc con `?icao=`) e APP su `/vsop/{acc}/apps` + `/apps/vipi`; "3 in evidenza" (`FeaturedRank`) scelti dall'editor ACC. Pagine fuori scope **disabilitate, non cancellate** (Ridotta/Ridotta-APP/AoR3D/Export/vLOA/Stati). **Fonte di verità rapida: `MAPPA_PAGINE.md` + `PAGINE_DISABILITATE.md`.** Editor non toccati (prossimo giro).

> 🔀 **Round 5 — Fusione Settore/Posizione.** `Position` e `Sector` sono ora **un'unica entità `Sector`** (callsign apribile + volume di spazio aereo); contenimento ad albero via `Sector.ParentSectorId` (sostituisce `HierarchyRelation`/`PositionSector`). Scope documenti **uno-a-molti** (`Sector.DocumentId` + `IsPrimary`): un documento descrive N settori, ogni settore ha un solo documento. Enum `PositionType`/`PositionKind` → `SectorType`/`SectorKind`. Migrazioni rigenerate da zero (greenfield). Dettagli in `SPEC_Modello_Dati.md` (banner Round 5).

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
- `/vsop/{acc}/airports` (`AeroportoPage`): **una sola rotta** — senza `?icao=` mostra l'**elenco** della FIR,
  con `?icao=` il **documento** (ex `/aeroporto`, contenuto invariato).
- `/vsop/{acc}/apps` (`AppsListPage`, nuova) = elenco APP non remotizzati (`Sector` con `Type=App`).
- `/vsop/{acc}/apps/vipi?icao=` (`AppnPage`, ex `/app`) = documento APP (mockup, contenuto invariato).

**5. "3 in evidenza":** campo **`FeaturedRank`** (1..3) su `Airport` e `Sector` (migrazione **`AddFeaturedRank`**);
proiezioni `AirportRow`/`SectorRow` estese. Setter FIR-gated `IStructureEditingService.SetFeaturedAirportsAsync`/
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
**rigenera** il documento. L'editor edita le entità (atomico, FIR-gated, **no lock**); il documento resta la
proiezione per viewer/ricerca/PDF. Piano: `C:\Users\cgran\.claude\plans\ancient-waddling-wind.md`.

**Entità** (`Domain/Entities/Anagrafica.cs`, FK→Airport cascade, indice `(AirportId,Order)`):
`AirportTransitionLevel` (QnhFrom/To numerici + Level), `AirportRunway` (Ident/LengthM/Bearing IVAO +
ToraM/LdaM/AppProcedures/Patterns/Circling editoriali), `AirportRunwayRule` (WindDirFrom/To, WindSpeedMin/Max,
Rain?/Snow?, DepRunways/ArrRunways CSV, Note), `AirportSid`, `AirportFrequencyLink` (SourceFrequencyId →
riferimento vivo). Su `Airport`: **`TransitionAltitudeFt`** + **`AtisFrequency`**. Migrazione `AddAirportProfile`.
`IvaoRunway` ora espone `LengthM`/`Bearing` (era `Dimensions` stringa).

**Application:** `AirportProfileModels.cs` (record `*Row` + `AirportProfileData`), porta
`IAirportProfileRepository`, service `IAirportProfileService`/`AirportProfileService`
(`LoadForView` senza authz per il viewer · `LoadForEdit`/Save*/Reimport/Rebuild FIR-gated · `ListLinkableFrequencies`).
- `Weather/MetarParser.cs`: `ParsedMetar.HasRain/HasSnow` (da codici RA/DZ, SN/SG).
- `Weather/RunwaySuggestion.cs`: `EvaluateRules(rules, windDir, windKt, rain, snow)` → DEP/ARR (prima regola
  applicabile, arco vento con wrap); il viewer prova le regole, **fallback** a `Suggest` (headwind).

**Infrastructure:** `EfAirportProfileRepository` (load/save per-area = replace-list; `MergeFromIvaoAsync` upsert piste
per ident sovrascrivendo solo Length/Bearing + seed TL standard se vuote; **`RebuildDocumentAsync`** rigenera
*in-place* le sole sezioni gestite — `Regole piste/Quote di transizione/Frequenze/Piste/SID` — **preservando** le
altre, risolve i link al valore corrente). `EfStructureEditingRepository.GenerateAirportDocumentAsync` rimosso →
`EnsureAirportSectorsAsync`; l'orchestrazione (ensure-sectors → merge → rebuild) è in `StructureEditingService`.
Il bottone «📄 Genera documenti» di `AeroportiPage` ora **rigenera sempre** (niente più skip "già esistente").

**UI:** `AeroportoEditorPage` (`/sop/{acc}/aeroporto/editor?icao=`, InteractiveServer, FIR-gated) — 5 pannelli
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

**Modello — entità `Airport`** (`Domain/Entities/Anagrafica.cs`): `Id, Icao(unico), Name, FirId→Fir, Sectors`. `Fir.Airports`. Su `Sector`: `AirportId?→Airport (OnDelete SetNull)`, `AirportIcao` **resta come denormalizzazione** (letta da `EfContentRepository`, `EfEditingRepository` fallback, ecc. — non rimuovere).
- ⚠️ **L'aeroporto NON ha gerarchia propria.** Il `ParentSectorId` su `Airport` (ex Round 6 intermedio) è stato **rimosso**: la gerarchia si **ricostruisce dai settori che puntano all'aeroporto** (`Sector.AirportId`). `Sector.ParentSectorId` (contenimento settori) è cosa diversa e resta.
- **Migrazioni:** `AddAirport`, `AddAirportParentSector`, `RemoveAirportParentSector` (in `Infrastructure/Persistence/Migrations`). Generate con `--startup-project src/Vipi.Infrastructure` (c'è `DesignTimeDbContextFactory`; Host non referenzia EF.Design). Applicate all'avvio dell'Host; per greenfield cancella `src/Vipi.Host/vipi.db*`.

**Anagrafica aeroporti IVAO** (`IIvaoAirportDirectory` / record `IvaoAirport(Icao,Name,FirCode,City)`; impl. in `IvaoApiClient`; cache di processo singleton `IvaoAirportCache`, TTL `Ivao:AirportsCacheHours`=12). Endpoint `/v2/airports?page=N&countryId=IT` — **scope `configuration`**. **`countryId=IT`**; paginato (`pages`, ~221 IT); ogni item ha **`centerId` = codice FIR di competenza** → `IvaoAirport.FirCode` (es. LIRF→LIRR; null per campi minori). Vedi memoria `ivao-api-app-token-limits`.

**Service/Repo** (`IStructureEditingService`/`EfStructureEditingRepository`):
- `CreateAirportAsync(firCode,icao,name)`, `DeleteAirportAsync` (blocca se settori puntano), `MoveAirportAsync(id,fromFir,toFir)` (sposta aeroporto + suoi settori, stacca i padri fuori FIR), `ListAllAirportsAsync()→AirportAdminRow(Id,Icao,Name,FirCode,Sectors)`, `ListAllSectorsAsync()→SectorBriefRow` (oggi non usato dalla pagina ma tenuto per la futura ricostruzione settori→aeroporto).
- **`AutoAssignKnownAirportsAsync()`** (admin): scarica la directory IVAO e delega a `EfStructureEditingRepository.AutoAssignAirportsAsync(candidates)` che crea in blocco gli aeroporti il cui `centerId` è una FIR esistente e l'ICAO è libero (verità esistenza nel DB, una `SaveChanges`); ritorna il conteggio creati. Test: `AutoAssign_Creates_Only_Known_Fir_And_Skips_Existing`.
- `AddSectorAsync` prende `int? airportId` (non più stringa ICAO); il campo aeroporto nel form settore compare **solo se Kind=Airport** (dropdown degli aeroporti della FIR).

**UI:**
- **`AeroportiPage`** (`/sop/admin/aeroporti`, admin) = **unico** punto di gestione aeroporti. Sinistra = assegnati (cambia FIR, rimuovi); destra = anagrafica IVAO (riga **verde** se già assegnato) con **«⟳ Auto-assegna noti»** e assegnazione **per-riga** (`Dictionary<string,string>` keyed per ICAO — niente più bug del select condiviso). **Ricerca** ICAO/Nome client-side su entrambe le tabelle. Stile brand (`doc-head`/`section-title`/`block`/`pill`).
- **`StrutturaPage`** (`/sop/admin/struttura`): **niente più gestione aeroporti** — solo FIR, settori, frequenze, documenti, + il dropdown aeroporto nel form settore (`Kind=Airport`, usa `_data.Airports`). Link «Gestione aeroporti →».
- **CSS:** controlli dentro `.struct .res-table` (select/input/btn + header) tematizzati in `vipi-theme.css`.

**Note operative:** `AeroportiPage` è admin-only e l'Host **non semina** (DB parte vuoto) → servono prima le FIR (poi «Auto-assegna noti» popola gli aeroporti di competenza). Credenziali IVAO dev in user-secrets (`Ivao:ClientId/Secret`) → la directory gira live; senza, le tabelle IVAO mostrano l'errore "non disponibile" (gestito).

**Follow-up aperti:** ricostruzione/visualizzazione gerarchia aeroporto **dai settori** (`Sector.AirportId`) — il dato c'è, manca la UI.

**Fix (26 giu):** `EfStructureEditingRepository.DeleteFirAsync` ora **elimina in cascata gli aeroporti** della FIR (FK `Sector.AirportId`=SetNull → sicuro); prima falliva con `FOREIGN KEY constraint failed` perché il guard controllava solo i settori. I settori restano un blocco esplicito (portano documenti).

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
dotnet test  Vipi.slnx          # 106 test
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
- ⚠️ **AZIONE PENDENTE (Round 10/11/12):** **riavviare il Host** per applicare le migrazioni nuove `Rename_Vid_To_UserId` + `AddImportPolicy` + **`AddFeaturedRank`** sul `vipi.db` esistente (le prime due verificate su copia, zero perdita dati; la terza aggiunge solo colonne nullable). Il Host era in esecuzione ed è stato **fermato** alla chiusura di questa sessione (bloccava le DLL in build). Inoltre, per veder comparire le **quote di transizione di default**, rigenerare i documenti aeroporto (LIRN e gli altri già generati hanno la tabella TL vuota): editor aeroporto → opz. «Salva TA» → «📄 Rigenera e pubblica» (oppure «Genera documenti» da `/vsop/admin/aeroporti`).
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`). **Nessun seed**: si parte da DB **vuoto**; i dati reali si inseriscono dall'app — `/sop/admin/struttura` (FIR, settori con contenimento padre, frequenze) + "Crea nuovo documento" (vIPI = N settori di scope, uno primario) → editor. Cancella `vipi.db*` per ripartire da zero (schema cambiato in Round 5 e poi in **Round 8/9/10/11**: migrazioni `AddAirportProfile`, `AddRunwayRuleSchedule`, `Rename_Vid_To_UserId`, `AddImportPolicy`). I `*Seed.cs` di Roma restano solo come riferimento/uso nei test.
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
- **Grant per-FIR** (`EditGrant`, VID→FIR): chi non è admin edita una FIR solo con grant; copre tutti i tipi (vIPI/aeroporto/vLOA/topologia/trasferimenti). Schermata `/sop/admin/permessi` (solo admin): aggiungi/revoca per VID manuale.
- **Lock** documento esclusivo (30 min sliding, acquisizione atomica via `ExecuteUpdateAsync`, release su publish/abbandono, **force admin**) → impedisce editing concorrente. `EditConflictException`.
- **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`) → conflitto gestito.
- **Validazione**: `UnificationRule` hard (sectorKey/callsign devono esistere), trasferimenti soft (catena non vuota/no duplicati).
- Verifiche **sempre server-side**. Security review fatta: **XSS in `AorBlock` corretto** (SVG hand-built ora HTML-encoded).

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; migrazioni (greenfield da Round 5): `InitialCreate` → `AddAirport` → `AddAirportParentSector` → `RemoveAirportParentSector` → `AddAirportProfile` → `AddRunwayRuleSchedule` → `Rename_Vid_To_UserId` → `AddImportPolicy`. Seed: `RomaStructureSeed` (anagrafica/gerarchia/regole), `RomaContentSeed` (vIPI ACC), `RomaAirportSeed` (LIRF), `RomaVloaSeed` (vLOA + FIR/posizione DTTC), `RomaTransferSeed`.

**Modello dati — aggiunte rispetto a SPEC §3:** `Transfer` (+enum `TransferPhase`; catena handler = array JSON), `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅ F3):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, evento `Changed`, impl. `IOnlineAtcProvider`), `IvaoApiClient` (typed HttpClient → `/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, serve solo per i membri divisione: il tracker è pubblico), `AtcPollingHostedService` (`BackgroundService`, 60s), `IvaoOptions` (sezione `Ivao`), DI via `AddVipiIvao(config)`. Transport **SSE** `/sop/live/atc` (`Program.cs`) + `Vipi.Ui/wwwroot/vipi-live.js` (`EventSource`→JS interop). `VipiViewService` calcola AoR reale quando `live=true`. `RidottaPage` ora `InteractiveServer` (selettore P, badge, online-nel-dominio, refresh SSE). `TransferOnlineResolver` + `ITransferService.ListResolvedByFirAsync` (primo-online). `IDivisionMembersProvider` per dropdown CH in `AdminGrantsPage`. Decisione in **ADR-0003**.

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
   - **Authoring da zero (FATTO):** DB parte vuoto. `IStructureEditingService`/`EfStructureEditingRepository` (FIR/posizioni/settori/ownership/frequenze) + `StrutturaPage` (`/sop/admin/struttura`); `EditingService.CreateDocumentAsync` (vIPI/vLOA vuoti) con entry "Crea nuovo documento". `StationResolver` ora DB-driven (`IStationDirectory`/`EfStationDirectory`). Token IVAO divisione: solo config (`Ivao:ClientId/Secret`, vedi `docs/CONFIG.md`).
3. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (scenari A/B, claim del sito `Ivao.It`) e OIDC (scenario C); mappare gli **staff code reali** (vedi §6 nodo aperto). Integrazione: montare la RCL nel sito host.
4. **Copertura/rifiniture:** seed altre FIR (LIMM/LIPP/LIBB), viewer **audit log**, "scarta bozza", editor visuale mappe AoR (oggi JSON grezzo), test property-based AoR, **rifinitura UI** (rimandata di proposito finché il live non gira).
5. **Deploy Round 10/11/12 (PENDENTE):** riavviare il Host (applica `Rename_Vid_To_UserId` + `AddImportPolicy` + `AddFeaturedRank`); poi **rigenerare i documenti aeroporto** già creati (LIRN ecc.) per popolare le quote di transizione di default. Provare `/vsop/admin/sorgenti` (toggle categorie → editor read-only/manuale) e i "3 in evidenza" dall'editor vIPI ACC.
6. **Housekeeping:** **niente è committato** (tutto in working tree, incl. Round 10/11 + doc aggiornati) — valutare commit logici: decoupling sorgente · policy import · I_TWR+quote transizione · doc.

---

## 6. Nodi aperti / decisioni
**Risolte in questa sessione:** modello editing persistente; modello autorizzazione (admin via staff code + grant per-FIR); lock esclusivo 30 min + force admin; validazione hard regole/soft trasferimenti; export = stampa browser; "cosa è cambiato" = lista+note+conteggi; catena handler trasferimenti = array JSON.

**Risolte F3 (sessione 23 giu):** trasporto live = **SSE** (ADR-0003); polling cache singleton 60s; token solo per membri divisione (tracker pubblico).

**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`), da confermare col sito host. Il codice "CH" non è gate: i permessi passano **solo** dai grant per-FIR; l'auto-elenco CH popola il dropdown via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/CONFIG.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **FIR pilota:** Roma (LIRR). Validare su una sola FIR prima di estendere.
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
