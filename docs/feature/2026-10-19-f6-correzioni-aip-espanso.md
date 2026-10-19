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

### Slice 2 — i gesti per le aree nel Lab: fatta (19 ottobre, via del committente)

- **Dove**: nella **scheda di un'area** dell'AIP espanso scelta sulla mappa, «Correggi», «Togli» e, se l'area è corretta,
  «Ritira la correzione»; la scheda dice «✎ Corretta/Aggiunta (id, dal ciclo …): perché — fonte». Nel riquadro AIP, sotto
  «Mostra l'AIP espanso sulla mappa», **«Aggiungi un'area»**. Sulle righe dei confronti i tasti non ci sono: dalla riga
  si arriva all'area sulla mappa. Il blocco «Messaggio · Registra · Manda» ora si vede col repo su un ramo `aip/…`
  anche senza una «Leggi» nella stessa sessione: una correzione si registra da sola.
- **Il modulo** (`ModuloDellArea.razor`): sezione, tipo, identificativo, nome, limiti superiore e inferiore (letti con
  `LimitText` come fa l'importer), classe, attività, note, **forma** (vuota = resta com'è; altrimenti i limiti laterali
  come li scrive l'AIP, letti con `GeometryText`: una forma sola, nessuna parola sconosciuta), perché e fonte
  obbligatori. Unità, orari, altitudine di transizione e pagina restano quelli dell'area; le fasce restano se i limiti
  non cambiano.
- **Il nucleo** (`CorrezioniDellAip`): lavora sul modello del ciclo già corretto e sul file, e li lascia **come li
  lascerebbe una «Leggi»** coi PDF (provato in ogni test: il file applicato al modello dei PDF dà lo stesso JSON). Le
  regole: `before` è sempre l'area del PDF (per un'area già corretta, il `before` di quella correzione, che diventa
  superata dalla nuova); correggere un'area aggiunta fa un'altra `add`; «Togli» su un'area aggiunta **ritira**
  l'aggiunta; «Ritira» mette `superseded: "ritirata"` e rimette l'area del PDF (una tolta torna in fondo all'elenco,
  al suo posto la rimette la «Leggi» dopo). Nel repo si scrive sul ramo del ciclo, come «Leggi»; il messaggio proposto
  è «Correzione dell'AIP espanso: R107A — perché (fonte)».
- **Test**: Lab 1 195 → 1 206 (10 del nucleo, uno dell'interfaccia che fa correggi, ritira, aggiungi e togli).
- **Provato a schermo sul banco** (clone `aip/2610`): «Aggiungi un'area» LI R999 con la forma incollata → 1 668 forme,
  la scheda sull'area nuova, «2 file da registrare»; «Correggi» la classe → «Aggiunta (2026-10-19-2)», messaggio
  aggiornato; «Togli» → aggiunta ritirata, 1 667 forme, `2610\li.json` di nuovo uguale al registrato (resta da
  registrare il solo file delle correzioni). Il modulo nella scheda sistemato col CSS (le etichette non si spezzano). Il
  clone poi rimesso com'era. Non provata a schermo la correzione di un'area **del PDF** (sceglierla col clic sulla mappa
  stretta del pannello non riesce): la fanno i test, col nucleo e con l'interfaccia.

### Slice 3 — gli altri tipi: fatta (19 ottobre, via del committente)

- **Sulla mappa**: lo strato «AIP espanso» ha ora anche **punti** (ENR 4.4), **radioassistenze** (ENR 4.1), **luci**
  (ENR 4.5), **pericoli** (ENR 5.3) e **scali** (all'ARP), un genere ciascuno da accendere negli Strati. Sul banco, ciclo
  2610: 3 023 forme (1 667 di prima + 1 254 punti, 55 radioassistenze, 11 luci, 31 pericoli, 5 scali). Ogni voce dello
  strato porta il suo tipo e la sua chiave (`VoceDellAip.Tipo`, `Chiave`): rotte e attese c'erano già, ora si correggono.
- **Un modulo solo** per tutti i tipi che non sono aree (`ModuloDellaVoce` nel nucleo, `ModuloDiUnaVoce.razor`): per ogni
  tipo l'elenco dei campi (testo, posizione come la scrive l'AIP, scelta, intero, sì/no, limite verticale, distanza
  «16 KM», elenco separato da virgole). Le **parti composte** — i punti e i tratti di una rotta, gli apparati di una
  radioassistenza, piste, distanze dichiarate, frequenze, radioassistenze e ILS di uno scalo — si scrivono **nel JSON
  del modello**: è il passo più corto per averle tutte; un modulo vero per ciascuna si fa se servirà davvero.
- 🔴 **Un campo non toccato resta com'era** nel modello, non riscritto dal testo del modulo: una coordinata scritta in
  secondi e riletta si sposta di un'inezia, e sarebbe una correzione che nessuno ha fatto. Provato sul banco: corretta la
  frequenza di BOA, nel file cambia **una riga** sola.
- **Il nucleo** (`CorrezioniDellAip`) è lo stesso delle aree, per tipo (`Correggi`, `Aggiungi`, `Togli` con il
  `CorrectionKind`; `Ritira` per qualunque tipo): stesse regole, stesso patto «come una Leggi», provato anche qui nei test.
- «**Aggiungi**» nel riquadro chiede il tipo (un'area, una rotta, un'attesa, una radioassistenza, un punto, una luce, uno
  scalo, un pericolo).
- **Test**: Lab 1 206 → 1 220 (13 del modulo e dei gesti sugli altri tipi, uno dell'interfaccia: un punto corretto
  dalla scheda e «Aggiungi» che chiede il tipo).
- **Provato a schermo sul banco**: col pannello a 1 600 × 1 000, solo le radioassistenze accese, il clic su BOA apre la
  sua scheda; il modulo riempito (sigla, tipo, frequenza, posizione `443213N 0111726E`, apparati in JSON); una correzione
  finita per sbaglio nel campo «Copertura» (il clic di chi guidava, non del Lab) ritirata con «Ritira la correzione» →
  `li.json` di nuovo identico; poi la frequenza 117.10 → 117.15: una riga nel diff. Il menu del tipo di «Aggiungi»
  allargato (era troncato). Il clone poi rimesso com'era.

Restano fuori le carte ATC SMAC (§2). **Le correzioni sono finite** per quel che chiedeva il committente; si torna al
resto della F6 quando lo dice lui.
