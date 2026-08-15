# Feature — Varianti a livelli: alternative pari-grado, eccezioni annidate, eccezioni trasversali

Data: 2026-08-12 · Stato: **CHIUSO — suite 2185 verde, Release 0 warning su entrambi i TFM,
✅ verifica live eseguita** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue e **corregge** [trasferimenti ACC↔APP](2026-08-11-trasferimenti-acc-app.md), il cui gruppo di varianti
non è ancora fuso: branch `feature/trasferimenti-acc-app`, PR #13.

## Obiettivo

Il gruppo di varianti introdotto ieri ha **una forma sola**: una capofila più N righe subordinate, con la riga
«negli altri casi» in fondo. Alla prima lettura del committente sono usciti tre difetti, e il terzo dice che
la forma è proprio quella sbagliata.

**1. L'ordine è rovesciato.** «Negli altri casi» stava in fondo. Un accordo si legge come si scrive una norma:
prima la regola generale, poi le eccezioni. La condizione operativa standard va **in testa**, ed è la capofila.

**2. Le alternative non sono subordinate a nessuno.** Le due righe reali in archivio:

| Id | CoP | Livello | Pista | Area |
|---|---|---|---|---|
| 76 | BIRSU | FL150 | **07** | — |
| 77 | BIRSU | FL130 | **25** | LI R403B |

Pista 07 e pista 25 sono **pari-grado**: nessuna è lo standard dell'altra, e ognuna può avere le proprie
eccezioni. Il modello «una capofila + subordinate» non le sa dire. ⚠️ È il caso che la scheda di ieri usava
come esempio del gruppo varianti: era l'esempio giusto della cosa sbagliata.

> Nota sul dato: la riga 77 porta pista 25 **e** area R403B insieme, e non esiste una riga «pista 25,
> normalmente». Nel modello nuovo quella riga diventa «pista 25 → eccezione se R403B attiva», e la sua
> capofila **manca**. Il modello di ieri non permetteva di accorgersene; questo sì, e l'editor lo segnala.

**3. Due livelli non bastano.** Il committente porta il caso: «con area attiva» e, dentro quello, «con area
attiva **e di notte**». È un raffinamento di un raffinamento: serve un annidamento a profondità libera, non
un rango binario.

**4. Serve anche l'eccezione trasversale.** Una condizione che **scavalca** le alternative — «di notte,
qualunque pista» — non è né un'alternativa (non partiziona) né un'eccezione di una capofila (non appartiene a
una sola).

## Decisioni del committente

1. **La capofila senza condizioni non scrive nulla.** Niente «negli altri casi», niente «normalmente»: cella
   vuota. Il concetto «negli altri casi» **sparisce**.
2. **Appartenenza per ordine**: un'eccezione appartiene alla riga che la precede al livello superiore, come una
   lista puntata. Con avviso quando resta orfana.
3. **Eccezione trasversale: sì**, serve.
4. **Profondità libera**, non due livelli.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova. `IsOtherwise` (bool, aggiunto ieri) **viene sostituito** — non
affiancato — da `VariantDepth` + `IsGroupWide`. Un flag binario e una profondità sono lo stesso concetto a due
risoluzioni diverse: tenerli entrambi vorrebbe dire due sorgenti di verità sulla stessa cosa.

**2. Dispatch.** Nessuno `switch` nuovo. Il rendering annidato è un'unica funzione che legge la profondità,
non un ramo per livello.

**3. Ingressi + verifica.** Ingressi esistenti (`/vsop/admin/trasferimenti`, le sezioni Coordinamenti).
Verifica: `/verifica-live` sul caso BIRSU reale, portato alla forma nuova (due alternative per pista, una
eccezione per area, una trasversale).

**4. Propagazione.** ⚠️ **Questa modifica RIMUOVE un campo e un concetto** (`IsOtherwise`, «negli altri
casi»), quindi la domanda 4 è quella che pesa. Elenco completo in «Propagazione».

## Modello

```
VariantDepth  int    // 0 = alternativa di primo livello; 1 = eccezione; 2 = eccezione dell'eccezione; …
IsGroupWide   bool   // la riga scavalca le alternative: vale per tutto il gruppo
```

Sostituiscono `IsOtherwise`. Il gruppo (`VariantGroup`) e l'ordine (`Order`) restano quelli che sono.

**Un gruppo è un outline.** Le righe si leggono in ordine; la profondità dà il rientro; una riga di profondità
`N` appartiene all'ultima riga di profondità `N-1` che la precede. È la struttura di una lista annidata, ed è
la ragione per cui non serve un puntatore al padre: l'ordine *è* la struttura.

```
BIRSU  FL150   pista 07                        depth 0   ← alternativa
       FL130     con R403B attiva              depth 1   ← eccezione della 07
       FL110       e di notte                  depth 2   ← eccezione dell'eccezione
       FL130   pista 25                        depth 0   ← alternativa, pari-grado alla 07
       FL90    in ogni caso · traffico LIPZ    depth 0 + IsGroupWide
```

CoP e ricevente restano in `rowspan` su tutto il gruppo: è un accordo solo.

**Invarianti**, validati nel service con `Vipi.Application.*.ValidationException`:

- profondità `N > 0` richiede una riga di profondità `N-1` prima di sé nello stesso gruppo (niente orfane);
- niente salti di profondità (0 → 2);
- `IsGroupWide` solo a profondità 0 — una riga che scavalca le alternative non può essere annidata dentro una;
- una riga trasversale non porta ordine rispetto alle alternative: si rende sempre **in fondo** al gruppo.

**Avviso non bloccante** (il caso della riga 77): un'eccezione la cui capofila non ha una riga «senza
condizioni proprie» lascia scoperto il caso normale di quella alternativa. Non è un errore — la capofila può
essere esaustiva — ma è quello che il lettore non trova quando cerca.

### Migrazione

Nuova, additiva: droppa `IsOtherwise`, aggiunge `VariantDepth` (default 0) e `IsGroupWide` (default false).
Nessun backfill: `IsOtherwise` non è mai stato scritto da nessuno — la migrazione di ieri **non è stata
applicata né alla produzione né al `vipi.db` del progetto** (verificato: 73 righe, nessuna colonna nuova).

> **Perché una seconda migrazione e non la correzione della prima.** Rigenerare quella di ieri darebbe una
> storia più pulita, ma è già spinta e sotto PR: riscriverla richiederebbe un force-push su un ramo in
> revisione. Il costo di una migrazione in più è zero righe di dati e una riga di changelog; il costo di un
> force-push è la fiducia di chi sta leggendo il PR.

Da emettere **due volte** (SQLite e MySQL) e da provare su copia del `vipi.db` reale.

## Frase

Nella **tabella** una riga mostra solo il proprio delta: il rientro dà il contesto.
Nella **frase** no — la frase è autonoma e viaggia da sola nella prosa del documento, quindi **cumula la
catena degli antenati**:

> …trasferisce … con pista 07 in uso **e** R403B attiva **e** di notte.

Senza cumulo, la riga di profondità 2 direbbe «di notte» e perderebbe le due metà che la rendono vera. Il
cumulo usa la congiunzione che c'è già (`Condition.Join`), quindi vale in italiano e in inglese senza
aggiungere chiavi.

La riga **trasversale** premette il proprio marcatore: chiave `Otherwise` **trasformata** in `GroupWide` —
IT «in ogni caso», EN «in any case». Non è una chiave nuova: è quella di ieri che cambia significato insieme
al concetto, e va rinominata perché un nome che descrive un meccanismo sparito mente a chi legge fra sei mesi.

## Viste

Rientro per profondità nella colonna condizione; la tinta di fondo distingue le eccezioni dalle alternative
(le alternative sono pari-grado alla capofila, non continuazioni). La trasversale va in fondo al gruppo, senza
rientro, col marcatore.

`CoordTable.Blocks()` cambia: oggi ordina per `IsOtherwise` e mette una riga in fondo; dovrà **preservare
l'ordine** delle righe (che ora è la struttura) e spostare in fondo solo le trasversali.

## Editor

Il tasto «⑂» di ieri diventa **due azioni**, perché sono due intenzioni diverse:

- **«+ alternativa»** — nuova riga a profondità 0, inserita dopo l'ultima discendente della capofila corrente
  (altrimenti spezzerebbe un sottoalbero);
- **«+ eccezione»** — nuova riga a profondità `corrente + 1`, subito sotto la riga da cui si parte.

La trasversale è una **spunta** sulla riga a profondità 0, non un terzo tasto: è una proprietà della riga, e
un tasto in più su una barra che ne ha già otto si perde.

⚠️ **I tasti su/giù devono muovere il sottoalbero, non la riga.** Spostare una capofila lasciando indietro le
sue eccezioni le riassegna in silenzio a un'altra alternativa — un cambio di significato senza un errore. È il
punto più delicato di questa carta, ed è la ragione per cui l'appartenenza per ordine ha bisogno di guardie.

## Propagazione — cosa cita `IsOtherwise` o «negli altri casi»

La domanda 4 del pre-flight, per esteso. Ogni voce va chiusa **nello stesso giro**:

| Punto | Cosa |
|---|---|
| `TransferPoint`, `TransferPointInput`, `TransferPointRow` | campo sostituito |
| `EfTransferRepository` | vincolo «una sola per gruppo» → i nuovi invarianti; `AddVariantAsync` → due metodi |
| `TransferHandoffFacet` + `CoordinationSentenceData` | il flag viaggia nella faccetta |
| `CoordinationSentenceComposer` | da «rimpiazza la condizione» a «cumula la catena» |
| `CoordinationDerivation.ToRow` | `ConditionLabel = tpl.Otherwise` → delta proprio + marcatore trasversale |
| `AppCoordRow` | `IsOtherwise` → profondità + trasversale |
| `CoordTable.razor` | `Blocks()`, rientro, ordine |
| `AdminTrasferimentiPage` | `FacetForm`, i due tasti, l'avviso, i tasti di spostamento |
| `ICoordinationSentenceTemplate` | `Otherwise` → `GroupWide` |
| `CoordinationSentenceOptions` (Hosting) | idem, chiave del file di configurazione |
| `.resx` IT + EN | `Coord_Otherwise`, `Xfer_Otherwise`, `Xfer_OtherwiseTitle`, `Xfer_GroupNoOtherwise` |
| Test | `CoordTableTests`, `CoordinationDerivationTests`, `TransferRepositoryTests`, composer |
| Doc | questa scheda, la scheda di ieri (§varianti), `modello-dati.md` §9.20-bis, `refactor/07` §8, rounds |

## Test

- Repository: profondità, orfane, salti, `IsGroupWide` annidata (tutte respinte), spostamento di sottoalbero.
- Composer: cumulo lungo la catena a profondità 2, marcatore trasversale, IT **e** EN.
- `CoordTable`: ordine preservato, rientro per profondità, trasversale in fondo, gruppi di flussi diversi
  che non si fondono (già coperto, non deve regredire).
- Derivazione: il caso BIRSU reale nella forma nuova.

## ✅ Verifica live — eseguita il 12 agosto 2026

Sul caso BIRSU vero, costruito guidando l'editor: capofila «pista 07» → eccezione «area LI R403B» → eccezione
dell'eccezione «di notte» → riga trasversale «traffico militare». Poi letto nel documento.

**Confermato a schermo:**

| Cosa | Esito |
|---|---|
| Frase a profondità 1 | «… su BIRSU **con pista 07 in uso e LI R403B attiva**.» |
| Frase a profondità 2 | «… **con pista 07 in uso e LI R403B attiva e in condizione di notte**.» |
| Frase trasversale | «… su BIRSU **in ogni caso, in condizione traffico militare**.» |
| Tabella | `BIRSU[rowspan 4]` · eccezione rientrata 20px · eccezione dell'eccezione 34px · trasversale in fondo |
| Delta in tabella | l'eccezione mostra `area LI R403B`, non la condizione della capofila |
| Editor | rientro 22px / 38px, marcatore «in any case» sulla riga trasversale |
| Avviso | «"07" ha eccezioni ma non dice cosa vale nel suo caso normale» — **compare sul dato reale** |

L'avviso è la conferma del punto §3 dell'obiettivo: il buco che il modello di ieri non permetteva di vedere
adesso si vede da solo, sul dato che era già in archivio.

**Un difetto di lingua trovato leggendo, non prevedendo**: il marcatore trasversale accostava due preposizioni
— «in ogni caso **in** condizione traffico militare». Ora c'è una virgola. È il genere di cosa che si vede solo
resa: la composizione era corretta, la lettura no.

## Esito — scostamenti e cose imparate

**Uno scostamento dalla carta, deciso in esecuzione.** La carta diceva che la frase avrebbe unito «le clausole
di ciascun livello con la congiunzione». Composta così, usciva **«con pista 07 in uso e con R403B attiva»**: ogni
livello ripeteva la propria preposizione. La condizione cumulata è **una** condizione in AND, e la fraseologia
approvata sa già dirla — quindi la catena si **fonde in una clausola sola** prima di diventare parole, e la
forma dedicata pista+area (`RunwayAndArea`) torna a valere anche quando i due pezzi vengono da livelli diversi.

**Due difetti dello scaffolding EF, presi guardando la migrazione invece di fidarsi.** Lo strumento proponeva un
`RenameColumn` da `IsOtherwise`, e lo proponeva **diverso nei due provider**: SQLite verso `VariantDepth`,
MySQL verso `IsGroupWide`. Due inferenze incompatibili dalla stessa modifica — che è la prova che il rename è
una supposizione sui tipi, non un'intenzione letta dal modello. Un `true` sopravvissuto sarebbe diventato «riga
a profondità 1» di là e «riga che scavalca le alternative» di qua, senza che nulla lo segnalasse. Entrambe
riscritte come drop + add, che con zero dati costa zero.

**Il caso reale ha convalidato l'avviso prima ancora dei test.** La riga 77 in archivio (pista 25 **e** area
R403B, senza una «pista 25, normalmente») fa scattare l'avviso appena la si apre nella forma nuova: il buco che
il modello di ieri non sapeva nemmeno esprimere adesso si segnala da solo.

## Fuori scopo

- **Precedenza calcolata**: il documento resta editoriale. Nessuno decide *quale riga vince* al posto del
  controllore; la struttura dice come si leggono, non come si risolvono.
- **Profondità massima**: nessun limite artificiale. Il rientro si adatta.
