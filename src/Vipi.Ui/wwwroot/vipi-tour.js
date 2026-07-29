// Tour guidato per gli editor: overlay a step che evidenzia i controlli chiave (indice, salva, anteprima, pubblica).
// Generico e resiliente: gli step si agganciano agli elementi con [data-tour="chiave"]; quelli assenti sulla pagina
// vengono saltati, così lo stesso tour funziona su editor diversi (ACC/APP/aeroporto/vLOA). Nessuna libreria.
// Auto-parte UNA volta per utente (localStorage); si rilancia con ?tour=1 o window.vipiTour.start(true).
// Ricollegato a ogni navigazione via 'enhancedload' (vedi App.razor), come gli altri script del modulo.
(function () {
    var SEEN_KEY = 'vipi-tour:editor:v1';

    // Ordine e testo degli step. `sel` = elemento bersaglio; il primo visibile per chiave vince.
    var STEPS = [
        { sel: '[data-tour="toc"]',     title: 'Indice del documento', body: 'Salta a una sezione. Il pallino segnala le sezioni con modifiche non salvate.' },
        { sel: '[data-tour="save"]',    title: 'Salva tutto', body: 'Scrive le modifiche nella <b>bozza</b> (anche con Ctrl+S). Non è ancora pubblico.' },
        { sel: '[data-tour="preview"]', title: 'Anteprima bozza', body: 'Apri il documento come apparirà. Controlla sempre qui <b>prima</b> di pubblicare.' },
        { sel: '[data-tour="release"]', title: 'Pubblica', body: 'Rendi la bozza pubblica con una <b>release AIRAC</b>: subito o programmata a un ciclo.' }
    ];

    var state = null; // { steps:[{el,title,body}], i, nodes:{...} }

    function seen() { try { return localStorage.getItem(SEEN_KEY) === '1'; } catch (e) { return false; } }
    function markSeen() { try { localStorage.setItem(SEEN_KEY, '1'); } catch (e) { } }

    function visible(el) {
        if (!el) return false;
        var r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0;
    }

    function resolveSteps() {
        var out = [];
        STEPS.forEach(function (s) {
            var el = document.querySelector(s.sel);
            if (visible(el)) out.push({ el: el, title: s.title, body: s.body });
        });
        return out;
    }

    function buildChrome() {
        var overlay = document.createElement('div');
        overlay.className = 'vt-overlay';
        var spot = document.createElement('div');
        spot.className = 'vt-spot';
        var card = document.createElement('div');
        card.className = 'vt-card';
        card.setAttribute('role', 'dialog');
        card.setAttribute('aria-live', 'polite');
        card.innerHTML =
            '<button type="button" class="vt-close" aria-label="Chiudi il tour" title="Chiudi (Esc)">✕</button>' +
            '<div class="vt-step"></div>' +
            '<h4 class="vt-title"></h4>' +
            '<div class="vt-body"></div>' +
            '<div class="vt-actions">' +
              '<button type="button" class="vt-skip">Salta</button>' +
              '<span class="vt-spacer"></span>' +
              '<button type="button" class="vt-prev btn ghost">Indietro</button>' +
              '<button type="button" class="vt-next btn primary">Avanti</button>' +
            '</div>';
        document.body.appendChild(overlay);
        document.body.appendChild(spot);
        document.body.appendChild(card);
        return {
            overlay: overlay, spot: spot, card: card,
            step: card.querySelector('.vt-step'), title: card.querySelector('.vt-title'),
            body: card.querySelector('.vt-body'), prev: card.querySelector('.vt-prev'),
            next: card.querySelector('.vt-next'), skip: card.querySelector('.vt-skip'),
            close: card.querySelector('.vt-close')
        };
    }

    function place() {
        var s = state.steps[state.i], n = state.nodes;
        var el = s.el;
        el.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'nearest' });
        // Spot e card sono position:fixed → coordinate VIEWPORT dirette (getBoundingClientRect), senza pageYOffset:
        // robusto con rail/TOC sticky, elementi più alti del viewport e zoom pagina (zoom su <html>).
        var r = el.getBoundingClientRect();
        var vw = window.innerWidth, vh = window.innerHeight, pad = 6;
        // Riquadro luminoso attorno al bersaglio (il box-shadow enorme oscura il resto della pagina).
        n.spot.style.top = (r.top - pad) + 'px';
        n.spot.style.left = (r.left - pad) + 'px';
        n.spot.style.width = (r.width + pad * 2) + 'px';
        n.spot.style.height = (r.height + pad * 2) + 'px';
        // Card: sotto se c'è spazio, sopra se c'è sopra, altrimenti sovrapposta — SEMPRE dentro il viewport.
        var cw = n.card.offsetWidth, ch = n.card.offsetHeight, top;
        if (vh - r.bottom > ch + 20) top = r.bottom + 12;
        else if (r.top > ch + 20) top = r.top - 12 - ch;
        else top = r.top;
        top = Math.max(12, Math.min(top, vh - ch - 12));
        var left = Math.min(Math.max(12, r.left), vw - cw - 12);
        n.card.style.top = top + 'px';
        n.card.style.left = left + 'px';
    }

    function render() {
        var s = state.steps[state.i], n = state.nodes, total = state.steps.length;
        n.step.textContent = 'Passo ' + (state.i + 1) + ' di ' + total;
        n.title.textContent = s.title;
        n.body.innerHTML = s.body;
        n.prev.style.visibility = state.i === 0 ? 'hidden' : 'visible';
        n.next.textContent = state.i === total - 1 ? 'Fine' : 'Avanti';
        place();
    }

    function stop() {
        if (!state) return;
        window.removeEventListener('resize', render);
        window.removeEventListener('scroll', onScroll, true);
        document.removeEventListener('keydown', onKey, true);
        document.removeEventListener('click', onDocClick, true);
        [state.nodes.overlay, state.nodes.spot, state.nodes.card].forEach(function (x) { if (x && x.parentNode) x.parentNode.removeChild(x); });
        state = null;
        markSeen();
    }

    function next() { if (state.i >= state.steps.length - 1) stop(); else { state.i++; render(); } }
    function prev() { if (state.i > 0) { state.i--; render(); } }

    var scrollRaf = 0;
    function onScroll() { if (scrollRaf) return; scrollRaf = requestAnimationFrame(function () { scrollRaf = 0; if (state) place(); }); }
    function onKey(e) {
        if (!state) return;
        if (e.key === 'Escape') { e.preventDefault(); stop(); return; }
        // Non dirottare le frecce/Invio mentre l'utente scrive in un campo.
        var t = e.target;
        if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;
        if (e.key === 'ArrowRight' || e.key === 'Enter') { e.preventDefault(); next(); }
        else if (e.key === 'ArrowLeft') { e.preventDefault(); prev(); }
    }
    // Un click ovunque FUORI dalla card chiude il tour SENZA bloccarlo: il click prosegue verso la pagina
    // (l'overlay è pointer-events:none), così il tour non intrappola mai l'utente.
    function onDocClick(e) {
        if (!state) return;
        if (state.nodes.card.contains(e.target)) return;   // click sui controlli del tour: gestiti a parte
        stop();
    }

    var autoStarted = false;   // guardia di sessione: evita che il tour riparta a ogni re-render (anche se localStorage è bloccato)

    function start(force) {
        if (state) return;                 // già in corso
        if (!force && (seen() || autoStarted)) return;
        var steps = resolveSteps();
        if (steps.length === 0) return;     // nessun controllo editor sulla pagina → non è un editor
        autoStarted = true;
        var nodes = buildChrome();
        state = { steps: steps, i: 0, nodes: nodes };
        nodes.next.addEventListener('click', next);
        nodes.prev.addEventListener('click', prev);
        nodes.skip.addEventListener('click', stop);
        nodes.close.addEventListener('click', stop);
        window.addEventListener('resize', render);
        window.addEventListener('scroll', onScroll, true);
        document.addEventListener('keydown', onKey, true);
        // Su next tick: non catturare l'eventuale click che ha AVVIATO il tour (es. futuro bottone "rivedi").
        setTimeout(function () { document.addEventListener('click', onDocClick, true); }, 0);
        render();
    }

    window.vipiTour = { start: start };

    // Aggancio a ogni (ri)render: ?tour=1 forza il tour; altrimenti auto-parte una sola volta (seen + guardia sessione).
    window.vipiMaybeTour = function () {
        var force = /[?&]tour=1(&|$)/.test(location.search);
        if (!force && (seen() || autoStarted)) return;
        // Ritardo: lascia montare i controlli InteractiveServer prima di cercarli.
        setTimeout(function () { start(force); }, force ? 200 : 900);
    };

    document.addEventListener('DOMContentLoaded', function () { window.vipiMaybeTour(); });
})();
