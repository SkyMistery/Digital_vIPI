// Logica delle schermate di prototipo (Ridotta, Aeroporto, ...), portata dal mockup v2.
// Ogni schermata ha una init idempotente; le pagine la chiamano dopo il render e su 'enhancedload'.
(function () {
    // ⚠️ La sezione «VISTA RIDOTTA» stava qui ed e' stata RIMOSSA il 23 agosto 2026.
    // Era il prototipo del mockup v2, e nessuna pagina la montava piu'. Non era pero' inerte: cercava
    // `#xfer-pairs` — un id che esisteva davvero, nella vista live — e come prima cosa gli faceva
    // `innerHTML = ''`, sostituendo i coordinamenti veri con quelli finti del mockup fino al primo render
    // interattivo di Blazor. Agganciava inoltre un `onclick` a OGNI `.xo-chip`, comprese quelle della
    // catena di copertura. Carta: docs/feature/2026-08-23-live-coordinamenti-a-colonne.md.
    // ===================== AEROPORTO =====================
    var aptSids = [
        { fix: 'VALMA', sid: 'VALMA 5A', rwy: ['16L', '16R'], trans: ['—'], climb: '5000 ft', type: 'RNAV', cat: 'A–D', wtc: 'L/M/H', cond: '—', pref: true },
        { fix: 'VALMA', sid: 'VALMA 5C', rwy: ['34L', '34R'], trans: ['—'], climb: '5000 ft', type: 'RNAV', cat: 'A–D', wtc: 'L/M/H', cond: '—', pref: true },
        { fix: 'ELKAP', sid: 'ELKAP 3B', rwy: ['16L', '16R'], trans: ['ELKAP → OST', 'ELKAP → DEVOX'], climb: '6000 ft', type: 'CONV', cat: 'C–D', wtc: 'M/H', cond: 'Solo diurno', pref: true },
        { fix: 'OST', sid: 'OST 1C', rwy: ['07', '25'], trans: ['OST → TARQ', 'OST → ELB'], climb: 'FL080', type: 'RNAV+CONV', cat: 'A–D', wtc: 'L/M/H', cond: 'Riduzione rumore', pref: true },
        { fix: 'TARQ', sid: 'TARQ 2D', rwy: ['34L', '34R'], trans: ['—'], climb: '5000 ft', type: 'RNAV', cat: 'A–C', wtc: 'L/M', cond: '—', pref: true },
        { fix: 'XIBIL', sid: 'XIBIL 1J', rwy: ['07', '25'], trans: ['XIBIL → ELB', 'XIBIL → TARQ'], climb: 'FL090', type: 'RNAV', cat: 'A–D', wtc: 'L/M/H', cond: '—', pref: false },
    ];
    var aptRwy = 'all', aptQ = '', aptUserPicked = false;
    var pillCls = { 'RNAV': 'blue', 'CONV': 'grey', 'RNAV+CONV': 'green' };

    function sidRender() {
        var tb = document.getElementById('sid-body'); if (!tb) return;
        var rows = [];
        aptSids.filter(function (s) { return aptRwy === 'all' || s.rwy.indexOf(aptRwy) >= 0; })
            .forEach(function (s) { (s.trans || ['—']).forEach(function (tr) { rows.push(Object.assign({}, s, { tr: tr })); }); });
        if (aptQ) { var t = aptQ.toLowerCase(); rows = rows.filter(function (s) { return (s.fix + s.sid + s.tr + s.cond + s.type).toLowerCase().indexOf(t) >= 0; }); }
        rows.sort(function (a, b) { return (b.pref - a.pref) || a.fix.localeCompare(b.fix) || a.sid.localeCompare(b.sid); });
        tb.innerHTML = rows.map(function (s) {
            var cls = pillCls[s.type] || 'grey';
            return '<tr class="' + (s.pref ? 'sid-pref' : '') + '"><td>' + (s.pref ? '<span class="star">★</span>' : '') + '</td>' +
                '<td><b>' + s.fix + '</b></td><td>' + s.sid + (s.pref ? '<span class="pref-tag">PREF</span>' : '') + '</td>' +
                '<td>' + s.tr + '</td><td>' + s.climb + '</td><td><span class="pill ' + cls + '">' + s.type + '</span></td>' +
                '<td>' + s.cat + '</td><td>' + s.wtc + '</td><td>' + s.cond + '</td></tr>';
        }).join('');
        var empty = document.getElementById('sid-empty'); if (empty) empty.hidden = rows.length > 0;
    }
    function setActivePill(r) { document.querySelectorAll('.sid-pill').forEach(function (x) { x.classList.toggle('on', x.dataset.rwy === r); }); }

    function windCalc() {
        var wd = document.getElementById('windDir'); if (!wd) return;
        var dirs = [{ d: '16', h: 160, dep: '16R', arr: '16L' }, { d: '34', h: 340, dep: '34L', arr: '34R' },
                    { d: '07', h: 70, dep: '07', arr: '07' }, { d: '25', h: 250, dep: '25', arr: '25' }];
        function angDiff(a, b) { var x = Math.abs(a - b) % 360; return x > 180 ? 360 - x : x; }
        var dir = +wd.value, kt = +document.getElementById('windKt').value;
        var best = dirs[0], bd = 999; dirs.forEach(function (x) { var diff = angDiff(x.h, dir); if (diff < bd) { bd = diff; best = x; } });
        document.getElementById('rwyDep').textContent = best.dep;
        document.getElementById('rwyArr').textContent = best.arr;
        var head = Math.round(kt * Math.cos(bd * Math.PI / 180)), cross = Math.round(kt * Math.sin(bd * Math.PI / 180));
        var n = 'Vento in prua ~' + head + ' kt, vento al traverso ~' + Math.abs(cross) + ' kt su pista ' + best.d + '.';
        if (kt < 3) n = 'Vento calmo: pista in uso a discrezione (preferenziale ' + best.d + ').';
        var note = document.getElementById('rwyNote'); if (note) note.textContent = n;
        if (!aptUserPicked) { aptRwy = document.querySelector('.sid-pill[data-rwy="' + best.dep + '"]') ? best.dep : 'all'; setActivePill(aptRwy); sidRender(); }
    }

    window.vipiInitAirport = function () {
        if (!document.getElementById('sid-body')) return;
        document.querySelectorAll('.wx-tab').forEach(function (tab) {
            tab.onclick = function () {
                document.querySelectorAll('.wx-tab').forEach(function (t) { t.classList.remove('on'); });
                tab.classList.add('on');
                document.querySelectorAll('.wx-view').forEach(function (v) { v.hidden = v.dataset.wx !== tab.dataset.wx; });
            };
        });
        document.querySelectorAll('.sid-pill').forEach(function (p) {
            p.onclick = function () { aptUserPicked = (p.dataset.rwy !== 'all'); aptRwy = p.dataset.rwy; setActivePill(aptRwy); sidRender(); };
        });
        var ss = document.getElementById('sidSearch'); if (ss) ss.oninput = function () { aptQ = ss.value; sidRender(); };
        var wd = document.getElementById('windDir'), wk = document.getElementById('windKt');
        if (wd) wd.oninput = windCalc; if (wk) wk.oninput = windCalc;
        aptUserPicked = false; windCalc(); sidRender();
    };

    // Aggregatore: chiama tutte le init di schermata (ognuna è guardata sugli elementi presenti).
    window.vipiInitScreens = function () {
        window.vipiInitAirport && window.vipiInitAirport();
    };
    document.addEventListener('DOMContentLoaded', function () { window.vipiInitScreens(); });
})();
