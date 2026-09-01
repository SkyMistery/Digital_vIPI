# Piano — le segnalazioni dal campo 🟣

**Stato:** **carta, non eseguita** · **Aggiornato:** 1 settembre 2026
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
