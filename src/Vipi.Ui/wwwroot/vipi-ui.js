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

    function wireAnchors() {
        // Con <base href="/"> i link "#id" verrebbero risolti come "/#id" (→ home).
        // Intercettiamo: scroll diretto all'elemento, senza navigazione.
        document.querySelectorAll('a[href^="#"]').forEach(function (a) {
            a.onclick = function (e) {
                var id = a.getAttribute('href').slice(1);
                if (!id) return;
                var el = document.getElementById(id);
                if (!el) return;
                e.preventDefault();
                // Scroll con offset = altezza reale della top-bar sticky (così il titolo resta leggibile).
                var bar = document.querySelector('.topbar');
                var off = (bar ? bar.getBoundingClientRect().height : 62) + 14;
                var y = el.getBoundingClientRect().top + window.pageYOffset - off;
                window.scrollTo({ top: y, behavior: 'smooth' });
                var toc = a.closest('.toc');
                if (toc) { toc.querySelectorAll('a').forEach(function (x) { x.classList.remove('active'); }); a.classList.add('active'); }
                history.replaceState(null, '', location.pathname + location.search + '#' + id);
            };
        });
    }

    window.vipiWireUi = function () {
        document.querySelectorAll('.aor-block').forEach(wireAor);
        wireExpand();
        wireAnchors();
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.vipiApplyZoom && window.vipiApplyZoom();
        window.vipiWireUi();
    });
})();
