// Le sezioni «STAR» degli aeroporti nascono NASCOSTE (21 settembre 2026, committente): nella vista del documento
// non ci sono, nell'editor sì, marcate nascoste e riaccendibili.
//   node star-nascoste-verifica.js                 LIBB/LIBD su :5034
//   (da PowerShell se si passano percorsi: Git Bash li traduce)
const puppeteer = require('puppeteer-core');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5034';
const ACC = process.env.ACC || 'libb', ICAO = process.env.ICAO || 'LIBD';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const esiti = [];
  const nota = (n, ok, d) => { esiti.push(ok); console.log(`${ok ? 'OK  ' : 'ROSSO'} ${n} — ${d}`); };
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new' });
  const p = await b.newPage();
  const apri = async (u) => {
    await p.goto(BASE + u, { waitUntil: 'networkidle2', timeout: 120000 });
    await p.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
    await sleep(3000);
  };
  const testate = () => p.$$eval('h1,h2,h3,h4,.toc a,nav a', (els) => els.map((e) => e.textContent.trim()).filter(Boolean));
  try {
    await apri(`/services/vsop/${ACC}/airports?icao=${ICAO}&as=draft`);
    const vista = await testate();
    nota('vista: c\'è la sezione SID', vista.some((t) => /^SID\b/.test(t)), vista.filter((t) => /SID|STAR/.test(t)).join(' | '));
    // ⚠️ Chi ha i permessi d'editore vede in bozza ANCHE le sezioni nascoste, col segno «🚫 nascosta (non
    // pubblica)»: la testata della STAR deve portarlo, quella della SID no. «SID | STAR» senza segno sono le chip.
    const testataStar = vista.filter((t) => /^STAR.+/.test(t));
    nota('vista: la testata STAR porta il segno «nascosta»', testataStar.length > 0 && testataStar.every((t) => t.includes('🚫')),
         testataStar.join(' | ') || 'nessuna testata STAR');
    nota('vista: la SID non è nascosta', !vista.some((t) => /^SID.*🚫/.test(t)), 'nessun segno sulla SID');

    await apri(`/services/vsop/${ACC}/airports/editor?icao=${ICAO}`);
    const riga = await p.evaluate(() => {
      const el = [...document.querySelectorAll('*')].find((e) => e.children.length === 0 && e.textContent.trim() === 'STAR'
        && e.closest('[data-section-key="stars"], .se-row, li, section'));
      const box = el && el.closest('[data-section-key], .se-row, li, section');
      return box ? box.outerHTML.slice(0, 600) : null;
    });
    nota('editor: la sezione STAR c\'è', !!riga, riga ? riga.replace(/\s+/g, ' ').slice(0, 300) : 'non trovata');
  } catch (e) { nota('giro', false, e.message); }
  await b.close();
  console.log(esiti.every(Boolean) ? `\nVERDE ${esiti.length}/${esiti.length}` : `\nROSSO`);
  process.exit(esiti.every(Boolean) ? 0 : 1);
})();
