// Ora locale del LETTORE accanto all'ora UTC scritta dal server.
//
// Il server emette sempre e solo UTC («14:32Z»): in Blazor Server non conosce il fuso di chi guarda, e
// `ToLocalTime()` gli darebbe quello della macchina — che non e' l'ora di nessuno. Vedi VipiTime.cs.
// Qui si aggiunge il pezzo che solo il browser sa: « · 16:32 CEST».
//
// COME si aggiunge, e perche' cosi':
//   - testo   → attributo `data-loc` sull'elemento, reso da CSS con `content: attr(data-loc)` su ::after.
//               NON si appende un nodo figlio: quell'elemento lo possiede Blazor, e un figlio estraneo
//               finirebbe in mezzo al suo diffing. Un attributo che Blazor non conosce lo lascia stare.
//   - tooltip → si accoda al `title` gia' scritto dal server (che senza JS resta un orario giusto, in UTC).
//
// Blazor ridisegna (l'heartbeat del lock batte ogni 60s e riscrive la barra): un MutationObserver
// riapplica su quello che ricompare. Idempotente per costruzione — `data-loc` si riscrive uguale, e al
// `title` si accoda solo se non finisce gia' col suffisso.
(function () {
    var ATTR = '[data-utc],[data-utc-title]';

    function due(n) { return n < 10 ? '0' + n : '' + n; }

    // Lingua della pagina; undefined (= quella del browser) se l'attributo manca.
    function lingua() { return document.documentElement.lang || undefined; }

    // Scarto dal meridiano quando il fuso non ha una sigla presentabile: 'UTC+2', 'UTC-5:30'.
    function scarto(d) {
        var m = -d.getTimezoneOffset();
        var seg = m < 0 ? '-' : '+';
        m = Math.abs(m);
        var h = Math.floor(m / 60), r = m % 60;
        return 'UTC' + seg + h + (r ? ':' + due(r) : '');
    }

    // Sigla del fuso nella lingua del lettore ('CEST', 'JST'…). Intl la da' solo per alcune coppie
    // lingua/fuso: altrove torna 'GMT+2' o un nome lungo, e in quel caso lo scarto e' piu' corto e chiaro.
    function sigla(d) {
        try {
            var parti = new Intl.DateTimeFormat(lingua(), { timeZoneName: 'short' }).formatToParts(d);
            for (var i = 0; i < parti.length; i++) {
                if (parti[i].type === 'timeZoneName') {
                    return /^[A-Za-z]{2,5}$/.test(parti[i].value) ? parti[i].value : scarto(d);
                }
            }
        } catch (e) { /* Intl assente o fuso non risolvibile: si ripiega sullo scarto */ }
        return scarto(d);
    }

    // fmt: 'hm' (default) | 'hms' | 'dhm'. In 'dhm' la data si ripete solo se in locale e' un ALTRO giorno:
    // ripeterla sempre raddoppierebbe la riga per dire la stessa cosa.
    function suffisso(iso, fmt) {
        var d = new Date(iso);
        if (isNaN(d.getTime())) return '';
        if (d.getTimezoneOffset() === 0) return '';   // il lettore e' gia' su UTC: non c'e' niente da aggiungere

        var ora = due(d.getHours()) + ':' + due(d.getMinutes());
        if (fmt === 'hms') ora += ':' + due(d.getSeconds());

        if (fmt === 'dhm' && d.getUTCDate() !== d.getDate()) {
            var data;
            // La lingua e' quella della PAGINA (`<html lang>`, che l'app scrive da UseRequestLocalization),
            // non quella del browser: misurato in verifica live, su pagina italiana usciva «23 Aug 2026»
            // accanto a «22 ago 2026». Il fuso resta quello del lettore — sono due cose diverse.
            try { data = d.toLocaleDateString(lingua(), { day: '2-digit', month: 'short', year: 'numeric' }); }
            catch (e) { data = d.toDateString(); }
            ora = data + ' · ' + ora;
        }
        return ' · ' + ora + ' ' + sigla(d);
    }

    // L'ora UTC COSI' COME l'ha scritta il server ('14:32Z'): serve a ritrovarla dentro il tooltip.
    function tokenUtc(d, fmt) {
        var t = due(d.getUTCHours()) + ':' + due(d.getUTCMinutes());
        if (fmt === 'hms') t += ':' + due(d.getUTCSeconds());
        return t + 'Z';
    }

    // Nel tooltip l'ora sta spesso IN MEZZO alla frase («Bloccata da X · lock fino alle 14:32Z. Non puoi
    // salvare…»): accodare in fondo metterebbe l'ora locale dopo la frase sbagliata. Si cerca il token UTC
    // e le si scrive subito accanto; se non c'e' (frase riscritta, lingua che formatta altrimenti) si accoda.
    function titoloConLocale(t, st, tok) {
        var i = t.indexOf(tok);
        if (i < 0) return t.slice(-st.length) === st ? t : t + st;
        var dopo = i + tok.length;
        return t.substr(dopo, st.length) === st ? t : t.slice(0, dopo) + st + t.slice(dopo);
    }

    function applica(el) {
        var fmt = el.getAttribute('data-utc-fmt');

        var iso = el.getAttribute('data-utc');
        if (iso) {
            var s = suffisso(iso, fmt);
            // Il confronto evita di risvegliare l'observer per riscrivere lo stesso valore.
            if (el.getAttribute('data-loc') !== s) {
                if (s) el.setAttribute('data-loc', s); else el.removeAttribute('data-loc');
            }
        }

        var isoT = el.getAttribute('data-utc-title');
        if (isoT) {
            var d = new Date(isoT);
            var st = suffisso(isoT, fmt);
            var t = el.getAttribute('title') || '';
            if (st && t) {
                var nuovo = titoloConLocale(t, st, tokenUtc(d, fmt));
                if (nuovo !== t) el.setAttribute('title', nuovo);
            }
        }
    }

    window.vipiApplyOreLocali = function (radice) {
        (radice || document).querySelectorAll(ATTR).forEach(applica);
    };

    var atteso = false;
    function programma() {
        if (atteso) return;
        atteso = true;
        requestAnimationFrame(function () { atteso = false; window.vipiApplyOreLocali(); });
    }

    function avvia() {
        window.vipiApplyOreLocali();
        // `title` sta nel filtro perche' Blazor lo riscrive dal proprio modello a ogni render, cancellando
        // il suffisso: senza, il tooltip tornerebbe in UTC secco dopo il primo battito dell'heartbeat.
        //
        // `data-loc` sta nel filtro perche' e' il nostro output, e va rimesso se sparisce: misurato in
        // verifica live, togliendolo NON tornava — l'observer non lo ascoltava, e l'ora locale restava via
        // fino alla mutazione successiva. Non innesca un ciclo: riscriverlo uguale non passa dal `set`.
        new MutationObserver(programma).observe(document.documentElement, {
            childList: true, subtree: true,
            attributes: true, attributeFilter: ['data-utc', 'data-utc-title', 'data-loc', 'title'],
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', avvia);
    else avvia();
})();
