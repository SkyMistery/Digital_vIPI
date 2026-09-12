/* ═══════════════════════════════════════════════════════════════════════════
   vAWOS — il quadro vivo. Carta: docs/feature/2026-09-12-vawos-e-minimi-lvp.md

   Fa TRE cose, e nient'altro:
     1. rilegge /services/vawos/api/{icao} ogni 60 s (un METAR cambia ogni 30 minuti;
        il prototipo interrogava un servizio pubblico ogni 10 secondi, cioe' 180
        chiamate per ogni bollettino nuovo, per ogni scheda aperta);
     2. interpola fra un bollettino e l'altro — SOLO dentro i valori che il METAR
        dichiara (§4.3 della carta): la direzione spazza il settore 200V280 perche'
        il bollettino lo dice, la velocita' oscilla fra vento e raffica perche' li
        da' entrambi. Vento fisso 18010KT ⇒ il numero sta fermo. Nessun seno che
        fabbrica una raffica che non c'e';
     3. tiene orologio, LED di vitalita' e l'ETA' del dato — che ingiallisce e poi
        arrossisce se il server smette di rispondere, invece di lasciare a schermo
        numeri vecchi che sembrano nuovi.

   ⚠️ UN SOLO timer per compito, e tutti fermati alla chiusura. Nel prototipo `tick`
   era pianificato due volte a 1 s e `tickVitality` due volte, a 450 ms e a 1000 ms:
   il LED lampeggiava a due ritmi contemporaneamente.
   ═══════════════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';

  var radice = null;           // la radice AGGANCIATA adesso: puo' cambiare a ogni navigazione
  var icaoAgganciato = null;   // e con lei l'aeroporto, che decide chi si interroga
  var vista = null;            // l'ultimo AwosView letto dal server
  var parole = { wx: [], nubi: [] };   // le due righe gia' scritte a parole dal server
  var scritte = null;                  // e tutte le altre scritte fisse, composte dal server
  // blocco -> 'L' | 'R' | undefined: la testata scelta A MANO, se qualcuno l'ha scelta.
  // ⚠️ Non piu' un booleano «invertita»: su una striscia dove nessuna testata risulta in uso (vento calmo)
  // invertire non faceva NIENTE — si cliccava e non cambiava nulla. Ora il clic SCEGLIE, e il giro e'
  // derivata → sinistra → destra → derivata.
  var manuale = {};
  var movimento = true;
  var timers = [];
  var ultimoDato = 0;          // Date.now() dell'ultima lettura riuscita
  // 🔴 La MEMORIA delle LVP, e l'unica cosa che questo modulo ricorda fra una lettura e l'altra: le soglie
  // di cancellazione sono piu' alte di quelle d'ingresso apposta (isteresi), e senza sapere «un minuto fa
  // erano in vigore?» quelle soglie sarebbero decorazione. La DECISIONE resta al server: qui si ricorda un
  // booleano e glielo si rimanda.
  var lvpInVigore = false;

  function $(sel, dove) { return (dove || radice).querySelector(sel); }
  function $$(sel, dove) { return Array.prototype.slice.call((dove || radice).querySelectorAll(sel)); }
  function testo(el, v) { if (el && el.textContent !== v) el.textContent = v; }
  function pad(v, n) { var s = String(Math.abs(Math.round(v))); while (s.length < n) s = '0' + s; return s; }

  // ── Aggancio, riaggancio, sgancio ────────────────────────────────────────
  //
  // 🔴 «Un caricamento = un pannello» E' FALSO, e crederci e' costato due difetti veri (revisione del
  // 12 settembre 2026, sera). La navigazione «enhanced» di Blazor rimpiazza il DOM senza ricaricare la
  // pagina e senza rieseguire gli script:
  //
  //   · arrivando dall'elenco (`/services/vawos` → `/services/vawos/libc`) il modulo era gia' partito
  //     SENZA aeroporto, non ripartiva, e il quadro restava fermo per sempre — con l'orologio che
  //     scorreva, cioe' con l'aria di essere vivo. Misurato: zero chiamate all'API dopo il clic;
  //   · uscendo dalla pagina `pagehide` NON scatta, quindi i timer restavano armati: misurata una
  //     chiamata a `/services/vawos/api/LIRN` due minuti dopo essere andati su `/services`.
  //
  // La cura e' una sola per tutt'e due: questa funzione si chiama a ogni navigazione (la chiama
  // `vipi-boot.js`, come per mappe e minime) e guarda che cosa c'e' ADESSO in pagina.
  function sincronizza() {
    var nuova = document.querySelector('.awos');

    // Il quadro non c'e' piu': si smonta tutto. E' l'unico posto che ferma i timer.
    if (!nuova) { ferma(); radice = null; icaoAgganciato = null; vista = null; return; }

    var icao = nuova.getAttribute('data-awos-icao') || null;
    if (nuova === radice && icao === icaoAgganciato) return;   // stessa pagina, niente da rifare

    ferma();
    radice = nuova;
    icaoAgganciato = icao;
    // Lo stato e' della PAGINA che si sta lasciando: portarselo dietro vorrebbe dire mostrare su Napoli la
    // freccia girata a mano su Bari.
    vista = null; parole = { wx: [], nubi: [] }; scritte = null; manuale = {}; lvpInVigore = false;

    collegaTasti();
    tema(leggiTema());

    if (icao) {
      ultimoDato = Date.now();                 // il primo dato e' gia' nell'HTML
      leggi(icao);                             // ...ma serve anche l'oggetto, o non si puo' animare niente
      ogni(60000, function () { leggi(icao); });
    }
    // Con `?test=` in coda all'indirizzo il pannello del bollettino finto si apre da se': ci si e' appena
    // arrivati premendo APPLY, e trovarlo chiuso costringe a riaprirlo per leggere che cosa si e' scritto.
    if (new URLSearchParams(location.search).get('test')) apri('prova');
    ogni(1000, orologio);
    ogni(1000, eta);
    ogni(900, vitalita);
    ogni(250, anima);
    orologio();
  }

  function ogni(ms, fn) { timers.push(setInterval(fn, ms)); }
  function ferma() { timers.forEach(clearInterval); timers = []; }

  function leggiTema() {
    try { return localStorage.getItem('vawos-tema') === 'day' ? 'day' : 'night'; }
    catch (e) { return 'night'; }              // finestra privata: si resta di notte
  }

  // ── Lettura dal server ───────────────────────────────────────────────────
  function leggi(icao) {
    var url = '/services/vawos/api/' + encodeURIComponent(icao);
    var q = [];
    var prova = new URLSearchParams(location.search).get('test');
    if (prova) q.push('test=' + encodeURIComponent(prova));
    if (lvpInVigore) q.push('inforce=true');
    if (q.length) url += '?' + q.join('&');

    fetch(url, { headers: { 'Accept': 'application/json' }, cache: 'no-store' })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (d) {
        if (!d || !d.vista) return;            // niente da fare: il quadro invecchia e lo dice
        vista = d.vista;
        parole = { wx: d.wx || [], nubi: d.nubi || [] };
        scritte = d.scritte || null;
        var st = vista.lvp && vista.lvp.valutazione && vista.lvp.valutazione.stato;
        lvpInVigore = st === 'InVigore' || st === 'Cancellabile';
        ultimoDato = Date.now();
        disegna();
      })
      .catch(function () { /* il quadro invecchia: non serve altro rumore */ });
  }

  // ── Disegno dei valori che NON si muovono fra una lettura e l'altra ──────
  function disegna() {
    if (!vista) return;
    var m = vista.metar;

    testo($('[data-awos="qnh"]'), m && m.qnhHpa != null ? String(m.qnhHpa) : '----');
    testo($('[data-awos="temp"]'), m && m.tempC != null ? String(m.tempC) : '--');
    testo($('[data-awos="dew"]'), m && m.dewpointC != null ? String(m.dewpointC) : '--');
    testo($('[data-awos="tl"]'), vista.transitionLevel || '—');
    testo($('[data-awos="vis"]'), (m && m.visibility) || '----');
    testo($('[data-awos="trend"]'), (m && m.trend) || '');
    testo($('[data-awos="attiva"]'), rigaAttiva());
    lvp();

    var rep = $('[data-awos-pan="report"] div');
    if (rep) testo(rep, vista.metarRaw || 'No METAR available');

    var a = vista.atis;
    testo($('[data-awos="atis-chi"]'), a ? a.callsign : '— ATIS —');
    testo($('[data-awos="atis"]'), (a && a.lettera) || '—');
    testo($('[data-awos="atis-ts"]'), (a && a.orario) || '--:--');
    testo($('[data-awos="atis-text"]'), (a && a.testo) || 'No ATIS on frequency.');

    // ⚠️ Le parole arrivano dal SERVER (AwosTesto), non si compongono qui: la lingua sta nella richiesta,
    // e il JavaScript non ce l'ha. Scrivendo i codici grezzi il quadro cambiava lingua al primo giro.
    righe('[data-awos="wx"]', parole.wx);
    righe('[data-awos="cloud"]', parole.nubi);

    (vista.piste || []).forEach(function (striscia, i) { striscia_(i, striscia); });
  }

  // La pastiglia LVP e la fascia RVR che si accende. Il TESTO e il titolo li compone il server? No: qui
  // sono sigle (LVP, PREP, N/A), e le sigle non hanno una lingua. Il «(standard)» invece e' un fatto che il
  // server dichiara, e si ripete tale e quale.
  // La pastiglia LVP e la fascia RVR che si accende. Testo, classe e spiegazione arrivano dal server: erano
  // scritti due volte, e la coppia gemella (WX) era gia' divergita a schermo.
  function lvp() {
    var el = $('[data-awos="lvp"]');
    if (!el || !scritte) return;
    testo(el, scritte.lvpTesto);
    el.title = scritte.lvpTitolo;
    el.classList.toggle('on', scritte.lvpClasse === 'on');
    el.classList.toggle('prep', scritte.lvpClasse === 'prep');
    var acceso = scritte.lvpClasse === 'on' || scritte.lvpClasse === 'prep';
    $$('[data-awos-rvr]').forEach(function (r) { r.classList.toggle('lvp', acceso); });
  }

  // Le righe restano SEMPRE tre: il riquadro non deve cambiare altezza quando il tempo peggiora.
  function righe(sel, valori) {
    var box = $(sel);
    if (!box) return;
    var celle = $$('.awos-riga', box);
    for (var i = 0; i < celle.length; i++) testo(celle[i], valori[i] || '');
  }

  function striscia_(i, striscia) {
    var scelta = manuale[i];
    var attivaSx = scelta ? scelta === 'L' : eAttiva(striscia.left && striscia.left.ident);
    var attivaDx = scelta ? scelta === 'R' : (striscia.right && eAttiva(striscia.right.ident));

    var teste = $$('[data-awos-blocco="' + i + '"] .awos-testata');
    if (teste[0]) teste[0].classList.toggle('attiva', !!attivaSx);
    if (teste[1]) teste[1].classList.toggle('attiva', !!attivaDx);

    var freccia = $('[data-awos-arrow="' + i + '"]');
    if (freccia) {
      freccia.style.transform = attivaDx ? 'scaleX(1)' : 'scaleX(-1)';
      var poly = freccia.querySelector('polygon');
      if (poly) poly.setAttribute('fill', (attivaSx || attivaDx) ? (scelta ? '#a08000' : '#2a8a3a') : '#8a8a8a');
    }

    rvr(i);
  }

  // ⚠️ `dep` e `arr` sono ELENCHI, non stringhe: un ATIS che dice «arrival runway 16L 16R» ne dichiara due.
  // (Erano stringhe fino alla revisione del 12 settembre 2026, e trattarle ancora come tali qui alzava
  // un'eccezione a ogni giro d'animazione.)
  function eAttiva(ident) {
    if (!ident || !vista || !vista.attiva) return false;
    return contiene(vista.attiva.dep, ident) || contiene(vista.attiva.arr, ident);
  }
  function contiene(elenco, ident) {
    if (!elenco || !elenco.length) return false;
    for (var i = 0; i < elenco.length; i++) if (eq(elenco[i], ident)) return true;
    return false;
  }
  function eq(a, b) { return !!a && !!b && String(a).toUpperCase() === String(b).toUpperCase(); }

  // Le tre celle RVR: le scrive il server (AwosTesto.Rvr), qui si copiano. 🔴 Un RVR che il bollettino non
  // da' resta `///`, mai «P2000»: la regola sta in un posto solo.
  function rvr(i) {
    var t = scritte && scritte.rvr && scritte.rvr[i];
    if (!t) return;
    testo($('[data-awos-rvrv="' + i + '-tdz"]'), t.tdz);
    testo($('[data-awos-rvrv="' + i + '-mid"]'), t.mid);
    testo($('[data-awos-rvrv="' + i + '-end"]'), t.end);
  }

  function rigaAttiva() {
    var base_ = (scritte && scritte.rigaAttiva) || '';
    var scelte = Object.keys(manuale).filter(function (k) { return manuale[k]; });
    return scelte.length ? base_ + ' · manual' : base_;
  }

  // ── Il movimento: interpolazione DENTRO i valori dichiarati ──────────────
  // 🔴 Finché il server non ha risposto ALMENO UNA VOLTA questa funzione non tocca niente. Il primo
  // bollettino sta gia' nell'HTML, scritto da chi ha reso la pagina: scriverci sopra `--` in attesa della
  // prima lettura — a 60 secondi di distanza — vuol dire cancellare i valori veri e mostrare un quadro
  // spento per un minuto. Visto a schermo il 12 settembre 2026, tutti i pannelli vento a «--».
  function anima() {
    if (!vista) return;
    var m = vista.metar;
    var w = m && m.wind;
    $$('[data-awos-wind]').forEach(function (pan) {
      var hdg = parseInt(pan.getAttribute('data-awos-hdg'), 10) || 0;
      scriviVento(pan, w, hdg);
    });
  }

  function scriviVento(pan, w, hdg) {
    function cella(nome) { return pan.querySelector('[data-awos-w="' + nome + '"]'); }
    if (!w) {
      ['dir', 'spd', 'vmin', 'vmax', 'gust', 'cross', 'tail'].forEach(function (n) { testo(cella(n), '--'); });
      return;
    }

    var t = Date.now() / 1000;
    var dir = w.directionDeg;
    var spd = w.speedKt;

    if (movimento) {
      // Il settore di variabilita': si spazza da un estremo all'altro, in 12 s.
      if (w.varFromDeg != null && w.varToDeg != null) {
        var ampiezza = ((w.varToDeg - w.varFromDeg) + 360) % 360;
        var f = (Math.sin(t / 12 * 2 * Math.PI) + 1) / 2;
        dir = Math.round((w.varFromDeg + ampiezza * f + 360) % 360);
      }
      // La raffica: si oscilla fra vento medio e raffica, e mai oltre nessuno dei due.
      if (w.gustKt != null && w.gustKt > w.speedKt) {
        var g = (Math.sin(t / 7 * 2 * Math.PI) + 1) / 2;
        spd = Math.round(w.speedKt + (w.gustKt - w.speedKt) * g);
      }
    }

    // «CALM» e non «360»: 00000KT vuol dire vento calmo, non «da nord a zero nodi».
    testo(cella('dir'), w.calm ? 'CALM' : w.variable ? 'VRB' : (dir == null ? '---' : pad(((dir + 359) % 360) + 1, 3)));
    testo(cella('spd'), pad(spd, 2));
    testo(cella('vmin'), w.varFromDeg != null ? pad(w.varFromDeg, 3) : '--');
    testo(cella('vmax'), w.varToDeg != null ? pad(w.varToDeg, 3) : '--');
    testo(cella('gust'), w.gustKt != null ? pad(w.gustKt, 2) : '--');

    if (dir == null || w.calm) { testo(cella('cross'), '--'); testo(cella('tail'), '--'); return; }
    var d = (dir - hdg) * Math.PI / 180;
    var testa = Math.round(spd * Math.cos(d));
    var cross = Math.round(Math.abs(spd * Math.sin(d)));
    var coda = testa < 0 ? -testa : 0;
    scala(cella('cross'), cross, 8, 15);
    scala(cella('tail'), coda, 1, 1);
  }

  function scala(el, v, ambra, rosso) {
    if (!el) return;
    testo(el, pad(v, 2));
    el.classList.toggle('ambra', v >= ambra && v < rosso);
    el.classList.toggle('rosso', v >= rosso);
  }

  // ── Orologio, eta' del dato, LED ─────────────────────────────────────────
  function orologio() {
    var n = new Date();
    testo($('[data-awos-clock]'), pad(n.getUTCHours(), 2) + ':' + pad(n.getUTCMinutes(), 2) + ':' + pad(n.getUTCSeconds(), 2) + 'Z');
  }

  // Il quadro INVECCHIA a vista. E' l'unica difesa contro la peggiore delle bugie:
  // numeri vecchi che sembrano di adesso perche' la pagina e' ancora aperta.
  function eta() {
    var el = $('[data-awos-eta]');
    if (!el || !ultimoDato) return;
    var s = Math.round((Date.now() - ultimoDato) / 1000);
    testo(el, s < 90 ? s + 's' : Math.round(s / 60) + 'm');
    el.classList.toggle('vecchio', s >= 150 && s < 600);
    el.classList.toggle('morto', s >= 600);
  }

  var segmento = 0;
  function vitalita() {
    segmento = (segmento + 1) % 8;
    $$('[data-vita]').forEach(function (c) {
      c.setAttribute('fill', parseInt(c.getAttribute('data-vita'), 10) === segmento ? '#ffffff' : '#333');
    });
  }

  // ── Tasti ────────────────────────────────────────────────────────────────
  function collegaTasti() {
    // Una volta sola per nodo: `sincronizza` puo' richiamarci sullo stesso `.awos` (cambia solo l'ICAO),
    // e due ascoltatori vorrebbero dire due inversioni della freccia per un clic solo.
    if (radice.dataset.awosLegato === '1') return;
    radice.dataset.awosLegato = '1';
    radice.addEventListener('click', function (e) {
      var t = e.target.closest('[data-awos-tema],[data-awos-mov],[data-awos-mask],[data-awos-chiudi],[data-awos-inverti]');
      if (!t) return;

      if (t.hasAttribute('data-awos-tema')) {
        tema(radice.getAttribute('data-awos-theme') === 'day' ? 'night' : 'day');
      } else if (t.hasAttribute('data-awos-mov')) {
        movimento = !movimento;
        t.setAttribute('aria-pressed', movimento ? 'true' : 'false');
        anima();
      } else if (t.hasAttribute('data-awos-mask')) {
        apri(t.getAttribute('data-awos-mask'));
      } else if (t.hasAttribute('data-awos-chiudi')) {
        var p = $('[data-awos-pan="' + t.getAttribute('data-awos-chiudi') + '"]');
        if (p) p.hidden = true;
      } else {
        // derivata → sinistra → destra → derivata. Tre stati e non due: su una striscia senza pista in uso
        // un'inversione non aveva niente da invertire, e il tasto non faceva niente.
        var i = t.getAttribute('data-awos-inverti');
        manuale[i] = manuale[i] === 'L' ? 'R' : manuale[i] === 'R' ? undefined : 'L';
        if (vista) { striscia_(parseInt(i, 10), vista.piste[i]); testo($('[data-awos="attiva"]'), rigaAttiva()); }
      }
    });
  }

  function apri(nome) {
    var pan = $('[data-awos-pan="' + nome + '"]');
    if (!pan) return;
    var era = pan.hidden;
    $$('[data-awos-pan]').forEach(function (p) { p.hidden = true; });
    pan.hidden = !era;
  }

  function tema(quale) {
    radice.setAttribute('data-awos-theme', quale);
    var b = $('[data-awos-tema]');
    if (b) b.textContent = quale === 'day' ? 'NIGHT' : 'DAY';
    try { localStorage.setItem('vawos-tema', quale); } catch (e) { /* finestra privata: pazienza */ }
  }

  // Il punto d'ingresso, e il nome che `vipi-boot.js` cerca per riagganciare il modulo dopo ogni
  // navigazione. ⚠️ È anche ciò che lo SMONTA quando si va altrove: la lista dei riagganci gira su ogni
  // pagina, non solo su questa.
  window.vipiInitAwos = sincronizza;

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', sincronizza);
  else sincronizza();
})();
