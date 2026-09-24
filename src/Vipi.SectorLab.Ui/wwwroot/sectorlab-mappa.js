// Aurora Sector Lab — la mappa (carta F3, slice 4). Leaflet servito da noi, canvas, NESSUNO sfondo a tessere:
// l'app non parla con la rete (§3), e le coste sono quelle del sector (GEO/itgeo.geo), non quelle di un fornitore.
//
// Le coordinate NON passano dal circuito Blazor: ogni strato si chiede con una fetch a /mappa/strato/<id> (§3, il
// tetto dei 32 KB di SignalR). Il formato è corto: { id, f: [ { p: file, r: record, t: 'p'|'l'|'a', e: etichetta,
// c: [ [lat,lon,lat,lon,…], … ], x: [nomi non risolti] } ] } — i punti di un tratto sono numeri in fila, a coppie.
(function () {
    'use strict';

    // Un colore per strato: gli stessi gruppi delle caselle, presi dal FOGLIO (--lab-strato-<id>), così cambiano col
    // tema chiaro/scuro e la verità è una sola. Tenui, perché sopra ci va l'evidenza.
    function colore(id) {
        var valore = getComputedStyle(document.documentElement).getPropertyValue('--lab-strato-' + id).trim();
        return valore || '#4a5060';
    }

    function token(nome, riserva) {
        var valore = getComputedStyle(document.documentElement).getPropertyValue(nome).trim();
        return valore || riserva;
    }

    var stato = null;

    function coppie(numeri) {
        var punti = new Array(numeri.length / 2);
        for (var i = 0, j = 0; i < numeri.length; i += 2, j++) punti[j] = [numeri[i], numeri[i + 1]];
        return punti;
    }

    function disegna(forma, colore) {
        var stile = { color: colore, weight: 1, opacity: .9, fillOpacity: forma.t === 'a' ? .06 : 0 };
        if (forma.t === 'p') {
            var uno = forma.c.length && forma.c[0].length ? [forma.c[0][0], forma.c[0][1]] : null;
            // Una forma senza punti (un nome che il catalogo non risolve, slice 3b) non si disegna: non è un errore
            // da nascondere, ma sulla mappa non c'è niente da mettere.
            return uno ? L.circleMarker(uno, { radius: 3, color: colore, weight: 1, fillOpacity: .8 }) : null;
        }

        var tratti = [];
        for (var i = 0; i < forma.c.length; i++) {
            if (forma.c[i].length >= 4) tratti.push(coppie(forma.c[i]));
        }
        if (!tratti.length) return null;
        return forma.t === 'a' ? L.polygon(tratti, stile) : L.polyline(tratti, stile);
    }

    function scelta(forma) {
        if (!stato || !stato.riferimento) return;
        stato.riferimento.invokeMethodAsync('SceltaDallaMappa', forma.p, forma.r);
    }

    window.sectorlab = window.sectorlab || {};
    window.sectorlab.mappa = {
        /// Crea la mappa nell'elemento dato e tiene il riferimento al componente, per le scelte col clic.
        crea: function (elemento, riferimento) {
            var nodo = document.getElementById(elemento);
            if (!nodo || typeof L === 'undefined') return false;
            if (stato) this.chiudi();

            var mappa = L.map(nodo, {
                preferCanvas: true,          // 21 667 forme in SVG non si muovono; in canvas sì (prova F0).
                attributionControl: false,
                zoomControl: true,
                worldCopyJump: false,
                maxZoom: 20                  // un pixel ≈ 11 cm, come i sei decimali delle coordinate (MappaDelLab)
            }).setView([42.0, 12.5], 6);

            stato = { mappa: mappa, riferimento: riferimento, strati: {}, evidenza: null, forme: {} };
            // Il riquadro della mappa cambia misura quando i pannelli vanno nell'altra finestra (e tornano): Leaflet
            // non se ne accorge da solo, e disegnerebbe solo nella parte che aveva prima.
            if (typeof ResizeObserver !== 'undefined') {
                stato.osservatore = new ResizeObserver(function () { mappa.invalidateSize(); });
                stato.osservatore.observe(nodo);
            }
            return true;
        },

        /// Accende o spegne uno strato. Le coordinate si scaricano una volta sola e restano in memoria.
        strato: function (id, acceso) {
            if (!stato) return Promise.resolve(0);
            var vivo = stato.strati[id];

            if (!acceso) {
                if (vivo) { stato.mappa.removeLayer(vivo); delete stato.strati[id]; }
                return Promise.resolve(0);
            }
            if (vivo) { vivo.addTo(stato.mappa); return Promise.resolve(0); }

            var tinta = colore(id);
            var partenza = performance.now();
            return fetch('/mappa/strato/' + encodeURIComponent(id), { credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : { f: [] }; })
                .then(function (dati) {
                    var gruppo = L.layerGroup();
                    for (var i = 0; i < dati.f.length; i++) {
                        var forma = dati.f[i];
                        var disegnata = disegna(forma, tinta);
                        if (!disegnata) continue;
                        disegnata.sectorlab = forma;
                        disegnata.on('click', (function (f) { return function (e) { L.DomEvent.stop(e); scelta(f); }; })(forma));
                        disegnata.bindTooltip(forma.e, { sticky: true });
                        gruppo.addLayer(disegnata);
                        stato.forme[forma.p + '#' + forma.r] = disegnata;
                    }

                    stato.strati[id] = gruppo;
                    gruppo.addTo(stato.mappa);
                    // Lo sfondo decide l'inquadratura: la prima volta che arriva, la mappa si mette sull'Italia vera.
                    if (id === 'sfondo' && !stato.inquadrato) {
                        var bordi = L.latLngBounds([]);
                        gruppo.eachLayer(function (l) { if (l.getBounds) bordi.extend(l.getBounds()); });
                        if (bordi.isValid()) { stato.mappa.fitBounds(bordi.pad(.02)); stato.inquadrato = true; }
                    }

                    // Il tempo del primo disegno di ogni strato finisce nel diario d'avvio: è la misura della slice 4,
                    // e più avanti la prima domanda da fare a un AOD che dice «la mappa è lenta».
                    var ms = Math.round(performance.now() - partenza);
                    stato.forme_disegnate = (stato.forme_disegnate || 0) + dati.f.length;
                    stato.tempo = (stato.tempo || 0) + ms;
                    window.sectorlab.diario('strato ' + id + ': ' + dati.f.length + ' forme in ' + ms + ' ms');
                    return dati.f.length;
                });
        },

        /// Evidenzia il record scelto (e toglie l'evidenza da quello di prima). Senza file, toglie e basta.
        evidenzia: function (file, record, inquadra) {
            if (!stato) return false;
            if (stato.evidenza) {
                var vecchia = stato.evidenza;
                if (vecchia.setStyle) vecchia.setStyle(vecchia.sectorlabStile);
                if (vecchia.bringToBack) vecchia.bringToBack();
                stato.evidenza = null;
            }
            if (!file) return false;

            var forma = stato.forme[file + '#' + record];
            if (!forma) return false;
            forma.sectorlabStile = forma.sectorlabStile || {
                color: forma.options.color, weight: forma.options.weight, fillOpacity: forma.options.fillOpacity
            };
            forma.setStyle({ color: token('--lab-evidenza', '#e26e17'), weight: 3, fillOpacity: forma.sectorlab.t === 'a' ? .15 : forma.options.fillOpacity });
            if (forma.bringToFront) forma.bringToFront();
            stato.evidenza = forma;

            if (inquadra) {
                if (forma.getBounds && forma.getBounds().isValid()) stato.mappa.fitBounds(forma.getBounds().pad(.3));
                else if (forma.getLatLng) stato.mappa.setView(forma.getLatLng(), Math.max(stato.mappa.getZoom(), 10));
            }

            return true;
        },

        /// Tutti gli strati chiesti sono arrivati: il totale va nel diario (e la prova automatica aspetta questa riga).
        disegnata: function () {
            if (!stato) return;
            window.sectorlab.diario('mappa disegnata: ' + Object.keys(stato.strati).length + ' strati, ' +
                (stato.forme_disegnate || 0) + ' forme in ' + (stato.tempo || 0) + ' ms');
        },

        /// L'anteprima di «incolla da testo»: la forma che uscirebbe, tratteggiata sopra quella di oggi, e i centri
        /// degli archi. Senza punti si toglie. La prima volta che compare, la mappa ci si porta sopra; poi, mentre l'AOD
        /// scrive o cambia la densità, resta ferma — si vede la forma cambiare, non la mappa saltare.
        anteprima: function (punti, centri) {
            if (!stato) return;
            var cera = !!stato.anteprima;
            if (stato.anteprima) { stato.mappa.removeLayer(stato.anteprima); stato.anteprima = null; }
            if (!punti || punti.length < 2) return;

            var tinta = token('--lab-anteprima', '#196b35');
            var gruppo = L.layerGroup();
            var tratto = coppie(punti);
            if (tratto.length > 1) {
                gruppo.addLayer(L.polyline(tratto, { color: tinta, weight: 2, opacity: 1, dashArray: '6,4', interactive: false }));
            }
            var centriInCoppia = coppie(centri || []);
            for (var i = 0; i < centriInCoppia.length; i++) {
                gruppo.addLayer(L.circleMarker(centriInCoppia[i], { radius: 3, color: tinta, weight: 1, fillOpacity: 1, interactive: false }));
            }
            gruppo.addTo(stato.mappa);
            stato.anteprima = gruppo;

            if (!cera) {
                var bordi = L.latLngBounds(tratto);
                if (bordi.isValid() && !stato.mappa.getBounds().contains(bordi)) stato.mappa.fitBounds(bordi.pad(.3));
            }
        },

        /// La vista: solo alcuni elementi sulla mappa ('file#3' un record, 'file#*' un file intero) più le coste; vuota,
        /// si torna agli strati accesi. Le forme restano quelle degli strati (nessuna fetch in più): si tolgono i gruppi
        /// dalla mappa e se ne fa uno con le sole forme scelte, che restano cliccabili.
        vista: function (chiavi) {
            if (!stato) return 0;
            if (stato.gruppoDellaVista) { stato.mappa.removeLayer(stato.gruppoDellaVista); stato.gruppoDellaVista = null; }
            var ids = Object.keys(stato.strati);

            if (!chiavi || !chiavi.length) {
                for (var i = 0; i < ids.length; i++) {
                    if (!stato.mappa.hasLayer(stato.strati[ids[i]])) stato.strati[ids[i]].addTo(stato.mappa);
                }
                return 0;
            }

            var volute = {};
            for (var c = 0; c < chiavi.length; c++) volute[chiavi[c]] = true;
            var gruppo = L.layerGroup();
            for (var j = 0; j < ids.length; j++) {
                var strato = stato.strati[ids[j]];
                if (ids[j] === 'sfondo') { if (!stato.mappa.hasLayer(strato)) strato.addTo(stato.mappa); continue; }
                stato.mappa.removeLayer(strato);
                strato.eachLayer(function (l) {
                    var f = l.sectorlab;
                    if (f && (volute[f.p + '#' + f.r] || volute[f.p + '#*'])) gruppo.addLayer(l);
                });
            }
            gruppo.addTo(stato.mappa);
            stato.gruppoDellaVista = gruppo;
            return gruppo.getLayers().length;
        },

        /// Il tema è cambiato: ogni strato riprende il suo colore dal foglio (le coordinate non si richiedono).
        ricolora: function () {
            if (!stato) return;
            for (var id in stato.strati) {
                if (!Object.prototype.hasOwnProperty.call(stato.strati, id)) continue;
                var tinta = colore(id);
                stato.strati[id].eachLayer(function (l) {
                    if (l.sectorlabStile) l.sectorlabStile.color = tinta;
                    if (l !== stato.evidenza && l.setStyle) l.setStyle({ color: tinta });
                });
            }
            if (stato.evidenza && stato.evidenza.setStyle) stato.evidenza.setStyle({ color: token('--lab-evidenza', '#e26e17') });
            if (stato.anteprima) {
                var anteprima = token('--lab-anteprima', '#196b35');
                stato.anteprima.eachLayer(function (l) { if (l.setStyle) l.setStyle({ color: anteprima }); });
            }
        },

        chiudi: function () {
            if (!stato) return;
            if (stato.osservatore) stato.osservatore.disconnect();
            stato.mappa.remove();
            stato = null;
        }
    };

    // Il tema cambia col tasto (l'attributo su <html>, anche dall'altra finestra) o con Windows (automatico).
    new MutationObserver(function () { window.sectorlab.mappa.ricolora(); })
        .observe(document.documentElement, { attributes: true, attributeFilter: ['data-tema'] });
    if (window.matchMedia) {
        var buio = window.matchMedia('(prefers-color-scheme: dark)');
        var segui = function () { window.sectorlab.mappa.ricolora(); };
        if (buio.addEventListener) buio.addEventListener('change', segui); else if (buio.addListener) buio.addListener(segui);
    }
})();
