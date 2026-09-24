# File per file — cosa serve a ogni file del sector, e in che fase (dal 24 settembre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md) (§7 le fasi F4-F9, §8 decisioni).
> Carte dell'app: [F3](2026-09-22-f3-l-app.md), [F3-bis](2026-09-23-f3-bis-copie-e-mappe-composte.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## §0 — Il metodo

Chiuse le prove a mano di F3 e F3-bis (52 su 52, 24 settembre), il committente ha scelto di passare il sector **per
struttura**: una cartella alla volta, nell'ordine di `Include\IT`. Per ogni cartella:

1. **Si misura** cosa c'è (file, righe, tipi di record, come il Lab li legge oggi) sul fork di prova.
2. **Il committente dice tutto** quello che servirebbe su quei file: significato dei campi, come si usano in Italia,
   cosa manca, idee «un giorno».
3. **Si confronta con le misure**: ogni regola detta a parole si controlla sul file vero, e le eccezioni si chiedono.
4. **Ogni esigenza va in una fase**: **Subito** (lotto prima della consegna agli AOD), **F4** git, **F5** Aurora a
   lotti, **F6** PDF ENR, **F7** procedure AD 2, **F8** geometria fine, **F9** trascinare sulla mappa, oppure
   **Da pensare** (serve un ragionamento a parte prima di mettere una fase).
5. Il committente conferma o sposta. Le voci **Subito** diventano un lotto di slice.

Vale per tutte le cartelle (detto dal committente il 24 settembre):

- **Rami di prova, niente file parassiti** (F4, decisione 6 della carta madre): un tentativo si fa in un ramo
  `aod/<VID>/<argomento>`; se va, PR e fusione; se non serve più, il ramo si cancella. I file di prova non entrano
  nel sector.

## §1 — `ACC` (3 file `.artcc`, sezione `[ARTCC]` di `ITALY.isc`)

### Cosa c'è (misure del 24 settembre, fork `c46226f`)

| File | Righe | Contenuto |
|---|---|---|
| `FRA.artcc` | 1 902 | 104 etichette `L;` + 1 750 righe `T;` in 6 gruppi |
| `FRA-gates.artcc` | 3 508 | 90 etichette `L;` (i nomi dei gate: J, X, Y, Z) + 3 291 righe `T;COPs;` = 85 cerchi |
| `test.artcc` | 0 | vuoto, ma incluso da `ITALY.isc` |

Formato di una riga (specifica di Aurora): `Tipo;Identificativo;Latitudine;Longitudine;[Font]` — Tipo `L` etichetta o
`T` traccia; latitudine e longitudine in DMS **o** nome di un fix/navaid; font facoltativo (oggi 8 ovunque).
`T;DUMMY` (anche minuscolo) separa i tratti. In Aurora, tasto ACC della barra = mostra tutto; Shift+clic = finestra
*ACC Selection* con una voce per gruppo.

I gruppi e il loro uso in Italia (committente, verificato sulle misure):

- **FRA BDRY** (31 tratti): tutti i confini fra ACC, nazionali e con l'estero. 64 punti per nome (`LUSIL;LUSIL;`).
- **LIMITROFI** (13 tratti): il confine fra due ACC **esteri**, dal nostro confine verso fuori per 15-44 NM —
  «quanto basta», nessuna regola di lunghezza.
- **NPZ** (2 poligoni chiusi, VEKEN e Firenze): No Planning Zone, dove non si vettora.
- **AOCC MM/PP/RR** (5 T): Area of Common Coordination, dove per agire si sente l'ACC accanto. **Gambo** dal
  confine verso dentro (misurati 8,8-15,3 NM), **stanghetta** dritta di **15 NM** in fondo al gambo, circa parallela
  al confine (confermato dal committente: la stanghetta NON sta sul confine).
- **COPs** (`FRA-gates.artcc`): i gate interni fra ACC. Cerchi di **0,5 NM** (37-38 punti) col centro sul confine
  (scarto ≤ 0,26 NM); **ogni cerchio è il confine fra due gate vicini** (85 cerchi, 90 gate); etichetta col nome al
  centro. Di solito 10 NM, **non sempre**: la serie X (2,9-17 NM) e le etichette di Y13-Y48 (1-2 NM fuori dal
  confine) sono giuste così, vengono dai documenti.
- **Etichette dei fix di confine** (`L;` in `FRA.artcc`): 104 su 104 hanno un fix omonimo nella STESSA posizione.

Come le legge il Lab oggi: un record = le righe consecutive col nome uguale → FRA BDRY è **un** record di 31 tratti,
COPs **uno** di 85; ogni `L;` un record. I commenti (`//FRA IT - Zona A`, `//X01-X02`) non contano. Trovati nelle
misure: il cerchio `//X07-X08` è **aperto**; `test.artcc` è un file parassita (il motore lo salta già per nome).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| A1 | **Tipo fisso**: «+ Nuovo record» chiede Etichetta (L) o Traccia (T), col significato; nient'altro si scrive | Subito | ✅ deciso |
| A2 | **Punto = coordinate o fix**: latitudine e longitudine sono un campo «Punto»; scrivendo si propongono fix, VOR, NDB del sector, filtrati man mano; scelto un nome si scrive `NOME;NOME;`. Font: campo numerico facoltativo | Subito | ✅ deciso |
| A3 | **Vista come Aurora**: sotto il file i gruppi della *ACC Selection*, ognuno acceso/spento sulla mappa; dentro il gruppo le parti col nome dal commento sopra («Zona A», «LSAG-LFMM», «X01-X02») | Subito | ✅ deciso |
| A4 | **Avvisi del validatore**: cerchio non chiuso · centro del cerchio fuori dal confine · stanghetta AOCC ≠ 15 NM · etichetta `L;` in posizione diversa dal fix omonimo. 🔴 NON «gate ≠ 10 NM» né «etichetta fuori centro»: le eccezioni sono vere | Subito | ✅ deciso (senza le due regole tolte) |
| A5 | **Generatore di gate**: solo su un confine; si sceglie la **lunghezza** (10 NM proposta, non obbligata); i cerchi da 0,5 NM; si riusa il cerchio del gate vicino; etichetta al centro; i pallini si **spostano a mano** lungo il confine, e spostarli cambia la lunghezza | F8 (lo spostamento col mouse: F9) | ✅ deciso |
| A6 | **Generatore di AOCC**: punto sul confine, lato, lunghezza del gambo → gambo + stanghetta di 15 NM in fondo | F8 | ✅ deciso |
| A7 | Gate e AOCC che **si rigenerano** quando il confine si sposta (tag `//@` come le mappe composte) | Da pensare | 🟡 «cosa molto delicata, va pensata bene» |
| A8 | **Etichette per riferimento**: `L;ABDAB;ABDAB;ABDAB;8;` invece delle coordinate copiate, così spostare il fix sposta l'etichetta (e l'ultimo avviso di A4 non serve più). Prima una prova in Aurora, in un ramo | F4 (la prova) → poi adozione | ✅ deciso |
| A9 | Togliere `test.artcc` (file e riga di `ITALY.isc`), in un ramo | F4 | ✅ deciso |
| A10 | Chiudere il cerchio `//X07-X08` | Subito (dato, non codice: lo segnala A4) | da segnalare agli AOD |

## §2 — `AIRWAY` (`itawlow.lairway`, `itawhigh.hairway`)

### Cosa c'è (misure del 24 settembre, fork `c46226f`)

Formato (specifica di Aurora): `Tipo;Aerovia;Latitudine;Longitudine;` — `L` etichetta, `T` traccia; lat/lon in DMS
**o** nome di un fix/navaid.

- **`itawhigh.hairway`** (128 righe): tutte commentate, solo etichette di vecchie aerovie «U». **Non incluso** in
  `ITALY.isc` (`[HIGH AIRWAY]` vuota): in Italia oggi le aerovie di alta non esistono.
- **`itawlow.lairway`** (3 016 righe), le aerovie di bassa, in quattro parti:
  - righe 1-18: **KY139** a sé, «inserita manualmente. non presente su IAB. NON CANCVELLARE»;
  - `//Airway tracks` (20-1422): **247 aerovie**, 1 413 punti, **tutti per nome** e tutti trovati nei NAVAIDS;
  - `//Airway labels` (1424-2317): **896 etichette, tutte per coordinate**: 821 su 862 a metà di un segmento. Un
    segmento condiviso ha l'etichetta coi nomi uniti (31, es. `L613-L615`; 19 portano ancora nomi «U» che non
    esistono più, es. `UL81-L81`). **22 aerovie senza etichetta** (A145, A725, N1, Q482, Y99…);
  - `//Waypoint references` (2319-3016): 692 righe commentate `//NOME;lat;lon;`, **vecchia**: 73 punti usati non ci
    sono, 3 hanno coordinate diverse dai NAVAIDS (GOVGO, ABNAT, SARKI), 1 non esiste.
- **Interruzioni**: `T;BREAK;RIVAM;RIVAM; //discontinuity (creates a break)` — 28 righe, un nome finto che spezza
  l'aerovia (L81 in tre pezzi). Negli `.artcc` la stessa cosa la fa `T;DUMMY`.
- Aerovie nascoste in `itawlow`: nessuna (le righe commentate sono la lista dei punti).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| B1 | **Un blocco per aerovia**: `//@"L81"` … `//@END "L81"`, in cima le sue etichette, poi il tracciato. Nel Lab la vista per aerovia (tracciato + etichette + tratti) c'è anche prima di riorganizzare il file | Subito (la vista) | ✅ deciso |
| B2 | **Livelli e verso PER TRATTO**: ogni tratto ha il suo verso, la sua quota minima e massima (così nel PDF), scritti **come nel PDF, in piedi**. Tag `//@` sul tratto; Aurora li legge come commenti. Scheda coi tratti uno per uno | Subito (tag e scheda) | ✅ deciso |
| B3 | **Nascondi / mostra**: commenta con `//` ogni riga del blocco, e al contrario; nascosta resta nell'elenco, grigia | Subito — **comune** (§C) | ✅ deciso |
| B4 | **Etichette calcolate dai tracciati**: una a metà di ogni segmento, coi nomi delle aerovie che lo condividono; la condivisa nel blocco della prima in ordine alfabetico. Sistema le 22 senza etichetta e i nomi «U». Serve già a B14 | Subito (anticipata da F8 per B14) | ✅ deciso |
| B5 | **Aerovie manuali**: `//@"KY139" manuale` → l'import dai PDF non la tocca né la toglie. **Ma l'utente può sempre cancellarla** dall'app se lo sceglie | Subito (il tag) · F6 (il rispetto nell'import) | ✅ deciso |
| B6 | **Interruzioni**: gesto «spezza qui / unisci» su un punto (qui scrive `BREAK`, negli `.artcc` `DUMMY`) | Subito — **comune** (§C) | ✅ deciso |
| B7 | **Punti**: sequenza dei fix con suggerimenti mentre si scrive, «inserisci un punto qui», «inverti» | Subito — **comune** (§C, con A2) | ✅ deciso |
| B8 | **Import da PDF** ENR 3.1 (rotte ATS di bassa): punti, per tratto verso e quote minima/massima → i tag di B2 | F6 | ✅ deciso |
| B9 | **Prova in Aurora dell'ordine a blocchi** (etichette `L` fra i tracciati `T` di ogni aerovia): la si fa **col committente**, in un ramo, prima di riscrivere il file | F4 (prova) → riorganizzazione | ✅ deciso |
| B10 | Via la lista `//Waypoint references` in fondo | F4 (in un ramo) | ✅ deciso |
| B11 | `itawhigh.hairway`: **si archivia così com'è**, per ora non si tocca | — | ✅ deciso |
| B12 | Controlli: aerovia senza etichetta, etichetta orfana o con nomi che non esistono, punto che non si trova | Subito | ✅ deciso |
| B13 | Sulla mappa: frecce del verso, colore per quota | F8 | proposta |
| B14 | **Aggiungere un'aerovia a mano**: nome + sequenza dei punti (B7); il Lab scrive da sé il blocco, il segno `manuale`, un tag per tratto e le etichette (B4, comprese le condivise). Quote e verso NON si inventano: si scrivono nella scheda per tratto; verso di base «entrambi», quote vuote con avviso «tratto senza quote» | Subito | ✅ deciso |
| B15 | **Togliere un'aerovia**: via il blocco intero (tracciato, etichette, tag); le etichette condivise perdono solo il suo nome | Subito | ✅ deciso |

## §3 — `CHANGELOG` (e `changelog.md`, `delete.upd`, `update.ini`)

### Cosa c'è (misure del 24 settembre)

- **`IT\changelog.md`**: il changelog del ciclo in corso («**AIRAC A2610 IN VIGORE DAL 01/10/2026»), **Aurora lo
  mostra agli utenti**. Schema fisso dal 2026: *Generale*, poi LIBB, LIMM, LIPP, LIRR; righe `*ICAO: cosa`, `*NIL` se
  vuota.
- **`IT\CHANGELOG\AAMM.txt`**: 22 cicli archiviati. 2023 in forma libera («+++AIRAC 2311 (AIP A10/23)+++»), 2024-2025
  solo `2401`, dal 2026 lo schema di `changelog.md`. Si scrive, in teoria, **alla fine** del ciclo: oggi `2610.txt` è
  già indietro rispetto a `changelog.md` (mancano LIPI, LIRL, LIRM).
- **`SectorFiles\delete.upd`** (95 righe): i file da cancellare nei client all'aggiornamento. Aggiornato a mano ogni
  tanto (repo originale: 2 giugno 2026 ENR/TERM/FRA.fix, 3 dicembre 2025 MVA/VFI).
- **`SectorFiles\update.ini`**: i file aggiornati. Fino a febbraio 2021 una riga `IT;1831;`, **vuoto da allora**:
  «ad oggi non li aggiorna nessuno».
- Il `changelog.md` nella cartella di Aurora è quello del programma, non del sector: non si tocca.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| C1 | **Righe proposte dal Lab**: da ogni modifica, FIR dal file/scalo (`lirl.sid` → LIRL → LIRR) e testo dal tipo di record («LIRL: aggiunte le SID MASE5L, 8J»). L'AOD **corregge, toglie, aggiunge** righe a mano: il testo lo leggono gli utenti in Aurora | F4 | ✅ deciso |
| C2 | Ogni PR porta le sue righe nel `changelog.md` del ramo `airac/AAMM`; la prima riga di una sezione toglie il `*NIL`, l'ultima tolta lo rimette | F4 | ✅ deciso |
| C3 | **Fine ciclo**: `changelog.md` archiviato in `CHANGELOG\AAMM.txt`, poi lo schema vuoto con l'intestazione del ciclo nuovo (ciclo e data dal calendario AIRAC che vIPI calcola già) | F4 | ✅ deciso |
| C4 | **`delete.upd` scritto dal Lab**: togliere un file aggiunge la sua riga qui e toglie quella di `ITALY.isc` (es. A9 `test.artcc`) | F4 | ✅ deciso |
| C5 | **`update.ini`**: i file aggiornati del ciclo li sa git (diff col ciclo prima); prima di generarlo va capito cosa legge Aurora (formato `IT;1831;` del 2021) | Da pensare | 🟡 formato da capire |
| C6 | Controlli: intestazione diversa dal ciclo del ramo, sezione mancante, riga accanto a `*NIL` | F4 | ✅ deciso |
| C7 | Archivio vecchio (2023 libero, 2024-2025 mancanti) lasciato com'è | — | ✅ deciso |

## §4 — `COLORS` — misurata, **per ora non serve** (committente, 24 settembre)

- `PAR2090.clr` (31 righe `CHIAVE=colore`, colori Delphi `$00BBGGRR` o nomi `clYellow`): i colori del PAR, sezione
  `[COLORSCHEME]` degli `.isc` (`F;COLORS\PAR2090.clr`).
- `colors.def` (16 righe `NOME;#RRGGBB;`): i colori dei layout di terra (GRASS, TAXIWAY, RUNWAY, STOPBAR…) e delle
  aree (TWR, APP, CTR, MIL, GCI, LIMMFIC, LIMMLIM), incluso come `F;IT\colors\colors.def`.
- Da ricordare se un giorno servirà: i due riferimenti negli `.isc` hanno basi diverse (`COLORS\…` come gli altri
  file, `IT\colors\…` con `IT` davanti); le combinazioni di colori di Aurora (`ColorSchemes\*.clr`) stanno fuori dal
  sector. Il Lab potrebbe usare `colors.def` per colorare la mappa come Aurora.

## §C — Meccanismi comuni (raccolti cartella per cartella)

Si costruiscono **una volta** per tutti i file che li chiedono. Il lotto «Subito» parte quando tutte le cartelle sono
passate (committente, 24 settembre): così le parti comuni si accorpano e non si fa lavoro doppio.

| Meccanismo | Chiesto da |
|---|---|
| **Tipo fisso** della riga (scelta fra i valori ammessi, col significato) | ACC A1 |
| **Punto = coordinate o nome**, con suggerimenti filtrati dai NAVAIDS; sequenze di punti con inserisci / inverti | ACC A2 · AIRWAY B7 |
| **Gruppi e parti** con nome (dal nome del record o dal commento sopra), accesi/spenti sulla mappa | ACC A3 · AIRWAY B1 |
| **Nascondi / mostra** (commentare e scommentare un blocco) | AIRWAY B3 — «può servire un po' ovunque» |
| **Interruzioni** spezza / unisci (`DUMMY`, `BREAK`) | AIRWAY B6 · ACC (tratti) |
| **Etichette calcolate** dalla geometria | AIRWAY B4 · ACC A5 (etichetta al centro del gate) |
| **Tag `//@`** di dati che Aurora non legge (livelli, verso, manuale) | AIRWAY B2, B5 · (F3-bis: composte) |
| **Rami di prova** per tentativi e prove in Aurora | tutti (§0) · ACC A8, A9 · AIRWAY B9, B10 |
| **Ogni modifica lascia una traccia** (riga di changelog proposta, `delete.upd`, `ITALY.isc`) | CHANGELOG C1, C4 · ACC A9 |
