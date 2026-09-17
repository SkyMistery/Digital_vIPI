# Soglie «mai in partenza» e «mai in arrivo» nel ripiego sul vento (17 settembre 2026)

> Stato: 🟡 **in main, NON in pacchetto** (§A68 di `docs/lavori-aperti.md`). **Migrazione ADDITIVA** `PisteMaiUsarePerVerso` (due colonne booleane). Provata dal vivo su copia di produzione.

**La richiesta del committente:** nell'editor marcare una pista come «mai usare», così che **quando nessuna regola
pista vale** quella pista non venga comunque mai scelta; poi, per verso: «mai in partenza» e «mai in arrivo».

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

1. **Il flag vale solo per il ripiego sul vento.** Una regola che nomina esplicitamente una soglia esclusa continua a
   valere; l'editor delle regole lo **segnala** con un avviso (come la pista inesistente), non lo impedisce.
2. **Solo interno**: nessuna etichetta per il lettore; si vede l'effetto sulla pista in uso.
3. **Due flag, per verso** (seconda richiesta, stesso giorno, prima di spedire): «mai in partenza» e «mai in arrivo»,
   combinabili — una soglia usata solo per gli arrivi si marca «mai in partenza».
4. **Il vAWOS legge il pubblicato** anche per questi flag, come per regole e LVP (decisione del 15 settembre).

## 3. Com'è fatta

- **Dato**: `AirportRunway.NeverDeparture` e `NeverArrival` (bool, nascono `false` = usabile). ⚠️ Qui `false` è il
  default giusto: sono flag opt-**in**, quindi la trappola del bool che nasce falso non morde. Migrazione additiva
  `PisteMaiUsarePerVerso`, SQLite + MySQL (la prima stesura a colonna unica non è mai stata spedita ed è stata sostituita).
- **La regola sta nel motore, in un posto solo**: `RunwaySuggestion.Suggest(idents, dir, kt, RunwayExclusions)`.
  - le soglie escluse in **tutti e due** i versi escono dalla classifica;
  - **partenze e arrivi si scelgono ciascuno fra le soglie ammesse in quel verso** (con la regola delle parallele:
    destra partenze, sinistra arrivi); senza esclusioni l'esito è identico a prima (test);
  - un verso senza soglie ammesse ha `DepIdent`/`ArrIdent` **null**. ⚠️ I chiamanti non ripiegano più su
    `Best.Ident`: sarebbe proporre per gli arrivi una soglia «mai in arrivo»;
  - tutte escluse in tutti e due i versi → `SuggestionReason.AllNeverUse` (distinto da `NoRunways`).
- **Trasporto**: `RunwayRow` (anagrafica), `AirportRunwayRowView` (sezione, quindi **si congela con la release**; gli
  snapshot di prima valgono «nessuna esclusione»), `RwEdit` (editor, conversione unica `Da`/`AllaRiga`). Ognuno ha
  `Esclusioni(...)`, che è il dato passato al motore.
- **Chi passa che cosa**: documenti (`PistaInUso`) la sezione mostrata; **vAWOS la sezione Piste della release in vigore**
  (`AwosService.DalPubblicatoAsync`, accanto a regole e LVP), l'anagrafica viva solo senza documento pubblicato o con la
  sezione in Live; banco di prova le righe in modifica; vista rapida ed elenco aeroporti l'anagrafica viva (come per le
  regole: preesistente, §A68).
- ⚠️ **Il salvataggio delle piste cancella e riscrive le righe**: i flag passano dall'editor, quindi viaggiano con la riga.
- ⚠️ **Il merge da IVAO** li tiene, e una riga orfana con un flag acceso **conta come lavoro editoriale**.
- **Editor**: nella tabella piste una colonna «Mai usare» con due caselle, DEP e ARR; in lettura «DEP», «ARR», «DEP · ARR».
  Nell'editor delle regole: avvisi `Ape_IssueRuleNeverDepRw` / `Ape_IssueRuleNeverArrRw`, **solo nel verso escluso**; il
  banco di prova scrive «nessuna (tutte escluse)» per un verso senza pista.

## 4. Verifica (17 settembre 2026, copia di produzione in MariaDB)

- Migrazione applicata all'avvio.
- LIBD, vento 070/12, nessuna regola: banco «DEP 07 · ARR 07»; spuntata 07 DEP → «DEP 25 · ARR 07»; caselle rilette dopo il
  ricarico.
- vAWOS (API, METAR di prova sempre diverso per non incontrare cache): prima della pubblicazione «DEP 07 · ARR 07»; pubblicato
  LIBD → «DEP 25 · ARR 07»; poi marcata **anche** la 25 «mai in partenza» **senza pubblicare** → resta «DEP 25 · ARR 07»
  (dal vivo avrebbe dato nessuna pista in partenza): legge davvero il pubblicato.
- ⚠️ La **pagina** vAWOS subito dopo la pubblicazione mostrava ancora il vecchio: cache breve della pagina, non un difetto
  del calcolo (l'API nello stesso istante era giusta).
- Sonda `.claude/skills/verifica-live/mai-usare-verifica.js`.
