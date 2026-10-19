# F6 — Le correzioni dell'AIP espanso (19 ottobre 2026)

> Carta madre: [`2026-10-18-f6-pdf-aip.md`](2026-10-18-f6-pdf-aip.md) (§3 «L'AIP espanso, e il file per vSOP», §6 il
> lavoro). Stato: **in corso** — domanda e decisioni del committente del 19 ottobre; il resto è proposta dell'agente,
> da confermare sul banco.

## §0 — La domanda

Il committente (19 ottobre): «rendiamo editabile anche l'AIP espanso, così da poter apportare correzioni o aggiunte?».

Oggi l'AIP espanso è **solo quel che i PDF dicono**: ogni «Leggi» riscrive `<ciclo>\li.json` da capo
(`AipEspanso.Leggi`). Una modifica scritta a mano lì dentro sparirebbe alla lettura dopo e al ciclo dopo, e non si
saprebbe più che cosa viene dal PDF e che cosa da noi.

## §1 — Le decisioni del committente (19 ottobre)

- **D1 — Che cosa si corregge**: gli **errori dell'AIP stesso** (un dato sbagliato nel PDF: Viterbo, quel che si segnala a
  ENAV) e le **aggiunte da altre fonti** (AIP SUP, AIC, e il resto). Gli errori del *lettore* dei PDF no: quelli si
  correggono nell'importer.
- **D2 — Quanto dura una correzione**: **da un ciclo all'altro**, finché non la supera un PDF (ENAV ha cambiato quel
  dato) o un'altra correzione o aggiunta sullo stesso oggetto.

La regola G5 (dati **mai da vIPI**) resta com'era: una fonte «altra» è un documento fuori da vIPI. Se il committente
la vuole diversa, lo dice.

## §2 — La proposta (dell'agente)

### Uno strato, non una modifica del file del ciclo

Le correzioni stanno in un file loro nel repo dell'AIP espanso, **`correzioni\li.json`** (uno per Stato, fuori dalle
cartelle dei cicli: valgono per tutti). «Leggi» compone il modello dei PDF, **poi** applica le correzioni ancora in
piedi, e scrive `<ciclo>\li.json` già corretto: il confronto col sector, la mappa, «Porta nel sector» e il file per
vSOP le vedono senza cambiare niente.

Ogni correzione è **un oggetto intero** del modello (un'area, un punto, una radioassistenza…), non un campo:

| Campo | Cosa dice |
|---|---|
| `id` | chi è (`2026-10-19-1`), per dire «superata dalla …» |
| `kind` | il tipo: `airspace`, `route`, `holding`, `navaid`, `point`, `light`, `aerodrome`, `hazard` |
| `key` | l'oggetto, con la chiave del modello (`LI R107A`, `ABRUS`, `PIS VOR/DME`, `LIRF`) |
| `op` | `change` (correggi), `add` (aggiungi), `remove` (togli) |
| `before` | l'oggetto **com'era nel PDF** quando la correzione è nata; lo scrive il Lab alla prima lettura |
| `after` | l'oggetto com'è dopo la correzione (per `remove` niente) |
| `reason` | perché (obbligatorio) |
| `source` | la fonte (obbligatoria): «errore AIP, segnalato a ENAV il …», «AIP SUP 123/26» |
| `since` | il ciclo in cui è nata |
| `superseded` | da chi è stata superata: `AIP 2612`, oppure l'`id` della correzione dopo; vuoto se è in piedi |

### Quando una correzione si applica, e quando è superata (D2)

A ogni «Leggi», per ogni correzione in piedi:
- **`change`/`remove`**: l'oggetto del PDF si confronta con `before` (**senza la pagina**: una pagina ristampata con
  la data nuova e lo stesso dato non conta). Uguale → la correzione si applica. Diverso → **superata dal PDF**
  (`AIP <ciclo>`): ENAV ha cambiato quel dato, vale il PDF. L'oggetto non c'è più nel PDF → superata anche lei.
- **`add`**: il PDF non ha quell'oggetto → si aggiunge. Il PDF ora lo ha → **superata dal PDF**.
- **Due correzioni sullo stesso oggetto**: vale l'ultima, la prima diventa «superata dalla `<id>`».
- Una correzione che non si può applicare (fonte o motivo vuoti, `after` che non si legge, un PDF con due oggetti
  della stessa chiave) **non si applica e si dice**, come le righe che il lettore non capisce.

Le superate restano nel file con `superseded` scritto: la storia la tiene anche git, ma così si legge in un posto
solo che cosa abbiamo corretto e quando ENAV ci ha dato ragione. «Leggi» dice quante ne ha applicate, quali sono
diventate superate e quali non vanno.

### Che cosa resta fuori, per ora

Le **carte ATC SMAC** (`MinimumAltitudeArea`): i settori non hanno una chiave loro (lo stesso scalo ha più settori alla
stessa quota), e quando la carta cambia si riscrive tutta (il gesto «Sostituisci» della carta madre). Si decide quando
servirà.

## §3 — Le slice

| # | Slice | Finita quando (misurato) |
|---|---|---|
| 1 | Il file delle correzioni (`Aip`, riusabile come il resto del modello) e l'applicazione a ogni «Leggi», col resoconto; il file si scrive a mano | sul banco una correzione, un'aggiunta e una rimozione scritte a mano finiscono in `2610\li.json`, e una correzione con un `before` diverso dal PDF esce «superata» |
| 2 | Nel Lab, «Correggi», «Aggiungi», «Togli» per le **aree** (sulla riga del confronto e sulla mappa), col campo «incolla coordinate» per la forma; la registrazione nel repo come sempre | una correzione fatta dal Lab arriva nella PR dell'AIP espanso |
| 3 | Gli altri tipi (punti, radioassistenze, attese, scali, rotte, luci, pericoli), un modulo per tipo | da decidere tipo per tipo |

## §4 — Il lavoro

### Slice 1 — il file delle correzioni: fatta (19 ottobre)

- **Il modello** (`src/Aip/AipCorrections.cs`, nel progetto `Aip`, senza dipendenze nuove): `AipCorrection`,
  `AipCorrectionFile` (schema 1, la versione si legge prima del resto come per il modello), `AipCorrections.Apply`
  (modello dei PDF + file → modello corretto, file aggiornato, un esito per correzione), `KeyOf` (la chiave di ogni
  tipo: aree `Key`, rotte e punti l'identificativo, radioassistenze `Key`, luci e pericoli il nome, scali l'ICAO, attese
  `ABRUS 250 left` / `PIS VOR/DME 027 right`). Il confronto con `before` si fa sul JSON compatto senza `page`.
- **Nel Lab**: «Leggi» applica `correzioni\li.json` prima di scrivere `<ciclo>\li.json`; riscrive il file delle
  correzioni solo se è cambiato (un `before` o un `since` scritto, una superata). Un file che non si legge, o di un altro
  Stato, **ferma la lettura** e `li.json` non si scrive: scritto senza correzioni le toglierebbe in silenzio alla
  registrazione dopo. L'esito ha una riga «Correzioni: 3 applicate · superate dal PDF: … · non vanno: …». I confini del
  repo (`RepoDellAip.DentroIConfini`) ora comprendono `correzioni/<Stato>.json`: si registra col ciclo.
- **Test**: `Aip` 37 → 48 (le regole di §2, una per una, e il file che va e torna uguale), Lab 1 190 → 1 195 (le
  correzioni in «Leggi», il file rotto che ferma tutto, i confini).
- **«Finita quando» misurato sul banco** (clone di prova `aip/2610`, 148 PDF): un file scritto a mano con quattro voci —
  `change` di ABADI (latitudine +0,01°), `add` di un punto PROVA, `remove` di MILANO FIR, `change` di ABAKO con un
  `before` diverso dal PDF — dà «Correzioni: 3 applicate · superate dal PDF: ABAKO»; in `2610\li.json` 1 095 aree e
  1 255 punti (prima 1 096 e 1 254), diff di 10 righe aggiunte e 107 tolte; nel file delle correzioni `since` 2610 su
  tutte, `before` scritto per ABADI e MILANO FIR, `superseded: "AIP 2610"` su ABAKO. Una seconda «Leggi»: «3
  applicate», il file delle correzioni non si riscrive. Il clone poi rimesso com'era (niente registrato).

**Prossima: slice 2**, «Correggi», «Aggiungi», «Togli» per le aree dentro il Lab.
