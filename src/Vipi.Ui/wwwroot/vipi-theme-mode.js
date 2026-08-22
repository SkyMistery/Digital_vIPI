// Scelta del tema (automatico / chiaro / scuro), persistita in localStorage e applicata PRIMA che la
// pagina si disegni.
//
// Perche' e' un file e non uno <script> in fondo alla pagina: serve nel <head>, prima del primo disegno.
// Altrimenti chi ha scelto il tema scuro vede un lampo bianco a ogni caricamento — ed e' peggio del
// lampo dello zoom, perche' e' l'intera pagina e non la sua dimensione.
// Perche' e' un file e non uno <script> INLINE nel <head>: uno script inline richiede
// `script-src 'unsafe-inline'` nella Content-Security-Policy, cioe' la clausola che la rende quasi
// inutile. Stessa ragione di vipi-zoom.js e vipi-boot.js.
//
// Va caricato con l'attributo `defer` ASSENTE e prima del <body>: deve girare subito.
(function () {
    var KEY = 'vipiTheme';           // '' | 'light' | 'dark'  ('' o assente = automatico)
    var MODI = ['auto', 'light', 'dark'];

    // ⚠️ localStorage puo' LANCIARE, non solo tornare null: in navigazione privata e con i dati di sito
    // bloccati il solo accesso e' un'eccezione. Un tema che non si ricorda e' un fastidio; una pagina che
    // non si disegna e' un guasto.
    function leggi() {
        try {
            var v = localStorage.getItem(KEY);
            return MODI.indexOf(v) > 0 ? v : 'auto';
        } catch (e) { return 'auto'; }
    }

    function scrivi(v) {
        try { if (v === 'auto') localStorage.removeItem(KEY); else localStorage.setItem(KEY, v); }
        catch (e) { /* preferenza non memorizzabile: il tema vale comunque per questa pagina */ }
    }

    // In automatico l'attributo NON c'e': il foglio di stile tratta l'assenza come «segui il sistema»
    // (`@media (prefers-color-scheme:dark)`), e scrivere `data-theme="auto"` vorrebbe dire aggiungere un
    // caso in piu' al CSS per dire la stessa cosa.
    window.vipiApplyTema = function () {
        var m = leggi();
        var r = document.documentElement;
        if (m === 'auto') r.removeAttribute('data-theme');
        else r.setAttribute('data-theme', m);

        // L'ICONA la sceglie il CSS sullo stato di :root: il chrome e' SSR statico e non si ridisegna da
        // solo. Qui resta solo l'etichetta, che in CSS non e' esprimibile. Le tre traduzioni arrivano
        // dagli attributi data-*, perche' le stringhe stanno nel resx e non in questo file.
        var nodi = document.querySelectorAll('[data-theme-ctrl]');
        for (var i = 0; i < nodi.length; i++) {
            var b = nodi[i];
            var eti = b.getAttribute('data-lbl-' + m);
            if (eti) { b.setAttribute('title', eti); b.setAttribute('aria-label', eti); }
        }
        var scelte = document.querySelectorAll('[data-theme-set]');
        for (var j = 0; j < scelte.length; j++) {
            var s = scelte[j], on = s.getAttribute('data-theme-set') === m;
            s.classList.toggle('on', on);
            s.setAttribute('aria-pressed', on ? 'true' : 'false');
        }
    };

    window.vipiSetTema = function (v) {
        if (MODI.indexOf(v) < 0) v = 'auto';
        scrivi(v);
        window.vipiApplyTema();
        // Chi ha gia' DISEGNATO usando un token che si gira (il fondo del visore 3D e' su canvas, e un
        // canvas non si ridipinge da solo) deve poter rifare i conti. `resize` e' il segnale che questo
        // codice gia' usa per lo zoom ed e' quello che quei disegni ascoltano.
        try { window.dispatchEvent(new CustomEvent('vipi:tema', { detail: { modo: v } })); } catch (e) { }
        try { window.dispatchEvent(new Event('resize')); } catch (e) { }
    };

    // Giro: automatico -> chiaro -> scuro -> automatico.
    window.vipiCicloTema = function () {
        window.vipiSetTema(MODI[(MODI.indexOf(leggi()) + 1) % MODI.length]);
    };

    window.vipiApplyTema();

    // ⚠️ La chiamata qui sopra gira nel <head>: i bottoni NON esistono ancora, quindi imposta il tema
    // (che e' l'urgenza: niente lampo) ma non le etichette. Senza questa seconda passata l'etichetta e'
    // giusta solo dopo la prima navigazione «enhanced».
    // (E' il difetto che vipi-zoom.js ha davvero: al primo disegno #vipiZoomPct mostra sempre «100%»
    //  anche a chi ha lo zoom al 120%. Qui non si ripete.)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { window.vipiApplyTema(); });
    } else {
        window.vipiApplyTema();
    }
})();
