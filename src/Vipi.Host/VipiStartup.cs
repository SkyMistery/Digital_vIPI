using System.IO.Compression;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Vipi.Host;
using Vipi.Host.Auth;
using Vipi.Host.Components;
using Vipi.Hosting;

namespace Vipi.Host;

/// <summary>
/// Il corpo dell'avvio, staccato da <c>Program.cs</c> per una ragione sola: <b>renderlo diagnosticabile</b>.
///
/// <para><b>Il problema che risolve.</b> Con le istruzioni di primo livello tutto <c>Program.cs</c> era un
/// <b>solo metodo</b> (<c>&lt;Main&gt;$</c>). Il runtime risolve i tipi di un metodo <b>prima</b> di eseguirne
/// la prima riga — e la prima riga era proprio <see cref="StartupDiagnostics.HookFatalErrors"/>. Un
/// <c>TypeLoadException</c> o <c>MissingMethodException</c> su un qualunque tipo citato lì dentro (una
/// libreria nostra rimasta indietro di un pacchetto, per dire) uccideva il processo <b>prima che il gancio
/// esistesse</b>: nessun <c>avvio-errore.txt</c>, nessuna riga da nessuna parte. Su un host senza accesso ai
/// log — vedi <see cref="StartupDiagnostics"/> — significa un guasto invisibile da entrambe le parti.</para>
///
/// <para>Spostando il corpo qui, <c>Main</c> non cita più nessuno di quei tipi: installa il gancio, e solo
/// dopo entra in un metodo la cui preparazione può fallire — dentro un <c>try</c>. Il fallimento diventa
/// un'eccezione gestita, e lascia scritto il proprio nome.</para>
///
/// <para>⚠️ <b>Non spostare codice da qui a <c>Program.cs</c>.</b> Ogni tipo citato là torna a essere risolto
/// prima del gancio, e riapre esattamente il buco che questa separazione chiude.</para>
///
/// <para>Accaduto davvero: la notte del 23→24 agosto 2026 il sito è rimasto giù senza lasciare una riga.</para>
/// </summary>
internal static class VipiStartup
{
    /// <summary>
    /// Costruisce l'applicazione e la manda in esecuzione. Non ritorna finché il processo non si spegne.
    /// </summary>
    /// <remarks>
    /// <c>NoInlining</c> è il punto della classe, non un'ottimizzazione: se il JIT ricopiasse questo corpo
    /// dentro <c>&lt;Main&gt;$</c>, i tipi qui citati tornerebbero a essere risolti alla preparazione di
    /// <c>Main</c> — cioè prima del gancio agli errori fatali. Un metodo di queste dimensioni non verrebbe
    /// mai inlinato comunque: l'attributo scrive l'invariante invece di affidarla a una soglia del JIT.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run(string[] args)
    {
        // Il cronometro delle fasi. Costa uno Stopwatch e sei righe in coda a diagnostica/avvio-diagnostica.txt,
        // e risponde alla sola domanda che su questo host non aveva risposta: «ci mette tanto a ripartire —
        // tanto DOVE?». Vedi StartupDiagnostics.CronometroAvvio.
        var crono = new StartupDiagnostics.CronometroAvvio();

        var builder = WebApplication.CreateBuilder(args);
        crono.Segna("CreateBuilder");

        // I segreti da FUORI del file che si scarica. Prima di tutto il resto, perché AddVipiStandaloneAuth
        // legge la sezione VipiAuth alla registrazione e deve vedere già i valori buoni.
        var segreti = SegretiFuoriDalWeb.Carica(builder.Configuration);

        // Riepilogo della configurazione vista, riscritto a ogni avvio — anche riuscito. Sta qui, subito dopo il
        // builder, perché serva anche quando l'avvio muore più avanti: dice con QUALE configurazione ci ha provato.
        StartupDiagnostics.WriteConfigurationSummary(builder, segreti);

        // ⚠️ RISCRITTO non vuol dire REGISTRATO: la riga qui sopra sovrascrive il suo file a ogni avvio, quindi
        // dice quando è ripartito l'ULTIMO processo e mai quanti ce ne sono stati. Questa invece scrive in coda,
        // una riga per avvio e una per arresto (l'aggancio allo spegnimento è più sotto, dopo builder.Build).
        // È la misura che serve per sapere se «Attempting to reconnect…» sul browser è Passenger che spegne per
        // inattività — fisiologico qui — o qualcosa che si rompe. Vedi RegistroAvvii.
        // ⚠️ L'etichetta corta («1.1.0 · ff88bbd»), non il dettaglio: quello finisce con «in servizio dal
        // <data>», e in un file dove ogni riga comincia già con il proprio orario sarebbe la stessa ora
        // scritta due volte. Visto nel registro del pacchetto vero, non nei test.
        RegistroAvvii.RegistraAvvio(VersioneBuild.Leggi().Etichetta);

        // La password c'è davvero? Se manca, l'applicazione ripiegherebbe su un file SQLite vuoto e il sito
        // ripartirebbe con l'aria di aver perso i dati: il modo peggiore di sbagliare. Meglio non partire.
        SegretiFuoriDalWeb.EnsureConnessioneUsabile(
            builder.Configuration["Persistence:Provider"],
            builder.Configuration.GetConnectionString("Vipi"));

        // File (default globale) della frase di coordinamento. reloadOnChange:false — il FileSystemWatcher esaurirebbe
        // le istanze inotify su host con limite basso (es. Render); in container il file è comunque immutabile (baked nell'immagine).
        builder.Configuration.AddJsonFile("content/coordination-sentence.json", optional: true, reloadOnChange: false);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(o =>
            {
                // Tetti espliciti invece dei default (100 circuiti trattenuti per 3 minuti). La scala decisa è UNA
                // istanza (audit 22 luglio, voce A2, e il vincolo è scritto in nginx-vipi.conf): il limite di memoria
                // di quel processo è quindi l'unica cosa che regge il sito, e un circuito disconnesso porta con sé
                // tutto lo stato della pagina editor che aveva aperta.
                // ⚠️ Numeri stimati sul traffico atteso (decine di persone, non migliaia): da rivedere dopo il primo
                // ciclo AIRAC pubblicato dal server nuovo, quando ci sarà una misura al posto di una stima.
                o.DisconnectedCircuitMaxRetained = 25;

                // ⚠️ Da 2 a 5 minuti il 31 agosto 2026, e la ragione è precisa: QUESTA finestra è l'unica cosa
                // che distingue «mi si è staccato un attimo e ritrovo la pagina com'era» da «ricarico e riparto
                // dall'inizio». Il circuito trattenuto conserva lo stato della pagina — la bozza dell'editor
                // compresa — e il browser, con i tempi di riconnessione scritti in App.razor, continua a
                // ritentare per circa quattro minuti: una retention di due li rendeva inutili, perché a metà dei
                // tentativi non c'era più niente da riagganciare.
                //
                // ⚠️ Non serve a niente quando a morire è il PROCESSO (Passenger che spegne per inattività): lì
                // i circuiti trattenuti muoiono con lui, e l'unica via d'uscita è ricaricare la pagina — cosa
                // che il gestore di riconnessione in App.razor fa da solo. Vale per i buchi di rete, che sono
                // l'altra metà dei casi.
                o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);

                // Scritto, non ereditato: con true le eccezioni non gestite arrivano al browser con lo stack.
                o.DetailedErrors = builder.Environment.IsDevelopment();
            })
            // I tempi del canale SignalR sotto il circuito. I default (30 s di attesa del client, keep-alive
            // ogni 15 s) sono tarati su una rete diretta; qui in mezzo ci sono Cloudflare e nginx, e ogni
            // apparato che sta nel mezzo chiude le connessioni che tacciono.
            .AddHubOptions(o =>
            {
                // Quanto il server aspetta un segno di vita dal browser prima di dichiarare morto il circuito.
                // Raddoppiato: un telefono che cambia cella o un portatile che si risveglia stanno zitti più di
                // trenta secondi, e con il default quella pausa costava una pagina intera.
                o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);

                // Il polso che il server manda quando non ha nient'altro da dire. ⚠️ Deve restare ben sotto la
                // metà del timeout appena scritto — è la regola di SignalR — e sotto la soglia di inattività di
                // chi sta nel mezzo: è questo traffico, e solo questo, che impedisce a un proxy di chiudere un
                // WebSocket aperto su una pagina che nessuno sta toccando.
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);

                // La stretta di mano iniziale: 15 s di default. Il primo visitatore dopo una pausa di Passenger
                // la paga mentre il processo sta ancora finendo di avviarsi (~1,3 s misurati, ma su un host
                // condiviso il picco è un'altra cosa). Trenta secondi tolgono di mezzo il caso in cui la prima
                // visita della giornata fallisce e la seconda va.
                o.HandshakeTimeout = TimeSpan.FromSeconds(30);
            });

        // Compressione asset di testo (CSS/JS/SignalR). NIENTE text/event-stream: la rotta SSE /vsop/live/atc
        // usa DisableBuffering() e dev'essere consegnata subito, non compressa/bufferizzata.
        builder.Services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
            o.MimeTypes = new[] { "text/css", "text/javascript", "application/javascript", "application/json", "image/svg+xml", "text/html" };
        });

        // ⚠️ I LIVELLI SI SCRIVONO, e la ragione è misurata — non è rifinitura.
        //
        // Il default di ASP.NET per ENTRAMBI i provider è CompressionLevel.Fastest, che per Brotli vuol dire
        // QUALITÀ 1: il livello più basso che il formato ha. E Brotli è registrato per primo, quindi vince la
        // negoziazione con ogni browser moderno (mandano tutti «br»). Il risultato misurato il 27 agosto 2026
        // sul server vero, prima di questa riga:
        //
        //     vipi-theme.css   grezzo 295 571 B   servito(br) 120 601 B   gzip 101 217 B
        //     HTML vIPI ACC    grezzo 294 776 B   servito(br)  62 161 B   gzip  50 018 B
        //
        // Cioè: attivare Brotli faceva scaricare ~24% di byte IN PIÙ di quanti se ne sarebbero scaricati
        // lasciando solo gzip. Un difetto che non somiglia a un difetto — la compressione c'era, l'header
        // «Content-Encoding: br» pure, e nessun errore da nessuna parte.
        //
        // Optimal e non SmallestSize: SmallestSize è la qualità 11, che su 300 KB costa centinaia di
        // millisecondi di CPU A OGNI RICHIESTA (qui non ci sono varianti precompilate: su net8 UseStaticFiles
        // serve i file così come sono, vedi il commento a UseResponseCompression). Optimal è la qualità 4, che
        // batte gzip-6 e costa poco. Chi volesse la qualità 11 la paghi a build-time, non a richiesta.
        builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
        builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

        // Ogni richiesta finita in eccezione lascia una riga in diagnostica/errori-richieste.txt, con lo
        // stesso codice che la pagina d'errore mostra all'utente. Su questo host i log del processo non li
        // legge nessuno: vedi DiagnosticaErrori.
        builder.Services.AddExceptionHandler<DiagnosticaErrori.Gancio>();

        // ⚠️ E la meta' che il gestore NON vede: un guasto dentro un circuito Blazor non passa dal middleware
        // delle richieste. Il 2 settembre 2026 un «A second operation was started» premendo un tasto ha
        // lasciato l'utente con la barra rossa e la cartella diagnostica VUOTA. Vedi DiagnosticaCircuito.
        builder.Logging.AddProvider(new DiagnosticaCircuito());

        // ⚠️ VIA il registro eventi di Windows. `WebApplication.CreateBuilder` lo aggiunge DA SOLO quando gira
        // su Windows, e non lo vuole nessuno:
        //
        //  - la produzione e' Linux (Plesk+Passenger), quindi la' quella riga non esiste nemmeno: e' un canale
        //    che non e' mai stato ne' scelto ne' letto. Quel che si legge sta in `diagnostica/` (vedi le due
        //    righe qui sopra), ed e' li' che vanno guardati i guasti;
        //  - in sviluppo e SOTTO I TEST, invece, esiste eccome: MISURATO il 2 settembre 2026, **535 voci** nel
        //    registro Applicazione della macchina in tre ore di suite — una decina di host per giro, ognuno
        //    col suo provider, sorgente «.NET Runtime», id 1000;
        //  - ed era la causa di un ROSSO INTERMITTENTE. Il provider tiene un `SafeEventLogWriteHandle` che
        //    muore quando il provider viene disposto: una riga di log scritta TARDI nello spegnimento —
        //    `AtcPollingHostedService.StopAsync` ne scrive una quando il salvataggio finale non riesce —
        //    trovava l'handle gia' chiuso, e l'`ObjectDisposedException` risaliva fino a far fallire
        //    `Host.StopAsync`, cioe' il `Dispose` della fabbrica di prova, cioe' il test.
        //
        // Una riga di log non deve poter far fallire uno spegnimento, e un giro di test non deve scrivere
        // nel registro eventi della macchina.
        //
        // ⚠️ Si toglie SOLO quello: `ClearProviders()` porterebbe via anche la console, il debug e la riga
        // qui sopra, cioe' la diagnostica che serve.
        // ⚠️ Il tipo si nomina, non si cerca per stringa: se un domani sparisse o cambiasse nome, questa riga
        // non compila — invece di smettere di funzionare in silenzio.
        foreach (var registrato in builder.Logging.Services
                     .Where(d => d.ImplementationType == typeof(Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider))
                     .ToList())
            builder.Logging.Services.Remove(registrato);

        // Modulo login IVAO standalone (scenario C). STACCABILE: attivo solo se VipiAuth:Enabled=true.
        // Se attivo, il ClaimsPrincipal lo produce questo modulo e HostIdentityCurrentUserProvider lo legge.
        var authEnabled = builder.AddVipiStandaloneAuth();

        // Persistenza chiavi Data Protection su DB (Postgres e MariaDB, cioè i due deploy): antiforgery, cookie di
        // auth e state OIDC sopravvivono a un riavvio su disco effimero. No-op in dev (SQLite → file-store di
        // default). Vedi VipiDataProtection.cs.
        builder.AddVipiDataProtection();

        // La versione in barra, per i soli admin: la passa l'HOST al modulo, che non ha modo di sapere da
        // quale pacchetto è stato costruito. Vedi VersioneBuild.
        var versione = VersioneBuild.Leggi();
        builder.Services.PostConfigure<Vipi.Application.VipiChromeOptions>(o =>
        {
            o.Versione = versione.Etichetta;
            o.VersioneDettaglio = versione.Dettaglio;
        });

        // Modulo vIPI: un'unica chiamata registra Application, Infrastructure/EF, polling IVAO, opzioni e identità.
        // In sviluppo usa l'utente CH fittizio; in produzione l'identità è letta dal login del sito ospitante.
        // Se il login IVAO standalone è attivo, esso vince sul dev identity anche in sviluppo (si prova il login vero).
        var useDevIdentity = builder.Environment.IsDevelopment() && !authEnabled;
        // Guardia di sicurezza (audit D1): mai identità dev fittizia (admin onnipotente) fuori da Development.
        Vipi.Hosting.ProductionIdentityGuard.EnsureSafe(builder.Environment.IsDevelopment(), useDevIdentity);
        builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: useDevIdentity);

        crono.Segna("registrazioni dei servizi");

        var app = builder.Build();
        crono.Segna("builder.Build");

        // L'altra metà del registro degli avvii. È l'ASSENZA di questa riga a raccontare il crash: uno
        // spegnimento per inattività passa di qui (Passenger manda il segnale e l'host chiude in ordine), un
        // processo ucciso o esploso no. Su ApplicationStopping e non su ApplicationStopped: il secondo arriva
        // dopo la chiusura dei servizi, e su un host che tronca lo spegnimento può non arrivare affatto.
        app.Lifetime.ApplicationStopping.Register(RegistroAvvii.RegistraArresto);

        // Chi chiede lo spegnimento: un segnale del sistema (l'hosting) o nessuno (ci siamo fermati da soli).
        // Va acceso PRIMA che il segnale possa arrivare, cioè prima di servire la prima richiesta.
        SegnaleDiArresto.Ascolta();

        // Da qui in poi un'eccezione fatale NON è più «l'avvio è fallito»: app.Run() blocca fino allo
        // spegnimento, quindi un guasto all'ARRESTO esce dallo stesso catch di Program.cs. Senza questa
        // riga finiva in avvio-errore.txt col titolo sbagliato — ed è successo davvero il 3 settembre
        // 2026, con un processo che aveva servito richieste per un'ora e cinquanta. Vedi
        // StartupDiagnostics.ShutdownFileName.
        app.Lifetime.ApplicationStarted.Register(StartupDiagnostics.SegnaAvvioRiuscito);

        // Dietro il proxy TLS di Fly.io/Render (TLS al bordo, HTTP interno): fidati di X-Forwarded-Proto/For così
        // UseHttpsRedirection non entra in loop e OIDC costruisce il redirect_uri in https. KnownIPNetworks/Proxies
        // svuotati perché l'IP del proxy non è fisso. Innocuo in locale (gli header non arrivano).
        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        // Su net8 la collezione si chiama KnownNetworks (KnownIPNetworks è .NET 9+).
        //
        // Svuotare entrambe significa «fidati di X-Forwarded-For da chiunque», ed è quel che serve su Render, dove
        // l'IP del proxy non è fisso. Su atc.it.ivao.aero NON serve: nginx sta sulla stessa macchina e arriva da
        // loopback. Lasciarle vuote lì vorrebbe dire che l'IP del chiamante lo sceglie il chiamante — e su
        // quell'IP si regge il tetto per-IP del bridge Aurora, oltre a ogni riga di log che dice «da dove».
        //
        // Perciò: in Production ci si fida SOLO del loopback; altrove resta il comportamento di prima.
        forwardedOptions.KnownNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();
        if (app.Environment.IsProduction())
        {
            forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);       // 127.0.0.1
            forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);   // ::1
        }
        app.UseForwardedHeaders(forwardedOptions);

        // ⚠️ PRIMO della pipeline, prima di qualunque cosa possa rispondere o rifiutare: qui non si chiede
        // se la richiesta è buona, si chiede SE È ARRIVATA. È la misura che dice se il keep-alive parla
        // davvero con questo processo — e dal lato di chi bussa un 200 si vede in entrambi i casi.
        app.Use(async (context, next) =>
        {
            TracciaRichieste.Segna(context.Request.Path.Value ?? "/");
            await next();
        });

        // Intestazioni di sicurezza. Non chiudono una falla nota — le due funzioni che costruiscono HTML a mano
        // (MarkdownLite, AorBlock.BuildSvg) encodano prima e costruiscono dopo — ma rendono innocuo l'errore di
        // domani, che è la difesa che costa meno di tutte. Prima di UseStaticFiles, così valgono anche per gli asset.
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["X-Frame-Options"] = "DENY";                        // nessuna pagina vIPI va dentro un iframe
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

            // CSP ancora in sola SEGNALAZIONE, e il perché è scritto qui sotto — non è dimenticanza.
            //
            // `script-src` ha perso `'unsafe-inline'`: i due <script> inline di App.razor (zoom e riaggancio dopo
            // le navigazioni enhanced) sono diventati file, e una guardia E2E pretende che la pagina non ne
            // contenga altri. È il pezzo che conta davvero, perché è quello che rende innocuo uno script iniettato.
            //
            // ⚠️ COSA MANCA PER PASSARE A `Content-Security-Policy` VERO, misurato l'11 agosto 2026:
            //   • 17 gestori inline nel markup (`onclick="window.print()"`, `ondragover="event.preventDefault()"`,
            //     `onclick="location.href=…"`): sono attributi HTML, quindi `script-src` li blocca esattamente come
            //     uno <script> inline. Vanno convertiti in delega di eventi o in handler Blazor — e ognuno cambia il
            //     comportamento di una pagina, quindi va guardato, non riscritto a tappeto.
            //   • 554 attributi `style="…"` nel markup, che tengono in piedi `style-src 'unsafe-inline'`. Questa
            //     clausola è molto meno grave della gemella sugli script, e può restare a lungo.
            //   • una passata con un browser vero: una CSP che entra in vigore rompe in modo vistoso, e nessun test
            //     qui dentro apre una pagina davvero.
            //
            // Finché quei 17 non sono spariti, mettere `Content-Security-Policy` senza `unsafe-inline` romperebbe
            // la stampa, il drag&drop della struttura e tre elenchi. Con `unsafe-inline` non proteggerebbe da nulla.
            // Report-Only dice la verità su entrambe le cose senza rompere niente.
            //
            // ⚠️ `blazor.web.js` ha bisogno di `connect-src` verso il proprio origin (WebSocket): 'self' lo copre
            // per ws:// e wss:// sullo stesso host.
            headers["Content-Security-Policy-Report-Only"] =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                // Le tessere delle mappe non si vendorizzano: sono gli unici host esterni rimasti, e sono dato
                // pubblico di sfondo — i poligoni, che sono il nostro dato, li disegna Leaflet in locale.
                // • `server.arcgisonline.com`: il fondo chiaro di AoR/aree (`vipi-aor.js`, pavimento del 3D) e il
                //   rilievo delle minime di vettoramento (`vipi-mva.js`). Ha sostituito CARTO il 27 agosto 2026,
                //   quando il fondo anonimo è stato chiuso.
                // • `*.tile.opentopomap.org`: il fondo con le curve di livello, seconda scelta delle minime.
                //   ⚠️ Mancava: le minime esistono da giorni e questa riga parlava ancora solo di CARTO. Non si
                //   è visto perché l'intestazione è **Report-Only** — segnala e non blocca.
                "img-src 'self' data: blob: https://server.arcgisonline.com https://*.tile.opentopomap.org; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                // Il visualizzatore PDF di Drive, dentro il riquadro del blocco «Allegato» in modo
                // incorporato. I byte non stanno da noi per vincolo contrattuale, quindi l'unico modo di
                // mostrarli nella pagina è ospitare il loro visualizzatore.
                // ⚠️ Senza questa riga la direttiva cadrebbe su `default-src 'self'` e il riquadro sarebbe
                // vuoto — ma siccome l'intestazione è Report-Only NON si vedrebbe: l'incorporato
                // funzionerebbe oggi e morirebbe in blocco il giorno del passaggio a CSP vera. È la lezione
                // delle tessere OpenTopoMap, mancate per giorni proprio perché segnala e non blocca.
                //
                // ⚠️⚠️ E ci vuole `'self'` PRIMA di Drive, che è la cosa che non si indovina: l'iframe non
                // punta a Drive, punta alla NOSTRA rotta `/vsop/files/{slug}` — è tutto il senso del disegno,
                // l'id del deposito non entra mai in un documento — e `frame-src` guarda l'indirizzo a cui il
                // riquadro naviga, che al primo passo è il nostro. Con la sola voce di Drive il browser
                // segnalava «Framing 'http://…/vsop/files/…' violates frame-src https://drive.google.com»:
                // misurato dal vivo il 30 agosto 2026, e visibile solo perché l'intestazione è Report-Only.
                "frame-src 'self' https://drive.google.com; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";

            await next();
        });

        // Crea la tabella delle chiavi Data Protection se manca (idempotente; no-op se il modulo non è attivo).
        app.UseVipiDataProtection();
        // Crea/migra il DB del modulo. Nessun seed: i dati reali si inseriscono dall'app (editor/struttura).
        // CRITICA: un guasto qui deve fermare l'avvio. Servire pagine su uno schema che non è quello atteso dal
        // codice significa scoprirlo a runtime, come colonna mancante, lontano dalla causa.
        app.MigrateVipiDatabase();
        crono.Segna("migrazione del database");

        // Le quattro manutenzioni non critiche (riconciliazioni documentali, proiezione settori, backfill e potatura
        // delle release), ognuna isolata dalle altre: un guasto viene registrato — log + diagnostica, quindi
        // /vsop/health in Degraded — e l'avvio prosegue. Prima erano quattro chiamate nude, e con Restart=always nel
        // servizio systemd un difetto in una di esse non era un degrado ma un ciclo di riavvii.
        app.RunVipiStartupMaintenance();
        crono.Segna("manutenzioni d'avvio");

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
            // Solo in prod: in dev l'host ascolta su http e il redirect logga un warning inutile.
            app.UseHttpsRedirection();
        }

        // Compressione delle risposte. Su net8 ci passano ANCHE i file statici, perché UseStaticFiles serve i file
        // così come sono: niente varianti .br/.gz precompilate a build-time, quelle le faceva MapStaticAssets (.NET 9+).
        // Costo: la compressione di CSS e JS si paga a ogni richiesta invece che una volta in build.
        app.UseResponseCompression();

        // File statici: wwwroot dell'host + wwwroot della RCL vIPI (_content/Vipi.Ui/...).
        // Rimpiazza MapStaticAssets, che è .NET 9+ (ADR-0007 §D4-ter). Il cache-busting lo fa AssetVersion, che
        // legge i file da QUESTO stesso provider: l'impronta nell'URL è quella del contenuto servito, non della
        // build, quindi un asset immutato conserva il proprio URL e resta valido in cache.
        AssetVersion.Initialize(app.Environment.WebRootFileProvider);

        // Le varianti già compresse alla qualità massima, quando ci sono (le prepara il publish: vedi il
        // target VipiOttimizzaAsset). Deve stare PRIMA di UseStaticFiles, che è chi poi le consegna.
        // In sviluppo non esistono e questo middleware non fa niente.
        app.UseVipiAssetPrecompressi();

        app.UseStaticFiles(new StaticFileOptions
        {
            // Senza questo, «vipi-theme.css.br» sarebbe un tipo sconosciuto e UseStaticFiles risponderebbe
            // 404: le varianti starebbero nel pacchetto senza che nessuno le riceva.
            ContentTypeProvider = new AssetPrecompressi.TipiConVariantiCompresse(),
            OnPrepareResponse = ctx =>
            {
                var dev = app.Environment.IsDevelopment();
                // ⚠️ Il nome può essere quello della VARIANTE («vipi-fonts.css.br»): la decisione sulla
                // cache si prende sul nome ORIGINALE, o un .woff2.br — se un domani ce ne fosse uno —
                // prenderebbe la scadenza corta di tutti gli altri.
                var percorso = SenzaSuffissoDiCompressione(ctx.File.Name);

                // I .woff2 sono referenziati da DENTRO vipi-fonts.css, quindi NON passano da AssetVersion e il
                // loro URL non cambia mai. I nomi però arrivano da Google Fonts, sono già content-addressed e i
                // file non cambiano: la cache lunga è sicura ed evita di riscaricarli a ogni deploy.
                var lunga = percorso.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);

                ctx.Context.Response.Headers.CacheControl = dev
                    ? "no-cache, no-store, must-revalidate"
                    : lunga ? "public,max-age=604800"    // 7 giorni
                            : "public,max-age=86400";    // 1 giorno: col ?v= per contenuto, un asset immutato
                                                         // conserva l'URL e alla scadenza si rivalida con un 304
            },
        });

        // Auth standalone (scenario C): serve il ClaimsPrincipal alle richieste. Prima di UseVipiModule,
        // così lo StaffLoginTrackingMiddleware vede già l'utente autenticato. Montato solo se attivo.
        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapVipiStandaloneAuth();
        }

        // DOPO UseAuthentication/UseAuthorization, come chiede la guida Blazor — e non prima, com'era fino
        // all'11 agosto 2026. Il token antiforgery va legato all'identità che lo chiede: girando prima, il
        // middleware lo emette quando l'utente non è ancora montato, e il token resta valido attraverso un
        // cambio di utente sulla stessa sessione. Nessun exploit da mostrare; solo un ordine che non aveva
        // motivo di essere quello.
        app.UseAntiforgery();

        // Le letture anonime dei documenti pubblici sono copie CONGELATE: si possono tenere per un minuto,
        // e non hanno bisogno del cookie antiforgery — in tutta l'interfaccia non esiste un form da inviare.
        // Dopo UseAuthentication, perché la decisione guarda anche se chi chiede è entrato. Vedi
        // CacheDelleLettureAnonime, che spiega ognuna delle sette clausole.
        app.UseVipiCacheDelleLettureAnonime();

        // Middleware del modulo (registrazione login staff nel roster).
        app.UseVipiModule();

        // Compat: TUTTI gli URL storici passano da qui, e ne escono con l'indirizzo di oggi — quello finale, in UN
        // salto solo. La tabella e il perché stanno in LegacyRoutes: qui resta il collegamento.
        //   /sop*  (pre-Round 12)             → /services/vsop/*
        //   /vsop* (pre-22 agosto 2026)       → /services/vsop/*, coi segmenti tradotti (guida → guide, …)
        //   /vsop/{acc}/operativa|live[-app]  → /services/vsop/live[/{callsign}]  (il callsign stava in query)
        //   /vsop/admin/struttura             → /services/vsop/admin/sector-structure
        // ⚠️ Gli endpoint macchina (health, api, media, live/atc) NON passano di qui: hanno segmenti letterali, che
        // nel routing battono queste catch-all, e LegacyRoutes.Resolve li rifiuta comunque.
        static IResult RedirectLegacy(HttpContext ctx)
        {
            var destinazione = LegacyRoutes.Resolve(ctx.Request);
            return destinazione is null ? Results.NotFound() : Results.Redirect(destinazione, permanent: true);
        }

        foreach (var storica in new[] { "/sop", "/sop/{*rest}", "/vsop", "/vsop/{*rest}" })
            app.MapGet(storica, RedirectLegacy);

        // (I file statici li serve UseStaticFiles, più in alto: su net8 non esistono né MapStaticAssets né
        //  WithStaticAssets, che sono .NET 9+.)

        // La pagina d'errore. E' un endpoint e non un componente perche' deve reggere anche quando a lanciare
        // e' stato il layout condiviso — successo il 24 agosto 2026: una pagina d'errore che passasse di li'
        // lancerebbe una seconda volta. Il codice che mostra e' quello scritto in diagnostica/errori-richieste.txt.
        app.MapGet("/Error", (HttpContext ctx) =>
            Results.Content(
                PaginaErrore.Build(System.Diagnostics.Activity.Current?.Id ?? ctx.TraceIdentifier),
                "text/html; charset=utf-8"));

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(VipiModuleExtensions.UiAssembly);   // monta la RCL vIPI

        // Endpoint del modulo (SSE live ATC).
        app.MapVipiModule();

        crono.Segna("resto della pipeline");
        crono.Scrivi();

        app.Run();
    }

    /// <summary>
    /// «vipi-fonts.css.br» → «vipi-fonts.css». Serve a decidere sul file VERO quando quello che si sta
    /// servendo è la sua variante compressa.
    /// </summary>
    private static string SenzaSuffissoDiCompressione(string nome) =>
        nome.EndsWith(".br", StringComparison.OrdinalIgnoreCase) ||
        nome.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? nome[..nome.LastIndexOf('.')]
            : nome;
}
