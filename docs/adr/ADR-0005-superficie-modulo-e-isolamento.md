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
- **D4 — Chrome opzionale.** La topbar del modulo è disattivabile via `Vipi:RenderTopbar=false`, per
  convivere con l'header del sito ospitante.
- **D5 — JS namespacing.** Le funzioni del modulo restano sotto il prefisso `vipi*` (namespace di
  fatto, collision-safe); non si toccano `window`/DOM globali oltre a quello.
- **D6 — Prefisso di rotta.** `/sop` resta fisso nelle `@page` (Blazor richiede letterali a compile-time);
  per path diversi si usa un reverse proxy. Documentato in `docs/INTEGRATION.md`.

## Conseguenze
- Integrazione su un host dello stesso stack a costo quasi nullo; identità reale dall'host.
- Nessun side-effect CSS sul sito ospitante; doppia topbar evitabile.
- Limite accettato: il prefisso `/sop` non è parametrizzabile a runtime (mitigato via proxy).
