# L'ordine delle sezioni è una scelta editoriale — carta (26 agosto 2026)

> **Stato: ✅ ESEGUITA il 26 agosto 2026**, ramo `identita-settori`.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Non aggiunge storage: l'ordine è già `DocumentSection.Order`, versionato e catturato nello snapshot di
> release. Segue [catalogo sezioni, fonte unica](../refactor/13-audit-tre-documenti.md).

## La domanda

> «Nell'editor le sezioni devono poter essere spostate sopra o sotto all'interno dello stesso gruppo, in modo
> da dare un ordine personalizzato se necessario. Nell'editor, ogni sezione deve riportare quante posizioni è
> sopra o sotto quella standard.»

## §0 — Cosa c'era già

| Pezzo | Dove | Stato |
|---|---|---|
| Ordine fra fratelli | `DocumentSection.Order` | ✅ versionato, copiato in bozza, catturato in release |
| Scambio con il fratello adiacente | `EfEditingRepository.MoveSectionAsync` → `SwapOrder` | ✅ già per gruppo (stesso `ParentSectionId`) |
| Ordine standard | `SectionCatalog.For(profile)` | ✅ ma è solo l'ordine di **nascita** del documento |
| Viewer che seguono l'ordine del documento | ACC/APP/vLOA | ✅ tranne **una** eccezione, §3 |

**Mancava un tasto, non un motore.** L'editor condiviso (`DocumentSectionsEditor`) legava insieme tre cose
sotto `IsMandatory`: una sezione di catalogo non si rinomina, non si elimina e — di conseguenza — **non si
spostava**. Le prime due sono giuste (il catalogo decide chiavi e titoli), la terza no: l'ordine è una scelta
editoriale, e il catalogo lo decide alla nascita del documento, non per sempre.

## §1 — Il gruppo

«Gruppo» = i **fratelli**, cioè le sezioni con lo stesso padre. Coincide già col perimetro dello scambio:

- vIPI ACC: le sezioni di **un blocco** (Aerovia o gruppo APP) — l'editor monta un'istanza per blocco con
  `RootSections="blockSection.Children"`;
- vIPI APP e vLOA: le sezioni di primo livello;
- ovunque: le sotto-sezioni di una sezione, fra loro.

Niente spostamenti **fra** gruppi: una sezione non cambia padre. Non è una limitazione da aggirare — spostare
«Frequenze» dal blocco Aerovia a un gruppo APP significherebbe cambiarne il significato, non la posizione.

## §2 — Lo scostamento

`SectionOrdering.OffsetsFromStandard(profile, fratelli)` (funzione pura, `Vipi.Application.Content`): dice di
quanti posti ogni sezione **fissa** si è allontanata dall'ordine di catalogo. Negativo = più in alto, positivo
= più in basso. L'editor lo scrive come pill accanto al titolo: `↑2`, `↓1`; a posto, nessuna pill.

Due decisioni che si vedono nei test:

1. ⚠️ **Si confrontano solo le sezioni fisse fra loro.** Una sezione libera non ha una posizione standard:
   contarla farebbe apparire `↓1` su tutte le fisse che la seguono appena qualcuno ne infila una in testa —
   scostamenti che nessuno ha prodotto.
2. ⚠️ **Il confronto è sulle sezioni PRESENTI.** Un blocco Aerovia non ha il VFR: la sezione che manca non
   lascia un buco che sposti le altre.

Lo scostamento si legge **sempre**, non solo in modifica: dice perché il documento non ha l'ordine che ci si
aspetta, ed è una domanda che ci si fa leggendo.

## §3 — L'eccezione trovata strada facendo

Il viewer della vLOA rendeva le **due direzioni** dei coordinamenti in una sequenza scritta nel codice
(uscente, poi entrante), pur riconoscendole per chiave. Spostarle nell'editor avrebbe cambiato l'editor e
**non** il documento pubblicato — la trappola del doc 11 §8 (viewer ed editor con sequenze opposte per la
stessa sezione). Ora l'ordine è quello delle sotto-sezioni, con l'ordine canonico come ripiego per gli
snapshot storici (dove entrambe portano ancora la chiave del padre e si distinguono solo per posizione).
La **chiave** resta l'unica cosa che dice quale verso è: cambia solo chi viene prima.

## §4 — Cosa NON cambia

- Le sezioni di catalogo restano non rinominabili e non eliminabili.
- Il catalogo resta la fonte unica dell'ordine **iniziale**: un documento nuovo nasce in ordine standard, e la
  riconciliazione al boot (`AddMissingCatalogSectionsAsync`) inserisce una sezione mancante al suo posto di
  catalogo **senza** rimescolare quello che c'è.
- Nessuna migrazione: `Order` esiste da sempre.

## §5 — Rete

- `SectionOrderingTests` (7): ordine standard senza pill, lo scambio visto da entrambe le parti, le sezioni
  libere che non spostano nessuno, la sezione di catalogo assente, il profilo nullo (aeroporto), le
  sotto-sezioni fisse del `ChildRegistry`.
- `VloaDocumentViewTests`: l'ordine delle figlie decide chi viene prima; senza figlie vale l'ordine canonico.

## §6 — I blocchi (26 agosto, sera tardi)

Seguito immediato, deciso dal committente: si riordinano anche i **blocchi** della vIPI ACC — che sono le
**sezioni radice** del documento, quindi il motore è di nuovo quello e basta.

- **Le frecce stanno nell'intestazione del blocco**, dentro il `<summary>`, solo in modifica, accanto al campo
  del titolo e a «✕ Gruppo». La riga si allunga di due tasti; il gesto resta dove sta il nome della cosa che
  si sposta.
- ⚠️ **I settori di aerovia restano in testa.** Non si spostano e nessun gruppo APP ci passa sopra. La regola
  vive in `AccDocumentService.MoveGroupAsync`, che legge i blocchi con `AccDocumentAssembler` — l'Aerovia si
  riconosce dal **blockmeta**, non dal posto che occupa — e rifiuta in silenzio la mossa illecita.
- ⚠️ **Due posti apposta**: l'editor **spegne** il tasto (`CanMoveGroup`), il servizio **rifiuta** la mossa.
  Il primo è quello che si vede, il secondo tiene se qualcuno arriva per un'altra strada o se l'elenco è
  cambiato sotto mentre la pagina era ferma. Non è una duplicazione da togliere.

Rete: due test in `AccDocumentServiceTests` (l'ordine che cambia davvero; l'Aerovia che non si muove e non si
scavalca). Verifica live: l'Aerovia senza frecce, un gruppo solo con tutt'e due spente, due gruppi che si
scambiano e il passo in più che non c'è.

## §7 — Verifica live sulle TRE famiglie (26 agosto, notte)

Il primo giro aveva guidato solo la vIPI ACC: nel database di sviluppo LIBB non aveva né una vIPI APP né una
vLOA su cui provare. Creati i due documenti sulla copia del DB, la verifica copre tutte e tre le famiglie.

| Documento | Cosa si è visto |
|---|---|
| vIPI ACC `/services/vsop/libb/editor` | le sezioni «obbligatoria» hanno `↑ ↓`; *Frequenze* sopra *AOR* persiste al ricarico, `↑1` e `↓1` sulle due. I **blocchi**: l'Aerovia senza frecce, un gruppo solo con tutt'e due spente, due gruppi che si scambiano e il passo in più che non c'è |
| vIPI APP `LIBV_APP` (creata al volo) | dieci sezioni, tutte con `↑ ↓`; *Frequenze* sale sopra *AOR*, pill `↑1`/`↓1`, e **si rilegge dopo il ricarico anche fuori dalla modifica** |
| vLOA `LIBB ↔ LDZO` | nove intestazioni, comprese le **due direzioni** dei coordinamenti come sotto-sezioni con le loro frecce |

⚠️ **La prova che vale di più è l'ultima**, ed è quella che il doc 11 §8 chiedeva: spostata `LDZO → LIBB`
sopra l'altra nell'editor (pill `↑1` e `↓1`), **l'anteprima bozza del documento le ha rese nell'ordine
nuovo**. Prima di questo giro il viewer le avrebbe rese nella sequenza scritta nel codice, e l'editor avrebbe
detto una cosa che il documento pubblicato non diceva.

Nessun errore di pagina in nessuno dei tre giri.
