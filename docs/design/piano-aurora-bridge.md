# vIPI Aurora Bridge — Piano e Architettura 🟡

> **F0 eseguita il 3 agosto 2026** su Aurora reale connessa (`LIZZ_AEW_CTR`, traffico in range): gli esiti sono
> in **§11** e correggono la wiki in cinque punti. Le sezioni §2 e §6–§7 vanno lette insieme a §11.

**Cosa:** applicativo desktop che, selezionato un aeromobile in Aurora, ricava dalla vIPI/vLOA il livello a
cui quel volo va trasferito al prossimo ente e lo scrive nell'etichetta quota del tag.
**Stato:** pianificazione (codice non iniziato).
**Data:** 3 agosto 2026 · **Autore:** Carmine + assistente
**Fonti:** [Aurora 3rd Parties Documentation](https://wiki.ivao.aero/en/home/devops/manuals/Aurora-3rd-parties-documentation) (rev. A, agg. 05-03-2025) · `piano-vipi-tool.md` §18 (API pubblica read-only, qui realizzata) · `docs/FEATURE-PROCESS.md`

---

## 1. Decisioni fissate (round 1)

| Tema | Decisione | Perché |
|---|---|---|
| Campo scritto in Aurora | **`#LBALT` (Altitude label)**, con sonda empirica dell'XFL in F0 | `#LBALT` è l'unico comando di scrittura quota documentato; l'XFL (campo 19 di TRPOS) la wiki lo espone **solo in lettura** |
| Dove gira il matching | **Server**, endpoint `POST /vsop/api/v1/transfers/resolve` | logica in un posto solo, riusa `ITransferService`/`TransferOnlineResolver`/`OnlineAtcCache`; nessun dato duplicato sul client |
| Shell del tool | **Avalonia** (net8.0, win-x64 + osx-arm64) | Aurora gira anche fuori Windows; nessun vincolo di riuso Razor essendo l'app fuori dal portale |
| Scrittura in Aurora | **Solo su azione esplicita** dell'utente (click o hotkey) | un livello sbagliato nel tag è un errore operativo: il tool propone, l'uomo decide |
| Collocazione | **stesso repo**, nuovi progetti (§4) | contratto DTO condiviso fra host e tool senza pacchetti esterni |

---

## 2. Cosa espone Aurora (verificato sulla wiki)

Server TCP **127.0.0.1:1130**, ASCII, campi separati da `;`, pacchetto chiuso da CR/LF, multi-client.
Va attivato a mano su Aurora: **PVD → Settings (F7) → Other → 3rd Party Software Access = YES**.
Identificatori: `#` messaggio bidirezionale, `$` messaggio d'errore. Comandi ignoti → `@ERR`.
Il client **non deve mai** mandare `;` dentro un argomento. Il raggio dei dati è limitato al Radar range di Aurora.

**Lettura utile al nostro caso**

| Comando | Restituisce | Ci serve per |
|---|---|---|
| `#CONN` | callsign connessa | capire **quale postazione** sono io → quali flussi sono miei |
| `#SELTFC` | callsign del traffico selezionato | l'innesco di tutto |
| `#FP;CS` | Flight Plan Record: dep, arr, alt, aeromobile, **FL crociera**, **rotta**, remarks | scelta flusso (arrivo/partenza/sorvolo), CoP in rotta, parità |
| `#TRPOS;CS` | quota, velocità, lat/lon, **assumed station**, **next station**, on ground, is selected, **XFL**, V/S | contesto verticale, ente successivo già impostato, stato del tag |
| `#CTRLRWY` / `#ATIS` | piste dep/arr per aeroporto controllato | **condizioni pista** dei punti di trasferimento |
| `#TR` | traffici in range | modalità elenco (F5, opzionale) |
| `#ATC` / `#ATCT` | ATC in range / in transfer list | diagnostica; l'online autorevole resta quello del sito |

**Scrittura disponibile:** `#LBALT;CS;valore` (etichetta quota), `#LBWP`, `#LBSPD`, `#LBSQK`, `#LBGTE`,
`#TRAS`/`#TRRE`/`#TRTR`, `#ATCTA`/`#ATCTR`, `#MSGFR`/`#MSGPM`, `#ZTR`/`#ZSTR`.
Valori dell'etichetta quota: intero `>=0` = quota, `-1` APP, `-2` LND, `-3` GA.
Macro `%SELTFC%` = callsign selezionata; **il comando viene scartato se nessun traffico è selezionato**.

**Limiti strutturali da cui discende il design**
1. **Nessun push** server→client (tranne gli eventi intercom) → il tool **fa polling**.
2. **Nessuna correlazione richiesta/risposta**: le risposte si riconoscono solo dal prefisso comando → le
   richieste vanno **serializzate** con timeout, non lanciate in parallelo.
3. **Nessun comando per scrivere l'XFL** → vedi §7 R2.

---

## 3. Cosa c'è già nel portale (da riusare, non riscrivere)

- `TransferFlowRow`: `AccCode`, `OwningSectorCallsign`, `Kind` (Arrival/Departure/Overflight/Vfr/Other), `AirportIcao`, `Points[]`.
- `TransferPointRow`: `Cop`, `LevelValue`/`LevelUnit`/`LevelConstraint`/`LevelSpecial`, `Parity`, `VerticalState`,
  `LevelText`, `NextSectorCallsign`, condizioni (`ConditionLabel` pista, `ConditionRefId` soft-ref pista,
  `ConditionAreaLabel`, `ConditionCustomLabel`), `Order`.
- `ITransferService.ResolveForAccAsync(acc, online)` + `TransferOnlineResolver`: risalita della gerarchia di
  copertura (cross-ACC) fino al primo ente online, terminale `UNICOM`.
- `OnlineAtcCache` (+ SSE `/vsop/live/atc`): chi è online, aggiornato dal polling IVAO.
- `ISectorTopology.BuildGlobalAsync()` → `Ancestors(callsign)`.

**Manca:** una superficie **JSON pubblica**. Oggi il modulo espone solo `/vsop/live/atc` (SSE) e le immagini.
`piano-vipi-tool.md` §18 la prevedeva «documentata, non implementata»: questo piano la implementa, ristretta
al solo caso d'uso trasferimenti.

---

## 4. Architettura

```
┌────────────────────── PC del controllore ──────────────────────┐
│  Aurora  ──TCP 1130 ASCII──►  Vipi.AuroraBridge.Core           │
│    ▲                            │  (client TCP + orchestratore)│
│    └──── #LBALT (solo su azione)│                              │
│                                 ▼                              │
│                          Vipi.AuroraBridge (Avalonia)          │
└─────────────────────────────────┬──────────────────────────────┘
                                  │ HTTPS  POST /vsop/api/v1/transfers/resolve
                                  ▼
        it.ivao.aero  ──►  TransferResolveEndpoint (Vipi.Hosting)
                              └─► TransferMatchService (Vipi.Application, puro)
                                    ├─ ITransferService / ITransferRepository
                                    ├─ ISectorTopology
                                    └─ OnlineAtcCache
```

**Progetti nuovi**

| Progetto | Target | Contenuto |
|---|---|---|
| `src/Vipi.AuroraBridge.Contracts` | `net8.0` | **solo DTO** richiesta/risposta dell'endpoint. Referenziato sia dall'host sia dal tool: un contratto, zero duplicazione di POCO |
| `src/Vipi.AuroraBridge.Core` | `net8.0` | `AuroraClient` (TCP), `VipiApiClient` (HTTP + cache su disco), `BridgeOrchestrator` (polling → contesto → resolve). **Nessuna UI**, tutto testabile |
| `src/Vipi.AuroraBridge` | `net8.0` | app Avalonia: finestra always-on-top, lista candidati, azione «Scrivi in Aurora» |
| `tests/Vipi.AuroraBridge.Tests` | `net8.0` | parser protocollo Aurora + orchestratore, contro un server TCP finto |

Codice nuovo **lato portale**: `TransferMatchService` in `Vipi.Application/Content/` (puro, niente IO) +
l'endpoint in `Vipi.Hosting/VipiModuleExtensions.cs` (accanto a `/vsop/live/atc`) + test in
`Vipi.Application.Tests`. `net8.0` ovunque per rispettare il multi-target del modulo.

**Perché il tool non referenzia `Vipi.Application`:** trascinerebbe EF Core e mezzo dominio dentro un'app
desktop. Il confine è il contratto DTO.

---

## 5. Il contratto API

`POST /vsop/api/v1/transfers/resolve` — `Content-Type: application/json`, anonimo, read-only.

**Richiesta** (tutto ciò che il tool sa da Aurora):

```jsonc
{
  "ownerCallsign": "LIRR_NE_CTR",       // #CONN, oppure assumed station di TRPOS
  "departure": "LIRF",
  "arrival":   "LIMC",
  "cruiseLevel": 350,                    // FL normalizzato dal campo 10 del FP
  "route": "OST UM729 ELB ...",
  "currentAltitudeFt": 24000,
  "verticalSpeedFpm": 1800,
  "onGround": false,
  "nextStation": "LIMM_W_CTR",           // TRPOS campo 13, se già impostata
  "runwaysInUse": { "LIRF": { "dep": ["25"], "arr": ["34L"] } }   // #CTRLRWY / #ATIS
}
```

**Risposta**:

```jsonc
{
  "asOf": "2026-08-03T10:00:00Z",
  "onlineAsOf": "2026-08-03T09:59:40Z",   // freschezza della cache ATC: il tool la mostra
  "resolvedOwner": "LIRR_NE_CTR",
  "candidates": [{
    "flowId": 12, "pointId": 88,
    "flowKind": "Departure", "airportIcao": "LIRF",
    "cop": "OST",
    "level": { "value": 250, "unit": "Fl", "constraint": "AtOrBelow",
               "special": null, "parity": "Any", "verticalState": "Climbing",
               "text": "FL250 o inferiore" },
    "nextSectorCallsign": "LIMM_W_CTR",
    "resolvedHandler": "LIMM_CTR", "handlerOnline": true,
    "condition": { "display": "RWY 25", "match": "matched" },   // matched | unmatched | unknown
    "auroraValue": "250",        // STRINGA da passare a #LBALT (§11.2); null se non scrivibile. Mai un ';'
    "writable": true,
    "score": 0.95,
    "reasons": ["CoP presente in rotta", "parità coerente", "pista in uso coerente"]
  }],
  "warnings": ["condizione «area LOTAR attiva» non verificabile automaticamente"]
}
```

**Regole del contratto**
- Versionato: `/v1`. Ogni cambio incompatibile → `/v2`, il tool vecchio continua a funzionare.
- **Nessuna scrittura, nessun dato personale**: il callsign dell'aeromobile non serve al server e **non si manda**.
- Vede **solo documenti con release effettiva e non nascosti**, stesso gate delle liste pubbliche.
- Rate limit per IP (ASP.NET `RateLimiter`), tetto sulla dimensione del body, CORS chiuso (client desktop).
- Contratto congelato in `docs/reference/api-aurora-bridge.md`, generato dai DTO.

---

## 6. L'algoritmo di matching (il cuore)

`TransferMatchService`, funzione pura: `(richiesta, flussi, topologia, online) → candidati ordinati`.

1. **Chi sono io.** `ownerCallsign` → `Sector` (match esatto, poi euristica a segmenti come
   `TransferOnlineResolver`). I flussi candidati sono **i miei + quelli dei settori che sto coprendo**
   (discendenti nella gerarchia **non** online): se Pisa APP è chiuso, i trasferimenti di Pisa sono roba mia.
   → richiede `Descendants()` su `ISectorTopology` (oggi c'è solo `Ancestors()`): estensione simmetrica, non
   un secondo modello.
2. **Che tipo di volo è.** `arrival` nel mio dominio → **Arrival**; `departure` nel mio dominio e traffico in
   salita/al suolo → **Departure**; nessuno dei due → **Overflight**. I kind non scelti non vengono esclusi:
   entrano coi punteggi bassi (i dati veri hanno eccezioni).
3. **Quale flusso.** Arrival/Departure → `AirportIcao` == arr/dep. Overflight → flussi senza aeroporto.
4. **Quali punti.** Filtri, ognuno dei quali **motiva** o **penalizza**, mai scarta in silenzio:
   - **CoP in rotta**: token della rotta; `ALL` e `ALL-to-X` come jolly (semantica già in uso nei sorvoli);
   - **parità**: `LevelParity` vs parità del FL di crociera;
   - **condizione pista**: `ConditionLabel`/`ConditionRefId` vs `runwaysInUse` → `matched`/`unmatched`;
   - **area / personalizzata**: non verificabili automaticamente → `unknown` + warning, punteggio ridotto.
5. **Livello → valore per Aurora.**
   - `Fl` → intero (unità da confermare in F0, §7 R1); `Feet` → piedi;
   - `Special` (testo libero tipo «per aerovia») → `writable=false`, si mostra solo il testo;
   - `AtOrAbove`/`AtOrBelow` → il valore si scrive, ma **il vincolo resta visibile in UI**: è un limite, non
     un'autorizzazione;
   - `VerticalState` → mostrato, mai scritto.
6. **Ente successivo**: `TransferOnlineResolver.Resolve(catena)` → chi prende davvero il traffico ora
   (`UNICOM` se nessuno). Se il nominale è offline il candidato resta valido, ma con l'avviso di risalita.
7. **Ordinamento** per punteggio, con `reasons` leggibili: l'utente deve capire **perché** quel livello.

**Test-first** su questo servizio (è deterministico e senza IO): arrivo/partenza/sorvolo, parità pari/dispari,
CoP assente dalla rotta, `ALL`/`ALL-to-X`, condizione pista coerente/incoerente/ignota, `Special`,
ente successivo offline → risalita, copertura top-down di un APP chiuso.

---

## 7. Rischi

| # | Rischio | Mitigazione |
|---|---|---|
| **R1** | Semantica di `#LBALT` ignota: FL o piedi? cosa mostra il tag? | **F0 empirica** su Aurora reale prima di scrivere altro codice |
| **R2** | L'XFL nativo **non è scrivibile** da comando documentato: il tool riempie l'etichetta, non il campo XFL | F0 sonda comandi non documentati (`#LBXFL` e varianti: il server risponde `@ERR` se non esistono, prova innocua). Se serve davvero, mail a Makis Giantsidis (`makis.giantsidis@ivao.aero`, cc `sdm@`/`dod@`/`doad@`) come chiede la wiki |
| **R3** | Qualità dei dati vIPI (CoP mancanti, condizioni a testo libero) | mai scrittura automatica, warning espliciti, candidati sempre motivati |
| **R4** | Il CoP di trasferimento può non comparire come token di rotta | i candidati senza match CoP si mostrano comunque, in fondo |
| **R5** | Sito irraggiungibile o lento in sessione | cache su disco dell'ultima risposta per chiave contesto + banner «dati delle HH:MM» |
| **R6** | Aurora chiusa, 3rd party access spento, porta occupata | diagnostica esplicita in UI con il rimedio (F7 → Other → YES) |
| **R7** | Convivenza con altri tool 3rd party | il server è multi-client: nessun lock, nessuno stato condiviso lato Aurora |
| **R8** | Policy IVAO sui tool di terze parti | il tool va segnalato al DoD/SDM e può entrare nell'elenco 3rd party della wiki |

---

## 8. Fasi

| Fase | Obiettivo | Uscita | Bloccante |
|---|---|---|---|
| **F0 — Spike protocollo** | `Vipi.AuroraBridge.Core` minimo + console: connessione, `#CONN`/`#SELTFC`/`#FP`/`#TRPOS`/`#CTRLRWY`, **test di `#LBALT`** (FL vs piedi, cosa appare nel tag), **sonda XFL** | verbale con le risposte reali → chiude R1 e R2 | **sì**: tutto il resto dipende dalla semantica reale |
| **F1 — Matching + API** ✅ | `TransferMatcher` (puro) + `TransferMatchService` + `POST /vsop/api/v1/transfers/resolve` + tetto per IP + [contratto](../reference/api-aurora-bridge.md) | **fatta il 3 ago 2026**: 26 test sul matcher, 6 sulle opzioni/limitatore, verifica live con `curl` sul DB reale (§11.6) | |
| **F2 — Core client** ✅ | `AuroraClient` (riconnessione, richieste serializzate, timeout, `@ERR`), `AuroraSession`, `VipiApiClient` + cache disco, `FlightContextBuilder`, `BridgeOrchestrator` | **fatta il 3 ago 2026**: 36 test con Aurora finta, CLI `tools/Vipi.AuroraBridge.Cli` end-to-end (§13) | |
| **F3 — UI Avalonia** ✅ | finestra always-on-top, contesto volo, candidati con livello/ente/condizione/motivi, pulsante di scrittura, impostazioni, stati degradati | **fatta il 3 ago 2026**: 16 test sul ViewModel, finestra verificata a video (§14). Hotkey globale rimandata a F4 | |
| **F4 — Rifinitura** ✅ | scorciatoia globale, registro locale, icona, packaging autonomo, [guida utente](../guide/aurora-bridge.md) | **fatta il 3 ago 2026**: 22 test nuovi, eseguibile win-x64 da 73,7 MB provato (§15) | |
| **F5 — Opzionali** | XFL se F0 dà esito positivo; modalità elenco su `#TR` (pre-compilazione dei traffici in ingresso) | | |

---

## 9. Pre-flight FEATURE-PROCESS

1. **Modello** — nessun concetto nuovo lato portale: si riusa `TransferFlow`/`TransferPoint` e la topologia
   settori. Le uniche aggiunte sono un servizio **puro** di matching e i DTO del contratto. Nessun modello
   «gemello» del trasferimento.
2. **Dispatch** — nessuno switch per-tipo duplicato: il `Kind` del flusso è un filtro dati, non un ramo di
   codice. Il registry esistente non viene toccato.
3. **Ingressi + verifica** — ingresso = il tool stesso; nessuna pagina nuova nel portale. Verifica: sessione
   Aurora reale con traffico, con traccia scritta (callsign, candidati proposti, valore scritto, tag risultante).
   Prima di F3 la verifica passa da `curl` sull'endpoint + tool a riga di comando.
4. **Propagazione** — niente rimozioni/rinomine. Da aggiornare comunque: `piano-vipi-tool.md` §18 (l'API
   pubblica passa da «solo documentata» a «realizzata, ristretta ai trasferimenti») e `docs/index.md`.

---

## 10. Questioni aperte

1. **Unità dell'etichetta quota** in Aurora (FL o piedi) e comportamento con `-1/-2/-3` → F0.
2. **Ente successivo offline**: proporre lo stesso il livello nominale, o quello dell'ente che assorbe? La
   risposta cambia cosa scriviamo nel tag quando il settore confinante è chiuso.
3. **Flussi `Vfr`/`Other`**: inclusi nel matching o esclusi finché non c'è una regola d'uso?
4. **Copertura dati**: quali ACC hanno i trasferimenti popolati oggi? Se solo Roma, la verifica live va fatta
   su LIRR e il tool deve dirlo chiaramente quando per la postazione non ci sono dati.
5. **Hotkey globale su macOS** richiede permessi accessibilità: accettabile o hotkey solo Windows?
6. ~~**Convenzione dell'etichetta quota**~~ **CHIUSA il 3 ago 2026**: verificato a video che il tag mostra
   **esattamente** ciò che si invia (`250` → `250`, `25000` → `25000`, `FL250` → `FL250`), senza alcuna
   formattazione da parte di Aurora. Resta il default `Number` (per FL si scrive il numero nudo, come si legge
   nei tag), ribaltabile con `AuroraBridge:LabelConvention`.

---

## 11. Esiti dello spike F0 (3 agosto 2026)

Eseguito con `tools/Vipi.AuroraProbe` contro Aurora connessa alla rete come `LIZZ_AEW_CTR`, con traffico in
range e `FDX126` assunto. **Prerequisito emerso:** il server TCP non nasce col processo — la porta 1130 è
comparsa solo dopo aver ri-applicato *3rd Party Software Access* nella sessione in corso, benché il profilo su
disco (`Profiles\LIRR_NE_CTR.cpr`, `[3RD PARTY] ENABLE_TCP_SERVER=1`) lo dichiarasse già attivo. Il tool deve
quindi diagnosticare «porta chiusa» con questo rimedio, non dare per buono il file.

### 11.1 Cinque correzioni alla wiki

1. **Le risposte fanno eco al comando inviato.** La wiki dà `#CTRL;…` come esito di `#CTRLRWY`, `#CONN`,
   `#CTO`, `#ZTO`: falso. Tornano `#CTRLRWY;…` e `#CONN;…`. Il parser correla sul **prefisso del comando inviato**.
2. **`#LBALT` accetta testo libero, non solo interi.** `FL250` è stato accettato e rimandato tale e quale.
   La wiki dice «Integer value». Conseguenza: possiamo scrivere esattamente la stringa che vuole la vIPI.
3. **Si scrive solo sul traffico assunto.** `@ERR;#LBALT;RYR90RC;250;Traffic not assumed.` — vincolo non
   documentato. Il tool deve leggere il campo 12 di `#TRPOS` (assumed station) e disabilitare il pulsante,
   spiegando il perché, quando il traffico non è assunto. **Mai** assumere da solo con `#TRAS`.
4. **`@ERR` distingue due casi, ma non tre.** `Unknown command` vs `Incomplete data in command`: quest'ultimo
   vale **sia** per argomenti mancanti **sia** per callsign sconosciuto (traffico uscito dal range) → il client
   non può distinguerli dal messaggio, deve dedurlo dal contesto.
5. **La rilettura non è immediata.** Dopo `#LBALT` il campo 10 di `#TRPOS` resta sul valore vecchio per
   ~1–2 s (ciclo di aggiornamento radar): a 838 ms ancora stale, a 2,5 s aggiornato. Nessuna verifica sincrona
   dopo la scrittura; se serve conferma, si rilegge al giro di polling successivo.

### 11.2 Semantica accertata di `#LBALT`

| Inviato | Campo 10 di `#TRPOS` | Note |
|---|---|---|
| `250` | `250` | valore grezzo, nessuna conversione |
| `25000` | `25000` | idem: Aurora non interpreta l'unità |
| `FL250` | `FL250` | testo libero accettato |
| `-1` | `APP` | speciali restituiti **come testo** (`-1` APP, `-2` LND, `-3` GA) → round-trip asimmetrico |
| *(vuoto)* | *(vuoto)* | `#LBALT;CS;` cancella l'etichetta: è il «pulisci» del tool |

**Conseguenza di design:** l'unità non è un problema tecnico ma una **convenzione di visualizzazione**. Il
`auroraValue` del contratto (§5) diventa quindi una **stringa**, non un intero, e il server la compone dal
`LevelText` della vIPI secondo la convenzione scelta. Questo riapre in positivo anche i livelli `Special`
(testo libero tipo «per aerovia»): scrivibili, purché senza `;`.

### 11.3 `#TRPATHL` cambia il matching in meglio

```
#TRPATHL;RYR90RC;OLGAT:0816;OKIMO:0824;AZHIF:0835;RIVAM:0843;ERITU:0856;LANLI:0903;LUMAV:0914;OSKOR:0925;LIME:0928;
```

Aurora restituisce **la sequenza dei fix già risolta** (con ETO `HHMM`, e `-` per i punti passati usando
`#TRPATHA`). Sostituisce il parsing della rotta grezza del FP previsto in §6.4: il CoP si cerca in questa
lista, e l'**ordine temporale** dice quale CoP è il prossimo quando un flusso ne ha più d'uno — informazione
che la rotta testuale non dava. Il campo `route` resta nel contratto come fallback (traffico al suolo: su
`DLH2MM` fermo a LIPE `#TRPATHL` è tornato vuoto).

### 11.4 Formati confermati

- **`#FP`**: `dep;arr;alt;etd;actype;wake;I;S;equip;F180;N0374;endurance;eet;rotta;remarks`. Livello di
  crociera nel formato ICAO (`F330` = FL330, `M082` = Mach .82 in velocità). ⚠️ i campi 7 e 8 sembrano
  **invertiti** rispetto alla wiki: arriva `I` (regole di volo) prima di `S` (tipo di volo).
- **`#TRPOS`**: ordine dei 21 campi confermato. Quota in **piedi** (`37992`), V/S in ft/min, lat/lon decimali.
  Campo 12 assumed station = `LIZZ_AEW_CTR` sul traffico assunto, vuoto sugli altri; campo 19 XFL sempre vuoto.
- **`#CTRLRWY`**: `ICAO;dep;arr;ICAO;dep;arr;…` con più piste separate da `:` — es.
  `LIRF;25;16L:16R`, e campi vuoti se non configurate (`LIRE;;`). Utilizzabile come previsto per le condizioni pista.
- **`#ATIS`** su una posizione ACC torna quasi tutto vuoto (`#ATIS;;;;;5000;;`): **non** è una fonte
  affidabile delle piste in uso, la fonte è `#CTRLRWY`.

### 11.5 Stato dei rischi

- **R1** *(unità dell'etichetta)* → chiuso a metà: tecnicamente qualunque stringa passa; resta da fissare la
  **convenzione** da scrivere (vedi §10.6).
- **R2** *(XFL non scrivibile)* → **confermato e chiuso**. `#LBXFL`, `#XFL`, `#TRXFL`, `#SETXFL` rispondono
  tutti `Unknown command`, mentre `#LBALT` senza argomenti risponde `Incomplete data in command`: la sonda
  discrimina, e non esiste alcun comando nascosto per l'XFL. Il tool riempie **l'etichetta quota**. Se l'XFL
  serve davvero, è una richiesta di feature a Makis Giantsidis.
- Nuovo **R9**: la scrittura richiede il traffico **assunto** → il caso d'uso «pre-compilo il livello sul
  traffico in arrivo prima di assumerlo» **non è realizzabile**. Da dire chiaro nella guida utente.

---

## 12. Esiti della fase F1 (3 agosto 2026)

Realizzato: `src/Vipi.AuroraBridge.Contracts` (POCO del contratto, `net8.0;net10.0`),
`TransferMatcher` + `TransferMatchService` in `Vipi.Application/Content/`, endpoint e configurazione in
`Vipi.Hosting`, contratto documentato in [`../reference/api-aurora-bridge.md`](../reference/api-aurora-bridge.md).
**845 test verdi** in tutta la solution (26 nuovi sul matcher, 6 su opzioni e limitatore).

### 12.1 Scostamenti dal piano, e perché

1. **Niente `Descendants()` su `ISectorTopology`** (§6.1 lo prevedeva). Non serviva un metodo nuovo: la
   copertura si esprime già con gli antenati. Un flusso è mio ⟺
   `TransferOnlineResolver.FirstOnline([proprietario, …antenati], online ∪ {io}) == io`. Una riga, e gestisce
   in un colpo i miei flussi, quelli degli enti chiusi che assorbo e l'esclusione di ciò che un sotto-settore
   online si è ripreso. Aggiungere un metodo sarebbe stato codice nuovo per una regola già espressa.
2. **`auroraValue` è una stringa** (§11.2): conseguenza diretta di F0.
3. **Il contratto è UN modello, non due.** `Vipi.Application` referenzia `Vipi.AuroraBridge.Contracts` e usa i
   suoi tipi come input/output del matching, invece di avere DTO propri poi ricopiati nell'endpoint
   (FEATURE-PROCESS §1: mai affiancare un secondo modello alla stessa cosa). Il progetto contratti non ha
   dipendenze, quindi non inquina l'Application.
4. **Il punteggio si normalizza, non si tronca.** Primo test rosso della serie: con `Clamp(score, 0, 1)` due
   candidati forti finivano entrambi a 1.0 e la graduatoria si perdeva proprio dove serviva (stesso CoP,
   distinti solo dal next ATC già impostato). Ora si divide per la somma dei contributi positivi massimi.
5. **Limitatore proprio invece di `AddRateLimiter`.** Il modulo gira anche **embedded** in Ivao.It: aggiungere
   `UseRateLimiter` significherebbe metter mano alla pipeline dell'host. `RequestRateLimiter` (finestra fissa,
   conteggio per IP) sta dentro l'endpoint e non tocca nulla. È distinto da `StaffLoginThrottle`, che ha
   semantica diversa (UNA azione per finestra, non un conteggio) e infatti non era riusabile.

### 12.2 Cosa fa il matcher, in breve

Accoppia il flusso al volo (aeroporto di partenza/arrivo, o sorvolo), poi valuta ogni punto su quattro assi —
**CoP** (fix da `#TRPATHL`, poi rotta del piano; riconosce i jolly `ALL`/`ALL to GR` e i range di aerovie
`Y01-Y12` visti nei dati LIBB), **parità** semicircolare rispetto al livello di crociera, **condizione pista**
contro `#CTRLRWY`, **next ATC** già impostato in Aurora — e produce per ciascuno un punteggio e le **ragioni in
italiano**. Non scarta mai in silenzio: ciò che non torna abbassa il punteggio e lascia una traccia leggibile.

### 12.3 Verifica live

Host locale sul `vipi.db` reale, `POST` con contesto realistico (LIBB_ES_CTR, arrivo a LIRF via ASPIR, FL350):

```
candidato 1 — ASPIR → LIRR_US_CTR, FL210- ↓ (dispari), auroraValue "210", score 0.806
reasons: ["arrivo a LIRF", "CoP ASPIR in rotta (ETO 0925)", "livello di crociera dispari"]
```

Corretto rispetto alla vIPI: FL350 è dispari e la riga per LIRF dispari è proprio FL210 o inferiore. Il
sospetto di mojibake sulla freccia (`FL210- â†“`) era della pipeline di test (`python` stampa in cp1252 su
Windows): i byte sulla rete sono `e2 86 93`, UTF-8 corretto.

### 12.4 Prossimo passo (F2)

`Vipi.AuroraBridge.Core`: client TCP con richieste serializzate e timeout, client HTTP verso questo endpoint
con cache su disco, orchestratore di polling (`#SELTFC` ~1 s, contesto ~5 s, `#CTRLRWY` ~60 s). Da tenere
presente da F0: leggere il campo 12 (assumed station) **prima** di offrire la scrittura, e non rileggere il
tag subito dopo `#LBALT` (il record posizione è stale per ~2 s).

---

## 13. Esiti della fase F2 (3 agosto 2026)

`src/Vipi.AuroraBridge.Core` (`net8.0`) + `tools/Vipi.AuroraBridge.Cli` + `tests/Vipi.AuroraBridge.Tests`.
**881 test verdi** in tutta la solution, 36 nuovi.

| Tipo | Ruolo |
|---|---|
| `AuroraClient` | protocollo grezzo: connessione, framing CR/LF, correlazione, timeout, `@ERR` |
| `AuroraRecords` | parser dei record (FP, TRPOS, CTRLRWY, TRPATHL), puri |
| `AuroraSession` | i comandi che servono, tipizzati; traduce i rifiuti in messaggi comprensibili |
| `FlightContextBuilder` | dal record Aurora alla richiesta del sito: è la giuntura fra i due protocolli |
| `VipiApiClient` | HTTP + cache su disco per chiave di contesto |
| `BridgeOrchestrator` | sorveglia la selezione, monta il contesto, pubblica lo stato; **non scrive mai da solo** |

### 13.1 Decisioni prese scrivendo il client

1. **Richieste serializzate con un semaforo.** Senza identificativo di richiesta, due comandi in volo insieme
   si prenderebbero la risposta l'uno dell'altro. Un test lancia apposta `#TRPOS;A` e `#TRPOS;B` insieme e
   verifica che ognuno riceva la propria.
2. **I push non sollecitati non inquinano la correlazione.** Le righe che non fanno eco al comando in corso
   (intercom) vanno all'evento `Unsolicited` e lo scambio prosegue.
3. **Il «;» si ferma prima dell'invio**, non dopo l'errore: un argomento che lo contiene romperebbe il
   protocollo, quindi il client rifiuta il comando senza spedirlo.
4. **Il punto di vista è la MIA postazione, non chi ha assunto il traffico.** Prima usavo l'assumed station:
   sbagliato — se guardo un traffico di un altro ente mi darebbe le regole di trasferimento *sue*. L'assumed
   station serve solo a sapere se Aurora accetterà la scrittura.
5. **Nessuna rilettura dopo la scrittura.** Il record posizione resta stale ~2 s (F0 §11.1): una verifica
   immediata mentirebbe. Il valore nuovo si vede al giro dopo.
6. **Override della postazione** (`--owner`, e in F3 un campo nelle impostazioni): quando il callsign connesso
   non è un settore del sito (addestramento, callsign fuori standard) il tool non avrebbe nulla da proporre e
   non si capirebbe perché. Con l'override si lavora lo stesso. L'interrogazione di `#CONN` resta comunque,
   altrimenti non ci si accorgerebbe che Aurora si è disconnessa.

### 13.2 Aurora finta

`FakeAuroraServer` parla lo stesso protocollo da un copione e sa fare ciò che dal vero è scomodo provocare:
tacere (timeout), spingere messaggi non sollecitati in mezzo a uno scambio, rifiutare una scrittura con
`Traffic not assumed.`. I record dei test sono **risposte reali catturate in F0**, non inventate.

### 13.3 Verifica live

CLI contro Aurora vera (connessa come `LIZZ_AEW_CTR`, traffico reale in range) e host locale sul `vipi.db`
reale, con `--owner LIBB_ES_CTR` per applicare le regole di una ACC che ha dati:

```
$ dotnet run --project tools/Vipi.AuroraBridge.Cli -- --site http://127.0.0.1:5034 --owner LIBB_ES_CTR
Aurora: connessa   postazione: LIBB_ES_CTR
Traffico: IBE0980   LIRN→LEMD   crociera F350   quota 32232 ft   ASSUNTO
⚠ Nessun candidato ha un livello scrivibile: sono tutti testuali o senza valore.
  [1] TIGRA      — (dispari)   → UNICOM   scrivi «—»  (0.250)
      sorvolo · CoP TIGRA non trovato in rotta · livello di crociera dispari
```

Catena completa: `#CONN` → `#SELTFC` → `#FP`/`#TRPOS`/`#TRPATHL`/`#CTRLRWY` → richiesta al sito → candidati
resi. Piano di volo, quota e stato di assunzione letti correttamente da traffico vero. Il percorso di
scrittura è stato provato dal vivo (`#LBALT` su traffico assunto, poi etichetta ripulita).

**Bug trovato e corretto proprio qui — solo la verifica live poteva scovarlo.** La CLI dichiarava
`non assunto` un traffico che Aurora considerava assunto: lo stato confrontava la *assumed station* con il
callsign passato in `--owner` invece che con quello **connesso**. Sono due cose diverse: l'override dice
quali regole di trasferimento applicare, ma chi può scrivere lo decide la connessione reale. Con il confronto
sbagliato il tool avrebbe rifiutato scritture perfettamente legittime ogni volta che l'override era attivo.
Corretto separando `ConnectedCallsign` da `OwnerCallsign` in `BridgeState`, con test dedicato.

### 13.4 Un problema di DATI, non di codice

Sul `vipi.db` reale, i punti di trasferimento **senza livello** sono:

| Tipo di flusso | Senza livello | Totale |
|---|---|---|
| Arrival | 3 | 37 |
| Departure | 0 | 3 |
| **Overflight** | **30** | **33** |

I sorvoli di LIBB hanno vincolo (`AtOrBelow`, `Exact`) e stato verticale, ma **nessun valore**: la vIPI dice
«a o sotto … cosa?». Il tool si comporta correttamente (candidato mostrato, `writable: false`, avviso
esplicito), ma finché quei livelli non vengono inseriti il bridge non ha nulla da scrivere sui sorvoli.
È una lacuna redazionale da colmare nell'editor, non un difetto del bridge.
Comportamento confermato dal committente: **se il sorvolo è senza quota, non si scrive nulla.**

---

## 14. Esiti della fase F3 (3 agosto 2026)

`src/Vipi.AuroraBridge` — Avalonia 11.2, `net8.0`, finestra unica sempre in primo piano.
**908 test verdi**, 16 nuovi sul ViewModel.

### 14.1 Dove sta la logica

Il `BridgeViewModel` vive in **Core**, non nel progetto Avalonia, e non referenzia nessun tipo di Avalonia.
Motivo: la logica di presentazione che conta davvero — *quando il pulsante di scrittura si accende e cosa
c'è scritto sopra* — è verificabile con test normali, mentre il progetto UI resta XAML più binding. Il
marshalling sul thread della UI entra dall'esterno (`Post = Dispatcher.UIThread.Post`), così nei test è
esecuzione diretta.

### 14.2 La regola dei pulsanti spenti

Un pulsante disabilitato dice **sempre** perché, e le tre ragioni restano distinte perché si risolvono in
modi diversi:

| Situazione | Testo sul pulsante |
|---|---|
| Nessun traffico selezionato | «Nessun traffico selezionato» |
| Livello assente nella vIPI (i sorvoli di §13.4) | «Livello non scrivibile: manca il valore nella vIPI» |
| Traffico non assunto | «Traffico non assunto: Aurora rifiuta la scrittura» |
| Tutto a posto | «Scrivi «210»» |

### 14.3 Due errori trovati guardando la finestra, non i test

1. **`InitializeComponent` scritto a mano.** Avevo rimpiazzato quello generato dal compilatore XAML con
   `AvaloniaXamlLoader.Load(this)`: compila benissimo, ma è il metodo GENERATO ad assegnare i campi dei
   controlli con `x:Name`. Risultato: `NullReferenceException` al primo uso di `SiteBox`, finestra morta
   all'avvio. I test non potevano vederlo — nessun test istanzia la finestra.
2. **Ragione troncata.** Il pulsante mostrava «Livello non scrivibile: manca il…»: una spiegazione tagliata
   a metà non spiega niente. Il contenuto è ora un `TextBlock` che va a capo.

### 14.4 Verifica a video

Host locale su porta dedicata (**5199**, per non pestare l'istanza di sviluppo del committente sulla 5034),
Aurora vera con `IBE0980` selezionato e assunto, override postazione `LIBB_ES_CTR`:

```
Aurora connessa · LIBB_ES_CTR (connesso come LIZZ_AEW_CTR)
Traffico selezionato: IBE0980  LIRN → LEMD  crociera FL350  35.006 ft  ASSUNTO
⚠ Nessun candidato ha un livello scrivibile: sono tutti testuali o senza valore.
TIGRA — (dispari) → UNICOM (offline)     [Livello non scrivibile: manca il valore nella vIPI]
```

Verificata anche la **degradazione**: con l'host spento la finestra ha mostrato i candidati dalla cache
locale con l'avviso «Sito irraggiungibile: sto mostrando l'ultima risposta valida», che è esattamente il
comportamento voluto (§7 R5). Non era un guasto simulato: l'host era caduto davvero.

### 14.5 Cosa resta (F4)

Hotkey globale (con la questione dei permessi accessibilità su macOS, §10.5), packaging self-contained,
guida utente, e l'icona/tray. Il tool è già usabile in sessione così com'è.

---

## 15. Esiti della fase F4 (3 agosto 2026)

**930 test verdi**, 22 nuovi. Il tool è completo per l'uso in sessione.

### 15.1 Scorciatoia globale: `RegisterHotKey`, non un hook di tastiera

`WindowsGlobalHotkey` registra la combinazione con `RegisterHotKey(hWnd: NULL)` da un **thread proprio** che
gira un message loop: i `WM_HOTKEY` finiscono nella coda di quel thread, senza dover agganciare la finestra
di Avalonia né sottoclassare una WndProc.

L'alternativa comune — `SetWindowsHookEx`/`WH_KEYBOARD_LL` — è stata **scartata di proposito**: per reagire a
una sola combinazione intercetterebbe *ogni* tasto premuto nel sistema. Sproporzionato, e per un tool che gira
sul PC di un controllore anche sgradevole da giustificare.

Se la combinazione è già presa da un altro programma, `RegisterHotKey` fallisce in silenzio: il tool lo
rileva e lo **dice** nella striscia avvisi e nel registro. Fuori da Windows la classe non fa nulla e lo
dichiara — resta il pulsante nella finestra (§10.5 resta quindi aperta solo per macOS).

Il parsing della combinazione (`HotkeySpec.Parse`) sta in Core ed è testato: è la parte che può sbagliare.
Rifiuta le combinazioni **senza modificatori** — `L` da sola ruberebbe un tasto a tutto il sistema — e i tasti
funzione, che non sono supportati: meglio dirlo che indovinare.

### 15.2 La scorciatoia non ripiega

Se il primo candidato non è scrivibile, la scorciatoia **si ferma e spiega**, invece di cercare più in basso
un candidato scrivibile. Scrivere un livello diverso da quello che il controllore si aspetta di aver premuto
sarebbe il modo peggiore di essere utili. Coperto da test.

### 15.3 Registro locale

`bridge.log` in `%LOCALAPPDATA%\VipiAuroraBridge`, con tetto a 512 KB e rotazione a taglio secco. Registra
**cosa è stato scritto nel tag e quando** (in sessione lo si dimentica) e i rifiuti col motivo. Non solleva
mai eccezioni: un tool in cabina non deve morire perché il disco è pieno.

### 15.4 Packaging

`tools/publish-aurora-bridge.ps1` → eseguibile **autonomo, file unico**: `artifacts/bridge/win-x64/VipiAuroraBridge.exe`,
**73,7 MB**, avviato e verificato. Autonomo di proposito: gira sul PC di un controllore, e pretendere che
installi prima .NET è un costo che si paga in supporto. Lo script accetta `-Runtime osx-arm64`, **non provato**
da qui (manca la macchina): il bundle `.app` di macOS resta da fare quando servirà davvero.

### 15.5 Guida utente

[`docs/guide/aurora-bridge.md`](../guide/aurora-bridge.md), scritta per il controllore e non per lo
sviluppatore: prerequisiti (con la trappola del flag 3rd-party da riapplicare a sessione aperta), le tre vie
per scrivere, la tabella dei «perché non posso scrivere», dove guardare quando non va, e i limiti noti.
