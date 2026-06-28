# MAPPA PAGINE — vIPI (prefisso `/vsop`)

> Documento di **rapida lettura**: la gerarchia delle pagine del sito e dove vive ciascuna.
> Aggiornato al **rebuild Round 12** (rename `/sop` → `/vsop` + nuova struttura ACC).
> Per le pagine spente vedi **`PAGINE_DISABILITATE.md`**.

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
│  │     voce   → /vsop/{acc}/apps/vipi?icao=XXXX (documento)
│  └─ Card vLOA (3 in evidenza)
│        titolo → /vsop/{acc}/vloa                (elenco)
│        voce   → /vsop/{acc}/vloa/{docId}        (documento)
└─ [Admin CH/AOD] Editor vIPI · Topologia · Editor trasferimenti · Editor vLOA

/vsop/{acc}/vipi          vIPI ACC Estesa  ......................... VipiDocument.razor
/vsop/{acc}/airports      Elenco aeroporti (no ?icao)  ............. AeroportoPage.razor
/vsop/{acc}/airports?icao=XXXX   vIPI aeroporto (con ?icao)  ....... AeroportoPage.razor
/vsop/{acc}/apps          Elenco APP non remotizzati  ............. AppsListPage.razor
/vsop/{acc}/apps/vipi?icao=XXXX  Documento APP non remot.  ........ AppnPage.razor
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
| `/vsop/{acc}/apps/vipi` | `AppnPage.razor` | Documento APP non remot. | tutti (edit: AOD/DIR) |
| `/vsop/{acc}/vloa` | `VloaListPage.razor` | Elenco vLOA della FIR | tutti |
| `/vsop/{acc}/vloa/{docId}` | `VloaPage.razor` | Documento vLOA (per id) | tutti (edit: AOD/DIR) |
| `/vsop/changed` | `ChangedPage.razor` | Cosa è cambiato | tutti |
| `/vsop/search` | `SearchPage.razor` | Ricerca full-text | tutti |
| `/vsop/screens` | `ScreensIndex.razor` | Indice schermate | staff |
| `/vsop/versioni`, `/vsop/{acc}/versioni` | `VersioniPage.razor` | Bozze & versioni | staff |
| `/vsop/editor` | `EditorHubPage.razor` | Hub editor | staff |
| `/vsop/{acc}/editor` | `EditorPage.razor` | Editor vIPI ACC **+ picker "in evidenza"** | admin/grant FIR |
| `/vsop/{acc}/topologia` | `TopologiaPage.razor` | Topologia/simulatore | admin/grant FIR |
| `/vsop/{acc}/editor-trasferimenti` | `XferEditorPage.razor` | Editor trasferimenti | admin/grant FIR |
| `/vsop/{acc}/editor-vloa` | `VloaEditorPage.razor` | Editor vLOA | admin/grant FIR |
| `/vsop/{acc}/aeroporto/editor` | `AeroportoEditorPage.razor` | Editor profilo aeroporto | admin/grant FIR |
| `/vsop/admin/struttura` | `StrutturaPage.razor` | FIR/settori/frequenze | admin |
| `/vsop/admin/aeroporti` | `AeroportiPage.razor` | Gestione aeroporti | admin |
| `/vsop/admin/permessi` | `AdminGrantsPage.razor` | Permessi editing | admin |
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
- **Editor:** non rivisti in questo round ("poi ragioniamo sugli editor"). Restano raggiungibili
  dalla sezione Admin della landing ACC, invariati.
