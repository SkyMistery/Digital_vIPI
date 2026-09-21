// Il sommario: un gruppo INTESTATO si chiude e si apre tutto intero (§A103, 1.39.1).
//
//   node gruppi-toc-verifica.js [URL ...]
// default: vIPI ACC LIBB (pubblica e bozza) ed editor ACC LIBB, su :5199.
//
// Per ogni pagina: i gruppi intestati sono <details open>; un clic sul titolo del PRIMO gruppo lo chiude e
// ne nasconde le voci (misurato con checkVisibility, non con innerText né getClientRects); un secondo clic lo riapre; e la
// pagina NON si sposta (il clic sull'intestazione non è un link). Lascia uno screenshot col primo chiuso.
const puppeteer = require('puppeteer-core');
const path = require('path');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const URLS = process.argv.slice(2).length ? process.argv.slice(2) : [
  '/services/vsop/libb/vipi',
  '/services/vsop/libb/vipi?as=draft',
  '/services/vsop/libb/editor',
];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1500, height: 1000 });
  const errori = [];
  page.on('pageerror', (e) => errori.push(e.message));
  let rossi = 0;
  const nota = (ok, s) => { if (!ok) rossi++; console.log(`${ok ? 'OK  ' : 'ROSSO'} ${s}`); };

  for (const u of URLS) {
    await page.goto(BASE + u, { waitUntil: 'networkidle2' });
    await sleep(2500);
    const stato = () => page.evaluate(() => [...document.querySelectorAll('.toc details.toc-grp-d')].map((d) => ({
      titolo: d.querySelector(':scope>summary').textContent.trim(),
      open: d.open,
      // ⚠️ checkVisibility e NON getClientRects: Edge nasconde il contenuto di un <details> chiuso con
      // `content-visibility`, e getClientRects continua a rendere le scatole — la prima stesura di questo
      // script diceva «voci visibili 11» su un gruppo chiuso, e sembrava un gruppo che non si chiude.
      vociVisibili: [...d.querySelectorAll(':scope>ul a')].filter((a) => a.checkVisibility()).length,
    })));
    const prima = await stato();
    console.log(`\n${u}: ${prima.length} gruppi — ${prima.map((g) => `${g.titolo}(${g.vociVisibili})`).join(', ')}`);
    if (prima.length === 0) { nota(false, 'nessun gruppo intestato'); continue; }
    nota(prima.every((g) => g.open), 'tutti i gruppi nascono aperti');

    const y0 = await page.evaluate(() => scrollY);
    await page.click('.toc details.toc-grp-d > summary');
    await sleep(300);
    const chiuso = (await stato())[0];
    nota(!chiuso.open && chiuso.vociVisibili === 0, `clic sul titolo: «${chiuso.titolo}» chiuso, voci visibili ${chiuso.vociVisibili}`);
    nota((await stato()).slice(1).every((g) => g.open), 'gli altri gruppi restano aperti');
    nota((await page.evaluate(() => scrollY)) === y0, 'la pagina non si sposta');
    await page.screenshot({ path: path.join(process.env.OUT || '.', `gruppi-toc-${URLS.indexOf(u)}.png`) });

    await page.click('.toc details.toc-grp-d > summary');
    await sleep(300);
    const riaperto = (await stato())[0];
    nota(riaperto.open && riaperto.vociVisibili === prima[0].vociVisibili, `secondo clic: riaperto, voci ${riaperto.vociVisibili}`);
  }
  nota(errori.length === 0, `console: ${errori.length ? errori.join(' | ') : 'pulita'}`);
  console.log(rossi ? `\n===== ${rossi} ROSSI =====` : '\n===== TUTTO VERDE =====');
  await browser.close();
  process.exit(rossi ? 1 : 0);
})();
