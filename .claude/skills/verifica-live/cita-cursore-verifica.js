// «Cita» deve scrivere DOVE STA IL CURSORE (21 settembre 2026). Segnalato dal campo: «la cite non la inserisce
// dove ho messo il cursore ma sempre alla fine». Tre casi, uno per lancio:
//   node cita-cursore-verifica.js <editor>                 cursore a meta' col fuoco: il riferimento va li'
//   PERDI_FUOCO=1 node cita-cursore-verifica.js <editor>   cursore a meta', poi un clic FUORI dal campo: va li'
//                                                          lo stesso (era QUESTO il difetto: finiva in coda)
//   MAI_TOCCATO=1 node cita-cursore-verifica.js <editor>   campo mai cliccato: va in CODA, per scelta
// ⚠️ Solo in LOCALE: scrive nel documento. Editor di prova: http://localhost:5034/services/vsop/libb/airports/editor?icao=LIBD
// ⚠️ dato-verifica.js non copre questo caso: mette sempre il cursore in coda prima di citare.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const URL = process.argv[2] || 'http://localhost:5034/services/vsop/libb/airports/editor?icao=LIBD';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await b.newPage();
  await page.setViewport({ width: 1440, height: 1100 });
  page.on('dialog', (d) => d.accept());
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(3000);
  const entrato = await page.evaluate(() => {
    const x = [...document.querySelectorAll('button')].find((y) => /✎/.test(y.textContent) && !y.disabled);
    if (!x) return false; x.scrollIntoView({ block: 'center' }); x.click(); return true;
  });
  console.log('in modifica:', entrato);
  await sleep(5000);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(2500);
  if (!await page.evaluate(() => [...document.querySelectorAll('.rta textarea')].some((t) => t.offsetParent !== null))) {
    await page.evaluate(() => { const m = document.querySelector('details.blk-add'); m.open = true; m.querySelector('.blk-add-menu button').click(); });
    await sleep(3000);
    await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
    await sleep(1000);
  }
  const sel = '#' + await page.evaluate(() => {
    const r = [...document.querySelectorAll('.rta')].find((x) => x.querySelector('textarea').offsetParent !== null);
    r.scrollIntoView({ block: 'center' });
    const t = r.querySelector('textarea'); t.id = t.id || 'rta-prova'; return t.id;
  });

  if (process.env.MAI_TOCCATO) {
    // Un campo col suo testo, in cui NESSUNO ha cliccato: il riferimento deve andare in CODA.
    const valore = await page.$eval(sel, (el) => el.value);
    const tastoM = await page.evaluateHandle((s) => document.querySelector(s).closest('.rta').querySelector('.rta-sid'), sel);
    await tastoM.click();
    await page.waitForSelector('.sidref-pick', { timeout: 20000 });
    await (await page.evaluateHandle(() => [...document.querySelectorAll('.sidref-pick-kind button')].find((x) => x.textContent.trim() === 'ATC'))).click();
    await page.waitForSelector('.sidref-pick-row', { timeout: 20000 });
    await sleep(500);
    await (await page.$('.sidref-pick-row')).click();
    await sleep(1500);
    const dopoM = await page.$eval(sel, (el) => el.value);
    console.log(`MAI TOCCATO: prima ${valore.length} caratteri · il riferimento sta in coda: ${dopoM.startsWith(valore) && /\]\]\s*$/.test(dopoM)} · ${JSON.stringify(dopoM.slice(-40))}`);
    await b.close();
    return;
  }

  // Testo noto scritto a TASTIERA, poi cursore a metà con le frecce.
  await page.click(sel);
  await page.keyboard.down('Control'); await page.keyboard.press('a'); await page.keyboard.up('Control');
  await page.keyboard.press('Backspace');
  await page.keyboard.type('AAAA BBBB');
  await sleep(1500);                                   // lascia arrivare l'eventuale ridisegno del circuito
  for (let i = 0; i < 4; i++) await page.keyboard.press('ArrowLeft');
  if (process.env.PERDI_FUOCO) {
    // Il gesto che toglie il fuoco al campo prima del tasto: un clic su un punto neutro della pagina.
    await page.mouse.click(5, 300);
    await sleep(400);
  }
  const prima = await page.$eval(sel, (el) => ({ s: el.selectionStart, fuoco: document.activeElement === el }));
  console.log('prima del tasto: cursore', prima.s, '· fuoco nel campo', prima.fuoco);

  const tasto = await page.evaluateHandle((s) => document.querySelector(s).closest('.rta').querySelector('.rta-sid'), sel);
  await tasto.click();
  await sleep(800);
  console.log('segno salvato:', await page.$eval(sel, (el) => el.getAttribute('data-sid-bersaglio')));

  await page.waitForSelector('.sidref-pick', { timeout: 20000 });
  const chip = await page.evaluateHandle(() =>
    [...document.querySelectorAll('.sidref-pick-kind button')].find((x) => x.textContent.trim() === 'ATC'));
  await chip.click();
  await page.waitForSelector('.sidref-pick-row', { timeout: 20000 });
  await sleep(500);
  await (await page.$('.sidref-pick-row')).click();
  await sleep(1500);
  const dopo1 = await page.$eval(sel, (el) => ({ v: el.value, s: el.selectionStart, fuoco: document.activeElement === el }));
  console.log('testo dopo la 1a:', JSON.stringify(dopo1.v));
  const atteso = dopo1.v.indexOf(']]') + 2 + 1;   // dopo il riferimento e lo spazio aggiunto
  console.log(`cursore dopo la 1a: ${dopo1.s} (dove dovrebbe stare: ~${atteso - 1}..${atteso}, lunghezza ${dopo1.v.length}) · fuoco ${dopo1.fuoco}`);

  // Seconda citazione SUBITO, come farebbe chi cita due enti di fila.
  const tasto2 = await page.evaluateHandle((s) => document.querySelector(s).closest('.rta').querySelector('.rta-sid'), sel);
  await tasto2.click();
  await page.waitForSelector('.sidref-pick', { timeout: 20000 });
  const chip2 = await page.evaluateHandle(() =>
    [...document.querySelectorAll('.sidref-pick-kind button')].find((x) => x.textContent.trim() === 'ATC'));
  await chip2.click();
  await page.waitForSelector('.sidref-pick-row', { timeout: 20000 });
  await sleep(500);
  await (await page.$$('.sidref-pick-row'))[1].click();
  await sleep(1500);
  console.log('testo dopo la 2a:', JSON.stringify(await page.$eval(sel, (el) => el.value)));
  await b.close();
})();
