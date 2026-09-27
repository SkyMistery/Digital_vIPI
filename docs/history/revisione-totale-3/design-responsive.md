# Revisione 3 · d12 — Pagine, CSS responsive: che cosa controllare dal vivo

Letto sul codice di `sito/lavori` @ 8247e1ea (src identico a 1.46.5). Tutto quel che segue è **dedotto dal CSS
e dal markup**, non misurato: va guardato a schermo. Righe di `vipi-theme.css` = righe del file vero.

## Come misurare (lezione già pagata, carta `docs/feature/2026-08-22-telefono-pagine-pubbliche.md`)

- Driver con `isMobile:true` e `hasTouch:true`, non solo un viewport stretto.
- Il segnale di «pagina più larga del telefono» è `window.innerWidth > larghezza dello schermo`, **non**
  `scrollWidth`: il browser mobile allarga il layout viewport e rimpicciolisce tutto.
- Assetti: **360** (Android), **375** (iPhone SE/mini), **390**, **768** (tablet verticale), **1024**
  (tablet orizzontale). ⚠️ La verifica del 22 agosto fu fatta a 375/390/430/768: **360 non c'era**, e le pagine
  statistiche sono nate DOPO (24 agosto) e non sono mai state misurate sul telefono.
- Guardare gli screenshot: le sovrapposizioni non le trova nessuna misura.

## A — Pagine più larghe dello schermo (il telefono rimpicciolisce tutto)

| # | Pagina | Regola responsabile | Larghezza a cui si rompe | Atteso se c'è il difetto |
|---|---|---|---|---|
| A1 | `/services/stats`, `/services/stats/user/{vid}`, `/services/stats/division` | `vipi-theme.css:613` `.stats-cols{grid-template-columns:repeat(auto-fit,minmax(420px,1fr))}` dentro `.wrap.stats` con padding `clamp(16px,2vw,32px)`; nessuna regola la allenta sotto i 700 (il `@container` a 823 converte solo le tabelle in schede) | ogni contenitore < 420px, cioè **ogni telefono** (< ~452px di finestra) | `innerWidth` ≈ 452 su 375/390; le schede `.tbl-cards` larghe 420 escono a destra |
| A2 | `/services` (hub), `/services/vsop`, `/services/vsop/{acc}`, `/services/vsop/{acc}/apps`, `/services/vsop/{acc}/vloa` | `vipi-theme.css:1052` `.choice-grid{…minmax(340px,1fr)}` con `.wrap` padding 24px | < **388px** di finestra: **375 e 360** sì, 390 no | `innerWidth` 388 a 375 (13px), 388 a 360 (28px) |
| A3 | `/services/vsop/live` (colonne dei trasferimenti) | `vipi-theme.css:1790` `#xl-cols{…minmax(320px,1fr)}` | < 368px: **360** | `innerWidth` 368 a 360 |
| A4 | tutte le pagine con `.red-top-grid` (live) | `vipi-theme.css:1840` `minmax(290px,1fr)` | < 338px (solo telefoni da 320) | — (fuori dal perimetro «sotto 360 non si va») |

## B — Elementi che si sovrappongono o coprono lo schermo

| # | Pagina | Regola | Larghezza | Atteso |
|---|---|---|---|---|
| B1 | `/services/vsop/live` | `vipi-theme.css:1844-1845` `.reduced-top{position:sticky;top:62px;flex-wrap:wrap…}` senza variante stretta: titolo, badge, «N ATC online · ora», orologio, «Compatta», «Documento esteso», «vAWOS ↗», selettore (staff) e la riga dell'avviso vanno a capo su 5-6 righe e restano **appiccicate** sotto la barra | ≤ 768 (peggio ≤ 430 e in **orizzontale**, altezza ~375) | fascia fissa di ~250-300px + 62 della barra = ~40% dello schermo verticale, quasi tutto in orizzontale |
| B2 | `/services/vsop` | `vipi-theme.css:941` `.hero .airac{position:absolute;top:26px;right:30px}` sopra l'`h1` da 40px | ≤ ~430 | la pastiglia «AIRAC 26xx» copre la fine della prima riga di «Documentazione operativa» |
| B3 | `/services/vsop` | `vipi-theme.css:953` `.acc-grid` 4 colonne → `2298` `pw-1080` 2 colonne e basta; `958` `.acc-card .onl{position:absolute;top:18px;right:18px}` | 375/360 (schede larghe ~154px) | «offline»/«N online» sovrapposto al codice ACC da 30px |
| B4 | `/services/vawos/{icao}` | `vipi-awos.css:391-395` `.awos-mask{position:fixed;top:54px}` mentre `.awos-barra` va a capo su 2-3 righe | ≤ 860 | LOCAL REP./ATIS MSG/EXT. DATA si aprono SOPRA la barra dei tasti |
| B5 | `/services/vsop/mil` (tema chiaro) | `.hero p.muted` (`MilListPage.razor:30`): `.muted{color:var(--ink-soft)}` (#606282) vince sul colore bianco del `.hero` | tutte | testo introduttivo grigio su blu, contrasto ~2:1 (illeggibile) |

## C — Mappe e 3D: il dito resta intrappolato (touch)

| # | Dove | Regola | Atteso |
|---|---|---|---|
| C1 | Mappa AoR 2D (vIPI ACC, APP, aree regolamentate, live) | `vipi-aor.js:340` e `:682` `L.map(el,{scrollWheelZoom:false,…})`: `dragging`/`touchZoom` restano accesi → `leaflet.css:78-80` `touch-action:none` sul contenitore; altezza fissa 340px (`AccAor.razor:24`) su tutta la larghezza | a 375 una passata verticale che parte sulla mappa **sposta la mappa**, non la pagina; restano 24px di margine ai lati per scorrere |
| C2 | Carta MRVA | `vipi-mva.js:193` `fitBox`: altezza **360-620px**; stessa `touch-action:none` | su 812 di schermo la mappa ne occupa fino al 76% |
| C3 | Vista 3D (tab «3D» della vIPI, `/services/vsop/aor3d/…`) | `vipi-aor3d.css:24-26` `.aor3d-stage{height:540px;touch-action:none}`; `vipi-aor3d.js:554-558` orbita col dito, zoom **solo a rotella** | sul telefono lo stage prende 2/3 dello schermo e non si scorre la pagina; **nessun modo di zoomare** col tocco (niente pinch) |

## D — Zoom di pagina irraggiungibile (desktop e tablet)

| # | Dove | Regola | Atteso |
|---|---|---|---|
| D1 | tutta la barra | `vipi-theme.css:4505` nasconde `.zoom-ctrl` allo scaglione `tb-4`; il menù «☰» (`SopLayout.razor:23-80`) contiene ACC, tema, lingua e collegamenti, **non lo zoom** (il commento a riga 4503 dice il contrario). `vipi-ui.js:664-716` sceglie lo scaglione in **unità di layout**, quindi alzare lo zoom stringe la barra | su 1366 o 1440 premere «+» fino a ~1.5-1.8: la barra passa a `tb-4`, «−», «+» e la percentuale spariscono e nel menù non ci sono → si resta a quello zoom, salvato in `localStorage`, anche alle visite successive |

## E — Campi che fanno zoomare iOS al fuoco (< 16px)

La regola `vipi-theme.css:4656` porta a 16px solo `.searchbar input, .top-search input, input.app-in, select.app-in,
textarea.app-ta, .htree-search, .htree-select`. Restano sotto 16px, su pagine pubbliche o da telefono:

| Campo | Regola | Pagina |
|---|---|---|
| filtro dei trasferimenti live | `:1779` `.xl-filter input{font-size:12.5px}` | `/services/vsop/live` |
| ricerca SID della vista rapida | `:2209` `.sid-find input{font-size:13px}` | live, chip d'aeroporto |
| selettore postazione (staff) | `:1851` `.reduced-top .live-pick input{font-size:12.5px}` | live |
| filtri archivio | `:795` `.arch-filtri input,select{font-size:13.5px}` | `/services/stats/world` |
| ricerca VID | `:774` `.vid-find input{font-size:13.5px}` | `/services/stats/division` |
| selettore scalo vAWOS | `vipi-awos.css:94-96` `font-size:11px` | `/services/vawos` |

Atteso su iPhone: al tocco la pagina si ingrandisce e resta ingrandita dopo.

## F — Bersagli al tocco troppo piccoli (< 24×24, WCAG 2.5.8)

| Elemento | Regola | Pagina |
|---|---|---|
| freccetta del dettaglio area | `:3816` `.milarea-exp{padding:2px 3px;font-size:12px}` (≈ 16×18) | vSOP militare pubblico |
| accendi/spegni spazio AIP | `:3860` `.aorasp-vis{width:16px;height:16px}` | tabella «spazi aerei» sotto l'AoR |
| ✕ del filtro live | `:1784` `.xl-fclear{padding:2px}` | live |
| «?» di aiuto | `:2976` `.help-hint>summary` (icona 16px, nessun padding) | ovunque |
| lingua IT/EN | `:2648` `.lang-ctrl a{height:26px}` | barra (≥ tb-4 sta nel menù) |
| chip AoR | `:2947` `.aor-chip{padding:4px 11px}` (~26px) | mappe |

⚠️ In più, `.milarea-exp:focus-visible` e `.aorasp-vis:focus-visible` (`:3819`, `:3863`) usano `var(--accent)`, che
fuori da `.nav-card` non esiste: la dichiarazione è invalida e **l'anello di fuoco sparisce** su quei due tasti.

## G — Tablet verticale (≤ 900): tabelle e allineamenti

| # | Regola | Rischio da guardare |
|---|---|---|
| G1 | `vipi-theme.css:4620` `.wrap table{display:block;overflow-x:auto;min-width:0}` sotto i 900px | una tabella `display:block` non è più larga il 100%: il box di tabella anonimo si stringe sul contenuto e `table-layout:fixed` non vale più. Da guardare a **768**: tabelle consecutive dello stesso blocco (coordinamenti `.coord-table` con le colonne al 13/17/21/27/15%, frequenze, configurazioni `.cfg-table`) con larghezze e colonne diverse fra loro, bordo destro frastagliato |
| G2 | `:4620` vale anche per `.tbl-cards` fuori dal `@container` (pagine stats fra 700 e 900 di contenitore) | tabelle stats non a piena larghezza a 768-900 |
| G3 | `.doc-head` (`:1213`) diventa a capo solo dentro la media query a 900 (`:4642`) | a **zoom alto su desktop** la media query non scatta (lo dice il foglio stesso): titolo e tasti della vIPI possono sforare con zoom 1.6-1.8 su 1280 |

## H — Da guardare, rischio minore

- `/services/vawos` sotto 860: la barra dei tasti va a capo su 3 righe; `.awos-rvr-v{min-width:92px}` con 3-4 celle per
  pista va a capo (atteso, ma controllare che i pannelli vento restino leggibili).
- `SopLayout` a `tb-4`: il pannello del menù (`:4531`) non si chiude toccando fuori (è un `<details>`); si chiude solo
  cambiando pagina o col «☰».
- `.help-hint.wide .help-pop{width:360px}` (`:2995`) è contenuto da `max-width:74vw` (`:2980`): ok sul telefono, ma
  la correzione di `placeHelpPop` (`vipi-ui.js:439-471`) va provata con il «?» vicino al bordo destro a 360.
