# Filone «coordinamenti su più aeroporti» — ramo `fix/coordinamenti-aeroporti`

Aperto il 25 settembre 2026 su segnalazione del committente (schermata dei coordinamenti di un APP, sezione
«Atterraggi»): un accordo per **LICC e LICZ** diceva nel testo solo «con destinazione Catania Fontanarossa LICC», e
in tabella la colonna «Anche per: LICC · LICZ» stava accanto a una riga di LICB senza niente.

## Perché succedeva

`AgreementExpansion` apre una sezione con N aeroporti in N flussi, uno per aeroporto. `CoordTable` richiude le
righe della stessa clausola in UNA (chiave `ClauseId`) e tiene una frase per clausola: sopravviveva quella del primo
flusso, che nominava solo il suo aeroporto.

## Correzione

- `TransferFlowRow.AirportIcaos`: ogni flusso porta l'elenco intero dell'accordo (solo se più d'uno).
- `CoordinationSentences.Compose`/`ComposeLead`: parametro facoltativo `airportIcaos`; la frase ripete solo il pezzo
  `{name} {icao}` del template, la relazione una volta: «con destinazione Bari Palese LIBD e Gioia del Colle LIBV».
  Congiunzione nel template, `AirportsAnd` («e» / «and»). Un aeroporto solo: frase identica a prima.
- Passano l'elenco: derivazione vIPI ACC/APP (`CoordinationDerivation`), vLOA, anteprima dell'editor.
- `CoordTable`: colonna **«Per»** (`Coord_For`, era `Coord_AlsoFor` «Anche per»); ogni riga dice per chi vale —
  l'elenco dell'accordo, o il suo aeroporto se è uno solo. La colonna compare ancora solo se una riga ha più aeroporti.
- ⚠️ Le release già pubblicate portano la frase congelata: il testo nuovo esce alla prossima pubblicazione.

## Verifiche

- Test: 4 in `CoordinationLeadSentenceTests`, 1 in `CoordTableTests`; sul codice di prima i primi non compilano
  (l'elenco non esisteva) e quello della tabella è rosso. Application 2974→2978, Ui 1707→1708, Infrastructure verde.
- Dal vivo su copia del DB (accordo LDZO→APP spostato su LIBA_APP, LIBV aggiunto alla sezione di LIBD): frase
  «… inbound to Bari Palese LIBD and Gioia Del Colle LIBV …», tabella `FOR`: «LIBD · LIBV» e «LIBR».

## Stato

Pronto da fondere quando la CI del ramo è verde.
