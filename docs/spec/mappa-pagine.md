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
└─ [solo CH/AOD/DIR] Bozze&Versioni · Tutte le schermate · Struttura · Permessi

/vsop/{acc}                             Landing ACC  ................. AccLanding.razor
├─ Documenti:
│  ├─ vIPI di ACC                                  → /vsop/{acc}/vipi
│  ├─ Card Aeroporti (3 in evidenza)
│  │     titolo → /vsop/{acc}/airports            (elenco)
│  │     voce   → /vsop/{acc}/airports?icao=XXXX  (documento)
│  ├─ Card APP non remot. (3 in evidenza)
│  │     titolo → /vsop/{acc}/apps                (elenco)
│  │     voce   → /vsop/{acc}/apps/vipi?app=XXX_APP (documento · editor: /apps/editor?app=)
│  └─ Card vLOA (3 in evidenza)
│        titolo → /vsop/{acc}/vloa                (elenco)
│        voce   → /vsop/{acc}/vloa/{docId}        (documento)
└─ [Admin CH/AOD] Editor vIPI · Editor vLOA  (Trasferimenti → /vsop/admin/trasferimenti · Gerarchia → /vsop/admin/sectorstructure)

/vsop/{acc}/vipi          vIPI ACC Estesa  ......................... VipiDocument.razor
/vsop/{acc}/airports      Elenco aeroporti (no ?icao)  ............. AeroportoPage.razor
/vsop/{acc}/airports?icao=XXXX   vIPI aeroporto (con ?icao)  ....... AeroportoPage.razor
/vsop/{acc}/apps          Elenco APP non remotizzati  ............. AppsListPage.razor
/vsop/{acc}/apps/vipi?app=XXX_APP  Documento APP non remot. (data-driven)  AppnPage.razor
/vsop/{acc}/apps/editor?app=XXX_APP  Editor dedicato APP non remot.  AppEditorPage.razor
/vsop/{acc}/vloa          Elenco vLOA  ............................ VloaListPage.razor
/vsop/{acc}/vloa/{docId}  Documento vLOA (per id)  ................ VloaPage.razor
```

> ⚠️ **Aeroporti = una sola rotta** `/vsop/{acc}/airports`: senza `?icao=` mostra l'elenco,
> con `?icao=` il documento (Blazor non distingue per query string → una pagina che ramifica).
> Gli APP invece hanno due path distinti (`/apps` elenco, `/apps/vipi` documento).

## Tabella rotte (attive)

| Rotta | File | Ruolo | Accesso |
|---|---|---|---|
| `/vsop` | `SopHome.razor` | Home | tutti |
| `/vsop/{acc}` | `AccLanding.razor` | Landing ACC | tutti |
| `/vsop/{acc}/vipi` | `VipiDocument.razor` | vIPI ACC Estesa | tutti (edit: AOD/DIR) |
| `/vsop/{acc}/airports` | `AeroportoPage.razor` | Elenco + doc aeroporto | tutti |
| `/vsop/{acc}/apps` | `AppsListPage.razor` | Elenco APP non remot. | tutti |
| `/vsop/{acc}/apps/vipi` | `AppnPage.razor` | Documento APP non remot. (`?app=CALLSIGN`, **data-driven** dal profilo; tasto **✎ Editor** se autorizzato) | tutti (edit: AOD/DIR) |
| `/vsop/{acc}/apps/editor` | `AppEditorPage.razor` | **Editor dedicato APP non remotizzato** (`?app=CALLSIGN`): WYSIWYG, 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + sezioni custom, riordino drag-and-drop + tasti, nascondi sezioni. Freq/coord/AOR **derivate live**. I doc APPn instradano qui (non all'editor generico/aeroporto) via `DocumentSummary.IsStandaloneApp` | admin/grant ACC |
| `/vsop/{acc}/vloa` | `VloaListPage.razor` | Elenco vLOA della ACC | tutti |
| `/vsop/{acc}/vloa/{docId}` | `VloaPage.razor` | Documento vLOA (per id) | tutti (edit: AOD/DIR) |
| `/vsop/changed` | `ChangedPage.razor` | Cosa è cambiato | tutti |
| `/vsop/search` | `SearchPage.razor` | Ricerca full-text | tutti |
| `/vsop/screens` | `ScreensIndex.razor` | Indice schermate | staff |
| `/vsop/versioni`, `/vsop/{acc}/versioni` | `VersioniPage.razor` | Bozze & versioni | staff |
| `/vsop/editor` | `EditorHubPage.razor` | Hub editor (+ bottone «Nuovo documento») | staff |
| `/vsop/editor/newdoc` | `NewDocumentPage.razor` | Creazione documenti. **vIPI ACC**: si scelgono i **root** degli alberi (ogni root porta lo scope dell'intero sottoalbero d'area = CTR + APP di ACC, **cross-ACC**; più alberi per doc). **vIPI APP**: solo APP non remotizzati (`App`+`Standalone`). **vLOA**: solo tra ACC **italiano** (Home) e **estero** (Neighbour), es. Roma↔Marsiglia. Lavora su una vista globale dei settori (`IStructureEditingService.ListSectorNodesAsync`) | admin |
| `/vsop/{acc}/editor` | `EditorPage.razor` | Editor vIPI ACC **+ picker "in evidenza"** | admin/grant ACC |
| `/vsop/{acc}/editor-vloa` | `VloaEditorPage.razor` | Editor vLOA | admin/grant ACC |
| `/vsop/{acc}/airports/editor` | `AeroportoEditorPage.razor` | Editor profilo aeroporto (profilo + settori ATC importati: mostra/nascondi + limiti) | admin/grant ACC |
| `/vsop/admin/acc` | `AccAdminPage.razor` | ACC + settori ATC: import da sorgente (`/v2/centers` + `/subcenters`, auto giornaliero), militare, mostra/nascondi, limiti quota admin | admin |
| `/vsop/admin/sectorstructure` | `StrutturaPage.razor` | **Gerarchia di copertura GLOBALE (cross-ACC)** per callsign sui settori importati (§9.12 round 20): UI a **card per ACC** (ogni card = gli alberi con radice in quell'ACC, comprimi/espandi card e rami + ricerca) + pannello dettaglio sticky con catena di fallback, picker padre e **Applica**. **Solo gerarchia**: niente selettore ACC; la creazione documenti è su `/vsop/editor/newdoc`. Creazione/eliminazione/frequenza settori NON qui (solo pagina ACC). Ex `/admin/struttura`, redirect 301 | admin |
| `/vsop/admin/airports` | `AeroportiPage.razor` | Gestione aeroporti (filtro per ACC; colonna **Stato** + mostra/nascondi; alias legacy `/vsop/admin/aeroporti`) | admin |
| `/vsop/admin/permessi` | `AdminGrantsPage.razor` | Permessi editing (+ card «Trasferimenti» in Dashboard) | admin |
| `/vsop/admin/trasferimenti` | `AdminTrasferimentiPage.razor` | Trasferimenti tra settori: selettore ACC + edit nidificato **Settore ▸ Aeroporto ▸ Arrivi/Partenze ▸ righe** (CoP/quota/settore ricevente, cross-ACC; ICAO esteri ammessi). Risoluzione live in Ridotta risale la gerarchia (terminale UNICOM). Ex `/vsop/{acc}/editor-trasferimenti`. Link da Struttura e card in Dashboard permessi | admin |
| `/vsop/admin/sorgenti` | `SorgentiAdminPage.razor` | Policy import sorgenti | admin |
| `/vsop/admin/audit` | `AuditPage.razor` | Audit log | admin |

## Note tecniche
- **Prefisso:** tutte le rotte sono sotto `/vsop`. I vecchi URL `/sop*` fanno **redirect 301** a `/vsop*`
  (preservando la query string) — middleware in `src/Vipi.Host/Program.cs`.
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
