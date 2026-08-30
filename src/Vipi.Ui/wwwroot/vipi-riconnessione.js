// Quando il server sparisce: quanto ci si riprova, che cosa legge chi guarda, e come si torna in piedi.
//
// Il sintomo che questo file affronta è «Attempting to reconnect to the server…» sul sito vero. NON vuol
// dire che il sito sia giù: vuol dire che è morto il CIRCUITO, cioè lo stato che il server tiene in memoria
// per quella pagina. Le cause sono due, e vogliono due rimedi opposti:
//
//   1. un buco di rete (WiFi che salta, telefono che cambia cella, portatile che si risveglia). Il circuito
//      di là c'è ancora — il server li trattiene cinque minuti, vedi DisconnectedCircuitRetentionPeriod in
//      VipiStartup — e la cosa giusta è RIPROVARE finché quella finestra dura: si ritrova la pagina esatta,
//      bozza dell'editor compresa.
//
//   2. il processo è morto e ne è nato un altro. Su questo hosting (Plesk + Passenger) succede da solo:
//      quando per un po' nessuno chiede niente, Passenger spegne, e alla richiesta successiva rigenera. Il
//      circuito di prima non esiste più da nessuna parte, e riprovare è tempo perso — il server risponde
//      «no, quel circuito non lo conosco». La cosa giusta è RICARICARE la pagina, che è anche la richiesta
//      che risveglia il processo.
//
// Il difetto di com'era prima è che il caso 2 finiva nel comportamento del caso 1: si riprovava, si
// falliva, e restava sullo schermo un riquadro nero in inglese con un tasto da premere. Questo file
// distingue i due casi — «rifiutato» è il caso 2, e lo dice il server stesso — e nel secondo ricarica da
// solo. Chi sta leggendo vede mezzo secondo di ricaricamento invece di un messaggio d'errore.
//
// La terza cosa che fa è il colpetto: una richiesta ogni due minuti e mezzo a /vsop/ping, che è vuota
// apposta, per impedire a Passenger di spegnere il processo mentre qualcuno ha una scheda aperta. Il caso 2
// così, per chi sta usando il sito, in gran parte non capita più.
//
// ⚠️ QUESTO FILE È OBBLIGATORIO. Da quando c'è, `blazor.web.js` è caricato con `autostart="false"` e a
// chiamare `Blazor.start` è la riga qui sotto: se il file non arriva al browser (dimenticato in un
// caricamento FTP, o l'indice degli asset scambiato senza i .js — è già successo, vedi
// LEGGIMI-CORREZIONE-20260824e.md), il sito si vede ma NIENTE è interattivo, senza errori in pagina.
// Va caricato subito dopo blazor.web.js e prima di tutti gli altri script nostri.
(function () {
    "use strict";

    var qui = document.currentScript;

    // ── I numeri, e perché sono questi ───────────────────────────────────────────────────────────────
    //
    // Il prodotto TENTATIVI × INTERVALLO dev'essere ≈ la finestra in cui il server tiene i circuiti
    // staccati (5 minuti, VipiStartup): riprovare più a lungo di così vuol dire ritentare quando di là non
    // c'è già più niente, riprovare meno vuol dire arrendersi mentre ci sarebbe ancora.
    var TENTATIVI = 55;
    var INTERVALLO_MS = 5000;              // 55 × 5 s ≈ 4 min 35 s

    // Quanto il BROWSER aspetta un segno di vita dal server prima di dichiararlo morto, e ogni quanto ne
    // manda uno lui. ⚠️ Vanno tenuti in coppia con ClientTimeoutInterval e KeepAliveInterval in
    // VipiStartup: sono gli stessi due valori visti dai due capi, e se divergono vince il più impaziente.
    var ATTESA_SERVER_MS = 60000;
    var POLSO_MS = 15000;

    // Il colpetto anti-spegnimento. Passenger spegne per inattività dopo qualche minuto: due minuti e mezzo
    // stanno sotto ogni soglia plausibile senza diventare traffico.
    var COLPETTO_MS = 150000;

    // Quante ricariche automatiche si tollerano in un minuto prima di smettere e chiedere all'utente. Se il
    // server rifiuta il circuito ANCHE dopo una pagina nuova, ricaricare all'infinito farebbe un ciclo che
    // non si può nemmeno interrompere leggendo il messaggio.
    var RICARICHE_MASSIME = 3;
    var FINESTRA_RICARICHE_MS = 60000;

    // ── 1. L'avvio di Blazor, con i nostri tempi ─────────────────────────────────────────────────────
    if (window.Blazor && typeof window.Blazor.start === "function") {
        try {
            var avvio = window.Blazor.start({
                circuit: {
                    reconnectionOptions: {
                        maxRetries: TENTATIVI,
                        retryIntervalMilliseconds: INTERVALLO_MS
                    },
                    // ⚠️ I due metodi si controllano prima di chiamarli: appartengono al client SignalR 8,
                    // e questo file deve continuare a fare il resto anche se un domani il pacchetto cambia
                    // e uno dei due sparisce. Perdere i tempi di attesa è un peggioramento; morire qui
                    // dentro lascerebbe il sito senza interattività.
                    configureSignalR: function (costruttore) {
                        if (typeof costruttore.withServerTimeout === "function") costruttore.withServerTimeout(ATTESA_SERVER_MS);
                        if (typeof costruttore.withKeepAliveInterval === "function") costruttore.withKeepAliveInterval(POLSO_MS);
                        return costruttore;
                    }
                }
            });
            if (avvio && typeof avvio.catch === "function") avvio.catch(function (e) { console.error("[vIPI] avvio di Blazor fallito", e); });
        } catch (e) {
            console.error("[vIPI] avvio di Blazor fallito", e);
        }
    }

    // ── 2. Il riquadro di riconnessione ──────────────────────────────────────────────────────────────
    //
    // Il riquadro è in App.razor (markup e testi tradotti); Blazor non lo disegna, gli mette e gli toglie
    // delle classi. Le due che contano:
    //
    //   components-reconnect-rejected  il server ha risposto «quel circuito non lo conosco»  → caso 2
    //   components-reconnect-failed    finiti i tentativi senza mai raggiungere il server    → caso 1 perso
    //
    // Sul primo si ricarica da soli. Sul secondo NO: quasi sempre è la rete dell'utente che non c'è, e una
    // pagina ricaricata senza rete è una pagina di errore del browser — peggio del riquadro, che almeno
    // spiega e offre il tasto.
    var modale = document.getElementById("components-reconnect-modal");
    if (modale) {
        var giaRicaricato = false;
        var osservatore = new MutationObserver(function () {
            if (giaRicaricato) return;
            if (!modale.classList.contains("components-reconnect-rejected")) return;
            giaRicaricato = true;
            ricarica();
        });
        osservatore.observe(modale, { attributes: true, attributeFilter: ["class"] });
    }

    // Il tasto del riquadro: ricarica, non `Blazor.reconnect()`. Riconnettersi è quel che si è già provato
    // cinquantacinque volte; ricaricare è la sola mossa che funziona in entrambi i casi.
    var tasto = document.getElementById("vipi-riconnessione-ricarica");
    if (tasto) tasto.addEventListener("click", function () { location.reload(); });

    /// Ricarica, contando le ricariche recenti per non entrare in un ciclo.
    function ricarica() {
        try {
            var chiave = "vipi.ricariche";
            var adesso = Date.now();
            var storia = [];
            try { storia = JSON.parse(sessionStorage.getItem(chiave) || "[]"); } catch (e) { storia = []; }
            if (!Array.isArray(storia)) storia = [];

            storia = storia.filter(function (t) { return typeof t === "number" && adesso - t < FINESTRA_RICARICHE_MS; });
            if (storia.length >= RICARICHE_MASSIME) {
                // Si smette e si lascia parlare il riquadro: ricaricare non sta risolvendo niente.
                if (modale) modale.classList.add("vipi-riconnessione-arresa");
                return;
            }

            storia.push(adesso);
            sessionStorage.setItem(chiave, JSON.stringify(storia));
        } catch (e) {
            // Navigazione privata o storage negato: si ricarica lo stesso. Il conteggio è una protezione,
            // non un permesso — vedi vipi-zoom.js, che sullo stesso storage ha lo stesso patto.
        }

        location.reload();
    }

    // ── 3. Il colpetto che tiene sveglio il processo ─────────────────────────────────────────────────
    //
    // ⚠️ Solo a scheda VISIBILE. Una scheda dimenticata in fondo alla barra non deve tenere acceso un
    // processo per giorni: il risparmio di Passenger è anche nostro, e i browser strozzano comunque i timer
    // in secondo piano — un colpetto «ogni due minuti e mezzo» da lì non sarebbe nemmeno quello.
    var indirizzo = new URL("vsop/ping", document.baseURI).href;
    var ultimoColpetto = 0;

    function colpetto() {
        if (document.visibilityState === "hidden") return;
        ultimoColpetto = Date.now();
        // `credentials: "omit"`: nessun cookie, quindi nessuna sessione toccata e nessun lavoro in più per
        // il server. `keepalive` no: è una richiesta che può morire con la pagina senza danni.
        fetch(indirizzo, { method: "GET", cache: "no-store", credentials: "omit" })
            .catch(function () { /* il server non risponde: se ne riparla fra due minuti e mezzo */ });
    }

    setInterval(colpetto, COLPETTO_MS);

    // Al ritorno su una scheda lasciata in secondo piano il timer è stato strozzato: se è passato più del
    // dovuto si bussa subito, così la prima cosa che l'utente fa non paga l'avvio del processo.
    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "visible" && Date.now() - ultimoColpetto > COLPETTO_MS) colpetto();
    });

    // Non al caricamento: la pagina che sta arrivando È già la richiesta che tiene sveglio il processo.
    ultimoColpetto = Date.now();

    // Lasciato raggiungibile per la verifica dal vivo: dalla console si controlla che i numeri in
    // produzione siano quelli scritti qui, senza doverli dedurre dal comportamento.
    window.vipiRiconnessione = {
        tentativi: TENTATIVI,
        intervalloMs: INTERVALLO_MS,
        colpettoMs: COLPETTO_MS,
        indirizzoColpetto: indirizzo,
        sorgente: qui ? qui.src : null
    };
})();
