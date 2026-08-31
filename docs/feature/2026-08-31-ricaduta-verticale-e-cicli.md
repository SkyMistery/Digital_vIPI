# La ricaduta guarda anche in alto — e un settore non è più nipote di sé stesso

**31 agosto 2026.** Carta di lavoro. Due cose che sembrano diverse e sono la stessa: **l'albero di
copertura**. Prima si chiude il buco che lo rende ciclico (difetto **visto in produzione**, su
`atc.it.ivao.aero`), poi gli si aggiunge la dimensione che gli manca — **la quota**.

---

## Parte 1 — Il ciclo

### Il fatto, dalla produzione

Nella pagina Struttura di `atc.it.ivao.aero` si legge questo:

```
LIMF_WW0_APP          (inherited)   ← riga di RADICE
└─ LIMF_WN0_APP
   ├─ LIMF_WW0_APP    (inherited)   ← lo stesso nodo, nipote di sé stesso
   ├─ LIMA — Aeritalia
   └─ LIMF — Torino Caselle
```

Tre fatti che si incastrano:

1. `LIMF_WN0_APP.ParentCallsign = LIMF_WW0_APP` — **scritto** da un admin.
2. `LIMF.ParentCallsign = LIMF_WN0_APP` — l'aeroporto pende da una **propria** posizione APP.
3. `LIMF_WW0_APP` **non ha padre scritto** (in pagina: `inherited`) → glielo deriva la scaletta.

In `AirportPositionLadder.ParentOf` una posizione APP ha `Rung = 5`: sopra di lei non esiste nessun
gradino (TWR=10, GND=20, DEL=30 sono tutti *sotto*), quindi il ciclo dei gradini non gira nemmeno una
volta e la funzione esce sull'ultima riga — `return airportParent` → **`LIMF_WN0_APP`**.

Albero effettivo: **WW0 → WN0 → WW0**.

### Perché nessuna guardia l'ha fermato

`HierarchyRules.EnsureNoCycle` esiste ed è corretta. Ha **un solo chiamante**
(`EfHierarchyEditingService`), e la mappa che riceve è `InternalNodeParentMapAsync`: **solo i
`ParentCallsign` scritti**. In quella mappa `LIMF_WW0_APP` non ha padre, la catena finisce subito, nessun
ciclo.

Ma tutto il resto del sistema legge `EffectiveParentCallsign = ParentCallsign ?? DerivedParentCallsign`
(`HierarchyNode.cs:26`). **La guardia controlla un albero diverso da quello che leggono tutti.**

E c'è di peggio: la validazione sta **dentro** `if (parentCallsign is not null)`
(`EfHierarchyEditingService.cs:181`). Scegliere «eredita» — cioè scrivere `null` — **non passa da nessun
controllo**. È esattamente il gesto che ha armato il ciclo: in sviluppo `LIMF_WW0_APP` ha ancora il padre
scritto `LIMM_WS2_CTR` e il ciclo non c'è; in produzione qualcuno ha messo «eredita» e il ciclo è comparso.

### Perché non è esploso (ed è la parte peggiore)

Tutti i lettori hanno una guardia sui nodi già visti — `Topology.Ancestors:41`,
`CoverageResolver.FirstOnlineAncestor`, `StrutturaPage.ChainRows/VisibleKeys/FlattenForest`. Nessun blocco,
nessun errore: la catena si **tronca in silenzio** dove l'anello si richiude. La ricaduta si ferma a un
antenato arbitrario e a schermo sembra normale. Un difetto che **si vede solo disegnato**.

### Il doppio disegno

Nella stessa pagina le **radici** si scelgono col padre *scritto* (`StrutturaPage.razor:588`,
`n.ParentCallsign is null`) e i **figli** col padre *effettivo* (`ChildrenOf`, riga 841). Un nodo senza
padre scritto ma con padre derivato è quindi **radice e discendente insieme** — ed è il motivo per cui
`LIMF_WW0_APP` compare due volte. La `guard` di `FlattenForest` (riga 615) taglia la ricorsione al secondo
passaggio: è l'unico motivo per cui la pagina non si pianta.

### Misura sul dato vero

Sonda sul `vipi.db` reale (30 agosto, 320 nodi interni: 153 ACC + 167 posizioni d'aeroporto):

| | |
|---|---|
| cicli **attivi** | **0** (in sviluppo `LIMF_WW0_APP` ha ancora il padre scritto) |
| padri inesistenti | 0 |
| **aeroporti che pendono da una PROPRIA posizione APP** | **19** |

I 19 sono `LIBD LIBG LIBN LIBV LIMF LIMJ LIML LIMP LIPA LIPC LIPE LIPH LIPI LIPL LIPQ LIPS LIPX LIPY LIPZ`.
Non sono un errore — è la configurazione normale: l'aeroporto pende dal suo APP. Ma **ognuno di essi è a un
solo clic di distanza** da un ciclo: basta mettere «eredita» su un'altra APP dello stesso scalo. Non è un
caso isolato da riparare, è **una classe da chiudere**.

### Le quattro mosse

**1. La derivazione non punta più a un proprio discendente.**
`AirportPositionLadder.ParentOf` non restituisce un candidato che, risalendo i padri *scritti* delle
posizioni dello stesso aeroporto, torna alla posizione per cui sta derivando. Se `airportParent` è tale, la
risposta è `null`: **meglio orfana e visibile che ciclica e muta**. È la mossa che chiude il difetto **alla
sorgente**, perché quella funzione è già la porta unica — la chiamano sia l'editor
(`EfHierarchyEditingService:79`) sia la proiezione (`EfSectorProjectionService:292`).

**2. La guardia guarda l'albero vero, e guarda sempre.**
`EnsureNoCycle` riceve la mappa dei padri **effettivi**, e la validazione esce da
`if (parentCallsign is not null)`: si valida anche il ritorno a «eredita». Il controllo è una
**simulazione** — si applica la modifica in memoria, si ricalcola l'albero effettivo di quell'aeroporto e
si cercano gli anelli — perché azzerare un padre scritto cambia anche il padre *derivato* di altri
(`PickOnRung` sceglie la radice guardando i padri scritti del gruppo).

**3. La pagina disegna un albero solo.**
Radici scelte col padre **effettivo**, come i figli.

**4. Se qualcuno ne mette uno lo stesso, si vede.**
Riga `Error` nel report di consistenza: «Gerarchia ciclica — `A → B → A`», area `Dati`, con il percorso
per esteso. Il rilievo entra anche nell'health check, dove i cicli non hanno mai avuto voce.

⚠️ Le mosse 1 e 2 sono **complementari, non alternative**: la 1 impedisce che l'arco derivato chiuda un
anello, la 2 impedisce che lo chiuda un arco scritto. La 4 è la rete per tutto ciò che entra da fuori
(import, seed, DB a mano, `EfDeletionRepository`, `EfCallsignRenameService` — che riparentano **senza**
controllo).

---

## Parte 2 — La ricaduta verticale (C + B)

### Il fatto

L'ACC di Milano si divide in due su due strati. ⚠️ **Questa tabella è misurata sul `vipi.db` reale**, non
assunta: la prima stesura di questa carta dava per buono un albero diverso (ES5 figlio di ES2) e uno split a
FL305, e il dato l'ha smentita su tutti e due i punti.

| Settore | Pianta | Banda (misurata) | Padre (misurato) |
|---|---|---|---|
| `LIMM_WS2_CTR` | ovest | SFC – FL325 | — (radice) |
| `LIMM_ES2_CTR` | est | SFC – FL325 | WS2 |
| `LIMM_WS5_CTR` | ovest | FL325 – UNL | WS2 |
| `LIMM_ES5_CTR` | est | FL325 – UNL | **WS5** |

L'albero mette quindi i due alti **uno sotto l'altro**: `ES5 → WS5 → WS2`. E questo cambia quale dei due
casi è rotto.

**Il caso che già funziona.** ES5 chiuso, online `{WS2, ES2, WS5}`: catena `[ES5, WS5, WS2]`, primo online
**WS5**. Giusto — per fortuna, non per costruzione: funziona perché l'albero mette per caso l'altro settore
alto sulla strada.

**Il caso rotto è lo specchio.** WS5 chiuso, ES5 aperto — online `{WS2, ES2, ES5}` — e un punto diretto a
**WS5 a FL350**: catena `[WS5, WS2]`, primo online **WS2**. Sbagliato: WS2 arriva a FL325, sopra non ha
niente, mentre quel cielo lo sta tenendo **ES5**, che non vede arrivare nulla. E l'albero **non può**
dirlo, perché ES5 sta *sotto* WS5: un figlio non è mai un ripiego per suo padre.

Nessun avviso in nessuno dei due casi, perché la ricaduta **riesce sempre**: al massimo verso il settore
sbagliato.

La causa è strutturale: **la ricaduta è un albero a un padre solo, senza dimensione verticale**. Le quote
esistono (`ShapePart.BaseFeet/TopFeet`) ma le usa solo l'attribuzione del traffico, mai la ricaduta. E un
albero a un padre solo non può esprimere «questi due si sostituiscono a vicenda»: uno dei due deve per forza
stare sotto l'altro.

### C — la catena di ripiego dichiarata

Il padre singolo diventa una **lista ordinata**, dove ogni riga può portare una banda:

```
SectorFallback(SectorCallsign, Order, TargetCallsign, BaseFeet?, TopFeet?)
```

Quote nulle = **riga sempre valida**.

> 🔄 **Decisione cambiata in esecuzione, e vale la pena dire perché.** La carta diceva «la migrazione semina
> una riga per ogni `ParentCallsign`, e il padre sparisce dalla ricaduta». Non si fa: **il padre resta la coda
> implicita della catena**, e la tabella nasce **vuota**.
>
> Due ragioni, e la seconda è quella che decide. La prima: seminare le righe richiede un `Sql` in migrazione,
> che è fra le operazioni **vietate** dal presidio della finestra cieca (`MigrazioniDellaFinestraCiecaTests`)
> proprio perché è codice che nessun tipo controlla, eseguito su un archivio diverso da quello su cui è stato
> provato. La seconda: **a tabella vuota il comportamento è identico a quello di prima, riga per riga**. Una
> feature che entra in produzione senza poter cambiare niente finché qualcuno non scrive una riga è una
> feature che si può consegnare dentro una finestra cieca; una che riscrive la sorgente della ricaduta di
> tutti i settori, no.
>
> Il pre-flight §1 resta rispettato: chi cerca «dove si decide chi riceve un trasferimento» trova **una**
> funzione — `FallbackChain.Candidates` — che legge le righe dichiarate e poi i padri. Non due sorgenti in
> concorrenza: una lista sola, di cui il padre è la coda.

Risoluzione, dato un ricevente `S` e una quota `L`:

```
Risolvi(S, L, visti):
  se S è online                          → S
  righe di S in ordine, tenendo solo quelle la cui banda contiene L
       (le righe SENZA banda passano sempre)
    per ognuna, se non già vista         → Risolvi(bersaglio, L, visti)
  nessuna                                → UNICOM
```

Puro, deterministico, con l'insieme dei visitati: stessa forma di `TransferOnlineResolver`, con una lista
al posto della catena dei padri.

### B — la geometria propone le righe

**B non gira a runtime.** Gira nell'editor Struttura e **propone** le righe che l'admin conferma.

Regola di proposta: per il settore `S`, i sostituti candidati sono gli altri settori la cui **banda
verticale si sovrappone** a quella di `S`, ordinati per quanta quota condividono davvero. Restano fuori il
settore stesso e i suoi **antenati** — quelli sono già la coda della catena, e riproporli vorrebbe dire
scrivere a mano ciò che il sistema fa da sé. La fascia proposta è l'**intersezione** delle due bande: solo il
cielo che il sostituto può davvero prendere.

> 🔄 La carta prevedeva anche l'**adiacenza in pianta** come secondo criterio d'ordine. Non è stata fatta: la
> misura richiede i poligoni di tutti i settori a ogni apertura del pannello, e sui casi veri non cambia
> l'ordine — chi condivide banda con un settore d'area della stessa ACC gli è quasi sempre anche accanto. Si
> aggiunge il giorno che una proposta esce nell'ordine sbagliato, non prima.

⚠️ **B accoppia per BANDA, non per sovrapposizione in pianta.** ES5 e WS5 sono affiancati, non impilati: in
pianta non si toccano mai. È lo *strato* che li rende sostituti l'uno dell'altro. L'adiacenza in pianta è
solo un criterio di ordinamento fra pari.

Per WS5 (FL325–UNL) B trova: ES5 si sovrappone in banda al **100 %**, ES2 e WS2 allo **0 %** (stanno tutti
sotto FL325). Propone **una riga sola**:

| # | Bersaglio | Fascia | Da dove |
|---|---|---|---|
| 1 | ES5 | FL325 – UNL | proposta da B (stesso strato) |
| — | WS2 | — | il padre, che resta la coda e non si scrive |

**Una riga per settore, non una regola per ogni combinazione di split.** E su ES5 la simmetrica, «sopra
FL325 → WS5», che rende esplicito il caso che oggi funziona per caso.

### I casi, per intero

Con su WS5 la riga «FL325–UNL → ES5»:

| # | Online | Punto diretto a | Oggi | Con C+B |
|---|---|---|---|---|
| 1 | WS2, ES2, **ES5** | WS5 @ **FL350** | WS2 ❌ | riga 1 (la banda contiene FL350) → **ES5** ✅ |
| 2 | WS2, ES2, **ES5** | WS5 @ **FL250** | WS2 | riga 1 scartata (fuori banda) → il padre → **WS2** ✅ |
| 3 | solo WS2 | WS5 @ FL350 | WS2 | riga 1 → ES5 offline → il padre → **WS2** ✅ (invariato) |
| 4 | WS2, ES2, ES5 | WS5 **senza quota** | WS2 | righe con banda non valutabili → **WS2** (invariato) |

Il caso 2 è il punto della carta: **la stessa tabella dà due risposte diverse perché la quota è diversa** —
a FL250 il traffico dell'ovest è davvero di WS2, che quella quota ce l'ha.

Il caso 4 è onesto, non è un difetto: un punto che non dichiara la quota non può essere risolto in
verticale. In interfaccia va **detto**, o l'admin lo legge come un guasto.

### Tre dettagli che decidono se funziona

1. **Il flusso non ha una quota: ce l'hanno i suoi punti.** `TransferMatcher.IsCoveredBy` chiede «questo
   flusso è mio adesso?». Con la banda la domanda diventa **per punto**: WS5 vede il flusso di ES5 se
   *almeno un punto* gli ricade addosso, e in tabella vede **solo quei punti**. Altrimenti si ritrova
   davanti anche i punti a FL250, che sono di ES2.
2. **Una porta sola** — per i due che contano. `AgreementEditingService` (vista live, quota **del punto**)
   e `TransferMatcher` (flussi e suggerimento Aurora, quota **di crociera del volo**) chiamano la stessa
   `FallbackChain.Candidates`. Erano loro due a potersi contraddire in silenzio: la vista live che dice «vai
   a WS5» mentre il flusso resta sullo schermo di ES2.

   ⚠️ **`CoverageResolver` (statistiche) resta fuori, di proposito.** Lì la domanda è un'altra — a chi si
   accredita il traffico — e la risposta la dà già la **geometria**: `SectorVolumeMap` confronta la quota
   vera del velivolo con i volumi. Portarci dentro le righe dichiarate significherebbe rendere le pretese
   **per pezzo di forma** invece che per settore, perché è il pezzo ad avere una banda: è una fetta sua, non
   una riga da aggiungere qui. Finché non si fa, resta questo scarto: con ES5 chiuso e WS5 aperto, a FL350 la
   vista live manda a WS5 e le statistiche accreditano ES2. È uno scarto di **conteggio**, non operativo.
3. **Serve una migrazione, e siamo nella finestra cieca.** Fino al 16 settembre `Migrate()` gira all'avvio
   sul pacchetto FTP: una migrazione sbagliata è il sito giù, e giù resta. Va sotto
   `MigrazioniDellaFinestraCiecaTests` prima di partire.

---

## Pre-flight (FEATURE-PROCESS)

**1. Modello.** Nessun gemello: `SectorFallback` **sostituisce** `ParentCallsign` come sorgente della
ricaduta (il padre resta come riga senza banda). Chi cerca «dove si decide chi riceve un trasferimento»
trova **un** posto.

**2. Dispatch.** Nessuno `switch` nuovo per tipo. La risoluzione resta una funzione pura sola.

**3. Ingressi + verifica.** Ingresso: pannello «catena di ripiego» nel dettaglio nodo di Struttura, dove
oggi c'è `StructureFallbackChain` (che già *mostra* la catena: diventa editabile). Nessun catch-22 — la
migrazione semina la riga del padre, quindi ogni settore nasce con la sua catena. Verifica: live, guidando
Struttura e la vista live di un settore con lo split di Milano.

**4. Propagazione.** `ParentCallsign` **non si rimuove** in questo giro (resta la sorgente dell'albero di
contenimento e dell'AoR): si aggiunge la catena accanto come sorgente della *ricaduta*. Nessun nome morto.

## Esecuzione — slice

| # | Slice | Stato |
|---|---|---|
| 1 | Ciclo: derivazione sicura, guardia sull'albero effettivo, radici effettive, rilievo | ✅ |
| 2 | C: `SectorFallback`, risolutore con la quota, due chiamate + editor in Struttura | ✅ |
| 3 | B: proposte geometriche nell'editor | ✅ |
| 4 | La schermata di dettaglio: sequenza per passi + form allineato | ✅ |
| 5 | Bersaglio a digitazione, e i due difetti che ha fatto cadere | ✅ |

### Verifica live (31 agosto 2026)

App avviata su una **copia** del `vipi.db`, con `LIMF_WW0_APP.ParentCallsign` azzerato per riprodurre
esattamente la configurazione di produzione. Guidata in Edge con puppeteer-core (skill `verifica-live`).

**Cosa si è confermato.** In `/services/vsop/admin/sector-structure`, `LIMF_WW0_APP` compare **una volta
sola**, come radice, con `LIMF_WN0_APP` figlio suo e **nessun secondo WW0 sotto di lui**. La sua catena dice
«Root (no parent)» — orfano e visibile, non ciclico e muto, come la mossa 1 prometteva. Il pannello della
catena di ripiego c'è, la riga si scrive, si salva e **sopravvive a un ricarico completo**. Zero errori in
console, zero risposte ≥ 400.

⚠️ Il rifiuto lato server dell'anello **non** si è potuto provare dal browser: la tendina dei padri esclude
già i discendenti, quindi il caso non è nemmeno proponibile da lì. Resta coperto dai test
(`GerarchiaSenzaAnelliTests`), che è dove deve stare — la guardia serve per le porte che *non* sono la
tendina.

**Quattro difetti trovati solo qui, nessuno dei quali sarebbe uscito dai test.**

| | Cosa si vedeva | Perché |
|---|---|---|
| 1 | Due titoli, uno in italiano dentro la pagina inglese | `StructureFallbackChain` aveva un letterale «Catena di fallback:» scritto a mano. Innocuo finché era solo; con l'intestazione nuova sopra, diventava un doppione nella lingua sbagliata |
| 2 | **155 proposte** su `LIMM_WS5_CTR`: Algeri, Vienna, Zurigo, Belgrado | Accoppiando per sola banda, **ogni settore alto d'Europa** è candidato. La proposta si ferma all'ACC: quando un settore chiude, a raccoglierlo è un collega dello stesso centro |
| 3 | La riga accettata mostrava il bersaglio **vuoto** | `value` su un `<select>` con opzioni rese dopo non seleziona niente: serve `selected` esplicito. ⚠️ E una riga che *sembra* senza bersaglio, al salvataggio successivo, **viene scartata**: il difetto si mangiava il dato |
| 4 | Proposti `LIMC_DEL`, `LIML_GND` come ripiego a FL325 | DEL e GND non hanno poligono, e `BandOf` senza pezzi torna «tutta aperta». **«Non ho una forma» non è «prendo tutto il cielo»**: è «non lo so», e chi non lo sa non si propone |

Dopo le correzioni, su `LIMM_WS5_CTR` le proposte sono **due**: `LIMM_ES5_CTR` (quella che serve, per prima)
e `LIMM_MIL_CTR`, che una banda aperta ce l'ha davvero e che l'admin scarta guardandola.

> Il difetto 2 è anche la smentita della nota qui sopra su B: l'adiacenza in pianta non era «un raffinamento
> da fare dopo», era il fatto che **mancava del tutto un criterio geografico**. Il confine giusto si è
> rivelato l'ACC, non la geometria — più semplice e più vicino a cosa significa il gesto.

### La schermata di dettaglio — 1 settembre 2026

Due richieste del committente dopo aver letto il meccanismo: **il form non combaciava con il resto della UI**,
e **mancava la sequenza intera** con, alla stessa altezza, i settori che si dividono il traffico per quota.

**La sequenza.** Il riquadro «Catena di ripiego» mostra ora tutta la catena, **per passi**, e le voci di uno
stesso passo stanno alla stessa altezza:

```
LIMM_WS5_CTR  CTR   QUESTO SETTORE
 ①  FL325–UNL  → LIMM_ES5_CTR  CTR  LIMM
    ogni quota → LIMM_WS2_CTR  CTR  LIMM  PADRE
 esauriti tutti, il traffico va su UNICOM
```

⚠️ **L'altezza è il punto.** Un elenco piatto direbbe che si provano una dopo l'altra, ed è falso: «sopra
FL325 ES5, altrimenti WS2» **non è una sequenza di tentativi, è una divisione** — decide la quota quale
delle due vale. Chi è in frequenza adesso si distingue, perché è la voce che il traffico prenderebbe davvero.

⚠️ **E viene dalla STESSA camminata che risolve** (`FallbackChain.Sequence` e `FallbackChain.Candidates`
condividono `Cammina`, con l'unica differenza del filtro sulla fascia). Disegnarla a parte vorrebbe dire due
catene che possono divergere — cioè un pannello che mostra una cosa e una ricaduta che ne fa un'altra: il
difetto che questa carta esiste per chiudere, riaperto proprio nella schermata che lo racconta.

⚠️ La sequenza legge i ripieghi **salvati**, non quelli in lavorazione: racconta cosa succede adesso al
traffico vero, non cosa succederebbe premendo «Applica». Le modifiche non salvate stanno nell'editor, dove
si vedono per quello che sono.

Il riquadro separato che elencava i soli padri **è sparito**: diceva metà della stessa cosa, e due riquadri
per una catena sola si leggono come due meccanismi.

**Il form.** Prima erano controlli nudi in fila, con la propria misura e i propri tasti: si vedeva che
venivano da un'altra parte. Ora usa il vocabolario di questa pagina — `field` + `label` come `.inline-form`,
`.btn` per le azioni, la carta a superficie morbida per la sequenza come `.fallback-chain`.
⚠️ I selettori stanno sotto `.struct`: senza, le regole di `.inline-form` scritte più in alto nel foglio
vincono per specificità — è la trappola già pagata su `.res-table`.

**Un terzo difetto, trovato dallo screenshot mentre si guardava il resto.** `StructureCoverage` stampava
«Copre (dominio):» e «N aeroporti coperti» **scritti a mano in italiano**, quindi così anche nella pagina
inglese — nello stesso riquadro che si stava allineando. Chiuso: due chiavi distinte per singolare e plurale,
perché in inglese la parola che cambia non è in fondo e una regola sulle desinenze italiane l'avrebbe rotta.
Era la voce «fuori perimetro» dell'elenco qui sotto, ed è caduta perché il perimetro se l'è tirata dentro.

### Il bersaglio si scrive, non si scorre — 1 settembre 2026

Richiesta del committente: **«deve consentire anche l'inserimento manuale, così da non dover scorrere una
lista enorme ogni volta»**. Aveva ragione: i bersagli possibili sono tutti i settori dei cataloghi, e
scorrerne centinaia per trovarne uno non è una scelta, è una ricerca fatta a mano.

Il campo è ora a **digitazione**, con `TypeaheadPicker` — lo stesso componente dell'editor trasferimenti,
dove lo stesso problema era già stato risolto, tastiera compresa. Si può **scrivere il callsign per intero
senza scegliere dall'elenco**: quel che si batte *è* il bersaglio, normalizzato in maiuscolo. Se non è nei
cataloghi lo dice subito una spia ambra accanto al campo, e il servizio lo rifiuta comunque al salvataggio —
meglio due volte che una riga muta.

**E ha fatto cadere un difetto mio.** La tendina si costruiva su `EligibleParents`, che **esclude i
discendenti**: regola giusta per il *padre* (è l'anti-ciclo), sbagliata per un **ripiego**. È letteralmente
il caso di Milano — ES5 è figlio di WS5, e quando WS5 chiude è ES5 a tenere quel cielo. Con quella lista il
bersaglio giusto **non era proponibile**: compariva solo perché già salvato. Ora i candidati sono tutti i
settori dell'albero tranne sé stesso. Un ripiego non può creare un anello di copertura come lo crea un
padre: la catena ha la sua guardia sui nodi già visti, e l'albero non lo tocca.

**Due difetti in più, trovati guidando l'app.**

1. ⚠️ **La fascia FL non si salvava senza togliere il fuoco dal campo.** I due input usavano `@onchange`,
   che su un campo di testo scatta al **fuoco perso**: chi scrive la quota e va dritto su «Applica» perdeva
   il numero, e la riga si salvava **senza fascia** — cioè valida a ogni quota, il contrario di quel che
   aveva scritto. Passati a `@oninput`. È la stessa trappola già pagata sul ciclo AIRAC degli spazi aerei; il
   campo del bersaglio non l'aveva perché il picker usa già `oninput`, erano questi due i dispari.

2. ⚠️ **La sequenza mentiva in un caso.** ES5 dichiara «sopra FL325 → WS5» **e** ha WS5 come padre: il
   disegno mostrava solo la riga con la fascia, facendo credere che sotto FL325 WS5 non ci fosse — mentre
   c'è, come padre. Ora allo stesso passo si vedono **tutti i motivi** per cui ci si arriva:

   ```
    ①  FL325–UNL  → LIMM_WS5_CTR
       ogni quota → LIMM_WS5_CTR  PADRE
    ②  ogni quota → LIMM_WS2_CTR  PADRE
   ```

   Due voci per il disegno, **un candidato solo** per chi risolve: `Candidates` distingue, `Sequence` no.

### Il giro sulla pagina — 1 settembre 2026

Tre richieste del committente sulla pagina `admin/sector-structure`, guardata **in inglese** in produzione.

**1. La finestra di eliminazione parlava italiano dentro una pagina inglese.** La finestra sì — titolo,
intestazioni, tasti: quelli passano dalle risorse. Ma il **piano** che ci sta dentro lo scrive
l'applicazione, e l'applicazione non può leggere i `.resx` (vivono in `Vipi.Ui`): si porta dietro tutte e
due le lingue con `Messaggio.Lingua`, ed era stato fatto **a metà** — circa venti frasi di
`DeletionRules` erano rimaste italiane per tutti, insieme a `SogliaEliminazione.MotivoDelRifiuto` e a
**tutti** i verdetti di `IvaoSourcePresenceProbe` («chiedi alla sorgente adesso»). Risultato a schermo:
*«Delete LIBB_ES_CTR?»* e sotto *«elimina prima l'accordo di coordinamento…»*.

⚠️ Le **tracce** della sonda restano in italiano di proposito: sono diagnostica (`GET`, status, conteggi),
non prosa da leggere.

⚠️ **I test che asserivano quelle frasi vanno ancorati alla lingua.** La cultura di questa macchina è
**inglese**: un'asserzione sul testo italiano, senza `CulturaDiProva`, passa in Italia e cade qui — ed è
successo, cinque test di `SourcePresenceProbeTests` sono diventati rossi appena le frasi hanno avuto due
versioni. Non è una fragilità nuova: è una fragilità vecchia che si vede.

**2. Le due sezioni si chiudono.** «Coverage hierarchy» e «Orphan sectors» nascono aperte e si richiudono
dal titolo, che **è** la maniglia. Il chevron è `.grp-chev`, lo stesso delle card dell'albero: stesso gesto
sulla stessa pagina, stesso segno. Serve a lavorare — la gerarchia è alta quanto lo schermo, e mentre si
sistema un orfano sta in mezzo. ⚠️ `hidden` da solo non bastava: `.gerarchia-2col` ha un `display:grid`
suo, e la regola dell'attributo sta nel foglio del **browser**, che perde contro il nostro.

**3. Il piede e il tetto si scrivono anche in piedi.** C'era il solo FL, e una fascia che comincia a
**2 500 piedi non si poteva scrivere**: si batteva 25 e usciva FL25. Ora accanto al numero c'è la tendina
**FL/ft**, la stessa dei Trasferimenti — e come lì, **cambiare unità non converte**: il numero battuto resta
quello e cambia di significato.

⚠️ **L'unità non si salva**, ed è una scelta: in archivio ci sono solo i piedi (come nelle forme dei settori
e nelle bande dell'AoR), e aggiungere due colonne avrebbe voluto dire una **seconda migrazione dentro la
finestra cieca** per una comodità di scrittura. Alla rilettura l'unità si **deduce** con la convenzione che
il sito usa già in `StatsView.Livello`: sotto i 10 000 piedi — o se non è un multiplo di cento — si legge in
piedi, sopra in livelli di volo. Non è fedele a quel che fu battuto (chi scrive «FL30» rilegge «3 000 ft»),
ed è il prezzo dichiarato: **la quota è la stessa, cambia come la si legge**.

E la **fascia disegnata** segue la stessa convenzione: `2,500 ft–FL195`, non più `FL25–FL195`.

**E lo stesso gesto sulla pagina ACC** (`admin/acc`, seconda richiesta dello stesso giro): «ACC» e «ACC
sectors» si chiudono dal loro titolo. ⚠️ Il riquadro degli ACC un titolo di sezione **non ce l'ha** — era
stato tolto apposta perché ripeteva l'H1 tre righe sopra — quindi la maniglia è **l'H1 stesso**, che è già
il titolo di quel riquadro: non se ne rimette uno solo per avere dove cliccare. ⚠️ E scegliere un ACC
**riapre** il riquadro dei settori: `TogglePick` porta in vista `#settori-acc`, e su una sezione chiusa lo
scorrimento avrebbe mostrato un titolo e basta.

**Verificato dal vivo** (Edge+CDP, copia del DB): le due sezioni si chiudono e si riaprono (`aria-expanded`
e chevron seguono), la riga `LIBB_ES_CTR → LIBB_EU_CTR 2 500 ft–FL195` si salva e **si rilegge com'è stata
scritta** (2500 in ft, 195 in FL) dopo un ricaricamento completo, e la finestra di eliminazione è inglese da
cima a fondo, verdetto della sorgente compreso. Sulla pagina ACC: pagina da **8 627 px a 1 500 px** con
tutt'e due i riquadri chiusi, testata `sticky` sempre a **una riga** (71 px, `--st-head-h` la segue), e
scegliendo un ACC il riquadro dei settori si riapre da sé con le sue 11 righe.

### Cosa resta aperto

- **Statistiche per pezzo di forma** (vedi il dettaglio 2 qui sopra): finché le pretese sono per settore, le
  righe dichiarate non entrano nell'attribuzione del traffico.
- **I 19 aeroporti che pendono da una propria APP** restano tali: è la configurazione normale, e ora è
  innocua. Non c'è niente da riparare, ma è il posto da cui guardare se un anello ricomparisse.
- **Le `UnificationRule`** restano un motore senza editor (le applica solo `AorService`, per la mappa AoR).
  Questa carta non le tocca: la ricaduta ora ha la sua strada, e sovrapporre le due sarebbe il secondo
  meccanismo che il pre-flight §1 vieta.
