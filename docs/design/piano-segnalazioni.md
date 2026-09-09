# Piano — le segnalazioni dal campo 🟣

**Stato:** **carta, non eseguita — RIMANDATA** (vedi §10.0) · **Aggiornato:** 9 settembre 2026
**Metodo:** [FEATURE-PROCESS](../FEATURE-PROCESS.md) · **Perimetro:** [regole-perimetro-servizi](regole-perimetro-servizi.md) §P1
**Richiesta del committente (1 set 2026):** *«sì, sarebbe molto utile, così da non dover passare dalle mail»*

> **In una riga.** Oggi il flusso di lavoro va **solo dall'alto verso il basso**: il sistema apre le
> segnalazioni, lo staff assegna gli incarichi. Chi usa i documenti in frequenza — e vede l'errore — non ha
> nessuna porta se non la posta elettronica, dove la richiesta esce dal prodotto, perde il contesto e non
> torna mai indietro con una risposta.

---

## §0 — Cosa c'è già (rilevato nel sorgente, 1 settembre 2026)

| Pezzo | Dove | Che cos'è |
|---|---|---|
| Righe **del sistema** su un documento | `DocumentImpact` + `IDocumentImpactService` | fatti a monte, dedotti, che si chiudono da sé |
| Righe **delle persone**, con assegnatario e scadenza | `EditorTask` + `IEditorTaskService` | impegni interni allo staff |
| La lista unica sopra le due | `WorkItem` (read-model) + `IWorkListService` | «Da fare», `/services/vsop/tasks` |
| Il ponte fra le due | `EditorTask.FromImpactId` | «prendi in carico» |
| Banner in cima all'editor | `DocReviewBar.razor` | mostra entrambe le nature |
| Livelli di autorizzazione ordinati | `VipiRole` (User 0 … Admin 4) | il cancello è un `>=` |
| Identità di chi legge | login IVAO OIDC: VID e nome sono **già noti** a chi è connesso | — |

**Manca una cosa sola: un ingresso dall'esterno.** Non esiste nessuna riga di codice che permetta a una
persona che *non è staff* di dire qualcosa a chi scrive i documenti.

---

## §1 — Il rischio, detto prima

Ci sono già **due** meccanismi che si somigliano, e la regola §1 del FEATURE-PROCESS è netta: *estendi o
sostituisci, mai affiancare*. La domanda va posta sul serio: **una segnalazione è un `DocumentImpact`?**

**No, e i motivi sono tre, tutti strutturali:**

1. Un `DocumentImpact` è **dedotto e riconciliato**: nasce da un calcolo e si **richiude da solo** quando il
   fatto smette d'essere vero (`ClearedByUserId = 0` significa «l'ha richiuso il calcolo»). Una segnalazione
   umana non è deducibile da nessun dato: il riconciliatore la chiuderebbe la notte stessa.
2. Un impatto **non ha un autore** e non ha nessuno a cui rispondere. Una segnalazione ha entrambi, e il
   ritorno è metà del suo valore — è ciò che la posta elettronica faceva e che non vogliamo perdere.
3. Un impatto non può essere **respinto**: o è vero o non c'è. Una segnalazione può essere sbagliata, e
   dirlo a chi l'ha scritta fa parte del ciclo.

**Quindi:** un'entità nuova sì (`FieldReport`), **ma nessuna terza lista**. Le segnalazioni entrano nel
read-model `WorkItem` che esiste già, con una `WorkOrigin` in più. Chi apre «Da fare» continua a vedere
**una** lista.

---

## §2 — Le decisioni

**D1 — Si segnala solo da connessi.** L'anonimo non segnala. Tre ragioni: il VID e il nome ci sono già senza
chiedere niente (login IVAO), una risposta ha bisogno di un destinatario, e una casella anonima su un sito
pubblico è una casella di spam entro una settimana. ⚠️ Non serve essere staff: **serve essere in IVAO**, che
è precisamente il pubblico di questi documenti.

**D2 — Si segnala da dove si legge, e la riga si porta dietro il contesto.** Il bottone sta accanto al
titolo di **ogni sezione** del documento pubblico. La segnalazione registra:

| Campo | Perché |
|---|---|
| `DocumentId` | quale documento |
| `SectionKey` | ⚠️ **la chiave di catalogo, non `DocumentSection.Id`**: le sezioni sono figlie di una `DocumentVersion` e alla pubblicazione successiva quell'Id non esiste più. È la stessa ragione per cui `DocumentImpact` è ancorato al `DocumentId` e non al bersaglio di release. |
| `ReleaseNumber` | 🔴 **il numero di rilascio che il segnalatore stava leggendo.** Senza, il triage apre la bozza, vede un testo diverso e risponde «non c'è nessun errore» — mentre in pubblico l'errore c'è ancora. |

Più una segnalazione **libera**, senza documento, per ciò che non sta in una sezione.

**D3 — La vede lo staff che può fare qualcosa, e il suo autore.** Triage da `VipiRole.Editor` (3) in su —
è il livello che possiede il contenuto documentale. L'autore vede **le proprie** e il loro stato, e nient'altro.

**D4 — Il ciclo, e ogni chiusura ha una frase.**

```
Nuova ──► Presa in carico (nasce un incarico) ──► (l'incarico fa il suo corso)
      ├─► Risolta          + risposta
      ├─► Respinta         + motivo   ← obbligatorio
      └─► Doppione di #N   + rimando
```

⚠️ **La risposta non è un optional del rifiuto: è il rifiuto.** Una segnalazione chiusa in silenzio insegna
a non segnalare più, ed è esattamente il modo in cui questa funzione muore.

**D5 — Il ponte: `EditorTask.FromReportId`,** gemello dichiarato di `FromImpactId`. Serve alle stesse due
cose: la lista non mostra due volte lo stesso lavoro, e l'incarico sa da dove viene.
⚠️ **Perché due colonne invece di generalizzare** in `SourceKind` + `SourceId`, che sarebbe più pulito: una
generalizzazione **rinomina** una colonna esistente, e fino al **16 settembre 2026** siamo nella finestra
cieca — `Rename*` e `AlterColumn` sono vietati dal presidio `MigrazioniDellaFinestraCiecaTests`, e una
migrazione sbagliata in quella finestra è il sito giù senza ripristino possibile. **Alla terza provenienza si
generalizza**, ed è la regola del 2 del processo: due casi restano due colonne, tre casi diventano un registro.

**D6 — Niente posta, niente notifiche.** Vale la §5 della carta «Da fare»: *la lista si guarda, non insegue*.
⚠️ Non è una scorciatoia: la richiesta del committente è **togliere** la posta dal giro, e un prodotto che
manda mail per dire che c'è una cosa da leggere l'ha rimessa dentro.

**D7 — Il corpo di una segnalazione non si traduce e non passa dai `.resx`.** È prosa di una persona: si
salva com'è scritta e si mostra com'è scritta. ⚠️ È l'**opposto** della regola di `DocumentImpact` e
`ConsistencyFinding` (chiave + argomenti), e la differenza è che lì la frase la scrive il **prodotto** — qui
la scrive un **essere umano**. Tradotte sono solo le etichette. E il motore di traduzione automatica non la
vede mai: quello lavora sulla prosa dei documenti (R7 di [regole-lingua](regole-lingua.md)).

---

## §3 — Il modello

Entità nuova in `Vipi.Domain/Entities/Support.cs`, accanto a `EditorTask`.

```csharp
public enum FieldReportKind   { Errore, Suggerimento }
public enum FieldReportStatus { Nuova, PresaInCarico, Risolta, Respinta, Doppione }

public class FieldReport
{
    public int Id { get; set; }

    // Chi
    public int ReporterUserId { get; set; }            // VID
    public string ReporterName { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    // Su che cosa (tutto opzionale: esiste la segnalazione libera)
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }
    public string SectionKey { get; set; } = "";       // ⚠️ chiave di catalogo, non SectionId
    public int? ReleaseNumber { get; set; }            // il rilascio che stava leggendo

    // Che cosa dice
    public FieldReportKind Kind { get; set; }
    public string Body { get; set; } = "";             // testo semplice, max 2000

    // Che fine ha fatto
    public FieldReportStatus Status { get; set; } = FieldReportStatus.Nuova;
    public int HandledByUserId { get; set; }
    public DateTime? HandledUtc { get; set; }
    public string Reply { get; set; } = "";            // la frase che l'autore legge
    public int? DuplicateOfId { get; set; }
}
```

⚠️ **Ogni stringa NOT NULL nasce con un default vero** (`""`): non è pignoleria, è una condizione che il
presidio della finestra cieca **verifica**, e senza la migrazione viene rifiutata dal test.

**Indici:** `(Status, CreatedUtc)` per la coda, `(DocumentId)` per il banner dell'editor, `(ReporterUserId)`
per «le mie». Nessun indice unico — non c'è niente da deduplicare, e un unico nuovo su tabella popolata è
un'altra cosa che il presidio vieta.

**Cancellazione:** FK verso `Document` **con cascata**, come `DocumentImpact`. ⚠️ Detto esplicitamente perché
è una perdita voluta: eliminando un documento si perdono anche le segnalazioni che lo riguardavano, e per la
politica di eliminazione (vedi la carta del 26 agosto) è la scelta coerente — una segnalazione su un
documento che non esiste più non è un lavoro, è un residuo.

### Come entra in «Da fare»

| Pezzo | Modifica |
|---|---|
| `WorkOrigin` | valore nuovo **in coda**: `Campo` — «l'ha scritta una persona da fuori: è una richiesta, e la apre un triage» |
| `WorkAction` | valore nuovo **in coda**: `ApriSegnalazione` — il tasto porta alla segnalazione, non la chiude dall'elenco |
| `WorkSeverity` | 🔴 **nessun valore nuovo.** La scala ha numeri **espliciti** ed è l'ordinamento della lista: infilarci un valore in mezzo riordina tutto ciò che c'è già. Una segnalazione di errore si mappa su `DaRileggere`, un suggerimento su `Normale`. |
| `WorkMapping` | la mappatura in **un posto solo**, come per gli impatti |

⚠️ **Una segnalazione presa in carico non compare due volte**: c'è già il test che lo pretende per gli
impatti (`WorkListServiceTests`), e va esteso — non riscritto — alla provenienza nuova.

---

## §4 — Gli argini

| Argine | Valore | Perché |
|---|---|---|
| Solo connessi | — | §2/D1 |
| Segnalazioni **aperte** per VID | **5** | chi ne ha cinque in attesa non ha bisogno della sesta: ha bisogno di una risposta |
| Segnalazioni al giorno per VID | **10** | tetto contro il pestaggio, non contro l'uso |
| Lunghezza del corpo | **2000** caratteri | una segnalazione, non un trattato |
| Formato | **testo semplice**, nessun HTML, nessun markdown, nessun allegato | niente da sanificare, niente da archiviare, niente immagini orfane da potare |

⚠️ I tetti si verificano **server-side** nel servizio, non nella pagina: *quello che la pagina nasconde, il
servizio deve comunque rifiutarlo* (`IEditAuthorizationService`). E l'errore si alza con
`Vipi.Application.*.ValidationException`, **mai** con DataAnnotations, o la UI non lo cattura e il circuito
Blazor cade.

---

## §5 — I pezzi

| Pezzo | Dove | Stato |
|---|---|---|
| `FieldReport`, `FieldReportKind`, `FieldReportStatus` | `Vipi.Domain/Entities/Support.cs` | nuovo |
| `EditorTask.FromReportId` | stesso file | esteso |
| Mappatura EF + **una** migrazione, doppia emissione (SQLite + MySQL) | `Vipi.Infrastructure/Persistence` + `Vipi.Infrastructure.MySqlMigrations` | nuovo |
| `IFieldReportRepository` + impl EF | `Vipi.Application/Abstractions` + Persistence | nuovo |
| `IFieldReportService` — `ApriAsync`, `MieAsync`, `CodaAsync`, `PrendiInCaricoAsync`, `RisolviAsync`, `RespingiAsync`, `DoppioneAsync` | `Vipi.Application/Content` | nuovo |
| `WorkOrigin.Campo`, `WorkAction.ApriSegnalazione`, mappatura | `Vipi.Application/Content/WorkItem.cs` | esteso |
| Pagina «Segnalazioni» (le mie · la coda) | `Vipi.Ui/Pages/SegnalazioniPage.razor`, rotta `/services/vsop/reports` | nuovo |
| Bottone + modulo nel documento pubblico | isola interattiva, accanto al titolo di sezione | nuovo |
| Banner dell'editor: terza natura | `DocReviewBar.razor` | esteso |
| Voce nella navigazione admin, con conteggio della coda | `AdminNav.razor` | esteso |
| Chiavi IT+EN | `SharedResource.resx` / `.en.resx` | nuovo |

### Le tre trappole della pagina pubblica

1. ⚠️ **Il documento pubblico è SSR statico.** Il modulo di segnalazione è uno **stato che cambia**, quindi
   deve vivere **dentro** la propria isola interattiva: un componente con `@rendermode InteractiveServer`
   che si occupa di sé. È la lezione già pagata delle chip morte su pagina statica (27 agosto).
2. ⚠️ **In stampa il bottone non esiste.** È chrome, e il foglio stampato non ne porta
   ([print-css](../feature/2026-07-30-stampa-documenti.md)).
3. ⚠️ **Il catch-22 degli ingressi** (pre-flight §3): la pagina delle segnalazioni deve essere raggiungibile
   e comprensibile **quando è vuota** — `EmptyState`, non un elenco bianco.

---

## §6 — Lo schema, e la finestra cieca

Il lavoro **tocca il database**, quindi va letto insieme alla memoria `finestra-cieca-al-16-settembre`.

- ✅ **Si può spedire lo stesso**: lo schema non è congelato — in produzione `Database.Migrate()` gira
  all'avvio sul pacchetto caricato via FTP. Sono congelati **i dati**.
- ✅ La migrazione è **puramente additiva**: una `CreateTable` e una colonna nullable. Nessun `Drop*`,
  `Rename*`, `AlterColumn`, `Sql`; default vero su ogni stringa NOT NULL; nessun indice unico nuovo.
  🔴 **Deve passare `MigrazioniDellaFinestraCiecaTests` senza eccezioni**: se il test protesta, non si
  discute con il test.
- ✅ **Nessun travaso**: la tabella nasce vuota in produzione, e vuota va benissimo.
- ⚠️ **Una migrazione sola**, non due: tabella e colonna insieme. Due migrazioni nella stessa finestra sono
  due occasioni di avvio fallito, e l'avvio fallito lì dentro non ha rete.

---

## §7 — Che cosa NON si fa

- **Nessuna mail, nessuna notifica** (§2/D6).
- **Nessuna segnalazione anonima** (§2/D1).
- **Nessun allegato, nessuna immagine.** Una biblioteca allegati esiste, ed è un'altra cosa: qui aprirebbe
  archiviazione, potatura degli orfani e moderazione di file caricati da chiunque.
- **Nessun thread.** Una segnalazione, una risposta. Se serve parlarne, si parla su Discord: un forum dentro
  un sito di documentazione è un prodotto diverso, e va deciso come tale.
- **Nessuna «domanda».** I due tipi sono *errore* e *suggerimento*. Un canale di domande fa di questa pagina
  un helpdesk, che nessuno ha chiesto e che qualcuno dovrebbe presidiare. ⚠️ Se il committente lo vuole, è
  una riga nell'enum — ma è una **decisione**, non un dettaglio.
- **Nessun voto, nessun «anche a me»**: farebbe di una segnalazione una petizione.
- **Non è il bug tracker del sito.** Questo canale parla dei **documenti**. I difetti del prodotto restano
  dove stanno.
- **Nessuna riga in `AuditLog`**: chi ha triato e quando sta **sulla riga stessa**, e un secondo registro
  della stessa cosa è un secondo posto da tenere allineato.

---

## §8 — Le slice

1. **S1 — lo schema**: entità, mappatura, la migrazione (una), il presidio della finestra verde. Nulla di
   visibile.
2. **S2 — il servizio**: apertura, tetti, cancelli, i quattro esiti; **test puri** sul cuore (chi può cosa,
   i tetti, gli stati che si possono attraversare).
3. **S3 — la pagina**: «le mie» + la coda, con `EmptyState`. Da qui il giro è **usabile dallo staff**.
4. **S4 — l'ingresso dal documento**: bottone di sezione, isola interattiva, contesto (documento · sezione ·
   rilascio).
5. **S5 — il ponte**: `WorkOrigin.Campo`, `WorkAction.ApriSegnalazione`, `FromReportId` usata, `DocReviewBar`.

Un commit per slice, `dotnet build Vipi.slnx -c Release --no-incremental` verde **sui due TFM** a ogni passo.

## §9 — La prova

- **Test puri** (`Vipi.Application.Tests`): i tetti, il cancello di livello, il ciclo degli stati (una
  respinta senza motivo si rifiuta), il rifiuto server-side anche quando la pagina l'avrebbe permesso.
- **Test del read-model**: una segnalazione presa in carico **non compare due volte**; tornando indietro,
  ricompare — è il gemello esatto del test che già esiste per gli impatti.
- ⚠️ **Test di migrazione**: `MigrazioniDellaFinestraCiecaTests` verde, e lo schema fisico che combacia sui
  due provider.
- **Verifica live, guidata sul flusso reale**, ed è l'unica che prova la cosa che conta — che il giro si
  **chiuda**: un utente non-staff apre una segnalazione da un documento pubblicato, uno staffista la prende
  in carico, l'incarico compare in «Da fare», la risposta torna all'autore.
  ⚠️ Per impersonare un livello più basso si usa il metodo già scritto in `docs/lavori-aperti.md` §AL.

---

# §10 — Il secondo canale: dallo staff allo sviluppatore (9 settembre 2026)

**Richiesta del committente (9 set 2026):** *«vorrei pensare a un sistema di feedback, sia dall'utente verso
lo staff che dallo staff verso lo sviluppatore»*.

## §10.0 — Perché tutto questo è fermo, e da quando

🔴 **Rimandata per decisione del committente, 9 settembre 2026**, con la motivazione: *siamo nel periodo
cieco e la cosa richiede un grosso cambiamento sul database*. Vale per **tutto** il piano, §1-§9 compresi:
non è mai partita nessuna slice.

⚠️ **La motivazione va letta con precisione, perché il §6 dice un'altra cosa.** Tecnicamente la migrazione di
§6 è **additiva e spedibile** anche dentro la finestra cieca — una `CreateTable` e una colonna nullable
passano `MigrazioniDellaFinestraCiecaTests`. Quindi il blocco **non** è un divieto del presidio: è
**prudenza sulla taglia**. Con il secondo canale di questo §10 la superficie di schema cresce ancora
(un enum e tre colonne in più), e una tabella nuova più un canale nuovo nella settimana in cui i dati non si
possono ripristinare è un rischio che il committente ha scelto di non correre.

▶ **Quando si riapre:** **dopo il 16 settembre 2026**, quando la finestra cieca si chiude. A quel punto la
carta è pronta: §1-§9 non vanno ridiscussi, §10 sì — restano le quattro domande di §10.6.

## §10.1 — Che cosa c'è oggi, rilevato nel sorgente (9 settembre 2026)

| Canale | Stato |
|---|---|
| **Utente → staff** (§1-§9 di questa carta) | **carta pura**: `FieldReport` e `FromReportId` hanno **zero occorrenze** nel sorgente; `WorkOrigin` ha ancora solo `Sistema` e `Persona` |
| **Staff → sviluppatore** | **non esiste**, né carta né codice |

Oggi il secondo canale passa da tre strade, **tutte fuori dal prodotto**:

1. `errori-richieste.txt` scaricato via FTP e messo in `diagnostica/` — automatico ma **pull**, e lo tira lo
   sviluppatore;
2. il **codice della richiesta** stampato da `PaginaErrore` (*«Se la segnalate, indicate questo codice»*),
   fotografato e mandato a mano;
3. voce, posta, Discord.

⚠️ **La carta esclude questo canale di proposito** (§7: *«Non è il bug tracker del sito. Questo canale parla
dei documenti. I difetti del prodotto restano dove stanno»*). Aprire il secondo canale **riapre quella
decisione**: va scritto che si riapre, non fatto di straforo.

## §10.2 — Una macchina sola, due bersagli

La §1 del FEATURE-PROCESS vale qui più che altrove: *estendi o sostituisci, mai affiancare*. Due sistemi di
segnalazione accanto sarebbero il **terzo** errore della stessa famiglia, dopo `DocumentImpact`,
`EditorTask` e il read-model `WorkItem` che è servito a rimetterli in fila.

Il ciclo del secondo canale è **identico** al primo: un autore, un corpo in prosa, la presa in carico, i
quattro esiti, la risposta obbligatoria. Cambia **chi triaga** e **che contesto la riga si porta dietro**.
Regola del 2: due casi sono **una colonna enum**, non due tabelle.

```csharp
public enum FieldReportTarget { Documenti, Prodotto }
```

| | **Documenti** (canale 1) | **Prodotto** (canale 2) |
|---|---|---|
| chi apre | chiunque connesso IVAO (§2/D1) | staff, `VipiRole.Editor` (3) in su |
| contesto | `DocumentId` · `SectionKey` · `ReleaseNumber` | 🔴 `Rotta` · `CodiceRichiesta` · `Timbro` (`1.17.0 · 30ca658`) · user-agent |
| chi triaga | staff `Editor`+ | **lo sviluppatore** |
| che cosa la chiude | una risposta | un **rilascio** |
| gravità in «Da fare» | `DaRileggere` / `Normale` (§3) | da decidere — vedi §10.6/4 |

🔴 **Il pezzo che vale più di tutto il resto: `CodiceRichiesta` catturato in automatico.** Oggi arriva la
fotografia di una pagina «Error.» e la caccia dentro `errori-richieste.txt` la si fa a mano. Se la riga
porta il **codice** e il **timbro**, si salta dritti allo stack e si sa **quale commit** girava — che è
esattamente il passo 2 della memoria `diagnostica-di-produzione`. Quel giunto la posta elettronica non può
farlo.

**Corollario:** il bottone «segnala questo» va messo **dentro `PaginaErrore`**. È l'unico posto dove chi ha
appena visto il guasto ha ancora il codice sotto gli occhi.

## §10.3 — 🔴 Il nodo vero, e non è il modello

**Lo sviluppatore non vede il database di produzione.** Una segnalazione salvata in una tabella su
`atc.it.ivao.aero` è lontana da lui quanto una mail — di più, perché nessuno gliela mette in mano. Tre
uscite, e la scelta cambia più codice di tutto il resto:

| | Come | Pro | Contro |
|---|---|---|---|
| **B-1** | lo sviluppatore ha un **login admin in produzione** e legge la coda come una pagina | pulito, la risposta torna da sola, un solo meccanismo | ⚠️ oggi i controlli col login li fa il **committente**: l'accesso va concesso, ed è una decisione sua |
| **B-2** | le segnalazioni si scrivono **anche** in `diagnostica/`, come settimo file, e scendono con lo stesso viaggio FTP | zero accesso nuovo, costo quasi nullo, si aggancia a un giro che **già esiste** | ⚠️ il **ritorno** verso lo staff non ha strada: la risposta arriva solo col rilascio dopo |
| **B-3** | pagina di export, il committente scarica e passa il file | — | è la posta elettronica travestita: **sconsigliata** |

⚠️ **E qui salta la D6** (*«niente posta, niente notifiche: la lista si guarda, non insegue»*). Regge per il
primo canale — lo staff il sito lo apre ogni giorno. **Non regge per il secondo**: lo sviluppatore il sito
non lo apre. Il secondo canale ha bisogno di un meccanismo **pull** per forza, e **B-2 è quel meccanismo**.
Non è una deroga alla D6: è il riconoscimento che la D6 parlava di un pubblico diverso.

## §10.4 — Che cosa NON si tocca

- ✋ **`errori-richieste.txt` resta.** Non è ridondante con il secondo canale, è **complementare**: il file
  prende ciò che **nessuno segnala** (le eccezioni notturne, le `ObjectDisposedException`); il canale prende
  ciò che **non lancia** — un testo sbagliato, un tasto che non si capisce, una pagina storta. Spegnere il
  file perché «adesso c'è la pagina» perderebbe la metà che nessuno racconta.
- ✋ Valgono uguali per il secondo canale tutte le esclusioni di §7: **niente allegati** (nemmeno lo
  screenshot, per quanto tenti), **nessun thread**, **nessun voto**, **nessuna riga in `AuditLog`**.
- ✅ La finestra cieca non è un divieto tecnico — vedi §10.0. Ma **una migrazione sola**, come dice §6: qui
  vuol dire che tabella, colonna del ponte **e** le colonne del secondo canale nascono **insieme**, non in
  due giri.

## §10.5 — ⚠️ La collisione di nomi, da decidere prima di scrivere codice

Esiste già in `main` la migrazione **`IncaricoDaSegnalazione`** (26 agosto 2026, `20260826122501`) — e
aggiunge **solo** `EditorTasks.FromImpactId`. Lì dentro «segnalazione» significa `DocumentImpact`, cioè la
riga **dedotta dal sistema**.

Quando arriva `FieldReport`, «segnalazione» significa **due cose diverse**: la riga dedotta e la riga
scritta da una persona. Per la regola di propagazione dei gate un rename costa il doppio a valle, quindi si
sceglie **adesso**:

- **(a)** `FieldReport` prende in UI e nei documenti un nome italiano diverso — *«richiesta dal campo»* —
  e «segnalazione» resta al sistema. Costa una riga oggi.
- **(b)** si accetta la collisione e la si documenta qui. Costa ogni volta che qualcuno legge.

## §10.6 — Le quattro domande aperte

Nessuna ha risposta al 9 settembre 2026. La **1** è quella che decide più codice.

1. **Lo sviluppatore ha, o può avere, un login admin in produzione?** Decide **B-1 contro B-2** (§10.3).
2. **Chi può aprire una segnalazione «Prodotto»?** Solo staff, o anche l'utente comune? Se anche l'utente,
   serve un triage **in due passi**: lo staff smista prima di girarla allo sviluppatore.
3. **Si riapre la §7?** Vuol dire che le segnalazioni di prodotto entrano in «Da fare» accanto a quelle
   documentali, e la lista dello staff si affolla. È una decisione, non un dettaglio.
4. **Come chiude una segnalazione «Prodotto»?** Lo stato lo mette lo sviluppatore a mano, oppure si aggancia
   al **timbro di versione** e si chiude da sé quando la versione online supera quella dichiarata nella
   risposta? La seconda è più bella e più fragile: il timbro dice il commit, non dice che il difetto è andato.

## §10.7 — L'ordine, se e quando si parte

**Prima il canale 1, poi il 2 come sesta slice.** Le slice S1-S3 di §8 (schema, servizio, pagina) sono il
**90%** del secondo canale: il canale 2 aggiunge un valore d'enum, tre colonne nullable e un filtro di coda.
Farlo per primo vorrebbe dire scrivere due volte le stesse rotaie.

| Slice | Che cos'è |
|---|---|
| S1-S5 | §8 di questa carta, invariate |
| **S6** | `FieldReportTarget`, le tre colonne di contesto, il bottone dentro `PaginaErrore`, la coda filtrata per bersaglio, e **B-1 o B-2** secondo la risposta alla domanda 1 |
