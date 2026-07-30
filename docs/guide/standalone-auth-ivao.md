# Login IVAO standalone (scenario C)

Guida al **modulo di login IVAO** per far girare la vIPI come **app autonoma** (ADR-0002 D3, scenario C),
quando NON è embedded in un host che fornisce già l'identità. Il modulo è **opt-in e staccabile**: si
accende da config e si spegne senza toccare il core (Application/Domain/RCL).

Riferimenti: `../adr/adr-0002-integrazione-e-autenticazione-portabile.md` (portabilità identità),
`integration.md` (scenari A/B, embedded).

## Idea

La logica vIPI legge sempre l'utente via l'astrazione neutra `ICurrentUserProvider` → `CurrentUser`.
Non conosce cookie/OIDC. Negli scenari embedded (A/B) l'host fornisce il `ClaimsPrincipal`; nello
scenario **C** lo produce questo modulo, e `HostIdentityCurrentUserProvider` lo proietta sul modello
neutro. Punto d'incontro: il `ClaimsPrincipal`. Il core non cambia tra A/B e C.

```
AddOpenIdConnect (api.ivao.aero) → ClaimsPrincipal (id, centerId, userStaffPositions, ...)
        ↓  HostIdentityCurrentUserProvider (claim → modello neutro)
CurrentUser (UserId, Name, Acc, StaffPositions, CanEdit)
        ↓
Application / RCL / Domain (INVARIATI)
```

## Cosa è stato aggiunto

Tutto vive nel composition root dell'host (`Vipi.Host`), isolato:

| File | Ruolo |
|---|---|
| `src/Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs` | `AddVipiStandaloneAuth()` + `MapVipiStandaloneAuth()` + `VipiAuthOptions` |
| `src/Vipi.Host/Auth/IvaoOidcProtocolValidator.cs` | validator OIDC adattato a IVAO (nonce/userinfo lasco) |
| `src/Vipi.Host/Program.cs` | wiring condizionale (`authEnabled`) |
| `src/Vipi.Host/appsettings.json` | sezione `VipiAuth` (default spenta) |
| `src/Vipi.Ui/Shared/SopLayout.razor` | link Login/Logout nell'header |
| `src/Vipi.Ui/Components/Icon.razor` | icone `log-in`/`log-out` |

Dipendenza: pacchetto `Microsoft.AspNetCore.Authentication.OpenIdConnect` (in `Vipi.Host.csproj`).

L'implementazione replica quella del sito ufficiale `Ivao.It` (progetto `Ivao.OpenIdConnect`):
authorization code + PKCE, `GetClaimsFromUserInfoEndpoint`, `ClaimActions.MapAll()`, Authority
`https://api.ivao.aero`.

## Configurazione

`appsettings.json`:
```json
"VipiAuth": {
  "Enabled": false,
  "Authority": "https://api.ivao.aero",
  "Scopes": [ "openid", "email", "tracker" ],
  "CallbackPath": "/signin-oidc",
  "SignedOutCallbackPath": "/signout-callback-oidc"
}
```

Credenziali (mai nel file versionato — user-secrets o variabili d'ambiente):
```powershell
$proj = "src/Vipi.Host/Vipi.Host.csproj"
dotnet user-secrets set "VipiAuth:ClientId" "<client id IVAO>" --project $proj
dotnet user-secrets set "VipiAuth:ClientSecret" "<client secret>" --project $proj   # opzionale
```

- **ClientSecret è opzionale**: se assente, il client è trattato come **pubblico** (solo PKCE). Se l'app
  IVAO è registrata senza secret, ometterlo (mandarne uno errato dà `invalid_client: client secret doesn't match`).
- `CallbackPath` deve combaciare **esatto** col redirect URI registrato sul portale IVAO, es.
  `http://localhost:5034/signin-oidc` in dev, `https://<dominio>/signin-oidc` in prod.

## Accendere / spegnere

- Accendere: `VipiAuth:Enabled=true` + credenziali. In `appsettings.Development.json` è già impostato per i test locali.
- Se `Enabled=true` senza `ClientId` → l'avvio fallisce con messaggio esplicito (guardia voluta).
- Spegnere (embedded scenari A/B): `VipiAuth:Enabled=false`. Rimozione totale: togliere i file `Auth/*.cs`
  + il PackageReference OpenIdConnect. In entrambi i casi il core resta invariato.

Quando l'auth standalone è attiva, `useDevIdentity` è forzato a `false` anche in sviluppo (il login vero
vince sull'utente fittizio `DevCurrentUserProvider`).

## Endpoint

| Rotta | Cosa fa |
|---|---|
| `GET /vsop/auth/login?returnUrl=/vsop` | avvia il flusso IVAO (Challenge); redirect locale anti open-redirect |
| `GET /vsop/auth/logout` | logout: cancella cookie locale + sessione IVAO |

Callback OIDC gestiti dal middleware: `/signin-oidc`, `/signout-callback-oidc`.

## Mappatura claim → modello neutro

I claim IVAO combaciano coi default di `HostIdentityOptions`:

| Modello neutro | Claim IVAO |
|---|---|
| `UserId` | `id` (fallback `sub`) |
| `Acc` | `centerId` |
| `StaffPositions` | `userStaffPositions` (array JSON; si estrae `id`, es. `IT-DIR`) |
| `Name` | `name` (nickname reale, vedi sotto) |

`userStaffPositions[].id` è il codice posizione (`IT-AOA1`, ...): coerente con `IvaoUserClient`/`StaffPosDto`.
Il roster (`StaffRosterService`) registra solo chi ha codici con prefisso di divisione (`IT-`).

## Limite noto: nome utente

IVAO via OIDC **non espone nome/cognome reali** (nessun claim `firstName`/`lastName` nel token). L'unico
nome è `publicNickname`/`nickname`; per gli utenti che non hanno impostato un nickname pubblico vale il
**placeholder** `"User {vid}"`, che il modulo scarta → il display ripiega sul VID.

Conseguenze:
- Uno staffista con **Public Nickname** impostato su `ivao.aero` viene mostrato col nickname.
- Senza nickname, compare come `UserId {vid}` — non è un bug: è il dato che IVAO fornisce.
- I claim ricevuti dipendono anche dai **permessi dell'app OAuth IVAO** usata: un client "tracker" riceve
  claim ridotti. Un'app di login dedicata (come quella del sito) può ricevere anche `firstName`/`lastName`;
  in tal caso reintrodurre la composizione "Nome Cognome" in `OnUserInformationReceived`.

`Ivao.It.Logging` **non c'entra** col nome: è la libreria di logging (Serilog) della divisione.

## Test svolto

Verificato in locale (`http://localhost:5034`): build pulita, `AddOpenIdConnect` con Authority
`api.ivao.aero`, challenge → redirect reale `https://sso.ivao.aero/authorize` (code + PKCE), login
completato come **client pubblico**, utente inserito nel roster con le posizioni staff `IT-`. Il nome
non compare per assenza del dato lato IVAO (vedi limite noto).
