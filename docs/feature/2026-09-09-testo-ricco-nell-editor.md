# Testo ricco nell'editor: grassetto, corsivo, sottolineato, elenchi (9 settembre 2026)

> Chi redige aveva un campo di testo nudo e una sintassi che nessuno gli aveva mai detto. Ora ogni campo di
> prosa ha la sua **barra**: grassetto, corsivo, sottolineato, elenco puntato, elenco numerato — e gli elenchi
> il documento li sa finalmente **rendere**.

## Da dove nasce

Una segnalazione: «va tutto su una riga sola» nella preview di alcuni documenti, vista in una sezione custom
della vIPI di **LIMC**. E due richieste: poter scrivere in grassetto/corsivo/sottolineato, e poter fare
elenchi.

### L'a capo: il difetto NON si è riprodotto — e questo è un risultato, non una resa

Percorso ripercorso per intero, e ogni pezzo assolto:

| pezzo | verdetto |
|---|---|
| `MarkdownLite.Render` | rende `"Test\nVEst"` come `<p>Test<br>VEst</p>`. **Provato eseguendolo**, non letto. |
| il salvataggio (`SaveBlock` → `UpdateBlockAsync`) | passa il corpo intero, non tocca niente |
| il db | i sei corpi con a capo ce l'hanno, `\n` compreso — uno è il `Test\nVEst` della segnalazione |
| la traduzione | **conserva** gli a capo: 4 unità multi-riga in memoria, **0** appiattite |
| il CSS | nessuna regola tocca `br` |
| il dispatch | una strada sola: `BlockRenderer` → `CalloutBlock` → `MarkdownLite` |

🔴 **Un'ipotesi buttata per strada, e vale la pena scriverla.** La prima diagnosi era: il motore di traduzione
appiattisce, perché `SplitProse` taglia solo sui paragrafi (`\n\n`) e un `\n` singolo viaggia **dentro** il
segmento, dove nessuno lo protegge — c'è la riparazione del grassetto (`RiparaGrassetto`, nata il 28 agosto
proprio così) e non c'è quella degli a capo. Il ragionamento era giusto, il fatto no: **interrogando
`TranslationUnits` si vede che Azure li ha conservati tutte e quattro le volte**. La fix che ne discendeva
avrebbe cambiato la segmentazione, orfanato le voci in memoria e **rispeso i caratteri** per niente.

Quindi il difetto resta **non riprodotto**. Ma non c'era **una sola prova** che ne presidiasse il percorso, e
un difetto che torna su un percorso non presidiato costa la stessa indagine da capo. Adesso ci sono, e sono
messe **dove è stato segnalato**: `MarkdownLiteTests` sul renderer, e due prove bUnit su `BlockRenderer` che
attraversano il dispatch e il markup del callout.

## Le decisioni

1. **Il testo si legge RIGA PER RIGA.** È il cuore del cambiamento. Prima `Render` era una catena di
   `Replace` sul testo intero; adesso normalizza i fine riga, taglia in righe, **classifica la riga** e solo
   dopo applica l'inline. Non è un rifacimento per gusto: è l'unico ordine in cui gli elenchi sono possibili
   senza litigare col corsivo (`* voce` sarebbe finito in `<em>`), e si porta dietro due difetti veri che
   nessuno aveva ancora incontrato — vedi sotto.
2. **Gli elenchi sono MARKUP, non un formato di blocco.** `BlockFormat.List` esisteva già nell'enum ed era
   instradato a `ProseBlock` **identico** a `Prose`: prosa travestita. Restava la tentazione di dargli un
   corpo vero. No: un elenco è tre righe dentro un paragrafo, non un blocco da aggiungere, spostare e
   cancellare. **Nessun tocco all'enum, nessuna migrazione** — e quindi spedibile dentro la finestra cieca.
3. **Il sottolineato è `__testo__`.** Nel Markdown di scuola `__` è un secondo modo di scrivere il grassetto;
   qui il grassetto è `**` e basta, quindi `__` era libero. **Contato prima di deciderlo**: sui corpi reali
   ci sono **zero** occorrenze di `__`, quindi la sintassi non ruba niente a nessuno.
4. **La barra è sempre nel DOM e si accende col fuoco in CSS**, non in C#. Vedi sotto: è la decisione con più
   ragioni dietro di tutta la carta.
5. **Niente elenchi annidati.** Una voce rientrata è una voce come le altre. Annidare vuol dire portarsi
   dentro l'ambiguità dei livelli a spazi, e nei documenti operativi un elenco a due livelli non è mai
   servito.

## Perché la barra non compare col fuoco

Sembrava più pulita: barra nascosta, appare quando entri nel campo. Quattro ragioni contro, e sono tutte di
sostanza:

- **Un giro di rete per ogni fuoco.** Blazor Server: legarla a `@onfocus`/`@onblur` è stato in C# più un
  round-trip a ogni entrata e uscita da ogni campo. Con venti blocchi aperti la barra arriva in ritardo sul
  fuoco.
- **Il `blur` parte PRIMA del `click`.** Una barra che sparisce sul blur sparirebbe **mentre la clicchi**.
- **Il salto di layout.** Comparendo, la barra fa crescere il blocco e scivolare in giù tutto quello sotto —
  lo stesso salto che `vipi-editor.js` già compensa a mano per la fisarmonica dei blocchi ACC. Occupando lo
  spazio sempre, non c'è niente da compensare.
- **La scoperta.** Chi redige non sa che gli elenchi esistono finché non vede il tasto.

La terza via presa: **sempre nel flusso, attenuata a `opacity:.5`, accesa da `:focus-within`** (e da `:hover`,
e da un tasto che prende il fuoco da tastiera). Zero stato, zero rete, zero salto. Attenuata e **non**
nascosta: `display:none` la toglierebbe anche alla tastiera e al lettore di schermo.

## Come è fatta

| pezzo | che cosa fa |
|---|---|
| `MarkdownLite` | riscritto a righe: capoversi, `<ul>`/`<ol>` con `class="md-list"`, inline per riga |
| `RichTextArea.razor` | la porta sola dei campi di prosa: barra + textarea + etichetta facoltativa |
| `vipiMdWrap` / `vipiMdList` | i gesti, in `vipi-editor.js`: lavorano su `selectionStart/End` e **fanno da interruttore** |
| `.rta` / `.rta-bar` / `.rta-btn` | il vestito, con `:focus-within` |
| `.md-list` | gli elenchi resi; su carta una **voce** non si spezza fra due pagine |

Sei campi convertiti: paragrafo, callout e nota d'allegato nei **due** editor di blocchi
(`DocumentSectionsEditor`, `DocumentBlocksEditor`), più l'introduzione VFR dell'APP.

### Tre trappole che non si vedono dal codice finito

- 🔴 **Il `change` sintetico.** Scrivere `el.value` da JS **non fa scattare nessun evento**. Senza il
  `dispatchEvent(new Event('change', {bubbles:true}))` in fondo a ogni gesto, il testo cambierebbe a schermo e
  **non tornerebbe mai nel modello**: sparirebbe al primo re-render. È l'unica riga che lega i tasti a Blazor.
- 🔴 **`@onmousedown:preventDefault` sui tasti.** Senza, premere il tasto toglie il fuoco alla textarea e il
  browser **azzera la selezione**. Quando il click arriva, il gesto formatterebbe il nulla.
- 🔴 **L'etichetta è un parametro, non un involucro.** Dentro un `<label>`, un click su un tasto della barra
  attiva l'etichetta, che rimanda il fuoco alla textarea — e nel rimandarlo azzera la selezione, cioè proprio
  la cosa che il tasto stava per formattare. Il legame si fa con `for`/`id`.

### Due difetti chiusi di sponda

Non erano stati segnalati da nessuno; li ha scoperti il taglio a righe, e adesso hanno una prova ciascuno.

- **I fine riga di Windows.** `"\r\n\r\n"` non contiene `"\n\n"`: due capoversi battuti su Windows uscivano
  **attaccati**, in un `<p>` solo, con un `\r` orfano dentro.
- **Il marcatore spaiato.** Un `*` dimenticato si mangiava tutto fino al successivo, **a capi compresi**:
  poteva mettere in corsivo mezza sezione. Ora al massimo perde la sua riga.

E uno **ripreso**: gli elenchi già scritti a mano coi `•` — quelli veri, visti sul primo SOP il 28 agosto —
diventano elenchi veri senza che nessuno debba riscriverli.

## Conseguenze accettate

- **Grassetto e corsivo non attraversano più un a capo.** `**a\nb**` non è più un grassetto. È il prezzo del
  taglio a righe, ed è lo stesso prezzo che rende innocuo il marcatore spaiato.
- **Gli estratti della ricerca mostrano i marcatori.** `EfSearchRepository.Snippet` prende il corpo grezzo:
  in un risultato si leggerà `- voce` e `**testo**`. Era **già vero** per il grassetto; adesso ci sono anche
  i trattini. Non si tocca in questa carta: ripulire l'estratto vuol dire cercare il match sul testo ripulito,
  cioè spostare gli indici della finestra, e non è un ritocco da fare di straforo dentro un altro lavoro.
- **Niente annidamento**, per scelta (decisione 5).

## Che cosa NON è cambiato

Lo schema, l'enum `BlockFormat`, la segmentazione della traduzione, le release congelate. Un documento
salvato prima si rende come prima — salvo i due difetti di sopra, che si raddrizzano da soli.

## La verifica

- `MarkdownLiteTests` — 18 prove: a capo (LF, CRLF, CR), capoversi, i tre marcatori inline, i due elenchi,
  il numero di partenza conservato, `*corsivo*` che **non** è una voce, il marcatore spaiato che non
  attraversa le righe, l'encoding che regge.
- `BlockRenderingTests` — due prove nuove **sul posto segnalato**: un callout manda a capo, la prosa manda a
  capo e rende gli elenchi.
- Tutta la soluzione verde: Ui 1434, Application 2287, Infrastructure 1352, Domain 140, Assets 57,
  Hosting 66, AuroraProfiles 63, AuroraBridge 79.
- ✅ **La prova viva, fatta** (`.claude/skills/verifica-live/testo-verifica.js`, editor ACC di LIBB su una
  copia del DB): quattordici controlli verdi. Avvolge la selezione; premuto di nuovo **sguscia**; corsivo e
  sottolineato; tre righe battute diventano `- uno / - due / - tre` e poi `1. 2. 3.` e poi tornano nude; col
  **cursore su una riga sola** marca solo quella; Ctrl+B e Ctrl+U. E il controllo che conta: quel che i tasti
  hanno scritto — mai battendo un marcatore — **sopravvive a un ricarico della pagina**, cioè il `change`
  sintetico arriva davvero nel modello. Riletta in sola lettura, la stessa sezione rende
  `<p><strong>foxtrot</strong> golf</p><ul class="md-list"><li>hotel</li><li>india</li></ul>`.
- ✅ **La barra a schermo**, nei due temi: `opacity` 0.5 a riposo e 1 col fuoco, misurata dal `getComputedStyle`.

🔴 **Due rossi del primo giro erano l'ATTREZZO, non il prodotto**, e vale la pena scriverli perché sono
già nel libro delle trappole di questa casa: (1) i campi stanno in `<details>` **collassati**, dove
`innerText` torna vuoto e i tasti non arrivano — si apre tutto con `vipiEditorSections(true)`; (2) svuotare
un campo con triplo clic + Backspace cancella **una riga**, e la prova finiva per scrivere sopra i propri
avanzi, rendendo false tutte le asserzioni dopo la prima. E una terza, nuova: il `clip` di
`page.screenshot` è in coordinate di **pagina**, non di viewport — senza sommare lo `scrollY` si fotografa
un pezzo qualunque del documento credendo di guardare il campo.
