/* ═══════════════════════════════════════════════════════════════════════════
   vAWOS — il quadro vivo. Carta: docs/feature/2026-09-12-vawos-e-minimi-lvp.md

   Fa TRE cose, e nient'altro:
     1. rilegge /services/vawos/api/{icao} ogni 60 s (un METAR cambia ogni 30 minuti;
        il prototipo interrogava un servizio pubblico ogni 10 secondi, cioe' 180
        chiamate per ogni bollettino nuovo, per ogni scheda aperta);
     2. scrive quel che il bollettino dice. Il vento, dal 15 settembre 2026, con una
        PICCOLA variazione attorno al METAR una volta ogni 45-200 s per pannello
        (decisione del committente: vedi `vento()` per i limiti e il perche' non e'
        l'animazione tolta il 12);
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
      // ⚠️ NON si interroga il server a scheda NASCOSTA (audit del 12 settembre 2026, voce Q6). Una scheda
      // vAWOS dimenticata in fondo al browser chiamava l'API una volta al minuto per sempre, e ogni giro
      // costa una lettura dell'elenco documenti piu' il profilo dello scalo su un processo solo.
      // Non e' un risparmio a scapito di niente: quel che si vede mentre la scheda e' nascosta non lo vede
      // nessuno, e appena torna visibile si legge SUBITO — quindi chi ci ritorna trova il dato fresco, non
      // il dato di quando se n'e' andato. L'orologio e l'eta' continuano a girare: sono locali.
      ogni(60000, function () { if (!document.hidden) leggi(icao); });
    }
    // Con `?test=` in coda all'indirizzo il pannello del bollettino finto si apre da se': ci si e' appena
    // arrivati premendo APPLY, e trovarlo chiuso costringe a riaprirlo per leggere che cosa si e' scritto.
    if (new URLSearchParams(location.search).get('test')) apri('prova');
    $$('[data-awos-wind]').forEach(pianificaVariazione);
    ogni(1000, orologio);
    ogni(1000, eta);
    ogni(900, vitalita);
    orologio();
  }

  function ogni(ms, fn) { timers.push(setInterval(fn, ms)); }
  function ferma() {
    timers.forEach(clearInterval); timers = [];
    attese.forEach(clearTimeout); attese.clear();
  }
  // I timer delle variazioni del vento: uno per pannello, ognuno con un intervallo suo e ripianificato a ogni
  // scatto. Un insieme e non un elenco: la pagina resta aperta per ore, e un elenco crescerebbe di un id ogni
  // pochi minuti per sempre.
  var attese = new Set();

  // Appena la scheda torna visibile si legge SUBITO, perche' il giro al minuto qui sopra l'ha saltata
  // finche' era nascosta: chi ci ritorna deve trovare il dato di adesso, non quello di quando se n'e'
  // andato. Se nel frattempo ha cambiato scalo, `icaoAgganciato` e' gia' quello nuovo.
  //
  // ⚠️ UNA VOLTA SOLA, qui fuori, e NON dentro l'init: l'init rigira a ogni navigazione «enhanced» (si passa
  // da uno scalo all'altro con un clic) e un ascoltatore aggiunto li' dentro si accumulerebbe a ogni
  // passaggio — la stessa specie di difetto dei timer che non si spegnevano, trovata guidando l'app il
  // 12 settembre 2026. `ferma()` spegne i timer, non gli ascoltatori.
  document.addEventListener('visibilitychange', function () {
    if (!document.hidden && icaoAgganciato) leggi(icaoAgganciato);
  });

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

    // ⚠️ Niente `cache: 'no-store'` (c'era fino al 12 settembre 2026). Quel modo esiste per DISFARE le
    // cache, e qui la cache e' esattamente quel che si vuole: la risposta esce `public, max-age=60` e sotto
    // c'e' un METAR con dieci minuti di TTL, quindi una copia di un minuto e' piu' fresca del dato che
    // trasporta. `no-store` faceva anche mandare al bordo intestazioni di richiesta che gli dicono di non
    // servire la sua copia, cioe' annullava il risparmio proprio dove doveva prodursi.
    // ⚠️ E non si vede: l'eta' del dato la calcola il quadro da un timbro ASSOLUTO dentro il payload, non da
    // quando e' arrivata la risposta. Una copia vecchia di un minuto dichiara la propria eta' vera.
    fetch(url, { headers: { 'Accept': 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (d) {
        if (!d || !d.vista) return;            // niente da fare: il quadro invecchia e lo dice
        vista = d.vista;
        parole = { wx: d.wx || [], nubi: d.nubi || [] };
        scritte = d.scritte || null;
        // La memoria la decide il SERVER (T-009, 13 settembre 2026): qui si contava «Cancellabile» come in
        // vigore, e la proposta di chiudere le LVP restava accesa per sempre; un giro senza bollettino la azzerava.
        lvpInVigore = !!(vista.lvp && vista.lvp.memoria);
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
    // Con più piste la visibilità sta in OGNI blocco (prototipi a 2 e 3 piste): si scrivono tutte, o i
    // blocchi dopo il primo resterebbero fermi al primo disegno.
    $$('[data-awos="vis"]').forEach(function (el) { testo(el, (m && m.visibility) || '----'); });
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

    vento();
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

  // Le righe restano quante ne ha disegnate il server (tre, o quattro per le nubi dei blocchi pista): il
  // riquadro non deve cambiare altezza quando il tempo peggiora. E si scrivono in OGNI riquadro con quel nome.
  function righe(sel, valori) {
    $$(sel).forEach(function (box) {
      var celle = $$('.awos-riga', box);
      for (var i = 0; i < celle.length; i++) testo(celle[i], valori[i] || '');
    });
  }

  function striscia_(i, striscia) {
    var scelta = manuale[i];
    var attivaSx = scelta ? scelta === 'L' : eAttiva(striscia.left && striscia.left.ident);
    var attivaDx = scelta ? scelta === 'R' : (striscia.right && eAttiva(striscia.right.ident));

    // Le testate restano arancioni: la pista in uso la dice SOLO la freccia, nel senso di marcia (dalla
    // testata in uso verso l'altra). Tutt'e due in uso (arrivi da una, partenze dall'altra): seguono le
    // PARTENZE. ⚠️ Stessa regola di `PuntaADestra` in AwosPage.razor.
    var destraInPartenza = !!(striscia.right && vista.attiva && contiene(vista.attiva.dep, striscia.right.ident));
    var aDestra = attivaSx && attivaDx ? !destraInPartenza : (attivaSx || !attivaDx);
    var freccia = $('[data-awos-arrow="' + i + '"]');
    if (freccia) {
      freccia.style.transform = aDestra ? 'scaleX(1)' : 'scaleX(-1)';
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

  // Le celle RVR — UNA PER TESTATA — le scrive il server (AwosTesto.Rvr), qui si copiano: P2000 quando il
  // bollettino non ha nessun RVR, `///` quando ne ha per altre piste ma non per questa. La regola sta la'.
  function rvr(i) {
    var celle = scritte && scritte.rvr && scritte.rvr[i];
    if (!celle) return;
    for (var k = 0; k < celle.length; k++) testo($('[data-awos-rvrv="' + i + '-' + k + '"]'), celle[k].valore);
  }

  function rigaAttiva() {
    var base_ = (scritte && scritte.rigaAttiva) || '';
    var scelte = Object.keys(manuale).filter(function (k) { return manuale[k]; });
    return scelte.length ? base_ + ' · manual' : base_;
  }

  // ── Il vento: il bollettino, con una PICCOLA variazione ogni 45-200 s ────
  //
  // 🔴 Storia, perche' la decisione e' stata ribaltata due volte. Il 12 settembre 2026 si e' tolta
  // un'animazione (la direzione spazzava il settore `200V280` con un seno di 12 s, quattro volte al secondo)
  // su decisione del committente — «non abbiamo modo di sapere il vento reale istantaneo». Il 15 settembre il
  // committente ha chiesto il contrario, ma in una forma DIVERSA, ed e' la forma che conta:
  //
  //   · uno SCATTO ogni 45-200 s, a intervallo casuale e diverso per pannello — non un moto continuo, che
  //     a schermo si leggeva come un generatore casuale;
  //   · lo scarto si tira ogni volta ATTORNO AL METAR, non a partire dal valore precedente: non e' una
  //     passeggiata casuale, e dopo ore la direzione e' ancora a ±10° dal bollettino;
  //   · la direzione resta DENTRO il settore dichiarato (`dddVddd`) se c'e'; VRB e CALM non si toccano;
  //   · la velocita' non supera la RAFFICA dichiarata e non scende a zero; EXTREMES e GUST restano quelli
  //     del bollettino, perche' sono la sua parte che dichiara i limiti.
  //
  // ⚠️ Traverso e coda si calcolano sui valori A SCHERMO: tre caselle che non tornano fra loro (DIR 347 e
  // un traverso calcolato su 340) sarebbero un quadro che si contraddice. Con ±10° e ±3 kt al massimo il
  // traverso si sposta di pochi nodi — ma PUO' attraversare una soglia di colore, ed e' voluto: la soglia
  // e' vicina, e un sensore vero farebbe lo stesso.
  var VAR_DIR = 10;
  function varVelocita(kt) { return kt < 5 ? 1 : kt < 15 ? 2 : 3; }

  function vento() {
    if (!vista) return;
    var m = vista.metar;
    var w = m && m.wind;
    $$('[data-awos-wind]').forEach(function (pan) { scriviVento(pan, w, hdgDi(pan)); });
  }

  function hdgDi(pan) { return parseInt(pan.getAttribute('data-awos-hdg'), 10) || 0; }

  // Lo scarto del pannello, in FRAZIONI di [-1, 1]: si scala sui limiti al momento di scrivere, cosi' un
  // bollettino nuovo con piu' vento non eredita uno scarto misurato sul vento di prima.
  function scarto(pan) { return pan._awosScarto || (pan._awosScarto = { dir: 0, spd: 0 }); }

  function pianificaVariazione(pan) {
    var id = setTimeout(function () {
      attese.delete(id);
      if (!pan.isConnected) return;            // pagina cambiata: il pannello non c'e' piu'
      var s = scarto(pan);
      s.dir = Math.random() * 2 - 1;
      s.spd = Math.random() * 2 - 1;
      if (vista) scriviVento(pan, vista.metar && vista.metar.wind, hdgDi(pan));
      pianificaVariazione(pan);
    }, (45 + Math.random() * 155) * 1000);
    attese.add(id);
  }

  // La direzione dentro il settore `da → a` in senso orario (anche a cavallo del nord: 340V030).
  function nelSettore(d, da, a) {
    if (da == null || a == null) return d;
    var ampiezza = (a - da + 360) % 360, dentro = (d - da + 360) % 360;
    if (dentro <= ampiezza) return d;
    // fuori: al bordo piu' vicino
    return dentro - ampiezza < 360 - dentro ? a : da;
  }

  function scriviVento(pan, w, hdg) {
    function cella(nome) { return pan.querySelector('[data-awos-w="' + nome + '"]'); }
    if (!w) {
      ['dir', 'spd', 'vmin', 'vmax', 'gust', 'cross', 'tail'].forEach(function (n) { testo(cella(n), '--'); });
      return;
    }

    var s = scarto(pan);
    var dir = w.directionDeg;
    if (dir != null && !w.calm && !w.variable)
      dir = nelSettore((dir + Math.round(s.dir * VAR_DIR) + 360) % 360, w.varFromDeg, w.varToDeg);
    var kt = w.speedKt;
    if (!w.calm && kt > 0) {
      kt = Math.max(1, kt + Math.round(s.spd * varVelocita(w.speedKt)));
      if (w.gustKt != null) kt = Math.min(kt, w.gustKt);
    }

    // «CALM» e non «360»: 00000KT vuol dire vento calmo, non «da nord a zero nodi».
    testo(cella('dir'), w.calm ? 'CALM' : w.variable ? 'VRB' : (dir == null ? '---' : pad(((dir + 359) % 360) + 1, 3)));
    testo(cella('spd'), pad(kt, 2));
    testo(cella('vmin'), w.varFromDeg != null ? pad(w.varFromDeg, 3) : '--');
    testo(cella('vmax'), w.varToDeg != null ? pad(w.varToDeg, 3) : '--');
    testo(cella('gust'), w.gustKt != null ? pad(w.gustKt, 2) : '--');

    if (dir == null || w.calm || w.variable) { testo(cella('cross'), '--'); testo(cella('tail'), '--'); return; }
    var d = (dir - hdg) * Math.PI / 180;
    var testa = Math.round(kt * Math.cos(d));
    var cross = Math.round(Math.abs(kt * Math.sin(d)));
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
    // 🔴 T-016 (revisione del 13 settembre 2026, visto a schermo): il segno stava in `data-awos-legato`, e la
    // navigazione enhanced dall'elenco a uno scalo lo CANCELLA tenendo lo stesso nodo. Il secondo ascoltatore
    // si aggiungeva proprio sul percorso che questo commento diceva coperto: DAY/NIGHT tornava com'era, i
    // pannelli si aprivano e si richiudevano. Il segno sta in una proprietà JS, che il DomSync non tocca.
    if (radice._awosLegato) return;
    radice._awosLegato = true;
    radice.addEventListener('click', function (e) {
      var t = e.target.closest('[data-awos-tema],[data-awos-mask],[data-awos-chiudi],[data-awos-inverti]');
      if (!t) return;

      if (t.hasAttribute('data-awos-tema')) {
        tema(radice.getAttribute('data-awos-theme') === 'day' ? 'night' : 'day');
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
