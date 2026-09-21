// Prova viva dei riferimenti ai DATI citati nel testo (carta 2026-09-20-riferimenti-ai-dati.md), cioè le due
// chip che la review del 20 settembre ha aggiunto al selettore: **RWY** e **FIX**. Le procedure (SID/STAR) le
// prova `sid-verifica.js`; qui si prova quel che quelle due hanno di DIVERSO:
//
//   • RWY vuole l'ICAO (le soglie sono di uno scalo) e scrive DUE gettoni: `[[RWY LIBD 07]]`;
//   • FIX non lo vuole (il catalogo dei punti è unico) e ne scrive uno solo: `[[FIX BANAV]]`;
//   • il campo dell'ICAO deve COMPARIRE e SPARIRE cambiando chip — senza, l'elenco delle piste resta vuoto
//     e sembra che non ci siano soglie;
//   • nell'anteprima esce il pezzo che si LEGGE (`07`, non «LIBD 07»), e mai `[[RWY` o `[[FIX`.
//
//   node dato-verifica.js "http://localhost:5000/services/vsop/libb/airports/editor?icao=LIBD" BANAV
//
// ⚠️ SCRIVE nel documento (aggiunge un paragrafo): solo su una COPIA del DB. Rifiuta ivao.aero.
//
// 🔴 **Per la chip FIX l'app va avviata con `Sectorfile:RawBaseUrl` VERO.** Il catalogo dei punti arriva dal
// sectorfile (`AuroraNavaidSource`, via HTTP), non dal `vipi.db`: avviando con `RawBaseUrl=" "` — la ricetta
// di SKILL.md §2, che spegne la rete — l'elenco è vuoto **per disegno** e la prova non può dire niente. È la
// stessa distinzione che il prodotto fa fra «non lo so» e «non c'è».
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const url = process.argv[2];
const punto = (process.argv[3] || 'BANAV').toUpperCase();
if (!url || /ivao\.aero/i.test(url)) { console.log('serve un indirizzo LOCALE (scrive nel documento)'); process.exit(2); }

let ko = 0;
const dice = (ok, msg) => { if (!ok) ko++; console.log((ok ? '  OK  ' : '  KO  ') + msg); };

async function apri(page, u) {
  await page.goto(u, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 120000 });
  await sleep(3000);
}

async function inModifica(page) {
  const entrato = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /✎/.test(x.textContent) && !x.disabled);
    if (!b) return false;
    b.scrollIntoView({ block: 'center' }); b.click(); return true;
  });
  if (!entrato) return false;
  await sleep(5000);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(2500);
  return true;
}

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1100 });
  page.on('dialog', (d) => d.accept());
  const errori = [];
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });

  await apri(page, url);
  if (!await inModifica(page)) { console.log('NESSUN tasto ✎ Modifica: mi fermo'); await browser.close(); process.exit(2); }

  // Un campo di prosa: se non ce n'è di visibili se ne aggiunge uno.
  const haCampo = await page.evaluate(() =>
    [...document.querySelectorAll('.rta textarea')].some((t) => t.offsetParent !== null));
  if (!haCampo) {
    await page.evaluate(() => {
      const menu = document.querySelector('details.blk-add');
      menu.open = true;
      menu.querySelector('.blk-add-menu button').click();
    });
    await sleep(3000);
    await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
    await sleep(1000);
  }
  const sel = '#' + await page.evaluate(() => {
    const rta = [...document.querySelectorAll('.rta')].find((r) => r.querySelector('textarea').offsetParent !== null);
    rta.scrollIntoView({ block: 'center' });
    const ta = rta.querySelector('textarea');
    ta.id = ta.id || 'rta-prova';
    return ta.id;
  });

  // Il gesto completo per una chip: cursore in coda, marcatore, «Cita», chip, (ICAO), ricerca, Invio.
  async function cita(parola, cerca, marcatore) {
    await page.click(sel);
    await page.keyboard.down('Control'); await page.keyboard.press('End'); await page.keyboard.up('Control');
    await page.keyboard.type(marcatore);
    const tasto = await page.evaluateHandle((s) => document.querySelector(s).closest('.rta').querySelector('.rta-sid'), sel);
    await tasto.click();                       // clic VERO: prova il preventDefault del mousedown
    await page.waitForSelector('.sidref-pick', { timeout: 20000 });
    const c = await page.evaluateHandle((p) =>
      [...document.querySelectorAll('.sidref-pick-kind button')].find((b) => b.textContent.trim() === p), parola);
    await c.click();
    await sleep(2500);
    const premuta = await page.evaluate((p) =>
      [...document.querySelectorAll('.sidref-pick-kind button')]
        .some((b) => b.textContent.trim() === p && b.getAttribute('aria-pressed') === 'true'), parola);
    dice(premuta, `chip ${parola} premuta`);
    // ⚠️ Il campo ICAO c'è solo per le famiglie di scalo: è la differenza fra le due chip nuove.
    const conIcao = await page.evaluate(() => {
      const e = document.querySelector('.sidref-pick input.icao');
      return e ? e.value : null;
    });
    dice(parola === 'RWY' ? conIcao !== null : conIcao === null,
      `chip ${parola}: il campo ICAO ${parola === 'RWY' ? 'C\'È' : 'non c\'è'} (letto: ${JSON.stringify(conIcao)})`);
    // ⚠️ Vuoto non è «non ci sono»: per FIX vuol dire quasi sempre sorgente spenta. Lo si dice, non si aspetta.
    const cePoi = await page.waitForSelector('.sidref-pick-row', { timeout: 20000 }).catch(() => null);
    if (!cePoi) {
      dice(false, `chip ${parola}: elenco VUOTO` +
        (parola === 'FIX' ? ' — avviare l\'app con `Sectorfile:RawBaseUrl` vero: il catalogo dei punti viene da lì' : ''));
      return null;
    }
    if (cerca) { await page.type('.sidref-pick input.cerca', cerca); await sleep(900); }
    const righe = await page.$$eval('.sidref-pick-row', (rs) => rs.length);
    dice(righe > 0, `chip ${parola}: l'elenco ha ${righe} voci`);
    const rif = await page.$eval('.sidref-pick-row', (e) => e.getAttribute('title'));
    // ⚠️ Si CLICCA la riga, non si preme Invio: il selettore mette il fuoco nella ricerca alla sola PRIMA
    // apertura (`OnAfterRenderAsync(firstRender)`), quindi dopo un cambio chip il fuoco è sulla chip e un
    // Invio la ripremerebbe soltanto. Il gesto vero di chi cambia famiglia è il clic sulla voce.
    const riga1 = await page.$('.sidref-pick-row');
    await riga1.click();
    await sleep(1500);
    const dopo = await page.$eval(sel, (e) => e.value);
    dice(dopo.endsWith(marcatore + rif), `${parola}: il riferimento è dove stava il cursore → ${JSON.stringify(dopo.slice(-32))}`);
    const aperti = await page.$$eval('.sidref-pick', (p) => p.length);
    dice(aperti === 0, `${parola}: scelta fatta, il selettore si chiude`);
    return { rif, marcatore, icao: conIcao };
  }

  const rwy = await cita('RWY', '', ' pista ');
  const fix = await cita('FIX', punto, ' punto ');
  if (!rwy || !fix) { console.log('\n🔴 una delle due chip non ha dato voci: mi fermo qui'); await browser.close(); process.exit(1); }
  dice(/^\[\[RWY [A-Z]{4} /.test(rwy.rif), `RWY porta DUE gettoni (scalo + soglia): ${rwy.rif}`);
  dice(/^\[\[FIX [A-Z0-9]+\]\]$/.test(fix.rif), `FIX porta UN gettone solo: ${fix.rif}`);

  // ---- sopravvive a un ricarico ------------------------------------------------------------------------
  await sleep(1500);
  await apri(page, url);
  await inModifica(page);
  const tutti = await page.evaluate(() =>
    [...document.querySelectorAll('textarea, input')].map((e) => e.value).join('\n'));
  dice(tutti.includes(' pista ' + rwy.rif), 'dopo il ricarico il riferimento RWY c\'è ancora');
  dice(tutti.includes(' punto ' + fix.rif), 'dopo il ricarico il riferimento FIX c\'è ancora');

  // ---- l'anteprima: il pezzo che si LEGGE, mai il codice ----------------------------------------------
  await apri(page, url);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(1500);
  const lettura = await page.evaluate(() => document.body.innerText);
  dice(!lettura.includes('[[RWY'), 'anteprima: nessun [[RWY a schermo');
  dice(!lettura.includes('[[FIX'), 'anteprima: nessun [[FIX a schermo');
  // 🔴 Il ripiego/valore della pista è la SOGLIA, non «LIBD 07»: lo scalo è il contesto della frase.
  const soglia = (rwy.rif.match(/\[\[RWY [A-Z]{4} ([A-Z0-9]+)\]\]/) || [])[1];
  const riga = (lettura.split('\n').find((l) => l.includes(' pista ')) || '').trim();
  console.log('  ..  anteprima: ' + riga);
  dice(new RegExp('pista ' + soglia + '\\b').test(riga), `anteprima: esce la sola soglia (${soglia}), non «${rwy.icao} ${soglia}»`);
  dice(riga.includes(punto), `anteprima: il punto esce col suo nome (${punto})`);

  await page.screenshot({ path: `${__dirname}/dato.png`, fullPage: false });
  console.log(`\nerrori console: ${errori.length}`); errori.slice(0, 5).forEach((e) => console.log('   ' + e));
  console.log(ko === 0 ? '\n✅ tutto verde' : `\n🔴 ${ko} controlli falliti`);
  console.log('ORA APRIRE dato.png: estrarre dati dal DOM non basta (SKILL.md §6).');
  await browser.close();
  process.exit(ko === 0 ? 0 : 1);
})().catch((e) => { console.error('VERIFICA FALLITA:', e); process.exit(1); });
