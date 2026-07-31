# MAPPA PAGINE — vIPI (prefisso `/vsop`)

> Documento di **rapida lettura**: la gerarchia delle pagine del sito e dove vive ciascuna.
> Aggiornato al **rebuild Round 12** (rename `/sop` → `/vsop` + nuova struttura ACC).
> Per le pagine spente vedi **`pagine-disabilitate.md`**.

## Gerarchia (cosa vede l'utente)

```
/vsop                                   Home  ........................ SopHome.razor
├─ Ricerca: titolo + AIRAC + barra
├─ Card ACC (codice, nome, n° ATC online)        → /vsop/{acc}
├─ Navigazione rapida: "Cosa è cambiato"         → /vsop/changed
└─ [staff/editori] Documenti · Bozze&Versioni (= hub editor unificato, tasto «Nuovo documento») · Tutte le schermate · Struttura · Permessi

/vsop/{acc}                             Landing ACC  ................. AccLanding.razor
├─ Documenti:
│  ├─ vIPI di ACC                                  → /vsop/{acc}/vipi
│  ├─ Card Aeroporti (3 in evidenza)
│  │     titolo → /vsop/{acc}/airports            (elenco)
│  │     voce   → /vsop/{acc}/airports?icao=XXXX  (documento)
│  ├─ Card APP non remot. (3 in evidenza)
│  │     titolo → /vsop/{acc}/apps                (elenco)
│  │     voce   → /vsop/{acc}/apps/vipi?app=XXX_APP (documento · solo APP NON remotizzati · editor: /apps/editor?app=)
│  └─ Card vLOA (3 in evidenza)
│        titolo → /vsop/{acc}/vloa                (elenco)
│        voce   → /vsop/{acc}/vloa?acc=YYYY        (documento · vicino YYYY · editor: /vloa/editor?acc=)
└─ [Admin CH/AOD] Editor vIPI · Editor vLOA  (Trasferimenti → /vsop/admin/trasferimenti · Gerarchia → /vsop/admin/sectorstructure)

/vsop/live[/{callsign}]   Vista live, per callsign  ................ LivePage.razor
/vsop/{acc}/vipi          vIPI ACC (data-driven, multi-albero)  .... AccVipiPage.razor
/vsop/{acc}/airports      Elenco aeroporti (no ?icao)  ............. AeroportoPage.razor
/vsop/{acc}/airports?icao=XXXX   vIPI aeroporto (con ?icao)  ....... AeroportoPage.razor
/vsop/{acc}/apps          Elenco APP non remotizzati  ............. AppsListPage.razor
/vsop/{acc}/apps/vipi?app=XXX_APP  Documento APP non remot. (data-driven)  AppnPage.razor
/vsop/{acc}/apps/editor?app=XXX_APP  Editor dedicato APP non remot.  AppEditorPage.razor
/vsop/{acc}/vloa          Elenco vLOA (no ?acc)  ................. VloaListPage.razor
/vsop/{acc}/vloa?acc=YYYY Documento vLOA (vicino YYYY)  .......... VloaListPage.razor
/vsop/{acc}/vloa/editor?acc=YYYY  Editor vLOA (coppia acc↔YYYY)  . VloaEditorPage.razor
```

> ⚠️ **Aeroporti = una sola rotta** `/vsop/{acc}/airports`: senza `?icao=` mostra l'elenco,
> con `?icao=` il documento (Blazor non distingue per query string → una pagina che ramifica).
> Gli APP invece hanno due path distinti (`/apps` elenco, `/apps/vipi` documento).

## Tabella rotte (attive)

| Rotta | File | Ruolo | Accesso |
|---|---|---|---|
| `/vsop` | `SopHome.razor` | Home | tutti |
| `/vsop/{acc}` | `AccLanding.razor` | Landing ACC | tutti |
| `/vsop/{acc}/vipi` | `AccVipiPage.razor` | vIPI ACC (data-driven, multi-albero `?tree=`) | tutti (edit: AOD/DIR) |
| `/vsop/{acc}/airports` | `AeroportoPage.razor` | Elenco + doc aeroporto | tutti |
| `/vsop/{acc}/apps` | `AppsListPage.razor` | Elenco APP non remot. | tutti |
| `/vsop/{acc}/apps/vipi` | `AppnPage.razor` | Documento APP non remot. (`?app=CALLSIGN`, **data-driven** dal profilo; tasto **✎ Editor** se autorizzato). Solo `ApproachKind.Standalone`: su un callsign remotizzato non esiste documento (doc 11 §3e). Il vecchio ramo `?vloa=` (seconda rotta della vLOA) è **rimosso**: la vLOA ha una rotta sola | tutti (edit: AOD/DIR) |
| `/vsop/{acc}/apps/editor` | `AppEditorPage.razor` | **Editor dedicato APP non remotizzato** (`?app=CALLSIGN`): WYSIWYG, 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + sezioni custom, riordino drag-and-drop + tasti, nascondi sezioni. Freq/coord/AOR **derivate live**. I doc APPn instradano qui (non all'editor generico/aeroporto) via `DocumentSummary.IsStandaloneApp` | admin/grant ACC |
| `/vsop/live` · `/vsop/live/{callsign}` | `LivePage.razor` | **Vista live UNICA per callsign** (doc [refactor/12](../refactor/12-vista-live-unificata.md)). Senza callsign = la postazione con cui sei connesso su IVAO, seguita live; **nessun selettore**. Con callsign = quella postazione in sola consultazione (banner + ritorno alla propria). Non connesso ⇒ stato d'attesa con gli ATC online cliccabili, aggancio automatico al tick SSE. Resa per tipo di ente dai descrittori `ILiveStationKind` (area · avvicinamento · aeroporto): CTR/FSS, APP standalone e remotizzati, **TWR/ITWR/GND/DEL**. Ex `/vsop/{acc}/operativa`, `/vsop/{acc}/live`, `/vsop/{acc}/operativa-app`, `/vsop/{acc}/live-app` — tutte **301 a un salto solo** | tutti |
| `/vsop/{acc}/vloa` | `VloaListPage.razor` | Elenco vLOA della ACC (no `?acc`); con `?acc=YYYY` mostra il **documento** della coppia acc↔YYYY (una rotta che ramifica per query, come aeroporti). Chiave = **codice ACC vicino** (`VloaRow.NeighbourCode`), non più docId | tutti (edit: AOD/DIR) |
| `/vsop/changed` | `ChangedPage.razor` | Cosa è cambiato | tutti |
| `/vsop/search` | `SearchPage.razor` | Ricerca full-text | tutti |
| `/vsop/guida` | `GuidaPage.razor` | **Guida in-app bilingue IT/EN** (toggle `?lang=it\|en`, default = cultura negoziata; contenuto data-driven, TOC derivato) — consultare + modificare: pagina statica cercabile (Ctrl+F) a sezioni collassabili con ancore deep-link (`#editor-release`, `#editor-lock`, …). Vi rimandano i `?` contestuali (`Components/HelpHint.razor`) agganciati ai controlli editor (ReleasePanel, EditLockBar, DocumentBlocksEditor, link Anteprima, Salva-tutto). Link nella topbar (icona `help-circle`, tutti). Le sezioni emergono anche nella **ricerca globale** (`GuideSearchCatalog` → `SearchService`, solo scope Tutti). **Tour onboarding** editor: `wwwroot/vipi-tour.js` (step su `data-tour`, auto 1×/utente, `?tour=1` per rivederlo) | tutti |
| `/vsop/screens` | `ScreensIndex.razor` | Indice schermate | staff |
| `/vsop/release/{id}` | `ReleasePreviewPage.razor` | **Redirect** (compat): risolve la release e reindirizza al viewer tipizzato con `?as=rel:{id}`. Le anteprime sono rese dentro i viewer, non più qui | staff/editori |
| `/vsop/versioni`, `/vsop/{acc}/versioni` | `VersioniPage.razor` | **Hub documenti unificato** (ex `/vsop/editor` assorbito): elenco completo doc (vIPI ACC/APP/aeroporto/vLOA) + ricerca/filtri, «Apri editor» per riga, tasto «Nuovo documento», storico versioni + release AIRAC. Azioni hide/elimina/annulla-release solo admin. `Apri editor` gated server-side | staff/editori (admin o grant) |
| `/vsop/editor/newdoc` | `NewDocumentPage.razor` | Creazione documenti. **vIPI ACC**: si scelgono i **root** degli alberi (ogni root porta lo scope dell'intero sottoalbero d'area = CTR + APP di ACC, **cross-ACC**; più alberi per doc). **vIPI APP**: solo APP non remotizzati (`App`+`Standalone`). **vLOA**: solo tra ACC **italiano** (Home) e **estero** (Neighbour), es. Roma↔Marsiglia. Lavora su una vista globale dei settori (`IStructureEditingService.ListSectorNodesAsync`) | admin |
| `/vsop/{acc}/editor` | `AccEditorPage.razor` | Editor vIPI ACC (data-driven, multi-albero `?tree=`) | admin/grant ACC |
| `/vsop/{acc}/vloa/editor` | `VloaEditorPage.razor` | Editor vLOA (con `ReleasePanel`, come ACC/APP/aeroporto — doc 11 §3f). Con `?acc=YYYY` apre la coppia acc↔YYYY (host del componente `VloaEditor`); senza `?acc` mostra un **chooser** delle vLOA della ACC. Ex `/vsop/{acc}/editor-vloa` (stub) ed ex host `apps/editor?vloa=` (rimossi) | admin/grant ACC |
| `/vsop/{acc}/airports/editor` | `AeroportoEditorPage.razor` | Editor profilo aeroporto (profilo + settori ATC importati: mostra/nascondi + limiti) | admin/grant ACC |
| `/vsop/admin/acc` | `AccAdminPage.razor` | ACC + settori ATC: import da sorgente (`/v2/centers` + `/subcenters`, auto giornaliero), militare, mostra/nascondi, limiti quota admin | admin |
| `/vsop/admin/sectorstructure` | `StrutturaPage.razor` | **Gerarchia di copertura GLOBALE (cross-ACC)** per callsign sui settori importati (§9.12 round 20): UI a **card per ACC** (ogni card = gli alberi con radice in quell'ACC, comprimi/espandi card e rami + ricerca) + pannello dettaglio sticky con catena di fallback, picker padre e **Applica**. **Solo gerarchia**: niente selettore ACC; la creazione documenti è su `/vsop/editor/newdoc`. Nodi: settori ACC, **tutte le posizioni d'aeroporto** (APP · TWR · GND · DEL; ATIS escluso) e aeroporti. Le posizioni senza padre scritto mostrano quello **ereditato** dalla scaletta DEL→GND→TWR→APP (interruttore «Posizioni d'aeroporto», spento di default); un padre più in basso nella scaletta è rifiutato. Creazione/eliminazione/frequenza settori NON qui (solo pagina ACC). Ex `/admin/struttura`, redirect 301 | admin |
| `/vsop/admin/airports` | `AeroportiPage.razor` | Gestione aeroporti (filtro per ACC; colonna **Stato** + mostra/nascondi; alias legacy `/vsop/admin/aeroporti`) | admin |
| `/vsop/admin/permessi` | `AdminGrantsPage.razor` | Permessi editing (+ card «Trasferimenti» in Dashboard) | admin |
| `/vsop/admin/trasferimenti` | `AdminTrasferimentiPage.razor` | Trasferimenti tra settori: selettore ACC + edit nidificato **Settore ▸ Aeroporto ▸ Arrivi/Partenze ▸ righe** (CoP/quota/settore ricevente, cross-ACC; ICAO esteri ammessi). Risoluzione live in Ridotta risale la gerarchia (terminale UNICOM). Ex `/vsop/{acc}/editor-trasferimenti`. Link da Struttura e card in Dashboard permessi | admin |
| `/vsop/admin/sorgenti` | `SorgentiAdminPage.razor` | Policy import sorgenti | admin |
| `/vsop/admin/audit` | `AuditPage.razor` | Audit log | admin |

## Note tecniche
- **Prefisso:** tutte le rotte sono sotto `/vsop`. I vecchi URL `/sop*` fanno **redirect 301** a `/vsop*`
  (preservando la query string) — middleware in `src/Vipi.Host/Program.cs`. Stesso middleware:
  le quattro rotte storiche della vista operativa/live per-ACC → `/vsop/live[/{callsign}]`, un salto solo.
  ⚠️ `/vsop/live/{callsign}` è a parametro e ricade sul prefisso dello stream SSE `/vsop/live/atc`: vince il
  segmento **letterale** (precedenza del routing), verificato da `SmokeTests.Sse_endpoint_wins_over_the_live_page_route`.
- **"3 in evidenza":** campo `FeaturedRank` (1..3) su `Airport` e `Sector` (`Domain/Entities/Anagrafica.cs`,
  migrazione `AddFeaturedRank`) e su `Document` per le vLOA (`Documents.cs`, migrazione `AddVloaFeaturedRank`).
  Si imposta dall'**Editor vIPI ACC** (pannello "In evidenza nella landing ACC", colonne Aeroporti/APP/vLOA).
  Senza selezione, la landing mostra i primi 3 per ICAO/callsign/titolo.
- **Conteggio ATC online in Home:** one-shot, conta i callsign il cui primo token = codice ACC
  (es. `LIRR_NE_CTR`). Le torri/APP degli aeroporti non sono ancora contate (follow-up).
- **Aeroporti nascosti:** `Airport.IsHidden` (toggle in `/vsop/admin/airports`) rende la pagina
  `/vsop/{acc}/airports?icao=` inaccessibile al pubblico e toglie l'aeroporto dagli elenchi/landing.
  Gli aeroporti **senza alcun settore** sono nascosti di default (`IsPublic = !IsHidden && Sectors>0`).
- **Editor:** non rivisti in questo round ("poi ragioniamo sugli editor"). Restano raggiungibili
  dalla sezione Admin della landing ACC, invariati.
- **Lock editing admin (2026-07):** le 4 pagine di struttura (`/vsop/admin/sectorstructure`, `/vsop/admin/acc`,
  `/vsop/admin/trasferimenti`, `/vsop/admin/airports`) condividono **un** lock esclusivo (`admin:structure`), e
  `/vsop/editor/newdoc` ne ha uno separato: una persona alla volta modifica. Barra `Components/EditLockBar.razor`
  («Inizia/Fine modifica» + banner + «Forza sblocco» admin). Read-only finché non si prende il lock; TTL 3min +
  heartbeat 60s (chiusa la scheda si libera da sé). Storage `EditResourceLock`. Vedi memoria `edit-resource-lock-design`.
- **Anteprime (`?as=`, round 33):** i 4 viewer documentali (`AccVipiPage`, `AeroportoPage`, `AppnPage`,
  `VloaListPage`) accettano un parametro uniforme `as`: assente → **pubblica** (release effettiva al ciclo
  corrente, altrimenti live); `?as=draft` → **bozza live**; `?as=rel:{releaseId}` → **snapshot congelato**
  di una release. Bozza/release sono **gated** al permesso di modifica dell'ACC; per un utente non autorizzato,
  identità release non corrispondente o URL forgiato la vista **degrada a pubblica** (nessun banner, nessuna
  fuga di bozza). Banner condiviso `Components/PreviewBanner.razor`; parsing `Shared/PreviewMode.cs` (alias
  legacy `?live=1` → `draft`). I tasti «Anteprima» degli editor puntano a `?as=draft`; «👁 Anteprima» per-release
  a `?as=rel:{id}`. La vecchia pagina `/vsop/release/{id}` è ora un redirect.

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
- **Rotte:** la vLOA ha una sola rotta viewer, `/vsop/{acc}/vloa?acc=YYYY` (rimosso `apps/vipi?vloa=`).
