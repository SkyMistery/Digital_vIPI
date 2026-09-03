# `atc-archiver` — la copia di quel che gira

⚠️ **Questo è il BUNDLE, non il sorgente.** È l'output compilato che sta davvero su Cloudflare, scaricato
con `wrangler init --from-dash atc-archiver` il 3 settembre 2026. Si riconosce dal `// src/index.ts` in
testa e dagli helper `__name` di esbuild.

🔴 **Il sorgente TypeScript non è in questo repository e non è su questa macchina.** Sta su un'altra, o è
andato perso. Finché resta così:

- **chi ripubblica dal TypeScript cancella il keep-alive di vIPI** senza accorgersene, e il sintomo — i
  giri periodici che smettono di partire — è invisibile finché qualcuno non apre la Diagnostica;
- questa copia serve a **vedere che cosa si è perso**, non a sostituire il sorgente.

Se il TypeScript salta fuori, le modifiche da riportare sono **due**, e sono la stessa cosa in due tempi:

1. `pingVipi()` chiamata **per prima** nel `scheduled`, prima di qualunque accesso a D1;
2. i **ping extra** dentro lo stesso giro — `PING_EXTRA_MS` + `pingVipiTraUnPo()`, lanciati con
   `ctx.waitUntil` e **mai attesi in linea** — perché un ping al minuto sveglia il processo e non lo tiene
   su (58 avvii in un'ora, misurati).

Il perché di tutt'e due sta in [`../LEGGIMI-ATC-ARCHIVER.md`](../LEGGIMI-ATC-ARCHIVER.md).

⚠️ `wrangler.jsonc` diceva `"main": "src/index.js"` e qui il bundle è **piatto**: `wrangler deploy` non
avrebbe trovato niente. Corretto in `"main": "index.js"` il 3 settembre 2026.

## Ripubblicare da qui

```powershell
cd deploy/cloudflare/atc-archiver
wrangler deploy
```

⚠️ I **segreti** (`ALERT_EMAIL`, `ALERT_FROM`, `RESEND_API_KEY`) non stanno qui e non devono starci: vivono
su Cloudflare e **sopravvivono** al deploy, perché non fanno parte dello script.
