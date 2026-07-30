# Feature — TOC laterale sezioni negli editor (menu di navigazione sempre disponibile)

Data: 2026-07-29 · Stato: FATTO (build + 460 test verdi). Include: TOC laterale, rail azioni a destra, fix doppio header sticky, UX lock documento. Live-verificato su :5034 (aeroporto); chip lock ACC/App/vLOA da provare con login/secondo utente.

## Obiettivo
Nelle pagine di editing un **menu-sezioni laterale sticky** deve essere sempre disponibile
durante lo scroll: click su una voce → salta alla sezione; badge "non salvato" opzionale.
Uniforme su tutti gli editor. Scelto rispetto alla topbar-sticky perché più comodo per il
lavoro editoriale (documenti lunghi, salto diretto tra sezioni). Vedi memoria `editor-uniform-pattern`.

## Stato di partenza (rilevato)
| Editor | Nav sezioni | Anchor id sezioni |
|---|---|---|
| `AeroportoEditorPage` | `nav.editor-nav` orizzontale **non-sticky** | sì `#sec-{Anchor(panel)}` su `<details>` |
| `AccEditorPage` | nessuna | sì `s-{id}` (via `DocumentSectionsEditor`), blocchi senza id |
| `AppEditorPage` | nessuna | sì `s-{id}`; release `p-release` |
| `VloaEditor` (componente) | nessuna | sì `s-{id}` |
| `StrutturaPage` | ha già `struct-nav` (fuori scope) | — |

- Viewer (`AccVipiPage`) usano `.doc-layout` (grid `248px | 1fr | 308px`) con `<aside class="toc">` sticky. CSS `.toc` pronto.
- CSS `.editor-toc` (pill orizzontali) e `.ed-layout` **orfani** in `vipi-theme.css` (nessun razor li usa) — resti di un tentativo.
- `DocumentSectionsEditor` è l'editor sezioni CONDIVISO (App/Vloa/Acc-per-blocco/Airport-extra): ogni sezione top-level è un `CollapsibleBlock Id="s-{s.Id}"` → anchor già presente.

## Design
- Record `EditorTocItem(AnchorId, Label, Dirty, Level, GroupLabel?)`.
- Componente `EditorToc.razor`: rende `<aside class="toc">` sticky dagli item (raggruppa per `GroupLabel`, badge dirty).
- **App/Vloa**: `DocumentSectionsEditor` guadagna `ShowToc` (opt-in) + `ExtraTocItems`. Quando attivo, avvolge il proprio output in `.ed-layout` (toc | contenuto) e costruisce gli item dalle sezioni top-level (`RootSections ?? Doc.Sections`). App passa l'item release (`p-release`) via `ExtraTocItems`.
- **ACC**: TOC a livello pagina (raggruppato per blocco/gruppo); id `blk-{id}` aggiunti alle card blocco; i `DocumentSectionsEditor` per-blocco restano `ShowToc=false`.
- **Aeroporto**: `nav.editor-nav` sostituita da `EditorToc` in `.ed-layout`; anchor `sec-*` invariati; pulsanti Espandi/Comprimi tutto spostati nel TOC.
- CSS: `.ed-layout` grid 2-col (base) — evita la regola rail 3-col di `.doc-layout`; collasso <1080px già presente.

## Perché non affianca un modello (FEATURE-PROCESS Q1/Q2)
- Nessun nuovo modello documento: gli item TOC sono proiezione delle sezioni già in memoria (nessuna query DB → nessun rischio "second operation").
- Nessun nuovo `switch(tipo)`: `EditorToc` è un renderer dato-guidato; ogni pagina costruisce la lista item.

## Passi (slice verticali, build verde a ogni passo)
1. Modello `EditorTocItem` + componente `EditorToc.razor` + CSS `.ed-layout`. (infra)
2. `DocumentSectionsEditor`: `ShowToc`/`ExtraTocItems` + wrap. Wire App + Vloa. (slice 1)
3. `AccEditorPage`: EditorToc pagina + id blocco. (slice 2)
4. `AeroportoEditorPage`: editor-nav → EditorToc. (slice 3)
5. Verifica live guidando ogni editor.

## Estensione — rail azioni a destra (2026-07-29)
Richiesta owner: spostare TUTTI i pulsanti in alto a destra (Preview draft, Modifica/Finish, Pubblica, badge stato,
Salva tutto, Reimport…) in un **rail sticky a destra**, come la `doc-rail` del viewer. La barra in alto resta col solo titolo.
- Griglia editor diventa 3 colonne: `.ed-layout.with-rail` = `248px | 1fr | 232px`; `<aside class="ed-rail">` sticky.
- **Il rail è l'ULTIMO figlio della griglia** (ordine DOM = ordine colonne): toc, contenuto, rail.
- App/vLOA: nuovo slot `RightRail` (RenderFragment) su `DocumentSectionsEditor`; la griglia passa a 3-col se presente.
- ACC/Aeroporto: `<aside class="ed-rail">` aggiunto a mano come 3° figlio della griglia pagina.
- Pulsanti/badge nel rail a piena larghezza (`.ed-rail .btn/.save-badge/.pill{width:100%}`).

## Fix — doppio header sticky (2026-07-29)
Segnalazione owner: scrollando restava un pezzo dell'`editor-bar` locale che copriva la cima del TOC.
Causa: due sticky sovrapposti — la **topbar globale** (`.topbar`, 62px, l'unico header che deve restare) + l'`editor-bar`
locale sticky `top:0` ormai inutile (le azioni sono nel rail). Fix:
- `.editor-bar` → **statico** (rimosso `position:sticky`), sia in CSS che l'inline dell'aeroporto.
- TOC/rail restano sticky `top:70px` (8px sotto la topbar) → niente sovrapposizione.
- `scroll-margin-top` ancore 150→**84px** (solo la topbar da scavalcare).

## Estensione — UX lock documento (2026-07-29)
Domande/ritocchi owner sul workflow bozza/lock (`EditingService`, TTL 30 min, sliding renew a ogni mutazione):
1. **"Saved" su Edit era fuorviante** (era solo acquisizione lock via lo stesso `Guard` del salvataggio). Separato
   `Guard(action)` (con badge, firma a 1 param preservata per `Run=` di `DocumentSectionsEditor`) da
   `GuardCore(action, silent)`. `StartEditing` ora è `silent` → niente badge.
2. **"Fine modifica" ora rilascia il lock** (`FinishEditing` → `ReleaseLockAsync` + re-inspect): il documento è subito
   libero per gli altri, non aspetta i 30 min di TTL. (Prima `_editing=false` lasciava il lock agganciato.)
3. **Lock visibile nel rail** (ACC/App/vLOA):
   - **tuo** (`IsMine`, in edit) → chip verde `.lock-mine` "🔒 Stai modificando · lock fino alle HH:mm" (chiavi `Lock_Editing`/`Lock_UntilSuffix`, già IT+EN).
   - **di un altro** (`Locked && !IsMine`) → chip rosso `.lock-badge` "🔒 {nome} · → HH:mm" + **Edit disabilitato** + banner in alto (già esistente).
   - libero → solo Edit.
   Riusate chiavi i18n esistenti, nessun resx nuovo. Aeroporto escluso (edit-diretto, senza lock/bozza).
- **Rail badge a capo**: `.ed-rail .save-badge/.pill{white-space:normal;border-radius:10px}` — i 232px stretti tagliavano
  "Stai modificando · lock fino alle HH:mm".

## Verifica
- `dotnet build` verde a ogni slice.
- `dotnet test` verde: **460** full (23 Domain + 216 Application + 13 Hosting + 13 Ui + 191 Infrastructure + 4 E2E),
  incl. `AuthLockTests`/`EditingRepositoryTests` per il lock.
- Live (:5034): aeroporto editor rende `ed-layout with-rail` + `ed-rail` (pill stato + Preview draft + Reimport + meta);
  TOC a sinistra con 8 ancore + footer Espandi/Comprimi; `.editor-bar` non-sticky; CSS `lock-badge`/`lock-mine` serviti.
  ACC/App/vLOA con chip lock e disabilitazione Edit richiedono login/secondo utente per la prova completa.

## File toccati
- Nuovi: `src/Vipi.Ui/Components/EditorTocItem.cs`, `EditorToc.razor`; `docs/feature/2026-07-29-toc-editor.md`.
- Modificati: `DocumentSectionsEditor.razor` (ShowToc/ExtraTocItems/RightRail), `AccEditorPage.razor`, `AppEditorPage.razor`,
  `VloaEditor.razor`, `AeroportoEditorPage.razor`, `wwwroot/vipi-theme.css`.
