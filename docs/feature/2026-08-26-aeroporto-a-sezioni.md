# La vIPI d'aeroporto diventa un documento come gli altri — carta (26 agosto 2026)

> **Stato: in esecuzione**, ramo `aeroporto-a-sezioni` (da `identita-settori`).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Chiude l'ultima famiglia rimasta fuori dall'asse
> 08 → 10 → 11 → 13: il `SectionCatalog` dice ancora, in testa al file, *«L'aeroporto NON partecipa
> (documento generato a struttura propria)»*.

## La domanda

> «Voglio rivedere completamente i documenti di aeroporto per rendere la struttura uguale a quella degli
> altri documenti. Devono rimanere le stesse sezioni (METAR, TA ecc.), ma deve adottare la stessa struttura
> degli altri documenti, compreso il meccanismo di riorganizzazione.»

## §0 — Perché oggi non si può riorganizzare

Non è che manchi un tasto: **manca il documento**. Le altre tre famiglie hanno un `Document` con sezioni
stabili e ci derivano sopra il contenuto; l'aeroporto ha una **proiezione cotta**.

`EfAirportRepository.RebuildDocumentAsync` (773 righe di file, ~200 di cottura):

1. cerca le sezioni «gestite» **per titolo** — `ManagedSectionTitles` elenca cinque titoli inglesi *e* i
   loro gemelli italiani legacy, perché un documento vecchio va riconosciuto lo stesso;
2. le **cancella con i loro blocchi**;
3. le **ricrea** cuocendo tabelle Markdown dalle entità del profilo.

Tre conseguenze, tutte e tre fatali per la richiesta:

| Cosa vuole il committente | Perché oggi non regge |
|---|---|
| Spostare una sezione | `Order` è riassegnato da `++order` a ogni rebuild |
| Nascondere una sezione | `IsHidden` sta sulla sezione, che viene distrutta |
| Sotto-sezioni | Distrutte con la sezione padre |
| Live/Frozen per sezione | Salvato a mano solo per `sids`, con una variabile apposta (`sidsMode`) |
| Sezione libera in mezzo alle fisse | Le libere hanno tutte la stessa chiave `airportextra` e vivono in una tabella a parte |

⚠️ E le chiavi: `DocBuilder.Section(titolo, BlockSection.Airport, …)` chiama
`SectionCatalogBridge.KeyFor(BlockSection.Airport)`, che ritorna **null** → `SectionKeys.NewCustom()`. Quindi
«Runway rules», «Transition levels» e «Runways» oggi hanno una **chiave casuale, diversa a ogni rebuild**.
Solo «Frequencies» (`frequencies`) e «SID» (`sids`) hanno una chiave vera. È il motivo per cui il viewer
ritrova le sezioni con `s.Title.Contains("transi")`.

**Anche il viewer e l'editor sono cablati.** `AeroportoPage` rende una sequenza fissa (METAR → TA+Frequenze
affiancate → Piste → SID → extra) e `AeroportoEditorPage` un array di otto stringhe
(`_panels`) con un `<details>` scritto a mano per ciascuna. Nessuno dei due legge l'ordine del documento.

## §1 — La decisione

**L'aeroporto entra nel catalogo.** Il `Document` smette di essere una proiezione cotta e diventa quello che è
per le altre tre famiglie: un **portante di sezioni** con chiave, ordine, stato e blocchi editoriali. Il
contenuto strutturato (regole piste, TA/TL, piste, SID, frequenze, meteo) **resta nelle sue tabelle** e si
**deriva a view-time**, esattamente come `aor`/`frequencies`/`minima` sull'APP.

### §1a — Il profilo `SectionProfile.Airport`

| Chiave | Titolo | Corpo | Natura | RenderMode di nascita |
|---|---|---|---|---|
| `weather` | METAR e TAF | Host | Derived | **Live, non commutabile** |
| `runwayrules` | Regole piste | Host | Derived | Frozen |
| `transition` | Quote di transizione | Host | Derived | Frozen |
| `frequencies` | Frequenze | Host | Derived | Frozen |
| `runways` | Piste | Host | Derived | Frozen |
| `sids` | SID | Host | Derived | **Live** (come oggi) |
| `operationaltechnique` | Procedure generali | Blocchi | Editorial | — |
| `validity` | Validità e revisione | Blocchi | Editorial | — |

Le sezioni libere (oggi «Sezioni extra») diventano **sezioni `custom:{guid}` normali**, create col
«+ Aggiungi sezione» condiviso e collocabili **ovunque** nella sequenza — non più tutte in coda.

⚠️ **`aor`, `coordination` e `regulated` NON entrano**: l'aeroporto non ha un'area di responsabilità né
accordi di coordinamento propri — quelli appartengono alla torre e all'avvicinamento, che hanno documenti
loro. Questo rompe l'invariante `Universals_present_in_every_profile` dei test, che era scritta quando tutti
i profili descrivevano **posizioni di controllo**. L'aeroporto descrive un **luogo**: l'invariante si
restringe ai profili di posizione e si scrive nero su bianco cosa l'aeroporto non ha.

⚠️ **`weather` è derivata ma non congelabile.** Un METAR congelato al ciclo AIRAC non è un documento
d'archivio, è **meteo scaduto spacciato per attuale**. Serve una porta nuova nel catalogo,
`IsAlwaysLive(key)`, e `IsRenderModeToggleable` diventa *derivata **e non** sempre-live*. La cattura frozen
la salta di conseguenza.

### §1b — Bozza e lock (decisione del committente, 26 agosto)

Montare `DocumentSectionsEditor` **obbliga** al workflow bozza+lock: ogni sua mutazione passa da
`IEditingService`, che chiama `EnsureLockAsync`. La scelta di luglio («l'aeroporto resta a modifica diretta»)
cade: l'editor prende ✎Modifica / 🔒lock / pill «Bozza vN» / «Fine modifica», identici ad APP e vLOA.

**Il confine resta netto**: sotto lock c'è il **documento** (sezioni, ordine, nascondi, Live/Frozen, blocchi
editoriali). I **dati strutturati** — regole piste, TA/TL, piste, SID, link frequenze, limiti settori —
restano a salvataggio diretto ACC-gated com'erano, perché li scrivono anche i servizi d'import IVAO che
girano in background e non possono prendere un lock.

### §1c — Cosa NON è una sezione

Due pannelli dell'editor non sono contenuto del documento e restano **strumenti dell'editor**, resi fuori da
`DocumentSectionsEditor` e agganciati all'indice via `ExtraTocItems` — la stessa strada che l'editor APP usa
già per il pannello Release:

- **Settori** — catalogo dei settori ATC dello scalo: è amministrazione della struttura, non testo pubblicato;
- **Versioni** — il `ReleasePanel`, che nelle altre tre famiglie è già fuori dalle sezioni.

⚠️ L'ancora `sec-versioni` non cambia: è dove atterra «Ripubblica» dalla lista «Da fare».

## §2 — Le sezioni fisse non hanno più blocchi

Oggi il corpo delle cinque sezioni cotte **è** nei blocchi del documento (tabelle Markdown serializzate).
Dopo, i blocchi spariscono e la sezione è un'**ancora**: chiave, titolo, ordine, stato. Il corpo lo produce
la pagina, in tre modi a seconda della vista, come per l'APP:

```
pubblica    → frozen dalla release effettiva, se c'è; altrimenti live
bozza       → sempre live
anteprima release → frozen di QUELLA release
```

Il pezzo che lo fa è `IAirportViewDerivationService`, che oggi risolve **una sola** sezione (`sids`) e passa a
risolverle tutte, sul modello esatto di `AppViewDerivationService.ResolveForViewAsync`.
`AirportFrozenSectionProvider` cattura di conseguenza (oggi ha un `switch` con un solo `case`).

⚠️ **Conseguenza di semantica pubblica, voluta.** Oggi la pagina pubblica dell'aeroporto è un ibrido: TA e TL
vengono dal documento pubblicato, ma **piste, frequenze e sezioni extra si leggono dal profilo LIVE** — cioè
una modifica appare al pubblico **senza pubblicare**. È lo stesso difetto che il doc 11 §3c ha chiuso per le
sezioni nascoste. Dopo, quelle sezioni nascono `Frozen` e il pubblico vede lo stato pubblicato.
Il passaggio è morbido: gli aeroporti con una release già effettiva non hanno un payload congelato per le
chiavi nuove, quindi il reader ritorna `null` e **si ricade su live** finché non si ripubblica.

## §3 — Il ponte per i documenti già scritti

Una riconciliazione one-shot al boot in `IDocumentMaintenance` (**mai** una migrazione EF — quelle del repo
sono SQLite-flavored e il deploy hostato crea lo schema col reconciler), idempotente:

1. **Titolo → chiave di catalogo** per le sezioni cotte, che oggi hanno chiavi casuali:
   `Runway rules`/`Regole piste`/`Configurazioni pista` → `runwayrules`; `Transition levels`/`Quote di
   transizione` → `transition`; `Runways`/`Piste` → `runways`; `Frequencies`/`Frequenze` → `frequencies`
   (già giusta); `SID` → `sids` (già giusta).
2. **Svuota i blocchi** delle sezioni così riconciliate: da qui in poi il corpo lo produce la pagina, e un
   blocco rimasto sarebbe testo scritto nel DB e invisibile in ogni vista (stessa cura del doc 13 sulle due
   direzioni dei coordinamenti).
3. **`airportextra` → `custom:{guid}`**, una chiave per sezione, con i blocchi **portati dentro il
   documento** leggendoli da `AirportExtraSection.Body` (envelope `{"blocks":[…]}`, `ExtraBlocks.Parse`).
   Da qui in poi la tabella non la legge più nessuno.
4. **Aggiunge le sezioni di catalogo mancanti** (`weather`, `operationaltechnique`, `validity`) nella
   posizione prevista — riusando `AddMissingCatalogSectionsAsync`, che oggi tocca solo APP e vLOA.

⚠️ **Le release già pubblicate non si toccano** (doc 13 §9): il pubblico legge `payload.Doc`, e uno snapshot
vecchio porta ancora le sezioni cotte con le chiavi casuali. Il viewer deve reggerle: una sezione con chiave
sconosciuta e blocchi dentro si rende con `SectionNode`, che è esattamente il ramo «editoriale» — quindi uno
snapshot storico continua a mostrare le sue tabelle. **Non si perde niente e non si rende due volte.**

## §4 — Propagazione (pre-flight §4: questa modifica rimuove cose)

| Cosa sparisce | Chi lo cita | Azione |
|---|---|---|
| La cottura in `RebuildDocumentAsync` | `AirportEditingService`, `ReleasePanel.BeforePublishAsync`, test | Diventa `EnsureDocumentAsync` (crea documento + sezioni di catalogo, collega scalo e settori) |
| `ManagedSectionTitles` | `EfAirportRepository` | Rimosso: le sezioni si riconoscono per **chiave** |
| `ExtraSectionKey = "airportextra"` | repository, viewer, editor | Rimosso dopo la riconciliazione |
| `AirportExtraSection` + `ExtraSectionRow` + `SaveExtraSectionsAsync` | repo, service, editor, viewer | Rimossi: le sezioni libere sono sezioni |
| `_panels` / `Anchor()` / `PanelLabel()` / `_dirtySections` | `AeroportoEditorPage` | Rimossi: l'indice lo costruisce `DocumentSectionsEditor` |
| `SplitSections()` e i match `Title.Contains("transi")` | `AeroportoPage` | Rimossi: si dispaccia per `SectionKey` |
| «L'aeroporto NON partecipa» | commento in testa a `SectionCatalog` | Riscritto |
| «Riusato da vLOA, ACC, APP, Airport» | commento in testa a `DocumentSectionsEditor` | Diventa **vero** |

## §5 — Le fette

| # | Fetta | Chiude |
|---|---|---|
| S1 | `SectionProfile.Airport` nel catalogo + `IsAlwaysLive` + test | Il vocabolario |
| S2 | `EnsureAirportDocumentAsync`: documento + sezioni di catalogo, senza cottura | La nascita |
| S3 | Riconciliazione one-shot dei documenti già scritti | Il ponte |
| S4 | Derivazione a view-time di tutte le sezioni + cattura frozen | Il corpo |
| S5 | Viewer: itera `_view.Sections` e dispaccia per chiave | La lettura |
| S6 | Editor: `DocumentSectionsEditor` + bozza/lock + Settori/Versioni come extra | La scrittura |
| S7 | Rimozione della cottura e di `AirportExtraSection` | La propagazione |
| S8 | Suite, build su entrambi i TFM, verifica live, memorie | La chiusura |

## §6 — Verifica (pre-flight §3)

Skill `verifica-live` su copia del DB. Le prove che contano:

1. Su un aeroporto vero, **spostare** «Frequenze» sopra «Quote di transizione», ricaricare: l'ordine tiene e
   la pill dice `↑1`/`↓1`.
2. **Trascinare** una sezione nel menu Navigazione (il motore è lo stesso: `EditorToc.OnReorder`).
3. **Nascondere** una sezione e verificare che l'anteprima bozza la marchi e la pubblica non la mostri.
4. **Aggiungere una sezione libera in mezzo** alle fisse e vederla nell'anteprima bozza al suo posto.
5. Aprire una **release già pubblicata** (`?as=rel:N`) di prima della modifica: le tabelle cotte si vedono
   ancora (§3 ⚠️).
6. Il **lock**: aprire l'editor da due sessioni e vedere il chip 🔒 con il nome dell'altro.
