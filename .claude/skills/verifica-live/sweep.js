// Battuta larga sul TEMA SCURO: su ogni pagina cerca gli elementi con un fondo dipinto quasi bianco,
// cioe' quelli che NON si sono girati. E' il controllo che mancava il 22 agosto e che avrebbe preso
// subito la legenda del visore 3D (fondo bianco scritto a mano, testo quasi bianco sopra).
//
//   node sweep.js
//
// Un foglio che sfugge alla passata sui token non fa fallire nessun test e non si vede nel tema chiaro:
// e' la classe di errore piu' insidiosa di tutto il lavoro sul brand.
//
// ⚠️ Falsi positivi noti: la pastiglia ACC attiva sulla barra blu (`a.active`) e' bianca DI PROPOSITO.
// La risalita degli antenati si ferma al primo fondo scuro e non riesce a riconoscerla: due sospetti
// attesi su /vsop/<acc>. Tutto il resto va guardato.
const puppeteer=require('puppeteer-core');
const EDGE='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const PAGINE=['/vsop','/vsop/lirr','/vsop/lirr/airports','/vsop/admin/struttura','/vsop/guida',
  '/vsop/aor3d/acc/libb','/vsop/aor3d/acc/limm','/vsop/live','/vsop/versioni','/vsop/admin/confinanti',
  '/vsop/admin/aeroporti','/vsop/search?q=li'];
const TROVA=()=>{
  const val=(c)=>{ if(!c) return null;
    if(c[0]==='#'){let h=c.slice(1); if(h.length===3)h=[...h].map(x=>x+x).join(''); return [0,2,4].map(i=>parseInt(h.slice(i,i+2),16));}
    const m=c.match(/-?[\d.]+/g); if(!m||m.length<3) return null;
    const srgb=/^color\(/.test(c); const a=m.length>3?parseFloat(m[3]):1;
    if(a<0.5) return null;                       // troppo trasparente per essere "il fondo"
    return m.slice(0,3).map(x=>srgb?parseFloat(x)*255:parseFloat(x));};
  const lum=(ch)=>0.2126*ch[0]+0.7152*ch[1]+0.0722*ch[2];
  const out=[];
  document.querySelectorAll('.vipi-root *').forEach(el=>{
    const cs=getComputedStyle(el);
    if(cs.display==='none'||!el.getClientRects().length) return;
    const bg=val(cs.backgroundColor);
    if(!bg) return;
    // un fondo chiaro DENTRO la barra blu o su un pieno di brand e' legittimo: si risale per capirlo
    let p=el.parentElement, suBrand=false;
    while(p&&p!==document.body){ const c=val(getComputedStyle(p).backgroundColor);
      if(c){ if(lum(c)<128) break; }
      if(/topbar|tb-menu|hero|metar|tip\b/.test(p.className||'')) {suBrand=true;break;}
      p=p.parentElement;}
    if(suBrand) return;
    if(lum(bg)>210) out.push({tipo:'FONDO CHIARO NEL TEMA SCURO',cls:(el.className||'').toString().slice(0,52),tag:el.tagName,bg:cs.backgroundColor.slice(0,40)});
  });
  return out;
};
(async()=>{
const b=await puppeteer.launch({executablePath:EDGE,headless:'new',args:['--no-sandbox','--enable-unsafe-swiftshader'],defaultViewport:{width:1500,height:1000}});
const p=await b.newPage();
await p.emulateMediaFeatures([{name:'prefers-color-scheme',value:'dark'}]);
let tot=0;
for(const u of PAGINE){
  try{
    await p.goto('http://localhost:5034'+u,{waitUntil:'networkidle2',timeout:40000});
    await new Promise(r=>setTimeout(r,u.includes('aor3d')?3500:1000));
    const r=await p.evaluate(TROVA);
    const uniq=[...new Map(r.map(x=>[x.cls+x.tag,x])).values()];
    tot+=uniq.length;
    console.log((uniq.length?'⚠ ':'  ')+u.padEnd(30)+uniq.length+' sospetti '+(uniq.length?JSON.stringify(uniq.slice(0,5)):''));
  }catch(e){console.log('  '+u.padEnd(30)+'ERRORE '+e.message.slice(0,60));}
}
console.log('\ntotale sospetti:',tot);
await b.close();})();
