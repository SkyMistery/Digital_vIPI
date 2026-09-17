// Vista rapida (live) ed elenco aeroporti: la pista consigliata di LIBD con le caselle vive TUTTE escluse. Se compare
// una pista, i pannelli leggono il pubblicato (IPisteDalPubblicato); dai vivi uscirebbe «—».
const puppeteer = require('puppeteer-core');
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: 'new', defaultViewport: { width: 1500, height: 1200 } });
  const p = await b.newPage(); const errs = [];
  p.on('pageerror', e => errs.push('pageerror ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push('console ' + m.text()); });

  await p.goto(BASE + '/services/vsop/libb/airports', { waitUntil: 'networkidle2', timeout: 180000 });
  await p.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(12000);   // il meteo arriva dopo
  console.log('ELENCO LIBD:', JSON.stringify(await p.evaluate(() => {
    const card = [...document.querySelectorAll('a, .apt-card, .choice')].find(c => /LIBD/.test(c.innerText));
    return card ? card.innerText.replace(/\s+/g, ' ').slice(0, 220) : null;
  })));

  await p.goto(BASE + '/services/vsop/live/LIBD_TWR', { waitUntil: 'networkidle2', timeout: 180000 });
  await p.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(10000);
  console.log('VISTA RAPIDA:', JSON.stringify(await p.evaluate(() => {
    const d = [...document.querySelectorAll('details.acc-item')].find(x => /LIBD/.test(x.innerText) && x.querySelector('.body'));
    if (!d) return null;
    return d.innerText.replace(/\s+/g, ' ').match(/.{0,60}(RWY|Runway|Pista|DEP|Departures|Partenze).{0,80}/gi)?.slice(0, 4);
  })));
  console.log('ERRORI', JSON.stringify(errs.slice(0, 6)));
  await b.close();
})().catch(e => { console.error('FATALE', e); process.exit(1); });
