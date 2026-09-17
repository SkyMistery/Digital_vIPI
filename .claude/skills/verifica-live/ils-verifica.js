// §A61: nell'anagrafica delle radioassistenze, due `change` di fila sul Tipo (come il sintetico di vipi-editor.js
// più quello del browser) non devono far morire la pagina. Prima: «A second operation was started» e circuito giù.
//   node ils-verifica.js [http://localhost:5199]
const puppeteer = require('puppeteer-core');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const BASE = process.argv[2] || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const p = await b.newPage();
  const errori = [];
  p.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });
  await p.goto(BASE + '/services/vsop/admin/navaids', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await p.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await p.waitForSelector('table.navadm-table td.c-type input', { timeout: 60000 });
  await sleep(1500);
  const giri = 5;
  for (let i = 0; i < giri; i++) {
    const valore = i % 2 === 0 ? 'ILS' : 'VOR';
    await p.evaluate((v) => {
      const el = document.querySelector('table.navadm-table td.c-type input');
      el.value = v;
      el.dispatchEvent(new Event('change', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }, valore);
    await sleep(1500);
  }
  const morta = await p.evaluate(() => {
    const ui = document.getElementById('blazor-error-ui');
    return !!ui && getComputedStyle(ui).display !== 'none';
  });
  const tipo = await p.$eval('table.navadm-table td.c-type input', (el) => el.value);
  // ancora viva? un altro change singolo deve passare
  await p.evaluate(() => {
    const el = document.querySelector('table.navadm-table td.c-type input');
    el.value = ''; el.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await sleep(1500);
  const dopo = await p.$eval('table.navadm-table td.c-type input', (el) => el.value);
  console.log(`${morta ? 'ROSSO' : 'OK  '} barra d'errore di Blazor ${morta ? 'VISIBILE' : 'assente'} dopo ${giri} coppie di change`);
  console.log(`${tipo === 'ILS' ? 'OK  ' : 'ROSSO'} valore scritto e riletto: «${tipo}»`);
  console.log(`${dopo === '' ? 'OK  ' : 'ROSSO'} la pagina risponde ancora (tipo svuotato: «${dopo}»)`);
  console.log(`console: ${errori.length ? errori.join(' | ') : 'pulita'}`);
  await b.close();
})();
