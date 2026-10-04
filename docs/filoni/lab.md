# Filone Sector Lab — stato

> Scrive **solo** l'agente del Lab (cartella `vipi-lab`, ramo `lab/f3`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Storia fino al 23 settembre 2026:
> `docs/lavori-aperti.md` §A71, §A113, §A115, §A116.

## 4 ottobre 2026 — lotto «Subito» in corso (slice 0-12 fatte, 13 cominciata)

Carta approvata: [`2026-09-27-lotto-subito.md`](../feature/2026-09-27-lotto-subito.md) (20 slice in tre ondate). Fatte
la slice 0 (misure di partenza) e la **slice 1** intera: sintassi dei tag di §M nel motore (1a), tag sui file a una
riga per record (1b), tag dei punti `//@@` in SID e STAR (1c), tag sui file a blocchi — `.artcc`, `.mva`, aerovie coi
`//@@` per tratto, `.tfl`, `.hartcc`/`.lartcc`, `.geo`/`.pol` — coi blocchi che tengono più record (1d), rotte VFR
`.vrt` e prova su tutti i formati (1e). Slice 2 in corso: **2a** commenti in coda (avviso per file, gesto «sposta
sopra», il lettore MVA che ne faceva scrivere 74), **2b** controllo degli `.isc` (file trovati per nome, inclusi
due volte, sotto la sezione sbagliata, vuoti, orfani copia di un altro), **2c** coordinate scritte male con la
correzione proposta e «Correggi la riga» (trovati due punti letti in Asia e in Africa). Test: motore **616**, Lab
**366**. Slice 2 chiusa. Slice 3 (scheda tipizzata) in corso: **3a** descrizioni dei campi per i 24 tipi di record
(nome dell'AOD, significato, editor; 0 campi sconosciuti sul fork), **3b** gli editor (tipo fisso, sì/no, quota,
numero, piste dal `.rw`, scali e posizioni; corretto lo scrittore delle MVA di ACC che metteva la quota al posto del
gruppo; MVA di scalo in sola lettura fino alla slice 15), **3c** il punto coi suggerimenti del master (corretta la
fusione dei campi del motore, che cancellava l'RNAV delle SID quando la riga si allungava), **3d** i metadati del
catalogo di §M nella scheda (1 271 record su 1 271 sul fork), **3e** «+ Nuovo record» che chiede il tipo fisso (negli
`.artcc` etichetta o traccia). Slice 3 chiusa. **Slice 4** (colori, 28 settembre): **4a** i colori nel motore (le
quattro forme del manuale, `colors.def` in tutte, gli schemi `.clr` di Aurora con la notazione di Delphi misurata sulle
coppie dello stesso colore), **4b** la mappa coi colori dello schema scelto (`LIRR_RDR_V1.0.clr` di base, fondo
radar, settori dinamici solo bordo, nei `.geo` vince lo schema — committente —, nei `.tfl`/`.pol` `colors.def`;
corretto l'ordine dei riempimenti di terra, che si rovesciava), **4c** il selettore nella scheda (nomi di
`colors.def` del master, selettore, opacità con l'avviso su Smooth Drawing), **4d** i punti coi simboli del `.sym`
del sector (chiesta dal committente; abbinamento per nome, confermato a schermo salvo i VFR, che usano il rombo di Aurora). Slice 4 chiusa.
**Slice 5** (sequenze di punti e gesti sul record, 28 settembre): **5a** tracciato delle aerovie, vertici MVA e dei
confini come elenchi, «+ in fondo» e «inverti» (che rilegge il file e rifiuta dove i separatori `T;dummy` finirebbero
al posto sbagliato; corretto lo scrittore MVA delle zone di scalo senza riga L), **5b** spezza/unisci come gesto sul
testo riletto dal motore, nelle cinque scritture (riga vuota, `<br>`, `DUMMY`, `BREAK`), **5c** nascondi/mostra (i
record commentati restano nell'elenco, grigi: fra gli altri, dentro, in parte), **5d** la vista a linea dei `.geo`.
Slice 5 chiusa. **Slice 6** (gruppi e blocchi come Aurora, 28 settembre): **6a** sotto il file le voci della finestra di
selezione (confini coi poligoni, MVA con le zone, aerovie coi pezzi, gruppi dei `.geo`/`.pol` sotto un commento, aree
P/R/D), accese e spente sulla mappa anche un poligono solo; **6b** il nome dal commento cambiato dalla scheda e la
regola `NomeMancante` del validatore (564 «senza titolo» nei `.geo`); **6c** il gruppo col nome dal commento passa a
blocco `//@` al primo metadato. Slice 6 chiusa. **Slice 7** («chi lo usa» e rinomina, 28 settembre, divisa in sei
passi): **7a** nella scheda di un punto chi lo usa, per master (e le copie a un decimo di miglio sono lo stesso punto;
VOR e NDB omonimi mostrati tutti e due, decisione del committente); **7b** la rinomina di un punto in una voce sola con
un diff per file, che **chiede** per le righe comuni (decisione del committente), e il «togli» impedito a un punto
usato; **7c** le posizioni (trasferimenti dei `.frq`, teste dei `.tfl`); **7d** i versi di pista (SID, STAR, mappe,
tag; i PAR dei `.cpr` e i commenti dei disegni «da cambiare a mano»); **7e** chi usa un file e i colori di
`colors.def`, solo da vedere (la loro rinomina è per il futuro, decisione del committente); **7f** il nome delle voci
di confini, MVA, aerovie e aree. Sul fork tutto rinominato e annullato con 0 guasti. Slice 7 chiusa. **Slice 8** (famiglie di forme e
gemelli fra tipi diversi, 28-29 settembre): **8a** la stessa forma in più record, trovata dal Lab come anello (inizio e
verso qualsiasi) e mostrata nella scheda, uguali e simili; **8b** la famiglia dichiarata `form=` e l'avviso «copie di
forma diverse» solo per le dichiarate (i settori solo simili non riempiono il pannello); **8c** la forma portata sulle
copie dopo ogni gesto sui vertici, che tengono partenza, verso, chiusura e scrittura, e «Allinea quella» / «Prendi la
sua»; **8d** le linee dei `.geo` come copia e come sorgente (l'erba e il suo confine si seguono), il bordo e il
riempimento che mancano, l'avviso del confine senza erba; **8e** il gemello `.vfi` ↔ `VFR_NASCOSTI.fix` (la
posizione passa, «crea il gemello», la domanda dopo «togli»). Sul fork 0 guasti in ogni misura. Slice 8 chiusa.
**Slice 9** (procedure, 29 settembre): **9a** i controlli (procedura ripetuta, tipo fuori posto, voce di un altro scalo,
pista che lo scalo non ha); **9b** la scheda scrive RNAV e transizione, nel `MAPS` il tipo è «il tasto che accende la
mappa», le voci per pista e tipo come la finestra delle procedure di Aurora e il «+» che mette la procedura nuova nel
gruppo della sua pista; **9c** le chiavi del genere di voce, le scelte chiuse, minimi e pendenza, il fix proposto dal
nome, «a tutta la voce»; **9d** i vincoli dei punti (`//@@`), al passaggio del mouse sulla mappa e mai in Aurora;
**9e** i legami STAR → attesa → avvicinamento → mancato avvicinamento e l'avviso della STAR che finisce dove nessun
avvicinamento passa. Tre difetti del motore trovati scrivendo, tutti di prima (testa `.str` doppia, commento in coda
letto come RNAV, record nuovo che rubava il commento della voce dopo). Slice 9 chiusa; poi, decisione del committente,
**la consegna agli AOD**: lo zip (eseguibile `f27d4ed4` e i `SectorFiles` del fork al commit `8cf32c6`, con le
istruzioni) è stato preparato e provato il 29 settembre, e lo manda il committente. **Slice 10** (NAVAIDS e attese,
29 settembre: 10a-10c) e **slice 11** (OTHER e PREFS, 29 settembre: 11a posizioni, 11b scali e piste, 11c CPDLC, 11d
profili `.cpr` e PAR) chiuse. **Slice 12** (terra, 4 ottobre, dopo l'allineamento a `main` 1.56.0): **12a** i controlli
(ICAO diverso dal file, stand ripetuto o lontano, etichetta lontana dalla taxiway, tipo vuoto o sconosciuto, `.pol`
senza `.geo`); **12b** tipo e slot degli stand nel motore e nella scheda, i metadati di stand e taxiway coi valori
chiusi, tipo e slot proposti dai metadati; **12c** i generi dentro gli strati della mappa (assi, bordi, edifici,
marcature; riempimenti, etichette, stand) e l'ordine di disegno nella scheda del riempimento; **12d** l'ordine di
disegno deciso (quello del fork: la carta «file per file» I3 è corretta), le marcature di una pista che il `.rw` non
ha, la **vista per scalo** nella linguetta «Scali» con le marcature per pista. Slice 12 chiusa, salvo i punti di
startup (R4: dopo la prova in Aurora). **Slice 13** (settori e spazi, dal 4 ottobre): misura sul fork e **13a** — la
testa dei `.tfl` con l'opacità e il filtro facoltativi (le 52 teste a quattro campi di `GCI.tfl` si leggevano come
vertici: un poligono solo al posto di 53), le posizioni separate anche dai due punti in «chi lo usa» e nella rinomina.
Test: motore **788**, Lab **799**; albero 235 errori, 978 avvisi. Il dettaglio di ogni passo sta in §6
«Traccia» della carta. I conteggi dei test si scrivono a mano finché il
cancello di `main` rifiuta `tools/conta-test.sh`.

## 27 settembre 2026 — revisione del giro dei file

Prima del lotto «Subito» il committente ha chiesto di rileggere tutto il giro e le decisioni: cosa è stato valutato
per un file e non per il gemello, e quali metadati ci sono. Trovato: gli esempi di tag della carta erano in quattro
forme e il motore ne legge una (alcune avrebbero spezzato le STAR). Deciso: **una sintassi e un catalogo** (carta
«file per file» §M), vIPI legge i metadati e non ne propone (supera carta madre §8.2), i generatori portano i loro
parametri (§M-G), metadati nuovi per settori, pista (TORA dagli intermedi, circuito, limiti d'uso), IAP, SID, rotte
VFR, taxiway. Proposte ancora da confermare e la lista delle correzioni dei dati: §R. Il «nomi con spazi nelle
composte» qui sotto è chiuso da §M regola 5. Nessun codice toccato.

## Dove siamo — 26 settembre 2026 (il committente chiude la chat e riorganizza il lavoro)

**Nessun codice in sospeso.** `lab/f3` pulito e spinto; ultimo codice `e06d6f20` (CI verde), dopo solo documenti.
Test: motore **479**, Lab **358**. Eseguibile di prova ripubblicato il 24 settembre alle 12:54 (= `e06d6f20`).
Il 26 settembre, all'apertura della chat «vIPI Lab», `lab/f3` si è allineato a `main` (1.46.5 compresa: `d670d606`).

1. **Prove a mano di F3 e F3-bis: FINITE, 52 su 52 ✅** (`SectorLab-prova\PROVE.md`). L'ultima correzione (prove 40-41):
   un fix nuovo va nella sezione del suo NOME (`OrdineAlfabetico.NellaSezioneGiusta`, prefisso più lungo), non in quella
   del record da cui si parte; anche «+ Nuovo record» `BD100` era rotto e nessun test lo guardava.
2. **Giro del sector «file per file»: FINITO** (24-26 settembre). Carta
   [`2026-09-24-file-per-file.md`](../feature/2026-09-24-file-per-file.md): **§1-§22** = ogni cartella e ogni tipo di file
   della radice `IT`, ciascuno con formato (dal manuale IVAO), misure sul fork, decisioni del committente, fase di ogni
   esigenza (Subito / F4 / F6 / F7 / F8 / F9 / Da pensare); **§C** = i meccanismi comuni (chi li chiede).
   Metodo: struttura dal manuale **prima** (wiki `SectorFile_Definition`, leggibile col browser del pannello; ricerca
   pagine con GraphQL `/graphql` `pages.search`), poi misure, poi le domande; **non** passare da soli al file dopo.
3. **Prossimo passo — da proporre al committente, NON ancora cominciato**: il **lotto «Subito»** = tutte le voci
   «Subito» delle §1-§22 raggruppate per meccanismo comune (§C), in slice, con un ordine; come carta a parte collegata
   alla «file per file», da far leggere prima di cominciare. Il committente ha detto: tutte le cartelle prima, poi si
   accorpa.
4. **Prove in Aurora da fare presto, in rami di prova** (le fa il committente): ordine dei simboli di `symbols.sym`
   (T3: le 2 righe senza `//` spostano la numerazione?); startup col tasto HOLD nel `MAPS` (R4); etichette `L` fra i
   tracciati `T` delle aerovie (B9); etichette ACC per riferimento `L;ABDAB;ABDAB;ABDAB;8;` (A8); `ENRVFI` sotto
   `[VFRFIX]` (F5); organizzazioni dei file di scalo A-E (I7, K2; il `.geo` NON è fra i file caricati da sé secondo il
   manuale, un esempio dice il contrario).
5. **Pulizie decise, in un ramo (F4)**: `limw.pol` (residuo del 2020), `test.artcc`, `limc_star`/`lirf_star` (AOD);
   `.fix` vuoti più avanti; `LIMM_WN4_CTR`/`LIMM_EN4_CTR` dai trasferimenti; SID ripetute (11) e campi spostati (28);
   ICAO sbagliati (`LINB`, `L3MC`, `L4MC`, stand LIBP in `libg.gts`); `update.ini` **tolto** dalle cose da generare.
6. **Regole nuove per tutto il Lab** (committente): 🔴 niente commenti `//` in coda a una riga (Aurora la legge in circa
   il doppio del tempo; 713 righe oggi) · 🔴 dati SOLO da fonte primaria (DB IVAO, PDF dell'AIP), **mai da vIPI** ·
   🔴 un `.cpr` in `PREFS` sovrascrive l'utente a ogni connessione (solo lo stretto necessario) · rami di prova, niente
   file parassiti · coordinate col punto.
7. **Da chiedere**: le `LL NW/NE/S1/S2` di `lied.str` (rotte militari a bassa quota?) a chi segue LIED; composizione
   delle configurazioni di Milano 2.1/2.2/3.
8. **La consegna** (slice 11 di F3) resta decisa dal committente.

Script di misura (fuori repo, riutilizzabili): scratchpad della sessione `079dd998-…\scratchpad\acc\` (`geo.py` = base
con `dms`/`nm`; uno script per cartella: `aw.py`, `tfl*.py`, `mva*.py`, `geo*.py`, `pol.py`, `airspace.py`,
`navaids.py`, `other.py`, `cpr.py`, `sid*.py`, `star.py`, `maps.py`, `terra.py`, `vrt.py`, `simboli*.py`…) e
`…\scratchpad\strumenti\` (ATIS Creator e SYMBOL Creator IVAO, **non eseguiti**). 🔴 Script Python con Write, non
heredoc (le `\` si perdono). PDF AIP: `vIPI Ivao Italy\RealDOCS\` (fuori da git), `pdftotext -layout` li legge.

🟡 Nel clone di prova `it-aurora-sector-test` git vede cancellato `IT\changelog.md` (non l'ha fatto il Lab): chiedere
al committente prima della prossima prova.

## Prima — 24 settembre 2026, 11:30

**In corso: le prove a mano del committente** (slice 6 di F3-bis, più le sue richieste man mano). `lab/f3` è pulito,
spinto, **CI verde** su `27a70daf`. Test: motore **479** (net8 e net10), Lab **356**.

- **Eseguibile di prova**: `D:\Programmazione\IVAO_Test\SectorLab-prova\VipiSectorLab.exe`, ripubblicato il 24 settembre
  alle 11:30 (tutto `lab/f3`). Il piano è `SectorLab-prova\PROVE.md`, **51 prove**, con in testa la tabella dello stato:
  ✅ 1-5, 8-12 · 🔧 corrette da rifare (35, 37-38, 40-41, 43, 45, 47, 48, 51) · ⏳ 13-51. Ripubblicare dopo ogni correzione:
  `dotnet publish src/Vipi.SectorLab -c Release -r win-x64 --self-contained -o D:\Programmazione\IVAO_Test\SectorLab-prova`,
  poi `VipiSectorLab.exe --autoprova --cartella <clone>` (esito 0). 🔴 Prima: `Get-Process VipiSectorLab` — il
  committente tiene spesso l'app aperta, e le DLL in uso non si sovrascrivono: si committa e si ripubblica quando la chiude.
- **Sector di prova**: il clone `D:\Programmazione\IVAO_Test\it-aurora-sector-test`, con le prove SALVATE del committente
  (`APT.fix`: `BC;518`, `BC420`; `lirn.str`: ATZ a N041; `twrs.tfl`: LIRN_TWR incollato). Servono alle prove 37-38, 43, 47;
  poi lui fa `git checkout -- .`.
- **Banco per vedere la UI** (fuori repo): scratchpad della sessione `ba2d7208-…\scratchpad\vetrina` — progetto
  `Microsoft.NET.Sdk.Web` con `RequiresAspNetWebAssets` e `OutputType Exe` (senza, `blazor.web.js` va a 404), che apre il
  clone e stampa l'indirizzo col segreto; si apre nel browser del pannello. Si ferma per riga di comando
  (`vetrina.dll`), 🔴 mai `taskkill /IM dotnet.exe` (ferma anche i processi degli altri).
- **Da discutere col committente**: l'ordine dei record e cosa vuol dire «aggiungere un elemento», **file per file** (oggi
  in ordine alfabetico solo i punti col nome: fix, VOR, NDB, VFR); l'impostazione «salva il tavolo di lavoro all'uscita»
  (oggi le larghezze delle colonne si ricordano sempre); i nomi di procedura con spazi nelle composte.
- **La consegna** (slice 11 di F3: workflow `sectorlab-v*`, zip + SHA-256, scheda `/services`) **la decide il
  committente**. Prima vuole qualcosa da presentare agli AOD (è nel team AOD: copre ~80% dei bisogni, il resto dopo).

### F3 — l'app

Carta [`2026-09-22-f3-l-app.md`](../feature/2026-09-22-f3-l-app.md): slice 0-10 fatte, resta la 11 (consegna, vedi sopra).
La scheda `/services` entra nel sito: quella parte della consegna la fa l'integratore.

### F3-bis — copie gemelle e mappe composte

Carta [`2026-09-23-f3-bis-copie-e-mappe-composte.md`](../feature/2026-09-23-f3-bis-copie-e-mappe-composte.md): il §8
«Traccia» ha numeri e scoperte di ogni slice.

| Slice | Cosa | Stato |
|---|---|---|
| 0 | Misure sul fork | ✅ gemelli 3/13/1; 58 aggregati, 16 già disallineati; D7 chiusa (l'8º campo è `RNAV`) |
| 1 | Gemelli nel motore, regola `CopieDiverse` | ✅ 17 chiavi sul fork |
| 2 | Una modifica va anche sulle copie gemelle | ✅ una voce, più diff; «allinea anche questo» |
| 3 | Tag fra virgolette, chiave `composta`, `<br>` nel modello | ✅ 545/545 `<br>`, 2782/2782 tag |
| 4 | Rigenerazione delle mappe composte, due regole del validatore | ✅ `lime.str`: STAR spostata → −2 +2 |
| 5 | «Composta da» nella scheda | ✅ «Composta da quello che disegna oggi»: 20/55 aggregati col solo tag |
| 6 | Prove a mano del committente, Aurora compresa | 🟡 in corso (vedi sopra) |

Decisioni del committente: D1-D10 in carta §5. Due **riviste** dopo le misure: **D4** (le virgolette restano, anche
se i nomi con spazi si leggevano già) e **D8** (per mappa: troncate di norma, `intere=si` le disegna intere).

### Correzioni dalle prove del committente (23 settembre)

- `ad26a3b4` i record di un file si vedono **sotto** il file (erano in fondo alla colonna, fuori schermo).
- `bea96c8c` togliere la scelta di un file **non chiude** più la sua cartella.
- STAR «(ALL)» spezzate al `<br>` in `Geometria` (si ricollegavano l'una all'altra: `lirn.str` STAR 06(ALL)).
- Zoom della mappa 16 → **20**, coordinate per la mappa a 6 decimali (~11 cm).
- **Due schermi**: tasto «Pannelli in un'altra finestra» → `/pannelli` (Sfoglia/Problemi, scheda, modifiche) in una
  seconda finestra del guscio (`FinestraDeiPannelli`), sull'altro schermo; la principale tiene mappa e strati. 🟡 Il
  guscio a due finestre l'ha provato solo bUnit: la prova vera sono la 25-27 del committente.

### Richieste della sera del 23 settembre (a–f) e prova 5 — PROVE.md 28-36

- **Le due finestre insieme** (`FinestreInsieme`): cliccata l'una, l'altra risale subito dietro (`SetWindowPos` senza
  attivare). Non con `Owner`: la posseduta starebbe sempre sopra, e con uno schermo i pannelli coprirebbero la mappa.
- **Colonne ridimensionabili**: divisori `data-divide` (sectorlab.js, un ascoltatore sul documento), larghezza in una
  variabile `--lab-l-<nome>` su `<html>`, ricordata nel localStorage della WebView2 (doppio clic = di base). 🟡 Il
  committente vuole, con le future impostazioni, l'interruttore «salva il tavolo di lavoro all'uscita»: oggi si
  ricorda sempre.
- **Tema scuro + brand IVAO**: il Lab NON seguiva il brand (Segoe UI, `#0b5cad`). Ora token del sito (`vipi-theme.css`:
  atmos/ocean/fuselage/semantic), Poppins/Nunito Sans/IBM Plex Mono serviti da `wwwroot/fonts/`, barra blu IVAO.
  Automatico/chiaro/scuro (`sectorlab-tema.js` nel `<head>`); i colori degli strati sono `--lab-strato-<id>` e la mappa
  li rilegge al cambio di tema.
- **Annulla/ripeti** (`SessioneDelLab.NellaStoria`/`Annulla`/`Ripeti`, tasti ↶ ↷ e Ctrl+Z/Ctrl+Y fuori dai campi):
  annullare = tutto com'era all'apertura (`AnnullaTutto` + `Modifiche = new()`) e si rigiocano i gesti tranne l'ultimo.
  Dopo un salvataggio o una rilettura la storia riparte. 🔴 Preso strada facendo: la mappa aveva UNA versione della
  geometria e ridisegnava solo l'ultimo strato toccato (un gesto sulle copie gemelle ne tocca più d'uno; anche il cambio
  di master non ridisegnava niente) → `VersioneDelloStrato`.
- **Anteprima dell'incolla** (`Core/Modifiche/TestoDaIncollare`, la STESSA lettura dell'incolla): nella scheda
  (`AnteprimaDelDisegno`, SVG: oggi grigio, nuova verde tratteggiata, centri) e sulla mappa; segue testo e densità a
  ogni scatto. Il «°» della densità non va più a capo.
- Test Lab 306 → **327**. Eseguibile ripubblicato alle 20:55.

### Prove del 24 settembre mattina (a, b, 6, 7, 10) — PROVE.md 37-44

Esiti: 6 ok ma ordine, 7 ✗, 8 ✅, 9 ✅, 10 ✗.

- **10 ✗ → corretto**: le zone degli `.str` erano **aree** e Leaflet le chiudeva; Aurora le disegna come **linee**
  (l'ATZ di LIRN col primo punto sbagliato restava aperta in Aurora e chiusa da noi). Ora `Geometria`: linea.
- **7 ✗ + (b)**: la riga salvata `BC;518;…` riletta non è più un record (riga illeggibile) → dal pannello Problemi si
  vedeva e non si correggeva. Ora si **scrive una riga a mano** (`RigheAMano`, clic → Invio → conferma):
  `ModificheInSospeso.CambiaRiga` prende le righe di ADESSO, cambia quella, rilegge il file col motore
  (`IFileConRecord.LeggiLeRighe`) e lo mette come nuova STRUTTURA (`ModificaDelTesto`, si annulla come
  `ModificaDiStruttura`: file dell'apertura). Quel che pendeva sul file entra nel testo (i campi si rimettono sui record
  prima, niente si annulla negli altri file). 🔴 Le righe `//@` hanno il lucchetto, e non se ne scrivono di nuove.
  Anche: Ctrl+Z in un campo già confermato ora va al Lab (dopo Invio il cursore resta lì e il gesto «non tornava»).
- **6 → ordine alfabetico**: `OrdineAlfabetico` (per ora fix, VOR, NDB, punti VFR) — il nuovo chiede il NOME e va al suo
  posto nella SEZIONE (record fra due commenti: `//LIBC`, `//LIBD` di `APT.fix`); primo della sezione = sotto
  l'intestazione (motore: `RecordNuovo.AggiungiPrimaDi`, i `//@` restano al vicino). **+ Nuovo record** anche dal file.
  L'ordine degli altri tipi: da decidere col committente file per file.
- **(a) la vista**: «◎ Solo questo sulla mappa» (record) e «◎ Solo questo file» (Sfoglia), elenco «In vista» sotto gli
  strati, «mostra tutto». JS `sectorlab.mappa.vista`: toglie i gruppi degli strati e ne fa uno con le sole forme scelte
  (niente fetch in più); le coste restano. 🟡 Le chiavi sono per indice: aggiungere un record prima di uno in vista lo
  sposta (da rivedere se dà fastidio).
- Test Lab 327 → **346**, motore 471 → **474**. Eseguibile ripubblicato 24 settembre 09:23.

- 24 settembre, dopo: 🔴 «clic sulla riga 30, si apre la 29». I problemi dell'ALBERO numerano le righe del DISCO; con un
  record aggiunto sopra (prova 40) nel file di adesso la riga è una più in giù. Ora `VaiAlProblema` traduce col diff
  (`Diff.Allinea`, `ModificheInSospeso.RigheDellApertura`, `SessioneDelLab.RigaDiAdesso`) e la vista del problema
  mostra le righe di ADESSO. Controllo su tutto il fork: numeri della scheda = righe del file (`NumeriDiRigaTests`,
  `SECTORLAB_ALBERO_VERO=<clone>` per l'albero vero). Lab 348.

- 24 settembre, «chiudi la forma»: l'incolla ora ha la casella (parte da com'è la forma di oggi, `ElencoChiuso`); la
  chiusura sta in `TestoDaIncollare.Leggi(…, chiudi)`, una sola lettura per anteprima e incolla. Lab 351. Ripubblicato
  10:14 (PROVE.md 45-46).

- 24 settembre, regola **`FormaQuasiChiusa`** (avviso, codice comune `Validazione/Validatore.cs`): primo e ultimo punto di
  settori `.tfl`, zone `.str`, MVA e poligoni che scritti differiscono per UNA cifra (sopra i 100 m). Misurata sul fork:
  «a meno di mezzo miglio» dava 367 avvisi (settori con l'ultimo lato corto), «sotto i 100 m» 1 598 (arrotondamenti);
  la sola cifra dà **1**: `lirn.str:63` LIRN ATZ, il refuso della prova 10. Motore 479. PROVE.md 47.

- 24 settembre, osservazioni a–c (prove 10-12 ✅): 🔴 **(c)** SID/STAR/punti non sparivano spegnendo la casella: scegliere
  un record accende il suo strato e fa partire più `Cambiata` di fila → due `Sincronizza` insieme chiedevano lo stesso
  strato due volte (lo segnavano disegnato solo DOPO la fetch) → due gruppi, la casella spegneva solo il secondo. Ora
  `Mappa.InFila` (una sincronizzazione alla volta) e il JS non fa mai due gruppi (`inArrivo`, `giro`, `voluti`). Il test
  bUnit con una fetch che non finisce è rosso sul codice di prima. **(a)** archi a 5° di base (`GradiPerPuntoDiBase`), la
  stima dal file resta come indicazione. **(b)** `Sezione.razor`: titoli che chiudono/aprono, stato per CHIAVE nel Lab
  (`SezioneAperta`/`ApriOChiudi`). Lab 355.

- 24 settembre: 🔴 «Solo questo sulla mappa» svuotava la mappa. `InvokeVoidAsync("…vista", string[])`: lo string[] per
  covarianza diventa l'object[] degli ARGOMENTI → una chiave per argomento, il JS riceveva una stringa. Ora `(object)`;
  test bUnit sulla forma della chiamata (rosso senza). I test di prima guardavano la sessione, non la chiamata. Lab 356.

### Aperto, da chiedere o dire al committente

- ❓ **Nomi di procedura con spazi** (63 su 1169: `RNP10 UPETI` di `lica.str`, le rotte `AAR …` di `lizz.str`): oggi
  non possono stare nell'elenco di una mappa composta (casella spenta). Allargare la grammatica (nomi fra virgolette
  anche nell'elenco)? Fra i 58 aggregati le usa solo `lica` `RNP10`.
- 🟡 **Errore nei dati del fork**: `limf.sid:28` `LIMF18;TOP1B LAG2L; ; ;0;LAGEN;` (manca il `;` fra `LIMF` e `18`,
  dal commit «LIMF: Revisione SIDs»). Il validatore oggi non lo dice: ora è la P3 della carta «file per file» (§16).
- Tutti gli altri aperti del giro dei file (26 settembre) stanno nella carta «file per file», voce per voce con lo
  stato (🟡 = da provare o da confermare).

### Codice comune toccato (per l'integratore, alla fusione)

`Vipi.Sectorfile`: `Validazione/CopieGemelle.cs`, `Regola.CopieDiverse`, `Regola.CompostaConProceduraAssente`,
`Regola.CompostaNonAllineata`; `IO/Metadati.cs` (virgolette, `composta`, `intere`, `Togli`, `NomeElencabile`);
`IO/MappeComposte.cs`; `StrRecord`/`StrParser`/`StrSaver` (`IniziaUnTratto`); `IO/RecordNuovo.AggiungiPrimaDi` (24 set); `Regola.FormaQuasiChiusa` (24 set). Lotto «Subito» slice 8: `Regola.FormeDiverse` e `Regola.ConfineSenzaErba` (le calcola il Lab). Slice 9: `Validazione/ControlloDelleProcedure.cs` (4 regole), `Validazione/LegamiDelleProcedure.cs` e `Regola.StarSenzaAvvicinamento`; `SidProcedure.IsRnav`, `StrRecord.TipoNonScritto`, `StrParser.Rnav`, `StrSaver` fino all'8° campo; `FusioneDelRecord` (il commento in coda resta in coda); `RecordNuovo.Aggiungi` (la coda del vicino passa sotto il nuovo); `Metadati.RigheDeiPunti`. Slice 10: `Fix.NomeDellAttesa`, `Vor.CanaleTacan`/`NomeDellAttesa`, `Ndb.Visibilita`/`ExtraField6`/`ExtraField7`/`NomeDellAttesa`, lettori e scrittori dei NAVAIDS, `IO/Savers/CampiFacoltativi.cs`; `Validazione/ControlloDelleAttese.cs`, `CampiDelNavaid`, sette regole (`AttesaNonDefinita`, `AttesaMaiCitata`, `AttesaFuoriPosto`, `AtteseNonCaricate`, `NomeInPiuCataloghi`, `CampoMancante`, `ValoreFuoriElenco`), `Validatore.Metri` internal; `Attesa.Fix`/`Rotta`/`Verso`/`Quota` scrivibili. Slice 11: `AtcPosition.Loa`/`Inclusi`/`Esclusi`, `FrqParser`/`FrqSaver` (7° campo = `.loa`), `Runway.RottaVeraDalleSoglie`, `CorrezioneDelleCoordinate` per i `.rw`, lettori e scrittori nuovi `.cpdlc`/`.cpdlcnames`/`.cpr` (`MessaggioCpdlc`, `NomeDelGruppoCpdlc`, `ImpostazioneDelProfilo`), `Validazione/ControlloDellePosizioni.cs`, `ControlloDelCpdlc.cs`, `ControlloDeiProfili.cs`, quattordici regole nuove, `CopieGemelle` (non confronta i campi derivati). Slice 12a: `Validazione/ControlloDellaTerra.cs` e sei regole (`ScaloDiversoDalFile`, `LontanoDalloScalo`, `StandRipetuto`, `EtichettaLontanaDallaTaxiway`, `TipoSconosciuto`, `RiempimentoSenzaDisegno`). Slice 12b: `Stand.Type`/`Stand.Slot`, `GtsParser`/`GtsSaver` (5° e 6° campo), `Models/Airport/SlotDelloStand.cs`, `EsitoDelFile.Chiavi`, `Regola.StandPiuGrandeDellaTaxiway`. Slice 12d: `Shared/OrdineDeiRiempimenti.cs`, `Regola.OrdineDiDisegno`, `Regola.MarcaturaDiUnaPistaAssente`, `ControlloDellaTerra.PisteCitate`. Slice 13a: `TflSector.SenzaOpacita`/`Filtro`/`Posizioni()`/`Statico`, `TflParser` (testa da quattro campi), `TflSaver` (opacità e filtro facoltativi). `tools/Vipi.SectorfileProva` (sezione
5b, `composta` sulle MAPS). Il sito non usa niente di questo; la build della soluzione è verde.

### Dove sta la storia di prima

Fino al 26 settembre la storia del filone stava anche nella memoria dell'agente; ora sta solo qui e nelle carte:
F2 → carta [`2026-09-22-f2-motore-del-sector.md`](../feature/2026-09-22-f2-motore-del-sector.md) e §A115; F3 slice
0-10 (una riga per slice, con lo sha) → `docs/lavori-aperti.md` §A116 e §8 «Traccia» della
[carta F3](../feature/2026-09-22-f3-l-app.md); F3-bis → §8 della sua carta e la tabella sopra; giro dei file → carta
«file per file».

### Lezioni del filone che le carte non dicono

- 🔴 Una **proposta** dell'agente non si scrive mai come «decisa dal committente» (successo per D8-D10 di F3-bis,
  corretto subito): le decisioni hanno la data e la parola del committente.
- 🔴 bUnit: un attributo `bool` vero diventa un attributo **vuoto** → nei `data-*` si usano stringhe (`"si"`/`null`).
- 🔴 CI su Ubuntu più lenta: le attese dei test bUnit a 15 s (`Attesa`), sennò rossi a tempo scaduto.
- 🔴 `Punto` ha una conversione implicita da `Coordinate`: in un ternario servono i due `(object)`, sennò cade ogni
  `.pol`/`.lairway` (l'ha presa la misura sull'albero, non i test).
- 🔴 Sorgenti con `\r\n` dentro le stringhe e percorsi Windows nei documenti: solo con gli strumenti di modifica, mai
  Python o sed passati da heredoc (un `\v` di `scratchpad\vetrina` era diventato un carattere di controllo qui sopra).

- 🔴 Un componente della scheda con soli parametri primitivi (file e record) **non si ridisegna** col padre: dopo
  «Annulla tutto» mostrava ancora la famiglia (8b). Si iscrive da sé a `Lab.Cambiata`, come la scheda.
- 🔴 La mappa cuce i segmenti dei `.geo` senza guardare commenti e righe vuote; la linea del file (5d) sì. Chi confronta
  forme lo sa: 34 linee su 5 599 sul fork non coincidono (8d).
- 🔴 Una scrittura che nessuna misura faceva (la testa di una procedura) nasconde difetti vecchi: prima di offrire un
  campo nuovo, misurarlo su **ogni** record del fork (9b: «una testa per procedura», 2 811 righe; prima della correzione
  `limc.sid` usciva con 16 righe in più).
- 🔴 Il lettore degli `.str` lascia nelle righe di una voce la riga vuota e il commento della voce DOPO: chi inserisce
  dopo una voce deve spostare quella coda (9b, `RecordNuovo.Aggiungi`).
- 🔴 Un campo della scheda che è il NOME di un punto citato non è un campo come gli altri: scritto lì, cambiava il nome
  senza riscrivere le citazioni e senza guardare i doppioni (trovato nella slice 10). Il nome di un punto citato passa
  da «Rinomina».
- 🔴 Riscrivere una linea può cambiarne il numero di segmenti: i record dopo di lei, nel suo file, slittano. Più
  scritture nello stesso file si fanno dall'ultima alla prima, e la scelta segue il suo record (8d, `liaa.geo`).

- 🔴 I numeri di una carta si rimisurano prima di scrivere la regola: nella slice 12 due non si ritrovavano (43
  poligoni con meno di 3 vertici: erano 2; 3 `.pol` senza `.geo`: nessuno) e l'ordine di disegno «deciso» lo seguivano
  16 file su 93 — il committente ha corretto la carta, non il sector.
- 🔴 Un albero di prova che usa i campioni del motore ha già gli scali veri (LIRF, LIBP): uno scalo inventato con le
  stesse coordinate perde contro quello vero in ogni controllo «il più vicino» (12d: i test della vista per scalo).
- 🔴 Il lettore dei `.rw` conta come piste solo le righe sotto `//PISTE`: un `.rw` di prova senza quell'intestazione
  non ha piste, e il controllo che le usa tace (12d).
- 🔴 Il banco a schermo tiene bloccate le DLL del Lab: si ferma (`Stop-Process -Name banco`) prima di ricompilare.

### Dove lavorare

- Conteggi del filone: `tests/conteggi/Vipi.SectorLab.Tests.txt` e, se si tocca il motore, `Vipi.Sectorfile.Tests.txt`
  (`bash tools/conta-test.sh <log> --scrivi <Assieme>`, nello stesso commit dei test).
- Prova sull'albero intero: `dotnet run --project tools/Vipi.SectorfileProva -c Release -- <…\SectorFiles\Include\IT>`.
