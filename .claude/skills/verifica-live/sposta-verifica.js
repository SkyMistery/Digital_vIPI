// Verifica live della carta 2026-09-04 (sezioni mobili), sul vSOP militare di LIBG.
//   A. il menu «Sposta in…» porta una sezione libera dentro un'altra sezione, e persiste al ricarico
//   B. una FIGLIA si trascina nel menu-sezioni e, lasciata su un altro gruppo, cambia padre (drag VERO via CDP)
//   C. la stessa figlia, marcata «sopra il corpo» sotto una sezione resa dalla PAGINA (Frequenze ATC/CRC),
//      esce SOPRA la scheda anche nel documento — è il difetto di SectionNode che la carta chiude
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = 'http://localhost:5034';
const EDITOR = '/services/vsop/libb/mil/editor?icao=LIBG';
const BOZZA = '/services/vsop/libb/mil?icao=LIBG&as=draft';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function apri(page, path, selettore) {
  await page.goto(BASE + path, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await page.waitForSelector(selettore, { timeout: 60000 });
  await sleep(2500);
}

async function inModifica(page) {
  const ok = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /✎/.test(x.innerText) && !x.disabled);
    if (b) { b.click(); return true; }
    return false;
  });
  await sleep(4000);
  return ok;
}

// L'indice come lo si legge: etichetta + livello (lvl2 = radice, lvl3 = figlia).
const indice = (page) => page.evaluate(() =>
  [...document.querySelectorAll('.toc a')].map((a) => a.className.trim() + ' | ' + a.innerText.trim()));

async function aggiungiSezione(page) {
  await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /Aggiungi sezione|Add section/i.test(x.innerText));
    b && b.click();
  });
  await sleep(4000);
}

// Rinomina la sezione appena creata, per riconoscerla in mezzo alle altre.
async function rinomina(page, da, a) {
  const ok = await page.evaluate((da, a) => {
    const i = [...document.querySelectorAll('input.app-in')].find((x) => x.value === da);
    if (!i) return false;
    const set = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    set.call(i, a);
    i.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  }, da, a);
  await sleep(4000);
  return ok;
}

// Apre il menu «Sposta in…» della sezione col titolo dato e clicca la destinazione.
async function spostaIn(page, titolo, destinazione) {
  const esito = await page.evaluate((titolo, destinazione) => {
    const teste = [...document.querySelectorAll('.dse-head')];
    const testa = teste.find((h) => {
      const i = h.querySelector('input.app-in');
      const t = i ? i.value : (h.querySelector('h3,h4')?.innerText ?? '');
      return t.trim() === titolo;
    });
    if (!testa) return { ok: false, perche: 'sezione non trovata' };
    const menu = [...testa.querySelectorAll('details.blk-add')]
      .find((d) => /Sposta in|Move to/i.test(d.querySelector('summary')?.innerText ?? ''));
    if (!menu) return { ok: false, perche: 'menu «Sposta in…» assente' };
    menu.open = true;
    const voci = [...menu.querySelectorAll('.blk-add-menu button')].map((b) => b.innerText.trim());
    const b = [...menu.querySelectorAll('.blk-add-menu button')].find((x) => x.innerText.trim() === destinazione);
    if (!b) return { ok: false, perche: 'destinazione assente', voci };
    b.click();
    return { ok: true, voci };
  }, titolo, destinazione);
  await sleep(4500);
  return esito;
}

// Clicca il comando «⤒ sopra il corpo» della sezione col titolo dato.
async function sopraIlCorpo(page, titolo) {
  const ok = await page.evaluate((titolo) => {
    const testa = [...document.querySelectorAll('.dse-head')].find((h) => {
      const i = h.querySelector('input.app-in');
      const t = i ? i.value : (h.querySelector('h3,h4')?.innerText ?? '');
      return t.trim() === titolo;
    });
    if (!testa) return false;
    const b = [...testa.querySelectorAll('button')].find((x) => /⤒/.test(x.innerText));
    if (!b) return false;
    b.click();
    return true;
  }, titolo);
  await sleep(4000);
  return ok;
}

// Trascinamento VERO: il browser avvia il drag, noi consegniamo enter/over/drop.
async function trascina(page, etichettaDa, etichettaSu) {
  const box = await page.evaluate((a, b) => {
    const n = [...document.querySelectorAll('.toc a[draggable="true"]')];
    const s = n.find((x) => x.innerText.trim() === a);
    const d = n.find((x) => x.innerText.trim() === b);
    if (!s || !d) return null;
    s.scrollIntoView({ block: 'center' });
    const rs = s.getBoundingClientRect(), rd = d.getBoundingClientRect();
    return { sx: rs.x + rs.width / 2, sy: rs.y + rs.height / 2, dx: rd.x + rd.width / 2, dy: rd.y + rd.height / 2 };
  }, etichettaDa, etichettaSu);
  if (!box) return { ok: false, perche: 'voci non trascinabili o non trovate' };

  const client = await page.createCDPSession();
  let data = null;
  client.on('Input.dragIntercepted', (e) => { data = e.data; });
  await client.send('Input.setInterceptDrags', { enabled: true });
  await client.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: box.sx, y: box.sy, button: 'left', clickCount: 1 });
  await client.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: box.sx + 8, y: box.sy + 14, button: 'left', buttons: 1 });
  await sleep(400);
  if (!data) return { ok: false, perche: 'il browser non ha avviato il drag' };
  await client.send('Input.dispatchDragEvent', { type: 'dragEnter', x: box.dx, y: box.dy, data });
  await client.send('Input.dispatchDragEvent', { type: 'dragOver', x: box.dx, y: box.dy, data });
  await client.send('Input.dispatchDragEvent', { type: 'drop', x: box.dx, y: box.dy, data });
  await sleep(4500);
  return { ok: true };
}

(async () => {
  const browser = await puppeteer.launch({
    executablePath: EDGE, headless: false, defaultViewport: { width: 1500, height: 1000 },
    args: ['--window-size=1520,1040'],
  });
  const page = await browser.newPage();
  page.on('dialog', async (d) => { await d.accept(); });
  const errori = [];
  page.on('pageerror', (e) => errori.push(String(e)));

  const NOME = 'PROVA-SPOSTAMENTO';

  await apri(page, EDITOR, '.toc');
  console.log('modifica :', await inModifica(page) ? 'presa' : 'NON presa');

  // ---- A. il menu «Sposta in…»
  const gia = (await indice(page)).some((x) => x.includes(NOME));
  if (!gia) {
    await aggiungiSezione(page);
    console.log('rinomina :', await rinomina(page, 'Nuova sezione', NOME) ? 'ok' : 'NON riuscita');
  } else console.log('sezione di prova: gia presente, riuso');

  const a1 = await spostaIn(page, NOME, 'General data');
  console.log('A. sposta in «General data»:', JSON.stringify(a1).slice(0, 300));
  const dopoA = await indice(page);
  console.log('   indice   :', dopoA.filter((x) => x.includes(NOME)).join(' / ') || '(non in indice)');

  await apri(page, EDITOR, '.toc');
  const ricaricoA = await indice(page);
  console.log('   ricarico :', ricaricoA.filter((x) => x.includes(NOME)).join(' / ') || '(non in indice)');

  // ---- B. la figlia si trascina su un altro gruppo
  console.log('modifica :', await inModifica(page) ? 'presa' : 'NON presa');
  const b1 = await trascina(page, NOME, 'Engine start');
  console.log('B. trascinata su «Engine start»:', JSON.stringify(b1));
  await apri(page, EDITOR, '.toc');
  const dopoB = await indice(page);
  const iB = dopoB.findIndex((x) => x.includes(NOME));
  console.log('   intorno  :', dopoB.slice(Math.max(0, iB - 2), iB + 2).join(' / '));

  // ---- C. sotto una sezione resa dalla pagina, e SOPRA il corpo
  console.log('modifica :', await inModifica(page) ? 'presa' : 'NON presa');
  const c1 = await spostaIn(page, NOME, 'ATC/CRC frequencies');
  console.log('C. sposta in «ATC/CRC frequencies»:', JSON.stringify(c1).slice(0, 300));
  console.log('   «sopra il corpo»:', await sopraIlCorpo(page, NOME) ? 'cliccato' : 'NON cliccato');

  await apri(page, BOZZA, '.doc-layout, .wrap');
  const ordine = await page.evaluate((nome) => {
    const testo = document.body.innerText;
    const iSez = testo.indexOf(nome);
    // La scheda delle frequenze è una tabella: la prima intestazione di colonna che la pagina disegna.
    const tab = [...document.querySelectorAll('table')].find((t) => /MHz|Frequen/i.test(t.innerText));
    const iTab = tab ? testo.indexOf(tab.innerText.trim().split('\\n')[0]) : -1;
    return { iSez, iTab, tabella: !!tab };
  }, NOME);
  console.log('   nel documento in bozza:', JSON.stringify(ordine),
    ordine.iSez >= 0 && ordine.iTab >= 0 ? (ordine.iSez < ordine.iTab ? '→ SOPRA la scheda (giusto)' : '→ SOTTO la scheda (SBAGLIATO)') : '→ da guardare');

  console.log('errori di pagina:', errori.length ? errori.join(' | ') : 'nessuno');
  await browser.close();
})();
