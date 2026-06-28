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
| `Division:AdminRolePatterns` | string[] | `["DIR","ADIR","WM","AWM","AOC","AOAC","AOA\\d+"]` | Suffissi (regex, **senza** prefisso divisione) che valgono come admin. Codice finale = `^{Code}-{ruolo}$`. |

**Cambiare divisione (IT → DE):**
```json
"Division": { "Code": "DE", "Name": "Germany", "IcaoPrefixes": [ "ED", "ET" ] }
```
`Code` sposta i codici admin (`DE-DIR`…) e l'id API; `IcaoPrefixes` filtra gli ATC online.
⚠️ Il **contenuto seed** (Roma/LIRR) è dato, non config: una nuova divisione va riseedata a parte.

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

## 2. `Ivao` — API IVAO e polling (F3)

Mappata su `IvaoOptions` (`src/Vipi.Infrastructure/Ivao/IvaoOptions.cs`). Vedi `docs/adr/ADR-0001` (D6) e
`ADR-0003` (trasporto SSE).

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Ivao:BaseUrl` | string | `https://api.ivao.aero` | Base delle API IVAO v2. |
| `Ivao:AtcSummaryPath` | string | `/v2/tracker/now/atc/summary` | Endpoint riepilogo ATC online (**pubblico**, nessun token). |
| `Ivao:TokenEndpoint` | string | `https://api.ivao.aero/v2/oauth/token` | Endpoint token OpenID (client_credentials). |
| `Ivao:DivisionMembersPathFormat` | string | `/v2/divisions/{0}/members` | Template path membri divisione; `{0}` = `Division:Code`. Richiede token. |
| `Ivao:Scopes` | string | `tracker` | Scope richiesti per il token client_credentials. |
| `Ivao:PollSeconds` | int | `60` | Intervallo di polling. Una sola chiamata/minuto a IVAO indipendentemente dagli utenti (RNF-1/RNF-4). **Minimo effettivo 15 s** (clamp nel hosted service). |
| `Ivao:StaffVerifyHours` | int | `24` | Ogni quante ore ri-verificare il roster staffisti via `/v2/users/{vid}` (disattiva chi non è più staff IT). |
| `Ivao:ClientId` | string | `""` | Credenziale app-to-app. **Vuota ⇒ nessun Bearer** (il tracker è pubblico). → §5 secrets. |
| `Ivao:ClientSecret` | string | `""` | Segreto app-to-app. → §5 secrets. |

Il polling ATC online funziona **senza** credenziali (endpoint pubblico). Il token serve per il **roster
staffisti** (verifica via `/v2/users/{vid}`, che col token app funziona).

> ⚠️ L'endpoint massivo `DivisionMembersPathFormat` (`/v2/divisions/{Code}/members`) **non è utilizzabile**
> col token app (404/500); il roster staffisti è quindi costruito dai **login** + verifica per-VID. Vedi
> il design in `MEMORY`/`staff-roster-design`. Le chiavi restano per compatibilità ma non alimentano la UI.

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
- **Grant per-FIR** (`EditGrant`, VID→FIR, da `/sop/admin/permessi`) → edita le FIR concesse.
- Altri → sola lettura (la sezione editor in `AccLanding` non compare).
- Verifica **sempre server-side**; la UI nasconde solo gli entry-point.

---

## 4. Infrastruttura host (standard ASP.NET Core)

| Chiave | Default | Significato |
|---|---|---|
| `ConnectionStrings:Vipi` | `Data Source=vipi.db` | Connessione SQLite. Il DB è creato/migrato all'avvio. **Nessun seed**: si parte da DB vuoto e i dati reali si inseriscono dall'app (FIR/posizioni/topologia + documenti). Cancellare il file per ripartire da zero. |
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
**`docs/INTEGRATION.md`** e `ADR-0005`.

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `HostIdentity:UserIdClaim` | string | `id` | Claim col VID utente (valore mappato su `CurrentUser.UserId`; default `id`). |
| `HostIdentity:NameClaims` | string[] | `["name","given_name","preferred_username"]` | Claim del nome (primo valorizzato). |
| `HostIdentity:FirClaim` | string | `centerId` | Claim FIR/centro (opzionale). |
| `HostIdentity:StaffPositionsClaim` | string | `userStaffPositions` | Claim posizioni staff (multipli o array JSON). |

In sviluppo (`useDevIdentity:true`) si usa l'utente fittizio e questa sezione è ignorata.

## 8. `Vipi` — chrome del modulo

| Chiave | Tipo | Default | Significato |
|---|---|---|---|
| `Vipi:RenderTopbar` | bool | `true` | Se mostrare la topbar propria del modulo. Impostare `false` quando l'host ha già la sua header (evita la doppia barra). |

## 9. Endpoint operativi
- `GET /sop/health` — health del modulo (`Healthy`/`Degraded` se la cache ATC non è fresca/`Unhealthy` se il DB è giù).
- `GET /sop/admin/audit` — viewer audit (admin): pubblicazioni e modifiche permessi.
- `GET /sop/admin/sorgenti` — **policy di import** (admin): decide quali categorie (TA, ATIS, Piste, Settori) arrivano dalla sorgente (sola lettura) o restano manuali. Vedi §10.

## 10. Policy di import (sorgenti dati) — non da appsettings

La provenienza dei dati che la sorgente può fornire è governata da una **policy globale persistita nel DB**
(entità `ImportPolicy`, riga singola), **non** da appsettings, ed è editabile dagli admin in
**`/sop/admin/sorgenti`**. Semantica **opt-out**: per default ogni categoria è **importata e in sola lettura**
(sovrascritta ai re-import, non modificabile negli editor); escludendo una categoria la si rende **manuale**
(l'import non la tocca più, gli editor la lasciano editabile).

| Categoria | Campi di sorgente |
|---|---|
| `TransitionAltitude` | `Airport.TransitionAltitudeFt` |
| `Atis` | `Airport.AtisFrequency` |
| `Runways` | `AirportRunway.Ident/LengthM/Bearing` |
| `Sectors` | settori d'aeroporto (callsign/tipo/frequenza) |

I campi **editoriali** (regole pista, SID, livelli TL, link frequenze, gerarchia settori) non sono categorie:
restano sempre dell'utente.
