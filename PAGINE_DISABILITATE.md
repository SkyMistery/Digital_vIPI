# PAGINE DISABILITATE — rebuild `/vsop` (Round 12)

Queste pagine sono state **disabilitate, non cancellate**: la direttiva `@page` è stata sostituita
da un commento Razor (`@* DISABILITATA … *@`), quindi la rotta non risponde più ma il codice resta
intatto e ricompilabile. Verranno ragionate una a una in seguito.

**Come riattivarne una:** rimettere la riga `@page "..."` in cima al file (vedi colonna "Ex-rotta"),
ripristinare gli eventuali link in `SopHome`/`AccLanding`/`ScreensIndex`, ricompilare.

| File | Ex-rotta | Funzione | Perché disabilitata |
|---|---|---|---|
| `RidottaPage.razor` | `/vsop/{acc}/ridotta` | Vista ridotta live (F3): frequenze + trasferimenti per la postazione, refresh SSE. | Era nella sezione "Strumenti" rimossa dalla nuova landing ACC. Cuore del live F3: candidata a rientrare presto. |
| `RidottaAppPage.razor` | `/vsop/{acc}/ridotta-app` | Vista ridotta per APP. | Idem (Strumenti). |
| `Aor3dPage.razor` | `/vsop/{acc}/aor3d` | Visualizzazione 3D dei volumi settore (stub SVG/Three.js). | Strumenti rimossi; era uno stub. Rimosso anche il bottone "Vista 3D" da `AorBlock.razor`. |
| `ExportPage.razor` | `/vsop/{acc}/export` | Export PDF della vista Estesa (stampa browser). | Strumenti rimossi. |
| `StatiPage.razor` | `/vsop/stati` | Pagina "Stati & messaggi" (demo/diagnostica). | Non prevista nella nuova struttura. |

> ✅ **`VloaPage.razor` RIATTIVATA** (giro vLOA, 28 giu): ora è il **viewer per-documento** su `/vsop/{acc}/vloa/{docId}` (carica per id, non più per FIR). L'elenco vive in `VloaListPage.razor` su `/vsop/{acc}/vloa`. Vedi MAPPA_PAGINE.md.

## Note / link residui da sapere
- **Ricerca e "Cosa è cambiato":** i documenti **vLOA** ora linkano `/vsop/{fir}/vloa/{docId}`
  (in `EfSearchRepository`/`EfChangesRepository`) → viewer per-documento. Risolto il 404.
- Gli **editor** (vIPI, topologia, trasferimenti, vLOA, profilo aeroporto) **restano attivi**: gli
  editor non sono stati toccati in questo round.
- La rotta APP è passata da `/vsop/{acc}/app` a **`/vsop/{acc}/apps/vipi`** (più la nuova `/apps` elenco).
- La rotta viewer aeroporto è passata da `/vsop/{acc}/aeroporto` a **`/vsop/{acc}/airports?icao=`**.
