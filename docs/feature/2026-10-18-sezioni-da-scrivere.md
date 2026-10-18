# Le sezioni da scrivere — quante sono vuote e quante hanno qualcosa

**18 ottobre 2026.** Carta di lavoro (S111).

---

## 1. La domanda

Il committente (18 ottobre): «Mi hanno chiesto di mettere in ogni tipo di file un indicatore di quante delle sezioni
obbligatorie non siano mai state toccate e quante invece ci sia almeno scritto o messo qualcosa».

## 2. Cosa è stato deciso (committente, 18 ottobre)

| Bivio | Deciso |
|---|---|
| Quali sezioni contano | Le **editoriali** del catalogo di quel tipo di documento: quelle che scrive una persona. Le derivate (AOR, frequenze, SID, METAR, validità…) si riempiono da sole e restano fuori |
| Dove si vede | Nell'**editor** di ogni documento, con l'elenco delle vuote; e nella pagina **Versioni**, per tutti i documenti insieme |
| Le sezioni nascoste | **Non contano**: nascosta vuol dire «in questo documento non serve» |

E le scelte del filone, che non cambiano il senso della domanda:

- **«Toccata» vuol dire che adesso dentro c'è qualcosa** — testo, tabella, immagine, allegato, una riga di una
  scheda. Non si ricostruisce la storia delle modifiche: una sezione scritta e poi svuotata è vuota, ed è quello che
  serve sapere.
- **Si guarda la versione di lavoro**: la bozza se c'è, altrimenti la pubblicata — la stessa che apre l'editor.
- **La vIPI di ACC si conta per blocco** (Aerovia e ogni gruppo APP) e in totale.
- Le sezioni **libere** aggiunte a mano non contano: non sono obbligatorie per nessuno.
- Visibile a chi può modificare, mai al pubblico.

## 3. Quali sezioni sono «da scrivere»

Una sezione è da scrivere se è **di catalogo** in quel profilo (`SectionCatalog.Find`), è **editoriale**
(`SectionCatalog.KindOf`), e non è in uno di questi tre casi:

| Fuori | Perché |
|---|---|
| I **contenitori**: «Dati generali», «Procedure di terra», «Procedure di volo», «Gestione del traffico», «Carte aeroportuali», «Procedure di partenza», «Procedure di arrivo» | Sono titoli che raccolgono figlie: il contenuto sta sotto, e le figlie si contano loro. Contati, sarebbero «vuoti» per sempre |
| **Coordinate delle soglie** (vSOP militare) | Editoriale di catalogo ma disegnata dalla pagina con i dati delle piste (vedi il commento in `SectionCatalog`): nessuno ci scrive |
| **Aree regolamentate del blocco Aerovia** | Nascono in automatico (`RegulatedSelection.OwnAuto`): mostrano tutte le aree dell'ACC e seguono gli import. È una derivata di fatto |
| **I due versi di «Coordination» della vLOA** | Li rende la pagina dagli accordi. Nel catalogo non hanno una natura scritta e passavano per editoriali: trovato sulla copia di produzione (§7), dove ogni vLOA aveva due vuote che nessuno può scrivere. Non si dichiarano derivate nel catalogo: lì la natura accenderebbe l'interruttore Live/Frozen dell'editor |

Per profilo, oggi (il numero vero lo conta un test):

| Profilo | Sezioni da scrivere |
|---|---|
| vIPI APP, vSOP APP militare, gruppo APP della vIPI ACC | Separazioni, IFR, VFR, Tecnica operativa, Aree regolamentate, Procedure generali |
| Blocco Aerovia della vIPI ACC | Separazioni radar, Procedure generali |
| vIPI d'aeroporto | Procedure generali, le cinque raccolte di carte |
| vSOP militare | le editoriali dell'indice del SOD (planimetria, nominativi, parcheggi, procedure di terra e di volo, aree di lavoro, BOAT, carte…) |
| vLOA | Purpose, General procedures, Military areas coordination and management |

## 4. Quando una sezione ha qualcosa

Basta **un** blocco della sezione (i suoi, non quelli delle figlie) con contenuto:

- testo (Markdown) non vuoto;
- un blocco riusato da un altro (`SharedBlockId`);
- un'**immagine** o un **allegato** (`mediaId`, `ref`);
- un JSON con almeno un valore dentro una lista: le righe di una tabella scritta a mano, di una scheda (separazioni,
  nominativi, parcheggi), le aree scelte. ⚠️ Non basta che il JSON ci sia: alla nascita le sezioni rese dalla pagina
  hanno un blocco segnaposto vuoto, una tabella può avere le intestazioni (`columns`) e nessuna riga, e una scheda
  porta la sua `variant` anche vuota. Intestazioni, `variant` e numeri o interruttori fuori da una lista non sono
  contenuto.

## 5. Dove si vede

- **Editor** (i cinque: ACC, APP, aeroporto, vSOP militare, vLOA): un riquadro «Sezioni da scrivere» — quante
  hanno qualcosa, quante sono vuote, e l'elenco delle vuote col loro percorso. Si aggiorna all'apertura e a «Fine
  modifica».
- **Versioni**: nella riga, una pastiglia solo quando qualcosa manca («3 sezioni vuote»), come quella della
  traduzione — una pastiglia che dice «tutto a posto» su ogni riga si smette di leggere. Il dettaglio nel pannello
  del documento scelto.

## 6. Limiti

- Un documento **unito** si conta da solo: l'editor dell'ospite conta l'ospite.
- Una sezione di catalogo che in un documento non c'è (tolta prima che il catalogo la rendesse fissa) non si conta:
  si contano le sezioni che il documento ha.
- L'elenco delle vuote non porta alla sezione: gli editor non hanno un'ancora per sezione da cui partire.

## 7. La prova sulla copia di produzione (12 ottobre 2026)

Rimontata col permesso del committente e cancellata subito dopo. Il conto su tutti i documenti: **94 su 94, in
94 ms**. Qualche numero: vIPI Milano 4 di 20 (un blocco APP per gruppo, sei sezioni ciascuno), vIPI Roma 1 di 20,
vSOP di Cameri 11 di 28, quasi tutti gli altri vSOP militari completi. Molte vIPI d'aeroporto danno «0 di 0»: hanno
nascosto Procedure generali e Carte aeroportuali (Napoli), e le nascoste non contano. Controllate a campione le
sezioni contate piene: le separazioni hanno righe, le aree di Verona tre id.

A schermo sugli stessi dati, travasati in un SQLite di prova (solo struttura e documenti, senza i nomi dei lock): la
pagina Versioni (pastiglia su 45 righe, riquadro compatto nel pannello), i cinque editor, e a «Fine modifica» il conto
che passa da 4 a 5 dopo un paragrafo aggiunto. Italiano e inglese.
