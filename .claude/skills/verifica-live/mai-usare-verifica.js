// Sonda «mai in partenza / mai in arrivo» (carta 2026-09-17-pista-mai-usare.md, §A68):
// 1) editor: casella DEP di una soglia, banco di prova che la tiene per gli arrivi e la toglie dalle partenze;
// 2) vAWOS col METAR di prova: la casella NON pubblicata non cambia il quadro; dopo «Publish now» sì.
// ⚠️ Scrive nel DB e pubblica: solo su una copia.
const puppeteer = require('puppeteer-core');
const BASE = process.env.BASE || 'http://localhost:5199';
const ACC = process.env.ACC || 'libb', ICAO = process.env.ICAO || 'LIBD', SOGLIA = process.env.SOGLIA || '07';
const VENTO = +(process.env.VENTO || 70);
if (/ivao\.aero/.test(BASE)) { console.error('solo su una copia'); process.exit(2); }
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: 'new', defaultViewport: { width: 1500, height: 1200 } });
  const p = await b.newPage(); const errs = [];
  p.on('pageerror', e => errs.push('pageerror ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push('console ' + m.text()); });
  p.on('dialog', d => d.accept());
  const pad = (n) => String(n).padStart(3, '0');
  const metar = `${ICAO} 171250Z ${pad(VENTO)}12KT 9999 FEW030 20/10 Q1015`;

  const awos = async () => {
    await p.goto(`${BASE}/services/vawos/${ICAO}?test=${encodeURIComponent(metar)}`, { waitUntil: 'networkidle2', timeout: 180000 });
    await sleep(3000);
    return p.evaluate(() => [...document.querySelectorAll('.awos-strip')].map(s => {
      const svg = s.querySelector('svg[data-awos-arrow]');
      const t = [...s.querySelectorAll('.awos-testata')].map(x => x.innerText.trim());
      const verso = svg && /scaleX\(-1\)/.test(svg.getAttribute('style') || svg.style.transform) ? '←' : '→';
      const colore = svg && svg.querySelector('polygon').getAttribute('fill');
      return `${t[0]} ${verso} ${t[1]} (${colore === '#2a8a3a' ? 'in uso' : 'grigia'})`;
    }));
  };
  const editor = async () => {
    await p.goto(`${BASE}/services/vsop/${ACC}/airports/editor?icao=${ICAO}`, { waitUntil: 'networkidle2', timeout: 180000 });
    await p.waitForFunction(() => !!window.Blazor, { timeout: 120000 }); await sleep(3000);
  };
  const modifica = async () => {
    await p.evaluate(() => { const x = [...document.querySelectorAll('button')].find(b => /(^|\s)(Modifica|Edit)\s*$/.test(b.innerText.trim())); x && x.click(); });
    await sleep(4000);
    await p.evaluate(() => document.querySelectorAll('details:not([open])').forEach(d => d.open = true));
    await sleep(1500);
  };
  // Dal 17-set-2026 sono CHIP (pulsanti con aria-pressed), non caselle: rosse (`no`) quando accese.
  const caselle = (s) => p.evaluate((s) => {
    const bs = [...document.querySelectorAll('td.col-mai button.sh-chip')].filter(c => (c.getAttribute('aria-label') || '').startsWith(s + ':'));
    return bs.map(c => (/depart|partenz/i.test(c.getAttribute('aria-label')) ? 'DEP' : 'ARR') + '=' + c.getAttribute('aria-pressed')
      + (c.classList.contains('no') ? '(rossa)' : '')).join(' ');
  }, s);
  const banco = async () => {
    await p.evaluate((v) => {
      const n = document.querySelector('input[type=number][max="360"]'), k = document.querySelector('input[type=number][max="99"]');
      for (const [el, val] of [[n, v], [k, 12]]) { el.value = String(val); el.dispatchEvent(new Event('change', { bubbles: true })); }
    }, VENTO);
    await sleep(600);
    await p.evaluate(() => { const x = [...document.querySelectorAll('button')].find(b => /▷/.test(b.innerText)); x && x.click(); });
    await sleep(1000);
    return p.evaluate(() => { const c = [...document.querySelectorAll('.block .callout')].find(c => /fallback|ripiego/i.test(c.innerText)); return c ? c.innerText.replace(/\s+/g, ' ').slice(0, 160) : null; });
  };

  console.log('vAWOS prima       :', JSON.stringify(await awos()));
  await editor(); await modifica();
  console.log('caselle prima     :', await caselle(SOGLIA));
  console.log('banco prima       :', await banco());
  await p.evaluate((s) => [...document.querySelectorAll('td.col-mai button.sh-chip')]
    .find(c => (c.getAttribute('aria-label') || '').startsWith(s + ':') && /depart|partenz/i.test(c.getAttribute('aria-label'))).click(), SOGLIA);
  await sleep(2500);
  console.log('caselle dopo clic :', await caselle(SOGLIA));
  console.log('banco dopo        :', await banco());
  console.log('vAWOS non pubbl.  :', JSON.stringify(await awos()));
  await editor();
  const pubblicato = await p.evaluate(() => { const x = [...document.querySelectorAll('button')].find(b => /^(Publish now|Pubblica ora)/i.test(b.innerText.trim())); if (x) { x.click(); return true; } return false; });
  await sleep(5000);
  console.log('pubblicato        :', pubblicato);
  console.log('vAWOS pubblicato  :', JSON.stringify(await awos()));
  await editor(); await modifica();
  console.log('caselle ricarico  :', await caselle(SOGLIA));
  console.log('ERRORI', JSON.stringify(errs.slice(0, 8)));
  await b.close();
})().catch(e => { console.error('FATALE', e); process.exit(1); });
