# La vIPI d'aeroporto diventa un documento come gli altri — carta (26 agosto 2026)

> **Stato: ✅ ESEGUITA il 26 agosto 2026**, ramo `aeroporto-a-sezioni` (da `identita-settori`, **non fuso**).
> Otto fette, undici commit, suite verde su entrambi i TFM, Release **0 avvisi**. Verifica live su LIBD in §6.
> **Nessuna migrazione**: non cambia lo schema, cambia chi scrive.
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

~~**Il confine resta netto**: sotto lock c'è il **documento** (sezioni, ordine, nascondi, Live/Frozen, blocchi
editoriali). I **dati strutturati** — regole piste, TA/TL, piste, SID, link frequenze, limiti settori —
restano a salvataggio diretto ACC-gated com'erano, perché li scrivono anche i servizi d'import IVAO che
girano in background e non possono prendere un lock.~~

⚠️ **Quel confine è caduto il 4 settembre 2026** (carta `2026-09-04-aeroporto-porta-sola.md`): la premessa era
vera — un job un lock non lo prende — ma la conclusione no, perché **i job non passano dal service
dell'editor**, passano dal repository. Ora il lock copre tutto l'editor, dati strutturati compresi, e la
guardia sta nel service (`IAirportLockGuard`), non nel bottone. Nello stesso giro sono spariti i nove tasti
«Salva»: ogni gesto scrive.

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

---

## §7 — Com'è andata

**Le otto fette sono state eseguite nell'ordine della tabella.** Quel che la carta non prevedeva:

- **La `switch` con la marcatura dentro non compila.** I sei editor strutturati non si possono mettere nei
  rami di una `switch` dentro `@code`: il parser Razor conta le graffe del blocco, e un `@{ … }` dentro un
  `case` gliele sbilancia (`RZ1006: the switch block is missing a closing "}"`). Un **frammento per sezione**
  e una `switch` di sola **espressione**: ogni frammento comincia con un elemento, quindi è marcatura dal
  primo carattere.
- **Il titolo restava a metà in inglese.** La riconciliazione rinominava la sezione solo quando le cambiava
  anche la chiave — e `frequencies`/`sids` la chiave giusta ce l'avevano già. Trovato **a schermo**, non dai
  test: «Frequencies» accanto a «Regole piste». Il titolo di una sezione fissa lo decide il catalogo,
  sempre: tanto `IsMandatory` ne vieta la rinomina a mano.
- **E i blocchi restavano a metà.** Stessa causa: svuotare i blocchi solo delle rinominate lasciava dentro la
  tabella cotta di «Frequencies», che avrebbe **raddoppiato** quella derivata.
- **Ogni sezione mostrava il titolo due volte**, a due righe di distanza: quello dell'intestazione e l'`h2`
  che il pannello si portava dietro da quando era un `<details>` scritto a mano.
- **`IsSectionDirty`**, parametro opt-in nuovo su `DocumentSectionsEditor`: il pallino «non salvato» nel menu
  è dell'aeroporto soltanto, l'unico editor in cui una sezione ha un «Salva» proprio e può restare sospesa.

**La verifica live ha dato la prova che i test non danno** (§6 punto 5, ampliato): pubblicata la release e poi
cambiato il **TORA** di una pista nel profilo, la pagina **pubblica** continua a dire `3000` e la **bozza**
dice il valore nuovo. Cioè: la release congela davvero le sezioni derivate — cosa che prima non poteva fare,
perché il contenuto era cotto nei blocchi e lo snapshot se lo portava dietro senza congelare niente.

E i **cinque Remarks** di LIBD sono passati dalla tabella al documento senza perdere un callout: il trasloco
regge sui dati veri.

---

## §8 — Il difetto che la carta aveva creato: il meteo sparito dal pubblico

Segnalato dal committente subito dopo l'esecuzione: **la sezione METAR/TAF c'era nell'editor ma non nella
pagina pubblica**.

**Perché.** La pagina pubblica non legge il documento di lavoro: legge lo **snapshot di release effettiva**. E
quello snapshot, per ogni aeroporto non ancora ripubblicato, è stato scritto **prima** di questa carta — quindi
non ha una sezione `weather`, e non ce l'avrà mai, perché le release pubblicate non si riscrivono (doc 13 §9).
Prima della carta il riquadro METAR/TAF lo disegnava la **pagina**, fuori dal documento: c'era sempre, per
costruzione. Diventando una sezione, ha cominciato a dipendere da un documento che non la conosce.

Misurato sull'archivio di sviluppo, guardando i payload delle release:

| Scalo | Sezioni nella release effettiva |
|---|---|
| LIBC, LIBD, LIRN, LIPA | `custom«Transition levels»`, `frequencies«Frequencies»`, `custom«Runways»`, `sids«SID»`, `airportextra«Remarks»` — **niente meteo** |
| LIBR | `weather`, `runwayrules`, `transition`, … — era stato **ripubblicato** |

⚠️ E c'era un secondo danno, più silenzioso: in quegli snapshot `transition` e `runways` hanno **chiavi
casuali**, quindi non venivano riconosciute come sezioni di catalogo e finivano rese come **tabelle generiche**
— la pagina pubblica perdeva la tabella dei livelli con la fascia QNH accesa e quella delle piste con 🛫🛬.

**La regola, e dove sta.** `AirportLegacySections` (puro, in Application) tiene **una sola** mappa
titolo→chiave, con **due lettori**: la riconciliazione d'avvio, che riscrive i documenti di lavoro una volta
per tutte, e il **viewer**, che deve leggere anche gli snapshot — e quelli non si riscrivono mai. Due copie
della stessa mappa sarebbero state due verità sullo stesso archivio.

`ForView` fa tre cose sulle sezioni del documento mostrato:

1. riporta le sezioni **cotte** alla loro chiave e al **titolo di catalogo** (anche quando la chiave era già
   giusta: `frequencies` e `sids` non passano dal riconoscimento per titolo, e senza questo il documento
   resterebbe metà in italiano e metà in inglese);
2. **toglie i blocchi** alle sezioni ora rese dalla pagina, o si vedrebbe la tabella due volte;
3. aggiunge le sezioni **sempre live** che mancano. ⚠️ La regola generale: *una sezione sempre live non è mai
   parte della verità di uno snapshot* — non si congela, non ha contenuto salvato, e la sua assenza da un
   documento vecchio dice solo che quel documento è stato scritto prima che esistesse.

Cade con questo il ramo `IsCooked` del §5: non serve più distinguere lo snapshot cotto, perché le sue sezioni
vengono ricondotte alle chiavi vere e rese dai componenti. Sul contenuto non si perde nulla — per quelle chiavi
una release anteriore non ha un payload congelato, quindi la derivazione ricade su **live**, che è esattamente
ciò che la pagina faceva prima (piste, frequenze ed extra li leggeva **dal profilo**).

## §9 — «Identica a com'era prima»: il confronto, e le tre differenze rimaste

Costruito il codice **pre-carta** in un worktree su `identita-settori` e messo in piedi su una copia del DB
**pre-migrazione** (porta 5035), accanto al codice nuovo sul DB migrato (5034). Stessa pagina, stesso browser,
stesso momento: `/services/vsop/libb/airports?icao=LIBD`.

| | prima | dopo |
|---|---|---|
| intestazione meteo | `🌦️ METAR & TAF · LIBD (live · NOAA)` | **identica** |
| chip METAR (vento, visibilità, nuvole, QNH, temp) | 5 | **identici** |
| frequenze | 6 righe, `★ Bari Tower` | **identiche** |
| livelli di transizione | 4 fasce, riga del QNH accesa | **identiche** |
| piste | 2 righe con 🛫🛬 | **identiche** |
| SID | 19 righe | **19** |
| tabelle generiche | 0 | **0** |

Il titolo di catalogo del meteo è tornato **«METAR & TAF»** (era «METAR e TAF»), e l'intestazione della sezione
porta di nuovo emoji, ICAO e la nota «live · NOAA» — non sono decorazione: dicono **di chi** è il tempo che si
sta leggendo e che è vivo, ed è quel che quella sezione ha da dire prima di essere aperta.

**Le tre differenze che restano, tutte volute:**

1. **Le due colonne affiancate non ci sono più.** «Quote di transizione» e «Frequenze» stavano in una griglia a
   due colonne (`.apt-2col`); ora sono impilate come tutte le altre. È il prezzo del riordino: una griglia di
   due sezioni fisse non si può riordinare, e la richiesta era proprio poterle spostare. Si può rimettere, ma
   solo rinunciando a spostare quelle due.
2. **I titoli sono in italiano** («Quote di transizione», «Frequenze», «Piste») invece che in inglese. Li dà il
   catalogo, come per le altre tre famiglie, e il documento è `Language.It`: erano inglesi solo perché la
   cottura li scriveva così.
3. **I callout dei Remarks portano l'etichetta «Nota».** Prima gli extra passavano da un renderer tutto loro;
   ora passano da quello condiviso, che etichetta i callout senza titolo come in ogni altro documento.

E due spaziature che Razor si mangiava davanti a un blocco di codice, corrette: «FL60current QNH» e
«★Bari Tower».

---

## §10 — I pannelli che non sono sezioni erano larghi quanto la PAGINA

Segnalato dal committente: «ATC sectors» e «Versions & AIRAC releases» più larghi delle sezioni.

**Perché.** Quando `DocumentSectionsEditor` monta il suo indice (`ShowToc`), è **lui** a possedere la griglia
`.ed-layout` — tre colonne, `indice | contenuto | rail`. Un pannello reso **dopo** `</DocumentSectionsEditor>`
finisce quindi **fuori** dalla griglia: largo quanto il `.wrap`, non quanto la colonna centrale.

⚠️ E non era solo l'aeroporto. Verificato su tutti e quattro gli editor: **l'unico a farlo giusto era l'ACC**,
proprio perché la griglia se la costruisce da sé e teneva il `ReleasePanel` dentro la colonna centrale, fra
l'indice e il rail. I tre che montano il componente condiviso — aeroporto, APP e vLOA — lo rendevano tutti
fuori, e mostravano il pannello Release più largo del resto del documento.

**La correzione**: un parametro nuovo, `DocumentSectionsEditor.AfterSections` — pannelli dell'host resi dopo
le sezioni ma **dentro la colonna centrale**. Ci finiscono il `ReleasePanel` dei tre editor e, sull'aeroporto,
anche il catalogo dei settori ATC. Nessun CSS: la larghezza viene dalla griglia, quindi è giusta per
costruzione anche quando la griglia cambia.

**Misurato** con `getBoundingClientRect().width`, sezione contro pannelli:

| | sezione | Settori ATC | Release |
|---|---|---|---|
| aeroporto, 1600px | 996 | **996** | **996** |
| APP / ACC / vLOA, 1600px | 996 | — | **996** |
| aeroporto, ⤢ larghezza piena | 1536 | **1536** | **1536** |
| aeroporto, 1000px (griglia collassata) | 960 | **960** | **960** |

⚠️ **Non c'è un test che lo protegga.** È una relazione di ANNIDAMENTO nel DOM — «il pannello è discendente
della colonna centrale» — e coprirla con bUnit vorrebbe dire impersonare l'intero `IEditingService`. Resta la
misura live, e il commento sul parametro che dice perché esiste.

---

## §11 — «Validità e revisione» porta tre campi che nessuno ricopia

Richiesta del committente: la sezione deve avere **tre campi fissi** — ciclo AIRAC di appartenenza, data, e chi
ha rivisto il documento con **nome, posizione staff e VID** di chi ha premuto Pubblica — **in tutti i documenti**.

**Erano già scritti a mano, e non si aggiornavano.** In archivio le sezioni `validity` contengono otto tabelle
`Item | Value` compilate a mano: «Effective from → AIRAC 2607», «Review cycle → Bilateral, at least annually»,
«Italian signatory → LIBB CH / AOD». La prima riga è un fatto che invecchia da solo; le altre due sono contenuto
d'accordo che nessuno può derivare. Decisione del committente: la scheda **si aggiunge sopra**, il testo resta.

### La sezione con due corpi

`SectionBodySource` guadagna un terzo valore, **`HostAndBlocks`**: la pagina disegna una scheda in testa e sotto
restano i `ContentBlock` della sezione. È l'unica sezione del catalogo ad averli entrambi, e un test lo presidia
— chi ne aggiungesse un'altra lo starebbe decidendo, non ereditando. Nuova porta `SectionCatalog.KeepsOwnBlocks`.

### Il timbro viene dalla release, e per questo è sempre-live

⚠️ `validity` entra in `AlwaysLiveKeys` per una ragione di **ordine**, non di gusto: il suo timbro parla della
release, e la cattura frozen gira **dentro** la creazione dello snapshot — quando quella release non esiste
ancora. Non c'è niente da congelare: si legge sempre dalla release che si sta mostrando.

Conseguenza gradita: la regola del §8 — *una sezione sempre-live non è mai parte della verità di uno snapshot* —
la inietta anche nei documenti d'aeroporto pubblicati prima, senza una riga in più.

| Pezzo | Dove |
|---|---|
| Quale release si legge | `ValidityRelease.Pick` — **pura**: è la parte che sbaglia più facilmente |
| Il timbro | `DocumentValidityService` (release + roster staff) |
| La scheda | `ValidityStamp.razor`, uno per tutte e quattro le famiglie |
| Nome e posizioni per VID | `IStaffRosterRepository.FindAsync`, **anche per i disattivati** |

⚠️ **Chi ha firmato una release resta il revisore di quella release**, anche dopo aver lasciato lo staff:
`FindAsync` non filtra su `IsActive`, o cancellarne il nome riscriverebbe la storia.

⚠️ **Un'anteprima che punta a una release cancellata NON ricade sull'effettiva**: mostra «non pubblicato».
Ricadere direbbe al lettore, sotto l'intestazione «stai guardando la release #N», il ciclo e il firmatario di
un'**altra** release.

⚠️ **VID `0` non è una persona.** In archivio ci sono tre vLOA pubblicate senza utente registrato: la scheda
dice «non registrato» invece di stampare uno zero che qualcuno proverebbe a cercare.

⚠️ **Il nome del roster porta già il VID dentro** («Carmine (704798)»): lo scrive il login, perché nell'elenco
dei permessi due omonimi vanno distinti. Qui il VID lo aggiunge il link, e senza pulizia si leggeva
«Carmine (704798) (VID 704798)» — trovato a schermo, chiuso con `CleanName` e sette casi di prova.

### Verifica live, quattro documenti

| Documento | Scheda | Testo sotto |
|---|---|---|
| aeroporto LIBD | `2607 · 29 Jul 2026 · 22:22Z · Carmine (VID 704798) · IT-AOA1 · IT-T03` | — |
| APP LIBA | `2608 · 23 Aug 2026 · 23:44Z · non registrato` | — |
| vIPI ACC LIBB | una **per blocco**, il timbro è per `ACC\|root` | — |
| vLOA LDZO | `2608 · 12 Aug 2026 · 13:56Z · Carmine (VID 704798) · IT-AOA1 · IT-T03` | ✅ la tabella scritta a mano |
| editor aeroporto | scheda + nota «questi tre campi li scrive il documento» | — |

### La scheda è larga quanto la sezione

Era nata sulle classi di `.tl-table` e con loro si portava dietro il **tetto di 420px** — che per la tabella dei
livelli di transizione ha senso (due colonne di numeri) e qui no: la colonna del valore porta un nome, le
posizioni staff e un link, e a 420px andava a capo mentre metà della card restava vuota. Ora usa `.res-table`,
che è la tabella pensata per essere piena.

⚠️ La colonna delle etichette è fissata in `ch`, non in percentuale: le tre etichette sono le stesse in ogni
documento, e senza un valore fisso la tabella si sarebbe ridisegnata larga diversa a seconda di quanto è lungo
il nome del revisore. Sotto i 560px il vincolo cade e le colonne tornano automatiche.

Misurato: scheda **822px = corpo della sezione** su vLOA, aeroporto e APP; **946** nell'editor; allineata alla
tabella scritta a mano che le sta sotto.

**Due cose da sapere, e nessuna delle due è un difetto:**

1. **Sulla vIPI ACC di Brindisi la sezione non si vede in pubblico**: `validity` e `operationaltechnique` sono
   **nascoste a mano** (`IsHidden = true`) su entrambi i blocchi. Sono le sole due in tutto l'archivio — le altre
   otto vIPI e quattro vLOA le hanno visibili. Si riaprono dall'editor con un clic; non l'ho fatto io perché
   nascondere una sezione è una scelta editoriale registrata.
2. **Sui documenti che avevano già la tabella a mano il ciclo AIRAC compare due volte** — una generata e una
   scritta. È la conseguenza annunciata della scelta «si aggiunge sopra»: si toglie cancellando quella riga.
