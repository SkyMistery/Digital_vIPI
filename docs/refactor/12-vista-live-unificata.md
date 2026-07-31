# 12 — Vista live unificata per callsign ✅

> Chiude l'ultimo doppione strutturale dell'asse: due pagine gemelle che switchavano sullo stesso tipo di ente.
> Gemello di [09](09-registri-per-tipo.md) come tecnica (descrittore + registry) e di
> [11](11-uniformita-tre-documenti.md) come intento (un comportamento solo per cose che sono la stessa cosa).
>
> **Fatto il 2026-07-31**, subito dopo la revisione della vista operativa dello stesso giorno.

## 1. Il problema

`AccLivePage` e `AppLivePage` rendevano la stessa pagina — frequenze, AoR, trasferimenti, aeroporto — con lo
stesso identico impianto, differendo solo su tre punti: da dove escono le frequenze, se l'aeroporto è uno solo
o è un elenco di chip, quale documento esteso si linka. È la **«regola del 2»** del `FEATURE-PROCESS`: lo stesso
switch per-tipo in ≥2 posti va estratto in un descrittore + registry.

Conseguenze concrete che si pagavano:

- **Le torri non avevano vista live.** Nessuna delle due pagine le contemplava, e aggiungerle avrebbe voluto
  dire una terza pagina gemella.
- **La chiave era sbagliata.** Le rotte erano `/vsop/{acc}/live` e `/vsop/{acc}/live-app?app=`: l'ACC nel path
  più il callsign nella query, cioè due fonti per la stessa informazione, libere di contraddirsi.
- Un fix a una pagina non arrivava all'altra se non lo si ricordava a mano.

## 2. La decisione

**La vista live è keyed sul callsign.** Non sull'ACC, non su un documento.

```
/vsop/live              → la postazione con cui l'utente è connesso su IVAO
/vsop/live/{callsign}   → quella postazione, in sola consultazione
```

L'ACC si deriva dal callsign (`IStationResolver.ResolveByCallsign`). Il documento non entra nella decisione di
*esistere*: entra solo nella presentazione (vedi doc del 2026-07-31 e memoria `live-view-design`).

### 2a. Descrittore + registry

`ILiveStationKind` — una implementazione per tipo, consultate per `Priority`, prima corrispondenza vince:

| Descrittore | Tipi | Frequenze (membri passati) | Aeroporto | Documento esteso |
|---|---|---|---|---|
| `AreaLiveStation` | `Ctr` (e gli FSS, tipizzati Ctr) | area + gruppi-APP del documento | chip del dominio | vIPI ACC |
| `ApproachLiveStation` | `App` standalone e remotizzato | documento APP / blocco gruppo-APP | fisso | vIPI APP o ACC |
| `AirportLiveStation` | `Twr` `ITwr` `Gnd` `Del` | sé → catalogo dell'aeroporto | fisso | vIPI aeroporto |

Il motore condiviso sta in `LiveStationParts`: i descrittori decidono **cosa** passare, non **come** si calcola.
Aggiungere un tipo = registrare un descrittore in `AddVipiApplication`, zero switch toccati. Un test
(`LiveStationRegistryTests`) verifica che **ogni** `SectorType` del catalogo abbia esattamente un descrittore:
se qualcuno aggiunge un tipo, fallisce lì e non in pagina.

### 2b. Il ritrovamento che rende gratis i tipi nuovi

`EfAccDerivationRepository.DeriveFrequenciesForMembersAsync` espandeva già l'intero catalogo dell'aeroporto
(ATIS · DEL · GND · TWR · APP) per **qualsiasi** membro che sia un settore d'aeroporto — non solo per gli APP,
com'era scritto nel commento. Passare `LIRF_TWR` produce quindi l'elenco che serve a una torre, dalla delivery
all'avvicinamento, senza una riga di derivazione nuova.

### 2c. Trasferimenti: mittente effettivo, non dominio

Regola operativa richiesta: «solo i trasferimenti di quel settore, o dei suoi figli **se chiusi**».

Non è `Topology.DomainOf` — quello include anche i figli online, che invece i propri trasferimenti se li tengono.
È il **mittente effettivo** dopo la risalita della gerarchia, cioè `ResolvedOwnerCallsign == postazione`.

La risoluzione gira sull'insieme online **più la postazione guardata**: senza, consultare una posizione offline
(o la propria prima di collegarsi) farebbe risalire i suoi flussi a un antenato e la pagina risulterebbe vuota
proprio quando serve. Coperto da `LiveStationPartsTests`.

> **Cambio di comportamento visibile**: la vecchia pagina ACC mostrava i flussi di *tutta* l'ACC. Per un CTR
> radice non cambia nulla; per un sotto-settore l'elenco si stringe a ciò che è davvero suo.

## 3. La postazione: nessun selettore

Il selettore in alto a destra è **rimosso**. La pagina dipende dalla postazione che l'utente ha aperto su IVAO,
risolta a ogni tick SSE.

- **Non connesso** non è un errore: è lo stato normale di chi apre la vista *prima* di collegarsi. Si mostra
  l'elenco degli ATC online (cliccabili, in consultazione) e la vista si aggancia da sola appena si va online,
  **senza ricaricare**.
- L'**età del dato ATC** è scritta accanto al conteggio: se il feed è fermo, «non risulti connesso» non deve
  far credere all'utente di essere offline quando è la sorgente a essere vecchia.
- Guardando una posizione altrui compare un banner esplicito, con il ritorno alla propria.

## 4. Rotte e compatibilità

Redirect **301 a un salto solo** (`Program.cs`) — sono pagine che finiscono nei preferiti di chi controlla, e
una catena di redirect si paga a ogni apertura:

| URL storico | Destinazione |
|---|---|
| `/vsop/{acc}/operativa`, `/vsop/{acc}/live` | `/vsop/live` |
| `…?p=LIRR_NE_CTR` | `/vsop/live/lirr_ne_ctr` |
| `/vsop/{acc}/operativa-app?app=X`, `/vsop/{acc}/live-app?app=X` | `/vsop/live/x` |

> **Trappola di routing, bloccata da un test.** `/vsop/live/{callsign}` è una rotta a parametro che ricade sul
> prefisso dello stream SSE `/vsop/live/atc`. La precedenza del routing ASP.NET (segmento **letterale** >
> parametro) manda quell'URL allo stream, non alla pagina — ma è una proprietà che si può rompere cambiando le
> rotte, quindi `SmokeTests.Sse_endpoint_wins_over_the_live_page_route` la verifica.

## 5. Codice morto rimosso nello stesso giro

- `AccLivePage.razor`, `AppLivePage.razor` — sostituite da `LivePage.razor`.
- `RidottaPage.razor`, `RidottaAppPage.razor` — già spente dal Round 12 (`spec/pagine-disabilitate.md`) e mai
  riattivate. La seconda era per metà un mockup hardcoded, quindi non riattivabile comunque.
- 16 chiavi resx orfane (`Live_Position`, `Live_DetectedFromIvao`, la famiglia `AppLive_*` della pagina APP…).

Il callout «Modalità ridotta + live» **non** è stato riportato nella pagina unica: su una vista tenuta aperta
tutti i giorni valeva soprattutto al primo accesso, e la pagina ha già due banner di stato. L'onboarding sta
nella Guida.

## 6. Verifica live (2026-07-31)

Guidata su copia del DB reale, 12 postazioni. Rotte finali 200, redirect **a un salto** verificati
(`/vsop/lirr/operativa` → `/vsop/live`; `…/live?p=LIRR_NE_CTR` → `/vsop/live/lirr_ne_ctr`;
`…/live-app?app=LIBD_CS0_APP` → `/vsop/live/libd_cs0_app`), stream SSE ancora `text/event-stream`.
Selettore postazione assente su **tutte** le pagine. Nessun errore di circuito, nessuno scroll orizzontale.

| Postazione | Esito |
|---|---|
| `LIBB_ES_CTR` (CTR) | titolo dal settore, 6 frequenze, gruppo-APP aperto, chip LIBD/LIBR, doc → `/vsop/libb/vipi` |
| `LIBD_CS0_APP` (APP remotizzato) | pannello aeroporto + SID, catena «sopra di te: LIBB_ES_CTR ·chiuso» |
| `LIBD_TWR`, `LIRF_TWR` (torre) | 3 e 12 frequenze dal catalogo dell'aeroporto, doc → `/vsop/{acc}/airports?icao=` |
| `LIRF_GND`, `LIRF_DEL` | idem, pagina piena senza essere un CTR |
| `LIRR_NE_CTR` (ACC senza vIPI) | banner + 86 frequenze dai cataloghi |
| `/vsop/live` senza connessione | stato d'attesa con gli ATC online cliccabili |
| `ZZZZ_CTR`, `LIBB_CTR`, `LIRF_APP` | «postazione sconosciuta» — non sono nei cataloghi (i due callsign «ovvi» semplicemente non esistono nel DB) |

### Ritrovamento: la catena di copertura è vuota per i tipi che più ne avrebbero bisogno

Interrogando il DB: **nessun** settore `Twr`/`Gnd`/`Del` ha un `ParentSectorId`. Solo `App` (59) e `Ctr` (18)
sono agganciati alla gerarchia. Quindi proprio le postazioni per cui la catena doveva essere l'informazione
principale non ne hanno una.

Un vuoto silenzioso qui è **ingannevole**: «nessuno sopra di me» si legge come un fatto operativo (non ho a chi
passare il traffico) mentre è un dato non ancora compilato in `/vsop/admin/sectorstructure`. La pagina ora lo
dice a parole per le postazioni d'aeroporto — non nasconde la riga.

**Follow-up aperto (dato, non codice):** agganciare TWR/GND/DEL alla gerarchia di copertura. Finché non è fatto,
la vista live di quelle postazioni resta corretta ma monca del pezzo che più le riguarda.

## 7. Il padre dell'aeroporto non arrivava alle sue posizioni (fix del 2026-07-31)

La catena vuota del §6 **non** era un dato mancante: era un legame che nessuno leggeva.

`/vsop/admin/sectorstructure` espone tre generi di nodo — ACC, APP e **Aeroporto** — e il padre impostato sul
nodo Aeroporto finisce in `Airport.ParentCallsign` (29 aeroporti popolati, es. `LIBD → LIBD_CS0_APP`).
La proiezione `EfSectorProjectionService` però derivava `Sector.ParentSectorId` **solo** da
`AirportSector.ParentCallsign`, popolato per i soli APP (58/58) e **mai** per TWR/GND/DEL (0 su 109).

Risultato: l'admin compilava il padre nella UI e per torri, ground e delivery non aveva **alcun** effetto.

### Regola scelta: scaletta interna, poi il padre dell'aeroporto

Una posizione d'aeroporto senza padre proprio sale la scaletta **DEL → GND → TWR → APP**, fermandosi alla prima
posizione davvero presente; in cima esce sul `ParentCallsign` dell'aeroporto.

```
LIBD_TWR → LIBD_CS0_APP → LIBB_ES_CTR          (aeroporto con APP)
LIRL_GND → LIRL_TWR → LIRR_NE_CTR              (aeroporto senza APP: la torre esce sul padre dell'aeroporto)
```

La scaletta è dedotta da `CoverageFor` (sequenza operativa standard), non da un dato scritto. Un
`AirportSector.ParentCallsign` esplicito **vince sempre** sulla scaletta.

**Portata oltre la vista live.** La stessa gerarchia regge la risoluzione dei trasferimenti
(`TransferOnlineResolver` risale `ParentSectorId`): un punto verso una torre offline terminava su **UNICOM**
invece di salire all'avvicinamento — cioè la vIPI avrebbe detto «rilascia a UNICOM» con l'APP online che quel
traffico lo possiede. Nel DB attuale i punti verso TWR/GND/DEL sono **0**, quindi era latente, non un danno in
corso; ma è la stessa classe di errore.

**Riproiezione all'avvio** (`ProjectVipiSectors`, idempotente come le altre riconciliazioni di boot): senza,
il cambio di regola sarebbe entrato in vigore solo al prossimo import, cioè un giorno dopo.

Effetto misurato sul DB reale — `Del` 0→**5/5**, `Gnd` 0→**20/20**, `Twr` 0→**51/84**:

```
LIBD_TWR → LIBD_CS0_APP → LIBB_ES_CTR
LIRF_DEL → LIRF_GND → LIRF_E_TWR → LIRF_AEM_APP → LIRF_TW1_APP → LIRR_TS_CTR → LIRR_NE_CTR
```

### ⚠️ Due limiti che restano, entrambi di dato

1. **33 torri ancora orfane**: aeroporti senza APP **e** senza `ParentCallsign` compilato (LICB, LICZ, LIEA,
   i vari `*_I_TWR`…). La scaletta non ha nulla su cui uscire — e non inventa: la pagina lo dichiara.
   Si risolve compilando il nodo Aeroporto in Struttura.
2. **Aeroporti con posizioni sdoppiate** (LIRF 6 APP + 2 TWR, LIMC 4+2, e altri 7): la scaletta deve *scegliere*
   fra pari grado e lo fa in ordine alfabetico — `LIRF_GND → LIRF_E_TWR` e `LIRF_TWR → LIRF_AEM_APP` sono
   **arbitrari**, non un dato. Peggio: TWR/GND/DEL **non sono nodi editabili** in `/vsop/admin/sectorstructure`
   (solo ACC, APP e Aeroporto lo sono), quindi oggi quella scelta non è correggibile dalla UI.
   Per gli aeroporti a posizione singola — la maggioranza — la scaletta è invece esatta.
