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
| `Ivao:ClientId` | string | `""` | Credenziale app-to-app. **Vuota ⇒ nessun Bearer** (il tracker è pubblico). → §5 secrets. |
| `Ivao:ClientSecret` | string | `""` | Segreto app-to-app. → §5 secrets. |

Il token serve **solo** per l'elenco membri divisione (auto-elenco CH in `/sop/admin/permessi`).
Il polling ATC online funziona senza credenziali.

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
| `ConnectionStrings:Vipi` | `Data Source=vipi.db` | Connessione SQLite. Il DB è creato/migrato e riseedato (Roma) all'avvio. |
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
  "Ivao": {
    "BaseUrl": "https://api.ivao.aero",
    "AtcSummaryPath": "/v2/tracker/now/atc/summary",
    "TokenEndpoint": "https://api.ivao.aero/v2/oauth/token",
    "DivisionMembersPathFormat": "/v2/divisions/{0}/members",
    "Scopes": "tracker",
    "PollSeconds": 60,
    "ClientId": "",
    "ClientSecret": ""
  }
}
```
`Auth` e `ConnectionStrings` sono assenti di proposito: si usano i default (admin derivati da `Division`, SQLite locale).
