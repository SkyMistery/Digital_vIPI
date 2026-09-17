// Sonda: accendere e spegnere i singoli spazi dalla tabella (carta §6), 2D e 3D, hover riga ↔ poligono.
const puppeteer = require('puppeteer-core');
const BASE = process.env.BASE || 'http://localhost:5199';
const PATH = process.env.DOC || '/services/vsop/libb/apps/vipi?app=LIBP_APP&as=draft';
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: 'new', defaultViewport: { width: 1400, height: 1200 } });
  const p = await b.newPage(); const errs = [];
  p.on('pageerror', e => errs.push('pageerror ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push('console ' + m.text()); });
  await p.goto(BASE + PATH, { waitUntil: 'networkidle2', timeout: 180000 });
  await sleep(4000);
  const stato = () => p.evaluate(() => {
    const lf = document.querySelector('.aor-block .aor-leaflet');
    const tab = document.querySelector('[data-aorasp]');
    const layers = lf && lf._secMap ? Object.values(lf._secMap).flatMap(e => e.layers) : [];
    return {
      scope: tab && tab.dataset.aorasp, blocco: lf && lf.closest('.aor-block').dataset.aor,
      poligoniSullaMappa: layers.filter(l => l._map).length, poligoniTotali: layers.length,
      conRef: layers.filter(l => l._ref).length,
      righe: [...document.querySelectorAll('.aorasp-table tr[data-volref]')].map(tr => tr.querySelector('.aorasp-vis').getAttribute('aria-pressed') + ':' + tr.cells[1].innerText.trim()),
    };
  });
  console.log('iniziale', JSON.stringify(await stato()));
  // spegni Z2 e Z4
  const vis = (n) => p.evaluate((n) => document.querySelectorAll('.aorasp-vis')[n].click(), n);
  await vis(1); await vis(3); await sleep(400);
  console.log('spenti Z2,Z4', JSON.stringify(await stato()));
  // chip del settore LIBP_APP: spegni e riaccendi -> Z2/Z4 devono restare spenti
  const chip = () => p.evaluate(() => document.querySelector('.aor-chip[data-sec="LIBP_APP"]').click());
  await chip(); await sleep(300);
  console.log('chip APP off', JSON.stringify((await stato()).poligoniSullaMappa));
  await chip(); await sleep(300);
  console.log('chip APP on', JSON.stringify((await stato()).poligoniSullaMappa));
  // hover riga Z1 -> poligono evidenziato (weight 4)
  await p.hover('.aorasp-table tr[data-volref]:nth-child(1) td:nth-child(2)'); await sleep(300);
  console.log('hover riga Z1 -> pesi', JSON.stringify(await p.evaluate(() => Object.values(document.querySelector('.aor-block .aor-leaflet')._secMap).flatMap(e => e.layers).filter(l => l._ref).map(l => l.options.weight))));
  await p.mouse.move(5, 5); await sleep(300);
  console.log('mouse via -> pesi', JSON.stringify(await p.evaluate(() => Object.values(document.querySelector('.aor-block .aor-leaflet')._secMap).flatMap(e => e.layers).filter(l => l._ref).map(l => l.options.weight))));
  // hover poligono Z3 -> riga evidenziata + tooltip
  await p.evaluate(() => { const l = Object.values(document.querySelector('.aor-block .aor-leaflet')._secMap).flatMap(e => e.layers).find(x => x._ref && x._ref.includes('Z3')); l.fire('mouseover'); });
  await sleep(200);
  console.log('hover poligono Z3 -> righe hl', JSON.stringify(await p.evaluate(() => [...document.querySelectorAll('tr.aorasp-hl')].map(t => t.cells[1].innerText.trim()))),
    'tooltip', JSON.stringify(await p.evaluate(() => { const l = Object.values(document.querySelector('.aor-block .aor-leaflet')._secMap).flatMap(e => e.layers).find(x => x._ref && x._ref.includes('Z3')); return l.getTooltip() && l.getTooltip().getContent(); })));
  await p.evaluate(() => { const l = Object.values(document.querySelector('.aor-block .aor-leaflet')._secMap).flatMap(e => e.layers).find(x => x._ref && x._ref.includes('Z3')); l.fire('mouseout'); });
  await p.evaluate(() => document.querySelector('[data-aorasp]').scrollIntoView({ block: 'end' }));
  await p.evaluate(() => document.querySelector('.aor-block .aor-leaflet').scrollIntoView({ block: 'start' }));
  await sleep(600);
  await p.screenshot({ path: __dirname + '/spazi-2d.png' });
  // 3D: apri il tab
  await p.evaluate(() => document.querySelector('[data-aor-view] [data-view="3d"]').click()); await sleep(6000);
  const s3 = () => p.evaluate(() => {
    const st = document.querySelector('.aor3d-stage');
    return { init: st && st.dataset.init, haSetVol: !!(st && st._aorSetVol) };
  });
  console.log('3D', JSON.stringify(await s3()));
  // riaccendi Z2 dalla tabella mentre il 3D e' aperto
  await vis(1); await sleep(500);
  console.log('3D prismi visibili', JSON.stringify(await p.evaluate(() => { const st = document.querySelector('.aor3d-stage'); return st && st._aor3dCanvas ? 'canvas ok' : 'no canvas'; })));
  await p.screenshot({ path: __dirname + '/spazi-3d.png' });
  console.log('righe dopo', JSON.stringify((await stato()).righe));
  console.log('ERRORI', JSON.stringify(errs.slice(0, 10)));
  await b.close();
})().catch(e => { console.error('FATALE', e); process.exit(1); });
