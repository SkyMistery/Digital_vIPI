# Ricerca rapida nella vista live: scali e aree regolamentate — carta (1 ottobre 2026)

> **Stato: ✅ ESEGUITA il 1 ottobre 2026** sul ramo `fix/sid-ricerca` (filone Sito, S88), provata dal vivo su una
> copia del DB. Nessuna migrazione, nessun `deploy/`. Codice comune: `Vipi.Application` (servizio nuovo). Da fondere.
> Nasce da una richiesta del committente (1 ottobre 2026): dalla vista live di un APP o di un ACC «cercare alcune cose»
> — per cominciare uno scalo qualunque con vIPI o vSOP pubblicati («sono la NE e cerco LIPE»), da aprire come uno scalo
> del proprio settore, e le aree regolamentate, tutte, su una mappa con sotto le informazioni di attivazione.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
>
> Dove sta: `IRicercaLive` / `RicercaLiveFiltro` (`Vipi.Application/Live/RicercaLive.cs`) · `LiveSearchPanel`
> (`Vipi.Ui/Components/App`) · la chip «🔎 Cerca» in `LivePage` · test in `Vipi.Application.Tests/RicercaLiveFiltroTests`.

## 1. Che cosa fa

- Nella riga delle chip della vista live («📡 Il mio settore» e gli scali sotto la postazione, diretti e indiretti,
  con i delegati), in coda a tutte c'è il **campo di ricerca** (committente: il campo, non una chip che lo apre).
  Scrivendo, i risultati compaiono al posto del settore; svuotando il campo, o scegliendo una chip, si torna lì. La
  riga c'è per ogni postazione che non è d'aeroporto (APP, ACC), anche senza scali propri. La scritta «Vista rapida —
  il mio settore o…» davanti alle chip è tolta (committente).
- Un campo solo cerca due cose insieme, e i risultati escono a gruppi:
  - **Scali**: quelli con vIPI civile o vSOP militare pubblici, di tutta la divisione. Si trovano con l'ICAO
    (esatto, poi per prefisso) o col nome. Scelto uno scalo, si apre lo **stesso pannello** degli scali del settore
    (`AirportQuickPanel`), col **suo** ACC: i tasti vIPI/vSOP portano ai documenti giusti anche fuori dal proprio ACC.
  - **Aree regolamentate**: tutte quelle dell'anagrafica (importate da IVAO). Si trovano col nome scritto in qualunque
    modo — «LI R14A - S.Severa», «LI-R14», «r 14», «severa» — o col tipo da solo («R», «D», «TRA»: tutte di quel tipo).
    Si apre una sola area o tutte quelle trovate (fino a 60): **mappa** con le chip per accenderle e spegnerle, e sotto
    la **tabella** con tipo, nome, limiti, attivazione e descrizione. La tabella segue le chip: spenta un'area, sparisce
    la sua riga.
- «← Risultati» torna all'elenco; scrivere di nuovo riporta all'elenco da solo.

## 2. Decisioni

1. **Gli elenchi si chiedono una volta**, all'apertura del pannello, e si filtrano in memoria a ogni tasto
   (`RicercaLiveFiltro`, puro e provato): qualche centinaio di righe, niente query per lettera.
2. **Il cancello degli scali è `AwosGate`** (release effettiva, documento non nascosto): lo stesso del vAWOS, delle chip
   del settore e dell'API degli aeroporti. «Che cosa vede il pubblico» ha una risposta sola.
3. **Le aree vengono dall'anagrafica viva** (`ISpecialAreaRepository`), non da un documento pubblicato: la vista live
   è «adesso», e le aree non hanno una release propria. La mappa è quella delle sezioni «Aree regolamentate» dei
   documenti (`RegulatedAreas` → `AccAor`), con le schede spente perché le informazioni stanno nella tabella.
4. **Testi delle aree in inglese**: descrizione e attivazione le scrive la sorgente IVAO e qui non passano dal
   traduttore dei documenti. Da rivedere se in italiano servono.
5. **Mappa nata a larghezza 0** (scheda in secondo piano): si inquadrava a zoom 19 sul niente. `vipi-aor.js` ora rifà
   l'inquadratura alla prima larghezza vera, una volta sola. Vale per tutte le mappe delle aree, non solo qui.
6. **Il pannello usa `ScopeProprioCheAspetta`** (`TerzaPortaTests`): chi chiude la ricerca a query aperta non si porta
   via il `DbContext` sotto.

## 3. Prove

- `RicercaLiveFiltroTests` (9): ICAO anche fuori dall'ACC, ordine ICAO esatto → prefisso → nome, un carattere solo non
  cerca, le quattro grafie di R14, il tipo da solo, testo vuoto.
- Dal vivo (copia del DB): da `LIRR_NE1_CTR` «LIML» apre Linate con i tasti verso `limm` (vIPI e vSOP); da
  `LIBB_ES_CTR` «LIRN» apre Napoli; «severa» trova P128, R14A, R14B, «Mostra tutte e 3» le disegna a zoom 11 con la
  tabella, e spegnendo R14A sparisce la sua riga.

## 4. Il secondo giro: postazioni, punti, trasferimenti, radioassistenze (1 ottobre 2026, S89)

Quattro delle cinque ricerche proposte qui, approvate dal committente («1, 2, 3, 4 sì; la 5 come proposta, non ora»).
Tutte nello stesso campo e nello stesso pannello, a gruppi dopo gli scali:

1. **Postazioni** (`PostazioniAsync`, dal catalogo degli enti `IFrequenzeDegliEnti`): per callsign, nominativo radio o
   **frequenza** — un testo fatto di cifre e punto («128.705», «128.7», «1287») cerca SOLO frequenze, dall'inizio, e
   non trova scali né aree. Pallino verde se online adesso. Scelta una postazione: online o chiusa, e se chiusa **chi
   la copre** (il primo online risalendo la topologia GLOBALE, cross-ACC: `CoperturaAsync`), la catena intera, UNICOM
   se nessuno; il tasto per aprirne la vista live solo a chi può (la regola della pagina, `PuoAprire`). Il catalogo
   comprende anche le postazioni straniere confinanti (LSAZ, DAAA…).
2. **Punti nelle SID e STAR** (`ProcedureAsync`): le righe PUBBLICATE di tutti gli scali (le stesse dei documenti, da
   `IProcedureCercabili`), cercate nel fix e nella transition dall'inizio, da tre lettere. Una riga apre lo scalo nel
   pannello rapido. ⚠️ La prima lettura costa una derivazione per scalo: arriva in sottofondo dopo il resto, e finché
   non c'è il pannello lo dice.
3. **Punti di trasferimento** (`TrasferimentiAsync`): i flussi di tutti gli ACC, per nome del punto dall'inizio, con
   da → a, livello, condizione e scali. **Solo allo staff di divisione**, come la finestra «Trasferimenti» della vista
   live (S81). Le righe identiche (lo stesso accordo espanso due volte, visto su ASPIR) si tengono una volta.
4. **Radioassistenze** (`NavaidAsync`, dall'anagrafica): per codice dall'inizio o per frequenza. Scelta una: frequenza o
   canale, coordinate e **un punto sulla mappa** — `vipi-aor.js` ha un modo nuovo, `data-points` (pallino ed etichetta,
   zoom 9), che si reinquadra anche se nasce a larghezza 0.

Provato a schermo sulla copia SQLite: «128.» (17 postazioni), «AGNIS» (due SID di LIRN), «PES» (postazioni di Pescara,
NDB e VOR, un'area), «ASPIR» e «NILTO» (trasferimenti LIBB → LIRR), «LIRF_TWR» (chiusa, catena fino a LIRR_NE_CTR,
UNICOM), il VOR di Pescara sulla mappa. `RicercaLiveFiltroTests` +8.

Insieme, dal committente: nella scheda gialla dei cambi della vista live la parola «scarta» è diventata una ✕ ben
visibile, in un tasto dentro la scheda.

## 4-bis. La review prima di pubblicare (1 ottobre 2026, S90)

- 🔴 **Memoria condivisa** (`MemoriaRicercaLive` in `RicercaLive.cs`): ogni elenco della ricerca è uno solo per tutto il
  processo, con un cancello — il primo che lo chiede lo carica, gli altri aspettano e ricevono lo stesso risultato — e
  vale cinque minuti (la topologia uno). Prima ogni ricerca di ogni utente rileggeva tutto, compresi gli accordi di
  tutti gli ACC: con dieci controllori insieme, dieci giri. Provato da `Dieci_richieste_insieme_caricano_una_volta`.
- Il dettaglio di una postazione si ricalcola a ogni giro del feed (`RicercaLiveFiltro.Copertura`, pura).
- Le sole cifre («128») cercano frequenze **e** nomi (l'area P128); le cifre col punto («128.») solo frequenze.
- Uno scalo aperto dalla ricerca mostra ICAO, nome e ACC accanto a «← Risultati».
- **TWR, GND, DEL** (committente): non hanno la riga delle chip, quindi il campo sta in testata, accanto alla postazione;
  scrivendo, i risultati prendono il posto della «Vista rapida aeroporto» (frequenze e trasferimenti restano sotto).

## 5. Proposta, non fatta

**Settori**: un punto o un'area → quale settore lo copre (dalle forme AoR già usate per la mappa). Il committente l'ha
lasciata come proposta (1 ottobre 2026).
