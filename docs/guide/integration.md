# Integrazione del modulo vIPI/vLOA in un sito esistente

Questa guida spiega come agganciare il modulo vIPI a un sito **ASP.NET Core + Blazor Server**
riusando il **login già presente sull'host** (nessun secondo login). Il modulo è una Razor Class
Library (`Vipi.Ui`) + libreria di composizione (`Vipi.Hosting`) che espone una superficie a poche
chiamate. Vedi anche `../adr/adr-0002-integrazione-e-autenticazione-portabile.md` (portabilità identità) e `../adr/adr-0005-superficie-modulo-e-isolamento.md` (superficie/isolamento).

## Prerequisiti dell'host
- ASP.NET Core **8 o 10** + **Blazor Server** (Interactive Server) abilitato.
- Un'autenticazione già configurata (tipicamente **OIDC IVAO**) che popola un `ClaimsPrincipal`
  con almeno: VID, nome, eventuali `userStaffPositions`.
- Accesso a un DB **SQLite** (default) o **PostgreSQL** (`Persistence:Provider`). MySQL non è
  supportato: un host su MySQL usa un DB separato per il modulo (connection string `Vipi` propria).
- Credenziali app IVAO (ClientId/ClientSecret) se serve il roster staff / live ATC.

## Installazione
Referenziare i progetti del modulo (o i futuri pacchetti NuGet):
`Vipi.Hosting`, `Vipi.Ui`, `Vipi.Infrastructure`, `Vipi.Application`, `Vipi.Domain`.

### Target framework
I cinque progetti del modulo sono **multi-target `net8.0;net10.0`**: un host net8 consuma il ramo
net8 (stack EF Core 8 / ASP.NET Core 8), un host net10 il ramo net10. Nessuna differenza di API o di
comportamento fra i due — cambia solo la versione dei pacchetti tirati. `Vipi.Host` (l'app autonoma
di sviluppo) e i progetti di test restano **net10.0 soli**.

Conseguenze pratiche per chi tocca il codice del modulo:
- Sotto net8 il compilatore scende a **C# 12**: niente sintassi C# 13/14 nelle cinque librerie.
- Niente API .NET 9+ nelle librerie. Caso già incontrato: `Convert.ToHexStringLower` (net9+) →
  usare `Convert.ToHexString(...).ToLowerInvariant()`.
- Le migration sono generate con EF Core 10 ma **applicate anche da EF Core 8** (verificato: le 65
  migration si applicano su SQLite sotto EF 8.0.29). Restano SQLite-flavored — vedi `config.md`.
- Il build net8 non è coperto dalla suite (i test girano su net10): va compilato a parte con
  `dotnet build src/Vipi.Hosting/Vipi.Hosting.csproj -f net8.0`.

## Wiring (Program.cs dell'host)
```csharp
using Vipi.Hosting;

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Registra l'intero modulo (Application, EF, polling IVAO, opzioni, identità host).
// useDevIdentity:false => l'identità è letta dal ClaimsPrincipal dell'host.
builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: builder.Environment.IsDevelopment());

var app = builder.Build();
app.MigrateVipiDatabase();          // crea/migra il DB del modulo

app.UseAuthentication();            // l'auth dell'host PRIMA del modulo
app.UseAuthorization();
app.UseAntiforgery();
app.UseVipiModule();                // middleware del modulo (roster login)

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(VipiModuleExtensions.UiAssembly);   // monta le pagine vIPI

app.MapVipiModule();               // endpoint del modulo: SSE /vsop/live/atc, /vsop/health
```
Il modulo vive sotto il prefisso di rotta **`/vsop`**. Per montarlo su un path diverso usare un
**reverse proxy** che rimappi (es. `/vipi` → `/vsop`); il prefisso è fisso nelle direttive `@page`.

### Sorgente dati esterna (decoupling)
L'app non dipende da IVAO direttamente: i dati esterni passano per **interfacce neutre**
(`IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IOnlineAtcProvider`). L'adapter attivo
si sceglie con **`DataSource:Provider`** (oggi `"Ivao"` → `AddVipiIvao`). Per agganciare un'altra rete o un
DB interno si registra un nuovo adapter e si cambia quel valore, senza toccare Application/UI. Vedi
`config.md` §1b.

## Mappa dei claim (sezione `HostIdentity`)
`HostIdentityCurrentUserProvider` proietta il `ClaimsPrincipal` dell'host sul modello neutro
`CurrentUser`. I default seguono i claim OIDC IVAO; adattarli se l'host usa nomi diversi:
```json
"HostIdentity": {
  "UserIdClaim": "id",
  "NameClaims": [ "name", "given_name", "preferred_username" ],
  "AccClaim": "centerId",
  "StaffPositionsClaim": "userStaffPositions"
}
```
`StaffPositionsClaim` supporta sia **claim ripetuti** sia **un claim con array JSON**
(`["IT-DIR","IT-WM"]`) sia array di oggetti con campo `id`/`connectAs`.

## Permessi
- **Admin** = staff position che matcha i ruoli divisione (`^{Division.Code}-{ruolo}$`, es. `IT-DIR`).
  Se l'host usa codici diversi, elencarli come pattern in `Auth:AdminStaffCodes`.
- **Grant per-ACC** = concessi dagli admin in `/vsop/admin/permessi` (lista dagli staffisti che si sono
  loggati almeno una volta). Audit in `/vsop/admin/audit`.

## Configurazione divisione e segreti
```json
"Division": { "Code": "IT", "Name": "Italy", "IcaoPrefixes": [ "LI" ] },
"ConnectionStrings": { "Vipi": "Data Source=vipi.db" }
```
Segreti IVAO **mai** in appsettings: env var `Ivao__ClientId` / `Ivao__ClientSecret` (o user-secrets in dev).

## Convivenza con la chrome dell'host (CSS/topbar)
- Tutti gli stili del modulo sono confinati sotto **`.vipi-root`**: non toccano `body`/reset dell'host.
- Se l'host ha già una propria header/navigazione, disattivare la topbar del modulo per evitare la
  doppia barra:
  ```json
  "Vipi": { "RenderTopbar": false }
  ```
- Le funzioni JS del modulo sono sotto il prefisso `vipi*` (namespace di fatto, collision-safe).

### CSS/JS del modulo nel layout dell'host
Gli asset della RCL si servono da `_content/Vipi.Ui/…`. Su un host **net10** conviene passarli per
`MapStaticAssets` + `@Assets[...]` (impronta per contenuto, precompressi a build-time) come fa
`Vipi.Host/Components/App.razor`. Su un host **net8** `MapStaticAssets` non esiste: si referenziano
con path nudi sotto `UseStaticFiles()`, rinunciando al cache-busting per impronta.
```html
<link rel="stylesheet" href="_content/Vipi.Ui/vipi-fonts.css" />
<link rel="stylesheet" href="_content/Vipi.Ui/vipi-theme.css" />
<link rel="stylesheet" href="_content/Vipi.Ui/vipi-aor3d.css" />
<link rel="stylesheet" href="_content/Vipi.Ui/vipi-print.css" />
<script src="_content/Vipi.Ui/vipi-ui.js"></script>
<script src="_content/Vipi.Ui/vipi-screens.js"></script>
<script src="_content/Vipi.Ui/vipi-live.js"></script>
<script src="_content/Vipi.Ui/vipi-aor.js"></script>
<script src="_content/Vipi.Ui/vipi-aor3d.js"
        data-three-src="_content/Vipi.Ui/vendor/three.min.js"></script>
<script src="_content/Vipi.Ui/vipi-editor.js"></script>
<script src="_content/Vipi.Ui/vipi-tour.js"></script>
<script src="_content/Vipi.Ui/vipi-media.js"></script>
```
`three.js` **non** va nel `<head>`: `vipi-aor3d.js` lo carica su richiesta leggendo `data-three-src`.
L'elenco autorevole resta `Vipi.Host/Components/App.razor` — se lì si aggiunge un file, va aggiunto
anche qui.

## Scenari di deploy (ADR-0002)
| Scenario | Host | Identità | `useDevIdentity` |
|---|---|---|---|
| A | Sito Ivao.It esistente (OIDC IVAO) | `HostIdentityCurrentUserProvider` (claim host) | false |
| B | Nuovo sito stesso stack | come A | false |
| C | App autonoma dedicata | login OIDC proprio → adapter custom passato a `AddVipiModule` | false |
| Dev | Host di sviluppo | utente fittizio `DevCurrentUserProvider` | true |

## Verifica rapida
- `GET /vsop/health` → `Healthy` (DB ok; `Degraded` se la cache ATC non è fresca).
- Login sull'host, poi `/vsop`: in alto compare l'utente; se admin, i tasti **Editor**/**Permessi**.
- `/vsop/admin/permessi`: gli staffisti IT loggati compaiono nel dropdown.

## Troubleshooting
- **Dropdown staff vuoto**: gli staffisti compaiono dopo il primo login; la verifica giornaliera rimuove
  chi non è più staff IT. Credenziali IVAO necessarie per la verifica.
- **`/vsop` non autenticato**: l'host deve applicare l'autenticazione prima delle rotte del modulo;
  l'autorizzazione di editing è comunque sempre verificata server-side.
- **Doppia barra in alto**: impostare `Vipi:RenderTopbar=false`.
- **Stili che “sbavano”**: verificare che il contenuto del modulo sia sotto `.vipi-root` (lo è di default
  via `SopLayout`); non spostare regole fuori da quel contenitore.
