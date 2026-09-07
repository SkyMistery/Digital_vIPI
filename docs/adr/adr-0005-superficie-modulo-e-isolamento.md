# ADR-0005 — Superficie del modulo e isolamento dall'host

Stato: Accettato (2026-06-24). Estende ADR-0002 (integrazione e identità portabile).

## Contesto
La vIPI deve essere un **modulo agganciabile** a siti esistenti, non un'app da copiare a mano. Prima
d'ora il wiring (SSE, middleware, RCL, migrazioni, identità) viveva in `Vipi.Host/Program.cs`, e gli
stili/JS erano globali (rischio di collisione con la chrome dell'host). Inoltre l'adapter d'identità
host (scenari A/B) era documentato ma non implementato.

## Decisioni
- **D1 — Libreria di composizione `Vipi.Hosting`.** Espone `AddVipiModule(config, useDevIdentity)`,
  `UseVipiModule()`, `MapVipiModule()`, `MigrateVipiDatabase()` e `VipiModuleExtensions.UiAssembly`.
  L'host si aggancia in poche righe; il wiring interno non è più duplicato.
- **D2 — Identità host config-driven.** `HostIdentityCurrentUserProvider` legge il `ClaimsPrincipal`
  dell'host e lo mappa su `CurrentUser` tramite `HostIdentityOptions` (sezione `HostIdentity`):
  nomi dei claim configurabili, `StaffPositions` da claim multipli o array JSON. `DevCurrentUserProvider`
  resta solo per lo sviluppo (`useDevIdentity:true`).
- **D3 — Isolamento CSS.** Tutte le regole del tema sono confinate sotto il contenitore `.vipi-root`
  (wrapper in `SopLayout`). Reset e stili base non toccano `body`/`html` dell'host. L'host standalone
  imposta da sé il proprio reset di pagina.

  ⚠️ **Dal 7 settembre 2026 è vero, e prima non lo era.** La revisione del 6 settembre l'ha misurato
  (R-008): su 2 031 regole di `vipi-theme.css` ne erano confinate **48**, e le altre 1 983 (97,6%) stavano
  su selettori-radice con nomi da collisione garantita — `.wrap`, `.block`, `.pill`, `.toc`, `.topbar`, e
  perfino `details`, che è un selettore d'ELEMENTO. Su un host che carica il foglio con un `<link>` globale,
  come `integration.md` gli dice di fare, ogni `<details>` del **sito** cambiava aspetto.

  ⚠️ **Il confino si scrive `:where(.vipi-root)`**, non `.vipi-root`: `:where()` ha specificità zero,
  quindi i pesi relativi del foglio restano quelli di prima. Col prefisso nudo ogni regola guadagna una
  classe e il foglio si riordina da solo — misurato su otto pagine: sei cominciavano a scorrere in
  orizzontale, un'altezza cambiava di 255px, il carattere di una tabella passava da 14px a 16px.

  Restano **fuori** dal contenitore, e devono: `:root`, che porta le variabili, e `.vipi-rec*`, il riquadro
  della riconnessione — sta in `App.razor` fuori dal layout, perché Blazor cerca
  `#components-reconnect-modal` per nome e a circuito morto la pagina potrebbe non esserci.
- **D4 — Chrome opzionale.** La topbar del modulo è disattivabile via `Vipi:RenderTopbar=false`, per
  convivere con l'header del sito ospitante.
- **D6 — La superficie pubblica è una PROMESSA, e si stringe tipo per tipo.** Ogni tipo `public` di questo
  modulo è una promessa verso l'host che lo incorpora: toglierlo domani è una rottura. La revisione del
  6 settembre 2026 (R-009) ha contato **179 tipi pubblici che nessun file fuori dal loro progetto nomina**.

  Dal 7 settembre 2026 due progetti sono stati stretti:
  - **`Vipi.Infrastructure`**: da 38 candidati a **3**. Sono `internal` i diciassette servizi in background
    (`AddHostedService<T>` non chiede che siano pubblici: la registrazione avviene dentro l'assieme) e venti
    fra repository EF, client IVAO, provider del sectorfile e sonde — tutti dietro un'interfaccia.
    `Vipi.Infrastructure.Tests` vede gli `internal` via `InternalsVisibleTo`, come già tre altri progetti.
  - **`Vipi.Hosting`**: da 5 candidati a **3**.

  ⚠️ **Chi resta pubblico, e perché — sono le tre forme di «pubblico per una ragione che il nome non dice»:**
  1. `DesignTimeDbContextFactory`: la trovano gli **strumenti EF** per riflessione, e le migrazioni si
     generano da lì. Non vale il rischio.
  2. `IvaoAirportCache`, `ConsistencyReportCache`: compaiono nella **firma** di un tipo pubblico. Il
     compilatore lo dice da sé (CS0051), ed è il modo giusto di scoprirlo.
  3. `IvaoServiceCollectionExtensions`, `CoordinationSentenceOptions`, `DevIdentityOptions`: le prime si
     usano col nome del **metodo** (`services.AddVipiIvao(...)`), le altre le costruisce il **binder della
     configurazione** da un altro assieme. Un censimento che cerca il nome del TIPO non le vede mai usate.

  Nello stesso giorno sono stati stretti anche gli altri due:
  - **`Vipi.Ui`**: 78 tipi pubblici → **64**. Restano pubblici i modelli che compaiono in un `[Parameter]`
    di un componente (la classe che Razor genera è pubblica: un tipo nella firma di un componente è
    superficie quanto il componente) e quelli nella firma dei quattro loader che `Vipi.Hosting` registra.
  - **`Vipi.Application`**: dei 98 candidati ne sono passati **23**. Gli altri 75 non erano superficie
    inutile: sono raggiungibili **attraverso la firma** di un tipo pubblico, e il compilatore lo ha
    dimostrato uno per uno (CS0050/CS0051/CS0053/CS0703). Il numero del censimento sopravvalutava il
    margine, ed è bene saperlo prima del prossimo giro.

  ⚠️ **Il metodo, che vale più dei numeri.** Si propone in blocco con
  `python tools/censimento-pubblici.py <progetto>` — che **propone e non tocca** — poi si compila e si
  accetta il verdetto del compilatore tipo per tipo. Su 122 proposte, **cinque** erano sbagliate e le ha
  fermate lui, in tre forme che un censimento per NOME non può vedere:
  1. il tipo che compare nella **firma** di un tipo pubblico (CS0050/CS0051/CS0053/CS0703);
  2. la classe di **metodi di estensione**, che si usa col nome del metodo (`AddVipiIvao`,
     `kind.Severita()`) — successo **due** volte;
  3. il tipo costruito per **riflessione** da un altro assieme: la factory degli strumenti EF, e i tipi di
     **opzioni**, che li istanzia il binder della configurazione.

  ⚠️ E resta un censimento: quel che il compilatore non vede — gli `.xaml`, la riflessione, un tipo
  pubblico per una ragione che il nome non dice — lo decide chi legge, non lo strumento.

- **D5 — JS namespacing.** Le funzioni del modulo restano sotto il prefisso `vipi*` (namespace di
  fatto, collision-safe); non si toccano `window`/DOM globali oltre a quello.
- **D6 — Prefisso di rotta.** `/sop` resta fisso nelle `@page` (Blazor richiede letterali a compile-time);
  per path diversi si usa un reverse proxy. Documentato in `../guide/integration.md`.

## Conseguenze
- Integrazione su un host dello stesso stack a costo quasi nullo; identità reale dall'host.
- Nessun side-effect CSS sul sito ospitante; doppia topbar evitabile.
- Limite accettato: il prefisso `/sop` non è parametrizzabile a runtime (mitigato via proxy).
