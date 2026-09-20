// Prova viva del campo che si adatta al testo (`data-adatta`, vipi-editor.js, 16 settembre 2026): nell'editor
// vero, un campo di prosa deve CRESCERE quando il testo cresce — e non basta guardare il codice JS, perché il
// guasto sta a valle, nel CSS: una regola che dà `flex` alla textarea dentro `.rta` (flex a COLONNA) rende una
// base che SCAVALCA l'altezza in linea, e vipiAdatta scrive 415px mentre il browser ne rende 67.
//
// Per questo la prova è DOPPIA, e la seconda metà è la parte che conta: la stessa misura si rifà dopo aver
// rimesso a mano la regola incriminata. Se le due metà dicono la stessa cosa, la prova non distingue e non
// prova niente.
//
//   node adatta-verifica.js [percorso-editor]
// Presuppone l'app avviata come da SKILL.md §2 (qui su :5000, `dotnet run --no-launch-profile`).
const puppeteer = require('puppeteer-core');

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.VIPI_BASE || 'http://localhost:5000';
const PERCORSO = process.argv[2] || '/services/vsop/libb/airports/editor?icao=LIBD';
const REGOLA_SOSPETTA = ':where(.vipi-root) .app-block-edit .app-ta{flex:1}';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Il gesto misurato: si azzera il campo, si scrive corto, si scrive lungo, si guarda se è cresciuto.
// `__vipiMinimo` si azzera perché l'altezza trascinata a mano è, per disegno, un minimo che vince.
function GESTO() {
  const campi = [...document.querySelectorAll('.app-block-edit textarea[data-adatta]')].filter((t) => t.closest('.rta'));
  const el = campi[0];
  if (!el) return { esito: 'nessun campo di prosa in modifica' };
  el.value = 'riga sola';
  el.style.height = '';
  el.__vipiMinimo = 0;
  el.dispatchEvent(new Event('input', { bubbles: true }));
  const corta = el.offsetHeight;
  el.value = Array.from({ length: 16 }, (_, i) => 'riga di prosa numero ' + (i + 1)).join('\n');
  el.dispatchEvent(new Event('input', { bubbles: true }));
  const cs = getComputedStyle(el);
  return {
    flexBasis: cs.flexBasis, flexGrow: cs.flexGrow,
    altezzaCorta: corta,
    altezzaLunga: el.offsetHeight,
    altezzaChiesta: el.style.height,
    altezzaCalcolata: cs.height,
    cresciuta: el.offsetHeight > corta + 20,
  };
}

(async () => {
  const browser = await puppeteer.launch({
    executablePath: EDGE, headless: 'new',
    args: ['--no-sandbox', '--window-size=1600,1400'],
    defaultViewport: { width: 1600, height: 1400 },
  });
  const page = await browser.newPage();
  const problemi = [];
  page.on('pageerror', (e) => problemi.push(`[pageerror] ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') problemi.push(`[console.error] ${m.text()}`); });

  await page.goto(BASE + PERCORSO, { waitUntil: 'domcontentloaded', timeout: 180000 });
  // Il circuito, non il DOM: la prima risposta è il prerender (SKILL.md §4).
  await page.waitForFunction(() => !!window.Blazor, { timeout: 180000 });
  await page.waitForSelector('.editor-bar', { timeout: 120000 });
  await sleep(6000);

  await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')]
      .find((x) => /^✎\s*(Edit|Modifica)$/i.test(x.innerText.replace(/\s+/g, ' ').trim()) && !x.disabled);
    if (b) b.click();
  });
  await sleep(4000);
  // Un campo dentro una sezione CHIUSA non ha altezza (`offsetParent` nullo) e vipiAdatta lo salta apposta.
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => { d.open = true; }));
  await sleep(1500);
  await page.waitForSelector('.app-block-edit textarea[data-adatta]', { timeout: 60000 })
    .catch(() => problemi.push('nessun campo di prosa dopo «Modifica»: lock non preso?'));
  await sleep(2000);

  const nelFoglio = await page.evaluate(() => {
    for (const foglio of document.styleSheets) {
      let regole; try { regole = foglio.cssRules; } catch { continue; }
      for (const r of regole) if (r.selectorText && /\.app-block-edit\s+\.app-ta/.test(r.selectorText)) return r.cssText;
    }
    return null;
  });

  const oggi = await page.evaluate(GESTO);
  await page.evaluate((css) => {
    const s = document.createElement('style');
    s.textContent = css;
    document.head.appendChild(s);
  }, REGOLA_SOSPETTA);
  await sleep(300);
  const conLaRegola = await page.evaluate(GESTO);

  await page.screenshot({ path: `${__dirname}/adatta.png` });
  console.log(JSON.stringify({ percorso: PERCORSO, regolaNelFoglio: nelFoglio, oggi, conLaRegola, problemi }, null, 2));
  if (!oggi.cresciuta) console.log('\n🔴 IL CAMPO NON SI ADATTA col foglio di oggi.');
  else if (conLaRegola.cresciuta) console.log('\n⚠️ Cresce ANCHE con la regola rimessa: la prova NON distingue, il guasto è altrove.');
  else console.log('\n✅ Cresce oggi e non cresce con la regola rimessa: la causa è quella regola.');
  console.log('\nORA APRIRE adatta.png: estrarre dati dal DOM non basta (SKILL.md §6).');

  await browser.close();
})().catch((e) => { console.error('VERIFICA FALLITA:', e); process.exit(1); });
