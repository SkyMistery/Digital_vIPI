# Integrazione del modulo vIPI/vLOA in un sito esistente

Questa guida spiega come agganciare il modulo vIPI a un sito **ASP.NET Core + Blazor Server**
riusando il **login già presente sull'host** (nessun secondo login). Il modulo è una Razor Class
Library (`Vipi.Ui`) + libreria di composizione (`Vipi.Hosting`) che espone una superficie a poche
chiamate. Vedi anche `docs/adr/ADR-0002-*` (portabilità identità) e `ADR-0005` (superficie/isolamento).

## Prerequisiti dell'host
- ASP.NET Core 8 + **Blazor Server** (Interactive Server) abilitato.
- Un'autenticazione già configurata (tipicamente **OIDC IVAO**) che popola un `ClaimsPrincipal`
  con almeno: VID, nome, eventuali `userStaffPositions`.
- Accesso a un DB (SQLite di default; PostgreSQL/SQL Server opzionali — vedi sotto).
- Credenziali app IVAO (ClientId/ClientSecret) se serve il roster staff / live ATC.

## Installazione
Referenziare i progetti del modulo (o i futuri pacchetti NuGet):
`Vipi.Hosting`, `Vipi.Ui`, `Vipi.Infrastructure`, `Vipi.Application`, `Vipi.Domain`.

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

app.MapVipiModule();               // endpoint del modulo: SSE /sop/live/atc, /sop/health
```
Il modulo vive sotto il prefisso di rotta **`/sop`**. Per montarlo su un path diverso usare un
**reverse proxy** che rimappi (es. `/vipi` → `/sop`); il prefisso è fisso nelle direttive `@page`.

### Sorgente dati esterna (decoupling)
L'app non dipende da IVAO direttamente: i dati esterni passano per **interfacce neutre**
(`IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IOnlineAtcProvider`). L'adapter attivo
si sceglie con **`DataSource:Provider`** (oggi `"Ivao"` → `AddVipiIvao`). Per agganciare un'altra rete o un
DB interno si registra un nuovo adapter e si cambia quel valore, senza toccare Application/UI. Vedi
`docs/CONFIG.md` §1b.

## Mappa dei claim (sezione `HostIdentity`)
`HostIdentityCurrentUserProvider` proietta il `ClaimsPrincipal` dell'host sul modello neutro
`CurrentUser`. I default seguono i claim OIDC IVAO; adattarli se l'host usa nomi diversi:
```json
"HostIdentity": {
  "UserIdClaim": "id",
  "NameClaims": [ "name", "given_name", "preferred_username" ],
  "FirClaim": "centerId",
  "StaffPositionsClaim": "userStaffPositions"
}
```
`StaffPositionsClaim` supporta sia **claim ripetuti** sia **un claim con array JSON**
(`["IT-DIR","IT-WM"]`) sia array di oggetti con campo `id`/`connectAs`.

## Permessi
- **Admin** = staff position che matcha i ruoli divisione (`^{Division.Code}-{ruolo}$`, es. `IT-DIR`).
  Se l'host usa codici diversi, elencarli come pattern in `Auth:AdminStaffCodes`.
- **Grant per-FIR** = concessi dagli admin in `/sop/admin/permessi` (lista dagli staffisti che si sono
  loggati almeno una volta). Audit in `/sop/admin/audit`.

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

## Scenari di deploy (ADR-0002)
| Scenario | Host | Identità | `useDevIdentity` |
|---|---|---|---|
| A | Sito Ivao.It esistente (OIDC IVAO) | `HostIdentityCurrentUserProvider` (claim host) | false |
| B | Nuovo sito stesso stack | come A | false |
| C | App autonoma dedicata | login OIDC proprio → adapter custom passato a `AddVipiModule` | false |
| Dev | Host di sviluppo | utente fittizio `DevCurrentUserProvider` | true |

## Verifica rapida
- `GET /sop/health` → `Healthy` (DB ok; `Degraded` se la cache ATC non è fresca).
- Login sull'host, poi `/sop`: in alto compare l'utente; se admin, i tasti **Editor**/**Permessi**.
- `/sop/admin/permessi`: gli staffisti IT loggati compaiono nel dropdown.

## Troubleshooting
- **Dropdown staff vuoto**: gli staffisti compaiono dopo il primo login; la verifica giornaliera rimuove
  chi non è più staff IT. Credenziali IVAO necessarie per la verifica.
- **`/sop` non autenticato**: l'host deve applicare l'autenticazione prima delle rotte del modulo;
  l'autorizzazione di editing è comunque sempre verificata server-side.
- **Doppia barra in alto**: impostare `Vipi:RenderTopbar=false`.
- **Stili che “sbavano”**: verificare che il contenuto del modulo sia sotto `.vipi-root` (lo è di default
  via `SopLayout`); non spostare regole fuori da quel contenitore.
