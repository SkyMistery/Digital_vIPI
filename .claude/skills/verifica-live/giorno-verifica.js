// §A59: apre una pagina interattiva, aspetta il circuito, la chiude. Il registro del giorno deve avere la riga
// GET /_blazor 101 con la vita del circuito, e le pagine con la loro rotta.
//   node giorno-verifica.js http://localhost:5034/services/vsop/LIBB
const puppeteer = require('puppeteer-core');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const url = process.argv[2] || 'http://localhost:5034/services/vsop/LIBB';
(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const p = await b.newPage();
  await p.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await p.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await new Promise((r) => setTimeout(r, 4000));
  await b.close();
  console.log('circuito aperto ~4 s e chiuso');
})();
