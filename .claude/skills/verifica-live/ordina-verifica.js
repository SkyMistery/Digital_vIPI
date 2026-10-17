// Accordi: l'ordinamento col verso e i tasti di riga (S100, 9 ottobre 2026). Nell'editor dei trasferimenti:
//   1. ALBERO, un accordo aperto: misura la colonna dei tasti di riga (tutti 28x26, nessuno fuori dal bordo,
//      scorrimento laterale 0) e il comando d'ordine in barra e in testata (alto quanto i tasti accanto);
//   2. «Ordina» in barra: punti A->Z e Z->A, quota;
//   3. l'ordine SALVATO della sezione coi punti piu' vari: per punto e per quota nei due versi, poi RICARICA la
//      pagina (il verso deve esserci ancora) e rimette «a mano» (le maniglie di trascinamento tornano);
//   4. ELENCO: mittente, ricevente, tipo, punti, quota nei due versi; un clic su un'intestazione, e la tendina
//      la segue; tornando in albero una chiave solo-elenco cade su «manuale».
// ⚠️ SCRIVE l'ordine di una sezione (e lo rimette com'era): solo su una COPIA, rifiuta ivao.aero.
// ⚠️ Lanciarlo da PowerShell: da Git Bash Edge si sgancia dal processo che lo avvia, puppeteer non lo aggancia
//    («Failed to launch the browser process: Code: 0») e resta acceso un Edge senza testa da fermare a mano.
// node ordina-verifica.js [dark|light] [larghezza] [base]     (default: dark 1860 http://localhost:5199)
// Le foto finiscono accanto allo script: guardarle (SKILL.md §6).
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.argv[4] || 'http://localhost:5199';
if (/ivao\.aero/i.test(BASE)) { console.error('Solo su una copia: questo script scrive.'); process.exit(2); }
const OUT = __dirname;
const TEMA = process.argv[2] || 'dark';
const W = +(process.argv[3] || 1860);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new',
    args: ['--no-sandbox', '--window-size=' + W + ',900'], defaultViewport: { width: W, height: 900 } });
  const page = await browser.newPage();
  await page.setExtraHTTPHeaders({ 'Accept-Language': 'it-IT,it;q=0.9' });
  await page.emulateMediaFeatures([{ name: 'prefers-color-scheme', value: TEMA }]);
  const problemi = [];
  page.on('pageerror', (e) => problemi.push('[pageerror] ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') problemi.push('[console] ' + m.text()); });
  page.on('response', (r) => { if (r.status() >= 400) problemi.push('[http ' + r.status() + '] ' + r.url()); });

  await page.goto(BASE + '/services/vsop/admin/transfers?acc=LIBB', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await page.waitForSelector('.xt-nav', { timeout: 60000 });
  await sleep(2500);
  await page.evaluate(() => { const b = document.querySelector('.lockbar.free .btn.primary'); if (b) b.click(); });
  await sleep(2000);

  // --- ALBERO: scelgo l'accordo con LGGG (quello della foto del committente) ---
  await page.evaluate(() => {
    for (const s of document.querySelectorAll('.xt-nav-sec')) if (s.getAttribute('aria-expanded') === 'false') s.click();
  });
  await sleep(1200);
  const quale = await page.evaluate(() => {
    const flows = [...document.querySelectorAll('.xt-nav-flow')];
    const f = flows.find((b) => /LGGG/.test(b.innerText)) || flows[0];
    if (f) f.click();
    return f ? f.innerText.replace(/\s+/g, ' ') : null;
  });
  await sleep(2000);
  const apriSezioni = () => page.evaluate(() => {
    for (const b of document.querySelectorAll('button.xt-dirtoggle')) if (b.getAttribute('aria-expanded') === 'false') b.click();
  });
  await apriSezioni();
  await sleep(1500);

  const misura = () => page.evaluate(() => {
    const r = (e) => { const b = e.getBoundingClientRect(); return { x: Math.round(b.x), w: Math.round(b.width), h: Math.round(b.height) }; };
    const cs = (e, p) => (e ? getComputedStyle(e)[p] : null);
    const body = document.querySelector('.xt-pane-body');
    const row = document.querySelector('.xfe-table tbody tr');
    const acts = row ? row.querySelector('td.xt-acts') : null;
    const btns = acts ? [...acts.querySelectorAll('button')] : [];
    const chip = (c) => c && {
      ...r(c), fondo: cs(c, 'backgroundColor'), colore: cs(c, 'color'), testo: c.innerText.replace(/\s+/g, ' '),
      voci: [...c.querySelectorAll('option')].map((o) => o.innerText), sel: c.querySelector('select').value,
      selFondo: cs(c.querySelector('select'), 'backgroundColor'), selFont: cs(c.querySelector('select'), 'fontFamily').slice(0, 30),
      dir: c.querySelector('.xt-sortchip-dir').innerText, dirOff: c.querySelector('.xt-sortchip-dir').disabled,
    };
    const tb = document.querySelector('.xt-bar .btn.ghost');
    const head = document.querySelector('.xt-dirhead');
    return {
      scorreDiLato: body ? body.scrollWidth - body.clientWidth : null,
      cellaAzioni: acts ? r(acts) : null,
      tasti: btns.map((b) => r(b).w + 'x' + r(b).h + (b.disabled ? ' spento' : '') + ' fuori:' + Math.round(b.getBoundingClientRect().right - acts.getBoundingClientRect().right)),
      chipBarra: chip(document.querySelector('.xt-bar .xt-sortchip')),
      tastoBarra: tb && { ...r(tb), fondo: cs(tb, 'backgroundColor'), colore: cs(tb, 'color') },
      selectBarra: (() => { const s = document.querySelector('.xt-bar > select'); return s && { ...r(s), fondo: cs(s, 'backgroundColor'), colore: cs(s, 'color') }; })(),
      chipSezione: chip(head && head.querySelector('.xt-sortchip')),
      tastiTestata: head ? [...head.querySelectorAll('.btn')].map((b) => r(b).w + 'x' + r(b).h) : [],
      altezzaTestata: head ? r(head).h : null,
      punti: [...document.querySelectorAll('.xfe-table tbody tr:not(.xfer-prev)')].slice(0, 12).map((t) => t.querySelector('td:nth-child(3)').innerText.replace(/\s+/g, ' ').trim()),
    };
  });

  console.log('ACCORDO:', quale);
  console.log('ALBERO', JSON.stringify(await misura(), null, 1));
  await page.screenshot({ path: OUT + '/albero-' + TEMA + '-' + W + '.png' });

  const scegli = async (sel, valore) => { await page.select(sel, valore); await sleep(1500); };
  let m;
  // --- verso nella BARRA (vista): punti, poi inverti ---
  await scegli('.xt-bar .xt-sortchip select', 'Points');
  m = await misura(); console.log('BARRA punti', m.chipBarra.dir, 'spento=' + m.chipBarra.dirOff, JSON.stringify(m.punti));
  await page.click('.xt-bar .xt-sortchip-dir'); await sleep(1200);
  m = await misura(); console.log('BARRA punti inverso', m.chipBarra.dir, JSON.stringify(m.punti));
  await scegli('.xt-bar .xt-sortchip select', 'Level');
  m = await misura(); console.log('BARRA quota', m.chipBarra.dir, JSON.stringify(m.punti));
  await scegli('.xt-bar .xt-sortchip select', 'Manual');

  // --- ordine SALVATO della sezione: la sezione coi punti più vari ---
  const n = await page.evaluate(() => {
    const secs = [...document.querySelectorAll('section.xt-dirblock')];
    let best = 0, bestN = 0;
    secs.forEach((sec, i) => {
      const pts = new Set([...sec.querySelectorAll('.xfe-table tbody tr:not(.xfer-prev) td:nth-child(3)')].map((t) => t.innerText.trim()));
      if (pts.size > bestN) { bestN = pts.size; best = i; }
    });
    secs[best].setAttribute('data-prova', '1');
    return [...secs[best].parentElement.children].indexOf(secs[best]) + 1;
  });
  const SEC = '.xt-dirblock:nth-child(' + n + ')';
  const righe = () => page.evaluate((SEC) => {
    const sec = document.querySelector(SEC);
    const c = sec.querySelector('.xt-sortchip');
    return { sel: c.querySelector('select').value, dir: c.querySelector('.xt-sortchip-dir').innerText, off: c.querySelector('.xt-sortchip-dir').disabled,
      testa: sec.querySelector('.xt-dirtoggle').innerText.replace(/\s+/g, ' '),
      maniglie: sec.querySelectorAll('.app-drag').length,
      righe: [...sec.querySelectorAll('.xfe-table tbody tr:not(.xfer-prev)')].map((t) => t.querySelector('td:nth-child(3)').innerText.replace(/\s+/g, ' ').trim() + ' @ ' + t.querySelector('td:nth-child(4)').innerText.replace(/\s+/g, ' ').trim()) };
  }, SEC);
  console.log('SEZIONE a mano      ', JSON.stringify(await righe()));
  for (const k of ['Points', 'Level']) {
    await scegli(SEC + ' .xt-sortchip select', k); await apriSezioni(); await sleep(600);
    console.log('SEZIONE ' + k + ' diritto ', JSON.stringify(await righe()));
    await page.click(SEC + ' .xt-sortchip-dir'); await sleep(1800); await apriSezioni(); await sleep(600);
    console.log('SEZIONE ' + k + ' inverso ', JSON.stringify(await righe()));
  }
  await page.screenshot({ path: OUT + '/albero-sezione-inverso-' + TEMA + '-' + W + '.png' });
  // il verso salvato deve sopravvivere a un ricarico della pagina
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await page.waitForSelector('.xt-dirblock', { timeout: 60000 }); await sleep(2500);
  await apriSezioni(); await sleep(1000);
  console.log('DOPO RICARICO       ', JSON.stringify(await righe()));
  await page.evaluate(() => { const b = document.querySelector('.lockbar.free .btn.primary'); if (b) b.click(); });
  await sleep(2000); await apriSezioni(); await sleep(800);
  await scegli(SEC + ' .xt-sortchip select', 'Manual'); await apriSezioni(); await sleep(600);
  console.log('SEZIONE di nuovo a mano', JSON.stringify(await righe()));

  // --- ELENCO ---
  await page.evaluate(() => [...document.querySelectorAll('.xt-viewsw .btn')][1].click());
  await sleep(2500);
  m = await misura();
  console.log('ELENCO', JSON.stringify({ scorre: m.scorreDiLato, cella: m.cellaAzioni, tasti: m.tasti, chip: m.chipBarra }, null, 1));
  await page.screenshot({ path: OUT + '/elenco-' + TEMA + '-' + W + '.png' });
  const colonna = (n) => page.evaluate((n) => [...document.querySelectorAll('.xfe-table tbody tr:not(.xfer-prev)')].slice(0, 8)
    .map((t) => t.querySelector('td:nth-child(' + n + ')').innerText.replace(/\s+/g, ' ').trim()), n);
  const coda = (n) => page.evaluate((n) => [...document.querySelectorAll('.xfe-table tbody tr:not(.xfer-prev)')].slice(-3)
    .map((t) => t.querySelector('td:nth-child(' + n + ')').innerText.replace(/\s+/g, ' ').trim()), n);
  for (const [k, n] of [['Sender', 3], ['Receiver', 4], ['Kind', 6], ['Points', 7], ['Level', 8]]) {
    await scegli('.xt-bar .xt-sortchip select', k);
    const su = await colonna(n), suCoda = await coda(n);
    await page.click('.xt-bar .xt-sortchip-dir'); await sleep(1200);
    const giu = await colonna(n), giuCoda = await coda(n);
    const testa = await page.evaluate(() => { const h = document.querySelector('.xt-sorth.on'); return h ? h.innerText.replace(/\s+/g, ' ') : null; });
    const dir = await page.evaluate(() => document.querySelector('.xt-bar .xt-sortchip-dir').innerText);
    console.log('ELENCO ' + k + '\n   su  ' + JSON.stringify(su) + ' … ' + JSON.stringify(suCoda) + '\n   giu ' + JSON.stringify(giu) + ' … ' + JSON.stringify(giuCoda) + '\n   intestazione: ' + testa + ' | tasto: ' + dir);
  }
  await page.screenshot({ path: OUT + '/elenco-ordinato-' + TEMA + '-' + W + '.png' });
  // clic su un'intestazione: la tendina deve seguirla
  await page.evaluate(() => [...document.querySelectorAll('.xt-sorth')][0].click()); await sleep(1200);
  m = await misura(); console.log('dopo clic intestazione: tendina =', m.chipBarra.sel, m.chipBarra.dir);
  // torno in albero: una chiave solo-elenco deve cadere su «manuale»
  await page.evaluate(() => [...document.querySelectorAll('.xt-viewsw .btn')][0].click()); await sleep(2000);
  await apriSezioni(); await sleep(800);
  m = await misura();
  console.log('tornato in albero: tendina =', m.chipBarra.sel, JSON.stringify(m.chipBarra.voci), '| sezione', m.chipSezione && m.chipSezione.sel, m.chipSezione && m.chipSezione.dir);

  console.log('problemi:', problemi.length ? JSON.stringify(problemi, null, 1) : 'nessuno');
  await browser.close();
})().catch((e) => { console.error('FALLITO', e); process.exit(1); });
