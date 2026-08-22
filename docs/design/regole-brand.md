# Regole di brand — colori e font

> Fonte unica: [ivaoaero/atmosphere](https://github.com/ivaoaero/atmosphere) → `brand/src/tokens.json`.
> Contiene **solo `color` e `font`**: spaziature, raggi e regole sul logo non sono brand, sono prodotto.
> Storia e misure: [2026-08-22-brand-atmosphere](../feature/2026-08-22-brand-atmosphere.md).

## Le tre regole che contano

### 1. Nessun colore letterale fuori dal livello 1

`vipi-theme.css` è a tre livelli:

| livello | cos'è | esadecimali ammessi |
|---|---|---|
| 1 | la scala di brand, copiata alla lettera (`--ivao-color-*`) | **sì, solo qui** |
| 2 | i ruoli (`--surface`, `--ink`, `--brand-ink`, `--ok-ink`, …) | no |
| 3 | tinte e categorie, derivate con `color-mix()` | no |
| corpo del foglio | le regole vere | **no — zero, ed è verificato** |

Serve una sfumatura che non c'è? **Si aggiunge un token**, non un letterale. Un `#rrggbb` scritto in una
regola è un colore che non segue il brand quando il brand cambia e non si gira quando il tema si gira.

Le sole eccezioni:
- `#fff` / `rgba(255,255,255,·)` e `rgba(0,0,0,·)` come **velo** su una superficie già colorata: lì il
  bianco e il nero non sono colori di brand, sono opacità;
- la **stampa**, dove il bianco è il foglio e il nero è l'inchiostro;
- i colori **scelti dall'utente** o **cartografici**, che finiscono in un `<input type=color>`, nel DB o in
  un attributo SVG (`AorColorScheme`, `AreaMapBlock._color`): lì un token non sarebbe né selezionabile né
  disegnabile.

### 2. I font hanno tre ruoli, e non sono negoziabili

| token | famiglia | uso |
|---|---|---|
| `--font-head` | **Poppins** | titoli, etichette, chrome |
| `--font-body` | **Nunito Sans** | corpo, prosa |
| `--font-mono` | **IBM Plex Mono** | monospaziato |

⚠️ Fino al 2026-08-22 i primi due erano **scambiati**. Se qualcosa sembra suggerire il contrario, è vecchio.

### 3. Il contrasto si misura, non si guarda

Regola per gli inchiostri semantici: **il passo `.700` della scala; dove `.700` non regge il 4.5:1 su
bianco, si scende a `.800`.** Perciò `--warn-ink` è `yellow-800` e non `yellow-700` (3.24:1).

⚠️ Non promuovere questi token a `.600` «per vivacità»: `green-600` fa 3.71:1 e `yellow-600` 3.24:1.

`--ink-faint` e `--ink-dim` **non** reggono AA e non devono: sono il tono spento, per testo grande,
controlli disabilitati e grafica. Un'etichetta piccola che si deve leggere prende `--ink-soft`.

## Scrivere una regola nuova senza rompere il tema scuro

Il tema scuro ridefinisce **solo i token**, mai le regole. Perché continui a funzionare:

1. **Fondo che si gira** → `--surface`, `--surface-soft`, `--bg`, `--surface-muted`.
   **Fondo che NON si gira** (sta sulla barra blu o su un pieno di brand) → `--on-brand`.
   ⚠️ Sono entrambi bianchi nel tema chiaro: **la differenza non si vede finché non si spegne la luce.**
2. **Testo in blu di brand** → `--brand-ink` (si schiarisce al buio).
   Usare `--ivao-blue` per il testo **solo** dentro un chip che resta chiaro anche al buio.
   `--ivao-blue` resta il colore dei **pieni e dei bordi**.
3. **Colori categoriali**: `--cat-*` per bordi e pieni, `--cat-*-ink` per il testo. I valori pieni sul testo
   non reggono AA (`--cat-vloa` su bianco: 2.24:1).
4. Un colore che finisce in **Leaflet, in un attributo SVG o in `ctx.fillStyle`** dev'essere risolto prima:
   `aorColor()` in `vipi-aor.js` accetta sia un esadecimale sia il nome di un token.

## Il tema lo sceglie l'utente

Tre stati: **automatico** (segue il sistema), **chiaro**, **scuro**. La scelta sta in `localStorage`
(`vipiTheme`) ed è **per browser**, non per utente.

- In **automatico** l'attributo `data-theme` **non c'è**. L'assenza *è* «segui il sistema»: non scrivere
  `data-theme="auto"`, vorrebbe dire aggiungere al CSS un caso per dire la stessa cosa.
- Il comando sta in `vipi-theme-mode.js` (`vipiSetTema`, `vipiCicloTema`, `vipiApplyTema`), caricato nel
  `<head>` **senza `defer`**. ⚠️ Non spostarlo e non aggiungergli `defer`: se arriva dopo il primo disegno,
  chi ha scelto il tema scuro vede un lampo bianco a ogni caricamento.
- ⚠️ Non è inline, ed è deliberato: uno `<script>` inline obbliga a `script-src 'unsafe-inline'` nella CSP.
- Un comando nuovo che dipende dallo stato del tema sceglie l'aspetto **in CSS** (`:root[data-theme=…] …`),
  non in JS: il chrome è SSR statico e non si ridisegna da sé. Al JS si lascia solo ciò che in CSS non è
  esprimibile — le etichette, che arrivano dal resx tramite attributi `data-*`.
- Chi **disegna** invece di dichiarare (un canvas, Leaflet) non si ridipinge da solo: `vipiSetTema` emette
  `vipi:tema` e un `resize` apposta.

## Come si verifica

Non basta compilare: i test sono verdi anche con un tema rotto.

```powershell
# procedura in .claude/skills/verifica-live/
node driver.js      # 5 pagine x 2 temi: token risolti, elementi rimasti bianchi, errori JS
node probe.js dark  http://localhost:5034/<pagina>   # contrasto elemento per elemento
node probe.js light http://localhost:5034/<pagina>
```

⚠️ `color-mix()` **non** si serializza in `rgb()`: `getComputedStyle` restituisce `color(srgb 0.95 0.95 0.97)`
coi canali fra 0 e 1. Chi scrive una sonda di contrasto deve gestirlo, o produrrà numeri inventati.

⚠️ La sonda non compone l'**alfa**: il testo su un velo semitrasparente esce come falso positivo. Quei casi
si ricalcolano a mano componendo il velo sul fondo.

### I due controlli che prendono la classe di errore più insidiosa

Un foglio che **sfugge alla passata sui token** non fa fallire nessun test e non si vede nel tema chiaro.
È successo a `vipi-aor3d.css`: fondo bianco scritto a mano (bianco su bianco al buio) e famiglie di font
scritte per nome (quelle *sbagliate*, dopo lo scambio dei ruoli). Da allora:

```powershell
node sweep.js          # 12 pagine in tema SCURO: ogni fondo dipinto quasi bianco = sospetto
# lint statico: nessuna famiglia scritta per nome fuori da vipi-fonts.css e dal livello 1
grep -rnE "font-family: *['\"]?(Poppins|Nunito|IBM Plex|ui-monospace|monospace|system-ui)" src/ ^
  | findstr /V vendor | findstr /V vipi-fonts.css | findstr /V ivao-font
```

⚠️ Il lint va fatto **anche** sui `.razor` e sui `.js`, non solo sui `.css`: due dei casi trovati stavano
in uno stile in linea e in una stringa di stile costruita da JavaScript.

## Il giorno che il sito ospitante adotta atmosphere

Il livello 1 usa gli stessi nomi che `@ivao/atmosphere-brand` emette in `tokens.css`
(`--ivao-color-atmos-700`, …) e gli stessi valori. Quando l'ospitante importerà quel foglio, **il livello 1
si cancella e basta**: i livelli 2 e 3 continuano a funzionare senza una modifica.
