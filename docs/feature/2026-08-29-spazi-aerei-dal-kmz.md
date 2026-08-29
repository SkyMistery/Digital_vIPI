# Gli spazi aerei dell'AIP: un file caricato a mano (29 agosto 2026)

> Stato: **carta approvata, codice da scrivere.** Ramo previsto: `spazi-aerei-aip`.
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
pavimento, 1 139 no, e in tutti e due i casi resta un anello. Restano **146 428 punti**, circa 3 MB
di JSON.

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
  vera, IVAO vince, come deve essere per un ripiego. Nessun aggancio da mantenere a mano: **42 ATZ
  su 46 portano l'ICAO nel nome** (`ATZ CROTONE LIBC`), e i quattro che non ce l'hanno sono tre
  MATZ e un campo di airwork, che se servono si agganciano come al punto 1.

- **La sostituzione (decisione 1) è una scelta**, e va **ultima**: vince su tutto, compreso l'import
  IVAO che ogni notte riscriverebbe il poligono di sorgente. È il motivo per cui la scelta vive in
  una **tabella sua** e viene **ritimbrata** dopo ogni giro, invece di essere scritta una volta e
  sperare: è lo stesso schema con cui già lavorano il ripiego da GitHub e quello dal sectorfile.

⚠️ **Perché non un «override» letto dai lettori**: `RegionMapPolygon` è citato in **113 punti**.
Infilare un `override ?? sorgente` in ognuno significa che il primo dimenticato mostra il confine
sbagliato in silenzio. Scrivendo invece nella colonna che tutti già leggono, i 113 lettori non
cambiano di una riga.

⚠️ **Il cancello AIRAC non si tocca.** `ShapeAiracGate` differisce **solo** le shape di provenienza
`Sectorfile`, e per una ragione precisa: il sectorfile lo scriviamo **noi in anticipo** sul ciclo.
Il file dell'AIP descrive quel che è **già pubblicato**, quindi `Aip` passa come `Source`: nessun
differimento, zero righe cambiate nel cancello. Il ciclo dichiarato al caricamento serve a **dire da
dove viene un confine**, non a rimandarlo.

## 7. La pagina pubblica (decisione 4)

`/services/airspace` — accanto al convertitore, che è l'altro servizio pubblico.

Si vedono **i settori nostri** (da IVAO, dal sectorfile, sintetici — con scritto quale) **e i volumi
del file**, accendibili per famiglia. Le aree regolamentate, se ci vanno, arrivano **dal DB IVAO**
come già fa la mappa delle aree.

⚠️ **Il peso.** I 146 428 punti dell'intero file al browser non ci vanno. Le sole famiglie
utilizzabili sono **363 volumi, 31 985 punti, ~625 KB** di JSON grezzo — e si mandano **per
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
| **S2** | **Il catalogo e il caricamento.** Le due tabelle, la pagina admin: carica, esito in chiaro («1 536 letti, 363 utilizzabili, 3 doppioni»), elenco filtrabile, anteprima in mappa. Il KMZ si conserva intero. | 1 |
| **S3** | **La sostituzione a mano.** Il legame, il servizio che lo timbra in coda alla catena, il tasto nella struttura settori con la chip «shape dall'AIP, scelta da *chi* il *quando*» e il tasto **«torna a IVAO»**. → **LIBA e LICC chiusi.** | 1 |
| **S4** | **L'ATZ per le TWR.** Passo automatico fra il ripiego del sectorfile e il cerchio; aggancio per ICAO nel nome (42 su 46). | — |
| **S5** | **La pagina pubblica**, per famiglia, con l'attribuzione — e la riga della fonte nelle mappe AoR che mostrano una shape sostituita. | — |
| **S6** | **Il convertitore**: sorgente «dal catalogo spazi aerei» accanto alle tredici che già legge. | — |
| **S7** | **Il rapporto radioassistenze e campi**: che cosa il file dice e l'anagrafica no (e viceversa). **Solo segnalazione, nessuna scrittura**: si corregge nel sectorfile e si reimporta. | — |

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
