// Verifica live della sezione «Aree regolamentate» rifatta: una mappa sola + chip + descrizioni.
// Guarda TUTTE le sezioni-aree della pagina (una vIPI ACC ne ha una per blocco), non solo la prima.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = 'http://localhost:5034';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const PAGINE = [
  { nome: 'vIPI ACC LIBB (pubblica)', path: '/services/vsop/libb/vipi' },
  { nome: 'vIPI ACC LIRR (bozza, 105 aree)', path: '/services/vsop/lirr/vipi?as=draft' },
  { nome: 'vIPI APP LIBA (bozza, seminata)', path: '/services/vsop/libb/apps/vipi?app=LIBA_APP&as=draft' },
  { nome: 'vLOA LDZO (nessuna area scelta)', path: '/services/vsop/libb/vloa?acc=LDZO&as=draft' },
];

// Stato di UNA sezione, identificata dal suo scope.
const statoDi = (scope) => {
  const box = document.querySelector('[data-areacards="' + scope + '"]');
  const blocco = document.querySelector('.aor-block[data-aor="' + scope + '"]');
  const blocco3d = document.querySelector('.aor-block[data-aor="' + scope + '-3d"]');
  const cards = box ? [...box.querySelectorAll('[data-areacard]')] : [];
  return {
    scope,
    mappa2d: blocco ? blocco.querySelectorAll('.aor-leaflet').length : -1,
    chip: blocco ? blocco.querySelectorAll('.aor-chip').length : -1,
    chip3d: blocco3d ? blocco3d.querySelectorAll('.aor-chip').length : -1,
    preset: blocco ? [...blocco.querySelectorAll('.cfg-btn')].map((b) => b.innerText.trim()) : [],
    cards: cards.length,
    accese: cards.filter((c) => !c.hidden).length,
    aperte: cards.filter((c) => c.open).length,
    conta: ((box && box.querySelector('[data-areacount]')) || {}).textContent,
    vuoto: !((box && box.querySelector('[data-areaempty]')) || { hidden: true }).hidden,
    prima: cards[0] ? cards[0].querySelector('summary').innerText.replace(/\s+/g, ' ').trim() : null,
  };
};

const scopes = () => [...document.querySelectorAll('[data-areacards]')].map((b) => b.dataset.areacards);
const clicca = (sel) => { const e = document.querySelector(sel); if (!e) return false; e.click(); return true; };

async function apri(page, path) {
  await page.goto(BASE + path, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await sleep(1200);
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => { if (!d.hasAttribute('data-areacard')) d.open = true; }));
  await sleep(3500);
}

// ---- semina: sceglie due aree su un APP dall'EDITOR vero (giro editor→viewer completo) ----
async function seminaApp(page) {
  console.log('\n-------- semina aree su LIBA_APP (editor vero)');
  await page.goto(BASE + '/services/vsop/libb/apps/editor?app=LIBA_APP', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await sleep(2500);
  await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /✎/.test(x.innerText) && !x.disabled);
    if (b) b.click();
  });
  await sleep(3500);
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => d.open = true));
  await sleep(1200);

  // Nel pannello «Aree del proprio ACC» si digita nella casella di ricerca e si clicca le voci proposte.
  const esito = await page.evaluate(async () => {
    const inp = [...document.querySelectorAll('input.app-in')].find((i) => /area/i.test(i.placeholder || ''));
    if (!inp) return 'nessuna casella di ricerca area';
    const set = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    set.call(inp, 'LI');
    inp.dispatchEvent(new Event('input', { bubbles: true }));
    return 'digitato';
  });
  await sleep(2500);
  const scelte = await page.evaluate(() => {
    const voci = [...document.querySelectorAll('.area-pick-list .sp-item')];
    const presi = voci.slice(0, 3).map((v) => v.innerText.replace(/\s+/g, ' ').trim());
    voci.slice(0, 1).forEach((v) => v.click());
    return { proposte: voci.length, presi };
  });
  await sleep(2500);
  await page.evaluate(() => {
    const voci = [...document.querySelectorAll('.area-pick-list .sp-item')];
    if (voci.length) voci[0].click();
  });
  await sleep(2500);
  console.log('  ' + esito + ' — proposte: ' + scelte.proposte + ' — prime: ' + JSON.stringify(scelte.presi));
  const dentro = await page.evaluate(() =>
    [...document.querySelectorAll('.sp-item.on')].map((x) => x.innerText.replace(/\s+/g, ' ').trim()).slice(0, 6));
  console.log('  selezionate ora: ' + JSON.stringify(dentro));
}

async function main() {
  const browser = await puppeteer.launch({
    executablePath: EDGE, headless: false,
    args: ['--no-sandbox', '--window-size=1600,1300'],
    defaultViewport: { width: 1600, height: 1300 },
  });
  const page = await browser.newPage();
  const errori = [];
  page.on('pageerror', (e) => errori.push('[pageerror] ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errori.push('[console.error] ' + String(m.text()).slice(0, 200)); });

  await seminaApp(page);

  for (const p of PAGINE) {
    errori.length = 0;
    console.log('\n======== ' + p.nome + '   ' + p.path);
    await apri(page, p.path);
    const sc = await page.evaluate(scopes);
    if (!sc.length) { console.log('  !! nessuna sezione aree in pagina'); if (errori.length) console.log('  ' + errori[0]); continue; }
    console.log('  sezioni-aree in pagina: ' + JSON.stringify(sc));
    console.log('  mappine vecchie (.area-map): ' + await page.evaluate(() => document.querySelectorAll('.area-map').length));

    for (const scope of sc) {
      const s = await page.evaluate(statoDi, scope);
      console.log('  --- ' + scope + ': mappa2D=' + s.mappa2d + ' chip=' + s.chip + '/3D=' + s.chip3d
        + ' cards=' + s.cards + ' accese=' + s.accese + ' aperte=' + s.aperte);
      console.log('      preset ' + JSON.stringify(s.preset) + '  conta "' + (s.conta || '').trim() + '"');
      console.log('      prima riga: ' + s.prima);
      if (!s.cards) continue;

      // preset del primo tipo
      if (s.preset.length) {
        await page.evaluate((sel) => { document.querySelector(sel).click(); },
          `.aor-block[data-aor="${scope}"] .cfg-btn`);
        await sleep(800);
        const d = await page.evaluate(statoDi, scope);
        console.log('      preset «' + s.preset[0] + '» → accese=' + d.accese + '  conta "' + (d.conta || '').trim() + '"');
      }
      // nessuno / tutti
      await page.evaluate((sel) => { document.querySelector(sel).click(); }, `.aor-block[data-aor="${scope}"] .aor-all[data-act="none"]`);
      await sleep(700);
      const v = await page.evaluate(statoDi, scope);
      await page.evaluate((sel) => { document.querySelector(sel).click(); }, `.aor-block[data-aor="${scope}"] .aor-all[data-act="all"]`);
      await sleep(700);
      const t = await page.evaluate(statoDi, scope);
      console.log('      Nessuno → ' + v.accese + ' (vuoto=' + v.vuoto + ')   Tutti → ' + t.accese + ' (vuoto=' + t.vuoto + ')');

      // 3D: la stessa chip, la stessa descrizione
      const ok3d = await page.evaluate(clicca, '[data-aor-view] [data-view="3d"]');
      await sleep(1800);
      const sec = await page.evaluate((sel) => {
        const ch = document.querySelector(sel); if (!ch) return null; ch.click(); return ch.dataset.sec;
      }, `.aor-block[data-aor="${scope}-3d"] .aor-chip`);
      await sleep(900);
      const d3 = await page.evaluate(statoDi, scope);
      console.log('      3D (barra=' + ok3d + '): spenta ' + sec + ' → accese=' + d3.accese + ' (attese ' + (t.accese - 1) + ')');
      await page.evaluate(clicca, '[data-aor-view] [data-view="2d"]');
      await sleep(900);
      await page.evaluate((sel) => { document.querySelector(sel).click(); }, `.aor-block[data-aor="${scope}"] .aor-all[data-act="all"]`);
      await sleep(500);
    }
    console.log(errori.length ? '  ERRORI: ' + JSON.stringify(errori.slice(0, 3)) : '  nessun errore in pagina');

    await page.evaluate(() => document.querySelector('[data-areacards]').scrollIntoView({ block: 'start' }));
    await sleep(800);
    await page.screenshot({ path: __dirname + '/aree-' + p.path.split('/')[3].split('?')[0] + '.png' });
  }
  await browser.close();
}
main().catch((e) => { console.error('FALLITO:', e); process.exit(1); });
