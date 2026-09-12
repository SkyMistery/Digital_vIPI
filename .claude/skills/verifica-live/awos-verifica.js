// Il quadro vAWOS provato sul PACCHETTO pubblicato (JS minificato), non sul sorgente.
// Il rischio che copre: vipi-awos.js e vipi-boot.js passano dall'ottimizzatore, e un modulo che si
// registra fra i moduli pigri e' esattamente il genere di cosa che una minificazione puo' rompere in
// silenzio - la pagina si vede, e sta ferma.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.BASE || 'http://localhost:5199';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const esiti = [];
  const nota = (nome, ok, dettaglio) => {
    esiti.push({ nome, ok });
    console.log(`${ok ? 'OK  ' : 'ROSSO'} ${nome} — ${dettaglio}`);
  };

  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errori = [];
  const chiamateApi = [];
  page.on('console', (m) => { if (m.type() === 'error') errori.push(m.text()); });
  page.on('pageerror', (e) => errori.push('pageerror: ' + e.message));
  page.on('request', (r) => { if (r.url().includes('/services/vawos/api/')) chiamateApi.push({ t: Date.now(), url: r.url() }); });

  try {
    // 1. I due asset nuovi arrivano, e sono minificati.
    for (const f of ['vipi-awos.css', 'vipi-awos.js']) {
      const r = await page.goto(`${BASE}/_content/Vipi.Ui/${f}`, { waitUntil: 'domcontentloaded' });
      const t = await page.evaluate(() => document.body.innerText);
      nota(`${f} servito e minificato`, r.status() === 200 && t.length > 500 && t.split('\n').length < 30,
        `HTTP ${r.status()}, ${t.length} caratteri, ${t.split('\n').length} righe`);
    }

    // 2. L'ELENCO: la pagina senza ICAO mostra gli scali con un documento pubblicato.
    await page.goto(`${BASE}/services/vawos`, { waitUntil: 'networkidle2' });
    const scali = await page.$$eval('.awos-elenco a, .awos-sel option', (a) => a.length);
    nota('l elenco degli scali c e', scali > 0, `${scali} voci`);

    // 3. IL PASSAGGIO CHE HA GIA' ROTTO UNA VOLTA: dall'elenco a uno scalo con la navigazione ENHANCED
    //    (un <a> interno, non un page.goto: e' il percorso di scoperta vero).
    chiamateApi.length = 0;
    const href = await page.$$eval('.awos-elenco a', (a) => (a[0] ? a[0].getAttribute('href') : null))
      .catch(() => null);
    if (href) {
      await page.click('.awos-elenco a');
      // ⚠️ Con la navigazione ENHANCED non c'e' nessuna navigazione da attendere: Blazor rattoppa il DOM.
      // Si aspetta il BERSAGLIO, non l'evento.
      await page.waitForSelector('.awos-eta', { timeout: 15000 });
    } else {
      await page.goto(`${BASE}/services/vawos/lirf`, { waitUntil: 'networkidle2' });
      await page.waitForSelector('.awos-eta', { timeout: 15000 });
    }
    const icao = await page.$eval('.awos', (e) => e.dataset.awosIcao).catch(() => null);
    nota('il quadro di uno scalo si apre', !!icao, `ICAO nel DOM: ${icao} (arrivato ${href ? 'con un clic (enhanced)' : 'per indirizzo'})`);

    // 4. Il foglio di stile e' in vigore (il quadro e' scuro, non testo nudo).
    const fondo = await page.$eval('.awos', (e) => getComputedStyle(e).backgroundColor);
    nota('il foglio vipi-awos.css e in vigore', fondo !== 'rgba(0, 0, 0, 0)' && fondo !== 'transparent', `sfondo .awos = ${fondo}`);

    // 5. IL MODULO E' VIVO: l'eta' del dato cresce, e l'API viene richiamata.
    const eta1 = await page.$eval('.awos-eta', (e) => e.textContent.trim()).catch(() => null);
    await sleep(4000);
    const eta2 = await page.$eval('.awos-eta', (e) => e.textContent.trim()).catch(() => null);
    nota('l eta del dato scorre (il JS minificato gira)', !!eta1 && eta1 !== eta2 && eta2 !== '—', `«${eta1}» -> «${eta2}»`);
    nota('il modulo interroga l API', chiamateApi.length > 0, `${chiamateApi.length} chiamate a /services/vawos/api/`);

    // 6. Il contenuto: strisce di pista, la pastiglia LVP, il METAR grezzo.
    const q = await page.evaluate(() => ({
      strisce: document.querySelectorAll('.awos-strip').length,
      lvp: (document.querySelector('.awos-pastiglia.lvp') || {}).textContent || null,
      metar: (document.querySelector('[data-awos-pan="report"] div') || {}).textContent || null,
      rvr: Array.from(document.querySelectorAll('.awos-rvr-l')).map((e) => e.textContent.trim()),
      attiva: (document.querySelector('[data-awos="attiva"]') || {}).textContent || null,
    }));
    nota('ci sono le strisce di pista', q.strisce > 0, `${q.strisce} strisce`);
    nota('la pastiglia LVP c e', !!q.lvp, `«${(q.lvp || '').trim()}»`);
    nota('il METAR grezzo c e', !!q.metar && q.metar.length > 10, `${(q.metar || '').trim().slice(0, 60)}…`);
    nota('le celle RVR portano il nome della TESTATA', q.rvr.length > 0 && q.rvr.every((k) => !/MID|TDZ/i.test(k)),
      q.rvr.length ? q.rvr.join(' · ') : '(nessuna cella)');
    nota('la riga dice CHI ha scelto la pista', !!q.attiva && /from /.test(q.attiva), (q.attiva || '').trim());

    // 7. USCENDO, i timer si spengono (il difetto della revisione: chiamate due minuti dopo).
    await page.goto(`${BASE}/services/vsop`, { waitUntil: 'networkidle2' });
    chiamateApi.length = 0;
    await sleep(8000);
    nota('uscendo, il modulo si spegne', chiamateApi.length === 0, `${chiamateApi.length} chiamate dopo aver lasciato la pagina`);

    // 8. Il cancello: uno scalo senza documento pubblicato non mostra niente.
    await page.goto(`${BASE}/services/vawos/lixx`, { waitUntil: 'networkidle2' });
    const testo = await page.evaluate(() => document.body.innerText);
    const pannello = await page.$('.awos-strip');
    nota('uno scalo che il quadro non apre non mostra niente',
      !pannello && /(no published document|not in the airport directory)/i.test(testo),
      testo.replace(/\s+/g, ' ').trim().slice(0, 100));

    nota('console del browser pulita', errori.length === 0, errori.length ? errori.slice(0, 2).join(' | ') : 'nessun errore');
  } catch (e) {
    nota('la prova e arrivata in fondo', false, e.message);
  } finally {
    await browser.close();
  }

  const rossi = esiti.filter((e) => !e.ok);
  console.log(`\n===== ${rossi.length ? rossi.length + ' ROSSI' : 'TUTTO VERDE'} =====`);
  process.exit(rossi.length ? 1 : 0);
})();
