# Editor accordi: per relazione, e con i due versi sempre a vista — carta ed esito (17 agosto 2026) 🟢

> Secondo giro sull'editor, **a valle** del modello nuovo: [`2026-08-16-accordi-di-coordinamento.md`](2026-08-16-accordi-di-coordinamento.md).
> Ramo di partenza `feature/accordi-coordinamento` (non ancora in `main`).
> Area: [`../refactor/07-trasferimenti.md`](../refactor/07-trasferimenti.md) §10 · Voce: `lavori-aperti.md` E6-bis.
> Ripartenza da freddo: [`../history/handoff-accordi-coordinamento.md`](../history/handoff-accordi-coordinamento.md).

## 1. Perché — il modello è cambiato, la pagina no

Il 16-17 agosto lo **storage** è passato da «flusso di un settore» ad **accordo fra due parti a due versi**.
L'editor è stato riadattato, ma ha conservato due assi del modello vecchio, e si vedono appena si apre
`/vsop/admin/trasferimenti?acc=LIBB`:

1. l'albero è indicizzato sul **lato B** dell'accordo, non su «l'altro capo rispetto alla ACC che sto guardando»;
2. il verso è un **interruttore**: si vede una tabella per volta, e l'altra va cercata con un clic.

Non è un giudizio estetico. Ecco le misure, prese sul `vipi.db` vero (41 accordi · 80 parti · 63 clausole):

| # | Misura | Numero |
|---|---|---|
| M1 | Accordi che riguardano LIBB in cui **LIBB sta solo sul lato B** | **13 su 40** |
| M2 | Lo stesso, per LIRR | **10 su 11** |
| M3 | Rami dell'albero per LIBB, oggi (chiave = lato B) | **17**, il più grosso è `LIBB_ES_CTR` con 11 |
| M4 | Accordi **bilaterali** in archivio | **0** — le 63 clausole sono tutte `AtoB` |
| M5 | Coppie di accordi che sono i **due versi della stessa relazione** (stessi enti, stesso tipo, stessi aeroporti) | **3**: `#13/#32` LGGG · `#17/#28` LDZO · `#23/#38` LAAA |
| M6 | Gruppi di accordi che scrivono la **stessa relazione** (stessi enti, tipo **e aeroporti**) | **1**: `#26/#27` |
| M7 | Clausole per `(accordo, verso)` | min 1 · media 1,6 · **max 6** |
| M8 | Lati con più di un ente · aeroporti per accordo | 0 · max 2 |

**M1/M2/M3 dicono la stessa cosa.** L'albero si chiama «controparte» ma mostra il lato B, e per un accordo in
cui la ACC scelta *è* il lato B il ramo prende il nome dei **nostri stessi settori**. Per Roma la pagina è
quasi interamente così: dieci accordi su undici finiscono sotto rami che si chiamano `LIRR_US_CTR`,
`LIRR_NC_CTR`, `LIRR_TS_CTR`. Chi apre la pagina non trova «l'accordo con Zagabria»: trova un elenco dei
propri settori, cioè esattamente l'asse del modello vecchio, sopravvissuto alla sua sostituzione.

**M4 è la ragione per cui il selettore del verso non paga.** In archivio non esiste **nessun** accordo
bilaterale: il tasto del verso opposto porta sempre a una tabella vuota. E il reciproco non si scrive proprio
perché non si vede che manca — un dato assente dietro un clic è un dato che nessuno scopre.

**M5 è il finale della storia.** Il travaso i due versi li ha lasciati in **accordi separati** (a ragione: non
poteva scegliere quale valesse). Con i versi a vista, tre accordi mostrerebbero un verso pieno e uno vuoto
mentre il loro reciproco vive nel nodo accanto. Le «due asimmetrie da decidere» rimaste aperte sono la punta
di questo, non un caso a sé.

**M7/M8 dicono che costa poco:** sei righe è la tabella più grande che esiste, quindi due tabelle nello stesso
riquadro ci stanno senza inventare niente.

## 2. Le due richieste del committente, tradotte

> **R1** — «Devo lavorare per accordi, non per settori: seleziono LIBB e vedo tutti gli accordi che
> coinvolgono almeno un settore di LIBB.»

Il **dato** lo fa già: `IAgreementRepository.ListByAccAsync` seleziona `OwnerAcc == acc || Parties.Any(p =>
p.Sector.Acc == acc)`, e le guardie di scrittura (`AgreementsOf`/`ClausesOf`) usano la stessa regola — quindi
un accordo si modifica **da entrambi i capi**, e questo è già verificato. Quel che manca è
l'**orientamento della vista**: la pagina non sa dire «noi» e «loro» rispetto alla ACC scelta, e finché non lo
sa il navigatore continua a indicizzare sul lato B. R1 è quindi lavoro di **vista**, non di modello: nessuna
migrazione, nessun campo nuovo.

> **R2** — «Ogni accordo deve sempre mostrare entrambi i casi (A→B e B→A) per facilitarne l'editing.»

Due tabelle una sotto l'altra, che è la forma dei documenti veri: EUROCONTROL Annex D.2 ne ha **due**
(D.2.1 `unit1→unit2`, D.2.2 `unit2→unit1`), e Annex E.3 mette perfino **una colonna per verso**. La carta del
16 agosto lo aveva già scritto — «due tabelle e non una sola con due metà» — ma le aveva messe dietro un
interruttore. Qui l'interruttore cade.

## 3. Il disegno

### 3.1 «Noi» e «loro» si calcolano, A e B non si toccano

Una funzione sola decide l'orientamento di un accordo rispetto alla ACC aperta:

```
NearSide(acc, agreement) =
    A  se un ente del lato A appartiene alla ACC
    B  se solo il lato B le appartiene
    A  se entrambi i lati le appartengono (accordo interno)  → e la testata lo dice
```

Da lì scendono tutte le etichette: `Near` = noi, `Far` = controparte, il verso «noi → loro» è `AtoB` quando
`Near == A` e `BtoA` quando `Near == B`.

⚠️ **Lo storage resta A/B, e non si riscrive nulla per «raddrizzare» un accordo.** Scambiare i lati in
archivio cambierebbe di significato le clausole di **entrambi** i versi e le release congelate; e soprattutto
un accordo di confine non ha un verso «giusto» — dipende da chi lo apre. L'orientamento è una lente, non un dato.

Gli accordi interni alla ACC (`LIBB_ES_CTR ↔ LIBD_CS0_APP`: tre in archivio) non hanno un «loro»: la
convenzione è `Near = A`, con la testata che li marca come interni. Convenzione dichiarata, non implicita.

### 3.2 L'albero: ACC controparte ▸ relazione ▸ accordo

| | Prima | Adesso |
|---|---|---|
| Livello 1 | lato B, callsign concatenati | **ACC della controparte**, letta dalla lente |
| Livello 2 | — | la **relazione**: `noi ⇄ loro`, la coppia di enti |
| Foglia | tipo + aeroporti | tipo + **gruppo di aeroporti** + i due conteggi (`3 ⇄ 0`) |
| Rami per LIBB | 17 | **7** (`LAAA` 6 · `LDZO` 7 · `LGGG` 9 · `LIRR` 10 · `LYBA` 4 · `LIBB` interni 4 · «senza ricevente» 1) |

Il primo livello è l'**ACC controparte**, perché «l'accordo con Roma» è il modo in cui un accordo viene in mente.

⚠️ **Il secondo livello è la coppia, non il solo ente lontano — e la prima stesura sbagliava.** L'identità di un
accordo nel modello è **(le due parti · il tipo di traffico · il gruppo di aeroporti)**: è la chiave con cui
`AgreementMerge.SplitRelations` decide che due accordi dicono la stessa cosa, e la tripla che la proposta di
fusione pretende identica prima di offrire il comando. Indicizzare sul solo lato lontano usava **mezza** di
quella chiave, e su due casi mentiva:

- sugli accordi **interni** «l'ente lontano» è un *nostro* settore, e compariva come se fosse la controparte —
  è il difetto che il committente ha visto per primo, sotto «LIBB interni»;
- sotto una ACC, due relazioni con capi nostri **diversi** finivano nello stesso mucchio: in archivio
  `LIBB_ES_CTR ⇄ LDZO_CTR` e `LIBD_CS0_APP ⇄ LDZO_CTR` stavano sotto l'unica intestazione `LDZO_CTR`, che non
  diceva che una delle due parte dal nostro **avvicinamento** e non dall'area.

Sotto la coppia le foglie si distinguono per **tipo e gruppo di aeroporti**, cioè per ciò che resta della
tripla — e due foglie identiche di fila **sono** la «relazione spezzata» del cruscotto, visibile senza aprirlo.

⚠️ **Callsign interi, non `ES ⇄ CS0`.** Abbreviare sarebbe un secondo modo di nominare gli enti in una colonna
che già li scrive per esteso, e due notazioni sono due verità. Se lo spazio stringe cede il **nostro** capo —
è lo stesso su quasi tutte le relazioni di una ACC — mentre la controparte, che è il nome che si sta cercando,
non si taglia mai; il titolo porta comunque la coppia piena. Misurato a 1800 px: nessun taglio.

Il ramo «senza ricevente» e quello «interni» restano in fondo: sono elenchi brevi e non sono un confine con cui
si lavora. ⚠️ Nella relazione senza controparte l'altro capo si legge **UNICOM**, non «— senza ricevente»: al
livello dell'ACC quello è il *nome del ramo*, ma in una coppia serve l'altro capo — e l'altro capo, quando non
c'è nessuno, è UNICOM. Reso in maiuscoletto «— SENZA RICEVENTE» urlava dentro una riga che deve leggersi come
due callsign.

### 3.3 Il riquadro di lavoro: due tabelle, sempre

```
┌─ LIBB_ES_CTR  ⇄  LGGG_W_CTR   [sorvoli]  [LIBD·LIBR]        ✎ modifica   ✕ elimina
│
│  ▾  noi → loro     LIBB_ES_CTR → LGGG_W_CTR            3 clausole   + clausola  ⎘ incolla
│     ┌──────────┬─────────┬───────────┐
│     │ punti    │ livello │ condizione│      … tabella di sempre, gesti di sempre
│
│  ▾  loro → noi     LGGG_W_CTR → LIBB_ES_CTR            0 clausole   + clausola  ⎘ incolla  ⇄ copia dall'altro verso
│     (vuoto — «il reciproco non è ancora scritto»)
└─
```

Cinque scelte, ognuna con la sua ragione:

1. **I tasti stanno in testa al blocco, non a piede.** Il piede-che-tiene-i-tasti è la misura che è costata
   sei giri ad agosto, e vale per **un** corpo che scorre; con due blocchi il piede del primo finirebbe a metà
   pagina, che è peggio del problema che risolveva. Il piede del riquadro resta, ma per ciò che è
   dell'accordo intero (incolla, avvisi), non del singolo verso.
2. **Ogni blocco è richiudibile** e ricorda lo stato — ma nasce **aperto anche se vuoto**: un verso vuoto è
   l'informazione, e nasconderlo rifarebbe il difetto dell'interruttore.
3. **Le intestazioni dicono gli enti, non «A» e «B»**: `LIBB_ES_CTR → LGGG_W_CTR`. «A→B» è il nome della
   colonna nel database, non una cosa che un controllore legga.
4. **«⇄ copia dall'altro verso» resta un punto di partenza**, come è già oggi, e compare solo quando un verso
   è vuoto e l'altro no. I livelli dei due versi sono quasi sempre diversi: indovinarli scriverebbe un accordo
   che nessuno ha concordato.
5. **Il pannello della clausola (colonna 3) non cambia.** Sa già il verso dalla clausola che apre; per una
   clausola nuova lo prende dal blocco in cui è stato premuto «+».

### 3.4 `_direction` da stato a parametro

Oggi `_direction` è **stato di pagina** e sette operazioni lo leggono implicitamente (aggiunta clausola,
incolla, copia verso, riordino, calcolo dei gruppi di varianti, ordinamento, chiave URL). Con due tabelle a
vista quello stato diventa una bugia: la pagina mostra due versi e la variabile ne nomina uno.

Il verso diventa **parametro esplicito** delle operazioni che lo richiedono. Non è pulizia estetica: è la
differenza fra «+ clausola» che aggiunge dove ho premuto e «+ clausola» che aggiunge dove la pagina si
ricorda di essere. Resta un solo uso legittimo di stato — il verso dell'**ultima** cosa toccata, che serve al
pannello e al ripristino dal link.

⚠️ La chiave URL `verso` esiste e i link salvati la portano: va accettata in lettura (seleziona quale blocco
è a fuoco) e smette di essere scritta.

### 3.5 La vista a elenco resta com'è

In elenco le colonne sono mittente/ricevente **reali** e vanno lasciate così: lì si attraversano gli accordi
di dieci controparti diverse, e «noi/loro» sarebbe un orientamento senza un soggetto.

⚠️ **La colonna «verso» era prevista e non si fa.** Guardando `XferRowsTable` si vede che mittente e ricevente
sono già calcolati *sulla direzione della clausola* (`SenderLabel`/`ReceiverLabel` leggono `c.Direction`):
il verso in elenco **si legge già**, e una colonna in più direbbe la stessa cosa una seconda volta.

## 4. Le altre idee, con il loro costo

### I1 — «Adotta come verso opposto»: unire le tre coppie · **da fare, con conferma**

I tre accordi di M5 sono, senza ambiguità, i due versi della stessa relazione: stessa coppia di enti, stesso
tipo di traffico, stessi aeroporti, versi opposti. Un comando che porta le clausole dell'uno **nel verso
libero** dell'altro e cancella il guscio rimasto vuoto chiude l'ultima voce aperta del ramo — e lo fa
mostrando le due tabelle *prima* di scrivere, che è il modo in cui i colleghi possono decidere.

- Il candidato si propone **solo** quando i tre attributi coincidono e i versi sono opposti: 3 casi su 41.
  Le altre cinque relazioni «a versi opposti» hanno **aeroporti diversi** (arrivi per gruppo di scali) e non
  sono lo stesso accordo — proporle sarebbe insegnare a ignorare la proposta.
- È **irreversibile** (l'accordo assorbito sparisce) → conferma in linea con l'anteprima delle due tabelle,
  e l'annulla che questa pagina ha già.
- Riusa `AgreementGaps`: il confronto per coppia-di-enti-senza-verso è già scritto lì.

### I2 — Il tipo di traffico di un accordo bilaterale · **serve una decisione**

`TrafficKind` sta sull'**accordo**, non sul verso. Finché i bilaterali sono zero (M4) non si vede; con due
tabelle a vista si vede subito: un accordo ACC↔APP di tipo *Arrivi* mostrerà un blocco «loro → noi» che
di arrivi non parla — da un APP verso l'ACC salgono **partenze**.

Tre strade:

| | Cosa | Costo | Rischio |
|---|---|---|---|
| **a** | Il tipo resta dell'accordo; il blocco opposto mostra il **reciproco calcolato** (Arrivi↔Partenze, Sorvoli↔Sorvoli, VFR↔VFR) | vista sola | il reciproco calcolato è una convenzione: se qualcuno scrive due versi che sono entrambi «sorvoli con condizioni diverse» va bene, se scrive un caso strano no |
| **b** | Tipo **per verso** (colonna additiva, nullable = «come l'accordo») | schema × 2 provider, migrazione, snapshot | apre un asse che i documenti veri non chiedono in modo evidente |
| **c** | Niente: il tipo è dell'accordo e basta | zero | l'etichetta mente su metà dei bilaterali futuri |

**Raccomandazione: (a) adesso.** Sorvoli e VFR sono simmetrici, e i tre casi di M5 sono tutti sorvoli — cioè
il reciproco calcolato è esatto su tutto ciò che esiste. (b) resta nel registro delle lacune, dove sta già la
sua famiglia (L1-L9), e si fa se e quando qualcuno scriverà un verso di tipo diverso.

### I3 — La stessa relazione scritta in più accordi: voce del cruscotto · **fatta**

⚠️ **Il numero di M6 era sbagliato in prima stesura, e la misura l'ha corretto.** Contando anche gli aeroporti
il gruppo è **uno solo**, non sei: `#26/#27`, `LIBB_ES_CTR → LIBD_CS0_APP`, arrivi su LIBD, tre clausole
ciascuno. Gli altri cinque «gruppi» venivano da una chiave che ignorava gli aeroporti — arrivi su scali diversi
fra gli stessi enti sono legittimamente due accordi, ed è lo stesso errore che avrebbe reso rumorosa I1.

E non sono **doppioni**: guardandone le clausole, `#26` porta EKMUR/PISIP/BIRSU e `#27` porta TOPNO. Sono un
accordo solo spezzato per **gruppo di punti** — la frammentazione che il modello nuovo permette di chiudere.
Quindi la voce **segnala e non offre nessun comando**: unirli è giusto, ma nel documento due tabelle diventano
una, e quella è una decisione editoriale, non un calcolo.

### I4 — La lacuna punta al verso · **caduta, e la ragione conta**

Era «costo nullo», ma con i due versi **sempre aperti** il beneficio è nullo anche lui: aprire l'accordo mostra
già entrambe le tabelle, alte al massimo sei righe (M7). Un campo `Direction?` in più su `AgreementGap` sarebbe
un dato che nessuno legge — e un campo che non cambia niente è debito, non completezza.

`AgreementGap` ha invece guadagnato un secondo **id**: serve a I1, dove la lacuna riguarda *due* accordi e senza
quello la voce potrebbe solo indicare, non offrire di sistemare.

### I5 — Il confronto dei punti fra i due versi · **da valutare dopo la 3.3**

Quando i due versi ci sono entrambi, la domanda vera è «gli stessi punti?» — è la domanda che ha prodotto le
due asimmetrie note. Con le tabelle affiancate la si legge a occhio nei casi piccoli (max 6 righe, M7); per i
casi grandi basterebbe una riga sopra i blocchi con i punti presenti **da un lato solo**. `AgreementGaps` lo
calcola già. Da decidere **dopo** aver visto le due tabelle a schermo: se l'occhio basta, l'avviso è rumore.

### I6 — Dove atterrerà «riceve da» (L8)

Il registro delle lacune ha `L8`: le frasi rendono il traffico entrante con la voce di chi trasferisce. Il
blocco «loro → noi» è il posto dove quella differenza si vedrà per la prima volta a schermo. Non si fa qui —
la frase vive nel template, non nella vista — ma è utile sapere che questo giro prepara il suo ingresso.

## 5. Cosa questo giro NON fa

- **Nessuna migrazione, nessun campo nuovo** (con I2 = (a)). Se si scegliesse I2(b), lo schema va emesso
  **due volte** e le release congelate vanno lasciate rileggibili — `AppCoordRow` solo additivo.
- **Non si tocca `AgreementExpansion`, la derivazione, le frasi, la vista live, la stampa, il matcher Aurora.**
  La rete di caratterizzazione (`CoordinationCharacterizationTests` + `real-flows.tsv`) resta l'invariante:
  finché è verde, i cinque consumatori a valle non possono essersi rotti.
- **Non si riscrive il pannello della clausola** né la tabella delle righe: `XferRowsTable` viene istanziata
  due volte con parametri diversi, non duplicata.
- **Non si raddrizzano gli accordi in archivio** scambiando A e B.

## 6. Pre-flight (`../FEATURE-PROCESS.md`)

**1. Modello — aggiungo un concetto o ne esiste già uno?**
Nessuna entità nuova. `NearSide`/`FarSide` sono una **lente** sull'accordo, non un secondo modello: una
funzione pura in `Application` (dove stanno già `AgreementSuggestions`, `ClausePaste`, `AgreementGaps`), così
che l'orientamento si possa provare e smentire senza un database. Se fra sei mesi qualcuno cerca «dove si
salva un accordo» trova un posto solo, come oggi.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?**
No. `AgreementDirection` ha due valori e con questo giro smette di essere **stato** per diventare parametro:
gli `if` sul verso diminuiscono, non aumentano. Il reciproco di I2(a) è **uno** `switch` su `TrafficKind`, in
un posto solo (`XferLabels`), accanto a quello che già rende l'etichetta.

**3. Ingressi + verifica — come ci arriva l'utente e come lo verifico?**
Rotta invariata `/vsop/admin/trasferimenti?acc=LIBB`. Nessun catch-22: «+ accordo» resta fuori dal corpo che
scorre, e l'albero vuoto non blocca la creazione del primo. Verifica: bUnit su navigatore e orientamento
(oggi non c'è nessun test sui componenti `Xfer*` — è la prima rete di questa famiglia), più guida live sulla
copia del `vipi.db` reale, su **LIRR** prima che su LIBB, perché è lì che l'orientamento sbagliato si vede su
dieci accordi su undici.

**4. Propagazione — rimuove o rinomina qualcosa?**
Sì, poco ma va fatto nello stesso giro: la chiave URL `verso` cambia ruolo; il selettore del verso sparisce
(guida in-app `#accordi` + `GuideSearchCatalog`); l'albero cambia asse, e i commenti di `XferNavigator` e di
`AdminTrasferimentiPage` descrivono l'asse vecchio («l'albero è per controparte» era già una promessa
mantenuta a metà). Chiavi `.resx` **it + en insieme** — 14 chiavi duplicate hanno già rotto la CI l'11 agosto,
e `SharedResourceIntegrityTests` è la guardia.

## 7. I passi

| Fase | Cosa | Verifica |
|---|---|---|
| 1 | `AgreementOrientation` puro (near/far/verso relativo) + test | unit su LIBB lato A, LIBB lato B, accordo interno, accordo senza lato B |
| 2 | Albero a due livelli sull'orientamento; nessuna scrittura toccata | bUnit su `XferNavigator`; a schermo su LIRR: 10 accordi non più sotto i propri settori |
| 3 | Due blocchi nel riquadro; verso da stato a parametro; URL retro-compatibile | a schermo: `#13` mostra 3 ⇄ 0, «+ clausola» del blocco vuoto scrive nel verso giusto |
| 4 | I1 «adotta come verso opposto» + voce cruscotto (3 candidati) | a schermo su `#13/#32`: fusione, poi vIPI ACC LIBB **identica** riga per riga |
| 5 | I3 duplicati nello stesso verso nel cruscotto (6 gruppi) + I4 | il cruscotto passa da 24 a ~30 voci, ordine di gravità invariato |
| 6 | Propagazione (guida, `.resx` it+en, doc di area, memoria) + `Release --no-incremental` | 0 avvisi su due TFM, suite ≥ 2485 |

Un commit per fase, build verde a ogni commit.

## 8. Trappole da non riscoprire

1. **L'altezza si misura, in CSS non è esprimibile** (memoria `trasferimenti-acc-app-carta`): due blocchi nello
   stesso riquadro scorrono **dentro** il corpo, e i tasti di ciascuno stanno in **testa** al proprio blocco.
2. **`localStorage` si legge al primo render**, mai in `OnInitializedAsync` — vale per lo stato di apertura
   dei due blocchi come vale per le anteprime.
3. **Attributo componente `string` senza `@` è un letterale**: `Direction="AtoB"` non è `Direction="@d"`, e
   `XferRowsTable` verrà istanziata due volte proprio con quel parametro.
4. **Il trascinamento fra versi diversi è già bloccato** nel repository (`MoveClauseToAsync` esce se
   `c.Direction != target.Direction`) — verificato, non va rifatto in pagina; ma con due tabelle il gesto
   diventa **possibile da provare**, quindi va dato un ritorno visivo invece di un no silenzioso.
5. **Il vincolo snapshot**: `AccCoordination`/`AppCoordination` sono JSON dentro le release congelate,
   `AppCoordRow` solo additivo.
6. **Gli avvisi sono errori** e `dotnet test` non lo sa: il cancello è
   `dotnet build Vipi.slnx -c Release --no-incremental`. Fermare `Vipi.Host` prima, o è `MSB3021`.
7. **La fusione di I1 non è invertibile** riga per riga, come non lo era il travaso. L'annulla della pagina
   copre la sessione; il backup del DB sta fuori dal repo (`../../vipi.db.bak-pre-travaso-20260817`).

## 9. Decisioni del committente (17 agosto 2026)

1. **I2 = (a)** — il tipo resta dell'accordo e il blocco opposto mostra il **reciproco calcolato**
   (Arrivi↔Partenze, Sorvoli↔Sorvoli, VFR↔VFR, Altro↔Altro), marcato come calcolato e non come dato. Nessuna
   migrazione. Il tipo per verso resta nel registro delle lacune, accanto a L1-L9, e si fa se qualcuno scriverà
   davvero un verso di tipo diverso.
2. **I1 = sì, con anteprima** — il comando «adotta come verso opposto» si fa, mostra le due tabelle **prima**
   di scrivere e chiede conferma in linea. Si propone solo dove enti, tipo e aeroporti coincidono e i versi
   sono opposti: tre casi su quarantuno.
3. **I5 = subito** — la riga «solo da un lato» sopra i due blocchi, con i punti presenti in un verso e non
   nell'altro. Il calcolo è quello che `AgreementGaps` fa già per la voce dell'asimmetria: **una sola
   funzione**, letta in due posti, non due conti che possono divergere.

## 10. Esito, verificato a schermo (17 agosto 2026)

Guidato con la skill `verifica-live` su una **copia** del `vipi.db` reale (il DB del progetto è rimasto a 41
accordi: controllato dopo). Sei pagine, due giri, zero errori di console e zero risposte ≥ 400.

| Cosa | Atteso | Visto |
|---|---|---|
| Albero **LIRR** | non più i propri settori come controparti | `LIBB (10)` · `— senza ricevente (1)`. Prima erano dieci rami chiamati `LIRR_US_CTR`, `LIRR_TS_CTR`, … |
| Albero **LIBB** | 7 rami | `LAAA 6 · LDZO 7 · LGGG 9 · LIRR 10 · LYBA 4 · LIBB «interni» 3 · «— senza ricevente» 1` = 40 |
| Accordo **#13** | due blocchi, `3 ⇄ 0` | `NOI → LORO` 3 clausole · `LORO → NOI` 0 con «il reciproco non è ancora scritto» |
| Tipo reciproco (**#26**, ACC→APP arrivi) | «Partenze» marcato calcolato | `Partenze *` nel blocco entrante, con il perché nel tooltip |
| Cruscotto lacune | 24 → 28 voci, due generi nuovi | **28**: 3 «reciproco a parte» + 1 «relazione spezzata» accanto alle precedenti |
| **Fusione** #13 ⇄ #32 | un accordo, `3 ⇄ 4` | testata `LIBB_ES_CTR ⇄ LGGG_W_CTR`, LGGG da 9 a 8, totale da 40 a 39, toast «Uniti: 4 clausole spostate nel verso opposto» |
| Punti spaiati (I5) | compaiono **dopo** la fusione | «⚠ Punti presenti in un verso solo: **BELIX, OLGAT**» — l'asimmetria nota, finalmente dentro il riquadro dove si scrive |

Le frasi del verso entrante si compongono da sé e sono giuste: «Athinai Radar West trasferisce a Brindisi
Radar ES il traffico stabile per un livello pari su OLGAT». È la prova che la proiezione non è stata toccata.

### Due difetti trovati **guardando**, non dai test

1. ⚠️ **La conferma della fusione diceva «Sì, elimina».** `InlineConfirm.ConfirmLabel` ha per default
   `"Sì, elimina"` — giusto per il novanta per cento dei suoi usi, sbagliato qui: nessuno elimina niente, si
   unisce. Corretto passando l'etichetta (`Xfer_MergeConfirm`). ⚠️ **Quel default è italiano e cablato nel
   componente**: nella pagina inglese *ogni* conferma in linea che non passa l'etichetta dice «Sì, elimina» in
   italiano. Difetto pre-esistente e più largo di quest'area — non toccato qui, ma va saputo.
2. ⚠️ **Stavo misurando la build sbagliata.** Il primo giro mostrava 27 lacune invece di 28 e nessuna
   «relazione spezzata»: `dotnet build src/Vipi.Ui` non aggiorna la copia di `Vipi.Application.dll` dentro
   `src/Vipi.Host/bin`, e `dotnet run --no-build` parte da lì. È la stessa trappola già pagata a luglio con le
   DLL bloccate: **prima di credere a ciò che si vede, controllare l'ora del `.dll` dentro `bin` dell'host.**

### Difetti pre-esistenti che i due versi rendono più visibili (non toccati)

- **`— (dispari)`**: `LevelFormatting.Format` appende la parità anche a un livello **assente**. Già noto (handoff
  del 15 agosto), ma prima si vedeva in una tabella per volta; adesso in due.
- **`Pari (Nord) - Dispari (Sud) (dispari)`**: stessa famiglia — la parità si appende anche a un livello
  *speciale* che la dice già a parole.
- **L10**, la parità non tradotta in tabella (`FL150- (pari)` nella pagina inglese), è confermata a schermo.

Tutti e tre stanno in `LevelFormatting` e sono **congelati nell'approvato** della rete di caratterizzazione:
si correggono in un giro loro, insieme, con la riapprovazione guardata riga per riga.

### Terzo giro, dopo la revisione del committente (17 agosto, sera)

> «Sotto LIBB interni c'è un solo settore; l'idea sarebbe raggrupparle per accordo tipo ES⇄CS0 e sotto l'elenco
> degli aeroporti.»

Aveva ragione, e la ragione era **nel modello**, non nell'estetica: il livello 2 usava mezza chiave d'identità.
Rifatto come descritto in §3.2 e riverificato a schermo (`report3.json`, `rel-libb.png`, `rel-lirr.png`):

| Cosa | Visto |
|---|---|
| LIBB interni | due relazioni leggibili — `LIBD_CS0_APP ⇄ LIBB_ES_CTR` e `LIBB_ES_CTR ⇄ LIBD_CS0_APP` — dove prima c'era un `LIBB_ES_CTR` solitario che si spacciava per controparte |
| Relazione spezzata | sotto `LIBB_ES_CTR ⇄ LIBD_CS0_APP` due foglie **`Arrivi LIBD 3 ⇄ 0`** identiche di fila: `#26/#27`, a occhio |
| LDZO | `LIBB_ES_CTR ⇄ LDZO_CTR` **e** `LIBD_CS0_APP ⇄ LDZO_CTR`, prima confuse sotto l'unica intestazione `LDZO_CTR` |
| LIRR | sei relazioni, tutte col **nostro** capo per primo (`LICA_ES0_APP ⇄ LIBB_ES_CTR`, …) |
| Larghezza | nessun callsign tagliato dall'ellissi a 1800 px (misurato con `scrollWidth > clientWidth`) |

Difetto trovato guardando e corretto subito: la relazione senza controparte leggeva **`— SENZA RICEVENTE`** in
maiuscoletto. Ora legge `UNICOM`, che è dove il traffico finisce davvero — vedi §3.2.

⚠️ Trappola ripagata: il primo tentativo di ricompilare l'host è morto con **MSB3021** perché il *mio* host di
verifica teneva i DLL. Fermare prima, compilare dopo. E la verifica è girata su **porta 5035**: il committente ha
il suo host sulla 5034, e prendergli la porta gli avrebbe rotto la pagina sotto le mani.

### Quarto giro: due capi obbligatori, e gli aeroporti sull'accordo (18 agosto)

> «Non deve essere possibile creare relazioni senza controparte. E quando creo un accordo devo indicare solo le
> due controparti, poi seleziono l'accordo e aggiungo un aeroporto o un gruppo di aeroporti.»

**Nel modello non cambia niente**: lo schema regge già 0..n parti per lato e 0..n aeroporti, e passare a «1..n» è
una **regola**, non una tabella. Nessuna migrazione.

#### Il lato B diventa obbligatorio, e il motivo non è di gusto

Un accordo senza lato B **non produce niente**: `CoordinationDerivation` scarta la riga — è la policy che la rete
di caratterizzazione ha già fotografato (delle 78 righe vere ne derivano 77, la riga GISAM di Zagabria non ha
ricevente). E «a UNICOM» **non è un capo che si scrive**: è ciò che `TransferOnlineResolver` calcola a runtime
quando il ricevente è offline, risalendo la gerarchia. L'etichetta `(vuoto = UNICOM)` sul picker insegnava il
contrario, e ora non c'è più.

- Regola in `ValidateAgreement`: **entrambi** i lati con almeno un ente.
- ⚠️ **Il ripristino ne è fuori di proposito.** In archivio due righe la violano — `#18` (`LIBB_ES_CTR`, sorvolo
  Zagabria, 1 clausola) e `#41` (`LIRR_NE_CTR`, vuota) — e un annulla che rifiutasse di rimettere l'accordo appena
  cancellato sarebbe peggio della regola. `RestoreAgreementAsync` non passa dalla validazione, e un test lo
  fissa perché nessuno lo «sistemi» per simmetria.
- La voce «senza ricevente» del cruscotto resta e cambia mestiere: da difetto che può ricomparire a **rilevatore
  di eredità**. Il percorso di riparazione è aprire l'accordo — verificato a schermo su `#18`: il salvataggio
  resta bloccato con «Seleziona l'ente che riceve per abilitare».

#### Gli aeroporti: dove servono nel form, sempre sull'accordo

**Decisione del committente: la regola dura resta.** Arrivi e partenze continuano a pretendere un aeroporto, e
quindi il form di creazione lo chiede ancora — ma **solo dove serve**: per sorvoli, VFR e «altro» il campo
sparisce, ed erano l'unico posto in cui invitava a scrivere un dato che il modello non vuole.

⚠️ **Il campo resta però visibile se degli aeroporti ci sono già**, anche col tipo cambiato: un campo nascosto
che tiene dati è il modo più rapido di perderli senza accorgersene. Stessa regola per il tasto «+ Aeroporto»
nella testata.

E il pezzo che chiudeva la richiesta: **gli aeroporti si aggiungono e si tolgono dalla testata dell'accordo
scelto**, con chip e picker in linea. Prima l'unico modo era entrare in «✎ Modifica accordo» — un form di sei
campi per toccarne uno — e il gruppo di aeroporti è ciò che si mette a punto più spesso («vale anche per
Brindisi»). La scrittura passa dalla **stessa porta** del form, quindi le regole valgono anche lì: togliere
l'ultimo aeroporto a un accordo di arrivi non passa, e lo dice.

#### La proposta persa, tornata come verifica

Il picker del ricevente propone «l'avvicinamento dell'aeroporto» a partire dall'ICAO. Con la regola dura tenuta
quella proposta **non si perde** alla creazione — l'aeroporto è ancora lì. Ma gli aeroporti aggiunti *dopo* non
la incontrerebbero mai, e allora la stessa conoscenza torna come **controllo**: se lo scalo è coperto da un ente
che non è fra i riceventi, la testata lo dice. Verificato a schermo: aggiungendo `LIBD` all'accordo `#1`
(`LIBB_ES_CTR → LIRR_NC_CTR`) compare **«⚠ LIBD è coperto da LIBD_CS0_APP, che non è fra i riceventi»**.

Vale solo per arrivi e partenze — un sorvolo non consegna a nessuno scalo — e solo dove la gerarchia dichiara un
ente di copertura: senza dato, nessuna affermazione.

#### Verificato a schermo (porta 5035, copia del DB)

| Cosa | Visto |
|---|---|
| Form nuovo accordo, arrivi | `Primo lato *` · `Secondo lato *` · `Tipo` · `Aeroporti` · `Descrizione` |
| Form nuovo accordo, sorvoli | il campo aeroporti **non c'è** |
| Tasto «+ Accordo» | disabilitato, col suggerimento che dice **quale** capo manca |
| Testata di `#26` | chip `LIBD ✕` più `+ Aeroporto` |
| Aggiunta di `LIBR` dalla testata | chip `LIBD ✕` `LIBR ✕`, salvata dalla porta di scrittura |
| Avviso di copertura | «⚠ LIBD è coperto da LIBD_CS0_APP, che non è fra i riceventi» |
| `#18`, eredità senza ricevente | pill «⚠ nessun ricevente», salvataggio bloccato col suggerimento |

Difetto trovato guardando: dopo l'aggiunta **il picker restava aperto**, e a campo vuoto la tendina proponeva
l'intero catalogo — cinquanta scali stesi sulla testata. Ora si chiude: aggiungerne due di fila è raro quanto
riaprirlo è economico.
