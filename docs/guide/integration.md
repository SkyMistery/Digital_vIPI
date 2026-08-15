# Integrazione del modulo vIPI/vLOA in un sito esistente

Questa guida spiega come agganciare il modulo vIPI a un sito **ASP.NET Core + Blazor Server**
riusando il **login già presente sull'host** (nessun secondo login). Il modulo è una Razor Class
Library (`Vipi.Ui`) + libreria di composizione (`Vipi.Hosting`) che espone una superficie a poche
chiamate. Vedi anche `../adr/adr-0002-integrazione-e-autenticazione-portabile.md` (portabilità identità) e `../adr/adr-0005-superficie-modulo-e-isolamento.md` (superficie/isolamento).

## Prerequisiti dell'host
- ASP.NET Core **8 o 10** + **Blazor Server** (Interactive Server) abilitato.
- Un'autenticazione già configurata (tipicamente **OIDC IVAO**) che popola un `ClaimsPrincipal`
  con almeno: VID, nome, eventuali `userStaffPositions`.
- Accesso a un DB **SQLite** (default), **PostgreSQL** o **MySQL/MariaDB**, scelto con
  `Persistence:Provider`. Il ramo `MySql` è il provider di **produzione** (il server di `atc.it.ivao.aero`
  è **MariaDB 11.4.10**) e usa **Pomelo 8.0.3**: esiste quindi **solo sul TFM `net8.0`**, perché Pomelo non
  ha una build per EF Core 10. Un host net10 può montare il modulo, ma non con questo provider. In ogni caso
  il modulo usa un DB **separato** da quello del sito, con connection string `Vipi` propria. Dettagli in
  ADR-0007 **§D4-ter** (che supera §D4-bis) e in [`../lavori-aperti.md`](../lavori-aperti.md) sezione A.
- Credenziali app IVAO (ClientId/ClientSecret) se serve il roster staff / live ATC.

## Installazione
Referenziare i progetti del modulo (o i futuri pacchetti NuGet):
`Vipi.Hosting`, `Vipi.Ui`, `Vipi.Infrastructure`, `Vipi.Application`, `Vipi.Domain`.

> **Per il sito Ivao.It il wiring è già scritto:** `ivao-it-wiring.patch` in questa cartella si applica
> con `git am` sul loro repository e tocca sette file (csproj, `Program.cs`, `Routes.razor`, `App.razor`,
> `appsettings.json` e `appsettings.production.json`, più un `INTEGRAZIONE-VIPI.md` di istruzioni).
> Verificato compilando davvero il loro `Ivao.It.Website` col modulo agganciato: 0 warning, 0 errori. Il
> submodule non può stare in una patch e va aggiunto con un comando, documentato nel file stesso.
> Le due configurazioni di persistenza sono **entrambe** già pronte: SQLite in `appsettings.json`,
> PostgreSQL in `appsettings.production.json` con connection string da riempire.

### Target framework
I cinque progetti del modulo sono **multi-target `net8.0;net10.0`**: un host net8 consuma il ramo
net8 (stack EF Core 8 / ASP.NET Core 8), un host net10 il ramo net10. Nessuna differenza di API o di
comportamento fra i due — cambia solo la versione dei pacchetti tirati. `Vipi.Host` è **net8.0 solo**
(è l'host che va in produzione, e Pomelo esiste solo lì).

**I progetti di test dall'11 agosto 2026** — prima erano net10 soli tranne uno:

| Progetto | TFM | perché |
|---|---|---|
| `Vipi.Application.Tests`, `Vipi.Domain.Tests`, `Vipi.Hosting.Tests`, `Vipi.Ui.Tests`, `Vipi.Infrastructure.Tests` | `net8.0;net10.0` | provano librerie multi-target: vanno provate su entrambi |
| `Vipi.E2E.Tests` | `net8.0` | avvia `Vipi.Host`, che è net8 solo |
| `Vipi.AuroraBridge.Tests` | `net8.0` | prova `AuroraBridge.Core`, net8 solo (Avalonia) |

Conseguenze pratiche per chi tocca il codice del modulo:
- Sotto net8 il compilatore scende a **C# 12**: niente sintassi C# 13/14 nelle cinque librerie.
- Niente API .NET 9+ nelle librerie. Caso già incontrato: `Convert.ToHexStringLower` (net9+) →
  usare `Convert.ToHexString(...).ToLowerInvariant()`.
- Le migration sono generate con EF Core 10 ma **applicate anche da EF Core 8** (verificato: le 65
  migration si applicano su SQLite sotto EF 8.0.29). Restano SQLite-flavored — vedi `config.md`.
- ⚠️ **Il ramo net8 è coperto dai test, e dall'11 agosto 2026 lo è davvero.** Fino a quel giorno solo
  `Vipi.Infrastructure.Tests` girava su net8: **347 test su ~1400**, mentre logica editoriale, resa e smoke
  di avvio vivevano esclusivamente su net10 — cioè sul runtime che *non* va in produzione. Ora la suite gira
  su entrambi i TFM (**1115 test su net8**, 996 su net10), la CI ha un job `test-net8` che li **esegue**
  invece di limitarsi a compilare, e applica lo schema a una MariaDB 11.4.10 vera su Linux.
- ⚠️ **Gli avvisi sono errori** (`Directory.Build.props`, `TreatWarningsAsErrors`). Vale per tutti i
  progetti, non solo per la CI: un avviso nuovo ferma la build sulla macchina di chi scrive.
- ⚠️ **Le dipendenze sono bloccate** (`packages.lock.json` committati, restore in «locked mode» in CI). Se
  il restore della CI si ferma, il rimedio è `dotnet restore --force-evaluate` in locale e un commit dei
  lock aggiornati: serve a rendere visibile un aggiornamento invece di subirlo.

## Wiring (Program.cs dell'host)
```csharp
using Vipi.Hosting;

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Registra l'intero modulo (Application, EF, polling IVAO, opzioni, identità host).
// useDevIdentity:false => l'identità è letta dal ClaimsPrincipal dell'host.
builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: builder.Environment.IsDevelopment());

var app = builder.Build();
app.MigrateVipiDatabase();          // crea/migra il DB del modulo — CRITICO: un guasto qui deve fermare l'avvio
app.RunVipiStartupMaintenance();    // riconciliazioni/proiezione/release: idempotenti, isolate, non fatali

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
