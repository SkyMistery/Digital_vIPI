# PAGINE DISABILITATE — rebuild `/services/vsop` (Round 12)

Queste pagine sono state **disabilitate, non cancellate**: la direttiva `@page` è stata sostituita
da un commento Razor (`@* DISABILITATA … *@`), quindi la rotta non risponde più ma il codice resta
intatto e ricompilabile. Verranno ragionate una a una in seguito.

**Come riattivarne una:** rimettere la riga `@page "..."` in cima al file (vedi colonna "Ex-rotta"),
ripristinare gli eventuali link in `SopHome`/`AccLanding`/`ScreensIndex`, ricompilare.

| File | Ex-rotta | Funzione | Perché disabilitata |
|---|---|---|---|
| _(nessuna: l'elenco si è svuotato — vedi sotto)_ | | | |

> 🗑️ **`RidottaPage` e `RidottaAppPage` ELIMINATE (2026-07-31, doc [refactor/12](../refactor/12-vista-live-unificata.md)).**
> Spente dal Round 12 e mai riattivate: quello che dovevano fare — frequenze e trasferimenti della postazione,
> refresh SSE — lo fa la **vista live** `/services/vsop/live[/{callsign}]`, che oltretutto la risolve dal callsign connesso
> invece che da un selettore. `RidottaAppPage` era per metà un mockup hardcoded, quindi non era comunque
> riattivabile scommentando l'`@page`. Nello stesso giro sono sparite `AccLivePage`/`AppLivePage`, fuse in
> `LivePage.razor`.

> 🗑️ **Eliminate (pulizia)**: `Aor3dPage`, `ExportPage`, `StatiPage` (disabilitate e morte), più le legacy
> `VipiDocument.razor` (sostituita da `AccVipiPage` su `/vipi`) e `EditorPage.razor` (dispatcher generico orfano;
> l'editor vLOA dedicato sarà realizzato con le vLOA). Rimosso anche il CSS `.aor3d-*`.

> 🗑️ **`VloaPage.razor` ELIMINATA** (ridisegno route vLOA): la view per-documento è stata **assorbita in `VloaListPage.razor`**. Ora `/services/vsop/{acc}/vloa` senza `?acc` è l'elenco, con `?acc=YYYY` è il documento della coppia acc↔YYYY (chiave = codice ACC vicino, non più docId). Rimosse anche le route `/services/vsop/{acc}/editor-vloa` (stub) e l'host `apps/editor?vloa=`: l'editor vLOA vive ora su `/services/vsop/{acc}/vloa/editor?acc=YYYY` (`VloaEditorPage.razor`). Vedi `mappa-pagine.md`.

## Note / link residui da sapere
- **Ricerca e "Cosa è cambiato":** i documenti **vLOA** ora linkano `/services/vsop/{acc}/vloa?acc=YYYY`
  (in `EfSearchRepository`/`EfChangesRepository`, con `YYYY` = codice ACC vicino) → view per-documento.
- Gli **editor** (vIPI, trasferimenti, vLOA, profilo aeroporto) **restano attivi**.
- **Rimossa** `/services/vsop/{acc}/topologia` (`TopologiaPage`): la gerarchia si gestisce da `/services/vsop/admin/sector-structure`
  (per callsign, round 20); regole di unificazione + simulatore AoR erano legacy e non hanno più UI (il motore
  `IAorService` + `UnificationRule` e i test S1–S10 restano).
- La rotta APP è passata da `/services/vsop/{acc}/app` a **`/services/vsop/{acc}/apps/vipi`** (più la nuova `/apps` elenco).
- La rotta viewer aeroporto è passata da `/services/vsop/{acc}/airports` a **`/services/vsop/{acc}/airports?icao=`**.
