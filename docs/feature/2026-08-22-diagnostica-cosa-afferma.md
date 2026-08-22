# Diagnostica — cosa afferma, e cosa succede quando è lei a rompersi (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/services/vsop/admin/diagnostics`. Prima carta del giro: **la sostanza**.
> La forma sta nella gemella [`2026-08-22-diagnostica-densita-ui.md`](2026-08-22-diagnostica-densita-ui.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## La domanda della pagina

«**Qualcosa nei dati non torna?**» — e, per chi la apre davvero, quella subito dopo: «**dove vado a
sistemarlo?**».

Il giro precedente ha lasciato una regola scritta: *prima di renderla bella, verificare che la pagina dica il
vero*. Versioni, Audit e Sorgenti — tre pagine su tre aperte per la densità — nascondevano un difetto di
sostanza. La Diagnostica è, per mestiere, una pagina che **afferma cose**: qui la verifica non è un di più.

## Cosa ho trovato

### ⚠️ D1 — La pagina che diagnostica i guasti muore se ha un guasto lei

`ConsistencyReportService.RunAsync` chiama in fila quattro sonde e **nessuna è protetta**:

```csharp
if (_schema  is not null) findings.AddRange(await _schema.RunAsync(ct));
if (_admin   is not null) findings.AddRange(await _admin.RunAsync(ct));
if (_server  is not null) findings.AddRange(await _server.RunAsync(ct));
if (_startup is not null) findings.AddRange(_startup.Findings);
```

E `DiagnosticaPage.OnInitializedAsync` fa `await Consistency.RunAsync()` **senza try/catch**. Due conseguenze,
la seconda peggiore della prima:

1. una sonda che lancia (`MySqlServerSettingsProbe` su un server che non risponde, `PostgresSchemaDriftProbe`
   su una connessione caduta) **uccide il circuito Blazor**: pagina morta, e il messaggio vero solo nei log;
2. e anche prendendo l'eccezione, **un guasto di una sonda cancella il lavoro di tutte le altre**: il report
   è una lista sola, costruita in ordine — se `_server` lancia, le incongruenze dati che `Analyze` aveva già
   trovato **non arrivano mai a video**. Un guasto della sonda del server nasconde una pista orfana.

⚠️ È la stessa lezione già scritta **in questa stessa area**, e non applicata a sé: `StartupMaintenanceReport`
esiste perché «un guasto di una passata d'avvio non deve uccidere l'avvio, ma non deve nemmeno restare
zitto». Il servizio che raccoglie quel registro non fa per le proprie sonde ciò che quel registro insegna.

Lo stesso vale per `VipiHealthCheck`, che chiama la stessa `RunAsync`: un'eccezione lì fa uscire l'health
check **Unhealthy con un messaggio di eccezione** invece di `Degraded`, cioè un monitor esterno legge «il
sito è giù» quando il sito è in piedi e a non funzionare è la sonda.

### ⚠️ D2 — Il sottotitolo promette **meno** di quello che la tabella mostra

«Incongruenze dei riferimenti **deboli** (soft-ref): etichette denormalizzate e padri per callsign». Ma nella
stessa tabella possono comparire:

| Rilievo | Che cosa è |
|---|---|
| `Pista orfana`, `Label pista divergente`, `Area fantasma`, `Gerarchia dangling`, `Area regolamentata dangling`, `Callsign ambiguo` | soft-ref — ciò che il sottotitolo promette |
| `Drift di schema` | schema fisico ≠ modello EF |
| `sql_mode`, `max_allowed_packet` | impostazioni del **server di database** |
| `Manutenzione d'avvio` | una passata dell'avvio è fallita |
| `Nessun admin fra gli staffisti conosciuti` | **configurazione**: nessuno può editare |

Cinque famiglie diverse in una tabella che si presenta come una sola. È il difetto di Audit **al contrario**:
là la pagina prometteva una categoria che nessuno scriveva, qui non promette quattro categorie che scrive.
E ha un costo pratico: chi legge «Manutenzione d'avvio» in una tabella di soft-ref non capisce se sia un
problema dei dati, e chi legge il sottotitolo non sa che quella tabella è anche il posto dove appare
«nessuno può editare» — il rilievo più grave che l'applicazione sappia produrre.

### ⚠️ D3 — In inglese, i rilievi sono in italiano

Misurato a video (`Accept-Language: en-US`): le intestazioni sono tradotte, il contenuto **no**.

> SEVERITY · CATEGORY · ENTITY · DETAIL
> SEVERE | **Gerarchia dangling** | Settore ACC LGGG_W_CTR | *ParentCallsign «LIRR_XX_CTR» non esiste nei
> cataloghi: catena di copertura interrotta.*

`Category` e `Detail` sono stringhe italiane cablate in `ConsistencyReportService`, `ServerSettings`,
`SchemaDrift`, `StartupMaintenance` e `AdminCoverageService`. La pagina è bilingue, il suo contenuto no —
ed è **l'unica** pagina admin in cui il contenuto è prosa scritta dall'applicazione, non dati.

### D4 — Nessun rilievo dice dove si ripara

Otto rilievi, **zero link** (contati nel DOM). La riga dice «Clausola #1 (LIBB, punti Y01-Y12)» e chi legge
deve andarsela a cercare a mano fra gli accordi. Eppure **chi produce il rilievo sa dove si ripara**: la
gerarchia in Struttura, le condizioni di clausola in Trasferimenti, le aree regolamentate nell'editor del
documento. È l'informazione che manca di più a chi la pagina la apre per lavorare, non per guardare.

### D5 — Il report è una fotografia, e non si può rifare

Nessun «Aggiorna»: gira una volta in `OnInitializedAsync` e per rieseguirlo si ricarica la pagina. Misurato:
**~1,3 s** dall'apertura al render completo, stabile su tre aperture di fila (la pagina legge fresco — la
cache dei 2 minuti è solo dell'health check, ed è giusto così). La pagina non dice **quando** ha guardato,
e chi ha appena corretto un'incongruenza non ha modo di chiedere «e adesso?».

### D6 — I pattern admin sono configurazione stampata a video

`^IT-DIR$ · ^IT-ADIR$ · ^IT-WM$ · ^IT-AWM$ · ^IT-AOC$ · ^IT-AOACS$ · ^IT-AOA\d+$ · ^LI[A-Z0-9]+-CH$ ·
^LI[A-Z0-9]+-ACH$` — due righe di espressioni regolari sempre a schermo. Servono **quando qualcosa non
torna** (per capire se un pattern è sbagliato), non tutte le volte.

## Cosa faccio

Cinque slice di sostanza, un commit ciascuna, `dotnet build -c Release --no-incremental` (0 avvisi) +
`dotnet test` su **entrambi** i TFM a ogni commit.

### 1. Una sonda che si rompe è un rilievo, non la fine del report
Ogni sonda dentro il proprio `try`. Il guasto **non sparisce**: diventa un `ConsistencyFinding` della
famiglia «Sonda non riuscita», gravità `Error`, che dice quale sonda e perché. ⚠️ `catch
(OperationCanceledException) { throw; }` **prima** di ogni `catch (Exception)`: la cancellazione della
richiesta non è un guasto della sonda.

E la pagina prende comunque la sua rete: `try/catch` attorno alla chiamata, con la fascia d'errore al posto
del circuito morto. Test: una sonda che lancia ⇒ le altre arrivano tutte **e** compare il rilievo della
sonda rotta.

### 2. Le famiglie si dichiarano, e il sottotitolo dice il vero
`ConsistencyFinding` prende un `Area` (dati / schema / server / avvio / configurazione). Non è una stringa
in più da tenere allineata: è **la** cosa che distingue «una pista è orfana» da «nessuno può editare», e
serve sia al testo della pagina sia ai chip che filtrano (slice della densità). Il sottotitolo passa nel «?»
e nomina tutte e cinque le aree.

### 3. Il rilievo dice dove si ripara
Campo `Where` (rotta) sul finding, valorizzato da chi il rilievo lo produce — è l'unico che lo sa.
⚠️ **Non una mappa categoria→rotta nella pagina**: sarebbe un secondo posto da tenere allineato, e la regola
139 («un formattatore per un tipo di dato») vale anche qui. Dove non c'è un posto da aprire (server, schema,
configurazione) il campo resta `null` e la cella non mostra niente: un link che non porta da nessuna parte è
peggio di nessun link.

### 4. Il contenuto del report si legge nella lingua dell'interfaccia
⚠️ La scelta va detta prima di farla, perché tocca cinque file. `Category` e `Detail` **non** diventano
stringhe localizzate al momento della scrittura: il finding lo consumano anche l'health check e i log, dove
una lingua d'interfaccia non c'è. Il finding porta invece una **chiave** più i suoi argomenti, e chi lo
mostra lo traduce — `ConsistencyNarrator` in `Vipi.Ui`, gemello di `AuditNarrator` e con lo stesso patto:
chiave sconosciuta ⇒ si mostra il testo grezzo, mai una riga vuota.

Le stringhe italiane esistenti restano come **testo di riserva** dentro il finding: è ciò che l'health check
e i log continuano a leggere, e non c'è una seconda verità da mantenere.

### 5. «Aggiorna», e da quando è la fotografia
Tasto in testata (come su Audit) e l'ora dell'analisi accanto al conteggio. Il report **resta senza cache**
sulla pagina: chi la apre l'ha aperta per vedere adesso.

## Cosa NON faccio, e perché

- **Non isolo il report su un `DbContext` proprio.** Fa scansioni complete sul context del circuito, ma la
  pagina non monta figli che interrogano il DB al montaggio (`MediaCleanupCard` è già
  `OwningComponentBase`, e legge solo a clic): la finestra di sovrapposizione che ha prodotto i crash
  «second operation» qui non si apre. Lo scrivo perché è un rischio **noto e valutato**, non ignorato: se un
  domani si aggiunge alla pagina un componente che legge il DB montandosi, va isolato quello.
- **Non aggiungo nuovi controlli.** Il giro chiude quello che la pagina già fa; controlli nuovi sono un giro
  loro, con la loro carta.
- **Non tocco `ConsistencyReportCache`.** I 2 minuti valgono per l'endpoint anonimo e sono argomentati;
  la pagina legge fresco ed è giusto.
- **Non silenzio i rilievi noti.** Un «ignora questo» richiede uno stato persistente e la domanda «per
  quanto?»: è un giro suo, e su un report che oggi trova zero righe nel DB di sviluppo sarebbe carpenteria
  senza un caso d'uso misurato.

## Rischi

- La slice 4 tocca **cinque** produttori di finding e due consumatori (pagina e health check). Il rischio è
  perdere il testo di un rilievo per una chiave sbagliata: da qui il patto «chiave sconosciuta ⇒ testo
  grezzo», e un test che percorre **tutte** le categorie prodotte.
- La slice 1 cambia il comportamento dell'health check: un guasto di sonda passa da `Unhealthy` (con lo
  stack) a `Degraded` (con un rilievo leggibile). È il comportamento voluto — «la sonda è rotta» non è «il
  sito è giù» — ma va detto a chi guarda il monitor.
