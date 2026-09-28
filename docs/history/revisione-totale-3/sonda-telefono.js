// Allegato del lotto L9 (sito S23). Si lancia da una cartella con puppeteer-core, app locale su VIPI_BASE
// (default http://127.0.0.1:5199, come da skill verifica-live). Da Git Bash: MSYS_NO_PATHCONV=1, o i percorsi
// «/services/...» diventano percorsi di Windows.
// Sonda L9 (revisione 3): stessa misura prima e dopo, su telefono e tablet VERI (isMobile + hasTouch).
//   node sonda-l9.js <uscita.json> [filtro-pagina] [larghezze separate da virgola]
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const BASE = process.env.VIPI_BASE || 'http://127.0.0.1:5199';
const sleep = ms => new Promise(r => setTimeout(r, ms));

const PAGINE = [
  '/services', '/services/vsop', '/services/vsop/libb', '/services/vsop/libb/vipi?as=draft',
  '/services/vsop/libb/airports?icao=LIBD&as=draft',
  '/services/vsop/libb/apps', '/services/vsop/libb/vloa', '/services/vsop/live', '/services/vsop/mil',
  '/services/vsop/libb/mil?icao=LIBV&as=draft', '/services/vsop/search?q=LIBD', '/services/vsop/changed',
  '/services/vsop/versions', '/services/vsop/airspace', '/services/vsop/screens', '/services/vsop/guide',
  '/services/stats', '/services/stats/division', '/services/stats/user/704798', '/services/stats/world',
  '/services/vawos', '/services/vawos/LIRF', '/services/coordinates', '/services/profile-swapper',
  '/services/vsop/libb/airports/editor?icao=LIBD', '/services/vsop/libb/mil/editor?icao=LIBV',
];
const ASSETTI = { 360: 780, 375: 812, 390: 844, 768: 1024, 1024: 768 };

function MISURA() {
  const W = window.screen.width, H = window.innerHeight;
  const vis = e => { const r = e.getBoundingClientRect(); const cs = getComputedStyle(e);
    return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none' && !e.closest('[hidden],details:not([open]) > :not(summary)'); };
  const sig = e => e.tagName.toLowerCase() + (e.className && typeof e.className === 'string' ? '.' + e.className.trim().split(/\s+/).slice(0, 2).join('.') : '');
  const scorreSopra = e => { for (let a = e.parentElement; a && a !== document.body; a = a.parentElement) {
    const ox = getComputedStyle(a).overflowX; if (ox === 'auto' || ox === 'scroll' || ox === 'hidden' || ox === 'clip') return true; } return false; };

  // 1. pagina più larga dello schermo
  const largo = window.innerWidth;
  const colpevoli = [];
  for (const e of document.querySelectorAll('body *')) {
    const r = e.getBoundingClientRect();
    if (r.right > largo + 1 && r.width > 0 && !scorreSopra(e) && getComputedStyle(e).position !== 'fixed') colpevoli.push(sig(e) + '@' + Math.round(r.right));
  }
  // 2. parole spezzate (>= 4 caratteri su più righe)
  const spezzate = []; let nSpez = 0;
  const tw = document.createTreeWalker(document.querySelector('main') || document.body, NodeFilter.SHOW_TEXT);
  for (let n; (n = tw.nextNode());) {
    const el = n.parentElement; if (!el || !vis(el) || el.closest('script,style,textarea,.leaflet-container,canvas,svg')) continue;
    const re = /\S{4,}/g; let m;
    while ((m = re.exec(n.data))) {
      const rg = document.createRange(); rg.setStart(n, m.index); rg.setEnd(n, m.index + m[0].length);
      const tops = new Set([...rg.getClientRects()].filter(r => r.width > 0).map(r => Math.round(r.top)));
      if (tops.size > 1) { nSpez++; if (spezzate.length < 6) spezzate.push(m[0].slice(0, 20) + '<' + sig(el) + '>'); }
    }
  }
  // 3. bersagli al tocco < 24x24 (fuori dai link dentro una frase)
  const piccoli = {}; let nPic = 0;
  for (const e of document.querySelectorAll('a[href],button,summary,input:not([type=hidden]),select,[role=button]')) {
    if (!vis(e)) continue;
    if (e.classList.contains('skip-link')) continue;
    let r = e.getBoundingClientRect();
    const pa = getComputedStyle(e, '::after');
    if (pa.content !== 'none' && pa.position === 'absolute') { const t = -parseFloat(pa.top) || 0, l = -parseFloat(pa.left) || 0;
      r = { width: r.width + 2 * Math.max(0, l), height: r.height + 2 * Math.max(0, t) }; }
    const lab = e.type === 'checkbox' && e.closest('label');
    if (lab) { const q = lab.getBoundingClientRect(); if (q.width >= 24 && q.height >= 24) continue; }
    if (r.width >= 24 && r.height >= 24) continue;
    const cs = getComputedStyle(e);
    if (e.tagName === 'A' && cs.display === 'inline' && e.parentElement && e.parentElement.textContent.trim().length > e.textContent.trim().length + 15) continue;
    if (e.closest('.leaflet-control-attribution,.doc-body p')) continue;
    nPic++; const k = sig(e); piccoli[k] = (piccoli[k] || '') ? piccoli[k] : Math.round(r.width) + 'x' + Math.round(r.height);
  }
  // 4. campi sotto 16px (iOS ingrandisce al fuoco)
  const campi = [];
  for (const e of document.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=range]):not([type=file]),select,textarea')) {
    if (!vis(e)) continue; const fs = parseFloat(getComputedStyle(e).fontSize); if (fs < 16) campi.push(sig(e) + ' ' + fs);
  }
  // 5. fasce appiccicate / fisse alte
  const fisse = [];
  for (const e of document.querySelectorAll('body *')) {
    const p = getComputedStyle(e).position; if (p !== 'sticky' && p !== 'fixed') continue; if (!vis(e)) continue;
    const r = e.getBoundingClientRect(); if (r.height > H * 0.2) fisse.push(sig(e) + ' ' + Math.round(r.height) + '/' + H);
  }
  // 6. dito intrappolato: touch-action none alto
  const trappole = [];
  for (const e of document.querySelectorAll('.leaflet-container,.aor3d-stage,canvas,[class*=mva]')) {
    if (!vis(e)) continue; const ta = getComputedStyle(e).touchAction; const r = e.getBoundingClientRect();
    if ((ta === 'none' || ta === 'pinch-zoom') && r.width > W * 0.8) trappole.push(sig(e) + ' ' + ta + ' h' + Math.round(r.height));
  }
  // 7. sovrapposizioni note
  const sopra = [];
  const tocca = (a, b) => { const x = a.getBoundingClientRect(), y = b.getBoundingClientRect(); return x.left < y.right && y.left < x.right && x.top < y.bottom && y.top < x.bottom; };
  const airac = document.querySelector('.hero .airac'), h1 = document.querySelector('.hero h1');
  if (airac && h1 && vis(airac) && tocca(airac, h1)) sopra.push('airac/h1');
  for (const c of document.querySelectorAll('.acc-card')) { const o = c.querySelector('.onl'); if (!o || !vis(o)) continue;
    for (const t of c.querySelectorAll(':scope > :not(.onl)')) if (vis(t) && tocca(o, t)) { sopra.push('acc-card .onl/' + sig(t)); break; } }
  const mask = document.querySelector('.awos-mask'), barra = document.querySelector('.awos-barra');
  if (mask && barra && vis(mask) && tocca(mask, barra)) sopra.push('awos-mask/barra');
  // 8. versioni negli editor
  const vact = [...document.querySelectorAll('span.vact')].filter(vis).map(e => Math.round(e.getBoundingClientRect().right)).filter(r => r > largo);
  return { W, largo, eccesso: largo - W, colpevoli: colpevoli.slice(0, 5), nColp: colpevoli.length, nSpez, spezzate, nPic, piccoli, campi: [...new Set(campi)], fisse, trappole, sopra, vact };
}

(async () => {
  const out = process.argv[2] || 'sonda.json';
  const filtro = process.argv[3] || '';
  const larghezze = (process.argv[4] || '360,375,390,768,1024').split(',').map(Number);
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new' });
  const risultati = {};
  for (const pagina of PAGINE.filter(p => p.includes(filtro))) {
    for (const w of larghezze) {
      const p = await b.newPage();
      await p.setViewport({ width: w, height: ASSETTI[w] || 800, isMobile: true, hasTouch: true, deviceScaleFactor: 2 });
      let esito;
      try {
        const r = await p.goto(BASE + pagina, { waitUntil: 'domcontentloaded', timeout: 60000 });
        await p.waitForFunction(() => !!window.Blazor, { timeout: 60000 }).catch(() => {});
        await sleep(2500);
        esito = { stato: r.status(), ...(await p.evaluate(MISURA)) };
        if (process.env.FOTO) await p.screenshot({ path: `foto/${pagina.replace(/[^a-z0-9]+/gi, '_')}_${w}.png`, fullPage: false });
      } catch (e) { esito = { errore: String(e).slice(0, 200) }; }
      risultati[pagina + ' @' + w] = esito;
      const e = esito;
      console.log(`${pagina} @${w}: ${e.errore || `st${e.stato} ecc${e.eccesso} colp${e.nColp} spez${e.nSpez} pic${e.nPic} campi${e.campi.length} fisse${e.fisse.length} trap${e.trappole.length} sopra${e.sopra.length} vact${e.vact.length}`}`);
      await p.close();
    }
  }
  fs.writeFileSync(out, JSON.stringify(risultati, null, 1));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
