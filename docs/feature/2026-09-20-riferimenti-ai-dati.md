# Riferimenti ai dati nel testo: frequenze, nominativi, piste, punti (20 settembre 2026)

> Stato: ✅ **chiusa** (6a→6d, più la revisione §6) — `[[FREQ]]`, `[[ATC]]`, `[[RWY]]`, `[[FIX]]` nel testo,
> con l'avviso per quel che sparisce e **un selettore solo**, a **sei** chip. Verificata a schermo su LIBD.
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
| `FREQ` | i settori **con** `Sector.DefaultFrequency` | una per pagina (`ListLinkableFrequenciesAsync`), e solo se il testo cita una frequenza |
| `ATC` | **tutti** i settori, col nominativo del catalogo IVAO | una per pagina (`ListSectorCallsignsAsync`), e solo se il testo cita un nominativo. 🔴 Vedi §6.1: non è la stessa domanda delle frequenze |
| `RWY` | le piste dello scalo (`ListRunwayDataAsync`) | una per pagina, sugli scali citati |
| `FIX` | il catalogo dei punti del sectorfile (`INavaidSource`) | tenuto in cache di processo |

⚠️ **La via breve prima di tutto**: se il testo non contiene `[[`, non si chiede niente a nessuno. È la regola
che ha tenuto il costo delle SID a zero su ogni pagina che non le cita.

## 4. Le slice

1. ✅ **6a — il meccanismo e le frequenze** e ✅ **6b — i nominativi** (insieme, credendoli la stessa
   domanda; 🔴 **non lo erano** — vedi §6.1).
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
   ⚠️ Dichiarato **per famiglia**, e non bastava: vedi §6.3.
3. ✅ **6d — il selettore**: **uno solo**, con le sue chip — SID · STAR · FREQ · ATC · RWY · FIX (le ultime
   due arrivate con la revisione, §6.2). Un tasto per famiglia in barra sarebbe una decisione prima ancora di
   aprire l'elenco; il gesto invece è sempre lo stesso, «cito qualcosa che vive nell'archivio». Il tasto si
   chiama ora **Cita**.
   ⚠️ Per gli enti l'elenco mette **prima quelli dello scalo del documento**: chi scrive la vIPI di LIBD cita
   quasi sempre un ente di LIBD.
   ⚠️ Il contratto del selettore è ora **il testo del riferimento** (una stringa), non la procedura scelta: è
   tutto quel che serve a chi lo inserisce, e vale per ogni famiglia presente e futura.

## 4-bis. Verifica

- **Il selettore a schermo** (`selettore-verifica.js`, prima della revisione): le chip escono nell'ordine `SID STAR FREQ ATC`, la
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

## 6. La revisione (20 settembre 2026, a mente fresca)

Riletto tutto come se l'avesse scritto qualcun altro. Quattro difetti in questa carta, tutti corretti; gli
altri — il percorso Postgres, le guardie mancanti, le minuzie — stanno nel
[foglio di review](../review/2026-09-20-a80-star-e-riferimenti.md) §8.

### 6.1 Il nominativo non dipende dall'avere una frequenza

`[[ATC …]]` si risolveva da `ListLinkableFrequenciesAsync`, che filtra `DefaultFrequency != null`. Due
conseguenze, e nessuna è teorica: un ente **senza** frequenza dichiarata non si poteva citare (né compariva
nella chip ATC), e il giorno in cui a un settore si cancella la frequenza il suo `[[ATC …]]` già scritto in un
documento sarebbe caduto nel ripiego **con** l'avviso «non si trova più nell'archivio» — falso: l'ente c'è.

Sono due domande diverse, e ora sono due metodi diversi (`EnteRow` / `ListSectorCallsignsAsync`). Si chiede
solo quel che il testo cita, quindi quasi sempre **una sola** delle due: il costo non cambia.

### 6.2 Piste e punti si risolvevano ma non si potevano inserire

Il committente ne aveva chiesti quattro, di riferimenti ai dati, e quattro se ne risolvono e si segnalano. Ma
il selettore ne offriva due: `[[RWY …]]` e `[[FIX …]]` restavano da scrivere **a mano**, che è precisamente il
modo di sbagliare la chiave — e una chiave sbagliata torna indietro come avviso «il dato non c'è più», che è
falso. Aggiunte le due chip: `RWY` chiede le soglie all'anagrafica dello scalo, `FIX` il catalogo dei punti
(che non ha scalo, quindi niente campo ICAO). L'elenco ha un tetto di 300 voci con la riga «affina la
ricerca»: i punti sono migliaia, e un elenco così non si scorre, si cerca.

### 6.3 «Sorgente muta» era per FAMIGLIA, e le due domande sbagliavano insieme

Le piste si chiedono per scalo, ma il «non lo so» si dichiarava per famiglia: uno scalo citato con un ICAO
**inventato** tornava senza soglie esattamente come uno scalo vero non ancora importato. Se era l'unico
citato, la famiglia non risultava guardata e l'avviso **non compariva affatto**; se il testo citava anche uno
scalo vero, l'avviso arrivava. Stesso testo, due comportamenti. Ora la famiglia è guardata appena la domanda è
stata fatta, e il «non lo so» si dichiara per **scalo** (`ValoriDato.Muta`).

Nello stesso passo: la **pista vuole tutti e due i gettoni**. Con il secondo facoltativo, `[[RWY LIRF]]`
entrava fra i citati e non si risolveva mai — la chiave delle piste è «scalo soglia» — e finiva in testata
come dato sparito. Non riconoscere la forma sbagliata lascia il testo com'è, che è la verità.

### 6.4 Il valore entrava nel JSON senza ripulitura

Il riferimento si sostituisce anche **dentro il JSON** dei blocchi tabella — ed è per questo che la sua forma
non ammette virgolette. Ma il **valore** che prende il suo posto arriva dall'archivio, e il nominativo è testo
libero di sorgente esterna: una virgoletta dal catalogo IVAO avrebbe spaccato il JSON, e il blocco avrebbe
smesso di rendersi in una pagina sola, senza un errore che lo dica. `RiferimentiProcedura.ValoreScrivibile`
toglie virgolette e barre rovesce da quel che prende il posto del riferimento, nomi di procedura compresi.
