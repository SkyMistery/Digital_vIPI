# Eliminare, con le protezioni — carta (26 agosto 2026)

> **Stato: ✅ ESEGUITA il 26 agosto 2026** (lavori A e B), sul ramo `statistiche-atc`. Build Release pulita
> sui due TFM, test verdi (net8 **2461**, net10 **2223**) e **provata sui dati veri** — §11.
> Quel che l'esecuzione ha cambiato rispetto al piano sta in **§12**. Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Nasce dall'inventario del 26 agosto su *come si elimina oggi un settore o un aeroporto* — la risposta è
> «quasi mai, e per vie traverse» — e dalle decisioni del committente della stessa sessione (§2).
> Il lavoro è **diviso in due**: [A] la verità sulle sorgenti, [B] l'eliminazione vera. B senza A mentirebbe.

## La domanda

«**Voglio un tasto elimina per qualsiasi cosa, con delle politiche di protezione.**»

Non è un tasto: è la differenza fra un archivio che si può correggere e uno in cui gli errori restano per
sempre. Oggi il secondo.

## §0 — Come si elimina oggi (rilevato, 26 agosto)

**Settore.** Nessun tasto. `DeleteSectorAsync` esiste in tre strati
(`StructureEditingService.cs:297` → `EfStructureEditingRepository.cs:361`) e **non ha nessun chiamante
nell'UI**: solo due test. L'unica via è indiretta e passa da tre pagine diverse:

1. nascondi o togli la riga di catalogo (`AccAdminPage`, o `AeroportoEditorPage.razor:743`);
2. il sync **disattiva** il settore proiettato — `IsActive = false`, non lo cancella
   (`EfSectorProjectionService.cs:217`);
3. il settore compare fra gli **orfani** nella casella degli impatti (`StrutturaPage.razor:336`) e da lì si
   **rimuove** (`EfOrphanSectorRepository.RemoveAsync`, che toglie anche la riga di catalogo).

**Aeroporto.** Il cestino c'è (`AeroportiPage.razor:236`, più l'azione di gruppo `:604`), ma
`DeleteAirportAsync` rifiuta se **un solo settore vi punta** (`EfStructureEditingRepository.cs:137`). Uno
scalo operativo non si cancella, e i suoi settori non hanno tasto: vicolo chiuso, si torna al giro degli
orfani, uno per uno.

**Il documento non segue.** `Airport.Document` è `SetNull`: cancellare lo scalo lascia in piedi il
`Document` e le sue `DocRelease` (che non hanno FK). Vanno tolti a parte, e solo
`EfDocumentAdminRepository.DeleteAsync:103` lo fa bene — release comprese, audit scritto **prima**.

**Il morto risorge.** Un settore proiettato torna al primo sync; un aeroporto torna al giro notturno
dell'anagrafica (`AirportDirectoryImportHostedService` → `AutoAssignAirportsAsync`, l'unico giro che
**crea**). Oggi «elimina» non promette niente di ciò che sembra promettere.

## §1 — Cosa c'è già, e in che stato

| Pezzo | Dove | Stato |
|---|---|---|
| Cancellazione settore, con due guardie | `EfStructureEditingRepository.cs:361` | c'è, **nessun chiamante UI** |
| Cancellazione aeroporto, guardia «settori vi puntano» | `EfStructureEditingRepository.cs:132` | c'è, quasi sempre blocca |
| Cancellazione ACC, guardia «prima i settori» | `EfStructureEditingRepository.cs:32` | c'è |
| Cancellazione documento + release + audit | `EfDocumentAdminRepository.cs:103` | ✅ riusabile così com'è |
| Chi trattiene un settore, **in frasi** | `EfOrphanSectorRepository.BloccantiAsync` | ✅ il cuore dell'anteprima |
| Rimozione orfano + riga di catalogo | `EfOrphanSectorRepository.RemoveAsync` | da far confluire nel motore nuovo |
| Riaggancio del documento a un altro settore | `EfOrphanSectorRepository.ReattachAsync` | ✅ è la terza via, già scritta |
| Casella degli impatti + sezione Orfani | `StrutturaPage.razor`, `ImpactDriftUseCase` | da promuovere a pagina |
| Marcatura «da rivedere» per settore | `IDocumentImpactService.RaiseForSectorAsync` | ✅ riusabile così com'è |
| Ultimo giro riuscito per categoria | `ImportState` (`ImportPolicy.cs:39`) | ⚠️ tiene **solo** l'ultimo |
| Timbro d'import sulla riga di catalogo | `AccSector.ImportedAtUtc`, `AirportSector` | ✅ c'è |
| Timbro d'import sull'aeroporto | — | ⚠️ **non esiste** |

## §2 — Le decisioni (committente, 26 agosto)

**D1 — Gerarchia.** Eliminando un settore, i figli passano al **nonno**:
`figlio.ParentSectorId = vittima.ParentSectorId`. Se la vittima è radice (`null`), i figli **diventano
radici**. Un `UPDATE` prima del `DELETE`: la FK `Restrict` su `ParentSectorId` non se ne accorge.

**D2 — Documento.** Se il settore **non** è l'ultimo che punta a quel documento: lo si sgancia
(`DocumentId = null`) e il documento si marca **da rivedere**, perché va aggiornato e **ripubblicato**. Se
è l'**ultimo**: si blocca, e si dice all'utente di eliminare prima quel documento.

**D3 — Parti di vLOA** (`DocumentParties.SectorId`): stessa regola di D2.

**D4 — Blocchi di contenuto** (`ContentBlocks` scope/da/a): stessa regola di D2.
⚠️ **Da confermare** — vedi §9: un blocco *da→a* rimasto con un lato solo non è incompleto, è **falso**, e
perdere un intero vIPI perché un blocco cita il settore è sproporzionato. Proposta: **tre** vie —
sgancia / elimina il blocco / blocca.

**D5 — Accordi di coordinamento** (`CoordinationAgreements.SideA/SideBSectorId`): si **blocca sempre**,
dicendo **quali** accordi l'utente deve eliminare prima, con il collegamento a dove stanno.

**D6 — La torre.** `TWR`/`I_TWR` si elimina **solo** insieme all'intero aeroporto, e in quel caso in
automatico. Da sola: mai. (È più stretta della guardia di oggi, che la lascia togliere se ce n'è un'altra:
vedi §6, caso «due torri».)

**D7 — Gli altri settori d'aeroporto.** `DEL`/`GND`/`APP` si eliminano **per conto loro**, con tutte le
protezioni da D1 a D5. Eliminando lo **scalo**, si eliminano **tutti** i settori collegati.

**D8 — Ciò che viene dalla sorgente.** Si può eliminare solo quel che **non è risultato presente nelle
ultime due chiamate** — automatiche o a mano, è uguale. Le righe **manuali** (`IsManual`, es. gli APP
esteri catalogati in Confinanti) sono fuori dalla regola: la sorgente non le ha mai mandate.

## §3 — [A] La verità sulle sorgenti

Serve prima, perché è ciò che **dà il permesso** di eliminare. Ha valore anche da solo: oggi nessuno vede
il quadro intero.

**A0 — `ImportState.PrevSuccessUtc`.** Una colonna. `MarkSuccessAsync` **ruota**: `Prev = Last`,
`Last = adesso`. Da lì la regola D8 è gratis per ogni riga che porta un timbro:

> assente nelle ultime due chiamate ⇔ `ImportedAtUtc < PrevSuccessUtc`

⚠️ **La trappola dei due clic.** Due giri a mano a cinque minuti l'uno dall'altro consumerebbero entrambe
le chiamate e autorizzerebbero l'eliminazione subito: la rotazione **non** avviene se il successo
precedente è più recente di una soglia (proposta: 1 ora, la stessa del retry).

**A1 — I giri a mano timbrano.** Oggi solo `GatedImportLoop.cs:54` e `EfSectorCatalogMaintenance` chiamano
`MarkSuccessAsync`: i bottoni («assegna aeroporti noti», re-import piste/SID/aree) fanno il lavoro e non
lasciano traccia. Vanno sullo stesso timbro, o «due chiamate a mano» non conta mai.

**A2 — Il timbro sull'aeroporto.** `Airport.LastSeenAtUtc`, scritto dal giro dell'anagrafica per **tutti**
gli ICAO che la sorgente elenca — non solo per quelli creati. Senza, «IVAO non lo elenca più» non è
un'affermazione che possiamo fare, e nessun aeroporto sarà mai eliminabile secondo D8.
⚠️ È la stessa trappola già pagata coi campi militari: `AutoAssignAirportsAsync` è **additiva**, quindi la
colonna nuova nasce vuota su tutti e 93 e nessun giro passa a riempirla — va scritta nello stesso punto in
cui `SyncAirportSourceFieldsAsync` riallinea gli altri campi.

**A3 — La regola in un posto solo.** `SogliaEliminazione.Consentita(importedAtUtc, prevSuccessUtc, isManual)`
in `Vipi.Domain/Services`, accanto a `SogliaTimbro`. Due letture dello stesso metro sono il modo in cui due
racconti divergono.

**A4 — La pagina «Da rivedere» (`/services/vsop/admin/pending`).** La casella degli impatti fa già tre
quarti del lavoro, ma sta in un angolo di un'altra pagina e **per un ACC alla volta**. La pagina la
promuove e la completa:

- gli **orfani** (settore sparito / nascosto / stantìo) di **tutti** gli ACC;
- i documenti la cui copia pubblicata è **indietro** (`ReleaseDrift`), o la cui chiave di release si è
  spostata, o il cui bersaglio non risolve;
- le aree regolamentate sparite o cambiate;
- 🆕 gli **aeroporti che IVAO non elenca più** (da A2);
- 🆕 per ogni riga: **eliminabile sì/no** secondo D8, con il perché («vista l'ultima volta il …»).

La casella in Struttura resta, ma diventa il riassunto che rimanda qui.

## §4 — [B] Eliminare, con le protezioni

**B0 — `IDeletionService`, l'anteprima.** Una porta sola per quattro bersagli:

```csharp
public enum DeletionTargetKind { Sector, Airport, Acc, Document }
public sealed record DeletionTarget(DeletionTargetKind Kind, int Id);

/// <summary>Chi trattiene, in una frase, col posto dove si risolve.</summary>
public sealed record DeletionBlocker(string Testo, string? Href);

public sealed record DeletionPlan(
    IReadOnlyList<string> Muore,          // cosa sparisce, per nome
    IReadOnlyList<string> SiSposta,       // i figli e il loro nuovo padre (D1)
    IReadOnlyList<string> DaRivedere,     // i documenti che restano da ripubblicare (D2-D4)
    IReadOnlyList<DeletionBlocker> Blocca,
    bool Eliminabile);

Task<DeletionPlan> AnteprimaAsync(DeletionTarget bersaglio, CancellationToken ct = default);
Task EliminaAsync(DeletionTarget bersaglio, CancellationToken ct = default);
```

`EliminaAsync` **ricalcola** l'anteprima e rifiuta se non è eliminabile: fra lo schermo e il clic passa del
tempo, e un altro admin può aver cambiato le carte. Il cuore delle regole è deterministico e senza IO →
test-first (§7).

**B1 — Settore, l'esecuzione.** Ordine, in **una transazione**: reparenting dei figli (D1) → sgancio dei
documenti non-ultimi + `RaiseForSectorAsync(SectorDetached, …)` (D2-D4) → rimozione della riga di catalogo
se la sorgente la espone ancora → `DELETE` → audit scritto **prima** del `DELETE` (il nome accanto all'Id è
tutto ciò che, fra sei mesi, distingue una pulizia da un incidente).

**B2 — Aeroporto.** L'anteprima elenca **tutti** i settori collegati (D7) e ciascuno passa per le proprie
protezioni: se uno di essi porta un documento non condiviso, il blocco è dell'intera operazione, con il
nome del documento da eliminare prima. Piste, SID, livelli di transizione e frequenze cadono già in
`Cascade`. Il documento **dell'aeroporto** è un bersaglio a parte e va spuntato nella stessa finestra.

**B3 — ACC.** `DeleteAccAsync` oggi pretende zero settori: entra nel motore e ottiene la cascata.

**B4 — Documento.** Bersaglio a pieno titolo, ma l'esecuzione resta `EfDocumentAdminRepository.DeleteAsync`
— è l'unico posto che toglie anche le `DocRelease`, che **non hanno FK** e non cascadano.

**B5 — La finestra.** Un componente solo, `DeleteDialog`, che mostra l'anteprima com'è: *cosa muore, cosa
si sposta, cosa resta da ripubblicare, chi blocca* (con i collegamenti). Sostituisce l'`InlineConfirm`
cieco di `AeroportiPage`, che oggi chiede conferma senza sapere se l'operazione è possibile. Tasti in
Struttura, Aeroporti, Documenti, ACC.

**B6 — Propagazione.** La `RemoveAsync` degli orfani diventa **un caso** del motore nuovo, non un secondo
motore: la regola §1 del FEATURE-PROCESS («estendi o sostituisci, non affiancare») è esattamente questa. Da
aggiornare nello stesso giro: la carta `2026-08-25-documenti-da-rivedere.md`, la guida in-app, e la memoria
`documenti-da-rivedere-impatti`.

## §5 — Trappole note

- **Transazione vera.** `BeginTransactionAsync` su tutto il giro: un'eliminazione a metà è peggio di
  nessuna eliminazione.
- **Niente `ExecuteDelete` nei repo**: desincronizza il change-tracker (già costato test rossi).
- **`Document.CurrentVersionId`** è un ciclo `NoAction`: va azzerato prima del cascade (lo si fa già).
- **`Airport.DocumentId` ha un indice unico**: attenzione a lasciarlo pendente.
- **Blazor**: le pagine admin che caricano DB nel render vanno su `OwningComponentBase`, o si ricade nella
  corsa sul `DbContext`.
- **Il ritorno del morto**: anche con D8 l'eliminazione non è per sempre — se la sorgente rimanda quel
  callsign, l'import lo ricrea. La finestra **lo dice**, perché non sembri un guasto.

## §6 — Casi limite da sciogliere in esecuzione

- **Due torri.** Uno scalo con `TWR` **e** `I_TWR`: D6 letta alla lettera non ne fa togliere nessuna delle
  due, nemmeno quella in più. Si esegue così (stretta), e si allenta solo se il caso si presenta davvero.
- **APP non remotizzato**: è un settore `Kind=Airport` con l'ICAO dello scalo, e porta il documento APP.
  Eliminando lo scalo (D7) muore con lui → il suo documento entra nell'elenco della finestra.
- **Aeroporto senza torre**: dopo D7 non esistono scali mezzi vuoti, ma l'anteprima deve reggere il caso
  storico di chi ne ha già uno.

## §7 — Test

Sul cuore deterministico, prima del codice: reparenting (radice e non), ultimo-vs-non-ultimo per documento
/ parte / blocco, accordo che blocca sempre, torre che blocca da sola e cade con lo scalo, D8 con timbro
prima/dopo `PrevSuccessUtc` e riga manuale esente. Su EF: la cascata dello scalo, e che l'audit esista con
il **titolo** dentro. Baseline attuale del ramo: net8 **2366**, net10 **2128**.

## §8 — Verifica live

Guidare il flusso vero su copia del `vipi.db`: eliminare un `GND` con documento condiviso (deve marcare
l'altro da rivedere), un `APP` ultimo di un documento (deve bloccare), uno scalo intero con quattro settori
e il suo documento, e un settore ancora elencato dalla sorgente (deve rifiutare per D8).

## §9 — Le decisioni chiuse in corsa

1. **D4, i blocchi**: confermate le **tre vie** (26 agosto). Un blocco che cita il settore come *estremo* di
   un da→a muore; uno che lo cita come *ambito* resta, sganciato; se il documento perde l'ultimo aggancio si
   blocca.
2. **D6, la torre**: si elimina **solo** insieme all'intero aeroporto. Letta alla lettera, su uno scalo con
   `TWR` **e** `I_TWR` non se ne toglie nessuna delle due — si esegue così, e si allenta solo se il caso si
   presenta davvero.
3. **D7, i settori d'aeroporto**: DEL/GND/APP si eliminano da soli; con lo scalo muoiono tutti.

## §10 — Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessuna entità nuova: due colonne (`ImportState.PrevSuccessUtc`, `Airport.LastSeenAtUtc`)
   e una `ImpactKind` (`SectorDetached`). **Un solo** motore di cancellazione: la rimozione orfani ci
   confluisce invece di restarci accanto.
2. **Dispatch** — quattro bersagli: un descrittore per bersaglio dietro `IDeletionService`, non un
   `switch (tipo)` in ogni chiamante. È la regola del 2, già applicata a `IReleaseTarget`/`IDocKindRoutes`.
3. **Ingressi + verifica** — i tasti stanno dove le entità già si vedono; la pagina «Da rivedere» è
   l'ingresso al *cosa posso eliminare*. Verifica: §8.
4. **Propagazione** — questa slice **rimuove** un percorso (orfani → rimuovi): carta degli impatti, guida
   in-app e memorie vanno aggiornate nello stesso giro.


## §11 — La prova sui dati veri (26 agosto 2026)

Non i test: l'applicazione avviata su una **copia del `vipi.db` di sviluppo** (321 settori, 93 aeroporti,
19 documenti) e guidata in Edge con puppeteer-core. Prima le migrazioni sono passate sulla copia senza
perdere niente; poi si è seminato lo scenario che la regola D8 richiede — due giri riusciti alle spalle,
`LIMM_FSS` con il timbro di catalogo fermo a cinque giorni prima, `LIQV` (Volterra) non elencato da sei.

| Prova | Esito |
|---|---|
| Pagina «Da sistemare» | 17 voci: **1** aeroporto non più elencato, **10** orfani, **6** documenti da rivedere; in cima i tre istanti che fanno da metro |
| `LIMM_FSS` — piano | «Sparisce: il settore LIMM_FSS (Milano Information)», nessun blocco, conferma attiva |
| `LIMM_FSS` — eliminazione | eseguita: 321 → **320** settori, riga di catalogo `AccSectors` sparita con lui, audit `Delete/Sector/10` col **nome** dentro, nodo sparito dall'albero |
| `LIBB_ES_CTR` — piano | **bloccato**, e dice tutto: la sorgente lo manda ancora, **sedici** accordi di coordinamento elencati per nome, e «elimina prima il documento «vIPI Brindisi»: è il suo ultimo aggancio». Sotto, il prezzo: sette figli che diventerebbero radici e **quattro vLOA** da ripubblicare |
| `LIMM_ES5_CTR` — piano | bloccato dalla sola D8: «la sorgente la manda ancora (vista l'ultima volta il 2026-08-25 22:02Z)» |
| `LIQV` — eliminazione | eseguita dalla pagina d'insieme: 93 → **92** aeroporti, audit `Delete/Airport/73` con ICAO e nome |

⚠️ **Un difetto trovato solo a schermo**, e non dai test: con un piano lungo — i sedici accordi di un CTR
vero — la finestra riempiva i suoi 82vh e i tasti **Elimina/Annulla** finivano fuori dallo schermo, dentro
l'area che scorre. Chi legge si trovava un elenco di ostacoli e nessun modo di chiudere che non fosse il
velo, che non è un comando che si trovi. I tasti ora sono `sticky` in fondo alla finestra, sopra una riga di
separazione. Misurato dopo il fix: card 1148px in un viewport da 1400, azioni dentro lo schermo.

⚠️ **La pagina nasce in sola lettura**: il lock di modifica della Struttura si prende a mano, e finché non lo
si prende **tutti** i comandi sono spenti — «Applica» come «Elimina». Non è un difetto del tasto nuovo, ed è
costato un giro del driver per capirlo.

## §12 — Cosa l'esecuzione ha cambiato rispetto al piano

- **`SogliaEliminazione` sta in `Application/Content`**, accanto a `SogliaTimbro`, non in `Domain/Services`:
  è lì che vive la gemella, e separarle sarebbe stato l'inizio della divergenza.
- **La trappola dei due clic** non era nel piano: due giri a mano ravvicinati consumerebbero entrambe le
  conferme. Il penultimo timbro non scorre sotto un'ora di distanza.
- **Gli import per-ICAO non timbrano la categoria**: il re-import dei settori di *un* aeroporto o delle aree
  di *una* ACC conferma una riga, non il catalogo. Timbrano solo i giri interi (`AccImportUseCase`,
  `AirportImportUseCase`), che sono gli stessi corpi del bottone e del giro notturno.
- **Il documento non è mai una cascata**: si elimina prima, a mano. Vale anche per la vIPI dello scalo, che
  quindi blocca l'eliminazione dell'aeroporto finché c'è. Più prevedibile di un cascade, e reversibile fino
  all'ultimo passo.
- **La rimozione degli orfani è confluita nel motore nuovo** e `IOrphanSectorRepository.RemoveAsync` non
  esiste più. ⚠️ Effetto voluto: un orfano **nascosto** da un admin ora non si elimina finché la sorgente lo
  manda. Prima si poteva — ed era la stessa regola D8 applicata a metà.

## §13 — Limiti dichiarati

- **Le frasi del piano sono in italiano** anche quando l'interfaccia è in inglese: nascono nel dominio, come
  già facevano i «bloccanti» della casella degli impatti. La cornice (titolo, intestazioni, tasti) è
  localizzata. Renderle chiavi di traduzione è un giro a sé, e va fatto per tutte e due le famiglie insieme.
- **La ACC non cascada** (§2, D-ACC): la politica è «svuotala prima», con l'elenco di quanto manca.
- **Eliminare non è per sempre**: se la sorgente rimanda quel callsign, l'import lo ricrea. La regola delle
  due chiamate lo rende raro, non impossibile.
