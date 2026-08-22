# Editor APP ed Editor vLOA — cosa fanno davvero (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/services/vsop/{acc}/apps/editor` e `/services/vsop/{acc}/vloa/editor`. Prima carta
> del giro: **la sostanza**. La forma sta nella gemella
> [`2026-08-22-editori-app-vloa-densita-ui.md`](2026-08-22-editori-app-vloa-densita-ui.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## La domanda delle due pagine

«**Scrivo il documento di questo ente**» — un APP non remotizzato, o la vLOA di una coppia di ACC.

Sedicesima e diciassettesima pagina del ramo, e le ultime della ricognizione. Sono **le uniche due che non
hanno mai avuto un giro**: l'editor ACC e quello dell'aeroporto sono stati rifatti il 20 agosto, e queste due
sono rimaste alla forma di prima — stesso componente di sezioni sotto, testata di due generazioni fa sopra.

## Cosa ho trovato

### ⚠️ E1 — Due file di chip identiche che significano cose opposte

Nella sezione **AoR della vLOA in modifica** ci sono, a una sessantina di pixel di distanza e con la
**stessa identica classe `.aor-chip`**, due file di chip:

| Dove | Cosa dice | Cosa fa |
|---|---|---|
| sopra la mappa, da `VloaEditor.AorView` | «Brindisi Military», «Athinai Radar West · estero» | **scrive nel documento** (`ToggleAorSectorAsync`) — il testo lo dichiara: «persistito nel documento» |
| dentro la mappa, da `AccAor` | «LIBB_MIL_CTR», «LGGG_W_CTR» | accende e spegne un poligono **finché sei lì**, non tocca niente |

Stesso aspetto, stessa forma, stesso gesto — e **uno dei due cambia il documento per sempre** mentre l'altro
è una lente. È il difetto `.sector-pick` che il committente aveva trovato guardando una schermata («una
classe non può significare due cose»), qui in una forma peggiore: là i due usi erano su pagine diverse, qui
sono **uno sopra l'altro**.

**Decisione (committente):** chi scrive nel documento **smette di somigliare** a chi non scrive. Il toggle
persistito diventa un **elenco con interruttori**, sotto la mappa e non sopra, con l'etichetta che dice
quanti settori sono nel documento. I chip `.aor-chip` tornano a significare **una cosa sola** in tutta
l'applicazione: accendono un poligono.

⚠️ Nell'editor **APP** il doppione non c'è (la sezione AoR ha una fila sola, quella della mappa): è la
conferma che il problema nasce dall'avere aggiunto il toggle *sopra* un componente che già aveva i suoi chip.

### ⚠️ E2 — Il «?» dell'anteprima bozza è italiano cablato, copiato in TRE editor

Lo stesso identico paragrafo — *«Apre la bozza in una nuova scheda (`?as=draft`): la vedi come sarà, ma non è
ancora pubblica…»* — sta scritto **tre volte a mano**, in `AppEditorPage`, in `VloaEditor` e in
`AccEditorPage`, con l'`AriaLabel` pure lui in italiano. In pagina inglese si legge in italiano.

Sono due difetti in uno: la **lingua** (regola 43: chiavi nuove sempre IT+EN) e la **copia tripla** (regola
123: due elenchi della stessa cosa divergono — qui sono tre). **Decisione (committente):** chiavi IT+EN e
**un componente solo** montato dai tre editor.

Stessa famiglia, in piccolo: `AccAor3d` scrive «Tutti»/«Nessuno» a mano mentre `AccAor` — il suo gemello,
nello stesso identico markup — usa `L["Common_All"]` e `L["Common_None"]`.

### E3 — Nessun «?» di pagina, e nessuna sezione di Guida

Sono le ultime due pagine di lavoro senza aiuto: il sottotitolo è prosa sempre a schermo (regola 7) e non c'è
niente in `GuidaPage` né in `GuideSearchCatalog` per l'editor APP e l'editor vLOA. L'editor ACC ha
`#editor-acc`, l'aeroporto `#editor-aeroporto`; queste due hanno voci nel catalogo che puntano a sezioni
scritte per il **viewer**, non per l'editor.

## Fuori ambito, dichiarato

- **`AppDoc.EnsureAsync` crea il documento al solo caricamento della pagina**, prima ancora di premere
  «Modifica». È il comportamento dichiarato dal giro Nuovo documento («per tre tipi su quattro non crea
  niente — apre l'editor, che crea se serve»), quindi è coerente, non un difetto. Non lo tocco.
- Il **pannello release** in coda (269px misurati) è roba del giro di `ReleasePanel`, non di queste pagine.
- Il **lock del `Document` dura 30 minuti senza heartbeat**: è il buco già dichiarato dal giro Versioni, e
  vale per tutti gli editor allo stesso modo.

## Cosa lascia questo giro

⚠️ **Un difetto si vede quando cambia il modo di leggerlo.** I quattro settori di Atene si chiamavano tutti
«Athinai Radar» anche prima, quando erano chip: nessuno se n'era accorto perché una fila di chip non la si
legge come un **elenco di scelte**. Trasformarla in elenco ha reso visibile un difetto che c'era già —
ed è la ragione per cui la cura di E1 non era solo estetica.

⚠️ **Il testo di un aiuto invecchia come il codice.** Le sezioni di Guida di questi due editor esistevano, e
dicevano «6 sezioni fisse» dove oggi ne ho misurate **undici**: è la stessa prosa-che-promette-il-falso
trovata nei sottotitoli di Sorgenti, Diagnostica, Nuovo documento e Incarichi, ma in un posto dove nessuno
va a controllarla. Chi tocca una pagina rilegge la sua voce di Guida.

## Slice

1. E2: il «?» dell'anteprima diventa un componente condiviso con le sue chiavi IT+EN, montato dai **tre**
   editor; «Tutti/Nessuno» di `AccAor3d` localizzati.
2. E1: il toggle AoR persistito della vLOA diventa un elenco con interruttori, sotto la mappa.
3. E3: sezioni di Guida `#editor-app` e `#editor-vloa`, voci in `GuideSearchCatalog`, «?» in testata su
   entrambe (arriva con la carta gemella, che la testata la rifà).
