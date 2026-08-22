// Ricollega zoom e interattività dopo ogni navigazione «enhanced» di Blazor, che rimpiazza il DOM e
// perderebbe lo stile zoom inline e gli handler agganciati a mano.
//
// Era uno <script> inline in fondo a App.razor. Sta in un file per la stessa ragione di vipi-zoom.js:
// uno script inline obbliga a `script-src 'unsafe-inline'` nella CSP.
//
// `Blazor` esiste perché blazor.web.js viene caricato prima di questo file: l'ordine nel <body> conta.
Blazor.addEventListener('enhancedload', function () {
    window.vipiApplyTema && window.vipiApplyTema();
    window.vipiApplyZoom && window.vipiApplyZoom();
    window.vipiWireUi && window.vipiWireUi();
    window.vipiInitScreens && window.vipiInitScreens();
    window.vipiInitAor && window.vipiInitAor();
    window.vipiInitAor3d && window.vipiInitAor3d();
    window.vipiMaybeTour && window.vipiMaybeTour();
});
