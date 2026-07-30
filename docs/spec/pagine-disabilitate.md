# PAGINE DISABILITATE — rebuild `/vsop` (Round 12)

Queste pagine sono state **disabilitate, non cancellate**: la direttiva `@page` è stata sostituita
da un commento Razor (`@* DISABILITATA … *@`), quindi la rotta non risponde più ma il codice resta
intatto e ricompilabile. Verranno ragionate una a una in seguito.

**Come riattivarne una:** rimettere la riga `@page "..."` in cima al file (vedi colonna "Ex-rotta"),
ripristinare gli eventuali link in `SopHome`/`AccLanding`/`ScreensIndex`, ricompilare.

| File | Ex-rotta | Funzione | Perché disabilitata |
|---|---|---|---|
| `RidottaPage.razor` | `/vsop/{acc}/ridotta` | Vista ridotta live (F3): frequenze + trasferimenti per la postazione, refresh SSE. | Era nella sezione "Strumenti" rimossa dalla nuova landing ACC. Cuore del live F3: candidata a rientrare presto. |
| `RidottaAppPage.razor` | `/vsop/{acc}/ridotta-app` | Vista ridotta per APP. | Idem (Strumenti). ⚠️ **Non riattivabile rimettendo `@page`**: a differenza di `RidottaPage`, il contenuto è un **mockup hardcoded** («LIRP Pisa APP» in markup, nessun binding ai dati). Riattivarla richiede di scriverla, non di scommentarla. |

> 🗑️ **Eliminate (pulizia)**: `Aor3dPage`, `ExportPage`, `StatiPage` (disabilitate e morte), più le legacy
> `VipiDocument.razor` (sostituita da `AccVipiPage` su `/vipi`) e `EditorPage.razor` (dispatcher generico orfano;
> l'editor vLOA dedicato sarà realizzato con le vLOA). Rimosso anche il CSS `.aor3d-*`.

> 🗑️ **`VloaPage.razor` ELIMINATA** (ridisegno route vLOA): la view per-documento è stata **assorbita in `VloaListPage.razor`**. Ora `/vsop/{acc}/vloa` senza `?acc` è l'elenco, con `?acc=YYYY` è il documento della coppia acc↔YYYY (chiave = codice ACC vicino, non più docId). Rimosse anche le route `/vsop/{acc}/editor-vloa` (stub) e l'host `apps/editor?vloa=`: l'editor vLOA vive ora su `/vsop/{acc}/vloa/editor?acc=YYYY` (`VloaEditorPage.razor`). Vedi `mappa-pagine.md`.

## Note / link residui da sapere
- **Ricerca e "Cosa è cambiato":** i documenti **vLOA** ora linkano `/vsop/{acc}/vloa?acc=YYYY`
  (in `EfSearchRepository`/`EfChangesRepository`, con `YYYY` = codice ACC vicino) → view per-documento.
- Gli **editor** (vIPI, trasferimenti, vLOA, profilo aeroporto) **restano attivi**.
- **Rimossa** `/vsop/{acc}/topologia` (`TopologiaPage`): la gerarchia si gestisce da `/vsop/admin/sectorstructure`
  (per callsign, round 20); regole di unificazione + simulatore AoR erano legacy e non hanno più UI (il motore
  `IAorService` + `UnificationRule` e i test S1–S10 restano).
- La rotta APP è passata da `/vsop/{acc}/app` a **`/vsop/{acc}/apps/vipi`** (più la nuova `/apps` elenco).
- La rotta viewer aeroporto è passata da `/vsop/{acc}/aeroporto` a **`/vsop/{acc}/airports?icao=`**.
