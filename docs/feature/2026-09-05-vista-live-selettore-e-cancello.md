# La vista live si sceglie — ma solo da chi può — 5 settembre 2026

> Richiesta del committente: *«Per chi è almeno division staff vorrei che nella visuale live fosse
> possibile selezionare una qualsiasi postazione per vedere l'interfaccia come se fosse live quella
> postazione. Mentre per gli utenti normali non voglio la possibilità di selezionare la pagina di un ente
> aperto che non sono io.»*

Sono **due** cose, e la seconda è la più importante: oggi `/services/vsop/live/{callsign}` è **aperta a
chiunque**, anonimi compresi. Il selettore è la parte facile — il modello si compone già dai cataloghi,
quindi «come se fosse live» funziona già su una postazione spenta.

## 0. ⚠️ Questa carta CAMBIA un principio scritto

La memoria `live-view-design` e la testata di `LivePage.razor` dicono, dal 31 luglio, **«nessun selettore
di postazione: la vista segue te»**. Resta vero per l'utente normale — anzi, ora è vero *davvero*, perché
prima bastava scrivere un callsign nell'URL. Per DivisionStaff in su il principio cade, e va riscritto
nello stesso giro (domanda 4 del pre-flight): memoria, commento di testata, commento in `CaricaAsync`.

## 1. Le decisioni del committente (5 settembre)

| domanda | risposta |
|---|---|
| Che cosa apre un utente normale | **solo la propria postazione**, online o spenta che sia. Qualunque altro callsign è negato. |
| Che cosa vede chi non è autorizzato | **rimando a `/services/vsop/live`**, silenzioso. Nessuna fascia, nessuna conferma che quel callsign esista. |
| Che elenco propone il selettore | **tutte le postazioni dei cataloghi**, con ricerca; gli enti aperti adesso in cima. |

La regola scartata era «blocca solo gli enti *aperti* altrui»: letterale alla richiesta, ma il cancello
avrebbe cambiato risposta a ogni tick SSE — un ente che apre ti sbatte fuori mentre guardi. «Solo la mia»
è una regola che non dipende dal tempo.

## 2. Il cancello sta in DUE sedi, come sempre

- **Servizio** — `LiveViewService.BuildAsync`: se il callsign non è il mio e non sono almeno
  DivisionStaff, torna `LiveViewResult.NotAllowed`. È l'unica sede che conta davvero: la pagina è
  l'unico chiamante *oggi*.
- **Pagina** — `LivePage`: su `Denied` fa `NavigateTo("/services/vsop/live", replace: true)`.

Il livello si chiede a `IEditAuthorizationService.IsDivisionStaff`: **zero query**, come tutti gli altri
cancelli dal §U.

⚠️ **`replace: true`**: senza, il tasto «indietro» rimanda sull'URL negato e il rimando riparte in loop.

## 3. I gesti che smetterebbero di fare qualcosa

Un cancello che nega e basta lascia in pagina tre link che, per l'utente normale, diventano **gesti che
non fanno niente** (memoria `gesto-piu-corto`). Vanno spenti alla fonte, non lasciati rimbalzare:

1. **Stato d'attesa** — l'elenco degli ATC online è cliccabile. Per chi non può, resta l'elenco (dice che
   il feed è vivo e chi c'è) ma **senza link**.
2. **Catena di copertura** — le pastiglie `xo-chip` puntano al live di chi ti copre. Stesso trattamento:
   testo, non link. L'informazione — *chi* ti copre — resta intera.
3. **`AppnPage`** — il tasto «Operativa» punta a `/live/{app}`. Si mostra solo a chi quel live lo può
   aprire.

## 4. Il selettore (DivisionStaff+)

`ILiveViewService.ListStationsAsync()` — `EnsureAtLeast(DivisionStaff)` **dentro**, e riusa la query che
c'è già (`ListSectorNodesAsync`, settori **attivi**, ordinati per callsign): nessuna interrogazione nuova
al database, nessun modello gemello. Torna `LiveStationOption(Callsign, AccCode, Online)`.

⚠️ **Si carica a richiesta, una volta sola** — non in `CaricaAsync`, che il callback SSE richiama a ogni
giro del poller: una query per tick sarebbe la stessa corsa sul `DbContext` di circuito che questa pagina
ha già pagato (`_caricamento`).

Forma: un **campo a digitazione nella testata**, ed è `TypeaheadPicker` — il campo con elenco del prodotto,
frecce ed Esc compresi. Non se ne scrive un settimo (regola del 2: nell'editor trasferimenti era già stato
scritto sei volte). La scelta **cambia la rotta**, non lo stato: `NavigateTo("/services/vsop/live/{callsign}")`,
così la pagina si ricompone da `OnParametersSetAsync` come per qualunque altro arrivo e l'indirizzo si può
mandare a qualcuno.

## 5. Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo: `LiveViewResult` cresce di un flag, e `LiveStationOption` è una
   riga di elenco, non un'entità. Il livello è quello del §U.
2. **Dispatch** — nessuno switch per tipo: il cancello è un `>=` su `VipiRole`.
3. **Ingressi + verifica** — ingresso: la testata della vista live. Verifica: `DevIdentityOptions` a tre
   identità (User scollegato, User **connesso**, DivisionStaff) guidando `/services/vsop/live/{callsign}` —
   è **esattamente** il caso che i test non vedono, come i tre difetti del §U. Esito nel §7.
4. **Propagazione** — §0: memoria `live-view-design` (principio 2), testata di `LivePage.razor`,
   commento di `CaricaAsync`, `docs/refactor/12-vista-live-unificata.md`.

## 6. Le slice

| # | che cosa | stato |
|---|---|---|
| 1 | Cancello: `Denied` nel risultato, guardia nel servizio, rimando nella pagina | ✅ |
| 2 | I tre gesti spenti per chi non può (attesa, catena, `AppnPage`) | ✅ |
| 3 | Selettore per DivisionStaff+ (`ListStationsAsync` + `TypeaheadPicker`) | ✅ |
| 4 | Propagazione: memoria, commenti, doc 12, lavori aperti | ✅ |

## 7. ✅ Verificato live, e le due cose che i test non vedevano

Guidando `dotnet run` su copia del `vipi.db` a **tre identità** (`DevIdentity`), con `curl` per il cancello —
che scatta al **prerender**, quindi si legge come un 302 vero — e Edge+puppeteer per il selettore.

| identità | `/live/{altrui}` | `/live` (la propria) | selettore | link al live altrui |
|---|---|---|---|---|
| VID 123456, `XX-ZZ9` (User), non connesso | **302** → `/live` | stato d'attesa | assente | **0** |
| VID 778116, `XX-ZZ9` (User), **connesso LIEE_TWR** | **302** → `/live` | «Elmas Tower», pastiglia verde | assente | **0** |
| VID 123456, `IT-T01` (DivisionStaff) | **200**, pagina intera | — | presente | 5–6 |

Lo staff ha scelto `LIRR_ES_CTR` dal selettore: l'indirizzo è cambiato, la pagina si è ricomposta
(«Roma — Roma Radar»), console pulita.

🔴 **Due cose che la suite non poteva vedere, tutte e due a schermo:**

1. **L'ordine del selettore era sbagliato**, e sembrava giusto: «prima le aperte» metteva, digitando `LIRR`,
   otto torri d'aeroporto della ACC di Roma davanti a `LIRR_CTR`, che non si vedeva. Ora prima chi combacia
   nel **callsign**, poi chi combacia solo nella **ACC**, e le aperte in cima **dentro** ciascun gruppo.
2. **La pastiglia verde «Live ·» diceva il falso** proprio nel caso nuovo: da quando lo staff apre una
   postazione **spenta**, un verde fisso la dichiarava aperta. Ora è verde solo se quel callsign è online,
   altrimenti grigia con «chiuso».
