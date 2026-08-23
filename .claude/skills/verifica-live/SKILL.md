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

⚠️ **Deroga**: se è proprio il sectorfile che si sta verificando (catalogo dei punti, import SID, shape TWR),
`Sectorfile__RawBaseUrl` va lasciato **acceso** — spento, il catalogo arriva vuoto e non si verifica niente.

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

⚠️ **`probe.js` non compone l'alfa**, e il 23 agosto 2026 è costato due giri: su una zebra fatta con
`color-mix(..., transparent)` ha risposto 2,63:1 e 1,39:1, numeri peggiori dei veri e tutti falsi. E lo
script scritto per rimediare ha letto `color(srgb 0.894 0.916 0.991)` con una regex da `rgb()`, prendendo
`0.894` per un valore su 255: accusava `.cop`, cioè una classe che tutta l'applicazione usa da mesi.
**Quando un numero accusa qualcosa che sta lì da mesi, il sospetto va prima allo strumento.** Il contrasto
vero si misura risalendo i fondi e componendo le alfa, e il `color()` moderno va convertito.

C'è anche `classi-morte.py`, che elenca le classi del foglio che nessuna sorgente nomina:

```powershell
python .claude/skills/verifica-live/classi-morte.py "<radice del repo>"
```

⚠️ Il suo elenco **apre** la domanda, non la chiude: cerca il nome nudo nelle sorgenti, quindi i nomi
composti a pezzi (`$"xt-ind{n}"`, `$"blk-{k}"`, `class="lvl@(livello)"`) risultano morti e non lo sono.

⚠️ E **il confronto è in minuscolo, apposta**: `.node-badge.fss` nasce da un `"FSS"` più un
`.ToLowerInvariant()`, e in minuscolo quel nome non compare in nessun file. Tolta il 23 agosto, e rimessa
un'ora dopo perché la prova qui sotto l'ha ripescata.

**La prova che chiude la domanda è `nessun-bersaglio.js`**: prende i selettori che il `git diff` dice
rimossi e verifica, su una trentina di pagine reali (con tutti i `<details>` aperti), che **nessuno trovi
più niente nel DOM**. È l'unico controllo che vede le classi costruite interamente da una variabile — che
nessuna passata sul testo può vedere.

```powershell
git diff src/Vipi.Ui/wwwroot/vipi-theme.css | ... > selettori-tolti.txt   # vedi la carta del 23 agosto
node nessun-bersaglio.js
```

C'è anche `sfora.js <url>`, che dice **quale elemento** fa scorrere una pagina in orizzontale (salta chi ha
`overflow-x:auto`, che scorre per costruzione e non sfora).

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

Stessa cosa in orizzontale, e il 23 agosto 2026 e' costata un giro di misure buttato: **si confrontano
`scrollWidth` e `clientWidth`**, che stanno tutti e due in unita' di layout. `getBoundingClientRect().width`
sta in px di finestra: messo accanto a `clientWidth` dice «serve 1280, ha 914» per ogni elemento della pagina
e fa sembrare colpevole il primo che capita (la topbar, che non c'entrava). E ⚠️ un elemento con
`overflow-x:auto` ha `scrollWidth` maggiore del contenitore **per costruzione**: scorre, non sfora. Chi cerca
il colpevole deve saltarlo, o legge il falso positivo piu' grosso della lista.

## 5. Bersagli utili nel DB di sviluppo

| Cosa provare | Rotta | Note |
|---|---|---|
| Salute | `/vsop/health` | `Healthy` in una riga |
| Viewer aeroporto | `/services/vsop/libb/airports?icao=LIBD` | SID (19 righe, tutti i casi di initial climb), livelli di transizione con `≤`/`–`/`≥`, frequenze, Remarks |
| Elenco aeroporti | `/services/vsop/libb/airports` | esercita `AirportQuickPanel` + METAR live NOAA |
| Editor aeroporto | `/services/vsop/libb/airports/editor?icao=LIBD` | `#sec-versioni`: **unico** che monta `ReleasePanel` con `ShowDiff`+`AllowCancel` **e** ha release in timeline → è qui che si prova diff/annulla |
| Editor APP | `/services/vsop/libb/apps/editor?app=LIBD_CS0_APP` | `#p-release`; senza bozza il publish risponde con un callout, non un crash |
| Versioni (admin) | `/services/vsop/libb/versions` | multi-documento |
| Cosa è cambiato | `/services/vsop/changed` | **non** `/services/vsop/{acc}/cambiato` |

Selettori: `#p-release`, `#sec-versioni`, `.ver-row`, `table.cfg-table`, `.vmeta`, `.vb`, `.callout`,
`.rail-card`, `.extra-inline`, `.doc-layout`, `[data-tour=release]`.

Altri ICAO con release Airport: `LIBC`, `LIBR`, `LIRN`. ACC con release: `LIBB`, `LIMM`.

⚠️ **Pagine con lock: prima si prende il lock, o non si prova niente.** Su `/services/vsop/admin/transfers`
(e sulle altre pagine con `EditLockBar`) i tasti di riga nascono **spenti**: `_canEdit` è vero solo se la
sessione tiene il lock di struttura. Un clic su un tasto disabilitato non fa niente e non dice niente — il
pannello resta su «nessuna clausola aperta» e sembra un difetto della pagina. Si prende così, prima di tutto:

```js
await page.evaluate(() => document.querySelector('.lockbar.free .btn.primary')?.click());
```

Nel dubbio, la diagnosi è una riga: `[...row.querySelectorAll('button')].map(b => b.innerText + ' ' + b.disabled)`.

⚠️ **Le sezioni degli accordi nascono chiuse** (`▸`): si aprono cliccando i cartigli `button.xt-dirtoggle`,
non c'è un «apri tutto» che le prenda. Senza, `tbody tr` è vuoto e sembra che l'accordo non abbia clausole.

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
- **Dato editoriale noto**: al 23 agosto 2026 `BANA8A` di LIBD (pista 07) è **già corretta a `9000`** nel
  `vipi.db` di sviluppo; quella ancora sbagliata è **`BANA5Z`** (pista 25), con `InitialClimb = "500"` →
  resa «500 ft» mentre le altre BANAV stanno a 5000/9000. È un errore di **contenuto**, non di codice: si
  vede a schermo nella tabella SID e non va «corretto» dal codice.

## 8. Chiudi

Fermare il processo, altrimenti resta in ascolto e blocca la porta al giro successivo:

```powershell
Get-Process -Name "Vipi.Host" -ErrorAction SilentlyContinue | Stop-Process -Force
```

Il comando di avvio esce con codice **255**: è l'effetto del kill, non un errore.

Poi verificare che `src/Vipi.Host/vipi.db` **non** risulti modificato in `git status`: se lo è, l'app ha girato
sul DB del progetto invece che sulla copia (§1 sbagliato).
