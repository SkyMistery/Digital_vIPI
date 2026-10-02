# Tabellone partenze/arrivi: l'endpoint dei dati — carta (2 ottobre 2026)

> **Stato: 🔨 IN CORSO** sul ramo `fix/tabellone` (filone Sito, S96). Nessuna migrazione, nessun `deploy/`.
> **Codice comune toccato**: `Vipi.Application` (cartella `Tabellone/`, e `SourcePilotFix` con il campo facoltativo
> `Plan`).
> Richiesta del committente (2 ottobre 2026): l'endpoint che alimenta il tabellone a palette partenze/arrivi. Il formato
> è già concordato con lui in `D:\Programmazione\IVAO_Test\Dep_arr_board\FORMATO-DATI.md` (versione 2): §1–§5 la
> risposta, §6 lo stand effettivo dal Gate Manager, §7 le fonti con le loro trappole. La grafica la fa un'altra chat
> in quella cartella; qui solo i dati.
>
> Dove sta: `RegoleTabellone` (Application: fusione, stati, stand, finestra, uscite, ordine; pura) · `BookingParser`
> e `BoardDelPonte` (Application: le due fonti lette) · `FotografiaPiloti` (Application: i piloti del poller) ·
> `BookingItClient` (Infrastructure) · `Tabellone` + `ServizioTabellone` (Hosting: rotta, cache, raccolta delle fonti)
> · `AtcPollingHostedService` (pubblica i piloti) · `CancelloDelLogin` e `RegistroRichieste` (Host). Test:
> `Vipi.Application.Tests/TabelloneTests` (con `Fixtures/tabellone/`), `Vipi.Hosting.Tests/ServizioTabelloneTests`,
> `Vipi.E2E.Tests/TabelloneEndpointTests`, più un caso in `WhazzupClientTests`, `FotografiaFermaTests` e
> `CancelloDelLoginTests`.

## 1. Le decisioni del committente (2 ottobre 2026)

1. **`GET /api/tabellone/{ICAO}` pubblico, solo lettura, senza chiave.** È un'eccezione voluta alla regola «le API
   non sono mai anonime» (carta `2026-09-13-chiavi-api.md`): lo legge uno schermo, e i dati sono quelli che chiunque
   vede già su Whazzup e sul sito del booking, **senza i VID**. Fuori dal login obbligatorio come il ponte RFO.
2. **Una risposta in cache al più ogni 15 s, uguale per tutti.**
3. **Whazzup dalla lettura che vIPI fa già** (`AtcPollingHostedService`, una al minuto): nessuna lettura in più.
4. **Booking di IVAO Italia** con la chiave nei segreti del server, mai nel repo.
5. **Stand dal Gate Manager**: la voce `board` del documento del ponte RFO, letta lato server.
6. **Impostazioni per scalo**: quale evento RFO, `voli` = `tutti` o `prenotati`; fuori dagli eventi coi soli voli di
   Whazzup.
7. **Regole, stati in inglese, finestra, ordine e uscite come nel §4 e §5 del formato.** Il VID serve solo a filtrare.
8. **Uno scalo per ora (LIRF)**, ma il parametro `{ICAO}` resta.

## 2. Pre-flight

**1. Modello.** Nessun concetto persistito: niente tabelle, niente migrazioni. Il tabellone è un calcolo sulle tre
fonti, con una memoria di processo piccola (§3.5).

**2. Dispatch.** Uno: la direzione della riga si ricava dagli ICAO (origine = scalo → partenza; destinazione = scalo →
arrivo), mai da `type_of_flight` (inaffidabile, §7 del formato). Un volo con origine e destinazione uguali allo scalo
sta nelle due liste.

**3. Ingressi e verifica.** Test sulle regole con risposte salvate (`Fixtures/tabellone/booking-lirf.json`, forma del
booking del Gate Manager; `ponte-board.json`, forma di SYN-TABELLONE), con dati **inventati**: niente VID veri nel repo.
Il servizio con fonti finte; la rotta E2E. Prova dal vivo col booking vero quando c'è la chiave (§6).

**4. Propagazione.** Additiva. `SourcePilotFix` prende un parametro facoltativo in fondo (`Plan`): chi lo costruiva
prima non cambia. Il poller prende un parametro facoltativo (`FotografiaPiloti`).

## 3. Come funziona

### 3.1 Le fonti

| Fonte | Da dove | Quanto spesso | Se tace |
|---|---|---|---|
| Whazzup | `FotografiaPiloti`, pubblicata dal poller dalla **stessa** lettura del whazzup | come il poller (60 s) | oltre 3' dalla data di **generazione** della fotografia non si usa più: nessun volo online, `fonti.whazzup.ok = false` |
| Booking | `BookingItClient`: `GET Tabellone:BookingUrl` con `x-api-key` | al più ogni 60 s, uno per tutti gli scali | si tiene l'ultima lettura buona; oltre 3' `fonti.booking.ok = false`, le righe restano |
| Gate Manager | `IRfoSharedStateStore.LoadAsync(EventoRfo)`, voci `board` e `boardAt` | a ogni calcolo (una riga dal database) | `boardAt` più vecchio di 10' → stand del booking, `fonti.gateManager.ok = false` |
| Città e IATA | booking (se li ha), poi `IAirportDirectory.GetByIcaoAsync` | prima di ogni calcolo, al più 10 scali nuovi per giro | la città è l'ICAO; uno scalo non trovato si riprova fra 6 ore |

Il poller non ripubblica una fotografia ferma (U-131): la sua data ferma fa scattare da sola la regola dei 3 minuti.
Al Whazzup servivano campi che il client buttava: `lastTrack.arrivalDistance` e, dal piano, `eet`, `departureTime`,
`actualDepartureTime`, `createdAt` (orari in secondi dalla mezzanotte UTC, il giorno da `createdAt` ±1 se lo scarto
supera le 12 ore). Arrivano nello stesso JSON: nessun byte in più sul filo.

### 3.2 Righe e fusione (§5 del formato)

- Booking: solo gli slot con un VID (`booked_by` > 0). `programmato` = `eobt` per le partenze, `eat ?? eobt` per gli
  arrivi (chi si prenota da solo ha solo `eobt`, che è l'orario allo scalo dell'evento). `gate` `TBD`/`TBA`/… = nessuno.
- Whazzup: i piloti col piano da o per lo scalo. Lo stesso callsign nelle due fonti è **una** riga: se il booking ha
  lo stesso callsign due volte, il pilota va a quella col programmato più vicino al suo orario di piano.
- Volo solo Whazzup: `programmato` = partenza del piano; per un arrivo partenza + `eet`. Con `voli = prenotati` resta
  fuori. Fuori dagli eventi `voli` vale sempre `tutti`.
- `id` = callsign; se torna nella giornata `AZA1#2`, in ordine d'orario.
- `compagnia` = prime tre lettere se il callsign è tre lettere più una cifra (`AZA123`), altrimenti `null` (`IABCD`).
- Testi per le palette: maiuscolo, senza accenti, solo `A-Z 0-9 . - / :` e spazio (gli altri diventano spazi),
  tagliati: `volo` 7, `aereo` 4, `gate` 4, `citta` 16, nome dello scalo e dell'evento 32.

### 3.3 Stati (§4 del formato)

| Partenze | quando |
|---|---|
| `SCHEDULED` | non online |
| `BOARDING` | `Boarding`, o `On Blocks` entro 10 NM dalla partenza (si è appena connesso al parcheggio) |
| `DEPARTED` | `Departing`, `Initial Climb` e tutto quel che viene dopo; `On Blocks` oltre 10 NM; in volo senza stato |
| `ON TIME` / `DELAYED` | online in un altro stato a terra: stima entro / oltre 15' dal programmato |

| Arrivi | quando |
|---|---|
| `SCHEDULED` | non online |
| `AIRBORNE` | `En Route` o `Initial Climb`; in volo senza stato |
| `APPROACHING` | `Approaching` |
| `LANDED` | `Landed`; `On Blocks` oltre 10 NM dalla partenza (o senza distanza dalla partenza ed entro 10 NM dall'arrivo) |
| `ON TIME` / `DELAYED` | online a terra all'origine (`Boarding`, `Departing`, `On Blocks` vicino alla partenza) |

**Stime.** Arrivo in volo (GS > 60 kt, distanza nota): distanza / GS + 6', dall'ora della fotografia. A terra: partenza
(effettiva, o del piano, o adesso se il piano è già passato) + `eet` + 6'. Partenza a terra: l'orario del piano, o
adesso se è passato; decollata: la partenza effettiva. `LANDED`: l'ora in cui è stato visto atterrato.

### 3.4 Stand effettivo (§6 del formato)

`board["dep:"/"arr:" + callsign]` se `boardAt` ha meno di 10 minuti, altrimenti lo stand del booking. `gateCambiato`
è vero quando il Gate Manager dà uno stand **diverso da uno stand prenotato**: a chi aveva `TBD` il Gate Manager lo
*dà*, non lo cambia, e `GATE CHANGE` sarebbe falso (scelta del filone; il formato dice «diverso dal secondo», e il
secondo con `TBD` è `null`).

### 3.5 Finestra, uscite, ordine

- **Uscite**: una partenza sparisce 10' dopo `DEPARTED`, un arrivo 20' dopo `LANDED`, un prenotato non online 60' dopo
  il programmato.
- **Finestra**: fino a +6 ore sull'orario stimato (o programmato). Il «−30'» vale per chi è online e non ha ancora
  concluso; per gli altri decidono le uscite qui sopra, che sono più lunghe (senza questa regola un prenotato mai
  visto online sparirebbe a −30', prima dei suoi 60').
- **Memoria** (`MemoriaTabellone`, una per scalo, nel processo): quando una riga è diventata `DEPARTED` o `LANDED`. Una
  partenza decollata e poi disconnessa resta `DEPARTED` fino all'uscita, non torna `SCHEDULED`. Il momento di
  `DEPARTED` è la **partenza effettiva** del piano quando c'è (altrimenti l'ora della fotografia): dal vivo, a sito
  appena acceso, un volo decollato due ore prima restava in lista altri 10'. Un arrivo visto per la prima volta già
  `LANDED` dopo un riavvio resta al più 20'. Le voci non viste da un giorno se ne vanno.
- **Ordine**: programmato, poi callsign; la stima non sposta le righe. Al più 40 righe per lista.

### 3.6 Evento

`evento` non è `null` quando il booking ha almeno un volo prenotato da o per lo scalo. Il nome viene dalla
configurazione (`NomeEvento`; il booking non lo dice), `attivo` vale vero da 2 ore prima del primo volo prenotato a 1
ora dopo l'ultimo: il booking restituisce l'evento «in corso», che può essere quello di domani.

### 3.7 La rotta

- `GET /api/tabellone/{ICAO}`: 404 per uno scalo senza voce in `Tabellone:Scali` (o un ICAO che non è 4 lettere/cifre).
- `ETag` sul contenuto **senza** `aggiornato`: se non è cambiato niente lo schermo che rimanda `If-None-Match` riceve
  un 304 senza corpo, che non entra nel registro delle richieste (`RegistroRichieste.PollingVuoto`).
- `Cache-Control: public, max-age=15`; `Access-Control-Allow-Origin: *` (la pagina del tabellone può stare altrove;
  niente credenziali). `OPTIONS` risponde al preflight.
- Orari ISO 8601 UTC con la `Z`, ai secondi. Nessun VID nel JSON (c'è un test che lo pretende).

## 4. Configurazione

In `appsettings.json` c'è LIRF con nome e fuso; il resto va nel **file dei segreti** del server
(`public_atc/segreti/`), come le chiavi del ponte RFO, così l'evento si cambia senza un pacchetto:

```json
{
  "Tabellone": {
    "BookingApiKey": "<la chiave del booking>",
    "Scali": {
      "LIRF": { "EventoRfo": "lirf-20261003", "NomeEvento": "Roma RFE", "Voli": "tutti" }
    }
  }
}
```

Dizionario per ICAO, non array (§A62: gli array delle sorgenti si sommano). Senza `BookingApiKey` il tabellone va col
solo Whazzup; senza `EventoRfo` lo stand è quello del booking.

## 5. Il ritmo di Whazzup: resta un minuto (proposta del filone)

Il committente ha chiesto se un minuto è troppo lento. **Proposta: lasciarlo a 60 s.** Il tabellone chiede ogni 30 s e
le palette girano solo quando cambia uno stato; gli stati del tabellone cambiano su scala di minuti (`BOARDING` →
`DEPARTED` → uscita 10' dopo), e un minuto di ritardo su uno schermo d'aeroporto non si vede. Scendere a 30 s
raddoppierebbe le chiamate al whazzup (~170 MB/giorno → ~340) e le scritture delle statistiche ATC (sessioni e minuti
di traffico, che seguono il giro: T-068). Se servisse, basta `Ivao:PollSeconds` nella configurazione, senza codice: il
giro minimo è 15 s, e statistiche e scadenze si adattano già.

## 6. Verifica

- Test: App +39 (regole), Hosting +6 (servizio), Infra +2 (campi nuovi del whazzup, piloti dal poller), E2E +7 (rotta,
  login libero, registro). Conteggi in `tests/conteggi/`.
- Dal vivo il 2 ottobre 2026 (17:23 locali), sito locale su un database vuoto, **Whazzup vero** (515 piloti), senza
  chiave del booking, documento del ponte `prova-locale` scritto a mano con un `board`: `GET /api/tabellone/LIRF` 200,
  `ETag`, `Cache-Control: public, max-age=15`, `Access-Control-Allow-Origin: *`; 4 partenze e 2 arrivi solo Whazzup
  con stati coerenti (BOARDING, ON TIME, DEPARTED, AIRBORNE), città e IATA dall'anagrafica (ISTANBUL/IST, KRAKOW/KRK),
  `evento` null, `fonti.booking.ok` false, `fonti.gateManager.ok` true. Da qui la regola della partenza effettiva (§3.5).
- Da fare dal vivo, quando il committente inserisce la chiave del booking: i nomi dei campi di città e IATA del booking
  (il formato li dà «da verificare»: oggi si provano `origin_city`/`origin_iata`/`destination_city`/`destination_iata`
  e simili, poi l'anagrafica), il `board` vero del documento `prova-ponte-rfo` (fermo al 2 ottobre).
