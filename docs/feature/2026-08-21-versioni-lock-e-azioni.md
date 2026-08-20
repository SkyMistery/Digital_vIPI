# Versioni — chi ci sta lavorando, e le azioni delicate (21 agosto 2026)

> Ottava pagina del ramo `ui-trasferimenti-densita`, ma **questa carta non è (ancora) di densità**: l'analisi
> del 21 agosto ha trovato in `/vsop/versioni` tre buchi di **sostanza** che vengono prima della forma —
> si può **eliminare un documento che un'altra persona sta editando**, «nascondi» non chiede niente,
> «elimina» chiede **due volte**. La densità è la parte B, dopo.

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo. Il lock del documento **esiste già** su `Document`
   (`LockedByUserId` / `LockedByName` / `LockExpiresUtc`, TTL 30 min, `IEditingService`): qui si **mostra e si
   rispetta**, non se ne inventa un secondo. `ManagedDoc` prende i tre campi già letti dalla stessa query.
   ⚠️ Tolto un **gemello** trovato strada facendo: `HasEffectiveRelease` (bool) e il riepilogo release che la
   pagina ricaricava per conto suo dicevano **la stessa cosa** con due query. Ora `ManagedDoc` porta
   `EffectiveCycle` / `NextScheduledCycle` e `HasEffectiveRelease` è **calcolato** da lì: un fatto, un posto.
2. **Dispatch** — nessuno `switch(tipo)` nuovo. Il permesso per riga si risolve sull'`AccCode` del
   `ManagedDoc`, che nei quattro descrittori **è già** l'ACC usato da `AuthAccCodeAsync` (verificato in tutti
   e quattro): non si duplica la risoluzione per-tipo.
3. **Ingressi + verifica** — la pagina si raggiunge da `/vsop` e dal breadcrumb; nessuna rotta nuova.
   Verifica: test (Application + Infrastructure) **più** guida live del flusso vero, con **due sessioni**:
   una tiene il lock su un documento dall'editor, l'altra guarda l'elenco.
4. **Propagazione** — `HasEffectiveRelease` cambia da parametro posizionale a proprietà calcolata: i 10 punti
   che lo leggono non cambiano una riga, ma il commento del repo che lo spiegava sì. Nessun nome muore.

## Cosa non va, verificato sul sorgente

| | Difetto | Dove |
|---|---|---|
| 1 | ⚠️ **Hide e delete ignorano il lock**: `DocumentAdminService` controlla solo il grant ACC → si elimina il documento **mentre un'altra persona lo sta editando**, e quella lo scopre al salvataggio | `DocumentAdminService.cs:31-41` |
| 2 | L'elenco **non dice chi sta lavorando a cosa**, benché il dato sia già in memoria (`ListAsync` carica i `Document` interi) | `EfDocumentAdminRepository.cs:26` |
| 3 | **Elimina chiede due volte**: `InlineConfirm` + `window.confirm` nativo — e il testo utile (titolo, «rimuove versioni e release») è nel **nativo**, che blocca il circuito e manda in stallo la verifica live | `VersioniPage.razor:108`, `:587` |
| 4 | **Nascondi non chiede niente**: cambia la visibilità pubblica al clic | `VersioniPage.razor:578` |
| 5 | «✕ Annulla» release usa il `confirm` nativo, e **riporta il pubblico alla release precedente** | `VersioniPage.razor:550` |
| 6 | «Pubblica ora» **non chiede niente** e scavalca il ciclo AIRAC | `VersioniPage.razor:492` |
| 7 | **Gate dei permessi divergente**: il markup mostra hide/delete solo a `IsAdmin`, il servizio autorizza per **grant ACC** → chi ha il grant può farlo, ma non vede il tasto | `VersioniPage.razor:100` |
| 8 | **Doppia query di release**: `ListAsync` calcola già i riepiloghi, la pagina li richiede **di nuovo** in `LoadSummariesAsync` | `VersioniPage.razor:189` |
| 9 | Nessun modo di filtrare per **ACC** (la rotta `/vsop/{acc}/versioni` esiste) né di vedere **cosa è in modifica adesso** | — |

## Le decisioni

- **Il blocco sta nel SERVICE, non nel bottone.** Un tasto `disabled` non è una guardia: chi arriva da
  un'altra scheda, o con la lista vecchia in mano, passa lo stesso. `SetHiddenAsync`/`DeleteAsync`
  rileggono il lock e sollevano `EditConflictException`. Il tasto spento è solo cortesia.
- **Chi elimina**: admin **e responsabili di quell'ACC** (decisione del committente, 21-ago). Il servizio
  era già così; si allinea il **markup**, che era più stretto.
- ⚠️ **Il lock del documento dura 30 minuti e non ha battito** (a differenza di `EditResourceLock`: 3 min +
  heartbeat). Si rinnova al salvataggio e si libera con «Fine modifica». Chi chiude la scheda lascia il lock
  in piedi fin quasi a mezz'ora → **senza force-unlock la pagina si autobloccherebbe**. Il force-unlock del
  documento esiste già lato service (`IEditingService.ForceUnlockAsync`, solo admin) e non era esposto da
  nessuna UI: lo si mette in riga, dietro conferma.
- ⚠️ **L'editor aeroporto non prende il lock del documento** (`AeroportoEditorPage` usa
  `IAirportEditingService`, non `IEditingService`): sugli aeroporti il badge non comparirà **mai** e hide/delete
  non saranno mai inibiti. È un buco **dichiarato**, non chiuso qui: portare l'aeroporto sul lock è un giro suo.
- **L'elenco è una fotografia**: un lock preso dopo il caricamento non si vede. Si aggiunge un **ricarico**
  (tasto, e automatico dopo ogni azione). Niente poll continuo: sarebbe una query su tutta la lista ogni pochi
  secondi per un'informazione che cambia due volte al giorno.
- **Conferme uniformi**: tutte in linea (`InlineConfirm`), **zero `window.confirm`**. Il testo che spiega la
  conseguenza sta nella conferma in linea, non nel prompt del browser.
- **«Mostra» resta immediato**: rimettere a vista non distrugge niente. Conferma solo su **nascondi**.

## Le slice

- **A1 — il dato.** `ManagedDoc` porta lock (`LockedByUserId`/`LockedByName`/`LockExpiresUtc`) e cicli
  release; `HasEffectiveRelease` diventa calcolato; il repo li popola **senza query nuove**. La pagina smette
  di rifare `SummariesAsync`.
- **A2 — la guardia.** `DocumentAdminService` rifiuta hide/delete se il lock è di un altro.
- **A3 — le conferme.** Via i tre `window.confirm`; conferma su nascondi e su «pubblica ora».
- **A4 — il badge e i tasti.** 🔒 in riga con nome e ora di scadenza; hide/delete/pubblica/scarta spenti se il
  lock è altrui; force-unlock admin; tasto «Aggiorna».
- **A5 — filtri.** Chip stato «in modifica», chip **ACC**, contatore 🔒 nella fascia di riepilogo.
- **A6 — permessi.** Il markup mostra hide/delete a chi **può editare quell'ACC**, non solo agli admin.

## Parte B — densità (dopo)

`.wrap` → `.wrap.struct`, testata in riga con «?», i 3 callout in fascia → `.st-msg`, 27 stili in linea →
classi, chip filtro `.sh-chip` **che contano**, conteggio come pill accanto al titolo. Le emoji di **stato**
(🟢 🕒 🕓) restano finché non c'è il set di pallini colorati: sono vocabolario, non comandi (regola 40).
