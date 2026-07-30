// Driver di verifica live della vIPI. Copiare nello scratchpad (dove sta puppeteer-core), adattare STEPS.
//   cd <scratchpad>; npm init -y; npm install puppeteer-core; node driver.js
// Presuppone l'app avviata su http://localhost:5034 come da SKILL.md §2.
const puppeteer = require('puppeteer-core');
const fs = require('fs');

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = 'http://localhost:5034';
const OUT = __dirname;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---- i passi da verificare: path + selettore che deve esistere + cosa estrarre dal DOM ----
const STEPS = [
  {
    title: 'viewer aeroporto LIBD',
    path: '/vsop/libb/airports?icao=LIBD',
    waitFor: '.wrap',
    extract: () => ({
      titolo: document.querySelector('h2')?.innerText.trim(),
      sezioni: [...document.querySelectorAll('h3')].map((h) => h.innerText.trim()),
      tabelle: [...document.querySelectorAll('table')].map((t) => ({
        head: [...t.querySelectorAll('thead th')].map((h) => h.innerText.trim()),
        righe: t.querySelectorAll('tbody tr').length,
        prime: [...t.querySelectorAll('tbody tr')].slice(0, 3)
          .map((r) => [...r.querySelectorAll('td')].map((c) => c.innerText.trim())),
      })),
    }),
  },
  {
    title: 'editor aeroporto timeline release',
    path: '/vsop/libb/airports/editor?icao=LIBD',
    waitFor: '#sec-versioni',
    extract: () => ({
      righeRelease: document.querySelectorAll('#sec-versioni .ver-row').length,
      bottoni: [...document.querySelectorAll('#sec-versioni button')].map((b) => b.innerText.trim()).filter(Boolean),
      meta: [...document.querySelectorAll('#sec-versioni .vmeta')].map((m) => m.innerText.replace(/\s+/g, ' ').trim()),
    }),
  },
];

// Sentinelle sempre controllate su ogni pagina.
const GUARDS = () => ({
  // Razor che non ha valutato un'espressione (vedi SKILL.md §7).
  letteraliRazor: (document.body.innerText.match(/@[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z]+/g) || []).slice(0, 5),
  erroreInPagina: /errore imprevisto|unhandled|An unhandled error/i.test(document.body.innerText),
  callout: [...document.querySelectorAll('.callout')].map((c) => c.innerText.replace(/\s+/g, ' ').trim().slice(0, 140)),
});

async function main() {
  const browser = await puppeteer.launch({
    executablePath: EDGE,
    headless: 'new',
    args: ['--no-sandbox', '--window-size=1600,1400'],
    defaultViewport: { width: 1600, height: 1400 },
  });
  const page = await browser.newPage();

  const problems = [];
  const confirms = [];
  page.on('pageerror', (e) => problems.push(`[pageerror] ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') problems.push(`[console.error] ${m.text()}`); });
  page.on('response', (r) => { if (r.status() >= 400) problems.push(`[http ${r.status()}] ${r.url()}`); });
  // Il pannello release usa confirm(): senza handler il click sull'annulla resta appeso.
  page.on('dialog', async (d) => { confirms.push(d.message()); await d.accept(); });

  const report = [];
  for (const s of STEPS) {
    problems.length = 0;
    await page.goto(BASE + s.path, { waitUntil: 'domcontentloaded', timeout: 120000 });
    // Il circuito, non il DOM: la prima risposta è il prerender (SKILL.md §4).
    try { await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 }); }
    catch { problems.push('circuito Blazor non agganciato'); }
    if (s.waitFor) {
      try { await page.waitForSelector(s.waitFor, { timeout: 60000 }); }
      catch { problems.push(`selettore assente: ${s.waitFor}`); }
    }
    await sleep(2500);

    const file = s.title.replace(/[^a-z0-9]+/gi, '-').toLowerCase() + '.png';
    await page.screenshot({ path: `${OUT}/${file}`, fullPage: true });
    report.push({
      titolo: s.title, path: s.path, screenshot: file,
      dati: await page.evaluate(s.extract),
      guardie: await page.evaluate(GUARDS),
      problemi: [...problems],
    });
  }

  fs.writeFileSync(`${OUT}/report.json`, JSON.stringify(report, null, 2));
  for (const r of report) {
    console.log(`\n===== ${r.titolo} (${r.path}) -> ${r.screenshot}`);
    console.log(JSON.stringify({ dati: r.dati, guardie: r.guardie }, null, 2));
    console.log('problemi: ' + (r.problemi.length ? JSON.stringify(r.problemi, null, 2) : 'nessuno'));
  }
  if (confirms.length) console.log('\nconferme chieste: ' + JSON.stringify(confirms));
  console.log('\nORA APRIRE GLI SCREENSHOT: estrarre dati dal DOM non basta (SKILL.md §6).');

  await browser.close();
}

main().catch((e) => { console.error('DRIVER FALLITO:', e); process.exit(1); });
