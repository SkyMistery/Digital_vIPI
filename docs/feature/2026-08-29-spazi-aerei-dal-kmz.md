# Gli spazi aerei dell'AIP: un file caricato a mano (29 agosto 2026)

> Stato: ✅ **CHIUSA E FUSA IN `main`** il 30 agosto 2026 (merge `86576ecc`, ramo `spazi-aerei-aip`).
> Tutte e sette le slice, verifica dal vivo compresa.
> Origine: richiesta del committente del 29 agosto 2026, e le nove risposte della stessa sera.

## 1. Perché

Certi avvicinamenti **controllano esattamente il CTR**, non il blocco unico che l'anagrafica IVAO
espone come loro area. `LIBA_APP` è Amendola, e il suo CTR sono **due zone** sovrapposte in quota;
`LICC_APP` è Catania, e il suo CTR sono **sette**. Da IVAO arriva un poligono solo, generoso, che
non è il confine che quel controllore ha davvero.

Il committente ha portato un file — `it (2).kmz` — che quei confini li contiene tutti.

## 2. Il file, misurato

Prodotto da **AirspaceConverter 0.4.4**, generato il **15 luglio 2026**. 1,3 MB compresso, 12 MB
aperto, 19 voci nello zip (un `doc.kml` e diciotto icone).

Dentro: **1 536 volumi di spazio aereo** e **684 punti** (569 campi, 78 VOR, 37 NDB).

| famiglia | quanti | esempio |
|---|---|---|
| CTR | 113 | `ALGHERO CTR`, `CATANIA CTR Z1…Z7` |
| CTA (classi A/C/D) | 162 | `CTA MILANO Z3 GARDA` |
| TMA | 30 | `TMA ROMA Z1 GIGLIO` |
| ATZ / MATZ (classe G) | 46 | `ATZ CROTONE LIBC`, `MATZ CERVIA-TWR` |
| FIR | 3 | Milano, Roma, Brindisi |
| TMZ | 9 | `FMC MINW---SSR4610` |
| R / P / D | 663 | `LI-R14A-S.SEVERA (FURBARA)` |
| OTH | 506 | acrobazia, airwork, `LI TRA424 - CAMERI` |
| Gliding | 4 | |

Ogni volume porta **nome, categoria, base, tetto** in forma leggibile (`GND`, `1500 FT AGL`,
`FL105`, `FL999` = UNL), più area e perimetro. Le radioassistenze portano **codice, tipo,
frequenza, canale, portata, declinazione, elevazione e posizione**: `ALGHERO` è tre impianti
distinti — `AHO` TACAN, `AEA` DVOR-DME, `ALG` VORTAC — cioè esattamente l'identità
*codice + famiglia + canale* che l'anagrafica nostra ha imparato il 30 agosto.

## 3. La trappola: il file non contiene contorni, contiene scatole

⚠️ Un volume con la base staccata da terra è scritto come **tetto + pavimento + una parete per
lato**. `TMA MILANO Z1` sono **147 poligoni** per una sola area. In tutto il file: **26 989 poligoni
per 1 536 aree**.

Il `KmlReader` che già abbiamo — quello del convertitore di coordinate — numera le geometrie di un
`MultiGeometry` come *aree distinte*, e su questo file produrrebbe `TMA MILANO Z1 (1)…(147)`. Non è
un suo difetto: è la scelta giusta per il suo caso d'uso, e **non va cambiata**.

La regola per uscirne è pulita, ed è **verificata su tutti e 1 536**:

> si tengono i poligoni i cui vertici hanno **una sola quota** (le pareti ne hanno due), e si
> **deduplica l'anello 2D** (il pavimento ripete il tetto).

Esito: **esattamente un anello per volume, sempre, senza eccezioni** — 397 volumi hanno il
pavimento, 1 139 no, e in tutti e due i casi resta un anello. Restano **144 892 punti**, circa 3 MB
di JSON — i 146 428 del file meno il vertice di chiusura di ogni anello, che è una proprietà dell'anello e
non un punto in più.

⚠️ E le quote **non si leggono dalle coordinate**: lì la base `GND` è la quota del *terreno*
(`ALGHERO CTR` sta tutto a 762 m, che è il suo tetto). Si leggono dai campi `Base`/`Top`.

## 4. Le decisioni del committente (29 agosto 2026)

| # | domanda | risposta |
|---|---|---|
| 1 | AoR vera degli APP che coincidono col CTR | **sì, ma su scelta umana**: un admin decide di sostituire la shape IVAO con quella del file |
| 2 | ATZ al posto del cerchio da 5 NM | **sì, come fonte secondaria**: solo se il sectorfile non ce l'ha |
| 3 | confronto R/P/D con IVAO | **no** — le aree regolamentate vengono **solo** dal DB IVAO |
| 4 | strato di contesto sulle mappe | **sì, come pagina a parte**: si vedono tutti i settori, quelli IVAO e quelli del file (le aree R/D/P/TRA no: quelle solo da IVAO) |
| 5 | correggere le quote delle aree IVAO | **no** |
| 6 | TRA/aree militari dal file | **no**, come la 3 |
| 7 | diagnosi di copertura | **no** |
| 8 | sorgente «dal catalogo» nel convertitore | **sì** |
| 9 | controincrocio radioassistenze/campi | **sì, ma solo segnalando**: le correzioni si fanno nel **sectorfile** e poi si reimporta |

E le tre che restavano aperte:

- **la pagina è pubblica**, citando la fonte;
- **il file lo carica un admin**;
- **il file si conserva intero** (1,3 MB: si tiene il KMZ così com'è).

### 4-bis. La decisione derivata (mia, da confermare)

Le risposte 3, 5 e 6 tolgono ogni uso a **1 169 volumi su 1 536** (R, P, D, OTH). Si legge e si
salva **tutto** — il file si conserva intero, e riparsarlo domani per una famiglia che oggi non
serve sarebbe lavoro rifatto — ma una **lista bianca di famiglie utilizzabili** decide che cosa si
può agganciare a un settore e che cosa si può mostrare: **CTR, CTA, TMA, ATZ/MATZ, FIR, TMZ**, e
basta. Le altre stanno in catalogo con scritto accanto *«non utilizzabile: le aree regolamentate
vengono da IVAO»*. La regola sta **in un posto solo**.

## 5. Il modello

Tre tabelle, nessun modello gemello di uno che c'è già.

**`AirspaceImport`** — il caricamento: nome del file, **il KMZ intero**, impronta sha256, chi l'ha
caricato e quando, il **ciclo AIRAC dichiarato**, i conteggi (letti / tenuti / doppioni). Uno è
**quello in vigore**; i precedenti restano per poter dire da dove viene una shape.

**`AirspaceVolume`** — il volume: famiglia, categoria, nome, base e tetto (piedi + riferimento
`GND`/`AMSL`/`AGL`/`FL`/`UNL`), l'anello **nella stessa forma del `regionMapPolygon` IVAO** — così
`AorPolygonProjector`, la mappa Leaflet, il 3D e la stampa funzionano **senza toccare una riga** —
più il riquadro (min/max lat/lon) per filtrare senza rileggere i punti.

⚠️ **L'identità.** Il nome da solo non basta: `GRAZZANISE CTR Z2` compare **due volte** con bande
diverse, e `CTA ROMA Z9 GOLFO MANFREDONIA` è **duplicato identico**. Chiave naturale =
`famiglia|nome|base|tetto`, scritta come colonna (stessa scelta di `Navaid.NaturalKey` e
`GlossaryTerm.SourceKey`: la chiave si scrive, non si lascia decidere al confronto del database).
Il doppione esatto prende un ordinale, e **il caricamento lo dice**.

**`SectorAirspaceBinding`** — la scelta umana: `(SourceCatalog, SectorId)` → uno o più volumi, con
chi ha scelto e quando. ⚠️ L'indirizzo è **la coppia catalogo+id**, non il callsign: è lo stesso
indirizzo con cui il ripiego shape già scrive (`ShapeWrite(SourceCatalog, int Id, …)`), e regge a
una rinomina del callsign.

E un valore nuovo **in coda** all'enum `ShapeSource`: `Aip`. ⚠️ In coda perché nel payload di
release gli enum sono **ordinali**.

## 6. Dove si innesta: la catena delle shape

Oggi, in `AirportSectorImportHostedService`, l'ordine è esplicito e sta in un posto solo:

```
import IVAO  →  twrs.tfl da GitHub  →  DYNAMIC_SEC (settori)  →  cerchio 5 NM
```

Diventa:

```
import IVAO  →  twrs.tfl  →  DYNAMIC_SEC  →  [ATZ dal file]  →  cerchio 5 NM  →  [timbro delle scelte umane]
```

Due innesti, e due nature diverse:

- **L'ATZ (decisione 2) è un ripiego**: riempie solo i settori che non hanno un'area che si disegni,
  esattamente come il cerchio, e sta **dopo** il sectorfile — che è quel che il committente ha
  chiesto: *fonte secondaria, solo se non la trovi nel sectorfile*. Se domani IVAO manda una shape
  vera, IVAO vince, come deve essere per un ripiego. Nessun aggancio da mantenere a mano: **74 ATZ
  su 91 portano l'ICAO nel nome** (`ATZ CROTONE LIBC`) — la misura «42 su 46» della prima stesura
  guardava la sola classe G, e gli ATZ stanno in due classi. I **17** che non ce l'hanno sono quasi
  tutti MATZ di basi militari (Amendola, Aviano, Cameri, Decimomannu…), che si agganciano a mano.

- **La sostituzione (decisione 1) è una scelta**, e la fa una persona.

### 6-bis. ⚠️ Correzione alla carta: la sostituzione NON si scrive nella colonna della shape

La prima stesura di questa carta diceva di scrivere il volume scelto dentro `RegionMapPolygon`, il
campo che tutti già leggono, per non toccare i 113 punti che lo citano. **Quel piano regge solo per
un volume solo, e i due casi che hanno fatto nascere la richiesta non lo sono**: Amendola sono
**due** zone, Catania **sette**.

La colonna tiene **un anello**, e `PolygonGeometry.ParsePoints` di fronte a un annidamento in più
scende su `items[0]` — c'è scritto nel suo commento, con la misura che lo giustificava: *zero casi su
1 338 poligoni reali*. Mettere sette anelli in quella colonna vorrebbe dire pubblicare **una zona su
sette**, disegnata benissimo, senza un errore da nessuna parte. È esattamente il modo di sbagliare
che questa applicazione ha già pagato tre volte.

Quindi la scelta **non scrive la shape del settore**: sta in una tabella sua e viene **letta dove
l'AoR si costruisce**, cioè dove i poligoni di un settore sono già una **lista**
(`AccSectorAor.Polygons`). I posti che la leggono sono **due** — la vista AoR dell'APP e quella
dell'ACC — e da lì scendono da sé in mappa 2D, viewer 3D, SVG e stampa.

**Che cosa continua a stare sulla shape IVAO**, e non per dimenticanza: i **confinanti**,
l'**attribuzione del traffico** e la **vLOA**. Sono motori tarati su quel poligono, e cambiarglielo
sotto vorrebbe dire cambiare in silenzio chi confina con chi e quali voli contano per una postazione
— cioè molto più di quel che il committente ha chiesto. La pagina lo scrive: *l'AoR pubblicata è
quella dell'AIP; i confinanti e le statistiche restano sulla forma di IVAO.*

**Il legame cita la CHIAVE NATURALE, non l'id della riga.** Un caricamento nuovo crea righe nuove:
con l'id, ogni aggancio si romperebbe a ogni ri-caricamento. Con la chiave, l'aggancio sopravvive
finché quel volume esiste — e quando non esiste più il settore **torna alla forma di IVAO** e la
pagina dice quali agganci sono rimasti scoperti.

## 7. La pagina pubblica (decisione 4)

`/services/airspace` — accanto al convertitore, che è l'altro servizio pubblico.

Si vedono **i settori nostri** (da IVAO, dal sectorfile, sintetici — con scritto quale) **e i volumi
del file**, accendibili per famiglia. Le aree regolamentate, se ci vanno, arrivano **dal DB IVAO**
come già fa la mappa delle aree.

⚠️ **Il peso.** I 146 428 punti dell'intero file al browser non ci vanno. Le sole famiglie
utilizzabili sono **362 volumi, 31 613 punti, ~620 KB** di JSON grezzo — e si mandano **per
famiglia**, su richiesta, non tutti all'apertura.

⚠️ **Pubblicare è ridistribuire.** Il file porta un disclaimer esplicito e vieta l'uso commerciale
del dato. Noi non lo siamo, ma la fonte va **citata dove il dato si vede**: in fondo alla pagina
pubblica, e — questo è il punto che si dimentica — anche **nella mappa AoR di un documento** che
mostra una shape sostituita, perché quella shape è pubblica quanto la pagina.

## 8. Pre-flight — le quattro domande

**1. Modello.** `AirspaceVolume` è un concetto nuovo: è la *geometria AIP*, non il catalogo
operativo. Non affianca `SpecialArea` — e non lo affianca **proprio perché** le risposte 3, 5 e 6
dicono che le aree regolamentate restano di IVAO: il file non ne è una seconda copia, ne è una
famiglia esclusa. La shape di un settore continua a stare **dove sta oggi**, in una colonna sola.

**2. Dispatch.** Nessuno switch per tipo nuovo: la lista bianca delle famiglie è **un dato**, non
una catena di `if`, e sta in un posto solo.

**3. Ingressi e verifica.** Il caricamento sta in `/services/vsop/admin/airspace` (Editor in su); la
sostituzione si fa dalla pagina della **struttura settori**, dov'è già la shape; la verifica è dal
vivo su **LIBA e LICC**, i due casi che hanno fatto nascere la richiesta — e su un campo con ATZ e
senza poligono, per il ripiego.

**4. Propagazione.** Non si rimuove né si rinomina niente. Il valore `Aip` è **additivo e in coda**.

## 9. Le slice

| # | cosa | migrazioni |
|---|---|---|
| **S1** | **Il lettore.** Scatole → anello, `ExtendedData`, quote parsate (piedi + riferimento), famiglie, chiave naturale. Metodo **nuovo** dentro `KmlReader`, che condivide il parsing delle coordinate e **non cambia** il comportamento del convertitore. Puro, tutto in test, fixture ritagliata dal file vero. | — |
| **S2** | **Il catalogo e il caricamento.** Le due tabelle, la pagina admin: carica, esito in chiaro («1 536 letti, 362 utilizzabili, 3 doppioni»), elenco filtrabile, anteprima in mappa. Il KMZ si conserva intero. | 1 |
| **S3** | **La sostituzione a mano.** Il legame (per chiave naturale), la sua lettura nelle due viste AoR, e il blocco «Settori agganciati» nella pagina degli spazi aerei — dove stanno i volumi, perché la scelta è *quali volumi*, non *quale settore*. Con «torna a IVAO» e l'avviso sugli agganci scoperti. → **LIBA e LICC chiusi.** | 1 |
| **S4** | ✅ **L'ATZ per le TWR.** Passo automatico fra il ripiego del sectorfile e il cerchio; aggancio per ICAO nel nome (74 su 91). ⚠️ Un ICAO con **più di un ATZ si salta**: la colonna tiene un anello. | — |
| **S5** | ✅ **La pagina pubblica** `/services/airspace`, per famiglia, con l'attribuzione. Riusa `AccAor` per intero. | — |
| **S6** | ✅ **Il convertitore**: sorgente «dal catalogo spazi aerei» accanto alle tredici che già legge. | — |
| **S7** | ✅ **Il rapporto radioassistenze**: che cosa il file dice e l'anagrafica no (e viceversa). **Solo segnalazione**: si corregge nel sectorfile e si reimporta. | — |

S1–S3 sono la richiesta e stanno in un ramo solo. **Due migrazioni**: la coda cresce di due.

## 10. Cosa può andare storto

- ⚠️ **Il timbro fuori posto.** Se la sostituzione gira prima dell'import IVAO, ogni notte il
  confine scelto sparisce e nessuno capisce perché. Va **ultima**, e il test lo deve dire.
- ⚠️ **La scelta che si dimentica di essere una scelta.** Un settore sostituito **smette** di
  ricevere gli aggiornamenti di IVAO: dev'essere scritto a schermo, non dedotto.
- ⚠️ **Il ri-caricamento che orfana i legami.** Un file nuovo può non contenere più un volume
  citato. Al caricamento si dice **quali legami restano scoperti**, come già fa la casella degli
  impatti — e il settore torna a IVAO invece di restare senza area.
- ⚠️ **I 146 mila punti al browser.** Solo per famiglia, solo le utilizzabili.
- ⚠️ **`ShapeSource` è un ordinale nel payload di release**: il valore nuovo va **in coda**.
- ⚠️ **Il `KmlReader` non deve cambiare comportamento** per il convertitore: il metodo degli spazi
  aerei è nuovo e affiancato, e i test del convertitore restano quelli.

## 11. La verifica dal vivo (29 agosto 2026, sera)

Host in Development su una copia del `vipi.db` reale, Edge guidato con puppeteer-core, e il **file vero**
caricato dalla pagina.

**Quel che ha funzionato**, e non era scontato:

| passo | esito |
|---|---|
| lettura del KMZ da 1,3 MB dalla pagina | **3,2 secondi**, dalla scelta del file alla tabella a schermo |
| conteggi | **1 536 letti, 362 utilizzabili, 3 chiavi in doppio, 144 892 punti** — gli stessi della misura |
| famiglie in chip | CTR 114 · CTA 112 · TMA 30 · ATZ/MATZ 94 · FIR 3 · TMZ 9, più le non utilizzabili con detto perché |
| il caso vero | cercando «CATANIA CTR» escono **sette** zone, con le loro bande |
| l'aggancio | le sette spuntate, `LICC_APP` scelto fra 211 settori, salvato con chi e quando |
| **la mappa** | la vIPI pubblica di Catania disegna **sette anelli**, e l'editor pure |
| errori di console | **zero**, su tutte e quattro le pagine |

**Il difetto che ha trovato.** Il **ciclo AIRAC digitato non veniva salvato**: `@bind` di default scrive al
**fuoco perso**, e qui il gesto subito successivo è scegliere un file — che apre una finestra di sistema. Il
caricamento è finito in archivio con `AiracCycle` nullo. Corretto con `@bind:event="oninput"`, che è quel che
il campo di ricerca due righe più sotto faceva già. ⚠️ Nessun test lo vedeva, e non poteva: è una regola di
quando Blazor propaga il valore, non di che cosa il codice fa con esso.

**Una cosa da sapere per l'esercizio.** Su `LICC_APP` la mappa pubblica ha mostrato le sette zone
**subito**, perché la sua release aveva la sezione AoR con `BodyJson` nullo — cioè non congelata — e il
viewer è caduto sulla derivazione viva. Dove invece una release **congela** l'AoR, l'aggancio si vede sulla
pagina pubblica solo dopo aver **ripubblicato**: è la stessa regola del ciclo AIRAC della vLOA, e non un
comportamento nuovo.

## 12. La verifica dal vivo di S4–S7 (29 agosto 2026, notte)

Stesso banco: host in Development su una copia del `vipi.db` reale, il **file vero** caricato dalla pagina.

| che cosa | esito misurato |
|---|---|
| **S4 · l'ATZ per le torri** | **13 torri su 84** hanno ricevuto la loro ATZ dal giro automatico dell'host — il log lo dice a chiare lettere — e restano **tre cerchi** (`LIAA_I`, `LIEF_MIL`, `LILA_I`), che nel file un ATZ non ce l'hanno |
| **S5 · la pagina pubblica** | i CTR si accendono all'apertura (**114 anelli**), le ATZ si aggiungono a richiesta (**208**), chip e preset ci sono, l'attribuzione in fondo |
| **S6 · il convertitore** | tendina con **363 voci** in sei gruppi; presa un'ATZ, escono **150 righe** in forma sectorfile |
| **S7 · il rapporto** | **218 differenze** su 115 radioassistenze |
| il ciclo AIRAC | ✅ **si salva**: la correzione di ieri sera regge |
| errori di console | **zero**, su tutte le pagine |

### ⚠️ Il difetto che ha trovato: il rapporto seppelliva i cinque che contano

Al primo giro il rapporto diceva **54 «canale diverso»**. Guardandoli: **49 erano «l'AIP ha un canale e noi
no»** — una **lacuna**, non una discordanza — e solo **5** erano canali davvero diversi fra i due archivi.
Quei cinque sono esattamente quel che il rapporto deve far vedere: `AEA 53Y↔54Y`, `ELB 94X↔88X`,
`ISA 54X↔80X`, `PNZ 93X↔87X`, `VIL 105X↔102X`.

Ora le due cose sono due codici distinti, e **l'ordine delle voci è la gravità**: prima le discordanze
(1 frequenza, 5 canali, 2 posizioni — fra cui `DEC` a **1,7 NM**), poi le assenze, poi le lacune. Con le
lacune in mezzo, otto righe vere stavano annegate in centouno.

⚠️ E il «da guardare a mano» è **uno solo**, ed è `GRO`: Grosseto è un VOR **e** un TACAN, in tutti e due gli
archivi. È il caso che la regola dell'accoppiamento uno-a-uno esiste per non sbagliare.
