# Feature — Immagini come blocco editoriale (upload da dispositivo e drag & drop)

Data: 2026-07-31 · Stato: **FATTO** (10 slice, suite 774 verde, verificato live) · Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md)

## Obiettivo

Ovunque oggi si possa aggiungere **Paragrafo / Callout / Tabella** si deve poter aggiungere anche una
**Immagine**: sezioni e sotto-sezioni di tutte le famiglie documentali (vIPI ACC, APP, vLOA, aeroporto)
e sezioni extra dell'aeroporto. Caricamento **scegliendo il file dal dispositivo** o con **drag & drop**.
La **dimensione massima** del singolo file è un **parametro di configurazione** modificabile in un solo posto.

## Stato di partenza (rilevato nel codice, 2026-07-31)

| Cosa | Dove | Nota |
|---|---|---|
| Formato blocco `Image` | `Vipi.Domain/Enums.cs:46` | **Esiste già** nell'enum `BlockFormat` |
| Resa immagine | `BlockRenderer.razor:29` | Placeholder `🖼️ Immagine (placeholder)` — mai stato implementato |
| Editor blocchi doc | `DocumentSectionsEditor.razor` | Prosa/Callout/Tabella su `IEditingService` (DB) |
| Editor blocchi extra | `DocumentBlocksEditor.razor` | Stessi tre formati, in-memory su `List<ExtraBlock>` |
| Modello extra | `Application/Content/ExtraBlockModels.cs` | `ExtraBlock` = Format + Text + CalloutKind + **TableJson** (specchio di `BodyJson`) |
| Rebuild aeroporto | `EfAirportRepository.cs:487` | `switch` su `blk.Format`: Callout / Table / Prose. **Nessun ramo per Image** |
| Snapshot release | `EfReleaseRepository.SnapshotWorkingAsync` | Serializza `RawDocument` (quindi `Body`/`BodyJson`) in `DocRelease.PayloadJson` |
| Ricerca | `EfSearchRepository.cs:90` | Indicizza `Body` **oppure `BodyJson` grezzo** |
| Upload / file statici utente | — | **Non esiste nulla**: nessun `InputFile`, nessun `byte[]`, nessuna tabella media |
| Schema Postgres | `PostgresSchemaReconciler.cs` | Allinea **colonne e indici**; le **tabelle** no (vedi rischio R1) |

## Pre-flight — 4 domande

**1. Modello — aggiungo un concetto o ne esiste già uno?**
Il *blocco immagine* esiste già (`BlockFormat.Image`): non si aggiunge un tipo di blocco, si **implementa
quello dichiarato**. Si aggiunge **un solo** concetto nuovo: l'**asset binario** (`MediaAsset`), che oggi non
ha alcun gemello. Il riferimento dal blocco all'asset viaggia in `BodyJson`, esattamente come già fa la
tabella — nessuna colonna nuova su `ContentBlock`, nessun secondo modello di contenuto.
Domanda di controllo: «dove si salva un'immagine?» → **un** posto (`MediaAssets`, dietro `IMediaStore`).

**2. Dispatch — switch per-tipo duplicato?**
Il `switch (Format)` per-blocco esiste già in 5 punti (2 editor, 2 anteprime read-only, `BlockRenderer`,
più il rebuild aeroporto). Questa feature **non ne aggiunge uno nuovo**: aggiunge un `case` a quelli esistenti,
e il corpo di ogni `case` è **una sola riga** che monta un componente condiviso —
`ImageBlockEditor` (editing) o `ImageFigure` (resa). La logica nuova sta in un posto solo.
Un registry di descrittori per-formato sarebbe la mossa giusta *se* si unificassero i due editor: è debito
già noto (memoria `airport-editor-qol-and-blocks`), **non** viene aperto qui — deciso esplicitamente.

**3. Ingressi + verifica**
Ingresso: pulsante **«+ Immagine»** accanto a «+ Paragrafo / + Callout / + Tabella», in entrambi gli editor →
compare in ogni sezione e sotto-sezione di ogni famiglia. Nessun catch-22 (non è un'entità raggiungibile da
una lista: nasce dentro un documento che esiste già).
Verifica: vedi §7 — guidando l'editor reale, non solo `dotnet test`.

**4. Propagazione — rimuove o rinomina qualcosa?**
Sì, una cosa sola: **sparisce il placeholder** `🖼️ Immagine (placeholder)` di `BlockRenderer`. Nello stesso
giro va aggiornata la guida in-app (`/vsop/guida#editor-blocchi`, che oggi elenca tre tipi di blocco) e
l'`HelpHint` di `DocumentBlocksEditor` che dice «Paragrafo, Callout, Tabella».

## Design

### 1. Dove stanno i byte — `MediaAsset` + porta `IMediaStore`

Nuova entità `Vipi.Domain/Entities/MediaAsset.cs`:

| Campo | Tipo | Perché |
|---|---|---|
| `Id` | int | PK |
| `Sha256` | string(64), **unique** | **Content-addressed**: identità = contenuto |
| `ContentType` | string | Derivato dai **byte**, mai dal client |
| `ByteSize`, `Width`, `Height` | int | `width`/`height` sull'`<img>` (niente layout shift), diagnostica |
| `Bytes` | byte[] (bytea/BLOB) | Il contenuto |
| `OriginalFileName` | string? | Solo per `Content-Disposition` e diagnostica |
| `CreatedUtc`, `CreatedByUserId` | | Tracciabilità |

Due invarianti che tengono in piedi il resto:
- **Immutabile**: i byte di una riga non si aggiornano mai. Stesso file caricato due volte → stesso sha →
  **una** riga (dedupe gratis).
- **Non si cancella dall'editing**: eliminare un blocco immagine **non** tocca `MediaAssets`, perché uno
  **snapshot di release** già pubblicato può ancora citare quello sha (vedi R2).

Porta `Vipi.Application/Abstractions/IMediaStore.cs` (`SaveAsync(Stream, fileName, ct) → MediaRef`,
`GetAsync(sha, ct) → MediaContent?`), impl `EfMediaStore` in Infrastructure. La porta esiste perché domani i
byte possano andare su object storage (R2/S3) **senza toccare editor e viewer**: cambia solo la registrazione DI.

### 2. Come il blocco cita l'immagine

`BodyJson` del blocco (Format = `Image`):

```json
{ "mediaId": "<sha256>", "alt": "Testo alternativo", "width": 1600, "height": 900 }
```

`Body` = **didascalia** (markdown-lite, come la prosa). Un solo helper `Vipi.Application/Content/MediaRef.cs`
(`Parse` / `Serialize`) è la **fonte unica** del formato: lo usano i due editor, il viewer, il rebuild
aeroporto e la ricerca. `ExtraBlock` guadagna il campo `ImageJson` — stessa stringa, stesso helper — replicando
il precedente già in casa (`TableJson` specchia `BodyJson`): **nessuna migrazione DB**, gli extra sono JSON dentro
`AirportExtraSection.Body`.

### 3. Servire l'immagine — `GET /vsop/media/{sha256}`

Endpoint in `MapVipiModule` (accanto a `/vsop/live/atc`), pubblico come i documenti che lo citano:
- `Cache-Control: public, max-age=31536000, immutable` + `ETag` = sha — legittimo perché l'URL **è** il contenuto;
- `Content-Type` = quello rilevato dai byte, `X-Content-Type-Options: nosniff`,
  `Content-Disposition: inline`;
- 404 se lo sha non esiste (release vecchia che cita un asset mai esistito → figura mancante, non 500).

### 4. Validazione (server, sempre — anche se il client ha già filtrato)

Nel servizio Application, con `Vipi.Application.*.ValidationException` (**mai** DataAnnotations: la UI non
le cattura → crash del circuito):
1. `byte ≤ Media:MaxUploadBytes` → «L'immagine supera il limite di N MB»;
2. **magic bytes**: PNG / JPEG / WebP / GIF. Il `Content-Type` dichiarato dal browser si ignora.
   **SVG escluso** (può contenere script → XSS servito dal nostro dominio);
3. **dimensioni in pixel** lette dall'header (parser piccolo e deterministico → *test-first*), rifiuto oltre
   `Media:MaxImagePixels` per lato: blocca le "decompression bomb" senza decodificare l'immagine.

### 5. UI — `ImageBlockEditor.razor` (unico, condiviso dai due editor)

- **Dropzone**: `<label class="img-drop">` con `<InputFile>` sovrapposto trasparente che copre l'area →
  **click** (scelta dal dispositivo) e **drop** funzionano entrambi nativamente; gli eventi Blazor
  `@ondragenter/@ondragleave/@ondrop` servono solo per lo stato visivo «rilascia qui».
  *Punto fragile, da provare live su Edge/Chrome/Firefox* (§7).
- **Ridimensionamento nel browser** (`wwwroot/vipi-media.js`): canvas → lato lungo ≤
  `Media:ClientDownscaleLongestSidePx`, ricodifica JPEG/WebP a `Media:JpegQuality`, e restituisce un
  `IJSStreamReference` che .NET legge con `OpenReadStreamAsync(maxAllowedSize)`. Così una foto da telefono da
  8 MB **passa** (arriva già ridotta), l'upload viaggia dentro il circuito già autenticato (nessun endpoint di
  scrittura, nessun antiforgery da gestire) e **non serve nessuna libreria di imaging server-side**.
  Fallback se il browser non decodifica il file: `InputFile.OpenReadStream(max)` sull'originale.
- Dopo l'upload: anteprima, campo **alt** (accessibilità e stampa) e **didascalia**, pulsanti
  *Sostituisci* / *Rimuovi*. ↑/↓ ed elimina-blocco restano quelli dell'host.
- Messaggio d'errore che **cita il limite corrente** letto dall'opzione, non un numero scritto a mano.

### 6. Resa — `ImageFigure.razor` (unico, condiviso da viewer ed editor)

```html
<figure class="doc-img"><img src="/vsop/media/{sha}" alt="…" width height loading="lazy"><figcaption>…</figcaption></figure>
```

Usato da: `BlockRenderer` (case `Image`, al posto del placeholder), le due anteprime read-only degli editor,
il viewer aeroporto. Un solo markup ⇒ documento, editor, anteprima release e stampa non possono divergere.

### 7. Il parametro richiesto — `MediaOptions`

`appsettings.json`, sezione `Media`, registrata in `AddVipiModule` accanto alle altre `services.Configure<…>`:

```json
"Media": {
  "MaxUploadBytes": 3145728,
  "MaxImagePixels": 12000,
  "ClientDownscaleLongestSidePx": 2000,
  "JpegQuality": 0.85
}
```

`MaxUploadBytes` è letto da **un solo** posto e usato in quattro: testo d'aiuto nella UI, limite di
`OpenReadStreamAsync`, controllo server, messaggio d'errore. Cambiarlo = cambiare quel numero (o la env var
`Media__MaxUploadBytes` su Render, senza redeploy del codice).

## Rischi e punti d'attenzione

**R1 — Una tabella nuova non nasce su Neon.** In produzione lo schema si crea con `EnsureCreated`, che **non
tocca un DB che ha già tabelle**; `PostgresSchemaReconciler` allinea **colonne e indici**, non le tabelle. Quindi
`MediaAssets` esisterebbe in locale (SQLite `Migrate()`) e **no** in produzione: primo upload → `42P01 relation
does not exist`. **Passo 1 obbligatorio**: `EnsureModelTables` nel reconciler (diff modello ↔
`information_schema.tables` → `CREATE TABLE` generato da `IMigrationsModelDiffer` + `IMigrationsSqlGenerator`).
Vale per **ogni entità futura**, non solo questa.

**R2 — Immutabilità verso le release.** Il payload di release cita lo sha: se un asset sparisse, una release
pubblicata mostrerebbe un buco. Regola: l'editing non cancella mai un `MediaAsset`. Un'eventuale GC (scansione
di documenti + payload release) è **fuori scope**, annotata come possibile azione admin futura.

**R3 — Ricerca inquinata.** `EfSearchRepository` indicizza `BodyJson` grezzo quando `Body` è vuoto: un blocco
immagine finirebbe nei risultati come stringa JSON. Fix **nello stesso giro**: per `Format=Image` si indicizzano
solo **alt + didascalia**.

**R4 — Concorrenza `DbContext`.** L'upload gira dentro il circuito: il servizio media va risolto dallo **scope
proprio** (`OwningComponentBase`, come già fa `DocumentSectionsEditor` per `IEditingService`) o via
`IDbContextFactory` — mai dal context condiviso, o torna «a second operation was started on this context»
(memoria `blazor-dbcontext-concurrency`).

**R5 — Trasporto SignalR.** Il default `MaximumReceiveMessageSize` è 32 KB; lo streaming JS→.NET è a chunk
quindi passa, ma **va cronometrato live** su un file al limite: se l'upload risulta lento, si alza il valore
in `AddInteractiveServerComponents`.

**R6 — Peso sul DB.** 3 MB × N immagini sullo stesso Postgres dei documenti (Neon free ≈ 0.5 GB). Il downscale
client tiene i file veri sotto i ~300–500 KB; la porta `IMediaStore` è la via d'uscita se un domani serve.

## Passi (slice verticali, 1 commit ciascuna, build verde a ogni passo)

1. **Schema**: `PostgresSchemaReconciler.EnsureModelTables` + test. *(sblocca tutto il resto, utile di per sé)*
2. **Dominio + storage**: `MediaAsset`, migrazione EF SQLite, `IMediaStore`/`EfMediaStore`, `MediaOptions`,
   validazione (magic bytes + header dimensioni) — **test-first** sul parser.
3. **Endpoint** `GET /vsop/media/{sha}` con header cache/sicurezza + test d'integrazione.
4. **Modello blocco**: `MediaRef.Parse/Serialize`, `ExtraBlock.ImageJson`, normalizzazione + test.
5. **Resa**: `ImageFigure.razor`, `BlockRenderer` case `Image`, CSS tema + `vipi-print.css`
   (`break-inside: avoid`, altezza massima in stampa) + test bUnit in `BlockRenderingTests`.
6. **Editing**: `ImageBlockEditor.razor` (dropzone + InputFile + downscale JS + alt/didascalia) e «+ Immagine»
   in `DocumentSectionsEditor` **e** `DocumentBlocksEditor`; anteprime read-only via `ImageFigure`.
7. **Aeroporto**: ramo `Image` nel rebuild (`EfAirportRepository`) — senza, l'immagine di una sezione extra
   sparirebbe in silenzio dal documento pubblicato — e resa nel viewer aeroporto.
8. **Ricerca**: alt + didascalia invece del JSON (R3).
9. **Guida e stringhe**: `/vsop/guida#editor-blocchi`, `HelpHint`, resx it/en.
10. **Chiusura**: questo doc a FATTO, `history/rounds.md`, memoria.

## Verifica (DoD)

- `dotnet test` ≥ baseline (663) con i nuovi test: parser header, `MediaRef`, endpoint, `ImageFigure`, rebuild aeroporto.
- **Live** (skill `verifica-live`), con traccia:
  1. immagine in una sezione **e** in una **sotto-sezione** vIPI ACC, scelta da file;
  2. stessa cosa con **drag & drop** (il punto fragile: input sovrapposto);
  3. immagine in una **sezione extra aeroporto** → Rebuild → compare nel documento;
  4. **Pubblica** → la vista pubblica e l'anteprima release mostrano l'immagine; poi **Stampa** (anteprima);
  5. file da **oltre il limite** → rifiuto con messaggio che cita il limite; `.txt` rinominato `.png` → rifiuto;
  6. cancellare il blocco **non** rompe la release già pubblicata.

## Esito della verifica live (2026-07-31)

Guidata su Edge+puppeteer sull'editor vIPI ACC di Brindisi, con una copia del `vipi.db` reale. Quattro difetti
trovati **con la suite verde**, tutti corretti nello stesso giro (commit `b5da9a3`):

1. **Il ridimensionamento nel browser non partiva mai, in silenzio.** Il disegno iniziale (JS che restituisce i
   byte a .NET come `IJSStreamReference`) non funziona qui per due motivi indipendenti: quando .NET richiama il
   JS, `input.files` è **già stato svuotato** da `InputFile`; e uno stream creato dentro una funzione `async`
   arriva a Blazor senza il blob dietro (`Supplied value is not a typed array or blob`). Riscritto: si intercetta
   il `change` in fase di **cattura**, si ricodifica, si ri-emette l'evento col file già rimpicciolito. Blazor ne
   vede uno solo, col file giusto, e il C# non tocca più JS per l'upload.
   *Provato*: 4000×3000 → 2000×1500; un PNG rumoroso da 20 MB entra come WebP invece di essere rifiutato.
2. **Alt e didascalia uscivano duplicati a metà parola**: salvare un campo fa ricaricare il documento all'host, e
   il render successivo riscriveva nell'altro campo il valore di prima. I due campi ora vivono nel componente.
3. **Due frasi diverse per lo stesso rifiuto** (una localizzata, una no) a seconda che il limite scattasse sul
   server o sul trasporto. Ora è una sola, e non cita una dimensione che non è quella rifiutata.
4. **In stampa le immagini sotto la piega uscivano vuote** (2×2 px): sono `loading=lazy` e nessuno le caricava.
   Il gestore `beforeprint` che apre i `<details>` ora le passa a `eager`. Misurato: 416 px = il tetto di 110 mm.

Verificato inoltre: immagine in una sezione **e** in una sotto-sezione; scelta da file **e** rilascio sull'area;
rifiuto di un file oltre il limite e di un `.txt` rinominato `.png`; `/vsop/media/{sha}` con
`public,max-age=31536000,immutable` e il tipo dedotto dai byte (`image/png`, `image/webp`); nessun errore di
console o di circuito.

## Non-obiettivi (dichiarati)

SVG; galleria/carousel; crop/rotazione/annotazioni; immagini **dentro le celle** di tabella; picker per
riusare un'immagine già caricata (il dedupe c'è, la libreria media no); quota per documento; GC degli asset orfani.
