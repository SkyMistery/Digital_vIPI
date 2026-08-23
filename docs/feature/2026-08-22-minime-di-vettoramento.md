# Minime di vettoramento (MRVA): la carta, non la tabella

**22 agosto 2026 — ✅ chiuso, verifica live eseguita.**

Ultima voce del piano. Le minime di vettoramento arrivano nei documenti: quelle **enroute** nella sezione
`minima` del blocco **Aerovia** della vIPI ACC, quelle **d'aeroporto** nei documenti APP — gruppo-APP dentro
la vIPI ACC e APP standalone.

## 1. Perché la decisione del 9 agosto andava rivista, e perché solo a metà

`lavori-aperti.md` §E2 aveva scartato l'import: *«nel sectorfile la struttura dei file MVA non dice a quale
settore appartiene un'area»*. È vero, e **resta vero** — ma vale contro un prodotto preciso: la **tabella**
`area → quota`. Misurato sui 28 file veri del sector italiano:

| Ostacolo | Misura |
|---|---|
| L'etichetta non è un attributo del poligono | in `liph.mva` le **dieci** `L;` stanno tutte in cima al file, prima di qualsiasi vertice |
| Il legame etichetta↔area va indovinato | su 345 etichette: **261** dentro una sola area, **70** dentro più aree annidate, **13** dentro nessuna |
| Il testo non è un numero | `TRL`, `NO MINIMA`, `80/TRL`, `*30/40`, `FL85` |
| Nessun campo dice le unità | `110` = centinaia di piedi, `1500` = piedi, nello stesso formato |
| Non tutti i tracciati sono aree | **92 su 315** sono aperti: archi e linee di confine (`LINEA2` di `lirs.mva` ha due punti) |

Quello che il formato **dichiara**, invece, è il proprietario del file: `ENRMVA/{acc}.mva` è l'enroute di un
ACC, `{icao}.mva` è un aeroporto. A quella granularità — che è poi quella dei documenti — l'attribuzione non
si indovina, **si legge**. Verificato sul `vipi.db`: i 24 file per-aeroporto corrispondono tutti a un APP
esistente, **zero orfani**.

Da qui la scelta: **una carta per file**, disegnata verbatim. Non asserisce nulla che il sectorfile non dica,
ed è esattamente ciò che il controllore vede in Aurora quando accende le MRVA di un ente.

## 2. Cosa c'è

| Pezzo | Dove |
|---|---|
| Parser puro | `AuroraSectorfileParser.ParseMva` → `MvaChart`/`MvaShape`/`MvaLabel` |
| Porta + adapter | `IVectoringMinimaSource` → `AuroraMvaProvider` (raw GitHub, cache per percorso) |
| Composizione | `MinimaCharts` — la regola «una carta per file» in **un posto solo**, condivisa da ACC e APP |
| Proiezione SVG | `MinimaChartProjector` (stampa e resa senza JavaScript) |
| Resa | `MinimaSection.razor` + `vipi-mva.js` |

Sezione `minima`: da `Editorial` a **`Derived`**, corpo reso dalla pagina. Riprende il toggle Live/Congelata
e viene catturata nello snapshot di release. **Nessuno storage**: le tabelle `VectoringMinimaSet/Row`, che
descrivevano la strada scartata, sono state droppate (modello dati §7.5).

## 2-bis. Come si legge la carta (rifatto dopo il primo giro a schermo)

Il primo rendering funzionava ma non si leggeva: tracciati arancioni sottili e numeri senza fondo su una
mappa stradale colorata. Quattro correzioni, tutte dal riscontro del committente:

- **Fondo senza strade.** La rete stradale non c'entra con le minime e ruba contrasto. Il fondo di partenza
  sono **due tile impilate**: `World Terrain Base` (terra, mare, vegetazione) più `World Hillshade` al 55%
  (il rilievo). Provate singolarmente non bastavano — la prima a questi zoom è quasi bianca, la seconda è
  grigia uniforme e fa sparire la costa. Resta selezionabile **OpenTopoMap** («Curve di livello»): ha le
  strade, ma è l'unico con le **quote scritte** sulle isoipse.
- **Tracciati con il *casing*.** Ogni linea è disegnata due volte, fascia bianca sotto e rosso `#c1121f`
  sopra. Su un fondo che passa dal verde al marrone al blu una linea sola cambia contrasto a ogni valle.
- **Etichette a pastiglia** invece dell'alone: il contorno bianco reggeva sul mare e cedeva sui bruni della
  montagna, cioè dove le quote contano.
- **Larghezza piena**, uguale a quella della mappa AoR (misurato: 729px per entrambe sulla stessa pagina);
  si adatta la sola altezza, 360–620px secondo la forma del dato.
- **AoR accendibile.** I settori della sezione `aor` **della stessa parte di documento** si accendono,
  spenti all'apertura: servono a vedere come le minime si rapportano ai confini. Solo contorno
  tratteggiato — accesi in più d'uno i riempimenti annacquavano le minime — e **fuori dall'inquadratura**,
  che resta quella delle minime. Costo misurato sui documenti veri: al massimo 2 fondi + 7 settori.
  (Il comando era il pannello dei livelli di Leaflet, nato **aperto** perché chiuso dietro l'iconcina gli
  AoR risultavano semplicemente assenti; dal 24 agosto sono le chip dell'AoR — vedi §2-ter.)

E la carta **non porta didascalia**: `LIBD — Bari Palese` diceva una cosa più stretta del vero, perché il
file copre un'area che va oltre lo scalo che gli dà il nome. Resta il titolo della sezione e la mappa.

## 2-ter. Le selezioni sono le chip dell'AoR (24 agosto 2026)

Le due sezioni stanno **una sotto l'altra nello stesso documento** e mostravano due comandi diversi per la
stessa cosa: l'AoR le chip, le minime il pannello dei livelli di Leaflet. Ora anche le minime hanno le chip,
con le stesse classi (`.aor-toggles`/`.aor-chip`, `.cfg-sel`/`.cfg-btn`) e lo stesso gesto — **settori sopra
la mappa, fondo mappa sotto**, come l'AoR mette sopra i settori e sotto le configurazioni.

- `L.control.layers` non è più usato. I fondi restano due, ma le chiavi di `basemaps()` sono ora
  **identificatori** (`relief`/`contour`): il nome visibile lo scrive `MinimaSection.razor` sulle chip, ed è
  **localizzato** — nel pannello Leaflet era una stringa italiana anche in inglese.
- La mappa espone la **stessa interfaccia** dell'AoR (`_secMap` + `_aorSetSec`), più `_mvaSetBase` per il
  fondo: è quella che le chip conoscono, e tenerla uguale è il motivo per cui il gestore delegato di
  `vipi-mva.js` è la copia corta di `onAorClick`.
- I due gestori delegati **non si pestano**: quello dell'AoR esce se non trova un `.aor-block` sopra la
  chip, quello delle minime se non trova un `.mva-block`. Per questo le chip nuove sono `.mva-chip`/
  `.mva-all`/`.mva-base` e prendono l'aspetto dalle classi dell'AoR affiancate, non il gesto.
- Le chip dei settori nascono **spente** (sono contesto, non contenuto) e la loro riga è `.noprint`: su
  carta una fila di chip tutte spente non è una legenda, è rumore. Nell'AoR restano perché lì dicono quali
  settori sono disegnati. `.cfg-sel` era già escluso dalla stampa.
- La pastiglia colorata della chip risolve il colore-token in `var(--token)`: il colore di un settore può
  essere un hex o il **nome** di un token del tema, e in CSS il nome nudo non colora niente. (In JS la
  stessa cosa la fa `color()` in `vipi-mva.js`, perché lì il valore finisce in un attributo SVG.)
- `Minima_Hint` non parla più del «controllo in alto a destra»; la frase sui settori AoR è una chiave a
  parte (`Minima_AorHint`) e compare **solo se ci sono settori**.

Il rilievo sotto non è decorazione: sulla carta di Milano si legge a colpo d'occhio — 195/180 sulle Alpi,
25/30 in pianura padana, 90/110 sull'Appennino.

## 3. Trappole pagate

- **`ValueTuple` si serializza come `{}`.** La carta si congela nella release e la cattura usa
  `System.Text.Json`: coi vertici in tupla lo snapshot sarebbe tornato **senza vertici**, e il guasto si
  sarebbe visto solo su un documento pubblicato. Esiste `MvaPoint` per questo, e un test che fa il giro.
- **`marker.getElement()` è `null` finché la mappa non ha una vista.** Leaflet rimanda `onAdd`: scrivere il
  testo dell'etichetta dopo `addTo()` — con `fitBounds` più in basso — perdeva **tutte** le etichette, in
  silenzio. Il testo va dentro l'icona.
- **Il fit giusto può essere illeggibile.** LIBB è 5,5° di latitudine per 3,1° di longitudine: in un
  contenitore largo e basso l'inquadratura lavora sull'altezza ed è corretta, ma i tracciati restano grandi un
  ventesimo della mappa. Si adatta quindi la **sola altezza** (360–620px): la larghezza resta piena, uguale a
  quella della mappa AoR. Restringerla — come si è provato in mezzo — faceva due mappe di formato diverso
  nella stessa pagina, ed è stato il committente a farlo notare.
- **Un tooltip su ogni tracciato è rumore.** Il nome del gruppo (`ZONA1`, `RR US0`, l'ICAO…) è un dettaglio
  interno del file, e appariva come un riquadro appiccicato al puntatore sopra la cosa che si sta guardando.
  Via il tooltip, e tracciati `interactive: false`.
- **Le tile sono chiare in entrambi i temi.** L'etichetta con token di tema diventava bianca su fondo chiaro:
  colori letterali, unica eccezione voluta, col perché scritto accanto nel CSS.
- **`<text>` in un blocco di codice Razor è la parola chiave di escape**, non l'elemento SVG: va annidato.
- **Una funzione dietro un'icona, per chi guarda, non c'è.** L'AoR accendibile era completo e invisibile
  finché il controllo dei livelli restava chiuso. Verificato riaprendo l'editor con la stessa domanda.
- **Il velo grigio sull'editor al primo accesso è il TOUR** (`vt-overlay` di `vipi-tour.js`), non un difetto
  della sezione: fa sembrare tutto disabilitato e allunga la diagnosi.
- **Un fondo «senza strade» può essere anche senza terreno.** `World Terrain Base` da solo, a questi zoom, è
  quasi bianco: tolte le strade erano sparite anche le montagne. Si è visto solo guardando lo screenshot.
- **Tre forme di coordinata**, non due come diceva il censimento del sector: DMS coi punti, DMS **compatta**
  (`liph.mva` — senza, quel file dava zero poligoni in silenzio) e **gradi decimali nudi**, una riga sola in
  tutti i 28 file (`lipx.mva:14`). Da segnalare all'AOD.

## 4. Verifica live

Copia del `vipi.db` reale, sorgente sectorfile **accesa** (deroga della skill: è il sectorfile l'oggetto della
verifica). I numeri a schermo combaciano con quelli misurati sui file: LIBB 7 tracciati / 10 etichette / 190
vertici, LIMM 43 / 51 / 1348, LIBD 6 / 7 / 478. Etichette verbatim (`NO MINIMA`, `80/TRL`), selettore di fondo
che passa a Esri, callout «nessuna carta» sull'APP senza file (LIBP). La migrazione di drop è stata applicata
davvero sulla copia.

## 5. Resta aperto

**25 APP su 49 non hanno il file** — fra cui LIRF, LIMC, LIML, LIME, LIPS — e nel sectorfile «non serve» è
indistinguibile da «non l'ha ancora fatto nessuno». Se quelle carte servono, la richiesta va all'AOD: dal lato
del codice non c'è niente da fare, e inventarle sarebbe peggio che non averle.

## 6. Verifica live delle chip (24 agosto 2026)

Stessa procedura (copia del DB, sorgente sectorfile accesa), su `/services/vsop/libb/vipi`, che porta **due**
blocchi minime (ACC LIBB e APP Brindisi CS0). Guidato in Edge: `.leaflet-control-layers` → **0** in pagina;
chip settore accende/spegne il layer (`_secMap` letto dal vivo: `LIBB_ES_CTR=ON` → `off`), «Tutti» accende
tutt'e quattro i settori e «Nessuno» li spegne, la chip «Curve di livello» porta le tile su
`tile.opentopomap.org` e «Rilievo» le riporta su `server.arcgisonline.com`; nessun errore di console, nessuna
risposta ≥ 400. Una cosa vista **solo dallo screenshot**: «Tutti»/«Nessuno» erano due pulsanti squadrati e non
i due collegamenti sottolineati dell'AoR — la regola `.aor-chip-actions .aor-all` non li nominava.
