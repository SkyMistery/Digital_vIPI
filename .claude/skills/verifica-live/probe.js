// Sonda di CONTRASTO. Per ogni elemento con testo, il rapporto fra il suo colore e il primo fondo
// davvero dipinto risalendo gli antenati. Elenca quello che sta sotto 4.5:1 (AA, testo normale).
//
//   node probe.js light http://localhost:5034/vsop/lirr
//   node probe.js dark  http://localhost:5034/vsop/admin/struttura
//
// ⚠️ NON compone l'ALFA: il testo su un velo semitrasparente (i tasti della barra blu) esce come falso
// positivo con rapporti assurdi tipo 1:1. Quei casi si ricalcolano a mano componendo il velo sul fondo —
// sulla barra stanno tutti fra 6 e 8:1.
// ⚠️ Salta i fondi a GRADIENTE: non sono un colore piatto e non sono giudicabili cosi'.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const scheme = process.argv[2] || 'dark';
const url = process.argv[3] || 'http://localhost:5034/vsop/lirr';

const FIND = () => {
  // ⚠️ color-mix() non si serializza in rgb(): getComputedStyle restituisce `color(srgb 0.95 0.95 0.97)`,
  // con i canali fra 0 e 1. Leggerli come 0-255 fa uscire luminanza ~0 e contrasti inventati.
  const lum = (c) => {
    const srgb = /^color\(srgb/.test(c);
    const m = c.match(/-?[\d.]+(?:e-?\d+)?/g).map(Number);
    let ch = m.slice(0, 3);
    if (!srgb) ch = ch.map((v) => v / 255);
    const f = (v) => (v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4));
    return 0.2126 * f(ch[0]) + 0.7152 * f(ch[1]) + 0.0722 * f(ch[2]);
  };
  const bgOf = (el) => {
    let e = el;
    while (e) {
      const c = getComputedStyle(e).backgroundColor;
      const bi = getComputedStyle(e).backgroundImage;
      if (bi && bi !== 'none') return '#GRAD';   // un gradiente non e' un colore piatto: non giudicabile
      if (c && !/rgba\(0, 0, 0, 0\)|transparent/.test(c)) return c;
      e = e.parentElement;
    }
    return 'rgb(255,255,255)';
  };
  const out = [];
  document.querySelectorAll('.vipi-root *').forEach((el) => {
    const t = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent.trim()).join('').trim();
    if (!t || t.length < 2) return;
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || cs.visibility === 'hidden' || cs.opacity === '0') return;
    if (!el.getClientRects().length) return;
    try {
      const bgv = bgOf(el);
      if (bgv === '#GRAD') return;
      const a = lum(cs.color), c = lum(bgv);
      const hi = Math.max(a, c), lo = Math.min(a, c);
      const r = (hi + 0.05) / (lo + 0.05);
      if (r < 4.5) out.push({ t: t.slice(0, 40), cls: (el.className || '').toString().slice(0, 46), fg: cs.color, bg: bgv, r: +r.toFixed(2), px: cs.fontSize });
    } catch (e) { /* colori non rgb (color(srgb ...)): saltati */ }
  });
  return out.sort((x, y) => x.r - y.r).slice(0, 25);
};

(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'], defaultViewport: { width: 1600, height: 1200 } });
  const p = await b.newPage();
  await p.emulateMediaFeatures([{ name: 'prefers-color-scheme', value: scheme }]);
  await p.goto(url, { waitUntil: 'networkidle2' });
  await new Promise((r) => setTimeout(r, 1200));
  const bad = await p.evaluate(FIND);
  console.log(`--- ${scheme} --- ${url}`);
  if (!bad.length) console.log('  niente sotto 4.5:1');
  bad.forEach((x) => console.log(`  ${String(x.r).padStart(5)}:1  ${x.px.padStart(7)}  ${x.t.padEnd(42)} .${x.cls}`));
  await b.close();
})();
