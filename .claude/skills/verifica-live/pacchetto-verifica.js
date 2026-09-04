// Verifica del PACCHETTO (non del sorgente): nel publish il JavaScript e' minificato, e da 1.1.0 uno di
// quei file e' l'unico che avvia Blazor. Vedi docs/guide/preparare-un-pacchetto.md §6.
//
// Due modi, tutti e due usati per 1.2.0 il 31 agosto 2026:
//   node pacchetto-verifica.js
//       sul publish win-x64 avviato dalla sua cartella su :5199 (dieci controlli, editor compreso)
//   BASE=https://atc.it.ivao.aero SOLO_PUBBLICO=1 node pacchetto-verifica.js
//       su PRODUZIONE, da anonimo: salta l'editor, che da fuori non si raggiunge (otto controlli)
//
// ⚠️ Il controllo che conta e' LA RICERCA: passa dal server, quindi distingue un sito vivo da uno
// mezzo caricato. Il selettore della lingua, lo zoom e il tema NON valgono — funzionano anche a sito morto.
// ⚠️ Il campo della Ricerca non dichiara un `type`: si prende `.wrap input`, non `input[type=search]`.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const esiti = [];
  const nota = (nome, ok, dettaglio) => {
    esiti.push({ nome, ok, dettaglio });
    console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`);
  };

  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const erroriConsole = [];
  page.on('console', (m) => { if (m.type() === 'error') erroriConsole.push(m.text()); });
  page.on('pageerror', (e) => erroriConsole.push('pageerror: ' + e.message));

  try {
    // 1. Il file che avvia Blazor arriva, ed e' minificato.
    const r = await page.goto(BASE + '/_content/Vipi.Ui/vipi-riconnessione.js', { waitUntil: 'domcontentloaded' });
    const js = await page.evaluate(() => document.body.innerText);
    nota('vipi-riconnessione.js servito', r.status() === 200 && js.length > 100,
      `HTTP ${r.status()}, ${js.length} caratteri, comincia con: ${js.slice(0, 40).replace(/\n/g, ' ')}`);
    // Minificato = quasi niente a capo rispetto alla lunghezza.
    const righe = js.split('\n').length;
    nota('e minificato', righe < 20, `${righe} righe per ${js.length} caratteri`);

    // 2. La home si apre e il circuito Blazor parte davvero (non il solo prerender).
    await page.goto(BASE + '/services/vsop', { waitUntil: 'networkidle2' });
    let blazor = false;
    for (let i = 0; i < 60 && !blazor; i++) {
      blazor = await page.evaluate(() => !!window.Blazor);
      if (!blazor) await sleep(1000);
    }
    nota('circuito Blazor avviato', blazor, blazor ? 'window.Blazor presente' : 'window.Blazor MAI comparso in 60 s');

    const schede = await page.evaluate(() => document.querySelectorAll('.acc-card').length);
    nota('la home mostra le schede ACC', schede > 0, `${schede} schede`);
    const avvisoCatalogo = await page.evaluate(() => !!document.querySelector('.callout.warning'));
    nota('nessun avviso «catalogo non disponibile»', !avvisoCatalogo, avvisoCatalogo ? 'AVVISO PRESENTE' : 'assente');

    // 3. IL controllo che distingue un sito vivo da uno mezzo caricato: la Ricerca passa dal SERVER.
    //
    // ⚠️ SI RIPROVA UNA VOLTA, e non e' indulgenza. Il 31 agosto 2026, lanciato SUBITO dopo il caricamento
    // di 1.3.0, questo controllo e' uscito ROSSO — «sito mezzo caricato» — su un sito perfettamente sano:
    // il processo era appena partito (l'avvio dura ~6 s e apre il database), e la prima interazione col
    // circuito e' arrivata oltre la finestra d'attesa. Al secondo giro, e misurando a mano, la Ricerca
    // rispondeva in 746 ms con 50 risultati.
    //
    // Un falso rosso QUI e' la cosa piu' cara che questa sonda possa fare: e' il controllo su cui si decide
    // se tornare indietro, e tornare indietro da una consegna sana e' peggio che non averla verificata. Il
    // secondo tentativo ricarica la pagina — cioe' riapre il circuito — invece di aspettare piu' a lungo
    // sullo stesso, perche' il caso da coprire e' «il primo circuito e' nato mentre l'app si scaldava».
    // ⚠️ 🔴 E `window.Blazor` NON vuol dire «il circuito e' aperto»: quell'oggetto esiste appena lo script
    // e' stato interpretato, mentre il WebSocket puo' ancora essere in viaggio. Scrivere in quella finestra
    // butta via i tasti — nessuno li raccoglie — e la sonda vede una pagina che non risponde.
    // Misurato su PRODUZIONE il 4 settembre 2026, dopo il caricamento di 1.8.0: senza attesa 3 giri su 3
    // ROSSI su un sito sano; con 2000 ms di attesa, «50 results for LI» tutte le volte. Era un falso rosso
    // sul controllo che decide se tornare indietro da una consegna — cioe' il modo piu' caro di sbagliare
    // che questa sonda abbia. Ora si aspetta il CIRCUITO (il WebSocket di `/_blazor`), che e' quel che la
    // sezione 4 del runbook dice da sempre e che proprio qui non si faceva.
    //
    // ⚠️ E si confronta il testo INTERO, non i primi 400 caratteri: quel taglio reggeva per caso, perche'
    // «50 results» cade al carattere 88. Basta una riga in piu' nel chrome e il controllo diventa cieco.
    let cambiata = false, quantiGiri = 0;
    for (let giro = 1; giro <= 2 && !cambiata; giro++) {
      quantiGiri = giro;

      // Il circuito si aspetta col CDP: `webSocketFrameReceived` e' il primo segno che il server risponde
      // davvero su quella connessione, non solo che il browser l'ha aperta.
      const cdp = await page.createCDPSession();
      await cdp.send('Network.enable');
      let circuito = false;
      cdp.on('Network.webSocketFrameReceived', (e) => { if (String(e.response?.payloadData ?? '').length) circuito = true; });

      await page.goto(BASE + '/services/vsop/search', { waitUntil: 'networkidle2' });
      for (let i = 0; i < 60; i++) { if (await page.evaluate(() => !!window.Blazor)) break; await sleep(1000); }
      for (let i = 0; i < 40 && !circuito; i++) await sleep(500);   // fino a 20 s per il primo fotogramma
      await sleep(500);                                             // e un respiro dopo, per il primo render
      // L'input della Ricerca non dichiara un type: si prende il primo input della pagina.
      await page.waitForSelector('.wrap input', { timeout: 30000 });
      const campo = await page.$('.wrap input');
      const prima = await page.evaluate(() => document.body.innerText.replace(/\s+/g, ' '));
      await campo.type('LI', { delay: 120 });
      for (let i = 0; i < 30 && !cambiata; i++) {
        await sleep(500);
        const dopo = await page.evaluate(() => document.body.innerText.replace(/\s+/g, ' '));
        cambiata = dopo !== prima;
      }
      await cdp.detach().catch(() => {});
    }
    nota('la RICERCA risponde (passa dal server)', cambiata,
      cambiata
        ? (quantiGiri === 1 ? 'la riga sotto il campo e cambiata' : 'la riga e cambiata al SECONDO giro (processo appena avviato)')
        : 'NESSUN cambiamento in due giri: sito mezzo caricato');

    if (!process.env.SOLO_PUBBLICO) {
    // 4. Una pagina di editor: e' li' che vivevano le corse di §AM.
    await page.goto(BASE + '/services/vsop/libb/editor', { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60; i++) { if (await page.evaluate(() => !!window.Blazor)) break; await sleep(1000); }
    const editorVivo = await page.evaluate(() =>
      !document.body.innerText.includes('second operation') &&
      !document.body.innerText.includes('This page did not open') &&
      document.querySelectorAll('.wrap').length > 0);
    const pannelloTr = await page.evaluate(() => !!document.querySelector('#tr-review'));
    nota('editor ACC LIBB si apre', editorVivo, editorVivo ? 'nessuna pagina d\'errore' : 'PAGINA D\'ERRORE');
    nota('pannello traduzioni presente', pannelloTr, pannelloTr ? '#tr-review nel DOM' : '#tr-review assente');
    }

    // 5. Il tema (che l'asset cambiato tocca) e' arrivato: il CSS deve avere effetto.
    const sfondo = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    nota('il foglio di stile e in vigore', sfondo && sfondo !== 'rgba(0, 0, 0, 0)', `body background = ${sfondo}`);

    nota('console del browser pulita', erroriConsole.length === 0,
      erroriConsole.length ? erroriConsole.slice(0, 3).join(' | ') : 'nessun errore');
  } catch (e) {
    nota('ECCEZIONE NEL DRIVER', false, e.message);
  } finally {
    await browser.close();
  }

  const rossi = esiti.filter((e) => !e.ok);
  console.log('\n===== ' + (rossi.length ? `${rossi.length} ROSSI` : 'TUTTO VERDE') + ' =====');
  process.exit(rossi.length ? 1 : 0);
})();
