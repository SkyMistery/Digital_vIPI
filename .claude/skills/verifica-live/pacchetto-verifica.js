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
// ⚠️ E la Ricerca deve TROVARE, non solo rispondere (S8, 24 settembre 2026): «0 results for …» e' una riga che
// cambia, e per settimane questo controllo e' stato verde su una ricerca che a schermo diceva zero.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Il TERMINE DI PROVA della Ricerca: un nome che sta di sicuro nei documenti PUBBLICI.
// Perche' LIRF: e' Fiumicino, il primo scalo della divisione — lo citano la vIPI di Roma, la vIPI d'aeroporto di LIRF
// e le vLOA di Roma, e smetterebbe di esserci solo se sparissero tutti e tre. Misurato in produzione, da anonimo,
// il 24 settembre 2026: 13 risultati. Non si usa piu' «LI»: due lettere trovano anche la GUIDA, che risponde a
// database vuoto, e il controllo non distinguerebbe un sito che trova da uno che non trova niente.
// Se un giorno il controllo dice «termine di prova da cambiare», si cambia QUI (o con TERMINE=… da fuori).
const TERMINE = process.env.TERMINE || 'LIRF';

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
      await campo.type(TERMINE, { delay: 120 });
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

    // 3-bis. ...e TROVA. Si contano i risultati che sono DOCUMENTI: i `.res-row` che NON portano alla Guida, perche'
    // le voci della Guida escono da un catalogo in memoria e ci sarebbero anche a database vuoto. ⚠️ Dall'indirizzo e
    // non dalla classe `guide`: fino al 24 settembre 2026 la pagina marcava le voci della Guida solo in italiano.
    // Si aspetta che il numero smetta di cambiare: la pagina aspetta 200 ms dall'ultimo tasto prima di cercare.
    if (cambiata) {
      let documenti = -1, fermo = 0;
      for (let i = 0; i < 20 && fermo < 3; i++) {
        await sleep(500);
        const ora = await page.evaluate(() => document.querySelectorAll('.wrap a.res-row:not([href^="/services/vsop/guide"])').length);
        fermo = ora === documenti ? fermo + 1 : 0;
        documenti = ora;
      }
      const riga = await page.evaluate(() =>
        [...document.querySelectorAll('.wrap p.muted')].map((p) => p.innerText).find((t) => /result|risultat/i.test(t)) || '');
      // ⚠️ Zero documenti con la ricerca che RISPONDE non si dice «sito rotto»: il sito ha risposto. Si dice che
      // il termine di prova non si trova piu' — che e' da cambiare se i documenti ci sono ancora (e allora si
      // cambia TERMINE qui sopra), oppure che la ricerca non trova niente (e allora e' un difetto da aprire).
      nota(`la RICERCA trova «${TERMINE}» fra i documenti pubblici`, documenti > 0,
        documenti > 0
          ? `${documenti} documenti · «${riga}»`
          : `ZERO documenti per «${TERMINE}» («${riga}»): la ricerca risponde ma non trova. Se «${TERMINE}» e' ancora ` +
            `nei documenti pubblici e' la ricerca a non trovare (difetto da aprire); se non c'e' piu', e' il TERMINE DI ` +
            `PROVA da cambiare (TERMINE in pacchetto-verifica.js)`);
    }

    if (!process.env.SOLO_PUBBLICO) {
    // 4. Una pagina di editor: e' li' che vivevano le corse di §AM.
    await page.goto(BASE + '/services/vsop/libb/editor', { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60; i++) { if (await page.evaluate(() => !!window.Blazor)) break; await sleep(1000); }
    // 🔴 IL CANCELLO SI DISEGNA, non si restituisce come stato HTTP: una pagina «Accesso riservato» torna 200,
    // ha il suo `.wrap` e non contiene nessuna delle due frasi d'errore — quindi passava per «editor aperto»,
    // e poi il controllo dopo suonava perche' il pannello ovviamente non c'era. Preso il 21 settembre 2026
    // avviando il publish in `Production`, dove l'utente e' anonimo. Vedi il runbook, «un 200 su una pagina
    // riservata». Se scatta questa riga: riavviare con ASPNETCORE_ENVIRONMENT=Development.
    const cancello = await page.evaluate(() =>
      /Accesso riservato|Restricted access|Access reserved|Non autorizzato|Not authorized/i.test(document.body.innerText));
    const editorVivo = await page.evaluate(() =>
      !document.body.innerText.includes('second operation') &&
      !document.body.innerText.includes('This page did not open') &&
      document.querySelectorAll('.wrap').length > 0) && !cancello;
    // ⚠️ Per PREFISSO: dal 6 settembre 2026 l'id porta il documento (`tr-review-<id>`), perche' in un
    // editor unito i pannelli sono uno per membro e un id ripetuto non e' un DOM valido.
    const pannelloTr = await page.evaluate(() => !!document.querySelector('[id^="tr-review"]'));
    nota('editor ACC LIBB si apre', editorVivo,
      cancello ? 'CANCELLO: «Accesso riservato» — utente anonimo, serve ASPNETCORE_ENVIRONMENT=Development'
        : editorVivo ? 'nessuna pagina d\'errore' : 'PAGINA D\'ERRORE');
    nota('pannello traduzioni presente', pannelloTr, pannelloTr ? '[id^=tr-review] nel DOM' : 'pannello traduzioni assente');
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
