# Audit full-stack — criticità back/front/DB (22 lug 2026) ✅

> **Chiusura (22 lug).** Fasi 1→3 (parte code) + minori B3/B4/C3/C4 **tutti attuati** + scaffolding provider
> Postgres (step 1/4); suite **447 verde** (6 progetti test). Restano solo attività **esterne** (montaggio RCL
> nel sito host, cutover Postgres, backplane) — vedi ADR-0007 e memoria `audit-2026-07-22-criticita`. Stato
> dettagliato in coda al piano.

Revisione senior dell'intero sito (backend · frontend · DB) a caccia di criticità, problemi con i dati e
difetti UI. Metodo: esplorazione a ventaglio (arch/DB/UI/sicurezza) + verifica su codice reale
(build 0 warning, **398 test verdi**: Domain 19 · App 205 · Infra 174). **Nessun fix applicato**: questo doc è
la carta d'ingresso al piano. Decisioni owner (22 lug): partenza **Fase 1 (rete sicurezza)**, target prodotto
**pubblico divisione (scala)**, output = questo documento versionato.

> Contesto: progetto maturo e disciplinato (Clean Arch 4 layer + Blazor Server, ADR/spec/refactor-process,
> porte sorgente-neutre, policy import, concorrenza ottimistica, lock, audit). Le criticità sotto sono di
> **rifinitura e rischio**, non falle strutturali.

## Sintesi severità

| # | Area | Criticità | Sev |
|---|------|-----------|-----|
| C2 | Front | Zero test UI/E2E → regressioni Blazor silenziose coi test verdi | ALTO |
| D1 | Sicurezza | Auth di produzione non montata (dev = admin onnipotente) | ALTO |
| A1 | Backend/DB | SQLite multi-utente: write-lock serializzato (rischio *database is locked*) | ALTO |
| A3 | Backend | Migrazioni pendenti / schema drift operativo (riavvio manuale) | ALTO |
| A2 | Backend | Blazor Server: scala orizzontale e resilienza circuito | MEDIO |
| A5 | Backend | Import periodici: fallimenti silenziosi senza superficie d'errore | MEDIO |
| B1 | DB | Soft-ref senza integrità (label denormalizzate → record "veri a metà") | MEDIO |
| B4 | Docs/DB | Spec §3–§5 storica vs §9 autorevole: rischio implementare su modello morto | MEDIO |
| C1 | Front | XSS: interpolazione raw di valori dinamici in MarkupString | MEDIO |
| D3 | Dati | Endpoint IVAO membri non confermato; mapping handler→callsign euristico | MEDIO |
| A4 | Backend | Sync-over-async a boot (DevIdentity chiama IVAO bloccante) | BASSO |
| B2 | DB | Enum→stringa senza check constraint (rinomina = righe perse in silenzio) | BASSO |
| B3 | DB | Nessun seed + reset dev frequenti → onboarding multi-step fragile | BASSO |
| C3 | Front | Stub UI (AoR SVG statico, aor3d) presentati come pagine reali | BASSO |
| C4 | Front | Markup in `RenderTreeBuilder`/stringhe (StrutturaPage) → fragile, fonte di C1 | BASSO |

## Dettaglio per area

### A — Backend / architettura
- **A1 [ALTO]** `vipi.db` singolo file: SQLite serializza le scritture. Editing staff + job import + polling live
  → contesa write-lock. *Sol:* WAL + `busy_timeout` come tampone; **piano Postgres** (target pubblico scelto),
  astrazione EF già presente.
- **A2 [MEDIO]** Blazor Server: circuito stateful + affinità server; SSE = 1 connessione/utente. Niente
  backplane documentato. *Sol:* definire scala attesa; Azure SignalR backplane o render Auto/WASM per i viewer
  pubblici read-only, Server solo sull'editing.
- **A3 [ALTO]** HANDOFF mostra azioni pendenti ricorrenti ("riavviare Host per applicare migrazione X").
  Codice/schema divergono finché non si riavvia a mano. *Sol:* health-check che fallisce su
  `GetPendingMigrations()` non vuoto; check CI modello==snapshot.
- **A4 [BASSO]** 3× `GetAwaiter().GetResult()` a boot (`DevCurrentUserProvider`, backfill, prune): accettabili
  (non su request path); `DevCurrentUserProvider` blocca su IVAO → timeout esplicito.
- **A5 [MEDIO]** Import gated + fallback in `try`: un fallimento lascia dati stantii senza segnale. *Sol:*
  esporre last-error per categoria `ImportState` in admin (badge rosso).

### B — Database / modello
- **B1 [MEDIO]** Soft-ref deliberati (`ConditionRefId`, `ParentCallsign`, `NextSector` SetNull): corretti per
  gli snapshot ma senza garanzia DB → label denormalizzate disallineabili (falla "propagazione" del runbook).
  *Sol:* job/report consistenza (ref orfani, label divergenti) + test. **Rilevare, non vincolare** (non
  aggiungere FK dove romperebbe gli snapshot pubblicati).
- **B2 [BASSO]** Enum→stringa senza check constraint: rinomina valore = righe vecchie non trovate. *Sol:* mai
  rinominare enum senza migrazione dati; test sui valori distinti a DB.
- **B3 [BASSO]** Nessun seed + reset dev → ripopolamento manuale multi-step. *Sol:* comando "bootstrap dev"
  idempotente riusando le fixture Roma.
- **B4 [MEDIO]** `modello-dati.md` §3–§5 storiche con banner "prevale §9". *Sol:* marcare `[SUPERATO]` o
  spostare in history; tenere viva solo la parte autorevole.

### C — Frontend / UI
- **C1 [MEDIO]** `StrutturaPage.razor` (~565/640) e `AeroportoPage.razor` (245) interpolano `Callsign`/`Label`/
  `FreqName` raw in `MarkupString` senza encode. Fonte cataloghi IVAO (rischio basso oggi), XSS stored se un
  Label diventa editabile. *Sol:* encodare i dinamici — pattern gemello già corretto in `SearchPage.Highlight`
  e `MarkdownLite`. (Nota buona: quei due encodano correttamente; `AorBlock` già corretto in passato.)
- **C2 [ALTO]** Solo test Domain/App/Infra. **Nessun bUnit, nessun E2E.** La memoria segnala "regressioni
  Blazor silenziose coi test verdi": oggi passerebbero i 398 test. *Sol:* progetto bUnit sui componenti critici
  (BlockRenderer, lock editor, DocReviewBar, render Live/Frozen) + 1 smoke E2E bozza→pubblica.
- **C3 [BASSO]** Mappe AoR / aor3d = SVG statico presentati come pagine. *Sol:* riusare `PreviewBanner`/badge
  "demo".
- **C4 [BASSO]** `StrutturaPage` costruisce markup in `RenderTreeBuilder`+stringhe (fonte di C1). *Sol:*
  estrarre sotto-componenti `.razor` dichiarativi.

### D — Sicurezza / operatività
- **D1 [ALTO]** Prod usa ancora `DevCurrentUserProvider` (VID 704798 admin). Adapter reali HostIdentity/OIDC +
  staff-code da montare **prima di ogni esposizione pubblica**.
- **D2 [OK]** Credenziali IVAO in user-secrets, `appsettings.json` vuoto → corretto; verificare env/secret store
  in prod.
- **D3 [MEDIO]** `/v2/divisions/{Code}/members` da confermare; mapping token-handler→callsign euristico
  (match-segmento) → dati trasferimenti live potenzialmente errati. *Sol:* confermare endpoint + tabella
  esplicita.

## Piano (ordinato per valore/rischio)

- **Fase 1 — rete di sicurezza [✅ ESEGUITA 22 lug]:** health-check migrazioni pendenti (A3) + osservabilità
  import last-error (A5) + progetti bUnit `Vipi.Ui.Tests` e E2E `Vipi.E2E.Tests` in-process (C2).
- **Fase 2 — correttezza dati [✅ ESEGUITA 22 lug]:** report consistenza soft-ref in `/vsop/admin/diagnostica`
  + health Degraded (B1); encode `HtmlEncode` in `StrutturaPage`/`AeroportoPage` (C1). Nessun cambio schema.
- **Fase 3 — produzione (target pubblico) [✅ parte code ESEGUITA 22 lug]:** tampone WAL+busy_timeout SQLite
  (A1) + guardia hard-fail identità dev in prod con test path prod `HostIdentityCurrentUserProvider` (D1);
  **cutover Postgres + scala Blazor pianificati in ADR-0007** (non attuati). Esterni residui: montaggio RCL nel
  sito host + config claim/staff-code IVAO (D1/D3), esecuzione cutover Postgres, backplane (A2).
- **Fase 4 — igiene:** snellire spec (B4), estrarre componenti StrutturaPage (C4), badge stub (C3), bootstrap
  dev (B3).

## Strategia anti-vibecoding (applicata, non reinventata)
Si seguono i gate esistenti (`FEATURE-PROCESS.md`, `REFACTOR-PROCESS.md`, regola di propagazione):
carta prima di codice · Regola del 2 / modello gemello · **propagazione nello stesso giro** (tipi, commenti/
`<see cref>`, doc d'area + `rounds.md`, spec, memorie) · **verify live** oltre a `dotnet test` (regressioni
Blazor silenziose) · `ValidationException` = `Vipi.Application.*` · scritture solo via service Application ·
DoD: test (unit + bUnit dove UI) → build 0 warning → propagazione → verify live → doc/memoria aggiornate.

---

## Riepilogo di chiusura (22 lug 2026) — fonte unica dello stato

Asse eseguito in questa sessione. Build **0 warning**, suite **398 → 447 test** (6 progetti test: erano 3).
Ogni criticità sotto è propagata in `rounds.md`, `HANDOFF.md`, ADR/spec/guide e memoria `audit-2026-07-22-criticita`.

| # | Criticità | Sev | Esito | Dove |
|---|-----------|-----|-------|------|
| C2 | Zero test UI/E2E | ALTO | ✅ Chiusa | progetti `Vipi.Ui.Tests` (bUnit) + `Vipi.E2E.Tests` (in-process) |
| A3 | Migrazioni pendenti / drift | ALTO | ✅ Chiusa | `VipiHealthCheck` → Unhealthy/503 |
| A5 | Import fallimenti silenziosi | MEDIO | ✅ Chiusa | `ImportState.LastError` + report `/vsop/admin/sorgenti` |
| B1 | Soft-ref senza consistenza | MEDIO | ✅ Chiusa (rileva) | `/vsop/admin/diagnostica` + health Degraded |
| C1 | XSS interpolazione raw | MEDIO | ✅ Chiusa alla radice | encode + estrazione componenti `Structure*` (C4) |
| A1 | Write-lock SQLite | ALTO | ⚙️ Mitigata + scaffolding | `SqliteTuningInterceptor` (WAL) · `Persistence:Provider` (step 1/4 Postgres) |
| D1 | Auth prod non montata | ALTO | ⚙️ Hardening + test | `ProductionIdentityGuard` (hard-fail) + test `HostIdentityCurrentUserProvider` |
| A2 | Scala Blazor | MEDIO | 📋 Pianificata | ADR-0007 D2 |
| D3 | Endpoint IVAO / euristica | MEDIO | 📋 Esterna | ADR-0007 (conferma host) |
| B4 | Spec storica vs autorevole | BASSO | ✅ Chiusa | §3 marcata `[SUPERATO]` |
| B3 | Onboarding dev fragile | BASSO | ✅ Chiusa | `guide/dev-bootstrap.md` |
| C3 | Stub UI | BASSO | ✅ Non-issue | aor3d già off; AoR block = editoriale |
| C4 | Markup in RenderTreeBuilder | BASSO | ✅ Chiusa | componenti `StructureCoverage`/`StructureFallbackChain` |

**Restano solo attività ESTERNE (richiedono ambiente/owner, non code-verificabili qui):**
1. Montare la RCL vIPI nel sito host + configurare `HostIdentity` coi claim/staff-code IVAO reali (D1/D3).
2. Completare il cutover **Postgres** su istanza reale (step 2→4 di ADR-0007 D1: pacchetto Npgsql, assembly
   migrazioni dedicato, revisione RowVersion/tipi, validazione).
3. Provisioning **backplane** + topologia di deploy + stima utenti concorrenti (A2).

**Sessione conclusa qui su richiesta owner.** Riferimenti: `../adr/adr-0007-produzione-persistenza-e-scala.md`,
`../guide/dev-bootstrap.md`, `../guide/config.md` §1c, memoria `audit-2026-07-22-criticita`.
