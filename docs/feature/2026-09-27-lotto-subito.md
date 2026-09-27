# Lotto «Subito» — le voci «Subito» del giro dei file, in slice e in ordine (dal 27 settembre 2026)

> Carta dei bisogni: [`2026-09-24-file-per-file.md`](2026-09-24-file-per-file.md) (§1-§22 le voci, §M i metadati,
> §M-G i generatori, §R la revisione, §C i meccanismi comuni). Carta madre:
> [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md). Carte dell'app:
> [F3](2026-09-22-f3-l-app.md), [F3-bis](2026-09-23-f3-bis-copie-e-mappe-composte.md). Metodo:
> [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Stato — 27 settembre 2026

**Approvata** (§5). Fatte la slice 0, la slice 1 (1a-1e) e la **2a**; in corso la slice 2 (§6 «Traccia»). Tutte le voci
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
