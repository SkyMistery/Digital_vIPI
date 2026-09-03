var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/index.ts
var WHAZZUP_URL = "https://api.ivao.aero/v2/tracker/whazzup";
var FETCH_TIMEOUT_MS = 1e4;
var CLEANUP_DAYS = 90;
// Keep-alive: ritardi (ms) dei ping EXTRA dentro lo stesso giro di cron, oltre a quello a t=0.
// Il cron non scende sotto il minuto, quindi la frequenza vera la fa questa lista.
// 3-set-2026: si prova [30 s]. Se `avvii.txt` continua a contare un avvio al minuto, il processo
// muore prima dei 30 s e la lista va infittita (p.es. [1e4, 2e4, 3e4, 4e4, 5e4]).
var PING_EXTRA_MS = [3e4];
var index_default = {
  // HTTP handler
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === "/health") {
      return handleHealth(env);
    }
    if (url.pathname === "/sessions") {
      return handleSessions(url, env);
    }
    return new Response("Not Found", { status: 404 });
  },
  // Cron handler (ogni minuto)
  async scheduled(_event, env, ctx) {
    // ⚠️ PRIMA di qualunque accesso a D1, e non e' un dettaglio d'ordine: quando la quota D1 e'
    //    esaurita `runPoller` fallisce subito, ed e' esattamente il giorno in cui il keep-alive serve di piu'.
    await pingVipi();
    // ⚠️ I ping intermedi vanno in `waitUntil` e NON si aspettano qui: aspettarli sposterebbe di mezzo
    //    minuto il campionamento ATC, che e' il lavoro vero di questo cron.
    for (const delayMs of PING_EXTRA_MS) {
      ctx.waitUntil(pingVipiTraUnPo(delayMs));
    }
    await runPoller(env);
  }
};

/**
 * Keep-alive per vIPI (https://atc.it.ivao.aero).
 *
 * L'hosting del sito e' Plesk + Phusion Passenger, che spegne il processo per inattivita' appena il
 * traffico si ferma: vite misurate sul server, 1:00 / 1:49 / 4:52. Con il processo muoiono il
 * campionamento ATC (un giro al minuto) e meta' dei giri periodici, che aspettano un ritardo d'avvio
 * fino a 150 s.
 *
 * 🔴 UNA richiesta al minuto NON basta, ed e' MISURATO: `diagnostica/avvii.txt` del 3 settembre 2026
 *    conta 58 avvii in un'ora. Il processo parte a hh:mm:59, vive 7-15 s, si spegne in modo ORDINATO,
 *    e al minuto dopo ricomincia. Il ping SVEGLIA e non TIENE SU, e con vite cosi' corte nessun giro
 *    periodico (bootDelay da 15 s a 150 s) arriva in fondo. Per questo il cron pinga piu' volte per
 *    giro: vedi `PING_EXTRA_MS`.
 *
 * ⚠️ Sonda ECONOMICA: `/vsop/health/ready` guarda le sole condizioni critiche. `/vsop/health` include
 *    il report di consistenza, che costa e fa I/O di rete — 1440 volte al giorno sarebbe uno spreco.
 * ⚠️ Cache-buster nell'URL: davanti al sito c'e' Cloudflare, e una risposta servita dalla cache non
 *    sveglierebbe nessun processo. Il ping tornerebbe 200 e il sito resterebbe morto.
 * ⚠️ best-effort: qualunque errore si ingoia. Il keep-alive non deve MAI impedire l'archiviazione.
 */
async function pingVipi() {
  try {
    await fetch(`https://atc.it.ivao.aero/vsop/health/ready?t=${Date.now()}`, {
      headers: { "Cache-Control": "no-cache" },
      cf: { cacheTtl: 0, cacheEverything: false }
    });
  } catch {
  }
}
__name(pingVipi, "pingVipi");

/**
 * Lo stesso ping, ritardato di `delayMs` dentro lo stesso giro di cron.
 *
 * ⚠️ Il cron di Cloudflare non scende sotto il minuto: per pingare piu' spesso l'unica strada e'
 *    aspettare DENTRO l'invocazione. L'attesa non consuma CPU e sta larga nei limiti di un cron.
 * ⚠️ Va lanciato con `ctx.waitUntil`, mai atteso in linea: in linea ritarderebbe il poller.
 */
async function pingVipiTraUnPo(delayMs) {
  try {
    await new Promise((r) => setTimeout(r, delayMs));
    await pingVipi();
  } catch {
  }
}
__name(pingVipiTraUnPo, "pingVipiTraUnPo");
async function handleHealth(env) {
  try {
    const rows = await env.DB.prepare(
      "SELECT key, value FROM archiver_state"
    ).all();
    const state = {};
    for (const row of rows.results) {
      state[row.key] = row.value;
    }
    return jsonResponse({
      status: "ok",
      consecutive_failures: parseInt(state["consecutive_failures"] ?? "0", 10),
      alert_sent: state["alert_sent"] === "true",
      last_success_at: state["last_success_at"] || null,
      last_failure_at: state["last_failure_at"] || null
    });
  } catch (err) {
    return jsonResponse({ status: "error", message: String(err) }, 500);
  }
}
__name(handleHealth, "handleHealth");
async function handleSessions(url, env) {
  const callsign = url.searchParams.get("callsign");
  const from = url.searchParams.get("from");
  const to = url.searchParams.get("to");
  try {
    let query;
    if (callsign) {
      query = env.DB.prepare(
        `SELECT callsign, position, latitude, longitude, user_id, started_at, ended_at
         FROM atc_sessions
         WHERE callsign = ?
         ORDER BY started_at DESC
         LIMIT 500`
      ).bind(callsign);
    } else if (from && to) {
      if (!isValidIso(from) || !isValidIso(to)) {
        return jsonResponse({ error: "Parametri 'from' e 'to' devono essere ISO 8601 validi" }, 400);
      }
      query = env.DB.prepare(
        `SELECT callsign, position, latitude, longitude, user_id, started_at, ended_at
         FROM atc_sessions
         WHERE started_at <= ? AND (ended_at IS NULL OR ended_at >= ?)
         ORDER BY started_at DESC
         LIMIT 1000`
      ).bind(to, from);
    } else {
      return jsonResponse(
        { error: "Parametri richiesti: 'callsign' oppure 'from' + 'to'" },
        400
      );
    }
    const result = await query.all();
    return jsonResponse(result.results);
  } catch (err) {
    return jsonResponse({ error: String(err) }, 500);
  }
}
__name(handleSessions, "handleSessions");
async function runPoller(env) {
  const now = (/* @__PURE__ */ new Date()).toISOString();
  let atcs;
  try {
    atcs = await fetchWhazzup();
  } catch (err) {
    await handleFetchFailure(env, now, String(err));
    return;
  }
  await handleFetchSuccess(env, now);
  const openRows = await env.DB.prepare(
    "SELECT id, callsign FROM atc_sessions WHERE ended_at IS NULL"
  ).all();
  const openInDb = new Map(
    openRows.results.map((r) => [r.callsign, r.id])
  );
  const liveCallsigns = new Set(atcs.map((a) => a.callsign));
  const stmts = [];
  for (const atc of atcs) {
    if (!openInDb.has(atc.callsign)) {
      stmts.push(
        env.DB.prepare(
          `INSERT INTO atc_sessions (callsign, position, latitude, longitude, user_id, started_at)
           VALUES (?, ?, ?, ?, ?, ?)`
        ).bind(
          atc.callsign,
          atc.atcSession?.position ?? null,
          atc.lastTrack?.latitude ?? null,
          atc.lastTrack?.longitude ?? null,
          atc.userId,
          now
        )
      );
    }
  }
  for (const [callsign, id] of openInDb) {
    if (!liveCallsigns.has(callsign)) {
      stmts.push(
        env.DB.prepare(
          "UPDATE atc_sessions SET ended_at = ? WHERE id = ?"
        ).bind(now, id)
      );
    }
  }
  const cutoff = new Date(Date.now() - CLEANUP_DAYS * 864e5).toISOString();
  stmts.push(
    env.DB.prepare(
      "DELETE FROM atc_sessions WHERE started_at < ?"
    ).bind(cutoff)
  );
  if (stmts.length > 0) {
    await env.DB.batch(stmts);
  }
}
__name(runPoller, "runPoller");
async function fetchWhazzup() {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);
  let response;
  try {
    response = await fetch(WHAZZUP_URL, { signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`);
  }
  let data;
  try {
    data = await response.json();
  } catch {
    throw new Error("Risposta whazzup non \xE8 JSON valido");
  }
  const whazzup = data;
  if (!whazzup?.clients?.atcs || !Array.isArray(whazzup.clients.atcs)) {
    throw new Error("Struttura whazzup imprevista: clients.atcs non trovato");
  }
  return whazzup.clients.atcs.filter(
    (a) => typeof a === "object" && a !== null && typeof a.callsign === "string" && a.callsign.length > 0 && typeof a.userId === "number"
  );
}
__name(fetchWhazzup, "fetchWhazzup");
async function handleFetchSuccess(env, now) {
  await env.DB.batch([
    env.DB.prepare("INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('consecutive_failures', '0')"),
    env.DB.prepare("INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('alert_sent', 'false')"),
    env.DB.prepare("INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('last_success_at', ?)").bind(now)
  ]);
}
__name(handleFetchSuccess, "handleFetchSuccess");
async function handleFetchFailure(env, now, reason) {
  const rows = await env.DB.prepare(
    "SELECT key, value FROM archiver_state WHERE key IN ('consecutive_failures', 'alert_sent', 'last_success_at')"
  ).all();
  const state = {};
  for (const row of rows.results) {
    state[row.key] = row.value;
  }
  const failures = parseInt(state["consecutive_failures"] ?? "0", 10) + 1;
  const alertAlreadySent = state["alert_sent"] === "true";
  const lastSuccess = state["last_success_at"] || "mai";
  const threshold = parseInt(env.ALERT_THRESHOLD ?? "5", 10);
  await env.DB.batch([
    env.DB.prepare("INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('consecutive_failures', ?)").bind(String(failures)),
    env.DB.prepare("INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('last_failure_at', ?)").bind(now)
  ]);
  if (failures >= threshold && !alertAlreadySent) {
    const sent = await sendAlert(env, failures, lastSuccess, now, reason);
    if (sent) {
      await env.DB.prepare(
        "INSERT OR REPLACE INTO archiver_state (key, value) VALUES ('alert_sent', 'true')"
      ).run();
    }
  }
}
__name(handleFetchFailure, "handleFetchFailure");
async function sendAlert(env, failures, lastSuccess, now, reason) {
  try {
    const res = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${env.RESEND_API_KEY}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        from: env.ALERT_FROM,
        to: env.ALERT_EMAIL,
        subject: "\u26A0\uFE0F ATC Archiver \u2014 IVAO whazzup non raggiungibile",
        text: [
          `Fetch fallito ${failures} volte consecutive.`,
          `Motivo: ${reason}`,
          `Ultimo successo: ${lastSuccess}`,
          `Timestamp: ${now}`
        ].join("\n")
      })
    });
    return res.ok;
  } catch {
    return false;
  }
}
__name(sendAlert, "sendAlert");
function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store"
    }
  });
}
__name(jsonResponse, "jsonResponse");
function isValidIso(value) {
  return !isNaN(Date.parse(value));
}
__name(isValidIso, "isValidIso");
export {
  index_default as default
};
//# sourceMappingURL=index.js.map
