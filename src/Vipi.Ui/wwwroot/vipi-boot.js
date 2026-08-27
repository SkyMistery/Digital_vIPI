// Due compiti, e sono lo stesso compito visto in due momenti: far trovare a ogni schermata gli strumenti
// che le servono — e SOLO quelli.
//
//   1. carica i moduli pesanti la prima volta che una pagina ne mostra il bersaglio (mappe, carte delle
//      minime, viewer 3D, tour), invece di spedirli a chiunque apra qualunque cosa;
//   2. li riaggancia dopo ogni navigazione «enhanced» di Blazor, che rimpiazza il DOM e perderebbe lo
//      stile zoom inline e gli handler agganciati a mano.
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
    var qui = document.currentScript;

    // ── 1. I moduli che non servono a tutti ──────────────────────────────────────────────────────────
    //
    // Misurato il 27 agosto 2026: questi quattro file pesavano 13 029 byte compressi su OGNI pagina —
    // ricerca, incarichi, elenchi, guida, login, hub — mentre servono alle sole schermate che mostrano una
    // mappa, una carta delle minime, uno stage 3D o il giro di presentazione dell'editor.
    //
    // ⚠️ Il criterio è il DOM, non l'indirizzo. Un elenco di percorsi sarebbe una seconda copia della
    // tabella delle rotte, da tenere allineata per sempre; il bersaglio invece è la cosa stessa su cui il
    // modulo lavora — se c'è, serve; se non c'è, non poteva fare niente comunque.
    //
    // ⚠️ Niente di tutto questo è chiamato dall'interop di Blazor: sono moduli che si agganciano da soli a
    // quel che trovano. È la ragione per cui vipi-editor.js e vipi-media.js NON stanno in questa lista —
    // quelli il codice C# li chiama per nome (vipiSetDirty, vipiMedia.osserva), e un modulo che arriva un
    // istante dopo la chiamata è un guasto silenzioso invece che qualche byte risparmiato.
    var moduli = [
        // [ nome del data- con l'indirizzo, che cosa deve esserci nella pagina, funzione di riaggancio, attributi da riportare ]
        ['aor',   '.aor-leaflet, .aor-chip, .cfg-btn, .cfg-collapse, [data-areacard]', 'vipiInitAor',   ['data-leaflet-src', 'data-leaflet-css']],
        ['mva',   '.mva-leaflet, .mva-base, .mva-chip',                                'vipiInitMva',   []],
        ['aor3d', '.aor3d-stage, .aor3d-z, .aor3d-legend, .aor3d-hint, .aor-vm-btn',   'vipiInitAor3d', ['data-three-src']],
        ['tour',  '[data-tour]',                                                        'vipiMaybeTour', []]
    ];

    var caricati = {};

    function carica(m) {
        var chiave = m[0];
        if (caricati[chiave]) return;

        var indirizzo = qui && qui.getAttribute('data-' + chiave + '-src');
        if (!indirizzo) return;                      // non dichiarato: non è un guasto, è una pagina che non lo vuole

        caricati[chiave] = true;

        var el = document.createElement('script');
        el.src = indirizzo;
        // Gli indirizzi delle librerie vendorizzate viaggiano sul tag, come prima: è il modulo a decidere
        // QUANDO tirarsele dentro (Leaflet alla prima mappa, three.js alla prima apertura del 3D).
        for (var i = 0; i < m[3].length; i++) {
            var extra = qui.getAttribute(m[3][i]);
            if (extra) el.setAttribute(m[3][i], extra);
        }
        document.head.appendChild(el);
    }

    function caricaQuelliCheServono() {
        for (var i = 0; i < moduli.length; i++) {
            try {
                if (document.querySelector(moduli[i][1])) carica(moduli[i]);
            } catch (e) {
                console.warn('[vipi] non ho potuto caricare il modulo «' + moduli[i][0] + '»', e);
            }
        }
    }

    // ── 2. Il riaggancio dopo una navigazione ────────────────────────────────────────────────────────
    var passi = [
        ['vipiApplyTema', 'tema'],
        ['vipiApplyZoom', 'zoom'],
        ['vipiWireUi', 'interattività'],
        ['vipiInitScreens', 'schermate']
    ];
    // I moduli caricati su richiesta si riagganciano con la loro funzione, se a quel punto ci sono.
    for (var i = 0; i < moduli.length; i++) passi.push([moduli[i][2], 'modulo ' + moduli[i][0]]);

    caricaQuelliCheServono();

    Blazor.addEventListener('enhancedload', function () {
        // ⚠️ PRIMA il caricamento: dopo una navigazione la pagina nuova può mostrare una mappa dove quella
        // di prima non ne aveva. Un modulo appena inserito si aggancia da sé al proprio DOMContentLoaded —
        // che è già passato — quindi lo riprende comunque il giro di riagganci qui sotto, al più tardi alla
        // navigazione successiva; e l'osservatore di mutazioni delle minime copre l'intervallo.
        caricaQuelliCheServono();

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
