// Logica delle schermate di prototipo (Ridotta, Aeroporto, ...), portata dal mockup v2.
// Ogni schermata ha una init idempotente; le pagine la chiamano dopo il render e su 'enhancedload'.
(function () {
    // ===================== VISTA RIDOTTA =====================
    var xOnline = { WS2: true, ES2: false, CE1: true, TS: false, DTTC: false, LIRP_APP: false };
    var apts = [
        { code: 'LIRF' }, { code: 'LIRA' },
        { code: 'LIRN', heldBy: 'TS' },
        { code: 'LIRQ' },
        { code: 'LIRP', heldBy: 'LIRP_APP' },
    ];
    var redView = 'sector', redCode = null, redAptRwy = '16R';

    var redSids = [
        { fix: 'VALMA', sid: 'VALMA 5A', rwy: ['16L', '16R'], trans: ['—'], climb: '5000 ft', cat: 'A–D', wtc: 'L/M/H' },
        { fix: 'ELKAP', sid: 'ELKAP 3B', rwy: ['16L', '16R'], trans: ['ELKAP → OST', 'ELKAP → DEVOX'], climb: '6000 ft', cat: 'C–D', wtc: 'M/H' },
        { fix: 'TARQ', sid: 'TARQ 2D', rwy: ['34L', '34R'], trans: ['—'], climb: '5000 ft', cat: 'A–C', wtc: 'L/M' },
        { fix: 'OST', sid: 'OST 1C', rwy: ['07', '25'], trans: ['OST → TARQ', 'OST → ELB'], climb: 'FL080', cat: 'A–D', wtc: 'L/M/H' },
    ];
    var xfer = [
        { pair: 'Roma ↔ Milano', apt: 'LIMC', phase: 'arr', cop: 'VALMA', fl: 'FL280+', chain: ['WS2'] },
        { pair: 'Roma ↔ Milano', apt: 'LIMC', phase: 'arr', cop: 'DEVOX', fl: 'FL250+', chain: ['ES2', 'WS2'] },
        { pair: 'Roma ↔ Milano', apt: 'LIMC', phase: 'arr', cop: 'RIXUV', fl: 'FL240+', chain: ['ES2', 'WS2'] },
        { pair: 'Roma ↔ Milano', apt: 'LIML', phase: 'arr', cop: 'ELB', fl: 'FL310+', chain: ['WS2'] },
        { pair: 'Roma ↔ Milano', apt: 'LIRP', phase: 'dep', cop: 'TARQ', fl: 'FL230+', chain: ['WS2'] },
        { pair: 'Roma ↔ Padova', apt: 'LIPE', phase: 'arr', cop: 'OSKOR', fl: 'FL330+', chain: ['CE1'] },
        { pair: 'Roma ↔ Padova', apt: 'LIRZ', phase: 'dep', cop: 'BAGNO', fl: 'FL250+', chain: ['CE1'] },
        { pair: 'Roma ↔ Roma (interno)', apt: 'LIRN', phase: 'arr', cop: 'PESET', fl: 'FL130-', chain: ['TS'], std: 'tieni' },
        { pair: 'Roma ↔ Roma (interno)', apt: 'LIRN', phase: 'arr', cop: 'TEANO', fl: 'FL150-', chain: ['TS'], std: 'tieni' },
        { pair: 'Roma ↔ Tunisi (DTTC)', apt: 'DTTA', phase: 'arr', cop: 'ESEBA', fl: 'FL350+', chain: ['DTTC'], std: 'boundary' },
        { pair: 'Roma ↔ Tunisi (DTTC)', apt: 'DTTA', phase: 'arr', cop: 'PESUN', fl: 'FL310+', chain: ['DTTC'], std: 'boundary' },
    ];
    var NEXT_PALETTE = [
        { bg: '#e2e8ff', fg: '#0D2C99' }, { bg: '#dafbe7', fg: '#0f7a37' }, { bg: '#f0e8ff', fg: '#6a3fb5' },
        { bg: '#ffe7d4', fg: '#9a4a00' }, { bg: '#ffe0e6', fg: '#b51d1d' }, { bg: '#d6f1fb', fg: '#0b6b86' },
    ];
    var NEXT_FIXED = { WS2: 0, ES2: 1, CE1: 2, TS: 3, DTTC: 4 };

    function flCls(fl) { return fl.indexOf('-') >= 0 ? 'down' : 'up'; }
    function resolveNext(r) { for (var i = 0; i < r.chain.length; i++) { if (xOnline[r.chain[i]]) return r.chain[i]; } return 'UNICOM'; }
    function nextStyle(to) {
        if (/Confine|UNICOM/.test(to)) return 'background:#eceef5;color:#555;border-left:3px solid #9aa0b0';
        var idx = NEXT_FIXED[to];
        if (idx === undefined) { var h = 0; for (var i = 0; i < to.length; i++) h = (h * 31 + to.charCodeAt(i)) >>> 0; idx = h % NEXT_PALETTE.length; }
        var c = NEXT_PALETTE[idx % NEXT_PALETTE.length];
        return 'background:' + c.bg + ';color:' + c.fg + ';border-left:3px solid ' + c.fg;
    }
    function rowVisible(r) { return r.std === 'tieni' ? r.chain.some(function (s) { return xOnline[s]; }) : true; }

    function aptRender() {
        var box = document.getElementById('apt-quick'); if (!box) return;
        var mine = apts.filter(function (a) { return !a.heldBy || !xOnline[a.heldBy]; });
        var gone = apts.filter(function (a) { return a.heldBy && xOnline[a.heldBy]; });
        box.innerHTML =
            '<span class="ch sw' + (redView === 'sector' ? ' on' : '') + '" data-view="sector">📡 Il mio settore</span>' +
            mine.map(function (a) { return '<span class="ch sw' + (redView === 'apt' && redCode === a.code ? ' on' : '') + '" data-view="apt" data-code="' + a.code + '">' + a.code + '</span>'; }).join('') +
            gone.map(function (a) { return '<span class="ch sw ch-deleg' + (redView === 'apt' && redCode === a.code ? ' on' : '') + '" data-view="apt" data-code="' + a.code + '" title="controllato da altra posizione online">' + a.code + ' ·delegato</span>'; }).join('');
    }
    function aptPanel(code) {
        var pills = ['16L', '16R', '34L', '34R', '07', '25'].map(function (r) { return '<span class="rap-pill' + (r === redAptRwy ? ' on' : '') + '" data-rwy="' + r + '">' + r + '</span>'; }).join('');
        var rows = []; redSids.filter(function (s) { return s.rwy.indexOf(redAptRwy) >= 0; }).forEach(function (s) { s.trans.forEach(function (tr) { rows.push(Object.assign({}, s, { tr: tr })); }); });
        var body = rows.length
            ? rows.map(function (s) { return '<tr><td><b>' + s.fix + '</b></td><td>' + s.sid + '</td><td>' + s.tr + '</td><td>' + s.climb + '</td><td>' + s.cat + '</td><td>' + s.wtc + '</td></tr>'; }).join('')
            : '<tr><td colspan="6" class="muted" style="font-size:12.5px">Nessuna SID per pista ' + redAptRwy + '.</td></tr>';
        var ap = apts.find(function (a) { return a.code === code; }); var deleg = ap && ap.heldBy && xOnline[ap.heldBy];
        var banner = deleg ? '<div class="callout warning" style="margin:0 0 12px"><span class="cic">🔁</span><div><p class="ct">Controllato da ' + ap.heldBy + ' (online)</p>Non è sotto il tuo controllo diretto ora — consultazione in sola lettura.</div></div>' : '';
        return '<div class="rap"><div class="rap-h"><b>' + code + '</b><span class="muted">vista rapida</span></div>' + banner +
            '<div class="rap-quick"><div class="rap-box"><span class="muted">TA</span><b>6000 ft</b></div><div class="rap-box"><span class="muted">TL · QNH 1015</span><b>FL75</b></div><div class="rap-box"><span class="muted">Piste suggerite</span><b>16R dep · 16L arr</b></div></div>' +
            '<div class="rap-rwy"><span class="muted">Pista selezionata</span>' + pills + '</div>' +
            '<div style="overflow:auto"><table class="sid-table" style="min-width:560px"><thead><tr><th>Punto</th><th>SID</th><th>Transition</th><th>Salita iniz.</th><th>Cat.</th><th>WTC</th></tr></thead><tbody>' + body + '</tbody></table></div></div>';
    }
    function showRed() {
        var sec = document.getElementById('red-sector'), apt = document.getElementById('red-apt'); if (!sec || !apt) return;
        if (redView === 'apt') { sec.hidden = true; apt.hidden = false; apt.innerHTML = aptPanel(redCode); }
        else { apt.hidden = true; sec.hidden = false; }
    }
    function xRender() {
        var box = document.getElementById('xfer-pairs'); if (!box) return; box.innerHTML = '';
        var pairs = []; xfer.forEach(function (r) { if (pairs.indexOf(r.pair) < 0) pairs.push(r.pair); });
        pairs.forEach(function (p) {
            var rowsP = xfer.filter(function (r) { return r.pair === p && rowVisible(r); });
            if (!rowsP.length) return;
            var body = '';
            [['arr', 'Arrivi'], ['dep', 'Partenze']].forEach(function (ph) {
                var rowsPh = rowsP.filter(function (r) { return r.phase === ph[0]; }); if (!rowsPh.length) return;
                var aptsPh = []; rowsPh.forEach(function (r) { if (aptsPh.indexOf(r.apt) < 0) aptsPh.push(r.apt); });
                var cards = aptsPh.map(function (a) {
                    var rows = rowsPh.filter(function (r) { return r.apt === a; });
                    var pts = rows.map(function (r) { var n = resolveNext(r); return '<div class="xt-pt"><span class="cop">' + r.cop + '</span><b class="fl ' + flCls(r.fl) + '">' + r.fl + '</b><span class="xt-next" style="' + nextStyle(n) + '">→ <b>' + n + '</b></span></div>'; }).join('');
                    return '<div class="xt-card"><div class="xt-card-h"><b class="xt-dest">' + a + '</b><span class="muted" style="font-size:11px">' + rows.length + ' pt</span></div>' + pts + '</div>';
                }).join('');
                body += '<div class="xt-sub-h">' + ph[1] + '</div><div class="xt-cards">' + cards + '</div>';
            });
            box.insertAdjacentHTML('beforeend', '<div class="xpair"><div class="xpair-h">' + p + '</div><div class="xpair-body">' + body + '</div></div>');
        });
        aptRender();
    }

    window.vipiInitReduced = function () {
        if (!document.getElementById('xfer-pairs')) return;
        document.querySelectorAll('.xo-chip').forEach(function (ch) {
            ch.onclick = function () { ch.classList.toggle('on'); xOnline[ch.dataset.sec] = ch.classList.contains('on'); xRender(); };
            ch.classList.toggle('on', !!xOnline[ch.dataset.sec]);
        });
        var box = document.getElementById('apt-quick');
        if (box) box.onclick = function (e) {
            var ch = e.target.closest('.sw'); if (!ch) return;
            if (ch.dataset.view === 'sector') { redView = 'sector'; redCode = null; } else { redView = 'apt'; redCode = ch.dataset.code; }
            showRed(); aptRender();
        };
        var apt = document.getElementById('red-apt');
        if (apt) apt.onclick = function (e) { var pill = e.target.closest('.rap-pill'); if (pill) { redAptRwy = pill.dataset.rwy; showRed(); } };
        xRender(); showRed();
    };

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
        window.vipiInitReduced && window.vipiInitReduced();
        window.vipiInitAirport && window.vipiInitAirport();
    };
    document.addEventListener('DOMContentLoaded', function () { window.vipiInitScreens(); });
})();
