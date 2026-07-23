// Scorciatoie da tastiera dell'editor vIPI. Registrato per-pagina con un riferimento DotNet (vipiEditorInit).
// Ctrl/Cmd+E alterna la modalità Modifica. Ignorato quando il focus è in un campo di testo.
(function () {
    var current = null;   // DotNetObjectReference della pagina editor attiva

    function isTypingTarget(el) {
        if (!el) return false;
        var tag = (el.tagName || '').toUpperCase();
        return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
    }

    // Un solo listener globale, installato una volta.
    if (!window.__vipiEditorKeys) {
        window.__vipiEditorKeys = true;
        document.addEventListener('keydown', function (e) {
            if (!current) return;
            if (!(e.ctrlKey || e.metaKey) || isTypingTarget(e.target)) return;   // in un campo: lascia l'undo nativo del testo
            var k = (e.key || '').toLowerCase();
            if (k === 'e') { e.preventDefault(); current.invokeMethodAsync('ToggleEdit'); }
            else if (k === 'z' && !e.shiftKey) { e.preventDefault(); current.invokeMethodAsync('UndoAction'); }
            else if (k === 'y' || (k === 'z' && e.shiftKey)) { e.preventDefault(); current.invokeMethodAsync('RedoAction'); }
        });
    }

    // Chiamato da OnAfterRenderAsync: l'ultima pagina montata diventa la destinataria.
    window.vipiEditorInit = function (dotRef) { current = dotRef; };

    // --- Editor aeroporto: Ctrl/Cmd+S salva le sezioni modificate; guardia beforeunload se ci sono modifiche non salvate. ---
    var airportRef = null;   // DotNetObjectReference dell'editor aeroporto
    var airportDirty = false;

    // Chiamato da OnAfterRenderAsync dell'editor aeroporto.
    window.vipiAirportEditorInit = function (dotRef) { airportRef = dotRef; };
    // Chiamato dalla pagina quando lo stato "sporco" cambia.
    window.vipiSetDirty = function (v) { airportDirty = !!v; };
    // Espande/comprime tutte le sezioni <details.ed-sec> dell'editor.
    window.vipiEditorSections = function (open) {
        document.querySelectorAll('details.ed-sec').forEach(function (d) {
            if (open) { d.setAttribute('open', ''); } else { d.removeAttribute('open'); }
        });
    };

    if (!window.__vipiAirportKeys) {
        window.__vipiAirportKeys = true;
        // Ctrl/Cmd+S → salva tutte le sezioni modificate (anche col focus in un campo).
        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
                if (airportRef) { e.preventDefault(); airportRef.invokeMethodAsync('SaveAllDirty'); }
            }
        });
        // Avviso del browser su chiusura/refresh/navigazione con modifiche pendenti.
        window.addEventListener('beforeunload', function (e) {
            if (airportDirty) { e.preventDefault(); e.returnValue = ''; }
        });
        // Saltando a una sezione dalla mini-nav (#sec-…), se è collassata la apre.
        window.addEventListener('hashchange', function () {
            if (!location.hash) return;
            var el = document.querySelector(location.hash);
            if (el && el.tagName === 'DETAILS') el.setAttribute('open', '');
        });
    }

    // Store chiave/valore su localStorage (best-effort: ignora quota/privacy errori).
    window.vipiStoreGet = function (key) {
        try { return window.localStorage.getItem(key); } catch (e) { return null; }
    };
    window.vipiStoreSet = function (key, val) {
        try { if (val == null) window.localStorage.removeItem(key); else window.localStorage.setItem(key, val); } catch (e) { }
    };

    // Scroll a un'ancora lasciando spazio per la barra sticky (altezza misurata a runtime).
    window.vipiScrollTo = function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var bar = document.querySelector('.editor-bar');
        var off = (bar ? bar.offsetHeight : 0) + 12;
        var y = el.getBoundingClientRect().top + window.pageYOffset - off;
        window.scrollTo({ top: y, behavior: 'smooth' });
    };
})();
