// Mappa AOR: disegna il poligono shape reale su una basemap minimal (CartoDB Positron) via Leaflet.
// Idempotente: ogni contenitore .aor-leaflet[data-poly] è inizializzato una sola volta (data-init).
(function () {
    function initOne(el) {
        if (!window.L || el.dataset.init === '1') return;
        var pts;
        try { pts = JSON.parse(el.dataset.poly || '[]'); } catch (e) { return; }
        if (!pts || pts.length < 3) return;

        el.dataset.init = '1';
        el.innerHTML = '';   // rimuove l'SVG di fallback

        var map = L.map(el, { scrollWheelZoom: false, zoomControl: true, attributionControl: true });
        L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19, subdomains: 'abcd',
            attribution: '© OpenStreetMap, © CARTO'
        }).addTo(map);

        var poly = L.polygon(pts, { color: '#3C55AC', weight: 2, fillColor: '#3C55AC', fillOpacity: 0.12 }).addTo(map);

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
        if (twrGroup) twrGroup.eachLayer(function (l) { bounds = bounds.extend(l.getBounds()); });
        map.fitBounds(bounds, { padding: [18, 18] });
        // Ricalcolo dimensioni quando il contenitore diventa visibile/ridimensiona.
        setTimeout(function () { map.invalidateSize(); }, 60);
    }

    function initAll() {
        if (!window.L) return;
        document.querySelectorAll('.aor-leaflet').forEach(initOne);
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
