# Lo stato dei documenti — carta (28 ottobre 2026)

> **Stato: 🔨 in corso** sul ramo `sito/lavori` (filone Sito, voce S133). Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Estende [«Da fare»: una lista sola](2026-08-26-da-fare-una-lista-sola.md), [«Da fare» per cambiamento](2026-09-23-da-fare-per-cambiamento.md),
> [Sezioni da scrivere](2026-10-18-sezioni-da-scrivere.md) e [il ciclo entrante](2026-09-02-il-ciclo-entrante.md).

## La domanda

> «Vorrei rivedere il sistema di gestione di cosa manca e cosa è modificato nei documenti, deve essere più user
> friendly: dalla dashboard posso vedere, anche documento per documento, cosa manca, quali sezioni obbligatorie sono
> vuote e cosa è cambiato da quello pubblico e dall'ultima versione schedulata. […] Oggi mi risulta abbastanza scomodo
> da usare.» — il committente, 28 ottobre 2026. Approvata la proposta in chat lo stesso giorno, con la raccomandazione
> che lo stile «combaci con quello del resto del sito».

## §0 — Cosa c'è (rilevato il 28 ottobre sul codice di `eef3de6f`)

| Domanda | Dove si legge oggi | Il guaio |
|---|---|---|
| Quali sezioni da scrivere sono vuote | pastiglia 📝 in Documenti, pannello compatto nel dettaglio, pannello pieno nell'editor | non è in «Da fare» |
| Cosa è cambiato rispetto al pubblico | tasto «Cosa è cambiato» sulle sole righe «da ripubblicare» | **conta i blocchi** per sezione: una frase riscritta in un paragrafo che c'era già non è un cambiamento (limite scritto in `IReleaseService.DriftFromEffectiveAsync`) |
| Cosa è cambiato rispetto alla programmata | **da nessuna parte** | `ProgrammataAllineataAsync` serve solo a far TACERE la riga «da ripubblicare» |
| Da rivedere (fatti a monte) | «Da fare», «Da sistemare», banner dell'editor; in Documenti una pastiglia sola, la più urgente | nel dettaglio di Documenti le righe non ci sono: si apre l'editor per leggerle |

Una riga di Documenti porta fino a nove pastiglie diverse (stato, unione, 🟢 ciclo, 🕒 ciclo, nessuna release,
traduzione, 📝, impatti, lock), ognuna al suo posto solo quando c'è qualcosa: l'occhio non trova una colonna.

## §1 — Le decisioni (in chat col committente, 28 ottobre)

- **D1. Una scheda sola per documento**, la stessa nel dettaglio di Documenti e in cima all'editor, con quattro voci:
  **Da completare** (sezioni da scrivere vuote, traduzioni mancanti), **Cambiato rispetto al pubblico** (sezioni
  aggiunte, tolte, testo cambiato, con prima e dopo), **Programmata** (uguale alla bozza, o «la bozza ha N modifiche in
  più»), **Da rivedere** (le righe di «Da fare» del documento). Ogni voce porta alla sua sezione.
- **D2. Documenti diventa il cruscotto**: colonne fisse al posto delle pastiglie di stato del lavoro, contatori in testa
  che filtrano, filtro «solo con qualcosa da fare». «Da fare» e «Da sistemare» restano come sono.
- **D3. Il confronto guarda il testo**, non il numero di blocchi. Conseguenza accettata dal committente: più righe «da
  ripubblicare» di prima, perché anche una frase corretta è un cambiamento che il lettore non vede.
- **D4. Le sezioni da scrivere le decide il catalogo**, con un interruttore «facoltativa» che un admin tiene per
  profilo e sezione, sopra la regola di oggi (che resta il punto di partenza).
- **D5. Lo stile è quello del sito**: classi e componenti esistenti (`pill`, `callout`, `cfg-table`, `block`,
  `help`, `muted`, `Icon`), nessun foglio nuovo se non serve.

## §2 — Le fette

1. **Confronto sul testo** (`ReleaseService`): la firma di una sezione porta, oltre al numero di blocchi, i suoi
   segmenti di testo normalizzati (gli stessi della traduzione, `TextSegmenter`). Una sezione è «modificata» se cambia
   il numero di blocchi **o** il testo; la riga del confronto porta le righe tolte e aggiunte. La tabella delle
   differenze le mostra, prima e dopo, sotto la sezione.
2. **La scheda del documento** (componente unico) nell'editor e nel dettaglio di Documenti, col confronto con la
   programmata (nuovo: `DriftFromScheduledAsync`).
3. **Il cruscotto**: colonne, contatori, filtri. Il confronto per tutti i documenti a ogni apertura costerebbe due
   snapshot per documento: si conserva il riassunto calcolato dal giro della deriva (dopo le modifiche, di notte, dopo
   una pubblicazione) in una tabella sua, e la scheda aperta lo ricalcola dal vivo.
4. **Facoltative dal catalogo**: tabella degli interruttori, pagina admin, `SezioniDaScrivere.DaScrivere` li consulta.

## §3 — Limiti dichiarati

- Il confronto legge il **testo**: grassetto, colore di un callout, immagine o allegato sostituiti senza cambiare testo
  non sono un cambiamento del confronto. Gli allegati sostituiti hanno già la loro riga in «Da fare».
- Le derivate congelate (TORA, minimi LVP…) restano confrontate come oggi (`FirmaCongelate`) per la programmata, e
  non hanno righe di testo.

## §4 — Cosa è stato fatto

**Fetta 1 — confronto sul testo** (28 ottobre).
- `TestoDelConfronto.Righe`: il testo di una sezione, una riga per paragrafo; una tabella dà l'intestazione e una riga
  per fila con le celle in fila (« · »). `TestoDelConfronto.Differenze`: righe tolte e aggiunte, allineate con la
  sottosequenza comune più lunga (oltre 250 000 confronti: tutto tolto, tutto aggiunto).
- `ReleaseService`: la firma di sezione porta testo e visibilità (nascosta lei o una madre). Una sezione è cambiata se
  cambiano i blocchi o il testo; nascosta prima e dopo non cambia mai; una nascosta che nasce o sparisce non è una riga.
  Nuovi tipi `ReleaseChangeKind.Hidden` e `Shown`. `ReleaseDiffRow.Lines` porta le righe.
- `ReleaseDiffTable`: sotto la sezione, «Testo cambiato (N righe)» apre le righe «−»/«+» (`DiffRighe`, classi
  `.diff-righe` nel tema, tinte dei callout); a stessi elementi il dettaglio dice «testo cambiato» invece di «3 → 3».
- `DriftFromScheduledAsync` + `StatoDelDocumentoService.SchedaAsync` (servizio della scheda, per la fetta 2).
- Test: `TestoDelConfrontoTests` (7), quattro in `ReleaseGenericFlowTests` (frase riscritta nello stesso blocco,
  stessi a-capo diversi, nascosta prima e dopo, nascondere), tre in `ReleaseDiffTableTests`. La prova rossa è per
  costruzione: prima il confronto guardava solo `Blocchi`, e `Hidden` non esisteva.

**Fetta 2 — la scheda del documento** (28 ottobre).
- `SchedaDocumento` (Components/Doc) prende il posto di `SezioniDaScriverePanel` (tolto, coi suoi test e le sue
  frasi) nei cinque editor, sopra le Versioni (ancora `sec-stato`), e nel pannello del documento della pagina
  Documenti (compatta, con le righe «Da rivedere» di `DocReviewBar` come quarta voce). Voci: da completare (sezioni
  vuote col collegamento `#s-{id}`, frasi da tradurre), rispetto al pubblico (tabella delle differenze col testo), la
  prossima programmata (allineata, o cosa le manca, derivate cambiate, come rimediare). Si rilegge all'apertura, a
  «Fine modifica», col ↻ e dopo una pubblicazione (i cinque editor e la pagina Documenti la richiamano).
- Negli editor «Da rivedere» resta il banner in cima: due liste delle stesse righe nella stessa pagina sarebbero una
  di troppo.
- Test: `SchedaDocumentoTests` (11, ereditano le quattro del riquadro tolto), `La_programmata_si_confronta_con_la_bozza_di_oggi`.

**Fetta 3 — il cruscotto** (28 ottobre).
- Tabella nuova `DocumentComparisons` (entità `DocumentComparison`, una riga per documento, chiave = `DocumentId`,
  cancellata col documento): ciclo in vigore e sezioni diverse, ciclo della prossima programmata, sezioni diverse e
  derivate cambiate. Migrazione **additiva** `AggiungiConfrontiDocumenti` (SQLite e MySQL).
- La scrivono: il giro della deriva (`ImpactDriftUseCase`, per ultimo — riusa il confronto col pubblico che fa già per
  decidere la riga, e confronta con la programmata solo chi ne ha una; il giro intero toglie i riassunti dei documenti
  che non guarda più), la riconciliazione dopo una pubblicazione, e la scheda quando la si apre.
- Pagina Documenti: chip del lavoro in testa alla barra (da fare, da completare, da pubblicare, programmata indietro,
  da rivedere; `?lavoro=`), e quattro colonne fisse in fondo a ogni riga con la loro intestazione appiccicata, al posto
  delle pastiglie traduzione, 📝 e impatti. Una colonna vuota vuol dire «niente». La regola delle colonne, dei chip e
  dei loro numeri è una sola: `LavoroSulDocumento` (Application), così si prova senza la pagina.
- Test: `LavoroSulDocumentoTests` (6), `Il_giro_conserva_il_riassunto_dei_confronti`, `I_riassunti_si_riscrivono_e_si_tolgono`.
