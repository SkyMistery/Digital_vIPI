# Documenti da rivedere — la casella degli impatti (carta v3, eseguita — 25 agosto 2026)

> **Stato: ✅ ESEGUITA il 25 agosto 2026** (slice 0→7), sul ramo `statistiche-atc`. Test verdi su entrambi i
> TFM (net8 **2401**, net10 **2163**, +35) e `dotnet build -c Release --no-incremental` pulito. Provata anche
> **sui dati veri** — §14. Quel che l'esecuzione ha cambiato rispetto al piano sta in **§13**.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Nasce dall'analisi del 25 agosto su *cosa succede se un
> dato importato viene eliminato dal DB* (§0) e ne assorbe tre dei sette fix.
>
> **v2 dopo revisione avversariale della v1** (25 agosto, sera). La v1 dava per buono un reverse-lookup che
> non lo è, non proteggeva l'avvio a freddo, e prometteva sulle sezioni `Live` una rilevazione che il disegno
> non faceva. Le decisioni D1–D4 sono rimaste; l'esecuzione no. Le correzioni sono marcate **🔁 v2**.

## La domanda

«**Quando qualcosa cambia a monte — un settore sparisce, un'area regolamentata cambia, un admin nasconde
una postazione — quali documenti devo rivedere o ripubblicare?**»

Oggi la risposta esiste per **un** caso su venti, e per gli altri diciannove il sistema tace.

## §0 — Da dove viene: la cancellazione silenziosa

L'analisi del 25 agosto ha trovato che la riga cancellata **torna** al giro successivo, ma il **legame** no:

1. il callsign sparisce dai cataloghi (sorgente che smette di esporlo, o `DELETE` a mano);
2. `SyncFromCatalogsAsync` disattiva il `Sector` proiettato **e recide** `DocumentId` / `IsPrimary` /
   `FeaturedRank` (`EfSectorProjectionService.cs:140`);
3. il documento resta in archivio ma **nessuno lo raggiunge più**: `AccVipiReleaseTarget` cerca un CTR
   radice `IsActive && DocumentId != null`, e non lo trova. La pagina pubblica va muta;
4. quando il callsign riappare, il settore si riproietta **con `DocumentId` null**: il legame non torna da sé.

Tutto in silenzio: nessun rilievo, nessuna riga di registro, nessun banner.

## §1 — Cosa c'è già, e in che stato è davvero

| Pezzo | Dove | Stato |
|---|---|---|
| Flag di revisione: `NeedsReviewUtc` + `ReviewReason` | `Document.cs:30-32` | slot singolo |
| Reverse-lookup settore → documenti | `EfDocumentReviewRepository.cs:22` | ⚠️ **da riscrivere**, vedi L5/L6 |
| Servizio che marca e apre un incarico | `DocumentReviewService.cs:47` | da estendere |
| Banner nell'editor col ✓ | `DocReviewBar.razor`, in 4 editor | da rendere multi-riga |
| Firma editoriale + diff fra release | `ReleaseService.cs:175-235` | riusabile così com'è |
| Snapshot «come sarebbe oggi» | `ReleaseService.BuildSnapshotJsonAsync` | riusabile così com'è |
| Rilievi su riferimenti che non risolvono | `ConsistencyReportService.cs:150-230` | riusabile così com'è |
| Cattura delle sezioni derivate Frozen | `IFrozenSectionProvider` + `FrozenSectionScan` (4 provider) | riusabile così com'è |

**L'unico chiamante** del meccanismo è `AccAdminService.cs:101`: subcenter ACC nascosto. Fine.

### I sei limiti

| | Limite | Conseguenza |
|---|---|---|
| L1 | **Slot singolo**: un `ReviewReason` per documento | il secondo evento sovrascrive il primo, che sparisce |
| L2 | **Non chiamabile da un import**: passa da `_tasks.ListAllAsync()` → `EnsureAdmin()` (`EditAuthorizationService.cs:162`) | un giro di background **lancia**. È il vincolo che decide l'architettura |
| L3 | **Reverse-lookup solo per settore** | niente per aree, piste, TA, SID, aeroporti |
| L4 | **Asimmetria in produzione**: nascondere una postazione d'aeroporto (`AirportSectorService.cs:53`) non segnala; un subcenter ACC sì | stessa azione, due comportamenti |
| L5 🔁 | **Il lookup sovra-segnala**: `IsPrimary \|\| Type == App \|\| Callsign == X` dentro l'ACC (`EfDocumentReviewRepository.cs:31-37`) = **ogni** documento primario e **ogni** APP dell'ACC | nascondere `LIRF_GND` segnala anche Bologna, Napoli, Pisa. Con sette rivelatori la casella è rumore alla prima notte |
| L6 🔁 | **E sotto-segnala**: il documento d'aeroporto è legato allo **scalo** (`Airport.DocumentId`, 25-ago) e il lookup passa solo da `Sector.DocumentId`; per giunta `EfAirportRepository.cs:436` **sgancia** gli APP standalone | uno scalo come LIBG — solo un APP non remotizzato — non produce **nessuna riga**: il caso del §0 non verrebbe visto |
| L7 🔁 | **Buco di autorizzazione**: `ClearReviewAsync` controlla il permesso solo `if (acc is not null)`, e `GetDocAccCodeAsync` passa da `Sector.DocumentId`. Per una **vLOA** (che sta sulle `Parties`, `EfEditingRepository.cs:248`) torna null → **nessun controllo** | e la vLOA è uno dei tipi che il lookup segnala |

## §2 — Il modello: non è uno stato, sono tre

| Stato | Significa | Chi lo trova | Chi lo chiude |
|---|---|---|---|
| **Da rivedere** | un cambio a monte può aver reso stantia una scelta editoriale | eventi (push) | l'uomo |
| **Da ripubblicare** | la copia pubblicata (sezioni `Frozen`) non dice più quel che direbbe oggi | calcolo (pull) | il calcolo |
| **Rotto** | un riferimento non risolve | diagnostica | il calcolo |

> **Regola: chi calcola, riconcilia; chi osserva un evento, apre e basta.**
> 🔁 **v2 — corollario mancante nella v1**: gli impatti **calcolati non sono chiudibili a mano**. Un ✓ su
> `ReleaseDrift` verrebbe riaperto dal giro notturno: ping-pong. Al più «silenzia fino alla prossima release».

### 🔁 v2 — Quel che la v1 diceva di sbagliato sulle sezioni `Live`

La v1 sosteneva che una sezione `Live` «cambia in pubblico senza approvazione». **Falso per il caso di
default**: le SID sono Live ma il contenuto è **cadenzato dall'AIRAC** — `SidRow.IsPublicAt`
(`AirportModels.cs:29`): una SID importata al ciclo N diventa pubblica **al ciclo N+1**. È un cambio
programmato, e il ciclo *è* l'approvazione.

Il caso vero resta, ma è l'altro: una sezione derivabile messa Live **a mano** dall'editor
(`DocumentSectionsEditor.razor:261-269`) non ha nessun gate. Misura sul `vipi.db` del 18 agosto: **5 sezioni
Live su ~200** — quattro `sids` (default) e **una `coordination`** manuale (§12). Una.

Decisione D6 (§3): **niente watermark**; si alza la **severità** degli impatti che già emettiamo, e si rende
il Live **visibile**. La rilevazione vera si fa quando i numeri la giustificano, con la soglia scritta.

## §3 — Le decisioni

| | Domanda | Decisione |
|---|---|---|
| D1 | Dove vivono le segnalazioni | **Tabella `DocumentImpact`**, molte righe aperte per documento |
| D2 | Il rivelatore per deriva entra subito | **Sì**, giro notturno dopo gli import |
| D3 | Il legame al documento quando il settore sparisce | **Si tiene** finché l'admin conferma; la rigenerazione si protegge filtrando su `IsActive` |
| D4 | Perimetro dei trigger nel primo taglio | **Settori + aree regolamentate** |
| D5 🔁 | A cosa si ancora un impatto | **Solo `DocumentId`** (FK + cascade). Vedi §3a |
| D6 🔁 | Sezioni `Live` | **Severità, non watermark** + badge di visibilità. Vedi §3b |

### §3a — D5: perché l'ancora è il documento e non il bersaglio di release

La chiave di release **non è stabile proprio sotto gli eventi che la casella deve tracciare**:

| Tipo | Chiave | Deriva da |
|---|---|---|
| `AccVipi` | `{acc}\|{callsign}` (`AccVipiReleaseTarget.cs:57`) | **callsign del settore primario** |
| `App` | callsign | **callsign del settore** |
| `Airport` | ICAO | l'aeroporto |
| `Vloa` | documentId | il documento |

`TryDescribe` compone la chiave da `doc.Sectors.FirstOrDefault(s => s.IsPrimary)`: un settore riparentato,
un primario che cambia, una rinomina in sorgente — cioè **gli eventi stessi** — spostano la chiave. Un
impatto ancorato lì diventa un impatto che parla di un documento e non sa più quale.

Scartate: **solo target** (instabile, senza FK, senza cascade); **entrambi come `EditorTask`** (l'incarico
porta il target perché può nascere *prima* del documento; l'impatto no, nasce da un documento che esiste →
due indirizzi sarebbero solo due modi di divergere). La UI non ne soffre: `ManagedDoc` porta **già entrambi**
(`ManagedDoc.cs:19-21`), quindi Versioni ricava il bersaglio da sé.

Prezzo dichiarato: «release orfana senza documento» **non è rappresentabile** come impatto → resta in
Diagnostica, dov'è già (fix 1 dell'analisi).

> ⚠️ **Difetto trovato scrivendo questa sezione, indipendente dalla casella.** Se la chiave si sposta, le
> release scritte sotto la vecchia non risultano più effettive → **il documento pubblicato va muto**, stessa
> famiglia del §0. Nel DB del 18 agosto le chiavi combaciano ancora (§12): **latente, non manifesto**. Con
> l'ancora su `DocumentId` il caso diventa un impatto normale — `ReleaseKeyMoved` — perché il documento c'è,
> è la chiave a essere scappata. Va aperto anche come voce a sé in `lavori-aperti.md`.

### §3b — D6: severità invece di rilevazione, con la soglia scritta

- Quando un evento (`SectorGone`, `AreaChanged`, …) tocca un documento che ha una sezione **Live alimentata
  da quella famiglia**, la riga dice «**è già cambiato in pubblico**» invece di «da rivedere». La mappa
  famiglia → sezione la dà `SectionCatalog`: nessun archivio nuovo, nessuna cattura nuova.
- Il Live diventa **visibile** dove si decide: badge nell'editor e in Versioni, «sezione viva: cambia da sé,
  non serve ripubblicare».
- ⚠️ **Limite dichiarato**: coglie solo i cambi che passano dai nostri eventi. Una frequenza cambiata a mano
  nel catalogo, con la sezione frequenze Live, non alza niente.
- **Soglia per passare alla rilevazione vera** (`LiveSectionChanged` + watermark degli hash, con
  `FrozenSectionScan.LiveDerived` e cattura che ignora il `RenderMode` sui 4 provider): quando le sezioni
  Live **non-SID** superano la decina, **oppure** al primo `aor` messo Live — che è il caso in cui una
  geometria cambiata entra in un documento pubblicato senza che nessuno l'abbia guardata.

## §4 — `DocumentImpact`: la casella

```
DocumentImpact
  Id
  DocumentId        FK → Document, cascade (il documento si porta via i suoi impatti)
  Kind              ImpactKind
  SourceKey         "LIRR_W_CTR" | "area:44120" | "" per gli impatti di documento   [INDICIZZATA]
  ReasonKey         chiave di localizzazione
  ReasonArgsJson    argomenti della frase
  IsPublicNow       true = tocca una sezione Live → «già cambiato in pubblico» (D6)
  RaisedUtc
  ClearedUtc        sentinella per «aperto», vedi sotto
  ClearedByUserId   0 = chiuso dal calcolo, non da una persona
```

🔁 **v2 — la frase non si salva, si compone.** La v1 aveva `Reason` *e* `ReasonKey`: due campi per la stessa
cosa, e una frase italiana salvata in DB si ripresenta in italiano a chi legge in inglese (il circuito Blazor
cambia lingua senza ricaricare). Il pattern della casa esiste già ed è l'altro: `ConsistencyFinding` porta
`CategoryKey`/`DetailKey`/`DetailArgs` e la frase la compone la UI. **Si copia quello.**

`ImpactKind` del primo taglio:

| Kind | Stato | Rivelatore | Si richiude | Chiudibile a mano |
|---|---|---|---|---|
| `SectorGone` | rivedere | proiezione | — | sì |
| `SectorHidden` | rivedere | proiezione | — | sì |
| `SectorReparented` | rivedere | proiezione | — | sì |
| `AreaGone` | rivedere | import aree | — | sì |
| `AreaChanged` | rivedere | import aree | — | sì |
| `ReleaseDrift` | ripubblicare | deriva (notturno) | **da sé** | **no** (D2/§2) |
| `ReleaseKeyMoved` 🔁 | rotto | deriva o consistenza | **da sé** | **no** |
| `BrokenTarget` | rotto | consistenza | **da sé** | **no** |

🔁 **v2 — la dedup la garantisce il DB, non la lettura.** La v1 lasciava aperta la scelta; va chiusa qui,
perché gli impatti si scrivono da `SyncFromCatalogsAsync`, che ha **13 chiamanti** e gira anche in
concorrenza (giro notturno + gesto admin), a volte sul `DbContext` del circuito: una dedup letta-poi-scritta
produce doppioni, o un «second operation». Su MariaDB l'indice unico **parziale** non esiste, quindi:
`ClearedUtc` **NOT NULL**, sentinella `0001-01-01` = aperto, e **indice unico** su
`(DocumentId, Kind, SourceKey, ClearedUtc)`.

### Perché non è un gemello di qualcosa che c'è già (pre-flight #1)

| Candidato | Perché non è lui |
|---|---|
| `AuditLog` | **immutabile e centrato sull'attore**. L'impatto ha uno **stato** ed è centrato sul **documento**; e un import **non ha attore** |
| `EditorTask` | **lavoro assegnato a una persona**. L'impatto è un **fatto rilevato**. Può *generare* un task; non è lui |
| `ConsistencyFinding` | **calcolato e transitorio**: non persiste, non si chiude |
| «Cosa è cambiato» (`EfChangesRepository`) | vetrina **pubblica** dei documenti pubblicati nel ciclo corrente |

⚠️ **Conseguenza**: `DocumentReviewService.cs:63` oggi apre **anche** un `EditorTask` per documento. Con la
casella sarebbero **due elenchi che dicono la stessa cosa** — quel che il pre-flight #1 vieta.
L'auto-creazione **si toglie**: il task nasce solo dal gesto «prendi in carico».

### `NeedsReviewUtc` / `ReviewReason`

**Si rimuovono** (drop di due colonne). La verità sta in un posto solo; il banner conta le righe aperte.
⚠️ La migrazione butta i flag pendenti al deploy: pochi e ricalcolabili, ma va nella nota di consegna.

### 🔁 v2 — Retention

Le righe chiuse non restano per sempre: potatura delle `Cleared` più vecchie di **due cicli AIRAC**, dentro
il giro notturno della deriva. Precedente in casa: `publication-retention`.

## §5 — I rivelatori, e dove si agganciano

Il rischio degli eventi è il **trigger dimenticato** — lezione già scritta in `AirportSectorImporter.cs`
(«un gate per categoria, non uno per chiamante»). Antidoto: i **choke point che esistono già**.

### A — Eventi (push)

| Choke point | Chiamanti | Cosa dà, diffando prima/dopo |
|---|---|---|
| `ISectorProjectionService.SyncFromCatalogsAsync` | **13** | settore comparso / sparito / riparentato |
| `ImportSpecialAreasAsync` + `PruneSpecialAreasNotInAsync` | import aree, auto e manuale | area sparita / cambiata |

Un aggancio per famiglia chiude anche **L4** senza toccare `AirportSectorService`.

> 🔁 **v2 — `AreaChanged` come la scriveva la v1 inonda.** `EfAccAdminRepository.cs:128-137` riassegna
> **tutti** i campi e fa `updated++` **senza confrontare niente**: «aggiornata» ≠ «cambiata». Ritornare gli
> id di `updated` darebbe un impatto per ogni area su ogni documento che la cita, **ogni notte**. Serve il
> confronto campo per campo prima di dichiarare il cambio — come già fa `ContentUnchanged` per le SID
> (`EfAirportRepository.cs:216`) — e conta solo ciò che un documento può mostrare: nome, tipo, quote, shape.

> 🔁 **v2 — Guardia dell'avvio a freddo, che nella v1 mancava.** `ProjectVipiSectors` gira a **ogni avvio**
> (`VipiModuleExtensions.cs:480`), indipendentemente dagli import. Catalogo vuoto o parziale — DB appena
> sostituito (è successo il 23 agosto), import fallito, ACC nascosti in blocco — e **ogni** settore proiettato
> risulta orfano: centinaia di righe su tutti i documenti. È lo stesso pericolo che l'import aree già
> disinnesca («se la fetch fallisce non si pota», `SpecialAreaImportUseCase.cs:41`). Regola: **nessun impatto
> di sparizione se il catalogo della famiglia è vuoto**, e **nessuno** se gli scomparsi superano una quota del
> totale (proposta: 25%) — in quel caso una riga sola, `Diag`, che dice «catalogo sospetto, impatti sospesi».

### B — Deriva (pull, calcolata)

`BuildSnapshotJsonAsync(target, key)` + `Signature()` esistono già. Applicati a **snapshot ipotetico vs
release effettiva** danno `ReleaseDrift` **senza un solo trigger da ricordare**.

- Gira **dopo** gli import, con `GatedImportLoop` (categoria `ImpactDrift`): eredita stato, retry ed errore.
- **Solo** documenti pubblicati e non nascosti (stesso gate di `PublicDocumentGate`): sono decine (§12).
- Riconcilia: apre le righe nuove, chiude quelle la cui deriva è sparita. Pota le chiuse vecchie.
- Rileva anche `ReleaseKeyMoved`: il bersaglio di oggi non ha release, ma il documento ne ha sotto un'altra chiave.
- ⚠️ La firma è **voce → conteggio elementi**: vede una sezione che cambia numero di righe, **non** un testo
  che cambia dentro una riga. Va detto nella UI, o il verde promette più di quel che misura.

> 🔁 **v2 — dove si vede lo stato del giro.** *Non* nella pagina Sorgenti: `ImportOverviewService.cs:129`
> esclude di proposito `SpecialAreaForeignOptOut` perché «in un elenco intitolato *stato degli import* è una
> riga che mente». Il giro di deriva **non è un import**: stessa obiezione, parola per parola. Va in
> **Diagnostica**, che ospita già i calcolati.

### C — Rotto (dalla consistenza)

Il report esiste e ha i rilievi giusti. Ci si aggancia il **fix 1**: «documento gestito la cui `ReleaseKey`
non risolve» → `BrokenTarget`. Il caso senza documento (release orfana) resta **rilievo di diagnostica**,
non impatto (D5).

## §6 — Il caso «settore sparito», passo per passo (D3)

1. La proiezione trova un callsign proiettato che non è più nel catalogo visibile.
2. Verifica la **guardia dell'avvio a freddo** (§5-A): catalogo vuoto o sparizione di massa → sospende.
3. **Disattiva** (`IsActive = false`) — come oggi.
4. **Non recide più** `DocumentId` / `IsPrimary` / `FeaturedRank`.
5. **Apre `SectorGone`** sui documenti del lookup **riscritto** (§8, slice 0), che ora include i settori
   disattivati: sono il soggetto della segnalazione.
6. In **Struttura**, sezione **«Orfani»**: i settori `IsProjected && !IsActive`, con i documenti toccati e
   due gesti — **riaggancia** (sposta `DocumentId`/`IsPrimary`) o **rimuovi definitivamente** (catalogo +
   proiezione, con **pre-check dei `Restrict`**, fix 7, e la frase onesta «se la sorgente lo rimanda, torna»).
7. Chiudere l'impatto è un atto esplicito e finisce nell'audit.

### Cosa proteggeva il taglio del legame, e come si protegge senza

`EfSectorProjectionService.cs:140` recide per evitare **FK dangling → artefatti doppio-documento in
rigenerazione e «primario» fantasma**. Motivo valido: va sostituito, non ignorato.

| Sito | Oggi | Con D3 |
|---|---|---|
| `EfAccDerivationRepository.cs:23` | filtra già `IsActive` | ✅ invariato |
| `EfAppDerivationRepository.cs:30` | non filtra | va bene: è ciò che tiene raggiungibile il documento APP da segnalare |
| `EfContentRepository.cs:178` | parte da `DocumentId` | ✅ invariato |
| `EfAirportRepository.cs:421/430` | riscrive `DocumentId`/`IsPrimary` su `sectors` | **verificare** che `sectors` sia ristretto agli attivi |
| `EfAirportRepository.cs:436` (`strayApps`) | mette `DocumentId = null` | 🔁 **v2**: è una **seconda porta** che scollega. D3 va applicata anche qui, o è un muro con tre porte e una chiusa |
| `EfStructureEditingRepository.cs:32` (`DeleteAccAsync` → cascade aeroporti) | scollega in cascata | 🔁 **v2**: terza porta. Almeno un avviso che dica quanti documenti restano orfani |
| `EfDocumentReviewRepository.cs:33` | esclude i disattivati | **includerli** |

⚠️ **Un test codifica la decisione vecchia** e va riscritto, non cancellato: `SectorProjectionTests.cs:158`
`Sync_Clears_Editorial_Links_When_Projected_Sector_Becomes_Orphan` →
`Sync_Keeps_Link_And_Raises_Impact_When_Projected_Sector_Becomes_Orphan`.

## §7 — Pre-flight (le quattro domande)

1. **Modello** — esiste già a metà (`NeedsReviewUtc`): lo **sostituisco**, non lo affianco. Le tre entità
   vicine sono distinte per fatto (§4). Fra sei mesi «dove si salva che un documento va rivisto» ha **una** risposta.
2. **Dispatch** — il lookup è per **famiglia di sorgente** (settore, area): due famiglie in **un** punto →
   regola del 2 → **niente registry**, un `DocumentImpactResolver` con due metodi. Il registry
   (`IImpactResolver`, sul modello di `IReleaseTarget`) si estrae **alla terza famiglia**.
3. **Ingressi + verifica** — due porte già esistenti: banner nell'editor (multi-riga) e **colonna/filtro in
   `/services/vsop/versions`**, dove si ripubblica; la sezione «Orfani» in Struttura, dove i settori vivono;
   lo stato del giro in Diagnostica. Nessuna pagina nuova. Nessun catch-22: le righe si vedono anche quando
   il documento non ha release. Verifica: skill `verifica-live`, nascondendo una postazione d'aeroporto e
   simulando la sparizione di un callsign.
4. **Propagazione** — sì, si **rimuove**: due colonne, un auto-task, una decisione della proiezione. §10.

## §8 — Le slice

| # | Slice | Cosa cambia in faccia all'utente |
|---|---|---|
| **0** 🔁 | **Il lookup, riscritto**: per settore *davvero* collegato (niente `IsPrimary`-a-tappeto), con `Airport.DocumentId` per gli scali e le vLOA per parti; e il **buco di authz L7** chiuso (permesso richiesto sempre, ACC risolto anche per vLOA e aeroporto) | niente: cambia solo *quali* documenti segnala il caso già esistente |
| 1 | **Meccanica**: entità + **due** migrazioni + `IDocumentImpactRepository` + `DocumentImpactService`; `FlagForHiddenSectorAsync` reindirizzato; via la dipendenza da `IEditorTaskService` (→ chiude **L2**); via l'auto-task | niente |
| 2 | Banner **multi-riga** (✓ solo sui non-calcolati); pill «da rivedere / da ripubblicare / già in pubblico» in Versioni; badge «sezione viva» (D6) | si vedono più motivi invece di uno |
| 3 | La proiezione **non recide più** e **apre gli impatti**, con la **guardia dell'avvio a freddo**; guardie `IsActive` e le altre due porte di §6; test riscritto | il documento non si sgancia più da solo |
| 4 | Sezione **«Orfani»** in Struttura: elenco, documenti toccati, riaggancia, rimuovi col pre-check dei `Restrict` | il gesto che oggi manca |
| 5 | **Aree**: confronto campo per campo, poi impatti sulle sezioni `regulated` che le citano (riusa `RegulatedSelectionJson`) | l'area che cambia lo dice, e **solo** quando cambia |
| 6 | **Deriva**: `ImpactDriftUseCase` + `GatedImportLoop` + retention, riga in **Diagnostica**; `ReleaseKeyMoved` | «da ripubblicare» compare da sé |
| 7 | **Rotto**: `BrokenTarget` — ⚠️ non dal report di consistenza ma dal giro della deriva, vedi §13 | il caso §0 ha finalmente una riga |

Un commit per slice (la 3 probabilmente due: le guardie sono meccaniche, l'emissione è logica — e il runbook
vuole meccanico e logico separati), `dotnet build Vipi.slnx -c Release --no-incremental` verde a ognuno.
⚠️ **Entrambi i TFM**: gli avvisi sono errori e `dotnet test` non usa quel flag.

### 🔁 v2 — Checklist di piattaforma per la slice 1

- **Due migrazioni**, non una: SQLite in `Persistence/Migrations` **e** MariaDB in
  `Vipi.Infrastructure.MySqlMigrations` (`MySqlSchema.cs:18`).
- `SourceKey` è **indicizzata** → va dimensionata in `MySqlStringLengths`, o `IndexedStringLengthTests`
  fallisce su net10 (dove il ramo MySQL non si compila nemmeno). Tetto 768 caratteri.
- `PostgresSchemaReconciler` per l'ambiente di prova (copre le **aggiunte**, che è il nostro caso).
- Enum `ImpactKind` come stringa: lunghezza dal default enum (32) di `MySqlStringLengths`.

**Fuori da questo giro, dichiarato**: i fix 2 (policy cancellata → «tutto importato» muto), 3 (audit sulle
cancellazioni strutturali) e 5 (ACC estero nuovo nasce con le aree accese). Il 3 conviene **dopo** la slice 1,
per riusarne le chiavi di frase.

## §9 — Rete

- **Application, senza IO**: dedup (due eventi uguali → una riga); riapertura dopo chiusura; «chi calcola
  riconcilia»; un evento su documento inesistente non esplode; un calcolato **non** si chiude a mano.
- **Infrastructure, DB in memoria**: la proiezione che perde un callsign **tiene** il legame e **apre** la
  riga (test riscritto); 🔁 **catalogo vuoto → nessun impatto**; 🔁 **sparizione di massa → sospensione**;
  il lookup pesca ACC + APP + **aeroporto via `Airport.DocumentId`** + vLOA, e **non** pesca i documenti
  estranei dello stesso ACC (è L5, e senza un test torna); la rimozione definitiva rifiuta se un accordo
  referenzia il settore.
- 🔁 **Authz**: `ClearReview` su una **vLOA** senza permesso → rifiutata (oggi passa, L7).
- **Il caso che ha originato tutto**: callsign sparito → riappare → il documento è ancora agganciato.
- **Regressione L2**: il servizio chiamato **senza utente** (background) non lancia.
- 🔁 **Aree**: import che non cambia niente → **zero** impatti.
- **UI**: banner con tre righe, ne chiude una e lascia le altre due; il ✓ non compare sui calcolati.

Baseline da non far scendere: **2366** test su net8, **2128** su net10. ✅ Dopo il giro: **2401** e **2163**.

## §10 — Propagazione (pre-flight #4)

- `DocumentReviewService` → **`DocumentImpactService`**; `IDocumentReviewRepository` →
  `IDocumentImpactRepository`; `FlagForHiddenSectorAsync` → `RaiseAsync(kind, sourceKey, …)`.
- `DocReviewBar.razor`: commento di testa riscritto (cita «un settore nascosto» come unico caso).
- `Document.cs:30-32`: colonne rimosse → nessun `<see cref>` orfano.
- `EfSectorProjectionService.cs:140`: il commento spiega la decisione **vecchia**; riscriverlo con la nuova e
  col perché il motivo originale è coperto altrove (§6).
- `SectorProjectionTests.cs:158`: nome e corpo.
- `docs/spec/modello-dati.md`: entità nuova, due colonne in meno.
- `docs/lavori-aperti.md`: voce del giro **+ voce a sé per `ReleaseKeyMoved`** (difetto già esistente).
- **Memorie**: aggiornare `tre-documenti-uniformita` e `sector-single-source-projection` (la proiezione non
  recide più); memoria nuova per la casella.

## §11 — Costi e rischi, dichiarati

| Rischio | Mitigazione |
|---|---|
| **Rumore** (il modo classico in cui questi meccanismi muoiono) | slice 0 prima di tutto, perimetro stretto, dedup dal DB, confronto vero sulle aree, e i calcolati che si richiudono da sé |
| Valanga all'avvio a freddo | guardia catalogo vuoto + soglia di massa (§5-A) |
| D3 riapre gli artefatti che il taglio preveniva | i **sette** siti di §6 verificati nella stessa slice |
| Il costo del giro cresce coi documenti | solo pubblicati e non nascosti; `GatedImportLoop` con stato e retry |
| La firma conta elementi, non testo | detto nella UI (§5-B) |
| D6 non rileva i cambi Live fuori dai nostri eventi | limite scritto, con la soglia per passare al watermark (§3b) |
| La migrazione butta i flag pendenti | pochi e ricalcolabili; nella nota di consegna |

## §12 — Le misure (25 agosto, `vipi.db.bak-pre-sezioni-20260818`)

La regola del runbook è misurare invece di supporre. Fatto:

| Misura | Valore | A cosa è servita |
|---|---|---|
| Documenti | 15 | dimensiona il giro notturno: decine, non migliaia |
| Release | 34 | idem |
| Sezioni `Live` | **5**: 4 `sids` (default) + **1** `coordination` manuale | ha **ribaltato** la v1: niente watermark (D6) |
| Sezioni `Frozen` | ~200 | la deriva copre il caso normale |
| Chiavi `AccVipi` in release vs vive | `LIBB\|LIBB_ES_CTR`, `LIMM\|LIMM_WS2_CTR` — **combaciano** | `ReleaseKeyMoved` è **latente**, non manifesto: va aperto, non trattato come incendio |

## §13 — Cosa resta fuori, dichiarato

- **Notifiche**: la casella è passiva (banner + colonna + diagnostica). Nessun badge in topbar, nessuna mail,
  nessun legame con la scadenza AIRAC (`EditorTask.DueAiracCycle` esiste ed è la strada naturale, dopo).
- **Watermark delle sezioni Live** (§3b), con la sua soglia.
- **Famiglie oltre settori e aree**: TA, piste, SID, shape, flag militari (D4).
- **Release orfane senza documento**: restano rilievo di diagnostica (D5).


## §13 — Che cosa è cambiato eseguendo

Sette scostamenti dal piano. Nessuno tocca le sei decisioni; tutti nascono da qualcosa che il codice ha detto
e la carta non sapeva.

### 13.1 ⚠️ La sentinella `0001-01-01` non esiste su MariaDB

Il piano diceva «`ClearedUtc` NOT NULL, sentinella `0001-01-01`». Il `DATETIME` di MariaDB parte dal
**1000-01-01**: in `sql_mode` stretto quel valore viene **rifiutato** (errore 1292), in modalità permissiva
diventa una data zero. Su SQLite sarebbe passato — cioè suite verde e produzione rotta alla prima
segnalazione. La sentinella è l'**epoca Unix**: dentro l'intervallo di tutti e tre i provider, e nel 1970 non
c'era niente da chiudere.

### 13.2 La segnalazione «settore nascosto» si è spostata dentro la proiezione

Il piano teneva l'aggancio esistente in `AccAdminService` e ne aggiungeva uno nella proiezione. Sarebbero
stati due posti per lo stesso fatto — e il primo copriva solo i subcenter ACC, lasciando fuori le postazioni
d'aeroporto (era il limite **L4**). La proiezione distingue da sé le due cause: se il callsign è **ancora in
catalogo** è `SectorHidden`, se non c'è più è `SectorGone`. `AccAdminService` non apre più niente.

### 13.3 Un callsign che torna richiude la sua segnalazione

Non era nel piano, ed è emerso subito: senza, una posizione nascosta e poi rimostrata lasciava una riga
aperta per sempre. Non la chiude una persona — non l'ha risolta nessuno, si è risolta — quindi la chiusura
porta l'utente **0**, come per i rivelatori calcolati.

### 13.4 Il reverse-lookup ha avuto bisogno di due ripieghi

- **L'ACC di un settore sparito** non lo dice più il catalogo (la riga non c'è): lo dice il `Sector.AccId`.
  Senza, la segnalazione partiva con l'ACC vuoto e la vIPI ACC — il documento che più di tutti racconta quel
  settore — non veniva avvisata.
- **La posizione (CTR/APP/TWR…)** idem: si ripiega sul `Sector.Type` proiettato. Senza, «pesa sulla
  sezionazione dell'ACC» era sempre falso proprio nel caso che conta.

Sono due righe di codice, ed erano la differenza fra un meccanismo che funziona e uno che tace: entrambe
trovate da un test che falliva, non a mente.

### 13.5 `BrokenTarget` sta nel giro della deriva, non nel report di consistenza

Il piano lo metteva fra i rilievi di consistenza. Ma quel report è una **fotografia di sola lettura** che non
scrive niente, e la domanda «il bersaglio risolve ancora a questo documento?» ha bisogno del registry dei
bersagli, che il giro della deriva ha già in mano. Metterlo lì evita di duplicare la risoluzione in due punti
— e gli fa ereditare gratis la **riconciliazione**, che è ciò che serve a un rilievo calcolato.

### 13.6 `ImportSpecialAreasAsync` cambia forma

Ritornava `(Created, Updated)`. Ora ritorna anche **che cosa è cambiato davvero**, confrontando i soli campi
che un documento mostra (nome, tipo, quote, raggio, testi di attivazione — non la shape). Senza quel
confronto, «aggiornata» sarebbe stato ogni giro per ogni area: una riga per ogni documento, ogni notte.

### 13.7 Il conteggio per tipo in Diagnostica

La carta non diceva come la Diagnostica leggesse la casella. Serve un conteggio per tipo e l'ultimo esito del
giro: senza la seconda riga, «zero segnalazioni» e «il controllo non gira da tre giorni» si leggono uguale.

## §14 — La prova sui dati veri

Fatta il 25 agosto su una **copia del `vipi.db` di sviluppo** (19 documenti, 321 settori, 153+193 righe di
catalogo, 230 aree, 37 release) — non su un database di comodo. ⚠️ Il file va copiato **con i suoi `-wal` e
`-shm`**, o SQLite lo dichiara *malformed*: il database è in WAL, e metà delle scritture recenti stanno lì.

| Prova | Esito |
|---|---|
| `LIBA_APP` cancellato dal catalogo, poi proiezione | disattivato, **legame al documento 3 conservato**, 2 segnalazioni `SectorGone` (il suo documento + la vIPI ACC) |
| effetto collaterale vero | `LIBA_TWR` ha cambiato padre — l'APP era il suo — e si è preso un `SectorReparented`: la catena di copertura si racconta da entrambi i lati |
| elenco orfani | **9** già in archivio (7 nascosti, 2 spariti), coi documenti toccati e i bloccanti |
| il callsign torna | riattivato, le 2 righe **chiuse dal calcolo** (utente 0) |
| area `1002` «LI D20 Gela» cambia nome | **5** documenti la citano, 5 righe aperte, tutte `IsPublicNow` |
| rimozione di un orfano dall'app | riga proiettata **e** riga di catalogo tolte; un orfano con bloccanti viene **rifiutato con la frase**, non con un errore di vincolo |
| aeroporto pubblicato scollegato | il giro apre `BrokenTarget` sul documento 13 — il caso del §0, colto |
| giro della deriva | 11 documenti pubblicati esaminati, nessuna deriva falsa |

⚠️ **Una cosa che la prova ha mostrato e nessun test avrebbe detto**: in quell'archivio ci sono già **4
documenti** che nessun settore attivo raggiunge. Non li ha creati questo lavoro — c'erano — ma da oggi hanno
un posto dove comparire.


## §15 — La verifica live (Edge + puppeteer, skill `verifica-live`)

App avviata su una copia del `vipi.db` di sviluppo e guidata in un browser vero. ⚠️ Le regressioni Blazor
sono silenziose coi test verdi: qui si guarda quel che compare a schermo.

| Superficie | Che cosa ha mostrato |
|---|---|
| `/services/vsop/admin/sector-structure` | sezione **«Orphan sectors 8»**, tabella con Sector/Why/Documents/Actions e il tasto **Remove** per riga. `LIBB_EU_CTR` porta in colonna i **tre** documenti che tocca — vIPI Brindisi e due vLOA: è il reverse-lookup nuovo, a schermo |
| editor vIPI ACC di Brindisi | banner **«⚠️ Needs review (1) · republish · The published copy is behind the draft: Brindisi CS0 / Minime di vettoramento, … (+1) · closes by itself»**, e **nessun ✓** — è una riga calcolata |
| `/services/vsop/versions` | pill **«republish (1)»** sulle due vLOA di Brindisi, accanto alle pill di release |
| `/services/vsop/admin/diagnostics` | riquadro **Documents needing review**: «Published copy behind — 4», più «Last drift check: 25 Aug 2026 · 21:05Z» |
| guardie di pagina | nessun letterale Razor non valutato, nessun errore in pagina, su tutte |

**Il giro notturno ha girato davvero**, 100 secondi dopo l'avvio, e ha trovato **quattro derive vere** su
documenti veri — non rumore: coordinamenti sulle due vLOA di Brindisi, minime/configurazioni sulla vIPI ACC,
AoR/separazioni sull'APP di Pescara. Sono documenti pubblicati il cui contenuto è cambiato dopo l'ultima
release: esattamente la cosa che questo progetto continua a scoprire a mano («⚠️ resta da RIPUBBLICARE i
documenti» ricorre nelle note da mesi).

⚠️ **Quel che la verifica live NON ha coperto, dichiarato**: il gesto «nascondi un subcenter **domestico**
descritto da un documento» non è stato guidato fino in fondo dal browser — i tasti della pagina ACC sono
dietro il lock di modifica e una conferma in linea, e il primo bersaglio libero era un ACC **estero senza
documenti** (nasconderlo ha prodotto, correttamente, **zero** segnalazioni: la prova del non-rumore). Quel
percorso resta coperto dal test d'integrazione `Un_Callsign_Nascosto_Apre_SectorHidden_Non_SectorGone` e
dalla prova sui dati veri del §14, dove la distinzione sparito/nascosto è verificata sull'archivio reale.


## §16 — La rinomina: `LIRN_US0_APP` → `LIRN_US1_APP` (25 agosto, sera)

Domanda del committente dopo la consegna: *«se cambia il nome, che succede?»*. Misurato riproducendo
l'import vero su `LIBD_CS0_APP` — e il caso è **peggiore** di quello chiuso in §0.

### Che cosa succedeva

| | |
|---|---|
| L'import fa upsert del nome nuovo | crea `CS1` |
| La riga vecchia | **resta**: i cataloghi non potano mai |
| La proiezione | **due settori attivi**, stessa shape, stessi limiti, stessa frequenza |
| La casella (§0→§15) | **nessuna segnalazione**: niente è sparito, e il nuovo è «nuovo», non «riparentato» |
| Documento, primario, `FeaturedRank` | restano sul **fantasma** |
| I figli con `ParentCallsign` sul vecchio | continuano a puntargli: il callsign *esiste*, quindi nemmeno «gerarchia dangling» se ne accorge |
| Chi controlla davvero | si connette col nome nuovo: vista live e statistiche vanno sul settore **senza documentazione** |
| Per un APP non remotizzato | la chiave di release **è** il callsign, e resta la vecchia: il documento funziona *descrivendo una posizione che non esiste* |

⚠️ **E non era teoria**: `LIED_G_APP` — l'APP primario di Decimomannu — aveva il timbro d'import del **5
agosto** contro il **24** delle altre tre posizioni dello stesso scalo. Diciannove giorni da fantasma, attivo,
disegnato nelle mappe e con un volume che rivendica traffico. Nessuno lo sapeva.

### Perché il meccanismo del giorno prima non lo vede

Guarda la proiezione; la proiezione guarda il catalogo; e per il catalogo non è successo niente. Il buco è a
monte, e nessuna delle otto famiglie di impatto lo copre.

### La decisione: il timbro, non la potatura

Il segnale c'era già e non lo usava nessuno: `ImportedAtUtc` sta su ogni riga di catalogo, viene riscritto a
**ogni** upsert, e il giro giornaliero passa su **tutti** gli aeroporti e tutti gli ACC. Quindi «la sorgente
non lo elenca più» è calcolabile **senza cancellare niente**.

Scartata **A — potare i cataloghi**: renderebbe la sparizione visibile col meccanismo di §0, ma butta le
impostazioni dell'admin (limiti, nascosto, shape sintetica, `IsAccApp`) e una fetch parziale cancella a
valanga. È la porta che avevamo appena chiuso dall'altro lato.

Fatto **B + C**:

- **B — stantìo, non cancellato.** `ImpactKind.SectorStale` sui documenti che raccontano quel settore, e una
  riga in «Orfani» con il motivo **«non più elencato»** e la data dell'ultimo timbro. ⚠️ È l'unico caso in cui
  la riga elencata è **attiva**: per tutto il resto del sistema quel settore esiste ancora.
- **C — il suggerimento di rinomina.** Se nello stesso perimetro compare **un solo** callsign con la stessa
  `Position` e il timbro fresco, la riga propone *«forse rinominato in …»* e lo segna nel picker di
  riaggancio. ⚠️ **Proposta, mai automatismo**: la cifra in `US0`/`US1` di solito significa **sdoppiamento**,
  non rinomina, e i due casi sono indistinguibili guardando i dati — identici per shape, limiti e frequenza.
  Con **due** candidati la riga tace, perché indovinare vorrebbe dire spostare un documento sul settore
  sbagliato. E il suggerimento arriva **un giro dopo** la rinomina, non lo stesso giorno.

### Le tre guardie, e la terza l'ha imposta la prova

1. **Righe aggiunte a mano**: la sorgente non le ha mai mandate, quindi il loro timbro è vecchio per
   costruzione. Serve un flag esplicito (`AccSector.IsManual`, `AirportSector.IsManual`) — messo da chi le
   crea, più un backfill una tantum che le riconosce dal prefisso del callsign diverso dal codice dell'ACC
   (misurato: cinque righe, cinque manuali, nessun falso).
2. **Stato mancante**: senza l'ultimo giro riuscito di **entrambe** le famiglie non si dice niente, e il metro
   è il giro più **vecchio** dei due.
3. ⚠️ **Guardia di massa** — non era nel piano, l'ha trovata la prova sui dati veri: un giro che **riesce** ma
   torna vuoto per un ente lascia tutte le sue righe senza timbro nuovo, e il giorno dopo sarebbero trenta
   segnalazioni in blocco. Nella prima esecuzione ne sono comparse **una trentina** di settori esteri tutti
   insieme. Sopra un quarto delle righe di catalogo il controllo tace: quel numero non parla delle righe,
   parla di un guasto a monte.

### La prova sui dati veri

Simulando un giro d'import completo sull'archivio di sviluppo (che ri-timbra tutto tranne il rinominato):
**una sola riga** segnalata — `LIBD_CS0_APP`, ultimo timbro 24 agosto, **«forse rinominato in
LIBD_CS1_APP»**. Nessun falso positivo.

### Quel che resta scoperto, dichiarato

- Il gesto «sposta i legami» sposta **documento, primario e rilievo**; le citazioni **per Id** (accordi, parti
  di vLOA, blocchi con scope/da/a) restano sul vecchio e compaiono come **bloccanti** della rimozione. È una
  scelta: spostarle vorrebbe dire riscrivere accordi in silenzio.
- I `ParentCallsign` dei figli non vengono ripuntati: se il padre è stato rinominato, la catena va sistemata a
  mano dalla Struttura.
- Le **sessioni storiche** restano sul callsign vecchio (319 su `LIBD_CS0_APP`, nell'archivio di prova). È
  giusto così — quelle ore sono state fatte con quel nome — ma le statistiche per callsign vanno lette
  sapendolo.
- La domanda a monte resta aperta: **il callsign non è un'identità stabile**, e la sorgente non ne espone
  un'altra (`SourceAtcPosition` non porta nessun id, e perfino il dettaglio si interroga per
  `composePosition`). La colonna `Sector.FacilityId` esiste dal primo giorno e **non la scrive nessuno**.
