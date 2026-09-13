// Verifica della NAVIGAZIONE ENHANCED (lotto L7 della revisione del 13 settembre 2026): Blazor rimpiazza il DOM
// senza ricaricare la pagina, il DomSync CONSERVA i nodi uguali e cancella gli attributi che il server non
// scrive. Un segno «già agganciato» tenuto in un `data-` sparisce, e l'aggancio si rifà sopra il vecchio.
//
//   node enhanced-verifica.js        sul publish win-x64 su :5199
//
// T-016: dall'elenco vAWOS a uno scalo i tasti si agganciavano due volte (un clic = due gestori: DAY/NIGHT
// tornava com'era, i pannelli si aprivano e si richiudevano subito).
// T-015: una mappa AoR dopo un cambio di ?vista= sulla stessa pagina restava vuota.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const esiti = [];
  const nota = (nome, ok, dettaglio) => { esiti.push(ok); console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`); };
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errori = [];
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });
  page.on('pageerror', (e) => errori.push('pageerror: ' + e.message));

  try {
    // ── T-016 ─────────────────────────────────────────────────────────────
    await page.goto(BASE + '/services/vawos', { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60 && !(await page.evaluate(() => !!window.Blazor)); i++) await sleep(500);
    const link = await page.$('.awos a[href^="/services/vawos/"]');
    const dove = await link.evaluate((a) => a.getAttribute('href'));
    await link.click();                                   // navigazione enhanced, non un ricarico
    await page.waitForFunction((h) => location.pathname === h, { timeout: 15000 }, dove);
    await page.waitForSelector('[data-awos-tema]', { timeout: 15000 });
    await sleep(1500);
    const ricaricata = await page.evaluate(() => performance.getEntriesByType('navigation')[0].name);
    nota('si è arrivati allo scalo senza ricaricare', !ricaricata.includes(dove), dove);

    const temaPrima = await page.$eval('.awos', (e) => e.getAttribute('data-awos-theme'));
    await page.click('[data-awos-tema]');
    await sleep(300);
    const temaDopo = await page.$eval('.awos', (e) => e.getAttribute('data-awos-theme'));
    nota('T-016: DAY/NIGHT commuta con un clic', temaPrima !== temaDopo, `${temaPrima} → ${temaDopo}`);

    await page.click('[data-awos-mask="report"]');
    await sleep(300);
    const aperto = await page.$eval('[data-awos-pan="report"]', (p) => !p.hidden);
    nota('T-016: LOCAL REP. si apre e resta aperto', aperto, aperto ? 'aperto' : 'richiuso subito');

    // ── T-015: la mappa AoR dopo un cambio di vista sulla stessa pagina ─────
    const vipi = process.env.DOC || '/services/vsop/lirr/vipi';
    await page.goto(BASE + vipi, { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60 && !(await page.evaluate(() => !!window.Blazor)); i++) await sleep(500);
    await sleep(2500);
    const mappePrima = await page.$$eval('.aor-leaflet', (els) => els.map((e) => !!e.querySelector('.leaflet-pane')));
    const chip = await page.$('a[href*="vista="]');
    if (!chip || mappePrima.length === 0) {
      nota('T-015: pagina con mappe e chip di vista', false, `mappe ${mappePrima.length}, chip ${!!chip} (impostare DOC=)`);
    } else {
      // ⚠️ Prima di tutto: le mappe devono essere disegnate PRIMA del clic, o «vuote dopo» non prova niente.
      nota('T-015: prima del clic le mappe sono disegnate', mappePrima.every((x) => x), `${mappePrima.length} mappe`);
      await chip.click();
      await sleep(3500);
      const mappeDopo = await page.$$eval('.aor-leaflet', (els) => els.map((e) => !!e.querySelector('.leaflet-pane')));
      const vuote = mappeDopo.filter((x) => !x).length;
      nota('T-015: le mappe restano disegnate dopo il cambio di vista', mappeDopo.length > 0 && vuote === 0,
        `${mappeDopo.length} mappe, ${vuote} vuote`);
    }

    nota('console del browser pulita', errori.length === 0, errori.length ? errori.slice(0, 3).join(' | ') : 'nessun errore');
  } catch (e) {
    nota('eccezione', false, e.message);
  } finally {
    await browser.close();
  }
  const rossi = esiti.filter((x) => !x).length;
  console.log(rossi ? `\n===== ${rossi} ROSSI =====` : '\n===== TUTTO VERDE =====');
  process.exit(rossi ? 1 : 0);
})();
