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
- **A7 — il lock si rilegge al clic** (chiesta dal committente dopo la prima consegna, vedi sotto).

## Fatto, e cosa ha insegnato guidarla

Verificata su copia del `vipi.db` con **due lock veri** scritti nel DB (uno mio, uno di «Giulia Bianchi»),
a 1600/1440/1280/1024, IT **ed** EN, zoom 0.8→1.5. Tutto quello che la carta prometteva risponde: badge
«chi · ora», nascondi/elimina spenti sul lock altrui **col perché nel tooltip**, force-unlock che libera
davvero (la riga sparisce dal filtro «in modifica» e compare «Lock rilasciato»), conferme in linea che
portano il **titolo del documento**, **zero** dialoghi nativi, zero errori di console.

**Quattro difetti che nessuna asserzione cercava**, due visti solo **guardando** lo screenshot:

1. ⚠️ **La riga documento si sfasciava con una conferma aperta.** `InlineConfirm` entra *dentro* la riga flex
   e si prende ~500px: senza un pavimento, il titolo col suo `flex:1` si comprimeva finché
   «vLOA — LIBB ↔ LAAA» non andava a capo **una parola per riga**, e le pill «AIRAC 2607» con lui. La pagina
   non sforava e non era più alta: **nessun numero lo diceva**.
2. ⚠️ **Il pavimento giusto è 200px, non 260.** A 260 la riga **più carica** (bozza + senza release + lock +
   apri + nascondi + cestino + sblocca ≈ 1.053px dentro i 1.072 utili a 1280) andava a capo lo stesso — e una
   riga alta il doppio è esattamente il difetto che il giro sta togliendo. Misurato, non stimato.
3. ⚠️ **Un badge lungo manda a capo i tasti.** «🔒 Stai modificando · lock fino alle 00:48» misurava 210px.
   Il badge dice «chi · ora» (89/147px); la forma distesa vive nel **tooltip**, dove non costa niente.
4. ⚠️ **Due chiavi, una traduzione.** `Ver_NoReleaseBadge` aveva lo **stesso valore** nei due `.resx`:
   «No release» compariva in inglese anche in pagina italiana. Il test che confronta le **chiavi** dei due
   file non lo vede — le chiavi c'erano entrambe.

Resta alta 118px **una sola riga**: quella con un lock altrui *e* i diritti da admin, che è l'unica a portare
sette elementi più il force-unlock. È il caso peggiore, e comprimerle il titolo sarebbe peggio.

⚠️ **Lo sforo orizzontale a 1280/1024 non è di questa pagina**: è `div.right` della topbar (1.385px), identico
sulla home, e **niente dentro il `.wrap` sfora**. Verificato elencando gli elementi oltre il bordo.

## A7 — «è normale che il lock si veda solo se ricarico?»

Sì, ed era **sicuro**: la guardia sta nel service, quindi un lock nato dopo il caricamento fa fallire
l'azione con un messaggio, non un danno. Ma la domanda («eliminare definitivamente?») veniva **posta lo
stesso**, e l'occupato si scopriva solo *dopo* aver confermato.

**Scelta: si rilegge il lock di UN documento al clic**, non tutta la lista e non a intervalli. Rileggere
tutto a ogni giro costa una query su tutti i documenti per un dato che cambia poche volte al giorno;
rileggere **quello che si sta per toccare, nel momento in cui lo si tocca**, costa una riga.

Servirebbe un gancio prima dell'apertura, e `InlineConfirm` non ne aveva: ora ha `CanOpenAsync`.
⚠️ È un `Func<Task<bool>>` e **non** un `EventCallback`, perché serve la **risposta** — un `EventCallback`
non ne restituisce, e leggere `Disabled` dopo l'`await` non funzionerebbe: i parametri arrivano al render
successivo del genitore, non al ritorno della chiamata. È additivo: gli altri 13 usi non cambiano.

⚠️ **Questo non sostituisce la guardia.** Fra la lettura e la scrittura passa comunque un istante, e una
scheda vecchia non passa di qui: il divieto resta in `DocumentAdminService`. Serve a non fare una domanda
la cui risposta verrà comunque rifiutata.

Nello stesso giro: il callout d'errore aveva **un titolo solo** — «Operazione non consentita» — anche per un
conflitto di lock. Ma un documento occupato non è un permesso negato, ed è un'altra reazione (aspetto un
minuto, non chiedo un grant): ora il titolo cambia col ramo d'errore.

**Verificato guidando il caso vero** (`report5.json`): pagina caricata **senza** lock → il lock nasce da
fuori, come da un'altra scheda → la pagina non lo sa (è una fotografia, atteso) → al clic sul cestino la
conferma **non si apre**, il badge 🔒 compare, il cestino si spegne e il callout dice «Documento occupato».

## Parte B — la densità, che resta da fare

**È il prossimo lavoro del ramo**, ed è la stessa pagina: il briefing misurato (inventario, decisioni aperte,
cosa è cambiato con questo giro) sta in
[`docs/history/handoff-densita-ui.md`](../history/handoff-densita-ui.md) §«Versioni», non duplicato qui.

In una riga: `.wrap` → `.wrap.struct` (da decidere), testata in riga con «?», i 3 callout in fascia →
`.st-msg`, ~27 stili in linea → classi, chip filtro `.sh-chip` **che contano**, conteggio come pill accanto
al titolo.

⚠️ **Due cose che questo giro ha lasciato alla parte B:**
- i chip filtro sono ora **quattro gruppi** (tipo, stato, release, **ACC**), non tre: la barra è cresciuta e
  la conversione a `.sh-chip` vale più di quanto diceva la ricognizione del 19;
- **una riga resta alta 118px** invece di 67 — quella con un lock altrui *e* i diritti da admin (sette
  elementi più il force-unlock). Se la densità accorcia le etichette si richiude da sé; comprimerle il titolo
  no, quello è il difetto che si è appena tolto (regola 100).

⚠️ **Le emoji di stato (🟢 🕒 🕓 ⚠️ 🔒) non si toccano senza riconferma.** Stanno nel markup e non nei `.resx`
— quindi si tolgono senza toccare le traduzioni — ma sono **vocabolario di stato**, non comandi, e la regola
40 salva solo le emoji-comando. La decisione in vigore è tenerle finché non c'è il set di pallini colorati
(deferito in `piano-ux-hardening`). **Domanda aperta al committente al 21 agosto 2026.**
