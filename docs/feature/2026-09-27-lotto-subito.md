# Lotto «Subito» — le voci «Subito» del giro dei file, in slice e in ordine (dal 27 settembre 2026)

> Carta dei bisogni: [`2026-09-24-file-per-file.md`](2026-09-24-file-per-file.md) (§1-§22 le voci, §M i metadati,
> §M-G i generatori, §R la revisione, §C i meccanismi comuni). Carta madre:
> [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md). Carte dell'app:
> [F3](2026-09-22-f3-l-app.md), [F3-bis](2026-09-23-f3-bis-copie-e-mappe-composte.md). Metodo:
> [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Stato — 27 settembre 2026

**Approvata** (§5). Fatte la slice 0, la slice 1 (1a-1e), la slice 2 (2a-2c), la slice 3 (3a-3e), la slice 4 (4a-4d), la slice 5 (5a-5d), la slice 6 (6a-6c), la slice 7 (7a-7f) la slice 8 (8a-8e), la slice 9 (9a-9e), la slice 10 (10a-10c) e la slice 11 (11a-11d, §6 «Traccia»); e la slice 12 (12a-12d, 4 ottobre; restano i punti di startup, R4, dopo la prova in Aurora). Dopo la 9, decisione del committente (29 settembre): **consegna
agli AOD** per una prima prova, uno zip con l'eseguibile e il sector. Tutte le voci
citate hanno la loro decisione nella carta «file per file»: qui c'è solo **come** e **in che ordine** si fanno.

## §1 — Cosa c'è già (F3, F3-bis) e cosa cambia

Oggi il Lab apre il sector, lo mostra sulla mappa per strati, e ogni record ha una **scheda generica**: i campi si
leggono **per riflessione** sul modello del motore (`Ispezione/SchedaDelRecord.cs`), si modificano come testo, e il
salvataggio passa dal motore (22 lettori/scrittori, round-trip byte per byte sull'albero intero). Ci sono già: incolla
con anteprima e archi, «+ Nuovo record» in ordine alfabetico per fix/VOR/NDB/VFR, copie gemelle fra i file delle FIR,
mappe composte, righe scritte a mano, annulla/ripeti, due finestre, pannello dei problemi (validatore con 22 regole: coordinate, righe illeggibili, `FileCitatoAssente`,
`FileMaiCitato`, `NomeNonRisolto`, `NomeDuplicato`/`NomeRipetuto`, `CopieDiverse`, composte, `FormaQuasiChiusa`…),
tag `//@` solo in `.sid`/`.str`.

Il lotto fa tre salti:

1. **Il motore capisce i dati in più** (i metadati di §M su tutti i file, i controlli nuovi).
2. **La scheda diventa tipizzata**: ogni campo sa cos'è (tipo fisso, punto, colore, posizione, quota, metadato) e
   si scrive con l'editor giusto. La riflessione resta come riserva per quello che non ha ancora una descrizione.
3. **Le viste parlano come Aurora e come l'AOD**: gruppi della finestra di selezione, blocchi, vista per scalo, chi
   usa cosa.

Tre file oggi non si leggono affatto e il lotto li aggiunge al motore: `.cpr` (PREFS, N1-N4), `.sym` (T1-T2),
`.cpdlc`/`.cpdlcnames` (M5); più `.datis` e `atisextra.fds` se il lettore degli `.atis` non li copre già (W1-W4).

## §2 — Come si lavora ogni slice

Come in F3 e F3-bis, sempre la stessa sequenza:

1. **Misura sul fork prima** (clone `it-aurora-sector-test`): quanti record, quanti avvisi darà ogni regola. Una
   regola che dà centinaia di avvisi falsi si rivede prima di scriverla (lezione di `FormaQuasiChiusa`: 367 / 1 598 / 1).
2. **Test rosso sul codice di prima**, poi il codice. Conteggi: `bash tools/conta-test.sh <log> --scrivi <Assieme>`
   nello stesso commit (Lab, e `Vipi.Sectorfile` se si tocca il motore).
3. **Prova sull'albero intero**: `tools/Vipi.SectorfileProva` (round-trip invariato, i numeri della misura).
4. **Prova a mano del committente**: eseguibile ripubblicato in `SectorLab-prova\` (prima `Get-Process
   VipiSectorLab`: se è aperto non si ripubblica), `--autoprova`, prove nuove in `PROVE.md`.
5. Commit e push di `lab/f3`, `gh run list` fino al verde. Il codice comune toccato (`Vipi.Sectorfile`) lo dice il
   messaggio del commit e il cartellino.

Regole che valgono per ogni slice (carta «file per file», testa): 🔴 il Lab **non scrive mai** un commento in coda ·
🔴 dati solo da fonte primaria, mai da vIPI · 🔴 i tag solo nella forma di §M · niente file parassiti · coordinate
col punto nelle righe che il Lab scrive.

## §3 — Le slice

Tre ondate. La **prima** costruisce i meccanismi comuni (§C): dopo, ogni cartella li usa e non li rifà. La
**seconda** passa le cartelle una alla volta. La **terza** sono gli editor speciali, che non servono ad altro.

### Ondata 1 — i meccanismi comuni

| # | Slice | Voci | Uscita misurata sul fork |
|---|---|---|---|
| 0 | **Misure di partenza**: per ogni regola nuova del lotto quanti avvisi dà oggi; per ogni tipo di record quanti ce ne sono e quanti campi | tutte | una tabella in §6 «Traccia»; le regole con troppi avvisi tornano al committente prima di scriverle |
| 1 | **Metadati nel motore, sintassi di §M**: lettura e scrittura su **ogni** file con un nome nelle righe di dati; `//@@` per i punti (non chiude il record); valori fra virgolette; prefisso `NN.` per verso di pista; catalogo delle chiavi **per tipo di file** (una chiave fuori dal suo file = avviso); `composta`/`intere` → `compose`/`whole` | §M, B5 (tag), E1 (blocco), D5 (chiave), G5, M9, M10, R2b, R6, D9, J7, Q8, F8, S6, P6, P11, Q2, Q2b, Q2d | round-trip invariato sull'albero intero (le righe `//@` oggi sono zero); ogni file: metti un tag, rileggi, toglilo = file uguale byte per byte. 🔴 codice comune: il catalogo è il contratto con vIPI |
| 2 | **Controlli comuni sul testo**: commento in coda (avviso) + gesto «sposta il commento sopra» (riga o file) + test che il Lab non ne scrive; controllo degli `.isc` — **estende** `FileCitatoAssente` (diventa avviso: Aurora trova il file per nome) e `FileMaiCitato` (+ i file caricati per nome di scalo, + «è una copia di…»), **nuovi** incluso due volte e incluso sotto la sezione sbagliata; coordinate non DMS o scritte male con la **correzione proposta** (spazio al posto del `;`, cifra in meno, `n` minuscola) | §C commenti, D7, F6, M6, V2, G3, L4, Q6, R3, F4, R-4 | 713 commenti in coda in 70 file · `GCI.tfl`, `lirrctr.tfl` doppio, `test.artcc`, `limw.pol` orfano · 86 righe `.geo` illeggibili, 5 dei NAVAIDS |
| 3 | **Scheda tipizzata**: ogni tipo di record ha la sua descrizione dei campi (nome italiano, significato, editor). Editor: **tipo fisso** da elenco col significato, **punto** = coordinate o nome coi suggerimenti filtrati dai NAVAIDS (scelto un nome scrive `NOME;NOME;`), **quota** (`FL80`, `2500ft`, centinaia), **numero**, **elenco** (posizioni, piste dal `.rw`), **sì/no**, **metadati** (i campi del catalogo di §M appaiono da soli nella scheda del loro tipo). «+ Nuovo record» chiede il tipo fisso prima di tutto | A1, A2, H2, J2, E4 (tipo e punti), le schede di ogni cartella partono da qui | ogni tipo di record del fork si apre senza «campo sconosciuto»; la scheda scrive la stessa riga che scrive oggi il motore (nessun byte diverso a parità di valori) |
| 4 | **Colori**: selettore vero + i nomi di `colors.def`; **mappa coi colori di Aurora** dallo schema scelto (`LIRR_RDR_V1.0.clr`, scelto una volta, fuori dal sector) per tutti gli strati; settori dinamici solo bordo; `#AARRGGBB` con l'avviso su *Smooth Drawing* | D2, D3, I1 | la mappa del fork accanto a uno schermo di Aurora: stessi colori per ARTCC, aerovie, SID/STAR, fix, costa, terra |
| 5 | **Sequenze di punti e gesti sul record**: inserisci un punto qui, inverti, **spezza/unisci** (`DUMMY`, `BREAK`, `<br>`, riga vuota dei `.geo` e delle `.mva` di scalo), **nascondi/mostra** (commenta e scommenta un blocco, grigio nell'elenco) su tutti i file; **vista a linea** dei `.geo` (un gruppo = una linea, un vertice riscrive i due segmenti che lo toccano) | B3, B6, B7, G1, R-3, R-5, E4, K4 | 28 `BREAK`, i `T;DUMMY`, 1 971 `<br>`: spezzare e riunire torna al file di prima; 52 righe MVA commentate e 276 di `limm_tma` si vedono grigie |
| 6 | **Gruppi e blocchi come Aurora**: sotto un file le voci della sua finestra di selezione (*ACC Selection*, *MVA Selection*, alta e bassa), le parti col nome dal commento sopra, ognuna accesa/spenta sulla mappa; i blocchi `//@` di §M (aerovia, zona) come unità; la **regola commento/blocco** di §M (un pezzo che riceve il primo dato passa a blocco); nome dal commento cambiato dalla scheda; «Percorso/Poligono senza titolo» = nome mancante | A3, J1, B1 (vista), H3, E1 (vista), §M nome | `FRA.artcc` 6 gruppi, 31 tratti di FRA BDRY con i nomi; `lirr.hartcc` 13 voci; 564 «senza titolo» |
| 7 | **«Chi lo usa»** e rinomina: indice dei riferimenti a fix, navaid, attese, punti VFR, **posizioni** (`.frq` ↔ teste `.tfl` ↔ trasferimenti), **piste** (`.rw` ↔ `.sid`/`.str` ↔ PAR ↔ marcature ↔ `NN.` di M9), **file** (`.cpr`, `.atis`), nomi di `colors.def`, **valori dei tag** (`fix=`, `trans=`, `compose=`); rinominare aggiorna tutto (una voce nelle modifiche, più diff), togliere è impedito se è usato, spostare avvisa | L2, R-1, S4 (fra scali) | rinomina di prova di `LUSIL` e di una posizione sul fork: il diff tocca tutti e soli i file giusti |
| 8 | **Famiglie di forme e gemelli fra tipi diversi**: `form=` (confronto come ANELLO, inizio e verso qualsiasi, formati diversi), modifica propagata alle copie uguali, «allinea anche questa», «copia la forma da…», avviso «copie di forma diverse»; `.geo` ↔ `.pol` (una forma, uscite bordo/riempimento), confine dello scalo ↔ erba; gemello `.vfi` ↔ `VFR_NASCOSTI.fix` (chiave = codice, nasce col punto) | D5, J3, Q5, I2, H10, F2 | 120 famiglie identiche e 12 divergenti (§5); 1 585 coppie `.geo`/`.pol`; 496 gemelli uguali, 9 diversi, 81 senza, 7 orfani |

### Ondata 2 — una cartella alla volta

L'ordine proposto va dai file che gli AOD toccano di più a quelli che toccano meno (da confermare, §5). Ogni slice
usa le schede, i gesti e le viste dell'ondata 1 e aggiunge solo quello che è del suo file.

| # | Slice | Voci | Uscita misurata sul fork |
|---|---|---|---|
| 9 | **Procedure** (`.sid`, `.str`): schede di SID e voci (tipo come i tasti di Aurora; nel `MAPS` «si accende col tasto …»), piste dal `.rw`, SID nuova nel gruppo della sua pista, vista per pista e tipo; metadati `fix`, `trans`, `initialclimb`, `wtc`, `cat`, `nav`, IAP `type`/`mins`/`gp`, per punto `role`/`alt`/`spd` (al passaggio del mouse, mai in Aurora); «valori per pista» come gesto; fix proposto dal nome della SID; legami STAR → attesa → IAP → GA; controlli (ripetuta, 6° campo, `;` mancante, decimali, voce di un altro scalo, piste inesistenti, nomi diversi nei due campi) | P1-P3, P6, P7, P11, Q1, Q2, Q2b, Q2c, Q2d, Q6, R-4 | 982 fix automatici / 277 da scegliere; 11 SID ripetute, 28 campi spostati, `limf.sid:28`; `liba.str` 27 punti decimali, `licz.str` LICC, `ALPHA SUOTH` |
| 10 | **NAVAIDS e attese**: schede tipizzate di fix, VOR, NDB (tipo, confine, visibilità, tipo VOR, TACAN), attesa collegata e info `ABBOZ/225R-9000` a campi; scheda dell'attesa (`HOLDENR.hold`) legata al fix nei due versi; nome unico | L1, L3 (estende `NomeDuplicato`/`NomeRipetuto`), L4, U1 | `EKLAP` → `HLD-ELKAP`; 24 nomi in posizioni diverse, 245 doppioni; 5 righe illeggibili |
| 11 | **OTHER e PREFS**: scheda della posizione (trasferimenti in due elenchi includi/escludi, profilo/ATIS/D-ATIS scelti fra i file che esistono, CPDLC, LOA); scheda dello scalo (nascosto, tipo, metadati M10); scheda della pista (rotta calcolata dalle soglie, `MAPS` e voci di settore come voci di menu, «arrotonda al grado», metadati M9: TORA dagli intermedi, circuito, limiti d'uso); CPDLC; **lettore `.cpr`** e scheda del profilo (ogni impostazione dice che sovrascrive l'utente; chiavi da un profilo completo di Aurora), PAR per pista dal `.rw` | M1-M5, M9, M10, N1-N4 | 51 include dopo un escluso; `LIPC.cpr`, `\liml.atis`; soglie invertite `LIDW 15`, `LIKL 36`; 44 rotte con decimali, 13 primarie oltre il 18; 10 profili PAR (radiale ±0,2°, elevazione ±1 ft) |
| 12 | **Terra** (`.geo` di scalo, `.pol`, `RW_MARKINGS`, `.txi`, `.gts`): strati per tipo, tipo fisso, scheda del poligono, ordine di disegno; **vista per scalo** (tutto quello che riguarda LIRF, da qualunque file) e dentro la **vista per pista** delle marcature; schede di stand (tipo, slot spiegato, metadati R2b, tipo e slot **proposti** da codice e uso) e taxiway (R6); punti di startup a mano (prima la prova R4 in Aurora); controlli (poligoni < 3 vertici, colore sconosciuto, `.pol` senza `.geo`, strisce, marcature lontane dalle soglie, etichetta lontana dalla taxiway, stand lontano, ICAO diverso, stand più grande della taxiway) | H1, H2, H3, I1, I3, I4, I6, O1, O3, R1-R4, R6 | 10 tipi vuoti in `liap.geo`; 43 poligoni < 3 vertici; 27 etichette taxiway oltre 100 m; LIBP in `libg.gts`, `L3MC`/`L4MC`, `LINB` |
| 13 | **Settori e spazi** (`.tfl`, `.hartcc`, `.lartcc`): testa del settore dinamico in una scheda (posizioni coi suggerimenti dai `.frq`, larghezza, opacità, filtro); settore italiano legato ai `.frq`; limiti verticali e classe (anche ATZ/CTR del `MAPS`); file giusto per un settore nuovo (`_CTR`/`_APP`); nomi coerenti delle configurazioni; nome ripetuto nello stesso file | D1, D4, D9, J4, J5, J7, K1, Q8, R-4 | 4 posizioni italiane assenti dai `.frq`; `LIBB_FSS`, `LIMM_FSS`; `RR CONF1`/`RR CNF1`/`MM CONF 2.1` |
| 14 | **ACC e aerovie** (`.artcc`, `.lairway`): avvisi degli `.artcc` (cerchio non chiuso, centro fuori dal confine, stanghetta AOCC ≠ 15 NM, etichetta lontana dal fix); aerovie: scheda per tratto (verso e quote, `//@@`), etichette **calcolate** (`gen=labels`), aggiungere e togliere un'aerovia a mano, controlli | A4, B2, B4, B12, B14, B15 | cerchio `X07-X08` aperto; 22 aerovie senza etichetta, 19 etichette coi nomi «U», 896 etichette ricalcolate: quante cambiano |
| 15 | **MVA** (di ACC e di scalo): zona = blocco con soprannome, scheda con la quota e il suo significato, valori speciali, gruppo nel 5° campo scritto dal Lab (anche sui `DUMMY`), stile ACC per le MVA di scalo, controlli | E1-E5, S1-S3 | 20 etichette fuori da ogni zona; 16 file di scalo «un nome per zona»; quote piene in `libn`, `libv`, `lict` |
| 16 | **VFR** (`.vfi`, `.vrt`, rotte en-route): schede delle quattro strutture (2° campo «Codice», tipo 0-3), codice proposto, scheda della rotta VFR (punti dal `.vfi` dello scalo e dei vicini, quote e verso per tratto), controlli | F1, F3, F4, F8, S4, S6 | `lict.vfi` senza `;`; 13 punti di `.vrt` dal `.vfi` di uno scalo vicino |

### Ondata 3 — gli editor speciali

| # | Slice | Voci | Uscita misurata sul fork |
|---|---|---|---|
| 17 | **Simboli**: lettore `.sym`, editor a pixel 13×13 (anteprima vera e ingrandita su sfondo radar, colonne nell'ordine giusto, nome nel commento, ordine visibile), anche per i `SYMBOLS_*` di un `.cpr`; controlli. Prima la prova T3 in Aurora (l'ordine conta?) | T1, T2 | 23 simboli riletti e riscritti uguali; le 2 righe senza `//` |
| 18 | **ATIS e D-ATIS**: editor dei modelli (segnaposto da elenco, parti facoltative evidenziate, anteprima riempita con un METAR vero), **«Ascolta»** con la voce di Windows, ATIS ↔ D-ATIS affiancati, controlli | W1-W4 | 7 `.atis`, 4 `.datis`, `atisextra.fds`; `\liml.atis` |
| 19 | **Prova del lotto intero**: giro di prove del committente come quelle di F3 (PROVE.md), la sintassi dei tag letta da vIPI in una prova (solo lettura, nessun cambio al sito) | — | tutte le prove ✅ |

## §4 — Cosa NON è nel lotto

- **F4** (rami git, changelog, `delete.upd`, adozioni in un ramo: famiglie D6, blocchi E6, marcature O4, S2 adozione,
  pulizie decise, **correzioni dei dati R-10**) e le **prove in Aurora** (T3, R4, B9, A8, F5, I7/K2): le fa il
  committente in rami di prova; il Lab le prepara quando servono (T3 prima della slice 17, R4 prima di R4 nella 12).
- **F6-F9**: import dai PDF e dal DB di IVAO (anche TA e IATA, CTR/ATZ), procedure dai PDF, generatori di geometria
  (gate, AOCC, marcature, prolungamenti, ovali, archi: il catalogo `gen=` c'è già dalla slice 1, i generatori no),
  trascinare sulla mappa.
- La **consegna** agli AOD (slice 11 di F3): la decide il committente. Proposta: dopo l'ondata 1 e le prime slice
  dell'ondata 2 c'è già un Lab che copre la gran parte dei file che gli AOD toccano ogni ciclo.
- **R-7** (coordinate in una forma sola per `.vfi`/`.tfl`) e **R-8** (`LIMJ_APP`/`LIBB_APP`): rimandate.
- **Per il futuro** (committente, 28 settembre: «lasciali così, segnali come cose per il futuro»): la **rinomina di
  un file** (vuol dire spostarlo sul disco: il salvataggio oggi scrive solo contenuti; aggiorna le righe `F;` degli
  `.isc` e i campi dei `.frq`) e la **rinomina di un nome di `colors.def`** (serve un lettore e uno scrittore del `.def`
  nel motore; TAXIWAY sono circa 19 000 righe in 164 file). «Chi lo usa» di file e colori c'è già (slice 7e).

## §5 — Decisioni del committente (27 settembre)

1. **L'ordine dell'ondata 2** resta quello proposto: procedure → NAVAIDS → OTHER/PREFS → terra → settori → ACC e
   aerovie → MVA → VFR.
2. **Il punto della consegna agli AOD**: «vediamo lavorando» — si decide strada facendo.
3. **La prova dei tag con vIPI** nella slice 19: **sì**, una lettura di prova (nessun cambio al sito).

La carta è **approvata**: si parte dalla slice 0.

## §6 — Traccia

**Slice 0 — misure di partenza (27 settembre, fork `8cf32c6`, motore di `lab/f3`).** `tools/Vipi.SectorfileProva`
sull'albero `Include\IT`:

- **Round-trip**: 701 file esatti, 0 diversi. **47 senza lettore**: 22 `.txt` (changelog), 15 `.cpr`, 4 `.datis`,
  e uno ciascuno `.clr`, `.cpdlc`, `.cpdlcnames`, `.def` (`colors.def`), `.fds`, `.sym`. → Il lotto aggiunge i
  lettori di `.cpr` (slice 11), `.cpdlc`/`.cpdlcnames` (11), `.sym` (17), `.datis`/`.fds` (18); `colors.def` e lo
  schema `.clr` di Aurora servono già alla slice 4 (il `DefParser` c'è ma la prova non lo usa: da collegare).
- **Righe opache** (il lettore le salta): 93, di cui 86 sono le righe `.geo` con lo spazio al posto del `;` (slice 2).
- **Validatore oggi**: 125 errori, 382 avvisi — `SeparatoreSbagliato` 86, `NomeNonRisolto` 16, `FileCitatoAssente` 8,
  `CoordinataFuoriCampo` 5 (`lovv.tfl:48` secondi 60), `NomeDuplicato` 5, `PoligonoConPochiVertici` 4,
  `CoordinataIllegibile` 1 · avvisi: `NomeRipetuto` 285, `CoppiaDecimale` 40, `CopieDiverse` 29, `FrazioneAmbigua`
  14, `DueNomiDiversi` 7, `FileMaiCitato` 6, `EmisferoMinuscolo` 1.
- **Tag**: 2 782 record etichettati e ritrovati, 148 file su 148 identici senza le righe `//@`; **1 guasto**: in
  `limf.sid` una SID col **nome vuoto** (la riga 28 senza `;`, P3) non si può dichiarare → slice 1: il Lab dice
  perché non mette il tag; la correzione del dato è in R-10.
- **Concordanza con vIPI**: 685 643 coordinate DMS, 0 discordi; punti 4 048, SID 1 516, STAR 863 concordi.
- **Modello del motore**: ~20 tipi di record (`SidProcedure`, `StrRecord`, `Runway`, `Stand`, `MvaSector`, `RottaVfr`,
  `AtcPosition`, `ElementoArtcc`, `FicSector`, `LabelPoint`, `StaticBoundaryGroup`, `Attesa`, `Vor`, fix/NDB, `.ap`,
  `.geo`/`.pol`…): sono le descrizioni che la slice 3 deve scrivere, una per tipo.

Cosa cambia nelle slice dopo, dalle misure:

- **Commenti in coda: 713 avvisi** annegherebbero il pannello → nel pannello dei problemi **una riga per file** col
  numero («`limm.mva`: 374 commenti in coda») e il gesto «sposta sopra» sul file intero (scelta dell'agente).
- **Nomi in posizioni diverse**: la carta dice 24 (§12, fra file diversi), il validatore ne dà 5 come errore
  (`NomeDuplicato`, stesso catalogo) → la slice 10 allinea le due misure prima di toccare la regola.
- Le altre regole nuove hanno già la loro misura nella carta «file per file» (colonna «Uscita» qui sopra): nessuna
  dà centinaia di avvisi falsi, salvo i 713 commenti in coda.

**Slice 1 — metadati nel motore.** Divisa in passi, un commit ciascuno: **1a** grammatica di §M · **1b** i file a una
riga per record · **1c** i tag di punto `//@@` in `.sid`/`.str` · **1d** i file a blocchi (`.artcc`, `.mva`,
aerovie, `.tfl`, `.hartcc`/`.lartcc`) · **1e** prova sull'albero intero.

- **1a (27 settembre)** — `IO/CatalogoDeiTag.cs` (nuovo): le chiavi per tipo di file (comuni `locked`, `gen`, `note`;
  SID e STAR come §M; chiavi **per verso** `06.tora` con la regex del numero di pista), una chiave fuori dal catalogo
  del suo file = avviso. `IO/Metadati.cs`: parole separate dagli spazi **fuori dalle virgolette**, valori conservati
  come scritti e letti con `Testo`, `ValoreDaScrivere` mette le virgolette solo se servono, `Scrivi` rifiuta un
  valore che riletto non sarebbe una parola sola; `composta`/`intere` → `compose`/`whole` (`Metadati.Compose`,
  `Metadati.Whole`); elenco delle composte con nomi fra virgolette (`ScriviLElenco` ↔ `ElencoDellaComposta`), e
  `NomeElencabile` lascia fuori solo nomi vuoti o con virgolette dentro → le 63 procedure con spazi (51 SID militari,
  `RNP10 UPETI`, `AAR …`) ora si compongono; `//@@` riconosciuto (`EUnTagDiPunto`) e saltato: non è una
  dichiarazione. Lab: la casella di `RNP10 UPETI` si accende, il gesto scrive `compose=ODIN4E,"RNP10 UPETI"`.
  Test: motore 479 → **512** (net8 e net10), Lab **358** (un test rovesciato: il nome con spazi si elenca). Albero
  intero invariato: 701/701, 0 diversi, tag su tutto 2 782/2 782, lo stesso guasto noto di `limf.sid`.
  🔴 Il cancello di `main` (`~/.claude/hooks/main-gate.mjs`) rifiuta `tools/conta-test.sh` («comando annidato
  troppe volte»: legge dentro lo script); il conteggio è scritto a mano coi numeri del log, la CI lo ricontrolla.
- **1b (27 settembre)** — i file a una riga per record: `CatalogoDeiTag` per `.rw` (M9: `width`, `length`,
  `vfronly`; per verso `thr`, `ils`, `tora` `toda` `asda` `lda`, `int`, `circuit`, `dep`, `arr`, `vfronly`), `.ap`
  (M10), `.gts` (R2b), `.txi` (R6), e le sole chiavi comuni per `.fix`, `.vor`, `.ndb`, `.vfi`, `.frq`, `.hold`.
  `Metadati.NomeDelRecord` (il nome d'aggancio di ogni tipo: la pista è la coppia, `LIRN 06/24`), `Metadati.Leggi(letto)`
  col nome e il catalogo del tipo, `Metadati.ProblemiDi(letto)`: il validatore ora guarda i tag di **ogni** file che
  ha un catalogo. Le piste sono record solo dentro `//PISTE` (come nei `.rw` veri). La prova «tag su tutto» ora passa
  anche questi file: **11 265 record su 11 265** ritrovati coi loro tag, **366 file su 366** identici senza le righe
  `//@` (erano 2 782 record e 148 file); i `.fix` vuoti si saltano (niente da etichettare), resta il solo guasto noto
  di `limf.sid`. Test: motore 512 → **522**, Lab **358**.
- **1c (27 settembre)** — i tag dei punti `//@@"PUNTO" …` in `.sid` e `.str`. `SidParser` e `StrParser`: un `//@@`
  resta nella procedura (un `//@` la chiude ancora), anche sopra il primo punto di una SID e sopra il punto che apre
  un tratto dopo una riga vuota (🔴 trovato dalla prova sull'albero in `lied.sid`: il lettore lo prendeva per un
  commento e chiudeva la SID). `Metadati.Leggi` aggancia ogni `//@@` alla riga dati subito sotto (`ChiaveDelPunto`: il
  nome, o le due coordinate col `;`) → `MetadatiDelFile.Punti`/`PuntiDi`; problemi nuovi `TagDiPuntoOrfano`,
  `PuntoNonCombacia`, `TagDiPuntoFuoriDalRecord` (errori), chiave fuori dal catalogo dei punti (avviso).
  `ScriviIlPunto`/`TogliIlPunto` scrivono sopra il punto e tolgono, e il file torna uguale. Prova sull'albero: ogni
  punto di ogni SID e STAR etichettato → **35 898 su 35 898** ritrovati, ogni record riletto si scrive come prima,
  366 file identici senza le righe `//@`. Test rosso sul codice di prima: col vecchio `StrParser` 8 test su 17
  cadono (la STAR si spezzava). Test: motore 522 → **540**, Lab **358**.
- **1d (27 settembre)** — i file a blocchi: `.artcc`, aerovie, `.mva` (di ACC e di scalo), `.tfl`/`.fic`,
  `.hartcc`/`.lartcc`, `.geo` (anche le aree P/R/D e `RW_MARKINGS`), `.pol`. Deciso nel codice prima di scrivere
  (scelte dell'agente, dentro §M):
  - **I lettori a blocchi chiudono il record su un `//@`.** MVA, ARTCC, TFL, confini e POL tenevano ogni commento
    nel blocco aperto: un `//@END` senza riga vuota finiva nel record, e due zone MVA di fila diventavano una. Ora un
    `//@` (non `//@@`) chiude il record, come in SID e STAR dalla F2 slice 7 (`Metadati.EUnTagCheChiude`). Nelle
    aerovie, dove ogni commento chiudeva già il record, il `//@@` resta col punto sotto, anche il primo (B2).
  - **Un blocco tiene più record.** Fra `//@START` e `//@END` stanno tutti i record col nome del blocco o senza nome
    (`MetadatiDelRecord.Records`, `Di(record)` dà il blocco anche per il 2°, 3°… pezzo): la zona MVA fatta di più
    poligoni (E1: «cosa sta nella zona lo decide l'utente»), il gruppo di segmenti di un `.geo`, l'aerovia che il
    `BREAK` spezza in tre record (B1). Un record di un altro nome dentro il blocco resta un errore (`NomeNonCombacia`);
    una dichiarazione dentro un blocco aperto è un errore nuovo (`DichiarazioneNelBlocco`: i blocchi non si annidano).
    `ScriviIlBlocco(primo, ultimo, nome, chiavi)` scrive il blocco su un tratto di record; `Scrivi` su un pezzo di un
    blocco cambia la sola dichiarazione; `Togli` toglie il blocco intero (la fine la ritrova dove l'ha letta).
  - **Record senza nome suo** (`NomeDelRecord` = null): il poligono `.pol`, il segmento `.geo` senza 6° campo,
    l'etichetta `.artcc` senza fix, la riga `BREAK`. Prendono il nome del blocco (§M: «nei file senza nome nelle righe
    di dati il nome del blocco è il nome del gruppo»); `Scrivi` da solo li rifiuta, il nome lo dà chi scrive.
  - **Nomi d'aggancio**: aerovia = 2° campo; settore = la testa (`LIPX_ES0_APP`, `LIBB_ES_CTR LIBB_EU_CTR`); gruppo di
    confini = il nome del primo vertice (`RR CONF2`, `FRA BDRY`); blocco MVA = 2° campo della prima riga `L;`/`T;` che
    non è `DUMMY`, anche commentata (`MvaSector.Nome`, nuovo, solo lettura: `LIMM` nei `.mva` di ACC, `CERCHIO-BA`,
    `RR US0` in quelli di scalo); area P/R/D = 6° campo (`D5A`). Il punto di un'aerovia sta nel 3°-4° campo
    (`ChiaveDelPunto<Airway>`).
  - **Catalogo** (`CatalogoDeiTag`): `.artcc` le comuni (i parametri di gate e AOCC arrivano coi generatori, A5-A7);
    aerovie le comuni, e per tratto `dir`, `lower`, `upper` (B2); `.mva` + `zone` (E1); `.tfl` + `form`, `lower`,
    `upper`, `class` (D5, D9); `.hartcc`/`.lartcc` come i `.tfl` più `compose`, `whole` (J6, J7); `.geo` + `form`,
    `lower`, `upper`, `class` (I2, H10, G5: segmenti e aree hanno lo stesso lettore, quindi lo stesso catalogo); `.pol`
    + `form`.
  - Il nome della forma di un `.fic` (`ShapeLabel`) salta le righe di tag: la dichiarazione sta fra `//GARDA` e la testa.
  Prova sull'albero: round-trip 701/701, 93 righe opache e validatore (125 errori, 382 avvisi) **invariati**; tag su
  tutto **118 417 record su 118 417** in **672 file** identici senza le righe `//@` (erano 11 265 e 366); punti
  **38 207 su 38 207** (+2 309 punti delle aerovie); nuova misura **BLOCCHI A PIÙ PEZZI**: i record di fila con lo
  stesso nome (o senza) in un blocco solo → **107 152 record su 107 152** ritrovati nel loro blocco, 1 848 blocchi di
  cui 805 con più record, 306 file identici senza le righe `//@`. Resta il solo guasto noto di `limf.sid`. Test rosso
  sul codice di prima: coi sei lettori vecchi 13 test nuovi su 23 cadono. Test: motore 540 → **563**, Lab **358**.
  `.vrt` (rotte VFR, F8/S6) rimandato alla 1e (committente: «mettilo nella 1e»).
- **1e (27 settembre)** — le rotte VFR di scalo (`.vrt`, F8 e S6) e la prova su tutti i formati. `VrtParser` chiudeva
  la rotta a ogni commento: ora il `//@@` resta col punto sotto, come nelle aerovie. Nome d'aggancio = il numero della
  rotta (`//@"1"`); il punto sta dopo il numero (`1;ROGOREDO;ROGOREDO;` → ROGOREDO); catalogo `.vrt` = le comuni, e
  per tratto `dir`, `lower`, `upper`. Con questo **ogni file che il motore legge porta i tag di §M**, salvo gli `.atis`
  (modelli di testo, §22: nessun metadato previsto). Prova sull'albero: round-trip 701/701, opache e validatore
  invariati; tag su tutto **118 469 su 118 469** in **687 file** (+52 rotte in 16 `.vrt`); punti **38 330 su 38 330**
  (+123 delle rotte VFR); blocchi a più pezzi **107 204 su 107 204** in 321 file. Resta il solo guasto noto di
  `limf.sid`. Test rosso: col `VrtParser` di prima 3 test nuovi su 6 cadono. Test: motore 563 → **569**, Lab **358**.
  **Slice 1 chiusa**; prossima la slice 2.

**Slice 2 — controlli comuni sul testo.** Divisa in tre passi, un commit ciascuno: **2a** commenti in coda · **2b**
controllo degli `.isc` · **2c** coordinate scritte male con la correzione proposta.

- **2a (27 settembre)** — i commenti in coda (§C). Misura: **712 righe in 54 file** letti dal motore (più 1 in un
  `.txt` del changelog: sono le 713 della carta; i «70 file» di §6 non tornano, sono 55 con quel `.txt`): `.mva` 456,
  `.str` 119, `.tfl` 36, `.sid` 34, aerovie 28, `.artcc` 18, `.hartcc` 15, `.lartcc` 6.
  - Motore: `IO/CommentiInCoda.cs` (dove sta il `//` dopo i dati, come si separa, il testo coi commenti spostati).
    Validatore: regola nuova **`CommentoInCoda`, avviso, UNO per file** col numero e le righe (scelta della slice 0:
    713 avvisi annegherebbero il pannello); non negli `.atis` (sono testi). Albero: 125 errori, 382 → **436 avvisi**
    (+54, uno per file).
  - 🔴 **Il Lab ne scriveva**: il lettore delle MVA di scalo prendeva `//FL110` di `T;110;N…;E…; //FL110` per il 5°
    campo, e lo scrittore lo riscriveva come dato, due volte (`T; //FL110;N…;E…; //FL110;`): **74 righe in 8 file**
    (`lipe` 26, `limj` 18, `liee` 11…), a ogni record MVA toccato. Corretto nel lettore: un 5° campo che comincia con
    `//` non è un campo. Nuova misura nella prova sull'albero: gli scrittori, riscrivendo **ogni** record dal modello,
    scrivono **0** commenti in coda (erano 74); test su sette formati.
  - Lab: gesto **«Sposta sopra i commenti di …»** sulla voce del pannello (file intero) e **«Sposta il commento
    sopra»** nella riga scritta a mano (riga sola): `ModificheInSospeso.CambiaRighe` (una riga ne diventa più d'una,
    una voce sola, si annulla). Il controllo delle modifiche confrontava i problemi per testo della riga: spostato il
    primo commento, l'avviso del file cambiava riga e risultava «introdotto» (visto a schermo) → per `CommentoInCoda`
    è nuovo solo se i commenti del file crescono.
  - Prova sull'albero del gesto: spostati sopra tutti i commenti di ogni file, **53 file su 54** si rileggono con gli
    stessi record; l'unico diverso è `lirs.str`, dove `<br> //ZONA2` diventa `<br>` e il Lab legge l'interruzione
    (come, si suppone, Aurora che il commento lo toglie).
  - 🟡 **Da provare in Aurora prima della pulizia (F4)**: una riga di commento **dentro** un tratto (`T;` di MVA e
    confini, `.artcc`) apre un tratto nuovo? Nei `.geo` sì (specifica), per gli altri non si sa. Il gesto oggi mette
    il commento sopra la riga ovunque: 374 `//Coast` in `limm.mva` stanno in mezzo ai poligoni.
  - Prova a schermo: Lab su una copia del fork (server locale, browser del pannello): voce `CommentoInCoda` di
    `itawlow.lairway` col tasto, 28 commenti spostati (diff giusto), annulla; riga 187 dalla riga a mano; nessun
    avviso «introdotto». Test: motore 569 → **587**, Lab 358 → **363**; rosso sul codice di prima: lettore MVA 1 su
    18, controllo delle modifiche 2 su 5.
- **2b (27 settembre)** — il controllo degli `.isc` (§C, D7, F6, M6, V2, A9). In `CarichiDegliIsc` (lo usano il
  validatore e il catalogo dei punti del Lab) e nel validatore dell'albero; tutte **avvisi**:
  - **`FileCitatoAssente` diventa avviso** (§C, M6). Se sotto `Include` c'è UN file solo con quel nome, Aurora lo trova
    per nome: conta come caricato e il dettaglio dice dove (scelta dell'agente: con due omonimi non si indovina).
    Fork: `DYNAMIC_SEC\GCI.tfl` → `OTHER\GCI.tfl` in 5 `.isc` (e `GCI.tfl` non è più «mai citato»); mancano davvero
    `PREFS\LIPC.cpr` (`itfreq.frq:175`, `lipp.frq:14`) e `DYNAMIC_SEC\lipp_es_ctr.tfl` (`LIPP.isc:130`).
  - **`FileInclusoDueVolte`** (D7): `lirrctr.tfl` in `ITALY.isc:254` e, nuovo, `lippctr.tfl` in `LIPP.isc:133`.
  - **`FileNellaSezioneSbagliata`** (F6): la forma di ogni sezione dal manuale (estensioni ammesse); `[VFRENR]` anche
    dalla forma della prima riga (vuole le rotte, `Numero;Lat;Lon;…`). Fork: i 3 `.vfi` di `ENRVFI` sotto `[VFRENR]`
    in 4 `.isc` = 6 avvisi (F5, da provare in Aurora); nessun altro file fuori posto.
  - **`FileVuoto`** (A9): `ACC	est.artcc` (incluso), i 3 `.fix` vuoti di `NAVAIDS`, `itawhigh.hairway` (tutto
    commentato) e, nuovo, **`lirm.vrt`**, che Aurora carica per nome di scalo ed è vuoto.
  - **`FileMaiCitato` con «è una copia di…»** (V2): se almeno il 90% delle coordinate di un orfano sta in un file
    caricato (a pari, quello dello stesso formato). Fork: **`limw.pol`** — ora orfano, perché le estensioni caricate
    per nome di scalo seguono il manuale (gts, txi, sid, str, vfi, vrt, mva, tfl; via `geo` e `pol`, §19, §21) — «copia
    di `GND_LAYOUT\mw_ad_gnd.pol`: le sue 52 coordinate ci stanno tutte». Gli `.atis` restano fra quelli per nome.
  Albero: **117 errori** (−8: i percorsi sbagliati ora sono avvisi), **458 avvisi**. La prova sull'albero stampa ora
  «FILE E INCLUDE» uno per uno. Test rosso: con la gravità e le estensioni di prima 2 test nuovi su 5 cadono (gli
  altri tre sono regole nuove). Test: motore 587 → **592**, Lab **363**.
- **2c (28 settembre)** — le coordinate scritte male e la **correzione proposta** (G3, L4, Q6, R3, F4). Misura prima
  (forme dei token sul fork): il compatto giusto ha **10 cifre**; fuori forma sono 22 gradi a due cifre col punto
  (`E12.30.18.200` 12 in `lirz.gts`, `N44.18.55.72` nei tre `.rw`, `lipz.str`, `lirn.vfi`), 14 frazioni di due o
  quattro cifre, 4 compatti di lunghezza sbagliata, più i casi già segnalati (86 spazi al posto del `;`, trattino,
  `n` minuscola, secondi a 60/72/75/99, 40 coppie decimali).
  - Motore: `Validazione/CorrezioneDelleCoordinate.cs` (il token nella forma giusta; la riga intera con lo spazio
    diventato `;` e la coppia decimale scritta in DMS, non nei `.txi`). Si propone solo quello che la riga dice già:
    secondi a 60 → il minuto dopo (lo stesso punto), frazione arrotondata al millesimo, secondi a tre cifre col punto
    scivolato (`E010.34.072.00` → `E010.34.07.200`); coi secondi a 75 o 99 niente. `ProblemaDelSector.Proposta` (la
    riga corretta) su ogni problema di coordinate.
  - 🔴 **Due punti letti altrove, in silenzio**: il motore e vIPI leggono il compatto **da destra** (concordanza 0
    discordi: sbagliano uguale). `E01221856000` (`lirf.vfi:6` COLOMBO e `VFR_NASCOSTI.fix:399` RFS3, 11 cifre) si legge
    **E122.18.56 — in Asia**; togliendo una cifra escono tre punti plausibili vicino a Fiumicino, quindi nessuna
    proposta. `N041131620` (`MIL.fix:14` BV-VICTOR, 9 cifre coi gradi interi) si legge **N004.11.31 — in Africa**;
    proposta `N0411316200` (N041.13.16.200). Regola nuova **`CoordinataLettaAltrove`, errore** (3 sul fork). Con 9
    cifre senza lo zero davanti (`E103441000`, `lipx.vfi` GAZOLDO) il valore letto è giusto: **`CoordinataFuoriForma`,
    avviso** (23 sul fork, coi gradi a due cifre), proposta la forma canonica.
  - Lab: sotto la voce del pannello la riga proposta e **«Correggi la riga»** (`SessioneDelLab.Correggi`: la scrive come
    una riga a mano, una voce, si annulla; se nel frattempo la riga è cambiata non la scrive).
  - Prova sull'albero: **169 problemi con la proposta su 163 righe; applicate a una copia, 163 righe su 163 tornano
    senza problemi**. Albero: **120 errori** (+3), **481 avvisi** (+23). Le righe opache della prova erano salite a 95:
    la misura dei commenti spostati (2a) rileggeva il file e una sua copia col raccoglitore comune — ora la prova conta
    solo i file dell'albero, una volta (93).
  - Prova a schermo col banco (server del Lab su una copia di `SectorFiles`): voce di `MIL.fix:14` con la proposta in
    verde, «Correggi la riga» → BV-VICTOR a N041.13.16.200, «riga 14 scritta a mano», nessun avviso introdotto.
  - Test: motore 592 → **616**, Lab 363 → **366**. Rosso: le regole e la proposta sono nuove (il codice di prima non
    compila i test); il rosso vero è sul fork — `E01221856000` e `N041131620` prima non davano nessun avviso.
  - Resta di F4 (VFR, slice 16): la «riga con un campo in meno» di `lict.vfi` e la «forma diversa dal file» (R-7,
    rimandata).

**Slice 2 chiusa.** Da provare a mano (eseguibile ripubblicato): pannello, «Sposta sopra i commenti», «Correggi la riga».

**Slice 3 — scheda tipizzata.** Divisa in passi, un commit ciascuno (scelta dell'agente, come le slice 1 e 2): **3a**
le descrizioni dei campi e la scheda che le usa · **3b** gli editor (tipo fisso, sì/no, numero, quota, elenco) ·
**3c** il punto coi suggerimenti dai NAVAIDS · **3d** i metadati del catalogo nella scheda · **3e** «+ Nuovo record»
che chiede il tipo fisso.

- **3a (28 settembre)** — `Ispezione/DescrizioniDeiCampi.cs` (Lab, nessun codice comune): per ognuno dei **24 tipi di
  record** del motore i campi nell'ordine della riga, col nome dell'AOD, il significato dal manuale (carta «file per
  file» §1-§22) e l'editor (`Testo`, `TipoFisso` coi valori e il loro significato, `Coordinate`, `Punto`, `Navaid`,
  `Quota`, `Numero`, `Elenco` con la fonte — scali, piste, posizioni —, `Colore`, `SiNo`, `SolaLettura`). Le MVA hanno
  due descrizioni: di ACC la quota in centinaia (5° campo della `L`), di scalo il 2° campo resta testo (sul fork è
  una quota, `4000` o `110`, o il nome della zona, `RR US0`). Scelte dell'agente:
  - **Nascoste** `Source`, `Sources`, `HasConflict`: non sono campi della riga (la testa della scheda e le righe
    dicono già da dove viene il record).
  - **Dove il manuale dà solo i valori non si inventa il significato**: la visibilità 0/1 dei VOR, il confine 0/1 dei
    fix, l'opacità dei settori restano «0» e «1».
  - Il 6° campo delle SID il motore lo chiama `DefaultVisible`: per il manuale è il **tipo** (0 SID, 1 transizione) e
    così si chiama nella scheda; i nomi delle proprietà del motore non cambiano (sono il contratto).
  - I colori dei settori sono `Colore` (testo finché la slice 4 non porta il selettore); i tipi dei `.geo` (H2) e i
    riempimenti dei `.pol` sono tipo fisso: sono gli strati di Aurora.
  La scheda (`Ispettore`): i campi descritti nell'ordine della riga, il nome italiano col significato al passaggio
  del mouse, il tipo del record in italiano in testa («Fix · …», «Traccia (T) · …»); una proprietà che la
  descrizione non conosce si vede lo stesso, per riflessione, in coda e con la scritta **«campo sconosciuto»**.
  Prova sul fork (strumento fuori repo, `scratchpad/prova3`): **701 file, 118 505 record, 24 tipi, 0 campi
  sconosciuti**; valori fuori dagli elenchi a tipo fisso: solo `COAST` come riempimento e bordo dell'orfano `limw.pol`
  (i vuoti — 9 tipi dei fix, 10 dei `.geo` di `liap` — sono il valore «non scritto»). 🟡 Il `3:` di un fix il motore lo
  legge **0**: la scheda mostra quello che legge il motore (la riga resta com'è finché non si tocca). Test: un test
  cerca nel motore i tipi di record (i T dei lettori e i loro figli concreti: oggi 24) e vuole ogni proprietà
  descritta; rosso provato togliendo la frequenza degli NDB (due test cadono, coi record dei campioni). Test: Lab
  366 → **376**. Prova a schermo con la 3b (la 3a cambia solo i nomi nella tabella).
- **3b (28 settembre)** — gli editor. `Components/EditorDelCampo.razor` sceglie l'editor dalla descrizione, e ognuno
  finisce nello stesso `CambiaCampo` di prima col testo del valore: la riga la scrive lo scrittore del motore (test:
  cambiato il tipo di `BC404`, esce `BC404;N039.05.11.290;E017.03.27.750;1;`, solo il 4° campo diverso). **Tipo
  fisso** = elenco coi significati («3 · nascosto», «TAXI_CENTER · asse della taxiway»); un valore che l'elenco non ha
  resta com'è e si vede («PIPPO — non è nell'elenco»). **Sì/no** = casella (menu con «non scritto» per i campi che
  possono mancare, RNAV). **Quota** (`Ispezione/Quote.cs`, dentro `ModificheInSospeso.Cambia`): si scrive `FL80`,
  `2500ft`, `2500'` o il numero nell'unità del campo; le MVA di ACC in centinaia, con accanto «= 10 000 ft»; `2550ft`
  in centinaia è rifiutato col perché. **Elenco** (`Ispezione/VociDegliElenchi.cs`, dai record della sessione come sono
  adesso): scali dell'`.ap` e posizioni dei `.frq` suggeriti nel campo; le **piste** della SID e delle voci `.str` sono
  tasti sotto il campo, quelle del `.rw` del suo scalo nell'ordine del file più `MAPS` (un clic la mette o la toglie:
  `07` → `07:25`). **Numero** = campo col tastierino decimale (non `type=number`, che riscrive `118.700` in `118.7`).
  Il pannello e la storia dicono il nome dell'AOD («Tipo: 3 → 1», «Elevazione: 14 → 15, come in lirr.ap»).
  Scelte dell'agente:
  - 🔴 Le voci `MAPS` del `.rw` per il motore **non sono record** (`//MENU MAPPE` e `//ACC` restano righe grezze):
    `MAPS` si propone sempre, in coda alle piste; le voci di settore (`LIRR;NE`) sono della slice 11.
  - Il tipo «altro» dell'`.ap` (`InstallationType.Custom`) resta nell'elenco: sceglierlo scrive l'8° campo vuoto.
  - 🔴 **Guasto dello scrittore delle MVA di ACC** (codice comune, `MvaSaver`): una zona con le `T;` tutte commentate
    (la prima di `lirr.mva`) non ha vertici col gruppo nel 5° campo, e lo scrittore ci metteva la **quota**: cambiare la
    quota scriveva `L;90;…;90;8;` al posto di `L;LIRR;…;100;8;`, e il gruppo della *MVA Selection* spariva. Ora il gruppo
    viene da `MvaSector.Nome` (2° campo della prima riga), mai dalla quota. Misura su **ogni** zona del fork (quota
    cambiata, campi confrontati): 105 zone di ACC cambiano solo il 5° campo della `L`, una senza etichetta niente.
    Rosso sullo scrittore di prima: 2 test su 2.
  - 🔴 **MVA di scalo in sola lettura** (quota e carattere) fino alla slice 15: il motore legge la quota dal 2° campo
    della `L` e la riscrive anche nel 5°, ma sul fork **194 etichette su 226** hanno il gruppo nel 2° (`BB CS0`) e la
    quota nel 5° (`45`), come le ACC — cambiarle dalla scheda cancellava la quota vera. È S1-S2 (MVA di scalo con le
    regole delle ACC, già decise): la lettura giusta la fa la slice 15.
  Prova sull'albero (lo scrittore MVA è cambiato): round-trip 701/701, opache 93, tag su tutto 118 469, punti 38 330,
  blocchi 107 204, validatore 120 errori e 481 avvisi, il solo guasto noto di `limf.sid` — **tutto invariato**. Prova a
  schermo col banco (server del Lab su una copia del fork): tipo di `BC404` (diff −1 +1), piste di `OST1E`
  (`07` → `07:25`), quota della prima zona di `lirr.mva` (`FL90` → `90`, `LIRR` resta), casella CPDLC di
  `LIMM_WS2_CTR` (anche in `limm.frq`), `libd.mva` in sola lettura; la nota «= 10 000 ft» usciva dalla colonna stretta,
  sistemata nel foglio. Test: motore 616 → **618**, Lab 376 → **408**.
- **3c (28 settembre)** — il punto coi suggerimenti (A2). `Components/CampoPunto.razor`: mentre si scrive, il Lab
  propone i nomi che il **master scelto** conosce (quelli che Aurora risolverebbe: fix, VOR, NDB, scali, VRP), filtrati
  man mano nel Lab e non nella pagina (`CatalogoDeiPunti.Suggerisci`: prima quelli che cominciano col testo, poi quelli
  che lo contengono, al più 20 — i nomi di un master sono ~4 400). Lo usano i campi `Punto` (la posizione di un'attesa
  di `HOLDENR.hold`, che prima non si scriveva: scelto un nome esce `HLD-ABBOZ;BC404;BC404;…`, A2), `Navaid` (navaid
  della transizione delle SID, nome dell'etichetta L) e i **vertici** dei file che sanno scrivere un nome (`.tfl`,
  `.mva`, SID, `.str`…; non i `.pol` né le aerovie, che tengono solo coordinate). La lettura del punto è una sola, la
  stessa dei vertici (`ModificheInSospeso.LeggiIlPunto`).
  - 🔴 **Trovato a schermo, codice comune (`FusioneDelRecord.UnisciCampi`)**: scritto il navaid di `OST1E` la riga
    diventava `LIRF;07;OST1E;;;;OSTE;;` — **l'RNAV `1` dell'8° campo spariva**. Il `;` di chiusura lasciava un pezzo
    vuoto che contava come campo: una riga nuova più lunga della base lo metteva sopra il campo che il modello non
    conosce; togliendo il navaid, l'RNAV scivolava nel 7° campo (diventava un navaid); con una grezza più corta un
    campo nuovo finiva un posto prima. Ora i campi si contano senza il terminatore, un campo tolto tiene il suo posto
    vuoto se dopo ci sono campi sconosciuti, i campi mancanti alla grezza entrano solo se servono a tenere il posto.
    Rosso: 3 test su 3 sul codice di prima. Misura su **ogni** SID del fork (navaid messo o tolto): cambia solo il 7°
    campo (e il 6° vuoto dove la riga finiva prima).
  - Misura nuova, **ogni campo scrivibile** di ogni tipo su tre record del fork, per la stessa strada della scheda:
    99 campi su 106 cambiavano la riga. Gli altri 7 non arrivavano mai nel file: transizione e RNAV degli `.str` (lo
    scrittore si ferma al 6° campo) → **sola lettura** fino alla slice 9; nome e testo di un'etichetta L contano solo
    nel loro modo («Cosa mostra») → si scrivono solo lì (`DescrizioneDelCampo.SiScriveSe`). Dopo: **99 su 99**. Regola
    (scelta dell'agente): la scheda non offre mai una scrittura che non arriva nella riga.
  Prova sull'albero (fusione cambiata): tutto invariato — round-trip 701/701, **tutto toccato 0 righe cambiate**, **una
  modifica per record 115 568/115 568, 0 righe con più di un campo cambiato**, tag, punti, blocchi, validatore 120/481.
  Prova a schermo sul banco: suggerimenti veri di ITALY.isc (`OST` → OST · ndb, OSTEG · fix, OSTIA · vrp…), navaid di
  `OST1E` con l'RNAV che resta, posizione di `HLD-ABBOZ` per nome. 🟡 Scegliere dalla tendina con la tastiera nel
  browser del pannello non passa (la tendina nativa non c'è): da provare nella finestra vera del Lab (WebView2).
  Test: motore 618 → **621**, Lab 408 → **418**.
- **3d (28 settembre)** — i metadati di §M nella scheda. Sotto i campi, la sezione **«Metadati»**: le chiavi del
  catalogo del tipo di file (`CatalogoDeiTag`, il contratto con vIPI) appaiono da sole, col nome dell'AOD e il
  significato del catalogo di §M (`Ispezione/MetadatiDellaScheda.cs`); per le piste anche quelle per verso col numero
  davanti (`16L.tora`, «TORA 16L»); le chiavi che il record ha già si vedono anche fuori catalogo. `locked`, `whole`,
  `vfronly` sono caselle (`si` o niente). Un valore scritto va nel tag `//@"NOME" …` sopra il record (`Metadati.Scrivi`,
  fra virgolette solo se serve: `initialclimb="COO APP"`), un valore vuoto toglie la chiave e, senza chiavi, il tag.
  Voce nuova `ModificaDelMetadato` (una per chiave: «initialclimb: — → 6000ft»), si annulla, segue i record che si
  spostano, rientra nella storia. Non si scrivono dalla scheda: `gen` (la scrive il generatore, F8), `compose`/`whole`
  (nelle mappe `.str` c'è già «Composta da»; nei confini J6, F8), le chiavi fuori catalogo, e i record **senza un nome
  loro** (segmenti `.geo`, poligoni `.pol`: il nome del blocco lo sceglie chi scrive, slice 6) — ognuno col perché.
  Misura sul fork: in **688 file** con un catalogo, tre record per file, **1 271 record su 1 271** — messa una nota,
  si aggiungono solo righe di tag, il file riletto non ha tag rotti, tolta torna identico; 669 record senza nome
  proprio. Prova a schermo sul banco: `EKLO8R` con `fix=EKLOS` e `initialclimb="COO APP"` (diff −0 +3:
  dichiarazione, `//@START`, `//@END`), righe del tag col lucchetto, annulla. Test: Lab 418 → **428** (motore invariato).
- **3e (28 settembre)** — «+ Nuovo record» chiede il tipo fisso prima di tutto (A1). `Ispezione/TipoDelNuovo.cs`: il
  «tipo» di ogni tipo di record è il suo campo a tipo fisso principale — quello che in Aurora sceglie strato, filtro o
  tasto: tipo del fix, del VOR, del punto VFR, dello scalo, della SID, della voce `.str`, del segmento `.geo`,
  riempimento del `.pol` — e negli `.artcc` la FORMA: **etichetta (L) o traccia (T)**, col significato. Il menu è la
  prima cosa del gesto; negli `.artcc` va scelto per forza («Aggiungi» spento finché non si sceglie), negli altri parte
  dal tipo del record che si copia (il gesto di oggi non si allunga; scelta dell'agente). Il vuoto «non scritto» e il
  tipo fuori manuale dell'`.ap` non si propongono. Quando il tipo è un campo si copia il record scelto e il nuovo prende
  il tipo; quando è la forma si copia un record di quel tipo (lo scelto se lo è, se no l'ultimo del file), e il nuovo va
  comunque sotto quello scelto. NDB, attese, settori e gli altri nascono come prima, senza domanda. Visto a schermo: le
  etichette L degli `.artcc` nell'elenco si chiamavano tutte «LabelPoint» — ora col nome del fix (`FixRef`).
  Misura sul fork: in **468 file** che chiedono un tipo, un record nuovo per **ogni** tipo possibile, **3 290 su 3 290**
  riletti col record in più e di quel tipo (`.geo` 1 572, `.pol` 658, `.str` 540, `.vfi` 308, `.sid` 118…). Prova a
  schermo sul banco: `FRA.artcc` → «+ Nuovo record» → «Etichetta o traccia?», L → `L;XOLTA;…` in fondo, −0 +1.
  Test: Lab 428 → **438**.

**Slice 3 chiusa.** Uscita misurata sul fork: **ogni tipo di record si apre senza «campo sconosciuto»** (701 file, 118 505
record, 24 tipi) e **la scheda scrive la stessa riga del motore**: ogni campo che offre di scrivere arriva nella riga
(99 su 99, stessa strada della scheda), e dove il motore scriveva male — la quota delle MVA di ACC, l'RNAV delle SID —
ora è corretto e misurato su tutto il fork. Restano in sola lettura, col perché scritto nella scheda: quota e carattere
delle MVA di scalo (slice 15), transizione e RNAV degli `.str` (slice 9), i metadati dei record senza nome (slice 6).
Da provare a mano (eseguibile ripubblicato): prove 62-70 in `SectorLab-prova\PROVE.md`.

**Slice 4 — colori.** Divisa in passi, un commit ciascuno (scelta dell'agente): **4a** i colori nel motore (le forme
del manuale, `colors.def`, gli schemi `.clr` di Aurora) · **4b** la mappa coi colori dello schema scelto · **4c** il
selettore nella scheda.

- **4a (28 settembre)** — i colori nel motore (codice comune, `Vipi.Sectorfile`). Dal manuale IVAO del sector
  («Colour Definitions», letto il 28 settembre): un colore si scrive `#RRGGBB`, `#AARRGGBB` (da Aurora 1.4.1,
  l'opacità si vede solo con *Smooth Drawing*), `R,G,B` o `%R:G:B`, e **ogni forma vale anche in `[DEFINE]`**.
  - `Shared/ColoreDelSector.cs` (nuovo): legge le quattro forme e dice quale (`FormaDelColore`); scrive `#RRGGBB`
    maiuscolo, `#AARRGGBB` solo se il colore non è opaco. `DefParser` leggeva solo `#RRGGBB`: ora tutte e quattro
    (rosso: col `DefParser` di prima 4 test su 10 cadono).
  - `IO/Parsers/ClrParser.cs` + `Shared/SchemaDeiColori.cs` (nuovi): gli schemi di Aurora, `CHIAVE=valore`. Il manuale
    **non descrive** la notazione, quindi la regola è misurata sugli schemi del fork: `$00BBGGRR` (byte alto a zero)
    è il `TColor` di Delphi, rosso nel byte basso; `$AARRGGBB` (byte alto diverso da zero) è opacità e poi RGB — le
    coppie dello stesso schema danno lo stesso colore (`ITALY_GND`: `DANGER=$00963CAE` = `SPEC_DANGER=$FFAE3C96`;
    `LIRR_RDR_V1.0`: `ARTCC=$00F06E90` = `COAST=$FF906EF0`, `PROHIBITED=$00006DFF` = `SPEC_PROHIBITED=$FFFF6D00`). I
    nomi di Delphi (`clWhite`, `clLime`…) dalla VCL, `clNone` = non si disegna; il resto (`ACC_SOLID=0`,
    `VORSYMBOL=«`) sono impostazioni, tenute come testo.
  - `Shared/NomiDeiColoriDelGeo.cs` (nuovo): i nomi che un `.geo` usa senza definirli (manuale: APRON, BUILDING,
    COAST, DANGER, PIER, PROHIBIT, RESTRICT, RUNWAY, STOPBAR, TAXI_CENTER, TAXIWAY, più `APPRON`, `STOPLINE` «ancora
    accettati» e `PARKING` dell'esempio) → la chiave dello schema (`TAXI_CENTER` → `TAXIWAYCENTER`, `STOPLINE` →
    `STOPBAR`, `PROHIBIT` → `PROHIBITED`…).
  - Misura nuova nella prova sull'albero, **COLORI**: 6 schemi (4 in `ColorSchemes\`, `Default.clr`, `PAR2090.clr`)
    letti con **0 avvisi**, ognuno dei 4 schemi radar ha tutte le chiavi del `.geo`; 16 nomi in `colors.def`;
    **107 806 colori scritti nell'albero**, di cui 50 666 nomi di `colors.def`, 57 132 nomi che colora lo schema
    (`TAXI_CENTER` 23 771, `COAST` 13 560, `PROHIBIT` 10 143, `PIER` 6 207…), 6 valori `#RRGGBB` nei `.tfl`, e **2
    sconosciuti**: `COAST` come riempimento e bordo dell'orfano `limw.pol` (già noto dalla 3a). Il resto della prova
    invariato: 701/701, opache 93, tutto toccato 0, una modifica per record 115 568/115 568, tag 118 469, punti
    38 330, blocchi 107 204, validatore 120/481, il solo guasto noto di `limf.sid`.
  - `TAXIWAY`, `BUILDING`, `RUNWAY`, `APRON`, `STOPBAR` sono **sia** in `colors.def` **sia** nomi che lo schema
    colora da sé (nei `.geo` 46 800 segmenti). **Committente, 28 settembre**: in Aurora i bordi taxiway di un `.geo`
    sono gialli → nelle linee dei `.geo` vince lo schema; `colors.def` vale per i riempimenti di `.pol` e `.tfl`.
  Test: motore 621 → **667** (net8 e net10), Lab **438**.
- **4b (28 settembre)** — la mappa coi colori di Aurora (D3; solo Lab, nessun codice comune).
  - **Lo schema** si sceglie una volta fra i `.clr` di `ColorSchemes\` accanto a `SectorFiles` (fuori dal sector: così è
    nel repository del sector e in un'installazione di Aurora); di base `LIRR_RDR_V1.0.clr`, lo schema del committente.
    Scelta e modo si ricordano fra un avvio e l'altro (`colori-della-mappa.txt` nella cartella dei dati del Lab). I nomi
    di `[DEFINE]` sono quelli del master scelto (`colors.def` di `ITALY.isc`): cambiando master si ricalcolano.
    Nella colonna degli strati, sotto «Colori»: **di Aurora** / **del Lab** (quelli di prima, uno per strato) e lo
    schema. Senza `ColorSchemes` la mappa resta coi colori del Lab e lo dice.
  - **La regola** (`Mappa/ColoriDellaMappa.cs`): i record senza un colore scritto prendono la chiave dello schema del
    loro file (`.lairway` → `AIRWAYLOW`, `.hartcc` → `ARTCCHIGH`, `.mva` → `MRVA`, `.fix` → `FIX`, `.gts` → `GATES`…)
    o della voce (`.str`: STAR, IAP, FAP, GOAROUND, TRANSITIONS, HOLDINGS dal 6° campo); nelle linee dei `.geo` e
    delle aree P/R/D vince lo schema, poi `colors.def`, poi il valore; nelle teste di `.tfl` e `.pol` `colors.def`,
    poi il valore (lo schema lì non conta); un nome che nessuno conosce è **magenta**, come la riserva di
    `ColorPalette`. `clNone` non si disegna. **Settori dinamici solo bordo**, e così uno statico con l'opacità a 1
    (manuale: «FILLCOLOR CLEAR»); i riempimenti dei `.pol` pieni, con l'opacità di `#AARRGGBB` se c'è. Il fondo della
    mappa è `RADARBACK`. Lo stile delle linee viene dalle impostazioni `…_SOLID` dello schema (1 tratteggio, 2
    puntini, 3 tratto-punto: i `PenStyle` di Delphi, 🟡 da confrontare con Aurora — in `LIRR_RDR_V1.0` sono a
    tratto-punto gli ARTCC alti e bassi, a puntini MVA e attese).
  - I segmenti dei `.geo` si cuciono solo se hanno anche lo **stesso colore** (prima bastava il nome dell'area).
  - I colori viaggiano con le forme (`k`/`ka` linea, `g`/`ga` riempimento, `s` stile): «di Aurora» ↔ «del Lab» non
    riprende le coordinate; cambiare schema sì.
  - 🔴 **Trovato a schermo**: i riempimenti di terra vanno SOTTO le linee dei `.geo` (pieni coprirebbero bordi e assi);
    portandoli in fondo forma per forma il loro ordine si **rovesciava**, e l'erba del confine di LIRF copriva taxiway e
    piste. Ora si portano in fondo dall'ultima alla prima: vince l'ultima del file, come in Aurora (I3).
  - Misura sul fork (strumento fuori repo, `scratchpad/colori4b`, schema `LIRR_RDR_V1.0`, master `ITALY.isc`): **21 853
    forme**, tutte con un colore dello schema o di `colors.def` salvo **3** già note, magenta: i 2 tipi vuoti di
    `liap.geo` (H2) e il `COAST` dell'orfano `limw.pol`; 1 753 riempite (tutti i `.pol`, nessun `.tfl`), 182 solo bordo,
    682 tratteggiate. Per strato: coste `COAST`; `.geo` RUNWAY 3 185, PIER 1 947, TAXIWAYCENTER 1 487…; settori ARTCC
    102, TWR/APP/CTR/MIL da `colors.def`, 3 valori; aree PROHIBITED 284, RESTRICTED 198, DANGER 55; terra GATES 1 674,
    TAXILABELS 1 075 e i riempimenti di `colors.def`.
  - Prova a schermo sul banco (server del Lab su una copia del fork con `ColorSchemes`): fondo `#040404`, coste
    viola-blu, settori, MVA a puntini e aerovie; LIRF con erba, taxiway e piazzali grigi, bordi gialli sopra; «del Lab»
    → fondo e colori di prima, «di Aurora» → di nuovo; `Default.clr` → coste arancioni e taxiway verdi, come i suoi
    valori.
  Test: Lab 438 → **462**, motore **667**.
- **4c (28 settembre)** — il selettore dei colori nella scheda (D2, I1; solo Lab). `Components/CampoColore.razor` per
  i campi `Colore` (riempimento e bordo dei `.tfl` e dei `.pol`): il **campione** del colore, il testo com'è scritto
  (ogni forma del manuale), il **selettore** di Windows (scrive `#RRGGBB` maiuscolo), l'**opacità** in % (sotto 100
  scrive `#AARRGGBB`; spenta su un nome, che ha l'opacità del suo `.def`), e sotto **i nomi di `colors.def` del
  master** come tasti col loro colore — nei `.pol` anche col significato (GRASS erba, HOLE buco…). Avvisi accanto al
  campo: `#AARRGGBB` (anche un nome definito così) → «l'opacità si vede solo con Smooth Drawing acceso (PVD → OTHER →
  Smooth Drawing)», dal manuale; un nome che `colors.def` non ha né un colore → «Aurora non sa come disegnarlo».
  `Ispezione/ColoriDellaScheda.cs` legge e scrive; i nomi sono quelli del master scelto (`SessioneDelLab.Definiti`).
  Scelte dell'agente:
  - Riempimento e bordo dei `.pol` passano da tipo fisso a colore (I1: «da `colors.def` o col selettore»), ma tengono
    i loro valori col significato: sono anche il tipo che «+ Nuovo record» chiede (3e), che non cambia.
  - In `[FILLCOLOR]` contano i nomi di `colors.def`, non lo schema (come sulla mappa): il `COAST` di `limw.pol` è
    detto sconosciuto.
  Misura sul fork (`scratchpad/colori4b`): **3 870 campi colore** nelle teste di `.tfl` e `.pol`, **3 862 nomi** di
  `colors.def`, 6 valori, **2 sconosciuti** (il `COAST` di `limw.pol`, riempimento e bordo); nessuna opacità. La
  scrittura passa dallo stesso `CambiaCampo` misurato nella 3c (ogni campo scrivibile arriva nella riga).
  Prova a schermo sul banco: `aa_ad_gnd.pol` GRASS → tasto TAXIWAY → «Riempimento: GRASS → TAXIWAY», diff −1 +1;
  `lirr_ne_ctr.tfl` bordo `CTR` → selettore `#FF8800` → opacità 60 → `LIRR_NE_CTR;CTR;1;#99FF8800;1;`, una voce,
  l'avviso su Smooth Drawing; «Annulla tutto». Nella colonna stretta la riga del colore andava oltre il bordo: ora va
  a capo. Test: Lab 462 → **479** (un test della 3a aggiornato: anche un colore può avere valori).

**Slice 4 chiusa.** Uscita misurata sul fork: ogni forma della mappa (21 853) ha il colore dello schema scelto o di
`colors.def`, salvo 3 già note che si vedono magenta; settori dinamici solo bordo; il selettore scrive nomi, `#RRGGBB`
e `#AARRGGBB` con l'avviso. Il confronto **accanto a uno schermo di Aurora** (ARTCC, aerovie, SID/STAR, fix, costa,
terra, e gli stili di linea `…_SOLID`) è del committente: prove 71-76 in `SectorLab-prova\PROVE.md`.
- **4d (28 settembre, chiesta dal committente dopo la chiusura)** — i punti coi simboli del sector, «così appare proprio
  come in Aurora». Committente: i simboli **dal `.sym` del sector** (`[SYMBOLS]`, `symbols.sym`), **non dai profili**.
  Anticipa dalla slice 17 il solo **lettore** del `.sym` (l'editor a pixel T1 e i controlli T2 restano là).
  - Motore (codice comune): `IO/Parsers/SymParser.cs` + `Shared/SimboloDelSector.cs`, solo lettura come `.def` e
    `.clr`. Un simbolo per riga, 13 gruppi di 13 cifre = **13 colonne** da sinistra, una cifra un pixel dall'alto
    (carta «file per file» §20: così il FIX punta in alto); il nome è l'ultimo commento sopra (`////ALTRI` è un
    titolo: il commento dopo lo sostituisce), o la riga di testo senza `//` (`AC_comb SEL`, `AC_DUPE`, segnati);
    numero = posizione nel file; una riga di pixel che non è 13×13 avvisa e si salta. Prova sull'albero, misura
    nuova in COLORI: **23 simboli**, 0 non 13×13, 1 senza nome (il 19°, dopo CROCE X), 2 col nome senza `//`.
  - Lab: `Mappa/SimboliDellaMappa.cs`. 🟡 **Quale simbolo per quale punto il sector non lo dice** (Aurora li sceglie
    per numero, T3 da provare): abbinamento **per nome, proposta dell'agente**, da confermare accanto ad Aurora — fix
    in rotta `FIX vuoto`, terminale `TERM`, in rotta e terminale `FIX pieno`, nascosto `FIX vuoto piccolo` (in Aurora
    non si vede; qui piccolo); VOR `VOR`, VOR/DME e DME `VOR2`, VORTAC `VOR3`, TACAN `TAC2`; NDB `NDB`; scali `APT`. **Punti VFR**: il committente, con uno
    schermo di Aurora, li vede come un piccolo **rombo pieno** che nel `.sym` non c'è — è il simbolo di Aurora
    (`SYMBOLS_FIX_VFR`, uguale in tutti i 35 profili misurati): il Lab lo tiene scritto (`VfrDiAurora`), in coda ai
    simboli del sector (🟡 i tipi 2 elicotteri e 3 area hanno in Aurora simboli loro; sul fork nessun punto li usa). Un nome che il file non ha → il punto resta un cerchio. Il `.sym` è quello che carica il master
    scelto. Ogni punto porta il suo tipo (`FormaDellaMappa.Punto`: `FIX:1`, `VOR:2`…), il JSON l'indice del simbolo
    (`y`), i pixel arrivano una volta coi colori.
  - Mappa: **coi colori di Aurora** ogni punto è il suo simbolo, pixel per pixel e nel colore dello schema (una tela
    13×13 per simbolo e colore, copiata per ogni punto: 4 000 fix a pixel singoli sarebbero 100 000 rettangoli a ogni
    spostamento); coi colori del Lab restano i cerchi. Clic, evidenza e strati come prima.
  - Misura sul fork (`scratchpad/colori4b`): **4 925 punti, tutti col loro simbolo** — fix nascosti 2 556, TERM 575,
    in rotta 454, in rotta e terminale 316, VFR 586, scali 295, VOR 95, TACAN 17, VORTAC 9, NDB 27.
  - Prova a schermo sul banco: intorno a OST triangoli, quadrati, simboli di VOR e NDB sul fondo radar; clic su OST
    (NDB) lo sceglie; «del Lab» → cerchi, «di Aurora» → simboli; console pulita.
  Committente, dopo la prova: «tutto pare ok, tranne i punti VFR» → rombo di Aurora, rivisto sul banco (PONTE
  GALERIA e dintorni come nello schermo di Aurora). Test: motore 667 → **670**, Lab 479 → **497**.

**Dalle prove 53-77 del committente (28 settembre).** Tutte ✅; tre richieste, fatte prima della slice 5:

- **Prova 68, i metadati di SID e STAR con l'editor giusto** (`Ispezione/ValoriDeiMetadati.cs`, `EditorDelMetadato`):
  **fix intero** e **transizione** sono punti del sector — il campo coi suggerimenti del master (`CampoPunto`), un nome
  che il master non conosce si rifiuta col perché; **salita iniziale** in piedi o FL (`6000`, `2500'` → `6000ft`,
  `2500ft`; `fl 100` → `FL100`; FL 1-660, piedi fino a 66 000) oppure la **spunta «COO APP»**, che scrive sempre
  `initialclimb="COO APP"` (niente refusi); **categorie di scia** L M H S e **Vref** A B C D E come tasti, scritte in
  ordine (`wtc=LMH`): un clic accende o spegne, il **doppio clic** porta le precedenti allo stato di quella cliccata
  (doppio clic su H: L, M, H accese; di nuovo: spente, S com'era). 🔴 Trovato a schermo: il secondo clic del doppio
  clic arriva prima che la scheda si ridisegni, e col valore di prima annullava il primo — il tasto rilegge il valore
  dalla sessione (bUnit non riproduce la corsa: provato col doppio clic vero nel browser del pannello).
  «Specifica di navigazione» (`nav`, P11 e Q2b): quale navigazione chiede la procedura (RNAV1, RNP1, RNP APCH); resta
  testo, i valori arrivano con l'import dall'AIP (F7).
- **Cerca anche i file per nome** (`Ricerca.CercaFile`): il nome con o senza estensione, poi il percorso
  (`GND_LAYOUT\rf`), anche i file tenuti come testo e gli `.isc`; in cima ai risultati, un clic apre il file nell'albero.
- **Prova 57, COLOMBO e VFR_NASCOSTI**: sì, la correzione di un punto `.vfi` va portata al suo gemello di
  `VFR_NASCOSTI.fix` (COLOMBO ↔ RFS3, chiave = il codice) — è la voce F2 della **slice 8** (gemello `.vfi` ↔
  `VFR_NASCOSTI.fix`: 496 uguali, 9 diversi, 81 senza, 7 orfani); oggi il Lab non lo sa.
  Test: Lab 497 → **534**.

**Slice 5 — sequenze di punti e gesti sul record.** Divisa in passi, un commit ciascuno (scelta dell'agente): **5a** le
sequenze su tutti i file e «inverti» · **5b** spezza/unisci · **5c** nascondi/mostra · **5d** la vista a linea dei `.geo`.
Misure di partenza (fork, testo): 28 `BREAK` attivi e 2 commentati nelle aerovie; `DUMMY` negli `.artcc` 101 in coda,
11 in mezzo, 2 in testa; nei `.hartcc` 22 in coda e 1 in mezzo; nei `.lartcc` 10 in coda e **19 in mezzo** (i
`T;dummy;INLER;INLER;` delle mappe STAR in `limc_star`/`lirf_star`, per nome); nelle MVA **169 tutti in coda** (nessuno
in mezzo a una zona); 1 973 `<br>` negli `.str`; nelle SID 2 righe vuote fra due punti (`lied.sid`); 42 righe vuote fra
due segmenti `.geo`; 169 righe vuote fra due righe di dati nelle MVA di scalo.

- **5a (28 settembre)** — le sequenze di punti su tutti i file (B7). Tre elenchi che la scheda mostrava in sola lettura
  diventano elenchi di vertici come gli altri (`ElenchiDiVertici`): il **tracciato delle aerovie** (le righe `T`,
  `Airway.FixLabels`: solo nomi, uno per punto — coordinate e due nomi diversi si rifiutano col perché), i **vertici
  delle MVA** (`MvaVertex`: il 5° campo resta quello del vertice, e un vertice nuovo prende il gruppo dei vicini, E3) e
  quelli dei **confini** `.artcc`/`.hartcc`/`.lartcc` (`StaticBoundaryVertex`, un elenco per poligono, «Poligono 2»,
  per coordinate o per nome). Sotto ogni elenco due tasti: **«+ in fondo»** (una copia dell'ultimo punto) e **«⇅
  Inverti»**. Inverti: quel che ogni punto porta (etichetta di una SID, suffisso `4E` di un `.str`, gruppo di una T)
  resta col suo punto; le interruzioni stanno FRA due punti e restano fra quegli stessi due (la riga vuota di una SID, il
  `<br>` di un `.str` passano all'altro punto della coppia; il segno sul primo punto resta sul primo). Inverti due volte
  = nessuna modifica (il confronto con l'apertura ora guarda anche le righe che le voci scrivono, `Firma`). Le voci
  dicono il nome dell'elenco come la scheda («Poligono 1: vertice spostato», non `Polygons[0].Vertices`); un elenco
  vuoto accanto a uno pieno (le righe L di un'aerovia fatta di righe T) non si mostra. Scelte dell'agente e trovati:
  - 🔴 **Codice comune, `MvaSaver`**: nelle zone di scalo senza riga L (`T;CERCHIO-BA;…`, `liba.mva`) lo scrittore
    metteva la quota (vuota) nel 2° campo: un vertice aggiunto usciva `T;;N…;E…;`, un'altra zona per Aurora. Ora il
    nome della zona (`MvaSector.Nome`). Rosso sullo scrittore di prima: 1 test su 14 del Lab, e un test nuovo nel motore.
  - 🔴 **Trovato dalla misura: girare le righe porta con sé quelle che lo scrittore non produce**, attaccate alla riga
    sotto: i separatori `T;dummy;INLER;INLER;` dei `.lartcc` finivano dentro un altro poligono. Inverti ora rilegge il
    file com'uscirebbe e, se la sequenza riletta non è quella girata, **non si fa** e dice perché (14 elenchi sul fork:
    `limc_star`, `lirf_star`, e la zona 12 di `licj.mva`). Rosso: senza la rilettura il test cade.
  - Trovato dalla misura: «+» copiava il punto passando dal testo, e un nome con gli spazi (`FOCI DEL FORTORE` dei
    `.vrt`) si leggeva come coordinate sbagliate → la copia prende il punto com'è. 🟡 Resta che un nome con gli spazi
    non si può **scrivere** in un vertice (`LeggiIlPunto` divide sugli spazi): da vedere con la slice 16 (VFR).
  - 🟡 Dato da correggere (R-10): `licj.mva`, zona 12 — due righe L di nomi diversi (`85TRS`, `85TPS`) e le T col
    secondo: l'unica zona su 334 del fork dove il nome delle T non è quello della prima riga.
  Misura sul fork (strumento fuori repo, `scratchpad/misura5`): **2 522 elenchi in 278 file** (ogni record di aerovie,
  MVA e confini, cinque per file negli altri), per ognuno «cambia il primo», «in fondo» e «inverti», il file riletto dal
  motore e poi annullato: **0 guasti** — cambia 2 522/2 522 (diff −1 +1), in fondo 2 522/2 522 (−0 +1), inverti 2 063
  riletti giusti, 14 rifiutati col perché, 440 con un punto solo, 5 uguali al contrario; annullare torna sempre al file.
  Per tipo: tracciati di aerovia 302, vertici MVA 317, poligoni di confine 211, `.pol` 439, zone `.str` 365, procedure
  `.str` 224. Prova sull'albero (lo scrittore MVA è cambiato): tutto invariato — 701/701, opache 93, tutto toccato 0,
  una modifica per record 115 568, tag 118 469, punti 38 330, blocchi 107 204, 120/481, il solo `limf.sid`. Prova a
  schermo sul banco: L613 invertita (−4 +4) e «+ in fondo»; `RR CONF1` di `lirr.hartcc`, TIPNI → OTNUN per nome
  (`T;RR CONF1;OTNUN;OTNUN;`); `CERCHIO-BA` «+ in fondo» (`T;CERCHIO-BA;…`, «Vertici: vertice aggiunto (73 → 74)»);
  «Annulla tutto». Test: motore 670 → **671**, Lab 534 → **548**.
- **5b (28 settembre)** — spezza e unisci (B6, R-3, E4; solo Lab, nessun codice comune). «Spezza dopo questo punto»
  (✂ su ogni punto) toglie il pezzo di linea fra un punto e il prossimo; «Unisci» lo rimette. Cinque scritture, una per
  famiglia (`Modifiche/Interruzioni.cs`): **riga vuota** nelle SID e nelle MVA di scalo, **`<br>`** nel 3° campo del
  punto che apre il tratto negli `.str` (procedure e zone), **`T;DUMMY;…`** nei confini, **`T;BREAK;PUNTO;PUNTO;`**
  nelle aerovie. Scelte dell'agente:
  - **Il gesto si fa sul testo** del file com'è adesso (modifiche comprese) e il motore rilegge il file: un'aerovia
    spezzata diventa davvero tre record (pezzo, `BREAK`, pezzo) come la legge Aurora, e spezzare e riunire torna al file
    di prima byte per byte. La voce è quella del testo del file, col nome del gesto («L615: riunita dopo VADIK»), e si
    annulla con lui; se il testo torna quello dell'apertura la voce sparisce. `IFileConRecord.PostiDeiRecord` dice dove
    sta ogni record nelle righe di adesso (anche un record già toccato); `CambiaRighe` prende il nome del gesto e un
    controllo sul file riletto: se il file non ha i record che il gesto voleva, o l'interruzione non si vede dove l'AOD
    l'ha messa, non si scrive niente e si dice perché.
  - Le scritture come nei file: il separatore ripete il punto di prima (`T;BREAK;RIVAM;RIVAM;` come in `itawlow`,
    `T;DUMMY;TIPNI;TIPNI;`); il `<br>` si chiude o no col `;` come fa il file (`lirf.str` `…;<br>;`); su un punto col
    suffisso (`4E`) il punto si ripete, una riga col `<br>` e una col suffisso (come `lime.str`), e riunire toglie la
    copia. Mai un commento in coda: riunito e rispezzato, `T;BREAK;VADIK;VADIK; //discontinuity…` torna senza commento.
  - Dove non si spezza: `.pol`, `.tfl`, `.vrt`, le righe L; **nelle MVA di ACC** il `T;DUMMY` chiude la zona (sul fork
    169 in coda, nessuno in mezzo) e il motore non lo tiene in mezzo: il Lab lo dice (slice 15).
  - Nella scheda: fra due punti «interruzione · riga vuota [Unisci]»; in fondo all'elenco «interruzione · riga T;BREAK
    — continua in L615, dopo il BREAK (record 31) [Unisci]» (così anche il poligono dopo, il tratto dopo, la zona di
    scalo dopo la riga vuota con lo stesso nome).
  - Trovati dalla misura: 84 punti `.str` con un commento in coda nella riga dopo → spezzare lì si rifiuta («spostalo
    sopra, poi spezza», slice 2a); la zona 12 di `licj.mva` (già nota dalla 5a) spezzata non si vede come spezzata →
    rifiutato; `itawlow.lairway:187` (L613), commento su una riga sua sopra il `BREAK`: tolto il `BREAK` i pezzi restano
    due per il Lab → unire si rifiuta. 🟡 Per Aurora quel commento spezza l'aerovia? È la stessa domanda della 2a
    (commento dentro un tratto).
  Misura sul fork (strumento fuori repo, `scratchpad/misura5b`): **ogni interruzione che c'è** — 1 776 `<br>`, 37
  `DUMMY` fra due poligoni, 28 `BREAK`, 5 righe vuote — riunita e rispezzata: 1 845 su 1 846 riunite (L613 rifiutata col
  perché), rispezzate **1 383 identiche** byte per byte e 461 la stessa linea scritta come il Lab (il `DUMMY` dei
  confini sul punto prima invece che su un punto qualsiasi, il `BREAK` senza commento in coda, il `<br>` nella forma del
  file dove il file ne ha due); **spezza e riunisci su tre punti di ogni elenco** che si spezza: **5 762 su 5 762
  tornano al file di prima, senza voce** (3 985 `<br>`, 582 `BREAK`, 554 `DUMMY`, 641 righe vuote), 87 rifiutati col
  perché; annullare torna sempre al file. Prova a schermo sul banco: L615 «Unisci» (−1, «L615: riunita dopo VADIK»), ✂
  dopo VADIK (`T;BREAK;VADIK;VADIK;`); NORTH DEP16 di `lied.sid`: la riga «interruzione · riga vuota», «Unisci», la SID
  sulla mappa diventa una linea sola e le forbici tornano sul punto 6; «Annulla tutto». Test: Lab 548 → **560**
  (motore invariato, 671).
- **5c (28 settembre)** — nascondi e mostra (B3, R-5, K4; solo Lab, nessun codice comune). «◐ Nascondi» mette `//`
  davanti a ogni riga di dati del record; «◑ Mostra» li toglie. Per Aurora un record nascosto non c'è; nel Lab resta
  nell'elenco, **grigio** e in corsivo, al suo posto. Come spezza e unisci il gesto si fa sul testo (una voce: «MM CONF 4:
  mostrato»), il motore rilegge, e un controllo sul file riletto decide se il gesto ha fatto quel che doveva.
  - **Come il Lab trova i nascosti** (`Modifiche/Nascosti.cs`, `Analizza`): il motore i record commentati non li vede
    (sono commenti). Il Lab toglie i `//` a tutte le righe commentate **che hanno dati** (un `;`: i titoli `//SARDEGNA`,
    `//discontinuity…` e i tag `//@` no), rilegge il file UNA volta, e guarda ogni record che ne esce. Tre casi, misurati
    sul fork: **nascosto fra gli altri** (tutte le sue righe erano commentate: le SID di `lipb.sid`, «MM CONF 4» di
    `limm_tma`, le procedure di `limw.str`, che per il motore stavano dentro la procedura prima — negli `.str` un
    commento non chiude il record); **nascosto dentro** (il motore lo tiene anche commentato: nei file a una riga per
    record una riga commentata è un record disattivato, `//LIRR;0;0;…; Roma Area;` di `itap.ap`; una zona MVA tutta
    commentata); **in parte** (un record attivo con qualche riga commentata: l'etichetta `//L;LIRR;…; //NAPOLI CTA`, le
    `//T;LIMM;…` di `limm.mva`, i punti commentati delle mappe `.str`), che nell'elenco porta «◑ N» e nella scheda «◑
    Mostra le N righe nascoste». I nascosti del file si calcolano una volta e si tengono finché il suo testo non cambia.
  - Scelte dell'agente: nelle MVA nascondere lascia attivo il `T;DUMMY` (come le zone commentate del fork: chiude il
    blocco); un record coi metadati `//@` non si nasconde (il tag resterebbe senza record: prima si tolgono); gli `.atis`
    sono testi e non si nascondono; «Mostra» su un record nascosto dentro scommenta tutte le sue righe con dati (anche
    quelle che erano commentate prima di «Nascondi»: il Lab non sa quali fossero).
  Misura sul fork (strumento fuori repo, `scratchpad/misura5c`): **nascosti fra gli altri 98** (71 aerovie dell'archivio
  `itawhigh.hairway`, 11 SID, 6 righe dei `.frq`, 4 settori `.tfl`, 2 procedure `.str`, 2 piste, **«MM CONF 4» con le
  sue 274 righe**, K4, e una riga di `liml.txi`), **dentro 28** (12 `italy.restrict`, 8 `.ap`, 6 `.geo`, 2 `.gts`), **in parte 129 record** (5 zone
  MVA con **53 righe** — le 52 della carta —, 120 procedure `.str` con 541 righe, 2 settori, 2 confini `.hartcc`/`.lartcc`). Mostra e
  rinascondi ognuno: **98 + 28 su 98 + 28 tornano al file di prima**, senza voce; «mostra in parte» cambia solo quelle
  righe (128 su 129; `lime.str` #4 rifiutato col perché: scommentate le sue righe il file avrebbe un record in più).
  **Nascondi e rimostra tre record per file: 1 928 tornano al file di prima**, 9 scommentano anche le righe che il record
  aveva già nascoste (atteso), 3 (`limm_tma` #7, `limw.str` #0, `lipb.sid` #16) si fondono col nascosto che hanno accanto:
  scommentati sono un record solo, e «Mostra» li mostra insieme; 7 `.atis` rifiutati. Annullare torna sempre al file.
  Prova a schermo sul banco: `limm_tma.lartcc` — «6 MM CONF 3 ◑ 2», «· MM CONF 4 nascosto» grigio; scelto, l'ispettore
  dice «righe 1062-1349 commentate: per Aurora non c'è»; «Mostra» → −274 +274, MM CONF 4 diventa il record 8 e la scheda
  va su di lui; «Nascondi» → nessuna modifica, di nuovo grigio in fondo. Test: Lab 560 → **572**.
- **5d (28 settembre)** — la vista a linea dei `.geo` (G1, R-3; solo Lab). Nella scheda di un segmento `.geo` (anche
  `RW_MARKINGS` e aree P/R/D) la sezione **«Linea · N punti · N−1 segmenti»** (`Modifiche/LineeDelGeo.cs`,
  `Components/LineaDelGeoVista.razor`): i segmenti di fila nel file, **attaccati** (la fine di uno è l'inizio del
  prossimo), dello stesso tipo e della stessa area, **senza righe in mezzo** (una riga vuota o un commento chiude la
  linea, come dice la specifica dei `.geo`). Cambiare un punto riscrive **la fine del segmento prima e l'inizio di quello
  dopo** (due campi, due voci, un gesto solo nella storia): la catena non si rompe. ✂ su un punto in mezzo spezza la
  linea con una riga vuota prima del segmento che comincia lì; dove due linee attaccate sono staccate solo da righe vuote
  la sezione dice «riga vuota — dopo c'è la linea che comincia qui [Unisci]» (e «prima»), e unire toglie le righe vuote.
  Le linee lunghe (la costa di `itgeo.geo`, 4 523 segmenti) si vedono intorno al segmento scelto, 20 punti per parte; i
  due punti del segmento scelto sono segnati.
  Misura sul fork (strumento fuori repo, `scratchpad/misura5d`): **134 file, 9 503 linee, 103 942 segmenti** (3 046 linee
  di un segmento solo); su ogni linea di almeno tre segmenti il punto in mezzo spostato: **6 120 su 6 120** cambiano due
  righe e la linea resta intera; spezza e riunisci lì: **6 120 su 6 120 tornano al file di prima**, senza voce. Delle 42
  righe vuote fra due segmenti del fork **una sola** sta fra due linee attaccate (`lira.geo`, due righe vuote: riunita e
  rispezzata ne torna una); le altre separano forme diverse. Annullare torna sempre al file. Prova a schermo sul banco:
  `liap.geo`, «//fence» — «Linea · 20 punti · 19 segmenti», punti 3-4 segnati; il punto 4 spostato → «Fine: … → …» e
  «Inizio: … → …», −2 +2; ✂ al punto 11 → +1 riga vuota, la linea scende a 11 punti con «Unisci»; «Unisci» → nessuna
  modifica, di nuovo 20 punti. Test: Lab 572 → **579**.

**Slice 5 chiusa.** Uscita misurata sul fork: le sequenze di punti su tutti i file (2 522 elenchi: cambia, in fondo,
inverti riletti giusti); **spezzare e riunire torna al file di prima** in ogni scrittura (5 762 punti di SID, `.str`,
confini, aerovie, MVA di scalo; 6 120 linee `.geo`), le interruzioni che ci sono si riuniscono tutte salvo L613 (col
perché); **le 53 righe MVA commentate e le 274 di `limm_tma` si vedono grigie** (in parte e «MM CONF 4» nascosto), e
nascondi/mostra torna al file di prima. Da provare a mano (eseguibile ripubblicato): prove 82-89 in
`SectorLab-prova\PROVE.md`.

**Slice 6 — gruppi e blocchi come Aurora.** Divisa in passi, un commit ciascuno (scelta dell'agente): **6a** le voci della
selezione e le parti, accese e spente sulla mappa · **6b** il nome dal commento cambiato dalla scheda, e il nome mancante
(H3) · **6c** la regola commento/blocco di §M (un pezzo senza nome che riceve il primo dato passa a blocco).

- **6a (28 settembre)** — le voci come la finestra di selezione di Aurora (A3, J1, B1, E1 vista, H3 vista; solo Lab).
  Sotto il file, sopra l'elenco dei record, la sezione **«Voci · come la selezione di Aurora»**
  (`Ispezione/VociDellaSelezione.cs`, `Components/VociDelFile.razor`), calcolata sul testo com'è adesso:
  - **confini** (`.artcc`, `.hartcc`, `.lartcc`): una voce per nome del gruppo T, anche in più blocchi (J1: `RR CONF2`,
    FRA BDRY); le parti sono i **poligoni** (fra i `T;DUMMY`), col nome dal **commento subito sopra** il primo punto — fra
    più commenti di fila l'ultimo corto che non è un titolo di sole barre (`// LINPZ1 VEKEN`, non la descrizione sotto) —
    o dal **commento in coda al `T;DUMMY`** che lo precede (`T;DUMMY;…; //brindisi/tirana`). Le etichette L sono una voce;
  - **MVA**: una voce per gruppo (il 2° campo: `LIMM`, la zona di scalo), le parti sono le zone col soprannome del blocco
    (`zone=`, E1) o il commento sopra;
  - **aerovie** (B1): una voce per aerovia, senza i `BREAK`, coi pezzi e le etichette;
  - **`.geo` e `.pol`** (H3): la voce è il gruppo di segmenti (o di poligoni) sotto un commento; «Percorso/Poligono
    senza titolo» di Google Earth e i gruppi senza commento sono **nomi mancanti** (⚠, in grigio, e contati nel titolo);
  - **aree P/R/D**: una voce per area, col nome del 6° campo.
  Ogni voce e ogni parte ha la sua **casella**: spenta, sparisce dalla mappa (anche nella vista «Solo questo…»). Per
  spegnere un poligono solo, le forme dei confini portano per ogni tratto il numero del suo poligono
  (`FormaDellaMappa.Parti`, `q` nel JSON); la pagina ridisegna la forma coi tratti accesi (`sectorlab.mappa.spenti`). Il
  filtro dei record vale anche sulle voci e sui nomi delle parti; un clic sul nome sceglie il record.
  - Trovato a schermo: nella scheda di un confine «Poligoni» si leggeva `Vipi.Sectorfile.Models.StaticBoundaryPolygon, …`
    → ora «N punti» (anche i tratti degli `.str`, i vertici MVA e i punti delle SID). Rosso: il test cade col codice di prima.
  Misura sul fork (strumento fuori repo, `scratchpad/misura6`): `FRA.artcc` **6 gruppi** (FRA BDRY, LIMITROFI, NPZ, AOCC
  MM/PP/RR) più le 104 etichette L; FRA BDRY **5 poligoni in 5 blocchi, tutti col nome** («FRA IT - Zona A», «Zona B»,
  «PADOVA TUTTA da sud a nord», «MILANO-ROMA NE», «confine ROMA-BRINDISI») — 🟡 i «31 tratti» della carta «file per file»
  §1 nel file di oggi non ci sono (5 blocchi, 7 commenti, nessun punto per nome): da rivedere col committente;
  LIMITROFI 14 poligoni, 9 col nome; **`lirr.hartcc` 13 voci**; **564 «senza titolo» nei `.geo`** (come la carta) più
  42 gruppi senza commento, e 253 «senza titolo» nei `.pol`. In tutto: `.artcc` 10 voci e 116 parti, `.hartcc` 26 e 45,
  `.lartcc` 21 e 50, MVA 169 voci e 334 zone, aerovie 277 voci e 540 pezzi, `.geo` 6 298 voci, `.pol` 1 731, aree P/R/D
  517; ogni record in una voce sola, ogni riga del nome un commento (0 guasti). Prova a schermo sul banco: `FRA.artcc` —
  7 voci con le caselle; LIMITROFI aperta mostra i suoi 14 poligoni («brindisi/tirana»…, «poligono 3 · senza nome»);
  spenta, coi soli FRA.artcc sulla mappa, i tratti verso l'estero spariscono; riaccesa, tornano. Test: Lab 579 → **596**.
- **6b (28 settembre)** — il nome dal commento cambiato dalla scheda, e il nome mancante (H3, §M «nome di un pezzo»).
  Nella scheda la sezione **«Voce»** (`Components/VoceNellaScheda.razor`): la voce del record e, per le sue parti di
  questo record, il nome da cambiare; nei `.geo` e nei `.pol` anche **«Nome del gruppo»**. Il gesto
  (`ModificheInSospeso.CambiaIlNome`, una voce nel testo del file: «nome «Percorso senza titolo» → «pista 03/21»»)
  riscrive il commento che dà il nome **tenendone le barre** (`// LINPZ1 VEKEN` → `// VEKEN`), ne mette uno sopra la prima
  riga del pezzo se non c'è (un poligono di LIMITROFI senza nome), e con un nome vuoto lo toglie; nelle zone MVA col
  soprannome del blocco (E1) cambia il metadato `zone`. Rifiutati col perché: un `;` (il commento sembrerebbe una riga di
  dati nascosta, 5c), un `@` davanti (sarebbe un tag). Il nome della voce dei confini, delle MVA e delle aerovie sta
  nelle righe di dati (il 2° campo): si cambia con la rinomina della slice 7.
  - **Codice comune, `Vipi.Sectorfile`**: regola nuova del validatore **`NomeMancante`, avviso, UNO per file** (come i
    commenti in coda, 2a) — i gruppi dei `.geo` e dei `.pol` il cui ultimo commento prima dei dati è «… senza titolo» di
    Google Earth, col numero e le righe (`Validatore.SenzaTitolo`). Nel controllo delle modifiche è «nuovo» solo se
    crescono (dare un nome cambia la riga del primo e non deve risultare introdotto).
  Misura: la regola del motore e le voci del Lab danno gli stessi numeri, **564 gruppi nei `.geo`** (la carta) e 253 nei
  `.pol`, in **62 file**; il validatore sull'albero passa da 481 a **543 avvisi** (+62), il resto della prova sull'albero
  invariato (701/701, tutto toccato 0, 115 568, tag, punti, blocchi, 120 errori, solo `limf.sid`). Prova a schermo sul
  banco: `liaa.geo`, la voce «Percorso senza titolo» (⚠) → la scheda «Voce · nome mancante» → «pista 03/21»: −1 +1 sulla
  riga 356, la voce cambia nome, nessun problema introdotto; «Annulla tutto». Test: motore 671 → **674**, Lab 596 → **602**.
- **6c (28 settembre)** — la regola commento/blocco di §M: «un pezzo che riceve il primo dato passa da commento a blocco,
  e il commento resta sopra» e «nei file senza nome nelle righe di dati (`.geo`, `.pol`) il nome del blocco è il nome del
  gruppo». Un segmento o un poligono che fino alla 3d non poteva ricevere metadati (non ha un nome suo) ora li scrive nel
  blocco del suo **gruppo col nome dal commento** (`VociDellaSelezione.GruppoDelRecord`, `IFileConRecord.ConIlBlocco`
  → `Metadati.ScriviIlBlocco` del motore, che c'era dalla 1d): `//fence`, poi `//@"fence" note=recinzione`,
  `//@START`, il gruppo intero, `//@END "fence"`. Le chiavi dopo cambiano la dichiarazione, e tolta l'ultima il blocco
  se ne va e il file torna quello di prima. Un gruppo col nome mancante (H3) non dà il nome a un blocco: «prima dagli un
  nome vero (sezione «Voce»)». Il test della 3d che voleva i segmenti sempre in sola lettura ora prova il caso senza
  commento. Rosso: col codice della 6b 3 test nuovi su 4 cadono.
  Misura sul fork (strumento fuori repo, `scratchpad/misura6c`): una nota sul record in mezzo di **ogni** gruppo di ogni
  `.geo` e `.pol` — **7 167 su 7 167** col blocco giusto (tutto il gruppo e niente fuori, dichiarazione sotto il
  commento, file riletto coi tag validi e gli stessi record), **7 167 su 7 167 tolti tornano al file di prima**; 862
  rifiutati col perché (564 + 253 «senza titolo», 45 gruppi senza nessun commento, quasi tutti in `itgeo.geo`). Prova a
  schermo sul banco: `liap.geo`, nota su un segmento del «fence» → −0 +3 (dichiarazione e `//@START` sotto `//fence`,
  `//@END` dopo il gruppo); «Annulla tutto». Test: Lab 602 → **606**.

**Slice 6 chiusa.** Uscita misurata sul fork: le voci come la finestra di selezione di Aurora — **`FRA.artcc` 6 gruppi**
(FRA BDRY coi suoi 5 poligoni tutti col nome; 🟡 i «31 tratti» della carta nel file di oggi non ci sono), **`lirr.hartcc`
13 voci**, **564 «senza titolo»** nei `.geo` segnalati come nome mancante (e 253 nei `.pol`); voci e parti accese e spente
sulla mappa, il nome dal commento cambiato dalla scheda, il blocco `//@` col nome del gruppo. Da provare a mano
(eseguibile ripubblicato): prove 90-94 in `SectorLab-prova\PROVE.md`.

**Slice 7 — «chi lo usa» e rinomina.** Divisa in passi, un commit ciascuno (scelta dell'agente): **7a** l'indice di chi
usa un punto chiamato per nome, nella scheda · **7b** la rinomina di un punto (una voce, più diff) e il «togli» impedito
a un punto usato · **7c** le posizioni (R-1) · **7d** le piste · **7e** i file (`.cpr`, `.atis`) e i nomi di `colors.def`
· **7f** il nome delle voci di confini, MVA e aerovie (il 2° campo, rimandato dalla 6b). (Divisa così il 28 settembre,
cominciando la 7c: le posizioni, le piste e i file hanno ognuno la sua forma di citazione.)

- **7a (28 settembre)** — «chi lo usa» (L2). Nella scheda di un fix, VOR, NDB, scalo, punto VFR o attesa la sezione
  **«Chi lo usa»** (`Components/ChiLoUsaNellaScheda.razor`): le righe che lo citano, file per file, col testo; il clic
  porta al record e segna la riga. L'indice (`Sessione/ChiLoUsa.cs`) tiene per nome i file e i record che lo citano;
  si fa all'apertura accanto ai cataloghi (330-450 ms sul fork) e si rifà per file a ogni modifica.
  - **Che cosa cita**: i nomi che risolve il validatore (`NomeNonRisolto`: punti per nome, punti dei `.str`, vertici
    `T;` dei confini, etichette delle aerovie), il fix della descrizione di un'attesa (`ABBOZ/225R-9000`), i valori dei
    tag `fix=` e `trans=` (§M, P6). Un'attesa la citano i fix e i VOR che ci rimandano (`HLD-ABBOZ`). Le righe
    commentate no: Aurora non le legge.
  - **Di chi è una citazione**: 🔴 un nome va a un punto **per master**. Una citazione è di questo punto se, nei master
    che caricano il suo file, il nome si risolve nel suo file — o in una sua **copia**, lo stesso catalogo a meno di un
    decimo di miglio (le copie gemelle di F3-bis, i `NomeRipetuto`). Se va a lui solo in alcuni master lo dice («solo
    in ITALY.isc»). Le citazioni dello stesso nome che vanno a un altro punto stanno sotto, in grigio, **«Stesso nome,
    altro punto»**, con dove vanno: non sono sue, e la rinomina non le toccherà.
  - **Codice comune, `Vipi.Sectorfile`**: `Cataloghi.Usati(record)`, pubblico, i nomi citati da un record — è lo
    stesso metodo del validatore, perché le due risposte non si separino. Nel Lab `IFileConRecord.ChiaviDeiRecord()`:
    le chiavi di tutti i record in un passo (con `ChiaviDi` a ogni record l'indice costava 9,7 s, quadratico).
  Misura sul fork (strumento fuori repo, `scratchpad/misura7`): **`LUSIL` 12 righe in 5 file** (`FRA.artcc` 1,
  `itawlow.lairway` 4, `limm.hartcc` 2, `lipp.hartcc` 2, `liml.str` 3), le stesse del grep tolta la riga commentata
  `itawlow.lairway:2636` e la dichiarazione. In tutto: fix 3 901, usati 2 069, 8 832 citazioni; VOR 121, usati 61,
  719; NDB 27, 13, 103; scali 295, 2, 2; VRP 586, 84, 134; attese 68, usate 67. «Altro punto»: i fix scendono da 317 a
  **0** contando le copie a meno di un decimo di miglio; restano **8 VOR col nome di un NDB** (LPD, MMP, OST, PAN, PES,
  PIS, TRP, VIE: 100 citazioni che il catalogo dà all'NDB, `itndb.ndb` viene prima di `itvor.vor`) e 2 citazioni di VRP
  — vedi sotto, VOR e NDB omonimi.
  L'attesa senza rimandi è **`HLD-EKLAP`** (L1: il fix la chiama `HLD-ELKAP`). Prova sull'albero invariata. Prova a
  schermo sul banco: `LUSIL` → «Chi lo usa · 12 righe in 5 file»; clic su `T;T772;LUSIL;LUSIL;` → scheda di T772, riga
  1119 segnata; VOR TRP → «Nessuna citazione va a questo punto» e «Stesso nome, altro punto · 28 righe». Rosso: i test
  nuovi non compilano sul codice di prima (tipi e metodi nuovi). Test: motore 674 → **676**, Lab 606 → **616**.
  - **VOR e NDB con lo stesso nome** (decisione del committente, 28 settembre: «mostrali entrambi, capita che VOR e NDB
    abbiano lo stesso nome»): la riga non dice quale dei due, e vale per tutti e due. Nella scheda di ognuno le righe
    sono sue, con la nota «Stesso nome anche per: NDB TRP, NAVAIDS/itndb.ndb» (`Citazione.AncheA`); contano solo gli
    omonimi in un file che un master carica. Sul fork: VOR usati 61 → **72**, 719 → **819** citazioni, «altro punto»
    dei VOR 100 → **0**; TRP 28 righe in 5 file, nel VOR e nell'NDB. Restano «altro punto» solo 2 citazioni di VRP.
    Test: Lab 616 → **617**.
- **7b (28 settembre)** — la rinomina di un punto (L2) e il «togli» impedito a un punto usato. Nella sezione «Chi lo
  usa» il campo **«Rinomina»** (uno per nome: il VRP ne ha due). `Modifiche/Rinomina.cs` prepara le righe, file per
  file: la dichiarazione del punto e delle sue **copie** (`ChiLoUsa.Copie`: stesso catalogo, stesso nome, meno di un
  decimo di miglio — lo scalo in `itap.ap` e `limm.ap`), ogni citazione di «chi lo usa», i tag che lo nominano
  (`//@"NOME"` del suo record, `//@@"NOME"` dei punti, i valori di `fix=` e `trans=`), il fix nella descrizione di
  un'attesa (`LUSIL/225R-9000`); il resto della riga resta com'era, spazi compresi, e le righe commentate no.
  `ModificheInSospeso.CambiaInPiuFile` prova prima ogni file (riletto: stessi record, tag validi come prima) e solo se
  vanno tutti li cambia: **una voce** nel pannello (il file del punto, «anche in …»), un diff per file
  (`ModificaDelTesto.ParteDi`), annullata da qualunque suo file torna tutta. Dopo una rinomina, e dopo ogni annulla, i
  cataloghi dei master si rifanno.
  - **Rifiuti**: un nome che non va (vuoto, `;`, `/` — separa il fix nella descrizione di un'attesa —, virgolette o
    `@`, una coordinata), un nome che c'è già («due punti con lo stesso nome»; cambiare solo le maiuscole si può).
  - **Le righe comuni si chiedono** (committente, 28 settembre: «deve chiedere»): quelle che valgono anche per un
    altro punto — un VOR e un NDB omonimi, o lo stesso nome che un altro master risolve altrove (la citazione è sua
    «solo in» alcuni) — non si cambiano finché l'AOD non sceglie «Sì, anche quelle» o «No, lasciale».
  - Un'attesa rinominata porta con sé il campo dei fix e dei VOR che ci rimandano (`HLD-ABBOZ`); un fix rinominato
    cambia la descrizione della sua attesa ma non il nome dell'attesa (`HLD-LUSIL` resta: è un altro nome).
  - **Togli**: un punto che qualcuno cita non si toglie — «È usato: lo citano N righe in M file (vedi «Chi lo usa»)».
  Scelta dell'agente: il file rinominato non si riordina (un fix fuori dall'ordine alfabetico lo si sposta a mano).
  Misura sul fork (strumento fuori repo, `scratchpad/misura7b`): **ogni punto usato** rinominato (nome + «Q7») e poi
  annullato — **2 307 su 2 307**, 12 686 righe, 0 rifiutati, **0 guasti** (le citazioni dopo sono le stesse righe di
  prima, il punto ha il nome nuovo, e annullato ogni file torna quello dell'apertura); 25 domande (22 VOR/NDB omonimi,
  3 «solo in»). **`LUSIL` → 13 righe in 6 file**: `itfix.fix` 1, `FRA.artcc` 1, `itawlow.lairway` 4 (non la riga
  commentata 2636), `limm.hartcc` 2, `lipp.hartcc` 2, `liml.str` 3 — tutti e soli i file giusti. `LIMA` (scalo) porta
  con sé la copia di `limm.ap`. Prova a schermo sul banco: `LUSIL` → «LUSIX»: «1 modifiche in 6 file», la voce in
  `itfix.fix` «anche in FRA.artcc, itawlow.lairway, limm.hartcc, lipp.hartcc, liml.str»; «annulla» su `FRA.artcc`
  toglie tutto; «Togli questo record» su LUSIL rifiutato col perché. Il motore non è toccato. Test: Lab 617 → **634**.
- **7c (28 settembre)** — le posizioni (R-1): «chi lo usa» e rinomina. Una posizione è un nome di rete: vale in tutto
  l'albero, non per master. La dichiara il 1° campo di un `.frq` (`itfreq.frq` e il `.frq` della FIR sono **copie**); la
  citano i **trasferimenti** (3° campo, parole separate da spazi — anche esclusa, `-LIRR_NW_CTR`, «trasferimento
  escluso») e la **testa dei settori dinamici** (1° campo di un `.tfl`, «settore dinamico»). Solo le parole uguali:
  `LIRR` nei trasferimenti include la posizione per prefisso, ma non la nomina, e la rinomina non lo tocca.
  - Rinomina: il 1° campo delle righe che la dichiarano e la **parola** nelle citazioni, col «-» dell'esclusa tenuto
    (`Rinomina.NelPrimoCampo`, `NellaParola`); il resto della riga com'era. Rifiuti: una posizione che c'è già, uno
    spazio (separa i trasferimenti), un «-» davanti (vuol dire «esclusa»).
  - «Togli» di una posizione usata: bloccato, **salvo** che un altro `.frq` la dichiari ancora (è una copia: la
    posizione resta nel sector). I punti restano bloccati sempre.
  Misura sul fork (`scratchpad/misura7` e `misura7b`, estesi alle posizioni): **423 posizioni, 308 citate, 3 308
  citazioni**; **LIRR_NW_CTR 25 righe in 5 file** (`lirr_ne_ctr.tfl` 1, `itfreq.frq` 12, `libb.frq` 4, `limm.frq` 5,
  `lirr.frq` 3), le stesse del grep tolte le 2 righe che la dichiarano. Rinomina di ogni punto e posizione usati, e
  annullata: **2 615 su 2 615** (2 307 punti + 308 posizioni), 16 618 righe, 0 rifiutati, **0 guasti**. Prova a schermo
  sul banco: LIRR_NW_CTR → «LIRR_WN_CTR»: una voce, 27 righe in 5 file (le 25 citazioni e le 2 dichiarazioni); «Annulla
  tutto» pulito. Il motore non è toccato. Test: Lab 634 → **640**.
- **7d (28 settembre)** — le piste (R-1): «chi lo usa» e rinomina di un verso. La pista è «scalo + verso»; il record
  del `.rw` ne dichiara due (`LIRF;16L;34R;…`), e la scheda ha un campo «Rinomina» per ciascuno (`Sessione/Piste.cs`).
  - **La citano, e la rinomina le riscrive**: il 2° campo delle SID (`LIRF;16L;NENI7J;…`) e delle voci dei `.str`
    (`LIRF;16L:16R;ELKA3A;…`, i versi separati da «:»: si cambia solo il suo), il nome delle mappe del `MAPS`
    (`RWY16L`), i tag del `.rw` (il nome del record `//@"LIRF 16L/34R"` e le chiavi `16L.tora=…`, §M regola 7), e le
    **copie** della pista (lo stesso scalo e la stessa coppia in un altro `.rw`: `itrw.rw` e `lirr.rw`).
  - **La citano, e la rinomina le ELENCA** («Da cambiare a mano», `RinominaPronta.AMano`): i PAR dei profili `.cpr`
    (`INS1PAR_CAPTION=LIBN RWY14/3.0°` — lo scalo è quello della didascalia, non del file: `LIPA.cpr` ha il PAR di
    LIPI) perché il Lab non ha ancora il lettore dei `.cpr` (slice 11), e i commenti dei disegni dello scalo
    (`lirf.geo`, `rf_ad_gnd.pol`, `rf_mark.geo`: `//Runway 16L designator`) perché una marcatura rinumerata è un
    disegno nuovo. Il clic su una di queste righe apre il file lì.
  - Rifiuti: un verso che non è un verso (due cifre fra 01 e 36, e L, R o C), un verso che lo scalo ha già.
  - Una pista usata non si toglie (come i punti).
  Scelte dell'agente: le mappe si riconoscono solo dal nome esatto `RWY` + verso (209 sul fork); le rotte e le quote
  della pista non cambiano con la rinomina (16L → 16C è lo stesso asfalto; una rinumerazione per la declinazione cambia
  la rotta e va scritta nella scheda).
  Misura sul fork (`scratchpad/misura7d`): **309 piste** (con le copie), 244 citate, **7 756 citazioni** — procedure
  6 383, mappe 209, da cambiare a mano 1 164 (PAR 44); **LIRF 16L/34R 108 righe in 4 file** (`lirf.sid` 57 — 35+22, le
  stesse del conteggio dei campi —, `lirf.str` 39 + 2 mappe, `lirf.geo` 9 e `rf_ad_gnd.pol` 1 commenti). Rinomina di
  ogni verso (a un verso libero dello stesso numero) e annullata: **618 su 618**, 8 685 righe, 1 570 da cambiare a
  mano, 0 rifiutati, **0 guasti** (dopo, il verso nuovo è citato dalle stesse righe, il vecchio da nessuna che il Lab
  scrive; annullato, ogni file torna quello dell'apertura). Prova a schermo sul banco: LIRF 16L → «16C»: una voce, 4
  file, e «Da cambiare a mano · 7 righe» (6 commenti di `lirf.geo`, 1 di `rf_ad_gnd.pol`); «Annulla tutto» pulito. Il
  motore non è toccato. Test: Lab 640 → **647**.
- **7e (28 settembre)** — «chi lo usa» dei file e dei nomi di `colors.def` (R-1), **solo da vedere**. Nel pannello di
  ogni file (anche quelli che il motore non interpreta: `.cpr`, `.def`, `.datis`) la sezione **«Chi lo usa»**
  (`Sessione/ChiUsaIlFile.cs`, `Components/ChiUsaIlFileVista.razor`): gli `.isc` che lo caricano — con la riga `F;`, o
  «trovato per nome» (il `F;` dice un'altra cartella, M6: `DYNAMIC_SEC\GCI.tfl` sta in `OTHER\`), o «per il codice
  dello scalo», o «perché un `.frq` lo cita» — e le righe dei `.frq` che lo citano come **profilo**, **ATIS** o
  **D-ATIS** (anche con la barra di troppo davanti, `\liml.atis`). Un file che nessuno usa lo dice («Aurora non lo
  legge»). In un `.def`, sotto, **ogni nome** col suo campione, quante volte e in quali file lo usano il riempimento e
  il bordo dei `.pol` e dei settori dinamici e le linee dei `.geo`; il clic su un file lo apre.
  - **Rinominare un file o un colore non è in questa slice** (per il futuro, §4: deciso dal committente il 28 settembre). Un file rinominato vuol dire un
    file spostato sul disco (il salvataggio oggi scrive solo contenuti), e `colors.def` il motore lo legge solo come
    tavolozza (niente record né scrittore): servono tutti e due un pezzo nuovo.
  Misura sul fork (`scratchpad/misura7e`): **748 file, 2 829 usi** (943 righe `F;`, 876 per il codice dello scalo, 70
  perché un `.frq` li cita, 5 trovati per nome, 386 profili, 166 ATIS, 383 D-ATIS), 3,6 s per tutti (il più lento 24
  ms); **28 file senza nessun uso** — 22 `.txt`, e `itawhigh.hairway`, `ENR.fix`, `FRA.fix`, `TERM.fix`, `WW0.cpr` («serve
  a un settore da venire», §14), `limw.pol` (la copia orfana di V2). `CTR.cpr` 59 usi, `default.atis` 107, `colors.def`
  le 5 righe `F;IT\colors\colors.def`. **Colori**: 16 nomi, 50 666 usi — gli stessi della prova sull'albero (TAXIWAY
  19 149 = 18 268 dei `.geo` + 881 dei `.pol`); **MARKING non lo usa nessuno**. Prova a schermo sul banco:
  `colors.def` → «Chi lo usa · 5 .isc» e i 16 colori coi campioni. Il motore non è toccato. Test: Lab 647 → **652**.
- **7f (28 settembre)** — il nome delle voci che sta nelle righe di dati (rimandato dalla 6b). Nella scheda, sezione
  «Voce», il nome dei confini, delle MVA, delle aerovie e delle aree P/R/D diventa un campo
  (`Modifiche/RinominaDellaVoce.cs`, `SessioneDelLab.RinominaLaVoce`). Vale **nel file della voce**: Aurora raccoglie per
  nome, e sul fork nessun nome di voce sta in due file (misurato: solo `DUMMY`; le MVA di scalo «2000» in `licc.mva` e
  `lipe.mva` sono zone di scali diversi).
  - Dove cambia: i confini nel 2° campo delle righe `T;` (le etichette `L;` sono la voce «Etichette (L)», e una col
    testo uguale resta); le MVA nel 2° campo delle `T;` e delle `L;`, e nel 5° dove era il gruppo (`T;LIMM;…;LIMM;`,
    4 257 righe sul fork); le aerovie nel 2° campo dei tratti e nella **parola** delle etichette (`L;M984-Y740;`); le
    aree nel 6° campo. Anche le **righe nascoste** della voce (5c: mostrate, tornano con lei) e il tag del suo blocco
    (`//@"M984"`). Una voce nel testo del file.
  - Non si rinominano: l'etichetta condivisa «L613-L615» (segue le sue aerovie), le zone MVA «(senza gruppo)» (il
    nome non è scritto), «Etichette (L)». Rifiuti: `DUMMY`/`BREAK`, un nome di un'altra voce del file («le due
    diventerebbero una»), spazi o «-» nel nome di un'aerovia, `;`, virgolette, «@» davanti.
  Misura sul fork (`scratchpad/misura7f`): **985 voci** rinominabili — aerovie 246 (2 302 righe), aree 517 (13 028,
  12 nascoste), confini 55 (12 959, 5 nascoste), MVA 167 (9 324, 53 nascoste) — rinominate (nome + «Q7») e annullate:
  **985 su 985, 0 guasti** (dopo, le stesse voci, quella nuova coi record di prima e nessuna col nome vecchio;
  annullato, il file torna quello dell'apertura). `M984` 13 righe (7 tratti + 6 etichette, come il grep), `FRA BDRY`
  1 589, `LIMM` 1 441 (42 nascoste). Prova a schermo sul banco: M984 → «M985» rifiutato («c'è già una voce M985»: nel
  fork c'è), → «M999»: `itawlow.lairway` −13 +13; «Annulla tutto» pulito. Il motore non è toccato. Test: Lab 652 →
  **661**.

**Slice 7 chiusa.** Uscita misurata sul fork: «chi lo usa» e rinomina di punti (2 307), posizioni (308), versi di pista
(618) e voci (985) — rinominati e annullati tutti, 0 guasti; **`LUSIL` 13 righe in 6 file** e **LIRR_NW_CTR 27 righe
in 5 file**, tutti e soli i file giusti; «togli» impedito a chi è usato; «chi lo usa» di file e colori (solo da vedere:
la loro rinomina è per il futuro, §4). Decisioni del committente del 28 settembre: VOR e NDB omonimi mostrati tutti e
due, e la rinomina **chiede** per le righe comuni. Da provare a mano (eseguibile da ripubblicare): prove in
`SectorLab-prova\PROVE.md`.

**Slice 8 — famiglie di forme e gemelli fra tipi diversi (dal 28 settembre).** Divisa in: **8a** l'indice delle
forme uguali e la vista nella scheda · **8b** il tag `form=` (la famiglia dichiarata) e l'avviso «copie di forma
diverse» · **8c** la modifica propagata alle copie uguali, «allinea anche questa», «copia la forma da…» · **8d**
`.geo` ↔ `.pol` (I2) e confine dello scalo ↔ erba (H10) · **8e** il gemello `.vfi` ↔ `VFR_NASCOSTI.fix` (F2).

- **8a (28 settembre)** — la stessa forma in più record (D5, I2, J3, Q5, H10), **solo da vedere**. L'indice
  (`Copie/FormeUguali.cs`) lavora sulle forme della mappa: i punti per nome sono già risolti nel master scelto, i
  segmenti dei `.geo` e delle aree P/R/D già cuciti in linee. Due forme sono **la stessa** se hanno gli stessi vertici
  nello stesso giro, da qualunque vertice e in qualunque verso (confronto come ANELLO), in qualunque forma siano
  scritte le coordinate (col punto, compatte, per nome); il vertice che ripete il primo non conta, due vertici sono lo
  stesso al decimo di metro. Sono **simili** (le «copie di forma diverse») se hanno in comune almeno 9 vertici su 10
  del più lungo dei due. Meno di 3 vertici non è una forma. Vale anche nello stesso file, fra record diversi
  (`libb_es_ctr.tfl`: LIBB_ES_CTR e LIBB_MIL_CTR). Nella scheda, sezione **«La stessa forma»**
  (`Components/StessaFormaNellaScheda.razor`): per ogni parte del record, le copie uguali e poi, in giallo, le diverse
  con «N vertici solo qui, M solo là»; il clic porta alla copia. Un segmento di un `.geo` vale per la sua linea.
  L'indice si rifà quando cambiano gli strati (una modifica rifà le forme del suo file).
  Scelta dell'agente: la soglia dei 9/10 si misura sul **più lungo** dei due anelli, così un settore e uno più piccolo
  che ne tiene i vertici (`LIMM_ES2_CTR` ⊃ `LIMM_ES5_CTR`, 12 vertici in più) restano «simili», non «uguali»; la
  famiglia vera la dichiara il tag (8b), e l'avviso varrà solo per quella.
  Misura sul fork (`scratchpad/misura8`, master `ITALY.isc`): **10 610 forme**, indice in 97 ms, la domanda della scheda
  sotto 1,5 ms (21 853 in 81 ms). **1 971 famiglie di forme uguali** (3 999 parti, al più 5): `.geo`+`.pol` 1 591,
  `.str` fra loro 158, `.geo` fra loro 77, `.str`+`.tfl` 69, `.restrict` 17, `.lartcc`+`.str` 14, `.lartcc`+`.tfl` 13,
  `.hartcc`+`.tfl` 5… **Settori dinamici**: 182 forme, **98 con una copia uguale, 17 solo con copie diverse** —
  LIRE_APP/LIRE CTR, LIRS_APP, LICC_APP, LICJ_APP/LICJ CTR, LICT_APP, LIBP_APP nei `.str`; LIRR_NC/US, LIMM_FSS, LIPP_FSS
  negli `.hartcc`; LIRF_TW1_APP in `lirr_tma.lartcc` (scarti veri, da 27 m a decine di km, non arrotondamenti). La
  slice 0 contava 120 identiche e 12 divergenti su 132 con un'altra regola (≥ 90% dei vertici del settore in un altro
  file, senza l'ordine): l'anello è più severo. **`.pol`**: 1 753 poligoni, **1 594 col bordo uguale in un `.geo`**, 9
  solo simili (la carta di I2 diceva 1 585). Prova a schermo sul banco: LIRE_APP → «La stessa forma · 1 copia
  diversa: `lire.str` LIRE CTR, 1 vertice solo qui, 1 solo là»; il clic apre `lire.str#0`, che mostra LIRE_APP allo
  stesso modo. Il motore non è toccato. Test: Lab 661 → **671**.
- **8b (28 settembre)** — la famiglia **dichiarata** e l'avviso «copie di forma diverse» (D5, §M `form`). I record che
  portano lo stesso `form=NOME` (settori, confini, mappe degli `.str`, `.geo`, `.pol`: le chiavi del catalogo della
  slice 1) sono una famiglia (`Copie/FamiglieDichiarate.cs`); la **forma della famiglia** è l'anello che hanno più
  membri (a pari, quello del primo nell'ordine dei file), e chi non ce l'ha è una copia di forma diversa: regola nuova
  **`FormeDiverse`, avviso**, sulla prima riga del record («Famiglia «LIRE»: LIRE CTR ha una forma diversa da
  lirrctr.tfl LIRE_APP»). Due record della stessa linea di un `.geo` contano una volta.
  - **Codice comune toccato**: solo la regola nell'elenco del motore (`Regola.FormeDiverse`, gravità avviso). La
    calcola il Lab, che ha le forme coi nomi risolti, e la aggiunge ai problemi della validazione dell'albero; il
    validatore del motore (e `tools/Vipi.SectorfileProva`) non la dà.
  - **Senza tag, nessun avviso**: la famiglia la trova il Lab (8a) e la dichiara l'AOD. Così le copie simili che non
    sono la stessa cosa (`LIMM_ES2_CTR` e `LIMM_ES5_CTR`) non riempiono il pannello, e le 17 divergenti dei settori
    diventano avvisi solo quando qualcuno dice che devono essere uguali (D6: l'adozione è di F4).
  - Nella scheda, sezione «La stessa forma»: la famiglia `form=NOME` coi suoi record (uguale / forma diversa, il clic
    porta lì), «Questo record non ha la forma della famiglia» se è lui il diverso; senza famiglia, **«Dichiara qui e
    sulle N copie uguali»** con un nome proposto (il nome del record, l'AOD lo cambia): `form=` sul record e sulle copie
    uguali, un gesto solo nella storia (le copie diverse restano fuori); con la famiglia, **«+ Anche le copie uguali»**
    per quelle che il tag non l'hanno. Un record che il tag non lo può portare lo dice il rifiuto, gli altri si scrivono.
  - Trovato sul banco: dopo «Annulla tutto» la sezione mostrava ancora la famiglia — ha solo parametri primitivi e il
    ridisegno del padre non la raggiunge (la trappola della mappa di F3). Si iscrive da sé ai cambi del Lab, come la
    scheda; test rosso sul codice di prima.
  Misura sul fork: **0 tag `form=`**, quindi 0 famiglie e 0 avvisi (la validazione dell'albero resta 120 errori e 543
  avvisi); leggere i tag di tutti i file costa 33-61 ms, e la scheda li tiene finché non cambiano gli strati o un
  metadato. Prova sull'albero invariata (701/701, 1 guasto noto `limf.sid`). Prova a schermo sul banco: LIRF_TWR →
  «Dichiara qui e sulla copia uguale» → `twrs.tfl −0 +3` e `lirf.str −0 +3` (tag, `//@START`, `//@END`), «Annulla tutto»
  pulito; `form=LIRE` a mano su LIRE_APP e LIRE CTR → «2 record · 1 di forma diversa», salvato nella copia di prova →
  543 → **544 avvisi**, «lire.str:5 FormeDiverse». Test: Lab 671 → **679** (motore 676, invariato).
- **8c (28 settembre)** — la forma portata sulle copie (D5: «modifica propagata alle copie uguali», «allinea anche
  questa», «copia la forma da…»). `Copie/PortaLaForma.cs` scrive l'anello di una forma su una sua copia; la copia
  **tiene** il suo vertice di partenza, il suo **verso** (dalle coppie di vertici vicini, non dall'area col segno, che su
  una forma che si incrocia non vuol dire niente), la sua **chiusura** (il primo punto ripetuto in fondo) e la
  **scrittura** dei vertici che restano (un nome resta nome); un vertice nuovo si scrive come nella forma di partenza,
  se la copia lo sa scrivere (un `.pol` tiene solo coordinate: un nome diventa la sua posizione; i punti di una
  procedura `.str` solo nomi: una coordinata non ci va, e la copia lo dice). Le righe toccate escono nella forma del
  file (`FormaDelPunto` di F2: un `.hartcc` compatto resta compatto anche nel vertice nuovo).
  - **Dopo ogni gesto sui vertici** (sposta, aggiungi, togli, inverti, incolla) la forma va sulle copie che PRIMA del
    gesto erano uguali, una voce di vertici per copia («Tratto 1: forma portata da twrs.tfl LIRF_TWR»), tutto un gesto
    solo nella storia; quelle già diverse restano. Le copie si fissano alla prima esecuzione del gesto, come la densità
    dell'incolla: rigiocato da annulla/ripeti, il gesto le ritrova anche con gli strati non ancora rifatti.
  - Nella scheda, sotto ogni copia diversa: **«⇒ Allinea quella»** (questa forma su quella copia) e **«⇐ Prendi la
    sua»** (la forma di quella su questo record); sopra, «Forma portata anche su: …» e, in giallo, dove non è andata e
    perché. La nota vale per il gesto appena fatto: scegliere un altro record la toglie.
  - Le **linee dei `.geo`** non sono un elenco di vertici (sono segmenti cuciti): non ricevono ancora la forma, e la
    scheda lo dice («una linea di un .geo: slice 8d»).
  Misura sul fork (`scratchpad/misura8c`, niente salvato): per ogni forma con copie uguali, il primo vertice spostato di
  un millesimo di grado col gesto vero, poi annullato — **413 forme** (i `.pol` limitati a 200 su 1 594), **232 copie
  portate** (`.tfl` 100, `.str` 74, `.lartcc` 41, `.hartcc` 12, `.mva` 4, `.artcc` 1), dopo il gesto **tutte uguali**,
  annullato **tutto come prima: 0 guasti**, 48 s; **212 non portate, tutte linee `.geo`** (201 bordi dei `.pol`); 149
  forme scritte solo per nome (le mappe delle procedure) non provate col vertice a coordinate. Prova a schermo sul
  banco: LIRE_APP → «⇒ Allinea quella» su LIRE CTR → `lire.str −1 +1` (solo il vertice diverso, il commento in coda
  della riga sopra intatto), la famiglia `form=LIRE` tutta uguale; LIRF_TWR, tolto il vertice 3 → «Forma portata anche
  su: lirf.str LIRF LIRF ATZ», `twrs.tfl −1 +0` e `lirf.str −1 +0`; Ctrl+Z pulito. Il motore non è toccato. Test: Lab
  679 → **688**.
- **8d (28 settembre)** — il bordo in un `.geo` e il riempimento in un `.pol` (I2, H10). Codice comune toccato: solo la
  regola nuova `Regola.ConfineSenzaErba` (avviso), calcolata dal Lab come `FormeDiverse`.
  - **Le linee dei `.geo` ricevono e portano la forma.** Una linea (i segmenti di fila della 5d) non è un elenco di
    vertici: si riscrive come testo (`ModificheInSospeso.RiscriviLaLinea`, `PortaLaForma.RigheDellaLinea`), un segmento
    per lato, col giro e la partenza della linea, chiusa se lo era; i segmenti che non cambiano restano **le righe di
    prima, byte per byte**, e quelli nuovi prendono tipo, area e resto dalla prima riga della linea (senza commento in
    coda), con le coordinate nella sua forma (col punto o compatte). Spostare un punto della linea (5d) porta la forma
    sull'erba e sulle altre copie; «Allinea» e «Prendi la sua» valgono anche fra linee e poligoni.
  - 🔴 Una linea riscritta può avere un segmento in più o in meno, e i record dopo di lei nel suo file slittano (misura:
    `liaa.geo` ha la stessa linea due volte). Nello stesso file le copie si scrivono dall'ultima alla prima, e la scelta
    nella scheda segue il suo record; test rosso sul codice di prima.
  - **L'uscita che manca (I2)**: nella scheda di un poligono di un `.pol` senza bordo, **«+ Bordo in lirf.geo
    (TAXIWAY)»**; in quella di una linea chiusa di un `.geo` di scalo senza riempimento, **«+ Riempimento in
    rf_ad_gnd.pol (GRASS)»**. Si aggiunge in fondo al file, dopo una riga vuota e un commento col nome del gruppo (quello
    del `.pol` senza `_Polygon`, e viceversa), coordinate col punto; il record nuovo diventa la scelta, e da lì le due
    sono la stessa forma. Regole prese dalla misura (sotto), scelte dell'agente: il file è quello dove stanno già i bordi
    (o i riempimenti) di quel file, se no `GEO/lixx.geo` per `xx_ad_gnd.pol` (e viceversa); il tipo del bordo è quello del
    riempimento per BUILDING, TAXIWAY, APRON, RUNWAY, e BUILDING per gli altri; il riempimento di un confine dello scalo
    è GRASS, degli altri il tipo della linea (BUILDING per i tipi che non sono riempimenti). Il riempimento si propone
    solo nei `.geo` degli scali (`GEO/li…geo`) e non per le linee RUNWAY: sul fork le 458 linee chiuse RUNWAY senza
    riempimento sono soglie e numeri di pista disegnati a tratti. Senza un file dello scalo dove metterla, la scheda lo
    dice (crearne uno è per il futuro, §4).
  - **Il confine e la sua erba (H10)**: il gruppo `ad_boundary` di un `.geo` (con o senza le due lettere dello scalo:
    `MC_ad_boundary`, `mw_boundary`, `boundary`) senza un poligono uguale in un `.pol`, e l'erba `…_Polygon` senza una
    linea uguale: avviso `ConfineSenzaErba`, con «ce n'è uno simile, diverso di N vertici» se c'è.
  - Limite noto: la mappa cuce i segmenti dei `.geo` senza guardare commenti e righe vuote, la linea del file (5d) sì.
    Dove le due non coincidono (una linea chiusa attaccata a un'altra dello stesso tipo: **34 su 5 599**, quasi tutte la
    costa di `itgeo.geo`; 25 delle 200 linee provate sotto) la forma non si porta: il Lab non scrive niente, e non lo dice.
  Misura sul fork (`scratchpad/misura8`, `misura8c`): coppie `.pol` ↔ `.geo` **1 594**, tipi (BUILDING→BUILDING 625,
  TAXIWAY 399, APRON 184, CONCRETE→BUILDING 107, RUNWAY 103, GRASS→BUILDING 92, HOLE→BUILDING 50…); ogni `xx_ad_gnd.pol`
  ha i suoi bordi in un solo `.geo`, `GEO/lixx.geo` (92 su 94, gli altri due con lo stesso nome); i `.pol` non ripetono il
  primo vertice (1 673 su 1 751). **H10: 0 avvisi** (60 confini, 57 con l'erba: gli altri 3 sono confini di pista, fuori
  dalla regola; 58 erbe tutte col confine). **Uscite proponibili**: 159 bordi (BUILDING 109, TAXIWAY 35, RUNWAY 11, APRON
  4), 105 riempimenti (BUILDING 65, TAXIWAY 30, APRON 10), 3 senza file (`lipm.geo`). **Propagazione**, gesto vero e
  annulla: **613 forme** (anche 200 linee `.geo` come sorgente), **617 copie portate** (201 bordi dai `.pol`), dopo il
  gesto tutte uguali, annullato tutto come prima, **0 guasti**. Prova sull'albero invariata (120 errori, 543 avvisi).
  Prova a schermo sul banco: l'erba di LIRF (`rf_ad_gnd.pol`), un vertice spostato → «Forma portata anche su: lirf.geo»,
  `lirf.geo −2 +2` (i due segmenti del vertice); `rf_ad_gnd.pol` #6 → «+ Bordo in lirf.geo (TAXIWAY)» → `lirf.geo −0
  +113` (`//Twy_D_1` e 111 segmenti), la scelta sulla linea nuova, «La stessa forma» uguale; «Annulla tutto» pulito.
  Test: Lab 688 → **698**, motore 676.
- **8e (29 settembre)** — il gemello `.vfi` ↔ `VFR_NASCOSTI.fix` (F2). Un punto di un `.vfi` col codice nel 2° campo
  (`COLOMBO;RFS3;…`) e il fix nascosto con quel nome (`RFS3;…;3;`) sono gemelli (`Copie/GemelliVfr.cs`): una rotta VFR
  riconosce un punto solo se sta in un `.fix`. Il gemello entra nell'indice delle copie gemelle di F3-bis
  (`GemelliDellaSessione`), che da qui sa anche di gemelli fra tipi diversi; fra i due passa **solo la posizione** (il
  nome del punto non è il nome del fix: test rosso senza il filtro sul campo).
  - **Spostare** il punto sposta il fix e viceversa: una voce su due file («come in lirf.vfi»); se erano già diversi, il
    gemello resta «non cambiato» con «allinea», come le copie di F3-bis.
  - **Aggiungere**: nella scheda del punto senza gemello, «+ Crea il gemello RFS4 in VFR_NASCOSTI.fix» — nome = il
    codice, al suo posto in ordine alfabetico, copiato da un fix del file (tipo 3), nella posizione del punto; un gesto
    solo, la scheda resta sul punto. Scelta dell'agente: il gemello nasce col tasto e non da solo con «+ Nuovo record»,
    perché un punto nuovo nasce copia del vicino, **col suo codice**, e il codice giusto lo darà la slice 16 (F3, codice
    proposto): lì il gemello potrà nascere col punto.
  - **Togliere** il punto: la domanda «Hai tolto OSTIA: togli anche il suo gemello RFS2 da VFR_NASCOSTI.fix?» (sì = un
    gesto suo, no = resta); il gesto dopo, o annulla, la toglie.
  - Nella scheda, sezione **«Gemello»**: il gemello (clic = ci va), «stessa posizione» o «posizione diversa»; «manca»; un
    codice ripetuto (non si sa quale va con quale: niente propagazione); nel fix senza punto, «nessun punto ha il codice».
  Un codice è 2-5 lettere e 1-2 cifre: il 2° campo che non lo è (`2500`, `BV`) non chiede un gemello. La posizione si
  confronta al decimo di metro, non come testo.
  Misura sul fork (`scratchpad/misura8e`): **586 punti**, 76 senza codice; **495 gemelli uguali**, 5 con posizione
  diversa (`BNNW1`, `BNSW1`, `BNW1`, `RPNE1`, `RPSE1`), **6 senza gemello** (`CZE1`, `MCE1`, `PYSW2`, `RFS4`, `RFE2`,
  `RPSW1`), 4 col codice ripetuto nel `.fix` (`MJNW1`, `PKS1`), **8 fix orfani**. La carta diceva 496/9/81/7: le 81
  «senza» erano 76 secondi campi che non sono codici e 5-6 codici veri; i refusi di scrittura di `PXSW1` e `RNNE1` si
  leggono nella stessa posizione (confronto per posizione) e contano uguali. Spostamento vero di ognuno dei 495, poi
  annullato: gemello uguale dopo, due file toccati, **0 guasti**, 29 s. Prova a schermo sul banco: COLOMBO spostato →
  «1 modifica in 2 file», `VFR_NASCOSTI.fix −1 +1` (e il refuso `E01221856000` di COLOMBO/RFS3, R-10, si corregge in tutti
  e due); CAPO DUE RAMI → «+ Crea il gemello RFS4» → `RFS4;N0414634000;E0121642000;3;` fra RFS3 e RFW1, compatto come il
  file; OSTIA tolto → la domanda → «Sì» → `VFR_NASCOSTI.fix −1 +0`; «Annulla tutto» pulito. Il motore non è toccato.
  Test: Lab 698 → **709**.

**Slice 8 chiusa.** Uscita misurata sul fork: la stessa forma trovata dal Lab come anello (**1 971 famiglie** uguali;
settori dinamici 98 con una copia uguale e 17 solo diverse; **1 594 `.pol` col bordo uguale** in un `.geo`), la famiglia
dichiarata `form=` con l'avviso `FormeDiverse`, la forma portata sulle copie dopo ogni gesto (**613 forme, 617 copie
portate, 0 guasti**, anche le linee dei `.geo` come copia e come sorgente), «Allinea quella» e «Prendi la sua», il bordo e
il riempimento che mancano (159 e 105 proponibili) con l'avviso `ConfineSenzaErba` (0 sul fork), i gemelli `.vfi` ↔
`VFR_NASCOSTI.fix` (**495 uguali**, spostati e annullati tutti, 0 guasti). Codice comune toccato: due regole nell'elenco
del motore (`FormeDiverse`, `ConfineSenzaErba`), calcolate dal Lab. Voci del giro dei file chiuse: D5, J3, Q5, I2, H10,
F2 (l'adozione in massa delle famiglie, D6, e le 17 divergenti sono di F4). Da provare a mano (eseguibile da
ripubblicare): prove in `SectorLab-prova\PROVE.md`.

**Slice 9 — procedure (dal 29 settembre).** Divisa in: **9a** i controlli (P3, Q6, R-4) · **9b** la vista per pista e
tipo e la scheda della voce (Q1, P2) · **9c** i metadati delle procedure e il fix proposto dal nome (P6, P7, Q2b, Q2d) ·
**9d** i metadati per punto (Q2, P11) · **9e** i legami STAR → attesa → IAP → GA (Q2c).

- **9a (29 settembre)** — i controlli delle procedure nel motore (`Validazione/ControlloDelleProcedure.cs`, nella
  validazione dell'albero). Codice comune toccato: quattro regole nuove.
  - **`ProceduraRipetuta`** (avviso): lo stesso scalo, piste, nome e tipo due volte nello stesso file (P3).
  - **`TipoFuoriPosto`** (errore): il 6° campo di una SID non è il tipo 0/1 — un campo manca prima e il navaid finisce
    lì (`LIBV;14L:14R;VICTOR6A; ;0;VICTOR;`, `LIMN;17:35;SID1; ; ;TOP;`): Aurora legge la riga sbagliata (P3).
  - **`VoceDiUnAltroScalo`** (avviso): la voce di un altro scalo nel file di uno scalo (Q6, R-4; solo nei file con un
    nome di scalo di quattro lettere, non in `lirr.str`/`lizz.str` che sono raccolte). Prende anche `limf.sid:28`
    (`LIMF18;TOP1B LAG2L;…`: il `;` mancante dopo lo scalo).
  - **`PistaInesistente`** (avviso): un verso che lo scalo non ha nei `.rw` (Q6, R-4). Scelta dell'agente, dalla
    misura: si controllano solo i campi con la forma di un verso (due cifre e L/R/C). `MAPS` combinato con una pista
    (`35:MAPS`, 72 STAR: la procedura sta nel menu della pista e in quello delle mappe), `NE`/`SU` di `lirr.str` e
    `BULL`/`AAR` di `lizz.str` sono gruppi del menu, non piste; uno scalo senza piste nei `.rw` non si controlla.
  - Già c'erano: `CoppiaDecimale` (i 38 punti decimali di `liba.str`), `DueNomiDiversi` (`ALPHA SOUTH;ALPHA SUOTH`).
  Misura sul fork (`scratchpad/misura9`): 1 306 SID e 1 505 voci STR; **11 SID ripetute** (5 di LIMC 35R, `lieo`,
  `lipq`), **12 SID col tipo fuori posto** (`libv` VICTOR, `limn` SID1/2/4, `lirl` PEMAR ×4, `lirm` VEGIM, `limf:28`; la
  carta ne contava 28 con un'altra lettura), **2 voci di un altro scalo** (`limf.sid:28`, `licz.str` LICC), **4 piste che
  lo scalo non ha** (`licz.str` LICC 10L:10R, `lipi.str` 06:24 con 06L/06R, `lirl.str` HLD-IRDUN e HLD-LAT su 05:12),
  STR ripetute 0. Il validatore sull'albero passa da 120 errori e 543 avvisi a **132 errori e 560 avvisi**; round-trip
  invariato. Test: motore 676 → **680**, Lab 709.
- **9b, primo passo (29 settembre)** — la scheda delle procedure scrive la coda della testa (P1, Q1). Codice comune
  toccato: `SidProcedure.IsRnav` (8° campo nel modello: prima lo teneva solo la fusione, come campo sconosciuto),
  `StrSaver` fino all'8° campo (transizione e RNAV, prima si fermava al 6°), `StrRecord.TipoNonScritto`,
  `StrParser.Rnav`, `FusioneDelRecord` (il commento in coda resta in coda).
  - Nella scheda: RNAV delle SID e delle voci `.str` (sì/no), navaid della transizione degli `.str` coi suggerimenti; nel
    `MAPS` il 6° campo si chiama **«Si accende col tasto»** e vale STAR/TRANS/HOLD/IAP/FAP/GA (mai un errore).
  - 🔴 **Trovati scrivendo, due difetti del motore di prima**: (1) il tipo vuoto o assente di una voce `.str` (519 teste
    RNAV `LIRF;16L:16R;ELKA3A;;;;;1;` e 215 teste corte) si riscriveva `0`: la testa non si riconosceva più nella
    fusione e **una testa cambiata usciva due volte** (valeva già per il nome cambiato dalla scheda). Ora il tipo resta
    vuoto finché resta STAR. (2) In `limc.sid` il commento in coda cade nell'8° campo (`…;0;AOSTA; //SUPER-HEAVY-A321`):
    letto come «RNAV no», riscritto `0`, stessa testa doppia (16 righe in più); e l'RNAV scritto avrebbe preso il posto
    del commento. Ora l'RNAV si legge solo da `1`/`0`, e la fusione tiene il commento in coda in coda.
  - Misura nuova nella prova sull'albero, **UNA TESTA PER PROCEDURA**: l'RNAV di ogni SID e voce `.str` invertito →
    **2 811 righe cambiate su 2 811 procedure, 0 file fuori misura** (prima delle correzioni: `limc.sid` 73 → 89 righe).
    Il resto invariato (round-trip 701/701, tutto toccato 0, una modifica per record 115 568, tag, blocchi, validatore
    132/560); «spostati sopra» 52 → 53 file su 54.
  Test: motore 680 → **688**, Lab 709 → **715**.
- **9b, secondo passo (29 settembre)** — la vista per pista e tipo (Q1) e la procedura nuova nel gruppo della sua
  pista (P2). Codice comune toccato: `RecordNuovo.Aggiungi`.
  - Sotto un `.sid` o un `.str` le voci sono **per pista e tipo**, come la finestra delle procedure di Aurora: `16L ·
    STAR`, `16L · HOLD`, `07 · IAP`…, le piste nell'ordine del file, i tipi nell'ordine dei tasti; il `MAPS` in fondo,
    per il tasto che accende la mappa (`MAPS · tasto FAP`). Una procedura su più piste (`16L:16R`) sta sotto ognuna,
    come nel menu di Aurora filtrato per pista attiva; una su pista e `MAPS` (`07:MAPS`) sotto tutte e due. Nelle SID
    il tipo vuoto e lo 0 sono «SID», l'1 «transizioni». Le voci di menu che non sono piste (`NE` di `lirr.str`) valgono
    come piste. Nella scheda la procedura dice tutte le sue voci («16L · STAR, 16R · STAR»).
  - **«+» su ogni voce** (P2): copia l'ultima procedura scritta per la sola pista della voce (sennò l'ultima della voce)
    e la mette sotto di lei. Scelta dell'agente: il nuovo nasce uguale al modello (nome compreso: l'avviso
    `ProceduraRipetuta` lo dice finché non si cambia), come «+ Record come questo».
  - 🔴 **Trovati a schermo sul banco**: (1) il lettore degli `.str` lascia nelle righe di una voce la riga vuota e il
    commento della voce DOPO; il record nuovo, messo sotto, **si prendeva il commento dell'altra** (`//LIRF RNP RWY07`
    passava da `RNP07(CMP)` al nuovo). Valeva per ogni «+ Record come questo». Ora la coda del vicino dalla prima riga
    vuota dopo i suoi dati passa sotto il nuovo, e la riga vuota si ripete fra i due; un punto commentato attaccato ai
    dati resta del vicino. (2) Il nome della «parte» di una procedura si offriva come commento da scrivere, e sarebbe
    finito in cima al file: per le procedure il nome è quello dei dati (campo «Nome»).
  - Misura sull'albero, **«+ record come questo» su ogni file** (il primo, quello a metà e il penultimo come vicini, 637
    file, 1 833 prove; `scratchpad/misura9b`): 1 804 buone, **0 righe di prima perse o spostate**; i 29 casi dove il
    nuovo riletto si attacca al vicino (`.vrt` e aerovie, che separa la chiave, e due `.artcc`) sono **gli stessi col
    codice di prima**. La coda passa sotto il nuovo in 186 prove in più (109 → 295 con righe vuote in più). Prova
    sull'albero invariata.
  Test: motore 688 → **691**, Lab 715 → **720**.
- **9c (29 settembre)** — i metadati delle procedure (P6, P7, Q2b, Q2d). Solo Lab, niente codice comune: il catalogo
  dei tag c'era dalla slice 1, le chiavi fix/trans/salita/scia/categorie con i loro editor dalla 3d (prova 68).
  - **Le chiavi del genere di voce** (scelta dell'agente): il catalogo dei `.str` resta uno — è il contratto con vIPI,
    che le legge tutte —, ma la scheda propone a una mappa del `MAPS` solo le chiavi delle mappe (composte, famiglia di
    forme, limiti e classe), a una STAR quelle delle SID senza la salita iniziale, a IAP/FAP/GA in più tipo, minimi e
    pendenza. Le chiavi che un record ha già si vedono comunque.
  - **Scelte chiuse**: `nav` da RNAV1 / RNP1 / RNP APCH (Q2b), `type` da ILS / LOC / RNP / VOR / NDB (Q2d); un valore
    fuori elenco già nel file si vede e resta. **Forme**: `mins` categoria:piedi nell'ordine A-E
    (`a:450, b:450` → `A:450,B:450`), `gp` in gradi da 1 a 10 (`3` → `3.0`); il resto si rifiuta col perché.
  - **Fix proposto dal nome** (P7, `FixDalNome`): la radice del nome (il punto prima di numero e lettera, `EKLO8R` →
    `EKLO`, `OST1E` → `OST`, la prima parte dei nomi composti) cercata fra fix, VOR e NDB del master scelto — il nome
    uguale, o un nome di cinque lettere che comincia con la radice di tre o quattro — entro **150 NM dallo scalo**, il
    più vicino prima. Nella scheda, sotto «Fix intero»: «dal nome: EKLOS» (un clic scrive `fix=`), o «dal nome,
    scegli:» coi candidati. Misura sul fork (`scratchpad/misura9c`, ITALY.isc): **SID 1 305 → 1 081 con un candidato
    (265 col nome già navaid), 114 da scegliere, 50 senza, 60 nomi senza radice** (militari, luoghi); procedure `.str`
    su pista: 619 / 4 / 3 / 26. La carta diceva 982 / 277 / 48: il raggio dallo scalo toglie gli omonimi lontani.
    Scelta dell'agente: è una proposta a un clic, non una scrittura in massa.
  - **«a tutta la voce»** (P6, «valori per pista come gesto»): accanto a ogni metadato scritto di una procedura, porta
    lo stesso valore alle altre della sua voce (stessa pista, stesso tipo) in un gesto solo della storia.
  Test: Lab 720 → **745**.
- **9d (29 settembre)** — i vincoli dei punti (Q2, P11). Codice comune toccato: `Metadati.RigheDeiPunti` (le righe di
  punto di un record, per la scheda; il lettore e lo scrittore dei `//@@` c'erano dalla slice 1c).
  - Nella scheda di una procedura (SID col tracciato, voce `.str` su una pista; non le mappe del `MAPS`) la sezione
    **«Vincoli dei punti»**: ogni punto (senza quelli commentati) con ruolo da elenco (IAF, IF, FAF, MAPt), quota e
    velocità. Si scrivono nel `//@@"PUNTO" …` sopra il punto; un punto si riconosce dal suo numero fra le righe di
    punto, che non cambia quando un tag si aggiunge sopra un altro. Tolte le chiavi il tag sparisce.
  - Le forme di §M: quota `+FL80` (a o sopra), `-5000` (a o sotto), `=4000` (a), `4000/6000` (fra), in piedi o FL;
    **il segno è obbligatorio** (scelta dell'agente: un numero da solo non dice se è un minimo, un massimo o la
    quota); velocità in nodi col segno, `-210`, da 60 a 400.
  - **Al passaggio del mouse, mai in Aurora**: la forma della mappa porta i vincoli (`BIBEK role=IAF alt=+FL80`), il
    suggerimento li mostra sotto il nome (ora come testo, non come HTML: i nomi vengono dai file), e scrivere un tag
    di punto rifà la forma. Provato sul banco (fork, `lirf.str` ELKA3A).
  Test: motore 691 → **692**, Lab 745 → **764**. Prova sull'albero invariata.
- **9e (29 settembre)** — i legami fra le procedure della stessa pista (Q2c). Codice comune toccato:
  `Validazione/LegamiDelleProcedure.cs` e la regola **`StarSenzaAvvicinamento`** (avviso) in `ControlloDelleProcedure`.
  - Nello stesso `.str`, sulla stessa pista vera, per nome: la **STAR** porta all'**attesa di scalo** e
    all'**avvicinamento** che passano dal suo ultimo punto; l'attesa all'avvicinamento che passa dal suo punto;
    l'avvicinamento al **mancato avvicinamento** che comincia dove lui finisce (o da un suo punto). ❌ Niente legame con
    `HOLDENR.hold` (attese in rotta). Nella scheda la sezione **«Legami»**: «da» e «verso», col tipo, la pista e il punto;
    un clic sceglie l'altra voce. Sul banco: `lirf.str` RITE2K (07:25) → RNP07(CMP), RNP25 e le due HLD-CMP, «a CMP».
  - Misura sul fork (`scratchpad/misura9e.py`): 645 STAR su una pista coi punti per nome, 602 su piste che hanno un
    avvicinamento; in **533** un avvicinamento della pista comincia dal loro ultimo punto, in **552** ci passa, in 546
    c'è un'attesa lì. GA su una pista: **0** nel fork (le «GA» sono le ATZ del `MAPS`).
  - L'avviso vale per la STAR la cui pista ha avvicinamenti ma nessuno passa dal suo ultimo punto. Scelta dell'agente,
    dalla misura: «ci passa» e non «comincia da lì» (una STAR può finire all'IF, dentro l'avvicinamento) — **50** STAR
    invece di 69 (`libv.str` HITACX…(ATC) → GIO, `lica.str` → SUGEP, `limj.str` → SES, le REC militari di `lied.str`…);
    una pista senza avvicinamenti nel file non si controlla. Validatore sull'albero: 132 errori, 560 → **610 avvisi**.
  Test: motore 692 → **694**, Lab 764 → **765**.

**Slice 9 chiusa.** Uscita misurata sul fork: **SID ripetute 11**, **tipo fuori posto 12** (la carta ne contava 28 con
un'altra lettura), `limf.sid:28` preso da `TipoFuoriPosto` e da `VoceDiUnAltroScalo`, `licz.str` LICC, i 38 punti
decimali di `liba.str` e `ALPHA SUOTH` (regole di prima, `CoppiaDecimale` e `DueNomiDiversi`); il fix dal nome **1 081
con un candidato / 114 da scegliere / 50 senza** (la carta: 982 / 277 / 48, senza il raggio dallo scalo); **50 STAR**
che finiscono dove nessun avvicinamento passa; **2 811 teste** riscritte una riga ciascuna. Trovati e corretti, nel
motore, tre difetti di prima: la testa `.str` col tipo vuoto che usciva due volte, il commento in coda di `limc.sid`
letto come RNAV, il record nuovo che si prendeva il commento della voce dopo. Voci del giro dei file chiuse: P1-P3, P6,
P7, P11 (catalogo e vincoli; i valori dai PDF sono di F7), Q1, Q2, Q2b, Q2c, Q2d, Q6, R-4. Validatore sull'albero:
**132 errori, 610 avvisi**. Test: motore 676 → **694**, Lab 709 → **765**. Da provare a mano (eseguibile ripubblicato
`f27d4ed4`): prove 116-129 in `SectorLab-prova\PROVE.md`.

**Consegna agli AOD (29 settembre).** Decisione del committente: dopo la slice 9, una prima prova degli AOD. Il Lab ha
preparato lo zip `AuroraSectorLab-prova-2026-09-29.zip` (64 MB, fuori dal repo in `IVAO_Test\SectorLab-consegna\`):
`SectorLab\` = l'eseguibile di `f27d4ed4` (senza `PROVE.md` e senza i `.pdb`), `Sector\` = i `SectorFiles` del fork
`it-aurora-sector-test` al commit `8cf32c6` presi con `git archive` (niente modifiche locali) più gli schemi colore di
Aurora, `LEGGIMI.txt` con le istruzioni. Estratto e provato (autoprova 0). Il sector del fork, e non l'ufficiale, l'ha
confermato il committente; lo zip lo manda lui. Le segnalazioni degli AOD entrano come prove o voci nuove. Poi
l'ondata 2 continua con la **slice 10** (NAVAIDS e attese).

**Slice 10 — NAVAIDS e attese (dal 29 settembre).** Manuale IVAO riletto (*Aurora Sectorfile Creation Manual*,
`[FIXES]`, `[NDB]`, `[VOR]`, `[HOLDENR]`): fix `Nome;Lat;Lon;Tipo;Confine;` con l'attesa nel 6° campo (il testo dice
«5th», l'esempio la scrive nel 6°, come il sector); NDB `Nome;kHz;Lat;Lon;[Visibilità];` con l'attesa nell'**8°**
(`ALP;351.0;…;;;;ABC HOLD;`); VOR `Nome;MHz;Lat;Lon;[Visibilità];[Tipo];[Canale TACAN];` e l'attesa nell'8°; un'attesa
`NOME;Lat;Lon;[Info];` può essere **una sequenza di punti** con lo stesso nome (l'ovale), l'info è facoltativa e
ammette `<br>`, il nome può avere spazi (`ABC HOLD`). Divisa in: **10a** il motore legge i campi che mancavano ·
**10b** i controlli · **10c** le schede (attesa collegata nei due versi, info a campi, nome unico).
- **Misura sul fork** (`scratchpad/misura10.py`, fork `8cf32c6`): fix 1 256 in `itfix.fix` (54 con l'attesa), 641
  `ESTERNI`, 186 `MIL`, 781 `APT`, 512 `VFR_NASCOSTI`, 527 `secsi`; **2 032 fix senza il campo confine** (Aurora li
  legge) e **9 senza il tipo** (`VFR_NASCOSTI`), un tipo `3:` (`APT.fix`); VOR 122 (32 nascosti, 26 col canale
  TACAN, 14 con l'attesa); NDB 27 (7 nascosti, nessuno con l'attesa). **Attese**: 68 definite, un punto ciascuna, info
  tutte nella forma `FIX/rotta+virata-quota` (53 in piedi, 15 FL); 68 citate; **una sola sbagliata**: `EKLAP` cita
  `HLD-ELKAP`, e l'info di `HLD-EKLAP` dice `ELKAP/090R-FL190` (quel fix esiste, a 143 NM). Righe illeggibili: le 5
  della carta più `PL-BRAVO` (secondi 72) e `itvor.vor:109` (secondi 75 e 99) — le prende già la slice 2 con la
  correzione proposta. Nomi: dentro un catalogo 5 in posizioni diverse (`SARKI` 556 NM, `BV-BRAVO`, `PKS1`, `MJNW1`,
  `ABNAT`) e 285 nello stesso punto — sono già `NomeDuplicato`/`NomeRipetuto`; la carta ne contava 24 e 245 con
  un'altra lettura. **Fra cataloghi**: 17 VOR e NDB con lo stesso nome, 10 a più di 0,1 NM (`AVI` 9 NM, `RIV` 7,
  `PIS` 6,7, `FAL` 4,5, `TRP` 1,5…). 🔴 `HOLDENR.hold` lo carica **solo** `ITALY.isc`: i quattro master di FIR citano
  le 68 attese dai loro fix e non le hanno.
- **Decisioni del committente (29 settembre)**: (1) le attese nei master di FIR → **un avviso per master** (4), e il
  controllo attesa per attesa solo nei master che caricano `[HOLDENR]`; (2) VOR e NDB con lo stesso nome → **avviso
  oltre 0,1 NM** (10), e una prova in Aurora (F4) per sapere quale dei due prende una procedura che scrive `PIS;PIS;`.
- **10a (29 settembre)** — il motore legge i campi che mancavano. Codice comune toccato: `Fix.NomeDellAttesa` (6°),
  `Vor.CanaleTacan` (7°) e `Vor.NomeDellAttesa` (8°), `Ndb.Visibilita` (5°), `Ndb.ExtraField6`/`ExtraField7` (il
  manuale non li descrive) e `Ndb.NomeDellAttesa` (8°), tutti com'erano scritti; lettori e scrittori, e
  `CampiFacoltativi` (un campo facoltativo scritto dopo uno che manca lascia vuoto il posto: `ALB;116.95;…;;;;HLD-ALB;`).
  Prima questi campi stavano nella riga come campi sconosciuti (la fusione li teneva, la scheda non li vedeva). Nel Lab
  le schede di fix, VOR e NDB li mostrano: **Attesa** coi suggerimenti delle attese di `HOLDENR.hold` (fonte nuova degli
  elenchi), **Canale TACAN**, **Visibilità** «0 mostrato / 1 nascosto» anche per i VOR. Prova sull'albero invariata
  (round-trip 701/701, validatore 132/610). Test: motore 694 → **705**, Lab 765 → **766**.
- **10b (29 settembre)** — i controlli (L3, L4, U1). Codice comune toccato: `Validazione/ControlloDelleAttese.cs`
  (per ogni master, nella validazione dell'albero), `CampiDelNavaid` nel validatore dei file, sette regole nuove.
  - **`AttesaNonDefinita`** (errore): un fix, VOR o NDB cita un'attesa che non è in `[HOLDENR]`; se c'è quella col suo
    nome, `HLD-<nome>`, la **propone** (`EKLAP;…;HLD-ELKAP;` → `HLD-EKLAP`).
  - **`AttesaMaiCitata`** (avviso): un'attesa che nessuno cita, e chi porta il suo nome cosa cita.
  - **`AttesaFuoriPosto`** (avviso): il punto dell'attesa a 0,1 NM o più dal navaid che la cita, o l'info che nomina un
    fix lontano dal punto; se nel punto c'è il fix col nome dell'attesa, **propone** l'info con lui
    (`ELKAP/090R-FL190` → `EKLAP/090R-FL190`). Un nome ripetuto in più file vale nel punto più vicino (senza questa
    scelta, 2 falsi avvisi: nomi che `ESTERNI.fix` ripete lontano).
  - **`AtteseNonCaricate`** (avviso, uno per master, decisione del committente): il master carica navaid che citano
    attese e non `[HOLDENR]`. Il controllo attesa per attesa si fa solo nei master che le caricano.
  - **`NomeInPiuCataloghi`** (avviso, decisione del committente): lo stesso nome fra fix, VOR e NDB a 0,1 NM o più, sul
    secondo nell'ordine fix, VOR, NDB. Estende `NomeDuplicato`/`NomeRipetuto`, che guardano un catalogo alla volta.
  - **`CampoMancante`** (avviso): il fix senza il tipo. **`ValoreFuoriElenco`** (avviso): tipo del fix fuori da 0-3,
    confine e visibilità fuori da 0/1, tipo del VOR fuori da 0-4 (🔴 il `3:` di `APT.fix:407` il motore lo leggeva 0,
    in rotta, invece che nascosto). Il confine che manca non si dice (2 032 fix: Aurora li legge).
  Uscita sul fork: **1** attesa non definita, **1** mai citata, **1** fuori posto (le tre di `EKLAP`), **4** master
  senza attese, **10** nomi VOR/NDB, **9** fix senza tipo, **1** tipo fuori elenco. Validatore sull'albero: 132 errori,
  610 avvisi → **133 errori, 636 avvisi**; le correzioni proposte 169 → 171, applicate a una copia le 165 righe tornano
  pulite. Round-trip invariato. Test: motore 705 → **711**.
- **10c (29 settembre)** — le schede (L1, L3, U1). Codice comune toccato: `Attesa.Fix`, `Rotta`, `Verso`, `Quota` si
  scrivono (ognuna ricompone l'info; un valore che non va è un `ArgumentException`, un'info fuori forma un
  `InvalidOperationException`).
  - **Attesa in rotta** nella scheda di un fix, VOR o NDB: il nome scritto nel suo campo porta alla definizione in
    `HOLDENR.hold` (un clic la sceglie, con l'info accanto); se non c'è lo dice e, se c'è quella col nome del punto,
    **«Usa HLD-EKLAP»** la scrive. L'altro verso c'era già: «Chi lo usa» dell'attesa elenca chi la cita.
  - **La scheda dell'attesa** scrive l'info a campi: fix (coi suggerimenti), rotta (1-360, tre cifre), virata da un
    elenco (L a sinistra, R a destra), quota (`9000` o `FL105`). Un valore sbagliato si rifiuta col perché, senza
    toccare il record (il Lab ora rifiuta col perché ogni valore che il modello del motore non accetta). Un'info fuori
    forma si scrive tutta nel campo Info e le parti restano in sola lettura.
  - **Nome unico** (scelta dell'agente): nella scheda il nome di un fix, VOR, NDB o attesa si scriveva come un campo
    qualunque — cambiava il nome senza riscrivere chi lo cita e senza guardare se c'era già. Ora, se il punto è
    citato, il campo rimanda a «Rinomina» in «Chi lo usa» (che riscrive le citazioni); se non lo è (un record appena
    nato) si scrive lì, ma non col nome di un altro punto (lo stesso controllo della rinomina).
  - Provato a schermo sul banco (fork pulito): `EKLAP` dice che `HLD-ELKAP` non c'è, «Usa HLD-EKLAP» cambia una riga
    di `itfix.fix`, il clic porta all'attesa, il fix dell'info scritto nel suo campo cambia una riga di `HOLDENR.hold`
    (`EKLAP/090R-FL190`); `9000ft` e il nome dell'attesa citata si rifiutano col perché. Annullato, copia intatta.
  Test: motore 711 → **720**, Lab 766 → **771**.

**Slice 10 chiusa.** Uscita misurata sul fork: `EKLAP` → `HLD-ELKAP` preso da tre regole, con le due correzioni
proposte che lo sistemano; **4** master senza `[HOLDENR]`; **10** nomi VOR/NDB lontani (la carta contava 24 nomi in
posizioni diverse e 245 doppioni: con la lettura di oggi sono 5 + 285 dentro un catalogo, già dati dalla slice 2, e 10
fra cataloghi); **5 + 2** righe illeggibili, già con la correzione proposta dalla slice 2; **9** fix senza tipo e il
`3:` letto come «in rotta». Voci del giro dei file chiuse: L1, L3 (il controllo; la pulizia è di F4), L4, U1.
Validatore sull'albero: **133 errori, 636 avvisi**. Test: motore 694 → **720**, Lab 765 → **771**. Da provare a mano:
prove 130-137 in `SectorLab-prova\PROVE.md`. Resta per F4: la prova in Aurora di quale fra VOR e NDB omonimi prende un
punto per nome.

**Slice 11 — OTHER e PREFS (dal 29 settembre).** Manuale IVAO riletto (`[ATC]`, `[AIRPORT]`, `[RUNWAY]`): nei
trasferimenti una voce è una posizione o un **ICAO** (tutte le sue posizioni), prima gli inclusi e poi gli esclusi
(«DO NOT use an INCLUDE definition after EXCLUDE definitions! It will not work»); il 7° campo è il file **`.loa`**
(«LOA & XFL»: livelli di trasferimento per punto e settore); la rotta della pista è **magnetica**; nascosto e tipo
dello scalo facoltativi. Divisa in: **11a** le posizioni (`.frq`) · **11b** scali e piste · **11c** il CPDLC · **11d** i
profili `.cpr` e i PAR.
- **Misura dei `.frq`** (`scratchpad/misura11a.py`, chat `2b142de2`): include dopo un escluso in **102** posizioni (51
  `itfreq`, 15 `libb`, 3 `limm`, 22 `lipp`, 11 `lirr`); citate e mai definite, fra le italiane, `LIMJ_APP`, `LIBB_APP`,
  `LIMM_WN4_CTR`, `LIMM_EN4_CTR` (le straniere, `LFMM_S_CTR`, `LDZO_CTR`…, sono normali); `LIMF_WN0_APP` due volte in
  `itfreq.frq` e in `limm.frq`, con trasferimenti e profilo diversi; il 7° campo vuoto ovunque; frequenze tutte
  `nnn.nnn`; un solo file citato che non c'è (`PREFS\LIPC.cpr`, già `FileCitatoAssente`).
- **Decisione del committente (29 settembre)**: l'include dopo un escluso è un **errore**, con il riordino proposto.
- **11a (29 settembre)** — le posizioni (M1, M2, N4). Codice comune toccato: `AtcPosition.Loa` (7° campo; prima un
  segnaposto sempre vuoto), `AtcPosition.Inclusi`/`Esclusi` (i trasferimenti in due liste: scriverne una rimette prima
  gli inclusi e poi gli esclusi; un «;» o un «-» fra gli inclusi si rifiuta col perché), `FrqParser`/`FrqSaver`,
  `Validazione/ControlloDellePosizioni.cs` e tre regole; `CopieGemelle` non confronta le due liste (le dice già
  `TransferList`).
  - **`IncludeDopoEscluso`** (errore) con la riga riordinata proposta; **`PosizioneNonDefinita`** (avviso) per una
    posizione italiana (`LI…_…`) che nessun `.frq` definisce; **`PosizioneRipetuta`** (avviso).
  - Nella scheda della posizione: **Trasferimenti: inclusi** e **esclusi**; profilo, ATIS, LOA e D-ATIS scelti fra i
    file dell'albero (fonti nuove degli elenchi), con **«apri»** accanto a un file che c'è (N4: il profilo si apre
    nell'elenco dei file) e «non c'è nell'albero» accanto a uno che manca. Il nome di una posizione citata si cambia
    con «Rinomina», come quello dei punti (10c).
  Uscita sul fork: **102** errori, tutti con la correzione che, applicata a una copia, torna pulita; **32** avvisi di
  posizioni mai definite (le righe che citano le quattro); **2** ripetute. Validatore sull'albero: 133/636 → **235
  errori, 670 avvisi**. Round-trip invariato, copie gemelle invariate (29). Test: motore 720 → **729**, Lab 771 → **775**.
- **11b (29 settembre)** — scali e piste (M3, M4, M9, M10). Misura (`scratchpad/misura11b.py`): rotte con decimali
  **107** righe (44 in `itrw.rw`: `109.5`, `065.49`, `345.9`…); verso primario oltre il 18 **80** righe (34 in
  `itrw.rw`; la carta ne contava 13); rotta scritta meno rotta vera dalle soglie sempre fra **−5° e +2°** (la
  declinazione: la rotta del file è magnetica), tranne **LIDW 15** (149° contro 15°) e **LIKL 36** (360° contro 180°).
  Le voci `MAPS` e di settore stanno nelle sezioni `//MENU MAPPE` e `//ACC`: per il motore sono righe del file, non
  piste, e nel Lab non si confondono con le piste (M4, la parte «voci di menu»). Codice comune toccato:
  `Runway.RottaVeraDalleSoglie` (calcolata, al decimo di grado), `CorrezioneDelleCoordinate` (per un `.rw` la riga
  corretta arrotonda le rotte e, col primario oltre il 18, scambia i versi — numero, elevazione, rotta, soglia — e
  scrive la reciproca se la rotta che diventa primaria manca: `LIMW 27/09` «261;;»), tre regole nel validatore dei file.
  - **`RottaConDecimali`** (avviso) e **`PrimariaOltre18`** (avviso), tutte e due con la riga corretta proposta (la
    stessa, se la riga ha tutti e due i problemi); **`RottaDiversaDalleSoglie`** (avviso, oltre 15°: nessun falso
    avviso sul fork) senza proposta: quale sia giusto, le soglie o la rotta, lo decide l'AOD.
  - Nel pannello dei problemi, sulla prima voce di un gruppo, **«Correggi tutte le N di <file>»**: ogni riga del file
    con quel problema diventa la sua proposta, in una voce sola della storia (il gesto «arrotonda al grado» su riga o
    file di M4). Nella scheda della pista **«Rotta dalle soglie»** accanto alla rotta scritta.
  - Scheda dello scalo (M3: nascosto e tipo) e metadati della pista per verso (M9) e dello scalo (M10): c'erano già
    dalle slice 3b e 3d.
  Uscita sul fork: **107 + 80 + 2** avvisi; correzioni proposte 273 → 460, applicate a una copia tornano pulite tutte
  tranne `LIKL` (le soglie invertite restano: il Lab non indovina quale dato è sbagliato). Validatore sull'albero:
  235/670 → **235 errori, 859 avvisi**. Round-trip invariato. Test: motore 729 → **734**, Lab 775 → **778**.
- **11c (29 settembre)** — il CPDLC (M5). Manuale IVAO «Aurora CPDLC Sectorfile»: un messaggio è `Comando;Risposta;
  Gruppo;` più tredici campi di valori che prepara uLink (ReplVal, AtVal, TpVal, TotVal); comando al più 128 caratteri
  coi valori `[0]`…`[3]`; risposta WU, AN, R, NE; gruppi 0-18, il 20 per il DCL; i nomi `GROUP.ID;NOME;`, al più 20
  caratteri. Misura: 155 messaggi, tutti a 16 campi; il gruppo 15 si chiama «TWR» e non ha messaggi; 3 senza risposta,
  tutti pezzi del DCL (gruppo 20: `CLIMB [0]`, `SQK [0]`, `ATIS INFO [0]`); 2 dichiarano più valori dei `[n]` del
  testo (`WHEN CAN YOU ACCEPT [0]` 2, `REPORT PASSING [0]` 3). Codice comune toccato: lettori e scrittori nuovi
  `.cpdlc` e `.cpdlcnames` (`MessaggioCpdlc`, `NomeDelGruppoCpdlc`; i campi dei valori si tengono come sono),
  `Validazione/ControlloDelCpdlc.cs` e tre regole.
  - **`GruppoSenzaMessaggi`**, **`MessaggioSenzaRisposta`** (non nel gruppo 20: scelta dell'agente dalla misura, i tre
    pezzi del DCL non l'hanno), **`ValoriDelMessaggio`** (TotVal diverso dai `[n]`: scelta dell'agente, si rifà con
    uLink), e `ValoreFuoriElenco` per risposta, gruppo e lunghezze fuori dal manuale. Tutti avvisi.
  - Nel Lab la scheda del messaggio (risposta e gruppo da un elenco col significato, «20 · DCL»; i valori di uLink in
    sola lettura) e quella del nome del gruppo; l'elenco dei record dice il messaggio.
  Uscita sul fork: **1** gruppo senza messaggi, **0** senza risposta, **2** valori diversi. Round-trip **703/703** (i
  due file ora hanno un lettore), tutto toccato 0. Validatore sull'albero: 235/859 → **235 errori, 862 avvisi**. Test:
  motore 734 → **739**, Lab 778 → **779**.
- **11d (29 settembre)** — i profili `.cpr` e i PAR (N1-N3). Misura (`scratchpad/misura11d.py`): i quattro generici e
  `WW0` con 2-4 impostazioni; dieci profili PAR con 1-6 finestre (`[INSETn]`, 15 chiavi `INSnPAR_…`); radiale entro
  ±0,2° dalla rotta del `.rw` nazionale ed elevazione entro 1 ft ovunque (per `LIBV` il `.rw` nazionale dice 137 come
  il PAR, le copie delle FIR 138: lo dice già `CopieDiverse`); `LIPI RWY06` in `LIPI.cpr` e `LIPA.cpr` (il `.rw` ha
  06L e 06R). 🔴 Trovato misurando: in `LIBN.cpr` la sezione `[INSET3]` contiene le chiavi `INS4…` — un INI si legge
  per sezione e chiave, quindi per Aurora INSET3 è una finestra PAR vuota (è la «INSET3 senza didascalia» della carta)
  e quelle 13 righe non valgono. Codice comune toccato: lettore e scrittore nuovi dei `.cpr` (`ProfiloParser`,
  `ImpostazioneDelProfilo`: un record per riga `Chiave=Valore` con la sua sezione; intestazioni e commenti restano
  righe del file), `Validazione/ControlloDeiProfili.cs` e cinque regole.
  - **`ParSenzaPista`** (finestra PAR — `VIEW_TYPE=2` o chiavi `PAR_` — senza didascalia o con una pista che il `.rw`
    dello scalo non ha; uno scalo che il `.rw` non ha del tutto non si controlla), **`RadialeDelPar`** e
    **`ElevazioneDelPar`** (oltre ±0,2° e ±1 ft, col valore del `.rw` proposto), **`ProfiloConMolteImpostazioni`** (oltre
    20 fuori dai PAR: scelta dell'agente, i generici ne hanno 2-4), **`ChiaveFuoriSezione`** (chiavi di un'altra
    finestra, una per sezione). Tutti avvisi. Non sono avvisi, come deciso: profilo non usato, PAR di un altro scalo,
    righe prima delle sezioni.
  - Nel Lab la scheda dell'impostazione: sezione, chiave scelta dall'elenco delle chiavi di Aurora **per quella
    sezione** (risorsa `ChiaviDeiProfiliDiAurora.txt`: solo i nomi, 1 448 in 36 sezioni, l'unione dei 30 profili della
    cartella `Profiles` di Aurora ALPHA, più quelle scritte nei `.cpr` dell'albero), valore col suo significato — «🔴
    sovrascrive quella dell'utente a ogni connessione» e le chiavi del PAR spiegate. Per aggiungerne una: «+ Record come
    questo» e si sceglie la chiave. Il profilo si apre dalla scheda della posizione (11a, N4).
  Uscita sul fork: **3** finestre PAR senza pista (LIPI, LIPA, LIBN INSET3), **1** sezione con chiavi di un'altra, 0
  radiali, 0 elevazioni, 0 profili con molte impostazioni. Round-trip **718/718** (i 15 `.cpr` ora hanno un lettore),
  tutto toccato 0. Validatore sull'albero: 235/862 → **235 errori, 866 avvisi**. Test: motore 739 → **747**, Lab 779 →
  **781**.
- **Prova a schermo della slice 11** (banco, fork pulito): la scheda di `LIML_TWR` con inclusi ed esclusi e «apri»
  accanto a profilo, ATIS e D-ATIS; «apri» porta a `TWR.cpr`, le cui impostazioni hanno la loro scheda (187 chiavi
  suggerite per `[PREFS]`); nel pannello 102 `IncludeDopoEscluso` con «Correggi tutte le 51 di itfreq.frq», «…le 3 di
  limm.frq» riordina tre righe in una voce; `LIDW 15` «Rotta 149, rotta dalle soglie 14.9»; i problemi del CPDLC e dei
  profili dove la misura li aspettava. 🔴 **Trovato a schermo**: il campo «Trasferimenti (come sono scritti)» diceva
  «8 voci: Vipi.Sectorfile.Models.Transfer…» (c'era da prima: la scheda non sapeva scrivere un trasferimento); ora
  `LIML LIMM LIRO … -LIMC_MAR_APP …`. Annullato, copia intatta.

**Slice 11 chiusa.** Uscita misurata sul fork: **102** include dopo un escluso (la carta ne contava 51 in `itfreq.frq`:
con le copie delle FIR sono 102), tutti con la riga riordinata; `LIMM_WN4_CTR`, `LIMM_EN4_CTR`, `LIMJ_APP`, `LIBB_APP`
mai definite (32 righe); `PREFS\LIPC.cpr` e `\liml.atis` (il primo già `FileCitatoAssente`; il secondo Aurora lo
trova, resta in R-10); soglie invertite `LIDW 15` e `LIKL 36`; **107** rotte con decimali e **80** primarie oltre il 18
(la carta: 44 e 13, solo `itrw.rw` e un'altra lettura); CPDLC: il gruppo 15 senza messaggi, 0 senza risposta fuori dal
DCL, 2 valori diversi; PAR: `LIPI RWY06` ×2 e `LIBN` INSET3 (con le chiavi `INS4…` finite lì), radiali ed elevazioni
entro lo scarto. Voci del giro dei file chiuse: M1-M5, M9-M10 (scheda), N1-N4 (N2: la coerenza; la generazione è di F8).
Validatore sull'albero: 133/636 → **235 errori, 866 avvisi**. Round-trip **718/718** (nuovi lettori: `.cpdlc`,
`.cpdlcnames`, `.cpr`). Test: motore 720 → **747**, Lab 771 → **781**. Da provare a mano: prove 138-150 in
`SectorLab-prova\PROVE.md`.

**Slice 12 — Terra (dal 4 ottobre).** `lab/f3` allineato a `main` 1.56.0 (`33a08df2`). Manuale IVAO riletto (`[GEO]`,
`[FILLCOLOR]`, `[TAXIWAY]`, `[GATES]`, «Slots for Gates», «Referenced files»): il 5° campo di un `.geo` è il **colore**,
facoltativo (i «tipi» del fork sono nomi dello schema di Aurora); la testa di un poligono ha anche **opacità** (5°, 0/1)
e **filtro** (6°: COAST, RUNWAY, GATES, PIER, TAXIWAY, APRON, BUILDING), che il fork non usa mai; lo stand ha tipo
(L/M/H/S/G) e slot facoltativi; `ICAO.gts` e `ICAO.txi` si caricano da sé.
- **Misura** (`scratchpad/misura12.py` e `misura12b.py`, chat `2ac5b9f2`, fork `8cf32c6`):
  - `.geo` di scalo: 95 file, 69 199 segmenti, tipi come in carta; **10 vuoti** (`liap.geo`), nessuno sconosciuto; un
    solo gruppo con due tipi (`lipm.geo:8`, TAXIWAY e PIER).
  - `.pol`: 1 753 poligoni, tutti con 5 campi (opacità e filtro mai scritti), bordo sempre 1; bordo diverso dal
    riempimento in 8; colori tutti in `colors.def`. 🔴 **I «43 poligoni con meno di 3 vertici» della carta non si
    ritrovano**: oggi sono **2** teste senza vertici (`eo_ad_gnd.pol:72`, `ml_ad_gnd.pol:1223`), più `limmctr.tfl:1772`
    con 2 (li dice già `PoligonoConPochiVertici`). 🔴 **I «3 `.pol` senza `.geo`» nemmeno**: ogni `.pol` ha il `.geo` del
    suo scalo (93 su 93; due `.geo` non hanno il `.pol`).
  - 🔴 **Ordine di disegno (I3)**: quello deciso in carta (erba → cemento → piazzale → taxiway → pista → edifici → buchi)
    **non è quello del fork**. Solo 16 file su 93 lo rispettano (152 inversioni); l'ordine più comune, in 36 file, è
    erba → **taxiway → cemento → piazzale → edifici → pista** (BUILDING prima di RUNWAY in 64 casi, TAXIWAY prima di
    CONCRETE in 54, HOLE prima di BUILDING in 7). Torna al committente prima di scrivere la regola.
  - `RW_MARKINGS`: 34 file, 270 parti (una senza commento). **34 parti** non toccano una pista del `.rw` (oltre 40 m
    dall'asse): frecce della soglia spostata prima della pista, e `br_mark.geo` (LIBR) che disegna una **05/23 che il
    `.rw` non ha** (ha solo 13/31); pettine e numero stanno a una mediana di 38 m dalla soglia, 6 oltre 400 m (`LIBR`
    05/23, `LIBD` 07). 14 commenti citano una pista che lo scalo non ha. Il numero di strisce del pettine si conta
    (8, 12, 16 i più frequenti) ma la **larghezza della pista non è scritta da nessuna parte** (il tag `width` di M9 è a
    zero): il controllo «strisce per larghezza» oggi non ha con cosa confrontarsi.
  - `.txi`: 1 075 etichette; a una mediana di 1 m da un asse o da un bordo di taxiway, **27 oltre 100 m** (contro il
    solo asse 71; contro ogni linea del `.geo` 4); `LINB` in `libn.txi`; 3 in gradi decimali in `lipz.txi` (già dette).
  - `.gts`: 1 672 stand; tipo `M` in 48, slot mai; a una mediana di 750 m dall'ARP, mai oltre 2,4 km tranne lo stand di
    `LIBP` in `libg.gts` (344 km); `L3MC`, `L4MC` in `limc.gts`; 2 nomi ripetuti (`lied` «ARM/DEARM», `lipk` «404»).
- **12a (4 ottobre)** — i controlli della terra che non aspettano una decisione (H2, I4, R1, R3). Codice comune toccato:
  `Validazione/ControlloDellaTerra.cs` (chiamato da `ValidatoreDellAlbero`) e sei regole, tutte avvisi.
  - **`ScaloDiversoDalFile`** (stand o etichetta con l'ICAO di un altro scalo: entro 10 km dal centro dello scalo del
    file è un refuso e si propone l'ICAO del file; oltre è «nel file sbagliato», senza proposta), **`LontanoDalloScalo`**
    (oltre 10 km col suo ICAO: 5 km dava due falsi avvisi a LIRF, le etichette alla soglia 16L sono a 5,3 km),
    **`StandRipetuto`**, **`EtichettaLontanaDallaTaxiway`** (oltre 100 m da ogni asse o bordo di taxiway del `.geo` dello
    scalo), **`TipoSconosciuto`** (5° campo di un `.geo` vuoto, o né tipo dello schema, né nome di un `.def`, né colore;
    lo stesso per riempimento e bordo di un `.pol`), **`RiempimentoSenzaDisegno`** (un `.pol` il cui scalo — riconosciuto
    da dove sta, non dal nome — non ha il `.geo`). Le soglie sono scelte dell'agente dalla misura.
  Uscita sul fork: **4** ICAO diversi (3 con la proposta, lo stand di LIBP senza), **0** lontani, **2** stand ripetuti,
  **28** etichette lontane, **11** tipi sconosciuti (i 10 di `liap.geo` e `limw.pol`, che riempie con `COAST`: non è in
  `colors.def`), **0** riempimenti senza disegno. Validatore sull'albero: 235/866 → **235 errori, 911 avvisi**.
  Round-trip 718/718, tutto toccato 0. Test: motore 747 → **754**, Lab **781**.
- **Da decidere col committente**: l'ordine di disegno (I3: quello della carta o quello del fork); le marcature (O3:
  cosa fare delle parti fuori pista e della 05/23 di LIBR; le strisce senza larghezza); la vista per scalo (I6: dove
  sta nell'app). Il committente (4 ottobre): «vai, fai 12b … e a lavoro chiuso parti con 12c» — per tipo e slot degli
  stand il lettore e lo scrittore dei `.gts` si toccano.
- **12b (4 ottobre)** — stand e taxiway (R2, R2b, R3, R6). Codice comune toccato: `Stand.Type` e `Stand.Slot` (5° e 6°
  campo, null se mancano), `GtsParser`/`GtsSaver` (tipo e slot solo se ci sono; uno slot senza tipo lascia il 5° campo
  vuoto), `Models/Airport/SlotDelloStand.cs` (i filtri `t_`, `c_`, `d_`, `w_` del manuale: letti, controllati, scritti
  in maiuscolo senza doppioni), `EsitoDelFile.Chiavi` (i tag di stand ed etichette, per i controlli fra file), in
  `ControlloDellaTerra` nome oltre 20 caratteri, tipo fuori da L/M/H/S/G e slot scritti male (`ValoreFuoriElenco`) e
  una regola nuova, **`StandPiuGrandeDellaTaxiway`** (avviso).
  - 🔴 **Misura prima di offrire i campi** (lezione della 9b): tipo e slot scritti su **ogni** stand del fork (52 file,
    1 672 stand) cambiano la riga dello stand e nient'altro — i primi quattro campi byte per byte, le altre righe
    intatte. Il test gira sul campione `lirf.gts` e, con `SECTORLAB_ALBERO_VERO`, su tutto l'albero.
  - Nel Lab: la scheda dello stand ha **Tipo** (elenco col significato) e **Slot** (rifiutato col perché se un filtro
    non è del manuale; vuoti non si scrivono, e la riga torna com'era); la sezione **Slot** spiega ogni filtro e dice
    quando manca `w_` (i cargo non entrano). I **metadati** hanno i loro valori: `code` A-F, `kind` contact/remote,
    `use` uno o più fra schengen, nonschengen, cargo, ga, mil, heli, `airlines` codici ICAO di tre lettere, `push`
    sì/no, `pushdir` e `oneway` sulla rosa a otto venti; per la taxiway `code` e `oneway` (R6).
  - **Tipo e slot proposti** (R2b), scritti con un clic in una voce sola della storia: 🔴 la corrispondenza è una
    **proposta dell'agente, da confermare** — codice A e B → L, C → M, D ed E → H, F → S; uso `ga` → G; uso `cargo` →
    `w_`; compagnie → `c_XXX`. Gli slot che lo stand ha già restano. I filtri `t_` (tipi di aereo per codice, come
    nell'esempio della carta `t_A320 t_B738`) **non** si propongono: serve una tabella dei tipi per codice da una fonte
    primaria.
  - «La taxiway che porta allo stand» (R6) è l'etichetta col `code` più vicina allo stand, entro 300 m: scelta
    dell'agente (il sector non dice quale taxiway serve uno stand).
  Uscita sul fork: nessun avviso nuovo (tipo scritto solo come `M` in 48 stand, slot mai, tag `code` zero). Validatore
  sull'albero invariato, **235 errori, 911 avvisi**; round-trip 718/718, tutto toccato 0. Test: motore 754 → **770**,
  Lab 781 → **787**. A schermo (banco, fork pulito): `limc.gts` stand 101, codice C + uso cargo + compagnia DHK →
  «codice C → M · uso cargo → w_ · compagnie → c_DHK», «Scrivi» mette tipo M e slot `w_ c_DHK`, la sezione Slot li
  spiega, uno slot «boh» è rifiutato col perché; annullato tutto, copia intatta.
- **12c (4 ottobre)** — strati per tipo e ordine di disegno (H1, I1, I3). Solo il Lab: nessun codice comune toccato.
  - **Generi dentro uno strato** (H1): sotto la casella di «Disegni .geo» e di «Aeroporti a terra», quando lo strato è
    acceso, una casella per genere col suo conto — piste, bordi e assi delle taxiway, piazzali, edifici, moli, stop bar,
    linee d'arresto, **marcature delle piste** (i file di `RW_MARKINGS`: lì il 5° campo è sempre `RUNWAY`, e spegnere
    le piste non deve spegnere i numeri), «senza tipo»; e riempimenti, etichette, stand. Il genere di una linea è il suo
    5° campo; viaggia con la forma (`h` nel JSON della mappa) e si spegne senza riprendere le coordinate, per la stessa
    via delle voci spente della slice 6 (chiave `§strato:genere`). Scegliere un record di un genere spento lo riaccende.
    I colori restano quelli di Aurora della slice 4. Sul fork: 975 piste, 456 bordi, 1 487 assi, 196 piazzali, 882
    edifici, 1 947 moli, 119 stop bar, 558 linee d'arresto, 2 210 marcature, 2 senza tipo (polilinee); 1 753
    riempimenti, 1 075 etichette, 1 674 stand.
  - **Scheda del poligono** (I1): riempimento e bordo coi nomi di `colors.def` e il selettore c'erano già dalla slice 4c
    (verificato a schermo su `rf_ad_gnd.pol`). Opacità e filtro della testa (5° e 6° campo del manuale) il modello non
    li ha, e il fork non li scrive mai: restano fuori.
  - **Ordine di disegno** (I3): la scheda di un riempimento dice a che posto si disegna («4° di 73») e cosa ha sopra,
    per riempimento, con la nota su `HOLE`. Un **poligono nuovo nasce dopo l'ultimo del suo riempimento**, dovunque si
    sia cliccato: l'ordine resta quello che il file ha già. 🔴 Il controllo «ordine sbagliato» **non** è scritto: quale
    sia l'ordine giusto (quello della carta o quello del fork, misura sopra) lo deve dire il committente.
  Test: Lab 787 → **793**, motore **770**. A schermo (banco, fork pulito): LIRF coi generi tutti accesi; spenti assi,
  stand ed etichette spariscono solo quelli; `rf_ad_gnd.pol` record 3 «4° di 73 — sopra: TAXIWAY 10, CONCRETE 2, APRON 2,
  HOLE 3, BUILDING 49, RUNWAY 3».
- **Decisioni del committente (4 ottobre)**: (1) l'ordine di disegno è **quello del fork**, e la carta si corregge
  («file per file» I3: fatto); (2) delle marcature si segnala la pista che il `.rw` non ha — a LIBR la 05/23 c'è ma è
  chiusa, per questo è stata tolta dal `.rw` — e non le frecce della soglia spostata; le strisce per larghezza quando
  la larghezza sarà fra i metadati delle piste; (3) la vista per scalo in una «finestra a parte», e sulla UI si
  ragiona una seconda volta.
- **12d (4 ottobre)** — ordine di disegno, marcature, vista per scalo e per pista (I3, I6, O1, O3). Codice comune
  toccato: `Shared/OrdineDeiRiempimenti.cs` (l'ordine deciso), in `ControlloDellaTerra` due regole nuove (avvisi) e
  `PisteCitate` (i versi nominati da un commento, dopo «rw», «rwy» o «runway»).
  - **Misura** (`scratchpad/misura12d.py`): con l'ordine erba → taxiway → cemento → piazzale → edifici → pista, il posto
    di `HOLE` che dà meno poligoni fuori posto è **fra piazzali ed edifici** (49 in 14 file; in ogni altro posto 109 o
    più; con l'ordine della carta del 25 settembre 595): sul fork un buco segue un altro buco 55 volte, un piazzale 8,
    e precede un edificio 7. Scelta dell'agente dalla misura, dentro la decisione del committente.
  - **`OrdineDiDisegno`**: un riempimento scritto dopo uno che gli sta sopra, sulla testa del poligono, con chi lo
    precede e la sua riga. **`MarcaturaDiUnaPistaAssente`**: un commento di un `.geo` che nomina una pista che il `.rw`
    dello scalo non ha (lo scalo di `br_mark.geo` si riconosce da dove sta; «rw 11» con 11L e 11R nel `.rw` va bene).
  - **Vista per scalo** (I6), linguetta **Scali** accanto a Sfoglia e Problemi: scelto lo scalo (i 134 degli `.ap`),
    le sezioni **Scalo**, **Piste**, **Posizioni** (i record, anche le copie delle FIR), **File dello scalo** (quelli
    col suo nome), **Disegni e riempimenti** (il suo `.geo`, e i `.pol` e i `.geo` senza il suo nome che stanno entro
    10 km dal suo centro), e le **Marcature per pista** (O1) con le parti dai commenti — nei file di `RW_MARKINGS`
    una parte che non nomina una pista è della pista nominata prima; nel `.geo` dello scalo contano solo i commenti
    che nominano una pista e una parte. Un record si sceglie (scheda e mappa); un file si apre in Sfoglia. La vista
    non sposta niente: il Lab scrive nei file dove le cose stanno già, con qualunque organizzazione (I7).
  Uscita sul fork: **49** riempimenti fuori ordine in 14 file, **18** marcature di piste che il `.rw` non ha (LIBR
  05/23 ×6, LIRP 04/22 ×4 contro 03/21, LILN 18/36 ×4 contro 17/35, LIDE 12/30 contro 11/29, `eo_mark` 18 contro
  23/05, `mw_mark` 28 contro 27/09, `mz_mark` 27 contro 21/03). Validatore sull'albero: 235/911 → **235 errori, 978
  avvisi**. Round-trip 718/718, tutto toccato 0. Test: motore 770 → **779**, Lab 793 → **798**. A schermo (banco, fork
  pulito): Scali → LIRF (2 righe di `.ap`, 6 piste, 22 posizioni, 5 file, `lirf.geo` e `rf_ad_gnd.pol`, marcature di
  16L, 16R, 34L, 34R dal `.geo`); LIBR (marcature di 05, 13, 23, 31 da `br_mark.geo`); clic su «designator rw 05» →
  il record scelto, lo «0» evidenziato sulla mappa, la colonna resta su Scali.

**Slice 12 chiusa**, salvo i punti di startup (R4), che aspettano la prova in Aurora. Voci del giro dei file chiuse:
H1-H2 (H3 dalla slice 6), I1, I3, I4, I6, O1, O3 (le strisce quando ci sarà la larghezza), R1 (il controllo), R2, R2b,
R3, R6. Validatore sull'albero: 235/866 → **235 errori, 978 avvisi**. Test: motore 747 → **779**, Lab 781 → **798**.
🔴 Proposte dell'agente ancora da confermare: codice → tipo dello stand e niente filtri `t_` (12b); «la taxiway dello
stand» (12b); le soglie dei 10 km e dei 100 m (12a). Da provare a mano: prove 151-168 in `SectorLab-prova\PROVE.md`.

### Slice 13 — Settori e spazi (`.tfl`, `.hartcc`, `.lartcc`) — dal 4 ottobre

**Manuale IVAO** (riletto il 4 ottobre, `[FILLCOLOR]`, `[ARTCC HIGH]`, `[ARTCC LOW]`): la testa di un poligono è
`Tipo;Riempimento;Bordo;ColoreBordo;[Opacità];[Filtro]` — tipo `Static` o l'elenco delle posizioni **separate da
spazio** (`LGAV_APP LGAV_DEP`); opacità e filtro sono **facoltativi**; i vertici in DMS o per nome. Un `ICAO.tfl` col
nome di uno scalo di `[AIRPORTS]` si carica da solo. Nei confini `T/L;Identificativo;Lat;Lon;[Font]`, «each … shall be
named differently».

**Misura sul fork** (`8cf32c6`, `scratchpad/misura13.py`; i numeri della carta rimisurati):

- **234 settori dinamici** in 28 file: **181** nei 27 file di `DYNAMIC_SEC` (il numero della carta) e **53** in
  `OTHER\GCI.tfl` (la penisola e le isole), di cui **52 con la testa a quattro campi** (`…;GCI;1;GCI;`, senza
  l'opacità). Spessore sempre 1; opacità scritta 1 in 177 teste e 0 in 5 (i 4 laghi di `limmfic.tfl` e il primo di
  `GCI.tfl`); **nessun filtro, nessun `Static`**; riempimento ≠ bordo in 3 (i due `LIBB_FSS`, `LIRH_APP`); 6 teste
  col commento in coda (`lirr_ne_ctr.tfl`).
- **Separatore delle posizioni**: una sola posizione in 162 teste, **spazio in 16** (15 estere e `LIBB_ES_CTR
  LIBB_EU_CTR`), **due punti in 4** di `DYNAMIC_SEC` (i tre confini `LIMMLIM` di `limmctr.tfl`) più le 53 di `GCI.tfl`.
  I due punti non sono nel manuale. Due teste con uno spazio in fondo al nome (`LIBN_APP `, `LIBV_APP `).
- **D4 — posizioni italiane e `.frq`**: 151 posizioni italiane diverse nelle teste, **147 definite** in un `.frq`,
  nessuna «solo citata», **4 assenti**: `LIBC_TWR` (`twrs.tfl:69`), `LIMF_WW0_APP` (`limmapp.tfl:375`), `LIQW_I_TWR`
  (`twrs.tfl:2769`), `LIRE_APP` (`lirrctr.tfl:1`) — le quattro della carta. Estere: 55 (20 citate nei trasferimenti,
  35 no; la regola non le guarda). Verso opposto, non in carta: 27 posizioni italiane dei `.frq` senza settore (17
  TWR, e `LICD_APP`, `LIMC_ANW_APP`, `LIMC_MAR_APP`, `LIRF_AET_APP`, `LIRF_AWL_APP`, `LIRF_PS1_APP`, `LIVK_RCC_CTR`,
  `LIZZ_AAR_CTR`, `LIZZ_JTA_CTR`, `LIZZ_NVY_CTR`).
- **R-4 — testa ripetuta nello stesso file**: tre casi, di tre nature. `LIBB_FSS` due volte in `libb_es_ctr.tfl`
  (righe 147 e 254) **con la stessa forma**: una copia vera. `LIMM_FSS` cinque volte in `limmfic.tfl`: la FIC e i
  quattro laghi (`//GARDA`, `//LAGO DI COMO`…, colore `LIMMFIC`). I tre confini `LIMMLIM` di `limmctr.tfl` (2, 6 e 4
  vertici: linee, non aree).
- **Difetti dei dati** (per la lista R-10): `lfmm.tfl:1105` `LFMN_APP` ha una riga vuota dopo il primo vertice, e il
  settore resta con 1 vertice (gli altri sono righe senza testa); `lovv.tfl:48` secondi a 60; `lipp.hartcc:2047` idem.
- **Confini**: `HI_AIRSPACE` 4 file con 25 voci più i `DUMMY` (`lirr.hartcc` 13, `lipp` 7, `limm` 3, `libb` 2);
  `LOW_AIRSPACE` 15 file, 22 voci. Nessuna etichetta `L`. Nomi: `RR CONF1`, `RR CONF1M`, `RR CONF2` (HI) · `RR CNF1`,
  `RR CNF2.1`, `RR CNF2.2`, `RR CNF3` (LOW) · `MM CONF 1`, `MM CONF 2.1`, `MM CONF 2.2`, `MM CONF 3` (LOW): tre scritture.
- **J5 — il file giusto**: i nomi delle voci non sono nomi di posizione (`RR NE`, `LIBB CS0`), quindi `_CTR`/`_APP`
  non si legge dal nome. Legando ogni voce al settore dinamico della stessa forma (≥ 90% dei vertici) se ne trovano
  25 su 43, e **nessuna è nel file sbagliato** (le voci di HI coi `_CTR`/`_FSS`, quelle di LOW con gli `_APP`).
- **D9/J7/Q8**: zero tag `lower`/`upper`/`class` nell'albero. Voci del `MAPS` negli `.str`: 336, di cui **87 ATZ** (6°
  campo 5) e **45 CTR** (6° campo 1).

- **13a (4 ottobre)** — la testa del settore dinamico (D1). Codice comune toccato: `TflSector.SenzaOpacita`, `Filtro`,
  `Posizioni()`, `Statico`; `TflParser` (testa da quattro campi, opacità e filtro facoltativi, il commento in coda non
  è un filtro); `TflSaver` (scrive fino all'ultimo campo che c'è).
  - 🔴 **Trovato misurando**: il lettore voleva cinque campi per una testa, e una riga a quattro campi con due «nomi»
    davanti la leggeva come un **vertice per nome**. Le 52 teste senza opacità di `GCI.tfl` finivano così nel primo
    settore: un poligono solo di oltre 9 000 vertici con 52 punti inesistenti, senza un avviso (il round-trip era esatto,
    perché le righe non cambiavano). Ora una testa ha da quattro campi in su e un vertice per nome ne ha meno di quattro.
  - L'opacità non scritta resta non scritta finché vale 0 e non c'è un filtro: toccare un settore di `GCI.tfl` non gli
    aggiunge un campo. Il filtro (6° campo) è nella scheda, coi sette valori del manuale.
  - Le posizioni di una testa si separano allo spazio **e ai due punti**: «chi lo usa» e la rinomina di una posizione
    non vedevano le teste `A:B:C` (i confini `LIMMLIM`, `GCI.tfl`); la rinomina tiene il separatore che c'è.
  Uscita sul fork: i record dell'albero 115 568 → **115 620** (+52), round-trip 718/718, tutto toccato 0, validatore
  invariato (235 errori, 978 avvisi). Test: motore 779 → **788**, Lab 798 → **799**. A schermo (banco, fork pulito):
  `GCI.tfl` 53 record; scelto il secondo si accende la sola Sardegna (1 412 punti); l'opacità a 1 dà il diff di una
  riga `…;GCI;1;GCI;` → `…;GCI;1;GCI;1;`; il campo «Filtro» c'è.

- **13b (4 ottobre)** — il settore italiano legato ai `.frq` (D4, «la cosa più importante»). Codice comune toccato:
  `Validazione/ControlloDeiSettori.cs` e `Regola.SettoreSenzaPosizione`, chiamato da `ValidatoreDellAlbero`.
  - **`SettoreSenzaPosizione`** (avviso — la gravità è una scelta dell'agente, da confermare): una posizione italiana
    (`LI??_…`) della testa di un `.tfl` che nessun `.frq` conosce, né definita né fra i trasferimenti. Sulla testa del
    settore, e dice le posizioni che lo stesso scalo ha davvero nei `.frq` (spesso il nome è solo cambiato). Le estere
    e `Static` non si guardano.
  - Nella scheda, sotto «Posizioni», la nota «… non è in nessun .frq — il settore non si accende» (come le piste che
    il `.rw` non ha).
  Uscita sul fork: **4** avvisi, i quattro della misura — `twrs.tfl:69` `LIBC_TWR` (LIBC ha `LIBC_I_TWR`),
  `limmapp.tfl:375` `LIMF_WW0_APP` (LIMF ha `LIMF_GND`, `LIMF_TWR`, `LIMF_WN0_APP`), `twrs.tfl:2769` `LIQW_I_TWR`,
  `lirrctr.tfl:1` `LIRE_APP`. Validatore sull'albero: 235/978 → **235 errori, 982 avvisi**. Round-trip 718/718, tutto
  toccato 0. Test: motore 788 → **790**, Lab 799 → **800**. A schermo (banco): in alto «235 errori · 982 avvisi»;
  `twrs.tfl` → `LIBC_TWR`: la nota sotto il campo.

- **13c (4 ottobre)** — limiti verticali e classe nella scheda (D9, J7, Q8; la mappa aspetta la risposta del
  committente). Solo il Lab: nessun codice comune toccato. Le chiavi `lower`, `upper`, `class` erano già nel catalogo
  e nella scheda di settori dinamici, confini e mappe ATZ/CTR del `MAPS` (slice 1 e 3d), ma come testo libero: sul
  fork i tag sono zero, quindi la forma si decide adesso.
  - **`lower`/`upper`** secondo §M regola 8 («come nel PDF, in piedi»): `SFC` e `GND` solo per l'inferiore, `UNL` solo
    per il superiore, piedi (`1500` → `1500ft`) o FL (`fl 195` → `FL195`). Il resto si rifiuta col perché. Vale anche
    per i tratti di aerovie e rotte VFR, che usano le stesse chiavi.
  - **`class`**: una scelta chiusa, A-G.
  - 🟡 Da chiedere: l'AIP scrive i limiti di ATZ e CTR anche come «2000 FT **AGL**» / «**AMSL**»; §M non lo prevede e
    oggi si rifiuta. Non si controlla ancora che l'inferiore stia sotto il superiore.
  Test: Lab 800 → **814**. A schermo (banco): `liap.str` → `LIAP ATZ`: `upper` 2000, `lower` sfc, classe D dal menu →
  `//@"LIAP ATZ" lower=SFC upper=2000ft class=D`; `lower` = `unl` rifiutato («il limite inferiore non può essere UNL»).

**Decisioni del committente del 5 ottobre** (sulle domande nate dalla misura): la testa ripetuta è un avviso **solo
per la copia vera** (stessa forma), non per i laghi di una FIC né per i pezzi di un confine; il file giusto di un
confine si decide **legando la voce al settore dinamico della stessa forma**, e per un settore nuovo si propone la
cartella dal tipo della posizione; la forma dei nomi delle configurazioni **è un'impostazione dell'app** («la farei
impostabile dall'app, così se cambia non devo ricorrere al codice»); limiti e classe sulla mappa **al passaggio del
mouse**, come i vincoli dei punti. Ancora aperte: i due punti fra le posizioni, il verso opposto di D4, avviso o
errore per `SettoreSenzaPosizione`, AGL/AMSL nei limiti.

- **13d (5 ottobre)** — la copia di un settore (R-4). Codice comune toccato: `Regola.SettoreRipetuto`, in
  `ControlloDeiSettori`. Stesse posizioni (in qualunque ordine e scrittura) e **stessa forma** come anello, nello
  stesso file. Sul fork **2**: `libb_es_ctr.tfl:254` (`LIBB_FSS`, già alla riga 147, 47 vertici) e — non era nella
  misura, che non leggeva le teste a quattro campi — `GCI.tfl:9707`, un'isola della laguna veneta scritta due volte
  (già alla riga 9660). I cinque `LIMM_FSS` e i tre `LIMMLIM` non si segnalano.
- **13e (5 ottobre)** — il file giusto per un confine (J5). Codice comune toccato: `Regola.ConfineNelFileSbagliato`
  (la calcola il Lab). `Core/Copie/PostoDelConfine.cs`: un settore con sole posizioni `_CTR`/`_FSS` ha il confine in
  `HI_AIRSPACE`, con sole `_APP` in `LOW_AIRSPACE`; torri e tipi misti non dicono niente. L'avviso tocca la voce che ha
  la forma (uguale, o 9 vertici su 10) di settori di un tipo solo e sta nella cartella dell'altro. Nella scheda del
  settore la sezione **«Il confine»**: dove va, e le voci che hanno la sua forma (clic → la voce), o «nessuna voce ha
  questa forma: un confine nuovo va in …». Sul fork **0** avvisi, come nella misura.
  🔴 Trovato scrivendo i test: due voci di un `.hartcc` senza una riga vuota fra loro non danno due forme alla mappa
  (sul fork sono sempre separate).
- **13f (5 ottobre)** — i nomi delle configurazioni (K1) e le **impostazioni dell'app**. Codice comune toccato:
  `Regola.NomeDellaConfigurazione` (la calcola il Lab). `Core/Ispezione/NomiDelleConfigurazioni.cs`: il modello è un
  testo con `{ACC}` e `{N}` (`{ACC} CONF{N}`, `{ACC} CNF {N}`…); una voce è una configurazione se è sigla + parola
  (`CONF`, `CNF`, `CONFIG`, `CFG` o quella del modello) + numero (+ una lettera: `CONF1M`). Il modello si cambia dal
  riquadro **Impostazioni** della barra (nelle due finestre), si ricorda in `nomi-delle-configurazioni.txt` fra i dati
  del Lab — fuori dal sector — e gli avvisi si rifanno subito. Quello di base, `{ACC} CONF{N}`, è una **scelta
  dell'agente** (la scrittura di `HI_AIRSPACE`): si cambia senza toccare il codice. Un avviso per nome e per file
  (una configurazione in più pezzi ne dà uno, «3 pezzi»). Sul fork, col modello di base, **8**: `MM CONF 1`, `2.1`,
  `2.2`, `3` di `limm_tma.lartcc` e `RR CNF1`, `CNF2.1`, `CNF2.2`, `CNF3` di `lirr_tma.lartcc`; con `{ACC} CNF{N}`
  diventano i tre di `lirr.hartcc` e i quattro di Milano.
- **13g (5 ottobre)** — limiti e classe sulla mappa (D9, J7, Q8). Solo il Lab. `Geometria.LimitiEClasse`: una riga nel
  suggerimento della forma, sopra i vincoli dei punti (`FL195 – UNL · classe C`; un limite solo: `? – FL195`), per
  settori dinamici, confini e mappe del `MAPS`. 🔴 Preso a schermo: scritto il tag, il suggerimento restava quello di
  prima — la geometria del file non si rifaceva dopo un metadato. Ora si rifà per `lower`, `upper`, `class`.

Uscita della slice sul fork: validatore 235/978 → **235 errori, 992 avvisi** (+4 `SettoreSenzaPosizione`, +2
`SettoreRipetuto`, +8 `NomeDellaConfigurazione`, 0 `ConfineNelFileSbagliato`); round-trip 718/718, tutto toccato 0.
Test: motore 779 → **791**, Lab 798 → **853**. A schermo (banco): «235 errori · 992 avvisi»; Impostazioni → modello
`{acc} cnf{n}` accettato e scritto `{ACC} CNF{N}` (esempio `RR CNF2.1`), `CONF{N}` rifiutato col perché, vuoto = di
base; `LIRR_EW_CTR` → «Il confine · HI_AIRSPACE»: `lirr.hartcc` RR EW, stessa forma; limiti e classe scritti dalla
scheda e letti sulla mappa passando sul bordo.

**Slice 13 chiusa** per le voci D1, D4, D9, J4 (commento in coda: slice 2), J5, J7, K1, Q8, R-4, salvo le quattro
domande aperte qui sopra.

**Decisioni del committente del 6 ottobre** (tre delle quattro domande): per le posizioni dei `.frq` senza settore
**sì, un avviso**; `SettoreSenzaPosizione` **resta un avviso**; AGL e AMSL nei limiti **servono**. Sui due punti fra
le posizioni ha chiesto un esempio: resta aperta (vedi sotto).

- **13h (6 ottobre)** — il verso opposto di D4. Codice comune toccato: `Regola.PosizioneSenzaSettore`, in
  `ControlloDeiSettori` (che ora prende i `.frq` coi loro file). Una posizione italiana che controlla uno spazio —
  `_TWR`, `_APP`, `_DEP`, `_CTR`, `_FSS`; terra e delivery no — definita in un `.frq` e che nessuna testa di `.tfl`
  nomina. Una volta per posizione (lo stesso nominativo nel `.frq` di una FIR è una copia), sulla sua riga, coi
  settori che lo stesso scalo ha davvero. Sul fork **27**, i numeri della misura: 17 torri, 6 avvicinamenti
  (`LICD_APP`, `LIMC_ANW_APP`, `LIMC_MAR_APP`, `LIRF_AET_APP`, `LIRF_AWL_APP`, `LIRF_PS1_APP`), 4 CTR militari.
  Tre fanno coppia con gli avvisi della 13b, e dicono che cosa è successo: `LIBC_I_TWR` (il `.tfl` ha `LIBC_TWR`),
  `LIQW_TWR` (il `.tfl` ha `LIQW_I_TWR`), `LIRE_TWR` (il `.tfl` ha `LIRE_APP`).
- **13i (6 ottobre)** — AGL e AMSL nei limiti. Solo il Lab. `lower`/`upper` in piedi accettano il riferimento:
  `2000 ft agl`, `2000ftAGL`, `2000 AGL` → `2000ft AGL`; scritto nel file fra virgolette (`upper="2000ft AGL"`, §M
  regola 5). Un FL, `SFC`, `GND`, `UNL` non ne hanno, e un altro riferimento (`QNH`) si rifiuta. §M regola 8 aggiornata.

Uscita sul fork: validatore 235/992 → **235 errori, 1 019 avvisi** (+27 `PosizioneSenzaSettore`) — è il conto del
pannello del Lab; `tools/Vipi.SectorfileProva`, che non ha le regole calcolate dal Lab (gli 8 nomi delle
configurazioni), dà 1 011. Round-trip 718/718, tutto toccato 0. Test: motore 791 →
**792**, Lab 853 → **862**. A schermo (banco): «235 errori · 1019 avvisi»; i 27 nel pannello; `LIAP ATZ` con
`2000 ft agl` → `//@"LIAP ATZ" lower=SFC upper="2000ft AGL"`.

🟡 **Aperta: i due punti fra le posizioni.** Il manuale separa le posizioni di una testa con lo spazio
(`LGAV_APP LGAV_DEP;#00120000;1;#00120000;1;`), e così fanno 16 teste del fork
(`LIBB_ES_CTR LIBB_EU_CTR;CTR;1;CTR;1;`); i tre confini di `limmctr.tfl` e le 53 teste di `GCI.tfl` usano i due punti
(`LIMM_WS2_CTR:LIMM_WS5_CTR:LIMM_ES2_CTR:LIMM_ES5_CTR;LIMMLIM;1;LIMMLIM;1;`). Il Lab legge tutte e due le scritture;
se in Aurora i due punti non valgono, quelle 56 teste non si accendono mai: serve una prova in Aurora (con
`LIMM_WS2_CTR` collegata, i confini `LIMMLIM` si vedono?).

🔴 Scelte dell'agente da confermare: `{ACC} CONF{N}` come modello di base; la scrittura `2000ft AGL`. Da provare a
mano: prove 169-185 in `SectorLab-prova\PROVE.md`.

### Slice 14 — ACC e aerovie (`.artcc`, `.lairway`, `.hairway`) — dal 6 ottobre

**Manuale IVAO** (riletto il 6 ottobre, `[ARTCC]`, `[LOW AIRWAY]`, `[HIGH AIRWAY]`): `T/L;Identificativo;Lat;Lon;[Font]`
negli ACC e `T/L;Aerovia;Lat;Lon;` nelle aerovie, coi punti in DMS o per nome («highly recommended»); «you can set
multiple labels for the same airway», e nell'esempio le etichette sono **per nome** (`L;V200;REPUK;REPUK;`).
🔴 Il lettore delle aerovie vuole le coordinate nelle righe `L;`: un'etichetta per nome oggi è una riga illeggibile
(sul fork non ce ne sono; servirà con A8 e B4).

**Misura sul fork** (`8cf32c6`, `scratchpad/misura14.py` e `misura14b.py`):

- **`FRA.artcc`**: 104 etichette `L;` (tutte per coordinate, **tutte sul fix che ha il loro nome**: scarto massimo
  sotto 0,05 NM); `FRA BDRY` 1 589 punti in 5 pezzi, `LIMITROFI` 14 tratti (15-44 NM), `NPZ` 2 poligoni chiusi,
  `AOCC` 10 tratti = **5 AOCC**, ognuna un gambo che parte dal confine (0,00 NM) e una stanghetta di traverso che il
  gambo tocca **a metà** (7,5 / 7,5 NM). Le stanghette: 15,08 · 14,93 · 15,00 · 14,97 · 14,98 NM. I gambi: 18,07 ·
  14,59 · 14,99 · 15,01 · 14,97.
- **`FRA-gates.artcc`**: 85 cerchi di raggio 0,499-0,500 NM (61 di 38 punti, 24 di 37), 90 etichette. 84 cerchi
  chiudono a meno di 0,01 NM (16 metri: arrotondamenti); **uno è aperto**, `//X07-X08` alla riga 702, a cui manca un
  passo (0,085 NM) — quello della carta. Tutti i centri a meno di 0,3 NM dal confine.
- **`itawlow.lairway`**: **246 aerovie** in 274 pezzi (28 `BREAK`, 27 aerovie in più pezzi), 1 385 punti tutti per
  nome e tutti trovati (resta `KPT`, che è il VOR con le coordinate sbagliate di `itvor.vor:109`); **1 089 tratti
  distinti, 21 condivisi** da più aerovie. **896 etichette**, tutte per coordinate, 34 col nome unito.
  - **12 aerovie senza etichetta** (la carta ne contava 22, guardando il nome esatto: `A145`, `A725`, `N1`… ce l'hanno
    dentro un nome unito): `Q482`, `T345`, `T369`, `Y480`, `Y498`, `Y526`, `Y662`, `Y769`, `Y801`, `Y831`, `Y842`, `Y99`.
  - **18 etichette nominano un'aerovia che non c'è** (23 nomi: 22 vecchie «U» e `Y11`), sempre accanto a una vera.
  - **25 etichette a più di mezzo miglio** dalla loro aerovia (`M740` a 38 NM, `M985` a 12 e 10, `M730` a 12, `Y138` a
    11, `L153` a 3,3…): l'aerovia è stata spostata, l'etichetta no.
  - 🔴 **La regola che il file segue già**: 773 etichette stanno a metà del loro tratto, e **nessun tratto più corto
    di 10 NM ha un'etichetta** (il più corto etichettato misura 10,0 NM). 218 tratti non ne hanno: 166 sono sotto i
    10 NM, **52 sopra** (33 sopra i 15). Chi ha fatto le etichette ha usato una soglia di 10 NM: un'etichetta «a metà
    di ogni tratto» (B4) ne aggiungerebbe 166 su tratti corti.
  - Sui 21 tratti condivisi: 13 hanno l'etichetta coi nomi uniti, 7 nessuna, 1 col nome di una sola (`M730` su
    `M730`-`T648`).
- **`itawhigh.hairway`**: 129 righe, tutte commentate; nessun `.isc` lo carica (B11: non si tocca).

- **14a (6 ottobre)** — gli avvisi degli `.artcc` (A4). Codice comune toccato: `Validazione/ControlloDegliAcc.cs` e
  quattro regole (avvisi). Si legge dalle righe: un gruppo è un record solo, e l'avviso va sulla riga del tratto, col
  nome che gli dà il commento sopra.
  - **`CerchioNonChiuso`**: un tratto è un cerchio se ha almeno 12 punti alla stessa distanza dal loro centro (entro
    2 NM); è aperto se fra l'ultimo punto e il primo manca più di mezzo passo.
  - **`CentroFuoriDalConfine`**: il centro a più di 0,3 NM dal tratto più vicino che non è un cerchio.
  - **`StanghettaDellAocc`**: nei gruppi `AOCC…`, il tratto che un altro tocca con un estremo lontano dalle sue punte
    deve misurare 15 NM ± 0,25.
  - **`EtichettaLontanaDalFix`**: un'etichetta per coordinate col nome di un fix, VOR o NDB a più di 0,1 NM da lui,
    con la riga corretta (le coordinate del punto). Il nome di un gate non è un fix e non si guarda.
  - 🔴 Non «gate ≠ 10 NM» né «etichetta fuori centro» (committente, 24 settembre).
  Uscita sul fork: **1** cerchio aperto (`X07-X08`), 0 centri fuori, 0 stanghette, 0 etichette: i dati sono a posto, e
  le regole ci sono per quando qualcuno li tocca.
- **14b (6 ottobre)** — i controlli delle aerovie (B12). Codice comune toccato: `Validazione/ControlloDelleAerovie.cs`
  e tre regole (avvisi), sui tracciati `T;` contro le etichette `L;` dello stesso file.
  - **`AeroviaSenzaEtichetta`** (12), sulla prima riga dell'aerovia; un `BREAK` non è un'aerovia.
  - **`EtichettaDiUnAeroviaAssente`** (18), con la riga senza i nomi che non ci sono (`L;UA145-A145;…` → `L;A145;…`):
    «Correggi tutte le 18» dal pannello.
  - **`EtichettaLontanaDallAerovia`** (25): a più di 0,5 NM da ogni tratto delle aerovie che nomina; il buco di un
    `BREAK` non è un tratto.
- **14c (6 ottobre)** — i tratti di un'aerovia (B2). Solo il Lab. Nella scheda di un pezzo di aerovia la sezione
  **Tratti**: una riga per tratto (`TOP → ROKUD`) col **verso** (`both` ↔, `fwd` →, `back` ←, rispetto all'ordine del
  file), la **minima** e la **massima** (le regole dei limiti della 13: `FL95`, `2000ft AGL`…). Si scrivono nel tag
  `//@@"PUNTO" dir=… lower=… upper=…` del punto che apre il tratto (§M regola 4), e si leggono sulla mappa passando
  sull'aerovia (`ROKUD → DIVIP: solo da ROKUD a DIVIP, FL95 – FL195`). L'interruzione e le righe delle etichette non
  hanno tratti. `Core/Ispezione/TrattiDelleAerovie.cs`.

Uscita sul fork: validatore 235/1 019 → **235 errori, 1 075 avvisi** nel pannello del Lab (1 067 in
`Vipi.SectorfileProva`): +1 `CerchioNonChiuso`, +12, +18, +25. Round-trip 718/718, tutto toccato 0. Test: motore 792 →
**800**, Lab 862 → **865**. A schermo (banco): i quattro conti nel pannello; `KY139` → dieci tratti, il secondo con
`fwd`, `fl 95`, `fl195` → `//@@"ROKUD" dir=fwd lower=FL95 upper=FL195`.

**Decisioni del committente del 6 ottobre** (sulle tre domande nate dalla misura): le etichette calcolate tengono la
**soglia dei 10 NM**, che è un'impostazione dell'app; le etichette nuove si scrivono **nella parte delle etichette, in
ordine di nome**, senza toccare il resto del file (l'ordine a blocchi aspetta la prova B9 in Aurora); le 25 lontane
**si rifanno a metà** del tratto giusto.

- **14d (6 ottobre)** — le etichette calcolate (B4). Codice comune toccato: `IO/RigheDelleAerovie.cs` (un file di
  aerovie letto dalle righe, per aerovia: pezzi, etichette, interruzioni — lo usano il controllo del motore e il Lab),
  `Shared/Distanze.cs` (le distanze del validatore, pubbliche). `Core/Modifiche/EtichetteDelleAerovie.cs`.
  - **Il piano**: un tratto lungo almeno la soglia e senza etichetta ne riceve una a metà, coi nomi delle aerovie che
    lo condividono in ordine alfabetico (`L53-P873`); un'etichetta a più di mezzo miglio dalla sua aerovia si toglie
    (e il suo tratto riceve la nuova); un'etichetta sul suo tratto col nome sbagliato prende quello giusto e **resta
    dov'è** — 98 etichette del fork stanno sul tratto ma non a metà, e non si spostano.
  - **Dove scrive**: dopo l'ultima etichetta col nome più grande fra quelli che non superano il suo. I tracciati non
    cambiano di un byte. Rifatto sul file sistemato, il piano è vuoto.
  - **La soglia** è nelle **Impostazioni** («Etichette delle aerovie: tratto minimo», di base 10, 0 = ogni tratto) e
    si ricorda in `etichette-delle-aerovie.txt` fra i dati del Lab.
  - Nella scheda di ogni record di un file di aerovie la sezione **Aerovie del file**: il piano coi numeri (al
    passaggio del mouse, quali) e **Sistema le etichette** — una voce sola nelle modifiche.
  - 🔴 Trovato scrivendo: il controllo della 14b chiudeva un pezzo di aerovia a ogni commento, **anche a un tag
    `//@@`**: scritto il verso di un tratto (14c), le etichette di quell'aerovia diventavano «lontane». Ora i tag non
    chiudono niente (`RigheDelleAerovie`), e c'è il test.
  - 🟡 Il tag `gen=labels` di §M-G non si scrive ancora: le etichette stanno sparse in una parte del file, non in un
    blocco. Arriva con l'ordine a blocchi (B1, dopo B9); fino ad allora il parametro è l'impostazione dell'app.
  Uscita sul fork (`itawlow.lairway`, soglia 10): **53** etichette nuove (5 di `KY139`, che ne aveva 3 su 10 tratti),
  **24** rinominate (i nomi «U» che non ci sono più, e un'aerovia in più o in meno su un tratto condiviso), **25**
  tolte perché lontane.
- **14e (6 ottobre)** — aggiungere un'aerovia a mano (B14, B5). Codice comune toccato: `Regola.TrattoSenzaQuote` (la
  calcola il Lab). `Core/Modifiche/AerovieAMano.cs`: nome e punti in ordine (per nome: fix, VOR, NDB del master); il
  Lab scrive `//@"NOME" locked=si`, `//@START`, un tag `//@@"PUNTO" dir=both` per tratto, le righe `T;`, `//@END` —
  fra i tracciati, in ordine di nome — e poi le sue etichette (anche il suo nome su quella di un tratto che condivide).
  Le quote non si inventano: `TrattoSenzaQuote` avvisa per ogni tratto che dice il verso e non le quote (0 sul fork: i
  tratti senza tag non si segnalano). Rifiuti col perché: nome che c'è già, con spazi o col trattino, `BREAK`, meno di
  due punti, un punto che il master non ha, lo stesso punto due volte di seguito.
- **14f (6 ottobre)** — togliere un'aerovia (B15): tutti i suoi pezzi, i `BREAK` fra loro, il blocco e i tag; un'etichetta
  solo sua sparisce, una condivisa perde il suo nome. I commenti scritti a mano restano. Anche un'aerovia `locked` si
  toglie (B5: quel segno ferma l'import, non l'AOD). 🔴 Preso a schermo: tolta l'aerovia, la scheda mostrava ancora il
  suo nome sul record che ne aveva preso il numero — ora la scelta si svuota.

Test: motore 800 → **801**, Lab 865 → **884**. Validatore invariato (235 errori, 1 075 avvisi nel Lab). A schermo
(banco, fork pulito): `KY139` → «53 da aggiungere · 24 col nome da correggere · 25 lontane», **Sistema le etichette** →
una voce «53 nuove, 24 rinominate, 25 tolte» e il piano a zero; **+ Aggiungi** `KZ1` con `top rokud chi` → il blocco
dopo `KY139`, due tratti `both`, le etichette `L;KY139-KZ1;…` (il tratto condiviso) e `L;KZ1;…`; **Togli l'aerovia KZ1**
→ l'etichetta condivisa torna `L;KY139;…`.

**Slice 14 chiusa** per le voci A4, B2, B4, B12, B14, B15. 🔴 Non fatto, e detto al committente: il lettore delle
aerovie non legge ancora un'etichetta scritta per nome (`L;V200;REPUK;REPUK;`, la forma del manuale): nel modello
un'etichetta è una coordinata, e cambiarlo tocca lettore, scrittore, mappa e scheda. Sul fork non ce ne sono; i
controlli e le etichette calcolate, che leggono le righe, le capiscono già. Va insieme ad A8 (le etichette per
riferimento, dopo la prova in Aurora). 🔴 Scelte dell'agente da confermare: le soglie degli ACC (0,3 NM dal confine,
15 ± 0,25 NM, 0,1 NM dal fix) e il mezzo miglio delle etichette. Da provare a mano: prove 186-200.

### Slice 15 — MVA di ACC e di scalo (`ENRMVA/*.mva`, `{icao}.mva`) — dal 6 ottobre

**Manuale IVAO** (riletto il 6 ottobre, `[MVA]`): `T/L;Identificativo;Lat;Lon;[Descrizione];[Font]`, con descrizione e
carattere solo sulle righe `L;`. La descrizione è quel che si legge sullo schermo: la quota. Il 5° campo delle `T;` il
manuale non lo nomina: è il gruppo della *MVA Selection*, provato dal committente (carta «file per file» §6, E3 ed E7).

**Misura sul fork** (`8cf32c6`, `scratchpad/misura15.py`, `misura15b.py`, `misura15c.py`):

- **MVA di ACC** (4 file): 119 etichette, 4 225 righe `T;` **tutte col gruppo** nel 5° campo; **104 separatori
  `T;DUMMY` senza gruppo** (E7); 382 commenti in coda; 9 etichette fuori da ogni poligono (5 sole nel loro blocco, senza un vertice).
- **MVA di scalo** (24 file): **226 etichette, tutte con la quota nel 5° campo** — il 2° è il nome (il settore, o la
  zona), non la quota: in `licj.mva` `L;5000TPS;…;50;7;`. Le `T;` non hanno il 5° campo (4 944). **8 file** hanno un
  nome solo e le zone separate dai `DUMMY` (`BB CS0`, `RR ES0`, `RR EW0`, `MM WN0`, `PP CE0`, `MM ES0`, `PP SE0`,
  `RR US0`), **16** un nome per zona; **nessuno** si chiama come lo scalo.
- **Quote**: in centinaia dappertutto, tranne **13** piene o come livello (`libn` 1, `libv` 6 — `FL85` —, `lict` 6).
  Valori speciali: `TRL`, `NO MINIMA`, `70/TRL`, `80/TRL`, `*30/40`.
- **8 blocchi** raccolgono in fondo al file le etichette di più zone, con quote diverse (`liba`, `licj`, `lict`,
  `lipa`, `lipe`, `liph`, `lipi`, `lirs`): per il modello sono una zona sola con una quota sola.
- Due scritture che la misura a mano non leggeva e il motore sì: un'etichetta in **gradi decimali**
  (`lipx.mva:14`, `L;MM ES0;45.55756591;10.27902575;60;8;`) e le coordinate **senza punti** di `libv.mva`
  (`N0410300000`, già `CoordinataFuoriForma`).

- **15a (6 ottobre)** — le MVA di scalo lette come quelle di ACC (S1) e il gruppo sui vertici nuovi (E3). Codice comune
  toccato: `IO/Parsers/MvaParser.cs`, `IO/Savers/MvaSaver.cs`, `Models/Airport/MvaSector.cs`.
  - 🔴 Fino a qui il lettore prendeva la quota di una MVA di scalo dal **2° campo** (`RR US0`, `5000TPS`,
    `CERCHIO-BA`): la scheda mostrava un nome al posto della quota, e per questo dalla slice 3 quota e carattere erano
    in sola lettura. Ora la quota è il **5° campo** in tutti e due i tipi di file; il 2° è il nome (`MvaSector.Nome`) e
    lo scrittore lo rimette com'era. Solo una `L;` di scalo senza 5° campo ricade sul 2°.
  - Un blocco con più righe `L;` che non dicono lo stesso nome, la stessa quota e lo stesso carattere si riconosce
    (`EtichetteDiverse`): lo scrittore le riscriverebbe tutte uguali, quindi la scheda **non fa scrivere** quota e
    carattere di quel blocco e dice perché («si cambiano dalle righe del file, qui sotto»). Il rifiuto sta anche in
    `ModificheInSospeso.Cambia` (`SiScriveSe` della descrizione del campo), non solo nella scheda.
  - Un vertice **nuovo** di una MVA di ACC esce col gruppo nel 5° campo; uno letto senza resta senza
    (`MvaVertex.LettoSenzaGruppo`), così un file toccato cambia solo dove si è messa mano.
- **15b (6 ottobre)** — i controlli (E3, E5, S2, S3). Codice comune toccato: `Validazione/ControlloDelleMva.cs` e sei
  regole (avvisi), lette dalle righe come quelle degli ACC.
  - **`EtichettaFuoriDallaZona`**: un'etichetta che non sta dentro nessun poligono (di almeno tre punti) del suo file.
  - **`ZonaSenzaEtichetta`**: un poligono **chiuso** (primo e ultimo punto a meno di 20 m) senza etichette dentro, solo
    nei file con un nome solo — in quelli «un nome per zona» i tratti sono linee, cerchi, pezzi di confine.
  - **`QuotaNonValida`**: il 5° campo che non è una quota in centinaia né `TRL`, `NO MINIMA`, `70/TRL`, `*30/40`.
  - **`QuotaNonInCentinaia`**: in piedi o come livello, con la riga corretta (`2500` → `25`, `FL85` → `85`).
  - **`GruppoMancanteNellaMva`**: in una MVA di ACC, una `T;` senza il gruppo del file nel 5° campo o con un altro,
    **separatori `DUMMY` compresi**; con la riga corretta (`T;DUMMY;…;LIBB;`), e il commento in coda resta in coda.
  - **`MvaNonDelloScalo`**: una MVA di scalo che non si chiama come lo scalo — uno per file, sulla prima riga che ha
    un altro nome, coi nomi che trova.
  - Il commento in coda (S3) lo dice già `CommentoInCoda` dalla slice 2.
  Uscita sul fork: **13** etichette fuori (9 di ACC, 4 di scalo: la quarta è `libv.mva:10`, che la misura a mano non
  leggeva), **0** zone senza etichetta (quella di `lipx.mva` ha l'etichetta in gradi decimali, dentro), **0** quote
  non valide, **13** piene, **104** separatori senza gruppo, **24** MVA di scalo.
- **15c (6 ottobre)** — la quota nella scheda (E2, S2). Solo il Lab: `Core/Ispezione/Quote.cs` (`LeggiDiMva`,
  `SignificatoDiMva`), `EditorDelCampo.razor`. La quota di una zona si scrive sempre **in centinaia**: `25`, e anche
  `2500`, `2500ft`, `FL85` (diventano `25` e `85`: nelle centinaia nessuna MVA arriva a 1 000, quindi un numero pieno è
  in piedi); i valori speciali si scelgono dall'elenco o si scrivono (`trl` → `TRL`, `no minima`, `70/trl`); `*30/40`
  resta com'è. Sotto il campo il significato: «= 2 500 ft», «= il livello di transizione», «= 7 000 ft, o il livello
  di transizione se è più alto». `2550` e `pippo` si rifiutano col perché.

Uscita sul fork: validatore 235/1 067 → **235 errori, 1 221 avvisi** in `Vipi.SectorfileProva` (**1 229** nel pannello
del Lab): +13, +13, +104, +24. Round-trip 718/718, tutto toccato 0, una modifica per record 115 620/115 620: la nuova
lettura delle MVA di scalo non sposta una riga. Test: motore 801 → **819**, Lab 884 → **907**. A schermo (banco, copia
del fork): i sei conti nel pannello; `lirn.mva` record 0 (`RR US0`, quota 110) → `2500` diventa `25`, `70/trl` →
`70/TRL`, `FL85` → `85`, `no minima` → la riga `L;RR US0;…;NO MINIMA;8;` col nome al suo posto, `2550` e `pippo`
rifiutati; `lirs.mva` record 14 (il blocco delle etichette) con quota e carattere fermi e il perché; `licj.mva`
record 3 (`5000TPS`) quota `50` → `60`, riga `L;5000TPS;…;60;7;`; in `limm.mva` un vertice in fondo → `T;LIMM;…;LIMM;`;
**Correggi tutte le 7 di libb.mva** → una voce «−7 +7», ogni `T;DUMMY;…;` col suo `LIBB;`.

**Slice 15 chiusa** per le voci E2, E3, E5, S1, S2 (le regole), S3. Già fatte dalle slice comuni: E1 (il blocco
`//@"LIMM" zone="…"` e il soprannome nella scheda, slice 1d) ed E4 (tipo fisso, punti coi suggerimenti, nascondi e
mostra). 🔴 **Non fatto, e detto al committente**:

- **Spezza / unisci nelle MVA di ACC** (E4): resta fermo (le forbici non compaiono). Una zona è un poligono solo con la sua
  etichetta, il `T;DUMMY` la chiude, e sul fork nessuno sta in mezzo a una zona; il modello non tiene un'interruzione
  dentro una zona. Per farne due si aggiunge una zona.
- **«Il Lab propone i tratti intorno all'etichetta»** (E1): serve nei 16 file «un nome per zona», dove le etichette
  stanno in fondo e la zona non è un poligono. È il lavoro dell'adozione (E6, S2: F4, in un ramo).
- **Una zona nuova** nasce copiando la vicina, separatore compreso: se il separatore della vicina non ha il gruppo,
  nemmeno il suo. Lo dice `GruppoMancanteNellaMva`, e si corregge con un clic.
- 🔴 Scelte dell'agente da confermare: un numero pieno senza unità letto in piedi sopra 660; venti metri per dire che
  una zona è chiusa; il gruppo di un file di ACC preso dal nome che le sue righe portano più spesso.

Da provare a mano: prove 201-211.

### Slice 16 — VFR (`.vfi`, `.vrt`, `ENRVFI`) — dal 6 ottobre

**Manuale IVAO** (riletto il 6 ottobre: `[VFRFIX]`, `[VFRROUTE]`, `[VFRENR]`, `[VFRRTEENR]`):

- `[VFRFIX]`: `Nome;Quota;Lat;Lon;[Tipo]` — la quota è «string or number» (`2500-4000`, `MAX 2500`), obbligatoria; le
  coordinate anche per riferimento a un fix; il tipo (`0` obbligatorio, `1` VFR, `2` eli, `3` area) è **facoltativo**.
- `[VFRROUTE]`: `Numero;Lat;Lon;Riservato;Militare` — il 4° campo è riservato, il 5° è **«Route Military»** (`1` = sì,
  `0` o vuoto = no). I due campi `…;;1;` di `libv.vrt` e `licz.vrt`, che il modello teneva come «sconosciuti», sono questi.
- `[VFRENR]`: come `[VFRROUTE]` col 4° campo «Group filter», in un `.vfi`; `[VFRRTEENR]`: `Nome rotta;Gruppo;Lat;Lon`.
  🔴 Sul fork **nessun file** ha queste due forme (i tre di `ENRVFI` hanno la forma dei punti: F5, F6).

**Misura sul fork** (`8cf32c6`, `scratchpad/misura16.py`; poi il validatore vero, `scratchpad/valida`):

- **Punti**: 74 `.vfi` di scalo e 3 di `ENRVFI`, **586 righe**, tutte di quattro campi: **il tipo non lo scrive
  nessuno**. Coordinate: 488 compatte, 95 coi punti, 3 scritte male (`lipx.vfi:7`, `lirf.vfi:6`, `lirn.vfi:5`: le dicono
  già `CoordinataFuoriForma` e `CoordinataLettaAltrove`).
- **Il 2° campo**: 510 codici (due-cinque lettere e una o due cifre: le due lettere dello scalo, una direzione, un
  numero — `RFS3`, `BNNW1`); **72 con altro** (quasi sempre le due lettere dello scalo senza numero: `ED` in
  `lied.vfi`, `PA`, `PL`…; `MASW` e `RZNE` senza numero), 2 numeri (`2500` in `liba.vfi`), 2 vuoti.
- **Sei righe col codice fuori posto**: in `lipa.vfi` quattro con nome e codice **scambiati** (`PASW1;CONEGLIANO;`), in
  `lict.vfi:8` e `liph.vfi:2` il codice **in coda al nome** (`MAZARA DEL VALLOCTSE3;;`, `CAORLE - PHE2;;`). Spiegano sei
  dei sette «fix nascosti senza punto» della carta «file per file».
- **Ordine**: 67 file su 77 sono in ordine di **codice** (3 anche di nome), 1 solo di nome, 9 in nessuno.
- **Gemelli** (`VFR_NASCOSTI.fix`, 510 fix): **5 diversi** (`BNNW1` 47 m, `BNSW1` 24 m, `BNW1` 4,2 NM, `RPNE1`
  2,5 NM, `RPSE1` 6,8 NM); **6 punti senza gemello** (`CZE1`, `MCE1`, `PYSW2`, `RFE2`, `RFS4`, `RPSW1`); **1 fix senza
  punto** (`PRNW5`); **2 codici su due punti** (`MJNW1` in `limj.vfi`, `PKS1` in `lipk.vfi`).
- **Rotte**: 16 `.vrt` (uno vuoto, `lirm.vrt`), **52 rotte, 123 punti**: 83 dal `.vfi` dello scalo (per nome, mai per
  codice), 13 dal `.vfi` di uno scalo vicino (`licc` ↔ `licz`, `lire` → `lirl`, `lirn` → `lirm`), 19 fix o navaid, 8 per
  coordinate. **8 rotte militari** (`libv`, `licz`), col 5° campo a 1 su tutte le righe: nessuna a metà.

- **16a (6 ottobre)** — la rotta militare (S4). Codice comune toccato: `Models/Airport/RottaVfr.cs`, `VrtParser`,
  `VrtSaver`. `RottaVfr.Militare` = il 5° campo a 1 su tutte le righe; lo scrittore lo mette su ogni riga, anche su
  quella di un punto aggiunto (prima nasceva senza: la rotta diventava militare a metà). `MilitareAMeta` quando le
  righe non sono d'accordo: la scheda allora non lo fa scrivere. Nella scheda della rotta la casella **Militare**.
- **16b (6 ottobre)** — i controlli (F4, S4). Codice comune toccato: `Validazione/ControlloDeiVfr.cs` e sei regole
  (avvisi). Senza `VFR_NASCOSTI.fix` il sector non segue la convenzione italiana e dei codici non si dice niente.
  - **`GemelloVfrMancante`** (6), **`GemelloVfrDiverso`** (5: a più di un metro), **`FixNascostoSenzaPunto`** (1).
  - **`CodiceVfrRipetuto`** (2), sul secondo punto; il punto «di casa» è quello dello scalo che ha le sue lettere nel
    codice. Di un codice ripetuto non si confronta il gemello.
  - **`CodiceVfrFuoriPosto`** (6), con la riga corretta (`CONEGLIANO;PASW1;…`, `MAZARA DEL VALLO;CTSE3;…`,
    `CAORLE;PHE2;…`): «Correggi tutte le 4 di lipa.vfi». Il codice si riconosce solo se è un fix nascosto o comincia
    con le due lettere dello scalo — 🔴 misurando: `IP31;PL;` e `IP13;PL;` di `lipl.vfi` sono punti che si chiamano
    così, e la prima stesura li dava per scambiati.
  - **`RottaMilitareAMeta`** (0), con la riga corretta verso la maggioranza delle righe.
  - Le coordinate scritte male e i file sotto la sezione sbagliata hanno già le loro regole (slice 2c, F6).
- **16c (6 ottobre)** — il punto nuovo (F3). Solo il Lab: `Core/Copie/CodiciVfr.cs`, `OrdineAlfabetico` (la chiave
  dell'ordine: per un punto VFR il codice, col numero che conta come numero), `NuovoRecord.razor`.
  - «+ Record come questo» in un `.vfi` chiede il nome e il **codice, già scritto**: le lettere del punto da cui si
    parte e il **primo numero libero dopo il suo** — libero in tutto il sector: in nessun `.vfi` e non fra i fix
    nascosti. Da un punto senza codice (`ED`) resta il suo 2° campo. Un codice già preso si rifiuta, dicendo di chi è.
  - Il punto nasce **in ordine di codice** (prima andava in ordine di nome, che è l'ordine di 4 file su 77; di
    codice sono 67).
  - 🔴 Preso sul banco: «Aggiungi» restava spento finché non si sceglieva un **tipo** 0-3. Il tipo è facoltativo e
    non lo scrive nessun punto: ora la domanda c'è («— tipo non scritto») e non ferma (`SceltaDelTipo.Facoltativo`).
  - Il gemello del punto nuovo si crea dalla scheda (**Crea il gemello**, slice 8e), come prima.
- **16d (6 ottobre)** — i tratti delle rotte (F8, S6). Solo il Lab: `TrattiDelleAerovie` vale anche per una
  `RottaVfr`. Nella scheda di una rotta la sezione **Tratti** (verso, minima, massima per tratto), nel file
  `//@@"FOCE DEL SIMETO" dir=both lower="1000ft AGL" upper=2000ft` sopra il punto che apre il tratto, sulla mappa il
  suggerimento al passaggio del mouse, e `TrattoSenzaQuote` per un tratto col verso e senza quote.
- **16e (6 ottobre)** — i punti di una rotta (S4). Solo il Lab: `CatalogoDeiPunti.SuggerisciPerUnaRottaVfr`,
  `CampoPunto.razor`. Scrivendo un punto di una rotta il Lab propone **prima i punti del `.vfi` dello scalo** (anche
  senza scrivere niente), **poi quelli degli altri `.vfi` dal più vicino**, poi fix e navaid; ogni punto VFR dice il
  suo file (`VFR · licz.vfi`). «Chi lo usa» attraversava già gli scali (slice 7): `LENTINI` di `licz.vfi` → 2 righe
  in `licc.vrt`, 3 in `licz.vrt`.
- 🔴 **Trovato a schermo, e non era della slice**: una modifica al **tag di un punto** (i vincoli di SID e STAR della
  9d, i tratti delle aerovie della 14c, ora quelli delle rotte) **non si annullava** dalle modifiche — né l'«annulla»
  della voce né «Annulla tutto». La voce stava sotto una chiave (`@@0.dir`) e si cercava sotto un'altra (il nome
  leggibile, `FOCE DEL SIMETO dir`). Ora la chiave è una, e il nome leggibile è a parte (`ModificaDelMetadato.Nome`).

Uscita sul fork: validatore 235/1 221 → **235 errori, 1 241 avvisi** in `Vipi.SectorfileProva` (**1 249** nel pannello
del Lab): +6, +5, +1, +2, +6. Test: motore 819 → **827**, Lab 907 → **921**. A schermo (banco, copia del fork): i conti
nel pannello; **Correggi tutte le 4 di lipa.vfi**; da `COLOMBO` (`RFS3`, `lirf.vfi`) il codice proposto `RFS5`
(`RFS4` c'è), `PROVA DEL LAB;RFS5;…` dopo `COLOMBO`, e nella scheda «gemello manca · Crea il gemello RFS5»; `libv.vrt`
rotta 1 **Militare** spuntata, tolta la spunta le due righe perdono `;;1;`; `licc.vrt` rotta 7: senza scrivere i
cinque punti di `licc.vfi`, con `LEN` `LENTINI (VFR · licz.vfi)` per primo; il tratto con `both`, `1000ft agl`,
`2000ft`; l'«annulla» di una voce e «Annulla tutto» sui tag.

**Slice 16 chiusa** per le voci F1 (le due strutture che il fork ha), F3, F4, F8, S4, S6. 🔴 **Non fatto, e detto al
committente**:

- **Le altre due strutture** (F1): le rotte en-route di `[VFRENR]` e quelle di `[VFRRTEENR]`. Sul fork non c'è un
  file con quella forma: un lettore senza un caso vero non si può misurare. Si fa quando ne nasce uno (o con F5).
- **Una settantina di punti senza codice** (`ED`, `PA`, `2500`…) non hanno un avviso: non chiedono un gemello, e un
  piano di volo non li riconosce. ✅ Committente, 6 ottobre: **va bene così**, nessun avviso.
- **Il gemello del punto nuovo** non nasce da solo: resta il tasto della scheda (e fino ad allora `GemelloVfrMancante`).
- 🔴 Scelte dell'agente da confermare: il codice proposto è «stesse lettere, primo numero libero dopo quello del
  punto da cui si parte»; due gemelli sono diversi oltre un metro; i punti VFR degli altri scali si propongono in
  ordine di distanza dalla media dei punti dello scalo; la riga corretta di una rotta militare a metà va verso la
  maggioranza delle righe.

Da provare a mano: prove 212-222.

**Decisioni del committente del 6 ottobre** (sulle domande nate dalla misura): i punti senza codice restano senza
avviso; dei cinque gemelli diversi **è giusto il fix**.

- **16f (6 ottobre)** — la riga corretta dei gemelli diversi. Codice comune toccato: `ControlloDeiVfr`.
  `GemelloVfrDiverso` porta la riga del `.vfi` col punto **dov'è il fix nascosto**, scritta nella forma della riga
  (compatta o coi punti): `PORTO CESAREO;BNW1;N0401651000;E0175025000;` → `…;N0401407000;E0175437000;`. «Correggi
  tutte le 3 di libn.vfi», le 2 di `lirp.vfi`. 🔴 La decisione è sui cinque del fork: per un caso nuovo la riga è
  una proposta, e chi sa che è giusto il punto sposta il fix dalla scheda (il gemello lo segue).

### Slice 18 — ATIS e D-ATIS (`.atis`, `.datis`, `atisextra.fds`) — dal 6 ottobre

Fatta prima della 17 (i simboli aspettano la prova T3 del committente in Aurora).

**Manuale IVAO** (riletto il 6 ottobre): `[ATIS]` dice solo di guardare lo strumento «ATIS Creator» (già esaminato,
carta «file per file» §22: dieci segnaposto); `[ATISFIELD]` e i `.fds` **non ci sono**; di `[ATC]` l'8° campo è
«DAtis: Assign Digital ATIS structure». Come Aurora riempia e legga un modello non sta scritto da nessuna parte.

**Misura sul fork** (`8cf32c6`):

- **7 `.atis`**, tutti di una riga: `default` e `arrdep` generici (con `[STATION_NAME]`), `lica`, `lied`, `limc`,
  `liml`, `lipz` col nome dello scalo scritto per la voce («mlpainsa», «lee,NAH,teh», «Venetziah»). **4 `.datis`**:
  `datis-ad`, `datis-arrdep`, `datis-acc` (solo `CPDLC ID [CPDLC]`) e `datis.datis`, **vuoto e voluto**.
- **Segnaposto usati**: `STATION_NAME`, `ATIS_LETTER`, `ATIS_TIME`, `ARR_TYPE`, `ARR`, `DEP`, `TL`, `METAR`, `QFE`,
  `REMARK`, `CPDLC`. Mai `DEP_FREQ` e `TA`. `ARR_TYPE` viene da `atisextra.fds` (`Type of Approach;[ARR_TYPE];`).
- **Parti facoltative**: `[Arrival runway [ARR]]`, `[Q F Echo [QFE]]`… un livello solo, un segnaposto per parte.
- 🔴 **Una parentesi in più in `default.atis`**: `[Runway in use [ARR]]].` — è il modello di **102 posizioni**. Con
  i valori d'esempio esce «Runway in use 16L].».
- **Chi usa cosa** (righe dei `.frq`): `default.atis` + `datis-ad` 98, `arrdep` + `datis-arrdep` 22, `limc` +
  `datis-arrdep` 18, `lipz` 8, `liml` 8, `lied` 6, `lica` 2 (tutti con `datis-ad`), `default` + `datis.datis` 4; 176
  posizioni col solo `datis.datis`, 39 col solo `datis-acc`. Le sette coppie differiscono **solo** per
  `[STATION_NAME]` (il modello di uno scalo dice il nome) e per `[CPDLC]` (solo nel D-ATIS).

- **18a (6 ottobre)** — il motore. Codice comune toccato: `IO/ModelloAtis.cs` (nuovo), `IO/Parsers/FdsParser.cs`,
  `IO/Savers/FdsSaver.cs`, `Models/Airport/CampoDellAtis.cs` (nuovi), `IO/Formati.cs`,
  `Validazione/ControlloDegliAtis.cs` (nuovo), quattro regole (avvisi), `ValidatoreDellAlbero`.
  - **I `.datis` si leggono come gli `.atis`** e i **`.fds`** hanno il loro lettore (`Etichetta;[SEGNAPOSTO];`):
    prima erano testo. Il 28° tipo di record del motore.
  - **`ModelloAtis.Leggi`**: un modello in testo, segnaposto e parti facoltative (anche annidate), con le posizioni
    delle parentesi che non tornano; non fallisce mai. Una parentesi che tiene solo un nome in maiuscole è un
    segnaposto, se no una parte facoltativa. **`Riempi`**: un segnaposto vuoto sparisce con la sua parte; uno che
    nessuno conosce resta scritto com'è.
  - **`SegnapostoSconosciuto`**: non è fra i dodici di Aurora (i dieci di ATIS Creator, `QFE`, `CPDLC`) né in un `.fds`.
  - **`ParentesiNonBilanciate`**: una `]` in più, con la riga corretta; una `[` mai chiusa, senza.
  - **`CampoDellAtisMaiUsato`**: un campo di un `.fds` che nessun modello cita.
  - **`AtisEDatisDiversi`** (W3): l'ATIS e il D-ATIS che una posizione usa insieme non hanno gli stessi segnaposto;
    `STATION_NAME` e `CPDLC` non contano. Uno per coppia, sull'ATIS.
  - `datis.datis` vuoto **non** è `FileVuoto` (W4). `\liml.atis` resta dov'era: Aurora lo trova (R-10, slice 11).
  Uscita sul fork: **1** `ParentesiNonBilanciate` (`default.atis`), 0 gli altri tre.
- **18b (6 ottobre)** — l'editor (W1). Solo il Lab: `ModelloAtisNellaScheda.razor`, `PezziDelModello.razor`,
  `Core/Ispezione/ModelliAtisDelLab.cs`. Nella scheda di un modello la sezione **Modello**: il testo in un campo
  largo; sotto, lo stesso testo **fatto a pezzi** — i segnaposto come etichette (col significato al passaggio del
  mouse), le parti facoltative in un riquadro tratteggiato, la parentesi che non torna detta col suo carattere, il
  segnaposto che nessuno conosce segnato; e i **segnaposto da scegliere** (i dodici di Aurora e quelli dei `.fds`,
  tenui quelli già usati): un clic lo mette **dove sta il cursore**. Il record nell'elenco si chiama «Modello», non
  più `AtisData`.
- **18c (6 ottobre)** — anteprima e «Ascolta» (W1, W2). `Ui/Servizi/Voce.cs`. **Anteprima**: il modello riempito con
  valori d'esempio (stazione, lettera, ora, piste, livello, un METAR, QFE, `ILS` per i campi dei `.fds`; note e TA
  vuote), che si cambiano lì sotto senza toccare il sector: svuotato un valore, la sua parte sparisce. **▶ Ascolta**
  la legge con una voce di Windows (SAPI 5, chiamata per nome: nessun pacchetto in più), scelta fra quelle
  installate — di base la prima inglese; **■** la ferma. Dove non c'è una voce il tasto è spento, col perché.
- **18d (6 ottobre)** — il modello accanto (W3). Nella scheda di un ATIS **Il D-ATIS accanto** (e viceversa): i
  modelli che le posizioni dei `.frq` usano insieme a questo, con quante posizioni, il loro testo a pezzi, la loro
  anteprima con gli stessi valori, e i segnaposto che ha solo l'uno o solo l'altro. Le pronunce non si toccano.

Uscita sul fork: validatore 235/1 241 → **235 errori, 1 242 avvisi** in `Vipi.SectorfileProva` (**1 250** nel pannello
del Lab): +1. Round-trip **723/723** (erano 718: i quattro `.datis` e il `.fds` ora hanno un lettore; restano 25 file
senza), tutto toccato 0, una modifica per record 115 620. Test: motore 827 → **839**, Lab 921 → **930**. A schermo (banco, copia del fork): `default.atis` →
nove segnaposto, quattro parti facoltative, «Una «]» in più al carattere 141», l'anteprima con «Runway in use 16L].»;
`[DEP]` messo al cursore dopo «This is »; il D-ATIS accanto `datis-ad.datis` «98 posizioni li usano insieme» e
`datis.datis` «vuoto (voluto)»; **Correggi la riga** dal pannello toglie la parentesi; le voci offerte sul PC del
committente sono Hazel (inglese britannico, di base) e Zira (americano).

**Slice 18 chiusa** per le voci W1-W4. 🔴 **Non fatto, e detto al committente**:

- **«Un METAR vero»**: l'anteprima ha un METAR d'esempio scritto nel codice, non quello di adesso: il Lab non va in
  rete. Si scrive a mano nel suo campo.
- **Come legge Aurora**: lettera, ora e METAR Aurora li trasforma prima di dirli (la lettera in «Alfa»? il METAR
  decodificato?), e non si sa come. Nell'anteprima entrano come sono scritti: per sentire una pronuncia basta; per
  sentire l'ATIS intero com'è in Aurora no.
- **Quale voce**: che Aurora usi le voci di Windows, e quale, non è scritto. Il Lab le offre tutte.
- **«Ascolta» sulle casse** l'agente non l'ha provato (per non far parlare il PC del committente): la voce è provata
  scrivendo la lettura in un file `.wav`.
- 🔴 Scelte dell'agente da confermare: `STATION_NAME` e `CPDLC` non contano nel confronto fra ATIS e D-ATIS; i valori
  d'esempio; la voce di base (la prima inglese); il segnaposto riconosciuto solo se è tutto maiuscolo.

Da provare a mano: prove 223-230.

### Slice 19 — La prova del lotto intero — dal 6 ottobre

Due cose (carta §3 riga 19, §5.3): la **lettura di prova dei tag da parte di vIPI**, senza cambiare il sito, e il
**giro di prove del committente**. La prima è fatta; il giro è preparato e aspetta lui. La slice 17 (simboli) resta
ferma sulla prova T3 in Aurora.

- **19a (6 ottobre)** — i tag letti da vIPI. Codice comune toccato: `tools/Vipi.SectorfileProva/Concordanza.cs` e
  `Program.cs` (misura 8), `tests/Vipi.Infrastructure.Tests/ConcordanzaSectorfileTests.cs` (il file della
  concordanza è compilato nei due posti, come dalla slice 9 di F2). **Nessun file di `src/` del sito è cambiato.**
  - **Cosa si prova.** «vIPI legge, non scrive» (§M regola 10): il Lab scrive i metadati nel sector come righe `//@`,
    e vIPI riempirà i suoi campi (fix intero, salita iniziale) leggendoli. Prima di toccare il sito servono due
    certezze: che per il lettore di produzione di **oggi** (`AuroraSectorfileParser`) un file coi tag sia lo stesso
    file, e che il **nome** con cui vIPI tiene una procedura sia la chiave buona per ritrovare i suoi tag.
  - **Come.** `Concordanza.DeiTag(file)`: il motore etichetta ogni record come farebbe il Lab — `//@source` in testa,
    la dichiarazione `//@"NOME" …` con le sue chiavi (per SID e STAR `fix=`, `initialclimb="COO APP"` con lo spazio,
    `nav=`; per gli altri `note=`), i `//@@` coi vincoli su ogni punto di SID e STAR — poi il lettore di vIPI legge il
    file com'è e coi tag, e i due esiti si confrontano oggetto per oggetto. Per SID e STAR, ogni procedura letta da
    vIPI cerca per nome i tag che il motore rilegge, e deve trovarci i valori scritti.
  - **Tutti i lettori di vIPI**: `ParseSids`, `ParseStars`, `ParseNavaids` (fix, VOR, NDB), `ParseMva`,
    `ParseTowerShapes` e `ParseSectorShapes` (`.tfl`), `ParseAtcPositions` (`.frq`), `ParseAirports` (`.ap`),
    `ParseRunwayEnds` (`.rw`).
  - **In CI**: 15 campioni letti uguali coi tag, le procedure di `lirf.sid` e `lirf.str` che ritrovano tutte i loro
    tag, e la stessa prova scritta per esteso su una SID col blocco come lo lascia il Lab (`fix=EKLOS`,
    `initialclimb="COO APP"`, `//@@"EKLOS" role=IAF alt=+FL80`).
  Uscita sul fork: **231 file** etichettati (8 458 record), il lettore di vIPI legge **gli stessi 7 860 oggetti**
  (0 cambiati); **1 516** voci di SID e **863** di STAR — tutte quelle che vIPI legge, una per pista — ritrovano i
  loro tag per nome, **0** no. Il sito può leggere i metadati quando vorrà: i tag non rompono niente di quel che
  legge oggi, e il nome della procedura basta come chiave.
- **19b (6 ottobre)** — il giro preparato. In `SectorLab-prova\\PROVE.md`, in testa, **Il giro del lotto**: come farlo
  (153 prove, dalla 78 alla 230, 17 con Aurora), le nove cose da guardare in Aurora e le tredici da decidere, ognuna
  col numero della sua prova, e le scelte dell'agente che aspettano un sì o un no. 🔴 Preparandolo: i conti «in alto»
  di dieci prove erano rimasti a quelli del giorno in cui la prova era nata (982, 1 019, 1 075… contro i 1 250 di
  oggi), e tre domande avevano già avuto risposta (`SettoreSenzaPosizione` resta un avviso, AGL e AMSL servono, le
  etichette lontane si rifanno a metà): riallineati.

Test: `Vipi.Infrastructure.Tests` 2 083 → **2 101** (net8.0 e net10.0); motore 839 e Lab 930 invariati.

**Stato del lotto** (6 ottobre): slice 0-16 e 18 chiuse; la **17** aspetta la prova T3; della **19** resta il giro del
committente. Il lotto si chiude quando il giro è ✅ e la 17 è fatta.
