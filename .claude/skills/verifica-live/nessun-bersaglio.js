// La prova che conta: ogni selettore tolto non trova NIENTE nel DOM vero, pagina per pagina.
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const SEL = fs.readFileSync(__dirname + '/selettori-tolti.txt', 'utf8').split('\n').map(s => s.trim()).filter(Boolean);
const P = ['/services','/services/vsop','/services/vsop/libb','/services/vsop/libb/vipi',
 '/services/vsop/libb/airports','/services/vsop/libb/airports?icao=LIBD',
 '/services/vsop/libb/airports/editor?icao=LIBD','/services/vsop/libb/apps/vipi?app=LIBD_CS0_APP',
 '/services/vsop/libb/apps/editor?app=LIBD_CS0_APP','/services/vsop/libb/vloa','/services/vsop/libb/editor',
 '/services/vsop/live/libb_es_ctr','/services/vsop/admin/sector-structure','/services/vsop/admin/transfers',
 '/services/vsop/admin/airports','/services/vsop/admin/neighbours','/services/vsop/admin/acc',
 '/services/vsop/versions','/services/vsop/tasks','/services/vsop/admin/audit','/services/vsop/admin/sources',
 '/services/vsop/admin/diagnostics','/services/vsop/admin/grants','/services/vsop/admin/documents',
 '/services/vsop/guide','/services/vsop/search?q=li','/services/vsop/changed','/services/vsop/aor3d/acc/libb',
 '/services/profile-swapper'];
(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless:'new', args:['--window-size=1600,1000'], defaultViewport:{width:1600,height:1000} });
  const trovati = {};
  for (const u of P) {
    const p = await b.newPage();
    try {
      await p.goto('http://localhost:5099'+u, { waitUntil:'domcontentloaded', timeout:60000 });
      await p.waitForFunction(() => !!window.Blazor, { timeout:60000 }).catch(()=>{});
      await sleep(1500);
      // apre tutto quello che si apre: i <details> chiusi nascondono meta' del DOM
      await p.evaluate(() => document.querySelectorAll('details').forEach(d => d.open = true));
      await sleep(1200);
      const hit = await p.evaluate(sels => {
        const out = [];
        for (const s of sels) {
          try { if (document.querySelector(s)) out.push(s); } catch (e) { out.push('SELETTORE NON VALIDO: ' + s); }
        }
        return out;
      }, SEL);
      for (const h of hit) (trovati[h] = trovati[h] || []).push(u);
    } catch (e) { console.log('  ! ' + u + ' ' + e.message.slice(0,60)); }
    await p.close();
  }
  const chiavi = Object.keys(trovati);
  console.log('selettori provati: ' + SEL.length + ' su ' + P.length + ' pagine');
  console.log('selettori che trovano ancora qualcosa: ' + chiavi.length);
  for (const k of chiavi) console.log('  ' + k + '  ->  ' + trovati[k].join(', '));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
