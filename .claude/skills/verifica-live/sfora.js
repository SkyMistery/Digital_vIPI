const puppeteer = require('puppeteer-core');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const url = process.argv[2];
(async () => {
  const b = await puppeteer.launch({ executablePath: EDGE, headless:'new', args:['--window-size=1600,1000'], defaultViewport:{width:1600,height:1000} });
  const p = await b.newPage();
  await p.goto(url, { waitUntil:'domcontentloaded', timeout:60000 });
  await p.waitForFunction(() => !!window.Blazor, { timeout:60000 }).catch(()=>{});
  await sleep(2000);
  const r = await p.evaluate(() => {
    const de = document.documentElement;
    const largo = de.clientWidth;
    const colpevoli = [];
    for (const e of document.querySelectorAll('body *')) {
      const cs = getComputedStyle(e);
      if (cs.overflowX === 'auto' || cs.overflowX === 'scroll') continue;   // scorre per costruzione
      const r = e.getBoundingClientRect();
      if (r.right > largo + 1) colpevoli.push({
        tag: e.tagName, cls: (e.className || '').toString().slice(0, 60),
        right: Math.round(r.right), w: Math.round(r.width) });
    }
    return { clientW: largo, scrollW: de.scrollWidth, colpevoli: colpevoli.slice(0, 12) };
  });
  console.log(JSON.stringify(r, null, 1));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
