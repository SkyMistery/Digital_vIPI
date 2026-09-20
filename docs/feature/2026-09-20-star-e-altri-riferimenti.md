# STAR dal sectorfile, e che altro può seguire la sorgente (20 settembre 2026)

> Stato: 🟡 **slice 1→4 fatte** — parser misurato sui file veri, archivio (`AirportProcedures` + `Kind`,
> migrazione provata su una copia di produzione), import dal sectorfile, editor, e **la sezione «STAR» nel
> documento**, derivata e congelabile come le SID (verificata a schermo su LIBD). Resta il riferimento nel testo.

**La richiesta del committente (20 settembre 2026):** ora che il riferimento alle SID nel testo funziona
(§A73, [carta del 18 settembre](2026-09-18-riferimenti-sid-nel-testo.md)), estenderlo alle **STAR** — «vedi se
riesci a montare un parser che tiri fuori le STAR, quelle che hanno una pista o più piste, **non le MAPS**» —
e poi alle **piste** e alle **frequenze**. Più: che altro potrebbe adottare lo stesso meccanismo.

## 1. Misurato sui file veri (90 `.str` della divisione, copia di lavoro del 20 settembre)

Il file `<icao>.str` ha **lo stesso formato** del `.sid`: `ICAO;pista[:pista…];CODICE;labelLat;labelLon;tipo;transition;RNAV;`.
Non contiene però solo STAR — un quarto delle sue etichette sono voci del **menu mappe** (`STATO_SECTORFILE_ITALIANO.md` §6.1).

| Misura | Valore |
|---|---|
| File `.str` | 90 |
| **Righe STAR estratte** | **645** (865 contando le piste separate, come le SID) |
| Aeroporti con almeno una STAR | **54** |
| RNAV / convenzionali | 522 / 343 (per pista) |
| Punto da rivedere a mano | 136 su 865 = **15,7 %** (si abbatte con gli alias: `ELKA`, `PIMO`, `NEVN`, `MARE`, `DOGU`, `LUMA`, `KAPP`, `PERO`, `LOME` coprono la metà) |
| Scartato perché non è una STAR | 339 voci `MAPS` + attese (tipo 2) + IAP (tipo 3) + FAP (tipo 4) + CTR/ATZ (tipi 1 e 5) |

Gli scali più carichi: LIRF 43, LIBD 38, LIPO 23, LICJ 22, LICA 21, LICZ 21.

## 2. La regola: due filtri, nessuno dei due sul nome

Una riga del `.str` è una STAR quando:

1. **ha almeno una pista vera** nel campo 2. I gettoni si separano su `:` e si tengono solo quelli di forma
   pista (due cifre più `L`/`R`/`C`). Questo butta le voci di menu (`MAPS`, `MAPS:07`) e le piste finte
   dell'ICAO militare `LIZZ` (`BULL`, `AAR`, `AEW`). ⚠️ `MAPS` può stare **dentro** l'elenco —
   `LIPA;05:MAPS;ROSK1E;…` è una STAR vera che è anche voce di menu: si butta il gettone, non la riga.
2. **ha il tipo vuoto** (campo 6). È il campo che dice che cosa disegna la riga: `1` CTR, `2` attesa
   (`HLD-ELVAD`), `3` IAP (`RNP25`, `VOR22L`), `4` FAP, `5` ATZ. **Tutte queste convivono con una pista vera
   nel campo 2**: senza questo secondo filtro entrerebbero 485 righe che non sono procedure d'arrivo. Si
   tollera `0` perché è il «niente» che i `.sid` scrivono nella stessa colonna.

Il **nome non filtra niente**: i militari scrivono `TANGO REC`, `HITACX14L(ATC)`, `VOR-TAG` — una ventina di
righe su 645 — e sono STAR quanto `ELKA3A`. Il punto irrisolto si **segnala** (`NeedsFixReview`), come per le SID.

Il resto — completamento del punto troncato (`GILI3A` → `GILIO`) dal catalogo navaid, alias autoritativi,
designatore, `StableKey`, espansione per pista — è **identico alle SID**, e infatti è lo stesso codice.

## 3. La decisione presa: una colonna, e la tabella cambia nome

Il gate «modello gemello» di [FEATURE-PROCESS](../FEATURE-PROCESS.md) dice: *mai affiancare un secondo modello
a uno esistente per la stessa cosa*. SID e STAR hanno gli stessi campi (scalo, pista, punto, nome, transition,
tipo, revisione, priorità, nascosta, ciclo AIRAC, correzioni a mano) e la stessa vita (import, merge per
`StableKey`, pubblicazione differita al ciclo, editor, tabella congelata).

✅ **Decisione del committente, 20 settembre 2026**: una tabella sola, e col nome giusto — `AirportSid`
diventa **`AirportProcedure`** (tabella `AirportProcedures`) con la colonna **`Kind`** (`Sid`/`Star`).
Rinominare toglie di mezzo l'obiezione che teneva in piedi il catalogo separato nel
[piano import trasferimenti](piano-import-trasferimenti.md) §B2 — «un flag su un'entità che si chiama `Sid`
è un nome che mente»: l'entità non si chiama più così.

Che cosa è costato davvero (censimento **intero**, non un `grep` troncato da `head`):

| | |
|---|---|
| Il tipo `AirportSid` | **12** occorrenze, 5 file |
| Il `DbSet` e la navigazione | **11** occorrenze, 2 file (+ il componente UI `AirportSids`, che NON si tocca: rende la tabella SID) |
| Query da filtrare `Kind = Sid` | **9**, tutte in `EfAirportRepository` — il resto del codice passa di lì |
| Migrazioni | **2** (SQLite + MySQL), col corpo **scritto a mano** |
| Documenti da correggere | la spec del modello dati, il piano import trasferimenti §B2, questa carta |

🔴 **La trappola, e costava l'archivio.** Lo scaffolding di `dotnet ef migrations add` ha proposto, in
**entrambi** i provider, `DropTable("AirportSids")` + `CreateTable("AirportProcedures")`: applicata così, la
migrazione avrebbe **cancellato le 1469 righe SID di produzione** — priorità, pubblicazioni forzate,
correzioni del punto — e il database sarebbe risultato «aggiornato». I due corpi sono riscritti a mano con
`RenameTable` + `AddColumn` (e su MySQL anche il rename di indice e chiave esterna: un vincolo che cita una
tabella che non esiste più fa fallire la prima migrazione futura che prova a lasciarlo cadere).

🔴 **Il percorso Postgres non ha migrazioni**: lì lo schema lo allinea `PostgresSchemaReconciler`, che una
tabella rinominata la vedrebbe come una tabella **nuova e vuota**, lasciando i dati nella vecchia. Ci si è
aggiunto un passo di rinomina idempotente (`TabelleRinominate`), da tenere per sempre: è l'unica memoria del
cambio su quel provider.

Il DTO di sorgente era già unificato nella slice 1: `SourceProcedure` (era `SourceSid`) con `ProcedureKind`.

### Slice previste (dopo la decisione)

1. ✅ **parser** — `AuroraSectorfileParser.ParseStars` + `SourceProcedure.Kind` + 8 test.
2. ✅ **2a — l'archivio** — `AirportProcedure` + `Kind` + le due migrazioni + i filtri `Kind = Sid` sulle
   letture esistenti.
   ✅ **2b — l'import** — `IProcedureProvider.GetAsync(icao, kind)` sceglie il file (`.sid`/`.str`),
   `ReplaceImportedProceduresAsync` prende il verso e cancella **solo quello**, e `ProcedureImporter` fa un giro
   solo per i due versi, con un ciclo AIRAC solo (stesso sectorfile, stessa release). Una categoria d'import
   sola: `ImportCategory.Sids` copre le procedure, non si è aggiunto un secondo interruttore.
   🔴 **Zero righe non è «non ce n'è più»**: 36 dei 90 `.str` non portano nessuna STAR e la rete può cadere —
   un verso senza righe non tocca l'archivio. È il test che lo tiene fermo.
3. ✅ **editor** — la tabella STAR sotto quella SID, **lo stesso componente montato due volte**
   (`<AirportSidsEditor Kind="ProcedureKind.Star">`): stessi gesti — priorità, nascondi, correggi il punto,
   crea alias, pubblica subito — perché è la stessa riga con un verso diverso. Quel che cambia col verso si
   conta sulle dita: i titoli e le frasi che nominano la famiglia (chiavi `Ape_Star*`), l'etichetta della
   colonna del nome, la colonna **Initial climb** che su un arrivo non vuol dire niente e quindi non c'è, il
   tasto di reimport che sta **solo** sulle partenze (il giro porta i due versi insieme), e gli id dei due
   elenchi a discesa, che con due montaggi nella stessa pagina non possono coincidere.
   ⚠️ La tabella sta **dentro la sezione SID** del documento: una sezione «STAR» sua è la slice 4, insieme
   alla resa pubblica. Fino ad allora l'indice dice «SID» e gli arrivi stanno lì sotto.
   ⚠️ `SaveSidsAsync` e il caricamento della scheda prendono anch'essi il verso: salvare le manuali degli
   arrivi non deve poter cancellare quelle delle partenze.
4. ✅ **sezione pubblica** — sezione di catalogo **`stars`**, sua e non una seconda tabella dentro le SID:
   `SectionKind.Derived`, subito dopo `sids` nei profili civile e militare. La derivazione prende il verso
   (`DeriveAsync(icao, kind, atCycle)`), `AirportDerived` porta `Stars` accanto a `Sids`, e la cattura di
   release congela `"stars"` al **ciclo della release** come fa per `"sids"`. Il visualizzatore è lo stesso
   componente con `Kind`: colonna **STAR**, niente *Initial climb*, e la chip marca la pista in **arrivo**
   (🛬) invece che in partenza.
   ⚠️ **Nasce Live** come le SID (`BornLive`): sono la stessa tabella, e due nascite diverse sarebbero due
   comportamenti da spiegare.
   ⚠️ **Ai documenti già scritti la semina la manutenzione d'avvio** (`AddMissingCatalogSectionsAsync`), che
   la mette al posto giusto e rinumera il gruppo. Misurato sulla copia del `vipi.db`: 17 sezioni `stars`
   create al primo avvio.
   🔴 **Nel documento PUBBLICATO la sezione compare solo dalla prossima release**: quel che è pubblico è lo
   snapshot di allora, e uno snapshot non si riscrive. Nella bozza c'è subito.
5. **riferimento nel testo** — `[[STAR LIRF ELKA3A]]`, sulle stesse cinque slice già pagate per le SID:
   il codice di `RiferimentiSid` è **già generico sulla radice del nome**, cambia il gettone e la tabella da cui
   si leggono i nomi.

## 4. Che altro può seguire la sorgente

✅ **Scelte dal committente il 20 settembre 2026, tutte e quattro**: frequenze, piste, nominativi ATC, punti e
VOR — nell'ordine della tabella, che è quello del guadagno. Restano proposte non decise le quote di
transizione e le aree speciali.

Il meccanismo è sempre lo stesso: **nel testo si scrive un riferimento stabile, in pagina esce il dato di oggi**,
e l'editor avvisa quando un riferimento non si risolve più. Vale la pena solo dove il dato **cambia** e dove il
testo lo **ripete**.

| Proposta | Gettone | Che cosa esce | Perché conviene |
|---|---|---|---|
| **Frequenze** | `[[FREQ LIRF_TWR]]` | `118.700`, o il nominativo + la frequenza | La più forte: le frequenze si citano in prosa dappertutto («contatta la TWR 118.700») e cambiano senza che nessuno riapra i documenti. La sorgente c'è già (`Sector.DefaultFrequency`, catalogo importato). |
| **Piste** | `[[RWY LIRF 16L]]` | l'ident di oggi | Le piste si rinominano per la deriva magnetica (16L → 17L, già successo in Italia). Il riferimento non «aggiorna» il testo da solo con la stessa sicurezza delle SID — 16L e 17L non hanno una radice comune — ma **l'avviso in editor** («la pista 16L non esiste più a LIRF») vale da solo. |
| **Nominativi ATC** | `[[ATC LIRR_CTR]]` | il callsign + il nome del settore | I settori si rinominano e si dividono; i documenti li citano a mano. |
| **Punti e radioassistenze** | `[[FIX OST]]` | il nome, e su richiesta frequenza/canale (`OST 114.90`) | Il catalogo navaid porta già frequenza e canale; oggi si ricopiano a mano nelle tabelle. |
| **Quote di transizione** | `[[TA LIRF]]` / `[[TL LIRF]]` | l'altitudine/il livello di oggi | Un numero solo, ripetuto in più sezioni e in più documenti. |
| **Aree speciali** | `[[AREA LI R49]]` | il nome e i limiti | Cambiano per NOTAM/AIRAC; oggi si citano a mano. |

Fuori dal meccanismo, per scelta: il METAR e la pista in uso (sono **già** dinamici, non citati), il glossario
(ha la sua strada), i coordinamenti (vivono in tabelle proprie, non in prosa).

## 5. Verifica

- 4 test in `SezioneStarPubblicaTests` (colonna STAR, niente Initial climb, la chip marca la pista in
  ARRIVO, il riquadro «non ce ne sono» con le parole degli arrivi) e 2 in `AirportSidDerivationServiceTests`
  (la derivazione dà il verso chiesto; nascoste e in attesa si comportano come le partenze).
- 5 test in `EditorStarTests` (l'etichetta della colonna, la colonna Initial climb che non c'è, i titoli
  degli arrivi, il reimport solo sulle partenze, gli id degli elenchi diversi fra i due montaggi), 2 in più in
  `StarImportTests` (la scheda porta gli arrivi a parte; le manuali di un verso non toccano l'altro).
- 8 test in `AuroraStarParserTests`, 4 in `AuroraProcedureProviderTests` (quale file per quale verso, il file
  che non c'è, la sorgente non configurata), 6 in `StarImportTests` (i due versi non si toccano, la scheda SID
  non vede gli arrivi, il salvataggio delle manuali non li cancella, priorità e forzatura si riapplicano dentro
  il verso, un giro importa entrambi, un verso vuoto non cancella niente) (righe reali di `lirf.str`, `lipa.str`, `lizz.str`, `lipc.str`).
- Misura dal vivo sui 90 `.str` veri della copia di lavoro del sectorfile: 54 scali, 865 righe, 136 da
  rivedere, 522 RNAV — prova usa-e-getta, non committata, rifattibile in due minuti.
- `dotnet build Vipi.slnx -c Release --no-incremental` verde su entrambi i TFM; suite intera verde.
- **Sezione pubblica provata a schermo** (bozza di LIBD): «STAR» nell'indice fra «SID» e «General
  procedures», tabella con RWY · FIX · **STAR** · TRANSITION · TYPE · CAT. · WTC · CONDITION — senza *Initial
  climb* —, chip di pista 07/25, nessun id doppio, console pulita. ⚠️ Le 38 STAR appena importate sono in
  attesa del ciclo d'entrata **2610** dichiarato dalla sorgente: la sezione diceva «STAR non ancora inserite»
  finché non se ne sono forzate (`ForcePublished`), ed è il gate AIRAC che funziona, non un difetto.
- **Editor provato a schermo** (Edge+puppeteer sulla copia del `vipi.db` di sviluppo, LIBD, 20 settembre):
  «STARs imported from the sectorfile 38» con le colonne FIX · STAR · RWY · TRANS. · TYPE · CAT. · WTC ·
  CONDITION · PRIORITY · STATUS — **senza Initial climb**, che resta sulle SID —, i triangoli sui punti da
  confermare (BIRS, DOGU), «Manual STARs 0 · No manual STAR.», **nessun id doppio** in pagina, nessun errore
  in console. ⚠️ Il primo giro aveva scoperto una frase rimasta indietro: sotto gli arrivi c'era scritto «No
  manual SID.» — da lì le chiavi `Ape_Star*` per tutte le frasi che nominano la famiglia.
- **La migrazione gira anche su SQLite di sviluppo**: la copia del `vipi.db` è passata a `AirportProcedures`
  tenendo le sue 1469 righe SID.
- **Import provato dal vivo sulla sorgente vera** (GitHub raw, 20 settembre): LIRF 292 righe (206 SID + 86
  STAR, 14 da rivedere), LIBD 77 (39 + 38), LIPO 52 (22 + 30), ciclo d'entrata `2610` timbrato dal changelog
  della sorgente, punti risolti (`GILIO`, `BANAV`, `DOLON`); secondo giro idempotente, i conteggi non
  raddoppiano. Prova usa-e-getta, non committata.
- **Migrazione provata su una copia di produzione** (MariaDB locale, porta 3399, `vipi_1330` → `vipi_star`):
  dopo l'`update` la tabella `AirportProcedures` ha **1469 righe, tutte `Kind = 'Sid'`**, 1467 importate,
  indice e chiave esterna col nome nuovo; il dietrofront (`database update <migrazione precedente>`) riporta
  `AirportSids` con le stesse 1469 righe.
