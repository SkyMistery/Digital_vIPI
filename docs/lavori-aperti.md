# Lavori aperti — elenco unico

**Aggiornato:** 4 settembre 2026 — ✅ **§BE: LE SEZIONI SI MUOVONO DAVVERO**, ramo `sezioni-mobili`, otto fette. Richiesta del committente. ⚠️ **Tre richieste su quattro avevano già il tasto** — frecce anche sulle sotto-sezioni, occhio «nascondi» a ogni profondità, «⤒ sopra il corpo» — ma leggendo il codice per dirlo sono usciti i due difetti che le rendevano vere a metà: 🔴 `SectionNode` **ignorava «sopra il corpo» quando il corpo lo rende la pagina** (l'editor la mostrava sopra, il pubblicato la metteva sotto — la trappola del doc 11 §8, terza volta), e la **vIPI ACC non passava mai `IsDraft`** alle sotto-sezioni, quindi una nascosta spariva anche dall'anteprima di bozza. Il lavoro nuovo è **la riparentazione**: `MoveSectionToParentAsync` con cinque guardie nel MOTORE, il menu **«⇵ Sposta in…»** su ogni sezione **libera** di tutti e cinque gli editor, e le **figlie che ora si trascinano** nel menu-sezioni — con il drop fuori gruppo che riparenta. Decisioni del committente: solo le libere, dentro il blocco per l'ACC, menu **e** trascinamento. In più: la freccia ↑↓ **reinserisce e rinumera** invece di scambiare due `Order` (su due fratelli con lo stesso numero non faceva nulla) e lo spareggio `ThenBy(Id)` allinea chi legge e chi sposta. Quattro suite verdi, build Release 0 avvisi, **sei prove guidate a schermo** su copia del DB — compreso un trascinamento **vero** via CDP. ⚠️ Nessuna migrazione. ⚠️ I documenti già pubblicati cambiano solo se **ripubblicati**.

**Aggiornato:** 3 settembre 2026, notte fonda — 🔴 **IL KEEP-ALIVE: VERDETTO DATO, e due ping al minuto NON bastano.** `diagnostica/avvii.txt` scaricato alle 22:06Z, le due ore a confronto: col ping **solo a +0** **53 avvii/ora**, vita mediana **15 s**, processo acceso il **43%** del tempo, buchi fino a **72 s**; col ping **+0 e +30** **60 avvii/ora**, vita mediana **44 s**, acceso il **72%**, buchi al massimo **31 s**. Il secondo ping ha fatto qualcosa — vita quasi tripla, e i giri periodici a 15 s ora partono **sempre** (60/60 contro 32/57) — ma **gli avvii restano sessanta l'ora**: il processo muore lo stesso, solo più tardi. E il numero che conta è l'ultimo: **nessuna vita arriva a 150 s** (0 su 60), quindi il giro periodico più lungo continua a non partire mai. ⚠️ **Il regalo della misura**: il processo parte a `:20`, muore fra `:48` e `:57`, e il ping delle `:50` lo ritrova già morto — **la finestra d'inattività di Passenger è ~30 s**, quindi i ping devono stare **sotto** quella distanza, non sopra. ✅ **Cura pubblicata** (commit `f1a38332`, versione Cloudflare `3b415af0`): `PING_EXTRA_MS = [1e4, 2e4, 3e4, 4e4, 5e4]`, un ping ogni 10 s. Provata in locale a `fetch` e D1 stubbati (sei ping a 0/10/20/30/40/50 s, `scheduled()` ritornata in **18 ms**) e confermata con `wrangler tail`: **wallTime 53-61 s** contro i ~31,6 s di prima. ⚠️ Le tre invocazioni del tail escono con `outcome: exception` ed **è la quota D1**, che lancia dentro `runPoller`: i ping erano già partiti e quelli in `ctx.waitUntil` hanno continuato lo stesso — prova sul campo della scelta «ping prima di D1». ▶ **Le due prove che restano, e nessuna si vede da qui**: (1) `avvii.txt` riscaricato via FTP fra qualche ora — se le righe di AVVIO smettono di comparire il processo non muore più; se ne resta una al minuto l'inattività è sotto i 10 s e la strada dei ping è finita (resta il pannello Plesk, e serve Ivao.It); (2) `wrangler d1 info atc-archiver-db` dopo la mezzanotte UTC — stasera `rows_read_24h` era **5 546 878** con la quota esaurita, ma quella finestra comprende ancora le ore **prima** dell'indice parziale: il verdetto è il valore di domani, atteso sulle decine di migliaia. ℹ️ Nella stessa diagnostica: avvio sceso a **5 678 ms** (era 8 433), e `avvio-errore.txt` + `errori-richieste.txt` **cancellati sul server** come previsto.

**Aggiornato:** 3 settembre 2026, notte fonda — ✅ **TRE LAVORI DEL COMMITTENTE, TUTTI IN `main` E NESSUN RAMO APERTO** (`f1a38332`). 🔴 **Non sono in produzione**: servono un pacchetto (§BB tocca due fogli di stile) e, per §BC e §BD, il primo avvio in produzione — che fa girare le riconciliazioni. **§BB — la testata si compatta**: sopra il documento stavano titolo, un blocco di tre bottoni e un riquadro pieno per dire la lingua; ora sono **due righe** (sottotitolo + chip pilota/ATC, avviso di simulazione + gettoni), su tutte e cinque le famiglie. Il vSOP militare, unico senza sottotitolo, ne ha uno gemello del civile. ⚠️ Terza volta che si paga `.doc-head` **nascosto in stampa**: `PrintMeta` ha una riga in più. 🔴 E guidando la pagina è saltato fuori che **l'anteprima di BOZZA di una vIPI ACC bloccata non era bloccata**. **§BC — i parcheggi ai Dati generali**: spostare una sezione in un **altro gruppo** non è un cambio di catalogo ma una **migrazione** (il catalogo decide solo alla nascita, e il riordino sposta solo fra fratelli) → `ReparentMilParkingsAsync`, e nello stesso giro `AddMissingCatalogSections` impara a guardare **i vSOP militari** e a scendere nelle **sotto-sezioni**. **§BD — le carte dello scalo**: «Carte aeroportuali» con Aerodromo, Carte di avvicinamento strumentale, SID, STAR, VFR, prima di «Validità e revisione», su vIPI d'aeroporto **e** vSOP militare. È anche il collaudo del modulo di §BC: su copia fresca del DB, **84 sezioni aggiunte** = 14 documenti × 6, e nient'altro. Suite verde su nove progetti, build Release 0 avvisi, tutto guidato a schermo.

**Aggiornato:** 3 settembre 2026, notte — ✅ **1.6.2 È IN PRODUZIONE** (20:25:55Z, timbro `1.6.2 · 84b0f4c`, avvio 8 433 ms, configurazione sana) e la `diagnostica/` è stata riletta. Zero errori di richiesta dopo il caricamento — ⚠️ ma la fotografia copre **sei minuti**: non è un verdetto. Gli ultimi due (20:12, ancora 1.6.1) erano `A second operation` su `/services/vsop/libb/airports/editor`, cioè esattamente quel che 1.6.2 corregge. 🔴 **Ma la cosa grossa che ha detto quella cartella non è il pacchetto: è il KEEP-ALIVE.** `avvii.txt` conta **58 avvii in un'ora** (19:21→20:25): il Worker chiama al minuto, il processo parte a `hh:mm:59`, vive **7-15 s**, si spegne **in modo ordinato** per inattività, e ricomincia. **Il ping sveglia e non tiene su.** 🔴 La conseguenza vera non è il carico ma i **giri periodici**: il `bootDelay` più corto dei tredici è **15 s** e il più lungo **150 s** — su vite così corte **non ne arriva in fondo nessuno**, e prima del keep-alive gli avvii lunghi (1h50, 2h25, 3h00) bastavano al gate delle 24 ore. È esattamente il caso previsto qui sotto («se restano lì dopo qualche giorno di ping attivo, allora il keep-alive non funziona»). ▶ **Rimedio provato e PUBBLICATO** (versione `decbc00e`): il cron non scende sotto il minuto, quindi la frequenza vera la fa `PING_EXTRA_MS = [3e4]` — un ping in più a **+30 s** dentro la stessa invocazione, in `ctx.waitUntil`. ⚠️ **Mai atteso in linea**: sposterebbe di mezzo minuto il campionamento ATC. Provato in locale a `fetch` e D1 stubbati (ping `+1 ms`, poller `+17 ms`, `scheduled` ritornato `+18 ms`, secondo ping `+30 026 ms`) e confermato dal vivo con `wrangler tail`: `wallTime` **31,6 s** contro il ~1 s di prima. ⚠️ **Trenta secondi è un TENTATIVO**: le vite stanno fra 7 e 15 s, quindi può non bastare — **la prova è `avvii.txt`, non una `curl`**, e se conta ancora un avvio al minuto la lista va infittita (`[1e4, 2e4, 3e4, 4e4, 5e4]`). ⚠️ E il `wrangler tail` ha mostrato un'altra cosa: **la quota D1 è ancora esaurita** («exceeded D1's free tier daily row read limit»), quindi il poller non archivia fino al reset di mezzanotte UTC — il ping però parte lo stesso, che è il motivo per cui sta **prima** di D1. ℹ️ Due residui da cancellare sul server: `diagnostica/avvio-errore.txt` (è il file che mentiva, 19:19:57Z) e `errori-richieste.txt` (129 kB, tutte voci pre-1.6.2).

**Aggiornato:** 3 settembre 2026, sera — 📦 **PACCHETTO 1.6.1 PRONTO** (`vipi-1.6.1-solo-file-cambiati.zip`, sha256 `6a92984e04aedabf6045d3a0b8570c7c3716278d6a16cb3b369eab78b6cca61b`, **3,20 MB**, **7 file**). Timbro **`1.6.1 · 6ffbe23a`**. 🟢 **PATCH**: nessuna migrazione, **niente `wwwroot`**, quindi nemmeno l'indice degli asset. 🔴 **Nasce dalla diagnostica di 1.6.0**, non da un'idea: nove richieste in errore in un'ora, tutte «A second operation was started on this context instance», una degenerata in «MySqlProtocolException: Packet received out-of-order». Due componenti prendevano dal **circuito** un servizio che scrive — `IMilitaryDocumentService` in `MilSectionsEditor` (a `CreaAsync`, che crea il documento) e `IAppDocumentService` in `AppSectionsEditor` — mentre nello stesso file, dieci righe sotto, altri servizi vengono presi da `ScopedServices` **col commento che spiega perché**. ⚠️ **Difetto precedente a §AZ**, verificato su git: le pagine avevano la riga identica prima che i loro corpi diventassero componenti. Dentro c'è anche il controllo nuovo della Diagnostica (giri periodici fermi). **Provato sul pacchetto pubblicato**: timbro giusto, nessun `avvio-errore.txt`, Ricerca 162 → 6 351 caratteri, **i due editor corretti** aperti e messi in modifica senza barra d'errore, pagina unita con 34 voci e zero ancore cieche, zero 4xx. 🔴 **La corsa NON si riproduce qui**: in sviluppo è SQLite e la finestra è di millisecondi — quel che le prove dimostrano è che spostare il servizio **non ha rotto niente**; che la correzione funzioni lo dirà `errori-richieste.txt` fra qualche giorno. Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.6.1.md`. 🔴 Resta da **caricare via FTP**.

**Aggiornato:** 3 settembre 2026, sera — ✅ **1.6.0 È IN PRODUZIONE**, caricata dal committente. Verificata **dall'esterno, da anonimo**: la **Guida** contiene «Unire due documenti» e l'ancora `documenti-uniti`, testo che esiste **solo** in 1.6.0 — è la prova pubblica che girano i binari nuovi, la stessa usata per 1.5.0. 🟢 **E quindi la migrazione è passata**: se `20260903092755_DocumentiUniti` avesse fallito, `Database.Migrate()` all'avvio avrebbe fermato l'applicazione. 🔴 Restano da guardare `diagnostica/avvio-diagnostica.txt` (timbro `1.6.0 · 2a7d86f`) e l'assenza di `avvio-errore.txt`: dall'esterno la cartella è 404, ed è giusto così.

**Aggiornato:** 3 settembre 2026, sera — 🔴 **CLOUDFLARE: il Worker che saturava D1 non era quello che sembrava, e ora vIPI ha un keep-alive.** Partiti da una domanda del committente — «il server resta acceso e registra i controllori ogni minuto?» — la risposta misurata è **no**: Passenger spegne il processo per inattività (vite di **1:00 / 1:49 / 4:52**), e con esso il campionamento ATC. Il rimedio scelto: il **Cloudflare Worker `atc-archiver`** — che ha già un cron al minuto — chiama `/vsop/health/ready` a ogni giro. ⚠️ **Il ping sta PRIMA di ogni accesso a D1**, e non è ordine estetico: con la quota esaurita il poller lancia subito, e il keep-alive non partirebbe **proprio nei giorni in cui serve**. Verificato con `wrangler tail`: con D1 bloccato l'eccezione arriva **dopo** il ping. 🔴 **E la diagnosi della quota era sbagliata due volte prima di essere giusta.** L'ipotesi del committente («le 5M di righe le brucia il cron cancellando i vecchi») e la mia («le brucia l'API del validatore») erano **tutte e due false**. La verità l'ha detta **una divisione**: 5.473.070 righe lette in **121 query** = ~45.000 righe l'una, cioè scansioni complete. Il colpevole era `SELECT id, callsign FROM atc_sessions WHERE ended_at IS NULL` nel **cron**, senza un indice utile — confermato dallo **stack trace**, non dedotto. Cura: un **indice parziale** sulle sole sessioni aperte (**49**), da ~73.587 righe per giro a ~50. ⚠️ Il «dopo» **non è misurato**: la quota era già bloccata e le letture rifiutate; il DDL è passato lo stesso. Verifica dopo il reset di mezzanotte UTC con `wrangler d1 info`. 🔴 **E il ping vive solo nel BUNDLE su Cloudflare**: il sorgente TypeScript del Worker non sta in nessun repository — chi ripubblica da quello lo cancella senza accorgersene. Il bundle è ora in `deploy/cloudflare/atc-archiver/`, e il perché di tutto in `deploy/cloudflare/LEGGIMI-ATC-ARCHIVER.md`.

**Aggiornato:** 3 settembre 2026, sera — ✅ **I GIRI PERIODICI FERMI ENTRANO NELLA DIAGNOSTICA.** ⚠️ Il punto era mezzo già fatto, e me ne sono accorto **guardando invece di codificare**: `ImportHealth.Ferma` esiste da sempre — ultimo successo più vecchio di **due** cadenze — ed è esattamente la traccia di un'attesa interrotta. Aggiungere uno stato «in attesa» sarebbe stato il **secondo meccanismo per una domanda già risposta**. 🔴 Il buco vero era un altro: quel segnale si vedeva **solo aprendo la pagina Sorgenti**, e la Diagnostica — la pagina che si apre per chiedere «c'è qualcosa che non va?» — degli import non sapeva niente. Il 2 settembre diceva **«Avvio 0»** mentre metà dei giri non partiva: **uno zero che rassicura sul contrario di quel che succede è peggio di nessun numero**. Ora ogni categoria `Ferma` produce un rilievo in area **`Avvio`** — scelta deliberata (l'enum non ha un default apposta): «l'istanza gira, ma non è partita intera» descrive letteralmente il caso, e il destinatario è chi guarda il processo, non chi apre un editor. ⚠️ `InErrore` resta **fuori**: ha già il suo messaggio con la causa vera. Rete: `GiriFermiNellaDiagnosticaTests` (4). ℹ️ **Da aspettarsi**: alla prossima apertura la Diagnostica mostrerà parecchi rilievi in Avvio. Non è una regressione, è la verità che prima non si vedeva — e se restano lì dopo qualche giorno di ping attivo, allora il keep-alive non funziona.

**Aggiornato:** 3 settembre 2026 — 🟡 **DA FARE, QUANDO SI PASSA DI LÌ: due cose di forma, già viste e rimandate col committente.** (1) Sulla **pagina unita** restano **due riquadri «Validità e revisione»**, uno per documento: con la pubblicazione accoppiata dicono lo **stesso ciclo e la stessa data efficace** ma **numeri di versione diversi** (su LIMN: v9 e v7). Tecnicamente giusto — ogni documento ha la sua storia — ma un lettore potrebbe chiedersi perché ce ne siano due. Si nasconde quello del membro, oppure se ne fa uno solo per l'unione: ⚠️ nel secondo caso va deciso **che cosa dice quando i membri hanno versioni diverse**. 🟢 **Decisione: si lascia**, finché non lo vede un controllore e si lamenta — è l'unico modo per sapere se dia davvero fastidio. (2) Due campi della **tabella SID** (`.in-cond`, `.in-prio`) sono ancora **nudi**, cioè con una classe che nessun foglio definisce: ⚠️ lì però la densità è una **scelta** — la colonna del fix è larga **76px misurati** e un campo vestito la allargherebbe. Stanno in una **tolleranza con la ragione scritta** dentro `CampiVestitiTests`, e un secondo test pretende che siano ancora nudi: chi li veste **toglie anche la riga**, o l'elenco invecchia e smette di dire la verità. ℹ️ Nessuna delle due ha rischio: si fanno quando si è già in quella schermata per altro.

**Aggiornato:** 3 settembre 2026 — 📦 **PACCHETTO 1.6.0 PRONTO** (`vipi-1.6.0-solo-file-cambiati.zip`, sha256 `7b153d40cfcc25fe8e7cc1b1f1172a11190dad4bbd61ec10438cf20546f4b417`, **4,73 MB**, **25 file**). Timbro **`1.6.0 · 2a7d86fd`**. 🟠 **PORTA UNA MIGRAZIONE**, la prima da 1.3.0: `20260903092755_DocumentiUniti`, **quattro operazioni tutte additive** (2 `CreateTable` + 2 `CreateIndex`). Gira da sola all'avvio. 🔴 **E preparandolo il runbook si e' smentito**: diceva che dentro la finestra cieca «una migrazione nuova nemmeno», nominando `MigrazioniDellaFinestraCiecaTests` come presidio — ma quel test vieta le sole operazioni **distruttive**, e in produzione ci sono **gia' due migrazioni della finestra** (`CatenaDiRipiego`, `LinguaBloccata`) uscite con 1.3.0 il 31 agosto. Non dedotto: **guardato dentro** il `MySqlMigrations.dll` di quel pacchetto. La riga e' stata corretta. ⚠️ **`Vipi.Infrastructure.MySqlMigrations.dll` entra** ed è la prima volta da 1.3.0: e' l'unico assieme che porta le migrazioni di produzione. ⚠️ **E `Vipi.Host.dll` entra pur senza un sorgente cambiato**: il timbro e' un `AssemblyMetadata` del suo csproj, e senza quel file la barra direbbe ancora «1.5.0 · a071575» su binari 1.6.0. ⚠️ **C'e' `wwwroot`** (tre file coi loro `.br`/`.gz`) e con lui l'indice degli asset: viaggiano **insieme**, o le pagine escono senza stili — la trappola del 24 agosto. Impronte confrontate con **l'ultimo pacchetto che conteneva davvero ogni file** (1.5.0 per la maggior parte, **1.3.0** per le migrazioni, 1.4.x per gli asset): nessuno dei 25 identico, niente da scartare. **Provato sul pacchetto pubblicato**: timbro giusto in `avvio-diagnostica.txt` e **nessun** `avvio-errore.txt`, circuito aperto col JS **minificato** (2 400 byte su una riga), **Ricerca 162 → 6 351 caratteri** («50 results for LI»), la Guida con la sezione nuova nelle due lingue, la pagina unita di LIMN con 34 voci e **zero ancore cieche**, e il **riavvio**: processo ucciso → la pagina se ne accorge, riavviato → torna viva da sola. Zero errori, zero 4xx. ⚠️ **Il controllo B era fallito al primo giro**, e non era il pacchetto: la pagina della Ricerca ha **due** campi con lo stesso segnaposto e avevo scritto in quello della barra. Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.6.0.md`. 🔴 Resta da **caricare via FTP**.

**Aggiornato:** 3 settembre 2026 (sera) — ✅ **§BA: DUE FILE PER DUE GUASTI, e gli ultimi sette servizi fuori dal contesto del circuito.** Dalla diagnostica di produzione di 1.6.1, non da una richiesta. 🔴 `avvio-errore.txt` diceva «l'avvio è FALLITO» di uno **spegnimento**: `app.Run()` blocca fino alla chiusura, quindi il guasto all'arresto usciva dal medesimo `catch` — e il foglio appena spedito diceva «se compare, fermatevi». Ora c'è **`arresto-errore.txt`**, che non ferma niente. E la guardia sugli `@inject` che toccano il database, nata guardando **un nome solo**, ha chiuso i sette casi tollerati: `AirportSectionsEditor` ×3, `NewDocumentPage`, `VersioniPage` ×3 — ⚠️ quest'ultima da guardare, perché accanto c'è la scelta **opposta** di `ReleasePanel`, che qui non vale (quella pagina non lo monta). Ogni servizio spostato è stato **fatto scrivere** a schermo e la scrittura verificata **in archivio**. 📦 **Pacchetto 1.6.2 pronto**, 4 file (`vipi-1.6.2-solo-file-cambiati.zip`, sha256 `28f0be88…`), timbro `1.6.2 · 84b0f4c7`: niente database, niente `wwwroot`, niente frasi. Non ancora caricato.

**Aggiornato:** 3 settembre 2026 — 🔴 **§AZ RIVISTO IN SUPERVISIONE: quindici difetti, tre seri, tutti corretti.** Il peggiore: l'elenco di governo mostrava «uniti: 2» e pubblicava **un** documento, perché l'accoppiamento era una **seconda** porta che quella pagina non chiamava. La cura non era il chiamante — le porte separate non esistono più. Poi: una lingua bloccata in un membro tingeva tutta la pagina unita, e un membro pubblicato sotto un ospite in bozza spariva dal web. Sei commit, suite verde su 8 progetti su 8.

**Aggiornato:** 3 settembre 2026 — ✅ **§AZ: DOCUMENTI UNITI — una pagina, un editor, una pubblicazione.** Un APP non remotizzato si unisce al documento dell'aeroporto o al vSOP militare, indipendentemente dal tipo: ordine deciso dal redattore, **un editor solo**, e la release — fatta o pianificata — con **un clic** su tutti i membri (annullarla li annulla tutti). Provato a schermo su LIBA e su **LIMN Cameri**, misto e pubblicato. Ramo `documenti-uniti`, fuso in `main`.

**Aggiornato:** 3 settembre 2026 — ✅ **§AY: LA SCHEDA DELLA CLAUSOLA DEI TRASFERIMENTI È UNA FINESTRA, e tre classi erano scollegate dal foglio di stile.** Segnalati dal committente due difetti in notturna; cercandoli ne è uscito un terzo, il più caro: markup `xt-panel-f` contro CSS `.xt-panel-foot`, regola **morta da tre settimane**, e senza `display:flex` nel piede **l'elimina stava appiccicato al duplica** — un tasto distruttivo a 8px da uno costruttivo, che non sembrava un guasto ma una scelta. Le barre bianche erano `--on-brand` (bianco di **brand**, che non ha tema) usato per i coperchi dello scroll shadow, e si vedevano **anche senza niente da scorrere**. ⚠️ **La misura ha ribaltato la mia stessa proposta**: «la finestra è larga il doppio quindi ci sta tutto» è **falso** — da 348 a 828px di corpo il contenuto scende da 896 a 860, il **4%**, perché i campi erano impilati in una colonna sola. Quello che paga è la larghezza **spesa in due colonne** (896 → 666), e oltre i 920px non paga più niente (a 1040/1160/1280 il contenuto non si muove). Esito misurato: come la scheda si apre davvero **non scorre** (378 in 378); con tutte e tre le sezioni forzate aperte, **30px fuori invece di 394**. ✅ **In produzione dal 3 settembre 2026** (pacchetto 1.6.0), coi due fogli `vipi-theme.css` e `vipi-print.css`. Carta: [`docs/feature/2026-09-03-trasferimenti-scheda-clausola-finestra.md`](feature/2026-09-03-trasferimenti-scheda-clausola-finestra.md).

**Aggiornato:** 3 settembre 2026 — 🟡 **§AX: IMPORTARE I TRASFERIMENTI, carta scritta e non ancora eseguita** (ramo `import-trasferimenti`). Richiesta del committente: «voglio importare i trasferimenti copiando le tabelle dagli attuali documenti, anche in forma mista». **Misurato sui tre documenti veri** in `vIPI word/` (estraendo le tabelle dai `.docx`): **~450 righe di trasferimento**, e nel solo IPI di Roma **33 forme d'intestazione distinte** — che è la ragione per cui il pezzo che regge tutto è la **rimappatura a mano** già dentro `ImportaTabella`, non una spec per forma. Seconda misura, quella che decide la prima fetta: delle **494 celle FL** la grammatica di oggi ne legge **324 (66%)** come livello vero, e **170** finiscono in testo libero — ma sono tre famiglie sole (`FL130 o` ×72, la parità fuori parentesi ×20, i marcatori di nota ×10), e tre regole di normalizzazione portano a **~87%**. 🔴 **Il salto vero non è leggere le celle: è che una riga porta ente, DEST/DEP e tipo, cioè dati che nel modello stanno SOPRA la riga** — una tabella di Roma da dodici righe è **tre accordi e cinque sezioni**, non dodici clausole in una sezione. Quindi un ingresso **a livello di controparte** (quello di oggi è in fondo a quattro clic dentro una sezione sola) che costruisce un **piano** da spuntare. **Decisioni del committente**: la `ROTTA ATS` si **ignora**; della lista enti `US/TS/NE/US0` si tiene **solo il primo** e il resto non si scrive da nessuna parte (la catena di ripiego è già la gerarchia di copertura: copiarla sarebbe una seconda verità); l'import **propone e basta**, l'albero nasce tutto non spuntato; **niente `.docx`** — si copia una tabella per volta, e il rumore del documento non entra invece di dover essere filtrato. ⚠️ **La carta è in due parti, e la seconda ha una data**: la **A** (l'import) non tocca lo schema e si può fare subito; la **B** — agganciare `EKMUR 3C` alla **procedura** invece di copiarla — vuole un **catalogo STAR che non esiste** (`AirportSid` c'è, 1.269 procedure; di STAR nel modello **zero occorrenze**) e **aspetta il 16 settembre**, perché è una tabella nuova più tre colonne su una tabella viva dentro una finestra senza ripristino. 🟢 Ad aspettare non si perde niente: l'aggancio è un confronto testo→catalogo e può girare **dopo** sulle clausole già importate. Le STAR la sorgente le ha (`.str`, **90 file / 1.511 righe / 89 aeroporti**) ⚠️ ma **339 di quelle righe non sono STAR**: sono l'hack `MAPS`, e dentro i file «STAR» vivono le shape di CTR e ATZ. E il legame va per **`StableKey`, mai per Id** (la strada per Id è già stata pagata: `ConditionRefId` 215/216) ⚠️ sapendo che la `StableKey` **non è unica** e l'indice unico fa fallire la migrazione su dati veri. ⚠️ **E «si aggiorna da solo» è vero solo nell'editor**: le release sono fotografie, il documento pubblicato cambia alla **prossima release** — quindi il pezzo che rende utile il meccanismo non è il link, sono gli **impatti**. Carta: [`docs/design/piano-import-trasferimenti.md`](design/piano-import-trasferimenti.md). 🔴 **Nessuna riga di codice scritta**: si parte dalla fetta A1 (letture pure, test-first).

**Aggiornato:** 3 settembre 2026 — ✅ **MISURATO: I GIRI PERIODICI PARTONO, E LA DEDUZIONE DI UN'ORA FA ERA SBAGLIATA.** Il committente ha aperto **Sorgenti** e **Diagnostica**, ed è il dato che serviva. Ultimo esito riuscito, tutto del 2 settembre: ACC e anagrafica aeroporti **17:56**, TA / piste / settori / **SID** / navaid / aree regolamentate **18:46**, statistiche ATC **21:23**, e la **deriva 18:46Z**. 🔴 **Quindi la deriva (100 s), i navaid (60 s) e le aree (45 s) partono davvero**: la tesi «oltre il minuto non parte mai», dedotta da tre avvii presi in dieci minuti, **è falsa**. Su una giornata gli avvii lunghi capitano, e il gate delle 24 ore ha bisogno che ne capiti **uno solo**. ⚠️ Tre campioni in dieci minuti non sono una giornata: era la misura sbagliata, ed è stata corretta nel codice e qui. ✅ **Lo sweep resta a 45 s**, ma per la ragione buona: quella potatura prima girava a **ogni avvio** (sincrona), e spostandola in un giro l'avevo messa nel posto più facile da mancare della scaletta — non c'è niente da guadagnare a stare a 130 s. 🟢 **E il resto della scaletta non si tocca**: non c'è niente da riparare. 🟢 **Diagnostica: 30 rilievi, 0 gravi**, e tutti nella sola categoria **Sorgente** — settori senza poligono, cerchi TWR sintetici, un anello ripetuto su `LIRR_TS_CTR`: dati della sorgente, non nostri, e preesistenti. **Dati 0 · Schema 0 · Server 0 · Avvio 0 · Configurazione 0 · Sectorfile 0**: ⚠️ **Avvio 0** vuol dire che nessuna manutenzione d'avvio è fallita, che era l'altra domanda aperta. ℹ️ **Un documento** risulta «copia pubblicata indietro» (deriva del 18:46Z, ancora sotto 1.4.1): è la riga che ora §AW sa aprire anche **in anticipo** sul ciclo entrante.

**Aggiornato:** 3 settembre 2026 — 🔴 **IL PROCESSO VIVE UN MINUTO, E META’ DEI GIRI PERIODICI NON PARTE MAI.** È la cosa vera uscita dalla seconda diagnostica, e vale molto più della domanda con cui era cominciata. Misurato su `avvii.txt` del server: **1:00, 1:49, 4:52** — Passenger spegne il processo per inattività appena il traffico si ferma, e ogni arresto è **ordinato** (nessun crash). Ma i giri periodici aspettano un **ritardo d'avvio** che va da 15 a **150 secondi**:

| giro | ritardo | su un avvio di 1:00 |
|---|---|---|
| ACC, anagrafica, SID, settori, TA/piste, aree | 15–50 s | 🟢 parte |
| navaid | 60 s | 🟡 al limite |
| storico ATC (70 s), traffico (90 s) | 70–90 s | 🔴 no |
| **deriva** (100 s), rollup (120 s), traduzioni (120 s), **sweep release** (130 s), retention traffico (150 s) | 100–150 s | 🔴 no |

⚠️ **E se non parte non lascia traccia**: `GatedImportLoop` registra gli **esiti**, non le attese interrotte — quindi il silenzio somiglia a «va tutto bene». È la stessa trappola già pagata due volte in questo progetto (un giro fermo indistinguibile da uno riuscito). ✅ **Corretto subito quel che era mio**: `ReleaseSweepHostedService` passa da **130 s a 45 s** — prezzo dichiarato nel commento, si perde l'ordine con la deriva, ma potare qualche release **già destinata a sparire** prima di un ripuntamento vale meno di un giro che non gira mai. 🔴 **Il resto NON è stato toccato**: ritarare la scaletta di dodici servizi è una decisione di disegno, e ognuno di quei ritardi ha una ragione scritta accanto (l'ordine fra import e giri che leggono il mondo *dopo*). ⚠️ E il difetto è **preesistente**, non introdotto ora — ma §AW ci poggia sopra: la riga «da preparare» la apre la **deriva**, che sta a 100 s. **Da decidere col committente.** ℹ️ Il modo per sapere quali giri girano davvero c'è già: la pagina **Sorgenti** e la **Diagnostica** mostrano l'ultimo esito per categoria — un timbro fermo da giorni è la prova.

**Aggiornato:** 3 settembre 2026 — 🟡 **L'avvio raddoppiato: seconda misura, e resta senza causa.** Boot delle 22:09 (quello che ha fatto ripartire il processo dall'esterno): **totale 15 200 ms** — migrazioni **3 795**, manutenzioni **10 419**. Col boot delle 22:02 (14 058: migrazioni 6 175, manutenzioni 7 061) fa **due misure vicine nel totale** ma con lo **spartito diverso** fra le due voci di database: è il segno di rumore d'ambiente, non di lavoro in più. Contro **una sola** misura di 1.4.1 (6 519 ms). ⚠️ **Non attribuita**: 1.5.0 non porta migrazioni nuove e ha una manutenzione d'avvio **in meno**, quindi il lavoro dichiarato è minore. Il confronto zoppica dal lato del **paragone** — n=1 su 1.4.1 — e la prova si costruisce da sé: il file si riscrive a ogni avvio, bastano due o tre `avvio-diagnostica.txt` nei prossimi giorni. 📏 Dall'esterno intanto: prima richiesta dopo l'inattività **18,2 s**, poi **0,21 s**.

**Aggiornato:** 3 settembre 2026 — ✅ **LA DIAGNOSTICA DI 1.5.0 È PULITA** (cartella caricata dal committente). **Controllo A superato**: `avvio-diagnostica.txt` dice **`1.5.0 · a071575`**, ambiente **Production**, avvio alle **22:02:23 UTC**. 🟢 **Nessun `avvio-errore.txt`** e — la cosa che conta di più — **nessun `errori-richieste.txt`**: dal caricamento non si è rotto niente, e la corsa del blocco allegato che aveva riempito quel file su 1.4.1 non ha lasciato una riga. 🟢 **E `avvii.txt` è tutto ordinato**: quattro righe, due avvii e due arresti, **ogni arresto «in modo ordinato»** — nessuna delle righe «il processo precedente NON si è spento in modo ordinato» che avevano accompagnato il caricamento di 1.4.1. ⚠️ Il file è **nuovo** (creato alle 21:57:06Z): la storia precedente non c'è più.

🟡 **Una cosa da tenere d'occhio, e non è conclusa: l'avvio è raddoppiato.** Le fasi di 1.5.0 contro quelle di 1.4.1, dagli stessi file:

| fase | 1.4.1 | 1.5.0 |
|---|---|---|
| migrazione del database | 2 587 ms | **6 175 ms** |
| manutenzioni d'avvio | 3 129 ms | **7 061 ms** |
| **totale** | **6 519 ms** | **14 058 ms** |

⚠️ **Non è stata trovata una causa nel codice, e non se ne inventa una.** In 1.5.0 non c'è nessuna migrazione nuova, e le manutenzioni d'avvio sono **una in meno** (`PruneVipiReleases` è passata al giro delle 24 ore): il lavoro dichiarato è *minore*, non maggiore. Restano due spiegazioni plausibili e non distinte: la macchina in quel minuto (host condiviso, MariaDB, una misura sola per parte) oppure il primo avvio dopo aver riscritto **sei** assiemi invece di due. 📏 **Misurato dall'esterno il 2 settembre alle 22:09Z**, su un processo spento da Passenger: prima richiesta **18,2 s**, poi **0,21 s** e **0,20 s**. Cioè il costo lo paga **la prima visita dopo l'inattività**, e a caldo il sito è rapido. 🔴 **Come si chiude**: quella richiesta ha fatto ripartire il processo e **riscritto `avvio-diagnostica.txt`** — basta rimandare quel file per avere una **seconda misura della stessa versione**, che è l'unica cosa che distingue «la macchina in quel minuto» da «il codice».

**Aggiornato:** 3 settembre 2026 — ✅ **1.5.0 È IN PRODUZIONE.** Caricata dal committente e verificata **dall'esterno**, da anonimo. **Otto controlli pubblici verdi** (`pacchetto-verifica.js` puntato su `atc.it.ivao.aero`): `vipi-riconnessione.js` servito **e minificato** (2 400 caratteri su una riga), circuito Blazor avviato, quattro schede ACC, nessun avviso «catalogo non disponibile», **Ricerca che risponde** — è quella che passa dal server — foglio di stile in vigore, console pulita. 🟢 **E la prova che girano davvero i binari nuovi è pubblica**: la **guida in-app** contiene il testo che esiste solo in 1.5.0 — «Preparare un AIRAC» / «Preparing an AIRAC» e la riga nuova sulle SID. 🟢 **Tre documenti veri aperti** (`LIBC`, `LIBD`, `LIBR`): il riquadro **«Validità e revisione»** — uno dei tre componenti corretti — rende le sue tre righe («AIRAC cycle 2608 · Effective from… · Reviewed by Carmine Granato…»), avviso di simulazione presente, **barra d'errore nascosta**, zero errori in console, zero 4xx. 🔴 **Un difetto trovato PROPRIO dalla verifica, e ora corretto in `main` ma NON ancora online**: della coppia it/en della guida avevo aggiornato solo l'**italiana**. La riga inglese delle SID diceva ancora «imported from Aurora **on the first fetch of the AIRAC cycle**», cioè esattamente il meccanismo che §AW ha cambiato — e la guida da anonimo esce in **inglese**, che è dove si è vista. ⚠️ Non è un difetto di funzionamento, è una frase: **non vale da sola un giro di FTP**, va con la prossima consegna. 🔴 **Resta da guardare la cartella `diagnostica/`**: il **timbro** (`1.5.0 · a071575`) e l'assenza di `avvio-errore.txt` e `errori-richieste.txt` non si vedono da fuori — la cartella è 404 sul web, ed è giusto così.

**Aggiornato:** 3 settembre 2026 — ⚠️ **IL CICLO 2609 È ENTRATO IN VIGORE** (3 settembre, 00:00Z). I documenti pubblici mostravano ancora «AIRAC cycle 2608» alle 21:59Z del 2, ed era corretto. 🔴 Da adesso **il sito continuerà a mostrare le fotografie del 2608 finché non si pubblica**: è esattamente ciò per cui esiste la sezione «Prossimo AIRAC» appena consegnata. ℹ️ Ma per il **2609 non c'era niente di nuovo da pubblicare**: misurato il 2 settembre, il changelog più alto del sectorfile era `2608.txt` e **`2609.txt` non esisteva**. La prima volta in cui la sezione servirà davvero è la preparazione del **2610** (1º ottobre), e da stanotte il giro della deriva comincerà ad aprire le righe «da preparare».

**Aggiornato:** 2 settembre 2026, notte — 📦 **PACCHETTO 1.5.0 PRONTO** (`vipi-1.5.0-solo-file-cambiati.zip`, sha256 `17e0072ff19b629b3a846dfcfbc030d3bef4bcb47cf827052cf8ab668f11d165`, **4,27 MB**, **13 file**). Timbro **`1.5.0 · a071575`**. 🟢 **NESSUNA migrazione** — si consegna da sola via FTP e sta dentro la finestra cieca. ⚠️ **È 1.5.0 e non 1.4.2**: dentro c'è il contenuto di **due** pacchetti — la PATCH (la corsa del blocco allegato, i suoi due fratelli, il registro eventi) e la MINOR (§AW, il ciclo entrante) — e stanno insieme perché stanno in un `main` solo: separarle vorrebbe dire costruire da un commit intermedio e spedire due volte. Il numero segue il contenuto **complessivo**, e quello ha una **sezione nuova**. 🟢 **Niente `wwwroot`**: nessun foglio di stile e nessun JavaScript toccati, quindi niente indice degli asset — e con esso sparisce la trappola del 24 agosto. Elenco deciso col `git diff` (sei progetti) e **verificato con le impronte** contro il publish di 1.4.1: `staticwebassets.endpoints.json` **identico** → fuori; `Vipi.Infrastructure.MySqlMigrations.dll` ha impronta diversa ma **nessun sorgente cambiato** in quel progetto (solo MVID) → fuori. 🔴 **E la rete dei segreti ha suonato, su `appsettings.json`**: contiene la parola `ClientSecret`. Guardato con i miei occhi come dice il runbook — `ClientId`, `ClientSecret` e le due `ApiKey` sono stringhe **vuote**, ci sono solo i nomi delle chiavi: falso allarme. ⚠️ Ma la risposta non è stata forzare la rete: quel file **non serve** (i due valori nuovi sono identici ai default in `SectorfileOptions`), ed è **rimasto fuori** — da 14 file a 13. Così il pacchetto non sovrascrive nemmeno la configurazione base sul server. **Provato sul pacchetto pubblicato**, non sul sorgente: avviato davvero, timbro in barra `1.5.0 · a071575`, circuito aperto **col JavaScript minificato**, **Ricerca che risponde** («LI» → 50 risultati, la pagina da 161 a 6 228 caratteri), sezione **«Prossimo AIRAC»** presente e **chiusa di suo** («2609, dal 03 Sep 2026, fra un giorno, 16 da programmare»), vIPI d'aeroporto / vLOA / editor aperti con il riquadro «Validità e revisione» a posto, **zero errori** in console, zero 4xx, barra rossa nascosta. ⚠️ **Quel che NON è stato guidato dal vivo, e sta scritto nel foglio**: il **blocco allegato**, cioè proprio il riquadro che in produzione uccideva l'editor — nel `vipi.db` di sviluppo **nessun documento ne contiene uno**. Lo coprono tre test provati **rossi** col codice di prima, e i due fratelli con lo stesso difetto sono stati aperti davvero. Per questo il **controllo C** del foglio è quello che vale di più. Suite: **9836 test** su quindici progetti, E2E compresi (289); build Release `--no-incremental` 0 avvisi. Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.5.0.md`. 🔴 Resta da **caricare via FTP** e rifare i controlli su produzione.

**Aggiornato:** 2 settembre 2026, notte — 🟢 **FUSIONE: tutto in `main`, nessun ramo aperto.** Nell'ordine: **`allegati-corsa`** (PATCH — ripara la produzione) e poi **`ciclo-entrante`** (§AW, MINOR — aggiunge una sezione). ⚠️ I due rami **non si toccavano in nessun file di codice**: i soli conflitti erano in questo file e in `history/rounds.md`, dove tutt'e due aggiungevano un blocco. Misurato **sul risultato della fusione**, non sui rami: build Release `--no-incremental` verde sui due TFM con **0 avvisi**, e **suite intera verde su tutti e quindici i progetti**, E2E compresi. 🔴 **Resta da decidere la consegna**: il contenuto è di **due** pacchetti diversi — una **PATCH** (la corsa e il registro eventi: solo correzioni) e una **MINOR** (§AW: sezione nuova su Versioni, riga nuova nella lista «da fare», guida in-app aggiornata). **Nessuna migrazione in nessuno dei due**, quindi entrambi consegnabili dentro la finestra cieca al 16 settembre. Vedi `guide/preparare-un-pacchetto.md`.

**Aggiornato:** 2 settembre 2026, notte — **IL ROSSO INTERMITTENTE ERA IL REGISTRO EVENTI DI WINDOWS** (ramo `allegati-corsa`, **fuso in `main`**). `WebApplication.CreateBuilder` aggiunge da sé `EventLogLoggerProvider` quando gira su Windows, e non lo vuole nessuno: la produzione è **Linux** (Plesk+Passenger), quindi quel canale là non esiste nemmeno — non è mai stato né scelto né letto, e quel che si legge sta in `diagnostica/`. In sviluppo invece esiste eccome: **misurato sul registro della macchina, 535 voci** nel log Applicazione in **tre ore** di suite (sorgente «.NET Runtime», id 1000, una decina di host per giro). 🔴 **Ed era la causa del rosso**: quel provider tiene un `SafeEventLogWriteHandle` che muore quando il provider viene disposto, e una riga di log scritta **tardi** nello spegnimento — `AtcPollingHostedService.StopAsync` ne scrive una quando il salvataggio finale del traffico non riesce — trovava l'handle già chiuso: l'`ObjectDisposedException` risaliva dentro `Host.StopAsync` fino a far fallire il `Dispose` della fabbrica di prova, cioè il test. Servivano **due** condizioni insieme (il flush che fallisce **e** l'handle già chiuso), ed è per questo che si vedeva **due volte su undici** passate. ⚠️ Si toglie **solo** quel provider — `ClearProviders()` porterebbe via console, debug e `DiagnosticaCircuito` — e il tipo si **nomina** invece di cercarlo per stringa, così se cambiasse nome la riga non compila invece di smettere di funzionare in silenzio. Guardia in `LivelliDiLogTests`, che chiede all'host **vero** l'elenco dei provider, verificata **rossa** togliendo la correzione e con il controllo che l'elenco non sia vuoto. ✅ **Verificato anche che la nostra catena non può fare lo stesso danno**: `DiagnosticaCircuito` → `DiagnosticaErrori.Registra` è tutto dentro un `try/catch` che ingoia — quindi **non** si è aggiunto nessun `try` attorno alla riga di log di `StopAsync`: senza un trigger noto sarebbe impiastro. 🟡 **Il secondo rosso resta senza nome**: due sole apparizioni in `Vipi.Ui.Tests`, sempre in una passata a **soluzione intera** e mai isolando il progetto (18 giri in parallelo a sei vie, tutti verdi), e in nessuna delle due è stato catturato il nome del test. È in corso una caccia a otto passate col nome del test catturato (`[FAIL]`): finché non ricompare **non si tocca niente**, perché una correzione a un difetto che non si sa nominare è solo un altro cambiamento.

**Aggiornato:** 2 settembre 2026, notte — 🔴 **LA CORSA DEL BLOCCO ALLEGATO, DALLA DIAGNOSTICA DI PRODUZIONE** (ramo `allegati-corsa`, **fuso in `main`**). Il committente segnala guai sulla versione pubblica e consegna `diagnostica/`: `errori-richieste.txt` porta **quindici voci**, tre raffiche su **tre circuiti diversi** fra le 18:07 e le 18:08, e **tutte** hanno gli stessi due fotogrammi — `AttachmentBlockEditor.OnParametersSetAsync` → `EfAttachmentLibrary.ListAsync`. **Un componente solo.** La guardia era `if (_voci.Count == 0) _voci = await …`: si controlla **prima** dell'`await` e si scrive **dopo**, e `OnParametersSetAsync` scatta a **ogni** ridisegno del genitore — quindi finché la prima lettura era in volo ne partiva un'altra a ogni giro, **sullo stesso `DbContext`**. La fotografia delle collisioni lo mostra a occhio nudo: **quattro** SELECT identiche aperte insieme, a 53, 39, 36 e 35 ms. Dietro viene il resto a cascata — `NotImplementedException: unsupported frame type during diffing: None` (il renderer corrotto) e le `ObjectDisposedException` di chi arriva mentre il circuito muore. ⚠️ **Lo scope proprio NON bastava**, e il commento in cima al file prometteva il contrario: `OwningComponentBase` protegge dal contesto **del circuito**, non da **sé stessi**. Rimedio: si memorizza il **compito** e non l'esito, scritto **prima** di aspettarlo — il pattern che tutti gli altri pannelli hanno già. 🔴 **E la stessa forma aveva due fratelli senza NESSUNA guardia**: `ValidityStamp` (in **ogni** documento, e su una vIPI ACC **una volta per blocco**) e `VloaDocumentView`. Più due cose che venivano dalla stessa causa: una biblioteca **legittimamente vuota** rileggeva il database a ogni ridisegno per sempre, e un guasto della lettura **si portava via il circuito** (ora si dice in pagina, e la riga «la biblioteca è vuota» **non** compare — vuota per guasto e vuota davvero sono due cose diverse). ⚠️ Il `??=` sta **dentro** il `try`: una porta può sollevare in modo **sincrono**, ed è il primo modo in cui questa correzione era stata scritta — a trovarlo è stato il test. **7 test nuovi, tutti verificati ROSSI togliendo la guardia**; build Release verde sui due TFM, **nessuna migrazione**. 🔴 Da consegnare come **PATCH (1.4.2)**, e **prima** di `ciclo-entrante`: questo ripara la produzione, quello aggiunge una funzione. ⚠️ Due rossi **intermittenti** nelle passate a soluzione intera, nessuno riproducibile isolando il progetto e nessuno in codice nuovo (uno è l'E2E `CorsaDbContextPagineTests` che cade nello **spegnimento** dell'host di prova, sul logger EventLog con l'handle già chiuso): è contesa fra test paralleli, già vista.

**Aggiornato:** 2 settembre 2026, notte — **§AW: IL CICLO ENTRANTE** (ramo `ciclo-entrante`, **fuso in `main`**). Segnalazione: «è uscito il 2609 ma il sito non ha pubblicato le SID di quel ciclo». ⚠️ **Il calendario prima di tutto**: il 2 settembre il corrente è **2608**, il 2609 entra il **3**, e il selettore che offre 2609 è **giusto**. Ma sotto c'erano quattro cose. **§AW1** la deriva guardava solo al ciclo di oggi, e siccome le derivate per-ciclo **nascondono** quel che entra dopo, l'avviso arrivava **sempre il giorno dopo il rollover**; ora c'è `ReleaseDriftNextCycle` con severità `DaPreparare`. **§AW2** 🔴 il timbro delle SID dipendeva dall'**ora in cui passava un job** (stesso file: pubblico il 3 settembre o il **1º ottobre**), e **la sorgente il ciclo lo dichiara** — `CHANGELOG/<ciclo>.txt`, e il 2 settembre il più alto era **2608.txt**: **le SID del 2609 non esistevano ancora**. `SourceAiracCycle` ora vuol dire «il ciclo **dal quale** la riga vale» (`>=`, **nessuna migrazione**), e cade il buffer sommato **due volte** — una release programmata al ciclo entrante contiene finalmente le SID di quel ciclo. **§AW3** sezione «Prossimo AIRAC» su Versioni + programmazione **in blocco** che dice chi salta e perché. **§AW4** lo sweep delle release da «all'avvio» a **24 ore** (gli stati invecchiano da soli al rollover, e la retention non potava più). 🔴 **La verifica dal vivo ha trovato due documenti che il pubblico legge** (vIPI Milano, Catania Radar) fuori **sia dal quadro sia dalla deriva**: una release **programmata** non promuove la bozza, quindi chi è pubblicato solo così resta `Status = Draft` per sempre — e il difetto si alimentava da sé. Cancello unico `VaTenutoAggiornato`; rimisurato **14 → 16**. Carta: [`docs/feature/2026-09-02-il-ciclo-entrante.md`](feature/2026-09-02-il-ciclo-entrante.md). **9820 test verdi sui due TFM**, build Release `--no-incremental` 0 avvisi, **nessuna migrazione** (consegnabile nella finestra cieca). 🔴 Restano: la **fusione**, e il fatto che al primo giro d'import dopo il deploy le SID già in archivio non cambiano timbro (solo un contenuto cambiato riparte) — il che è voluto, ma vuol dire che l'effetto pieno di §AW2 si vede dal **primo AIRAC nuovo**.

**Aggiornato:** 2 settembre 2026, notte — ✅ **1.4.1 È IN PRODUZIONE** (§A21). Caricata dal committente e verificata dall'esterno: `avvio-diagnostica.txt` dice **`1.4.1 · 42f23f9`**, Production, avvio alle **17:44:48 UTC** in 6,2 s, **nessun `avvio-errore.txt`** e — la cosa che conta di più — **nessun `errori-richieste.txt`**: dal caricamento non si è rotto niente. **Otto controlli pubblici verdi**. Controllo C superato per intero: la **barra del circuito morto** ora **esiste su tutte e quattro** le pagine provate — guscio di servizio *e* pagine dei documenti — **nascosta** e con lo stile in vigore (`position:fixed` dal foglio comune), e `vipi-theme.css` è servito con l'impronta **`761000df`**, cioè esattamente il file del pacchetto. ⚠️ **Una riga da tenere presente in `avvii.txt`**: alle **17:36:13**, *durante il caricamento* e ancora su 1.4.0, un avvio segnala «il processo precedente NON si è spento in modo ordinato — crash, memoria esaurita, o **una dll sovrascritta via FTP**». Nessun danno (subito dopo arresto ordinato e alle 17:44 è partita 1.4.1), ma è la firma di un file sovrascritto **in luogo** invece che caricato col nome finto e rinominato: la regola del foglio esiste per quello.

**Aggiornato:** 2 settembre 2026, notte — 📦 **PACCHETTO 1.4.1 PRONTO** (`vipi-1.4.1-solo-file-cambiati.zip`, sha256 `f408bb3c91275eeba573a9a1283b8474a163bef971b165ab331f56810744396a`, 2,27 MB, **9 file**). Timbro **`1.4.1 · 42f23f9`**. **PATCH**: solo correzioni, **nessuna migrazione**, nessuna pagina nuova. Dentro c'è **§AV** e la sua seconda metà: (1) il **tornello** — una operazione per volta sul contesto della pagina, perché la corsa erano due caricamenti della stessa pagina, non un servizio iniettato male; (2) 🔴 **la barra del circuito morto sulle pagine dei documenti**, che *non c'era*: `#blazor-error-ui` stava solo in `MainLayout` e le pagine dei documenti usano `SopLayout` — un circuito morto era **muto**, cioè esattamente la seconda segnalazione della sera («clicco e non succede nulla»); (3) i guasti del circuito ora **finiscono in `errori-richieste.txt`** con la fotografia delle collisioni. Elenco confrontato per impronta col pacchetto 1.4.0, non dedotto: `vipi-ui.js` e `vipi-print.css` sono **identici** e restano fuori. **Provato sul pacchetto pubblicato**: timbro giusto, Ricerca che risponde, «+ sotto-sezione» **tre volte di fila** senza che niente si pianti, barra presente e nascosta. Suite: **5085 test**, E2E compresi (288). Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.4.1.md`. 🔴 Resta da **caricare via FTP**.

**Aggiornato:** 2 settembre 2026, sera — **§AV: LA CORSA ERANO DUE CATENE DELLA STESSA PAGINA.** Segnalazione dal sito vero su **1.4.0**: «A second operation was started on this context» aggiungendo una **sotto-sezione alle Separazioni** della vIPI di LIBB. Non un servizio iniettato male: un gesto chiama `OnChanged` → il ricarico parte e **cede** al primo `await`; il ridisegno che segue fa scattare `OnParametersSetAsync`, che ricarica **di nuovo**. Due catene, lo stesso `DbContext`, e chi arriva secondo muore portandosi via il circuito. ⚠️ **Non si risolve isolando altri servizi**: il pannello release **deve** condividere il contesto della pagina (il publish è un'operazione sola composta con `BeforePublishAsync`, e spezzarla manda in stallo — decisione già pagata, scritta in testa a `ReleasePanel`). Rimedio: un **tornello** nel guscio dei cinque editor — una operazione per volta, e ci passano sia i gesti sia i **caricamenti**, con memoria del flusso corrente perché le catene si annidano (un semaforo non rientrante si aspetterebbe da solo: un editor piantato è peggio di uno morto, sembra lentezza). 🔴 **E l'altra metà**: un guasto dentro un **circuito** non passa dal middleware delle richieste, quindi `diagnostica/` restava **vuota** — barra rossa a schermo e niente da leggere. `DiagnosticaCircuito` lo aggancia ai log del framework e lo scrive nel registro **con la fotografia delle collisioni**. ⚠️ La corsa **non si riproduce in locale** (su SQLite le query finiscono prima): i test provano che il tornello serializza e non si pianta, e il gesto vero è stato guidato a schermo. **Nessuna migrazione.** Suite: **5083 test**, E2E compresi (288). 🔴 Da consegnare come **1.4.1** (PATCH).

**Aggiornato:** 2 settembre 2026, sera — ✅ **1.4.0 È IN PRODUZIONE** (§A20). Caricata dal committente e verificata dall'esterno: `avvio-diagnostica.txt` dice **`1.4.0 · 669762f`**, ambiente Production, avvio alle **16:28:32 UTC** in 6,8 s, **nessun `avvio-errore.txt`**; in `avvii.txt` il precedente 1.3.0 si era spento in modo ordinato. **Otto controlli pubblici verdi** (`pacchetto-verifica.js` puntato su `atc.it.ivao.aero`): riconnessione servita e minificata, circuito Blazor avviato, quattro schede ACC, **Ricerca che risponde** — è quella che passa dal server — foglio di stile in vigore, console pulita. I **tre file di `wwwroot`** sono saliti col loro indice: serviti 200 con impronte nuove (`vipi-theme.css?v=60576a9c`, `vipi-ui.js?v=39e56d25`, `vipi-print.css?v=4fb663de`), e il `vipi-ui.js` servito **contiene il gestore del Tab** (18 315 byte, gli stessi del pacchetto). Avviso di simulazione visto su `/services/vsop/live` e `/services/vsop/airspace`. ⚠️ **Il pannello d'import non è verificabile da fuori**: sta nell'editor, che da anonimo non si raggiunge — da qui si è potuto provare solo che il CSS e il JS che lo fanno funzionare sono arrivati. 🔴 **Resta da guardare l'import dal vivo su produzione, da un account che può modificare.**

**Aggiornato:** 2 settembre 2026, sera — 📦 **PACCHETTO 1.4.0 PRONTO** (`vipi-1.4.0-solo-file-cambiati.zip`, sha256 `b8f680bc477c0f0683a5bf51729d186597de0ef8af2dfae74566092c75d7b6e4`, 4,28 MB, **21 file**). Timbro **`1.4.0 · 669762f`**. 🟢 **NESSUNA migrazione**: la prima consegna da tre che non tocca il database — niente copia di sicurezza da concordare, niente finestra. ⚠️ **Non è 1.3.1 e il numero è stato corretto**: una PATCH è «solo correzioni, nessuna pagina o sezione nuova», e §AU aggiunge un pannello, sei comandi e una porta d'ingresso ai documenti. Dentro: §AO §AP §AQ §AR §AS §AT §AU. Rispetto a 1.3.0 mancano **quattro file** apposta (`Vipi.Domain`, `Vipi.Infrastructure.MySqlMigrations`): non sono cambiati, e ogni `.dll` in più è una rinomina in più su un file che il processo tiene aperto. ⚠️ L'unico limite del ritorno indietro: una distanza di alternato scritta **con i decimali** non è leggibile da 1.3.0 (la tabella sparirebbe a schermo). **Provato sul pacchetto pubblicato**, non sul sorgente: avviato davvero, timbro giusto, **Ricerca risponde**, import e tasto Tab funzionanti **col JavaScript minificato**. Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.4.0.md`. 🔴 Resta da **caricare via FTP** e rifare i tre controlli su produzione.

**Aggiornato:** 2 settembre 2026, sera — 🟢 **FUSIONE: tutto in `main` (`fb2c6212`), nessun ramo aperto.** Dentro sono entrati `dati-scalo-solo-militare` (§AS, §AT) e `import-tabelle` (§AU). Misurato **sul risultato della fusione**, e di nuovo dopo la barra dell'import rimessa in riga: build Release verde sui due TFM (`--no-incremental`, 0 avvisi) e **suite intera verde su NOVE progetti — 5068 test, E2E compresi (276/276)**. ⚠️ Il verde si legge contando i progetti nel riepilogo, non dall'exit code. 🔴 Il pacchetto 1.3.1 sale a **sette** lavori (§AO §AP §AQ §AR §AS §AT §AU) e porta **tre** fogli di stile toccati; la nota di consegna deve dire che la distanza degli alternati è passata a `decimal` nel `BodyJson`.

**Aggiornato:** 2 settembre 2026 — **§AU: IMPORTARE UNA TABELLA** (ramo `import-tabelle`, **fuso in `main`**). Le tabelle dei documenti si compilavano una cella alla volta mentre **quindici SOP militari** le hanno già scritte in PDF: ora si incollano. Una pipeline sola a quattro stadi — acquisizione (`Griglia`: tabulazioni, CSV con le virgolette RFC 4180, Markdown, **tabella HTML**, larghezza fissa a righello, `.xlsx` letto **senza pacchetti**, ancore per il PDF) → mappatura (`SpecImport`) → proposta (`CostruttoreProposta` + `RisolutoreCelle` sui cataloghi) → **anteprima che si approva** (`ImportaTabella.razor`). Agganciata a: blocco tabella generico (tutti i documenti), Nominativi e Parcheggi, **Aeroporti alternati**; più l'**esportazione CSV** e il «**prendi la tabella da un altro documento**». `ClausePaste` smette di avere una grammatica sua e usa lo stesso primo stadio. ⚠️ **Nessuna entità, nessuna migrazione**: la distanza degli alternati passa a `decimal` **dentro il `BodyJson`** — ma un `72.2` letto da **binari vecchi** alza `JsonException` e `Leggi` restituisce **nessuna riga**, cioè la tabella sparisce a schermo: **un rollback dopo che qualcuno ha scritto un decimale è visibile all'utente**. Il registry delle specifiche è stato **tolto** (i titoli sono nella lingua di chi guarda: una lista statica sarebbe stata una seconda definizione degli stessi nomi). ✅ **Verificato dal vivo** anche l'incolla clausole dell'admin trasferimenti (`LIBB_ES_CTR ⇄ LAAA_CTR`): Markdown letto, intestazione saltata, avviso sul ricevente intatto, «EKMUR, PISIP» ancora una cella sola. ⚠️ Per arrivare al modulo servono quattro clic non ovvi: ACC → **controparte chiusa** → accordo → **sezione**, il cui corpo esiste solo da aperta (levetta `.xt-dirtoggle`, non un `<details>`) — a sezione chiusa il tasto c'è, è acceso e non apre niente. Carta: [`docs/design/piano-import-tabelle.md`](design/piano-import-tabelle.md). **4739 test verdi**, build Release verde sui due TFM.

**Aggiornato:** 2 settembre 2026, sera — **§AS e §AT, dal ramo `dati-scalo-solo-militare` (`7bef3c3e`), fusi in `main`.** Due segnalazioni del committente, nessuna entità e nessuna migrazione. **§AS:** su un campo **solo militare** il rimando «per cambiarli: editor dell'aeroporto» era un **giro chiuso** — quella pagina rimanda indietro qui, perché `EnsureDocumentAsync` rifiuterebbe di far nascere la vIPI civile — e livelli di transizione, colonne editoriali delle piste e collegamenti di frequenza non avevano **nessuna** porta di scrittura in tutto il sito. ⚠️ Lo stato c'era già (`CivilEdition`): mancava la **domanda**, e va fatta **uguale** a quella della pagina che rimanda indietro. Riparato di striscio anche il **meteo**, che prendeva la nota sbagliata su tutti e 26 i campi. **§AT:** l'avviso «tradotta a macchina» era un riquadro che si mangiava **un quarto della prima schermata**; ora è un gettone nella riga sotto il titolo, insieme all'avviso di simulazione e su una riga loro. ⚠️ Due trappole invisibili leggendo la pagina: `.doc-head` **sul foglio è nascosto** (serve la riga in `PrintMeta`, col testo per esteso), e **un `<p>` non può contenere un `<details>`** — il parser sposta il «?» fuori. 🔴 **Il pacchetto 1.3.1 ora porta SEI lavori**, e §AT tocca gli stessi due fogli di stile di §AO.

**Aggiornato:** 2 settembre 2026 — **§AR: LA PAGINA CHE SI BLOCCA IN SALVATAGGIO.** Segnalazione dal sito vero, sul gesto **«Fine modifica»**. ⚠️ **Non si riproduce in locale** (dieci giri con clic veri, log pulito): il difetto è una **corsa**, e su SQLite la finestra è di millisecondi — in produzione c'è MariaDB. Due buchi trovati leggendo quel gesto: `FinishEditingAsync` rileggeva il lock **fuori dal guardiano** (l'unico `await` scoperto della classe → eccezione che abbatte il circuito, pagina da ricaricare, lavoro già salvato), e il guardiano **non chiedeva il ridisegno alla fine** (badge inchiodato su «Salvataggio…» ed errore invisibile per i gesti nati in un componente figlio). Sotto c'è la corsa vera: `@inject IEditingService` prende il `DbContext` **del circuito** → sei pagine passano a `OwningComponentBase`. ⚠️ Due trappole silenziose nella conversione: un `public void Dispose()` non viene più chiamato, e una pagina `IAsyncDisposable` non riceve mai il Dispose che chiude lo scope. Cinque test sul guscio (quattro rossi prima) + guardia strutturale. **Nessuna entità, nessuna migrazione.**

**Aggiornato:** 1 settembre 2026 — **§AQ: I TITOLI DELLE SEZIONI SEGUONO LA LINGUA DEL DOCUMENTO.** Segnalazione del committente: un documento **bloccato in inglese** mostrava le testate in italiano. Non era la traduzione a mancare — era la **stampella**: i titoli delle sezioni di catalogo stanno scritti nel documento nella lingua che aveva alla NASCITA, e finora arrivavano in inglese **di rimbalzo**, perché sono segmenti e passavano dal traduttore. Bloccare la lingua **spegne** la traduzione, e la stampella è caduta. Ora i titoli si risolvono dal catalogo **dove si legge** (`TitoliDiCatalogo`), in tutte e cinque le famiglie, a ogni profondità e anche nell'editor — dove una sezione fissa **non si può rinominare a mano**, quindi non c'era nessun rimedio. ⚠️ Il catalogo vince anche sulla **memoria di traduzione**: è la resa DECISA contro quella plausibile, ed è quel che impedisce a «MRVA» di tornare «Minimum vectoring». ⚠️ La vLOA va nel verso opposto e **non ha una resa italiana**: letta in italiano il catalogo non impone niente e il titolo resta al traduttore. Nuova **R9** in `design/regole-lingua.md`. **Nessuna entità, nessuna migrazione.**

**Aggiornato:** 1 settembre 2026, fuso in `main` — **§AP: COERENZA COL SECTORFILE.** Due sorgenti indipendenti descrivono le stesse cose (API IVAO e sectorfile Aurora) e nessuno le confrontava: ora un giro ogni 24 ore legge `itfreq.frq`, `itap.ap` e `itrw.rw` e dice **dove non concordano** — 36 rilievi sui dati veri, fra cui due frequenze che divergono di **5 e 3 MHz** e dodici aeroporti con **designatori di pista diversi**. **Nessuna entità, nessuna migrazione.** 🔴 Tre decisioni la reggono: l'health check **ignora** l'area nuova (contarla = `/vsop/health` giallo per sempre), il confronto **non gira dentro la richiesta** (I/O di rete su un endpoint anonimo) e la prima slice è stata **misurare** — che ha ucciso il controllo sul QFU (115 falsi a 1°, zero a 5°) e aggiunto quattro filtri. Visibile come **scorciatoia** `/services/vsop/sectorfile` a chi è **Editor** e oltre (non allo staff di divisione: i rilievi parlano del contenuto dei documenti). ⚠️ Un difetto visto solo a schermo: la testata della tabella finiva **sotto** la prima riga — `sticky-head` dentro `.st-scroll` trasforma `top:62px` in uno spostamento in giù.

**Aggiornato:** 1 settembre 2026, fuso in `main` — **§AO: L'AVVISO DI SIMULAZIONE, OVUNQUE.** Richiesta del committente: «ONLY FOR SIMULATION: DO NOT USE FOR REAL LIFE NAVIGATION» in rosso e grassetto **sotto il titolo** di ogni documento pubblico (le cinque famiglie) più **vista live** e **spazi aerei**, e a **piè di ogni foglio stampato di ogni pagina del sito** — *«in tutti, nessuno escluso»*. Il testo vive in UN posto (`Components/SimDisclaimer.razor`, costante) e **non si traduce**: è un cartello, non prosa, ed è la nuova **R8** di `docs/design/regole-lingua.md`. ⚠️ **In stampa `.doc-head` è NASCOSTO** (`.print-meta + .doc-head`), quindi la riga che si vede a schermo sul foglio non esiste: l'avviso è **anche** in `PrintMeta`, e chi togliesse quella riga credendola un doppione la toglierebbe dalla prima pagina di ogni documento stampato. ⚠️ **E il `bottom` del piè di pagina NON PUÒ ESSERE NEGATIVO** (regola corretta il 1 settembre, sera, e due volte): `position:fixed` è l'unico modo che il CSS ha di ripetere una riga su ogni foglio (le margin box di `@page` non le implementa nessun browser), ma la parte che finisce **sotto** il bordo inferiore dell'area di pagina Chrome non la taglia — la **ridisegna in cima al foglio successivo**. Con `-4mm` l'avviso usciva **tagliato per il lungo fra due fogli** e il suo fondo bianco cancellava la prima riga del foglio dopo: **41 fogli su 50**. E non si ripara a millimetri — la scala al 90% sposta il muro: solo **`bottom:0`** regge a tutte e sei le stampe misurate. ⚠️ Il secondo muro è il difetto insidioso: sui cinque documenti la prima pagina l'avviso ce l'ha comunque da `PrintMeta`, quindi un valore sbagliato si vedrebbe **solo stampando un elenco o la home**. **Verificato dal vivo** generando i PDF veri con `printToPDF`: 10 pagine, **54 fogli, tutti con l'avviso**; contrasto 7,6:1 chiaro e 7,1:1 scuro. ⚠️ **Non verificati a schermo APP e vSOP militare**: il `vipi.db` di sviluppo non contiene nessun documento di quei due tipi (solo `Vipi` e `Vloa`) — li copre `AvvisoDiSimulazioneTests` sul sorgente, e il costrutto è identico a quello delle altre schermate, che sono state guidate. 🔴 **Da consegnare**: cambiano `vipi-theme.css` e `vipi-print.css`, quindi servono impronte nuove e un pacchetto (1.3.1). ⚠️ **§AO non ha una sezione sua in questo file**: la cronaca per intero sta in `history/rounds.md`, alla voce del 1 settembre.
**Aggiornato:** 1 settembre 2026, sera — **§AL: I VSOP MILITARI NON PUBBLICATI — VERIFICATO** (nessuna modifica al prodotto). Domanda del committente: i documenti non pubblicati si vedono solo con i permessi? **Sì**, misurato impersonando un utente senza permessi: elenco a 3 righe (le sole pubblicate), indirizzo diretto di una bozza → «No military vSOP published» con **zero sezioni**, `?as=draft` che **degrada**, `?as=rel:` di un'altra release **rifiutata**. ⚠️ «Con i permessi» qui vuol dire **Editor**, non staff di divisione. ⚠️ E per impersonare un livello più basso **va cambiato anche il VID**: 704798 è un **fondatore** e resta Admin qualunque posizione gli si dia — il primo tentativo ha misurato il livello sbagliato. Scritto nella skill `verifica-live`. · **§AK: GLI SPAZI AEREI CHIUDONO ALLO STAFF E PASSANO SOTTO LA VSOP** (stesso ramo). `/services/airspace` → **`/services/vsop/airspace`**, riservata allo **staff di divisione** e superiori («per ora», decisione del committente). Il cancello sta in **due sedi** come per il convertitore — la scheda dell'hub si **sposta** nella sezione dello staff, la pagina rifiuta chi scrive l'indirizzo a mano — e ⚠️ **rifiuta prima delle query**. ⚠️ Il vecchio indirizzo è stato **tolto** (decisione del committente): era il percorso con cui la mappa girava senza cancello, e risponde 404 come qualunque altro percorso inesistente. ⚠️ La scheda è marcata `shortcut`: sotto `/services/vsop/` la mappa ha smesso di essere un servizio a sé, come i vSOP militari, e il conto delle scorciatoie sale da uno a due **di proposito**. · **§AJ: «DA FARE» NON PARLAVA LA LINGUA DEL SITO** (stesso ramo). Verificata la segnalazione su `/services/vsop/tasks`: quattro cose fuori posto, una delle quali un difetto vero. L'elenco stava **nudo** sul fondo della pagina mentre «Da sistemare» rende le **stesse righe** dentro un `.panel`; «My tasks» era un `<details>` **senza una riga di CSS** (triangolino del browser); ⚠️ **la lavagna era BIANCA nel tema scuro** — `var(--paper, var(--on-brand))` con `--paper` **inesistente**, quindi vinceva il bianco del testo sul blu: schede illeggibili, stessa classe della legenda 3D del 22 agosto; e le due sezioni chiuse non avevano **nessun** segno d'apertura. ⚠️ **Era sfuggito perché `/tasks` non era in `sweep.js`** — e non sarebbe bastato aggiungercelo: la lavagna nasce chiusa. Ora lo script apre anche i titoli-maniglia `.sect-toggle` prima di misurare. · **BIBLIOTECA ALLEGATI: il tipo «PIV»** (§E10, stesso ramo). ⚠️ **Non si tocca il database**: gli enum di questo modello si scrivono in colonna come **stringhe** (`SetProviderClrType(string)`), la colonna è `TEXT` su SQLite e `varchar(32)` **senza vincolo di dominio** su MySQL — niente migrazione, il che dentro la finestra cieca conta doppio. Chip e tendina compaiono da soli (la pagina cicla `Enum.GetValues`); serviva solo la riga nelle risorse, che `SharedResourceIntegrityTests` pretende. Trovato e corretto **nel solo commento**: `Other` diceva di essere lo zero dell'enum e non lo è (lo è `Loa`). · **§AI: RADIOASSISTENZE** (stesso ramo). Chip per **tipo** costruiti dal **dato** (non da `TipiSuggeriti`, che è un elenco aperto: un chip fisso a zero prometterebbe un filtro che non filtra), e si **sommano** — «senza tipo» sta nello stesso insieme. Il modulo d'aggiunta sale **in cima** (149 righe da scorrere per aggiungerne una) e **chiede ogni volta**: una riga scritta a mano è nostra per sempre, mentre lo stesso impianto nel sectorfile arriva a tutti e si aggiorna da sé. ⚠️ La domanda la pone la pagina e non un `InlineConfirm` — quello non si apre da codice, e con l'Invio servirebbe un secondo meccanismo. Più intestazione appiccicata, **ordinamento a tre stati** (il terzo clic torna all'ordine dell'anagrafica; le frequenze si ordinano da numero), «N di M», testo cercato marcato col nuovo componente `<Marca>` e Invio che aggiunge rimettendo il fuoco nel campo. **8 test nuovi.** · **§AH: FRASEOLOGIA E TRADUZIONI IN UNA PAGINA SOLA** (stesso ramo). Le due pagine `admin/translations` e `admin/glossary` diventano una, a sezioni richiudibili e con **una** tendina di direzione; il vecchio indirizzo è una seconda rotta della stessa pagina e la barra admin passa da 17 a 16 voci. ⚠️ **La ricerca sta sul database**: il registro mostrava **cento righe su 176 senza dirlo**, e un filtro sulle righe caricate mentirebbe proprio quando la memoria è lunga. Nuovo **«dove si usa»** a due livelli (formula → frasi → documenti, col **dove**: prosa/tabella/titolo e il collegamento all'editor): ⚠️ il corpus editoriale si legge **una volta per lotto**, non una per riga. Più «N di M» + «carica altre», tastiera (Invio / Ctrl+Invio / Esc) e testo cercato **marcato**. **15 test nuovi**, Release 0 avvisi, suite verde. · **§AG: LA MAPPA BIANCA DEI CONFINANTI** — non era la basemap, era `vipi-boot.js` che guardava il DOM solo al primo render e alle navigazioni, mentre lì la mappa nasce da un render **interattivo**. · **§AF4: IL GIRO SULLA PAGINA STRUTTURA** (stesso ramo). Tre richieste del committente, guardando la pagina **in inglese**: la **finestra di eliminazione** parlava italiano *dentro* (la finestra passa dalle risorse, il **piano** lo scrive l'applicazione — `Messaggio.Lingua` era messo **a metà**: ~20 frasi di `DeletionRules`, il motivo di `SogliaEliminazione`, **tutti** i verdetti della sonda IVAO); **«Coverage hierarchy» e «Orphan sectors» ora si chiudono** dal titolo; il piede e il tetto di un ripiego si scrivono **anche in piedi** (tendina FL/ft come nei Trasferimenti) — prima una fascia da 2 500 piedi **non si poteva scrivere**. ⚠️ **L'unità non si salva**: due colonne sarebbero state una **seconda migrazione nella finestra cieca**, quindi alla rilettura si **deduce** e «FL30» rilegge «3 000 ft». ⚠️ **La cultura di questa macchina è inglese**: cinque test sono diventati rossi appena le frasi hanno avuto due versioni — vanno ancorati con `CulturaDiProva`. · **Aggiornato:** 31 agosto 2026, tarda notte — **§AF NUOVA: LA RICADUTA GUARDA ANCHE IN ALTO, E UN SETTORE NON È PIÙ NIPOTE DI SÉ STESSO** (ramo `ricaduta-verticale-e-cicli`, tre commit, **NON fuso**). Difetto **visto in produzione** su `atc.it.ivao.aero`: `LIMF_WW0_APP` compariva come radice **e** come proprio nipote. ⚠️ Nessuna guardia poteva vederlo, perché `EnsureNoCycle` guardava i soli padri **scritti** mentre tutti leggono `EffectiveParentCallsign` — e scegliere «eredita» **non passava da nessun controllo**. E non esplodeva: ogni lettore ha la sua guardia sui nodi visti, quindi la catena si **tronca in silenzio**. **0 anelli attivi** in sviluppo ma **19 aeroporti** pendono da una PROPRIA APP: erano 19 inneschi a un clic. Poi la **ricaduta verticale**: misurato che Milano è divisa a **FL325** e che i due alti stanno **uno sotto l'altro**, quindi con **WS5** chiuso il traffico alto d'ovest finiva su WS2, che sopra FL325 non ha niente. `SectorFallbacks` porta righe con **fascia di quota**, consultate **prima** del padre — che resta la **coda implicita**, e per questo la tabella nasce **vuota**: nessun travaso, nessun `Sql` in migrazione (vietato nella finestra cieca). ⚠️ **Migrazione additiva, due provider: quelle in coda diventano TRENTASETTE.** ⚠️ La **verifica dal vivo** ha trovato **quattro** difetti che i test non vedevano, fra cui **155 proposte** (mancava del tutto un criterio geografico) e una riga che *sembrava* vuota e alla riscrittura **si sarebbe mangiata il dato**. **56 test nuovi**, Release 0 avvisi, suite **4 734**. · **Aggiornato:** 31 agosto 2026, notte — ✅ **1.1.0 È IN PRODUZIONE**, caricato dal committente e **verificato dal vivo dall'esterno** (§A15): `/vsop/ping` risponde `204` — indirizzo che nel pacchetto `j` non esisteva — la Ricerca risponde («33 risultati per LI») e la console è pulita. ✅ **La chiave Microsoft è caricata** dal file dei segreti. ✅ **Il `.sql` del 30 era già dentro** (la vIPI di LIBB dice «Aeroporti 0»: è l'archivio nuovo). ⚠️ Restano il **KMZ degli spazi aerei da caricare** e `errori-richieste.txt` da cancellare. · **Aggiornato:** 31 agosto 2026 — 📦 **RAMO DI CONSEGNA `consegna-20260831`**: dentro ci sono la consegna del 30 (pacchetto `j`), **`intro-di-pagina`** e **§AE — la riconnessione del circuito**. **Nessuna migrazione** in nessuno dei due rami fusi, che dentro la finestra cieca è il punto. Il pacchetto per l'FTP è **incrementale** (solo i file cambiati) e la versione è **1.1.0**. ⚠️ Da qui in avanti `blazor.web.js` parte con `autostart="false"`: un caricamento senza `vipi-riconnessione.js` dà un sito che **si vede e non risponde**. · **Aggiornato:** 30 agosto 2026, **sera** — 🔴 **CONSEGNA A METÀ**: pacchetto `j` caricato via FTP, database `.sql` pronto e **in attesa che Ivao.It lo importi**; ⚠️ **nove commit sul ramo `consegna-db-20260830` DA SPINGERE**; ⚠️ la consegna **riparta da zero sul contenuto** (il `vipi.db` di sviluppo è intatto); 🔒 **dal 31 agosto al 16 settembre non si consegna database** e una migrazione girerebbe da sola in produzione — presidio in `MigrazioniDellaFinestraCiecaTests`, **da cancellare a finestra chiusa**. Tutto in **§A14** e nel riquadro di «Dove siamo». · **Aggiornato:** 30 agosto 2026, sera (**§AC — L'INTRO DI PAGINA** e **§AD — «USCIRE NON BUTTA VIA»**, ramo **`intro-di-pagina`** (da `main`), **spinto e NON fuso**, quattordici commit. Sezioni editabili in cima all'elenco dei vSOP militari, con i PDF della biblioteca: vivono in **`SharedBlocks`** — tabella dell'`InitialCreate` che **non usava nessuno** — quindi **ZERO migrazioni**, che dentro la finestra cieca è il punto. ⚠️ **Non è un documento**: niente ciclo AIRAC, niente release, quel che si salva è **subito pubblico**. Tradotta su richiesta del SOD, e la traduzione regge solo perché le frasi entrano nel **corpus**. Poi la verifica chiesta dal committente su **tutto ciò che si modifica**: i quattro editor documentali salvano **a ogni gesto** e sono sani; il difetto era sull'editor **aeroporto** — l'unico che accumula — dove «Fine modifica» usciva **senza guardare**, e su `AccAdminPage`. ⚠️ E la guardia `beforeunload` copriva **un buffer su tre**. **Release verde e suite verde su tutt'e due i TFM.**) · **Aggiornato:** 30 agosto 2026, pomeriggio (**§AB — LA SHAPE DI UN SETTORE HA UNA PORTA SOLA**, carta [refactor/15](refactor/15-shape-del-settore-una-porta-sola.md), **S0→S10 fuse in `main` e SPINTE** (merge `--no-ff`, quattordici commit; ramo cancellato). Un settore agganciato agli spazi aerei dell'AIP **disegnava** quel confine e **rivendicava** il traffico dentro il monoblocco di IVAO: sei motori, due lo sapevano. Adesso la forma — **anello E quote insieme** — si chiede a `ISectorShapeResolver`, e le quote stanno **DENTRO il pezzo**, così «laterale da una fonte, verticale da un'altra» non è una cosa da evitare ma una cosa che **non si può scrivere**. ⚠️ La **prova dal vivo**: agganciato `LIRR_EW_CTR` — in frequenza — alla FIR ROMA `GND→FL195`, in **un giro del poller** le tratte nuove sono uscite col timbro `Aip` e i quattro voli **sopra FL195** hanno smesso di essere rivendicati. ⚠️ **S11 resta fuori**: non è una slice, è la seconda metà della cura — otto siti di scrittura e il percorso del **congelamento di release** — e non si fa il giorno prima della consegna (§4-bis della carta). ⚠️ Migrazioni in coda: **TRENTASEI**) · **Aggiornato:** 30 agosto 2026 (**I DUE RAMI SONO FUSI IN `main`, CHE È STATO SPINTO** — `biblioteca-allegati` (§E10) e `spazi-aerei-aip` (§AA), in quest'ordine. Sette conflitti, e la regola era quasi sempre «servono tutti e due»: registrazioni DI, DbSet, voce nella barra admin, chiavi di traduzione. ⚠️ **L'eccezione non l'ha segnalata git**: i due rami dicevano tutti e due «16 voci nella barra admin» perché ognuno ne aggiungeva UNA a quindici — fuse sono **DUE**, quindi 17 e 12 — e git ha fuso due numeri identici senza chiamarlo conflitto. Se n'è accorto solo `AdminNavTests`. ⚠️ I due **ModelSnapshot** git li ha fusi da solo e stavolta bene: la prova non è che i nomi ci siano ma che una migrazione di prova esca **VUOTA**, e su tutti e due i provider lo è. **Release verde e quindici assiemi su quindici verdi DOPO la fusione.** ⚠️ Le migrazioni in coda al cutover MariaDB sono ora **TRENTAQUATTRO**. ✅ **R8 CHIUSO**: l'embed provato dal vivo con due PDF veri del committente — il documento si vede davvero nel riquadro. ⚠️ E ha trovato che in `frame-src` mancava **`'self'`**: l'iframe punta alla NOSTRA rotta, non a Drive, e col solo Drive il riquadro sarebbe rimasto vuoto al passaggio a CSP vera. ⚠️ Più un difetto in produzione: la colonna versione della biblioteca stampava `v@r.VersionNumber` alla lettera, che è la regola di Razor per gli indirizzi email) · **Aggiornato:** 29 agosto 2026, notte (**§E10 — LA BIBLIOTECA ALLEGATI, TUTTE E NOVE LE SLICE FATTE**, ramo
**Aggiornato:** 31 agosto 2026, **notte** — ✅ **1.3.0 È IN PRODUZIONE** (§A19), caricato dal committente e verificato dall'esterno: otto controlli verdi, e i due file statici scaricati dal sito sono **byte per byte identici** alle impronte consegnate (`vipi-theme.css` 176 853 B, `vipi-aor3d.js` 15 964 B) — che è anche la prova che `wwwroot` e l'indice sono saliti insieme. ⚠️ **Il primo giro della sonda era ROSSO sulla Ricerca, e il rosso era della SONDA**: lanciata subito dopo il riavvio, su un processo appena partito. Misurata a mano un minuto dopo, la Ricerca rispondeva in **746 ms con 50 risultati**. La sonda ora riprova una volta ricaricando la pagina — un falso rosso lì è ciò che fa tornare indietro una consegna sana. · **Aggiornato:** 31 agosto 2026, **notte tardi** — 📦 **PACCHETTO 1.3.0 PRONTO, DA CARICARE** (§A19): 22 file, zip 4,46 MB, sha256 `7435d225…`, timbro `1.3.0 · 1ade0db`. MINOR: porta §AN (lingua bloccata) con la sua **migrazione additiva** — una sola colonna, `Documents.LanguageLocked` — e le cinque correzioni della sera. 🔴 **La migrazione parte dentro la finestra cieca per decisione del committente**: si carica **finché c'è qualcuno raggiungibile**, con la copia di sicurezza fatta prima. ✅ Provato sul **pacchetto pubblicato** (dieci controlli verdi) e le due correzioni sui file statici cercate **dentro il minificato**. ⚠️ Due cose che un elenco fatto col solo `git diff` avrebbe sbagliato: `Vipi.Host.dll` ci va **anche senza una riga cambiata** (il timbro di versione è un suo `AssemblyMetadata`) e `Vipi.Hosting.dll` è **nuovo** rispetto a 1.2.0. · **Aggiornato:** 31 agosto 2026, **notte** — ✅ **§AM È CHIUSA PER INTERO, E IL VERDETTO È BUONO.** `avvii.txt` riletto alle 20:11 locali finisce all'avvio delle **14:50:04Z**: **nessuna riga ⚠ nuova**, **3h20:56** di vita contro le **3h01:56** della più lunga di 1.1.0 — §A18-bis e §AM3 chiuse. Nessun `errori-richieste.txt` e nessun `avvio-errore.txt` sul server: §AM4 chiusa senza niente da cancellare. E **§AM2**, l'unica che da fuori non si poteva chiudere, l'ha chiusa il committente **da collegato**: salvataggio dall'editor delle vIPI di LIBB, «A second operation was started» **non comparso**. ⚠️ Il verdetto sulla memoria regge sull'**assenza di un riavvio**, non su un carico misurato: prova positiva di vita solo fino alle 16:52Z (`neighbours-debug.log`). ✅ E in `main` cinque correzioni chieste a voce (merge `8fa5eaf5`): il 3D delle aree regolamentate col **nome** invece dell'id, la pagina degli spazi aerei che aveva **quindici classi senza una regola nel foglio**, i campi delle regole piste illeggibili a tema scuro (`--on-brand` come fondo di un campo), e l'anteprima di una release che mostrava le SID del ciclo di **oggi**. «Regole piste» nascondibile era già possibile: verificato dal vivo, zero codice. · **Aggiornato:** 31 agosto 2026, sera — ✅ **1.2.0 È IN PRODUZIONE** (§A18), caricato alle 14:46 UTC: migrazione girata (3 403 ms), nessun `avvio-errore.txt`, e **otto controlli dal vivo dall'esterno tutti verdi** — compresa la Ricerca, che è quella che distingue un sito vivo da uno mezzo caricato. 🔴 **MA IL VERDETTO SUL DIFETTO NON È ANCORA DATO** (§A18-bis): la prova è l'ASSENZA di una nuova riga «NON si è spento in modo ordinato» in `avvii.txt`, e va guardata **dopo le 19:48 locali** — prima di allora 1.2.0 non ha vissuto più a lungo del peggior 1.1.0. Era: pacchetto pronto, 20 file, da caricare stasera (§A18): porta una **migrazione**, e parte solo perché chi amministra il database può ancora ripristinare entro stasera — deroga a tempo, non fine della finestra cieca. Verificato sul pacchetto pubblicato: dieci controlli verdi, migrazione vista applicarsi, timbro `1.2.0 · 9d5d902`. ⚠️ La prova gira su SQLite: la gemella MySql non è stata vista girare su MariaDB vera · **Aggiornato:** 31 agosto 2026, pomeriggio — 🔴 **§AM: la diagnostica di produzione ha smentito §A16 in poche ore.** Tre errori nuovi e **due morti male del processo** (10:57 e 13:05). Quattro difetti, e ⚠️ **il primo era lo strumento di diagnosi**: `CollisioniDbContext` aggiungeva un riferimento a un elenco **a ogni query** e non lo potava mai. Gli altri tre: il registro allegava venti fotografie a ogni errore (634 kB per **tre** voci, su un tetto di 512), il pannello delle traduzioni caricava il documento **due volte a ogni ridisegno** dell'editor sul contesto del circuito, e la home moriva su una lettura che la barra sopra proteggeva gia'. ✅ E il **rimedio alla radice** è nello stesso giro (AM-E): il catalogo delle stazioni si legge **una volta per processo**, e la spinta di invalidazione è passata dai servizi — dove mancava in **sei metodi su sette** — a un intercettore sul salvataggio. Ramo `corse-e-perdita-diagnostica` da **`consegna-20260831`**, **23 test nuovi**, **nessuna migrazione** — consegnabile nella finestra cieca · **Aggiornato:** 31 agosto 2026, notte — ✅ **1.1.0 È IN PRODUZIONE**, caricato dal committente e **verificato dal vivo dall'esterno** (§A15): `/vsop/ping` risponde `204` — indirizzo che nel pacchetto `j` non esisteva — la Ricerca risponde («33 risultati per LI») e la console è pulita. ✅ **La chiave Microsoft è caricata** dal file dei segreti. ✅ **Il `.sql` del 30 era già dentro** (la vIPI di LIBB dice «Aeroporti 0»: è l'archivio nuovo). ⚠️ Restano il **KMZ degli spazi aerei da caricare** e `errori-richieste.txt` da cancellare. · **Aggiornato:** 31 agosto 2026 — 📦 **RAMO DI CONSEGNA `consegna-20260831`**: dentro ci sono la consegna del 30 (pacchetto `j`), **`intro-di-pagina`** e **§AE — la riconnessione del circuito**. **Nessuna migrazione** in nessuno dei due rami fusi, che dentro la finestra cieca è il punto. Il pacchetto per l'FTP è **incrementale** (solo i file cambiati) e la versione è **1.1.0**. ⚠️ Da qui in avanti `blazor.web.js` parte con `autostart="false"`: un caricamento senza `vipi-riconnessione.js` dà un sito che **si vede e non risponde**. · **Aggiornato:** 30 agosto 2026, **sera** — 🔴 **CONSEGNA A METÀ**: pacchetto `j` caricato via FTP, database `.sql` pronto e **in attesa che Ivao.It lo importi**; ⚠️ **nove commit sul ramo `consegna-db-20260830` DA SPINGERE**; ⚠️ la consegna **riparta da zero sul contenuto** (il `vipi.db` di sviluppo è intatto); 🔒 **dal 31 agosto al 16 settembre non si consegna database** e una migrazione girerebbe da sola in produzione — presidio in `MigrazioniDellaFinestraCiecaTests`, **da cancellare a finestra chiusa**. Tutto in **§A14** e nel riquadro di «Dove siamo». · **Aggiornato:** 30 agosto 2026, sera (**§AC — L'INTRO DI PAGINA** e **§AD — «USCIRE NON BUTTA VIA»**, ramo **`intro-di-pagina`** (da `main`), **spinto e NON fuso**, quattordici commit. Sezioni editabili in cima all'elenco dei vSOP militari, con i PDF della biblioteca: vivono in **`SharedBlocks`** — tabella dell'`InitialCreate` che **non usava nessuno** — quindi **ZERO migrazioni**, che dentro la finestra cieca è il punto. ⚠️ **Non è un documento**: niente ciclo AIRAC, niente release, quel che si salva è **subito pubblico**. Tradotta su richiesta del SOD, e la traduzione regge solo perché le frasi entrano nel **corpus**. Poi la verifica chiesta dal committente su **tutto ciò che si modifica**: i quattro editor documentali salvano **a ogni gesto** e sono sani; il difetto era sull'editor **aeroporto** — l'unico che accumula — dove «Fine modifica» usciva **senza guardare**, e su `AccAdminPage`. ⚠️ E la guardia `beforeunload` copriva **un buffer su tre**. **Release verde e suite verde su tutt'e due i TFM.**) · **Aggiornato:** 30 agosto 2026, pomeriggio (**§AB — LA SHAPE DI UN SETTORE HA UNA PORTA SOLA**, carta [refactor/15](refactor/15-shape-del-settore-una-porta-sola.md), **S0→S10 fuse in `main` e SPINTE** (merge `--no-ff`, quattordici commit; ramo cancellato). Un settore agganciato agli spazi aerei dell'AIP **disegnava** quel confine e **rivendicava** il traffico dentro il monoblocco di IVAO: sei motori, due lo sapevano. Adesso la forma — **anello E quote insieme** — si chiede a `ISectorShapeResolver`, e le quote stanno **DENTRO il pezzo**, così «laterale da una fonte, verticale da un'altra» non è una cosa da evitare ma una cosa che **non si può scrivere**. ⚠️ La **prova dal vivo**: agganciato `LIRR_EW_CTR` — in frequenza — alla FIR ROMA `GND→FL195`, in **un giro del poller** le tratte nuove sono uscite col timbro `Aip` e i quattro voli **sopra FL195** hanno smesso di essere rivendicati. ⚠️ **S11 resta fuori**: non è una slice, è la seconda metà della cura — otto siti di scrittura e il percorso del **congelamento di release** — e non si fa il giorno prima della consegna (§4-bis della carta). ⚠️ Migrazioni in coda: **TRENTASEI**) · **Aggiornato:** 30 agosto 2026 (**I DUE RAMI SONO FUSI IN `main`, CHE È STATO SPINTO** — `biblioteca-allegati` (§E10) e `spazi-aerei-aip` (§AA), in quest'ordine. Sette conflitti, e la regola era quasi sempre «servono tutti e due»: registrazioni DI, DbSet, voce nella barra admin, chiavi di traduzione. ⚠️ **L'eccezione non l'ha segnalata git**: i due rami dicevano tutti e due «16 voci nella barra admin» perché ognuno ne aggiungeva UNA a quindici — fuse sono **DUE**, quindi 17 e 12 — e git ha fuso due numeri identici senza chiamarlo conflitto. Se n'è accorto solo `AdminNavTests`. ⚠️ I due **ModelSnapshot** git li ha fusi da solo e stavolta bene: la prova non è che i nomi ci siano ma che una migrazione di prova esca **VUOTA**, e su tutti e due i provider lo è. **Release verde e quindici assiemi su quindici verdi DOPO la fusione.** ⚠️ Le migrazioni in coda al cutover MariaDB sono ora **TRENTAQUATTRO**. ✅ **R8 CHIUSO**: l'embed provato dal vivo con due PDF veri del committente — il documento si vede davvero nel riquadro. ⚠️ E ha trovato che in `frame-src` mancava **`'self'`**: l'iframe punta alla NOSTRA rotta, non a Drive, e col solo Drive il riquadro sarebbe rimasto vuoto al passaggio a CSP vera. ⚠️ Più un difetto in produzione: la colonna versione della biblioteca stampava `v@r.VersionNumber` alla lettera, che è la regola di Razor per gli indirizzi email) · **Aggiornato:** 29 agosto 2026, notte (**§E10 — LA BIBLIOTECA ALLEGATI, TUTTE E NOVE LE SLICE FATTE**, ramo
`biblioteca-allegati` **spinto e NON fuso**. I PDF non possono stare da noi — il piano di hosting non ammette
il formato, ed è un vincolo **contrattuale** — quindi stanno sul **Drive di divisione**, e da noi stanno
identità, organizzazione, versioni e il registro dei link. Il documento cita uno **slug** e passa da
`/vsop/files/{slug}`: **302** e ⚠️ **`no-cache`** (non `immutable` come le immagini — lì l'URL è il
contenuto, qui l'URL è stabile e il contenuto cambia sotto, che è il senso della sostituzione). Blocco
«Allegato» in **due modi**, Link e **Incorporato** (⚠️ senza `frame-src` in CSP l'incorporato funziona
**oggi** — l'intestazione è Report-Only — e muore in blocco al passaggio a CSP vera), più il link inline
`[testo](allegato:slug)`, che è l'**unico** che `MarkdownLite` riconosce. Il registro «chi cita cosa» si
**ricava** dai quattro posti, mai da una tabella di join; sostituzione ed eliminazione mostrano **quali**
documenti cambiano e aprono una riga nella **casella degli impatti** — ⚠️ **non** in «Cambiamenti», che è
pubblica e si ricava dal ciclo AIRAC. ⚠️ Enum **IN CODA**: nel payload di release sono ordinali. **Due
migrazioni**: quelle in coda diventano **VENTOTTO**. ✅ **R8 CHIUSO il 30 agosto**: l'embed provato dal vivo con due PDF veri, e ha trovato che in
`frame-src` mancava **`'self'`**) · **Aggiornato:** 29 agosto 2026 (**§Z — LA COLONNA DESTRA DELLE «QUOTE DI TRANSIZIONE»**, chiesta dal committente e **già in `main`** (`3b305831`, ramo cancellato): la tabella dei livelli ha un tetto di 420px — giusto, sono due colonne di numeri — e su una sezione da 822 lasciava **402px vuoti**, misurati a schermo. Fra sei proposte il committente ha scelto le due che usano dati **già in casa**: «**TL adesso**» (il verdetto sul QNH del METAR scritto grande, con l'ora del bollettino — ⚠️ **`noprint`**, come il meteo) e «**Dati del campo**» (elevazione in piedi e metri, variazione magnetica **con l'emisfero**, IATA, coordinate). ⚠️ I quattro dati il database **ce li aveva già** e nessuna pagina pubblica li mostrava: arrivano dall'anagrafica **in cache** (`AirportStation`, **zero query nuove**) e **non** dallo snapshot di release — non sono dati di release, e nello snapshot sarebbero **trattini** su ogni documento già pubblicato finché qualcuno non ripubblica; il vSOP militare li ha allo stesso prezzo. ⚠️ Le schede stanno al **centro** dell'altezza della tabella, non appese in alto (correzione del committente): il caso che contava è **una scheda sola**, campo senza METAR. **7 test nuovi, nessuna migrazione**, verifica dal vivo nei due temi. **Coda migrazioni invariata: VENTISEI**) · **Aggiornato:** 30 agosto 2026, notte (**TUTTO FUSO IN `main` — `e99b8e14`, spinto, NESSUN ramo con lavoro fuori.** Di §Y resta aperto il solo **BOAT**, che è del committente. ⚠️ Prima del prossimo deploy si rileggono **§Y10** — la migrazione delle radioassistenze **svuota e rifà** l'anagrafica al primo avvio, il **re-import piste** va premuto anche in produzione, le migrazioni in coda sono **VENTISEI** — e **«Dove siamo, in cinque righe»**, che porta le TRE cose da fare subito dopo il deploy) · **Aggiornato:** 30 agosto 2026, sera tardi (**§Y12 — il TASTO D'IMPORT e la FORMA delle sei tabelle**: «Importa dal sectorfile» sulla pagina Radioassistenze — ⚠️ un giro che **riscarica** la sorgente, non quello notturno che leggerebbe una copia vecchia fino a 24 ore — con l'esito in chip e le **tre risposte distinte** (policy, sorgente muta, quante righe). E la revisione della forma chiesta dal committente: ⚠️ il «rudimentale» era `cfg-table`, che **cabla le larghezze su quattro colonne**, usata da tabelle che ne hanno da tre a otto — la stessa diagnosi già pagata dalle SID. **30 test nuovi**, nessuna migrazione) · **Aggiornato:** 30 agosto 2026, sera (**§Y9 — «il nome del file non dice il tipo»**: l'osservazione del committente ha fatto cadere il modello dell'anagrafica, e sotto c'erano tre difetti — ⚠️ il **TACAN di Grosseto** non era mai arrivato in archivio e **17 NDB su 27** nemmeno, perché l'import passava dal catalogo dei punti, che toglie gli omonimi. Identità ora **codice + famiglia + canale**, tipo **editoriale**, tabella svuotata e rifatta: **149 righe** contro 132. Più la **pagina Radioassistenze** per gli Editor, unico posto da cui si elimina, con le due guardie. ⚠️ Migrazioni in coda: **VENTISEI**) · **Aggiornato:** 30 agosto 2026 (**§Y — TUTTE LE SLICE CHIUSE, verifica a schermo compresa**. Il payload di una sezione scende nei **figli** (venti su ventisei nel profilo militare) e non abita più «il primo blocco», che sulle sezioni piene di prosa si sarebbe perso al primo paragrafo scritto sopra. L'**indice** mostra le sotto-sezioni, in tutt'e due le navigazioni. E le **radioassistenze** sono diventate un'anagrafica di divisione: il documento dice quali cita e in che ordine, l'anagrafica dice quanto valgono, e la release **fotografa** la tabella. ⚠️ La sorgente quel dato **ce l'aveva già** — 128 VOR, 30 NDB, **26 col canale** — e il parser lo buttava via a ogni giro. ⚠️ La fonte vince: un campo che arriva dal sectorfile **non ha nemmeno la casella**. Gli **aeroporti alternati** citano due cataloghi diversi e si portano dietro il nome dello scalo, perché un alternato è spesso **estero** e una pagina pubblica non può chiamare IVAO per stampare una cella. E le **coordinate delle soglie pista** entrano in archivio: ⚠️ il difetto vero non era l'import ma il **salvataggio**, che cancella e riscrive le righe e se le sarebbe portate via al primo tocco di una colonna editoriale. **114 test nuovi**, quattro migrazioni: quelle in coda diventano **VENTICINQUE**. E la **verifica su LIMN Cameri** — campo misto, per la terza volta quello che trova le cose vere — ha preso **tre difetti** che i test verdi non vedevano: «Aggiungi riga» che non aggiungeva niente, la tabella delle aree indietro di una scelta, e ⚠️ un **500 sull'intero editor** perché `TryGetProperty` su un array alza `InvalidOperationException`, che **non è una `JsonException`** e passava indenne il `catch`. **149 test nuovi**, quattro migrazioni: quelle in coda diventano **VENTICINQUE**. Restano il **BOAT** (ritirato dal committente), il **re-import** delle soglie e la fusione del ramo) · **Aggiornato:** 29 agosto 2026, notte fonda (**§Y NUOVA — LE TABELLE DEL vSOP MILITARE**. Otto richieste del committente: sette sezioni oggi a prosa libera diventano **tabelle** — radioassistenze, aeroporti alternati, nominativi, parcheggi, coordinate soglia pista, attività delle aree — e l'indice impara a mostrare le **sotto-sezioni**. L'ottava (il BOAT) è stata **ritirata dal committente**. ✅ **Y0 chiusa**: il payload di una sezione cercava `ParentSectionId == null`, cioè **solo le radici**, e nel profilo militare venti sezioni su ventisei sono figlie — è la **terza** volta che la stessa assunzione si presenta con un vestito diverso. Chiuso insieme a un difetto latente: il payload nel «primo blocco» si sarebbe perso al primo paragrafo scritto sopra la tabella. ⚠️ **Misurato sul filo**: IVAO manda **lat/lon ed elevazione di ogni soglia pista** e noi ne mappiamo quattro campi su otto → **una migrazione (le in coda diventano VENTIQUATTRO) e un re-import**. ⚠️ E la sorgente ha **già** frequenza e canale delle radioassistenze — `MNL - CH 99Y (115.25)` è alla lettera una riga di `itvor.vor`, e il parser la butta via. ⚠️ **Y3**: sarebbe il **terzo ramo impilato**, decisione del committente) · **Aggiornato:** 29 agosto 2026, notte fonda (**§X — L'EDIZIONE GIUSTA PER IL CAMPO, e i vSOP militari raggiungibili**. La regola che finora stava solo nella testa di chi scrive: su un campo **solo militare** la vIPI civile **non nasce**, su un campo **misto** il vSOP militare nasce **solo dopo** la civile (basta che esista, anche in bozza). Due guardie gemelle **nei servizi**, non nelle tendine — `EnsureDocumentAsync` è chiamato dall'APERTURA dell'editor, quindi bastava l'URL — e bloccano la **nascita**, non l'apertura. Il viewer militare era l'**unico dei cinque senza `doc-layout`**. E i militari erano difficili da raggiungere in quattro punti: il ponte al civile gated su «pubblicata» invece che su «esiste», il filtro «Tipo» delle Versioni senza la loro voce, la pagina di una ACC con tre famiglie invece di quattro, l'hub `/services` che non li nominava (⚠️ **ribalta §5 della carta militare**, con la scheda marcata `shortcut` per non cancellare la regola). ⚠️ La **verifica sui duplicati** chiesta dal committente ha trovato un buco vero: le due porte che creano una vLOA confrontavano cose diverse — la **coppia di ACC** una, i **SectorId** l'altra — e nascevano **due vLOA sulla stessa coppia**, di cui una invisibile. **19 test nuovi**, nessuna migrazione, **3 762** su net8. ⚠️ **X4**: `Vipi.E2E.Tests` non compilato, il Host era acceso. ⚠️ **X5**: verifica a schermo da fare. ⚠️ **X7**: **LIML** ha un vSOP militare pubblicato e nessuna vIPI civile — dato di prima della guardia, ora si vede ma va creata a mano) · **Aggiornato:** 29 agosto 2026, notte tarda (**§W NUOVA: il convertitore di coordinate**, un servizio nuovo in `/services` per lo staff di divisione. Tredici forme di coordinate in ingresso — KML/KMZ compresi — e le due uscite chieste, col sectorfile in **due forme**: l'elenco dei punti (default) e i segmenti. ⚠️ Il DB elenca **VERTICI**, i segmenti elencano **LATI**. Nessun modello nuovo e nessun motore nuovo: il DMS e l'ordine lat/lon del JSON **traslocano** in `Vipi.Application/Coordinates` e l'infrastruttura delega; la mappa è quella dell'AoR. **Nessuna migrazione**. ⚠️ La **verifica dal vivo** ha trovato **cinque** difetti che la suite non vedeva. Dieci slice, **143 test nuovi**, suite **3 984** su nove progetti. ⚠️ Ramo `convertitore-coordinate` **NON fuso**) · **Aggiornato:** 29 agosto 2026, notte (**§V: TREDICI voci, tredici chiuse.** Dieci le ha trovate la lettura,
**Aggiornato:** 29 agosto 2026, notte (**§AA — GLI SPAZI AEREI DELL'AIP, TUTTE E SETTE LE SLICE FATTE E VERIFICATE DAL VIVO**, ramo `spazi-aerei-aip` ✅ **fuso in `main` il 30 agosto** (`86576ecc`). Il committente ha portato il KMZ dell'AIP perché **LIBA** e **LICC** controllano esattamente il CTR e non il monoblocco di IVAO. ⚠️ **Il file non contiene contorni, contiene SCATOLE 3D**: tetto, pavimento e una parete per lato — 26 989 poligoni per 1 536 volumi, e `TMA MILANO Z1` da sola ne ha 147. La regola (poligoni a **una sola quota**, dedup dell'anello 2D) dà **un anello per volume, sempre**. ⚠️ **Correzione alla carta, §6-bis**: l'aggancio **non si scrive** in `RegionMapPolygon` come diceva la prima stesura — quella colonna tiene UN anello e `ParsePoints` scende su `items[0]`, quindi le sette zone di Catania sarebbero diventate **una**, disegnata benissimo. Vive in una tabella sua e si legge dove l'AoR si costruisce, dove i poligoni sono già una **lista**; confinanti, attribuzione del traffico e vLOA restano sulla forma di IVAO. ⚠️ L'aggancio cita la **chiave naturale**, non l'id: un file nuovo rifà tutte le righe. **Verifica dal vivo**: KMZ da 1,3 MB letto in **3,2 s**, 1 536 letti / 362 utilizzabili / 3 doppioni, le sette zone di Catania agganciate a `LICC_APP`, e la vIPI pubblica disegna **sette anelli**; ha trovato un difetto — il **ciclo AIRAC digitato non si salvava**, perché `@bind` scrive al fuoco perso e il gesto dopo apre una finestra di sistema. **104 test nuovi, quattro migrazioni**: quelle in coda diventano **TRENTAQUATTRO**. Restano le altre quattro: **S4** l'ATZ per le torri — **13 torri su 84** hanno un confine vero invece di un cerchio, e ⚠️ un ICAO con **più di un ATZ si salta** perché la colonna tiene un anello — **S5** la pagina pubblica `/services/airspace`, **S6** il convertitore che pesca dal catalogo, **S7** il rapporto radioassistenze che **segnala e basta**. ⚠️ La verifica ha trovato un secondo difetto: il rapporto dava **54 «canale diverso»** ma **49 erano lacune** e solo **5** discordanze vere — ora sono due codici distinti e l'ordine delle voci è la **gravità**. Resta la **FUSIONE**, che è una decisione del committente) · **Aggiornato:** 29 agosto 2026 (**§Z — LA COLONNA DESTRA DELLE «QUOTE DI TRANSIZIONE»**, chiesta dal committente e **già in `main`** (`3b305831`, ramo cancellato): la tabella dei livelli ha un tetto di 420px — giusto, sono due colonne di numeri — e su una sezione da 822 lasciava **402px vuoti**, misurati a schermo. Fra sei proposte il committente ha scelto le due che usano dati **già in casa**: «**TL adesso**» (il verdetto sul QNH del METAR scritto grande, con l'ora del bollettino — ⚠️ **`noprint`**, come il meteo) e «**Dati del campo**» (elevazione in piedi e metri, variazione magnetica **con l'emisfero**, IATA, coordinate). ⚠️ I quattro dati il database **ce li aveva già** e nessuna pagina pubblica li mostrava: arrivano dall'anagrafica **in cache** (`AirportStation`, **zero query nuove**) e **non** dallo snapshot di release — non sono dati di release, e nello snapshot sarebbero **trattini** su ogni documento già pubblicato finché qualcuno non ripubblica; il vSOP militare li ha allo stesso prezzo. ⚠️ Le schede stanno al **centro** dell'altezza della tabella, non appese in alto (correzione del committente): il caso che contava è **una scheda sola**, campo senza METAR. **7 test nuovi, nessuna migrazione**, verifica dal vivo nei due temi. **Coda migrazioni invariata: VENTISEI**) · **Aggiornato:** 30 agosto 2026, notte (**TUTTO FUSO IN `main` — `e99b8e14`, spinto, NESSUN ramo con lavoro fuori.** Di §Y resta aperto il solo **BOAT**, che è del committente. ⚠️ Prima del prossimo deploy si rileggono **§Y10** — la migrazione delle radioassistenze **svuota e rifà** l'anagrafica al primo avvio, il **re-import piste** va premuto anche in produzione, le migrazioni in coda sono **VENTISEI** — e **«Dove siamo, in cinque righe»**, che porta le TRE cose da fare subito dopo il deploy) · **Aggiornato:** 30 agosto 2026, sera tardi (**§Y12 — il TASTO D'IMPORT e la FORMA delle sei tabelle**: «Importa dal sectorfile» sulla pagina Radioassistenze — ⚠️ un giro che **riscarica** la sorgente, non quello notturno che leggerebbe una copia vecchia fino a 24 ore — con l'esito in chip e le **tre risposte distinte** (policy, sorgente muta, quante righe). E la revisione della forma chiesta dal committente: ⚠️ il «rudimentale» era `cfg-table`, che **cabla le larghezze su quattro colonne**, usata da tabelle che ne hanno da tre a otto — la stessa diagnosi già pagata dalle SID. **30 test nuovi**, nessuna migrazione) · **Aggiornato:** 30 agosto 2026, sera (**§Y9 — «il nome del file non dice il tipo»**: l'osservazione del committente ha fatto cadere il modello dell'anagrafica, e sotto c'erano tre difetti — ⚠️ il **TACAN di Grosseto** non era mai arrivato in archivio e **17 NDB su 27** nemmeno, perché l'import passava dal catalogo dei punti, che toglie gli omonimi. Identità ora **codice + famiglia + canale**, tipo **editoriale**, tabella svuotata e rifatta: **149 righe** contro 132. Più la **pagina Radioassistenze** per gli Editor, unico posto da cui si elimina, con le due guardie. ⚠️ Migrazioni in coda: **VENTISEI**) · **Aggiornato:** 30 agosto 2026 (**§Y — TUTTE LE SLICE CHIUSE, verifica a schermo compresa**. Il payload di una sezione scende nei **figli** (venti su ventisei nel profilo militare) e non abita più «il primo blocco», che sulle sezioni piene di prosa si sarebbe perso al primo paragrafo scritto sopra. L'**indice** mostra le sotto-sezioni, in tutt'e due le navigazioni. E le **radioassistenze** sono diventate un'anagrafica di divisione: il documento dice quali cita e in che ordine, l'anagrafica dice quanto valgono, e la release **fotografa** la tabella. ⚠️ La sorgente quel dato **ce l'aveva già** — 128 VOR, 30 NDB, **26 col canale** — e il parser lo buttava via a ogni giro. ⚠️ La fonte vince: un campo che arriva dal sectorfile **non ha nemmeno la casella**. Gli **aeroporti alternati** citano due cataloghi diversi e si portano dietro il nome dello scalo, perché un alternato è spesso **estero** e una pagina pubblica non può chiamare IVAO per stampare una cella. E le **coordinate delle soglie pista** entrano in archivio: ⚠️ il difetto vero non era l'import ma il **salvataggio**, che cancella e riscrive le righe e se le sarebbe portate via al primo tocco di una colonna editoriale. **114 test nuovi**, quattro migrazioni: quelle in coda diventano **VENTICINQUE**. E la **verifica su LIMN Cameri** — campo misto, per la terza volta quello che trova le cose vere — ha preso **tre difetti** che i test verdi non vedevano: «Aggiungi riga» che non aggiungeva niente, la tabella delle aree indietro di una scelta, e ⚠️ un **500 sull'intero editor** perché `TryGetProperty` su un array alza `InvalidOperationException`, che **non è una `JsonException`** e passava indenne il `catch`. **149 test nuovi**, quattro migrazioni: quelle in coda diventano **VENTICINQUE**. Restano il **BOAT** (ritirato dal committente), il **re-import** delle soglie e la fusione del ramo) · **Aggiornato:** 29 agosto 2026, notte fonda (**§Y NUOVA — LE TABELLE DEL vSOP MILITARE**. Otto richieste del committente: sette sezioni oggi a prosa libera diventano **tabelle** — radioassistenze, aeroporti alternati, nominativi, parcheggi, coordinate soglia pista, attività delle aree — e l'indice impara a mostrare le **sotto-sezioni**. L'ottava (il BOAT) è stata **ritirata dal committente**. ✅ **Y0 chiusa**: il payload di una sezione cercava `ParentSectionId == null`, cioè **solo le radici**, e nel profilo militare venti sezioni su ventisei sono figlie — è la **terza** volta che la stessa assunzione si presenta con un vestito diverso. Chiuso insieme a un difetto latente: il payload nel «primo blocco» si sarebbe perso al primo paragrafo scritto sopra la tabella. ⚠️ **Misurato sul filo**: IVAO manda **lat/lon ed elevazione di ogni soglia pista** e noi ne mappiamo quattro campi su otto → **una migrazione (le in coda diventano VENTIQUATTRO) e un re-import**. ⚠️ E la sorgente ha **già** frequenza e canale delle radioassistenze — `MNL - CH 99Y (115.25)` è alla lettera una riga di `itvor.vor`, e il parser la butta via. ⚠️ **Y3**: sarebbe il **terzo ramo impilato**, decisione del committente) · **Aggiornato:** 29 agosto 2026, notte fonda (**§X — L'EDIZIONE GIUSTA PER IL CAMPO, e i vSOP militari raggiungibili**. La regola che finora stava solo nella testa di chi scrive: su un campo **solo militare** la vIPI civile **non nasce**, su un campo **misto** il vSOP militare nasce **solo dopo** la civile (basta che esista, anche in bozza). Due guardie gemelle **nei servizi**, non nelle tendine — `EnsureDocumentAsync` è chiamato dall'APERTURA dell'editor, quindi bastava l'URL — e bloccano la **nascita**, non l'apertura. Il viewer militare era l'**unico dei cinque senza `doc-layout`**. E i militari erano difficili da raggiungere in quattro punti: il ponte al civile gated su «pubblicata» invece che su «esiste», il filtro «Tipo» delle Versioni senza la loro voce, la pagina di una ACC con tre famiglie invece di quattro, l'hub `/services` che non li nominava (⚠️ **ribalta §5 della carta militare**, con la scheda marcata `shortcut` per non cancellare la regola). ⚠️ La **verifica sui duplicati** chiesta dal committente ha trovato un buco vero: le due porte che creano una vLOA confrontavano cose diverse — la **coppia di ACC** una, i **SectorId** l'altra — e nascevano **due vLOA sulla stessa coppia**, di cui una invisibile. **19 test nuovi**, nessuna migrazione, **3 762** su net8. ⚠️ **X4**: `Vipi.E2E.Tests` non compilato, il Host era acceso. ⚠️ **X5**: verifica a schermo da fare. ⚠️ **X7**: **LIML** ha un vSOP militare pubblicato e nessuna vIPI civile — dato di prima della guardia, ora si vede ma va creata a mano) · **Aggiornato:** 29 agosto 2026, notte tarda (**§W NUOVA: il convertitore di coordinate**, un servizio nuovo in `/services` per lo staff di divisione. Tredici forme di coordinate in ingresso — KML/KMZ compresi — e le due uscite chieste, col sectorfile in **due forme**: l'elenco dei punti (default) e i segmenti. ⚠️ Il DB elenca **VERTICI**, i segmenti elencano **LATI**. Nessun modello nuovo e nessun motore nuovo: il DMS e l'ordine lat/lon del JSON **traslocano** in `Vipi.Application/Coordinates` e l'infrastruttura delega; la mappa è quella dell'AoR. **Nessuna migrazione**. ⚠️ La **verifica dal vivo** ha trovato **cinque** difetti che la suite non vedeva. Dieci slice, **143 test nuovi**, suite **3 984** su nove progetti. ⚠️ Ramo `convertitore-coordinate` **NON fuso**) · **Aggiornato:** 29 agosto 2026, notte (**§V: TREDICI voci, tredici chiuse.** Dieci le ha trovate la lettura,
TRE la verifica a schermo su un campo MISTO (§V1, chiusa) — e le tre dello schermo sono le peggiori: **un vSOP
militare pubblicato non si apriva affatto** (il bersaglio di release non si risolveva sul legame militare),
**tre tabelle su tre erano titoli vuoti** (il corpo derivato lo disegnavano solo le sezioni radice) e **il
catalogo non trovava le sezioni annidate** (venti su ventisei date per libere). Nuova **§V4**: «Regole piste»
→ *Slope rules* nella vIPI CIVILE. Suite **3 841**, nove progetti su nove) · **Aggiornato:** 29 agosto 2026, sera (**§V NUOVA: la supervisione dei vSOP militari**, dieci voci trovate
e dieci chiuse — il documento militare era agganciato al motore di **lettura** e non a quello di **governo**:
nessun congelamento della release e vista che leggeva lo snapshot **civile**, documento **invisibile** ai tre
elenchi generici per un `.Include` mancante, impatti che non avvisavano il gemello, eliminazione che lo
lasciava orfano, badge pilota/ATC **mai reso da nessuna pagina**. Restano aperte **V1** — rifare la verifica a
schermo su un campo **misto**, non su Rivolto che è solo militare — **V2** e **V3**. Suite **3 570** su net8,
otto progetti) · **Aggiornato:** 29 agosto 2026 (**§U CHIUSA E FUSA IN `main`** — merge `8d14b499`, ramo cancellato. Le **autorizzazioni a livelli**: `IsAdmin` (160 usi su 46 file) diventa un enum ORDINATO a cinque livelli **cumulativi**, l'Editor edita **TUTTO** e le concessioni per ACC sono **eliminate** (219 riferimenti). Otto slice, **86 test nuovi**, tre migrazioni. ⚠️ La **verifica live** su cinque identità ha trovato **tre difetti** che la suite non vedeva — un 500 sulla pagina Struttura per un Editor, due pagine senza cancello, e due falsi allarmi della sonda. ✅ **DEPLOY annunciato** il 30-ago-2026: gli `IT-` fuori dagli otto codici di direzione smettono di editare. ⚠️ Migrazioni in coda: **VENTITRÉ****) · **Aggiornato:** 28 agosto 2026, notte (**§U APERTA — LE AUTORIZZAZIONI A LIVELLI. L'interruttore unico `IsAdmin` (160 usi su 46 file) diventa un numero ordinato a cinque livelli: User, IvaoStaff, DivisionStaff, Editor, Admin, **cumulativi**. Sei decisioni del committente, fra cui l'Editor che edita **tutto** (non la sola ACC) e le concessioni per ACC **eliminate** — il che toglie dal layout `HasAnyGrantAsync`, cioè la prima query di ogni pagina e la causa prima delle corse sul DbContext. Carta approvata: `docs/feature/2026-08-28-autorizzazioni-a-livelli.md`. Ramo `autorizzazioni-a-livelli`, **codice da scrivere**. ⚠️ Le migrazioni in coda diventano VENTIDUE**) · **Aggiornato:** 28 agosto 2026, sera (**§Q3 CHIUSA — il glossario di fraseologia. La fraseologia DENTRO le frasi, che la memoria di traduzione non poteva coprire perché è indicizzata per segmento intero: segnaposto `<g>` col contratto OPPOSTO a `<x>`, tabella `GlossaryTerms`, pagina di cura. E la risposta del committente alla domanda che teneva aperta la voce: lo curano TUTTI GLI ADMIN, cioè tutto lo staff di divisione — nessun codice, la pagina era già dietro `IsAdmin`. Ramo **`glossario-fraseologia`** (`ee5fad7`, `f2ee4eb`), ✅ **FUSO in `main` la notte del 28 agosto** (`332f881`), build Release verde su entrambi i TFM dopo la fusione. ⚠️ Le migrazioni in coda diventano VENTUNO**) · 28 agosto 2026, notte (**I DUE RAMI SONO FUSI IN `main` E SPINTI**: `bilingue-tutte-le-pagine` — la lingua per intero, §Q-bis — e `archivio-atc-mondiale` — §S e §T. I due non si toccavano: un conflitto solo, in questo file. Sul codice tutto verde su net8+net10 DOPO la fusione, che è l'unico momento in cui la prova conta**) · **Aggiornato:** 28 agosto 2026, sera (**§Q-bis — LA LINGUA. Il bilingue girava su DUE viewer su cinque e la barra non aveva nessun comando per cambiare lingua: entrambe le cose chiuse, più le REGOLE su carta (`docs/design/regole-lingua.md`), i messaggi del backend, il catalogo della Guida, «MRVA» e il correttore delle traduzioni dentro l'editor. Sei commit nel ramo **`bilingue-tutte-le-pagine`** (`2af3a39`), spinto e NON fuso**) · **Aggiornato:** 28 agosto 2026, pomeriggio (**I DUE ROSSI INTERMITTENTI SONO CHIUSI — Q5 e Q6, riprodotti prima di correggerli: erano due contese diverse fra test paralleli. Q5 era il file di diagnostica dell'avvio, uno solo per processo, riscritto da un altro host nella finestra fra scrittura e rilettura; Q6 era un test che passava per via del POOL di SQLite invece che per via dell'interceptor, e che un `ClearAllPools()` di processo chiamato da un altro test faceva cadere. Ora «tutto verde» si può leggere alla lettera: 6633 verdi su entrambi i TFM**) · **Aggiornato:** 28 agosto 2026, mattina (**§Q e §R — i documenti bilingue e i vSOP militari, VENTI slice chiuse in due giorni e tutte in `main`, che è stato SPINTO (`6644b5e`): `origin/main` allineato, `origin/basemap-esri` cancellato perché ormai contenuto in main. Restano aperte Q1/Q2/Q3 (che dipendono da qualcun altro), Q4 (la vLOA che deve smettere di nascere inglese), Q5 e Q6 (due rossi intermittenti) e R1 (le figure dei SOP, lavoro manuale)**) · **Aggiornato:** 27 agosto 2026, notte (**§P — il ramo `basemap-esri`, spinto e NON fuso: il fondo delle mappe non è più CARTO (§N3 chiusa) e le chip METAR/TAF e pista SID, che non facevano niente. Resta P3, come fonderlo; e due decisioni: le tessere nostre, e l'attribuzione mancante nel 3D**) · **Aggiornato:** 27 agosto 2026, sera tardi (**§O — l'audit delle prestazioni, chiuso lo stesso giorno e fuso in `main` `8e5f640` insieme a `riordino-e-aree`: prima visita 336 → 113 KB, avvio 465 → 153 query. DUE interventi scartati su misura. Restano O1 (le query degli orfani), O2 e O3, che sono del committente**) · **Aggiornato:** 27 agosto 2026, pomeriggio (**§M — i resti: C7a/b/c, C6, H3, H1 e E9 chiusi lato codice in · **Aggiornato:** 28 agosto 2026, sera tardi (**§T — la SELECT dei duplicati su `DocReleases`: NON era eseguibile (il 3306 del server è su localhost, niente pannello), quindi ora la fa l'applicazione prima di `Migrate()` e nomina le righe in `avvio-errore.txt`; gli altri quattro indici unici della coda non possono fallire, verificato**) · **Aggiornato:** 28 agosto 2026, sera (**§S — l'archivio ATC mondiale, ramo `archivio-atc-mondiale` (il codice è tutto in `0ced074`, il resto sono documenti) SPINTO e NON fuso: il poller archivia tutte le postazioni ATC aperte, i conti della divisione non cambiano, ritenzione dodici mesi per tutto. S1 CHIUSA (il volume del mondo è misurato: 10×-14× le sessioni italiane, il database intero va a ~230 MB su MariaDB a regime) e S2 DECISA (si ricomincia da capo, raccolta dal 1° settembre 2026, cutover dal Worker nel 2027). ⚠️ Le migrazioni in coda al cutover MariaDB diventano VENTI**) · **Aggiornato:** 28 agosto 2026, pomeriggio (**I DUE ROSSI INTERMITTENTI SONO CHIUSI — Q5 e Q6, riprodotti prima di correggerli: erano due contese diverse fra test paralleli. Q5 era il file di diagnostica dell'avvio, uno solo per processo, riscritto da un altro host nella finestra fra scrittura e rilettura; Q6 era un test che passava per via del POOL di SQLite invece che per via dell'interceptor, e che un `ClearAllPools()` di processo chiamato da un altro test faceva cadere. Ora «tutto verde» si può leggere alla lettera: 6633 verdi su entrambi i TFM**) · **Aggiornato:** 28 agosto 2026, mattina (**§Q e §R — i documenti bilingue e i vSOP militari, VENTI slice chiuse in due giorni e tutte in `main`, che è stato SPINTO (`6644b5e`): `origin/main` allineato, `origin/basemap-esri` cancellato perché ormai contenuto in main. Restano aperte Q1/Q2/Q3 (che dipendono da qualcun altro), Q4 (la vLOA che deve smettere di nascere inglese), Q5 e Q6 (due rossi intermittenti) e R1 (le figure dei SOP, lavoro manuale)**) · **Aggiornato:** 27 agosto 2026, notte (**§P — il ramo `basemap-esri`, spinto e NON fuso: il fondo delle mappe non è più CARTO (§N3 chiusa) e le chip METAR/TAF e pista SID, che non facevano niente. Resta P3, come fonderlo; e due decisioni: le tessere nostre, e l'attribuzione mancante nel 3D**) · **Aggiornato:** 27 agosto 2026, sera tardi (**§O — l'audit delle prestazioni, chiuso lo stesso giorno e fuso in `main` `8e5f640` insieme a `riordino-e-aree`: prima visita 336 → 113 KB, avvio 465 → 153 query. DUE interventi scartati su misura. Restano O1 (le query degli orfani), O2 e O3, che sono del committente**) · **Aggiornato:** 27 agosto 2026, pomeriggio (**§M — i resti: C7a/b/c, C6, H3, H1 e E9 chiusi lato codice in
un giro solo. Restano aperte solo voci che dipendono da qualcun altro: le risposte di Ivao.It (A9/A13), le
quattro vLOA da ripubblicare (L2), il documento di Brindisi (B10-bis) e le decisioni di contenuto**) · **Aggiornato:** 27 agosto 2026, notte (**§K chiusa tutta: la vIPI d'aeroporto entra nel catalogo delle sezioni
— ramo `aeroporto-a-sezioni`, il TERZO in fila — più le tre rifiniture della stessa notte: il meteo tornato
nella pagina pubblica, i pannelli larghi quanto la colonna, e «Validità e revisione» che porta ciclo AIRAC,
data e chi ha premuto Pubblica in tutti e quattro i documenti**) · **Aggiornato:** 26 agosto 2026, sera tardi (**§J7 chiusa: anche i blocchi della vIPI ACC si riordinano, con
i settori di aerovia fissi in testa; con §J6 la sezione J non ha più voci di UI aperte**) · **Aggiornato:** 26 agosto 2026, sera tardi (**§J6: l'ordine delle sezioni è una scelta editoriale — anche le
sezioni di catalogo si spostano dentro il loro gruppo e dicono di quanto si sono allontanate dallo standard;
nasce §J7, i blocchi della vIPI ACC che non si riordinano**) · **Aggiornato:** 26 agosto 2026, notte (**§E11 chiusa: la casella degli impatti, la sezione Orfani, il giro notturno della deriva e il rilevatore delle RINOMINE dal timbro d'import; nascono §C6 (chiave di release derivata da un callsign) e §C7 (i tre resti dell'analisi sulla cancellazione dei dati importati)**) · **Aggiornato:** 25 agosto 2026, tarda sera (**§B12: il ramo `statistiche-atc` porta anche le otto richieste
del committente sul servizio statistiche — §16 della carta — fra cui la sezione Aeroporti, la potatura e il
capitolo di Guida che mancava; restano aperte le stesse due voci UI**) · vecchia testata: (**§B12 aperta: il ramo `statistiche-atc` è completo e NON fuso** — la fusione è una decisione del committente, non un passo tecnico; il 24: §B10 e §B11 fuse e cancellate. La sera del 25 si chiudono **§H5** — il VID è un link al profilo IVAO, verifica live fatta — e **§H2**, il «rosso intermittente», che erano due difetti. Della sezione UI restano aperte **H1** e **H3**) · **Scopo:** una cosa alla volta, senza rileggere la cronologia.

Ogni voce è pensata per essere presa da sola in una sessione nuova. Dove serve contesto, il rimando è al
documento che ce l'ha per esteso. L'ordine dentro ogni sezione è quello in cui conviene affrontarle.

**Legenda del blocco:** 🟢 si può fare subito · 🟡 dipende da un'altra voce · 🔴 dipende da qualcun altro
(Ivao.It, il portale IVAO, l'owner).

> ### 🆕 15 agosto 2026 — trasferimenti e audit database **fusi in `main`**, per la consegna
> `main` non è più fermo al 9 agosto: ci sono dentro sia i **trasferimenti ACC↔APP** (ex
> `feature/trasferimenti-acc-app`, PR #13 — B6, E6) sia l'**audit di database del 14 agosto** (§G). Il merge
> è stato fatto in quest'ordine apposta: le due migrazioni del 14 agosto esistevano in **due copie con lo
> stesso identificativo**, una per ramo, e al secondo merge si è tenuta quella che porta dentro il modello
> coi trasferimenti (l'altra ne aveva uno più povero, e lo `ModelSnapshot` deve descrivere il modello fuso).
>
> ✅ **Il 22 agosto sono stati fusi in `main` gli ultimi due rami che avevano roba dentro**, in quest'ordine e non
> a caso: prima **`brand-atmosphere`** (brand IVAO alla sua fonte, tema chiaro/scuro scelto dall'utente), poi
> **`feature/services-hub-profile-swapper`** (hub `/services`, prefisso `/services/vsop` con le rotte italiane
> tradotte, Aurora Profile Swapper, topbar misurata). Il primo **riscrive** `vipi-theme.css` (+937/−546), il
> secondo ci **aggiunge** poco (+130/−89): mettendo sotto il tema, il secondo merge ha riportato 130 righe sui
> token invece di 937 all'indietro. Otto file in conflitto, risolti con una regola sola — **struttura dal ramo
> dei servizi, colori dai token del tema**.
>
> Da qui **`main` è il posto dove si lavora, e non c'è nessun ramo con lavoro fuori**: la frase ha vacillato
> il 24 agosto con `coordinamenti-lato-ricevente`, fuso e cancellato in giornata (**B10**). Gli altri undici sono
> tutti a zero commit di distanza — e il 22 agosto sono stati **cancellati tutti e undici**, locale e origin.
> ⚠️ Cancellandoli è saltata fuori una cosa: **`refactor/13-tre-documenti` (B5) non aspettava nessun ok**, era
> in `main` dal 15 agosto, portato dentro dal merge dei trasferimenti. Vedi B5.
>
> ⚠️ La frase ha vacillato due volte nella stessa giornata — `catalogo-punti-suggerimenti` (**B7**) e
> `coordinamenti-lettura` (**B8**) — ed è di nuovo vera: **tutti e due fusi e cancellati**, locale e origin.
> Carte: [servizi ATC](feature/2026-08-22-servizi-atc-e-profile-swapper.md),
> [brand](feature/2026-08-22-brand-atmosphere.md), [topbar misurata](feature/2026-08-22-topbar-misurata.md).
>
> ⚠️ **Fuso non vuol dire consegnabile** — lo è diventato il **23 agosto**. Il blocco (sezione E, punto 9: le
> migrazioni degli accordi girano all'avvio e `AgreementSectionsFinalize` fallisce finché la MariaDB di
> produzione non è convertita) non è stato risolto ma **aggirato**: la consegna sostituisce il database
> invece di migrarlo. Vedi **A11**.
>
> - **11 agosto — audit full-stack, eseguito** (sta in B5). 34 voci, 23 chiuse, 3 ribaltate dalla misura.
>   Tre regole di build che cambiano: `TreatWarningsAsErrors` in `Directory.Build.props`, i test che
>   **girano davvero su net8** (da 347 a 1115) e i `packages.lock.json` committati con restore in locked
>   mode. ⚠️ `dotnet test` **non** applica il flag degli avvisi: suite verde e build di produzione rotta
>   possono convivere, quindi prima di un push serve `dotnet build Vipi.slnx -c Release --no-incremental`.
>   Esito in [`history/audit-2026-08-11-crepe-full-stack.md`](history/audit-2026-08-11-crepe-full-stack.md).
>
> ⚠️ **Il primo effetto da leggere prima di consegnare:** i dump del 6, 7 e 9 agosto (A3) sono **tutti
> inutilizzabili**, e sembravano perfetti. Vedi A3.
>
> ⚠️ **Un test intermittente non chiuso** (bridge Aurora): fallisce **solo** nella corsa completa in
> parallelo della soluzione, mai da solo — otto giri isolati e una seconda corsa completa sono verdi. Il
> sospetto è la **contesa fra progetti** (porta, file temporaneo, cartella condivisa), non il tempo dentro
> un test. Alla prossima occorrenza **tenere il log intero** (`dotnet test Vipi.slnx > log.txt 2>&1`): il
> nome del test sta nella riga sopra «Error Message».
>
> **22 agosto 2026 — il nome, preso.** `AuroraBridge.Tests.AuroraClientTests.Richieste_in_sequenza_non_si_mescolano`.
> Caduto nella corsa completa in Release (`la seconda richiesta non ha avuto risposta: Nessuna risposta a #TRPOS
> entro 15000 ms`), **verde da solo subito dopo in 65 ms** — cioè tre ordini di grandezza sotto la scadenza,
> il che esclude che sia lento e conferma che la risposta non arriva **affatto**. Il giro non toccava il bridge
> (rename delle rotte), quindi la causa resta la contesa, non una regressione. Il candidato ora ha un indirizzo:
> il client Aurora è a **porta/socket condivisa**, e in corsa parallela è l'altro progetto a occuparla.

> ### ✅ 22 agosto 2026 — CHIUSA: la topbar non sfonda più, perché non indovina più
> Chiusa lo stesso giorno, e **non** con nessuna delle due strade che questa voce proponeva. Il committente
> l'ha riaperta da un'altra parte: vedeva la barra rotta già a **1940**, cioè 530px sopra il numero misurato
> qui — perché la sua configurazione (zoom di pagina, stringa staff, login) non era quella su cui le soglie
> erano state tarate. Il difetto non era la soglia: **una media query misura la finestra, mentre il problema
> è la larghezza della barra**, che dipende da sei cose che una `@media` non vede.
>
> Adesso la barra si **misura** e sceglie da sé lo scaglione (`vipiFitTopbar` in `vipi-ui.js`, classi
> `tb-1…tb-4` in `vipi-theme.css`). Verificato su **256 combinazioni** — 8 larghezze × 4 zoom × 4 famiglie
> di pagina × 2 lingue — con `scrollWidth == clientWidth` su tutte e nessun comando perso.
>
> ⚠️ E il compromesso che questa voce dava per obbligato **non c'è stato**: a 1366 e 1440 la ricerca resta
> **aperta e intera**, perché separando «la ricerca si chiude» da «le etichette spariscono» il gradino da
> 500px è diventato due. Carta: [`feature/2026-08-22-topbar-misurata.md`](feature/2026-08-22-topbar-misurata.md).
>
> <details><summary>La misura di allora, per memoria</summary>

> ### 🆕 22 agosto 2026 — la topbar sfonda fra 1301 e ~1410px (**preesistente**, misurato)
> Trovato guidando la verifica live dei servizi ATC, e **non e' del giro nuovo**: si misura identico con e
> senza il tasto aggiunto quel giorno. Lo scaglione 2 della barra (`vipi-theme.css`, `@media (max-width:1300px)`
> — «Editor»/«Incarichi» a sole icone, ricerca richiusa) scatta **troppo tardi**:
>
> | larghezza | sforamento |
> |---|---|
> | 1420 e oltre | 0 |
> | 1400 | +10 |
> | 1380 | +30 |
> | 1350 | +60 |
> | 1320 | +90 |
> | 1301 | **+109** |
>
> Cioe' la soglia andrebbe a **~1410**, non a 1300: appena sopra i 1300 alla barra mancano 109px. Sotto i
> 1300 lo scaglione scatta e torna tutto a posto, quindi il difetto vive in una fascia sola — che pero'
> contiene **1366 e 1400**, due larghezze di portatile molto comuni.
>
> ⚠️ **Non l'ho corretto da solo perche' e' un compromesso, non un numero.** Il commento nel CSS racconta
> che quella soglia fu tarata *apposta* per tenere la **ricerca aperta** il piu' a lungo possibile, sbagliando
> due volte in direzioni opposte. Alzarla a 1410 ripara lo sforamento **e** richiude la ricerca su tutti i
> portatili 1366: e' esattamente la cosa che quella taratura voleva evitare. Le due strade sono alzare la
> soglia, oppure recuperare ~110px dentro la fascia (il candidato piu' grasso e' la ricerca, che a barra
> piena vale piu' di 200px).
>
> </details>

> ### ✅ 23 agosto 2026 — CHIUSI: i due difetti visti mentre si chiudeva la topbar
> Carta del giro: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md).
> Erano stati messi qui senza toccarli. Chiusi tutti e due, e **il primo aveva il colpevole sbagliato**.
>
> **1. Le tabelle del viewer sforano a zoom alto** — chiuso. La diagnosi di ieri diceva `table.sid-table`
> col suo `min-width:720px`: **non era lui**, la SID sta già dentro un `div` che scorre. Rimisurato a 1280
> con zoom 1.4 (**914 unità di layout**): sforo di **35 unità**, e il colpevole è `table.rwy-table`, che non
> dichiara nessun minimo e ne pretende **570** in una `.cb-body` che ne ha 497.
> ⚠️ Confermato invece il **meccanismo**: lo zoom qui è `zoom` sull'`<html>` (`vipi-zoom.js`) e **le media
> query non lo vedono** — misurano la finestra (1280) mentre il layout ne ha 914. Per questo la cura non è
> spostare la soglia dei 900 ma **toglierla**: il contenitore diretto di una tabella scorre sempre,
> `.apt-2col` passa a `minmax(0,1fr)` + `min-width:0`, e l'`overflow-wrap` dei titoli esce dalla media query
> (il minimo di un titolo è la sua parola più lunga, che in unità di layout non si accorcia mai).
> Verificato guidando Edge su **144 combinazioni** (6 pagine × 6 larghezze × 4 zoom): il viewer aeroporto va
> da 48px di sforo a **0 su tutti gli zoom fino a 1.8**, e a zoom 1 la pagina è identica a prima.
> ⚠️ **Restano fuori**, e sono **preesistenti**: i **390px con zoom ≥ 1.25** (elenco aeroporti, landing,
> ricerca, «cosa è cambiato»). Là il layout ha 312 unità o meno, sotto il pavimento dichiarato di 375
> (`docs/design/regole-ui-pagine-admin.md`, perimetro d'uso). Se un giorno contano, si riparte da qui.
>
> **2. La cultura non arriva al circuito** — chiuso. In Blazor Server le richieste sono **due**: il documento,
> che porta `?culture=it` e vince con la stringa di query, e la connessione `/_blazor` che apre il circuito,
> che quella stringa non ce l'ha e ricade su `Accept-Language`. Il circuito nasce con quella cultura e la
> tiene per tutta la vita. `CultureCookieMiddleware` (in `Vipi.Hosting`, montato **dopo**
> `UseRequestLocalization`) scrive il cookie standard di `CookieRequestCultureProvider` quando — e **solo**
> quando — la lingua è stata chiesta esplicitamente nell'indirizzo.
> ⚠️ **Solo su richiesta esplicita**: scriverlo anche per `Accept-Language` congelerebbe per un anno una
> scelta che l'utente non ha mai fatto, e cambiare lingua al browser non avrebbe più effetto. Due test E2E,
> uno per verso; verificato anche guidando Edge con `Accept-Language: en-US`.

## Dove siamo, in cinque righe

**Riscritto il 30 agosto 2026**, con le cifre **contate**. È la sezione da leggere per prima quando si
riprende senza contesto: dice dov'è il codice, cosa manca e cosa va fatto *prima* del prossimo deploy.

---

### 🔴 30 agosto, sera — LA CONSEGNA È A METÀ, E QUESTO VIENE PRIMA DI TUTTO

**Nove commit sul ramo `consegna-db-20260830`, NON SPINTI.** Albero pulito, 15 assiemi su 15 verdi.
Prima cosa da fare riprendendo: `git push -u origin consegna-db-20260830`.

**Stato della consegna** (cronaca completa in **§A14**):

| | |
|---|---|
| **Pacchetto `j`** | ✅ pubblicato *e caricato* dal committente via FileZilla (457 file). **Non ancora riavviato.** |
| **Database** | ✅ `.sql` 985 KB + `.gz` 186 KB in `_mariadb/dump/`, con i due script di rete in `artifacts/`. ⏳ **in attesa che Ivao.It lo importi** |

⚠️ **La finestra scoperta è adesso.** I file nuovi sono sul server e il database è ancora quello vecchio: se
Passenger rigenera il processo da solo — **è già successo il 23 agosto senza che nessuno toccasse
`tmp/restart.txt`** — il pacchetto `j` applica **in produzione, da solo, le 36 migrazioni** che il dump
avrebbe portato già fatte. Il rilevatore c'è ed è la query in cima allo script di copia: se elenca tabelle, il sito è
ripartito prima.

⚠️⚠️ **QUESTA CONSEGNA RIPARTE DA ZERO SUL CONTENUTO**, per decisione del committente: dentro ci sono le
anagrafiche, la memoria di traduzione (274 unità) e il glossario, **e nient'altro**. Niente vIPI, niente
accordi, niente release, niente spazi aerei, niente statistiche. Il `vipi.db` di sviluppo **è intatto**
(40 144 righe, 25 documenti) e non è stato toccato: è da lì che si riscrive.

**Cosa resta al committente:** `tmp/restart.txt` **solo a database dentro** → prima riga di
`diagnostica/avvio-diagnostica.txt` con la data di adesso → il push del ramo.

### 📦 A18 — Pacchetto 1.2.0, 31 agosto 2026: 20 file — ✅ **CARICATO E IN PRODUZIONE**

`vipi-1.2.0-solo-file-cambiati.zip` · **4,37 MB** ·
sha256 `733c619e50b92e08f11bf114ea2c325dd273cd91b4e63fbe95a286ddd5b165d9`
Timbro **`1.2.0 · 9d5d902`**. Foglio: `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.2.0.md`.

Sostituisce **1.1.0** (`bfb2c056`). Dentro: §AF–§AL (ricaduta verticale, cicli, il giro su sette pagine) e
**§AM** (i quattro difetti letti nella diagnostica, fra cui quello che faceva morire il processo).

#### ✅ Caricato alle 14:46 UTC, e verificato dal vivo dall'esterno

`diagnostica/avvio-diagnostica.txt` del server: **`1.2.0 · 9d5d902`**, ambiente **Production**, avvio
completo in **7 276 ms**. La voce **«migrazione del database» sale a 3 403 ms** (erano 2 935 su 1.1.0): la
migrazione è girata ed è arrivata in fondo. ✅ **Nessun `avvio-errore.txt`** — cioè il caso per cui serviva
il ripristino non si è verificato.

✅ **Otto controlli dal vivo su `atc.it.ivao.aero`, da ANONIMO, con un browser vero** (Edge guidato,
`.claude/skills/verifica-live/pacchetto-verifica.js` con `BASE=https://atc.it.ivao.aero SOLO_PUBBLICO=1`):

| | |
|---|---|
| `vipi-riconnessione.js` | HTTP 200, 2 400 byte, **identico** a quello del pacchetto |
| circuito Blazor | aperto (`window.Blazor` presente): non il solo prerender |
| **la Ricerca risponde** | la riga sotto il campo cambia digitando `LI` — il controllo che distingue un sito vivo da uno mezzo caricato |
| home `/services/vsop` | 4 schede ACC, **nessun** avviso «catalogo non disponibile» |
| foglio di stile | in vigore · console del browser **pulita** |

E due prove indipendenti che gira il codice nuovo, non solo che il timbro lo dice:
`/services/airspace` risponde **404** (§AK l'ha tolto) e `/services/vsop/airspace` fa scattare il cancello.

> ⚠️ **Un 200 su una pagina riservata NON vuol dire che sia aperta**, e vale la pena scriverlo perché la
> prima lettura inganna: da anonimo `/services/vsop/airspace` risponde **200**, ma il corpo è «Accesso
> riservato» dentro un `callout danger` e **zero dati**. In questo prodotto i cancelli si disegnano, non si
> restituiscono come stato HTTP — il cancello di `AirspacePage` sta in **due** sedi, nel markup e prima
> delle query. Controllato, non dedotto.

#### 🔴 Perché «stasera» — e la parte che NON è ancora chiusa

Questo pacchetto porta **una migrazione**, `CatenaDiRipiego` — additiva, ma in produzione
`Database.Migrate()` gira **all'avvio, da solo, su DDL non transazionale**. Il runbook lo vieterebbe dentro
la finestra cieca. Parte lo stesso perché il committente ha detto (31 agosto) che chi amministra il database
**può ancora ripristinare entro stasera**.

⚠️ **È una deroga a tempo, non la fine della finestra.** Il caricamento *e* i tre controlli vanno fatti
mentre quella rete c'è. Se non si fa in tempo: **non si carica**, si aspetta — e nel frattempo il difetto
che uccide il processo ogni due-tre ore resta. Il foglio lo dice al committente con queste parole.

#### I file: 20, e perché non 26

Il confronto per impronta col publish di 1.1.0 dà **26** file diversi. Sei sono
`Vipi.Hosting`, `Vipi.AuroraProfiles` e `Vipi.AuroraBridge.Contracts` coi loro `.pdb`, che
`git diff --name-only bfb2c056 HEAD -- src` dice **invariati**: differiscono per il solo MVID di una
ricompilazione. Ogni `.dll` in più è una rinomina in più su un file che il processo tiene aperto.

I 20: i sei assiemi cambiati davvero coi loro `.pdb` (12), `en/Vipi.Ui.resources.dll`, l'indice
`staticwebassets` e i due file di `wwwroot` **coi loro `.br`/`.gz`** (7).
⚠️ `Vipi.Infrastructure.MySqlMigrations.dll` è nel pacchetto e **va con** `Vipi.Infrastructure.dll`: è lì
che sta la migrazione nella forma che capisce MariaDB.
ℹ️ `vipi-riconnessione.js` **non è cambiato** e resta sul server — il foglio avverte di non cancellarlo,
perché senza quel file il sito si vede intero e non risponde a niente.

#### ✅ Verifica sul PACCHETTO, non sul sorgente

Publish `win-x64` nello scratchpad, avviato dalla sua cartella su una copia del `vipi.db`, guidato con Edge
(`verifica-1.2.0.js`). **Dieci controlli, tutti verdi**:

| | |
|---|---|
| `vipi-riconnessione.js` | servito, **HTTP 200**, 2 400 caratteri **minificati su una riga** |
| circuito Blazor | `window.Blazor` presente — non il solo prerender |
| home `/services/vsop` | 4 schede ACC, **nessun** avviso «catalogo non disponibile» |
| **la Ricerca risponde** | la riga sotto il campo cambia digitando `LI` — è il controllo che distingue un sito vivo da uno mezzo caricato |
| editor ACC LIBB | si apre, nessun «second operation», pannello traduzioni nel DOM |
| foglio di stile | in vigore (`body background = rgb(18, 19, 27)`) |
| console del browser | pulita |

✅ **La migrazione è stata vista applicarsi**: `20260831014235_CatenaDiRipiego` è in
`__EFMigrationsHistory` della copia di prova, e il timbro d'avvio dice **`1.2.0 · 9d5d902`**.

⚠️ **Quel che questa prova NON copre, e va detto**: gira su **SQLite**, quindi esercita la migrazione di
`Vipi.Infrastructure`, non la gemella MySql che girerà in produzione. Quella è coperta dalla suite (verde,
4 807 su net8) ma **non è stata vista girare su una MariaDB vera in questa sessione** — in locale non ce
n'era una in ascolto. È il motivo in più per cui la copia di sicurezza va fatta **prima** del riavvio.

✅ Le due reti dello script hanno taciuto perché non c'era niente da dire: nessun file non dichiarato nella
cartella, nessun segreto dentro i file di testo. Lo zip contiene **27 voci**: i 20 file, `IMPRONTE.txt` e i
quattro fogli in `docs/`. Le 20 impronte sono state **riverificate** una per una contro i file: 0 problemi.

#### ✅ A18-bis — IL VERDETTO È DATO: IL DIFETTO È CHIUSO

`avvii.txt` del server, dopo il caricamento:

```
14:46:26Z  AVVIO  1.2.0 · 9d5d902   ⚠ il processo precedente NON si è spento in modo ordinato
```

⚠️ **Quella riga riguarda il processo VECCHIO, non il nuovo.** Il «precedente» era 1.1.0, partito alle
13:05. È il terzo della serie — **3h02 → 2h08 → 1h41** — e da questo file non si può dire se sia morto per
la perdita di memoria o se l'abbia ucciso il caricamento. Non serve saperlo: in tutt'e tre i casi era la
versione **col difetto**. Di 1.2.0 non dice niente.

🔴 **La prova che il difetto è chiuso non è una riga che compare: è una riga che NON compare.** Quando il
committente ha scaricato i file, 1.2.0 era acceso **da un minuto**.

| quando rileggere `avvii.txt` | perché |
|---|---|
| 18:27 locale (16:27 UTC) | 1h41 di vita: supera solo la vita **più corta** di 1.1.0 — **non basta** |
| **19:48 locale (17:48 UTC)** | 3h02: supera anche la **più lunga**. È il primo momento in cui un «niente» vuol dire qualcosa |
| 20:46 locale (18:46 UTC) | 4h: margine comodo |

- **nessuna riga ⚠ nuova** → la perdita di memoria è chiusa, e §AM-A si può spuntare;
- **una riga ⚠ nuova** → non lo è, e lo si sa **mentre chi amministra il database è ancora raggiungibile**.

##### ✅ Riletto alle 20:11 locali: nessuna riga nuova

`avvii.txt` finisce a **`2026-08-31 14:50:04Z  AVVIO  1.2.0 · 9d5d902`**, e dopo quella riga non c'è niente.
I file sono stati scaricati alle **20:11 locali (18:11Z)**: il processo era acceso da **3h20:56**, cioè
**19 minuti oltre** la vita più lunga di 1.1.0 (3h01:56) e più di un'ora oltre le altre due. È esattamente
la soglia che questa tabella fissava.

⚠️ **Le tre righe fitte fra 14:48 e 14:50 non sono instabilità**: dicono tutte «spento in modo ordinato» —
è il rimbalzo del rilascio. L'unica ⚠ del pomeriggio resta quella delle 14:46, e riguarda il processo
vecchio.

✅ **Prova di vita indipendente**: `neighbours-debug.log` ha due righe alle **16:51:16Z** e **16:52:36Z** —
a 2h02 dall'avvio — col ricalcolo dei confinanti riuscito (28 domestici × 21 ACC → 33 coppie). E
`avvio-diagnostica.txt` è timbrato **14:50:04Z**, cioè è quello di *questo* processo e nessuno l'ha
riscritto: avvio in 6 014 ms, migrazione 2 408 ms (già girata alle 14:46, qui è il solo controllo).

⚠️ **Che cosa NON prova**, perché la differenza conta: fra le 16:52Z e le 18:11Z non c'è una prova
*positiva* di attività. Il verdetto regge sull'**assenza di un riavvio**, non su un carico misurato. Sotto
Passenger però un processo morto riparte alla prima richiesta e lascia la riga ⚠ — è così che sono comparse
le tre di 1.1.0, in una fascia oraria confrontabile (12:57→16:46 locali contro 16:50→20:11 di adesso).

**§AM-A chiusa. §AM3 chiusa.**

#### Il cancello, guidato

Con `IT-XYZ9` (**DivisionStaff**) l'hub mostra spazi aerei e convertitore ma **non** la coerenza, e la pagina
scritta a mano risponde «Accesso riservato… livello Editor e superiori» con **zero** tabelle e nessun tasto.
Con `LIRR-CH` (**Editor**) compaiono tutte e tre le scorciatoie e la pagina si apre. ⚠️ Il VID va cambiato:
704798 è un fondatore e resterebbe Admin qualunque posizione gli si dia.

### Quel che resta

- ✅ **A18-bis CHIUSA** il 31 agosto alle 20:11 locali — vedi il riquadro qui sopra. Con essa **§AM3**.
- ✅ **`errori-richieste.txt` — la lettura è la prima**: nella cartella `diagnostica` mandata dal committente
  ci sono **tre** file e basta (`avvii.txt`, `avvio-diagnostica.txt`, `neighbours-debug.log`), e il
  committente ha confermato che c'è tutto. Quindi il file **non si è ricreato**: zero richieste fallite da
  quando 1.2.0 è in servizio. E non c'è nessun `avvio-errore.txt`. **§AM4 chiusa** senza niente da
  cancellare.
- ✅ **§AM2 CHIUSA il 31 agosto sera**: il committente è entrato col proprio VID, ha aperto l'editor delle
  vIPI di **LIBB** e ha **salvato**. «A second operation was started» **non è comparso**. È la prova che
  §AM-C — il pannello traduzioni che correva contro l'editor sul contesto del circuito — è chiusa **in
  produzione**, dove la latenza del database remoto apre quella finestra che in locale è quasi nulla.
  ⚠️ Era l'unica voce di §AM che da fuori non si poteva chiudere.
- 🟡 Da prima: caricare il **KMZ degli spazi aerei** (§A16).

### 📦 A19 — Pacchetto 1.3.0, 31 agosto 2026 notte: 22 file — ✅ **IN PRODUZIONE**

**Timbro `1.3.0 · 1ade0db`.** Sostituisce 1.2.0 (`9d5d902`). Zip
`artifacts/publish/vipi-1.3.0-solo-file-cambiati.zip`, **4,46 MB**, sha256
`7435d225c9b9723c670c8da31767735a737753c4c056d6645902590616f6124c`. I 22 file scompattati pesano 14,9 MB.

**MINOR**, per tutt'e due i motivi che la tabella del runbook ammette: una funzionalità nuova (§AN, la
lingua bloccata) e **una migrazione additiva**, `LinguaBloccata` — *una sola colonna*,
`Documents.LanguageLocked`, `bool NOT NULL DEFAULT 0`, su una tabella che c'è già.

🔴 **La migrazione parte dentro la finestra cieca per decisione esplicita del committente** (31 agosto,
notte), come già per 1.2.0. Passa `MigrazioniDellaFinestraCiecaTests` — è additiva e ha un default vero —
ma «passa il presidio» non è «non può fare danno»: il caricamento va fatto **finché c'è qualcuno
raggiungibile**, e il foglio lo dice in testa col riquadro rosso.

#### Che cosa porta

1. **§AN — lingua bloccata**: un documento si legge in UNA lingua sola; il blocco **spegne** la traduzione
   di quel documento, non la fa. È da qui che viene la colonna nuova.
2. **Il 3D delle aree regolamentate dice il NOME** invece dell'id IVAO: il payload `data-sectors3d` non
   emetteva affatto `Label`.
3. **La pagina «Spazi aerei» prende un foglio**: le quindici classi `asp-*` non avevano **nessuna** regola
   nel CSS, e le colonne `c-*` del markup sono scoped ad altre tabelle. Più i cinque riquadri
   comprimibili con memoria dello stato (richiesta esplicita), il rapporto radioassistenze che nasce
   chiuso, il tasto «pulisci filtri» e l'`<input type=file>` finalmente nascosto.
4. **I campi delle «Regole piste» leggibili a tema scuro**: avevano `--on-brand` come fondo, che per
   progetto resta bianco in ogni tema.
5. **L'anteprima di una release guarda le SID al ciclo di QUELLA release** e non a quello di oggi.

ℹ️ E una cosa senza codice, verificata dal vivo: «Regole piste» **si può già nascondere** dal documento
pubblicato senza spegnere le regole — pista in uso, simboli e SID iniziale continuano a uscire.

#### I file: 22, e due novità rispetto a 1.2.0

Sette coppie `.dll`+`.pdb` (Host, Ui, Application, Domain, Infrastructure, Infrastructure.MySqlMigrations,
**Hosting**), `en/Vipi.Ui.resources.dll`, `Vipi.Host.staticwebassets.endpoints.json` e sei file di
`wwwroot/_content/Vipi.Ui/` (`vipi-theme.css` e **`vipi-aor3d.js`**, ognuno coi suoi `.br`/`.gz`).

⚠️ **`Vipi.Hosting.dll` è nuovo nell'elenco**: 1.2.0 non lo conteneva, e qui `VipiModuleExtensions.cs` è
cambiato. ⚠️ E stavolta il file di `wwwroot` che viaggia col foglio di stile è **`vipi-aor3d.js`**, non
`vipi-boot.js`: quello di 1.2.0 resta dov'è e non va ricaricato.

⚠️ **`Vipi.Host.dll` c'è anche se `src/Vipi.Host` non ha una riga cambiata**: il timbro di versione è un
`AssemblyMetadata` di *quel* progetto, quindi passando da 1.2.0 a 1.3.0 cambia lui e basta. Un elenco
costruito solo dal `git diff` lo lascerebbe fuori, e il sito direbbe la versione vecchia.

#### Le prove fatte

✅ Build Release `--no-incremental`: **0 avvisi**. ✅ `dotnet test -c Release`: **15 assiemi su 15**, e sono
stati contati — un riepilogo verde con un progetto mancante è verde lo stesso.

✅ **Provato sul pacchetto pubblicato**, non sul sorgente: publish `win-x64` avviato dalla sua cartella,
`pacchetto-verifica.js` → **dieci controlli verdi**, Ricerca ed editor compresi, console pulita, timbro
`1.3.0 · 1ade0db`. ✅ E le due correzioni sui file statici sono state cercate **dentro il minificato**:
`.rule-grid input{…background:var(--surface)…}` nel CSS e `s.label||s.sec` nel JS.

✅ Le due reti dello script hanno taciuto perché non c'era niente da dire. Lo zip ha **29 voci** (22 file,
`IMPRONTE.txt`, quattro fogli, due cartelle) e le **22 impronte sono state riverificate una per una contro
il contenuto dello zip**: 0 problemi.

#### ✅ Caricato e verificato dall'esterno la notte del 31

Il committente ha caricato e vede **1.3.0** nella barra. Verifica da anonimo, `pacchetto-verifica.js`
puntato su `https://atc.it.ivao.aero`: **otto controlli verdi**, console pulita.

✅ **E le due correzioni sui file statici sono state verificate PER IMPRONTA, non a occhio.** Scaricati dal
sito con `Accept-Encoding: identity` e confrontati con `IMPRONTE.txt`:

| file servito da produzione | esito |
|---|---|
| `_content/Vipi.Ui/vipi-theme.css` | **identico**, 176 853 B, `baf2782f…` |
| `_content/Vipi.Ui/vipi-aor3d.js` | **identico**, 15 964 B, `c37576f9…` |

È anche la prova che `wwwroot` e `Vipi.Host.staticwebassets.endpoints.json` sono saliti **insieme**: con
l'indice vecchio quegli indirizzi non risponderebbero con questi byte.

#### ⚠️ Il primo giro era ROSSO, e il rosso era della sonda

Lanciata **subito dopo il riavvio**, `pacchetto-verifica.js` ha detto *«la RICERCA risponde — NESSUN
cambiamento: sito mezzo caricato»*, cioè esattamente la diagnosi che fa tornare indietro una consegna.

Il sito era sano. Misurato a mano un minuto dopo: WebSocket connesso (`wss://…/_blazor?id=…`), e la Ricerca
rispondeva in **746 ms con «50 results for LI»**. Il processo era appena partito — l'avvio dura ~6 s e apre
il database — e la prima interazione col circuito è caduta oltre la finestra d'attesa della sonda.

⚠️ **Un falso rosso su QUEL controllo è la cosa più cara che la sonda possa fare**: è quello su cui si
decide se tornare indietro, e tornare indietro da una consegna sana è peggio che non averla verificata.
La sonda ora **riprova una volta ricaricando la pagina** (cioè riaprendo il circuito, che è il caso da
coprire) e lo dice quando è passata al secondo giro.

ℹ️ Trovato per strada e vale come promemoria: «0 results for LIRF» **non è un difetto**. L'archivio di
produzione è quello ripartito da zero il 30 agosto — anagrafiche, memoria di traduzione, glossario — quindi
i documenti d'aeroporto non ci sono. `LI` dà 50 risultati, e la vIPI di LIBB dice «No vIPI published».

#### Quel che resta

- 🟡 **Il controllo A del foglio lo può fare solo chi ha l'FTP**, perché `diagnostica/` è chiusa dall'esterno
  (§A13): `avvio-diagnostica.txt` deve dire `1.3.0 · 1ade0db` e avere la voce «migrazione del database»,
  e **non** deve esserci `avvio-errore.txt`. Che l'app serva le pagine è già un forte indizio che la
  migrazione sia passata — se fosse fallita non sarebbe partita — ma l'indizio non è la misura.
- 🟡 **Il controllo C** (i campi delle «Regole piste» a tema scuro) vuole un accesso da staff: da fuori
  quella pagina non si raggiunge.
- 🟡 Da prima: caricare il **KMZ degli spazi aerei** (§A16).

### 📦 A15 — Pacchetto 1.1.0, 31 agosto 2026: solo i file cambiati — ✅ **IN PRODUZIONE**

Il primo pacchetto che porta un **numero** invece di una lettera, e il primo **incrementale**.

#### ✅ Caricato e verificato dal vivo, la notte del 31

Il committente ha caricato i 18 file, i segreti e `tmp/restart.txt`. Verifica **dall'esterno**, senza login:

| Controllo | Esito |
|---|---|
| `/vsop/ping` | **204** — e nel pacchetto `j` quell'indirizzo **non esisteva**: è la prova che gira il codice nuovo |
| prima chiamata | **7,7 s**, poi istantanea → avvio a freddo |
| `_content/Vipi.Ui/vipi-riconnessione.js` | **200, 2 400 B**, impronta `ed2c64e7` = quella del pacchetto |
| markup | `autostart="false"` ✅, riquadro di riconnessione presente ✅, circuito aperto ✅ |
| **la Ricerca** | «Digita almeno 2 caratteri» → **«33 risultati per LI»** — il controllo del foglio, passato sul sito vero |
| console / rete | zero errori, zero risposte ≥ 400 |

#### ✅ Il registro degli avvii, al primo giro vero

```
00:28:23  AVVIO    1.1.0 · aaaeddb   (primo avvio registrato in questo file)
00:28:45  ARRESTO  acceso per 00:00:21
00:28:54  AVVIO    1.1.0 · aaaeddb   (il precedente si era spento in modo ordinato)
00:30:45  ARRESTO  acceso per 00:01:50
00:36:38  AVVIO    1.1.0 · aaaeddb   (il precedente si era spento in modo ordinato)
```

✅ **Tutti gli arresti sono ORDINATI**, ed è la conferma che serviva: su quel server Passenger chiude in
modo pulito, quindi il giorno che la riga dirà «NON si è spento in modo ordinato» sarà un **fatto**, non
rumore. Finora quel comportamento si era visto solo nei test e uccidendo il processo a mano.

✅ **E c'è la fotografia del fenomeno che ha originato tutta §AE**: fra le **00:30:45 e le 00:36:38 il
processo era spento**, ed è ripartito **alla prima richiesta** — il `/vsop/ping` da 7,7 s della verifica.
Sei minuti senza visite e il processo non c'è più: è esattamente ciò che faceva comparire «Attempting to
reconnect» a chi aveva una pagina aperta.

#### ✅ I segreti, e la chiave Microsoft

```
Cartella «segreti» ....... 1 file letti (i nomi non si riportano)
  Translation:Enabled ........ True
  Translation:Azure:ApiKey ... valorizzato (84 caratteri)  (regione: italynorth)
```
Più: ambiente **Production**, `appsettings.Production.json` presente, provider **MySql**, password non in
chiaro, e **nessun `avvio-errore.txt`** — l'avvio non è mai fallito.

#### ✅ Il `.sql` del 30 ERA già stato importato

Non lo dice un file, lo dice il contenuto: la vIPI di **LIBB annuncia «Aeroporti 0»**, che è esattamente
l'archivio consegnato il 30 (anagrafiche + memoria di traduzione + glossario, e nient'altro). Con il
database vecchio ci sarebbero stati i documenti del 16 agosto. ⚠️ È un'**inferenza dal contenuto**, non una
lettura diretta: la conferma definitiva è una riga di `/services/vsop/admin/diagnostics`.

ℹ️ **Il numero da confrontare al prossimo avvio**: `migrazione del database 2 872 ms` + `manutenzioni
2 099 ms` su **5 715 ms** totali. In sviluppo erano ~1 300 ms in tutto: su MySQL costa di più, ma se un
domani quella voce cambia di molto vuol dire che qualcosa è stato applicato.

#### Il pacchetto

| | |
|---|---|
| **Timbro** | `1.1.0 · aaaeddb` (il foglio con questo timbro è nel commit **dopo**: il timbro nasce dal commit al momento del publish) |
| **Da caricare** | **18 file, 12,4 MB** — `artifacts/publish/solo-18-file-1.1.0/` |
| **Zip da spedire** | `artifacts/publish/vipi-1.1.0-solo-file-cambiati.zip`, **4,03 MB**, sha256 `4ea7458d…d1ee9f88`. Dentro **due rami**: `solo-18-file-1.1.0/` (si carica) e `docs/` (si legge) |
| **Fogli** | `artifacts/publish/docs/`, copiati da `deploy/atc-ivao/` che resta la sorgente |
| **Pacchetto completo** | `artifacts/publish/linux-x64-20260831/` (460 file), tenuto come riferimento e per il prossimo diff |
| **Database** | **non si tocca.** Nessuna migrazione in nessuno dei due rami fusi |
| **Foglio** | [`../deploy/atc-ivao/LEGGIMI-PACCHETTO-1.1.0.md`](../deploy/atc-ivao/LEGGIMI-PACCHETTO-1.1.0.md) |

📋 **Da qui in avanti la consegna ha un runbook**: [`guide/preparare-un-pacchetto.md`](guide/preparare-un-pacchetto.md),
e i passi meccanici li fa `tools/prepara-pacchetto.ps1`. ⚠️ **Lo script ha avuto lui stesso il difetto che
presidia**: con **un solo** file sospetto PowerShell 5.1 torna uno scalare, `.Count` non vale 1, e la rete
restava muta — cioè taceva esattamente nel caso per cui esiste (con due file parlava). Chiuso con `@()`,
e provato in tutt'e due i versi.

**Come sono organizzati gli artifacts** (dal 31 agosto, e il foglio è `artifacts/publish/LEGGIMI-CARTELLE.md`):
in **`publish/`** sta **solo la consegna corrente** — il publish completo, il pacchetto da caricare, i fogli in
`docs/` e lo zip; in **`publish_old/<data>/`** sta **una cartella per consegna passata**, con dentro il suo
publish, il suo pacchetto incrementale, il suo zip e i **suoi** `docs/`.
⚠️ **I `docs/` di una cartella vecchia non si aggiornano**: sono la fotografia di quel che dicevamo allora,
ed è l'unico modo di rispondere fra sei mesi a «cosa gli avevamo detto di fare?».
⚠️ I `.md` non stanno **mai** dentro la cartella dei file da caricare: se uno finisse sul server non farebbe
danno, ma quella cartella diventerebbe un misto di due cose — ed è così che si carica quella sbagliata.
ℹ️ `publish_old/solo-2-file/` sta alla radice e non in un gruppo: **non ha un suffisso di versione**, e
attribuirlo per data sarebbe stata un'ipotesi travestita da riordino.

✅ **La chiave Microsoft: provato che il sito la pesca da `segreti/`.** Il modello del file è
`deploy/atc-ivao/segreti.esempio.json` (solo segnaposti), e la catena è verificata in due tratti che si
toccano:
1. **file → configurazione**, dal vivo *sul pacchetto pubblicato*: messo un file con una chiave finta in
   `segreti/`, `diagnostica/avvio-diagnostica.txt` ha scritto `Cartella «segreti» ... 1 file letti`,
   `Translation:Enabled ... True` e `Translation:Azure:ApiKey ... valorizzato (32 caratteri) (regione:
   italynorth)`;
2. **configurazione → intestazioni HTTP**, presidiato da `AzureTranslationEngineTests`, che asserisce
   `Ocp-Apim-Subscription-Key` e `-Region` presi da `TranslationOptions.Azure`.

⚠️ **Non è stata fatta nessuna chiamata vera a Microsoft**, ed è voluto: l'endpoint della prova puntava a
una porta morta locale, così nessun testo dei documenti è uscito per una verifica.
ℹ️ Il segnaposto `METTI-QUI-LA-PASSWORD` è quello che `SegretiFuoriDalWeb` riconosce: caricare il modello
senza riempirlo **ferma l'avvio** con un errore scritto, invece di far ripartire il sito su un database
vuoto — che sembrerebbe «abbiamo perso tutto».

🔴 **E il riordino ha scoperto una cosa che andava scoperta**: nella cartella dei file da caricare era
comparso **`k7f3a91c4atce8b2.json`**, cioè il file dei **segreti** di produzione (connection string con la
password, `ClientSecret` di IVAO e di VipiAuth). Costruendo lo zip **camminando la cartella**, ci era finito
dentro — nel file che si spedisce per posta. Quel file è protetto **solo** dal nome non indovinabile
([[host-reale-plesk-passenger]]): dentro un allegato non è protetto da niente. ⚠️ **Non è la prima volta**:
la stessa cosa è in `publish_old/20260824-i/solo-4-file-i/`, del 24 agosto.
**La cura non è «stare attenti»**: lo zip ora si costruisce dall'elenco di `IMPRONTE.txt` — cioè da quel che
il foglio **dichiara** — e ogni file presente ma non dichiarato viene elencato e lasciato fuori.

**Perché 18 e non 474.** Solo cinque assiemi cambiano davvero (`Host`, `Ui`, `Hosting`, `Application`,
`Infrastructure`): gli altri differiscono solo per l'MVID di una ricompilazione, e ricaricarli sarebbe
rischio senza guadagno — ogni `.dll` in più è una rinomina in più su un file che il processo tiene aperto.
Con loro vanno le mappe `.pdb` (o `errori-richieste.txt` perde il numero di riga), `en/Vipi.Ui.resources.dll`,
l'indice `staticwebassets` e i due file di `wwwroot` con i loro `.br`/`.gz`.

⚠️ **Il rischio nuovo di questo pacchetto**: da qui `blazor.web.js` parte con `autostart="false"`, quindi un
caricamento senza `vipi-riconnessione.js` (o senza l'indice) dà un sito che **si vede intero e non risponde
a niente**. Nel foglio è il primo dei due avvisi, e il controllo finale non è «la pagina si apre» ma la **Ricerca**
(`/services/vsop/search`): si scrivono due lettere e la riga sotto il campo deve cambiare.

⚠️ **La prima versione del foglio diceva «premete un tasto qualsiasi, il selettore della lingua va
benissimo», e sarebbe stato un controllo che dice sempre «a posto»**: il selettore è un `<a>`, lo zoom e il
tema sono JavaScript di pagina — tutti e tre funzionano su un sito in cui Blazor non è mai partito. Il
controllo giusto è stato **cercato e provato nei due modi** (pacchetto completo, e con
`vipi-riconnessione.js` bloccato dal browser): con il file, digitando `LIRF` la riga diventa «0 risultati
per LIRF»; senza, resta «Digita almeno 2 caratteri» **col campo pieno**. Sulle pagine pubbliche non c'è
nessun altro comando che passi dal circuito — la ricerca è una pagina interattiva intera.

✅ **Verificato sul PACCHETTO, non sul sorgente** — è l'unico giro in cui si vede l'effetto
dell'ottimizzatore, che minifica il JavaScript: pubblicato per `win-x64`, avviato, e guidato con Edge.
Il file servito è quello minificato (2 400 B), il circuito si apre, le frasi sono in italiano, `/vsop/ping`
risponde `204`, il timbro dice `1.1.0 · aaaeddb`. Poi il processo è stato **ucciso e riavviato**: la pagina
si è **ricaricata da sola in 4 secondi**.

✅ E il registro degli avvii ha scritto, da solo, tutte e due le righe che deve saper scrivere: l'`ARRESTO`
sullo spegnimento ordinato (novanta volte, nei giri della suite E2E) e il verdetto **«⚠ il processo
precedente NON si è spento in modo ordinato»** dopo l'uccisione secca — che è la forma che hanno un crash,
una memoria esaurita e una `.dll` sovrascritta mentre gira.

⚠️ **Trovato dal pacchetto e non dai test**: la riga `AVVIO` portava il *dettaglio* della versione, che
finisce con «in servizio dal \<data\>» — cioè la stessa ora già scritta a inizio riga. Ora porta
l'etichetta corta. I test guardavano che la riga ci fosse, non come suonasse.

### ✅ A17 — CHIUSA: `main` è stato allineato, e i sei rami sono stati cancellati

✅ Fatto su richiesta del committente. `main` contiene ora `consegna-db-20260830`, `intro-di-pagina`,
`riconnessione-circuito`, `consegna-20260831` (= **1.1.0, il codice online**), `ricaduta-verticale-e-cicli`
(§AF–§AL) e `corse-e-perdita-diagnostica` (§AM). I sei rami sono stati cancellati, locali e remoti.
Build Release **0 avvisi** su net8 e net10, suite verde (**4 807** / 4 473).

🔴 **Ma «fondere» non è «consegnare», e adesso in `main` c'è una migrazione.** `CatenaDiRipiego`
(`20260831014235` + la gemella MySql `20260831014248`) arriva da §AF, e siamo nella **finestra cieca fino
al 16 settembre**: in produzione gira `Database.Migrate()` all'avvio, quindi una migrazione spedita in
questa finestra **gira da sola, su DDL non transazionale, senza nessuno che possa ripristinare**. È additiva
e passa il presidio, ma chi costruisce il prossimo pacchetto **da `main`** se la porta dietro. Per
consegnare i soli quattro difetti di §AM — che di migrazioni non ne hanno — si apre un ramo di consegna da
**`bfb2c056`** con sopra i due commit `8a540bb2` e `7ad5df21`.

<details><summary>Com'era la voce, prima</summary>


Il codice in produzione è **`consegna-20260831`** (spinto). `main` è fermo a **`30363753`** e **non contiene**
né la consegna del 30, né l'intro di pagina, né la riconnessione: sono **41 commit** di distanza.

⚠️ **È esattamente la trappola che questo progetto ha già pagato due volte** — «si parte da `main`» quando
`main` non è ciò che gira. Finché dura, chiunque apra un ramo da `main` costruisce un pacchetto che
**riporta indietro il sito**, e il runbook lo dice in testa ([`guide/preparare-un-pacchetto.md`](guide/preparare-un-pacchetto.md)).

**Da fare** (decisione del committente, non si spinge su `main` senza chiederlo):
```
git checkout main && git merge --no-ff consegna-20260831 && git push
```
Poi si possono cancellare i tre rami assorbiti: `consegna-db-20260830`, `intro-di-pagina`,
`riconnessione-circuito` (tutti e tre già dentro `consegna-20260831`, e tutti e tre spinti).

</details>

### 🟡 A16 — Dopo il deploy: cosa dice la diagnostica di produzione

`/vsop/health` risponde **Degraded** e `/vsop/health/ready` **Healthy**: le condizioni critiche stanno
bene, e a far scattare il degrado è il **report di consistenza** — **26 avvisi, zero errori**. Letti uno per
uno il 31 agosto, si dividono in tre gruppi con tre destini diversi.

#### Nove settori «senza poligono» — ✅ non c'è niente da riparare

`LIZZ_AEW_CTR` · `LIZZ_JTA_CTR` · `LIZZ_NVY_CTR` · `LIZZ_AAR_CTR` · `LIVK_CRC_CTR` · `LIVK_RCC_CTR` ·
`LIRO_CRC_CTR` · `LIPP_PLN_CTR` · `LIRR_PLN_FSS`

Sono **enti che un'area geografica non ce l'hanno**: early warning, Navy, rifornimento in volo, Control and
Reporting Centre, Rescue Coordination Centre, planning, FSS. Non è una lacuna della sorgente — il ripiego
dal sectorfile è **automatico** e ci ha già provato: quel poligono non esiste da nessuna parte.

⚠️ **La conseguenza da ricordare è nelle statistiche**: per quegli enti le **ore contano** e i **movimenti
saranno sempre zero**. Chi un giorno confronterà le due colonne non deve leggerci «non hanno lavorato».

#### Sedici torri sul cerchio sintetico — 🟡 una parte si chiude caricando il KMZ

`LIAA_I` `LIAP_I` `LIBC_I` `LIDH_I` `LIEF_MIL` `LIER_I` `LILA_I` `LILE_I` `LILG_I` `LILN_I` `LIMB_I`
`LIMC_E` `LIPN_I` `LIQS_I` `LIQW` `LIRF_E`

Il giro automatico prova quattro fonti in ordine — IVAO → poligoni GitHub (`twrs.tfl`) → sectorfile →
**ATZ dell'AIP** → e solo alla fine il cerchio da 5 NM. Che siano finite sul cerchio vuol dire che le prime
tre non le avevano.

⚠️ **Ma la quarta oggi non può girare**: gli ATZ vengono dal catalogo degli spazi aerei dell'AIP, che si
carica **a mano** da un KMZ (§AA), e in produzione l'archivio è ripartito da zero — quel catalogo è **vuoto**.
Alla misura del 29 agosto quel file dava un contorno vero a **13 torri su 84**.

**🟡 Da fare, quando si vuole**: caricare il KMZ da `/services/vsop/admin/airspace`. **Nessuna migrazione**,
quindi si può fare dentro la finestra cieca. Oltre alle torri restituisce gli **agganci CTR** degli
avvicinamenti (Catania sette zone, Amendola due), che è il motivo per cui §AA esiste.
Le torri che nemmeno l'AIP copre restano sul cerchio, ed è la risposta giusta: meglio un cerchio
**dichiarato per quello che è** che nessuna area.

#### `LIRR_TS_CTR`, anello ripetuto — ✅ difetto della sorgente

IVAO manda lo stesso anello due volte (66 punti). L'applicazione lo ripara in lettura, e l'avviso esiste
solo per dire che **senza** quella riparazione quel settore avrebbe traffico zero. Nessuna azione.

#### 🗑️ `errori-richieste.txt` va cancellato

299 righe, ma **sette voci sole e tutte del 24 agosto** (17:43–17:44 UTC, VID 713322, la pagina aeroporti di
LIBB): il residuo dell'epoca delle corse sul `DbContext`. **Nessun errore nuovo dopo l'1.1.0.**

> 🔴 **Smentito il 31 agosto, in giornata.** La frase qui sopra e' rimasta vera per poche ore: lo
> stesso giorno il file e' tornato a **634 kB** con **tre** errori nuovi (11:31→11:40 UTC), e `avvii.txt`
> ha registrato **due morti male del processo**. Vedi **§AM** — e ⚠️ il primo dei quattro difetti era
> proprio lo strumento che scrive questo file.

⚠️ Vale la regola già imparata con `avvio-errore.txt`: un file di errori vecchi che resta lì fa suonare un
allarme a **ogni** controllo futuro — è già costato otto giorni a inseguire un guasto del 16 agosto.

ℹ️ La copia della cartella `diagnostica/` presa dal server sta in **`vIPI Ivao Italy\diagnostica`**, cioè
**fuori** dalla cartella del repository: non finisce in git. Dentro non ci sono credenziali (di ogni valore
si dice *se* c'è, mai quale), ma ci sono stack trace e un VID.

### 🔒 Dal 31 agosto al 16 settembre non si consegna database

Chi lo amministra in Ivao.It è via. ⚠️ **Lo schema però NON è congelato**: in produzione gira
`Database.Migrate()` all'avvio, sul pacchetto che il committente carica via FTP. Quindi una migrazione in
quella finestra gira **da sola, su DDL non transazionale, senza nessuno che possa ripristinare**.

Il presidio è `tests/Vipi.Infrastructure.Tests/MigrazioniDellaFinestraCiecaTests.cs` — e **va cancellato
quando la finestra si chiude**, non aggiornato spostando le date. ⚠️ **S11 non va spedita in questa
finestra.**

---

✅ **Fino al pomeriggio del 30 agosto non c'era nessun ramo con lavoro fuori**, e `main` era allineato con
`origin/main` a **`30363753`**. ⚠️ Da quella sera i **nove commit di `consegna-db-20260830` sono da
spingere** (riquadro qui sopra).

> ℹ️ **Quale codice sta sul server, e non è la testa del ramo.** Il pacchetto `j` è stato costruito da
> **`2e96bbc8`**, e a quel commit `src/` era **identico a `main`**: fino a lì il ramo toccava solo test e
> documentazione. I due commit successivi — il passaggio della versione da lettera a numero — cambiano
> `src/Vipi.Host/VersioneBuild.cs` e `Vipi.Host.csproj`, quindi **la testa del ramo non è più ciò che gira**.
> Non è un problema (quel codice va nel prossimo pacchetto, che sarà `1.0.0`), ma chi confronta deve
> confrontare con `2e96bbc8`, non con `HEAD`.

### 📦 31 agosto — i due rami fuori sono confluiti in `consegna-20260831`

Il ramo di consegna parte da `consegna-db-20260830` (cioè dal codice del pacchetto `j`, che è quello online)
e ci fonde sopra:

- **`intro-di-pagina`** — quindici commit, §AC e §AD. Sezioni editabili in cima agli elenchi + la verifica
  «uscire non butta via». **Zero migrazioni** (vivono in `SharedBlocks`, tabella dell'`InitialCreate`).
- **`riconnessione-circuito`** — §AE. Le quattro mosse contro «Attempting to reconnect to the server…».
  **Zero migrazioni.**

⚠️ **Dentro la finestra cieca questo è il punto**: il pacchetto si consegna da solo, via FTP, e non tocca lo
schema. Versione **1.1.0** (funzionalità nuove, nessuna migrazione).

✅ **CARICATO IL 31 AGOSTO, e il sito gira su 1.1.0.** La verifica dall'esterno e la lettura della
cartella `diagnostica/` stanno in **§A15**. Quel che resta da fare dopo il deploy è **§A16**.

Il 30 agosto sono
stati fusi e spinti, in quest'ordine: **`biblioteca-allegati`** (§E10), **`spazi-aerei-aip`** (§AA) e
**`shape-una-porta-sola`** (§AB, carta [refactor/15](refactor/15-shape-del-settore-una-porta-sola.md),
quattordici commit); tutti e tre cancellati dopo la fusione. **Release verde e nove assiemi su nove verdi
DOPO l'ultima fusione**, E2E compresi (255).

⚠️ **Dal 31 agosto 2026 un ramo fuori c'è: `riconnessione-circuito`**, aperto da `main` — le quattro mosse
contro «Attempting to reconnect to the server…» (§AC). **Nessuna migrazione**, quindi non tocca la finestra
cieca sullo schema; ma porta un `.js` nuovo che il pacchetto **deve** contenere, vedi l'avviso in §AC punto 2.

⚠️ **La trappola della fusione, che git non segnala.** I due rami dicevano tutti e due «16 voci nella barra
admin» perché ognuno ne aggiungeva **una** a quindici. Git ha fuso due numeri identici senza chiamarlo un
conflitto — il conflitto era sui **commenti** accanto — e fuse le voci sono **due**: 17 e 12. Se n'è accorto
solo `AdminNavTests`. ⚠️ I due **ModelSnapshot** git li ha fusi bene, ma la prova non è che i nomi ci siano:
è che una **migrazione di prova esca VUOTA** su tutti e due i provider.

### Che cosa è entrato il 30 agosto

- 🆕 **§AC — l'intro di pagina** ([carta](feature/2026-08-30-intro-di-pagina.md)), sul **ramo**. Sezioni
  editabili in cima a `/services/vsop/mil`, con i **PDF della biblioteca** dentro. Vivono in
  **`SharedBlocks`** — c'era dall'`InitialCreate` e **non la scriveva nessuno** — quindi **zero DDL** dentro
  la finestra cieca. ⚠️ **Non è un documento**: si salva ed è **pubblico**, niente release da riaprire; il
  normativo va in un documento. Tradotta, e regge solo perché le frasi entrano nel **corpus**.
- 🆕 **§AD — «uscire non butta via»**, la verifica su tutto ciò che si modifica, sullo stesso ramo. I quattro
  editor documentali **salvano a ogni gesto** (sani); l'**aeroporto** è l'unico che accumula, e l'uscita non
  guardava niente. ⚠️ La guardia `beforeunload` copriva **un buffer su tre**, e il pannello settori si
  marcava sporco **per sempre**: due difetti opposti nello stesso posto.
- **§AB — la shape di un settore ha una porta sola**, S0→S10
  ([carta](refactor/15-shape-del-settore-una-porta-sola.md)). L'aggancio agli spazi aerei dell'AIP era
  onorato da **due motori su sei**: un settore agganciato **disegnava** il confine dell'AIP nel documento e
  **rivendicava** il traffico dentro il monoblocco di IVAO. Adesso la forma — **anello e quote insieme** —
  la dà `ISectorShapeResolver`, e ogni pezzo porta **la sua banda**: su `LIBA_APP` l'inviluppo
  (`GND → FL195`) coincideva col monoblocco, quindi il 3D disegnava un parallelepipedo dove il cielo vero ha
  **due gradini**. ⚠️ **Resta S11** (§4-bis della carta): non è una slice.
- **§AA — gli spazi aerei dell'AIP**, tutte e sette le slice ([carta](feature/2026-08-29-spazi-aerei-dal-kmz.md)).
  Il file KMZ si carica a mano, e un avvicinamento può disegnare il **CTR che controlla davvero**: Catania
  sono sette zone, e da IVAO arriva un poligono solo. ⚠️ Il file contiene **scatole 3D**, non contorni.
  ⚠️ **§6-bis**: l'aggancio **non scrive** la shape — quella colonna tiene **un anello**. Più l'ATZ al posto
  del cerchio (**13 torri su 84**), la pagina `/services/vsop/airspace` (nata pubblica, dal 1 settembre 2026
  riservata allo **staff di divisione**), il convertitore che pesca dal
  catalogo e il rapporto radioassistenze.
- **§E10 — la biblioteca allegati**, e ✅ **R8 chiuso**: l'embed di Drive provato dal vivo con due PDF veri.
  ⚠️ E ha trovato che in `frame-src` mancava **`'self'`** (l'iframe punta alla **nostra** rotta, non a
  Drive): sarebbe morto al passaggio a CSP vera. Più `v@r.VersionNumber` stampato alla lettera in ogni riga
  — la regola di Razor per gli indirizzi email.
- **§Q16 chiusa tutta**: le frasi di partenza di una vLOA sono **parola nostra** e si seminano invece di
  comprarle (una tornava rotta a ogni giro, 155 caratteri ogni quarto d'ora), e la spesa ora si **conta**
  (`TranslationSpends`) invece di dedurla dalla memoria. ⚠️ Con la **fotografia iniziale**, o il tetto
  ripartirebbe da zero.

Il **cutover MariaDB** è in `main` e verificato (A1–A8). Le sezioni **B**, **C**, **D**, **G**, **H** sono
chiuse o chiuse-con-la-ragione-scritta; **I** è sospesa di proposito; **J**–**Z**, **§AA** ed **§E10** sono
chiuse — di **§Y** resta aperto il solo **BOAT** (§Y10), che è del committente.

⚠️ **Le migrazioni in coda al cutover sono TRENTASEI**: ventisei erano in `main`, più due della
biblioteca (`BibliotecaAllegati`), quattro degli spazi aerei (`CatalogoSpaziAerei`, `AgganciSpaziAerei`),
due del registro della spesa (`RegistroSpesaTraduzione`) e **due di §AB** (`PezziDiForma`,
`FormaCheHaContato`). ⚠️ Quella di §AB sul traffico porta un valore di partenza **scritto a mano**
(`Source`): EF genera `defaultValue: ""` per una colonna enum-a-stringa, e le 1 853 tratte già in archivio
sarebbero diventate **illeggibili** al primo caricamento. Tutte **additive**. Le sei prima di quelle le
porta §Y. ⚠️ **Quella della correzione delle radioassistenze non è innocua**: `DELETE FROM Navaids` **e**
`DELETE FROM ImportStates WHERE Category='Navaid'` — svuota l'anagrafica e azzera lo stato d'import, così al
primo avvio il giro la rifà da zero. Ci mette un minuto, e nel frattempo le tabelle dei SOP sono vuote.
⚠️ Alcune migrazioni sono datate **25-ago 15:19**, quindi su un DB già aggiornato EF le applicherà **fuori
ordine** — lecito, ma da sapere. ✅ La SELECT dei duplicati su `DocReleases` la fa l'applicazione da sé (§T).

🔴 **Le cose da fare SUBITO DOPO il deploy** — le prime due nessuna le fa il codice da sé, la terza la fa
lui e va **guardata**:

1. **Premere il re-import delle piste** su `/services/vsop/admin/airports` (§Y10): in produzione le
   coordinate delle soglie nascono vuote, e la sezione lo dice invece di non comparire.
2. **Riempire il tipo delle radioassistenze** da `/services/vsop/admin/navaids` (§Y12): dopo l'import ne
   restano **122 senza tipo**, e il filtro «senza tipo» è la lista di lavoro. La sorgente il tipo non lo sa —
   `itvor.vor` tiene VOR, TACAN e VORTAC insieme — e sui documenti si legge un trattino finché non lo scrive
   una persona. 🆕 Il **rapporto degli spazi aerei** ne propone **53**: dice che cosa ne pensa l'AIP, non lo
   scrive.
3. 🆕 **Guardare la conversione delle 13 torri ATZ** (§AB, S3). Al primo giro d'import delle posizioni
   d'aeroporto le 13 righe con `ShapeSource='Aip'` in colonna devono diventare **pezzi**, e la colonna
   tornare **libera** — è quel che rende l'ATZ sganciabile. Nove test la coprono, ma dal vivo non è mai
   girata: il giro d'import non è partito nella sessione di verifica. Si controlla così:
   `select ShapeSource, count(*) from AirportSectors group by 1` deve perdere le 13 `Aip`, e
   `select Source, count(*) from SectorShapeParts group by 1` deve guadagnarle.

✅ **Quella dei permessi è caduta**: l'annuncio del cambio (§U) l'ha fatto il committente il 30 agosto.
Resta il **fatto**: al deploy gli `IT-` fuori dagli otto codici di direzione smettono di editare, e chi deve
editare si promuove a mano da `/services/vsop/admin/permissions`.

🆕 **Da annunciare quando il deploy va**: la pagina **Allegati** esiste, e un PDF si carica **prima** sul
Drive di divisione e poi se ne incolla il link. ⚠️ E lo slug proposto **non si cambia col triplo clic** —
lascia il valore vecchio attaccato al nuovo; con **Ctrl+A** o col Backspace funziona. È il comportamento dei
campi «controllati» (`value=@…` + `@oninput`), che in questa applicazione sono **19 file**: non è della
biblioteca, ed è un lavoro a sé, mai aperto.
⚠️ **Tre passi d'avvio** nuovi rispetto al 26: `LinkAirportDocumentsAsync` (dai rami fusi),
`ClearVloaSeededAiracRowAsync` e `ClearUnpublishedCurrentVersionAsync` (§L). Tutti idempotenti e tutti si
scrivono nei log.

🔵 **Quel che aspetta il committente, oggi**

| Cosa | Dove |
|---|---|
| **Ripubblicare le quattro vLOA** — quattro clic, la lista «Da fare» le indica | §L |
| 🆕 **Caricare il file dell'AIP** in produzione, e agganciare i CTR agli avvicinamenti che li controllano | **§AA** |
| Le risposte di Ivao.It, fra cui **chi fa il backup** | A9 / A13 |
| La **rotazione** dei segreti esposti il 24-25 agosto | A13 |
| Le decisioni di contenuto (LIBB: due sezioni nascoste a mano; l'elenco aeroporti da 75 righe) | K / statistiche |
| Se `99999 ft` debba diventare `UNL` a schermo | N4 |
| Se prendere in carico le **tessere nostre** (l'unico fondo che nessuno può chiudere) | P1 |
| ~~Chi cura il glossario di fraseologia~~ — ✅ **CHIUSA il 28-ago**: meccanismo, pagina di cura, e la risposta del committente — **tutti gli admin**, cioè tutto lo staff di divisione | **Q3** |
| I **termini di ritenzione** del piano gratuito Azure, e la domanda a IVAO HQ sul trattamento esterno | Q1 / Q2 |
| **Rileggere la trascrizione di LIPI** e le **figure** dei SOP da estrarre dai PDF | R1 / R2 |
| Gli altri **quattordici SOP** militari, e su quattro campi la sezione QRA da riempire | R3 |
| ~~Avvisare lo staff prima del deploy~~ — ✅ **FATTO il 30-ago**: resta il fatto, non l'annuncio | **U** |

🔵 **Resta deciso**: il database si ripulisce un'ultima volta prima di popolarlo — quindi **I1** (le radici
orfane di LIRR) è sospesa apposta: non si sistema un albero che sta per essere rifatto.

**Sezioni con lavoro aperto, oggi**: **nessuna sul codice** — `main` è l'unica cosa che conta.
Le ultime fuse sono §V, §W, §X e §Y (30 agosto); prima §U, §Q-bis, §S/§T, §P, §Q e §R. Restano **I3/I4**,
**N4**, **P1** (decisioni, non lavoro), la lingua sorgente della vLOA **Q4**, il **BOAT** di §Y10 e
**§X7** — **LIML** ha un vSOP militare pubblicato e nessuna vIPI civile, dato di prima della guardia, da
creare a mano — più tutto ciò che aspetta qualcun altro: A9, A13, L2, B10-bis, **Q1/Q2**, **R1/R2/R3**. ✅ **I due rossi intermittenti Q5 e Q6 sono
chiusi** il 28 agosto: erano due contese diverse fra test paralleli, entrambe riprodotte prima di
correggerle.

✅ **L'annuncio delle autorizzazioni a livelli è stato fatto** (committente, 30 agosto 2026): quel
prerequisito del deploy è caduto. Resta il **fatto**, che vale il giorno che questo `main` va in produzione:
tutti gli `IT-` fuori dagli otto codici di direzione (`IT-T01`, `IT-T03`, `IT-FOC`, `IT-FOAC`, `IT-AOA1`…)
**smettono di editare** — vedono le statistiche e basta. Chi ha bisogno di editare si promuove a mano da
`/services/vsop/admin/permissions`, trenta secondi a persona. Vedi **§U**.

⚠️ **Quel che il deploy aspetta ancora** è il **re-import piste** (in produzione le soglie nascono vuote) e
il **tipo** delle **122 radioassistenze**, che aspetta una persona.

---

## A. Cutover su `atc.it.ivao.aero` — la strada critica

✅ **Fuso in `main` il 9 agosto 2026**: il ramo `feat/persistenza-mysql` non è più il posto dove si lavora —
`main` è net8 + Pomelo + MariaDB. Contesto: [`design/piano-supporto-mysql.md`](design/piano-supporto-mysql.md),
decisioni in ADR-0007 §D4/§D4-bis (⚠️ entrambe **superate**, vedi A8).

Stato: il server è **MariaDB 11.4.10**, non MySQL. `Vipi.Host` è passato a **net8** e il provider è
**Pomelo**; suite verde su net8 (309) e net10 (300). **Dal 6 agosto 2026 il ramo è provato contro una
MariaDB 11.4.10 vera**: schema, collation, case-sensitivity, avvio dell'applicazione (A1), key-ring
Data Protection che sopravvive a un riavvio (A4) e **travaso dei dati veri da Neon fino al `.sql`
reimportabile** (A2/A3). Restano i flussi editoriali (A6) e la CI (A5).

**Dal 7 agosto 2026 il ramo porta anche B1+B2** (B4 deciso: in produzione va `main` + B1). Quel merge ha
richiesto tre correzioni che i test guardia hanno chiesto da soli, e che valgono come promemoria del costo
dichiarato in ADR-0007 — **ogni cambio di schema va emesso due volte**:
- migrazione MySQL **`20260807125819_SpecialAreasHardening`**, che copre le tre migrazioni SQLite delle aree
  in una sola (il set MySQL nasce il 5 agosto e non ha una storia da rispettare). ⚠️ Lo scaffold di EF metteva
  il `DropColumn` di `SpecialAreas.CenterId` **prima** del travaso: riordinato a mano e aggiunto il backfill,
  come nella gemella SQLite, o su un database con dati i legami sparivano in silenzio;
- `MySqlStringLengths`: `SpecialArea.CenterId` non esiste più, e le due colonne della PK composta di
  `SpecialAreaCenter` vanno lunghe **esattamente** come le principali (64 e 16) o è `errno 150`;
- lo smoke E2E del bridge spento pretendeva **405**: quel codice è di net10, dove il catch-all della pagina
  «non trovato» risponde al GET di qualunque path. Su net8 — l'host che va in produzione — è **404**.

### A1 ✅ MariaDB 11.4 in locale, e rifare le verifiche — eseguita il 6 agosto 2026
MariaDB **11.4.10** portable in `D:\Programmazione\IVAO_Test\_mariadb` (fuori dal repo), porta 3399,
default di server `utf8mb4_uca1400_ai_ci` come il pacchetto Debian loro, database `itivao_atc` creato
**senza** `COLLATE` e utente con permessi **solo** su quel database. Ricetta completa e trappole:
[`../deploy/mariadb/README.md`](../deploy/mariadb/README.md).

Le quattro verifiche, tutte passate:
1. **Migrazioni da zero su database vuoto** — sia da `dotnet ef database update`, sia **all'avvio
   dell'host** (`MigrateVipiDatabase`), che è il percorso vero del cutover. 38 tabelle, una sola riga in
   `__EFMigrationsHistory`.
2. **Collation** — **163** colonne stringa su 163 con `utf8mb4_uca1400_as_cs`, dichiarata sulla colonna
   nella DDL vera (`SHOW CREATE TABLE`). Le 2 eccezioni sono `__EFMigrationsHistory`, che EF crea prima
   della nostra migrazione: innocue, quei valori li genera e li confronta EF.
3. **`LIRF` e `lirf` convivono** nell'indice unico di `Accs.Code` e `WHERE Code='lirf'` ne torna uno solo.
   È la verifica che conta: il default del database è ai_ci, quindi senza la collation sulla colonna il
   secondo INSERT sarebbe stato un duplicate key.
4. **Avvio con `Persistence__Provider=MySql`** — `/services/vsop` 200, `/vsop/health` Healthy, `/vsop/health/ready`
   200, log senza un solo `warn`. E ha **scritto davvero**: import ACC (7 ACC, 36 settori) e aree speciali
   (230 create, 17 aggiornate) andati a buon fine su MariaDB, con `LastSuccessUtc` valorizzato e
   `LastError` vuoto in `ImportStates`.

Tre cose imparate, che valgono per il cutover:
- **Pomelo emette `ALTER DATABASE CHARACTER SET utf8mb4;`** come prima istruzione della migrazione. Passa
  con un `GRANT ALL ON itivao_atc.*` (è ALTER *sul database*), ma se il loro utente avesse una lista di
  privilegi ritagliata è la riga che si pianta per prima. → domanda per A9.
- **`lower_case_table_names`** è 1 su Windows e sarà 0 sul loro Linux: col default le tabelle si salvano
  come `accs`, là esisterebbero solo come `Accs`. Sembrava riguardare solo le `TRUNCATE` scritte a mano;
  in A3 si è scoperto che avvelena il **dump**, e il `my.ini` ora porta `lower-case-table-names=2`.
- `mariadb-install-db` su Windows crea **utenti anonimi** che dirottano le connessioni da 127.0.0.1: il
  sintomo è `Access denied ... (using password: YES)`, che accusa la password mentre il problema è l'utente.

Restano **non** verificati e non verificabili qui: `sql_mode` del loro server, e la loro
`DEFAULT_COLLATION_NAME` vera (mai letta). Vedi «Cosa questo ambiente NON dice» nel README.

ℹ️ Non è un guasto: l'import settori-aeroporto ha scritto **0 aeroporti** perché parte dal catalogo
aeroporti, che su un database appena creato è vuoto finché non si passa da `/services/vsop/admin/acc` → «Importa da
sorgente». Da riprendere in **A6**, che è dove i flussi si guidano.

ℹ️ Osservazione di modello, non urgente: `Accs.Code` porta **due** indici unici, `AK_Accs_Code` (chiave
alternativa, bersaglio della FK da `AccSector.AccCode`) e `IX_Accs_Code` (`HasIndex(...).IsUnique()`,
`VipiDbContext.cs:66`). Ridondanza vecchia, presente anche su SQLite: costa una scrittura d'indice in più,
non un difetto. Toglierla vorrebbe dire toccare entrambi i set di migrazioni.

### A2 ✅ `Vipi.DbSeed` a net8 — fatta il 6 agosto 2026
Tool su **net8** (come l'host, e per lo stesso motivo: Pomelo), sorgente **SQLite o Postgres**, destinazione
**Postgres o MariaDB**. I due punti Postgres-specifici sono sostituiti come da §S8: `TRUNCATE … RESTART
IDENTITY CASCADE` → `SET FOREIGN_KEY_CHECKS=0` + `TRUNCATE` per tabella (**riacceso subito dopo**, così gli
INSERT restano verificati), e `setval` → `ALTER TABLE … AUTO_INCREMENT = max+1` sulle sole colonne che un
contatore ce l'hanno davvero, chieste a `information_schema`. Conservati il trucco a due fasi per
`Document↔DocumentVersion` e la normalizzazione `DateTimeKind.Utc`, che su MariaDB serve per un motivo
diverso: `DATETIME` non porta fuso, quindi senza normalizzare a monte lo deciderebbe la macchina.

Tre aggiunte non chieste ma che il travaso vero (A3) userà:
- **Riconciliazione riga per riga** in fondo, con **uscita in errore** se una tabella non combacia. A3 chiede
  di riconciliare per conteggio: ora lo fa il tool, invece che l'occhio di chi guarda il log.
- **Verifica dello schema di destinazione prima del wipe**: su MariaDB si ferma elencando le tabelle mancanti
  e il comando da lanciare, invece di svuotare e poi fallire l'INSERT.
- **Riga di comando a flag** (`--from-postgres … --to-mysql …`): con due capi variabili e un TRUNCATE in
  mezzo, due connection string posizionali si invertono e l'errore si scopre a database svuotato.

**Eseguito**: `--from-sqlite src/Vipi.Host/vipi.db --to-mysql <MariaDB 11.4.10 locale>` → 4578 righe lette,
4588 scritte, 36 contatori risincronizzati, **37 tabelle su 37 combaciano**, e l'host avviato su quel
database serve le pagine con i dati veri (`/services/vsop` mostra LIRR/LIMM/LIBB).

✅ **Il percorso sorgente-Postgres è stato eseguito il 9 agosto 2026** (A3): 4303 righe da Neon, 4314 scritte
su MariaDB, 38/38 tabelle riconciliate. Era l'unico pezzo del tool che nessuno aveva visto girare.

ℹ️ `/vsop/health` su quel database dice **Degraded**. Non è il travaso né MariaDB: l'host avviato sullo
**stesso `vipi.db` via SQLite** dice Degraded uguale. Sono le incongruenze soft-ref già note (E2: gerarchia
`ParentCallsign` dangling), che viaggiano coi dati.

### A3 ✅ Travaso dei dati veri — catena eseguita il 6 agosto 2026
`Neon → DbSeed → MariaDB locale → mariadb-dump → .sql`, tutta. Da Neon: **4807 righe** lette, 4818 scritte,
**37 tabelle su 37 riconciliate** dal tool. Dump: `_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-06.sql`,
4,7 MB, sha256 `B4989D63…A475A296`. Procedura e opzioni: §6 di
[`../deploy/mariadb/README.md`](../deploy/mariadb/README.md).

**Verificato reimportandolo**, non guardandolo: database vuoto → import → 38 tabelle, 4808 righe, conteggi
**identici** all'origine; host avviato su quel database → `/services/vsop` 200 con LIRR/LIMM/LIBB a schermo e
**nessuna migrazione riapplicata** (`__EFMigrationsHistory` viaggia nel dump apposta).

⚠️ **Il primo dump era inutilizzabile e sembrava perfetto.** Windows nasce con
`lower_case_table_names=1`: i nomi di tabella si salvano in minuscolo e `mariadb-dump` li riemette così, per
cui sul loro Linux (`=0`) sarebbero nate `accs` mentre EF cerca `Accs`. Rifatto tutto con
`lower-case-table-names=2` nel `my.ini` — lo 0 su Windows corrompe gli indici — da mettere **prima** di
creare lo schema. È la trappola annotata in A1, materializzatasi al primo tentativo.

Il `.sql` **non è nel repository**: contiene contenuto reale, VID dello staff e audit log. Sta in
`_mariadb/dump/`, fuori dall'albero.

**Rifatto dopo il merge di B1 — 9 agosto 2026, ed è questo il dump buono.**
`_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-09.sql`, 4,0 MB, sha256
`1CD77F3A5428AA55ECB85F96DB9D8939224C4974D0DA2694F8BFA7801B562DFC`. Da Neon **4303 righe**, 4314 scritte,
38/38 tabelle riconciliate; riletto in un database vuoto: **39/39 tabelle combaciano**, 4305 righe, e i
legami multi-ACC sopravvivono al giro (223 aree con un ente, 4 con tre, 3 con quattro = 247). I due dump
precedenti (06 e 07 agosto) sono **superati**: il primo ha l'archivio vecchio da 993 legami, il secondo il
solo backfill da 230.

⚠️ **Il dump del 7 agosto sembrava a posto e non lo era.** Dopo il deploy di B1 l'archivio aveva 230 legami,
uno per area: la firma del **solo backfill**. Il motivo non era un guasto ma il **gate a 24h** di
`ImportState` — l'ultimo import aree era del 6 agosto 18:15, quindi al boot veniva saltato, e solo il
bottone manuale lo scavalca. Premuto quello, i legami sono tornati **247**. Il controllo che lo rivela è
`SpecialAreaCenters == SpecialAreas`: se i due numeri coincidono, l'import delle aree non è ancora girato.

L'import è stato lanciato **da un host locale puntato a Neon** (`Persistence__Provider=Postgres` +
connection string di Neon), non dal sito: usa il secret IVAO dei user-secrets locali, che funziona, mentre
quello su Render risultava stale il 5 agosto. Esito: «ACC: 0 create, 7 aggiornate · settori ACC: 0 create,
147 aggiornati».

ℹ️ La pagina è **`/services/vsop/admin/acc`**, al singolare — `/services/vsop/admin/accs` non esiste e risponde 404 (su net8
non c'è nemmeno il catch-all che su net10 darebbe altro). Il bottone è inoltre inerte finché non si prende
il **lock di risorsa** dalla barra in cima: `OnLockChanged(mine)` è ciò che accende `_canEdit`.

⚠️⚠️ **14 agosto: tutti e tre i dump — 6, 7 e 9 agosto — hanno il BOM, e sono da rifare.** La ricetta usava
la redirezione di PowerShell 5.1, che scrive UTF-8 **con BOM** e converte i fine riga in CRLF. Quei tre byte
finiscono davanti alla prima istruzione del file, e sul loro Linux `mariadb < file.sql` muore con un
`ERROR 1064` **alla riga 1** — un errore che parla di sintassi mentre il problema è la codifica, cioè il
modo peggiore di scoprirlo, in call con loro il giorno del cutover. Non era una svista di un giorno: era
nella ricetta, quindi in tutti i dump prodotti fin qui.

Si rifà da una shell che scrive byte grezzi (Git Bash, `cmd`) e **si controlla prima di consegnare**: i primi
quattro byte devono essere `2f 2a 4d 21` (`/*M!`) e non `ef bb bf`.
```sh
od -A n -t x1 -N 4 vipi-atc-it-ivao-aero-<data>.sql
```
Ricetta corretta e spiegazione in [`../deploy/mariadb/README.md`](../deploy/mariadb/README.md) §6.

**Cosa resta, e non è lavoro tecnico:**
- **Consegnarlo**, per il canale che concorderanno (A9) — con 4 MB va verificato che phpMyAdmin regga.
- **Rifarlo poco prima del cutover**: fra oggi e il passaggio, Render continua a essere modificato. Stesso
  comando, due minuti — e prima di rifarlo, premere «Importa da sorgente» e ricontrollare quei due conteggi.
  ⚠️ Va comunque rifatto **subito**, BOM o no: quello in mano oggi non è consegnabile.
- Escluso di proposito dal dump: `DataProtectionKeys`. Sono le chiavi che decifrano i cookie della nostra
  installazione locale, non un dato da consegnare; l'host se le ricrea al primo avvio.

### A4 ✅ Data Protection su MariaDB — fatta il 6 agosto 2026
`VipiDataProtection` montava il key-store su DB **solo se il provider era Postgres**; sotto MariaDB
ricadeva sul file-store, cioè antiforgery rotto e utenti sloggati a ogni riavvio su disco effimero. Ora la
decisione «questo provider tiene le chiavi nel database» sta in
`Vipi.Infrastructure/Persistence/DataProtectionSchema.cs` — funzione pura, un caso per provider — e l'host
fa solo il wiring. Su MariaDB il context usa Pomelo con la **versione fissata** (come `DependencyInjection`:
`AutoDetect` aprirebbe una connessione mentre si costruiscono le opzioni) e **senza** retry, che su Neon
serve per il risveglio del compute e qui non avrebbe motivo.

**Verificato sopravvivendo a un riavvio, non per ispezione**: primo avvio → tabella creata e chiave
`key-454f958a…` scritta in `DataProtectionKeys`; riavvio → **la stessa chiave, una sola riga**, nessuna
chiave nuova, `AUTO_INCREMENT` fermo. Se il key-ring fosse tornato sul file-store ne sarebbe nata una seconda.

+7 test per target (`DataProtectionSchemaTests`, Infra **316** su net8 e **307** su net10): coprono il set
di provider, l'idempotenza della DDL, il **nome della tabella con le maiuscole** — su Linux
`lower_case_table_names=0`, e una `dataprotectionkeys` minuscola non sarebbe quella che EF cerca — e la
collation, che qui va dichiarata a mano perché la tabella non è nel modello e `MySqlCollation.Apply` non la
raggiunge.

⚠️ Non verificato: che un **cookie** emesso prima del riavvio venga ancora decifrato dopo. La prova richiede
un login vero (`VipiAuth:Enabled=true`), quindi va con A6 o col primo login su `atc.it.ivao.aero`.

### A5 ✅ CI con MariaDB — fatta il 6 agosto 2026
Job nuovo **`mariadb-schema`**: servizio `mariadb:11.4.10` — la versione esatta loro, non il tag mobile
`11.4` — con database creato **senza `COLLATE`** e utente con permessi **solo** su quello, come da loro.
Applica le migrazioni e poi verifica **sul database**: nessuna colonna stringa fuori da
`utf8mb4_uca1400_as_cs` (attese esattamente 2, quelle di `__EFMigrationsHistory`), `LIRF` e `lirf` che
convivono nell'indice unico mentre il `WHERE` li distingue, e le tabelle nate con le maiuscole giuste.

**La terza verifica si può fare solo lì.** Su Windows `lower_case_table_names` vale 1 o 2 e la differenza
non è osservabile: la CI su Linux è l'unico posto dove il guasto che ha rovinato il primo dump di A3 può
essere colto prima di arrivare da loro.

**Due job erano rossi da prima, e non per MariaDB:**
- `docker-image` falliva da quando l'host è net8: il Dockerfile pubblicava un'applicazione net8 dentro
  `aspnet:10.0`. Build e publish riescono lo stesso — il container muore all'avvio con
  «Microsoft.NETCore.App version 8.0.0 not found». Immagine finale portata a **`aspnet:8.0`**; lo stage di
  build resta su `sdk:10.0`. ⚠️ **Vale anche per il deploy su Render**, che usa questo Dockerfile: senza
  questa correzione il primo deploy dopo il merge sarebbe morto all'avvio.
- I test del ramo net8 giravano **in roll-forward sul runtime 10**, perché in CI c'era il solo SDK 10:
  assembly giusta, runtime sbagliato, proprio sul ramo che va in produzione. Aggiunto il runtime 8.

Tre inciampi di CI, tutti nel job nuovo e tutti risolti: `dotnet ef` non si risolve dalla dispatch della
CLI su un runner senza manifest di tool (si invoca `~/.dotnet/tools/dotnet-ef` per percorso), e `dotnet ef`
compila ma **non restora** (senza `dotnet restore` esplicito muore con `NETSDK1004`, che parla di NuGet e
non di migrazioni).

Esito: **quattro job su quattro verdi**.

### A6 ✅ Verifica live sui flussi editoriali — **chiusa il 9 agosto 2026**
Guidata con la skill `verifica-live` su `Vipi.Host` con `Persistence__Provider=MySql` contro la MariaDB
11.4.10 locale, caricata col **travaso vero da Neon** (A3), non con dati finti. `/vsop/health` e
`/vsop/health/ready` **Healthy**.

**Due bug trovati, entrambi corretti e con la loro rete di test.** Nessuno dei due è colpa del provider:
sono corse che MariaDB rende sistematiche, e che su SQLite e Postgres capitano solo con la tempistica
giusta — cioè nel modo peggiore.
- **`/services/vsop/admin/transfers` e `/services/vsop/admin/permissions` non si aprivano affatto**: leggono
  `IStationResolver` dal **markup**, e il lazy-load partiva durante il render sul `DbContext` del circuito
  ⇒ «A second operation was started», circuito morto (la pagina restava al prerender, che a occhio sembra
  viva). Sistemate con `Stations.Prewarm()` nel ciclo di vita, come già facevano `AccVipiPage`, `SopHome` e
  `VloaListPage` dal 29 luglio: queste due erano rimaste indietro. Guardia: `StationResolverPrewarmTests`
  cammina i `.razor` e pretende il Prewarm da **ogni** componente interattivo che legge il resolver nel
  render — il chrome statico (`SopLayout`) è escluso per costruzione, non per elenco.
- **`/services/vsop/live/{callsign}` uccideva il circuito**: `LoadAsync` ha **due** ingressi non coordinati — il
  ciclo di vita e il callback SSE `OnLiveUpdate`, che il poller invoca a ogni giro — e un aggiornamento che
  atterra a lettura in corso ne fa partire una seconda sullo stesso context. Serializzati con un
  `SemaphoreSlim`. Guardia: `LivePageConcurrencyTests` lancia i due ingressi in parallelo contro un servizio
  che si accorge della sovrapposizione. Entrambi i test sono stati **visti fallire** senza la correzione.

**Flussi esercitati e passati:** import ACC (7 aggiornati) e settori (147) — girati sia al boot sia dal
bottone manuale; import settori-aeroporto; **import SID per-ICAO** dall'editor («SID LIBC: 16 estratti»);
**pubblicazione** dall'editor aeroporto LIBC (release v6 ciclo 2608 `Effective`, la v5 archiviata a
`Superseded`, snapshot da 3355 byte); **lock di risorsa** (preso e rilasciato dalla barra); **ricerca
globale**; **vista live** per callsign; **blob**: le tre immagini in archivio escono dall'endpoint con byte
identici alla colonna `longblob` (179286 / 146102 / 280283).

⚠️ **La collation `as_cs` NON ha reso la ricerca sensibile alle maiuscole** — era il rischio dichiarato in
`MySqlCollation`: `LIBC`/`libc` danno 2 risultati, `Crotone`/`crotone` 1. Verificato, non dedotto.

**Seconda passata, 9 agosto: chiusi i tre buchi che restavano.**
- **Scrittura del blob** ✅ «+ Image» in una sezione extra dell'aeroporto: PNG da 694 byte caricato, salvato,
  e riletto dall'endpoint con **sha256 identico** a quello del file di partenza. `longblob` regge andata e
  ritorno.
- **Pubblicazione degli altri due tipi** ✅ vIPI **ACC** (release v16, payload 62 KB, con la derivazione
  pesante che gira al publish) e **vLOA** (release v2, payload **73 KB**). ⚠️ L'editor vLOA non si apre con
  `?doc=`: vuole `/services/vsop/{acc}/vloa/editor?acc={estero}` — con `?doc=8` si finisce sull'editor ACC e si
  pubblica quello, cosa che è successa al primo tentativo.
- **`sql_mode` non-strict** ✅ provato davvero, mettendo il server in `NO_ENGINE_SUBSTITUTION` e rifacendo la
  passata: **nessuna differenza** sulle pagine esercitate. Le uniche due che cambiavano erano quelle col
  METAR live, cioè dato che cambia da sé. Non è una dimostrazione — è l'assenza di sintomi sulla superficie
  provata — ma la domanda ad A9 resta per prudenza, non per ignoranza.

ℹ️ Osservazione, non guasto: **pubblicare una release non scrive audit**. L'audit lo scrive solo la
promozione di una bozza (`EfReleaseRepository.PromoteDraftAsync`). È una scelta di prodotto da confermare,
visto che il viewer dell'audit è fra i lavori aperti (E5).

### A7 ✅ Nuovo pacchetto di deploy — fatto il 9 agosto 2026
`artifacts/publish/vipi-linux-x64-mariadb-20260809.zip` — **47,8 MB**, self-contained **net8**
(sha256 `F17A0512E2D37AF7…`), con dentro `LEGGIMI-DEPLOY.md`, `appsettings.Production.json`,
`deploy/vipi.service` e `deploy/nginx-vipi.conf`. Rigenerato dopo la correzione C4, così il binario
consegnato contiene anche quella.

ℹ️ Il `LEGGIMI-DEPLOY.md` ora vive in **`deploy/atc-ivao/`**, cioè nel repository, e viene copiato nel
pacchetto: prima esisteva solo dentro `artifacts/`, che è gitignorata, e un `dotnet publish` che ripulisce
la cartella se lo portava via.

`LEGGIMI-DEPLOY.md` **riscritto**: diceva MySQL 8.4.9, provider Oracle e collation `utf8mb4_0900_as_cs` —
tre cose false su MariaDB. Ora dice MariaDB/Pomelo/`uca1400_as_cs`, aggiunge il passo «carica il `.sql`»
(che prima non esisteva: il documento dava per scontato un database vuoto), e chiede esplicitamente le due
impostazioni del loro server, `max_allowed_packet` ≥ 4 MB e `sql_mode`.

⚠️ **Dire a Ivao.It che il pacchetto del 5 agosto non funzionerà mai su quel server**: è compilato contro
un provider che non supporta MariaDB. Lo zip vecchio è ancora in `artifacts/publish/`: va tolto di mezzo
prima della consegna, o si consegna quello sbagliato.

⚠️ Il pacchetto **non è mai stato eseguito su Linux** — è compilato in modo incrociato da Windows. Il primo
avvio da loro è anche la prima prova su quel sistema, ed è scritto nel LEGGIMI.

### A8 ✅ Riscritte le decisioni che dicevano il falso — 9 agosto 2026
- **ADR-0007 §D4-ter** scritto: MariaDB 11.4.10, Pomelo 8.0.3, host net8, collation `utf8mb4_uca1400_as_cs`,
  migrazioni in assembly dedicato. Dice anche **perché** §D4-bis è caduta (era costruita su «il server è
  MySQL 8.0+», che era di seconda mano) e **quanto costa** questa scelta: schema doppio, ritorno del
  multi-target nei test, cache-busting degradato. §D4-bis è marcata superata in testa, com'era già §D4.
- **Piano MySQL**: avviso in cima che dichiara il documento superato, dice cosa resta valido (analisi dei
  rischi e catena del travaso §S8) e rimanda a questo elenco per lo stato reale.
- `guide/config.md`: tabella dei provider corretta, più l'avviso che `utf8mb4_0900_as_cs` su MariaDB **non
  esiste** e il requisito `max_allowed_packet` ≥ 4 MB.
- `guide/integration.md`: il ramo `MySql` esiste **solo su net8** (Pomelo); e non è più vero che il net8 non
  sia coperto dai test.
- `HANDOFF.md`: blocco di testa riscritto sullo stato verificato.
- Memorie aggiornate: `mysql-embedding-plan`, `multitarget-net8-embedding`, `deploy-hosting-options`.

### A9 🔴 Domande e conferme da Ivao.It
Messaggio pronto in appendice al piano, **da aggiornare** perché parla ancora di MySQL. Aperte:
- **Come raggiungiamo il database** (SSH? phpMyAdmin? IP autorizzato?) — decide se il travaso lo facciamo
  noi o gli consegniamo un file, e con quale limite di dimensione.
- **`sql_mode`** del server: strict o no (vedi A6). Da noi è `STRICT_TRANS_TABLES`; fuori da strict un CAST
  non numerico dà warning e 0 invece di lanciare, e quella classe di bug torna silenziosa.
- **`max_allowed_packet`**, che nessuno aveva ancora chiesto: le immagini dei blocchi sono `longblob` e
  viaggiano in un solo pacchetto. L'app taglia a **3 MB per immagine** (`MediaOptions.MaxUploadBytes`, 25 MB
  per documento), quindi basta che il loro valore sia **≥ 4 MB** — il default MariaDB è 16 MB, ma su hosting
  condiviso capita 1 MB, e allora gli upload sopra il mega fallirebbero al primo INSERT.
- **I privilegi dell'utente `itivao_atc`**, in dettaglio: la migrazione iniziale apre con
  `ALTER DATABASE CHARACTER SET utf8mb4;` (lo emette Pomelo, non noi). Con `GRANT ALL ON itivao_atc.*`
  passa — verificato in locale il 6 agosto — ma con una lista ritagliata è la prima riga che si pianta.
- ⚠️ **Chi fa il backup di `itivao_atc`, e con che frequenza.** Non è una conferma di cortesia: al 14 agosto
  2026 **nessuno sa rispondere**, e finché la risposta è «non lo so» va trattato come «il backup non esiste».
  ⚠️⚠️ **Dal 23 agosto è la domanda più urgente delle sei**, perché la procedura di aggiornamento (A11)
  comincia con un `DROP DATABASE`: il passo 1 del foglio è un dump fatto da loro, ed è l'unica rete sotto a
  quel comando. Se non sono in grado di farlo, l'aggiornamento non va cominciato.
  🟡 **23 agosto, sera — una mezza risposta**: il committente riferisce che **il backup lo fanno loro**
  (Ivao.It), «a quanto ho capito». È la prima notizia in nove giorni ed è verosimile — un hosting Plesk
  gestito di norma fa backup di vhost e database. Ma resta **riferita, non confermata**, e la regola scritta
  qui sopra vale ancora: finché non è confermata si pianifica come se non ci fosse. **Tre domande la
  chiudono**, e sono corte: (1) con che **frequenza** e quanta **retention**; (2) copre anche i **file del
  vhost** — cioè la cartella `public_atc/vipi-keys`, che **non sta nel database** e che un backup del solo
  MySQL non prenderebbe (perderla slogga tutti); (3) è mai stato provato un **ripristino**, o è solo
  configurato. La terza è quella che di solito sorprende.
  ℹ️ Da oggi la posta è più alta: fino a ieri in produzione c'erano i contenuti del 14 agosto, che erano una
  copia. Dal 23 c'è il contenuto **vero e corrente**, e la copia di sviluppo (`vipi.db`) diverge da lì in poi.
- ⚠️ **`itivao_atc` può fare `DROP DATABASE`?** `GRANT ALL ON itivao_atc.*` lo comprende, ma su Plesk
  l'utente lo crea il pannello e nessuno ha verificato la lista vera. Se non può, si svuota tabella per
  tabella o si consegna un `.sql` con i `DROP TABLE` in testa — è scritto nel foglio, ma è meglio saperlo
  prima che scoprirlo a metà.
  Il database tiene **tutto** lo stato dell'app, immagini comprese (ADR-0007): non c'è una seconda copia
  altrove da cui ricostruire. La `ReleaseRetention` **non** è un backup — pota di proposito. Se la risposta
  tarda, il paracadute è uno script `mariadb-dump` in cron, da concordare con loro.
- ⚠️ **Nei backup vanno due cose, non una**: il database e la **cartella delle chiavi** Data Protection,
  `/var/lib/vipi/keys` (§G). Perderla slogga tutti una volta sola, ma è un file solo ed è banale includerlo.
- Sulla macchina: **WebSocket** sul reverse proxy (senza, Blazor Server apre le pagine e resta muto),
  header inoltrati, supervisione del processo, e il **percorso persistente** per il key-ring (che dal 14
  agosto non sta più sul database: §G).

### A10 ✅ Redirect OIDC sul portale IVAO — chiusa dai fatti il 16 agosto 2026
`https://atc.it.ivao.aero/signin-oidc` e `/signout-callback-oidc` **sono registrati**: non perché qualcuno
l'abbia confermato, ma perché **il login in produzione funziona** dal 16 agosto — e senza quei due redirect
non tornerebbe indietro affatto. Stessa cosa per `VipiAuth:ClientSecret`, che nel file di produzione c'è.

ℹ️ La voce è rimasta 🔴 per una settimana dopo essere diventata vera: **una domanda in sospeso non si chiude
da sola quando la risposta arriva sotto forma di fatto** invece che di messaggio. Vale la pena rileggere le
🔴 dopo ogni deploy riuscito, e chiedersi quali abbia già risposto l'esercizio.

---

### A11 ✅ Consegna del 23 agosto 2026 — pacchetto **e** database, prodotti insieme
Il pacchetto e il `.sql` sono **due metà della stessa consegna** e non sono separabili: il codice del 23
agosto non sa leggere l'archivio del 15, perché in mezzo c'è il modello degli accordi a sezioni.

| | Cosa | Dove | sha256 |
|---|---|---|---|
| **Sito** | `vipi-linux-x64-mariadb-20260823.zip` — 48,7 MB, 421 file (418 da caricare: `deploy/` è riferimento), self-contained net8 | `artifacts/publish/` | `FE7F9FC8…E3FC4025` |
| **Database** | `vipi-atc-it-ivao-aero-2026-08-23.sql` — 3,1 MB, schema + dati + `__EFMigrationsHistory` | `_mariadb/dump/` (**fuori dal repo**) | `0861BE6A…C20969A` |

**La strategia è la sostituzione, non la migrazione**, ed è ciò che scioglie il blocco di E6-bis §9: il
`.sql` porta con sé lo schema già convertito e la storia delle migrazioni, quindi all'avvio l'applicazione
**non applica niente** e `AgreementSectionsFinalize` non ha modo di fallire. Il prezzo è dichiarato in testa
al foglio di aggiornamento: **si perde ciò che è stato scritto in produzione dal 16 agosto in poi**, e il
committente ha confermato che non c'è.

**Il foglio nuovo è [`../deploy/atc-ivao/LEGGIMI-AGGIORNAMENTO.md`](../deploy/atc-ivao/LEGGIMI-AGGIORNAMENTO.md)**:
finora esisteva solo la procedura di **prima installazione**, e per systemd. Questo è per un sito **già in
piedi** su Plesk+Passenger, e mette per iscritto le tre cose che l'FTP cancellerebbe senza chiedere —
`appsettings.Production.json`, `vipi-keys/`, `tmp/` — più l'ordine dei passi: **prima i file, poi il
database, poi `tmp/restart.txt`**, perché Passenger serve la versione vecchia finché non gli si dice di
ripartire e così la finestra di disallineamento dura secondi.

⚠️ **La consegna si divide in due canali, e il foglio di istruzioni sta nel canale sbagliato.** I file
dell'applicazione li carica il committente via FTP; il `.sql` va a chi in Ivao.It ha accesso al database.
Ma `LEGGIMI-AGGIORNAMENTO.md` sta **dentro lo zip**, che a loro non arriva. Per questo c'è
`artifacts/CONSEGNA-DB-20260823.md`: la sola parte del database, che si manda insieme al `.sql` — backup,
`DROP DATABASE`, import, e le due domande mai risposte (chi fa il backup, `max_allowed_packet`).

⚠️ Per la stessa ragione nel pacchetto **non c'è** `appsettings.Production.json`, ma
`appsettings.Production.json.esempio`: un file con quel nome, caricato via FTP, cancellerebbe la password
del database e le credenziali IVAO che stanno solo sul loro server — e il sito ripartirebbe **su SQLite
vuoto**, che è il modo peggiore di sbagliare (sembra che i dati siano spariti).

⚠️ **Ma il `.esempio` non va in produzione, e la domanda l'ha fatta il committente.** Rinominando per non
sovrascrivere si era prodotto un file che **non finisce più per `.json`**, quindi la regola che nega
`appsettings*.json` — quella che fa rispondere 403 a `/appsettings.json` — **non lo copre**. Non contiene
segreti, ma descrive nome del database, nome dell'utente e percorso del key-ring. Spostato in `deploy/`
insieme all'unit systemd e alla conf nginx, cioè fra le cose che **non si caricano**: sono tutti file di
riferimento, e su Passenger nessuno dei tre serve a qualcosa.

ℹ️ La regola generale che ne resta: **rinominare un file per proteggerlo da una sovrascrittura non lo
protegge da tutto il resto** — cambiando estensione si esce anche dalle deny list scritte su quella. Se un
file non è di runtime, la risposta giusta non è rinominarlo ma **non metterlo lì**.

**Cosa è stato verificato, e come:**
- catena `vipi.db → Vipi.DbSeed → MariaDB 11.4.10 locale → mariadb-dump → .sql`, **38 tabelle su 38
  riconciliate** dal tool, 4151 righe lette;
- il `.sql` **reimportato in un database vuoto** e confrontato **tabella per tabella**: 39/39, 4162 righe,
  **zero differenze**;
- l'host avviato su quel database: `/services/vsop` **200** con LIRR/LIMM/LIBB a schermo e **zero**
  `Applying migration`;
- collation `utf8mb4_uca1400_as_cs` su **168 colonne** (le 2 rimanenti sono `__EFMigrationsHistory`), e la
  prova che conta — `ZZZZ` e `zzzz` convivono nell'indice unico e il `WHERE` li distingue;
- primi quattro byte `2f 2a 4d 21` (`/*M!`): la trappola del BOM di A3 non si è ripetuta.
  ⚠️ **La seconda metà di questa riga diceva anche «e nessun CRLF», ed era falsa.** Rimisurato il 30 agosto
  2026: quel file ha CRLF su **tutte e 5512** le righe, ed è lo stesso che Ivao.It ha importato senza un
  inciampo (A12). Non è un difetto scampato per fortuna: i fine riga li mette `mariadb-dump.exe`, che su
  Windows apre lo stdout in text mode, e MariaDB li ingoia. La lezione non è sul CRLF ma sul metodo —
  **una verifica si scrive dopo averla eseguita**, e questa è stata elencata insieme a quattro misure vere
  senza essere una di loro. Dettagli in `../deploy/mariadb/README.md` §6.

⚠️ **Due trappole ripagate, entrambe già scritte e comunque incontrate.** `Vipi.DbSeed` non è in
`Vipi.slnx`, quindi il suo `packages.lock.json` era rimasto a EF 8.0.29 mentre `Vipi.Infrastructure` è a
8.0.30: `CS1705` **il giorno del travaso**, cioè l'unico giorno in cui il tool si usa. E il `publish` con
RID ha toccato **nove** `packages.lock.json`, che vanno rimessi a posto prima di committare — è la nota di
[[stato-9-agosto-2026]], e vale ancora.

⚠️ **Non verificato, come sempre**: il pacchetto non è mai stato eseguito su Linux (compilazione
incrociata da Windows). Il `.sql` invece sì, contro una MariaDB 11.4.10 vera.

⚠️ **Rifatto la sera del 23** per portare dentro la correzione di **H4** (l'intestazione della tabella ACC
che non si appiccicava). Il committente aveva già finito di caricare la versione precedente: fra i due
pacchetti differiscono **21 file**, ma **19 sono DLL ricompilate** — stessa funzione, identità di build
diversa. Quelli che cambiano davvero sono **due**: `wwwroot/_content/Vipi.Ui/vipi-theme.css` e
`Vipi.Host.staticwebassets.endpoints.json`, che porta impronta e `integrity` dell'asset e **va aggiornato
insieme al CSS** — da solo, il vecchio manifesto continuerebbe a chiedere la versione di prima.
ℹ️ Il confronto si fa a **sha256 file per file** contro una copia del pacchetto già caricato, non a occhio:
è ciò che ha distinto i 2 file veri dai 19 rumorosi.

ℹ️ **Il contenuto consegnato non è quello del 18 agosto**: gli accordi sono 16 come allora, ma le sezioni
sono **34** e le clausole **50** (erano 38 e 60 il giorno della conversione). È lavoro editoriale fatto in
mezzo, non una perdita del travaso — il tool riconcilia riga per riga ed esce in errore se una tabella non
combacia.

### A12 ✅ 23 agosto, sera — la produzione si è aggiornata da sola, e il `.sql` è stato importato
> ⚠️ **Correzione, e vale più della voce.** Questa voce ha sostenuto per un'ora che i coordinamenti di
> produzione fossero **perduti**. **Non lo sono**: il committente ha guardato `/services/vsop/admin/trasferimenti`
> e gli accordi ci sono. Ivao.It ha importato il `.sql`, e la consegna è **completa** — sito e database.
>
> L'errore non è stato nella misura ma nell'**inferenza**: dal fatto che le migrazioni fossero girate ho
> concluso che *nient'altro* fosse successo, e da lì che il `DROP` fosse l'ultima parola sui dati. Era una
> deduzione su una cosa che non potevo vedere — lo stato del loro database — spacciata per constatazione.
> ⚠️ **Il committente aveva l'unica prova che contava** («c'è la quota corretta da 90 a 9000, e nell'ultima
> release del DB non c'era») e io ho continuato a cercarla da fuori, dove non era raggiungibile. Quando
> qualcuno che *guarda il sistema da dentro* riferisce un fatto, quello è un dato, non un'impressione da
> verificare prima di crederci.

<details><summary>Quel che resta vero, e serve al prossimo giro di migrazioni</summary>


Il committente ha caricato i file via FTP e **Passenger ha rigenerato il processo** senza che nessuno
toccasse `tmp/restart.txt`. Misurato dall'esterno, non dedotto: `/services/vsop` risponde **200** (quella
rotta nella build del 15 agosto non esiste), `/vsop` dà **301**, `/vsop/health/ready` dice **Healthy**, e
lo `scope` dentro un `code` OIDC del 23 alle 20:31 UTC è `openid profile email` — quello del 15 agosto era
`openid email tracker`.

Quindi in produzione gira **il codice del 23 sui contenuti del 14**, e le migrazioni sono state applicate
all'avvio.

⚠️⚠️ **E lì c'è la perdita.** L'archivio del 14 agosto è sul modello **legacy**: il suo `.sql` contiene
`TransferFlows` e `TransferPoints` **con dati**. Salendo alla testa, `20260817090137_DropLegacyTransferTables`
li **droppa** (`Up` = due `DropTable`; il `Down` li ricrea **vuoti**, e lo dice il commento della migrazione
stessa), mentre `AddCoordinationAgreements` non travasa niente — **zero** `InsertData`, **zero**
`migrationBuilder.Sql`. La conversione flussi→accordi non è mai stata una migrazione: è stata un passo a
parte, eseguito **solo in sviluppo**.

**Quindi un archivio ancora sul modello legacy che sale alla testa perde i coordinamenti in silenzio**, e le
tabelle nuove restano vuote: nessun errore, nessun avviso. In produzione **non è successo** — l'import del
`.sql` ha rimesso tutto — ma la proprietà della catena di migrazioni è questa, ed è verificata nel codice.

⚠️ **La protezione su cui contavamo non copriva questo caso, e va detto per esteso.** `AgreementSectionsFinalize`
fallisce rumorosamente su un archivio che **ha già gli accordi** in forma vecchia — quello era il caso previsto.
Un archivio ancora sul modello **legacy** non lo incontra mai: passa dal `DROP`, e arriva in fondo pulito e
vuoto. Avevamo scritto «un deploy fatto adesso non parte»; il vero rischio era «parte, e non dice niente».

**Cosa ne consegue:** la sostituzione del database (**A11**) è stata **fatta**, e ha portato in produzione sia
i contenuti aggiornati sia gli accordi. Se non fosse stata fatta, quella catena avrebbe lasciato un sito senza
coordinamenti — motivo per cui la regola qui sotto resta.

</details>

ℹ️ Da qui una regola per il prossimo set di migrazioni: quando una migrazione **droppa** una tabella che
altrove è stata *convertita* da un passo esterno, la migrazione deve o portarsi dentro la conversione, o
**rifiutarsi di girare** se trova righe. Un `DROP` silenzioso su dati veri non è reversibile e non si accorge
di nulla.

### A13 ✅ CHIUSO DALL'ESTERNO il 25 agosto 2026 (sera) — resta solo la rotazione

> **Aggiornamento 25 agosto 2026 (sera).** Il committente ha riferito che **l'hosting ha cambiato le
> impostazioni di accesso**. Rimisurato dal vivo con `curl` (solo status code, non i corpi dei file
> segreti):
>
> | URL | esito ORA |
> |---|---|
> | `/appsettings.Production.json`, `/appsettings.json`, `/appsettings.Development.json` | **404** |
> | `/Vipi.Host.dll`, `/Vipi.Host.pdb` | **404** |
> | `/diagnostica/avvio-diagnostica.txt`, `/diagnostica/errori-richieste.txt` | **404** |
> | 7 varianti di aggiramento (`.JSON`, `//`, `/./`, `?x=1`, `%61`, maiuscole, `…/.`) | **404 tutte** |
> | `/services/vsop` (GET reale) | **200** `text/html` |
> | `/_content/Vipi.Ui/vipi-theme.css` | **200** — gli asset dell'app si servono ancora |
>
> **Il 404 nasce dall'ORIGINE, non dal CDN** (`cf-cache-status: DYNAMIC`, nessuna block-page Cloudflare):
> è l'applicazione a rispondere «non esiste». Firma del fix: `/_content/…` dà 200 mentre i file alla radice
> danno 404 ⇒ **ora tutte le richieste passano all'applicazione** invece di essere servite dal filesystem —
> è la strada giusta («document root ≠ cartella app»), non il cerotto per oscurità. Novità: davanti al sito
> ora c'è **Cloudflare** (`Server: cloudflare`), prima assente. (Questo chiude anche la vecchia **A13-bis**:
> il criterio «devono diventare 403/404 …» è soddisfatto.)
>
> ⚠️ **RESTA APERTO — rotazione dei segreti, IN CORSO.** Chiudere l'accesso oggi non annulla l'esposizione
> 24→25 agosto: password DB e `ClientId`/`ClientSecret` IVAO sono stati pubblicamente scaricabili (e il repo
> GitHub è pubblico). Il committente **ha chiesto le credenziali nuove il 25 agosto**; vanno considerati
> compromessi finché non arrivano e non sono in opera.
>
> ⚠️ **Igiene con la nuova architettura**: ora che c'è un CDN davanti, restringere l'ORIGINE ad accettare
> solo il traffico Cloudflare (Authenticated Origin Pulls o whitelist IP CF), o chi conosce l'IP origine
> aggira il WAF andando diretto. Oggi non sfruttabile (il 404 nasce dall'app), ma è la mossa corretta.

**Storia — trovato il 24 agosto 2026** mentre si indagava il 500 di E8, con `curl -I` sulla produzione. Il
front server serviva i file **direttamente dalla cartella dell'applicazione**: `public_atc` non era solo la
radice dell'app, era anche il **document root** del sito. *(Non è più così dal 25 agosto — vedi sopra.)*

| URL | esito misurato |
|---|---|
| `/appsettings.Production.json` | **200**, `application/json` — dentro ci sono **password del database** e **ClientSecret IVAO** |
| `/appsettings.json`, `/appsettings.Development.json` | 200 |
| `/diagnostica/avvio-diagnostica.txt` | 200 — configurazione vista all'avvio, percorsi, quali segreti sono valorizzati |
| `/Vipi.Host`, `/Vipi.Host.dll`, `/Vipi.Host.pdb`, `/Vipi.Infrastructure.dll` | 200 — l'applicazione intera, coi simboli di debug |
| `/web.config`, `/vipi.db.bak` | 403 — Plesk nega **alcuni nomi**, non la cartella |
| `/vipi-keys/`, `/diagnostica/`, `/deploy/` | 404 — **niente elenco cartelle**: i file si prendono solo per nome esatto |

⚠️ **Il commento dentro `deploy/atc-ivao/appsettings.Production.json` dice «Che non sia scaricabile via HTTP
è stato verificato: /appsettings.json risponde 403». Oggi non è più vero.** Quella misura è del 16 agosto,
prima del passaggio a Plesk+Passenger, ed è invecchiata **in silenzio**: è il caso da tenere a mente ogni
volta che si scrive «verificato» accanto a un fatto che dipende dall'hosting.

⚠️ **`deploy/atc-ivao/nginx-vipi.conf` nega `^/diagnostica/`, ma su quel server non è la nostra conf a
girare**: è un file di riferimento per un deploy systemd+nginx che lì non esiste. Una regola scritta in un
file che nessuno carica non protegge niente.

**Il key-ring si salva per il rotto della cuffia.** `public_atc/vipi-keys/key-<guid>.xml` non è elencabile e
il nome è un GUID; ma è sicurezza per oscurità, e chi lo indovina **fabbrica un cookie di autenticazione
valido per qualunque VID, admin compresi** — è scritto nel commento `DataProtection` di appsettings.

**Le due strade giuste erano chiuse** al 24 agosto — **ENTRAMBE si sono riaperte** con la segnalazione a chi
supervisiona it.ivao.aero (25 agosto):
1. ~~Ruotare i segreti~~ → **IN CORSO**: il committente ha chiesto le credenziali nuove il 25 agosto (prima
   «non si può fare», perché la password DB e l'app IVAO non erano nostre da cambiare — ora c'è un
   interlocutore).
2. ~~Chiedere a chi ha il pannello~~ → **FATTO**: chi ha segnalato il problema supervisiona il dominio e ha
   cambiato le impostazioni di accesso (i file non si scaricano più — vedi l'aggiornamento in testa).

**Il rimedio che resta, ed è quello messo in opera (pacchetto «f»).** Se il file non si può nascondere, si
svuota: `SegretiFuoriDalWeb` unisce alla configurazione ogni `*.json` dentro la cartella `segreti/` accanto
all'eseguibile, **dopo** tutto il resto, quindi quei valori vincono su `appsettings.Production.json`. Il
**nome del file lo sceglie chi installa** e non è scritto da nessuna parte: il server non elenca le
cartelle, quindi un file si prende solo indovinandone il nome esatto. Istruzioni in
[`../deploy/atc-ivao/LEGGIMI-SEGRETI.md`](../deploy/atc-ivao/LEGGIMI-SEGRETI.md).

⚠️ **È sicurezza per oscurità, ed è giusto chiamarla col suo nome.** Non chiude il buco: sposta i segreti da
«scaricabili con un indirizzo scritto nel nostro repository» a «scaricabili da chi indovina un nome che
nessuno conosce». È esattamente la protezione che regge oggi il key-ring, e che il progetto ha già
accettato per quello. La riparazione vera resta la 2, quando ci sarà un canale per chiederla.

⚠️ **Il passo che chiude davvero è il quarto del foglio: togliere i valori da `appsettings.Production.json`.**
Finché la password sta anche là, spostarla non è servito a niente. Per questo l'avvio **si ferma** se la
connection string è vuota o porta ancora il segnaposto: senza quella guardia, la configurazione a metà
ripiegherebbe su uno SQLite vuoto e il sito tornerebbe su con l'aria di aver perso tutti i dati.

⚠️ **Il nome dei file di `segreti/` non entra in `avvio-diagnostica.txt`** — quel riepilogo è a sua volta
scaricabile, e scriverci il nome vanificherebbe l'unica protezione che c'è. Si riporta quanti, mai quali.

**Quel che resta esposto, e non si può chiudere da qui:** `*.dll`, `*.pdb`, `appsettings.json`, i file di
`diagnostica/` (che da E8 contengono stack trace e VID). Nessuna credenziale, ma una mappa del server.
E i segreti già scaricati nelle settimane scorse restano scaricati: **questo rimedio ferma l'emorragia, non
la ripara**.

### A14 ✅ Consegna del 30 agosto 2026 — l'ultimo database prima del 16 settembre

Chi amministra il database di Ivao.It è via fino al **16 settembre**: questo `.sql` è l'ultimo travaso
possibile per diciassette giorni.

⚠️ **Decisione del committente, la sera del 30**: in produzione si riparte **da zero sul contenuto**. Il
`.sql` consegnato non è il travaso del `vipi.db` di sviluppo — è un archivio nuovo con dentro **le
anagrafiche, la memoria di traduzione e il glossario, e nient'altro**. Niente vIPI, niente accordi, niente
release, niente spazi aerei, niente statistiche: si riscrive dal sito nei giorni successivi.

| | Cosa | Dove | sha256 |
|---|---|---|---|
| **Database** | `vipi-atc-it-ivao-aero-2026-08-30.sql` — **985 KB** (1 008 594 byte), 3546 righe | `_mariadb/dump/` (**fuori dal repo**) | `5C4BC0BC…F1E5C8AB` |
| **Lo stesso, compresso** | `…​.sql.gz` — **186 KB** | idem | `2F3AA500…9474E36C` |
| **Foglio + i due script di rete** | [`../artifacts/`](../artifacts/) — `CONSEGNA-DB-20260830.md`, `-copia-di-sicurezza.sql`, `-ripristino.sql` | fuori da git | — |
| **Sito** | `vipi-linux-x64-mariadb-20260830.zip` — 50,4 MB, 474 file, self-contained net8, pacchetto **`j`** | `artifacts/publish/` | `08335127…55FAB922` |

⚠️ **Il timbro del pacchetto è `j · 2e96bbc`, e quel commit sta solo in locale**: è la testa del ramo
`consegna-db-20260830`. La barra lo mostra all'admin, e finché il ramo non è spinto quel numero non si può
risalire. Il codice di runtime è identico a `main` (`30363753`) — oggi sono stati toccati solo test e
documentazione — quindi non è un problema di *cosa* gira, ma di *rintracciabilità*: si chiude spingendo il
ramo.

ℹ️ Le lettere dei pacchetti erano arrivate a **`i`**, non a `g`: `g` era solo l'ultima *consegnata*, e le
correzioni del 24 agosto hanno consumato h e i. Guardare `artifacts/publish/`, non la memoria.

ℹ️ Il timore sul limite di caricamento di phpMyAdmin **è caduto da sé**: 186 KB compressi contro i 3,1 MB
di agosto, che erano già passati.

⚠️ **La trappola della policy, e come si è disinnescata.** Le statistiche ATC si sono tenute fuori
spegnendo `ImportCategory.AtcSessions` — un flag solo che governa poller, storico, traffico aeroporti e
riassunto mensile. Ma **la policy viaggia nel dump**: consegnata spenta, in produzione l'archivio non
sarebbe mai partito. Riaccesa come ultimo gesto prima di fermare l'host, e verificata **sul database** e non
a schermo.
ℹ️ E spegnerla dall'interfaccia a giro avviato **non basta**: il recupero dei 365 giorni legge la policy una
volta sola, all'inizio, e in quel primo tentativo sono entrate **21 109 sessioni**. Il database di consegna
è stato rifatto seminando la riga di policy **prima** dell'avvio, dove nessuna corsa è possibile.
ℹ️ `AtcHistory` resta senza un giro riuscito in `ImportStates`, quindi in produzione al primo avvio parte il
**recupero completo dei 365 giorni**: l'archivio si ricostruisce di là invece di viaggiare nel file.

**Che cosa si congela davvero, e che cosa no** — la domanda che ha guidato tutto il resto. Lo **schema
non** si congela: il provider di produzione è MySQL, `MigrateVipiDatabase` chiama `Database.Migrate()`
all'avvio, e il pacchetto lo carica il committente via FTP (A12). Si congelano **i dati** — questo è
l'ultimo carico — e soprattutto **la rete**: dal 31 agosto una migrazione gira da sola, su DDL non
transazionale, senza nessuno che possa ripristinare. Da qui il presidio, non da una preferenza di stile.

**Il presidio:** `tests/Vipi.Infrastructure.Tests/MigrazioniDellaFinestraCiecaTests.cs`. Legge le
migrazioni **MySQL** datate fra `20260831` e `20260917` e ne guarda le `UpOperations` **strutturate** — non
il testo del `.cs`, che conterebbe anche i `Down`, dove una `DropTable` è l'inverso innocuo di una
`CreateTable`. Vieta `DropTable`/`DropColumn`/`RenameTable`/`RenameColumn`/`AlterColumn`/`Sql`; pretende un
valore di riposo vero su ogni colonna stringa NOT NULL (la trappola dell'enum che `FormaCheHaContato` ha
schivato a mano); rifiuta un indice unico nuovo su una tabella già popolata. Uscita esplicita:
`RevisionateAMano`, dove l'id si scrive **con la ragione**.
⚠️ **Va cancellato quando la finestra si chiude**, non aggiornato spostando le date: sarebbe una regola
permanente travestita da eccezione.
ℹ️ Provato che morde allargando la finestra all'indietro: rosso su `ConcessioniPerAccRimosse` (DropTable),
`RadioassistenzeFamigliaETipo` (due `Sql` + RenameColumn), `Navaids.NaturalKey` (default vuoto) e due
indici unici. Un presidio che non si è mai visto fallire non è un presidio.

**Preparazione della sorgente, prima del travaso:**
- il `vipi.db` era **indietro di tre migrazioni** (108 su 111): `SectorShapeParts` e `TranslationSpends`
  non esistevano, e `Vipi.DbSeed` legge dal modello — la catena sarebbe morta in lettura. Backup in
  `vipi.db.bak-pre-consegna-20260830`, poi allineato;
- **sette documenti ripubblicati** dei nove in deriva (`/services/vsop/admin/pending`), guidando l'app:
  `LIBD #4`, `LIBR #4`, `LIPA #2`, `LIRN #6`, `LIBA_APP #2`, vLOA `LGGG #2`, vLOA `LYBA #2`.
  ℹ️ Quel lavoro vive nel `vipi.db` di **sviluppo** e **non è entrato in questa consegna**, che riparte da
  zero sul contenuto. Non è sprecato: è lo stato da cui si riscriverà.
  ⚠️ **Due tenuti fermi apposta**: `vIPI Brindisi` e `Pescara Approach` portano tre sezioni intitolate
  «Nuova sezione» (due vuote, una con dentro solo un'immagine). Ripubblicarli le avrebbe messe **in
  pubblico per diciassette giorni**. Si sistemano dall'editor, poi si ripubblicano.

**Che cosa è stato verificato, e come:**
- le **45 migrazioni MySQL** applicate a un MariaDB 11.4.10 vero, da database vuoto: sono le prime 36 a
  vederne uno davvero (finora erano solo state generate);
- `Vipi.DbSeed`: **56 tabelle su 56 riconciliate**, 3546 righe, 48 contatori `AUTO_INCREMENT` riportati
  oltre il massimo; e il controllo del 7 agosto che era sfuggito — `SpecialAreaCenters` (247) **>**
  `SpecialAreas` (230);
- il `.sql` **reimportato in un database vuoto** e confrontato tabella per tabella: **57/57**, 3546 righe,
  `__EFMigrationsHistory` a 45;
- **l'host avviato sul database reimportato**: `Now listening`, **zero `Applying migration`**, cinque
  pagine 200 e nessun errore — è la prova che il 23 agosto si faceva e che al primo giro avevo saltato;
- collation `utf8mb4_uca1400_as_cs` su **266 colonne**; le due fuori regola sono `MigrationId` e
  `ProductVersion`, che la tabella la crea EF;
- `.gz` riaperto e ricontato: 14 091 458 byte, gli stessi;
- primi quattro byte `2f 2a 4d 21`. **Sul CRLF vedi la correzione in A11**: non è mai stato un problema, e
  la riga che diceva di averlo escluso non aveva misurato niente.
- **quindici assiemi su quindici verdi**, 8796 test, E2E compresi (255) — e misurati con l'host **spento**,
  che è la condizione senza la quale `Vipi.E2E.Tests` sparisce dal riepilogo in silenzio.
  ⚠️ Il primo giro sembrava dire che cinque progetti non erano nemmeno partiti: era la misura, non il
  software — il comando finiva in `| tail -30` e il file conservava solo la coda. **Un riepilogo di test si
  legge intero o non si legge**, e vale la pena dirlo perché la conclusione sbagliata era già scritta.

**La rete non gliela chiediamo più: gliela diamo.** Per tre consegne il passo 1 è stato «fate un
`mysqldump`», e per tre consegne nessuno ha confermato di saperlo fare — una dipendenza da una capacità mai
verificata, sotto un `DROP DATABASE`. Dal 30 agosto la procedura **non droppa più il database**: copia ogni
tabella in una gemella `bak30_…` **dentro `itivao_atc`**, toglie le originali, importa. Servono solo i
permessi con cui lavorano già; niente `DROP`/`CREATE DATABASE`, niente shell, niente pannello, e la scheda
SQL di phpMyAdmin basta. Costa lo spazio dei dati attuali (pochi MB), e l'app quelle tabelle non le vede —
su MySQL **nessuno enumera le tabelle**: `ISchemaDriftProbe` si spegne fuori da Npgsql, EF tocca solo il suo
modello.

Due script, generati dai dump e **provati end-to-end** su una copia del loro database:
`artifacts/CONSEGNA-DB-20260830-copia-di-sicurezza.sql` e `…-ripristino.sql`. La prova:
39 tabelle/4162 righe → copia → import (57/40 078) → ripristino → **di nuovo 39 e 4162, esatte, zero
residui**. Il ripristino sono `RENAME`, cioè metadati: dura secondi.

⚠️ **La prova ha trovato quattro difetti che leggere non avrebbe trovato**, e i due peggiori li ha trovati
solo la seconda prova, quella sulla **struttura**:

1. la lista da *salvare* e quella da *togliere* non sono la stessa: loro hanno lo schema del 23 agosto (39
   tabelle), il nuovo ne crea 57, e generarle entrambe dal dump nuovo faceva fallire il passo 1 su
   diciannove tabelle inesistenti;
2. da togliere serve l'**unione**: `EditGrants` (caduta con `ConcessioniPerAccRimosse`) sopravviveva
   all'import e occupava il nome, e il `RENAME` del ripristino sarebbe morto con «table already exists» —
   la rete si sarebbe strappata proprio nel momento in cui serviva;
3. ⚠️ **`CREATE TABLE … LIKE` non copia le foreign key.** Il ripristino restituiva righe e indici esatti e
   **zero vincoli su trentotto**. Il confronto dei conteggi diceva verde: contava le righe, e le righe
   c'erano tutte. Ora il ripristino le riemette una per una, testuali, prese dal dump del 23;
4. ⚠️ **e la cura ovvia era peggiore del male.** Sostituire la copia con un `RENAME TABLE` conserverebbe
   tutto — è la stessa tabella — ma **si porta dietro i nomi dei vincoli**, che in InnoDB sono globali per
   schema: l'import che ricrea `FK_AccSectors_Accs_CenterId` trova il nome occupato dalla tabella spostata e
   muore con **errno 121**. Scoperto eseguendolo, un minuto dopo averlo scritto.

ℹ️ Ne resta una regola: **un ripristino si verifica su due assi, i dati e la forma.** Un solo asse dà un
verde che non significa niente, ed è il verde che si guarda proprio nel momento peggiore.

ℹ️ Sul banco di prova alcune copie tornano **minuscole**: è `lower-case-table-names=2` di Windows
(§1 di `../deploy/mariadb/README.md`). Sul loro Linux vale `0` e i nomi si conservano esatti — sorgente e
destinazione del `RENAME` vengono dallo stesso script con la stessa grafia, quindi lì la cosa non si pone.

ℹ️ Una delle tre domande **si ritira**: `max_allowed_packet` non serve chiederlo, lo legge
`MySqlServerSettingsProbe` all'avvio e finisce nella diagnostica insieme a `sql_mode`. Restano il backup
loro (che ora è un di più, non un prerequisito) e il limite di caricamento di phpMyAdmin.

⚠️ **La query sui permessi è stata TOLTA dal foglio, ed è una lezione sul verificare le premesse.** Per
tre consegne il foglio ha chiesto a Ivao.It di mandarci `SELECT * FROM RoleOverrides` prima dell'import,
«per non perdere le promozioni date a mano». Ma quella tabella **in produzione non esiste**: nasce con
`20260828212039_PromozioniAMano`, cinque giorni dopo l'ultima consegna, e nel dump del 23 agosto — che è il
loro database di oggi — non c'è. Il meccanismo precedente, `EditGrants`, c'è ed è **vuoto**. La query
sarebbe morta con «table doesn't exist» in mezzo a una procedura delicata, facendo dubitare di aver rotto
qualcosa, e non avrebbe salvato niente perché non c'era niente da salvare.
ℹ️ L'ordine dei passi era anche sbagliato — la query stava **dopo** lo script che toglie le tabelle. Due
domande di fila del committente, due difetti nello stesso passo: prima l'ordine, poi il fatto che non
servisse affatto. **Una premessa non verificata sopravvive alle riletture**, perché rileggere conferma che
la frase è coerente, non che è vera.

ℹ️ Le nove righe «da rivedere» restano accese nel database consegnato anche per i sette ripubblicati: il
ricalcolo della deriva è un giro gestito con cancello a 24h e il periodo è una **costante nel codice**
(`ImpactDriftHostedService.Periodo`), non una configurazione. Si richiudono da sole al primo giro in
produzione. È cosmetico, ma sapere perché evita di cercare un guasto che non c'è.

## B. Branch non fusi — decisioni, non lavoro

> ✅ **Al 1 settembre 2026 non esiste NESSUN ramo non fuso.** `main` è a `50028edc` e ha assorbito gli
> ultimi due (`avviso-simulazione` §AO, `coerenza-sectorfile` §AP); tutti e tre i rami rimasti sono stati
> cancellati, locale e remoto. Quel che segue è **storia**: serve per le lezioni sulle fusioni, non per
> cercare lavoro da fondere. L'unica voce ancora aperta qui dentro è **B10-bis**.

### B12 ✅ FUSO — i tre rami in fila, fusi il 27 agosto 2026

**Il committente ha deciso: fondere.** I tre rami — `statistiche-atc` → `identita-settori` →
`aeroporto-a-sezioni` — erano costruiti l'uno sopra l'altro, quindi otto rami su nove sono entrati in
**fast-forward, zero conflitti**. Il solo lavoro vero è stato `audit-versioni-release`, nato da `main` in
parallelo.

⚠️ **Due guasti che git non poteva vedere**, e sono la lezione che resta: *il conflitto segnalato non è
quello che rompe*. Git ha marcato **un file solo**; a rompere sono stati (a) un parametro obbligatorio nuovo
sui costruttori `EfReleaseRepository`/`EfDocumentAdminRepository` contro test nati sull'altro ramo — otto
errori di compilazione, **file diversi, nessun conflitto** — e (b) `ReleasePanel`, dove il `title` della riga
diceva «sistema» sul VID 0 mentre la riga a schermo diceva «VID 0», perché i due rami avevano toccato **due
metodi diversi** della stessa idea.

**Dopo una fusione fra rami paralleli, compilare e far girare i test è parte della fusione, non una verifica
successiva.**

Punti di ritorno spinti come tag: `punto-di-ritorno-20260827-{main,aeroporto,audit}`.

<details><summary>Com'era scritta finché la decisione era aperta</summary>



**Una sessantina di commit** oltre `main`, spinti su `origin/statistiche-atc`. ⚠️ **La cifra esatta si conta,
non si legge da qui** — ed è scritta così di proposito: ogni volta che la si fissava a un numero, il commit
che la aggiornava la faceva sbagliare di uno. Qui
c'è stata scritta «24» per due giri di fila mentre il ramo era già a 27, ed è il motivo per cui accanto c'è
il comando — `git rev-list --count main..statistiche-atc`.
**Niente lo blocca sul piano tecnico**: build a **0 avvisi** e suite **tutta verde** su tutti e due i TFM —
**2368 su net8, 2130 su net10**, rimisurati il 25 agosto a tarda sera dopo le otto richieste e la
correzione delle chip (§16 della carta). Fondere è una decisione, non un passo rimasto indietro.
⚠️ Prima di credere a un conteggio: `grep "error MSB"`. Con `Vipi.Host` acceso (la verifica live) i suoi DLL
sono bloccati, mezzo albero non compila e il totale cala di centinaia senza che il comando diventi rosso in
modo visibile.

ℹ️ Per qualche ora del 25 sera su net10 c'era **un rosso**, ed era del ramo: due difetti nelle proprietà
CsCheck dell'AoR, chiusi in giornata. La storia sta in **§H2**, e vale la pena leggerla prima di rilanciare
una proprietà che cade.

ℹ️ **L'ultimo commit non è delle statistiche.** È
[il VID che diventa un link al profilo IVAO](feature/2026-08-25-vid-porta-sul-profilo-ivao.md) (`03463bf`),
chiesto dal committente il 25 sera: è finito qui perché qui si stava lavorando, e due dei suoi quindici
punti (`StatsHome`, `StatsDivisionPage`) sono file che **esistono solo su questo ramo**. Non aggiunge
migrazioni e non tocca il modello. Cosa gli resta: **§H5**.

Carta con tutto: [`feature/2026-08-24-servizio-statistiche-atc.md`](feature/2026-08-24-servizio-statistiche-atc.md)
— **§12** è l'elenco vivo di cosa resta, **§13** la veste del 25 agosto, **§14** le statistiche di un altro,
**§15** i due modi di leggere gli aeroporti.

**Le otto cose chieste dal committente a tarda sera del 25** stanno in **§16** della carta, e sono tutte
dentro. La sola davvero nuova: **Aeroporti: traffico e copertura** — quanto traffico c'è stato su ogni campo
italiano e quanto ha trovato un controllore acceso — dentro `/services/stats/division`, **solo staff**,
raggruppabile per ACC (`?g=LIRR`).

⚠️ **La carta diceva una cosa falsa**, e chi legge §3 la deve leggere corretta:
`/v2/airports/{icao}/stats` **non** dà conteggi giornalieri di movimenti — è una fotografia al minuto dello
stato corrente, con `limit` sotto 100. Quel che serve lo dà `/traffics`, che **regge trenta giorni in una
chiamata** (LIRF: 981 KB, 1,3 s) e porta gli **istanti** che il nostro client buttava. Zero endpoint nuovi.

ℹ️ Provato con **dati veri**: durante la verifica live il consolidamento ha girato contro IVAO e ha misurato
**3 525 giorni-aeroporto** su 75 campi. Il totale di quella finestra: **16 374 movimenti, 3 307 con ATC — il
20%**. Estremi misurati: LIEO 52%, LIRP 0%.

**Le quattro cose chieste dal committente la prima parte della sera del 25**, tutte già dentro:

1. **Il numero nel buco della ciambella non ci stava** (§13.8). Il buco è largo 69 unità del viewBox e il
   corpo era fisso a 19: cinque cifre ci stanno, sei no. Si vedeva **solo su `/division`** perché il
   componente era stato provato con le ore di **una persona**, mai con quelle di una divisione.
2. **Lo staff può aprire le statistiche di un altro** (§14): `/services/stats/user/{vid}`, tutto lo staff
   `IT-`. ⚠️ La guardia sta **prima di ogni query**, e un test lo verifica con un `IAtcStatsQueries` che
   esplode a ogni metodo. L'accesso lascia **una riga di audit** (`AuditAction.View`, valore nuovo e
   additivo: gli enum sono stringhe, nessuna migrazione), accorpata a mezz'ora perché i chip di periodo
   ricaricano la pagina. **Chi viene guardato non viene avvisato** — deciso, non rinviato.
3. **Aeroporti gestiti accanto ad aeroporti visti** (§15). Sono due domande opposte: i campi che coprivi,
   e i capi del piano di volo che ti passano davanti. ⚠️ **Un sorvolo vettorato non è traffico «di» un
   aeroporto** ma resta nei totali — quindi la somma della colonna dei gestiti **non** è il totale dei voli.
   Per i settori d'area il campo lo dice la **geometria** (`PolygonGeometry.Contains` sul poligono del
   settore), non l'albero: `Airport.ParentCallsign` è compilato a mano e ce l'hanno **31 aeroporti su 93**,
   con **12 CTR su 140** che abbiano qualcosa sotto. ⚠️ Il poligono è quello di **oggi**: una
   risettorizzazione cambia i numeri dei turni **passati**, ed è stato accettato sapendolo.
4. **Il salvataggio finale del poller non usava il gettone giusto.** Allo spegnimento il log diceva
   «salvataggio finale del traffico fallito» con una `TaskCanceledException` e **sembrava un guasto del
   database**: non lo era, `StopAsync` passava alla scrittura il proprio gettone di arresto. E `FlushAsync`
   chiama `TakeAll`, che **svuota** il registro prima di salvare — quei minuti non erano più né su disco né
   in RAM. Ora la scrittura ha un gettone suo con cinque secondi di tetto.

**Cosa porta.** Il **terzo servizio** dell'hub, `/services/stats`: ore, turni, traffico gestito, copertura
della divisione. ⚠️ IVAO dà le **connessioni**, non il traffico: chi hai gestito lo costruiamo campionando
l'AoR a ogni giro del poller che esisteva già — **stessa cadenza, stesso numero di chiamate**, in più i
piloti. Dal 25 agosto ogni volo porta le sue **targhette** (in partenza / in arrivo / sorvolo · decollato ·
**atterrato** · al parcheggio · consegnato a X · uscito in volo · solo rullaggio · fermo), la sessione ha una
**striscia del turno**, e c'è la **costanza** in settimane di fila.

⚠️ **La regola che governa le targhette, e va tenuta se qualcuno ci lavora sopra: si dice quel che si è
VISTO.** Un volo diretto al tuo campo che esce dall'area ancora in volo **non è «atterrato»**. La regola sta
in `TrafficStory` (puro, con test) e la usa **anche il filtro** della pagina: una seconda copia nel markup si
scollerebbe dalla prima al primo cambiamento.

**Le tre cose da fare quando si decide di fonderlo** (dettaglio in §12 della carta):

| | cosa | quando |
|---|---|---|
| ✅ | ~~**La Guida**~~ — **fatta** il 25 sera: capitolo `statistiche` in `GuidaPage.razor`, IT ed EN. ⚠️ La diagnosi qui scritta era **sbagliata a metà**: la voce in `GuideSearchCatalog` c'**era già**, e puntava a un'ancora che nella Guida non esisteva — chi cercava «statistiche» trovava un risultato, lo apriva e finiva su una pagina senza quel capitolo. Un collegamento morto è peggio di nessun collegamento, perché nessuno lo denuncia. Ora c'è `GuidaAncoreTests`, che verifica che **ogni** voce del catalogo abbia il suo capitolo. | fatto |
| 🔴 | **La `UPDATE` dei tetti TWR** (§4.5-bis) è stata eseguita **solo sul `vipi.db` di sviluppo**. Senza, in produzione le torri rivendicano fino a FL195 e il traffico in crociera finisce a loro. Stessa guardia: `Position='TWR' AND LimitsFromSource=0 AND UpperLimit=19500`. | al primo deploy |
| ✅ | ~~**La potatura del dettaglio traffico**~~ — **scritta** il 25 sera: `TrafficRetentionUseCase` + `TrafficRetentionHostedService`, a scaglioni e con tetto per giro. ⚠️ Tocca **solo** `AtcSessionTraffic`: le sessioni e i loro contatori denormalizzati restano, ed è precisamente il motivo per cui quei contatori esistono (c'è un test che lo verifica). ⚠️ `RemoveRange`, **non** `ExecuteDelete`. | fatto |

⚠️ **Due cose non ancora viste dal vivo**, e sono l'una il seguito dell'altra:

- **la sequenza delle piste in uso**: coperta da test contro un database vero, ma in tutt'e due i momenti in
  cui si poteva provare non c'era **nessun** ATC italiano collegato (0 su 444 piloti, poi 0 su 422);
- **le targhette di fase e le consegne**: le otto colonne nascono con la migrazione `FasiQuoteConsegne` e si
  riempiono **dal primo turno campionato dal vivo**. Sulle righe già in archivio restano vuote — e in quel
  caso la pagina **non scrive** targhette di fase, che è la stessa regola delle righe ricostruite.

Verifica per tutt'e due: aprire `/services/stats/session/{id}` di una sessione registrata **dopo** il deploy.

⚠️ **Sette migrazioni** del servizio statistiche (più le due del giro aeroporti = **nove** in tutto sul ramo), tutte a doppia emissione: `StatisticheAtc`, `PolicyStatisticheAtc`,
`TrafficoRiempitoAPosteriori`, `ImpostazioniStatistiche`, `PisteInUso`, `FasiQuoteConsegne` e
`TrafficoAeroportoGiornaliero` (25 sera, §16.3). Il ramo
**allunga la coda del cutover MariaDB** — a differenza di B10, che non aveva migrazioni.

</details>

### B11 ✅ FUSO — `login-utente-nuovo`, fuso in `main` il 24 agosto 2026

Undici commit più il merge `1d43767`. Nessun conflitto: il ramo era nato da `main` a `1883446` e nessuno
l'ha toccata nel frattempo. Dopo il merge: **Release verde su entrambi i TFM (0 avvisi)**, **1952 test
verdi** su net8.

⚠️ **Il merge è arrivato DOPO la verifica sul campo, non prima**: il codice era già in produzione dalle
16:19 UTC (pacchetto «g», commit `e8fc4a2`) e la cartella `segreti/` in opera dalle 16:43. Fondere prima
avrebbe voluto dire scrivere in `main` una cosa che nessuno aveva ancora visto funzionare sul server vero.

Contenuto: **E8** (la barra che non affonda la pagina, la pagina d'errore e il registro degli errori, la
versione in barra) e **A13** (i segreti fuori dal file che si scarica).

### B10 ✅ FUSO — `coordinamenti-lato-ricevente`, fuso in `main` il 24 agosto 2026

Sei commit più il merge `84f741b`, ramo cancellato (locale e origin). **Nessun conflitto**: il ramo era nato
da `main` a `f03cd57` e nessun altro ha toccato quei file dopo. Dopo il merge: build Release verde su
entrambi i TFM (0 avvisi), **1925 test verdi** su net8.

Carta con tutto: [`feature/2026-08-24-coordinamenti-lato-ricevente.md`](feature/2026-08-24-coordinamenti-lato-ricevente.md).

| commit | cosa |
|---|---|
| `2ed4a52` | la frase dal lato di chi riceve (`AppCoordRow.IsIncoming` + 3 template nuovi) + fixture |
| `54b4cc9` | `CoordTable`: il corpo diventa una sezione — **meccanico**, nessun cambio di reso |
| `8c7b49b` | due tabelle quando il nodo porta i due versi; via `LastColHeader` |
| `6ad66df` | carta con l'esito e la verifica live |
| `265b882` | anche la frase **uscente** con faccetta cambia forma (secondo giro, chiesto dal committente) |
| `f0a0088` | il ramo entra nei lavori aperti e nell'indice |

**In breve.** Un accordo si scrive una volta sola, dal lato di chi cede, e il documento di chi **riceve**
mostrava quelle stesse parole — «Zagreb Radar trasferisce a Brindisi Radar CS0…» dentro la vIPI di Brindisi —
con la colonna della controparte intestata «Prossimo» mentre porta chi **consegna**. Ora la riga porta il
verso, la frase si gira («X **riceve da** Y…») e un nodo che contiene i due versi si **spezza in due tabelle**
(«Arrivi · che cediamo» / «Arrivi · che riceviamo»).

⚠️ **Il fatto che decide qualunque lavoro futuro lì dentro: le tabelle dei coordinamenti sono MISTE.**
`BuildAccTree` raggruppa per `settore → ACC della controparte → aeroporto/tipo` e **la direzione non è una
chiave di raggruppamento**: il nodo `ES › Zagreb-LDZO › Sorvoli` porta **8 righe entranti e 6 uscenti**.
Qualunque disegno che metta la direzione sulla tabella — o la deduca dal tipo di flusso — è sbagliato e sembra
giusto.

⚠️ **Il secondo giro tocca anche le righe USCENTI.** `TemplateCleared` girava il verbo principale («{owner}
autorizza … e lo trasferisce a {target}») mentre la forma breve dice «{owner} trasferisce a {target} …»:
nella stessa tabella due righe dello stesso accordo si aprivano in due modi diversi. Ora le quattro forme
(× direzione, × faccetta) hanno la stessa testa e la stessa coda.

⚠️ **Nessuna entità nuova e nessuna migrazione**: non allunga la coda del cutover MariaDB. `IsIncoming` è un
campo **additivo** sul DTO serializzato dentro le release congelate, che lo deserializzano `false`.

### 🔴 B10-bis — resta da fare: ripubblicare **un** documento

`Sentence` e `LeadSentence` sono **stringhe già scritte** dentro la release: i documenti pubblicati
continueranno a dire «Zagreb Radar trasferisce a…» finché non esce una release nuova. Misurato fianco a
fianco sulla stessa vIPI ACC di Brindisi: **viewer pubblico 33 tabelle, tutte «PROSSIMO», zero «riceve da»**;
**editor (derivato live) 39 tabelle, 13 «DA», 4 nodi tagliati**. La differenza è tutta la ripubblicazione.

**Quanti documenti sono davvero, misurato in produzione la sera del 24 agosto 2026: uno.** Sono stati
interrogati **tutti** i documenti pubblici del sito, contando le occorrenze delle due frasi:

| Documento | tabelle | «riceve» | «trasferisce» |
|---|---|---|---|
| `/services/vsop/libb/vipi` | 39 | **0** | **55** |
| `limm/vipi` | 4 | 0 | 0 |
| 5 aeroporti (LIBC, LIBD, LIBR, LIBA, LIRN) | 4 ciascuno | 0 | 0 |
| 2 APP non remotizzati (LIBP, LICC) | 1 ciascuno | 0 | 0 |
| 3 vLoA di LIBB (LYBA, LDZO, LGGG) | 8÷9 | 0 | 0 |

Solo la **vIPI ACC di Brindisi** porta la prosa dei coordinamenti; gli altri documenti pubblicati non ne
hanno affatto, quindi per loro la ripubblicazione non cambierebbe una parola. ⚠️ La voce diceva
«ripubblicare i documenti», al plurale e senza numero, e per questo sembrava un lavoro: **è un documento,
e sono due clic**. Il modo di saperlo era interrogare il sito, non ricordare.

⚠️ **Non è un lavoro che si può fare da qui**: pubblicare significa scrivere nel database di produzione, e
si fa dall'editor con un'identità admin. Chi lo esegue: il committente. La verifica dopo, invece, si fa da
fuori in dieci secondi — la pagina pubblica deve smettere di dire zero «riceve».


### B9 ✅ FUSO — `sorgenti-giro-ta-piste`, fuso in `main` il 22 agosto 2026

Cinque commit più il merge `9be2200`, ramo cancellato. Nessun conflitto: il ramo era nato dopo l'ultimo merge.

Contenuto e decisioni: [`feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md`](feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md).
In breve: **tutti** gli import di `/services/vsop/admin/sources` girano ogni 24 ore. **Transition Altitude** e
**Piste** avevano solo i bottoni (`AirportDataImportUseCase`, chiave `AirportData`); l'**anagrafica aeroporti**
non compariva affatto nell'elenco e ora è un giro (`AirportDirectoryImportHostedService`, chiave
`AirportDirectory`). Da qui **nessuna riga resta «su richiesta»**, e un test lo pretende.

⚠️ **Due cose da tenere a mente per la produzione.**

1. L'anagrafica aeroporti è l'**unico giro che crea entità** — era stata lasciata a mano proprio per questo, ed
   è stata automatizzata su decisione del committente. È **additiva**: uno scalo tolto dalla sorgente resta in
   archivio e si toglie a mano. Al primo giro dopo il deploy comparirà **LIDS (Parco Livenza)**, che IVAO ha
   aggiunto e che il `vipi.db` non aveva.
2. I 21 aeroporti senza `TransitionAltitudeFt` si popoleranno da soli, e `RecomputeDefaultBandLevels`
   ricalcolerà i TL delle fasce **default**. Prima del deploy conviene guardare la policy vera in
   `/services/vsop/admin/sources`: in sviluppo la tabella `ImportPolicies` è **vuota**, quindi i valori a video
   vengono dai default delle colonne e non da una decisione.

Nessuna entità nuova e nessuna migrazione: non allunga la coda del cutover MariaDB.

⚠️ **Trappola di verifica pagata qui, e riutilizzabile.** La pagina sembrava non aggiornata: l'app girava da
un `dotnet run` avviato **dodici minuti prima** del commit che accendeva il giro. Il `.dll` in `bin/Debug`
aveva una data *più recente* (l'avevano riscritto i `dotnet test`), ma il processo tiene in memoria
l'assembly caricata all'avvio. Prima di dare la colpa al codice si guarda l'**ora di avvio del processo**,
non la data del file.

### B8 ✅ FUSO — `coordinamenti-lettura`, fuso in `main` il 22 agosto 2026

Cinque commit più il merge `1d74246`, ramo cancellato (locale e origin). Nessun conflitto: il ramo era nato
dopo l'ultimo merge e nessun altro ha toccato quei file.

Contenuto e decisioni: [`feature/2026-08-22-coordinamenti-lettura.md`](feature/2026-08-22-coordinamenti-lettura.md).
In breve, la **lettura** della sezione Coordinamenti: la prosa nasce chiusa in un blocco per tabella, con
l'invito ad aprirla sulla stessa riga del titolo («Arrivi · Testo esteso (2 frasi)»); le decine di tabelle di
un documento ACC si stringono (`10345 → 8423 px` sul blocco Aerovia di LIBB, **−18,6%**); la FIR porta il suo
ICAO (`Greece-LGGG`); e dentro un settore gli ACC si ordinano per **distanza da chi legge** — casa, italiani,
esteri — invece che per alfabeto.

Suite **1 695** verde su net8 **anche dopo il merge**, `Release --no-incremental` **0 avvisi** su due TFM,
verifica live guidata con Edge sulla bozza LIBB.

⚠️ **`dotnet test --artifacts-path` non si usa alla leggera**: sposta l'output, e i progetti che leggono
**fixture accanto all'assembly** ne scoprono di meno — `Vipi.AuroraProfiles.Tests` è passata da 63 casi a 13,
con 11 rossi che sembravano una regressione del merge e non lo erano. Serve solo dove i `bin` sono davvero
bloccati (il progetto E2E, che referenzia `Vipi.Host`); gli altri si lanciano normalmente.

**Non tocca la coda del cutover:** nessuna entità nuova, **nessuna migrazione**. L'unico cambio di forma è la
sostituzione di `GetSectorAccNameMapAsync` con `GetSectorAccRefMapAsync` (→ `AccRef`), propagata nello stesso
giro ai due chiamanti (vIPI ACC e vLOA) — nessuno resta indietro.

⚠️ **Due cose da sapere prima di fondere.** I punti «ICAO» e «ordine» cambiano il **derivato**, e le pagine
pubbliche leggono lo snapshot congelato: sui documenti già pubblicati compaiono alla **prossima release**, non
al merge. E lo scaglione **«casa»** dell'ordinamento non è riproducibile sui dati di sviluppo di oggi (né LIBB,
né LIRR, né LIMM hanno in bozza un coordinamento interno alla propria ACC): è coperto da due test unitari,
**non** dallo schermo.

### B7 ✅ FUSO — `catalogo-punti-suggerimenti`, fuso in `main` la sera del 22 agosto 2026
Sette commit più il merge `2b4480d`, ramo cancellato (locale e origin).
Suite **1 677** verde su net8, `Release --no-incremental` **0 avvisi**, verifica live guidata con Edge su
editor aeroporto (LIBD, LIRF), accordi (LIBB) e sorgenti.

Contenuto e decisioni: [`feature/2026-08-22-catalogo-punti-suggerimenti.md`](feature/2026-08-22-catalogo-punti-suggerimenti.md).
In breve: il catalogo di fix/VOR/NDB diventa una porta (`INavaidSource`), i campi punto suggeriscono e segnano
i nomi inesistenti, gli alias dei fix diventano visibili e cancellabili.

**Non ha toccato la coda del cutover:** niente entità nuove, **niente migrazioni**
— il catalogo vive in memoria. È la ragione per cui è stato progettato così: il deploy è fermo in attesa della
conversione MariaDB (§A) e una tabella in più avrebbe allungato quella coda.

⚠️ Modifiche fuori dal proprio perimetro, da sapere leggendo codice più vecchio: la classe CSS `.cop-unknown` è stata
**rinominata** in `.nav-unknown-txt` (serve a due cose ora), e `AuroraSectorfileParser.ParseNavaids` ha
**cambiato firma** (restituisce `NavaidCatalog`, prende anche il file NDB). Entrambe propagate nello stesso
giro — nessun chiamante resta indietro.


### B5 ✅ CHIUSA — il doc 13 era **già in `main`**, e nessuno se n'era accorto
⚠️ **Non c'era nessuna decisione da prendere.** Scoperto il 22 agosto cancellando i rami fusi: la punta di
`refactor/13-tre-documenti` (`90aa917`, 11 agosto) risultava **antenata di `main`**, cioè zero commit fuori.
Il lavoro è entrato il **15 agosto**, trasportato dal merge di `feature/trasferimenti-acc-app`, che ne
condivideva la storia — e la voce qui è rimasta a chiedere un ok per qualcosa di già fatto.

Verificato sul codice, non sul grafo: `EfDocumentMaintenance` in `main` porta `ReconcilePurposeKeyAsync`,
`MinimaKey` e le sezioni di catalogo mancanti — le tre riconciliazioni one-shot descritte qui sotto. E il
[doc 13](refactor/13-audit-tre-documenti.md) si dichiara **CHIUSO** in testa da allora.

⚠️ **La lezione**: un ramo che il grafo dice a zero commit non è «da fondere», è **già dentro** — e un
elenco di lavori aperti può restare vero a metà per una settimana senza che niente lo contraddica. Il ramo
è stato cancellato (locale e origin) insieme agli altri otto a zero.

<details><summary>La scheda di allora, per memoria</summary>

25 commit, suite **2111** verde su entrambi i TFM, **verifica live fatta** sui tre documenti (copia del
`vipi.db` reale). È il [doc 13](refactor/13-audit-tre-documenti.md): audit di vIPI ACC, vIPI APP e vLOA, nato
dall'osservazione che «la sezione delle versioni dovrebbe essere la stessa per tutti e tre».

Perché conviene farlo entrare: due difetti **uscivano dal documento** — la pagina APP pubblica derivava le
configurazioni dalla versione di lavoro (bozza in pubblico, contro l'invariante del doc 10), e ricerca e
«Cosa è cambiato» indicizzavano documenti nascosti, **sezioni** nascoste e contenuto senza release effettiva.
Il resto è uniformità: catalogo fonte unica anche di «chi rende il corpo» e «obbligatoria», ciclo AIRAC del
documento invece che di oggi, pannello release uguale nei quattro editor.

⚠️ **«Build senza errori» è stato falso fino all'11 agosto 2026.** Il ramo portava 14 chiavi duplicate nei
`.resx`: con `-warnaserror` la CI dava **28 errori**, e nessuno l'aveva visto perché il ramo non era mai
stato spinto e la suite locale resta verde (`dotnet test` non usa quel flag). Corretto, con tre guardie che
leggono i `.resx` dal disco.

Da sapere prima del merge: al primo avvio girano **tre riconciliazioni one-shot** (chiavi delle direzioni
vLOA + «Purpose», placeholder vuoti di «minima», sezioni di catalogo mancanti su APP/vLOA). Sul DB di
sviluppo hanno toccato 15 sezioni e rimosso 18 blocchi. Sono idempotenti e non toccano le release già
pubblicate.

**Decisione da prendere:** merge in `main` (serve l'ok esplicito, come per il doc 10) e push.

</details>

### B6 ✅ FUSA — `feature/trasferimenti-acc-app`, fusa in `main` il 15 agosto 2026
72 commit, suite **2403** verde su entrambi i TFM, `Release --no-incremental` 0 warning, verifica live su
copia del `vipi.db` reale in **tutti e sei** i giri (ventuno difetti trovati proprio lì, quasi nessuno
visibile alla suite). Contenuto in **E6**; sei schede in `docs/feature/`, l'ultima è
`2026-08-12-editor-trasferimenti-rifiniture.md`. Fusa perché la consegna a Ivao.It parte da `main` e il
committente ha chiesto che partisse **con tutto dentro**.

⚠️ Il ramo portava anche il proprio giro dell'audit database (§G) nella forma nata lì: le due migrazioni
avevano **lo stesso identificativo** di quelle rigenerate su `main`, apposta. Al merge si sono scontrate sullo
stesso percorso, e si è tenuta **la copia del ramo trasferimenti**, non quella di `main`: è l'unica il cui
`Designer` descrive il modello fuso (l'altra non conosceva le colonne dei trasferimenti). L'unica differenza
nel corpo delle due migrazioni era il commento.

⚠️ La **PR #13 resta aperta con un corpo vecchio** (descrive il primo giro, «autorizzazione e trasferimento
sono due eventi», quando i giri sono diventati sei). Va chiusa a mano dopo il push di `main`.

### B1 ✅ FUSA — `feature/aree-speciali-hardening`, verificata il 6 agosto e fusa il 7 agosto 2026
**Fusa in `main` in fast-forward** (21 commit, `bbbbf2b` → `7557ec4`) e da lì nel ramo del cutover
`feat/persistenza-mysql`. Il ramo può essere cancellato dopo il push di `main`. Sotto, l'esito della
verifica live che ha sbloccato la decisione.
I quattro punti sono stati guidati con la skill `verifica-live` su una copia del `vipi.db` reale, con import
veri contro l'API IVAO. Esito per esteso nella carta,
[`feature/2026-08-03-aree-regolamentate-hardening.md`](feature/2026-08-03-aree-regolamentate-hardening.md).

- **Riconciliazione al boot**: 993 → 230 aree, zero orfane, colonna `CenterId` sparita. Come previsto.
- **1. Interruttore** ✅ con la categoria spenta l'import aggiorna ACC e settori e **lascia le aree intatte**
  (230 aree / 247 legami, conteggi per ACC identici); a video «❄ Congelate». E dura **24 secondi** contro i
  minuti dell'import pieno: la fetch non parte davvero.
- **2. Dangling** ✅ cancellata a mano l'area `1131` citata dalla vIPI Brindisi: «⚠ 1131 non più disponibile»
  nell'editor, rilievo in `/services/vsop/admin/diagnostics`, `/vsop/health` a **Degraded**.
- **3. R49 «Zita»** ⚠️ la **meccanica multi-ACC funziona** — 7 aree con più enti, fra cui `WEST/EAST SARDINIA`
  e `Donald`/`Eolia` che ora appartengono anche a LIRR — ma **l'esempio è invecchiato**: oggi la sorgente
  elenca la 8870 sotto LIPP, LIRO, LIVK, LIZZ e non più sotto LIRR. Non è l'import: nello stesso giro la
  fetch di LIRR ha portato 105 legami. Sono cambiati gli elenchi IVAO fra il 3 e il 6 agosto.
- **4. Aree estere** ✅ «Importa aree» su LFMM → 162 aree e l'ente si accende; «Escludi aree» → 162 legami
  rimossi, torna «non importate», archivio di nuovo a 230/247 senza orfane (le 4 aree condivise restano).

⚠️ **Il ramo contiene anche B2**: `feature/aurora-bridge` è interamente dentro questi 18 commit (i 7 del
bridge, da `b5f1f58` a `7e7e406`, sono i suoi antenati). Fondere B1 porta dentro anche il bridge Aurora — e
l'endpoint `POST /vsop/api/v1/transfers/resolve` con esso. Va deciso in B4, non scoperto al merge.

Resta non provato un solo dettaglio, perché serve un ACC estero già citato da un documento: che riaccendere
l'ente faccia **rientrare** un'area diventata dangling.

### B2 ✅ FUSO — `feature/aurora-bridge`, dentro B1, endpoint spento
È entrato in produzione **come codice, non come superficie**: `AuroraBridge:Enabled` nasce `false` e la
rotta non si registra affatto. Accenderlo resta una decisione separata, e il giorno in cui si accende la
prima sessione col tool va guidata — non è mai stato esercitato contro un host remoto vero.
Il tool desktop funziona **solo** contro un host locale finché l'endpoint
`POST /vsop/api/v1/transfers/resolve` non è acceso in produzione. Chiuse per decisione: i sorvoli LIBB senza
livello (lacuna redazionale, il tool non deve indovinare) e il pacchetto macOS.

⚠️ **Non è un ramo a sé**: sta per intero dentro B1. Fondere B1 porta dentro anche questo.

**Rivisto prima di considerarlo mergiabile** — era l'unica superficie pubblica, anonima e interrogabile da
fuori del sito, e aveva tre difetti nella protezione (nessuno nella logica, che è pura e testata):
- **`AuroraBridge:Enabled`, default `false`**: spento, la rotta non si registra affatto. È ciò che rende
  reversibile la decisione di B4 — fondere B1 non aggiunge superficie pubblica finché nessuno la accende.
- **Tetto complessivo** (600/min) accanto a quello per IP: dietro il reverse proxy l'indirizzo arriva da
  `X-Forwarded-For` e `UseForwardedHeaders` gira senza proxy noti, quindi **la chiave del tetto per IP la
  sceglie il chiamante**. Aggiunto anche un tetto alle chiavi tracciate: ruotare l'header faceva crescere il
  dizionario dei contatori senza limite — esaurimento di memoria a colpi di richieste da 200 byte.
- **Cache di 30 s della topologia globale**, solo per il bridge: ogni richiesta rileggeva tutti i settori
  attivi, e su `atc.it.ivao.aero` quel costo lo pagherebbe il database condiviso col sito che ci ospita.
- `MaxRequestBytes` era un'opzione morta: il tetto del corpo era una costante.

Osservato scrivendo il test invece di darlo per buono: a endpoint spento la risposta è **405**, non 404 — il
catch-all delle pagine risponde al GET di qualunque percorso, a mancare è il verbo. Il tool desktop traduce
404/405 in «su questo sito il bridge non è attivo» invece del codice nudo.

Suite **944 verde**, e le correzioni sono state fuse anche in B1, che altrimenti se le sarebbe perse.

**Cosa resta**: decidere se accenderlo e quando, cioè B4. Il tool non è mai stato esercitato contro un host
remoto vero: se si accende, la prima sessione con Aurora va guidata.

### B3 ✅ `fix/dataprotection-retry` — fuso il 6 agosto 2026
Fuso in `feat/persistenza-mysql` e ramo cancellato. Il commit aggiunge
`EnableRetryOnFailure` al context del key-ring Data Protection, che apriva la connessione senza, a
differenza di `VipiDbContext`: su Neon un transient sul key-ring uccideva antiforgery, cookie di auth e
state OIDC (i «Correlation failed» del 3 agosto). Il passaggio a net8/Pomelo non aveva toccato il file, e
il ramo Postgres resta in piedi perché Neon resta l'ambiente di prova ⇒ fusione pulita, suite verde
(net8 309 · net10 300 + gli altri progetti).

⚠️ Vive dentro A4: quando il key-store passerà a MariaDB, la stessa resilienza va rifatta lì — Pomelo ha il
proprio `EnableRetryOnFailure`, e questa registrazione oggi è nel ramo `Persistence:Provider=Postgres`.

### B4 ✅ DECISO il 7 agosto 2026 — in produzione va `main` + B1
Con B2 dentro, perché il ramo del bridge sta per intero in quello delle aree. Eseguito: B1 fusa in `main`,
`main` fusa in `feat/persistenza-mysql`, suite verde su entrambi i TFM.

**Cosa resta di questa decisione, in ordine:**
1. 🔴 **`git push origin main`** — 21 commit locali non ancora sul remoto. Fa partire il redeploy Render.
2. 🟡 **Dopo il deploy**: al primo boot Neon riconcilia l'archivio (993 → 230 legami, aree estere spente);
   poi premere **«Importa da sorgente»**, perché il backfill recupera un solo legame per area.
3. 🟡 **Rifare il travaso di A3** su quei dati: il `.sql` del 6 agosto non vale più.
4. 🟢 Cancellare i rami `feature/aree-speciali-hardening` e `feature/aurora-bridge`, ormai contenuti in `main`.

---

## C. Debito noto — non urgente, ma non dimenticabile

### C1 ✅ Il percorso Npgsql di `ISchemaDriftProbe` è stato eseguito — 9 agosto 2026
Host locale puntato a **Neon**, cioè l'unico modo di eseguirlo (in locale non c'è un Postgres). Non ci si è
fermati al «non segnala niente», che da solo non distingue *pulito* da *mai eseguito*: si è **introdotto un
drift finto** — una colonna `Accs.ZzSondaDrift` — e la sonda l'ha vista.

- a schema pulito: `/services/vsop/admin/diagnostics` «nessuna incongruenza», `/vsop/health` **Healthy**;
- con la colonna estranea: rilievo **«Colonna orfana nello schema — `Accs.ZzSondaDrift`»**, col messaggio
  che conta («se è una rinomina, i dati sono ancora QUI e la colonna nuova è vuota»), e `/vsop/health` a
  **Degraded**;
- rimossa la colonna: di nuovo pulito e Healthy. Neon è tornato esattamente com'era.

Nessun falso positivo di tipo sulle 39 tabelle reali, quindi la mappa alias di `SchemaDriftAnalyzer.Canonical`
non è stata toccata.

### C2 ✅ CHIUSA il 9 agosto 2026 — `ImportSids` non è spento da nessuna parte
Il timore era: la migration dell'8 luglio creò la colonna con `defaultValue: false` e il reconciler la
backfillava a `false`, quindi su un database dove la riga `ImportPolicies` **esisteva già** la categoria
sarebbe nata spenta, in modo indistinguibile da una scelta dell'admin.

**Quella riga non esiste.** Su Neon `ImportPolicies` ha **zero righe**, e senza riga
`EfImportPolicyStore.GetAsync` torna `ImportPolicySnapshot.AllImported` — tutto importato, SID comprese. Il
`.sql` del travaso porta la stessa situazione in produzione, quindi il trabocchetto non si materializza né
di qua né di là. Verificato leggendo i dati veri, non l'interfaccia.

⚠️ Resta vera la regola generale, ed è quella che vale la pena ricordare: un `bool NOT NULL` nuovo nasce
`false` ovunque, migration e reconciler compresi, ed è veleno per un flag **opt-out**. Memoria:
[[bool-column-default-trap]]. Dal branch delle aree il default sta nel modello (`HasDefaultValue`) e il
reconciler lo legge.

### C3 🟡 ADR-0007 punto (b): migrazioni Postgres versionate — **rischio accettato, con un rilevatore che ora funziona**
Il `PostgresSchemaReconciler` copre **solo le aggiunte di colonna**: il primo rename, drop o cambio di tipo
su Neon va applicato a mano. Resta vero.

**Perché non si costruisce adesso il terzo set di migrazioni.** Costerebbe **emettere ogni cambio di schema
tre volte** (SQLite, MySQL, Postgres) per sempre — e ADR-0007 §D4-ter dichiara quel costo già pesante a
due. Lo si spenderebbe per un ambiente che è **di prova** e che, a cutover riuscito, è candidato a essere
ritirato: la decisione su Neon è ancora aperta e va presa prima, non dopo.

**Cosa rende accettabile aspettare, e non era vero prima del 9 agosto:** il guasto temuto — una rinomina
che lascia i dati nella colonna vecchia mentre l'app legge la nuova, vuota, **senza lanciare niente** — ora
ha un rilevatore **provato sul campo** (C1): compare in `/services/vsop/admin/diagnostics` e porta `/vsop/health` a
Degraded. Il rischio passa da *silenzioso* a *rumoroso*, che è la differenza che conta.

**Cosa lo riaprirebbe, e allora va costruito:** se Neon smettesse di essere un ambiente di prova (dati che
non si possono ricreare), oppure se servisse un rename/drop/cambio-tipo — a quel punto il baseline si
genera dal modello **mentre la sonda dice che lo schema combacia**, che è esattamente lo stato verificato
oggi, e si timbra come applicato. È la stessa ricetta già usata per MariaDB.

#### C3-bis 🟡 Decisione su Neon — riesaminata il 9 agosto 2026, esito: **tenerlo fino a dopo il cutover**
«Ritirare Neon» e «chiudere il sito di prova» sono la stessa cosa: il servizio Render è senza stato, i dati
stanno lì. Le alternative non reggono — un MySQL gestito gratuito non parla `utf8mb4_uca1400_as_cs` (un
ambiente di prova che mente è peggio di nessun ambiente), e SQLite su disco effimero perde i dati a ogni
redeploy.

**Cosa è cambiato, e non basta a decidere adesso.** Come banco di prova del *database* Neon **non serve
più**: la MariaDB locale coi dati veri riproduce la produzione meglio, ed è lei ad aver trovato i tre bug di
A6. E C1 ha alleggerito C3, rendendo rumoroso un guasto che era silenzioso. Ma ciò che Neon dà **non è il
database**: è l'unico ambiente **hostato** — reverse proxy, WebSocket, TLS, redirect OIDC, key-ring senza
disco persistente — cioè proprio quello che A9/A10 devono ancora chiarire con loro. E il `.sql` del cutover
nasce da lì: fino al passaggio serve per definizione.

**Quando decidere, e con quale prova.** Dopo il cutover e **un ciclo AIRAC pubblicato dal server nuovo**
senza sorprese. La domanda diventa allora osservabile invece che opinabile: *in quelle settimane Neon è
stato aperto anche una sola volta?*
- **No** → si chiude. Spariscono C1, C3 e un intero dialetto: `PostgresSchemaReconciler`,
  `PostgresSchemaDriftProbe`, il ramo Postgres di `DataProtectionSchema` e `DependencyInjection`,
  `--from-postgres`/`--to-postgres` di `Vipi.DbSeed`. Da tre dialetti a due.
- **Sì** → è un ambiente che conta davvero, e **C3 va costruita**, non più rimandata.

⚠️ Indipendente dalla decisione: la password di Neon è passata in chat il 9 agosto 2026 e **va ruotata**.

### C4 ✅ Cache-busting rimesso a posto — 9 agosto 2026, senza aspettare EF Core 10
Era accettato come costo di net8: niente `@Assets[...]`, quindi un'unica impronta per tutti gli asset (il
MVID dell'assembly), e a ogni deploy il browser riscaricava **tutto**, anche i file identici byte per byte.

Ora `AssetVersion` calcola lo **SHA-256 del contenuto di ogni file**, letto dallo **stesso provider** che poi
lo serve — così le due cose non possono divergere — e ne mette 8 caratteri nell'URL. Un asset immutato
conserva il proprio URL e resta valido in cache; cambia solo ciò che è cambiato davvero. Le impronte si
calcolano una volta per percorso e restano in memoria.

Il ripiego è deliberato: se un file non si risolve si torna al MVID, **non** a un URL nudo — invalidare
troppo è innocuo, invalidare troppo poco lascia un CSS vecchio in cache dopo un aggiornamento.

Guardia: `Ogni_asset_ha_la_propria_impronta_di_contenuto` in `Vipi.E2E.Tests` guarda la pagina servita e
pretende impronte **diverse** fra asset diversi — fallisce sia se torna l'impronta unica sia se i file non
si risolvono e si sta usando il ripiego. Vista fallire sull'implementazione precedente.

### C5 ✅ Audit 22 luglio — le due voci residue sono risolte, 9 agosto 2026
`history/audit-2026-07-22-criticita-full-stack.md`. Fasi 1 e 2 erano già eseguite.

**A2 (scala Blazor) — deciso, non costruito.** La scala attesa è **una sola istanza**: un processo dietro
`proxy_pass` verso un solo indirizzo. Con un'istanza il backplane non serve a nulla, e aggiungerlo ora
sarebbe infrastruttura da mantenere per un problema che non abbiamo. La decisione ha però un vincolo che
va **detto a chi amministra la macchina**, perché il guasto è vistoso e la causa no: Blazor Server tiene lo
stato dell'utente nel processo che ha aperto il circuito, quindi **due processi dietro un bilanciatore
fanno cadere le pagine in riconnessione continua**. Se un domani serve scalare, prima il backplane, poi il
secondo processo. Scritto in `deploy/atc-ivao/nginx-vipi.conf`, dove lo legge chi tocca il proxy.

⚠️ **Trovato mentre si verificava questo: `nginx-vipi.conf` conteneva `proxy_read_send_timeout`, che non è
una direttiva nginx.** Non è un dettaglio di stile: nginx rifiuta di avviarsi con «unknown directive», e la
consegna si sarebbe fermata lì. Rimossa — le due valide (`proxy_read_timeout`, `proxy_send_timeout`) erano
già presenti sotto.

**D1 (provisioning) — non è più una voce di debito, è una dipendenza da loro.** La parte di codice è chiusa
dal 22 luglio (`ProductionIdentityGuard` fa hard-fail se l'identità di sviluppo è attiva fuori da
Development, con test sul percorso di produzione). Quel che resta — montare i claim e gli **staff-code IVAO
reali** — vive già come **A9/A10** (segreti e redirect) ed **E4** (conferma dei codici staff): tenerlo anche
qui era contarlo due volte.

### C6 ✅ CHIUSA il 27 agosto 2026 — la chiave che si sposta si RIPUNTA (era: il documento pubblicato va muto)

Trovato il **25 agosto 2026** scrivendo la carta [documenti da
rivedere](feature/2026-08-25-documenti-da-rivedere.md) §3a. **Non** è un difetto introdotto da quel lavoro:
c'è oggi, in produzione.

`AccVipiReleaseTarget.TryDescribe` compone la chiave di release come `{acc}|{callsign del settore primario}`
(`AccVipiReleaseTarget.cs:57`); per l'APP non remotizzato la chiave **è** il callsign. Quelle chiavi non sono
stabili: le cambiano un settore riparentato, un primario che cambia, una rinomina in sorgente.

**Cosa succede quando si sposta.** Le `DocRelease` restano scritte sotto la chiave **vecchia**; `ManagedDoc`
descrive il documento con quella **nuova**; `PublicDocumentGate` chiede le release della nuova, non ne trova,
e il documento — pubblicato, con release valide in archivio — **sparisce dal pubblico**. Nessun errore,
nessun rilievo: la stessa famiglia di guasto del §0 di quella carta.

⚠️ **Latente, non manifesto**: nel `vipi.db` del 18 agosto le chiavi in archivio (`LIBB|LIBB_ES_CTR`,
`LIMM|LIMM_WS2_CTR`) **combaciano** con quelle vive. Va aperto perché è a un rename di distanza, non perché
stia bruciando.

**Rilevatore, già previsto**: il giro di deriva della casella impatti apre `ReleaseKeyMoved` quando il
bersaglio di oggi non ha release ma il documento ne ha sotto un'altra chiave (E11, slice 6). **Rilevare non
è riparare**: la riparazione — migrare le release alla chiave nuova, oppure rendere la chiave stabile — è
una decisione a sé, e va presa sapendo che una chiave stabile per l'ACC vorrebbe dire un identificativo
proprio del documento al posto del callsign.

### C7 ✅ CHIUSA il 27 agosto 2026 — i tre resti dell'analisi del 25 agosto sulla cancellazione dei dati importati

Analisi completa in
[`history/audit-2026-08-25-cancellazione-dati-importati.md`](history/audit-2026-08-25-cancellazione-dati-importati.md).
Sette rilievi: quattro chiusi con **E11**, uno è diventato **C6**, questi tre restano. Sono piccoli,
indipendenti fra loro, e ognuno ha il suo punto esatto nel codice.

**C7a — la policy di import cancellata torna «tutto importato» in silenzio.**
`EfImportPolicyStore.GetAsync` (riga 28) su riga assente ritorna `ImportPolicySnapshot.AllImported`: una
`DELETE` sulla tabella riporta il regime a «la sorgente può scrivere tutto», e il primo giro dopo
**sovrascrive TA e piste messe a mano**. Il dato per accorgersene c'è già — `GetInfoAsync` distingue «decisa
da qualcuno» da «nata dai default» (`UpdatedUtc == null`) — quindi basta un rilievo di diagnostica quando
almeno una categoria risulta manuale e nessuno l'ha decisa. ⚠️ Non è teorico: la riga è **una sola** in tutto
il database.

**C7b — le cancellazioni strutturali non lasciano traccia.**
`StructureEditingService.cs:127` (ACC), `:144` (aeroporto), `:297` (settore) non scrivono nel registro,
mentre l'eliminazione di un **documento** ci finisce dal 22 agosto. Serve `AuditAction.Delete` con
ICAO/callsign nei dettagli, scritto **prima** della cancellazione (dopo, il nome non è più leggibile — è la
lezione già pagata su `EliminaBozzaAsync`). È il **buco 5** dell'audit del 22 agosto, chiuso solo per
`SetParentAsync`. ⚠️ Da fare **dopo** E11, così riusa le stesse chiavi di frase.

**C7c — un ACC estero nuovo nasce con le aree accese.**
`EfNeighbourRepository.cs:53` e `:258` creano l'`Acc` estero senza toccare `SpecialAreasEnabled`, il cui
default d'entità è **`true`**: il giro delle 24h si porta dentro tutte le sue aree regolamentate. Il tappo
del 3 agosto (`OptOutForeignAreasAsync`) è **one-shot** e vale solo per gli esteri che c'erano allora. Una
riga: `SpecialAreasEnabled = false` alla creazione.

---

## D. ✅ Verifiche live arretrate — **sezione chiusa il 9 agosto 2026**

Erano lavori già scritti e testati che nessuno aveva mai **guidato**. Tutte rifatte su MariaDB coi dati
veri, non su un database di comodo.

- ✅ **Aree regolamentate** — 6 agosto: esito in B1.
- ✅ **Settori esteri aggiunti a mano.** In `/services/vsop/admin/neighbours`, su coppia confermata, provati i tre
  esiti che contano: **aggiunta** di `LGRP_APP` a LGGG → verificato su IVAO e materializzato con dati veri
  (*Rodos Approach*, 127.250, poligono di 3378 caratteri), **riproiettato** come `Sector` attivo e presente
  nel **picker del ricevente** (`LGRP_APP LGGG`); **ri-aggiunta** dello stesso → «already present», non un
  errore; **dirottamento** di `LGKR_APP` su LAAA → rifiutato («appartiene già all'ACC LGGG»), e soprattutto
  **nessuna riga fantasma** sotto LAAA.
  ⚠️ Il dropdown del picker è governato da `@onfocus`: un click di automazione lo chiude. Va aperto con
  `page.focus` e riempito da tastiera, senza altri click — altrimenti sembra vuoto quando non lo è.
- ✅ **Coordinamenti/sorvoli rielaborati.** Sulla vIPI ACC di LIBB: CoP `ALL` → «su tutti i punti», `ALL to
  GR` → «su tutti i punti verso GR», **nessuna riga col vecchio «su —»**; sorvoli senza aeroporto presenti;
  parità resa («*stabile a livello 260 **pari** su tutti i punti verso GR*»). Sulla vLOA LIBB↔LGGG:
  coordinamenti in stile ACC e frasi in inglese. **Lookup IVAO**: scritto `LFPG`, il tasto compare e riempie
  «Paris Charles de Gaulle» — e l'aeroporto **non entra nel catalogo** (92 prima, 92 dopo), che era il
  vincolo.
  ⚠️ **Difetto trovato qui e corretto**: in inglese la parità era attaccata con l'ordine italiano —
  «at level 260 even», «for a level odd». Ora l'ordine sta nel template (`WithParity`, `ForLevelParity`):
  «at level 260 (even)», «for an odd level». Un test **fotografava il difetto** invece di impedirlo ed è
  stato corretto: è la ragione per cui non era mai emerso.
- ✅ **Retention pubblicazione.** Non solo conteggi: fatta scattare. Una pubblicazione in più su LIBB →
  `Superseded` 12→13 e versioni archiviate **ferme a 3** (il cap regge, era l'off-by-one del 21 luglio).
  Poi tre release retrodatate oltre soglia e riavvio: il boot sweep ne ha potate **esattamente tre**
  (53→50 release, `Effective` intatte), e un secondo riavvio è stato **no-op**.

---

## E. Funzionalità aperte (da `HANDOFF.md` §5)

Ordinate per valore, come lì.

### E1 ✅ Live IVAO — **chiusa il 9 agosto 2026**: tre voci su quattro erano già morte
L'elenco veniva da prima della riscrittura della vista live (doc 12, 31 luglio) e non era stato ricontrollato.

- ~~**Identità «P»** legata al callsign connesso~~ — **già fatto**: `/services/vsop/live` *è* la tua postazione, presa
  dalla connessione IVAO. Il selettore manuale non esiste più, e nemmeno la pagina Ridotta che lo ospitava
  (rimossa al Round 12).
- ~~**Endpoint membri divisione** da confermare~~ — **confermato da tempo, in negativo**:
  `/v2/divisions/{id}/members` risponde **404** e `/users` dà 500 col token app. È la ragione per cui esiste
  il roster costruito dai login ([[staff-roster-design]]). Non c'era niente da confermare, solo da cancellare.
- ~~Estendere **`live=true`** a vIPI aeroporto e vLOA~~ — **obsoleta**: quel parametro non esiste più. La
  vista live è una pagina unificata legata all'**ente**, non un livello di dettaglio dei documenti.
- **Mapping token-handler → callsign: valutato, e la tabella esplicita NON serve.** L'euristica di
  `TransferOnlineResolver` accetta match esatto, segmento e sottostringa ≥4. Provata sui **313 callsign
  reali**: le coppie che collidono sono **zero**, e non per caso — nessun callsign del catalogo è privo di
  underscore (quindi la regola «segmento» non può scattare) e nessuno è contenuto in un altro. Nella pratica
  l'euristica **si riduce al match esatto**: una tabella di mapping sarebbe manutenzione in più a parità di
  comportamento.

  La scelta è però resa **revocabile da sola**: nuova regola nella diagnostica, **«Callsign ambiguo
  (risoluzione live)»**, che riusa il resolver invece di ricopiarne le regole — se l'euristica cambia, la
  diagnosi cambia con lei. Il giorno che nasce un settore che collide si vede in `/services/vsop/admin/diagnostics`
  invece che in frequenza. Verificata sui dati veri: nessun rilievo, nessun rumore.

### E2 Dati reali che mancano
- ✅ ~~**Shape reali delle TWR** dal sectorfile GitHub~~ — **già fatto e verificato il 9 agosto 2026.**
  `GithubTowerShapeService` applica i poligoni di `DYNAMIC_SEC/twrs.tfl` **prima** del cerchio sintetico ed è
  agganciato all'import automatico più un bottone nell'editor. Sui dati veri: **68 TWR su 84 hanno un
  poligono reale**, 16 restano col cerchio. E i 16 non sono un buco: scaricato `twrs.tfl` e confrontato,
  **nessuno dei 16 callsign è presente nel file** — il cerchio copre esattamente le torri che nemmeno la
  sorgente ha.
- ✅ ~~**Minime MVA da GitHub**~~ — **fatte il 22 agosto 2026, come CARTA e non come tabella.** Verificate
  live sulla copia del `vipi.db` reale.

  L'obiezione del 9 agosto era giusta e resta in piedi, ma riguardava **la tabella**: `area → quota` non si
  può ricostruire dal formato. L'etichetta è un testo piazzato a una coordinata sua, indipendente dai
  poligoni (in `liph.mva` le dieci `L;` stanno tutte in cima al file); il legame etichetta↔area va indovinato
  geometricamente e su 345 etichette **70 cadono dentro più aree annidate e 13 dentro nessuna**; il testo non
  è un numero (`TRL`, `NO MINIMA`, `80/TRL`, `*30/40`) e nessun campo dice le unità (`110` sono centinaia di
  piedi, `1500` sono piedi); e **92 tracciati su 315 sono aperti**, quindi non sono aree.

  Quello che il formato **dichiara** è un'altra cosa: il proprietario del file. `ENRMVA/{acc}.mva` è
  l'enroute di un ACC, `{icao}.mva` è un aeroporto — e a quella granularità l'attribuzione non si indovina,
  si legge. Misurato: i 24 file per-aeroporto corrispondono tutti a un APP esistente, zero orfani. Perciò la
  sezione mostra **una carta per file**, disegnata verbatim su fondo topografico (tracciati aperti compresi,
  etichette col testo originale). È esattamente ciò che il controllore vede in Aurora, e non asserisce nulla
  che il sectorfile non dica.

  Sezione `minima` da `Editorial` a `Derived`: riprende il toggle Live/Congelata e si congela nello snapshot
  di release. Nessuno storage — le tabelle `VectoringMinimaSet/Row`, che descrivevano la strada scartata,
  sono state droppate nello stesso giro (modello dati §7.5).

  ✅ **Chiuso il 23 agosto 2026 dal committente**: i 25 APP su 49 senza file (fra cui LIRF, LIMC, LIML,
  LIME, LIPS) sono **procedurali**, e una carta di minime di vettoramento **non ce l'hanno**. Quindi il file
  che manca non è una lacuna dell'archivio: è la risposta giusta, e la sezione che non compare è corretta.
  Non c'è nessuna richiesta da mandare all'AOD.
- 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso. Si sistemano
  dalla pagina: il filtro «solo da agganciare» li raccoglie.
- ⚠️ **La SID `BANA8A` di LIBD è GIÀ a `9000`** nel `vipi.db` di sviluppo (verificato il 23 agosto 2026):
  qualcuno l'ha corretta e nessun documento se n'era accorto. **Da rifare in produzione**, dove nessuno l'ha
  guardata.
  ⚠️ **Ma nello stesso aeroporto ce n'è un'altra, e non era in elenco:** `BANA5Z` (pista 25) ha
  `InitialClimb = "500"` → resa «500 ft», mentre tutte le altre BANAV stanno a 5000 o 9000. È quasi
  certamente uno zero perduto, ma **correggerla è una decisione editoriale** e la prende chi conosce la
  procedura. (Nel DB di sviluppo c'è anche `TESTE8A` a `80`, che però è una riga di prova, `IsImported = 0`.)
  ℹ️ Il valore non arriva dal sectorfile: `libd.sid` **non porta la quota iniziale**, la scrivono a mano gli
  editori — quindi è un dato che nessun import ricontrolla, e nessun import sovrascrive.
- Il CoP **`BESIV`** dell'accordo `LIBB_ES_CTR ⇄ LDZO_CTR` (sorvoli, verso LDZO→LIBB) **non esiste nel
  sectorfile**; a una lettera di distanza c'è `BEKIV`. Lo segnala da solo l'editor degli accordi dal giro del
  22 agosto ([feature/2026-08-22-catalogo-punti-suggerimenti.md](feature/2026-08-22-catalogo-punti-suggerimenti.md)),
  ma **correggerlo è una decisione editoriale** — può essere un typo o un punto estero non elencato — e la
  prende chi conosce l'accordo. Finché resta così, compare anche fra i «punti presenti in un verso solo» del
  cruscotto delle lacune, dove sembra un'asimmetria dell'archivio e non un errore di scrittura.
- Stessa cosa da rifare **sui CoP di produzione**: il conteggio (1 su 52) è del DB di sviluppo, che ha 52
  clausole. Aperta la pagina degli accordi, i nomi fuori catalogo si vedono sottolineati senza cercarli.

### E3 🟡 Fonte unica — «presidenza aeroporto» ✅ fatta il 9 agosto 2026; resta il distacco dai `Sector`
Documenti e AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi: **quella parte
resta aperta** ed è il grosso del follow-up del Round 20. La **risalita**, che era l'altra metà, è fatta.

**`AirportPresidencyResolver`** (Application/Live, puro) risponde a «chi controlla questo aeroporto adesso»
nella forma scelta dal committente: le posizioni **sue** online dal gate in su (DEL → GND → TWR → APP), più
**chi copre il resto** risalendo la gerarchia, e UNICOM se non c'è nessuno dei due. La risposta non è una
sola perché non lo è la domanda: al gate serve il ground, in avvicinamento la torre.

⚠️ **La regola di confronto è quella dei trasferimenti, riusata e non riscritta** (`TransferOnlineResolver`).
È il punto che conta: due logiche di risalita affiancate darebbero, prima o poi, risposte diverse sullo
stesso settore — e la sentinella dei callsign ambigui in diagnostica vale già per entrambe.

**Sostituisce una risposta binaria che aveva due difetti.** La vista live diceva «delegato» se esisteva un
callsign online che *cominciava* con l'ICAO: non diceva **chi** chiamare, e contava anche l'**ATIS**, che è
una frequenza e non una posizione che controlla. Ora si parte dalle posizioni note dell'aeroporto, quindi
l'ATIS non entra.

**Fatto:** risolutore + 7 test (compresi il caso «solo il ground online, il resto lo copre chi sta sopra» e
l'avvicinamento dell'aeroporto che non deve comparire due volte), innestato nelle **chip aeroporto** della
vista live, che ora nel tooltip dicono chi presiede invece di una stringa fissa. Verificato a schermo:
`LIPA`/`LIPI` → «Nobody online: UNICOM». ℹ️ Il ramo positivo non era osservabile in quel momento — i tre
ATC realmente online (`LIEO_EW0_APP`, `LIMC_ANE_APP`, `LIME_TWR`) non toccano nessuno degli aeroporti
pubblicati — ed è coperto dai test.

**Innestato in tutti e tre i punti** (9 agosto 2026), con due modi diversi e deliberati:
- **chip della vista live** — il tooltip dice chi presiede;
- **`AirportQuickPanel`** — riga «Adesso» accanto a TA/TL/vento/piste. La presidenza **arriva come parametro**
  dalla pagina live, che l'ha già risolta per le chip: ricalcolarla dentro il pannello vorrebbe dire rifare le
  query a ogni tick del feed, e rischiare che due parti della stessa schermata dicano cose diverse;
- **viewer dell'aeroporto** `/services/vsop/{acc}/airports?icao=` — riga nel riepilogo, risolta da
  `IAirportPresidencyService` perché quella pagina sta fuori dalla vista live e non ha un contesto pronto.
  Risolta nel ciclo di vita, mai nel render.

⚠️ **Difetto corretto strada facendo:** il conteggio «ATC online» del viewer contava anche l'**ATIS**, che è
una frequenza registrata e non qualcuno che risponde — un aeroporto deserto poteva mostrare «1 online».

Verificato a schermo su tutti e tre: chip → «Nobody online: UNICOM», vista rapida → «ADESSO nessuno online —
UNICOM», viewer → «Now Nobody online: UNICOM» accanto al ciclo AIRAC.

### E4 🟡 Auth di produzione — ora si **vede**, e i codici veri dicono già qualcosa
I pattern admin (`^IT-DIR$`, `^LI[A-Z0-9]+-CH$`, …) erano ipotesi. Due contromisure, fatte il 9 agosto 2026:
- **scheda «Chi può editare»** in `/services/vsop/admin/diagnostics`: i pattern in vigore a confronto con i codici
  staff **realmente osservati ai login** (IVAO non espone l'elenco degli staffisti — `/members` è 404 — quindi
  il roster costruito dagli accessi è l'unica fonte possibile);
- **rilievo grave** nel report di consistenza (quindi anche in `/vsop/health`) quando **nessuno** degli
  staffisti conosciuti risulta admin. Non scatta a roster vuoto: su un'installazione nuova nessuno ha ancora
  fatto login, e segnalarlo lì sarebbe solo rumore.

I pattern sono ora una **fonte sola** (`AdminStaffCodes`), condivisa fra chi decide e chi diagnostica: una
diagnosi che se li ricalcolasse potrebbe dire «tutto a posto» mentre l'autorizzazione ne usa altri.

**Cosa dicono i dati veri** (roster attuale, 5 staffisti):

| VID | codici osservati | vale admin |
|---|---|---|
| 201143 | `IT-AOC`, **`IT-SOC`** | `IT-AOC` |
| 286571 | **`IT-T01`** | — |
| 516571 | **`IT-FOC`** | — |
| 657465 | `IT-ADIR`, **`IT-FOAC`** | `IT-ADIR` |
| 704798 | `IT-AOA1`, **`IT-T03`** | `IT-AOA1` |

Quindi: **il formato è confermato** (`IT-XXX`), tre su cinque sono admin, e ci sono **quattro codici veri non
coperti** — `IT-SOC`, `IT-T01`, `IT-FOC`, `IT-FOAC`. Se debbano valere admin **non è una domanda tecnica**:
un coordinatore training o un flight-ops devono poter editare le vIPI? È la decisione che resta a voi.
Nessun codice chief `{ACC}-CH` è ancora comparso: quel pattern resta **non verificato**.

✅ **DECISA il 22 agosto 2026 (sera) dal committente: lo staff di divisione è admin, tutto.** Il default di
`Division:AdminRolePatterns` è ora il jolly `[A-Z0-9]+`, cioè `^IT-[A-Z0-9]+$` (carta: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md) §1): i quattro codici scoperti
entrano, e soprattutto **un ruolo nuovo della divisione non nasce più escluso** — l'elenco puntuale
sbagliava in silenzio, e se ne accorgeva solo chi restava fuori. Il jolly **non allarga oltre la divisione**:
un codice `IT-…` lo assegna il portale IVAO solo allo staff di divisione, e il prefisso resta la barriera
(`DE-DIR` non è admin qui). Il lato chief ACC non cambia e **resta l'unica ipotesi non verificata**.

⚠️ **Il rilievo «nessun admin fra gli staffisti conosciuti» ora suona molto più raramente**, ed è voluto: con
un jolly, per non avere nessun admin serve che i codici siano *malformati* o di un'altra divisione — cioè il
guasto vero, non una lista incompleta.

⚠️ **Trappola di configurazione trovata qui:** dalle liste della sezione `Division` si può solo **allargare**
l'insieme degli admin, mai restringerlo — il binder *aggiunge* ai default invece di sostituirli (era anche
la causa dei prefissi ICAO duplicati, ora deduplicati). Per restringere davvero si usa
`Auth:AdminStaffCodes`, che sostituisce tutto. Su un permesso di questo peso, la differenza conta.

### E5 Copertura e rifiniture — due voci chiuse il 9 agosto 2026
- ✅ **«Scarta bozza»** — fatto. Elimina la versione `Draft` col suo contenuto, scrive l'audit
  (`AuditAction.Discard`, nuovo) e libera il lock. La cancellazione di una versione esisteva già dentro la
  potatura: **estratta e riusata**, non ricopiata. Due regole nel servizio: si scarta solo una bozza, e solo
  se c'è una versione a cui tornare (su un documento mai pubblicato la bozza *è* il documento). Verificato
  sull'app: bozze 11 → 10, audit con documento e numero di versione, zero sezioni o blocchi orfani.
- ✅ ~~Viewer dell'**audit log**~~ — **esisteva già** (`AuditPage`, rotta `/services/vsop/admin/audit`): voce stantia.
  ⚠️ Resta la domanda aperta di prodotto: pubblicare una **release** non scrive audit (lo fa solo la
  promozione di una bozza), quindi il viewer non mostra quelle pubblicazioni.
- ✅ **Test property-based sull'AoR** — **fatti il 23 agosto 2026** (`AorProiezioneProperties`, CsCheck 4.8,
  pacchetto senza dipendenze). Sei proprietà sulla proiezione: nessun punto fuori dal riquadro, lato lungo
  sempre 400 (cioè scala uniforme), invarianza alla traslazione in longitudine, rapporto fra i lati uguale a
  quello dell'estensione proiettata, `ProjectShared` di un poligono solo uguale a `Project`, e meno di tre
  punti ⇒ `null`.
  ⚠️ **Scrivendoli è uscito un difetto di documentazione**: il commento di `AorPolygonProjector` diceva coppie
  `[lat,lon]`, mentre il formato IVAO `regionMapPolygon` mette la **longitudine prima** (lo fa
  `ParsePoints`, e i test esistenti lo sapevano). Chi ne avesse ricavato una fixture avrebbe scritto un
  poligono **ruotato di 90°**, e la proiezione non se ne lamenta: disegna. Commento corretto.
  ⚠️ **Sono test non deterministici per costruzione**: i casi cambiano a ogni giro, quindi un rosso può
  comparire su un codice fermo. Non si rilancia finché passa — è un controesempio nuovo: il seed sta nel
  messaggio, si riproduce con `-e CsCheck_Seed=…` e si congela in un test a esempio.
- 🟡 **Editor visuale delle mappe AoR** — è una feature di interazione, non una rifinitura: va disegnata
  con chi la userà prima di essere scritta.

### E6 ✅ Trasferimenti ACC↔APP — chiuso l'11-12 agosto 2026, **in `main` dal 15 agosto** (B6)
Il modello descriveva **un evento con un livello**: basta per un accordo ACC↔ACC, non per un ACC→APP —
«autorizzo a FL160 via CHI, trasferisco al confine dell'AoR passando FL110» non era esprimibile. Sei giri
sullo stesso ramo, ognuno con la propria scheda in `docs/feature/`: due eventi separati con velocità e punto
di trasferimento propri; il gruppo di varianti diventato un **outline** (alternative pari grado, eccezioni
annidate); la pagina rifatta col pattern del progetto, poi a **tre colonne** con vista a elenco, stato in URL
e scrittura dentro la tabella; infine le rifiniture d'uso (costo per gesto da 8 query a 1, tastiera nei
picker, annulla che rimette l'outline, modifica in blocco).

⚠️ **Resta da fare dai colleghi, non dal codice:** le **15 righe** con ricevente APP che non dicono ancora
dove avviene il trasferimento vanno riviste **a mano** — il loro livello può voler dire «autorizzato» o «al
trasferimento», e solo chi le ha scritte lo sa. Le elenca il **cruscotto delle lacune** in
`/services/vsop/admin/transfers` (genere «da rivedere»). ⚠️ Il numero va **rimisurato sulla produzione MariaDB**: 15
è il conteggio sul DB di sviluppo.

### E6-bis 🟡 Accordi di coordinamento — un accordo per COPPIA, il traffico nelle sezioni (in `main` dal 18 ago 2026)
Carte, in ordine di vigore:
[`feature/2026-08-18-accordi-a-sezioni.md`](feature/2026-08-18-accordi-a-sezioni.md) **(il modello di adesso)** ·
[`feature/2026-08-16-accordi-di-coordinamento.md`](feature/2026-08-16-accordi-di-coordinamento.md) ·
[`feature/2026-08-17-editor-accordi-per-relazione.md`](feature/2026-08-17-editor-accordi-per-relazione.md).
Schema `spec/modello-dati.md` **§9.25-bis**; area `refactor/07-trasferimenti.md` **§11**.
**Per riprendere da freddo**: [`history/handoff-accordi-coordinamento.md`](history/handoff-accordi-coordinamento.md).

Tre giri, e ognuno ha tolto un asse del modello precedente. **Ferragosto**: `TransferFlow`+`TransferPoint`
lasciano il posto all'**accordo** fra due parti (droppate il 17 con `DropLegacyTransferTables`; l'ultima copia di
quei dati nella forma originale è `tests/Vipi.Application.Tests/Fixtures/real-flows.tsv`). **17 agosto**:
l'editor, che aveva ancora l'albero sul lato B e il verso come interruttore. **18 agosto**: l'accordo diventa la
**relazione fra due enti** — uno solo per coppia, un ente per lato — e il traffico scende nelle **sezioni**.

Le misure che hanno deciso il terzo giro, sul `vipi.db` vero: **40 accordi stavano in 16 coppie** (la sola
`LGGG ⇄ LIBB` ne teneva otto); il **verso** si esprimeva *orientando* l'accordo — 60 clausole su 60 `AtoB` —
quindi i due sensi finivano in accordi diversi; e **nessun accordo aveva più di un ente per lato**.

Conversione in tre passi — migrazione additiva → `tools/Vipi.AgreementsToSections` → migrazione distruttiva:
**40 accordi / 60 clausole → 16 accordi / 38 sezioni / 60 clausole**, con `real-coordination.approved.txt`
**invariato carattere per carattere**.

**Cosa resta, ed è il motivo per cui la voce è 🟡:**
1. ✅ **Verifica live fatta** (porta 5035, copia del DB convertita). Ha confermato albero a due livelli, ordine
   imposto, verso proposto dall'aeroporto, blocco fantasma del reciproco, gemelle e deep-link — e ha trovato
   **tre difetti invisibili ai test**, corretti: l'avviso «scalo non coperto» che urlava su 3 sezioni su 8, lo
   stesso avviso che dalla testata mandava i tasti a capo, e cinque etichette rimaste sull'operazione vecchia.
2. ✅ **Conversione eseguita sul `vipi.db` di sviluppo** (18 agosto): 40 accordi / 60 clausole →
   **16 accordi / 38 sezioni / 60 clausole**, `integrity_check` ok, zero orfani, zero violazioni di FK, e
   l'app ci gira sopra (vIPI ACC LIBB: 37 tabelle, 76 righe di coordinamento).
   ⚠️ **Da qui in poi quel DB vuole questo ramo**: il codice di `main` cerca ancora `AgreementParties`. Il
   backup sta **fuori dal repo** in `../vipi.db.bak-pre-sezioni-20260818`, ed è l'unica copia dello stato
   precedente perché il `vipi.db` non è tracciato in git.
   ⚠️ Resta da fare sulla **MariaDB di produzione**, con `--mysql` e le migrazioni gemelle.
3. ✅ **Suite completa 2569 verdi** (E2E inclusi) e `dotnet build -c Release --no-incremental` a **0 warning**
   su due TFM.
4. **Le due asimmetrie** — `LGGG ⇄ LIBB` (BELIX, OLGAT) e `LDZO ⇄ LIBB` (sei punti da un lato solo) — le
   decidono i colleghi. Adesso stanno nello **stesso accordo**, una sezione sotto l'altra, quindi si vedono.
5. ✅ **I tre reciproci separati** (`#13/#32`, `#17/#28`, `#23/#38`) e la **relazione spezzata** (`#26/#27`) si
   sono chiusi da soli: i due versi della stessa coppia **sono** lo stesso accordo, e le gemelle le ha unite la
   conversione. Anche i **due accordi senza ricevente** sono spariti — il lato è ora una colonna `NOT NULL`.
6. ✅ **I tre difetti di `LevelFormatting` sono chiusi** (18 agosto): `— (dispari)` diventa `dispari` (21
   clausole su 60 lo mostravano — non era un caso limite), la parità non si appende più a un livello *speciale*
   che la dice già a parole, e la colonna del documento prende le parole dal **template** come già facevano
   handoff e velocità — così una vLOA inglese non scrive più «FL260 (pari)». L'approvato è stato riapprovato
   **dopo aver letto le nove famiglie di differenza**: 82 righe, nessuna aggiunta o tolta, nessuna frase
   toccata. ⚠️ Le release **già pubblicate** conservano il testo vecchio: uno snapshot è una fotografia, e il
   testo nuovo compare alla prossima release.
7. ✅ **`InlineConfirm` localizzato** (18 agosto): i default di prompt, conferma e annulla passano dal
   localizer. Erano cablati in italiano e su 14 usi solo 3 passavano le proprie etichette — gli altri 11
   dicevano «Sì, elimina» anche in pagina inglese, e anche per azioni che non eliminano.
8. ✅ **Plurali dei conteggi** (18 agosto): «1 clause» invece di «1 clauses», in entrambe le lingue e in quattro
   punti. Un conteggio è la cosa che si legge più spesso nella pagina.
8. ✅ **Merge in `main` fatto** il 18 agosto (`06798a9`), autorizzato dal committente; main verificato dopo il
   merge (build Release 0 warning su due TFM, 2569 test verdi).
9. ✅ **Il blocco al deploy è sciolto — aggirandolo, non risolvendolo** (23 agosto). Resta vero che le
   migrazioni girano all'avvio e che `AgreementSectionsFinalize` fallisce su un archivio non convertito; la
   consegna del 23 agosto (**A11**) però **sostituisce** il database invece di migrarlo, e un `.sql` che
   porta con sé la storia delle migrazioni non lascia niente da applicare. La conversione in posto — backup
   → additiva → `tools/Vipi.AgreementsToSections --mysql` → finale — resta scritta in
   [`history/handoff-accordi-coordinamento.md`](history/handoff-accordi-coordinamento.md) e **torna
   necessaria** il giorno in cui l'archivio di produzione conterrà qualcosa che non si può ributtare via.

⚠️ **Due difetti trovati eseguendo, e che nessun test vedeva** — valgono fuori da quest'area: fra le due
migrazioni lo schema è **misto**, e cancellare un guscio si portava via clausole già riappese correttamente (60
→ 23, in silenzio); e lo scaffolding EF ha proposto, per la seconda volta su quest'area, un `RenameColumn` che
avrebbe prodotto dati **validi e sbagliati** (`AgreementId` spacciato per `SectionId`). Le migrazioni si
leggono, non si accettano.

### E6-ter ✅ CHIUSA il 23 agosto 2026 — non era un test ballerino, era un **difetto del client**
Carta: [`feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](feature/2026-08-23-quattro-difetti-e-le-proprieta.md) §4.
Inseguita dall'11 al 22 agosto come un problema di tempi — «il thread-pool sotto carico», «la prova dipende
dai tempi del socket» — e per due volte la cura proposta è stata allargare l'attesa. Non era quello.

`AuroraClient.SendAsync` **si connetteva prima di prendere il turno**. Due invii lanciati insieme trovavano
entrambi «non connesso» — l'assegnazione avviene dopo `ConnectAsync`, che cede il controllo — e aprivano
**un socket a testa**; il secondo chiudeva il primo. E peggio: `stream` e canale delle righe si leggevano in
**due istruzioni separate**, quindi un invio poteva scrivere su un socket e aspettare la risposta sul canale
dell'altro. Nessuna delle due arrivava a destinazione: **silenzio fino alla scadenza**, cioè esattamente
«Nessuna risposta a #TRPOS entro 15000 ms» — che sembrava lentezza e non lo era.

Adesso la connessione è **un oggetto solo** (socket + flusso + canale + ciclo di lettura), letta in un colpo,
e si apre **dentro** il turno. Visto fallire e visto passare: col client di prima **200 giri su 200**
aprivano due connessioni, e la prova ci metteva 3 minuti e 10; col client nuovo, **133 ms**.

⚠️ **La lezione, che vale oltre questo caso:** il test lo vede solo se i due invii partono *davvero* insieme
(due thread e un cancelletto che li rilascia). Chiamati in sequenza sullo stesso thread, su loopback la prima
connessione fa in tempo e **il difetto non si vede** — ed è per questo che per undici giorni è sembrato un
problema di tempi. Un rosso a intermittenza merita di essere letto nel codice prima che nel calendario.

---

### E7 ✅ Login — **chiusa il 24 agosto 2026**: `OnRemoteFailure` c'è, e il guasto ora si vede

**Cosa è stato fatto.** `oidc.Events.OnRemoteFailure` è registrato in `VipiStandaloneAuthExtensions`:
logga la ragione sotto la categoria fissa **`Vipi.Auth.Ivao`** (motivo, errore del portale, se lo stato del
giro si è recuperato, se c'era già una sessione, dove si stava andando — mai il `code` né i token), poi
decide invece di rilanciare. Due esiti: **sessione già attiva ⇒ si torna al `returnUrl`** (è il caso del
23 agosto, dove l'utente vedeva `Error.` ed era dentro); **nessuna sessione ⇒
`/services/vsop/auth/accesso-non-riuscito`**, una pagina che dice cosa è successo e offre un «riprova».

⚠️ **La pagina non rimanda da sola al login, ed è una scelta.** Se il guasto è stabile — il portale che
risponde `access_denied` — il rimbalzo automatico diventa un anello infinito, perché IVAO ha già la sessione
aperta e rispedisce indietro subito. Il secondo tentativo lo chiede l'utente, con un clic.

⚠️ **`context.HttpContext.User` è vuoto dentro `OnRemoteFailure`**, e crederci sarebbe stato un errore
silenzioso: il gestore del callback gira dentro `UseAuthentication` **prima** che il middleware monti
l'utente dello schema di default. La sessione esistente va chiesta a mano con
`AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)`.

**Il motivo è un insieme chiuso** — `portale`, `correlazione`, `nonce`, `sconosciuto` — perché serve a due
padroni: finisce nel log **e** sceglie la frase in pagina. Niente testo di provenienza esterna arriva allo
schermo; il `returnUrl` passa comunque da `SafeReturn` ed è codificato per attributo.

ℹ️ **`Unable to unprotect the message.State.` sta in `correlazione`, e vale la pena saperlo**: è il messaggio
misurato sul flusso vero, ed è anche il sintomo di un **key-ring perso** (`public_atc/vipi-keys`, una delle
tre cose che un FTP distratto cancella). Se *ogni* login esce con quel motivo, si guarda lì prima che ai
browser degli utenti.

**Verificato**: 20 test nuovi (`tests/Vipi.E2E.Tests/LoginFailureTests.cs`), e soprattutto un guasto **vero**
provocato in locale — `GET /signin-oidc?state=abc&code=def` senza cookie di stato — che prima finiva su
`/Error` e ora esce **302** verso la pagina, lasciandosi dietro la riga di log che il 23 agosto non c'era.
Suite intera: **3621 verdi**, 0 avvisi.

<details><summary>Il testo originale della voce, com'era prima della chiusura</summary>

### E7 🟢 Login: manca `OnRemoteFailure`, e il cookie della build vecchia fa fallire il primo accesso
Trovato in produzione la sera del 23 agosto, segnalato dal committente: dopo il login compare la pagina
`Error.` generica, ma **al refresh risulta loggato**.

**Cosa succede davvero.** L'URL della pagina d'errore è il **callback** (`/signin-oidc?state=…&code=…`),
quindi su quella richiesta l'accesso **non si è completato**. Sembrava riuscito perché il cookie `vipi.auth`
è **persistente, 7 giorni, sliding**: era già loggato da prima.

**La causa, e si consuma da sé.** In **incognito il login fila liscio** — quindi i login nuovi non sono
rotti, e la differenza fra le due finestre è solo il cookie. La build in produzione fino a quella sera
(15 agosto) usava `oidc.ClaimActions.MapAll()`: nel cookie finiva l'**intero profilo IVAO** — `hours[]`,
`rating{}`, `groups`, `userStaffDetails`, `userStaffPositions` (~1,5 kB) — cioè un cookie che ASP.NET spezza
in più pezzi (`vipi.authC1`, `C2`, …). La build del 23 ne mappa **sei campi**. Chi era loggato da prima si
porta dietro il cookie grasso, e il callback fallisce; chi entra per la prima volta no. **Rimedio per
l'utente: uscire e rientrare** (o cancellare i cookie del sito). Colpisce solo chi era loggato prima del
23 agosto, e sparisce da sé al primo logout o alla scadenza dei 7 giorni.

**Il difetto da chiudere, che è un altro.** In tutta la storia del repo **non è mai esistito un
`OnRemoteFailure`** (`git log -S` su tutti i commit: zero). Qualunque guasto dentro il flusso IVAO —
correlazione fallita, cookie del `nonce` mancante, errore restituito dal portale — esce come **eccezione non
gestita** e finisce su `UseExceptionHandler("/Error")`: una pagina che non dice niente all'utente e non
lascia niente a noi. Il costo è stato pagato: la causa è stata ricostruita **dagli `scope` dentro il `code`
OIDC**, non da un log.

**Cosa fare:** registrare `OnRemoteFailure` — logga la ragione, e se l'utente è già autenticato reindirizza
al `returnUrl` invece di lanciare; altrimenti rimanda al login con un messaggio leggibile. ⚠️ Tocca
`Vipi.Host.dll`, quindi vuole un giro di ripacchettamento: da fare **fuori** da un deploy a metà.

ℹ️ Sospettato secondario, non escluso: il `nonce` è stato **acceso il 22 agosto** e la sera del 23 è la
**prima volta che gira in produzione**. Se il cookie del nonce manca al ritorno, `base.ValidateNonce` lancia
— e lancia esattamente così. Il `OnRemoteFailure` è anche ciò che renderebbe distinguibili i due casi,
perché oggi la ragione non la vede nessuno.

</details>

### E8 ✅ «Error.» a chi entra per la prima volta — chiusa il 24 agosto 2026, ramo `login-utente-nuovo`

**Segnalato dal committente** il 24 agosto: un socio **senza incarichi** vede la pagina `Error.` su
`/services` — l'elenco degli strumenti, che non legge una riga di database — mentre lo stesso indirizzo,
**da non collegato, risponde 200** (verificato con curl sulla produzione, mentre la segnalazione arrivava).

**La deduzione, che vale più della causa.** Su quella pagina l'unica cosa che un utente **loggato** fa in
più di un anonimo era, in `SopLayout`:

```csharp
_canEdit = _user is not null && (_isAdmin || (await Editing.ListEditableDocumentsAsync()).Count > 0);
```

L'anonimo esce dalla condizione **prima** del database; l'admin esce su `IsAdmin`, che guarda codici staff
in memoria; **paga il giro solo il socio qualunque**. E il giro era **N+1**: tutti i documenti, più due
query di autorizzazione per ognuno, a **ogni** pagina, per accendere un tasto. Conseguenza: qualunque
intoppo del database — connessione caduta, pool esaurito, comando scaduto, riavvio a freddo di Passenger —
non spegneva un tasto, **buttava giù tutta la pagina, per i soli utenti collegati**, mentre ogni sonda
anonima continuava a dire che il sito era su.

**Chiuso così:**
- `IEditAuthorizationService.CanEditAnythingAsync` — **una query sola** (admin, oppure almeno una
  concessione). `ListEditableDocumentsAsync` non aveva altri chiamanti ed è stato rimosso.
  ⚠️ La semantica si sposta di un filo: chi ha una concessione su una ACC **senza documenti** ora vede il
  tasto e trova un elenco vuoto. È scritto nel contratto.
- Se quella domanda **fallisce**, il tasto resta spento e la pagina esce. È l'eccezione motivata alla regola
  «non si ingoiano gli errori»: il **contorno non decide se la pagina esiste**. Non è muta — va nel log.
- `PaginaErrore` prende il posto della `Error.razor` del modello: HTML scritto a mano, come
  `IvaoLoginFailurePage` e per lo stesso motivo — deve reggere anche quando a lanciare è stato il **layout
  condiviso**, che è esattamente il caso di oggi.
- `DiagnosticaErrori` scrive ogni richiesta fallita in `diagnostica/errori-richieste.txt` con **lo stesso
  codice mostrato in pagina**, il percorso vero, il **VID** e lo stack trace: dalla fotografia che arriva
  su WhatsApp si risale all'eccezione. Niente query string (su `/signin-oidc` è una credenziale), niente
  cookie, niente intestazioni. Lo stato resta **500**.

⚠️ **Tre trappole trovate scrivendolo:**
1. Dentro un `IExceptionHandler`, `ctx.Request.Path` vale **già** `/Error`: il middleware riscrive il
   percorso prima di chiamare i gestori. Il percorso vero sta in `IExceptionHandlerPathFeature.Path`, e
   senza quello il registro direbbe sempre «/Error» — inutile, e in modo silenzioso.
2. Il **VID** nel registro è ciò che rende leggibile un guasto «che si vede solo da loggati». Si legge dai
   claim e non da `ICurrentUserProvider`: dentro la gestione di un guasto, risolvere un servizio è un modo
   in più di fallire.
3. I test stanno in ambiente **Staging**: `UseExceptionHandler` è montato solo fuori da Development.

> ### ⚠️ CORREZIONE del 24 agosto sera — la causa era un'altra, e l'ha detta il registro
>
> Poche ore dopo il deploy il difetto è **ricapitato**, e questa volta ha lasciato le sue righe:
> **92 richieste fallite fra le 16:55 e le 17:07**, tutte con lo stesso stack.
>
> ```
> System.InvalidOperationException: A second operation was started on this context instance
>    at Vipi.Infrastructure.Persistence.EfStationDirectory.ListAccs()
>    at Vipi.Application.Content.StationResolver.get_Accs()
>    at Vipi.Ui.Shared.SopLayout.BuildRenderTree()
> ```
>
> **Il colpevole è il CATALOGO, non `_canEdit`.** `Stations.Accs` è a caricamento pigro e il markup lo
> leggeva **dentro** `BuildRenderTree`; Blazor disegna l'albero mentre `OnParametersSetAsync` è ancora in
> volo, quindi quella query partiva sullo stesso `DbContext` su cui era già in corso quella di `_canEdit`.
> `_canEdit` **non fallisce**: apre la finestra. La guardia messa al mattino non poteva prenderlo, ed è
> esattamente per questo che è ricapitato.
>
> ⚠️ **Non era «a intermittenza»: era sistematica per una classe di utenti**, e la prima stesura di questa
> voce sbagliava a dire «solo a freddo, dopo ogni riavvio». Il catalogo è **Scoped**, quindi per una pagina
> SSR la sua cache è fredda a **ogni** richiesta: non è lì la variabile. La variabile è se c'è un `await`
> **in volo** quando parte il render — e `ComponentBase` il render lo fa comunque, subito dopo
> `OnParametersSetAsync`, aspettando il task solo se non è già completato.
>
> | Chi | `_canEdit` | Task | Esito |
> |---|---|---|---|
> | anonimo | non chiesto | — | nessuna corsa |
> | **admin** | risposto dai claim (`IsAdmin`) | **già completato** | nessuna corsa |
> | **socio senza incarichi** | query vera sul database | **in volo** | **corsa, ogni volta** |
>
> **I numeri lo confermano**: delle 92 righe, le **78 della corsa vengono tutte dallo stesso VID
> non-admin**; l'admin che navigava nella stessa finestra ne ha prodotte **zero** (le sue 4 sono della
> Fase 2, i timeout). Ecco perché il difetto lo vedeva un socio e a noi non capitava mai — e perché
> «riprova, a me funziona» non l'avrebbe mai smentito.
>
> **Chiuso** chiamando `Prewarm()` prima di qualunque `await` e facendo leggere al markup un campo: il
> render non tocca più il database. `Prewarm()` era già scritto per questo — «chiamata dal ciclo di vita
> async, context libero e sequenziale» — e non lo chiamava nessuno.
>
> ℹ️ **La seconda metà di quella finestra è un'altra cosa**: dalle 16:59 alle 17:07 le 11 righe rimaste sono
> `RetryLimitExceededException`/Timeout, e colpiscono **anche gli anonimi**. Lì il database non rispondeva
> più. Le due fasi sono consecutive e la prima è una causa plausibile della seconda — 78 richieste fallite
> in due minuti e mezzo, ognuna con una query abbandonata a metà — ma **plausibile non è misurato**, e senza
> i log del server MariaDB resta un'ipotesi.
>
> ℹ️ Trovato scrivendo i test: `StaffLoginTrackingMiddleware` proteggeva solo la scrittura, non
> `users.Get()`. Un guasto lì usciva, il gestore rieseguiva `/Error`, il middleware girava **di nuovo**
> sulla richiesta rieseguita e lanciava una seconda volta: non usciva nemmeno la pagina d'errore. Un pezzo
> che gira prima del routing gira anche sulla via di fuga. Ora è protetto per intero.

**Non era provato che fosse questa la causa di quel preciso 500** — e infatti non lo era. — sul server non c'era una riga da leggere,
ed è il motivo per cui la seconda metà della voce esiste. È provato il **meccanismo**: era l'unica strada
per cui una pagina senza dati potesse morire per un utente collegato e non per un anonimo. Contesto della
giornata: il pacchetto «e» era stato caricato quel pomeriggio, e Passenger riavvia il processo per
inattività — i **riavvii a freddo sono frequenti**, ed è la finestra in cui un intoppo del database è più
probabile.

**Chiesto dal committente mentre si preparava il pacchetto, e sta bene qui**: la **versione in barra**, ai
soli admin. La domanda era «che versione del sito è online?», e non aveva risposta — `AssemblyVersion` è
`1.0.0` in ogni pacchetto, e la data in `avvio-diagnostica.txt` dice quando è *ripartito*, non *che cosa*:
per giunta si rinfresca da sé, perché Passenger riavvia il processo per inattività. Ora la build si timbra
col **commit** (non con l'ora di compilazione: ricompilare lo stesso codice deve dare la stessa versione) e
con la **lettera del pacchetto**, passata al publish come `-p:VipiPacchetto=g`. In barra `g · e8fc4a2`, il
resto nel `title`; la stessa riga apre `avvio-diagnostica.txt`.

> ✅ **Superato il 30 agosto 2026: la lettera è diventata un numero.** Le lettere erano arrivate a **`j`**, e
> un nome che non promette niente non mente mai — ma non dice nemmeno se un pacchetto si può caricare da
> solo. Ora è `VipiVersione` in `Directory.Build.props`, **con la regola scritta accanto**:
>
> | | Quando |
> |---|---|
> | **PATCH** `1.0.→1` | solo correzioni: nessuna migrazione, nessuna pagina o sezione nuova |
> | **MINOR** `1.→1.0` | funzionalità nuove, e/o migrazioni **additive** |
> | **MAJOR** `→2.0.0` | il pacchetto **non si consegna da solo**: serve una sostituzione del database, o il codice nuovo non sa leggere l'archivio in produzione |
>
> ⚠️ Il maggiore non misura l'importanza: risponde a «basta l'FTP, o serve anche il database?» — l'unica
> domanda che qui costa una consegna coordinata con Ivao.It, e che il 23 agosto è costata una serata (A11).
>
> ⚠️ **Il numero sta in un file, non sulla riga di comando.** La lettera si passava al publish e non viveva
> da nessuna parte: nessuno poteva vederla cambiare in un diff, né sapere da quale codice venisse la «g».
> Una promessa si rivede insieme a ciò che la giustifica.
>
> ⚠️ **Il commit resta**, e la barra dice `1.0.0 · <commit>`. Il numero è il nome che diamo noi; il commit è
> l'unica cosa che identifica il codice. Un test lo presidia (`Il_numero_non_sostituisce_il_commit_in_barra`),
> perché toglierlo per accorciare l'etichetta riporterebbe esattamente al 24 agosto.
>
> ℹ️ La forma è verificata **sul binario compilato**, non sul file di build: è l'unico modo di sapere che il
> numero è arrivato fin dove il sito lo legge. Provato che morde — con `v1.0` il test è rosso.

⚠️ Tre scelte, tutte a difesa di qualcosa: **solo admin** (a un socio non dice niente, a chi passa dice con
quale build sta parlando); **prima cosa a uscire** dalla barra quando lo spazio manca, che è già a corto;
e senza timbro si scrive **«sviluppo»** invece di inventare un numero — a una versione si crede.
⚠️ Niente marcatore «albero sporco»: `git status` in forma breve elenca anche i file che differiscono per i
soli fine riga, e un allarme che suona sempre non è un allarme.

**Verificato**: `BarraNonAffondaLaPaginaTests` (7), `PaginaErroreTests` (2), `VersioneBuildTests` (6) e due
sull'HTML servito (l'admin vede la targhetta, il socio no); senza la guardia i due casi del guasto tornano
500, cioè lo screenshot. Propagata `CanEditAnythingAsync` ai 12 finti
`IEditAuthorizationService` dei test. Suite intera verde su entrambi i TFM.

### E9 ✅ CHIUSA lato codice il 27 agosto 2026 — la corsa si riproduce, e la prima operazione ha un nome

**Misurato in produzione alle 17:44 del 24 agosto 2026**, con il pacchetto «h» già online (quindi con la
correzione del catalogo dentro): il registro, che il committente aveva appena azzerato, si è riempito di
nuovo. **Sette richieste**, tutte dello stesso VID **non-admin**, tutte «A second operation was started on
this context instance», ma questa volta **dentro le pagine**:

| Percorso | Dove muore |
|---|---|
| `/services/vsop/libb`, `/lirr` (×3) | `AccLanding.OnParametersSetAsync` → `EditAuthorizationService.CanEditAccAsync` |
| `/services/vsop/libb/airports` (×2) | `AeroportoPage.OnParametersSetAsync` → `AirportPresidencyService.ResolveAsync` |
| `/services/vsop/limm/airports` (×2) | `AeroportoPage.OnParametersSetAsync` → `EfStructureEditingRepository.LoadAsync` |

**Quello che si sa**: la seconda operazione è quella della pagina. **Quello che NON si sa**: quale sia la
prima, cioè chi tenesse occupato il contesto in quel momento. Lo stack dell'eccezione mostra solo chi è
morto, non chi stava già correndo.

⚠️ **L'ipotesi naturale — il layout che lascia `_canEdit` in volo mentre il `@Body` viene disegnato — NON è
stata riprodotta.** Tentativi fatti, tutti falliti nel senso che il test resta verde anche col difetto
dentro:

1. intercettore EF che rallenta ogni comando ⇒ **non scatta**: gli `IInterceptor` registrati in DI non
   arrivano al contesto in questo assetto (da capire perché);
2. `HasAnyGrantAsync` sostituita con una query **lenta davvero** (ricorsiva SQLite da tre milioni di giri)
   sullo stesso `DbContext` ⇒ la pagina non si è mai sovrapposta;
3. ⚠️ e il primo tentativo era sbagliato in modo istruttivo: avevo sostituito **tutto** il repository, così
   anche la query della pagina diventava finta e non toccava il contesto. Un test che non può fallire.

Quindi: o in SSR il render del corpo aspetta davvero il task del layout (e allora l'altra operazione è
qualcun altro), o la finestra si apre solo sotto condizioni che il locale non riproduce.

**Cosa è stato fatto comunque** (`SopLayout.SetParametersAsync`): il layout conclude il proprio lavoro
asincrono **prima** di far disegnare l'albero, così non può essere lui la prima operazione. È corretto in
sé e toglie una fonte possibile — **ma non è provato che sia LA causa**, e va scritto così finché non lo è.

**Fatto entrambi, pacchetto «i»:**

1. **Scope proprio** (`OwningComponentBase`) per `AccLanding` e `AeroportoPage`, le due che compaiono nel
   registro — il rimedio già adottato da sei componenti dopo l'audit del 30 luglio. Chiude la **classe** del
   guasto senza dipendere da chi sia l'altra operazione, che è il punto: quello non si è capito.
   ⚠️ `IStationResolver` NON si sposta: il layout l'ha già scaldato, e riprenderlo dallo scope nuovo
   vorrebbe dire ripagare la stessa query a freddo.
2. **Il registro dirà chi c'era già**: un intercettore EF annota inizio e fine di ogni comando col
   chiamante, e all'istante del lancio (`FirstChanceException`) si fotografa che cosa è aperto.

⚠️ **Tre cose imparate costruendo il punto 2, tutte contro-intuitive:**
- il **rilevatore di concorrenza di EF scatta prima dell'esecuzione**, quindi la seconda query non arriva
  mai all'intercettore: cercare lì la collisione non avrebbe mai visto niente;
- **aspettare il gestore d'errore è tardi**: mentre l'eccezione risale, la prima operazione fa in tempo a
  chiudersi e la lista torna vuota. `FirstChanceException` è l'unico istante in cui la scena è intatta;
- il rilevatore **non copre i comandi grezzi** (`ExecuteSqlRawAsync`): il modo ovvio di provocare una corsa
  in un test non fa scattare niente, ed è per questo che il test costruisce la scena a mano.

**Resta da chiudere la voce**: serve un socio senza incarichi che apra `/services/vsop/{acc}` e la pagina di
un aeroporto dopo il caricamento di «i». ⚠️ Da admin non prova niente.

### E10 ✅ FATTA il 29 agosto 2026 (ramo `biblioteca-allegati`, **NON fuso**) — Biblioteca allegati: i PDF su Drive, linkati dai documenti

Chiesto dal committente il **25 agosto 2026**, carta `docs/feature/2026-08-25-biblioteca-allegati.md`.
**Tutte e nove le slice fatte il 29 agosto**, un commit per slice, build Release verde su entrambi i TFM a
ogni passo e tutti i progetti di test verdi alla fine — **E2E compresi**.

⚠️ **Il ramo è spinto e NON fuso**: la fusione è una decisione del committente.

⚠️ **Resta il rischio R8, e nessun test può darlo**: l'**embed di Drive non è mai stato provato dal vivo**.
Google può togliere l'incorporamento della preview quando vuole — è già successo col fondo mappa CARTO il 27
agosto. Serve il sito acceso **e un PDF vero sul Drive di divisione**. A cadere sarebbe il solo modo
*Incorporato*, che ha comunque il link sotto come ripiego.

#### Il tipo «PIV» — 1 settembre 2026

Chiesto dal committente: «si può aggiungere il tipo PIV? O si deve toccare il DB?».

**Non si tocca il database, ed è per costruzione.** Gli enum di questo modello si scrivono in colonna come
**stringhe** — `VipiDbContext` mette `SetProviderClrType(typeof(string))` su ogni proprietà di tipo enum —
quindi un valore nuovo è una riga nuova che scrive `"Piv"`. Verificato su tutt'e due i provider: su SQLite
la colonna è `TEXT`, su MySQL `varchar(32)` **senza vincolo di dominio** (nessun `CHECK`, nessun tipo
`ENUM` di MySQL). Niente migrazione, niente numeri da riallineare — il che conta doppio dentro la finestra
cieca.

⚠️ **E per la stessa ragione l'ORDINE di `AttachmentKind` si può cambiare** senza toccare quel che è già in
archivio: decide solo come compaiono i chip a schermo. `Piv` sta prima di `Other`, che resta ultimo perché
è il caso di raccolta.

⚠️ Il chip e la voce di tendina **non** sono stati aggiunti a mano: la pagina cicla `Enum.GetValues`, quindi
compaiono da soli. Quel che serviva era la **riga nelle risorse** (`AttKind_Piv`, IT ed EN) — e senza,
`SharedResourceIntegrityTests` fallisce prima che qualcuno veda il nome della chiave a schermo. È il
presidio che esiste apposta.

⚠️ **Trovato e corretto solo nel commento**: `Other` diceva di essere «lo zero dell'enum, quindi una voce
nasce così finché non la si classifica». **Non è vero**: `Other` è dichiarato per ultimo, lo zero è `Loa`, e
una riga creata da codice senza scegliere il tipo nascerebbe «LoA». Oggi non capita — il modulo della
biblioteca un tipo lo impone sempre — quindi si è corretta la frase e **non** il comportamento: cambiare lo
zero è una decisione, non una pulizia.

**Verifica dal vivo** (Edge+CDP, copia del DB): il chip «PIV 0» compare fra «Manual» e «Other», la tendina
offre `Piv=PIV`, un allegato creato con quel tipo si salva («Added «PIV di prova»…»), il chip passa a
«PIV 1» e filtra — e in archivio la riga porta `Kind='Piv'`.

**Il vincolo che ha deciso il deposito.** I byte **non stanno da noi**: il piano di hosting **non ammette il
formato PDF** — ⚠️ vincolo **contrattuale**, quindi *non* si aggira mettendoli in MariaDB come blob, sarebbe
elusione — e IVAO HQ indica di tenere i documenti sul **Drive di divisione**. Il deposito è quindi Drive;
noi teniamo metadati, organizzazione, versioni e registro dei link.

⚠️ **Conseguenza**: il file Drive è «chiunque abbia il link», quindi **tutto ciò che entra in biblioteca è
pubblico**. Allegati riservati allo staff **non sono supportati** — un controllo di accesso davanti a un URL
Drive pubblico sarebbe teatro. Confermato dal committente che non servono.

**Le sette decisioni prese** (tutte approvate):

1. modello a **due livelli**: `Allegato`(slug stabile) → `AllegatoVersione` → file Drive. I documenti citano
   lo **slug**, mai il file: altrimenti sostituire un PDF vuol dire riaprire tutti i documenti che lo citano;
2. **l'identità del link è nostra**: `/vsop/files/{slug}` → 302 verso Drive. Cambiare deposito domani (o
   riportarli in casa, se l'hosting cambia) non tocca **un solo documento** — è una colonna in una tabella;
3. il registro «usato in N» si **ricava** dalle quattro fonti che `EfMediaMaintenance.ReferencedShasAsync`
   già scansiona, **mai** una tabella di join: quella si desincronizza e mente proprio quando serve;
4. il link segue **sempre la versione corrente**, release comprese. La regola di casa è già in
   `DocRelease.cs` (la release congela le *scelte editoriali*, non i cataloghi esterni), e congelare avrebbe
   un difetto pratico grave: una scansione sbagliata già pubblicata si correggerebbe solo **ripubblicando
   tutti** i documenti che la citano;
5. biblioteca a **due assi** (tipo × ambito) + ricerca, non cartelle: un albero a 50+ file si riempie di roba
   archiviata male;
6. **due** modi di linkare: blocco «Allegato» e `[testo](allegato:slug)` inline, con **un token solo** in
   entrambi → una sola regex per lo scanner;
7. v1 = caricamento **a mano** su Drive; l'API Drive (service account sul drive condiviso) è rimandata;
8. 🆕 **29 agosto**: il blocco rende in **due modi**, scelti blocco per blocco — **Link** (default, porta
   fuori dal sito) e **Incorporato** (`<iframe>` col visualizzatore Drive *dentro* la pagina, **più il link
   sotto** come ripiego). L'iframe punta alla **nostra** rotta `/vsop/files/{slug}`, quindi neanche lì l'ID
   Drive entra nei documenti. Altezza a **tre scaglioni**, non un numero libero;
9. 🆕 **29 agosto**: i campi si chiamano **`Provider` + `ExternalId`**, non `DriveFileId`. La decisione 2
   dice che il deposito è una colonna: un nome che dice «Drive» la richiuderebbe. Costa zero adesso e tiene
   aperte **Cloudflare R2** (c'è già Cloudflare davanti al sito) e GitHub senza migrazione.

**Le trappole trovate leggendo il codice:**

- ⚠️ **`MarkdownLite.cs` non ha link di NESSUN tipo.** Il link inline va aperto **solo** allo schema
  `allegato:`: il renderer fa HTML-encode e poi regex, quindi aprirlo a `[testo](url)` qualunque farebbe
  entrare `javascript:` e link esterni arbitrari nel contenuto editoriale.
- ⚠️ `/vsop/files/{slug}` **non può essere `immutable`** come `/vsop/media/{sha}`: sostituisci il PDF e il
  browser tiene il vecchio per un anno. Va `no-cache`. È ciò che renderebbe la sostituzione «non
  funzionante» in modo intermittente e inspiegabile.
- ⚠️ 🆕 **La CSP non ha `frame-src`** (`VipiStartup.cs:213`) ⇒ cade su `default-src 'self'`, che l'iframe
  Drive non passa. E siccome l'intestazione è **Report-Only**, oggi l'incorporato **funzionerebbe lo
  stesso**, con la violazione solo segnalata: il difetto resterebbe invisibile fino al passaggio a CSP vera,
  quando la resa incorporata morirebbe **in blocco**. La riga va aggiunta **nella stessa slice**. Stessa
  lezione delle tessere OpenTopoMap, mancanti per giorni per lo stesso motivo.
- ✅ 🆕 `X-Frame-Options: DENY` (`VipiStartup.cs:187`) **non è un ostacolo**: vieta che *le nostre* pagine
  stiano in un iframe altrui, non che noi ne incorporiamo uno.

**Debito annotato, non aperto**: i punti di dispatch su `BlockFormat` sono **9 file**, e questa è la
**seconda** feature che vi aggiunge un `case` (la prima furono le immagini). La regola del 2 del gate è
superata da un pezzo, ma aprire il registry dei formati dentro una feature sarebbe il refactor trasversale
che il gate vieta. **Alla terza volta si apre.**

**Verifiche fatte il 25 agosto, prima di sospendere:**

- ✅ **Il reconciler Postgres crea le tabelle**: R3 della carta era copiato dal doc immagini del 31 luglio ed
  **era già chiuso** dal commit `eac14fd`. `CreateTableStatements` genera la DDL dal diff del modello EF, con
  tre test in `PostgresSchemaReconcilerTests`. Le tabelle nuove nascono da sole. Riguarda comunque solo
  Render/Neon: SQLite e MariaDB vanno di migrazioni versionate.
- ✅ **Revisioni Drive**: le purgabili durano **~30 giorni**, o meno con 100 revisioni non marcate; fino a
  **200** si marcano «Keep Forever» e occupano quota. ⚠️ Ma **la revisione di testa non è mai purgata**: la
  versione *corrente* — l'unica che i documenti servono — è al sicuro senza spuntare niente. A scadere sono
  solo i byte delle versioni passate, già fuori perimetro (`AllegatoVersione` registra **chi, quando e
  perché**, non promette di riscaricare la v1).

**Risposto dal committente il 29 agosto 2026:**

- ✅ **Nessuna pulizia periodica** sul drive condiviso: non scade niente per policy. Restano solo le regole di
  piattaforma già misurate qui sopra, e la **revisione di testa non è mai purgata**.
- ✅ **Accesso alla cartella**: c'è, con l'**account IVAO del committente** (dominio `ivao.aero`). Basta per
  la v1, dove il caricamento è **a mano** e il sito non parla con Drive: tiene solo l'`ExternalId`.

⚠️ **Due cose che restano da non riscoprire:**

1. l'accesso è **di una persona, non del sito**. Il giorno del caricamento via API (fuori perimetro v1) non si
   usa quell'account: serve un **service account** membro del drive condiviso, o i caricamenti muoiono al
   primo cambio d'incarico;
2. ✅ **confermato il 29 agosto**: è una cartella dentro un **Drive condiviso**. I byte sono quindi
   dell'**organizzazione** e sopravvivono a qualunque cambio d'incarico; niente da spostare prima della
   slice 1. È anche la precondizione del caricamento via API, perché un service account si aggiunge come
   **membro** di un Drive condiviso e in un «Mio Drive» non si potrebbe.

**✅ Slice 1 fatta il 29 agosto 2026** (ramo `biblioteca-allegati`): entità `Attachment` e
`AttachmentVersion` — nomi **inglesi** come tutti i tipi di casa, mentre il blocco e il token restano
`allegato:` — più le migrazioni **×2** (`BibliotecaAllegati`, SQLite e MySQL). Additive: nessuna tabella
esistente toccata.

⚠️ **Una decisione della carta ribaltata scrivendo il modello**: l'id del file **non sta anche sulla voce**,
come diceva la carta del 25, ma **solo sulla versione**. Due posti che dicono la stessa cosa un giorno
dicono cose diverse, e quello sbagliato sarebbe proprio quello che serve il link. Conseguenza voluta: una
voce **nasce con la v1**, lo stato «voce senza file» non esiste.

⚠️ **Trappola pagata subito**: `IndexedStringLengthTests` pretende una voce **esplicita** in
`MySqlStringLengths.Map` per ogni colonna **indicizzata**, e la regola generale sugli enum non le basta — i
due assi `Kind` e `Scope` stanno nello stesso indice. Due righe aggiunte, stessa forma di
`DocumentImpact.Kind`.

**✅ Slice 2 fatta il 29 agosto 2026**: la pagina `/services/vsop/admin/attachments` (livello **Editor**,
voce nella barra admin), con l'elenco, i due assi come chip che contano, la ricerca e la creazione di una
voce. Sotto: `IAttachmentLibrary`/`EfAttachmentLibrary` e le regole pure di `AttachmentRules`.

- **Dal link si tiene l'ID, non l'URL.** `AttachmentRules.ExternalIdDa` legge tutte le forme che Drive
  produce davvero (`/file/d/<id>/view`, `?id=`, l'id nudo) e l'indirizzo lo ricostruisce **un posto solo**,
  nella forma `/preview` — che è anche l'unica che funzionerà dentro l'iframe della 5-bis.
- **Lo slug si propone dal titolo** e gli accenti si **traslitterano**: «Forlì» → `forli`, non `forl`.
  Ma appena qualcuno lo batte a mano, il titolo non lo tocca più: lo slug è definitivo.
- ⚠️ **I rifiuti restano cinque, distinti**: slug occupato e link illeggibile si correggono in due modi
  diversi, e un «non valido» solo manderebbe a indovinare.
- ⚠️ **La pagina linka ancora l'indirizzo di Drive** nel tasto «Apri»: dalla slice 3 punterà a
  `/vsop/files/{slug}`. Nei **documenti** un URL di Drive non entrerà mai.
- Guida: capitolo `admin-allegati` in `GuidaPage` **e** voce in `GuideSearchCatalog` (senza la seconda la
  ricerca globale non lo trova).

**✅ Slice 3 fatta il 29 agosto 2026**: la rotta `/vsop/files/{slug}` — 302 verso il deposito, **`no-cache`**,
404 per uno slug che non c'è. È l'identità del link: da qui in poi nessun link a un allegato porta l'indirizzo
di Drive, nemmeno il tasto «Apri» della pagina admin.

- **Sta sotto `/vsop` e non sotto `/services/vsop`** come le pagine, per la stessa ragione di `/vsop/media/`:
  è un **endpoint macchina**, un indirizzo che finirà dentro documenti già pubblicati e che quindi non si
  sposta più. Aggiunto `files` fra i `MachineFirstSegments` di `LegacyRoutes`, che ora lo rifiuta
  esplicitamente invece di redirigerlo su una pagina che non esiste.
- ⚠️ **`no-cache` e non `immutable`**, ed è la differenza con `/vsop/media/{sha}`: quello è
  content-addressed (immagine diversa = URL diverso), qui l'URL è **stabile** e il contenuto cambia sotto.
  Con una cache lunga si sostituirebbe il PDF e il browser terrebbe il vecchio per un anno — la sostituzione
  «non funziona» a intermittenza, perché a chi ha la pagina fresca funziona benissimo.
- **302 e non 301**: un permanente il browser lo tiene per sempre, e il giorno che cambia il deposito ci
  sarebbero utenti mandati a un indirizzo morto senza modo di correggerli.
- La rotta e il token (`allegato:`) stanno scritti in `AttachmentRules` e **da nessun'altra parte**.

⚠️ **Non ancora eseguiti i due test E2E** che provano 302, destinazione e `no-cache`
(`SmokeTests.Files_endpoint_*`): sono **scritti**, ma con l'host di sviluppo acceso `Vipi.E2E.Tests` non
compila — tiene i `.dll` — e sparisce dal riepilogo in silenzio. Vanno lanciati a sito spento.

**✅ Slice 4 fatta il 29 agosto 2026**: il registro «chi cita cosa». `AttachmentReferenceScanner` trova il
token `allegato:` in un testo, `EfAttachmentTextSource` legge i **quattro posti** in cui può comparire (blocchi
di tutte le versioni, sezioni extra, payload delle release, blocchi condivisi) e `AttachmentUsageService`
attribuisce ogni citazione al documento che la contiene. In pagina: colonna «Citato da» che si apre
sull'elenco, e chip **«mai usate»**.

- **Si RICAVA, non si mantiene.** Nessuna tabella di join: si desincronizza al primo percorso di scrittura
  che dimentica di aggiornarla, e mente proprio davanti alla conferma di una cancellazione.
- ⚠️ **Una release NON porta un `DocumentId`**: si identifica con la coppia *(tipo, chiave)*. Cercare solo
  per id lascerebbe senza nome e senza link **proprio le citazioni pubblicate**, cioè quelle che il lettore
  sta guardando adesso. Due indici, non uno.
- ⚠️ **Confini della regex in tutte e due le direzioni**: senza quello a destra `loa-lirr` «vincerebbe» dentro
  `loa-lirr-bis` e la guardia direbbe che è citata la voce sbagliata.
- ⚠️ **Gli escape JSON si neutralizzano prima di cercare**, come per le immagini: dentro il payload di una
  release il JSON di un blocco è una stringa *annidata*, e senza quel passaggio la citazione **pubblicata**
  non si trova.
- **Nessun riferimento in giro ⇒ non si legge nemmeno l'elenco dei documenti**: una query in meno all'apertura,
  che è il caso normale finché la biblioteca è nuova.
- ⚠️ `IAttachmentUsage` è **separata** da `IAttachmentLibrary` apposta: il redirect `/vsop/files/{slug}` chiama
  quella a ogni clic, e se «chi mi cita» fosse un campo della riga ogni apertura di un PDF pagherebbe una
  scansione di tutti i blocchi e di tutte le release.

**✅ Slice 5 fatta il 29 agosto 2026**: il blocco **Allegato**. `AttachmentRef` è la fonte unica del formato
(`{"ref":"allegato:…","titolo":…}`), `AttachmentLink` la resa condivisa e `AttachmentBlockEditor` l'editor —
una riga per `case`, come per le immagini. Tasto «+ Allegato» in tutti e due gli editor di blocchi.

- ⚠️ **`BlockFormat.Attachment` è IN CODA**, ed è la cosa da non toccare: nel payload di una release gli enum
  sono serializzati come **ordinali**, quindi inserirne uno in mezzo reinterpreterebbe in silenzio ogni
  release già pubblicata — un blocco tabella diventerebbe un'immagine. C'è un test che inchioda i sei valori
  storici alle loro posizioni.
- ⚠️ **Nel blocco finisce il TOKEN, non l'URL** — nemmeno il nostro: se ci finisse `/vsop/files/…`, spostare
  la rotta domani vorrebbe dire riscrivere il JSON di ogni blocco già pubblicato.
- ⚠️ **Solo lo schema `allegato:`**: un `ref` con un URL qualunque non è un riferimento, e accettarlo
  farebbe entrare un indirizzo arbitrario — `javascript:` compreso — in un `href` costruito da noi.
- **Si sceglie da un elenco, non si incolla un link.** Se dall'editor si potesse incollare un URL, il registro
  «chi cita cosa» direbbe il falso il giorno dopo: è il difetto che questa feature esiste per chiudere.
- **Il titolo sta nel blocco** ed è una decisione editoriale del documento: rinominare una voce in biblioteca
  non riscrive il testo dei documenti che la citano. A cambiare sotto è il **file**, non il nome.
- I punti toccati sono **sei** dei nove che dispatchano su `BlockFormat`: gli altri tre riguardano i **byte
  delle immagini** (pulizia, quota, editing) e un allegato non ne ha. Il debito del registry resta annotato.

**✅ Slice 5-bis fatta il 29 agosto 2026**: il modo **Incorporato**. Il PDF si legge dentro la pagina, in un
riquadro alto quanto uno dei **tre scaglioni** scelti dall'editore, **più il link sotto**.

- ⚠️ **L'iframe punta alla NOSTRA rotta**: il 302 vale anche dentro un riquadro, quindi l'indirizzo del
  deposito resta fuori dal documento esattamente come nel link. Nessuna eccezione.
- ⚠️ **`frame-src https://drive.google.com` aggiunta alla CSP nella STESSA slice.** Senza, la direttiva
  cadrebbe su `default-src 'self'` — ma siccome l'intestazione è **Report-Only** l'incorporato funzionerebbe
  oggi e morirebbe **in blocco** il giorno del passaggio a CSP vera. Presidiata da `SmokeTests`, non da una
  prova a mano: è la lezione delle tessere OpenTopoMap.
- ⚠️ **Il link sotto c'è sempre**, anche da incorporato: è il ripiego per il giorno che Google chiude
  l'embed (già successo col fondo mappa CARTO) ed è l'unica cosa che sopravvive alla stampa.
- **Stampa**: `vipi-print.css` nasconde l'iframe e stampa **l'indirizzo accanto al titolo** — la regola
  generale toglie la sottolineatura a tutti gli `<a>`, quindi senza questo un allegato stampato sarebbe una
  riga di testo che non porta da nessuna parte.
- **Tre scaglioni, non un numero libero** (320 / 520 / 800): un numero libero produce riquadri da 3000px e
  non se ne accorge nessuno finché non li apre un telefono.
- Nel JSON il modo si scrive **col nome**, non con l'ordinale, e un modo sconosciuto torna al **link**
  invece di far esplodere il blocco.

⚠️ **Da provare DAL VIVO prima di dire chiusa la 5-bis** (rischio R8): Google può togliere l'embed della
preview quando vuole. Nessun test qui dentro apre un browser.

**✅ Slice 6 fatta il 29 agosto 2026**: il link inline. Nella prosa si scrive
`[come si legge](allegato:lo-slug)` e diventa un'ancora verso `/vsop/files/{slug}`. **Chiude R1 della carta.**

- ⚠️ **È l'UNICO link che la prosa riconosce**, e non è una mancanza da colmare: `MarkdownLite` encoda e poi
  sostituisce con delle regex, quindi aprirlo a `[testo](url)` qualunque farebbe entrare un indirizzo
  arbitrario — `javascript:` compreso — dentro un `href` che costruiamo noi.
- ⚠️ **Lo slug è vincolato alla sua forma**, non è «qualunque cosa dopo i due punti»: senza,
  `allegato:../../qualcosa` passerebbe per uno slug e finirebbe dentro l'indirizzo che componiamo.
- La sostituzione gira **dopo** grassetto e corsivo (così il testo del link può portarli) e **prima** degli
  a capo (un'ancora non deve spezzarsi su una riga).
- Il registro «chi cita cosa» copriva già questa forma: lo scanner legge anche il `Body` in prosa, non solo
  il `BodyJson` dei blocchi.

**✅ Verificati i presidi rimasti indietro** (sito di sviluppo spento il 29 agosto): `Vipi.E2E.Tests` gira di
nuovo — **255 verdi** — e dentro ci sono i due test della slice 3 (302, destinazione, `no-cache`, 404), la
riga `frame-src` della 5-bis e `/vsop/files` fra gli endpoint macchina.

**✅ Slice 7 fatta il 29 agosto 2026**: la sostituzione. Il pannello mostra **prima** l'elenco dei documenti
che cambiano, con lo stato di ciascuno; poi il link e la nota di versione. Nasce la versione successiva, il
registro porta **id vecchio e nuovo**, e su ogni documento che cita la voce si apre una riga **«da rivedere»**.

⚠️ **La carta diceva «voce in Cambiamenti», e non era il posto giusto.** *Cambiamenti* è **pubblica** e si
ricava dai documenti col ciclo AIRAC corrente: una voce d'allegato lì sarebbe di natura diversa e rivolta al
pubblico, mentre la frase serve a **chi cura il documento**. È andata nella **casella degli impatti**
(`ImpactKind.AttachmentReplaced`, in coda), che è già «un fatto a monte tocca un documento» e ha la
deduplicazione su *(documento, tipo, origine)*.

- ⚠️ È l'**unico impatto in cui non c'è niente di rotto**: la copia pubblicata mostra già il file nuovo,
  perché il link segue la versione corrente. La riga serve a **farlo sapere**, e si chiude a mano.
- ⚠️ **Il non-evento non si registra**: rimettere lo stesso file torna `Invariato` e **non** apre righe —
  altrimenti si manderebbero delle persone a rileggere un documento che non è cambiato.
- ⚠️ **Chi cita si legge una volta sola, PRIMA di scrivere**: rileggere dopo vorrebbe dire che un salvataggio
  in un'altra scheda cambia l'elenco fra la conferma e la segnalazione.
- La sostituzione è un **servizio a parte** (`IAttachmentReplacement`): scrivere è una riga, sapere chi cita
  costa una scansione — e quella non la devono pagare né il redirect né l'elenco.

**✅ Slice 8 fatta il 29 agosto 2026 — la feature è COMPLETA, tutte e nove le slice.**

- **Cancellazione con guardia**: si vede **quali** documenti resteranno col link morto, si conferma, e quei
  documenti vengono segnalati (`ImpactKind.AttachmentDeleted`, in coda). ⚠️ **Non si rifiuta**: rifiutare
  avrebbe senso se ci fosse un modo automatico di rimediare, e non c'è. ⚠️ E il **file sul Drive resta**: i
  byte non sono nostri.
- **Ricerca**: presidiata — cercare «Marseille» trova la LoA dal titolo del blocco, e lo **slug non è testo**
  (cercarlo non pesca il blocco, né mostra JSON nel risultato).
- **Stampa**: già chiusa nella 5-bis.
- **Guida**: sostituzione ed eliminazione scritte nel capitolo dei blocchi, in italiano e in inglese.
- 🔧 `IAttachmentReplacement` è diventato **`IAttachmentCuration`**: fa due atti, non uno. Un nome che
  descrive metà di quel che c'è dentro mente a chi legge fra sei mesi, e rinominarlo prima che qualcuno lo
  citi costava una riga.

⚠️ **RESTA una sola cosa, e i test non possono darla**: il rischio **R8** — l'embed di Drive **non è mai
stato provato dal vivo**. Google può togliere l'incorporamento della preview quando vuole (è già successo col
fondo mappa CARTO il 27 agosto). Serve il sito acceso **e un PDF vero sul Drive di divisione** di cui
incollare il link. Il modo *Link* non ne dipende: a cadere sarebbe solo l'*Incorporato*, che ha comunque il
link sotto come ripiego.

**Da fare al deploy**: le due migrazioni `BibliotecaAllegati` (SQLite + MySQL) e l'annuncio agli staffisti
che la pagina **Allegati** esiste — il primo caricamento è a mano sul Drive.

⚠️ **La 5-bis non si chiude senza una prova dal vivo**: Google può togliere l'embed della preview quando
vuole — è già successo col fondo mappa CARTO il 27 agosto — e qui dentro nessun test apre un browser.

**Fuori perimetro, deciso**: `RealDOCS/IPI Roma ACC.pdf` (**180 MB**) e gli altri monoliti — sono i documenti
che il sito **sostituisce**, non allegati.

### E11 ✅ FATTA il 25 agosto 2026 — Documenti da rivedere: la casella degli impatti

Chiesto dal committente il **25 agosto 2026**, dopo l'analisi su *cosa succede se un dato importato viene
eliminato dal DB*. Carta (v2, rivista dopo revisione avversariale):
`docs/feature/2026-08-25-documenti-da-rivedere.md`.

**La domanda**: quando un settore sparisce, un'area cambia o un admin nasconde una postazione, **quali
documenti vanno rivisti o ripubblicati?** Oggi la risposta esiste per **un** caso su venti — `AccAdminService.cs:101`,
subcenter ACC nascosto — e per gli altri diciannove il sistema tace.

**Sei decisioni** (§3 della carta): tabella `DocumentImpact` a molte righe aperte per documento; rivelatore
per **deriva** calcolata (giro notturno, non solo eventi); il legame al documento **si tiene** finché
l'admin conferma; perimetro settori + aree; ancora sul **`DocumentId`** e non sul bersaglio di release
(la chiave è instabile, vedi **C6**); sezioni `Live` gestite alzando la **severità**, non con un watermark.

⚠️ **Tre cose che la revisione ha ribaltato** rispetto alla prima stesura, e che vale la pena non riscoprire:

- il reverse-lookup esistente (`EfDocumentReviewRepository.cs:31-37`) **sovra-segnala** — `IsPrimary || Type == App`
  dentro l'ACC significa *ogni* documento primario e *ogni* APP dell'ACC — e **sotto-segnala**: non guarda
  `Airport.DocumentId`, quindi uno scalo come LIBG non produce nessuna riga. Va riscritto **prima** di tutto (slice 0);
- `ProjectVipiSectors` gira a **ogni avvio** (`VipiModuleExtensions.cs:480`): con un catalogo vuoto o
  parziale — DB appena sostituito, import fallito — ogni settore proiettato risulta orfano. Serve la guardia
  «catalogo vuoto → nessun impatto» + soglia di massa, come già fa l'import aree;
- `ImportSpecialAreasAsync` fa `updated++` **senza confrontare niente** (`EfAccAdminRepository.cs:128-137`):
  «aggiornata» ≠ «cambiata», e senza confronto campo per campo la casella si riempie ogni notte.

**Misure che hanno deciso** (`vipi.db` del 18 agosto): 15 documenti, 34 release, **5** sezioni `Live` in
tutto — quattro `sids` (default, e il loro cambio è cadenzato dall'AIRAC via `SidRow.IsPublicAt`) e **una**
`coordination` manuale. È il dato che ha tolto di mezzo il watermark.

**Da dove nasce**: l'analisi
[«cosa succede se un dato importato viene eliminato dal DB»](history/audit-2026-08-25-cancellazione-dati-importati.md),
sette rilievi. Questo giro ne chiude **quattro** (documento sganciato, `BrokenTarget`, pre-check dei
`Restrict`, e la rinomina di §16); uno è diventato **C6**; gli altri due, più il terzo lasciato fuori dal
perimetro, stanno in **C7** — con il loro punto esatto nel codice, pronti da prendere.

**Che cosa c'è adesso**

| | Dove si vede |
|---|---|
| Banner **multi-riga** nell'editor, un rigo per fatto, col ✓ solo sulle righe non calcolate | i 4 editor |
| Pill di riepilogo | `/services/vsop/versions` |
| Sezione **«Orfani»**: elenco, documenti toccati, **riaggancia** e **rimuovi** | `/services/vsop/admin/sector-structure` |
| Conteggio per tipo + ultimo giro della deriva | `/services/vsop/admin/diagnostics` |
| Giro notturno (`ImpactDriftHostedService`, 24h, parte 100s dopo il boot) | — |

**Verificato sui dati veri** (copia del `vipi.db` di sviluppo, §14 della carta): settore cancellato → legame
al documento **conservato** + 2 segnalazioni; callsign che torna → righe **chiuse dal calcolo**; area
cambiata → 5 documenti avvisati; aeroporto scollegato → `BrokenTarget`; rimozione di un orfano bloccato →
**rifiutata con la frase**. ⚠️ Per copiare quel DB servono anche i file `-wal` e `-shm`, o SQLite lo dichiara
*malformed*.

**Trovato eseguendo, e non era nel piano**: la sentinella «riga aperta» **non può essere `0001-01-01`** — il
`DATETIME` di MariaDB parte dal 1000 e in `sql_mode` stretto lo rifiuta, mentre SQLite lo accetta: suite
verde e produzione rotta. È l'epoca Unix. Vedi §13.1 della carta.

**⚠️ 25 agosto, sera — la RINOMINA** (carta §16). Domanda del committente: «se `LIRN_US0_APP` diventa
`LIRN_US1_APP`, che succede?». Misurato: **peggio** della cancellazione. L'import fa upsert del nome nuovo, la
riga vecchia **resta** (i cataloghi non potano mai), e quindi restano **due settori attivi** con la stessa
shape: il documento è sul fantasma, chi controlla si connette col nome nuovo che non ha documentazione, e
**nessuna** delle otto famiglie di impatto se ne accorge — non è sparito niente.

Non era teoria: `LIED_G_APP`, l'APP primario di Decimomannu, aveva il timbro del **5 agosto** contro il **24**
delle altre tre posizioni dello stesso scalo. Diciannove giorni da fantasma.

Fatto, senza potare i cataloghi: il segnale è il **timbro** `ImportedAtUtc`, che il giro giornaliero riscrive
su tutto ciò che la sorgente elenca ancora. Nasce `ImpactKind.SectorStale`, la sezione «Orfani» mostra il
motivo **«non più elencato»** con la data, e — quando il candidato è **uno solo** — propone *«forse rinominato
in …»*. ⚠️ Proposta e mai automatismo: la cifra in `US0`/`US1` di solito vuol dire **sdoppiamento**, e i due
casi sono indistinguibili dai dati.

Tre guardie: righe **aggiunte a mano** escluse (colonne `IsManual` nuove + backfill one-shot che le riconosce
dal prefisso), niente segnalazioni senza l'ultimo giro riuscito di entrambe le famiglie, e **silenzio se gli
stantìi superano un quarto del catalogo** — quest'ultima l'ha imposta la prova sui dati veri, dove una
simulazione storta ha fatto comparire trenta settori esteri in blocco.

Verificato sull'archivio vero simulando un giro d'import completo: **una sola riga**, `LIBD_CS0_APP`, con
«forse rinominato in `LIBD_CS1_APP`».

**Restano fuori, dichiarati**: policy di import cancellata → «tutto importato» muto; ACC estero nuovo che
nasce con le aree accese; **audit delle cancellazioni strutturali**; notifiche (la casella è passiva) e il
legame con la scadenza AIRAC; famiglie oltre settori e aree (TA, piste, SID, shape); **watermark** delle
sezioni Live, con la soglia scritta in §3b. E dal giro della rinomina: il gesto «sposta i legami» **non**
sposta le citazioni per Id (accordi, parti di vLOA, blocchi) né ripunta i `ParentCallsign` dei figli — le
prime restano come bloccanti della rimozione, i secondi si sistemano a mano dalla Struttura. Sotto tutto
questo resta la domanda vera: **il callsign non è un'identità stabile** e la sorgente non ne espone un'altra
(`Sector.FacilityId` esiste dal primo giorno e non lo scrive nessuno).

## F. Rimandato, non cancellato

**Embedding nel sito `Ivao.It.Website`.** Il sito definitivo è il nostro host standalone, ma le cinque
librerie restano multi-target `net8.0;net10.0` proprio per questo — e ora che `Vipi.Host` è net8, la
distanza fra i due scenari è minima. Lavoro aperto in
[`guide/integrazione-ivao-it-da-fare.md`](guide/integrazione-ivao-it-da-fare.md): runtime EF Core 8 mai
eseguito (⚠️ ora lo sarà, in produzione), doppia localizzazione, Bootstrap del sito che sbava dentro
`.vipi-root`.

---

## G. Audit del database — 14 agosto 2026, chiuso lato codice

Carta ed esito in [`history/audit-2026-08-14-database-mariadb.md`](history/audit-2026-08-14-database-mariadb.md).
Sei commit **in questo ramo**. Cinque cose cambiano il comportamento in esercizio:

- **La concorrenza ottimistica era dichiarata e inerte.** `IsConcurrencyToken()` su sette `RowVersion`,
  funzionante su **uno**. Ora la rotazione la fa `VipiDbContext.SaveChangesAsync` — cioè il modello, non un
  repository che deve ricordarsene — e quattro entità hanno perso token e colonna, perché lì il
  last-write-wins è voluto.
- **Le chiavi Data Protection escono dal database del committente**: `/var/lib/vipi/keys` +
  `StateDirectory=vipi`. Stavano in chiaro in `DataProtectionKeys`, e chi ha `SELECT` su quel database
  potrebbe fabbricare un cookie valido per qualunque VID, admin compresi.
- **Pool a 20 + `EnableRetryOnFailure`** sul ramo MySQL: il default era **100**, contro un
  `max_user_connections` tipico di 25÷50.
- **Le quattro manutenzioni d'avvio non critiche sono isolate** (`RunVipiStartupMaintenance`): con
  `Restart=always` un guasto lì non era un degrado ma un ciclo di riavvii. `MigrateVipiDatabase` resta fatale.
- **`MySqlServerSettingsProbe`** verifica `sql_mode` e `max_allowed_packet`, provata guastando il server vero.

**La misura che ha deciso le priorità:** il `vipi.db` reale ha **~4 800 righe** (tabella più grossa
`AirportSids` 1481, corpo totale dei `ContentBlocks` **20 KB**, `AuditLogs` 20 righe). A questa scala **non
esiste un problema di prestazioni**: indici, cache e denormalizzazioni sono elencate in §E della carta come
scartate apposta, così non vengono riscoperte come idee nuove.

**Cosa resta aperto:** nulla di codice. Restano il **backup** (A9) e la **consegna del `.sql`** (A3).

ℹ️ Una regola che vale oltre questo caso: *«nessuno ha ancora applicato quella migrazione» è un'affermazione
sul mondo, non sul repository*. La carta proponeva di rigenerare `InitialCreate` MySQL perché nessun database
l'aveva vista; la MariaDB locale ce l'aveva già in `__EFMigrationsHistory`, e rigenerarla l'avrebbe resa non
aggiornabile.

---

## H. Frontend/UI — l'audit del 23 agosto, e ciò che è arrivato dopo

Carta ed esito per esteso in [history/audit-2026-08-23-frontend-ui.md](history/audit-2026-08-23-frontend-ui.md).
Quindici voci, tredici chiuse in giornata sul ramo `audit-frontend-ui` (sei commit, 3.595 test verdi,
verifica live fatta). ✅ **Il ramo è stato fuso in `main` la sera del 23 agosto**, ed è il codice della
consegna (**A11**). Qui restano le due che **non** sono state chiuse, e il perché — più **H3**, che
dell'audit non fa parte: è saltata fuori verificando il lavoro sui coordinamenti live dello stesso giorno
([feature/2026-08-23-live-coordinamenti-a-colonne.md](feature/2026-08-23-live-coordinamenti-a-colonne.md)),
ed è un difetto che stava lì da prima.

✅ **27 agosto 2026, pomeriggio: la sezione H è chiusa tutta.** Le ultime due — **H3** (la testata che
sborda) e **H1** (le `@media` cieche allo zoom degli editor) — stanno in **§M**, con quello che è saltato
fuori chiudendole: il `DeleteDialog` che impedisce la `@container`, e il riquadro che Blazor rifà.

**La sezione è diventata il posto dove finisce l'UI aperta**, non solo l'audit di quel giorno. Stato al 25
agosto, sera: **H1** e **H3** aperte come allora · **H2** ✅ **chiusa** — erano due difetti, non uno, e il
secondo l'ha trovato il martello a 2 milioni di giri · **H4** chiusa · **H5** ✅ **chiusa** — il VID è un
link, verifica live fatta, e il buco che ha trovato (nove VID muti nel Registro) è chiuso anche quello ·
**H6** ✅ **chiusa** — il numero che sbordava dalla ciambella.
**Aperte: H1 e H3.**

### H6 ✅ CHIUSA — il totale sbordava dalla ciambella, e si vedeva solo su una pagina

Segnalata dal committente il 25 agosto sera su `/services/stats/division`.

Il buco della ciambella è largo **69 unità** del viewBox (r 42 meno mezza traccia da 15 per parte) e il corpo
del numero era **fisso a 19**: cinque cifre ci stanno («1234,5»), sei no — «12345,6» misura circa 80 unità e
finisce **sopra l’anello**. `StatsDonut.FontCentro` ora ricava il corpo dalla lunghezza (mai oltre 19, mai
sotto 11), coi casi limite fissati in un `[Theory]`.

⚠️ **La lezione non è «rimpicciolire il testo».** Il componente era stato provato — anche dalla verifica
live — solo con le ore di **una persona**, che sono sempre corte. Le ore di una **divisione** non lo sono, e
i due usi vivevano nello stesso file senza che niente lo dicesse. Un componente provato su un solo ordine di
grandezza non è provato.

Dettaglio in §13.8 della carta delle statistiche.

### H1 ✅ CHIUSA il 27 agosto 2026 — `.ed-layout` e le altre `@media` degli editor

**Cos'è.** La stessa malattia curata sul viewer (A3 della carta): una `@media` misura la **finestra**, mentre
lo zoom di questa applicazione è `zoom` sull'`<html>` e la finestra non lo vede. Sul viewer pubblico il
danno era misurato e grave — il documento scendeva a **161px a zoom 1.8** fra due barre laterali a larghezza
fissa — ed è stato chiuso con una `@container`.

Restano **`.ed-layout`** più **dieci regole `.struct`**, tutte sulle pagine di editor e admin.

**Perché non è stata fatta insieme.** Le pagine admin hanno un perimetro d'uso **dichiarato** da 1024px in su
(`design/regole-ui-pagine-admin.md`): lì l'assetto attuale è quello voluto, non un incidente. E ogni regola va
decisa e **vista a schermo** una per una — non è un travaso meccanico come lo era per il viewer, dove il
layout è uno solo e ripetuto su quattro pagine.

**Come si riprende.** Gli attrezzi ci sono già, nello scratchpad della verifica live: `zoom2.js` interroga
`matchMedia` a cinque livelli di zoom e stampa le colonne effettive, `zoom3.js` fa la controprova a finestra
stretta e verifica che il contenimento non tocchi chi non deve.

> ⚠️ **La trappola da conoscere prima di misurare.** In **Edge 151** `documentElement.clientWidth` **non è
> più in unità di layout** sotto `zoom`: restituisce i px di finestra. Una misura dedotta da lì dice che non
> succede niente. Si chiede a `matchMedia`, che è ciò che decide davvero se la regola vale.

> ⚠️ **E la trappola del rimedio.** `container-type:inline-size` porta con sé `contain:layout`, che rende
> l'elemento contenitore anche per i discendenti `position:fixed`. Le pagine di editor hanno un
> `.editor-toast` fisso **dentro** il `.wrap`: mettere il contenimento sul `.wrap` glielo incolla dentro. Sul
> viewer è stato aggirato con `.wrap:has(> .doc-layout)`; per gli editor servirà una soluzione propria.

### H2 ✅ CHIUSA — il rosso di `Vipi.Application.Tests` era **due** difetti, non uno (25 agosto 2026, sera)

**Come stava scritto** (23 agosto): «in uno dei giri completi la suite ha segnato **1 fallimento su 625**, e
il nome non è stato catturato; in sei esecuzioni successive non si è più presentato». La voce diceva anche
come riprenderla — catturare il nome con `grep "\[FAIL\]"` invece di filtrare il riepilogo. È bastato farlo.

**Il nome.** `Vipi.Application.Tests.AorProiezioneProperties.Il_rapporto_fra_i_lati_e_quello_vero`, caduto
nella corsa completa in Release del 25 sera **su net10 e non su net8**. ⚠️ Il TFM non c'entra: è una
proprietà **CsCheck**, e i due TFM sorteggiano poligoni diversi.

**Il seme, che la rende riproducibile a comando:**

```
CsCheck_Seed=bxKC4K6PiVz6 dotnet test tests/Vipi.Application.Tests -c Release -f net10.0 --filter "FullyQualifiedName~Il_rapporto_fra_i_lati_e_quello_vero"
```

Fallisce sempre, in 21 ms: `Expected 374.00806170184399, Actual 371.7`.

**La causa, e non è nel proiettore.** La proprietà ricalcola per conto suo la proiezione per confrontarla
con quella vera, e per farlo prende `k = cos(latitudine media)` dai punti **generati**. Ma
`AorPolygonProjector` lavora sui punti **parsati**, e dal 25 agosto `PolygonGeometry.ParsePoints` passa per
`SenzaPuntiGemelli`, che **toglie i punti ripetuti di fila** (i lati a lunghezza zero: li ha il 29% dei
poligoni reali, ed erano il sospetto numero uno sulle facce degeneri dell'estrusione 3D).

Il campione che fallisce comincia con `(36, 13), (36, 13)` — due gemelli. Tolto il doppione, la media delle
latitudini sale da 40,3296 a 40,7626, `k` scende da 0,76245 a 0,75758, e la larghezza del viewBox scende di
2,3 unità. Rifatto il conto a mano fuori dal test:

| conto | larghezza |
|---|---|
| con i gemelli (quel che si aspetta la proprietà) | **374,00806170184** |
| senza i gemelli (quel che fa il proiettore) | **371,70117732413** → arrotondato `371.7` |

Sono esattamente i due numeri dell'asserzione, all'ultima cifra. Non è tolleranza in virgola mobile: lo
scarto è 2,3 su una soglia di 0,1.

⚠️ **Quindi il rosso è del ramo `statistiche-atc`**, non un fantasma del 23 agosto: `SenzaPuntiGemelli`
nasce lì, il 25. Se il rosso di due giorni prima fosse stato lo stesso test la causa era per forza un'altra
— la famiglia però è quella, «una proprietà che cade solo per certi sorteggi», ed è il motivo per cui in
sei giri non si era più vista.

**Il primo difetto, chiuso.** La proprietà ora modella il proiettore con **gli stessi punti che il
proiettore vede**: `PolygonGeometry.ParsePoints(json)` invece di `punti`. Col seme `bxKC4K6PiVz6` passa.

**Le altre cinque, guardate una per una** — perché «chi parte dai punti generati ha lo stesso difetto» era
un sospetto, non una misura. Esito: **nessun'altra rifà il conto**. Le altre cinque confrontano fra loro due
*uscite* del proiettore (o due proiezioni dello stesso ingresso), e per costruzione il parsing lo attraversano
tutt'e due allo stesso modo.

**Il secondo difetto, trovato col martello e chiuso anche quello.** Non bastava guardarle: sono state
rilanciate a **200 000** giri invece dei 100 di default, ed è caduta subito una proprietà diversa —
`Spostare_la_longitudine_non_cambia_il_disegno`, sui **punti del path**. Nulla a che vedere con i gemelli:
è il **mezzo decimale**. Il proiettore emette valori già arrotondati a un decimale (`R()`), e
`Assert.Equal(a, b, 0)` — «uguali arrotondati a **zero** decimali» — non è una tolleranza, è un **secondo**
arrotondamento con un **secondo** mezzo su cui cadere: 223,5 e 223,4 distano un passo di `R()` e diventano
224 e 223.

⚠️ Ed è un difetto che il file **aveva già curato una volta**, sul viewBox, dimenticando il path: il
commento a fianco cita il seme `bryagYjiWP_m` e mette tolleranza `0,11`. Le due righe del path erano rimaste
a `0`. ⚠️ **È il candidato più probabile per il rosso visto il 23 agosto**, che invece NON poteva essere il
conto della latitudine media: `SenzaPuntiGemelli` è nato due giorni dopo.

ℹ️ La correzione **stringe**, non allenta: `0,11` è una tolleranza assoluta, mentre «arrotondati a zero
decimali» tollerava fino a quasi un'unità intera nei casi non di confine.

**Il controesempio è congelato**, come prescrive il commento della classe:
`AorPolygonProjectorTests.Un_Punto_Ripetuto_Di_Fila_Non_Cambia_La_Proiezione` — un poligono con un gemello
consecutivo disegna **identico** a quello senza, viewBox compreso.

**Come è stata verificata.** `CsCheck_Iter=2000000` su **entrambi** i TFM, cioè ventimila volte la copertura
di un giro normale: verdi. Poi Release `--no-incremental` **0 avvisi** e suite completa **tutta verde** —
2243 net8 / 2005 net10.

⚠️ **La lezione, ed è generale.** «Rosso intermittente» era una diagnosi sbagliata due volte: una proprietà
CsCheck non è ballerina, cade **per certi sorteggi** — e il modo di trovarli non è rilanciare finché passa,
è **alzare le iterazioni**. Cento giri al giorno non sono una rete: sono un campione. Se un giorno una di
queste torna rossa, il primo gesto è `CsCheck_Iter=2000000`, non `dotnet test` un'altra volta.

### H4 ✅ CHIUSA il 23 agosto — l'intestazione della tabella ACC non si appiccicava affatto
Segnalata dal committente («l'header appare come una colonna normale») mentre preparava il deploy, e non era
un difetto di aspetto: **la `thead` non era sticky per niente**. Misurato: dopo 1200px di scorrimento
l'intestazione stava a `y = -812`, cioè fuori dallo schermo come una riga qualunque.

**La causa.** `.wrap *:has(> table):not(.st-scroll){overflow-x:auto}` (commit `a3b60d5`, la mattina del 23:
«le tabelle scorrono a qualunque zoom») dava `overflow-x:auto` a **ogni** contenitore diretto di una
tabella dentro `.wrap` — compreso il `<div class="block">` della pagina ACC. E un `overflow` diverso da
`visible` rende quel contenitore il **riferimento** dello `position:sticky` che sta dentro: l'intestazione
si appiccicava a un contenitore che non scorre, cioè a niente.

⚠️ **Non è aggirabile con `overflow-y:visible`**: per specifica, se un asse non è `visible` l'altro calcola
`auto`. Le due cose — «questo contenitore scorre in orizzontale» e «l'intestazione si appiccica alla
finestra» — **si escludono per costruzione**, e la scelta va fatta.

**Cos'ha deciso la misura.** Quelle tabelle non sfiorano il loro contenitore a nessuna larghezza:
1486 in 1534 (a 1600px), 1261 in 1309, 1179 in 1227, 933 in 981 (a 1024px). La barra orizzontale **non
sarebbe mai comparsa**, quindi la regola su quella pagina non comprava niente e costava il difetto.
Aggiunto `:not(:has(> table.sticky-head))`. Dopo: `y = 133` dopo 1200px di scorrimento — esattamente il
`top` calcolato — e nessun contenitore che scorra fra l'intestazione e la finestra.

**Raggio, misurato e non dedotto.** Delle sei pagine admin con tabelle, solo ACC era rotta: Aeroporti e
Registro tengono le loro in `.st-scroll`, che è il contenitore che scorre **apposta** e dove l'intestazione
sta a `top:0`; le altre tre non rendono tabelle appiccicate. Build Release 0 avvisi su due TFM, 3595 test
verdi.

ℹ️ **La regola diceva «del viewer» nel commento e `.wrap *` nel selettore.** È lì che è passata: il commento
descriveva l'intenzione, il selettore ha preso anche le pagine admin. Vale come promemoria — quando una
regola nasce per una famiglia di pagine, il perimetro va **nel selettore**, non nella prosa accanto.

### H3 ✅ CHIUSA il 27 agosto 2026 — `/services/vsop/admin/acc` sforava di 24px a OGNI larghezza

**Cos'è.** A 1600px di finestra la pagina ACC chiede 1624: la testata appiccicata
(`.doc-head.st-head.sticky`) misura **1648** dentro un contenuto da 1536.

⚠️ **23 agosto, sera — misurato più largo di com'era scritto**: non è un difetto dei 1600px, c'è a **ogni**
larghezza provata. Testata contro contenuto: **1648/1600** · **1407/1366** · **1318/1280** · **1055/1024**;
lo sforo di pagina vale 24 · 20 · 19 · 15 px. Cioè la testata è sistematicamente ~30÷48px più larga del
contenuto che la ospita, e la larghezza della finestra non c'entra. Trovato misurando la pagina per H4. Non c'entra la potatura del foglio
di stile del 23 agosto: ⚠️ **misurato con il foglio di PRIMA e con quello di DOPO, il numero è lo stesso**
(1624 in tutt'e due), quindi il difetto stava lì da prima e nessuno l'aveva visto.

**Perché non è stato chiuso subito.** Perché è un difetto a sé, e sistemarlo dentro un giro di pulizia
avrebbe mescolato due cose. Il colpevole è uno solo e si trova in una riga
(`node sfora.js http://localhost:5099/services/vsop/admin/acc` nella skill `verifica-live`).

### H5 ✅ CHIUSA — il VID è un link al profilo IVAO, e la verifica live ha trovato un buco vero

Chiesto dal committente e **fatto**: cliccando un VID, in qualsiasi pagina, si apre
`https://ivao.aero/Member.aspx?Id=<VID>`. Quindici punti in dieci file, un componente solo
(`Components/VidLink.razor`), sul ramo `statistiche-atc` (`03463bf`). Carta con tutto:
[`feature/2026-08-25-vid-porta-sul-profilo-ivao.md`](feature/2026-08-25-vid-porta-sul-profilo-ivao.md).

**La verifica live è stata fatta** (Edge + puppeteer-core su una copia del `vipi.db`, nove pagine guidate) e
ha detto due cose.

**Quel che funziona, misurato.** La **risalita del clic** in Permessi è ferma: il clic arriva all'ancora
(`clic: 1`) e la selezione non si muove — con la **controprova** che cliccando la riga lontano dal VID la
selezione cambia, altrimenti «non è successo niente» poteva voler dire «il clic non è arrivato». Le pagine
**SSR statiche** portano i link senza circuito (53 nella classifica). Nei **due temi** il colore del link è
identico a quello della cella, e col mouse sopra arriva il blu. In **stampa** la punteggiatura sparisce.

**Il buco, ed era vero.** Nel Registro **nove VID a schermo e zero link**: la colonna «cosa» porta le frasi
del narratore («Granted VID 704798 permission on LIRR»), e lì il VID non è un campo ma una **parola**.
⚠️ Nessuna prova sbagliava — nessuna guardava quella colonna, perché quella colonna non era stata toccata.
**Solo lo schermo poteva dirlo.**

**Chiuso con un secondo componente**, `VidText`: prende la frase già composta e la taglia sulla forma che
scriviamo noi (`Audit_VidN`, «VID 1234567»), emettendo i pezzi in mezzo come **testo** — niente
`MarkupString`, perché quelle frasi portano dentro titoli e note scritti da persone. Aggancia quattro punti:
Registro, la stessa frase nella riga di storia di Versioni, il «Deciso da …» di Sorgenti e l'«Assegnato da …»
di Incarichi — cioè **anche i due che questa voce dava per irrisolvibili**. Riverificato: 9 su 9 nel
Registro; le altre due frasi si sono viste **seminando** un incarico e una policy nella copia, perché su
questi dati non c'erano.

⚠️ **La forma tagliata dipende da una risorsa tradotta.** Se qualcuno ritraduce `Audit_VidN`, `VidText`
smette di trovare qualunque cosa **in silenzio**: per questo `VidTextTests` legge i due `.resx` dal disco e
fa fallire la suite invece.

**Cosa resta davvero, e sono due limiti del formato**: le **chip** di Incarichi (un `<a>` dentro un
`<button>` non è HTML valido) e le **tendine** di assegnazione (un `<option>` è solo testo). In tutt'e due
il VID compare solo come ripiego.

🟢 **E una voce aperta, piccola: la Guida non nomina il gesto.** `GuidaPage` parla di VID nella sezione
Permessi ma non dice che il numero si può premere. Da mettere insieme al capitolo sulle statistiche, che è
già una voce aperta di **B12** — così la Guida si tocca una volta sola.

ℹ️ **Due cose viste e non toccate**, con il perché nella carta (§8): «Carmine (704798)» nella colonna «chi»
non è un link perché quel numero sta dentro un **nome** (il `publicNickname` di IVAO), e «VID 0» non è un
link perché zero non è una persona — è la prima versione dei documenti generati dal profilo aeroporto, e la
sua esistenza nei dati veri conferma che quel ramo di `VidLink` serviva.

---

## I. Dopo la pulizia del database (26 agosto 2026)

Il committente ripulisce il database **un'ultima volta** prima di iniziare a popolare i dati veri. Queste
voci nascono dall'inventario dell'archivio fatto il 26 sera e **si guardano dopo**, non prima: sistemare
oggi un albero che sta per essere rifatto sarebbe lavoro buttato.

### I1 🔵 SOSPESA — le sette radici orfane di LIRR

`LIRR` ha **otto radici CTR** e una sola (`LIRR_EW_CTR`) porta il documento; `LIPP` ne ha due. Un albero così
scollegato è la ragione per cui un residuo si era formato: quando un import cambia il padre di un settore,
ciò che ci stava appeso si stacca — ed è esattamente com'è nata la «vIPI Roma» fantasma
([§17 della carta](feature/2026-08-26-eliminare-con-le-protezioni.md)).

Da rifare **dopo** la pulizia, e su dati veri: agganciare le radici superflue sotto quella buona dalla
pagina Struttura. ⚠️ Il riaggancio va scritto nel **catalogo** — è quello che la proiezione rilegge — e
adesso l'eliminazione lo fa già da sola (§14).

### I2 🔵 SOSPESA — lo stato dell'archivio, com'era il 26 agosto

La fotografia da cui ripartire per capire **cosa c'è davvero** (misurata sul `vipi.db` di sviluppo, prima
della pulizia):

| | |
|---|---|
| Documenti | 19, poi **18**: la «vIPI Roma» fantasma è stata eliminata |
| Pubblicati e visibili | **14** bersagli: 2 ACC (Brindisi 61 KB, Milano 5 KB), 3 APP, 5 aeroporti, 4 vLOA |
| Mai pubblicati | 4: **vIPI Roma** e **vIPI Padova** (scheletri nudi), Bologna Radar, vIPI LIBA |
| ACC italiane | Brindisi finita (21 sezioni, 9 blocchi pieni, 15 versioni) · Milano a metà · **Roma e Padova da scrivere** |
| Aeroporti | **6 documenti su 93**; 78 scali hanno settori e nessuna vIPI |
| Da ripubblicare | 4 bozze più avanti della copia pubblicata: Brindisi v15, Pescara v2, vLOA LGGG v2, vLOA LDZO v3 |

⚠️ Nell'archivio **due documenti diversi possono avere lo stesso titolo**: dove si elencano documenti, il
numero va accanto al nome quando il nome si ripete.

### I3 🟡 APERTA — gli orfani non sono tutti orfani, e ora si può sapere quali

Provando «chiedi alla sorgente adesso» ([carta](feature/2026-08-26-chiedere-alla-sorgente.md)) contro IVAO
vero, i **nove** orfani della Struttura si sono divisi così:

| | |
|---|---|
| la sorgente li **manda ancora** | LIBB_EU_CTR, LIRO_CRC_CTR, LIVK_CRC_CTR, LIVK_RCC_CTR, LIZZ_AAR_CTR, LIZZ_AEW_CTR, LIZZ_JTA_CTR, LIZZ_NVY_CTR — **otto** |
| **sparito davvero** | LIED_G_APP (Decimo Precision): `LIED` ne elenca 3 e questo non è fra loro |

⚠️ Otto su nove sono orfani perché qualcuno li ha **nascosti nel nostro catalogo**, non perché IVAO li abbia
tolti — e la sezione «Orfani» li mostra tutti uguali. Sono due situazioni diverse con due rimedi diversi:
uno si **rimostra**, l'altro si **elimina**. Da decidere dopo la pulizia, uno per uno; il tasto per
distinguerli adesso c'è.

### I4 🟡 APERTA — l'azione di gruppo sugli aeroporti non offre la domanda

`AeroportiPage.razor:619` elimina in blocco chiamando `EliminaAsync` senza verifica alla sorgente: chi la usa
passa dalla regola dei due giri come prima. È **voluto** — una raffica di verifiche puntuali su N scali è
esattamente ciò che la carta §3/P7 evita — ma il tasto singolo e quello di gruppo si comportano
diversamente sullo stesso oggetto, e va deciso se dirlo a schermo o dare al gruppo una verifica sola.

---

## J. Identità dei settori e shape — 26 agosto 2026, ramo `identita-settori`

Ramo aperto **da `statistiche-atc`** (non da `main`), **non fuso**, spinto su origin. Porta **dieci lavori
chiusi e nessuna voce aperta**. Carte:
[identità dei settori](feature/2026-08-26-identita-dei-settori.md),
[l'assenza non cancella](feature/2026-08-26-lassenza-non-cancella.md),
[le shape dal sectorfile](feature/2026-08-26-shape-dal-sectorfile.md),
[l'ordine delle sezioni](feature/2026-08-26-ordine-sezioni-personalizzato.md),
[il riordino trascinando](feature/2026-08-26-riordino-sezioni-trascinando.md).

⚠️ Il quarto lavoro (**J6**) non c'entra con i settori: è arrivato dal committente mentre il ramo era aperto,
e sta qui perché sta qui il ramo. **Non aggiunge migrazioni.**

⚠️ **Le migrazioni in coda passano da quattordici a DICIASSETTE**: `IdentitaDeiSettori`, `ShapeVuoteANull`,
`GateAiracShape`. Conta per §B12 e per il cutover MariaDB.

⚠️ **Questo ramo si somma a `statistiche-atc`, non lo sostituisce.** La decisione B12 (fondere) adesso
riguarda **due** rami in fila, e questo va fuso **dopo** quello.

### J1 ✅ FATTA il 26 agosto — l'avviso a chi pubblica una shape non ancora in vigore

Il gate AIRAC faceva già la cosa giusta da solo, ma **in silenzio**: chi pubblica vedeva a schermo il confine
nuovo e nel documento ne trovava un altro. Ora l'avviso sta **nel pannello release**, sopra i due tasti che
pubblicano, con l'interruttore accanto.

- **`ShapeGateNoticeService`** (`src/Vipi.Application/Content/ShapeGateNotice.cs`) — dice quali aree del
  perimetro resterebbero indietro, e le forza. ⚠️ **Nessuna regola nuova**: la domanda «è differita?» la fa
  `ShapeAiracGate.IsDeferredAt`, la stessa del congelamento. Se le due divergessero, l'avviso mentirebbe.
- **`EfShapeGateRepository`** (`src/Vipi.Infrastructure/Persistence/`) — il perimetro per bersaglio di
  release: `AccVipi`/`Vloa` → i settori della ACC (subcenter + posizioni d'aeroporto), `Airport`/`App` → le
  posizioni di quell'ICAO (la chiave dell'APP è un callsign: l'aeroporto sono le prime quattro lettere).
- **`ReleasePanel.razor`** — callout `warning` con callsign, nome e ciclo d'entrata, più il tasto
  «Pubblica comunque le aree nuove». Chiavi `Rel_Shape*` in italiano e inglese.

⚠️ **I cicli in gioco sono DUE**, perché i tasti sono due: «pubblica ora» usa il ciclo corrente, «pubblica al
ciclo» quello scelto nella tendina. Si avvisa per l'**unione**: sbagliare per eccesso costa una riga di
troppo, sbagliare per difetto costa un confine vecchio pubblicato senza che nessuno l'abbia saputo. L'avviso
si ricalcola anche quando si cambia il ciclo nella tendina (`@bind:after`).

⚠️ **Il perimetro è quello dell'ENTE, non l'elenco esatto delle configurazioni AoR.** Ricavare quello vorrebbe
dire rieseguire la derivazione del documento — cioè il congelamento — solo per decidere se mostrare un
avviso. L'imprecisione è dalla parte giusta: si può avvisare per un settore che quella mappa non disegna, mai
tacere per uno che disegna davvero.

⚠️ **Forzare non vuol dire «è in vigore»**: `ShapeAiracCycle` resta scritto. Quando il ciclo arriva davvero, la
promozione notturna chiude la pratica e **spegne la forzatura da sé**. Il permesso è quello del documento che
si sta pubblicando (ACC-scoped, o del documento per la vLOA): forzare è un atto editoriale.

Test: `ShapeGateNoticeTests` (8), `ShapeGateScopeTests` (6), più 4 in `ReleasePanelTests`.

### J2 ✅ DECISA il 26 agosto — i ripieghi valgono **solo per gli enti della divisione**

**Decisione del committente**: *le aree degli ATC esteri le dà IVAO, se ce le dà*. Un ente straniero senza
poligono resta senza poligono — né dal sectorfile, né da GitHub, né col cerchio sintetico. La ragione è che
quei confini non sono nostri: prenderli da una fonte che non è l'anagrafica del titolare vuol dire pubblicare
come vera un'area che nessuno di competente ha approvato.

La regola sta in **un posto solo** (`ShapeFallbackScope`, `src/Vipi.Application/Content/`), perché i ripieghi
sono tre e tre copie della stessa condizione sono tre racconti che prima o poi divergono. Riusa la stessa
domanda della gerarchia (`HierarchyRules.IsForeignCode` sui prefissi di `DivisionOptions`): «estero» ha una
definizione sola. Applicata a:

| Ripiego | File | Cosa cambia |
|---|---|---|
| Sectorfile (CTR/APP/MIL/FSS) | `SectorShapeFallbackService` | esteri fuori dai bersagli **e dal conteggio** |
| GitHub `twrs.tfl` (TWR) | `GithubTowerShapeService` | esteri fuori, anche col bottone manuale per ICAO |
| Cerchio 5 NM (TWR) | `TowerShapeFallbackService` | mai un cerchio finto su un campo estero |

⚠️ **Vale per i ripieghi, non per l'anagrafica**: le shape che IVAO manda si scrivono per tutti, esteri
compresi, esattamente come prima.

⚠️ Misurato in archivio: dei 118 settori esteri, **116 hanno già la shape da IVAO** (`ShapeSource = Source`) e
gli aeroporti in `AirportSectors` sono **tutti** italiani — la decisione oggi non toglie niente a nessuno, ma
chiude la porta prima che si apra.

⚠️ **Il caso che aveva aperto questa voce non era una decisione di divisione: era un difetto nostro.** I
punti `GODRA` e `GIGUS` **ci sono** — in `ESTERNI.fix`, un file che non leggevamo. Vedi **J8**.

### J3 ✅ DECISA il 26 agosto — quei settori **non devono avere** un'area

Contati sul `vipi.db` di lavoro il 26 agosto (non ricordati): **11 righe su 153** in `AccSectors`. In
`AirportSectors` le 51 senza poligono non contano — sono tutte ATIS/GND/DEL più una TWR (`LIED_TWR`):
posizioni che un'area non ce l'hanno per natura.

| Callsign | Nome | Che roba è |
|---|---|---|
| `LIPP_PLN_CTR` | Padova CE1 Planner | pianificazione |
| `LIRR_PLN_FSS` | Roma FSS Planner | pianificazione |
| `LIRO_CRC_CTR` | Barca Radar | militare |
| `LIVK_CRC_CTR` | Pioppo Radar | militare |
| `LIVK_RCC_CTR` | RCC | militare |
| `LIZZ_AAR_CTR` | Boom | militare (rifornimento in volo) |
| `LIZZ_AEW_CTR` | Legion | militare (AEW) |
| `LIZZ_JTA_CTR` | Gladiator | militare |
| `LIZZ_NVY_CTR` | Navy | militare |
| `DTTC_FMP_CTR` | Tunis ATFM | **estero** — per §J2 non lo tocchiamo più |
| `LOVV_EXA_CTR` | Vienna | **estero** — per §J2 non lo tocchiamo più |

Sette di questi sono **inattivi** in `Sectors` (`IsActive = 0`): i due `LIVK_*`, `LIRO_CRC_CTR` e i quattro
`LIZZ_*`. Gli altri quattro sono attivi.

**Decisione del committente**: vanno bene senza area, e non c'è niente da disegnare. Non sono settori con un
volume proprio: sono **postazioni operative in più** sullo stesso cielo di qualcun altro — guidacaccia,
planner, coordinamento — e un poligono per loro sarebbe una finzione. Non è un dato mancante: è un dato che
non esiste perché non ha senso.

Con §J2 i due esteri escono comunque dal conto per conto loro.

⚠️ L'elenco di questa voce nella stesura precedente era **sbagliato** (citava `LOVV_FSS`, `LSAS_EXA_FSS`,
`DTTC_FSS`, `LMMM_FSS`): quei quattro l'area ce l'hanno. Contato, non ricordato.

### J4 ✅ FATTO il 26 agosto — ripristino dei poligoni persi

`tools/ripristino-shape/ripristina-poligoni.sql` **eseguito** sul `vipi.db` di lavoro, con backup preso
prima (`src/Vipi.Host/vipi.db.bak-pre-ripristino-shape-20260826`) e host fermo.

```
AccSectors con poligono      5 → 142
AirportSectors con poligono 83 → 141   (58 APP)
righe                      153 → 153 · 192 → 192
TWR reali/sintetiche        66 / 17    (intatte)
```

Verifica: **283 poligoni in archivio, tutti e 283 si proiettano**; dei 211 settori che possono avere un'area
ne hanno **200**, contro i 5 di partenza.

⚠️ Lo script vale per **SQLite**. In produzione (MariaDB) il travaso è un'altra cosa: si esporta dal backup
e si applica per `UPDATE`.

### J5 ✅ CHIUSA — IVAO ha confermato che è un guasto loro

Il campo `regionMapPolygon` è vuoto su **tutta** l'API (misurato su 237 risorse, tre tipi, sei paesi, incluse
le forme `/all` e la chiamata pubblica esatta di webeye). **IVAO ha confermato il 26 agosto**, su richiesta
del committente, che è un **guasto loro** e che lo sistemeranno.

Quindi il ripiego dal sectorfile è una rete, non una sostituzione — e il rientro **è già provato**: quando
l'anagrafica torna a mandare una shape vera, riprende il comando per intero (provenienza a `Source`,
differimento chiuso). Vedi §2-ter della carta.

ⓘ **Una cosa da ricordare comunque**: il **tracker** (`/v2/tracker/now/atc/summary`, pubblico, senza token)
porta i poligoni **pieni** annidati in `subcenter`/`atcPosition`, ma solo per gli ATC connessi in quel
momento. Se il guasto dovesse durare, è la sorgente di riserva — e **non ha bisogno del gate AIRAC**, perché
è quel che IVAO serve ai controllori adesso.

### J6 ✅ CHIUSA — l'ordine delle sezioni è una scelta editoriale

Richiesta del committente, 26 agosto sera: «le sezioni devono poter essere spostate sopra o sotto all'interno
dello stesso gruppo, e ognuna deve riportare quante posizioni è sopra o sotto quella standard».

**Il motore c'era già.** `MoveSectionAsync` scambia `DocumentSection.Order` fra **fratelli** — che è già «lo
stesso gruppo»: il blocco per la vIPI ACC, la radice per APP e vLOA — e l'`Order` è versionato, copiato in
bozza e catturato nello snapshot di release. Mancava **il tasto**: `DocumentSectionsEditor` legava
`IsMandatory` a tre divieti insieme (rinomina + elimina + **sposta**). Le prime due restano: **è l'ordine a
non essere del catalogo, non l'identità della sezione**.

Lo scostamento dall'ordine standard lo calcola `SectionOrdering.OffsetsFromStandard` (funzione pura), e
l'editor lo scrive come pill accanto al titolo: `↑2`, `↓1`. Si legge **sempre**, non solo in modifica.

⚠️ **Si contano solo le sezioni FISSE, e solo quelle PRESENTI.** Una sezione libera non ha una posizione
standard: contarla farebbe apparire `↓1` su tutte le fisse che la seguono appena qualcuno ne infila una in
testa — scostamenti che nessuno ha prodotto. E una sezione di catalogo assente (il VFR su un blocco Aerovia)
non lascia un buco.

⚠️ **Difetto trovato strada facendo, ed è la trappola del doc 11 §8**: il viewer della vLOA rendeva le due
**direzioni** dei coordinamenti in una sequenza scritta nel codice (uscente, poi entrante), pur
riconoscendole per chiave. Spostarle nell'editor avrebbe cambiato l'editor e **non** il documento pubblicato.
Ora segue l'ordine delle sotto-sezioni, con l'ordine canonico come ripiego per gli **snapshot storici**, dove
entrambe portano ancora la chiave del padre e si distinguono solo per posizione.

Nove test nuovi, suite intera verde, Release **0 avvisi** sui due TFM. Verifica live sulle **tre famiglie**
(§7 della carta): vIPI ACC, vIPI APP e vLOA — i due documenti che il database di sviluppo non aveva sono
stati creati al volo sulla copia. La prova che vale di più è la vLOA: spostata una delle due **direzioni**
dei coordinamenti, **l'anteprima bozza del documento la rende nell'ordine nuovo**. Sull'editor ACC
(`/services/vsop/libb/editor`) le sezioni «obbligatoria» mostrano `↑ ↓`, *Frequenze* portata sopra *AOR*
persiste al ricarico e le due sezioni portano `↑1` e `↓1` anche fuori dalla modifica. Commit `30dad4e`.

### J7 ✅ CHIUSA — anche i blocchi della vIPI ACC si riordinano, ma l'Aerovia resta in testa

Seguito naturale di J6, chiuso il 26 agosto sera tardi. Due decisioni del committente: le frecce stanno
**nell'intestazione del blocco** (dentro il `<summary>`, solo in modifica, accanto al campo del titolo e a
«✕ Gruppo»), e **i settori di aerovia restano primi** — i gruppi APP si riordinano fra loro, nessuno passa
sopra l'Aerovia.

Il motore è sempre lo stesso: i blocchi sono le **sezioni radice** del documento, quindi `MoveSectionAsync`
va bene com'è. Quel che si aggiunge è la **regola**, e sta in `AccDocumentService.MoveGroupAsync`: legge i
blocchi nell'ordine del documento con `AccDocumentAssembler` — così l'Aerovia si riconosce dal **blockmeta**
e non dal posto che occupa — e rifiuta in silenzio la mossa che uscirebbe dall'elenco, che partirebbe
dall'Aerovia o che le passerebbe sopra.

⚠️ **La regola sta in due posti apposta, e non è una duplicazione da togliere**: l'editor **spegne** il tasto
(`CanMoveGroup`), il servizio **rifiuta** la mossa. Il primo è quello che si vede, il secondo è quello che
tiene se qualcuno arriva per un'altra strada — o se l'elenco è cambiato sotto mentre la pagina era ferma.

Due test nuovi in `AccDocumentServiceTests`. Verifica live su `/services/vsop/libb/editor`: l'Aerovia non ha
frecce, un gruppo solo le ha tutt'e due spente, con due gruppi il primo ha `↑` spenta e il secondo sale
davvero — e il passo successivo in su non c'è.

### J8 ✅ FATTA il 26 agosto — il catalogo punti leggeva **tre file su otto**

`GODRA` e `GIGUS` non mancavano: stavano in `NAVAIDS/ESTERNI.fix`, che non scaricavamo. La configurazione
elencava **tre** file a mano (`itfix.fix`, `itvor.vor`, `itndb.ndb`) mentre `ITALY.isc` ne cita **otto** —
`ESTERNI.fix`, `MIL.fix`, `APT.fix`, `VFR_NASCOSTI.fix`, `secsi.fix` oltre ai tre.

Ora `AuroraNavaidSource` legge l'elenco dall'indice, **stessa regola già usata per i file di settore**
(`AuroraSectorShapeProvider`): quali file leggere lo dice Aurora, non una lista scritta da noi.

Misurato sui file veri del repo sectorfile:

```
nomi in catalogo   1385 → 3732   (+2347)
GODRA / GIGUS      assenti → presenti, con le coordinate
LIMM_WS2/WS5/ES2   3 punti irrisolti a testa → ZERO
```

⚠️ Gli irrisolti erano **tre**, non due: c'era anche `GEMLA`. Cercarli a uno a uno avrebbe chiuso metà del
buco — la lista scritta a mano era il difetto, non i due nomi.

⚠️ **L'ordine di lettura non è estetico**: a parità di nome il catalogo tiene la **prima** occorrenza e con
essa la natura del punto, quindi VOR e NDB si leggono **prima** dei fix. Seguendo l'ordine dell'indice, un
omonimo cambierebbe natura ogni volta che qualcuno riordina `ITALY.isc`.

⚠️ I tre percorsi in configurazione restano come **ripiego**, per quando l'indice non risponde: un catalogo
ridotto è meglio di nessun catalogo (e nessun catalogo vuol dire **nessun poligono di settore**, perché i
vertici per nome non si risolvono più).

⚠️ Il catalogo dei punti serve anche ai **suggerimenti dei CoP** e alla completion delle SID: da qui in avanti
quei campi non segnano più come inesistente un punto d'oltreconfine scritto giusto.

Test: `NavaidIndiceTests` (7).

### J9 ✅ CHIUSA — le sezioni si riordinano anche **trascinandole** nel menu Navigazione

Richiesta del committente, 26 agosto: «se nell'editor le sezioni si potessero spostare anche trascinando nel
pannello Navigazione… per ora fallo per la vIPI di ACC, di avvicinamento e la vLOA».
[Carta](feature/2026-08-26-riordino-sezioni-trascinando.md).

**Perché lì.** Le frecce di J6 stanno sulla card della sezione, in una pagina alta migliaia di pixel: portare
*Validità e revisione* in cima sono otto pressioni, e a ogni pressione la sezione esce dallo schermo. Il
menu-sezioni è l'unico posto dove l'ordine si vede **tutto insieme**. Le frecce restano — sono la strada da
tastiera, e il trascinamento HTML5 non esiste sul tocco.

**La regola è una sola**: *la sezione lasciata prende il posto di quella su cui la si lascia*. Dalla stessa
frase escono due riferimenti diversi secondo il verso, e il conto lo fa una funzione pura
(`SectionOrdering.TryDropOnto`); su fratelli **adiacenti** dà esattamente l'esito della freccia.

Il motore serviva nuovo, perché lo scambio `±1` non salta N posti:
`MoveSectionBeforeAsync(sezione, prima-di?)` reinserisce e **rinumera il gruppo**.

⚠️ **Il vincolo «solo dentro il suo gruppo» sta nel MOTORE, non nella UI**: il riferimento dev'essere un
**fratello**, altrimenti la mossa non avviene. Non è ridondanza — è ciò che rende impossibile trasformare un
riordino in una **riparentazione silenziosa**, che cambierebbe il significato di una sezione e non la sua
posizione.

⚠️ **`draggable="false"` va scritto esplicitamente**: un `<a href>` nasce trascinabile per conto suo, e senza
quell'attributo la voce del pannello Release si lascia prendere per poi non andare da nessuna parte.

⚠️ **Il trascinamento è opt-in dell'host** (`EditorToc.OnReorder`): non passarlo lascia il pannello identico a
prima, **senza gestori registrati sul circuito**. È così che l'editor aeroporto — che di sezioni-documento
non ne ha — resta fuori senza una condizione dedicata, e fuori dalla modifica l'indice resta un indice.

15 test nuovi (5 puri, 2 sul repository, 8 bUnit sul pannello), suite intera verde, Release **0 avvisi** sui
due TFM. Verifica live sulle **tre famiglie** con browser vero: ACC `/services/vsop/libb/editor`, APP
`?app=LIBG_APP`, vLOA `?acc=LDZO` — lo spostamento persiste al ricarico e le pill `↑2 ↓1` si aggiornano.
Le due prove che i test non danno: trascinata una sezione **fra due blocchi** della vIPI ACC il menu è
identico prima e dopo, e **l'anteprima bozza** della vLOA rende l'ordine nuovo.

**Non fa** (deciso, non dimenticato): i **blocchi** ACC non si trascinano — nel menu sono intestazioni, e il
loro riordino ha la regola propria dell'Aerovia in testa (J7); le **sotto-sezioni** nemmeno, il menu mostra
solo il primo livello.

## K. La vIPI d'aeroporto entra nel catalogo — 26 agosto 2026, ramo `aeroporto-a-sezioni`

Ramo aperto **da `identita-settori`** (non da `main`), **non fuso**. Una voce sola, chiusa. Carta:
[l'aeroporto entra nel catalogo](feature/2026-08-26-aeroporto-a-sezioni.md).

Richiesta del committente: «rendere la struttura del documento d'aeroporto uguale a quella degli altri,
compreso il meccanismo di riorganizzazione».

**Non mancava un tasto: mancava il documento.** Il documento d'aeroporto era una **proiezione cotta** —
`RebuildDocumentAsync` riconosceva le sezioni **per titolo**, le cancellava e le riscriveva a ogni
rigenerazione, con chiavi **casuali** (`BlockSection.Airport` non ha una chiave di catalogo, e il builder
ricadeva su `SectionKeys.NewCustom()`). Ordine, «nascondi», sotto-sezioni e Live/Frozen stanno **sulla
sezione**, e la sezione veniva distrutta: per questo l'aeroporto era l'unica famiglia che non li aveva.

Ora ha un profilo suo nel catalogo (otto chiavi), l'editor monta `DocumentSectionsEditor` e il viewer itera
`_view.Sections`. Le sezioni fisse sono **ancore senza corpo**: il contenuto si deriva a view-time dalle
tabelle del profilo e si **congela** alla release, come per l'APP.

⚠️ **Nessuna migrazione**: non cambia lo schema, cambia chi scrive. Le diciassette in coda restano diciassette.

⚠️ **Un passo d'avvio nuovo**: `ReconcileAirportSectionKeysAsync`, che porta i documenti già scritti sulle
chiavi del catalogo e **trasloca** le sezioni libere dalla tabella `AirportExtraSection` dentro il documento.
Idempotente, lo scrive nei log («Riconciliate N sezioni d'aeroporto sulle chiavi del catalogo»). Gira **prima**
di `AddMissingCatalogSectionsAsync`, che ora copre anche gli aeroporti.

⚠️ **`AirportExtraSection` non si droppa in questo giro.** Le migrazioni girano all'avvio **prima** delle
riconciliazioni: una migrazione che la cancellasse porterebbe via il contenuto un istante prima che il
trasloco lo sposti. Nessuno ci scrive più; si toglie un rilascio dopo. **È l'unica voce che questa sezione
lascia in eredità.**

⚠️ **Due conseguenze volute, da dire al committente:**
1. L'editor aeroporto adotta **bozza + lock** (✎Modifica): obbligato, perché ogni mutazione del motore
   condiviso passa da `IEditingService`, che pretende il lock. Cade la scelta di luglio.
2. La pagina pubblica **smette** di mostrare piste, frequenze e sezioni libere prese dal profilo **live**:
   d'ora in poi vede lo stato **pubblicato**. Il passaggio è morbido — chi ha già una release non ha un
   payload congelato per le chiavi nuove e continua a leggersi live finché non ripubblica.

Verifica live su LIBD guidando Edge: lock e bozza v2, riordino con le pill `↑1`/`↓1`, «nascondi», sezione
libera **in mezzo** alle fisse, ordine che tiene al ricarico, anteprima bozza coerente. **La prova che
conta**: pubblicato e poi cambiato il TORA di una pista, la pagina pubblica resta a 3000 e la bozza dice il
valore nuovo — la release congela davvero ciò che prima non era congelabile perché era cotto.

⚠️ **Una correzione, subito dopo** (carta §8-§9): la sezione METAR/TAF c'era nell'editor e **non** nella pagina
pubblica, perché il pubblico legge lo **snapshot di release** e quello — per ogni scalo non ancora ripubblicato
— è anteriore alla carta e non la conosce. Con lo stesso difetto, `transition` e `runways` uscivano come
tabelle generiche. Chiuso con `AirportLegacySections`, **una** mappa titolo→chiave con **due** lettori (la
riconciliazione d'avvio e il viewer), e con la regola generale: *una sezione **sempre live** non è mai parte
della verità di uno snapshot*. Verificato mettendo in piedi il codice **pre-carta** in un worktree su una copia
del DB **pre-migrazione**, accanto a quello nuovo: le due pagine coincidono. Restano tre differenze volute —
niente più due colonne affiancate (una griglia non si riordina), titoli in italiano, «Nota» sui callout.

### K1 — Le tre rifiniture della stessa notte (27 agosto, chiuse)

**1. Il meteo era sparito dalla pagina pubblica** (carta §8-§9). La pagina pubblica non legge il documento di
lavoro: legge lo **snapshot di release effettiva**, e quello — per ogni scalo non ancora ripubblicato — è
anteriore alla carta e non conosce la sezione `weather`. Prima il riquadro lo disegnava la **pagina**, fuori dal
documento: c'era sempre. Misurato: LIBC, LIBD, LIRN, LIPA senza meteo; il solo LIBR ce l'aveva perché
ripubblicato. Con lo stesso difetto `transition` e `runways` uscivano come **tabelle generiche**.

Chiuso con `AirportLegacySections`: **una** mappa titolo→chiave, **due** lettori (la riconciliazione d'avvio e
il viewer, perché gli snapshot non si riscrivono mai). E con la regola generale — *una sezione **sempre live**
non è mai parte della verità di uno snapshot*. Verificato mettendo in piedi il codice **pre-carta** in un
worktree su una copia del DB **pre-migrazione**, accanto a quello nuovo: le due pagine coincidono.

⚠️ **Tre differenze restano, volute**: niente più le due colonne affiancate (una griglia di due sezioni fisse
non si riordina, e il riordino era la richiesta), titoli in italiano (li dà il catalogo), «Nota» sui callout
(renderer condiviso).

**2. I pannelli che non sono sezioni erano larghi quanto la pagina** (carta §10). Quando
`DocumentSectionsEditor` monta l'indice è **lui** a possedere la griglia `.ed-layout`; un pannello reso *dopo*
la chiusura del componente finisce fuori. ⚠️ Verificato su tutti e quattro gli editor: **l'unico a farlo giusto
era l'ACC**, che la griglia se la costruisce da sé. Parametro nuovo `AfterSections`. Misurato: 996/996 a
1600px, 1536 in larghezza piena, 960 a griglia collassata.

**3. «Validità e revisione» porta tre campi fissi** (carta §11, `docs/spec/modello-dati.md` §9.32): ciclo AIRAC,
data e chi ha premuto Pubblica — nome, posizione staff, VID — in **tutti e quattro** i documenti. La scheda si
aggiunge **sopra** e il testo scritto a mano resta (`SectionBodySource.HostAndBlocks`, l'unica sezione con due
corpi). ⚠️ È **sempre live** per una ragione di ordine: il timbro parla della release, e la cattura frozen gira
*dentro* la creazione dello snapshot, quando quella release non esiste ancora.

**Due cose da sapere, nessuna delle due è un difetto:**

- 🟡 **Sulla vIPI ACC di Brindisi la sezione non si vede in pubblico**: `validity` e `operationaltechnique` sono
  **nascoste a mano** su entrambi i blocchi. Sono le sole due in tutto l'archivio (le altre otto vIPI e quattro
  vLOA le hanno visibili). Si riaprono dall'editor con un clic — **decisione del committente**, non l'ho
  toccata: nascondere una sezione è una scelta editoriale registrata.
- 🟡 **Dove c'era già la tabella a mano il ciclo AIRAC compare due volte**, una generata e una scritta. È la
  conseguenza annunciata della scelta «si aggiunge sopra»: sparisce cancellando quella riga.

⚠️ **Il `vipi.db` di sviluppo è stato migrato dal nuovo passo d'avvio** durante il lavoro (un `Vipi.Host` già in
esecuzione da `bin/Debug` ha ripreso i binari nuovi). `AirportExtraSections` è vuota e i tre «Remarks» di
LIBC/LIBD/LIBR sono dentro i documenti con 2/5/3 blocchi: **nulla perso**, ed è la stessa migrazione che girerà
al primo avvio in produzione. Backup pre-migrazione: `src/Vipi.Host/vipi.db.bak-pre-ripristino-shape-20260826`.

---

## L. I quattro documenti, un motore solo — 27 agosto 2026, **chiusa**

Carta ed esito: [`docs/refactor/14-quattro-documenti.md`](refactor/14-quattro-documenti.md). Tutto in `main`.
**Suite 5432 → 5714**, build 0 avvisi, verifica sul flusso reale con l'applicazione vera **quattro volte**.

Audit di **supervisione** chiesto dal committente: i quattro tipi di documento previsti da direttiva devono
condividere quanto più possibile. È il primo giro che ne guarda **quattro** — i doc 11 e 13 ne vedevano tre,
perché la vIPI d'aeroporto è entrata nel catalogo delle sezioni solo il 26.

**Il verdetto**: lo strato profondo rispettava già la direttiva (catalogo sezioni, `IReleaseTarget`, editor
di sezioni e pannello release condivisi). La divergenza si era ritirata **in alto** — il guscio delle pagine —
e **in basso**, le porte d'ingresso.

### L1 ✅ Che cosa è stato fatto

| | |
|---|---|
| La guardia «questa release è di questo documento» | sale nel servizio: la firma non si può soddisfare senza dirlo |
| Il ciclo AIRAC scritto due volte sulla vLOA | **difetto visibile**, chiuso |
| Lo snapshot di release | letto **una volta per pagina**: ACC LIBB 375 KB → 62,5 KB per render |
| `DocumentEditorShell` | un guscio solo per i quattro editor (erano 16 membri con lo stesso nome) |
| `DocumentSectionsView` | un ciclo solo per le sezioni dei viewer — porta la **vLOA sul catalogo** |
| Le sezioni alla nascita | le dice il catalogo; i due array `LiveKeys` divergenti spariscono |
| Un enum solo | `ManagedDocKind` fuso in `ReleaseTargetType` |
| L'aeroporto | editor **2180 → 1001** righe, viewer **594 → 464**, pagina **SSR statica** con due isole |
| `DocumentBirth` | la nascita del documento è una sola |
| `CurrentVersionId` | ha **un significato solo** (§3i) |

### L2 🔵 L'unica cosa che aspetta il committente: **ripubblicare le quattro vLOA**

⚠️ **La correzione del ciclo AIRAC non arriva al pubblico da sola.** La pagina pubblica legge lo **snapshot**
della release; la riconciliazione d'avvio corregge il *documento*. Sulla `LIBB ↔ LDZO` il timbro dice `2608` e
lo snapshot dice ancora `AIRAC 2607`.

**Gli snapshot non si riscrivono**, ed è una scelta: una release «congela davvero» (doc 10), e riscriverne il
payload a posteriori cambierebbe quel che un ciclo passato ha detto — un precedente peggiore del difetto. La
strada è **ripubblicare**, e il giro notturno `ImpactDriftUseCase` lo segnala già in «Da fare» col tasto
**Ripubblica**.

**Quattro clic.** La lista li indica da sola dopo il primo giro notturno.

### L3 ✅ Le due reti, che valgono più degli otto passi

- **`ParitaQuattroDocumentiTests`** — 40 prove che pongono ai **cinque profili** (l'ACC ne ha due) le stesse
  domande di comportamento. Il catalogo aveva già invarianti su tutti i profili, ed è per questo che non
  divergeva; per il **comportamento** non esisteva l'equivalente, e **ogni** divergenza di questo audit era
  passata attraverso una suite verde.
- **`NascitaDocumentoParitaTests`** — la stessa domanda a tutte e **quattro** le porte di nascita.

Chi aggiungerà un quinto documento le eredita: basta aggiungere il profilo, o il caso.

### L4 ⚠️ Le assunzioni cadute eseguendo — da leggere prima di rifare qualcosa di simile

1. **«Un componente, due modi» non si applica alle sezioni dell'aeroporto**, e non per pigrizia: lettura e
   scrittura hanno forme diverse *per una ragione* — la lettura è una proiezione già formattata perché
   dev'essere **serializzabile per il congelamento della release**. Il modello di `AppSeparations` vale dove
   lettura e scrittura sono la stessa riga.
2. **Per togliere il circuito a una pagina NON serve separarne la rotta**: il render mode si dichiara sul
   **componente**. Bastano le isole, e gli indirizzi pubblici non si toccano. ⚠️ I parametri che attraversano
   il confine SSR→interattivo devono **serializzare** (`HashSet` → array).
3. **`IReleaseTarget.EnsureDocumentIdAsync` non si può fare**: crea un **ciclo** di dipendenze in DI
   (descrittore → servizio della famiglia → `IReleaseRepository` → registro → descrittori).
4. **Una scelta che sembra da fare può essere il residuo di una già fatta.** `CurrentVersionId` sembrava una
   decisione fra due significati: il secondo lo teneva in vita **codice morto** (un congelamento SID
   dedicato, con quattro righe di test come unici chiamanti). Tolto quello, non c'era niente da decidere.

### L5 ⚠️ I difetti trovati **spostando**, non leggendo

Tutti e tre invisibili a chi legge il codice, perché le due metà stavano lontane:

- la conversione di una **regola pista** verso il dominio era scritta **due volte**, a quattrocento righe di
  distanza — una per il banco di prova, una per il salvataggio. Il banco poteva dire «vince la #2» e il
  pubblicato applicarne un'altra;
- il blocco delle **SID manuali** marcava la sezione `"SID"` invece di `"sids"`: «Salva tutto» la saltava
  **in silenzio** e restava per sempre fra le non salvate;
- **sette frasi in italiano cablato** (il banco di prova, gli avvisi SID) mai tradotte.

---

## M. I resti, chiusi in un giro — 27 agosto 2026, pomeriggio

Sette voci rimaste indietro perché piccole, o perché nessuno sapeva da dove prenderle: **C7c, C7a, C7b, C6,
H3, H1, E9**. Nessuna migrazione. Suite **5746** (net8 **2992**, net10 **2754**), build Release
`--no-incremental` **0 avvisi**. Sei commit, fusi in `main` (**`290b833`**) e ramo cancellato.

### M1 ✅ C7c — un ACC estero nasce con le aree SPENTE

Il default d'entità di `SpecialAreasEnabled` è `true` (giusto per i domestici) e nessun chiamante lo toccava:
il tappo storico `OptOutForeignAreasAsync` è **one-shot** e non copre chi nasce dopo. La regola sta ora in un
posto solo — **`Acc.NewForeign`** — usata dai tre siti di nascita (import confinanti, generazione vLOA, seed).
⚠️ L'upsert **non** rispegne un ACC che l'admin ha acceso a mano: c'è una prova apposta.

### M2 ✅ C7a — il regime di scrittura, quando non l'ha deciso nessuno, si vede

Due rilievi distinti in Diagnostica, perché sono due guasti distinti:

- **riga assente** (una `DELETE` sulla tabella): l'applicazione torna a «la sorgente scrive tutto» e il primo
  giro sovrascrive TA e piste messe a mano. La riga è **una sola** in tutto il database;
- **riga mai firmata con qualcosa di manuale**: è la storia di `ImportSids`, nato `false` su un DB già
  popolato — un import fermo da mesi è indistinguibile da una decisione.

⚠️ **Una policy tutta «da sorgente» e mai toccata NON è un rilievo**: è il default dichiarato del prodotto, e
mostrarlo a ogni apertura insegnerebbe solo a ignorare la diagnostica. `ImportPolicyInfo` porta ora
`RigaPresente` per distinguere i due casi.

### M3 ✅ C7b — le cancellazioni della Struttura entrano nel registro

ACC, aeroporto e settore uscivano muti mentre l'eliminazione di un **documento** ci finisce dal 22 agosto:
era il **buco 5** di quell'audit, chiuso solo per `SetParentAsync`. La riga si scrive **prima** della
cancellazione (dopo, callsign e ICAO non sono più leggibili) e con lo stesso vocabolario di
`EfDeletionRepository` — le due strade cancellano le stesse cose e devono raccontarle uguale.

### M4 ✅ C6 — la chiave che si sposta si RIPUNTA

**Metà del problema era già caduta da sé**: dal 26 agosto la **rinomina** riscrive `DocReleases.TargetKey`,
perché quella chiave non è storia ma un **puntatore**. Restava l'altra metà — un primario che cambia, un
settore riparentato — e si chiude con la stessa regola: `IReleaseRepository.RepointKeyAsync` sposta release
e incarichi sotto la chiave viva, e scrive nel registro (attore `0` = giro notturno).

⚠️ **Rifiuta e ritorna 0 se la chiave nuova ha già delle release**: due storie di pubblicazione non si
fondono da sole — i `VersionNumber` si scontrerebbero con l'indice unico — e quale sia quella buona è una
decisione, non un calcolo. In quel caso resta `ReleaseKeyMoved` e la decide una persona. Il giro notturno
ripara **prima** di segnalare, e il conteggio delle ripuntate esce nel log.

### M5 ✅ H3 — la testata copre il respiro VERO del riquadro

`.st-head.sticky` sborda coi margini negativi per coprire il respiro laterale, e il valore era una **copia**
del padding del `.wrap` generico (clamp 24/3.5vw/64). Le pagine admin però sono `.wrap.struct`, che quel
respiro lo **stringe** (clamp 16/2vw/32): la testata sbordava più di quanto dovesse coprire. 56 contro 32 a
1600 = **24px**, e a ogni larghezza la stessa differenza fra due clamp. Ora il respiro è una variabile
(`--wrap-pad`) dichiarata da chi ce l'ha e letta da chi lo copre. Misurato a schermo: sforo **0** a 1600,
1366, 1280 e 1024.

### M6 ✅ H1 — anche i riquadri scelgono il loro scaglione misurandosi

Diciassette `@media` di editor e admin misuravano la **finestra**, mentre lo zoom di questo prodotto è `zoom`
sull'`<html>`. Misurato: a 1600px e zoom 1.8 il riquadro ha **889** unità di layout — sotto la soglia di 900 —
e `matchMedia('(max-width:900px)')` risponde ancora **NO**.

⚠️ **La `@container` del viewer qui non si poteva**: `container-type` porta con sé `contain:layout`, che rende
il riquadro contenitore anche per i `position:fixed`. Sulle pagine admin il **`DeleteDialog`** è un
`.del-card` fisso centrato sullo SCHERMO e vive **dentro la riga di una tabella**: contenendo il `.wrap`
sarebbe finito centrato su un riquadro alto migliaia di pixel. Quindi la cura è quella della **topbar**: lo
scaglione lo sceglie il JS misurando (`vipiFitPanes`, classi cumulative `pw-1200/1180/1080/900/760`).

⚠️ **E un osservatore all'avvio non basta**: su una pagina `InteractiveServer` il riquadro che si vede non è
quello del `DOMContentLoaded` — Blazor lo rifà quando il circuito parte, e il nuovo nasce senza osservatore.
A finestra 860px le colonne restavano affiancate mentre a zoom 1.8 si impilavano: a rimisurare era
l'osservatore del riquadro **vecchio**.

Verificato su undici pagine, quattro zoom e due finestre strette. **`.tk-board` non è stato esercitato**: nel
database di sviluppo l'utente non ha incarichi propri.

### M7 ✅ E9 — la corsa si riproduce, e la prima operazione ha un nome

**`EditAuthorizationService.CanEditAnythingAsync → EfEditGrantRepository.HasAnyGrantAsync`**: la domanda «hai
qualcosa da modificare?» del layout, quella che pagano i soli utenti **collegati e non admin**. La pagina è la
**seconda**, ed è quella che muore. Non è più una deduzione: è la fotografia del rilevatore, in un test.

⚠️ **Perché i tre tentativi precedenti non scattavano**: l'intercettore era registrato **in DI**, e in questo
assetto si monta sulle **OPZIONI** del contesto (`AddInterceptors`). Il test ridichiara il `DbContext` con
dentro un **ritardo per comando**, così la finestra è larga come quella di un database remoto — che è la
differenza vera fra questa macchina e `atc.it.ivao.aero`.

⚠️ **Le due guardie di oggi bastano ognuna da sola**, provato spegnendole una per volta: col layout che
conclude prima del render la pagina regge anche sul contesto della richiesta; con lo scope proprio regge
anche se il layout lascia qualcosa in volo. **Chi ne togliesse una sola non vedrebbe rompersi niente** — ed è
la trappola della prossima pulizia.

**Resta fuori dal codice**: la conferma dal vivo in produzione la può dare solo un socio senza incarichi che
apra `/services/vsop/{acc}` dopo un riavvio a freddo. Da admin non prova niente.

### M8 — quel che NON è stato toccato, e perché

Restano su `@media` le soglie delle pagine **pubbliche** (`.wrap table` a 900, `.wi` a 720, `.area-wrap` a
600, `.validity-stamp` a 560, il rail del viewer a 1500): lì il perimetro dichiarato parte da 375px e il
layout di lettura è già governato dalla `@container`. Non è una dimenticanza: è il confine del giro.


---

## N. Il trascinamento rotto e le aree regolamentate — 27 agosto 2026, sera

Ramo **`riordino-e-aree`**, **spinto e non ancora fuso in `main`**. Suite **5789** (+17 test ×2 TFM), build
Release `--no-incremental` **0 avvisi**, **nessuna migrazione**.

| commit | cosa |
|---|---|
| `ff8ec29` | il trascinamento nel menu Navigazione non concedeva il rilascio |
| `6d0c309` | le aree regolamentate sono una mappa sola con le chip |
| *(docs)* | carte, indice, HANDOFF, e la Guida in-app — che mostrava i tag escapati (N5) |

### N1 ✅ Il riordino trascinando non ha mai funzionato col mouse

Segnalato dal committente. Vero **dal primo giorno**, su tutte e tre le famiglie, e per giunta con la carta
del 26 che lo dava per verificato.

`@ondragover:preventDefault="true"` sulla voce **non faceva niente**. Blazor installa il proprio listener
globale per un evento soltanto quando un componente vi registra un **gestore**, e per `dragover` non ce
n'era nessuno — solo il modificatore, memorizzato e mai consultato. Nel modello HTML5 il rilascio va
**richiesto** con `preventDefault` sul `dragover` del bersaglio: senza, il browser chiude il gesto da sé,
**senza errori e senza segni**. Misurato con spie in cattura sull'editor ACC:

```
dragstart ✓   dragenter ✓ (la voce si illuminava DAVVERO)
dragover  ✓   ma defaultPrevented = false
drop      ✗   mai        → dragend, gesto annullato
```

Cura: **`wireTocDrop`** in `vipi-ui.js`, un listener in cattura installato una volta (stessa forma di
`wireBlockMenu`). Scartata la strada in-framework — un gestore `@ondragover` finto — perché `dragover` scatta
a ogni movimento del mouse: un giro sul circuito e un re-render del menu una decina di volte al secondo,
proprio durante il gesto.

⚠️ **La lezione, che vale oltre questo difetto.** Né gli otto test bUnit né la verifica live guardavano il
pezzo rotto: i primi chiamano `DragStart()` e poi `Drop()` invocando i gestori **direttamente**, la seconda
**sintetizzava** gli eventi con `new DragEvent(...)` — dispacciando da sé proprio il `drop` che nella realtà
non arrivava. **Un gesto del browser si prova col browser che lo fa**: `Input.setInterceptDrags` +
`Input.dispatchDragEvent` (CDP), headful. Script e lezione: `.claude/skills/verifica-live/drag-verifica.js`
e §4-bis della skill. Dettaglio completo in
[`feature/2026-08-26-riordino-sezioni-trascinando.md`](feature/2026-08-26-riordino-sezioni-trascinando.md) §8.

Test nuovo sul contratto fra il selettore del JS e il markup del componente, **provato per mutazione**
(tolta la chiamata a `wireTocDrop()` → rosso; rinominata la classe nel solo JS → rosso).

### N2 ✅ Aree regolamentate: una mappa sola, con le chip

Richiesta del committente. Carta:
[`feature/2026-08-27-aree-regolamentate-una-mappa.md`](feature/2026-08-27-aree-regolamentate-una-mappa.md).

C'era un `<details>` per area **con la propria mappina Leaflet**: 105 contenitori mappa su LIRR, 69 su LIBB.
Ora una mappa (2D/3D) riusata dall'AoR, una chip per area colorata per tipo, i preset R/D/P/TSA/TRA, e sotto
le descrizioni delle **sole aree accese**, chiuse, col conteggio.

Nessun motore di mappa nuovo: la traduzione area→«settore» è `RegulatedAreasMap` (pura). ⚠️ Chiave = **id
IVAO** (i nomi hanno spazi e finirebbero in `[data-sec="…"]`); ⚠️ le quote delle aree sono in **piedi** e il
3D estrude su FL (`AorFlBand.Normalize`); ⚠️ i preset per tipo devono stare **dentro `AccAorView.Configs`**,
perché la fila di tasti la disegna `AccAor` leggendo quello — costruirli accanto è come non averli, ed è
successo (visto solo alla prima prova dal vivo, con 105 chip e nessun filtro).

**Niente migrazioni, niente ripubblicazione**: le aree sono sempre **live** dai cataloghi, gli id li porta il
documento — le release già scritte mostrano la forma nuova appena il codice è in linea.

Tolto per propagazione: `.area-map` e `.area-noshape`, `PRINT_AREA_MAP_H` e il ramo `isArea` di `resizeMaps`
in `vipi-ui.js`. ⚠️ Trovato per strada: `.area-wrap`/`.area-svg`/`.area-alt` erano definite **due volte** nel
foglio, a novecento righe di distanza, e vinceva la seconda copia. Restano — le usa `AreaMapBlock` — ma ora
in un posto solo.

⚠️ **La vLOA non c'entra**: lì `regulated` è **Editorial** nel catalogo (un paragrafo di prosa sul
coordinamento delle aree militari transfrontaliere), non l'elenco delle aree.

### N3 ✅ CHIUSA il 27 agosto — la basemap CARTO voleva una API key, ora è Esri

**Trovato guardando gli screenshot della verifica, non cercandolo.** Le tessere di
`basemaps.cartocdn.com/light_all` arrivavano stampigliate **«API KEY REQUIRED — carto.com/basemaps/apikey»**:
CARTO ha chiuso il fondo anonimo. Riguardava **tutte le mappe del prodotto** — AoR 2D, visore 3D, aree
regolamentate — ed era **già così in produzione**. ⚠️ Il fondo si **caricava**, quindi né il ritentatore né lo
spazzino potevano accorgersene: guardano la tessera che non arriva, e questa arrivava con la scritta sopra.

**Fatto**: il fondo è **Esri «Light Gray Canvas»** (`server.arcgisonline.com`, host che il prodotto interroga
già per il rilievo delle minime), due fogli — base muto più etichette. Toccati `addBasemap` in `vipi-aor.js`,
il pavimento del 3D in `vipi-aor3d.js`, la CSP e le note di terzi. ⚠️ **`vipi-mva.js` non c'entrava**: le
minime avevano già lasciato Positron per il rilievo. Carta:
[`feature/2026-08-27-basemap-esri.md`](feature/2026-08-27-basemap-esri.md).

🔵 **Resta aperta la categoria, non il caso.** Esri ci serve a titolo gratuito e senza contratto, come
CARTO fino a ieri: il giorno in cui chiude siamo di nuovo qui. L'unica strada che non si ripresenta sono le
**tessere nostre** — un PMTiles della sola Italia servito da noi, con la CSP che torna `'self'`. È lavoro
vero (file da produrre e ospitare, un plugin al posto di `L.tileLayer`), quindi è una **decisione**, non un
residuo.

⚠️ **Trovato per strada e chiuso**: la CSP `img-src` elencava **solo** CARTO, mentre le minime chiedono
tessere a `server.arcgisonline.com` e `*.tile.opentopomap.org` — due host fuori dalla politica da giorni. Non
si è visto perché l'intestazione è **Report-Only**: segnala e non blocca. Il giorno che diventerà una CSP
vera, una riga così **spegne le mappe**.

### N4 🔵 Piccolezza di contenuto, da decidere

Nei dati IVAO **`99999 ft` fa da «illimitato»**, e si stampa così com'è (`INDIA5 31000 ft – 99999 ft`), come
faceva l'elenco di prima. Renderlo `UNL` a schermo è una riga, ma è una scelta editoriale.

### N5 ✅ La Guida in-app mostrava i tag e gli apostrofi

Trovato aggiungendo alla Guida la sezione «Leggere le aree regolamentate»: il corpo di ogni sezione è reso con
`@((MarkupString)…)`, cioè **come HTML**, ma **novantasei tag erano scritti escapati** (`&lt;b&gt;`) e
**trentasette apostrofi raddoppiati** (`l''editor`) — abitudini prese da altri contesti di quoting, che in una
stringa verbatim C# resa come markup escono **letterali**. A schermo si leggeva «Le sezioni `<b>`derivate`</b>`»
e «L''editor», in **cinque** sezioni della guida utente (`editor-app`, `editor-vloa`, `admin-sorgenti`,
`incarichi`, `admin-incarichi`), da mesi.

⚠️ **Non lo vedeva nessuno perché la Guida è testo**: nessun test la esercitava, nessuna asserzione poteva
accorgersene, e chi la scrive guarda il sorgente, non la pagina. Ora `GuidaMarkupTests` guarda il sorgente al
posto suo — due prove, una per difetto. `&amp;` e `&nbsp;` restano leciti: sono entità che si **vogliono**
vedere.

Aggiunta, nella parte di **consultazione**, la sezione **«Leggere le aree regolamentate»** (pastiglie, filtri
per tipo, colori, 3D, e che sulla carta finiscono solo le aree accese), con la sua voce in
`GuideSearchCatalog` perché emerga dalla ricerca globale.


---

## O. L'audit delle prestazioni — 27 agosto 2026, sera tardi

✅ **Chiuso lo stesso giorno**, dieci commit sul ramo `prestazioni`, **fuso in `main` `8e5f640`** insieme a
`riordino-e-aree`; entrambi i rami cancellati, locale e su origin. Carta completa con tutte le misure:
[`docs/history/audit-2026-08-27-prestazioni.md`](history/audit-2026-08-27-prestazioni.md).

Revisione della responsività **tenendo conto dell'ambiente di produzione** — Plesk + Passenger, una sola
istanza senza backplane, MariaDB sulla stessa macchina, Cloudflare davanti, aggiornamento via FTP.

```
prima visita   336 192  ->  113 052 byte     -66%
avvio            465    ->      153 query,  e zero UPDATE inutili
```

Lo **stato stazionario era già sano** (trenta richieste concorrenti: p50 16 ms, p90 34 ms). Il costo stava
nei byte spediti, nell'avvio, e in ciò che impediva a qualunque cache di aiutare.

⚠️ **Il filo: quattro difetti su otto sono default del framework mai scritti** — il livello di compressione
(`Fastest`, che per Brotli è la qualità 1), il livello di log (`Information`, che per EF è il testo di ogni
query su disco), un `@rendermode` su una pagina senza comandi, un `DateTime.UtcNow` dove serviva il timbro
della sorgente. Nessuno somiglia a un difetto: non danno errore, e tre su quattro rendono la configurazione
*più* ricca a leggerla.

⚠️ **Due interventi pianificati sono stati SCARTATI SU MISURA**, e la misura è finita **nel codice** perché
nessuno li rifaccia partendo dalla stessa ipotesi:
- **ReadyToRun** — +29 MB di pacchetto su un deploy solo-FTP per un 2% dentro il rumore. Il cronometro
  d'avvio aggiunto al suo posto dice perché: **1 172 ms su ~1 300 sono database**, non compilazione.
- **Deduplicare i poligoni AoR** — guardava i byte **grezzi**. Compressi, arrotondare le coordinate e
  togliere i `&quot;` fa uscire **più** byte: le ripetizioni sono ciò che Brotli mangia meglio. La copia
  costa 770 B.

### O1 🟢 APERTO — `ListOrphansAsync`: ~150 query per otto orfani

La Struttura settori è scesa da **173 a 167** query soltanto: l'accorpamento è parziale, e il grosso sta lì.
Per ogni orfano `EfOrphanSectorRepository.ListOrphansAsync` cerca i documenti che lo citano e chi ne blocca
la rimozione — due chiamate da una decina di query ciascuna. Con cinquanta orfani diventerebbero un migliaio.

⚠️ **Non è stato accorpato di proposito**: è il percorso che decide se un settore si può eliminare, e
riscrivere due metodi in versione massiva è un lavoro con i suoi test e la sua verifica, non una cosa da fare
di sfuggita mentre si sistema il peso delle pagine. È una pagina di sola amministrazione e a caldo costa
trenta millisecondi: il conto non è urgente, ed è scritto **accanto al ciclo**.

**Blocco:** nessuno. **Dove:** `EfOrphanSectorRepository.RigaAsync`.

### O2 🔴 APERTO — la Cache Rule su Cloudflare

Dal 27 agosto le letture **anonime** dei documenti pubblici escono dichiarando `public, max-age=60` e
`Vary: Accept-Encoding, Cookie`, senza cookie antiforgery. Il browser di chi ricarica o torna indietro riusa
già la pagina da solo.

Quel che **non** succede da solo è la cache al bordo: Cloudflare, di suo, non tiene le pagine HTML. Serve una
**Cache Rule** (`URI Path starts with /services/` → *Eligible for cache* → **Respect origin TTL**), scritta
per esteso in [`deploy/atc-ivao/LEGGIMI-DEPLOY.md`](../deploy/atc-ivao/LEGGIMI-DEPLOY.md).

⚠️ **«Respect origin TTL» e non un numero scritto a mano**: la distinzione fra ciò che si può tenere e ciò
che non si può la fa l'applicazione — sette clausole, una per una nel codice — e una durata imposta dal
pannello ci passerebbe sopra.

**Blocco:** committente (accesso al pannello Cloudflare).

### O3 🔴 APERTO — due impostazioni del Plesk da verificare

Nessuna delle due è codice, e nessuna delle due si vede da qui.

- **`passenger_min_instances ≥ 1`**: senza, Passenger spegne il processo per inattività. Il primo visitatore
  dopo la pausa paga ~1,3 s di avvio — e, cosa peggiore, **a processo spento i dodici hosted service non
  girano**, polling ATC compreso.
- **`proxy_read_timeout ≥ 100s`** nelle *direttive nginx aggiuntive* del sito: col default di 60 s il
  circuito Blazor **cade da solo ogni minuto** e l'utente vede «Tentativo di riconnessione». ⚠️ Il file
  `deploy/atc-ivao/nginx-vipi.conf` ce l'ha già scritto ma **su quel server non lo carica nessuno**: è
  riferimento per un deploy systemd+nginx.

**Blocco:** committente (accesso al pannello Plesk).

## P. Il ramo `basemap-esri` — 27 agosto 2026, notte

Due lavori usciti dalla stessa sera, **spinti e non fusi**, entrambi nati da una segnalazione del committente
e non da un piano. Stanno sullo stesso ramo solo per questo: **si possono scorporare**.

### P1 ✅ Il fondo delle mappe non è più CARTO (`95e4227`)

Vedi **§N3**, chiusa, e la carta [`feature/2026-08-27-basemap-esri.md`](feature/2026-08-27-basemap-esri.md).

🔵 **Quel che resta non è un residuo ma una decisione**: Esri ci serve a titolo gratuito e senza contratto,
come CARTO fino a ieri. L'unica strada che non si ripresenta sono le **tessere nostre** — un PMTiles della
sola Italia servito da noi, con la CSP che torna `'self'`. Da valutare a mente fredda, non sotto un fondo che
si è appena rotto.

🔵 **Piccolezza rimasta aperta, ed è di prima**: il pavimento del visore **3D non mostra attribuzione**. Non
è una mappa Leaflet ma una texture su un piano Three.js, e non ce l'aveva neanche con CARTO. Dove scrivere il
credito in un visore che si ruota è una scelta di interfaccia, non una riga di codice.

### P2 ✅ Le chip che non facevano niente (`1c15f81`)

METAR/TAF e il selettore di pista delle SID sulla vIPI d'aeroporto. Carta:
[`feature/2026-08-27-chip-morte-pagina-statica.md`](feature/2026-08-27-chip-morte-pagina-statica.md).

⚠️ **La regola che resta, e vale oltre queste due sezioni**: *uno stato che cambia deve vivere **dentro**
l'isola che lo cambia; un genitore statico può solo **seminarlo***. Da tenere presente ogni volta che una
pagina passa da `InteractiveServer` a SSR statica: non basta promuovere i componenti a isola, bisogna
**spostare lo stato con loro**.

⚠️ **E una lacuna della rete che non si chiude scrivendo un test**: **bUnit ignora i render mode**, cioè monta
ogni componente come se fosse interattivo. La prova del meteo aggiunta in questo giro **sarebbe passata anche
prima della cura**. Il render mode è un fatto dell'**hosting**, non del componente: l'unica rete che lo vede è
il browser vero (skill `verifica-live`).

**Propagazione già fatta**: chiusura transitiva dei componenti raggiunti dalle undici pagine pubbliche
statiche, fermandosi alle isole. Nessun altro comando muto; i quattro candidati (`AppConfigurations`,
`AppFrequencies`, `AppSeparations`, `AppVfr`) hanno i gestori dentro il ramo `Editing`, che nel viewer è
`false`.

### P3 ✅ CHIUSO — fuso il 28 agosto 2026

Fuso in main insieme ai documenti bilingue (`0c81e61`), in un colpo solo. Ramo locale cancellato, e il
**28 agosto** — spinto `main` — anche `origin/basemap-esri`: era interamente contenuto in `main`
(20 commit dietro, 0 avanti), quindi non c'era più niente che vivesse solo lì.

⚠️ **La regola, per la prossima volta**: un ramo remoto si cancella quando ciò che porta è **anche
altrove**, e la verifica è `git rev-list --count main..origin/<ramo>` = 0. Fino a quel momento è l'unica
copia sul server, e cancellarlo è perdere lavoro senza un errore che lo dica.

---

## Q. I documenti bilingue — 28 agosto 2026

Carta: [feature/2026-08-27-documenti-bilingue.md](feature/2026-08-27-documenti-bilingue.md).
✅ **Tutte e dieci le slice chiuse e fuse in main** (`0c81e61`), suite 6379 verdi.

### Q1 🔴 APERTO — i termini di ritenzione dati del piano gratuito Azure

Da leggere prima di considerare la funzione «in produzione». ⚠️ La ritenzione sul piano gratuito **non è**
quella del piano a pagamento, e DeepL ha appena dimostrato quanto i termini cambino (da 500k/mese a
1M una tantum, mentre si scriveva la carta).

### Q2 🔴 APERTO — la domanda a IVAO HQ sul trattamento esterno

Mandare i testi a un terzo è trattamento esterno. I documenti sono pubblici, e VID e nomi non escono per
costruzione, ma **HQ ha già posto un vincolo contrattuale sui PDF**: meglio chiedere prima che dopo.

### Q3 ✅ CHIUSA — chi cura il glossario di fraseologia: **tutti gli admin**

⚠️ **Il 28 agosto 2026 ne è nato il primo pezzo, e non per scelta ma per un difetto visto a schermo**: la
macchina rendeva «Piste» con *Slopes* e «Quote di transizione» con *Transition Dimensions*.
`Vipi.Application/Translation/TitoliUfficiali.cs` mette in memoria come **Human** i 26 titoli del profilo
militare, presi dagli originali inglesi dei quindici SOP (carta vSOP militari §2 e §8b). Un test pretende
che ogni titolo del profilo abbia il suo originale, quindi la lista non può restare indietro.

Il 28 agosto, caricando il primo SOP vero, si è aggiunta la **seconda lista**: `Termini`, le intestazioni
delle tabelle. Erano sbagliate **tutte** — «Pista» → *Track*, «Piazzale» → *Forecourt*, «Stand» → *Booth*,
«Rilevamento» → *Detection*, «Quota» → *Share*, «Ente» → *Institution* — e sono le colonne che un
controllore legge per trovare il dato.

Quello **non chiudeva la domanda**: copriva i segmenti INTERI (un titolo, una cella di tabella), non la
fraseologia dentro le frasi — «riporta sottovento» era il caso che nessun elenco risolveva, e nella prosa
di LIPI restavano cose come «the cocking and disarming positions». Ma diceva dove va messo ciò che si
decide, e mostrava che il meccanismo (voce umana in memoria, mai toccata dalla macchina) funziona.

#### ✅ Il 28 agosto sera: il meccanismo dentro le frasi, e la lista non è più nel codice

**Il pezzo tecnico è chiuso.** `GlossarioFraseologia` + tabella `GlossaryTerms` + pagina
`/services/vsop/admin/glossary`. Come funziona, in una riga: prima di spedire, il protettore mette la
formula italiana in un segnaposto `<g id="0" translate="no">riporta sottovento</g>` e tiene da parte la
resa inglese; al ritorno rimette **la nostra**, qualunque cosa il motore abbia fatto lì dentro.

⚠️ **I due segnaposto hanno contratti OPPOSTI, ed è tutta la faccenda.** Un `<x>` deve tornare *identico*
— se torna diverso il motore ha rovinato un identificatore e la frase si butta. Un `<g>` torna *sempre*
diverso, perché dentro è partito l'italiano e la resa inglese non c'è mai stata: se passasse dal confronto
degli identificatori, ogni frase con dentro una formula finirebbe fra gli scartati, e il glossario
**spegnerebbe** la traduzione invece di migliorarla. La lettera del tag è il solo posto in cui la
differenza si vede.

⚠️ **Il glossario passa PRIMA delle regole sugli identificatori**, non dopo. Dopo, una formula che ne
contenesse uno non scatterebbe più — a quel punto il callsign è un tag, non più le lettere che la voce
cerca — e non scatterebbe in silenzio. La simmetria è il rifiuto `ContieneIdentificatore` nella pagina di
cura: siccome passa per primo, una voce con dentro un identificatore se lo *inghiottirebbe*, e quello
finirebbe cablato nella resa, uguale in ogni documento che contiene la formula.

⚠️ **La funzione nativa dei motori è nel TESTO, non in un ramo di codice.** `translate="no"` lo onora Azure
da sé in modalità marcatura, e a DeepL si passa `ignore_tags: ["x","g"]`: la formula non si traduce, quindi
non si paga e il motore non ci può spostare dentro le parole di contorno. Un motore che lo ignorasse non
romperebbe niente — tradurrebbe l'italiano per niente, e il ripristino butterebbe via la sua fatica.

⚠️ **E c'era un difetto che il glossario avrebbe introdotto**: una cella che è *tutta* una formula non parte
(del protetto non resta che il segnaposto), e prima il giro ne scriveva in memoria «il testo così com'è» —
cioè il sorgente ricopiato, che con soli identificatori dentro era giusto. Con una formula avrebbe scritto
l'**italiano spacciandolo per inglese**, come voce definitiva che nessun giro riprova. Adesso scrive il
protetto *ripristinato*.

⚠️ **Il seme non è una regola che il codice fa rispettare.** Le voci di partenza si scrivono **solo se il
glossario è vuoto**: da quando lo tocca una persona, il codice non ci scrive più. Con la condizione ovvia
(«questa voce manca») una formula *tolta* dal curatore tornerebbe al riavvio dopo, per sempre, senza che si
capisca da dove.

⚠️ **Una voce nuova NON tocca le frasi già tradotte**, perché il giro traduce solo ciò che manca — e senza
dirlo, chi la scrive rileggerebbe il documento trovandolo identico. La pagina conta le automatiche che
contengono la formula e offre di **buttarle**, così il giro dopo le rifà: mai quelle riviste da una persona,
e ⚠️ **i caratteri si ripagano al motore**.

**Le voci di partenza sono 24, e sono di due specie dichiarate.** Tre gruppi vengono da difetti **visti**:
«riporta sottovento» → *bring it back downwind* (Azure, 27 ago, carta §5), «il campo» → *the camp* e
«armamento e disarmo» → *the cocking and disarming positions* (LIPI, 28 ago, §R2 qui sotto — che con questo
si chiude). Gli altri sono fraseologia standard, messi perché la lista non nasca vuota: di quelli non
sappiamo che cosa facesse la macchina, e chi cura il glossario è libero di toglierli.

#### ✅ Chi lo cura: TUTTI GLI ADMIN — deciso dal committente il 28 agosto 2026

**La domanda è chiusa, e la risposta è un ruolo, non un nome.** Cura il glossario chiunque sia admin: per la
decisione già presa sui codici staff (`^IT-[A-Z0-9]+$`, vedi §staff), **tutto lo staff di divisione lo è**.

⚠️ **Non serviva codice.** La pagina è già dietro `Authz.IsAdmin` e la sua voce in `AdminNav` è `Chi.Admin`
come le altre: verificato, non assunto. Quello che si chiude qui è la **domanda**, non un lavoro.

⚠️ **Con un ruolo al posto di un nome, la colonna «Da chi» smette di essere un dettaglio.** La carta chiedeva
un nome apposta: un elenco che è di tutti non è di nessuno, e una resa che entra **verbatim** in ogni carta
che contiene la formula è una cosa che si vuole poter risalire. La pagina registra il VID di chi ha scritto
o corretto ogni voce e lo mostra come link al profilo IVAO, e tiene distinte le voci **di partenza** (senza
autore: sono contenuto iniziale, non la scelta di qualcuno). È quella colonna a rendere praticabile la
decisione: chi arriva dopo vede *chi* ha deciso come si dice quella formula, e a chi chiedere perché.

⚠️ **E resta il limite del meccanismo, che ora va detto a tutti e non a uno**: la resa entra nel documento
**verbatim**. Non c'è declinazione, non c'è concordanza, non c'è contesto — «report downwind» è quella
stringa lì, in ogni frase in cui la voce scatta, anche a inizio periodo. Vanno bene le formule che *sono*
fisse; una parola comune che cambia forma no. Sapere quali sono quali resta mestiere di chi controlla, non
di chi programma: è il motivo per cui la lista non poteva restare nel codice, e non cambia con la platea.

Delle tre voci aperte di questa carta restano quindi **Q1** (ritenzione del piano gratuito) e **Q2** (la
domanda a IVAO HQ).

### Q4 🟢 APERTO — la vLOA dovrebbe smettere di nascere in inglese

Deciso il 28 agosto 2026, **da fare dopo**. Se la lingua sorgente è «quella in cui si redige» — criterio
adottato dal documento militare (vSOP militari §1d) — allora la **vLOA è l'unico documento che nasce
`Language.En`**, residuo di quando l'inglese era l'unico modo di renderla leggibile alla controparte
estera: problema che la traduzione ha risolto.

⚠️ **Non è un cambio di una riga.** Le vLOA esistenti sono *scritte* in inglese: ribaltarne la lingua
sorgente senza riscriverne il contenuto renderebbe la memoria di traduzione **inutile su tutto quel
corpus** — le impronte sono del testo inglese, e cercarle come italiano non troverebbe niente. Vuole un
giro suo, con un travaso pensato.

### Q5 ✅ CHIUSO — il rosso intermittente: era la contesa sul file di diagnostica

⚠️ **Identificato il 28 agosto 2026**: `CronometroAvvioTests.Lavvio_vero_lascia_il_riepilogo_nel_file_di_diagnostica`
(`Vipi.E2E.Tests`). ✅ **Chiuso lo stesso giorno.**

**Come si riproduceva e come no.** Da solo passava (3/3 con `--filter CronometroAvvio`); con **tutto il
progetto E2E** cadeva; nella corsa dell'intera soluzione comparve in sei corse su otto — perché il segnale
era la contesa, non il test.

**La causa, letta nel codice.** Il file di diagnostica è **uno solo per processo** — `diagnostica/avvio-diagnostica.txt`
accanto all'eseguibile, che nei test è la cartella `bin` del progetto — e ogni avvio lo **riscrive da capo**
(`WriteConfigurationSummary` fa `File.WriteAllText`) prima di aggiungerci in coda il riepilogo delle fasi
(`CronometroAvvio.Scrivi`, che invece fa `AppendAllText`). In produzione è il disegno giusto: un host per
processo, e chi scarica il file vuole l'avvio corrente. Ma in `Vipi.E2E.Tests` gli host li avviano **dieci
classi**, e xUnit fa girare le classi in parallelo: la finestra fra «il mio avvio ha scritto» e «io rileggo»
è aperta a chiunque, e la `WriteAllText` di un altro host porta via il riepilogo appena scritto.

**La correzione**: `tests/Vipi.E2E.Tests/ParallelismoDelProgetto.cs` — un solo test alla volta in tutto il
progetto (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`), col perché scritto accanto.
Serializzare le **sole** classi che avviano un host costerebbe uguale — sono quelle lente — e lascerebbe una
trappola silenziosa: l'undicesima classe che avvia un host senza mettersi l'attributo rimetterebbe il rosso
senza che nulla lo dica. **Costo misurato** (due corse per parte, build ferma): 227 test, **43 s in parallelo,
78 s in fila** — ~35 secondi per corsa, in cambio di un cancello di cui ci si può fidare.

L'asserzione ora dice anche **dove guardare** se ricapita, perché un rosso lì sembra un difetto del
cronometro mentre quasi sempre è un secondo avvio nella stessa finestra.

### Q6 ✅ CHIUSO — il secondo rosso: passava per via del POOL, non dell'interceptor

⚠️ `SqliteTuningTests.Interceptor_enables_wal_and_busy_timeout` (`Vipi.Infrastructure.Tests`), rosso **una
volta sola** il 28 agosto 2026, e solo su net10. ✅ **Chiuso lo stesso giorno, con la riproduzione in mano.**

**Il messaggio d'asserzione che serviva** — quello che questa scheda chiedeva di catturare — dice
`Expected: 5000, Actual: 0`: è caduto sul **`busy_timeout`**, non sul WAL. L'ipotesi di prima (il passaggio
a WAL che non riesce) era sbagliata, ed era sbagliata per un motivo che vale la pena tenere: il WAL è
scritto nell'**intestazione del file**, quindi sopravvive a qualunque connessione; il `busy_timeout` invece
è **per-connessione** e muore con essa.

**La causa vera.** Il test apriva la connessione con `conn.Open()` sulla `DbConnection` **nuda**. Ma
l'interceptor è di EF Core e gira **solo quando è EF ad aprire**: `Open()` diretto lo scavalca. Il test
passava lo stesso perché Microsoft.Data.Sqlite tiene le connessioni in un **pool**, e dopo `EnsureCreated`
gli restituiva la **stessa handle**, che il `busy_timeout` ce l'aveva già addosso da quando l'interceptor
era girato per davvero. Verde per via del pool, non per via di ciò che diceva di provare.

Basta quindi che qualcuno **svuoti il pool** in quella finestra perché arrivi una handle nuova, col
`busy_timeout` a zero — e qualcuno lo faceva: `TraduzioneDalVivoTests.DisposeAsync` chiamava
`SqliteConnection.ClearAllPools()`, che è di **processo**, e lo chiamava **sempre**, anche quando quella
prova è saltata (è saltata in ogni corsa normale: vuole `VIPI_TRADUZIONE_LIVE=1` e una chiave Azure). Le
classi sono due, quindi giravano in parallelo.

**Riprodotto in modo deterministico** aggiungendo `Pooling=False` alla stringa di connessione: rosso ogni
volta, con quel messaggio.

**La correzione, in due mosse**: (1) il test riapre **attraverso EF** (`db.Database.OpenConnection()`), così
l'interceptor gira davvero, e tiene `Pooling=False` perché non possa più passare per sbaglio — senza pool,
se l'interceptor non gira l'asserzione cade **sempre** invece che una volta ogni tanto; (2) `ClearAllPools()`
è sparito da **tutti e due** i posti che lo chiamavano, sostituito da `Pooling=False` nella stringa di
connessione: il file si libera alla chiusura del `DbContext` senza toccare i pool degli altri.

⚠️ **La regola generale**: `SqliteConnection.ClearAllPools()` è una chiamata **di processo**. In una suite
che gira in parallelo, un test che ripulisce dopo di sé con quella sporca tutti gli altri. Se serve
liberare un file, si evita il pool alla radice con `Pooling=False`.

**Esito**: suite **6633 verdi** su entrambi i TFM, `dotnet build -warnaserror` senza avvisi.

---

## Q-bis. La lingua, per intero — 28 agosto 2026, sera

Ramo **`bilingue-tutte-le-pagine`**, sei commit, **spinto e non fuso**. Regole:

⚠️ **Aggiornamento del 1 settembre 2026**: il ramo `bilingue-tutte-le-pagine` **non esiste più** e il suo contenuto è in `main` da tempo (il sito è bilingue in produzione). Questa riga resta come storia: non c'è niente da fondere.
[design/regole-lingua.md](design/regole-lingua.md).

Nasce da una domanda del committente — «sono sulla vIPI di Crotone e non c'è traccia della traduzione» — e
ogni voce qui sotto è un difetto che **la suite non poteva vedere**: ognuno sta nell'incontro fra due pezzi
che, da soli, funzionavano.

| | Che cosa mancava | Dove |
|---|---|---|
| **Q7** ✅ | **La lettura bilingue girava su DUE viewer su cinque.** `DocumentTranslator` era iniettato solo in `MilDocumentPage` e `VloaListPage`; aeroporto, APP e vIPI ACC non lo chiamavano. Documento italiano dentro interfaccia inglese, **senza avviso** | `1c44c92` |
| **Q8** ✅ | **Il selettore di lingua non esisteva.** C'erano il cookie, la risoluzione per richiesta e il badge: la lingua si poteva chiedere solo scrivendo `?culture=` nell'indirizzo | `13df644` |
| **Q9** ✅ | **Le REGOLE su carta**, e le cose che non si traducono: marchio, titolo del documento, briciola di pane (sempre inglese), indirizzi | `6247987` |
| **Q10** ✅ | **La Guida** risponde alla ricerca nella lingua di chi ha cercato (l'unico testo di backend che vede il pubblico) | `857bf58` |
| **Q11** ✅ | **125 messaggi a chi modifica** (validazione + blocchi all'eliminazione) hanno due lingue | `1649358` |
| **Q12** ✅ | **«MRVA»** al posto di «Minime di vettoramento», uguale nelle due lingue; e il **correttore delle traduzioni dentro l'editor** | `2af3a39` |

### Le trappole, che valgono oltre questo giro

- ⚠️ **Cambiare lingua deve RICARICARE la pagina.** La navigazione «enhanced» di `blazor.web.js` non
  ricarica il documento: sostituisce il DOM e **riusa il circuito**, la cui cultura è quella di quando è
  nato. Visto a schermo: pagina inglese col METAR ancora «VENTO / VISIBILITÀ / NUBI». Cura:
  `data-enhance-nav="false"` sui link del selettore.
- ⚠️ **`AirportLegacySections.ForView` butta via la traduzione**: riporta ogni sezione di catalogo al suo
  titolo cablato, quindi chiamato *dopo* il traduttore rimette l'italiano. Indice e testate dicevano cose
  diverse.
- ⚠️ **Il titolo di una sezione di catalogo sta NEL DOCUMENTO**: cambiare `SectionCatalog` vale solo per i
  documenti nuovi. Serve un passo d'avvio, e le release pubblicate restano com'erano.
- ⚠️ **`IStringLocalizer` non sa leggere in un'altra lingua**: risolve sempre sulla cultura corrente. Per la
  briciola in inglese fisso serve il `ResourceManager` (`EnglishStrings`).
- ⚠️ **Nei test la cultura si FISSA** (`CulturaDiProva`): dodici test asserivano il testo italiano e
  sarebbero caduti su una macchina inglese. Fragilità vecchia, resa visibile dai messaggi bilingui.

### Q13 🟢 APERTO — la memoria contiene rese plausibili e sbagliate

Non è un difetto del codice: è il lavoro che la §Q3 aspetta da una persona. Viste a schermo il 28 agosto:
«Regole piste» → *Slope rules* (corretta in *Runway rules* provando il correttore nuovo), «Minime di
vettoramento» → *Minimum vectoring*. ⚠️ Ora si correggono **dall'editor**, senza passare dal Registro.

### Q14 🟢 APERTO — il ramo va fuso

Sei commit, suite verde su entrambi i TFM, build Release senza avvisi. Da fondere insieme a
[archivio-atc-mondiale](feature/2026-08-28-archivio-atc-mondiale.md), o dopo: i due rami non si toccano.

### Q15 ✅ CHIUSA — l'interruttore esisteva e nessuno sapeva dove fosse

Trovata rileggendo la funzione da fuori. `Translation:Enabled` è **falso per default** — scelta giusta, un
sito senza chiave non è rotto — ma non compariva in **nessun** `appsettings`, in nessun file di deploy, in
nessun foglio di manovra: cercando `Translation__` in tutto il repository non usciva niente. Il cablaggio
era completo dal 27 agosto (motori, catena, memoria, giro dei 15 minuti, congelamento nelle release), e la
funzione era **spenta**.

⚠️ **È il modo silenzioso di sbagliare, ed è per questo che vale una voce.** Spenta, la traduzione non
somiglia a una funzione spenta: il selettore IT/EN c'è lo stesso, le 2 487 etichette cambiano lo stesso, e
solo aprendo un documento si scopre che la **prosa** è rimasta nella lingua in cui è stata scritta. Nessun
errore, nessun log, nessuna pagina che lo dica. Dimenticare di accenderla era indistinguibile dall'averla
accesa.

Che cosa è stato fatto:

| | |
|---|---|
| **La forma della sezione, scritta** | `src/Vipi.Host/appsettings.json` porta ora `Translation` per intero (spenta, chiavi vuote, tetto di DeepL a 450 000). Non serve a configurare: serve a **dire che esiste** e quale chiave si aspetta |
| **L'interruttore, acceso** | `deploy/atc-ivao/appsettings.Production.json`: `"Translation": { "Enabled": true }`. ⚠️ Chi aggiorna da un pacchetto precedente **non ha questa riga** e deve aggiungerla |
| **Il posto dove guardare** | `StartupDiagnostics`: tre righe nuove (`Enabled`, presenza della chiave Azure con la sua regione, presenza della chiave DeepL) più un blocco `⚠` esplicito quando è **accesa senza nessuna chiave** — che è la combinazione che non dà nessun altro segnale |
| **Il rumore, tolto prima di farlo** | `TranslationFillHostedService`: accesa senza motore configurato, il giro finiva la catena e riportava `NotConfigured` **per ogni direzione** — due Warning ogni quarto d'ora, 96 al giorno per sempre, che dicono «non riuscita» dove la verità è «non è mai stata chiesta a nessuno». Ora esce prima, con **un** Warning che non si ripete |
| **Il foglio di manovra** | [deploy/atc-ivao/LEGGIMI-TRADUZIONE.md](../deploy/atc-ivao/LEGGIMI-TRADUZIONE.md): quale chiave, dove si mette, i due tranelli dei motori (regione Azure ⇒ 401, chiave `:fx` di DeepL ⇒ 403), come si verifica, e le tre cose che cambiano a schermo quando si accende |

⚠️ **Due conseguenze da sapere prima di premere l'interruttore**, e sono scritte anche nel foglio: le
release pubblicate **prima** si traducono da sole (non portano niente di congelato, quindi ricadono sulla
memoria viva), mentre da quel momento ogni **nuova** pubblicazione fotografa quel che la memoria sa in
quell'istante — quindi pubblicare prosa nuova *prima* del giro la congela non tradotta. E comparirà
l'avviso «traduzione automatica, non revisionata» finché una persona non rilegge (§Q13).

### Q16 ✅ CHIUSA — il tetto di spesa sottostimava

**a) ✅ La deriva da revisione è chiusa.** `CaratteriSpesiStimatiAsync` filtrava su
`Origin == TranslationOrigin.Machine`. Ma quando una persona corregge una resa, `SaveHumanAsync` ribalta
`Origin` a `Human` e **lascia intatto `Engine`**: quei caratteri, spesi davvero, sparivano dal conto. Più
si revisionava, più il tetto si allargava — la difesa si allentava proprio mentre il lavoro andava avanti,
e nel verso peggiore: sottostimare vuol dire sfondare una franchigia che per DeepL **non si rinnova**.

La cura è togliere il filtro: la colonna `Engine` è la domanda giusta — dice **chi ha tradotto**, e non
cambia quando cambia chi ha l'ultima parola sul testo. Una riga nata da una correzione umana senza che
nessun motore l'avesse tradotta ha `Engine` nullo e non conta per nessuno, che è corretto.

⚠️ **Il test che c'era non poteva vederlo**: si chiamava «i caratteri spesi contano solo la macchina», e la
sua riga umana non era mai passata da un motore — `Engine` nullo, quindi fuori dal conto in tutti e due i
modi. Ora c'è quello che manca: macchina, **poi** correzione, e il conto non cala.

**b) ✅ CHIUSA — i segmenti scartati si ripagano a ogni giro.** Quando il motore restituisce un segmento
rotto (un segnaposto mangiato), `TranslationFillUseCase` **non lo salva**, per non mettere in memoria una
frase che sembra giusta e non lo è. Conseguenza: quei caratteri sono stati **pagati**, non entrano in
questa somma (che si deduce da ciò che è rimasto in tabella), e **il giro dopo li rispedisce** — ogni
quarto d'ora, per sempre.

Non si chiude senza un posto dove ricordarsene, cioè senza schema: la cura vera è un **contatore suo**, una
riga per giro con i caratteri spediti, invece di dedurre la spesa dallo stato della memoria — che è una
cosa diversa e lo sarà sempre. In attesa, la perdita **non è più invisibile**: il rapporto porta
`CaratteriScartati` e il giro lo scrive come **Warning**, perché vuole una persona.

⚠️ Sui volumi misurati (98 000 caratteri di semina contro il milione di DeepL) il tetto non si avvicina
nemmeno. Diventa urgente se il corpus cresce di un ordine di grandezza, o se un segmento comincia a tornare
rotto sistematicamente — ed è per quello che ora si vede nei log.

### ✅ 30 agosto 2026 — è successo, ed era una frase NOSTRA

Il registro diceva **155 caratteri per 1 segmento** a ogni giro. Cercato interrogando il corpus, era il
testo di partenza di una vLOA — quello che scriviamo noi quando la vLOA nasce:

> `Both areas of responsibility are imported from the IVAO database; the common boundary is the LIBB/LGGG ACC limit.`

⚠️ **Due segnaposti attaccati** (`LIBB/LGGG`), che è il costrutto che un motore tende a fondere. Circa
**446 000 caratteri al mese** per una frase di cui sapevamo già la traduzione.

**Due cure, tutt'e due fatte.**

1. **Le sette frasi di `VloaSections` hanno il loro italiano accanto all'inglese**, nello stesso file, e
   `FrasiVloa.SeminaAsync` le mette in memoria prima di chiedere alla macchina — la stessa scelta di
   `TitoliUfficiali`, un gradino più in là. ⚠️ Le coppie di ACC sono quelle dei **confinanti**, cioè la
   sorgente da cui la vLOA è nata: il testo seminato è **identico** a quello nei documenti, e un test lo
   prova confrontandolo con `VloaSections.Canonical`. Dal vivo: **135 frasi seminate**, e il giro dopo non
   ha più niente da tradurre né da scartare.
2. **L'avviso dice QUALI sono.** «1 segmento tornato rotto» non si può cercare — il corpus ne ha decine — e
   il testo ce l'avevamo in mano proprio nel punto in cui lo buttavamo.

### ✅ E il contatore vero, il 30 agosto 2026

C'è: **`TranslationSpends`**, una riga per invio coi caratteri **spediti** — rotti compresi, perché è quello
che il fornitore fattura — più quanti segmenti erano e quanti si sono dovuti buttare. Il tetto legge lì.

⚠️ **La parte che non si indovina è la fotografia iniziale.** Passare al contatore avrebbe **azzerato** la
spesa, e il tetto avrebbe creduto di avere tutta la franchigia davanti. Al primo avvio dopo la migrazione si
scrive una riga `Baseline` per motore con la vecchia stima dedotta: è l'unica cosa che sappiamo del passato,
e la riga lo dichiara nel nome. Dal vivo: **5280 caratteri per azure**, esattamente quel che si deduceva.

⚠️ E **«una volta sola» si chiede al database**, non a un campo in memoria: il giro vive in un processo che
si riavvia, e un flag ricomincerebbe da capo scrivendo una fotografia in più — cioè **gonfiando** la spesa,
che è il verso opposto ma altrettanto sbagliato. Provato con due avvii sullo stesso database: due righe.

I due test che presidiavano la deduzione non sono spariti: si sono **spostati sulla fotografia**, che è dove
la deduzione è rimasta a vivere — compresa la lezione di **§Q16a**.

### Q17 ✅ CHIUSA — due chiavi mancanti, e il buco della guardia che le ha lasciate passare

`ImpactKind_SectorRenamed` e `ImpactKind_SectorDetached` non erano scritte in **nessuno** dei due `.resx`.
`DiagnosticaPage` rende quella tabella con `L["ImpactKind_" + r.Key]`, e un `IStringLocalizer` a cui manca
la chiave **non lancia**: restituisce il **nome della chiave**. La tabella scriveva
`ImpactKind_SectorRenamed`, in italiano e in inglese.

⚠️ **Non era un valore teorico.** Entrambi gli impatti si alzano in produzione: `SectorRenamed` da
`EfCallsignRenameService`, `SectorDetached` da `DeletionService`. I gemelli `Impact_*` — le frasi della
riga «da rivedere» — c'erano tutti e undici: mancava solo l'etichetta corta del riepilogo.

**Perché la suite non lo vedeva**, ed è la parte che conta: `SharedResourceIntegrityTests` verificava le
chiavi **letterali** e saltava di proposito quelle composte (`if (m.Groups["coda"].Success) continue`),
perché `L["ImpactKind_" + r.Key]` leggendo il sorgente non è verificabile. Il buco era dichiarato e
nessuno l'aveva mai riempito.

La cura non è una riga di risorsa in più: è **guardare dall'altro capo**. La guardia nuova non parte dal
sorgente ma dall'**enum**, con una tabella dichiarativa di otto famiglie
(`Audit_Cat_`/`Categoria`, `TaskStatus_`/`EditorTaskStatus`, `Stats_Tag_` e `Stats_TagHint_`/`TrafficTag`,
`Diag_Area_`/`ConsistencyArea`, `Sorg_St_`/`ImportHealth`, `ImpactKind_` e `Impact_`/`ImpactKind`), più i
sette giorni di `Day_{n}` e `Day_{n}_Full`, che non sono un enum.

- ⚠️ **`typeof` e non il nome dell'enum come stringa**: un enum rinominato non compila, invece di far
  passare un test che ha smesso di guardare qualcosa.
- ⚠️ **Il controllo è a senso unico** e dev'esserlo: ogni valore deve avere la sua chiave, non ogni chiave
  col prefisso deve avere un valore — `Impact_Title` e `Impact_ToReview` stanno in quella famiglia e non
  sono impatti.
- Nello stesso giro la guardia letterale guarda anche **`En["…"]`** (`EnglishStrings`): legge le stesse
  chiavi dallo stesso resx e sbaglia allo stesso modo, e guardarne una sola lasciava scoperta metà della
  briciola di pane, che sta in cima a ventinove pagine.

Verificata **togliendo** le due chiavi: la guardia le nomina tutte e due, e togliendone una sola dall'en
cade anche la guardia di parità. `Vipi.Ui.Tests` 715 → **724**.

⚠️ **Resta fuori** `src/Vipi.Host`: la guardia letterale scandisce solo `src/Vipi.Ui`. Oggi non ci sono
chiavi di risorsa là dentro, quindi non copre niente — ma è una condizione, non una garanzia.

### Q18 ✅ CHIUSA — il congelato era un muro, e l'avviso non si spegneva mai

Due difetti diversi nello stesso pezzo di codice, e nessuno dei due si vedeva prima che la funzione
girasse davvero (§Q15). Entrambi stanno fra `ReleaseService` — che scatta la fotografia — e
`DocumentTranslator` — che la legge.

**a) Il congelato SOSTITUIVA la memoria invece di sovrapporsi.** `PreparaAsync` faceva
`congelate is not null ? congelate : memoria`: bastava **una** voce nello snapshot perché la memoria viva
non venisse più letta per tutto il documento. Ma la fotografia la scatta la pubblicazione, nell'istante in
cui si preme il tasto, e il giro che riempie la memoria passa **ogni quarto d'ora**. Chi scriveva prosa
nuova e pubblicava subito — il caso normale, non quello raro — congelava una traduzione **incompleta**: il
motore traduceva il resto dieci minuti dopo, la memoria ce l'aveva, e nessuno andava più a prenderla.

⚠️ **Ed era peggio di «a chiazze», misurato a schermo il 28 agosto sera** sulla vIPI di LIBD, servita dalla
sua release effettiva. Preparando la copia del database con **una sola** voce congelata — per un segmento
che sulla pagina non compare nemmeno — e due frasi tradotte in memoria:

```
codice vecchio:  40 of 40 sentences are not translated yet      (la riga resta in lingua sorgente)
codice nuovo:    33 of 40 sentences are not translated yet      (la riga dice la sua traduzione)
```

Non è un pezzo che manca: è **tutta** la traduzione del documento che si spegne, per una voce congelata.
Un documento pubblicato dopo la prima frase congelata si sarebbe letto **interamente** nella lingua
d'origine, con l'avviso che diceva la verità e nessuno che potesse capire perché.

⚠️ **La ragione del congelamento resta intatta**, e la riparazione non la tocca: dove lo snapshot ha una
voce, vince la voce — una resa corretta oggi su un altro documento non deve riscrivere quel che questo ha
già pubblicato. Ma **dove lo snapshot non ha niente non c'è niente di pubblicato da proteggere**: quella
frase, nella release, si legge nella lingua sorgente. Prenderla dalla memoria non cambia una parola
pubblicata, riempie un buco. «Dove», non «se».

⚠️ **A congelato completo il database non si tocca**, come prima: la memoria si legge solo se resta
qualche impronta scoperta, e **solo per quelle**. Su una release pubblicata con calma sono zero query.

**b) Il timbro «riletta» non viaggiava nello snapshot.** Le congelate erano `hash → stringa`, quindi il
viewer non poteva che dichiararle tutte «non revisionate». Conseguenza: l'avviso «traduzione automatica,
non revisionata» **non si spegneva mai** su un documento pubblicato — nemmeno con ogni singola frase
corretta a mano. Lo staff correggeva nel pannello, ripubblicava, e l'avviso restava: un giro di revisione
senza uscita, cioè un giro che nessuno fa una seconda volta.

Ora lo snapshot porta `FrozenTranslation(Text, Reviewed)`. ⚠️ **La cautela non è cambiata**: quel che
arriva senza timbro si dichiara **non riletto** — dichiarare riletta una frase che nessuno ha guardato, su
un documento operativo, è l'errore che non si può fare. Le release pubblicate prima restano marcate finché
non si ripubblicano, che è la regola di ogni altra correzione editoriale.

**Il lettore legge due forme e ne scrive una.** Gli snapshot già pubblicati portano la stringa nuda e sono
documenti **in vigore**: un cambio di forma che non sapesse leggere la vecchia non darebbe un errore di
compilazione, darebbe un'eccezione su una pagina pubblica — o, peggio, una vLOA che si apre con tutte le
traduzioni sparite. Il convertitore accetta stringa e oggetto, ignora i campi che non conosce, e su
qualunque altra forma legge «niente di congelato» invece di sollevare.

⚠️ **`HandleNull` non è rifinitura.** Senza, `System.Text.Json` non chiama affatto il convertitore su un
token `null`: infila un `null` nel dizionario, e la prima riga che chiede a quella voce se ha un testo
esplode — su una pagina pubblica, per un solo `null` in fondo a uno snapshot. Trovato dal test, non a
mano.

**Le prove.** Nove test sul convertitore (le due forme, la convivenza, il campo sconosciuto, le quattro
forme illeggibili, la voce rotta che non deve rovinare quelle dopo) più due sul **payload vero** — perché
il convertitore da solo non dice niente su quel che succede due livelli di annidamento più in là, dentro
un `DocReleasePayload` serializzato come lo serializza `BuildSnapshotJsonAsync`. Uno di quei due apre uno
snapshot **nella forma di prima** e pretende che si legga. `Vipi.Application.Tests` 1303 → **1322**.

⚠️ Il test che diceva «il congelato vince e la memoria NON si legge» **asseriva il difetto**: era vero, ed
era il guasto. Ora si chiama «se il congelato copre TUTTO», e accanto c'è quello che prova il caso
parziale. Un test verde non è una prova che il comportamento sia quello giusto: è una prova che è quello
che qualcuno ha scritto.

### Q19 ✅ CHIUSA — «questa correzione tocca N documenti» diceva un numero falso

Il pannello di revisione mostra quel numero **prima** che si salvi, ed è l'unico avviso che ha chi
corregge: una correzione di traduzione tocca la **frase**, quindi vale su ogni documento che la contiene.
`EfTranslationMemory.DocumentiToccatiAsync` faceva un `Body.Contains(testo)` e guardava **solo**
`ContentBlock.Body`.

⚠️ **Sbagliava in tutte e tre le direzioni**, e ognuna nel modo peggiore:
- una frase in un **titolo di sezione** contava **zero** — e i titoli di sezione sono nel corpus, quindi la
  memoria ne è piena;
- una frase in una **cella di tabella** (`BodyJson`) contava **zero** — e le tabelle sono metà del contenuto
  di una vIPI;
- un corpo con l'**apostrofo tipografico** o l'a-capo di Windows non corrispondeva al testo normalizzato che
  arriva dalla memoria, quindi contava zero anche lì.

E siccome il pannello mostra l'avviso solo **sopra il primo documento**, un conto che sbaglia per difetto
non dà un numero impreciso: non dà **nessun avviso**. Chi correggeva salvava credendo di toccare il
documento che aveva davanti.

⚠️ **Il commento prometteva un passo che nel codice non c'era**: «si filtra grossolanamente sul database e
si conferma in memoria con la normalizzazione» — la conferma non esisteva, il `Contains` era l'ultima
parola. Un commento che descrive una difesa assente è peggio di nessun commento: chi lo legge smette di
cercare.

Adesso si confronta l'**impronta**, e i segmenti si tagliano **come li taglia il corpus** — una definizione
sola di «segmento» fra chi traduce, chi conta e chi riempie la memoria. ⚠️ Si legge tutto e si conta in
memoria, senza `LIKE`: un prefiltro sul database non può essere corretto, perché la normalizzazione avviene
*dopo* e quel che il database confronta è la grafia. Costa quanto il corpus misurato — 499 campi, 23 344
caratteri — e parte solo quando una persona apre **una** riga del pannello.

Con un conto vero, la frase doveva reggere anche a uno e a zero: `Tr_SavedOne` e `Tr_SavedNone` al posto di
`Math.Max(_toccati, 1)`, che scriveva «vale per 1 documenti» — il segno che quel numero non lo guardava
nessuno. Dieci test, **verificati mutando il codice al comportamento vecchio: sei cadono**.

### Q20 ✅ CHIUSA — le due pagine d'errore erano solo italiane

`PaginaErrore` e `IvaoLoginFailurePage`: `<html lang="it">`, testo italiano, e una riga inglese in grigio in
fondo. Sono **pubbliche**, e sono quelle che un lettore inglese vede *proprio quando* qualcosa si è rotto —
il momento peggiore per non capire che cosa c'è scritto.

⚠️ **Non cadevano sotto nessuna eccezione dichiarata.** La carta ne dichiara tre — `ScreensIndex`,
`AccCoordinationView`, e «log e diagnostica» — e l'ultima sembrava coprirle. Non le copre: il confine è
**«lo legge un utente?»**, non «è testo tecnico?». Ora la regola lo dice a parole.

Le due lingue viaggiano in linea (`Messaggio.Lingua`), non dalle risorse: quelle vivono in `Vipi.Ui`, e il
senso di quelle due pagine è dipendere dal minor numero di pezzi possibile — devono reggere quando è il
**layout condiviso** ad aver lanciato (successo il 24 agosto 2026), o quando l'autenticazione è rotta.

⚠️ **`lang` esce dallo stesso posto del testo** (`Messaggio.Codice`, nuovo). Con due letture separate della
cultura, un giorno una pagina direbbe `lang="it"` con dentro l'inglese — e per un lettore di schermo, o per
il traduttore automatico del browser, quella riga è l'unica cosa che dice in che lingua è scritta.

⚠️ **La parte non ovvia era che la cultura fosse già risolta.** `UseExceptionHandler("/Error")` non scrive
una risposta al volo: **ri-esegue** la pipeline, e in quel secondo giro `UseRequestLocalization` passa prima
dell'endpoint. Se la pagina si fosse composta dove l'eccezione è stata *catturata*, la cultura sarebbe stata
quella sbagliata — le modifiche a `CurrentUICulture` scendono lungo la catena di chiamate, **non risalgono**.
A tavolino non si vede; il test lo chiede con `Accept-Language` e lo prova dal vivo. ⚠️ E nel giro di
ri-esecuzione la stringa di query non c'è più: restano il **cookie** e `Accept-Language`, che è l'ennesima
ragione per cui la lingua si ricorda nel cookie.

⚠️ **Nei test asserire solo ASCII**: titolo e spiegazione passano da `HtmlEncoder`, che rende in entità
numeriche sia l'apostrofo sia le accentate. Cercare «L'accesso è scaduto» non trova niente **nemmeno quando
c'è** — un test verde che ha smesso di guardare. E `CulturaDiProva` è ora **collegata** (non copiata) anche
in `Vipi.E2E.Tests`: da qui in avanti quelle pagine dipendono dalla cultura ambientale, e un test che
asserisce l'italiano senza fissarla passa in Italia e cade su una macchina inglese.

### Q21 ✅ CHIUSA — 56 messaggi a chi modifica erano in una lingua sola

La §Q11 ne aveva portati 125 a due lingue, ma si era fermata ai **servizi di `Vipi.Application`**: i
**repository di `Vipi.Infrastructure`** non erano stati nominati da nessuna parte, e la carta non li
menzionava. Restavano in italiano frasi che legge un controllore in cima all'editor — «Il blocco è stato
modificato nel frattempo: ricarica l'editor prima di salvare», «Fra questi due enti esiste già un accordo»,
«proietta i settori (pagina ACC) prima di generare la vLOA».

**Il confine, ora scritto** (`docs/design/regole-lingua.md`): lo dice il **tipo dell'eccezione**, ed era
già così prima che qualcuno lo scrivesse. `ValidationException` / `EditConflictException` = frase per una
**persona** → due lingue. `InvalidOperationException` / `KeyNotFoundException` = **invariante** → resta in
italiano, perché «Sezione 41 inesistente» non dice niente a nessuno e finisce nel registro. Quattro
istruzioni viaggiano per ragioni storiche dentro una `InvalidOperationException` e sono tradotte lo stesso,
marcate nel codice.

⚠️ **La guardia è STRUTTURALE, e non è un dettaglio di stile.** Questa passata è cominciata con una
scansione a parole italiane, e quella scansione ne ha **mancati quattro**: «Intervallo QNH invertito
(From > To).», «Vento in coda massimo fuori range (0–40 kt).» — nessun accento, nessuna parola funzione. Li
ha trovati la guardia, che non prova a indovinare se una stringa è italiana ma pretende che l'argomento sia
`Lingua(...)`, l'unica forma che porta due lingue. Un elenco di parole sbaglia in tutti e due i versi; una
regola sulla forma no.

⚠️ **E la cultura di UI di questa macchina è INGLESE** — è la lingua di Windows, indipendente dal formato
regionale. Se n'è accorto un test: `SetParent_Rifiuta_Un_Padre_Piu_In_Basso_Nella_Scaletta` cercava
«scaletta» e ha trovato «cannot cover». Non era un difetto nuovo: era una fragilità vecchia, resa visibile
dalla traduzione. `CulturaDiProva` è ora collegata anche in `Vipi.Infrastructure.Tests`.

Restano in italiano, dichiarati: avvio, configurazione, credenziali IVAO, provider di persistenza,
key-ring — chi li legge tiene su il sito, non lo usa.

### Q22 ✅ CHIUSA — i sette resti dell'audit, e la prova a schermo

Chiusi in un giro perché piccoli e della stessa famiglia. In ordine di quanto si vedono:

| | | |
|---|---|---|
| **§9** | La **Guida** era un quarto meccanismo non dichiarato | I *corpi* restano in linea, e ora la carta **dice perché** (sono paragrafi HTML di quindici righe: nel resx sarebbero valori giganti che nessuno rilegge in parallelo). I **titoli** invece vengono dal catalogo di ricerca: stavano in due posti e gli **inglesi divergevano in 11 casi su 38** |
| **§10** | `TranslationLookup` è scoped, e in un circuito Blazor lo scope vive **ore** | La cache **scade** dopo 30 s: una correzione fatta nel pannello si rivede ricaricando, invece che al circuito successivo |
| **§11** | `Rendering()` non si annidava: il `Dispose` **azzerava** invece di rimettere | Si rimette. Con un blocco solo era identico; il primo che ne annidasse due avrebbe visto la release uscire metà in una lingua e metà nell'altra, **senza errore** |
| **§12** | Il **titolo del documento** finiva fra i segmenti da congelare | Tolto. Innocuo — nessuno lo traduce — ma era l'unico posto del prodotto che chiedeva la traduzione di un titolo, e la prossima persona ne avrebbe dedotto la regola sbagliata (R4) |
| **§13c** | La guardia sulle risorse scandiva solo `src/Vipi.Ui` | Anche `Vipi.Host`, che ha un `<head>`, due pagine d'errore e i suoi endpoint |
| **§14** | Nessun `hreflang`: per un motore di ricerca il sito bilingue era **una pagina sola** | `rel="alternate"` per lingua più `x-default`, composti da `LinkLingua` — lo **stesso** codice del selettore in barra, o un'alternativa che punta altrove nessuno la vedrebbe |
| **§15** | `NeutralResourcesLanguage` non dichiarato | `it`. Toglie una ricerca a vuoto di satellite a ogni lettura italiana, ed è la sola cosa scritta che dica quale lingua c'è nel `.resx` senza suffisso |

### La verifica live, e le due cose che ha detto

Guidata su Edge contro una **copia** del `vipi.db`, 13 controlli su 13 (`lingua-verifica.js`):
gli `hreflang` puntano a **questa** pagina e non perdono la query (`?icao=LIBD` sopravvive); `<html lang>`
segue `?culture=`; la Guida rende 39 capitoli col titolo dal catalogo in IT e in EN; il selettore ha
`data-enhance-nav="false"` e, cambiando lingua, cambiano **anche le isole** (METAR, badge live); le due
pagine d'errore sono nella lingua giusta.

⚠️ **La prima mira era sbagliata, e lo ha detto lo schermo.** Avevo seminato la memoria coi titoli che la
pagina d'aeroporto mostra — e non si è tradotto niente. Quei titoli vengono dal **catalogo**, non dallo
snapshot: **non sono segmenti del documento**, quindi la passata non li conosce e non li conoscerà mai
(`TranslatedDocument.Pass` ri-applica la passata, non ne allarga il vocabolario). Vale finché il titolo di
catalogo **coincide** con quello del documento — che è il caso dei vSOP militari, dove il difetto era stato
trovato, e **non** quello degli aeroporti importati, che hanno titoli di sezione inglesi. 🟢 **Resta
aperto**: su una vIPI d'aeroporto letta in inglese le testate delle sezioni di catalogo sono **italiane**, e
nessun avviso lo dice.

⚠️ **E il §Q18a era peggio di come l'avevo scritto.** Rimesso il codice vecchio, ricompilato e ricaricata la
stessa pagina: **40 su 40 non tradotte**, contro 33 su 40 col codice nuovo. Una sola voce congelata — per un
segmento che sulla pagina non compare nemmeno — spegneva **tutta** la traduzione del documento. «A chiazze»
era ottimismo.

---

## R. I vSOP militari — 28 agosto 2026

Carta: [feature/2026-08-27-vsop-militari.md](feature/2026-08-27-vsop-militari.md).
✅ **Tutte e dieci le slice chiuse e in `main`** (`b940609`, `2ffc728`, `6644b5e`), suite 6633 verdi.
`main` **spinto**: `origin/main` allineato a `6644b5e`.

Documento **separato** dalla vIPI civile: profilo `AirportMil` con 26 sezioni, release e ciclo AIRAC
propri, elenco nazionale `/services/vsop/mil`, viewer + editor dedicati, filtro pilota/ATC per sezione.
Il primo SOP vero — **LIPI Rivolto** — è caricato in bozza.

### R1 🟢 APERTO — le figure dei SOP: lavoro manuale, non codice

Su LIPI restano **tre** sezioni incomplete perché nell'originale sono disegni: `taxiing` (flussi del
piazzale e dell'area di manovra), `arming` (posizioni per pista 06 e 24), `vfrjet` (circuiti e porte).
Il testo attorno c'è, e ogni sezione porta una nota che dice che manca la figura — così una sezione
incompleta non si confonde con una vuota.

`MediaAsset`/`IMediaStore` ci sono già: serve estrarre le immagini dai PDF e caricarle. È lavoro di chi
redige, non di chi programma.

### R2 🟢 APERTO — la trascrizione di LIPI va RILETTA

La prima stesura italiana è mia, non di un controllore militare, ed è in bozza apposta: **non è
pubblicata**, e non deve esserlo prima che qualcuno che conosce il campo l'abbia riletta. Il badge
«traduzione non revisionata» dice la stessa cosa al lettore inglese.

⚠️ ✅ **Chiuse la sera del 28 agosto.** Erano le due rese che il glossario non copriva, perché sono
**parole dentro una frase** e non segmenti interi: «the **camp**» per «il campo» e «the **cocking** and
disarming positions» per «armamento/disarmo». Sono state il caso di prova del meccanismo dentro le frasi
(**Q3**) e sono fra le sue voci di partenza. ⚠️ Le traduzioni **già in memoria** non cambiano da sole: dalla
pagina del glossario si preme «falle rifare», o il documento resta com'era.

### R3 🟢 APERTO — gli altri quattordici SOP

I PDF stanno in `vIPI Ivao Italy\MIL vSOP IVAO\` (fuori dal repo). Ognuno è un file come
`tools/Vipi.MilSopLoader/SopLipi.cs` più una riga nello `switch` di `Program.cs`.

⚠️ **La parte che conta non è scriverli**: è rileggerli con qualcuno che conosca il campo. E quattro dei
quindici — Amendola, Gioia del Colle, Istrana, Grosseto — sono basi di difesa aerea: su quelle la sezione
`qra` va **riempita**, non nascosta, ed è contenuto nuovo che nei PDF non c'è (carta §2).

### R4 🟢 DECISO, da fare dopo — la vSOP militare di un APP non remotizzato

`SectionProfile.AppMil` esiste e rimanda al profilo civile dell'APP; `AppMilReleaseTarget` e
`AppMilDocRoutes` sono registrati. Manca l'ingresso UI (`/services/vsop/{acc}/mil/apps`): nessuno dei
quindici SOP è di un APP, quindi non c'era niente da caricarci dentro.

⚠️ **Aggiornato il 29 agosto 2026** (§V): `AppMilDocRoutes` **dichiarava** quei due indirizzi verso pagine
che non esistono — la stessa forma del difetto già pagato al §6 della carta. Ora le quattro rotte tornano
`null`, che il contratto prevede, e sul file c'è scritto **che cosa serve quando le pagine si faranno**:
i quattro indirizzi, l'`.Include(d => d.MilSectors)` nei tre elenchi generici, un
`IFrozenSectionProvider` per `AppMil`, e la voce nel conteggio di `RegistrazioniPerFamigliaTests`. Il
test `RotteDeiDocumentiEsistonoTests` diventa rosso il giorno in cui si aggiunge un indirizzo senza la
sua pagina.

## S. L'archivio ATC mondiale — 28 agosto 2026

Carta: [feature/2026-08-28-archivio-atc-mondiale.md](feature/2026-08-28-archivio-atc-mondiale.md).
✅ **Slice chiuse**, suite verde su entrambi i TFM, verificato dal vivo contro IVAO vero
(0 ATC di divisione online, **18 fuori divisione archiviate**, endpoint e pagina guidati in Edge).
⚠️ **Consegna attesa entro il 1° settembre 2026**: è la data da cui il committente vuole che parta la
raccolta, e non c'è nessun cancello nel codice che la faccia rispettare (§S2).

Il poller smette di buttare le postazioni fuori divisione: le archivia tutte, marcate con
`AtcSession.IsOutsideDivision`. I conti della divisione non cambiano — ogni lettura che conta passa da
`AtcSessionScope.DiDivisione()`. Ritenzione **dodici mesi per tutto**; il riassunto mensile resta italiano.
Nuovi: pagina staff `/services/stats/world` e `GET /vsop/api/v1/atc/sessions`.

### S1 ✅ CHIUSA — il rapporto mondo/Italia è MISURATO

Il numero l'ha dato il committente il 28 agosto sera: il D1 dell'archiviatore del validatore archivia il
mondo **dal 2 giugno 2026** e pesa **13,35 MB**. Sono 87 giorni, cioè **0,153 MB al giorno** su quello
schema; convertiti in righe con un costo per riga fra 200 e 280 byte danno **575-805 sessioni al giorno**
nel mondo, cioè un rapporto di **10×-14×** sulle 58 italiane.

La stima a occhio (8×-12×) era **bassa di poco**. I dodici mesi passano da ~88 a **~93 MB** su MariaDB
(81-109 agli estremi). Tabelle rifatte nella carta §3.

⚠️ Resta un'ipotesi sola, il costo per riga dello schema altrui — e il campione copre **giugno-agosto**,
mesi d'estate. Fra qualche mese la misura diretta ce l'avremo in casa.

**Il database intero**, misurato lo stesso giorno tabella per tabella (carta §3-ter): **10,05 MiB** oggi, di
cui `AtcSessions` è già **metà** (4,82 MiB, 239 B/riga). A regime dopo dodici mesi: **~137 MiB su SQLite,
~230 MB su MariaDB**, di cui ~85 MB sono le righe fuori divisione.
⚠️ Il pezzo più grosso **non è il mondo**: è `AtcSessionTraffic` (~72 MiB), che nasce solo dalle sessioni
di divisione — e le sue 500 000 righe l'anno vengono dalla carta del 24 agosto, non da una misura: è il
solo numero grosso di questa analisi che non poggia su dati veri. **Da ricontrollare** quando l'archivio
avrà qualche mese.

### S2 ✅ DECISA — si ricomincia da capo, cutover nel 2027

Decisione del committente (28 agosto sera): **lo storico del Worker non si travasa.** I due archivi
restano separati, il nostro comincia da zero il **1° settembre 2026**, e il passaggio dal servizio vecchio
a questo si fa **nel 2027** — quando qui dentro ci sarà già più di un anno di dati.

⚠️ **Nessun cancello di data nel codice, ed è voluto**: una data fissa in una `if` può solo fare danno —
prima del 1° settembre toglierebbe giorni gratis, dopo non ne recupererebbe nessuno. Il 1° settembre è
una **scadenza di consegna**: se il ramo non è in produzione entro quel giorno, la raccolta comincia dopo.

⚠️ Fino al 2027 i due archiviatori girano **in parallelo**: due processi che chiedono lo stesso file allo
stesso server. È il prezzo accettato per non dipendere da un travaso.

### S3 🟢 APERTO (piccolo, non nostro di questo giro) — `StatsView.Ore` scrive la virgola a mano

`StatsView.Ore` rende `"<0,1"` come **letterale**, mentre tutto il resto formatta con `ToString("0.0")`,
cioè col punto: nella stessa colonna si legge `<0,1 h` accanto a `0.2 h`. Si vede su ogni pagina delle
statistiche, non solo su quella nuova, ed è precedente a questo giro — sta qui perché l'ho visto a schermo
verificando l'archivio.

## T. La SELECT dei duplicati che nessuno poteva eseguire — 28 agosto 2026, sera

✅ **CHIUSA.** Stava nei lavori aperti da agosto come «prima del deploy serve la SELECT dei duplicati su
`DocReleases`, o `CREATE UNIQUE INDEX` fallisce». Andandola a fare, il problema è risultato essere la nota
stessa: **quella SELECT non è eseguibile con gli accessi che abbiamo.**

- Il **3306 del server sta sul suo `localhost`** e da fuori non è raggiungibile
  (`deploy/mariadb/README.md`, prima sezione: è scritto lì dal 6 agosto).
- Sull'host **non c'è un pannello** da cui aprire una console SQL: il canale è l'FTP confinato a
  `public_atc` ([[host-reale-plesk-passenger]]).
- L'unico programma che quel database lo raggiunge è **l'applicazione stessa**.

**Quindi il controllo lo fa lei**: `ReleaseNumberPreflight.Verifica()` gira in `MigrateVipiDatabase` subito
prima di `Migrate()`, e solo se la migrazione è ancora pendente (applicata, l'indice c'è già e i doppioni
non possono esistere).

⚠️ **Non cambia l'esito, cambia che cosa si legge.** Coi doppioni l'avvio si ferma comunque — si
fermerebbe da solo — ma in `avvio-errore.txt`, il file che si scarica via FTP, c'è l'elenco di bersagli e
numeri da sistemare invece di un `Duplicate entry '...' for key '...'` che dice la chiave e non le righe.

⚠️ **Non ripara, di proposito**: rinumerare un rilascio cambia un «rilascio #N» che qualcuno può aver
già letto o citato, ed è una decisione di chi pubblica, non di una routine d'avvio.

⚠️ **Un difetto trovato dal test, non da una rilettura**: la stessa migrazione ha **due id diversi** nei due
insiemi — `20260825151953` (SQLite) e `20260825152005` (MySQL), emesse a dodici secondi di distanza. Il
controllo scritto con l'identificativo completo sarebbe stato **muto su uno dei due provider, in silenzio**.
Ora si confronta il nome senza il timbro.

**Gli altri quattro indici unici della coda non possono fallire**, e non è una speranza: `Airports.DocumentId`,
`Airports.MilDocumentId`, `AccSectors.IvaoId` e `AirportSectors.IvaoId` stanno su colonne **create dalla
stessa migrazione** che vi posa l'indice — nascono tutte nulle, e un indice unico ammette quanti nulli
vuole. `CallsignAliases` è una tabella nuova. Verificato leggendo le migrazioni, una per una.

Sul `vipi.db` di sviluppo (38 rilasci, 14 bersagli): **zero doppioni**. Della produzione non si sa, ed è
esattamente il motivo per cui il controllo ora viaggia col programma.


## U. Le autorizzazioni a livelli — 28 agosto 2026, notte

🟢 **APERTA, carta approvata, codice da scrivere.** Documento:
[`docs/feature/2026-08-28-autorizzazioni-a-livelli.md`](feature/2026-08-28-autorizzazioni-a-livelli.md).
Ramo `autorizzazioni-a-livelli`, aperto da `main` subito dopo la fusione del glossario.

**Il problema in una riga.** Il prodotto ha un interruttore solo — `IsAdmin`, **160 usi su 46 file** — e
in mezzo fra «vede tutto» e «vede le pagine pubbliche» non c'è niente. Un `IT-T01` può ridistribuire i
permessi; e non c'è modo di dare a qualcuno **le sole statistiche di divisione**, che è ciò che allo
staff serve più spesso.

**La forma nuova.** Cinque livelli **cumulativi** (`enum VipiRole { User=0, IvaoStaff=1, DivisionStaff=2,
Editor=3, Admin=4 }`), un `>=` a ogni cancello. Chi sta sopra ha tutte le prerogative di chi sta sotto —
il che risolve da solo il caso «il chief è anche membro della divisione italiana»: Editor (3) ≥
DivisionStaff (2), senza scrivere una regola in più.

Le sei decisioni del committente del 28 sera:

| | scelta |
|---|---|
| **Editor** | edita **tutto**, non la sola ACC — «il CH di Roma può dare una mano a quello di Milano» |
| **Concessioni per ACC** (`EditGrant`) | **eliminate**, entità compresa (in produzione già cancellate a mano) |
| **Statistiche personali altrui** | le vede **tutto lo staff italiano**, non i soli admin |
| **`IT-WM`** | admin, come `IT-DIR` |
| **Fondatore** | Admin per VID, indipendentemente dalla posizione staff, da `appsettings.json` |
| **Promozione** | un admin promuove e declassa per VID; `Effettivo = max(DaStaff, Override)` |

⚠️ **Il pavimento non è un controllo, è il `max`.** «Non si declassa nessuno sotto il livello garantito
dalla sua posizione staff» è ciò che `max` fa già: un declassamento sotto il pavimento è un no-op. E
siccome i no-op silenziosi sono bugie, i livelli sotto il pavimento in pagina sono **disabilitati**, col
codice staff che li garantisce scritto accanto.

⚠️ **Questa feature TOGLIE codice.** Se l'Editor edita tutto, i cinque metodi `CanEdit*`/`EnsureCanEdit*`
smettono di interrogare il database e diventano `Role >= Editor`, **sincroni**. Sparisce
`HasAnyGrantAsync` chiamato dal layout — la prima query di ogni pagina per un utente loggato, e la causa
prima delle corse sul `DbContext` di circuito ([[corse-dbcontext-diagnosi]],
[[barra-non-affonda-la-pagina]]). Non si mitiga: non c'è più.

⚠️ **Ma l'override sta in banca dati e rifarebbe lo stesso danno**, se lo si leggesse per richiesta.
Quindi `RoleOverride` (poche decine di righe, sempre) si tiene **intera in memoria** in un singleton
invalidato alla scrittura: il livello resta a **zero query per richiesta**, come oggi `IsAdmin`.

**Chi perde l'editing**, ed è voluto: tutti gli `IT-` fuori dagli otto codici di direzione — `IT-T01`,
`IT-T03`, `IT-FOC`, `IT-FOAC`, `IT-AOA1`… Gli assistenti Ops oggi editano, domani no. La risposta è una
promozione a mano, trenta secondi. **Nessuno perde una concessione**, perché in produzione non ce ne sono
più: chi editava lo faceva da admin, quindi il travaso è pulito ed è il momento giusto per farlo.

⚠️ **Torna l'elenco puntuale**, e torna il difetto per cui il 22 agosto si era scelto il jolly: un ruolo
di direzione **nuovo** nascerà DivisionStaff invece che Admin. Compromesso accettato, perché adesso
esiste la promozione a mano che allora non c'era — il difetto si sposta su qualcosa che si ripara da
dentro il prodotto in trenta secondi. La memoria [[staff-code-reali]] va riscritta di conseguenza:
la decisione del 22 agosto diventa **storia**.

⚠️ **Migrazione doppia** (SQLite + MySql, due identificativi diversi per la stessa migrazione — la
trappola di §T): con questa la coda al cutover MariaDB passa da ventuno a **VENTIDUE**.

### U1. Le due decisioni che mancavano — ✅ **CHIUSA** il 28 agosto, notte

- **VID del fondatore: `704798`**, scritto in `src/Vipi.Host/appsettings.json` (sezione `Auth`).
- **`IT-AWM` è admin**, dentro l'elenco degli otto.

### U2. Slice 0 e 1 — ✅ **CHIUSE** il 28 agosto, notte

`VipiRole` in `Vipi.Domain/Enums.cs` (valori numerici espliciti: finiranno in banca dati) e `RoleResolver`
in `Vipi.Application/Auth/`, **puro**, con **47 test** di tabella di verità sui codici staff **veri**.
Build Release `--no-incremental` 0 avvisi, suite intera verde. ⚠️ **Niente è cablato**: il prodotto si
comporta esattamente come prima, e le due liste legacy di `DivisionOptions` (`AdminRolePatterns` col jolly,
`AdminAccRolePatterns`) sono ancora quelle che decidono davvero. **Muoiono nella slice 3.**

⚠️ Tre trappole trovate scrivendo, tutte con un test che le tiene ferme: i pattern **vanno ancorati**
(`IT-DIRETTIVO` sarebbe diventato direttore); **l'ordine di valutazione è la regola** (un `IT-DIR`
combacia anche col pattern dello staff di divisione — è il valutare l'admin per primo a renderlo admin);
**l'ordine dell'enum è un contratto** (rinumerarlo lascerebbe ogni `Role >= X` compilabile e cambiato di
significato).

### U2-bis. Slice 2, le promozioni a mano — ✅ **CHIUSA** il 28 agosto, notte

`RoleOverride` (**chiave = il VID**: «una riga per persona» la garantisce la tabella), lo store EF, la
cache `IRoleOverrides` e la migrazione `PromozioniAMano` **in entrambi gli insiemi**, puramente additiva.
**19 test nuovi.** La cache si scalda all'avvio, per prima fra le manutenzioni, e un suo guasto **non
ferma l'avvio**: il fotogramma vuoto non nega niente a nessuno.

⚠️ `For()` torna `null` per «nessuna promozione», **mai** per «non lo so»: chi chiama ricade sul livello
dello staff. È la differenza fra una promozione che tarda (fastidio) e un permesso negato a chi lo ha per
ruolo (guasto).

⚠️ **Prezzo dichiarato**: una promozione fa effetto **solo dopo una ricarica**, e chi scrive deve
ricaricare. ⚠️ **La coda al cutover MariaDB è ora VENTIDUE** — id `20260828212030` (SQLite) e
`20260828212039` (MySql), la stessa migrazione con due identificativi.

### U2-ter. Slice 3, il servizio — ✅ **CHIUSA** il 28 agosto, notte

`Role`, `IsEditor`, `IsDivisionStaff`, `EnsureAtLeast(livello)`; `IsAdmin` **conserva il significato**
(`Role >= Admin`) e infatti **nessuno dei 160 usi è stato toccato**. Morti `AdminStaffCodes` e le due liste
legacy di `DivisionOptions`. La diagnostica «Chi può editare» legge ora i pattern dal `RoleResolver`.

⚠️ **DA QUI IL RAMO NON È DEPLOYABILE FINO ALLA SLICE 5.** Il comportamento è cambiato — un `IT-AOA1` non
è più admin, un `LIRR-CH` è `Editor` — ma i cancelli delle pagine guardano ancora `IsAdmin`: in questo
stato intermedio il prodotto **aprirebbe di meno**, non di più.

⚠️ **I predicati derivati e il cancello sono default sull'interfaccia**, perché ci sono **ventitré** classi
finte che la implementano: scriverli a mano sarebbe stato ventitré occasioni di sbagliare un `>=` sul
permesso più alto del prodotto. Il `max` sta in un posto solo, `RoleResolver.Effective`.

⚠️ **Due test hanno cambiato colonna, non forma** — il chief d'ACC non è più admin, e un roster di soli
chief risulta senza admin. È lì che il cambio di regola si legge. `AdminCodeTests` è diventato
`LivelloEffettivoTests`: rispondeva a una domanda che non esiste più.

### U2-quater. Slice 4 e 6, la morte delle concessioni — ✅ **CHIUSE** il 29 agosto

Via `EditGrant`, il suo repository, `GrantRow`, la tabella (migrazione `ConcessioniPerAccRimosse`, in
entrambi gli insiemi) e le otto domande che le interrogavano. Le cinque `CanEdit…`/`EnsureCanEdit…` —
**219 riferimenti** — sono diventate `IsEditor` e `EnsureAtLeast(VipiRole.Editor)`: **sincrone, zero query,
nessun parametro**.

⚠️ **La slice 6 è stata tirata avanti**: tolte le concessioni, `/admin/permissions` restava senza
contenuto. `AdminGrantsPage` → **`AdminRolesPage`**, una riga per persona, col **pavimento** dichiarato e i
livelli sotto di esso **disabilitati** — un comando che accetta e non fa niente è peggio di uno che dice di
no. Servizio nuovo `RoleAdminService`, 10 test.

⚠️ **Le tre guardie sono tre modi di perdere il prodotto**: non ci si declassa da soli, non si tocca un
fondatore, non si scende sotto il pavimento. ⚠️ **Ogni scrittura ricarica il fotogramma**, o la promozione
non fa effetto fino al riavvio.

⚠️ **Quattro test hanno perso il loro oggetto e lo dicono nel codice** invece di sparire: il più
significativo è l'E2E «se la domanda della barra fallisce la pagina esce lo stesso» — quella domanda non è
stata resa tollerante, è stata **TOLTA**.

⚠️ **La coda al cutover MariaDB è VENTITRÉ.**

### U2-quinquies. Slice 5, i cancelli — ✅ **CHIUSA** il 29 agosto

**84 cancelli spostati**, e il prodotto torna coerente. `AdminNav.Chi` → `VipiRole Minimo`, filtro in una
riga (`Authz.Role >= v.Minimo`); il default resta **Admin apposta**, così una pagina nuova nasce chiusa.

⚠️ **La barra ha smesso di fare una domanda.** `PuoModificareAsync` è sparito col suo `try/catch` che
ingoiava l'errore: quella domanda andava al database e poteva portare giù la pagina (il difetto del 24
agosto, [[barra-non-affonda-la-pagina]]). Una domanda che non tocca il database non fallisce — chiuso alla
**radice**, non mitigato.

⚠️ **Due cose restano agli admin dentro pagine da Editor**: la voce «Permessi» nella Home e l'assegnare un
incarico **a un'altra persona**. ⚠️ **Forzare il lock di un collega è sceso all'Editor**: serviva l'admin
solo perché l'admin era l'unico che editava.

⚠️ **La rete è un test per rotta**, e prova ogni voce al suo livello **e a quello subito sotto** — la metà
che conta. Serve un `TestContext` per render: bUnit congela il contenitore al primo render, e due livelli
nello stesso contesto darebbero due volte la stessa risposta.

### U2-sexies. Slice 7, la propagazione — ✅ **CHIUSA** il 29 agosto

La diagnostica «Chi può editare» racconta i **livelli** (col pallino per chi l'ha per promozione) e i
pattern di tutti e tre; ⚠️ `AnyAdmin` guarda il livello **effettivo**, non i codici — un admin per
promozione è un admin. La Guida in-app e il catalogo della ricerca non parlano più di concessioni.
Aggiornate `mappa-pagine.md`, `modello-dati.md`, `regole-ui-pagine-admin.md` e `guide/config.md` (che
documentava due chiavi `Division:*` inesistenti: ora c'è la sezione **`Auth`**). Tolte **31 chiavi di
traduzione morte**, il vocabolario delle concessioni.

⚠️ **Due memorie puntavano a `HasAnyGrantAsync` come primo sospettato** delle corse sul `DbContext`: quella
query non esiste più, e il metodo di diagnosi resta valido — cambia il sospettato.

### U2-septies. La verifica live — ✅ **FATTA** il 29 agosto, e ha trovato tre difetti

La suite era verde su tutti e quattordici i progetti. La verifica ha trovato lo stesso:

1. ⚠️ **La pagina Struttura moriva con un 500 per un Editor**: `OrphanSectorService.ListAsync` chiedeva
   ancora `EnsureAdmin()`. È **esattamente** il caso che la carta annunciava — il cancello sta in DUE sedi —
   e nessun test lo vedeva, perché nessun test apre quella pagina con quell'identità.
2. ⚠️ **Struttura e Documenti non si chiudevano affatto**: non hanno mai avuto un cancello di *pagina*, e
   l'elenco dei documenti porta bozze e documenti nascosti. Ora rifiutano prima di caricare i dati.
3. ⚠️ **Due falsi allarmi della sonda**, non del prodotto: `a.editor-btn` prende anche Guida/login/logout, e
   il «non puoi entrare» in questa applicazione si scrive in **due** modi (fascia rossa **e**
   `<p class="help">`). *Quando un numero accusa qualcosa che sta lì da mesi, il sospetto va prima allo
   strumento.*

**Come si guida l'app a un livello che non è il proprio**: `DevIdentityOptions` (sezione `DevIdentity`,
solo in Development) prende VID e posizioni staff da configurazione. Prima erano una costante nel codice,
quindi cinque livelli = cinque ricompilazioni, quindi in pratica non si verificava.

### U3. ✅ **FUSO IN `main`** il 29 agosto (merge `8d14b499`)

Ramo cancellato da locale e da `origin`; punto di ritorno locale
`main-prima-del-merge-20260829-autorizzazioni` (= `332f8814`). Build Release e suite verdi **dopo** la
fusione, che è l'unico momento in cui la prova conta.

⚠️ **Resta una cosa, e non è codice: il DEPLOY.** Quando questo `main` va in produzione, tutti gli `IT-`
fuori dagli otto codici di direzione (`IT-T01`, `IT-T03`, `IT-FOC`, `IT-FOAC`, `IT-AOA1`…) **smettono di
editare** — vedono le statistiche e basta. La risposta è una promozione a mano da
`/services/vsop/admin/permissions`, trenta secondi a persona, ma è meglio che lo sappiano prima loro che
dopo.

### U4. ⚠️ Gli E2E non girano finché l'host è acceso

Scoperto qui, ma vale per tutto il repo: `Vipi.E2E.Tests` referenzia `Vipi.Host`, e finché il processo
`Vipi.Host` è acceso (porta 5034, quello del committente — **non si spegne**) i suoi DLL sono bloccati e
il progetto **non si costruisce**. Non compare fra i `Passed!` e **l'exit code di `dotnet test` è
inaffidabile**: tre esecuzioni con lo stesso guasto hanno dato 0, 0 e 1. «Verde» si legge **contando i
progetti** nel riepilogo, non dall'exit code.

## V. La supervisione dei vSOP militari — 29 agosto 2026

Carta: [`history/audit-2026-08-29-vsop-militari-relazione.md`](history/audit-2026-08-29-vsop-militari-relazione.md).
✅ **Dieci voci, dieci chiuse.** Suite su net8: **3 570** test, otto progetti verdi (il nono, `Vipi.E2E.Tests`,
non si costruisce con l'host acceso — vedi **U4**).

La carta dei vSOP militari letta **contro il codice**, da fuori. Il filo: il documento militare era
agganciato al motore di **lettura** e non a quello di **governo**. Le quattro grosse — congelamento della
release inesistente e vista che leggeva lo snapshot **civile**; documento **invisibile** ai tre elenchi
generici per un `.Include` mancante; impatti che non avvisavano mai il gemello; eliminazione dell'aeroporto
che lasciava il documento orfano — erano lo stesso errore ripetuto: **`MilDocumentId` aveva quattro lettori
in tutto il repository**, `DocumentId` decine.

### V1 ✅ CHIUSA — la verifica a schermo su un campo MISTO, fatta

⚠️ **È la voce che conta di questa sezione.** La prova a schermo della slice 10 fu fatta su **LIPI Rivolto
perché era il SOP più corto**, e Rivolto è **solo militare**: nessun documento civile. È precisamente il
campo su cui tutti e quattro i difetti grossi sono **invisibili** — il congelamento sbagliato degrada in
«live», non c'è un gemello con cui confondersi, e il campo non ha ancora una release da elencare.

Serve una passata sul primo campo **misto** che avrà tutt'e due i documenti pubblicati — nei quindici SOP è
**LIRP Pisa** — e va guardato:

1. che la tabella **Frequenze** del vSOP militare pubblico **non** cambi ripubblicando la vIPI civile, e
   **cambi** ripubblicando il militare;
2. che il ciclo AIRAC in fondo alle due pagine sia quello della **propria** release;
3. che il documento militare compaia in `/services/vsop/admin/versions`, nella **ricerca** (col taglio
   nuovo «vSOP militari») e nelle **«Novità»** del ciclo;
4. che i **due ponti** fra le edizioni si vedano su Pisa e che su Rivolto se ne veda **uno solo**;
5. che il badge pilota/ATC compaia accanto a una sezione marcata **anche in una vIPI civile**, e che
   `?vista=pilota` filtri su aeroporto, ACC e APP come già fa sul militare.

✅ **Fatta la sera del 29 agosto**, su **LIML Linate** (scalo civile con presenza militare): creata e
pubblicata la vIPI civile, ripubblicato il vSOP militare, cambiata una frequenza nel catalogo e ripubblicato
**solo il civile**. Tutti e cinque i punti verificati.

⚠️ **E ha trovato altre TRE voci**, le peggiori del giro — un vSOP militare pubblicato che non si apriva
affatto, tre tabelle su tre rese come titoli vuoti, e il catalogo che non trovava le sezioni annidate. Sono
il §J della carta dell'audit. Il conto del giro passa da dieci voci a **tredici**.

La misura che chiude il punto, sullo stesso scalo e nello stesso istante:

```
vIPI civile   LIML_TWR  118.999   ← ripubblicata dopo il cambio di catalogo
vSOP militare LIML_TWR  118.100   ← non ripubblicata: tiene il SUO snapshot
```

### V2 🟢 APERTO — i documenti militari già pubblicati vanno RIPUBBLICATI

Se un vSOP militare fosse stato pubblicato prima di oggi, il suo snapshot **non contiene** le sezioni
derivate congelate: il provider non esisteva. La pagina pubblica ricade live — quindi non si rompe niente —
ma il congelamento comincia a valere solo **dalla prossima release**. È la stessa trappola già pagata due
volte con le altre famiglie: *le chiavi nuove si leggono live finché non si ripubblica*.

⚠️ Oggi la coda è **vuota** (nessun vSOP militare è pubblicato). La voce resta scritta perché se ne pubblica
uno prima di leggere questa riga, la risposta è: **Ripubblica**, non «indagare».

### V3 🟢 APERTO — cercare gli altri legami «gemelli» prima che facciano lo stesso

La lezione di questa sezione non è sui vSOP militari: **un legame nuovo va cercato con `grep`, non con la
memoria**. Il difetto si sarebbe visto in un minuto contando i lettori della colonna nuova contro quelli
della colonna che imita.

Da fare una volta, su tutto il repository: per ogni colonna che è il **gemello** di un'altra
(`MilDocumentId` ↔ `DocumentId` è l'unica di oggi), contare i due insiemi di lettori e spiegare ogni
differenza. Dove la differenza non si spiega, è un difetto.

### V4 🟢 APERTO — «Regole piste» esce «Slope rules» nella vIPI CIVILE d'aeroporto

Visto durante la verifica §V1, sulla pagina inglese della vIPI civile di Linate: il titolo di sezione
«Regole piste» è reso **«Slope rules»**. È la stessa resa che il 28 agosto si era chiusa per i titoli del
profilo **militare** («Piste» → *Slopes*), seminandoli in memoria come **umani**
(`Vipi.Application/Translation/TitoliUfficiali.cs`). Quel seme copre i **ventisei titoli militari**; i titoli
del catalogo **civile** non li mette nessuno.

#### ⚠️ La misura, prima di decidere quanto è grosso: è UNA voce

Interrogata la memoria di traduzione del `vipi.db` di sviluppo su tutti i titoli del catalogo civile
(29 agosto 2026):

| titolo | resa in memoria | origine | |
|---|---|---|---|
| **Regole piste** | ***Slope rules*** | Machine | ⚠️ **sbagliata** |
| Aree regolamentate | *Regulated areas* | Machine | ok |
| Configurazioni | *Configurations* | Machine | ok |
| Coordinamenti | *Coordination* | Machine | ok |
| Frequenze | *Frequencies* | Machine | ok |
| Separazioni | *Separations* | Machine | ok |
| Separazioni radar | *Radar separations* | Machine | ok |
| AOR · SID · VFR | invariati | Machine | ok, ma **per caso** |
| MRVA | invariato | **Human** | pinnata a mano il 27-ago |
| Procedure generali · Validità e revisione | corrette | **Human** | già seminate: coincidono con due titoli militari |

**Su tredici, una sola è sbagliata.** Non è «seminare il catalogo civile»: è **una riga**, più una decisione
sulle sigle.

#### Che cosa fare

1. **La riga.** In `TitoliUfficiali.Sezioni`, `("Regole piste", "Runway rules")`.
   ⚠️ Quella tabella oggi si chiama «dai quindici SOP militari» e il suo commento lo dice: aggiungendoci un
   titolo civile **va riscritto il commento**, o la prossima persona toglie la riga perché «non è di un SOP».
   La ragione che le tiene insieme non è «militare», è **«di questo titolo conosciamo l'originale»**.
2. **Le sigle — decisione da prendere, non ovvia.** `AOR`, `SID`, `VFR` oggi tornano invariate ma sono
   `Machine`: nessuno le protegge, e a un cambio di motore possono diventare altro (`MRVA` è `Human` proprio
   perché una volta è tornata «Minimum vectoring»). Pinnarle come umane costa tre righe e chiude la classe;
   lasciarle com'è è una scommessa sul motore. ⚠️ Sono in `Termini`, non in `Sezioni`, se si sceglie di
   pinnarle: `Sezioni` è documentata come «titoli di sezione», e `SID` è anche una parola dentro le tabelle.

#### ⚠️ Due cose sul meccanismo, e la seconda l'avevo scritta sbagliata

- ✅ **Una voce nuova SOSTITUISCE la resa della macchina, non la scavalca.** `SeminaAsync` confronta con le
  impronte **umane** (`LoadHumanHashesAsync`) e non con tutte, apposta: «guardando *tutte* il seme non
  avrebbe corretto niente su un sito che aveva già tradotto — cioè esattamente il caso in cui serve». E
  `EfTranslationMemory.SaveHumanAsync` fa **upsert**: se la riga c'è, ribalta `Origin` a `Human` e riscrive
  il testo. Il seme parte a **ogni giro** di traduzione (`TranslationFillHostedService`), prima di chiedere
  alla macchina, ed è idempotente.
  ⚠️ **Correzione di quanto scritto la sera del 29 agosto**: qui **NON** serve il tasto «falle rifare». Quel
  tasto è del **glossario di fraseologia** (§Q3), che è l'altro meccanismo — i termini *dentro* una frase —
  e lì sì che una voce nuova non tocca ciò che è già in memoria.
- ⚠️ **I documenti già PUBBLICATI restano com'erano.** La release congela le traduzioni nello snapshot
  (`ConTraduzioniCongelateAsync`), quindi «Slope rules» resta nelle release in vigore finché il documento non
  si **ripubblica**. È la stessa trappola di sempre, e qui pesa poco: [[riprendere]] ricorda che **i documenti
  vanno comunque tutti eliminati e ricreati**.

#### Come si verifica che è fatto

La memoria si guarda direttamente — è la prova più corta e non richiede di pubblicare niente:

```sql
SELECT SourceText, TargetText, Origin FROM TranslationUnits
WHERE SourceLang='it' AND TargetLang='en' AND SourceText='Regole piste';
-- atteso: Runway rules | Human
```

E a schermo, sulla vIPI civile di un aeroporto **ripubblicato**, in inglese: la sezione si chiama
*Runway rules*. Il campo di prova buono è **LIML Linate** — durante §V1 ci è stata creata e pubblicata una
vIPI civile apposta, e la sezione «Regole piste» c'è.

## W. Il convertitore di coordinate — 29 agosto 2026

> **Carta:** [`docs/feature/2026-08-29-convertitore-coordinate.md`](feature/2026-08-29-convertitore-coordinate.md)
> **Ramo:** `convertitore-coordinate`, dieci slice, **143 test nuovi** (suite a 3 984 su nove progetti).
> Release verde su net8 e net10, 0 avvisi. Verifica dal vivo fatta (§14 della carta).

### W1 ✅ CHIUSA — il servizio esiste e funziona

`/services/coordinates`, riservato a **DivisionStaff** e superiori. Legge tredici forme di coordinate
(DMS Aurora puntato e compatto, DMS coi simboli, coi due punti e a spazi, gradi e primi decimali, ARINC,
decimali con segno o emisfero, `lat:lon` del DB, CSV, sectorfile a punti e a segmenti, JSON, KML/KMZ) e
scrive nei due formati chiesti — col sectorfile in **due forme**, l'elenco dei punti (default) e i segmenti.

Il motore è **puro** e sta in `Vipi.Application/Coordinates`: nessuna tabella, nessuna migrazione, nessuno
stato. La mappa è quella dell'AoR, senza un motore nuovo.

### W2 🟢 APERTO — il ramo NON è fuso

La fusione è una decisione del committente, non un passo tecnico. Il ramo è locale: `convertitore-coordinate`,
undici commit sopra `main`.

### W3 🔵 FUTURO — le due cose rinviate dal committente

Entrambe fanno parte del «ragionamento più ampio» annunciato il 29 agosto, e **non** sono accennate in pagina:

- **salvare l'area convertita** fra le aree regolamentate (o mandarla al Bridge Aurora): il motore è puro e
  non ha nulla che impedisca di chiamarlo da altrove, ma è un'altra feature con un'altra carta;
- **incollare un `italy.restrict` intero** e scegliere l'area da un elenco: oggi il selettore compare solo se
  l'ingresso porta davvero più aree, e il file vero ne ha 2 222 righe.

⚠️ Se un giorno il convertitore avrà un **endpoint HTTP** (per il Bridge, per esempio), il cancello va messo
lì **lo stesso giorno**: oggi la sola sede è la pagina, perché il motore non fa I/O e non c'è un servizio da
chiudere.

## X. L'edizione giusta per il campo, e le tre colonne del vSOP militare — 29 agosto 2026

> **Carta:** [`docs/feature/2026-08-27-vsop-militari.md`](feature/2026-08-27-vsop-militari.md) **§11**
> (§11a le colonne, §11b le due guardie gemelle, §11c la verifica sui duplicati, §11d le trappole).
> **Test nuovi:** 10 (8 in `Vipi.Infrastructure.Tests`, 2 teorie in `Vipi.Ui.Tests`). **Nessuna migrazione.**

Tre richieste del committente e una verifica che ne è venuta fuori.

### X1 ✅ CHIUSA — il vSOP militare non aveva le due colonne laterali

Era l'**unico dei cinque viewer** senza `doc-layout`: niente indice a sinistra, niente riquadro dei
collegamenti a destra, su un documento che ha nove sezioni radice e venti figlie. Ora ha le tre colonne come
gli altri quattro, e `reading-cap` è caduto insieme (dichiarava un tetto che `.wrap:has(.doc-layout)` toglie
già).

Il rail porta ciclo AIRAC **del documento mostrato**, ATC online sul campo (ATIS escluso) e i collegamenti,
fra cui il ponte verso la vIPI civile — **solo se esiste**.

⚠️ **Anche il rail civile ora mostra il vSOP militare in BOZZA, ma solo allo staff.** Il documento militare
adesso nasce dall'editor civile e nasce in bozza: col gate «pubblicato» chi l'aveva appena creato non aveva
modo di tornarci dalla pagina civile.

**Presidio:** `DueColonneSuOgniDocumentoTests` — due teorie su tutti e cinque i viewer, per nome.

### X2 ✅ CHIUSA — quale edizione può esistere su quale campo

Due guardie **gemelle**, nei servizi e non nelle tendine:

| campo | vIPI civile | vSOP militare |
|---|---|---|
| **solo militare** (Aviano, Ghedi, Decimomannu, Rivolto) | **non nasce** | nasce subito |
| **misto** (Pisa, Linate, Ciampino) | nasce normalmente | **solo dopo** la civile |

⚠️ La civile deve **esistere**, anche solo in bozza: pretenderla pubblicata bloccherebbe il lavoro parallelo
sulle due edizioni.

⚠️ La prima guardia blocca la **nascita**, non l'apertura: una civile già esistente su un campo marcato solo
militare dopo continua ad aprirsi, o la via d'uscita sarebbe murata.

⚠️ L'anagrafica si legge dal **database** (`IAirportRepository.GetMilitaryStateAsync`), non da
`IStationResolver.Airport`: quella cache è `scoped`, cioè vive quanto il **circuito**, e può avere ore.

Le strade, dopo: «Nuovo documento» dichiara «solo militare» **nella tendina** e su quei campi crea il vSOP e
ci porta; l'editor della vIPI civile ha il tasto **«Crea il vSOP militare»** nel rail; `/services/vsop/mil`
sui campi misti senza civile offre «Prima la vIPI civile» invece di un «Crea» che fallirebbe sempre;
l'editor civile aperto su un campo solo militare **reindirizza** a quello militare; la generazione in blocco
dei documenti d'aeroporto **salta** i campi solo militari con un motivo scritto, invece di fallire.

### X3 ✅ CHIUSA — la verifica sui duplicati, e il buco che ha trovato

Domanda del committente: creando un documento che esiste già, si crea un doppione o si finisce sull'esistente?

Quattro famiglie su cinque erano a posto (ACC, APP standalone, aeroporto e vSOP militare sono idempotenti;
la vLOA rifiuta e offre «Apri esistente»). **La quinta aveva un buco**, ed era la vLOA vista dall'altra porta:

- «Nuovo documento» confronta la **coppia di ACC** e lascia scegliere qualunque settore d'area come Home;
- la generazione da «ACC confinanti» confrontava i **SectorId**, e il suo Home lo sceglie da sé (la radice).

Su una ACC con più settori d'area bastava che la prima vLOA fosse nata sull'altro settore perché la seconda
porta non la trovasse: **due vLOA sulla stessa coppia**. E `FindVloaIdByPairAsync` — come l'editor e il
pubblico trovano la vLOA di una coppia — fa `FirstOrDefault`: ne apre una senza un criterio, l'altra resta
invisibile pur potendo avere release pubblicate.

Ora le due porte fanno la stessa domanda. ⚠️ La **direzione** continua a contare: LIRR→DTTC e DTTC→LIRR sono
due vLOA legittime, e c'era già un test a difenderlo.

### X4 🟢 APERTO — `Vipi.E2E.Tests` non è stato compilato

Il `Vipi.Host` era **acceso** durante il lavoro (PID 10988) e tiene bloccati i `.dll`: il progetto E2E non
compila e, come già scritto in questo elenco, sparisce dal riepilogo **in silenzio** con exit code 0. Gli
altri **otto** progetti sono verdi su net8 e net10. Va rifatto a host spento, insieme al riavvio già in coda.

### X5 🟢 APERTO — la verifica a schermo

La suite non vede le tre colonne renderizzate né i percorsi di creazione dal browser. Da provare dal vivo,
sul solito campo **misto e pubblicato** (la lezione di §V1): la vIPI civile di Pisa con il tasto «Crea il
vSOP militare», l'elenco militare che offre «Prima la vIPI civile» su un campo misto vergine, e
«Nuovo documento» su un campo solo militare.

### X6 ✅ CHIUSA — il ponte al civile si accende su «esiste», non su «pubblicata»

Trovata alla prova a schermo su `/services/vsop/limm/mil?icao=LIML`. `HasPublishedCivilAsync` è diventato
`GetCivilEditionAsync` → `CivilEdition(Esiste, Pubblicata, SoloMilitare)`: al pubblico il ponte vuole la
release, allo staff basta la bozza. Carta §11e.

### X7 🟡 DATO DA SISTEMARE — LIML ha un vSOP militare PUBBLICATO e nessuna vIPI civile

Non è un difetto di codice: il documento è del **28 agosto**, di prima della guardia di §11b, e la guardia
non ripara il passato. Misurato in archivio il 29 agosto:

| campo | presenza mil. | solo mil. | vIPI civile | vSOP militare |
|---|---|---|---|---|
| LIBG Grottaglie | sì | **sì** | — | #24, pubblicato |
| LIBN Lecce Galatina | sì | **sì** | — | #27, bozza |
| **LIML Linate** | sì | **no** | **manca** | **#25, PUBBLICATO** |
| LIMN Cameri | sì | no | #28 | #29 |

⚠️ **Solo LIML è fuori regola.** LIBG e LIBN sono marcati `IsMilitaryOnly`, quindi la civile non deve
esistere: se in realtà hanno traffico civile, la correzione è **togliere la spunta «solo militare»** in
Struttura, e da quel momento l'avviso comparirà anche su di loro. LIMN è nato **nell'ordine giusto** (civile
alle 09:17:15 UTC, militare alle 09:17:20): è la guardia che ha funzionato.

Il caso ora si **vede**: callout sul viewer militare e pill rossa nell'elenco nazionale, solo per lo staff,
entrambi col tasto che apre l'editor della vIPI civile — che è anche ciò che la fa nascere. ⚠️ **Non si crea
niente da soli**: creare un documento al posto di una persona è la stessa categoria di errore appena chiusa.

### X8 ✅ CHIUSA — il filtro «Tipo» di `/services/vsop/versions` non aveva i vSOP militari

Trovato a schermo. L'elenco li **mostrava** (icona, etichetta, riga: sistemate in §V) ma il menu Tipo restò
ai quattro civili, quindi non si potevano **isolare**. Stessa forma dei difetti di §V — mostrare è lettura,
filtrare è governo — in scala ridotta.

⚠️ `AppMil` resta fuori apposta: quel documento non esiste e le sue rotte tornano `null`. Va aggiunto
**insieme** alle sue pagine. Presidio: `FiltroPerTipoCompletoTests`, che deriva l'elenco dai **descrittori di
rotta** invece di ricopiarlo. Carta §11f.

### X9 ✅ CHIUSA — il quarto riquadro sulla pagina di una ACC

`/services/vsop/{acc}` offriva Aeroporti, APP e vLOA; i vSOP militari di quei campi si raggiungevano solo
tornando all'ingresso. Ora c'è la quarta scheda: stesso gate delle altre tre, stesso elenco `managed`,
nessuna query in più. Compare **solo se ce n'è almeno uno**, perché l'edizione militare non è una famiglia
che ogni ACC ha. Verificato sui dati veri: `/services/vsop/limm` mostra LIML, `/services/vsop/libb` mostra
LIBG. Carta §11g.

⚠️ **Non ha un test automatico**: è presentazione su un elenco già filtrato, e il gate è la stessa espressione
usata tre volte sopra nello stesso file. Va guardata a schermo insieme a §X5.

### X10 ✅ CHIUSA — i vSOP militari su `/services`, e l'hub riordinato

Ordine chiesto dal committente e ora presidiato da un test: vSOP civili → **vSOP militari** → statistiche
ATC → Aurora Profile Swapper → riga **«Staff di divisione»** → convertitore di coordinate. Va dal documento
allo strumento.

⚠️ **Ribalta la decisione di §5 della carta militare** («niente card su `/services`»). La regola
architetturale resta vera, ma perdeva sul lettore: chi cerca un vSOP militare non sa di dover entrare prima
nella documentazione civile. La scheda è marcata `a.choice.shortcut` — **non è un servizio, è una porta
dentro a uno** — così `ServicesHomeTests` continua a pretendere un solo segmento sotto `/services` per tutte
le altre. ⚠️ **Marcare invece di allargare**: senza il segno quel test sarebbe stato cancellato per far
entrare un'eccezione, e da lì in poi non avrebbe più protetto nessuno. Carta §11h.

## Y. Le tabelle del vSOP militare — 29 agosto 2026, notte

> **Carta:** [`docs/feature/2026-08-27-vsop-militari.md`](feature/2026-08-27-vsop-militari.md) **§12**
> (12a il payload nei figli, 12a-bis l'indice, 12b le decisioni del committente, 12c le soglie pista,
> 12e le radioassistenze, 12f gli alternati).

Otto richieste del committente sulle sezioni del vSOP militare: sette diventano **tabelle** al posto della
prosa libera, una è l'indice. L'ottava — il BOAT — è stata **ritirata dal committente** dopo un ricontrollo,
e torna separatamente.

**Le cinque decisioni prese prima di scrivere** stanno tutte in carta §12b: l'anagrafica condivisa delle
radioassistenze, **la fonte vince sempre** (un campo che viene dalla sorgente non è modificabile), identità
`codice + tipo`, concorrenza a **ultimo-che-scrive-vince** ma **per campo** e con vecchio+nuovo nel registro,
rilevamento e distanza degli alternati **a mano**.

### Y0 ✅ CHIUSA — il payload di una sezione non scendeva nei figli

⚠️ **Il blocco che stava davanti a tutto.** `GetSectionBlockJsonAsync` / `SaveSectionBlockJsonAsync`
cercavano la sezione con `ParentSectionId == null`: **solo fra le radici**. Nel profilo `AirportMil` venti
sezioni su ventisei sono figlie, e ci stanno dentro *tutte* le tabelle chieste — su quelle il salvataggio
sollevava «Sezione assente» e la lettura tornava `null`.

⚠️ **Terza occorrenza della stessa assunzione**: `SectionCatalog.Find` (§V), il corpo derivato reso solo
dalle radici (§V), e ora il payload. Quando una famiglia introduce un annidamento che le altre non hanno,
vanno cercate **tutte** le query che dicono `ParentSectionId == null` o `Depth == 0`.

**E un secondo difetto, latente**: il payload viveva nel `BodyJson` del **primo blocco**, convenzione scritta
in cinque file. Sulle sezioni militari — che `MilSopLoader` riempie di prosa — «il primo» è «quello che oggi
sta in cima»: chi avesse scritto una premessa sopra la tabella l'avrebbe vista **svuotarsi**, senza errori.
Ora la domanda è una sola e uguale nei due versi (`SectionPayload.Read` in lettura): **il primo blocco che un
payload ce l'ha**; in scrittura, in mancanza, un blocco **senza prosa** (il segnaposto della nascita), e solo
in ultima istanza uno nuovo **in coda**. ⚠️ Un blocco di prosa non si tocca mai.

**2 test nuovi**, nessuna migrazione. Verdi: 989 + 1 555 + 852 su net8.

### Y1 ✅ CHIUSA — l'indice mostra le sotto-sezioni (S1)

Vale in **due** navigazioni, e in tutt'e due l'elenco era delle sole radici: l'indice del **viewer** — che
stava scritto **quattro volte** e ora è `DocumentToc`, un componente solo — e il **menu-sezioni degli
editor**. Una sezione con figlie diventa un `<details>` aperto, e il titolo del padre **resta un link**.

⚠️ `EditorTocItem.Level = 3` e la classe `.lvl3` **esistevano già e non le usava nessuno**: cablaggio, non
disegno nuovo. ⚠️ Che il clic sul titolo non chiuda l'elenco è merito di `wireAnchors` (`preventDefault` su
ogni «#id»), non del CSS: commutare è l'azione di *default* del clic su un `<summary>`. ⚠️ La **vLOA** ha
dovuto passare `SlotsOf` anche all'indice — le due direzioni le disegna il corpo da sé e **non hanno un
id** — e a prenderla è stata la rete che contava i titoli duplicati.

⚠️ **Un carattere di controllo nel CSS non fa fallire un test: fa cadere l'host dei test.** Uno 0x15 finito
in `vipi-theme.css` al posto del chevron ha piantato `Vipi.Assets.Tests` — che quel file lo **minifica
davvero** — e l'ha chiuso con «Test host process crashed», senza nominare file né riga. Se una suite che non
c'entra col lavoro in corso si pianta, il sospetto è **quel che il lavoro ha scritto su un file che quella
suite legge**.

**12 test nuovi**, la proiezione del menu estratta in funzione pura (`EditorTocProjection`).
Verdi: Ui **864**, Application **1 555**, Infrastructure **989**, Assets **52**; Release verde su entrambi i TFM.

### Y2 ✅ CHIUSA — l'anagrafica delle radioassistenze e la sua tabella (S2a, S2)

Una tabella `Navaids` **di divisione**, e una sezione di documento che **non contiene i valori**: il
documento dice quali radioassistenze cita e in che ordine, l'anagrafica dice quanto valgono. È quel che ha
chiesto il committente — «si memorizzano nel DB così quella radioassistenza esce uguale ovunque».

⚠️ **La sorgente quel dato ce l'aveva già** e noi lo buttavamo via a ogni giro: `AEA;111.65;lat;lon;0;2;54Y;`
sono codice, frequenza, coordinate **e canale**, e il parser teneva le prime due cose. Misurato sul repo:
**128 VOR, 30 NDB, 26 col canale** — i VORTAC/TACAN, cioè i militari.

⚠️ La sezione è diventata **`Derived`**, e non è una riclassificazione estetica: solo le derivate le cattura
`FrozenSectionScan`, quindi solo così una release **fotografa** la tabella. È l'unica sezione congelata i cui
valori stanno fuori dal documento *e* fuori dal profilo dello scalo. ⚠️ E **`HostAndBlocks`, non `Host`**: con
`Host` puro sparirebbe la prosa dei SOP già caricata.

⚠️ **Un campo che viene dalla sorgente non ha la casella.** Mostrarla e poi rifiutare il salvataggio sarebbe
peggio che non mostrarla; e quando il rifiuto capita lo stesso, la riga lo **dice** — la scrittura torna un
esito, e per questo il callback è un `Func<…, Task<NavaidWrite>>` e non un `EventCallback`.

⚠️ Un giro d'import **saltato** (categoria esclusa, o catalogo vuoto) **non consuma il gate**: la pagina
Sorgenti direbbe «ultimo giro riuscito: adesso» su un giro che non ha letto niente.

**67 test nuovi**, due migrazioni (SQLite + MySQL). Verdi: Application **1 597**, Ui **864**,
Infrastructure **1 017**, Domain 117, Assets 52, Hosting 57; Release verde su entrambi i TFM.

### Y3 ✅ CHIUSA — gli aeroporti alternati (S3)

Quattro colonne: aeroporto (ICAO e nome), radioassistenze nella forma `MNL VORTACAN - 99Y (115.25)`,
rilevamento e distanza — scritti come **numeri**, con l'unità messa dalla resa.

⚠️ **La differenza con le Radioassistenze**: là i valori erano di un'anagrafica condivisa, qui rilevamento e
distanza sono **del documento** — il rilevamento di Grottaglie *da Amendola* non è un fatto di Grottaglie.
Nomi e radioassistenze vengono invece da **due cataloghi diversi**, e nessuno dei due appartiene al
documento: per questo anche questa sezione è `Derived` e la release la fotografa.

⚠️ **Il nome dello scalo si porta dietro nel documento**: un alternato è spesso **estero** e il nostro
archivio tiene i soli scali italiani. Una pagina pubblica non può dipendere da una chiamata a IVAO per
stampare una cella. Quando lo scalo *è* dei nostri **vince l'archivio**.

⚠️ `IAirportNameLookup` ha **due metodi**, ed è la differenza fra leggere e scrivere: chi mostra guarda solo
l'archivio, chi **aggiunge** interroga anche la sorgente — una chiamata, chiesta da una persona. Se la
sorgente non risponde la riga si aggiunge **lo stesso, senza nome**.

⚠️ Per citare una radioassistenza **basta il codice**: la natura la sa l'anagrafica. La domanda torna
legittima solo se lo stesso codice esiste con più nature (`DEC` è un VOR *e* un NDB).

**27 test nuovi**, nessuna migrazione. Verdi: Application **1 619**, Ui **864**, Infrastructure **1 022**.

### Y4 ✅ CHIUSA — le coordinate delle soglie pista (S4)

I tre campi (lat, lon, elevazione) risalgono la catena `RunwayDto` → `SourceRunway` → entità → vista, e il
vSOP militare li mostra in una **seconda tabella** sotto quella delle piste — a parte, perché quella ha già
otto colonne e una coordinata sessagesimale è larga il doppio di una cella.

⚠️ **Il difetto vero non era l'import, era il SALVATAGGIO**: `SaveRunwaysAsync` cancella e riscrive le righe,
e le soglie non passano dall'editor — senza la conservazione per ident sarebbero sparite al primo salvataggio
di una colonna qualsiasi, per tornare solo al re-import successivo. Nessun errore, nessun avviso. La
conservazione sta nel **repository**, non nella buona memoria del chiamante.

⚠️ **Resta da fare un giro di re-import**: la tabella nasce vuota su tutti i campi, e il bottone bulk sta in
`/services/vsop/admin/airports`. Finché non lo si preme, la sezione dice che i dati non sono ancora arrivati.

**8 test nuovi**, due migrazioni: quelle in coda diventano **VENTICINQUE**.

### Y5 ✅ CHIUSA — nominativi, parcheggi e le attività delle aree (S5, S6, S7)

Le tre tabelle scritte a mano, e sono di una **specie diversa** dalle due di prima: qui non c'è niente da
risolvere su un catalogo, quindi restano `Editorial` e la release le fotografa già copiando i blocchi.
«Nominativi» e «Parcheggi» hanno la stessa forma — colonne fisse dal profilo, celle libere — e quindi **un
componente per tutt'e due**. Le aree hanno la tabella sotto la mappa, con nome e limiti presi dalle aree
**scelte** e l'attività coi gettoni delle WTC.

⚠️ **Selezione e attività vivono nello stesso oggetto JSON** (perché il lettore condiviso da ACC e APP legga
ancora la selezione), e il salvataggio della selezione serializzava solo quella: ogni chip aggiunta o tolta
avrebbe cancellato tutte le attività, senza un errore e senza che chi le ha scritte tocchi mai quella tendina.

**29 test nuovi**, nessuna migrazione.

### Y6 ✅ CHIUSA — «il nome del file non dice il tipo», e la pagina Radioassistenze

⚠️ **L'osservazione del committente ha fatto cadere il modello di §12b**, e sotto c'erano tre difetti.
`itvor.vor` non è «i VOR»: contiene VOR, TACAN e VORTAC insieme — **GRO ci sta due volte**, un VOR a 109.85 e
un TACAN puro col solo canale 35Y — e nemmeno il canale distingue (115.25 è la frequenza *appaiata* di 99Y).

1. ⚠️ **Il TACAN di Grosseto non era mai arrivato, e diciassette NDB nemmeno** (10 su 27 in archivio):
   l'import passava dal **catalogo dei punti**, che toglie gli omonimi tenendo la prima occorrenza. Un
   contenitore fatto per suggerire nomi non è un'anagrafica. Ora il catalogo ha **due viste** dello stesso
   dato: deduplicata per la completion, tutte le righe per l'anagrafica.
2. ⚠️ **Un gemello vuoto creato in silenzio** scrivendo un tipo diverso da quello del file.
3. Il modello: identità = **codice + famiglia + canale**, famiglia = la **banda** (VHF/NDB, che il file
   attesta), tipo **editoriale** e null quando nessuno l'ha detto — in tabella un **trattino**, mai un ripiego.

La tabella si è **svuotata e rifatta** (decisione del committente). ⚠️ La migrazione azzera anche lo **stato
d'import**, o il giro gestito non ripartirebbe per ventiquattro ore: svuotare e riempire sono lo stesso atto.

**Misurato dopo la correzione, sul sectorfile vero: 149 righe** (122 VHF + 27 NDB) contro 132, GRO in due
righe distinte, DEC in due.

**La pagina `/services/vsop/admin/navaids`**, per gli **Editor**: tutte le righe in un posto solo, il filtro
**«senza tipo»** che è la lista di lavoro (121 righe aspettano una persona), e l'**unico** posto da cui si
elimina — con due guardie: non si tocca quel che manda la sorgente (tornerebbe), e non si toglie una riga
**citata** da un documento (sparirebbe da sotto una tabella pubblicata; la pagina dice **chi** la cita).
⚠️ Non si chiama «fix»: nel prodotto quelli sono i punti di riporto, ed è l'ambiguità che ha causato il giro.

**28 test nuovi**, due migrazioni (le in coda diventano **VENTISEI**). Verdi: Application 1 640, Ui 875,
Infrastructure 1 047, Assets 52.

### Y12 ✅ CHIUSA — il tasto d'import, e la forma delle sei tabelle

Due richieste del committente: *«vorrei il tasto per importare i fix anche in `/admin/navaids`, così com'è in
Sorgenti»* e *«nei documenti militari molti degli editor sono molto rudimentali, puoi fare una revisione e
dargli lo stesso stile del resto?»*. Carta: `docs/feature/2026-08-27-vsop-militari.md` §12n.

**Il tasto** sta in testata (agisce sulla pagina, non su una riga) e l'esito è un chip in testata, non una
fascia che spinge in giù la tabella.

⚠️ **Non è lo stesso giro del notturno**: `RunNowAsync` **riscarica** la sorgente prima di leggerla. Chi
preme un tasto d'import lo preme perché il sectorfile è cambiato *oggi*, e sulla copia in memoria — vecchia
fino a ventiquattro ore — la risposta sarebbe «0 create, 0 aggiornate» con la riga nuova già sul repository.

⚠️ **Il giro lo timbra il CORPO** (come `AccImportUseCase`), o la pagina Sorgenti direbbe «ferma da tre
giorni» di un'anagrafica riempita un minuto fa. E lo timbra **solo** quando la sorgente ha parlato: i due
giri saltati — policy che esclude, sorgente muta — non timbrano niente.

⚠️ **Le tre risposte non si riassumono in «fatto»**: *esclusa dalla policy* è una decisione, *sorgente muta*
è un guasto, il giro riuscito dice **quanto** ha portato. «Fatto» e basta, e nessuno si accorge mai che il
repository è stato spostato.

**Il «rudimentale» aveva una causa, e non era il gusto**: tutt'e sei le tabelle erano `cfg-table`, che non è
una tabella generica — è quella delle «Configurazioni operative» e **cabla le larghezze su quattro colonne**
(26/38/18/18%). Sull'anagrafica, che ne ha otto, le prime quattro si prendevano tutto e le altre finivano a
zero; su nominativi e parcheggi le colonne le decide il **profilo**, quindi cablarle è sbagliato per
costruzione. ⚠️ E le larghezze in linea coprivano il caso a metà: valgono per il `th`, non per il `td`.
È **la stessa diagnosi che le SID avevano già pagato**, ritrovata un mese dopo: ora c'è un test che la
presidia su tutti e sei i file.

Il resto è la ricognizione del ramo di modifica applicata a queste sei: la fascia azzurra sempre a schermo
diventa **pastiglia + «?»** (stessa frase, stessa chiave), le barre d'aggiunta prendono le etichette
maiuscole delle altre, i tasti di riga sono gli stessi rimpiccioliti dal foglio, l'eliminazione passa da
`InlineConfirm`, e la barra dei filtri della pagina è quella di Struttura e delle SID.
⚠️ **Un «?» da solo non si mette**: sugli alternati era diventato un segno isolato in fondo a una testata
piena di comandi, ed è tornato una riga sotto la barra d'aggiunta.

**Verificato a schermo** (1600px, tema scuro, sectorfile vero): «Import fatto: 0 create, 0 aggiornate, 149
invariate (su 149 righe dal sectorfile)» in chip verde, nessuna `cfg-table` rimasta, **zero** stili in linea
nelle sei tabelle, niente scorrimento orizzontale, console e rete pulite. ⚠️ E la prova ha trovato una cosa
che i test non vedono: la colonna «Coordinate», unica senza larghezza, si prendeva **824px su 1600**.

**Nessuna migrazione** (restano **VENTISEI**). 30 test nuovi: Ui **899**, Infrastructure **1053**,
Application 1640, Assets 52, E2E 252 — nove progetti su nove, Release verde sui due TFM.

✅ **Fuso in `main`** (`e99b8e14`) per avanzamento diretto, ramo cancellato da locale e da `origin`.

### Y10 🟢 DA FARE — quel che resta di §Y

**Una cosa sola, ed è del committente: il BOAT**, che lui stesso ha ritirato («ho scoperto essere più
complicato di quanto mi aspettassi») e che va ripreso quando avrà deciso come si scrive. Tutto il resto di
§Y è chiuso e in `main`.

✅ Il giro di **re-import** delle piste **l'ha fatto il committente** il 30 agosto 2026: le coordinate delle
soglie non sono più vuote qui. ⚠️ Ma **in produzione va premuto di nuovo** (`/services/vsop/admin/airports`),
dove le colonne nascono vuote esattamente come nascevano qui: un aeroporto importato prima di §Y4 non ha
soglie finché non si preme quel tasto, e la sezione lo dice invece di non comparire.

⚠️ **Al deploy**, e vale la pena rileggerlo prima di premere: la migrazione della correzione del modello
**svuota** l'anagrafica delle radioassistenze e azzera lo **stato d'import**, quindi il primo avvio la rifà
da zero. Ci mette un minuto, e nel frattempo le tabelle che la citano sono vuote. Dopo, le righe VHF **senza
tipo** (misurate **122** sul sectorfile di oggi) aspettano una persona: si trovano col filtro «senza tipo»
della pagina Radioassistenze, o si riempiono col tasto d'import e poi a mano.

⚠️ E le migrazioni in coda al cutover MariaDB sono **VENTISEI**: sei le porta §Y (anagrafica
radioassistenze, coordinate soglia e la correzione del modello, ognuna emessa per i due provider).

✅ **Tutto fuso in `main`** — `main` = **`e99b8e14`**, spinto, e i rami di §Y (`tabelle-vsop-militari`,
`anagrafica-radioassistenze`, `tasto-import-e-forma-tabelle`) sono **cancellati** da locale e da `origin`:
**non c'è più nessun ramo con lavoro fuori**.

### Y7 ✅ CHIUSA — la verifica a schermo su LIMN Cameri, e i tre difetti che ha trovato

⚠️ **Per la terza volta il campo MISTO ha trovato le cose vere**, e nessuna era visibile leggendo:

1. **«Aggiungi riga» non aggiungeva niente** (Nominativi, Parcheggi): la riga nuova nasce vuota per
   definizione e il payload scartava le righe vuote. Nessun errore, venti test verdi sopra. Ora **una riga
   vuota è una riga**: si toglie col tasto che la toglie.
2. **La tabella delle aree restava indietro di una scelta**: il salvataggio ricaricava la selezione ma non le
   **aree risolte**, che sono quel che la tabella mostra.
3. ⚠️ **Un 500 sull'intero editor**: il payload di «Nominativi» finiva nell'anteprima della tabella generica,
   e `TryGetProperty` su un array alza **`InvalidOperationException`** — che **non è una `JsonException`**, e
   passava indenne il `catch` messo lì apposta. La stessa trappola aspettava chiunque avesse in archivio una
   selezione d'aree nella forma **legacy**, che è un array.

La regola che chiude tutti e tre sta in **un posto solo** (`BlockJson.EStruttura`), e la fanno la stessa
domanda viewer ed editor: *una tabella con una **variante** è il payload di una sezione resa dalla pagina,
non contenuto*.

Visto funzionare sulla pagina vera: le tre colonne delle radioassistenze coi campi di sorgente in **pill**,
il decimale **rifiutato**, l'alternato **estero** col nome trovato da IVAO (`LSZH Zurich`), le coordinate
delle soglie, nominativi e parcheggi, e i gettoni `A/A` / `A/A - A/G` col **doppio clic vero**.

### Y8 ✅ CHIUSA — tutto fuso in `main`, e i rami cancellati

I due rami di partenza (`convertitore-coordinate`, `edizione-giusta-per-il-campo`) erano già stati portati
dentro; il 30 agosto 2026 è stato fuso anche `tabelle-vsop-militari` — **avanzamento diretto**, perché `main`
era interamente contenuto nel ramo — e **tutti e tre sono stati cancellati**, locale e `origin`, dopo aver
verificato `git rev-list --count main..<ramo>` = 0 su ognuno. `main` = **`34ee5595`**, spinto: non c'è più
nessun ramo con lavoro fuori.

⚠️ **Verde DOPO la fusione**, che è l'unico momento in cui la prova conta: Release su entrambi i TFM, e
**otto progetti su nove**. Il nono — `Vipi.E2E.Tests` — non si è potuto compilare perché un `Vipi.Host` era
acceso e teneva i `.dll`: è **X4**, e il conteggio si legge contando i progetti, non le righe «Passed!».

### Y11 ✅ STORIA — come erano nati i due rami di partenza

Questo lavoro nasceva su `edizione-giusta-per-il-campo`, che nasce sopra `convertitore-coordinate`: nessuno
dei due era fuso, e S2 userà `Vipi.Application/Coordinates` — il DMS del convertitore — per validare le
coordinate delle radioassistenze. Sarebbe stato il **terzo ramo impilato**, e il committente ha deciso di
fondere prima.

⚠️ **Non c'era niente da fondere davvero**: `main` era **interamente contenuto** nel ramo (zero commit suoi
fuori), quindi sono stati due **avanzamenti diretti** in fila — prima il convertitore (14 commit), poi
l'edizione (7) — senza un conflitto e senza commit di fusione finti. `main` = **`636abff6`**, spinto.

⚠️ `convertitore-coordinate` era **solo locale**: su `origin` non c'era. I suoi commit erano comunque al
sicuro, perché contenuti in `edizione-giusta-per-il-campo`, che era spinto — ma è la seconda volta che un
ramo con dieci slice dentro vive su una macchina sola.

⚠️ **La suite completa non si è potuta contare**: il `Vipi.Host` era acceso (PID 35396) e tiene i `.dll` di
Debug, quindi `Vipi.E2E.Tests` non compila e **sparisce dal riepilogo in silenzio** con exit code 0 — è
**X4**, ancora aperto. Verificati a mano i progetti toccati.

## Z. La colonna destra delle «Quote di transizione» — 29 agosto 2026

> **Carta:** [`docs/feature/2026-08-29-quote-transizione-colonna-destra.md`](feature/2026-08-29-quote-transizione-colonna-destra.md)
> **Ramo:** `quote-transizione-colonna-destra`. **Nessuna migrazione.** **7 test nuovi.**

### Z1 ✅ CHIUSA — metà sezione non è più vuota

La tabella dei livelli ha un tetto di 420px (giusto: due colonne di numeri) e lasciava **402px vuoti** su una
sezione da 822 — misurato a schermo su LIBD. Accanto ci sono ora due schede, scelte dal committente fra sei
proposte: **«Transition Level adesso»** (il verdetto sul QNH del METAR, scritto grande, con l'ora del
bollettino) e **«Dati del campo»** (elevazione in piedi e metri, variazione magnetica con l'emisfero, IATA,
coordinate).

⚠️ La prima è **`noprint`**, come il meteo: nasce dal METAR e su carta sarebbe un verdetto già scaduto. La
tabella accanto si stampa come prima.

⚠️ I quattro dati del campo il database **ce li aveva già** e nessuna pagina pubblica li mostrava. Arrivano
dall'anagrafica in cache (`AirportStation`, cinque colonne in più su una lettura che il layout fa comunque),
**non** dallo snapshot di release: non sono dati di release — li riscrive il giro notturno — e nello snapshot
sarebbero trattini su ogni documento già pubblicato finché qualcuno non ripubblica. Il vSOP **militare** li ha
allo stesso prezzo.

⚠️ Va a capo con un **flex**, senza `@media` e senza `@container`: la soglia la calcola il layout, che lo
`zoom` dell'applicazione lo vede.

⚠️ **Le schede stanno al CENTRO dell'altezza della tabella**, non appese in alto (correzione del committente,
carta §5-bis): con **due** schede sposta di 10px, ma con **una sola** — campo senza METAR, niente «TL
adesso» — la scheda restava in alto a destra col vuoto sotto. Misurato su LIML: **67px sopra e 67 sotto**,
prima 0 e 134.

### Z2 ✅ CHIUSA — fuso in `main`

`quote-transizione-colonna-destra` è entrato in `main` il 29 agosto per **avanzamento diretto** (`main` era
interamente contenuto nel ramo: zero commit suoi fuori, nessun conflitto, nessun commit di fusione finto), e
il ramo è stato **cancellato** da locale e da `origin`. Build Release `--no-incremental` e suite completa
verdi **dopo** la fusione, che è l'unico momento in cui la prova conta.

### Z3 ⚠️ VISTO, NON TOCCATO — due cose che stanno lì da prima

- **LIRN** ha una sezione «Transition Altitude/Level» che non è quella derivata (nessun `.ta-grid` nel DOM):
  documento vecchio, sezione scritta a mano. Non è una regressione di questo lavoro, ma è un documento che
  non riceve né la tabella né le schede.
- `AirportViewFormat.QnhRowMatches` legge la fascia dal **testo**: «1013.2 e oltre» lo leggerebbe come
  intervallo 2–1013. In archivio quel testo non esiste (le fasce le scrive `QnhRange` dai numeri), quindi è
  una trappola latente, non un difetto vivo.

## AA. Gli spazi aerei dell'AIP — 29 agosto 2026, sera

Carta: [`feature/2026-08-29-spazi-aerei-dal-kmz.md`](feature/2026-08-29-spazi-aerei-dal-kmz.md).
Ramo **`spazi-aerei-aip`**, spinto e **non fuso**. **S1, S2 e S3 fatte**, verifica dal vivo compresa.

**Che cosa c'è adesso.** Il file dell'AIP (`it (2).kmz`, AirspaceConverter, 1 536 volumi) si carica da
`/services/vsop/admin/airspace`, sta in archivio **intero**, e un Editor può dire «questo avvicinamento
disegna la sua AoR con questi volumi». `LICC_APP` mostra le sette zone del CTR di Catania al posto del
blocco unico dell'anagrafica.

**Le tre cose da non riscoprire.**

1. ⚠️ **Il file contiene scatole, non contorni.** Chi legge un `MultiGeometry` come un elenco di aree —
   cioè `KmlReader`, e per il suo caso d'uso ha ragione — ottiene `TMA MILANO Z1 (1)…(147)`.
2. ⚠️ **§6-bis della carta**: l'aggancio non tocca la shape del settore. Vale sull'AoR (mappa, 3D, stampa);
   confinanti, attribuzione del traffico e vLOA restano sulla forma di IVAO, e la pagina lo scrive.
3. ⚠️ **Dove una release congela la sezione AoR, l'aggancio si vede in pubblico solo dopo aver
   ripubblicato.** Su `LICC_APP` la release aveva quel corpo nullo, e infatti si è visto subito.

### ✅ AA1–AA4 — S4, S5, S6 e S7: **fatte e verificate dal vivo**

- **S4, l'ATZ per le torri.** Passo automatico fra il ripiego del sectorfile e il cerchio, marcato
  `ShapeSource.Aip` (valore nuovo **in coda**) così si aggiorna quando il file cambia e il sectorfile se lo
  riprende. Misurato: **13 torri su 84** hanno un confine vero invece di un cerchio; ne restano tre.
  ⚠️ Un ICAO con **più di un ATZ si salta** — Guidonia ne ha due, Torino Aeritalia tre — perché la colonna
  tiene un anello: si agganciano a mano.
- **S5, la pagina** `/services/vsop/airspace` + scheda nell'hub. ⚠️ Nata **pubblica** a `/services/airspace`;
  dal 1 settembre 2026 è dello **staff di divisione**. Una famiglia per volta (i CTR
  all'apertura, 114 anelli), attribuzione in fondo. Riusa `AccAor` per intero.
- **S6, il convertitore** prende un'area dal catalogo: 363 voci in sei gruppi.
- **S7, il rapporto radioassistenze**, che **segnala e basta**.

⚠️ **Il difetto che la verifica ha trovato**: il rapporto dava **54 «canale diverso»**, ma **49 erano
lacune** («l'AIP ce l'ha e noi no») e solo **5** discordanze vere. Ora sono due codici distinti e l'ordine
delle voci è la gravità: le otto righe che contano stanno in cima, le centouno lacune in fondo.

### ✅ AA5 — la fusione: **fatta il 30 agosto 2026**

Il ramo è **il secondo fuori** insieme a `biblioteca-allegati`. ⚠️ Le migrazioni in coda al cutover MariaDB
diventano **trentaquattro**: due per il catalogo, due per gli agganci.

---

## AB. La shape di un settore: una porta sola — 30 agosto 2026

Carta: [`refactor/15-shape-del-settore-una-porta-sola.md`](refactor/15-shape-del-settore-una-porta-sola.md).
Ramo **`shape-una-porta-sola`**, **fuso in `main` e spinto** (merge `--no-ff`, quattordici commit), poi
cancellato. **S0→S10 fatte e verificate dal vivo**; **S11 resta fuori** (§4-bis della carta).

### Il difetto, e perché non si vedeva

L'aggancio agli spazi aerei dell'AIP (§AA) era onorato da **due motori su sei**. Un avvicinamento agganciato
al suo CTR **disegnava** quel confine nel documento e **rivendicava** il traffico dentro il monoblocco di
IVAO: due verità sullo stesso oggetto, e quella sbagliata non dava nessun errore — dava **numeri**.

⚠️ E l'inviluppo mentiva: `LIBA_APP` è `GND→FL105` su una zona e `7000 FT AMSL→FL195` sull'altra, quindi
base minima + tetto massimo danno `GND→FL195`, cioè **esattamente il monoblocco generoso dell'anagrafica**.
Il viewer 3D disegnava un parallelepipedo unico dove il cielo vero ha **due gradini**.

### Che cosa c'è adesso

- **`SectorShapeParts`**: la forma di un settore è un **elenco di pezzi con una fonte**, e ⚠️ **le quote
  stanno DENTRO il pezzo**. «Laterale da una fonte e verticale da un'altra» non è una cosa da evitare con
  attenzione: è una cosa che **non si può scrivere**.
- **`ISectorShapeResolver`**, la porta unica, con la precedenza in **un posto solo**: aggancio a mano →
  shape **vera** del catalogo → pezzi in archivio → cerchio sintetico. ⚠️ Quest'ordine l'ha **corretto un
  test rosso**: col primo, l'ATZ automatica scavalcava il **sectorfile**, che è fonte primaria per decisione
  del committente.
- **La regola d'oro in una firma**: `ReplacePartsAsync`/`ClearPartsAsync` prendono una `ShapeSource`
  **obbligatoria** e cancellano solo dentro quella; un elenco vuoto è «sorgente muta» e non cancella. È ciò
  che rende lo **sgancio reversibile** — e senza, si romperebbe **in silenzio**.
- **N pezzi dappertutto**: il volume del traffico è «dentro **un** pezzo», l'adiacenza dei confinanti è vera
  se lo è **un** pezzo, e il 3D estrude **ogni anello alla sua quota**.
- **L'ATZ delle torri** non abita più la colonna: è reversibile, prende **tutte** le zone (Guidonia due,
  Torino Aeritalia tre, che prima si **saltavano**) e porta le **quote** che il cerchio non ha mai avuto.
- **Il gettone** `ShapeChangeStamp`: un aggancio entra in vigore in **≤ 60 s** invece che «fra zero e sessanta
  minuti», e i **confinanti** si ricalcolano dall'archivio senza chiamare IVAO.
- **Il timbro**: ogni tratta scrive **con quale forma** è stata contata, perché un gradino nei numeri
  dev'essere leggibile anche fra sei mesi.
- **A schermo**: `ShapeSourcePill` dice la fonte **e quanti pezzi** («AIP · 2»), e dove l'aggancio comanda le
  caselle dei limiti si **sbiadiscono** invece di sparire.

### ✅ La verifica dal vivo — otto controlli

Quello che vale il capitolo: agganciato **`LIRR_EW_CTR`** — in frequenza, quattro aerei — alla **FIR ROMA**
`GND→FL195`, in **un solo giro del poller** le tratte nuove sono uscite col timbro `Aip`, e sono i quattro
voli **sotto** FL195 (16, 2 613, 91, 3 652 ft); i quattro **sopra** (39 081, 43 006, 21 661, 28 049 ft) hanno
**smesso di essere rivendicati**, perché il settore ha tetto **UNL** nell'anagrafica e la FIR si ferma a
FL195. È il difetto visto dal verso giusto, sui dati veri della rete.

Il resto: LIBA due anelli con bande diverse; **LICC agganciato dalla pagina** → sette anelli, sette bande;
lo **sgancio** di LIBA → forma IVAO al primo render **senza ri-importare niente**; confinanti ricalcolati
(33 coppie invariate); Struttura ACC **142 «IVAO» + 11 «nessuna»**, esattamente le cifre del database; e un
documento **già pubblicato** — congelato *prima* di questa carta — che si rilegge e **ricade
sull'inviluppo**: la compatibilità all'indietro, provata invece che sperata.

⚠️ **Guardare gli screenshot serviva**: la pastiglia «AIP · 2» andava **a capo** e alzava la riga della
tabella. Nessuna asserzione l'avrebbe vista.

### Quel che resta

- **S11** — le colonne gemelle del gate. ⚠️ **Non è una slice**: otto siti di scrittura, le letture del
  **congelamento di release**, un backfill e due migrazioni (§4-bis della carta). Si esegue **dopo** la
  consegna del 1° settembre.
- **Le 13 torri ATZ**, da guardare al primo deploy: vedi il punto 3 della lista rossa in §«Dove siamo».

## AC. L'intro di pagina — 30 agosto 2026

Carta: [`feature/2026-08-30-intro-di-pagina.md`](feature/2026-08-30-intro-di-pagina.md).
Ramo **`intro-di-pagina`**, da `main`. **Nessuna migrazione**, e la cosa conta: siamo nella finestra cieca
fino al 16 settembre. **Verificata dal vivo.** **29 test nuovi.**

Chiesta dal committente sull'elenco dei vSOP militari: «in alto una sezione intro dove mettere alcuni PDF,
come se fosse un documento, usando la funzione link delle sezioni». **Tradotta**, su richiesta esplicita
del SOD.

### Dove si salva, e perché senza DDL

`SharedBlocks`, chiave `page-intro:{pagina}`. La tabella esiste dall'`InitialCreate` — quindi è già in
produzione su tutti e due i provider — e **nessuno la scriveva**. È contenuto **senza padrone**, chiavato su
una stringa: la forma che serve a una zona di pagina, che un padrone non ce l'ha.

⚠️ **Un `Document` non andava bene**, e non per gusto: senza aeroporto né settori cade fuori da **tutti** i
descrittori di `IReleaseTarget` — invisibile all'elenco admin, al motore di release, agli impatti. Sarebbe
stato irraggiungibile **in silenzio**, che è il guasto già pagato col catch-all dell'aeroporto.

### Che cosa c'è

- `PageIntro` — modello e proiezione. Salvato c'è **un modello solo** (titolo + `ExtraBlock`, cioè quel che
  `DocumentBlocksEditor` sa già scrivere); la `DocumentView` si **calcola** a ogni resa e non si salva mai.
- `IPageIntroStore` / `EfPageIntroStore` — una porta, col cancello **dentro** (Editor). Svuotare **cancella
  la riga**: una riga col JSON nullo sarebbe un secondo modo di essere vuota.
- `PageIntroZone.razor` — isola sua: si carica, si edita e si salva da sé, col lock (`editor:page-intro:…`).
  Vuota, al pubblico **non si rende affatto** — nemmeno un contenitore.
- Traduzione: le frasi entrano nel **corpus** (`EfTranslatableCorpus`, giro `it`) come già fanno le aree
  regolamentate dal lato inglese. Senza quel pezzo la pagina avrebbe chiesto alla memoria frasi che nessuno
  le aveva mai messo dentro, e sarebbe rimasta italiana **senza che nulla protestasse**.
- Capitolo di Guida `#intro-di-pagina` + voce di ricerca: un «?» che punta a un capitolo assente è un
  collegamento morto, e nessuno lo denuncia.

### La prova dal vivo

Su una copia del `vipi.db` reale, guidando Edge: lock preso, sezione «Documenti generali» con un paragrafo e
un **blocco allegato** scelto dalla biblioteca (`MIL abbriviation`), salvata; ricaricata, la sezione si legge
e il link è **la nostra rotta** (`/vsop/files/mil-abbriviation`); riletta in inglese, esce tradotta
(«General documents»). Zero errori di pagina, zero letterali Razor.

### La passata sulla forma, e il difetto che ha scoperto

Guardando la pagina, il committente ha visto «parti che non coincidono col resto della UI». Erano **tre
classi che cadevano in silenzio perché SCOPED** sotto un antenato che una pagina pubblica non ha:
`.st-msg` vive dentro `.st-head`, `.ln` dentro `.section-title`, e **`.in` non esiste affatto** — il campo
del titolo era un `<input>` nudo. La testata è ora quella di casa (`.section-title .doc-head .st-head`) e la
riga di sezione quella dell'editor documento, `InlineConfirm` sull'eliminazione compreso.

⚠️ **Ma il difetto vero è più largo di questa pagina.** `.app-in` e `.app-ta` **non dichiaravano né fondo né
inchiostro**: prendevano quelli del browser. Nel tema chiaro non si vedeva — il bianco di serie e
`--surface` combaciano — ma nel **tema scuro** uscivano `rgb(59,59,59)` contro un `--surface` di `#21212e`,
**su ogni editor che usa quei campi**, e la tendina restava quella del sistema operativo. Corretto sui token
in un posto solo. Misurato in tutt'e due i temi; `sweep.js`: 16 sospetti, tutti i falsi positivi noti.

*Un campo che non dichiara i propri colori non ne ha di sbagliati: ne ha di altrui.*

### «Fine modifica» salva (30 agosto, sera)

Chiesto dal committente. ⚠️ **La seconda metà della richiesta non era una comodità: era un difetto.** Il
rilascio del lock faceva **rileggere da archivio**, quindi chi scriveva e premeva «Fine modifica» — la strada
naturale per «ho finito» — perdeva tutto **in silenzio**. E la prima metà vale comunque: **un tasto «Salva»
spento non è un avviso**, dice l'opposto di quel che succede.

Adesso: pastiglia **«Modifiche non salvate»** finché c'è da salvare, e **«Fine modifica» salva** prima di
lasciare il lock, con il **«Salvato» che sopravvive all'uscita**. L'aggancio è un parametro **additivo** su
`EditLockBar` (`BeforeRelease`): chi non lo passa non cambia di una virgola.

- ⚠️ **Solo sul tasto.** Scadenza e sblocco forzato vogliono dire «il lock non è più tuo»: salvare lì
  coprirebbe il lavoro di chi ce l'ha adesso.
- ⚠️ **Salvataggio fallito ⇒ lock NON rilasciato**, o chi ha scritto non avrebbe più il permesso di riprovare.

### Quel che resta

- **Una pagina sola.** L'intro è agganciata solo a `/services/vsop/mil`: le altre landing ne registrano una
  **chiave** quando servirà, non un secondo meccanismo.
- ⚠️ **Nessun congelamento.** Quel che si salva è subito pubblico e non c'è una versione precedente da
  riaprire. È scritto nella Guida e nel «?», ma è la cosa da ricordare se qualcuno ci mette del normativo.

## AD. «Uscire non butta via» — la verifica su tutto ciò che si modifica, 30 agosto 2026

Chiesta dal committente dopo l'intro: «puoi fare questa verifica dovunque si possa modificare qualcosa?
Secondo me abbiamo questo difetto anche altrove». Aveva ragione, e non dove ci si aspettava.

### Dove il difetto NON c'era

I quattro editor documentali — **ACC, APP, vSOP militare, vLOA** — **salvano a ogni gesto**: non hanno niente
in sospeso da perdere. Sta scritto in `DocumentSectionsEditor.IsSectionDirty`, ed è il motivo per cui il
pallino d'indice è opt-in e lo usa il solo aeroporto. Sane anche le altre pagine di struttura, che agiscono
per bottone e non accumulano.

### 🔴 Editor AEROPORTO — l'unico che accumula, e usciva senza guardare

`«Fine modifica»` chiamava `FinishEditingAsync()` e basta. Il lock se ne andava, i pannelli tornavano in sola
lettura, e i valori digitati restavano **a schermo ma non salvabili** — «Salva tutto» spento perché il lock
non era più nostro — per sparire al primo ricarico. Nessun errore: una pagina che tornava com'era.

⚠️ **Ironia**: l'aeroporto la guardia ce l'aveva già in due punti — conferma prima di **pubblicare** e prima
del **re-import**. Mancava proprio sull'uscita.

Ora l'uscita salva i **tre** buffer (sezioni in blocco, limiti di settore riga per riga, SID importate che le
possiede il loro editor) e, se qualcosa non passa la validazione, **non esce** e lo dice.

### 🔴 La guardia del browser copriva un buffer su tre

`beforeunload` si accendeva solo dalle sezioni. Le **SID importate e corrette** non l'accendevano affatto: il
conteggio c'era (`_sidNonSalvate`) e non serviva a niente — chiudere la scheda le perdeva **senza prompt**.
⚠️ *Una guardia che copre due terzi di quel che protegge è peggio di nessuna guardia, perché la si crede
accesa.* Ora la porta è una sola (`AggiornaGuardiaAsync`).

### 🟠 E il difetto opposto, nello stesso pannello

Il blocco dei settori si marcava sporco con un `@onchange` **sul contenitore**, che **nessuno puliva mai**:
bastava toccare un interruttore che salva da sé — nascondi, primario — perché il browser chiedesse «uscire
davvero?» per il resto della sessione, **con tutto salvato**. Un avviso che si accende sempre è un avviso che
si impara a ignorare. Ora la marcatura è sui due campi che si compilano e si spegne quando la riga si salva.

### 🟠 `AccAdminPage` — i limiti pendenti restavano orfani

Stessa forma dell'aeroporto: `_dirtyLimits` bufferizzato, `OnLockChanged` che non guardava niente. Ora
«Fine modifica» li salva (`EditLockBar.BeforeRelease`); se qualcuno non passa, il lock **resta**.

### Il contratto, corretto in corsa

`BeforeRelease` era `Func<Task>` con la regola «se solleva, il lock non si rilascia»: un invito a lanciare da
dentro un gestore d'evento Blazor, cioè ad **abbattere il circuito** — la pagina sparisce invece di dire di
no. Ora torna un `bool`, e un test tiene ferma la firma.

### Fuori tema, stessa classe di errore

`class="in"` **non esiste nel foglio**: la portavano ancora tre campi del Glossario e uno delle Traduzioni,
nudi coi colori del browser. Passati a `app-in`/`app-ta`, con la regola di riga che `.perm-add` aveva già.

### Quel che resta, per scelta

- **StrutturaPage** (`_pendingParent`) e **AdminTrasferimentiPage** (form `_editPair`/`_sectionForm`): stessa
  famiglia, ma sono una scelta singola e un modulo corto, **entrambi a vista**. Lasciati come sono.
- **`AccAdminPage` non ha un `beforeunload`**: chiudere la scheda con dei limiti digitati li perde ancora,
  senza prompt. L'aeroporto ce l'ha; qui vorrebbe dire portare la guardia fuori dall'editor aeroporto, ed è
  un lavoro suo.
## AE. «Attempting to reconnect to the server…» — 31 agosto 2026

Carta: [`feature/2026-08-31-riconnessione-circuito.md`](feature/2026-08-31-riconnessione-circuito.md).
Ramo **`riconnessione-circuito`**, fuso nel ramo di consegna **`consegna-20260831`**. Build Release 0 avvisi, suite verde, 18 test nuovi,
**nessuna migrazione**.

### Il fatto

Sul sito vero compare, mentre si legge o si scrive, un riquadro nero in inglese: *«Attempting to reconnect
to the server…»*. Non vuol dire che il sito sia giù — vuol dire che è morto il **circuito**, cioè lo stato
che il server tiene in memoria per quella singola pagina. Le cause sono due e vogliono rimedi **opposti**:
un **buco di rete** (il circuito di là c'è ancora: **riprovare**, e si ritrova la pagina esatta) e il
**processo morto e rinato** — che qui succede **da solo**, perché Plesk + Passenger spegne per inattività e
rigenera alla richiesta dopo (il circuito non esiste più da nessuna parte: **ricaricare**).

⚠️ Il difetto era che il **secondo caso finiva nel comportamento del primo**: si riprovava, si falliva, e
restava sullo schermo un messaggio inglese con un tasto da premere.

### Che cosa c'è adesso

1. **`diagnostica/avvii.txt`** (`RegistroAvvii`): una riga per avvio, una per arresto, **in coda**.
   `avvio-diagnostica.txt` è *riscritto* a ogni avvio, quindi tre riavvii al giorno e quaranta producevano
   lì lo stesso file. ⚠️ **Un AVVIO che segue un altro AVVIO è un processo morto male**: lo spegnimento per
   inattività fa in tempo a scrivere la sua riga, un crash o una `.dll` sovrascritta via FTP no.
2. **Ricarica da soli quando riconnettersi è impossibile**: `blazor.web.js` parte con `autostart="false"`
   e ad avviarlo è `vipi-riconnessione.js` — l'unico modo di scrivere i tempi. 55 tentativi ogni 5 s,
   riquadro **nostro** tradotto e dentro il tema, e sullo stato `rejected` (è il **server** a dire «quel
   circuito non lo conosco») si ricarica, con un tetto di **3 ricariche al minuto**. Sullo stato `failed`
   no: quasi sempre è la rete dell'utente, e una pagina ricaricata senza rete è una pagina d'errore del
   browser.
   ⚠️ **`autostart="false"` e quel file sono una cosa sola.** Un pacchetto col primo e senza il secondo dà
   un sito che si vede **intero e non risponde a niente**, senza errori in pagina. Presidio:
   `RiconnessioneTests.Chi_spegne_lavvio_automatico_deve_riaccenderlo`, che guarda anche l'**ordine** dei tag.
3. **`/vsop/ping`**, `204` e basta, chiamato ogni 2,5 minuti dalle schede **visibili**: a Passenger per non
   spegnere serve *una richiesta qualsiasi*. ⚠️ **Non deve diventare una sonda**: `/vsop/health/ready` fa
   due query, e lì sarebbe carico continuo **per scheda aperta**.
4. **I tempi, dai due capi**: retention dei circuiti staccati **2 → 5 min**, `ClientTimeoutInterval`
   **30 → 60 s**, `KeepAliveInterval` 15 s **scritto**, `HandshakeTimeout` **15 → 30 s**; gli stessi due
   numeri stanno in `vipi-riconnessione.js`. ⚠️ La retention **non serve a niente quando muore il
   processo**: i circuiti trattenuti muoiono con lui.

### 🟡 Quel che resta

- ✅ **AE1 — la prima lettura è stata fatta** (31 agosto, §A15): tre avvii e due arresti, **tutti
  ordinati**, e un'attesa di sei minuti da spento finita alla prima richiesta. 🟡 **Resta da rileggerlo fra
  qualche giorno**, quando le righe saranno quelle di un uso normale: è lì che si vede se i riavvii sono
  pochi e nelle ore vuote (Passenger, fisiologico) o tanti e nelle ore di punta (un difetto da cercare).
- **AE2 🔵 Il pinger esterno.** UptimeRobot (o simile) ogni 5 minuti su `/vsop/ping` terrebbe il processo
  caldo **anche quando non c'è nessuno**, e direbbe a noi quando il sito è giù davvero. È fuori dal nostro
  codice: va deciso con Ivao.It, insieme a §A9.
- **AE3 — meno circuiti in giro.** Una pagina SSR statica non ha circuito e quel riquadro non può vederlo.
  Le pagine pubbliche lo sono già (doc 14); resta da rivedere **quali schermate admin** abbiano davvero
  bisogno di `InteractiveServer`. Da fare **dopo** AC1, o si ottimizza al buio.
- ✅ **AE4 — la verifica dal vivo: FATTA il 31 agosto 2026.** Edge guidato con puppeteer-core
  (`riconn-verifica.js`), processo **ucciso e riavviato** con una pagina aperta: si è **ricaricata da sola
  in 10 secondi**. E ha trovato due cose che i test non vedevano — `/vsop/ping` rispondeva **405 a HEAD**
  (i pinger esterni bussano così: corretto, con un test suo) e la frase dei tentativi mostrava una **barra
  sola** finché Blazor non riempie i contatori. Dettaglio nella carta, §Verifica.

## AF. La ricaduta guarda anche in alto, e un settore non è più nipote di sé stesso — 31 agosto 2026

Carta: [feature/2026-08-31-ricaduta-verticale-e-cicli.md](feature/2026-08-31-ricaduta-verticale-e-cicli.md).
Ramo `ricaduta-verticale-e-cicli` (da `consegna-20260831`), tre commit, **non fuso**.

### AF1. Il ciclo — ✅ chiuso

Su `atc.it.ivao.aero` la pagina Struttura disegnava `LIMF_WW0_APP` due volte: una come radice e una come
figlio del proprio figlio. Tre fatti incastrati — l'aeroporto pendeva da `LIMF_WN0_APP`, che aveva come
padre **scritto** `LIMF_WW0_APP`, che un padre scritto non ne aveva — e `AirportPositionLadder.ParentOf`,
per un APP (`Rung = 5`, nessun gradino sopra), usciva diritta su `airportParent`: una **propria discendente**.

⚠️ **Nessuna guardia poteva vederlo.** `EnsureNoCycle` riceveva `InternalNodeParentMapAsync`, cioè i soli
padri **scritti**, mentre tutto il resto legge `EffectiveParentCallsign`. E la validazione stava **dentro**
`if (parentCallsign is not null)`: scegliere «eredita» — scrivere `null` — non passava da nessun controllo,
ed è precisamente il gesto che ha armato l'anello.

⚠️ **E non esplodeva.** Ogni lettore ha una guardia sui nodi già visti, quindi la catena di ricaduta si
**tronca in silenzio** dove l'anello si richiude, e il traffico finisce su un antenato arbitrario.

Quattro mosse: la derivazione non restituisce più un candidato che risale ai propri discendenti (risposta
`null`, **meglio orfana e visibile che ciclica e muta**); `EffectiveHierarchy` diventa la porta unica
dell'albero effettivo, e la guardia valida una **simulazione** della modifica — rifiutando gli anelli che
essa **crea**, non quelli che trova, perché in produzione uno c'è e la pagina è l'unico posto da cui si
scioglie; la pagina sceglie le **radici** col padre effettivo, come già faceva coi figli; e un rilievo
«Gerarchia ciclica» nel report di consistenza fa da rete per import, seed, DB a mano, riaggancio
dell'eliminazione e rinomina, che i padri li scrivono senza chiedere niente.

**Misura sul `vipi.db` reale** (320 nodi interni): **0 anelli attivi**, 0 padri inesistenti, ma **19
aeroporti** pendono da una **propria** posizione APP. Non è un errore — è la configurazione normale — ma
erano diciannove inneschi a un clic di distanza. Ora sono innocui.

### AF2. La ricaduta verticale (C + B) — ✅ chiusa

⚠️ **Il dato ha smentito la prima stesura della carta su due punti**: Milano è divisa a **FL325** (non
FL305) e i due settori alti stanno **uno sotto l'altro** (`ES5 → WS5 → WS2`, non `ES5 → ES2`). Cambia quale
verso è rotto: con ES5 chiuso la catena passa comunque da WS5 e la risposta è giusta *per caso*; con **WS5**
chiuso salta diritta a WS2, che sopra FL325 non ha niente, mentre quel cielo lo tiene ES5 — e l'albero **non
può** dirlo, perché ES5 sta *sotto* WS5.

`SectorFallbacks` porta righe con **fascia di quota** (piede incluso, tetto **escluso**), consultate prima
del padre; il padre resta la **coda implicita** della catena. ⚠️ Per questo la tabella nasce **vuota** e non
c'è nessun travaso: a tabella vuota il comportamento è identico a prima, riga per riga — e non serve un
`Sql` in migrazione, che il presidio della finestra cieca vieta. La risoluzione è **in ampiezza**.

Chi la usa: la vista live (quota del **punto**) e `TransferMatcher` (quota di **crociera** del volo).
⚠️ Le **statistiche restano fuori di proposito**: lì le pretese andrebbero rese per **pezzo di forma**, ed è
una fetta sua. Lo scarto che resta è di conteggio, non operativo.

### AF3. Cosa resta

- ⚠️ **Migrazione additiva su due provider**: quelle in coda diventano **TRENTASETTE**.
- **Il ramo non è fuso**: decisione del committente, come sempre.
- **Statistiche per pezzo di forma** (vedi AF2).
- Le **`UnificationRule`** restano un motore senza editor: la ricaduta ora ha la sua strada, e sovrapporle
  sarebbe il secondo meccanismo per la stessa cosa.

### AF4. Il giro sulla pagina — 1 settembre 2026 — ✅ chiuso

Tre richieste del committente su `admin/sector-structure`, guardata **in inglese** in produzione.

1. ⚠️ **La finestra di eliminazione parlava italiano dentro una pagina inglese.** La finestra passa dalle
   risorse; il **piano** che ci sta dentro lo scrive l'applicazione, che i `.resx` non può leggerli e si
   porta dietro tutt'e due le lingue con `Messaggio.Lingua` — ed era stato fatto **a metà**: ~20 frasi di
   `DeletionRules`, `SogliaEliminazione.MotivoDelRifiuto` e **tutti** i verdetti di
   `IvaoSourcePresenceProbe`. Le **tracce** della sonda restano italiane apposta: sono diagnostica.
   ⚠️ E i test che asserivano quelle frasi vanno ancorati con `CulturaDiProva`: la cultura di questa
   macchina è **inglese**, e cinque test sono diventati rossi appena le frasi hanno avuto due versioni.
2. **Le due sezioni si chiudono** dal titolo (nascono aperte). ⚠️ `hidden` da solo non basta a
   `.gerarchia-2col`, che ha un `display:grid` suo: la regola dell'attributo sta nel foglio del browser.
   **Stesso gesto su `admin/acc`**, tutt'e tre i riquadri: «ACC», «ACC sectors», «Regulated areas».
   ⚠️ Il riquadro degli ACC un titolo di sezione non ce l'ha (era stato tolto perché ripeteva l'H1),
   quindi la maniglia è **l'H1**; e scegliere un ACC **riapre** il riquadro dei settori, o lo scorrimento
   a `#settori-acc` mostrerebbe un titolo e basta. Le aree si vedono solo con un ACC scelto: il riquadro
   compare e sparisce da sé, e il richiudibile dice soltanto **come** compare.
3. **Il piede e il tetto si scrivono anche in piedi** (tendina FL/ft, come nei Trasferimenti; cambiare
   unità **non converte**). ⚠️ **L'unità non si salva**: in archivio ci sono solo i piedi, e due colonne
   nuove sarebbero una **seconda migrazione nella finestra cieca** per una comodità di scrittura. Alla
   rilettura si **deduce** (`StatsView.Livello`: sotto 10 000 ft in piedi, sopra in FL), quindi «FL30»
   rilegge «3 000 ft» — prezzo dichiarato. La fascia disegnata segue: `2,500 ft–FL195`.

**Verificato dal vivo** (Edge+CDP, copia del DB): sezioni, salvataggio e **rilettura** della riga
`LIBB_ES_CTR → LIBB_EU_CTR 2 500 ft–FL195`, finestra di eliminazione inglese fino al verdetto della
sorgente; sulla pagina ACC, **8 627 px → 1 500 px** con tutto chiuso, testata `sticky` sempre a una riga
(`--st-head-h` la segue), riapertura automatica dei settori alla scelta di un ACC e le **105 aree** di LIRR
che chiudendosi portano la pagina da **7 025 px a 2 537 px**. Build Release **0 avvisi** su tutt'e due i
TFM, suite verde, **3 test nuovi**.

## AG. La mappa bianca dei Confinanti: un modulo pigro che il gesto non svegliava — 1 settembre 2026

Segnalazione del committente su `admin/neighbours`: **«verify adjacency» apre una mappa bianca**. Il
sospetto naturale era la basemap — CARTO ha chiuso il fondo anonimo il 27 agosto e il sito è passato a Esri — e invece la mappa **non veniva disegnata affatto**.

**Misurato dal vivo** (Edge+CDP, 1 settembre): dopo il clic il contenitore `.aor-leaflet` c'è, alto 320 px,
con le sue 5 chip — e **zero figli**, `data-init` assente, `window.L` non definito, `vipi-aor.js` **mai
chiesto** benché il tag in `App.razor` lo dichiari. Nessun errore in console: il modulo semplicemente non
arrivava.

### La causa, che non è di questa pagina

`vipi-boot.js` carica i quattro moduli pesanti **guardando il DOM** — «se il bersaglio c'è, serve» — e lo
guarda in due momenti: al primo render e a ogni `enhancedload`. ⚠️ **Un render interattivo di Blazor non è
né l'uno né l'altro.** Sui Confinanti la mappa nasce dal clic su «verifica adiacenza», cioè da un render
interattivo: al caricamento il bersaglio non c'era, e dopo nessuno guardava più.

Non è il caso singolo che conta: vale per **ogni** pagina che riveli una mappa, una carta delle minime o
uno stage 3D dopo un gesto. Il criterio «guarda il DOM» era giusto; era l'elenco dei momenti in cui
guardarlo a essere incompleto.

### Il rimedio

Un `MutationObserver` sul `body`, con la stessa forma che `vipi-aor.js` ha già al suo interno: strozzato a
150 ms, ri-scandisce e carica quel che ora serve. ⚠️ **Si spegne da solo** appena non resta niente da
caricare (i moduli sono quattro e si prendono una volta sola), così la sorveglianza è a termine e non un
costo che ogni pagina si porta dietro per sempre. Un modulo **non dichiarato** dal tag non conta come
lavoro in sospeso, o l'osservatore non si spegnerebbe mai.

E un secondo difetto che si vedeva solo insieme al primo: **un modulo che arriva in ritardo si aggancia da
sé al proprio `DOMContentLoaded`, che a quel punto è già passato**. Ora `vipi-boot.js` chiama la sua
funzione di riaggancio sull'`onload` dello script, invece di sperare nella mutazione successiva.

### Verifica

`lazy-verifica.js` della skill `verifica-live` — quello scritto apposta per questo file — **quattro prove su
quattro**: la guida e l'hub restano senza nessuno dei quattro moduli (l'osservatore non li tira dentro per
sbaglio), la vIPI ACC monta 66 tessere e 177 poligoni, e la navigazione «guida → vIPI ACC» continua a
portarsi dietro il modulo. Sui Confinanti, dopo «verify adjacency»: `vipi-aor.js` e Leaflet caricati,
`data-init="1"`, **12 tessere** Esri (`World_Light_Gray_Base` + `Reference`) tutte `200`, console pulita.

⚠️ **Un rosso intermittente** in `Vipi.Ui.Tests` (net8.0) al primo giro della suite, **verde ai due giri
successivi** e non riproducibile; il nome del test non è stato catturato. Non tocca nulla di questo lavoro
(qui cambia un `.js`), ma è della stessa famiglia dei rossi da contesa già visti — se ricompare, va preso
col nome.

## AH. Fraseologia e traduzioni: una pagina sola, una ricerca vera, «dove si usa» — 1 settembre 2026

Carta: [`feature/2026-09-01-fraseologia-e-traduzioni.md`](feature/2026-09-01-fraseologia-e-traduzioni.md).
Richiesta del committente sulle due pagine `admin/translations` e `admin/glossary`.

**Le due pagine diventano una.** Glossario sopra, registro sotto, tutt'e due **richiudibili** e aperte
all'arrivo, con **una** tendina di direzione per entrambe. ⚠️ Il vecchio indirizzo del registro è una
**seconda rotta della stessa pagina** — sta nei preferiti e nei commenti dei pannelli — e nella barra admin
la voce è una sola (17 → 16; per l'Editor 12 → 11). ⚠️ Prezzo dichiarato della tendina unica: le direzioni
non pesano uguale (misurate: **22 formule `it→en` contro 1**, **176 frasi `en→it` contro 98**), quindi una
sezione è sempre la più magra. È il dato, non la pagina.

**La ricerca sta sul database.** Il registro mostrava **cento righe e non lo diceva**: chi cercava oltre la
centesima concludeva che non ci fosse. ⚠️ Un filtro sulle righe già caricate mentirebbe **esattamente** nel
caso in cui la ricerca serve. Si cerca nei due lati (frase e resa); il **M** di «N di M» esce dalla stessa
query dell'elenco; l'ordinamento è **totale**, o «carica altre» salta e ripete righe. La ricerca mentre si
scrive è strozzata: il giro nuovo annulla quello di prima.

**«Dove si usa», a due livelli** — una formula vive nelle frasi, una frase vive nei documenti. Pastiglia col
numero in elenco, pannello al clic (documenti col **dove**: prosa · tabella · titolo, e il collegamento
all'editor). ⚠️ Il corpus editoriale (499 campi, 23 344 caratteri) si legge **una volta per lotto di frasi**:
il vecchio `DocumentiToccatiAsync` lo leggeva intero per **una** frase, e cento righe sarebbero state cento
letture. Ora quel conteggio **poggia** sulla stessa passata, o il numero e l'elenco divergerebbero.
⚠️ «Nessun documento» è una risposta e si mostra: è una frase in memoria che il testo si è lasciata indietro.

**Comodità**: «N di M» + «carica altre» (le righe si **aggiungono**), Invio salva nel glossario e Ctrl+Invio
nel registro (là il campo è un'area di testo e una resa può avere un a-capo), Esc chiude, e il testo cercato
è **marcato** — costruito con `AddContent`, non con una `MarkupString`: quelle frasi le scrivono persone.

**Verifica.** Release **0 avvisi** su tutt'e due i TFM, suite verde, **15 test nuovi**. Dal vivo (Edge+CDP,
copia del DB): vecchio indirizzo → pagina fusa; sezioni; ricerca «runway» → «2 formulas · 0 phrases» con due
`<mark>`; «dove si usa» → `vIPI — LIBC Crotone · prose` col collegamento vero all'editor; «carica altre»
**100 of 176 → 176 of 176, zero duplicati**; Esc e Ctrl+Invio. ⚠️ La pastiglia «N frasi» è stata provata
**inserendo una frase apposta** nella copia: nel dato di sviluppo nessuna delle 98 frasi `it→en` contiene una
delle 22 formule — misurato prima di chiamarlo difetto.

**Le tre aggiunte del giro dopo** (carta §6), chieste dal committente e fatte tutt'e tre:

1. ⚠️ **Il filtro per origine è UN comando a tre stati, non un secondo interruttore.** Misurato: 192 righe
   umane **tutte** riviste e 82 automatiche **tutte** mai riviste, **zero** miste — `SaveHumanAsync` scrive
   `ReviewedUtc` e ribalta `Origin` nello stesso gesto. «Solo da rileggere» e «solo automatiche» erano la
   stessa domanda. Ora tre chip col conteggio (*tutte 99 · macchina 41 · persona 58*), e si guadagna lo
   stato che prima non si poteva chiedere: **solo le corrette da una persona**.
2. ⚠️ **«Vedi» porta al PUNTO**, all'ancora della sezione (`s-{id}`), e il pannello nomina la sezione. La
   trappola, trovata **dal vivo**: gli id di sezione sono **per versione** — la stessa «Remarks» di LIBC è
   la 611 nella pubblicata e la 651 nella bozza, e il primo collegamento apriva la pagina giusta su
   un'ancora inesistente. Vince l'occorrenza della versione **corrente**; se la frase sta solo in una
   vecchia, l'ancora **non si offre** (il titolo sì). Il **conto** resta su tutte le versioni: è la portata
   del corpus.
3. **Il glossario si ordina**: recenti (default) o A→Z — alfabetico per `SourceKey`, o «Riporta» finirebbe
   prima di «attendi».

Verificate dal vivo tutt'e tre; **9 test nuovi** (24 in `RicercaEDoveSiUsaTests`). **Resta fuori**: il
collegamento alla **cella** esatta di una tabella e l'ordinamento del registro (resta «le mai riviste
prime», che è l'ordine del lavoro).

## AI. Radioassistenze: i chip per tipo, l'aggiunta in cima e l'avvertimento — 1 settembre 2026

Tre richieste del committente su `admin/navaids`, più le comodità che ha scelto lui da un elenco.

**Misurato prima**: 149 righe — **122 VHF tutte senza tipo** (la sorgente il tipo non lo sa: `itvor.vor`
contiene VOR, TACAN e VORTAC insieme) e **27 NDB**. È la lista di lavoro della pagina.

### I chip per tipo

⚠️ **Si costruiscono dal DATO, non da `NavaidRules.TipiSuggeriti`**: quello è un elenco *aperto* — un campo
può avere un DME o un VOR/DME che lì non compare — e un chip fisso a zero prometterebbe un filtro che non
filtra niente. Oggi sono «senza tipo 122» e «NDB 27»; gli altri compariranno man mano che i tipi si
compilano, che è poi il lavoro di questa pagina.

⚠️ **Si sommano**, e «senza tipo» sta nello **stesso** insieme degli altri: chiedere «TACAN e VORTACAN» è
una domanda sola, e tenere «senza tipo» in un booleano a parte sarebbero due regole per lo stesso filtro.

### L'aggiunta sale in cima, e chiede ogni volta

Con 149 righe, per aggiungerne una bisognava scorrere l'intera tabella — e chi aggiunge di solito ne
aggiunge parecchie di fila, leggendole da una carta.

⚠️ **E chiede conferma ogni volta**: una riga scritta a mano è **nostra per sempre** — l'import non la tocca
più, e chi usa il sectorfile non la vede — mentre lo stesso impianto messo nel sectorfile arriva a tutti e
si aggiorna da sé. La domanda non impedisce niente: dice qual è la strada migliore nel momento in cui si sta
per non prenderla.

⚠️ La domanda la pone la **pagina** e non un `InlineConfirm`: quello non si apre da codice, e con l'Invio da
tastiera sarebbe servito un secondo meccanismo — due modi di porre la stessa domanda che un giorno diranno
due cose diverse. ⚠️ E il codice si valida **prima** di chiedere: far confermare un'operazione che il
servizio rifiuterà un istante dopo è lo stesso difetto per cui la finestra di eliminazione calcola prima.

### Le comodità

- **Intestazione appiccicata** (`sticky-head`): 149 righe, e nelle celle si **scrive** — senza, appena si
  scorre non si sa più quale colonna si sta compilando.
- **Ordinamento per colonna** a **tre** stati: crescente, decrescente e **l'ordine dell'anagrafica**, che è
  uno stato vero (è quello in cui la sorgente le manda) e non deve diventare irraggiungibile al primo clic.
  ⚠️ La frequenza si ordina da **numero**: in alfabetico «115.25» verrebbe prima di «75». E il codice è
  sempre chiave secondaria — ordinando per banda ci sono 122 righe uguali, e senza un secondo criterio il
  loro ordine è quello che capita.
- **«N di M»** nella pastiglia quando un filtro è acceso; prima diceva sempre il totale.
- **Testo cercato marcato**, con il componente `<Marca>` — estratto dalla pagina Fraseologia, che lo aveva
  come metodo suo. ⚠️ Costruisce i `<mark>` con `AddContent`, mai con una `MarkupString`: qui passano codici
  battuti a mano.
- **Invio aggiunge, e il fuoco torna nel campo** dopo il salvataggio.

**Verifica.** Release **0 avvisi** su tutt'e due i TFM, suite verde, **8 test nuovi** (bUnit). Dal vivo
(Edge+CDP, copia del DB): chip «Without a type 122 · NDB 27» che filtrano e si sommano (27 → «27 / 149»,
poi 149 → «149 / 149»); modulo d'aggiunta a 264px contro i 487 della tabella; ordinamento AEA→ZZT→AEA nei
tre clic e frequenze 108.05 · 108.20 · 108.30 (numeriche); ricerca «MNL» → «1 / 150» con un `<mark>`;
avvertimento aperto da tastiera e dal tasto, «Annulla» che **conserva il codice**, conferma che salva e
rimette il fuoco nel campo; codice «X» → nessuna domanda e il messaggio di rifiuto. Console pulita.

⚠️ **Trappola di misura pagata di nuovo** (già in `verifica-live/SKILL.md`): con l'app viva su :5034 la
build fallisce con `MSB3027` sui `.dll` bloccati, e il `dotnet test` che segue gira sui **binari vecchi**
dicendo verde. Il processo va fermato **prima**, e la build riletta.

## AJ. «Da fare» non parlava la lingua del sito — 1 settembre 2026

Segnalazione del committente su `/services/vsop/tasks`: «mi pare che la UI non sia allineata con il resto,
puoi verificare?». **Verificato**, e le cose fuori posto erano quattro — una delle quali un difetto vero,
non una questione di gusto.

### 1. L'elenco stava nudo

`/services/vsop/tasks` rendeva `<ul class="wi-list">` **direttamente sul fondo della pagina**: senza bordo,
senza ombra, a tutta larghezza. ⚠️ La prova che non è un'opinione: **`/services/vsop/admin/pending` rende le
stesse identiche righe** — stesso `WorkItemRow`, stesso read-model — **dentro un `.panel`**. Due cornici
diverse per la stessa lista sono due prodotti, non due pagine. Ora è un `.panel` anche qui.

### 2. «My tasks» era un `<details>` senza una riga di CSS

`.tk-board-wrap` **non compariva in nessun foglio di stile**: a schermo si vedeva il triangolino nero del
browser accanto a un titolo grande, e nessuna delle due cose appartiene a questo sito. Ora è la sezione di
casa — `.section-title`, titolo che fa da maniglia, chevron `.grp-chev`, riga a filo — la stessa di
Struttura, ACC e Fraseologia. **Nasce chiusa**, come prima.

### 3. ⚠️ La lavagna era BIANCA nel tema scuro

`.tk-col-head` e `.tk-card` avevano `background: var(--paper, var(--on-brand))`, e **`--paper` non esiste
in nessun foglio**: vinceva sempre il ripiego, cioè `--on-brand`, che è il colore del testo **sopra** il
blu — bianco. Nel tema scuro le intestazioni di colonna e le schede erano rettangoli bianchi con sopra
scritte quasi bianche: **illeggibili**. È la stessa classe di errore della legenda del visore 3D del 22
agosto: un fondo scritto a mano sfuggito alla passata sui token. Il token giusto è `--surface`.

### 4. Le due sezioni chiuse non dicevano di potersi aprire

`.tk-closed > summary` è in `display:flex`, e questo toglie **anche** il triangolo del browser: «Bloccato» e
«Fatto» non avevano nessun segno. Ora hanno il chevron dei blocchi collassabili (`.cb-chev`, ruotato dal CSS
su `[open]`), che è l'idioma di casa per un `<details>`.

### Perché era sfuggito, e cosa si è fatto perché non risucceda

⚠️ **`/services/vsop/tasks` non era nell'elenco di `sweep.js`** — la passata che esiste apposta per i fondi
che non si girano. E non sarebbe bastato aggiungercela: **la lavagna nasce chiusa**, e un corpo nascosto non
ha un fondo da misurare. Quindi lo script ora fa due cose in più: la pagina è in elenco, e **prima di
misurare apre tutto quel che si può aprire** — i `<details>` (già previsti) **e** i titoli-maniglia
`.sect-toggle`, che nascondono il corpo con `hidden` e che nessuno script apriva.

**Più una rifinitura che vale per tutte e quattro le pagine convertite**: il chevron di un titolo di sezione
era fisso a 11px — accanto a un titolo grande si leggeva come un puntino. Ora cresce col titolo.

**Verifica.** Release **0 avvisi** su tutt'e due i TFM, suite verde. Dal vivo (Edge+CDP, copia del DB, con
quattro incarichi seminati apposta perché la lavagna avesse qualcosa da mostrare): l'elenco è in un `.panel`
(bordo 1px, raggio 14px, ombra), la sezione ha maniglia, `aria-expanded`, chevron e riga, le due sezioni
chiuse hanno il loro chevron — e `sweep.js` su `/tasks` torna **un solo sospetto**, che è il falso positivo
noto della pastiglia della lingua sulla barra blu, come su tutte le altre quattordici pagine.

## AK. Gli spazi aerei chiudono allo staff, e passano sotto la vSOP — 1 settembre 2026

Due richieste del committente su `/services/airspace`: **accessibile allo staff di divisione e superiori**
(«per ora»), e **sotto `/services/vsop/airspace`**.

### Il cancello, in due sedi

Come per il convertitore di coordinate, e per la stessa ragione: **un indirizzo si scrive anche a mano**.

1. **L'hub** non mostra più la scheda nella griglia pubblica: si è **spostata** nella sezione dello staff,
   sopra il convertitore. Spostata e non nascosta — chi la vede sa anche che gli altri non la vedono, che è
   quel che la riga di separazione dice.
2. **La pagina rifiuta**, e ⚠️ **rifiuta prima delle query**: un rifiuto disegnato sopra un dato già letto è
   un dato già letto. Il test lo prova contando le chiamate all'archivio (zero, sotto lo staff).

### L'indirizzo

⚠️ Il **vecchio** `/services/airspace` **non esiste più**. La prima stesura lo teneva in vita come seconda
rotta della stessa pagina — è la regola che vale per il Registro delle traduzioni — ma il committente ha
deciso il contrario, e qui la differenza c'è: quel percorso è l'indirizzo con cui la mappa girava **senza
cancello**, e tenerlo in piedi vorrebbe dire lasciarlo in giro. Chi ce l'ha nei segnalibri trova un **404**,
identico a quello di qualunque altro percorso inesistente (misurato: `/services/airspace` e
`/services/pippo` rispondono allo stesso modo).

⚠️ **E la scheda è marcata `shortcut`.** Spostandosi sotto `/services/vsop/` la mappa ha smesso di essere un
**servizio** a sé ed è diventata una **parte della documentazione**, esattamente come i vSOP militari.
`ServicesHomeTests` pretende che un servizio sia figlio **diretto** di `/services` e conta le scorciatoie
proprio perché non diventino la scusa per cancellare la regola: il numero è salito da uno a **due**, e il
perché è scritto lì.

**Verifica.** Release **0 avvisi** su tutt'e due i TFM, suite verde, **6 test nuovi**
(`PaginaSpaziAereiTests`) più due sull'hub. Dal vivo (Edge+CDP): l'hub elenca
`/services/vsop/airspace` **dentro la sezione dello staff** e marcata scorciatoia, il nuovo indirizzo apre la
mappa (7 famiglie, 28 tessere Esri, attribuzione a posto) e il **vecchio risponde 404**. ⚠️ Il **rifiuto** non è stato provato dal vivo — l'utente di sviluppo è staff — ma lo
provano i test, livello per livello.

⚠️ **Trovato e non corretto**, perché è contenuto e non codice: il «?» della pagina punta a
`/services/vsop/guide#spazi-aerei`, e quella **sezione della guida non esiste**. Il collegamento non è
rotto — il browser ignora un'ancora sconosciuta e si apre la guida — ma promette un punto che non c'è.

## AL. I vSOP militari non pubblicati: verificato, e come si verifica — 1 settembre 2026

Domanda del committente su `/services/vsop/mil`: «mi confermi che i documenti non pubblicati qui sono
visibili solo a chi ha i permessi?». **Sì**, e non per deduzione: misurato dal vivo impersonando un utente
**senza permessi**.

### La catena, per intero

| dove | che cosa la chiude |
|---|---|
| **L'elenco** | `EfMilitaryDocumentService.ListAsync`: `perStaff ? righe : righe.Where(r => r.Pubblicato)`, dove `Pubblicato` = esiste una `DocReleases` di tipo `AirportMil` con `ReleaseEffectiveUtc <= adesso`. ⚠️ Il gate sta nel **servizio**, non nella pagina: una pagina che filtra è una pagina che può dimenticarsene. |
| **Il documento per indirizzo** | `EfContentRepository.LoadVipiAsync`: sul percorso pubblico puro, **niente release effettiva ⇒ `null`**. Non è un filtro sul markup: il documento non viene proprio caricato. |
| **`?as=draft`** | Gated su `Authz.IsEditor`; a chi non lo è **degrada alla vista pubblica**. |
| **`?as=rel:N`** | `ReleaseService.GetPreviewAsync` chiama `EnsureCanEditAsync` e, se rifiuta, torna `null` → di nuovo la vista pubblica. E controlla che la release sia **di quel documento**: una release altrui sotto un altro indirizzo è rifiutata. |

⚠️ **«Chi ha i permessi» qui vuol dire Editor e Admin**, non staff di divisione: la pagina chiama
`ListAsync(perStaff: Authz.IsEditor)`. Un `DivisionStaff` vede l'elenco **pubblico**.

### La misura

Dati di sviluppo: **LIBG, LIML, LIMN** pubblicati con release effettiva; **LIBN e LIMS** in **bozza, senza
nessuna release**. Impersonando un utente qualunque (VID non fondatore, posizione staff che nessun pattern
riconosce):

- elenco: **3 righe**, tutte «published». LIBN e LIMS **non ci sono**, e non c'è nessun tasto «Crea» o
  «Modifica» (misurati: 0 e 0);
- `?icao=LIMS` diretto → «No military vSOP published», **zero sezioni**, corpo di 215 caratteri;
- `?icao=LIMS&as=draft` → **identico**: l'anteprima bozza degrada, non mostra niente;
- `?icao=LIBN` → idem;
- `?icao=LIBG` (pubblicato) → si vede, 26 sezioni: il confronto che dimostra che il vuoto di sopra non è
  la pagina rotta;
- `?icao=LIBG&as=rel:45` → **vista pubblica**, senza il banner d'anteprima;
- `?icao=LIMS&as=rel:45` (la release di **un altro** documento) → rifiutata.

ℹ️ Dallo stesso giro, con l'utente **Editor**: l'indirizzo semplice di un documento in bozza mostra
comunque «No military vSOP published». Per vedere una bozza bisogna **chiederla** con `?as=draft`.

ℹ️ Il caso «release **programmata** ma non ancora in vigore» non si è potuto misurare — nel `vipi.db` di
sviluppo non ce n'è nessuna — ma passa dalla stessa condizione `ReleaseEffectiveUtc <= adesso`, in
tutt'e due i punti.

### ⚠️ Come si impersona un livello più basso, che è la parte non ovvia

Non basta togliere le posizioni staff: `RoleResolver.Resolve` comincia con
`if (_founders.Contains(userId)) return VipiRole.Admin`, e **il VID di sviluppo 704798 è un fondatore**.
Col primo tentativo — VID invariato, posizione `XX-ZZ9` — l'elenco mostrava **ancora** le bozze, e la
misura sarebbe stata sbagliata credendola giusta. Serve **anche** `DevIdentity__UserId`. E le promozioni a
mano (`RoleOverrides`) non servono al contrario: possono solo **alzare** il livello. Scritto in
`.claude/skills/verifica-live/SKILL.md`.
---

## AM. Quattro difetti letti nella diagnostica, e uno era la diagnostica — 31 agosto 2026

Carta: [`feature/2026-08-31-corse-dbcontext-e-diagnostica.md`](feature/2026-08-31-corse-dbcontext-e-diagnostica.md).
Ramo **`corse-e-perdita-diagnostica`**, aperto da **`consegna-20260831`** — cioe' da **cio' che gira**, non
da `main`, che e' 41 commit indietro. Build Release **0 avvisi** su net8 e net10, **14 test nuovi**,
⚠️ **nessuna migrazione**: consegnabile dentro la finestra cieca.

### Il fatto

Due sintomi dal committente, senza uno schema evidente: la pagina **«This page did not open»** (da cui
bisogna tornare alla documentazione a mano), e **«A second operation was started on this context
instance»** sotto un documento su cui sta lavorando — ⚠️ **pur essendo l'unico a lavorarci**. Piu' la
cartella `diagnostica/` scaricata via FTP.

⚠️ **La direzione l'ha data `avvii.txt`, non il registro degli errori.** Due righe:

```
10:57:28Z  AVVIO  il processo precedente NON si e' spento in modo ordinato — era partito 03:01:56 prima
13:05:22Z  AVVIO  il processo precedente NON si e' spento in modo ordinato — era partito 02:07:54 prima
```

Due morti male in una giornata, a due-tre ore l'una dall'altra: una cadenza cosi' regolare non e' un difetto
che scatta su un gesto, e' **qualcosa che cresce**. `avvii.txt` esiste da un giorno solo (§AE) ed e' il file
che ha risolto l'indagine — **un registro di *stato* vale piu' di un registro di *errori***.

### I quattro difetti

- 🔴 **AM-A · `CollisioniDbContext` perdeva memoria a ogni query.** Lo strumento scritto il 24 agosto
  *per capire* le corse sul `DbContext` teneva, accanto alla tabella debole, un elenco delle liste vive
  (`Viventi`) e ci aggiungeva un riferimento **dentro `Apre()`**, cioe' **a ogni comando SQL del processo**;
  la potatura stava solo nello scatto della fotografia, che gira quasi mai. Con quattordici `HostedService`
  piu' il traffico sono milioni di oggetti in poche ore. **La prova a occhio nudo**: dentro una sola
  fotografia la stessa lista compare **34, 38 e 44 volte**, identica riga per riga — non erano trentotto
  query concorrenti, era una lista sola stampata trentotto volte.
  **Adesso**: `Viventi` non esiste piu' (`ConditionalWeakTable` si enumera da se'), tetto di 64 operazioni
  per contesto, interruttore `VIPI_DIAGNOSTICA_COLLISIONI=0`, e via `DataReaderClosing` dall'intercettore
  (EF chiama *sia* Closing *sia* Disposing: la seconda chiusura toglieva la riga di un'**altra** operazione
  con lo stesso SQL, cioe' proprio quella che raccontava la collisione).
  ⚠️ **La regola che resta**: *uno strumento di diagnosi non puo' tenere stato che cresce con il
  traffico.*

- **AM-B · Il registro allegava a ogni errore venti fotografie, quasi tutte di altri guasti.** 634 kB per
  **tre** errori, su un tetto di rotazione di 512 kB: il file si metteva da parte dopo tre voci e la storia
  che serviva non c'era gia' piu'. E la voce delle **11:40:17** portava uno scatto delle **11:37:06** — di
  un'altra richiesta, con dentro query che con quella pagina non c'entravano. ⚠️ Una scena di un altro
  guasto allegata al tuo **la prima volta la si legge come se fosse la tua**: e' il modo di sbagliare che il
  24 agosto era gia' costato un giro di deploy. **Adesso**: una sola fotografia, l'ultima, e solo se
  scattata **entro dieci secondi**.

- **AM-C · Il pannello delle traduzioni correva contro l'editor che lo contiene** — e' il «second
  operation» che il committente vedeva «pur essendo l'unico». La corsa non era fra due persone: era fra il
  pannello e chi scrive. Tre difetti nello stesso metodo, ⚠️ **ognuno gia' scritto nero su bianco
  altrove**: non isolato (scope del circuito), lettura **non condizionata al cambio dei parametri**
  (`ReleasePanel`, 1 agosto: *il pericolo e' il RI-render, non il montaggio*), e **documento caricato DUE
  volte** — la seconda con l'altra lingua, solo per dedurre se il vuoto significasse «stessa lingua» — per
  un blocco **chiuso di suo** che nessuno stava guardando.
  **Adesso**: `_lettura` dallo scope proprio per il ciclo di vita, guardia su `(DocumentId, lingua)`, e
  `RevisioneAsync` che **dice** la lingua sorgente invece di farla dedurre (la deduzione vecchia sbagliava
  anche: su un documento senza frasi rispondeva «no» a chi leggeva proprio nella lingua del documento).
  ⚠️ **`Revisione` resta `@inject` per la SCRITTURA, e non va «uniformato»**: `CorreggiAsync` passa da
  `IEditAuthorizationService`, che legge l'identita' dall'`HttpContext`; in uno scope creato dopo la
  richiesta risponderebbe «anonimo» e **il salvataggio verrebbe rifiutato a tutti**.

- **AM-D · La home moriva su una lettura che la barra sopra proteggeva gia'.** `SopHome.OnInitialized()`
  era `=> Stations.Prewarm();`, nudo. ⚠️ La parte istruttiva: il rimedio c'era, e stava **un piano
  sopra**. La barra quella lettura la protegge dal 24 agosto — ma **ingoiare lascia la cache vuota**, e la
  pagina sotto ritenta sullo stesso contesto rotto. Il presidio della barra proteggeva la barra e non cio'
  che le sta dentro. **Adesso**: stesso gesto nella pagina, catalogo in un campo suo (la proprieta' pigra
  del servizio **lancia dentro il markup**, dove non si cattura niente), e un avviso **suo** — dire «il
  database e' vuoto» a chi ha avuto un singhiozzo manda a cercare un guasto che non c'e'.

- **AM-E · Il rimedio alla radice: il catalogo si legge una volta per PROCESSO.** C e D sono due modi
  diversi di proteggersi dalla **stessa** lettura — ACC e mappa aeroporti, che `IStationResolver`
  (`scoped` = per circuito) rileggeva per ogni sessione aperta e per ogni richiesta SSR. In una settimana
  quella lettura e' finita nello stack di **tre** guasti. Adesso sta in `CatalogoStazioni`, singleton: il
  **cosa** nel singleton, il **come si legge** nel resolver, che ha l'`IStationDirectory` del suo scope
  (⚠️ un singleton con dentro un `DbContext` sarebbe una dipendenza prigioniera, cioe' il difetto
  spostato invece che tolto). Copia e versione in **un oggetto solo** — due campi non si leggono insieme e
  si terrebbe per buona una copia scaduta per sempre; versione letta **prima** della query; lettura
  fallita **non** messa in cache; serratura tenuta **durante** la lettura, cosi' venti circuiti a freddo
  fanno una query invece di venti (con `MaximumPoolSize=20` erano il pool intero per sette righe).
  `Prewarm()` **resta**: qualcuno dev'essere il primo, e su Passenger — che spegne per inattivita' — il
  primo capita a ogni risveglio.

  🔴 **E il difetto che questa modifica avrebbe CREATO, se non lo si fosse cercato prima.** Allargare la
  cache sposta il peso su `IStationCatalogVersion`. Conto fatto prima di scrivere una riga: **4** chiamate a
  `Bump()` contro **11** posti che scrivono `Acc` o `Airport`. Mancava in `CreateAcc`, `DeleteAcc`,
  `CreateAirport`, `DeleteAirport`, `MoveAirport`, `SetAirportHidden`, in tutta la catena di eliminazione e
  nella scrittura delle **coordinate** dell'aeroporto. ⚠️ Nessuno se n'era accorto perche' la copia era
  scoped: il dato vecchio durava un istante. Con la copia di processo, un amministratore che crea un ACC
  non lo vedrebbe comparire — ne' lui ne' nessun altro. Rimedio: **non** ricordarsi la riga in sei posti in
  piu', ma spingere **dove avviene la scrittura** — `BumpCatalogoStazioniInterceptor`, un
  `SaveChangesInterceptor` che guarda il change-tracker. Le quattro chiamate a mano sono state tolte.
  ⚠️ `Modified` conta quanto `Added`/`Deleted` (quota, IATA, coordinate e i segni militari cambiano con un
  `UPDATE`: un filtro sul solo inserimento avrebbe lasciato fuori il giro notturno); si spinge **prima** del
  salvataggio, perche' una spinta di troppo costa una rilettura e una mancata costa un dato falso fino al
  riavvio; e l'intercettore va montato su **tutti e tre** i provider, o si ottiene un ambiente in cui il
  catalogo non si aggiorna piu' e nessun test dell'altro lo direbbe.

### Quel che resta

- **AM1 ✅ FATTA nello stesso giro** — il catalogo in memoria di **processo**. Vedi **AM-E** qui sopra:
  la misura da fare prima c'era davvero, ed e' servita (`Bump()` mancava in sei metodi su sette).
- **AM2 🔴 La verifica dal vivo, che i test non possono fare.** Le corse si aprono con la **latenza di
  un database remoto**: in locale la finestra e' quasi nulla. Serve un'identita' con permessi di editor, su
  `/services/vsop/libb/editor` e `/services/vsop`, **subito dopo un riavvio** (memoria fredda).
- **AM3 🟡 Rileggere `avvii.txt` fra qualche giorno.** La perdita di memoria di AM-A si vede **solo in
  esercizio**: la conferma e' **l'assenza** di righe «NON si e' spento in modo ordinato». Da fare insieme
  ad **AE1**, che chiede la stessa rilettura per un altro motivo.
- **AM4 🟡 Cancellare `errori-richieste.txt` sul server** dopo il deploy, come gia' chiedeva §A16: un
  file di errori vecchi che resta li' fa suonare un allarme a ogni controllo futuro.

## AN. Lingua bloccata: un documento in una lingua sola, e il canale che era rotto — 31 agosto 2026

Carta: [`docs/feature/2026-08-31-lingua-bloccata.md`](feature/2026-08-31-lingua-bloccata.md) ✅.
Ramo `lingua-bloccata`. `Document.LanguageLocked` + migrazione additiva sui due provider (`AddColumn`,
default `0`: passa la guardia della finestra cieca). L'interruttore sta in `ReleasePanel`, accanto ai tasti
che pubblicano, insieme alla **lingua di redazione** — che fino a ieri era cablata (`Vloa → En`, il resto
`It`) e quindi un vSOP inglese non si poteva nemmeno dichiarare.

⚠️ **Il canale su cui doveva viaggiare era rotto da sempre, e si è visto solo guardando dentro un payload.**
`EfContentRepository.BuildRawFromVersionAsync` non copiava `Document.Language` nello snapshot di release:
**13 payload su 13** nel `vipi.db` vero dicono `"Language":null` (confermato su una seconda copia, 5 su 5).
Da lì, in silenzio: il congelamento delle traduzioni non è **mai** scattato — tutta la §6 della carta
bilingue, scritta e testata, in produzione non girava — e la prosa derivata si è sempre congelata in
italiano, anche per una vLOA che nasce inglese. I test coprivano il lato del **lettore** e mai quello di chi
**scatta la fotografia**: un modello di prova che si costruisce da sé non può accorgersi di un campo che la
produzione non riempie.

⚠️ **Il confine «documento sì, pagina no» non si ottiene dall'ordine di render.** Primo tentativo: lingua
imposta alla richiesta → il documento usciva giusto e l'arredamento lo seguiva («Print / SUMMARY / LINKS»
dentro un sito italiano). Secondo: accenderla nel componente del corpo, contando sul fatto che la pagina
rende prima dei figli → pagina **a chiazze**, «Ciclo AIRAC» accanto a «Print» e un callout «Nota» italiano
dentro un documento inglese. In Blazor una pagina si rende **più volte**. Il confine ora è esplicito e sta
in un posto solo (`StringheDelSito`): **dentro una pagina documentale `L` è la lingua del DOCUMENTO,
`Sito` è quella di chi guarda.**

⚠️ **Le isole interattive non vedono la lingua della richiesta**: `@rendermode` = circuito suo, scope suo.
Sono due e stanno dentro un documento (METAR, SID): ricevono la lingua come parametro e risolvono le
stringhe a mano. **Non** si impone al contesto del circuito — lì uno scoped vive quanto il circuito, non
quanto la pagina, e resterebbe acceso sulle pagine visitate dopo.

Chiusi per strada, ed erano difetti **già visibili** prima di questa carta: le testate di catalogo di una
vIPI d'aeroporto non si traducevano mai (§Q18a della carta bilingue — ora `SectionDescriptor.TitleEn` su
tutti e 72 i descrittori, con guardia strutturale) e le etichette dei callout erano letterali italiani
(«Nota», «Attenzione») dentro un componente del corpo.

Verificato **a schermo** su copia del `vipi.db` (LIBD, sito italiano, documento bloccato in inglese),
bloccando e sbloccando dall'editor vero. Suite 9192 verdi su entrambi i TFM.

⚠️ **Aperto**: la spesa. Un documento bloccato esce dal corpus (`EfTranslatableCorpus`), ma una frase
presente **anche** in un documento non bloccato si traduce lo stesso — la memoria è indicizzata sulla frase,
non sul documento. È giusto così, e va detto a chi guarda il conto dei caratteri.

## AP. Coerenza col sectorfile: due sorgenti a confronto — 1 settembre 2026

> ✅ **ESEGUITO e FUSO IN `main`** (`50028edc`; ramo cancellato). Carta:
> [`design/piano-coerenza-sectorfile.md`](design/piano-coerenza-sectorfile.md). Perimetro e motivi dei «no»
> alle altre proposte: [`design/regole-perimetro-servizi.md`](design/regole-perimetro-servizi.md).
> **Nessuna entità, nessuna migrazione** — si poteva fare dentro la finestra cieca, e infatti è stato fatto.

### Il fatto

vIPI prende posizioni, frequenze, aeroporti, TA e piste dall'**API IVAO**. Il **sectorfile Aurora** della
divisione descrive le stesse cose, e lo scrive l'IT-AOD. **Nessuno le confrontava**, quindi una divergenza si
scopriva in frequenza. Ora un giro ogni 24 ore legge `OTHER/itfreq.frq`, `OTHER/itap.ap` e `OTHER/itrw.rw` e
dice **dove non concordano** — non chi ha ragione, che da qui non si sa e non si ripara.

### La prima slice è stata misurare, e ha cambiato il disegno

Lo script buttabile ha risposto **prima** che si scrivesse una riga di produzione, e ha corretto quattro cose
che sarebbero diventate una pagina di rumore:

- 🔴 **il QFU non si confronta**: 115 divergenze a 1°, 55 a 2°, 10 a 3°, **zero a 5°** — non esiste una soglia
  che separi il segnale dal rumore perché segnale non ce n'è, e togliere la declinazione magnetica **peggiora**
  il residuo. Il controllo è stato **eliminato dalla carta**;
- `itrw.rw` **non contiene le lunghezze di pista**: i campi 4 e 5 sono le elevazioni delle soglie (la
  descrizione in `STATO_SECTORFILE_ITALIANO.md` §5 dice «lunghezze» ed è imprecisa);
- i cataloghi vIPI contengono **142 confinanti esteri** e **25 ATIS**: fuori dal confronto, o la famiglia
  «posizioni» nasce con 167 falsi;
- gli **ident pista** hanno lo zero iniziale da una parte sola (`09` vs `9`) e fra gli «aeroporti» ci sono i
  **codici ACC**.

### Le tre decisioni che reggono il lavoro

1. 🔴 **L'health check ignora l'area nuova.** `findings.Count > 0 ⇒ Degraded` avrebbe reso `/vsop/health`
   giallo **per sempre** — le due sorgenti hanno cadenze diverse, qualche divergenza c'è sempre. Il conteggio
   è stato estratto in una funzione pura (`VipiHealthCheck.ContaIncongruenze`) perché è una **decisione**, e
   ha un test suo.
2. **Il confronto non gira dentro la richiesta**: fa I/O di rete e il report di consistenza lo legge anche
   `/vsop/health`, che è **anonimo**. Modello di `IStartupMaintenanceReport`: il giro prende la fotografia,
   la pagina la legge. ⚠️ E **nessuna riga in `ImportStates`**: quello è il registro di ciò che **scrive**.
3. **Visibile, ma come scorciatoia** (richiesta del committente): pagina `/services/vsop/sectorfile` con
   cancello in due sedi, scheda `shortcut` nella sezione staff dell'hub. La Diagnostica la apre solo
   l'admin, e questa scende di un gradino perché la aprano anche i **chief d'ACC**.
   ⚠️ Il livello è **Editor**, non staff di divisione (seconda richiesta del committente, a giro fatto): è
   un gradino **più su** delle altre due schede di quella sezione, e la ragione è che questi rilievi parlano
   del **contenuto dei documenti** — chi li legge deve poterci fare qualcosa.

### Che cosa si vede, sui dati veri

**36 rilievi**: 13 posizioni · 7 aeroporti · 16 piste. I due grossi sono `LIRF_PS1_APP` (136.100 contro
131.100) e `LIRM_APP` (132.255 contro 135.255) — **5 MHz e 3 MHz**. Tre TA divergenti (`LICD`, `LIMF`,
`LIMZ`). Dodici aeroporti con **designatori di pista diversi**: `LIRP` 22L/22R/4L/4R contro 21L/21R/3L/3R,
`LIED` 17→16, `LICG`, `LIPR`, `LIBA`, `LIRS` — rinumerazioni per deriva magnetica applicate da una parte
sola, ed è la famiglia che vale il lavoro: se il sectorfile dice pista 17 e il documento dice 16, si sente in
frequenza. E una coordinata rotta che nessuno avrebbe mai visto: `LIAA/27`, soglie a **4 907 km**.

### ⚠️ Il difetto visto solo a schermo

La testata della tabella si disegnava **sotto la prima riga** (misurato: 320 contro 295). Non era la pagina:
`.res-table.sticky-head thead th` ha `top:62px` — l'altezza della topbar — e dentro un contenitore con
`overflow` (`.st-scroll`) il riferimento dello sticky diventa **quel contenitore**, quindi 62px smettono di
essere «resta sotto la barra» e diventano uno **spostamento in giù**. In Diagnostica non si vede perché lì
una regola più specifica (`.struct .st-scroll …{top:0}`) lo azzera, e vuole un antenato `.struct`. Tolto il
`.st-scroll`: testata contigua alla prima riga, e scorrendo si incolla a **esattamente 62 px**.
⚠️ La lezione: **una classe copiata da un'altra pagina si porta dietro le regole di quella pagina**, e nessun
test lo vede — il DOM era corretto.

### Quel che resta

- ✅ **In produzione dal 2 settembre 2026**, col pacchetto **1.4.0** (che porta anche §AO).
- 🟡 Alla **seconda** ricognizione varrà la pena guardare quali divergenze sono sopravvissute a un ciclo
  AIRAC: quelle sono le vere, le altre erano il sectorfile in anticipo (§4 della carta).

## AQ. I titoli delle sezioni seguono la lingua del documento — 1 settembre 2026

✅ **Fuso in `main` il 2 settembre 2026** (`c3ad9dab`; ramo `titoli-di-catalogo-bilingui`, cancellato).
Nessuna carta: è un **difetto**, segnalato dal committente in una riga —
*«quando si blocca la lingua in inglese e si flagga per non tradurre, i titoli delle sezioni rimangono in
italiano»*.

### Che cos'era davvero

Non mancava una traduzione: mancava il **proprietario** di quel titolo. Le sezioni fisse di un documento
prendono il titolo dal profilo (`SectionCatalog`) ma se lo portano **scritto dentro**
(`DocumentSection.Title`, seminato da `DocumentBirth` con `TitleIn(lingua)`), nella lingua che il documento
aveva **in quel momento**. `SetLanguageAsync` cambia lingua e blocco e non tocca nient'altro; l'editor non
può rimediare perché una sezione fissa **non si rinomina a mano** — il campo di rinomina esiste solo per le
sezioni libere. Vicolo cieco: nessuno, in nessun modo, poteva mettere quei titoli in inglese.

⚠️ **Perché non se n'era accorto nessuno.** I titoli **sono** segmenti del documento
(`DocumentTranslator.SegmentiSezione` parte proprio da lì), quindi un lettore inglese li otteneva dalla
memoria di traduzione: la copertura diceva «tutto tradotto» ed era vero. Poi è arrivata la lingua bloccata
(§AN): bloccare **spegne** la traduzione — sorgente e bersaglio coincidono, `PreparaAsync` esce a
`TranslationPass.Nessuna` — e con la traduzione è caduta la stampella. **Un difetto vecchio reso visibile da
una funzione nuova**, non un difetto della funzione nuova.

⚠️ **Ed era già stato trovato una volta, in una famiglia sola.** La §Q18a della carta bilingue è
esattamente questo, visto sulla vIPI di Crotone il 28 agosto: fu riparato dentro
`AirportLegacySections.ForView`, cioè nel percorso dell'**aeroporto**. Le altre quattro famiglie rendevano
il titolo che il documento portava — `SectionNode` mostra `Section.Title` — e l'assembler ACC risolve dal
catalogo **solo** le sezioni che il documento non ha mai scritto. Una riparazione locale a un difetto
generale non lascia niente che protesti.

### Come si è chiuso

`TitoliDiCatalogo` (in `Vipi.Application.Content`): una risoluzione sola, **a view-time**, ricorsiva.
Agganciata ai quattro viewer che non ce l'avevano (`MilDocumentPage`, `AppnPage`, `VloaListPage`,
`AccVipiPage` — quest'ultima sul posto, come fa il suo traduttore) e a `DocumentSectionsEditor`, che la
applica **in sola visualizzazione**: card e indice, senza scrivere niente nel modello.

⚠️ **A view-time e non riscrivendo il DB**, ed è la decisione che regge tutto: riscrivere i titoli quando
cambia la lingua sistemerebbe la sola bozza di lavoro, mentre le release **già pubblicate** portano i loro
dentro lo snapshot e non si toccano (doc 13 §9). Il lettore le vedrebbe italiane per un ciclo AIRAC intero.
Il blocco è una regola di **servizio**: vale appena si accende — stesso ragionamento della §AN, dove il
lettore chiede lingua e blocco al documento **vivo** e non allo snapshot.

⚠️ **Il catalogo vince sulla memoria di traduzione**, quindi il passo si applica **dopo** la traduzione: è
la resa **decisa** contro quella **plausibile**, ed è quel che impedisce a «MRVA» di tornare «Minimum
vectoring» — che è successo davvero, ed è il motivo per cui quella sezione si chiama MRVA.

⚠️ **La vLOA va nel verso opposto, e ha imposto una distinzione nuova.** I suoi titoli di catalogo sono già
**inglesi** — è una lettera d'accordo bilaterale — quindi «risolvi dal catalogo» letto in italiano avrebbe
imposto «Purpose» a chi legge in italiano, **cancellando** l'unica resa italiana che quel titolo può avere:
quella del traduttore. Il catalogo scrive solo **dove ha davvero una resa** in quella lingua, e la lingua
nativa dei titoli la dice `SectionCatalog.TitoliInInglese`. ⚠️ Stava scritta a mano dentro
`CatalogoBilingueTests` (l'elenco dei «profili italiani»): ora la guardia la **legge** dal catalogo, o un
profilo nuovo ne resterebbe fuori in silenzio — il modo esatto in cui una guardia smette di guardare.

⚠️ **Un titolo di catalogo VUOTO non è una resa**: le due sezioni di coordinamento della vLOA stanno nel
`ChildRegistry` con titolo «» perché il loro dipende dai codici della coppia e lo compone la pagina.
Imporlo vorrebbe dire cancellarlo.

⚠️ **Ricorsivo, e non è teoria**: il vSOP militare ha **venti sezioni di catalogo su ventisei** dentro
quattro contenitori. Una risoluzione ferma al primo livello avrebbe lasciato italiane proprio quelle — ed è
la famiglia dove il difetto si vede di più.

### Che cosa resta

- Nuova **R9** in [`design/regole-lingua.md`](design/regole-lingua.md), e corretto lì il paragrafo che
  diceva «serve un passo di manutenzione all'avvio»: vale per **rinominare** una sezione, non per mostrarla
  nella lingua giusta.
- ✅ **Verificato dal vivo** (Edge headless su copia del `vipi.db`, tre documenti messi a `Language='En'`,
  `LanguageLocked=1`): **sito in italiano** (`<html lang="it">`, barra e avvisi italiani) e documento
  bloccato in inglese ⇒ testate e indice **tutti inglesi**. Il vSOP di Grottaglie ha dato 26 voci d'indice
  in inglese comprese le venti annidate; l'APP di Amendola dieci; la vIPI ACC di Brindisi entrambi i blocchi
  (con «Radar separation» sull'Aerovia e «Separations» sul gruppo APP — profili diversi, sulla stessa
  pagina); l'editor del vSOP le stesse ventisei fra card e indice. **Nessun errore di pagina, nessuna
  console.error, nessun 4xx.**
- ✅ **Nessuna regressione sui NON bloccati**, che è la metà che poteva rompersi: APP di Pescara e vIPI di
  LIBD letti in italiano restano italiani e in inglese vengono inglesi; una **vLOA non bloccata letta in
  italiano** mostra ancora «Scopo / Aree di Responsabilità / Validità e revisione», cioè la resa del
  traduttore — quella che imporre il catalogo avrebbe **cancellato**. Il verso opposto, la vLOA bloccata in
  inglese dentro un sito italiano, mostra i titoli inglesi e l'avviso «Documento in una lingua sola» in
  italiano: il confine di §AN, intatto.
- Suite verdi: `Vipi.Application.Tests` 2004, `Vipi.Ui.Tests` 1070, `Vipi.Infrastructure.Tests` 1190;
  `dotnet build Vipi.slnx -c Release --no-incremental` pulita.
- 🟡 **Trovato per strada, e NON è di questo giro**: i passi del **giro guidato** (`vipi-tour.js`) hanno
  titoli e testi **cablati in italiano** — «Indice del documento», con l'interfaccia in inglese. È
  un'eccezione non dichiarata a R7, e vale per tutte le pagine che montano il tour.
- ✅ **In produzione dal 2 settembre 2026**, col pacchetto **1.4.0**, insieme a §AO, §AP, §AR, §AS e §AT.

## AR. La pagina che si blocca in salvataggio: due buchi e una corsa — 1/2 settembre 2026

✅ **Fuso in `main` il 2 settembre 2026** (`bb265c44`; ramo `editor-non-si-blocca`, cancellato). Suite intera
verde sul risultato della fusione, E2E compresi (276/276), Release pulita sui due TFM.
Nessuna carta: è un **difetto**, segnalato dal committente — *«quando si crea
una sezione per un documento la pagina si blocca in salvataggio e si deve ricaricare la pagina per farla
salvare»*, sul gesto **«Fine modifica»**, **sul sito vero**.

### ⚠️ Non si riproduce, e il primo lavoro è stato ammetterlo

Guidato dal vivo l'editor del vSOP di Grottaglie — copia del `vipi.db`, browser vero, gesti veri: sezione
nuova, blocco allegato senza sceglierne uno, nota, tendina rimessa su «nessuno», **biblioteca svuotata** (così
il link non si *può* mettere), tre blocchi vuoti di fila, scritture ravvicinate a 150 ms sullo stesso blocco,
anteprima bozza, pubblicazione, e **dieci giri** del ciclo completo con clic veri. Sempre pulito: badge
«Salvataggio…» → «Salvato», zero `pageerror`, zero 4xx, log del server senza una riga.

⚠️ **Su SQLite in locale la finestra è di millisecondi.** Il difetto è una CORSA, e in produzione gira
MariaDB: la latenza vera apre la finestra. Dai file di diagnostica presi via FTP: nessun
`errori-richieste.txt` — ma quello registra gli errori di **richiesta**, e un'eccezione di **circuito** non
passa di lì. Il registro degli avvii però dice che il processo è morto male **il 1 settembre alle 18:57Z**,
dopo dodici minuti di vita.

### I due buchi, trovati leggendo il gesto indicato

**1) `FinishEditingAsync` rileggeva il lock FUORI dal guardiano** — l'unico `await` non protetto della
classe. Un'eccezione lì non la prende nessuno: esce dal gestore dell'evento e **abbatte il circuito**. A
schermo: si preme «Fine modifica» e la pagina non risponde più, senza errore. Il lavoro era già salvato — i
gesti dell'editor salvano uno per uno — quindi ricaricare «lo faceva salvare». Ora tutto dentro il guardiano,
e si esce dalla modifica **comunque**: restare «in modifica» dopo aver chiesto di uscire è lo stato peggiore
dei tre.

**2) Il guardiano non chiedeva il ridisegno alla fine.** Accendeva «Salvataggio…» chiedendolo, poi contava sul
render automatico dell'evento — che ridisegna il componente che l'evento l'ha **ricevuto**, mentre badge ed
errore li disegna la **pagina**. Un gesto nato dentro un componente figlio (allegato, immagine, editor
strutturati) lasciava il badge inchiodato e il messaggio invisibile. Ora il ridisegno si chiede sempre, in
`finally`. ⚠️ E la rilettura del lock dentro il `catch` del conflitto non solleva più: un'eccezione lanciata da
un `catch` esce dal guardiano intatta, proprio mentre si stava scrivendo che cos'era andato storto.

⚠️ **Test-first**: cinque test nuovi sul guscio, **quattro rossi** prima della riparazione — e quello
sull'eccezione che scappa falliva perché l'eccezione scappava davvero.

### La corsa sotto, e il rimedio strutturale

In Blazor Server **«scoped» vuol dire per CIRCUITO** — per sessione, per ore, non per richiesta
(`blazor-scoped-is-session-cache`). Un `@inject IEditingService` in una pagina prende quindi il `DbContext`
che la sessione condivide con barra, isole e pannelli. «Fine modifica» fa due operazioni di fila (rilascia il
lock, rileggilo) **proprio mentre** il campo di testo che stavi scrivendo perde il fuoco e fa partire il
salvataggio del blocco, che a sua volta richiama il `LoadAsync` della pagina: due operazioni sullo stesso
contesto = `A second operation was started on this context`.

✅ **Sei pagine** passano a `OwningComponentBase` + `ScopedServices`, come già facevano `DocumentSectionsEditor`
e `VloaEditor`: i quattro editor documentali, «Nuovo documento» e «Bozze & versioni».

⚠️ **Due trappole della conversione, tutte e due silenziose:**
- un `public void Dispose()` con `OwningComponentBase` **non lo chiama nessuno** — la base implementa
  `IDisposable` in modo *esplicito* — e la pulizia salta senza un errore: va scritto
  `protected override void Dispose(bool)`;
- una pagina `IAsyncDisposable` **non riceve mai** il `Dispose` sincrono, che è quello che chiude lo scope:
  l'editor aeroporto lo chiude a mano, in un `finally`. Senza, ogni visita lasciava in piedi uno scope con
  dentro un `DbContext`.

E il vSOP militare il guscio non lo chiudeva affatto: era l'unico dei quattro senza `Dispose`.

`ScopeDellEditingTests` è la guardia strutturale: chi scrive documenti non prende il servizio di editing dal
circuito, non ha un `Dispose` pubblico, e la pagina async-disposable chiude lo scope a mano. ⚠️ La prima
stesura della guardia ha acceso **due rossi falsi**: cercava `public void Dispose()` col `Contains`, e quella
frase compare dentro il commento che spiega perché non si scrive. Una scansione sul testo vede anche i
commenti.

### Verificato dal vivo, dopo

Tutti e quattro gli editor guidati con clic veri sul ciclo intero — entra in modifica, aggiungi una sezione,
scrivi con il fuoco vero, esci: vSOP militare (21→22 sezioni), APP di Amendola (12→13), aeroporto di LIBD
(10→12), vIPI ACC di Brindisi (21→23). Nessun errore, nessuna pagina morta, lock rilasciato ogni volta.
Suite intera verde, E2E compresi (276/276), Release pulita sui due TFM.

### Quel che resta

- 🟡 **La corsa non è dimostrata, è dedotta.** Il rimedio la toglie e il guardiano ora la sopravvivrebbe
  comunque, ma se il blocco si ripresenta in produzione la prossima prova è il **registro degli avvii**: se
  accanto all'ora del blocco c'è un ⚠ «non si è spento in modo ordinato», allora non era una corsa — era il
  processo che moriva, e si guarda la memoria.
- 🟡 Trovato per strada e **non riparato**: `/services/vsop/admin/versions` mostra «Nessun documento
  corrisponde ai filtri» con tutti i conteggi a zero. **Identico su `main`**, quindi preesistente e fuori da
  questo giro.
- ✅ **In produzione dal 2 settembre 2026**, nel pacchetto **1.4.0** con §AO, §AP, §AQ, §AS e §AT. ⚠️ Il
  pacchetto si è chiamato 1.3.1 finché conteneva i soli §AO–§AR: il numero segue il **contenuto**, e con §AS
  e §AT dentro non era più una patch.

### ⚠️ Un lavoro che ha viaggiato dentro alla fusione, e non doveva

Con `bb265c44` è entrato anche `60a8823f`, **il piè di stampa di §AO**: era rimasto in lavorazione
nell'albero dal giro precedente, e un `git add -A <cartella>` l'ha rastrellato dentro un commit che parlava
d'altro. Rimesso in un commit suo **prima** di fondere, quindi il record è a posto — ma la regola vale per
chiunque: **i file si aggiungono per nome**, e prima di committare si guarda `git status`.

⚠️ E porta con sé una **correzione di rotta** già scritta nel CSS ma non nei documenti: il `bottom` del piè
di pagina **non può essere negativo**. Le tre sedi che dicevano «scelto −4mm» — la riga §AO in cima a questo
file, `history/rounds.md` e `HANDOFF.md` — sono state corrette nello stesso giro.

## AS. Il giro chiuso dei campi solo militari: i dati dello scalo senza una porta — 2 settembre 2026

🟡 **In lavorazione**, ramo da aprire su `main` (`b8e6b22c`). Nessuna carta: è un **difetto**, segnalato dal
committente — *«C'è un problema sui military only, che non hanno vIPI: in assenza di vIPI le cose che
normalmente sono in “This card is drawn by the document from the airport data. To change it: airport editor”
vanno direttamente qui»* — con l'indirizzo del caso, l'editor del vSOP di **LIBG Grottaglie**.

### Il fatto, e perché era peggio di un testo sbagliato

Nell'editor del vSOP militare le sezioni derivate mostravano «per cambiarli: **editor dell'aeroporto**». Su
un campo **solo militare** quel collegamento è un **giro chiuso**: `AeroportoEditorPage` legge lo stato
militare e, se il campo è solo militare **e** la vIPI civile non esiste, **rimanda indietro** all'editor
militare — perché `AirportEditingService.EnsureDocumentAsync` rifiuterebbe di far nascere il documento
(§11b). Verificato a schermo: il clic torna sulla **stessa pagina**, senza errore e senza spiegazione.

⚠️ La conseguenza non è cosmetica. **Livelli di transizione, colonne editoriali delle piste (TORA/LDA/APP/
Patterns/Circling) e collegamenti di frequenza non avevano NESSUNA porta di scrittura in tutto il sito.** Su
LIBG quei dati esistono solo perché ce li ha messi l'import IVAO; tutto ciò che è editoriale era
irraggiungibile per sempre. Campi colpiti oggi: **LIBG** (pubblicato) e **LIBN** (bozza); in arrivo LIBV,
LIPA, LIBA.

### ⚠️ L'asimmetria che lo dimostra: lo stato c'era già, la pagina non lo chiedeva

`CivilEdition(Esiste, Pubblicata, SoloMilitare)` esiste dal giro §X, e **viewer** (`MilDocumentPage`) ed
**elenco** (`MilListPage`) erano già gated su di esso. Solo la nota dell'**editor** era incondizionata. Non
mancava un'informazione: mancava la **domanda**.

### La regola, e le due risposte opposte

- Campo **misto** (o che una vIPI civile deve averla): si **rimanda**, come prima. Due editor sullo stesso
  dato sarebbero due verità che divergono, ed è la ragione per cui la nota è nata.
- Campo **solo militare SENZA vIPI civile**: si **scrive qui**. Là il secondo editor non esiste, quindi non
  c'è nessuna verità da far divergere.

⚠️ **La domanda è la STESSA che fa la pagina che rimanda indietro** — «solo militare **E** nessun documento
civile» — e non una che le somiglia. `SoloMilitare` da solo **non basta**: un campo marcato solo militare
*dopo* che la sua vIPI civile era nata continua ad aprirla (la guardia blocca la **nascita**, non
l'apertura), e lì il rimando è quello giusto. Due porte che decidono la stessa cosa devono chiedere la
stessa cosa, o una manda dove l'altra non lascia entrare. La guardia strutturale sta in
`DatiDelloScaloMilitareTests`.

### ⚠️ E il meteo non è un dato dell'aeroporto — su TUTTI i campi, non solo su questi

`weather` cadeva nel ramo generico e prendeva la stessa nota: «per cambiarlo, editor dell'aeroporto». Il
METAR/TAF è **live dal NOAA** e non si compila in nessun editor — nemmeno in quello dell'aeroporto, dove
infatti c'è la nota «non c'è nulla da compilare». Ora ha il suo ramo e **quella** nota
(`Ape_WeatherTitle`/`Ape_WeatherBody`, riusate: non una seconda stesura). Era sbagliato su tutti e ventisei
i campi militari, e nessuno l'aveva visto perché il campo di prova non aveva mai fatto quella domanda.

### Come si è chiuso

- `MilEditorPage` legge `GetCivilEditionAsync` **una volta per aeroporto**, e monta i tre editor
  dell'aeroporto — `AirportTransitionEditor`, `AirportRunwaysEditor`, `AirportFrequenciesEditor` — quando
  `ScaloSenzaCivile`. ⚠️ **Nessuna seconda stesura**: quei tre sono già componenti estratti e indipendenti
  dalla pagina che li ospita (doc 14 §3g), quindi qui c'è un secondo **ospite**, non un secondo editor.
- ⚠️ **`IAirportEditingService` e `IAirportSectorService` dallo scope PROPRIO della pagina**
  (`ScopedServices`), non da `@inject`: scrivono sul database, quindi vale parola per parola il blocco già
  scritto per `IEditingService` — con `@inject` arriverebbero dal `DbContext` del **circuito**, e il
  salvataggio di una pista correrebbe contro il `LoadAsync` di questa stessa pagina.
- ⚠️ **Il carico sta FUORI da `LoadAsync`**, che rigira a ogni salvataggio di blocco (`OnChanged` di
  `DocumentSectionsEditor`): questi tre editor hanno buffer a **salvataggio esplicito**, e rileggerli lì
  butterebbe via quel che si stava scrivendo perché *un'altra* sezione si è salvata da sé. Dopo i
  salvataggi di qui non si rilegge: il repository sostituisce in blocco, quindi i buffer **sono** già lo
  stato salvato.
- ⚠️ **La scrittura segue il RUOLO (`_canEditScalo`), non il lock del documento** (decisione del
  committente): quel lock governa il vSOP, l'anagrafica dell'aeroporto è un'altra cosa e si possiede
  separatamente. Legarli darebbe un editor spento a chi ha il permesso di scrivere.
- Guardia «modifiche non salvate» del browser (`vipiSetDirty`) aggiunta per queste tre sole sezioni: i
  blocchi del documento si salvano da sé, questi no.
- Testo: `Mil_HelpBody` perde la frase sul meteo; la riga su dove si cambiano i dati diventa condizionale
  (`Mil_HelpDataCivil` / `Mil_HelpDataOwn`), e la nota in pagina è `Mil_OwnDataNote`.

### Verificato dal vivo, sui dati veri

App pubblicata in scratchpad e guidata su una **copia** del `vipi.db` (porta libera: l'app di chi lavora
resta in piedi, e i `bin/` restano suoi).

- **LIBG** (solo militare): **zero** collegamenti all'editor d'aeroporto, tre note «dati dell'anagrafica»,
  la nota giusta sul meteo. Piste 17/35 con le colonne editoriali scrivibili, tabella livelli con le fasce
  QNH, TA col lucchetto «di sorgente». **Giro di andata e ritorno**: LDA di 17 scritta, «Salva piste»,
  ricaricata la pagina, valore **persistito**.
- **LIML** (misto): tre note di rimando coi collegamenti, **nessun** editor in pagina, e il collegamento
  **apre davvero** l'editor d'aeroporto. La nota del meteo è quella nuova anche qui.

### Quel che resta

- 🟡 **La TA resta di sorgente**, e il rifiuto che si prende scrivendola è quello di «Sorgenti dati» — lo
  stesso che si prende su Linate. **Non è una regola dei campi militari**, e confonderla con la guardia
  dell'edizione vorrebbe dire credere risolto un blocco che sta altrove. Il test lo pretende per iscritto.
- 🟡 **`SaveFrequencyLinksAsync` non è provata** nel test di scrittura (vuole un catalogo settori): è lo
  stesso servizio con la stessa chiave, ma è l'unica delle tre a fidarsi.
- ✅ **In produzione dal 2 settembre 2026**, nel pacchetto **1.4.0** con §AO, §AP, §AQ, §AR e §AT.

## AT. L'avviso di traduzione automatica diventa un gettone — 2 settembre 2026

🟡 **In lavorazione**, stesso ramo di §AS. Nessuna carta: richiesta del committente — *«questa può andare
nella riga … dopo ONLY FOR SIMULATION …? Così da occupare meno spazio, ora visivamente si mangia 1/4 del
documento a schermo»*.

### Il fatto

Il riquadro «Pagina tradotta automaticamente» era un `callout warning` a piena larghezza sopra il documento,
su tutte e cinque le famiglie. Su una schermata da 750px si prendeva circa un quarto dell'altezza utile:
il documento cominciava sotto la piega. ⚠️ **E un avviso che costringe a scorrere per arrivare a quel che si
è venuti a leggere è un avviso che si impara a saltare** — cioè il contrario di quel che serve, perché
questo non è una formalità: misurato contro il servizio vero, «riporta sottovento» torna *«bring it back
downwind»*, che è plausibile, grammaticalmente giusto e **non è fraseologia**.

### La forma nuova

Un **gettone** nella riga sotto il titolo, subito dopo l'avviso di simulazione, con il testo per esteso
dietro il «?» (`HelpHint`, che si apre al clic — [[help-hint-click-only]]).

⚠️ **Che cosa NON si perde**: che l'avviso esista e **quale** dei due problemi ci sia restano scritti in
chiaro — «tradotta a macchina» e «4/10 frasi non tradotte» sono due difetti diversi, e chi
legge deve poter decidere se il pezzo che gli serve è fra i quattro. Dietro il «?» va **solo** la frase
lunga, che è un'istruzione, non una notizia.

⚠️ **Giallo e non rosso**, e non è un dettaglio di gusto: il rosso in quella riga è già preso dall'avviso di
simulazione, che è l'unica cosa che deve gridare. Due rossi accanto non fanno due allarmi, ne fanno zero.

### La seconda passata, stessa sera: la riga era ordinata a metà

⚠️ **Appeso in coda al sottotitolo, il gettone stava in una riga che andava a capo in mezzo alla frase.**
Sulla vIPI ACC — dove il sottotitolo è lungo — si leggeva «…DO NOT USE FOR REAL LIFE / NAVIGATION», e il
gettone atterrava dopo quel troncone come se ci fosse finito per sbaglio. Segnalato dal committente, che
proponeva di spostare tutto sulla riga delle briciole.

Scelta (sua, fra tre proposte): **l'avviso e il gettone prendono una riga LORO**, sotto il sottotitolo — la
forma che il vSOP militare aveva già (`.sim-line`), che ora vale su tutte e cinque le testate. Le briciole
sono state scartate perché parlano del **sito**, non di quel documento: l'avviso deve restare attaccato al
titolo a cui si riferisce. Etichetta accorciata a «tradotta a macchina» / «machine-translated» — «non
riletta» resta dietro il «?».

⚠️ E il gettone sta nella **stessa** riga dell'avviso di simulazione, non su una terza: due righe di
cartelli sopra il documento sono di nuovo il problema che si stava togliendo.

### 🔴 E lì è saltato fuori un difetto che il Razor non mostra

⚠️ **Un `<p>` NON può contenere un `<details>`.** Il gettone porta dentro di sé il «?», che è un
`<details>`: il parser del browser **chiude il `<p>` da solo** e sposta il `<details>` **fuori**. A schermo
il «?» atterrava su una riga sua, sotto e a sinistra del gettone, come un glifo perso.

⚠️ **Non si vede leggendo il sorgente**: il markup scritto è giusto, è il **DOM** a essere un altro. Prima
non capitava perché il gettone stava dentro uno `<span>`, e solo il `<p>` ha quella regola di chiusura
automatica del parser. Chiuso portando `.sim-line` da `<p>` a `<div>` in **tutti e sette** i posti — anche
vista live e mappa degli spazi aerei, che il gettone oggi non ce l'hanno: chi ce lo aggiungesse domani
ricadrebbe dentro senza che niente lo avvisi. `AvvisoTraduzioneSuOgniSedeTests` lo pretende su tutti e sette.

### ⚠️ La trappola che lo spostamento porta con sé, e che non si vede leggendo la pagina

Il gettone sta dentro `.doc-head`, e **sul foglio `.doc-head` è nascosto**
(`.print-meta + .doc-head{display:none}`): spostandolo lì, **il documento stampato perdeva l'avviso** senza
che niente lo dicesse. È lo stesso inciampo già pagato con l'avviso di simulazione (§AO), e si chiude allo
stesso modo — una riga `.pm-tr` dentro `PrintMeta`, **col testo per esteso**: davanti a un foglio non c'è
nessun «?» da aprire né l'originale a portata di clic.

Il patto è quindi **doppio**, e `AvvisoTraduzioneSuOgniSedeTests` fa le due domande insieme su tutte e
cinque le testate: c'è il gettone? **e** la copertura arriva a `PrintMeta`? Lo stesso test pretende anche
che nessuna pagina tenga *anche* il riquadro pieno — basta lasciarne indietro uno in fondo a una pagina,
dove nessuno guarda, per riavere ciò che si voleva togliere.

⚠️ **`tr-notice` resta sull'elemento in ENTRAMBE le forme**: la rete che pretende «l'avviso c'è» non sa
quale forma sia, e così continua a proteggerle tutt'e due invece di diventare cieca su una.

⚠️ **La vLOA non ha una pagina con una riga sotto il titolo**: la testata è di `VloaDocumentView`, quindi la
copertura si passa al *componente*, non si disegna nella pagina. Un elenco che guardasse solo `Pages/` la
salterebbe in silenzio — la stessa nota vale già per l'avviso di simulazione.

### Verificato dal vivo

Stessa ricetta di §AS (scratchpad, porta libera, copia del DB). Sulla **vIPI ACC di Brindisi** e sulla
**vIPI d'aeroporto di LIBD**, lette in inglese: gettone nella riga giusta, documento che comincia subito
sotto il titolo, «?» che apre la frase intera, riga `.pm-tr` presente nell'intestazione di stampa. Provato
anche in **tema chiaro**. Sulle altre tre famiglie il gettone non compare perché quei documenti non hanno
traduzioni memorizzate — coperte dal test strutturale e dai test del componente. Riverificato dopo la
seconda passata: riga alta 24px (una sola), «?» dentro il gettone, e su finestra stretta il gettone scende
INTERO invece di spezzarsi.

### Lo stato del ramo, misurato

Build **Release** verde sui due TFM (`--no-incremental`, 0 avvisi) e suite verde su **sette progetti**:
1129 (Ui) + 2004 (Application) + 1191 (Infrastructure) + 130 (Domain) + 63 (AuroraProfiles) + 58 (Hosting)
+ 54 (Assets) = **4629**. 🟡 **Gli E2E non sono stati girati** in questo giro: vogliono l'host vivo, e vanno
fatti sul risultato della fusione — non sul ramo.

### Quel che resta

- 🟡 **Non provato a schermo su vLOA, APP e vSOP militare**: nel `vipi.db` di sviluppo nessuno dei tre ha
  segmenti tradotti, quindi l'avviso non compare — né prima né dopo. Il markup è pinnato dal test
  strutturale, ma una prova a schermo su quelle tre resta da fare quando ci sarà un documento tradotto.
- ✅ **In produzione dal 2 settembre 2026**, nel pacchetto **1.4.0** con §AO, §AP, §AQ, §AR e §AS.
- ℹ️ Toccava `vipi-theme.css` **e** `vipi-print.css`, cioè gli stessi due fogli di §AO: sono partiti insieme,
  con l'indice `staticwebassets`.

## AY. La scheda della clausola diventa una finestra, e tre classi scollegate — 3 settembre 2026

✅ **Fatto.** Nessun ramo dedicato. Carta:
[trasferimenti, la scheda della clausola diventa una finestra](feature/2026-09-03-trasferimenti-scheda-clausola-finestra.md).
Richiesta del committente su `/services/vsop/admin/transfers`: *«nella night mode ci sono delle barre bianche
in Edit row, poi nelle chip di aeroporto le X hanno sfondo grigio … la sezione edit row, com'è ora messa lì
è scomoda. Si potrebbe pensare a farla tipo pop-up?»*

### I tre difetti erano lo stesso difetto

Non erano tre problemi di stile: erano tre **collegamenti** rotti fra markup e foglio.

- Le barre bianche: le due falde-**coperchio** dello scroll shadow dipinte con `--on-brand`, che è `#fff` di
  brand e non ha un tema (definito una volta sola, mai ribaltato nei blocchi scuri). ⚠️ E si vedevano **anche
  senza niente da scorrere**, perché è proprio il coperchio a doverle nascondere agli estremi: del colore
  sbagliato non copre, si mostra.
- La ✕ dei chip: `.xt-chipx` era nel markup in **tre** punti e nel foglio in **zero**. Usciva il bottone di
  sistema — fondo grigio, bordo `outset`, 27×23px dentro una pastiglia alta 20.
- 🔴 Il più caro dei tre, e nessuno l'aveva visto: markup `xt-panel-f`, CSS `.xt-panel-foot`. Regola morta da
  tre settimane, quindi niente `display:flex` nel piede, quindi lo spaziatore non spingeva e **l'elimina
  stava appiccicato al duplica**. Un tasto distruttivo a 8px da uno costruttivo non sembrava un guasto:
  sembrava una scelta.

### La misura che ha ribaltato la proposta

Nella colonna da 380px il corpo aveva **348** per **896** di contenuto in 502 visibili. La prima proposta
diceva «la finestra è larga il doppio, quindi ci sta tutto». ⚠️ **Falso, e misurato**: a 828px il contenuto
scende a **860**, il 4%. I campi erano impilati in una colonna sola, e le righe a tre campi ci stavano già.

Quello che paga è la larghezza **spesa in colonne**: 896 → **666** a due colonne, e oltre i 920px non paga
più nulla (a 1040, 1160 e 1280 il contenuto non si muove). Da lì la scelta fra finestra e cassetto è
diventata aritmetica, e il committente ha chiesto se il cassetto si salvasse recuperando il cromo: no — i
~150px recuperabili vanno a tutte e due, ma il cassetto li deve **dividere in due** e la finestra no.

**Esito misurato**: come la scheda si apre davvero, 378 in 378 — **non scorre**. Forzando aperte tutte e tre
le sezioni, 666 in 636: **30px fuori invece di 394**.

### Quel che resta

- ✅ **In produzione dal 3 settembre 2026**, nel pacchetto **1.6.0** (è entrato nella fusione che l'ha
  preceduto). ℹ️ Toccava `vipi-theme.css` **e** `vipi-print.css`: sono partiti con l'indice
  `staticwebassets`.
- ✅ Il lock **è rimasto dov'era**, per decisione del committente — ma la fascia «lock in scadenza» che lo
  accompagnava, e che spingeva giù tutto di 76px mentre si scrive, **era un falso allarme**. La domanda del
  committente («ma il lock non si rinnova da solo?») ha aperto il difetto: `EditLockBar` pubblicava
  `ExpiresChanged` **solo quando cambiava il proprietario**, quindi la pagina teneva la scadenza della PRESA
  e dopo ~2 minuti credeva che il lock stesse scadendo. 🔴 Il conto dice che l'avviso non può scattare per
  davvero se non a battito rotto: TTL **3 minuti**, battito **60 s**, residuo sempre fra 180 e 120, soglia
  **60**. Ora la scadenza si pubblica a ogni rinnovo — come la `<summary>` del parametro già prometteva.
  Provato dal vivo: **3 minuti e mezzo, tre rinnovi** (01:05:59 → 06:59 → 07:59 → 08:59) e la fascia non è
  mai comparsa. ⚠️ Quando comparirà vorrà dire che **due battiti di fila sono falliti**, che è esattamente
  ciò che deve dire.
- 🟡 `StructureAccessibilityTests.Nessun_comando_raggiungibile_col_solo_mouse` è diventato rosso sul velo
  della finestra e **aveva ragione**: sta in whitelist con la ragione scritta, come quello di `DeleteDialog`.

## AZ. Documenti uniti: una pagina, un editor, una pubblicazione — 3 settembre 2026

Chiesto dal committente: unire il documento di un APP non remotizzato con quello dell'aeroporto (o col vSOP
militare), **indipendentemente dal tipo di documento**; e poter scegliere di unire vIPI e vSOP anche sui campi
con *presenza militare*. Precisato dopo: unire vuol dire **una pagina sola**, con l'**ordine** deciso dal
redattore, **un editor solo**, e la pubblicazione — fatta o **pianificata** — con **un clic**.

Carta `docs/feature/2026-09-03-documenti-uniti.md`, schema `docs/spec/modello-dati.md` §9.33.
Ramo **`documenti-uniti`**, da `main` (`cd1bc5c7`), spinto. §1-§9 chiuse.

### ⚠️ Il fatto misurato che ha deciso il modello

**LIBV Gioia del Colle ha DUE APP non remotizzati** — `LIBV_APP` e `LIBV_G_APP` — e così LIBN, LIPE, LIRM,
LIRS. **L'unione è un elenco ORDINATO, non una coppia**: due colonne su `Document` non reggevano un caso che
era già in archivio. Cinque minuti di query prima di disegnare, e il modello è cambiato.

### Che cosa c'è

`DocumentUnion` + `DocumentUnionMember` (indice **unico** su `DocumentId`), e **nessun tipo nuovo**: l'unione
è una *relazione*, ed è ciò che la rende indipendente dalla famiglia senza toccare i sei descrittori di
release, le sei rotte e i cinque provider di congelamento. Il legame è verso `Document.Id` e **non** verso
`TargetKey`, che è un puntatore e viene riscritto dalla rinomina di un callsign.

Lettura: un indice per membro impilato, i corpi in ordine sotto l'intestazione del loro documento, un solo
`PrintMeta`. La vista pubblica di un membro non-ospite **reindirizza** alla pagina unita.
Editor: il corpo delle tre famiglie è uscito dalle pagine in `Components/Doc/*SectionsEditor.razor`, e
l'ospite li monta con `Chrome="false"` dentro la sua griglia — il pattern della vIPI ACC.
Pubblicazione: **dentro** `PublishAsync`/`PublishNowAsync` — una transazione, catture **in sequenza**, un
solo `now`; annullamento accoppiato. 🔴 Le porte separate `PublishUnion*` sono esistite mezza giornata e
sono state tolte in supervisione: l'elenco di governo chiamava quelle normali e pubblicava **un** documento
mostrando «uniti: 2».

### 🔴 La supervisione del 3 settembre: quindici cose, tre serie

Rilettura da capo del lavoro §AZ, fingendo di non averlo scritto. **Tutte corrette**, in sei commit.
Dettaglio in [carta §6, §3, §4, §5b, §9c](feature/2026-09-03-documenti-uniti.md).

Le tre serie, che hanno tutte la stessa forma — **nessun errore, nessun rosso, e una cosa falsa a schermo**:

1. **L'elenco di governo diceva «uniti: 2» e ne pubblicava UNO.** Chiamava `PublishAsync`, la porta a
   bersaglio singolo, perché quella accoppiata era una **seconda** porta che solo `ReleasePanel` usava.
   🔴 La cura non era aggiornare il chiamante: **due porte per lo stesso gesto, di cui una sola sicura,
   sono un invito a chiamare quella sbagliata.** `CancelReleaseAsync` era già accoppiata dentro di sé e da
   quella pagina funzionava — l'asimmetria fra le due *era* il difetto. Le porte `PublishUnion*` non
   esistono più.
2. **Una lingua bloccata in un membro tingeva tutta la pagina.** `ReadingLanguageContext.Fissa` è appiccicoso
   per il resto della richiesta, e la sua stessa documentazione dice che regge *perché una pagina mostra un
   documento solo*. 🔴 **L'unione ha rotto quella premessa e nessuno è tornato a rileggere quella riga**:
   l'ultimo membro caricato con la lingua bloccata decideva la lingua di tutta la pagina, in base
   all'ordine di caricamento.
3. **Un membro pubblicato sotto un ospite non pubblicato spariva dal web.** Il rimando partiva senza chiedere
   se l'ospite avesse qualcosa da mostrare: due clic e un documento in vigore diventava irraggiungibile.

Le altre dodici: la vista di un'unione senza membri descritti faceva `Members[0]` su una lista vuota (circuito
giù su pagina pubblica), la domanda dell'annullamento sovrastimava, il giro dell'AIRAC entrante avrebbe
pubblicato la stessa unione una volta per membro, l'avviso «sezioni non salvate» non veniva chiesto ai
membri, la pastiglia del lock accusava anche sé stessi, e sei cose piccole (§9c).

⚠️ **Quel che la supervisione NON ha trovato** vale quanto il resto: le due migrazioni sono equivalenti,
l'annullamento è una cancellazione fisica (quindi nessuna release annullata può essere ripescata), i lock
scaduti sono normalizzati a monte, il rollback dei lock molla davvero quel che aveva preso, e i due `.resx`
hanno lo stesso insieme di chiavi. 🔴 E un sospetto è stato **ritirato dopo averlo verificato**:
l'autorizzazione per ACC non è un buco, è morta il 28 agosto — `EnsureAtLeast(Editor)` È il cancello.

### ⚠️ Le cose che non si deducono dal codice

- **L'ospite si riconosce da famiglia E chiave insieme**: un aeroporto e il suo vSOP militare hanno la
  **stessa** chiave di release (l'ICAO). Sulla sola chiave, la pagina civile disegnerebbe l'unione del militare.
- **`AppMil` è fuori dalle famiglie ammesse perché non ha un `IFrozenSectionProvider`**: un membro senza
  provider si pubblicherebbe senza congelare niente **e senza protestare**.
- **Il lock si prende su tutti in un gesto, o su nessuno**, e il rifiuto dice **chi** lo tiene.
- **Ricerca, «Novità» e impatti non sono stati toccati**, ed è una decisione: il redirect li copre tutti. Un
  rimando al posto di N chiamanti da tenere d'accordo.
- ⚠️ **Il commento di `MilDocRoutes` — «non è la stessa pagina con un parametro» — resta vero**, ed è stato
  aggiornato: l'unione non è un parametro, è un atto editoriale esplicito e reversibile.

### ⚠️ Quel che ha trovato la verifica dal vivo, e i test non vedevano

1. **La domanda prima di annullare mentiva**: diceva «il pubblico torna alla precedente» al singolare mentre
   ne toglieva due.
2. **Il pannello di release non rileggeva l'unione nata nella stessa pagina**: memoizza su `(bersaglio,
   chiave)`, e quelle non cambiano quando si unisce. ⚠️ *Quando un componente memoizza su una chiave,
   chiedersi che cosa può cambiare SENZA cambiare quella chiave.*
3. **L'indice unito restava con le sole voci dell'ospite**, per TRE cause in fila: le voci si *tiravano* con
   un `@ref` (assegnato dopo il render, mentre i membri si registrano durante); si spingevano *una volta
   sola*, quando il documento del membro non è ancora caricato; e la `.Concat` che le univa **non era mai
   stata applicata**. ⚠️ *Un `[Parameter]` dichiarato e mai letto non dà nessun segnale.*
4. ⚠️ `string.Format(L[chiave].Value, n)` **non interpola**: l'unico indexer che formatta è `L[chiave, n]`.
   In produzione l'argomento sarebbe sparito in silenzio.

### Quel che resta

- ✅ **In produzione dal 3 settembre 2026**, pacchetto **1.6.0** (25 file), e le **due migrazioni** — una per
  serie, SQLite e MySQL — sono **applicate sul database vero**. È la prima consegna con una migrazione da
  1.3.0. ⚠️ Non era una violazione della [finestra cieca al 16 settembre]: quella riguarda i **dati**, non
  lo schema, e il runbook diceva il falso su questo punto — corretto nello stesso giro.
- ✅ **Provato a schermo su tutti e due gli assi**: LIBA (aeroporto + APP) e **LIMN Cameri** (misto e
  **pubblicato**, che è la seconda richiesta). Su LIMN: release 57 e 58 allo stesso ciclo e alla stessa data
  efficace, **entrambi** i documenti promossi a `Published`, 34 voci d'indice con **zero** ancore senza
  bersaglio, il redirect dalla pagina civile alla vSOP unita, e l'anteprima a un ciclo in cui il membro non
  aveva pubblicato che ricade sulla pubblica. ⚠️ La migrazione è stata applicata su una **copia del
  `vipi.db` reale**, non su un database vuoto.
- 🟡 **La vIPI ACC e la vLOA restano fuori** dalle famiglie unibili, dichiarato: la prima è a blocchi e non
  passa da `DocumentSectionsView`, la seconda disegna da sé le direzioni dei coordinamenti.

## BA. Due file per due guasti, e gli ultimi sette servizi fuori dal circuito — 3 settembre 2026 (sera)

Nasce dalla **diagnostica di produzione** mandata dopo il caricamento di 1.6.1, non da una richiesta: due
cose da guardare, e tutt'e due si sono rivelate più grandi del sintomo.

### 1. `avvio-errore.txt` diceva «l'avvio è FALLITO» di uno spegnimento

🔴 **Era un falso allarme, e il foglio d'aggiornamento appena spedito diceva «se compare, fermatevi».**

Il fatto misurato: `avvii.txt` diceva `19:19:57 ARRESTO acceso per 01:50:57`, cioè il processo aveva
servito richieste per un'ora e cinquanta prima di morire — e lo stack era
`AtcPollingHostedService.StopAsync` → `Host.StopAsync` → `WaitForShutdownAsync`. Non era 1.6.1 che non
partiva: era 1.6.0 che finiva.

⚠️ **La causa è strutturale e non si vede leggendo `Program.cs`**: `app.Run()` **blocca fino allo
spegnimento**, quindi qualunque eccezione dell'arresto esce dal medesimo `catch`. Non c'era modo di
distinguerle, perché al momento della scrittura nessuno sapeva se l'host fosse mai partito.

La cura è una sentinella su `ApplicationStarted` e **due file distinti**:

| file | vuol dire | ferma il caricamento? |
|---|---|---|
| `diagnostica/avvio-errore.txt` | il sito **non è partito** | **sì** |
| `diagnostica/arresto-errore.txt` | il sito era partito ed è morto **chiudendo** | **no** |

ℹ️ Due file e non due intestazioni nello stesso file: il consiglio scritto su ogni foglio d'aggiornamento è
«`avvio-errore.txt` non deve esistere», e con un file solo quel consiglio resta falso per lo spegnimento più
comune di questo hosting. I due fogli vivi (`LEGGIMI-AGGIORNAMENTO.md`, `LEGGIMI-AGGIORNARE-VIA-FTP.md`)
dicono che il secondo non ferma niente.

⚠️ **La decisione è una funzione pura col parametro esplicito** (`FileDelGuasto(bool)`,
`Descrivi(ex, bool)`), non un test sull'host: dentro un host avviato il caso «avvio fallito» non è
riproducibile, perché la sentinella è già alzata.

### 2. Gli ultimi sette `@inject` che toccano il database

La guardia strutturale nata il mattino (`ScopeDellEditingTests`) tollerava sette casi noti, per non
allargare in una sera una correzione mirata. La diagnostica di produzione — **nove pagine d'errore vere**,
tutte `A second operation was started on this context instance`, una degenerata in
`MySqlProtocolException: Packet received out-of-order` — ha tolto la ragione della tolleranza.

| file | servizi spostati |
|---|---|
| `Components/Doc/AirportSectionsEditor.razor` | `IAirportEditingService`, `IAirportSectorService`, `IMilitaryDocumentService` |
| `Pages/NewDocumentPage.razor` | `IMilitaryDocumentService` |
| `Pages/VersioniPage.razor` | `IReleaseService`, `IDocumentAdminService`, `IDocumentUnionService` |

⚠️ **`VersioniPage` era il caso da guardare, non il più semplice.** Accanto c'è una scelta documentata di
segno **opposto**: `ReleasePanel` prende `IReleaseService` dal circuito **apposta**, perché il publish è
un'operazione sola composta col `BeforePublishAsync` della pagina ospite, e spezzarla su due contesti la
manda in stallo. Quella ragione qui non vale — `VersioniPage` **non monta `ReleasePanel`**. Verificato col
`grep`, non dedotto: è la differenza fra applicare una regola e capirla.

L'elenco dei tollerati **resta, vuoto**, col perché dentro e un `Assert.Empty` accanto: con zero voci
l'asserzione «sono ancora tutti sul circuito» è vera per costruzione, cioè vacua.

### Provato a schermo, con la prova nel database

Ogni servizio spostato è stato **fatto scrivere** guidando l'applicazione in un browser su una copia del
`vipi.db` reale, e la scrittura è stata verificata in archivio — non a schermo.

| servizio | gesto | prova in archivio |
|---|---|---|
| `IReleaseService` | «Publish at cycle» su LIBD | `DocReleases` #57, `LIBD 2610 Scheduled` |
| `IAirportEditingService` | «+ Rule» + «Save rules» | `AirportRunwayRules` #7, `DepRunways='07'` |
| `IAirportSectorService` | nascondi `LIBD_ATIS` | `AirportSectors` #7, `IsHidden=1` |
| `IMilitaryDocumentService` | nuovo documento su **LIBV**, campo solo militare senza vSOP | `Documents` #33, `vSOP MIL — LIBV`, `Edition=Military` |

`IDocumentAdminService` e `IDocumentUnionService` sono stati esercitati in **lettura** (l'elenco di dieci
documenti e il riquadro «Joined documents»). Barra d'errore mai comparsa, nessun «A second operation».

⚠️ **Quel che NON è stato provato, e va detto**: `IMilitaryDocumentService` **dentro
`AirportSectionsEditor`** — il tasto «crea il vSOP militare» compare solo su un campo misto che non ce
l'ha ancora, e nel database di sviluppo non ce n'è. È lo stesso metodo dello stesso servizio già provato
da `NewDocumentPage`, con la stessa riga di risoluzione.

### ⚠️ Due trappole dello strumento, non del prodotto

- **Il tasto «✕ Cancel» delle release apre una conferma in linea**, non un `confirm()` del browser: un solo
  clic sembra non fare niente e la release resta. Il testo che spiega la conseguenza sta nella pagina
  **apposta** (il nativo bloccava tutto). Chi guida la pagina da fuori deve prevedere **due** gesti.
- **Un `click()` via JavaScript su un elemento appena scrollato non equivale al clic**, e la pagina si
  sposta fra la misura delle coordinate e il clic. Le coordinate si rileggono **dopo** l'ultimo movimento,
  da uno screenshot fresco — la regola era già scritta in `.claude/skills/verifica-live` §4-bis, ed è
  costata tre gesti a vuoto per averla applicata a metà.

## BB. La testata si compatta: la chip nel sottotitolo, la lingua a gettone — 3 settembre 2026

✅ **FUSA IN MAIN** il 3 settembre 2026 (`e0f27a2b`, spinta; il ramo `testata-compatta` è stato cancellato).
🔴 Cambia due fogli di stile → impronte nuove → serve un **pacchetto** prima che si veda in produzione.
Nessuna carta: richiesta del committente, partita dal vSOP
militare — *«nelle vsop militari non abbiamo compattato questa sezione come negli altri documenti … Voglio
che appaia: Military Airport · &lt;ACC&gt; e i tasti everyone, pilot, atc; ONLY FOR SIMULATION … e accanto
l'informazione sulla lingua»*. Estesa a **tutte e cinque** le famiglie su sua scelta esplicita.

### Il fatto

Sopra il documento stavano fino a **tre** cartelli: il titolo, un blocco di tre bottoni («Everything ·
Pilot · ATC», `AudienceChip`) e un `callout` pieno a tutta larghezza — titolo più due righe di prosa — per
dire che il documento è pubblicato in **una lingua sola**. È lo stesso problema chiuso il 2 settembre per
l'avviso di traduzione (§AT), rimasto aperto per gli altri due elementi della stessa riga.

⚠️ E il vSOP militare era il caso peggiore: **l'unica delle cinque testate senza sottotitolo**, quindi
titolo nudo, blocco di bottoni, riquadro.

### La forma nuova, uguale sulle cinque famiglie

- **`.sub-line`** — sottotitolo e chip di lettura sulla stessa riga (`AudienceChip Compatto="true"`, classe
  `.aud-chip.inline`). Sul telefono la riga va a capo **intera**: i tre link restano insieme.
- **`.sim-line`** — l'avviso di simulazione, il gettone «tradotta a macchina» e il nuovo gettone della
  lingua (`LinguaBloccataNotice Compatto="true"`, classe `.lang-chip`), col testo per esteso dietro il «?».
- Il vSOP militare ha finalmente il suo sottotitolo: **«Aeroporto militare · &lt;nome ACC&gt;»**, gemello di
  «Aeroporto · &lt;nome ACC&gt;» della vIPI civile dello stesso scalo. La pagina ora risolve l'ACC per il
  **nome** (`IStationResolver`), non solo per il codice della rotta. `Mil_PrintSubtitle` è morta con questo:
  a schermo e sul foglio il sottotitolo è **uno**.

⚠️ **Il gettone della lingua è BLU**, non giallo e non rosso: in quella riga il rosso è dell'avviso di
simulazione e il giallo del «tradotta a macchina». E questa non è un'avvertenza — è un'informazione, il
documento è in una lingua sola perché **così si è voluto**. Un terzo colore d'allarme avrebbe spento gli
altri due. Contrasto misurato: **5,1:1** chiaro, **7,5:1** scuro.

### ⚠️ La trappola di sempre, terza volta: `.doc-head` sul foglio NON esiste

`vipi-print.css` nasconde `.print-meta + .doc-head`. Tutto quel che si sposta in testata **sparisce dalla
stampa**, ed è già costato l'avviso di simulazione (§ 1 settembre) e il gettone di traduzione (§AT). Quindi
`PrintMeta` ha ora una **terza** riga sua, `.pm-lang`, col testo per **esteso** — davanti a un foglio non
c'è nessun «?» da aprire. Il patto è doppio e lo pretende `TestataCompattaSuOgniSedeTests`: **chi mostra il
gettone passa la stessa lingua a `PrintMeta`**.

⚠️ La chip pilota/ATC invece **non** ha bisogno del gemello di stampa: è un comando, e in stampa era già
nascosta apposta. Ciò che resta sul foglio è il **badge** sulle singole sezioni, che è contenuto.

⚠️ `PrintMeta` chiede ora anche `StringheDelSito`: la riga della lingua segue **chi guarda**, non il
documento — è l'unica di quell'intestazione che lo fa, e la ragione è che è l'unica scritta per spiegare
perché il resto del foglio non è nella lingua di chi legge.

### 🔴 Quel che si è visto solo guidando la pagina

**L'anteprima di BOZZA di una vIPI ACC bloccata non era bloccata.** `AccVipiPage` leggeva `Language` e
`LanguageLocked` **solo** nel ramo della release: nel ramo bozza il modello li porta
(`AccDocumentModel.Language/LanguageLocked`) e la pagina li buttava via. Conseguenze: nessun avviso, e la
prosa **derivata** — frasi di coordinamento, etichette AoR, intestazioni — calcolata nella lingua di chi
guarda invece che in quella del documento. Difetto **precedente** a questo lavoro, trovato perché il
gettone mancava dove doveva esserci; una riga, nello stesso giro.

### Verifica

`dotnet build Vipi.slnx -c Release --no-incremental` verde (0 warning), suite intera verde (net8 + net10),
**30 test nuovi** (`LinguaBloccataNoticeTests`, `TestataCompattaSuOgniSedeTests`).

A schermo, su copia del `vipi.db` con quattro documenti bloccati a mano e sezioni marcate pilota/ATC: le
**cinque** famiglie (vSOP militare LIBG, vIPI ACC Brindisi, aeroporto LIPA, vLOA LIBB↔LGGG, APP standalone
LIBA_APP), in pubblico e in bozza, col filtro `?vista=` attivo, in tema chiaro e scuro, a 1600px e a 390px,
più il PDF — che porta la riga `Single-language document — …` per esteso. Zero errori di console, zero
riquadri pieni rimasti.

## BC. I parcheggi sono un dato dello scalo, non una procedura — 3 settembre 2026

✅ **FUSA IN MAIN** il 3 settembre 2026 (`9594ac2f`, spinta; il ramo `parcheggi-dati-generali` è stato
cancellato). Nessuna carta: richiesta del committente — *«spostare
parking presente in ground procedure in general data alla fine della sezione, però considera che ci sono vSOP
in produzione che già usano parking e vanno gestite»*.

### Il fatto

Nel profilo `AirportMil` la sezione `parkings` era la **prima figlia** di «Procedure di terra». Un piazzale e
i suoi stalli sono un **dato del campo** — come piste, radioassistenze e frequenze — non una procedura che si
esegue: ora è l'**ultima figlia** di «Dati generali» (ordine 7, dopo «Nominativi»).

### ⚠️ Perché il solo catalogo non bastava, ed è la parte che conta

Il catalogo decide la struttura **solo alla nascita** (`DocumentBirth.Semina`). Sui documenti già scritti non
cambia niente — e **nessuno potrebbe rimediare a mano**: il motore di riordino sposta soltanto fra
**fratelli** (`MoveSectionBeforeAsync` pretende un fratello, apposta, perché un riordino non diventi una
riparentazione silenziosa). In UI il gesto «portala in un altro gruppo» non esiste. Senza un passo di
riconciliazione i cinque vSOP militari già scritti avrebbero tenuto i parcheggi fra le procedure **per
sempre**, e la pill dello scostamento avrebbe pure cominciato a segnalarli come fuori posto.

Quindi: `IDocumentMaintenance.ReparentMilParkingsAsync`, one-shot idempotente in `ReconcileVipiDocuments`
come le altre (mai una migrazione EF: lo schema hostato lo crea il `PostgresSchemaReconciler`).

- **Prudente**: solo documenti militari, solo la chiave `parkings`, e **solo se il padre è ancora
  `groundprocedures`**. Chi l'avesse già portata altrove ha fatto una scelta, e non si tocca — è anche ciò
  che rende il passo idempotente.
- **Il contenuto viaggia con la sezione**: è la stessa riga, quindi blocchi, tabella `milparkings`,
  «nascosta» e marcatura pilota/ATC restano attaccati. Il corpo si cerca per **chiave**, ricorsivamente
  (`MilMemberLoader.Cerca`), non per posizione.
- Il gruppo che l'ha persa si **richiude**: `Order` è una posizione fra fratelli, e lasciare il buco farebbe
  partire le Procedure di terra dal numero due.

⚠️ **Le release già pubblicate non si toccano** (doc 13 §9): il pubblico continua a vedere i parcheggi
dov'erano finché quel vSOP non viene **ripubblicato**. Misurato a schermo — vedi sotto — ed è una decisione da
prendere, non un difetto.

### 🔴 Due buchi dello stesso passo, chiusi insieme

`AddMissingCatalogSectionsAsync` — quello che porta ai documenti già scritti le sezioni aggiunte al catalogo
dopo la loro nascita — aveva **due limiti mai dichiarati**:

1. **non guardava i vSOP militari**: l'elenco dei documenti da controllare era vLOA + APP standalone +
   aeroporti. Nessuna chiave aggiunta al profilo militare è mai arrivata a un documento esistente;
2. **si fermava al primo livello**: confrontava le sole radici. Cioè proprio dove il profilo militare non ha
   quasi niente, avendo **ventisei sezioni dentro sei contenitori** — su un documento a cui mancava mezzo
   indice avrebbe risposto «non manca niente».

Ora l'elenco comprende i militari e il confronto **ricorre nei sotto-gruppi**, inserendo ogni mancante nella
posizione che il catalogo le dà fra i suoi fratelli.

⚠️ **E la presenza si misura sulla CHIAVE in tutta la versione, non dentro il singolo gruppo.** Un documento
non ancora riparentato ha i parcheggi sotto le Procedure di terra: un confronto fatto gruppo per gruppo ne
avrebbe creato un **secondo** dentro i Dati generali — due sezioni con la stessa chiave, e il corpo che ne
pesca una a caso. Una sezione nel posto sbagliato va **spostata**, non duplicata in quello giusto. L'invariante
su cui poggia questa lettura — *dentro un profilo ogni chiave compare una volta sola* — non era scritta da
nessuna parte: ora è un test su tutti e sette i profili.

### Verifica

Build Release 0 warning, suite intera verde, **14 test nuovi** (`ParcheggiNeiDatiGeneraliTests`, più le
guardie di catalogo in `ProfiloMilitareTests` e `SectionCatalogTests`).

A schermo, su una copia fresca del `vipi.db` con i suoi **cinque** vSOP militari nella forma vecchia: al primo
avvio il log dice *«Spostata la sezione «Parcheggi» sotto «Dati generali» in 5 vSOP militari»* e nessuna
sezione mancante (quei documenti sono nati col profilo completo). Nel database la sezione sta a `Order` 7,
`Depth` 1, sotto `generaldata`, e le Procedure di terra ripartono da uno.

Nel documento reso — indice e corpo, `/services/vsop/libb/mil?icao=LIBG` — la **bozza** e l'**editor**
mostrano «Parking» dopo «Callsigns» e prima del contenitore «Ground procedures», senza nessuna pill di
scostamento; la vista **pubblica** la mostra ancora dentro «Ground procedures», perché legge lo snapshot della
release. È esattamente il patto scritto sopra.

### Da decidere

I vSOP militari già **pubblicati** (nel database di sviluppo: LIBG, LIML, LIMN) mostreranno la posizione nuova
solo dopo una **ripubblicazione**.

## BD. Le carte dello scalo entrano nel catalogo — 3 settembre 2026

✅ **FUSA IN MAIN** il 3 settembre 2026 (`90156232`, spinta; il ramo `carte-aeroportuali` era impilato su
quello di §BC e sono stati cancellati tutt'e due — `main` li ha presi in fast-forward, storia lineare).
Nessuna carta: richiesta del committente — *«aggiungere la sezione Airport charts prima di validity & revision nelle
vSOP e nelle vIPI. Al suo interno deve contenere Aerodrome, Instrumental approach charts, SID, STAR, VFR»*.

### Che cosa nasce

Un contenitore, **«Carte aeroportuali» / «Airport charts»**, con cinque raccolte — Aerodromo, Carte di
avvicinamento strumentale, SID, STAR, VFR — **appena prima** di «Validità e revisione», che resta l'ultima:
è il timbro che chiude il documento, non una sezione fra le altre.

Su **due** profili: la vIPI d'aeroporto e il vSOP militare, cioè i due documenti che descrivono uno **scalo**.
⚠️ L'elenco è scritto **una volta sola** (`CarteAeroportuali(ordine)`): due copie sarebbero due elenchi
diversi al primo ritocco — è il difetto che `AppMil` evita rimandando al profilo civile invece di ricopiarlo.

Sezioni **editoriali**: il contenuto sono immagini e allegati PDF, che i blocchi sanno già portare. Non c'è
niente da derivare — le carte non stanno in nessun catalogo nostro.

⚠️ **Le chiavi sono `charts:*`, non `sids` e `vfr`.** Quelle due hanno già un mestiere — le SID **importate**
della vIPI d'aeroporto (corpo reso dalla pagina) e la sezione VFR di un profilo di posizione — e dentro un
profilo una chiave compare **una volta sola** (l'invariante scritta ieri in §BC). Riusare il nome avrebbe
messo la tabella delle SID importate dentro una raccolta di carte.

⚠️ **SID, STAR e VFR sono SIGLE**: uguali nelle due lingue, come AOR e MRVA. Stanno in `CatalogoBilingueTests.Sigle`
e in `TitoliUfficiali`, che semina la memoria di traduzione con la **nostra** parola — senza, la macchina
tradurrebbe «STAR» da sé. Nella stessa tabella entrano «Carte aeroportuali → Airport Charts», «Aerodromo →
Aerodrome» e «Carte di avvicinamento strumentale → Instrument Approach Charts».

### La prova del modulo di ieri

È anche il collaudo di `AddMissingCatalogSectionsAsync` esteso in §BC. Su una copia fresca del `vipi.db`, al
primo avvio: *«Aggiunte **84** sezioni di catalogo mancanti»* — **14 documenti** (9 vIPI d'aeroporto + 5 vSOP
militari) × 6 sezioni, e **nient'altro**: quei documenti non avevano altri buchi. Le raccolte arrivano
**dentro** il contenitore appena creato, con l'ordine che riparte da uno, e il contenitore si infila **prima**
di «Validità e revisione», non in coda al documento.

⚠️ E i titoli nascono **nella lingua del documento**: il vSOP di LIBG è redatto in inglese, e infatti le sue
sezioni sono nate «Airport charts / Aerodrome / Instrument approach charts»; sulla vIPI di Aviano, italiana,
«Carte aeroportuali / Aerodromo / Carte di avvicinamento strumentale».

### ⚠️ Quel che è cambiato per chi legge i test

Il profilo dell'**aeroporto** ora ha un descrittore **con figli**: erano annidati solo i militari. Tre test
confrontavano l'elenco **piatto** delle sezioni di un documento con le **radici** del catalogo, e con
`Order` che è una posizione fra **fratelli** quell'elenco mescolava padri e figlie (`weather`,
`charts:aerodrome`, `runwayrules`, `charts:iac`, …). Ora confrontano le radici e chiedono a parte che le
raccolte stiano dentro il loro contenitore. ⚠️ I conteggi scritti a mano — «ventisei sezioni» — sono
diventati conteggi **sul catalogo**: un numero scritto a mano invecchia, ed era già successo.

### Verifica

Build Release 0 warning, suite intera verde (net8 + net10), test nuovi sul profilo e sulla riconciliazione.

A schermo, su copia fresca: **vIPI d'aeroporto (LIPA)** e **vSOP militare (LIBG)**, in bozza e in editor —
«Airport charts» con le cinque raccolte, subito prima di «Validity and revision», che resta l'ultima. In
modifica ogni raccolta offre quel che serve a riempirla: **+ Blocco**, sotto-sezioni, «nascondi», destinatari
pilota/ATC e le frecce di riordino. Zero errori di console.

## BE. Sezioni e sotto-sezioni che si muovono davvero — 4 settembre 2026

✅ **FATTA**, ramo `sezioni-mobili` (8 fette, 8 commit). Carta: `docs/feature/2026-09-04-sezioni-mobili.md`.
Richiesta del committente: *«le sezioni e le sottosezioni devono potersi muovere dentro la sezione di
appartenenza o nel documento senza problemi; le singole sottosezioni devono potersi nascondere; le
sottosezioni custom devono poter stare anche sopra il contenuto principale (tipo sopra la mappa in AOR); e
sarebbe ottimo poter spostare le sottosezioni in sezioni diverse. Su tutti e cinque gli editor.»*

### ⚠️ Tre richieste su quattro avevano già il tasto — e due erano vere a metà

Le frecce ↑/↓ ci sono da sempre anche sulle **sotto-sezioni**, l'occhio «nascondi» è nell'header condiviso a
**ogni** profondità, e il comando «⤒ sopra il corpo» (`BeforeParentBody`, doc 11 §3g) sta su ogni
sotto-sezione. Il lavoro nuovo è **uno**: spostare una sotto-sezione in un'**altra** sezione. Ma leggendo il
codice per dirlo sono usciti due difetti, e sono quelli che facevano sembrare rotto ciò che c'era:

- 🔴 **`SectionNode` ignora «sopra il corpo» quando il corpo lo rende la pagina.** Rende la scheda derivata e
  poi le sotto-sezioni con `Slot=All`: una figlia marcata «sopra» esce **sotto**. Nel vSOP militare
  `frequenze`, `piste` e `quote di transizione` sono figlie di «Dati generali» e sono rese dalla pagina —
  chi ci scrive una nota sopra la vede sopra nell'**editor** e sotto nel **pubblicato**. È la trappola del
  doc 11 §8, terza volta: `DocumentSectionsView` e `AccSectionBody` i tre slot li fanno giusti, il terzo
  lettore no.
- **La vIPI ACC non passa mai `IsDraft`** alle sotto-sezioni: una sotto-sezione nascosta spariva **anche
  dall'anteprima di bozza**, invece di comparire con la pill «nascosta».

### Le tre decisioni del committente

1. **Solo le sezioni libere si riparentano** — una sezione di catalogo ha una posizione standard, ed è quella
   che la pill dello scostamento conta.
2. **Nella vIPI ACC si resta dentro il blocco**: il blocco *è* il gruppo.
3. **Due gesti**: il menu «Sposta in…» (preciso, tastiera e tocco) **e** il trascinamento nel menu-sezioni.

### Stato per fetta

| Fetta | Che cosa | Stato |
|---|---|---|
| S0 | Carta + questa voce | ✅ |
| S1 | `SectionNode` a tre slot | ✅ |
| S2 | `IsDraft` scende in ACC | ✅ |
| S3 | Motore `MoveSectionToParentAsync` + cinque guardie | ✅ |
| S4 | `SectionMoveTargets` + menu «Sposta in…» | ✅ |
| S5 | Figlie trascinabili nel menu-sezioni | ✅ |
| S6 | Spareggio stabile nell'ordinamento | ✅ |
| S7 | Propagazione e verifica live sulle cinque famiglie | ✅ |

### Che cosa c'è adesso

- **«⇵ Sposta in…»** nell'intestazione di ogni sezione **libera**, a ogni profondità, su tutti e cinque gli
  editor: l'elenco delle destinazioni lo calcola una funzione pura (`SectionMoveTargets`) che esclude sé
  stessa, il proprio sottoalbero, il padre attuale e ogni posto senza profondità residua.
- **Le figlie si trascinano** nel menu-sezioni, e lasciarne una su un altro gruppo la **riparenta**. Le regole
  del gesto stanno in `TocDropRules`, funzione pura: si provano senza fabbricare eventi di trascinamento —
  che è ciò che nell'agosto 2026 tenne verdi otto test su un gesto rotto.
- **Il motore** è `MoveSectionToParentAsync`, con cinque guardie (bozza, sezione libera, stessa versione,
  niente cicli, profondità del **sottoalbero**), `Depth` riscritta su tutto il sottoalbero e **due** gruppi
  rinumerati. ⚠️ Le guardie stanno lì e non nella UI: l'elenco che disegna il menu può essere vecchio.
- **La freccia ↑↓ non scambia più i due `Order`: reinserisce e rinumera.** Su due fratelli con lo stesso
  numero — e nessun indice unico lo vieta — lo scambio non cambiava niente: era un tasto che non faceva nulla.

### Le tre decisioni, e perché

1. **Solo le sezioni libere cambiano gruppo**: una di catalogo ha un posto standard, ed è quello che conta la
   pill dello scostamento.
2. **Nella vIPI ACC si resta dentro il blocco**: il blocco *è* il gruppo. «Primo livello», nel menu, è il
   **blocco** e non la radice del documento — là una sezione diventerebbe un blocco.
3. **Due gesti**: il menu (preciso, da tastiera e da tocco) e il trascinamento (veloce).

### Verifica live (copia del DB, Edge headful)

Le sei prove sono nella carta. Le due che valgono di più: una figlia **trascinata** su un gruppo diverso
finisce sotto il nuovo padre e **prima** del bersaglio; e la stessa sezione, marcata «sopra il corpo» dentro
«ATC/CRC frequencies», esce **sopra** la tabella nel documento — e rimessa «dopo» torna sotto. Nuovo script
durevole: `.claude/skills/verifica-live/sposta-verifica.js`.

⚠️ **Niente migrazione**: nessuna colonna nuova, quindi è spedibile dentro la finestra cieca. ⚠️ E i documenti
**già pubblicati** non cambiano forma finché non si **ripubblicano**: le release non si toccano mai.

## BF. «Da un altro documento…» non era rotto: taceva — 4 settembre 2026

✅ **FATTA**, in `main`. Segnalazione del committente: *«il tasto move to another document non funziona»*.

### Che cosa succedeva davvero

Il tasto è la **tendina delle sorgenti** del pannello di import (`ImportaTabella`), quella che prende la
stessa tabella da un altro vSOP militare: Nominativi, Parcheggi, Aeroporti alternati.

Guidando l'app su una copia del DB: la tendina c'è, elenca i quattro altri campi, e scegliendone uno **non
succede niente**. Nessun errore, nessuna riga, nessun messaggio. Il motivo, misurato sul `vipi.db`: di quelle
tre tabelle **nessun vSOP ha righe** — cinque campi, tre tabelle, zero payload. Il documento si legge
benissimo, semplicemente non ha niente da dare.

⚠️ **Il difetto non è il caricamento, è il silenzio.** Un comando che non fa niente e non lo dice si legge
come rotto — ed è il caso NORMALE finché i vSOP sono da riempire, quindi si leggeva come rotto sempre. È la
stessa lezione di «Aggiungi riga non aggiungeva niente» (30 agosto): nessun errore da nessuna parte.

### Le tre correzioni

1. **Lo dice**: pastiglia neutra «Quel documento non ha righe in questa tabella» / *«That document has no
   rows in this table»*. Neutra e non rossa: non è un guasto, è una risposta.
2. **La tendina torna al segnaposto** dopo ogni scelta: è un **comando**, non uno stato. Lasciata sul
   documento scelto, sceglierlo una seconda volta non emette nessun `change` — e chi ha appena visto un
   comando non produrre niente prova esattamente quello, e lo trova morto una seconda volta.
3. ⚠️ **La pastiglia della forma diceva il falso**: su una tabella presa da un altro documento — celle già
   separate — dichiarava `RigaIntera`, che vuol dire l'opposto («una cella sola per riga, spezzala tu»), e a
   schermo si leggeva «righe intere». Ora c'è `FormaGriglia.AltroDocumento` e la pastiglia dice «da un altro
   documento». La forma serve a **dirlo** a chi importa: dirlo sbagliato è peggio che non dirlo.

### Verifica

Cinque test bUnit (`ImportDaAltroDocumentoTests`), provati per mutazione: rimesso il componente di prima,
tre diventano rossi. Guidato a schermo su copia del DB (LIBG, sezione Nominativi): con una sorgente
**seminata** l'anteprima mostra le tre righe e la pastiglia dice «from another document»; con una sorgente
**vuota** compare «That document has no rows in this table». Zero errori di console. Build Release verde,
suite UI 1246 e Application 2145 verdi.

### ⚠️ Seguito di §BE (stessa sera): «Sposta in…» c'era e non si poteva premere

Il committente: *«o non va, oppure il fatto che su questo schermo non si vede tutto il tasto dà problemi»*.
Misurato: a **1280** la riga dei comandi sforava di 68px (197 su una sotto-sezione), il tasto **usciva dalla
card**, e a menu aperto finivano fuori **tutte** le 32-33 destinazioni; a 1500 una sotto-sezione ne perdeva
27 su 33, tagliate da `.coord-sub{overflow:hidden}`.

🔴 **Il menu in linea di «+ Blocco» funziona LÌ perché sta nel corpo della sezione, che va a capo.** La riga
dei comandi è `nowrap`: un commento che spiega perché una scelta regge in un posto non è il permesso di
rifarla altrove. Ora è una **tendina** di sistema — trenta voci, e nessun `overflow` ritaglia il pannello di
una `<select>` — con `value=""` riazzerato dopo la mossa (è un comando, non uno stato: la stessa lezione di
§BF). E la riga dei comandi va a capo **solo in modifica**, dove i comandi sono otto; in lettura resta
`nowrap`, che è dove valeva la ragione originale (una sezione chiusa alta 92px invece di 50).

Dopo: sforo 0 a 1280 e a 1500, tendina dentro la card, tutte le destinazioni raggiungibili, mossa vera
provata a schermo. Suite UI 1248 verde, build Release verde.

