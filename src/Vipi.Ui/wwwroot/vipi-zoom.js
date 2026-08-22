// Zoom globale persistente (localStorage), applicato PRIMA che la pagina si disegni.
//
// Perché è un file e non uno <script> in fondo alla pagina: serve nel <head>, prima del primo disegno,
// o si vede la pagina alla dimensione sbagliata per un istante e poi saltare a quella giusta.
// Perché è un file e non uno <script> INLINE nel <head>: uno script inline richiede
// `script-src 'unsafe-inline'` nella Content-Security-Policy, cioè la clausola che rende la CSP quasi
// inutile — con quella attiva, uno script iniettato in pagina verrebbe eseguito.
//
// Va caricato con l'attributo `defer` ASSENTE e prima del <body>: deve girare subito.
(function () {
    var read = function () {
        var z = parseFloat(localStorage.getItem('vipiZoom') || '1');
        return isNaN(z) ? 1 : z;
    };

    window.vipiApplyZoom = function () {
        var z = read();
        document.documentElement.style.zoom = z;
        var el = document.getElementById('vipiZoomPct');
        if (el) el.textContent = Math.round(z * 100) + '%';
        // Chi misura lo spazio disponibile (vipiFitViewport, vipiStickyOffset) deve rifare i conti: lo zoom
        // cambia quanta pagina ci sta, ma non fa scattare né un render Blazor né un `resize` di suo.
        try { window.dispatchEvent(new Event('resize')); } catch (e) { }
    };

    window.vipiSetZoom = function (v) {
        v = Math.min(1.8, Math.max(0.7, Math.round(v * 100) / 100));
        localStorage.setItem('vipiZoom', v);
        window.vipiApplyZoom();
    };

    window.vipiZoom = function (d) { window.vipiSetZoom(read() + d); };
    window.vipiZoomReset = function () { window.vipiSetZoom(1); };

    window.vipiApplyZoom();

    // ⚠️ La chiamata qui sopra gira nel <head>, quando #vipiZoomPct non esiste ancora: applica lo zoom
    // (l'urgenza) ma non aggiorna la percentuale scritta nella barra. Senza questa seconda passata, chi
    // ha lo zoom al 120% leggeva «100%» fino alla prima navigazione «enhanced».
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { window.vipiApplyZoom(); });
    } else {
        window.vipiApplyZoom();
    }
})();
