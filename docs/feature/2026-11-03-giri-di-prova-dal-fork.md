# I giri di prova dal fork (3 novembre 2026)

Filone Sector Lab. **Da approvare.** Segue [«Il kit di prova per gli AOD»](2026-11-03-kit-di-prova-per-gli-aod.md)
(fatto, release `lab-prova-f61cec43` pubblicata dal committente il 3 novembre). Scelte proposte in §3 (P1-P9), domande
al committente in §6.

## §0 — La domanda

Il committente, 3 novembre, dopo la release: «Se volessimo fargli fare più test come facciamo?». La proposta in chat
(approvata per la carta: «Sì, scrivila»): più giri scelti dal pannello, i compiti letti da una cartella del fork, un tasto
per rifare un giro dopo una versione nuova.

## §1 — Com'è oggi

- I dodici compiti G1-G12 stanno **nel codice** (`GiroDiProva.Compiti`, `Vipi.SectorLab.Ui`). Un compito nuovo = un'uscita
  del Lab, uno zip, una release, e ogni AOD che riscarica.
- Gli esiti stanno in `giro-di-prova.json` nei dati (`{"Acceso": true, "Esiti": [...]}`), per compito; il giro è uno solo.
- Il Lab parla già col fork: il sector dell'ambiente (`lavoro\sector`) è un clone parziale
  (`--filter=blob:none --sparse`, solo `SectorFiles` sul disco) e a ogni lettura del ramo fa `fetch` di tutti i rami
  (`RamiDelSector.ChiediAlServer`, da `StatoDelRamo`). Il ramo predefinito del fork è `master` (`Predefinito`).
- `PROVE.md` (fuori repo) ha circa 130 prove a mano scritte per il committente, coi numeri del sector vero e del 2611:
  sono il materiale da cui nascono i giri, non i giri.

## §2 — Che cosa cambia, in una riga

I giri diventano **file sul fork di prova** (`giri/*.json` sul ramo `master`), che il Lab legge da git a ogni avvio:
un giro nuovo arriva agli AOD con un clic del committente (la fusione), senza zip nuovo.

## §3 — Le scelte proposte

- **P1 — Dove stanno i giri**: in `giri/` alla radice del fork `SkyMistery/it-aurora-sector-test`, ramo `master`, fuori
  da `SectorFiles` (Aurora non li vede, il sector non cambia). Uno per file: `giri/ground.json`, `giri/settori.json`…
- **P2 — Come li legge il Lab**: da git, non dal web: `git ls-tree origin/master -- giri/` e `git show
  origin/master:giri/<file>` nel clone del sector, dopo il `fetch` che fa già. Così vale anche per un repo privato e usa
  le credenziali di sempre, e la cartella non scende sul disco (il clone resta parziale). Ogni giro letto si copia nei dati
  (`giri/`): senza rete il Lab usa l'ultima copia; senza copia, i giri che ha dentro (P8).
- **P3 — Il formato** (un giro):

      { "id": "ground", "titolo": "Il ground dello scalo", "versione": 1,
        "perChi": "Chi ritocca piazzali, stand e taxiway",
        "compiti": [
          { "id": "1", "titolo": "…", "pagina": "Sector", "fai": "…", "dovresti": "…" },
          { "id": "9", "titolo": "Che cosa ti è mancato?", "fai": "…", "dovresti": "…", "soloNota": true } ] }

  `pagina` è il nome di una pagina del Lab (`Sector`, `Testo`, `Consegna`, `AipIndice`, `Aip`, `Confronti`,
  `Impostazioni`) o manca. Un campo sconosciuto si ignora (un giro scritto per un Lab più nuovo si legge lo stesso); un
  giro che non si legge (JSON rotto, `id` ripetuto, nessun compito) si salta, e il pannello dice quale e perché.
- **P4 — La scelta del giro**: in testa al pannello un elenco dei giri, ognuno coi suoi fatti («Primi passi 12/12 ✓ ·
  Il ground dello scalo 0/9 · nuovo»); il tasto della barra dice quello aperto e «· 1 nuovo» se ne è arrivato uno.
  «Nuovo» = un giro che l'AOD non ha mai aperto, o con una `versione` più alta di quella dei suoi esiti.
- **P5 — Esiti per giro**: il file dei dati tiene gli esiti per giro e versione; quello di oggi diventa il giro
  «primi-passi» alla prima lettura (niente si perde). Un giro aggiornato tiene gli esiti dei compiti che hanno ancora lo
  stesso `id`; i compiti nuovi nascono senza.
- **P6 — Una issue per giro**: «Giro «Il ground dello scalo» v1 · VID 123456 · 03/11/2026», col nome del file e la
  versione nel testo; il resto come oggi (K8).
- **P7 — Rifare un giro**: «Ricomincia questo giro» svuota i suoi esiti (con la domanda in due tasti, non `confirm()`, e
  l'avviso se non sono stati mandati); gli esiti mandati restano nella issue.
- **P8 — Il giro dentro il Lab**: «Primi passi» (G1-G12 di oggi) resta nel codice come ripiego, se il fork non ne ha
  nessuno o non risponde; se il fork ha un `primi-passi.json`, vale quello.
- **P9 — Chi scrive i giri**: l'agente del Lab, su un ramo del fork (`giri/<cosa>`) con la PR; il committente la fonde
  (è `master` del fork: un clic suo). Un test (`GiriDiProvaTests`) legge i file del ramo prima della PR, perché un giro
  rotto non arrivi agli AOD.

## §4 — I giri della prima ondata (proposti)

Da `PROVE.md`, riscritti per chi non ha mai visto il Lab e senza numeri del sector vero. Otto-dieci compiti ciascuno,
l'ultimo una domanda («che cosa ti è mancato?»).

| Giro | Per chi | Dentro (dalle prove) |
|---|---|---|
| **Primi passi** | tutti, per primo | G1-G12 di oggi |
| **Il ground dello scalo** | chi ritocca piazzali e stand | fuoco sullo scalo, strati e grigio, editor sulla mappa, calamita, disegnare a clic, trascinare i punti, Consegna dello scalo, Scheda a striscia (A110-A132) |
| **Settori e confini** | chi tiene settori e bordi | fuoco sul settore, settori accanto, `.tfl` e `.hartcc`, vertici (+, −, ✂, Unisci, Inverti, Incolla da testo) |
| **SID, STAR e procedure** | chi tiene le procedure | Scheda Procedura (vincoli, tratti, legami), curve, «Porta» dalla prosa dell'AIP, controlli delle procedure (A54-A62, slice 9) |
| **AIP espanso e Confronti** | chi aggiorna dal ciclo AIRAC | Indice, scheda e PDF, correzioni (scrivi, ritira), Confronti per sezione, Porta nel sector, Sostituisci, metti in questo file (A17-A40, R7-R9) |
| **Dalla modifica alla PR** | tutti, dopo i primi passi | Testo, Problemi, Salva, Registra il sector, vetrina di Aurora, Manda, Allinea (F4, T1-T3) |

## §5 — Le fette

1. **Il formato** (P3): il lettore dei giri con le sue regole, e «Primi passi» scritto come un giro (P8). Test.
2. **Dal fork** (P1, P2): la lettura da `origin/master:giri/`, la copia nei dati, il ripiego; «Cerca giri nuovi» nel
   pannello per rileggere senza riavviare.
3. **Il pannello** (P4-P7): la scelta del giro, gli esiti per giro col passaggio del file di oggi, la issue col giro,
   «Ricomincia».
4. **Una release** col Lab nuovo (la pubblica il committente, K10): l'ultima che gli AOD devono riscaricare per i giri.
5. **I giri della prima ondata** (§4): uno per PR sul fork, dal più chiesto; ognuno provato sul banco prima della PR.

## §6 — Da decidere col committente

1. I giri della prima ondata (§4): vanno bene questi sei? Da quale si comincia dopo «Primi passi»?
2. I giri stanno sul fork `master` (P1): va bene che sul fork ci sia una cartella `giri/` che il sector vero non ha?
3. Un giro è per tutti gli AOD, o ne vuoi anche **per qualcuno** (per esempio solo per chi ha uno scalo)? La carta oggi
   non lo prevede: ogni AOD vede tutti i giri e sceglie lui.
