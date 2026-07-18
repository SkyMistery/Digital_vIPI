# 10 — Snapshot totale + RenderMode per sezione 🟢

> Estende l'asse di pubblicazione (doc [09](09-flusso-pubblicazione.md)). Cambia la **semantica di
> congelamento** della release: da *freeze parziale* (solo sezioni statiche; derivate sempre live)
> a **snapshot totale** — al Pubblica ora / Programma si congela una **copia statica completa** del
> documento (derivate incluse). Le modifiche restano visibili **solo nell'editor** finché non si
> ripubblica. Eccezione governata da un flag **per-sezione** (`RenderMode`), non da regole hardcoded.
>
> **Stato: carta approvata (Fase 0) — esecuzione da avviare.** Dipende da: doc 08, doc 09.

## 1. Stato attuale (post-09, 2026-07-18)

Il freeze oggi è **parziale**. `DocReleasePayload.Doc` (`RawDocument`) congela solo le sezioni
**statiche**; le sezioni **derivate** (AoR / frequenze / coordinamenti / config / separazioni) si
renderizzano **live** dai cataloghi correnti al momento del view. Conseguenze rilevate:

1. **Freeze incoerente.** Una release "congelata" mostra prose statiche fotografate ma derivate che
   cambiano quando cambiano i cataloghi IVAO / la gerarchia / i trasferimenti. Il pubblico vede un
   documento metà-fermo metà-vivo.
2. **Overlay snapshot morto.** `EfReleaseRepository.SnapshotWorkingAsync` scrive `payload.Vloa`
   (`VloaOverlaySnapshot`: `HiddenAorSectors`/`HiddenFrequencies`) per vLOA e App, ma **nessuno lo
   rilegge**. La visibilità (settori/frequenze nascosti) è sempre presa **live** dal `DocumentProfile`
   corrente (`VloaDerivationService`), mai dallo snapshot. `IReleaseTarget.IncludesVisibilityOverlay`
   e tutto il ramo overlay sono di fatto codice morto.
3. **Aeroporto già "cotto".** L'aeroporto NON ha derivate live: `AirportEditingService.RebuildDocumentAsync`
   **materializza** piste/TA/frequenze/**SID** dentro l'albero `Document`. Il viewer
   (`BuildAirportVipiAsync(..., live:false)`) legge il documento cotto. Le SID importate dal sectorfile
   NON si aggiornano finché non si fa Rebuild **e** si ripubblica.
4. **Due path viewer**: ACC via `AccDocumentService.LoadForViewAsync`; vLOA/App/Airport via
   `EfContentRepository.LoadVipiAsync`. Entrambi leggono `GetEffectiveAsync` ma applicano regole di
   fallback live proprie.
5. **Nessun controllo per-sezione.** "Live vs congelato" è una proprietà implicita del tipo di sezione,
   non una scelta editoriale.

## 2. Motivazione

- **Un solo significato di "pubblicato":** ciò che il pubblico vede è una **fotografia** deliberata,
  non uno stato vivo che deriva mentre nessuno guarda. Le bozze/modifiche vivono nell'editor.
- **Eliminare il dead code** (overlay snapshot) unificando il congelamento: se congelo *tutto*
  l'output, la visibilità è già dentro la fotografia — l'overlay separato non serve.
- **Flessibilità editoriale governata:** alcune sezioni traggono valore dall'essere sempre aggiornate
  (dati di riferimento IVAO, es. **SID**). Serve un interruttore **per-sezione**, non un'eccezione
  hardcoded per famiglia.

## 3. Architettura target (APPROVATA) 🟢

Modello: **"snapshot totale + allowlist live esplicita, per-sezione"**.
Default: al publish si congela **tutto** (statico + derivate editoriali). Le eccezioni live sono
dichiarate da un flag per-sezione, più un piccolo insieme di overlay runtime mai congelabili.

### 3a. `RenderMode` per sezione
Nuovo enum `RenderMode { Frozen, Live }`, campo su **`DocumentSection`** (uniforme per tutte e 4 le
famiglie: ognuna ha un `Document` con sezioni — un solo posto, nessuna dipendenza da `DocumentProfile`
vs blockmeta che oggi divergono).

- **Solo le sezioni *derivabili*** espongono il flag (vedi §3b). Le sezioni statiche (prose scritte a
  mano) sono sempre `Frozen`: "live" non ha significato per un blocco redazionale.
- **Default `Frozen`**, tranne la sezione **`sids`** → default **`Live`**.
- **L'editor mostra SEMPRE lo stato di lavoro live**; solo la **vista pubblica** rispetta `RenderMode`.

### 3b. Registry delle sezioni derivabili — `IFrozenSectionProvider`
Porta Application, una impl per famiglia, iterata dai motori (cattura + view). Dichiara **quali**
`SectionKey` sono derivabili e sa **derivare + serializzare** l'output di ciascuna.

- Derivabili per famiglia (da confermare puntualmente in Fase 2, test-first):
  - **ACC / App:** `aor`, `frequencies`, `coordination`, `configurations`, `separations`, `minima`.
  - **vLOA:** `aor`, `frequencies`.
  - **Airport:** `sids`.
- Due fatti distinti e non in conflitto: *quali sezioni sono derivabili* = costante per-famiglia
  (descrittore); *quale `RenderMode` ha questa sezione in questo doc* = dato per-documento
  (`DocumentSection.RenderMode`).

### 3c. Payload esteso, overlay rimosso
`DocReleasePayload` porta, oltre a `Doc`, i **view-model renderizzati** delle sezioni congelate
(l'**output**, non i dati grezzi → immune ai cambi cataloghi): frequenze / AoR (callsign + poligoni +
colori + selezioni config) / coordinamenti / config-table / separazioni / vfr / minima.

- Ogni poligono `FrozenAor` **porta il callsign** del settore, così l'overlay runtime online/offline si
  mappa sui poligoni congelati (§3d).
- **Rimossi:** `VloaOverlaySnapshot`, `DocReleasePayload.Vloa`, `IReleaseTarget.IncludesVisibilityOverlay`
  e il ramo overlay in `SnapshotWorkingAsync` (assorbiti: la visibilità è dentro la fotografia).

### 3d. Cattura al publish + dispatch al view
- **Publish:** i motori snapshot iterano le sezioni della versione di lavoro; per ogni sezione
  derivabile con `RenderMode == Frozen` invocano il derivation service via `IFrozenSectionProvider` e
  serializzano l'output nel payload. Le sezioni `Live` **non** vengono catturate.
- **View (release effettiva):** per sezione, se presente nel payload congelato → render dal frozen
  (zero chiamate ai derivation service); altrimenti → render live. **Sempre live, non congelabile**
  (overlay runtime, non editoriale): stato online dei settori, meteo / pista in uso — applicati *sopra*
  i poligoni/le tabelle congelate.
- Entrambi i path viewer (`AccDocumentService`, `EfContentRepository`) leggono lo **stesso** payload.

### 3e. SID aeroporto de-cotta (l'eccezione collassa nel meccanismo)
`RebuildDocumentAsync` smette di **cuocere** la SID nell'albero: `sids` diventa una **sezione
derivabile** (default `RenderMode.Live`). La logica di merge SID editoriali (`SidRow`) + importate
(`ImportedSid`) si sposta da rebuild-time a una **derivazione a view-time** (un solo posto), esposta
via `IFrozenSectionProvider` come le altre. Così l'aeroporto non ha più un ramo speciale: è "un
documento con la sezione `sids` in Live di default".

### 3f. Visibilità pubblica e migrazione
- **Visibilità pubblica = esiste una `DocRelease` effettiva** (`GetEffectiveAsync`). Rimosso il
  fallback "render live della versione `Published`" nei path pubblici. `Document.Status` resta per
  liste/editoriale; i gate lista che usavano `Status == Published` passano al gate "ha release
  effettiva".
- **Migrazione A (backfill una tantum):** allo switch, per ogni documento `Status == Published` senza
  release effettiva si genera una copia statica al ciclo AIRAC corrente (riusa il path di cattura §3d).
  Nessun buco pubblico. Idempotente. Set dei default `RenderMode` sulle sezioni esistenti
  (`Frozen`, `sids` `Live`).

### Non-obiettivi
- NON si tocca il doppio strato `DocumentVersion` (lifecycle bozza) vs `DocRelease` (snapshot AIRAC).
- NON si cambia la matematica AIRAC né la timeline release (Scheduled/Effective/Superseded).
- NON si toccano le rotte pagina (solo il *cosa* renderizzano).

### Caveat accettato — AIRAC misto in pagina
Una sezione `Live` in un documento per il resto congelato mostra dati di un ciclo diverso dal resto
(es. aeroporto congelato 2606 + SID importate 2607 nella stessa pagina). È il costo intrinseco
dell'eccezione live, accettato consapevolmente (le SID sono dati di riferimento IVAO, non editoriali).

## 4. Passi di migrazione (APPROVATO) 🟢

Slice verticali, 1 commit/passo, `dotnet build` verde a ogni commit, meccanico separato da logica.

- **S0 — Carta.** Questo doc + riga indice `00-overview`. Zero codice. *(questo passo)*
- **S1 — Test-first di caratterizzazione.** Baseline del comportamento live attuale per famiglia;
  test target: sezione `Frozen` invariante ai cambi catalogo, sezione `Live` che li riflette.
  Estende `ContentReleaseVisibilityTests` / `ReleaseRepositoryTests`.
- **S2 — Modello dati (meccanico).** `RenderMode` su `DocumentSection` (migration, default `Frozen`);
  `DocReleasePayload` esteso coi frozen view-model. Nessun consumo ancora.
- **S3 — Cattura al publish.** Registry `IFrozenSectionProvider` per famiglia; `SnapshotWorkingAsync`
  (o la cattura spostata in `ReleaseService`, dove i derivation service sono iniettabili) cattura una
  sezione **solo se `Frozen`**.
- **S3b — SID de-cotta.** `RebuildDocumentAsync` non cuoce più `sids`; sezione derivabile, default
  `Live`; merge editoriali+importate → derivazione a view-time.
- **S4 — Viewer da frozen.** Per sezione: frozen-in-payload → payload, sennò live; overlay runtime
  sempre live sopra. Entrambi i path.
- **S4c — Editor: toggle + badge.** Controllo `Live`/`Frozen` per sezione derivabile nei 4 editor
  (ACC/App/vLOA/Airport), persistenza su `RenderMode`, badge di stato per sezione.
- **S5 — Rimozione (propagazione).** Drop `VloaOverlaySnapshot`/`payload.Vloa`/`IncludesVisibilityOverlay`
  + fallback live pubblico. Visibilità = release effettiva. Aggiorna commenti/`<see cref>`, docs
  (08e, 08e-acc, 09, `spec/modello-dati.md`), memorie — **nello stesso giro**.
- **S6 — Migrazione A.** Backfill copia statica per i `Published`; set default `RenderMode` sulle
  sezioni esistenti.
- **S7 — Verify live + chiusura.** Guida il flusso reale per le 4 famiglie × {`Live`,`Frozen`} con
  traccia; `history/rounds.md` + indice `00-overview` + memorie coerenti.

## 5. Impatto / Verifica

- **Schema DB:** cambia (`DocumentSection.RenderMode` + payload esteso) → snapshot pre/post in
  `spec/modello-dati.md` (Fase 1/4).
- **Verifica obiettivo utente (Fase 3, live con traccia):** per ogni famiglia — pubblico; poi cambio un
  catalogo / una freq / un hidden; confermo che il **pubblico non cambia** e l'**editor sì**; ripubblico
  e il pubblico si allinea. Per l'aeroporto: importo nuove SID → la sezione `sids` (Live) si aggiorna
  **senza** ripubblicare, mentre il resto resta fermo. Toggle una sezione ACC/App su `Live` → si comporta
  come le SID.
- **Regressioni Blazor silenziose** (attributi `string` senza `@`, flussi `EnsureAsync`/lock/bozza):
  verify live obbligatoria, non basta `dotnet test`.
- **`ValidationException`:** `Vipi.Application.*.ValidationException`, mai DataAnnotations.
- **Logging:** nessuno swallow silenzioso introdotto nei nuovi provider/derivazioni.

## 6. Gate FEATURE-PROCESS (pre-flight)

- **Modello (no gemello):** `RenderMode` sulla sezione = un posto per tutte le famiglie; il payload
  resta l'unico contenitore snapshot. La visibilità overlay separata viene **rimossa**, non affiancata.
- **Dispatch (Regola del 2):** cattura + view iterano `IFrozenSectionProvider` (registry), nessun
  `switch(tipo)` nuovo. L'eccezione SID è dato, non ramo.
- **Ingressi + verifica:** toggle negli editor esistenti (nessun catch-22); verifica live definita in §5.
- **Propagazione:** S5 rimuove overlay/fallback e aggiorna nomi/commenti/doc/memorie nello stesso giro.
