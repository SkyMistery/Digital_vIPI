// §CN — la prova del RIMBALZO IN HOME, sul PACCHETTO (JS minificato), non sul sorgente.
//
// Il difetto: in `wireAnchors` la ricerca del bersaglio stava PRIMA di `preventDefault`, con un `return`
// in mezzo. Sezione non nel DOM -> la protezione si sfila -> il clic va all'intercettore di Blazor ->
// con `<base href="/">` il link «#s-4256» viene risolto come «/#s-4256», cioe' LA HOME.
//
// ⚠️ Il presidio C# (`AncoraCheNonTrovaIlBersaglioTests`) legge il SORGENTE. Questo guida il file
// MINIFICATO dentro un browser vero: il minificatore riscrive quel gestore in espressioni-virgola, e
// «l'ordine regge anche dopo la minificazione» e' un fatto che solo questa prova puo' stabilire.
//
//   NODE_PATH=<...>/node_modules node ancora-verifica.js
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const PAGINA = process.env.PAGINA || '/services/vsop/libb/airports/editor?icao=LIBD';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const esiti = [];
  const nota = (nome, ok, dettaglio) => {
    esiti.push({ nome, ok });
    console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`);
  };

  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const erroriConsole = [];
  page.on('console', (m) => { if (m.type() === 'error') erroriConsole.push(m.text()); });
  page.on('pageerror', (e) => erroriConsole.push('pageerror: ' + e.message));

  try {
    await page.goto(BASE + PAGINA, { waitUntil: 'networkidle2' });
    await sleep(3500);

    // Il menu delle sezioni dev'esserci, o la prova non prova niente.
    const ancore = await page.$$eval('a[href^="#"]', (a) => a.map((x) => x.getAttribute('href')));
    nota('la pagina ha ancore interne', ancore.length > 0, `${ancore.length} trovate, es. ${ancore[0]}`);
    if (ancore.length === 0) throw new Error('nessuna ancora: prova non conclusiva');

    // ---- 1. Il caso NORMALE: il bersaglio c'e', si scorre e si resta sulla pagina ----
    const conBersaglio = await page.evaluate(() =>
      [...document.querySelectorAll('a[href^="#"]')]
        .map((a) => a.getAttribute('href'))
        .find((h) => h.length > 1 && document.getElementById(h.slice(1))));
    if (conBersaglio) {
      await page.evaluate((h) => document.querySelector(`a[href="${h}"]`).click(), conBersaglio);
      await sleep(1200);
      const u = new URL(page.url());
      nota('ancora CON bersaglio: resta sulla pagina', u.pathname.includes('/airports/editor'),
        `${u.pathname}${u.hash}`);
    } else {
      nota('ancora CON bersaglio: nessuna disponibile', true, 'saltato (non inficia il caso che conta)');
    }

    // ---- 2. 🔴 IL CASO CHE CONTA: il bersaglio NON c'e' ----
    // E' esattamente quel che succede riordinando i membri di un'unione: la voce del menu c'e' gia', la
    // sezione non e' ancora resa. Qui si riproduce in modo deterministico puntando un id inesistente.
    const primaUrl = page.url();
    await page.evaluate(() => {
      const a = document.querySelector('a[href^="#"]');
      a.setAttribute('href', '#s-999999');   // nessun elemento con questo id, garantito
      a.id = 'vipi-ancora-di-prova';
    });
    await page.evaluate(() => document.getElementById('vipi-ancora-di-prova').click());
    await sleep(2000);

    const dopo = new URL(page.url());
    const prima = new URL(primaUrl);

    // ⚠️ UNA asserzione sola, e larga: «non e' andata in home» sarebbe TROPPO STRETTA e darebbe un verde
    // bugiardo. Misurato sul JS di 1.18.1: il clic non finisce su «/» ma su «/services» — la radice
    // rimanda li', ed e' la «home» che si vede a schermo. Un controllo su `pathname === '/'` sarebbe
    // passato proprio nel caso che deve inchiodare.
    nota('ancora SENZA bersaglio: resta sulla STESSA pagina', dopo.pathname === prima.pathname,
      dopo.pathname === prima.pathname ? dopo.pathname : `PORTATA VIA: ${prima.pathname} -> ${dopo.pathname}`);

    // ---- 3. E la pagina risponde ancora: il circuito non e' caduto ----
    const viva = await page.evaluate(() => typeof window.Blazor !== 'undefined' && !!document.querySelector('body'));
    nota('il circuito Blazor e ancora vivo', viva, 'window.Blazor presente');

    nota('console del browser pulita', erroriConsole.length === 0,
      erroriConsole.length ? erroriConsole.slice(0, 3).join(' | ') : 'nessun errore');
  } catch (e) {
    nota('esecuzione', false, e.message);
  } finally {
    await browser.close();
  }

  const rossi = esiti.filter((e) => !e.ok);
  console.log(rossi.length === 0 ? '\n===== TUTTO VERDE =====' : `\n===== ${rossi.length} ROSSI =====`);
  process.exit(rossi.length === 0 ? 0 : 1);
})();
