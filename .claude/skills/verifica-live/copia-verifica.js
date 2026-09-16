// Verifica della COPIA DEL DATABASE (§A47, docs/feature/2026-09-16-copia-del-database.md).
//
//   BASE=http://localhost:5034 node copia-verifica.js
//
// Il sito deve girare su MariaDB (Persistence__Provider=MySql), con l'identità di sviluppo Admin. Il giro:
// la scheda in Diagnostica c'è; il clic sul tasto SCARICA davvero (il router di Blazor non se lo prende, il
// circuito resta su e la pagina non cambia indirizzo); il file è un gzip che comincia con la riga della copia e
// finisce con quella di chiusura; ricaricando, la scheda dice «completata» con la stessa impronta.
// ⚠️ Scrive due righe nel registro di audit del database a cui punta: si usa su una COPIA.
const fs = require('fs');
const os = require('os');
const path = require('path');
const zlib = require('zlib');
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5034';
const PAGINA = BASE + '/services/vsop/admin/diagnostics';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  if (/ivao\.aero/.test(BASE)) { console.log('ROSSO: questo script scrive nel registro, non si usa su produzione'); process.exit(2); }
  const esiti = [];
  const nota = (nome, ok, dettaglio) => {
    esiti.push(ok);
    console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`);
  };

  const cartella = fs.mkdtempSync(path.join(os.tmpdir(), 'vipi-copia-'));
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errori = [];
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });
  page.on('pageerror', (e) => errori.push('pageerror: ' + e.message));
  const cdp = await page.target().createCDPSession();
  await cdp.send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: cartella, eventsEnabled: true });

  const apri = async () => {
    await page.goto(PAGINA, { waitUntil: 'networkidle2' });
    for (let i = 0; i < 60 && !(await page.evaluate(() => !!window.Blazor)); i++) await sleep(1000);
    await page.waitForSelector('a[href$="/diagnostics/database-backup"]', { timeout: 30000 });
    await sleep(1500); // il circuito aggancia i gestori dopo il prerender
  };
  const testoScheda = () => page.evaluate(() => {
    const a = document.querySelector('a[href$="/diagnostics/database-backup"]');
    return a ? a.closest('.diag-card').innerText : '';
  });

  try {
    await apri();
    const prima = await testoScheda();
    nota('la scheda c\'è, col tasto', /Copia del database|Database copy/.test(prima), prima.split('\n')[0]);
    const attr = await page.evaluate(() => document.querySelector('a[href$="/diagnostics/database-backup"]').hasAttribute('download'));
    nota('il tasto è un link con download', attr, String(attr));

    const indirizzo = page.url();
    await page.click('a[href$="/diagnostics/database-backup"]');
    let file = null;
    for (let i = 0; i < 120 && !file; i++) {
      await sleep(500);
      file = fs.readdirSync(cartella).find((f) => f.endsWith('.sql.gz'));
    }
    nota('il clic scarica un file', !!file, file || 'nessun file in 60 s');
    nota('la pagina non ha cambiato indirizzo', page.url() === indirizzo, page.url());
    await sleep(800);
    const avviato = await testoScheda();
    nota('la scheda dice che il download è partito', /Download avviato|Download started/.test(avviato), 'dopo il clic');
    const circuito = await page.evaluate(() => !document.querySelector('#components-reconnect-modal.components-reconnect-show, .components-reconnect-show'));
    nota('il circuito resta su', circuito, circuito ? 'nessun riquadro di riconnessione' : 'riconnessione in corso');

    let sha = null;
    if (file) {
      const testo = zlib.gunzipSync(fs.readFileSync(path.join(cartella, file))).toString('utf8');
      const righe = testo.split('\n');
      nota('prima riga della copia', righe[0] === '-- vipi-backup formato=1', righe[0]);
      const chiusura = righe[righe.length - 2] || '';
      const m = /^-- vipi-backup-fine tabelle=(\d+) righe=(\d+) istruzione-max=(\d+) sha256=([0-9a-f]{64})$/.exec(chiusura);
      nota('ultima riga di chiusura', !!m, chiusura.slice(0, 90));
      nota('DataProtectionKeys resta fuori', !/CREATE TABLE `DataProtectionKeys`/.test(testo), 'nessun CREATE');
      if (m) sha = m[4];
    }

    await apri();
    const dopo = await testoScheda();
    nota('ricaricando, la scheda dice «completata» con la stessa impronta',
      !!sha && /completata|completed/.test(dopo) && dopo.includes(sha.slice(0, 12)), dopo.split('\n').slice(-2).join(' | '));

    const veri = errori.filter((e) => !/favicon/i.test(e));
    nota('nessun errore in console', veri.length === 0, veri.slice(0, 3).join(' || ') || 'pulita');
  } catch (e) {
    nota('giro completato', false, e.message);
  } finally {
    await browser.close();
    fs.rmSync(cartella, { recursive: true, force: true });
  }
  const rossi = esiti.filter((x) => !x).length;
  console.log(rossi === 0 ? `\nTUTTO VERDE (${esiti.length})` : `\n${rossi} ROSSI su ${esiti.length}`);
  process.exit(rossi === 0 ? 0 : 1);
})();
