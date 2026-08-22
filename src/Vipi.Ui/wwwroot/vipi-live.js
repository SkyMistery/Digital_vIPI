// Transport live F3: sottoscrive l'endpoint SSE /vsop/live/atc e notifica i componenti Blazor
// a ogni cambio della cache ATC. Il browser riconnette da solo su errore (EventSource nativo).
//
// Un solo EventSource per pagina, condiviso da piu' sottoscrittori: su /services/vsop/live convivono la pagina e il
// badge in topbar, e con un singolo riferimento il secondo subscribe() chiuderebbe lo stream del primo.
// L'identita' del riferimento .NET non e' confrontabile lato JS (ogni interop ne ricostruisce il wrapper),
// quindi subscribe() restituisce un id e chi si stacca lo ripassa a unsubscribe().
window.vipiLive = {
    _src: null,
    _subs: new Map(),
    _nextId: 1,

    subscribe: function (dotnetRef) {
        const id = this._nextId++;
        this._subs.set(id, dotnetRef);
        this._open();
        return id;
    },

    unsubscribe: function (id) {
        if (id === undefined || id === null) this._subs.clear();
        else this._subs.delete(id);
        if (this._subs.size === 0 && this._src) { this._src.close(); this._src = null; }
    },

    _open: function () {
        if (this._src) return;
        try {
            this._src = new EventSource('/vsop/live/atc');
            this._src.onmessage = () => {
                for (const [id, ref] of [...this._subs]) {
                    try {
                        const p = ref.invokeMethodAsync('OnLiveUpdate');
                        // Circuito chiuso senza dispose (tab in background, riavvio del server): il riferimento
                        // e' morto e resterebbe a rigettare a ogni evento. Si toglie da se'.
                        if (p && p.catch) p.catch(() => this.unsubscribe(id));
                    } catch (e) { this.unsubscribe(id); }
                }
            };
            this._src.onerror = () => { /* riconnessione automatica del browser */ };
        } catch (e) { /* ambiente senza EventSource: nessun push, resta il render iniziale */ }
    }
};
