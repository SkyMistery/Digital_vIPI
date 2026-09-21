# Documenti collegati (§A109, §A111) — carta

> Chiesto dal committente il 21 settembre 2026 (sera). Decisioni prese in chat, riportate qui come contratto.

## 1. Che cosa si vede

Nella **colonna di destra** dei documenti pubblici, un riquadro «Documenti collegati» **sotto «Link»**.
⚠️ In 1.41.0 stava nel riquadro di sinistra, sopra il «Sommario»; il committente l'ha voluto qui (1.41.1). La
colonna di destra si vede da 1500 px in su: sotto quella larghezza il riquadro non c'è (scelta del committente).
Ordine sempre: **vIPI ACC → documenti APP → documenti d'aeroporto**. Solo documenti **pubblici** (release in
vigore e non nascosti), anche in anteprima e nell'editor.

| Documento aperto | Link |
|---|---|
| vIPI ACC | gruppo **APP** (chiuso): gli APP non remotizzati con documento sotto l'ACC · gruppo **Aeroporti** (chiuso): TUTTI gli aeroporti sotto l'ACC — vIPI se c'è, altrimenti vSOP |
| APP non remotizzato | vIPI dell'ACC · per ogni aeroporto sotto l'APP: vIPI **e** vSOP (quelle che esistono) |
| vIPI / vSOP d'aeroporto | vIPI dell'ACC (testa) · se l'APP che lo controlla è **remotizzato**: `LIRR vIPI · LIRN_US0_APP` (la sezione di quell'APP nella vIPI ACC) · altrimenti il documento dell'APP · l'altra edizione dello stesso scalo |
| vLOA | la vIPI degli ACC italiani coinvolti |

Etichette: `LIRR vIPI`, `LIRR vIPI · LIRN_US0_APP`, `LIBN_APP` (nominativo dell'ente principale del
documento), `LICZ vSOP`, `LICC vIPI`. Dentro ogni gruppo: alfabetico; per lo stesso scalo prima vIPI poi vSOP.

- Nessun fratello: niente link agli altri scali dello stesso APP né agli altri APP.
- Nessun antenato con documento pubblico = nessun link, nessuna intestazione vuota.
- Un bersaglio che sta nella **stessa pagina unita** (§AZ) diventa un'ancora alla sua sezione (`#doc-{id}`).
- Un APP militare con un documento suo è un APP come gli altri. L'«ACC di appartenenza» è quello civile.

## 2. Chi è sopra: la struttura, risalendo

L'albero è quello **effettivo** (`EffectiveHierarchy.ParentMap`, scritto ?? derivato dalla scaletta): lo stesso
della pagina Struttura, costruito dalle **stesse righe** — la lettura è estratta, non ricopiata.

- La catena di uno scalo parte dalla sua posizione più bassa (DEL→GND→TWR→APP) e sale; senza posizioni parte da
  `Airport.ParentCallsign`. L'APP «che lo controlla» è il primo APP della catena **con un documento** (remotizzato
  = la sua sezione nella vIPI ACC; non remotizzato = il suo documento). Se non ne ha, **si sale ancora** (LIBN →
  LIBN_G_APP senza documento → LIBN_APP). Ci si ferma al primo settore ACC.
- L'ACC è il `CenterId` del primo settore ACC della catena; se la catena non ne incontra, l'ACC dell'anagrafica.
- «Sotto l'ACC» e «sotto l'APP» sono la stessa domanda rovesciata: uno scalo sta sotto X se X è nella sua catena.
  La vIPI ACC elenca gli scali il cui ACC risolto è lei — la stessa funzione, così le due direzioni non divergono.

## 3. Quando si decide: scelta A

**Alla pubblicazione si congela la struttura, con TUTTI i candidati; al disegno si tengono solo i pubblici.**

- `DocReleasePayload.Collegamenti`: una lista di **posti** (`DocLinkSlot`), ognuno con le sue **alternative in
  ordine** (`DocLinkTarget`). Al disegno vince la prima alternativa visibile adesso. Così:
  - uno scalo in bozza quando si pubblica l'ACC compare **da solo** quando viene pubblicato;
  - un APP senza documento pubblico cede il posto a quello sopra (risalita risolta al disegno, non congelata);
  - un documento ritirato o nascosto sparisce, niente link morti.
- Si ripubblica solo se cambia la **struttura** (scalo spostato, documento creato dopo).
- Release di prima (campo assente) e anteprima **bozza**: i candidati si calcolano dalla struttura di adesso
  (albero in memoria per 2 minuti). Anteprima di una release (`as=rel:N`): i candidati di quella release.
- ⚠️ La firma del diff (`ReleaseService.Signature`) non guarda i collegamenti: una struttura cambiata non fa
  comparire «modifiche da pubblicare». Voluto: la deriva racconta il contenuto, non la navigazione.

## 4. Pre-flight

1. **Modello**: nessun gemello. I documenti vengono da `IDocumentAdminRepository.ListAsync` (il descrittore
   unico, con release in vigore e nascosto); la struttura dalla lettura estratta di `EfHierarchyEditingService`.
2. **Dispatch**: gli indirizzi da `IDocRoutesRegistry.PublicUrl`, nessuno switch nuovo sugli URL. La regola che
   sceglie i link per famiglia è **una** (`DocumentiCollegati.Capture`), pura e testata.
3. **Ingressi + verifica**: niente da creare. Verifica dal vivo su copia del DB: LIBN vSOP (risalita fino a
   LIBB), LIRN vIPI (APP remotizzato), LIBV (vIPI + vSOP + APP), vIPI ACC LIBB/LIRR (gruppi chiusi).
4. **Propagazione**: additiva. (In 1.41.0 `TocVoce.Href`, `TocGruppo.Chiuso` e `DocumentToc.Collegati`; tolti in
   1.41.1 insieme al blocco nel sommario — il riquadro è `DocumentiCollegatiCard`, l'aiuto `Collegati`.)
