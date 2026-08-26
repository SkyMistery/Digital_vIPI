# «Da fare»: una lista sola — carta (26 agosto 2026, notte)

> **Stato: ✅ ESEGUITA il 26 agosto 2026 sera**, ramo `statistiche-atc`. Build Release pulita sui due TFM
> (0 avvisi), test verdi — net8 **2596**, net10 **2358** — e **provata a schermo** (§7).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Unisce due meccanismi che esistono già e **non ne aggiunge un terzo**:
> [Documenti da rivedere](2026-08-25-documenti-da-rivedere.md) (la casella degli impatti) e gli **incarichi**
> editoriali. Decisioni del committente in §2.

## La domanda

> «Si può far sì che *Documents to review* sia una sorta di to-do list? Cioè tutto ciò che il sistema ritiene
> debba essere fatto, con link al documento su cui bisogna lavorare e con possibilità di flaggarli come
> eliminato. Poi lo stesso task deve anche apparire in cima all'editor nella pagina del documento stesso. Si
> può fare senza sminchiare tutto? E ovviamente legarlo al sistema di gestione dei task. Una cosa che, ad
> esempio, se cambio il trasferimento o ne aggiungo uno che sta nelle IPI di Roma e Brindisi mi mette i due
> task da svolgere sui due documenti (in questo caso di ripubblicarli).»

## §0 — Cosa c'è già (rilevato, 26 agosto notte)

**Il motore c'è, e l'esempio del committente funziona già.**

| Pezzo | Dove | Stato |
|---|---|---|
| Righe generate dal sistema su un documento | `DocumentImpact` + `IDocumentImpactService` | ✅ dedup, riconciliate, frasi localizzate |
| «la copia pubblicata è indietro» | `ImpactKind.ReleaseDrift`, aperto da `ImpactDriftUseCase` | ✅ **confronta il risultato, non la causa** |
| Righe create da persone, con assegnatario e scadenza | `EditorTask` + `IEditorTaskService` | ✅ |
| Da `(tipo, chiave)` all'**URL dell'editor** | `IEditorTaskLinksService` → `IDocRoutesRegistry` | ✅ già scritto, già sbagliato una volta |
| Banner in cima all'editor | `DocReviewBar`, su tutti e quattro gli editor | ✅ ma mostra **solo** gli impatti |
| Elenco «Documenti da rivedere» | `PendingPage.razor:224` | ⚠️ **sola lettura**: niente link, niente azioni |

⚠️ **L'esempio del committente è già coperto, e va detto perché.** «Cambio un trasferimento che sta nelle
vIPI di Roma e Brindisi» non ha bisogno di un trigger nuovo: `ImpactDriftUseCase` non guarda la causa, guarda
**il risultato** — confronta ciò che la copia pubblicata dice con ciò che il documento direbbe oggi, per ogni
documento pubblicato. Un accordo che alimenta due vIPI produce due `ReleaseDrift`, e ne produrrebbe due anche
se il cambio arrivasse da una strada che nessuno ha previsto (una regola pista riscritta, una TA aggiornata
dall'import). **È il motivo per cui quel giro è stato scritto così**, ed è la ragione per cui questo lavoro è
un lavoro di *interfaccia e di ponte*, non di motore.

Quel che manca è tutto sul davanti: la riga non è cliccabile, non porta dove si lavora, non si può chiudere
dall'elenco, e le due liste — quella del sistema e quella delle persone — non si parlano.

## §1 — Il rischio, detto prima

Ci sono **due** meccanismi che si somigliano. La regola §1 del FEATURE-PROCESS dice: *estendi o sostituisci,
mai affiancare*. Una terza tabella «Todo» sarebbe il modo più veloce di rendere questo prodotto illeggibile
fra sei mesi. Quindi:

- **nessuna entità nuova** per la lista;
- la lista è un **read-model** (`WorkItem`) sopra i due meccanismi che ci sono;
- l'unica riga di schema che si aggiunge è il **ponte** fra i due (§2/D5).

## §2 — Le decisioni (committente, 26 agosto notte)

**D1 — La lista vive in «Incarichi»**, `/services/vsop/tasks`, che diventa **«Da fare»**: i propri incarichi
e le righe che il sistema ha aperto sui documenti che si possono modificare, **mescolate** e ordinate per
urgenza. «Da sistemare» (`/admin/pending`) resta la pagina dell'**igiene d'archivio** — aeroporti spariti,
orfani, documenti senza chiave — e la sua sezione «Documenti da rivedere» mostra le stesse righe leggendo lo
**stesso read-model**. Una verità, due porte.

**D2 — Ognuno vede ciò che può modificare.** Un editor con concessione su LIRR vede le righe dei documenti di
LIRR; gli admin vedono tutto. È l'unica forma in cui la lista è una to-do list **vera** anche per chi non è
admin. ⚠️ Il filtro si fa sull'**ACC del documento** (`ManagedDoc.AccCode`), non con
`CanEditDocumentAsync` riga per riga: quello costa due query per riga (regola 136, «una query per pagina»).

**D3 — Le righe calcolate non si spuntano: si risolvono.** Su «la copia pubblicata è indietro» il tasto
giusto non è ✓ ma **«Ripubblica»**: eseguirlo rende il fatto falso e la riga si chiude da sé, onestamente. Un
✓ sarebbe un ping-pong col giro notturno, che la riaprirebbe stanotte. Il ✓ resta dov'era: sulle righe **non**
calcolate (settore sparito, area cambiata, blocco sganciato), dove una persona che rilegge è l'unica
chiusura possibile.

**D4 — Il banner in cima all'editor mostra tutt'e due le nature**: le segnalazioni del sistema *e* gli
incarichi aperti su quel documento. È la richiesta «lo stesso task deve anche apparire in cima all'editor», e
il componente c'è già — gli manca metà della sorgente.

**D5 — «Prendi in carico»: il ponte.** Da una riga di sistema si crea un incarico assegnato a qualcuno, con
la frase già dentro. L'incarico porta l'**Id dell'impatto** da cui nasce (`EditorTask.FromImpactId`,
nullable) e questo serve a due cose: la lista non mostra **due volte** lo stesso lavoro, e quando il fatto a
monte smette di essere vero l'incarico lo sa. È l'**unica** riga di schema di questo giro.

⚠️ Costo dichiarato: **una migrazione**, a doppia emissione (SQLite + MySQL), che porta la coda del cutover
MariaDB da tredici a **quattordici**. Costa poco *adesso* proprio perché il database sta per essere ripulito.

## §3 — Il modello: `WorkItem`

Un read-model, non un'entità. Una riga di lavoro, qualunque sia la sua natura.

```csharp
public sealed record WorkItem(
    WorkOrigin Origine,          // Sistema | Persona
    string Chiave,               // "imp:42" / "task:7" — stabile, per @key e per le azioni
    int? DocumentId,
    string Titolo,               // il titolo del DOCUMENTO (o dell'incarico, se libero)
    string? AccCode,
    string? Url,                 // dove si va a lavorare; null = non raggiungibile
    string FraseKey,             // chiave di localizzazione…
    IReadOnlyList<string> FraseArgs,   // …e argomenti: la frase NON si salva mai
    WorkSeverity Severita,
    WorkAction Azione,           // che cosa chiude questa riga
    DateTime Da,
    int? AssegnatarioId, string? AssegnatarioNome,
    string? ScadenzaCiclo, bool InRitardo);
```

**Perché la frase resta chiave+argomenti fino a schermo**: è la regola già pagata dagli impatti — una riga
scritta in italiano si ripresenterebbe in italiano a chi legge in inglese, e il circuito Blazor cambia lingua
senza ricaricare.

**`WorkAction`** — che cosa chiude la riga, deciso in **un posto solo** (`WorkMapping.AzioneCheChiude`).
⚠️ Sta in `Vipi.Application` e **non** accanto ai fratelli `IsCalcolato`/`IsRotto`/`IsDaRipubblicare`, che
vivono in `Vipi.Domain`: quelli sono fatti di dominio, questa è la loro traduzione in comportamento di lista,
e il dominio non conosce `WorkAction` né deve. Non li duplica — li **consulta**, così la verità resta una:

| riga | azione | perché |
|---|---|---|
| `ReleaseDrift` | **Ripubblica** | rende il fatto falso: la riga si chiude da sola |
| `ReleaseKeyMoved`, `BrokenTarget` | **Vai a sistemare** | non si risolve dall'elenco: è una decisione |
| `SectorStale` | **Vai alla Struttura** | il fantasma si toglie di là |
| gli altri impatti | **✓ fatto** | rileggere è l'unica chiusura |
| incarico | **cambia stato** | Todo → InProgress → Done, com'è oggi |

**Ordinamento** (una funzione pura, testabile senza database): già in pubblico → rotto → in ritardo → da
ripubblicare → da rileggere → il resto; a parità, la più vecchia in cima. Chi apre la lista deve trovare in
alto ciò che sta facendo danno **adesso**.

## §4 — I pezzi

| pezzo | dove | stato |
|---|---|---|
| `WorkItem`, `WorkOrigin`, `WorkSeverity`, `WorkAction` | `Vipi.Application/Content/WorkItem.cs` | nuovo |
| `IWorkListService` — `MieAsync`, `PerDocumentoAsync`, `PrendiInCaricoAsync` | `Vipi.Application/Content/WorkListService.cs` | nuovo |
| `WorkMapping.AzioneCheChiude` + `Severita` | `Vipi.Application/Content/WorkItem.cs` | nuovo, consulta i fatti di dominio |
| `ListAccCodesForUserAsync` — le ACC di uno, in una query | `IEditGrantRepository` + impl EF | esteso |
| `EditorTask.FromImpactId` | `Support.cs` + una migrazione ×2 | **l'unica riga di schema** |
| «Da fare» | `TasksPage.razor` | esteso |
| Il banner con tutt'e due le nature | `DocReviewBar.razor` | esteso |
| «Documenti da rivedere» legge il read-model | `PendingPage.razor` | esteso |
| `id="sec-versioni"` sui quattro editor | i tre che non ce l'hanno | perché «Ripubblica» atterri sul pannello |

## §5 — Che cosa NON si fa, e perché

- **Nessuna notifica, nessuna posta**: la lista si guarda, non insegue.
- **Nessun trigger nuovo** per l'esempio dei trasferimenti: c'è già, ed è migliore di un trigger (§0).
- **Nessuno stato «silenziato»** sulle righe calcolate: §2/D3 dice che si risolvono, non si mettono a tacere.
- **Gli incarichi liberi** (senza documento) restano nella lista come sono: hanno un titolo e nessun link, e
  va bene — sono promemoria.

## §6 — La prova

**Test** — 53 nuovi, tutti verdi (net8 **2596**, net10 **2358**):

- `WorkListTests` (19, `Vipi.Application.Tests`) — il cuore puro: che cosa chiude una riga, quanto urge, in
  che ordine compare. ⚠️ Uno gira su **tutti** gli `ImpactKind` e verifica che l'azione non contraddica mai
  il dominio: se un giorno qualcuno aggiunge un tipo calcolato e si scorda della mappatura, il ✓ non deve
  comparirgli sopra.
- `WorkListServiceTests` (18) — chi vede che cosa (admin vs. concessioni vs. incarico libero) e, soprattutto,
  che una segnalazione **presa in carico non compaia due volte**; che tornando `Done` l'incarico la riga di
  sistema **ritorni**, perché il fatto è ancora vero.
- `WorkItemRowTests` (16, `Vipi.Ui.Tests`) — il tasto promesso: niente ✓ sulla copia indietro, «Ripubblica»
  che punta a `#sec-versioni`, il titolo di un incarico stampato com'è scritto, l'urgenza leggibile **anche
  senza distinguere i colori** (la barretta di sinistra, non solo la pastiglia).
- `DocReviewBarTests` — adattati: provano le stesse cose, leggendo dal read-model.

⚠️ **Un rosso trovato per strada e non era di questo giro**: `CatalogoStantioTests` affermava «10 giorni di
silenzio» contro un timbro di fixture **fisso** mentre il codice conta su `DateTime.UtcNow`. Scritto il 25
agosto, il 26 è diventato rosso da solo. Corretto calcolando l'atteso con la stessa formula del codice.

## §7 — Verificato a schermo

Guidata in locale (Edge + puppeteer-core, copia del `vipi.db` reale). ⚠️ I `bin/` Debug erano bloccati da un
`Vipi.Host` del committente (avviato alle 13:56, **non** mio): si è pubblicato in una cartella a parte e
lanciato sulla **5035**, che è la deroga già scritta nella skill `verifica-live`.

Nessun errore di console, nessun 4xx, nessuna chiave `Work_*`/`Impact_*` non tradotta, nessuna espressione
Razor rimasta letterale.

**L'elenco** (`/services/vsop/tasks`, «To do · 7») — le righe escono nell'ordine deciso: prima le quattro
`republish`, poi le tre `needs review`. Ogni riga porta al **suo** editor, con la rotta giusta per tipo
(`?acc=` per la vLOA, `?app=` per l'APP, nuda per la vIPI ACC). La lavagna per stato è sotto, **chiusa**.

**L'esempio del committente, dal vivo.** Le prime due righe sono:

> `vLOA — LIBB ↔ LGGG` · *The published copy is behind the draft: **Coordination** / LGGG → LIBB, Coordination / LIBB → LGGG* · **[Republish →]**
> `vLOA — LIBB ↔ LYBA` · *The published copy is behind the draft: **Coordination** / LIBB → LYBA, Coordination / LYBA → LIBB* · **[Republish →]**

Cioè: un coordinamento cambiato ha messo **due documenti diversi** in lista, ciascuno col suo «ripubblica» e
col suo collegamento. È letteralmente la cosa chiesta — e nessuno ha dovuto agganciare un trigger.

**D3 dal vivo.** Sulle tre righe di `vIPI Roma` il tasto cambia da solo secondo la natura: `SectorReparented`
e `SectorHidden` mostrano **✓ Mark as reviewed**, mentre `SectorStale` — che è **calcolata** — mostra
**Go and fix →**. Nessun ✓ dove il giro notturno lo smentirebbe.

**Il banner** in cima all'editor della vLOA LGGG: una riga, col «Republish →», e **senza** ripetere l'ACC —
è quella della pagina che si sta guardando. L'ancora `#sec-versioni` risponde.

**«Da sistemare»** mostra le stesse sette righe attraverso lo stesso read-model, **senza** «Take it on»
(quella è la pagina dell'archivio, non quella di chi lavora), e il totale in testata conta ciò che la pagina
mostra davvero.

## §8 — Il picker, e il difetto che ha fatto emergere

Chiesto dal committente subito dopo: **assegnare ad altri anche dalla lista**, non solo a sé stessi.

Il picker sta **dentro la riga** (`WorkItemRow`) e non in una finestra: assegnare è un gesto di scorrimento —
si guarda l'elenco e si distribuisce — e una modale per ogni riga farebbe perdere il posto ogni volta. Si
apre su **una riga per volta**, propone **me** come primo assegnatario (il caso frequente; una tendina vuota
da riempire costerebbe un gesto a ogni riga), e offre i cicli AIRAC come scadenza facoltativa.

⚠️ **Il nome dell'assegnatario non lo manda la UI quando è chi preme**: il servizio ce l'ha già in casa, e
farselo passare vorrebbe dire fidarsi di un dato non verificato. Parte solo quando si sceglie un altro.

⚠️ **Il roster si popola ai login** (`IStaffRosterService`): finché uno staffista non è mai entrato non
compare fra gli assegnatari. In sviluppo c'è una voce sola, quindi **la scelta di un altro è coperta dai
test ma non dalla prova a schermo** — a schermo si è potuto assegnare solo a sé stessi.

### Il difetto che solo lo schermo ha mostrato

Prendere in carico **peggiorava** la riga, e nessun test lo vedeva:

> `task · vLOA — LIBB ↔ LGGG · **vLOA — LIBB ↔ LGGG** · assigned to Carmine`

Il titolo del documento si ripeteva al posto del **motivo**, e la riga scivolava in fondo alla lista perché
un incarico a priorità normale urge meno di una copia da ripubblicare. Assegnare un lavoro non lo rende né
meno urgente né meno comprensibile.

**La causa e la cura**: l'incarico non portava la frase dell'impatto. Ora, finché la segnalazione d'origine è
aperta, **frase e urgenza restano le sue** e l'incarico aggiunge solo *chi* e *entro quando* — che è
esattamente ciò che §2/D5 aveva promesso («la segnalazione resta la verità su se il fatto è ancora vero»).
Fra l'urgenza dell'una e quella dell'altro vince la maggiore: una scadenza scaduta batte la deriva.

Riverificato a schermo: la riga presa in carico resta `wi-daripubblicare`, dice *«The published copy is
behind the draft: Coordination / LGGG → LIBB»*, mostra «assigned to Carmine (704798)» e i tasti
**Start** / **✓ Done**.
