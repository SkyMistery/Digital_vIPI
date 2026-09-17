# Soglie «mai in partenza» e «mai in arrivo» nel ripiego sul vento (17 settembre 2026)

> Stato: ✅ **online in 1.32.0** (§A68). 🟡 Le chip rosse al posto delle caselle (§A69) in main, NON in pacchetto.

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
- **Chi passa che cosa**: documenti (`PistaInUso`) la sezione mostrata; banco di prova le righe in modifica; **vAWOS,
  vista rapida ed elenco aeroporti la release in vigore** attraverso una porta sola, `IPisteDalPubblicato` (regole, LVP e
  soglie escluse dalle sezioni congelate; l'anagrafica viva solo senza documento pubblicato o con la sezione in Live).
  ⚠️ Fino al 17-set-2026 vista rapida ed elenco leggevano regole e flag dal vivo: decisione del committente di allinearli.
  L'elenco fa una lettura di release per scalo, in fila nel caricamento — non nel giro del meteo, che gira fuori dal
  render e sovrapporrebbe letture sullo stesso DbContext.
- ⚠️ **Il salvataggio delle piste cancella e riscrive le righe**: i flag passano dall'editor, quindi viaggiano con la riga.
- ⚠️ **Il merge da IVAO** li tiene, e una riga orfana con un flag acceso **conta come lavoro editoriale**.
- **Editor**: nella tabella piste una colonna «Mai usare» con due **chip**, DEP e ARR (`sh-chip`, come APP procedures, Patterns e Circling), **rosse** quando accese (`sh-chip.no`, red-600 con testo bianco, 5,56:1); in lettura le sole chip accese, rosse e senza gesto. ⚠️ Fino a 1.32.0 erano caselle: il committente le ha chieste allineate al resto (17-set, §A69).
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
- Vista rapida ed elenco aeroporti (vento reale 080/13): con le caselle **vive** che escludono entrambe le soglie in tutti e
  due i versi — dai vivi uscirebbe «—» — mostrano «25 dep · 07 arr» come vAWOS e documento pubblicato.
- Sonde `.claude/skills/verifica-live/mai-usare-verifica.js` e `pannelli-pubblicato-verifica.js`.
