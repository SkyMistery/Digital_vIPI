// Verifica delle CHIAVI API sul pacchetto (carta docs/feature/2026-09-13-chiavi-api.md), 1.26.0.
//
//   node chiavi-verifica.js        sul publish win-x64 su :5199, identità di sviluppo = fondatore (può emettere)
//
// Il giro intero, dal vivo: la pagina crea una chiave e la mostra una volta; l'archivio risponde 200 con quella
// chiave e 401 con una inventata; dopo il ricarico la chiave non è più a schermo; revocata, torna 401.
// ⚠️ Scrive nel database a cui punta: si usa su una COPIA, mai su produzione.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const ARCHIVIO = BASE + '/vsop/api/v1/atc/sessions?limit=1';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function stato(headers) {
  const r = await fetch(ARCHIVIO, { headers });
  return r.status;
}

(async () => {
  if (/ivao\.aero/.test(BASE)) { console.log('ROSSO: questo script scrive, non si usa su produzione'); process.exit(2); }
  const esiti = [];
  const nota = (nome, ok, dettaglio) => {
    esiti.push({ nome, ok, dettaglio });
    console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`);
  };

  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const erroriConsole = [];
  page.on('console', (m) => { if (m.type() === 'error') erroriConsole.push(m.text()); });
  page.on('pageerror', (e) => erroriConsole.push('pageerror: ' + e.message));

  const apri = async () => {
    await page.goto(BASE + '/services/vsop/admin/api-keys', { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60 && !(await page.evaluate(() => !!window.Blazor)); i++) await sleep(1000);
    await page.waitForSelector('#api-key-nome', { timeout: 30000 });
    await sleep(1500); // il circuito aggancia i gestori dopo il prerender
  };

  try {
    await apri();
    nota('la pagina si apre a chi emette', true, '#api-key-nome presente');
    const voce = await page.evaluate(() => !!document.querySelector('nav.admin-nav [href="/services/vsop/admin/api-keys"], nav.admin-nav .an-link.on'));
    nota('la voce di barra c\'è', voce, voce ? 'presente' : 'assente');

    await page.type('#api-key-nome', 'Prova pacchetto');
    await page.click('#api-key-ep-archivio');
    await sleep(500);
    await page.click('button.perm-go');
    await page.waitForSelector('code.api-key-value', { timeout: 15000 });
    const chiave = (await page.$eval('code.api-key-value', (e) => e.textContent)).trim();
    nota('la chiave compare una volta', /^vipi_[A-Za-z0-9_-]{43}$/.test(chiave), chiave.slice(0, 12) + '…');

    nota('archivio con la chiave (Bearer)', (await stato({ Authorization: 'Bearer ' + chiave })) === 200, 'atteso 200');
    nota('archivio con la chiave (X-Api-Key)', (await stato({ 'X-Api-Key': chiave })) === 200, 'atteso 200');
    nota('archivio con una chiave inventata', (await stato({ Authorization: 'Bearer vipi_inventata' })) === 401, 'atteso 401');
    nota('archivio senza chiave (passaggio)', (await stato({})) === 200, 'atteso 200 con Api:RichiediChiave spento');

    await apri();
    // ⚠️ Il campo c'è PRIMA dell'elenco, che si carica dopo: senza aspettare le righe, «la chiave non c'è»
    // sarebbe vero su una pagina ancora vuota — una prova che non distingue.
    await page.waitForSelector('.api-key-row', { timeout: 15000 });
    const html = await page.content();
    nota('dopo il ricarico la chiave non c\'è più', !html.includes(chiave), 'resta solo il prefisso');
    // ⚠️ Sul TESTO, non sull'HTML: nel render interattivo Blazor mette un `<!--!-->` fra il prefisso e i puntini.
    const testoRighe = await page.$$eval('.api-key-row', (rr) => rr.map((r) => r.innerText).join('\n'));
    nota('il prefisso resta in elenco', testoRighe.includes(chiave.slice(0, 12) + '…'), chiave.slice(0, 12) + '…');

    // Revoca: il tasto apre la conferma in riga, poi «Sì, revoca» / «Yes, revoke».
    const righe = await page.$$('.api-key-row');
    let riga = null;
    for (const r of righe) if ((await r.evaluate((e) => e.textContent)).includes(chiave.slice(0, 12))) riga = r;
    const tasti = async (el) => el.$$('button');
    for (const b of await tasti(riga)) if (/Revoca|Revoke/.test(await b.evaluate((e) => e.textContent))) { await b.click(); break; }
    await sleep(800);
    let confermato = false;
    for (const b of await page.$$('button')) {
      if (/Sì, revoca|Yes, revoke/.test(await b.evaluate((e) => e.textContent))) { await b.click(); confermato = true; break; }
    }
    await sleep(1500);
    nota('revoca confermata', confermato, confermato ? 'cliccato' : 'tasto di conferma non trovato');
    nota('archivio con la chiave revocata', (await stato({ Authorization: 'Bearer ' + chiave })) === 401, 'atteso 401');

    nota('console del browser pulita', erroriConsole.length === 0, erroriConsole.length ? erroriConsole.join(' | ') : 'nessun errore');
  } catch (e) {
    nota('eccezione', false, e.message);
  } finally {
    await browser.close();
  }

  const rossi = esiti.filter((e) => !e.ok).length;
  console.log(rossi ? `\n===== ${rossi} ROSSI =====` : '\n===== TUTTO VERDE =====');
  process.exit(rossi ? 1 : 0);
})();
