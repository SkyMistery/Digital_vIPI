// Prova viva delle SID citate nel testo (§A73, slice 3): il tasto «SID» di un campo di prosa e quello sotto una
// tabella. Non prova il nome risolto — quello lo provano gli unit test e la pagina — prova i GESTI: il cursore
// segnato al clic, il selettore col suo fuoco, il riferimento scritto nel campo e il `change` sintetico che lo
// riporta nel modello (cioè: SOPRAVVIVE A UN RICARICO). E che le anteprime mostrino il nome, mai `[[SID`.
//
//   node sid-verifica.js "http://localhost:5034/services/vsop/libb/airports/editor?icao=LIBD" BANAV
//
// Il secondo argomento è il punto da cercare nel selettore: la prima SID che lo contiene va nel testo.
// ⚠️ SCRIVE nel documento (un paragrafo e una tabella nuova): solo su una COPIA del DB. Rifiuta ivao.aero.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const url = process.argv[2];
const punto = (process.argv[3] || 'BANAV').toUpperCase();
if (!url || /ivao\.aero/i.test(url)) { console.log('serve un indirizzo LOCALE (scrive nel documento)'); process.exit(2); }

let ko = 0;
const dice = (ok, msg) => { if (!ok) ko++; console.log((ok ? '  OK  ' : '  KO  ') + msg); };

async function apri(page, u) {
  await page.goto(u, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await sleep(3000);
}

async function inModifica(page) {
  const entrato = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find(x => /✎/.test(x.textContent) && !x.disabled);
    if (!b) return false;
    b.scrollIntoView({ block: 'center' }); b.click(); return true;
  });
  if (!entrato) return false;
  await sleep(5000);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(2500);
  return true;
}

// Il selettore aperto: aspetta le righe, cerca il punto, Invio. Torna il riferimento che la riga promette.
async function scegli(page) {
  await page.waitForSelector('.sidref-pick-row', { timeout: 20000 });
  const icao = await page.$eval('.sidref-pick input.icao', e => e.value);
  const attivo = await page.evaluate(() => document.activeElement && document.activeElement.classList.contains('cerca'));
  dice(attivo, 'aperto il selettore, il fuoco è nella sua ricerca');
  await page.type('.sidref-pick input.cerca', punto);
  await sleep(700);
  const rif = await page.$eval('.sidref-pick-row', e => e.getAttribute('title'));
  await page.keyboard.press('Enter');
  await sleep(1200);
  return { icao, rif };
}

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1100 });
  page.on('dialog', d => d.accept());
  const errori = [];
  page.on('console', m => { if (m.type() === 'error') errori.push(m.text()); });

  await apri(page, url);
  if (!await inModifica(page)) { console.log('NESSUN tasto ✎ Modifica: mi fermo'); await browser.close(); process.exit(2); }

  // ---- 1. un campo di prosa -----------------------------------------------------------------------------
  // Nessun campo di prosa visibile (su LIBD di sviluppo le callout di Remarks non entrano nell'editor, vedi
  // lavori-aperti §A73): se ne aggiunge uno col «+ Blocco → Paragrafo» della prima sezione libera.
  const haCampo = await page.evaluate(() =>
    [...document.querySelectorAll('.rta textarea')].some(t => t.offsetParent !== null));
  if (!haCampo) {
    const ok = await page.evaluate(() => {
      const menu = document.querySelector('details.blk-add');
      if (!menu) return false;
      menu.open = true;
      menu.querySelector('.blk-add-menu button').click();   // il primo è «Paragrafo»
      return true;
    });
    dice(ok, 'nessun campo di prosa: aggiunto un paragrafo');
    await sleep(3000);
    await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
    await sleep(1000);
  }
  const id = await page.evaluate(() => {
    const rta = [...document.querySelectorAll('.rta')].find(r => r.querySelector('textarea').offsetParent !== null);
    rta.scrollIntoView({ block: 'center' });
    const ta = rta.querySelector('textarea');
    ta.id = ta.id || 'rta-prova';
    return ta.id;
  });
  const sel = `#${id}`;
  const prima = await page.$eval(sel, e => e.value);
  // Il cursore in fondo, con un gesto vero: clic nel campo e Ctrl+Fine.
  await page.click(sel);
  await page.keyboard.down('Control'); await page.keyboard.press('End'); await page.keyboard.up('Control');
  await page.keyboard.type(' Then');
  await page.$eval(sel, e => e.dispatchEvent(new Event('change', { bubbles: true })));   // salva «Then»
  await sleep(1500);
  await page.click(sel);
  await page.keyboard.down('Control'); await page.keyboard.press('End'); await page.keyboard.up('Control');

  // ⚠️ `ElementHandle.click()`: mousedown/up VERI. Col solo `el.click()` il `preventDefault` del mousedown non
  // si proverebbe, e il fuoco uscirebbe dal campo — proprio il guasto da prendere.
  const tasto = await page.evaluateHandle(s => document.querySelector(s).closest('.rta').querySelector('.rta-sid'), sel);
  await tasto.click();
  const { icao, rif } = await scegli(page);
  console.log(`  ..  selettore: ICAO ${icao}, scelta ${rif}`);
  const dopo = await page.$eval(sel, e => e.value);
  dice(dopo.endsWith(' Then ' + rif), `il riferimento è in CODA, dove stava il cursore: …${JSON.stringify(dopo.slice(-40))}`);
  const aperti = await page.evaluate(() => [...document.querySelectorAll('.sidref-pick')].map(p =>
    (p.closest('.rta, [data-sid-host]') || {}).className + ' | ' + p.innerText.slice(0, 60).replace(/\s+/g, ' ')));
  if (aperti.length) { console.log('  ..  selettori ancora aperti:', aperti); await page.screenshot({ path: 'sid-ancora-aperto.png' }); }
  dice(aperti.length === 0, 'scelta fatta, il selettore si chiude');

  // ---- 2. una cella di tabella ------------------------------------------------------------------------
  // Una tabella nuova nella stessa sezione: il «+ Blocco» che segue il campo.
  const aggiunta = await page.evaluate(s => {
    const ta = document.querySelector(s);
    const menu = [...document.querySelectorAll('details.blk-add')]
      .find(d => ta.compareDocumentPosition(d) & Node.DOCUMENT_POSITION_FOLLOWING);
    if (!menu) return false;
    menu.open = true;
    const b = [...menu.querySelectorAll('button')].find(x => /Tabel|Table/.test(x.textContent));
    if (!b) return false;
    b.click(); return true;
  }, sel);
  dice(aggiunta, 'aggiunta una tabella nella sezione');
  await sleep(3000);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(1000);

  // L'ultimo contenitore di tabella dopo il campo: è quello appena aggiunto. Nasce senza righe: «+ riga».
  await page.evaluate(s => {
    const ta = document.querySelector(s);
    const host = [...document.querySelectorAll('[data-sid-host]')]
      .filter(h => ta.compareDocumentPosition(h) & Node.DOCUMENT_POSITION_FOLLOWING).pop();
    host.scrollIntoView({ block: 'center' });
    host.id = 'tab-prova';
    if (!host.querySelector('tbody input'))
      [...host.querySelectorAll('button')].find(b => /^\+\s/.test(b.textContent.trim())).click();
  }, sel);
  await sleep(2500);
  const cella = await page.$('#tab-prova tbody input');
  dice(cella !== null, 'la tabella ha una cella');
  await cella.click();
  await page.keyboard.type('via ');
  const tastoSid = await page.evaluateHandle(() => [...document.querySelectorAll('#tab-prova button')].find(b => b.textContent.trim() === 'SID'));
  await tastoSid.click();
  const scelta2 = await scegli(page);
  const valCella = await page.$eval('#tab-prova tbody input', e => e.value);
  dice(valCella === 'via ' + scelta2.rif, `nella cella: ${JSON.stringify(valCella)}`);

  // ---- 3. sopravvive a un ricarico --------------------------------------------------------------------
  await sleep(1500);
  await apri(page, url);
  await inModifica(page);
  const tutti = await page.evaluate(() =>
    [...document.querySelectorAll('textarea, input')].map(e => e.value).join('\n'));
  dice(tutti.includes(' Then ' + rif), 'dopo il ricarico il riferimento nel campo di prosa c\'è ancora');
  dice(tutti.includes('via ' + scelta2.rif), 'dopo il ricarico il riferimento nella cella c\'è ancora');

  // ---- 4. le anteprime: il nome, mai il codice del riferimento ----------------------------------------
  await apri(page, url);   // senza entrare in modifica: l'editor mostra i blocchi in lettura
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(1000);
  const lettura = await page.evaluate(() => document.body.innerText);
  dice(!lettura.includes('[[SID'), 'anteprima dell\'editor: nessun [[SID a schermo');
  const riga = (lettura.split('\n').find(l => l.includes('Then')) || '').trim();
  console.log('  ..  anteprima editor: ' + riga);
  dice(new RegExp(punto + ' \\d[A-Z]').test(riga), `anteprima dell'editor: la SID col nome completo (${punto} …)`);

  console.log(`\nerrori console: ${errori.length}`); errori.slice(0, 5).forEach(e => console.log('   ' + e));
  console.log(ko === 0 ? 'TUTTO VERDE' : `ROSSI: ${ko}`);
  await page.screenshot({ path: 'sid-editor.png' });
  await browser.close();
  process.exit(ko === 0 ? 0 : 1);
})();
