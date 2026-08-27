// Interattività di consultazione (pagine SSR statiche): toggle settori AoR + selettore configurazioni.
// Ricollegato a ogni navigazione via 'enhancedload' (vedi App.razor).

// Il verso dello scorrimento, secondo chi guarda: 'smooth' o 'auto'.
//
// ⚠️ Non è una rifinitura. Chi ha chiesto al sistema di ridurre le animazioni lo ha fatto per un motivo —
// per una parte delle persone un contenuto che scorre da solo dà nausea o innesca un'emicrania — e il
// foglio di stile da solo non ci arriva: `behavior:'smooth'` è una stringa scritta nel JS, e nessuna
// media query la spegne. Il pendant CSS sta in fondo a vipi-theme.css.
//
// Fuori dall'IIFE e in cima al file: la chiamano anche vipi-aor.js e vipi-editor.js. Si rilegge a ogni
// chiamata invece di ricordarsela: la preferenza si può cambiare mentre la pagina è aperta.
window.vipiScorrimento = function () {
    try {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches
            ? 'auto' : 'smooth';
    } catch (e) { return 'smooth'; }
};

(function () {
    // Stato acceso/spento di una chip: la classe `.on` per l'occhio, `aria-pressed` per tutto il resto.
    // Gemella di quella in vipi-aor.js — le chip sono le stesse, le pilotano due file diversi a seconda che
    // dietro ci sia una mappa Leaflet o l'SVG di ripiego.
    function segna(el, on) {
        if (!el) return;
        el.classList.toggle('on', !!on);
        el.setAttribute('aria-pressed', on ? 'true' : 'false');
    }

    function setSector(scope, key, on) {
        segna(scope.querySelector('.aor-chip[data-sec="' + key + '"]'), on);
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
                scope.querySelectorAll('.cfg-btn').forEach(function (b) { segna(b, false); });
                segna(btn, true);
                applyConfig(btn.getAttribute('data-rows'), btn.getAttribute('data-secs'));
            };
        });
        var clr = scope.querySelector('.cfg-clear');
        if (clr) clr.onclick = function () {
            scope.querySelectorAll('.cfg-btn').forEach(function (b) { segna(b, false); });
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

    // Porta un elemento a schermo sotto la top-bar. ⚠️ Si MISURA DOPO che il layout si è assestato: aprire un
    // <details> fa scattare un `toggle`, che è messo in coda e può cambiare l'altezza di quello che sta SOPRA —
    // sull'editor ACC apre un blocco e la fisarmonica ne chiude un altro. Misurando subito, il bersaglio finiva
    // a −249px (misurato, saltando dall'indice a una sezione del blocco chiuso). Due giri di rAF: il primo
    // lascia svuotare la coda dei task, il secondo misura sul layout definitivo.
    function scrollAfterLayout(el, behavior) {
        function porta(behavior) {
            var bar = document.querySelector('.topbar');
            var off = (bar ? bar.getBoundingClientRect().height : 62) + 14;
            var delta = el.getBoundingClientRect().top - off;
            if (Math.abs(delta) < 4) return true;              // già al posto giusto: non muovere niente
            window.scrollTo({ top: window.pageYOffset + delta, behavior: behavior || 'auto' });
            return false;
        }
        // Due giri di rAF: il primo lascia svuotare la coda dei task (i `toggle` messi in coda dall'apertura),
        // il secondo misura sul layout definitivo. Poi una scaletta di correzioni: ciò che sta sopra può ancora
        // cambiare altezza — una mappa AoR che si inizializza all'apertura del suo <details>, un render Blazor
        // in arrivo. Ogni giro si ferma da solo appena il bersaglio è al suo posto. ⚠️ Con una correzione sola
        // il bersaglio atterrava a 25px dal bordo, cioè sotto la top-bar: misurato, ne servono due.
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                if (porta(behavior)) return;
                var ritardi = [120, 380, 800];
                (function ritenta(i) {
                    if (i >= ritardi.length) return;
                    setTimeout(function () { if (!porta('auto')) ritenta(i + 1); }, ritardi[i]);
                })(0);
            });
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

    // Al caricamento con un hash (es. arrivo da un "?" HelpHint in nuova scheda su /services/vsop/guide#editor-release):
    // apre la sezione target e scorre con l'offset della top-bar. Anche su hashchange nella stessa pagina.
    var hashLandingWired = false;
    function wireHashLanding() {
        function land() {
            var id = location.hash ? location.hash.slice(1) : '';
            if (!id) return;
            var el = document.getElementById(id);
            if (!el) return;
            openDetailsFor(el);
            scrollAfterLayout(el, 'auto');
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
            scrollAfterLayout(el, vipiScorrimento());
            var toc = a.closest('.toc');
            if (toc) { toc.querySelectorAll('a').forEach(function (x) { x.classList.remove('active'); }); a.classList.add('active'); }
            history.replaceState(null, '', location.pathname + location.search + '#' + id);
        }, true);
    }

    // Il menu «+ Blocco» degli editor si comporta come un menu: si chiude al clic su una voce e al clic fuori.
    //
    // ⚠️ `open` di un <details> e' stato del DOM, non del markup: dopo che Blazor ha aggiunto il blocco e
    // ri-renderizzato, il menu resterebbe spalancato sotto la sezione. E chiuderlo dal C# vorrebbe dire
    // passare per JS a ogni clic. Una delega sola, in cattura, vale per tutti gli editor e per le sezioni
    // che ancora non esistono.
    var blockMenuWired = false;
    function wireBlockMenu() {
        if (blockMenuWired) return;
        blockMenuWired = true;
        document.addEventListener('click', function (e) {
            var dentro = e.target && e.target.closest ? e.target.closest('details.blk-add') : null;
            document.querySelectorAll('details.blk-add[open]').forEach(function (d) {
                // Il clic sul proprio <summary> lo gestisce il browser (apre/chiude): non toccarlo, o si
                // riaprirebbe subito dopo essere stato chiuso.
                if (d === dentro && e.target.closest('summary') === d.querySelector('summary')) return;
                d.open = false;
            });
        }, true);
    }

    // Il menu-sezioni degli editor ACCETTA il rilascio: `preventDefault` sul `dragover` delle voci.
    //
    // ⚠️ Perche' non lo fa Blazor. `EditorToc.razor` scriveva `@ondragover:preventDefault="true"`, e quel
    // modificatore NON basta da solo: Blazor installa il proprio listener globale per un evento soltanto
    // quando un componente vi registra un GESTORE, e per `dragover` non ce n'era nessuno — solo il
    // modificatore, che restava lettera morta. Misurato il 27 agosto 2026 sull'editor ACC: `dragstart` e
    // `dragenter` arrivavano (la voce si illuminava davvero), `dragover` arrivava con
    // `defaultPrevented=false`, e il `drop` non arrivava MAI. Senza un bersaglio che accetta, il browser
    // annulla il trascinamento — nessun errore, nessun segno: il gesto semplicemente non faceva niente.
    //
    // La strada in-framework sarebbe un gestore `@ondragover` finto, ma `dragover` scatta a ogni movimento
    // del mouse: sarebbe un giro sul circuito e un re-render del menu una decina di volte al secondo,
    // proprio durante il gesto. Qui basta un listener, installato una volta (stessa scelta di wireBlockMenu
    // e delle chip AoR), e il `drop` resta di Blazor.
    var tocDropWired = false;
    function wireTocDrop() {
        if (tocDropWired) return;
        tocDropWired = true;
        document.addEventListener('dragover', function (e) {
            if (e.target && e.target.closest && e.target.closest('.toc-drag a[draggable="true"]')) e.preventDefault();
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
        // Chrome segnala la stampa DUE volte (beforeprint e il passaggio a media 'print'): senza questa guardia
        // la seconda apertura ripartirebbe da una pagina già espansa, raccogliendo un elenco vuoto — e dopo la
        // stampa le sezioni che l'utente aveva chiuso resterebbero aperte (verificato live).
        var expanded = false;

        function stampTime() {
            // L'intestazione PrintMeta porta l'ora di render lato server: qui la sostituiamo con quella reale di
            // stampa (UTC, stesso formato), così una pagina rimasta aperta a lungo non stampa un orario vecchio.
            var now = new Date().toISOString().slice(0, 16).replace('T', ' ') + 'Z';
            document.querySelectorAll('.print-meta [data-print-time]').forEach(function (el) {
                el.textContent = now;
            });
        }

        // Mappe AoR: a schermo il contenitore è alto 340px, su A4 sono ~90 mm di cui gran parte mare. Le
        // riduciamo per la stampa. Non basta il CSS: Leaflet tiene la propria dimensione in memoria, quindi
        // cambiare l'altezza da foglio di stile RITAGLIA la mappa invece di riadattarla. Serve invalidateSize()
        // + il refit sui settori accesi, che vipi-aor.js espone su `_leafletMap` / `_aorRefit`.
        // Due misure: la mappa AoR principale del documento resta leggibile (200px ≈ 53 mm), le miniature
        // per-area (.area-map, una per area regolamentata: su una ACC sono decine) scendono a 130px ≈ 34 mm.
        // Solo verso il BASSO: una vIPI ACC ha mappe-area già a 190px e portarle alla misura della principale
        // le ingrandirebbe, allungando il documento invece di accorciarlo (preso in questo modo alla prima
        // verifica: 34 pagine prima, 34 dopo).
        var PRINT_MAP_H = 260, PRINT_AREA_MAP_H = 130;

        // Larghezza della cornice della mappa AoR principale, dedotta dalle PROPORZIONI dell'area inquadrata.
        // Perché serve: `fitBounds` sceglie lo zoom che fa stare i bounds in ENTRAMBE le dimensioni. In una
        // cornice larga e bassa (703 × 200) un AoR alto e stretto come LIBB è limitato dall'altezza, quindi lo
        // zoom scende e il foglio esce con mezzo Mediterraneo attorno a un poligono minuscolo. Dando alla
        // cornice la forma dell'AoR, il poligono la riempie; il margine auto la centra nel foglio.
        function frameWidth(el, m, h) {
            var b = el._aorBounds;
            if (!b || !m.project) return null;
            var z = 6;   // zoom qualsiasi: serve solo il RAPPORTO fra le due proiezioni Mercator
            var nw = m.project(b.getNorthWest(), z), se = m.project(b.getSouthEast(), z);
            var dy = Math.abs(se.y - nw.y);
            if (dy < 1) return null;
            var w = Math.round(h * (Math.abs(se.x - nw.x) / dy)) + 30;   // +30 = margine attorno al poligono
            var max = el.parentElement ? el.parentElement.clientWidth : w;
            return Math.max(170, Math.min(w, max || w));
        }

        function resizeMaps(toPrint) {
            document.querySelectorAll('.aor-leaflet').forEach(function (el) {
                var m = el._leafletMap;
                if (!m) return;   // fallback SVG (nessun Leaflet): scala già da sé
                var isArea = el.classList.contains('area-map');
                if (toPrint) {
                    // Altezza calcolata, non il rettangolo: con lo zoom di pagina attivo il rect è scalato, e
                    // 'beforeprint' scatta prima che il media passi a print (quindi prima del reset dello zoom).
                    var target = isArea ? PRINT_AREA_MAP_H : PRINT_MAP_H;
                    var now = parseFloat(getComputedStyle(el).height) || 0;
                    // Le miniature per-area vivono in una griglia accanto al testo: si toccano solo in altezza.
                    if (isArea && now <= target) return;
                    // Misure da ripristinare sull'elemento, non in un array indicizzato che si disallineerebbe
                    // se Blazor rirenderizzasse la pagina fra apertura e ripristino.
                    el._printPrevH = el.style.height;
                    el.style.height = target + 'px';
                    if (!isArea) {
                        var w = frameWidth(el, m, target);
                        if (w) {
                            el._printPrevW = [el.style.width, el.style.margin];
                            el.style.width = w + 'px';
                            el.style.margin = '0 auto';
                        }
                    }
                } else {
                    if (el._printPrevH === undefined) return;
                    el.style.height = el._printPrevH;
                    el._printPrevH = undefined;
                    if (el._printPrevW) {
                        el.style.width = el._printPrevW[0];
                        el.style.margin = el._printPrevW[1];
                        el._printPrevW = undefined;
                    }
                }
                m.invalidateSize(false);
                if (el._aorRefit) el._aorRefit();
            });
        }

        function expand() {
            if (expanded) return;
            expanded = true;
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
            // Le immagini editoriali nascono `loading=lazy`: quelle sotto la piega non sono ancora state scaricate
            // e in stampa uscirebbero come riquadri vuoti (visto misurando: 2x2 px). Si passa a eager prima che il
            // browser fotografi la pagina. Idempotente: al secondo giro non c'è più nessun lazy da convertire.
            document.querySelectorAll('.doc-img img[loading="lazy"]').forEach(function (img) {
                img.loading = 'eager';
                if (!img.complete && img.decode) { img.decode().catch(function () { }); }
            });
            // Dopo l'apertura: una mappa dentro un <details> chiuso ha dimensione zero e invalidateSize() non
            // avrebbe niente da misurare.
            resizeMaps(true);
        }

        function restore() {
            if (!expanded) return;
            expanded = false;
            resizeMaps(false);
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
    // ---- Quota di una fascia appiccicata, MISURATA ----
    // Chi sta sotto una testata appiccicata (es. il `thead` di una tabella) deve saperne l'altezza per non
    // finirle sotto. In CSS non è esprimibile: quell'altezza cambia da sola — un messaggio che compare, la
    // riga che va a capo su schermi stretti. Qui si misura e si scrive in una variabile CSS.
    // La variabile si mette sull'AMBITO (di norma il `.wrap` della pagina), non su <html>: cambiando pagina
    // l'elemento sparisce e con lui il valore, invece di restare buono per una pagina che non c'entra.
    var stickyOffsets = [];

    function measureStickyOffset(t) {
        var scope = t.scope ? document.querySelector(t.scope) : document.documentElement;
        if (!scope) return;
        var el = document.querySelector(t.sel);
        if (!el) { scope.style.removeProperty('--' + t.varName); return; }
        // Il nodo può essere stato ricreato (navigazione fra pagine): l'osservatore va riagganciato.
        if (t.el !== el) {
            if (t.ro) { t.ro.disconnect(); }
            t.el = el;
            if (window.ResizeObserver) {
                t.ro = new ResizeObserver(function () { measureStickyOffset(t); });
                t.ro.observe(el);
            }
        }
        // Per difetto, non per eccesso: un pixel di sovrapposizione non si vede (la fascia sta sopra), un pixel
        // di buco lascia passare le righe — ed è esattamente ciò che si stava correggendo.
        var h = Math.floor(el.getBoundingClientRect().height / rootZoom());
        scope.style.setProperty('--' + t.varName, h + 'px');
    }

    window.vipiStickyOffset = function (selector, varName, scopeSelector) {
        var t = stickyOffsets.filter(function (x) { return x.sel === selector && x.varName === varName; })[0];
        if (!t) { t = { sel: selector, varName: varName, scope: scopeSelector || null, el: null, ro: null }; stickyOffsets.push(t); }
        measureStickyOffset(t);
    };

    window.addEventListener('resize', function () { stickyOffsets.forEach(measureStickyOffset); });

    // ---- I «?» si aprono dove c'e' posto ----
    // Il popover di HelpHint nasce agganciato a sinistra del suo «?». Va bene finche' il «?» sta a sinistra:
    // quello della barra del lock sta all'ESTREMA DESTRA della testata, e il popover finiva 210px fuori
    // schermo — misurato, uguale su tutte e tre le pagine che montano EditLockBar.
    // Non e' esprimibile in CSS: dipende da dove si trova il «?» in quel momento, e la barra si sposta con la
    // larghezza della finestra. Si misura all'apertura e si ribalta, come farebbe un menu.
    // ⚠️ Classi PROPRIE (`help-flip`, `help-up`): la classe `left` puo' averla messa chi ha scritto la pagina,
    // e toglierla qui gli cancellerebbe una decisione presa a mano.
    function placeHelpPop(d) {
        var pop = d.querySelector('.help-pop');
        if (!pop) { return; }
        d.classList.remove('help-flip');
        d.classList.remove('help-up');
        pop.style.removeProperty('margin-left');
        if (!d.open) { return; }

        var vw = document.documentElement.clientWidth;
        var vh = document.documentElement.clientHeight;
        var r = pop.getBoundingClientRect();

        // Fuori a destra → si apre verso sinistra. `getBoundingClientRect` e `clientWidth` parlano la stessa
        // lingua (pixel di finestra) anche sotto zoom, quindi il confronto regge; ciò che si SCRIVE invece va
        // in unita' di layout — vedi rootZoom.
        if (r.right > vw - 8) {
            d.classList.add('help-flip');
            r = pop.getBoundingClientRect();
        }
        // Se anche ribaltato esce a sinistra (popover piu' largo dello spazio), lo si riporta dentro.
        if (r.left < 8) {
            pop.style.marginLeft = Math.round((8 - r.left) / rootZoom()) + 'px';
            r = pop.getBoundingClientRect();
        }
        // Fuori sotto → si apre verso l'alto, ma solo se sopra c'e' piu' spazio: sotto una testata appiccicata
        // ribaltare in alto vorrebbe dire finire sotto la testata, che e' peggio del bordo dello schermo.
        // ⚠️ Solo se il «?» e' DAVVERO a schermo: aperto da codice mentre sta mille pixel piu' in giu', "sotto"
        // e "sopra" non vogliono dire niente e si finirebbe per ribaltare al contrario quello che l'utente
        // vedra' quando ci arrivera' scorrendo.
        var dr = d.getBoundingClientRect();
        var aVista = dr.bottom > 0 && dr.top < vh;
        if (r.bottom > vh - 8 && aVista && dr.top > r.height + 14) { d.classList.add('help-up'); }
    }

    // `toggle` non fa bolla: si ascolta in fase di CATTURA, così un solo gestore vale per tutti i «?» della
    // pagina, compresi quelli che Blazor disegnera' fra un minuto.
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (d && d.classList && d.classList.contains('help-hint')) { placeHelpPop(d); }
    }, true);

    // La finestra cambia larghezza mentre un «?» e' aperto: il posto giusto puo' essere cambiato.
    window.addEventListener('resize', function () {
        var aperti = document.querySelectorAll('details.help-hint[open]');
        for (var i = 0; i < aperti.length; i++) { placeHelpPop(aperti[i]); }
    });

    window.vipiScrollToId = function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.scrollIntoView({ behavior: vipiScorrimento(), block: 'center' });
    };

    // Porta in vista un pannello di editing quando il suo PIEDE non lo è. Il bersaglio è il piede e non il
    // riquadro: un pannello che comincia a schermo ma finisce sotto la piega ha il Salva fuori campo, ed è
    // esattamente il caso misurato (pannello a 757 px, alto 906, viewport 1000 → Salva a 1609).
    // Serve a schermo stretto, dove la griglia lista+pannello collassa a una colonna e il pannello sta dopo
    // tutta la lista, e serve anche a schermo largo con la pagina in cima. Se il piede è già visibile non
    // muove niente: uno scorrimento non richiesto su una pagina ferma è peggio del problema che risolve.
    // Il pannello è STICKY: scorrere la pagina sposta anche lui, quindi una correzione sola non basta e
    // scrollIntoView manca il bersaglio (misurato: lasciava il piede fuori di 15 px). Due o tre passate
    // convergono, e se il piede è già a posto la prima esce subito senza muovere niente.
    // Adatta un riquadro allo spazio che resta sotto di lui: altezza = fondo dello schermo meno dove comincia.
    //
    // Serve alle pagine a colonne indipendenti (editor trasferimenti), dove ogni colonna deve scorrere per conto
    // proprio DENTRO lo schermo e la pagina non deve scorrere affatto. In CSS puro non si esprime: un
    // calc(100vh - N) richiede di sapere N, e N è tutto ciò che sta sopra — barra dell'applicazione, briciole,
    // testata, barra ACC, barra dei filtri, e l'avviso del lock che compare e sparisce. Misurato a schermo il
    // valore vero era 398 px dove la stima diceva 250: la pagina scorreva di 148.
    //
    // Si rimisura a ogni chiamata (l'editor la chiama a ogni render) e al ridimensionamento della finestra.
    // Sotto la soglia in cui la griglia collassa l'altezza fissa va TOLTA: lì il riquadro sta dentro una pagina
    // che scorre, e bloccarlo creerebbe due barre di scorrimento annidate.
    // ---- Zoom di pagina: due unità di misura diverse ----
    // `vipi-zoom.js` applica `zoom` su <html>. Da lì in poi convivono DUE spazi: `getBoundingClientRect` (e
    // `window.innerHeight`) parlano in pixel di finestra, mentre tutto ciò che si SCRIVE in CSS — `top:`,
    // `height:` — è in unità di layout, cioè pixel di finestra diviso lo zoom. Misurare in uno e scrivere
    // nell'altro è il difetto che faceva comparire una striscia di righe sopra l'intestazione appiccicata
    // (a zoom 1.2 erano 17px; a 0.8 l'intestazione finiva sotto la fascia).
    function rootZoom() {
        var v = getComputedStyle(document.documentElement).zoom;
        var z = parseFloat(v);
        if (typeof v === 'string' && v.indexOf('%') >= 0) { z = z / 100; }
        return !z || isNaN(z) || z <= 0 ? 1 : z;
    }

    var fitMin = 320;          // sotto questa altezza il riquadro è inutilizzabile: meglio far scorrere la pagina
    var fitTargets = [];

    // ⚠️ La misura vale fin dove arriva il RIQUADRO: quello che gli sta sotto non lo vede. Su /services/vsop/admin/audit
    // erano i 18px di padding del `.wrap` e si sono chiusi nel foglio di stile; dove sotto c'è invece
    // CONTENUTO — le due colonne chiuse in fondo a «I miei incarichi» — il foglio non basta, perché
    // quell'altezza dipende da quante colonne chiuse ci sono. `reserveSel` è la risposta: gli elementi che
    // indica si tolgono dallo spazio disponibile. Facoltativo: chi non lo passa si comporta esattamente come
    // prima.
    function riserva(reserveSel) {
        if (!reserveSel) return 0;
        var tot = 0;
        var nodi = document.querySelectorAll(reserveSel);
        for (var i = 0; i < nodi.length; i++) {
            var r = nodi[i].getBoundingClientRect();
            if (r.height <= 0) continue;
            var st = getComputedStyle(nodi[i]);
            tot += r.height + (parseFloat(st.marginTop) || 0) * rootZoom() + (parseFloat(st.marginBottom) || 0) * rootZoom();
        }
        return tot;
    }

    function fitOne(sel, collapseBelow, cap, reserveSel, cssVar) {
        var el = document.querySelector(sel);
        if (!el) return;
        if (cssVar) {
            if (window.innerWidth <= collapseBelow) { el.style.removeProperty(cssVar); return; }
            var topV = el.getBoundingClientRect().top + window.pageYOffset - document.documentElement.scrollTop;
            // ⚠️ Lo spazio si divide per le RIGHE della griglia, e le righe si contano — non si sanno.
            // `repeat(auto-fit, minmax(...))` manda i figli a capo quando la finestra si stringe: dando a
            // ognuno l'altezza piena, due righe di colonne ne occuperebbero il doppio e la promessa «sta in
            // una schermata» salterebbe proprio dove serve di piu'. Le righe si riconoscono dall'offsetTop.
            var cime = [], k;
            for (k = 0; k < el.children.length; k++) {
                var t = Math.round(el.children[k].offsetTop);
                if (cime.indexOf(t) < 0) cime.push(t);
            }
            var righe = Math.max(1, cime.length);
            var gapV = parseFloat(getComputedStyle(el).rowGap) || 0;
            var liberoV = (window.innerHeight - topV - riserva(reserveSel)) / rootZoom() - 18;
            // floor e non round: arrotondando per eccesso il riquadro sfora di 1px, e 1px di scorrimento
            // e' comunque una barra di scorrimento.
            var hV = Math.floor((liberoV - (righe - 1) * gapV) / righe);
            // fitMin vale per RIGA: sotto quella soglia il riquadro e' inutilizzabile e si lascia scorrere
            // la pagina, che e' il comportamento giusto su schermo basso o con molte righe di colonne.
            if (hV >= fitMin) el.style.setProperty(cssVar, hV + 'px'); else el.style.removeProperty(cssVar);
            return;
        }
        var prop = cap ? 'maxHeight' : 'height';
        var altra = cap ? 'height' : 'maxHeight';
        el.style[altra] = '';
        if (window.innerWidth <= collapseBelow) { el.style[prop] = ''; return; }
        var top = el.getBoundingClientRect().top + window.pageYOffset - document.documentElement.scrollTop;
        // Lo spazio che avanza si misura in pixel di finestra e si SCRIVE in unità di layout: sotto zoom i due
        // numeri non coincidono (vedi rootZoom). I 18px di respiro restano unità di layout, come li ha pensati
        // il foglio di stile.
        var h = Math.round((window.innerHeight - top - riserva(reserveSel)) / rootZoom() - 18);
        el.style[prop] = h >= fitMin ? h + 'px' : '';
    }

    window.vipiFitViewport = function (selector, collapseBelow, reserveSel) {
        var below = collapseBelow || 0;
        if (!fitTargets.some(function (t) { return t.sel === selector; })) fitTargets.push({ sel: selector, below: below, res: reserveSel });
        fitOne(selector, below, false, reserveSel);
    };

    // Come vipiFitViewport, ma scrive `max-height` invece di `height`: il riquadro sta alto quanto il suo
    // contenuto e si accorcia SOLO quando non ci starebbe.
    //
    // ⚠️ Quale delle due serve dipende da cosa c'è dentro, e la differenza si vede a occhio. `height` è giusto
    // dove il contenuto è più alto dello schermo per mestiere (il registro di audit, l'elenco aeroporti): lì
    // stirare il riquadro E far scorrere l'interno è tutto guadagno. È sbagliato dove il contenuto è corto e
    // FISSO: su /services/vsop/admin/sources le sei righe lasciavano mezzo riquadro di bianco perché il riquadro era
    // stato stirato a tutto lo schermo. «La pagina non scorre» non è l'obiettivo: l'obiettivo è che ciò che si
    // guarda stia a schermo, e con `max-height` lo si ottiene senza inventare vuoto.
    window.vipiCapViewport = function (selector, collapseBelow, reserveSel) {
        var below = collapseBelow || 0;
        if (!fitTargets.some(function (t) { return t.sel === selector; })) fitTargets.push({ sel: selector, below: below, cap: true, res: reserveSel });
        fitOne(selector, below, true, reserveSel);
    };

    // Come vipiCapViewport, ma scrive lo spazio disponibile in una CUSTOM PROPERTY (`--vipi-inner-h`) invece
    // che sull'elemento.
    //
    // ⚠️ Serve perché le altre due non sanno fare questo caso: un contenitore a GRIGLIA con `max-height` non
    // rimpicciolisce i suoi figli — le colonne restano alte quanto il loro contenuto e il riquadro ritaglia
    // invece di far scorrere. La misura va presa sul contenitore (che sa dov'è la sua cima) e APPLICATA ai
    // figli, e in CSS l'unico modo di passarla è una variabile. La usano le colonne dei coordinamenti live:
    // `#xl-cols` riceve la variabile, `.xl-krows` ci mette il proprio `max-height`.
    window.vipiCapInner = function (selector, collapseBelow, reserveSel) {
        var below = collapseBelow || 0;
        var v = '--vipi-inner-h';
        if (!fitTargets.some(function (t) { return t.sel === selector && t.cssVar === v; }))
            fitTargets.push({ sel: selector, below: below, res: reserveSel, cssVar: v });
        fitOne(selector, below, false, reserveSel, v);
    };

    function fitAll() { fitTargets.forEach(function (t) { fitOne(t.sel, t.below, t.cap, t.res, t.cssVar); }); }

    window.addEventListener('resize', fitAll);
    // ⚠️ Aprire o chiudere un <details> sposta ciò che sta SOTTO senza passare da un resize: chi si misura
    // dallo spazio che resta va rifatto. `toggle` non fa bolla, quindi si ascolta in cattura.
    document.addEventListener('toggle', function () { requestAnimationFrame(fitAll); }, true);

    // ---- La topbar sceglie il suo scaglione MISURANDOSI ----
    //
    // Fino al 22 agosto 2026 lo sceglievano tre media query (1500/1300/900). Non ha funzionato, e la ragione
    // e' di metodo: ⚠️ una media query misura la FINESTRA, mentre il problema e' la larghezza della BARRA. Le
    // due cose non sono la stessa, perche' quanto la barra pretende dipende da sei cose che una `@media` non
    // vede — login si'/no, lunghezza della stringa staff (chi ha quattro incarichi pesa il doppio di chi ne
    // ha due), numero di ACC, lingua, ZOOM di pagina (che arriva a 1.8) e stato della ricerca. Tarate su una
    // configurazione sola, le soglie erano giuste soltanto li': la barra si rompeva gia' a 1940, cioe' 440px
    // sopra la prima soglia. Carta: docs/feature/2026-08-22-topbar-misurata.md.
    //
    // Le regole di ogni scaglione sono le stesse di prima e stanno dov'erano, in vipi-theme.css: qui si
    // decide solo QUANDO valgono. Le classi sono cumulative (tb-3 implica tb-1 e tb-2).
    // tb-1 spazi + badge staff a icona · tb-2 sottotitolo e nomi dei comandi via · tb-3 la ricerca si chiude
    // · tb-4 forma telefono. Oltre tb-4 non c'e' piu' niente da togliere: la barra e' gia' logo + ricerca + «☰».
    // ⚠️ Sono QUATTRO e non tre perche' il gradino contava: tenendo «la ricerca si chiude» insieme alle
    // etichette, un solo scaglione buttava via 500px e a 1440 la barra passava da sfondare a essere mezza
    // vuota. Se un gradino e' piu' alto di quanto serva, non e' una scaletta.
    var TB_MAX = 4;
    // Isteresi, e serve solo nel verso che MOSTRA di piu': salire di scaglione si fa appena serve, scendere
    // solo con margine. Senza, la barra sbatte fra due assetti sul pixel di confine mentre si trascina il bordo.
    var TB_SLACK = 40;
    var tbLevel = 0;
    var tbSettledAt = 0;
    var tbQueued = false;
    var tbObserved = null;

    function tbApply(bar, lvl) {
        for (var i = 1; i <= TB_MAX; i++) bar.classList.toggle('tb-' + i, i <= lvl);
    }

    // Quanto MANCA perche' la barra stia, a questo scaglione. Due addendi, e il secondo non e' un di piu':
    // ⚠️ `scrollWidth == clientWidth` da solo MENTE. La ricerca ha `flex-shrink:1`, quindi cede fino al suo
    // minimo PRIMA che la barra sfori: a quel punto la barra «sta» e il segnaposto dice «Cerca Co…». Il
    // difetto si e' solo spostato, ed e' lo stesso inganno che in fase di taratura aveva fatto leggere 306px
    // liberi a 1280 — misurati con la ricerca chiusa.
    function tbDeficit(bar, lvl) {
        // ⚠️ `scrollWidth`/`clientWidth` parlano in unita' di LAYOUT: lo zoom di pagina non li tocca, al
        // contrario di `getBoundingClientRect` (vedi rootZoom). Qui e' proprio quel che serve, e non c'e'
        // niente da convertire.
        var d = bar.scrollWidth - bar.clientWidth;
        if (lvl >= 3) return d;      // da tb-3 la ricerca e' un'icona: non ha un minimo da difendere
        var search = bar.querySelector('.top-search');
        if (!search) return d;
        var min = parseFloat(getComputedStyle(bar).getPropertyValue('--tb-search-min'));
        if (!min || isNaN(min)) return d;
        return d + Math.max(0, min - search.clientWidth);
    }

    function tbFit() {
        var bar = document.querySelector('.topbar');
        if (!bar) return;
        // ⚠️ Mai rimisurare mentre la ricerca ha il fuoco: da tb-2 il campo aperto e' `position:fixed`, esce
        // dal flusso, e la barra sembra piu' stretta di quanto sara' quando si richiude. Rifare i conti li'
        // vorrebbe dire far saltare il campo sotto le dita di chi sta scrivendo.
        var a = document.activeElement;
        if (a && a.closest && a.closest('.topbar .top-search')) return;

        // ⚠️ Si riparte SEMPRE dal livello 0 e si sale. Misurare lo scaglione corrente e indovinare il
        // prossimo e' lo stesso errore di prima con un altro vestito: l'unico stato di cui si puo' dire
        // qualcosa di vero e' quello applicato. Costa tre riflow, una volta per ridimensionamento.
        var lvl = 0;
        tbApply(bar, 0);
        // ⚠️ Lo spazio disponibile e' `bar.clientWidth`, NON `documentElement.clientWidth`, e sbagliarlo e'
        // costato un difetto che la griglia ha preso: sotto zoom i due numeri divergono — a 1920 con zoom 1.4
        // la barra ha 1371 unita' di layout mentre `documentElement` continua a dire 1920 — e l'isteresi,
        // confrontando l'uno con l'altro, diventava un CRICCHETTO: saliva di scaglione allo zoom e non
        // scendeva piu', tanto che a 1440 la barra era gia' in forma telefono. La misura del fit e quella
        // dell'isteresi devono stare nella stessa unita', che e' quella della barra.
        var w = bar.clientWidth;
        for (; lvl < TB_MAX; lvl++) {
            if (lvl > 0) tbApply(bar, lvl);
            if (tbDeficit(bar, lvl) <= 0) break;
        }
        if (lvl >= TB_MAX) { lvl = TB_MAX; tbApply(bar, lvl); }

        // ⚠️ L'isteresi frena SOLO la barra che si allarga di poco, e il «di poco» va misurato dall'ultimo
        // assestamento. Frenare sempre e' un difetto che la verifica ha preso: allungando la stringa staff a
        // larghezza ferma la barra saliva a tb-1 e, rimessa la stringa corta, NON tornava a tb-0 — perche' la
        // larghezza non era cambiata e il margine non poteva maturare. Un calo dovuto al CONTENUTO non ha
        // niente da frenare: non c'e' nessun bordo che si sta trascinando.
        if (lvl < tbLevel && w > tbSettledAt && w - tbSettledAt < TB_SLACK) {
            lvl = tbLevel;               // troppo poco margine per rimostrare: si resta dov'eravamo
            tbApply(bar, lvl);
        } else {
            tbSettledAt = w;
        }
        tbLevel = lvl;
    }

    function tbSchedule() {
        if (tbQueued) return;
        tbQueued = true;
        requestAnimationFrame(function () { tbQueued = false; tbFit(); });
    }

    // Il badge live e' un'isola interattiva: quando ti colleghi in frequenza il suo testo cambia, la barra si
    // allarga e ⚠️ NESSUN `resize` viene emesso. Da qui l'osservatore.
    // ⚠️ Solo `childList`/`characterData`: gli ATTRIBUTI restano fuori dall'osservazione, o le classi che
    // scriviamo noi rientrerebbero da sole e il giro non finirebbe piu'.
    function tbWatch() {
        var bar = document.querySelector('.topbar');
        if (!bar || bar === tbObserved) return;
        tbObserved = bar;
        new MutationObserver(tbSchedule).observe(bar, { childList: true, characterData: true, subtree: true });
        bar.addEventListener('focusout', tbSchedule);
    }

    window.vipiFitTopbar = function () { tbWatch(); tbFit(); };

    // ---- E anche i RIQUADRI scelgono il loro scaglione misurandosi ----
    //
    // Stessa malattia della topbar, stessa cura, un anno di distanza: ⚠️ una `@media` misura la FINESTRA,
    // mentre lo zoom di questa applicazione è `zoom` sull'`<html>` — la finestra non cambia e la soglia non
    // scatta mai. Misurato il 27 agosto 2026 su /services/vsop/admin/tasks a 1600px: a zoom 1.8 il riquadro
    // ha 889 unità di layout (sotto la soglia di 900) e `matchMedia('(max-width:900px)')` risponde ancora
    // NO. Le due colonne restavano affiancate a 526 e 277px invece di impilarsi.
    //
    // ⚠️ Perché non `@container`, che è la cura usata per il viewer (`.wrap:has(> .doc-layout)`):
    // `container-type` porta con sé `contain:layout`, che rende il riquadro contenitore anche per i
    // discendenti `position:fixed`. Sulle pagine admin il `DeleteDialog` è un `.del-card` fisso, centrato
    // sullo SCHERMO, e vive dentro la riga di una tabella: contenendo il `.wrap` finirebbe centrato su un
    // riquadro alto migliaia di pixel, cioè fuori dallo schermo. Le pagine di lettura quel dialogo non ce
    // l'hanno, ed è per questo che là il contenimento si può.
    //
    // Le classi sono cumulative (pw-760 implica pw-900 e pw-1080) e le regole di ogni scaglione stanno dove
    // stavano, in vipi-theme.css: qui si decide solo QUANDO valgono.
    // ⚠️ Le soglie sono quelle che c'erano: non si è colta l'occasione per «razionalizzarle» (1200 e 1180
    // sono vicine ma governano due pagine diverse, e ognuna era stata misurata dov'è).
    var pwSoglie = [1200, 1180, 1080, 900, 760];
    function pwFit(el) {
        // ⚠️ `clientWidth` del RIQUADRO, non di `documentElement`: in Edge 151 quello della radice non è in
        // unità di layout sotto `zoom` e risponde con i px di finestra — una misura presa da lì dice che
        // non succede niente.
        var w = el.clientWidth;
        if (!w) return;                              // riquadro non ancora disegnato: non si decide al buio
        for (var i = 0; i < pwSoglie.length; i++) el.classList.toggle('pw-' + pwSoglie[i], w <= pwSoglie[i]);
    }
    function pwWire() {
        document.querySelectorAll('.wrap').forEach(function (el) {
            pwFit(el);
            if (el.hasAttribute('data-pw')) return;
            el.setAttribute('data-pw', '1');
            // L'osservatore prende TUTTO quel che un `resize` non emette: lo zoom, una colonna che compare,
            // un pannello che si apre di fianco.
            if (window.ResizeObserver) new ResizeObserver(function () { pwFit(el); }).observe(el);
        });
    }
    // ⚠️ Non basta agganciarsi una volta all'avvio, e costa un giro di misure scoprirlo: su una pagina
    // `InteractiveServer` il riquadro che si vede NON è quello che c'era al `DOMContentLoaded` — Blazor lo
    // rifà quando il circuito parte, e il nuovo nasce senza osservatore. A finestra stretta (860px) le
    // colonne restavano affiancate mentre a zoom 1.8 si impilavano: stesso codice, due esiti, perché nel
    // secondo caso a rimisurare era l'osservatore del riquadro VECCHIO, ancora vivo.
    var pwPending = false;
    function pwSchedule() {
        // ⚠️ Il segno di «già agganciato» è un ATTRIBUTO e non una proprietà, perché serve qui: questa
        // domanda gira a ogni render e dev'essere un selettore, non una lettura di `clientWidth` — che
        // costerebbe un ricalcolo di layout ogni volta che una tabella si aggiorna.
        if (pwPending || !document.querySelector('.wrap:not([data-pw])')) return;
        pwPending = true;
        requestAnimationFrame(function () { pwPending = false; pwWire(); });
    }
    function pwWatch() {
        var host = document.querySelector('main') || document.querySelector('.vipi-root') || document.body;
        if (!host || host.__vipiPwHost) return;
        host.__vipiPwHost = true;
        new MutationObserver(pwSchedule).observe(host, { childList: true, subtree: true });
    }
    window.vipiFitPanes = function () { pwWatch(); pwWire(); };
    window.addEventListener('resize', pwWire);

    window.addEventListener('resize', tbSchedule);   // e lo zoom ne emette uno apposta (vipi-zoom.js)
    // I font web cambiano le misure: al primo giro `scrollWidth` e' quello del ripiego, non quello vero.
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(tbSchedule);

    window.vipiRevealPanel = function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var foot = el.querySelector('.xt-panel-foot') || el;
        for (var i = 0; i < 3; i++) {
            // Se la scheda intera ci sta, il bersaglio è la scheda: mirando al solo piede restavano fuori i
            // pixel di bordo e padding sotto di lui.
            var target = el.getBoundingClientRect().height <= window.innerHeight - 16 ? el : foot;
            var r = target.getBoundingClientRect();
            var d = 0;
            if (r.bottom > window.innerHeight - 8) d = r.bottom - (window.innerHeight - 8);
            else if (r.top < 8) d = r.top - 8;
            if (Math.abs(d) < 2) break;
            window.scrollBy(0, d);
        }
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

    // Modalità compatta: classe su <html> (non su .vipi-root, che Blazor ricostruisce) + localStorage.
    // Fuori dal circuito Blazor come lo zoom: sopravvive a re-render e navigazioni senza round-trip.
    function applyDense() {
        var on = false;
        try { on = localStorage.getItem('vipiDense') === '1'; } catch (e) { }
        document.documentElement.classList.toggle('vipi-dense', on);
        document.querySelectorAll('[data-dense-toggle]').forEach(function (b) {
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }
    window.vipiToggleDense = function () {
        var on = document.documentElement.classList.contains('vipi-dense');
        try { localStorage.setItem('vipiDense', on ? '0' : '1'); } catch (e) { }
        applyDense();
    };

    // Orologio UTC: i controllori ragionano in Z. Aggiornato dal browser, non dal server (nessun tick sul circuito).
    var clockTimer = null;
    function wireUtcClock() {
        function tick() {
            var now = new Date();
            var t = ('0' + now.getUTCHours()).slice(-2) + ':' + ('0' + now.getUTCMinutes()).slice(-2) + ':'
                  + ('0' + now.getUTCSeconds()).slice(-2) + 'Z';
            document.querySelectorAll('[data-utc-clock]').forEach(function (el) { el.textContent = t; });
        }
        tick();
        if (clockTimer) return;
        clockTimer = setInterval(tick, 1000);
    }

    // Riaggancia SOLO la persistenza del collasso. Serve alle pagine InteractiveServer (vista live) che
    // ricostruiscono i <details> a ogni tick: senza questo, dopo il primo aggiornamento il collasso non
    // verrebbe più ricordato. Idempotente (wireCollapse salta quelli già agganciati); non si chiama
    // vipiWireUi perché wireHashLanding riscorrerebbe la pagina a ogni render.
    window.vipiWireCollapse = wireCollapse;

    window.vipiWireUi = function () {
        document.querySelectorAll('.aor-block').forEach(wireAor);
        wireExpand();
        wireAnchors();
        wireCollapse();
        wireBlockMenu();
        wireTocDrop();
        applyDense();
        wireUtcClock();
        wireSearchKey();
        wirePrint();
        wireHashLanding();   // deep-link "#id" verso sezioni collassate (Guida) → apri + scorri
        window.vipiFitTopbar();
        window.vipiFitPanes();
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.vipiApplyZoom && window.vipiApplyZoom();
        window.vipiWireUi();
    });

    // ⚠️ E anche SUBITO, senza aspettare DOMContentLoaded: questo file e' caricato in fondo al <body>, quindi
    // qui la topbar c'e' gia' — e da quando gli scaglioni non sono piu' media query, fra il primo disegno e
    // la prima misura la barra sta al livello 0. Misurare adesso e' ciò che tiene quel divario dentro un
    // fotogramma invece di regalarlo alla rete.
    window.vipiFitTopbar();
    window.vipiFitPanes();
})();

// Consegna un file al browser a partire da uno stream .NET (Aurora Profile Swapper).
// Perche' non base64: lo zip dei profili aggiornati passerebbe come UNA stringa dentro un messaggio di
// interoperabilita', gonfiata di un terzo e tenuta in memoria tre volte (stringa, decodifica, blob). Con
// DotNetStreamReference i byte arrivano come sono. L'URL si revoca, o il blob resta appeso alla pagina
// finche' non si cambia scheda.
window.vipiScaricaFile = async function (nome, streamRef) {
    const buffer = await streamRef.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], { type: 'application/zip' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = nome;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
};
