# Importare una tabella — incolla, CSV, XLSX

> Carta prima del codice ([FEATURE-PROCESS](../FEATURE-PROCESS.md)). Decisa il 2 settembre 2026.
> Stato: ✅ **eseguita** (ramo `import-tabelle`, dieci fette). Le decisioni che l'esecuzione ha cambiato
> sono scritte qui sotto dove stavano quelle vecchie — non in fondo.

## Context

Le tabelle dei documenti si compilano **una cella alla volta**. Il corpus vero non è ipotetico: quindici
`*_SOP.pdf` in `MIL vSOP IVAO/` e tre IPI/LoA in `RealDOCS/`, tutti con tabelle già scritte da qualcun
altro. Il costo per riga — apri, aggiungi riga, scrivi quattro celle — moltiplicato per le righe di
quindici SOP **è il motivo per cui i documenti non vengono compilati**. È lo stesso ragionamento che ha
prodotto l'incolla delle clausole (`ClausePaste`), e questa carta lo generalizza.

⚠️ **Non si aggiunge un secondo meccanismo di incolla**: `ClausePaste` diventa una *spec* di quello nuovo
(domanda 1 del pre-flight — estendi o sostituisci, non affiancare).

## Che cosa si importa, e che cosa no

| Famiglia | Chi | Forma | Import |
|---|---|---|---|
| **A. Generica** | blocco `BlockFormat.Table` **senza** `variant` | `{"columns":[…],"rows":[{"cells":[…]}]}` | ✅ colonne libere |
| **B. Colonne fisse, celle libere** | `milcallsigns`, `milparkings` (`MilTablePayload`) | `{"variant","rows":[[…]]}` | ✅ N colonne dal profilo |
| **C. Legate a catalogo** | `mildiversion`, `milnavaids`; editoriali strutturate (`separations`) | righe che **citano** entità DB | ✅ con risoluzione celle |
| **D. Derivate** | `frequencies`, `aor`, `coordination`, `weather`, `validity`, `runways`, `sids`, `transition`, `runwayrules`, `minima` | tutto dal DB/live | ❌ hanno già i loro importer |

La regola che separa C da D: **se una riga la scrive una persona, si importa; se la calcola il sistema, no.**

## Il meccanismo — una pipeline, quattro stadi

```
Acquisizione   →   Mappatura     →   Proposta            →   Applica
string[][]         spec+colonne      RigaImportata[]         payload della sezione
```

### Stadio 1 — Acquisizione (`Griglia`)

Puro, senza IO. Da testo o da file a `string[][]`. Le porte:

| Porta | Come si riconosce | Note |
|---|---|---|
| **TSV** | contiene TAB | quel che esce da un foglio di calcolo e da un PDF selezionato per colonne |
| **CSV** | `;` o `,` con conteggio stabile | virgolette RFC4180, doppio apice = apice |
| **Markdown** | righe che iniziano e finiscono con `\|` | si salta la riga `\|---\|` |
| **HTML** | contiene `<table` | ⚠️ **la porta a fedeltà più alta**: è ciò che Excel/Word/browser mettono davvero in clipboard come `text/html`. Le celle sono celle: niente separatore da indovinare, niente multi-parola ambiguo |
| **Larghezza fissa** | scelta a mano | i tagli di colonna si mettono cliccando un righello sull'anteprima |
| **Ancore di spec** | ripiego | la spec fornisce il suo `SplitLine` (vedi §Ancore) |
| **XLSX** | file | zip + `xl/worksheets/sheet*.xml` + `xl/sharedStrings.xml`, **zero pacchetti** |

⚠️ **La virgola non è mai un separatore di colonna quando c'è un'alternativa**: separa già i punti dentro
una cella (`EKMUR, PISIP`), lezione pagata in `ClausePaste`.

⚠️ **Normalizzare prima di spezzare, ma non tutto allo stesso momento.** Fine-riga e spazi unificatori
(`U+00A0`, `U+202F`, `U+2009`) si appianano **sempre**: sono differenze che nessuno vede. Trattini lunghi e
spazi ripetuti si appianano **solo dove si spezza per spazi** (`TestoTabellare.NormalizzaSegni`) — dentro
una cella di un CSV sono contenuto scritto da qualcuno, e riscriverlo sarebbe cambiare il documento mentre
lo si importa. Nelle cinque righe d'esempio degli «Aeroporti alternati» convivono `–` e `-`, e una riga ha
un doppio spazio.

### Stadio 2 — Mappatura (`SpecImport`, senza registry)

Ogni tabella importabile dichiara **un descrittore**. Aggiungere una tabella importabile = scrivere una
fabbrica e passarla al pannello, **zero `switch` toccati** (domanda 2 — regola del 2).

⚠️ **Il registry non c'è, ed è una decisione dell'esecuzione.** L'elenco statico avrebbe dovuto tenere una
copia **neutra** dei titoli di colonna, perché quelli veri stanno nella lingua di chi guarda e arrivano dal
localizzatore della pagina: due definizioni degli stessi nomi, cioè il difetto che il registry doveva
evitare. Le specifiche vivono nelle fabbriche di `SpecTabelle` e `SpecImport`, che sono un posto solo. La
ragione è scritta in fondo a `SpecImport.cs`, dove il registry stava.

```csharp
sealed record TableImportSpec(
    string Key,                          // "generic" | "milcallsigns" | "mildiversion" | …
    IReadOnlyList<ColumnSpec> Columns,   // nome, CellKind, obbligatoria, sinonimi d'intestazione
    Func<string, string[]?>? SplitLine); // ancore, quando i TAB non ci sono

enum CellKind { Text, Number, Decimal, AirportRef, NavaidRef, Level, Coordinate }
```

Intestazione riconosciuta se **almeno metà** delle celle della prima riga combacia con un nome o un
sinonimo di colonna. L'utente può rimappare a mano; per la famiglia A le colonne sono quelle incollate.

### Stadio 3 — Proposta (risoluzione)

Il `CellKind` decide chi risolve, e sono tutti riusi:

- `AirportRef` → archivio scali. **Il nome vince sempre dal DB**; quello incollato si butta.
- `NavaidRef` → anagrafica: 0 risultati = si segnala, 1 = automatico, >1 = **si chiede quale** (è già la
  regola di `MilDiversionsEditor`).
- `Level` → `LevelFormatting.Parse` · `Coordinate` → `CoordinateParser` · `Number`/`Decimal` → si tolgono
  `°`, `NM`, e la virgola decimale diventa punto.

⚠️ **Un codice sconosciuto si segnala e basta.** L'import di *un* documento non crea né modifica un dato
di *tutti*: la riga resta rossa con il link alla pagina d'anagrafica. È la stessa ragione per cui in
`MilNavaidsEditor` un campo che viene dalla sorgente non si modifica.

Uscita: una riga per riga incollata, `(Numero, Grezza, Valore?, Errori)` — la forma di `PastedClause`,
generalizzata.

### Stadio 4 — Approvazione (`ImportaTabella.razor`)

Un solo componente, aperto da qualunque editor con la sua spec. Griglia d'anteprima con colore per cella:
**risolto dal catalogo** · **testo com'era** · **non letto** (il testo originale e il perché stanno nel
fumetto). Poi «Importa N righe», con **in coda** (default) o **sostituisci**.

⚠️ Una cella **ambigua** porta una tendina con i candidati, e ognuno si porta dietro la propria
**identità**: «si chiede quale» deve avere una risposta possibile, o è un rifiuto scritto in modo gentile —
e sceglierne uno deve bastare a scriverlo, senza ricercarlo.

⚠️ **Prima del tasto non si scrive niente.** Un incolla che salvasse metterebbe in archivio la propria
interpretazione di un testo che nessuno ha riletto — e l'interpretazione di una tabella copiata da un PDF
sbaglia, non «potrebbe sbagliare».

## Ancore, non spazi

Il caso reale: «Aeroporti alternati» copiata da un SOP PDF. Nessun TAB, celle multi-parola.

```
AIRPORT NAVAIDS BEARING DISTANCE
LIBA Amendola MNL TAC – 99Y 115.25 308° 72.2NM
```

Spezzare per spazi dà sette colonne e nessuna giusta. Spezza invece per **ancore in testa e in coda**,
dichiarate dalla spec: ICAO `^[A-Z]{4}\b` all'inizio, i due numeri con unità (`\d{1,3}\s*°` e
`[\d.,]+\s*NM`) alla fine; quel che resta in mezzo è la colonna larga. È lo `SplitLine` della spec, e
serve solo quando l'acquisizione non ha trovato un separatore vero.

## Distanza con i decimali

⚠️ `72.2NM` oggi si tronca a `72`: `MilDiversionPayload.Riga.Distance` è `int?`. **Il dato vive nel
`BodyJson`, non in una colonna** — nessuna entità, nessuna migrazione, niente da far passare dalla
finestra cieca. Diventa `decimal?` (non `double`: round-trip JSON esatto), **un decimale**, campo di
validità `0..9999`, reso con `0.#` così che `72.0` non si legga mai.

🔴 **Nota di consegna.** JSON vecchio (`72`) si legge senza problemi nel tipo nuovo. Il contrario no: un
`72.2` letto da **binari vecchi** alza `JsonException`, che `Leggi` cattura restituendo **nessuna riga** —
la tabella sparirebbe a schermo senza un errore. Un rollback dell'applicazione dopo che qualcuno ha
scritto un decimale è quindi visibile all'utente. Il rilevamento resta `int` (sono gradi, e il formato è
`000`).

## Fasi — tutte eseguite

Una fetta per commit, build verde a ogni passo.

| | Fetta | Esito |
|---|---|---|
| 1 | `Griglia` + test — TSV, CSV, Markdown, HTML, larghezza fissa, normalizzazione | ✅ `e04ef3c7` |
| 2 | `LettoreXlsx` + test — zip/XML, senza dipendenze | ✅ `8d56b605` |
| 3 | Distanza `decimal`, e i due numeri separati in due voci | ✅ `5b7484d7` |
| 4 | Spec, mappatura, proposta, risolutori di cella | ✅ `a31a2537` |
| 5 | `ImportaTabella.razor` + famiglia A (prima l'estrazione di `TabellaGenerica`, commit meccanico a parte) | ✅ `62b9845c` + `baaba0e8` |
| 6 | Famiglia B — Nominativi, Parcheggi | ✅ `26ebd7e5` |
| 7 | Famiglia C — `mildiversion` con le ancore, e il risolutore vero sui cataloghi | ✅ `057d9260` |
| 8 | Esportazione CSV | ✅ `9a27cfad` |
| 9 | «Prendi la tabella da un altro documento» | ✅ `beb8eed0` |
| 10 | `ClausePaste` smette di avere una grammatica sua | ✅ `beb8eed0` |

Fra la 8 e la 9, **due giri di correzioni nati dalla verifica live** (`6cae0835`, `be727ae9`).

## Che cosa ha trovato la verifica live, e i test no

Guidando l'editor militare di LIBG e incollando la tabella vera di un SOP (skill `verifica-live`):

1. **L'intestazione finiva fra i dati, in rosso.** Le ancore cercano un ICAO e due numeri; un'intestazione
   non ne ha, quindi restava una cella sola — e una cella sola non nomina metà delle colonne. Ora una riga
   rimasta intera si guarda anche **parola per parola**.
2. **E poi le righe sono diventate tutte vuote.** Un'intestazione *riconosciuta* non è un'intestazione che
   dice **dove**: quella del PDF nomina le colonne una dopo l'altra ma non ne colloca nessuna. Quando non
   colloca niente, le colonne si prendono in ordine.
3. **«Da scegliere» non aveva un modo di scegliere**: la riga restava fuori per sempre.
4. **Il giro esporta→reimporta** ha trovato il **segno d'ordine dei byte** dentro la prima cella
   dell'intestazione: la mappatura «non funzionava» su un file che sembrava giusto.

Nessuno dei quattro sarebbe uscito dai test: i primi tre vivevano nella forma dei dati veri, il quarto
nell'incontro fra due pezzi che i test provavano separatamente.

✅ **Verificato anche l'incolla clausole** di `AdminTrasferimentiPage` dopo il travaso su `Griglia`
(accordo `LIBB_ES_CTR ⇄ LAAA_CTR`): una tabella **Markdown** incollata dà due clausole, l'**intestazione**
`POINTS/LEVEL/RECEIVER` viene saltata invece di diventare una clausola, l'avviso sul ricevente estraneo
all'accordo compare come prima, e con la forma di sempre (tabulazioni) «EKMUR, PISIP» resta **una cella
sola**. Nessun errore in console, nessun 4xx.

⚠️ **Il percorso per arrivarci costa quattro clic non ovvi**, e ci sono voluti cinque tentativi per
trovarli — vale la pena scriverli: ACC dalla barra (`.xt-bar button`) → **controparte**, che nasce
**chiusa** (`.xt-nav-sec`) → accordo (`.xt-nav-flow`) → **sezione**, il cui corpo esiste solo da aperta e
la cui levetta è `.xt-dirtoggle`, **non** un `<details>`. A sezione chiusa il tasto «Incolla tabella» c'è
ed è acceso, ma il modulo non compare: sembra un tasto rotto, ed è solo un corpo non renderizzato.

⚠️ **Sfumatura vista dal vivo e lasciata così**: quando la lettura **salta** una riga — la riga di trattini
di una tabella Markdown, che è impaginazione — i numeri di riga dell'anteprima scalano di uno (clausole
alle righe 3 e 4, mostrate come 2 e 3). Farli tornare esatti vorrebbe dire far portare a ogni cella la riga
da cui viene, per tutta la catena: costo alto per un dato diagnostico. Scritto nel codice dove il numero si
calcola.


## Fuori perimetro

`.docx`, `.ods`, `.xls` binario, estrazione PDF lato server (dipendenza pesante, e il layout a colonne si
perde comunque: il copia-incolla dà lo stesso testo gratis), import del payload JSON grezzo (nessuna
rilettura umana in mezzo), OCR.

## Verifica — fatta

Test unitari sul cuore puro (`Griglia`, `LettoreXlsx`, spec, mappatura, proposta, ancore) e **verifica
live** guidando l'app vera. **4739 test verdi**, build Release verde sui due TFM (`--no-incremental`,
0 avvisi). E2E **non** girati sul ramo: vogliono l'host vivo e vanno fatti sul risultato della fusione.

Quel che è stato guidato a schermo:

- **Aeroporti alternati di LIBG**: incollata la tabella vera di un SOP (trattino lungo su quattro righe e
  corto su una, un doppio spazio, nessun separatore). Tre righe risolte — nome dallo **archivio** e
  frequenza dall'**anagrafica**, non dal testo (GRA rende `117.50`, non il `111.65` incollato) — una
  ambigua che chiede quale, una sconosciuta che resta fuori. `291.3 NM` arrivata intatta nel `BodyJson`.
- **La tendina della cella ambigua**: 0 righe importabili → scelto un candidato → 1, con l'identità giusta.
- **«Da un altro documento»**: gli alternati di LIBG dentro l'editor di LIMS.
- **Incolla clausole** su `LIBB_ES_CTR ⇄ LAAA_CTR`: Markdown letto, intestazione saltata, avviso sul
  ricevente intatto, «EKMUR, PISIP» ancora una cella sola.
