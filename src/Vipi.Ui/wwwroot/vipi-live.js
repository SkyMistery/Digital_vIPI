// Transport live F3: sottoscrive l'endpoint SSE /vsop/live/atc e notifica il componente Blazor
// a ogni cambio della cache ATC. Il browser riconnette da solo su errore (EventSource nativo).
window.vipiLive = {
    _src: null,
    subscribe: function (dotnetRef) {
        this.unsubscribe();
        try {
            this._src = new EventSource('/vsop/live/atc');
            this._src.onmessage = function () {
                dotnetRef.invokeMethodAsync('OnLiveUpdate');
            };
            this._src.onerror = function () { /* reconnessione automatica del browser */ };
        } catch (e) { /* ambiente senza EventSource: nessun push, resta il render iniziale */ }
    },
    unsubscribe: function () {
        if (this._src) { this._src.close(); this._src = null; }
    }
};
