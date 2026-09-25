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
| C5 | ~~`update.ini`~~: il manuale IVAO del sector non lo cita (letto il 25 settembre) | — | ❌ tolto dal committente, per ora |
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

## §5 — `DYNAMIC_SEC` (27 `.tfl`, sezione `[FILLCOLOR]` degli `.isc`)

### Cosa c'è (misure del 24 settembre)

Formato (specifica di Aurora): testa `Tipo;Riempimento;Bordo;ColoreBordo;[Opacità];[Filtro]` — Tipo = `Static` o
lista di posizioni IVAO (**il poligono si vede solo se una di quelle è collegata**); colori RGB o nome di
`colors.def`; larghezza del bordo; opacità 0/1; filtro (COAST, RUNWAY, GATES, PIER, TAXIWAY, APRON, BUILDING). Poi i
vertici, in DMS o per nome. **Convenzione italiana: solo il bordo, riempimento vuoto**; Aurora chiude da sola i
poligoni aperti.

- **181 settori**. Nomi: posizioni separate da spazio (18) o da `:` (56). Colori: TWR 69, APP 57, CTR 40, MIL 5,
  LIMMFIC 4, LIMMLIM 3 (da `colors.def`), 3 in `#esadecimale`. `1;1` in 177 teste, `1;0` in 4.
- **Colori di Aurora**: lo schema del committente è `ColorSchemes\LIRR_RDR_V1.0.clr` (156 chiavi: ARTCC,
  AIRWAYLOW, SID, STAR, FIX, VOR, COAST, TAXIWAY…), fuori da `SectorFiles`.
- **`.isc`**: `DYNAMIC_SEC\GCI.tfl` incluso ma inesistente (sta in `OTHER\`); `lirrctr.tfl` due volte in
  `ITALY.isc`; `limmfic.tfl` solo in `LIMM.isc`.
- **12 settori aperti** (Aurora li chiude: nessun avviso). Nomi ripetuti nello stesso file: `LIBB_FSS`, `LIMM_FSS`.
- **Posizioni nei `.frq`**: italiane 161 vere, **4 assenti** — il committente le aggiunge: `LIRE_APP` (forse
  `LIRE_TWR`), `LIBC_TWR` (è `LIBC_I_TWR`), `LIMF_WW0_APP` (da finire), `LIQW_I_TWR` (da vedere). Estere: 20 citate,
  35 no — **per gli esteri la regola non vale** (situazione particolare).
- **Forme ripetute**: dei 178 settori con ≥ 4 vertici, **132 hanno la stessa forma in un altro file** (≥ 90% dei
  vertici): 91 in uno `.str`, 39 in `.hartcc`/`.lartcc`, poi `.mva`, `.geo`, altri `.tfl`. **120 identiche, 12
  divergenti**: `LIMM_WS2/ES2/WS5/ES5/MIL_CTR` vs `limmfic.tfl`/`limm.hartcc` (11-16 vertici), `LIPP_CE1/NE3/MIL_CTR`
  vs `lipp.hartcc` (21-23), `LICC_APP` vs `licc.str` (1), `LIRR_MIL_CTR` vs `lirr_ne_ctr.tfl` (1).
- Vertici condivisi fra settori (copie PARZIALI, confini in comune): 3 653 su 14 778, 535 fra file diversi.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| D1 | **Testa in una scheda**: posizioni come caselle coi suggerimenti dai `.frq`, larghezza, opacità, filtro | Subito | ✅ deciso |
| D2 | **Selettore di colori** vero, più i nomi di `colors.def` | Subito — comune | ✅ deciso |
| D3 | **Mappa coi colori di Aurora** dallo schema scelto (`LIRR_RDR_V1.0.clr`), per tutti gli strati; settori dinamici solo bordo | Subito — comune | ✅ deciso |
| D4 | **Settore italiano legato ai `.frq`**: ogni sua posizione è citata in un `.frq`, come posizione o fra i trasferimenti (anche non primaria). Esteri esclusi | Subito | ✅ deciso — «la cosa più importante» |
| D5 | **Famiglie di forme**: tag `//@forma="NOME"` (riga propria) uguale in ogni copia; la famiglia la trova il Lab. Modifica propagata alle copie uguali, «allinea anche questa» per le già diverse, «copia la forma da…», avviso «copie di forma diverse». Confronto come ANELLO (inizio e verso qualsiasi), formati diversi | Subito — comune | ✅ deciso: **opzione B** (l'opzione A, link verso gli altri file, scartata: N² link, si rompono coi nomi dei file, le copie si perdono di vista) |
| D6 | **Adozione**: il Lab propone le famiglie trovate (120 identiche), il committente conferma, i tag si scrivono in un ramo. Le 12 divergenti si sistemano **quando il sistema è pronto** | F4 | ✅ deciso |
| D7 | Include rotti/doppi negli `.isc`: controllo | Subito — comune (poi correzione in F4) | ✅ deciso |
| D8 | Copie parziali (confini in comune fra settori) | F8 «saldatura bordi» | come da piano |

## §6 — `ENRMVA` (4 `.mva`, uno per ACC; sezione `[MVAENR]`)

### Cosa c'è (misure del 25 settembre)

Formato (specifica di Aurora): `Tipo;Identificativo;Lat;Lon;[Descrizione];[Font]` — `L` etichetta (descrizione = la
quota mostrata, font), `T` traccia. Sono le MVA **di ACC**; quelle di aeroporto stanno nei `.mva` di `Include\IT`.

| File | Righe | Etichette | Tratti | Nomi di zona già scritti |
|---|---|---|---|---|
| `libb.mva` | 214 | 10 | 7 | — |
| `limm.mva` | 1 535 | 51 | 43 | in coda `//2500NE`, `//FL70NE/TRLNE`, `//FL110`…; righe `//mva LIMM a Torino` |
| `lipp.mva` | 2 331 | 23 | 20 | — |
| `lirr.mva` | 526 | 35 | 31 | in coda `//ex brindisi`, `//mista ex brindisi`; righe `//EX ETNA`, `//EX NOMIN` |

- Quota in centinaia di piedi (`25` = 2500 ft), più valori speciali `TRL`, `NO MINIMA`, `*30/40`, `70/TRL`.
- **Il 5° campo delle `T`** (`LIMM`, 4 100 righe) è il **gruppo della finestra *MVA Selection*** di Aurora (una voce
  per ACC, accesa/spenta): serve, provato dal committente. 🔴 Nella finestra compare anche **DUMMY** (le righe
  separatrici `T;DUMMY;…;` senza 5° campo), mentre nella *ACC Selection* no.
- Etichette e poligoni: 20 etichette su 119 fuori da ogni poligono chiuso, 10 poligoni con più etichette, 1 senza →
  **la zona non si ricava con sicurezza dalla geometria**. Le 20 vanno controllate (committente: quando l'app è pronta).
- 355 `//Coast` in coda in `limm.mva`: in realtà punti del confine con la Svizzera (N046 E009).
- 52 righe di dati commentate (zone nascoste, es. `//L;LIRR;…;100;8; //NAPOLI CTA`).
- **Fonte**: oggi carte senza coordinate (es. «livelli minimi di vettoramento entro la TMA di Roma»); il committente
  cerca se esistono le coordinate.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| E1 | **Zona = blocco** `//@zona="Torino"` … `//@END`: etichetta con la quota + i suoi tratti; soprannome facoltativo, **anche ripetuto** (due zone possono chiamarsi uguali). Cosa sta nella zona lo decide l'utente; il Lab propone i tratti intorno all'etichetta | Subito | ✅ deciso |
| E2 | **Scheda della zona**: quota col significato («25 = 2500 ft»), valori speciali da elenco, font | Subito | ✅ deciso |
| E3 | **Gruppo scritto dal Lab**: il 5° campo di ogni `T` nuova è il gruppo del file (ACC), **anche sulle righe separatrici** (`T;DUMMY;…;LIRR;`); avviso se manca o è diverso | Subito | ✅ deciso |
| E4 | Tipo fisso, punti coi suggerimenti, nascondi/mostra, spezza/unisci | Subito — comuni | ✅ deciso |
| E5 | Controlli: poligono senza etichetta, etichetta fuori da ogni zona (le 20 di oggi, da rivedere), valore non valido | Subito | ✅ deciso |
| E6 | **Adozione**: blocchi dall'ordine di oggi + nomi dai commenti che ci sono; via i commenti in coda | F4, in un ramo | ✅ deciso |
| E7 | La voce **DUMMY** nella *MVA Selection* viene dai separatori `T;DUMMY;…;` **senza 5° campo**: col gruppo in coda (`T;DUMMY;…;LIRR;`) Aurora li attribuisce all'ACC e DUMMY sparisce (provato dal committente, 25 settembre). Adozione: il gruppo sui 104 separatori di oggi | F4, in un ramo (i nuovi: E3) | ✅ risolto |
| E8 | Tratti lungo il confine della FIR (le 355 «Coast») = copie parziali del confine | F8 (saldatura bordi) | proposta |
| E9 | Mappa con le zone colorate per quota (buchi e sovrapposizioni a colpo d'occhio) | F8 | proposta |
| E10 | **Ricalco da immagine**: una carta senza coordinate (PDF o immagine) agganciata alla mappa su 3-4 punti noti, in trasparenza, e le zone disegnate sopra | F6 (aggancio) + F9 (disegno) | proposta — per le carte MVA senza coordinate |

## §7 — `ENRVFI` (3 `.vfi`) e le strutture VFR di Aurora

### Le quattro strutture (specifica di Aurora)

| Sezione | File | Riga | Uso |
|---|---|---|---|
| `[VFRFIX]` | `.vfi` | `Nome;Quota;Lat;Lon;[Tipo 0-3]` | punti VFR, accesi per aeroporto |
| `[VFRROUTE]` | `.vrt` | `N° rotta;Lat;Lon;;[Militare]` | rotte VFR di un aeroporto |
| `[VFRENR]` | `.vfi` | `N° rotta;Lat;Lon;[Gruppo];[Militare]` | rotte en-route, filtro Shift+VFR |
| `[VFRRTEENR]` | `.vrt` | `Nome rotta;Gruppo;Lat;Lon` | rotte non legate a un aeroporto |

`ICAO.vfi` e `ICAO.vrt` di un aeroporto dichiarato Aurora li carica da sé: negli `.isc` non ci sono `[VFRFIX]`,
`[VFRROUTE]`, `[VFRRTEENR]` (74 `.vfi`, 16 `.vrt` di scalo).

### Cosa c'è (misure del 25 settembre)

- `ENRVFI\limm.vfi` 21 punti, `lipp.vfi` 8, `lirr.vfi` 4: punti VFR della FIR (messi per ACC: le FIR in Aurora non
  ci sono). Hanno la forma di **`[VFRFIX]`** ma sono inclusi sotto **`[VFRENR]`** (le rotte): «dovrebbero essere
  VFRFIX» (committente).
- **Convenzione italiana**: il 2° campo dei `.vfi` è il **codice** (`MMN1`, `MCW2`), non la quota — voluto: Aurora
  mostra il nome e sotto il codice, più compatto. I codici NON seguono una regola di direzione.
- Coordinate compatte (`N0455440000`), nomi con spazi. Le rotte `.vrt` citano i punti **per nome** (`1;ROMAGNANO;ROMAGNANO;`).
- **Gemello in `NAVAIDS\VFR_NASCOSTI.fix`** (`MMN1;N…;E…;3;`): serve, perché un traffico che mette il punto in
  rotta viene riconosciuto solo se il punto sta in un `.fix`. Misura su TUTTI i `.vfi` (scali compresi), 530 codici:
  **496 gemelli uguali · 9 diversi** (piccoli scarti `BNNW1`, `BNSW1`; punti spostati `BNW1`, `MJNW1`, `PKS1` ~20 NM,
  `RPNE1`; refusi `lipx.vfi` `PXSW1` `E103441000`, `lirn.vfi` `RNNE1` `N40.59.33.000` col punto) · **81 senza
  gemello** (quasi tutti col 2° campo che non è un codice: `2500` in `liba.vfi`, `BV` in `libv.vfi`, `ED` in
  `lied.vfi`; un refuso: `lict.vfi` `MAZARA DEL VALLOCTSE3;;` — manca il `;`) · **7 fix nascosti senza punto**
  (`CTSE3`, `PASE1`, `PASW1`, `PASW2`, `PAW1`, `PHE2`, `PRNW5`) · **2 codici doppi nel `.fix`** (`MJNW1`, `PKS1`).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| F1 | **Scheda per ognuna delle quattro strutture** coi nomi giusti dei campi; nei `.vfi` il 2° campo si chiama «Codice» (convenzione italiana), il tipo 0-3 da elenco (obbligatorio, VFR, eli, area) | Subito | ✅ deciso |
| F2 | **Gemello `.vfi` ↔ `VFR_NASCOSTI.fix`** (chiave = codice): spostare il punto sposta il gemello, **aggiungere un punto crea il gemello**, togliere il punto propone di togliere il gemello. Estende le copie gemelle di F3-bis | Subito — comune | ✅ deciso |
| F3 | Punto nuovo in ordine di codice, **codice proposto** = numero libero successivo con lo stesso prefisso (nessuna regola di direzione) | Subito | ✅ deciso |
| F4 | Controlli: gemello mancante o diverso, fix nascosto senza punto, codice doppio, coordinate scritte male (cifre mancanti, forma diversa dal file), riga con un campo in meno (il `;` mancante di `lict.vfi`) | Subito | ✅ deciso |
| F5 | **Prova in Aurora**: i tre file di `ENRVFI` sotto `[VFRFIX]` invece di `[VFRENR]` — si vedono? come si accendono senza un aeroporto? | F4, in un ramo | 🟡 da provare |
| F6 | Controllo `.isc`: file incluso sotto una sezione che non ha la sua forma | Subito — comune (controllo degli `.isc`) | ✅ deciso |
| F7 | Sistemare i 9 gemelli diversi, il refuso di `lict.vfi`, i 7 orfani, i 2 doppi | quando il sistema è pronto | da fare coi dati |

## §8 — `GEO`: i file globali (`itgeo.geo`, `italy.danger`, `italy.prohibit`, `italy.restrict`)

I `.geo` degli aeroporti hanno un ragionamento a parte (§8-bis, da fare).

### Cosa c'è (misure del 25 settembre)

Formato (specifica di Aurora, sezione `[GEO]`): a **segmenti**, `LatInizio;LonInizio;LatFine;LonFine;Colore;` — ogni
punto scritto due volte (fine di un segmento, inizio del successivo); una riga vuota o commentata apre un tratto
nuovo; un commento per area («raccomandato»). Qui il colore è un NOME (`COAST`, `DANGER`…) e le aree hanno un 6°
campo col nome (`D5A`, `P1`, `R10A`). La specifica avverte: troppo dettaglio nei file globali **pesa molto sulle
prestazioni**.

| File | Segmenti | Gruppi | Segmento mediano | Anomalie |
|---|---|---|---|---|
| `itgeo.geo` (852 KB) | 13 560 | 128 pezzi di costa (PENISOLA, SARDEGNA, LAGUNA VENETA…), 126 chiusi | 0,40 NM | 66 doppi, 3 nulli; coordinate miste (4 652 compatte, 8 908 col punto) |
| `italy.danger` | 664 | 54 aree | 0,52 NM | 51 doppi |
| `italy.prohibit` (760 KB) | 10 143 | 278 aree | **0,086 NM** (2 118 sotto i 90 m) | 6 rotture, **56 righe illeggibili** |
| `italy.restrict` | 2 209 | 185 aree | 1,1 NM | 265 doppi, 9 nulli, 12 rotture, **30 righe illeggibili** |

- 86 righe illeggibili (`italy.prohibit:3132…`, `italy.restrict:1139…`): **uno spazio al posto del `;`** fra
  latitudine e longitudine → Aurora le scarta, quelle aree oggi sono a pezzi.
- **Semplificazione misurata** (Douglas-Peucker, tolleranza = scarto massimo dalla forma): costa 100 m −49%,
  proibite 50 m −68%, restrict 50 m −25%, danger 50 m −25% → **26 576 → 12 290 segmenti (−54%)**. Altre soglie:
  costa 50 m −34% / 200 m −63%; proibite 25 m −54% / 100 m −75%.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| G1 | **Vista a linea**: un gruppo = una linea di punti, non N segmenti; cambiare un vertice riscrive i due segmenti che lo toccano (la catena non si rompe) | Subito — comune | ✅ deciso |
| G2 | **Scheda dell'area**: nome (6° campo), tipo dal file, commento come descrizione, vertici; area nuova con «incolla da testo AIP» (archi, convertitore di F1) | Subito | ✅ deciso |
| G3 | Controlli: riga illeggibile (con la correzione proposta per lo spazio al posto del `;`), segmento doppio/nullo, catena rotta, area non chiusa | Subito | ✅ deciso |
| G4 | **Semplifica con tolleranza** (costa 100 m, aree 50 m di partenza), col conto di quanto toglie e la mappa prima/dopo. 🔴 Sulle aree i vertici dichiarati dall'AIP si tengono sempre: si semplificano solo gli archi (pulito con G5) | F8 | ✅ deciso (tolleranze confermate) |
| G5 | **Quote e forme delle aree dalla fonte primaria**: DB di IVAO o PDF dell'AIP (ENR 5.1), archi rigenerati alla densità scelta; quote come metadato `//@area="P1" da=… a=…`. 🔴 **Mai da vIPI**: niente riferimenti circolari, dati solo dalla fonte primaria | F6 | ✅ deciso |
| G6 | **Adozione in un ramo**: le 86 righe corrette, via doppi e nulli, **coordinate tutte col punto** (`N045.00.00.000`) | F4 | ✅ deciso |

## §8-bis — `GEO`: i `.geo` degli aeroporti

### Cosa c'è (misure del 25 settembre)

- **95 file**, uno per scalo, tutti inclusi una volta in `ITALY.isc`: 78 118 righe, **69 199 segmenti**. Il più grande
  `lirf.geo` (5 296), poi `limc.geo` (3 619); mediana 454; il più piccolo `lilg.geo` (4). Coordinate col punto (4
  compatte), nessuna riga illeggibile, nessun commento in coda.
- **Il tipo in fondo alla riga** dice cosa si disegna: `TAXI_CENTER` 23 771 segmenti (80 file) · `TAXIWAY` 18 268 (82)
  · `BUILDING` 12 467 (93) · `PIER` 6 201 (57) · `APRON` 4 184 (67) · `RUNWAY` 3 612 (95) · `STOPLINE` 566 (70) ·
  `STOPBAR` 120 (10) · **vuoto** 10 (`liap.geo:28…`). `TAXI_CENTER` e `STOPLINE` non sono nello schema colori né in
  `colors.def`, ma in Aurora si vedono e si accendono/spengono (committente).
- **Oggi si disegna in Google Earth**: i commenti più frequenti sono «Percorso senza titolo» (338) e «Poligono senza
  titolo» (226). 🔴 Le immagini satellitari sono **vecchie**: servono le carte dell'AIP. Il confine dello scalo è
  disegnato come `BUILDING` (`//AD_BOUNDARY`).
- **OpenStreetMap, misurato su LIRF**: centro taxiway coincidente (mediana 1,1 m, 94% entro 5 m); piste = centro +
  larghezza; piazzali ed edifici modellati diversamente (mediana 80-145 m). Licenza ODbL (derivato pubblico con la
  stessa licenza). **Scartato dal committente**: le geometrie di terra si continuano a fare a mano.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| H1 | **Strati per tipo** sulla mappa (TAXI_CENTER, TAXIWAY, BUILDING…), ognuno acceso a sé, coi colori di Aurora; vista a linea (G1) | Subito | ✅ deciso |
| H2 | **Tipo fisso** da elenco; avviso se vuoto (i 10 di `liap.geo`) o sconosciuto | Subito — comune | ✅ deciso |
| H3 | **Nome del gruppo** = il commento sopra, cambiato dalla scheda; «Percorso/Poligono senza titolo» segnalati come nome mancante (564) | Subito | ✅ deciso |
| H4 | **Import/export KML/KMZ** (Google Earth): percorsi e poligoni ↔ gruppi `.geo`, tipo dalla cartella o scelto | F6 | ✅ deciso |
| H5 | Famiglie `.geo` ↔ `.pol` (bordo e riempimento della stessa forma) | da misurare con `GND_LAYOUT` | 🟡 |
| H7 | **Carta AIP sopra la mappa**: PDF (AD 2.24) → immagine dentro l'app; **aggancio su 3 punti** noti (soglie e ARP dal testo dell'AIP, AD 2.12/2.2) con lo **scarto misurato** (carta «not to scale» = scarto grande, detto subito); trasparenza regolabile, sotto le linee di oggi. Precisione attesa: carta 1:15 000-20 000, un tratto ≈ 5 m | F6 | ✅ deciso — stesso meccanismo del ricalco MVA (E10) |
| H8 | **Disegnare sulla carta**: linee e poligoni col tipo, il nome, l'aggancio ai vertici vicini | F9 | ✅ deciso |
| H9 | ~~OSM come base~~ | — | ❌ scartato: si continua a mano |

## §9 — `GND_LAYOUT` (93 `.pol`, i riempimenti dei layout di terra)

### Cosa c'è (misure e manuale IVAO, 25 settembre)

- **I `.pol` sono `.tfl` con un'altra estensione**: il manuale ammette in `[FILLCOLOR]` file con estensione qualsiasi
  (`F;SCEZ.myext`). Colori anche `#AARRGGBB` (opacità, da Aurora 1.4.1) — funziona solo con *Smooth Drawing*.
- 93 file, **1 753 poligoni**, 38 763 vertici, tutti `STATIC`, tutti in `ITALY.isc`. Riempimento coi nomi di
  `colors.def`: BUILDING 626, TAXIWAY 437, CONCRETE 221, APRON 190, RUNWAY 114, GRASS 98, **HOLE 67** (buca un'area già
  riempita). Generati da un convertitore (`//***Converted Items***`, `//AD_BOUNDARY_Polygon`). **43 poligoni con meno di
  3 vertici**.
- **Stessa forma del `.geo`**: 1 585 poligoni su 1 707 (93%) hanno ≥ 90% dei vertici nel `.geo` dello stesso scalo.
- **Caricamento**: la riga 6 di `[INFO]` di `ITALY.isc` dice solo `IT` → Aurora carica da sé solo i file di scalo in
  `Include\IT\` (per questo `lirf.sid`, `lirf.vfi`… stanno lì). `.geo`, `.pol` e `RW_MARKINGS` sono elencati a mano:
  **223 righe** di `ITALY.isc`. Nomi misti: `lirf.geo` ma `rf_ad_gnd.pol` e `rf_mark.geo`.
- Manuale, `delete.upd`: la cancellazione avviene **dopo** lo scompattamento → mai elencare un file che
  l'aggiornamento riscrive (controllo per C4).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| I1 | **Scheda del poligono** (come D1): riempimento e bordo da `colors.def` o col selettore, anche `#AARRGGBB` con l'avviso su *Smooth Drawing* | Subito | ✅ deciso |
| I2 | **Una forma, uscite a scelta**: una forma si disegna/modifica una volta; due caselle, **«bordo» (`.geo`, col suo tipo) e «riempimento» (`.pol`, col suo colore)** — tutte e due o una sola (linea di centro solo `.geo`, erba senza bordo solo `.pol`); legate come famiglia (`//@forma=`, D5). Adozione delle 1 585 coppie in un ramo | Subito (famiglie) + F4 (adozione) | ✅ deciso |
| I3 | **Ordine di disegno** (vince l'ultimo): erba → cemento → piazzale → taxiway → pista → edifici → buchi; il Lab lo mostra e mette un poligono nuovo al posto del suo tipo | Subito | ✅ deciso |
| I4 | Controlli: poligono con meno di 3 vertici (43), colore sconosciuto, `.pol` senza `.geo` dello scalo (3) | Subito | ✅ deciso |
| I5 | L'import/export KML (H4) produce tutte e due le uscite | F6 | ✅ deciso |
| I6 | **Organizzazione dei file di scalo**: vedi la proposta sotto | Subito (la vista) + F4 (la prova) | 🟡 proposta |

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
| **Famiglie di forme** (`//@forma=`: la stessa forma in più file, modifica propagata, integrità) — estende le copie gemelle di F3-bis dai record alle forme | DYNAMIC_SEC D5, D6 |
| **Gemelli fra file di tipo diverso** (punto `.vfi` ↔ fix nascosto `.fix`, chiave = codice; il gemello nasce col punto) — estende le copie gemelle di F3-bis | ENRVFI F2 |
| **Colori**: selettore, nomi di `colors.def`, mappa con lo schema di Aurora | DYNAMIC_SEC D2, D3 · COLORS §4 |
| **Controllo degli `.isc`** (file incluso che non c'è, incluso due volte, file non incluso) | DYNAMIC_SEC D7 · AIRWAY (`itawhigh` non incluso) · ACC A9 |
| **Blocchi con nome** (`//@"NOME"` … `//@END`: un'unità del file con soprannome, anche ripetibile) | AIRWAY B1 · ENRMVA E1 · (F3-bis composte) |
| **Campi scritti dal Lab** (il gruppo nel 5° campo MVA, `NOME;NOME;` dei punti per nome) | ENRMVA E3 · ACC A2 |
| **Ricalco da immagine** (carta PDF/immagine agganciata alla mappa su 3 punti, scarto misurato, disegno sopra) | ENRMVA E10 · GEO H7, H8 |
| **Import/export KML** (Google Earth) | GEO H4 |
| **Fonte primaria, mai vIPI** (committente, 25 settembre): i dati entrano dal DB di IVAO o dai PDF dell'AIP, non dal sito — niente riferimenti circolari | GEO G5 · AIRWAY B8 · tutto F6 |
| **Coordinate in una forma sola**: col punto (`N045.00.00.000`), anche dove oggi sono compatte | GEO G6 · (da decidere per `.vfi`/`.tfl`, dove la forma compatta è la regola del file) |
| **Semplifica / densità** (tolleranza in metri, archi a N gradi) | GEO G4 · F1 archi |
| **Ogni modifica lascia una traccia** (riga di changelog proposta, `delete.upd`, `ITALY.isc`) | CHANGELOG C1, C4 · ACC A9 |
| **Niente commenti in coda** (committente, 24 settembre: «dopo una riga letta da Aurora non vanno commenti `//`»). Oggi **713 righe in 70 file** (`limm.mva` 374 `//Coast`, `limc.sid` 34, `itawlow.lairway` 28 `BREAK`, `FRA.artcc` 18, `GCI.tfl` 19…); `SectorError.log` di Aurora vuoto, quindi non le segnala. 🔴 `limc.sid:…;0;OSKOR; //SUPER-HEAVY-A321`: il commento sta nell'8° campo (`RNAV`). → controllo «commento in coda» · gesto «sposta il commento sopra» (riga o file) · garanzia con test che il Lab non ne scrive mai. **Avviso**, non errore: lo sviluppatore di Aurora dice che una riga col `//` in coda si legge in **circa il doppio del tempo** (lentezza, non dato sbagliato). Pulizia di tutto il sector in un colpo: in un ramo (F4) | tutti |
