# 07 — Trasferimenti (punto 8) 🟢✅

> ✅ **MODELLO SOSTITUITO — 16/17 agosto 2026** (ramo `feature/accordi-coordinamento`, non ancora in `main`).
> `TransferFlow` + `TransferPoint` hanno lasciato il posto a un **accordo** fra due parti, a due direzioni:
> vedi §10 in fondo e la carta
> [`../feature/2026-08-16-accordi-di-coordinamento.md`](../feature/2026-08-16-accordi-di-coordinamento.md).
>
> ⚠️ **Tutto ciò che segue (§1–§9) è STORIA**: descrive il modello fino a quella data. Le *decisioni* restano
> valide e sono state portate sull'entità nuova — l'outline delle varianti, la condizione a tre dimensioni, la
> faccetta trasferimento, la direzione owner→next — ma i **nomi** e il posto dove i campi stanno sono cambiati.
> Le due tabelle esistono ancora, in **sola lettura**, finché la migrazione che le droppa non gira in una
> release successiva al travaso in produzione.

> **✅ REFACTOR FATTO — 2026-07-09** (branch `refactor/07-transfers`, 222 test).
> `ITransferService` + 6 DTO estratti in file singoli (§4.1); porta di lettura
> `INeighbourReader` (ISP) — `AdminTrasferimentiPage` non dipende più dal service import
> completo (§4.2). Validazione già conforme (`Aor.ValidationException`); `TransferOnlineResolver`
> già testato. Vedi `../history/rounds.md` «Refactor 07».

> Flussi di trasferimento (CoP + livelli + settore successivo). Risoluzione live che
> cammina l'albero: un settore chiuso è assorbito dal primo antenato online.
> Dipende da: doc 06 (gerarchia/topologia).

## 1. Stato attuale

| File:riga | Classe/membro | Ruolo |
|---|---|---|
| `Vipi.Domain/Entities/Support.cs:87` | `TransferFlow` | Flusso: settore owner, `TransferFlowKind`, aeroporto, punti. |
| `Vipi.Domain/Entities/Support.cs:112` | `TransferPoint` | CoP + `LevelValue/Unit/Constraint/Special` + `NextSectorId`. |
| `Vipi.Domain/Enums.cs:70,73,77` | `TransferFlowKind`, `LevelUnit`, `LevelConstraint` | Enum flusso/livello. |
| `Vipi.Application/Content/TransferEditingService.cs:8` | `ITransferService` | Entry: list/resolve/add/update/delete flussi & punti, `MovePointAsync`. |
| `Vipi.Application/Content/TransferEditingService.cs:31` | `TransferService` | Impl; `ResolveForAccAsync` (`:47`) costruisce catene via `ITopologyProvider`; write ACC-gated; validazione soft (`:124-134`). |
| `Vipi.Application/Content/TransferOnlineResolver.cs` | `TransferOnlineResolver` | `FirstOnline`/`Resolve` — primo settore online nella catena. |
| `Vipi.Application/Content/TransferModels.cs:11,25,40,49,60,70` | `TransferFlowRow`, `TransferPointRow`, `TransferFlowInput`, `TransferPointInput`, `ResolvedTransferPoint`, `ResolvedTransferFlow` | DTO read/input/live. |
| `Vipi.Application/Abstractions/ITransferRepository.cs` | `ITransferRepository` | Porta persistenza. |
| `Vipi.Infrastructure/Persistence/EfTransferRepository.cs` | `EfTransferRepository` | CRUD EF + ordinamento flussi/punti. |
| `Vipi.Infrastructure/Persistence/Seed/RomaTransferSeed.cs` | `RomaTransferSeed` | Trasferimenti demo. |
| `Vipi.Ui/Pages/AdminTrasferimentiPage.razor` | route `/vsop/admin/trasferimenti` | Editor; inietta `ITransferService`, `IStationResolver`, `INeighbourImportService`. |
| `Vipi.Ui/Components/App/TransfersLive.razor` | `TransfersLive` | Vista live risolta. |

I trasferimenti compaiono come sezione **Coordinamenti** nei documenti ACC/APP/vLOA —
derivati live, non salvati nel payload editoriale.

## 2. Problemi

1. **`TransferEditingService.cs` multi-classe**: interfaccia + impl.
2. **`TransferModels.cs` multi-classe**: 6 DTO in un file.
3. **Pagina editor accoppiata con l'import confinanti** (`INeighbourImportService` in
   `AdminTrasferimentiPage`) → mescola editing trasferimenti e trigger import (doc 05).
4. **Risoluzione live** (`ResolveForAccAsync` + `TransferOnlineResolver`) dipende dalla
   topologia (doc 06): l'accoppiamento è corretto ma va documentato come contratto stabile.
5. **Validazione soft** inline (`:124-134`) — verificare che segua la convenzione
   `ValidationException` di Application (memoria: mai DataAnnotations).

## 3. Architettura target

> ✅ APPROVATA — Fase 0, 2026-07-09. Verifica sez.2 vs codice:
> - **P3 mal descritto**: la pagina NON triggera l'import — fa `Neighbours.ListAsync()`
>   (lettura dei confinanti per i mittenti estero→home). È un problema di **ISP/decoupling
>   d'area**, non di trigger.
> - **P5 già conforme**: la validazione usa `Vipi.Application.Aor.ValidationException` — nessun cambio.
> - `TransferOnlineResolver` (risoluzione live) è **già testato** → nulla da irrobustire (#8).

- **Estrarre `ITransferService`** (da `TransferEditingService.cs`) e i **6 DTO** (da
  `TransferModels.cs`) in file singoli.
- **Porta di lettura dedicata `INeighbourReader`** (`{ ListAsync }`): `NeighbourImportService`
  la implementa (oltre a `INeighbourImportService`); `AdminTrasferimentiPage` inietta
  `INeighbourReader` invece del service import completo. `ConfinantiAdminPage` (usa
  import/generate/pair-detail) resta su `INeighbourImportService`. Decoupling d'area 05↔07.
- `ITopologyProvider` confermato come unico contratto verso la gerarchia (doc 06) — invariato.

## 4. Passi di migrazione

> ✅ APPROVATA — Fase 0, 2026-07-09. Meccanico → logica.

**Meccanico (commit separato):**
1. Estrarre `ITransferService` da `TransferEditingService.cs`; i 6 DTO
   (`TransferFlowRow`/`TransferPointRow`/`TransferFlowInput`/`TransferPointInput`/
   `ResolvedTransferPoint`/`ResolvedTransferFlow`) da `TransferModels.cs`. File singoli.

**Con logica:**
2. Introdurre `INeighbourReader { ListAsync }`; far estendere `INeighbourImportService : INeighbourReader`;
   registrare la porta in DI; `AdminTrasferimentiPage` inietta `INeighbourReader`.

*(§4.3 validazione: già conforme, nessuna azione.)*

## 5. Impatto

- **Dipende da** doc 06. **A valle**: doc 08 (sezione Coordinamenti derivata dai flussi).
- **Verifica** (Fase 3): risoluzione live invariata (test `TransferOnlineResolver` verdi);
  CRUD flussi/punti ACC-gated; la pagina trasferimenti mostra ancora i mittenti esteri
  confinanti; conteggio test = baseline (222).

## 6. Aggiornamenti successivi (2026-07-13)
- **Sorvoli senza aeroporto** derivati nei Coordinamenti (cuore condiviso `CoordinationDerivation`;
  frase kind-aware). I consumer ACC/APP/**vLOA** devono passare `flow.Kind` a `CoordinationSentences.Compose`
  (il fix vLOA chiudeva una falla di propagazione: prima ometteva il kind → sorvolo trattato come arrivo →
  frase nulla). Un punto con ricevente non risolto (null/non in `types`) viene scartato: policy intenzionale,
  segnalata in editor (badge «nessun ricevente»). Vedi `history/rounds.md` «Fix sorvoli end-to-end».
- **Parità livello** (`TransferPoint.Parity`, enum `LevelParity`): resa nel `LevelText` via
  `LevelFormatting.Format(...,parity)` → propaga a tutte le viste + frase senza rami duplicati.

## 7. Aggiornamenti successivi (2026-07-22) — condizione operativa (pista/area)
- **Livelli variabili per pista in uso / area attiva** (modello **editoriale**: varianti etichettate, non
  calcolate live). Campi additivi su `TransferPoint`: `ConditionKind {None,Runway,Area,Custom}` + `ConditionLabel`
  (verità display, denormalizzata) + `ConditionRefId` (soft-ref a `AirportRunwayRule`/`SpecialArea`, no FK).
  Migrazione `AddTransferPointCondition`. Dettaglio schema: `spec/modello-dati.md` §9.20.
- **Clausola frase** dallo slot `Condition` del template (`CoordinationSentenceComposer`, IT/EN), appesa a fine
  frase; `AppCoordRow.ConditionLabel` reso come colonna condizionale (ACC/APP/vLOA) e pill nella Ridotta.
- **Editor** (`AdminTrasferimentiPage`): selettore condizione nel form riga (kind + label con datalist delle
  config pista dell'aeroporto del flusso, via `IAirportEditingService.LoadForViewAsync`); batch multi-CoP e
  clona propagano la condizione. Validazione soft: kind ≠ None richiede una label.
- Additivo (nessun rename → nessuna propagazione distruttiva). Test: composer (clausola IT/EN), derivation
  (label su riga+frase), EF round-trip (`TransferRepositoryTests`). Suite verde (19 dom + 200 app + 174 infra).

### 7.1 Estensione condizione (2026-07-22, stessa sessione) — multi-pista + area in AND
- **Stessa condizione su più piste** in una sola riga: `ConditionLabel` con `Runway` può elencarle («16R / 16L»).
  Editor: **multi-select** delle **piste reali** (`AirportRunways`, non le config `AirportRunwayRule`) dell'aeroporto
  del flusso. Fix collegato: la condizione «Pista» ora legge `d.Runways` (prima leggeva `d.Rules` → vuoto senza
  regole editoriali; es. LIBD aveva piste ma 0 regole).
- **Pista + area in AND**: nuovo campo `ConditionAreaLabel` (overlay valido solo con `Kind=Runway`) → frase
  «… con pista X in uso **e** Y attiva» (slot template `RunwayAndArea`, IT/EN). Migrazione
  `AddTransferPointConditionArea`. Etichetta combinata per il display via `TransferConditionText.Display`
  (`TransferPointRow.ConditionDisplay`), usata da pill admin + `AppCoordRow.ConditionLabel` (ACC/APP/vLOA/Ridotta).
- vLOA ora porta anch'essa la condizione (prima `VloaDerivationService` la ometteva). Test: composer multi-pista +
  combinato IT/EN + degrado area-sola. Suite verde (204 app + 174 infra).

### 7.2 Condizioni indipendenti + area ricercabile (2026-07-22, stessa sessione)
Su richiesta: pista / area / personalizzata devono essere **indipendenti** (una riga può averle tutte), non un tipo
singolo. **Rimosso `ConditionKind` + enum `TransferConditionKind`**; la condizione è ora **tre colonne indipendenti**:
- `ConditionLabel` (pista/e) · `ConditionAreaLabel` (area) · **`ConditionCustomLabel`** (personalizzata, nuova). Migrazione
  `SplitTransferConditionColumns` (drop `ConditionKind`, add `ConditionCustomLabel`, backfill Area/Custom→colonne).
- **Editor**: la colonna «Condizione» diventa **tre colonne** (Pista multi-select · Area · Personalizzata). L'**area** è
  ora un **picker con ricerca a digitazione** (typeahead sul nome, stile picker settori), non più una `<select>`.
- **Frase**: il composer compone la clausola di ogni dimensione presente e le unisce con `Condition.Join` («e»/«and»);
  pista+area usano la forma dedicata `RunwayAndArea`. `ConditionDisplay` = «pista · area · personalizzata».
- Test: composer (tre insieme, area+custom, join), EF round-trip (colonne indipendenti, ref solo con pista). Suite verde
  (19 dom + 205 app + 174 infra).

### 7.3 Stato verticale disaccoppiato dal vincolo (2026-07-24)
Su richiesta: la parola `{stato}` («in discesa/salita/stabile») nella frase **derivava dal `LevelConstraint`**
(≤→discesa, ≥→salita, esatto→stabile). Sbagliato: `≥` è un **bound di livello** («a 130 o superiore»), non implica
una salita. Ora lo stato verticale è una **dimensione indipendente**.
- **Nuovo enum** `TransferVerticalState { Unspecified, Level, Descending, Climbing }` + campo `TransferPoint.VerticalState`.
  Il composer sorgente `{stato}` da questo campo (non più dal constraint); `Unspecified` → **nessuna parola** (frase col
  solo bound: «… con destinazione LIBD a livello 130 o livello inferiore su PISIP»). Il `{fl}` resta guidato dal constraint.
- **Rinomina** `CoordinationSentenceState.{AtOrBelow,AtOrAbove,Exact,Special}` → `{Descending,Climbing,Level}` (nomi che
  ora dicono lo stato, non il vincolo); propagata a `CoordinationSentenceOptions.StateWords` + chiavi JSON
  `content/coordination-sentence.json` (`descending/climbing/level`).
- **Migrazione** `AddTransferPointVerticalState` (colonna TEXT, default `Unspecified`) con **backfill da constraint**
  (≤→Descending, ≥→Climbing, esatto→Level) → le frasi esistenti restano identiche. Seed demo idem (`VStateFrom`).
- **Editor** (`AdminTrasferimentiPage`): nuova `<select>` «Stato verticale» nei form add/edit riga (risorse `Xfer_VState*`).
- **Convenzione display** (`LevelFormatting.Format`, usato in tabella trasferimenti + tutte le viste coordinamenti): le
  **frecce `↑`/`↓`** indicano ora lo **stato verticale** (salita/discesa), il **vincolo di livello** usa i segni
  **`+`** (≥) / **`-`** (≤). Es. `FL290+ ↑ (dispari)`. Prima le frecce indicavano il vincolo. Le opzioni del
  selettore vincolo mostrano `≤ (-)` / `≥ (+)`; le opzioni stato verticale mostrano `↓ In discesa` / `↑ In salita`.
- Test: composer (stato scelto a mano IT/EN, regressione «constraint senza stato»), EF round-trip. Suite verde
  (450 tot, 0 warning).

## 8. Autorizzazione e trasferimento separati, varianti, velocità (2026-08-11)

Carta ed esito completi: [`../feature/2026-08-11-trasferimenti-acc-app.md`](../feature/2026-08-11-trasferimenti-acc-app.md);
schema autorevole: `../spec/modello-dati.md` §9.20-bis. Qui solo cosa cambia per quest'area.

Il modello descriveva **un evento con un livello**. Regge un accordo ACC↔ACC — al CoP il traffico entra e lì
passa il controllo — e non regge un ACC→APP, dove i due eventi sono distinti: «autorizza via CHI a FL160 o
superiore, trasferisce al confine dell'AoR passando FL110 in discesa, a 250 kt o inferiore».

- **Semantica chiarita, non cambiata**: `Cop` = punto/rotta d'**ingresso**; `Level*` = livello **autorizzato**.
  Su un ACC↔ACC sono anche il punto e il livello del trasferimento, perché i due eventi coincidono.
- **Faccetta trasferimento** su `TransferPoint`: dove passa il controllo (`HandoffKind` + `HandoffLabel`), a che
  livello (`HandoffLevel*`), dove passano le **comunicazioni** se altrove (`CommsHandoff*`), più la
  **velocità** (`SpeedValue`/`SpeedConstraint`). `HandoffKind = Unspecified` ⇒ riga identica a prima, frase
  compresa: è l'invariante che ha reso la migrazione un no-op sulle 73 righe in archivio.
- **Gruppo di varianti** (`VariantGroup` + `VariantDepth` + `IsGroupWide`): le righe dello stesso accordo,
  prima scollegate. Chiave sulla riga e non tabella figlia — vedi la carta per il perché. Righe piatte a valle:
  matcher, bridge e vista live continuano a vedere candidati distinti, che per loro è la lettura giusta.
  ⚠️ La **forma** del gruppo è cambiata il giorno dopo, prima del merge: vedi §9 qui sotto.
- **Frase**: con la faccetta cambia il **verbo**, quindi cambia template (`TemplateCleared` con `{handoff}` e
  `{handoffLevel}`). Velocità e comunicazioni sono code separate da virgola. Le parole del trasferimento
  vivono in `TransferHandoffText`, condiviso con la
  derivazione: le colonne arrivano alla vista **già a parole**, perché la lingua sta nel template (IT e EN).
- **Derivazione — la sezione estesa porta tutto ciò che entra o esce.** `CoordinationDerivation.Build`, passo 2,
  accettava solo `Kind == Arrival` da un `Ctr`: l'ACC **non vedeva le partenze** che i suoi APP gli consegnano.
  Il filtro è caduto. Nel bucketing dell'APP sono emerse due categorie che cadevano in silenzio — le partenze
  verso una torre e **qualunque** coordinamento con un altro APP — e quest'ultima ha ora il suo gruppo
  (`AppCoordination.TowardApps`).
- **Tabella condivisa.** `CoordTable.razor` rende la tabella dei coordinamenti per vIPI ACC, vIPI APP e vLOA:
  colonne **per presenza di dati** (mai uno switch su `SectorType`), gruppi di varianti con `rowspan` su CoP e
  ricevente così che a schermo resti il **delta**. Supera la
  nota del refactor 13 «resta di proposito la doppia resa dei coordinamenti»: restano due **viste** (l'albero è
  diverso), non più due tabelle.
- **Editor**: la faccetta sta su una riga propria della tabella di modifica (in linea non ci starebbe), «⑂»
  aggiunge una variante copiando tutto tranne la condizione, «⇤» la sfila; avviso non bloccante sui gruppi
  senza caso normale; filtro **«da rivedere»** = righe con ricevente APP e faccetta ancora vuota (le
  righe scritte prima, il cui livello può voler dire due cose e solo chi le ha scritte lo sa).
- **Aurora**: `CandidateLevel` porta ora entrambi i livelli, e l'etichetta `#LBALT` prende quello **al
  trasferimento** quando c'è — è la quota che il traffico ha nel momento in cui passa di mano.

## 9. Il gruppo di varianti diventa un outline (2026-08-12)

Carta ed esito: [`../feature/2026-08-12-varianti-a-livelli.md`](../feature/2026-08-12-varianti-a-livelli.md);
schema `../spec/modello-dati.md` §9.20-ter. Cambio deciso **prima del merge** del giorno prima, quindi a costo
di dati zero.

Il gruppo introdotto ieri aveva una forma sola — una capofila più subordinate, con «negli altri casi» in fondo
— e alla prima lettura del committente sono usciti tre difetti, di cui il terzo dice che la forma era proprio
quella sbagliata:

1. **l'ordine era rovesciato**: un accordo si legge come una norma, prima la regola generale e poi le
   eccezioni. La condizione standard va in testa;
2. **le alternative non sono subordinate a nessuno**: pista 07 e pista 25 sono pari-grado, e il dato reale in
   archivio è esattamente quello (righe 76/77, arrivi LIBD). Il modello non lo sapeva dire;
3. **due livelli non bastano**: «con area attiva» e, dentro, «con area attiva **e di notte**».

Più una quarta cosa chiesta esplicitamente: l'**eccezione trasversale**, che scavalca le alternative («di
notte, qualunque pista») e quindi non è né un'alternativa né l'eccezione di una capofila.

`IsOtherwise` lascia il posto a `VariantDepth` (int) + `IsGroupWide` (bool). Il gruppo diventa un **outline**:
l'ordine È la struttura, una riga appartiene all'ultima meno profonda che la precede, e il rango lo decide chi
scrive — non il tipo di condizione, perché «giorno/notte» sono pari-grado e sono `custom`, non pista.

Due conseguenze che valgono più della sintassi:

- **Tutto ciò che sposta una riga deve spostare il sottoalbero.** Una capofila che si muove lasciando indietro
  le sue eccezioni le riassegna a un'altra alternativa **senza nessun errore**: nessuna eccezione, nessun log,
  solo un accordo che dice un'altra cosa. `Subtree` è usato da spostamenti, distacco e inserimenti.
- **Frase e tabella divergono apposta.** In tabella si legge il delta (il rientro dà il contesto); nella frase
  si cumula la catena, perché viaggia da sola nella prosa. E la catena si **fonde** in una clausola prima di
  diventare parole: comporre un pezzo per livello ripeteva la preposizione — «con pista 07 in uso **e con**
  R403B attiva» — mentre la condizione cumulata è un AND unico, che la fraseologia approvata già sa dire.

L'avviso «alternativa con eccezioni ma senza un caso normale» scatta sul dato vero appena lo si apre nella
forma nuova: la riga 77 porta pista 25 **e** area R403B e non ha una «pista 25, normalmente». Il modello di
ieri non permetteva nemmeno di accorgersene.

## 10. Il modello diventa un **accordo** (dal 16 agosto 2026) 🟡

Carta, pre-flight e registro delle lacune:
[`../feature/2026-08-16-accordi-di-coordinamento.md`](../feature/2026-08-16-accordi-di-coordinamento.md).
Qui solo cosa cambia per quest'area.

Il modello descrive **un flusso di un settore verso un aeroporto**, e una riga dentro. Regge finché l'accordo è
uno solo, con un punto, un aeroporto e un mittente — e non è la forma di nessun documento reale. Nella LoA
EUROCONTROL (Annex D.2) e nell'IPI ENAV la tabella canonica è `rotta │ CoP │ livello │ condizioni`, **una per
direzione**, con gli aeroporti raccolti in un gruppo («LIRF-LIRA-LIRU-LIRE») e i punti in un elenco.

- **Sostituzione, non affiancamento**: `CoordinationAgreement` (+ `AgreementParty`, `AgreementAirport`,
  `AgreementClause`) prende il posto di `TransferFlow`/`TransferPoint`, che spariscono dallo schema.
- **`TransferFlowRow`/`TransferPointRow` restano e cambiano ruolo**: da DTO di lettura dello storage a
  **proiezione** dell'accordo, prodotta da `AgreementExpansion`. È perché `CoordinationDerivation`, il composer
  delle frasi, `CoordTable`, la vista live e il matcher Aurora **non si toccano**. Stesso schema dei settori
  (cataloghi = fonte unica, `Sector` = proiezione).
- **Cinque duplicazioni chiuse dal modello**: aeroporto, punto, settore mittente, direzione, ACC. Le prime
  quattro erano visibili nell'archivio; la quarta era già degenerata (i sorvoli LIBB↔LGGG elencano punti diversi
  nei due versi, e niente lo segnalava).
- **La rete viene prima del modello.** `CoordinationCharacterizationTests` deriva i **flussi veri** (37/78,
  congelati in `tests/Vipi.Application.Tests/Fixtures/`) e confronta righe e frasi con un file approvato, in
  italiano e in inglese. L'invariante del lavoro è che resti verde.
- **Parità di campi in questo giro.** I campi che i documenti reali richiedono e che ancora mancano — rotta
  distinta dal punto, *release*, modo di coordinamento, nota per clausola, default in testa, condizione come
  intestazione, clausole in prosa — sono un **secondo giro dichiarato** (§5 della carta), progettato nel posto
  dove atterrerà.

### 10.1 Cosa è stato fatto, in ordine

| # | Cosa | Verificato |
|---|---|---|
| 0 | Carta, registro delle lacune, **rete di caratterizzazione** sui 78 punti veri | invariante «frasi e righe identiche prima/dopo» |
| 1 | Quattro entità + migrazione nei due provider + `CopList`/`FlowsToAgreements`/`AgreementExpansion` + porta di scrittura | il **cancello**: giro completo lossless sull'archivio |
| 2 | Travaso armato all'avvio, lettori sugli accordi, editor riscritto, vecchia scrittura rimossa | live: 41 accordi, albero per controparte, due versi |
| 3 | Punti richiusi in una riga, prosa a scelta della sezione, colonna «Anche per» | live: otto celle con l'elenco dei punti |
| 4 | Riceventi proposti · incolla-tabella · cruscotto delle lacune | live: **due asimmetrie vere** trovate |
| 5 | Propagazione: spec, lavori aperti, guida, memorie, nomi e commenti | — |

### 10.2 Cosa resta

1. ⚠️ La migrazione che **droppa `TransferFlows`/`TransferPoints`**, in una release **successiva** a quella in
   cui il travaso ha girato in produzione. Le migrazioni girano *prima* della manutenzione d'avvio: nella stessa
   release il travaso non troverebbe più niente da leggere.
2. Le **due asimmetrie** che il cruscotto ha trovato — `LGGG ⇄ LIBB` (BELIX, OLGAT) e `LDZO ⇄ LIBB` (sei punti,
   mai notata prima) — le decidono i colleghi: il travaso non le ha risolte apposta.
3. Il **secondo giro** dei campi mancanti (§5 della carta).
4. **Merge in `main`**: serve l'ok esplicito.
