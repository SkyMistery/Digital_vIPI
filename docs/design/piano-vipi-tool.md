# vIPI/vLOA Interactive — Documento di Pianificazione e Architettura

> ℹ️ **Documento di design.** Per lo **stato corrente del codice** vedi `README.md` (§Stato) e `HANDOFF.md` (§4). Gran parte del piano è implementata; modello dati con aggiunte (Transfer, EditGrant, lock, RowVersion) e permessi evoluti (admin via staff code + grant per-ACC + lock documento) — dettagli in HANDOFF.

**Progetto:** Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) — IVAO Italia
**Versione documento:** 0.5 (pianificazione + design UI completati)
**Data:** 13 giugno 2026 · *agg. 16 giugno 2026 (round 4–5)*
**Autore:** Carmine + assistente
**Stato:** Pianificazione e UI definiti. Codice non iniziato. → vedi `HANDOFF.md` per lo stato e i prossimi passi.
**Storico round:** §19 (r2), §20 (r3), §22 (r4: flusso/UX), §23 (r5: integrazione/auth). ADR in `docs/adr/`.

---

## 1. Sintesi esecutiva

L'obiettivo è trasformare le **vIPI** (virtual IVAO Procedures Italy — istruzioni operative di posizione) e le **vLOA** (virtual Letters of Agreement) da documenti Word statici in un **portale web interattivo, leggero e manutenibile**.

L'utente digita la postazione che intende aprire (es. `LIRP_APP`, `LIRR_NE_CTR`, `LIBP_TWR`) e il sistema raccoglie e mostra **solo** la documentazione pertinente a quella postazione, applicando automaticamente la logica top-down di IVAO:

- **Aeroporto/TWR** → la vIPI di quell'aeroporto.
- **Approach (APP)** → la vIPI dell'aeroporto + quella dell'avvicinamento.
- **ACC** → la vIPI dell'ACC + tutte le vIPI delle posizioni APP e TWR che ricadono sotto il suo AoR, più le vLOA con i centri confinanti.

Due livelli di dettaglio: una **versione ridotta** (solo tabelle e dati essenziali) e una **versione estesa** (tutta la prosa procedurale).

Il portale si collega all'**API IVAO ogni minuto** per:

1. capire se l'utente loggato è **connesso** a una postazione → abilita la **modalità live** (visione ridotta, aggiornata in tempo reale);
2. capire **quali ATC italiani (callsign `LI…`) sono online** → e **nascondere dinamicamente** dalla vista dell'utente le porzioni di documentazione che ora ricadono sotto l'AoR di una postazione subordinata effettivamente aperta (delega top-down inversa).

Chi è loggato con ruolo **CH (Chief)** o **AOD (Assistant Operations Director)** della divisione italiana può entrare in **modalità modifica** dei contenuti.

Il tool è scritto in **C# / ASP.NET Core**, con rendering server-side (Razor) e JavaScript minimo, processo unico, database **SQLite**, e meccanismo di **autenticazione IVAO SSO (OAuth2/OIDC)** facilmente collegabile al sito ospitante.

---

## 2. Glossario e concetti di dominio

| Termine | Significato |
|---|---|
| **vIPI** | Documento di istruzioni operative per una posizione/aeroporto (frequenze, AoR, coordinamenti, tecniche operative). |
| **vLOA** | Lettera di accordo tra due unità (tipicamente ACC confinanti) sulle procedure di trasferimento. |
| **Posizione / Station** | Una callsign apribile (es. `LIRP_APP`, `LIRR_NE_CTR`, `LIBP_TWR`, `LIMM_WS2_CTR`). |
| **Settore (Sector)** | Volume di spazio aereo atomico. Una posizione "possiede" uno o più settori. |
| **AoR** | Area of Responsibility: insieme dei settori di cui una posizione è responsabile in un dato momento. |
| **Top-down** | Principio IVAO: se una posizione subordinata non è online, la sua area è coperta dalla posizione superiore. |
| **AoR effettivo** | AoR di una posizione = settori propri **meno** i settori coperti da posizioni subordinate **attualmente online**. |
| **Blocco di contenuto** | Unità minima di documentazione (una tabella, un paragrafo, una procedura), taggata con il settore/posizione che la "possiede". |
| **CH / AOD** | Ruoli staff della divisione (Chief / Assistant Operations Director) abilitati alla modifica. |

---

## 3. Requisiti

### 3.1 Requisiti funzionali

1. **RF-1 — Ricerca postazione.** Homepage con campo di ricerca/autocomplete delle postazioni apribili. Inserendo una callsign (o nome), il sistema risolve la posizione e la sua gerarchia.
2. **RF-2 — Aggregazione documentale top-down.** Per la posizione scelta, raccogliere tutte le vIPI/vLOA pertinenti secondo le regole di copertura (TWR ⊂ APP ⊂ ACC) e mostrarle in modo organizzato.
3. **RF-3 — Due livelli di dettaglio.** Toggle tra *Ridotta* (solo dati tabellari: frequenze, AoR, settings, minime, separazioni) ed *Estesa* (tutta la prosa).
4. **RF-4 — Polling IVAO (1 min).** Interrogare l'endpoint ATC summary ogni minuto lato server, con cache condivisa.
5. **RF-5 — Rilevamento connessione utente.** Se l'utente loggato è connesso a una postazione, proporre l'attivazione della **modalità live** (vista ridotta in tempo reale relativa alla SUA postazione).
6. **RF-6 — Nascondimento dinamico per AoR.** Quando una posizione subordinata al mio AoR è online, i blocchi di contenuto posseduti da quella posizione/settore vengono **rimossi** dalla mia vista (es.: sono Roma ACC, apre Pisa APP → spariscono le info specifiche di Pisa; sono `LIRR_NE`, apre `LIRR_TS` → spariscono i blocchi che ora ricadono nell'AoR del TS).
7. **RF-7 — Editing per CH/AOD.** Gli utenti con ruolo abilitato possono creare/modificare/pubblicare contenuti tramite editor web, con workflow bozza → pubblicato e storico delle modifiche.
8. **RF-8 — Autenticazione IVAO SSO.** Login via OAuth2/OIDC IVAO; lettura profilo e staff position per determinare i permessi.
9. **RF-9 — Integrazione nel sito esistente.** Meccanismo di auth e di embedding semplice da collegare al sito ospitante (linguaggio backend ignoto).

### 3.2 Requisiti non funzionali

- **RNF-1 Leggerezza.** Footprint minimo: rendering server-side, JS essenziale, una sola dipendenza DB (SQLite), nessun framework SPA pesante.
- **RNF-2 Manutenibilità & modularità.** Clean Architecture, separazione netta dei layer, codice testabile, SOLID.
- **RNF-3 Documentazione.** Il progetto è documentato come un prodotto professionale (README, ADR, XML-doc, diagrammi, guida autori).
- **RNF-4 Sicurezza.** Token IVAO mai esposto al browser; segreti in configurazione protetta; HTTPS; autorizzazione basata su ruoli; audit log delle modifiche.
- **RNF-5 Resilienza.** Se l'API IVAO non risponde, il portale resta consultabile in modalità statica (ultima cache valida) e segnala il degrado.
- **RNF-6 Accessibilità & stampa.** Pagine stampabili/PDF-export per consultazione offline in cabina.
- **RNF-7 i18n.** Predisposizione multilingua (IT primario, EN per le vLOA internazionali).

---

## 4. Modello dei contenuti (cuore del progetto)

La scelta di **migrare a contenuto strutturato** è ciò che rende possibili RF-3 e RF-6. I documenti Word vengono ri-modellati in entità con tag espliciti.

### 4.1 Entità principali

```
Country (1) ──< Position (gerarchia) ──< Sector
                    │
                    ├──< Frequency
                    ├──< Document (vIPI | vLOA)
                    │        └──< ContentBlock ──> Sector (owner)  [tag AoR]
                    │                 ├─ tipo: Table | Prose | Image | List
                    │                 ├─ tier: Reduced | Extended
                    │                 └─ section: enum (AoR, Frequenze, Settings, Coordinamenti, ...)
                    └──< NeighbourAgreement (vLOA) ──> Position (confinante)
```

### 4.2 Posizioni e gerarchia

Ogni `Position` ha:

- `Callsign` (es. `LIRR_NE_CTR`), `Type` (DEL/GND/TWR/APP/CTR), `FacilityId`, `Name`, `Frequency`.
- `ParentId` → la posizione che la copre in top-down (TWR→APP→ACC sector→ACC).
- `Sectors[]` → settori atomici posseduti quando la posizione è online da sola.
- `CoverageOrder` → priorità per risolvere chi possiede un settore quando più posizioni potrebbero coprirlo.

Esempio gerarchia parziale Roma:

```
LIRR (ACC Roma)
 ├─ LIRR_NE_CTR  → settori {NE...}
 ├─ LIRR_EW_CTR  → settori {EW...}
 ├─ LIRR_SU_CTR  → settori {SU...}  (può splittare in SU + ES)
 ├─ LIRR_TS_CTR  → settori {TS...}  (sotto-settore di NE/SU)
 │    └─ (quando TS è online, "ruba" i suoi settori al NE/SU)
 ├─ LIRP_APP (Pisa)  → CTR/TMA Pisa
 │    └─ LIRP_TWR
 ├─ LIBP_APP (Pescara) → CTR Pescara
 │    └─ LIBP_TWR
 └─ ... (tutti gli APP/TWR sotto Roma ACC)
```

### 4.3 Il ContentBlock e il tag di AoR

Ogni pezzo di documentazione è un `ContentBlock` con:

- `DocumentId`, `Section` (sezione logica), `Order`.
- `Tier`: `Reduced` (compare anche nella vista ridotta) oppure `Extended` (solo estesa).
- `Format`: `Table` / `Prose` / `Image` / `List`.
- `ScopeSectorId` **(centrale)**: il settore/posizione a cui il blocco si riferisce (es. ANE).
- `Visibility` **(centrale)**: `Operational` | `Handoff` | `Always` — vedi §20. Governa quando il blocco è espanso o compresso in modalità live.
- `Shared` / `SharedKey` **(opzionale)**: se il blocco è condiviso/riusato da più posizioni (vedi §20.4).
- `Body`: contenuto (Markdown per la prosa; struttura JSON per le tabelle, così restano renderizzabili e filtrabili).

> **Regola di visibilità (RF-6) — versione raffinata in §20.**
> La semplice appartenenza all'AoR effettivo non basta: alcuni blocchi (coordinamenti/handoff) devono comparire *proprio quando* un settore è online, non scomparire. Il modello completo a due facce con **collasso morbido** è descritto in **§20** e sostituisce questa regola semplificata.

### 4.4 Tabelle come dati, non come immagini

Le tabelle dei docx (frequenze, settings/range, minime di vettoramento, FIX/FL di trasferimento) vengono modellate come **dati strutturati**. Vantaggi: compaiono nella vista ridotta, sono filtrabili per settore, restano leggere e sono facilmente aggiornabili dall'editor.

---

## 5. Architettura software (modulare, Clean Architecture)

### 5.1 Struttura a progetti (.NET solution)

```
vIPI.sln
├─ src/
│  ├─ vIPI.Domain          // entità, value objects, regole di dominio pure. Nessuna dipendenza.
│  ├─ vIPI.Application      // use case/servizi, interfacce (ports), DTO. Dipende solo da Domain.
│  ├─ vIPI.Infrastructure  // EF Core (SQLite), client API IVAO, OIDC, cache. Implementa le interfacce.
│  └─ vIPI.Web             // ASP.NET Core: Razor Pages, endpoint SSE/JSON, DI, configurazione.
└─ tests/
   ├─ vIPI.Domain.Tests
   ├─ vIPI.Application.Tests
   └─ vIPI.Web.IntegrationTests
```

Regola di dipendenza: **Web → Infrastructure → Application → Domain** (le frecce puntano verso l'interno; Domain non dipende da nulla). Questo garantisce testabilità e sostituibilità (es. cambiare SQLite con PostgreSQL tocca solo Infrastructure).

### 5.2 Servizi chiave (Application layer)

- **`IStationResolver`** — da callsign/nome a `Position` + gerarchia + documenti pertinenti (RF-1, RF-2).
- **`IAorService`** — calcola l'AoR effettivo data la lista delle posizioni online (RF-6). Cuore della logica di delega.
- **`IContentService`** — assembla la vista (ridotta/estesa) applicando il filtro AoR e il tier (RF-3, RF-6).
- **`ILiveStatusService`** — espone lo stato live derivato dalla cache IVAO (chi è online, dov'è l'utente) (RF-4, RF-5).
- **`IEditingService`** — CRUD contenuti con workflow bozza/pubblicato + audit (RF-7).
- **`IAuthorizationPolicy`** — mappa ruoli IVAO → permessi (RF-8).

### 5.3 Componenti Infrastructure

- **`IvaoApiClient`** — `HttpClient` tipizzato verso `api.ivao.aero`. Gestione token (vedi §7.3), retry con back-off, timeout.
- **`AtcPollingHostedService`** — `BackgroundService` che ogni 60 s chiama l'ATC summary e aggiorna una cache in memoria (singola fonte per tutti i client → token mai esposto, RNF-4).
- **`EfContentRepository`** — EF Core su SQLite, migrations versionate.
- **`IvaoOidcHandler`** — integrazione OpenID Connect.

### 5.4 Flusso runtime (richiesta di consultazione)

```
Browser ──GET /station/LIRR_NE_CTR?tier=reduced──> Web (Razor Page)
   Web → StationResolver.Resolve("LIRR_NE_CTR")
   Web → LiveStatusService.GetOnlineItalianAtc()        (da cache, no chiamata diretta)
   Web → AorService.ComputeEffectiveAor(position, online)
   Web → ContentService.BuildView(position, effectiveAor, tier)
   Web ← HTML renderizzato (+ eventuale stream SSE per il live)
```

Per la **modalità live** (RF-5): una connessione **Server-Sent Events** (`text/event-stream`) spinge al browser un evento ogni volta che la cache IVAO cambia, e la pagina aggiorna solo le parti interessate. SSE è più leggero di WebSocket e perfetto per aggiornamenti unidirezionali server→client.

---

## 6. Logica di nascondimento per AoR — dettaglio algoritmico

> ⚠️ **Aggiornamento:** l'algoritmo sotto calcola l'AoR effettivo (chi possiede cosa), che resta valido come *base*. La **regola di visibilità definitiva** (operativo vs handoff, collasso morbido, unificazione) è in **§20** e raffina questo passaggio: il risultato di `effectiveAor` alimenta il calcolo dello stato COVERED/ONLINE di ogni settore.

Questo è il punto più delicato; lo specifico esplicitamente.

```
Input:
  P            = posizione aperta dall'utente
  Online       = insieme delle callsign ATC italiane online (da cache IVAO)

1. ownedByP   = settori di P  ∪  settori di tutte le posizioni subordinate a P (chiusura transitiva top-down)
2. delegated  = ∅
   per ogni posizione Q in Online tale che Q è subordinata a P (Q ≠ P):
        delegated ← delegated ∪ settori posseduti da Q (e dai suoi subordinati non-online... ricorsivo)
3. effectiveAor(P) = ownedByP \ delegated
4. Per ogni ContentBlock B dei documenti pertinenti:
        mostra B  ⟺  B.OwnerSectorId è nullo  OR  B.OwnerSectorId ∈ effectiveAor(P)
```

Esempi (coerenti con la richiesta):

- **Sono `LIRR_NE` (ACC, top-down su tutto il ACC), apre `LIRP_APP`:** i settori di Pisa escono dal mio AoR effettivo → i blocchi taggati con i settori di Pisa (la vIPI specifica di Pisa) spariscono dalla mia vista.
- **Sono `LIRR_NE`, apre `LIRR_TS`:** i settori del TS sono sottoinsieme di quelli che coprivo → i relativi blocchi spariscono.
- **Nessun subordinato online:** vedo tutto top-down (AoR effettivo = tutti i settori miei e dei subordinati chiusi).

Punto di attenzione progettuale: la **granularità dei settori** determina la precisione del nascondimento. Conviene partire da una granularità "per posizione" (un settore logico per ogni callsign apribile) e raffinare in sotto-settori solo dove serve (es. split ES/SU, TS). Definiremo questa mappa insieme, è un lavoro di data-entry più che di codice.

---

## 7. Integrazione API IVAO

### 7.1 Endpoint usati

- **ATC summary** (`GET /v2/tracker/now/atc/summary`) — lista degli ATC online; estraggo callsign, posizione/frequenza, userId. Filtro per prefisso `LI` per l'Italia.
- **Whazzup v2** (in alternativa/integrazione) per dettagli sessione ATC.
- **OIDC `.well-known/openid-configuration`** per gli endpoint di login e gli scope disponibili.
- **Profilo utente / staff positions** per determinare CH/AOD (scope da richiedere).

### 7.2 Polling lato server

`AtcPollingHostedService` interroga ogni 60 s, normalizza e salva in cache. Tutti i client leggono dalla cache: una sola chiamata al minuto a IVAO indipendentemente dal numero di utenti (RNF-1, RNF-4). Configurabile l'intervallo.

### 7.3 Gestione del token (importante)

Il token che hai incollato è un **access token a scadenza breve** (campo `exp` ≈ 30 min): non va messo nel codice. In produzione si usa il flusso **client_credentials** (app-to-app): l'app si autentica con `client_id`/`client_secret` e ottiene/rinnova automaticamente l'access token. Segreti in *user-secrets* (dev) e variabili d'ambiente / secret store (prod). **Mai** nel repository, mai nel browser.

> Azione richiesta: registrare l'applicazione su IVAO Developers e inviare email a `web@ivao.aero` con gli scope necessari e i redirect URL. È un prerequisito esterno da avviare presto perché ha tempi di approvazione.

---

## 8. Autenticazione e autorizzazione

- **Login:** OpenID Connect IVAO SSO. ASP.NET Core ha supporto OIDC nativo (`AddOpenIdConnect`).
- **Sessione:** cookie di autenticazione del portale dopo il login IVAO.
- **Ruoli:** dal profilo/staff position IVAO ricavo se l'utente è CH o AOD della divisione IT → claim/policy `CanEdit`.
- **Policy:**
  - *Anonimo / utente IVAO*: sola lettura (ridotta/estesa).
  - *Utente connesso a una posizione*: in più, modalità live.
  - *CH / AOD IT*: in più, editing e pubblicazione.
- **Integrazione col sito ospitante (confermata):** il portale viene servito come **reverse proxy** sotto il path `https://it.ivao.aero/sop`. Stesso dominio (niente problemi di cookie cross-site), URL pulito, il portale resta un'app .NET indipendente dietro al proxy (es. Nginx/Apache del VPS divisionale). L'astrazione `IAuthorizationPolicy` permette in futuro di accettare anche un'identità già fornita dal sito ospitante senza riscrivere la logica.

---

## 9. Editing dei contenuti (CH/AOD)

- Editor web per `ContentBlock`: prosa in **Markdown** (semplice, diff-abile), tabelle con editor a griglia (JSON sotto).
- Ogni blocco è assegnabile a una **sezione** e a un **settore owner** (per il nascondimento) tramite menu a tendina popolati dalla gerarchia.
- Workflow **bozza → pubblicato**, con anteprima e **storico versioni** (audit: chi, quando, cosa).
- **Costruttore di gerarchia**: sezione dell'editor dove gli editor definiscono manualmente le relazioni padre→figlio tra posizioni, gli split/unificazioni e l'assegnazione settore→owner (vedi §17.1). Le posizioni di base sono già importate dall'API; qui si aggiunge la struttura operativa.
- **Nessun import dai docx**: si crea solo la struttura (schema + editor); i contenuti vengono inseriti a mano dagli autori.

---

## 10. Frontend (leggero)

- **Razor Pages** per il rendering. CSS minimale brand-compliant (un solo file di tema, vedi §15), senza framework SPA pesanti.
- **JavaScript**: solo per (a) autocomplete della ricerca, (b) toggle ridotta/estesa, (c) client SSE per il live. Nessun bundler obbligatorio; eventualmente un piccolo `app.js`.
- **Stampa/PDF**: CSS `@media print` (foglio `vipi-print.css`), **senza** endpoint di export: il PDF è quello
  di «Salva come PDF» del browser. Realizzato il 30 lug 2026, vedi `../feature/2026-07-30-stampa-documenti.md`.
- **Mobile-friendly** per consultazione da tablet in sessione.

### 10.1 Riferimento di stile UI

Impostazione ispirata alle pagine SOP/LoA della divisione austriaca (come riferimento di layout, non di contenuto):

- [SOP LOWW Wien](https://at.ivao.aero/sop/loww-wien)
- [LoA LOVV](https://at.ivao.aero/loa/loa-lovv)

Direzione di design da replicare in chiave IVAO Italia + brand: documento con **indice/sommario navigabile** (sidebar o TOC ancorato), **sezioni collassabili**, **tabelle pulite** per frequenze/AoR, intestazione con titolo posizione e ciclo AIRAC, barra superiore con toggle **Ridotta/Estesa** e indicatore di **stato live** (badge verde/rosso). Layout a colonna singola leggibile, allineato a sinistra, tipografia Nunito Sans/Poppins.

> Nota: al momento della stesura le due pagine di riferimento sono client-rendered e non sono state leggibili via fetch; il layout esatto andrà osservato direttamente in fase di design (F2).

---

## 11. Documentazione del progetto (RNF-3)

Da produrre come parte integrante:

1. **README.md** — setup, build, run, deploy.
2. **/docs/architecture.md** — questo documento mantenuto aggiornato + diagrammi.
3. **/docs/adr/** — Architecture Decision Records (una decisione per file: scelta SQLite, scelta SSE vs WebSocket, ecc.).
4. **/docs/content-authoring-guide.md** — guida per CH/AOD su come scrivere e taggare i contenuti.
5. **/docs/data-model.md** — schema DB + diagramma ER.
6. **XML doc comments** sul codice pubblico → documentazione API generabile.
7. **/docs/reference/sector-map.md** — la mappa posizioni↔settori (il data-entry che faremo insieme).

---

## 12. Roadmap a fasi

| Fase | Obiettivo | Output |
|---|---|---|
| **F0 — Setup** | Solution, repo, CI minima, ADR iniziali, registrazione app IVAO (avviare subito). | Scheletro a 4 progetti + pipeline build/test. |
| **F1 — Dominio & dati** | Modello dominio, EF Core + SQLite, mappa settori iniziale (granularità per-posizione). | DB migrabile + seed di una ACC pilota (es. Roma). |
| **F2 — Consultazione statica** | StationResolver + ContentService + viste ridotta/estesa. | Homepage di ricerca + pagina postazione funzionante (senza live). |
| **F3 — Integrazione IVAO** | Polling, cache, ATC summary, gestione token. | Stato online consultabile internamente. |
| **F4 — Logica AoR & live** | AorService + nascondimento dinamico + SSE + modalità live. | RF-5 e RF-6 complete. |
| **F5 — Auth & editing** | OIDC IVAO, ruoli, editor CH/AOD, workflow + audit. | RF-7, RF-8. |
| **F6 — Rifinitura** | Stampa/PDF, i18n, accessibilità, hardening, docs finali, deploy. | Release 1.0. |

Suggerimento: partire con **una sola ACC pilota** (Roma o Milano, già negli esempi) per validare modello dati e logica AoR prima di estendere a tutta l'Italia.

---

## 13. Proposte aggiuntive (valuta tu)

- ✅ **Vista mappa AoR** *(accettata)*: render delle shape geografiche con evidenza dei settori attualmente coperti vs delegati. Le shape si ottengono dalle API IVAO `GET /v2/ATCPositions/{callsign}` (posizioni legate a un aeroporto, es. LIRF) oppure `GET /v2/subcenters/{callsign}` (posizioni legate a un ACC, es. LIRR). Stessi endpoint utili anche per **derivare automaticamente la gerarchia/owner dei settori** (vedi §17).
- ✅ **Diff visivo tra versioni** *(accettata)*: l'editor degli autori è un'interfaccia distinta dalla consultazione, con confronto tra versioni.
- ✅ **Notifica "documento aggiornato"** *(accettata)*: agli ATC connessi quando un CH pubblica una modifica durante una sessione (via SSE).
- ✅ **Export "briefing di posizione"** *(accettata, se leggero)*: singolo PDF al momento dell'apertura.
- 📄 **API pubblica di sola lettura** *(solo documentata, non implementata ora)*: predisporre l'astrazione ma rimandare l'implementazione (vedi §18).
- ✅ **Validazione di coerenza** *(accettata)*: due livelli — strutturale bloccante (integrità referenziale su link espliciti) e semantica non bloccante (scan di FIX/aerovie/callsign confrontati con un dataset di riferimento legato all'AIRAC). Dettaglio in §17.
- ✅ **Cache offline (PWA)** *(accettata, se non pesa)*: per consultare la ridotta anche con rete instabile.

---

## 14. Rischi e questioni aperte

1. **Granularità settori vs sforzo di data-entry.** Scelta confermata: **modellazione granulare fin da subito** (tutti i sotto-settori). Più lavoro di data-entry ma modello completo. → Mitigazione: derivare la base dalle API IVAO (§17).
2. ~~Qualità dell'import dai docx.~~ **Non applicabile**: nessun import automatico, i dati si inseriscono a mano. Si crea solo la **struttura** (schema + editor).
3. **Tempi di approvazione app IVAO.** Dipendenza esterna. Richiesta in avvio. → Avviare in F0.
4. **Mappatura ruoli CH/AOD.** Confermato: ruoli letti da `GET /v2/users/{vid}/userStaffPositions`. → Nessuna whitelist necessaria, ma resta come fallback.
5. **Definizione precisa delle relazioni top-down** (chi copre cosa) per l'intera Italia. → Vedi chiarimento e strategia in §17; documentato in `sector-map.md`.

---

## 15. Identità visiva — Brand IVAO (obbligatorio)

Il portale **deve** rispettare le linee guida brand IVAO ([font](https://brand.ivao.aero/font/), [colori](https://brand.ivao.aero/colors/)).

### 15.1 Palette colori

| Ruolo | Nome | HEX | Uso |
|---|---|---|---|
| Primario | Blue | `#0D2C99` | colore principale, titoli, header, elementi chiave |
| Secondario | Light Blue | `#3C55AC` | sotto-titoli, accenti secondari |
| Secondario | Grey | `#D7D7DC` | sfondi, bordi, superfici neutre |
| Semantico | Green | `#2EC662` | stato OK / online (solo interazioni) |
| Semantico | Yellow | `#F9CC2C` | warning (solo interazioni) |
| Semantico | Red | `#E93434` | errore / offline (solo interazioni) |
| Semantico | Info Blue | `#7EA2D6` | info (solo interazioni) |

> I colori semantici si usano **solo per le interazioni/stati** (es. badge "online" verde, banner degradato rosso), non come complemento decorativo della palette principale. Tutti definiti come **CSS custom properties** (`--ivao-blue`, ecc.) in un unico file di tema.

### 15.2 Tipografia

- **Nunito Sans** → titoli/header (tono serio e ordinato).
- **Poppins** → testo lungo/prosa procedurale (leggibile e amichevole).
- Allineamento **a sinistra** (no center align, salvo header brevi su banner/bottoni).
- Sizing **Bootstrap 5** (1rem = 16px): h1 40px, h2 32px, h3 28px, h4 24px, h5 20px, h6 16px; body 16px.
- Header in colore primario; sotto-header in light blue; su sfondi scuri header bianco e sotto-header in colore secondario.
- Font caricati **self-hosted** (file woff2 nel progetto) per leggerezza e per non dipendere da CDN esterne.

---

## 16. Ciclo AIRAC

Requisito: ogni documento riporta automaticamente il **ciclo AIRAC dell'ultimo aggiornamento**.

- Un `AiracService` (nel Domain/Application) mappa una data al ciclo AIRAC corrispondente (cicli di 28 giorni, formato `YYMM` es. `2606`), partendo da una data di epoca AIRAC nota.
- Al salvataggio/pubblicazione di un documento si memorizza `LastUpdatedUtc` e si calcola `LastUpdatedAiracCycle`.
- Il ciclo è mostrato nell'intestazione di ogni vIPI/vLOA (es. "Aggiornato AIRAC 2606").
- Lo stesso servizio alimenta la ri-validazione dei riferimenti nav a ogni cambio AIRAC (§17.2).

---

## 17. Modello top-down e validazione di coerenza (dettaglio)

### 17.1 Cosa sono le "relazioni top-down" e come le popoliamo

Sono la **tabella di verità** che stabilisce, per ogni settore, chi lo possiede e chi lo assorbe quando il proprietario è offline (catene di copertura, split ACC NE/EW/SU/ES/TS, unificazioni tipo `WS2 = WS2+ES2+WS5+ES5`, e quale settore ACC copre ciascun APP/TWR chiuso). È **dato**, non codice, e la sua correttezza determina la correttezza del nascondimento per AoR.

Strategia di popolamento (modellazione granulare confermata):

> **Nota importante (correzione):** le API IVAO **non** espongono la gerarchia operativa top-down. Restituiscono solo l'elenco delle posizioni e a quale **ACC** appartengono (più le shape geografiche). La gerarchia operativa (chi copre chi, split, unificazioni) **va specificata a mano** da chi ha accesso editor.

1. **Import automatico delle posizioni italiane** — l'app importa dal DB/API tutte le posizioni italiane (callsign, ACC di appartenenza, frequenza, shape da `GET /v2/ATCPositions/{callsign}` e `GET /v2/subcenters/{callsign}`). Questo popola l'**anagrafica piatta** di posizioni e settori, **senza** relazioni gerarchiche.
2. **Definizione manuale delle relazioni** — gli editor, dall'interno del sistema, specificano le relazioni tra le postazioni: `ParentId`/catene di copertura, `CoverageOrder`, regole di split/unificazione e assegnazione dei `ContentBlock` agli `OwnerSector`. È un'apposita sezione dell'editor (un "costruttore di gerarchia").
3. **Documentazione** della mappa risultante in `docs/reference/sector-map.md`, sorgente di verità leggibile dagli autori.

Conseguenza progettuale: serve una **UI dedicata di gestione gerarchia** (drag&drop o form padre→figli + assegnazione settori), perché questo dato non arriva da nessuna fonte esterna.

### 17.2 Validazione di coerenza (due livelli)

1. **Strutturale (bloccante):** integrità referenziale. Una vLOA deve puntare a posizioni esistenti; un `ContentBlock.OwnerSectorId` deve riferire un settore reale. Garantita da foreign key + check al salvataggio.
2. **Semantica (warning, non bloccante):** un validatore scansiona prosa e tabelle per token che sembrano entità nav (FIX = 5 lettere maiuscole, aerovie, callsign `LIxx_…`, FL) e li confronta con un **dataset di riferimento** (nav-data FIX/aerovie + tabella posizioni). I riferimenti non trovati generano avvisi all'autore (es. "FIX BAVOM non presente — AIRAC 2606"), senza bloccare la pubblicazione. A ogni cambio AIRAC i documenti si ri-validano e i riferimenti obsoleti vengono segnalati.

---

## 18. API pubblica read-only ~~(documentata, non implementata)~~ → **realizzata il 3 ago 2026, ristretta ai trasferimenti**

L'idea originale era predisporre l'astrazione senza implementarla: endpoint JSON di sola lettura per integrazioni future (EuroScope/Aurora), contando sul fatto che il layer Application è già separato dalle viste.

**È andata proprio così, e il primo consumatore è arrivato:** `POST /vsop/api/v1/transfers/resolve` (in `MapVipiModule`) riusa `ITransferRepository`/`ITopologyProvider`/`IOnlineAtcProvider` senza toccare la logica esistente, e alimenta il tool desktop del bridge Aurora. La superficie è **volutamente stretta** — un solo endpoint, il caso d'uso «a che livello cedo questo volo» — invece della vista documentale generica ipotizzata qui: una vista `?tier=reduced` avrebbe dovuto versionare l'intero modello dei contenuti, questo versiona solo un contratto di trasferimenti.

Dettaglio: [`piano-aurora-bridge.md`](piano-aurora-bridge.md) · contratto: [`../reference/api-aurora-bridge.md`](../reference/api-aurora-bridge.md).

---

## 19. Decisioni confermate (round 2)

| Tema | Decisione |
|---|---|
| ACC pilota | **Roma** |
| Granularità settori | **Granulare da subito** (tutti i sotto-settori) |
| Import dati | **Nessun import**: solo struttura/schema + editor; dati inseriti a mano |
| Hosting | **VPS divisionale** |
| Convenzione lingua | vIPI in **IT**, vLOA in **EN** |
| Path di pubblicazione | `https://it.ivao.aero/sop` via **reverse proxy** (stesso dominio) |
| Gerarchia top-down | **Manuale**: API importano solo posizioni + ACC; le relazioni le definiscono gli editor nel sistema |
| Riferimento UI | Pagine SOP/LoA IVAO Austria (§10.1) reinterpretate col brand IT |
| Auth & ruoli | IVAO SSO; ruoli da `GET /v2/users/{vid}/userStaffPositions` (CH/AOD ⇒ editor) |
| Brand | Obbligatorio: palette §15.1, font Nunito Sans + Poppins §15.2 |
| AIRAC | Ogni documento mostra il ciclo AIRAC dell'ultimo aggiornamento (§16) |
| Mappa AoR | Shape da API ATCPositions/subcenters |
| Editor autori | Interfaccia distinta dalla consultazione, con diff versioni |
| Notifiche | Push SSE agli ATC connessi su nuova pubblicazione |
| Validazione coerenza | Strutturale (bloccante) + semantica (warning) §17.2 |
| API pubblica | Solo documentata, non implementata (§18) |
| PWA offline | Accettata se leggera |
| Richiesta app IVAO | In avvio |

---

---

## 20. Modello di visibilità raffinato (round 3) — la logica centrale

Questa sezione **sostituisce** la regola semplificata di §6: il nascondimento per AoR non è un semplice "mostra se nel mio AoR", ma una condizione a due facce con collasso morbido.

### 20.1 Stato di un settore

In modalità live, per la posizione *P* aperta e l'insieme *O* delle posizioni online, ogni settore *S* nel dominio top-down di *P* è in uno stato:

- **COVERED** — nessuna posizione subordinata che possiede *S* è online ⇒ lo copre *P*.
- **ONLINE** — una posizione subordinata che possiede *S* è online ⇒ lo gestisce un altro controllore.

Lo stato dipende dalla **risoluzione dell'unificazione** (§20.3).

### 20.2 Le tre visibilità del blocco

Ogni `ContentBlock` ha un campo `Visibility` riferito al suo `ScopeSectorId = S`:

| Visibility | Significato | S = COVERED | S = ONLINE |
|---|---|---|---|
| **Operational** | procedure interne che esegui *tu* quando copri S (es. tecniche operative dell'ANE) | **espanso** | **compresso** |
| **Handoff** | frequenza + coordinamenti verso S, utili solo quando S è gestito da altri | **compresso** | **espanso** |
| **Always** | info valide a prescindere (es. minime generali) | espanso | espanso |

Esempio richiesto (WS2 verso ANE):

- **ANE offline** → WS2 copre ANE: vede *espanse* tutte le procedure operative dell'ANE (`Operational`), mentre il blocco frequenza+coordinamenti (`Handoff`) è *compresso* (non gli serve, è lui stesso ANE).
- **ANE online** → le procedure operative ANE si *comprimono*, e compare *espanso* solo il blocco frequenza + coordinamenti (`Handoff`).

### 20.3 Collasso morbido (non rimozione)

I blocchi non pertinenti **non vengono rimossi**: diventano una **striscia compressa, etichettata e sempre riespandibile** (es. "ANE online — dettagli operativi delegati, espandi per vedere" oppure "Coordinamenti verso ANE — non necessari ora"). Vantaggi: sicurezza operativa (il controllore può sempre recuperare l'info se l'API è stale o se ne ha bisogno) e nessun salto/disorientamento della pagina a metà sessione. Si applica in **entrambe le direzioni** (info delegate via + info handoff non ancora necessarie).

In **modalità live OFF**: tutto espanso, nessuna condizione applicata (consultazione completa del documento).

### 20.4 Coordinamento come entità relazionale

I coordinamenti riguardano *due* posizioni, non un solo settore. Si modellano come blocco `Handoff` con `FromSectorId` / `ToSectorId`; sono rilevanti (espansi) solo quando i due lati sono **separati e online**, altrimenti compressi. Questo evita di "appiccicare" forzatamente un coordinamento a un singolo owner.

### 20.5 Regole di unificazione (motore formale, editabile)

Il legame callsign↔settori non è 1:1 (es. `LIMM_WS2` copre WS2+ES2+WS5+ES5 unificati). Serve un **motore di regole dichiarativo** per dedurre, dato *O*, quale posizione possiede ciascun settore:

- Regole per ACC, del tipo: *"WS2 possiede {WS2,ES2,WS5,ES5} salvo se WS5 è online, allora WS2 possiede {WS2,ES2} e WS5 possiede {WS5,ES5}"*.
- Le regole sono **dato editabile** da CH/AOD nella sezione "costruttore di gerarchia" dell'editor (§9), non hardcoded.
- Il `IAorService` applica il motore prima di calcolare gli stati dei settori.

### 20.6 Blocchi condivisi o duplicati

Alcuni blocchi (minime generali, best practice ricorrenti) possono essere **condivisi** tra più posizioni: supportiamo sia il **riuso per riferimento** (`SharedKey`, un'unica fonte mostrata in più posizioni — modifica una volta, aggiorna ovunque) sia la **duplicazione** (copia indipendente, quando le varianti divergono). L'autore sceglie caso per caso.

### 20.7 Proprietà delle vLOA

Sul portale italiano, una vLOA è modificabile **solo** da CH o membro AOD italiano. Il lato confinante non ha accesso in scrittura qui (eventuale coordinamento avviene fuori dal tool). Edit-right verificato via `userStaffPositions` (§8).

---

## 21. Temi minori da fissare (non bloccanti)

- **Test della logica AoR**: è la parte più rischiosa ⇒ suite di test, preferibilmente **property-based**, con scenari (ANE on/off, split/unificazione, vLOA neighbor on/off) come casi di verità.
- **SQLite su VPS**: backup periodico del file DB, migrazioni versionate, **concorrenza ottimistica** sugli edit (rowversion) per evitare sovrascritture tra autori.
- **Lingua**: scelta **per-documento fissa** (IT per vIPI, EN per vLOA), non toggle utente.

---

*Prossimo passo proposto:* approvato il piano, parto dalla **Fase F0/F1** generando lo scheletro della solution .NET a 4 progetti, il modello di dominio (con `AiracService`, entità Position/Sector/ContentBlock con `Visibility`, e il motore di unificazione), lo schema EF Core e il file di tema brand-compliant, con un seed **strutturale** (senza dati) della ACC di Roma.

---

## 22. Decisioni round 4 (16 giugno 2026)

Recepiscono il flusso utente e le risposte in `../history/review-flusso-gap.md`. Dettaglio modello dati in `../spec/modello-dati.md` §7.

### 22.1 Navigazione: 4 ACC + ricerca/live **convivono**

L'homepage mostra i **4 ACC** (LIRR, LIMM, LIPP, LIBB) come porta d'ingresso documentale. Selezionato un ACC, l'utente sceglie tra: **vIPI dell'ACC**, un **aeroporto**, un **avvicinamento non remotizzato**, o una **vLOA** con uno stato estero. Una **barra di navigazione persistente** consente sempre di cambiare ACC, più **breadcrumb gerarchico** (ACC › Aeroporto/APP › Sezione).

In parallelo restano attivi la **ricerca per callsign** (RF-1) e il **rilevamento postazione + modalità live** (RF-5/RF-6): la navigazione a 4 ACC è la consultazione, la logica AoR resta agganciata alla postazione aperta.

Apertura di default: **versione Estesa (full)**, con toggle a Ridotta.

### 22.2 Avvicinamenti remotizzati vs non remotizzati

Gli APP **remotizzati** hanno la documentazione dentro la **vIPI di ACC**; i **non remotizzati** hanno un **documento proprio** (selezionabile al punto 3.2 del flusso). Attributo `Position.ApproachKind` (`Remotized`/`Standalone`).

### 22.3 Template d'ordine della vIPI di ACC

Ordine canonico (aggiornato mockup 20 giu): (1) sommario dinamico, (2) riquadro separazioni radar, (3) mappa AoR ACC, (4) tabella configurazioni operative, (5) tabella frequenze, (6) tabella minime di vettoramento *(future)*, (7) sezione coordinamenti con sottosezione per ogni settore interagente → per aeroporto → lista trasferimenti con CoP + tabella riepilogo, (8) **settore SCCAM** (AoR dal DB IVAO + descrizioni), (9) **aree regolamentate** (shape + range quota + descrizione). **SCCAM e aree regolamentate sono sezioni di pari livello, fuori dai coordinamenti** (prima erano gruppi interni ai coordinamenti). Realizzato tramite **sezioni annidate fino a 3 livelli** (`DocumentSection`).

### 22.4 Vista ridotta — accordion UI

La ridotta mostra: tabella frequenze, tabelle trasferimenti, e un **selettore rapido degli aeroporti** sotto il proprio ACC. Comportamento **accordion**: aprendone uno, gli altri si comprimono (riespandibili a mano). Questo collasso è **puramente di presentazione** e **non sovrascrive** lo stato live/AoR (vedi `../spec/logica-aor.md`).

### 22.5 Blocchi callout colorati (nuovo)

Riquadri informativi piazzabili ovunque, in quattro varianti semantiche brand: **Info** (`#7EA2D6`), **Success** (`#2EC662`), **Warning** (`#F9CC2C`), **Danger** (`#E93434`). Estensione deliberata dei colori semantici (§15.1) a contenuto editoriale: da annotare nella guida di stile.

### 22.6 Quality-of-life accettate

Deep-link a sezione/blocco; ricerca full-text trasversale; pannello "chi è online nel mio dominio"; anteprima "vista controllore" nell'editor; toggle densità tabelle. **Validazione CoP** con fix dal sectorfile + whitelist per i CoP convenzionali tipo `Jx` (non sono fix reali). **"Cosa è cambiato dall'ultimo AIRAC"**: realizzata come **pagina a parte**, non dentro il documento. Indicatore di **AIRAC del sectorfile** nelle sezioni minime per verificare l'allineamento.

### 22.7 Export PDF

Modifica della proposta §13: è esportabile in PDF **solo la versione Estesa** (niente export della ridotta/kneeboard).

Realizzato il 30 lug 2026 come **stampa del browser**, non come endpoint: foglio `vipi-print.css` + tasto
«Stampa» sui viewer documento (aeroporto, vIPI ACC, APP non remotizzato, vLOA) + intestazione di stampa
`PrintMeta`. Dettaglio e limiti noti in `../feature/2026-07-30-stampa-documenti.md`.

### 22.8 Minime di vettoramento — **implementazione FUTURE**

Documentate ora, fuori dalla prima release. Fonte: **sectorfile della divisione su GitHub** (non API, non a mano), via `SectorfileImportService`, ri-validate a ogni cambio AIRAC. Possibili più sezioni minime per documento. Dettaglio in `../spec/modello-dati.md` §7.5.

### 22.9 Settore SCCAM (nuovo — 20 giugno)

Sezione top-level dedicata al coordinamento con la **Circolazione Aerea Militare (CAM)**. La sua **AoR proviene sempre dal database IVAO** (non editabile a mano), più descrizioni a testo libero. Modellata come `DocumentSection` con un blocco `AorMap` + blocchi prosa: **nessuna modifica allo schema**. Collocata fuori dai coordinamenti, prima delle aree regolamentate (§22.3).

### 22.10 Struttura della vLOA (nuovo — 20 giugno)

Template della lettera di accordo (lingua **EN**): **Purpose · Areas of Responsibility** (due AoR: italiana + confinante, entrambe dal DB IVAO) **· Frequencies** (due tabelle: parte italiana + controparte) **· General procedures · Coordination** (stessa identica struttura dei coordinamenti della vIPI ACC) **· Military areas coordination and management · Validity and Revision**. Tutto su `DocumentSection`/`ContentBlock`; editabile **solo lato Home (LIRR)**, parte Neighbour in sola lettura (§20.7). L'avvicinamento **non remotizzato** organizza i coordinamenti come il settore TW1: una sezione **verso ACC** e una **verso la/e torre/i** (più torri se l'APP copre più scali, es. Catania).

---

## 23. Integrazione nel sito e autenticazione (round 5 — 16 giugno 2026)

> Questa sezione **sostituisce** l'impostazione di §8 (app separata dietro reverse proxy, backend host "ignoto"). Decisione formale in `ADR-0002`.

### 23.1 Lo stack del sito host non è ignoto

L'analisi del codice del sito divisionale (`Ivao.It`) ha mostrato che è **ASP.NET Core + Blazor Server + ASP.NET Core Identity**, con **IVAO OIDC come external login** (progetto `Ivao.OpenIdConnect`). Cade quindi l'assunzione RF-9 di "backend ignoto": è lo **stesso stack** della vIPI. È inoltre in sviluppo un **secondo sito** della divisione, stesso stack ma struttura diversa, su cui la vIPI dovrà poter migrare.

### 23.2 Identità già disponibile nei claim

Dopo il login, il `ClaimsPrincipal` contiene l'intero profilo IVAO: `id` (vid), `centerId` (ACC), `divisionId`, `isStaff`, `userStaffPositions` (es. `IT-DIR`, `IT-WM`). Esistono già `IvaoUser`, `ClaimsPrincipalIvaoExtensions`, le `Policies` (`IsStaff`) e il merge delle staff position in **ruoli Identity** (`IvaoRolesHandler`). Conseguenza: il rilevamento **CH/AOD non richiede** la chiamata API `userStaffPositions` (§14.4); si legge dai claim di sessione.

### 23.3 La vIPI è una RCL Blazor integrabile

Il layer UI è una **Razor Class Library** che il sito host monta su una rotta (es. `/sop`); la logica resta nei progetti Clean Architecture separati. Girando **in-process** nell'host, eredita l'autenticazione esistente: **niente doppio login, niente API esposte, niente ticket store distribuito**. L'identità è acquisita tramite l'astrazione neutra **`ICurrentUserProvider`**, con adapter intercambiabili.

### 23.4 Tre scenari di deploy, una sola codebase

- **A** — embedded nel sito attuale: adapter che legge il `ClaimsPrincipal`.
- **B** — embedded nel sito nuovo (stesso stack): stesso adapter, config diversa.
- **C** — app autonoma futura: host .NET minimo dedicato + adapter **IVAO OIDC proprio** (qui servono client e redirect URL).

Spostare la vIPI = referenziare RCL + progetti logici nel nuovo host e fornire l'adapter; nessuna riscrittura. Regola di portabilità: RCL e logica **non dipendono da tipi specifici dell'host** (solo da `ICurrentUserProvider` e da un modello utente neutro). Dettaglio e alternative scartate in `ADR-0002`.

### 23.5 Impatto su live/SSE

Su host Blazor Server gli aggiornamenti push viaggiano nativamente sul **circuito Blazor**: da valutare (ADR successivo) se SSE (ADR-0001 D6) resti necessario o se basti il circuito per la modalità live.
