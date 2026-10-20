# Riordino delle pagine del Sector Lab (20 ottobre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md). Viene dopo la F7
> ([`2026-10-19-f7-sid-e-star.md`](2026-10-19-f7-sid-e-star.md)).
> Stato: ✅ **direzione APPROVATA** dal committente il 20 ottobre (le quattro pagine: «sì, mi sembra ok, vediamo come
> viene»). Le decisioni D1-D7 di §4 sono come proposte. Dopo il primo giro sull'app (R0-R1) il committente ha chiesto
> **i due mondi** e **la pagina Testo**: §4-bis, D8-D13, e le slice rifatte in §5-bis. Il lavoro è in §6.

## §0 — La domanda

Il committente (20 ottobre), aprendo l'app: «ora è molto confusionaria. Vogliamo riorganizzarla?». Si parte dall'analisi
di tutte le operazioni, raggruppate per tipo, per decidere come dividere le pagine.

Sui due schermi il committente dice che chi li ha usa i pannelli nell'altra finestra, ma non tutti li hanno: l'app deve
andare bene **su uno schermo solo** e sfruttarne **due** quando ci sono.

## §1 — Cosa c'è (inventario del 20 ottobre, ramo `lab/f3` @ `8934f7ec`)

Una sola pagina (`Pages/Home.razor`) con la barra in alto, la colonna di sinistra (Sfoglia, Scali, Problemi), la colonna
della Scheda e delle Modifiche, la colonna degli strati e la mappa. Con «Pannelli in un'altra finestra» la colonna, la
Scheda e le Modifiche passano in `Pages/Pannelli.razor`. Le operazioni sono circa centocinquanta. Raggruppate per tipo
di lavoro:

| # | Gruppo | Operazioni | Dove stanno oggi |
|---|---|---|---|
| 1 | **Aprire il sector** | cartella, «Aperti di recente», «Prendi» dal repo, select ISC «Nomi risolti con», «Cambia cartella…» | schermata iniziale, barra |
| 2 | **Trovare e guardare** | ricerca, albero di cartelle e file, elenco dei record, Voci (come la selezione di Aurora), Scali, «Chi lo usa» del file e del colore, clic sulla mappa, Strati e generi, Colori e schema, «In vista» / «Solo questo sulla mappa» | colonna di sinistra, colonna di destra, mappa, tasti nella Scheda e nel file |
| 3 | **Modificare un record** | Campi (dieci editor diversi), Metadati, Vertici con «Incolla da testo», Linea dei `.geo`, Vincoli dei punti, Tratti, Righe a mano, Nascondi/Mostra, Nuovo record / Record come questo / Togli, modello ATIS e anteprima a voce, disegno dei simboli, slot degli stand, nome della voce e delle parti | Scheda |
| 4 | **Gesti su più record o file** | Rinomina un punto e le citazioni, La stessa forma (famiglia, Allinea, Prendi la sua, bordo e riempimento), gemello in `VFR_NASCOSTI.fix`, Aerovie del file (Sistema le etichette, Nuova, Togli), Composta da, «a tutta la voce», Correggi tutte, Sposta sopra i commenti, «allinea anche questo» | Scheda, Problemi, Modifiche |
| 5 | **Controllare** | errori e avvisi con filtri, problemi nuovi delle modifiche, note da Aurora, esito del salvataggio | linguetta Problemi, testo in barra, Scheda, Modifiche |
| 6 | **AIP** | Prendi i PDF da Download, Prendi l'AIP espanso, Leggi nell'AIP espanso, segnalazioni, Registra/Manda del repo AIP, strato sulla mappa, Aggiungi/Correggi/Togli/Ritira una voce, 13 confronti (CTR, ATZ ×2, ENR 5, SID e STAR, rotte, attese, radioassistenze ×2, punti, scali, piste, frequenze) coi gesti di riga Porta nel sector / Sostituisci / Dividi / Scrivi i tratti / Correggi l'uso / Mappa delle SID | tendina AIP della barra; la scheda di una voce AIP solo dal clic sulla mappa |
| 7 | **Salvare e consegnare** | Modifiche col diff, Salva, Annulla tutto, ↶ ↷ e Ctrl+Z/Y, Registra, delete.upd, Manda e PR, Allinea alla base, changelog (Proponi, Scrivi, Archivia), riporto master → ciclo coi conflitti, Nuovo lavoro, Passa a un altro ramo, Chiedi le novità | pannello Modifiche, tendina Ramo, Storia in barra |
| 8 | **Aurora** | Porta in Aurora (vetrina), lotto «Da guardare in Aurora» (✅/❌, Visto), Prove in Aurora (Prepara, Butta) | tendina Aurora |
| 9 | **Impostazioni** | configurazioni, tratto minimo delle etichette, VID, repo del sector, vetrina, PDF, AIP espanso, repo dell'AIP, tema, Registro, pannelli in un'altra finestra | tendina Impostazioni e tasti in barra |

## §2 — Perché è confusionaria

1. **Tutto in una pagina, nell'ordine delle slice.** Ogni lavoro ha aggiunto il suo pezzo dove c'era posto: la barra ha
   11 controlli e 4 tendine, la Scheda fino a 21 sezioni impilate (Stand e Ordine di disegno prima di Voce, Composta da
   fra Aerovie e Linea), la mappa non fa che scegliere.
2. **L'AIP è un'altra app dentro una tendina** larga 24rem, senza altezza massima: 13 confronti, fino a cinque elenchi
   di risultati, due moduli e un giro git suo.
3. **Il giro di consegna è spezzato in tre posti**: Salva sta nelle Modifiche (con i pannelli staccati, nell'altra
   finestra), Registra e Manda nella tendina Ramo, Porta in Aurora nella tendina Aurora.
4. **Le due finestre hanno barre diverse**: quella dei pannelli non ha Ramo, Aurora e AIP.
5. **Doppioni**: tre posti per un nuovo record, quattro per un nome, due editor per i vertici di un `.geo` (Linea e
   Vertici), «Chi lo usa» con lo stesso titolo per il file e per il punto, l'accensione dello strato AIP in due posti.
6. **Gesti sul file dentro la scheda del record**: «Aerovie del file» compare su ogni record di un'aerovia; la rinomina
   della voce e «a tutta la voce» toccano molti record senza che si veda.
7. **Verbi incoerenti**: per scartare «annulla», «Lascia stare», «Lascia com'era», «No, lasciale», «Torna alle
   modifiche»; «Togli» cancella un record del sector, una voce dell'AIP o solo una nota; «Registra» e «Manda» valgono per
   due repo diversi; «Porta in Aurora» copia nella vetrina, «Porta nel sector» è un gesto sul sector.
8. **I rifiuti stanno lontano dal tasto** che li ha causati (in fondo alla Scheda, in fondo alla tendina).
9. **Dal record del sector non si arriva alla voce AIP corrispondente**; il contrario solo dalla mappa.

Due **difetti** trovati nell'analisi, indipendenti dal riordino:
- **«Cambia cartella…» butta le modifiche non salvate senza chiedere** (`SessioneDelLab.Chiudi` azzera modifiche e
  storia), ed è il tasto più evidente della barra.
- Il link «aperta nel browser» della PR dell'AIP (`AipNellaBarra.razor`, `target="_blank"`) è probabilmente rifiutato da
  `Finestra.AllaRichiestaDiUnaFinestra`, che accetta solo `/pannelli`.

## §3 — La proposta

**Quattro pagine**, una per modo di lavorare, scelte con le linguette nella barra:

| Pagina | Gruppi | Cosa c'è |
|---|---|---|
| **Sector** (quella di base) | 2, 3, 4, 5 | mappa, colonna (Sfoglia, Scali, Problemi), Scheda, Modifiche |
| **AIP** | 6 | a sinistra l'AIP espanso (PDF, lettura, segnalazioni, git dell'AIP) e l'elenco dei confronti; al centro i risultati in una tabella larga coi gesti; la mappa accanto |
| **Consegna** | 7, 8 | nell'ordine vero del lavoro: le modifiche → Salva → Registra → Porta in Aurora e il lotto → Manda, PR, changelog; a parte i rami (nuovo lavoro, passa, riporto) e le prove in Aurora |
| **Impostazioni** | 9, e «Cambia cartella» | le impostazioni di oggi, il registro, il tema, i pannelli |

**La barra** si riduce a: nome del sector · ramo e stato (porta a Consegna) · errori e avvisi (portano ai Problemi) ·
↶ ↷ · **Salva (N)** · le linguette · ⚙. Uguale nelle due finestre.

**Uno o due schermi.** Su uno schermo ogni pagina sta tutta nella finestra. Con due, la finestra principale tiene la
barra e la mappa, quella dei pannelli tiene i pannelli **della pagina scelta**: Sector → colonna, Scheda e Modifiche;
AIP → AIP espanso, confronti e risultati. Le linguette sono le stesse e si seguono fra le due finestre; Consegna e
Impostazioni non hanno mappa e stanno nella finestra dei pannelli se c'è, altrimenti nella principale.

**La Scheda** per gruppi fissi, sempre nello stesso ordine: intestazione (tasti del record) · **Dati** (Campi, Metadati,
nome della voce e delle parti, Slot) · **Geometria** (Vertici, Incolla da testo, Ordine di disegno; un solo editor dei
vertici anche per i `.geo`) · **Procedura** (Vincoli, Tratti, Legami, Composta da) · **Collegamenti** (Chi lo usa, La
stessa forma, Gemello, Confine, Attesa, la voce AIP corrispondente) · **Righe del file**. I gesti sul file (Aerovie del
file, Sistema le etichette) passano al pannello del file nella colonna. Il rifiuto compare accanto al tasto.

## §4 — Decisioni

- **D1** Quattro pagine: Sector, AIP, Consegna, Impostazioni (§3), con le linguette in barra.
- **D2** Una sola mappa con un solo stato (strati, colori, vista, scelta) per Sector e AIP; cambiando pagina non si perde
  niente. Consegna e Impostazioni non la mostrano.
- **D3** Due finestre: la principale tiene barra e mappa, i pannelli seguono la pagina scelta; la barra è la stessa nelle
  due. Su uno schermo solo tutto sta nella principale come oggi.
- **D4** «Salva (N)» in barra, sempre visibile; Ctrl+S salva. «Cambia cartella» va nelle Impostazioni e chiede conferma se
  ci sono modifiche non salvate.
- **D5** La Scheda per gruppi fissi (§3); i gesti sul file nel pannello del file.
- **D6** Un vocabolario solo: **Scrivi** per ogni gesto che cambia il sector, **Lascia stare** per scartare un modulo,
  **Annulla** solo per la storia e le modifiche, **Togli** solo per il sector; per l'AIP «Togli dall'AIP espanso», per le
  note «Archivia»; «Registra/Manda il sector» e «Registra/Manda l'AIP»; «Porta in vetrina» al posto di «Porta in Aurora».
- **D7** I `data-tasto`, `data-campo` e simili restano gli stessi dove il gesto resta lo stesso: i test e l'autoprova li
  usano.

## §4-bis — La seconda forma: due mondi, AIP e Sector (20 ottobre sera)

Il committente, provata la R1: «Vorrei una pagina dove poter editare il sector file come se fosse Visual Studio Code,
quindi scrivendo a mano. In questo modo se qualcuno vuole editare il sector in modo "classico" lo può fare ma noi
possiamo proteggere i metadati» e «per l'AIP espanso vorrei una pagina a parte con la mappa in cui analizzare e
eventualmente correggere i dati di AIP […] dividere l'app in lavoro con l'AIP e Sector con i due pezzi che si parlano
[…] magari AIP è organizzato come fosse un AIP in modo da trovare facilmente le cose».

| Mondo | Pagine | Repo e consegna |
|---|---|---|
| **AIP** | **Indice**: l'albero come nell'AIP (ENR 2, ENR 3, ENR 4, ENR 5, AD 2 scalo per scalo), la mappa, la scheda della voce con le correzioni e, sezione per sezione, il confronto col sector · **Lettura e consegna**: PDF, ciclo, «Leggi», segnalazioni, Registra/Manda dell'AIP espanso | il repo dell'AIP espanso |
| **Sector** | **Mappa**: Sfoglia, Scheda, Problemi (la pagina Sector della R1) · **Testo**: i file scritti a mano · **Consegna**: modifiche, ramo, PR, Aurora | il repo del sector |

Le **Impostazioni** sono comuni.

- **D8** Due mondi nella barra, AIP e Sector, ognuno con le sue pagine; le Impostazioni a parte. Sostituisce le quattro
  linguette di D1 (la R1 resta: le linguette diventano due gruppi).
- **D9** L'Indice dell'AIP segue l'ordine dell'AIP: sezione, sottosezione, voce; AD 2 per scalo. Una voce si apre nella
  scheda AIP con la mappa accanto, e lì si corregge (le correzioni della F6, file delle correzioni nel repo dell'AIP).
- **D10** I confronti stanno dentro l'Indice, sezione per sezione: ogni voce dice se nel sector è uguale, diversa o
  manca, coi gesti «Porta nel sector» / «Sostituisci». I gesti sono modifiche in sospeso **del sector**, che si salvano
  dalla Consegna del sector: correggere l'AIP e cambiare il sector restano due cose e due repo.
- **D11** I due mondi si parlano: dalla scheda di un record del sector, «nell'AIP: …» porta alla voce; dalla scheda di
  una voce AIP, «nel sector: file, record» porta al record.
- **D12** La pagina **Testo**: albero dei file ed editor (CodeMirror 6, dentro l'app, niente rete), colori per tipo di
  file, problemi del validatore sulla riga, nomi suggeriti dal catalogo, Ctrl+clic per andare alla definizione. Quel
  che si scrive è una modifica in sospeso come le altre (diff, Annulla, problemi nuovi, lo stesso «Salva»); un file con
  modifiche fatte coi gesti si apre con quelle dentro.
- **D13** I **metadati** (righe `//@` e `//@@`) nel Testo si vedono **bloccati, con la possibilità di sbloccarli**
  (committente); cancellare a mano un record che ha metadati **chiede prima** (committente).

## §4-ter — La terza forma: tre spazi di lavoro, un motore di mappa (20 ottobre sera)

Il committente, guardando i fix dell'AIP espanso sulla mappa (pallini blu, non i simboli di Aurora): «vorrei che AIP e
sector avessero delle mappe ognuna a sé che si basano sullo stesso motore e poi una terza sezione dell'app per fare i
confronti tra AIP e sector […] Spazio di lavoro per l'AIP espanso, spazio di lavoro per il sector, spazio di lavoro per
passare le cose da AIP espanso a sector (o da sector ad AIP espanso dove le eccezioni lo consentiranno tipo il file
.geo)». Alla proposta dell'agente: mappa del Confronto con AIP e sector **sovrapposti** («1) sì», letto come sì alla
sovrapposizione proposta, non alle due mappe affiancate); eccezioni verso l'AIP oltre ai confini «per ora no, poi ci
penso». E in più: «nella sezione confronto si potesse anche dire metti questa cosa (tipo selezioni un'area) in questo
file, oppure metti tutto il contenuto di questa sezione in questo file».

| Spazio | Cosa ha | Repo |
|---|---|---|
| **AIP espanso** | Indice come l'AIP, la **sua** mappa, la scheda della voce con le correzioni; Lettura dei PDF e consegna | AIP espanso |
| **Sector** | la **sua** mappa, Sfoglia, Scheda, Problemi; Testo; Consegna | sector |
| **Confronti** | sezione per sezione uguale / diversa / manca / solo nel sector, la mappa con AIP e sector sovrapposti, i gesti che passano le cose da un lato all'altro | scrive nell'uno o nell'altro; ognuno si consegna dal suo spazio |

- **D14** Tre spazi nella barra: AIP espanso, Sector, Confronti; le Impostazioni a parte. Sostituisce D8 (due mondi).
- **D15** Un motore di mappa (Leaflet, `sectorlab-mappa.js`, gli strati del Core), **tre mappe**: ognuna col suo stato
  (strati accesi, vista, scelta), che non si perde cambiando spazio. Sostituisce D2 (una mappa con un solo stato).
- **D16** I due lati si disegnano allo stesso modo: i punti dell'AIP coi simboli di Aurora del profilo scelto (come quelli
  del sector), le linee e le aree coi colori dello schema dove l'AIP ha un genere che il sector conosce. Un fix dell'AIP
  e lo stesso fix del sector si devono vedere uguali.
- **D17** I confronti stanno nello spazio Confronti (non più dentro l'Indice: sostituisce D10). Nell'Indice una voce dice
  solo «nel sector: uguale / diversa / manca» e porta alla sua riga del Confronto (D11 resta: i legami record ↔ voce).
- **D18** Nel Confronto la mappa mostra i due lati **sovrapposti**, in due colori fissi (AIP / sector), con la riga scelta
  in evidenza; si accende un lato, l'altro o tutti e due.
- **D19** «**Metti in questo file**»: una voce (scelta nell'elenco o sulla mappa), più voci spuntate, o **tutta una
  sezione** (tutte, o solo le mancanti / le diverse) vanno nel file che l'AOD sceglie. La destinazione proposta è quella
  di oggi (le regole delle slice F6/F7: ENR 5 in `italy.prohibit|danger|restrict`, CTR e ATZ nel `MAPS` dello `.str` dello
  scalo, le procedure nel loro `.sid`/`.str`…); l'elenco dei file mostra solo quelli **compatibili** col genere della voce
  (lo stesso formato di righe), e il punto del file (in fondo, prima/dopo una voce, dentro un blocco). Prima di scrivere,
  l'anteprima del diff; poi è una modifica in sospeso come le altre (una voce nella storia, si annulla tutta insieme).
- **D20** Dal sector all'AIP espanso solo dove la regola lo consente: oggi i confini da `itgeo.geo` (eccezione di F6).
  Le altre eccezioni le dice il committente. Il gesto scrive una correzione nel file delle correzioni dell'AIP espanso,
  mai un PDF.

**Approvate dal committente** (20 ottobre sera, «D1) Perfetto … D7) Si», nell'ordine D14-D20). E ancora:
- **D21** La pagina **Testo** (D12, D13) sta nello **spazio Sector**, accanto a Mappa e Consegna: quel che si scrive è
  una modifica in sospeso del sector come i gesti; dalla Scheda «apri nel Testo», dal Testo alla mappa del sector; dal
  Confronti l'anteprima di «Metti in questo file» può aprire il file di destinazione nel Testo (committente: «Ok, va bene,
  perfetto»). Resta dopo la R9, salvo che il committente la chieda prima.
- **D22** L'**AIP espanso** si modifica **solo coi campi dei moduli** della scheda della voce (Correggi, Togli, Ritira),
  che scrivono nel file delle correzioni; il modello del ciclo si riscrive sempre dai PDF più le correzioni, e nessun
  testo a mano ne altera la struttura (committente: «l'AIP espanso rimane editabile solo i campi di input appositi, così
  da non alterare la struttura»). Niente pagina Testo per l'AIP.

🟡 Proposte dell'agente, da confermare quando si arriva lì: «selezioni un'area» letto come «scegli una voce» (un clic
sull'elenco o sulla mappa) e più voci con le caselle; una selezione a rettangolo sulla mappa solo se il committente la
chiede. La tabella dei file compatibili per genere si **misura** sul sector (quali formati tengono già quel genere di
righe), non si inventa.

## §5 — Le slice

- **R0** I due difetti di §2: conferma su «Cambia cartella» con modifiche in sospeso; il link della PR dell'AIP.
- **R1** L'impianto: linguette e pagine, barra nuova e uguale nelle due finestre, Salva in barra con Ctrl+S, Impostazioni
  come pagina (con Cambia cartella e Registro). Sector resta com'è.
- **R2** Consegna: Modifiche, Ramo e Aurora in una pagina sola, nell'ordine del giro.
- **R3** AIP come pagina: elenco dei confronti, tabella dei risultati, moduli, git dell'AIP; mappa accanto.
- **R4** La Scheda per gruppi, i gesti sul file nel pannello del file, il legame record → voce AIP, un solo editor dei
  vertici per i `.geo`.
- **R5** I verbi (D6), i rifiuti accanto al tasto, le scorciatoie (Ctrl+F per la ricerca).

Ogni slice si prova **a schermo** sul banco, su uno e su due schermi, oltre ai test.

## §5-bis — Le slice dopo la seconda forma

R0 e R1 sono fatte. Le altre si rifanno così:

- **R2** I due mondi nella barra (D8): AIP (Indice, Lettura e consegna) e Sector (Mappa, Testo, Consegna); le pagine di
  oggi ci entrano com'è (l'AIP intero in «Lettura e consegna» finché non c'è l'Indice).
- **R3** L'Indice dell'AIP (D9): l'albero per sezioni, la mappa, la scheda della voce, le correzioni.
- **R4** I confronti nell'Indice e i legami record ↔ voce (D10, D11).
- **R5** Le due consegne: quella del sector come giro (modifiche → Salva → Registra → vetrina → Manda), quella
  dell'AIP (lettura, segnalazioni, Registra/Manda).
- **R6** La pagina Testo (D12, D13).
- **R7** La Scheda per gruppi (D5), un solo editor dei vertici per i `.geo`.
- **R8** I verbi (D6), i rifiuti accanto al tasto, le scorciatoie.

## §5-ter — Le slice dopo la terza forma (piano del 20 ottobre sera, approvato)

R0-R3 fatte; dopo la R3 l'ambiente di lavoro (primo avvio, cartelle fisse, simboli dal profilo). Le slice da R4 in poi
si rifanno così. Ognuna: test, banco a schermo su uno e due schermi, eseguibile di prova, e il via del committente prima
della successiva.

- **R4 — Tre mappe, un motore** (D14, D15). `StatoDellaMappa` per spazio (strati accesi, vista, scelta) nella sessione;
  il componente `Mappa` riceve lo spazio; la barra con AIP espanso · Sector · Confronti. L'Indice dell'AIP ha la sua
  mappa con l'AIP espanso, la pagina Mappa del sector la sua; cambiando spazio ognuna resta com'era. Le pagine di oggi
  entrano negli spazi come sono («Lettura e confronti» nel Confronti finché non c'è la R6).
- **R5 — Due lati, un disegno** (D16). Lo strato dell'AIP coi simboli di Aurora (punto significativo ENR / terminale,
  VOR, VOR/DME, DME, TACAN, NDB, scali per tipo) e coi colori dello schema dove il genere ha un corrispondente nel sector;
  coi colori «del Lab» i suoi colori di genere come oggi. Prova a schermo: lo stesso fix dai due lati.
- **R6 — Lo spazio Confronti, in lettura** (D17, D18). Le sezioni (CTR e ATZ, ENR 5, ENR 3 attese, ENR 3 rotte, ENR 4,
  AD 2 piste, SID e STAR) con i loro conteggi; per sezione la tabella uguale / diversa / manca / solo nel sector; la mappa
  sovrapposta in due colori con la riga scelta in evidenza. I confronti di «Lettura e confronti» si spostano qui; quella
  pagina torna «Lettura e consegna». Nell'Indice il segno «nel sector: …» con il rimando alla riga.
- **R7 — I gesti di oggi nel Confronti**. Porta nel sector, Sostituisci, le attese, le procedure, le piste, le rotte,
  Scrivi i tracciati: le stesse regole, dalla tabella del Confronto; il diff e la Consegna del sector come oggi.
- **R8 — «Metti in questo file»** (D19). Prima la **misura**: per ogni genere, quali file del sector hanno già quel
  genere di righe (la tabella dei compatibili, in carta). Poi: una voce, le voci spuntate o tutta la sezione (tutte /
  mancanti / diverse) → scelta del file fra i compatibili (proposto quello delle regole di oggi) e del punto → anteprima
  del diff → modifica in sospeso, una voce sola nella storia. Rifiuti accanto al tasto: file non compatibile, voce che nel
  file c'è già (si propone Sostituisci), righe coi metadati.
- **R9 — Verso l'AIP espanso** (D20). «Porta nell'AIP espanso» per i confini da `itgeo.geo`, come correzione; il resto
  quando il committente dice le eccezioni.
- Poi, come prima: le due consegne (ex R5), la pagina Testo (ex R6), la Scheda per gruppi (ex R7), i verbi (ex R8).

## §6 — Il lavoro

**R0 fatta** (20 ottobre). «Cambia cartella…» con modifiche in sospeso non chiude più subito: sotto la barra compare
«Hai N modifiche non salvate: cambiando cartella le perdi», con «Perdile e cambia cartella» e «Resta qui»; senza
modifiche chiude come prima. Il link della PR dell'AIP è diventato il tasto «Riaprila», che apre la pagina nel browser di
Windows come «Manda» (la finestra del Lab rifiuta i `target="_blank"`). Test: Lab 1 261 (+1).

**R1 fatta** (20 ottobre). L'impianto delle quattro pagine:
- `PaginaDelLab` e `SessioneDelLab.Pagina` (una per le due finestre); `BarraDelLab` è la barra di tutt'e due: nome del
  sector, linguette Sector · AIP · Consegna · Impostazioni, «N errori · M avvisi» (porta ai Problemi, anche da un'altra
  pagina: `MostraIProblemi` e una richiesta che la colonna consuma), il ramo (porta a Consegna), ↶ ↷, **Salva (N)**,
  «Pannelli a parte» / «Pannelli qui» (solo nella principale), il tema. **Ctrl+S** salva (fuori dai campi dove si sta
  scrivendo; lì ferma solo il «salva pagina» del browser).
- `PannelliDellaPagina` dà i pannelli della pagina, uguali nella principale e nella finestra dei pannelli. Sector come
  prima; AIP: l'AIP espanso coi confronti in una colonna larga (34rem, 55% coi due schermi), Scheda e Modifiche
  accanto; Consegna: Modifiche, Ramo e Aurora in tre colonne; Impostazioni: il sector aperto (percorso, numeri, ISC,
  Registro, Cambia cartella con la domanda della R0) e le impostazioni.
- La mappa non si smonta mai (D2): Consegna e Impostazioni la nascondono (`lab-con-la-mappa`, `display: contents` /
  `none`).
- I riquadri di AIP, Ramo, Aurora e Impostazioni non sono più tendine (`<details>`): sono sezioni in pagina
  (`lab-riquadro`), coi `data-` di prima. Il loro interno resta com'era: lo riordinano la R2 (Consegna) e la R3 (AIP).
- Test: Lab 1 266 (+5, `PagineDelLabTests`); i test che aprivano AIP, Ramo e Aurora dalla barra scelgono prima la loro
  pagina. A schermo sul banco: le quattro linguette, un confronto dei CTR (6 diversi, 39 uguali) col clic che apre la
  Scheda e la mappa accanto, Consegna, Impostazioni, la finestra dei pannelli che segue la pagina.

**R2 fatta** (20 ottobre). Le linguette in due gruppi, AIP e Sector (D8), le Impostazioni a parte. Il test
dell'AIP che riscriveva `li.json` subito dopo un confronto è caduto una volta nella suite intera («file in uso»): ora
riprova la scrittura.

**R3 fatta** (20 ottobre). L'Indice dell'AIP (D9), prima pagina del mondo AIP; la pagina di prima si chiama «Lettura e
confronti».
- `IndiceDellAip` (Core) mette le voci dello strato come nell'AIP: parti ENR 2, ENR 3, ENR 4, ENR 5, AD 2; dentro, la
  sezione e, se ci sono più generi, un gruppo per genere (P, R, D; SID, STAR…); AD 2 scalo per scalo, con lo scalo in
  testa. I numeri si ordinano come numeri (M72 prima di M726, LI R2 prima di LI R12). Si costruisce dallo strato, non dal
  modello: l'indice di una voce è quello della mappa, e un clic usa lo stesso `Scegli` della mappa.
- I nomi delle sezioni sono quelli dell'ICAO che l'AIP Italia segue; le sottosezioni di ENR 3 e di ENR 5.3, che l'Italia
  organizza a modo suo, hanno il solo codice (meglio che un nome inventato: i titoli veri si leggono dai PDF, da fare).
- Le ATZ di AD 2 non dicono lo scalo: vanno nel loro scalo per nome («Roma Fiumicino ATZ» → LIRF, «Napoli ATZ» → LIRN
  se un solo scalo comincia con «Napoli»); le altre in un gruppo con la loro sezione.
- La ricerca guarda il titolo e, in AD 2, il nome dello scalo («Fiumicino» trova LIRF con le sue procedure).
- La pagina carica da sé lo strato dell'AIP (lo stesso di «Mostra l'AIP espanso sulla mappa»); senza cartella dice dove
  sceglierla. Scheda della voce (Correggi, Togli, Ritira) e mappa accanto.
- La barra sta in 1400 px: il ramo si accorcia, «⧉ Pannelli».
- Test: Lab 1 273 (+7: `IndiceDellAipTests` 6, una prova della pagina). A schermo sul banco, AIP espanso vero 2610:
  ENR 2 329 voci, ENR 3 571, ENR 4 1 320, ENR 5 793, AD 2 447; «Fiumicino», il clic sull'ATZ apre la scheda e la mappa la
  inquadra.

**Dopo la R3** (20 ottobre). Il committente vedeva nell'AIP espanso «solo ENR 2 e 5». Non mancavano i PDF: il suo Lab
leggeva il clone dell'AIP espanso preso il 19 ottobre alle 01:17 sul ramo `main`, mai aggiornato, che allora aveva
solo ENR 2 ed ENR 5 (870 forme). Alle 14:39 il clone è passato da sé sul ramo `aip/2610`, che ha tutto (3 677 forme): il
Lab però non rilegge lo strato quando il repo cambia ramo. Da fare nella R5 (la consegna dell'AIP): dire ramo e
ciclo del repo dell'AIP e quanto è vecchio, «Chiedi le novità al server», e rileggere lo strato quando il repo cambia.
Guardando, un difetto dell'indice: i punti RNAV degli scali hanno la sezione «AD 2 LIRF» e finivano in «Altro»; ora
vanno nel loro scalo. Test: Lab 1 274 (+1).

**Da sistemare** (trovato il 20 ottobre): `VipiSectorLab.exe --autoprova --cartella <copia>` scrive nei dati del Lab di chi
lo usa (`%LOCALAPPDATA%\VipiSectorLab`): la copia diventa «ultima cartella» e va in cima agli «Aperti di recente». La
prova d'avvio non deve ricordare niente. **Sistemato** (20 ottobre sera), con l'argomento nuovo `--dati <cartella>`: i
dati del Lab (ultima cartella, recenti, impostazioni, backup, registro, WebView2) stanno lì invece che in
`%LOCALAPPDATA%\VipiSectorLab`; l'autoprova senza `--dati` usa `%TEMP%\VipiSectorLab-autoprova`
(`SessioneDelLab.CartellaDeiDati`). Serve anche al committente per simulare il primo avvio di un AOD senza toccare il
suo Lab: `SectorLab-prova\PrimoAvvio\` (lanciatore e LEGGIMI, fuori repo). Test: Lab 1 278 (+4,
`CartellaDeiDatiTests`). Provato sull'eseguibile pubblicato: autoprova 0, i dati di `%LOCALAPPDATA%` non cambiano.

**Primo avvio: i repo li prende il Lab** (20 ottobre sera, chiesto dal committente prima del giro da zero: «che il lab
cloni da sé le repo che gli servono, con l'AOD che gli dà il link»). I due gesti c'erano già (F4 slice 2 «Prendi il
sector», F6 «Prendi l'AIP espanso»); mancava quel che serve a chi parte da zero:
- sulla schermata d'apertura «Non hai il sector? Prendilo dal repo» è **già aperto** quando non c'è nessun sector recente;
- il riquadro dell'AIP ha il campo **«Dal repo»** accanto a «Prendi l'AIP espanso» (prima l'indirizzo si cambiava solo
  nelle Impostazioni);
- il link si incolla com'è dal browser: `IndirizzoDelRepo.Pulito` tiene di GitHub solo `proprietario/repo`
  (`…/tree/master`, `…/pulls`, la barra in fondo, le virgolette spariscono); un indirizzo che comincia con «-» si
  rifiuta e il clone mette `--` prima dell'indirizzo (per git sarebbe un'opzione: `--upload-pack=…` esegue un comando);
- la presa del sector scarica anche `ColorSchemes` (gli schemi di Aurora che la mappa legge), se il repo ce l'ha.
Test: Lab 1 291 (+13: `IndirizzoDelRepoTests` 9, `PresaDelSectorTests` 2, `PrimoAvvioTests` 2). A schermo sul banco con
dati vuoti: la tendina aperta, il sector aperto, il campo «Dal repo» dell'AIP. La presa vera da GitHub dal browser del
banco no (il cancello ferma GitHub nel browser): i suoi comandi git, lanciati a mano sul repo di prova, prendono
`SectorFiles` in 2,6 s (18 MB) e saltano `ColorSchemes`, che quel repo non ha.

**Primo avvio in un posto solo** (20 ottobre sera, committente: «così so che è tutto in un posto solo, e all'avvio deve
creare anche una cartella per mettere i pdf presi dall'AIP italia»). Senza sector aperti prima, la schermata d'apertura
ha il riquadro **«Primo avvio»**: i due link (sector, AIP espanso), la **cartella di lavoro**, l'elenco delle tre
cartelle che il Lab ci mette (`sector`, `aip-espanso`, `pdf-aip`) e **«Prepara tutto»**, che prende i due repo, crea la
cartella dei PDF, le scrive nelle impostazioni e apre il sector; sotto, «Hai già il sector?» con «Apri». Dopo il primo
sector «Prendilo dal repo» torna com'era (per il ciclo dopo, una prova). Si può rifare: un repo già preso in quella
cartella si salta, così un passo fallito (rete, credenziali) si riprova senza ricominciare (`Core.Git.PrimoAvvio`,
`SessioneDelLab.PreparaIlPrimoAvvioAsync`). Test: Lab 1 295 (+4: `PrimoAvvioCoreTests` 3, `PrimoAvvioTests` +1). A
schermo sul banco con dati vuoti, coi due link puntati a un clone locale del repo di prova (GitHub nel browser no):
le tre cartelle, le impostazioni scritte, il sector aperto.

**Cartella di Aurora** (20 ottobre sera, committente: «ora che Aurora mette tutto qui AppData\Roaming\IVAO\AuroraWindows,
lo schema colori e i profili, fai scegliere all'utente dove prenderlo con quello sopra come default»). Nelle
Impostazioni c'è **«Cartella di Aurora»**, di base `%APPDATA%\IVAO\AuroraWindows` (la passa l'eseguibile a
`ServerDelLab.Crea`; nei test niente, così non leggono l'Aurora vera del PC). Gli schemi di colore si prendono da lì,
in `ColourSchemes` (il nome che usa Aurora, all'inglese) o `ColorSchemes`; se lì non ce n'è nessuno, dal clone come
prima (`SchemiDiAurora.Cartella`). Cambiare la cartella rilegge gli schemi e ricolora la mappa; sotto il campo si dice
quanti schemi e da dove. **I profili** (`Profiles\*.cpr`) il Lab oggi non li legge da nessuna parte (legge solo i
`.cpr` dentro il sector): la cartella è già quella giusta per quando serviranno. La vetrina («Porta in Aurora») resta
su `%APPDATA%` com'era. Test: Lab 1 297 (+2, `ColoriDellaMappaTests`). A schermo sul banco: con la cartella di Aurora del
PC 5 schemi da `ColourSchemes`, `LIRR_RDR_V1.0.clr` scelto e la mappa coi colori di Aurora.
Poi (committente, guardando il riquadro: «già qui mi devi dare la possibilità di selezionare da dove prendere profilo e
colori») la stessa scelta sta anche nel **riquadro «Primo avvio»**: «Cartella di Aurora (profili e colori)», già
scritta con quella di base, con «Sfoglia…» e, sotto, che cosa c'è (`SchemiDiAurora.NellaCartellaDiAurora`: «Qui ci sono
5 schemi di colore e 36 profili di Aurora», o «Questa cartella non c'è: Aurora è installata?»). Test: Lab 1 299 (+2). A
schermo sul banco: la cartella di Aurora del PC dà 5 schemi e 36 profili.

**Il Lab lavora nel suo ambiente** (20 ottobre sera, committente: «Toglimi l'opzione "hai già un sectorfile"… Prevediamo
che il lab lavori nel suo ambiente e basta che è più sicuro e più facile da gestire»). Al primo avvio la schermata
d'apertura ha solo il riquadro «Primo avvio»: niente «Apri» su un clone che c'è già. Dopo il primo sector la schermata
resta com'era (Apri, recenti, «Prendilo dal repo»), finché il committente non dice fin dove portare la regola. Test: Lab
1 300 (+1; i due test di `PaginaConLaMappaTests` che aprivano dal campo al primo avvio ora lo fanno dopo il primo sector,
e il rifiuto al primo avvio si prova su «Prepara tutto»).

Poi la regola fino in fondo (committente, sulla proposta: «più è isolato l'ambiente di lavoro meno casino facciamo»):
- **l'ambiente di lavoro** è la cartella che «Prepara tutto» ha preparato, ricordata (`cartella-di-lavoro.txt`);
  il riquadro «Primo avvio» c'è finché non c'è l'ambiente (aprire un sector con `--cartella` non lo fa);
- all'avvio, senza `--cartella`, il Lab **riapre da sé** il sector dell'ambiente: l'ultimo aperto se sta lì, se no
  `sector` (`SessioneDelLab.SectorDaRiaprire`, in `Program`);
- la schermata d'apertura con l'ambiente ha **l'elenco dei sector dell'ambiente** (`sector` per primo; `aip-espanso` e
  `pdf-aip` non sono sector) e **«Prendi un altro sector»**, che chiede solo il nome: diventa `<ambiente>\<nome>`
  (lettere, cifre, `-`, `_`, `.`; `PrimoAvvio.PercheNonVaUnAltroSector`). Spariscono «Apri» su una cartella qualsiasi,
  «Sfoglia…» e i recenti;
- **guardie nella sessione**: con l'ambiente, `ApriAsync` e `PrendiIlSectorAsync` rifiutano una cartella fuori
  («Il Lab lavora nel suo ambiente…»; `PrimoAvvio.NellAmbiente`, che non scambia `D:\IVAO2` per `D:\IVAO`);
- nelle Impostazioni «Cambia cartella…» diventa **«Altro sector dell'ambiente…»** (stesso `data-tasto`, D7): chiude il
  sector e torna all'elenco, chiedendo prima se ci sono modifiche non salvate.
Test: Lab 1 303 (+3). Sul banco: primo avvio coi repo locali, il sector aperto, «Altro sector dell'ambiente…» →
l'elenco con `sector`, «Prendi un altro sector», né «Apri» né il primo avvio.

Poi (committente: «blocca anche le cartelle di PDF e AIP espanso»; e subito dopo: «lo sfoglia lo devi lasciare per poter
rapidamente aprire la cartella e metterci i pdf dentro») con l'ambiente le cartelle dei PDF e dell'AIP espanso sono
**fisse**, `<ambiente>\pdf-aip` e `<ambiente>\aip-espanso`, qualunque cosa ricordino i dati (`CartelleDellAipFisse`):
cambiarle si rifiuta («Il Lab lavora nel suo ambiente…»), «Prendi l'AIP espanso» non chiede più «Dove», nelle
Impostazioni i due campi sono in sola lettura. Al posto di «Sfoglia…» c'è **«Apri la cartella»** (anche accanto ai PDF
nel riquadro AIP), che apre la cartella in Esplora risorse per trascinarci i PDF (`ApriLaCartellaDellAip`: solo quelle
due). Restano libere la vetrina e la cartella di Aurora. Test: Lab 1 304 (+1).

**Simboli dal profilo di Aurora** (committente: «per le mappe devi usare i simboli di Aurora così la visualizzazione è
uguale»). Misurato prima di toccare: i disegni che il Lab usava (copiati il 17 ottobre da `BASIC_RADAR.cpr`) sono
uguali ai `SYMBOLS_` dei punti in 35 profili su 36 della cartella di Aurora del committente (il 36°, `LIPP_CE1_CTR`, ha
i suoi). Ora non sono più copiati: `SimboliDellaMappa.DallaCartellaDiAurora` li legge dal profilo scelto nella cartella di
Aurora (Impostazioni, «Profilo di Aurora (simboli dei punti)», di base `BASIC_RADAR.cpr`, ricordato in
`profilo-di-aurora.txt`); i disegni copiati restano solo quando non c'è un profilo. 🔴 Del `.cpr` si leggono SOLO le
righe `SYMBOLS_<ruolo>=` dei 19 ruoli dei punti: il profilo tiene anche `[Connection]`. Un disegno che non è 13×13 resta
quello di base. Cambiare profilo o cartella di Aurora ridisegna la mappa. Test: Lab 1 306 (+2). Sul banco con la
cartella di Aurora del PC: 36 profili nella scelta, `BASIC_RADAR.cpr` scelto. Sul banco con un ambiente: i due campi
in sola lettura con le cartelle dell'ambiente, i due «Apri la cartella», nessun «Sfoglia…» (non premuto: aprirebbe
Esplora risorse sullo schermo del committente).

**R4 fatta** (20 ottobre sera): tre mappe, un motore (D14, D15).
- `SpazioDiLavoro` (Aip, Sector, Confronti) e `StatoDellaMappa` (strati accesi, vista, voci e generi spenti, le loro
  versioni), uno per spazio, in `SessioneDelLab`. Lo spazio attivo lo dà la pagina (Indice → AIP espanso, Mappa →
  Sector, Lettura e confronti → Confronti); Consegna e Impostazioni, senza mappa, lasciano quello di prima. `Accesi`,
  `InVista`, `Spenti` e i gesti che li cambiano parlano con la mappa dello spazio attivo: i pannelli di oggi non sono
  cambiati. Scegliere un record del sector dallo spazio dell'AIP accende il suo strato sulla mappa del Sector.
- Gli strati di ogni mappa (`StratiDi`): AIP espanso = coste e strato dell'AIP; Sector = i suoi, **senza** l'AIP;
  Confronti = tutti e due. Lo strato dell'AIP si accende nelle mappe dell'AIP e del Confronti.
- `Mappa` riceve lo spazio; l'id del suo elemento è `lab-mappa` (Sector) o `lab-mappa-aip` / `lab-mappa-confronti`.
  In `Home` una mappa nasce la prima volta che si entra nel suo spazio e poi resta, nascosta: strati, vista, scelta e
  zoom restano. Nella barra tre gruppi: AIP espanso (Indice), Sector (Mappa, Consegna), Confronti (Lettura e confronti).
- `sectorlab-mappa.js`: lo stato globale è diventato uno per mappa (`mappe[id]`); ogni funzione pubblica ha l'id come
  ultimo argomento (senza, la mappa del sector), così i test che leggono gli argomenti non sono cambiati.
- 🔴 Trovato sul banco: una mappa nata con il riquadro a zero (nascosta, o in una finestra stretta) si inquadrava sulle
  coste allo zoom più lontano — un puntino al centro. Ora l'inquadratura aspetta che il riquadro abbia una misura (la fa
  il ResizeObserver).
- Test: Lab 1 310 (+4, `TreMappeTests`; `PagineDelLabTests` e il test della vista aggiornati: il gruppo Confronti,
  l'id della mappa come secondo argomento). Sul banco a 1400 px, copia del sector e AIP espanso di prova 2610: l'Indice
  con la sua mappa (solo coste e AIP, inquadrata sull'Italia), il Sector coi punti accesi e senza AIP, il Confronti coi
  due lati; lo zoom della mappa dell'AIP uguale dopo un giro nel Sector.

**R5 fatta** (20 ottobre sera): due lati, un disegno (D16). Le forme dell'AIP portano la stessa **chiave dello schema** e
lo stesso **tipo del punto** della forma del sector che tiene quel genere (`AipSullaMappa.ChiaviDiAurora`, `TipoDelPunto`):
punti di ENR 4.4 `FIX:0` (FIX_ENR) e di AD 2 `FIX:1` (FIX_TERM); radioassistenze per `NavaidKind` (VOR, VOR/DME, VORTAC,
TACAN, DME → `VOR:0-4`; NDB e locator → `NDB`); scali `APT:0`; P, R, D con PROHIBITED, RESTRICTED, DANGER; FIR/UIR
ARTCC; rotte AIRWAYLOW, quelle di ENR 3.2 AIRWAYHIGH (🟡 proposta: superiori nella struttura ICAO); SID e salite iniziali
SID, STAR, transizioni TRANSITIONS, attese HOLDINGS. I generi che il sector non tiene in un file suo (CTR, ATZ, TMA, CTA,
RMZ/TMZ, TSA/TRA, militari, luci, pericoli) restano col colore del genere anche coi colori di Aurora: il server non gli
scrive `k` (prima del ritocco prendevano il grigio dello «sconosciuto»). Lo strato dell'AIP ha ora colori e simboli come
quelli del sector e si riprende quando cambiano schema o profilo. Test: Lab 1 312 (+2).
Sul banco (copia del sector, AIP espanso di prova 2610): i 1 471 punti, le 55 radioassistenze e i 5 scali col simbolo;
ABADI, ADFAM e OSTEG nella stessa posizione e con lo stesso colore dei due lati. Dove il simbolo cambia è il **dato**
a cambiare, ed è materia del Confronti: ABADI e OSTEG il sector li segna fix di confine (FIX_BOUND, 658 punti così),
l'AIP non lo dice; OST il sector lo scrive VOR (FIX_VOR), l'AIP VOR/DME (FIX_VORDME).

**R6 fatta** (20 ottobre sera): lo spazio Confronti, in lettura (D17, D18).
- `SezioneDelConfronto` (13: CTR, ATZ, ATZ delle torri, rotte, attese, radioassistenze e punti di ENR 4, aree di ENR 5,
  scali, piste, frequenze, radioassistenze degli scali, SID e STAR) e `RigaDelConfronto` (esito, AIP, sector, file,
  record, dettagli) in `ConfrontiDelleSezioni`: le righe dei cinque confronti di oggi portate a una forma sola. I
  confronti sono quelli di F6/F7, gli stessi numeri (CTR sulla copia: 45 righe, 6 diverse).
- Nella sessione `ConfrontaLaSezioneAsync` (carica da sé lo strato dell'AIP), `Confronti` (le righe di ogni sezione,
  finché la cartella non cambia), `ScegliLaRigaDelConfronto`: sceglie il record del sector e la voce dell'AIP insieme
  (`ConfrontiDelleSezioni.VoceDelNome`, 🟡 per nome: uguale, o che comincia col nome — «OST» → «OST VOR/DME»).
- La pagina **Confronti** (`ConfrontiVista`) nel gruppo Confronti: le sezioni per parte dell'AIP con «N da guardare»,
  la tabella della sezione aperta col filtro per esito (di base senza gli uguali), «Rifai»; accanto la Scheda del
  sector e quella della voce dell'AIP. La pagina di prima resta come **«Lettura e gesti»** (PDF, lettura, consegna
  dell'AIP, i gesti dei confronti) finché la R7 non sposta i gesti.
- La mappa del Confronto (`dueLati`): l'AIP **arancio e tratteggiato**, il sector **blu e pieno** (`--lab-lato-aip`,
  `--lab-lato-sector`, chiari e scuri), i simboli dei punti nel colore del lato, le coste come sono; la **coppia** della
  riga scelta più spessa e inquadrata insieme.
- Test: Lab 1 318 (+6: `ConfrontiDelleSezioniTests` 4, due in `ConfrontoDiEnr4Tests`; il numero delle linguette in
  `PagineDelLabTests`). Sul banco (copia del sector, AIP di prova 2610): CTR 45 righe, 6 diverse; la riga di Amendola
  apre «Amendola CTR Zona '1'» e «LIBA CTR» di `liba.str`, la mappa li mostra nei due colori; punti 130 righe, 98 da
  guardare. Nessun errore nella console.
- Con tutto l'AIP acceso la mappa del Confronto era piena. **Ritocco dopo la R6** (committente: «fai prima il ritocco,
  poi la R7»): aprendo (o confrontando) una sezione, sulla mappa del Confronto solo la sezione — le coste, gli strati del
  sector che la tengono (`ConfrontiDelleSezioni.StratiDelSector`: CTR, ATZ e procedure → «SID e STAR» dei MAPS e degli
  `.sid`/`.str`; ATZ delle torri → settori; scali, frequenze, punti → punti…) e dello strato dell'AIP i soli generi della
  sezione (`GeneriDellAip`, gli altri spenti come i generi di uno strato). Il resto si riaccende dalle caselle. Sul banco:
  i punti, coi soli «Punti» dell'AIP accesi, e RF532 (sector a 0,75 NM dall'AIP) che ora si legge.

**R7 fatta** (20 ottobre notte): i gesti di oggi nel Confronti.
- `RigaDelConfronto` porta la riga di partenza (`Origine`, il tipo che i gesti vogliono) e `DaGuardare` (anche una riga
  uguale con un gesto da fare: l'uso di un fix, i tratti di una rotta, un'area di ENR 5 da dividere, una radioassistenza
  stampata diversa in ENR 4.1). «N da guardare» e il filtro di base le contano.
- Le righe di una sezione si leggono **vive** dall'ultimo confronto quando è di quella sezione (`RigheDellaSezione`,
  `SezioneViva`): i gesti rifanno il confronto, e la tabella è quella di adesso. Riaprire una sezione il cui confronto
  non è più l'ultimo (punti dopo radioassistenze: sono lo stesso confronto di ENR 4) **lo rifà**, perché i gesti vogliono
  quello di adesso; i gesti si mostrano solo su una sezione viva.
- `GestiDellaRiga` (Sostituisci, Dividi, Porta nel sector con lo scalo, Porta la procedura, Scrivi i tratti, Porta la
  rotta, la scelta delle attese, Correggi l'uso, Sostituisci delle radioassistenze, delle piste e degli scali, Porta la
  pista…) e `GestiDellaSezione` (Porta tutte di ENR 5 e di ENR 4, Scrivi i tracciati delle SID uguali, Mappa delle SID,
  Scrivi i tratti di tutte, Porta le attese selezionate, Correggi l'uso di tutti, e le note della sezione): il markup e i
  gestori di prima, con gli **stessi `data-tasto`** (D7). `ScelteDelleAttese` tiene le attese spuntate fra righe e
  sezione. Nella pagina, il riassunto della sezione (`data-riassunto-confronto`: «uguale 39 · diversa 6»).
- «Lettura e confronti» torna **«Lettura e consegna»** nello spazio AIP espanso: PDF, lettura, repo dell'AIP espanso,
  strato sulla mappa, voci da aggiungere; tolti i confronti (356 righe) e i loro gestori (147). Il gruppo Confronti ha la
  sola pagina Confronti.
- Test: Lab 1 319 (+1, i gesti dal Confronti in `ConfrontoDiEnr4Tests`; i quattro test dei gesti di CTR e ATZ in
  `AipNellaBarraTests` ora passano dalla pagina Confronti; tre attendono il gestore della casella «sulla mappa» con
  `ChangeAsync`, perché lo strato è già caricato dal Confronti e la ricarica finiva dopo la scelta della voce). Sul banco
  (copia del sector, AIP di prova 2610): CTR con «Sostituisci»; punti con «Correggi l'uso di tutti (4)» e «Porta tutte
  (95)»; aree di ENR 5 con «Porta tutte (214)»; SID e STAR con «Scrivi i tracciati (280 voci)» e «Mappa delle SID di
  LIED»; «Sostituisci» su Amendola: il CTR diventa «uguale 40 · diversa 5» e la barra «Salva (1)» (in memoria, non
  salvato: la copia non è cambiata).

**R8, la misura** (20 ottobre notte; prova temporanea sulla copia del sector, non committata). Quali file del sector tengono
già il genere di righe di ogni sezione, e dove i gesti di oggi scrivono:

| Sezione | File che tengono quel genere (record) | Dove scrivono i gesti di oggi | La scelta ha senso? |
|---|---|---|---|
| Punti (ENR 4.4) | **9 `.fix`**: itfix 1 256, APT 780, ESTERNI 641, secsi 527, VFR_NASCOSTI 512, MIL 185; TERM, FRA, ENR vuoti | sempre `itfix.fix`, in ordine di nome | **sì** |
| Radioassistenze | `itvor.vor` 121, `itndb.ndb` 27 (uno per tipo) | il file del tipo | no: un file per tipo |
| Aree di ENR 5 | `italy.prohibit` 10 143 linee, `italy.restrict` 2 221, `italy.danger` 664 (il quinto campo = il tipo); nessuna linea P/R/D nei 131 `.geo` | il file del tipo | no: il file dice il tipo (il colore in Aurora) |
| CTR, ATZ (MAPS) | voci MAPS in 88 `.str` su 90: CTR 45 in 45 file, ATZ 87 in 87 file | lo `.str` dello scalo che l'AOD scrive | già scelto (lo scalo) |
| ATZ delle torri | `twrs.tfl` 69 settori (e 27 altri `.tfl` dei settori ACC/APP) | `twrs.tfl` | da chiedere |
| Attese | `HOLDENR.hold` 68; `HLD-` in `.str` 202 | in rotta `HOLDENR.hold`, di procedura lo `.str` dello scalo | già scelto (rotta / scalo) |
| Rotte | `itawlow.lairway` 568 aerovie; `itawhigh.hairway` **vuoto** | `itawlow.lairway` | **sì** (basse / alte) |
| SID e STAR | 59 `.sid` (1 306 SID), 90 `.str` (854 procedure) | il `.sid`/`.str` dello scalo | no: lo scalo è nel dato |
| Scali, piste | `.ap` 5 e `.rw` 5: `OTHER/it…` e i quattro delle FIR (lirr, limm, lipp, libb) | `OTHER/it…` | **forse** (le copie delle FIR) |

🟡 Proposta dell'agente per la R8, da approvare: farla dove la scelta ha senso, in quest'ordine — **R8a** i punti (il file
fra i 9 `.fix`, per una voce, le spuntate o tutta la sezione; nel file in ordine di nome come oggi, o in fondo),
**R8b** le rotte (basse o alte), **R8c** scali e piste anche nei file delle FIR. Per ogni gesto: il file proposto è
quello di oggi, l'elenco mostra solo i file con lo stesso genere di righe, l'anteprima del diff prima di scrivere, una
voce sola nella storia. Le ATZ delle torri in un altro `.tfl`: da chiedere.

Il committente: «Vai con la R8a, le torri solo twrs.tfl e nei file .str» — le ATZ restano dove i gesti le scrivono oggi
(i settori in `twrs.tfl`, le voci MAPS negli `.str`), senza scelta del file. R8b e R8c aspettano il loro via.

**R8a fatta** (20 ottobre notte): «metti in questo file» per i punti.
- `Enr4NelSector.FileDeiPunti`: i `.fix` del sector, `itfix.fix` per primo. `PortaNelSectorDiEnr4(nomi, fileDeiPunti,
  inFondo)`: il file scelto (null = `itfix.fix`, com'era; un file che non è dei punti si rifiuta) e il posto (in ordine di
  nome, com'era, o in fondo). `AnteprimaDeiPunti`: per ogni punto la riga e dove va («prima di CALKE», «in fondo»), senza
  scrivere niente. Il confronto dei punti guardava già tutti i `.fix`: un punto messo in `APT.fix` non manca più.
- `MettiInQuestoFile` (il riquadro): il file (di base quello di oggi, segnato «di oggi»), il posto, l'anteprima, «Scrivi»
  (un gesto solo) e «Lascia stare». Per una riga: «Porta nel sector…» lo apre; per la sezione: «Metti le spuntate…» (le
  righe si spuntano) e «Porta tutte…». Le radioassistenze restano come prima (un file per tipo).
- La classe delle scelte delle attese è diventata `ScelteDelConfronto` (anche i punti spuntati); una spunta ridisegna la pagina e il tasto
  della sezione si accende (il ridisegno arriva subito dopo, non nello stesso giro: il test lo aspetta).
- Test: Lab 1 321 (+2: il punto nel `.fix` scelto, in fondo, con l'anteprima e il rifiuto; gli spuntati dalla sezione in
  `APT.fix`; il test della R7 ora conferma con «Scrivi»). Sul banco (copia del sector, AIP di prova 2610): i 9 `.fix`;
  CA501 in `APT.fix`, «prima di CALKE» o «in fondo»; «Scrivi»: punti mancanti 95 → 94, uguali 1 375 → 1 376, «Salva (1)»
  (in memoria: la copia non è cambiata).

**R8b fatta** (20 ottobre notte, «Vai con la R8b»): «metti in questo file» per le rotte, nelle aerovie basse o alte.
- `ConfrontoDiEnr3.FileDelleRotte`: i `.lairway` e gli `.hairway` del sector, `itawlow.lairway` per primo. Il confronto
  delle rotte e i tratti da scrivere leggono ora **tutti e due** i file (prima solo `itawlow.lairway`): una rotta messa
  nelle alte non manca più, e la riga apre il file dove sta. «Scrivi i tratti» fa una modifica per file, in un gesto solo.
- `PortaLaRotta(nome, fileDelleRotte)`: il file scelto (null = `itawlow.lairway`, com'era; un file che non è delle
  aerovie si rifiuta). Il posto lo dà il nome, come prima (in un file senza tracciati, come `itawhigh.hairway` di oggi
  tutto commentato, in testa). `AnteprimaDellaRotta`: il blocco e dove va («dopo la riga N (…)», «in testa al file»);
  una rotta con un'interruzione non ha anteprima e dice perché.
- `MettiInQuestoFile` con `Rotta`: la scelta fra i file delle aerovie (di base quello di oggi), niente scelta del posto,
  l'anteprima del blocco. Nella riga: «Porta nel sector…» lo apre (prima portava subito in `itawlow.lairway`).
- Test: Lab 1 323 (+2: la rotta nelle alte con l'anteprima, il rifiuto e il confronto che la trova; il riquadro nella
  pagina Confronti). Sul banco (copia del sector, AIP di prova 2610): i due file; M616 nelle alte, «in testa al file» col
  blocco di 6 righe; «Scrivi»: rotte mancanti 5 → 4, uguali 21 → 22, «Salva (1)» (in memoria: la copia non è cambiata).
- 🟡 Resta aperto: se le rotte di ENR 3.2 vadano di norma nelle alte (la proposta di §4-ter); oggi la scelta è dell'AOD,
  rotta per rotta, e il file proposto resta `itawlow.lairway`.

**R8c fatta** (20 ottobre notte, «Vai con la R8c»): scali e piste anche nei file delle FIR.
- La misura (copia del sector): cinque `.ap` e cinque `.rw` in `OTHER/`, quelli di tutta Italia (`itap.ap` 130 scali,
  `itrw.rw` piste di 110 scali) e le copie di lirr, limm, lipp, libb, ognuna caricata dal suo master (`LIRR.isc`…). Gli scali delle FIR
  stanno tutti anche in `itap.ap`. Le piste no: contate per scalo e testate (senza le righe `MAPS`), `itrw.rw` ne ha 135;
  non ci sono 9 delle 72 di `lirr.rw` (LIAQ, LIAT, LIAU, LICK, LIDP, LIQF, LIQQ, LIQY, LIZZ), 9 delle 47 di `limm.rw`, 11
  delle 50 di `lipp.rw`, 1 delle 17 di `libb.rw` (LIZZ, la voce finta che c'è in tutte). Una voce sta quindi di norma in
  due file, quello di tutta Italia e quello della sua FIR, ma non sempre: per questo i file si spuntano, non si impongono.
- `Ad2NelSector.FileDi` (i `.ap` o i `.rw`, quello di tutta Italia per primo) e `RigaNuovaDelloScalo`
  (`ICAO;elevazione;transizione;lat;lon;NOME;` coi valori dell'AIP, la transizione di AD 2.17 o 0, il nome in maiuscolo).
- **Porta nel sector…** su una pista che manca e (nuovo) su uno scalo che manca: il riquadro con una casella per file.
  Proposte dell'agente: il file di tutta Italia e le FIR che hanno già lo scalo (la FIR è quella del `.ap` che lo tiene:
  `lirr.ap` → `lirr.rw`); uno scalo nuovo non sta in nessuna FIR, e la sua FIR la spunta l'AOD. L'anteprima: la riga e,
  per file, dove va («dopo LICA 04/22»). «Scrivi»: tutti i file spuntati in un gesto solo. `PortaLaPista(riga, file)`,
  `PortaLoScalo(riga, file)`; null = il file di tutta Italia, com'era.
- **Sostituisci in tutti i file (N)** accanto a «Sostituisci» quando la stessa voce è diversa in più file: ogni riga
  coi valori dell'AIP diversi dalla sua, in un gesto solo (`SostituisciOvunqueInAd2`). «Sostituisci» resta, un file.
- Il posto di una riga nuova si cerca fra le voci del modello (nel `.rw` solo le piste della sezione `//PISTE`): dopo
  l'ultima con l'indicativo che non viene dopo, come prima.
- Test: Lab 1 327 (+4: la pista in itrw.rw e lirr.rw con proposte e anteprima, annullata in un colpo; lo scalo in itap.ap
  e nella FIR scelta; «Sostituisci in tutti i file»; il riquadro con le caselle nel Confronti). Sul banco (copia del sector,
  AIP di prova 2610): piste «uguale 13 · diversa 8», 7 voci diverse in più file; «Sostituisci in tutti i file (3)» su LICA
  10/28 → «uguale 16 · diversa 5», «Salva (3)» (in memoria: la copia non è cambiata). Nell'AIP di prova nessuna pista e
  nessuno scalo mancano: il riquadro delle caselle sul banco non si è visto, solo nei test.

**R9 fatta** (20 ottobre notte, «Vai con la R9»): dal sector all'AIP espanso, solo i confini (D20).
- Lo spazio Confronti ha una sezione nuova, **Confini e coste**, nella parte «Dal sector all'AIP»: le aree dell'AIP
  espanso (di ogni sezione: FIR, CTA, ENR 5, ATZ…) con un tratto che l'AIP nomina senza disegnarlo («Italian northern
  geographical border»), contro il tracciato di `itgeo.geo` (`ConfiniDelSector`, la rete della F6). Una riga per area,
  «solo nel sector» (il sector ha il tracciato, l'AIP no), coi tratti: quanti punti dal sector, quanti NM contro la retta.
  Quelle già portate sono «uguali», con la loro correzione. Aprendola, sulla mappa restano i generi delle sue aree.
- **Porta nell'AIP espanso** (la riga) e **Porta tutti nell'AIP espanso (N)** (la sezione): per ogni area una
  **correzione** nel file delle correzioni (D22: l'AIP espanso si cambia solo così), un salvataggio solo, sul ramo del
  ciclo come le altre correzioni. Nel `before` l'area del PDF con la frase; nell'`after` la stessa area con i tratti di
  confine fatti di rette sui punti del sector (i capi del tratto restano quelli dell'AIP). Fonte: «GEO/itgeo.geo del sector
  (eccezione del committente, 19 ottobre 2026)», da cui la sezione riconosce le aree già portate. Si ritira come le altre,
  dalla scheda dell'area: torna la frase. Il sector non si tocca. Un'area che non si traccia si dice e resta.
- **Rette** al posto del tratto di confine, perché il modello dell'AIP (comune, multi-Stato) non cambia; la frase
  dell'AIP resta nel `before` della correzione. L'alternativa era un tratto di confine coi punti dentro, che tocca lo
  schema del modello. Proposta dell'agente, **confermata dal committente** (20 ottobre notte: «Rette va bene, confermo»).
- Trovato a schermo e corretto: dalla R6 la pagina Confronti mostrava la scheda della voce dell'AIP **due volte** (la
  Scheda la ha già in testa); ora una.
- Test: Lab 1 330 (+3: le righe e l'area tracciata; il gesto che scrive una correzione per area, salta quella che non si
  traccia e si ritira; il tasto nella pagina). Sul banco (copia del sector, AIP di prova 2610): **30 aree, tutte
  tracciabili**; la FIR di Milano «132 punti dal sector, 737 NM (retta di 286 NM)», come la misura della F6. «Porta
  nell'AIP espanso» sulla FIR: la correzione 2026-10-20-1 nel clone di prova, la riga «uguale», sulla mappa la FIR lungo
  l'arco delle Alpi, «Salva» del sector senza modifiche; «Ritira la correzione»: di nuovo «solo nel sector 30», il modello
  del ciclo identico a prima. Il clone di prova è stato rimesso com'era (tolta la cartella `correzioni/` nata dalla prova).

**La pagina Testo** (D12, D13, D21; il committente, 21 ottobre: «Vai con la pagina Testo»). 🟡 Proposta dell'agente per
dividerla: **T1** la pagina e l'editor (albero dei file, CodeMirror dentro l'app, «Applica» come modifica in sospeso, i
metadati bloccati); **T2** quel che aiuta a scrivere (i problemi del validatore sulla riga, i nomi suggeriti dal catalogo,
Ctrl+clic alla definizione, la domanda prima di cancellare un record coi metadati); **T3** i legami (dalla Scheda «apri
nel Testo», dal Testo alla mappa, dall'anteprima del Confronti al file).

**T1 fatta** (21 ottobre).
- **CodeMirror 6 dentro l'app**: un file solo, `wwwroot/vendor/codemirror/codemirror.js` (370 kB, con `LICENSE`: MIT),
  come Leaflet; la ricetta per rifarlo in `tools/codemirror/` (versioni esatte, le ultime uscite da almeno due settimane,
  `package-lock.json`, `LEGGIMI.md`). Contiene già i pezzi della T2 (lint, autocompletamento).
- **La pagina**: «Testo» nello spazio Sector, accanto a Mappa e Consegna (D21); pagina intera come Consegna (la mappa si
  nasconde, non si smonta). A sinistra l'albero dei file del sector per cartella, con la ricerca e il segno ● sui file
  con modifiche in sospeso; a destra l'editor (`sectorlab-testo.js`): numeri di riga, ricerca (Ctrl+F), annulla
  dell'editor, colori per genere di campo (il nome, coordinate e numeri, separatori, commenti, metadati, sezioni) uguali
  per tutti i file a punto e virgola.
- **Il testo è quello di adesso**, modifiche dei gesti comprese (`RigheDiAdesso`). Un file che il motore non interpreta
  si legge dal disco e si guarda soltanto.
- **«Applica»** (🟡 proposta dell'agente: un tasto, non a ogni tasto premuto, perché ogni applicazione è una voce della
  storia): `RigheCambiate` confronta il testo scritto con quello del file riga per riga (la sequenza comune più lunga, tolte
  le uguali in testa e in coda; oltre 4 milioni di confronti il pezzo in mezzo va in blocco) e cambia solo le righe
  toccate con `CambiaRighe`: una modifica in sospeso come le altre (diff, Annulla, problemi nuovi, «Salva»), «APT.fix
  scritto nel Testo» nella storia. «Scarta» torna al file com'è. Un gesto o un Annulla che cambiano il file aperto: se non
  c'è niente di scritto l'editor lo riprende da solo; se c'è, lo dice e «Applica» si spegne finché non si scarta.
- **I metadati (D13)**: le righe `//@` sono in grigio col lucchetto; bloccate, l'editor scarta ogni cambio che le tocca
  (le cambia, le toglie, ne aggiunge) e lo dice, e il server rifiuta un testo coi tag diversi da prima. Una riga nuova
  fra due blocchi va bene (i tag restano quelli). Con «Sblocca i metadati» si cambiano; il file riletto deve avere i tag
  validi (`TagRotti`), se no il testo si rifiuta con la riga.
- Test: Lab 1 340 (+10: il confronto delle righe, sei casi; Applica, i metadati bloccati e sbloccati, il file in sola
  lettura, la pagina con l'editor finto). Sul banco (copia del sector): `itawlow.lairway` 3 017 righe coi colori;
  `APT.fix`: cambiare un tag bloccato non passa («Le righe //@ … bloccate»); un commento fra `//@END "BC404"` e
  `//@"BC406"` → «Salva (1)», ● sul file; Annulla della barra → l'editor riprende il file da solo; sbloccati, la nota di
  un tag cambiata passa, un tag con le virgolette aperte si rifiuta («riga 10»). Tutto in memoria: la copia non è cambiata.

**T2 fatta** (21 ottobre, «Vai con la T2»): quel che aiuta a scrivere.
- **I problemi del validatore sul margine** (il lint di CodeMirror), errore o avviso, col testo della regola; in testata
  «1 errore · 90 avvisi». Senza modifiche sul file sono quelli dell'albero (le righe del disco sono quelle di adesso); con
  modifiche, quelli che le modifiche porterebbero («nuovi»): quelli di prima avrebbero le righe spostate. Mentre si scrive
  CodeMirror sposta da sé quelli già mostrati; si rifanno dopo «Applica», «Scarta» o un gesto.
- **I nomi del catalogo** del master scelto mentre si scrive (due lettere, o Ctrl+Spazio), chiesti al server come nella
  scheda (`Suggerimenti`: prima chi comincia così, poi chi lo contiene), col genere e il file accanto.
- **Ctrl+clic su un nome**: dove è dichiarato (`DefinizioneDi`: il file del catalogo e la riga del suo record nel file
  com'è adesso); nello stesso file ci si va, in un altro file lo si apre lì. Un nome che il catalogo non conosce si dice.
- **Con del testo non applicato non si cambia file** (né dall'albero né col Ctrl+clic): «prima Applica o Scarta».
- **Cancellare un record che ha i metadati chiede prima** (D13): se il testo non ha più il nome di un record dichiarato
  da un `//@"…"` (`MetadatiNelTesto`: il nome non è più un campo di nessuna riga di dati), «Applica» non scrive e chiede
  «BC404 ha i metadati…: Togli anche i metadati / Lascia stare». Togliendoli se ne vanno la dichiarazione e il suo blocco
  (`//@START`, `//@END "BC404"`), anche coi metadati bloccati; gli altri tag restano.
- Test: Lab 1 353 (+13: i nomi dichiarati in otto forme, i tolti, il testo senza i loro tag; cancellare chiede e toglie;
  la definizione e i problemi; la pagina coi nomi, il Ctrl+clic, la guardia e la domanda). Sul banco: `itfix.fix` «1
  errore · 90 avvisi» sul margine; Ctrl+clic su TOP in `itawlow.lairway` → `itvor.vor` riga 63; scrivendo «AMS» → AMSED,
  AMSOR (fix, `itfix.fix`); cambiare file con del testo scritto → la guardia; in `APT.fix` tolta la riga di BC404 → la
  domanda, poi il blocco tolto e «Salva (1)». In memoria: la copia non è cambiata.

**T3 fatta** (21 ottobre, «Vai con la T3»): i legami (D21).
- **Dalla Scheda** «✎ Apri nel Testo»: il file del record nel Testo, col cursore alla prima riga del record nel file com'è
  adesso (`RigaDelRecord`). Se il file è già aperto nel Testo, ci si va e basta.
- **Dal Testo** «Sulla mappa»: il record della riga del cursore (`MostraSullaMappa`: le righe dei record nel file com'è
  adesso) diventa la scelta, e si va alla mappa del sector. Una riga che non è di un record (commento, vuota, tag) si dice;
  col testo non applicato no, perché le righe del file non sono ancora quelle scritte.
- **Dal Confronti**: nel riquadro «metti in questo file» «✎ Apri nel Testo» sul file di destinazione (per scali e piste il
  primo spuntato), com'è adesso: per guardarlo prima di scrivere.
- **La bozza** (🟡 proposta dell'agente): lasciando la pagina Testo con del testo non applicato, il testo resta come bozza
  del suo file (una sola; ✎ nell'albero) e torna riaprendo quel file, con «Ripreso il testo…»; se nel frattempo il file è
  cambiato (un gesto), lo dice come prima e «Applica» resta spento finché non si scarta. Senza, uscire dal Testo per
  guardare la mappa faceva perdere quel che si era scritto. Cambiando sector il Testo si azzera (anche il file aperto: prima
  della T3 restava quello del sector di prima).
- Test: Lab 1 355 (+2: la riga del record, «Sulla mappa», l'apertura dalla Scheda; la pagina con «Sulla mappa» e la bozza
  che resta e torna). Sul banco: `itvor.vor` riga 63 → «Sulla mappa» → Mappa con TOP scelto (record 62); «Apri nel Testo»
  dalla Scheda → Testo col cursore alla riga 63; scritto un commento, andato su Mappa e tornato → «Ripreso il testo…»;
  dal Confronti (punti) «Apri nel Testo» → `itfix.fix`, e `itvor.vor` resta segnato ✎. In memoria: la copia non è cambiata.

La pagina Testo è finita (T1-T3).

**Dove siamo** (20 ottobre sera, aggiornato): R0-R3 fatte; poi l'ambiente di lavoro (primo avvio, repo presi dal Lab,
cartelle fisse, cartella e profilo di Aurora, simboli dal profilo), Lab 1 306. Il committente ha chiesto la **terza forma**
(§4-ter: tre spazi di lavoro, un motore di mappa, «metti in questo file»), D14-D22 **approvate**; il piano è in §5-ter
(R4-R9). **R4 fatta** (tre mappe, un motore), **R5 fatta** (due lati, un disegno), **R6 fatta** (lo spazio Confronti in
lettura) col ritocco «solo la sezione sulla mappa», **R7 fatta** (i gesti nel Confronti, «Lettura e consegna» nello spazio
AIP), **R8a fatta** (la misura dei file compatibili e «metti in questo file» per i punti), **R8b fatta** (le rotte nelle aerovie basse o
alte), **R8c fatta** (scali e piste anche nei file delle FIR), **R9 fatta** (i confini dal sector all'AIP espanso, come
correzioni). Il piano di §5-ter è finito. Poi la **pagina Testo**: T1 fatta (la pagina, l'editor, «Applica», i metadati
bloccati), T2 fatta (problemi sul margine, nomi, Ctrl+clic, la domanda prima di cancellare un record coi metadati),
T3 fatta (i legami con la Scheda, la mappa e il Confronti; la bozza): la pagina Testo è finita. Restano, al via del
committente: le due consegne, la Scheda per gruppi, i verbi. Dopo: le due
consegne, la Scheda per gruppi, i verbi. Il giro di prova del primo avvio (`SectorLab-prova\PrimoAvvio`) resta
da fare.
