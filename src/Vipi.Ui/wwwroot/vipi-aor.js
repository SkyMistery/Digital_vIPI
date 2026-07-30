// Mappa AOR: disegna il poligono shape reale su una basemap minimal (CartoDB Positron) via Leaflet.
// Idempotente: ogni contenitore .aor-leaflet[data-poly] è inizializzato una sola volta (data-init).
(function () {
    // Basemap CartoDB Positron condivisa.
    function addBasemap(map) {
        L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19, subdomains: 'abcd', attribution: '© OpenStreetMap, © CARTO'
        }).addTo(map);
    }

    // ACC: una mappa, anelli per settore toggleabili. data-sectors = [{sec,name,color,rings:[[[lat,lng],…]]}].
    // Le chip (settori sopra, config sotto) vivono nel .aor-block genitore e pilotano i layer client-side.
    function initSectors(el) {
        var sectors;
        try { sectors = JSON.parse(el.dataset.sectors || '[]') || []; } catch (e) { return; }
        if (!sectors.length) return;

        el.dataset.init = '1';
        el.innerHTML = '';
        var map = L.map(el, { scrollWheelZoom: false, zoomControl: true, attributionControl: true });
        el._leafletMap = map;
        addBasemap(map);

        // sec (upper) → { layers:[L.polygon], on:bool }
        var secMap = {};
        sectors.forEach(function (s) {
            var layers = (s.rings || []).filter(function (r) { return r && r.length >= 3; }).map(function (r) {
                return L.polygon(r, { color: s.color, weight: 2, fillColor: s.color, fillOpacity: 0.16 });
            });
            secMap[(s.sec || '').toUpperCase()] = { layers: layers, on: false };
        });
        el._secMap = secMap;

        function refit() {
            var b = null;
            Object.keys(secMap).forEach(function (k) {
                if (!secMap[k].on) return;
                secMap[k].layers.forEach(function (l) { b = b ? b.extend(l.getBounds()) : l.getBounds(); });
            });
            // I bounds inquadrati restano leggibili da fuori: la stampa ne ricava le proporzioni per dare alla
            // cornice la forma dell'AoR (vedi wirePrint in vipi-ui.js).
            el._aorBounds = b;
            if (b) map.fitBounds(b, { padding: [18, 18] });
        }
        el._aorRefit = refit;

        function setSec(sec, on) {
            var e = secMap[(sec || '').toUpperCase()];
            if (!e) return;
            e.on = on;
            e.layers.forEach(function (l) { on ? l.addTo(map) : map.removeLayer(l); });
        }
        el._aorSetSec = setSec;

        // Tutti accesi all'avvio.
        Object.keys(secMap).forEach(function (k) { setSec(k, true); });
        refit();
        setTimeout(function () { map.invalidateSize(); refit(); }, 60);
    }

    // Interazione chip via EVENT DELEGATION (installato una volta): robusto a qualsiasi re-render di Blazor,
    // niente listener per-elemento da riattaccare. Funziona con Leaflet o col fallback SVG.
    function onAorClick(ev) {
        var t = ev.target && ev.target.closest ? ev.target.closest('.aor-chip,.aor-all,.cfg-btn,.cfg-clear') : null;
        if (!t) return;
        var block = t.closest('.aor-block');
        if (!block) return;
        var lf = block.querySelector('.aor-leaflet');

        function setSec(sec, on) {
            if (lf && lf._aorSetSec) { lf._aorSetSec(sec, on); return; }
            if (lf) lf.querySelectorAll('svg [data-sec="' + (sec || '').replace(/"/g, '') + '"]').forEach(function (p) {
                p.style.display = on ? '' : 'none';
            });
        }
        function refit() { if (lf && lf._aorRefit) lf._aorRefit(); }
        // Apre/collassa le configurazioni nella sezione "Configurazioni operative" (details a parte, scoping per block).
        function syncCfgDetails(key) {
            var scope = block.dataset.aor || '';
            document.querySelectorAll('.cfg-collapse').forEach(function (d) {
                if ((d.dataset.cfgblock || '') !== scope) return;
                d.open = (key != null && d.dataset.cfgkey === key);
            });
        }
        // Verità = stato del layer Leaflet (secMap), non la classe (che può desincronizzarsi col re-render).
        function isOn(sec) { var e = lf && lf._secMap && lf._secMap[(sec || '').toUpperCase()]; return e ? !!e.on : t.classList.contains('on'); }

        if (t.classList.contains('aor-chip')) {
            var nn = !isOn(t.dataset.sec);
            t.classList.toggle('on', nn);
            setSec(t.dataset.sec, nn);
            refit();
        } else if (t.classList.contains('aor-all')) {
            var allOn = t.dataset.act === 'all';
            block.querySelectorAll('.aor-chip').forEach(function (ch) {
                ch.classList.toggle('on', allOn);
                setSec(ch.dataset.sec, allOn);
            });
            refit();
        } else if (t.classList.contains('cfg-btn')) {
            // Verità selezione = proprietà JS sul block (la classe si desincronizza). Riclick sulla stessa = deseleziona → mostra tutti.
            var wasSel = block.__selCfgNode === t;
            block.querySelectorAll('.cfg-btn').forEach(function (x) { x.classList.remove('on'); });
            if (wasSel) {
                block.__selCfgNode = null;
                block.querySelectorAll('.aor-chip').forEach(function (ch) { ch.classList.add('on'); setSec(ch.dataset.sec, true); });
                syncCfgDetails(null);   // deseleziona → collassa tutte
            } else {
                block.__selCfgNode = t;
                t.classList.add('on');
                var set = (t.dataset.secs || '').split(',').map(function (s) { return s.toUpperCase(); }).filter(Boolean);
                block.querySelectorAll('.aor-chip').forEach(function (ch) {
                    var on = set.indexOf((ch.dataset.sec || '').toUpperCase()) >= 0;
                    ch.classList.toggle('on', on);
                    setSec(ch.dataset.sec, on);
                });
                syncCfgDetails(t.dataset.cfgkey || '');   // apre solo questa, collassa le altre
            }
            refit();
            // Tiene l'AoR al centro schermo: aprire i details config sposta il layout.
            if (lf) setTimeout(function () { lf.scrollIntoView({ behavior: 'smooth', block: 'center' }); }, 90);
        } else if (t.classList.contains('cfg-clear')) {
            block.__selCfgNode = null;
            block.querySelectorAll('.cfg-btn').forEach(function (x) { x.classList.remove('on'); });
            block.querySelectorAll('.aor-chip').forEach(function (ch) { ch.classList.add('on'); setSec(ch.dataset.sec, true); });
            syncCfgDetails(null);
            refit();
        }
    }
    document.addEventListener('click', onAorClick);

    // Mappe dentro <details> chiusi partono a dimensione 0: all'apertura inizializza/ricalcola. (toggle non fa bubbling → capture)
    document.addEventListener('toggle', function (ev) {
        var d = ev.target;
        if (!d || d.tagName !== 'DETAILS' || !d.open) return;
        d.querySelectorAll('.aor-leaflet').forEach(function (el) {
            if (el._leafletMap) setTimeout(function () { el._leafletMap.invalidateSize(); }, 60);
            else initOne(el);
        });
    }, true);

    function initOne(el) {
        if (!window.L || el.dataset.init === '1') return;

        // ACC multi-settore: una mappa con anelli toggleabili.
        if (el.dataset.sectors != null) { initSectors(el); return; }

        // Due modalità: data-poly = singolo anello (APP); data-polys = array di anelli (ACC, unione settori config).
        var rings = [];
        if (el.dataset.polys != null) {
            try { rings = (JSON.parse(el.dataset.polys || '[]') || []).filter(function (r) { return r && r.length >= 3; }); }
            catch (e) { return; }
        } else {
            var pts;
            try { pts = JSON.parse(el.dataset.poly || '[]'); } catch (e) { return; }
            if (pts && pts.length >= 3) rings = [pts];
        }
        if (!rings.length) return;

        el.dataset.init = '1';
        el.innerHTML = '';   // rimuove l'SVG di fallback

        var map = L.map(el, { scrollWheelZoom: false, zoomControl: true, attributionControl: true });
        el._leafletMap = map;
        L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19, subdomains: 'abcd',
            attribution: '© OpenStreetMap, © CARTO'
        }).addTo(map);

        var color = el.dataset.color || '#3C55AC';
        var mainPolys = rings.map(function (r) {
            return L.polygon(r, { color: color, weight: 2, fillColor: color, fillOpacity: 0.16 }).addTo(map);
        });
        var poly = mainPolys[0];

        // Overlay shape torre/i dello stesso aeroporto: gruppo unico con control layer per mostrare/nascondere.
        var towers = [];
        try { towers = JSON.parse(el.dataset.towers || '[]'); } catch (e) { towers = []; }
        var twrGroup = null;
        if (towers && towers.length) {
            var rings = [];
            towers.forEach(function (ring) {
                if (ring && ring.length >= 3)
                    rings.push(L.polygon(ring, { color: '#C2410C', weight: 2, dashArray: '5,4', fillColor: '#F97316', fillOpacity: 0.12 }));
            });
            if (rings.length) {
                twrGroup = L.layerGroup(rings).addTo(map);   // visibile di default
                L.control.layers(null, { 'Shape torre': twrGroup }, { collapsed: false }).addTo(map);
            }
        }

        var bounds = poly.getBounds();
        mainPolys.forEach(function (p) { bounds = bounds.extend(p.getBounds()); });
        if (twrGroup) twrGroup.eachLayer(function (l) { bounds = bounds.extend(l.getBounds()); });
        map.fitBounds(bounds, { padding: [18, 18] });
        // Refit esposto come in initSectors (contratto uniforme per i due tipi di mappa): la stampa riduce
        // l'altezza del contenitore e deve riadattare l'inquadratura, non ritagliarla. Vedi wirePrint in
        // vipi-ui.js. Qui i poligoni non cambiano, quindi i bounds sono quelli calcolati sopra.
        el._aorBounds = bounds;
        el._aorRefit = function () { map.fitBounds(bounds, { padding: [18, 18] }); };
        // Ricalcolo dimensioni quando il contenitore diventa visibile/ridimensiona.
        setTimeout(function () { map.invalidateSize(); }, 60);
    }

    function initAll() {
        if (window.L) document.querySelectorAll('.aor-leaflet').forEach(initOne);
        // Le chip AoR usano event delegation (onAorClick), nessun wiring per-elemento.
    }
    window.vipiInitAor = initAll;

    document.addEventListener('DOMContentLoaded', initAll);

    // Re-init dopo i render di Blazor (statico-enhanced e interattivo): osserva l'aggiunta di nodi.
    var pending = false;
    var obs = new MutationObserver(function () {
        if (pending) return;
        pending = true;
        setTimeout(function () { pending = false; initAll(); }, 80);
    });
    if (document.body) obs.observe(document.body, { childList: true, subtree: true });
})();
