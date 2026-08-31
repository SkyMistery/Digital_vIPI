# Quattro difetti letti nella diagnostica del 31 agosto, e uno era la diagnostica

*Piu' il rimedio alla radice: il catalogo delle stazioni in memoria di processo.*

**31 agosto 2026.** Ramo **`corse-e-perdita-diagnostica`**, aperto da **`consegna-20260831`** (cioè da
**ciò che gira**, non da `main`, che è 41 commit indietro). Build Release **0 avvisi** su net8 e net10,
suite verde, **23 test nuovi**, **nessuna migrazione** — quindi consegnabile dentro la finestra cieca fino
al 16 settembre. Lavori aperti **§AM**.

> ⚠️ **La prima cosa da sapere, se si legge questo file di fretta.** Uno dei quattro difetti è
> **`CollisioniDbContext`**, cioè lo strumento scritto il 24 agosto *per capire* le corse sul `DbContext`.
> Perdeva memoria a ogni query e ha ucciso il processo due volte in una giornata. Non è un dettaglio
> ironico: è la ragione per cui questa carta esiste in questa forma — **quando un guasto ha per sintomi
> «pagine che ogni tanto non si aprono», il sospettato numero uno non è il codice che scrive le pagine, è
> tutto ciò che gira a ogni richiesta senza che nessuno lo guardi mai.**

---

## Il fatto

Il committente segnala due sintomi, in produzione, senza uno schema evidente:

1. **«This page did not open»** — la pagina d'errore nostra, con il codice da riportare — e da lì bisogna
   tornare alla documentazione a mano.
2. **«A second operation was started on this context instance…»** sotto un documento su cui sta lavorando,
   o eseguendo una modifica, ⚠️ **pur essendo l'unico a lavorare su quel documento**.

E consegna la cartella `diagnostica/` scaricata via FTP: `avvii.txt`, `avvio-diagnostica.txt`,
`errori-richieste.txt` (634 kB), `neighbours-debug.log`.

## Come si è letta, che è la parte riutilizzabile

`errori-richieste.txt` pesa 634 kB e contiene **tre** errori. Quel rapporto è già una notizia — un file di
registro che pesa duecento volte quel che dice sta stampando qualcosa di sbagliato — ed è stato il primo
filo. Il secondo l'ha dato `avvii.txt`, che nessuno aveva pensato di aprire perché il sintomo non
somigliava a un riavvio:

```
10:57:28Z  AVVIO  1.1.0  ⚠ il processo precedente NON si è spento in modo ordinato — era partito 03:01:56 prima
13:05:22Z  AVVIO  1.1.0  ⚠ il processo precedente NON si è spento in modo ordinato — era partito 02:07:54 prima
```

**Due morti male in una giornata, a distanza di circa due-tre ore l'una dall'altra.** Una cadenza così
regolare non è un difetto che scatta su un gesto: è qualcosa che **cresce**. E i tre errori registrati
stanno tutti nella finestra fra le due (11:31 → 11:40).

> ⚠️ **La lezione di metodo.** `avvii.txt` esiste da un giorno solo (§AE, scritto per il riquadro «Attempting
> to reconnect»), ed è il file che ha dato la direzione a tutta l'indagine. Un registro di *stato* vale più
> di un registro di *errori*: gli errori dicono che cosa è morto, lo stato dice **come stava il processo**.

---

## A · `CollisioniDbContext` perdeva memoria a ogni query 🔴

`src/Vipi.Application/Diagnostica/CollisioniDbContext.cs`

Lo strumento teneva **due** strutture: una `ConditionalWeakTable` (giusta — debole, muore col `DbContext`)
e, accanto, un `List<WeakReference<…>>` chiamato `Viventi` per poterle enumerare tutte al momento dello
scatto. E ci aggiungeva un elemento **dentro `Apre()`**, cioè **a ogni comando SQL eseguito dal processo**:

```csharp
lock (lista) lista.Add(nuova);
lock (Viventi) Viventi.Add(new WeakReference<List<Aperta>>(lista));   // ← una per query, per sempre
```

La potatura (`Viventi.RemoveAll(w => !w.TryGetTarget(out _))`) stava solo dentro `Fotografa()`, che gira
**soltanto** quando qualcuno lancia «second operation» — cioè quasi mai. E anche quando girava toglieva i
morti, mai i **duplicati dei vivi**: la stessa lista compariva N volte.

Con quattordici `HostedService` che interrogano il database a ciclo continuo più il traffico normale, sono
milioni di oggetti in poche ore. Le due morti delle 10:57 e delle 13:05 hanno esattamente quella forma.

**La prova nel file, leggibile a occhio nudo**: dentro una sola fotografia la stessa lista di operazioni
compare **34, 38 e 44 volte**, identica riga per riga. Non erano trentotto query concorrenti: era una lista
sola, stampata trentotto volte.

### Che cosa c'è adesso

- **`Viventi` non esiste più.** `ConditionalWeakTable` è enumerabile e prende il suo lucchetto da sé: la
  tabella debole è l'**unica** struttura che cresce, e cresce solo quanto i `DbContext` vivi.
- **Tetto per contesto (64).** Serve perché una lettura **abbandonata** — richiesta annullata a metà
  enumerazione — non chiude mai il suo lettore e lascia la riga lì; su un circuito che vive ore le righe si
  accumulerebbero. Oltre il tetto si butta la più vecchia.
- **Interruttore**: `VIPI_DIAGNOSTICA_COLLISIONI=0` spegne tutto senza ricompilare. Dopo questa giornata,
  chi si trovasse un processo che cresce deve poter escludere questo codice in un minuto invece che in un
  pacchetto FTP.
- **Niente più doppia chiusura.** `TracciaCollisioniInterceptor` sovrascriveva sia `DataReaderClosing` sia
  `DataReaderDisposing`, e EF li chiama **tutti e due** sullo stesso lettore: la seconda `Chiude()` non
  trovava più la sua riga e `FindLastIndex` toglieva quella di un'**altra** operazione con lo stesso SQL —
  cioè proprio la riga che avrebbe dovuto raccontare la collisione. Resta solo `DataReaderDisposing`.

> **La regola che resta:** *uno strumento di diagnosi non può tenere stato che cresce con il traffico.*
> Presidio: `CollisioniSenzaPerditaTests` (il gemello in `Vipi.E2E.Tests/CollisioniDbContextTests` prova
> invece che l'aggancio scatti e che l'intercettore sia montato: qui si prova che lo strumento non COSTI).

---

## B · Il registro allegava a ogni errore venti fotografie, quasi tutte di altri guasti

`src/Vipi.Host/DiagnosticaErrori.cs`

`Registra()` stampava **tutta** la coda degli scatti (venti) sotto **ogni** voce. Due conseguenze, tutt'e
due misurate sul file vero:

- **634 kB per tre errori.** Il tetto di rotazione è 512 kB: il file si metteva da parte dopo **tre voci**,
  e la storia che serviva a capire non c'era già più.
- **Fotografie di altre richieste allegate alla tua.** La voce delle **11:40:17** portava, come più recente,
  uno scatto delle **11:37:06**: tre minuti prima, con dentro `WorkListService.PerDocumentoAsync` e
  `AccDerivationService.DeriveFrequenciesAsync`, che con `/services/vsop` non c'entrano niente. ⚠️ Questa è
  la parte pericolosa: una scena di un altro guasto, allegata al tuo, **la prima volta la si legge come se
  fosse la tua**. È lo stesso modo di sbagliare che il 24 agosto era costato un giro di deploy.

**Adesso**: `CollisioniDbContext.UltimoScatto(freschezza)` restituisce **una** fotografia, l'ultima, e solo
se è stata scattata **entro dieci secondi**. Una corsa sul `DbContext` e l'eccezione che ne esce distano
millisecondi, non minuti: se è più vecchia, è di un altro guasto e non si allega.

---

## C · Il pannello delle traduzioni correva contro l'editor che lo contiene

`src/Vipi.Ui/Components/TranslationReviewPanel.razor` · `src/Vipi.Application/Translation/DocumentTranslationReview.cs`

È il **«A second operation»** che il committente vedeva «pur essendo l'unico a lavorare sul documento» — e
infatti la corsa non era fra due persone: era **fra il pannello e chi sta scrivendo**. Due voci su tre nel
registro, dieci secondi l'una dall'altra, su `/services/vsop/libb/editor`:

```
System.InvalidOperationException: A second operation was started on this context instance…
   at EfEditingRepository.LoadForEditAsync
   ← DocumentTranslationReview.RigheAsync
   ← TranslationReviewPanel.OnParametersSetAsync
```

Tre difetti nello stesso metodo, e ⚠️ **ognuno dei tre era già scritto nero su bianco altrove**, nessuno
applicato qui:

1. **Non era isolato.** `@inject IDocumentTranslationReview` = il `DbContext` del circuito, cioè quello che
   l'editor genitore sta usando mentre monta i figli. Sei componenti hanno uno scope proprio dal 30 luglio
   2026 esattamente per questo.
2. **La lettura non era condizionata al cambio dei parametri.** `OnParametersSetAsync` **non** scatta solo
   al montaggio: scatta a **ogni ridisegno del genitore**, e l'editor si ridisegna al primo `await` di
   qualunque suo gestore — ogni salvataggio, ogni sezione aggiunta, ogni blocco aperto. È la regola pagata
   il 1 agosto 2026 con `ReleasePanel` (*«non è l'`@if` sui dati caricati a proteggere: il pericolo è il
   RI-render»*).
3. **Caricava il documento DUE VOLTE.** `RigheAsync` fa `LoadForEditAsync`, cioè il documento intero; e
   quando tornava vuoto, `StessaLinguaAsync()` lo richiamava con l'**altra** lingua solo per capire se il
   vuoto significasse «stai già leggendo nella lingua del documento». Due letture complete, a ogni
   ridisegno, **per un blocco che è chiuso di suo** (`InitiallyOpen="false"`) e che nessuno stava guardando.

### Che cosa c'è adesso

- **Due istanze dello stesso servizio, e la differenza conta.** `_lettura` viene da `ScopedServices`
  (scope DI proprio → `VipiDbContext` isolato) e serve a **tutto ciò che parte dal ciclo di vita**;
  `Revisione` resta `@inject`, sul contesto del circuito, e serve alla **scrittura**.
  ⚠️ **Non è una svista, ed è la parte da non «uniformare» in una pulizia futura**: `CorreggiAsync` passa da
  `IEditAuthorizationService`, che risolve l'identità da `IHttpContextAccessor`. In uno scope creato dopo la
  richiesta quell'`HttpContext` non c'è più: il servizio risponderebbe «anonimo» e **il salvataggio verrebbe
  rifiutato a tutti**. È la stessa regola già scritta per gli altri sei componenti isolati — *i servizi che
  leggono i claim restano quelli del circuito*. E la scrittura non ha comunque la corsa da evitare: parte da
  un clic, non dal render.
- **Guardia sul cambio** `(DocumentId, lingua)`: il ridisegno del genitore non rilegge più niente.
- **`RevisioneAsync`**, che torna `RevisioneDocumento(LinguaSorgente, Righe)`: la lingua sorgente si
  **dice**, non si deduce da un secondo giro. `RigheAsync` resta come implementazione di default
  dell'interfaccia, per chi vuole solo le righe.
  ⚠️ E la deduzione vecchia era anche **sbagliata**: su un documento senza niente da tradurre nessuna delle
  due lingue ha righe, quindi «se l'altra lingua ne ha, allora è stessa lingua» rispondeva **no** anche a
  chi stava leggendo proprio nella lingua del documento. C'è un test apposta.

### Quel che NON è stato fatto, e perché

**Caricare solo quando il blocco si apre.** Era nella proposta, ed è stato lasciato fuori dopo averlo
guardato: il blocco è un `<details>` nativo con persistenza JS, quindi il server non sa quando si apre se
non aggiungendo un `@ontoggle` — e soprattutto **l'intestazione del blocco mostra il contatore**
`da rileggere / totale`, che è la cosa che dice *se vale la pena aprirlo*. Caricare pigramente spegnerebbe
proprio l'informazione per cui il pannello sta lì chiuso. Con l'isolamento e la lettura singola il costo è
già passato da «due documenti a ogni gesto» a «un documento all'apertura della pagina».

---

## D · La home moriva su una lettura che la barra sopra proteggeva già

`src/Vipi.Ui/Pages/SopHome.razor`

È il **«This page did not open»**. Codice `00-c4cd722449e979cfba270f800c474cd7-…`, 11:40:17 UTC,
`GET /services/vsop`:

```
System.InvalidOperationException: Cannot Open when State is Connecting.
   at MySqlConnector.MySqlConnection.Open()
   at EfStationDirectory.ListAccs()
   at StationResolver.Prewarm()
   at Vipi.Ui.Pages.SopHome.OnInitialized()   ← SopHome.razor:94
```

La riga era una sola, nuda: `protected override void OnInitialized() => Stations.Prewarm();` — una lettura
del database nel ciclo di vita, senza rete sotto.

⚠️ **La parte istruttiva è che il rimedio c'era già, e stava un piano sopra.** La barra
(`SopLayout.LeggiCatalogo`) quella stessa lettura la protegge dal 24 agosto: se fallisce, ingoia, scrive un
avviso e la barra esce senza i collegamenti alle ACC. Ma **ingoiare lascia la cache del resolver vuota**, e
la pagina sotto ritenta la stessa lettura sullo stesso contesto rotto — dove rete non ce n'era. Cioè la
sequenza vera è:

1. la barra prova per prima, fallisce, ingoia e prosegue → l'avviso finisce su `stdout`, che su Passenger è
   il vuoto (nessuno lo legge);
2. la cache del resolver resta quindi **vuota**;
3. la home ritenta, e muore. **500.**

Il presidio della barra proteggeva la barra e non ciò che le sta dentro.

### Che cosa c'è adesso

Lo stesso gesto, per lo stesso motivo: il catalogo si legge **una volta** nel ciclo di vita dentro un
`try/catch`, la pagina tiene `_accs` in un campo suo, e il markup guarda quel campo invece della proprietà
pigra del servizio (che dentro il markup **lancia**, e lì non si può catturare niente).

⚠️ Con un avviso **suo**, e non riusando «il database è vuoto»: i due vuoti si somigliano a schermo e non
sono la stessa notizia. Dire «il database è vuoto» a chi ha avuto un singhiozzo manda a cercare un guasto
che non c'è. Due chiavi nuove, `Home_CatalogDownTitle` e `Home_CatalogDownBody`.

⚠️ La guardia serve **anche** dentro `OnlineCount`, e non è ridondante con l'`@if` del markup:
`ResolveByCallsign` legge **anche** la mappa degli aeroporti, che è una seconda lettura pigra e lancia per
conto suo.

---

## Perché la connessione fosse «Connecting», e che cosa resta aperto

`Cannot Open when State is Connecting` è MySqlConnector che trova la propria `MySqlConnection` già in fase
di apertura: due usi concorrenti della stessa connessione, cioè la stessa famiglia dei difetti C e D. Le
condizioni che la rendono probabile su quel server sono note e stanno scritte in `avvio-diagnostica.txt`:
`MaximumPoolSize=20`, quattordici `HostedService` che aprono scope a ciclo continuo, e — quel giorno — un
processo che stava esaurendo la memoria per il difetto A.

⚠️ **Non è dimostrato quale coppia di operazioni si sia scontrata sulla home**, e non lo si può dimostrare
dal file di quel giorno: la fotografia allegata a quell'errore era di tre minuti prima (difetto B). È
**esattamente** il buco che B chiude: alla prossima occorrenza la fotografia sarà quella giusta o non ci
sarà nessuna fotografia, e in entrambi i casi non si andrà a caccia del sospettato sbagliato.

✅ **Il rimedio alla radice è stato fatto nello stesso giro**: è la sezione **E** qui sotto. La misura da
fare prima c'era davvero, ed è servita: ha trovato che `Bump()` mancava in sei metodi su sette.

---

## E · Il rimedio alla radice: il catalogo si legge **una volta per processo**

`src/Vipi.Application/Content/CatalogoStazioni.cs` ·
`src/Vipi.Infrastructure/Persistence/BumpCatalogoStazioniInterceptor.cs`

I difetti C e D sono due modi diversi di proteggersi dalla **stessa** lettura: ACC e mappa aeroporti, che
`IStationResolver` rileggeva dal database **una volta per circuito** — cioè per ogni sessione aperta e per
ogni richiesta SSR. Proteggere i chiamanti uno per uno funziona, ma va rifatto a ogni pagina nuova e si
dimentica: in una settimana quella lettura è finita nello stack di **tre** guasti.

Sono dati di **divisione**: sette ACC e novantatré aeroporti, uguali per tutti, che cambiano quando un
amministratore tocca la struttura o quando passa il giro notturno. Adesso stanno in `CatalogoStazioni`, che
è **singleton**.

- Il **cosa** sta nel singleton; il **come si legge** resta nel resolver, che è `scoped` e ha
  l'`IStationDirectory` del suo scope. ⚠️ Un singleton che si tenesse un `DbContext` sarebbe una
  dipendenza prigioniera — esattamente il difetto da togliere, non da spostare.
- La copia e la versione con cui è stata riempita stanno in **un oggetto solo**, scambiato con
  `Volatile.Write`. ⚠️ Due campi separati non si possono leggere insieme: chi legge potrebbe vedere il dato
  vecchio e la versione nuova, e da lì in poi terrebbe per buona una copia scaduta **per sempre**.
- La versione si legge **prima** della query, non dopo: una scrittura che arriva mentre la lettura è in volo
  fa nascere la copia già vecchia, e il prossimo rilegge.
- Una lettura **fallita** non si mette in cache: il prossimo ritenta. Senza, un singhiozzo del database
  durante la prima lettura dopo un riavvio spegnerebbe il catalogo per tutti e fino al riavvio dopo.
- La serratura si tiene **mentre** si legge, ed è voluto: alla partenza a freddo venti circuiti facevano
  venti letture uguali, e con `MaximumPoolSize=20` era il modo di prendersi il pool intero per un elenco di
  sette righe. Adesso ne parte una e le altre aspettano quella.

`Prewarm()` **resta**, e serve ancora: qualcuno dev'essere il primo, e se paga la lettura dentro il render
cade sullo stesso `DbContext` della pagina. Quel che cambia è **quante volte** capita — una per processo
invece di una per circuito — e su Passenger, che spegne per inattività, capita a ogni risveglio.

### 🔴 Il difetto che questa modifica avrebbe creato, se non lo si fosse cercato prima

Allargare la cache sposta il peso su `IStationCatalogVersion`: prima una spinta mancata costava un dato
vecchio per il tempo di un circuito, adesso costerebbe un dato vecchio **finché qualcuno non riavvia**.
Quindi, prima di scrivere una riga, si è contato chi spinge e chi scrive:

| | |
|---|---|
| chiamate a `Bump()` | **4** |
| posti che scrivono `Acc` o `Airport` | **11** |

Mancava in `CreateAcc`, `DeleteAcc`, `CreateAirport`, `DeleteAirport`, `MoveAirport`, `SetAirportHidden`,
in tutta la catena di eliminazione (`DeletionService`) e nella scrittura delle **coordinate**
dell'aeroporto (`EfAirportSectorRepository`). ⚠️ **Nessuno se n'era accorto**, e la ragione è istruttiva:
la copia era `scoped`, quindi una richiesta SSR ne apriva una nuova ogni volta e il dato vecchio durava un
istante. Con la cache di processo, un amministratore che crea un ACC non lo vedrebbe comparire — né lui né
nessun altro.

Il rimedio non poteva quindi essere «ricordarsi la riga in sei posti in più»: la spinta è andata **dove
avviene la scrittura**, cioè in un `SaveChangesInterceptor` che guarda il change-tracker. Un posto solo, e
nessuno se ne può dimenticare — nemmeno il codice che nessuno ha ancora scritto. Le quattro chiamate a mano
sono state **tolte**, con le loro dipendenze.

- ⚠️ `Modified` conta quanto `Added` e `Deleted`: quota, variazione magnetica, IATA, coordinate e i due
  segni militari cambiano con un `UPDATE`, e un filtro sul solo inserimento avrebbe lasciato fuori proprio
  il giro notturno.
- ⚠️ Si spinge **prima** del salvataggio, non dopo. Il segnale è un numero da invalidare, non un evento da
  consegnare: una spinta di troppo costa **una rilettura** di sette ACC, una spinta mancata costa un dato
  sbagliato a schermo fino al riavvio. Fra i due errori si sceglie il primo, apposta.
- ⚠️ L'intercettore va montato su **tutti e tre** i provider (SQLite, Postgres, MySql): dimenticarne uno
  vuol dire un ambiente in cui il catalogo non si aggiorna più, e non lo direbbe nessun test che gira
  sull'altro. Sono tre righe in `Vipi.Infrastructure/DependencyInjection.cs`, e ci sono tutte e tre.

## Verifica

- Build Release `--no-incremental` su **net8 e net10**: **0 avvisi, 0 errori**.
  ⚠️ Regola già pagata: `dotnet test` verde non vale se la build è fallita, e `Directory.Build.props` rende
  gli avvisi errori mentre `dotnet test` non applica quel flag.
- Suite completa **verde**: 4 681 su net8, 4 347 su net10.
  ⚠️ In un primo giro su net8 era rosso `DelayedUiActionTests.La_nuova_annulla_la_precedente` — misura una
  tempistica di 40 ms in una finestra di 250 ms; passa da solo e nei giri successivi è tornato verde. È
  contesa di macchina, non una regressione di questo ramo, ma va scritto invece che nascosto: un rosso
  intermittente che nessuno annota è il modo in cui si perde un difetto vero.
- **23 test nuovi**, e due di essi provati **al contrario**: tolta la guardia da
  `TranslationReviewPanel.OnParametersSetAsync`, `Un_ridisegno_del_genitore_non_rilegge_il_documento`
  diventa rosso e gli altri due restano verdi; spento il filtro di `BumpCatalogoStazioniInterceptor`,
  **quattro** dei cinque test del bump diventano rossi. Un presidio che non fallisce quando il difetto torna
  non è un presidio.
  ⚠️ E la prima controprova sull'intercettore è stata **buttata**: la modifica non compilava (nullable), il
  test è girato sui **binari vecchi** e ha detto verde. È la regola già scritta — `dotnet test` verde non
  vale se la build è fallita — e vale anche quando si sta cercando un rosso.

### 🔴 Quel che i test NON possono dire

- La perdita di memoria di A si vede **solo in esercizio**: la prova sta in `avvii.txt`, e la conferma sarà
  **l'assenza** di righe «NON si è spento in modo ordinato» nei prossimi giorni. È da rileggere fra qualche
  giorno, insieme ad AE1.
- Le corse di C e D si aprono con la **latenza di un database remoto**: in locale la finestra di
  sovrapposizione è quasi nulla. La verifica vera è dal vivo, da un'identità con permessi di editor, su
  `/services/vsop/libb/editor` e `/services/vsop` **subito dopo un riavvio** (memoria fredda).

---

## Dove sta

| File | Cosa |
|---|---|
| `src/Vipi.Application/Diagnostica/CollisioniDbContext.cs` | A — niente `Viventi`, tetto per contesto, interruttore, `UltimoScatto` |
| `src/Vipi.Infrastructure/Persistence/TracciaCollisioniInterceptor.cs` | A — via `DataReaderClosing` (chiudeva due volte) |
| `src/Vipi.Host/DiagnosticaErrori.cs` | B — una sola fotografia, e solo se fresca |
| `src/Vipi.Application/Translation/DocumentTranslationReview.cs` | C — `RevisioneAsync` + `RevisioneDocumento` |
| `src/Vipi.Ui/Components/TranslationReviewPanel.razor` | C — scope proprio in lettura, guardia sul cambio |
| `src/Vipi.Ui/Pages/SopHome.razor` | D — il catalogo non decide se la pagina esiste |
| `src/Vipi.Ui/Resources/SharedResource*.resx` | D — `Home_CatalogDownTitle`, `Home_CatalogDownBody` |
| `src/Vipi.Application/Content/CatalogoStazioni.cs` | E — la copia di processo, con serratura e versione |
| `src/Vipi.Application/Content/StationResolver.cs` | E — non tiene piu' cache sue: porta solo il *come si legge* |
| `src/Vipi.Infrastructure/Persistence/BumpCatalogoStazioniInterceptor.cs` | E — la spinta dove avviene la scrittura |
| `src/Vipi.Infrastructure/DependencyInjection.cs` | E — l'intercettore su tutti e tre i provider |
| `AccAdminService`, `AirportImportUseCase`, `StructureEditingService` | E — tolte le quattro `Bump()` a mano e le loro dipendenze |
| `tests/Vipi.Application.Tests/CollisioniSenzaPerditaTests.cs` | A, B — 5 test |
| `tests/Vipi.Application.Tests/DocumentTranslationReviewTests.cs` | C — 3 test (una lettura sola, lingua sorgente, documento vuoto) |
| `tests/Vipi.Ui.Tests/TranslationReviewPanelTests.cs` | C — 3 test (montaggio, ridisegno, cambio documento) |
| `tests/Vipi.Ui.Tests/CatalogoNonAffondaLaHomeTests.cs` | D — 3 test |
| `tests/Vipi.Application.Tests/StationResolverCacheTests.cs` | E — 8 test (4 riscritti, 4 nuovi: venti sessioni una lettura, la corsa alla partenza, la lettura fallita, la scrittura in volo) |
| `tests/Vipi.Infrastructure.Tests/BumpCatalogoStazioniTests.cs` | E — 5 test sul bump (e uno che dice che NON si spinge a sproposito) |
