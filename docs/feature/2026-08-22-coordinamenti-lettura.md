# I coordinamenti si leggono (carta, 22 agosto 2026)

> Ramo `coordinamenti-lettura`. Tocca la **lettura** della sezione Coordinamenti — `CoordTable`,
> `AccCoordinationView`, `CoordinationDerivation.BuildAccTree` — non il modello degli accordi, che resta
> quello di [`2026-08-18-accordi-a-sezioni.md`](2026-08-18-accordi-a-sezioni.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Perché adesso

Quattro attriti riferiti dal committente **guardando i documenti veri**, non i test. Tutti e quattro sono
della stessa famiglia: la sezione contiene le informazioni giuste e le presenta in un ordine e a una densità
che costringono a scorrere per trovarle.

## 1. La prosa nasce chiusa

Un nodo aeroporto dice la stessa cosa **due volte**: in frasi («Brindisi Radar ES trasferisce a Beograd Radar
il traffico con destinazione Tivat LYTV stabile a livello 210 su CRAYE.») e in tabella. Chi consulta il
documento in cuffia legge la tabella, e la prosa distesa lo obbligava a scorrere oltre decine di paragrafi
per arrivarci.

Le frasi vanno dentro un `<details class="coord-prose">` **chiuso**, con un riassunto che dice quante sono.

- ⚠️ **Un blocco per TABELLA, non per aeroporto.** Arrivi e partenze sono due tabelle, e la frase che
  introduce una non introduce l'altra. Sta in `CoordTable`, quindi vale per vIPI ACC, vIPI APP e vLOA con una
  modifica sola; per aeroporto avrebbe voluto codice nelle viste, ripetuto, e la vIPI APP — che nodi-aeroporto
  non ha — sarebbe rimasta scoperta.
- **La stampa lo apre da sé** (`beforeprint` in `vipi-ui.js`) e ne **nasconde il riassunto**: sulla carta il
  testo esteso *è* il documento, e la riga direbbe solo di aprire ciò che è già aperto.
- Il riassunto ha **due chiavi** (`Coord_Prose` / `Coord_Prose_One`): la sola forma plurale dà «1 frasi» e
  «1 sentences» in tutte e due le lingue — lo stesso conto che `XferLabels` ha già pagato.
- Segue la **cultura dell'interfaccia** quando `English=false`, come le intestazioni di colonna della stessa
  tabella (misurato: in pagina inglese escono `Level`/`Next`, e il riassunto esce `Full text (2 sentences)`).
- ⚠️ Il corpo si chiama `prose-body` e **non** `body`: `.coord-sub2 .body` è un selettore *discendente* e
  avrebbe preso anche questo, con l'imbottitura del livello sopra.

## 2. La densità

Un documento ACC porta **decine** di tabelle. Con l'imbottitura generale — pensata per la tabella singola di
una pagina admin — lo spazio fra una tabella e l'altra superava l'altezza delle righe che separava.

| | prima | dopo |
|---|---|---|
| celle `.coord-table` | `10px 12px` | `6px 10px` |
| `.coord-sub` / il suo corpo | `14px` / `14px 16px` | `10px` / `10px 12px` |
| `.coord-sub2` / il suo corpo | `10px` / `12px 14px` | `8px` / `9px 12px` |
| `.coord-tools` | `0 0 10px` | `0 0 6px` |
| «Arrivi»/«Partenze» | stile in linea, due volte | classe `.coord-kind` |

**Misurato**, non deciso a occhio: sul blocco Aerovia di LIBB tutto espanso, `10345 px → 9249 px` (−1096,
−10,6%). Il confronto si fa rimettendo in pagina il foglio vecchio e rileggendo l'altezza.

- ⚠️ Tutto sotto `.coord-wrap`: `.coord-sub`/`.coord-sub2` le usano anche l'editor struttura, le aree
  regolamentate e `SectionNode`, che qui non c'entrano.
- La **stampa** ha già misure sue (`3px 5px`), più strette di queste: non passa di qui.

## 3. La FIR porta il suo ICAO

`Beograd` e `Zagreb` sono LYBA e LDZO solo per chi le ha già in testa. L'etichetta del nodo diventa
**`Nome-ICAO`**: `Greece-LGGG`, `Tirana-LAAA`, `Brindisi-LIBB`.

I nomi restano quelli della **sorgente IVAO** (inglesi, quindi «Greece» e non «Grecia»): tradurli vorrebbe
dire una tabella di nomi FIR che il progetto non ha, e sarebbe un secondo posto dove un nome può essere
sbagliato. Scelta confermata dal committente.

## 4. Dentro un settore l'ordine è la distanza, non l'alfabeto

| scaglione | chi | fra loro |
|---|---|---|
| 0 | **casa** — l'altro capo sta nella nostra stessa ACC | — |
| 1 | le altre ACC **italiane** | alfabetico |
| 2 | le **estere** | alfabetico |

Alfabeticamente la propria ACC — quella che si coordina a ogni volo — finiva in mezzo agli stranieri.
`IsForeign` è il **flag di dominio** (lo mette l'import confinanti), non il prefisso del codice: un ACC
italiano materializzato col prefisso sbagliato resta comunque di casa.

⚠️ «Casa» si legge dal **nostro settore**, non dal documento: lo stesso albero visto da un settore LIBB e da
uno LIRR mette in testa ACC diverse. C'è un test apposta.

## La mappa che è cambiata

`GetSectorAccNameMapAsync` → **`GetSectorAccRefMapAsync`**, che ritorna `AccRef(Name, Code, IsForeign)`.
Nome, codice ed estero servono **insieme e alla stessa domanda** — «sotto quale FIR va letta questa riga, e in
che ordine sta fra le altre»; tre mappe parallele sullo stesso callsign sarebbero tre letture da tenere
d'accordo a mano. Vale anche per la **vLOA**, che condivide `BuildAccTree`.

## ⚠️ Quando si vede

I punti **1 e 2** sono resa: si vedono **subito**, anche sulle release già pubblicate.
I punti **3 e 4** cambiano il **derivato**, e le sezioni pubbliche leggono lo **snapshot congelato**
(`_useFrozen` in `AccVipiPage`): sui documenti già pubblicati compaiono alla **prossima release**. Sulla
bozza (`?as=draft`) si vedono adesso.

## Verifica live (22 agosto, Edge + puppeteer-core, porta 5035, copia del DB)

- **A/B contro `main`** sulla stessa bozza LIBB: **53 righe e 53 frasi prima e dopo**, stessi aeroporti sotto
  ogni ACC; cambiano **solo** etichette e ordine. Serviva perché nella bozza manca il nodo «Brindisi» che la
  release pubblicata mostra — l'A/B dimostra che manca **anche in `main`**: è contenuto, non codice.
- Ordine visto a schermo su ES: `Roma-LIRR · Beograd-LYBA · Greece-LGGG · Tirana-LAAA · Zagreb-LDZO`.
- Prosa: 33 blocchi, **0 aperti** all'apertura del documento; «Espandi tutto» li apre col resto.
- Stampa (`emulateMediaType('print')`): riassunto e chevron nascosti, frasi visibili.
- `sweep.js`: **0 sospetti** di fondo non girato nel tema scuro.
- ⚠️ **Lo scaglione «casa» non è riproducibile sui dati di sviluppo di oggi**: né LIBB, né LIRR, né LIMM hanno
  in bozza un coordinamento interno alla propria ACC. È coperto da due test unitari, non dallo schermo.

## Rete

`CoordinationCharacterizationTests` + `real-coordination.approved.txt`: l'approvato cambia **solo** nella
parte `--- albero ---`; 630 righe, 20 nodi ACC, 70 aeroporti e **170 frasi** identici prima e dopo. Il fixture
`real-maps.tsv` guadagna due colonne (`AccCode`, `AccForeign`).
Nuovi: 4 test su `BuildAccTree` (etichetta, etichetta neutra senza riferimento, ordine, «casa» per settore) e
5 su `CoordTable` (prosa chiusa, singolare, nessuna frase = nessun blocco, modo capofila, inglese).
