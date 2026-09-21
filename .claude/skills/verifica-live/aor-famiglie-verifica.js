// Settori militari e FSS nelle LORO sezioni della vIPI di ACC (21 settembre 2026). Legge la pagina e dice:
//   - l'ordine delle sezioni del blocco Aerovia (MIL prima delle aree regolamentate, FSS dopo);
//   - i chip di ciascuna mappa AoR: la principale senza *_MIL_* e *_FSS, le due nuove con i soli loro.
//   node aor-famiglie-verifica.js [url]        default: vIPI di LIRR in locale (bozza)
//   EDITOR=1 node aor-famiglie-verifica.js     la stessa lettura sull'editor ACC
// Sola lettura: non scrive niente.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const ACC = process.env.ACC || 'LIRR';
const URL = process.argv[2] || `http://localhost:5034/services/vsop/${ACC.toLowerCase()}/${process.env.EDITOR ? 'editor' : 'vipi'}`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await b.newPage();
  await page.setViewport({ width: 1440, height: 1100 });
  const errori = [];
  page.on('pageerror', (e) => errori.push(e.message));
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(8000);
  const r = await page.evaluate(() => {
    const titoli = [...document.querySelectorAll('h3')].map((h) => h.textContent.trim().replace(/\s+/g, ' '));
    const mappe = [...document.querySelectorAll('[data-aor]')].map((m) => ({
      id: m.getAttribute('data-aor'),
      chip: [...m.querySelectorAll('.aor-chip[data-sec]')].map((c) => c.getAttribute('data-sec')),
    }));
    const note = [...document.querySelectorAll('p.muted')].map((p) => p.textContent.trim()).filter((t) => /militar|FSS/i.test(t));
    return { titoli, mappe, note };
  });
  console.log('URL', URL);
  console.log('sezioni:', r.titoli.filter((t) => /settori|sectors|regolament|regulated|AoR|responsab/i.test(t)).join(' | '));
  for (const m of r.mappe) console.log(`mappa ${m.id}: ${m.chip.length} chip ->`, m.chip.join(', '));
  if (r.note.length) console.log('note:', r.note.join(' | '));
  const principale = r.mappe.find((m) => m.id === 'aerovia');
  if (principale) {
    // Stessa regola di AccFamigliaAorRegola: MIL in un pezzo DI MEZZO, oppure FSS in coda.
    const intrusi = principale.chip.filter((c) => {
      const p = c.toUpperCase().split('_');
      return p.length > 1 && (p[p.length - 1] === 'FSS' || p.slice(1, -1).some((x) => x.includes('MIL')));
    });
    console.log('AoR principale con MIL/FSS:', intrusi.length ? intrusi.join(', ') : 'nessuno');
  }
  console.log('errori JS:', errori.length ? errori.join(' | ') : 'nessuno');
  await b.close();
})();
