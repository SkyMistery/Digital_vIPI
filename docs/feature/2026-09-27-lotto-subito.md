# Lotto «Subito» — le voci «Subito» del giro dei file, in slice e in ordine (dal 27 settembre 2026)

> Carta dei bisogni: [`2026-09-24-file-per-file.md`](2026-09-24-file-per-file.md) (§1-§22 le voci, §M i metadati,
> §M-G i generatori, §R la revisione, §C i meccanismi comuni). Carta madre:
> [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md). Carte dell'app:
> [F3](2026-09-22-f3-l-app.md), [F3-bis](2026-09-23-f3-bis-copie-e-mappe-composte.md). Metodo:
> [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Stato — 27 settembre 2026

**Approvata** (§5). Fatte la slice 0, la slice 1 (1a-1e), la slice 2 (2a-2c), la slice 3 (3a-3e), la slice 4 (4a-4d), la slice 5 (5a-5d) e la slice 6 (6a-6c); in corso la **slice 7** (7a-7c fatte; §6 «Traccia»). Tutte le voci
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
