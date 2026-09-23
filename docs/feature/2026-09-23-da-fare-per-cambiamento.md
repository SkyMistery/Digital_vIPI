# «Da fare» per cambiamento — carta (23 settembre 2026)

> **Stato: ✅ eseguita il 23 settembre 2026** (§3), provata a schermo, sul ramo `dafare/raggruppa` (filone [lista-da-fare](../filoni/lista-da-fare.md)).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Estende [«Da fare»: una lista sola](2026-08-26-da-fare-una-lista-sola.md)
> e [Documenti da rivedere](2026-08-25-documenti-da-rivedere.md). Non aggiunge meccanismi.

## La domanda

> «Se vado in miei task c'è un elencone: non si potrebbero compattare per "cosa è cambiato"? Tu clicchi su cosa
> è cambiato e ti dice tutti i documenti su cui intervenire.»

## §0 — Cosa c'è (rilevato il 23 settembre sul codice di `6b5b438d`)

Una riga per **(documento, fatto)**. Su una copia del database locale del 15 settembre: 64 righe aperte, di cui
**48 «area cambiata» nate da 9 aree** su 13 documenti, 15 «da ripubblicare», 1 allegato sostituito. Chi apre la
lista legge 48 volte nove fatti.

**Che cosa sa ogni riga della sua causa**, ed è il vincolo di tutta la carta:

| Famiglia | La causa è salvata? | Come si raggruppa |
|---|---|---|
| Eventi: settore sparito/nascosto/riparentato/rinominato/eliminato, area sparita/cambiata, allegato sostituito/eliminato, aeroporto passato d'ACC, settore stantio | **Sì**: `SourceKey` è il callsign, `area:<id>`, lo slug | per `(Kind, SourceKey)` |
| `ReleaseDrift`, `ReleaseDriftNextCycle` | **No**: il giro confronta il risultato (copia pubblicata contro bozza di oggi), non la causa. `SourceKey` è la chiave di release del documento | per **stessa frase**: stesso tipo e stesso riassunto delle sezioni cambiate |
| `BrokenTarget`, `ReleaseKeyMoved` | è il documento stesso | non si raggruppano: sono guasti di **un** documento |
| Incarichi scritti da una persona | nessuna | non si raggruppano |
| Incarichi nati da «prendi in carico» | quella della segnalazione d'origine | con la loro segnalazione |

Sui dati: il 4 settembre **6** documenti sono entrati in deriva col riassunto «Carte aeroportuali, … (+3)», il 15
settembre **4** con «Regole piste, LVP». È un cambiamento solo, spezzato in sei e in quattro righe. Raggruppare
per stessa frase li riunisce **senza sapere la causa** — e per questo il gruppo dice «stesse sezioni cambiate», non
«causa». La causa vera arriva col punto 3 del filone (deriva ricalcolata dopo il salvataggio, con migrazione).

⚠️ Limite noto: il riassunto si tronca a tre sezioni con «(+N)». Due documenti con le stesse prime tre sezioni e
code diverse finiscono nello stesso gruppo. Si accetta: il gruppo è un modo di leggere, non un fatto che si salva,
e l'errore è raggruppare **troppo** (si vede aprendolo), mai perdere una riga.

## §1 — Pre-flight

1. **Modello.** Nessun concetto nuovo salvato. `WorkItem` (read-model) porta in più il **tipo** e la **sorgente**
   della segnalazione; il gruppo è un secondo read-model **puro**, calcolato in memoria da una lista già letta.
   Zero migrazioni, zero query in più.
2. **Dispatch.** La regola «come si raggruppa» consulta i fatti di dominio che esistono già (`IsRotto`,
   `IsDaRipubblicare`, `IsDaPreparare`) come fa `WorkMapping`: non li ridichiara.
3. **Ingressi e verifica.** Due pagine rendono la lista: «Da fare» (`/services/vsop/tasks`) e «Da sistemare»
   (`/admin/pending`). Tutt'e due passano per **un** componente nuovo, `WorkItemList`, che usa `WorkItemRow` per
   le righe. Il banner in cima all'editor (`DocReviewBar`) resta com'è: è già «per documento». Verifica: test puri
   del raggruppamento, test bUnit del componente, prova a schermo.
4. **Propagazione.** Non rimuove né rinomina niente.

## §2 — Le decisioni

- **D1. Tre viste**, scelte da un selettore in testa all'elenco: **per cambiamento** (predefinita), **per
  documento**, **elenco** (quella di oggi). La scelta si ricorda nel browser di chi guarda.
- **D2. Un gruppo di una riga sola non è un gruppo**: si mostra la riga com'è. Un gruppo ha una testata con la
  frase (per cambiamento) o il titolo del documento (per documento), il numero di righe e la pastiglia della più
  urgente. Nasce **chiuso**: si apre col clic e mostra i documenti.
- **D3. Dentro un gruppo non si ripete ciò che la testata dice già**: per cambiamento la riga mostra il
  documento e non la frase; per documento il contrario.
- **D4. ✓ di gruppo** solo se **tutte** le righe del gruppo si spuntano (eventi non calcolati), con conferma in
  linea: «segna rilette tutte (N)». Sulle derive non c'è: ogni documento si ripubblica da sé, e il ✓ sarebbe la
  promessa che il giro notturno smentisce (carta del 26 agosto, §2/D3).
- **D5. Ordine**: i gruppi per urgenza della riga più urgente, poi dal più vecchio — la stessa regola delle righe.

## §3 — Eseguito (23 settembre 2026)

- `WorkItem` porta `Tipo` e `Sorgente` (per un incarico: quelli della segnalazione d'origine).
- `WorkGrouping.Raggruppa(righe, vista)` in `Vipi.Application/Content/WorkGrouping.cs`, puro; `WorkGroup` sa il
  suo capo, se è un gruppo e se si spunta tutto.
- `WorkItemList` (`Vipi.Ui/Components`) rende selettore e gruppi; la vista sta in `localStorage`
  (`vipi.dafare.vista`). `WorkItemRow` guadagna `ShowDoc`/`ShowWhy`; pastiglia e frase passano a
  `WorkItemText`, condivise con la testata.
- «Da fare» e «Da sistemare» usano `WorkItemList`; il ✓ di gruppo chiude una riga alla volta dalla porta di
  sempre (`IDocumentImpactService.ClearAsync`).
- Test: 13 in `WorkGroupingTests`, 8 in `WorkItemListTests`.

**Prova a schermo** (copia del database locale del 15 settembre, porta 5199): 64 righe diventano **11 gruppi e 10
righe sciolte**. Le 48 righe delle aree diventano 8 gruppi e una riga; le carte aeroportuali diventano 2 gruppi —
i riassunti «(+3)» e «(+5)» sono frasi diverse, come previsto al §0. Aperto un gruppo, 10 documenti senza la
frase ripetuta. La vista «per documento» resta dopo un ricarico. Il ✓ di un gruppo di 4 porta il totale da 64 a
60. Nessun errore in console; tema scuro guardato.
