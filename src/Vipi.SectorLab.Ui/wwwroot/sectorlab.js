// Aurora Sector Lab: il ponte fra la pagina e la finestra che la ospita (WebView2).
// Fuori dalla WebView2 (un browser qualunque, i test) `chrome.webview` non c'è e i messaggi si perdono senza errori.
(function () {
    function allaFinestra(testo) {
        try {
            if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(String(testo));
        } catch (e) { /* la finestra se n'è andata: niente da dire a nessuno */ }
    }

    // Un errore di JavaScript nella WebView2 non lo vede nessuno: la finestra lo scrive nel diario d'avvio.
    window.addEventListener('error', function (e) { allaFinestra('errore: ' + e.message + ' (' + e.filename + ':' + e.lineno + ')'); });
    window.addEventListener('unhandledrejection', function (e) { allaFinestra('errore: ' + (e.reason && e.reason.message || e.reason)); });

    window.sectorlab = {
        /// Il circuito Blazor è vivo: lo chiama la pagina iniziale al primo render interattivo.
        pronto: function () { allaFinestra('pronto'); }
    };
})();
