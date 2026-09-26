# File per file — cosa serve a ogni file del sector, e in che fase (dal 24 settembre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md) (§7 le fasi F4-F9, §8 decisioni).
> Carte dell'app: [F3](2026-09-22-f3-l-app.md), [F3-bis](2026-09-23-f3-bis-copie-e-mappe-composte.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Stato — 26 settembre 2026

**Giro finito**: §1-§22 coprono tutte le cartelle di `Include\IT` e tutti i tipi di file della radice, ognuno con
formato (manuale IVAO), misure sul fork `c46226f`, decisioni del committente e fase. §C raccoglie i meccanismi comuni.

- **Prossimo**: il **lotto «Subito»** — le voci «Subito» raggruppate per meccanismo comune (§C), in slice e in un
  ordine, in una carta a parte da far leggere al committente prima di cominciare.
- **Prove in Aurora da fare presto** (committente, in rami di prova): T3 ordine dei simboli · R4 startup col tasto HOLD ·
  B9 etichette `L` fra i `T` delle aerovie · A8 etichette ACC per riferimento · F5 `ENRVFI` sotto `[VFRFIX]` · I7/K2
  organizzazioni dei file di scalo A-E.
- **Pulizie decise** (F4, in un ramo): `limw.pol`, `test.artcc`, `limc_star`/`lirf_star`, SID ripetute e campi spostati,
  ICAO sbagliati, `LIMM_WN4/EN4_CTR` nei trasferimenti; `.fix` vuoti più avanti.
- **Da chiedere**: `LL` di `lied.str`; configurazioni di Milano 2.1/2.2/3.
- **Regole trasversali**: niente commenti in coda · fonte primaria, mai vIPI · `PREFS` solo lo stretto necessario ·
  rami di prova, niente parassiti · coordinate col punto.

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
| I6 | **Vista per scalo nel Lab**: scelto LIRF, tutto quello che lo riguarda da qualunque file (layout di terra, procedure, VFR, txi, gts, MVA, righe di `.ap`/`.rw`/`.frq`); il Lab scrive nei file giusti. Vale con qualunque organizzazione dei file | Subito | ✅ deciso («molto d'accordo») |
| I7 | **Organizzazione dei file di scalo**: più proposte (sotto), **provate in Aurora dal committente**, che poi dice quale funziona | F4, in rami di prova | 🟡 da provare |

### I7 — Proposte di organizzazione dei file di scalo (da provare)

Oggi: 95 `GEO\lirf.geo` + 93 `GND_LAYOUT\rf_ad_gnd.pol` + 35 `RW_MARKINGS\rf_mark.geo` = **223 righe a mano** in
`ITALY.isc`; in `Include\IT\` ci sono già ~480 file di scalo (`.sid`, `.str`, `.vfi`, `.txi`, `.gts`…). Il manuale
IVAO: Aurora carica da sé i file col nome dello scalo nelle cartelle della riga 6 di `[INFO]` (oggi solo `IT`), e per
la terra prevede un file per tipo (`ICAO.rwy` piste, `.txo` bordi taxiway, `.txc` centro taxiway, `.stp` stop, `.apr`
piazzali, `.pier`, `.bld` edifici) e `ICAO.tfl` per i riempimenti.

| | Proposta | Come | Pro | Contro |
|---|---|---|---|---|
| **A** | Oggi, coi nomi uniformi | stesse cartelle, nomi tutti ICAO (`lirf.geo`, `lirf.pol`, `lirf_mark.geo`), righe negli `.isc` scritte dal Lab | nessun rischio in Aurora | restano 223 righe, un file con tutti i tipi |
| **B** | Caricamento automatico in una **cartella per tipo** | `IT\GND\LIRF.rwy`, `LIRF.txo`, `LIRF.txc`, `LIRF.stp`, `LIRF.apr`, `LIRF.pier`, `LIRF.bld`, `LIRF.tfl`; riga 6 di `[INFO]` = `IT;IT\GND` | via le 223 righe; la radice non cresce; un file per tipo | ~95 × 8 file in `GND` (fino a ~760, molti piccoli) |
| **C** | Caricamento automatico in una **cartella per scalo** | `IT\AD\LIRF\` con dentro tutto lo scalo (terra, `.sid`, `.str`, `.vfi`, `.txi`, `.gts`…); riga 6 = `IT;IT\AD\LIAA;IT\AD\LIAP;…` | lo scalo sta tutto in un posto, come lo pensa l'AOD | riga 6 con ~100 cartelle: va provato che Aurora la regga |
| **D** | Caricamento automatico **nella radice** | i file per tipo direttamente in `Include\IT\` | nessuna modifica alla riga 6 | ❌ centinaia di file in più nella radice (sconsigliata dal committente) |

**Cosa si verifica in Aurora per ogni proposta** (un solo scalo, LIRF, in un ramo `aod/<VID>/prova-organizzazione-X`):
1. il layout di terra appare **identico** (stessi colori, stesso ordine di disegno, HOLE sopra GRASS);
2. si accende e spegne per tipo come oggi (TAXI_CENTER, STOPLINE…);
3. `SectorError.log` vuoto;
4. tempo di caricamento del sector e fluidità della mappa, prima e dopo;
5. se i file per tipo si caricano **solo per lo scalo scelto** («toggled per airport» nel manuale).

Il Lab prepara ogni ramo di prova con uno strumento (divide/rinomina/sposta i file dello scalo e aggiorna gli `.isc`);
la proposta vincente si applica poi a tutti gli scali in un colpo, sempre in un ramo.

## §10 — `HI_AIRSPACE` (4 `.hartcc`, sezione `[ARTCC HIGH]`)

### Cosa c'è (manuale IVAO + misure, 25 settembre)

Formato: `T/L;Identificativo;Lat;Lon;[Font]` (T traccia, L etichetta); punti per coordinate o per nome (consigliato).
Il manuale chiede nomi tutti diversi. Nell'esempio di `.isc` del manuale: **i file `icao.geo` non si dichiarano,
si caricano da soli se lo scalo è in `[AIRPORTS]`** → da verificare nelle prove di I7.

- **Cosa ci va** (committente): i **settori di aerovia** (ACC, `…_CTR`); quelli di avvicinamento (`…_APP`) vanno in
  `LOW_AIRSPACE`. Es. `LIRR_NE_CTR` qui, `LIRN_US0_APP` là.
- `libb` 2 voci (`BB`, `BB FIC`) · `limm` 3 (`MM WS`, `MM WS-ES`, `MM FIC`) · `lipp` 7 (`PP CE`, `PP CESW`, `PP FIC`,
  `DEL PP-MM`, `COOR EDMMUU`, `COOR EDUU`, `COOR LSAZ`) · `lirr` 13 (`RR CONF1`, `CONF1M`, `CONF2`, `EW`, `NC`, `NE`, `NW`,
  `US`, `TS`, `OV`, `SU`, `ES`, `FIC`). Il nome = la voce della finestra di selezione di Aurora; le parti hanno il
  nome nel commento sopra (`//MILANO-ROMA`, `//ROMA G1-G2`, `//ARLBERG LINE`, `//brindisi/tirana`).
- Nessuna etichetta `L`. 5 nomi in più blocchi (`BB`, `MM WS`, `PP CE`, `DEL PP-MM`, `RR CONF2`): **voluto**, più pezzi
  dello stesso settore. 4 voci aperte (linee: `MM WS-ES`, `COOR …`). 15 commenti in coda. 492 punti per nome, tutti
  trovati. Qui stanno le copie che divergono dai settori dinamici (`limm.hartcc`, `lipp.hartcc`, §5).
- **Configurazioni di Roma** (committente): `CONF1` = NE; `CONF2` = NE + TS; `CONF2B` = SU + ES; le altre sono
  divisioni di NE e TS; EW non si divide mai; SU solo in SU + ES.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| J1 | **Vista come la selezione di Aurora**: voci per nome (anche in più pezzi), parti col nome dal commento, accese/spente sulla mappa (come A3) | Subito — comune | ✅ deciso |
| J2 | Tipo fisso T/L, punti coi suggerimenti, etichetta col font facoltativo | Subito — comune | ✅ deciso |
| J3 | **Famiglie con `DYNAMIC_SEC`** (D5): settore colorato e confine restano uguali | Subito + F4 (adozione) | ✅ deciso |
| J4 | Controlli: commento in coda (avviso). Nome in più blocchi **non** è un errore (pezzi dello stesso settore) | Subito | ✅ deciso |
| J5 | **Il file giusto per un settore nuovo**: `…_CTR` → `HI_AIRSPACE`, `…_APP` → `LOW_AIRSPACE`; avviso se un settore sta nell'altro | Subito | ✅ deciso |
| J6 | **Configurazioni composte**: `//@"RR CONF2" composta=RR NE,RR TS` — la forma della configurazione si calcola dall'unione dei settori (come le mappe composte di F3-bis); le regole di Roma (EW mai diviso, SU solo con ES) diventano controlli. 🔴 L'unione ha bisogno di confini che coincidono (saldatura bordi) | F8 | ✅ deciso (dipende dalla saldatura) |

## §11 — `LOW_AIRSPACE` (15 `.lartcc`, sezione `[ARTCC LOW]`)

### Cosa c'è (misure del 25 settembre)

Stesso formato di §10. Qui i **settori di avvicinamento** (`…_APP`).

- **11 file con un settore APP ciascuno** (17-77 righe: `libb_cs0_app` → `LIBB CS0`, `limm_es0/wn0/ws0/ww0_app`,
  `lipp_ce0/se0_app`, `lirr_es0/ew0/nn0/us0_app`) — in HI invece un file per ACC.
- **2 file di configurazioni del TMA**: `limm_tma.lartcc` (`MM CONF 1`, `2.1`, `2.2`, `3`; **276 righe commentate**
  = configurazioni nascoste, da tenere così per ora) e `lirr_tma.lartcc` (`RR CNF1`, `CNF2.1`, `CNF2.2`, `CNF3`).
  Milano (committente): **CONF 1 = WS2, CONF 2 = WS2 + ES2** (2.1, 2.2, 3 da precisare).
- **2 file di STAR** (`limc_star.lartcc` `LIMC 35`, `lirf_star.lartcc` `LIRF 16`): **da eliminare a breve**, deciso in AOD.
- Nomi in più blocchi (pezzi delle configurazioni), nessuna etichetta, 6 commenti in coda, 133 punti per nome tutti
  trovati. Nomi non uniformi: `RR CONF1` (HI) / `RR CNF1` (LOW) / `MM CONF 2.1`. Le forme APP sono copie di
  `DYNAMIC_SEC` (es. `LIMF_WN0_APP` = `limm_wn0_app.lartcc` al 100%).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| — | J1-J6 di §10 valgono anche qui (vista, campi, famiglie con `DYNAMIC_SEC`, controlli, file giusto per `_APP`, configurazioni composte) | come in §10 | ✅ deciso |
| K1 | **Nomi coerenti delle configurazioni** (`CONF`/`CNF`, spazi): il Lab segnala le differenze e propone una forma | Subito (controllo) + F4 (rinomina) | ✅ deciso |
| K2 | **Un file per ACC anche per gli APP** (`limm.lartcc` coi settori di Milano) invece di un file per settore: **proposta E di organizzazione**, da provare insieme ad A-D (I7) | F4, da provare | 🟡 da provare |
| K3 | Togliere `limc_star.lartcc` e `lirf_star.lartcc` (file, righe degli `.isc`, `delete.upd` — C4) | F4 (o a mano, quando l'AOD lo fa) | ✅ deciso in AOD |
| K4 | Configurazioni nascoste di `limm_tma` (276 righe): restano commentate; nel Lab visibili come «nascoste» (B3) | — | ✅ per ora così |

## §12 — `NAVAIDS` (fix, NDB, VOR; sezioni `[FIXES]`, `[NDB]`, `[VOR]`)

### Formati (manuale IVAO)

- Fix: `Nome (max 5);Lat;Lon;Tipo;Confine;[Attesa]` — tipo 0 ENR, 1 TERM, 2 entrambi, 3 nascosto; confine 0/1; il nome
  dell'attesa sta nel **6° campo** (il testo del manuale dice «5°», il suo esempio e il sector il 6°).
- NDB: `Nome;kHz;Lat;Lon;[Visibilità 0/1]`, attesa all'8° campo. VOR: `Nome (max 3);MHz;Lat;Lon;[Visibilità];[Tipo 0
  VOR, 1 VORDME, 2 VORTAC, 3 TACAN, 4 DME];[Canale TACAN];[Attesa]`.
- Attese (`[HOLDENR]`, `HOLDENR.hold`): `NOME;Lat;Lon;[Info]`, il nome uguale a quello scritto nel fix/navaid.

### Cosa c'è (misure del 25 settembre)

| File | Dati | Cosa sono |
|---|---|---|
| `itfix.fix` | 1 256 | i fix: 444 ENR, 496 TERM, 316 entrambi; 658 di confine; 54 con l'attesa |
| `APT.fix` | 781 | fix di scalo nascosti, a sezioni `//LIBC` |
| `ESTERNI.fix` | 641 | fix esteri, nascosti |
| `secsi.fix` | 527 | **i fix del FRA internazionale**, per poter dare i diretti (committente) |
| `VFR_NASCOSTI.fix` | 512 | gemelli dei punti VFR (§7) |
| `MIL.fix` | 186 | militari; 89 nomi oltre i 5 caratteri (`BV-ARGIR`) — **Aurora li accetta** |
| `itvor.vor` / `itndb.ndb` | 122 / 27 | 14 VOR con l'attesa |
| `ENR.fix`, `FRA.fix`, `TERM.fix` | vuoti | resti del «file unico dei fix» (giugno 2026), in `delete.upd` |

- **Attese**: 68 citate, 68 definite; **una sbagliata**: il fix `EKLAP` cita `HLD-ELKAP`, la definizione è `HLD-EKLAP`
  → oggi non si vede. L'info ha una forma fissa: `ABBOZ/225R-9000` (fix/rotta, virata, quota).
- 5 righe illeggibili: `APT.fix` `MG763` (`E008-11.31.443`), `MIL.fix` `BV-VICTOR` (cifra in meno) e `TAC-06R`
  (decimale), `VFR_NASCOSTI.fix` `RFS3` (cifra in più), `itvor.vor` `VBA` (`n` minuscola). Refusi: `3:` in `APT.fix`,
  9 righe di `VFR_NASCOSTI.fix` senza tipo.
- **269 nomi ripetuti**: 245 nella stessa posizione (doppioni), 24 in posizioni diverse (`SARKI` ×2 in `ESTERNI.fix`
  a 568 NM, `BV-BRAVO` ×2 in `MIL.fix` a 34 NM, `PKS1`, `MJNW1`…). Doppioni soprattutto `ESTERNI` ↔ `secsi` (121) e
  `ESTERNI` ↔ `itfix` (72).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| L1 | **Schede tipizzate** (tipo fix, confine, visibilità, tipo VOR, canale TACAN) con l'**attesa collegata** alla sua definizione e l'info `ABBOZ/225R-9000` mostrata a campi | Subito | ✅ deciso |
| L2 | **«Chi lo usa»**: dove è citato un fix/navaid (aerovie, SID/STAR, settori, ACC, MVA, `.vfi`, attese); **rinominare aggiorna tutti i riferimenti**, spostare avvisa, togliere è impedito se è usato | Subito — comune | ✅ deciso |
| L3 | **Nome unico**: avviso per i nomi in posizioni diverse (24); i doppioni nella stessa posizione (245, per lo più `ESTERNI` che ripete `secsi`/`itfix`) segnalati, da pulire | Subito (controllo) + F4 (pulizia) | ✅ deciso |
| L4 | Controlli: coordinate illeggibili (con correzione proposta), attesa citata e non definita o viceversa, campi mancanti. **Nome oltre i 5 caratteri NON è un errore** (Aurora lo accetta) | Subito | ✅ deciso |
| L5 | Import di fix (ENR 4.4) e navaid (ENR 4.1) dall'AIP, col confronto | F6 | ✅ deciso |
| L6 | Via `ENR.fix`, `FRA.fix`, `TERM.fix` vuoti | **più avanti**, non ora (committente) | 🕓 rimandato |

## §13 — `OTHER` (`.frq`, `.ap`, `.rw`, CPDLC, `GCI.tfl`)

Fonti: manuale del sector file ([ATC], [AIRPORT], [RUNWAY], [FILLCOLOR]) e la pagina **«Aurora CPDLC Sectorfile»**
della wiki IVAO (`/en/home/devops/manuals/CPDLC-Sectorfile`) per `.cpdlc`/`.cpdlcnames`. `ITALY.isc` carica i file
nazionali (`itfreq.frq`, `itap.ap`, `itrw.rw`), ogni `.isc` di FIR i suoi (`libb.frq`…): da qui le copie gemelle di
F3-bis. Il CPDLC è comune.

### Cosa c'è (misure del 25 settembre)

- **`.frq`** (`Posizione;Frequenza;Trasferimenti;Profilo;ATIS;Blocco CPDLC;LOA;D-ATIS`): `itfreq` 201, `libb` 22, `limm`
  55, `lipp` 44, `lirr` 101. Frequenze tutte `nnn.nnn`. Manuale: nei trasferimenti **prima gli include, poi gli
  esclusi** (`-POS`), un include dopo un escluso NON funziona → **51 in `itfreq.frq`** (es. `LIML_TWR`: `LIRO` dopo
  un escluso) più le copie. File citati inesistenti: `PREFS\LIPC.cpr`, `\liml.atis`. Posizioni italiane citate ma non
  definite: `LIMM_WN4_CTR`, `LIMM_EN4_CTR` (12 volte ciascuna: **da togliere dai trasferimenti**), `LIMJ_APP`,
  `LIBB_APP`. CPDLC bloccato su 155 posizioni.
- **`.ap`** (`ICAO;Elev;TA;Lat;Lon;Nome≤50;[Nascosto];[Tipo 0-4]`): 130 scali in `itap.ap`; «nascosto» e «tipo»
  (militare, eliporto, privato, non controllato) **mai usati**; TA 0 in 66. `//LIRR;…;Roma Area` commentato.
- **`.rw`** (`ICAO;Prim 01-18;Opp 19-36;Elev;Elev;Rotta;Rotta;soglie`): 231 righe in `itrw.rw`, di cui **96 piste
  finte `MAPS`** = il **menu delle cose «generali»** a cui si agganciano le mappe degli `.str` (committente), più voci
  di settore (`LIRR;NE`, `LIMM;WS2`). Soglie invertite: `LIDW 15` (rotta 149°, coordinate verso 11°), `LIKL 36`
  (360° contro 176°); `LIDB 05` 049° contro 43° dalle soglie. **Rotte con decimali** (`109.5`, `345.9`, `065.49`,
  `283.8/104`): 44 in `itrw.rw` + copie — Aurora le accetta ma **rallenta**. **Primaria oltre il 18** in 13 piste
  (LIMC 35R/17L, LIML 35/17, LIRN 24/06, LIMF 36/18…), contro il manuale.
- **CPDLC** (`Comando≤128;Risposta WU/AN/R/NE;Gruppo 0-18, 20=DCL;…`): 155 messaggi nei gruppi 0-10 e 20; 3 senza
  risposta; `ita.cpdlcnames` rinomina solo il gruppo 15 («TWR»), **vuoto**.
- **`GCI.tfl`** (9 837 righe, 53 settori dinamici `LIZZ_AEW_CTR:LIRO_CRC_CTR:LIVK_CRC_CTR;GCI;…`): gli `.isc` lo
  cercano in `DYNAMIC_SEC\` e sta in `OTHER\`, ma **in Aurora si colora lo stesso** (committente) → Aurora trova il
  file per nome: un percorso sbagliato è un avviso, non un guasto.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| M1 | **Scheda della posizione**: frequenza, **trasferimenti in due elenchi (includi / escludi)** → l'ordine è sempre giusto, profilo/ATIS/D-ATIS scelti fra i file che esistono, blocco CPDLC, LOA | Subito | ✅ deciso |
| M2 | Controlli `.frq`: include dopo escluso (i 51), file citato inesistente, posizione italiana citata e non definita | Subito | ✅ deciso |
| M3 | **Scheda dello scalo**: nascosto e tipo (militare, eliporto…) proposti | Subito | ✅ deciso |
| M4 | **Scheda della pista**: la rotta **calcolata dalle soglie** accanto a quella scritta (soglie invertite subito visibili); `MAPS` e le voci di settore mostrate come **voci di menu**, non piste. Avvisi: rotta con decimali (**rallenta Aurora**; gesto «arrotonda al grado», su riga o file, a scelta dell'AOD perché il dato dell'AIP ha il decimale), primaria oltre il 18 | Subito | ✅ deciso |
| M5 | Scheda dei messaggi CPDLC e dei nomi dei gruppi; avviso: gruppo con nome ma senza messaggi, messaggio senza risposta | Subito | ✅ deciso |
| M6 | Percorso di include inesistente (`DYNAMIC_SEC\GCI.tfl`): **avviso**, correzione non urgente | Subito (controllo) + F4 | ✅ deciso |
| M7 | Togliere `LIMM_WN4_CTR` e `LIMM_EN4_CTR` dai trasferimenti | F4, in un ramo | ✅ deciso |
| M8 | Import di scali, piste (AD 2.2, AD 2.12) e posizioni dall'AIP, col confronto | F6 | ✅ deciso |

## §14 — `PREFS` (15 `.cpr`, i profili delle posizioni ATC)

### Cosa sono (manuale IVAO + committente)

Un `.cpr` ha **lo stesso formato dei profili di Aurora** (`Aurora\Profiles`): può contenere tutte quelle impostazioni
(un profilo completo: ~40 sezioni, **1 447 chiavi** — `[Screens]`, `[LABELS]`, `[Sounds]`, `[Connection]`,
`[INSET1-8]`…). Ma 🔴 **ogni impostazione di un `.cpr` in `PREFS` sovrascrive quella dell'utente a ogni
connessione** → si mettono **solo le cose strettamente necessarie**. Il profilo lo sceglie la posizione nel `.frq`
(4° campo).

### Cosa c'è (misure del 26 settembre)

- **Generici** `TWR` (220 posizioni), `CTR` (54), `APP` (44), `TMA` (40): 2-3 chiavi (`AircraftHorizontal`…) ✅.
  `WW0` (non usato: **serve a un settore da venire**, `LIMF_WW0_APP`). `LIPC.cpr` citato da un `.frq` ma **non esiste**.
- **10 profili PAR** (`LIBN`, `LIBV`, `LIPA`, `LIPH`, `LIPI`, `LIPL`, `LIPS`, `LIPX`, `LIRM`, `LIRS`): ogni `[INSETn]` è
  una finestra PAR (didascalia, pendenza 2,5-3,3°, radiale, elevazione, DA, soglia, distanza 20 NM, **centro del PAR**
  `Par_Lat`/`Par_Long`). Radiale = rotta del `.rw` (±0,2°), elevazione = soglia del `.rw` (±1 ft). PAR di un altro
  scalo (`LIPA_APP` → Rivolto, `LIPX_ES0_APP` → Ghedi, Treviso → Istrana): **voluto**, quegli APP servono quei campi.
  `PAR_VERTICAL_SCAN`/`PAR_HORIZONTAL_SCAN` prima di ogni `[sezione]`: **funzionano**. Didascalia «LIPI 06» (nel `.rw`
  06L/06R); `LIBN.cpr` INSET3 senza didascalia.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| N1 | **Scheda del profilo**: sezioni e valori leggibili; **ogni impostazione dice che sovrascrive quella dell'utente a ogni connessione**; per aggiungerne una si sceglie dall'elenco delle chiavi di un profilo completo di Aurora (preso dalla cartella `Profiles`), con la sezione giusta | Subito | ✅ deciso |
| N2 | **PAR**: una finestra per pista (pista dal `.rw`, pendenza, DA, distanza, centro); radiale ed elevazione ricavate dal `.rw` e riallineate se la pista cambia | Subito (coerenza) + F8 (generazione) | ✅ deciso |
| N3 | Controlli: profilo citato che non esiste (`LIPC`), didascalia con una pista che non esiste o vuota, radiale/elevazione diverse dal `.rw`, **profilo con molte impostazioni** (avviso: sovrascrivono l'utente). NON sono avvisi: profilo non usato (può servire a un settore da venire), PAR di un altro scalo, righe prima delle sezioni | Subito | ✅ deciso |
| N4 | Il profilo scelto da un elenco nella scheda della posizione (M1), e da lì aperto | Subito | ✅ deciso |

## §15 — `RW_MARKINGS` (le marcature delle piste)

### Cosa c'è (misure del 26 settembre)

Normali `.geo` a segmenti (sezione `[GEO]`), tutti di tipo `RUNWAY`: numero, pettine, linea di centro, segni di
mira, frecce della soglia spostata, croci.

- **35 file, 8 149 segmenti**, uno per scalo (LIMC due: `mc35L`, `mc35R`); commenti per parte abbastanza regolari
  (`//DESIGNATOR RW 35L`, `//THRESHOLD MARKS RW 06`, `//AIMING POINT MARKS`, `//DISPLACED THRESHOLD ARROW`,
  `//RUNWAY STRIPES`).
- **Le marcature stanno in posti diversi**: 34 scali in `RW_MARKINGS`; **13 nel `.geo` dello scalo** (LIRF, LIRN,
  LIPZ, LIPE, LIPX, LIAA, LIRA…); **56 con solo il contorno o niente** (LICA, LIEA, LIPH, LIPS…).
- **LIRN** (`GEO\lirn.geo` 426-641): barra di soglia, pettine di 12 strisce (giusto per 45 m), numero («24» col 4
  disegnato a metà), frecce prima della soglia, linea di centro, segni di mira, croci alle estremità.
- **Le croci** (committente): aree della pista **inutilizzabili per le normali operazioni** o, a volte, **piste chiuse**.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| O1 | **Vista per pista**: marcature raggruppate per pista e per parte (dai commenti), dentro la vista per scalo (I6) | Subito | ✅ deciso |
| O2 | **Generatore di marcature**: dal `.rw` (numero, soglie, direzione) + larghezza, soglia spostata, precisione, aree inutilizzabili/pista chiusa (croci); regole ICAO dell'Annesso 14 **in una tabella modificabile** (strisce per larghezza 18→4, 23→6, 30→8, 45→12, 60→16; numero; linea di centro; segni di mira e zona di contatto per lunghezza; frecce). Scritto in un blocco coi parametri (`//@marcature="LIRN 06" larghezza=45 spostata=0 precisione=si`) che **si ridisegna se la pista cambia** (dipende solo da pista e parametri). **Un pezzo alla volta**: prima barra, pettine, numero, linea di centro; si guarda come va; poi segni di mira, zona di contatto, frecce, croci | F8, a passi | ✅ deciso |
| O3 | Controlli: strisce diverse da quelle previste per la larghezza, marcature lontane dalle soglie del `.rw`, pista senza marcature (solo informativo) | Subito | ✅ deciso |
| O4 | **Dove stanno le marcature** (`RW_MARKINGS` o `.geo` dello scalo): lo decidono le prove di organizzazione (I7) | F4, da provare | 🟡 con I7 |
| O5 | Larghezza, soglia spostata, precisione importate dall'AIP (AD 2.12/2.13/2.14) | F6 | ✅ deciso |

## §16 — `.sid` (59 file nella radice `IT`, sezione `[SID]`, caricati da sé per nome di scalo)

### Formato (manuale IVAO)

Etichetta `ICAO;Piste (più con :);Nome;Lat;Lon;[Tipo 0 SID/1 transizione];[Navaid della transizione, spazi];[RNAV 1]`,
poi i punti `Lat;Lon;[Info]` (vincoli, `:` va a capo); `<br>` in coda al primo punto apre un tratto nuovo.

### Cosa c'è (misure del 26 settembre)

- **1 305 procedure, quasi senza tracciato**: righe di sola etichetta (`LIRF;25;EKLO8R;;;;;1;`), lat/lon vuote; solo 85
  punti in tutto (militari `QUIRRA DEP34`, `IP FRASCA`…). L'elenco serve al **menu SID di Aurora, filtrato per pista
  attiva** (committente). Raggruppate per pista con una riga vuota; 192 per più piste (`14L:14R`); RNAV su 614.
- **Convenzione italiana** (committente): SID + transizione in un **nome composto con tipo 0** e il navaid nel 7° campo
  (`SOS5A-ESI8H; ; ;0;ESINO;1;`) — separate, Aurora non le propone unite nel menu. 373 così.
- 28 righe col 6° campo spostato (`PEMAR`, `VICTOR`, `VEGIM R50 LIR50`…) → **errori**. 11 procedure ripetute (stesso
  scalo, pista, nome; 5 di LIMC 35R) → **da togliere**. 51 nomi con spazi (militari). Piste tutte nel `.rw`.
  10 commentate, 34 commenti in coda, `limf.sid:28` senza `;`.
- **Fix della SID dal nome**: 982/1 305 automatici (694 un solo candidato, 288 il nome è già il navaid), 277 con più
  candidati (`EKTO6M` → `EKTOL`/`EKTOR`), 48 senza (luoghi e militari). Il navaid della transizione c'è in tutte.
- **PDF dell'AIP (LIRF, parte 6)**: tabelle SID RNAV **in testo** — path terminator (CA/CF/DF/TF), waypoint, rotta,
  distanza, vincoli di quota e velocità, RNAV1 — più la **WAYPOINT LIST** con le coordinate e le sezioni «INITIAL
  CLIMB PROCEDURE RWY …». Il nome intero del fix c'è («EKLOS 8R»). WTC e categorie NON sono in quelle tabelle (note
  delle carte, o a mano). L'estrazione a volte sposta un valore sulla riga sotto → lettura tollerante + revisione.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| P1 | **Scheda della SID**: scalo, piste (più, fra quelle del `.rw`), nome, tipo, navaid della transizione coi suggerimenti, RNAV, tracciato | Subito | ✅ deciso |
| P2 | SID nuova nel gruppo della sua pista | Subito | ✅ deciso |
| P3 | Controlli: procedura ripetuta, 6° campo che non è 0/1 (salvo la convenzione del tipo 0 con transizione), `;` mancante | Subito | ✅ deciso |
| P3b | Via le 11 ripetute, correzione dei 28 campi spostati | F4, in un ramo | ✅ deciso |
| P6 | **Metadati** su una riga propria sopra la SID, con **valori di gruppo per pista** (`//@gruppo pista=25 wtc=LMHS cat=ABCD`, poi `//@sid fix=EKLOS trans=ESINO salita=6000ft\|COO APP [wtc=…] [cat=…]`): nome intero del fix e della transizione, salita iniziale (ft o `COO APP`), WTC (L M H S), categoria Vref (A-E) | Subito | ✅ deciso |
| P7 | Fix proposto dal nome della SID (982 automatici, 277 da scegliere) | Subito | ✅ deciso |
| P8 | **Lettura delle tabelle SID dai PDF** (punti, coordinate, vincoli, nome intero, salita iniziale), con revisione prima di scrivere | F7 | ✅ deciso |
| P9 | **Disegno automatico della SID intera** nei suoi punti **dentro il `.sid`** (prima le RNAV) | F7 | ✅ deciso |
| P10 | **Mappa di gruppo in testa al `.sid`** (voce `MAPS`) che aggrega più SID, **come per le STAR**: stesso meccanismo delle mappe composte di F3-bis (`composta=…`) | F7 (col disegno) | ✅ deciso |

## §17 — `.str` (90 file nella radice `IT`, sezione `[STAR]`, caricati da sé per nome di scalo)

### Formato (manuale IVAO)

Come la SID: etichetta `ICAO;Piste;Nome;Lat;Lon;[Tipo];[Navaid transizione];[RNAV 1]` e punti `Lat;Lon;[Info]`,
`<br>` apre un tratto. Il tipo: **0 STAR, 1 transizione, 2 attesa, 3 IAP, 4 FAP, 5 mancato avvicinamento** = i tasti
STAR / TRANS / HOLD / IAP / FAP / GA della finestra delle procedure di Aurora.

### Cosa c'è (misure del 26 settembre)

- **1 504 voci, 35 797 punti** (29 117 per coordinate, 6 680 per nome), 1 971 `<br>`, 1 188 punti con un'etichetta
  (`1B 6000`, `C1`, `THR26`…), RNAV su 520, 7° campo (navaid della transizione) **mai usato**.
- **Procedure su piste vere** (1 169): STAR ~390, transizioni 58, **attese di scalo** 225 (`HLD-ELVAD`), IAP 322, FAP 91,
  GA 86.
- **Menu `MAPS`** (335): **ATZ** e **CTR** (disegni rapidi: altrimenti andrebbero fatti da `LOW_AIRSPACE`, più lungo e
  separato), **prolungamenti `RWYxx`** (91), **STAR (ALL)** aggregate (69), `FIX MILITARI`, `WORK AREAS`, `IAFS`, `RNP`,
  `LL NW/NE/S1/S2` (`lied.str`: probabilmente rotte militari a bassa quota — **da chiedere a chi segue LIED**),
  `LIPB VFR`. 🔴 **Nel `MAPS` il tipo sceglie il TASTO che accende la mappa**: CTR, WORK AREAS, LL, LIPB VFR → TRANS;
  ATZ → GA; RWYxx → FAP; FIX MILITARI, IAFS, RNP → IAP; STAR (ALL) → STAR.
- **Prolungamenti d'asse** (committente: prolungamenti customizzati, tacca corta ogni NM, lunga ogni 5 NM): linea di
  14 o 29 NM, un punto ogni NM; tacche di lunghezza diversa per scalo (LIBA 1 852/4 630 m, LIBC 463/926 m).
- Anomalie: **`liba.str` con coordinate in gradi decimali** (`41.00850773;16.07432896;1B 6000;`, 27 punti, fuori
  dal manuale: probabilmente saltati da Aurora); **`licz.str` con una voce di LICC** (`LICC;10L:10R;LIBR1V`); piste
  strane `05:12`, `06:24`; **`lied.str` `ALPHA SOUTH;ALPHA SUOTH;`** (nome diverso nei due campi); 119 commenti in coda.
- `HOLDENR.hold` contiene le **attese in rotta** dall'AIP: **diverse** da quelle di scalo degli `.str` (nessun legame).

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| Q1 | **Scheda della voce**: tipo da elenco sulle piste vere (STAR/TRANS/HOLD/IAP/FAP/GA); nel `MAPS` lo stesso campo mostrato come **«si accende col tasto …»** (mai un errore); piste dal `.rw`, RNAV, punti con etichetta. Vista per scalo **per pista e tipo** come la finestra delle procedure di Aurora; `MAPS` a parte | Subito | ✅ deciso |
| Q2 | **Metadati per punto** su una riga propria sopra il punto: **vincoli di quota e velocità** e **ruolo** (IAF, IF, FAF, MAPt) — `//@punto ruolo=IAF quota=+FL80 vel=-210`. **Mai a schermo in Aurora** (inquinamento visivo): nel Lab al passaggio del mouse e nella scheda | Subito | ✅ deciso |
| Q2b | Per ogni STAR, come le SID: fix intero, WTC, categoria; **specifica di navigazione** (RNAV1, RNP1, RNP APCH) oltre al flag | Subito | ✅ deciso |
| Q2c | **Legami fra procedure della stessa pista** (STAR → attesa di scalo → IAP → GA) e controllo «STAR che finisce dove non parte nessuna IAP». ❌ NIENTE legame con `HOLDENR.hold` (attese in rotta, altra cosa) | Subito (legami) | ✅ deciso |
| Q3 | **Lettura dai PDF** (tabelle «STAR RNAV1 … DESCRIPTION TABLES», come per le SID) e disegno automatico; IAP/FAP/GA dalle carte di avvicinamento (immagini) più tardi | F7 | ✅ deciso |
| Q4 | **Generatore dei prolungamenti d'asse** dal `.rw`: lunghezza, punto ogni NM, tacca corta ogni NM e lunga ogni 5 NM, lunghezza di ciascuna tacca (parametri per scalo) | F8 | ✅ deciso |
| Q5 | ATZ/CTR del `MAPS` legati ai settori dinamici (famiglie di forme, D5) | Subito | ✅ deciso (D5) |
| Q6 | Controlli: coordinate non DMS (decimali), voce di un altro scalo, piste inesistenti o combinate male, nome diverso nei due campi di un punto | Subito | ✅ deciso |
| Q7 | Altre mappe `MAPS` generate (cerchi di distanza, sottovento, aree P/R/D, TMA, circuiti VFR) | — | ❌ non servono (committente). SID (ALL) sta nel `.sid` (P10) |

## §18 — `.txi` e `.gts` (etichette delle taxiway, stand)

### Formato (manuale IVAO)

- `[TAXIWAY]` (`ICAO.txi`, caricato da sé): `Nome;ICAO;Lat;Lon;` — solo l'etichetta.
- `[GATES]` (`ICAO.gts`): `Nome≤20;ICAO;Lat;Lon;[Tipo L/M/H/S/G];[Slot]` — lo stop point dello stand. **Slot** (6°
  campo, novità): `t_A320` tipo di aereo, `c_OAL` prefisso del nominativo, `d_LIRF` scalo di partenza, `w_` cargo
  ammessi (volo cargo = `CARGO` nelle RMK); **E** fra tipi diversi, **O** dentro lo stesso tipo.

### Cosa c'è (misure del 26 settembre)

- **`.txi`**: 65 file, 1 075 etichette, **sulla linea di centro** (mediana 1 m dalla `TAXI_CENTER`, 90% entro 37 m; 27
  oltre 100 m). Nomi ripetuti (95: una taxiway lunga ha più etichette — normale). Errori: 3 di `lipz.txi` in gradi
  decimali; `libn.txi` con `LINB`.
- **`.gts`**: 52 file, 1 672 stand, sullo stand (mediana 1 m, 90% entro 11 m). **Tipo quasi mai** (48 `M`), **slot
  mai**. Errori: in `libg.gts` uno stand di **LIBP** (a 185 NM: **da togliere**, si rivede in revisione); in
  `limc.gts` `L3MC`, `L4MC`; 2 nomi ripetuti.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| R1 | Etichette taxiway sulla mappa; controllo «etichetta lontana dalla sua taxiway»; trascinarle lungo la linea di centro | Subito (controllo) + F9 | ✅ deciso |
| R2 | **Scheda dello stand**: tipo (L/M/H/S/G) da elenco, **slot** coi suggerimenti spiegati (`t_` `c_` `d_` `w_`) — «iniziare a implementarlo» | Subito | ✅ deciso |
| R2b | **Metadati dello stand** (tutti, committente): **codice ICAO** dello stand (dimensione massima, A-F), **tipo** (a contatto / remoto), **uso** (Schengen, extra-Schengen, cargo, aviazione generale, militare, elicotteri), **compagnie abituali**, **pushback** (obbligatorio sì/no, verso), **piazzale**, **note**. Da codice e uso il Lab **propone tipo L/M/H/S/G e slot** per Aurora (es. codice C + cargo → `M;w_ t_A320 t_B738…`, compagnie → `c_`) | Subito | ✅ deciso |
| R3 | Controlli: ICAO diverso dal file, coordinate non DMS, nomi ripetuti, stand lontano dallo scalo | Subito | ✅ deciso |
| R3b | Via lo stand di LIBP da `libg.gts`; `L3MC`/`L4MC` → `LIMC`; `LINB` → `LIBN` | F4 | ✅ deciso |
| R4 | **Punti di startup** (committente: non stanno nelle carte, si prendono dalle foto satellitari o se si trovano le posizioni): un **trattino perpendicolare alla taxiway principale** agganciato alla linea di centro, lunghezza fissa modificabile, col nome. **Dove**: proposta del Lab = una mappa `STARTUP` nel `MAPS` dello `.str` dello scalo col **tipo 2 (tasto HOLD)**, l'unico tasto che il `MAPS` non usa oggi (misurato: le 225 voci di tipo 2 stanno TUTTE su piste vere — le attese di scalo come `LIRN;24;HLD-BENTO; ; ;2;` + il circuito punto per punto) → ogni punto un tratto (`<br>`), il nome nell'etichetta del punto. 🔴 Col tasto HOLD la mappa `STARTUP` starebbe nello stesso elenco delle attese della pista (col suo occhio): **prova in Aurora in un ramo** prima di adottarla. Ripiego: `TAXI_CENTER` nel `.geo` | Subito (a mano, coordinate) + F9 (clic sulla mappa) | 🟡 da provare in Aurora |
| R5 | Stand e taxiway dall'AIP (AD 2.8), col confronto | F6 | ✅ deciso |

## §19 — `.mva` di scalo (24) e `.vrt` (16)

Manuale: `ICAO.mva` (formato `[MVA]`, come §6) e `ICAO.vrt` (`[VFRROUTE]`, `N° rotta;Lat;Lon;;[Militare]`) si caricano
da sé. Regola degli `.isc` letta qui: **dopo il nome del file niente altri caratteri né `;`** (controllato: tutti a
posto). Nell'elenco dei file di scalo caricati da sé (gts, txi, sid, str, vfi, vrt, mva, tfl) il `.geo` **non c'è** →
nelle prove I7.

### Cosa c'è (misure del 26 settembre)

- **`.mva` di scalo, due stili**: 8 file «come le ACC» (`libd`, `lica`, `lieo`, `limf`, `lipq`, `lipx`, `lipz`, `lirn`:
  un nome per file, `RR`/`MM`/`PP`/`BB`, zone separate da `T;DUMMY`); 16 con **un nome per zona** (`3500`, `ZONA13`,
  `CERCHIO-BA`, `6000E`, `85TPS`, separate da righe vuote) → ogni zona una voce nella *MVA Selection*, nomi uguali in
  più scali. Quote in centinaia quasi ovunque, **piene** in `libn`, `libv`, `lict` (`1500`, `2500`, `FL85`). 5° campo
  sempre vuoto. 7 file con commenti in coda (soprannomi `//Area 6000ft EST`, `//Zona B`, note `//Discrepanza tra i dati
  e la carta`, `//Arco senso orario 35NM`). Cerchi disegnati a mano (`CERCHIO-EE`, 74 punti).
- **`.vrt`**: 52 rotte, 123 punti, **puliti** — 83 dal `.vfi` dello scalo, 19 fix/navaid, 8 coordinate, **13 dal `.vfi`
  di uno scalo vicino** (`licc.vrt` → `licz.vfi`, `lirn.vrt` → `lirm.vfi`), nessuno che manca; 16 punti militari.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| S1 | MVA di scalo con le regole delle ACC (§6): zona = blocco `//@zona="…"` col soprannome, scheda con la quota e il suo significato | Subito | ✅ deciso |
| S2 | **Tutto nello stile ACC**: un nome per file = **l'ICAO dello scalo** (`LIRN`), zone separate da `T;DUMMY` col gruppo (E3), soprannomi nei tag; **quote tutte in centinaia** (`25` = 2500 ft). Adozione in un ramo: nomi di zona e commenti in coda → soprannomi e note nei tag | Subito (regole) + F4 (adozione) | ✅ deciso |
| S3 | Controlli: quota in un'unità diversa dal resto, nome del gruppo diverso dall'ICAO, commento in coda | Subito | ✅ deciso |
| S4 | **Scheda della rotta VFR**: numero, punti in ordine scelti dal `.vfi` dello scalo e **di quelli vicini**, militare; «Chi lo usa» (L2) attraversa gli scali | Subito | ✅ deciso |
| S5 | Cerchi e archi delle MVA dal convertitore degli archi (F1) invece che a mano | F8 | ✅ deciso |

## §20 — `symbols.sym` e `HOLDENR.hold`

### `symbols.sym` (sezione `[SYMBOLS]`)

- Manuale: solo «i simboli si definiscono con questo strumento» — link inesistente, niente altro nella wiki.
- **Formato** (ricavato): un simbolo per riga, **13 gruppi di 13 cifre separati da `;` = 13 COLONNE da sinistra a
  destra, ogni cifra un pixel dall'alto in basso** (`1` acceso). Letti come righe vengono ruotati (il FIX punta a
  sinistra). Commento col nome sopra.
- **23 simboli**: APT, FIX vuoto/pieno grande/piccolo, TERM, AC_COMB, AC_comb SEL, AC_DUPE, AC_COMB_MODE_S, acft coast,
  TAC2, VOR/VOR2/VOR3, NDB, VFR, croce X, (senza nome), croce +, croce nel rombo, PAR 2090, PAR2080. Anomalie:
  **`AC_comb SEL` e `AC_DUPE` senza `//`** (righe di testo lette come dati), il 18° senza nome.
- **L'ordine conta** (committente), ma il manuale non dice come. Trovato nei profili completi di Aurora
  (`Profiles\*.cpr`): i simboli **con nome** nello stesso formato — `SYMBOLS_AIRPORT`, `SYMBOLS_HELIPORT`,
  `SYMBOLS_FIX_ENR/TERM/ENR_TERM/BOUND`, `SYMBOLS_FIX_VOR/VORDME/VORTAC/TACAN/DME/NDB`, `SYMBOLS_FIX_VFR/VFRA/VFRH/VFRN`,
  `SYMBOLS_HOLD/IAP/FAP/GA`, `SYMBOLS_AC_1…10` — e impostazioni che scelgono un simbolo **per numero**
  (`AircraftSSRSymbol=3`, `AircraftPSRSymbol=0`, `AircraftCPDLCSymbol=7`, `AircraftExtraPolitedSymbol=8`). Ipotesi: il
  `.sym` del sector aggiunge simboli scelti per numero = posizione nel file (e le 2 righe senza `//` potrebbero
  spostare la numerazione). **Da provare in Aurora**.

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| T1 | **Editor a pixel 13×13**: griglia cliccabile, anteprima a grandezza vera e ingrandita su sfondo radar, scrive le colonne nell'ordine giusto, nome sempre nel commento, ordine visibile e spostabile solo di proposito (col suo numero). Lo stesso editor per i simboli con nome dei profili (`SYMBOLS_*` in un `.cpr`, con l'avviso di §14) | Subito | ✅ deciso |
| T2 | Controlli: simbolo che non è 13×13, nome senza `//`, simbolo senza nome | Subito | ✅ deciso |
| T3 | **Prova in Aurora dell'ordine**: nella scelta del simbolo dell'aereo, quali simboli compaiono, in che ordine, da che numero partono quelli del sector; se conta la posizione, correggere le 2 righe senza `//` | F4, in un ramo — presto | 🟡 da provare |

### `HOLDENR.hold` (sezione `[HOLDENR]`, le attese in rotta)

- Manuale: `NOME;Lat;Lon;[Info]`, un'attesa = una **sequenza di punti** (l'ovale); il nome uguale a quello scritto nel
  6° campo del fix (8° di VOR/NDB); tasto HOLD.
- **68 attese, una riga ciascuna** = solo il punto + l'info in forma fissa `FIX/rotta+virata-quota`
  (`ABBOZ/225R-9000`, `…L-FL100`): **nessun ovale disegnato**. Diverse dalle attese di scalo degli `.str` (§17).

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| U1 | **Scheda dell'attesa**: fix, rotta di avvicinamento, virata L/R, quota minima (ft o FL) come campi separati dall'info; legata al fix che la cita (controllo nei due versi, es. `EKLAP` → `HLD-ELKAP`) | Subito | ✅ deciso |
| U2 | **Per ora solo la scritta** (come oggi). Il disegno dell'ovale (fix, rotta, virata, tratto in minuti o NM) **si sceglie attesa per attesa**: all'import dall'AIP o quando se ne scrive/modifica una | F6 (import) + F8 (ovale) | ✅ deciso |
| U3 | Import dall'AIP ENR 3.6 (attese in rotta), col confronto | F6 | ✅ deciso |

## §21 — `limw.pol` (nella radice `IT`)

- Due riempimenti di **LIMW** (Aosta): confine (`STATIC;COAST`) e piazzale. **Nessun `.isc` lo carica** e Aurora non
  lo carica per nome (`.pol` non è fra le estensioni di scalo). Ultima modifica 16 maggio 2020 («RISCRITTURA COMPLETA
  SECTORFILE DIVISIONE IN SOTTO CARTELLE»).
- **Residuo**: i suoi 52 vertici stanno tutti in `GND_LAYOUT\mw_ad_gnd.pol` (incluso, 254 righe: confine come GRASS,
  piazzale, 2 edifici, taxiway, 5 poligoni di pista).

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| V1 | **Togliere `limw.pol`** (committente: va tolto; non serve `delete.upd`: non è caricato), insieme agli altri parassiti (`test.artcc`, `limc_star`/`lirf_star`, i `.fix` vuoti quando si decide) | F4, in un ramo | ✅ deciso |
| V2 | **Controllo «file orfano»**: file del sector che nessun `.isc` carica e che Aurora non carica per nome di scalo (lo avrebbe trovato da solo); con «è una copia di…» se le sue forme stanno in un altro file | Subito — comune (controllo degli `.isc`) | ✅ proposto |

## §22 — `.atis`, `.datis`, `atisextra.fds`

Fonti: il manuale del sector per `[ATIS]` rimanda a «ATC Operations Staff: Tools For The Job»
(`/en/home/atcoperations/tools`), che offre **ATIS Creator** (Google Drive, 2022) e **SYMBOL Creator** (`matrix.zip`),
più «Quality Standards and tips» e il flusso ufficiale Google Earth → KML → IAB. Scaricati (col permesso del
committente) nello scratchpad **senza eseguirli**: dentro solo gli eseguibili; dai loro testi:
- ATIS Creator conosce `STATION_NAME`, `ATIS_LETTER`, `ATIS_TIME`, `ARR`, `DEP`, `DEP_FREQ`, `TA`, `TL`, `METAR`,
  `REMARK`, nome di file `icao.atis`. Da noi anche `ARR_TYPE` (da `atisextra.fds`), `QFE`, `CPDLC` (più recenti);
  `DEP_FREQ` e `TA` mai usati.
- SYMBOL Creator = «MATRIX for FIX SYMBOLS»: dell'ordine dei simboli non dice niente → T3 resta da provare.

### Cosa c'è (misure del 26 settembre)

- **`.atis`** (7): modelli del testo con segnaposto e **parti facoltative fra parentesi annidate** (`[Arrival runway
  [ARR]]` solo se `ARR` c'è); scritti **per la voce** («aitis», «Q F Echo», «mlpainsa», «lee,NAH,teh») — **voluto**, si
  correggono a mano se serve. `default`, `arrdep` generici; `lica`, `lied`, `limc`, `liml`, `lipz` col nome dello scalo.
- **`.datis`** (4): D-ATIS, stesso modello senza pronunce, `[CPDLC]` negli arrivi/partenze; `datis-acc` = solo
  `CPDLC ID [CPDLC]`; **`datis.datis` vuoto, usato da 90 posizioni: voluto** — solo LIRF e LIMC hanno il D-ATIS.
- **`atisextra.fds`**: `Type of Approach;[ARR_TYPE];` = un campo in più nella finestra ATIS.
- Dai `.frq` (§13): `\liml.atis` citato con una barra di troppo.

### Cosa serve, per fase

| # | Esigenza | Fase | Stato |
|---|---|---|---|
| W1 | **Editor dei modelli**: segnaposto da un elenco (quelli di ATIS Creator + quelli dei `.fds` + `QFE`, `CPDLC`), parti facoltative evidenziate, **anteprima riempita** con valori di esempio (un METAR vero, pista, lettera) | Subito | ✅ deciso |
| W2 | **«Ascolta»**: l'anteprima letta con la voce di Windows (quella di Aurora) per sentire le pronunce | Subito | ✅ deciso |
| W3 | ATIS ↔ D-ATIS affiancati; avviso se la struttura diverge. Le pronunce NON si correggono da sole | Subito | ✅ deciso |
| W4 | Controlli: segnaposto sconosciuto, parentesi non bilanciate, file citato inesistente (`\liml.atis`), campo `.fds` che nessun modello usa. `datis.datis` vuoto NON è un avviso | Subito | ✅ deciso |

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
| **Controllo degli `.isc`** (file incluso che non c'è — AVVISO: Aurora trova il file per nome, vedi GCI —, incluso due volte, file non incluso) | DYNAMIC_SEC D7 · AIRWAY (`itawhigh` non incluso) · ACC A9 |
| **Blocchi con nome** (`//@"NOME"` … `//@END`: un'unità del file con soprannome, anche ripetibile) | AIRWAY B1 · ENRMVA E1 · (F3-bis composte) |
| **Metadati con valori di gruppo** (un tag di gruppo vale per le righe sotto finché non si ripete il campo) | SID P6 |
| **Mappe composte** (una mappa che aggrega più procedure o settori) | F3-bis (STAR) · SID P10 · HI J6 (configurazioni) |
| **Campi scritti dal Lab** (il gruppo nel 5° campo MVA, `NOME;NOME;` dei punti per nome) | ENRMVA E3 · ACC A2 |
| **Ricalco da immagine** (carta PDF/immagine agganciata alla mappa su 3 punti, scarto misurato, disegno sopra) | ENRMVA E10 · GEO H7, H8 |
| **Import/export KML** (Google Earth) | GEO H4 |
| **Fonte primaria, mai vIPI** (committente, 25 settembre): i dati entrano dal DB di IVAO o dai PDF dell'AIP, non dal sito — niente riferimenti circolari | GEO G5 · AIRWAY B8 · tutto F6 |
| **Coordinate in una forma sola**: col punto (`N045.00.00.000`), anche dove oggi sono compatte | GEO G6 · (da decidere per `.vfi`/`.tfl`, dove la forma compatta è la regola del file) |
| **Semplifica / densità** (tolleranza in metri, archi a N gradi) | GEO G4 · F1 archi |
| **«Chi lo usa»**: indice dei riferimenti a un nome (fix, navaid, attesa, punto VFR); rinomina che aggiorna tutto, togliere impedito se usato | NAVAIDS L2 · L1 (attese) · §7 (rotte VFR per nome) |
| **Ogni modifica lascia una traccia** (riga di changelog proposta, `delete.upd`, `ITALY.isc`) | CHANGELOG C1, C4 · ACC A9 |
| **Niente commenti in coda** (committente, 24 settembre: «dopo una riga letta da Aurora non vanno commenti `//`»). Oggi **713 righe in 70 file** (`limm.mva` 374 `//Coast`, `limc.sid` 34, `itawlow.lairway` 28 `BREAK`, `FRA.artcc` 18, `GCI.tfl` 19…); `SectorError.log` di Aurora vuoto, quindi non le segnala. 🔴 `limc.sid:…;0;OSKOR; //SUPER-HEAVY-A321`: il commento sta nell'8° campo (`RNAV`). → controllo «commento in coda» · gesto «sposta il commento sopra» (riga o file) · garanzia con test che il Lab non ne scrive mai. **Avviso**, non errore: lo sviluppatore di Aurora dice che una riga col `//` in coda si legge in **circa il doppio del tempo** (lentezza, non dato sbagliato). Pulizia di tutto il sector in un colpo: in un ramo (F4) | tutti |
