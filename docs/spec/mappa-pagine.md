# MAPPA PAGINE — servizi ATC (hub `/services`)

> Documento di **rapida lettura**: la gerarchia delle pagine del sito e dove vive ciascuna.
> Aggiornato al **25 agosto 2026** (statistiche ATC, il terzo servizio). Al **22 agosto**: il sito è il
> contenitore dei **servizi**, la documentazione operativa è il primo di essi e vive sotto `/services/vsop`;
> le rotte rimaste in italiano sono passate all'inglese.
> Tabella di conversione e ragioni in
> [`../feature/2026-08-22-servizi-atc-e-profile-swapper.md`](../feature/2026-08-22-servizi-atc-e-profile-swapper.md) §2.
> (Prima di allora: **rebuild Round 12**, che rinominò `/sop` → `/vsop` e rifece la struttura ACC.)
> Per le pagine spente vedi **`pagine-disabilitate.md`**.

## Gerarchia (cosa vede l'utente)

```
/                                       → 301 a /services
/services                               Hub dei servizi  ............. ServicesHome.razor
├─ vSOP — documentazione operativa                 → /services/vsop
├─ Aurora Profile Swapper                          → /services/profile-swapper
└─ Statistiche ATC                                 → /services/stats

/services/profile-swapper               Copia sezioni fra profili Aurora .cpr  ... ProfileSwapperPage.razor

/services/stats                         Le mie statistiche ATC  ............... StatsHome.razor
│                                       (?p=30|90|365|all — il periodo sta nell'INDIRIZZO: la pagina
│                                        resta SSR statica e un periodo si puo' mandare a qualcuno)
├─ riga di un turno                              → /services/stats/session/{id}
└─ Statistiche di divisione                      → /services/stats/division
                                                   (solo staff, o tutti i loggati se lo staff
                                                    ha acceso la classifica pubblica)

/services/stats/session/{id}            Dettaglio di un turno  ................ StatsSessionPage.razor
│                                       striscia del turno, targhette per volo, quote, consegne
└─ filtro traffico (?f=dep|arr|ovf|air)          → stessa pagina

/services/stats/division                Copertura e classifica  ............... StatsDivisionPage.razor
                                        (unica delle tre INTERATTIVA: lo staff accende e spegne
                                         la classifica pubblica da qui)

/services/vsop                                   Home  ........................ SopHome.razor
├─ Ricerca: titolo + AIRAC + barra
├─ Card ACC (codice, nome, n° ATC online)        → /services/vsop/{acc}
├─ Navigazione rapida: "Cosa è cambiato"         → /services/vsop/changed
└─ [staff/editori] Documenti · Bozze&Versioni (= hub editor unificato, tasto «Nuovo documento») · Tutte le schermate · Struttura · Permessi

/services/vsop/{acc}                             Landing ACC  ................. AccLanding.razor
├─ Documenti:
│  ├─ vIPI di ACC                                  → /services/vsop/{acc}/vipi
│  ├─ Card Aeroporti (3 in evidenza)
│  │     titolo → /services/vsop/{acc}/airports            (elenco)
│  │     voce   → /services/vsop/{acc}/airports?icao=XXXX  (documento)
│  ├─ Card APP non remot. (3 in evidenza)
│  │     titolo → /services/vsop/{acc}/apps                (elenco)
│  │     voce   → /services/vsop/{acc}/apps/vipi?app=XXX_APP (documento · solo APP NON remotizzati · editor: /apps/editor?app=)
│  └─ Card vLOA (3 in evidenza)
│        titolo → /services/vsop/{acc}/vloa                (elenco)
│        voce   → /services/vsop/{acc}/vloa?acc=YYYY        (documento · vicino YYYY · editor: /vloa/editor?acc=)
└─ [Admin CH/AOD] Editor vIPI · Editor vLOA  (Trasferimenti → /services/vsop/admin/transfers · Gerarchia → /services/vsop/admin/sector-structure)

/services/vsop/live[/{callsign}]   Vista live, per callsign  ................ LivePage.razor
/services/vsop/{acc}/vipi          vIPI ACC (data-driven, una per ACC)  ..... AccVipiPage.razor
/services/vsop/{acc}/airports      Elenco aeroporti (no ?icao)  ............. AeroportoPage.razor
/services/vsop/{acc}/airports?icao=XXXX   vIPI aeroporto (con ?icao)  ....... AeroportoPage.razor
/services/vsop/{acc}/apps          Elenco APP non remotizzati  ............. AppsListPage.razor
/services/vsop/{acc}/apps/vipi?app=XXX_APP  Documento APP non remot. (data-driven)  AppnPage.razor
/services/vsop/{acc}/apps/editor?app=XXX_APP  Editor dedicato APP non remot.  AppEditorPage.razor
/services/vsop/{acc}/vloa          Elenco vLOA (no ?acc)  ................. VloaListPage.razor
/services/vsop/{acc}/vloa?acc=YYYY Documento vLOA (vicino YYYY)  .......... VloaListPage.razor
/services/vsop/{acc}/vloa/editor?acc=YYYY  Editor vLOA (coppia acc↔YYYY)  . VloaEditorPage.razor
```

> ⚠️ **Aeroporti = una sola rotta** `/services/vsop/{acc}/airports`: senza `?icao=` mostra l'elenco,
> con `?icao=` il documento (Blazor non distingue per query string → una pagina che ramifica).
> Gli APP invece hanno due path distinti (`/apps` elenco, `/apps/vipi` documento).

## Tabella rotte (attive)

| Rotta | File | Ruolo | Accesso |
|---|---|---|---|
| `/services` | `ServicesHome.razor` | **Hub dei servizi** (SSR statico: è un elenco di collegamenti). Vive nella RCL e non nell'host, perché quando il modulo sarà montato dentro Ivao.It `/` non sarà nostro | tutti |
| `/services/profile-swapper` | `ProfileSwapperPage.razor` | **Aurora Profile Swapper**: copia sezioni intere fra profili `.cpr`. Motore in `Vipi.AuroraProfiles`. I file passano dal server ed elaborati in memoria — la pagina lo dichiara | tutti |
| `/services/stats` | `StatsHome.razor` | **Le mie statistiche ATC** (SSR statico). Ore, turni, movimenti, presenze, grafici, elenco dei **turni** (non delle connessioni). ⚠️ Dal 25 agosto 2026 «quando controlli» sono **due grafici** (a che ora · che giorno), non più la griglia 7×24: quella è rimasta alla divisione, dove l'incrocio serve. Il periodo sta nell'indirizzo (`?p=30|90|365|all`): è ciò che tiene la pagina statica invece di darle un circuito per un elenco di numeri fermi | chi ha fatto login (vede **solo i propri**) |
| `/services/stats/session/{id}` | `StatsSessionPage.razor` | **Dettaglio di un turno**: striscia del turno, targhette per volo (in partenza/in arrivo/sorvolo · decollato · atterrato · al parcheggio · consegnato a X · uscito in volo · solo rullaggio · fermo), quote e consegne. Filtro `?f=dep|arr|ovf|air`. ⚠️ La regola delle targhette sta in `TrafficStory`, non nel markup: **si dice quel che si è visto** | il proprietario; **lo staff vede tutte** |
| `/services/stats/division` | `StatsDivisionPage.razor` | **Copertura e classifica di divisione**: griglia ora×giorno, andamento per mese, classifica. L'unica delle tre **interattiva**: lo staff accende e spegne la classifica pubblica da qui. ⚠️ Default **spento** — esporre nome e ore degli altri è una scelta politica, non un default di colonna. ⚠️ **Tre pezzi dentro hanno una guardia PROPRIA** (`Authz.IsAdmin`, prima delle query, non davanti al markup), perché la pagina la aprono anche i soci a classifica accesa: la **ricerca per VID**, la **lente** verso le statistiche di un altro e la sezione **Aeroporti** (`?g={ACC}`) col traffico di ogni campo e quanto ne è stato coperto | staff sempre; tutti i loggati se la classifica è pubblica |
| `/services/vsop` | `SopHome.razor` | Home | tutti |
| `/services/vsop/{acc}` | `AccLanding.razor` | Landing ACC | tutti |
| `/services/vsop/{acc}/vipi` | `AccVipiPage.razor` | vIPI ACC (data-driven). **Una sola per ACC**: la pagina usa sempre il CTR radice primario e **ignora** `?tree=` (nessun selettore d'albero) | tutti (edit: AOD/DIR) |
| `/services/vsop/{acc}/airports` | `AeroportoPage.razor` | Elenco + doc aeroporto | tutti |
| `/services/vsop/{acc}/apps` | `AppsListPage.razor` | Elenco APP non remot. | tutti |
| `/services/vsop/{acc}/apps/vipi` | `AppnPage.razor` | Documento APP non remot. (`?app=CALLSIGN`, **data-driven** dal profilo; tasto **✎ Editor** se autorizzato). Solo `ApproachKind.Standalone`: su un callsign remotizzato non esiste documento (doc 11 §3e). Il vecchio ramo `?vloa=` (seconda rotta della vLOA) è **rimosso**: la vLOA ha una rotta sola | tutti (edit: AOD/DIR) |
| `/services/vsop/{acc}/apps/editor` | `AppEditorPage.razor` | **Editor dedicato APP non remotizzato** (`?app=CALLSIGN`): WYSIWYG, sezioni fisse dal `SectionCatalog` (profilo App: Separazioni · Configurazioni · AOR · Frequenze · Minime · VFR · Coordinamenti · Aree regolamentate · Procedure generali · Validità) + sezioni libere, riordino drag-and-drop + tasti, nascondi sezioni. Freq/coord/AOR **derivate live**. I doc APPn instradano qui (non all'editor generico/aeroporto) via `DocumentSummary.IsStandaloneApp` | admin/grant ACC |
| `/services/vsop/live` · `/services/vsop/live/{callsign}` | `LivePage.razor` | **Vista live UNICA per callsign** (doc [refactor/12](../refactor/12-vista-live-unificata.md)). Senza callsign = la postazione con cui sei connesso su IVAO, seguita live; **nessun selettore**. Con callsign = quella postazione in sola consultazione (banner + ritorno alla propria). Non connesso ⇒ stato d'attesa con gli ATC online cliccabili, aggancio automatico al tick SSE. Resa per tipo di ente dai descrittori `ILiveStationKind` (area · avvicinamento · aeroporto): CTR/FSS, APP standalone e remotizzati, **TWR/ITWR/GND/DEL**. Ex `/services/vsop/{acc}/operativa`, `/services/vsop/{acc}/live`, `/services/vsop/{acc}/operativa-app`, `/services/vsop/{acc}/live-app` — tutte **301 a un salto solo** | tutti |
| `/services/vsop/{acc}/vloa` | `VloaListPage.razor` | Elenco vLOA della ACC (no `?acc`); con `?acc=YYYY` mostra il **documento** della coppia acc↔YYYY (una rotta che ramifica per query, come aeroporti). Chiave = **codice ACC vicino** (`VloaRow.NeighbourCode`), non più docId | tutti (edit: AOD/DIR) |
| `/services/vsop/changed` | `ChangedPage.razor` | Cosa è cambiato | tutti |
| `/services/vsop/search` | `SearchPage.razor` | Ricerca full-text. Filtri per tipo: Tutti · vIPI (ACC) · **APP** · vLOA · Aeroporti. Indicizza solo ciò che è pubblico — documento non nascosto, **release AIRAC effettiva**, sezioni non nascoste (doc 13 §3f) — e i deep-link usano l'ancora `#s-{id}`, uguale in tutte le famiglie | tutti |
| `/services/vsop/guide` | `GuidaPage.razor` | **Guida in-app bilingue IT/EN** (toggle `?lang=it\|en`, default = cultura negoziata; contenuto data-driven, TOC derivato) — consultare + modificare: pagina statica cercabile (Ctrl+F) a sezioni collassabili con ancore deep-link (`#editor-release`, `#editor-lock`, …). Vi rimandano i `?` contestuali (`Components/HelpHint.razor`) agganciati ai controlli editor (ReleasePanel, EditLockBar, DocumentBlocksEditor, link Anteprima, Salva-tutto). Link nella topbar (icona `help-circle`, tutti). Le sezioni emergono anche nella **ricerca globale** (`GuideSearchCatalog` → `SearchService`, solo scope Tutti). **Tour onboarding** editor: `wwwroot/vipi-tour.js` (step su `data-tour`, auto 1×/utente, `?tour=1` per rivederlo) | tutti |
| `/services/vsop/screens` | `ScreensIndex.razor` | Indice schermate | staff |
| `/services/vsop/release/{id}` | `ReleasePreviewPage.razor` | **Redirect** (compat): risolve la release e reindirizza al viewer tipizzato con `?as=rel:{id}`. Le anteprime sono rese dentro i viewer, non più qui | staff/editori |
| `/services/vsop/versions`, `/services/vsop/{acc}/versions` | `VersioniPage.razor` | **Hub documenti unificato** (ex `/vsop/editor` assorbito): elenco completo doc (vIPI ACC/APP/aeroporto/vLOA) + ricerca/filtri, «Apri editor» per riga, tasto «Nuovo documento», storico versioni + release AIRAC. Azioni hide/elimina/annulla-release solo admin. `Apri editor` gated server-side | staff/editori (admin o grant) |
| `/services/vsop/editor/new-document` | `NewDocumentPage.razor` | Creazione documenti. **vIPI ACC**: si scelgono i **root** degli alberi (ogni root porta lo scope dell'intero sottoalbero d'area = CTR + APP di ACC, **cross-ACC**; più alberi per doc). **vIPI APP**: solo APP non remotizzati (`App`+`Standalone`). **vLOA**: solo tra ACC **italiano** (Home) e **estero** (Neighbour), es. Roma↔Marsiglia. Lavora su una vista globale dei settori (`IStructureEditingService.ListSectorNodesAsync`) | admin |
| `/services/vsop/{acc}/editor` | `AccEditorPage.razor` | Editor vIPI ACC (data-driven, a **blocchi**: Aerovia + gruppi APP) | admin/grant ACC |
| `/services/vsop/{acc}/vloa/editor` | `VloaEditorPage.razor` | Editor vLOA (con `ReleasePanel`, come ACC/APP/aeroporto — doc 11 §3f). Con `?acc=YYYY` apre la coppia acc↔YYYY (host del componente `VloaEditor`); senza `?acc` mostra un **chooser** delle vLOA della ACC. Ex `/services/vsop/{acc}/editor-vloa` (stub) ed ex host `apps/editor?vloa=` (rimossi) | admin/grant ACC |
| `/services/vsop/{acc}/airports/editor` | `AeroportoEditorPage.razor` | Editor della vIPI d'aeroporto: **sezioni del documento** (riordino, nascondi, sotto-sezioni, sezioni libere) col motore condiviso `DocumentSectionsEditor`, **bozza + lock** come le altre tre famiglie; fuori dalle sezioni i due pannelli che non sono contenuto — settori ATC importati (mostra/nascondi + limiti) e release. Carta 2026-08-26 | admin/grant ACC |
| `/services/vsop/admin/acc` | `AccAdminPage.razor` | ACC + settori ATC: import da sorgente (`/v2/centers` + `/subcenters`, auto giornaliero), militare, mostra/nascondi, limiti quota admin | admin |
| `/services/vsop/admin/sector-structure` | `StrutturaPage.razor` | **Gerarchia di copertura GLOBALE (cross-ACC)** per callsign sui settori importati (§9.12 round 20): UI a **card per ACC** (ogni card = gli alberi con radice in quell'ACC, comprimi/espandi card e rami + ricerca) + pannello dettaglio sticky con catena di fallback, picker padre e **Applica**. **Solo gerarchia**: niente selettore ACC; la creazione documenti è su `/services/vsop/editor/new-document`. Nodi: settori ACC, **tutte le posizioni d'aeroporto** (APP · TWR · GND · DEL; ATIS escluso) e aeroporti. Le posizioni senza padre scritto mostrano quello **ereditato** dalla scaletta DEL→GND→TWR→APP (interruttore «Posizioni d'aeroporto», spento di default); un padre più in basso nella scaletta è rifiutato. Creazione/eliminazione/frequenza settori NON qui (solo pagina ACC). Ex `/admin/struttura`, redirect 301 | admin |
| `/services/vsop/admin/airports` | `AeroportiPage.razor` | Gestione aeroporti (filtro per ACC; colonna **Stato** + mostra/nascondi; alias legacy `/services/vsop/admin/airports`) | admin |
| `/services/vsop/admin/permissions` | `AdminGrantsPage.razor` | Permessi editing (+ card «Trasferimenti» in Dashboard) | admin |
| `/services/vsop/admin/transfers` | `AdminTrasferimentiPage.razor` + `XferNavigator` / `XferRowsTable` | Trasferimenti tra settori, **tre colonne**: navigatore **Settore ▸ Aeroporto ▸ gruppo** · riquadro di lavoro (il gruppo scelto) · pannello riga. Ogni colonna scorre per conto proprio. Interruttore **Albero ⇄ Elenco**: l'elenco mostra tutte le righe dell'ACC con settore/aeroporto/tipo come colonne (è il modo di rivedere). CoP, livello e ricevente si scrivono **in cella** (Invio scende, Tab passa, Esc annulla); condizione e faccetta solo nel pannello. Stato in URL — `?acc=&vista=&gruppo=&riga=&q=&tipo=&rev=&norx=` — quindi un gruppo si può linkare e un F5 non azzera. In elenco **ordinano le intestazioni** (la tendina resta all'albero); modifica **in blocco** su ricevente, livello, condizione ed eliminazione; **annulla** dopo un'eliminazione, che rimette anche l'outline e vive quanto il messaggio che lo propone. I campi con elenco a digitazione sono un componente solo (`TypeaheadPicker`), con frecce/Invio/Esc. Risoluzione live in Ridotta risale la gerarchia (terminale UNICOM). Ex `/services/vsop/{acc}/editor-trasferimenti`. Link da Struttura e card in Dashboard permessi | admin |
| `/services/vsop/admin/sources` | `SorgentiAdminPage.razor` | Policy import sorgenti | admin |
| `/services/vsop/admin/audit` | `AuditPage.razor` | Audit log | admin |

## Note tecniche
- **Prefisso:** tutte le rotte sono sotto `/services/vsop`. I vecchi URL `/sop*` fanno **redirect 301** a `/services/vsop*`
  (preservando la query string) — middleware in `src/Vipi.Host/Program.cs`. Stesso middleware:
  le quattro rotte storiche della vista operativa/live per-ACC → `/services/vsop/live[/{callsign}]`, un salto solo.
  ⚠️ `/services/vsop/live/{callsign}` è a parametro e ricade sul prefisso dello stream SSE `/vsop/live/atc`: vince il
  segmento **letterale** (precedenza del routing), verificato da `SmokeTests.Sse_endpoint_wins_over_the_live_page_route`.
- **"3 in evidenza":** campo `FeaturedRank` (1..3) su `Airport` e `Sector` (`Domain/Entities/Anagrafica.cs`,
  migrazione `AddFeaturedRank`) e su `Document` per le vLOA (`Documents.cs`, migrazione `AddVloaFeaturedRank`).
  Si imposta dall'**Editor vIPI ACC** (pannello "In evidenza nella landing ACC", colonne Aeroporti/APP/vLOA).
  Senza selezione, la landing mostra i primi 3 per ICAO/callsign/titolo.
- **Conteggio ATC online in Home:** one-shot, conta i callsign il cui primo token = codice ACC
  (es. `LIRR_NE_CTR`). Le torri/APP degli aeroporti non sono ancora contate (follow-up).
- **Aeroporti nascosti:** `Airport.IsHidden` (toggle in `/services/vsop/admin/airports`) rende la pagina
  `/services/vsop/{acc}/airports?icao=` inaccessibile al pubblico e toglie l'aeroporto dagli elenchi/landing.
  Gli aeroporti **senza alcun settore** sono nascosti di default (`IsPublic = !IsHidden && Sectors>0`).
- **Editor:** non rivisti in questo round ("poi ragioniamo sugli editor"). Restano raggiungibili
  dalla sezione Admin della landing ACC, invariati.
- **Lock editing admin (2026-07):** le 4 pagine di struttura (`/services/vsop/admin/sector-structure`, `/services/vsop/admin/acc`,
  `/services/vsop/admin/transfers`, `/services/vsop/admin/airports`) condividono **un** lock esclusivo (`admin:structure`), e
  `/services/vsop/editor/new-document` ne ha uno separato: una persona alla volta modifica. Barra `Components/EditLockBar.razor`
  («Inizia/Fine modifica» + banner + «Forza sblocco» admin). Read-only finché non si prende il lock; TTL 3min +
  heartbeat 60s (chiusa la scheda si libera da sé). Storage `EditResourceLock`. Vedi memoria `edit-resource-lock-design`.
- **Anteprime (`?as=`, round 33):** i 4 viewer documentali (`AccVipiPage`, `AeroportoPage`, `AppnPage`,
  `VloaListPage`) accettano un parametro uniforme `as`: assente → **pubblica** (release effettiva al ciclo
  corrente, altrimenti live); `?as=draft` → **bozza live**; `?as=rel:{releaseId}` → **snapshot congelato**
  di una release. Bozza/release sono **gated** al permesso di modifica dell'ACC; per un utente non autorizzato,
  identità release non corrispondente o URL forgiato la vista **degrada a pubblica** (nessun banner, nessuna
  fuga di bozza). Banner condiviso `Components/PreviewBanner.razor`; parsing `Shared/PreviewMode.cs` (alias
  legacy `?live=1` → `draft`). I tasti «Anteprima» degli editor puntano a `?as=draft`; «👁 Anteprima» per-release
  a `?as=rel:{id}`. La vecchia pagina `/services/vsop/release/{id}` è ora un redirect.

## Nota doc 11 (uniformità dei tre documenti, 2026-07-30)

- **Sezioni nascoste:** stato **versionato** su `DocumentSection.IsHidden` (gemello di `RenderMode`), non più in
  `AccBlockMeta`/`DocumentProfile`. I tre viewer si comportano allo stesso modo: omessa in pubblica/release, resa
  con pill «nascosta» in anteprima bozza. Migrazione dati one-shot al boot (`ReconcileVipiDocuments`).
- **Sezioni libere:** chiave `custom:{guid8}` univoca per sezione (prima la costante `"custom"` le faceva collidere).
- **Contenuto editoriale:** reso ovunque da `SectionNode`/`SectionBody` (prosa/callout/tabella + sotto-sezioni), anche
  nella vIPI ACC, che prima lo appiattiva a sola prosa.
- **Sotto-sezioni:** possono stare **prima o dopo** il corpo della sezione (`DocumentSection.BeforeParentBody`,
  §3g); il corpo è una posizione in una sequenza di tre slot, uguale nei tre viewer e nell'editor.
- **Anteprime:** un `?as=` non valido degrada alla pubblica **con le derivate frozen** (prima restava live).
- **Rotte:** la vLOA ha una sola rotta viewer, `/services/vsop/{acc}/vloa?acc=YYYY` (rimosso `apps/vipi?vloa=`).

## Nota doc 13 (audit dei tre documenti, 2026-08-10)

- **Chi rende il corpo di una sezione** lo dice il `SectionCatalog`, per profilo
  (`IsHostRendered`): l'insieme viveva in sei copie sparse fra pagine ed editor. Con esso anche
  «sezione obbligatoria» (`IsFixed`), che aveva tre implementazioni — e nella vLOA confrontava i **titoli**.
- **La vLOA nasce dal catalogo** come le altre due: `VloaSections` resta la sola sorgente dei contenuti
  iniziali. Le due direzioni dei coordinamenti hanno una chiave per verso
  (`coordination:out` / `coordination:in`) invece di ripetere quella del padre.
- **«Minime di vettoramento»** è tornata una sezione editoriale: si scrive a mano (Guida `#editor-minime`).
- **Pannello release**: ancora, titolo e aiuto stanno dentro `ReleasePanel`; i quattro editor passano gli
  stessi parametri (differenze, annullo admin, anteprima). La voce di menu punta a `p-release`.
- **Landing ACC**: la card della vIPI segue lo stesso gate di aeroporti/APP/vLOA (release effettiva).
- **Rotte pubbliche**: `IDocRoutesRegistry` è in `Vipi.Application.Routing` e lo consultano anche ricerca
  e «Cosa è cambiato» — prima ognuna aveva la propria copia, e i documenti APP finivano sulla vIPI di ACC.
