// Carta delle minime di vettoramento (MRVA): disegna il contenuto di un file .mva su una basemap TOPOGRAFICA.
// Idempotente: ogni contenitore .mva-leaflet[data-mva] è inizializzato una sola volta (data-init), come vipi-aor.js.
//
// La basemap di partenza è il rilievo SENZA STRADE, e non è una scelta estetica: la MRVA di una zona dipende
// dall'orografia, e col terreno sotto i poligoni si legge PERCHÉ in quel punto la minima è quella. La rete
// stradale, invece, non c'entra nulla e toglie contrasto ai tracciati.
(function () {
    // I fondi. Il primo è quello che si vede all'apertura ed è SENZA STRADE: qui la rete stradale non aggiunge
    // niente e ruba leggibilità ai tracciati, mentre il rilievo è il motivo per cui la carta sta su una mappa.
    // Positron è sparito per la stessa ragione: neutro sì, ma è una mappa di strade senza rilievo.
    function basemaps() {
        return {
            // Partenza: DUE tile impilate, non una. Provate separatamente non bastavano — «World Terrain Base»
            // dà terra, mare e vegetazione ma a questi zoom le montagne quasi non si vedono; «World Hillshade»
            // dà il rilievo ma su fondo grigio uniforme, dove costa e mare spariscono. Insieme si leggono
            // entrambi, e nessuna delle due porta strade.
            'Rilievo': L.layerGroup([
                L.tileLayer('https://server.arcgisonline.com/arcgis/rest/services/World_Terrain_Base/MapServer/tile/{z}/{y}/{x}', {
                    maxZoom: 13, attribution: '© Esri — World Terrain Base'
                }),
                L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Elevation/World_Hillshade/MapServer/tile/{z}/{y}/{x}', {
                    maxZoom: 16, opacity: 0.55, attribution: '© Esri — Elevation/World Hillshade'
                })
            ]),
            // Unico fondo con le strade, e l'unico con le QUOTE scritte sulle curve di livello: resta come scelta
            // esplicita per chi vuole leggere l'altitudine del suolo, non come partenza.
            'Curve di livello': L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
                maxZoom: 17, subdomains: 'abc',
                attribution: '© OpenStreetMap, SRTM | © OpenTopoMap (CC-BY-SA)'
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

    // Colore dei tracciati: LETTERALE come per l'etichetta, e per lo stesso motivo — il substrato è una tile a
    // rilievo, chiara in tutti e due i temi. Rosso perché deve staccare da verdi, marroni e blu del terreno,
    // che sono tutto quello che c'è sotto.
    var MVA_COLOR = '#c1121f';

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
        var aor = [];
        try { aor = JSON.parse(el.dataset.aor || '[]') || []; } catch (e) { aor = []; }

        el.dataset.init = '1';
        el.innerHTML = '';   // via il fallback SVG reso dal server
        var map = L.map(el, { scrollWheelZoom: false, zoomControl: true, attributionControl: true });
        el._leafletMap = map;

        var maps = basemaps();
        maps['Rilievo'].addTo(map);

        var layers = [];

        shapes.forEach(function (s) {
            var pts = s.p || [];
            if (pts.length < 2) return;
            // Ogni tracciato è disegnato DUE VOLTE: una fascia bianca sotto e la linea colorata sopra. È la
            // tecnica cartografica del casing, e qui non è un vezzo — su un fondo a rilievo, che passa dal verde
            // al marrone al blu, una linea sola cambia contrasto a ogni valle e sparisce dove il terreno ha il
            // suo stesso tono.
            var casing = s.c
                ? L.polygon(pts, { color: '#fff', weight: 5, opacity: 0.9, fill: false, interactive: false })
                : L.polyline(pts, { color: '#fff', weight: 5, opacity: 0.9, interactive: false });
            // Chiuso = area, aperto = linea. La distinzione viene dal file e NON si corregge qui: i tracciati
            // aperti del sectorfile sono archi e confini, e chiuderli disegnerebbe una figura inesistente.
            var layer = s.c
                ? L.polygon(pts, { color: MVA_COLOR, weight: 2.5, fillColor: MVA_COLOR, fillOpacity: 0.05 })
                : L.polyline(pts, { color: MVA_COLOR, weight: 2.5 });
            if (s.n) layer.bindTooltip(s.n, { sticky: true });
            casing.addTo(map);
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

        // AoR della stessa parte di documento: contesto accendibile, spento all'apertura. Non entra nei bounds —
        // l'inquadratura resta quella delle minime, che sono il contenuto della sezione.
        var overlays = {};
        aor.forEach(function (s) {
            var rings = (s.rings || []).filter(function (r) { return r && r.length >= 3; });
            if (!rings.length) return;
            var col = color(s.color, '--ivao-lightblue');
            overlays['AoR ' + (s.sec || '')] = L.layerGroup(rings.map(function (r) {
                // Solo contorno tratteggiato: accesi in più d'uno, i riempimenti si sommavano e annacquavano
                // le minime, che sono il contenuto della sezione — l'AoR qui è un riferimento, non un dato.
                return L.polygon(r, { color: col, weight: 2, dashArray: '7,5', fill: false, interactive: false });
            }));
        });
        L.control.layers(maps, Object.keys(overlays).length ? overlays : null, { collapsed: true }).addTo(map);

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
