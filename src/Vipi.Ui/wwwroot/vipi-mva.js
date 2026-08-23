// Carta delle minime di vettoramento (MRVA): disegna il contenuto di un file .mva su una basemap TOPOGRAFICA.
// Idempotente: ogni contenitore .mva-leaflet[data-mva] è inizializzato una sola volta (data-init), come vipi-aor.js.
//
// La basemap di partenza è il rilievo SENZA STRADE, e non è una scelta estetica: la MRVA di una zona dipende
// dall'orografia, e col terreno sotto i poligoni si legge PERCHÉ in quel punto la minima è quella. La rete
// stradale, invece, non c'entra nulla e toglie contrasto ai tracciati.
(function () {
    // I fondi. Le chiavi (`relief`/`contour`) sono IDENTIFICATORI, non nomi da mostrare: il nome visibile lo
    // scrive MinimaSection.razor sulle chip, localizzato. Il primo è quello che si vede all'apertura ed è SENZA STRADE: qui la rete stradale non aggiunge
    // niente e ruba leggibilità ai tracciati, mentre il rilievo è il motivo per cui la carta sta su una mappa.
    // Positron è sparito per la stessa ragione: neutro sì, ma è una mappa di strade senza rilievo.
    function basemaps() {
        return {
            // Partenza: DUE tile impilate, non una. Provate separatamente non bastavano — «World Terrain Base»
            // dà terra, mare e vegetazione ma a questi zoom le montagne quasi non si vedono; «World Hillshade»
            // dà il rilievo ma su fondo grigio uniforme, dove costa e mare spariscono. Insieme si leggono
            // entrambi, e nessuna delle due porta strade.
            relief: L.layerGroup([
                L.tileLayer('https://server.arcgisonline.com/arcgis/rest/services/World_Terrain_Base/MapServer/tile/{z}/{y}/{x}', {
                    maxZoom: 13, attribution: '© Esri — World Terrain Base'
                }),
                L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Elevation/World_Hillshade/MapServer/tile/{z}/{y}/{x}', {
                    maxZoom: 16, opacity: 0.55, attribution: '© Esri — Elevation/World Hillshade'
                })
            ]),
            // Unico fondo con le strade, e l'unico con le QUOTE scritte sulle curve di livello: resta come scelta
            // esplicita per chi vuole leggere l'altitudine del suolo, non come partenza.
            contour: L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
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

        // Fondo mappa: uno alla volta, pilotato dalle chip `.mva-base` (vedi onMvaClick).
        var maps = basemaps(), baseKey = 'relief';
        maps[baseKey].addTo(map);
        el._mvaSetBase = function (key) {
            if (!maps[key] || key === baseKey) return;
            map.removeLayer(maps[baseKey]);
            baseKey = key;
            maps[key].addTo(map);
        };

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
            // interactive:false e NESSUN tooltip: il nome del gruppo (`ZONA1`, `RR US0`, l'ICAO…) è un dettaglio
            // interno del file, e appariva come un riquadro appiccicato al puntatore su ogni tracciato — rumore
            // sopra la cosa che si sta guardando.
            var layer = s.c
                ? L.polygon(pts, { color: MVA_COLOR, weight: 2.5, fillColor: MVA_COLOR, fillOpacity: 0.05, interactive: false })
                : L.polyline(pts, { color: MVA_COLOR, weight: 2.5, interactive: false });
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
        //
        // Stessa interfaccia di vipi-aor.js (`_secMap` + `_aorSetSec`): è quella che le chip conoscono, e tenerla
        // uguale è il motivo per cui il gestore qui sotto è la copia corta di `onAorClick`.
        var secMap = {};
        aor.forEach(function (s) {
            var rings = (s.rings || []).filter(function (r) { return r && r.length >= 3; });
            if (!rings.length) return;
            var col = color(s.color, '--ivao-lightblue');
            secMap[(s.sec || '').toUpperCase()] = {
                on: false,
                layers: rings.map(function (r) {
                    // Solo contorno tratteggiato: accesi in più d'uno, i riempimenti si sommavano e annacquavano
                    // le minime, che sono il contenuto della sezione — l'AoR qui è un riferimento, non un dato.
                    return L.polygon(r, { color: col, weight: 2, dashArray: '7,5', fill: false, interactive: false });
                })
            };
        });
        el._secMap = secMap;
        el._aorSetSec = function (sec, on) {
            var e = secMap[(sec || '').toUpperCase()];
            if (!e) return;
            e.on = !!on;
            e.layers.forEach(function (l) { on ? l.addTo(map) : map.removeLayer(l); });
        };

        var bounds = boundsOf();
        if (bounds) { fitBox(bounds); map.fitBounds(bounds, { padding: [18, 18] }); }

        // La carta tiene la LARGHEZZA PIENA, come la mappa dell'AoR: è la misura a cui l'occhio è abituato nel
        // resto del documento, e restringerla per far quadrare l'aspetto — come si faceva prima — rendeva le due
        // mappe della stessa pagina di formato diverso. Si adatta quindi la sola ALTEZZA: su dati alti e stretti
        // (LIBB: 5,5° di latitudine per 3,1° di longitudine) l'inquadratura lavora sull'altezza, e darne di più
        // è l'unico modo di ingrandire il disegno senza toccare la larghezza.
        function fitBox(b) {
            var latSpan = b.getNorth() - b.getSouth();
            var lonSpan = (b.getEast() - b.getWest()) * Math.cos((b.getNorth() + b.getSouth()) / 2 * Math.PI / 180);
            if (latSpan <= 0 || lonSpan <= 0) return;
            var aspect = lonSpan / latSpan;                      // >1 larga, <1 alta
            var avail = el.parentElement ? el.parentElement.clientWidth : el.clientWidth;
            if (!avail) return;
            var h = Math.round(Math.min(620, Math.max(360, avail / aspect)));
            el.style.height = h + 'px';
            map.invalidateSize();
        }
        // Stesso contratto dell'AoR (vedi wirePrint in vipi-ui.js): la stampa riduce il contenitore e deve
        // riadattare l'inquadratura invece di ritagliarla.
        el._aorBounds = bounds;
        el._aorRefit = function () { if (bounds) map.fitBounds(bounds, { padding: [18, 18] }); };
        setTimeout(function () { map.invalidateSize(); if (bounds) map.fitBounds(bounds, { padding: [18, 18] }); }, 60);
    }

    // Stato acceso/spento di una chip: classe `.on` per l'occhio, `aria-pressed` per tutto il resto — le due
    // cose si scrivono INSIEME, da qui (stessa `segna` di vipi-aor.js, stesso motivo).
    function segna(el, on) {
        if (!el) return;
        el.classList.toggle('on', !!on);
        el.setAttribute('aria-pressed', on ? 'true' : 'false');
    }

    // Interazione chip via EVENT DELEGATION (installata una volta): robusta a qualsiasi re-render di Blazor,
    // niente listener per-elemento da riattaccare. Le chip vivono nel `.mva-block` genitore.
    //
    // Non tocca `.aor-chip`/`.cfg-btn` di vipi-aor.js e viceversa: là il gestore esce se non trova un
    // `.aor-block` sopra, qui se non trova un `.mva-block`.
    function onMvaClick(ev) {
        var t = ev.target && ev.target.closest ? ev.target.closest('.mva-chip,.mva-all,.mva-base') : null;
        if (!t) return;
        var block = t.closest('.mva-block');
        if (!block) return;
        var lf = block.querySelector('.mva-leaflet');

        function setSec(sec, on) { if (lf && lf._aorSetSec) lf._aorSetSec(sec, on); }
        // Verità = stato del layer (secMap), non la classe, che un re-render può desincronizzare.
        function isOn(sec) {
            var e = lf && lf._secMap && lf._secMap[(sec || '').toUpperCase()];
            return e ? !!e.on : t.classList.contains('on');
        }

        if (t.classList.contains('mva-chip')) {
            var nn = !isOn(t.dataset.sec);
            segna(t, nn);
            setSec(t.dataset.sec, nn);
            // Nessun refit: l'inquadratura è quella delle minime e non insegue l'AoR (vedi sopra).
        } else if (t.classList.contains('mva-all')) {
            var allOn = t.dataset.act === 'all';
            block.querySelectorAll('.mva-chip').forEach(function (ch) {
                segna(ch, allOn);
                setSec(ch.dataset.sec, allOn);
            });
        } else if (t.classList.contains('mva-base')) {
            // Scelta singola: acceso solo quello premuto.
            block.querySelectorAll('.mva-base').forEach(function (b) { segna(b, b === t); });
            if (lf && lf._mvaSetBase) lf._mvaSetBase(t.dataset.base || 'relief');
        }
    }
    document.addEventListener('click', onMvaClick);

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
