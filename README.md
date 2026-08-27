# vIPI / vLOA Interactive

Portale web interattivo per la documentazione operativa ATC (vIPI e vLOA) della divisione **IVAO Italia**.
Trasforma i Word statici in contenuto strutturato con due livelli (Estesa/Ridotta), logica di visibilità
live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

## Documentazione
- **`HANDOFF.md`** — stato corrente e come riprendere il lavoro (leggere per primo).
- **`docs/index.md`** — indice di tutta la documentazione (specifiche, guide, ADR, storia).
- **`docs/history/rounds.md`** — changelog cronologico dei round.
- Specifiche: `docs/spec/modello-dati.md`, `docs/spec/logica-aor.md`, `docs/spec/mappa-pagine.md`.
- Config & integrazione: `docs/guide/config.md`, `docs/guide/integration.md`. Decisioni: `docs/adr/`.

## Architettura (Clean Architecture — ADR-0001 D2, ADR-0002)

| Progetto | Ruolo | Dipende da |
|---|---|---|
| `src/Vipi.Domain` | Entità, enum, regole pure (`AiracService`). Nessuna dipendenza. | — |
| `src/Vipi.Application` | Use case e porte: `IAorService`, `IContentService`, `ICurrentUserProvider`. Logica AoR pura. | Domain |
| `src/Vipi.Infrastructure` | EF Core + SQLite (`VipiDbContext`), `TopologyBuilder`, migrazioni. | Application, Domain |
| `src/Vipi.Ui` | **RCL Blazor** montabile in-process nel sito host. Stili confinati in `.vipi-root`. | Application, Domain |
| `src/Vipi.Hosting` | **Superficie del modulo**: `AddVipiModule`/`UseVipiModule`/`MapVipiModule`/`MigrateVipiDatabase`, identità host, middleware, SSE, health. | Ui, Infrastructure, Application, Domain |
| `src/Vipi.Host` | Host Blazor Server di **sviluppo/esempio** che aggancia il modulo. | tutti |
| `src/Vipi.AuroraBridge.Contracts` | Contratto di filo dell'API del bridge Aurora (solo POCO, `net8.0;net10.0`). | — |
| `src/Vipi.AuroraBridge.Core` | Cuore del **tool desktop**: protocollo Aurora (TCP 1130), client del sito, orchestrazione, ViewModel. Nessuna UI. | Contracts |
| `src/Vipi.AuroraBridge` | Shell **Avalonia** del tool desktop (solo XAML e binding). | Core |
| `tests/*` | xUnit: AIRAC, scenari AoR S1–S10, editing, proiezione settori, import, bridge Aurora. | — |

Il **tool desktop** (ultime tre righe) sta fuori dalla regola di dipendenza del modulo: è un programma a sé che
gira sul PC del controllore e parla col sito via HTTP. Vedi `docs/guide/aurora-bridge.md`.

Regola di dipendenza verso l'interno: `Host → Infrastructure → Application → Domain`. La RCL e la logica
**non dipendono da tipi specifici dell'host** (ADR-0002 D5): l'identità arriva solo da `ICurrentUserProvider`.
In sviluppo (`useDevIdentity:true`) è attivo `DevCurrentUserProvider` (admin `IT-AOC`). Integrazione in `docs/guide/integration.md`.

## Build & run

```bash
dotnet build Vipi.slnx            # gli avvisi sono ERRORI (Directory.Build.props)
dotnet test  Vipi.slnx            # ~5980 test, su net8 e net10: entrambi i TFM girano
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /services/vsop
```

⚠️ **`dotnet test` non basta a dire «verde»**, per due ragioni distinte.

1. Non applica `TreatWarningsAsErrors`, quindi la suite può passare mentre la build di produzione è rotta —
   è già successo (1391 test verdi, 28 errori in CI).
2. Un progetto che **non compila** non produce nessuna riga di esito: contare i «Failed!» dà **zero** anche
   quando non è stato eseguito niente. È successo il 27 agosto 2026.

Perciò: **prima** `dotnet build Vipi.slnx -c Release --no-incremental`, e poi si controlla che i progetti con
esito siano **15** — non che i falliti siano zero.

ℹ️ Le dipendenze sono bloccate dai `packages.lock.json` committati. Se aggiungi un pacchetto, il restore
aggiorna il lock e va committato: la CI restora in «locked mode» e si ferma se il file non combacia.

ℹ️ **Il `publish` fa un passo in più del `build`**: `tools/Vipi.Assets` prepara la `wwwroot` del pacchetto —
toglie i commenti da CSS e JavaScript (erano il **44% dei byte spediti**) e lascia accanto a ogni file di
testo la variante `.br`/`.gz` già compressa alla qualità massima. Si spegne con
`-p:VipiOttimizzaAsset=false`, che serve a una cosa sola: guardare l'output di un publish con i file ancora
leggibili mentre si cerca un guasto. ⚠️ Se un file non è minificabile il publish **si ferma**: è quasi sempre
un errore di sintassi che nessun altro passo della build guarda.

Tool desktop Aurora (facoltativo, fuori dal sito): `./tools/publish-aurora-bridge.ps1` → eseguibile autonomo
in `artifacts/bridge/win-x64/`. Guida: `docs/guide/aurora-bridge.md`.

Il DB SQLite viene creato/migrato all'avvio dell'host (`Data Source=vipi.db`, override via `ConnectionStrings:Vipi`).

### Migrazioni EF Core
```bash
dotnet ef migrations add <Nome> \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure \
  -o Persistence/Migrations
```
(usa `DesignTimeDbContextFactory`; a runtime la connection string la fornisce l'host)

## Stato in breve
Solution a 4 layer + Host Blazor Server **net8** (multi-target `net8.0;net10.0` nelle librerie), **2111 test verdi**. Consultazione + editing + sicurezza dal DB;
live IVAO (polling + SSE); sorgente dati disaccoppiata; pagine su prefisso `/services/vsop`; **fonte unica = cataloghi**
(i `Sector` sono una proiezione, gerarchia di copertura per callsign cross-ACC, Round 20). **Bridge Aurora**:
tool desktop + endpoint `POST /vsop/api/v1/transfers/resolve` che propone il livello di trasferimento al
prossimo ente e lo scrive nel tag (fuso in `main`, ma l'endpoint nasce **spento**: `AuroraBridge:Enabled`
è `false` e senza quello la rotta non si registra affatto). Dettaglio completo
e prossimi passi in **`HANDOFF.md`**; storia in **`docs/history/rounds.md`**.

## Licenza
**Apache License 2.0** — vedi `LICENSE` e `NOTICE`. Stessa licenza del sito `Ivao.It`, così il prodotto
combinato resta sotto un regime unico quando il modulo viene embeddato (vedi `docs/guide/integration.md`).

I componenti di terzi ridistribuiti nel repository (three.js, i font Poppins, Nunito Sans e IBM Plex Mono) restano sotto la
propria licenza: elenco e testi in **`THIRD-PARTY-NOTICES.md`**. «IVAO» e il logo IVAO sono marchi
dell'International Virtual Aviation Organisation e la licenza non ne concede l'uso (§6 Apache 2.0).
