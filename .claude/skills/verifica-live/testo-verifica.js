// Prova viva della barra di formattazione (§CL). Non prova il markup — quello lo provano gli unit test —
// prova i GESTI: selezione, fuoco, e il `change` sintetico che riporta il testo nel modello Blazor.
// Dal 16 settembre 2026 (§A41) anche gli elenchi annidati: i tasti ⇤ ⇥, Tab/Maiusc+Tab e Invio che continua.
//
// ⚠️ Due trappole gia' pagate qui dentro:
//  - i campi stanno in <details> COLLASSATI: `innerText` torna vuoto e `page.type` non arriva.
//    Si apre tutto con `vipiEditorSections(true)`, l'helper dell'editor stesso.
//  - svuotare con triplo clic + Backspace cancella UNA RIGA: la prova scriveva sopra i propri avanzi
//    e ogni asserzione dopo la prima era falsa. Si svuota con Ctrl+A.
const puppeteer = require('puppeteer-core');
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const url = process.argv[2];

let ko = 0;
const dice = (ok, msg) => { if (!ok) ko++; console.log((ok ? '  OK  ' : '  KO  ') + msg); };

async function inModifica(page) {
  const entrato = await page.evaluate(() => {
    // ⚠️ `textContent` e NON `innerText`: su un elemento non reso — dentro un <details> chiuso o in un
    // aside collassato — `innerText` torna VUOTO, e il tasto sembra non esserci.
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

async function apri(page) {
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await page.waitForFunction(() => !!window.Blazor, { timeout: 90000 });
  await sleep(3000);
}

(async () => {
  const browser = await puppeteer.launch({ executablePath: EDGE, headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1100 });
  page.on('dialog', d => d.accept());

  await apri(page);
  if (!await inModifica(page)) { console.log('NESSUN tasto ✎ Modifica: mi fermo'); await browser.close(); process.exit(2); }

  const n = await page.evaluate(() => document.querySelectorAll('.rta').length);
  dice(n > 0, `campi con barra trovati: ${n}`);
  if (n === 0) { await browser.close(); process.exit(2); }

  const prep = await page.evaluate(() => {
    // il primo campo DAVVERO visibile: uno dentro una sezione chiusa non riceve né fuoco né tasti
    const rta = [...document.querySelectorAll('.rta')].find(r => r.querySelector('textarea').offsetParent !== null);
    rta.scrollIntoView({ block: 'center' });
    const ta = rta.querySelector('textarea');
    ta.id = ta.id || 'rta-prova';
    return { id: ta.id, prima: ta.value, tasti: [...rta.querySelectorAll('.rta-btn')].map(b => b.innerText) };
  });
  console.log('  ..  tasti nella barra:', JSON.stringify(prep.tasti));
  console.log('  ..  contenuto di partenza:', JSON.stringify(prep.prima));
  // Dal 18 settembre 2026 (§A73) c'e' anche «SID», in coda: lo prova `sid-verifica.js`, qui basta che ci sia.
  dice(JSON.stringify(prep.tasti) === '["B","I","U","•","1)","⇤","⇥","SID"]', 'la barra ha gli otto tasti attesi');

  const sel = `#${prep.id}`;
  const valore = () => page.$eval(sel, e => e.value);
  const tasto = async i => {
    await page.evaluate((s, i) => {
      const b = document.querySelector(s).closest('.rta').querySelectorAll('.rta-btn')[i];
      b.scrollIntoView({ block: 'center' }); b.click();
    }, sel, i);
    await sleep(500);
  };
  const svuota = async () => {
    await page.focus(sel);
    await page.keyboard.down('Control'); await page.keyboard.press('KeyA'); await page.keyboard.up('Control');
    await page.keyboard.press('Backspace');
  };
  const scriviE = async testo => { await svuota(); await page.type(sel, testo); await sleep(300); };
  const seleziona = async (a, b) => { await page.evaluate((s, a, b) => {
    const t = document.querySelector(s); t.focus(); t.setSelectionRange(a, b);
  }, sel, a, b); };
  const tutto = async () => { const v = await valore(); await seleziona(0, v.length); };

  // --- 1. grassetto sulla selezione ---
  await scriviE('alfa bravo charlie');
  await seleziona(5, 10);              // "bravo"
  await tasto(0);
  dice(await valore() === 'alfa **bravo** charlie', `grassetto sulla selezione → ${JSON.stringify(await valore())}`);

  // --- 2. lo stesso tasto premuto di nuovo TOGLIE ---
  await tasto(0);
  dice(await valore() === 'alfa bravo charlie', `il tasto è un interruttore → ${JSON.stringify(await valore())}`);

  // --- 3. corsivo e sottolineato ---
  await scriviE('alfa');
  await tutto(); await tasto(1);
  dice(await valore() === '*alfa*', `corsivo → ${JSON.stringify(await valore())}`);
  await scriviE('delta');
  await tutto(); await tasto(2);
  dice(await valore() === '__delta__', `sottolineato → ${JSON.stringify(await valore())}`);

  // --- 4. elenco puntato su più righe, battute con Enter vero ---
  await svuota();
  await page.type(sel, 'uno'); await page.keyboard.press('Enter');
  await page.type(sel, 'due'); await page.keyboard.press('Enter');
  await page.type(sel, 'tre');
  await sleep(300);
  dice(await valore() === 'uno\ndue\ntre', `tre righe battute → ${JSON.stringify(await valore())}`);
  await tutto(); await tasto(3);
  dice(await valore() === '- uno\n- due\n- tre', `elenco puntato → ${JSON.stringify(await valore())}`);

  // --- 5. da puntato a numerato, e poi via ---
  await tutto(); await tasto(4);
  dice(await valore() === '1) uno\n2) due\n3) tre', `elenco numerato → ${JSON.stringify(await valore())}`);
  await tutto(); await tasto(4);
  dice(await valore() === 'uno\ndue\ntre', `smarcato → ${JSON.stringify(await valore())}`);

  // --- 5-bis. elenchi annidati (§A41): Tab e Maiusc+Tab VERI, i tasti ⇥ ⇤, e Invio che continua ---
  await tutto(); await tasto(3);                                    // di nuovo puntato
  await seleziona(6, 6);                                            // dentro "due"
  await page.keyboard.press('Tab'); await sleep(500);
  dice(await valore() === '- uno\n-- due\n- tre', `Tab rientra la voce → ${JSON.stringify(await valore())}`);
  dice(await page.evaluate(s => document.activeElement === document.querySelector(s), sel),
       'il Tab su una voce NON porta via il fuoco');
  await page.keyboard.down('Shift'); await page.keyboard.press('Tab'); await page.keyboard.up('Shift'); await sleep(500);
  dice(await valore() === '- uno\n- due\n- tre', `Maiusc+Tab la riporta su → ${JSON.stringify(await valore())}`);
  await seleziona(6, 6); await tasto(6);
  dice(await valore() === '- uno\n-- due\n- tre', `il tasto ⇥ rientra → ${JSON.stringify(await valore())}`);
  await seleziona(7, 7); await tasto(5);
  dice(await valore() === '- uno\n- due\n- tre', `il tasto ⇤ riduce → ${JSON.stringify(await valore())}`);
  // Invio in fondo a una voce la continua allo stesso livello; Invio sulla voce vuota esce dall'elenco.
  const v5 = await valore(); await seleziona(v5.length, v5.length);
  await page.keyboard.press('Enter'); await page.type(sel, 'quattro'); await sleep(300);
  dice(await valore() === '- uno\n- due\n- tre\n- quattro', `Invio continua l'elenco → ${JSON.stringify(await valore())}`);
  await page.keyboard.press('Enter'); await page.keyboard.press('Enter'); await sleep(300);
  dice(await valore() === '- uno\n- due\n- tre\n- quattro\n', `Invio sulla voce vuota esce → ${JSON.stringify(await valore())}`);
  // Tab su un capoverso fa quel che fa ovunque: cambia campo.
  await scriviE('solo testo');
  await page.keyboard.press('Tab'); await sleep(300);
  dice(await page.evaluate(s => document.activeElement !== document.querySelector(s), sel),
       'il Tab su un capoverso cambia campo (nessuna trappola da tastiera)');
  await scriviE('uno\ndue\ntre');

  // --- 6. il cursore su UNA riga sola marca solo quella ---
  await seleziona(4, 4);               // dentro "due"
  await tasto(3);
  dice(await valore() === 'uno\n- due\ntre', `una riga sola → ${JSON.stringify(await valore())}`);

  // --- 7. le scorciatoie da tastiera ---
  await scriviE('echo');
  await tutto();
  await page.keyboard.down('Control'); await page.keyboard.press('KeyB'); await page.keyboard.up('Control');
  await sleep(400);
  dice(await valore() === '**echo**', `Ctrl+B → ${JSON.stringify(await valore())}`);
  await tutto();
  await page.keyboard.down('Control'); await page.keyboard.press('KeyU'); await page.keyboard.up('Control');
  await sleep(400);
  dice(await valore() === '__**echo**__', `Ctrl+U sopra il grassetto → ${JSON.stringify(await valore())}`);

  // --- 8. LA PROVA CHE CONTA: il testo torna nel MODELLO, non solo a schermo ---
  // Il contenuto si scrive COI TASTI, mai battendo i marcatori. Poi si ricarica la pagina: se il
  // `change` sintetico non fosse partito, sarebbe tutto sparito.
  await svuota();
  await page.type(sel, 'foxtrot golf'); await page.keyboard.press('Enter');
  await page.type(sel, 'hotel'); await page.keyboard.press('Enter');
  await page.type(sel, 'india');
  await sleep(300);
  await seleziona(0, 7); await tasto(0);                       // grassetto su "foxtrot"
  const v = await valore();
  await seleziona(v.indexOf('hotel'), v.length); await tasto(3);  // elenco sulle ultime due righe
  // ⚠️ E un secondo livello fatto col TAB: il rientro riscrive il campo da JS come i tasti, quindi deve
  // passare anche lui dal `change` sintetico — o sparisce al ricarico e nessun unit test se ne accorge.
  const v8 = await valore(); await seleziona(v8.length, v8.length);
  await page.keyboard.press('Tab'); await sleep(500);
  const scritto = await valore();
  dice(scritto.endsWith('- hotel\n-- india'), `secondo livello col Tab → ${JSON.stringify(scritto)}`);
  console.log('  ..  scritto coi soli tasti:', JSON.stringify(scritto));
  await page.evaluate(s => document.querySelector(s).blur(), sel);
  await sleep(3500);

  await apri(page);
  await inModifica(page);
  const dopo = await page.evaluate(() => {
    const t = [...document.querySelectorAll('.rta textarea')].find(x => x.offsetParent !== null);
    return t ? t.value : null;
  });
  dice(dopo === scritto, `dopo il ricarico il modello ha quel che i tasti hanno scritto → ${JSON.stringify(dopo)}`);

  // --- 9. e come si RENDE, uscendo dalla modifica ---
  await page.evaluate(() => {
    // ⚠️ NON basta cercare la spunta: nell'editor ACC decine di chip delle aree cominciano con ✓,
    // e il primo che si trova e' una di quelle. Si ancora all'ETICHETTA del tasto (Lock_FinishEdit),
    // nelle due lingue.
    const b = [...document.querySelectorAll('button')]
      .find(x => /(Fine modifica|Finish editing)/i.test(x.textContent) && !x.disabled);
    if (b) { b.scrollIntoView({ block: 'center' }); b.click(); }
  });
  await sleep(4500);
  await page.evaluate(() => { if (window.vipiEditorSections) window.vipiEditorSections(true); });
  await sleep(1500);
  const reso = await page.evaluate(() => {
    const ul = document.querySelector('ul.md-list');
    return {
      ul: ul ? ul.outerHTML : null,
      strong: [...document.querySelectorAll('strong')].map(e => e.textContent).find(t => /foxtrot/.test(t)) || null,
      br: /foxtrot/.test(document.body.innerHTML),
    };
  });
  console.log('  ..  reso:', JSON.stringify(reso));
  dice(!!reso.ul && /<li>hotel<ul class="md-list md-l2"><li>india<\/li><\/ul><\/li>/.test(reso.ul),
       'l\'elenco è reso annidato: <ul class="md-list md-l2"> dentro la voce');
  dice(reso.strong === 'foxtrot', 'il grassetto è reso come <strong>');

  await page.screenshot({ path: 'reso.png', fullPage: false });
  await browser.close();
  console.log(ko === 0 ? '\nTUTTO VERDE' : `\n${ko} CONTROLLI ROSSI`);
  process.exit(ko === 0 ? 0 : 1);
})();
