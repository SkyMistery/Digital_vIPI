// SID/STAR fra i punti di una clausola di trasferimento (21 settembre 2026). Nell'editor dei trasferimenti:
//   1. apre «+ Clausola» sulla prima sezione di ARRIVO con uno scalo (o PARTENZA con PARTENZA=1);
//   2. scrive le prime lettere nei punti e legge i suggerimenti: devono esserci STAR (o SID) dello scalo;
//   3. sceglie la prima procedura e legge il luogo di trasferimento (deve diventare «confine dell'AoR»),
//      l'opzione «come l'ingresso» (spenta) e l'anteprima della frase («autorizzato via … al confine dell'AoR»).
// Non salva: chiude il pannello. node procedura-nei-punti-verifica.js [url]   (default: LIBB in locale)
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const URL = process.argv[2] || 'http://localhost:5034/services/vsop/admin/transfers?acc=LIBB';
const VERSO = process.env.PARTENZA ? /partenz|depart/i : /arriv/i;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await b.newPage();
  await page.setViewport({ width: 1600, height: 1100 });
  const errori = [];
  page.on('pageerror', (e) => errori.push(e.message));
  page.on('dialog', (d) => d.dismiss());
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(6000);

  // Si entra in modifica: senza il lucchetto i tasti «+ Clausola» sono spenti.
  await page.evaluate(() => {
    const x = [...document.querySelectorAll('button')].find((y) => /Start editing|Inizia a modificare|Modifica/i.test(y.textContent) && !y.disabled);
    if (x) x.click();
  });
  await sleep(4000);

  // I «+ Clausola» accesi, ciascuno col testo della sua sezione intorno: si prende il primo del verso chiesto
  // con uno scalo ICAO nel testo.
  // Il contenitore della sezione è il più ampio antenato che contiene UN SOLO «+ Clausola»: più su si
  // leggerebbe il testo delle sezioni accanto (e lo scalo di un'altra).
  const scelto = await page.evaluate((versoSrc, scalo) => {
    const verso = new RegExp(versoSrc, 'i');
    const eClausola = (y) => /^\+\s*(Clause|Clausola)$/i.test(y.textContent.trim());
    const tasti = [...document.querySelectorAll('button')].filter((y) => eClausola(y) && !y.disabled);
    for (const t of tasti) {
      let n = t;
      while (n.parentElement && [...n.parentElement.querySelectorAll('button')].filter(eClausola).length === 1) n = n.parentElement;
      const testo = n.innerText;
      if (verso.test(testo) && testo.includes(scalo)) { t.scrollIntoView({ block: 'center' }); t.click(); return testo.slice(0, 120).replace(/\s+/g, ' '); }
    }
    return null;
  }, VERSO.source, process.env.SCALO || 'LIBD');
  console.log('sezione:', scelto);
  if (!scelto) { console.log('nessuna sezione del verso con uno scalo'); await b.close(); return; }
  await sleep(4000);

  const intestazione = await page.evaluate(() => document.querySelector('.xt-panel-h')?.innerText.replace(/\s+/g, ' '));
  console.log('pannello:', intestazione);

  // Il campo punti è il primo typeahead del pannello (etichetta «Punti»/«Points»).
  const campo = await page.evaluateHandle(() => {
    const l = [...document.querySelectorAll('.xt-panel-body label')].find((x) => /^(Punti|Points)/i.test(x.textContent.trim()));
    return (l && l.closest('.field, .xt-wide, div').querySelector('input')) || null;
  });
  if (!campo.asElement()) { console.log('campo punti non trovato'); await b.close(); return; }

  // Una lettera dopo l'altra: il picker propone sull'ULTIMA voce scritta.
  const scalo = (intestazione.match(/\bLI[A-Z]{2}\b/) || [])[0];
  const prefissi = ['A', 'B', 'C', 'D', 'E', 'G', 'L', 'M', 'N', 'O', 'P', 'R', 'S', 'T', 'V'];
  let proc = null; let suggerimenti = [];
  for (const pre of prefissi) {
    await campo.click({ clickCount: 3 });
    await page.keyboard.press('Backspace');
    await campo.type(pre, { delay: 60 });
    await sleep(900);
    suggerimenti = await page.evaluate(() => [...document.querySelectorAll('.xt-panel-body .sector-pick .sp-item')]
      .filter((x) => x.offsetParent !== null).map((x) => x.innerText.replace(/\s+/g, ' ').trim()).slice(0, 12));
    // ⚠️ innerText incolla nome e nota («VURKE 5WSID LIBD · 07»): niente \b davanti a SID.
    proc = suggerimenti.find((s) => /(STAR|SID) [A-Z]{4}/.test(s));
    if (proc) break;
  }
  console.log('scalo:', scalo, '· suggerimenti:', suggerimenti.join(' | '));
  if (process.env.DEBUG) console.log(await page.evaluate((el) => ({ ph: el.placeholder, v: el.value, pick: document.querySelectorAll('.sector-pick').length, html: el.closest('.sector-pick-wrap')?.outerHTML.slice(0, 900) }), campo));
  if (!proc) { console.log('NESSUNA procedura fra i suggerimenti'); await b.close(); return; }

  const nome = proc.split(/(STAR|SID) [A-Z]{4}/)[0].trim();
  await campo.click({ clickCount: 3 });
  await page.keyboard.press('Backspace');
  await campo.type(nome, { delay: 40 });
  await page.keyboard.press('Escape');   // chiude la tendina, non il pannello: la tendina ferma la risalita
  await sleep(1500);
  // Si apre la parte «trasferimento», se è chiusa, per leggerne la tendina.
  // Il riassunto della sezione chiusa dice già il luogo: si legge PRIMA di aprirla.
  const riassunto = await page.evaluate(() => {
    const h = [...document.querySelectorAll('.xt-grp')].find((x) => /^\s*(Trasferimento|Transfer)(\s|$)/i.test(x.innerText));   // maiuscolo dal CSS
    if (!h) return 'gruppi: ' + [...document.querySelectorAll('.xt-grp')].map((x) => x.innerText.replace(/\s+/g, ' ').slice(0, 30)).join(' / ');
    const t = h ? h.innerText.replace(/\s+/g, ' ') : null;
    if (h && h.getAttribute('aria-expanded') !== 'true') h.click();
    return t;
  });
  console.log('riassunto «Trasferimento»:', riassunto);
  await sleep(1500);
  const esito = await page.evaluate(() => {
    const s = [...document.querySelectorAll('.xt-panel-body select')].find((x) => [...x.options].some((o) => o.value === 'AorBoundary'));
    const come = s && [...s.options].find((o) => o.value === 'Unspecified');
    const frasi = [...document.querySelectorAll('.xt-panel-body *')].filter((x) => x.children.length === 0 && /via /.test(x.textContent)).map((x) => x.textContent.trim());
    return { luogo: s && s.value, comeIngressoSpenta: come && come.disabled, frasi: frasi.slice(0, 3), punti: document.activeElement?.value };
  });
  console.log('punti scritti:', nome);
  console.log('luogo di trasferimento:', esito.luogo, '· «come l\'ingresso» spenta:', esito.comeIngressoSpenta);
  console.log('frase:', esito.frasi.join(' || ') || '(nessuna anteprima con «via»)');
  console.log('errori JS:', errori.length ? errori.join(' | ') : 'nessuno');
  await b.close();
})();
