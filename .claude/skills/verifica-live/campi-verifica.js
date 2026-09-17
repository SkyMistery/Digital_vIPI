// Censimento dei campi nei 5 editor: stile CALCOLATO di ogni input/select/textarea visibile in modifica, confrontato con
// il campo di casa (`.app-in` / `.app-ta`). Stampa i gruppi di firme e, per ogni firma diversa, dove sta.
const puppeteer = require('puppeteer-core');
const BASE = process.env.BASE || 'http://localhost:5199';
const TEMA = process.env.TEMA || 'dark';
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const EDITORI = (process.env.EDITORI || [
  'ACC|/services/vsop/libb/editor',
  'APP|/services/vsop/libb/apps/editor?app=LIBP_APP',
  'AEROPORTO|/services/vsop/libb/airports/editor?icao=LIBP',
  'MIL|/services/vsop/lirr/mil/editor?icao=LIBA',
  'VLOA|VLOA',
].join(';')).split(';').map(s => s.split('|'));

(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: 'new', defaultViewport: { width: 1500, height: 1200 } });
  const p = await b.newPage();
  p.on('dialog', d => d.dismiss());
  await p.emulateMediaFeatures([{ name: 'prefers-color-scheme', value: TEMA }]);
  const tutti = [];
  for (let [nome, path] of EDITORI) {
    if (path === 'VLOA') {
      await p.goto(BASE + '/services/vsop/lirr/vloa', { waitUntil: 'networkidle2', timeout: 180000 });
      await sleep(2500);
      const card = await p.evaluate(() => { const a = document.querySelector('a.apt-card[href*="vloa?acc="]'); return a ? new URL(a.href).search.replace('?acc=', '') : null; });
      path = card ? '/services/vsop/lirr/vloa/editor?acc=' + card : null;
      if (!path) { console.log('VLOA: nessun link editor'); continue; }
    }
    await p.goto(BASE + path, { waitUntil: 'networkidle2', timeout: 180000 });
    await p.waitForFunction(() => !!window.Blazor, { timeout: 120000 }); await sleep(3500);
    const ed = await p.evaluate(() => {
      const btn = [...document.querySelectorAll('button')].find(x => /(^|\s)(Modifica|Edit)\s*$/.test(x.innerText.trim()));
      if (btn) btn.click(); return !!btn;
    });
    await sleep(4000);
    // apri tutto quel che si apre
    for (let k = 0; k < 3; k++) {
      await p.evaluate(() => document.querySelectorAll('details:not([open])').forEach(d => d.open = true));
      await sleep(800);
    }
    const campi = await p.evaluate((nome) => {
      const out = [];
      const vis = (el) => { const r = el.getBoundingClientRect(); const cs = getComputedStyle(el); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none'; };
      document.querySelectorAll('input, select, textarea').forEach(el => {
        const t = (el.type || '').toLowerCase();
        if (['hidden', 'checkbox', 'radio', 'file', 'range', 'color', 'submit', 'button'].includes(t)) return;
        if (!vis(el)) return;
        if (el.closest('.top-search, .searchbar, header, .aor3d-wrap, .leaflet-container')) return;
        const cs = getComputedStyle(el);
        const firma = [cs.borderTopWidth, cs.borderTopStyle, cs.borderTopColor, cs.borderTopLeftRadius, cs.backgroundColor, cs.color, cs.fontFamily.split(',')[0], cs.fontSize, el.tagName === 'SELECT' ? cs.appearance : ''].join(' | ');
        // il titolo più vicino risalendo: sezione dell'editor, poi h2/h3/h4 che precede
        let sez = null, n = el;
        while (n && !sez) { let q = n.previousElementSibling; while (q && !sez) { const h = q.matches('h1,h2,h3,h4,.h-card,.dse-title,summary') ? q : q.querySelector && q.querySelector('h2,h3,h4,.h-card,summary'); if (h && h.innerText.trim()) sez = { id: h.innerText.trim().slice(0, 40) }; q = q.previousElementSibling; } n = n.parentElement; }
        const tr = el.closest('table');
        out.push({
          ed: nome, tag: el.tagName.toLowerCase() + (t && el.tagName === 'INPUT' ? '[' + t + ']' : ''),
          cls: el.className || '(nessuna)', firma,
          dove: (sez ? sez.id : '?') + (tr ? ' · table.' + [...tr.classList].join('.') : '') + ' · parent.' + [...el.parentElement.classList].slice(0, 3).join('.') + ' · gp.' + [...(el.parentElement.parentElement || el).classList].slice(0, 3).join('.'),
          ph: (el.getAttribute('placeholder') || el.getAttribute('aria-label') || '').slice(0, 40),
        });
      });
      return out;
    }, nome);
    console.log(`${nome}: modifica=${ed} campi=${campi.length} (${path})`);
    tutti.push(...campi);
  }
  // riferimento: firma di .app-in e .app-ta
  const rif = new Set(tutti.filter(c => /\bapp-(in|ta)\b/.test(c.cls)).map(c => c.firma));
  console.log('\nFIRME DI RIFERIMENTO (.app-in/.app-ta):'); rif.forEach(f => console.log('  ' + f));
  const gruppi = {};
  tutti.forEach(c => { const k = c.firma; (gruppi[k] = gruppi[k] || []).push(c); });
  console.log('\nFIRME DIVERSE:');
  Object.entries(gruppi).filter(([f]) => !rif.has(f)).forEach(([f, cs]) => {
    console.log('\n## ' + f + '  (' + cs.length + ')');
    const visti = new Set();
    cs.forEach(c => { const k = c.ed + c.tag + c.cls + c.dove; if (visti.has(k)) return; visti.add(k); console.log(`  ${c.ed} ${c.tag}.${c.cls} @ ${c.dove} «${c.ph}»`); });
  });
  await b.close();
})().catch(e => { console.error('FATALE', e); process.exit(1); });
