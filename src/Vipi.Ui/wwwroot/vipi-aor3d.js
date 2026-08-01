// Viewer 3D dell'AoR (Three.js, orbita manuale — nessun OrbitControls).
// Ogni settore è un prisma estruso: base = poligono shape reale proiettato su piano XY (Web Mercator centrato
// sul bbox), altezza = banda FL (bottom→top). Legenda toggle, orbita drag, zoom rotella, reset.
// Idempotente: ogni .aor3d-stage[data-sectors3d] è inizializzato una sola volta (data-init). Fallback se manca WebGL/THREE.
// Guidato dagli stessi dati dell'AoR 2D (vedi AccAor3d.razor): data-sectors3d = [{sec,name,color,rings:[[[lat,lon],…]],fl:[bottom,top]}].
// three.js NON è nel <head>: sono 592 KB per il solo tab 3D. Lo carica loadThree() alla prima apertura di uno stage.
(function () {
    // URL di three.js (con l'impronta di MapStaticAssets) dal data attribute sul nostro <script>.
    // document.currentScript va letto QUI: dentro una funzione chiamata dopo varrebbe null.
    var SELF = document.currentScript;
    var THREE_SRC = (SELF && SELF.getAttribute('data-three-src')) || '';
    var threePromise = null;

    /// Carica three.js una sola volta. La promise è memorizzata anche se fallisce: niente tempeste di retry,
    /// coerente con il comportamento di prima (fallback mostrato una volta, nessun secondo tentativo).
    function loadThree() {
        if (window.THREE) return Promise.resolve();
        if (threePromise) return threePromise;
        threePromise = new Promise(function (resolve, reject) {
            if (!THREE_SRC) { reject(new Error('data-three-src assente sul tag di vipi-aor3d.js')); return; }
            var s = document.createElement('script');
            s.src = THREE_SRC;
            s.onload = function () {
                if (window.THREE) resolve();
                else reject(new Error('three.js caricato ma THREE non è globale'));
            };
            s.onerror = function () { reject(new Error('three.js non caricato: ' + THREE_SRC)); };
            document.head.appendChild(s);
        });
        return threePromise;
    }

    var TARGET = 150;       // il lato orizzontale maggiore riempie ~questo numero di unità (z adattivo in build)
    var ZDEF = 0.5;         // fattore «Altezza» iniziale: i prismi a piena scala erano torri illeggibili
    var clamp = function (v, a, b) { return Math.max(a, Math.min(b, v)); };
    var hex = function (c) { return (c && c[0] === '#') ? c : ('#' + String(c || '3C55AC')); };
    // Nomi/callsign finiscono in innerHTML (legenda): arrivano dal DB, quindi passano da qui.
    var esc = function (t) { return String(t == null ? '' : t).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); };

    // --- Web Mercator + tile helpers (per allineare le tile della basemap ai poligoni proiettati) ---
    var D2R = Math.PI / 180, R2D = 180 / Math.PI;
    function mercY(lat) { return Math.log(Math.tan(Math.PI / 4 + lat * D2R / 2)) * R2D; }   // lat → Y mercatore (in "gradi")
    function lon2tile(lon, z) { return (lon + 180) / 360 * Math.pow(2, z); }
    function lat2tile(lat, z) { return (1 - Math.log(Math.tan(lat * D2R) + 1 / Math.cos(lat * D2R)) / Math.PI) / 2 * Math.pow(2, z); }
    function tile2lon(x, z) { return x / Math.pow(2, z) * 360 - 180; }
    function tile2lat(y, z) { var n = Math.PI - 2 * Math.PI * y / Math.pow(2, z); return R2D * Math.atan(0.5 * (Math.exp(n) - Math.exp(-n))); }

    // Proiezione condivisa lat/lon → piano XY in WEB MERCATOR (x=lon, y=mercY(lat)), centrata sul bbox e scalata così
    // che il lato maggiore = TARGET. Mercatore (non equirettangolare) così le tile della basemap combaciano coi poligoni.
    // Ritorna { project:(lat,lon)→[X,Y], minLat,maxLat,minLon,maxLon } o null se dati degeneri.
    function makeProjector(sectors) {
        var all = [];
        sectors.forEach(function (s) {
            (s.rings || []).forEach(function (r) { (r || []).forEach(function (p) { if (p && p.length >= 2) all.push(p); }); });
        });
        if (all.length < 3) return null;
        var lats = all.map(function (p) { return p[0]; }), lons = all.map(function (p) { return p[1]; });
        var minLat = Math.min.apply(null, lats), maxLat = Math.max.apply(null, lats);
        var minLon = Math.min.apply(null, lons), maxLon = Math.max.apply(null, lons);
        var minX = minLon, maxX = maxLon, minY = mercY(minLat), maxY = mercY(maxLat);
        var cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        var span = Math.max(maxX - minX, maxY - minY);
        var scale = span > 0 ? TARGET / span : 1;
        return {
            project: function (lat, lon) { return [(lon - cx) * scale, (mercY(lat) - cy) * scale]; },
            minLat: minLat, maxLat: maxLat, minLon: minLon, maxLon: maxLon
        };
    }

    // Pavimento = mappa geografica reale: cuce le tile CartoDB Positron che coprono il bbox e le applica come texture su
    // un piano a z=0. crossOrigin='anonymous' (le tile mandano ACAO:*) → niente canvas "tainted". Fallimento rete/CORS =
    // nessuna basemap (resta la griglia). onReady(plane) al completamento per il primo render.
    function buildBasemap(THREE, proj, onReady) {
        // Zoom massimo che tiene la griglia di tile piccola (≤ ~20 tile).
        var z = 11, tx0, tx1, ty0, ty1;
        for (; z >= 2; z--) {
            tx0 = Math.floor(lon2tile(proj.minLon, z)); tx1 = Math.floor(lon2tile(proj.maxLon, z));
            ty0 = Math.floor(lat2tile(proj.maxLat, z)); ty1 = Math.floor(lat2tile(proj.minLat, z));   // lat alta = tile y bassa
            if ((tx1 - tx0 + 1) * (ty1 - ty0 + 1) <= 20) break;
        }
        var cols = tx1 - tx0 + 1, rows = ty1 - ty0 + 1;
        var canvas = document.createElement('canvas'); canvas.width = cols * 256; canvas.height = rows * 256;
        var g = canvas.getContext('2d');
        g.fillStyle = '#eef1f7'; g.fillRect(0, 0, canvas.width, canvas.height);

        var texture = new THREE.CanvasTexture(canvas);
        // Estensione geografica della griglia di tile → angoli in XY (stessa proiezione dei poligoni). Il piano vi si adatta.
        var west = tile2lon(tx0, z), east = tile2lon(tx1 + 1, z);
        var north = tile2lat(ty0, z), south = tile2lat(ty1 + 1, z);
        var nw = proj.project(north, west), se = proj.project(south, east);
        var w = Math.abs(se[0] - nw[0]), h = Math.abs(nw[1] - se[1]);
        var geo = new THREE.PlaneGeometry(w, h);
        var mat = new THREE.MeshBasicMaterial({ map: texture, transparent: true, opacity: 0.95, depthWrite: false });
        var plane = new THREE.Mesh(geo, mat);
        plane.position.set((nw[0] + se[0]) / 2, (nw[1] + se[1]) / 2, -0.5);   // sotto i prismi (z≥0)

        // Carica e disegna ogni tile; ridisegna la texture quando tutte sono pronte.
        var pending = cols * rows, ok = 0;
        var subs = ['a', 'b', 'c', 'd'];
        for (var ix = tx0; ix <= tx1; ix++) {
            for (var iy = ty0; iy <= ty1; iy++) {
                (function (col, row, tileX, tileY) {
                    var img = new Image();
                    img.crossOrigin = 'anonymous';
                    img.onload = function () {
                        try { g.drawImage(img, col * 256, row * 256, 256, 256); ok++; } catch (e) { }
                        if (--pending === 0) { texture.needsUpdate = true; if (onReady) onReady(ok > 0 ? plane : null); }
                    };
                    img.onerror = function () { if (--pending === 0) { texture.needsUpdate = true; if (onReady) onReady(ok > 0 ? plane : null); } };
                    img.src = 'https://' + subs[(tileX + tileY) % 4] + '.basemaps.cartocdn.com/light_all/' + z + '/' + tileX + '/' + tileY + '@2x.png';
                })(ix - tx0, iy - ty0, ix, iy);
            }
        }
        return plane;
    }

    function build(stage, sectors, onBasemap) {
        var THREE = window.THREE;
        var w = stage.clientWidth || 800, h = stage.clientHeight || 540;
        var proj = makeProjector(sectors);
        if (!proj) return null;
        var project = proj.project;

        var scene = new THREE.Scene();
        var camera = new THREE.PerspectiveCamera(45, w / h, 1, 5000); camera.up.set(0, 0, 1);
        var renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setPixelRatio(window.devicePixelRatio || 1); renderer.setSize(w, h);
        stage.insertBefore(renderer.domElement, stage.firstChild);

        scene.add(new THREE.AmbientLight(0xffffff, 0.92));
        var dl = new THREE.DirectionalLight(0xffffff, 0.45); dl.position.set(80, 60, 200); scene.add(dl);
        var grid = new THREE.GridHelper(TARGET * 1.3, 18, 0xc3cdf0, 0xe0e5f6); grid.rotation.x = Math.PI / 2;
        grid.visible = false; scene.add(grid);   // nascosta di default: si mostra se la basemap non c'è / è spenta

        // Pavimento = mappa geografica (asincrono): al termine lo aggiunge alla scena e notifica per il re-render.
        var basemap = buildBasemap(THREE, proj, function (plane) {
            if (plane) { scene.add(plane); } else { grid.visible = true; }
            if (onBasemap) onBasemap(plane);
        });

        // Scala verticale ADATTIVA di RIFERIMENTO (= fattore ×1 del selettore «Altezza»): l'altezza max dei prismi
        // ≈ 55% del lato orizzontale. Il fattore scelto dall'utente si applica dopo, come group.scale.z (vedi
        // build3d): scala insieme geometrie, quote di base e ancore delle etichette senza ricostruire nulla.
        // maxHeight torna alla camera per inquadrare a metà.
        var maxTop = 1;
        sectors.forEach(function (s) { var b = s.fl || [0, 660]; if ((b[1] || 660) > maxTop) maxTop = b[1] || 660; });
        var flz = (TARGET * 0.55) / maxTop;
        var maxHeight = maxTop * flz;

        // Disegno prima i settori con footprint più grande: i più piccoli (interni) restano visibili sopra.
        var order = sectors.map(function (s, i) { return i; }).sort(function (a, b) { return ringArea(sectors[b]) - ringArea(sectors[a]); });

        var group = new THREE.Group(); scene.add(group);
        order.forEach(function (idx) {
            var s = sectors[idx];
            var band = s.fl || [0, 660];
            var bottom = band[0] || 0, top = band[1] || 660;
            var depth = Math.max(1, (top - bottom)) * flz;
            var col = new THREE.Color(hex(s.color));
            var edgeCol = col.clone().multiplyScalar(0.72);   // spigolo più scuro = stacco netto sulla mappa chiara
            var secGroup = new THREE.Group(); secGroup.position.z = bottom * flz;

            var cxSum = 0, cySum = 0, n = 0;
            (s.rings || []).forEach(function (r) {
                var pts = (r || []).map(function (p) { return project(p[0], p[1]); });
                if (pts.length < 3) return;
                var shape = new THREE.Shape();
                pts.forEach(function (p, i) { i ? shape.lineTo(p[0], p[1]) : shape.moveTo(p[0], p[1]); });
                shape.closePath();
                var geo = new THREE.ExtrudeGeometry(shape, { depth: depth, bevelEnabled: false });
                var mesh = new THREE.Mesh(geo, new THREE.MeshLambertMaterial({ color: col, transparent: true, opacity: 0.16, depthWrite: false }));
                var edges = new THREE.LineSegments(new THREE.EdgesGeometry(geo), new THREE.LineBasicMaterial({ color: edgeCol, transparent: true, opacity: 0.9 }));
                secGroup.add(mesh); secGroup.add(edges);
                pts.forEach(function (p) { cxSum += p[0]; cySum += p[1]; n++; });
            });
            if (n === 0) return;
            // Ancora dell'etichetta HTML (vedi layoutLabels): oggetto vuoto sul centroide, in cima al prisma. Sta DENTRO
            // secGroup, quindi segue la scala verticale del gruppo; il testo è HTML e non si deforma con essa.
            var anchor = new THREE.Object3D();
            anchor.position.set(cxSum / n, cySum / n, depth);
            secGroup.add(anchor);
            s._anchor = anchor;
            s._ink = '#' + edgeCol.getHexString();
            s._area = ringArea(s);
            s._g = secGroup;
            group.add(secGroup);
        });

        return { scene: scene, camera: camera, renderer: renderer, group: group, grid: grid, maxHeight: maxHeight, getBasemap: function () { return basemap; } };
    }

    // Area (relativa) del primo anello di un settore, per ordinare i prismi dal più grande al più piccolo (shoelace su XY grezzi).
    function ringArea(s) {
        var r = s && s.rings && s.rings[0]; if (!r || r.length < 3) return 0;
        var a = 0; for (var i = 0, j = r.length - 1; i < r.length; j = i++) { a += (r[j][1] + r[i][1]) * (r[j][0] - r[i][0]); }
        return Math.abs(a / 2);
    }

    // --- Etichette = overlay HTML sopra il canvas (prima erano sprite su canvas 256×64: i nomi lunghi venivano
    // tagliati e i doppioni per nome saltati). Una .aor3d-lab per settore, riposizionata a ogni render proiettando
    // l'ancora 3D in coordinate schermo. Declutter greedy: priorità al footprint più grande, chi collide prova
    // qualche offset verticale e poi sparisce — meglio nessuna etichetta che una pila illeggibile.
    // Testo = callsign (come le chip del 2D), sotto la banda FL; il nome esteso è nel title.
    function buildLabels(stage, sectors, onToggle) {
        var THREE = window.THREE;
        var layer = document.createElement('div');
        layer.className = 'aor3d-labels';
        stage.appendChild(layer);

        var items = [];
        sectors.forEach(function (s) {
            if (!s._anchor) return;
            var b = s.fl || [0, 660];
            var el = document.createElement('div');
            el.className = 'aor3d-lab';
            el.style.color = s._ink || '';
            el.title = s.name || s.sec || '';
            var nm = document.createElement('b'); nm.textContent = s.sec || s.name || '';
            var fl = document.createElement('i'); fl.textContent = 'FL' + (b[0] || 0) + '–' + (b[1] || 660);
            el.appendChild(nm); el.appendChild(fl);
            // Il pointerdown NON deve arrivare allo stage: là parte l'orbita e con essa setPointerCapture, che
            // ridirige il pointerup sullo stage — il click finirebbe sull'antenato comune e l'etichetta non lo
            // vedrebbe mai. Fermandolo qui il click arriva, e trascinare da un'etichetta semplicemente non ruota.
            el.addEventListener('pointerdown', function (ev) { ev.stopPropagation(); });
            el.addEventListener('click', function (ev) { ev.stopPropagation(); if (onToggle) onToggle(s); });
            layer.appendChild(el);
            items.push({ s: s, el: el, w: 0, h: 0 });
        });
        // Priorità di piazzamento: footprint grande prima (le etichette dei settori piccoli cedono per prime).
        items.sort(function (a, b) { return (b.s._area || 0) - (a.s._area || 0); });

        var OFFS = [0, -22, 22, -44, 44];      // px: tentativi verticali prima di rinunciare
        var PAD = 4;
        var v = new THREE.Vector3();

        function hits(x, y, w, h, boxes) {
            for (var i = 0; i < boxes.length; i++) {
                var b = boxes[i];
                if (x < b[0] + b[2] + PAD && x + w + PAD > b[0] && y < b[1] + b[3] + PAD && y + h + PAD > b[1]) return true;
            }
            return false;
        }
        // Legenda e suggerimento sono UI fissa: le etichette le trattano come ostacoli invece di finirci sotto.
        function chrome() {
            var out = [], r0 = stage.getBoundingClientRect();
            stage.querySelectorAll('.aor3d-legend, .aor3d-hint').forEach(function (el) {
                if (!el.offsetParent) return;
                var r = el.getBoundingClientRect();
                out.push([r.left - r0.left, r.top - r0.top, r.width, r.height]);
            });
            return out;
        }

        function layout(camera) {
            var W = stage.clientWidth, H = stage.clientHeight;
            if (!W || !H) return;
            var placed = chrome();
            items.forEach(function (it) {
                var el = it.el;
                if (!it.s._g || !it.s._g.visible) { el.style.display = 'none'; return; }
                el.style.display = '';                                   // deve essere reso per misurarlo
                if (!it.w) { it.w = el.offsetWidth; it.h = el.offsetHeight; }
                it.s._anchor.getWorldPosition(v).project(camera);
                if (v.z > 1) { el.style.display = 'none'; return; }      // dietro la camera
                var x = (v.x * 0.5 + 0.5) * W, y = (-v.y * 0.5 + 0.5) * H;
                var bx = 0, by = 0, ok = false;
                for (var i = 0; i < OFFS.length && !ok; i++) {
                    bx = x - it.w / 2; by = y - it.h - 8 + OFFS[i];
                    if (bx < 2 || bx + it.w > W - 2 || by < 2 || by + it.h > H - 2) continue;
                    if (!hits(bx, by, it.w, it.h, placed)) ok = true;
                }
                if (!ok) { el.style.display = 'none'; return; }
                placed.push([bx, by, it.w, it.h]);
                el.style.transform = 'translate(' + Math.round(bx) + 'px,' + Math.round(by) + 'px)';
            });
        }

        return { layout: layout };
    }

    /// Punto d'ingresso: garantisce three.js, poi costruisce. Tutti i chiamanti (tab 3D, <details>, initAll,
    /// MutationObserver) passano di qui e ignorano il valore di ritorno, quindi l'attesa resta interna.
    function initOne(stage) {
        // 'pending' = caricamento di three.js in corso per questo stage: senza, un secondo evento (resize,
        // re-render di Blazor) rientrerebbe e costruirebbe due volte lo stesso stage.
        if (stage.dataset.init === '1' || stage.dataset.init === 'pending') return;
        var sectors;
        try { sectors = JSON.parse(stage.dataset.sectors3d || '[]') || []; } catch (e) { return; }
        if (!sectors.length) return;

        if (window.THREE) { build3d(stage, sectors); return; }

        stage.dataset.init = 'pending';
        loadThree().then(
            function () { stage.dataset.init = ''; build3d(stage, sectors); },
            function () {
                // three.js non disponibile (rete, blocco, file mancante): fallback testuale, nessun ritentativo.
                stage.dataset.init = '1';
                var fb = stage.querySelector('.aor3d-fallback'); if (fb) fb.style.display = 'flex';
            });
    }

    function build3d(stage, sectors) {
        var ctx;
        // WebGL assente/disabilitato: `new THREE.WebGLRenderer` lancia. Prima l'eccezione usciva e lo stage
        // restava vuoto, nonostante il fallback previsto in cima al file — qui viene mostrato davvero.
        try { ctx = build(stage, sectors, function () { render(); }); }   // re-render quando la basemap è pronta
        catch (e) {
            stage.dataset.init = '1';
            var fbw = stage.querySelector('.aor3d-fallback'); if (fbw) fbw.style.display = 'flex';
            return;
        }
        if (!ctx) return;
        stage.dataset.init = '1';

        var zf = ZDEF;                                           // fattore «Altezza» corrente (selettore in barra)
        ctx.group.scale.z = zf;
        var lookZ = (ctx.maxHeight || 80) * zf * 0.4;            // mira a ~40% dell'altezza dei prismi
        var DEF = { theta: 0.78, phi: 1.02, radius: 300 };       // vista un po' più alta e arretrata di prima
        var theta = DEF.theta, phi = DEF.phi, radius = DEF.radius;
        var box = stage.querySelector('.aor3d-legrows');

        // Verità dello stato acceso/spento: `secMap` (gemello di quello del layer Leaflet in vipi-aor.js, che lo
        // legge per sapere se una chip è accesa). Legenda, chip ed etichette sono tre viste della stessa cosa.
        var secMap = {};
        sectors.forEach(function (s) { secMap[(s.sec || '').toUpperCase()] = { on: true }; });
        stage._secMap = secMap;

        function setSec(sec, on) {
            var key = (sec || '').toUpperCase();
            var e = secMap[key];
            if (!e) return;
            e.on = on;
            sectors.forEach(function (s, i) {
                if ((s.sec || '').toUpperCase() !== key || !s._g) return;
                s._g.visible = on;
                var row = box && box.querySelector('.lg-row[data-i="' + i + '"]');
                if (row) row.classList.toggle('off', !on);
            });
            var blk = stage.closest('.aor-block');
            if (blk) blk.querySelectorAll('.aor-chip').forEach(function (c) {
                if ((c.dataset.sec || '').toUpperCase() === key) c.classList.toggle('on', on);
            });
            render();
        }
        // Stessa interfaccia del contenitore Leaflet: così le chip del 2D pilotano anche il 3D senza logica duplicata.
        stage._aorSetSec = setSec;
        // Le etichette si posizionano DOPO il render (matrici mondo fresche) e solo lì: render() è on-demand
        // (drag, zoom, toggle), non un loop raf, quindi il costo del declutter è trascurabile.
        var labels = buildLabels(stage, sectors, function (s) { setSec(s.sec, !(s._g && s._g.visible)); });
        function render() {
            if (!ctx) return;
            ctx.renderer.render(ctx.scene, ctx.camera);
            labels.layout(ctx.camera);
        }
        function updateCam() {
            ctx.camera.position.set(
                radius * Math.sin(phi) * Math.cos(theta),
                radius * Math.sin(phi) * Math.sin(theta),
                radius * Math.cos(phi) + lookZ);
            ctx.camera.lookAt(0, 0, lookZ); render();
        }
        function resize() {
            var w = stage.clientWidth, h = stage.clientHeight;
            if (w && h) { ctx.camera.aspect = w / h; ctx.camera.updateProjectionMatrix(); ctx.renderer.setSize(w, h); render(); }
        }
        stage._aor3dResize = resize;
        updateCam();

        // Legenda (toggle visibilità settore). Etichetta = callsign col nome esteso nel title, come le chip del 2D.
        // Resta anche a schermo intero, dove le chip fuori dallo stage non si vedono.
        if (box) {
            // Stessa ragione delle etichette: senza fermare il pointerdown lo stage cattura il puntatore e il click
            // sulla riga non arriva mai (la legenda, per giunta, deve poter scorrere senza far ruotare la scena).
            var legend = stage.querySelector('.aor3d-legend');
            if (legend) legend.addEventListener('pointerdown', function (ev) { ev.stopPropagation(); });
            box.innerHTML = sectors.map(function (s, i) {
                var b = s.fl || [0, 660];
                return '<div class="lg-row" data-i="' + i + '" title="' + esc(s.name || '') + '"><span class="sw" style="background:' + hex(s.color) +
                    '"></span>' + esc(s.sec || s.name || '') + '<span class="fl">FL' + (b[0] || 0) + '–' + (b[1] || 660) + '</span></div>';
            }).join('');
            box.querySelectorAll('.lg-row').forEach(function (r) {
                r.addEventListener('click', function () {
                    var s = sectors[+r.dataset.i];
                    if (s && s._g) setSec(s.sec, !s._g.visible);
                });
            });
        }

        // Orbita manuale + zoom.
        var dragging = false, lx = 0, ly = 0;
        stage.addEventListener('pointerdown', function (e) { dragging = true; lx = e.clientX; ly = e.clientY; stage.classList.add('grabbing'); try { stage.setPointerCapture(e.pointerId); } catch (_) { } });
        stage.addEventListener('pointermove', function (e) { if (!dragging) return; theta -= (e.clientX - lx) * 0.006; phi = clamp(phi - (e.clientY - ly) * 0.006, 0.18, 1.45); lx = e.clientX; ly = e.clientY; updateCam(); });
        stage.addEventListener('pointerup', function () { dragging = false; stage.classList.remove('grabbing'); });
        stage.addEventListener('pointerleave', function () { dragging = false; stage.classList.remove('grabbing'); });
        stage.addEventListener('wheel', function (e) { e.preventDefault(); radius = clamp(radius + e.deltaY * 0.14, 110, 620); updateCam(); }, { passive: false });

        var rst = stage.parentElement && stage.parentElement.querySelector('.aor3d-reset');
        if (rst) rst.addEventListener('click', function () { theta = DEF.theta; phi = DEF.phi; radius = DEF.radius; setZ(ZDEF); });

        // Selettore «Altezza» (esagerazione verticale): scala il gruppo dei prismi sull'asse Z, niente ricostruzione.
        // La camera rimira a metà della nuova altezza, altrimenti a ×2 i settori escono dall'inquadratura.
        var zBtns = stage.parentElement ? stage.parentElement.querySelectorAll('.aor3d-z') : [];
        function setZ(v) {
            zf = v;
            ctx.group.scale.z = zf;
            lookZ = (ctx.maxHeight || 80) * zf * 0.4;
            // Anche la distanza segue il fattore: a ×2 i prismi sfondavano l'inquadratura. Cambiare l'altezza è
            // un'azione di inquadratura, quindi ricalcolare lo zoom qui è coerente (la rotella resta libera dopo).
            radius = clamp(DEF.radius * (0.8 + 0.5 * zf), 110, 620);
            zBtns.forEach(function (b) { b.classList.toggle('on', parseFloat(b.dataset.z) === zf); });
            updateCam();
        }
        zBtns.forEach(function (b) { b.addEventListener('click', function () { setZ(parseFloat(b.dataset.z) || 1); }); });
        setZ(zf);

        // Schermo intero (Fullscreen API sullo stage): il canvas riempie il viewport; resize al cambio stato.
        var full = stage.parentElement && stage.parentElement.querySelector('.aor3d-full');
        if (full) full.addEventListener('click', function () {
            if (document.fullscreenElement) { document.exitFullscreen(); return; }
            if (stage.requestFullscreen) stage.requestFullscreen();
        });
        document.addEventListener('fullscreenchange', function () { setTimeout(resize, 60); });

        // Toggle «Mappa base»: mostra/nasconde il pavimento geografico (e, in alternanza, la griglia).
        var mapBtn = stage.parentElement && stage.parentElement.querySelector('.aor3d-basemap');
        if (mapBtn) mapBtn.addEventListener('click', function () {
            var plane = ctx.getBasemap && ctx.getBasemap();
            var on = mapBtn.classList.toggle('on');
            if (plane) plane.visible = on;
            if (ctx.grid) ctx.grid.visible = !on || !plane;   // senza basemap resta la griglia
            render();
        });

        // Ricalcolo dimensioni quando il contenitore diventa visibile/ridimensiona.
        if (window.ResizeObserver) { var ro = new ResizeObserver(function () { resize(); }); ro.observe(stage); }
        setTimeout(resize, 60);
    }

    // Tab 2D/3D del blocco AoR (event delegation, robusto ai re-render di Blazor). Lo stage 3D è inizializzato in
    // modo pigro solo alla prima apertura della vista 3D (evita un contesto WebGL se l'utente resta sul 2D).
    function onViewTab(ev) {
        var t = ev.target && ev.target.closest ? ev.target.closest('.aor-vm-btn') : null;
        if (!t) return;
        var bar = t.closest('.aor-viewmode');
        if (!bar) return;
        var view = t.dataset.view;
        bar.querySelectorAll('.aor-vm-btn').forEach(function (b) { b.classList.toggle('on', b === t); });
        // Le due viste sono i fratelli .aor-view subito dopo la barra.
        var node = bar.nextElementSibling, is3d = view === '3d';
        while (node) {
            if (node.classList && node.classList.contains('aor-view')) {
                var match = node.classList.contains(is3d ? 'aor-view-3d' : 'aor-view-2d');
                node.hidden = !match;
                if (match) {
                    node.querySelectorAll('.aor3d-stage').forEach(function (el) {
                        if (el.dataset.init === '1') { if (el._aor3dResize) setTimeout(el._aor3dResize, 60); }
                        else initOne(el);
                    });
                    // Ricalcola anche le mappe Leaflet tornando al 2D (partite a dimensione 0 se erano nascoste).
                    node.querySelectorAll('.aor-leaflet').forEach(function (el) {
                        if (el._leafletMap) setTimeout(function () { el._leafletMap.invalidateSize(); if (el._aorRefit) el._aorRefit(); }, 60);
                    });
                }
            }
            node = node.nextElementSibling;
        }
    }
    document.addEventListener('click', onViewTab);

    function initAll() {
        // Solo gli stage VISIBILI: quelli in una vista 3D nascosta (tab non aperto) si inizializzano pigramente
        // al primo click sul tab (onViewTab). offsetParent === null ⇒ un antenato ha display:none.
        document.querySelectorAll('.aor3d-stage').forEach(function (el) { if (el.offsetParent !== null) initOne(el); });
    }
    window.vipiInitAor3d = initAll;
    document.addEventListener('DOMContentLoaded', initAll);

    // Stage dentro <details>/tab nascosti partono a dimensione 0: al toggle inizializza/ricalcola.
    document.addEventListener('toggle', function (ev) {
        var d = ev.target;
        if (!d || d.tagName !== 'DETAILS' || !d.open) return;
        d.querySelectorAll('.aor3d-stage').forEach(function (el) {
            if (el.dataset.init === '1') { if (el._aor3dResize) setTimeout(el._aor3dResize, 60); }
            else initOne(el);
        });
    }, true);

    // Re-init dopo i render di Blazor (enhanced/interattivo).
    var pending = false;
    var obs = new MutationObserver(function () {
        if (pending) return;
        pending = true;
        setTimeout(function () { pending = false; initAll(); }, 90);
    });
    if (document.body) obs.observe(document.body, { childList: true, subtree: true });
})();
