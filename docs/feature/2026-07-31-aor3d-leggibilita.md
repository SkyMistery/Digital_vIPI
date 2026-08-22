# Feature — AoR 3D: leggibilità (altezza, etichette, selezione settori)

Data: 2026-07-31 · Stato: **FATTO** (suite verde, verificato live su LIBB) · Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue: il viewer 3D introdotto il 2026-07-30 (tab 2D/3D nel blocco AoR + pagina dedicata).

## Obiettivo

Il viewer 3D si guardava ma non si leggeva: prismi troppo alti, etichette tagliate e accavallate, e nessun modo di
accendere/spegnere i settori che assomigliasse al 2D. Quattro interventi, più la pagina dedicata messa in pausa.

## Pre-flight — 4 domande

**1. Modello.** Nessun dato nuovo: si lavora sulla stessa `AccAorView` (callsign, colore, anelli, banda FL). Il
fattore «Altezza» è **vista, non dato** — non si salva da nessuna parte, come la proiezione.

**2. Dispatch.** Nessuno `switch` per tipo. Al contrario, si è tolta una duplicazione potenziale: le chip del 3D
sono le stesse del 2D e le pilota lo stesso gestore.

**3. Ingressi + verifica.** Ingresso invariato (tab «Vista 3D» del blocco AoR). Verifica: bUnit sul markup +
guida reale del flusso con Edge/puppeteer su LIBB (vedi sotto).

**4. Propagazione.** Sì, due rimozioni: il link «Apri pagina» (con i parametri che lo instradavano) e le etichette
sprite. Entrambe propagate nello stesso giro — parametri, call site, commenti, questa carta, HANDOFF, memoria.

## Cosa è cambiato

### 1. Link «Apri pagina» rimosso (temporaneo)

La pagina dedicata `/services/vsop/aor3d/{Kind}/{Key}` va rilavorata, quindi il suo unico ingresso UI sparisce. Non è bastato
togliere l'`<a>`: sarebbero restati `AccAor3d.FullPageUrl`, `AccAor.Aor3dPageUrl`, `AccSectionBody.Aor3dPageUrl` e
quattro call site a puntare a un link che non esiste più (domanda 4 del gate). Via tutti.

La **rotta resta viva**: `Aor3dFullPage.razor` porta in testa la nota del perché e ci si arriva a URL diretto.
Rimettere il link = `git revert` del commit «AoR 3D: rimosso il link «Apri pagina»».

### 2. Selettore «Altezza» (esagerazione verticale)

I prismi occupavano fino al 55% del lato orizzontale: torri che nascondevano la geografia. Ora la barra ha
`×0.25 · ×0.5 · ×0.75 · ×1 · ×1.5 · ×2` e **parte da ×0.5**; `×1` è esattamente la resa precedente.

Il fattore è `group.scale.z`: scala in un colpo geometrie, quote di base dei prismi e ancore delle etichette,
senza ricostruire la scena. La camera rimira a metà della nuova altezza e **rivede la distanza** (`radius`),
altrimenti a ×2 i prismi sfondavano l'inquadratura. «Reset vista» rimette anche il fattore di default.

### 3. Etichette: da sprite a overlay HTML

| Difetto | Causa |
|---|---|
| testo tagliato | sprite disegnato su un canvas fisso 256×64 px: i nomi lunghi uscivano dal canvas |
| etichette mancanti | dedup **per nome**: due settori omonimi ne producevano una sola |
| accavallamenti | posizione = centroide + uno scaglionamento in z troppo debole |

Ora ogni settore ha un'ancora 3D in cima al prisma e una `div.aor3d-lab` in un layer sopra il canvas, riposizionata
dopo ogni render proiettando l'ancora in coordinate schermo. Testo completo e nitido (HiDPI), font del sito,
**cliccabile** (click = mostra/nasconde il settore). Il testo è il **callsign** + la banda FL, col nome esteso nel
`title` — come le chip del 2D.

Declutter greedy: priorità al footprint più grande, qualche tentativo di offset verticale, poi l'etichetta sparisce
(meglio nessuna etichetta che una pila illeggibile). Legenda e suggerimento sono trattati come ostacoli.

Il layout gira dentro `render()`, che è **on-demand** (drag, zoom, toggle) e non un loop `requestAnimationFrame`:
il costo del declutter è trascurabile.

### 4. Chip settore come nel 2D

Il 3D emette le stesse chip del 2D (`.aor-block > .aor-toggles > .aor-chip`, con «Tutti»/«Nessuno»). Non c'è logica
nuova: lo stage espone `_aorSetSec` e `_secMap` **come il contenitore Leaflet**, e in `vipi-aor.js` il bersaglio
delle chip è diventato `'.aor-leaflet, .aor3d-stage'`. Un solo gestore per entrambe le viste.

**Niente chip «configurazione» nel 3D** (scelta): lì si accendono i singoli settori. La legenda resta perché a
schermo intero le chip, che stanno fuori dallo stage, non si vedono; chip, legenda ed etichette passano tutte da
`setSec`, quindi non possono divergere.

## Trappola pagata (trovata in verifica live)

**`setPointerCapture` mangia i click dei figli.** Lo stage cattura il puntatore sul `pointerdown` per l'orbita:
il `pointerup` viene ridiretto sullo stage e il `click` è dispatchato sull'antenato comune — le etichette (e già
prima le righe di legenda) non lo ricevevano mai. Rimedio: etichette e legenda fermano il `pointerdown`, così il
click arriva e trascinare da lì semplicemente non ruota la scena.

## Test

`AccAor3dTests`: +2 (chip settore senza chip configurazione; selettore Altezza con 6 fattori e ×0.5 acceso, e
assenza del link alla pagina). Suite completa verde.

## Verifica live

`/services/vsop/libb/vipi` (tab 3D) e `/services/vsop/aor3d/acc/LIBB`, Edge headless, screenshot letti:

- chip → prismi, legenda ed etichette si spengono insieme; «Nessuno»/«Tutti» coerenti;
- click su etichetta spegne il settore, click su riga di legenda lo riaccende;
- trascinare la scena **non** spegne nulla;
- `×0.25 … ×2` restano inquadrati; ×0.5 è il default;
- 0 etichette sovrapposte e 0 tagliate in tutti gli stati provati (misurato sui rettangoli resi);
- il 2D non è regredito (chip → layer Leaflet come prima); nessun errore di console, nessun 4xx.
