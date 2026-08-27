// Verifica live del riordino trascinando, con il drag del BROWSER (CDP Input.setInterceptDrags) e
// NESSUN preventDefault iniettato: se il drop arriva, arriva perché lo consente wireTocDrop.
// Copre le tre famiglie + il rifiuto fra gruppi + la persistenza al ricarico.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = 'http://localhost:5034';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const CASI = [
  { nome: 'vIPI ACC', path: '/services/vsop/libb/editor', da: 0, su: 2 },
  { nome: 'vIPI APP', path: '/services/vsop/libb/apps/editor?app=LIBG_APP', da: 0, su: 2 },
  { nome: 'vLOA', path: '/services/vsop/libb/vloa/editor?acc=LDZO', da: 0, su: 2 },
];

async function apri(page, path) {
  await page.goto(BASE + path, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await page.waitForSelector('.toc', { timeout: 60000 });
  await sleep(2500);
}

async function inModifica(page) {
  const ok = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /✎/.test(x.innerText) && !x.disabled);
    if (b) { b.click(); return true; }
    return false;
  });
  await sleep(3500);
  return ok;
}

const voci = (page) => page.evaluate(() => [...document.querySelectorAll('.toc a')].map((x) => x.innerText.trim()));

// Un trascinamento vero: il browser avvia il drag, noi consegniamo enter/over/drop alle coordinate.
async function trascina(page, iDa, iSu) {
  const box = await page.evaluate((a, b) => {
    const n = [...document.querySelectorAll('.toc a[draggable="true"]')];
    if (n.length <= Math.max(a, b)) return null;
    n[a].scrollIntoView({ block: 'center' });
    const s = n[a].getBoundingClientRect(), d = n[b].getBoundingClientRect();
    return { sx: s.x + s.width / 2, sy: s.y + s.height / 2, dx: d.x + d.width / 2, dy: d.y + d.height / 2,
             st: n[a].innerText.trim(), dt: n[b].innerText.trim() };
  }, iDa, iSu);
  if (!box) return { ok: false, perche: 'meno voci trascinabili del previsto' };

  const client = await page.createCDPSession();
  let data = null;
  client.on('Input.dragIntercepted', (e) => { data = e.data; });
  await client.send('Input.setInterceptDrags', { enabled: true });
  await client.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: box.sx, y: box.sy, button: 'left', clickCount: 1 });
  await client.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: box.sx + 10, y: box.sy + 12, button: 'left', buttons: 1 });
  await sleep(600);
  if (!data) { await client.detach(); return { ok: false, perche: 'il browser non ha avviato il drag', box }; }
  await client.send('Input.dispatchDragEvent', { type: 'dragEnter', x: box.dx, y: box.dy, data });
  await sleep(350);
  await client.send('Input.dispatchDragEvent', { type: 'dragOver', x: box.dx, y: box.dy, data });
  await sleep(350);
  await client.send('Input.dispatchDragEvent', { type: 'drop', x: box.dx, y: box.dy, data });
  await sleep(3500);
  await client.detach();
  return { ok: true, box };
}

async function main() {
  const browser = await puppeteer.launch({
    executablePath: EDGE, headless: false,
    args: ['--no-sandbox', '--window-size=1600,1300'],
    defaultViewport: { width: 1600, height: 1300 },
  });
  const page = await browser.newPage();
  const errori = [];
  page.on('pageerror', (e) => errori.push('[pageerror] ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errori.push('[console.error] ' + String(m.text()).slice(0, 160)); });

  for (const c of CASI) {
    errori.length = 0;
    console.log('\n======== ' + c.nome + '  ' + c.path);
    await apri(page, c.path);
    if (!(await inModifica(page))) { console.log('  !! non sono riuscito a entrare in Modifica'); continue; }

    const prima = await voci(page);
    const r = await trascina(page, c.da, c.su);
    if (!r.ok) { console.log('  !! ' + r.perche); continue; }
    const dopo = await voci(page);
    console.log('  gesto  : «' + r.box.st + '» su «' + r.box.dt + '»');
    console.log('  prima  : ' + JSON.stringify(prima.slice(0, 6)));
    console.log('  dopo   : ' + JSON.stringify(dopo.slice(0, 6)));
    console.log('  esito  : ' + (dopo.join() === prima.join() ? '>>> INVARIATO (KO)' : 'cambiato'));

    // pill ↑n ↓n: si ricalcolano dallo stesso ordine
    const pill = await page.evaluate(() => [...document.querySelectorAll('.sec-moved,.ed-moved,[class*="moved"]')].map((x) => x.innerText.trim()).slice(0, 8));
    if (pill.length) console.log('  pill   : ' + JSON.stringify(pill));

    // persistenza
    await apri(page, c.path);
    const ric = await voci(page);
    console.log('  ricarico: ' + (ric.join() === dopo.join() ? 'PERSISTE' : '>>> NON persiste: ' + JSON.stringify(ric.slice(0, 6))));
    if (errori.length) console.log('  errori : ' + JSON.stringify(errori.slice(0, 3)));
  }

  // --- rifiuto fra gruppi diversi (solo ACC: è l'unico con più gruppi) ---
  console.log('\n======== rifiuto fra blocchi diversi (vIPI ACC)');
  await apri(page, CASI[0].path);
  await inModifica(page);
  const gruppi = await page.evaluate(() => {
    // le voci sono raggruppate: prendo la prima del primo gruppo e la prima del secondo
    const li = [...document.querySelectorAll('.toc li')];
    const idx = []; let visto = 0;
    li.forEach((l, i) => { if (l.classList.contains('toc-grp-li')) { visto++; idx.push(i); } });
    return visto;
  });
  const prima2 = await voci(page);
  // l'ultima voce-sezione appartiene per forza all'ultimo blocco: trascino la prima sull'ultima
  const n = await page.evaluate(() => document.querySelectorAll('.toc a[draggable="true"]').length);
  const r2 = await trascina(page, 0, n - 1);
  const dopo2 = await voci(page);
  console.log('  gruppi visti: ' + gruppi + ' — gesto: ' + (r2.ok ? '«' + r2.box.st + '» su «' + r2.box.dt + '»' : r2.perche));
  console.log('  esito : ' + (dopo2.join() === prima2.join() ? 'INVARIATO (giusto)' : '>>> CAMBIATO (KO: ha attraversato i gruppi)'));

  await page.screenshot({ path: __dirname + '/drag-verifica.png' });
  await browser.close();
}
main().catch((e) => { console.error('FALLITO:', e); process.exit(1); });
