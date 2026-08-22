---
name: verifica-live
description: Lancia la vIPI in locale (dotnet run su una copia del DB) e la guida in un browser reale con Edge+puppeteer-core, per verificare a schermo una modifica UI. Usare quando serve provare l'app davvero — non i test — su viewer, editor, pannello release.
---

# Verifica live della vIPI

Il runbook (`docs/REFACTOR-PROCESS.md`, `docs/FEATURE-PROCESS.md`) chiede di guidare il flusso reale perché
**le regressioni Blazor sono silenziose con i test verdi**. Questa è la procedura che funziona su questa macchina.

## 1. Copia il DB (non lavorare sul `vipi.db` del progetto)

L'app scrive: pubblicare o annullare una release durante la verifica modificherebbe i dati di sviluppo.
Copiare **anche `-wal` e `-shm`** (il DB è in WAL mode, ADR-0007): senza, la copia perde le ultime scritture.

```powershell
$sc = "<scratchpad>\live"
New-Item -ItemType Directory -Force -Path $sc | Out-Null
Get-ChildItem "src\Vipi.Host" -Filter "vipi.db*" | Copy-Item -Destination $sc -Force
```

## 2. Avvia

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:VipiAuth__Enabled      = "false"                        # OBBLIGATORIO, vedi sotto
$env:ConnectionStrings__Vipi = "Data Source=$sc\vipi.db"
$env:ASPNETCORE_URLS        = "http://localhost:5034"
$env:Sectorfile__RawBaseUrl = ""                             # spegne l'import SID da GitHub
dotnet run --project src/Vipi.Host --no-launch-profile
```

Lanciare in background e attendere la riga `Now listening on`.

**Perché `VipiAuth__Enabled=false`**: `appsettings.Development.json` porta `VipiAuth:Enabled=true`, e
`Program.cs` calcola `useDevIdentity = IsDevelopment && !authEnabled`. Con la config di default l'app pretende
il **login OIDC IVAO reale** e l'identità di sviluppo non si attiva: non entri. Con l'override parte
`DevCurrentUserProvider` (VID 704798); senza credenziali IVAO nei user-secrets cade sul fallback statico
(staff `IT-AOA1`/`IT-T03`, `CanEdit=true`), che basta per editare.

`Sectorfile__RawBaseUrl=""` evita che il job d'avvio richiami GitHub: la verifica non deve dipendere dalla rete.

⚠️ **Se invece di `dotnet run` si avvia un exe PUBBLICATO** (quando i `bin/` sono bloccati dall'app di chi
lavora), lanciarlo **dalla sua cartella**: la content root e' la directory corrente. Avviato da altrove l'app
parte e risponde 200, ma serve la pagina **senza CSS ne' JS** (`_content/...` in 404, «MIME type ""») — e una
misura di densita' presa cosi' e' la misura di una pagina nuda.

## 3. Guida il browser

Su questa macchina **non** ci sono Chrome, Playwright né `chromium-cli`. Ci sono Node ed **Edge**:

```powershell
# nello scratchpad, NON nel repo (niente node_modules committati)
cd $sc; npm init -y; npm install puppeteer-core
Copy-Item "<repo>\.claude\skills\verifica-live\driver.js" $sc
node driver.js
```

`driver.js` accanto a questo file è il punto di partenza: adattarne la sezione dei passi.
Edge sta in `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`.

Accanto a `driver.js` ci sono altri **due** script, che non si adattano: si lanciano così com'è.

| script | cosa prende | quando |
|---|---|---|
| `sweep.js` | fondi che **non si sono girati** nel tema scuro (12 pagine) | dopo ogni modifica a un foglio di stile |
| `probe.js` | testo sotto **4.5:1** su una pagina, in un tema | quando si cambia un colore di testo |

```powershell
node sweep.js
node probe.js dark  http://localhost:5034/vsop/lirr
node probe.js light http://localhost:5034/vsop/lirr
```

⚠️ **Perché esistono.** Il 22 agosto `vipi-aor3d.css` è **sfuggito** alla passata sui token: la legenda del
visore 3D è rimasta col fondo bianco scritto a mano e, nel tema scuro, aveva le scritte quasi bianche
sopra — illeggibile. **Nessun test lo ha visto**, e nel tema chiaro non si vedeva. `driver.js` non l'ha
preso perché guardava solo cinque pagine, e quella non era fra loro. `sweep.js` lo prende.

⚠️ Tutt'e due hanno **falsi positivi noti e attesi**, scritti in testa ai file: il velo semitrasparente
della barra blu (`probe.js` non compone l'alfa) e la pastiglia ACC attiva (`sweep.js` non riesce a
riconoscerla come voluta). Leggere quelle righe prima di «correggere» qualcosa che non è rotto.

## 4. Attendi il circuito, non il DOM

**La regola più importante.** Le pagine sono `@rendermode InteractiveServer`: la prima risposta HTTP è il
**prerender**. Uno smoke test che vede `200` non prova nulla — passa anche con la pagina rotta.

```js
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });  // 90s: dopo un riavvio il JIT è lento
await page.waitForSelector('#p-release', { timeout: 60000 });
await sleep(2500);                                                      // derivazioni async, render successivi
```

## 4-bis. Gesti col mouse: clic fuori schermo e doppio clic finto

Due modi di perdere mezz'ora inseguendo un difetto che sta nell'attrezzo:

- **`page.mouse.click(x, y)` colpisce le coordinate della finestra, non l'elemento.** Se il bersaglio sta a
  2500px dall'alto, il clic cade nel vuoto e non succede niente. Portarlo a schermo prima:
  `el.scrollIntoView({ block: 'center' })`, poi rileggere `getBoundingClientRect()`.
- **`mouse.click(x, y, { clickCount: 2 })` NON genera l'evento `dblclick`**: manda un solo `press` con
  `detail=2`. Il doppio clic vero è la sequenza completa:

```js
await page.mouse.move(x, y);
await page.mouse.down({ clickCount: 1 }); await page.mouse.up({ clickCount: 1 });
await page.mouse.down({ clickCount: 2 }); await page.mouse.up({ clickCount: 2 });
```

Per capire *quale* evento arriva davvero, mettere una spia in cattura e leggerla dal `console` di puppeteer:

```js
await page.evaluate(() => document.addEventListener('dblclick',
    e => console.log('DBG dblclick ' + e.target.className + ' detail=' + e.detail), true));
page.on('console', m => { if (String(m.text()).startsWith('DBG')) console.log(m.text()); });
```

Agganciare sempre l'handler dei dialoghi: il pannello release usa `confirm()` e senza handler il click
sull'annulla resta appeso.

```js
page.on('dialog', async d => { confirms.push(d.message()); await d.accept(); });
```

## 4-ter. Zoom: usare la funzione della pagina

Lo zoom si mette con `window.vipiSetZoom(z)`, **non** scrivendo `document.documentElement.style.zoom`: a mano
non scatta il `resize`, quindi `vipiFitViewport` non rimisura e il driver denuncia uno scorrimento che nella
pagina vera non c'e'. E «la pagina scorre?» sotto zoom si chiede con `clientHeight`, non con `innerHeight`:
`scrollHeight` sta in unita' di layout, `innerHeight` in px di finestra.

## 5. Bersagli utili nel DB di sviluppo

| Cosa provare | Rotta | Note |
|---|---|---|
| Salute | `/vsop/health` | `Healthy` in una riga |
| Viewer aeroporto | `/vsop/libb/airports?icao=LIBD` | SID (19 righe, tutti i casi di initial climb), livelli di transizione con `≤`/`–`/`≥`, frequenze, Remarks |
| Elenco aeroporti | `/vsop/libb/airports` | esercita `AirportQuickPanel` + METAR live NOAA |
| Editor aeroporto | `/vsop/libb/airports/editor?icao=LIBD` | `#sec-versioni`: **unico** che monta `ReleasePanel` con `ShowDiff`+`AllowCancel` **e** ha release in timeline → è qui che si prova diff/annulla |
| Editor APP | `/vsop/libb/apps/editor?app=LIBD_CS0_APP` | `#p-release`; senza bozza il publish risponde con un callout, non un crash |
| Versioni (admin) | `/vsop/libb/versioni` | multi-documento |
| Cosa è cambiato | `/vsop/changed` | **non** `/vsop/{acc}/cambiato` |

Selettori: `#p-release`, `#sec-versioni`, `.ver-row`, `table.cfg-table`, `.vmeta`, `.vb`, `.callout`,
`.rail-card`, `.extra-inline`, `.doc-layout`, `[data-tour=release]`.

Altri ICAO con release Airport: `LIBC`, `LIBR`, `LIRN`. ACC con release: `LIBB`, `LIMM`.

## 6. Guarda gli screenshot

Estrarre dati dal DOM non basta: `page.screenshot()` e **aprire l'immagine**. Il bug del numero di versione
(§7) era invisibile a qualunque asserzione che non lo cercasse per nome, ma ovvio a occhio.

## 7. Trappole già pagate

- **`v@r.Proprietà` in Razor esce LETTERALE.** Una `@` fra due caratteri non-spazio è letta come indirizzo
  email e non apre un'espressione: a schermo compariva `rel. v@r.VersionNumber`. Nessun warning di
  compilazione. Serve `v@(r.VersionNumber)`. Stessa famiglia di `Key="x"` invece di `Key="@x"`.
- **`Invoke-WebRequest` 200 ≠ pagina funzionante** (vedi §4).
- **Migration**: provarle su una **copia del `vipi.db` reale**, non solo su un DB vuoto da `EnsureCreated`.
  Un indice unico su `AirportSids(AirportId, StableKey)` passa sui test e **fallisce** sui dati veri.
- **Dato editoriale noto**: la SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft»,
  quota implausibile (le altre BANAV hanno `9000` → «FL90»). È un errore di contenuto, non di codice.

## 8. Chiudi

Fermare il processo, altrimenti resta in ascolto e blocca la porta al giro successivo:

```powershell
Get-Process -Name "Vipi.Host" -ErrorAction SilentlyContinue | Stop-Process -Force
```

Il comando di avvio esce con codice **255**: è l'effetto del kill, non un errore.

Poi verificare che `src/Vipi.Host/vipi.db` **non** risulti modificato in `git status`: se lo è, l'app ha girato
sul DB del progetto invece che sulla copia (§1 sbagliato).
