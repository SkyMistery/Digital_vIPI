# Le disconnessioni mentre si legge: che cosa dicono i registri, un registro nuovo, e la pagina che non si blocca — carta (30 settembre 2026)

> **Stato: ✅ ESEGUITA il 30 settembre 2026** sul ramo `fix/disconnessioni` (filone Sito, S79), allineato a `main`
> @ `da5cc59f`. Nessuna migrazione, niente `deploy/`. Provata dal vivo spegnendo il server sotto una pagina aperta.
> Nasce da una richiesta del committente (30 settembre 2026): «possiamo trovare un modo per gestire e diminuire
> queste disconnessioni? Non so se sono sempre dovute a Plesk, ho notato che su documenti come vIPI LIRF o LIMC che
> sono più lunghe avvengono più spesso mentre leggo il documento … Non potremmo mettere in produzione qualche altro
> strumento per monitorare queste cose?», con i file della cartella `diagnostica/` di produzione (24–30 settembre).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## 1. Che cosa dicono i registri di produzione (24–30 settembre 2026)

- **Il processo viene spento di continuo.** `avvii.txt` (29–30 settembre): 407 avvii in un giorno e mezzo, 387 arresti
  con `SIGTERM dal sistema`, vita mediana **56 secondi**. A ondate: dalle 03:50 alle 10:30 del 29 e dalle 23:48 del 29
  alle 07:19 del 30 un processo nasce e muore ogni minuto circa, **anche con una richiesta servita due secondi prima**
  («ultima 2s fa»). Il primo a svegliarlo è quasi sempre `/vsop/health/ready` (369 volte su 387): una sonda esterna.
  ⚠️ Non è l'«inattività» che i commenti del codice davano per scontata.
- **Più processi insieme.** Nello stesso tempo in cui quelli brevi nascono e muoiono, uno resta acceso per ore (fino
  a 2 h 38 min): Passenger ne tiene più d'uno. I registri delle richieste vedono **33–84 processi diversi al giorno**.
  Per Blazor Server è delicato: un circuito vive in UN processo.
- **Metà dei circuiti dura meno di un minuto** (su 248 circuiti WebSocket del 29–30: 77 sotto i 10 s, 166 «altro»),
  ma **solo il 2% muore insieme al suo processo**. Quindi gli spegnimenti notturni colpiscono soprattutto processi
  vuoti, e la causa delle disconnessioni DURANTE la lettura non si vedeva: il server non distingue una scheda chiusa da
  una connessione caduta.
- **Qualche browser non usa il WebSocket**: righe `GET /_blazor → 200` di **90 secondi esatti** sono il long polling di
  SignalR (il suo tempo di attesa è 90 s). Su quella strada ogni richiesta può cadere, e un proxy in mezzo la taglia.
- **Perché i documenti lunghi**: i documenti sono SSR statici, ma ogni pagina ha un'isola interattiva (il badge Live in
  barra; meteo e SID sugli aeroporti). Più si resta su una pagina, più è probabile incrociare un buco — e la vIPI di LIRF
  si legge per molti minuti. Il riquadro copriva il documento, e al «rifiutato» la pagina si ricaricava dall'inizio.

## 2. Le decisioni del committente (30 settembre 2026)

1. **Un registro delle disconnessioni** viste dai browser, in `diagnostica/` come gli altri, e riassunto in Diagnostica.
2. **Tenere il punto di lettura** quando la pagina si ricarica dopo una disconnessione.
3. **Niente riquadro sulle pagine di lettura.**
4. (Le direttive di Passenger per Plesk non in questo giro.)

## 3. Che cosa si fa

**Il registro** (`RegistroDisconnessioni`, nell'host). `vipi-riconnessione.js` segue le classi che Blazor mette sul
riquadro: all'apertura (`show`) annota da quanto la pagina era aperta, se la scheda era visibile e da quanto nascosta,
se il browser si diceva in rete; alla chiusura manda un beacon a `POST /vsop/diag/disconnessione` con l'esito —
**riagganciata** (tornato da solo), **rifiutata** (il server non conosceva più il circuito: processo spento o
ripartito), **fallita** (tentativi finiti), **abbandonata** (pagina chiusa mentre si riprovava) — e il processo che
aveva servito la pagina (`<meta name="vipi-pid">`). Il server scrive `disconnessioni-AAAA-MM-GG.tsv` col proprio
processo accanto: «stesso processo» 0 dice che quello di prima non c'è più. ⚠️ Niente VID, niente query; valori solo
nella forma attesa; sessanta righe al minuto per processo al più. Dietro il login come il resto del sito. Il riassunto
sta nella scheda «Disconnessioni viste dai browser» di Diagnostica (`DisconnessioniCard`, servizio facoltativo
`IRiepilogoDisconnessioni`) e in `tools/registro-del-giorno.py`.

**Le pagine di lettura** (`PaginaDiLettura`, `IPaginaCorrente`). Il layout sa quale pagina sta servendo (dall'endpoint,
tramite Vipi.Hosting) e, se la pagina è SSR statica — il suo tipo non porta `@rendermode` —, scrive
`data-riconnessione="silenziosa"`. Lì il riquadro non si mostra (si riprova in silenzio); se il collegamento non
torna, invece di ricaricare da sola la pagina mostra in basso un avviso discreto («Collegamento con il server perso: il
documento resta leggibile…») col tasto per ricaricare, e zittisce la barra rossa di Blazor che direbbe la stessa cosa.
Le pagine interattive (editor) tengono il comportamento di prima: lì c'è lavoro da non perdere.

**Il punto di lettura.** Ogni ricarica passata da `vipi-riconnessione.js` segna percorso e posizione in
`sessionStorage`; alla pagina nuova, se è la stessa e sono passati meno di due minuti, si torna lì (a pagina caricata e
di nuovo poco dopo, perché un documento lungo cresce ancora; se chi legge ha già mosso la pagina, vince lui). Il tasto
dell'avviso discreto non pianta la bandierina del «gesto perso»: su una pagina di lettura non c'era nessun gesto.

## 4. Prove

- Test: `RegistroDisconnessioniTests` (E2E, sull'host: la riga dal beacon, i rifiuti, i numeri stretti, il riassunto,
  lo script che parla alla rotta giusta), `PaginaDiLetturaTests` (Ui: documenti sì, editor no — se il compilatore
  smettesse di scrivere l'attributo del render mode, tutte le pagine diventerebbero «di lettura»).  Ui 1926 → 1930, E2E 498 → 507.
- **Dal vivo** sul sito di prova: vIPI di Brindisi aperta a metà (2500 px), server spento → Blazor riprova, nessun
  riquadro; server riacceso (processo nuovo) → «rifiutata», nessuna ricarica, avviso discreto, stessa posizione; riga
  nel registro (`pid 41100`, `pid_pagina 15248`, `stesso_processo 0`, buco 25 s); tasto dell'avviso → ricarica e
  ritorno a 2500 px. La scheda in Diagnostica mostra il giorno e la pagina.

## 5. Dopo il carico

Una settimana di `disconnessioni-*.tsv` dice quale dei casi pesa: se prevalgono le **rifiutate** con processo
cambiato, la cura sta in Passenger (spegnimenti, più processi: le direttive nel pannello); se prevalgono le
**riagganciate** con scheda visibile, è la rete o un proxy (Cloudflare, nginx) che taglia; se la scheda era
**nascosta**, è il browser che strozza i timer e il polso non parte.
