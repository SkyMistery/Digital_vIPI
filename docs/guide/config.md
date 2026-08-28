# Configurazione — vIPI / vLOA

Riferimento di **tutte** le impostazioni runtime dell'host. Sorgenti, in ordine di precedenza crescente
(l'ultima vince): `appsettings.json` → `appsettings.{Environment}.json` → **user-secrets** (solo Development)
→ **variabili d'ambiente** → argomenti CLI.

Convenzione env var: il separatore di sezione `:` diventa `__` (doppio underscore).
Es. `Ivao:ClientId` → `Ivao__ClientId`; elementi di lista per indice: `Division__IcaoPrefixes__0`.

---

## 1. `Division` — identità della divisione IVAO

Centralizza tutto ciò che cambia passando divisione (es. IT → DE). Mappata su `DivisionOptions`
(`src/Vipi.Application/DivisionOptions.cs`).

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Division:Code` | string | `IT` | Codice divisione IVAO. Prefisso degli **staff code** (`{Code}-DIR`…) e id nell'**API membri** (`/v2/divisions/{Code}/members`). |
| `Division:Name` | string | `Italy` | Nome leggibile (display). |
| `Division:IcaoPrefixes` | string[] | `["LI"]` | Prefissi ICAO dei callsign ATC della divisione. Filtra il polling online. IT→`["LI"]`, DE→`["ED","ET"]`. |
| `Division:AdminRolePatterns` | string[] | `["[A-Z0-9]+"]` | Suffissi (regex, **senza** prefisso divisione) che valgono come admin. Codice finale = `^{Code}-{ruolo}$`. ⚠️ Il default è un **jolly**: tutto lo staff di divisione è admin (decisione del 22 agosto 2026). Per restringere serve `Auth:AdminStaffCodes`, non questa chiave — da qui si può solo allargare. |
| `Division:AdminAccRolePatterns` | string[] | `["CH","ACH"]` | Suffissi (regex) di ruoli admin **ACC-scoped** (prefisso ICAO dell'ACC, non della divisione, es. `LIRR-CH`, `LIMM-ACH`). Codice finale = `^{prefissoIcao}[A-Z0-9]+-{ruolo}$` per ogni `IcaoPrefixes`. |

**Cambiare divisione (IT → DE):**
```json
"Division": { "Code": "DE", "Name": "Germany", "IcaoPrefixes": [ "ED", "ET" ] }
```
`Code` sposta i codici admin (`DE-DIR`…) e l'id API; `IcaoPrefixes` filtra gli ATC online.
⚠️ Il **contenuto seed** (Roma/LIRR) è dato, non config: una nuova divisione va riseedata a parte.

> ### ⚠️ Da queste liste si può solo ALLARGARE, mai restringere
> Il binder della configurazione **aggiunge** alle liste di default invece di sostituirle: elencare qui tre
> ruoli non toglie gli altri, li somma (è anche il motivo per cui `IcaoPrefixes: ["LI"]` produceva «LI» due
> volte). Per **restringere** davvero l'insieme degli admin si usa **`Auth:AdminStaffCodes`**, che sostituisce
> l'intero elenco con pattern completi. Su ciò che è il permesso più alto del prodotto, la differenza conta.
>
> ### Come si verifica che i pattern siano quelli giusti
> IVAO **non** espone l'elenco degli staffisti di divisione (`/v2/divisions/{id}/members` → 404 col token
> app), quindi la verifica è empirica: il roster si popola dai login, e la scheda **«Chi può editare»** in
> `/services/vsop/admin/diagnostics` mette i pattern in vigore accanto ai codici staff **realmente osservati**. Se
> nessuno degli staffisti conosciuti risulta admin scatta un rilievo grave (e `/vsop/health` va a Degraded);
> a roster vuoto invece tace, perché su un'installazione nuova nessuno ha ancora fatto login.
>
> Codici veri visti al 9 agosto 2026: `IT-AOC`, `IT-SOC`, `IT-T01`, `IT-FOC`, `IT-ADIR`, `IT-FOAC`,
> `IT-AOA1`, `IT-T03` — quindi **`IT-SOC`, `IT-T01`, `IT-FOC` e `IT-FOAC` non sono coperti** dai default, e
> nessun codice chief `{ACC}-CH` è ancora comparso.

---

## 1b. `DataSource` — selezione della sorgente dati esterna

Mappata su `DataSourceOptions` (`src/Vipi.Infrastructure/DataSourceOptions.cs`). Disaccoppia l'app dalla rete:
le porte dati (`IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IOnlineAtcProvider`) sono
**neutre**; qui si sceglie quale adapter le implementa.

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `DataSource:Provider` | string | `Ivao` | Adapter attivo. Oggi solo `Ivao` (registra `AddVipiIvao`). Un valore sconosciuto fa **fallire l'avvio** con messaggio chiaro. |

In futuro un nuovo provider (altro network, **DB interno**, dataset statico) si aggiunge come implementazione in
`Infrastructure` + un branch in `VipiModuleExtensions.AddVipiModule`, **senza toccare Application/UI**. La sezione
`Ivao` (§2) resta la config dell'adapter IVAO concreto.

---

## 1c. `Persistence` — selezione del provider di database

Mappata su `PersistenceProviderResolver` (`src/Vipi.Infrastructure/Persistence/PersistenceProvider.cs`), scelta in
`AddVipiInfrastructure`. La connection string resta `ConnectionStrings:Vipi` (§4).

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Persistence:Provider` | string | `Sqlite` | Uno fra `Sqlite`, `Postgres`, `MySql` (case-insensitive). Valore sconosciuto ⇒ errore con l'elenco dei validi. |

I tre provider sono tutti operativi, ma servono a tre scopi diversi e non sono intercambiabili:

| Valore | A cosa serve | Come nasce lo schema |
|---|---|---|
| `Sqlite` | **sviluppo** (default): file + WAL/`busy_timeout` via `SqliteTuningInterceptor` | migrazioni versionate del repo |
| `Postgres` | **deploy di prova** Render+Neon, e sorgente del travaso verso la produzione | `EnsureCreated` + `PostgresSchemaReconciler` (solo aggiunte di colonna) |
| `MySql` | **produzione** su `atc.it.ivao.aero` — il server è **MariaDB 11.4.10**, provider **Pomelo 8.0.3** | set di migrazioni dedicato (`Vipi.Infrastructure.MySqlMigrations`) — *non* `EnsureCreated` |

> ℹ️ Il valore si chiama `MySql` anche puntando a MariaDB: è il nome del **dialetto**, non del prodotto.
> Perché l'host è `net8.0`: Pomelo non esiste per EF Core 10, e Pomelo è l'unico provider che porta la
> collation fino alla DDL.
>
> ℹ️ **Solo su `MySql`** il modello riceve due aggiustamenti che sugli altri due non si applicano:
> le **lunghezze** delle colonne stringa indicizzate (`MySqlStringLengths` — InnoDB non indicizza `longtext`)
> e la **collation** `utf8mb4_uca1400_as_cs` su ogni colonna stringa (`MySqlCollation` — il default del
> server ignora maiuscole e accenti, e il modello ha indici unici su callsign, ICAO e hash). Non sono
> opzionali e non sono configurabili: senza, il `CREATE TABLE` fallisce o i confronti cambiano semantica in
> silenzio. ⚠️ Il nome MySQL `utf8mb4_0900_as_cs` su MariaDB **non esiste**: la DDL non sarebbe eseguibile.
> Razionale in `../adr/adr-0007-produzione-persistenza-e-scala.md` (**§D4-ter**, che supera §D4-bis).
>
> ⚠️ **`max_allowed_packet` del server ≥ 4 MB**: le immagini sono `longblob` e viaggiano in un pacchetto
> solo; l'applicazione taglia a 3 MB per immagine (`MediaOptions.MaxUploadBytes`).

---

## 2. `Ivao` — API IVAO e polling (F3)

Mappata su `IvaoOptions` (`src/Vipi.Infrastructure/Ivao/IvaoOptions.cs`). Vedi `../adr/adr-0001-scelte-architetturali-fondanti.md` (D6) e `../adr/adr-0003-trasporto-live-sse.md` (trasporto SSE).

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Ivao:BaseUrl` | string | `https://api.ivao.aero` | Base delle API IVAO v2. |
| `Ivao:AtcSummaryPath` | string | `/v2/tracker/now/atc/summary` | Endpoint riepilogo ATC online (**pubblico**, nessun token). |
| `Ivao:TokenEndpoint` | string | `https://api.ivao.aero/v2/oauth/token` | Endpoint token OpenID (client_credentials). |
| `Ivao:DivisionMembersPathFormat` | string | `/v2/divisions/{0}/members` | Template path membri divisione; `{0}` = `Division:Code`. Richiede token. |
| `Ivao:Scopes` | string | `tracker configuration` | Scope richiesti per il token client_credentials (`configuration` serve per aeroporti/ACC/subcenter). |
| `Ivao:PollSeconds` | int | `60` | Intervallo di polling. Una sola chiamata/minuto a IVAO indipendentemente dagli utenti (RNF-1/RNF-4). **Minimo effettivo 15 s** (clamp nel hosted service). |
| `Ivao:StaffVerifyHours` | int | `24` | Ogni quante ore ri-verificare il roster staffisti via `/v2/users/{vid}` (disattiva chi non è più staff IT). |
| `Ivao:AirportsPath` | string | `/v2/airports` | Anagrafica aeroporti (paginato). Richiede scope `configuration`. |
| `Ivao:AirportsCountryId` | string | `IT` | Paese (countryId) per aeroporti **e** ACC/center. |
| `Ivao:AirportsCacheHours` | int | `12` | TTL cache di processo dell'anagrafica aeroporti. |
| `Ivao:CentersPath` | string | `/v2/centers` | Anagrafica ACC/center (paginato). Scope `configuration`. |
| `Ivao:SubcentersPathFormat` | string | `/v2/centers/{0}/subcenters` | Template settori (subcenter) di un ACC; `{0}` = ICAO ACC. |
| `Ivao:SubcenterDetailPathFormat` | string | `/v2/subcenters/{0}` | Template dettaglio subcenter (freq + regionMapPolygon); `{0}` = composePosition. |
| `Ivao:AtcPositionDetailPathFormat` | string | `/v2/ATCPositions/{0}` | Template dettaglio postazione ATC d'aeroporto (freq/shape/limiti); `{0}` = composePosition (es. `LIRN_TWR`). |
| `Ivao:AccImportHours` | int | `24` | Ogni quante ore re-importare automaticamente ACC + settori ACC (job giornaliero). |
| `Ivao:AirportSectorImportHours` | int | `24` | Ogni quante ore re-importare automaticamente i settori ATC degli aeroporti (`AirportSector`, job giornaliero). |
| `Ivao:AirportDirectoryImportHours` | int | `24` | Ogni quante ore riassegnare alla loro ACC gli aeroporti nuovi dell'anagrafica (`AirportDirectory`, job giornaliero), col loro catalogo settori. ⚠️ **L'unico giro che crea entità**; additivo, non rimuove né riassegna: uno scalo tolto dalla sorgente resta in archivio e si toglie a mano. Gira **subito dopo** gli ACC (25 s), perché i giri successivi iterano gli aeroporti che questo ha creato. |
| `Ivao:AirportDataImportHours` | int | `24` | Ogni quante ore rileggere **TA e piste** di tutti gli aeroporti (`AirportData`, job giornaliero). Un giro costa **1** chiamata per la TA (anagrafica, già in cache per `AirportsCacheHours`) più **una per aeroporto** per le piste. Rispetta la policy di `/services/vsop/admin/sources`: con «Transition Altitude» **e** «Piste» escluse non interroga nemmeno la sorgente. |
| `Ivao:ClientId` | string | `""` | Credenziale app-to-app. **Vuota ⇒ nessun Bearer** (il tracker è pubblico). → §5 secrets. |
| `Ivao:ClientSecret` | string | `""` | Segreto app-to-app. → §5 secrets. |

Il polling ATC online funziona **senza** credenziali (endpoint pubblico). Il token serve per il **roster
staffisti** (verifica via `/v2/users/{vid}`, che col token app funziona).

> ⚠️ L'endpoint massivo `DivisionMembersPathFormat` (`/v2/divisions/{Code}/members`) **non è utilizzabile**
> col token app (404/500); il roster staffisti è quindi costruito dai **login** + verifica per-VID. Vedi
> il design in `MEMORY`/`staff-roster-design`. Le chiavi restano per compatibilità ma non alimentano la UI.

> **Gating import periodici (round 34):** i job 24h (ACC, settori aeroporto, aree speciali, SID) sono *gated* via
> `ImportState` persistente: all'avvio **saltano il fetch** se l'ultima esecuzione riuscita è ancora entro il
> periodo (`*ImportHours`) — così un riavvio non richiama le sorgenti. Stamp solo su successo; retry 1h su errore.
> I trigger **manuali** (pagine admin/editor) bypassano sempre il gate; il polling live 60s non è gated.

---

## 2c. `Sectorfile` — SID dal sectorfile Aurora (GitHub)

Mappata su `SectorfileOptions` (`src/Vipi.Infrastructure/Sectorfile/SectorfileOptions.cs`). Repo **pubblico raw**
(nessuna auth). Import SID per-aeroporto + completion fix/VOR. Registrato **sempre** (ortogonale a `DataSource:Provider`),
attivo solo se `RawBaseUrl` è valorizzata. Round 34.

| Chiave | Tipo | Default | Descrizione |
|---|---|---|---|
| `Sectorfile:RawBaseUrl` | string | `""` | Base raw dei file (deve finire con `/`). Prod: `https://raw.githubusercontent.com/ivao-italy/it-aurora-sector/master/SectorFiles/Include/IT/`. **Vuota ⇒ import SID disattivato.** |
| `Sectorfile:FixPath` | string | `NAVAIDS/itfix.fix` | ⚠️ **Ripiego.** I file di punti li elenca `ITALY.isc` (**otto**, non tre: ci sono anche `ESTERNI.fix`, `MIL.fix`, `APT.fix`, `VFR_NASCOSTI.fix`, `secsi.fix`). Questi tre percorsi si usano **solo** se l'indice non risponde o non cita file di punti. |
| `Sectorfile:VorPath` | string | `NAVAIDS/itvor.vor` | Path del file VOR (fallback completion quando il prefisso non è un fix). |
| `Sectorfile:ImportHours` | int | `24` | Ogni quante ore re-importare le SID (job gated). |

Le SID importate sono **pubbliche dal ciclo AIRAC successivo** al prelievo (o se forzate a mano nell'editor);
i prefissi troncati irregolari si risolvono via tabella **alias** editabile. Vedi `MEMORY`/`round34-sid-import-github`.

---

## 2b. `Weather` — METAR/TAF reali (NOAA)

Mappata su `WeatherOptions` (`src/Vipi.Infrastructure/Weather/WeatherOptions.cs`). Sorgente pubblica
**senza chiave** (NOAA aviationweather.gov). Cache in-memory per ICAO a TTL.

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Weather:BaseUrl` | string | `https://aviationweather.gov` | Base API meteo. Endpoint usati: `/api/data/metar` e `/api/data/taf` (`?ids={ICAO}&format=json`). |
| `Weather:TtlMinutes` | int | `10` | Durata cache per ICAO (il METAR aggiorna ~oraria). |

Reso nella vIPI aeroporto (`AeroportoPage`). In errore/servizio irraggiungibile mostra l'empty-state
"METAR/TAF non disponibile".

---

## 2d. `Media` — immagini dei blocchi editoriali

Mappata su `MediaOptions` (`src/Vipi.Application/Media/MediaOptions.cs`). Governa il caricamento delle immagini
nei blocchi (docs/feature/2026-07-31-immagini-nei-blocchi.md). I byte finiscono nella tabella `MediaAssets` dello
stesso database, indirizzati per **sha256**, e si servono da `/vsop/media/{sha}`.

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Media:MaxUploadBytes` | int | `3145728` (3 MB) | Dimensione massima del singolo file. **È il numero da cambiare** per alzare o abbassare il limite: lo leggono il testo d'aiuto sotto l'area di caricamento, il controllo lato server e il messaggio di rifiuto. |
| `Media:MaxImagePixels` | int | `12000` | Lato massimo in pixel accettato: guardia contro le immagini-bomba (dimensioni enormi, file piccolo). |
| `Media:MaxBytesPerDocument` | int | `26214400` (25 MB) | **Quota per documento**: spazio massimo che le immagini di un singolo documento possono occupare. `0` = nessun limite. Contato sulle righe, quindi la stessa foto usata due volte pesa una volta sola. |
| `Media:ClientDownscaleLongestSidePx` | int | `2000` | Lato lungo a cui il **browser** rimpicciolisce la foto prima di spedirla (`0` = nessun ridimensionamento: allora una foto da telefono sopra il limite viene rifiutata). |
| `Media:JpegQuality` | double | `0.85` | Qualità della ricodifica fatta dal browser (0..1). |

Formati accettati: PNG, JPEG, WebP, GIF — riconosciuti dai **byte**, non dall'estensione né dal `Content-Type`
dichiarato. L'SVG è escluso di proposito (è markup: potrebbe eseguire script servito dal nostro dominio).

Su Render si cambiano i limiti dalla dashboard con le variabili `Media__MaxUploadBytes` e
`Media__MaxBytesPerDocument` (riavvio del servizio, nessun rebuild).

**Ciclo di vita di un'immagine.** Togliere il blocco che la mostrava la cancella **subito**, ma solo se non la
cita piu' nessuno: un altro blocco, un'altra versione, una sezione extra o una **release pubblicata** la tengono
in vita (una vIPI dell'AIRAC scorso deve continuare a mostrarla). Lo stesso controllo governa la pulizia manuale
in `/services/vsop/admin/diagnostics`, che serve per le foto rimaste indietro da prima o liberate dalla retention.

---

## 2e. `Translation` — documenti bilingue (Azure primario, DeepL di riserva)

Mappata su `TranslationOptions` (`src/Vipi.Application/Translation/TranslationOptions.cs`).
Carta: [feature/2026-08-27-documenti-bilingue.md](../feature/2026-08-27-documenti-bilingue.md).

```jsonc
"Translation": {
  "Enabled": false,                 // spento di default: senza motore il sito mostra la lingua sorgente
  "Targets": [ "it", "en" ],        // lingue offerte in lettura, oltre a quella sorgente del documento
  "Order":   [ "azure", "deepl" ],  // ORDINE DI PREFERENZA: il primo che risponde vince
  "Azure": {
    "ApiKey": "",                   // ⚠️ MAI qui: user-secrets in dev, variabile d'ambiente in produzione
    "Region": "westeurope",         // ⚠️ obbligatoria su risorsa regionale, vedi sotto
    "BaseUrl": "https://api.cognitive.microsofttranslator.com",
    "MaxTextsPerCall": 50,
    "MaxCaratteriTotali": 0         // 0 = nessun tetto
  },
  "DeepL": {
    "ApiKey": "",
    "GlossaryId": "",               // glossario di fraseologia della divisione, se esiste
    "BaseUrl": "",                  // vuoto = dedotto dalla chiave (:fx = piano gratuito)
    "EnglishVariant": "EN-GB",      // «EN» secco e' deprecato come bersaglio
    "MaxTextsPerCall": 50,
    "MaxCaratteriTotali": 0
  }
}
```

### La catena, e perche' c'e'

`Order` elenca i motori **in ordine di preferenza**. Il primo che risponde vince; se non risponde — quota
finita, chiave rifiutata, servizio giu', non configurato — **il successivo subentra da solo** e il servizio
non si ferma. Nel rapporto del giro e nella riga di memoria resta scritto **chi ha tradotto davvero**.

⚠️ L'ordine lo detta questa chiave, **non** l'ordine di registrazione nel contenitore: un motore aggiunto in
fondo al file di DI non deve diventare il primario per sbaglio.

⚠️ Cambiare motore **non ripaga niente**: la memoria e' indicizzata sul testo, non su chi l'ha tradotto.

### `MaxCaratteriTotali` e' PER MOTORE, e i due budget sono di natura diversa

| Motore | Natura della franchigia | Che cosa protegge il tetto |
|---|---|---|
| Azure | mensile ricorrente | che un giro impazzito non bruci il mese |
| DeepL | **una tantum, non si rinnova** | una **riserva**: finita, e' finita per sempre |

Superato il tetto, quel motore si **salta** e la catena passa al successivo: il giro non si ferma.
Il controllo avviene **prima** di spendere.

⚠️ **Un segmento che è solo un identificatore non parte, e non conta.** Le celle fatte di un callsign, di un
punto o di uno stand vengono messe da parte dal protettore, e ciò che resta non ha più niente da tradurre:
si scrivono in memoria col motore «nessuno». Misurato sul primo SOP militare, **28 segmenti su 218** —
caratteri che nessun tetto deve contare, perché nessuno li ha spesi.

### ⚠️ Le due trappole di Azure

1. **La regione.** Su una risorsa regionale o multi-servizio, senza `Ocp-Apim-Subscription-Region` Azure
   risponde **401** — che somiglia a una chiave sbagliata e manda a rigenerare una chiave che andava
   benissimo. Compila `Region`.
2. **Il 403 vuol dire due cose.** Chiave rifiutata *e* quota gratuita esaurita rispondono entrambe 403, e le
   azioni sono opposte. Le distingue solo il codice nel corpo (`403000` = non autorizzato, `403001` = quota
   finita), e il codice **si legge**.

### ⚠️ La trappola di DeepL

`BaseUrl` vuoto = dedotto dalla chiave. Le chiavi del piano gratuito finiscono in `:fx` e vogliono
`api-free.deepl.com`; le altre `api.deepl.com`. Puntare al server sbagliato risponde **403**.

### ⚠️ VID e nomi utente non escono mai

Decisione del committente del 27 agosto 2026: **i dati pubblici si possono mandare a un servizio esterno,
VID e nomi utente mai.** Non e' una nota di policy, e' un cancello nel codice (`TextProtector`), con un test
che passa **tutto il corpus editoriale reale** nel protettore e pretende che nessun payload in uscita
contenga un VID o un nome del roster. Vale per **entrambi** i motori: il protettore sta a monte della porta,
e nessun adapter vede mai l'originale.

Corollario operativo: **non attivare log di diagnostica che registrino il payload inviato.** Un log del
genere riapre da solo il buco che il protettore chiude.

### Costo, misurato

| Corpus | Caratteri |
|---|---|
| `vipi.db` del 27 agosto 2026, 18 documenti | **23.344** |
| I 15 SOP militari — **solo prosa** (il 42% del grezzo) | **74.401** |
| **Semina iniziale completa** | **~98.000** |

Dopo la semina si paga solo il **delta**, perche' la memoria e' indicizzata sull'hash: cambia una frase,
si ritraduce quella. Il glossario di fraseologia resta la difesa che conta sulla **qualita'** — «riporta
sottovento» reso in modo plausibile ma non standard e' peggio di non tradotto, perche' nessuno se ne accorge.

---

## 3. `Auth` — override codici admin (opzionale)

Mappata su `AuthOptions` (`src/Vipi.Application/Auth/AuthOptions.cs`).

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Auth:AdminStaffCodes` | string[] | `[]` (vuoto) | Pattern regex **completi** dei codici staff admin. Se valorizzato **sostituisce** i default derivati da `Division`. Se vuoto, admin = `^{Division:Code}-{AdminRolePatterns}$`. |

Usare solo se servono codici admin non derivabili dal codice divisione. Esempio:
```json
"Auth": { "AdminStaffCodes": [ "^IT-DIR$", "^IT-WM$", "^XX-SPECIAL$" ] }
```

Regole di autorizzazione (`EditAuthorizationService`):
- **Admin** (match dei pattern sopra) → edita tutto + gestisce i grant.
- **Grant per-ACC** (`EditGrant`, VID→ACC, da `/sop/admin/permessi`) → edita le ACC concesse.
- Altri → sola lettura (la sezione editor in `AccLanding` non compare).
- Verifica **sempre server-side**; la UI nasconde solo gli entry-point.

---

## 4. Infrastruttura host (standard ASP.NET Core)

| Chiave | Default | Significato |
|---|---|---|
| `ConnectionStrings:Vipi` | `Data Source=vipi.db` | Connessione SQLite. Il DB è creato/migrato all'avvio. **Nessun seed**: si parte da DB vuoto e i dati reali si inseriscono dall'app (ACC/posizioni/topologia + documenti). Cancellare il file per ripartire da zero. |
| `Logging:LogLevel:Default` | `Information` | Livello log. Il polling logga `Poll IVAO: {N} ATC divisione online` a ogni ciclo. |
| `AllowedHosts` | `*` | Host consentiti. |

> **HTTPS:** `UseHttpsRedirection` è attivo solo fuori da `Development` (l'host di sviluppo ascolta su http;
> in prod configurare la porta https / il binding). Lo stream SSE `/sop/live/atc` disabilita il buffering
> per la consegna immediata anche dietro reverse-proxy.

---

## 5. Segreti (credenziali IVAO)

**Mai** in `appsettings.json` versionato. In sviluppo → **user-secrets** (file fuori dal repo):
```bash
cd src/Vipi.Host
dotnet user-secrets init                     # genera UserSecretsId nel .csproj
dotnet user-secrets set "Ivao:ClientId" "…"
dotnet user-secrets set "Ivao:ClientSecret" "…"
```
File reale: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`.

In produzione → **variabili d'ambiente**:
```
Ivao__ClientId=…
Ivao__ClientSecret=…
```

---

## 6. Esempio `appsettings.json` completo
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Division": { "Code": "IT", "Name": "Italy", "IcaoPrefixes": [ "LI" ] },
  "DataSource": { "Provider": "Ivao" },
  "Ivao": {
    "BaseUrl": "https://api.ivao.aero",
    "AtcSummaryPath": "/v2/tracker/now/atc/summary",
    "TokenEndpoint": "https://api.ivao.aero/v2/oauth/token",
    "DivisionMembersPathFormat": "/v2/divisions/{0}/members",
    "Scopes": "tracker",
    "PollSeconds": 60,
    "ClientId": "",
    "ClientSecret": ""
  },
  "Weather": { "BaseUrl": "https://aviationweather.gov", "TtlMinutes": 10 }
}
```
`Auth` e `ConnectionStrings` sono assenti di proposito: si usano i default (admin derivati da `Division`, SQLite locale).

---

## 7. `HostIdentity` — mappa dei claim dell'host (integrazione)

Mappata su `HostIdentityOptions` (`src/Vipi.Hosting/HostIdentityOptions.cs`). Usata da
`HostIdentityCurrentUserProvider` per leggere il login del sito ospitante (scenari A/B). Vedi
**`integration.md`** e `../adr/adr-0005-superficie-modulo-e-isolamento.md`.

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `HostIdentity:UserIdClaim` | string | `id` | Claim col VID utente (valore mappato su `CurrentUser.UserId`; default `id`). |
| `HostIdentity:NameClaims` | string[] | `["name","given_name","preferred_username"]` | Claim del nome (primo valorizzato). |
| `HostIdentity:AccClaim` | string | `centerId` | Claim ACC/centro (opzionale). |
| `HostIdentity:StaffPositionsClaim` | string | `userStaffPositions` | Claim posizioni staff (multipli o array JSON). |

In sviluppo (`useDevIdentity:true`) si usa l'utente fittizio e questa sezione è ignorata.

## 8. `Vipi` — chrome del modulo

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Vipi:RenderTopbar` | bool | `true` | Se mostrare la topbar propria del modulo. Impostare `false` quando l'host ha già la sua header (evita la doppia barra). |

## 9. Endpoint operativi
- `GET /sop/health` — health del modulo (`Healthy`/`Degraded` se la cache ATC non è fresca/`Unhealthy` se il DB è giù).
- `GET /sop/admin/audit` — viewer audit (admin): pubblicazioni e modifiche permessi.
- `GET /sop/admin/sorgenti` — **policy di import** (admin): decide quali categorie (TA, Piste, Settori) arrivano dalla sorgente (sola lettura) o restano manuali. Vedi §10.

## 10. Policy di import (sorgenti dati) — non da appsettings

La provenienza dei dati che la sorgente può fornire è governata da una **policy globale persistita nel DB**
(entità `ImportPolicy`, riga singola), **non** da appsettings, ed è editabile dagli admin in
**`/sop/admin/sorgenti`**. Semantica **opt-out**: per default ogni categoria è **importata e in sola lettura**
(sovrascritta ai re-import, non modificabile negli editor); escludendo una categoria la si rende **manuale**
(l'import non la tocca più, gli editor la lasciano editabile).

| Categoria | Campi di sorgente |
|---|---|
| `TransitionAltitude` | `Airport.TransitionAltitudeFt` |
| `Runways` | `AirportRunway.Ident/LengthM/Bearing` |
| `Sectors` | settori d'aeroporto (callsign/tipo/frequenza) |

> La categoria **ATIS** è stata **rimossa** (round 16): l'ATIS non è più un campo dell'aeroporto ma un `AirportSector` come gli altri; la sua frequenza segue la categoria `Sectors`.

I campi **editoriali** (regole pista, SID, livelli TL, link frequenze, gerarchia settori) non sono categorie:
restano sempre dell'utente.
