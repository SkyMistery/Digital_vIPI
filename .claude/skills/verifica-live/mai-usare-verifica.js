// Sonda «mai usare» (carta 2026-09-17-pista-mai-usare.md): editor aeroporto, colonna nella tabella piste, salvataggio,
// banco di prova che ripiega senza la soglia marcata.
const puppeteer = require('puppeteer-core');
const BASE = process.env.BASE || 'http://localhost:5199';
const PATH = process.env.DOC || '/services/vsop/libb/airports/editor?icao=LIBD';
const SOGLIA = process.env.SOGLIA || '07';
const VENTO = +(process.env.VENTO || 70);
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: 'new', defaultViewport: { width: 1500, height: 1200 } });
  const p = await b.newPage(); const errs = [];
  p.on('pageerror', e => errs.push('pageerror ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push('console ' + m.text()); });
  p.on('dialog', d => d.accept());
  const apri = async () => {
    await p.goto(BASE + PATH, { waitUntil: 'networkidle2', timeout: 180000 });
    await p.waitForFunction(() => !!window.Blazor, { timeout: 120000 }); await sleep(3000);
    await p.evaluate(() => { const x = [...document.querySelectorAll('button')].find(b => /(^|\s)(Modifica|Edit)\s*$/.test(b.innerText.trim())); x && x.click(); });
    await sleep(4000);
    await p.evaluate(() => document.querySelectorAll('details:not([open])').forEach(d => { if (!d.closest('.help-hint, .hh')) d.open = true; }));
    await sleep(1500);
  };
  const casella = (s) => p.evaluate((s) => {
    const t = [...document.querySelectorAll('table')].find(t => [...t.querySelectorAll('th')].some(th => /never use|mai usare/i.test(th.innerText)));
    if (!t) return 'tabella non trovata';
    const tr = [...t.querySelectorAll('tbody tr')].find(tr => (tr.cells[0].querySelector('input')?.value || tr.cells[0].innerText).trim().split(/s/)[0] === s);
    const cb = tr && tr.querySelector('td.col-mai input[type=checkbox]');
    return cb ? cb.checked : 'casella non trovata';
  }, s);
  await apri();
  console.log('prima', SOGLIA, await casella(SOGLIA));
  const banco = async () => {
    await p.evaluate((v) => {
      const n = [...document.querySelectorAll('input[type=number][max="360"]')][0];
      const k = [...document.querySelectorAll('input[type=number][max="99"]')][0];
      for (const [el, val] of [[n, v], [k, 12]]) { el.value = String(val); el.dispatchEvent(new Event('change', { bubbles: true })); }
    }, VENTO);
    await sleep(600);
    await p.evaluate(() => { const x = [...document.querySelectorAll('button')].find(b => /▷/.test(b.innerText)); x && x.click(); });
    await sleep(1000);
    return p.evaluate(() => { const c = [...document.querySelectorAll('.callout')].find(c => /DEP|No rule|Nessuna regola|runway|pista/i.test(c.innerText) && c.closest('.block')); return c ? c.innerText.replace(/\s+/g, ' ').slice(0, 200) : null; });
  };
  console.log('banco prima:', await banco());
  // spunta la casella
  await p.evaluate((s) => {
    const t = [...document.querySelectorAll('table')].find(t => [...t.querySelectorAll('th')].some(th => /never use|mai usare/i.test(th.innerText)));
    const tr = [...t.querySelectorAll('tbody tr')].find(tr => (tr.cells[0].querySelector('input')?.value || tr.cells[0].innerText).trim().split(/s/)[0] === s);
    tr.querySelector('td.col-mai input[type=checkbox]').click();
  }, SOGLIA);
  await sleep(2500);
  console.log('dopo clic', SOGLIA, await casella(SOGLIA));
  console.log('banco dopo:', await banco());
  console.log('avvisi regole:', JSON.stringify(await p.evaluate(() => [...document.querySelectorAll('.callout.warning')].map(c => c.innerText.replace(/\s+/g, ' ').slice(0, 160)).filter(t => /never|mai/i.test(t)))));
  await apri();
  console.log('dopo ricarico', SOGLIA, await casella(SOGLIA));
  console.log('ERRORI', JSON.stringify(errs.slice(0, 8)));
  await b.close();
})().catch(e => { console.error('FATALE', e); process.exit(1); });
