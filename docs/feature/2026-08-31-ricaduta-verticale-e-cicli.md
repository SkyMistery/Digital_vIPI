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

L'ACC di Milano si divide in due su due strati:

| Settore | Pianta | Banda |
|---|---|---|
| WS2 | ovest | SFC – FL305 |
| ES2 | est | SFC – FL305 |
| WS5 | ovest | FL305 – UNL |
| ES5 | est | FL305 – UNL |

Albero di oggi: `WS2` radice, `ES2` e `WS5` figli di WS2, `ES5` figlio di ES2.

Con online `{WS2, ES2, WS5}` — cioè i due bassi divisi e l'alto tutto a WS5 — un punto di trasferimento
diretto a **ES5 a FL350** oggi risolve così: catena `[ES5, ES2, WS2]`, ES5 chiuso, primo online **ES2**.
Sbagliato: a FL350 quel cielo è di **WS5**, che infatti non vede niente. Nessun avviso, perché la ricaduta
è **riuscita** — solo verso il settore sbagliato.

La causa è strutturale: **la ricaduta è un albero a un padre solo, senza dimensione verticale**. Le quote
esistono (`ShapePart.BaseFeet/TopFeet`) ma le usa solo l'attribuzione del traffico, mai la ricaduta.

### C — la catena di ripiego dichiarata

Il padre singolo diventa una **lista ordinata**, dove ogni riga può portare una banda:

```
SectorFallback(SectorId, Order, TargetCallsign, BaseFeet?, TopFeet?, Origin)
```

Quote nulle = **riga sempre valida**. Il padre di oggi *è* precisamente questo: una riga sola, senza banda,
in fondo. **C non affianca un meccanismo al padre: lo sostituisce** (pre-flight §1), e la migrazione è
meccanica — una riga per ogni `ParentCallsign` esistente.

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
verticale si sovrappone** a quella di `S`, ordinati per frazione di sovrapposizione in quota → adiacenza in
pianta → distanza gerarchica.

⚠️ **B accoppia per BANDA, non per sovrapposizione in pianta.** ES5 e WS5 sono affiancati, non impilati: in
pianta non si toccano mai. È lo *strato* che li rende sostituti l'uno dell'altro. L'adiacenza in pianta è
solo un criterio di ordinamento fra pari.

Per ES5 (FL305–UNL) B trova: WS5 si sovrappone in banda al **100 %**, ES2 allo **0 %**. Propone:

| # | Bersaglio | Banda | Da dove |
|---|---|---|---|
| 1 | WS5 | FL305 – UNL | proposta da B (stesso strato) |
| 2 | ES2 | — | il padre di oggi |

**Due righe, non una regola per ogni combinazione di split.**

### I casi, per intero

| # | Online | Punto | Oggi | Con C+B |
|---|---|---|---|---|
| 1 | WS2, ES2, WS5 | ES5 @ **FL350** | ES2 ❌ | riga 1 (banda contiene FL350) → **WS5** ✅ |
| 2 | WS2, ES2, WS5 | ES5 @ **FL250** | ES2 | riga 1 scartata, riga 2 → **ES2** ✅ |
| 3 | solo WS2 | ES5 @ FL350 | WS2 | riga 1 → WS5 offline → ricorsione → **WS2** ✅ (invariato) |
| 4 | WS2, ES2, WS5 | ES5 **senza quota** | ES2 | righe con banda non valutabili → **ES2** (invariato) |

Il caso 2 è il punto della carta: **la stessa tabella dà due risposte diverse perché la quota è diversa**.
Il caso 4 è onesto, non è un difetto — un punto che non dichiara la quota non può essere risolto in
verticale; in interfaccia va **detto** («quota non specificata: ricaduta non verticale»), o l'admin penserà
a un guasto.

### Tre dettagli che decidono se funziona

1. **Il flusso non ha una quota: ce l'hanno i suoi punti.** `TransferMatcher.IsCoveredBy` chiede «questo
   flusso è mio adesso?». Con la banda la domanda diventa **per punto**: WS5 vede il flusso di ES5 se
   *almeno un punto* gli ricade addosso, e in tabella vede **solo quei punti**. Altrimenti si ritrova
   davanti anche i punti a FL250, che sono di ES2.
2. **Una porta sola.** `AgreementEditingService`, `TransferMatcher`, `CoverageResolver` (dove la quota è
   quella vera del velivolo, che l'attribuzione già maneggia) e `ConsistencyReportService` chiamano **la
   stessa funzione**. Tre su quattro significa che la vista live e l'elenco dei flussi si contraddicono in
   silenzio.
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
| 1 | Ciclo: derivazione sicura, guardia sull'albero effettivo, radici effettive, rilievo | 🟡 |
| 2 | C: `SectorFallback`, migrazione dal padre, risolutore, quattro chiamate | 🟡 |
| 3 | B: proposte geometriche nell'editor | 🟡 |
