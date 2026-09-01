# Piano — coerenza vIPI ↔ sectorfile 🟣

**Stato:** ✅ **ESEGUITA e FUSA IN `main`** il 1 settembre 2026 (`50028edc`; il ramo `coerenza-sectorfile`
è stato cancellato). Build Release pulita sui due TFM (0 avvisi), suite verde **E2E compresi**, e **provata
a schermo** (§9). 🔴 **Non è in produzione**: gira ancora 1.3.0, serve il pacchetto 1.3.1. · **Aggiornato:** 1 settembre 2026
**Metodo:** [FEATURE-PROCESS](../FEATURE-PROCESS.md) · **Perimetro:** [regole-perimetro-servizi](regole-perimetro-servizi.md) §P1
**Richiesta del committente (1 set 2026):** *«sì, molto utile, completerebbe la serie di strumenti a disposizione»*

> **In una riga.** Due sorgenti indipendenti descrivono le stesse cose — l'**API IVAO** (da cui vIPI prende
> posizioni, frequenze, aeroporti, TA e piste) e il **sectorfile Aurora** della divisione (da cui prende
> punti, SID e poligoni). Oggi **nessuno le confronta**, e quando divergono lo si scopre in frequenza.
> Questo lavoro le confronta e **dice soltanto**: non corregge niente, non importa niente, non decide chi
> ha ragione.

---

## §0 — Cosa c'è già (rilevato nel sorgente, 1 settembre 2026)

**Il motore c'è quasi tutto**, ed è la ragione per cui questo lavoro è piccolo.

| Pezzo | Dove | Stato |
|---|---|---|
| Accesso ai file raw del sectorfile | `Vipi.Infrastructure/Sectorfile/SectorfileRaw.cs` | ✅ 404 = «assente», non errore |
| Parser puro, senza I/O | `AuroraSectorfileParser` (navaid, SID, MVA, shape, TWR) | ✅ testato, `TryParseDms` incluso |
| Cache di processo dei file grandi | `SectorfileCache` (singleton, gate per fetta) | ✅ da estendere di una fetta |
| Configurazione della sorgente | `SectorfileOptions.RawBaseUrl` (vuota = spento) | ✅ |
| Rilievo diagnostico con area, gravità, dove-si-ripara, chiavi di traduzione | `ConsistencyFinding` + `ConsistencyArea` | ✅ |
| Pagina che li mostra, con chip per area | `DiagnosticaPage.razor` (itera `Enum.GetValues<ConsistencyArea>()`) | ✅ **nessuna modifica necessaria** |
| Traduzione dei rilievi | `ConsistencyNarrator` (`Diag_Area_{area}`) | ✅ due chiavi resx da aggiungere |
| Guasto di una sonda che diventa un rilievo invece di travolgere il report | `ConsistencyReportService.Raccogli` | ✅ |

**Quel che manca è solo il confronto.** Del sectorfile oggi leggiamo `NAVAIDS/*`, `*.sid`, `DYNAMIC_SEC/*.tfl`,
`ENRMVA/*.mva`. **Non leggiamo** i tre file che descrivono le stesse cose che vIPI tiene dall'API IVAO:

| File | Righe | Che cosa dice | Il gemello in vIPI |
|---|---|---|---|
| `OTHER/itfreq.frq` | **201 posizioni** (83 TWR, 58 APP, 30 CTR, 21 GND, 5 DEL, 4 FSS) | callsign + frequenza | `AccSector.Frequency`, `AirportSector.Frequency` |
| `OTHER/itap.ap` | **129 aeroporti** | ICAO, elevazione, **TA**, coordinate, nome | `Airport.ElevationFt`, `TransitionAltitudeFt`, `Latitude`/`Longitude`, `Name` |
| `OTHER/itrw.rw` | **241 righe**, di cui **96 pseudo-piste** `MAPS` | ident, ident opposto, QFU, lunghezze, soglie | `AirportRunway.Ident`, `Bearing`, `LengthM`, `ThresholdLat`/`Lon` |

I numeri vengono da [`STATO_SECTORFILE_ITALIANO.md`](../../../STATO_SECTORFILE_ITALIANO.md), misurati sul
contenuto versionato del repository `ivao-italy/it-aurora-sector`.

## §0-bis — ✅ La misura, ESEGUITA il 1 settembre 2026

> *«Prima di dire "non serve, misuriamo": misura davvero.»* — FEATURE-PROCESS

Tre file scaricati veri, confrontati col `vipi.db` di sviluppo. **Esito: ~38 rilievi**, dentro la banda in
cui questo lavoro ha senso. Ma la misura ha **cambiato il disegno in quattro punti**, e ognuno di essi
sarebbe diventato una pagina di rumore:

| Scoperta | Conseguenza sul disegno |
|---|---|
| `itrw.rw` **non contiene le lunghezze di pista**: i campi 4 e 5 sono le **elevazioni delle due soglie** | il confronto sulle lunghezze **non esiste** — era scritto in questa carta e non è eseguibile |
| Il **QFU non è confrontabile**: a 1° escono **115** divergenze, a 2° 55, a 3° 10, a 5° **zero**. Il delta medio è +1,63° e togliere la declinazione lo **peggiora** (91 soglie oltre 1°) | il controllo sul QFU **si toglie**. Le due sorgenti calcolano la rotta pista con riferimenti diversi: un controllo lì è un generatore di falsi, a qualunque soglia |
| I cataloghi vIPI contengono i **confinanti esteri** (DAAA, LDZO, LATI…): 142 callsign «solo in vIPI» | si confrontano **solo i callsign `LI*`**. `IsManual` da solo non basta: erano 5 righe su 142 |
| Gli **ATIS** in vIPI sono posizioni (25 callsign `*_ATIS`), nel sectorfile sono file `.atis` a parte | i callsign `*_ATIS` escono dal confronto |
| Gli **ident pista** hanno lo zero iniziale da una parte sola (`09` vs `9`) | si normalizza prima di confrontare, o 40 piste divergono per una cifra |
| Fra gli «aeroporti» di vIPI ci sono i **codici ACC** (`LIRR`, `LIMM`, `LIBB`) | esclusi: non sono scali |

### I numeri, dopo la taratura

| Famiglia | Rilievi | Note |
|---|---|---|
| **Frequenza divergente** | **6** | tolleranza 5 kHz (sotto è solo il nome del canale 8.33). Due sono grosse: `LIRF_PS1_APP` 136.100 vs 131.100 e `LIRM_APP` 132.255 vs 135.255 — **5 MHz e 3 MHz** |
| Posizione solo nel sectorfile | 2 | `LIDA_I_TWR`, `LIPR_GND` |
| Posizione solo in vIPI | 6 | `LIRR_NE1_CTR`, `LIPP_PLN_CTR`, `LIRR_PLN_FSS`, `LILA_I_TWR`, `LIPN_I_TWR`, `LIMF_WW0_APP` — chi si connette lì in Aurora non ha nulla |
| **TA divergente** | **3** | `LICD` 5000/4000, `LIMF` 6000/7000, `LIMZ` 6000/7000 |
| Elevazione aeroporto | **0** | il controllo resta, ed è muto: è una guardia, non una fonte di lavoro |
| Coordinate aeroporto | **0** | idem |
| Aeroporto solo in vIPI | 4 | `LIEF`, `LIEP`, `LIQV`, `LISF` (tolti i tre codici ACC) |
| **Ident pista divergente** | **~12 aeroporti** | e sono i rilievi migliori del lotto: `LIRP` 3L/3R/21L/21R contro 4L/4R/22L/22R, `LIED` 16/34 contro 17/35, `LICG` 2/7/20/25 contro 3/8/21/26, `LIPR` 12/30 contro 13/31. **Rinumerazioni per deriva magnetica applicate da una parte sola** |
| Soglia oltre 50 m | 5 | `LIBF/15` 297 m, `LIEO/23` 444 m, `LIPK/12` 197 m, e **`LIAA` 4907 km**: una coordinata rotta, che nessuno avrebbe mai visto |

⚠️ **Il rilievo che da solo giustifica il lavoro**: un aeroporto dove il sectorfile dice pista **17** e il
documento dice pista **16** è un errore che si sente in frequenza. Sono dodici.

⚠️ Restano fuori dal conto le divergenze **fisiologiche**: le due sorgenti hanno cadenze diverse (IVAO in
continuo, il sectorfile per ciclo AIRAC), quindi una parte di ciò che si vede oggi sparisce da sola. Vedi §4.

Lo script della misura sta nello scratchpad di sessione (`misura.py`, `taratura.py`): **è buttabile**, il
codice vero è quello delle slice.

---

## §1 — Le quattro domande di pre-flight

**1. Modello — aggiungo un concetto o ne esiste già uno?** Ne esiste già uno: `ConsistencyFinding`. Questo
lavoro **non aggiunge nessuna entità, nessuna tabella, nessuna migrazione**. Aggiunge un valore in coda a
`ConsistencyArea` e un produttore di rilievi.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?** No. La pagina di diagnostica itera
`Enum.GetValues<ConsistencyArea>()` e il narratore compone `"Diag_Area_" + area`: **nessuno dei due switcha**,
quindi un'area nuova non apre nessun ramo nuovo. Costo di propagazione dell'enum: **due chiavi resx** (IT+EN).

**3. Ingressi + verifica.** Ingresso: la pagina `/services/vsop/admin/diagnostics`, che è già dove si va a
capire cosa non va, **e** una pagina propria sotto la vSOP raggiunta dall'hub (§2/D1). Verifica: §8 e §9.

**4. Propagazione — rimuovo o rinomino qualcosa?** No, è puramente additivo.

---

## §2 — Le decisioni

**D1 — Visibile, ma come scorciatoia: una pagina propria sotto la vSOP.** Decisione del committente del
1 settembre 2026 (*«lo voglio visibile»*), sulla forma prevista dalla regola **P5**: non una scheda-servizio
su `/services`, ma una **pagina sotto `/services/vsop/`** raggiunta da una scheda marcata `shortcut`, come
gli spazi aerei e i vSOP militari.

- **Pagina:** `/services/vsop/sectorfile`, cancello **`IsEditor`** in due sedi (markup e caricamento) —
  *nascondere e basta è un cancello che non c'è*.
- **Scheda:** nella sezione **staff** dell'hub, con `class="choice shortcut"` e una **guardia propria**.
- ⚠️ **Il livello è `Editor`, non `DivisionStaff`** (committente, 1 settembre 2026, dopo il primo giro): è un
  gradino **più su** delle altre due schede di quella sezione — spazi aerei e convertitore — e la ragione è
  che questi rilievi parlano del **contenuto dei documenti** (frequenze, TA, designatori di pista). Chi li
  legge deve poterci fare qualcosa: aprire l'editor, o scrivere all'AOD con l'autorità per farlo.
  Uno staffista di divisione qualsiasi vede le altre due schede e **non** questa.
- ⚠️ **Perché una pagina propria e non solo il chip nella diagnostica:** la diagnostica la apre **solo
  l'`Admin`**, e questa scende di un gradino perché la aprano anche i **chief d'ACC**. In più questi rilievi
  si leggono **per famiglia** (frequenze · aeroporti · piste), che una tabella generica non sa fare.
- **Una verità, due porte:** gli stessi rilievi restano nella diagnostica sotto il chip dell'area nuova,
  perché è lì che si guarda lo stato complessivo. Nessun secondo calcolo: entrambe leggono la **stessa
  fotografia** (§2/D6).

**D2 — Area propria: `ConsistencyArea.Sectorfile`, in coda all'enum.** Non `Dati` (non si ripara aprendo un
editor) e non `Sorgente` (che significa «il dato arriva così da IVAO e ci conviviamo»). Qui il destinatario è
**una terza persona** — l'IT-AOD, che il sectorfile lo scrive — ed è precisamente ciò che l'enum esiste per
dire: *«a chi legge, se deve aprire un editor, il pannello del server o il file di configurazione — e sono
tre persone diverse in tre momenti diversi»*.

**D3 — Severità sempre `Warning`, mai `Error`.** Un `Error` afferma che qualcosa è rotto **da noi**. Qui non
sappiamo chi ha ragione: la frase è «le due sorgenti dicono cose diverse», e la porta entrambe.

**D4 — `Where` è `null`.** Non c'è una pagina di questa applicazione dove si ripara: si ripara nel sectorfile,
che sta su GitHub. ⚠️ `null` è già una risposta prevista dal modello — *«un link che non porta da nessuna
parte è peggio di nessun link»*.

**D5 — 🔴 L'health check deve ignorare quest'area.** Oggi `VipiHealthCheck` fa `if (findings.Count > 0)
return Degraded`. Con questa famiglia, `/vsop/health` diventerebbe **Degraded per sempre**, e un monitor che
è sempre giallo è un monitor spento. Il conteggio dell'health check filtra `f.Area != ConsistencyArea.Sectorfile`.
⚠️ **Questa riga non è un dettaglio: è la condizione perché il lavoro non peggiori la sorveglianza del sito.**

**D6 — Non gira dentro la richiesta.** Il confronto fa **I/O di rete** (tre GET su raw.githubusercontent), e
`ConsistencyReportService.RunAsync` è letto anche da `/vsop/health`, che è **anonimo** e chiamato da un
monitor. Il modello da copiare è quello di `IStartupMaintenanceReport`: *«non è una sonda: è già successo.
Qui si legge soltanto»*. Quindi:

- un hosted service confronta ogni **24 h** (stessa cadenza di `SectorfileOptions.ImportHours`) e mette la
  fotografia in un **singleton in memoria**, col timbro di quando è stata presa;
- il report di consistenza **legge la fotografia**, non la produce (passa comunque per `Raccogli`, così un
  guasto resta un rilievo);
- ⚠️ **niente riga in `ImportState`, niente categoria di import nuova**: quello è il registro di ciò che
  **scrive**, e questo giro non scrive niente. Una riga lì lo farebbe comparire in Sorgenti come se
  importasse, che è la cosa che questo lavoro non fa. Costo del non-persistere: dopo un riavvio si
  riscaricano tre file (~200 KB). Accettabile.

**D7 — Non si importa nulla da questi tre file, e la policy di import non c'entra.** La sorgente autoritativa
di frequenze, TA e piste resta l'API IVAO. ⚠️ Corollario: il confronto gira **anche** se una categoria di
import è esclusa dalla policy — «escludere = congelare» vale per chi **scrive**, e qui non si scrive.

---

## §3 — Le tre famiglie di confronto, e le loro tolleranze

Il cuore è una **funzione pura** `Confronta(fotografia del sectorfile, fotografia di vIPI) → rilievi`,
testabile senza rete e senza database — lo stesso taglio di `ConsistencyReportService.Analyze`.

### A. Posizioni e frequenze — `itfreq.frq` ↔ `AccSector` + `AirportSector`

| Rilievo | Quando | Frase |
|---|---|---|
| **Frequenza divergente** | stesso callsign, `Frequency` diversa | «`LIRN_TWR`: IVAO dice 118.500, il sectorfile 118.505» |
| **Posizione assente nel sectorfile** | callsign nei cataloghi IVAO, non in `itfreq.frq` | chi si connette non ha profilo né mappa |
| **Posizione assente in IVAO** | callsign nel sectorfile, non nei cataloghi | il sectorfile offre una posizione che non esiste |

⚠️ **Fuori dal confronto, misurato:** i callsign che non cominciano per **`LI`** (i confinanti esteri dei
cataloghi vIPI: **142** su 345, e il filtro `IsManual` ne prendeva 5) e i callsign **`*_ATIS`** (**25**: in
vIPI sono posizioni, nel sectorfile stanno nei file `.atis`, che è un'altra cosa).
⚠️ La terza colonna di `itfreq.frq` è una **lista di visibilità con esclusioni** `-XXX`: non si guarda.
⚠️ **Tolleranza 5 kHz**, e non è lassismo: nella spaziatura 8.33 lo **stesso canale** si scrive `118.955` o
`118.950`. Misurato: a 0 kHz escono 7 divergenze, a 5 kHz ne restano **6**, e quella che cade è esattamente
un canale scritto nei due modi.

### B. Aeroporti — `itap.ap` ↔ `Airport`

| Rilievo | Tolleranza |
|---|---|
| **TA divergente** (`TransitionAltitudeFt`) | nessuna: è un numero operativo, o coincide o no |
| **Elevazione divergente** | ±10 ft (arrotondamenti diversi alla fonte) |
| **Coordinate del riferimento divergenti** | ~0,5 NM: sotto è la stessa cosa detta con più cifre |
| **Aeroporto nel sectorfile e non in vIPI**, e viceversa | — |

⚠️ **La TA è il controllo che vale il lavoro da solo.** È il dato che un controllore legge sul documento e
che il sectorfile stampa nel suo, e una divergenza lì si vede in frequenza.
⚠️ `itap.ap` contiene voci **non aeroportuali** (`LIZZ … AIR DEFENCE`) e **44 scali minori** che vIPI non
documenta: «solo nel sectorfile» **non è un rilievo**, è la normalità. Il verso che conta è l'altro — un
aeroporto che **noi documentiamo** e il sectorfile non ha (misurato: 4).
⚠️ E fra gli «aeroporti» di vIPI ci sono i **codici ACC** (`LIRR`, `LIMM`, `LIBB`): si escludono, o si
segnala che il sectorfile non ha un aeroporto che non esiste.
⚠️ Il nome dell'aeroporto **non** si confronta: `FIUMICINO` e `Roma / Fiumicino` sono lo stesso posto, e un
rilievo per riga sarebbe rumore su 129 righe.

### C. Piste — `itrw.rw` ↔ `AirportRunway`

Il formato vero, letto sui dati (la descrizione in `STATO_SECTORFILE_ITALIANO.md` dice «lunghezze» ed è
imprecisa): `ICAO ; ident ; ident opposto ; elev soglia 1 ; elev soglia 2 ; QFU 1 ; QFU 2 ; lat/lon 1 ; lat/lon 2`.
**Le lunghezze di pista non ci sono**, quindi non si confrontano.

| Rilievo | Tolleranza | Misurato |
|---|---|---|
| **Ident divergente** (rinumerazione applicata da una parte sola) | — | **~12 aeroporti**, ed è la famiglia che vale il lavoro |
| **Soglia divergente** | 50 m | 5 |
| ~~QFU divergente~~ | — | 🔴 **controllo eliminato**, vedi sotto |

🔴 **Il QFU non si confronta.** Misurato: 115 divergenze a 1°, 55 a 2°, 10 a 3°, **0 a 5°** — cioè non
esiste una soglia che separi il segnale dal rumore, perché segnale non ce n'è: il delta medio è **+1,63°** e
sottrarre la declinazione magnetica lo **peggiora**. Le due sorgenti scrivono la rotta pista con riferimenti
diversi, e un controllo lì produrrebbe **cento righe false al primo giro**. È la voce che la misura ha
ucciso, ed è il motivo per cui la misura viene prima del codice.

⚠️ **Gli ident si normalizzano prima di confrontarli** (`09` == `9`): il sectorfile scrive lo zero iniziale,
IVAO no. Senza normalizzazione ~40 piste risultano «assenti da una parte» per una cifra, e le **dodici vere**
— `LIRP` 3L/3R/21L/21R contro 4L/4R/22L/22R, `LIED` 16/34 contro 17/35, `LICG`, `LIPR`, `LIBA`, `LIRS` —
sparirebbero nel rumore.
⚠️ **Le 96 righe `MAPS` non sono piste**: sono l'hack italiano per costruire i menu delle mappe, hanno
coordinate nulle e stanno sotto `//MENU MAPPE`. Chi non le filtra apre 96 rilievi falsi al primo giro.
⚠️ Le soglie in vIPI **restano vuote finché l'aeroporto non si ri-importa** (l'import piste è per-aeroporto):
un campo vuoto da una parte è «non lo so», non «divergono». **Mai un rilievo su un dato mancante di casa.**

### D. Fuori scopo, per scelta

- **Le SID** (`*.sid`): le importiamo già, e il loro disallineamento è coperto dal gate AIRAC dell'import.
- **I riferimenti rotti del sectorfile** (`IT\colors\colors.def`, `DYNAMIC_SEC\GCI.tfl`, `PREFS\LIPC.cpr`…,
  §9 di `STATO_SECTORFILE_ITALIANO.md`): sono un audit **interno** al sectorfile. vIPI non è il linter del
  sectorfile — si pronuncia solo dove ha una verità propria da mettere a confronto. ⚠️ Se un giorno lo si
  vorrà, è un altro lavoro con un altro destinatario, e va detto così.

---

## §4 — La trappola che nasconderà i falsi positivi: il **differimento AIRAC**

Il sectorfile lo scriviamo **prima** che il ciclo AIRAC entri in vigore — è il motivo per cui esistono
`AccSector.RegionMapPolygonInForce` e `ShapeAiracCycle`. Quindi una divergenza può essere semplicemente **il
futuro**: il sectorfile ha già il dato del ciclo prossimo, IVAO no.

**Conseguenza sul disegno:** la frase del rilievo dice **sempre** il ciclo AIRAC corrente e da che parte sta
il valore più nuovo, e il rilievo **non** si presenta come un errore. Una divergenza che sparisce da sola al
cambio di ciclo è normale; una che resta per **due cicli** è un problema — ed è la sola che meriterebbe, un
giorno, di essere alzata di grado.

---

## §5 — I pezzi

| Pezzo | Dove | Stato |
|---|---|---|
| Parse di `itfreq.frq` / `itap.ap` / `itrw.rw` | `AuroraSectorfileParser` (funzioni pure, come le altre) | nuovo |
| Fotografia del sectorfile (le tre fette insieme) | `AuroraSectorfileFactsProvider` — ⚠️ **nessuna fetta in `SectorfileCache`**: là la cache esiste perché gli stessi file li chiedono più percorsi, questi tre li legge **un chiamante solo ogni 24 ore**, e una copia in memoria direbbe «confrontato adesso» mostrando file di ieri | nuovo |
| **Un confronto solo**, per il giro e per il tasto | `ISectorfileComparisonRunner` | nuovo |
| Fotografia di vIPI (una query per famiglia) | `ISectorfileComparisonRepository` + impl EF | nuovo |
| Il confronto, **puro** | `Vipi.Application/Diagnostics/SectorfileComparison.cs` | nuovo |
| `ConsistencyArea.Sectorfile` | `ConsistencyModels.cs`, **in coda** | esteso |
| Giro periodico 24 h + fotografia in memoria | hosted service + singleton, modello `IStartupMaintenanceReport` | nuovo |
| Aggancio al report | `ConsistencyReportService` — un `Raccogli` in più | esteso |
| 🔴 Filtro dell'health check | `VipiHealthCheck` — una riga | esteso |
| Chiavi di traduzione (area, categorie, frasi) IT+EN | `SharedResource.resx` / `.en.resx` | nuovo |
| **Pagina «Coerenza sectorfile»**, per famiglia | `Vipi.Ui/Pages/SectorfilePage.razor`, rotta `/services/vsop/sectorfile` | nuovo |
| **Scheda `shortcut`** nella sezione staff dell'hub | `ServicesHome.razor` | esteso |
| ~~Riga «ultimo confronto» in Sorgenti~~ | — | 🔴 **non fatto, e per la ragione di §2/D6**: Sorgenti è il registro di ciò che **scrive**, e una riga lì direbbe che questo giro importa qualcosa. Il timbro sta dove serve — in cima alla pagina del confronto |
| **«Copia l'elenco per l'AOD»** | sulla pagina nuova, un bottone | nuovo |
| **«Confronta adesso»** | stesso `Runner` del giro periodico | nuovo |

Sul bottone: il destinatario di questi rilievi **non ha accesso a questa pagina**. Un elenco in testo semplice
da incollare in Discord o in una mail è ciò che trasforma la diagnosi in una correzione. Costa dieci righe.

## §6 — Che cosa NON si fa

- **Non si corregge niente in automatico.** Mai, in nessun caso: la sorgente autoritativa resta l'API IVAO e
  il sectorfile lo scrive l'AOD.
- **Non si importa** da `itfreq`/`itap`/`itrw`.
- **Nessuna entità, nessuna migrazione, nessuna riga di schema.** ⚠️ È anche il motivo per cui questo lavoro
  può essere fatto **dentro la finestra cieca** (31 ago → 16 set, vedi la memoria `finestra-cieca-al-16-settembre`):
  non tocca il database.
- **Nessun servizio nuovo su `/services`** (§2/D1).
- **Nessuna notifica**: la pagina si guarda, come tutto il resto (`da-fare-una-lista-sola` §5).

## §7 — Le slice

1. ✅ **S0 — misura** (§0-bis). Eseguita il 1 settembre 2026: ha eliminato un controllo e aggiunto quattro filtri.
2. ✅ **S1 — parse + fotografia**: le tre funzioni di parse nel parser puro, con fixture ritagliate dai file veri.
3. ✅ **S2 — confronto puro** + i suoi test, senza rete e senza DB.
4. ✅ **S3 — aggancio**: repository EF, hosted service, singleton, `ConsistencyArea.Sectorfile`, resx.
   🔴 Nella **stessa** slice il filtro dell'health check (§2/D5): mai un commit in cui l'health è giallo.
5. ✅ **S4 — la lettura**: la pagina per famiglia, la scheda `shortcut` nell'hub, il tasto «confronta adesso»
   e quello «copia per l'AOD».

Un commit per slice, `dotnet build Vipi.slnx -c Release --no-incremental` verde sui due TFM a ogni passo.

## §8 — La prova

**Test** — 50 nuovi, tutti verdi sui due TFM (Application 1991, Ui 1064, Hosting 58, Infrastructure 1202/1188):

- `SectorfileComparisonTests` (33, `Vipi.Application.Tests`) — il cuore puro. ⚠️ **Metà difende dal rumore,
  non dai difetti**: i callsign esteri, gli ATIS, le righe manuali, il dato mancante di casa, i codici ACC fra
  gli aeroporti. Ogni filtro corrisponde a una famiglia che la misura ha visto produrre falsi.
- `AuroraSectorfileFactsParserTests` (15, `Vipi.Infrastructure.Tests`) — le fixture sono **righe vere**, con le
  stranezze dentro: l'intestazione a barre, la TA a zero, le pseudo-piste `MAPS`, lo zero iniziale.
- `VipiHealthCheckTests` — 🔴 la riga che conta: **solo divergenze col sectorfile ⇒ zero incongruenze**, cioè
  l'endpoint di salute resta verde. Il conteggio è stato **estratto in una funzione pura** apposta per poterlo
  provare: è una decisione, non un `Count()`.
- `SharedResourceIntegrityTests` — ogni categoria prodotta ha le sue **tre** righe (categoria, spiegazione,
  bersaglio) in **entrambe** le lingue. ⚠️ Quelle chiavi il sorgente non le nomina mai come letterali — stanno
  in costanti e in un campo `DetailKey` — quindi il controllo generale non le vedeva: è lo stesso buco da cui
  erano passate `ImpactKind_SectorRenamed` e `ImpactKind_SectorDetached`.
- `ServicesHomeTests` — la scheda nuova: ordine, `shortcut` contate (2 → **3**), e il cancello dello staff.

## §9 — ✅ Verificato a schermo (1 settembre 2026)

Guidata in locale (Edge + puppeteer-core, copia del `vipi.db` reale, sorgente sectorfile **accesa** — è la
deroga prevista dalla skill `verifica-live`: se è il sectorfile che si verifica, spegnerlo non verifica niente).

**Che cosa si è visto**, cliccando «Confronta adesso» su dati veri:

| | |
|---|---|
| Rilievi totali | **36** — 13 posizioni · 7 aeroporti · 16 piste |
| I due grossi | `LIRF_PS1_APP` 136.100 contro 131.100 e `LIRM_APP` 132.255 contro 135.255: **5 MHz e 3 MHz** |
| Le TA | `LICD` 5000/4000, `LIMF` e `LIMZ` 6000/7000 |
| Le rinumerazioni | `LIRP` 22L/22R/4L/4R contro 21L/21R/3L/3R, `LIED` 17→16, `LICG`, `LIPR`, `LIBA`, `LIRS` |
| La coordinata rotta | `LIAA/27`: le due sorgenti mettono la soglia a **4 907 002 m** di distanza |
| Diagnostica | il chip dell'area nuova compare accanto agli altri sei: `Dati 1 · Sorgente 29 · **Sectorfile 36**` |
| Salute | `/vsop/health` conta **30**, non 66: le divergenze non muovono il verdetto |
| «Copia per l'AOD» | 5 961 caratteri negli appunti, e la conferma compare **solo** perché la copia è riuscita |
| Larghezza | nessuno sforo orizzontale a 1440 |

**Il cancello, guidato ai due livelli** (identità di sviluppo abbassata, VID **non** fondatore):

| chi | hub | pagina scritta a mano |
|---|---|---|
| `IT-XYZ9` → **DivisionStaff** | vede spazi aerei e convertitore, **non** la coerenza | «Accesso riservato… livello Editor e superiori», **zero** tabelle, nessun tasto «confronta adesso» |
| `LIRR-CH` → **Editor** | vede tutte e tre le scorciatoie | pagina aperta, tasto presente |

⚠️ Il VID va cambiato e non basta la posizione: `RoleResolver` comincia con `if (_founders.Contains(userId))`
e **704798 è un fondatore** — con quello si resta Admin qualunque cosa si scriva.

### ⚠️ Il difetto trovato **solo** a schermo, e non dai test

La testata della tabella si disegnava **sotto la prima riga**. Misurato: intestazione a 320, prima riga a 295.

La causa non era la pagina ma la coppia di classi: `.res-table.sticky-head thead th` ha `top:62px` — l'altezza
della topbar appiccicata — e dentro un contenitore con `overflow` (`.st-scroll`) **il riferimento dello sticky
diventa quel contenitore**, quindi quei 62px smettono di essere «resta sotto la barra» e diventano uno
**spostamento in giù di 62 px**. Sulla pagina di Diagnostica non si vede perché lì una regola più specifica
(`.struct .st-scroll …{top:0}`) lo azzera, e quella regola pretende un antenato `.struct` che questa pagina non
ha. Lo dice anche il commento della regola globale `.wrap *:has(> table)`, che infatti **esclude** le tabelle a
intestazione appiccicata.

**Rimedio:** tolto il `.st-scroll`. Rimisurato: intestazione 258→295, prima riga 295→330 — contigue — e
scorrendo la pagina l'intestazione si **incolla a esattamente 62 px**, che è ciò per cui la classe esiste.

⚠️ La lezione è la solita di questo progetto, e vale più del difetto: **una classe copiata da un'altra pagina
si porta dietro le regole di quella pagina**. Nessun test l'avrebbe vista: il DOM era corretto, l'ordine dei
nodi giusto, e il conteggio delle righe pure.
