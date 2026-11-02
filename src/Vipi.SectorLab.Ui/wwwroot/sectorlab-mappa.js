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
// loro simbolo di Aurora (slice 17b, y = indice nell'elenco dei simboli che arriva con i colori).
//
// Riordino R4 (D15): un motore, TRE mappe — una per spazio di lavoro (AIP espanso, Sector, Confronti), ognuna col suo
// stato, preso dall'id del suo elemento. Ogni funzione pubblica ha l'elemento come ULTIMO argomento (crea come primo,
// com'era): senza, è la mappa principale «lab-mappa», quella del sector.
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

    /// Le mappe vive, per id dell'elemento.
    var mappe = {};
    // Il riquadro del fuoco per mappa (carta «Lavorare su uno scalo», R2): le coste che arrivano ci portano qui, non sull'Italia.
    var fuochi = {};

    function prendi(elemento) {
        return mappe[elemento || 'lab-mappa'] || null;
    }

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
    function immagine(stato, indice, colore) {
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

    /// Un punto col suo simbolo di Aurora: un cerchio di Leaflet (clic, evidenza, strati uguali a prima) che sulla tela si
    /// disegna col simbolo quando ne ha uno, pixel per pixel e centrato come in Aurora.
    var PuntoConSimbolo = L.CircleMarker.extend({
        _updatePath: function () {
            var tela = this._renderer, simbolo = this.options.sectorlabSimbolo, stato = this.sectorlabMappa;
            var img = simbolo === null || simbolo === undefined || !tela._ctx || !stato ? null : immagine(stato, simbolo, this.options.color);
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
    function stile(stato, forma, tinta) {
        // R6 (D18): la mappa del Confronto mostra i due lati in due colori fissi — l'AIP tratteggiato, il sector pieno —
        // coi simboli dei punti nel colore del lato. Le coste restano com'erano.
        if (stato && stato.dueLati && forma.sid !== 'sfondo') {
            var aip = !!forma.p && forma.p.indexOf('aip:') === 0;
            var lato = aip ? token('--lab-lato-aip', '#e09a1a') : token('--lab-lato-sector', '#3a8fe0');
            if (forma.t === 'p') {
                var suo = forma.y !== undefined && stato.simboli[forma.y] ? forma.y : null;
                return { color: lato, weight: 1, opacity: 1, fillColor: lato, fillOpacity: .8, dashArray: null,
                         radius: suo === null ? 3 : 6, sectorlabSimbolo: suo };
            }
            return { color: lato, weight: aip ? 1.5 : 1, opacity: .9, fillColor: lato, fillOpacity: forma.t === 'a' ? .04 : 0,
                     dashArray: aip ? '6,4' : null };
        }
        // Lo strato dell'AIP espanso (F6): il colore del genere della forma, non quello dello strato.
        if (forma.lc) tinta = forma.lc;
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

    function disegna(stato, forma, tinta) {
        var suo = stile(stato, forma, tinta);
        if (forma.t === 'p') {
            var uno = forma.c.length && forma.c[0].length ? [forma.c[0][0], forma.c[0][1]] : null;
            // Una forma senza punti (un nome che il catalogo non risolve, slice 3b) non si disegna: non è un errore
            // da nascondere, ma sulla mappa non c'è niente da mettere.
            if (!uno) return null;
            var punto = new PuntoConSimbolo(uno, suo);
            punto.sectorlabMappa = stato;
            return punto;
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
        if (!l.setStyle || !l.sectorlab || !l.sectorlabMappa) return;
        var suo = stile(l.sectorlabMappa, l.sectorlab, colore(l.sectorlabStrato));
        // Il fuoco col resto in grigio (carta «Lavorare su uno scalo», D1): la forma fuori dal fuoco è grigia e si vede
        // appena. Solo attenuate, le SID di tutta Italia restavano un groviglio verde (visto sul banco).
        if (l.sectorlabInGrigio) {
            var grigio = token('--lab-in-grigio', '#80868f');
            suo.color = grigio;
            suo.fillColor = grigio;
            suo.opacity = Math.min(suo.opacity || 0, .3);
            suo.fillOpacity = Math.min(suo.fillOpacity || 0, .1);
            suo.sectorlabSimbolo = null;
        }
        l.setStyle(suo);
    }

    /// D1: mette in grigio (o toglie dal grigio) una forma fuori dal fuoco. In grigio non prende il clic: sulla tela
    /// (canvas) il clic e il suggerimento guardano options.interactive.
    function inGrigio(l, si) {
        if (!!l.sectorlabInGrigio === !!si) return;
        l.sectorlabInGrigio = !!si;
        l.options.interactive = !si;
        applica(l);
    }

    /// Il fondo della mappa: quello dello schermo radar coi colori di Aurora, quello del foglio coi colori del Lab.
    function fondo(stato) {
        if (!stato) return;
        stato.mappa.getContainer().style.background = stato.aurora && stato.sfondo ? stato.sfondo : '';
    }

    /// 🔴 I riempimenti di terra (.pol) stanno SOTTO le linee dei .geo, come in Aurora: coi colori di Aurora sono pieni, e
    /// sopra coprirebbero bordi e assi delle taxiway. Una tela sola (canvas) disegna nell'ordine della lista: la terra si
    /// porta in fondo, e le coste ancora sotto.
    /// 🔴 Dall'ULTIMA forma alla prima: portate in fondo nell'ordine del file, la prima finirebbe sopra l'ultima e l'ordine
    /// dei .pol si rovescerebbe — l'erba del confine copriva taxiway e piste (visto a schermo su LIRF). In Aurora vince
    /// l'ultima del file (carta «file per file», I3).
    function ordina(stato, id) {
        if (id !== 'terra' && id !== 'sfondo') return;
        var sotto = ['terra', 'sfondo'];
        for (var i = 0; i < sotto.length; i++) {
            var gruppo = stato.strati[sotto[i]];
            if (!gruppo || !stato.mappa.hasLayer(gruppo)) continue;
            var forme = gruppo.getLayers();
            for (var j = forme.length - 1; j >= 0; j--) if (forme[j].bringToBack) forme[j].bringToBack();
        }
    }

    /// Le voci e le parti spente (lotto «Subito» slice 6): 'file#3' un record, 'file#3.1' il suo secondo poligono; e i
    /// generi spenti di uno strato (slice 12c): '§geo:TAXI_CENTER', contro il genere 'h' della forma. Una
    /// forma spenta esce dal suo gruppo (la vista e i colori non la vedono più); una con qualche parte spenta si ridisegna
    /// coi tratti accesi. Si rifà per ogni strato quando cambiano le spente, e quando uno strato arriva.
    /// 26 ottobre: '+aip:2611#3' è una voce accesa da sola, che si vede anche col suo genere spento («Vai alla voce» lascia
    /// accesa solo quella).
    function applicaSpenti(stato, id) {
        var gruppo = stato.strati[id], tutte = stato.tutte && stato.tutte[id];
        if (!gruppo || !tutte) return;
        var spenti = stato.spenti || {};
        for (var i = 0; i < tutte.length; i++) {
            var l = tutte[i], f = l.sectorlab, base = f.p + '#' + f.r;
            // Slice 12c (H1): un genere spento del suo strato ('§geo:TAXI_CENTER') toglie tutte le sue forme.
            var via = !!spenti[base] || (!!f.h && !!spenti['§' + id + ':' + f.h] && !spenti['+' + base]), accesi = null;
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

    /// L'inquadratura sulle coste, quando il riquadro ha una misura. 🔴 R4: una mappa che nasce nascosta (o in una
    /// finestra stretta) ha il riquadro a zero, e un fitBounds lì la manda allo zoom più lontano: un puntino al centro
    /// (visto sul banco). Allora aspetta, e la fa il ResizeObserver quando la mappa compare.
    function inquadraSeAspetta(stato) {
        if (!stato || !stato.daInquadrare) return;
        var misura = stato.mappa.getSize();
        if (misura.x <= 0 || misura.y <= 0) return;
        stato.mappa.fitBounds(stato.daInquadrare);
        stato.daInquadrare = null;
        stato.inquadrato = true;
    }

    /// Una forma dello strato `id` pronta per il suo gruppo: disegnata, col clic che la sceglie, il suggerimento, e nota
    /// in stato.forme (per l'evidenza). Null se non ha niente da disegnare.
    function nuovaForma(stato, id, forma, tinta) {
        forma.sid = id;
        var disegnata = disegna(stato, forma, tinta);
        if (!disegnata) return null;
        disegnata.sectorlab = forma;
        disegnata.sectorlabStrato = id;
        disegnata.sectorlabMappa = stato;
        disegnata.on('click', function (e) {
            L.DomEvent.stop(e);
            // §9: mentre si disegna, il clic su una forma fa un punto (la calamita sceglie dove), non la sceglie.
            if (stato.disegno) {
                puntoDelDisegno(stato, e.latlng);
                return;
            }
            // S3: mentre la posa aspetta un punto, il clic dà il posto, non sceglie la voce.
            if (stato.prende) {
                if (forma.t === 'p' && forma.c.length && forma.c[0].length >= 2) presa(stato, forma.c[0][0], forma.c[0][1], forma.e);
                else presa(stato, e.latlng.lat, e.latlng.lng, null);
                return;
            }
            scelta(stato, forma);
        });
        disegnata.bindTooltip(suggerimento(forma), { sticky: true });
        stato.forme[forma.p + '#' + forma.r] = disegnata;
        disegnata.sectorlabTratti = forma.c.filter(function (t) { return t.length >= 4; }).length;
        return disegnata;
    }

    function scelta(stato, forma) {
        if (!stato || stato.chiusa || !stato.riferimento) return;
        stato.riferimento.invokeMethodAsync('SceltaDallaMappa', forma.p, forma.r);
    }

    /// Carta «Gli aeroporti nel Lab», S3: il posto del punto della posa in attesa. Un clic su un punto (un fix dell'AIP) ne
    /// prende le coordinate e il nome; altrove, dove si è cliccato.
    function presa(stato, lat, lon, nome) {
        if (!stato || stato.chiusa || !stato.riferimento) return;
        stato.riferimento.invokeMethodAsync('PuntoDallaMappa', lat, lon, nome || null);
    }

    /// La carta posata sopra la mappa: l'immagine intera, portata con una matrice CSS sui tre angoli. Sul piano di Mercatore
    /// la posa è affine (PosaSullaMappa), e i punti di livello di Leaflet sono quel piano in scala: la matrice è esatta. Si
    /// rifà a ogni zoom; durante l'animazione dello zoom l'immagine si nasconde (leaflet-zoom-hide), come i livelli di Leaflet.
    function posizionaLaCarta(stato) {
        var c = stato && stato.carta;
        if (!c || !c.img.naturalWidth) return;
        var m = stato.mappa, w = c.img.naturalWidth, h = c.img.naturalHeight;
        var p0 = m.latLngToLayerPoint(c.angoli[0]), p1 = m.latLngToLayerPoint(c.angoli[1]), p2 = m.latLngToLayerPoint(c.angoli[2]);
        c.img.style.width = w + 'px';
        c.img.style.height = h + 'px';
        c.img.style.transform = 'matrix(' + [(p1.x - p0.x) / w, (p1.y - p0.y) / w, (p2.x - p0.x) / h, (p2.y - p0.y) / h, p0.x, p0.y].join(',') + ')';
        if (c.inquadra) {
            c.inquadra = false;
            var p3 = [c.angoli[1][0] + c.angoli[2][0] - c.angoli[0][0], c.angoli[1][1] + c.angoli[2][1] - c.angoli[0][1]];
            m.fitBounds(L.latLngBounds([c.angoli[0], c.angoli[1], c.angoli[2], p3]));
        }
    }

    function togliLaCarta(stato) {
        if (!stato.carta) return;
        stato.mappa.off('zoomend viewreset', stato.carta.metti);
        L.DomUtil.remove(stato.carta.img);
        stato.carta = null;
    }

    /// F9 (carta «L'editor sulla mappa»): le maniglie del record scelto. Marcatori di Leaflet (DOM, sopra la tela): sono i
    /// vertici di UN record, al più qualche centinaio, e Leaflet sa già trascinarli.
    function togliLeManiglie(stato) {
        if (stato.maniglie) { stato.mappa.removeLayer(stato.maniglie.gruppo); stato.maniglie = null; }
        stato.indicata = null;
        if (stato.traccia) { stato.mappa.removeLayer(stato.traccia); stato.traccia = null; }
        if (stato.anello) { stato.mappa.removeLayer(stato.anello); stato.anello = null; }
        stato.mappa.closePopup();
    }

    function icona(classe, lato) {
        return L.divIcon({ className: 'lab-maniglia' + (classe ? ' ' + classe : ''), iconSize: [lato, lato] });
    }

    /// R5: la calamita. Raggio in pixel dello schermo, e lato delle celle della griglia (≥ raggio: bastano le 9 celle intorno).
    var RAGGIO_DELLA_CALAMITA = 8, CELLA = 16;

    // Alt tenuto stacca la calamita. Lo dice ogni movimento del mouse (anche durante il trascinamento di Leaflet).
    var altTenuto = false;
    document.addEventListener('mousemove', function (e) { altTenuto = e.altKey; }, true);
    document.addEventListener('keydown', function (e) { if (e.key === 'Alt') altTenuto = true; }, true);
    document.addEventListener('keyup', function (e) { if (e.key === 'Alt') altTenuto = false; }, true);

    /// I punti a cui agganciarsi, presi una volta all'inizio del trascinamento: le forme sulla mappa ADESSO (strati accesi,
    /// vista, voci spente già tolte), dentro la vista allargata, tranne il record stesso. Un fix o un VOR è un punto; di una
    /// linea o di un'area ogni vertice. `soloPunti`: per un vertice col nome, che va solo su un punto col nome (R6).
    /// In una griglia sul piano dei layer di Leaflet, che non cambia quando la mappa scorre (autoPan) ma solo con lo zoom.
    /// `partenza` (facoltativa): dove la maniglia è partita. Quel che sta proprio lì (le copie gemelle del vertice, il fix
    /// nascosto gemello di un punto VFR) non si aggancia: riporterebbe la maniglia al posto vecchio.
    function raccogli(stato, soloPunti, diChi, partenza) {
        var mappa = stato.mappa, bordi = mappa.getBounds().pad(.5), griglia = {},
            via = partenza ? mappa.latLngToLayerPoint(partenza) : null;
        function metti(lat, lon, f, punto) {
            if (!bordi.contains([lat, lon])) return;
            var lp = mappa.latLngToLayerPoint([lat, lon]), chiave = Math.floor(lp.x / CELLA) + ',' + Math.floor(lp.y / CELLA);
            if (via && Math.abs(lp.x - via.x) < .5 && Math.abs(lp.y - via.y) < .5) return;
            (griglia[chiave] = griglia[chiave] || []).push({ x: lp.x, y: lp.y, lat: lat, lon: lon, p: f.p, r: f.r, punto: punto });
        }
        mappa.eachLayer(function (l) {
            var f = l.sectorlab;
            if (!f || !f.c || (f.p + '#' + f.r) === diChi) return;
            if (f.t === 'p') {
                if (f.c.length && f.c[0].length >= 2) metti(f.c[0][0], f.c[0][1], f, true);
                return;
            }
            if (soloPunti) return;
            for (var t = 0; t < f.c.length; t++) {
                for (var i = 0; i + 1 < f.c[t].length; i += 2) metti(f.c[t][i], f.c[t][i + 1], f, false);
            }
        });
        return griglia;
    }

    /// Il punto della griglia più vicino a `qui` entro il raggio, o null. A pari distanza (±2 px) vince un punto (un fix)
    /// sul vertice di una forma: è lì che di solito si vuole andare.
    function piuVicino(stato, griglia, qui) {
        var lp = stato.mappa.latLngToLayerPoint(qui), cx = Math.floor(lp.x / CELLA), cy = Math.floor(lp.y / CELLA), meglio = null, voto = Infinity;
        for (var dx = -1; dx <= 1; dx++) {
            for (var dy = -1; dy <= 1; dy++) {
                var cella = griglia[(cx + dx) + ',' + (cy + dy)];
                if (!cella) continue;
                for (var i = 0; i < cella.length; i++) {
                    var c = cella[i], d = Math.sqrt((c.x - lp.x) * (c.x - lp.x) + (c.y - lp.y) * (c.y - lp.y));
                    if (d > RAGGIO_DELLA_CALAMITA) continue;
                    var v = d - (c.punto ? 2 : 0);
                    if (v < voto) { voto = v; meglio = c; }
                }
            }
        }
        return meglio;
    }

    /// Una maniglia che si trascina: mentre la tieni, `tratti(qui)` dice i tratti tratteggiati da mostrare (R2: i due che
    /// toccano il vertice; R3: i due che il vertice nuovo farebbe); al rilascio `gesto(dove, aggancio)` chiede il gesto al
    /// Lab, con il punto a cui la calamita l'ha agganciato (R5) o null. `soloPunti`: la calamita prende solo i punti (R6).
    function trascinabile(stato, marcatore, tratti, gesto, soloPunti) {
        var partenza = marcatore.getLatLng(), griglia = null, aggancio = null;
        marcatore.on('dragstart', function () {
            stato.trascina = true;
            griglia = raccogli(stato, soloPunti, stato.maniglie && stato.maniglie.diChi, marcatore.getLatLng());
            aggancio = null;
            // Le maniglie di mezzo dei tratti che si muovono resterebbero a metà del tratto vecchio: si nascondono.
            L.DomUtil.addClass(stato.mappa.getContainer(), 'lab-mappa-trascina');
            if (stato.traccia) stato.mappa.removeLayer(stato.traccia);
            stato.traccia = L.polyline([], { color: token('--lab-evidenza', '#e26e17'), weight: 2, dashArray: '6,4', interactive: false })
                .addTo(stato.mappa);
        });
        marcatore.on('drag', function () {
            var qui = marcatore.getLatLng();
            aggancio = altTenuto ? null : piuVicino(stato, griglia, qui);
            // Agganciato: un anello sul punto, e i tratti tratteggiati vanno lì (è lì che andrà il vertice).
            if (aggancio) {
                qui = L.latLng(aggancio.lat, aggancio.lon);
                if (!stato.anello) {
                    stato.anello = L.circleMarker(qui, { radius: 10, color: token('--lab-evidenza', '#e26e17'), weight: 2, fill: false, interactive: false })
                        .addTo(stato.mappa);
                }
                stato.anello.setLatLng(qui);
            } else if (stato.anello) {
                stato.mappa.removeLayer(stato.anello);
                stato.anello = null;
            }
            if (stato.traccia) stato.traccia.setLatLngs(tratti(qui));
        });
        marcatore.on('dragend', function () {
            stato.trascina = false;
            griglia = null;
            L.DomUtil.removeClass(stato.mappa.getContainer(), 'lab-mappa-trascina');
            if (stato.traccia) { stato.mappa.removeLayer(stato.traccia); stato.traccia = null; }
            if (stato.anello) { stato.mappa.removeLayer(stato.anello); stato.anello = null; }
            var dove = aggancio ? L.latLng(aggancio.lat, aggancio.lon) : marcatore.getLatLng(), preso = aggancio;
            if (stato.chiusa || !stato.riferimento) return;
            // Maniglie nuove arrivate durante il trascinamento (un gesto dall'altra finestra): adesso si mettono, e il
            // gesto parte lo stesso — il Lab lo rifiuta se quel vertice non c'è più.
            if (stato.maniglieInAttesa) {
                var attese = stato.maniglieInAttesa;
                stato.maniglieInAttesa = null;
                window.sectorlab.mappa.maniglie(attese.elenco, stato.id, attese.diChi);
            }
            if (preso) marcatore.setLatLng(dove);
            // Un gesto solo, al rilascio. Fatto: il Lab rimanda le maniglie dove sono adesso. Rifiutato: la maniglia torna
            // dov'era, e il motivo sta nella Scheda accanto all'elenco.
            gesto(dove, preso).then(function (fatto) { if (!fatto) marcatore.setLatLng(partenza); },
                                    function () { marcatore.setLatLng(partenza); });
        });
    }

    /// R4: il clic destro su una maniglia apre «Togli il vertice N» lì accanto. Un menu e non un tasto da solo (né Canc):
    /// con la mappa sotto la mano si toglierebbe troppo facilmente.
    function menuDelVertice(stato, m, marcatore) {
        marcatore.on('contextmenu', function (e) {
            L.DomEvent.stop(e);
            if (e.originalEvent) L.DomEvent.preventDefault(e.originalEvent);
            var tasto = L.DomUtil.create('button', 'lab-tasto lab-togli-dalla-mappa');
            tasto.type = 'button';
            tasto.textContent = 'Togli il vertice ' + (m.posizione + 1);
            L.DomEvent.on(tasto, 'click', function (ev) {
                L.DomEvent.stop(ev);
                stato.mappa.closePopup();
                if (!stato.chiusa && stato.riferimento)
                    stato.riferimento.invokeMethodAsync('VerticeToltoDallaMappa', m.campo, m.posizione).catch(function () { /* il circuito se n'è andato */ });
            });
            L.popup({ closeButton: false, offset: [0, -6], className: 'lab-menu-maniglia' })
                .setLatLng(marcatore.getLatLng()).setContent(tasto).openOn(stato.mappa);
        });
    }

    /// §9 (R10-R12): il disegno a clic. Ogni clic (anche sopra una forma) fa un punto, con la calamita; la forma che esce
    /// si vede tratteggiata, con l'elastico fino al puntatore; Invio o doppio clic la manda al Lab in un gesto solo,
    /// Backspace toglie l'ultimo punto, Esc lascia stare. `soloNomi` (D7): si clicca solo su un punto col nome.
    function puntoDelDisegno(stato, latlng) {
        var d = stato.disegno;
        if (!d || d.inviando) return;
        var preso = altTenuto ? null : piuVicino(stato, d.griglia, latlng);
        if (d.soloNomi && !(preso && preso.punto)) {
            avvisoSullaMappa(stato, latlng, 'Qui i punti sono nomi: clicca un fix, un VOR o un NDB (anche col suo anello).');
            return;
        }
        var p = preso ? { lat: preso.lat, lon: preso.lon, suFile: preso.p, suRecord: preso.r }
                      : { lat: latlng.lat, lon: latlng.lng, suFile: null, suRecord: 0 };
        // Il doppio clic che finisce arriva prima come due clic sullo stesso posto: il secondo non è un punto.
        var ultimo = d.punti[d.punti.length - 1];
        if (ultimo && stato.mappa.latLngToLayerPoint([ultimo.lat, ultimo.lon]).distanceTo(stato.mappa.latLngToLayerPoint([p.lat, p.lon])) < 3) return;
        d.punti.push(p);
        anteprimaDelDisegno(stato, null);
    }

    function anteprimaDelDisegno(stato, cursore) {
        var d = stato.disegno;
        if (!d) return;
        var punti = d.punti.map(function (p) { return [p.lat, p.lon]; });
        d.linea.setLatLngs(d.chiudi && punti.length >= 3 ? punti.concat([punti[0]]) : punti);
        d.segni.clearLayers();
        for (var i = 0; i < punti.length; i++) {
            d.segni.addLayer(L.circleMarker(punti[i], { radius: 4, color: token('--lab-evidenza', '#e26e17'), weight: 2,
                fillColor: token('--lab-superficie', '#fff'), fillOpacity: 1, interactive: false }));
        }
        // L'elastico: dall'ultimo punto al puntatore (e, per una forma chiusa, di nuovo al primo).
        var elastico = [];
        if (cursore && punti.length) {
            elastico.push([punti[punti.length - 1], cursore]);
            if (d.chiudi && punti.length >= 2) elastico.push([cursore, punti[0]]);
        }
        d.elastico.setLatLngs(elastico);
    }

    function avvisoSullaMappa(stato, latlng, testo) {
        var riga = L.DomUtil.create('span', 'lab-avviso-sulla-mappa');
        riga.textContent = testo;
        var avviso = L.popup({ closeButton: false, className: 'lab-menu-maniglia', autoPan: false })
            .setLatLng(latlng).setContent(riga).openOn(stato.mappa);
        setTimeout(function () { stato.mappa.closePopup(avviso); }, 2500);
    }

    function mandaIlDisegno(stato) {
        var d = stato.disegno;
        if (!d || d.inviando || stato.chiusa || !stato.riferimento) return;
        d.inviando = true;
        // Fatto: il Lab chiude il disegno (e la mappa lo toglie). Rifiutato: i punti restano, il motivo è nella Scheda.
        stato.riferimento.invokeMethodAsync('FormaDisegnata', d.punti)
            .then(function () { d.inviando = false; }, function () { d.inviando = false; });
    }

    function chiudiIlDisegno(stato) {
        var d = stato.disegno, mappa = stato.mappa;
        if (!d) return;
        mappa.off('click', d.suClic).off('mousemove', d.suMuovi).off('dblclick', d.suDoppio).off('zoomend moveend', d.rifai);
        document.removeEventListener('keydown', d.suTasto, true);
        mappa.removeLayer(d.linea).removeLayer(d.elastico).removeLayer(d.segni);
        if (stato.anello) { mappa.removeLayer(stato.anello); stato.anello = null; }
        mappa.doubleClickZoom.enable();
        L.DomUtil.removeClass(mappa.getContainer(), 'lab-mappa-prende');
        stato.disegno = null;
    }

    // Esc spegne le maniglie (R1), come il loro tasto. Un ascoltatore solo, per tutte le mappe.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        for (var chiave in mappe) {
            var stato = mappe[chiave];
            if (stato && stato.maniglie && !stato.trascina && stato.riferimento)
                stato.riferimento.invokeMethodAsync('SpegniLaModifica').catch(function () { /* il circuito se n'è andato */ });
        }
    });

    window.sectorlab = window.sectorlab || {};
    window.sectorlab.mappa = {
        /// Crea la mappa nell'elemento dato e tiene il riferimento al componente, per le scelte col clic.
        crea: function (elemento, riferimento) {
            var nodo = document.getElementById(elemento);
            if (!nodo || typeof L === 'undefined') return false;
            if (mappe[elemento]) this.chiudi(elemento);

            var mappa = L.map(nodo, {
                preferCanvas: true,          // 21 667 forme in SVG non si muovono; in canvas sì (prova F0).
                attributionControl: false,
                zoomControl: true,
                worldCopyJump: false,
                maxZoom: 20                  // un pixel ≈ 11 cm, come i sei decimali delle coordinate (MappaDelLab)
            }).setView([42.0, 12.5], 6);

            var stato = mappe[elemento] = { id: elemento, mappa: mappa, riferimento: riferimento, strati: {}, evidenza: null, forme: {}, aurora: false,
                                            sfondo: null, simboli: [], immagini: {} };
            // Il riquadro della mappa cambia misura quando i pannelli vanno nell'altra finestra (e tornano): Leaflet
            // non se ne accorge da solo, e disegnerebbe solo nella parte che aveva prima.
            if (typeof ResizeObserver !== 'undefined') {
                stato.osservatore = new ResizeObserver(function () { mappa.invalidateSize(); inquadraSeAspetta(stato); });
                stato.osservatore.observe(nodo);
            }
            return true;
        },

        /// Accende o spegne uno strato. Le coordinate si scaricano una volta sola e restano in memoria.
        strato: function (id, acceso, elemento) {
            var stato = prendi(elemento);
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
                    if (stato.chiusa || stato.giro[id] !== giro) return 0;
                    delete stato.inArrivo[id];
                    var gruppo = L.layerGroup();
                    var tutte = [];
                    for (var i = 0; i < dati.f.length; i++) {
                        var disegnata = nuovaForma(stato, id, dati.f[i], tinta);
                        if (!disegnata) continue;
                        gruppo.addLayer(disegnata);
                        tutte.push(disegnata);
                    }

                    if (!stato.voluti[id]) return 0;
                    if (stato.strati[id]) stato.mappa.removeLayer(stato.strati[id]);
                    stato.strati[id] = gruppo;
                    stato.tutte = stato.tutte || {};
                    stato.tutte[id] = tutte;
                    applicaSpenti(stato, id);
                    gruppo.addTo(stato.mappa);
                    ordina(stato, id);
                    // Lo sfondo decide l'inquadratura: la prima volta che arriva, la mappa si mette sull'Italia vera — o sullo
                    // scalo del fuoco, se c'è (carta «Lavorare su uno scalo», R2): sul banco, due volte su sei, le coste
                    // arrivavano dopo lo scalo e riportavano la mappa sull'Italia.
                    if (id === 'sfondo' && !stato.inquadrato) {
                        var bordi = L.latLngBounds([]);
                        gruppo.eachLayer(function (l) { if (l.getBounds) bordi.extend(l.getBounds()); });
                        if (fuochi[stato.id]) { stato.daInquadrare = fuochi[stato.id]; inquadraSeAspetta(stato); }
                        else if (bordi.isValid()) { stato.daInquadrare = bordi.pad(.02); inquadraSeAspetta(stato); }
                    }

                    // Il tempo del primo disegno di ogni strato finisce nel diario d'avvio: è la misura della slice 4,
                    // e più avanti la prima domanda da fare a un AOD che dice «la mappa è lenta».
                    var ms = Math.round(performance.now() - partenza);
                    stato.forme_disegnate = (stato.forme_disegnate || 0) + dati.f.length;
                    stato.tempo = (stato.tempo || 0) + ms;
                    window.sectorlab.diario('strato ' + id + ': ' + dati.f.length + ' forme in ' + ms + ' ms');
                    return dati.f.length;
                }, function (e) { if (stato.giro[id] === giro) delete stato.inArrivo[id]; throw e; });
            stato.inArrivo[id] = arrivo;
            return arrivo;
        },

        /// F9 (carta «L'editor sulla mappa», R9): dopo un gesto, le sole forme del file toccato si riprendono e prendono il
        /// posto delle sue di prima nello strato (stesso posto nell'elenco: l'ordine dei .pol conta), invece di riprendere lo
        /// strato intero — 8832 forme in ~200 ms per i .geo del sector vero. -1: non si può (strato non disegnato o in
        /// arrivo, o ripreso intero nel frattempo), e allora il componente riprende lo strato intero come prima.
        rifaiIlFile: function (id, file, elemento) {
            var stato = prendi(elemento);
            if (!stato || !stato.strati[id] || !stato.tutte || !stato.tutte[id] || (stato.inArrivo && stato.inArrivo[id]))
                return Promise.resolve(-1);
            var giro = (stato.giro && stato.giro[id]) || 0, tinta = colore(id), partenza = performance.now();
            return fetch('/mappa/strato/' + encodeURIComponent(id) + '?file=' + encodeURIComponent(file), { credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (dati) {
                    // Arrivata tardi: lo strato è stato spento o ripreso intero nel frattempo.
                    if (!dati || stato.chiusa || ((stato.giro && stato.giro[id]) || 0) !== giro || !stato.strati[id]) return -1;
                    var gruppo = stato.strati[id], tutte = stato.tutte[id], restano = [], dove = -1;
                    for (var i = 0; i < tutte.length; i++) {
                        var l = tutte[i], chiave = l.sectorlab.p + '#' + l.sectorlab.r;
                        if (l.sectorlab.p !== file) { restano.push(l); continue; }
                        if (dove < 0) dove = restano.length;
                        gruppo.removeLayer(l);
                        if (stato.forme[chiave] === l) delete stato.forme[chiave];
                        if (stato.evidenza === l) stato.evidenza = null;
                    }
                    var nuove = [];
                    for (var j = 0; j < dati.f.length; j++) {
                        var disegnata = nuovaForma(stato, id, dati.f[j], tinta);
                        if (disegnata) nuove.push(disegnata);
                    }
                    if (dove < 0) dove = restano.length;
                    stato.tutte[id] = restano.slice(0, dove).concat(nuove, restano.slice(dove));
                    // Le nuove entrano nel gruppo (o restano fuori, se spente) come al primo disegno.
                    applicaSpenti(stato, id);
                    ordina(stato, id);
                    window.sectorlab.diario('file ' + file + ' nello strato ' + id + ': ' + dati.f.length + ' forme in ' +
                        Math.round(performance.now() - partenza) + ' ms');
                    return dati.f.length;
                }, function () { return -1; });
        },

        /// Evidenzia il record scelto (e toglie l'evidenza da quello di prima). Senza file, toglie e basta.
        /// `soloSeFuori` (il fuoco, R4): si lavora sullo scalo e la mappa non salta a ogni scelta — si muove solo se il record
        /// non si vede, e allora ci si porta senza cambiare zoom se ci sta, se no lo inquadra.
        evidenzia: function (file, record, inquadra, elemento, soloSeFuori) {
            var stato = prendi(elemento);
            if (!stato) return false;
            if (stato.evidenza) {
                var vecchia = stato.evidenza;
                applica(vecchia);
                if (vecchia.bringToBack) vecchia.bringToBack();
                stato.evidenza = null;
                // Portata in fondo, una forma qualsiasi finirebbe sotto la terra e le coste: si rimettono in fondo loro.
                ordina(stato, 'terra');
            }
            if (!file) return false;

            var forma = stato.forme[file + '#' + record];
            if (!forma) return false;
            forma.setStyle({ color: token('--lab-evidenza', '#e26e17'), weight: 3, opacity: 1, dashArray: null,
                             fillOpacity: forma.sectorlab.t === 'a' ? Math.max(.15, forma.options.fillOpacity) : forma.options.fillOpacity });
            if (forma.bringToFront) forma.bringToFront();
            stato.evidenza = forma;

            if (inquadra && soloSeFuori) {
                var vista = stato.mappa.getBounds();
                if (forma.getBounds && forma.getBounds().isValid()) {
                    var suoi = forma.getBounds();
                    if (!vista.contains(suoi)) {
                        var ci = vista.getNorthEast().lat - vista.getSouthWest().lat >= suoi.getNorthEast().lat - suoi.getSouthWest().lat
                              && vista.getNorthEast().lng - vista.getSouthWest().lng >= suoi.getNorthEast().lng - suoi.getSouthWest().lng;
                        if (ci) stato.mappa.panTo(suoi.getCenter());
                        else stato.mappa.fitBounds(suoi.pad(.3));
                    }
                }
                else if (forma.getLatLng && !vista.contains(forma.getLatLng())) stato.mappa.panTo(forma.getLatLng());
            }
            else if (inquadra) {
                if (forma.getBounds && forma.getBounds().isValid()) stato.mappa.fitBounds(forma.getBounds().pad(.3));
                else if (forma.getLatLng) stato.mappa.setView(forma.getLatLng(), Math.max(stato.mappa.getZoom(), 10));
            }

            return true;
        },

        /// R6 (D18): i due lati in due colori fissi (la mappa del Confronto). Ogni forma riprende il suo stile.
        dueLati: function (acceso, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            stato.dueLati = !!acceso;
            this.ricolora(elemento);
        },

        /// R6 (D18): la coppia di una riga del Confronto — la voce del sector e quella dell'AIP — in evidenza, ognuna nel
        /// colore del suo lato e più spessa, e (se si chiede) inquadrate insieme. Senza file, si toglie.
        coppia: function (fileSector, recordSector, fileAip, recordAip, inquadra, elemento) {
            var stato = prendi(elemento);
            if (!stato) return 0;
            if (stato.coppia) {
                for (var i = 0; i < stato.coppia.length; i++) if (stato.coppia[i] !== stato.evidenza) applica(stato.coppia[i]);
                stato.coppia = null;
                ordina(stato, 'terra');
            }
            var forme = [];
            if (fileSector && stato.forme[fileSector + '#' + recordSector]) forme.push(stato.forme[fileSector + '#' + recordSector]);
            if (fileAip && stato.forme[fileAip + '#' + recordAip]) forme.push(stato.forme[fileAip + '#' + recordAip]);
            if (!forme.length) return 0;
            var bordi = L.latLngBounds([]);
            for (var j = 0; j < forme.length; j++) {
                var forma = forme[j], aip = forma.sectorlab.p.indexOf('aip:') === 0;
                forma.setStyle({ color: aip ? token('--lab-lato-aip', '#e09a1a') : token('--lab-lato-sector', '#3a8fe0'),
                                 weight: 4, opacity: 1, dashArray: aip ? '8,4' : null });
                if (forma.bringToFront) forma.bringToFront();
                if (forma.getBounds && forma.getBounds().isValid()) bordi.extend(forma.getBounds());
                else if (forma.getLatLng) bordi.extend(forma.getLatLng());
            }
            stato.coppia = forme;
            if (inquadra && bordi.isValid()) {
                var sw = bordi.getSouthWest(), ne = bordi.getNorthEast();
                if (sw.lat === ne.lat && sw.lng === ne.lng) stato.mappa.setView(sw, Math.max(stato.mappa.getZoom(), 11));
                else stato.mappa.fitBounds(bordi.pad(.3));
            }
            return forme.length;
        },

        /// Tutti gli strati chiesti sono arrivati: il totale va nel diario (e la prova automatica aspetta questa riga).
        disegnata: function (elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            window.sectorlab.diario('mappa disegnata: ' + Object.keys(stato.strati).length + ' strati, ' +
                (stato.forme_disegnate || 0) + ' forme in ' + (stato.tempo || 0) + ' ms');
        },

        /// L'anteprima di «incolla da testo»: la forma che uscirebbe, tratteggiata sopra quella di oggi, e i centri
        /// degli archi. Senza punti si toglie. La prima volta che compare, la mappa ci si porta sopra; poi, mentre l'AOD
        /// scrive o cambia la densità, resta ferma — si vede la forma cambiare, non la mappa saltare.
        anteprima: function (punti, centri, elemento) {
            var stato = prendi(elemento);
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
        spenti: function (chiavi, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            stato.spenti = {};
            for (var i = 0; i < (chiavi || []).length; i++) stato.spenti[chiavi[i]] = true;
            var ids = Object.keys(stato.strati);
            for (var j = 0; j < ids.length; j++) applicaSpenti(stato, ids[j]);
            if (stato.vistaVoluta && stato.vistaVoluta.length) this.vista(stato.vistaVoluta, elemento, stato.vistaInGrigio);
        },

        /// `grigio` (il fuoco, D1): le forme fuori dalla vista restano sulla mappa in grigio e senza clic, invece di sparire.
        vista: function (chiavi, elemento, grigio) {
            var stato = prendi(elemento);
            if (!stato) return 0;
            stato.vistaVoluta = chiavi;
            stato.vistaInGrigio = !!grigio;
            if (stato.gruppoDellaVista) { stato.mappa.removeLayer(stato.gruppoDellaVista); stato.gruppoDellaVista = null; }
            var ids = Object.keys(stato.strati);
            // Il grigio di prima si toglie a tutte: si rimette sotto, solo dove serve.
            for (var g = 0; g < ids.length; g++) stato.strati[ids[g]].eachLayer(function (l) { inGrigio(l, false); });

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
                if (grigio) {
                    // Lo strato resta intero; le forme fuori dalla vista vanno in grigio.
                    if (!stato.mappa.hasLayer(strato)) strato.addTo(stato.mappa);
                    strato.eachLayer(function (l) {
                        var f = l.sectorlab;
                        var dentro = f && (volute[f.p + '#' + f.r] || volute[f.p + '#*']);
                        if (dentro) gruppo.addLayer(l);
                        else inGrigio(l, true);
                    });
                    continue;
                }
                stato.mappa.removeLayer(strato);
                strato.eachLayer(function (l) {
                    var f = l.sectorlab;
                    if (f && (volute[f.p + '#' + f.r] || volute[f.p + '#*'])) gruppo.addLayer(l);
                });
            }
            if (!grigio) {
                gruppo.addTo(stato.mappa);
                stato.gruppoDellaVista = gruppo;
            }
            return gruppo.getLayers().length;
        },

        /// Il fuoco (carta «Lavorare su uno scalo», R2): la mappa va sul riquadro dello scalo. Come l'inquadratura sulle coste,
        /// aspetta che la mappa abbia una misura (nascosta, la fa il ResizeObserver quando compare).
        inquadra: function (sud, ovest, nord, est, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            var bordi = L.latLngBounds([sud, ovest], [nord, est]);
            if (!bordi.isValid()) return;
            // Si ricorda fuori dallo stato della mappa: una mappa rifatta (crea) che riceve le coste dopo ci torna.
            fuochi[elemento] = bordi.pad(.1);
            stato.daInquadrare = bordi.pad(.1);
            // Le coste che arrivano dopo non riportano la mappa sull'Italia.
            stato.inquadrato = true;
            inquadraSeAspetta(stato);
        },

        /// Uscendo dal fuoco: una mappa rifatta torna a mettersi sull'Italia.
        scordaIlFuoco: function (elemento) {
            delete fuochi[elemento];
        },

        /// S3: la carta posata ('/carta/immagine?v=…'), i tre angoli [[lat,lon] in alto a sinistra, in alto a destra, in
        /// basso a sinistra] e l'opacità; `inquadra` porta la mappa sulla carta (all'apertura di una posa salvata). Senza
        /// indirizzo si toglie. Sta sotto le forme (un riquadro suo, sotto overlayPane) e non prende i clic.
        carta: function (indirizzo, angoli, opacita, inquadra, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            if (!indirizzo || !angoli || angoli.length < 3) { togliLaCarta(stato); return; }
            var mappa = stato.mappa;
            if (!mappa.getPane('sectorlabCarta')) {
                var riquadro = mappa.createPane('sectorlabCarta');
                riquadro.style.zIndex = 350;
                riquadro.style.pointerEvents = 'none';
            }
            if (!stato.carta || stato.carta.indirizzo !== indirizzo) {
                togliLaCarta(stato);
                var img = L.DomUtil.create('img', 'leaflet-image-layer leaflet-zoom-hide lab-carta-posata', mappa.getPane('sectorlabCarta'));
                img.alt = '';
                img.draggable = false;
                var c = stato.carta = { img: img, indirizzo: indirizzo, angoli: angoli };
                c.metti = function () { posizionaLaCarta(stato); };
                img.onload = function () { if (stato.carta === c) c.metti(); };
                mappa.on('zoomend viewreset', c.metti);
                img.src = indirizzo;
            }
            stato.carta.angoli = angoli;
            stato.carta.inquadra = stato.carta.inquadra || !!inquadra;
            stato.carta.img.style.opacity = opacita;
            posizionaLaCarta(stato);
        },

        /// S3: la mappa aspetta il posto di un punto della posa. Il clic su un punto dell'AIP ne prende le coordinate (vedi
        /// sopra, nel clic delle forme); altrove, dove si è cliccato.
        prendi: function (acceso, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            stato.prende = !!acceso;
            stato.mappa.getContainer().classList.toggle('lab-mappa-prende', stato.prende);
            if (!stato.suPresa) {
                stato.suPresa = function (e) { if (stato.prende) presa(stato, e.latlng.lat, e.latlng.lng, null); };
                stato.mappa.on('click', stato.suPresa);
            }
        },

        /// F9 (R1-R6): le maniglie dei vertici del record scelto — [{ campo, posizione, lat, lon, perNome, aggiungibile }],
        /// vuoto le toglie; `diChi` è il record ('file#3'), che la calamita non prende. Un vertice scritto col nome ha una
        /// maniglia vuota e si sposta solo sulla calamita di un punto (R6). Fra due vertici di fila di un elenco che sa
        /// scrivere coordinate, una maniglia di mezzo più piccola: trascinata, aggiunge lì un vertice (R3). Il clic destro
        /// su una maniglia la toglie (R4).
        maniglie: function (elenco, elemento, diChi) {
            var stato = prendi(elemento);
            if (!stato) return 0;
            // 🔴 Mai sotto il dito: rifatte a metà trascinamento, la maniglia presa sparirebbe e il gesto con lei.
            if (stato.trascina) { stato.maniglieInAttesa = { elenco: elenco || [], diChi: diChi }; return 0; }
            togliLeManiglie(stato);
            if (!elenco || !elenco.length) return 0;
            var gruppo = L.layerGroup(), perCampo = {}, ferma = function (e) { L.DomEvent.stop(e); };
            for (var i = 0; i < elenco.length; i++) {
                (function (m) {
                    // R6: un vertice col nome (o di un elenco fatto di nomi) si trascina, ma solo la calamita su un punto lo
                    // sposta: lasciato altrove torna dov'era.
                    var soloNomi = !m.punto && (m.perNome || !m.aggiungibile);
                    // D4 (R14, R16): la maniglia di un fix, VOR, NDB o punto VFR — il punto stesso, e chi lo cita.
                    var titolo = m.punto
                        ? 'Il punto' + (m.citatoDa ? ' — citato da ' + m.citatoDa + ' record: lo seguono' : '')
                        : (m.posizione + 1) + (soloNomi ? ' — è il nome di un punto: va solo su un altro punto col nome' : '') + ' (clic destro: togli)';
                    var marcatore = L.marker([m.lat, m.lon], {
                        icon: icona(m.punto ? 'lab-maniglia-punto' : m.perNome ? 'lab-maniglia-nome' : null, m.punto ? 16 : 12), draggable: true,
                        autoPan: true, keyboard: false, zIndexOffset: 1000, title: titolo
                    });
                    // Il clic su una maniglia non sceglie la forma che c'è sotto.
                    marcatore.on('click', ferma);
                    trascinabile(stato, marcatore, function (qui) {
                        var suoi = stato.maniglie.perCampo[m.campo] || {}, prima = suoi[m.posizione - 1], dopo = suoi[m.posizione + 1], tratti = [];
                        if (prima) tratti.push([prima.getLatLng(), qui]);
                        if (dopo) tratti.push([qui, dopo.getLatLng()]);
                        return tratti;
                    }, function (dove, preso) {
                        return stato.riferimento.invokeMethodAsync('VerticeDallaMappa', m.campo, m.posizione, dove.lat, dove.lng,
                                                                   preso ? preso.p : null, preso ? preso.r : 0);
                    }, soloNomi);
                    // Un punto non si toglie dalla mappa (si toglie il record, dalla Scheda).
                    if (!m.punto) menuDelVertice(stato, m, marcatore);
                    else marcatore.on('contextmenu', ferma);
                    (perCampo[m.campo] = perCampo[m.campo] || {})[m.posizione] = { marcatore: marcatore, dati: m };
                    gruppo.addLayer(marcatore);
                })(elenco[i]);
            }
            // Le maniglie di mezzo, dopo quelle dei vertici: sotto di loro (zIndexOffset più basso).
            for (var j = 0; j < elenco.length; j++) {
                (function (m) {
                    var dopo = (perCampo[m.campo] || {})[m.posizione + 1];
                    if (!m.aggiungibile || !dopo) return;
                    var a = L.latLng(m.lat, m.lon), b = L.latLng(dopo.dati.lat, dopo.dati.lon);
                    var mezzo = L.marker([(a.lat + b.lat) / 2, (a.lng + b.lng) / 2], {
                        icon: icona('lab-maniglia-mezzo', 8), draggable: true, autoPan: true, keyboard: false, zIndexOffset: 500,
                        title: 'Trascina per aggiungere un vertice fra il ' + (m.posizione + 1) + ' e il ' + (m.posizione + 2)
                    });
                    mezzo.on('click', ferma);
                    mezzo.on('contextmenu', ferma);
                    trascinabile(stato, mezzo, function (qui) { return [[a, qui], [qui, b]]; }, function (dove, preso) {
                        return stato.riferimento.invokeMethodAsync('VerticeAggiuntoDallaMappa', m.campo, m.posizione + 1, dove.lat, dove.lng,
                                                                   preso ? preso.p : null, preso ? preso.r : 0);
                    }, false);
                    gruppo.addLayer(mezzo);
                })(elenco[j]);
            }
            // indica() cerca il marcatore del vertice.
            for (var campo in perCampo) {
                for (var posizione in perCampo[campo]) perCampo[campo][posizione] = perCampo[campo][posizione].marcatore;
            }
            gruppo.addTo(stato.mappa);
            stato.maniglie = { gruppo: gruppo, perCampo: perCampo, diChi: diChi || null };
            return elenco.length;
        },

        /// §9 (R10): comincia, aggiorna o finisce il disegno a clic. `opzioni` = { chiudi, soloNomi, diChi } o null (finito).
        disegna: function (opzioni, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            if (!opzioni) { chiudiIlDisegno(stato); return; }
            if (stato.disegno) {
                // Solo la casella «Chiudi la forma» è cambiata: i punti restano.
                stato.disegno.chiudi = !!opzioni.chiudi;
                anteprimaDelDisegno(stato, null);
                return;
            }
            var mappa = stato.mappa, tinta = token('--lab-evidenza', '#e26e17');
            var d = stato.disegno = {
                punti: [], chiudi: !!opzioni.chiudi, soloNomi: !!opzioni.soloNomi, diChi: opzioni.diChi || null, griglia: {},
                linea: L.polyline([], { color: tinta, weight: 2, dashArray: '6,4', interactive: false }).addTo(mappa),
                elastico: L.polyline([], { color: tinta, weight: 1, dashArray: '2,4', interactive: false }).addTo(mappa),
                segni: L.layerGroup().addTo(mappa)
            };
            // La griglia della calamita si rifà quando la mappa si muove o cambia zoom (il piano dei layer cambia con lo zoom).
            d.rifai = function () { d.griglia = raccogli(stato, d.soloNomi, d.diChi); };
            d.rifai();
            d.suClic = function (e) { puntoDelDisegno(stato, e.latlng); };
            d.suMuovi = function (e) {
                var preso = altTenuto ? null : piuVicino(stato, d.griglia, e.latlng), qui = preso ? L.latLng(preso.lat, preso.lon) : e.latlng;
                if (preso && (!d.soloNomi || preso.punto)) {
                    if (!stato.anello) stato.anello = L.circleMarker(qui, { radius: 10, color: tinta, weight: 2, fill: false, interactive: false }).addTo(mappa);
                    stato.anello.setLatLng(qui);
                } else if (stato.anello) {
                    mappa.removeLayer(stato.anello);
                    stato.anello = null;
                }
                anteprimaDelDisegno(stato, qui);
            };
            d.suDoppio = function (e) { L.DomEvent.stop(e); mandaIlDisegno(stato); };
            d.suTasto = function (e) {
                var t = e.target, scrive = t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName));
                if (e.key === 'Escape') {
                    e.preventDefault();
                    if (stato.riferimento) stato.riferimento.invokeMethodAsync('DisegnoLasciato').catch(function () { /* il circuito se n'è andato */ });
                } else if (e.key === 'Enter' && !scrive) {
                    e.preventDefault();
                    mandaIlDisegno(stato);
                } else if (e.key === 'Backspace' && !scrive) {
                    e.preventDefault();
                    d.punti.pop();
                    anteprimaDelDisegno(stato, null);
                }
            };
            mappa.on('click', d.suClic).on('mousemove', d.suMuovi).on('dblclick', d.suDoppio).on('zoomend moveend', d.rifai);
            document.addEventListener('keydown', d.suTasto, true);
            mappa.doubleClickZoom.disable();
            L.DomUtil.addClass(mappa.getContainer(), 'lab-mappa-prende');
        },

        /// R7: la maniglia del vertice che il mouse indica nella Scheda (campo null: nessuna).
        indica: function (campo, posizione, elemento) {
            var stato = prendi(elemento);
            if (!stato || !stato.maniglie) return;
            if (stato.indicata && stato.indicata.getElement()) L.DomUtil.removeClass(stato.indicata.getElement(), 'lab-maniglia-indicata');
            stato.indicata = campo === null ? null : (stato.maniglie.perCampo[campo] || {})[posizione] || null;
            if (stato.indicata && stato.indicata.getElement()) L.DomUtil.addClass(stato.indicata.getElement(), 'lab-maniglia-indicata');
        },

        /// I colori della mappa: di Aurora (col fondo dello schermo radar) o del Lab. Le forme hanno già tutti e due.
        colori: function (aurora, sfondo, simboli, elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            stato.aurora = !!aurora;
            stato.sfondo = sfondo || null;
            stato.simboli = simboli || [];
            stato.immagini = {};
            fondo(stato);
            this.ricolora(elemento || 'lab-mappa');
        },

        /// Il tema o il modo è cambiato: ogni forma riprende il suo colore (le coordinate non si richiedono). Senza
        /// elemento (il tema) tutte le mappe.
        ricolora: function (elemento) {
            if (!elemento) {
                for (var chiave in mappe) if (Object.prototype.hasOwnProperty.call(mappe, chiave)) this.ricolora(chiave);
                return;
            }
            var stato = prendi(elemento);
            if (!stato) return;
            for (var id in stato.strati) {
                if (!Object.prototype.hasOwnProperty.call(stato.strati, id)) continue;
                stato.strati[id].eachLayer(function (l) {
                    if (l !== stato.evidenza && !(stato.coppia && stato.coppia.indexOf(l) >= 0)) applica(l);
                });
            }
            if (stato.evidenza && stato.evidenza.setStyle) stato.evidenza.setStyle({ color: token('--lab-evidenza', '#e26e17') });
            if (stato.anteprima) {
                var anteprima = token('--lab-anteprima', '#196b35');
                stato.anteprima.eachLayer(function (l) { if (l.setStyle) l.setStyle({ color: anteprima }); });
            }
        },

        chiudi: function (elemento) {
            var stato = prendi(elemento);
            if (!stato) return;
            if (stato.osservatore) stato.osservatore.disconnect();
            // Il disegno ha un ascoltatore sul documento: va tolto, la mappa no.
            chiudiIlDisegno(stato);
            stato.mappa.remove();
            // Una fetch ancora in viaggio la trova chiusa e non disegna niente.
            stato.chiusa = true;
            delete mappe[elemento || 'lab-mappa'];
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
