# Revisione totale del codice — aperta il 6 settembre 2026

**Ramo:** `revisione-totale` (da `main` `2b33791a`) · **Stato:** 🔵 **in corso — Fasi 0-3 chiuse, Fase 4 aperta (4a-4c fatti), 19 findings**

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
| **4** | Application — undici ambiti funzionali (vedi sotto) | 🔵 **in corso** — 4a ✅ · 4b ✅ · 4c ✅ · 4e parziale · 3 findings |
| **5** | Autorizzazioni e sicurezza: matrice completa, guardia nel *service*, cancelli pubblici, segreti, upload | ⏳ |
| **6** | UI Blazor: render mode e isole, difetti Razor invisibili al compilatore, JS, CSS, i18n, stampa, accessibilità | ⏳ |
| **7** | Test: copertura del **rischio**, test che passano sempre, fragilità, la trappola dell'uscita zero | ⏳ |
| **8** | Documenti: doc↔doc, doc↔codice, stato↔realtà | ⏳ |
| **9** | Build, consegna, host, strumenti: config di deploy vive e morte, `.github`, lock file, i 7 tool, runbook | ⏳ |
| **10** | Prestazioni, misurate dal vivo e **divise per operazione** | ⏳ |
| **11** | Sintesi: registro ordinato, le due liste (🟢🟡 subito / 🔴 dopo il 16), lotti di rimedio | ⏳ |

### Fase 4 — gli undici ambiti

Non si procede per cartella: si procede per **ambito funzionale**, e ogni ambito si legge insieme alla
propria documentazione.

| # | Ambito | Documento di riscontro |
|---|---|---|
| 4a ✅ | Documento, sezioni, catalogo, blocchi | `refactor/08`, `11`, `14` |
| 4b ✅ | Release, snapshot, pubblicazione, retention | `refactor/09`, `10` · `audit-2026-08-25-versioni-release` |
| 4c ✅ | Import: SID, piste, settori, confinanti, GitHub | `refactor/01`-`05` |
| 4d | Import: tabelle, trasferimenti | `design/piano-import-*` |
| 4e 🔵 | Gerarchia, AoR, shape, aree | `refactor/06`, `15` · `spec/logica-aor` |
| 4f | Trasferimenti e accordi di coordinamento | `refactor/07` · feature accordi |
| 4g | Aeroporti: dati, posizioni, quote, regole piste | feature aeroporti · vSOP militari |
| 4h | Traduzioni: catena, stato, spesa, glossario | documenti bilingue |
| 4i | Lock di risorsa e di editing | design lock |
| 4j | Audit, incarichi, impatti, notifiche | `refactor/13` |
| 4k | Live, statistiche, Aurora bridge, servizi | `refactor/12` · `adr-0003` |

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
