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
    // ⚠️ localStorage puo' LANCIARE, non solo tornare null: in navigazione privata e con i dati di sito
    // bloccati il solo accesso e' un'eccezione. Il gemello vipi-theme-mode.js lo sapeva gia'; qui mancava, e
    // il prezzo non era lo zoom. `vipiApplyZoom` e' la PRIMA riga di vipi-boot.js: un'eccezione qui spegneva
    // tutto il riaggancio dopo ogni navigazione «enhanced» — chip AoR, mappe, persistenza del collasso,
    // misura della topbar. Uno zoom che non si ricorda e' un fastidio; meta' applicazione ferma e' un guasto.
    var read = function () {
        var g;
        try { g = localStorage.getItem('vipiZoom'); }
        catch (e) { return window.__vipiZoom || 1; }
        var z = parseFloat(g || '1');
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
        // Se la preferenza non si puo' memorizzare, lo zoom vale comunque per questa pagina: e' la stessa
        // scelta che fa vipi-theme-mode.js col tema.
        try { localStorage.setItem('vipiZoom', v); } catch (e) { }
        window.__vipiZoom = v;   // memoria di sessione, per quando localStorage non c'e'
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
