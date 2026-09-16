// Aree di lavoro del vSOP militare (§A40): tabella sola, riga che si apre, chip che spengono le righe, stampa.
// E che la vIPI ACC tenga il suo elenco a schede (l'interruttore ShowCards vale per i soli vSOP militari).
//
//   node aree-mil-verifica.js                          sul publish win-x64 su :5199 (DB di sviluppo: LIBG, 3 aree)
//   BASE=http://localhost:5034 node aree-mil-verifica.js
//
// ⚠️ La vIPI ACC sta in /services/vsop/{acc}/vipi, NON in /services/vsop/{acc}: su quella pagina di schede non
// ce n'è nessuna, e il controllo «l'ACC tiene le schede» esce rosso per colpa dello strumento (16-set-2026).
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let ko = 0;
const dice = (ok, msg) => { if (!ok) ko++; console.log((ok ? '  OK  ' : '  KO  ') + msg); };

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--window-size=1500,1300'], defaultViewport: { width: 1500, height: 1300 } });
  const page = await browser.newPage();
  const errori = [];
  page.on('pageerror', (e) => errori.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });

  await page.goto(BASE + '/services/vsop/libb/mil?icao=LIBG', { waitUntil: 'networkidle2', timeout: 120000 });
  await sleep(3000);
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => { d.open = true; }));
  await sleep(1500);

  const scope = 'reg-regulated';
  const s0 = await page.evaluate((sc) => {
    const box = document.querySelector(`[data-areacards="${sc}"]`);
    return {
      box: !!box,
      tabella: !!(box && box.querySelector('table.milarea-table')),
      righe: box ? box.querySelectorAll('tr[data-areacard]').length : 0,
      dettagli: box ? box.querySelectorAll('tr.milarea-more').length : 0,
      aperti: box ? box.querySelectorAll('tr.milarea-more:not([hidden])').length : 0,
      schede: document.querySelectorAll('details.reg-card').length,
      conta: box?.querySelector('[data-areacount]')?.textContent.trim(),
      chip: document.querySelectorAll(`.aor-block[data-aor="${sc}"] .aor-chip`).length,
    };
  }, scope);
  console.log('  ..', JSON.stringify(s0));
  dice(s0.box && s0.tabella, 'la sezione ha la tabella dentro il contenitore delle aree');
  dice(s0.righe === 3, `tre righe d'area (${s0.righe})`);
  dice(s0.dettagli === s0.righe && s0.aperti === 0, 'una riga di dettaglio per area, tutte chiuse');
  dice(s0.schede === 0, `nessun elenco a schede nel vSOP (${s0.schede})`);
  dice(s0.chip === 3, `tre chip sulla mappa (${s0.chip})`);

  // ▸ apre il dettaglio
  const id = await page.evaluate((sc) => {
    const tr = document.querySelector(`[data-areacards="${sc}"] tr[data-areacard]`);
    const b = tr.querySelector('.milarea-exp'); b.scrollIntoView({ block: 'center' }); b.click();
    return tr.getAttribute('data-areacard');
  }, scope);
  await sleep(500);
  const s1 = await page.evaluate((sc, id) => {
    const box = document.querySelector(`[data-areacards="${sc}"]`);
    const det = box.querySelector(`tr[data-areamore="${id}"]`);
    return { aperto: !det.hidden, visibile: det.offsetParent !== null, testo: det.innerText.trim().slice(0, 120),
             aria: box.querySelector(`tr[data-areacard="${id}"] .milarea-exp`).getAttribute('aria-expanded') };
  }, scope, id);
  console.log('  ..', JSON.stringify(s1));
  dice(s1.aperto && s1.visibile && s1.aria === 'true', 'la freccetta apre il dettaglio e aria-expanded lo dice');
  dice(s1.testo.length > 0, 'il dettaglio ha testo (attivazione/descrizione o «—»)');

  // la chip spegne la riga (e chiude il dettaglio), il conteggio cambia
  await page.evaluate((sc, id) => {
    const c = document.querySelector(`.aor-block[data-aor="${sc}"] .aor-chip[data-sec="${id}"]`);
    c.scrollIntoView({ block: 'center' }); c.click();
  }, scope, id);
  await sleep(700);
  const s2 = await page.evaluate((sc, id) => {
    const box = document.querySelector(`[data-areacards="${sc}"]`);
    return { rigaNascosta: box.querySelector(`tr[data-areacard="${id}"]`).hidden,
             detNascosto: box.querySelector(`tr[data-areamore="${id}"]`).hidden,
             conta: box.querySelector('[data-areacount]').textContent.trim() };
  }, scope, id);
  console.log('  ..', JSON.stringify(s2));
  dice(s2.rigaNascosta && s2.detNascosto, 'spenta la chip, spariscono la riga e il suo dettaglio');
  dice(s2.conta !== s0.conta, `il conteggio cambia («${s0.conta}» → «${s2.conta}»)`);

  // riaccesa: la riga torna, CHIUSA
  await page.evaluate((sc, id) => document.querySelector(`.aor-block[data-aor="${sc}"] .aor-chip[data-sec="${id}"]`).click(), scope, id);
  await sleep(700);
  const s3 = await page.evaluate((sc, id) => {
    const box = document.querySelector(`[data-areacards="${sc}"]`);
    return { riga: !box.querySelector(`tr[data-areacard="${id}"]`).hidden, det: box.querySelector(`tr[data-areamore="${id}"]`).hidden };
  }, scope, id);
  dice(s3.riga && s3.det, 'riaccesa, la riga torna e il dettaglio riparte chiuso');

  // stampa: dettagli aperti, freccette nascoste
  await page.emulateMediaType('print');
  await sleep(500);
  const s4 = await page.evaluate((sc) => {
    const box = document.querySelector(`[data-areacards="${sc}"]`);
    const det = [...box.querySelectorAll('tr.milarea-more')];
    const exp = [...box.querySelectorAll('.milarea-exp')];
    return { detVisibili: det.filter((d) => getComputedStyle(d).display !== 'none').length, det: det.length,
             frecceVisibili: exp.filter((b) => getComputedStyle(b).display !== 'none').length };
  }, scope);
  console.log('  ..', JSON.stringify(s4));
  dice(s4.detVisibili === s4.det && s4.frecceVisibili === 0, 'in stampa i dettagli sono aperti e le freccette spariscono');
  await page.emulateMediaType('screen');
  await page.evaluate((sc) => document.querySelector(`[data-areacards="${sc}"]`).scrollIntoView({ block: 'start' }), scope);
  await sleep(400);
  await page.screenshot({ path: 'aree-mil.png' });

  // l'ACC tiene le schede
  await page.goto(BASE + '/services/vsop/libb/vipi', { waitUntil: 'networkidle2', timeout: 120000 });
  await sleep(3000);
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => { if (!d.hasAttribute('data-areacard')) d.open = true; }));
  await sleep(2500);
  const acc = await page.evaluate(() => ({ schede: document.querySelectorAll('details.reg-card').length, tabelle: document.querySelectorAll('table.milarea-table').length }));
  console.log('  ..', JSON.stringify(acc));
  dice(acc.schede > 0 && acc.tabelle === 0, 'la vIPI ACC tiene l\'elenco a schede');

  dice(errori.length === 0, `console pulita${errori.length ? ': ' + errori.slice(0, 3).join(' | ') : ''}`);
  await browser.close();
  console.log(ko === 0 ? '\nTUTTO VERDE' : `\n${ko} ROSSI`);
  process.exit(ko ? 1 : 0);
})();
