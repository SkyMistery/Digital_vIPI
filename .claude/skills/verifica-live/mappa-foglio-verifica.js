// Il FOGLIO di Leaflet dopo la navigazione enhanced (21 settembre 2026). Riferito dal campo: «arrivo su un
// documento da un'altra pagina e le mappe sono a pezzi; ricarico ed è a posto». Le tessere uscivano come
// immagini in fila: `leaflet.css` era sparito dal <head> (il DomSync di Blazor toglie i `link` messi da uno
// script) mentre `window.L` restava, e vipi-aor.js rimetteva il foglio solo quando `L` mancava.
//
//   node mappa-foglio-verifica.js                         su :5034, LIBB in bozza
//   BASE=http://localhost:5199 DOC=/services/vsop/lirr/vipi node mappa-foglio-verifica.js
//
// Giro: pagina senza mappa (caricata per intero) → documento con mappe → pagina senza mappa → di nuovo il
// documento, tutto con navigazione enhanced (Blazor.navigateTo). Al secondo arrivo si guarda che il foglio
// ci sia e che le tessere siano posizionate da Leaflet (position:absolute), come al primo.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5034';
const DOC = process.env.DOC || '/services/vsop/libb/vipi?as=draft';
const ALTRA = process.env.ALTRA || '/services/vsop';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const esiti = [];
  const nota = (nome, ok, dettaglio) => { esiti.push(ok); console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`); };
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1400, height: 1000 });
  const errori = [];
  page.on('pageerror', (e) => errori.push('pageerror: ' + e.message));

  const vai = async (dove) => {
    await page.evaluate((d) => Blazor.navigateTo(d), dove);
    try {
      await page.waitForFunction((d) => (location.pathname + location.search) === d, { timeout: 30000 }, dove);
    } catch (e) {
      throw new Error(`verso ${dove}: sono su ${await page.evaluate(() => location.href)} — ${e.message}`);
    }
    await sleep(6000);                                   // mappe a scaglioni, tessere in rete
  };
  const stato = () => page.evaluate(() => {
    const fogli = [...document.querySelectorAll('link[rel=stylesheet]')].filter((l) => /leaflet\.css/.test(l.href)).length;
    const mappe = [...document.querySelectorAll('.aor-leaflet')];
    const vive = mappe.filter((m) => m.querySelector('.leaflet-pane')).length;
    const tessere = [...document.querySelectorAll('.aor-leaflet img.leaflet-tile')];
    const fuori = tessere.filter((t) => getComputedStyle(t).position !== 'absolute').length;
    const pane = document.querySelector('.aor-leaflet .leaflet-pane');
    return { L: !!window.L, fogli, mappe: mappe.length, vive, tessere: tessere.length, fuori,
             pane: pane ? getComputedStyle(pane).position : '-' };
  });

  try {
    await page.goto(BASE + ALTRA, { waitUntil: 'networkidle2', timeout: 120000 });
    await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
    await sleep(1500);

    await vai(DOC);
    const primo = await stato();
    console.log('primo arrivo ', JSON.stringify(primo));
    nota('primo arrivo: mappe disegnate', primo.vive > 0, `${primo.vive}/${primo.mappe}`);
    nota('primo arrivo: foglio in pagina', primo.fogli > 0, `${primo.fogli} link`);

    await vai(ALTRA);
    const via = await stato();
    console.log('fuori        ', JSON.stringify(via));

    await vai(DOC);
    const secondo = await stato();
    console.log('secondo arr. ', JSON.stringify(secondo));
    const nav = await page.evaluate(() => performance.getEntriesByType('navigation')[0].name);
    nota('nessun ricarico vero nel giro', !nav.includes('/vipi'), nav);
    nota('secondo arrivo: mappe disegnate', secondo.vive > 0, `${secondo.vive}/${secondo.mappe}`);
    nota('secondo arrivo: foglio in pagina', secondo.fogli > 0, `${secondo.fogli} link`);
    nota('secondo arrivo: pane posizionato da Leaflet', secondo.pane === 'absolute', secondo.pane);
    nota('secondo arrivo: tessere posizionate', secondo.tessere > 0 && secondo.fuori === 0,
         `${secondo.fuori} fuori posto su ${secondo.tessere}`);
    await page.screenshot({ path: 'mappa-foglio.png' });
    nota('nessun errore di pagina', errori.length === 0, errori.slice(0, 3).join(' | ') || 'pulita');
  } catch (e) {
    nota('giro completato', false, e.message);
  } finally {
    await browser.close();
    console.log(esiti.every(Boolean) ? `\nVERDE ${esiti.length}/${esiti.length}` : `\nROSSO ${esiti.filter(Boolean).length}/${esiti.length}`);
    process.exit(esiti.every(Boolean) ? 0 : 1);
  }
})();
