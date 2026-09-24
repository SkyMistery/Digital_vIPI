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
