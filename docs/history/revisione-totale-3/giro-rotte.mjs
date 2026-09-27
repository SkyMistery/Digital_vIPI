// Giro delle rotte con quattro identità (istanze locali sulla copia vipi_rev3). Solo GET, nessuna scrittura:
// la copia del DB NON si chiede (database-backup) sull'istanza Admin.
import { writeFileSync } from 'node:fs';

// Sessioni e VID di prova si passano da fuori: nel repo niente dati veri della copia del DB.
// SESSIONE_ALTRUI, SESSIONE_MIA, VID_ALTRUI (es. da una SELECT sulla copia locale).
const SA = process.env.SESSIONE_ALTRUI || '1', SM = process.env.SESSIONE_MIA || '2', VA = process.env.VID_ALTRUI || '123456';
const ID = { admin: 5199, editor: 5198, nessuno: 5197, anonimo: 5196 };
const R = [
  '/', '/services', '/services/coordinates', '/services/profile-swapper',
  '/services/stats', '/services/stats/division', '/services/stats/world',
  '/services/stats/session/' + SA, '/services/stats/session/' + SM,
  '/services/stats/user/' + VA, '/services/stats/user/704798',
  '/services/vawos', '/services/vawos/LIBD',
  '/services/vsop', '/services/vsop/admin/acc', '/services/vsop/admin/airports', '/services/vsop/admin/airspace',
  '/services/vsop/admin/api-keys', '/services/vsop/admin/attachments', '/services/vsop/admin/audit',
  '/services/vsop/admin/diagnostics', '/services/vsop/admin/glossary', '/services/vsop/admin/navaids',
  '/services/vsop/admin/neighbours', '/services/vsop/admin/pending', '/services/vsop/admin/permissions',
  '/services/vsop/admin/sector-structure', '/services/vsop/admin/sources', '/services/vsop/admin/tasks',
  '/services/vsop/admin/transfers', '/services/vsop/admin/translations',
  '/services/vsop/airspace', '/services/vsop/aor3d/acc/LIBB', '/services/vsop/aor3d/app/LIBD',
  '/services/vsop/changed', '/services/vsop/editor/new-document', '/services/vsop/guide',
  '/services/vsop/live', '/services/vsop/live/LIBD_TWR', '/services/vsop/mil',
  '/services/vsop/release/11', '/services/vsop/release/451',
  '/services/vsop/screens', '/services/vsop/search', '/services/vsop/search?q=LIBD', '/services/vsop/sectorfile',
  '/services/vsop/tasks', '/services/vsop/versions',
  '/services/vsop/libb', '/services/vsop/libb/airports', '/services/vsop/libb/airports?icao=LIBD',
  '/services/vsop/libb/airports?icao=LIBD&as=draft', '/services/vsop/libb/airports?icao=LIBD&as=release:11',
  '/services/vsop/libb/airports/editor?icao=LIBD', '/services/vsop/libb/apps', '/services/vsop/libb/apps/editor',
  '/services/vsop/libb/apps/vipi', '/services/vsop/libb/editor', '/services/vsop/libb/mil',
  '/services/vsop/libb/mil?icao=LIBV', '/services/vsop/libb/mil/editor?icao=LIBV', '/services/vsop/libb/versions',
  '/services/vsop/libb/vipi', '/services/vsop/libb/vipi?as=draft', '/services/vsop/lirr/vloa',
  '/services/vsop/lirr/vloa/editor',
  '/vsop/health', '/vsop/ready', '/vsop/ping', '/vsop/api/v1/atc/sessions', '/services/vawos/api/LIBD',
  '/api/rfo/events/prova/state', '/services/vsop/auth/accesso-non-riuscito',
  '/services/vsop/admin/diagnostics/database-backup', '/Error', '/mappa/strati',
];
const NEGATO = /Restricted access|Accesso riservato|reserved to|riservat[ao] agli|Admin only|not allowed|non autorizzat|Sign in|Accedi/i;

const out = [];
for (const [chi, porta] of Object.entries(ID)) {
  for (const r of R) {
    if (chi === 'admin' && r.includes('database-backup')) { out.push({ chi, r, st: 'SALTATA' }); continue; }
    const ctl = new AbortController(); const t = setTimeout(() => ctl.abort(), 30000);
    const t0 = performance.now();
    try {
      const res = await fetch(`http://localhost:${porta}${r}`, { redirect: 'manual', signal: ctl.signal });
      const body = await res.text();
      const title = (body.match(/<title>([^<]*)<\/title>/i) || [])[1] || '';
      const neg = (body.match(NEGATO) || [])[0] || '';
      out.push({ chi, r, st: res.status, loc: res.headers.get('location') || '', len: body.length, ms: Math.round(performance.now() - t0), title: title.trim().slice(0, 60), neg,
        cc: res.headers.get('cache-control') || '' });
    } catch (e) { out.push({ chi, r, st: 'ERR ' + e.name, ms: Math.round(performance.now() - t0) }); }
    finally { clearTimeout(t); }
  }
}
writeFileSync(new URL('./giro-rotte.json', import.meta.url), JSON.stringify(out, null, 1));
// Tabella: una riga per rotta, quattro colonne
const by = {};
for (const o of out) (by[o.r] ??= {})[o.chi] = o;
const cell = (o) => !o ? '-' : typeof o.st !== 'number' ? String(o.st) : `${o.st}${o.loc ? '→' + o.loc.slice(0, 30) : ''} ${o.len}b${o.neg ? ' NEG' : ''}`;
for (const r of R) console.log([r.padEnd(58), ...Object.keys(ID).map(k => cell(by[r][k]).padEnd(22))].join(' | '));
