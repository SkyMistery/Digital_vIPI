# Le API non sono mai anonime: chiavi per i client — carta (13 settembre 2026)

> **Stato: ✅ ESEGUITA il 13 settembre 2026, nel pacchetto 1.26.0** (`cad6698`, da caricare; migrazione
> additiva `ChiaviApi`). Provata sul pacchetto con `.claude/skills/verifica-live/chiavi-verifica.js`.
> Passi 2-4 del §7 da fare in produzione dopo il carico.
> Nasce da **T-017** della revisione del 13 settembre
> ([`history/audit-2026-09-13-revisione-totale-2.md`](../history/audit-2026-09-13-revisione-totale-2.md)).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Domande del §8 risposte dal committente lo stesso giorno.
>
> Dove sta: `ApiClient` (Domain) · `ChiaviApi.cs` (Application: forma della chiave, `EmittentiChiaviApi`,
> `ApiClientService`, `VerificaChiaveApi`) · `EfApiClientStore` · `PortaDelleApi` (Hosting, l'unico punto che
> decide 401/403/429) · `AdminApiKeysPage` (`/services/vsop/admin/api-keys`) · bridge desktop: campo «Chiave
> API» e `--key`/`VIPI_API_KEY` nella CLI.

## 1. Il problema

Due endpoint del sito sono **API per altri programmi**, e oggi rispondono a chiunque:

| Endpoint | Chi lo usa | Oggi |
|---|---|---|
| `GET /vsop/api/v1/atc/sessions` | il validatore dei tour della divisione, altri strumenti | **anonimo, acceso in produzione** |
| `POST /vsop/api/v1/transfers/resolve` | il tool desktop del bridge Aurora | anonimo, **spento** di default (`AuroraBridge:Enabled`) |

La revisione aveva trovato due carte che si contraddicevano: le statistiche (§14) riservano le sessioni di un
altro VID allo staff con audit, l'archivio (§8) dichiarava l'endpoint anonimo di proposito.

Il resto non è un'API per altri programmi e **resta fuori** da questa carta: `/services/vawos/api/{icao}` è il
backend della pagina pubblica del quadro vAWOS; `/vsop/live/atc` dal 1.25.4 è solo per chi è entrato;
`/vsop/health`, `/vsop/health/ready` e `/vsop/ping` sono sonde di monitoraggio.

## 2. Le decisioni del committente (13 settembre 2026)

1. **Le API non sono mai anonime.** Ogni chiamata porta un mezzo di autenticazione.
2. **Chi è autenticato all'API vede le sessioni di qualunque VID.** La chiave è l'autorizzazione: non ci sono
   filtri per persona dietro. (Chiude il conflitto §14/§8 a favore di questa regola; §14 resta vera per le
   **pagine**, dove chi guarda è una persona.)
3. **L'archivio resta acceso e aperto finché l'autenticazione non esiste**: il validatore dei tour non si ferma.
4. **Le chiavi le emettono gli amministratori membri di IT-HQ o di IT-WD, oppure il committente.**

## 3. Pre-flight

**1. Modello — aggiungo un concetto o ne esiste già uno?** Nuovo: un **cliente API** con la sua chiave. Non è
un utente (non ha VID né livello), non è una promozione (`RoleOverrides`). Esiste invece già tutto il resto:
l'audit (`AuditScribe`), il limitatore per chiave (`RequestRateLimiter.PassaITetti`, 1.25.4), le soglie di
eliminazione con i loro cancelli.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?** No. Un solo punto decide «questa
richiesta porta una chiave valida»: un filtro sugli endpoint del gruppo `/vsop/api/v1`, non un controllo
ripetuto in ogni `MapGet`.

**3. Ingressi e verifica.** Chi emette: una pagina di amministrazione. Chi usa: un header nella richiesta.
Verifica: test d'integrazione per le quattro risposte (nessuna chiave, chiave sbagliata, chiave revocata,
chiave buona), test del cancello di emissione per livello e reparto, e `curl` da fuori dopo il carico.

**4. Propagazione.** Cambia la carta dell'archivio (§8, «anonimo di proposito») e la guida del bridge
(`docs/guide/aurora-bridge.md`); `mappa-pagine.md` prende la pagina nuova; il commento di `AuroraBridgeOptions`
che dice «l'endpoint è anonimo» va riscritto.

## 4. Il modello

Una tabella nuova, **`ApiClients`** (migrazione **additiva**):

| Colonna | |
|---|---|
| `Id` | |
| `Nome` | chi è il cliente: «Validatore tour IT», «Bridge Aurora — postazione di Mario» |
| `Prefisso` | i primi 8 caratteri della chiave, **in chiaro**: per riconoscerla in elenco e nei log |
| `ImprontaSha256` | l'impronta della chiave intera. **La chiave non si conserva**: si mostra una volta sola, alla creazione |
| `Endpoint` | quali API può chiamare: `archivio`, `bridge` (un elenco, così una chiave del bridge non legge l'archivio) |
| `CreataDaUserId`, `CreataUtc` | |
| `RevocataUtc`, `RevocataDaUserId` | una chiave revocata resta in tabella: è storia |
| `UltimoUsoUtc` | aggiornato al più una volta ogni qualche minuto, non a ogni richiesta |

La chiave: `vipi_` + 32 byte casuali in base64url. Si confronta per impronta, in tempo costante.

## 5. Chi emette

Un cancello **nuovo e più stretto** di Admin, perché Admin oggi comprende anche AOC, AOAC, SOC e SOAC:

- il committente: i `Auth:FounderVids`;
- gli amministratori (livello `Admin`) che sono membri di **IT-HQ** o di **IT-WD**.

⚠️ «Membro di IT-HQ / IT-WD» si può leggere in due modi, e va misurato prima di scegliere (§8, domanda 1):
dal **codice** della posizione (una lista in configurazione, `Auth:ApiKeyIssuerRoles`, come già
`Auth:AdminRoles`) oppure dal **reparto** che IVAO scrive dentro ogni posizione (`userStaffPositions[].
departmentTeam.department`), che oggi al login si butta via tenendo i soli codici. La seconda non ha liste da
tenere aggiornate, ma obbliga a portare il reparto nel cookie e a rileggerlo nella riconvalida di T-002.

Emettere, revocare e ogni chiamata rifiutata finiscono nell'**audit**, con il prefisso e mai la chiave.

## 6. Come si usa

- Header: `Authorization: Bearer vipi_…` (oppure `X-Api-Key`, §8 domanda 3).
- Risposte: **401** senza chiave o con chiave sconosciuta/revocata; **403** con una chiave buona ma
  non abilitata a quell'endpoint.
- Il tetto di richieste diventa **per chiave** (`PassaITetti("archivio", prefisso, …)`): chiude anche il
  problema degli IP condivisi dietro Cloudflare (T-020) per chi usa le API.
- Il tool desktop del bridge prende la chiave dalla propria configurazione.

## 7. Il passaggio, senza fermare nessuno

1. Si consegna tutto con **`Api:RichiediChiave = false`**: l'archivio risponde come oggi, e in più accetta le
   chiavi; la pagina di emissione è attiva.
2. Si emettono le chiavi ai client che esistono (il validatore dei tour per primo).
3. Il committente conferma che i client le mandano (l'`UltimoUsoUtc` lo dice senza chiedere a nessuno).
4. **`Api:RichiediChiave = true`**: da lì le API non sono più anonime. Un cambio di configurazione, non un pacchetto.

Il bridge, che nasce spento, quando si accende chiede la chiave da subito.

## 8. Le domande, e le risposte del committente (13 settembre 2026)

1. **IT-HQ e IT-WD** si riconoscono dal **codice**: `IT-DIR`, `IT-ADIR`, `IT-WM`, `IT-AWM`. Lista in
   `Auth:ApiKeyIssuerRoles` (default quei quattro suffissi, prefisso `Division:Code`), e in più il livello
   effettivo dev'essere `Admin`: così `Auth:AdminStaffCodes`, quando restringe l'admin, restringe anche chi emette.
   Il reparto IVAO resta fuori.
2. **Nessuna scadenza**: una chiave vale finché qualcuno la revoca. La colonna `ScadeUtc` del §4 **non si fa**.
3. **Entrambi gli header**: `Authorization: Bearer vipi_…` e `X-Api-Key: vipi_…`. Se arrivano tutti e due, vale
   `Authorization`.
4. **L'elenco lo vede solo chi può emettere.** Gli altri Admin non vedono né la pagina né la voce di menu.

Scelte di esecuzione che ne discendono:

- Emettere e revocare vanno nell'**audit** (`ApiClient`, `Create` / `Archive`), col prefisso. Le **chiamate
  rifiutate** vanno nel **log** (avviso con prefisso e IP), non nell'audit: sono richieste anonime e un client
  rotto in polling riempirebbe il registro delle azioni dello staff.
- La chiave si cerca nel database per impronta (indice unico) a ogni chiamata: una revoca vale subito, anche
  con più processi, e la query costa quanto la `COUNT` dell'archivio che la segue.
