# Allegati nei callout e link nel testo — carta (21 ottobre 2026)

**S121** e **S122**, filone Sito. Il committente: «nei callout vorrei la possibilità di inserire allegati. E in
qualunque posto si possa editare del testo dei link».

## 1. Le decisioni del committente (21 ottobre 2026)

| Bivio | Deciso |
|---|---|
| Allegati nei callout | **Un riquadro nel callout**: sotto il testo uno o più allegati della biblioteca, mostrati come nel blocco «Allegato» (link o PDF incorporato, altezza, rotazione). In più un tasto nella barra del testo per mettere un allegato **dentro la frase** |
| Dove i link | **Ovunque si scrive testo**. Le **note** dei dati strutturati (LVP, regole piste, tabelle delle aree, trasferimenti) invece **spariscono**: il testo libero si scrive coi blocchi del documento. È **S123**, a parte |

Le scelte del filone:

- **Un tasto solo, «Link»**, con due chip dentro il selettore — *Indirizzo web* e *Allegato* — e non due tasti in
  barra: il gesto è lo stesso, «porto chi legge da un'altra parte». Le parole selezionate nel campo diventano il testo
  del link, e il link prende il loro posto.
- **Solo `http` e `https`**, assoluti, con un host e senza credenziali (`LinkWeb.Sicuro`). Ad agosto il renderer era
  stato tenuto chiuso a ogni link diverso dagli allegati per non far entrare `javascript:` e simili in un `href` che
  costruiamo noi: con la lista chiusa quel rischio non c'è, e il test che lo presidia c'è ancora (gli schemi vietati
  sono di più).
- **Il link web esce dal sito come le altre uscite**: scheda nuova, `rel="noopener noreferrer"`, classe `md-ext`; in
  stampa l'indirizzo tra parentesi, come per gli allegati.
- **Nelle celle delle tabelle i soli link** (`MarkdownLite.SoloLink`): una cella resta testo, senza grassetti né
  elenchi. Il tasto «Link» sta sotto la tabella accanto a «Cita», e vale per l'ultima cella col fuoco.
- **Gli allegati del callout si scelgono dalla biblioteca**, mai incollando un indirizzo: il registro «chi cita cosa»
  li conta da solo, perché legge `allegato:slug` nel JSON grezzo di ogni blocco.

## 2. Come è fatto

| Pezzo | Dove |
|---|---|
| Il JSON del callout | `CalloutJson` (`Vipi.Application/Content/CalloutJson.cs`): `{"title":…,"allegati":[…]}`, ogni allegato nella forma di `AttachmentRef`. Nelle pagine (intro, sezioni extra) lo porta `ExtraBlock.CalloutJson` |
| 🔴 Callout ≠ payload | `SectionPayload.EEditoriale` riconosceva un callout solo col **titolo da solo**: con un'altra chiave accanto il blocco passava per il payload di una scheda, spariva dall'editor e una scheda poteva scriverci sopra. Ora la forma la dice `CalloutJson.EForma`: solo `title` e/o `allegati` |
| Lettura | `CalloutBlock` disegna gli allegati sotto il testo con `AttachmentLink`, lo stesso componente del blocco «Allegato» |
| Editor | `CalloutAllegatiEditor`, montato dai due editor di blocchi: un `AttachmentBlockEditor` per allegato, la «×», e «+ Allegato» che apre un posto vuoto |
| La regola del link web | `LinkWeb` (`Vipi.Application/Content/LinkWeb.cs`): la regex, `Sicuro` e `Scrivi`. `Scrivi` codifica parentesi tonde e apostrofi dell'indirizzo (Wikipedia ne è piena), che chiuderebbero il link a metà |
| Il renderer | `MarkdownLite`: il link web si riconosce **per primo** sul testo encodato e si mette da parte con un segnaposto mentre girano grassetto e corsivo — un indirizzo con `__` o `*` dentro avrebbe preso dei tag nell'`href` —, poi l'ancora si ricompone. L'indirizzo si decodifica, si rilegge con `LinkWeb.Sicuro` e si encoda per l'attributo |
| Traduzione | `TextProtector`: come per l'allegato, la `[` e la coda `](https://…)` non arrivano al motore, e `StessiRiferimenti` pretende gli stessi indirizzi in una resa a mano |
| Editor del testo | `RichTextArea` (tasto «Link»), `TastoRiferimentoTabella` (sotto le tabelle), il selettore `LinkPicker`; in JS `vipiSidTesto` legge le parole selezionate, e il resto è il meccanismo della SID citata (`vipiSidPrendi`/`vipiSidInserisci`) |
| Guida | La sezione dei blocchi: allegati in una callout, e il link dentro una frase |

## 3. Che cosa non fa

- Un link web **non entra nel registro «chi cita cosa»** e non si aggiorna: se il sito di là cambia, il link si rompe.
  Per un file che deve restare la strada è la biblioteca.
- Il **titolo** di un callout resta senza editor (c'era già così): lo scrivono le sezioni seminate.
- I link nelle **note** dei dati strutturati non ci sono: le note se ne vanno con S123.

## 4. Verifica

- Test: Application `CalloutAllegatiTests`, `LinkWebTests`, `TraduzioneRiferimentiTests` (link web); Ui
  `BloccoAllegatoTests` (callout), `MarkdownLiteLinkTests` (link web, celle, schemi vietati), `LinkNelTestoTests`
  (selettore e tasto della barra).
- A schermo, host locale su un database nuovo con uno scalo inventato (LIRQ) e una voce di biblioteca di prova: un
  callout con un allegato scelto dalla biblioteca; nel suo testo le parole «sito di Eurocontrol» selezionate e il tasto
  «Link» → `[sito di Eurocontrol](https://www.eurocontrol.int/)` al loro posto; in una tabella la parola «carta»
  selezionata in una cella e «Link» → allegato. Nell'anteprima della bozza: il link nel testo del callout, l'allegato
  sotto col segno «esterno», e nella cella il link all'allegato. Non visti: la stampa, il telefono, un documento
  tradotto.
