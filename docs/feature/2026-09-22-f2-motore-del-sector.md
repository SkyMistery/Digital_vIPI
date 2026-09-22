# F2 — Il motore del sector: leggere, capire, validare e riscrivere l'albero intero (22 settembre 2026)

> **Stato: 🟡 IN CORSO** — le quattro proposte del §9 **approvate dal committente il 22 settembre**; slice 0 e 1 fatte. Seconda fase di Aurora Sector Lab
> ([carta madre](2026-09-18-aurora-sector-lab.md), §7 e §8.2). Nessuna interfaccia: F2 è la libreria che F3
> (l'app) userà per aprire, mostrare e scrivere i file. Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> 🔴 **Nessun dato di vIPI si tocca**: niente migrazioni, niente tabelle, l'import di vIPI resta com'è.

## §0 — La domanda

Il piano (carta madre §7) chiede a F2: i formati del sector **ereditati dalla libreria A**, in più `.vrt`,
`.hold`, aree P/R/D e i due DMS; un **validatore**; finita quando il **round-trip è a zero differenze
sull'albero intero**. Più i tag `//@` decisi il 21 settembre (§8.2): solo `.sid`/`.str`, aggancio col nome.

La domanda vera, dopo la misura del §1, è un'altra: il round-trip c'è **già**. Quello che manca è che il motore
**capisca** ogni riga che riscrive — altrimenti l'app di F3 non può mostrarla, spostarla né controllarla.

## §1 — Le misure (22 settembre, `origin/master` del sector = `7e761aa`, 749 file)

Harness fuori repo: `scratchpad\f2-misure\rt` (la libreria A, senza modifiche, su una copia di `git archive`).

**Round-trip byte per byte: 681 file su 681 leggibili, zero differenze.** Il lavoro di A regge ancora, tre mesi e
mezzo e molti AIRAC dopo. Il criterio di F2 così com'era scritto è già soddisfatto — ma in modo ingannevole:

**7 579 righe passano intatte perché A NON le capisce** («Skipping malformed line»): le conserva come testo e le
riscrive uguali. Round-trip perfetto, contenuto invisibile. Divise per forma:

| righe | dove | che cosa sono davvero | esempio |
|---:|---|---|---|
| 5 041 | `.artcc` | righe `T;` del bordo con un'etichetta di gruppo | `T;COPs;N044.18.35.717;E010.55.53.893;` (`FRA-gates.artcc`) |
| 2 041 | `.fix` | fix a **4 campi** (tipo 3 = nascosto, senza i campi opzionali) | `BC404;N039.05.11.290;E017.03.27.750;3;` (`APT.fix`) |
| 315 | `.tfl` | vertici dati **per nome di punto** | `AMSOR;AMSOR;` |
| 84 | `.sid` | vertici per nome, e tracciati sotto la label | `ZULU;ZULU;` |
| 35 | `.frq` | elenchi di scali lunghi | `LIZZ_AEW_CTR;136.400;LIMM LIRR …` |
| 32 | `.pol` | righe vuote dentro un poligono (in Aurora **spezzano** il tracciato) | |
| 18 | `.mva` | bordi `T;` per nome di punto | `T;LIRR;UTENO;UTENO;LIRR;` |
| 10 | `.geo` | colore vuoto in coda | `…;E013.18.35.124;;` |
| 3 | `.vor` | **errori veri** | `KPT;108.40;N047.44.75.000;…` (minuti 75), `n045.44.52.080` minuscolo, `GRO;;` senza frequenza |

Le tre del `.vor` sono il primo lavoro del validatore; tutte le altre sono **forme legittime** (le documenta la
specifica di B, `SPECIFICA_FORMATI.md` §1, §4.3, §6) che A non modella. La radice comune è una: **il punto per
nome** (`AMSOR;AMSOR;`) al posto delle coordinate. Aurora lo ammette ovunque ci sia una coppia lat/lon.

**File senza lettore in A: 68.** Di questi contano per F2:

| formato | file | righe | forma |
|---|---:|---:|---|
| `.vrt` (rotte VFR) | 16 | 140 | `N;NOME;NOME;` — numero di rotta + punto per nome; riga vuota separa le rotte |
| `.hold` (attese ENR) | 1 | 71 | `HLD-ABBOZ;lat;lon;ABBOZ/225R-9000;` — descrizione = fix/rotta di avvicinamento+verso-quota |
| `.restrict` `.prohibit` `.danger` (aree P/R/D) | 3 | ~2 500 in `italy.restrict` | è il formato `.geo` (segmenti) con tipo e nome in coda, `//R4` come titolo |

Gli altri (`.txt` 22, `.cpr` 15, `.datis` 4, `.clr`, `.def`, `.sym`, `.cpdlc*`, `.fds`, `.md`) sono testo,
profili e configurazione: **passano intatti come testo**, F2 non li interpreta.

Sul `master` le righe `//@` sono ancora **zero**; `.sid`+`.str` = 149 file, 42 385 righe, 1 268 label di SID.
Il repo del sector è **pubblico** (GitHub `ivao-italy/it-aurora-sector`).

## §2 — Il motore

### 2.1 Dove

Un progetto nuovo, **`Vipi.Sectorfile`** (`net8.0;net10.0`, nessuna dipendenza: né EF, né ASP.NET, né
`Vipi.Infrastructure`), accanto agli altri in `src/`. Lo useranno il Lab (F3) e, quando servirà, vIPI.
Perché non dentro `Vipi.Infrastructure/Sectorfile`: il Lab non deve trascinarsi dietro EF, MySQL e il resto
dell'host; e un motore senza dipendenze si prova da solo.

### 2.2 Che cosa entra dalla libreria A (circa 5 500 righe: IO + Models + Shared)

- `SectorFileReader` (codifica: UTF-8 stretto con ripiego Windows-1252, BOM, fine riga, riga finale).
- I 21 lettori e 20 scrittori, il modello a **riga grezza + record letto** (è ciò che rende il round-trip esatto:
  una riga non toccata si riscrive coi suoi byte).
- `FileSaverOrchestrator.WriteAtomic` (`.tmp` + flush + `File.Replace`) — ⚠️ **senza** i marcatori
  `//Start`/`//End` che inietta (§8 della carta madre).
- `CoordinateConverter`, `WarningCollector`, `IscLoader` (poi col multi-master di B, in F3).
- Fuori: `ParserRegistry` (codice morto), la UI WPF, i ViewModel.

Si porta **adattato alle regole del repo** (avvisi = errori, nullable, multitarget net8 senza C#13 né API .NET9+),
non riscritto. I test di A vengono con lui.

### 2.3 Che cosa si aggiunge

1. **Il punto** come tipo unico: DMS puntato, DMS compatto, decimale **o nome** — e ricorda la forma in cui era
   scritto, così una riga modificata si riscrive nello stesso stile del file (i due DMS convivono, anche nello
   stesso `itgeo.geo`: 8 914 puntati e 4 658 compatti).
2. **Le 7 579 righe opache diventano record**: `T;` con etichetta di gruppo, fix a 4 campi, vertici per nome in
   `.tfl`/`.sid`/`.str`/`.mva`, `.frq` lunghi, colore vuoto, righe vuote come **interruzione** di tracciato.
3. **Lettori nuovi**: `.vrt`, `.hold`, `.restrict`/`.prohibit`/`.danger`.
4. **I tag `//@`** (carta madre §8.2), solo per `.sid`/`.str`:
   - a livello di file: `//@source=AIRAC2610`;
   - per procedura: `//@BANA6W` · `//@START` · … · `//@END BANA6W`;
   - sul record: `//@BANA6W fix=BANAV initialclimb=5000`, una riga, subito sopra la label.
   Lettura e scrittura in **un** posto (il catalogo delle chiavi è un contratto Lab ↔ vIPI). Un file senza tag si
   legge come oggi; un tag cambia solo le righe dove compare.
5. **Il validatore** (§3).

### 2.4 Il rapporto con quel che vIPI ha già

vIPI legge il sector in `Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs` (703 righe: navaid, SID/STAR,
MVA, forme di settore e di torre, posizioni, scali, piste) e ha il suo DMS (`TryParseDms`, `DmsCoordinate`).
**In F2 non si tocca**: l'import di produzione resta com'è. Due verità sul formato però non possono convivere in
silenzio, quindi F2 porta una **prova di concordanza**: sull'albero vero, il motore nuovo e il lettore di vIPI
devono dare gli stessi punti (nomi e coordinate) e le stesse SID/STAR. Il passaggio dell'import al motore nuovo
è un lavoro a parte, dopo (con la lettura di `initialclimb` dal `//@`, F7).

## §3 — Il validatore

Regole, ognuna con gravità (errore / avviso) e con la riga e il file:

| regola | esempio vero di oggi |
|---|---|
| coordinata fuori campo (minuti o secondi ≥ 60, gradi oltre il limite) | `itvor.vor:109` `N047.44.75.000` |
| emisfero minuscolo o sconosciuto | `itvor.vor:125` `n045.44.52.080` |
| campo obbligatorio vuoto | `itvor.vor:81` `GRO;;` |
| DMS e decimale mescolati sulla stessa riga | (vietato dalla specifica) |
| **punto per nome non risolto** nei cataloghi dichiarati dall'`.isc` | B ne trovò 1 |
| nome duplicato dove dev'essere unico (fix, ARTCC) | B: 214 chiavi duplicate |
| poligono con meno di 3 vertici | |
| file citato dall'`.isc` e assente / file presente e mai citato | B: 31 orfani |
| `//@` il cui nome **non** è quello del record sotto | (la guardia decisa il 21-set) |
| `//@START` senza `//@END`, o `//@END` con un altro nome | |

I numeri di B sono di giugno: la slice del validatore li rimisura sul `master` di oggi, e la carta si aggiorna
coi suoi.

## §4 — Cosa F2 NON fa

- Nessuna finestra, nessuna mappa, nessun git: sono F3 e F4.
- Non converte i file: nessun `//@` scritto sul sector in F2 (la conversione è graduale, la fa l'app su ciò
  che l'AOD tocca).
- vIPI non legge ancora i `//@`, e il suo import non cambia (§2.4).
- Non interpreta `.txt`, `.cpr`, `.datis`, `.clr`, `.def`, `.sym`, `.cpdlc*`, `.fds`: li porta intatti.
- Non corregge gli errori che trova: li segnala.

## §5 — Le slice

| # | cosa | prova |
|---|---|---|
| 0 | Progetto `Vipi.Sectorfile` vuoto nella soluzione, multitarget, e il suo progetto di test | build Release verde sui due TFM, conta-test aggiornato |
| 1 | Porto di A **senza cambi di comportamento**, coi suoi test | i test di A verdi; round-trip dell'albero: **681/681** e **7 579** righe opache (la misura di oggi, fissata) |
| 2 | Il punto unico (4 forme, stile ricordato); il DMS confrontato con quello di vIPI | ogni coordinata dell'albero dà lo stesso valore nei due |
| 3 | Punti per nome in `.tfl`/`.sid`/`.str`/`.mva` | le righe opache scendono di 417, round-trip sempre esatto |
| 4 | `.fix` a 4 campi, `T;` degli `.artcc`, `.frq` lunghi, colore vuoto, interruzioni | opache: restano solo le 3 del `.vor` |
| 5 | `.vrt`, `.hold`, aree P/R/D | tutti i 20 file letti e riscritti uguali |
| 6 | `//@` in `.sid`/`.str`: lettura e scrittura | un file con tag e uno senza; nome che non combacia segnalato |
| 7 | Il validatore | le tre righe del `.vor` trovate; i numeri di B rimisurati e scritti qui |
| 8 | Prova di concordanza col lettore di vIPI | stessi punti e stesse SID/STAR sull'albero intero |
| 9 | Chiusura: carta, lavori aperti, memorie | |

Un commit per slice; `dotnet build Vipi.slnx -c Release --no-incremental` verde su entrambi i TFM a ogni commit;
`conta-test.sh --scrivi` nello stesso commit dei test nuovi.

**L'albero vero nei test.** Il sector è pubblico, ma 13 MB non entrano nel repo di vIPI. Proposta: nel repo
**pochi file veri piccoli** come campioni (uno per formato, scelti fra quelli con le forme del §1), e la prova
sull'albero intero come **strumento locale** (`tools/`) che si lancia puntando a una copia del sector. In CI
girano i campioni; l'albero intero si prova a ogni slice del motore, a mano, e il risultato si scrive qui.

## §6 — Pre-flight, le quattro domande

1. **Modello**: un modello solo del file (riga grezza + record), quello di A. Il punto per nome è **un'altra
   forma del punto**, non un tipo di record in più. I `//@` sono righe del file come le altre, con un record loro.
2. **Dispatch**: il formato si decide in **un** posto, dall'estensione (e dal nome: `*fic.tfl`, `ENRMVA/`), come
   in A. `.restrict`/`.prohibit`/`.danger` sono il lettore `.geo` con due campi in più, non un lettore nuovo.
3. **Ingressi + verifica**: nessun ingresso utente in F2. Si verifica con i campioni in CI e l'albero intero in
   locale; il numero delle righe opache è la misura che scende slice dopo slice.
4. **Propagazione**: niente si rimuove né si rinomina in vIPI. Si **aggiunge** un progetto; `AuroraSectorfileParser`
   resta, e la sua coesistenza è tenuta onesta dalla prova di concordanza (slice 8) e scritta nei lavori aperti.

## §7 — Trappole note

- 🔴 **Un round-trip perfetto non prova la lettura**: lo dice il §1. Ogni slice si misura anche sulle righe
  opache, non solo sui byte.
- 🔴 **Codifica**: mai UTF-8 forzato (è l'errore di B). Il lettore di A decide per file e lo scrittore ricorda.
- ⚠️ In `[GEO]` e nei poligoni una riga vuota o un commento **spezza** il tracciato: non è rumore da saltare.
- ⚠️ Nome o coordinata: oggi **nessun** nome di punto (`.fix`/`.vor`/`.ndb`/`.vfi`) comincia con un emisfero
  seguito da una cifra, quindi la distinzione è netta. Il validatore lo tiene vero: un nome così è un errore.
- ⚠️ `dotnet test` esce 0 anche rotto: il verde si legge contando i progetti.
- 🔴 Sorgenti **solo con l'editor**, mai con heredoc (caratteri di controllo nelle regex, già pagato).

## §8 — Definition of done

- [ ] Slice 0-9, un commit ciascuna, build Release verde sui due TFM, suite verde contando i progetti.
- [ ] Albero intero di `master`: round-trip a zero differenze **e** righe opache = solo gli errori veri.
- [ ] Validatore: errori veri del sector elencati qui, da passare agli AOD (con `R47` di F1).
- [ ] Prova di concordanza col lettore di vIPI verde.
- [ ] Nessun dato di vIPI toccato; nessuna migrazione; l'import di produzione invariato.

Commit: slice 0 `de0a3cad` (progetto e test vuoti; l'unico test, `NessunaDipendenzaTests`, è stato provato
**rosso** aggiungendo per un momento un riferimento a `Vipi.Domain`: fa il nome dell'assieme estraneo) ·
slice 1 = questo.

**Slice 1 — il porto di A** (commit `5761d00` di A; Shared + Models + IO, 82 file; test di IO, Models e
Shared). Dei sorgenti è cambiato il namespace (`AuroraSectorDrawer` → `Vipi.Sectorfile`) e nient'altro: la
build Release con avvisi = errori è passata **al primo colpo sui due TFM**. Prova: **236 test verdi** su
net8 e net10; `tools/Vipi.SectorfileProva` sul `master` `7e761aa` dà **681 esatti, 0 diversi, 68 senza
lettore, 7 579 righe opache** — la misura del §1, identica.

Scostamenti, decisi strada facendo:
- **Il pacchetto `System.Text.Encoding.CodePages` di A non serve**: su net8/net10 le code page sono nel
  runtime. Il motore resta a zero dipendenze.
- **`ParserRegistry` fuori** (codice morto, §2.2) coi suoi 5 test; due commenti che lo citavano, riscritti.
- **`WarningCollector` rimandato a F3**: vive nei Services di A e dipende da `ILogger`. I test usano già il
  loro doppione, e lo strumento il suo.
- 🔴 **I test su file veri ora falliscono se il file manca.** In A tornavano verdi a vuoto quando l'albero non
  c'era (cioè sempre in CI): ~30 test che non provavano niente. Qui leggono `tests/Vipi.Sectorfile.Tests/
  Campioni/` (30 file veri dal `master`, ~1,1 MB, `.gitattributes` senza conversione dei fine riga) e un
  campione assente è rosso. La regola si è pagata subito: 5 file passati come parametro di Theory erano
  sfuggiti al censimento, e sono venuti fuori rossi invece che verdi.
- Fuori per peso `GEO/itgeo.geo` e `GEO/lirf.geo` (1,2 MB, due soli confronti di byte): il loro round-trip
  lo fa lo strumento. Fuori anche i 7 segnaposto `Skip` di §27.2-§27.9 (aspettavano i caricatori di A, F3).
- `RealFileIntegrationTests` (§27.1, l'albero intero) è diventato **`tools/Vipi.SectorfileProva`**, nella
  soluzione perché compili sempre, lanciato a mano.

## §9 — Decise col committente prima della slice 0 (✅ tutte e quattro, 22 settembre)

1. **Progetto nuovo `Vipi.Sectorfile`** (proposta) invece di allargare `Vipi.Infrastructure/Sectorfile`.
2. **Campioni veri in repo + albero intero in locale** (proposta) invece di scaricare il sector in CI.
3. **Porto di A adattato**, non riscritto (proposta): le 5 500 righe e i 310 test vengono con la loro storia,
   il primo commit dice da dove.
4. vIPI **non cambia** in F2 (proposta): la lettura dei `//@` da parte dell'import arriva con F7.
