// Interattività di consultazione (pagine SSR statiche): toggle settori AoR + selettore configurazioni.
// Ricollegato a ogni navigazione via 'enhancedload' (vedi App.razor).
(function () {
    function setSector(scope, key, on) {
        var chip = scope.querySelector('.aor-chip[data-sec="' + key + '"]');
        if (chip) chip.classList.toggle('on', on);
        scope.querySelectorAll('.sec[data-sec="' + key + '"], .lbl[data-sec="' + key + '"]').forEach(function (el) {
            el.classList.toggle('hidden', !on);
        });
    }

    function wireAor(scope) {
        // chip on/off
        scope.querySelectorAll('.aor-chip[data-sec]').forEach(function (chip) {
            chip.onclick = function () {
                setSector(scope, chip.getAttribute('data-sec'), !chip.classList.contains('on'));
            };
        });
        // tutti / nessuno
        scope.querySelectorAll('.aor-all[data-act]').forEach(function (a) {
            a.onclick = function () {
                var on = a.getAttribute('data-act') === 'all';
                scope.querySelectorAll('.aor-chip[data-sec]').forEach(function (chip) {
                    setSector(scope, chip.getAttribute('data-sec'), on);
                });
            };
        });
        // selettore configurazioni → evidenzia righe nella tabella configurazioni + setta i settori
        scope.querySelectorAll('.cfg-btn').forEach(function (btn) {
            btn.onclick = function () {
                scope.querySelectorAll('.cfg-btn').forEach(function (b) { b.classList.remove('on'); });
                btn.classList.add('on');
                applyConfig(btn.getAttribute('data-rows'), btn.getAttribute('data-secs'));
            };
        });
        var clr = scope.querySelector('.cfg-clear');
        if (clr) clr.onclick = function () {
            scope.querySelectorAll('.cfg-btn').forEach(function (b) { b.classList.remove('on'); });
            applyConfig('', null);
        };
    }

    function applyConfig(rowsCsv, secsCsv) {
        var table = document.getElementById('cfg-ops');
        if (table) {
            var rows = (rowsCsv || '').split(',').map(function (s) { return s.trim(); }).filter(Boolean);
            table.querySelectorAll('tr[data-r]').forEach(function (tr) {
                tr.classList.toggle('cfg-hl', rows.indexOf(tr.getAttribute('data-r')) >= 0);
            });
        }
        if (secsCsv !== null) {
            var aor = document.querySelector('.aor-block');
            if (aor) {
                var want = (secsCsv || '').split(',').map(function (s) { return s.trim(); }).filter(Boolean);
                aor.querySelectorAll('.aor-chip[data-sec]').forEach(function (chip) {
                    var key = chip.getAttribute('data-sec');
                    setSector(aor, key, want.indexOf(key) >= 0);
                });
            }
        }
    }

    function wireExpand() {
        // Espandi/Comprimi tutto su un blocco
        document.querySelectorAll('.exp-ctrl button').forEach(function (b) {
            b.onclick = function () {
                var blk = b.closest('.block'); if (!blk) return;
                blk.querySelectorAll('details').forEach(function (d) { d.open = b.getAttribute('data-exp') === 'open'; });
            };
        });
        // Espandi/Comprimi i flussi di una singola sezione
        document.querySelectorAll('.sub-exp a').forEach(function (a) {
            a.onclick = function (e) {
                e.preventDefault(); e.stopPropagation();
                var det = a.closest('details'); var open = a.getAttribute('data-exp') === 'open';
                if (open) det.open = true;
                det.querySelectorAll('details').forEach(function (d) { d.open = open; });
            };
        });
        // Espandi/Comprimi per gruppo (fino al prossimo .coord-group)
        document.querySelectorAll('.grp-exp a').forEach(function (a) {
            a.onclick = function (e) {
                e.preventDefault();
                var open = a.getAttribute('data-exp') === 'open';
                var n = a.closest('.coord-group') ? a.closest('.coord-group').nextElementSibling : null;
                while (n && !n.classList.contains('coord-group')) {
                    if (n.tagName === 'DETAILS') { n.open = open; n.querySelectorAll('details').forEach(function (d) { d.open = open; }); }
                    n = n.nextElementSibling;
                }
            };
        });
    }

    // Apre l'elemento (se <details>) e tutti i <details> che lo contengono, così un deep-link "#id" verso una
    // sezione collassata (Guida, editor) la mostra invece di atterrare su un pannello chiuso. Ritorna l'elemento.
    function openDetailsFor(el) {
        var d = el;
        while (d && d !== document.body) {
            if (d.tagName === 'DETAILS' && !d.open) d.open = true;
            d = d.parentElement;
        }
        return el;
    }

    // Al caricamento con un hash (es. arrivo da un "?" HelpHint in nuova scheda su /vsop/guida#editor-release):
    // apre la sezione target e scorre con l'offset della top-bar. Anche su hashchange nella stessa pagina.
    var hashLandingWired = false;
    function wireHashLanding() {
        function land() {
            var id = location.hash ? location.hash.slice(1) : '';
            if (!id) return;
            var el = document.getElementById(id);
            if (!el) return;
            openDetailsFor(el);
            var bar = document.querySelector('.topbar');
            var off = (bar ? bar.getBoundingClientRect().height : 62) + 14;
            // Ritardo minimo: lascia riflettere l'apertura del <details> nel layout prima di misurare.
            setTimeout(function () {
                var y = el.getBoundingClientRect().top + window.pageYOffset - off;
                window.scrollTo({ top: y, behavior: 'auto' });
            }, 30);
        }
        land();
        if (hashLandingWired) return;
        hashLandingWired = true;
        window.addEventListener('hashchange', land);
    }

    var anchorsWired = false;
    function wireAnchors() {
        // Con <base href="/"> i link "#id" verrebbero risolti come "/#id" (→ home).
        // Delegazione su document: cattura anche gli ancoraggi resi DOPO il load da Blazor (InteractiveServer).
        if (anchorsWired) return;
        anchorsWired = true;
        // Fase di CATTURA + stopImmediatePropagation: gira PRIMA dell'intercettore di navigazione di Blazor
        // (che altrimenti, vista la <base href="/">, risolverebbe "#id" come "/#id" e andrebbe in home).
        document.addEventListener('click', function (e) {
            var a = e.target && e.target.closest ? e.target.closest('a[href^="#"]') : null;
            if (!a) return;
            var id = a.getAttribute('href').slice(1);
            if (!id) return;
            var el = document.getElementById(id);
            if (!el) return;
            e.preventDefault();
            e.stopImmediatePropagation();
            openDetailsFor(el);   // se il target è una sezione collassata (Guida), aprila prima di scorrere
            // Scroll con offset = altezza reale della top-bar sticky (così il titolo resta leggibile).
            var bar = document.querySelector('.topbar');
            var off = (bar ? bar.getBoundingClientRect().height : 62) + 14;
            var y = el.getBoundingClientRect().top + window.pageYOffset - off;
            window.scrollTo({ top: y, behavior: 'smooth' });
            var toc = a.closest('.toc');
            if (toc) { toc.querySelectorAll('a').forEach(function (x) { x.classList.remove('active'); }); a.classList.add('active'); }
            history.replaceState(null, '', location.pathname + location.search + '#' + id);
        }, true);
    }

    // Sospende la persistenza del collasso: l'apertura in massa per la stampa non deve riscrivere le preferenze
    // dell'utente (vedi wirePrint).
    var suppressPersist = false;

    function wireCollapse() {
        // <details data-persist="key">: ricorda aperto/chiuso in localStorage tra le navigazioni.
        document.querySelectorAll('details[data-persist]').forEach(function (d) {
            if (d.dataset.persistWired) return;
            d.dataset.persistWired = '1';
            var key = 'vipi-collapse:' + d.getAttribute('data-persist');
            var saved = null;
            try { saved = localStorage.getItem(key); } catch (e) { }
            if (saved !== null) d.open = saved === '1';
            d.addEventListener('toggle', function () {
                if (suppressPersist) return;
                try { localStorage.setItem(key, d.open ? '1' : '0'); } catch (e) { }
            });
        });
    }

    var printWired = false;
    function wirePrint() {
        // Stampa: una sezione collassata (CollapsibleBlock, collasso persistito) resterebbe fuori dal foglio.
        // Apriamo tutti i <details> prima di stampare e ripristiniamo esattamente quelli che erano chiusi.
        // Il CSS d'autore da solo non basta: in Chrome il contenuto di un <details> chiuso è nascosto dallo
        // user-agent (content-visibility su ::details-content). Il foglio vipi-print.css tiene comunque le
        // regole di ripiego per i browser che non segnalano la stampa.
        if (printWired) return;
        printWired = true;
        var closed = [];

        function stampTime() {
            // L'intestazione PrintMeta porta l'ora di render lato server: qui la sostituiamo con quella reale di
            // stampa (UTC, stesso formato), così una pagina rimasta aperta a lungo non stampa un orario vecchio.
            var now = new Date().toISOString().slice(0, 16).replace('T', ' ') + 'Z';
            document.querySelectorAll('.print-meta [data-print-time]').forEach(function (el) {
                el.textContent = now;
            });
        }

        function expand() {
            stampTime();
            closed = [];
            suppressPersist = true;
            document.querySelectorAll('details:not([open])').forEach(function (d) {
                // Il `?` di aiuto e il tour non vanno in stampa: aprirli sposterebbe il layout per nulla.
                if (d.classList.contains('help-hint')) return;
                closed.push(d);
                d.open = true;
            });
            suppressPersist = false;
        }

        function restore() {
            suppressPersist = true;
            closed.forEach(function (d) { d.open = false; });
            closed = [];
            suppressPersist = false;
        }

        window.addEventListener('beforeprint', expand);
        window.addEventListener('afterprint', restore);
        // Safari non emette beforeprint/afterprint: il cambio di media 'print' è l'equivalente.
        var mq = window.matchMedia && window.matchMedia('print');
        if (mq && mq.addEventListener) {
            mq.addEventListener('change', function (e) { if (e.matches) { expand(); } else { restore(); } });
        }
    }

    // Espandi/comprimi tutti i <details> di uno scope. Bottone dentro un <details> → agisce sui discendenti di
    // quel nodo (il nodo resta aperto); bottone di sezione (fuori dai details) → l'intero blocco `.coord-wrap`.
    // onclick inline: funziona anche su pagine InteractiveServer, senza dipendere dal wiring a enhancedload.
    window.vipiDetails = function (btn, open) {
        var scope = btn.closest('details') || btn.closest('.coord-wrap');
        if (!scope) return;
        // Mantieni il bottone (quindi la sezione) alla stessa posizione a schermo: comprimendo, il contenuto
        // sopra si accorcia e la pagina scorrerebbe; compensa con uno scrollBy della differenza.
        var before = btn.getBoundingClientRect().top;
        scope.querySelectorAll('details').forEach(function (d) { d.open = open; });
        var after = btn.getBoundingClientRect().top;
        window.scrollBy(0, after - before);
    };

    // Porta in vista un elemento per id (usato dall'editor struttura: scroll al primo match di ricerca).
    window.vipiScrollToId = function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    var searchKeyWired = false;
    function wireSearchKey() {
        // "/" mette a fuoco la barra di ricerca in header (se non stai già digitando).
        if (searchKeyWired) return;
        searchKeyWired = true;
        document.addEventListener('keydown', function (e) {
            if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
            var t = e.target;
            if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;
            var input = document.querySelector('.top-search input');
            if (!input) return;
            e.preventDefault();
            input.focus();
            input.select();
        });
    }

    window.vipiWireUi = function () {
        document.querySelectorAll('.aor-block').forEach(wireAor);
        wireExpand();
        wireAnchors();
        wireCollapse();
        wireSearchKey();
        wirePrint();
        wireHashLanding();   // deep-link "#id" verso sezioni collassate (Guida) → apri + scorri
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.vipiApplyZoom && window.vipiApplyZoom();
        window.vipiWireUi();
    });
})();
