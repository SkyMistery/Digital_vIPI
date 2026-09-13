// Verifica delle PORTE delle pagine (lotto L5 della revisione del 13 settembre 2026): gesti ravvicinati su
// Ricerca, Audit, Glossario e Versioni non devono abbattere il circuito. Sul publish win-x64 su :5199, da admin.
//
//   node porte-verifica.js
//
// ⚠️ Un circuito caduto non dà un errore HTTP: si vede come barra di riconnessione o come errori nella console.
// Qui si guardano tutte e due, più il registro `diagnostica/errori-richieste.txt` se esiste.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  if (/ivao\.aero/.test(BASE)) { console.log('ROSSO: da usare in locale'); process.exit(2); }
  const esiti = [];
  const nota = (nome, ok, dettaglio) => { esiti.push(ok); console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`); };

  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errori = [];
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });
  page.on('pageerror', (e) => errori.push('pageerror: ' + e.message));

  const apri = async (url, selettore) => {
    await page.goto(BASE + url, { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60 && !(await page.evaluate(() => !!window.Blazor)); i++) await sleep(1000);
    await page.waitForSelector(selettore, { timeout: 30000 });
    await sleep(1500);
  };
  const vivo = async () => page.evaluate(() => {
    const r = document.getElementById('components-reconnect-modal');
    return !(r && getComputedStyle(r).display !== 'none' && r.className && /show|failed|rejected/.test(r.className));
  });

  try {
    // Ricerca: si scrive di seguito, senza pause.
    await apri('/services/vsop/search', '.wrap input');
    await page.type('.wrap input', 'LIRF', { delay: 15 });
    await sleep(1500);
    await page.$eval('.wrap input', (e) => { e.value = ''; e.dispatchEvent(new Event('input', { bubbles: true })); });
    await page.type('.wrap input', 'rwy', { delay: 10 });
    await sleep(1500);
    const riga = await page.evaluate(() => document.querySelector('.wrap p.muted')?.textContent || '');
    nota('Ricerca: scrittura di seguito', (await vivo()) && /for\s*rwy|per\s*rwy/i.test(riga), riga.trim().slice(0, 60));

    // Audit: periodo cambiato tre volte di fila, più «Aggiorna».
    await apri('/services/vsop/admin/audit', 'select.htree-select');
    for (const v of ['7', '90', '0', '30']) await page.select('select.htree-select', v);
    await page.click('button.btn.ghost');
    await sleep(2500);
    nota('Audit: periodi a raffica', await vivo(), 'circuito vivo');

    // Glossario: ricerca digitata, poi apertura rapida di più voci.
    await apri('/services/vsop/admin/glossary', 'input.htree-search, input');
    const campo = await page.$('input.htree-search') || await page.$('input');
    await campo.type('pist', { delay: 20 });
    await sleep(600);
    await campo.type('a', { delay: 20 });
    await sleep(2500);
    const chip = await page.$$('button.sh-chip');
    for (const c of chip.slice(0, 3)) { try { await c.click(); } catch { } }
    await sleep(2500);
    nota('Glossario: ricerca e aperture a raffica', await vivo(), `${chip.length} chip`);

    // Versioni: righe scelte di seguito, senza aspettare.
    await apri('/services/vsop/versions', '.doc-rowi.acc-pick');
    const righe = await page.$$('.doc-rowi.acc-pick');
    for (const r of righe.slice(0, 5)) { try { await r.click(); } catch { } }
    await sleep(3000);
    nota('Versioni: righe scelte a raffica', await vivo(), `${Math.min(5, righe.length)} righe`);

    nota('console del browser pulita', errori.length === 0, errori.length ? errori.slice(0, 3).join(' | ') : 'nessun errore');
  } catch (e) {
    nota('eccezione', false, e.message);
  } finally {
    await browser.close();
  }
  const rossi = esiti.filter((x) => !x).length;
  console.log(rossi ? `\n===== ${rossi} ROSSI =====` : '\n===== TUTTO VERDE =====');
  process.exit(rossi ? 1 : 0);
})();
