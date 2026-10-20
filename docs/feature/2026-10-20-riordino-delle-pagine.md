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
due). Restano libere la vetrina e la cartella di Aurora. Test: Lab 1 304 (+1). Sul banco con un ambiente: i due campi
in sola lettura con le cartelle dell'ambiente, i due «Apri la cartella», nessun «Sfoglia…» (non premuto: aprirebbe
Esplora risorse sullo schermo del committente).

**Dove siamo** (20 ottobre sera): R0-R3 fatte, `lab/f3` @ `662766b6`, Lab 1 274, eseguibile di prova rifatto. **Prossima:
R4** (i confronti dentro l'Indice e i legami record ↔ voce), dopo un giro del committente sull'Indice. Poi R5 (le due
consegne, con ramo ed età del repo dell'AIP e la rilettura dello strato), R6 (Testo), R7 (Scheda), R8 (verbi).
