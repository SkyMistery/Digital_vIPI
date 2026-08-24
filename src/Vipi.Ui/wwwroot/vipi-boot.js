// Ricollega zoom e interattività dopo ogni navigazione «enhanced» di Blazor, che rimpiazza il DOM e
// perderebbe lo stile zoom inline e gli handler agganciati a mano.
//
// Era uno <script> inline in fondo a App.razor. Sta in un file per la stessa ragione di vipi-zoom.js:
// uno script inline obbliga a `script-src 'unsafe-inline'` nella CSP.
//
// `Blazor` esiste perché blazor.web.js viene caricato prima di questo file: l'ordine nel <body> conta.
//
// ⚠️ Ogni riaggancio nel proprio try/catch, e non sette chiamate in fila. Una catena nuda ha un difetto di
// forma che non dipende da cosa contiene: la PRIMA che lancia spegne tutte quelle dopo, per tutta la vita
// della pagina. Il caso vero era `vipiApplyZoom` in navigazione privata (localStorage che lancia sul solo
// accesso, vedi vipi-zoom.js) e portava via con sé chip AoR, mappe, persistenza del collasso e misura della
// topbar — cioè quattro cose che non c'entrano niente con lo zoom. Il rimedio a quel caso sta nel suo file;
// questo toglie di mezzo l'intera classe, compresa la prossima.
(function () {
    var passi = [
        ['vipiApplyTema', 'tema'],
        ['vipiApplyZoom', 'zoom'],
        ['vipiWireUi', 'interattività'],
        ['vipiInitScreens', 'schermate'],
        ['vipiInitAor', 'mappe AoR'],
        // Le minime hanno un osservatore di mutazioni che le riprende comunque, ma dipendere da quello
        // significa dipendere da un ritardo: dopo una navigazione si riagganciano qui, come le altre.
        ['vipiInitMva', 'carte delle minime'],
        ['vipiInitAor3d', 'AoR 3D'],
        ['vipiMaybeTour', 'tour']
    ];

    Blazor.addEventListener('enhancedload', function () {
        for (var i = 0; i < passi.length; i++) {
            var nome = passi[i][0];
            try {
                if (window[nome]) window[nome]();
            } catch (e) {
                // Si registra e si tira avanti: il resto della pagina non ha colpe.
                console.warn('[vipi] riaggancio «' + passi[i][1] + '» fallito dopo la navigazione', e);
            }
        }
    });
})();
