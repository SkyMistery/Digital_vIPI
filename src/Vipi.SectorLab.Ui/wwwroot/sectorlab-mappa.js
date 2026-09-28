// Aurora Sector Lab — la mappa (carta F3, slice 4). Leaflet servito da noi, canvas, NESSUNO sfondo a tessere:
// l'app non parla con la rete (§3), e le coste sono quelle del sector (GEO/itgeo.geo), non quelle di un fornitore.
//
// Le coordinate NON passano dal circuito Blazor: ogni strato si chiede con una fetch a /mappa/strato/<id> (§3, il
// tetto dei 32 KB di SignalR). Il formato è corto: { id, f: [ { p: file, r: record, t: 'p'|'l'|'a', e: etichetta,
// c: [ [lat,lon,lat,lon,…], … ], x: [nomi non risolti], k/ka, g/ga, s: i colori di Aurora } ] } — i punti di un tratto
// sono numeri in fila, a coppie.
//
// Due modi di colore (lotto «Subito» slice 4): quelli del Lab, uno per strato dal foglio, tenui; e quelli di Aurora,
// che il server calcola forma per forma dallo schema scelto e da colors.def (k = linea, g = riempimento, s = stile
// della linea). Si passa dall'uno all'altro senza riprendere le coordinate. Coi colori di Aurora i punti hanno il
// loro simbolo del .sym (slice 4d, y = indice nell'elenco dei simboli che arriva con i colori).
(function () {
    'use strict';

    // Un colore per strato: gli stessi gruppi delle caselle, presi dal FOGLIO (--lab-strato-<id>), così cambiano col
    // tema chiaro/scuro e la verità è una sola. Tenui, perché sopra ci va l'evidenza.
    function colore(id) {
        var valore = getComputedStyle(document.documentElement).getPropertyValue('--lab-strato-' + id).trim();
        return valore || '#4a5060';
    }

    // Il suggerimento al passaggio del mouse: il nome, e sotto i vincoli dei punti di una procedura (slice 9d, Q2: nel Lab
    // sì, in Aurora mai). Come testo, non come HTML: i nomi vengono dai file.
    function suggerimento(forma) {
        var testo = document.createElement('div');
        testo.style.whiteSpace = 'pre-line';
        testo.textContent = forma.v ? forma.e + '\n' + forma.v : forma.e;
        return testo;
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

    // Gli stili delle linee dello schema di Aurora (…_SOLID): 1 tratteggio, 2 puntini, 3 tratto-punto, 4 tratto-punto-punto
    // (i PenStyle di Delphi: da provare accanto ad Aurora).
    var tratteggi = { 1: '6,4', 2: '2,3', 3: '8,3,2,3', 4: '8,3,2,3,2,3' };

    /// Un simbolo 13×13 nel suo colore, disegnato una volta su una tela piccola e poi copiato (drawImage) per ogni punto:
    /// 4 000 fix a pixel singoli sarebbero 100 000 rettangoli a ogni spostamento della mappa.
    function immagine(indice, colore) {
        var chiave = indice + '|' + colore;
        var fatta = stato.immagini[chiave];
        if (fatta) return fatta;
        var colonne = stato.simboli[indice];
        if (!colonne) return null;
        var tela = document.createElement('canvas');
        tela.width = 13; tela.height = 13;
        var ctx = tela.getContext('2d');
        ctx.fillStyle = colore;
        // Ogni gruppo del .sym è una COLONNA, ogni cifra un pixel dall'alto (carta «file per file» §20).
        for (var x = 0; x < 13; x++) {
            for (var y = 0; y < 13; y++) if (colonne[x].charAt(y) === '1') ctx.fillRect(x, y, 1, 1);
        }
        stato.immagini[chiave] = tela;
        return tela;
    }

    /// Un punto col simbolo del .sym: un cerchio di Leaflet (clic, evidenza, strati uguali a prima) che sulla tela si
    /// disegna col simbolo quando ne ha uno, pixel per pixel e centrato come in Aurora.
    var PuntoConSimbolo = L.CircleMarker.extend({
        _updatePath: function () {
            var tela = this._renderer, simbolo = this.options.sectorlabSimbolo;
            var img = simbolo === null || simbolo === undefined || !tela._ctx ? null : immagine(simbolo, this.options.color);
            if (!img) { L.CircleMarker.prototype._updatePath.call(this); return; }
            if (!tela._drawing || this._empty()) return;
            var p = this._point, ctx = tela._ctx;
            ctx.save();
            ctx.imageSmoothingEnabled = false;
            ctx.globalAlpha = this.options.opacity;
            ctx.drawImage(img, Math.round(p.x) - 6, Math.round(p.y) - 6);
            ctx.restore();
        }
    });

    /// Lo stile di una forma nel modo di adesso. `tinta` è il colore del Lab per il suo strato.
    function stile(forma, tinta) {
        var aurora = stato && stato.aurora && Object.prototype.hasOwnProperty.call(forma, 'k');
        if (!aurora) {
            return forma.t === 'p'
                ? { color: tinta, weight: 1, opacity: 1, fillColor: tinta, fillOpacity: .8, dashArray: null, radius: 3, sectorlabSimbolo: null }
                : { color: tinta, weight: 1, opacity: .9, fillColor: tinta, fillOpacity: forma.t === 'a' ? .06 : 0, dashArray: null };
        }
        // clNone (k null): Aurora non la disegna, e nemmeno noi; resta raggiungibile dall'elenco.
        var linea = forma.k || tinta;
        var opacita = forma.k ? (forma.ka === undefined ? 1 : forma.ka) : 0;
        if (forma.t === 'p') {
            var simbolo = forma.y !== undefined && stato.simboli[forma.y] ? forma.y : null;
            return { color: linea, weight: 1, opacity: opacita, fillColor: linea, fillOpacity: opacita * .8, dashArray: null,
                     radius: simbolo === null ? 3 : 6, sectorlabSimbolo: simbolo };
        }
        return {
            color: linea, weight: 1, opacity: opacita,
            fillColor: forma.g || linea, fillOpacity: forma.g ? (forma.ga === undefined ? 1 : forma.ga) : 0,
            dashArray: forma.s ? (tratteggi[forma.s] || null) : null
        };
    }

    function disegna(forma, tinta) {
        var suo = stile(forma, tinta);
        if (forma.t === 'p') {
            var uno = forma.c.length && forma.c[0].length ? [forma.c[0][0], forma.c[0][1]] : null;
            // Una forma senza punti (un nome che il catalogo non risolve, slice 3b) non si disegna: non è un errore
            // da nascondere, ma sulla mappa non c'è niente da mettere.
            return uno ? new PuntoConSimbolo(uno, suo) : null;
        }

        var tratti = [];
        for (var i = 0; i < forma.c.length; i++) {
            if (forma.c[i].length >= 4) tratti.push(coppie(forma.c[i]));
        }
        if (!tratti.length) return null;
        return forma.t === 'a' ? L.polygon(tratti, suo) : L.polyline(tratti, suo);
    }

    /// Rimette a una forma lo stile del modo di adesso (dopo l'evidenza, dopo un cambio di tema o di modo).
    function applica(l) {
        if (!l.setStyle || !l.sectorlab) return;
        l.setStyle(stile(l.sectorlab, colore(l.sectorlabStrato)));
    }

    /// Il fondo della mappa: quello dello schermo radar coi colori di Aurora, quello del foglio coi colori del Lab.
    function fondo() {
        if (!stato) return;
        stato.mappa.getContainer().style.background = stato.aurora && stato.sfondo ? stato.sfondo : '';
    }

    /// 🔴 I riempimenti di terra (.pol) stanno SOTTO le linee dei .geo, come in Aurora: coi colori di Aurora sono pieni, e
    /// sopra coprirebbero bordi e assi delle taxiway. Una tela sola (canvas) disegna nell'ordine della lista: la terra si
    /// porta in fondo, e le coste ancora sotto.
    /// 🔴 Dall'ULTIMA forma alla prima: portate in fondo nell'ordine del file, la prima finirebbe sopra l'ultima e l'ordine
    /// dei .pol si rovescerebbe — l'erba del confine copriva taxiway e piste (visto a schermo su LIRF). In Aurora vince
    /// l'ultima del file (carta «file per file», I3).
    function ordina(id) {
        if (id !== 'terra' && id !== 'sfondo') return;
        var sotto = ['terra', 'sfondo'];
        for (var i = 0; i < sotto.length; i++) {
            var gruppo = stato.strati[sotto[i]];
            if (!gruppo || !stato.mappa.hasLayer(gruppo)) continue;
            var forme = gruppo.getLayers();
            for (var j = forme.length - 1; j >= 0; j--) if (forme[j].bringToBack) forme[j].bringToBack();
        }
    }

    /// Le voci e le parti spente (lotto «Subito» slice 6): 'file#3' un record, 'file#3.1' il suo secondo poligono. Una
    /// forma spenta esce dal suo gruppo (la vista e i colori non la vedono più); una con qualche parte spenta si ridisegna
    /// coi tratti accesi. Si rifà per ogni strato quando cambiano le spente, e quando uno strato arriva.
    function applicaSpenti(id) {
        var gruppo = stato.strati[id], tutte = stato.tutte && stato.tutte[id];
        if (!gruppo || !tutte) return;
        var spenti = stato.spenti || {};
        for (var i = 0; i < tutte.length; i++) {
            var l = tutte[i], f = l.sectorlab, base = f.p + '#' + f.r;
            var via = !!spenti[base], accesi = null;
            if (!via && f.t !== 'p') {
                accesi = [];
                for (var t = 0; t < f.c.length; t++) {
                    var parte = f.q ? f.q[t] : t;
                    if (!spenti[base + '.' + parte] && f.c[t].length >= 4) accesi.push(coppie(f.c[t]));
                }
                if (!accesi.length) via = true;
            }
            if (via) { if (gruppo.hasLayer(l)) gruppo.removeLayer(l); continue; }
            if (!gruppo.hasLayer(l)) { gruppo.addLayer(l); applica(l); }
            if (accesi && l.setLatLngs && (l.sectorlabParziale || accesi.length !== l.sectorlabTratti)) {
                l.setLatLngs(accesi);
                l.sectorlabParziale = accesi.length !== l.sectorlabTratti;
            }
        }
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

            stato = { mappa: mappa, riferimento: riferimento, strati: {}, evidenza: null, forme: {}, aurora: false, sfondo: null,
                      simboli: [], immagini: {} };
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
            // 🔴 Quel che si vuole ADESSO: una fetch lenta che arriva dopo uno «spegni» non deve accendere niente, e due
            // richieste dello stesso strato non devono fare due gruppi — il primo restava sulla mappa, orfano, e la
            // casella non lo spegneva più (committente, 24 settembre: SID, STAR e punti che non sparivano).
            stato.voluti = stato.voluti || {};
            stato.voluti[id] = !!acceso;

            stato.inArrivo = stato.inArrivo || {};
            stato.giro = stato.giro || {};
            if (!acceso) {
                if (vivo) { stato.mappa.removeLayer(vivo); delete stato.strati[id]; }
                // Una fetch ancora in viaggio non vale più: spento, o spento per essere ridisegnato con dati nuovi.
                delete stato.inArrivo[id];
                stato.giro[id] = (stato.giro[id] || 0) + 1;
                return Promise.resolve(0);
            }
            if (vivo) { vivo.addTo(stato.mappa); return Promise.resolve(0); }
            if (stato.inArrivo[id]) return stato.inArrivo[id];
            var giro = stato.giro[id] = (stato.giro[id] || 0) + 1;

            var tinta = colore(id);
            var partenza = performance.now();
            var arrivo = fetch('/mappa/strato/' + encodeURIComponent(id), { credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : { f: [] }; })
                .then(function (dati) {
                    // Arrivata tardi: dopo di lei lo strato è stato spento o richiesto di nuovo. Non si disegna.
                    if (!stato || stato.giro[id] !== giro) return 0;
                    delete stato.inArrivo[id];
                    var gruppo = L.layerGroup();
                    var tutte = [];
                    for (var i = 0; i < dati.f.length; i++) {
                        var forma = dati.f[i];
                        var disegnata = disegna(forma, tinta);
                        if (!disegnata) continue;
                        disegnata.sectorlab = forma;
                        disegnata.sectorlabStrato = id;
                        disegnata.on('click', (function (f) { return function (e) { L.DomEvent.stop(e); scelta(f); }; })(forma));
                        disegnata.bindTooltip(suggerimento(forma), { sticky: true });
                        gruppo.addLayer(disegnata);
                        stato.forme[forma.p + '#' + forma.r] = disegnata;
                        disegnata.sectorlabTratti = forma.c.filter(function (t) { return t.length >= 4; }).length;
                        tutte.push(disegnata);
                    }

                    if (!stato.voluti[id]) return 0;
                    if (stato.strati[id]) stato.mappa.removeLayer(stato.strati[id]);
                    stato.strati[id] = gruppo;
                    stato.tutte = stato.tutte || {};
                    stato.tutte[id] = tutte;
                    applicaSpenti(id);
                    gruppo.addTo(stato.mappa);
                    ordina(id);
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
                }, function (e) { if (stato && stato.giro[id] === giro) delete stato.inArrivo[id]; throw e; });
            stato.inArrivo[id] = arrivo;
            return arrivo;
        },

        /// Evidenzia il record scelto (e toglie l'evidenza da quello di prima). Senza file, toglie e basta.
        evidenzia: function (file, record, inquadra) {
            if (!stato) return false;
            if (stato.evidenza) {
                var vecchia = stato.evidenza;
                applica(vecchia);
                if (vecchia.bringToBack) vecchia.bringToBack();
                stato.evidenza = null;
                // Portata in fondo, una forma qualsiasi finirebbe sotto la terra e le coste: si rimettono in fondo loro.
                ordina('terra');
            }
            if (!file) return false;

            var forma = stato.forme[file + '#' + record];
            if (!forma) return false;
            forma.setStyle({ color: token('--lab-evidenza', '#e26e17'), weight: 3, opacity: 1, dashArray: null,
                             fillOpacity: forma.sectorlab.t === 'a' ? Math.max(.15, forma.options.fillOpacity) : forma.options.fillOpacity });
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
        /// Le voci e le parti spente (slice 6): la mappa le toglie, e la vista (se c'è) si rifà senza di loro.
        spenti: function (chiavi) {
            if (!stato) return;
            stato.spenti = {};
            for (var i = 0; i < (chiavi || []).length; i++) stato.spenti[chiavi[i]] = true;
            var ids = Object.keys(stato.strati);
            for (var j = 0; j < ids.length; j++) applicaSpenti(ids[j]);
            if (stato.vistaVoluta && stato.vistaVoluta.length) this.vista(stato.vistaVoluta);
        },

        vista: function (chiavi) {
            if (!stato) return 0;
            stato.vistaVoluta = chiavi;
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

        /// I colori della mappa: di Aurora (col fondo dello schermo radar) o del Lab. Le forme hanno già tutti e due.
        colori: function (aurora, sfondo, simboli) {
            if (!stato) return;
            stato.aurora = !!aurora;
            stato.sfondo = sfondo || null;
            stato.simboli = simboli || [];
            stato.immagini = {};
            fondo();
            this.ricolora();
        },

        /// Il tema o il modo è cambiato: ogni forma riprende il suo colore (le coordinate non si richiedono).
        ricolora: function () {
            if (!stato) return;
            for (var id in stato.strati) {
                if (!Object.prototype.hasOwnProperty.call(stato.strati, id)) continue;
                stato.strati[id].eachLayer(function (l) { if (l !== stato.evidenza) applica(l); });
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
