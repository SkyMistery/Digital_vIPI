# Spazi aerei dell'AIP: la tabella sotto l'AoR (17 settembre 2026)

> Stato: 🟡 **in main, NON in pacchetto** (§A65 di `docs/lavori-aperti.md`). **Nessuna migrazione**: le correzioni
> stanno nel JSON della sezione `aor`. `dotnet build -c Release` verde su net8 e net10, suite intera verde.
> Gemella di [2026-09-16-aree-di-lavoro-una-tabella-sola.md](2026-09-16-aree-di-lavoro-una-tabella-sola.md)
> (nome e limiti dal catalogo, nota scritta a mano) e figlia di
> [2026-08-29-spazi-aerei-dal-kmz.md](2026-08-29-spazi-aerei-dal-kmz.md) (l'aggancio settore → volumi).

**La richiesta del committente:** per i settori agganciati agli spazi aerei del KMZ (esempio: `LIBP_APP` →
PESCARA CTR Z1…Z5) l'AoR dei documenti primari mostra anche una **tabella** con nome, base, tetto, classe e
note; le note le scrive chi aggiorna il documento.

## 1. Il dato vero (copia di produzione del 17-set 06:28Z)

- **29 agganci, 8 settori, tutti APP, tutti su volumi CTR.** Nessun settore di ACC agganciato.
- 🔴 **La classe non c'è.** Il KMZ la porta solo quando la *categoria* la dichiara («Airspace class D»): dei
  **114 CTR del file, 113 hanno classe NULL** — Pescara compresa — e l'unico con una classe è `PISA CTR Z3`
  (C). Una colonna «Classe» di sola lettura sarebbe «—» su tutte le righe di oggi.
- Base e tetto ci sono, nella grafia del file (`GND`, `4500 FT AMSL`, `FL135`): è quel che si stampa.

## 2. Decisioni del committente (17 settembre 2026)

1. **Classe modificabile**: si mostra quella del file quando c'è; chi aggiorna il documento la scrive o la
   corregge. Senza valore, «—».
2. **APP e ACC**: la vIPI APP e i blocchi della vIPI ACC — ovunque l'AoR venga da un aggancio.
3. **Solo i settori del documento**: le shape extra aggiunte a mano restano sulla mappa ma fuori tabella.

## 3. Com'è fatta

- **Le righe nascono dalla porta unica.** `ShapePart` guadagna `Name` e `AirspaceClass` (facoltativi), che
  riempie solo `EfSectorShapeResolver.DaAggancio`. Una riga di tabella è un pezzo con `Source == Aip`: la
  tabella dice **esattamente** quel che la mappa disegna — nessuna seconda lettura degli agganci.
- `AorAirspaceTable.Build` (Application/Aor, pura): pezzi AIP dei settori del documento, in ordine di
  disegno, **deduplicati per chiave** (lo stesso volume sotto due settori di un blocco è una riga), più le
  correzioni.
- **Le righe stanno nella vista** (`AccAorView.Airspaces`), quindi **si congelano con la release** insieme alla
  mappa: il pubblico vede note e classe della versione pubblicata. Snapshot vecchi: campo assente → nessuna
  riga.
- **Le correzioni** stanno in `AorExtraShapes.AirspaceEdits` — lo stesso JSON della sezione `aor` che porta già
  shape extra e colori, salvato dalla stessa porta. Chiave = **chiave naturale** del volume
  (`FAMIGLIA|NOME|BASE|TETTO`): un KMZ ricaricato rifà le righe ma non la chiave, e la nota sopravvive.
  ⚠️ Il nome del campo **non** è `Airspaces`: nello snapshot ACC quel JSON contiene l'`AccAorView`, e
  `AccDocumentAssembler` ci legge sopra un `AorExtraShapes` — due campi omonimi di forma diversa farebbero
  saltare la lettura.
- **Classe** accettata solo `A`…`G`; una correzione con classe e nota vuote si toglie.
- **UI**: `AorAirspaceTable.razor`, disegnata **dentro** `AccAor` sotto la mappa (sola lettura in 4 pagine
  senza toccarle), editabile passando `Editing` + `OnSetAirspace` dai due editor.

## 4. Verifica dal vivo (17 settembre 2026)

Copia di produzione del 17-set 06:28Z ripristinata su MariaDB locale (`vipi_aorasp`, porta 3399), app su
`localhost:5199`, Edge headless:

- editor `LIBP_APP`, sola lettura: 5 righe PESCARA CTR Z1…Z5, base/tetto del file, classe «—»;
- «Modifica»: tendina e campo nota su ogni riga; Z1 → classe **D**, Z2 → nota scritta e campo lasciato;
- ricarico dell'editor e lettore in bozza (`as=draft`): classe e nota **ci sono**; zero errori in console;
- JSON salvato: `{"Callsigns":["LIBP_TWR"],"Colors":{},"AirspaceEdits":{…}}` — la shape extra resta;
- lettore pubblicato: **nessuna riga** — la release di `LIBP_APP` ha congelato l'AoR prima della tabella (atteso,
  §5); `LIPR_APP` senza aggancio: nessuna tabella.
- ⚠️ **Il ramo ACC non è provato dal vivo**: in produzione nessun settore di ACC è agganciato. Lo copre la stessa
  `AorAirspaceTable.Build` e la compilazione dell'editor ACC.

## 5. Limiti accettati

- ⚠️ Le **note non si traducono** nella versione inglese, come l'«Applicabilità» delle separazioni.
- ⚠️ Il doppione esatto di chiave (tre nel file, nessuno agganciato) condividerebbe la nota.
- ⚠️ Dove una release congela l'AoR, tabella e note si vedono in pubblico **dopo aver ripubblicato**.
