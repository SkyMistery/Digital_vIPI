# «Da fare» per cambiamento — carta (23 settembre 2026)

> **Stato: ✅ eseguita tutta il 23 settembre 2026** — raggruppamento (§3), causa e deriva dopo il salvataggio (§4),
> età, dettaglio e casella di pubblicazione (§5) — provata a schermo, sul ramo `dafare/raggruppa` (filone
> [lista-da-fare](../filoni/lista-da-fare.md)). Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Estende
> [«Da fare»: una lista sola](2026-08-26-da-fare-una-lista-sola.md) e [Documenti da rivedere](2026-08-25-documenti-da-rivedere.md).
> Nessuna entità nuova. Aggiunge un interceptor e un giro in background (§4) e due colonne a `DocumentImpacts`.

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

## §4 — La causa vera, e la deriva poco dopo il salvataggio (punto 3 del filone)

**Misurato prima** (copia locale del 15 settembre): il giro intero `ImpactDriftUseCase.RunAsync` dura **~2 s** su 19
documenti. Si può rilanciare dopo un salvataggio senza pesare.

**Scoperto misurando, e cambia il piano.** L'idea era prendere la causa dal registro di audit. Ma il registro
non vede le modifiche di contenuto: dal 20 agosto sulla copia ci sono 55 voci in tutto, quasi tutte
pubblicazioni, permessi e cancellazioni. Nemmeno una sezione, un blocco o un trasferimento. Come causa non serve.

**Allora la causa la prende chi vede OGNI scrittura**: un interceptor dei salvataggi, sul modello di
`BumpCatalogoStazioniInterceptor` e montato come lui su tutti e tre i provider. Guarda quali entità cambiano, le
riduce a **famiglie** (coordinamenti, settori, aeroporti, testo, aree, spazi aerei, radioassistenze, allegati) e
segnala a un raccoglitore di processo. Le entità che il giro scrive da sé (segnalazioni, incarichi, release,
registro, lock, sessioni ATC, statistiche, traduzioni) sono fuori, apposta: tenerle dentro farebbe un anello, o un
giro ogni pochi secondi.

- **Finestra**: dalla prima modifica si aspettano **2 minuti**, poi si rilancia il giro intero con la finestra come
  causa. Le modifiche che arrivano mentre si aspetta entrano nella stessa finestra. Non si aspetta la calma: con
  modifiche continue il giro non partirebbe mai.
- **Chi riceve la causa**: una riga di deriva **nuova**, oppure una già aperta il cui riassunto è **cambiato** in
  questo giro. Una riga aperta e uguale di prima non l'ha toccata la finestra, e tiene la causa che aveva.
- **Dove si salva**: due colonne nullable su `DocumentImpacts`, `CauseKey` e `CauseArgsJson`, cioè chiave +
  argomenti come la frase. Migrazione ×2.
- **Chi non c'è**: l'utente. Dentro un circuito Blazor l'identità dell'interceptor non è affidabile (in sviluppo
  darebbe l'utente fittizio anche agli import). Meglio nessun nome che uno sbagliato.
- **Lettura**: per cambiamento, le derive con la stessa causa stanno in un gruppo «Modifiche del 23/09 21:04Z ·
  coordinamenti, settori». Dentro, ogni riga **mantiene** la sua frase, perché le sezioni cambiate sono diverse
  per documento. Senza causa (giro notturno, righe di prima) vale la regola del §0.

⚠️ **Limite, trovato dal vivo.** «Il racconto è cambiato» vuol dire che il riassunto delle sezioni è cambiato, e il
riassunto conta sezioni e blocchi, **non il testo**. Se si corregge una frase in una sezione già indietro, la riga
resta com'era e **non** prende la causa della finestra: continua a mostrare la causa che aveva, oppure nessuna. Il
giro non sbaglia la riga (c'è ed è giusta), sbaglia solo il gruppo in cui cade. Distinguerlo vorrebbe dire
confrontare il contenuto della copia pubblicata a ogni giro: non vale il costo finché la lista è quella di oggi.

### §4 eseguito (23 settembre 2026)

- `DocumentImpact.CauseKey`/`CauseArgsJson` + migrazione `CausaDelleSegnalazioni` ×2 (SQLite, MariaDB): solo due
  colonne nullable aggiunte.
- `IModificheInAttesa`/`ModificheInAttesa` (singleton) e `FamiglieDiModifica` in `Vipi.Application/Content`;
  `SegnalaModificheInterceptor` su tutti e tre i provider; `DerivaDopoLeModificheHostedService` (finestra 2 min) che
  chiama `IImpactDriftUseCase.RunAfterChangesAsync`.
- `RaiseAsync` scrive la causa su una riga nuova, o su una aperta il cui racconto è cambiato; un giro senza causa
  non la cancella.
- `WorkItem.Causa`/`CausaArgs`; `WorkGrouping` raggruppa le derive per causa (`WorkGroup.PerCausa`); la testata dice
  «Dopo le modifiche del 23/09 21:04Z (coordinamenti, settori): la copia pubblicata è indietro» e ogni riga tiene
  la sua frase.
- ⚠️ `ModificheInAttesa.PrendiAsync` non aspetta mai più della finestra: un istante di segnalazione nel futuro
  faceva aspettare ore. L'hanno trovato i test, un'ora e undici minuti con un istante fisso scritto come UTC.

**Prova dal vivo** (copia del 15 settembre, porta 5199): una modifica di testo nell'editor LIBB → nel log, circa due
minuti dopo, «Deriva dopo le modifiche (Testo, dalle 21:15:42Z): 19 documenti». La riga di Brindisi, già indietro
sulle stesse sezioni, **non** prende la causa: è il limite qui sopra. Chiusa a mano la riga e ripetuta la
modifica, il giro la riapre con `mod:20260923211831` e argomenti `["2026-09-23T21:18:31.7002145Z","Testo"]`.

## §5 — Le tre aggiunte scelte dal committente (punto 4 del filone)

Scelte il 23 settembre 2026: ✅ età della riga · ✅ dettaglio del cambiamento nella riga · ✅ casella nel
pannello di pubblicazione. ❌ **Contatore sull'avatar: scartato** (non riproporlo).

- **Età.** Ogni riga dice da quanto aspetta, in giorni interi («oggi», «da 3 g»; l'ora UTC esatta nel
  suggerimento). Oltre un ciclo AIRAC (28 giorni) lo dice più forte, in ambra **e** in grassetto: il colore da solo
  non basta a chi non distingue l'ambra. La testata di un gruppo porta l'età della sua riga più vecchia.
  `WorkItemText.Eta`/`EVecchia`.
- **«Cosa è cambiato».** Sulle righe «da ripubblicare / da preparare» un tasto apre, sotto la riga, la stessa
  `ReleaseDiffTable` del pannello delle release: le sezioni della bozza che la copia pubblicata non ha. Per «da
  preparare» il confronto è col ciclo entrante, perché a oggi la copia è allineata. `WorkItem` porta
  `Bersaglio`/`ChiaveRelease`, che il documento gestito aveva già: nessuna query in più finché non si apre.
  Il servizio delle release si **risolve** e non si inietta, come nel pannello: la riga la montano tre pagine e i
  loro test.
- **«Segna rilette anche queste N».** Nel pannello di pubblicazione, sopra i due tasti: le segnalazioni
  **da rileggere** aperte su quel documento (quelle col ✓; la deriva la chiude la pubblicazione da sé), con
  l'elenco sotto. **Nasce spenta**, e torna spenta dopo ogni pubblicazione: pubblicare non vuol dire aver
  riletto. Accesa, dopo la pubblicazione chiude una riga alla volta dalla porta di sempre
  (`IDocumentImpactService.ClearAsync`). Vale per «pubblica ora» e per «programma al ciclo».

**Prova a schermo** (copia del 15 settembre): età su tutte le righe, la deriva del 25 agosto evidenziata («28 d»),
«What changed» su Pescara apre le sue 8 sezioni, e nell'editor LIBB la casella dice «Also mark the 2 open notices
on this document as reviewed» con le due aree sotto, spenta. Nessun errore in console.

Test: `WorkItemRowTests` +5, `ReleasePanelTests` +4.
