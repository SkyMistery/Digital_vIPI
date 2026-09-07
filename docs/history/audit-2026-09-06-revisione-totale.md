# Revisione totale del codice — aperta il 6 settembre 2026

**Ramo:** `revisione-totale` (da `main` `2b33791a`) · **Stato:** 🔵 **in corso — Fasi 0-2 chiuse, 14 findings**

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
| **3** | Persistenza e concorrenza: corse sul `DbContext` censite a tappeto, sentinelle prima dell'`await`, `ExecuteDelete`, N+1 | ⏳ |
| **4** | Application — undici ambiti funzionali (vedi sotto) | ⏳ |
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
| 4a | Documento, sezioni, catalogo, blocchi | `refactor/08`, `11`, `14` |
| 4b | Release, snapshot, pubblicazione, retention | `refactor/09`, `10` · `audit-2026-08-25-versioni-release` |
| 4c | Import: SID, piste, settori, confinanti, GitHub | `refactor/01`-`05` |
| 4d | Import: tabelle, trasferimenti | `design/piano-import-*` |
| 4e | Gerarchia, AoR, shape, aree | `refactor/06`, `15` · `spec/logica-aor` |
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
| s-08 | **196 registrazioni `AddScoped`** e un `AddDbContext` (che è Scoped): in Blazor Server «scoped» vuol dire *per circuito*, cioè ore. Solo **29 componenti su ~140** che iniettano hanno uno scope proprio (`OwningComponentBase`) | Fase 3 |
| ~~s-09~~ | ~~la sonda EF8 ferma a 65 migrazioni~~ → **chiuso in Fase 2, misurato**: le 114 si applicano da vuoto sotto net8 | — |
| ~~s-10~~ | ~~`EnsureCreated` contro `Migrate()`~~ → **chiuso in Fase 2**: la scelta è esplicita per provider e un provider ignoto solleva | — |
| ~~s-06~~ | ~~63 raccolte scrivibili~~ → **chiuso in Fase 2**: sono navigazioni EF e binding di opzioni, che i setter li vogliono | — |
| s-11 | `AiracService.EffectiveUtcForCycle` valida con `int.TryParse`: **`"+261"` e `"-261"` passano** e producono una data sbagliata invece di sollevare — ed è proprio il sollevamento che `SidStampCycle` usa *come* validatore. Oggi lo schermano i chiamanti (regex `\d{4}`), non il validatore | Fase 4c |

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
