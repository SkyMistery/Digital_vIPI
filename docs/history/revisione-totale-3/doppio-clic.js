// Incollato nel browser integrato (javascript_tool). Doppio clic «sincrono» su elementi cliccabili
// che NON scrivono; dopo ciascuno controlla se il circuito Blazor è caduto. Si ferma al primo crollo.
(async () => {
  const PERICOLOSI = /elimin|delete|remove|rimuov|revoc|revoke|pubblic|publish|import|sciogli|dissolve|cancel|annull|sblocc|unlock|reset|svuota|✕|🗑|finish|fine modifica|start editing|inizia|edit|modific|crea|create|salva|save|applica|apply|copia|copy|sposta|move/i;
  const morto = () => { const u = document.querySelector('#blazor-error-ui'); return !!u && getComputedStyle(u).display !== 'none'; };
  const nome = (e) => e.tagName.toLowerCase() + '.' + [...e.classList].slice(0, 2).join('.') + ' «' + (e.innerText || e.title || '').trim().replace(/\s+/g, ' ').slice(0, 30) + '»';
  const cand = [...document.querySelectorAll('main *')].filter((e) => {
    if (!e.offsetParent || e.closest('header, nav, .topbar')) return false;
    const cs = getComputedStyle(e);
    if (cs.cursor !== 'pointer' && e.tagName !== 'BUTTON' && e.tagName !== 'SUMMARY') return false;
    if (e.tagName === 'A' && e.getAttribute('href') && !e.getAttribute('href').startsWith('#')) return false;
    if (e.disabled || e.tagName === 'INPUT' || e.tagName === 'SELECT' || e.tagName === 'TEXTAREA') return false;
    const t = (e.innerText || '') + ' ' + (e.title || '') + ' ' + (e.getAttribute('aria-label') || '');
    if (PERICOLOSI.test(t)) return false;
    // solo l'elemento più esterno di una catena di cliccabili
    return !(e.parentElement && getComputedStyle(e.parentElement).cursor === 'pointer');
  });
  const provati = [];
  for (const e of cand.slice(0, 12)) {
    if (!e.isConnected) continue;
    e.click(); e.click();
    await new Promise((r) => setTimeout(r, 1500));
    provati.push(nome(e));
    if (morto()) return { pagina: location.pathname, crollo: true, su: nome(e), provati };
  }
  return { pagina: location.pathname, crollo: false, candidati: cand.length, provati };
})()
