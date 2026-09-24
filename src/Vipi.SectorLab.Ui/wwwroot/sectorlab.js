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
        pronto: function () { allaFinestra('pronto'); },

        /// Una riga nel diario d'avvio della finestra: i tempi della mappa, e quel che si vuole misurare dal vivo.
        diario: function (testo) { allaFinestra(String(testo)); },

        /// Due schermi: i pannelli in un'altra finestra. Nella WebView2 la window.open arriva al guscio, che apre la sua
        /// finestra (solo per /pannelli); in un browser qualunque è una scheda nuova, e funziona lo stesso.
        staccaIPannelli: function () { window.open('/pannelli', 'sectorlab-pannelli'); },

        /// Chiede al guscio di chiudere la finestra dei pannelli; chiudendola, lui dice al Lab «di nuovo qui».
        riportaIPannelli: function () { allaFinestra('pannelli: chiudi'); },

        /// Il tasto del tema: automatico → chiaro → scuro (sectorlab-tema.js, caricato nel <head>).
        cambiaTema: function () { if (window.sectorlabTema) window.sectorlabTema.cambia(); },

        /// Ctrl+Z e Ctrl+Y (o Ctrl+Maiusc+Z) vanno al Lab: annulla e ripeti l'ultimo gesto. Dentro un campo di testo no:
        /// lì annullano quel che si sta scrivendo, come ovunque in Windows.
        tasti: function (riferimento) {
            storia = riferimento;
            if (tastiAgganciati) return;
            tastiAgganciati = true;
            document.addEventListener('keydown', function (e) {
                if (!storia || !(e.ctrlKey || e.metaKey) || e.altKey) return;
                var t = e.target;
                // In un campo del Lab dove NON si sta scrivendo (il valore è quello confermato: dopo Invio il cursore
                // resta lì) Ctrl+Z va al Lab: lì il browser non avrebbe niente da annullare, e il gesto sembrava non
                // funzionare (prova 7 del committente). Dove si sta scrivendo, annulla la scrittura come ovunque.
                var campoFermo = t && t.tagName === 'INPUT' && t.classList.contains('lab-campo') && !t.sectorlabSporco;
                if (t && !campoFermo && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
                var tasto = (e.key || '').toLowerCase();
                var cosa = tasto === 'z' && !e.shiftKey ? 'annulla'
                    : tasto === 'y' || (tasto === 'z' && e.shiftKey) ? 'ripeti' : null;
                if (!cosa) return;
                e.preventDefault();
                storia.invokeMethodAsync('Tasto', cosa).catch(function () { /* il circuito se n'è andato */ });
            });
        }
    };

    var storia = null;
    var tastiAgganciati = false;

    // Un campo «sporco» ha testo scritto e non ancora confermato (Invio o uscita): in una proprietà JS, non in un
    // data-*, perché Blazor ridisegnando cancellerebbe l'attributo.
    document.addEventListener('input', function (e) { if (e.target) e.target.sectorlabSporco = true; }, true);
    document.addEventListener('change', function (e) { if (e.target) e.target.sectorlabSporco = false; }, true);

    // Le colonne si allargano trascinando il divisore alla loro destra (il suo data-divide dice quale). Un ascoltatore
    // solo, sul documento: i divisori Blazor li ridisegna e li ricrea, e un ascoltatore attaccato a loro si perderebbe.
    document.addEventListener('pointerdown', function (e) {
        var divisore = e.target && e.target.closest ? e.target.closest('[data-divide]') : null;
        if (!divisore || e.button !== 0) return;
        var colonna = divisore.previousElementSibling;
        if (!colonna) return;

        e.preventDefault();
        var nome = divisore.getAttribute('data-divide');
        var partenza = e.clientX;
        var larghezza = colonna.getBoundingClientRect().width;
        var ultima = larghezza;
        divisore.setPointerCapture(e.pointerId);
        divisore.classList.add('lab-divisore-attivo');
        document.body.classList.add('lab-trascina');

        function muovi(m) {
            // Mai sotto i 160 px, e alla mappa (o all'ultima colonna) restano sempre almeno 280 px.
            var massimo = Math.max(160, window.innerWidth - 280 - colonna.getBoundingClientRect().left);
            ultima = Math.min(massimo, Math.max(160, larghezza + m.clientX - partenza));
            document.documentElement.style.setProperty('--lab-l-' + nome, ultima + 'px');
        }

        function lascia() {
            divisore.removeEventListener('pointermove', muovi);
            divisore.removeEventListener('pointerup', lascia);
            divisore.removeEventListener('pointercancel', lascia);
            divisore.classList.remove('lab-divisore-attivo');
            document.body.classList.remove('lab-trascina');
            if (window.sectorlabTema) window.sectorlabTema.larghezza(nome, ultima);
        }

        divisore.addEventListener('pointermove', muovi);
        divisore.addEventListener('pointerup', lascia);
        divisore.addEventListener('pointercancel', lascia);
    });

    // Doppio clic sul divisore: la colonna torna alla larghezza di base.
    document.addEventListener('dblclick', function (e) {
        var divisore = e.target && e.target.closest ? e.target.closest('[data-divide]') : null;
        if (!divisore) return;
        var nome = divisore.getAttribute('data-divide');
        document.documentElement.style.removeProperty('--lab-l-' + nome);
        if (window.sectorlabTema) window.sectorlabTema.larghezza(nome, null);
    });
})();
