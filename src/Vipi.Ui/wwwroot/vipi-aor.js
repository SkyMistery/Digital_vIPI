// Mappa AOR: disegna il poligono shape reale su una basemap minimal (CartoDB Positron) via Leaflet.
// Idempotente: ogni contenitore .aor-leaflet[data-poly] è inizializzato una sola volta (data-init).
//
// ⚠️ Leaflet NON è nel <body> di ogni pagina: sono 162 KB (js + css) che servono alle sole pagine con una
// mappa, mentre quel <body> vale per ricerca, incarichi, elenchi admin, guida, login e hub. È la stessa
// regola che App.razor scriveva già per three.js e che a Leaflet non era stata applicata. Lo carica
// caricaLeaflet() alla prima `.aor-leaflet` incontrata; fino ad allora si vede il ripiego SVG, che è già
// in piedi e disegna il dato nostro.
(function () {
    // URL (con l'impronta di MapStaticAssets) dai data attribute sul nostro <script>. `document.currentScript`
    // va letto QUI: dentro una funzione chiamata dopo varrebbe null.
    var SELF = document.currentScript;
    var LEAFLET_SRC = (SELF && SELF.getAttribute('data-leaflet-src')) || '';
    var LEAFLET_CSS = (SELF && SELF.getAttribute('data-leaflet-css')) || '';
    var leafletPromise = null;

    /// Carica Leaflet una sola volta. La promise è memorizzata anche se fallisce: niente tempeste di retry,
    /// e il ripiego SVG resta quello che si vede — che è il comportamento di prima quando la CDN non
    /// rispondeva, salvo che ora i byte sono nostri e il caso non dovrebbe capitare.
    function caricaLeaflet() {
        if (window.L) return Promise.resolve();
        if (leafletPromise) return leafletPromise;
        leafletPromise = new Promise(function (resolve, reject) {
            if (!LEAFLET_SRC) { reject(new Error('data-leaflet-src assente sul tag di vipi-aor.js')); return; }
            // Il foglio prima dello script: Leaflet misura il contenitore appena parte, e senza le sue
            // regole quel contenitore ha l'altezza sbagliata.
            if (LEAFLET_CSS && !document.querySelector('link[data-leaflet-css]')) {
                var l = document.createElement('link');
                l.rel = 'stylesheet';
                l.href = LEAFLET_CSS;
                l.setAttribute('data-leaflet-css', '');
                document.head.appendChild(l);
            }
            var s = document.createElement('script');
            s.src = LEAFLET_SRC;
            s.onload = function () {
                if (window.L) resolve();
                else reject(new Error('Leaflet caricato ma L non è globale'));
            };
            s.onerror = function () { reject(new Error('Leaflet non caricato: ' + LEAFLET_SRC)); };
            document.head.appendChild(s);
        });
        return leafletPromise;
    }
    // Un colore che arriva dal DOM puo' essere un hex vero (override manuale dell'utente, che esce da
    // un <input type=color>) oppure il NOME di un token del tema (es. "--ivao-red"). Leaflet vuole un
    // colore vero: qui il token viene risolto una volta sola sul :root.
    // ⚠️ Restituire il token com'e' non funziona: Leaflet lo scrive in un attributo SVG `fill`, che
    // non fa la sostituzione di var() come farebbe una proprieta' CSS.
    function aorColor(v, fallback) {
        v = (v || '').trim();
        if (!v) v = fallback;
        if (v.indexOf('--') !== 0) return v;
        var r = getComputedStyle(document.documentElement).getPropertyValue(v).trim();
        return r || fallback;
    }

    // Stato acceso/spento di una chip: la classe `.on` per l'occhio, `aria-pressed` per tutto il resto.
    //
    // ⚠️ Le due cose si scrivono INSIEME, da qui, e non ognuna dove capita: fino al 23 agosto 2026 esisteva
    // solo la classe, e chi non vede la barra colorata non aveva modo di sapere quali settori fossero accesi.
    // I comandi sono passati da <span>/<a> a <button> (Chip.razor spiega perche' un comando che esiste solo
    // per il mouse non e' un comando); `aria-pressed` e' la meta' che il <button> non porta da solo.
    function segna(el, on) {
        if (!el) return;
        el.classList.toggle('on', !!on);
        el.setAttribute('aria-pressed', on ? 'true' : 'false');
    }

    /// Ritenta le tessere che non sono arrivate. **Leaflet non lo fa**: una tessera fallita resta grigia
    /// per sempre, e l'unico rimedio è ricaricare la pagina — che è esattamente il sintomo riferito dalla
    /// produzione («apro una vIPI, la mappa è a scacchi; refresho ed è a posto»: al secondo giro le
    /// tessere arrivano dalla cache del browser).
    ///
    /// Perché succede: una vIPI ACC porta **decine di mappe** (misurato su LIBB: 77, quasi tutte le mappine
    /// delle aree regolamentate) e all'apertura chiedono le tessere tutte insieme — 115 richieste in 19 ms a
    /// due host. Il browser ne tiene 6 per host, il fornitore di tessere limita la frequenza, e quel che
    /// cade non lo ripesca nessuno. Qui si ripesca: tre tentativi con attesa crescente e un po' di
    /// dispersione, così i ritenti non ripartono tutti nello stesso istante.
    ///
    /// ⚠️ La classe `leaflet-tile-loaded` la mette Leaflet **solo** quando il caricamento gli riesce: dopo
    /// un ritento andato bene va aggiunta a mano, o la tessera resta trasparente (la regola di dissolvenza
    /// del suo foglio parte da `opacity:0`).
    /// ⚠️ Cinque tentativi, non tre, e l'attesa raddoppia: 0,6s → 1,2 → 2,4 → 4,8 → 9,6, cioè quasi
    /// **venti secondi** coperti. Misurato: con tre tentativi ravvicinati, un'interruzione di dodici secondi
    /// se li mangiava tutti e restavano 31 mappe bucate su 77 — i ritenti finivano prima del guasto.
    var RITENTI_MAX = 5;

    /// Rimette in coda UNA tessera. La classe va aggiunta a mano (vedi sopra): Leaflet la mette solo quando
    /// il caricamento gli riesce, e senza di quella la tessera arriva ma resta trasparente.
    function richiedi(img, url) {
        if (!img.parentNode || !url) return;         // tessera già potata da Leaflet: non la si resuscita
        img.addEventListener('load', function () {
            img.classList.add('leaflet-tile-loaded');
            img.style.opacity = 1;
        }, { once: true });
        img.src = url;
    }

    function ritentaTessere(layer) {
        layer.on('tileerror', function (e) {
            var img = e.tile;
            if (!img || !img.parentNode) return;
            var fatti = Number(img.getAttribute('data-vipi-ritento') || 0);
            if (fatti >= RITENTI_MAX) return;
            img.setAttribute('data-vipi-ritento', fatti + 1);
            var url;
            try { url = e.coords ? layer.getTileUrl(e.coords) : img.src; } catch (err) { url = img.src; }
            if (!url) return;
            setTimeout(function () { richiedi(img, url); },
                       600 * Math.pow(2, fatti) + Math.round(Math.random() * 400));
        });
        avviaSpazzino();
        return layer;
    }

    /// Lo SPAZZINO: ripassa a intervalli e ripesca le tessere che la scala qui sopra ha abbandonato.
    ///
    /// Perché serve, misurato: la scala è cinque tentativi da 0,6s a 9,6s, cioè copre ~19 secondi. Un
    /// guasto più lungo se li mangia TUTTI mentre è ancora in corso, e dopo non riprova più nessuno:
    /// con un'interruzione di **30 secondi** restavano **25 tessere su 9 mappe** nere per sempre, immutate
    /// anche 35 secondi dopo la fine del guasto. L'unico rimedio era ricaricare la pagina — cioè di nuovo
    /// il sintomo che il ritentatore doveva togliere, solo con la soglia spostata più in là.
    ///
    /// Non è un doppione della scala: quella serve ai cali brevi e recupera in un secondo, questo copre le
    /// interruzioni lunghe senza tenere un timer per tessera. Guarda TUTTE le mappe in pagina (AoR e minime
    /// insieme), perché il guasto non distingue.
    ///
    /// ⚠️ Si guarda `naturalWidth`, non la classe: una tessera fallita resta `complete` con larghezza
    /// zero, e `complete` da solo direbbe che è a posto. Le tessere ancora IN VOLO (`complete` falso) non si
    /// toccano, o si raddoppierebbero le richieste proprio mentre la rete arranca.
    var SPAZZA_OGNI = 8000, SPAZZA_MAX = 20, SPAZZA_PER_TESSERA = 8;
    var spazzinoTimer = null, spazzate = 0, pulitiDiFila = 0;

    /// RINUNCIA AL FONDO: quando la basemap non arriva e non arriverà, si toglie invece di lasciarla a pezzi.
    ///
    /// Il ritentatore e lo spazzino coprono i guasti che passano. Contro un blocco STABILE — un'estensione
    /// che filtra `basemaps.cartocdn.com`, un DNS che non risolve, un fornitore che ci ha chiuso fuori — non
    /// esiste ritento che tenga: si riprova e si resta col riquadro a scacchi. Lì la cosa giusta non è
    /// insistere ma **smettere**: via il fondo, restano lo sfondo neutro e i nostri poligoni, che sono il
    /// dato che la mappa esiste per mostrare.
    ///
    /// È deliberatamente la STESSA FACCIA del ripiego SVG che si vede prima che Leaflet carichi
    /// (`background: var(--surface-soft)` + i poligoni), e per la stessa ragione: una mappa senza fondo si
    /// legge come una scelta, una mappa a scacchi si legge come un guasto. Nessuna etichetta, come là.
    /// Togliendo il foglio sparisce anche la sua attribuzione, ed è corretto: non stiamo più mostrando le
    /// tessere di nessuno.
    ///
    /// ⚠️ **Il conto sta sui giri dello SPAZZINO, non su `data-vipi-ritento`.** Quell'attributo si
    /// incrementa quando il ritento viene *programmato*, non quando fallisce: arriva a 5 già al nono secondo
    /// mentre l'ultimo tentativo è in volo fino al diciannovesimo. Contando quello, un guasto passeggero di
    /// 30 secondi — cioè il caso che lo spazzino recupera per intero — verrebbe scambiato per blocco
    /// permanente e perderebbe il fondo per niente. `data-vipi-spazzata` invece si alza solo DOPO che la
    /// scala veloce ha finito, ed è il segnale onesto.
    ///
    /// ⚠️ **E si può tornare indietro.** Rinunciare non è definitivo: una sonda leggera — UNA richiesta,
    /// non la raffica di tutte le mappe — riprova ogni mezzo minuto, e se la tessera arriva il fondo torna
    /// dove stava. Così un'interruzione lunga costa una mappa pulita per un po', non fino al prossimo
    /// ricaricamento.
    /// ⚠️ **QUATTRO giri, non tre, e la differenza è misurata.** Con tre si rinuncia al 32° secondo, che
    /// cade dentro la coda di un guasto passeggero di 30: il giro delle 32s vede ancora rotto quel che la
    /// sua stessa richiesta sta per riparare, e **38 mappe su 75 perdevano il fondo per una quarantina di
    /// secondi** — mappe che prima di questa aggiunta lo spazzino recuperava senza spegnere niente. Col
    /// quarto giro il guasto di 30s si ripara da sé e il conto si azzera; il blocco vero costa otto secondi
    /// in più di attesa, che è il prezzo giusto da pagare in quella direzione.
    var RINUNCIA_DOPO = 4, idMappa = 0;
    var senzaFondo = [], urlSonda = null, sondaTimer = null, sondeFatte = 0, SONDE_MAX = 20;

    function rinunciaAlFondo(el, campione) {
        if (!el || el.dataset.senzaFondo === '1') return;
        var map = el._leafletMap;
        if (!map || !window.L) return;
        el.dataset.senzaFondo = '1';
        // Si raccoglie PRIMA e si rimuove poi: `eachLayer` itera la collezione che `removeLayer` modifica.
        var fogli = [];
        map.eachLayer(function (l) { if (l instanceof L.TileLayer) fogli.push(l); });
        fogli.forEach(function (l) { map.removeLayer(l); });
        el._fondiRimossi = fogli;
        el.classList.add('senza-fondo');
        senzaFondo.push(el);
        if (campione && !urlSonda) urlSonda = campione;
        avviaSonda();
    }

    function riprendiIlFondo() {
        senzaFondo.splice(0).forEach(function (el) {
            var map = el._leafletMap, fogli = el._fondiRimossi || [];
            el.dataset.senzaFondo = '';
            el.dataset.vipiGiriPersi = 0;
            el.classList.remove('senza-fondo');
            if (map) fogli.forEach(function (l) { l.addTo(map); });
            el._fondiRimossi = null;
        });
        fermaSonda();
        avviaSpazzino();   // se ricadono, si ricomincia da capo: ritenti, spazzino, e in ultimo la rinuncia
    }

    /// UNA richiesta ogni 30s, non un nuovo giro di mappe: se ci hanno bloccati, ripetere la raffica intera
    /// per scoprirlo sarebbe il modo peggiore di chiederlo.
    function unaSonda() {
        if (!urlSonda) { fermaSonda(); return; }
        if (++sondeFatte > SONDE_MAX) { fermaSonda(); return; }
        var img = new Image();
        img.onload = function () { riprendiIlFondo(); };
        // `cache-bust`: senza, il browser risponderebbe con lo stesso fallimento memorizzato.
        img.src = urlSonda + (urlSonda.indexOf('?') < 0 ? '?' : '&') + 'vipisonda=' + Date.now();
    }
    function avviaSonda() { if (!sondaTimer) { sondeFatte = 0; sondaTimer = setInterval(unaSonda, 30000); } }
    function fermaSonda() { if (sondaTimer) { clearInterval(sondaTimer); sondaTimer = null; } }

    /// Il contenitore di mappa a cui una tessera appartiene.
    function mappaDi(img) {
        return img.closest ? img.closest('.aor-leaflet, .mva-leaflet') : null;
    }

    function unGiro() {
        var tutte = [].slice.call(document.querySelectorAll('img.leaflet-tile'));

        // Chi è rotto AVENDO già finito la scala veloce: sono questi a far maturare la rinuncia.
        var perse = {};
        tutte.forEach(function (i) {
            if (!i.parentNode || !i.complete || i.naturalWidth > 0) return;
            // Almeno un giro di spazzino già fallito su questa tessera: la scala veloce ha finito davvero.
            if (Number(i.getAttribute('data-vipi-spazzata') || 0) < 1) return;
            var el = mappaDi(i);
            if (!el || el.dataset.senzaFondo === '1') return;
            var k = el.dataset.vipiMappaId || (el.dataset.vipiMappaId = 'm' + (++idMappa));
            var v = (perse[k] = perse[k] || { el: el, n: 0, url: i.src });
            v.n++;
        });
        // Un giro senza perdite azzera il conto: il guasto era passeggero e si è ripreso.
        [].slice.call(document.querySelectorAll('.aor-leaflet, .mva-leaflet')).forEach(function (el) {
            var k = el.dataset.vipiMappaId;
            if (!k || !perse[k]) el.dataset.vipiGiriPersi = 0;
        });
        Object.keys(perse).forEach(function (k) {
            var el = perse[k].el;
            var g = Number(el.dataset.vipiGiriPersi || 0) + 1;
            el.dataset.vipiGiriPersi = g;
            if (g >= RINUNCIA_DOPO) rinunciaAlFondo(el, perse[k].url);
        });

        var rotte = tutte.filter(function (i) {
            if (!i.parentNode || !i.complete) return false;
            if (i.naturalWidth > 0) return false;
            return Number(i.getAttribute('data-vipi-spazzata') || 0) < SPAZZA_PER_TESSERA;
        });
        spazzate++;
        if (!rotte.length) {
            // Due giri puliti di fila: il guasto è finito. Si riparte da soli se la pagina torna in primo
            // piano o se la rete rientra (vedi sotto), quindi fermarsi qui non è una resa.
            if (++pulitiDiFila >= 2) { fermaSpazzino(); return; }
        } else {
            pulitiDiFila = 0;
            rotte.forEach(function (img, k) {
                img.setAttribute('data-vipi-spazzata', Number(img.getAttribute('data-vipi-spazzata') || 0) + 1);
                var url = img.src;
                // Sfalsate: rimetterle in coda tutte insieme rifarebbe la raffica che gli scaglioni evitano.
                setTimeout(function () { richiedi(img, url); }, k * 60 + Math.round(Math.random() * 200));
            });
        }
        if (spazzate >= SPAZZA_MAX) fermaSpazzino();
    }

    function avviaSpazzino() {
        if (spazzinoTimer) return;
        spazzate = 0; pulitiDiFila = 0;
        spazzinoTimer = setInterval(unGiro, SPAZZA_OGNI);
    }
    function fermaSpazzino() {
        if (spazzinoTimer) { clearInterval(spazzinoTimer); spazzinoTimer = null; }
    }
    // Due momenti in cui vale la pena riprovare comunque: la rete che rientra, e la scheda che torna davanti
    // (un guasto durato tutto il tempo in cui la pagina era in secondo piano si vede solo adesso).
    window.addEventListener('online', avviaSpazzino);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) avviaSpazzino();
    });
    // Serve anche a vipi-mva.js, che ha i suoi fondi (Esri, OpenTopoMap) e lo stesso problema.
    window.vipiRitentaTessere = ritentaTessere;

    // Basemap CartoDB Positron condivisa.
    function addBasemap(map) {
        ritentaTessere(L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19, subdomains: 'abcd', attribution: '© OpenStreetMap, © CARTO'
        })).addTo(map);
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
                // ⚠️ anche qui il colore puo' essere il NOME di un token (lo manda ConfinantiAdminPage):
                // va risolto, perche' Leaflet lo scrive in un attributo SVG che non sostituisce var().
                var sc = aorColor(s.color, '--ivao-lightblue');
                return L.polygon(r, { color: sc, weight: 2, fillColor: sc, fillOpacity: 0.16 });
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
        // Bersaglio delle chip: la mappa 2D (Leaflet) o lo stage 3D — espongono la stessa interfaccia
        // (`_aorSetSec` + `_secMap`), quindi la logica chip/Tutti/Nessuno/configurazione qui sotto vale per entrambi.
        var lf = block.querySelector('.aor-leaflet, .aor3d-stage');

        // Chiave di scope della sezione: la stessa per la vista 2D e per quella 3D, che si distinguono col
        // suffisso «-3d» nel data-aor. Serve a raggiungere quel che sta FUORI dal .aor-block — le descrizioni
        // delle aree regolamentate — come già fa syncCfgDetails coi <details> di configurazione.
        var scope = (block.dataset.aor || '').replace(/-3d$/, '');

        // Aree regolamentate: la chip accende l'area sulla mappa E la sua descrizione qui sotto. Sull'AoR non
        // c'è nessuna card con quel nome e questa funzione non trova niente: costa una query e basta.
        function setCard(sec, on) {
            var box = document.querySelector('[data-areacards="' + scope + '"]');
            if (!box) return;
            var c = box.querySelector('[data-areacard="' + (sec || '').replace(/"/g, '') + '"]');
            if (c) c.hidden = !on;
        }

        // Porta in vista la PRIMA chip accesa dentro la sua barra. Serve quando la barra scorre — 105 chip su
        // LIRR — e un preset ne accende quattro in fondo: la barra resta in cima, mostrando quattro righe di
        // pastiglie tutte spente, e sembra che il tasto abbia spento tutto. Si muove SOLO la barra
        // (`scrollTop`), non la pagina: `scrollIntoView` trascinerebbe con sé ogni antenato che scorre.
        function mostraPrimaAccesa() {
            var bar = block.querySelector('.aor-toggles');
            if (!bar || bar.scrollHeight <= bar.clientHeight + 1) return;
            var chip = bar.querySelector('.aor-chip.on');
            if (chip) bar.scrollTop = Math.max(0, chip.offsetTop - bar.offsetTop - 4);
        }

        // Quante ne restano accese: senza, un elenco che si accorcia da 105 a 3 sembra rotto.
        function syncCount() {
            var box = document.querySelector('[data-areacards="' + scope + '"]');
            if (!box) return;
            var tutte = box.querySelectorAll('[data-areacard]');
            var accese = box.querySelectorAll('[data-areacard]:not([hidden])');
            var riga = box.querySelector('[data-areacount]');
            if (riga) riga.textContent = riga.dataset.fmt
                ? riga.dataset.fmt.replace('{0}', accese.length).replace('{1}', tutte.length) : '';
            var vuoto = box.querySelector('[data-areaempty]');
            if (vuoto) vuoto.hidden = accese.length > 0;
        }

        function setSec(sec, on) {
            setCard(sec, on);
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
            segna(t, nn);
            setSec(t.dataset.sec, nn);
            refit();
        } else if (t.classList.contains('aor-all')) {
            var allOn = t.dataset.act === 'all';
            block.querySelectorAll('.aor-chip').forEach(function (ch) {
                segna(ch, allOn);
                setSec(ch.dataset.sec, allOn);
            });
            refit();
        } else if (t.classList.contains('cfg-btn')) {
            // Verità selezione = proprietà JS sul block (la classe si desincronizza). Riclick sulla stessa = deseleziona → mostra tutti.
            var wasSel = block.__selCfgNode === t;
            block.querySelectorAll('.cfg-btn').forEach(function (x) { segna(x, false); });
            if (wasSel) {
                block.__selCfgNode = null;
                block.querySelectorAll('.aor-chip').forEach(function (ch) { segna(ch, true); setSec(ch.dataset.sec, true); });
                syncCfgDetails(null);   // deseleziona → collassa tutte
            } else {
                block.__selCfgNode = t;
                segna(t, true);
                var set = (t.dataset.secs || '').split(',').map(function (s) { return s.toUpperCase(); }).filter(Boolean);
                block.querySelectorAll('.aor-chip').forEach(function (ch) {
                    var on = set.indexOf((ch.dataset.sec || '').toUpperCase()) >= 0;
                    segna(ch, on);
                    setSec(ch.dataset.sec, on);
                });
                syncCfgDetails(t.dataset.cfgkey || '');   // apre solo questa, collassa le altre
                mostraPrimaAccesa();
            }
            refit();
            // Tiene l'AoR al centro schermo: aprire i details config sposta il layout.
            if (lf) setTimeout(function () { lf.scrollIntoView({ behavior: vipiScorrimento(), block: 'center' }); }, 90);
        } else if (t.classList.contains('cfg-clear')) {
            block.__selCfgNode = null;
            block.querySelectorAll('.cfg-btn').forEach(function (x) { segna(x, false); });
            block.querySelectorAll('.aor-chip').forEach(function (ch) { segna(ch, true); setSec(ch.dataset.sec, true); });
            syncCfgDetails(null);
            refit();
        }
        syncCount();
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
        // ⚠️ Passa da addBasemap, che è la funzione con quel nome due schermate più su: qui c'era una
        // COPIA della stessa tileLayer scritta a mano, e per questo le mappine restavano fuori dal
        // ritentatore delle tessere — cioè proprio le decine di mappe che fanno la raffica.
        addBasemap(map);

        var color = aorColor(el.dataset.color, '--ivao-lightblue');
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
                    rings.push(L.polygon(ring, { color: aorColor('--ivao-color-product-artifice-dark', '#e26e17'), weight: 2, dashArray: '5,4',
                                          fillColor: aorColor('--ivao-color-product-artifice-light', '#ea984e'), fillOpacity: 0.12 }));
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

    /// Accende le mappe **a scaglioni**, non tutte insieme.
    ///
    /// Una vIPI ACC ne porta decine (misurate su LIBB: 77, quasi tutte mappine di aree regolamentate) e
    /// accendendole in fila chiedeva 115 tessere in 19 ms a due host: il browser ne serve 6 per host, il
    /// fornitore limita la frequenza, e quel che cade resta grigio. Prima quelle vicine allo schermo — sono
    /// le uniche che qualcuno stia guardando — poi le altre a piccoli gruppi.
    ///
    /// ⚠️ Le altre si accendono comunque, non «quando si scorre»: la **stampa** prende tutta la pagina, e
    /// una mappa mai inizializzata stamperebbe il ripiego SVG. Qui cambia il ritmo, non l'esito.
    var LOTTO = 4, PAUSA = 300, scaglioniInCorso = false;

    function accendiMappe() {
        var da = [].slice.call(document.querySelectorAll('.aor-leaflet'))
            .filter(function (el) { return el.dataset.init !== '1'; });
        if (!da.length) return;
        var h = window.innerHeight || 900;
        function vicina(el) {
            var r = el.getBoundingClientRect();
            return r.top < h * 2 && r.bottom > -h;        // in vista, o a meno di una schermata da essa
        }
        da.filter(vicina).forEach(initOne);
        var dopo = da.filter(function (el) { return !vicina(el) && el.dataset.init !== '1'; });
        if (!dopo.length || scaglioniInCorso) return;
        scaglioniInCorso = true;
        (function scaglione() {
            dopo.splice(0, LOTTO).forEach(initOne);
            if (dopo.length) setTimeout(scaglione, PAUSA);
            else scaglioniInCorso = false;
        })();
    }

    function initAll() {
        // Le chip AoR usano event delegation (onAorClick), nessun wiring per-elemento.
        if (window.L) { accendiMappe(); if (window.vipiInitMva) window.vipiInitMva(); return; }
        // ⚠️ Si chiede Leaflet solo se in pagina c'è davvero una mappa: questa funzione gira a ogni render
        // di Blazor e a ogni navigazione, cioè anche sulle pagine che una mappa non ce l'hanno.
        //
        // ⚠️ Si guarda **anche** `.mva-leaflet`: le minime di vettoramento non hanno un caricatore proprio,
        // aspettano `window.L`. Una pagina la cui unica mappa è la carta delle minime (un APP senza shape
        // AoR) restava per sempre col ripiego SVG, e nessun refresh la salvava.
        if (!document.querySelector('.aor-leaflet, .mva-leaflet')) return;
        caricaLeaflet().then(function () {
            accendiMappe();
            if (window.vipiInitMva) window.vipiInitMva();
        }).catch(function (e) {
            // Resta il ripiego SVG: il dato nostro si disegna lo stesso, senza la basemap.
            console.warn('[vipi] mappa AoR senza Leaflet, resta il ripiego SVG', e);
        });
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
