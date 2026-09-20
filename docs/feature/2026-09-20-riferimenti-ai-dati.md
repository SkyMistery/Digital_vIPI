# Riferimenti ai dati nel testo: frequenze, nominativi, piste, punti (20 settembre 2026)

> Stato: ✅ **chiusa** (6a→6d) — `[[FREQ]]`, `[[ATC]]`, `[[RWY]]`, `[[FIX]]` nel testo, con l'avviso per quel
> che sparisce e **un selettore solo** a quattro chip. Verificata a schermo su LIBD.
> Gemella di
> [riferimenti alle procedure](2026-09-18-riferimenti-sid-nel-testo.md) (§A73) e
> [STAR](2026-09-20-star-e-altri-riferimenti.md) (§A80), da cui eredita il meccanismo.

**La richiesta del committente (20 settembre 2026):** dopo SID e STAR, lo stesso meccanismo per le
**frequenze**, le **piste**, i **nominativi ATC** e i **punti** — scelti tutti e quattro, in quest'ordine di
guadagno. Restano fuori, per ora, le quote di transizione e le aree speciali.

## 1. Che cosa cambia rispetto alle procedure

Una procedura citata ha un **nome che si muove**: `OST1E` diventa `OST2E`, e il riferimento serve a seguirlo.
I dati di questa carta si dividono in due famiglie, e vanno trattate in due modi:

| Famiglia | Gettone | Che cosa esce | Perché serve |
|---|---|---|---|
| **Il valore cambia, la chiave no** | `[[FREQ LIRF_TWR]]` · `[[ATC LIRR_CTR]]` | la frequenza / il nominativo **di oggi** | il testo segue la sorgente senza che nessuno riapra il documento |
| **La chiave È il valore** | `[[RWY LIRF 16L]]` · `[[FIX OST]]` | quel che c'è scritto, sempre | non si può indovinare un rinomino (`16L` → `17L` non ha radice comune): il guadagno è **l'avviso** quando il dato non esiste più |

🔴 **Niente «ultimo valore visto» nel gettone**, al contrario delle procedure. Là serviva per **ritrovare la
riga** quando il nome cambia; qui la riga si ritrova col callsign — che è stabile — e un gettone corto è più
facile da scrivere a mano, da leggere nel sorgente e da confrontare in un diff. Se il dato non si trova più,
esce **la chiave**: `LIRF_TWR` dice sempre qualcosa di vero, e l'editor lo segnala in cima.

## 2. La forma

```
[[FREQ LIRF_TWR]]      →  118.700
[[ATC LIRR_CTR]]       →  Roma Radar          (il nominativo IVAO; il callsign se non c'è)
[[RWY LIRF 16L]]       →  16L                 (+ avviso se LIRF non ha più quella soglia)
[[FIX OST]]            →  OST                 (+ avviso se il catalogo non lo ha più)
```

Regola unica: `[[TIPO CHIAVE]]`, più un secondo gettone dove la chiave è di due pezzi (`RWY` = scalo + soglia).
La protezione dalla traduzione vale come per le procedure: **la stessa regola** che riconosce il riferimento lo
protegge, o un riferimento riconosciuto dal renderer e non dalla protezione partirebbe verso il motore.

## 3. Le sorgenti, e quante domande costano

| Gettone | Sorgente | Domanda |
|---|---|---|
| `FREQ`, `ATC` | `Sector.DefaultFrequency` + nominativo ATC del catalogo IVAO | **una sola** per pagina (`ListLinkableFrequenciesAsync`), e solo se il testo cita qualcosa |
| `RWY` | le piste dello scalo (`ListRunwayDataAsync`) | una per pagina, sugli scali citati |
| `FIX` | il catalogo dei punti del sectorfile (`INavaidSource`) | tenuto in cache di processo |

⚠️ **La via breve prima di tutto**: se il testo non contiene `[[`, non si chiede niente a nessuno. È la regola
che ha tenuto il costo delle SID a zero su ogni pagina che non le cita.

## 4. Le slice

1. ✅ **6a — il meccanismo e le frequenze** e ✅ **6b — i nominativi** (insieme: stessa sorgente, stessa
   query, e dividerli avrebbe voluto dire chiedere due volte la stessa cosa).
   🔴 **Una porta sola per la sostituzione**: `Riferimenti.Sostituisci`. I riferimenti si risolvono in cinque
   punti — disegno dei blocchi, anteprima dell'editor, resa Markdown, indice della ricerca, release in vigore —
   e un meccanismo nuovo che entrasse per conto suo dovrebbe ricordarsi di entrare in tutti e cinque. In
   cascata passa `RiferimentiRisolti` (procedure + dati), non più i soli nomi.
   ⚠️ **Il cambio del tipo in cascata è una regressione silenziosa**: il compilatore non dice niente, il
   parametro resta `null` e i riferimenti escono col ripiego. Se ne sono accorti i test di resa, che erano
   scritti apposta.
   ⚠️ `IFrequenzeDegliEnti`: una porta **ristretta** sullo stesso dato di `ListLinkableFrequenciesAsync` — chi
   risolve i riferimenti non ha niente a che fare con le trenta scritture dell'anagrafica, e una prova non deve
   implementarne trenta per arrivare a una.
   ⚠️ **Piste e punti non entrano nell'avviso** finché non hanno la loro sorgente: la loro chiave è il valore,
   esce sempre giusta, e segnalarla sarebbe un falso allarme a ogni riga.
2. ✅ **6c — piste e punti**: `[[RWY LIBD 07]]` e `[[FIX BANAV]]`. Escono **come sono scritti** — un rinomino
   per deriva magnetica non si indovina — e il guadagno è l'avviso.
   🔴 **Il ripiego è il pezzo che si legge, non la chiave intera**: su `[[RWY LIBD 07]]` esce `07`, non
   «LIBD 07» — lo scalo è il contesto della frase. Preso da un test al primo giro.
   🔴 **Sorgente muta ≠ dato sparito**: `ValoriDato` sa quali famiglie ha **davvero guardato**, e una sorgente
   che risponde a vuoto — catalogo dei punti non raggiungibile, anagrafica piste non ancora importata — non
   genera nessun avviso. Senza questa regola un guasto di rete riempirebbe la testata dell'editor di allarmi
   falsi, che è il modo più rapido per far smettere di leggerli.
3. ✅ **6d — il selettore**: **uno solo**, con quattro chip — SID · STAR · FREQ · ATC. Quattro tasti in barra
   sarebbero quattro decisioni prima ancora di aprire l'elenco; il gesto invece è sempre lo stesso, «cito
   qualcosa che vive nell'archivio». Il tasto si chiama ora **Cita**.
   ⚠️ Per gli enti l'elenco mette **prima quelli dello scalo del documento**: chi scrive la vIPI di LIBD cita
   quasi sempre un ente di LIBD.
   ⚠️ Il contratto del selettore è ora **il testo del riferimento** (una stringa), non la procedura scelta: è
   tutto quel che serve a chi lo inserisce, e vale per ogni famiglia presente e futura.

## 4-bis. Verifica

- **Il selettore a schermo** (`selettore-verifica.js`): le chip escono nell'ordine `SID STAR FREQ ATC`, la
  chip FREQ carica gli enti con **LIBD per primo**, la scelta scrive `[[FREQ LIBD_CS0_APP]]` dove stava il
  cursore e chiude il selettore; scritto a mano `[[RWY LIBD 99X]]`, la testata dell'editor dice «RWY LIBD 99X
  — is no longer in the archive», con la sezione fra parentesi. Nessun gettone grezzo, zero errori in console.
- **A schermo** (`dato-verifica.js`, copia del `vipi.db`, LIBD): scritto `[[FREQ LIBD_TWR]] … [[ATC LIBD_TWR]]`
  in un campo di prosa, dopo il ricarico l'anteprima dell'editor **e** il documento dicono «Su **118.300** con
  **Bari Tower**», nessun gettone grezzo, zero errori in console.
- 10 test in `RiferimentiDatoTests` (la forma del gettone, il valore di oggi, il ripiego alla chiave, la chiave
  di due pezzi, l'avviso che elenca solo quel che non si trova, piste e punti fuori dall'avviso, le due
  famiglie nello stesso testo, il catalogo chiesto **una volta sola**, la via breve) e 1 in
  `TraduzioneRiferimentiTests` (un dato citato non arriva al motore).

## 5. Che cosa NON si fa

- **Niente conversione dell'esistente.** Le frequenze già scritte nei documenti non si convertono a tappeto: un
  `118.700` nel testo può essere un esempio, una citazione storica o la frequenza di un altro ente. Si
  convertono a mano, col selettore, quando si tocca il paragrafo.
- **Niente riferimento a una frequenza per ICAO** (`[[FREQ LIRF TWR]]`): la chiave è il **callsign**, che è
  l'identità del settore in tutto il progetto.
