# Aree regolamentate: una mappa sola, con le chip — carta (27 agosto 2026)

> **Stato: ✅ ESEGUITA il 27 agosto 2026**, ramo `riordino-e-aree`.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Riusa la mappa dell'AoR — `AccAor` / `AccAor3d` + `vipi-aor.js` / `vipi-aor3d.js`, vedi anche
> [la leggibilità del 3D](2026-07-31-aor3d-leggibilita.md): **nessun motore di mappa nuovo**.

## La domanda

> «La sezione aree regolamentate la vorrei rielaborare. Una mappa come AoR (versione 2D/3D) con chip per
> attivare/disattivare le aree e sotto le descrizioni delle aree selezionate, che partono compatte e l'utente
> apre quelle che gli interessano. Ovviamente la minimappa in ogni descrizione si può togliere.»

## §0 — Cosa c'era, e perché non andava

Un `<details>` per area, **ognuno con la propria mappina Leaflet**. Misurato sul `vipi.db` di sviluppo:

| ACC | aree |
|---|---|
| LIRR | **105** |
| LIBB | 69 |
| LIMM | 27 |
| LIPP | 25 |
| LIZZ | 15 |

Centocinque contenitori mappa in una pagina, ognuno che chiede il suo lotto di tessere per disegnare
**cento volte lo stesso pezzo di Mediterraneo**. È il caso che `vipi-aor.js` cita nel commento
sull'accensione a scaglioni — *«misurate su LIBB: 77, quasi tutte mappine di aree regolamentate»* — e per cui
esistono lo scaglionamento, il ritentatore delle tessere e una misura di stampa dedicata.

E il difetto peggiore non era il costo: con una mappina per riquadro **due aree non si possono confrontare**.
La domanda vera di chi guarda la sezione — *«questa zona R sta dentro il mio settore? si sovrappone a
quell'altra?»* — non aveva risposta.

## §1 — La forma nuova

Una mappa, le chip sopra, le descrizioni sotto.

```
[ Mappa 2D | Vista 3D ]                     ← barra già esistente (vipi-aor3d.js)
[chip area] [chip area] … [Tutte · Nessuna] ← una per area, colore = tipo
┌──────────────────────────────────────┐
│            UNA mappa Leaflet         │
└──────────────────────────────────────┘
TIPO:  [R] [D] [P] [TSA] [TRA]  azzera    ← preset: accendono solo quel tipo
«Aree accese: 43 su 69.»
▸ ● R  LI R300A Amendola   GND – 4000 ft   ← chiusa; si apre chi vuole
▸ ● D  LI D409A            1500 – 14500 ft
```

Le descrizioni mostrate sono **solo quelle accese**: spegnere una chip toglie il poligono dalla mappa *e* la
riga dall'elenco. Il conteggio dice sempre quante sono, perché un elenco che passa da 105 a 3 senza spiegarlo
sembra rotto.

## §2 — Nessun motore nuovo: l'area diventa un «settore»

La mappa 2D, il visore 3D, le chip, Tutti/Nessuno e il commutatore ci sono già e sono **guidati dal DOM**.
Riusarli costa una traduzione, ed è tutta lì: `RegulatedAreasMap` (`Vipi.Application/Aor`, pura).

| campo del «settore» | cosa ci finisce |
|---|---|
| `Callsign` | l'**id IVAO** dell'area — chiave tecnica, quella che va in `data-sec` |
| `Label` *(nuovo)* | il nome leggibile, senza il prefisso `LI ` |
| `Name` | il nome intero (resta nel `title` della chip) |
| `Color` | `SpecialAreaColorScheme.For(tipo)` |
| `Polygons` | la shape, o **nessuna** |
| `LowerFl`/`UpperFl` | `AorFlBand.Normalize(MinimumAlt, MaximumAlt)` |

⚠️ **La chiave è l'id, non il nome.** I nomi contengono spazi e punti (`LI R301B SMarco in Lamis Bis`) e
finirebbero dentro un selettore `[data-sec="…"]`. L'id IVAO è un numero: non ha niente da rompere.

⚠️ **Le quote delle aree sono in PIEDI** (29 000, 1 500), il visore 3D estrude su una banda FL. Senza la
normalizzazione un'area a 29 000 ft diventerebbe un prisma alto FL 29 000. L'euristica piedi/FL esisteva già
(`AorFlBand`), bastava chiamarla.

`AccSectorAor` ha guadagnato il solo campo `Label`, opzionale: per i settori è null e la chip dice il
callsign, che è il nome con cui li si chiama.

## §3 — I preset per tipo sono le chip-configurazione

Con 105 chip serve un modo di dire «solo le R». Il contratto delle chip-configurazione dell'AoR è
letteralmente quello che serve — *accendi esattamente questo insieme* — quindi i preset **sono**
`AccConfigSelection`, uno per tipo, con gli id delle aree di quel tipo. Zero JS nuovo, zero markup nuovo:
cambia solo l'etichetta della riga (`ConfigLabel`).

⚠️ **Devono stare DENTRO la vista.** La fila di tasti la disegna `AccAor` leggendo `View.Configs`: costruirli
e non metterceli è come non averli, ed **è successo** — alla prima prova dal vivo su LIRR c'erano 105 chip e
nessun modo di filtrarle. Adesso lo tiene un test, e il test guarda i **tasti**, non la funzione che li
fabbrica.

## §4 — I colori

`SpecialAreaColorScheme`, gemello di `AorColorScheme`: colori cartografici, non di brand, per la stessa
ragione (riempimento al 16%, poligoni sovrapposti), ed esadecimali veri perché Leaflet li scrive in attributi
SVG, che non sostituiscono `var()`.

| tipo | colore | |
|---|---|---|
| R | `#B0413E` rosso | Restricted |
| D | `#C9A227` giallo | Danger |
| P | `#7B4EA8` viola | Prohibited |
| TSA | `#2F6FB0` blu | Temporary Segregated Area |
| TRA | `#3E8E5A` verde | Temporary Reserved Area |

⚠️ **R, D e P seguono la pratica cartografica corrente. TSA e TRA no, e non possono**: non sono aree ICAO
Annex 4, sono costrutti FUA, e per loro un colore ufficiale **non esiste**. Blu e verde sono la convenzione
più diffusa sulle carte europee, non uno standard. Se la divisione ha una carta di riferimento che dice
altro, si cambia in un posto solo.

## §5 — Il legame chip → descrizione

Le descrizioni vivono **fuori** dai due riquadri 2D/3D (devono restare in pagina in tutt'e due le viste), e
si legano alla mappa con la **chiave di scope**, come già fanno i `<details>` di configurazione:

```
.aor-block[data-aor="reg-{blocco}"]          ← la mappa 2D
.aor-block[data-aor="reg-{blocco}-3d"]       ← lo stage 3D
[data-areacards="reg-{blocco}"]              ← le descrizioni
  └─ [data-areacard="{idIvao}"]              ← una riga
```

In `vipi-aor.js`, `setCard` sta **dentro `setSec`**: qualunque strada accenda un'area — chip, Tutti/Nessuno,
preset, 2D o 3D — passa di lì, e la descrizione la segue senza un secondo elenco di casi da tenere allineato.
Il JS toglie il suffisso `-3d` dallo scope, così le chip del 3D muovono le stesse righe.

Sull'AoR non c'è nessuna card con quei nomi: `setCard` non trova niente e costa una query.

## §6 — Quel che è stato tolto (regola di propagazione)

- `RegulatedAreas.razor`: la mappina per area, il ripiego SVG per area, «Shape non disponibile» come riquadro.
- CSS `.area-map` e `.area-noshape`: senza consumatori.
- `vipi-ui.js`: `PRINT_AREA_MAP_H` e il ramo `isArea` di `resizeMaps` — esistevano **solo** per rimpicciolire
  le mappine in stampa. Ora la sezione ha una mappa sola e prende la misura dell'AoR.
- ⚠️ **Un doppione trovato per strada**: `.area-wrap` / `.area-svg` / `.area-alt` erano definite **due volte**
  nel foglio, a novecento righe di distanza, e vinceva la seconda copia (la prima diceva `align-items:center`
  e nessuna altezza). Restano — le usa `AreaMapBlock`, che è un'altra cosa — ma ora in un posto solo.
- Il commento di `SectionCatalog` che spiegava perché la sezione nasce collassata diceva «decine di aree,
  ognuna con la sua mappa». Resta collassata, ma per il numero di **righe**.

## §7 — Stampa

- `.cfg-sel` (i preset) era già nascosto: sono comandi.
- **Le chip spente non si stampano** (`.aor-chip:not(.on)`): sulla carta la chip non è un comando, è la
  **legenda** della mappa, e una legenda che nomina poligoni assenti è rumore. Vale anche per l'AoR, ed è
  giusto: il foglio dice quello che dice lo schermo.
- Il tetto della barra chip si toglie in stampa: sulla carta non si scorre.

Misurato con `emulateMediaType('print')` su LIBB col preset **R**: 43 chip visibili su 69, 43 descrizioni
tutte aperte, barra non tagliata, mappa a 260px, nessuno scorrimento orizzontale.

## §8 — Verifica live

App vera, browser vero, copia del DB (skill `verifica-live`, `aree-verifica.js`).

| pagina | sezioni | esito |
|---|---|---|
| vIPI ACC LIBB (pubblica) | `reg-aerovia` (69 aree) e `reg-grp:b72d0a92` (4) | 1 mappa ciascuna, chip 2D=3D, preset `R D TSA TRA`, «R» → 43/69 |
| vIPI ACC LIRR (bozza) | `reg-aerovia` (**105 aree**) | preset `R D P TSA TRA`, «R» → 52/105, «P» → 1/105 |
| vIPI APP LIBA (bozza) | `reg-x` (nessuna chiave di blocco) | preset `D TSA`, tutto funzionante |
| vLOA LDZO | — | la sezione «Military areas…» **non** usa questo componente (vedi sotto) |

Per ogni sezione: Nessuno → 0 accese e compare il messaggio; Tutti → torna il totale; **una chip spenta nel
3D toglie la sua descrizione** (la prova che lo scope attraversa le due viste). `.area-map` in pagina: **0**.
Nessun errore, nessun `console.error`.

Il caso APP è stato **seminato dall'editor vero** (nessun APP dell'archivio aveva aree scelte): due aree
prese col picker, poi lette nel viewer in bozza. Copre anche il giro editor→viewer.

⚠️ **La vLOA non c'entra.** Nel catalogo `regulated` per la vLOA è **Editorial**: è un paragrafo di prosa sul
coordinamento delle aree militari transfrontaliere, non l'elenco delle aree. Il componente non passa di lì.

Schermate nei due temi con il preset **P** (una sola area, si legge tutto).

## §9 — Test (+15, ×2 TFM)

| dove | quanti | cosa fissano |
|---|---|---|
| `RegulatedAreasMapTests` | 8 | id come chiave e nome come etichetta, il taglio del prefisso `LI `, un colore per tipo (e cinque colori distinti), piedi→FL, l'area senza shape che resta, i preset dentro la vista e il loro ordine |
| `RegulatedAreasTests` (bUnit) | 7 | **una** mappa e non una per area, una chip per area, i tasti per tipo, la riga senza shape, le descrizioni chiuse, gli scope distinti fra blocchi, e il **contratto con vipi-aor.js** |

Il contratto col JS è la stessa rete del menu-sezioni: il test legge `vipi-aor.js`, pretende che `setCard` e
`syncCount` esistano **e siano chiamate**, e prova i quattro attributi contro il markup vero.

Suite intera verde: **5 785** casi. `dotnet build Vipi.slnx -c Release --no-incremental`: **0 avvisi**.

## §10 — Cosa NON cambia

- **La scelta delle aree** (`RegulatedAreasEditor`) è quella di prima: auto/manuale, aree proprie ed extra.
- **Nessuna migrazione, nessuna ripubblicazione.** Gli id li porta il documento, i dettagli e le shape
  vengono dai cataloghi correnti: le aree sono sempre **live** (doc 10 §3d), quindi le release già scritte
  mostrano la forma nuova appena il codice è in linea.

## §11 — Restano fuori (segnalati, non fatti)

- **Le tessere CARTO arrivano stampigliate «API KEY REQUIRED».** Il basemap anonimo è stato chiuso dal
  fornitore. Riguarda **tutte** le mappe del prodotto, non questa sezione: va deciso se prendere una chiave o
  cambiare fondo.
- **`99999 ft` come «illimitato»** nei dati IVAO (es. `INDIA5 31000 ft – 99999 ft`): si stampa così com'è,
  come faceva l'elenco di prima. È dato, non codice, ma varrebbe la pena renderlo `UNL` a schermo.
