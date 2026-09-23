// Aurora Sector Lab — la mappa (carta F3, slice 4). Leaflet servito da noi, canvas, NESSUNO sfondo a tessere:
// l'app non parla con la rete (§3), e le coste sono quelle del sector (GEO/itgeo.geo), non quelle di un fornitore.
//
// Le coordinate NON passano dal circuito Blazor: ogni strato si chiede con una fetch a /mappa/strato/<id> (§3, il
// tetto dei 32 KB di SignalR). Il formato è corto: { id, f: [ { p: file, r: record, t: 'p'|'l'|'a', e: etichetta,
// c: [ [lat,lon,lat,lon,…], … ], x: [nomi non risolti] } ] } — i punti di un tratto sono numeri in fila, a coppie.
(function () {
    'use strict';

    // Un colore per strato: gli stessi gruppi delle caselle. Tenui, perché sopra ci va l'evidenza.
    var COLORI = {
        sfondo: '#8a9099',
        geo: '#9a7b4f',
        settori: '#0b5cad',
        aree: '#b03030',
        mva: '#7a4fa3',
        aerovie: '#3f7f7f',
        procedure: '#1f7a3f',
        vfr: '#2f8f8f',
        attese: '#b06a10',
        terra: '#6b6b6b',
        piste: '#303030',
        radioassistenze: '#0b5cad',
        punti: '#4a5060'
    };

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

            var colore = COLORI[id] || '#4a5060';
            var partenza = performance.now();
            return fetch('/mappa/strato/' + encodeURIComponent(id), { credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : { f: [] }; })
                .then(function (dati) {
                    var gruppo = L.layerGroup();
                    for (var i = 0; i < dati.f.length; i++) {
                        var forma = dati.f[i];
                        var disegnata = disegna(forma, colore);
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
            forma.setStyle({ color: '#e06000', weight: 3, fillOpacity: forma.sectorlab.t === 'a' ? .15 : forma.options.fillOpacity });
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

        chiudi: function () {
            if (!stato) return;
            if (stato.osservatore) stato.osservatore.disconnect();
            stato.mappa.remove();
            stato = null;
        }
    };
})();
