# Allineamento al brand IVAO «atmosphere» (22 agosto 2026)

> Ramo `brand-atmosphere`. Fonte: [ivaoaero/atmosphere](https://github.com/ivaoaero/atmosphere),
> file `brand/src/tokens.json` — che contiene **solo `color` e `font`**. Niente spaziature, niente raggi,
> niente regole sul logo: il perimetro «brand» è più stretto di quanto il nome del repo lasci pensare.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole UI: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Perché adesso

Il committente ha segnalato il repo. La ricognizione ha trovato **un errore netto** (i due font scambiati),
una **deriva sistematica ma innocua oggi** (i neutri fuori scala), un **rischio a scadenza** (nessun tema
scuro, mentre atmosphere ne ha uno) e — bene — la **palette di brand presa giusta su sei colori su sei**.

## Cosa ho trovato, misurato

### ⚠️ B1 — I due font erano scambiati

`tokens.json` dice `head: Poppins`, `sans: Nunito Sans`. Il componente `H1.tsx` di atmosphere conferma
(`className={'font-head …'}`): **titoli = Poppins, corpo = Nunito Sans**.

Noi avevamo l'opposto, e non per deriva: era scritto a tavolino in
[`piano-vipi-tool.md`](../design/piano-vipi-tool.md) («Nunito Sans → titoli, Poppins → prosa»).

Lo scambio è di due righe perché le famiglie erano già in due variabili sole (144 usi complessivi), **ma non
è gratis**: i titoli girano a 700/800 e di Poppins erano self-hostati solo 300–600. Aggiunti i quattro
`.woff2` mancanti (700 e 800, latin + latin-ext).

### ✅ B2 — La palette di brand era giusta

| nostro | valore | token atmosphere |
|---|---|---|
| `--ivao-blue` | `#0D2C99` | `atmos.700` (DEFAULT) |
| `--ivao-lightblue` | `#3C55AC` | `ocean.600` (DEFAULT) |
| `--ivao-green` | `#2EC662` | `semantic.green.500` |
| `--ivao-yellow` | `#F9CC2C` | `semantic.yellow.500` |
| `--ivao-red` | `#E93434` | `semantic.red.500` |
| `--ivao-info` | `#7EA2D6` | `semantic.blue.500` |

### ⚠️ B3 — 501 colori letterali, e quasi tutti erano *derivati* dal brand

`vipi-theme.css` conteneva 501 `#rrggbb` sotto il blocco dei token, su ~140 valori distinti. Misurando la
distanza di ciascuno dalla scala di brand è venuto fuori che **il lavoro originale era derivato bene, solo
cotto in letterali**:

| scarto dal valore di brand | quanti |
|---|---:|
| 0–4 /255 (invisibile) | 350 |
| 5–9 | 88 |
| 10–14 | 29 |
| ≥ 15 | 34 |

I 34 sopra soglia sono quasi tutti **consolidamenti voluti**: cinque rossi scuri leggermente diversi che
diventano un solo `--danger-ink`, tre verdi che diventano `--ok-ink`. Era il senso dell'operazione.

⚠️ Le tinte pallide (fondi dei callout, delle pastiglie) **non esistono nella palette di brand**: le scale
semantiche partono dal passo 50, che è già una mezza tinta. Non sono state inventate a mano: sono
`color-mix()` del colore di brand **con la superficie**, con la percentuale tarata sul valore che il foglio
usava prima. Scarto massimo misurato: 7/255. Così seguono il brand se il brand cambia, e si girano da sole
quando la superficie diventa scura.

### ⚠️ B4 — Tre colori proprio estranei al brand

Il primo giro non li aveva visti perché non erano scritti in esadecimale a sei cifre:

| dove | cos'era | ora |
|---|---|---|
| `.staff-badge` | `rgba(255,193,7,·)` — **ambra di Bootstrap** | `--ivao-yellow` |
| ombra di un pannello | `rgba(209,36,47,·)` — **rosso di GitHub** | `--ivao-red` |
| `MainLayout.razor.css` | `#3a0647` — **il viola del template Blazor di serie** | scala `atmos` |
| overlay torri in mappa | `#C2410C` / `#F97316` — **arancioni di Tailwind** | `product.artifice` |
| `.apt-tag` in AppsList | `#b8860b` — `darkgoldenrod` di CSS | `--warn-ink` |

Il gradiente viola del template Blazor era il colore più lontano dal brand in tutto il repo. Si vede solo
sulla pagina d'errore dell'host (la home rimanda a `/vsop`), ma spediva lo stesso.

### ⚠️ B5 — `--brand` era un token mai definito

Quattro usi (`AeroportoEditorPage` ×3, `vipi-theme.css` ×1), **nessuna definizione**: il ripiego scattava
sempre, e i due ripieghi erano diversi fra loro (`#0D2C99` in un punto, `#3C55AC` nell'altro).

### ⚠️ B6 — Gli inchiostri semantici: il brand da solo non basta

Regola adottata: **il passo `.700` di ogni scala è l'inchiostro; dove `.700` non regge il 4.5:1 su bianco si
scende a `.800`.** Misurato (WCAG 2.1):

| ruolo | passo scelto | contrasto | il passo più chiaro non va |
|---|---|---:|---|
| `--ok-ink` | `green-700` | 6.57:1 | `green-600` 3.71:1 |
| `--warn-ink` | `yellow-800` | 6.08:1 | `yellow-700` **3.24:1** |
| `--info-ink` | `blue-700` | 5.67:1 | — |
| `--danger-ink` | `red-700` | 8.39:1 | — |

⚠️ `--nbr-ink` (viola, «confinante / FSS») resta un'**estensione locale dichiarata**: il viola non esiste
nella palette come colore semantico, e l'unico viola del brand (`product.creators #8b5cf6`) su bianco fa
4.23:1 — sotto AA, non usabile per il testo.

### ⚠️ B7 — `--bg` è l'unico valore che il brand non ha

La scala `fuselage` salta da `50` (`#fafaff`) a `100` (`#eeeff5`) e il fondo pagina sta in mezzo. È scritto
come **interpolazione fra i due passi**, che è esattamente il modo in cui atmosphere ha ricavato i suoi
`150`/`250`/`450`/`550`.

## Come è fatto adesso

Tre livelli, tutti dentro `:root` in `vipi-theme.css`:

1. **Livello 1 — copia, non progetto.** La scala di brand, valori alla lettera da `tokens.json`, coi nomi
   che `@ivao/atmosphere-brand` emette (`--ivao-color-atmos-700`, …). Il giorno che il sito ospitante
   importa quel foglio i valori coincidono e **questo blocco si cancella senza toccare nient'altro**.
   È l'unico posto dove un esadecimale è legittimo: 97 letterali, tutti lì.
2. **Livello 2 — ruoli.** Nomi nostri, valori sempre presi dal livello 1.
3. **Livello 3 — tinte e categorie**, derivate dal livello 1/2 con `color-mix()`.

**Sotto il blocco dei token non è rimasto un solo colore letterale** (verificato: 0).

### Il tema scuro

Ridefinisce **solo** i token dei livelli 2/3 — nessuna regola del corpo è stata toccata, ed è possibile
proprio perché il corpo non contiene più colori. Tre agganci, come li fa atmosphere: `prefers-color-scheme`,
`[data-theme="dark"]` / `.dark`, e `[data-theme="light"]` che deve poter vincere sul sistema.

⚠️ Quello che **non** si gira: la barra blu, i chip che le stanno sopra, i tooltip scuri. Quei fondi sono di
brand, non di tema.

## Le trappole, che non si vedono leggendo il codice

⚠️ **Il blu di brand faceva due mestieri opposti.** 112 usi come *colore del testo* contro 30 come *fondo*.
Al buio il testo deve schiarirsi e il fondo no. Separati in `--brand-ink` e `--ivao-blue`. Restano 13 regole
col blu scuro: sono quelle il cui chip resta chiaro anche al buio (`background:var(--on-brand)`) — quel
fondo non si gira, quindi non deve girarsi nemmeno il testo.

⚠️ **`background:#fff` è ambiguo, e lo spareggio alfabetico sceglie male.** Sia `--surface` sia `--on-brand`
distano zero da `#ffffff`. La prima passata ha scelto `--on-brand` per tutti e 65: al buio 58 superfici
restavano bianche con testo bianco sopra. Il criterio non è la distanza, è **il selettore**.

⚠️ **`color-scheme` va dichiarato.** Senza, `<input>`, `<select>` e le barre di scorrimento restano chiari
qualunque cosa dica il CSS: il browser non deduce il tema dai colori che gli diamo, glielo si deve dire.

⚠️ **LA CARTA È SEMPRE CHIARA.** I due agganci del tema scuro stanno sotto `@media screen`. Senza, chi ha il
sistema in tema scuro stamperebbe un documento operativo a blocchi neri: il foglio di stampa forza il fondo
bianco, ma i token no.

⚠️ **`color-mix()` non si serializza in `rgb()`.** `getComputedStyle` restituisce `color(srgb 0.95 0.95 0.97)`
con i canali fra 0 e 1. La prima versione della sonda di contrasto li leggeva come 0–255 e produceva numeri
inventati — dava per falliti i titoli che a schermo si vedevano benissimo. **Una sonda che sbaglia è peggio
di nessuna sonda.**

⚠️ **Leaflet e canvas non sostituiscono `var()`.** Un colore che finisce in un attributo SVG o in
`ctx.fillStyle` dev'essere un colore vero. `vipi-aor.js` ha ora `aorColor()`, che accetta sia un esadecimale
(l'override scelto dall'utente col selettore) sia il **nome** di un token e lo risolve sul `:root`.
Misurato: una catena `var()` a tre livelli si risolve correttamente (`--ivao-blue` → `#0d2c99`).

⚠️ **I colori che l'utente sceglie devono restare esadecimali.** `AreaMapBlock._color` e `AorColorScheme`
finiscono in un `<input type=color>` e nel DB: un token lì non sarebbe né selezionabile né salvabile.

## Cosa NON è stato ricondotto al brand, e perché

- **`AorColorScheme`** (colori degli anelli AoR per tipo di ente). Sono colori **cartografici**, non chrome:
  i poligoni si sovrappongono e si riempiono al 16%, e i passi del brand a piena saturazione a quell'opacità
  diventano indistinguibili. Tre combaciano già col brand (CTR, APP, ATIS).
- **`--cat-*`** (schede di navigazione, guide dei gruppi trasferimento). Insieme **categoriale**: il loro
  lavoro è distinguersi *fra loro*. Schiacciarle su una scala sola le renderebbe indistinguibili, che è il
  difetto che dovevano evitare. ⚠️ Ma sul **testo** i valori pieni non reggevano: `--cat-vloa` su bianco fa
  2.24:1. Separate in `--cat-*` (bordi/pieni) e `--cat-*-ink` (testo). Il `.see-all` verde era **già** sotto
  AA prima di questo lavoro.
- **`--ink-faint` / `--ink-dim`.** Non reggono il 4.5:1 e non possono: sono il tono «spento», per testo
  grande, controlli disabilitati (che WCAG esenta) e grafica. ⚠️ I valori di prima erano **peggio**
  (`#888` 3.54:1, `#9aa0b0` 2.62:1). Le etichette piccole che si devono leggere sono passate a `--ink-soft`.

## Verifica

- Build `Release`: **0 avvisi, 0 errori** (gli avvisi qui sono errori, vedi audit dell'11 agosto).
- Test su net8: **1520 verdi**, 0 rossi.
- A schermo (Edge + puppeteer-core, procedura `verifica-live`), 5 pagine × 2 temi:
  font dei titoli **Poppins**; al buio la barra resta `atmos-700` e i titoli salgono ad `atmos-200`;
  **0 errori JS**; **0 token con catena `var()` rotta**; **0 elementi rimasti bianchi** nel tema scuro.
- Sonda di contrasto su 3 pagine × 2 temi: nessun testo sotto 4.5:1. I quattro casi che la sonda segnala
  sono falsi positivi (testo su veli semitrasparenti, che lei non compone); ricalcolati a mano stanno fra
  **6.06:1 e 8.12:1**.
- Stampa col sistema in tema scuro: `--surface` torna `#fff`. ✅

## Il comando per scegliere il tema

Il tema scuro nasceva **solo** automatico (`prefers-color-scheme`). Ora l'utente sceglie fra tre stati:
**automatico / chiaro / scuro**, con la scelta salvata in `localStorage` e valida per quel browser.

⚠️ Tre stati e non due: togliere «automatico» vorrebbe dire togliere il comportamento che va bene alla
maggior parte delle persone. In automatico l'attributo `data-theme` **non c'è**: il foglio tratta l'assenza
come «segui il sistema», e scrivere `data-theme="auto"` significherebbe aggiungere un caso al CSS per dire
la stessa cosa.

**Dove sta.** Un tasto solo nella barra, che gira fra i tre stati; e le tre scelte **esplicite** nel menù a
scomparsa, che sotto i 900px è l'unico posto dove il tema si sceglie (la barra lì nasconde zoom e tasti —
«quello che esce dalla riga vive nel menù»). Un tasto e non tre in barra perché la barra ha un minimo
incomprimibile noto e tre ne costerebbero una novantina di px.

**Come è fatto.** `vipi-theme-mode.js`, sul modello di `vipi-zoom.js`: in un **file** e non inline (uno
script inline obbliga a `script-src 'unsafe-inline'` nella CSP), nel `<head>` e **senza `defer`**, prima di
quello dello zoom.

### Le trappole di questo pezzo

⚠️ **Il lampo bianco.** È la ragione per cui lo script sta nel `<head>` senza `defer`. Se arriva tardi, chi
ha scelto il tema scuro vede l'intera pagina bianca per un istante a ogni caricamento — peggio del lampo
dello zoom, che riguarda solo la dimensione. **Misurato**: l'attributo è già presente al **primo frame**
(sonda registrata a `document_start`, che legge dentro il primo `requestAnimationFrame`).

⚠️ **L'icona la sceglie il CSS, non il JS.** Il chrome è SSR statico e non si ridisegna da sé: se l'icona
dipendesse dal JS sarebbe giusta solo *dopo* il primo render, cioè sbagliata proprio al primo disegno. Si
rendono tutte e tre e se ne mostra una con `:root[data-theme=…] .theme-ctrl .ti-…`. Al JS resta la sola
**etichetta**, che in CSS non è esprimibile, e le stringhe gli arrivano dai `data-lbl-*` perché stanno nel
resx e non dentro un `.js`.

⚠️ **`localStorage` può LANCIARE, non solo tornare `null`**: in navigazione privata o coi dati di sito
bloccati il solo accesso è un'eccezione. Un tema che non si ricorda è un fastidio; una pagina che non si
disegna è un guasto. Letture e scritture sono in `try/catch`.

⚠️ **Il tasto costa 38px, e la barra non li aveva.** Misurato: con etichette intere la barra passava da
1409 a **1447px**, cioè sforava di 7px a **1440** — una larghezza comunissima. Recuperati accorciando le
pastiglie ACC da `15px` a `13px` di padding (4 × 2 × 2 = 16px). L'alternativa era far scattare a 1460 la
soglia dei 1300 che toglie le etichette a «Editor» e «Incarichi»: costava di più (un degrado visibile a
1440) per risparmiare meno. Ora l'eccesso è **0 a 1600, 1440, 1280, 1024, 900 e 375**.

### Un difetto trovato di rimbalzo, in `vipi-zoom.js`

Lo script dello zoom gira nel `<head>`, quando `#vipiZoomPct` non esiste ancora: applicava lo zoom ma non
aggiornava la percentuale scritta in barra. Chi aveva lo zoom al 120% **leggeva «100%»** fino alla prima
navigazione «enhanced». Una riga (`DOMContentLoaded`), e non si ripete nel tema.

### Verifica

| cosa | esito |
|---|---|
| giro automatico → chiaro → scuro → automatico | ✅ attributo, `localStorage`, icona ed etichetta seguono |
| persistenza dopo `reload` | ✅ |
| lampo bianco | ✅ attributo già presente al primo frame |
| scelta esplicita contro il sistema, nei due versi | ✅ sistema scuro + scelta chiara → `--surface` `#fff` |
| sopravvivenza alla navigazione «enhanced» | ✅ |
| tre scelte nel menù a 390px, con lo stato attivo | ✅ `aria-pressed` incluso |
| sforo della barra a 6 larghezze | ✅ 0px ovunque |

## Coda: il visore 3D era rimasto fuori dal giro

Segnalato dal committente: **nel tema scuro la legenda «SETTORI» del visore 3D era bianca con le scritte
bianche.** Riprodotto e misurato: `.aor3d-legend` aveva `background: rgba(255,255,255,.92)` **scritto a
mano** — non si girava — mentre il testo dentro eredita `--ink`, che al buio è `#fafaff`.

`vipi-aor3d.css` era **sfuggito alla passata sui token**, e ne portava tutti i segni:

| cos'era | perché era un difetto | ora |
|---|---|---|
| `background: rgba(255,255,255,.92)` | non si gira: bianco su bianco al buio | `color-mix(… var(--surface) 92% …)` |
| `.lgt` in `var(--ivao-blue)` | il titolo su un fondo che ora si gira | `var(--brand-ink)` |
| `.aor-vm-btn` in `var(--ivao-lightblue)` | idem, sta su `--tint-atmos` | `var(--brand-ink-2)` |
| `font-family: 'Nunito Sans'` ×2 | **dopo lo scambio dei ruoli era il font sbagliato** | `var(--font-head)` |
| `font-family: 'Poppins'` | giusto per caso, ma scavalca il token | `var(--font-head)` |
| `ui-monospace, Consolas…` ×2 | non è il monospaziato del brand | `var(--font-mono)` |
| alone dell'etichetta in bianco | su fondo scuro disegna il contorno invece di staccare | alone da `--surface` |

Stessi difetti, fuori da lì: `.metar` in `vipi-theme.css` (il suo `font-family` conteneva
`'Cascadia Code'`, e la mia sostituzione cercava la stringa esatta senza) e `StructureCoverage.razor`.

### ⚠️ Quello che il CSS non poteva sistemare: l'inchiostro delle etichette

`edgeCol = col × 0.72` — l'inchiostro di ogni settore era il suo colore **scurito**, e il commento accanto
lo diceva già: «stacco netto sulla mappa **chiara**». Al buio le etichette diventavano blu notte su fondo
blu notte. Ora si **schiarisce** (`lerp` verso il bianco al 45%) quando la superficie è scura.

⚠️ Il tema si legge dalla **superficie**, non da `data-theme`: così vale per tutti e tre gli stati —
automatico compreso — senza doverli conoscere. E il parser gestisce `#rrggbb`, `rgb()` **e**
`color(srgb 0–1)`: è la stessa trappola che aveva già fatto sbagliare la sonda di contrasto.

⚠️ **Ricolorare, non ricostruire.** three.js ha già *disegnato*, e un disegno non si aggiorna da sé come
farebbe una regola CSS. Al cambio di tema si ricalcolano solo i colori (etichette + materiali degli
spigoli) e si ridisegna: ricostruire la scena azzererebbe **l'orbita che l'utente si è scelto**. Questo
chiude l'ultimo punto aperto della carta precedente, dove l'evento `vipi:tema` c'era ma non lo ascoltava
nessuno.

### Perché la verifica precedente non l'aveva visto

Controllava il font **solo su 5 pagine**, e la vista 3D non era fra quelle. Ora ci sono due controlli che
prendono l'intera classe di errore, ed è così che questo è stato chiuso:

- una **battuta larga** (`sweep.js`) che su 12 pagine in tema scuro cerca ogni elemento con un fondo
  dipinto quasi bianco. Dopo la correzione: **0 difetti** (i 2 «sospetti» che segnala sono la pastiglia ACC
  attiva sulla barra blu, bianca di proposito — è la sonda a risalire male gli antenati, non il CSS);
- un **lint statico** su `font-family` scritte per nome invece che via token. Dopo la correzione: **0**.

## Cosa resta aperto

- **Il logo.** Atmosphere ha un componente `IVAOLogo` (SVG, varianti orizzontale/icona, `white`/`atmos`).
  Noi usiamo un quadrato con gradiente e la scritta `vIPI`: non usiamo il logo IVAO da nessuna parte, il che
  è più prudente che usarlo male, ma è una scelta da fare, non un difetto da correggere in silenzio.
- **`MainLayout` / `NavMenu` dell'host** sono ancora il template Blazor di serie nella *struttura* (barra
  laterale, link «About» a `learn.microsoft.com`, `blazor-error-ui` con testo **in inglese** in
  un'applicazione localizzata). Qui sono stati portati sui token solo i **colori**: cosa debba mostrare
  quella pagina è una decisione di prodotto, non di brand.
- **Il tema scuro non è stato guardato su tutte le pagine.** Verificate a schermo: landing, vIPI ACC,
  elenco aeroporti, struttura admin, guida. Gli editor, la vista live e i blocchi mappa hanno il tema
  applicato per costruzione (non contengono più colori propri) ma **non sono stati guardati uno per uno**.
- ~~Il canvas del visore 3D non si ridipinge al cambio di tema.~~ **CHIUSO**: `vipi-aor3d.js` ascolta
  `vipi:tema` e ricolora senza ricostruire la scena.
- **Le tessere della mappa base restano chiare** anche nel tema scuro: arrivano da CARTO `light_all`, ed
  esiste `dark_all`. Non l'ho cambiato da solo perché è una scelta estetica e i poligoni sono tarati su
  una base chiara — ma nel tema scuro il piano della mappa resta un rettangolo luminoso nella scena.
- **La scelta è per browser**, non per utente: sta in `localStorage`, non nel profilo. Se un domani si
  vorrà seguire l'utente fra dispositivi, va nel DB.
