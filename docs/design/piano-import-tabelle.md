# Importare una tabella — incolla, CSV, XLSX

> Carta prima del codice ([FEATURE-PROCESS](../FEATURE-PROCESS.md)). Decisa il 2 settembre 2026.
> Stato: **in esecuzione**.

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

### Stadio 2 — Mappatura (`TableImportSpec` + registry)

Ogni tabella importabile dichiara **un descrittore**; il registry li tiene. Aggiungere una tabella
importabile = registrare una spec, **zero `switch` toccati** (domanda 2 — regola del 2).

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

Un solo componente, aperto da qualunque editor con la chiave della spec. Griglia d'anteprima con
colore per cella: **risolto dal catalogo** · **testo com'era** · **non letto** (il testo originale e il perché stanno nel fumetto).
Poi «Importa N righe», con **in coda** (default) o **sostituisci**.

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

## Fasi

Una fetta per commit, build verde a ogni passo.

1. **`Griglia` + test** — TSV, CSV, Markdown, HTML, larghezza fissa, normalizzazione. Nessuna UI.
2. **`XlsxReader` + test** — zip/XML, senza dipendenze.
3. **Distanza `decimal`** — indipendente dal resto, cinque punti in cinque file.
4. **Spec + registry + proposta** — con i risolutori di cella; test sul cuore.
5. **`ImportaTabella.razor` + famiglia A** — prima l'estrazione dell'helper JSON tabella duplicato fra
   `DocumentBlocksEditor` e `DocumentSectionsEditor` (commit meccanico separato).
6. **Famiglia B** — Nominativi, Parcheggi.
7. **Famiglia C** — `mildiversion` (con le ancore) e `milnavaids`.
8. **Esportazione CSV** — chiude il giro: esporta, sistema in Excel, reimporta con «sostituisci».
9. **«Prendi la tabella da un altro documento»** — non è un formato, ma sul corpus dei quindici SOP è il
   guadagno più grosso: stessa anteprima, zero parsing, celle già risolte.
10. **`ClausePaste` diventa una spec** — ⚠️ propagazione nello stesso giro: `AgreementFillingTests` e
    `AdminTrasferimentiPage`, o restano due modelli di incolla (domanda 4).

## Fuori perimetro

`.docx`, `.ods`, `.xls` binario, estrazione PDF lato server (dipendenza pesante, e il layout a colonne si
perde comunque: il copia-incolla dà lo stesso testo gratis), import del payload JSON grezzo (nessuna
rilettura umana in mezzo), OCR.

## Verifica

Test unitari sul cuore puro (`Griglia`, `XlsxReader`, spec, risoluzione) e **verifica live** guidando
l'editor vero: incollare la tabella «Aeroporti alternati» di un SOP reale e vederla comparire con lo
scalo risolto dall'archivio, la radioassistenza citata dall'anagrafica e `72.2 NM` intatto.
