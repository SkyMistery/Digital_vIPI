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

- Nella riga «Vista rapida» della vista live, in coda agli scali del settore, c'è la chip **🔎 Cerca**. La riga c'è
  per ogni postazione che non è d'aeroporto (APP, ACC), anche senza scali propri: la ricerca serve proprio a chi
  vuole uscire dal suo settore.
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

## 4. Prossime ricerche proposte (non fatte)

Elementi che il sito ha già e che un controllore cerca al volo:

1. **Postazioni e frequenze**: «chi è su 128.705?», «LIRR_NE_CTR» → frequenza, nome, chi la copre se chiusa, se è
   online adesso. Il catalogo delle frequenze e la topologia live ci sono già.
2. **Punti (fix e navaid)**: «AGNIS» → in quali SID/STAR compare (di tutti gli scali), e se è un punto di coordinamento
   (CoP) fra chi e chi. Le procedure cercabili esistono già per la barra di ricerca (S83).
3. **Punti di trasferimento (CoP)**: «a chi passo il traffico su BOL?» → i flussi che usano quel punto, con livelli.
4. **Navaid**: «VOR 115.80» o «PES» → frequenza, posizione su mappa.
5. **Settori**: un punto o un'area → quale settore lo copre (dalle forme AoR già usate per la mappa).
