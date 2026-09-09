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

    // ⚠️ Qui stavano il Ctrl/Cmd+S dell'editor aeroporto («salva le sezioni modificate»), la guardia
    // `beforeunload` e i due ganci che le servivano (`vipiAirportEditorInit`, `vipiSetDirty`). Sono caduti
    // il 4 settembre 2026 con la cosa che proteggevano: quell'editor non accumula più niente, ogni gesto
    // scrive (carta 2026-09-04-aeroporto-porta-sola). Una guardia che non ha niente da guardare insegna
    // solo a ignorare gli avvisi.
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

    if (!window.__vipiEditorAnchors) {
        window.__vipiEditorAnchors = true;
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

    // ---- Barra di formattazione delle textarea markdown (RichTextArea.razor) -----------------------
    //
    // ⚠️ Ogni gesto finisce con un `change` SINTETICO. Blazor ascolta `onchange` con un listener delegato
    // sul documento: scrivere `el.value` da JS non fa scattare nessun evento da solo, e senza questa riga
    // il testo cambierebbe a schermo e NON tornerebbe mai nel modello — sparirebbe al primo re-render.
    function vipiMdFine(el, s, e) {
        el.focus();
        el.selectionStart = s;
        el.selectionEnd = e;
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Avvolge la selezione, o la sguscia se è già avvolta (il tasto è un interruttore, come in un editor
    // vero: premuto due volte torna indietro invece di impilare i marcatori).
    window.vipiMdWrap = function (el, pre, post) {
        if (!el) return;
        var s = el.selectionStart, e = el.selectionEnd, v = el.value;
        var dentro = v.substring(s, e);

        // Già avvolta DENTRO la selezione (**testo**) o FUORI (il cursore sta fra i marcatori)?
        if (dentro.length > pre.length + post.length &&
            dentro.slice(0, pre.length) === pre && dentro.slice(-post.length) === post) {
            var nudo = dentro.slice(pre.length, dentro.length - post.length);
            el.value = v.substring(0, s) + nudo + v.substring(e);
            return vipiMdFine(el, s, s + nudo.length);
        }
        if (v.substring(s - pre.length, s) === pre && v.substring(e, e + post.length) === post) {
            el.value = v.substring(0, s - pre.length) + dentro + v.substring(e + post.length);
            return vipiMdFine(el, s - pre.length, s - pre.length + dentro.length);
        }

        el.value = v.substring(0, s) + pre + dentro + post + v.substring(e);
        // Selezione vuota: il cursore va FRA i marcatori, pronto a scrivere. Con la selezione, resta sul
        // testo — così si può incalzare con un secondo tasto (grassetto E corsivo) senza riselezionare.
        vipiMdFine(el, s + pre.length, s + pre.length + dentro.length);
    };

    // Marca/smarca come elenco le righe TOCCATE dalla selezione (anche solo sfiorate: il cursore su una
    // riga basta). Se sono già tutte marcate dello stesso tipo, le smarca — stesso interruttore di sopra.
    window.vipiMdList = function (el, ordinato) {
        if (!el) return;
        var v = el.value;
        var ini = v.lastIndexOf('\n', Math.max(0, el.selectionStart - 1)) + 1;
        var fin = v.indexOf('\n', el.selectionEnd);
        if (fin < 0) fin = v.length;

        var righe = v.substring(ini, fin).split('\n');
        var puntata = /^[ \t]*[-*+•][ \t]+/;
        var numerata = /^[ \t]*\d{1,3}[.)][ \t]+/;
        var mia = ordinato ? numerata : puntata;

        var tutteMie = righe.every(function (r) { return r.trim() === '' || mia.test(r); });
        var n = 0;
        var nuove = righe.map(function (r) {
            if (r.trim() === '') return r;
            var nudo = r.replace(puntata, '').replace(numerata, '');
            if (tutteMie) return nudo;                        // erano già mie: le smarco
            n++;
            return (ordinato ? n + '. ' : '- ') + nudo;
        });

        var testo = nuove.join('\n');
        el.value = v.substring(0, ini) + testo + v.substring(fin);
        vipiMdFine(el, ini, ini + testo.length);
    };

    // Ctrl/Cmd+B/I/U dentro una textarea markdown. ⚠️ Delegato sul documento e NON legato al componente:
    // un `@onkeydown` di Blazor sarebbe un giro di rete a OGNI tasto battuto, su ogni campo dell'editor.
    // Qui il costo è zero finché non si preme la combinazione.
    // Il listener globale di sopra ignora di proposito i campi di testo, quindi non si pestano i piedi.
    if (!window.__vipiMdKeys) {
        window.__vipiMdKeys = true;
        document.addEventListener('keydown', function (e) {
            if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
            var el = e.target;
            // ⚠️ Si riconosce dall'INVOLUCRO (`.rta`), non da una classe propria sulla textarea: una classe
            // messa lì solo per farsi trovare dal JS sarebbe un campo con un vestito che nessun foglio
            // cuce — e c'è una prova che lo vieta, perché è così che un campo finisce coi colori del browser.
            if (!el || !el.matches || !el.matches('textarea.app-ta') || !el.closest('.rta')) return;
            var k = (e.key || '').toLowerCase();
            if (k === 'b') { e.preventDefault(); window.vipiMdWrap(el, '**', '**'); }
            else if (k === 'i') { e.preventDefault(); window.vipiMdWrap(el, '*', '*'); }
            else if (k === 'u') { e.preventDefault(); window.vipiMdWrap(el, '__', '__'); }
        });
    }

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
