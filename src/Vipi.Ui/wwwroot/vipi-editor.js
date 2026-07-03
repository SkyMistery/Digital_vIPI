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
            if ((e.ctrlKey || e.metaKey) && (e.key === 'e' || e.key === 'E') && !isTypingTarget(e.target)) {
                e.preventDefault();
                current.invokeMethodAsync('ToggleEdit');
            }
        });
    }

    // Chiamato da OnAfterRenderAsync: l'ultima pagina montata diventa la destinataria.
    window.vipiEditorInit = function (dotRef) { current = dotRef; };

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
