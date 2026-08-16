# Accordi di coordinamento — carta (16 agosto 2026) 🟡

> Sostituisce `TransferFlow` + `TransferPoint` con un **accordo** fra due parti, a due direzioni.
> Area: [`../refactor/07-trasferimenti.md`](../refactor/07-trasferimenti.md) §10. Voce: `lavori-aperti.md` E6.
> Ramo `feature/accordi-coordinamento`.

## 1. Perché

L'unità di scrittura di oggi è `TransferFlow(settore proprio · tipo · UN aeroporto)` + `TransferPoint(UN CoP ·
livello · UN ricevente)`. **Non è l'unità di nessun documento reale**, e nemmeno quella del dato in archivio.

### 1.1 I documenti veri

Letti in `RealDOCS/`: **LoA ACC Roma ↔ Marseille ACC** (28.01.2021, rev. 25.03.2021), **IPI ACC Roma** 02/22
(sezioni COO2/COO3), più il **Common Format Letter of Agreement** EUROCONTROL ed. 6. La tabella canonica è
sempre la stessa forma:

| Fonte | Forma |
|---|---|
| EUROCONTROL Annex D.2 | `ATS-Route │ COP │ Level Allocation │ Special Conditions`, **una tabella per direzione** (D.2.1 unit1→unit2, D.2.2 unit2→unit1) |
| EUROCONTROL Annex E.3 | `ATS-Route │ Transfer of Control Point │ Transfer of Communications` — e le comunicazioni hanno **una colonna per direzione** |
| LoA Roma–Marseille D.3.2.5 | `Route │ COP │ Dest LIEE-LIED │ Dest LIEA │ Dest LIEO │ Warning`, con note `(1)…(6)` |
| IPI Roma COO 2.2.1.1 | «TS EXE trasferisce a US1 EXE il traffico secondo la seguente tabella:» → `RWY06` → `STAR │ FL │ DESCRIZIONE` |

Da cui: **un accordo, fra due enti, per un traffico, con due direzioni, e una tabella di clausole dentro.** Gli
aeroporti sono un **gruppo** («Roma Group», «Dest LIEE–LIED», «LIRF-LIRA-LIRU-LIRE»), i punti sono un **elenco**,
la direzione è la prima partizione.

### 1.2 Il dato vero

`src/Vipi.Host/vipi.db`: **37 flussi, 78 punti**. Il modello costringe a moltiplicare su cinque assi, e si vede:

| # | Asse | Prova nell'archivio |
|---|---|---|
| A1 | **aeroporto** | arrivi LIRF · LIRA · LIRU · LIRE = 4 flussi identici (ASPIR, FL210−, → LIRR_US). La LoA vera scrive quei quattro **su una riga sola** |
| A2 | **punto** | sorvoli LIBB→LGGG = 7 righe che dicono la stessa cosa su 7 CoP |
| A3 | **settore mittente** | un accordo valido per più settori si scrive N volte. Oggi lo si scrive su **uno solo**, e le vIPI degli altri settori restano mute |
| A4 | **direzione** | LIBB→LGGG (7 punti) e LGGG→LIBB (7 punti) sono due scritture scollegate, e **non coincidono**: BELIX di qua, OLGAT di là |
| A5 | **ACC** | i flussi stanno nel *secchio* di una ACC: un centro estero che confina con due ACC italiane va riscritto due volte |

L'unico riuso esistente è la **copia** (clona gruppo · copia righe da · duplica gruppo): produce gemelli che poi
divergono. A4 è quella divergenza, già successa.

## 2. Pre-flight (`../FEATURE-PROCESS.md`)

**1. Modello — aggiungo un concetto o ne esiste già uno?**
Ne **sostituisco** uno. `CoordinationAgreement` + `AgreementParty` + `AgreementAirport` + `AgreementClause`
prendono il posto di `TransferFlow` + `TransferPoint`, che spariscono dallo schema. Nessun modello gemello:
chi cerca «dove si salva un accordo» trova **un** posto.
`TransferFlowRow`/`TransferPointRow` **restano ma cambiano ruolo** — da DTO di lettura dello storage a
**proiezione** dell'accordo, prodotta da `AgreementExpansion`. È lo schema già stabilito per i settori
(cataloghi = fonte unica, `Sector` = proiezione), non un secondo modello.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?**
No. `Direction` è un enum a due valori consumato in **un** punto (l'espansione, che decide chi è mittente e chi
ricevente). Nessun `switch` per-tipo si aggiunge alle viste: `CoordTable` continua a scegliere le colonne **per
presenza di dati**, mai per tipo di ente.

**3. Ingressi + verifica — come ci arriva l'utente e come lo verifico?**
Ingresso invariato: `/vsop/admin/trasferimenti` (la rotta è citata in guida, aiuto in-app e memorie). Nessun
catch-22: il navigatore mostra gli accordi esistenti e il tasto «nuovo accordo» non dipende da nessun elenco
precompilato. Verifica: la rete di §3 più la guida live sulla copia del `vipi.db` (skill `verifica-live`) su
vIPI ACC LIBB, vIPI APP LIBD, vLOA LIBB↔LGGG in inglese, `/vsop/live` e stampa.

**4. Propagazione — rimuove o rinomina qualcosa?**
Sì, ed è la domanda che pesa di più: spariscono due entità e la loro tabella. Nello **stesso giro** vanno
`docs/spec/modello-dati.md` §9.20/-bis/-ter, `docs/refactor/07-trasferimenti.md`, `docs/lavori-aperti.md` E6, la
guida in-app + `GuideSearchCatalog`, e le memorie `trasferimenti-acc-app-carta`, `transfer-condition-model`,
`transfers-overflight-rework`. Il criterio: nessun nome, commento o `<see cref>` deve citare
`TransferFlow`/`TransferPoint` **come storage**.

## 3. La rete — fatta

`tests/Vipi.Application.Tests/CoordinationCharacterizationTests.cs` + `RealCoordinationFixture.cs` +
`Fixtures/real-flows.tsv` · `real-maps.tsv` · `real-coordination.approved.txt`.

I **flussi veri** (37/78, estratti dal `vipi.db` il 16 agosto) vengono derivati con
`CoordinationDerivation.Build` + `BuildAccTree` per due insiemi di enti — **LIBB**, che possiede i flussi e ne
riceve (passo 1 + passo 2), e **Roma**, che non ne possiede nessuno e vede solo ciò che le entra (passo 2 puro) —
e una terza volta col **template inglese**, che è la resa delle vLOA. Il risultato — righe di tabella *e* frasi
composte — è confrontato carattere per carattere con un file approvato.

> **Invariante del lavoro:** questo confronto resta verde. Finché lo è, vIPI ACC, vIPI APP, vLOA, vista live,
> stampa e matcher Aurora non possono essersi rotti, perché leggono tutti queste stesse righe.

Un fixture inventato non avrebbe potuto dire altrettanto: i casi che rompono sono quelli scritti dai colleghi.
Il file approvato **non si riapprova da sé** — a differenza fallita il test scrive un `.received.txt` accanto e
dice dov'è, così la differenza si guarda prima di accettarla.

Due cose che la rete ha già detto, prima ancora di toccare il modello:
- delle 78 righe ne derivano **77**: la riga GISAM (sorvolo Zagabria) non ha ricevente e viene scartata in
  silenzio dalla derivazione. È la policy nota, ora fotografata;
- ⚠️ **difetto pre-esistente, L10 in §5**: nella resa **inglese** la colonna livello esce `FL260 (pari)` —
  `LevelFormatting` non conosce la lingua. Nella frase la parità è tradotta («(even)»), in tabella no. Congelato
  dall'approvato apposta: si corregge in un giro suo, non dentro la sostituzione del modello.

## 4. Il modello target

Parità di campi: in questo giro l'accordo porta **esattamente** i campi di oggi. I campi che i documenti reali
richiedono e che oggi mancano sono §5, progettati ora nel posto dove atterreranno.

**`CoordinationAgreement`** — `Id`, `OwnerAccId` (solo autorizzazione: `EnsureCanEditAccAsync`), `TrafficKind`,
`Order`, `Description?`.

**`AgreementParty`** — `AgreementId`, `Side {A,B}`, `SectorId`, `Order`. Più righe per lato = l'accordo vale per
quei settori (**A3**); il documento reale lo scrive già così («trasferisce a PS EXE / PN EXE»). Lato B vuoto =
rilascio a UNICOM, segnalato come oggi.

**`AgreementAirport`** — `AgreementId`, `Icao`, `Name?` (fuori catalogo), `Order`. Zero righe = sorvolo/VFR/altro
(**A1**).

**`AgreementClause`** — l'ex `TransferPoint`, con tre differenze e nient'altro:
- `Direction {AtoB, BtoA}` → il bilaterale è **un accordo solo** (**A4**). «È bilaterale» non è un flag: è «ha
  clausole nei due versi», quindi non c'è niente da tenere d'accordo;
- `Cops` come **stringa con separatore** (come `ConditionLabel` fa già per le multi-pista) invece di un CoP
  singolo (**A2**), letta da `CopList.Parse/Format` — `ALL`, `ALL to X`, `Y01-Y12`, `TOPNO 3A` restano token
  singoli;
- **perde `NextSectorId`**: il ricevente è l'altro lato dell'accordo, non un campo ripetuto su ogni riga.

Restano identici: livello (`LevelValue/Unit/Constraint/Special/Parity`), `VerticalState`, faccetta trasferimento
(`Handoff*`, `CommsHandoff*`, `Speed*`), condizione a tre dimensioni, **outline varianti**
(`VariantGroup`/`VariantDepth`/`IsGroupWide`), `Order`.

**Scoping per parti, non per secchio** (**A5**): la lettura non è più «gli accordi di questa ACC» ma «gli accordi
che hanno una parte in questa ACC». `OwnerAccId` resta solo per i permessi. Un accordo non può più essere
invisibile a uno dei suoi due capi.

⚠️ **Vincolo snapshot**: `AccFrozenSectionProvider` & co. serializzano `AccCoordination`/`AppCoordination` in JSON
dentro le release congelate. `AppCoordRow` si tocca **solo in modo additivo** — mai rinominare o cambiare tipo a
un campo esistente, o le release vecchie non si rileggono.

## 5. Registro delle lacune — il secondo giro

Trovate **leggendo i documenti veri**, non rinviate per dimenticanza. Ognuna ha già il posto dove atterrerà, così
il secondo giro è additivo e non una seconda chirurgia.

| # | Cosa manca | Prova | Dove andrà |
|---|---|---|---|
| L1 | **Rotta separata dal punto** (+ tipo: rotta ATS · STAR/SID · punto · jolly) | EUROCONTROL D.2 `ATS-Route │ COP`; IPI `STAR │ FL │ DESCRIZIONE`; in archivio `Y01-Y12` e `TOPNO 3A` stanno **già** nel campo CoP | due colonne su `AgreementClause`. Con le **1481 SID già importate** (`AirportSids`), la colonna «DESCRIZIONE» verrebbe gratis |
| L2 | **Release** (climb · descent · turn) | definito in EUROCONTROL Annex A.1.8; nella LoA: «released after GINOX», «released till FL240 when in contact», «Release Descent procedure» | faccetta sulla clausola, gemella di quella di trasferimento. **Non** è il livello di trasferimento: autorizza il ricevente ad agire *prima* |
| L3 | **Modo di coordinamento** (Silent · Approval Request · verbale) | IPI COO 2.1 §3 lo dà come default generale; LoA Annex F è tutto su questo | campo sulla clausola + **default sull'accordo** |
| L4 | **Nota per clausola** | ogni tabella vera ne ha: `(1)…(6)`, `Nota*` | campo testo sulla clausola, resa come nota a piè di tabella |
| L5 | **Default in testa** invece che ripetuti su ogni riga | «transfer of control takes place at the AoR-boundary, *unless otherwise specified*» | default sull'accordo per luogo di trasferimento, comunicazioni e modo; la clausola porta solo lo scostamento. È anche la cura strutturale delle «15 righe da rivedere» |
| L6 | **Condizione come intestazione**, non come cella | IPI: `RWY06 [tabella] · RWY24 [tabella]` | condizione sul **gruppo** di clausole, resa come sotto-titolo. L'outline resta per le *eccezioni*, che è un'altra cosa |
| L7 | **Clausole in prosa** e **spaziatura** | «coordina con DEP PIA il livello di trasferimento»; «provide spacing to converging traffic»; «10 NM tra successivi aeromobili» | clausola senza livello con solo testo + campo spaziatura. Senza, finiscono in un blocco libero **scollegato dalla sezione** |
| L8 | voce «**riceve da**» nelle frasi | «TS1 EXE **riceve da** US1 EXE il traffico in salita per FL180 via VEXUX» | secondo template: oggi l'entrante è reso con la voce del trasferente |
| L9 | **etichetta del gruppo di aeroporti** | «Roma Group», «Dest LIEE–LIED» | campo sull'accordo, accanto all'elenco ICAO |
| L10 | ⚠️ **parità non tradotta in tabella** (difetto pre-esistente) | resa inglese della rete: `FL260 (pari)` accanto a «at level 260 (even)» | `LevelFormatting.Format` non conosce la lingua. Va risolto dove sta la lingua — il template — non con uno `switch` nella vista |

**Chiusa e da non riaprire:** le **configurazioni di settore**. L'IPI Roma ripete interi blocchi di coordinamento
per configurazione (US0+USA · US1+USA · USA assegnato all'altro); su IVAO i settori si aprono e si chiudono, e la
gerarchia di copertura copre già il caso. Scritto qui perché non venga riscoperto come idea nuova.

## 6. Passi

| Fase | Cosa | Stato |
|---|---|---|
| 0 | Carta, registro lacune, **rete di caratterizzazione** sui flussi veri | ✅ |
| 1 | Entità + migrazione nei due provider + convertitore + espansione + porta di scrittura | ✅ |
| 2 | Editor sugli accordi (guscio a tre colonne e gesti conservati) + **scambio**: travaso armato, lettori sugli accordi, vecchia scrittura rimossa | ✅ |
| 3 | Lettura: punti richiusi in una riga, **prosa a scelta della sezione** (distesa ⇄ capofila), colonna «Anche per» | ✅ |
| 4 | Riempimento: riceventi proposti · incolla-tabella · cruscotto lacune | ⬜ |
| 5 | Propagazione doc/spec/memorie e chiusura | ⬜ |

### Esito della lettura (16 agosto, verificato live)

- **I punti si richiudono**: sulla derivazione live otto celle portano ora l'elenco («EKMUR, PISIP»,
  «DINOB, RUTOM, LORNO, BELIX»), e la frase è **una per clausola** invece di una per punto.
- ⚠️ **La richiusura NON si applica ai documenti già pubblicati**, ed è la scoperta che conta: la vista pubblica
  legge lo **snapshot congelato**, catturato prima di questa modifica e senza la provenienza. Le righe senza
  clausola passano intatte apposta — un documento pubblicato non deve cambiare forma perché il codice è andato
  avanti. Si ricompatterà alla prossima release, che è il momento in cui un documento cambia.
- La colonna **«Anche per»** compare solo dove un accordo copre più scali: l'accordo con Tivat la mostra
  («LIBD · LIBR»), quelli su un aeroporto solo no.
- La **prosa a capofila** è un flag per-sezione (`DocumentSection.LeadSentence`), quarto dopo `RenderMode`,
  `IsHidden` e `BeforeParentBody`, con l'interruttore sulla sola sezione «coordinamenti» — offrirlo altrove
  sarebbe un comando che non fa niente.

### Esito dello scambio (16 agosto, verificato live su copia del `vipi.db` reale)

- Il travaso ha prodotto **41 accordi** dai 37 flussi / 78 punti, e al secondo avvio **non è ripartito**.
- `/vsop/admin/trasferimenti` apre **40 accordi / 63 clausole** per LIBB (il quarantunesimo è di LIRR e si vede
  da lì), l'albero è per **controparte**, e l'accordo `LIBB_ES_CTR → LIBD_CS0_APP` mostra i due versi «(3)» e «(0)».
- La sua prima clausola porta i punti **«EKMUR, PISIP»**, e nel documento vIPI ACC quella clausola torna a essere
  **due righe** con le frasi di sempre. È l'invariante vista a schermo, non asserita.
- ⚠️ Le tabelle `TransferFlows`/`TransferPoints` **restano in piedi**: la migrazione che le droppa va in una
  release **successiva**, perché le migrazioni girano prima della manutenzione d'avvio e nella stessa release il
  travaso non troverebbe più niente da leggere.
- Difetti trovati **guardando lo schermo**: una dozzina di testi descriveva ancora il modello vecchio («per ogni
  settore … raggruppati per aeroporto», «Scegli un gruppo», «+ Riga»). Aggiornati in italiano e in inglese
  insieme alla voce della guida.

**Decisioni del committente (16 agosto):** modello sostituito; bilaterale a due direzioni; parità di campi in
questo giro; le due rese di prosa a scelta della sezione; configurazioni chiuse; i tre ausili di riempimento si
fanno.
