# UX Hardening — carta 🟢

> Refactor trasversale UI (non tocca dominio/dati). Segue il ciclo di
> [REFACTOR-PROCESS](../refactor/REFACTOR-PROCESS.md) (Fase 0→4, «carta prima di codice»).
> Asse **separato** da quello dati/import (`refactor/01→10`): qui l'area è la UI Blazor.
>
> **Stato: approvata dall'owner 2026-07-22; U1/U2/U3 eseguite, P2 chiusa il 23 agosto 2026
> dall'[audit frontend/UI](../history/audit-2026-08-23-frontend-ui.md).**
> Origine: audit UX 2026-07-22 (3 criticità 🔴 + 9 follow-up 🟡).
> Repo non-git → nessun branch; lavoro diretto. Baseline: `Vipi.Ui.Tests` 13 verde.
>
> **Avanzamento (2026-07-22):**
> - **U1 (conferma delete) ✅** — `Components/InlineConfirm.razor` + CSS `.btn.danger`/`.btn:disabled`.
>   Applicato a: `VersioniPage` (elimina definitivo), `AeroportiPage` (Remove + BulkDelete),
>   `AdminTasksPage`, `DocumentSectionsEditor` (DeleteSection/DeleteBlock). Migrato
>   `AdminTrasferimentiPage` (rimosso stato inline `_confirmDeleteFlowId`).
> - **U3 (zoom a11y) ✅** — `#vipiZoomPct` → `role="status"` + `aria-live="polite"` (verificato live).
> - **U2 (icone SVG) ✅ (sweep emoji-colore completo)** — `Components/Icon.razor` (set Lucide,
>   `currentColor`, aria opt-in, hook `data-icon` per i test). Convertite **tutte** le emoji-colore
>   UI: chrome, home, dashboard/nav, bottoni azione (Anteprima/Differenze/Programma/Pubblica/Nuovo),
>   header viewer (Riepilogo/Collegamenti), sistema callout (`CalloutBlock` + inline `⛔/ℹ️/✅/📭/🌍/…`),
>   badge AIRAC, doc-kind (`VersioniPage.KindIcon`), header operativa. Test `BlockRenderingTests`
>   aggiornato (asseriva l'emoji, ora `data-icon`).
>   **Deferred by design** (non è emoji-icona ma *vocabolario-stato colorato* — serve design a
>   dot/pill colorati, non SVG mono): `🟢` online / `🔴` Live / `🧊` Frozen / `🕒` Scheduled /
>   `🚫🙈👁` toggle nascondi / `🛫🛬` pista-in-uso. Minori lasciati: `ScreensIndex` (indice dev),
>   `⠿` drag-handle, `🔇` (una occorrenza demo ridotta).
>
> **Verify live (2026-07-22):** Host avviato, `/services/vsop`, `/services/vsop/lirr`, `/services/vsop/admin/tasks`,
> `/services/vsop/versions`, `/services/vsop/search` → HTTP 200, 0 errori; icone SVG rese (`data-icon`), **0 emoji**
> nel markup; zoom `aria-live` presente. Baseline test `Vipi.Ui.Tests` 13 verde mantenuta.

## 1. Stato attuale (2026-07-22)

Design system maturo e visivamente coerente in `src/Vipi.Ui/wwwroot/vipi-theme.css`
(~1033 righe, token `--ivao-*`). I problemi non sono estetici ma di **interazione,
accessibilità e manutenibilità**. Rilevati per grep sull'intero `src/Vipi.Ui`:

- **Delete distruttivi:** solo `AdminTrasferimentiPage` conferma (pattern inline
  `_confirmDeleteFlowId` → «Sì, elimina / Annulla»). Tutte le altre azioni distruttive
  cancellano al primo click.
- **Accessibilità:** `aria-*` presente in 4 file su ~70 (solo il chrome `SopLayout`);
  86 emoji-come-icona in 36 file senza `aria-hidden`/label; `alt=` = 0 su SVG/immagini.
- **Zoom:** controllo custom `vipiZoom(±0.1)` con `onclick` inline in `SopLayout` (topbar),
  percentuale in `<span id="vipiZoomPct">` non annunciata.

## 2. Problemi (evidenza)

### P1 — Delete senza conferma, incoerente (🔴 rischio dato)
Cancellazioni immediate, alcune **irreversibili**:
- `VersioniPage.razor:108` — 🗑 «Elimina definitivamente» (irreversibile, no conferma).
- `AeroportiPage.razor:74` `BulkDeleteAssigned`; `:124` `Remove`.
- `AdminTasksPage.razor:84` `Delete`.
- `DocumentSectionsEditor.razor:164/268` `DeleteSection`/`DeleteBlock`.
- Rimozioni in form non-persistiti (annullabili col non-salva): `AeroportoEditorPage`
  Remove×6, `AppSeparations/Vfr/Frequencies/Configurations`, ecc. — **non** critiche.

Incoerenza collaterale: lessico (Elimina/Rimuovi/Rifiuta/Scarta), icona (`✕` vs `🗑`),
stile bottone (`btn ghost` vs `btn ghost danger` vs `btn danger`) per pari gravità.

### P2 — Accessibilità sotto soglia (🔴 → ✅ chiusa il 23 agosto 2026)
- Emoji decorative lette dagli screen reader come testo casuale; rese diverse per OS/browser.
- Bottoni icon-only (`✕`, `🗑` in tabelle) senza nome accessibile affidabile (`title` ≠ nome).
- SVG/mappe (`AorBlock`, `AreaMapBlock`, gallerie MVA) senza `role="img"`/`aria-label`.
- Copertura `aria` quasi nulla fuori dal chrome.

> ✅ **Chiusa dall'[audit frontend/UI del 23 agosto](../history/audit-2026-08-23-frontend-ui.md).** U2 aveva
> già preso le emoji e i nomi accessibili; l'audit ha trovato **quello che questa carta non aveva visto**, e
> vale la pena dire *cosa* le era sfuggito, perché è sempre la stessa cosa:
>
> - **I comandi del blocco AoR non esistevano per la tastiera** — chip `<span>`, «Tutti/Nessuno/Azzera» `<a>`
>   senza href, e per giunta sulle pagine **pubbliche**. La regola era già scritta in `Chip.razor`; il blocco
>   AoR le è sfuggito perché la sua interattività è **JS puro**, non Blazor, quindi nessun giro sui
>   componenti lo toccava.
> - **Nessuna pagina aveva un `<h1>`**, e sulle vIPI titolo e blocchi stavano allo stesso livello. Una carta
>   che cerca «copertura aria» non guarda i tag di intestazione.
> - **`prefers-reduced-motion` non era nominato da nessuna parte** (42 transizioni, un'animazione infinita).
> - **Il fuoco da tastiera era invisibile in tre campi**, dove `outline:0` era voluto ma senza sostituto.
> - **Le live region non venivano annunciate**, perché nascevano insieme al messaggio.
>
> ⚠️ Il filo comune: sono tutte cose che **non si vedono leggendo i componenti uno per uno**. Si vedono
> guardando il documento reso, la tastiera e il foglio di stile — cioè misurando, non ispezionando.

### P3 — Zoom custom senza a11y, possibile ridondanza (🔴)
- La % di zoom non è annunciata (nessun `aria-live`).
- Da confermare se `vipiZoom` è un requisito reale o ridondante col zoom nativo del browser
  (le memorie non lo citano come requisito). Se ridondante → candidato rimozione.

## 3. Architettura target (🟡 da approvare)

### 3a. U1 — `InlineConfirm` (componente condiviso)
Estrae il pattern già collaudato di `AdminTrasferimentiPage` in
`src/Vipi.Ui/Components/InlineConfirm.razor`.

- Parametri: `EventCallback OnConfirm`; `string ConfirmLabel` (default «Sì, elimina»);
  `string CancelLabel` (default «Annulla»); `string? Prompt`; `bool Danger` (default true);
  `bool Disabled`. Stato aperto/chiuso **interno** al componente (niente stato per-riga
  nelle pagine chiamanti).
- **Regola di applicazione (decisa):**
  - **Elimina** = distrugge dato persistito → **conferma obbligatoria** + `btn danger` + icona 🗑.
  - **Rimuovi** = stacca relazione o toglie riga in form non ancora salvato → **immediata**
    (già annullabile non salvando) + `btn ghost` + icona ✕.
- Applicare U1 solo alle **Elimina**: `VersioniPage`, `AeroportiPage` (Remove + BulkDelete),
  `AdminTasksPage`, `DocumentSectionsEditor` (DeleteSection/DeleteBlock).
- `AdminTrasferimentiPage` migra al componente (rimuove `_confirmDeleteFlowId` inline).

> ⚠ Gotcha Blazor (memoria [[dev-process-gates]]): attributo `string` passato senza `@` =
> letterale → i binding `OnConfirm`/`Disabled` vanno con `@`. Verify live, non solo test.

### 3b. U2 — Icone SVG unificate + aria (scelta owner: **SVG unico**)
- Nuovo componente `src/Vipi.Ui/Components/Icon.razor` (o partial di sprite SVG) con un set
  chiuso di icone inline (stroke `currentColor`, `width/height` param, `aria-hidden="true"`
  di default; `Label` opzionale → diventa `role="img"` + `<title>`). Stile coerente con
  l'SVG già presente in `.htree-search`/`.htree-select`.
- Mappatura emoji→icona per i ~86 usi in 36 file: cestino, matita, chiave, ricerca, ecc.
  Le emoji **semantiche** in contenuto redazionale (non UI) restano.
- Bottoni icon-only → `aria-label` esplicito (oltre al `title`).
- SVG di dominio (`AorBlock`, `AreaMapBlock`, MVA) → `role="img"` + `aria-label` o `<title>`.
- `<nav>` senza etichetta → `aria-label`.

> **Nota scope (raffinata in esecuzione):** il problema reale sono le **emoji-colore**
> (🗑🔑📋🛰️🏗️👁🙈📄…), rese in multicolor variabile per OS e illeggibili agli screen reader.
> I **glifi-simbolo monocromatici** (`✕ ✎ ⧉ ▲ ▼ ↑ ↓ ⚠`) rendono in modo stabile ed ereditano
> `currentColor`: **non** vanno migrati a SVG per rendering. Per questi la fix a11y è un
> `aria-label` sul bottone icon-only (follow-up leggero, non bloccante). Lo sweep U2 riguarda
> quindi le sole emoji-colore nei file non ancora toccati.

### 3c. U3 — Zoom accessibile (decisione owner: **tieni + a11y**)
- `vipiZoom` **resta**. Interventi:
  - `#vipiZoomPct` → `role="status"` + `aria-live="polite"` (la % viene annunciata al cambio).
  - Verificare che Ctrl +/− nativo del browser resti funzionante (il controllo custom non lo
    intercetta né lo sostituisce).
  - I bottoni ± hanno già `aria-label` — confermare invariati.

### Non-obiettivi
- Nessun cambio di dominio, schema DB o rotte pagina.
- Nessun redesign visuale: colori/spaziature/tipografia restano.
- Backlog 🟡 (U4→U12) **fuori** da questo scope: annotato §4, eseguito dopo.

## 4. Passi di migrazione (🟡 da approvare)

Slice verticali, 1 commit/passo, `dotnet build` verde a ogni commit, meccanico separato
da logica.

- **U0 — Carta.** Questo doc + riga in `docs/index.md`. *(gate: no codice prima dell'ok)*
- **U1a — `InlineConfirm` (meccanico).** Nuovo componente, nessun consumo ancora.
- **U1b — Applicazione critici.** Versioni, Aeroporti (Remove+Bulk), AdminTasks,
  DocumentSectionsEditor; migra AdminTrasferimenti al componente.
- **U2a — `Icon` (meccanico).** Componente + set SVG, nessuna sostituzione ancora.
- **U2b — Sostituzione emoji→SVG + aria** su chrome, liste, editor (per gruppi di file).
- **U2c — aria su SVG di dominio** (AorBlock/AreaMapBlock/MVA) + `nav` senza label.
- **U3 — Zoom a11y:** `aria-live`/`role="status"` sulla %; verifica Ctrl+/− nativo.

## 5. Impatto / Verifica

- **Schema/rotte:** invariati → nessuno snapshot `spec/modello-dati.md`/`mappa-pagine.md`.
- **Baseline (Fase 1):** `dotnet build` + `dotnet test`, registra conteggio verde; deve
  restare uguale o superiore a fine giro.
- **Verify live (Fase 3, obbligatoria — non bastano i test verdi):**
  - U1: click reale su «Elimina definitivamente» (Versioni) e BulkDelete (Aeroporti) →
    la conferma appare; «Annulla» non cancella; «Sì» cancella una sola volta.
  - U2: passata screen-reader (NVDA) su chrome + una lista + un editor; Lighthouse a11y
    score prima/dopo (atteso in salita).
  - U3: zoom da tastiera nativo funziona; screen reader annuncia la % (se mantenuto).
- **Regressioni Blazor silenziose** (attributo senza `@`, flussi lock/bozza): verify con traccia.

## 6. Gate FEATURE/REFACTOR (pre-flight)

- **Modello (no gemello):** un solo `InlineConfirm`, un solo `Icon`. Non si affianca una
  seconda variante di conferma/icona: si **sostituisce** ciò che c'è.
- **Dispatch (Regola del 2):** la conferma non introduce `switch(tipo)`; la mappa emoji→icona
  è un dato/tabella, non rami.
- **Ingressi + verifica:** definiti in §5.
- **Propagazione (stesso giro):** U1 rimuove `_confirmDeleteFlowId` inline → aggiorna
  commenti + memoria [[editor-uniform-pattern]]. U3-rimozione → togli CSS `.zoom-ctrl` +
  JS + commenti. A chiusura: `history/rounds.md` + `docs/index.md` + questa carta → ✅.

---

## Backlog 🟡 — refactor tema (eseguito 2026-07-22)

Giro «pulizia tema» completato, salvo U4 (decisione owner):

1. **U10 ✅** dedup CSS: rimosse **27 regole morte** — 8 duplicati esatti (`.fl.down`,
   `.aor-chip.on`, `.cfg-btn.on`, `.cfg-table tr.grp-start/tr:hover/cfg-hl:nth-child(2)`,
   `.area-wrap`, `.area-alt b`), 9 divergenti morte (blocco `.cfg-sel/.aor-chip` iniziale,
   vinto dal rework AoR ACC), 10-riga blocco morto `.rule-card`/`.rc-*` (nessun razor lo usa;
   la `.rule-card` viva è quella dell'editor-pista). **Zero cambiamento visivo** (rimosse solo
   regole soverchiate/inutilizzate).
2. **U9 ✅** tokenizzati i colori-testo semantici: `--ok-ink #0f7a37` (22×), `--warn-ink
   #8a6a00` (14×), `--info-ink #2c5d99` (5×), `--nbr-ink #6a3fb5` (3×), `--danger-ink #b51d1d`
   (7×). 51 hex→var, restano solo le 5 definizioni.
3. **U8 ✅ (pattern semantici)** estratte 2 classi ricorrenti: `.choice` assorbe il reset link
   (18 inline `text-decoration:none;color:inherit` rimossi) e `.pill.neutral` (8 inline
   rimossi) = **26 inline eliminati**. Le utility one-off (`margin:0`, `font-size:11px`) NON
   estratte: sarebbe over-engineering contro l'idioma semantico del progetto.
4. **U11 ✅** font con fallback centralizzato: `--font-head` (89×) / `--font-body` (35×)
   sostituiscono `'Nunito Sans'`/`'Poppins'` senza fallback (124 sostituzioni).
5. **U7 ✅** `.live-badge.off` + `.live-badge.off .dot` (rimosso override inline in `SopLayout`).
6. **U6 ✅** componenti condivisi `Components/LoadingState.razor` (applicato a 6 pagine) +
   `Components/EmptyState.razor` (applicato a 4 liste: Aeroporti/APP/vLOA lista+editor). CSS
   `.loading-state` + spinner standalone.
7. **U5 ✅ (con U1/U2)** lessico+icone distruttive: regola in atto — *Elimina* dato = 🗑
   (`Icon trash`) + `InlineConfirm` + `btn danger`; *Rimuovi* relazione/riga = ✕ + `btn ghost`.
8. **U12 ✅** touch target: `.zoom-ctrl button` e `.rowdel` 24/26px → **32px**.
9. **U4 🔵 in corso — decisione owner: «completa l'adozione» (IT+EN)**
   Infrastruttura già presente e ora **provata end-to-end**: `IStringLocalizer<SharedResource>`,
   `Resources/SharedResource.resx` (it) + `.en.resx`, `AddLocalization` + `UseRequestLocalization`
   (it default / en, switch runtime via `?culture=en`, cookie o Accept-Language).
   **Pattern stabilito:** chiavi `Common_*` (riusabili) + `Pagina_*` (specifiche); interpolazione
   con `L["Key", arg]` e `{0}` nel valore; **niente localizer annidato** dentro `$"..."` (rompe il
   parse C#) → calcolare i suffissi in `@code` (vedi `ChangedPage.AccSuffix()`).
   **Convertite (verify live EN 2026-07-22, entrambe le direzioni):** fronte pubblico di navigazione
   `SopHome` (/services/vsop), `AccLanding` (/services/vsop/{acc}), `ChangedPage` (/services/vsop/changed), `SearchPage`
   (/services/vsop/search) + viewer `AccVipiPage` (/services/vsop/{acc}/vipi), `AppnPage` (/services/vsop/{acc}/apps/vipi),
   `AccOperativaPage` (/vsop/{acc}/operativa), `AppOperativaPage` (/vsop/{acc}/operativa-app)
   — *dal 2026-07-31 sono `AccLivePage` (/vsop/{acc}/live) e `AppLivePage` (/vsop/{acc}/live-app),
   chiavi resx `Live_*`/`AppLive_*`; i nomi qui sopra restano come record storico della sessione*.
   **8 pagine, ~145 chiavi IT+EN.** Test 13/13 verdi. `RidottaPage`/`RidottaAppPage` **saltate**:
   disabilitate (nessun `@page`, rotta rimossa Round 12 — `spec/pagine-disabilitate.md`), non user-facing.
   > **Bug pre-esistente trovato e RISOLTO (2026-07-22):** `AccOperativaPage.razor` faceva
   > `(await AccDoc.LoadForViewAsync(_acc.Code))!.Data` → **NRE / HTTP 500** quando l'ACC non ha vIPI
   > pubblicata (LoadForViewAsync torna null). Fix: guard `if (model is null) { _notAvailable = true; return; }`
   > + stato render "Vista operativa non disponibile" (come `AccVipiPage`). Verificato live: operativa
   > ora HTTP 200 (IT+EN). `AppOperativaPage` usava già `acc?.Blocks` (null-safe) → nessun bug lì.
   **Lotto viewer COMPLETO (2026-07-22):** aggiunti `AeroportoPage` (METAR/TAF/piste/SID/TA-TL),
   `VloaDocumentView` (chrome IT localizzato; corpo doc resta EN by-design), e i 6 componenti condivisi
   `App/*` (Frequencies, Minima, CoordinationView, Vfr, Separations, Configurations). Gotcha risolto:
   un `RenderFragment` field-initializer che usa `L` (istanza) va convertito in **property**
   (`=> __builder =>`), non field (CS0236). Build + 13 test verdi, verify live EN OK.
   **Lotto admin (parziale, 2026-07-22):** localizzate `AuditPage`, `DiagnosticaPage`, `ScreensIndex`,
   `StrutturaPage` (grande — helper `OrphansLabel` per la pluralizzazione IT/EN via chiavi `_1`/`_N`),
   `AdminGrantsPage`, `AdminTasksPage`, `TasksPage` (chiavi condivise `TaskStatus_*`/`TaskPrio_*`;
   helper enum-label resi **istanza** per usare `L`). Build + 13 test + verify EN OK.
   **Lotto admin COMPLETO (2026-07-23):** localizzate le 5 restanti — `AccAdminPage` (+ nazioni `Country_*`),
   `SorgentiAdminPage`, `ConfinantiAdminPage`, `AeroportiPage`, `AdminTrasferimentiPage` (~1011 righe, la più grande).
   Chiavi condivise nuove: `Common_Save/Close/Error/UnexpectedError/OpAdminOnly/Yes`. Interpolazioni con conteggi/suffissi
   condizionali risolte con chiavi `{0}`-argomentate + parti-suffisso separate (es. `Apt_*Ok`/`*Fail`, `Xfer_GroupsN`/`_1`);
   plurali IT via helper istanza (`GroupsCountLabel`, `NoAssignedResults`); `KindLabel` reso **istanza** per usare `L`.
   Totale ora **683 chiavi IT+EN, allineate** (diff nomi vuoto). Build + 13 test + verify live EN↔IT OK (le 5 rotte
   HTTP 200, title/breadcrumb/AdminOnly commutano correttamente). **Admin 12/12 fatte.**
   **Lotto editor COMPLETO (2026-07-23):** localizzati tutti i 12 componenti editor —
   componenti condivisi (`PreviewBanner`, `EditLockBar`, `DocReviewBar`, `ReleasePanel`),
   `NewDocumentPage`, `VersioniPage`, `DocumentSectionsEditor`, `VloaEditor`+`VloaEditorPage`,
   `AppEditorPage`, `AccEditorPage`, `AeroportoEditorPage` (~1082 righe, la più grande).
   Chrome editor condivisa in chiavi `Ed_*` (Saving/Saved/DraftVN/Publish/LockedByOther…) riusate da tutti gli editor;
   timeline release in `Rel_*` condivise. Gotcha risolti: (a) `L["x"]` è `LocalizedString` → serve `.Value` negli array
   `string[]` (mesi/giorni di AeroportoEditor); (b) le **chiavi stringa di logica** (`_dirtySections`/`_panels`/switch
   `SaveAllDirty`) restano IT stabili, display via helper `PanelLabel(key)` — non localizzare gli identificatori usati
   come chiavi. Enum-label e helper resi **istanza** dove usano `L`. Totale **1071 chiavi IT+EN allineate**.
   Build + 13 test + verify live EN↔IT OK (le 4 rotte editor HTTP 200, corpo commuta: «Editor vIPI»↔«vIPI editor»,
   «Regole scelta pista»↔«Runway selection rules», ecc.). **U4 i18n: chrome app COMPLETA** (nav+viewer+admin+editor).
   Contenuto editoriale (vIPI/vLOA/aeroporti dal DB) resta IT by-design; termini ATC standard (Delivery/Ground/Tower)
   restano EN. Chiuso il follow-up memoria [[editor-uniform-pattern]] (Release() inline di AppEditor NON migrato a
   ReleasePanel: fuori scope i18n, resta come nota refactor).
   (`AccEditorPage`, `AppEditorPage`, `AeroportoEditorPage`, `VloaEditor`, `VloaEditorPage`,
   `DocumentSectionsEditor`, `VloaDocumentView`, componenti `App/*`); admin (`AccAdminPage`,
   `AeroportiPage`, `ConfinantiAdminPage`, `SorgentiAdminPage`, `AdminGrantsPage`,
   `AdminTrasferimentiPage`, `AdminTasksPage`, `TasksPage`, `AuditPage`, `DiagnosticaPage`,
   `NewDocumentPage`, `VersioniPage`, `ScreensIndex`, `StrutturaPage`, `AccVipiPage`, ecc.).
   Nota: il **contenuto operativo** (vIPI/vLOA/aeroporti) resta dal DB in IT — la localizzazione
   riguarda la **chrome dell'app** (etichette/pulsanti/stati), non i documenti redazionali.
