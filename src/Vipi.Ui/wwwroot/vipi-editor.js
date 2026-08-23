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
    // Fisarmonica dei BLOCCHI dell'editor ACC: aprendone uno gli altri si chiudono. Senza, la pagina torna
    // subito ai 9 690px misurati — e con due o tre gruppi APP aperti l'indice non basta più a orientarsi.
    // `toggle` NON fa bubbling: va ascoltato in cattura (stessa ragione di vipi-aor.js).
    // Il listener è installato UNA volta: questo file può essere rieseguito da una navigazione arricchita, e
    // due listener chiuderebbero i fratelli due volte.
    if (!window.__vipiAccAccordion) {
        window.__vipiAccAccordion = true;
        document.addEventListener('toggle', function (ev) {
            var d = ev.target;
            if (!d || !d.matches || !d.matches('details.acc-block')) return;
            // Apertura fatta da «Espandi tutto»: consuma il marchio e lascia stare i fratelli. ⚠️ Il `toggle`
            // arriva DOPO (è messo in coda), quindi una bandiera spenta in fondo alla funzione di gruppo
            // sarebbe già spenta quando l'evento arriva — misurato: «espandi tutto» ne apriva uno solo.
            if (d.__vipiBulk) { d.__vipiBulk = false; return; }
            if (!d.open) return;
            var parent = d.parentElement;
            if (!parent) return;
            // Chiudere un fratello che sta SOPRA accorcia la pagina sotto il puntatore: il blocco appena aperto
            // scivola in su. Misurato saltandoci da una voce dell'indice: la sezione bersaglio finiva a −249px,
            // cioè fuori schermo di sopra. Si compensa con uno scrollBy della differenza, come fa vipiDetails.
            var prima = d.getBoundingClientRect().top;
            parent.querySelectorAll(':scope > details.acc-block').forEach(function (other) {
                if (other !== d) { other.open = false; }
            });
            var dopo = d.getBoundingClientRect().top;
            if (dopo !== prima) window.scrollBy(0, dopo - prima);
        }, true);
    }

    // Espande/comprime tutto l'editor: le sezioni bespoke dell'aeroporto (details.ed-sec), i blocchi della vIPI
    // ACC e le card di sezione condivise (CollapsibleBlock → details.cb). Un helper solo: due farebbero due
    // comportamenti diversi per lo stesso tasto. Le aperture di gruppo sono MARCHIATE, altrimenti la
    // fisarmonica richiuderebbe subito i blocchi appena aperti.
    window.vipiEditorSections = function (open) {
        var want = !!open;
        document.querySelectorAll('details.ed-sec, .ed-layout details.acc-block, .ed-layout details.cb').forEach(function (d) {
            if (d.open === want) return;       // già com'è: nessun toggle, nessun marchio da consumare
            d.__vipiBulk = true;               // un marchio = un evento, quindi niente bandiere che restano su
            if (want) { d.setAttribute('open', ''); } else { d.removeAttribute('open'); }
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
        window.scrollTo({ top: y, behavior: vipiScorrimento() });
    };
})();
