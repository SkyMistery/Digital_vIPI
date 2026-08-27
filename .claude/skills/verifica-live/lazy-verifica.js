// Prova che i quattro moduli pesanti arrivino SOLO dove servono, e che dove servono arrivino davvero.
const puppeteer = require('puppeteer-core');
const BASE = 'http://127.0.0.1:5360';
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';

const PESANTI = ['vipi-aor.js', 'vipi-mva.js', 'vipi-aor3d.js', 'vipi-tour.js', 'leaflet.js', 'three.min.js'];

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  let rotti = 0;

  async function apri(percorso, attesi, vietati, etichetta) {
    const page = await browser.newPage();
    const scaricati = new Set(); const errori = [];
    page.on('request', r => { const u = r.url(); PESANTI.forEach(p => { if (u.includes(p)) scaricati.add(p); }); });
    page.on('pageerror', e => errori.push(String(e)));
    page.on('console', m => { if (m.type() === 'error') errori.push(m.text()); });

    await page.goto(BASE + percorso, { waitUntil: 'networkidle2', timeout: 60000 });
    await new Promise(r => setTimeout(r, 2500));

    const mancanti = attesi.filter(a => !scaricati.has(a));
    const intrusi  = vietati.filter(v => scaricati.has(v));
    const ok = mancanti.length === 0 && intrusi.length === 0 && errori.length === 0;
    if (!ok) rotti++;
    console.log(`${ok ? 'OK  ' : 'ROTTO'} ${etichetta}`);
    console.log(`      scaricati: ${[...scaricati].join(', ') || '(nessuno)'}`);
    if (mancanti.length) console.log(`      MANCANO: ${mancanti.join(', ')}`);
    if (intrusi.length)  console.log(`      DI TROPPO: ${intrusi.join(', ')}`);
    if (errori.length)   console.log(`      ERRORI JS: ${errori.slice(0,3).join(' | ')}`);
    return page;
  }

  // 1. Una pagina senza mappe: nessuno dei quattro deve arrivare.
  let p = await apri('/services/vsop/guide', [], PESANTI, 'guida (nessuna mappa)');
  await p.close();

  // 2. La home: idem.
  p = await apri('/services/vsop', [], PESANTI, 'hub');
  await p.close();

  // 3. Il documento ACC: mappe, chip e tabelle di configurazione -> serve vipi-aor.js, e con lui Leaflet.
  p = await apri('/services/vsop/libb/vipi', ['vipi-aor.js', 'leaflet.js'], ['vipi-tour.js'], 'vIPI ACC (mappe)');
  const mappa = await p.evaluate(() => ({
    leaflet: typeof window.L !== 'undefined',
    init: typeof window.vipiInitAor === 'function',
    tessere: document.querySelectorAll('.leaflet-tile, .leaflet-container').length,
    poligoni: document.querySelectorAll('.leaflet-overlay-pane path, svg path').length,
    chipAttive: document.querySelectorAll('.aor-chip').length
  }));
  console.log('      DOM mappa:', JSON.stringify(mappa));
  if (!mappa.leaflet || !mappa.init || mappa.tessere === 0) { console.log('      ROTTO: la mappa non si e\' montata'); rotti++; }
  await p.close();

  // 4. IL CASO DIFFICILE: navigazione «enhanced» da una pagina senza mappa a una con la mappa.
  //    E' il momento in cui un caricamento condizionale sbagliato non fa niente e non lo dice.
  {
    const page = await browser.newPage();
    const scaricati = new Set(); const errori = [];
    page.on('request', r => { const u = r.url(); PESANTI.forEach(x => { if (u.includes(x)) scaricati.add(x); }); });
    page.on('pageerror', e => errori.push(String(e)));
    await page.goto(BASE + '/services/vsop/guide', { waitUntil: 'networkidle2', timeout: 60000 });
    await new Promise(r => setTimeout(r, 1200));
    const primaDi = [...scaricati];
    await page.evaluate(() => { window.location.assign('/services/vsop/libb/vipi'); });
    await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 60000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 3000));
    const dopo = await page.evaluate(() => ({
      leaflet: typeof window.L !== 'undefined',
      contenitori: document.querySelectorAll('.leaflet-container').length
    }));
    const ok = primaDi.length === 0 && scaricati.has('vipi-aor.js') && dopo.leaflet && dopo.contenitori > 0 && errori.length === 0;
    if (!ok) rotti++;
    console.log(`${ok ? 'OK  ' : 'ROTTO'} guida -> vIPI ACC (il modulo arriva dopo)`);
    console.log(`      prima: ${primaDi.join(', ') || '(nessuno)'}  |  dopo: ${[...scaricati].join(', ')}  |  ${JSON.stringify(dopo)}`);
    if (errori.length) console.log(`      ERRORI JS: ${errori.slice(0,3).join(' | ')}`);
    await page.close();
  }

  await browser.close();
  console.log(rotti === 0 ? '\nTUTTO A POSTO' : `\n${rotti} PROVE ROTTE`);
  process.exit(rotti === 0 ? 0 : 1);
})();
