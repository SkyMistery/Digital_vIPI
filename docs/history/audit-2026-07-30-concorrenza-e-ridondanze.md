# Audit — concorrenza, codice morto, ridondanze (30 lug 2026) ✅

> **Chiusura (30 lug).** Tutti i punti attuati sul branch `fix/audit-race-deadcode-redundancy` (14 commit).
> Suite **631 verde** (Domain 23 · App 273 · Infra 228 · Hosting 18 · Ui/bUnit 85 · E2E 4), build 0 warning.
> Verifica live eseguita guidando l'app reale. Restano due voci **non di codice**, in coda (§6).

Revisione senior a caccia di race condition, codice morto e ridondanze. Punto di partenza: la build era già a
**0 warning**, quindi tutto ciò che segue era invisibile al compilatore — codice raggiungibile solo da simboli
pubblici mai chiamati, e difetti di concorrenza che nessun test copriva.

---

## 1. Concorrenza (8 fix)

| # | Difetto | Fix |
|---|---|---|
| A1 | **`EfUnitOfWork` non era retry-safe.** Avvolgere la transazione nell'execution strategy è necessario ma non sufficiente: al retry (`EnableRetryOnFailure` su Neon) la strategy rigira la lambda sullo **stesso context scoped**, e il rollback **non** ripulisce il change-tracker → le entità `Added`/`Modified` del tentativo fallito venivano riemesse (doppi insert) | `ChangeTracker.Clear()` in testa a ogni tentativo + `ct` passato alla strategy. 4 test |
| A2 | **Cache e lock d'istanza su servizi transient.** `AuroraSidProvider`/`AuroraTowerShapeProvider` sono registrati con `AddHttpClient<I,T>` = **transient**: la cache era per-risoluzione (itfix/itvor e twrs.tfl ri-scaricati a ogni click) e il `SemaphoreSlim` d'istanza non sincronizzava nulla — sincronizzazione morta, malgrado i doc XML dicessero «cache per processo» | Nuovo `SectorfileCache` **singleton** (pubblicazione con `Volatile`, gate per fetta, errore non memorizzato) + `SectorfileRaw` per il GET condiviso. 4 test |
| A3 | **TOCTOU in `StaffLoginThrottle`.** Il leggi-poi-scrivi lasciava passare due richieste concorrenti dello stesso utente — la doppia scrittura DB che la classe esiste per evitare, e Blazor Server apre più richieste in parallelo per pagina | Decisione atomica con `TryAdd`/`TryUpdate` sul valore osservato. 5 test |
| A4 | **ABA sul CTS dell'heartbeat lock.** `EditLockBar.StopHeartbeat()` annullava `_beat` senza verificarne la proprietà: un loop vecchio che scopriva di aver perso il lock uccideva il battito **nuovo** avviato da «Inizia modifica» → heartbeat morto in silenzio e lock scaduto (TTL 3 min) mentre l'utente editava | `StopIfCurrent` con confronto d'identità; stato del lock letto dentro `InvokeAsync`; catturata anche `ObjectDisposedException` (che il `catch (OperationCanceledException)` non prendeva, lasciando un'eccezione non osservata) |
| A5 | **Init schema Postgres non serializzata.** Un rolling deploy su Render fa coesistere istanza vecchia e nuova: due `EnsureCreated` concorrenti possono collidere con un errore di tabella duplicata, non intercettato → l'istanza perdente non partiva. Inoltre il reconcile copriva solo le **colonne**, non gli **indici** (che `EnsureCreated` crea solo insieme alla tabella) | `PostgresSchemaReconciler.InitializeSchema` unico punto d'ingresso: `pg_advisory_lock` + colonne + **indici** + diagnostica su `ILogger` (prima solo `Console.Error`, invisibile nei log Render) |
| A6 | **Stampede e cache avvelenata nel meteo.** La lista aeroporti chiede il METAR di tutti gli scali in parallelo: a cache fredda erano N chiamate identiche in volo su un'API pubblica con rate limit. E un esito **vuoto** veniva memorizzato col TTL pieno → un blip NOAA di pochi secondi azzerava il meteo per tutta la finestra | Coalescenza per ICAO (`Lazy` con `ExecutionAndPublication`) + `WeatherOptions.EmptyTtlMinutes` (default 1). 8 test |
| A7 | **Coppie valore/scadenza lette non atomicamente.** `IvaoTokenProvider` e `IvaoAirportCache` tenevano token/catalogo e scadenza in due campi letti fuori dal gate: una `DateTimeOffset` non si scrive atomicamente, quindi il percorso veloce poteva vedere una scadenza strappata | Snapshot immutabile in un solo campo pubblicato con `Volatile` — lo schema che `OnlineAtcCache` già applicava correttamente. `IvaoAirportCache` non espone più `Gate`/`Items`/`ExpiresAt` pubblici |
| A8 | → è diventato §2 | |

## 2. Import SID rotto in silenzio (il difetto più grave, trovato per caso)

Avevo aggiunto un indice unico su `AirportSids(AirportId, StableKey)` ragionando sul codice. Provandolo su una
**copia del `vipi.db` reale** è fallito: `UNIQUE constraint failed`. La `StableKey` **esclude di proposito la
cifra della revisione**, quindi un file `.sid` con due revisioni della stessa SID (es. `ROBO1H` e `ROBO2H`)
produce legittimamente due righe con la stessa chiave — 20 coppie su 1478 righe nel DB di sviluppo.

Diagnosticando quel fallimento è emerso il difetto vero: `ReplaceImportedSidsAsync` indicizzava le righe
precedenti con `ToDictionaryAsync(x => x.StableKey)`, che su chiave ripetuta **lancia**. Il primo import passava
(tabella vuota, nessuna chiave da indicizzare) e **ogni reimport successivo falliva** — quindi l'import SID era
rotto in modo permanente su **LIRF, LIMC, LIME, LIBG, LIED, LIEO, LIPQ**, e in silenzio perché
`SidImportHostedService` logga il fallimento per-aeroporto a `LogDebug`.

Fix: indicizzazione **first-wins** per `StableKey` (ordine per `Id`); log del fallimento per-aeroporto a
`LogWarning` con conteggio dei falliti; annotato in `VipiDbContext` e `SidImporter` **perché** l'indice unico non
si può aggiungere, così nessuno ci riprova. Schema invariato. Verificato reimportando i 7 aeroporti sulla copia
del DB reale: tutti OK, conteggi invariati.

> ⚠️ **Regola che ne esce: le migration si provano su una copia di `src/Vipi.Host/vipi.db`**, non solo su DB
> vuoti da `EnsureCreated` — i test partono sempre da vuoto e non possono vedere questa classe di problemi.

## 3. Codice morto rimosso (450 righe)

Ogni voce verificata simbolo per simbolo su `src` + `tests` + `tools`: nessun chiamante, nessun riferimento in
markup, nessun uso nei test.

- **Verticale audit**: `IEditAuditWriter` + `EfEditAuditWriter` + registrazione DI. `RecordEditAsync` non era mai
  chiamato, quindi erano irraggiungibili anche la dedup a finestra di sessione, `Merge`/`Parse` e `EditDetails`.
  L'audit che serve continua a essere scritto **inline** da `EfEditGrantRepository`, `EfEditingRepository`,
  `EfReleaseRepository` e letto da `AuditPage`/`VersioniPage`.
- **Verticale membri divisione**: `IDivisionMembersProvider` + `IvaoDivisionClient` + `DivisionMembersDto` + 2
  registrazioni DI. Residuo dell'approccio «elenco membri dall'API», sostituito dal roster popolato dai login;
  il doc dell'interfaccia lo ammetteva («oggi solo UserId manuale»). `DivisionOptions` **resta**, è usata altrove.
- **Componente orfano**: `SectionShell.razor` — zero riferimenti nel codice, ma due doc lo davano per «usato da
  ACC & APP».
- **Tipi/membri**: `AccConfigAor`, `AccRegulatedArea` + `AccBlock.RegulatedAreas` (marcata legacy, mai letta né
  scritta: emetteva una chiave morta nel JSON ACC salvato), `AorPalette`, `UndoHistory<T>`;
  7 metodi d'interfaccia senza chiamanti; `SourceAirport.Latitude/Longitude` (scritte e mai rilette — cleanup
  già annotato in `handoff-round22.md`, ora chiuso).

## 4. Ridondanze estratte

- **`FrequencyPositions`** (Application): ordine, nome leggibile e sigla-da-tipo delle posizioni-frequenza erano
  **triplicati** nei repository di derivazione e **avevano già divergiato** — la copia dell'aeroporto usava
  `position ?? "—"` invece di `IsNullOrWhiteSpace(...)`, quindi una posizione di soli spazi rendeva una **cella
  bianca** nel documento aeroporto dove ACC/APP rendevano il trattino. 26 test.
- **`AirportViewFormat`** (Ui/Shared): `InitialClimb`, `QnhRowMatches`, `ParseTransitionLevels`, `MapRule`, prima
  duplicate fra `AeroportoPage` e `AirportQuickPanel`. Scrivendo i test sono emersi due difetti: un JSON valido
  ma **non-oggetto** faceva cadere il render (`TryGetProperty` lancia `InvalidOperationException`, che non è una
  `JsonException` e sfuggiva al catch), e i due parser divergevano su `columns`. 44 test.
- **`ReleasePanel` in `AppEditorPage`**: era l'unico dei tre editor con la copia inline (~110 righe). Ora delega
  al componente condiviso. Differenze di comportamento **volute**: gli errori compaiono nei callout del pannello
  invece che nella barra della pagina, l'annullamento **chiede conferma** (prima no), i pulsanti si disabilitano
  durante l'operazione. 13 test bUnit (il componente non ne aveva, e ci si appoggiano tre editor).
- **`ReleaseDiffTable`**: tabella diff condivisa fra `ReleasePanel` e `VersioniPage`. `VersioniPage` **non** può
  usare `ReleasePanel` (è multi-documento, con stato e comandi per riga). 9 test.
- **`SectionCatalog.IsRenderModeToggleable`**: la regola del toggle Live/Frozen, identica nei tre editor.
- **Catch-all nei guard degli editor**: `Guard`/`GuardCore`/`Guarded` catturavano solo le eccezioni di dominio;
  qualunque altra (`DbUpdateException`, Npgsql…) sfuggiva, abbatteva il circuito e lasciava il badge inchiodato
  su «Salvataggio». Aggiunta la catch finale in tutti e quattro gli editor.

> **Lezione: confrontare i corpi, non le firme.** Il rilevamento per firma segnalava come duplicati anche
> `IsMandatory`, `IsHidden`, `IsDerived` — che condividono il **nome** ma codificano regole di dominio diverse
> per tipo (la vLOA lavora per Titolo, l'ACC sul blocco, l'APP sul catalogo di profilo). Unificarle le
> falserebbe: **restano separate di proposito.** Non è stata estratta nemmeno la macchina a stati del badge
> salvataggio: per composizione servirebbero quattro callback per ~20 righe di stato UI stabile, sul percorso di
> salvataggio dei tre editor.

## 5. Verifica live — un bug che 631 test verdi non vedevano

Guidando l'app reale (skill `.claude/skills/verifica-live/`) la timeline delle release mostrava a schermo
**`rel. v@r.VersionNumber`**: in Razor una `@` circondata da caratteri non-spazio è riconosciuta come indirizzo
**email** e non apre un'espressione, quindi finiva nell'output letterale. **Nessun warning di compilazione.**
Serve `v@(r.VersionNumber)`. Difetto pre-esistente in 4 punti (`ReleasePanel`, `VersioniPage` ×2, `ChangedPage`);
corretti tutti, con test di regressione sul markup reso.

È la stessa famiglia dei binding Razor silenziosi già annotata nel `FEATURE-PROCESS` (`Key="x"` ≠ `Key="@x"`):
**i test sul markup non bastano se nessuno guarda la pagina.**

Verificato a schermo anche il resto: tabella livelli di transizione con intestazioni e riga «current QNH»
evidenziata dal METAR live, tabella SID con tutti i casi di initial climb (`5000 ft` · `FL90` · testo non
numerico invariato), diff aperto/chiuso e annullamento con conferma nel pannello release.

## 6. Aperto (non di codice)

- **Dato editoriale**: la SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` e viene resa «90 ft», quota
  implausibile — le altre BANAV hanno `9000` → «FL90». Probabile `9000` battuto come `90`: da correggere
  nell'editor, non nel codice.
- **Publish APP end-to-end**: il pannello è montato e configurato correttamente, ma un publish reale
  dall'editor APP richiede prima la creazione della bozza e resta da provare a mano.
