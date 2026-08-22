// Carta delle minime di vettoramento (MRVA): disegna il contenuto di un file .mva su una basemap TOPOGRAFICA.
// Idempotente: ogni contenitore .mva-leaflet[data-mva] è inizializzato una sola volta (data-init), come vipi-aor.js.
//
// La basemap di partenza è il rilievo, e non è una scelta estetica: la MRVA di una zona dipende dall'orografia,
// e con le curve di livello sotto i poligoni si legge PERCHÉ in quel punto la minima è quella.
(function () {
    // I tre fondi. OpenTopoMap porta curve di livello e quote scritte (il più utile per capire il dato); Esri
    // World Hillshade dà il rilievo senza il rumore delle curve; Positron è il fondo neutro già usato dall'AoR.
    function basemaps() {
        return {
            'Rilievo': L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
                maxZoom: 17, subdomains: 'abc',
                attribution: '© OpenStreetMap, SRTM | © OpenTopoMap (CC-BY-SA)'
            }),
            'Ombreggiatura': L.tileLayer(
                'https://server.arcgisonline.com/ArcGIS/rest/services/Elevation/World_Hillshade/MapServer/tile/{z}/{y}/{x}', {
                maxZoom: 19, attribution: '© Esri — Elevation/World Hillshade'
            }),
            'Neutra': L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
                maxZoom: 19, subdomains: 'abcd', attribution: '© OpenStreetMap, © CARTO'
            })
        };
    }

    // Un colore che arriva dal DOM può essere un hex vero o il NOME di un token del tema: Leaflet lo scrive in un
    // attributo SVG, che non fa la sostituzione di var(). Stessa risoluzione di vipi-aor.js.
    function color(v, fallback) {
        v = (v || '').trim();
        if (!v) v = fallback;
        if (v.indexOf('--') !== 0) return v;
        var r = getComputedStyle(document.documentElement).getPropertyValue(v).trim();
        return r || fallback;
    }

    // L'etichetta è testo che arriva dal sectorfile, cioè da un repository esterno: va scritta come DATO, mai
    // interpretata come marcatura. Serve l'escape perché Leaflet accetta solo una stringa HTML per l'icona.
    var ENTITIES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return ENTITIES[c]; });
    }

    function initOne(el) {
        if (el.dataset.init === '1') return;
        var data;
        try { data = JSON.parse(el.dataset.mva || 'null'); } catch (e) { return; }
        if (!data) return;

        var shapes = data.shapes || [], labels = data.labels || [];
        if (!shapes.length && !labels.length) return;

        el.dataset.init = '1';
        el.innerHTML = '';   // via il fallback SVG reso dal server
        var map = L.map(el, { scrollWheelZoom: false, zoomControl: true, attributionControl: true });
        el._leafletMap = map;

        var maps = basemaps();
        maps['Rilievo'].addTo(map);
        L.control.layers(maps, null, { collapsed: true }).addTo(map);

        var stroke = color('--ivao-color-product-artifice-dark', '#e26e17');
        var layers = [];

        shapes.forEach(function (s) {
            var pts = s.p || [];
            if (pts.length < 2) return;
            // Chiuso = area, aperto = linea. La distinzione viene dal file e NON si corregge qui: i tracciati
            // aperti del sectorfile sono archi e confini, e chiuderli disegnerebbe una figura inesistente.
            var layer = s.c
                ? L.polygon(pts, { color: stroke, weight: 2, fillColor: stroke, fillOpacity: 0.07 })
                : L.polyline(pts, { color: stroke, weight: 2 });
            if (s.n) layer.bindTooltip(s.n, { sticky: true });
            layer.addTo(map);
            layers.push(layer);
        });

        labels.forEach(function (lb) {
            if (typeof lb.lat !== 'number' || typeof lb.lon !== 'number') return;
            // Il testo è VERBATIM dal sectorfile ("110", "1500", "TRL", "NO MINIMA", "80/TRL"): niente unità
            // aggiunte, niente conversioni — il formato non dice quali siano.
            //
            // ⚠️ Il testo va DENTRO l'icona, non scritto dopo su marker.getElement(): finché la mappa non ha una
            // vista Leaflet rimanda onAdd, quindi subito dopo addTo() l'elemento è ancora null — e il fitBounds
            // qui sotto viene dopo. Scritto così si perdeva ogni etichetta, in silenzio.
            // iconSize null = la dimensione la dà il CSS, che è quel che serve a un testo di lunghezza varia.
            var icon = L.divIcon({ className: 'mva-label', html: '<span>' + esc(lb.t) + '</span>', iconSize: null });
            var marker = L.marker([lb.lat, lb.lon], { icon: icon, interactive: false, keyboard: false });
            marker.addTo(map);
            layers.push(marker);
        });

        function boundsOf() {
            var b = null;
            layers.forEach(function (l) {
                var lb = l.getBounds ? l.getBounds() : L.latLngBounds(l.getLatLng(), l.getLatLng());
                b = b ? b.extend(lb) : lb;
            });
            return b;
        }

        var bounds = boundsOf();
        if (bounds) { fitBox(bounds); map.fitBounds(bounds, { padding: [18, 18] }); }

        // La SCATOLA si adatta ai dati, non viceversa. Senza, una carta alta e stretta (LIBB: 5,5° di latitudine
        // per 3,1° di longitudine) in un contenitore largo e basso viene inquadrata sull'altezza — corretto, ma
        // il resto della larghezza è mare, e i tracciati restano minuscoli. Si sceglie quindi un'altezza che
        // avvicini l'aspetto del contenitore a quello del dato, e si limita la larghezza di conseguenza.
        function fitBox(b) {
            var latSpan = b.getNorth() - b.getSouth();
            var lonSpan = (b.getEast() - b.getWest()) * Math.cos((b.getNorth() + b.getSouth()) / 2 * Math.PI / 180);
            if (latSpan <= 0 || lonSpan <= 0) return;
            var aspect = lonSpan / latSpan;                      // >1 larga, <1 alta
            var avail = el.parentElement ? el.parentElement.clientWidth : el.clientWidth;
            if (!avail) return;
            var h = Math.round(Math.min(620, Math.max(320, avail / aspect)));
            // Un po' di margine oltre l'aspetto esatto (×1.35): incorniciare al millimetro toglie il contesto
            // geografico, che qui serve — è il motivo per cui sotto c'è il rilievo.
            var w = Math.round(Math.min(avail, Math.max(340, h * aspect * 1.35)));
            el.style.height = h + 'px';
            el.style.width = w + 'px';
            el.style.margin = '0 auto';
            map.invalidateSize();
        }
        // Stesso contratto dell'AoR (vedi wirePrint in vipi-ui.js): la stampa riduce il contenitore e deve
        // riadattare l'inquadratura invece di ritagliarla.
        el._aorBounds = bounds;
        el._aorRefit = function () { if (bounds) map.fitBounds(bounds, { padding: [18, 18] }); };
        setTimeout(function () { map.invalidateSize(); if (bounds) map.fitBounds(bounds, { padding: [18, 18] }); }, 60);
    }

    function initAll() {
        if (window.L) document.querySelectorAll('.mva-leaflet').forEach(initOne);
    }
    window.vipiInitMva = initAll;

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
