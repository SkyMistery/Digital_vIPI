# Accordi a SEZIONI — un accordo per coppia, il traffico dentro 🟢

> **Carta, 18 agosto 2026 — approvata e ESEGUITA lo stesso giorno.** Cosa l'esecuzione ha smentito sta in
> fondo, sotto «Cosa è cambiato eseguendo»: due numeri e un difetto che nessun test vedeva.
> Terzo giro del modello dei coordinamenti, dopo
> [`2026-08-16-accordi-di-coordinamento.md`](2026-08-16-accordi-di-coordinamento.md) (il modello) e
> [`2026-08-17-editor-accordi-per-relazione.md`](2026-08-17-editor-accordi-per-relazione.md) (l'editor).
> Ramo `feature/accordi-coordinamento`, non ancora in `main`.

## In una riga

L'accordo smette di essere «due parti · **un tipo** · **un gruppo di aeroporti**» e diventa **la relazione fra
due enti, e basta**: uno solo per coppia, sempre bidirezionale. Il tipo di traffico e gli aeroporti scendono di
un livello, in **sezioni** dentro l'accordo — *arrivi verso LIBD·LIBR*, *partenze da LIRF*, *sorvoli A→B*,
*sorvoli B→A* — e le clausole stanno nelle sezioni.

## Cosa è stato deciso (18 agosto, committente)

| Bivio | Deciso |
|---|---|
| «1 a 1» | **Un ente per lato** (non una lista) **e coppia unica**: `AgreementParty` sparisce, due FK sull'accordo |
| Verso di arrivi/partenze | **Uno solo**, *proposto* dall'aeroporto e correggibile. I sorvoli restano due sezioni |
| Aeroporti di una sezione | **Gruppo** (`LIRF·LIRA·LIRU·LIRE`), come i documenti veri e come oggi |
| Migrazione | **Fonde e ripulisce**: sezioni gemelle unite in automatico, gusci senza ricevente e senza clausole eliminati |

## Le misure, prima del disegno

Sul `vipi.db` di sviluppo, **18 agosto** (il committente lo modifica dal vivo: rimisurare prima di eseguire).

```
40 accordi · 60 clausole · 36 aeroporti · tutte le clausole AtoB · zero bilaterali
tipi: Arrival 27 · Departure 4 · Overflight 9
aeroporti per accordo: 1 → 26 accordi, 2 → 5 accordi
enti per lato: 1 ovunque (zero accordi con più di un ente su un lato)
```

Tre numeri portano il disegno:

1. **40 accordi stanno in 16 coppie**, più un guscio senza controparte. La coppia
   `LGGG_W_CTR ⇄ LIBB_ES_CTR` da sola ne tiene **otto**. Oggi
   l'utente che vuole vedere «cosa ho concordato con Atene» apre otto schede; domani ne apre una con otto
   sezioni.
2. **Il verso oggi si esprime ORIENTANDO l'accordo**, non con `Direction`: 60 clausole su 60 sono `AtoB`, e la
   stessa coppia compare due volte a versi opposti (`#13` LIBB→LGGG e `#32` LGGG→LIBB). Questo è il motivo per
   cui «zero bilaterali» non voleva dire «nessuno ha scritto il reciproco»: il reciproco c'era, in un altro
   accordo. Il modello a sezioni **usa `Direction` per davvero**, e quel doppione diventa impossibile.
3. **Nessun accordo ha più di un ente per lato.** La collezione `AgreementParty` costa un prodotto cartesiano
   in `AgreementExpansion`, una tabella, un ordine e un picker multiplo, e in archivio non è mai stata usata.
   ⚠️ **Il prezzo è dichiarato:** la forma «TS EXE trasferisce a PS EXE / PN EXE» dei documenti reali si
   scriverà come **due accordi**. È la decisione del committente, non un effetto collaterale.

## Il modello nuovo

```
CoordinationAgreement           la RELAZIONE: OwnerAcc, SideASectorId, SideBSectorId, Note, Order
└── AgreementSection            il TRAFFICO: Kind, Direction, Description, Order
    ├── AgreementAirport        (Icao, Name, Order)      — zero per sorvoli/VFR
    └── AgreementClause         invariata, meno Direction, più SectionId
```

### `CoordinationAgreement` — cosa resta

| Campo | Nota |
|---|---|
| `OwnerAccId` | invariato: serve **solo** all'autorizzazione, la visibilità passa dai due lati |
| `SideASectorId` / `SideBSectorId` | **nuovi, NOT NULL, FK `Sectors`**. Rimpiazzano `Parties` |
| `Note` | l'ex `Description`: era mostrata **solo** nel navigatore (verificato: nessun consumatore a valle la legge). Resta lì, col nome che dice cosa è |
| `Order` | invariato |
| ~~`TrafficKind`~~ | **scende sulla sezione** |
| ~~`Parties`~~, ~~`Airports`~~, ~~`Clauses`~~ dirette | `Sections` |

**Unicità:** indice univoco sulla **coppia non orientata**. In SQL non esiste «insieme di due», quindi si
salva in **forma canonica**: `SideASectorId < SideBSectorId` garantito dall'applicazione, indice univoco
`(SideASectorId, SideBSectorId)`.

⚠️ **E qui una cosa va guardata in faccia:** ordinare i lati per id **riscrive A e B**, cioè fa esattamente ciò
che le due carte precedenti vietavano («A e B in archivio non si toccano»). Il divieto valeva perché **il verso
delle clausole dipendeva dall'orientamento**: girare i lati capovolgeva il significato di tutto. Da qui in poi
il verso sta sulla **sezione** e la canonizzazione lo porta con sé (`AtoB ⇄ BtoA` si scambiano insieme ai
lati), quindi girare la coppia diventa un'operazione **senza perdita** — ed è la stessa cosa che fa la
migrazione, una volta, per fondere `#13` e `#32`. La lente `AgreementViewpoint` continua a decidere chi è
«noi»: **quella parte non cambia**.

> **Alternativa scartata:** lasciare A e B come capitano e mettere l'unicità su una colonna calcolata
> `min|max`. Costa una colonna ridondante in due provider e un trigger di coerenza, per non fare una volta
> quello che la migrazione fa comunque.

### `AgreementSection` — l'entità nuova

| Campo | Cosa dice |
|---|---|
| `Kind` | `TransferFlowKind` (Arrival · Departure · Overflight · Vfr · Other): **l'ex tipo dell'accordo** |
| `Direction` | `AgreementDirection` (`AtoB`/`BtoA`): il verso della sezione. Una sezione = **una** tabella |
| `Airports` | il gruppo di scali. **Obbligatorio ≥1 per Arrival/Departure** (regola confermata dal committente); vietato su Overflight; libero su Vfr/Other |
| `Description` | prosa che introduce la tabella (l'IPI ENAV la scrive così) |
| `Order` | ordine dentro l'accordo |

⚠️ **Il nome.** `Section` in questo codice è già la sezione di un **documento** (`DocumentSection`,
`SectionCatalog`, `SectionView`, `RawSection`…). L'entità nuova si chiama `AgreementSection` **sempre**, e nel
codice non esiste una variabile `section` nuda nell'area accordi. È l'unico modo perché fra sei mesi «sezione»
resti cercabile.

### `AgreementClause` — cosa cambia

Due campi, nient'altro: `AgreementId` → **`SectionId`**, e `Direction` **sparisce** (la dice la sezione). Tutti
gli altri — livello, parità, stato verticale, faccetta trasferimento, comunicazioni, velocità, condizione a tre
dimensioni, outline `VariantGroup`/`VariantDepth`/`IsGroupWide` — restano identici, con lo stesso significato.

⚠️ **L'outline vive dentro la SEZIONE.** Prima viveva in `(accordo, verso)`, che è esattamente la stessa cosa
detta con due chiavi invece di una: spostare, annidare e sciogliere ragionano su una sezione sola. Nessuna
operazione attraversa due sezioni — perché due sezioni sono **due tabelle**, non alternative.

### Il verso, e come si propone

Una sezione **arrivi verso LIRF** ha un verso solo: cede chi non ha LIRF, riceve chi ce l'ha. Il verso non si
indovina a runtime, si **salva** — ma quando l'utente aggiunge la sezione il verso arriva già proposto:

```
lato che "possiede" l'aeroporto = quello il cui settore copre l'ICAO
   1. Airport.ParentCallsign (e la sua catena) == callsign di un lato   → quel lato
   2. altrimenti  Airport.Acc == Acc del settore di un lato             → quel lato
   3. altrimenti (nessuno, o entrambi)                                  → A→B, e l'utente corregge
Arrivi    : verso il lato che possiede
Partenze  : dal lato che possiede
```

**Provata contro l'archivio** prima di scriverla: sulla coppia `LIBB ⇄ LGGG` la regola dà `A→B` per gli arrivi
LGKF (greco) e `B→A` per gli arrivi LIBD (italiano) — cioè esattamente i versi con cui `#16` e `#33` sono
scritti oggi. La freccia resta **visibile e cliccabile** nella testata della sezione: è una proposta, non un
calcolo che si impone.

## La migrazione — «fonde e ripulisce»

**Logica pura, testabile, in `Application`** (`AgreementsToSections`), scritta a mano e non in SQL: la fusione
richiede canonizzazione della coppia, ribaltamento dei versi e unione delle gemelle, e farla due volte in due
dialetti SQL sarebbe due volte il rischio per lo stesso risultato.

```
1. raggruppa per COPPIA NON ORIENTATA di settori          40 accordi → 16 coppie (misurato)
2. per ogni coppia: A/B canonici (id minore = A)
3. ogni vecchio accordo → una SEZIONE
       Kind, Airports, Description, Order   dal vecchio accordo
       Direction = (vecchio A == A canonico) ? AtoB : BtoA
   le clausole passano intatte sotto la sezione, ORDINE COMPRESO
4. GEMELLE (stesso Kind + stesso verso + stesso insieme di aeroporti) → una sezione sola,
   clausole concatenate nell'ordine dei vecchi id, VariantGroup rinumerati per non collidere
5. SCARTI: accordo senza un lato E senza clausole  → eliminato (oggi: #41)
           accordo con i due lati ma senza clausole → sopravvive come sezione VUOTA
6. rapporto scritto a video: coppie, sezioni, gemelle unite, scarti — riga per riga
```

**Cosa ha prodotto, misurato eseguendolo su una copia del `vipi.db` vero:** **16 accordi, 38 sezioni, 60
clausole su 60**, una fusione di gemelle (`#26`/`#27`, entrambi arrivi LIBD `AtoB` — la «relazione spezzata» che
l'editor mostrava come due foglie identiche), un solo scarto (`#41`, `LIRR_NE_CTR`, senza ricevente e senza
clausole), 35 aeroporti (36 meno quello che le gemelle avevano in due). ⚠️ `#42` (partenze LIBD·LIBR, zero
clausole) ha **entrambi i lati** e **sopravvive** come sezione vuota: è una scrittura in corso del committente,
non spazzatura.

⚠️ **Le tre coppie con «il reciproco a parte» spariscono da sole** (`#13`/`#32`, `#17`/`#28`, `#23`/`#38`): non
serve più il comando «unisci i due versi», e la voce aperta n.0 dell'handoff si chiude qui.

⚠️ **Le due asimmetrie note NON si toccano** — `LGGG ⇄ LIBB` (BELIX di qua, OLGAT di là) e `LDZO ⇄ LIBB` (sei
punti da un lato solo). Dopo la fusione stanno nello **stesso accordo**, una sezione sopra l'altra, quindi
finalmente si **vedono**; sceglierne una è una decisione dei colleghi, non una migrazione.

### Sequenza (la trappola del travaso precedente, evitata)

L'handoff lo dice a caratteri cubitali: le migrazioni girano **prima** della manutenzione d'avvio, quindi
«migrazione che droppa + passata che legge» nella stessa release perde i dati **senza un errore**. Qui il
travaso **non gira all'avvio**:

| Passo | Cosa | Dove |
|---|---|---|
| 1 | Migrazione **additiva** (due provider): `AgreementSections`, `SideA/BSectorId` *nullable*, `SectionId` *nullable* su clausole e aeroporti | schema |
| 2 | **Conversione one-shot**, comando esplicito su una **copia** del `vipi.db`, con rapporto a video e confronto della proiezione prima/dopo | mano |
| 3 | Migrazione **distruttiva**: `NOT NULL` sulle colonne nuove, via `AgreementParties`, `TrafficKind`, `Direction`, `AgreementId` da clausole e aeroporti | schema |

Si può fare in tre passi separati **per la stessa ragione della volta scorsa, e solo per quella**: il DB di
produzione **viene sostituito** con quello di sviluppo già convertito. Non è una regola generale.

## La proiezione, e i cinque consumatori

`AgreementExpansion` continua a produrre `TransferFlowRow`/`TransferPointRow` **immutati**: derivazione (vIPI
ACC, vIPI APP, vLOA), composer delle frasi, vista live, stampa e matcher Aurora **non si toccano**. Cambia solo
da dove legge:

| Prima | Adesso |
|---|---|
| per accordo: prodotto `mittenti × aeroporti × versi` | per **sezione**: `1 mittente × aeroporti` |
| `Kind`, `AirportIcao` dall'accordo | dalla **sezione** |
| ricevente = ogni ente del lato opposto | **il** settore del lato opposto |
| `Order` del flusso = `Order` dell'accordo | `Order` dell'accordo, poi della sezione |

⚠️ **Il cartesiano sparisce**, e con lui il commento che lo difendeva. Con un ente per lato una sezione produce
`1 × N aeroporti` righe-flusso, come oggi in tutti i 40 casi reali.

> **Invariante che regge tutto il giro:** `tests/…/CoordinationCharacterizationTests` + `real-coordination.approved.txt`
> **non cambiano di un carattere**. Se la conversione è corretta, le righe derivate dai dati veri sono le stesse
> — stesse frasi, stesso ordine. Un `.received.txt` che compare è un difetto della conversione, non un file da
> riapprovare. ⚠️ Per questo il passo 4 della conversione **conserva l'ordine** delle clausole e il passo 3
> l'`Order` dell'accordo vecchio: l'ordine è ciò che la rete confronta.

**Release congelate:** intatte. Gli snapshot serializzano `AccCoordination`/`AppCoordination` a partire dalla
**proiezione**, che non cambia forma. `AppCoordRow` non si tocca — vincolo di sempre, additivo o niente.

## L'editor

Tre colonne come oggi, ma la prima si **accorcia** e la seconda si **struttura**.

### Colonna 1 — l'albero perde un livello

```
PRIMA                                    ADESSO
ACC controparte                          ACC controparte
└─ relazione (noi ⇄ loro)                └─ accordo (noi ⇄ loro)     ← la relazione È l'accordo
   └─ accordo (tipo · aeroporti)            [3 sezioni · 8 clausole · ⚠]
```

La relazione e l'accordo erano due livelli perché una coppia poteva avere più accordi. Adesso non può: il
livello 3 sparisce, e con lui la foglia che ripeteva il nome della coppia. Restano sul nodo i conteggi che
fanno alzare la mano (sezioni, clausole, avvisi) — la ragione per cui esistevano non cambia: **l'avviso deve
stare dove si sceglie**.

### Colonna 2 — le sezioni dell'accordo

Testata dell'accordo: `LIBB_ES_CTR ⇄ LGGG_W_CTR`, i due enti (modificabili), la nota, i tasti dell'accordo.
Sotto, le sezioni, ognuna col suo titolo, la sua freccia e la sua tabella:

```
┌ Arrivi → LGKF                                       LIBB_ES_CTR → LGGG_W_CTR   [+ clausola] [⋯]
│  … tabella clausole (outline, varianti, scrittura in cella: invariata) …
├ Arrivi → LIBD · LIBR                                LGGG_W_CTR → LIBB_ES_CTR   [+ clausola] [⋯]
│  …
├ Sorvoli                                             LIBB_ES_CTR → LGGG_W_CTR   [+ clausola] [⋯]
│  …
└ Sorvoli                                             LGGG_W_CTR → LIBB_ES_CTR   ⚠ vuota — il reciproco non è scritto
```

Quattro scelte, ognuna con la sua ragione:

1. **I sorvoli si mostrano sempre in coppia.** Se esiste la sezione sorvoli in un verso, l'altro verso appare
   comunque, vuoto e marcato. È la stessa regola del 17 agosto («due versi sempre a vista»): l'interruttore
   nascondeva ciò che mancava, e per questo il reciproco non si scriveva. Vale **solo** per i sorvoli, perché
   solo lì il reciproco è la stessa sezione girata; il reciproco degli arrivi a LIRF sono le **partenze** da
   LIRF, che è una sezione diversa e non sempre esiste.
2. **Arrivi e partenze dello stesso scalo si accostano.** Ordine di presentazione: per aeroporto
   (arrivi, poi partenze), poi sorvoli (i due versi), poi VFR/Altro. `Order` manuale dentro il gruppo.
3. **Le sezioni si collassano, e lo stato sta nell'URL** (`?acc=&agr=&sec=`), come già le altre sezioni
   collassabili degli editor. Un accordo con otto sezioni non deve costringere a scorrere per arrivare alla
   nona.
4. ⚠️ **Una sezione col corpo nascosto non perde niente:** una sezione collassata resta contata nella testata
   («3 sezioni · 8 clausole»). Il campo nascosto che tiene dati è il modo più rapido di perderli — trappola già
   pagata sul blocco aeroporti.

### Creare

- **Un accordo** = scegliere **due enti**. Nient'altro, come dal 18 agosto. ⚠️ Se la coppia esiste già, il form
  **non dà errore**: apre l'accordo che c'è. Un doppione non è un errore dell'utente, è una domanda a cui
  esiste una risposta migliore di «no».
- **Una sezione** = scegliere il **tipo**; se è arrivi/partenze, subito gli aeroporti (il picker si apre da sé,
  perché senza aeroporto la sezione non è valida). Il verso arriva proposto.
- ⚠️ **Niente catch-22**, e stavolta è per costruzione: il tipo si sceglie *creando* la sezione, e gli
  aeroporti stanno nella stessa schermata. Il giro di ferragosto ci è cascato all'incontrario (per dire
  «arrivi» serviva un aeroporto, per aggiungerlo serviva aver detto «arrivi»).

### Regole di validità

| Regola | Dove vive |
|---|---|
| Due enti, sempre, e **diversi** | accordo — un ente non concorda con se stesso |
| Coppia unica | indice univoco + il form che apre l'esistente |
| Arrivi/partenze pretendono ≥1 aeroporto | sezione (conferma del committente) |
| Sorvoli non hanno aeroporti | sezione |
| Uno stesso ICAO in **due** sezioni con lo stesso tipo e verso → **avviso**, non errore | sezione — è ciò che rifà le gemelle appena unite, ma per un `LIRF` che arriva da due condizioni diverse la risposta giusta sono le **varianti**: si segnala e si offre «unisci» |
| Ripristino **fuori** dalle regole | come oggi, di proposito, con il suo test — un annulla che rifiuta di rimettere ciò che ha appena cancellato è peggio della regola |

## Cosa muore in questo giro (domanda 4 del pre-flight)

| Muore | Perché | Chi va aggiornato |
|---|---|---|
| `AgreementParty` (entità, tabella, `AgreementPartyRow`, picker multiplo) | un ente per lato | `EfAgreementRepository`, `AgreementExpansion`, `AgreementInput`, editor, test |
| `AgreementClause.Direction` | la dice la sezione | proiezione, repo, editor, snapshot |
| `AgreementMerge` + `AbsorbAsReverseAsync` + il riquadro «il reciproco a parte» | impossibile per costruzione: due versi della stessa coppia **sono** lo stesso accordo | `IAgreementRepository`, `IAgreementService`, editor, `AgreementGaps`, test |
| `CopyDirectionAsync` (accordo) | diventa **«copia la sezione nel verso opposto»**, che è un'altra firma | idem |
| `AgreementGapKind.NoReceiver` | il lato B è `NOT NULL`: la lacuna non può più esistere. Resta un **rapporto di conversione**, non una voce viva | cruscotto, test |
| `AgreementGapKind.ReciprocalApart` | idem | idem |

⚠️ **Propagazione, nello stesso giro e non «dopo»:** `docs/spec/modello-dati.md` §9.25-9.26,
`docs/refactor/07-trasferimenti.md` §10, `docs/lavori-aperti.md` (E6-bis chiude, voci 0 e 2 dell'handoff
chiudono), l'handoff, le due carte precedenti (che restano **storia valida nelle decisioni**, marcate come
superate nel modello) e la **memoria** `accordi-di-coordinamento`.

## Cosa proporrei di aggiustare, già che si passa di qui

Quattro dentro, tre fuori. Le tre fuori sono difetti veri ma slegati: metterli qui rende il giro
irrevisionabile.

**Dentro:**

1. **`Description` → `Note` sull'accordo, e prosa sulla sezione.** Oggi `Description` è mostrata **solo** nel
   navigatore (verificato: nessun consumatore a valle la legge) e si chiama come un campo derivato che non è.
   Sulla sezione la prosa serve davvero: è la frase che introduce la tabella nei documenti reali.
2. **Il cruscotto delle lacune si restringe a ciò che resta vero.** Perse due voci su costruzione, ne
   guadagna una che oggi non si poteva porre: **«sezione senza clausole»** (il `#42` di adesso) e **«sorvoli in
   un verso solo»** dentro lo stesso accordo. `AsymmetricDirections` sopravvive e migliora: i due versi
   confrontati sono ora vicini per costruzione.
3. **Il ricevente di una clausola sparisce del tutto dall'editor.** Con un ente per lato non c'è più niente da
   scegliere: la colonna «ricevente» delle tabelle diventa la **freccia della sezione**, scritta una volta in
   testata. È il campo che più spesso si contraddiceva fra righe sorelle.
4. **Deep-link alla sezione** (`?acc=&agr=&sec=`): serve alla verifica live e all'aiuto in-app, e costa un
   parametro.

**Fuori (voci separate, da tenere aperte):**

- I **tre difetti di `LevelFormatting`** congelati nell'approvato (`FL260 (pari)` in inglese, `— (dispari)`,
  la parità appesa a un livello speciale). Un giro loro, con la riapprovazione guardata riga per riga: qui
  cambierebbero l'approvato **insieme** alla conversione, e non si capirebbe più quale dei due l'ha mosso.
- `InlineConfirm.ConfirmLabel` con default «Sì, elimina» **italiano e cablato**, che esce così anche nella
  pagina inglese e anche per azioni che non eliminano.
- `AuroraClientTests.Richieste_in_sequenza_non_si_mescolano` instabile (E6-ter).

## Slice — un commit per passo, build verde a ogni passo

| # | Passo | Fine | Stato |
|---|---|---|---|
| 1 | `AgreementsToSections` **puro** + test sulla fixture reale: coppie, versi ribaltati, gemelle, scarti | la fusione è provata **senza** database | ✅ 11 test |
| 2 | Entità + `AgreementSection` + migrazioni **additive** (due provider, scritte a mano) | schema pronto, niente legge ancora il nuovo | ✅ |
| 3 | `AgreementRow`/`AgreementSectionRow`/`AgreementInput` + `AgreementExpansion` sul nuovo modello | ⚠️ `real-coordination.approved.txt` **invariato** | ✅ nessun `.received` |
| 4 | `EfAgreementRepository` + `IAgreementRepository`/`IAgreementService` (sezioni, outline per sezione, snapshot annidato) | le scritture funzionano sul nuovo modello | ✅ |
| 5 | Conversione one-shot su **copia** del `vipi.db`, rapporto letto riga per riga | dati convertiti, e la prova che sono gli stessi | ✅ 60/60 clausole |
| 6 | Migrazioni **distruttive** + rimozione di ciò che muore (`AgreementParties`, `AgreementMerge`, `TrafficKinds`, due voci del cruscotto) | nessun nome morto | ✅ |
| 7 | Editor: albero a due livelli, riquadro a sezioni, form di creazione, deep-link | ⚠️ **verifica live sulla 5035** (la 5034 è del committente) | ✅ guidata a schermo |
| 8 | Doc, spec, handoff, memoria | tracciamento coerente | ✅ |

**Cancelli:** `dotnet build Vipi.slnx -c Release --no-incremental` (avvisi = errori, **due TFM**) e
`dotnet test Vipi.slnx` ≥ 2581. ⚠️ Fermare `Vipi.Host` prima di compilare (`MSB3021`), e controllare l'ora di
`src/Vipi.Host/bin/Debug/net8.0/Vipi.Application.dll` prima di credere a ciò che si vede a schermo.

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — si **sostituisce**, non si affianca: `AgreementSection` prende i due campi che l'accordo
   perde, `AgreementParty` sparisce. Chi cerca «dove si salva un coordinamento» continua a trovare **un** posto.
2. **Dispatch** — nessuno switch nuovo per tipo. `TransferFlowKind` si consulta dove già si consultava (frasi,
   etichette), e resta uno solo: la sezione lo porta invece dell'accordo.
3. **Ingressi + verifica** — rotta invariata `/vsop/admin/trasferimenti?acc=LIBB`; si crea un accordo dai due
   enti e una sezione dal suo tipo, nessun catch-22. Verifica **guidando l'editor** su porta 5035 con la skill
   `.claude/skills/verifica-live/`, più la rete di caratterizzazione come cancello sui dati veri.
4. **Propagazione** — è la sezione «cosa muore»: sei tipi/campi rimossi, sei documenti e una memoria da
   riscrivere **nello stesso giro**.

## Rischi, in ordine di quanto fanno male

1. ⚠️ **Perdere una clausola nella fusione.** Mitigazione: logica pura provata prima (slice 1), conversione su
   **copia**, rapporto riga per riga, e il confronto della **proiezione prima/dopo** come prova d'identità —
   più il backup fuori repo `../../vipi.db.bak-pre-travaso-20260817` e uno nuovo prima del passo 5.
2. ⚠️ **L'approvato che si muove per l'ordine.** Se l'ordine dei flussi cambia, la rete diventa rossa per un
   motivo che non è un difetto — e la tentazione è riapprovare. La conversione **conserva** `Order` a ogni
   livello proprio per questo; un `.received.txt` va **letto**, mai accettato al volo.
3. **La forma «un mittente, più riceventi» diventa inesprimibile.** Decisione presa; va scritta nella memoria e
   nella spec, o fra sei mesi sembrerà una dimenticanza.
4. **L'editor è 2900 righe**, ed è il pezzo dove i test non vedono niente (attributo `string` senza `@`,
   `<select>` che non torna indietro, `return` dentro un elemento aperto). La slice 7 va guidata a schermo,
   non dedotta.

## Deciso prima di partire (18 agosto, committente)

1. **Lo stesso ICAO in due sezioni con stesso tipo e verso** → **avviso + tasto «unisci»**, non errore duro. Un
   `LIRF` che arriva da due condizioni diverse si scrive con le **varianti**, e vietare non lo insegna.
2. **VFR/Altro** → aeroporti **facoltativi**. È la regola «dove non sono esclusi», già pagata una volta.
3. **Ordine delle sezioni** → **imposto** (aeroporto ▸ tipo ▸ verso), con `Order` manuale dentro il gruppo dello
   stesso aeroporto.

## Cosa è cambiato eseguendo

Tre cose, e nessuna delle tre l'avrebbe detta un test.

1. **Le coppie sono 16, non 17.** Il diciassettesimo era `#41` — `LIRR_NE_CTR` senza controparte — che la mia
   misura di partenza aveva contato come una coppia `('?', 'LIRR_NE_CTR')`. Non è una coppia: è un guscio, e la
   conversione lo butta perché non ha né un capo né clausole.

2. ⚠️ **La cancellazione dei gusci assorbiti si portava via le clausole.** Fra la migrazione additiva e quella
   finale lo schema è **misto**: `AgreementClauses.AgreementId` esiste ancora, col suo FK in cascade. Riappendere
   la clausola alla sezione giusta e lasciare il vecchio `AgreementId` puntato al guscio significa che, quando il
   guscio viene cancellato, la clausola se ne va con lui — **col `SectionId` già scritto bene**. Misurato
   rileggendo dopo l'apply: delle 60 clausole ne sopravvivevano **23**, e nessun errore lo diceva. La conversione
   adesso sposta anche il vecchio `AgreementId`, e il tool **si rifiuta di girare due volte** (una seconda
   passata rileggerebbe le righe già convertite come se fossero ancora vecchie, rifondendo accordi già fusi).

3. **`MySqlStringLengths.Map` citava colonne morte e non le due nuove.** L'ha detto una guardia che esisteva già
   (`IndexedStringLengthTests`): `AgreementSection.Kind` e `.Direction` stanno in un indice, e senza una
   lunghezza dichiarata su MySQL nascono `longtext` — errno 1170 al `CREATE TABLE`. `AgreementClause.Direction`
   e `AgreementParty.Side` invece non esistono più. È il tipo di difetto che si vede solo perché qualcuno, a
   luglio, ha scritto il test che lo cerca.

**Cancelli, a fine giro:** `dotnet build -c Release --no-incremental` verde su tutte le librerie (0 warning, due
TFM) e **2062 test verdi** — Domain 114×2, Application 492×2, Infrastructure 391+382, Ui 177×2, Hosting 57×2,
AuroraBridge 78. ⚠️ `Vipi.Host` e `Vipi.E2E.Tests` non hanno potuto compilare: l'host del committente era acceso
e teneva i DLL (`MSB3021`, la trappola nota). Vanno rifatti a host spento, insieme alla verifica live.

## Come si esegue la conversione, in ordine

```
# 0. backup, FUORI dal repo
cp src/Vipi.Host/vipi.db ../vipi.db.bak-pre-sezioni-20260818

# 1. schema nuovo, tutto nullable: non tocca niente di ciò che c'è
dotnet ef database update 20260818115830_AgreementSectionsAdditive \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure --framework net8.0

# 2. i dati. SENZA --apply stampa il piano e non scrive: si guarda PRIMA.
dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite src/Vipi.Host/vipi.db
dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite src/Vipi.Host/vipi.db --apply

# 3. NOT NULL, indice unico, via il vecchio
dotnet ef database update 20260818115838_AgreementSectionsFinalize \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure --framework net8.0
```

⚠️ **Su MariaDB si usa `--mysql "<conn>"`** e le due migrazioni gemelle di `Vipi.Infrastructure.MySqlMigrations`.

⚠️ **Il passo 3 fallisce se il passo 2 non è girato**, ed è la protezione: il `NOT NULL` non passa su colonne
ancora nulle e l'indice unico non passa se due accordi descrivono ancora la stessa coppia. Un fallimento
rumoroso è l'unica difesa che vale — la trappola di ferragosto era una passata che «non trova niente, scrive
zero, e i dati spariscono senza un errore».

## La verifica live — cosa ha detto lo schermo

Guidata con Edge+puppeteer sulla **5035**, su una copia del `vipi.db` già convertita nelle tre fasi.

**Ha confermato**, e non erano cose che i test potessero dire:

| Cosa | Esito |
|---|---|
| Albero a **due** livelli | 6 rami ACC, 16 accordi, **zero** `.xt-nav-rel` superstiti |
| L'accordo che erano otto schede | `LIBB_ES_CTR ⇄ LGGG_W_CTR` — **una foglia, «8 ▤ 13»** |
| **Ordine imposto** | LGKF · **LIBD arrivi+partenze accostate** · LIBG · **LIBR arrivi+partenze accostate** · sorvoli nei due versi |
| **Verso proposto dall'aeroporto** | arrivi a LGKF (greco) → `noi→loro`; a LIBD/LIBG/LIBR (italiani) → `loro→noi` |
| Il verso **gira digitando lo scalo** | form su accordo LYBA: nasce `LIBB→LYBA`, aggiungo **LIBD**, diventa `LYBA→LIBB` |
| Tasto **⇄** | rigira la proposta e la rigira indietro |
| **Reciproco mancante** | blocco fantasma `LYBA_CTR → LIBB_ES_CTR` sotto il sorvolo scritto in un verso solo |
| «copia l'altro verso» | *«Copied 1 clauses into the opposite direction: levels must be reviewed»*, e il fantasma diventa sezione |
| **Gemelle** | seconda sezione identica → avviso + tasto; unite, l'avviso **sparisce** e le sezioni tornano 4 |
| **Deep-link** | `?acc=LIBB&accordo=4&sezione=6` ricaricato riproduce le 8 sezioni |
| Conteggi vivi | 60→61 clausole dopo la copia, Gaps 23→22 |

**E ha trovato tre difetti che il DOM non diceva** — due miei, uno di etichetta:

1. ⚠️ **L'avviso «LIBD è coperto da LIBD_CS0_APP, che non è fra i riceventi» urlava su tre sezioni su otto**, e
   tutte e tre erano scritte bene: un'**area** che riceve arrivi e poi li gira all'avvicinamento è il caso
   **normale**. Ristretto ai riceventi di tipo **APP o torre**. *Una categoria che urla sempre non si guarda
   più* — è la seconda volta che questa pagina lo impara, dopo il cruscotto delle lacune.
2. **L'avviso stava nella TESTATA della sezione** e mandava a capo «Paste table ✎ ✕» sotto «+ Clause»: la riga
   porta già traffico, coppia, freccia, scali, conteggio e quattro tasti. Sceso nel **corpo**, come gli altri.
3. **Cinque etichette descrivevano l'operazione vecchia**: il tasto delle gemelle diceva «Merge the two
   **directions**» / «Unisci i due **versi**» — ma lì non si uniscono due versi, si uniscono due **sezioni
   gemelle dello stesso verso**. Riscritte nelle due lingue.

⚠️ Il `vipi.db` del progetto è rimasto **intatto**: tutto è girato sulla copia (`git status` pulito su quel file
a fine giro).

**Difetto pre-esistente lasciato lì**, perché è fuori dal giro e vale in tutte e due le lingue: `Xfer_ClausesCount`
rende «1 clauses» / «1 clausole» — plurale su uno.

## L'esecuzione sull'archivio vero (18 agosto)

Backup fuori dal repo (`../vipi.db.bak-pre-sezioni-20260818`, 5,1 MB), poi i tre passi, poi le verifiche.

```
40 accordi · 79 parti · 36 aeroporti · 60 clausole
      ↓
16 accordi · 38 sezioni · 35 aeroporti · 60 clausole
```

| Controllo | Esito |
|---|---|
| Clausole ritrovate | **60 su 60**, nessuna persa e nessuna inventata |
| Clausole / aeroporti / sezioni orfani | 0 · 0 · 0 |
| Lati mancanti · coppie duplicate · canonico violato | 0 · 0 · 0 |
| `AgreementParties` | droppata |
| `pragma integrity_check` · `foreign_key_check` | `ok` · 0 violazioni |
| L'app ci gira | vIPI ACC LIBB: 37 tabelle, **76 righe** di coordinamento; editor: 16 accordi, 60 clausole |
| `/vsop/health/ready` | `Healthy` |

⚠️ **`/vsop/health` dice `Degraded`, e non viene da qui**: sono due piste orfane
(clausole `#38`/`#39`, `ConditionRefId` 215/216, re-importate con altri Id). Verificato sul **backup
pre-conversione**, dove erano identiche. È contenuto, non codice.

⚠️ **Da qui in poi `src/Vipi.Host/vipi.db` gira solo con questo ramo**: `main` cerca ancora
`AgreementParties`. E il `vipi.db` **non è tracciato in git**, quindi il backup fuori dal repo è l'unica copia
dello stato precedente.

⚠️ **Il percorso del DB va ASSOLUTO** in `dotnet ef database update --connection`: il relativo si risolve dalla
cartella del progetto di startup e dà `SQLite Error 14: unable to open database file`. Fallisce senza toccare
niente, ma fa perdere un giro.

