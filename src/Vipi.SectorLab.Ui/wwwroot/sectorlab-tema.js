// Aurora Sector Lab — il tema (automatico / chiaro / scuro) e le larghezze delle colonne, messi su <html> PRIMA che la
// pagina si disegni: sta nel <head>, senza defer. Dopo, un lampo bianco a ogni apertura per chi lavora al buio.
// Tutt'e due si ricordano nel localStorage della WebView2 (la sua cartella dei dati è in %LOCALAPPDATA%\VipiSectorLab):
// la finestra dei pannelli lo condivide con la principale, e l'evento «storage» porta la scelta dall'una all'altra.
(function () {
    'use strict';

    var TEMA = 'sectorlab.tema';            // assente = automatico (segue Windows) | 'chiaro' | 'scuro'
    var LARGHEZZE = 'sectorlab.larghezze';  // { sfoglia: 320, ispettore: 400, … } in pixel

    // ⚠️ localStorage può LANCIARE, non solo tornare null: un tema che non si ricorda è un fastidio, una pagina che non
    // si disegna è un guasto.
    function leggi(chiave) {
        try { return localStorage.getItem(chiave); } catch (e) { return null; }
    }

    function scrivi(chiave, valore) {
        try {
            if (valore === null) localStorage.removeItem(chiave); else localStorage.setItem(chiave, valore);
        } catch (e) { /* non si ricorda: vale per questa finestra e basta */ }
    }

    function applicaIlTema() {
        var tema = leggi(TEMA);
        var radice = document.documentElement;
        if (tema === 'chiaro' || tema === 'scuro') radice.setAttribute('data-tema', tema);
        else radice.removeAttribute('data-tema');
    }

    function applicaLeLarghezze() {
        var larghezze = {};
        try { larghezze = JSON.parse(leggi(LARGHEZZE) || '{}') || {}; } catch (e) { larghezze = {}; }
        var stile = document.documentElement.style;
        for (var i = stile.length - 1; i >= 0; i--) {
            if (stile[i].indexOf('--lab-l-') === 0) stile.removeProperty(stile[i]);
        }
        for (var nome in larghezze) {
            if (Object.prototype.hasOwnProperty.call(larghezze, nome) && typeof larghezze[nome] === 'number')
                stile.setProperty('--lab-l-' + nome, larghezze[nome] + 'px');
        }
    }

    applicaIlTema();
    applicaLeLarghezze();

    // L'altra finestra ha cambiato tema (o una larghezza): si segue.
    window.addEventListener('storage', function (e) {
        if (e.key === TEMA || e.key === null) applicaIlTema();
        if (e.key === LARGHEZZE || e.key === null) applicaLeLarghezze();
    });

    window.sectorlabTema = {
        /// Automatico → chiaro → scuro → automatico.
        cambia: function () {
            var tema = leggi(TEMA);
            var prossimo = tema === 'chiaro' ? 'scuro' : tema === 'scuro' ? null : 'chiaro';
            scrivi(TEMA, prossimo);
            applicaIlTema();
        },

        /// Una larghezza di colonna; null la toglie (si torna a quella di base).
        larghezza: function (nome, pixel) {
            var larghezze = {};
            try { larghezze = JSON.parse(leggi(LARGHEZZE) || '{}') || {}; } catch (e) { larghezze = {}; }
            if (pixel === null) delete larghezze[nome]; else larghezze[nome] = Math.round(pixel);
            scrivi(LARGHEZZE, JSON.stringify(larghezze));
        }
    };
})();
