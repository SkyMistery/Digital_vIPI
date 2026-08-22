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

L'implementazione nasce da quella del sito ufficiale `Ivao.It` (progetto `Ivao.OpenIdConnect`, gemello
del campione `aspnetcore7/` in `ivaoaero/OAuth-samples`): authorization code + PKCE,
`GetClaimsFromUserInfoEndpoint`, Authority `https://api.ivao.aero`. Se ne discosta su tre punti, tutti
motivati sotto: `SaveTokens=false`, claim mappati a mano invece di `ClaimActions.MapAll()`, nonce validato.

## Configurazione

`appsettings.json`:
```json
"VipiAuth": {
  "Enabled": false,
  "Authority": "https://api.ivao.aero",
  "Scopes": [ "openid", "profile", "email" ],
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
| `GET /services/vsop/auth/login?returnUrl=/services/vsop` | avvia il flusso IVAO (Challenge); redirect locale anti open-redirect |
| `GET /services/vsop/auth/logout` | logout: cancella cookie locale + sessione IVAO |

Callback OIDC gestiti dal middleware: `/signin-oidc`, `/signout-callback-oidc`.

## Mappatura claim → modello neutro

I claim IVAO combaciano coi default di `HostIdentityOptions`:

| Modello neutro | Claim IVAO |
|---|---|
| `UserId` | `id` (fallback `sub`) |
| `Acc` | `centerId` |
| `StaffPositions` | `userStaffPositions` (array JSON dei soli codici, es. `["IT-AOA1","IT-T03"]`) |
| `Name` | `name` (nome e cognome, vedi sotto) |

I codici posizione (`IT-AOA1`, ...) li estrae dal profilo IVAO `StaffPositionCodesJson`, che legge
`userStaffPositions[].id`: coerente con `IvaoUserClient`/`StaffPosDto`.
Il roster (`StaffRosterService`) registra solo chi ha codici con prefisso di divisione (`IT-`).

## Il nome utente dipende dallo scope `profile`

> ⚠️ Fino al 22 agosto 2026 questa sezione diceva che «IVAO non espone nome/cognome reali». **Era falso**,
> e per mesi il sito ha mostrato `UserId 704798` al posto del nome per una riga di configurazione.

`/v2/users/me` restituisce `firstName` e `lastName` — ma **solo se si è chiesto lo scope `profile`**.
Senza, la stessa chiamata risponde ugualmente, con quei due campi assenti: nessun errore, nessun avviso.
Da cui l'equivoco. Tutti i campioni ufficiali IVAO (`ivaoaero/OAuth-samples`: PHP, Laravel, React) chiedono
`profile`; la vIPI no.

Misurato il 22-ago-2026 sul flusso reale (scope `openid profile email`), la userinfo contiene:

```
firstName "Carmine"   lastName "Granato"   publicNickname "Carmine (704798)"
id 704798   centerId "LIRR"   divisionId "IT"   isStaff true
userStaffPositions [ { id "IT-AOA1", connectAs …, staffPosition { … } }, … ]
given_name / family_name  (le stesse due stringhe, nei nomi OIDC standard)
```

Ordine con cui si compone il nome (`VipiStandaloneAuthExtensions.ComposeDisplayName`):

1. `firstName` + `lastName` (o `given_name`/`family_name`) → «Mario Rossi». Se ne manca uno, si mostra l'altro;
2. `publicNickname` / `nickname`, se non è il placeholder `"User {vid}"`;
3. niente → `HostIdentityCurrentUserProvider` ripiega su `UserId {vid}`.

Attenzione: `publicNickname` per chi non ne ha scelto uno vale `"Nome (VID)"` (es. `Carmine (704798)`),
che **non** è il placeholder `"User {vid}"` e viene accettato come ripiego — è comunque meglio del VID nudo.

Se un giorno il nome sparisse di nuovo, la prima cosa da guardare è lo **scope concesso all'app OAuth**:
un client registrato per il solo `tracker` non riceve `profile`, e si torna al nickname.

Gli scope chiesti sono **tre**: `openid`, `profile`, `email`. Il `tracker` è stato tolto il 22-ago-2026:
chiedeva il permesso di leggere la sessione live **dell'utente**, e nessuno lo usava — il pallino «in
frequenza» (`LiveBadge`) lo alimenta il polling del server col token dell'**applicazione** (sezione `Ivao`),
e il token utente non lo conserviamo (`SaveTokens = false`). Era un permesso chiesto a ogni staffista nella
schermata di consenso IVAO in cambio di niente.

`Ivao.It.Logging` **non c'entra** col nome: è la libreria di logging (Serilog) della divisione.

## Cosa finisce nel cookie (e cosa no)

Il cookie di autenticazione viaggia a **ogni** richiesta e a ogni handshake SignalR, quindi contiene solo
i claim che qualcuno legge. Non c'è più `ClaimActions.MapAll()`: portava dentro l'intero profilo IVAO
(`hours[]`, `rating{}`, `groups`, `userStaffDetails` con email e note interne, e un `userStaffPositions`
di ~1,5 kB per due incarichi) e per giunta **azzerava** le `DeleteClaim` che il framework registra da sé
(`nonce`, `aud`, `iss`, `iat`, `exp`, `at_hash`…), che ora sono di nuovo in servizio.

Si mappano: `id`, `sub`, `centerId`, `firstName`, `lastName`, `publicNickname`, e `userStaffPositions`
**ridotto ai soli codici** (`["IT-AOA1","IT-T03"]`). Si cancellano dall'id_token `ivao.aero/permissions`,
`profile`, `jti`, `type`.

⚠️ Toccando quell'elenco si rischia in silenzio: se `userStaffPositions` non si forma, lo staffista entra
come semplice lettore senza un errore da nessuna parte. Il segnale da controllare dopo ogni modifica è che
in testata restino i tasti **Editor** e **Permessi**.

## nonce validato; state, il flag da non toccare

Il **nonce** era spento (`IvaoOidcProtocolValidator`) perché si riteneva che IVAO non lo mandasse.
Misurato il 22-ago-2026: è dentro l'id_token. Ora si valida — è la difesa contro il replay dell'id_token.

**`RequireState` deve restare `false`**, e non è una resa a IVAO. ASP.NET Core non popola mai
`OpenIdConnectProtocolValidationContext.State`: alzando il flag il validator non trova il campo e lancia
`IDX21329: State is null` — con qualunque IdP, sempre. Provato sul login vero, e il login si rompe al
ritorno da IVAO. Lo `state` è comunque controllato, ma dall'handler: ci viaggia l'id del cookie di
correlazione, confrontato in `ValidateCorrelationId`. Alzare quel flag non aggiunge una difesa: toglie il login.

Resta spenta anche la validazione della **userinfo**: `/v2/users/me` non è una userinfo OIDC.

Via di fuga sul nonce, se un cambio lato IVAO rompe il login in produzione:

```
VipiAuth__RelaxProtocolValidation=true
```

Rimette la validazione lasca senza ricompilare né ridistribuire. È una toppa per mentre si indaga, non
uno stato normale.

## Test svolto

Verificato in locale (`http://localhost:5034`): build pulita, `AddOpenIdConnect` con Authority
`api.ivao.aero`, challenge → redirect reale `https://sso.ivao.aero/authorize` (code + PKCE), login
completato come **client pubblico**, utente inserito nel roster con le posizioni staff `IT-`. Il nome
non compare per assenza del dato lato IVAO (vedi limite noto).
