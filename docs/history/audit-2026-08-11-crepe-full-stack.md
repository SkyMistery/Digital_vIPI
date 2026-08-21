# Audit full-stack — 11 agosto 2026

**Ramo esaminato:** `refactor/13-tre-documenti` · **Stato:** ✅ **eseguito lo stesso giorno.** L'esito per
voce è in fondo, in «[Esito dell'esecuzione](#esito-dellesecuzione--11-agosto-2026)»; qui sotto resta l'analisi
com'era stata scritta, **prima** di toccare il codice, perché la parte che vale rileggere è il ragionamento —
e in due casi (M13, D4/D5) la misura ha poi ribaltato la conclusione.

Suite dopo l'esecuzione: **2111 test verdi** — 1115 su net8, 996 su net10. Build pulita, zero avvisi.

Audit indipendente di tutta l'applicazione: sicurezza, concorrenza, persistenza, scala, front-end, build e
processo. Non riparte da `lavori-aperti.md` — quello dice cosa si sa già; questo cerca quello che non si sa.
Le voci già note e già decise (Neon, C3, staff code, consegna del `.sql`) **non** sono ripetute qui.

**Metodo:** build reale con `-warnaserror`, suite eseguita, lettura del codice sui percorsi che scrivono e su
quelli raggiungibili da anonimo. Dove una cosa è **verificata** lo dico; dove è **dedotta** lo dico lo stesso.

**Esito in una riga:** il codice è in ottimo stato — nessun TODO, nessun `catch` cieco, authz server-side
sistematica, `AsNoTracking` ovunque. Le crepe non sono nella scrittura: sono **al bordo** (superficie anonima,
identità, deploy) e **nel processo di build**.

---

## Riepilogo per gravità

| # | Crepa | Gravità | Verificata? |
|---|---|---|---|
| B1 | CI rossa su questo ramo: 14 chiavi duplicate nel `.resx` → 28 errori con `-warnaserror` | **Bloccante** | ✅ riprodotta |
| B2 | Su net8 (= produzione) gira **un solo** progetto di test su sette | **Bloccante** | ✅ riprodotta |
| A1 | `/vsop/search` anonimo carica in memoria l'intero corpus a ogni ricerca | Alta | ✅ letta |
| A2 | `/vsop/health` anonimo esegue scansioni complete, ed è sempre Degraded | Alta | ✅ letta |
| A3 | `SaveTokens=true` + `MapAll()`: token IVAO nel cookie, mai letti da nessuno | Alta | ✅ verificata |
| A4 | Pubblicazione non transazionale (3 `SaveChanges`), con `IUnitOfWork` già pronto e inutilizzato | Alta | ✅ letta |
| A5 | Leaflet caricato da `unpkg.com` a runtime | Alta | ✅ letta |
| M1 | Open redirect: `SafeReturn` non filtra `/\evil.com` | Media | dedotta |
| M2 | OIDC: `ValidateUserInfoResponse` svuotata + nonce spento | Media | ✅ letta |
| M3 | Identità nel circuito via `IHttpContextAccessor`, e `IsAdmin` riparsa i claim a ogni render | Media | ✅ letta |
| M4 | `X-Forwarded-For` e `Host` spoofabili (proxy fidati vuoti + `AllowedHosts: "*"`) | Media | ✅ letta |
| M5 | Nessun header di sicurezza (CSP, frame, referrer) né in app né in nginx | Media | ✅ letta |
| M6 | Il lock `admin:structure` lo può prendere qualunque utente loggato | Media | ✅ letta |
| M7 | `SidImporter.ImportAsync` è l'unica scrittura senza `EnsureCanEdit*` | Media | ✅ letta |
| M8 | `avvio-errore.txt` (stack trace intero) scritto accanto all'eseguibile | Media | ✅ letta |
| M9 | SSE `/vsop/live/atc`: anonimo, senza tetto di connessioni, semaforo mai disposto | Media | ✅ letta |
| M10 | Nessun limite sui circuiti Blazor su host condiviso | Media | ✅ letta |
| M11 | `HeartbeatLoop` non cattura le eccezioni generiche: il lock scade in silenzio | Media | ✅ letta |
| M12 | `_ = DismissSavedAsync(...)`: `StateHasChanged` dopo dispose, ×4 | Media | ✅ letta |
| M13 | `ExtractPoints` scende solo nel **primo** anello: i multi-poligono perdono pezzi in silenzio | Media | ✅ letta |
| M14 | Versioni NuGet con wildcard e nessun lock file: pacchetto non riproducibile | Media | ✅ verificata |
| M15 | `<html lang="it">` fisso mentre la UI è it/en | Media | ✅ letta |
| M16 | Testo italiano hardcoded fuori dalle risorse (es. `AorBlock.razor`) | Media | ✅ letta |
| M17 | `UseAntiforgery()` prima di `UseAuthentication`/`UseAuthorization` | Media | ✅ letta |
| D1…D11 | Debito strutturale (duplicazione, file-mostro, a11y, retention, build props) | Bassa | varie |

---

## Bloccanti

### B1 — La CI è **rossa su questo ramo**, e nessuno se n'è accorto

`SharedResource.resx` e `SharedResource.en.resx` contengono **14 chiavi duplicate** ciascuno
(`Country_LI`, `Country_LF`, `Country_LS`, `Country_LO`, `Country_LD`, `Country_LJ`, `Country_LG`,
`Country_LM`, `Country_LA`, `Country_LQ`, `Country_LY`, `Country_LW`, `Country_DT`, `Country_DA`):
il blocco alle righe 79–93 e quello alle righe 513–526 dicono le stesse cose due volte.

Il compilatore di risorse emette `MSB3568 — Duplicate resource name … is not allowed, ignored`. Il job CI
`build-net8` compila `Vipi.Hosting` con `-warnaserror`, e `Vipi.Hosting` tira dentro `Vipi.Ui`.

**Riprodotto:**

```
dotnet build src/Vipi.Ui/Vipi.Ui.csproj -f net8.0 -c Release -warnaserror --no-incremental
  → 0 Warning(s), 28 Error(s)
```

Su `main` i duplicati sono **zero**; su `refactor/13-tre-documenti` sono 14. Sono entrati con
`e2f8639 fix(13/S14)` e `a4a56dc fix(13/S18+S19)`. Il ramo non è mai stato spinto dopo quei commit, quindi la
CI non l'ha mai visto — e la suite locale (`dotnet test`, senza `-warnaserror`, su net10) resta verde: **1391
test verdi e build di produzione rotta sono compatibili**, ed è esattamente il caso in cui ci troviamo.

Non è cosmetico anche a warning: dei due `Country_LQ` uno dice «Bosnia ed Erzegovina» e l'altro «Bosnia».
Vince il primo, e quale sia «il primo» dipende dall'ordine di lettura del file — cioè da niente di dichiarato.

**Come la sistemo.** Tolgo il blocco duplicato (righe 513–526 in `it`, 510–523 in `en`) tenendo la versione
lunga di `Country_LQ`. Poi metto una guardia perché non torni: un test in `Vipi.Ui.Tests` che apre i due
`.resx`, estrae i `name=` e pretende `Distinct().Count() == Count()` su entrambi — costa dieci righe e non ha
bisogno della CI per parlare. In più (vedi D9) un `Directory.Build.props` con `TreatWarningsAsErrors`, così il
segnale arriva sulla macchina di chi scrive invece che su un runner che questo ramo non ha mai visto.

⚠️ **Questa voce va chiusa prima della decisione di merge di B5.** Il ramo è descritto come «pronto, in attesa
di un ok»: nello stato attuale, quell'ok manderebbe rossa la CI di `main`.

### B2 — Su net8, cioè in produzione, gira **un solo** progetto di test su sette

I TFM dei progetti di test:

| Progetto | TFM |
|---|---|
| `Vipi.Infrastructure.Tests` | `net8.0;net10.0` |
| `Vipi.Application.Tests` | net10.0 |
| `Vipi.Ui.Tests` | net10.0 |
| `Vipi.Hosting.Tests` | net10.0 |
| `Vipi.Domain.Tests` | net10.0 |
| `Vipi.E2E.Tests` | net10.0 |
| `Vipi.AuroraBridge.Tests` | net10.0 |

**Riprodotto:** `dotnet test Vipi.slnx -c Release --framework net8.0` → sei progetti falliscono con
`NETSDK1005 … doesn't have a target for 'net8.0'`, e passa solo `Vipi.Infrastructure.Tests` con **347 test**.

Il job CI `build-net8` **compila** `Vipi.Hosting` per net8 e basta: non esegue un solo test su quel TFM.

La memoria di progetto dice «il ramo net8 è coperto dai test (non più a mano)». È vero **per Infrastructure**.
Per Application, Ui, Hosting, Domain, E2E e il bridge — circa mille test, cioè tutta la logica editoriale, tutta
la resa e tutti gli smoke di avvio — il TFM che va su `atc.it.ivao.aero` **non è coperto da niente**.

È un rischio reale e non teorico: le differenze net8/net10 hanno già morso due volte in questo progetto (il
catch-all `MapRazorComponents` che dà 405 su net10 e 404 su net8; `KnownIPNetworks` che su net8 si chiama
`KnownNetworks`). Sono precisamente differenze che un test coglie e una compilazione no.

**Come la sistemo.** Porto a `net8.0;net10.0` almeno `Vipi.Application.Tests`, `Vipi.Hosting.Tests` e
`Vipi.E2E.Tests` — sono i tre che toccano rispettivamente la logica, l'avvio e la superficie HTTP, cioè dove
le differenze di runtime si vedono. `Vipi.Ui.Tests` dipende da bunit e va provato: se bunit 1.40 non regge
net8 lo dichiaro qui invece di scoprirlo dopo. Poi il job `build-net8` diventa `dotnet test … -f net8.0`
invece di `dotnet build`, con il runtime 8 già installato (c'è).

Costo dichiarato: la suite gira due volte, e ogni test che usa API .NET 9+ va scoperto adesso. È il prezzo che
ADR-0007 §D4-ter aveva già messo in conto per il multi-target — questa voce lo salda per i test.

---

## Alta

### A1 — `/vsop/search` è anonimo e materializza l'intero corpus a ogni battuta

`EfSearchRepository.SearchAsync`:

1. `_db.Documents.Where(CurrentVersionId != null)` con due `Include`/`ThenInclude` → **tutti** i documenti;
2. `_db.DocumentSections.Where(versionIds.Contains(...))` → **tutte** le sezioni delle versioni correnti;
3. `_db.ContentBlocks.Where(versionIds.Contains(...))` → **tutti** i blocchi, `Body` e `BodyJson` compresi.

Il `Contains` case-insensitive è poi fatto **in memoria**. `BodyJson` è dove vivono i poligoni AoR, le tabelle
di configurazione e gli envelope dei blocchi immagine: sono i campi più grossi del database.

Due conseguenze distinte:

- **Costo.** Ogni ricerca è una fotografia completa del contenuto pubblicato, trasferita dal database e
  allocata sull'heap. Su MariaDB condivisa con il sito che ci ospita, il costo lo paga anche lui.
- **Forma.** `foreach (var m in metas) foreach (var s in sections.Where(...))` e la gemella sui blocchi sono
  O(documenti × sezioni) e O(documenti × blocchi): la `Where` riscorre l'intera lista a ogni documento.

La pagina è pubblica e non ha limitatore. Non serve un avversario: bastano dieci persone che cercano.

Il commento dice «scala pilota», e allora era vero. Adesso il pilota va in produzione.

**Come la sistemo.** In due passi, il primo dei quali è quasi gratis:

1. **Togliere la quadraticità** raggruppando una volta sola: `sections.ToLookup(s => s.DocumentVersionId)` e
   `blocks.ToLookup(b => b.DocumentVersionId)`. Cinque righe, stesso risultato, nessun cambio di semantica.
2. **Spostare il filtro nel database** con `EF.Functions.Like(b.Body, $"%{q}%")` sulle tre colonne, così
   tornano solo le righe che contengono il termine. Il gate di visibilità (documento non nascosto + release
   effettiva + sezioni nascoste) resta dov'è: si applica **dopo**, su un insieme già piccolo.
   ⚠️ Va verificato sul campo che la collation `utf8mb4_uca1400_as_cs` non renda il `LIKE` sensibile alle
   maiuscole — A6 ha già misurato che **non** lo fa per l'uguaglianza, ma il `LIKE` va provato a parte prima
   di fidarsi.

Il minimo indispensabile prima della consegna è il passo 1 più un tetto di lunghezza sul termine.

### A2 — `/vsop/health` è anonimo, costa scansioni complete, ed è **sempre** Degraded

`MapHealthChecks("/vsop/health")` non ha `RequireAuthorization()`. Dietro c'è `VipiHealthCheck`, che il suo
stesso commento descrive così: «Costa: il report di consistenza fa scansioni complete».

Chiunque, senza credenziali, può far girare quelle scansioni quante volte vuole.

E c'è un secondo guasto, indipendente: A2/E2 documentano che su quei dati il report **trova sempre**
incongruenze soft-ref (gerarchia `ParentCallsign` dangling), quindi `/vsop/health` risponde **Degraded**
stabilmente. Un semaforo permanentemente giallo non è un semaforo: quando qualcosa si romperà davvero, il
colore non cambierà.

**Come la sistemo.**

- `/vsop/health/ready` resta com'è: è la sonda dell'orchestratore, costa due query, deve restare aperta.
- `/vsop/health` prende `.RequireAuthorization()` — chi lo apre è un umano, e un umano ce l'ha il login — **e**
  una cache di 60 s del risultato del report, così anche l'apertura ripetuta non ripaga la scansione.
- Le incongruenze note vanno **portate a zero** (le 33 torri senza padre di E2), oppure dichiarate:
  se sono uno stato accettato, il report le classifichi `Info` e non `Degraded`. La scelta è del committente,
  ma finché è aperta il segnale non vale niente e conviene dirlo nel LEGGIMI di consegna.

### A3 — Il cookie di autenticazione trasporta i token IVAO, che nessuno legge

In `VipiStandaloneAuthExtensions`:

```csharp
oidc.GetClaimsFromUserInfoEndpoint = true;
oidc.SaveTokens = true;          // ← e nessuno li rilegge mai
oidc.ClaimActions.MapAll();      // ← tutto il profilo IVAO diventa claim
```

`SaveTokens = true` scrive `id_token`, `access_token` e `refresh_token` dentro il cookie di autenticazione.
Ho cercato il consumatore: **non esiste**. `GetTokenAsync` non compare da nessuna parte nella soluzione; le
chiamate all'API IVAO usano un percorso completamente diverso (`IvaoTokenProvider` → client credentials
dell'applicazione, `IvaoHttp.cs:36`).

`ClaimActions.MapAll()` porta a claim **ogni** campo della userinfo IVAO — e anch'esso finisce nel cookie.

Il risultato: un cookie che cresce oltre i 4 KB e viene spezzato in `vipi.authC1`, `vipi.authC2`… inviato a
ogni richiesta e a ogni handshake SignalR, e che contiene credenziali IVAO di un utente vero, riemesse a ogni
login, per una funzione che non c'è.

**Come la sistemo.** `SaveTokens = false`. Poi, al posto di `MapAll()`, mappo i cinque claim che il modulo
usa davvero — `id`, `centerId`, `userStaffPositions`, `name`, `email` — perché `HostIdentityCurrentUserProvider`
non legge nient'altro. Il cookie torna singolo e smette di essere un contenitore di credenziali.
Verifica: login vero, e si conta quanti cookie `vipi.auth*` arrivano (prima ≥1, dopo esattamente 1).

### A4 — Pubblicare non è atomico, e la porta transazionale esiste già

`ReleaseService.PublishNowAsync` fa tre scritture indipendenti:

```csharp
await SnapshotAndSaveAsync(...);              // SaveChanges #1: la release
await _repo.PublishWorkingVersionAsync(...);  // SaveChanges #2: promuove la bozza, archivia la precedente
await PruneArchivedVersionsForTargetAsync(…); // SaveChanges #3: retention
```

Se la #2 fallisce — connessione persa, timeout, `max_allowed_packet` — resta una release pubblicata di un
documento la cui bozza **non** è stata promossa. La pubblicazione è l'operazione più importante che l'app
compie e non ha rete.

L'ironia è che la soluzione è già scritta: `IUnitOfWork.ExecuteInTransactionAsync` esiste, ha la sua
implementazione EF, ed è usata in **due** posti — entrambi in `NeighbourImportService`.

**Come la sistemo.** Avvolgo il corpo di `PublishNowAsync` (e di `PublishAsync`) in
`ExecuteInTransactionAsync`. Guardia: un test con un repository che lancia al secondo passo, e che pretende
zero release nel database dopo. ⚠️ Da verificare che la transazione regga su tutti e tre i provider — su
SQLite e MariaDB sì; su Postgres il reconciler di schema non c'entra, ma va provato che non litighi con
`EnableRetryOnFailure` (una strategia di retry rifiuta le transazioni manuali: serve
`CreateExecutionStrategy().ExecuteAsync(...)`, e questo è **il** punto che può far fallire la correzione).

### A5 — Leaflet arriva da `unpkg.com`, a runtime, da un sito che deve funzionare da solo

```html
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" integrity="sha256-…" />
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" integrity="sha256-…"></script>
```

L'SRI c'è, quindi la manomissione è coperta. Non è coperto il resto:

- **Disponibilità.** Se unpkg è irraggiungibile o filtrato, tutte le mappe (AoR 2D, aree regolamentate,
  confinanti) restano vuote. Non c'è ripiego.
- **Coerenza.** Nello stesso progetto i font sono self-hosted («nessuna chiamata esterna a Google Fonts») e
  three.js è vendorizzato in `wwwroot/vendor/`. Leaflet è l'unica eccezione, e non per una ragione scritta.
- **Privacy.** Ogni visitatore di `atc.it.ivao.aero` fa una richiesta a un terzo che non è nel giro.
- **CSP.** Finché sta lì, una CSP stretta (M5) deve aprire un host esterno.

**Come la sistemo.** Vendorizzo `leaflet.js`, `leaflet.css` e la cartella `images/` (i marker sono referenziati
dal CSS con percorso relativo — è la parte che si dimentica) in `wwwroot/vendor/leaflet/`, e le servo via
`AssetVersion.Url(...)` come tutto il resto. ~150 KB, caricati solo dove servono le mappe.
Guardia: un test E2E che pretende **zero** `src`/`href` verso host esterni nella pagina servita — la stessa
forma della guardia già scritta per il cache-busting in C4.

---

## Media

### M1 — Open redirect nel ritorno dal login

```csharp
private static string SafeReturn(string? returnUrl) =>
    !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
        ? returnUrl : "/vsop";
```

Il controllo copre `//evil.com` ma non `/\evil.com`: Chrome ed Edge normalizzano `\` in `/` **prima** di
risolvere l'URL, quindi `/vsop/auth/login?returnUrl=/\evil.com` fa atterrare l'utente su `https://evil.com`
dopo un login riuscito su un dominio IVAO. È un buon punto di partenza per il phishing, perché il primo salto
è autentico.

**Come la sistemo.** Rifiuto anche il secondo carattere `\` e `/`, e per non doverci ripensare uso la regola
che ASP.NET Core già applica: `Uri.IsWellFormedUriString(url, UriKind.Relative)` più il controllo esplicito
sui prefissi. Test con la tabella dei casi: `//evil`, `/\evil`, `\\evil`, `https://evil`, `/vsop/x` (unico
accettato).

### M2 — OIDC: la validazione della userinfo è stata svuotata, il nonce è spento

`IvaoOidcProtocolValidator.ValidateUserInfoResponse` è un metodo **vuoto**. Quel che la base faceva era una
cosa sola e importante: verificare che il `sub` della userinfo coincida con quello dell'id_token. Senza,
l'identità dell'utente viene dalla risposta userinfo **senza legarla al token che l'ha autorizzata**.

`ShouldValidateNonce = false` toglie la protezione dal replay dell'id_token.

Va detto con onestà quanto vale: il flusso è authorization code con PKCE, e la protezione CSRF del callback
non è il `RequireState` del validator ma il **cookie di correlazione** dell'handler ASP.NET Core, che è ancora
attivo (i «Correlation failed» del 3 agosto lo dimostrano — funzionava, e si è rotto quando si è rotto il
key-ring). Quindi non è una porta aperta: è una **rete tolta** su un percorso che ne ha altre.

**Come la sistemo.** Non ripristino alla cieca — il commento dice che con la validazione piena il login IVAO
falliva, e credo al commento. Faccio così: rimetto il controllo `sub`-vs-`sub` **da solo** (è una riga, e non
è la parte non conforme: la non conformità di IVAO sta nella *forma* della userinfo, non nel `sub`), lascio
il resto svuotato, e lo provo su un login vero. Se il `sub` non torna, la cosa da scrivere è **perché** —
oggi il commento dice «non standard» e non dice quale campo.
Sul nonce: va riprovato con `shouldValidateNonce: true` una volta sola, durante A6/A10. Se passa, resta acceso.

⚠️ Questo è l'unico punto dell'audit dove la correzione **richiede un login IVAO vero** per essere validata.

### M3 — L'identità dentro il circuito sta in piedi per un motivo che nessuno ha scritto

`HostIdentityCurrentUserProvider.Get()` legge `IHttpContextAccessor.HttpContext?.User`. Il progetto **sa** che
è un terreno scivoloso: `LiveBadge.razor:27` lo dice a chiare lettere — «`ICurrentUserProvider` legge
l'`HttpContext`, che in un circuito interattivo non c'è più» — ed è il motivo per cui il VID gli arriva come
parametro dal layout.

Ma le ventiquattro pagine `@rendermode InteractiveServer` chiamano `Authz.IsAdmin` **dentro il render e dentro
gli handler**, cioè esattamente nel circuito. Funziona perché sotto trasporto WebSocket la richiesta HTTP di
upgrade resta viva quanto la connessione, e con lei l'`HttpContext`. È vero, e non è documentato da nessuna
parte come garanzia: cambia con il fallback a long-polling, e cambia con la versione del framework.

Secondo problema, indipendente e misurabile: `IsAdmin` non è memoizzato. Ogni valutazione fa
`FindAll(claim)` + `JsonDocument.Parse` dell'array `userStaffPositions`. `StrutturaPage.razor` lo valuta
**sette volte per render**, due delle quali dentro un `@foreach` sui nodi della gerarchia. Un albero da 300
callsign sono ~600 parse JSON per ridisegno.

Terzo: il progetto **non usa** `AuthenticationStateProvider`, `CascadingAuthenticationState` né `AuthorizeView`
— zero occorrenze. È una scelta legittima (l'authz vera è nei servizi, e lì è fatta bene), ma significa che
tutta l'identità lato UI poggia sull'unico meccanismo che Blazor sconsiglia.

**Come la sistemo**, in due mosse separabili:

1. **Subito e a costo zero:** memoizzo in `EditAuthorizationService` — è `Scoped`, quindi vive quanto il
   circuito: `private bool? _isAdmin;` calcolato una volta. Toglie le 600 parse e non cambia semantica.
2. **Poi, e con più cura:** l'identità si risolve **una volta** all'apertura del circuito e si tiene lì.
   La strada idiomatica è un `AuthenticationStateProvider` con `CascadingAuthenticationState`; la strada corta
   è un `CircuitHandler` che cattura il `CurrentUser` alla creazione del circuito e lo serve a
   `ICurrentUserProvider`. Preferisco la corta: non tocca nessuna pagina, e le ventiquattro `Authz.IsAdmin`
   restano scritte come sono.

### M4 — `X-Forwarded-For` e `Host` sono scelti dal chiamante

Tre pezzi che presi da soli si difendono e insieme no:

- `Program.cs`: `forwardedOptions.KnownNetworks.Clear(); KnownProxies.Clear();` → «fidati dell'header da
  chiunque». Era necessario su Render (IP del proxy non fisso). Su `atc.it.ivao.aero` **non lo è più**: nginx
  sta sulla stessa macchina.
- `nginx-vipi.conf`: `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;` → *appende* l'IP reale a
  quello che il client ha mandato, invece di sostituirlo.
- `appsettings.json`: `"AllowedHosts": "*"` e nginx che passa `Host $host` senza filtro.

Conseguenze: il tetto per-IP del bridge Aurora è aggirabile ruotando l'header (già annotato in B2, ma la causa
sta qui e non lì); i log di accesso dicono l'IP che il chiamante ha scelto; e il `redirect_uri` che l'handler
OIDC costruisce nasce dall'`Host` della richiesta — che IVAO respinge se non è quello registrato, ma è una
difesa che sta dall'altra parte del filo.

**Come la sistemo.** In `appsettings.Production.json`: `"AllowedHosts": "atc.it.ivao.aero"`. In nginx:
`proxy_set_header X-Forwarded-For $remote_addr;`. Nel codice: `KnownProxies.Add(IPAddress.Loopback)` quando
l'ambiente è Production, tenendo il comportamento attuale altrove (Render serve ancora). Tre righe in tre file,
e vanno insieme: due su tre lasciano il buco.

### M5 — Nessun header di sicurezza, da nessuna parte

Né `Program.cs` né `nginx-vipi.conf` emettono `Content-Security-Policy`, `X-Frame-Options`,
`Referrer-Policy`, `Permissions-Policy` o `X-Content-Type-Options` globale (`nosniff` c'è, ma solo
sull'endpoint delle immagini).

Il sito è per buona parte contenuto editoriale scritto da staff e reso in pagina; le due funzioni che rendono
HTML costruito a mano (`MarkdownLite`, `AorBlock.BuildSvg`) le ho lette entrambe e **encodano prima e
costruiscono dopo**, quindi non c'è una XSS da chiudere oggi. La CSP serve a rendere innocuo l'errore di
domani, ed è la difesa che costa meno di tutte.

**Come la sistemo.** Un middleware di sei righe che aggiunge:
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` (nessuna pagina va in iframe; se
l'embedding in `Ivao.It.Website` tornerà d'attualità, diventa `frame-ancestors`), `Referrer-Policy:
strict-origin-when-cross-origin`.
La CSP la introduco **in `Content-Security-Policy-Report-Only`** per primo giro, perché con l'inline script
dello zoom in `App.razor` e Blazor una CSP stretta rompe la pagina — e va vista rompersi in report prima che
in produzione. Poi si stringe: `script-src 'self' 'nonce-…'` dopo aver spostato l'inline in un file.
⚠️ Se A5 (Leaflet) non è stata fatta prima, la CSP deve aprire `unpkg.com`: sono due voci che conviene fare
nell'ordine A5 → M5.

### M6 — Chiunque abbia un login può bloccare l'editing della struttura

`ResourceLockService.AcquireAsync` chiede una cosa sola: che l'utente sia autenticato.

```csharp
if (_authz.CurrentUserId is not int uid) return Task.FromResult(LockInfo.Free());
return _repo.AcquireOrInspectAsync(resourceKey, uid, …);
```

`ResourceLockKeys.Structure` = `"admin:structure"`, cioè il lock delle quattro pagine di struttura, che sono
pagine **admin**. Un qualunque membro IVAO loggato può prenderlo e — con l'heartbeat della UI, o a mano ogni
tre minuti — tenerlo per sempre. Gli admin restano fuori dal loro strumento.

L'attenuante c'è: il force-unlock esiste ed è già riservato agli admin (`ForceUnlockAsync` controlla
`IsAdmin`). Quindi è fastidio, non blocco. Ma è fastidio che si ripete finché l'altro smette.

**Come la sistemo.** Do alle chiavi un requisito dichiarato invece che implicito: `ResourceLockKeys` diventa un
piccolo record `(Key, RequiresAdmin)`, e `AcquireAsync` chiama `_authz.EnsureAdmin()` quando la chiave lo
chiede. `editor:newdoc` resta com'è (l'editing di documento è già filtrato dai grant per ACC).

### M7 — `SidImporter.ImportAsync` è l'unica scrittura senza controllo di autorizzazione

Tutti i servizi che scrivono chiamano `EnsureCanEditAccAsync` / `EnsureAdmin`: le ho contate, sono oltre
sessanta chiamate su venti servizi. `SidImporter` — che con `ReplaceImportedSidsAsync` **cancella e riscrive**
le SID importate di un aeroporto — non ne ha nessuna, ed è iniettato direttamente in
`AeroportoEditorPage.razor:18`, dove `ReimportSids()` lo invoca.

Oggi non è sfruttabile: Blazor consegna solo gli eventi che appartengono all'albero renderizzato, e il bottone
è dietro il controllo di editing della pagina. È un buco **di principio**, non di fatto — ma il principio è
scritto nel codice, in cima a `IEditAuthorizationService`: «Verifica **sempre** server-side».

I fratelli lo rispettano tutti: `AirportSectorService.ImportFromSourceAsync` ha il guard alla riga 84,
`AirportEditingService.ReimportFromSourceAsync` alla 217. Quello che manca è uno solo, e stona.

**Come la sistemo.** Inietto `IEditAuthorizationService` in `SidImporter` e chiamo
`EnsureCanEditAccAsync(accDellAeroporto, ct)` in testa. ⚠️ Attenzione: lo stesso `ISidImporter` è chiamato
anche da `SidImportHostedService`, che gira **senza utente**. Quindi la firma va sdoppiata come è già stato
fatto altrove in questo progetto — `AccAdminService.cs:35` porta il commento «solo il manual applica il guard»,
che è esattamente il modello da copiare: due ingressi, il guard sul percorso interattivo.

### M8 — Il file che racconta un avvio fallito racconta anche lo stack

`StartupDiagnostics` scrive due file **accanto all'eseguibile**: `avvio-diagnostica.txt` e `avvio-errore.txt`.
La scelta è ottima e va tenuta — su un host senza `journalctl` è l'unico modo di sapere perché il servizio non
parte, e il riepilogo di configurazione è scritto con cura (password mascherata, segreti riportati solo come
«valorizzato (N caratteri)»).

Il punto scoperto è l'altro file. `Describe(ex)` scrive `ex.ToString()` **intero**: messaggi, eccezioni interne
e stack trace completo. È il contenuto giusto per chi deve capire, e il contenuto sbagliato da lasciare in una
cartella che su un hosting a pannello/FTP può stare dentro il documento radice.

**Come la sistemo.** Due cose piccole. I file vanno in una sottocartella `diagnostica/`, creata dall'app, e il
LEGGIMI di consegna chiede esplicitamente che non sia raggiungibile dal web (con la riga di nginx pronta:
`location ~ ^/diagnostica/ { deny all; }`). E `avvio-errore.txt` nasce con permessi `600` dove il sistema lo
consente. Non tolgo lo stack: serve, ed è il motivo per cui il file esiste.

### M9 — Lo stream SSE è anonimo, senza tetto, e lascia in giro un semaforo

`/vsop/live/atc` (`VipiModuleExtensions.cs:156`) non ha autenticazione né limite di connessioni concorrenti.
Ogni connessione tiene un `SemaphoreSlim` e una sottoscrizione a `cache.Changed`, e il `finally` disiscrive
l'evento ma **non fa `Dispose()` del semaforo** — che ha un handle di attesa allocato pigramente, quindi il
`WaitAsync` con timeout lo materializza.

Il contenuto è pubblico e innocuo (quanti ATC online e a che ora), quindi non è un problema di riservatezza:
è un problema di quante connessioni tiene aperte un processo su una macchina condivisa.

**Come la sistemo.** `using var signal = new SemaphoreSlim(0);` — una parola. E un contatore di connessioni
attive nell'endpoint che risponde `503` oltre una soglia configurabile (poche centinaia): il numero di persone
che guardano la vista live è noto e piccolo, e superarlo di molto significa che sta succedendo altro.

### M10 — Nessun tetto ai circuiti Blazor su una macchina che non è nostra

`builder.Services.AddRazorComponents().AddInteractiveServerComponents();` — senza opzioni. Restano i default:
`DisconnectedCircuitMaxRetained = 100`, `DisconnectedCircuitRetentionPeriod = 3 min`,
`MaxBufferedUnacknowledgedRenderBatches = 10`. Cento circuiti disconnessi trattenuti tre minuti ciascuno, con
tutto lo stato delle pagine editor dentro, su un hosting condiviso.

C5 ha già deciso — bene — che si va a **istanza singola**, e ha scritto il vincolo nel `nginx-vipi.conf`.
Quella decisione rende però il tetto di memoria di quell'unico processo l'unica cosa che regge il sito.

**Come la sistemo.** `AddInteractiveServerComponents(o => { o.DisconnectedCircuitMaxRetained = 25;
o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2); })`, e `DetailedErrors` esplicitamente
**false** fuori da Development (oggi lo è per default, ma è meglio scritto che dedotto). Numeri da rivedere
dopo il primo ciclo AIRAC vero: sono una stima, e lo dico.

### M11 — Se il battito del lock inciampa, muore in silenzio

`EditLockBar.HeartbeatLoop` cattura `OperationCanceledException` e `ObjectDisposedException`, con un commento
che spiega bene perché serve la seconda. Non cattura nient'altro. Il corpo del ciclo chiama
`Locks.HeartbeatAsync(...)`, che va al database.

Un errore transitorio — la disconnessione di MariaDB, un timeout — esce dal `while`, esce dal `try`, e finisce
in un task fire-and-forget: nessuno lo osserva, l'utente non vede niente, e il lock scade dopo tre minuti
mentre sta ancora scrivendo. Al primo salvataggio si becca «Lock scaduto o preso da un altro editor», che è il
messaggio giusto per la causa sbagliata.

**Come la sistemo.** `catch (Exception ex)` in coda al ciclo, che **non** interrompe il battito: logga, aspetta
il tick successivo e riprova. Un heartbeat che salta un colpo su un TTL di tre minuti e un periodo di sessanta
secondi ha due tentativi di recupero prima che il lock scada — che è esattamente il margine per cui quei numeri
sono stati scelti.

### M12 — `StateHasChanged` dopo che il componente non c'è più (quattro copie)

Il motivo è lo stesso in tutti e quattro i posti (`AeroportoEditorPage`, `AccEditorPage`, `AppEditorPage`,
`VloaEditor`): il badge «Salvato» si spegne da solo.

```csharp
private async Task DismissSavedAsync(int tick)
{
    await Task.Delay(2000);
    if (tick == _saveTick && _save == SaveState.Saved) { …; await InvokeAsync(StateHasChanged); }
}
```

Invocata come `_ = DismissSavedAsync(tick)`. Il contatore `tick` protegge dal salvataggio *successivo*, non
dalla **navigazione**: chi salva e cambia pagina entro due secondi lascia dietro un `InvokeAsync` su un
renderer smontato, che lancia in un task che nessuno osserva.

**Come la sistemo.** Un `CancellationTokenSource` per componente, annullato nel `Dispose`, passato al
`Task.Delay` — e siccome il codice è **identico** in quattro posti, va estratto una volta sola (vedi D1, che è
la stessa estrazione vista da un'altra angolazione).

### M13 — Un settore fatto di più poligoni ne perde tutti tranne il primo

`PolygonGeometry.ExtractPoints`:

```csharp
// Annidamento di un livello (es. [[[lng,lat],…]]): scendi al primo anello.
if (items[0].ValueKind == JsonValueKind.Array && items[0].EnumerateArray().FirstOrDefault().ValueKind == JsonValueKind.Array)
    return ExtractPoints(items[0]);
```

Il commento è onesto — dice «al primo anello» — ma la conseguenza non è scritta: in un MultiPolygon GeoJSON
quel livello di annidamento **è** l'elenco dei poligoni. Un settore composto da due aree disgiunte, o un
poligono con un buco, entra nel sistema con **solo la prima parte**, in silenzio, e da lì alimenta la mappa
AoR, il calcolo di adiacenza dei confinanti e i poligoni pubblicati.

Un settore che perde metà della propria forma sbaglia i vicini, e i vicini decidono i coordinamenti.

**Come la sistemo.** Prima **misuro**: una query sui poligoni reali in archivio che conta quanti hanno più di
un anello a quel livello. Se sono zero, la voce si chiude con la misura scritta e un test che fotografa il
comportamento voluto. Se non sono zero, `ToRing` deve restituire **più** anelli e chi lo consuma va adeguato —
ed è un lavoro più grosso di questa riga, da mettere in conto a parte.

⚠️ Non tocco niente prima di aver misurato: qui la correzione a occhio è più rischiosa del difetto.

### M14 — Il pacchetto consegnato non è riproducibile

I `PackageReference` usano wildcard: `8.0.*`, `10.0.*`, `Avalonia 11.2.*`,
`Microsoft.AspNetCore.Authentication.OpenIdConnect 8.0.*`. Non esiste `Directory.Packages.props`, non esiste
un `packages.lock.json`.

Vuol dire che ricompilare **lo stesso commit** fra due mesi produce un binario diverso da quello che sta in
`artifacts/publish/vipi-linux-x64-mariadb-20260809.zip`. Per un pacchetto self-contained consegnato a terzi,
che va rigenerato ogni volta che si corregge qualcosa, è una differenza che nessuno può vedere finché non
rompe qualcosa.

**Come la sistemo.** `Directory.Packages.props` con central package management: le versioni si scrivono una
volta, in un posto, e i venti `.csproj` smettono di poter divergere. Le wildcard diventano versioni esatte
(quelle attualmente risolte, che si leggono in `project.assets.json`). Poi
`<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` e i `packages.lock.json` committati.
⚠️ Il multi-target complica: alcune versioni sono già condizionate al TFM, e vanno tenute tali.

### M15 — La pagina dice sempre di essere in italiano

`App.razor:2` → `<html lang="it">`, costante. Ma il modulo è localizzato in `it` e `en`
(`SupportedCultures = { "it", "en" }`, 1232 chiavi per lingua, e A6 ha verificato la vLOA in inglese).

Con la UI in inglese, `lang="it"` dice a uno screen reader di pronunciare l'inglese con le regole italiane, e
al browser di offrire una traduzione che non serve. È il tipo di difetto che non si vede provando l'app e si
sente usandola.

**Come la sistemo.** `<html lang="@System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName">`.
Una riga; `App.razor` gira già sul server con la cultura risolta da `UseRequestLocalization`.

### M16 — Testo italiano ancora dentro il markup

`AorBlock.razor` rende in pagina, come letterali: «Sovrapposizione = confine condiviso tra i settori
visibili…», «Configurazione:», «azzera», «Selezionando una configurazione si evidenziano…».

Il commit `e2f8639 fix(13/S14)` dichiara che «tutto il testo visibile dei **tre documenti** passa dalle
risorse» — ed è vero per i tre documenti. `AorBlock` è un blocco condiviso, e sta fuori da quel perimetro: la
frase è vera e la copertura no.

**Come la sistemo.** Sposto le quattro stringhe nelle risorse. E poi la guardia che manca a monte: un test che
cammina i `.razor` e segnala il testo letterale fuori da `L[...]` — con una whitelist esplicita, perché i falsi
positivi (simboli, unità, callsign) sono tanti. Vale la pena scriverla una volta: è la stessa forma della
`StationResolverPrewarmTests` già in casa, che ha funzionato.

### M17 — `UseAntiforgery()` sta prima dell'autenticazione

In `Program.cs` l'ordine è: `UseStaticFiles` → `UseAntiforgery` → `UseAuthentication` → `UseAuthorization`.
La guida Blazor chiede il contrario: `UseAntiforgery` **dopo** i due. Il motivo è che il token antiforgery va
legato all'identità che lo richiede, e all'ora in cui il middleware gira l'identità non è ancora montata.

Non ho un exploit da mostrare, e lo dico chiaramente: il sintomo tipico è un token che resta valido dopo un
cambio di utente sulla stessa sessione. Ma è un ordine che non ha nessuna ragione di essere quello, e cambiarlo
costa lo spostamento di una riga.

**Come la sistemo.** Sposto `app.UseAntiforgery()` dopo il blocco `if (authEnabled) { UseAuthentication();
UseAuthorization(); }`. ⚠️ Da riprovare subito con un login vero: l'ordine dei middleware in Blazor è la classe
di modifica che rompe in modo vistoso, e se rompe si vede al primo POST.

---

## Debito strutturale

**D1 — Lo stesso editor scritto quattro volte.** `private enum SaveState { Idle, Saving, Saved }`,
`Guarded(...)` con la sua catena di `catch`, `DismissSavedAsync` e `DismissInfoAsync` sono copiati in
`AeroportoEditorPage`, `AccEditorPage`, `AppEditorPage`, `VloaEditor` — e le prime due anche in
`AdminTrasferimentiPage`, `AeroportiPage`, `ConfinantiAdminPage`, `StrutturaPage`. Otto copie della stessa
gestione «occupato / salvato / errore».
→ Un `EditorActionRunner` (servizio scoped o classe base) con `SaveState`, `Guarded`, l'auto-dismiss e il
`CancellationTokenSource` di M12 dentro. Chiude M12 di conseguenza, invece che quattro volte.
⚠️ Prima di dichiararla duplicazione ho confrontato i **corpi**, non le firme: i `catch` divergono un po' fra i
file, e l'estrazione deve prendere l'unione — non il minimo comune.

**D2 — File che nessuno rilegge.** `AeroportoEditorPage.razor` 1538 righe, `AdminTrasferimentiPage.razor` 1126,
`EfEditingRepository.cs` 1089, `AeroportoPage.razor` 878. Il primo è una pagina con dodici sezioni
indipendenti dentro un `@code` solo.
→ Non un refactor per il gusto: le sezioni dell'editor aeroporto sono già visivamente separate, e ognuna
diventa un componente col proprio stato. Da fare **una alla volta**, e solo quando si tocca quella sezione per
altro.

**D3 — a11y.** 21 `@onclick` su `div`/`span`/`a`/`td`: non raggiungibili da tastiera, non annunciati come
comandi. `aria-*`/`role` compaiono in 13 file su 57.
→ Passata mirata: i 21 diventano `<button class="…">` (lo stile si conserva con una classe reset) o prendono
`tabindex="0"` + `role="button"` + `@onkeydown`. Poi una passata con axe sulle cinque pagine più usate.

**D4 — Niente retention su `AuditLogs`.** Cresce e basta. Le release hanno la loro potatura (fatta il 20
luglio), l'audit no.
→ Stessa forma già collaudata: sweep all'avvio, soglia configurabile, idempotente.

**D5 — Immagini orfane.** `MediaAsset` non ha FK di proposito (una release pubblicata cita lo sha), quindi la
raccolta va fatta a mano da `MediaCleanupCard`. Su un database condiviso il conto lo paga qualcun altro.
→ Un rilievo nella diagnostica quando gli orfani superano una soglia, così la cartella si svuota perché
qualcuno lo vede, non perché se lo ricorda.

**D6 — La concorrenza ottimistica copre solo i blocchi.** `RowVersion` fa round-trip fino al client (via DTO)
solo per `ContentBlock` (`EfEditingRepository.cs:622`). Per `DocumentSection` il token è nel modello, quindi
protegge il singolo `SaveChanges`, ma non due editor sulla stessa sezione a dieci minuti di distanza.
→ Oggi lo copre il lock di risorsa, ed è probabilmente abbastanza. Da tenere scritto, non da correggere ora.

**D7 — 44 chiavi di risorsa mai usate**, e `VloaListPage.KnownPrefixes` ricopia a mano l'elenco dei paesi che
sta già nel `.resx`: aggiungere un paese in un posto solo dà una tendina che non mostra il nome.
→ La whitelist si deriva dalle chiavi presenti, o sparisce.

**D8 — Startup che scrive.** Sei passate di riconciliazione girano **a ogni avvio**, in sincrono, prima di
`app.Run()`, senza lock distribuito. Sono idempotenti (verificato) e la scala è a istanza singola (decisa in
C5), quindi oggi regge. Ma è l'unica parte del sistema dove «una sola istanza» non è una preferenza: è un
requisito non scritto nel codice.
→ Il vincolo va **nel codice**: un lock applicativo sul database attorno al blocco di riconciliazione, che su
una seconda istanza fa aspettare invece di far correre. Poche righe, e toglie una trappola dal futuro.

**D9 — Nessun `Directory.Build.props`.** Niente `TreatWarningsAsErrors`, niente `AnalysisLevel`, niente
`EnforceCodeStyleInBuild`. È la causa a monte di B1: un avviso che nasce sulla macchina di chi scrive è
arrivato fino alla CI perché niente lo trasformava in un ostacolo.
→ `Directory.Build.props` con `TreatWarningsAsErrors` e `AnalysisLevel=latest-recommended`.
⚠️ Da introdurre **dopo** aver ripulito i warning esistenti, o la prima compilazione è un muro.

**D10 — nginx nudo.** Nessun `add_header`, nessun `limit_req`, nessun `client_max_body_size`. Il terzo
probabilmente non serve (gli upload passano da SignalR, non da POST), ma «probabilmente» è la parola che
segnala una cosa mai provata: **un caricamento da 3 MB non è mai stato fatto** — A6 ne registra uno da 694
byte. Il limite vero da guardare è `MaximumReceiveMessageSize` degli hub, che non è configurato.
→ Con M5 arrivano gli header; `limit_req` sulle rotte anonime; e un upload da 3 MB nella prossima verifica
live, che è l'unico modo di sapere.

**D11 — Documentazione che invecchia più in fretta di quanto la si poti.** 58 file, 1,1 MB, con documenti
marcati «superato» a mano in testa (piano MySQL, ADR-0007 §D4 e §D4-bis).
→ Non è una crepa del prodotto, ed è il costo di aver documentato bene. Vale però la stessa regola dei
`Country_*`: quando lo stesso fatto è scritto in due posti, uno dei due sta già mentendo.

---

## Ordine di esecuzione proposto

Non è l'ordine di gravità: è l'ordine in cui le voci **si sbloccano a vicenda**.

**Passo 0 — sbloccare il merge di B5** (mezza giornata)
B1 (duplicati `.resx` + test di guardia). Senza questo il ramo del doc 13 non va in `main`, e tutto il resto
si costruirebbe su una base che non compila in CI.

**Passo 1 — la rete che dice la verità** (1–2 giorni)
B2 (test su net8) e D9 (`Directory.Build.props`), in quest'ordine. Sono le due voci che cambiano **cosa si
scopre** da qui in avanti: fatte queste, ogni correzione successiva è verificata sul TFM che va in produzione.
⚠️ B2 può far emergere test che su net8 non compilano: è il suo scopo, ma va messo in conto come tempo.

**Passo 2 — il bordo, prima della consegna** (2–3 giorni)
A3 (token nel cookie), M1 (open redirect), M4 (XFF/Host), M5 (header + CSP report-only), A5 (Leaflet), M8
(diagnostica), M6, M7, M9, M10. Sono indipendenti fra loro e quasi tutte piccole. A5 va prima di M5.
Fine del passo: una passata di verifica live sola, che li copre tutti insieme.

**Passo 3 — quello che si rompe sotto carico** (2–3 giorni)
A1 (ricerca), A2 (health), A4 (transazione sulla pubblicazione), M11, M12+D1. A4 è la più delicata per via
della strategia di retry su Postgres, e va fatta con calma.

**Passo 4 — misure e decisioni, non codice**
M13 (misurare i multi-poligono prima di toccarli), M2 (richiede un login IVAO vero, quindi va con A6/A10),
M14 (lock file, da fare quando si rigenera il pacchetto di consegna), D3, D4, D5, D8.

**Fuori scala, da programmare a parte:** D2 (i file-mostro) e M3 punto 2 (l'identità del circuito). Nessuna
delle due è urgente, entrambe toccano molto, e nessuna va fatta di fretta.

---

## Cosa questo audit **non** dice

- **Non ho eseguito l'applicazione.** Tutto ciò che è marcato «letta» viene dal codice; le due voci marcate
  «riprodotta» (B1, B2) e la «verificata» (A3, M14) sono state provate con un comando. La differenza conta:
  M2, M17 e M13 sono le tre voci dove leggere non basta e la verifica live può ribaltare la conclusione.
- **Non ho provato MariaDB.** Le voci di persistenza (A4, D8) sono ragionate sul codice, non misurate sul
  server.
- **Non ho contato la copertura.** «Mille test non girano su net8» è una conta di *test*, non di *copertura*:
  quanto di quel codice sia davvero esercitato non lo so, e nel progetto non c'è ancora una misura.
- **Non ho toccato il bridge Aurora** oltre a quel che serviva per M4: è spento per default, e la sua
  superficie è già stata rivista in B2 con più attenzione di quanta gliene avrei dedicata io qui.

---

## Esito dell'esecuzione — 11 agosto 2026

Tutte le voci sono state affrontate nell'ordine proposto. Quattro commit: `f44a3bb` (passo 0+1), `3807585`
(passo 2), `dc2ca23` (passo 3), più quello del passo 4.

**Suite: 2087 test verdi** (net8 1102, net10 985 — erano 347 su net8 prima del passo 1). Build pulita con
`TreatWarningsAsErrors`, zero avvisi.

### Chiuse (23)

| # | Come è finita |
|---|---|
| **B1** | 14 chiavi duplicate rimosse per file. Tre guardie in `SharedResourceIntegrityTests` che leggono i `.resx` **dal disco** — un duplicato nella risorsa compilata non c'è più per definizione. **Vista fallire** inserendo un duplicato finto. |
| **B2** | Application/Domain/Hosting/Ui multi-target; E2E e AuroraBridge portati a **net8 e basta**, perché i progetti che avviano (`Vipi.Host`, `AuroraBridge.Core`) sono net8 e basta — prima li caricavano in roll-forward sul runtime 10. Il job CI fa `dotnet test -f net8.0` invece di `dotnet build`. |
| **D9** | `Directory.Build.props` con `TreatWarningsAsErrors`. Gli avvisi NuGet restano avvisi (li decide il feed, non noi) ma `NuGetAudit` è acceso in modalità `all`. |
| **A1** | Filtro dei blocchi spostato nel database + `ToLookup` al posto delle due `Where` nel ciclo. Il filtro è `ToLower()` su entrambi i lati e **non** un `LIKE` nudo: `LIKE` segue la collation, e in produzione è `as_cs` — un `LIKE` nudo avrebbe reso la ricerca sensibile alle maiuscole in silenzio, solo su MariaDB. |
| **A2** | `ConsistencyReportCache`, TTL 2 minuti, stessa forma di `GlobalTopologyCache`. Endpoint lasciato **aperto** di proposito: il problema era il costo, non la riservatezza, e chiuderlo dietro il login lo toglierebbe a chi ha più motivo di guardarlo. |
| **A3** | `SaveTokens = false`. |
| **A4** | Pubblicazione dentro `IUnitOfWork`. Test **visto fallire** senza la transazione: «Assert.Empty() Failure: Collection was not empty». |
| **A5** | Leaflet vendorizzato in `wwwroot/vendor/leaflet/`. Lo sha256 dei file scaricati **combacia** con l'`integrity` che stava in `App.razor` — stessi byte, verificato. Guardia E2E: zero `src`/`href` esterni nella pagina servita. |
| **M1** | `SafeReturn` riscritta, 16 casi in tabella. |
| **M4** | Loopback fra i proxy fidati in Production; nginx passa `$remote_addr`; `AllowedHosts` col nome vero. |
| **M5** | Header su ogni risposta + CSP in **sola segnalazione**. Guardia E2E su due percorsi. |
| **M6** | `ResourceLockKeys.RichiedonoAdmin`. Due test: la struttura la prende solo un admin, `editor:newdoc` resta di tutti. |
| **M7** | Due ingressi come in `AccAdminService`. **Prima non aveva alcun test**; ora ne ha cinque. |
| **M8** | Sottocartella `diagnostica/` + la riga di nginx che la nega. |
| **M9** | `using` sul semaforo + tetto a 300 connessioni contemporanee. |
| **M10** | 25 circuiti trattenuti, 2 minuti, `DetailedErrors` scritto. Numeri **stimati**: da rivedere dopo il primo ciclo AIRAC dal server nuovo. |
| **M11** | `catch` generico che salta il colpo e riprova al tick dopo. |
| **M12** | `DelayedUiAction` al posto del contatore in quattro editor, con cinque test. |
| **M13** | **Misurata, non corretta** — vedi sotto. |
| **M14** | `packages.lock.json` committati + «locked mode» nel restore della CI. **Non** la gestione centralizzata dei pacchetti: la riproducibilità la dà il lock file, e toccare venti csproj con versioni già condizionate al TFM è rischio senza il guadagno che conta. |
| **M15** | `<html lang>` segue la cultura risolta. |
| **M16** | `AorBlock` e `AirportQuickPanel` passano dalle risorse (7 chiavi nuove per lingua). |
| **M17** | `UseAntiforgery()` dopo `UseAuthentication`/`UseAuthorization`. |
| **D7** | La lista dei prefissi noti sparisce: si chiede al localizzatore se la chiave esiste (`ResourceNotFound`). |

### Ribaltate dalla misura (3)

Il piano diceva «misurare prima di toccare». Misurato, tre voci **non** andavano fatte — e dirlo è il
risultato, non una scorciatoia.

- **M13 — multi-poligono.** Contati i poligoni reali in `vipi.db`: **1338**, di cui 1273 con un anello solo,
  50 colonne vuote, 15 array vuoti. **Zero** con più di un anello. Quindi il ramo che scende solo nel primo
  non perde niente oggi. Far restituire più anelli a `ToRing` significherebbe toccare tutti i consumatori —
  mappa AoR, adiacenza dei confinanti, poligoni pubblicati — per un caso che non si verifica. Fatto invece:
  la misura è scritta nel commento, e un test **fotografa** il limite dicendo che se un giorno sembrerà
  sbagliato la correzione è restituire tutti gli anelli, non aggiustare l'asserzione.
- **D4 — retention dell'audit.** `AuditLogs` ha **19 righe**, dal 12 al 31 luglio, cioè tre settimane di
  sviluppo fitto: circa 330 righe l'anno. Costruire una potatura sarebbe infrastruttura da mantenere per un
  problema che non esiste.
- **D5 — immagini orfane.** `MediaAssets` ha **1 riga**. Stessa conclusione.

### Non fatte, con la ragione (5)

- **M2 — nonce e validazione userinfo OIDC.** Richiede un login IVAO vero per essere verificata, e sbagliare
  lì significa non far entrare nessuno il giorno del cutover. Va con **A10**, non prima.
  → **CHIUSA a metà il 22-ago-2026** (ramo `login-nome-cognome`), col login vero in mano: il **nonce si
  valida** (IVAO lo mette nell'id_token). La **userinfo no**, e resta così: `/v2/users/me` non è una
  userinfo OIDC. Scoperta lungo la strada: `RequireState = true` non c'entra con IVAO — ASP.NET Core non
  popola mai quel campo e il login si rompe con qualunque IdP (`IDX21329`); lo `state` lo controlla
  l'handler col cookie di correlazione.
- **A3, seconda metà — `ClaimActions.MapAll()`.** Stessa ragione: restringere la mappa dei claim è giusto,
  ma un nome di campo sbagliato non lancia — toglie l'admin, in silenzio, al primo accesso dopo il cutover.
  Il grosso del cookie erano comunque i token, ed è già andato via.
  → **CHIUSA il 22-ago-2026** (stesso ramo). Il timore era giusto e il modo di scioglierlo è stato misurare
  il payload reale di `/v2/users/me` **prima** di scrivere la mappa (sonda con le credenziali di prova
  pubblicate da IVAO), poi verificare sul login vero che le posizioni staff arrivassero ancora. Restano
  sette claim; `userStaffPositions` si riduce ai soli codici (erano ~1,5 kB per due incarichi). In più
  `MapAll()` **azzerava** le `DeleteClaim` del framework (`nonce`, `aud`, `iss`, `iat`, `exp`, `at_hash`):
  togliendolo tornano in servizio, e questo nell'audit non era stato visto.
- **D1 — estrarre `Guarded`.** `SaveState` e l'auto-dismiss erano identici e sono stati estratti. `Guarded`
  **no**: confrontati i corpi e non le firme, divergono davvero — `catch` diversi, `EditConflictException`
  che ricarica il lock solo in tre, messaggi diversi, e una versione che torna `bool`. Unificarla porterebbe
  l'unione di cinque `catch` dentro ogni chiamante.
- **D2 — i file da 1500 righe.** Da fare una sezione alla volta, quando si tocca quella sezione per altro.
- **D8 — lock distribuito all'avvio.** La scala decisa è una istanza (audit 22 luglio, voce A2), scritta in
  `nginx-vipi.conf`. Un lock applicativo vorrebbe dire un terzo dialetto SQL (`GET_LOCK` su MySQL,
  `pg_advisory_lock` su Postgres, niente su SQLite), che è esattamente il costo di cui ADR-0007 §D4-ter si
  lamenta già a due.

### Nate durante l'esecuzione (3)

- ⚠️ **`Tmds.DBus.Protocol` 0.20.0 — vulnerabilità high (GHSA-xrw6-gwf8-vvr9)**, tirata da Avalonia nel tool
  desktop. **Trovata dal `NuGetAudit` acceso in D9**, non da un allarme esterno. Provate 0.21.2, 0.22, 0.25,
  0.30, 0.80, 0.90: l'avviso resta su tutte, la **0.94.2 è la prima che lo chiude**. Pinnata. Il salto è
  grosso e riguarda l'integrazione D-Bus **su Linux**, che il tool non spedisce e il sito non referenzia
  affatto: rischio teorico su un bersaglio che non consegniamo. Da togliere quando Avalonia sale da sé.
- ⚠️ **Un test di `Vipi.AuroraBridge.Tests` fallisce a intermittenza** sotto carico: 2 volte su ~9 giri della
  suite intera, mai in 3 giri in isolamento né negli ultimi 5 giri interi. **Nome non catturato** — il logger
  normale non lo riporta quando il giro successivo passa. Sospetto `FakeAuroraServer`, che apre un socket TCP
  vero. **Voce aperta.** Un test che fallisce a caso è peggio di un test che manca: insegna a ignorare il rosso.
- ⚠️ **Un `Directory.Build.props` non valido si porta via tutto.** Un doppio trattino dentro un commento XML
  (stavo scrivendo il nome dell'opzione «locked mode» con i trattini davanti) rende il file illeggibile:
  MSBuild dà `MSB4024` e **nessuna** delle proprietà si applica, `TreatWarningsAsErrors` compreso. L'errore è
  rumoroso, ma la conseguenza — che quel file è un punto singolo di fallimento per tutte le garanzie di build
  — vale la pena saperla.

### a11y (D3) — metà

Le 12 chip/pill di filtro (ricerca, versioni, confinanti, «Cosa è cambiato», vista live, vista rapida
aeroporto) passano dal componente `Chip`: `role="button"`, `tabindex="0"`, `aria-pressed`, Invio e Spazio.
Rende uno `<span>` e non un `<button>` di proposito — i fogli di stile disegnano `.ch` e `.pill` su elementi
in linea, e un `<button>` porterebbe margini, font e box-sizing da azzerare in ogni tema. Sette test.

**Restano fuori**: le celle di tabella cliccabili di `AeroportiPage` (3) e i toggle dell'albero in
`StrutturaPage` (2). Sono comandi con una forma diversa e vanno guardati insieme alla pagina, non convertiti
a tappeto.

### Cosa resta aperto, in una riga

1. ✅ ~~Il **test intermittente** del bridge Aurora~~ — chiuso, vedi il seguito qui sotto. Resta **non
   riprodotto**: la correzione è la causa più probabile letta nel codice, più asserzioni che alla prossima
   occorrenza diranno che cosa è successo.
2. 🟡 **M2 + la seconda metà di A3**: al primo login IVAO vero (con A10).
3. 🟡 **CSP da segnalazione a vera**: `script-src` è già stretto; restano **17 gestori inline** nel markup e
   una passata con un browser vero. Vedi «Seguito 2».
4. ✅ ~~**D3 restante**~~ e ~~**M3 punto 1**~~ — chiuse nel «Seguito 2». Restano **D2** (file lunghi) e
   **M3 punto 2** (identità del circuito).
5. 🟢 I numeri di **M10** (tetti dei circuiti) sono stime: vanno rivisti su un ciclo AIRAC vero.

### Seguito — le tre voci nate durante l'esecuzione, chiuse

Le tre voci qui sopra erano state lasciate come «nate durante l'esecuzione», e una sola era davvero
sistemata. Ripreso e chiuso tutto lo stesso giorno.

**1. `Tmds.DBus.Protocol` — già chiusa.** Pin a 0.94.2, l'unica versione che chiude l'avviso.

**2. Il test intermittente — chiuso, ma non nel modo che speravo.**

Prima cosa da dire, perché cambia il valore di tutto il resto: **il controllo con cui avevo dichiarato «sei
giri puliti» non provava niente.** Cercava i file `.trx` sotto `/tmp`, che Git Bash mappa altrove e che
Python su Windows non risolve: il glob non trovava nulla e il codice stampava «nessun fallimento». Rifatto
col percorso vero: 6 giri della suite intera più 3 con la macchina sotto carico a 16 processi, **tutti
verdi**. Il guasto resta non riprodotto — l'ho visto due volte, e restano quelle due.

Non potendo riprodurlo, l'ho chiuso leggendo il codice, e le due cose fatte sono diverse fra loro:

- **Ipotesi, non certezza.** Il tempo d'attesa dei test che pretendono una risposta era 1500 ms — un valore
  che lì non misura niente di utile, perché il server finto è su localhost. Con dodici assembly di test in
  parallelo il thread-pool cresce di circa un thread al secondo oltre il numero di core, e il `Task.Run` del
  ciclo di lettura può aspettare il proprio turno per centinaia di millisecondi: scaduto il tempo,
  `SendAsync` torna `Ok = false` e `Assert.True(response.Ok)` fallisce **senza dire perché**. Portato a
  15000 ms (il test del silenzio, che ha bisogno di un tempo corto, se lo passa a mano). **Non è verificato
  che questo fosse il guasto.**
- **L'altra metà, che vale a prescindere:** le asserzioni ora riportano `response.Error` e la riga grezza.
  Alla prossima occorrenza il messaggio dirà cosa è successo invece di «expected True, actual False». Se
  l'ipotesi è sbagliata, è così che lo si scopre.

**E cercandolo è saltato fuori un difetto vero, questo sì riprodotto.** `AuroraClient.EnsureConnectedAsync`
passava a `ConnectAsync` il token del chiamante e basta: **`TimeoutMs` non copriva la connessione**, solo
l'attesa della risposta. Un host che *tace* invece di rifiutare — firewall che scarta i SYN, macchina spenta
con l'IP ancora assegnato — lasciava il tool fermo per il timeout del sistema operativo mentre l'opzione
diceva 3000 ms. Misurato: **21,1 secondi** contro i 500 ms richiesti. Corretto, con un test verso
`203.0.113.1` (TEST-NET-3, RFC 5737: riservata alla documentazione e non instradata) **visto fallire** senza
la correzione.

Nello stesso giro, `Aurora_chiusa_produce_un_errore_leggibile` non usa più la porta 1 — «quasi certamente
libera» — ma una porta che il sistema assegna e che viene chiusa subito prima: su una macchina che filtra la
porta 1 invece di rifiutarla, quel test sarebbe rimasto appeso proprio al timeout appena corretto.

**3. `Directory.Build.props` — guardato, e ha trovato roba.**
Quattro asserzioni in `BuildConfigurationTests`. Su quella dell'XML sono onesto: se il file è illeggibile
MSBuild non valuta nemmeno il progetto di test, quindi quel test non gira — fallisce la build, che è già
rumorosa. Le altre tre portano il peso vero, perché una proprietà **cancellata** non rompe niente e nessuno
se ne accorge: `TreatWarningsAsErrors`, `RestorePackagesWithLockFile`, `NuGetAudit`/`NuGetAuditMode`.

La quarta — «ogni progetto ha il proprio `packages.lock.json`» — **ha trovato tre buchi al primo giro**: i
progetti in `tools/` stanno fuori da `Vipi.slnx`, quindi il restore della soluzione non li tocca e non
avevano lock file. Fra questi c'è **`Vipi.DbSeed`**, cioè lo strumento con cui si fa il travaso dei dati
verso MariaDB: il pezzo dove la riproducibilità conta di più. Generati e committati.

**Suite: 2100 test verdi** (net8 1109, net10 991).

### Seguito 2 — un'omissione mia, e le voci a11y/CSP

**Prima cosa: un'omissione.** Ricontrollando l'elenco, **M3 punto 1 — memoizzare `IsAdmin`** era nel piano
come «subito e a costo zero» e non era stata fatta. Nell'esito non compariva né fra le chiuse né fra le
rimandate: era semplicemente caduta.

**M3 punto 1 — fatta.** `EditAuthorizationService` è `Scoped` e risolve l'identità **una volta per scope**.
Ogni `ICurrentUserProvider.Get()` rilegge i claim e rifà il parse dell'array JSON `userStaffPositions`; le
pagine leggono `IsAdmin` dentro il markup, e `StrutturaPage` lo valuta sette volte per render — una delle
quali **dentro il `foreach` sui nodi**. Con ~300 callsign erano ~300 parse JSON a ogni ridisegno per
rispondere sempre la stessa cosa. Due test **visti fallire** senza la memoizzazione: **150 letture invece di
1**, e 50 invece di 1 per l'anonimo (il caso che si sbaglia per primo, se non si distingue «non ancora
chiesto» da «chiesto, e non c'è nessuno»).

Sicura perché lo scope è la richiesta HTTP o il circuito Blazor, e in un circuito l'identità **era già** di
fatto fissa: viene dall'`HttpContext` della richiesta di upgrade. Un login o un logout aprono un circuito
nuovo, quindi uno scope nuovo.

**D3 — chiusa, e la guardia ha smentito una mia affermazione.** Il commit del passo 4 diceva «le 12 chip di
filtro»: **erano 8**. Le quattro di `ConfinantiAdminPage` non erano state convertite, e me ne sono accorto
solo perché la guardia nuova le ha trovate. Convertite.

Sui 5 comandi che avevo lasciato indietro l'analisi ha cambiato il rimedio:

- **Le 3 celle di `AeroportiPage` non vanno rese focalizzabili.** Entrambe le tabelle hanno **già** una
  checkbox vera nella prima cella: il click sulla cella è una comodità per il mouse che duplica un comando
  già raggiungibile. Dandole il fuoco si otterrebbero **tre fermate di tabulazione per un solo toggle** —
  peggio, non meglio. Il difetto vero era un altro e non l'avevo visto: **quelle checkbox non avevano un
  nome**. Uno screen reader annunciava «casella di controllo, non selezionata» venti volte di fila senza
  dire di quale aeroporto — e quelle caselle comandano una cancellazione in blocco. Aggiunto `aria-label`
  con l'ICAO, e `aria-label` anche sulle due «seleziona tutto».
- **I 2 toggle di `StrutturaPage` sì**, quelli sono comandi veri senza equivalente da tastiera. Il toggle del
  nodo è diventato un `<button>` (con reset CSS, perché il browser gli mette di suo sfondo, bordo, padding e
  font); l'intestazione della scheda ACC ha preso `role="button"` + `tabindex` + Invio/Spazio — non un
  `<button>` perché contiene già codice, nome e conteggio, e un pulsante che avvolge tre elementi si annuncia
  leggendoli tutti di fila. Entrambi dichiarano `aria-expanded`, e c'è un `:focus-visible` visibile.

Tre guardie nuove in `StructureAccessibilityTests`, con whitelist **dichiarata** per le celle-comodità.

**CSP — metà, e dico quale metà.** `script-src` ha perso `'unsafe-inline'`: i due `<script>` inline di
`App.razor` sono diventati `vipi-zoom.js` e `vipi-boot.js`, e una guardia E2E pretende che la pagina non ne
contenga altri. È il pezzo che conta, perché è quello che rende innocuo uno script iniettato.

**Resta in sola segnalazione**, e non per dimenticanza. Misurato: **17 gestori inline nel markup**
(`onclick="window.print()"`, `ondragover="event.preventDefault()"`, `onclick="location.href=…"`) — sono
attributi HTML, quindi `script-src` li blocca esattamente come uno `<script>` inline — e **554 attributi
`style="…"`**, che tengono in piedi `style-src 'unsafe-inline'` (clausola molto meno grave della gemella
sugli script). Finché i 17 non spariscono, una CSP in vigore **senza** `unsafe-inline` romperebbe la stampa,
il drag&drop della struttura e tre elenchi; **con** `unsafe-inline` non proteggerebbe da niente. E il
passaggio va comunque fatto con un browser vero davanti: una CSP che entra in vigore rompe in modo vistoso,
e nessun test qui dentro apre una pagina davvero.

**Suite: 2111 test verdi** (net8 1115, net10 996).
