# Revisione totale del codice — aperta il 6 settembre 2026

**Ramo:** `revisione-totale` (da `main` `2b33791a`) · **Stato:** 🔵 **in corso — Fasi 0-10 CHIUSE · 33 findings** · resta la sintesi (11)

Revisione **integrale e senza perimetro escluso**, condotta con la postura di uno sviluppatore senior
**esterno che non ha scritto questo codice** e deve valutarlo. Cerca *tutto*: bug, incoerenze, codice morto,
disallineamenti fra documenti, disallineamenti fra documenti e codice, ottimizzazioni fattibili, e qualunque
cosa possa migliorare il sito.

**Questo documento è il registro.** Cresce a ogni fase. Non contiene fix: contiene *findings*.

---

## Che cosa comprende la superficie

Misure rilevate il 6 settembre 2026 su `2b33791a`, albero pulito.

| Superficie | Misura |
|---|---|
| Progetti | **11** in `src` · **9** in `tests` · **7** in `tools` |
| Codice scritto a mano | **~127 000 righe** — Application 42 931 · Ui 46 885 · Infrastructure (fuori migrazioni) 26 146 · Domain 3 955 · Host 3 349 · Hosting 1 791 · Aurora\* ~2 029 |
| Codice generato | **~399 000 righe** — 229 migrazioni SQLite (248 051) + `Infrastructure.MySqlMigrations` (150 737). Si **ispeziona**, non si legge riga per riga |
| Test | **~84 458 righe** su 471 file |
| Razor · JS · CSS | 175 · 15 · 8 + 2 `.razor.css` |
| Documenti | **167 file** — 94 `feature` · 19 `history` · 17 `refactor` · 13 `design` · 8 `guide` · 7 `adr` · 4 `spec` · 2 `reference` — più `HANDOFF.md` (179 KB) e `lavori-aperti.md` |

---

## Regole d'ingaggio

**Postura.** Non credo a `HANDOFF.md`, non credo ai commenti, non credo ai nomi delle cose. Credo al codice
letto e al comportamento provato.

**Lettura, non grep.** Ogni fase legge per intero i file del proprio perimetro. Il grep serve a trovare i
**chiamanti**, non a sostituire la lettura.

**Ogni finding ha quattro campi obbligatori.** Senza tutti e quattro non entra nel registro:

| Campo | Contenuto |
|---|---|
| `file:riga` | Dove |
| **Difetto** | Che cosa è sbagliato, una frase |
| **Scenario di rottura** | Input o stato concreto → esito sbagliato. *Se non so scriverlo, non è un finding: è un sospetto, e i sospetti stanno in fondo, separati* |
| **Verdetto** | `CONFERMATO` (provato leggendo il codice fino in fondo, o eseguendolo) · `PLAUSIBILE` (ragionato, non provato) |

### Severità

| | Che cosa |
|---|---|
| **S1** | Danno all'utente: perdita di dati, falla di autorizzazione, pagina che cade, numero sbagliato mostrato come vero |
| **S2** | Bug latente: corsa, caso limite, guasto che si vede solo sotto carico o in una sequenza rara |
| **S3** | Incoerenza: codice morto, documento che mente, due strade per la stessa cosa, invariante scritta in un posto solo |
| **S4** | Migliorabile: prestazioni, duplicazione, leggibilità |

### Bollino di consegna

Sta su **ogni** finding, perché la revisione cade dentro la **finestra cieca al 16 settembre**.

| | Significato |
|---|---|
| 🟢 **SUBITO** | Il fix tocca solo codice o asset. Entra nel prossimo pacchetto senza toccare il database |
| 🟡 **SUBITO, ma** | Caricabile ora, però la consegna richiede attenzione: rigenerare asset, ordine dei file, DLL viva, riavvio |
| 🔴 **DOPO IL 16-SET** | Richiede migrazione o cambio di schema. La carta si scrive ora, l'esecuzione aspetta |

> ⚠️ Sul 🔴 la regola è precisa: **lo schema non è congelato, la *consegna* sì.** Le migrazioni si progettano
> e si scrivono su questo ramo. Non si caricano.

**Niente fix durante la revisione.** Le fasi 0-11 producono solo registro. I fix partono dopo il via libera,
a lotti, uno per volta, ognuno con la sua verifica.

**La revisione non blocca 1.14.0.** Il pacchetto pronto si carica quando il committente vuole; questo ramo
parte da `2b33791a` e non lo tocca.

**Identificatori.** Ogni finding ha un ID stabile `R-NNN`, citabile per numero.

---

## Le dodici fasi

| Fase | Perimetro | Stato |
|---|---|---|
| **0** | Baseline misurabile: build, test, analizzatori, pacchetti, grafo, file mai citati | ✅ **chiusa** — 5 findings |
| **1** | Architettura e contratti: grafo fra progetti, ADR, multitarget net8/net10, superficie pubblica, cicli di vita DI | ✅ **chiusa** — 5 findings |
| **2** | Dominio e modello dati: invarianti, `spec/modello-dati.md` contro lo schema reale, parità SQLite↔MySQL, indici | ✅ **chiusa** — 4 findings |
| **3** | Persistenza e concorrenza: corse sul `DbContext` censite a tappeto, sentinelle prima dell'`await`, `ExecuteDelete`, N+1 | ✅ **chiusa** — 2 findings |
| **4** | Application — undici ambiti funzionali (vedi sotto) | ✅ **chiusa** — tutti e undici · 6 findings |
| **5** | Autorizzazioni e sicurezza: matrice completa, guardia nel *service*, cancelli pubblici, segreti, upload | ✅ **chiusa** — 2 findings |
| **6** | UI Blazor: render mode e isole, difetti Razor invisibili al compilatore, JS, CSS, i18n, stampa, accessibilità | ✅ **chiusa** — 3 findings |
| **7** | Test: copertura del **rischio**, test che passano sempre, fragilità, la trappola dell'uscita zero | ✅ **chiusa** — 1 finding |
| **8** | Documenti: doc↔doc, doc↔codice, stato↔realtà | ✅ **chiusa** — 3 findings |
| **9** | Build, consegna, host, strumenti: config di deploy vive e morte, `.github`, lock file, i 7 tool, runbook | ✅ **chiusa** — 2 findings |
| **10** | Prestazioni, misurate dal vivo e **divise per operazione** | ✅ **chiusa** — 0 findings nuovi, 1 proposta scartata sulla misura |
| **11** | Sintesi: registro ordinato, le due liste (🟢🟡 subito / 🔴 dopo il 16), lotti di rimedio | ⏳ |

### Fase 4 — gli undici ambiti

Non si procede per cartella: si procede per **ambito funzionale**, e ogni ambito si legge insieme alla
propria documentazione.

| # | Ambito | Documento di riscontro |
|---|---|---|
| 4a ✅ | Documento, sezioni, catalogo, blocchi | `refactor/08`, `11`, `14` |
| 4b ✅ | Release, snapshot, pubblicazione, retention | `refactor/09`, `10` · `audit-2026-08-25-versioni-release` |
| 4c ✅ | Import: SID, piste, settori, confinanti, GitHub | `refactor/01`-`05` |
| 4d ✅ | Import: tabelle, trasferimenti | `design/piano-import-*` |
| 4e ✅ | Gerarchia, AoR, shape, aree | `refactor/06`, `15` · `spec/logica-aor` |
| 4f ✅ | Trasferimenti e accordi di coordinamento | `refactor/07` · feature accordi |
| 4g ✅ | Aeroporti: dati, posizioni, quote, regole piste | feature aeroporti · vSOP militari |
| 4h ✅ | Traduzioni: catena, stato, spesa, glossario | documenti bilingue |
| 4i ✅ | Lock di risorsa e di editing | design lock |
| 4j ✅ | Audit, incarichi, impatti, notifiche | `refactor/13` |
| 4k ✅ | Live, statistiche, Aurora bridge, servizi | `refactor/12` · `adr-0003` |

Per ogni ambito, oltre a correttezza e casi limite, si pone **la domanda che trova i difetti veri**:
*questa regola regge soltanto perché adesso ce n'è UNO SOLO?*

---

# Registro dei findings

> Gli ID sono progressivi e non si riusano.

| ID | Fase | Sev. | Bollino | Verdetto | Voce | `file:riga` |
|---|---|---|---|---|---|---|
| **R-001** | 0 | **S2** | 🟢 | CONFERMATO | **Un byte NUL letterale dentro il sorgente** fa da separatore di chiave nel rilevatore di drift dello schema | `src/Vipi.Application/Diagnostics/SchemaDrift.cs:101` |
| **R-002** | 0 | S3 | 🟢 | CONFERMATO | `DivisionMemberDto` — DTO `internal` mai usato da nessuno: estratto morto in `refactor(01)` | `src/Vipi.Infrastructure/Ivao/Dtos/DivisionMemberDto.cs:5` |
| **R-003** | 0 | S4 | 🟢 | CONFERMATO | **156 file su 2 983 non sono formattati** e nessun passo di CI se ne accorge | 156 file · vedi sotto |
| **R-004** | 0 | S4 | 🟢 | CONFERMATO | `xunit 2.9.3` è marcato **deprecato (Legacy)** dal feed: alternativa `xunit.v3`. Tocca tutti e 9 i progetti di test | 9 `.csproj` in `tests/` |
| **R-005** | 0 | S3 | 🟢 | CONFERMATO | **Tre strumenti su sette stanno fuori dalla soluzione**: la CI non li compila mai, quindi possono marcire senza che nessuno lo sappia | `tools/Vipi.AuroraBridge.Cli` · `Vipi.AuroraProbe` · `Vipi.DbSeed` |
| **R-006** | 1 | S3 | 🟢 | CONFERMATO | La guida di configurazione manda a **`/sop/health`** e **`/sop/live/atc`**, che oggi rispondono **404**: sono segmenti macchina, e `LegacyRoutes` li rifiuta apposta | `docs/guide/config.md:351,421` |
| **R-007** | 1 | **S2** | 🟢 | CONFERMATO | **La patch che si consegna a ivao.it non aggancia `RunVipiStartupMaintenance()`**: cinque passi d'avvio non girerebbero mai, e la sonda di salute resterebbe verde | `docs/guide/ivao-it-wiring.patch:317-348` |
| **R-008** | 1 | **S2** | 🟢 | CONFERMATO | **1 983 regole CSS su 2 031 non sono confinate sotto `.vipi-root`**, mentre ADR-0005 D3 e la guida dichiarano il contrario. Fra i selettori c'è `details` | `src/Vipi.Ui/wwwroot/vipi-theme.css` |
| **R-009** | 1 | S4 | 🟢 | CONFERMATO | **179 tipi `public` su 1 381** non sono nominati fuori dal proprio progetto: superficie che vincola senza servire, in un modulo la cui superficie è un ADR | 179 tipi · vedi sotto |
| **R-010** | 1 | S4 | 🟢 | CONFERMATO | Tre commenti che **descrivono il codice con un numero sbagliato**: «le 60 migrazioni», «le 68 migrazioni» (sono **114**) e «le quattro manutenzioni d'avvio» (sono **cinque**) | `DependencyInjection.cs:46` · `VipiModuleExtensions.cs:493,536` |
| **R-011** | 2 | S3 | 🟢 | CONFERMATO | `modello-dati.md` **§9.13 descrive `AppProfile`, `AppFrequencyLink` e `IAppProfileService`: nessuno dei tre esiste**, e la sezione non porta il 🛑 che lo stesso documento usa altrove | `docs/spec/modello-dati.md:614-620` |
| **R-012** | 2 | S3 | 🟢 | CONFERMATO | **§9.8, dichiarata «la lista migrazioni autoritativa», si ferma alla 85ª di 114**: mancano 29 migrazioni, cioè sottosistemi interi | `docs/spec/modello-dati.md:577` |
| **R-013** | 2 | S3 | 🟢 | CONFERMATO | **16 entità su 59 non compaiono da nessuna parte** nella specifica del modello dati | `docs/spec/modello-dati.md` |
| **R-014** | 2 | **S2** | 🟢 | PLAUSIBILE | `EffectiveHierarchy.ParentMap` **perde in silenzio** un nodo se lo stesso callsign esiste nei due cataloghi: gli indici unici sono per-tabella, non fra tabelle | `src/Vipi.Domain/Services/EffectiveHierarchy.cs:48-64` |
| **R-015** | 3 | **S2** | 🟢 | CONFERMATO | **Una transazione aperta fuori dall'execution strategy**: passa su SQLite (sviluppo e tutti i test) e **solleva su MariaDB**, cioè in produzione. La potatura dell'archivio ATC non avviene mai | `src/Vipi.Infrastructure/Persistence/EfAtcTrafficStore.cs:290` |
| **R-016** | 3 | **S2** | 🟢 | PLAUSIBILE | **Sei pagine con handler `async` che toccano un repository EF senza sentinella e senza scope proprio**, sul `DbContext` del circuito | `StatsDivisionPage` · `DiagnosticaPage` · `AtcWorldArchivePage` · `StatsHome` · `StatsSessionPage` · `CoordinateConverterPage` |
| **R-017** | 4b | S4 | 🟢 | CONFERMATO | **La pubblicazione programmata di un documento singolo sta fuori dalla transazione** che il ramo dell'unione, dodici righe sotto, usa. Due scritture senza rete | `src/Vipi.Application/Content/ReleaseService.cs:219-226` |
| **R-018** | 4c | **S2** | 🟢 | CONFERMATO | **Un vertice malformato non invalida la forma del settore: la tronca**, la salva e non lascia traccia — mentre un punto *nominato* mancante la invalida | `AuroraSectorfileParser.cs:399-437` |
| **R-019** | 4c | S3 | 🟢 | CONFERMATO | **Tre validatori accettano ciò che il loro contratto dichiara di rifiutare**: `"+261"` come ciclo AIRAC, una latitudine di 91°, un segno dentro un DMS | `AiracService.cs:36` · `DmsCoordinate.cs:36,60` |
| **R-020** | 4d | **S2** | 🟢 | CONFERMATO | **Il `rowspan` non produce la cella vuota che il commento promette**: le righe sotto una cella unita scalano a sinistra, e l'anteprima mostra una tabella plausibile e sbagliata | `src/Vipi.Application/Import/TabellaHtml.cs:20-23,64-69` |
| **R-021** | 4d | S4 | 🟢 | CONFERMATO | **`CruiseLevel` entra dall'API senza un controllo di unità**: i piedi al posto dei FL danno parità e catena di ripiego sbagliate, in silenzio | `src/Vipi.Hosting/VipiModuleExtensions.cs:394` |
| **R-022** | 4i ✅ | S3 | 🟢 | CONFERMATO | **La premessa che autorizza l'uso di `ExecuteUpdate` è già falsa**: dice che nessuna entità versionata lo usa, e `Document` — che il token ce l'ha — lo usa in quattro punti | `VipiDbContext.cs:52-55` · `EfEditingRepository.cs:1226,1262,1268,1284` |
| **R-023** | 5 | **S2** | 🟢 | CONFERMATO | **La biblioteca allegati si difende solo dentro una pagina**: servizio e repository non hanno nessun controllo di ruolo, e l'`userId` dell'audit lo dichiara chi chiama | `AttachmentCurationService.cs` · `EfAttachmentLibrary.cs` · `AdminAttachmentsPage.razor:435` |
| **R-025** | 6 | **S2** | 🟢 | CONFERMATO | **Byte di controllo nel sorgente, secondo caso**: `0x1F`/`0x1E` come separatori della firma dell'indice unito. Perderli riapre un difetto già chiuso, e nessun test cadrebbe | `UnionMembersEditor.razor:136` |
| **R-032** | 9 | S3 | 🟢 | CONFERMATO | **Cinque artefatti di consegna descrivono strade non prese**, senza il marcatore ⛔ che il repository usa bene altrove — e uno sta nella cartella della consegna vera | `fly.toml` · `Caddyfile` · `docker-compose.yml` · `deploy/oracle/` · `nginx-vipi.conf` |
| **R-033** | 9 | S4 | 🟢 | CONFERMATO | **Un file di zero byte chiamato `--nologo`, versionato** in radice: residuo di un comando finito storto | `--nologo` |
| **R-029** | 8 | S3 | 🟢 | CONFERMATO | **La sezione «da leggere per prima» manda su un ramo che non esiste** e dichiara a metà una consegna finita quattro versioni fa. «In cinque righe» è lunga 707 | `docs/lavori-aperti.md:329-341` |
| **R-030** | 8 | S3 | 🟢 | CONFERMATO | **L'indice si dichiara «di tutti i documenti» e ne mancano 44**, di cui 33 carte di funzionalità — cioè dove sta scritto il *perché* | `docs/index.md` |
| **R-031** | 8 | S4 | 🟢 | CONFERMATO | **La consegna 1.12.0 non compare in `HANDOFF.md`**: zero occorrenze, mentre 1.11, 1.13 e 1.14 ci sono | `HANDOFF.md` |
| **R-028** | 7 | S3 | 🟢 | CONFERMATO | **Niente conta quanti test girano**: `dotnet test` esce zero anche su meno test di ieri, e la differenza si è già pagata una volta — ~1000 test sul runtime sbagliato per settimane | `.github/workflows/ci.yml:31` |
| **R-027** | 6 | S3 | 🟢 | CONFERMATO | **La regola del brand dichiara «zero letterali, ed è verificato»**: nessun test lo verifica, e quattro letterali sono entrati — uno con una ragione buona che le eccezioni scritte non contemplano | `docs/design/regole-brand.md:9-20` · `vipi-theme.css:2556,4251,4336` |
| **R-026** | 6 | S4 | 🟢 | CONFERMATO | **13 etichette e 9 segnaposto non seguono la barra della lingua** (regola R6): sette sono `aria-label`, cioè il testo che esiste solo per chi non vede l'icona | 13 file · vedi sotto |
| **R-024** | 5 | **S2** | 🟢 | CONFERMATO | **L'APP nascosto resta pubblico**: delle quattro porte pubbliche è l'unica che passa da un `Sector` e l'unica che non filtra `IsActive` — contro la premessa scritta nella proiezione | `EfContentRepository.cs:52-61` · `EfSectorProjectionService.cs:229` |

---

# Sospetti non confermati

> Tenuti **separati** dai findings. Un sospetto senza scenario di rottura non diventa un finding
> soltanto perché è vecchio.

| # | Sospetto | Dove si decide |
|---|---|---|
| s-01 | `vipi-live.js` e `vipi-theme-mode.js` compaiono **soltanto** in `App.razor`: dichiarati e forse mai invocati. Idem `vipi-aor3d.css` | Fase 6 |
| s-02 | Il percorso **Npgsql** di `ISchemaDriftProbe` non è eseguito da nessun test — lo dichiara un commento del repository stesso | Fase 2 |
| s-03 | ~248 diagnostiche di cultura (`CA1305/1307/1310/1311/1304/1308`) in `src/`: confronti e `ToLower` sensibili alla lingua su codici ICAO e chiavi | Fase 4 |
| s-04 | 73 `catch (Exception)` in `src/` (`CA1031`): quanti inghiottono un errore che l'utente dovrebbe vedere | Fase 4 e 5 |
| s-05 | 30 tipi `internal` mai istanziati in `src/` (`CA1812`) — altri candidati morti oltre a R-002 | Fase 4 |
| s-06 | 63 proprietà di raccolta scrivibili (`CA2227`) nel modello | Fase 2 |
| s-07 | 4 punti in `AppMemberLoader.cs` non propagano il `CancellationToken` (`CA2016`) | Fase 6 |
| ~~s-08~~ | **196 registrazioni `AddScoped`** e un `AddDbContext` (che è Scoped): in Blazor Server «scoped» vuol dire *per circuito*, cioè ore. Solo **29 componenti su ~140** che iniettano hanno uno scope proprio (`OwningComponentBase`) → **sciolto in Fase 3**: nessun uso parallelo, sei pagine esposte (R-016) | — |
| ~~s-09~~ | ~~la sonda EF8 ferma a 65 migrazioni~~ → **chiuso in Fase 2, misurato**: le 114 si applicano da vuoto sotto net8 | — |
| ~~s-10~~ | ~~`EnsureCreated` contro `Migrate()`~~ → **chiuso in Fase 2**: la scelta è esplicita per provider e un provider ignoto solleva | — |
| ~~s-06~~ | ~~63 raccolte scrivibili~~ → **chiuso in Fase 2**: sono navigazioni EF e binding di opzioni, che i setter li vogliono | — |
| ~~s-11~~ | → **diventato R-019 in 4c**. `AiracService.EffectiveUtcForCycle` valida con `int.TryParse`: **`"+261"` e `"-261"` passano** e producono una data sbagliata invece di sollevare — ed è proprio il sollevamento che `SidStampCycle` usa *come* validatore. Oggi lo schermano i chiamanti (regex `\d{4}`), non il validatore | — |

---

# Fase 0 — Baseline misurabile

**Stato:** ✅ **chiusa** il 6 settembre 2026 · 5 findings, 7 sospetti

Non si giudica niente finché non si sa da dove si parte.

## Esito in una riga

**La base è sana e la disciplina è reale**: la soluzione ricompila da zero su entrambi i target con
**zero avvisi**, i **5 543 test passano tutti** (10 639 esecuzioni contando i due target), nessun pacchetto
vulnerabile, e nel codice non esiste **un solo** `TODO`, `FIXME` o `HACK`. Quel che la Fase 0 ha trovato non
è marciume: sono **cinque crepe di manutenzione**, tutte 🟢, e una di queste — R-001 — è il tipo di difetto
che non dà mai errore finché un giorno non lo dà.

## I sette passi

| # | Passo | Esito |
|---|---|---|
| 0.1 | Build | ✅ **`--no-incremental`, 98 `CoreCompile`, 0 skip: 0 avvisi 0 errori** su `net8.0;net10.0`. Il numero è credibile perché `Directory.Build.props` impone `TreatWarningsAsErrors` — un avviso *fermerebbe* la build |
| 0.2 | Test, uno per progetto | ✅ **9 su 9 verdi**, nessuno saltato. Vedi tabella |
| 0.3 | Analizzatori a tappeto | ✅ **4 133 diagnostiche uniche in `src/`** con `AnalysisMode=All`. È il radar delle fasi seguenti, non un verdetto |
| 0.4 | Pacchetti | ✅ **0 vulnerabili** (23 progetti) · ⚠️ 1 deprecato → **R-004** |
| 0.5 | Formattazione | ⚠️ **156 file su 2 983** → **R-003** |
| 0.6 | Grafo delle dipendenze | ✅ **pulito**: `Domain` non dipende da nessuno, `Application → Domain`, `Infrastructure → Application+Domain`, `Ui → Application+Domain+AuroraProfiles`. Nessun ciclo, nessuna inversione |
| 0.7 | Inventario del mai citato | 1 file morto (**R-002**) · 3 strumenti fuori dalla CI (**R-005**) · asset `wwwroot` tutti citati |

## 0.2 — La suite, progetto per progetto

| Progetto | net8.0 | net10.0 | Durata |
|---|---|---|---|
| Vipi.Application.Tests | 2 215 ✅ | 2 215 ✅ | ~1 s |
| Vipi.Assets.Tests | 54 ✅ | — | 1 s |
| Vipi.AuroraBridge.Tests | 79 ✅ | — | 1 s |
| Vipi.AuroraProfiles.Tests | 63 ✅ | 63 ✅ | ~0,3 s |
| Vipi.Domain.Tests | 130 ✅ | 130 ✅ | ~0,25 s |
| Vipi.E2E.Tests | 300 ✅ | — | **2 m 4 s** |
| Vipi.Hosting.Tests | 58 ✅ | 58 ✅ | ~0,6 s |
| Vipi.Infrastructure.Tests | **1 303** ✅ | **1 289** ✅ | ~45 s |
| Vipi.Ui.Tests | 1 341 ✅ | 1 341 ✅ | 3 s |
| **Totale** | **5 543** | **5 096** | |

> Il **delta di 14 test** fra i due target di `Vipi.Infrastructure.Tests` **non è un difetto**: il ramo
> MariaDB vive sotto `#if NET8_0` perché Pomelo non ha una build per EF Core 10 (ADR-0007 §D4-ter). È
> voluto, dichiarato nel `.csproj`, e la scelta di multi-target esiste proprio per non spedire in produzione
> l'unico pezzo che nessun test esegue.

## 0.3 — Il radar degli analizzatori

Con `AnalysisMode=All` + `EnforceCodeStyleInBuild`, deduplicando per `(file, riga, regola)`:

| Area | Diagnostiche uniche |
|---|---|
| `src/` | **4 133** |
| `tests/` | 7 092 (3 991 sono `CA1707`, il trattino basso nei nomi dei test: convenzione, non difetto) |
| `tools/` | 133 |

Le prime per numero in `src/` sono rumore di convenzione — `CA2007` (2 154, `ConfigureAwait`, che in
ASP.NET Core non serve) e `CA1062` (868, validazione degli argomenti). **Quelle che contano** sono in coda,
e sono già smistate fra i sospetti s-03…s-07. L'elenco completo sta in
`scratchpad/diag-uniche.txt` (11 358 righe) e va riletto all'inizio di ogni fase, filtrato sul suo perimetro.

> ⚠️ Gli analizzatori sono stati alzati **solo da riga di comando** (`-p:AnalysisMode=All`). Nessun file del
> repository è stato toccato, e `TreatWarningsAsErrors` è stato disattivato solo per quella singola build.

## 0.4 — Pacchetti

Zero vulnerabili su 23 progetti (l'audit NuGet è già acceso in `Directory.Build.props`, con
`NuGetAuditMode=all`). Un solo deprecato, `xunit 2.9.3` → **R-004**.

> ⚠️ `dotnet list package` ha esaminato **23 progetti su 26**: i tre strumenti fuori soluzione non sono
> stati controllati da nessuno. È la seconda faccia di **R-005**.

## 0.7 — Che cosa la CI non guarda

`\.github/workflows/ci.yml` ha quattro job: build+test su `Vipi.slnx`, test su `net8.0`, migrazioni MySQL
applicate su MariaDB vera, e smoke del container Docker. **Non c'è** un passo di formattazione (→ R-003),
**non c'è** un passo con gli analizzatori alzati, e i tre strumenti fuori soluzione **non vengono mai
compilati** (→ R-005). Provati a mano il 6 settembre: tutti e tre compilano ancora.

---

## R-001 — Un byte NUL letterale nel sorgente

`src/Vipi.Application/Diagnostics/SchemaDrift.cs:101` · **S2** · 🟢 **SUBITO** · CONFERMATO

```csharp
private static string Key(SchemaColumn c) => $"{c.Table}<NUL>{c.Column}";
```

Dove `<NUL>` è **il byte 0x00 scritto dentro il file**, non l'escape `\0`. La chiave serve a decidere se una
colonna attesa dal modello esiste davvero nello schema (`actualKeys.Contains(Key(m))`, riga 87): il separatore
esiste per impedire che `Tabella="AB", Colonna="C"` e `Tabella="A", Colonna="BC"` finiscano nella stessa chiave.

**Scenario di rottura.** Il file è **binario per ogni strumento testuale**: `grep` lo salta con «Binary file
matches», `git diff` non lo mostra, e qualunque passaggio che normalizzi il testo — un formattatore, un salvataggio
d'editor, un filtro `.gitattributes`, un copia-incolla attraverso un terminale — può togliere o sostituire quel
byte **senza che nulla segnali niente**. Nel momento in cui il separatore sparisce, `Key` diventa una semplice
concatenazione e due colonne diverse collidono: la diagnostica dichiara **presente** una colonna che nel database
non c'è, cioè fallisce esattamente il controllo per cui esiste. Il difetto non si vede mai, finché la prima volta
che conta non è già passato.

**Rimedio** (una riga): scrivere il separatore come escape testuale — `$"{c.Table}\0{c.Column}"` — oppure usare
un separatore stampabile che non può comparire in un nome di tabella o colonna. Il file torna testuale e diffabile.
Nessuna migrazione, nessun cambio di comportamento a parità di byte.

## R-002 — Un DTO morto nato da un refactor

`src/Vipi.Infrastructure/Ivao/Dtos/DivisionMemberDto.cs:5` · **S3** · 🟢 **SUBITO** · CONFERMATO

`internal sealed record DivisionMemberDto(...)` con tre `JsonPropertyName`. Il tipo è `internal`, quindi
nessun consumatore esterno è possibile, e **dentro l'assembly non lo nomina nessuno**: l'unica occorrenza
in tutto il repository è la sua stessa dichiarazione. Nato in `eaca2a3f` — «estrai 11 DTO `IvaoApiClient`
in `Ivao/Dtos/`» — cioè era **già morto dentro `IvaoApiClient`** e il refactor lo ha promosso a file proprio
senza accorgersene.

## R-003 — 156 file non formattati, e nessuno che se ne accorga

`dotnet format --verify-no-changes` esce **2** su `Vipi.slnx`: 156 file su 2 983.

| Progetto | File |
|---|---|
| `tests/Vipi.Infrastructure.Tests` | 68 |
| `tests/Vipi.Application.Tests` | 28 |
| `src/Vipi.Infrastructure` | 23 |
| `src/Vipi.Application` | 16 |
| `tests/Vipi.Ui.Tests` | 12 |
| altri 6 progetti | 9 |

Da solo è S4. Conta per due ragioni: la CI **non ha un passo di formattazione**, quindi il numero può
soltanto crescere; e la formattazione è il posto dove si nasconde il codice arrivato per copia-incolla —
che è esattamente ciò che questa revisione deve trovare.

---

# Fase 1 — Architettura e contratti

**Stato:** ✅ **chiusa** il 6 settembre 2026 · 5 findings (2 × S2), 3 sospetti nuovi

Perimetro: `Vipi.slnx`, i 26 `.csproj`, `Directory.Build.props`, i 7 ADR, `docs/refactor/00-overview.md`,
`docs/design/regole-*.md`, `docs/guide/integration.md` e `ivao-it-wiring.patch`.

## Esito in una riga

**La struttura tiene; è il contratto scritto verso l'esterno che non tiene.** Il grafo dei progetti è
pulito, le regole di perimetro sono rispettate alla lettera, il namespacing JS regge. Ma le **tre cose che
l'ADR-0005 promette a un sito ospitante** — superficie minima, isolamento CSS, aggancio in poche righe —
sono tutte e tre disallineate dal codice, e l'unico posto dove il disallineamento costa qualcosa è
**esattamente l'integrazione in Ivao.It, che è lavoro aperto**.

## Quel che è stato verificato e regge

| Contratto | Verifica | Esito |
|---|---|---|
| ADR-0001 D2 — dipendenze verso l'interno | grafo dei `ProjectReference` | ✅ `Domain` non dipende da nessuno · `Application → Domain` · `Infrastructure → Application+Domain` · `Ui → Application+Domain+AuroraProfiles` · nessun ciclo, nessuna inversione |
| Multi-target `net8.0;net10.0` senza API .NET 9+ | non serve grep: **lo prova la build** di Fase 0, che compila ogni libreria su entrambi i target con `TreatWarningsAsErrors` | ✅ |
| ADR-0005 D1 — la superficie del modulo esiste | `AddVipiModule` · `UseVipiModule` · `MapVipiModule` · `MigrateVipiDatabase` · `UiAssembly` | ✅ tutte presenti, più `RunVipiStartupMaintenance` (vedi R-007) |
| ADR-0005 D4 — topbar disattivabile | `VipiChromeOptions.RenderTopbar` → `SopLayout.razor:13` | ✅ |
| ADR-0005 D5 — JS sotto `vipi*` | i soli globali sono `__vipiAccAccordion`, `__vipiEditorAnchors`, `__vipiEditorKeys`, `__vipiZoom` + API del browser | ✅ (restano i globali dei vendor `L` di Leaflet e `THREE`, inerenti alla vendorizzazione) |
| `regole-perimetro-servizi` P5 — un servizio è figlio diretto di `/services` | 4 `@page` figlie dirette (`vsop`, `stats`, `profile-swapper`, `coordinates`); le altre 3 schede dell'hub sono marcate `shortcut` | ✅ alla lettera |

## R-006 — La guida manda a due indirizzi che rispondono 404

`docs/guide/config.md:351,421` · **S3** · 🟢 · CONFERMATO

Oggi convivono **tre prefissi**: `/services/vsop/…` per le pagine, **`/vsop/…` per gli endpoint macchina**
(`health`, `health/ready`, `live/atc`, `api/v1/*`, media, files) e `/sop` + `/vsop` come rotte storiche che
rispondono 301.

`config.md` — che si presenta come «riferimento di **tutte** le impostazioni runtime dell'host» — cita
sei indirizzi col prefisso `/sop`. Quattro di essi (`/sop/admin/audit`, `/sop/admin/sorgenti`,
`/sop/admin/permessi`) funzionano ancora, perché la catch-all storica li reindirizza. **Due no**:

```
GET /sop/health      → /sop/{*rest} → LegacyRoutes.Resolve() → null → 404
GET /sop/live/atc    → idem                                          → 404
```

`LegacyRoutes.cs:40,105` rifiuta di proposito i primi segmenti macchina (`health`, `ping`, `api`, `media`,
`files`) e la coppia `live/atc`, perché quegli endpoint «non si spostano». La scelta è giusta; è la guida
che non l'ha seguita. **Scenario di rottura:** chi configura la sorveglianza del sito leggendo la guida
punta il monitor su `/sop/health` e sorveglia un 404 — cioè o allarma sempre, o (se il monitor accetta
qualunque risposta) **non sorveglia niente**.

## R-007 — La patch per ivao.it non fa girare cinque passi d'avvio

`docs/guide/ivao-it-wiring.patch:317-348` · **S2** · 🟢 · CONFERMATO

`ivao-it-wiring.patch` è **l'artefatto che si consegna**: il sito ospitante lo applica con `git am`. È
datato **1 agosto 2026** e aggancia quattro chiamate:

```
AddVipiModule → MigrateVipiDatabase → UseVipiModule → MapVipiModule
```

Manca **`app.RunVipiStartupMaintenance()`**, che il nostro host chiama (`VipiStartup.cs`) e che la guida
`integration.md:75` documenta. Non è un dettaglio: è l'**ombrello di cinque passi**
(`VipiModuleExtensions.cs:559`):

| Passo | Che cosa succede se non gira |
|---|---|
| `LoadVipiRoleOverrides` | Chi è stato **promosso a mano** vale quanto dice la sua posizione staff: i permessi concessi a mano non esistono |
| `ReconcileVipiDocuments` | Le riconciliazioni documentali non avvengono |
| `ProjectVipiSectors` | I settori **non sono proiettati** dai cataloghi |
| `BackfillVipiReleases` | Le release effettive non sono riempite |
| `TidyVipiDocumentUnions` | Le unioni di documenti restano sporche |

**Scenario di rottura, e il dettaglio che lo rende cattivo.** Il commento del codice spiega che un guasto di
questi passi passa da `IStartupMaintenanceReport` alla diagnostica e manda `/vsop/health` in **Degraded** —
«un *logga e prosegui* che si ferma al log è un modo per non accorgersene mai». Ma quella rete scatta solo
se il passo **gira e fallisce**. Se non viene mai chiamato, non fallisce: la salute resta **verde** e il
sito parte con le promozioni a mano ignorate e i settori non proiettati.

**Secondo scarto della stessa patch:** descrive le pagine sotto **`/vsop`** (12 occorrenze, zero
`/services/vsop`), prefisso spostato il 22 agosto.

> ⚠️ `integrazione-ivao-it-da-fare.md:164` **sa** che la patch è congelata, ma dichiara scaduto **un solo
> punto** («su questo punto dice ancora *unpkg*»). Il difetto non è la patch congelata: è che l'elenco di
> ciò che è scaduto sia incompleto, perché è quell'elenco che qualcuno userà per correggerla.

## R-008 — L'isolamento CSS promesso non esiste

`src/Vipi.Ui/wwwroot/vipi-theme.css` · **S2** · 🟢 · CONFERMATO

ADR-0005 D3: *«Tutte le regole del tema sono confinate sotto il contenitore `.vipi-root`»*, con
conseguenza dichiarata *«Nessun side-effect CSS sul sito ospitante»*. `integration.md:126` lo ripete al
sito ospitante come garanzia.

Misurato analizzando le graffe (non a grep), contando come «primo livello» ciò che sta a profondità zero
al netto dei blocchi `@media`/`@supports`:

| | Regole |
|---|---|
| Totali in `vipi-theme.css` | **2 031** |
| Confinate sotto `.vipi-root` (o `:root`, che definisce le variabili) | **48** |
| **Non confinate** | **1 983** (97,6%) |

I selettori-radice non confinati più frequenti sono nomi da collisione garantita:

```
.struct(394) .res-table(116) .wrap(56) .topbar(46) .st-head(25) .ed-layout(24)
.cfg-table(17) .coord-table(17) .doc-head(16) .toc(16) .pill(15) .block(14)
.callout(11) .help-hint(11)   …e details(11), che è un selettore d'ELEMENTO
```

**Scenario di rottura.** `integration.md:140-143` dice all'host di caricare `vipi-theme.css` con un `<link>`
globale. Applicato a `Ivao.It.Website`: ogni `<details>` del sito ospitante — non del modulo — cambia
aspetto, e `.wrap`, `.block`, `.pill`, `.toc`, `.topbar`, `.card` collidono **nei due versi** (le loro
regole entrano nel modulo, le nostre escono). È esattamente il danno che ADR-0005 dichiara evitato, su
un'integrazione che è lavoro aperto e che nessuno ha ancora eseguito (`integrazione-ivao-it-da-fare.md`
§2.1: *«Nessuno ha mai eseguito il modulo dentro un host net8»*).

> Il rimedio non è piccolo (1 983 regole da prefissare), ma è **meccanico e senza rischio a runtime** per
> l'host autonomo, dove `.vipi-root` avvolge già tutto il contenuto. Va misurato prima di prometterlo:
> `SopLayout` avvolge la pagina, ma la **topbar** e alcuni contenitori potrebbero stare fuori dal wrapper.
> Va deciso in fase di rimedio, non qui.

## R-009 — Superficie pubblica più larga del necessario

**S4** · 🟢 · CONFERMATO

**179 tipi `public` su 1 381** non sono nominati da nessun file fuori dal proprio progetto (test compresi).

| Progetto | Tipi |
|---|---|
| `Vipi.Application` | 98 |
| `Vipi.Infrastructure` | 38 |
| `Vipi.Ui` | 22 |
| `Vipi.Hosting` | 8 |
| `Vipi.AuroraBridge.Core` | 6 |
| `Vipi.Host` | 3 · `Vipi.AuroraBridge` 2 · `Vipi.Domain` 2 |

Conta perché la superficie di questo modulo **è un ADR**: ogni tipo pubblico è una promessa verso un host
che lo incorpora. In `Vipi.Infrastructure` la maggioranza sono `…HostedService`, che `AddHostedService<T>`
sa registrare anche se `internal`.

> ⚠️ **Da non applicare in blocco.** La scansione non legge i `.xaml` (quindi `MainWindow` è un falso
> positivo), e un tipo può essere pubblico per una ragione che il nome non dice. È un **censimento**, non
> una lista di cancellazioni: si decide tipo per tipo nella fase di rimedio.

## R-010 — Due numeri sbagliati che giustificano una scelta

**S4** · 🟢 · CONFERMATO

```
DependencyInjection.cs:46      «le 60 migrazioni sono SQLite-flavored e non girano su Postgres»
VipiModuleExtensions.cs:493    «avrebbe applicato le 68 migrazioni SQLite-flavored a MariaDB»
```

Le migrazioni SQLite oggi sono **114**. I due numeri non cambiano la decisione — che resta giusta — ma in
un repository dove le prove sono numeri, un numero fermo dentro un commento è un numero che qualcuno
riuserà.


---

# Fase 2 — Dominio e modello dati

**Stato:** ✅ **chiusa** il 7 settembre 2026 · 4 findings (1 × S2), 3 sospetti chiusi, 1 aperto

Perimetro: `Vipi.Domain` (30 file, 3 955 righe), `VipiDbContext.OnModelCreating` (991 righe), le 114
migrazioni SQLite, le migrazioni MySQL, `docs/spec/modello-dati.md` (1 269 righe).

## Esito in una riga

**Lo schema è la parte più difesa di questo repository, e la sua specifica è la meno aggiornata.** Nessun
difetto di modello: i due insiemi di migrazioni sono allineati al modello (misurato, non letto), le 114
migrazioni SQLite si applicano da vuoto anche sotto EF Core 8, e i vincoli che su MySQL fanno la differenza
— collation sensibile, lunghezze delle colonne indicizzate, tetto di chiave InnoDB — hanno tutti un test che
li tiene fermi. Il documento che dovrebbe descrivere tutto questo è invece **fermo al 25 agosto** su tre
assi diversi.

## Quel che è stato misurato, non letto

| Verifica | Comando / metodo | Esito |
|---|---|---|
| Il modello ha modifiche non ancora migrate? (SQLite) | `dotnet ef migrations has-pending-model-changes` su `net8.0` | ✅ **nessuna** |
| Idem sull'insieme **MySQL**, che è quello di produzione | idem su `Vipi.Infrastructure.MySqlMigrations` | ✅ **nessuna** |
| Le 114 migrazioni si applicano **da database vuoto sotto EF Core 8**? | `dotnet ef database update --framework net8.0` | ✅ **tutte e 114** — chiude s-09 |
| Lo stato del database di sviluppo | interrogato in copia, sola lettura | 114 migrazioni applicate, coerente col repository |
| Callsign presenti in **entrambi** i cataloghi | query sui due cataloghi | **0 oggi** — vedi R-014 |

## Le difese che reggono

- **Il token di concorrenza si ruota nel `DbContext`, non nei repository.** `SaveChanges`/`SaveChangesAsync`
  riassegnano il token a ogni entità aggiunta o modificata che ne dichiari uno. È la forma giusta della
  garanzia — «passare dal context basta» invece di «un repository si ricorda di farlo» — e la scelta di
  *togliere* il token alle quattro entità che lo dichiaravano senza mai scriverlo è più difendibile di
  averlo lasciato lì a fare finta.
- **I default degli enum-stringa sono dichiarati nel modello e non solo nella migrazione**, con la ragione
  scritta accanto: il reconciler Postgres li rilegge di lì, e uno scaffolding lasciato a sé emetterebbe `""`,
  che non è il nome di nessun valore.
- **La trappola del `bool` opt-out è conosciuta e contenuta.** Le 39 colonne `bool` aggiunte da migrazione
  hanno il default giusto per il loro verso: `true` per i flag di policy (opt-out), `false` per gli opt-in.
  `ImportPolicy.ImportSids` — nato `false` l'8 luglio e corretto il 3 agosto — è citato **per nome** in
  quattro file come il precedente da non ripetere, e nel database la riga non esiste nemmeno.
- **Sei test tengono ferme le regole MySQL**: ogni colonna stringa indicizzata ha una lunghezza, nessuna
  supera il tetto InnoDB, le FK su chiave alternata hanno la stessa misura della principale, ogni enum ha
  una lunghezza, nessuna colonna resta `longtext` nella DDL, la collation è sensibile a maiuscole e accenti.
- **La finestra cieca ha una guardia eseguibile**, non una raccomandazione: `MigrazioniDellaFinestraCiecaTests`
  respinge una migrazione distruttiva emessa dopo l'ultimo dump, e chiede di essere **cancellata** il 16
  settembre invece di spostare le date in avanti.

## R-011 — La specifica descrive tre cose che non esistono

`docs/spec/modello-dati.md:614-620` · **S3** · 🟢 · CONFERMATO

L'intestazione del documento dice: *«§9 è la parte AUTOREVOLE corrente e prevale dove in conflitto»*. La
§9.13 descrive per esteso `AppProfile` (campi, FK, cascade, indice unico), `AppFrequencyLink` e il servizio
`IAppProfileService`. Nel codice **non esiste nessuno dei tre**: lo storage è migrato su
`Document` + `DocumentProfile`, e lo dice `VipiDbContext` — *«entità AppProfile rimosse»*.

Non è che il documento non sappia marcare ciò che è superato — lo fa altrove, e bene: §9.11 porta
«⚪ superata da §9.31», §9.15 un banner 🛑 **[SUPERATO — non implementare da qui]**, §9.25 «⚪ storia». La
§9.13 non porta niente. Di quella sezione sopravvive solo la rotta (`/services/vsop/{acc}/apps/editor`).

## R-012 — La «lista migrazioni autoritativa» si ferma alla 85ª di 114

`docs/spec/modello-dati.md:577` · **S3** · 🟢 · CONFERMATO

§9.8 si chiude su `DocumentoDellAeroporto` (25 agosto), che è la **migrazione numero 85**. Ne mancano **29**,
e non sono rifiniture: sono le tabelle di sottosistemi interi.

```
TimbriPerEliminare · RiassuntoMensileAtc · IncaricoDaSegnalazione · IdentitaDeiSettori
ShapeVuoteANull · GateAiracShape · MemoriaDiTraduzione · DestinatarioSezione · EdizioneMilitare
ArchivioAtcMondiale · GlossarioFraseologia · PromozioniAMano · ConcessioniPerAccRimosse
RadioassistenzeAnagrafica · CoordinateSogliaPista · RadioassistenzeFamigliaETipo
BibliotecaAllegati · CatalogoSpaziAerei · AgganciSpaziAerei · RegistroSpesaTraduzione
PezziDiForma · FormaCheHaContato · CatenaDiRipiego · LinguaBloccata · DocumentiUniti
```

Il difetto non è l'elenco incompleto: è che si **dichiari autoritativo**. Un elenco che dice di sé «questo è
tutto» e non lo è costa più di un elenco che non promette niente.

## R-013 — Un quarto del modello non è nella specifica del modello

**S3** · 🟢 · CONFERMATO

**16 entità su 59** non compaiono in `modello-dati.md`, in nessuna sezione:

```
Attachment · AttachmentVersion · AirspaceImport · AirspaceVolume · SectorAirspaceBinding
SectorShapePart · SectorFallback · TranslationUnit · TranslationSpend · GlossaryTerm
MediaAsset · CallsignAlias · EditResourceLock · AgreementAirport · AirportDayTraffic
AirportTransitionLevel
```

Il documento si apre dicendo di essere *«la sorgente da cui derivare le entità di dominio e le configurazioni
EF Core»*. Oggi la sorgente vera è `VipiDbContext.OnModelCreating`, che infatti spiega ogni scelta — ed è il
posto giusto perché ci sia. Il difetto è che la specifica continui a presentarsi come la sorgente.

> **I tre findings hanno un rimedio solo, e vale la pena dirlo qui.** Non è riscrivere §9: è **spostare la
> corona**. `modello-dati.md` dichiara che la sorgente è `OnModelCreating`, marca §9.13 come le altre tre già
> marcate, e sostituisce §9.8 con il comando che la genera. Un elenco che si aggiorna a mano ricade sempre.

## R-014 — Due cataloghi, un dizionario, e chi arriva secondo vince

`src/Vipi.Domain/Services/EffectiveHierarchy.cs:48-64` · **S2** · 🟢 · **PLAUSIBILE**

`ParentMap` costruisce l'albero di copertura **effettivo** a partire dalle righe dei **due** cataloghi
(`AccSectors` e `AirportSectors`, uniti dai chiamanti in `EfHierarchyEditingService` e
`EfConsistencyReportRepository`) e le scrive in un dizionario con l'**indicizzatore**:

```csharp
mappa[r.Callsign] = r.ParentCallsign;      // e, più sotto, il ramo derivato dalla scaletta
```

L'unicità di `ComposePosition` è garantita **dentro ciascuna tabella** — due indici unici separati — e
**niente** vieta lo stesso callsign in tutte e due. Se accade, la seconda riga sovrascrive la prima **in
silenzio**: nessuna eccezione, nessun rilievo di diagnostica.

**Scenario di rottura.** Lo stesso callsign compare nei due cataloghi con due padri diversi. Nell'albero
effettivo ne sopravvive uno: il settore perde il padre vero, la catena di copertura si accorcia, e la
ricaduta dei trasferimenti finisce su un ente sbagliato o su UNICOM. Il difetto non si vede all'ingresso —
entrambe le righe sono legali — ma a valle, come una gerarchia che «non è quella che ho scritto».

**Perché PLAUSIBILE e non CONFERMATO:** misurato oggi sul database di sviluppo, i callsign comuni ai due
cataloghi sono **zero**. La regola regge perché al momento la collisione non c'è — che è esattamente la
domanda che questa revisione si porta dietro. Il rimedio non è un indice (le tabelle sono due): è **fare
rumore** invece di sovrascrivere, come questo repository fa già altrove con `DocRelease` e `DocumentImpact`
(«meglio un conflitto rumoroso da ritentare»).

## Sospetti chiusi in questa fase

| # | Perché era un sospetto | Perché si chiude |
|---|---|---|
| s-06 | 63 raccolte scrivibili (`CA2227`) | Sono navigazioni EF e binding di `IOptions`: i setter servono. Nessun caso residuo |
| s-09 | La sonda «EF10 applicabili sotto EF8» ferma a 65 migrazioni | **Eseguita**: 114 su 114 applicate da vuoto sotto `net8.0` |
| s-10 | `EnsureCreated` (Postgres) contro `Migrate()` (SQLite/MySQL) | La scelta è esplicita per `ProviderName`, con un `throw` per il provider ignoto e la ragione scritta |

---

# Fase 3 — Persistenza e concorrenza

**Stato:** ✅ **chiusa** il 7 settembre 2026 · 2 findings (2 × S2), s-08 sciolto

Perimetro: i 164 file di `Vipi.Infrastructure` fuori dalle migrazioni, `EfUnitOfWork`,
`TracciaCollisioniInterceptor`, e i 140 componenti `.razor` che iniettano un servizio.

## Esito in una riga

**La corsa che tutti temono non c'è più; ce n'è un'altra, e sta dove nessun test può vederla.** Il censimento
a tappeto non ha trovato un solo uso parallelo del `DbContext` — i due `Task.WhenAll` del repository sono
HTTP e basta — e le scorciatoie che sfuggono al change-tracker sono usate in quattro file, ognuna con la sua
ragione scritta. Il difetto vero è di **configurazione**, non di codice concorrente: una transazione aperta
fuori dall'execution strategy **funziona su SQLite e solleva su MariaDB**, cioè passa tutti i test e cade
solo in produzione.

## Quel che è stato misurato

| Misura | Valore |
|---|---|
| Registrazioni `AddScoped` | 196 · `AddSingleton` 37 · `AddHostedService` 17 · `AddTransient` 1 |
| Componenti con scope proprio (`OwningComponentBase`) | **29** |
| Componenti `.razor` con handler `async` | **82** — 31 con una guardia (`_busy`/`_salvando`/semaforo), **51 senza** |
| Di quei 51, quanti toccano davvero un repository EF | **6** → R-016 |
| Usi paralleli del `DbContext` (`Task.WhenAll`, `Parallel`, `Task.Run`) | **0** — i due `WhenAll` sono chiamate HTTP |
| Query dentro un ciclo (N+1) in `Infrastructure` | **0** |
| Letture EF · di cui `AsNoTracking` | 774 · 424 (le altre sono percorsi di scrittura, che il tracking lo vogliono) |
| Punti che aprono una transazione esplicita | **2** — `EfUnitOfWork` e R-015 |

## Le difese che reggono

- **`ExecuteDelete` non si usa nei repository**, e non per abitudine: in quattro file c'è scritto *perché*
  (`RemoveRange` e non `ExecuteDelete`, il secondo desincronizza il change-tracker). I sette `ExecuteUpdate`
  che restano sono tutti su entità **senza** token di concorrenza — lock e flag — e ognuno porta la nota che
  spiega che scrive subito e non passa dal tracker.
- **L'interceptor che nomina chi c'era prima è senza stato** (un solo campo, e statico) ed è montato su
  **tutti e tre** i provider: la diagnosi delle collisioni non cambia a seconda di dove gira.
- **La sentinella prima dell'`await` è una convenzione viva**: `StatsDivisionPage.OnParametersSetAsync`
  segna la chiave *prima* di leggere, con il commento che spiega che segnarla dopo farebbe le query due volte.
  Il difetto di R-016 è che la stessa pagina non lo faccia nell'altro handler.

## R-015 — Una transazione fuori dall'execution strategy: passa i test, cade in produzione

`src/Vipi.Infrastructure/Persistence/EfAtcTrafficStore.cs:290` · **S2** · 🟢 **SUBITO** · CONFERMATO

`RollupAndPruneSessionsAsync` apre la transazione **a mano**:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
...
await _db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

`EfUnitOfWork` — l'unico altro punto che apre transazioni — fa la stessa cosa **dentro**
`Database.CreateExecutionStrategy().ExecuteAsync(...)`. E `DependencyInjection.cs:89` scrive l'invariante
a lettere chiare: *«l'unico punto che apre transazioni esplicite è `EfUnitOfWork` … Prima di aprire una
transazione altrove, rileggere quel file.»* Questo è il punto che non l'ha riletto.

**Il meccanismo, provato invece che ricordato.** Una sonda scritta apposta (SQLite + una execution strategy
che dichiara di ritentare, come fanno `EnableRetryOnFailure` di Pomelo e di Npgsql):

```
RetriesOnFailure = True
BeginTransactionAsync da solo  → PASSA, nessuna eccezione
BeginTransaction + SaveChanges + Commit (la forma esatta del codice)
   → InvalidOperationException
     "The configured execution strategy '…' does not support user-initiated transactions.
      Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()'…"
```

> ⚠️ Vale la pena dire anche l'ipotesi **sbagliata**: il `BeginTransaction` da solo **non** solleva. Se ci si
> fermava lì, il difetto risultava inesistente. È la coppia transazione + `SaveChanges` a farlo uscire.

**Perché nessun test lo vede.** `EnableRetryOnFailure` è configurato per **MySQL** (produzione, MariaDB su
`atc.it.ivao.aero`) e per **Postgres** (Render + Neon). Su **SQLite** — cioè in sviluppo e in *tutti* i
test, `SessioniPotateTests` e `ArchivioAtcMondialeTests` compresi — non c'è nessuna strategy che ritenta,
quindi la stessa riga passa. Il codice è verde su 5 543 test e rosso sull'unico ambiente che conta.

**Scenario di rottura, e perché è adesso.** `TrafficRetentionHostedService` gira ogni `TrafficRetentionHours`
e chiama `AtcSessionRetentionUseCase`, che chiama questo metodo. Il metodo esce prima della transazione solo
se **non** ci sono sessioni chiuse più vecchie di 366 giorni. Misurato sul database di sviluppo: le sessioni
partono dal **5 settembre 2025** (le porta il backfill dello storico IVAO) e **49 righe** sono già oltre la
soglia. Quindi in produzione il giro entra nella transazione, solleva, e `GatedImportLoop` la cattura, la
registra come `ImportState.LastError` della categoria e **riprova ogni ora, per sempre**.

Il danno non è visibile a schermo: è che **la potatura non avviene mai**. L'archivio ATC mondiale — misurato
a 10-14× le sessioni italiane, ~230 MB a regime — cresce senza il freno che è stato scritto per contenerlo,
e il riassunto mensile non si costruisce più. Si vede in `/services/vsop/admin/sources`, dove quella
categoria resta in errore con quel messaggio.

**Rimedio:** far passare il metodo da `IUnitOfWork` come tutti gli altri, o avvolgerlo in
`CreateExecutionStrategy()` con `ChangeTracker.Clear()` a ogni tentativo, come fa `EfUnitOfWork` e per la
ragione che quel file spiega. Nessuna migrazione.

> **Come renderlo impossibile invece che corretto una volta:** un test che monti il `VipiDbContext` con una
> execution strategy che ritenta e chiami i percorsi transazionali. Oggi la differenza fra i provider è
> proprio il buco in cui questo difetto è passato.

## R-016 — Sei pagine senza sentinella, sul `DbContext` del circuito

**S2** · 🟢 **SUBITO** · **PLAUSIBILE**

In Blazor Server un handler `async` cede il contesto al primo `await`: il gesto successivo dell'utente parte
**mentre** il primo è ancora in volo. Se entrambi toccano il `DbContext` — che è *scoped*, cioè uno per
circuito e vivo per ore — la seconda operazione trova il contesto occupato e la pagina muore con
«A second operation was started on this context instance».

Sei componenti hanno handler `async` che toccano un repository EF, **senza** una guardia di rientro e
**senza** scope proprio:

| Componente | Handler che toccano EF | Scope proprio |
|---|---|---|
| `StatsDivisionPage.razor` | 3 | no |
| `DiagnosticaPage.razor` | 3 | no |
| `AtcWorldArchivePage.razor` | 2 | no |
| `StatsHome.razor` | 1 | no |
| `StatsSessionPage.razor` | 1 | no |
| `CoordinateConverterPage.razor` | 1 | no |

**Scenario di rottura, con il caso lavorato.** `StatsDivisionPage` sa fare la cosa giusta e la fa **in un
posto solo**: `OnParametersSetAsync` segna `_caricato` **prima** dell'`await`, col commento che spiega
perché. `CambiaVisibilita` — la casella «classifica pubblica» — non ha niente:

```csharp
private async Task CambiaVisibilita(ChangeEventArgs e)
{
    if (!Authz.IsDivisionStaff) return;
    await Impostazioni.SaveAsync(acceso, …);   // scrive
    await CaricaAsync();                        // e rilegge tutto
}
```

Chi spunta la casella e subito dopo clicca una chip di periodo (che è un link, quindi
`OnParametersSetAsync`) mette **due `CaricaAsync` in volo sullo stesso contesto**. È la sequenza esatta che
il 24 agosto 2026 ha ucciso sette volte una pagina di questo sito, e che ha fatto nascere la convenzione
dello scope proprio.

**Perché PLAUSIBILE e non CONFERMATO:** la corsa dipende dai tempi e non è stata riprodotta a schermo. Il
meccanismo però è quello già pagato da questo repository, e le sei pagine sono l'elenco completo di dove
può ripresentarsi.

> Il rimedio non è una guardia per pagina: sei pagine su ottantadue vuol dire che la convenzione c'è e
> regge, e che a queste è sfuggita. Delle due porte — `OwningComponentBase` o `_busy` — la prima è quella
> che protegge dagli **altri**, la seconda quella che protegge da **sé stessi**. A queste sei serve la
> seconda, e a quattro delle sei anche la prima.

## Quel che è stato guardato e non è un finding

- **`CA1001` — cinque singleton tengono un `SemaphoreSlim` senza fare `Dispose`** (`CachedGlobalTopology`,
  `ConsistencyReportCache`, `IvaoAirportCache`, `IvaoTokenProvider`, `SectorfileCache`). Vivono quanto il
  processo: non c'è perdita da misurare. Vero, e irrilevante.
- **`CA1849` in `EfMediaStore:99`** — `MemoryStream.Write` sincrona dentro un metodo `async`. È la scelta
  **giusta**: su uno stream in memoria l'`await` costerebbe e non renderebbe.
- **`CA1849` nelle sonde `Postgres`/`MySql`** — chiamate sincrone in avvio, fuori da ogni percorso di
  richiesta.

---

# Fase 4 — Application (in corso)

**Stato:** 🔵 **aperta** · **4a e 4b chiusi** il 7 settembre 2026 · 1 finding (S4), 1 sospetto

## 4a — Documento, sezioni, catalogo, blocchi ✅

Perimetro: `SectionCatalog` (518 righe), `SectionKeys`, `IFrozenSectionProvider`, `DocSection`,
i quattro provider di congelamento, `VloaDocumentView`.

**Nessun finding.** Tre piste sono state seguite fino in fondo e si sono chiuse:

| Pista | Come si è chiusa |
|---|---|
| `KindOf` **ripiega su `Editorial`** per una chiave che non conosce, e `FrozenSectionScan` decide *proprio da lì* che cosa congelare in una release. Una chiave fissa senza natura dichiarata uscirebbe da un documento pubblicato **non congelata** | Confronto meccanico fra le 52 chiavi con natura e quelle usate nei registri: le uniche due scoperte sono `coordination:out` e `coordination:in`. **Non è un difetto**: sono sotto-sezioni che portano solo ordine, titolo e visibilità — il corpo lo disegna il caso `coordination` del padre, che è Derived ed è congelato. Non hanno un corpo da congelare |
| 23 `catch` vuoti | Ventuno sono attorno a `IJSRuntime` in `OnAfterRenderAsync` (il circuito può non esserci più), due sono discese da GitHub con quindici secondi di timeout, e **tutte** portano scritto perché |
| `DateTime.Now` invece di `UtcNow` | Due sole occorrenze in tutto `src`: il log del ponte Aurora e il nome di uno zip da scaricare. Nessuna finisce in un dato |

## 4b — Release, snapshot, pubblicazione, retention ✅

Perimetro: `ReleaseService` (753 righe), `EfReleaseRepository`, `RecomputeStatuses`, la potatura.

### R-017 — Una pubblicazione su due strade, e una sola ha la rete

`src/Vipi.Application/Content/ReleaseService.cs:219-226` · **S4** · 🟢 · CONFERMATO

`PublishAsync` (pubblicazione **programmata** a un ciclo futuro) ha due rami. Quello dell'unione avvolge
tutto in `_uow.ExecuteInTransactionAsync`, col commento che spiega perché — *«tutto o niente … metà unione a
un ciclo e metà a un altro»*. Quello del **documento singolo**, dodici righe sopra, chiama
`SnapshotAndSaveAsync` **senza transazione**:

```csharp
if (membri.Count == 0)
{
    await EnsureCanEditAsync(type, key, ct);
    await EnsureNotLockedByOthersAsync(type, key, ct);
    await SnapshotAndSaveAsync(type, key, releaseCycle, …, ct);   // ← nessuna transazione
    return;
}
```

E `SnapshotAndSaveAsync` fa **due** scritture: `SaveReleaseAsync` e poi `PruneReleasesAsync`.

**Scenario di rottura, e la sua misura onesta.** Se il processo cade fra le due, la release è pubblicata e la
potatura non è avvenuta: restano in tabella delle Superseded che dovevano sparire. È un danno **piccolo e che
si ripara da solo** al salvataggio successivo — per questo è S4 e non di più.

**Perché conta lo stesso.** `PublishNowAsync`, che è l'operazione gemella, avvolge in transazione
**entrambi** i rami, e il suo commento dice *«è l'operazione più importante che l'applicazione compie, ed era
l'unica senza rete»*. Qui la rete c'è per l'unione e non per il singolo: è la stessa regola scritta due volte
in due modi. Il giorno che a `SnapshotAndSaveAsync` si aggiunge una terza scrittura — promuovere la bozza,
mollare un lock, toccare un impatto — quel ramo diventa il buco che oggi non è. È il difetto della
**premessa che vale per uno solo**.

### Quel che è stato guardato e regge

- **`RecomputeStatuses`** applica «una release per ciclo, vince il `VersionNumber` più alto», poi elegge
  l'effettiva fra le vincitrici. Nessun pareggio possibile: due righe con la stessa data efficace sono dello
  stesso ciclo, e una delle due è per costruzione Superseded.
- **`SummariesAsync` non ha lo spareggio su `VersionNumber`** che hanno le altre quattro query di selezione.
  Cercato apposta il caso in cui questo la fa sbagliare: **non esiste**, per la regola qui sopra.
- **Gli stati invecchiano da soli** — al rollover AIRAC una programmata entra in vigore senza che nessuno
  scriva — e la selezione lo regge: filtra su `Status != Superseded` e poi **sulla data**, quindi una
  Scheduled diventata attuale viene scelta anche prima che lo sweep delle 24 ore la marchi.
- **La soglia di eliminazione (D8)** difende dal doppio clic: un secondo giro che arriva a meno di un'ora
  aggiorna l'ultimo timbro ma **non** fa scorrere il penultimo, che è ciò che autorizza a cancellare. E la
  «prova di assenza» salta l'attesa solo col verdetto `Assente` — «non si sa» non basta.

## Sospetto nuovo

| # | Sospetto | Dove si decide |
|---|---|---|
| s-12 | `MySqlCollation` porta MariaDB su `uca1400_as_cs`, che per **maiuscole e accenti** allinea la produzione a SQLite (era il rischio grosso, ed è chiuso). Restano due differenze che nessun test vede: `LOWER()`/`UPPER()` di SQLite sono **solo ASCII** mentre MariaDB piega anche le accentate, e l'**ordinamento** di SQLite è binario mentre quello UCA è alfabetico. **Misurato**: oggi in archivio c'è **una sola** stringa non ASCII (`Zürich ACC`), quindi la differenza esiste e non si vede. Il repository lo sa già e chiede di verificarlo *guidando l'app* | verifica live |

## 4c — Import: SID, piste, settori, confinanti, sectorfile ✅

Perimetro: `AuroraSectorfileParser` (589 righe), `DmsCoordinate`, `GitHubSidSourceRelease`, `SidStampCycle`,
`AiracService`, `SogliaEliminazione`, i cinque `…ImportHostedService`.

### R-018 — Un vertice malformato non invalida la forma: la tronca, e nessuno lo sa

`src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:399-437` · **S2** · 🟢 · CONFERMATO

`ParseSectorShapes` legge i file di forma del sectorfile. Una riga può essere tre cose: un **vertice in
coordinate** (due campi DMS), un **vertice per nome** (due campi uguali fra loro), oppure — tutto il resto —
un'**intestazione**, che chiude il blocco precedente e ne apre uno nuovo.

La chiusura del blocco tratta i due modi di sbagliare in maniera **opposta**:

```csharp
void Flush()
{
    if (callsigns is { Length: > 0 })
    {
        if (mancante is not null) irrisolti.Add(…);         // punto NOMINATO che non esiste → INVALIDA tutto
        else if (ring is { Count: >= 3 })
            foreach (var cs in callsigns) rings[cs] = ring;  // altrimenti SALVA
    }
    …
}
```

Un punto **nominato** che il catalogo non conosce invalida l'anello intero, e il commento del codice lo dice:
*«il PRIMO che manca: basta lui a invalidare»*. Un vertice **in coordinate malformato** non produce nessun
`mancante`: `TryParseDms` torna semplicemente `false`, la riga non è più riconosciuta come vertice, e cade nel
ramo dell'intestazione.

**Scenario di rottura.** Nel file di forma di un settore una riga di coordinate è scritta male — un file
di testo curato a mano su GitHub, e basta un separatore sbagliato. Allora, in quest'ordine:

1. la riga viene presa per un'**intestazione**;
2. `Flush()` chiude il settore in corso e **ne salva l'anello troncato** ai vertici visti fin lì, se sono
   almeno tre — e tre vertici sono sempre un poligono valido;
3. si apre un blocco fantasma il cui «callsign» è la stringa della coordinata malformata;
4. `irrisolti` resta **vuoto**, quindi la pagina «Coerenza col sectorfile» non ha niente da mostrare.

Il risultato è una **AoR silenziosamente sbagliata**: `PolygonGeometry.Contains` — che decide dal vivo chi
copre uno spazio — risponde su un poligono tagliato. Non c'è errore, non c'è rilievo, e la forma *sembra*
buona: è più piccola, non vuota.

**Rimedio:** trattare un vertice non riconosciuto come si tratta un punto nominato mancante — segnarlo e
invalidare l'anello — invece di lasciarlo scivolare nel ramo dell'intestazione. Il posto dove farlo è quello
dove già oggi si scrive `mancante`.

### R-019 — Tre validatori accettano ciò che il loro contratto dichiara di rifiutare

**S3** · 🟢 · CONFERMATO

Tre punti in cui la validazione è **più debole della sua stessa documentazione**, e in due casi più debole
del gemello che fa lo stesso mestiere.

| Dove | Il contratto scritto | Che cosa accetta davvero |
|---|---|---|
| `AiracService.EffectiveUtcForCycle` | *«Throws se malformato»* — e `SidStampCycle` usa proprio quel sollevamento **come validatore** del ciclo dichiarato dalla sorgente | La validazione è `Length == 4 && int.TryParse`. **`"+261"` e `"-261"` passano**: `int.Parse("+2")` dà l'anno 2002 e `int.Parse("-2")` il 1998. Nessun sollevamento, una data sbagliata |
| `DmsCoordinate.TryParse` | «converte una coordinata DMS … false se malformata» | Nessun controllo di **intervallo**: `N091.99.99.999` è accettata. Il gemello `KmlReader` scarta il punto (`Math.Abs(lat) > 90 …`), e `CoordinateParser` — il convertitore che usa l'utente — lo rifiuta con un errore |
| `DmsCoordinate.TryParse`, forma puntata | idem | `NumberStyles.Integer` ammette il **segno**: `N-41.37.28` dà una latitudine di −40,4° sotto un emisfero Nord |

**Perché conta, e perché non è S2.** Oggi i chiamanti schermano tutti e tre: il ciclo dal changelog passa da
un `^(\d{4})` prima di arrivare ad `AiracService`, e le coordinate arrivano da file che finora sono ben
formati. Ma è di nuovo la **premessa che vale per uno solo** — la garanzia sta nel chiamante, non nel
validatore che la dichiara — e nel caso delle coordinate il difetto si somma a R-018: una minuti-99 non è
nemmeno «malformata» per il parser, quindi non tronca niente. **Entra e basta**, e disegna un vertice
plausibile nel posto sbagliato.

### Quel che è stato guardato e regge

- **`SogliaEliminazione`** e la trappola dei due clic (vedi 4b), `SidStampCycle` coi suoi tre gradini di
  ripiego e il commento sul perché sbagliano *per eccesso di fretta*.
- **`CicloDalNome`** usa `^(\d{4})(?:[_\-.].*)?\.txt$`: quattro cifre esatte, e ammette le revisioni
  intermedie (`2304_1.txt`) senza confonderle con un ciclo diverso.
- **I separatori di callsign nelle intestazioni sono due** (spazio e due punti), e la ragione è misurata sui
  file veri: leggendo solo lo spazio, quattro settori di Milano restavano senza area *in silenzio*.
- **Il `PiuRecente` dei changelog** controlla che la radice JSON sia un array prima di iterarla, perché la
  forma degli errori di GitHub è un **oggetto** e `EnumerateArray` solleverebbe un'eccezione di tipo diverso
  da quella che si cattura.

## 4e — Gerarchia, AoR, shape, aree 🔵 (parziale)

Guardati in questo giro, perché R-018 ci finisce dentro:

- **`PolygonGeometry.Contains`** — ray casting con riquadro di scarto, il lato di chiusura incluso
  (`j` parte dall'ultimo) e un solo estremo contato per lato, così un raggio che passa per un vertice non
  conta due volte. **Corretto.**
- **Le shape vuote non cancellano**: i servizi di forma escono presto su un insieme vuoto invece di
  sovrascrivere. La regola pagata il 26 agosto (83 aree azzerate) regge.

Restano da fare: la gerarchia effettiva oltre R-014, le aree regolamentate multi-ACC, il viewer 3D, i
KMZ degli spazi aerei.

## 4d — Import di tabelle e trasferimenti ✅

Perimetro: `TabellaHtml`, `Griglia`, `RisolutoreCelle`, `CostruttoreProposta`, `LettoreXlsx`,
`TransferMatcher` (448 righe), il contratto del ponte Aurora.

### R-020 — Il `rowspan`: il commento dice una cosa, il codice ne fa un'altra

`src/Vipi.Application/Import/TabellaHtml.cs:20-23, 64-69` · **S2** · 🟢 · CONFERMATO

La porta d'import a fedeltà più alta è quella HTML: quando si incolla da Excel, da Word o da una pagina, la
clipboard porta anche il `text/html`, dove **le celle sono celle**. Il file lo spiega bene, e spiega anche
come tratta le celle unite:

> *«Il `colspan` si espande, il `rowspan` no. Una cella su due colonne diventa la cella più una vuota, perché
> altrimenti la riga sarebbe più corta e in una tabella le celle successive **scalerebbero a sinistra** — il
> dato sembrerebbe sbagliato invece che unito. Il `rowspan` invece vorrebbe ricordare le righe precedenti:
> **si legge come cella vuota**, e chi rilegge l'anteprima la riempie.»*

La prima metà è vera: il `colspan` viene espanso con celle vuote, e c'è il codice che lo fa. **La seconda no.**
Non esiste nessun ramo che inserisca una cella vuota per un `rowspan`: la riga di continuazione ha
semplicemente un `<td>` in meno nel sorgente, e nessuno lo rimpiazza.

```csharp
celle.Add(Testo(c.Groups[2].Value));
var span = Colspan.Match(c.Groups[1].Value);        // ← solo colspan
if (span.Success && … && n > 1)
    for (var k = 1; k < Math.Min(n, 64); k++) celle.Add("");
```

E `Griglia.Colonne` è `Righe.Max(r => r.Count)`: le righe corte **restano corte**, non vengono pareggiate.

**Scenario di rottura.** Si incolla una tabella con una cella unita in verticale — che nelle tabelle
aeronautiche è la norma, e che **questo stesso sito produce**: `CoordTable`, `TableBlock` e `AppFrequencies`
rendono le loro tabelle proprio con `rowspan`, quindi basta copiare da una pagina della vIPI e reincollarla
nell'import. Da lì in poi ogni riga sotto quella unita ha una cella in meno, e **tutte le colonne scalano a
sinistra**: la mappatura assegna il valore della colonna *n* al campo della colonna *n−1*. L'anteprima non
mostra un buco — mostra una tabella **plausibile e sbagliata**, che è precisamente il difetto che il commento
descrive due frasi prima come la ragione per cui il `colspan` si espande.

**Rimedio:** fare per il `rowspan` ciò che il commento già promette — tenere una riga di «celle in eredità» e
inserire la vuota nelle righe coperte. È lo stesso conto del `colspan`, su un asse diverso.

### R-021 — Il livello di crociera arriva da fuori senza che nessuno ne controlli l'unità

`src/Vipi.Hosting/VipiModuleExtensions.cs:394` · **S4** · 🟢 · CONFERMATO · *stessa famiglia di R-019*

`POST /vsop/api/v1/transfers/resolve` accetta `CruiseLevel`, documentato come **FL** («già normalizzato dal
formato ICAO, `F330` → 330»). L'endpoint verifica il tetto di richieste e che `OwnerCallsign` non sia vuoto;
**sul livello non controlla niente**.

Un client che mandi i **piedi** (25000 invece di 250) ottiene due risposte sbagliate insieme: la parità
semicircolare si calcola su `(cruise / 10) % 2`, quindi 25000 diventa «pari» invece di «dispari»; e
`FeetOf(CruiseLevel, Fl)` moltiplica per cento, dando 2 500 000 piedi alla catena di ripiego, che finisce
fuori da ogni fascia. Il controllore riceve un consiglio di trasferimento sbagliato senza nessun avviso.

**Perché S4 e non di più:** l'unico client che esiste è il ponte Aurora, ed è **conservativo per costruzione**
— `CruiseFlightLevel` torna un valore solo per le quote in forma `F…`, e per i piedi (`A050`) o le metriche
(`S1130`) torna `null`, così la parità non viene proprio valutata. Di nuovo: la garanzia sta nel chiamante,
non nel contratto. Bastano un intervallo plausibile e un avviso.

### Quel che è stato guardato e regge

- **Il punteggio di `TransferMatcher`.** `ScoreScale` è la somma dei massimi positivi
  (1,00 + 0,30 + 0,15 + 0,20 + 0,15 = 1,80) e ogni contributo si applica **una volta sola**: base del flusso,
  un solo punteggio di CoP fra i quattro possibili, parità, condizione, next ATC. Il `Math.Clamp` finale non
  scatta mai, ed è una rete, non una toppa. La parità semicircolare `(cruise / 10) % 2` è quella giusta:
  FL250 → 25 → dispari.
- **La forma canonica dei due lati di un accordo.** Il servizio valida che i lati ci siano e siano diversi;
  a metterli in ordine (`id minore = A`) è il **repository**, in tutte e tre le porte di scrittura — nuovo,
  modifica e ripristino da snapshot. L'unicità della coppia non orientata non dipende da chi chiama.
- **Le regole di sezione degli accordi**: arrivi e partenze pretendono un aeroporto, i sorvoli lo vietano,
  gli ICAO non possono ripetersi. Ognuna con la ragione, e una col riferimento al caso che l'ha prodotta.

## 4e · 4g · 4h · 4i · 4k — passata trasversale sui meccanismi a rischio 🔵

Gli ambiti restano **aperti**: questa non è la loro lettura per intero, è una passata mirata sui meccanismi
dove un difetto costa di più — il tempo che si piega, i lock, i tetti di spesa, i numeri mostrati come veri.
Un finding, e sette meccanismi verificati integri.

### R-022 — La premessa scritta nel `DbContext` è già falsa

`src/Vipi.Infrastructure/Persistence/VipiDbContext.cs:52-55` · **S3** · 🟢 · CONFERMATO

`RuotaTokenDiConcorrenza` — il metodo che assegna un token nuovo a ogni entità versionata a ogni salvataggio,
cioè la garanzia centrale della concorrenza ottimistica — chiude con questo avvertimento:

> *«⚠️ Restano fuori `ExecuteUpdate`/`ExecuteDelete`, che non passano dal change-tracker né da qui. **Oggi è
> innocuo — l'unica entità che li usa (`EditResourceLock`) non ha token** — ma è la condizione da
> ricontrollare prima di convertire una scrittura su entità versionata in `ExecuteUpdate`.»*

La premessa non regge. `Document` **ha** il token (`VipiDbContext:338`), e `EfEditingRepository` lo scrive con
`ExecuteUpdateAsync` in **quattro** punti: acquisizione del lock (1226), rinnovo (1262), rilascio (1268),
sblocco forzato (1284).

**Perché non è S2.** Il comportamento è, in questo caso, quello **giusto**: le colonne toccate sono soltanto
quelle del lock, e ruotare il token a ogni battito del lock — uno ogni pochi secondi — farebbe fallire il
salvataggio di chiunque avesse l'editor aperto. Non ruotarlo è la scelta corretta, anche se nessuno l'ha
scritta.

**Perché conta lo stesso.** Quella frase non descrive: **autorizza**. È la guardia lasciata a chi verrà dopo,
e dice «puoi convertire in `ExecuteUpdate` finché l'entità non ha un token». Chi la legge oggi conclude che
nessuna entità versionata è scritta così — e non è vero da quattro punti. La prossima conversione su
`Document` potrebbe essere una che il token lo deve ruotare (una promozione di bozza, un cambio di stato), e
la guardia avrà detto di sì.

**Rimedio:** correggere la premessa e dire la regola vera — *«su `Document` si scrive con `ExecuteUpdate`
soltanto per le colonne del lock, che di proposito non ruotano il token»* — invece di lasciare in piedi
un'affermazione che un `grep` smentisce in dieci secondi.

### I sette meccanismi verificati integri

| Ambito | Meccanismo | Esito |
|---|---|---|
| 4g ✅ | **Il giorno operativo delle regole pista.** Una finestra 22:00→06:00 vive in due giorni di calendario, e la coda dopo mezzanotte deve contare come il giorno *prima* — o «venerdì notte» diventa sabato | ✅ `GiornoOperativo` sottrae un giorno solo quando la finestra scavalca **e** l'ora sta nella coda. Giorni della settimana, parità e finestra stagionale si valutano tutti sul giorno operativo, non sul calendario |
| 4g ✅ | Finestre di orario e stagionali col **wrap** (22:00→06:00, 1101→0228), fuso italiano con DST e ripiego a UTC se il fuso non c'è | ✅ entrambe con la stessa forma `f <= t ? dentro : fuori-o-dentro`, e la rimappa Domenica→6 per il bitmask |
| 4e | `PolygonGeometry.Contains` | ✅ (già in 4c) |
| 4i ✅ | **Acquisizione del lock**, di documento e di risorsa: `ExecuteUpdate` con la condizione «libero, scaduto o già mio» **dentro la `WHERE`** | ✅ è atomica lato database: due editori non possono prenderlo entrambi. L'inserimento della prima riga cattura la violazione di unicità e ricade sull'ispezione |
| 4k ✅ | **La griglia di copertura.** Due controllori sovrapposti non devono contare doppio, e una sessione a cavallo di un'ora non deve dare il 120% | ✅ si fondono gli intervalli **prima** di contare (`Unione`), i minuti si spalmano sulle caselle attraversate, e il clamp sul bordo della finestra è scritto con la sua ragione |
| 4h ✅ | **Il tetto di spesa di traduzione** si legge dal registro `TranslationSpends` invece che dedurlo dalla memoria — perché la spesa dedotta non vede i segmenti tornati rotti | ✅ e la fotografia iniziale «una volta sola per motore» si chiede al **database**, non a un flag in memoria che un riavvio azzererebbe |
| 4b/4j | `AuditScribe` dopo un `ExecuteUpdate` ha il suo `SaveChanges` esplicito, «perché qui non c'è un salvataggio dell'atto a cui accodarsi» | ✅ |

> **Nota di metodo.** Tre di questi sette sono meccanismi che in altri repository sarebbero difetti quasi
> certi — il giorno operativo, la fusione degli intervalli, l'atomicità del lock. Qui sono corretti **e**
> commentati con il caso che li ha prodotti. Vale la pena scriverlo, perché è la ragione per cui la resa di
> questa fase è bassa: non è che si stia cercando male.

---

# Fase 5 — Autorizzazioni e sicurezza (in corso)

**Stato:** 🔵 **aperta** · fatta **la matrice** (pagine e servizi) · 1 finding

Fatto in questo giro: le due matrici — 49 pagine × cancello, 110 metodi di scrittura × cancello — e la
verifica dei casi che ne escono. Restano: segreti, upload, intestazioni, OIDC, cancelli pubblici.

## Il modello, in una riga

Il livello è **un numero ordinato** (`VipiRole`: User → IvaoStaff → DivisionStaff → Editor → Admin) e ogni
cancello è un `>=`. Il livello effettivo è `max(posizioni staff IVAO, promozione a mano)`. Il contratto
scritto su `IEditAuthorizationService` è esplicito:

> *«Verifica sempre server-side: **quello che la pagina nasconde, il servizio deve comunque rifiutarlo**.»*

È quella frase il metro di questa fase.

## Matrice 1 — le 49 pagine

| | Pagine |
|---|---|
| Con un marcatore di cancello nel proprio file | **39** |
| Senza | **10** |

Delle dieci, sette sono **pubbliche per costruzione** (`/`, la Guida, la ricerca, l'anteprima di release che
è un redirect, l'AoR a schermo intero, il convertitore, il profile swapper). Le altre tre —
`NewDocumentPage`, `VloaEditorPage`, `ScreensIndex` — la `mappa-pagine` le dichiara riservate ad admin,
Editor e staff.

**Verificate una per una: reggono, e non per caso.** Il cancello c'è, ma sta **un piano più sotto**:
`EditingService.LoadForEditAsync` apre con `EnsureAtLeast(VipiRole.Editor)`, e i componenti che lo chiamano
(`VloaEditor` col suo `DocumentEditorShell`, `AppSectionsEditor`, `AirportSectionsEditor`) catturano il
rifiuto e mostrano uno stato «non permesso» invece di un errore. È l'ordine giusto: la porta vera è nel
servizio, la pagina si limita a raccontarlo.

> ⚠️ `NewDocumentPage` inietta `IEditAuthorizationService` **e non lo usa mai**, e il suo commento cita
> `EnsureCanEditAccAsync` come la garanzia — un metodo **morto il 28 agosto 2026** insieme alle concessioni
> per ACC. La garanzia oggi la dà `StructureEditingService`, che apre con `EnsureAtLeast(Editor)` in otto
> metodi. Iniezione morta e commento che nomina un fantasma: da ripulire quando si tocca il file.

## Matrice 2 — i 110 metodi di scrittura dei servizi

Prima passata: 23 senza un cancello **nel proprio corpo**. Seguiti tutti, uno a uno, e **22 sono gatati un
livello più sotto** — il che è la forma giusta, non un difetto:

| Famiglia | Dove sta davvero il cancello |
|---|---|
| `EditingService` (blocchi e sezioni) | `AuthorizeBlockAsync` / `AuthorizeSectionAsync` + `EnsureLockAsync` |
| `AirportEditingService` (7 metodi) | `EnsureLockMineAsync` → `EnsureCanEditAsync` **+** lock mio |
| `AccDocumentService`, `AppDocumentService` | `SaveJsonAsync` / `WithDocumentAsync` → `EnsureAsync` |
| `VloaDerivationService` | `ToggleAsync` apre con `EnsureAtLeast(Editor)` |
| `DocumentImpactService.ClearBySourceAsync` | non è raggiungibile da un utente: unico chiamante `EfSectorProjectionService`, con `byUserId: 0` |
| `*ShapeService.ApplyAsync` | percorsi di sistema (giri d'import), non comandi d'utente |

Il ventitreesimo è R-023.

## R-023 — La biblioteca allegati si difende solo dentro una pagina

**S2** · 🟢 **SUBITO** · CONFERMATO

Le tre scritture della biblioteca — creare una voce, sostituirne il file, cancellarla — non hanno **nessun**
controllo di ruolo lato servizio:

| Strato | Riferimenti a `_authz` / `VipiRole` / `EnsureAtLeast` |
|---|---|
| `AttachmentCurationService` (`ReplaceAsync`, `DeleteAsync`) | **0** |
| `EfAttachmentLibrary` (`CreateAsync`, `DeleteAsync`, …) | **0** |
| `AdminAttachmentsPage.razor` | 4 — tutti sulla **stessa** proprietà di pagina |

L'unica guardia è `private bool PuoScrivere => Authz.Role >= VipiRole.Editor`, usata due volte: per nascondere
i comandi nel markup e come `if (!PuoScrivere) return;` in testa ai tre gestori. Due controlli, **ma dentro
lo stesso componente**. Sotto, il servizio e il repository accettano qualunque chiamante — e l'`userId` per
il registro di audit arriva come **parametro**, cioè lo dichiara chi chiama.

**Scenario di rottura.** Oggi la catena regge perché in Blazor Server un gestore non si invoca senza che il
componente sia reso, e la pagina ha una sola porta. Ma è esattamente la condizione che questo repository si è
scritto contro: *«quello che la pagina nasconde, il servizio deve comunque rifiutarlo»*. Basta una seconda
porta sugli stessi servizi — un pannello nell'editor documenti che offra «sostituisci l'allegato», un comando
in blocco, un endpoint — e non c'è niente sotto a fermarla. Le altre quattro famiglie di scrittura
(documenti, aeroporti, ACC/APP, vLOA) quel piano sotto ce l'hanno tutte: **la biblioteca è l'unica che non
ce l'ha**, ed è anche quella dove l'atto è più distruttivo, perché cancellare una voce lascia orfani i
documenti che la citano.

**Rimedio:** `_authz.EnsureAtLeast(VipiRole.Editor)` in testa a `ReplaceAsync`, `DeleteAsync` e `CreateAsync`,
e l'`userId` letto da `CurrentUserId` invece che ricevuto. Tre righe, nessuna migrazione.

## Verificato e corretto

- **Il servizio degli allegati sul web è pubblico di proposito**, e la ragione è scritta: quel che entra in
  biblioteca è pubblico per costruzione, perché il file sul Drive è condiviso «chiunque abbia il link». Il
  redirect è un **302** e non un 301, così il giorno che cambia il deposito nessuno resta mandato a un
  indirizzo morto.
- **`MarkdownLite`** — il renderer del contenuto documentale — **encoda prima e arricchisce dopo**, conosce
  un solo schema di link (`allegato:slug`) con lo slug vincolato alla sua forma, e il testo dell'ancora è già
  encodato quando la regex gira. Nessuna via per un `javascript:` dentro un `href` che componiamo noi.
- **`AuditScribe`** usa un encoder JSON rilassato — scelta giusta e motivata (il registro si legge anche in
  SQL, e un titolo scappato a metà non lo pesca un `LIKE`), e non è un rischio perché il valore torna da un
  parser JSON e lo rende Blazor, che scappa da sé.

## Fase 5, seconda parte — segreti, upload, intestazioni, OIDC, cancelli pubblici

**Stato:** ✅ **Fase 5 chiusa** · 2 findings in tutto (R-023, R-024)

### R-024 — L'APP nascosto resta pubblico

`src/Vipi.Infrastructure/Persistence/EfContentRepository.cs:52-61` · **S2** · 🟢 · CONFERMATO

Il repository che risolve i documenti **per il pubblico** ha quattro porte. Tre chiudono quando l'oggetto è
nascosto; la quarta no.

| Porta | Come esclude ciò che è nascosto |
|---|---|
| `LoadAirportVipiAsync` | `!d.IsHidden` **e** `!Airports.Any(a => a.Icao == icao && a.IsHidden)` — col commento: *«Aeroporto nascosto dall'admin ⇒ pagina pubblica inaccessibile»* |
| `LoadAirportMilVipiAsync` | identica alla gemella civile |
| `LoadVloaByIdAsync` | `!d.IsHidden` (una vLOA non ha un oggetto di catalogo dietro) |
| **`LoadAppVipiAsync`** | **solo** `!d.IsHidden`. Il settore lo cerca così: `d.Sectors.Any(s => s.IsPrimary && s.Type == App && s.ApproachKind == Standalone && s.Callsign == app)` — **nessun `s.IsActive`** |

È l'unica delle quattro che passa da un `Sector`, cioè dalla **proiezione dei cataloghi**. E la proiezione,
`EfSectorProjectionService:229`, dichiara la premessa opposta:

> *«Il motivo originale resta coperto: **chi risolve un documento filtra su `IsActive`**.»*

Non è vero per questa porta. E gli altri che risolvono sui settori — `EfAccDerivationRepository`,
`EfVloaDerivationRepository`, `EfSectorVolumeCatalog`, `EfStructureEditingRepository` — il filtro ce l'hanno
tutti. **La porta pubblica dell'APP è l'unica senza.**

**Scenario di rottura, due strade per lo stesso esito.**

1. Un amministratore **nasconde** una posizione APP dal catalogo. La proiezione la disattiva
   (`IsActive = false`, riga 236). La sua pagina pubblica **resta in piedi**.
2. La **sorgente smette di mandare** quella posizione (rinomina, riorganizzazione). La proiezione la
   disattiva allo stesso modo. La pagina pubblica continua a servire un documento che descrive una postazione
   **che non esiste più** — e lo fa con la release congelata, quindi con l'aria di un dato buono.

Nessuno dei due casi produce un errore: producono una pagina. Chi ha nascosto la posizione crede di averla
tolta di mezzo, e sull'aeroporto quel gesto funziona davvero — che è ciò che rende difficile accorgersene.

**Rimedio:** aggiungere `s.IsActive` alla `Any(...)` di `LoadAppVipiAsync`, con lo stesso schermo per
l'anteprima che hanno le altre (`preferWorking || ignoreRelease || …`), così l'editor continua a vedere la
bozza di un APP disattivato mentre il pubblico no. Una riga.

### Verificato e corretto

| Area | Esito |
|---|---|
| **Segreti** | Nessuna credenziale nei file versionati. `deploy/atc-ivao/appsettings.Production.json` porta la connection string **senza** `Password=`, con un `"//SEGRETI"` che dice di non metterci niente di segreto «per principio, anche ora che il file…», e un `segreti.esempio.json` accanto che mostra la forma |
| **Upload delle immagini** | `MediaValidator` controlla, in quest'ordine: file vuoto, tetto di byte, **formato vero** (`ImageProbe` sui byte, non sul tipo dichiarato), dimensioni dichiarate valide, lato massimo in pixel, e la quota per documento. Sull'uscita: `nosniff`, ETag sullo sha, cache immutabile — l'URL **è** il contenuto |
| **Intestazioni** | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` che spegne cinque permessi. `UseHsts` e `UseHttpsRedirection` fuori da Development |
| **CSP** | In **Report-Only**, e non per dimenticanza: il commento conta ciò che manca per accenderla — **17** gestori inline nel markup e **554** attributi `style`, misurati l'11 agosto — e spiega che accenderla senza `unsafe-inline` romperebbe stampa, drag&drop e tre elenchi, mentre accenderla *con* non proteggerebbe da niente. `script-src` ha già perso `'unsafe-inline'`, che è il pezzo che conta, e una guardia E2E pretende che la pagina non contenga altri script inline |
| **OIDC** | Codice + **PKCE**, `SaveTokens = false` con la ragione scritta, scope minimi (`openid profile email`), `OnRemoteFailure` gestito con una pagina propria |
| **Cookie** | `HttpOnly`, `SameSite=Lax` e `SecurePolicy=Always` fuori da Development — **scritti invece che ereditati**, «perché un default non dichiarato è un default che qualcuno cambia» |
| **Cancello pubblico delle release** | La visibilità pubblica **è** l'esistenza di una release effettiva: niente release ⇒ invisibile, senza il vecchio ripiego che rendeva pubblica una versione pubblicata senza release |
| **Allegati serviti sul web** | Pubblici di proposito (il file sul Drive è condiviso «chiunque abbia il link»), redirect **302** e non 301 perché il deposito può cambiare |

---

# Fase 6 — UI Blazor

**Stato:** 🔵 **fatta la parte meccanica** · 2 findings · s-01 chiuso

Perimetro coperto: caratteri di controllo, trappole Razor invisibili al compilatore, render mode e isole,
fughe di sottoscrizioni, timer e `StateHasChanged`, nomi accessibili, i18n degli attributi, uso reale degli
asset. **Residuo dichiarato**: CSS in profondità (oltre R-008), foglio di stampa, i18n del contenuto testuale
degli elementi, comportamento su telefono.

## R-025 — Il byte di controllo non era un caso isolato: è un'abitudine

`src/Vipi.Ui/Components/Doc/UnionMembersEditor.razor:136` · **S2** · 🟢 · CONFERMATO

Stessa specie di **R-001**, e proprio per questo cambia natura: due file, tre byte, due autori diversi dello
stesso gesto. Qui i separatori sono scritti **come byte veri** dentro il sorgente — `0x1F` (unit separator)
fra le voci, `0x1E` (record separator) fra i campi:

```csharp
private static string Firma(IReadOnlyList<EditorTocItem> voci) =>
    string.Join('\u001F', voci.Select(v => $"{v.AnchorId}\u001E{v.Label}\u001E{v.GroupLabel}"));
// ↑ nel file quei due non sono escape: sono i BYTE 0x1F e 0x1E scritti dentro il sorgente
```

**A che cosa serve la firma.** A decidere se l'indice unito è cambiato: `if (Firma(voci) != Firma(_ultime))`.
Il commento sopra racconta il difetto che l'ha fatta nascere — *«il confronto era sulle sole ancore, quindi
rinominare una sezione di un membro non rinfrescava l'indice unito: chi rinominava vedeva la card prendere
il nome nuovo e il menu a sinistra restare col vecchio»*.

**Scenario di rottura.** Il file è binario per ogni strumento testuale: `grep` lo salta, `git diff` non lo
mostra, e una normalizzazione qualunque può togliere quei byte in silenzio. Senza separatori la firma diventa
una concatenazione, due elenchi diversi possono produrre la stessa stringa, e il confronto smette di vedere
un cambiamento — cioè **si riapre esattamente il difetto che quel commento dice di aver chiuso**, e si
riapre senza che nessun test cada.

**Rimedio:** scriverli come escape (`'\u001F'`, `"\u001E"`). Il file torna testuale, e il comportamento non cambia di un
bit. Insieme a R-001 sono due righe.

> ⚠️ **La lezione che vale più del fix**: dopo il primo caso nessuno ha guardato se ce n'erano altri. Un
> controllo che rifiuti i byte di controllo nei sorgenti — dieci righe di test — chiude la famiglia invece
> dei due esemplari.

## R-026 — Tredici etichette e nove segnaposto non seguono la barra della lingua

**S4** · 🟢 · CONFERMATO

`docs/design/regole-lingua.md` **R6**: *«Tutto il resto segue la lingua scelta nella barra.»*

| | Quante | Esempio |
|---|---|---|
| Etichette d'interfaccia scritte a mano | **13** | `AirportSidsEditor` ne ha **sette**: `title="Condition"`, `aria-label="Transition"`, `aria-label="Type"`, `aria-label="Initial climb"` — inglese fisso in un editor usato in italiano |
| Segnaposto con «es. » | **9** | `placeholder="es. GINEL"`, `"es. 1000 ft"`, `"es. Marseille ACC"` — italiano fisso, che resta italiano quando il sito si legge in inglese |

Le due metà sbagliano in **direzioni opposte** e per lo stesso motivo: la stringa non è passata dai `.resx`.
Colpisce soprattutto chi usa un lettore di schermo, perché sette delle tredici sono `aria-label` — cioè
proprio il testo che **esiste solo** per chi non vede l'icona.

> Restano fuori, e correttamente: `aria-label="AoR …"` (sigla che non si traduce, R9) e i segnaposto che sono
> **formati** e non prosa (`LIBG`, `FL75`, `loa-lirr-lfmm`, `N41°32'05.07''E015°43'42.47''`).

## Verificato e corretto

| Meccanismo | Esito |
|---|---|
| **Render mode e isole** | **Zero** pagine con gestori d'evento e senza `@rendermode` interattivo. La regola «chrome statico, pagine isole» non ha eccezioni |
| **Fughe di sottoscrizioni** | Nessun componente si iscrive a un evento senza staccarsi. Le tre segnalazioni della sonda erano `+=` su variabili locali |
| **Timer e thread** | Un solo `PeriodicTimer`, nel battito del lock — e legge lo stato **dentro** `InvokeAsync`, «perché è lì che `_lock` viene scritto». Il commento fa anche il conto: TTL 3 minuti, periodo 60 s, soglia d'allarme 60 s ⇒ l'avviso può scattare solo dopo **due** battiti falliti di fila |
| **Nomi accessibili** | **Zero** bottoni senza testo, `aria-label` o `title` |
| **Commenti fra gli attributi** | Zero (è la trappola che dà 500) |
| **`<text>` nel DOM** | 41 usi, e una guardia che lo verifica — ma su **un** componente solo. Vedi s-13 |
| **Asset «mai citati»** | s-01 **chiuso**: `vipi-live.js` espone `window.vipiLive` e lo chiamano `LiveBadge` e `LivePage` via interop; `vipi-theme-mode.js` è una IIFE che deve girare nel `<head>` *prima* del primo disegno, o chi ha scelto il tema scuro vede un lampo bianco. La sonda di Fase 0 cercava il prefisso sbagliato |

## Sospetto nuovo

| # | Sospetto | Dove si decide |
|---|---|---|
| s-13 | La guardia «nessun `<text>` nel markup reso» esiste in `SezioniAeroportoTests` e copre **un** componente. Gli altri 40 usi non hanno nessuno che verifichi che siano dentro un blocco di codice — e fuori da un blocco `<text>` non è un comando Razor, è un tag che finisce nel DOM | Fase 7 |

---

# Residuo colmato — Fase 6 completa, Fase 4 chiusa

**Stato:** ✅ **Fase 6 chiusa** (1 finding nuovo) · ✅ **Fase 4 chiusa** (0 findings nuovi, 1 rinforzo a R-014)

## Fase 6 — il residuo

### R-027 — La regola dice «zero, ed è verificato». Non lo verifica nessuno

`docs/design/regole-brand.md:9-20` · **S3** · 🟢 · CONFERMATO

`regole-brand` §1 descrive `vipi-theme.css` a tre livelli e chiude la tabella così:

| livello | esadecimali ammessi |
|---|---|
| 1 — la scala di brand | **sì, solo qui** |
| 2 — i ruoli · 3 — le tinte | no |
| **corpo del foglio** | **no — zero, ed è verificato** |

**Non c'è niente che lo verifichi.** I test sul tema esistono e sono parecchi — la minificazione che non
perde variabili, il rosso dell'avviso che *viene dal token*, la gerarchia dei titoli, la regione live, il
piè di stampa — ma nessuno conta i letterali. E in assenza del controllo ne sono entrati **quattro** che le
tre eccezioni scritte non coprono:

| Dove | Letterale | Perché non è coperto |
|---|---|---|
| `vipi-theme.css:2556` (`.mva-label`) | `color:#1a1a22` | L'eccezione «cartografica» vale per un `<input type=color>`, il DB o un attributo SVG — non per una regola CSS |
| idem | `border:1px solid rgba(193,18,31,.5)` | L'eccezione del **velo** ammette solo bianco e nero: questo è il rosso di brand scritto a mano |
| `vipi-theme.css:4251` e `4336` | `--nbr-ink:#b39dfa` (chiaro e scuro) | È un token di **livello 2**, dove l'esadecimale è vietato |

⚠️ E il terzo caso è il più interessante, perché ha una **ragione buona scritta accanto**:
*«product.creators (#8b5cf6) sul fondo scuro fa 4.09:1, sotto AA: schiarito»*. Cioè una correzione di
**contrasto**, che le tre eccezioni non contemplano. Il difetto quindi non è il CSS: è la **regola**, a cui
manca una quarta eccezione — «una tinta corretta per raggiungere AA, con il rapporto misurato scritto
accanto» — oppure la forma derivata (`color-mix(… var(--ivao-color-product-creators) …, white)`) che
resterebbe dentro la regola.

**Rimedio:** il test che la regola dichiara già di avere. Conta i letterali fuori dal primo `:root` e
confronta con una lista di eccezioni **nominata**: dieci righe, e la frase «ed è verificato» diventa vera.

### Verificato e corretto nel residuo

| Cosa | Esito |
|---|---|
| **Testo scritto a mano nel markup** | La sonda ne segnalava 249, **tutti in `GuidaPage`** — e sono **falsi positivi**: la Guida è bilingue con un helper `T("italiano","english")` sulla stessa riga, e una sonda a righe ne vede solo una metà. Nessun testo monolingua nel markup |
| **Foglio di stampa e `<details>`** | La trappola è chiusa **due volte**: `beforeprint` apre i `<details>` da JS, e il CSS ha la rete — `details:not([open]) > .cb-body { display:block !important }` più `::details-content { content-visibility: visible }`, perché in Chrome il contenuto di un `<details>` chiuso non si stampa |
| **Rotture di pagina** | Sette regole fra `break-inside`, `break-before`, `orphans`/`widows` |
| **Isolamento del foglio di stampa** | 119 selettori sotto `.vipi-root` su 82 regole — l'opposto di `vipi-theme.css` (R-008). Qui l'isolamento c'è |

## Fase 4 — il residuo

Chiusi 4e, 4f, 4g, 4h, 4i, 4j, 4k sui meccanismi che restavano. **Nessun finding nuovo**, e sette verifiche
che vale la pena aver fatto perché ognuna è un difetto classico che qui non c'è:

| Ambito | Meccanismo | Esito |
|---|---|---|
| 4e | **Guardia anti-ciclo** (`HierarchyRules.EnsureNoCycle`) | ✅ risale i padri con un indice posizione→profondità e restituisce **l'anello**, non un booleano. E lavora sull'albero **effettivo**, che è la correzione del difetto vero: in produzione `LIMF_WW0_APP → LIMF_WN0_APP → LIMF_WW0_APP` era invisibile perché `WW0` non aveva un padre *scritto* |
| 4g ✅ | **Scaletta delle posizioni d'aeroporto** | ✅ sale di gradino in gradino (DEL→GND→TWR→APP), e se un gradino è ambiguo **sale invece di tirare a sorte**; un gradino che darebbe una risposta ciclica vale come ambiguo |
| 4h ✅ | **La lingua nello snapshot** | ✅ `ConLinguaDelDocumento` ricostruisce lo snapshot con la lingua del documento **vivo** — il difetto che la memoria del progetto registra come pagato — e `LanguageLocked` spegne la traduzione a monte |
| 4j ✅ | **La sentinella «aperto» degli impatti** | ✅ `ClearedUtc` è NOT NULL con sentinella (MariaDB non ha indici unici parziali), e **tutti e dieci** i punti di lettura confrontano con `DocumentImpact.Aperto`, mai con `null`. Chi chiude si difende perfino dal chiudere *con* la sentinella |
| 4i ✅ | Lock di documento e di risorsa | ✅ (già in Fase 4 trasversale) |
| 4f ✅ | Forma canonica dei lati, regole di sezione | ✅ (già in 4d) |
| 4k ✅ | Griglia di copertura, tetto di traduzione | ✅ (già in Fase 4 trasversale) |

### Rinforzo a R-014

Chiudendo 4e è emersa una **seconda conseguenza** del finding, che vale più della prima.

`EnsureNoCycle` pretende la mappa dei padri **effettivi**, e il commento spiega perché con l'incidente che
l'ha prodotta: *«due alberi diversi, e il difetto stava nella differenza»*. Quella mappa la costruisce
`EffectiveHierarchy.ParentMap` — cioè proprio il metodo che, se lo stesso callsign esiste nei due cataloghi,
**ne perde uno in silenzio** (R-014).

Vuol dire che R-014 non produce soltanto una gerarchia sbagliata: fa **validare alla guardia anti-ciclo un
albero che non è quello vero**, cioè riapre esattamente la differenza fra due alberi che quella guardia è
stata scritta per chiudere. La severità resta **S2** e il verdetto **PLAUSIBILE** — le collisioni misurate
oggi sono zero — ma il rimedio sale di priorità: non è una svista di igiene, è la premessa di una guardia.

---

# Fase 7 — I test

**Stato:** ✅ **chiusa** il 7 settembre 2026 · 1 finding · s-13 chiuso

Perimetro: 3 992 test su 471 file, il parallelismo, le fragilità, e — la parte che conta — **la copertura
del rischio**: per ognuno dei findings di questa revisione, quale test l'avrebbe preso.

## Esito in una riga

**La suite è sana e disciplinata; quello che le manca non sono test, è un metro.** Nessun test disattivato,
nessuno senza asserzione, il parallelismo deciso con una misura e non a sentimento, e un pattern
d'autorizzazione già in tredici file. Ma **niente conta quanti test girano**, e nove findings su nove sono
sfuggiti a forme di test che la suite sa già scrivere: non mancava la tecnica, mancava che qualcuno le
puntasse lì.

## La copertura del rischio — i nove S2 di questa revisione

Per ognuno: quale test l'avrebbe preso, e quanto costa.

| Finding | Il test che l'avrebbe preso | C'è? | Costo |
|---|---|---|---|
| **R-001 · R-025** byte di controllo nel sorgente | Un test che rifiuta i byte di controllo nei file di `src/` | ❌ | ~10 righe, e **chiude la famiglia** invece dei due esemplari |
| **R-014** stesso callsign nei due cataloghi | `ParentMap` con una riga ACC e una aeroporto **dallo stesso callsign**: deve fare rumore, non scegliere | ❌ | Il metodo è **puro**: cinque righe |
| **R-015** transazione fuori dall'execution strategy | Un `VipiDbContext` montato su una strategy **che ritenta**, e i percorsi transazionali chiamati | ❌ — e oggi **non può** esistere: tutti i test girano su SQLite, che una strategy non ce l'ha | La sonda scritta in Fase 3: ~30 righe. È il finding che nessun test *poteva* prendere |
| **R-016** sentinella mancante | Una corsa temporale: difficile da scrivere e fragile da tenere | ⚠️ | Meglio una **guardia strutturale**: «ogni handler che tocca un repository EF ha una sentinella o uno scope proprio», sullo stesso stampo delle guardie di forma già presenti |
| **R-018** vertice malformato che tronca la forma | Il parser è **puro**: gli si dà un file con una coordinata storta e si pretende `irrisolti`, non un anello corto | ❌ | Banale |
| **R-020** `rowspan` | `TabellaHtml.Leggi` con una tabella che unisce in verticale: tutte le righe devono avere la stessa larghezza | ❌ | Banale, la funzione è pura |
| **R-023** biblioteca senza cancello | Chiamare il servizio da anonimo e pretendere `EditNotAllowedException` | ❌ **e il pattern esiste**: tredici file lo fanno già per le altre famiglie. `PaginaAllegatiTests` copre ogni *messaggio* di rifiuto e nessun *permesso* | Due righe per porta |
| **R-024** APP nascosto pubblico | Disattivare il settore e pretendere `null` dalla porta pubblica | ❌ | Facile |
| **R-027** letterali nel CSS | Il test che `regole-brand` **dichiara già di avere** | ❌ | ~10 righe |

> **Che cosa dice questa tabella.** Sette findings su nove sarebbero stati presi da un test della stessa
> forma di quelli che la suite scrive già benissimo — un metodo puro, un ingresso storto, un'asserzione. Non
> è una lacuna di tecnica: è che nessuno aveva puntato la tecnica lì. L'unica eccezione vera è **R-015**, che
> nessun test *poteva* prendere perché la differenza sta fra i provider e i test ne vedono uno solo.

## R-028 — Nessuno conta i test, e la trappola ha già colpito una volta

`.github/workflows/ci.yml:31` · **S3** · 🟢 · CONFERMATO

`dotnet test` sulla soluzione **non fallisce** quando un progetto sparisce dalla corsa: la riga esce zero e
la CI diventa verde su meno test di ieri. In CI il caso peggiore — un progetto che non compila — lo prende
il `dotnet build` che sta prima. **Ma niente controlla il numero**, e la differenza si è già pagata:

> *«Dall'11 agosto 2026 questa riga esegue ENTRAMBI i TFM di ogni progetto multi-target: prima erano net10
> tutti tranne `Vipi.Infrastructure.Tests`, cioè **~1000 test che non toccavano mai il runtime di
> produzione**.»* — il commento della riga stessa

Mille test che giravano sul runtime sbagliato per settimane, e a scoprirlo è stato un ragionamento, non un
cancello. Oggi la stessa cosa può succedere di nuovo in silenzio: basta che un `.csproj` perda un TFM, che
un filtro escluda una classe, che un progetto esca dalla soluzione — **la CI resta verde e nessuno vede che
i test sono meno**.

**Scenario di rottura.** Un `TargetFrameworks` che diventa `TargetFramework` in una modifica innocua:
5 543 test diventano 4 240, tutto verde, e il ramo net8 — cioè **la produzione** — smette di essere provato.
È esattamente il difetto dell'11 agosto, con la stessa causa e la stessa invisibilità.

**Rimedio:** far scrivere alla CI il conteggio per progetto e per TFM e confrontarlo con un atteso versionato
(`--logger trx` più tre righe di script, oppure `dotnet test` per progetto come ho fatto in Fase 0). Non
serve la precisione: serve che **calare** faccia rumore.

## Verificato e corretto

| Cosa | Esito |
|---|---|
| **Test disattivati** | **Zero** `Skip=` in tutta la suite |
| **Test senza asserzione** | 21 su 3 992, e **tutti legittimi**: o passano da un helper che asserisce (`PretendiConflitto`, che regge da solo i tre test della concorrenza ottimistica), o sono «non deve sollevare», che in xUnit *è* l'asserzione |
| **Parallelismo** | Disattivato in `Vipi.E2E.Tests` e **solo lì**, con la ragione e la misura: il file di diagnostica d'avvio è uno per processo e dieci host in parallelo se lo portavano via a vicenda. Costo dichiarato: 43 s → 78 s, «pagati una volta per corsa in cambio di un cancello di cui ci si può fidare» |
| **Autorizzazione** | Tredici file pretendono `EditNotAllowedException`: il pattern c'è, ed è per questo che la sua assenza sugli allegati (R-023) è una svista e non una scelta |
| **Orologio vero** | 186 usi di `UtcNow` in 66 file di test — ma i motori che decidono (`RunwaySuggestion`, `SogliaEliminazione`, `SidStampCycle`, `AiracService`) prendono **l'istante come parametro**, quindi la fragilità resta sul contorno e non sul verdetto |
| **s-13** | ✅ **chiuso**: la guardia «nessun `<text>` nel markup reso» esiste in `SezioniAeroportoTests` e copre un componente. Non è un difetto: è la **prima** occorrenza del genere di guardia che la tabella qui sopra chiede di estendere |

---

# Fase 8 — I documenti

**Stato:** ✅ **chiusa** il 7 settembre 2026 · 3 findings

Tre confronti distinti, come previsto: doc↔doc, doc↔codice, **stato↔realtà**. I primi due avevano già dato
sei findings nelle fasi precedenti (R-006, R-007, R-011, R-012, R-013, R-027); questa fase chiude il terzo,
che è il più scomodo — perché riguarda i documenti che qualcuno legge **quando non sa niente**.

## R-029 — La sezione «da leggere per prima» manda su un ramo che non esiste

`docs/lavori-aperti.md:329-341` · **S3** · 🟢 · CONFERMATO

`lavori-aperti.md` ha una sezione che dichiara sé stessa così:

> `## Dove siamo, in cinque righe`
> *«Riscritto il 30 agosto 2026 … **È la sezione da leggere per prima quando si riprende senza contesto**:
> dice dov'è il codice, cosa manca e cosa va fatto prima del prossimo deploy.»*

Ed è la prima cosa che dice:

> 🔴 **30 agosto, sera — LA CONSEGNA È A METÀ, E QUESTO VIENE PRIMA DI TUTTO**
> *«Nove commit sul ramo `consegna-db-20260830`, **NON SPINTI**. … Prima cosa da fare riprendendo:
> `git push -u origin consegna-db-20260830`.»*

**Tre fatti, misurati.**

| Cosa dice | Cosa è vero |
|---|---|
| Nove commit da spingere sul ramo `consegna-db-20260830` | Il ramo **non esiste**, né locale né su origin: è stato fuso (`549e9357`) e cancellato. Non c'è niente da spingere |
| «La consegna è a metà, e questo viene prima di tutto» | Da allora sono uscite **quattro** consegne — 1.11.0, 1.12.0, 1.13.0, 1.14.0 — e `main` è al 6 settembre |
| «in cinque righe» | La sezione è lunga **707 righe** |

**Scenario di rottura.** Il lettore a cui questa sezione parla è **per definizione** quello che non ha altro
contesto: una persona nuova, o l'autore dopo una pausa. Fa la prima cosa che gli viene detta, `git push` su
un ramo che non c'è, e non capisce; poi legge che la consegna è a metà e va a cercare che cosa manca, mentre
in produzione sta girando roba di quattro versioni dopo. **Il documento non è vecchio in un punto: è vecchio
esattamente nel punto in cui si offre come mappa.**

⚠️ E non è che il documento non sappia: le quattro consegne successive **ci sono**, nel corpo (1.11.0 vi
compare 29 volte). Il difetto è che la sezione d'ingresso non è stata riscritta da allora, e sopra di lei
stanno **328 righe** di cronologia — 62 voci `**Aggiornato:**` — che raccontano stati passati **senza dire
che sono passati**. Ci sono cascato anch'io: una riga della cronologia («il ramo `statistiche-atc` è completo
e NON fuso») l'ho presa per stato corrente, e ho dovuto guardare la struttura del file per capire che era
storia.

**Rimedio:** riscrivere le cinque righe — e che siano cinque —, e mettere la cronologia **sotto** la sezione
di stato o dietro un `<details>`. Il valore di quel documento non è quanto racconta: è che la prima schermata
dica il vero.

## R-030 — L'indice si dichiara «di tutti i documenti» e ne mancano 44

`docs/index.md` · **S3** · 🟢 · CONFERMATO

L'indice si apre così: *«Mappa **di tutti** i documenti del progetto, con scopo e stato»*, e propone un
**ordine di lettura consigliato per una nuova chat**.

Documenti sotto `docs/`: **167**. Citati dall'indice: **123**. **Ne mancano 44.**

| Cartella | Non citati |
|---|---|
| `feature` | **33** |
| `history` | 6 |
| `design` · `refactor` | 2 + 2 |
| `guide` | 1 |

I 33 mancanti di `feature` sono il caso che conta: le carte di funzionalità sono il posto dove sta scritto
**perché** una cosa è fatta così, e sono esattamente ciò che questa revisione ha usato per distinguere una
scelta da una svista. Chi arriva dall'indice non sa che esistono.

È la **stessa specie di R-012** (§9.8 che si dichiara «la lista migrazioni autoritativa» e si ferma alla 85ª
di 114): non un elenco incompleto — un elenco che **dichiara di essere completo** e non lo è. E il rimedio è
lo stesso: o l'elenco lo genera un comando, o non promette di essere tutto.

## R-031 — Una consegna su cinque non è mai entrata nel registro

`HANDOFF.md` · **S4** · 🟢 · CONFERMATO

`HANDOFF.md` è aggiornato al **6 settembre** e apre con 1.14.0 pronta: la testata è viva. Ma la **1.12.0**
non compare **da nessuna parte** nel file — zero occorrenze — mentre 1.11.0, 1.13.0 e 1.14.0 ci sono con
sette, sette e cinque menzioni.

La 1.12.0 è esistita davvero: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.12.0.md`, timbro `1.12.0 · e5077ab9`,
6 settembre 2026.

Da solo è un buco piccolo. Conta perché `HANDOFF.md` è il documento che risponde a «che cosa è successo»:
un anello mancante nella catena non si vede finché qualcuno non risale proprio quello — e allora non trova
né la ragione né il contenuto di quella consegna.

## Verificato e corretto

| Cosa | Esito |
|---|---|
| **I «NON fuso» ancora scritti** | Dieci occorrenze, e **tutte legittime**: stanno nella cronologia in testa, cioè in voci datate che raccontano un momento. La fusione poi c'è, e il documento la registra (§A17: «`main` è stato allineato, e i sei rami sono stati cancellati») |
| **La convenzione di superamento degli ADR** | Esiste e funziona: ADR-0001 porta due «⚠️ Emendato da ADR-0002», ADR-0005 estende ADR-0002 dichiarandolo. Dove manca — §9.13 di `modello-dati`, R-011 — è una svista dentro una regola che c'è, non l'assenza della regola |
| **La testata di `HANDOFF.md`** | Al 6 settembre, con sha256, dimensione, numero di file e timbro del commit: è il documento di stato che funziona meglio |
| **`mappa-pagine.md`** | Corrente (25 agosto + note successive), e marca da sé le rotte assorbite («ex `/vsop/editor` assorbito») |

---

# Fase 9 — Build, consegna, host, strumenti

**Stato:** ✅ **chiusa** il 7 settembre 2026 · 2 findings

## R-032 — Cinque artefatti di consegna descrivono strade non prese, e nessuno lo dice

**S3** · 🟢 · CONFERMATO

Questo repository sa marcare ciò che è superato, e lo fa bene. `deploy/mysql/README.md` si apre così:

> ⛔ **SUPERATO dal 6 agosto 2026 — usare `../mariadb/README.md`.** *«Il server di `atc.it.ivao.aero` è
> MariaDB 11.4.10, e il provider non è più quello di Oracle ma Pomelo: questa ricetta monta un MySQL 8.4 su
> cui il ramo di produzione non gira più. Resta come storia … non come istruzione.»*

È il modo giusto. **Cinque artefatti non l'hanno avuto:**

| Artefatto | Ultimo tocco | Chi lo cita |
|---|---|---|
| `fly.toml` | 25 lug | **nessuno**, in tutto il repository |
| `docker-compose.yml` | 25 lug | **nessuno** |
| `Caddyfile` | 25 lug | solo `docker-compose.yml`, che a sua volta non è citato da nessuno |
| `deploy/oracle/README.md` | 30 lug | **nessuno** — è la ricetta «VM Oracle Cloud + Docker Compose + Caddy + DuckDNS», cioè il consumatore dei tre file qui sopra |
| `deploy/atc-ivao/nginx-vipi.conf` | 25 ago | i runbook — che però dicono che **non si usa** |

I primi quattro sono un gruppo solo: la strada «container su una VM gratuita», abbandonata quando la
produzione è diventata **Plesk + Passenger su `atc.it.ivao.aero`** e l'anteprima **Render + Neon**. Nessuno
dei quattro porta un marcatore; `deploy/oracle/README.md` si presenta con lo stesso tono di runbook di
`deploy/render/README.md`, che invece è vivo.

Il quinto è più fine, e sta **nella cartella della consegna vera**. `LEGGIMI-DEPLOY.md` descrive nel corpo il
deploy con **systemd** (`/opt/vipi`, `vipi.service`, nginx davanti), e in testa ha un riquadro onesto:

> 📌 *«Se il sito gira su Plesk con Phusion Passenger (non con systemd) … Passenger si riavvia toccando
> `tmp/restart.txt`, non con `systemctl`. E **`nginx-vipi.conf` non viene usato**.»*

Cioè: **il testo principale descrive l'host che non è quello vero**, e la realtà è un riquadro aggiunto sopra.
Il file di configurazione che il riquadro dichiara inutilizzato resta nella cartella, senza marcatore, accanto
ai diciannove fogli di consegna veri.

**Scenario di rottura.** Chi deve consegnare — e questo prodotto lo consegna **una persona sola, via FTP** —
apre `deploy/`, trova sei cartelle e un runbook che parla di `systemctl`, e deve capire da sé che quella non è
la sua strada. Il rischio non è teorico: la differenza fra i due modelli riguarda **dove stanno le chiavi di
Data Protection** e **come si riavvia**, cioè le due cose che, sbagliate, lasciano il sito giù o gli utenti
sloggati.

**Rimedio:** il marcatore che il repository usa già. Quattro righe in cima a quattro file, e in
`LEGGIMI-DEPLOY.md` invertire il peso — Passenger nel corpo, systemd nel riquadro.

## R-033 — Un file di zero byte chiamato `--nologo`, versionato

**S4** · 🟢 · CONFERMATO

Nella radice del repository c'è un file **tracciato da git**, di **0 byte**, il cui nome è `--nologo`
(27 agosto 2026). È il residuo di un comando in cui l'opzione è finita a fare da nome di file.

Non rompe niente. Vale una riga perché il nome comincia con due trattini: chi prova a toglierlo con
`rm --nologo` non ci riesce — serve `rm -- --nologo` o `git rm -- --nologo` — e perché un file così è il
genere di cosa che resta per anni proprio in quanto nessuno la guarda.

## Verificato e corretto

| Cosa | Esito |
|---|---|
| **Artefatti di build versionati** | **Zero** file sotto `bin/` o `obj/` tracciati; `artifacts/` non è in git |
| **I 26 `packages.lock.json`** | Presenti, uno per progetto, e **nessuno modificato** rispetto a git: la regola del RID dichiarato (5 settembre) tiene, e il «ballo» dei lock è chiuso davvero |
| **`tools/prepara-pacchetto.ps1`** | Ha **due reti** sui segreti: il nome del file e il **contenuto** dei file di testo, cercando **le chiavi e mai i valori** — così l'esito si può incollare in una chat. E porta la correzione della prima stesura, che accusava `Vipi.Host.dll` perché dentro un assembly la stringa «ClientSecret» c'è come nome di configurazione: *«un allarme che suona a ogni consegna su un file che dev'esserci non è un allarme»* |
| **I fogli di consegna** | 19 fogli di pacchetto e 8 di correzione, ognuno col proprio **timbro** (`1.12.0 · e5077ab9`) e la propria data: un archivio che si identifica da sé, non una pila ambigua |
| **`Dockerfile`** | Vivo: lo costruisce e lo prova il job `docker` della CI. È l'unico dei quattro artefatti «container» che qualcuno usa |
| **`deploy/mariadb/README.md`** | 293 righe, ed è la ricetta che serve davvero: MariaDB 11.4.10 identica in locale, «perché il 3306 loro è su localhost del loro server» |

---

# Fase 10 — Prestazioni

**Stato:** ✅ **chiusa** il 7 settembre 2026 · **0 findings nuovi** · 4 misure, 1 proposta **scartata sulla misura**

L'audit del 27 agosto ha già fatto il lavoro grosso, e l'ha fatto misurando (336 → 113 KB alla prima visita,
465 → 153 query all'avvio, due interventi su dieci **scartati** perché la misura ha ribaltato l'ipotesi).
Questa fase non lo rifà: cerca ciò che è rimasto, con la stessa regola — *una proposta senza una misura non
è una proposta*.

## Le misure

### 1. Il peso degli asset, oggi

| File | grezzo | gzip -9 |
|---|---|---|
| `vipi-theme.css` | 377 698 | **109 394** |
| `vipi-ui.js` | 60 697 | 20 049 |
| `vipi-aor3d.js` | 36 996 | 12 679 |
| `vipi-aor.js` | 36 285 | 12 255 |
| `vipi-print.css` | 23 485 | 8 531 |
| *(altri 13)* | | |
| **totale** | **631 881** | **198 111** (31%) |

Il foglio del tema è **il 60% dei byte grezzi e il 55% di quelli compressi**, prima ancora della
minificazione. Non è un difetto in sé — copre un'interfaccia amministrativa grande — ma è il posto dove
qualunque guadagno si trova.

### 2. Quanto del foglio è morto: **quasi niente**

Classi definite: **1 129**. Mai viste in un `class="…"` né in una stringa del JS: **63** — e la maggior parte
sono falsi positivi (`components-reconnect-*` è di Blazor, `leaflet-container` è di Leaflet, `ctr`/`gnd`/`fss`
si compongono a runtime dal tipo di posizione). **Il 94% delle classi è usato**: non c'è CSS morto da togliere.

### 3. La proposta che sembrava ovvia, e la misura che la scarta

**Ipotesi:** un lettore anonimo di un documento pubblico scarica anche il vestito dell'intera interfaccia di
editing. Dividere il foglio in «pubblico» e «admin» gli risparmierebbe byte.

**Misura:** delle 2 031 regole, quelle il cui selettore cita **solo** classi che compaiono esclusivamente nei
41 componenti di editing/admin sono **233**, per **21 839 byte su 192 381** di testo delle regole — **l'11%**.
Sui 109 KB compressi del foglio fanno **~12 KB** risparmiati a chi legge e basta.

**Verdetto: non vale.** Dodici kilobyte compressi, una volta sola perché poi la cache tiene, in cambio di una
seconda richiesta, di due file da tenere allineati e del rischio che una regola finisca dal lato sbagliato —
in un foglio dove il 97,6% delle regole non è nemmeno confinato (R-008). Se un giorno si tocca quel foglio
per R-008, **allora** la divisione si fa nello stesso giro e costa quasi zero; da sola non si paga.

### 4. Il radar delle allocazioni non punta su nessun percorso caldo

| Regola | In `src` | Dove stanno davvero |
|---|---|---|
| `CA1861` — array costante allocato a ogni chiamata | 165 | Migrazioni (girano una volta) e seed. Il primo file non-migrazione è `SpecTabelle` (8), che è un import |
| `CA1873` — log costoso valutato comunque | 62 | Tutti in servizi ospitati e nell'avvio: 16 in `VipiModuleExtensions`, il resto nei giri d'import. **Nessuno su un percorso di richiesta** |
| `CA1848` — logging senza delegati | 115 | Stessa distribuzione |

Nessuno di questi costa qualcosa a chi apre una pagina.

## Il vero problema di capacità è un bug, non una scelta

L'unica crescita **senza freno** del sistema non viene da un dimensionamento sbagliato: viene da **R-015**.

Misurato sul database di sviluppo (23,2 MB):

| Tabella | Righe |
|---|---|
| `AtcSessions` | **23 816** |
| `AirportDayTraffic` | 16 650 |
| `AtcSessionTraffic` | 2 900 |
| `AtcMonthRollups` | 305 |
| tutto il resto (documenti, release, audit, traduzioni, media) | poche centinaia |

Le sessioni coprono **esattamente un anno** (5 settembre 2025 → 6 settembre 2026), che è la finestra dei 366
giorni; e dal 1° settembre ne sono entrate **1 579 in cinque giorni** — circa 316 al giorno, ~115 000 l'anno
al ritmo attuale, con l'archivio mondiale acceso solo dal 28 agosto.

La potatura che dovrebbe tenere quella finestra ferma a 366 giorni **su MariaDB non gira mai** (R-015), e
fallisce in un modo che non si vede: la categoria resta in errore in `/services/vsop/admin/sources` e riprova
ogni ora. Quindi il dimensionamento scritto — ~230 MB a regime **con** la potatura — è il numero di un sistema
che oggi non esiste.

**Non serve un intervento di prestazioni: serve il fix di R-015.** È la ragione per cui quel finding, che
sembra un dettaglio di transazioni, sta in cima alla lista delle cose da caricare.

## Che cosa resta da misurare dal vivo (e perché non l'ho fatto qui)

La regola del progetto è che una proposta si misura guidando l'app, non leggendola. Qui non l'ho guidata:
serve una copia del database e la skill `verifica-live`. Le tre misure che varrebbe la pena prendere:

| Da misurare | Perché |
|---|---|
| Query per pagina sui **quattro viewer pubblici**, col log di EF | L'audit contò 465 → 153 all'**avvio**; nessuno ha contato le query **per pagina** dopo i documenti uniti e le carte d'aeroporto |
| Peso della **prima visita** oggi, con un browser vero | I 113 KB sono del 27 agosto: da allora sono entrate cinque consegne |
| Tempo del **primo disegno** di un documento con release congelata contro uno live | La release serve a non ri-derivare: quanto vale, in millisecondi, non l'ha misurato nessuno |
