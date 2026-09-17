# Una soglia «mai usare» nel ripiego sul vento (17 settembre 2026)

> Stato: 🟡 **in main, NON in pacchetto** (§A68 di `docs/lavori-aperti.md`). **Migrazione ADDITIVA** `PistaMaiUsare` (una colonna booleana). Provata dal vivo su copia di produzione.

**La richiesta del committente:** nell'editor marcare una pista come «mai usare», così che **quando nessuna regola
pista vale** quella pista non venga comunque mai scelta.

## 1. Che cosa c'è oggi

La pista in uso si decide in due passi (`RunwaySuggestion`): prima le **regole** in ordine (`EvaluateRules`), e se
nessuna vale il **ripiego sul vento** (`Suggest`), che sceglie la soglia col massimo headwind fra **tutte** quelle dello
scalo. Una soglia usata solo in un verso (per ostacoli, procedure, rumore) può così uscire dal ripiego.

Il ripiego si calcola in **cinque** posti, e tutti devono dire la stessa cosa:

| dove | da dove prende le soglie |
|---|---|
| vIPI d'aeroporto e vSOP militare (`PistaInUso.Calcola`) | la sezione Piste **mostrata** (congelata o viva) |
| vAWOS (`AwosComposition.PistaAttiva`) | anagrafica viva |
| vista rapida (`AirportQuickPanel`), elenco aeroporti (`AirportListPanel`) | anagrafica viva |
| banco di prova dell'editor (`AirportRunwayRulesEditor`) | le righe in modifica |

Una riga della tabella piste è **una soglia** (`AirportRunway.Ident` = «16», «34L»).

## 2. Decisioni del committente (17 settembre 2026)

1. **Il flag vale solo per il ripiego sul vento.** Una regola che nomina esplicitamente una soglia «mai usare» continua
   a valere; l'editor delle regole lo **segnala** con un avviso (come la pista inesistente), non lo impedisce.
2. **Solo interno**: nessuna etichetta per il lettore; si vede l'effetto sulla pista in uso.

## 3. Com'è fatta

- **Dato**: `AirportRunway.NeverUse` (bool, nasce `false` = usabile). ⚠️ Qui `false` è il default giusto: è un flag
  opt-**in**, quindi la trappola del bool che nasce falso non morde. Migrazione additiva, SQLite + MySQL.
- **La regola sta nel motore, in un posto solo**: `RunwaySuggestion.Suggest(idents, dir, kt, maiUsare)` toglie quelle
  soglie prima di classificare. I cinque chiamanti passano solo il **dato**. Se restano zero soglie il motivo è
  `SuggestionReason.AllNeverUse` (distinto da `NoRunways`: lì le piste mancano, qui ci sono e sono tutte escluse).
- **Trasporto**: `RunwayRow.NeverUse` (anagrafica), `AirportRunwayRowView.NeverUse` (sezione, quindi **si congela con
  la release**: sul documento pubblicato il flag vale dopo aver ripubblicato, come le regole), `RwEdit.NeverUse` (editor).
- ⚠️ **Il salvataggio delle piste cancella e riscrive le righe**: il flag passa dall'editor, quindi viaggia con la riga.
- ⚠️ **Il merge da IVAO** tiene il flag (aggiorna la riga esistente) e una riga orfana col flag acceso **conta come
  lavoro editoriale**: non si cancella da sola.
- **Editor**: nella tabella piste una colonna «Mai usare» (casella) in modifica; in lettura nell'editor un segno.
  Nell'editor delle regole: avviso `Ape_IssueRuleNeverUseRw` e il banco di prova ripiega senza quelle soglie.

## 4. Verifica

Test del motore (esclusione, tutte escluse, parallele), del salvataggio che conserva il flag, del merge; dal vivo su una
copia di produzione: marcare una soglia, banco di prova col vento che la favorirebbe → sceglie l'altra.
