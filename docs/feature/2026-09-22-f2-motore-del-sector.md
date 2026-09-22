# F2 — Il motore del sector: leggere, capire, validare e riscrivere l'albero intero (22 settembre 2026)

> **Stato: 🟡 IN CORSO** — le quattro proposte del §9 **approvate dal committente il 22 settembre**; slice 0-7 fatte. Seconda fase di Aurora Sector Lab
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
| frazione dei secondi che non ha 3 cifre (si legge, ma è ambigua: chi l'ha scritta intendeva millesimi?) | `N046.34.25.8735` (`lovv.tfl`), `N44.18.55.72` (tre `.rw`), `E015.37.07.1000` (tre `.str`), `N043.49.49.00` (`.hartcc`) — slice 2 |
| coppia decimale (legale, ma rara: 38 righe in 4 `.str`) | `41.00850773;16.07432896;` — da segnalare solo come avviso |
| campo obbligatorio vuoto | `itvor.vor:81` `GRO;;` |
| DMS e decimale mescolati sulla stessa riga | (vietato dalla specifica) |
| **punto per nome non risolto** nei cataloghi dichiarati dall'`.isc` | B ne trovò 1 |
| punto per nome con **due nomi diversi** (Aurora prende la latitudine dal primo e la longitudine dal secondo) | 7 righe di `.str`: `ALPHA SOUTH;ALPHA SUOTH` (refuso), `MC905;MC904` |
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
| 3 | **Riga come campi** (§9.5): la base fotografata all'apertura, la fusione a tre per righe e per campi | «una modifica per record» = **una riga** cambiata per record, e «tutto toccato» a zero |
| 4 | Punti per nome in `.tfl`/`.sid`/`.str`/`.mva`, un tipo solo per il punto | le righe opache scendono di 417, round-trip sempre esatto |
| 5 | `.fix` a 4 campi, `T;` degli `.artcc`, `.frq` lunghi, colore vuoto, interruzioni | opache: restano solo le righe dei `.vor` che sono errori veri |
| 6 | `.vrt`, `.hold`, aree P/R/D | tutti i 20 file letti e riscritti uguali, anche con una modifica per record |
| 7 | `//@` in `.sid`/`.str`: lettura e scrittura | un file con tag e uno senza; nome che non combacia segnalato |
| 8 | Il validatore | le righe del `.vor` trovate; i numeri di B rimisurati e scritti qui |
| 9 | Prova di concordanza col lettore di vIPI | stessi punti e stesse SID/STAR sull'albero intero |
| 10 | Chiusura: carta, lavori aperti, memorie | |

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
  🔴 Qui c'era scritto anche «o con una cifra»: falso, i VRP dei `.vfi` si chiamano `2NM NORTH LUCERA` (slice 6).
  Cifra in testa è coordinata solo **senza lettere**.
- ⚠️ `dotnet test` esce 0 anche rotto: il verde si legge contando i progetti.
- 🔴 Sorgenti **solo con l'editor**, mai con heredoc (caratteri di controllo nelle regex, già pagato).

## §8 — Definition of done

- [ ] Slice 0-9, un commit ciascuna, build Release verde sui due TFM, suite verde contando i progetti.
- [ ] Albero intero di `master`: round-trip a zero differenze **e** righe opache = solo gli errori veri **e**
      «tutto toccato» a zero righe cambiate (un record toccato ma non cambiato esce com'era).
- [ ] Validatore: errori veri del sector elencati qui, da passare agli AOD (con `R47` di F1).
- [ ] Prova di concordanza col lettore di vIPI verde.
- [ ] Nessun dato di vIPI toccato; nessuna migrazione; l'import di produzione invariato.

Commit: slice 0 `de0a3cad` (progetto e test vuoti; l'unico test, `NessunaDipendenzaTests`, è stato provato
**rosso** aggiungendo per un momento un riferimento a `Vipi.Domain`: fa il nome dell'assieme estraneo) ·
slice 1 `fda60bfa`.

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

**Slice 2 — il punto** (commit = il successivo a `fda60bfa`). Tre cose, tutte sul punto:

1. **La lettura si allinea al DMS di vIPI.** La concordanza (nuova misura dello strumento: ogni token DMS
   dell'albero letto da `DmsCoordinate` di vIPI e dal motore) dava **8 discordi su 685 561** col convertitore
   di A. Quattro difetti veri di A, tutti da una radice: la parte dopo il terzo punto letta come
   **millisecondi interi** invece che come frazione dei secondi —
   `N046.34.25.8735` (`lovv.tfl:70`) spostato di 8,7 s, ~270 m; `N44.18.55.72` (una soglia pista in
   `limm.rw`, `lipp.rw`, `lirr.rw`) di 0,65 s, ~20 m; `E015.37.07.1000` (`libf.str`, `licc.str`, `lipk.str`)
   di 0,9 s — e l'emisfero minuscolo `n045.44.52.080` (`itvor.vor:125`), che faceva perdere il VOR VBA. In più
   il tetto dei gradi (90/180), che A non controllava. Dopo: **0 discordi**. La controprova si è fatta
   rimettendo il convertitore di A: gli 8 ricompaiono.
2. **Il punto si legge come COPPIA** (`CoordinateConverter.ParsePair`), in tutti i 16 lettori. A leggeva i due
   token da soli e ricomponeva: una coppia **decimale** (`41.00850773;16.07432896;`, 38 righe di
   `liba/libd/lict/lire.str`) usciva con **longitudine 0**, un punto nel golfo di Guinea, senza avvisi; e una
   coppia con gli assi scambiati diventava 0,0. Ora la coppia decide insieme: due decimali, o N/S + E/W;
   decimale e DMS mescolati si rifiutano (il formato lo vieta).
3. **La scrittura**: niente più `//Start`/`//End` aggiunti (chi li ha li tiene; nell'albero nessuno), e una
   riga toccata esce **nella forma dei suoi punti** — puntata, compatta o decimale (`IO/FormaDelPunto.cs`); se
   le sue righe non ne dicono nessuna, in quella prevalente del file.

Prova: 267 test (31 nuovi) su net8 e net10; strumento: 681 esatti, 7 578 righe opache (VBA ora si legge),
0 discordi.

🔴 **Scoperta della slice 2, che cambia il piano: A non sa SCRIVERE.** Lo strumento ha una misura nuova,
«tutto toccato»: segna come modificati tutti i record e li fa riscrivere. Un record toccato ma non cambiato
dovrebbe uscire com'era. Escono diverse **60 750 righe su 257 435 (23,6%), in 210 file su 681** — erano
67 025 in 330 file prima della regola della forma. Il round-trip di A è perfetto solo perché nessuno tocca
niente; appena un AOD modificasse un record:

| dove | che cosa si perde | esempio |
|---|---|---|
| `.str` 39 944 righe | spazi dei campi vuoti | `LIZZ;BULL;LEVIS; ; ;3;` → `…;;;3;` |
| `.mva`, `.lartcc` | i terminatori `T;DUMMY` | la riga sparisce |
| `.hartcc` | la frazione a due cifre | `N043.49.49.00` → `…49.000` |
| `.tfl`, `.geo` | 🔴 **righe commentate che tornano attive** | `//GARDA` → un vertice; `//N037…;PIER;` → una linea sulla mappa |
| `.sid` | campi vuoti in coda | `LIRF;07;OST1E;;;;;1;` → `LIRF;07;OST1E;;;` |
| `.vor`, `.fix`, `.gts`, `.ndb` | 🔴 **campi in coda** | il canale TACAN `54Y`; il rimando all'attesa `HLD-ABBOZ`; il tipo di gate `M` |
| `.rw`, `.ap` | gli zeri davanti | `095` → `95` |
| `.txi` | precisione dei decimali | `45.49980413` → `45.49980417` |
| `.frq` | righe lunghe | (troncate) |

Nessuno di questi tocca il sito o l'app di oggi (nessuno scrive ancora con il motore), ma F3 senza questo
lavoro produrrebbe PR con migliaia di righe cambiate a ogni modifica. Da qui la decisione del §9.5, la **slice 3** nuova (le slice dopo sono slittate di uno) e la terza
condizione della definition of done.

**Slice 3 — riga come campi** (§9.5; commit = il successivo a `d58be224`). `IO/Basi.cs` (`FissaLeBasi`: la
base di ogni record, all'apertura), `IO/FusioneDelRecord.cs` (la fusione a tre), e l'orchestratore che
**rifiuta** un record toccato senza base. La fusione:
1. allinea le righe grezze alla base con un'uguaglianza di significato (spazi, zeri davanti, forma della
   coordinata; i campi in coda della grezza e i campi vuoti della base valgono come «sconosciuti al modello»);
2. nei buchi che restano, un secondo passo aggancia un record **disattivato** alla sua riga commentata
   (`//LIBB;00;00;…`) e una riga con **gli stessi punti** alla sua base anche se un campo dedotto dallo
   scrittore differisce (`L;LIMM;…` contro `L;50;…` dei `.mva` di rotta). Al primo passo sarebbe pericoloso: la
   versione vecchia tenuta commentata sopra la nuova prenderebbe la modifica;
3. confronta base e nuove alla lettera e riscrive, delle righe cambiate, **solo i campi cambiati**, nella forma
   dei punti del record; righe aggiunte nella stessa forma; righe che lo scrittore non produce (commenti,
   `T;DUMMY`, commenti in coda come `; //3500 SE`) restano dove sono.

Prova sull'albero intero: **tutto toccato 0 righe** (erano 60 750); **una modifica per record: 99 707 record
spostati, 99 707 righe cambiate, 0 file fuori misura, 0 righe con più di un campo cambiato**. Controprova:
rimettendo la riscrittura dell'intera riga le righe con più di un campo cambiato tornano 6 484. La strada
fino allo zero è passata per cinque casi veri, ognuno oggi un test (`FusioneDelRecordTests`): il `;` finale
contato come campo (`54Y` perso), la coppia decimale non riconosciuta, il record disattivato di `libb.ap` e
`licz.geo`, l'etichetta dedotta dei `.mva` di rotta, il commento in coda di `lipe.mva`. In CI la stessa misura
gira su 15 campioni veri (`UnaModificaPerRecordTests`). Test: 303 su net8 e net10.

**Slice 4 — il punto per nome** (commit = il successivo a `449b52ea`). `Shared/Punto.cs`: il tipo unico del
punto, **coordinate o nome**. Il nome sta in due campi e il punto li tiene tutti e due (`ALPHA SOUTH;ALPHA
SUOTH`: Aurora prende la latitudine dal primo e la longitudine dal secondo — riscriverne uno solo sposterebbe il
punto; il refuso lo dirà il validatore). La regola nome/coordinata è quella del §7: un campo che comincia con un
emisfero e una cifra, o con una cifra o un segno, è una coordinata, e se non si legge resta una coordinata
**sbagliata**, non diventa un punto chiamato `N047.44.75.000`. `TryRisolvi` lo risolve in un catalogo (F3).
Dove entra:
- **`.tfl`**: `TflSector.Vertices` è una lista di `Punto`. 🔴 In A il primo vertice per nome **chiudeva il
  settore**: i vertici dopo finivano in righe grezze, fuori dal record (`libb_es_ctr.tfl`, 72 righe). Il nome
  vale solo su una riga di meno di 5 campi: letti come nomi, i primi due campi di un'intestazione
  (`LIBB_ES_CTR LIBB_EU_CTR;CTR;…`) passerebbero per un vertice (controprova: tolta la guardia, rossi i test delle intestazioni di `.tfl` e fic).
- **`.mva`**: `MvaVertex.Position` e `LabelAnchors` sono `Punto` (`T;LIRR;UTENO;UTENO;LIRR;`, 18 righe).
- **`.sid`**: `SidParser` non è più un lettore a riga singola. Una SID può avere un **tracciato** sotto
  l'intestazione (`SidProcedure.Track`, punti con etichetta facoltativa, `N…;E…;GOLF;`): sono le partenze a
  vista di `lied.sid` (`QUIRRA DEP34`, `FRASCA DEP34`…), 84 righe. Una riga vuota **fra due punti** del
  tracciato lo spezza e non chiude la SID (`NORTH DEP16`: `PuntoDelTracciato.NuovoTratto`); prima di
  un'intestazione separa le SID come prima. I commenti dopo una SID di una riga restano in testa alla successiva,
  come in A.
- **`.str`**: i nomi li capiva già (`ProcedureWaypoint`, `HoldingFixPoint`) e il modello resta quello di A. Ma
  una coordinata che non si legge diventava **in silenzio** un fix chiamato come lei: resta un fix, ora con
  l'avviso «Unparseable STR point». Sull'albero: zero.

Prova sull'albero intero: **righe opache 7 578 → 7 162** (−416: 314 `.tfl`, 84 `.sid`, 18 `.mva`). La carta
diceva −417: la riga che manca è **`lovv.tfl:48` `E017.04.60.000`**, secondi a 60 — un errore vero, che stava
nascosto fra le 315 «per nome» e ora è l'unica opaca dei `.tfl` (per il validatore, slice 8). Round-trip 681/681,
tutto toccato 0, **una modifica per record 100 098 → 100 098 righe** (erano 99 707: i settori non più spezzati e
le SID col tracciato entrano nella misura; lo spostamento prende il primo punto **per coordinate**), 0 discordi.
Campioni nuovi: `DYNAMIC_SEC/libb_es_ctr.tfl`, `lied.sid`. Test: 334 su net8 e net10 (31 nuovi: `PuntoTests`,
`PuntiPerNomeTests`, due misure in `UnaModificaPerRecordTests`); controprova: spenti i nomi nel lettore `.tfl`,
3 rossi.

**Slice 5 — le forme opache** (commit = il successivo a `3d331045`). Cinque forme legittime, ognuna presa dalla
specifica di B (§4.3, §5.1, §6.1, §6.2, §6.4) e ricontata sull'albero:
- **`.fix`**: `DisplayType` e il campo 5 sono **facoltativi** (`int?`, `string?`). 2 032 fix a 4 campi (i
  nascosti, `BC404;…;3;`) e 9 a 3 (`POE1;lat;lon;`, `VFR_NASCOSTI.fix`). Ribalta §22.3 di A.
- **`.artcc`**: il record è un'unione, `ElementoArtcc` = `LabelPoint` (`L;`) **o** `StaticBoundaryGroup`
  (`T;`, lo stesso modello e lo stesso scrittore dei `.hartcc`). Erano 5 041 righe: tutti i bordi di `FRA.artcc`
  e `FRA-gates.artcc`. Ribalta §19.4 di A. 🔴 Trovato strada facendo, **anche nei `.hartcc`** (latente): una
  serie di `T;` che **comincia con DUMMY** (`FRA.artcc`: `T;DUMMY;N038.34…` poi `T;LIMITROFI;…`) prendeva
  «DUMMY» come nome del gruppo, e lo scrittore riscriveva ogni vertice come una riga DUMMY. L'ha visto «una
  modifica per record» (121 record → 1 901 righe cambiate); ora il nome è quello del primo vertice vero.
- **`.frq`**: il profilo è facoltativo (`string?`): 35 posizioni si fermano all'elenco dei trasferimenti
  (`LIZZ_AEW_CTR;136.400;LIMM LIRR …`), e si riscrivono senza campi in più.
- **`.geo`**: il campo colore può essere **vuoto** (10 segmenti, `…;E013.18.35.124;;`).
- **`.pol`**: un commento **subito dopo l'intestazione** è il nome del poligono (`STATIC;TAXIWAY;1;TAXIWAY;` poi
  `//BR_twy_B`) e resta nel blocco. In A chiudeva il poligono a **zero vertici** e i vertici finivano in righe
  grezze: 30 poligoni invisibili al modello (più 2 davvero vuoti, sotto). Il §1 li contava come «righe vuote»:
  erano tutti commenti.

Prova sull'albero intero: **righe opache 7 162 → 7**, tutte **errori veri del sector** (da passare agli AOD con
`R47` di F1; il validatore della slice 8 li riprenderà):

| file:riga | riga | errore |
|---|---|---|
| `NAVAIDS/itvor.vor:81` | `GRO;;N042.45.37.200;…` | frequenza vuota |
| `NAVAIDS/itvor.vor:109` | `KPT;108.40;N047.44.75.000;E010.20.99.000;…` | minuti 75 e 99 |
| `NAVAIDS/APT.fix:294` | `MG763;N044.03.11.145;E008-11.31.443;3;` | trattino al posto del punto |
| `NAVAIDS/MIL.fix:96` | `PL-BRAVO;N044.54.40.500;E010.34.072.00;3;` | secondi 72 |
| `DYNAMIC_SEC/lovv.tfl:48` | `N047.42.27.000;E017.04.60.000;` | secondi 60 |
| `GND_LAYOUT/eo_ad_gnd.pol:72` | `STATIC;TAXIWAY;1;TAXIWAY;` poi un'altra intestazione | poligono senza vertici |
| `GND_LAYOUT/ml_ad_gnd.pol:1223` | idem | poligono senza vertici |

Round-trip 681/681, tutto toccato 0, **una modifica per record 102 281 → 102 281**, 0 discordi. Lo strumento ora
elenca tutte le opache quando sono poche (≤ 50). Campioni nuovi: `NAVAIDS/APT.fix`, `NAVAIDS/VFR_NASCOSTI.fix`,
`GND_LAYOUT/br_ad_gnd.pol`, `GEO/liap.geo`. Test: 352 su net8 e net10 (`FormeOpacheTests`, cinque misure in
`UnaModificaPerRecordTests`, §19.4 e §22.3 ribaltati); controprove: tolta la regola del commento nel `.pol` e il
nome dal primo vertice, 4 rossi.

**Slice 6 — `.vrt`, `.hold`, aree P/R/D** (commit = il successivo a `31f0a97e`). I 20 file senza lettore del §1:
- **`.hold`** → `Attesa` (`HoldParser`/`HoldSaver`, a riga singola): nome, punto (`Punto`), descrizione. La
  descrizione resta il dato scritto; fix, rotta di avvicinamento, verso e quota ne sono una **lettura** (null se
  la forma non è quella solita). Sul master sono **68 attese** (le 71 righe del §1 contano 3 righe vuote), tutte
  nella forma `ABBOZ/225R-9000`; l'ultima, `HLD-OZE`, senza `;` finale.
- **`.vrt`** → `RottaVfr` (`VrtParser`/`VrtSaver`): le righe **consecutive con lo stesso numero**, un punto per
  riga. 🔴 La riga vuota **non** basta a separare le rotte: in 15 file su 16 il numero cambia almeno una volta
  senza (`libv.vrt`, `limp.vrt`…). Un commento chiude la rotta (`lirh.vrt` ne ha uno in coda: «interruzione
  obbligata per evitare collegamento con rotte RL»). `libv.vrt` e `licz.vrt` hanno due campi in più (`…;;1;`)
  che nessuna specifica spiega: restano nella riga, sconosciuti al modello.
- **`.restrict`/`.prohibit`/`.danger`** → il lettore `.geo`, col **nome dell'area** nel sesto campo
  (`Line.Nome`, `…;RESTRICT;R4;`); un `.geo` non l'ha e lo scrittore non lo aggiunge.
- 🔴 **Correzione del §7** («nessun nome comincia con una cifra»): falso per i VRP dei `.vfi`, che i `.vrt`
  citano — `2NM NORTH LUCERA`, `5.5NM EAST LAMPEDUSA`, 20 nomi. Letti come coordinate sbagliate, erano **7 righe
  opache**. Ora la regola di `Punto` è: cifra o segno in testa **e nessuna lettera** = coordinata decimale;
  emisfero + cifra = coordinata come prima (`N047.44.75.000` resta un errore, non un nome).

Prova sull'albero intero: **round-trip 701/701** (681 + i 20 nuovi; restano 48 file senza lettore, tutti testo e
configurazione del §1), tutto toccato 0 su 271 388 righe, **una modifica per record 115 381 → 115 381**, 0
discordi. Righe opache **7 → 93**: le 7 di prima, più **86 errori veri** nelle aree P/R/D — lo **spazio al posto
del `;`** fra latitudine e longitudine (`N038.55.55.424 E016.36.08.523;…`), che fa del segmento una riga illeggibile:

| area | file | segmenti rotti | prima riga |
|---|---|---:|---|
| P154 | `GEO/italy.prohibit` | 28 su 32 | 3132 |
| P219 | `GEO/italy.prohibit` | 28 su 32 | 6249 |
| R107A | `GEO/italy.restrict` | 3 su 3 | 1139 |
| R107B | `GEO/italy.restrict` | 9 su 9 | 1143 |
| R107C | `GEO/italy.restrict` | 9 su 9 | 1153 |
| R107D | `GEO/italy.restrict` | 9 su 9 | 1163 |

R107A-D non hanno **nemmeno un** segmento leggibile: è la spiegazione probabile del confronto di F0-bis (carta
madre §11), che le dava **assenti** dal sector. Da passare agli AOD con le 7 della slice 5 e `R47`. (Che Aurora le
scarti come fa il motore non è provato: lo si vede aprendo il sector.)

Lo strumento elenca le opache fino a 100 (erano 50). Campioni nuovi: `HOLDENR.hold`, `liba.vrt`, `libv.vrt`, `lirh.vrt`, `GEO/italy.danger`. Test: 367 su net8 e
net10 (`LettoriNuoviTests`, tre misure in `UnaModificaPerRecordTests`, due nomi e una coordinata in
`PuntoTests`); controprova: rimessa la regola di prima in `Punto`, 4 rossi.

**Slice 7 — i tag `//@`** (commit = il successivo a `d4ee3048`). `IO/Metadati.cs` (lettura e scrittura in un posto,
il catalogo delle chiavi accanto) e `Models/Parsing/MetadatiDelFile.cs` (l'esito). Non è un lettore in più: i tag
sono commenti, e i lettori li lasciano già fra le righe grezze o nei commenti di testa dei record; `Metadati.Leggi`
li percorre in ordine di file. La forma, per un record:

```
//@source=AIRAC2610                       del file, nelle prime righe (catalogo: source)
//@BANA6W fix=BANAV initialclimb=5000     la dichiarazione: il NOME del record, poi le chiavi (catalogo: fix, initialclimb)
//@START
LIRF;25;BANA6W;…                          il record: la riga del .sid, o intestazione e corpo del .str
//@END BANA6W
```

Scelte strada facendo, dentro quel che il committente ha deciso il 21 settembre:
- **Le chiavi stanno sulla riga della dichiarazione** che apre il blocco: la carta le mostrava su due righe
  (`//@BANA6W` per il blocco, `//@BANA6W fix=…` sopra la label), che in un blocco di una riga sola sarebbero state
  due righe con lo stesso nome una sopra l'altra. In lettura basta anche la dichiarazione subito sopra il record,
  senza START/END (`Delimitato` = falso); la scrittura mette sempre il blocco intero.
- **Il nome arriva fino alla prima parola con `=`**: i nomi dei MAPS hanno spazi (`//@LIRF CTR fix=X`).
- 🔴 **Un `//@` chiude sempre il record aperto** (`StrParser`, e il tracciato in `SidParser`). Un record di `.str` va
  fino all'intestazione dopo, righe vuote e commenti compresi: senza questa regola `//@END` e la dichiarazione del
  record seguente finivano DENTRO il record di prima. Sul master nessuna riga `//@`: il round-trip non cambia.
- Nella scrittura `//@END` va subito dopo l'ultima riga di dati; le righe vuote in coda di un `.str` escono dal record
  e restano dopo la fine del blocco.
- **Ciò che non torna** è un problema con la riga: nome che non combacia (la guardia: le chiavi **non** si attaccano
  al record sotto), dichiarazione orfana (riga vuota, altra dichiarazione o fine file prima del record), START
  senza dichiarazione o senza END, END senza START o con un altro nome, riga illeggibile — errori; chiave fuori
  catalogo o chiave di file fuori posto — avvisi (si leggono). **Sopra un file con errori `Scrivi` rifiuta**: prima
  si sistema. Una chiave fuori catalogo, o un valore vuoto o con spazi, non si scrive.
- La scrittura restituisce un file **nuovo** (pezzi nuovi, stessa base): quello passato non cambia.

Prova sull'albero intero, misura nuova dello strumento («TAG SU TUTTO»: ogni record di ogni `.sid`/`.str` riceve il
suo blocco con una chiave e il file il suo `//@source`; si salva, si rilegge, si tolgono le righe `//@`): **2 808
record ritrovati su 2 808**, tutti delimitati e col loro nome, **0 problemi**, **149 file su 149 identici byte per
byte** tolte le righe `//@`. Sull'albero com'è: 0 tag, 0 problemi. Le altre misure invariate (701/701, opache 93,
tutto toccato 0, una modifica per record 115 381, 0 discordi). Controprova: tolta la regola «un `//@` chiude il
record» dallo `StrParser`, 1 304 su 2 808 e 90 file guasti. Test: 390 su net8 e net10 (`MetadatiTests`); controprove
sulle due regole dei lettori: 2 e 1 rossi.

## §9 — Decise col committente prima della slice 0 (✅ tutte e quattro, 22 settembre)

1. **Progetto nuovo `Vipi.Sectorfile`** (proposta) invece di allargare `Vipi.Infrastructure/Sectorfile`.
2. **Campioni veri in repo + albero intero in locale** (proposta) invece di scaricare il sector in CI.
3. **Porto di A adattato**, non riscritto (proposta): le 5 500 righe e i 310 test vengono con la loro storia,
   il primo commit dice da dove.
4. vIPI **non cambia** in F2 (proposta): la lettura dei `//@` da parte dell'import arriva con F7.
5. ✅ **Riga come campi** (committente, 22 settembre, dopo la scoperta della slice 2) — **deroga al punto 3**
   per la sola scrittura. Gli scrittori di A ricostruiscono ogni riga dal modello e perdono ciò che il modello
   non ha; completarli formato per formato avrebbe riperso ogni campo sconosciuto futuro. Invece:
   - all'**apertura** ogni record fotografa la sua **base**: le righe che lo scrittore produce dal record ancora
     intatto;
   - al **salvataggio** si confronta la base con le righe del record modificato: dove coincidono restano i
     **byte originali**; dove una riga cambia entra solo il **campo** cambiato (nella forma del file); i campi
     che il modello non conosce, in coda o no, restano; le righe che il modello non vede (commenti, `T;DUMMY`,
     righe ignote) restano al loro posto;
   - un record segnato come toccato ma non cambiato esce **identico per costruzione**. La misura dura diventa
     «**una modifica per record**»: si sposta di un millesimo di secondo il primo punto di ogni record e si
     contano le righe cambiate — dev'essere **una** per record.

   I lettori e il modello di A restano; cambia il modo di riscrivere. È la slice 3.
