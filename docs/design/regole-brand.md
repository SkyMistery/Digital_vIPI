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

## Il giorno che il sito ospitante adotta atmosphere

Il livello 1 usa gli stessi nomi che `@ivao/atmosphere-brand` emette in `tokens.css`
(`--ivao-color-atmos-700`, …) e gli stessi valori. Quando l'ospitante importerà quel foglio, **il livello 1
si cancella e basta**: i livelli 2 e 3 continuano a funzionare senza una modifica.
