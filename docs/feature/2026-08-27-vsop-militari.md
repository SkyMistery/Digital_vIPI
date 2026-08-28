# vSOP militari — carta 🟣

> Portare i SOP militari italiani dentro il sistema come **edizione militare** dei documenti che già
> esistono, non come silo parallelo. Aeroporti prima, APP non remotizzati subito dopo.
>
> Sorgente misurata: i **15 PDF** in `MIL vSOP IVAO/` (LIBA, LIBN, LIBV, LICZ, LIED, LIPA, LIPC, LIPI,
> LIPL, LIPS, LIRE, LIRL, LIRM, LIRP, LIRS).

## 0. Il dato che decide il disegno

I 15 indici sono **lo stesso indice**, parola per parola, con code diverse per campo. Non è contenuto
libero: è un **profilo di catalogo**, esattamente come App, AccAerovia, Vloa e Airport.

E non è il profilo `Airport`: su venti sezioni militari, con quello civile ne condivide **due**
(`frequencies`, `runways`) e anche quelle con colonne diverse — le frequenze militari portano CRC/GCI/AEW
(`LIVK_CRC_CTR`, `LIZZ_AEW_CTR`) che nel catalogo settori non esistono, le piste sono coordinate di soglia
che `AirportRunway` non ha.

⚠️ **I PDF di partenza sono in inglese, ma il documento nostro no** — vedi §1d. Fino al 28 agosto 2026
questa carta contava la lingua fra le ragioni per separare i due profili; da quando i documenti si leggono
in due lingue quell'argomento non serve più, e le due ragioni che restano — venti sezioni diverse e cicli
AIRAC indipendenti — bastano da sole.

Da qui: **documento separato**, non sezioni innestate nel documento civile.

## 1. Modello — «aggiungo un concetto o ne esiste già uno?»

Nessun modello gemello. Stesso `Document`, stessa `DocumentVersion`, stesse `DocumentSection`, stesso
motore di release, stessi editor. Cambiano **tre cose sole**:

### 1a. Un discriminatore, in un posto solo

```csharp
public enum DocumentEdition { Civil = 0, Military = 1 }   // Vipi.Domain/Enums.cs
public DocumentEdition Edition { get; set; }              // colonna su Document
```

Perché sul `Document` e non dedotto dalle navigazioni: `IReleaseTarget.TryDescribe(Document doc, …)`
decide il tipo **guardando l'oggetto in mano**, e `AirportReleaseTarget` è il **catch-all** dei `Document`
vIPI non riconosciuti come APP/ACC (`DescribeOrder = 3`). Senza un discriminatore locale ogni documento
militare finirebbe lì dentro in silenzio, e la diagnosi sarebbe «l'aeroporto mostra il documento
sbagliato» — lo stesso guasto già pagato con l'APP standalone.

### 1b. Un link per proprietario, gemello di quello che c'è

```csharp
public int? MilDocumentId { get; set; }   // su Airport, gemello di DocumentId (indice unico)
public int? MilDocumentId { get; set; }   // su Sector,  gemello di DocumentId
```

⚠️ **Perché due colonne e non una relazione sola.** La FK sta sul **proprietario** (`Airport.DocumentId`
dal 25 agosto, `Sector.DocumentId` per APP/ACC), quindi una riga proprietario può puntare a un documento
solo. Le alternative si sono misurate e perdono:

- *auto-riferimento* `Document.MilitaryTwinOf` → si rompe sugli aeroporti **solo militari** (Aviano,
  Ghedi, Decimomannu, Rivolto): non hanno un documento civile a cui agganciarsi, e bisognerebbe
  fabbricargliene uno finto;
- *tabella di legame* `DocumentBinding` → due posti dove cercare «dov'è il documento di X». È
  esattamente ciò che la domanda 1 del pre-flight vieta.

La colonna gemella non è un **modello** gemello: è il minimo che la direzione della FK impone, e la
domanda «di che edizione è questo documento» continua ad avere **una** risposta sola (§1a).

### 1c. Due profili di catalogo

`SectionProfile.AirportMil` e `SectionProfile.AppMil`. `DocumentBirth.Crea(...)` prende già `Language` e
`SectionProfile`: la nascita costa zero.

**`AppMil` = le stesse sezioni di `App`**, per ora, per decisione del committente (il contenuto vero
arriva dopo). Nel `SectionCatalog` il profilo **rimanda** a quello civile invece di ricopiarne l'array —
due elenchi che devono restare uguali divergono, è già successo fra `VloaSections` e il registro.

### 1d. ⚠️ Il documento militare nasce in ITALIANO — decisione del 28 agosto 2026

Questa carta diceva `Language.En`, perché i quindici PDF di partenza sono in inglese. **Non vale più.**

Dal 28 agosto i documenti si **scrivono in una lingua e si leggono in due**
([[../feature/2026-08-27-documenti-bilingue.md]]): la lingua sorgente non è più «la lingua in cui il
lettore lo vedrà», è **la lingua in cui lo si redige**. E chi redige è la divisione italiana.

Quindi `DocumentBirth.Crea(..., Language.It, SectionProfile.AirportMil, ...)`, e un lettore inglese lo
ottiene tradotto come qualunque altro documento.

**Ne discendono tre cose, e la prima va applicata subito.**

1. **I titoli delle 24 sezioni sono in italiano** (§2). Erano in inglese *proprio perché* il documento
   nasceva `En`: cambiata la premessa, cambia la conclusione. L'originale inglese resta scritto accanto a
   ciascuno, perché serve a chi trascrive dal PDF.
2. **Il verso della traduzione si inverte**: it→en invece di en→it. Nessun codice da cambiare — la
   direzione la decide `Document.Language` — ma è il verso che il giro schedulato riempirà.
3. **Il congelamento** della prosa generata avverrà in italiano (`ReadingLanguageContext` prende la lingua
   sorgente del documento al publish), e un lettore inglese la ricomporrà live.

### ⚠️ Lavoro aperto: lo stesso criterio vale per gli ALTRI documenti

Deciso il 28 agosto 2026, **da fare dopo**: se la lingua sorgente è «quella in cui si redige», allora la
**vLOA non dovrebbe più nascere `Language.En`**. Oggi è l'unico documento che nasce inglese, ed è un
residuo di quando l'inglese era l'unico modo di renderla leggibile alla controparte estera — un problema
che la traduzione ha risolto.

Non si cambia in questo giro: le vLOA esistenti sono **scritte** in inglese, e ribaltarne la lingua
sorgente senza riscriverne il contenuto renderebbe la memoria di traduzione inutile su tutto quel corpus
(le impronte sono del testo inglese, e cercarle come italiano non troverebbe niente). Vuole un giro suo,
con un travaso pensato.

## 2. Le sezioni del profilo `AirportMil`

Titoli in **italiano** (`Language.It`, vedi §1d), con accanto l'originale inglese del PDF — serve a chi
trascrive. `H` = corpo dalla pagina, `D` = corpo dai blocchi, `HB` = scheda dalla pagina **più** blocchi
propri. **In grassetto le chiavi riusate** dal catalogo esistente.

| # | Chiave | Titolo | Corpo | Note |
|---|---|---|---|---|
| 1 | **`weather`** | METAR & TAF | H | ✚ *non è nel PDF*: METAR/TAF live. Sempre-live, costo zero, nascondibile |
| 2 | `generaldata` | Dati generali <br><small>*General Data*</small> | D | contenitore |
| 2.1 | `navaids` | Radioassistenze <br><small>*Navaids*</small> | D | tabella Type/Name/Freq/Coordinates |
| 2.2 | **`frequencies`** | Frequenze ATC/CRC <br><small>*ATC/CRC Freqs*</small> | **HB** | derivata: posizioni IVAO dello scalo · blocchi: CRC/GCI/AEW, che il catalogo settori non ha |
| 2.3 | `diversion` | Aeroporti alternati <br><small>*Diversion Airfields*</small> | D | Airport/Navaid/Bearing/Distance |
| 2.4 | **`runways`** | Piste <br><small>*Runways*</small> | **HB** | derivata: ident/lunghezza/QFU dall'anagrafica · blocchi: coordinate soglie (`AirportRunway` non le ha) |
| 2.5 | **`transition`** | Quote di transizione <br><small>*Transition Altitude/Level*</small> | H | ✚ *non è nel PDF*: TA + tabella TL per fascia QNH |
| 2.6 | `callsigns` | Nominativi <br><small>*Callsigns*</small> | D | Squadron / OAT c/s / GAT c/s |
| 3 | `groundprocedures` | Procedure di terra <br><small>*Ground Procedures*</small> | D | contenitore |
| 3.1 | `parkings` | Parcheggi <br><small>*Parkings*</small> | D | |
| 3.2 | `enginestart` | Messa in moto <br><small>*Engine Start*</small> | D | |
| 3.3 | `taxiing` | Rullaggio <br><small>*Taxiing*</small> | D | **immagini**: Apron flow, Manoeuvring area flow |
| 3.4 | `arming` | Armamento/disarmo <br><small>*Arming/Dearming*</small> | D | |
| 4 | `flightprocedures` | Procedure di volo <br><small>*Flight Procedures*</small> | D | contenitore |
| 4.1 | `takeoff` | Restrizioni al decollo <br><small>*Takeoff Restrictions*</small> | D | |
| 4.2 | `sfo` | Circuito SFO/precauzionale <br><small>*SFO / Precautional Circuit*</small> | D | |
| 4.3 | `commfail` | Avaria comunicazioni <br><small>*Commfail*</small> | D | |
| 4.4 | `gca` | Circuito GCA <br><small>*GCA Circuit*</small> | D | |
| 4.5 | `vfrjet` | Porte e circuiti VFR jet <br><small>*VFR Jet Entry/Exit Gates and Circuits*</small> | D | **immagini** + tabella significant points |
| 4.6 | `ifrsignificant` | Punti significativi strumentali <br><small>*Instrumental Procedures Significant Points*</small> | D | |
| 4.7 | `gat` | Partenze/arrivi IFR GAT <br><small>*IFR GAT Dep/Arr*</small> | D | |
| 4.8 | `qra` | QRA / Scramble <br><small>*QRA / Scramble*</small> | D | ✚ *non è nel PDF* — vedi sotto |
| 5 | **`regulated`** | Aree di lavoro <br><small>*Working Areas*</small> | **HB** | la mappa AoR con le chip per area **è già** quello che il PDF disegna a mano; sotto la prosa |
| 5.1 | **`operationaltechnique`** | Procedure generali <br><small>*General Procedures*</small> | D | dep/arr come blocchi o sotto-sezioni |
| 5.2 | `lowlevel` | Bassa quota (BOAT) <br><small>*Low Level (BOAT)*</small> | D | aree tattiche dove si vola il BOAT — 9 SOP su 15 |
| 6 | **`validity`** | Validità e revisione <br><small>*Validity and Revision*</small> | HB | obbligatoria, timbra ciclo e release |

⚠️ **Ventisei, non ventiquattro come diceva questa carta fino al 28 agosto 2026**: il conto era rimasto
indietro di due quando si sono aggiunte `qra` e `lowlevel`. L'ha corretto un test che contava le righe
vere del profilo, non la memoria di chi le aveva scritte.

**Sei sezioni riusate su ventisei**, e due (`regulated`, `frequencies`) riusano anche il **motore**,
non solo la chiave.

### ⚠️ `qra` è contenuto NUOVO, non trascrizione

Misurato sui 15 PDF: **una sezione QRA/Scramble non esiste in nessuno**. QRA compare solo come
*colonne* — il callsign in Callsigns (`GA01-GA02` a Grosseto, `HAxx` a Gioia) e i parcheggi in Parkings
(`Q1->Q4`) — e solo nelle **quattro basi di difesa aerea**: LIBA Amendola, LIBV Gioia del Colle,
LIPS Istrana, LIRS Grosseto.

Va scritta da zero (stati di allarme, priorità, clearance, passaggio al CRC/GCI), e si semina su tutti
perché nascondere è un clic mentre aggiungere una sezione di catalogo non seminata non lo è. Sugli
altri undici campi nasce e si nasconde.

È anche la **prima candidata al flag audience** di tutto il documento: la procedura di scramble è la
cosa più asimmetrica fra chi la vola e chi la gestisce.

### `lowlevel` sta sotto Working Areas, non fra le Flight Procedures

Nei PDF è sempre sorella di Departure/Arrival procedures dentro WORKING AREAS, e il contenuto lo spiega:
«Tactical Areas (AT) surrounding PISA CTR where BOAT can be executed». Parla di **aree**, quindi vive
sotto la sezione che le disegna.

### ⚠️ Che cosa vuol dire «riusata» — e che cosa NON vuol dire

Riusare una chiave condivide **tre cose**, e il contenuto non è fra queste.

| Livello | Condiviso? | Conseguenza |
|---|---|---|
| Chiave e natura (`SectionCatalog`) | **sì**, un posto solo | «`frequencies` è Derived» vale ovunque |
| Motore che disegna il corpo | **sì** | un difetto nel renderer si corregge una volta per tutti |
| **Contenuto editoriale** | **no** | ogni documento ha le sue righe `DocumentSection` + `ContentBlock` |

Quindi: **scrivere nella sezione Frequenze del SOP militare di Rivolto non tocca nessun altro
documento.** Le sezioni vivono sulla `DocumentVersion`, i blocchi sulla sezione: due documenti sono due
righe, con due contenuti, due versioni e due release.

C'è però un caso che **si propaga davvero**, e non è il riuso della chiave: le sezioni **derivate**. Il
loro corpo non sta nel documento, si calcola da una sorgente unica al momento della vista. Se cambio
una frequenza nel **catalogo settori**, cambia in ogni documento che la deriva — militare e civile — e
questo vale già oggi fra la vIPI d'aeroporto e quelle di torre e avvicinamento. Con due limiti:

- vale sulla parte **derivata**, non sui blocchi editoriali che le stanno sotto;
- il pubblico la vede solo se la sezione è `RenderMode.Live`; se è **Frozen** — il default — resta ferma
  allo snapshot fino alla **ripubblicazione**. È la trappola già pagata due volte.

Detto sull'esempio chiesto: cambio una frequenza nel catalogo → la tabella derivata cambia in entrambi i
documenti di quello scalo (alla vista se Live, alla ripubblicazione se Frozen); scrivo una riga CRC nei
blocchi del SOP militare → la vede solo il SOP militare.

⚠️ **Sul contenuto di `frequencies` in particolare**, misurato su LIPI Rivolto: la tabella ATC/CRC del
SOP non è «le posizioni di questo scalo». Elenca `LIPI_TWR`, **`LIPA_APP` — l'avvicinamento di un altro
campo** —, `LIPI_G_APP`, `LIPP_MIL_CTR` e i CRC/AEW. La parte derivabile dall'anagrafica è quindi la
**minoranza**: su un campo militare i blocchi editoriali sotto la scheda pesano più della scheda. `HB`
resta la forma giusta — quel che si può derivare non invecchia mai — ma senza illusioni su quanto copra.

### Quello che NON semino, e perché

Le code per campo — LVP (LIRE), SAR alert (LIPC), Combat departure/recovery (LIBV), Range LI-R59 (LIED),
HEMS + Random shallow approach (LIRP), VFR routes + Special VFR (LIRE), IFTS parkings (LIED) — sono
**custom sections**, che il catalogo già sa fare. Il profilo semina il denominatore comune; il singolo
campo aggiunge il suo. Seminare venticinque sezioni perché una le ha tutte significa che ventiquattro
documenti nascono con roba da nascondere.

Fuori restano anche `aor` (un aeroporto è un luogo, l'AoR è della torre), `coordination` (idem) e `sids`
(l'import SID Aurora non copre i campi militari; il GAT DEP/ARR è editoriale).

### ⚠️ Sezioni annidate: costo da mettere in conto

`DocumentBirth` semina **solo il livello 0** (`SectionCatalog.For(profile)`), e la struttura vera dei SOP
ha quattro contenitori con figli. Serve estendere la nascita a seminare i figli del catalogo. È un
miglioramento **condiviso** (il `ChildRegistry` esiste già per i coordinamenti vLOA e alla nascita non lo
legge nessuno), non un pezzo militare.

## 3. Il filtro pilota / ATC — su tutti i documenti

Quinto flag per-sezione, accanto a `RenderMode`, `IsHidden`, `BeforeParentBody`, `LeadSentence`: stesso
posto, stessa ragione — **versionato**, quindi catturato nello snapshot di release senza codice in più.

```csharp
public enum SectionAudience { Both = 0, Pilots, Controllers }
public SectionAudience Audience { get; set; }   // su DocumentSection
```

`Both` di default → **nessun documento esistente cambia di una virgola**.

**Etichetta prima, filtro dopo.** Ogni sezione marcata porta un badge (`Pilota` / `ATC`); le `Both` —
che saranno la maggioranza, il contenuto davvero di uno solo dei due è poco — nessun badge. In testata
una chip *Tutto · Pilota · ATC* che nasconde **solo** le sezioni marcate dell'altro. Le `Both` restano
sempre: un pilota che perde il contesto ATC legge peggio, non meglio.

La chip compare **solo se il documento ha almeno una sezione marcata**. Così su ACC/APP/vLOA il
meccanismo c'è ma non si vede finché non lo si usa.

**La scelta va in query string** (`?vista=pilota`), non in stato d'isola. Due ragioni: su pagina
interattiva `OnInitializedAsync` gira una volta sola e un filtro in query non ricarica niente (già
pagato); e un link filtrato è **condivisibile** — la divisione pubblica ai piloti l'URL della vista
pilota, che è probabilmente il valore vero della feature. Le pagine pubbliche sono SSR statiche: la
navigazione ridisegna, e basta.

**Regola sui figli**: una sezione filtrata via porta via i suoi figli. Un figlio `ATC` sotto un padre
`Both` sparisce da solo in vista pilota.

⚠️ **Non è controllo d'accesso.** Il documento è pubblico e la vista ATC la apre chiunque cambi l'URL.
È un filtro di lettura, e va scritto nella guida perché nessuno ci metta dentro cose che non deve.

### Impronta di propagazione (misurata su `BeforeParentBody`)

Un flag per-sezione tocca: `DocumentSection` + migrazione ×2 (SQLite/MySql), due DTO in
`DocumentModels`/`EditingModels`, le proiezioni in `AccDocumentAssembler` (×2),
`AirportLegacySections`, `VipiViewService`, `EfContentRepository` (×2), `EfEditingRepository` (×2),
`IEditingService`/`EditingService`/`IEditingRepository` + setter. **Circa 15 punti**, tutti meccanici e
tutti già percorsi tre volte.

## 4. Dispatch — «switch che switcho già altrove?»

Regola del 2: i due registry esistono già, e adesso si guadagnano lo stipendio.

- `ReleaseTargetType` += `AirportMil`, `AppMil`. Nuovi `AirportMilReleaseTarget` / `AppMilReleaseTarget`
  con **`DescribeOrder` più basso di tutti** e primo controllo `doc.Edition == Military`.
- `IDocKindRoutes` += due descrittori. Nessuna pagina tocca una URL.
- ✅ **Verificato**: l'identità di una release è `(TargetType, TargetKey)` — indice unico
  `(TargetType, TargetKey, VersionNumber)`. Quindi `AirportMil|LIRP` e `Airport|LIRP` **convivono** con
  progressivi e cicli AIRAC indipendenti. È il requisito che ha deciso il documento separato, ed è già
  soddisfatto dallo schema: zero lavoro.
- `AuthAccCodeAsync` dei due target militari **delega** al gemello civile: l'ACC di autorizzazione è lo
  stesso, il permesso pure.

## 5. Ingressi UI — e il catch-22

- `/services/vsop/mil` — elenco nazionale dei campi con edizione militare, **raggruppato per ACC** (un
  pilota cerca «Ghedi», non «LIMM»). Linkata da `/services` (card «vSOP militari») e da `/services/vsop`.
- Testata del documento civile → scheda «vSOP militare» quando `MilDocumentId != null`. E il ritorno.
- **Catch-22 da evitare**: l'elenco pubblico filtra su release effettiva, quindi il primo documento non
  sarebbe raggiungibile. Come per gli APP: allo **staff** l'elenco mostra anche i campi senza documento,
  con il tasto che lo crea.
- Creazione dalla pagina Aeroporti admin (dove già vive `IsMilitaryOnly`) e dal wizard nuovo documento.

### Com'è finita davvero (28 agosto 2026)

Gli ingressi realizzati sono **tre**: la card su `/services/vsop`, l'elenco nazionale `/services/vsop/mil`
(con «Crea il vSOP» e «✎ Modifica» per lo staff) e la scheda incrociata sul documento civile. Due previsti
non si sono fatti, e vale la pena dire perché:

| previsto | esito | ragione |
|---|---|---|
| card su `/services` | **no** | i vSOP militari non sono un **servizio**: sono una parte della vSOP. C'è un test che pretende che ogni figlio di `/services` sia un servizio a un solo segmento (`ServicesHomeTests`), e metterla lì lo faceva diventare rosso. La regola ha ragione: la card sta su `/services/vsop`, accanto agli altri elenchi di documenti |
| tasto nella pagina Aeroporti admin | **no** | quella colonna azioni è già misurata al limite: il commento in `AeroportiPage.razor` racconta che un quinto tasto spingeva il cestino **oltre il bordo del pannello** a 1600px. Un solo posto da cui si crea — l'elenco militare — è anche più facile da spiegare |

⚠️ **I candidati si elencano per `HasMilitaryPresence`, non per `IsMilitaryOnly`** — al contrario di quanto
diceva la trappola 6 di §7. La ragione è nei quindici PDF: **LIRP Pisa c'è**, ed è uno scalo civile con
sedime militare. Filtrare per `IsMilitaryOnly` avrebbe nascosto proprio i campi misti che un SOP militare
ce l'hanno davvero. `IsMilitaryOnly` resta, ma come **etichetta** sulla riga («solo militare» /
«civile+militare»): dice al lettore che cos'è quel campo, senza decidere per lui.

## 6. Slice di esecuzione

| # | Slice | Verde su | |
|---|---|---|---|
| 1 | `SectionAudience` su `DocumentSection` + migrazioni + propagazione DTO/proiezioni | test |
| 2 | Editor: selettore audience per sezione + badge | live |
| 3 | Viewer: chip `?vista=` + filtro + regola sui figli | live |
| 4 | `DocumentEdition` + `Airport.MilDocumentId` + `Sector.MilDocumentId` + migrazioni | test |
| 5 | `DocumentBirth` semina anche i figli del catalogo (`ChildRegistry`) | test |
| 6 | `SectionProfile.AirportMil` + le 26 sezioni nel `SectionCatalog` | test |
| 7 | `SectionProfile.AppMil` (rimanda ad `App`) | test |
| 8 | Due `IReleaseTarget` + due `IDocKindRoutes` | test |
| 9 | Rotte, `/services/vsop/mil`, schede incrociate, creazione da admin, **editor** | live | ✅ 28-ago |
| 10 | Carico di un SOP vero (LIPI Rivolto, il più corto) come prova end-to-end | live | ✅ 28-ago |

⚠️ **L'editor non era in elenco e serviva lo stesso.** La slice 9 diceva «rotte, elenco, schede, creazione»:
tutte cose che portano a un documento che poi **non si può scrivere**. `MilDocRoutes` dichiarava già un
`EditorUrl` verso una pagina che non esisteva. `MilEditorPage` è nato qui, ed è il più magro dei cinque —
26 sezioni di cui 20 di sola prosa, e le sei rese dalla pagina che dicono soltanto *dove* si cambiano i
dati (nell'editor dell'aeroporto civile), perché due editor sullo stesso dato sono due verità che divergono.

## 7. Trappole già note

1. **Il catch-all dell'aeroporto** (§1a) — ✅ **soluzione approvata dal committente il 28 agosto 2026**,
   da applicare nella slice 8.

   `AirportReleaseTarget.TryDescribe` accetta **qualunque** `Document` vIPI non riconosciuto come APP o
   ACC: è il catch-all, e ha `DescribeOrder = 3`, il più alto. Senza intervento ogni documento militare ci
   finirebbe dentro **in silenzio**, e la diagnosi sarebbe «l'aeroporto mostra il documento sbagliato» —
   lo stesso guasto già pagato con l'APP standalone.

   **La soluzione è a due mani, e servono entrambe:**

   | | Che cosa | Perché non basta l'altra da sola |
   |---|---|---|
   | **a** | `AirportMilReleaseTarget` e `AppMilReleaseTarget` hanno `DescribeOrder` **più basso di tutti** (0) | l'ordine da solo non basta: un documento militare che i due target militari *non* riconoscono ricadrebbe comunque nel catch-all |
   | **b** | Ogni `TryDescribe` — militare **e civile** — controlla `doc.Edition` come **prima** riga | il controllo da solo non basta: senza l'ordine, il catch-all civile verrebbe interrogato prima e il test su `Edition` non lo raggiungerebbe mai |

   ⚠️ **Il controllo va messo anche sui target CIVILI**, non solo sui nuovi. È la metà che si dimentica:
   aggiungere `Edition == Military` ai militari lascia i civili disposti ad accettare un documento
   militare, e l'ordine è l'unica cosa che lo impedirebbe. Due difese indipendenti, ognuna sufficiente —
   la stessa forma delle due guardie sulle corse del `DbContext`.

   ⚠️ **Serve un test che lo pretenda**, non solo il codice: un `Document` con `Edition = Military` passato
   a `AirportReleaseTarget.TryDescribe` deve tornare `false`. Senza, la regressione è muta — il catch-all
   non fallisce, risponde.
2. **`ReleaseTargetType` è persistito** nelle `DocReleases`: i valori nuovi vanno **in coda** all'enum,
   mai inseriti in mezzo.
3. **Immagini**: apron flow, manoeuvring flow, circuiti VFR sono figure. `MediaAsset`/`IMediaStore`
   ci sono, ma i file vanno estratti a mano dai 15 PDF — è lavoro manuale, non codice.
4. **Bool nuovo = false ovunque**: qui sono enum/int, quindi siamo puliti. Ma `Edition` deve nascere
   `Civil` per migrazione esplicita, non per fortuna.
5. **`packages.lock.json`** se si aggiunge qualcosa; `dotnet build Vipi.slnx -c Release
   --no-incremental` su **entrambi** i TFM prima di dire fatto.
6. **Aeroporti solo militari**: `IsMilitaryOnly` è un giudizio di un amministratore, l'import non lo
   tocca. È il flag giusto per suggerire «questo campo vuole un'edizione militare», **non**
   `HasMilitaryPresence`, che è vero anche su Linate e Ciampino.

## 8. Quel che ha trovato la prova a schermo (28 agosto 2026)

I test erano verdi e la funzione girava. Poi si è guidato il browser sul documento vero, e sono usciti
**due difetti** — nessuno dei due era del codice militare.

### 8a. Una sezione con la scheda **e** le sotto-sezioni le mostrava DUE volte

`DocumentSectionsView` rende il corpo di una sezione derivata in tre chiamate: sotto-sezioni «prima», la
scheda che disegna la pagina, sotto-sezioni «dopo». La chiamata di mezzo — quella dei blocchi propri —
usava lo slot `All`, che rende **anche** le sotto-sezioni. Su tutte le famiglie fin qui non si vedeva:
nessuna sezione `HostAndBlocks` aveva figli. «Aree di lavoro» del vSOP militare è la prima, e a schermo
usciva così:

```
▸ Aree di lavoro
    Procedure generali
    Bassa quota (BOAT)
    Procedure generali      ← di nuovo
    Bassa quota (BOAT)      ← di nuovo
```

Difetto **latente da sempre** nel componente condiviso: mancava soltanto un profilo che lo esercitasse.
Chiuso con `SectionSlot.Blocks` (solo i blocchi, nessuna sotto-sezione) più una prova in
`ParitaQuattroDocumentiTests` che gira su **tutti** i profili — verificata rossa prima di correggere.

### 8b. La macchina traduceva «Piste» con *Slopes*

Alla prima lettura in inglese: «Piste» → *Slopes*, «Quote di transizione» → *Transition Dimensions*. Non
sono sfumature, sono i titoli sbagliati di due sezioni — e un controllore che li legge così non trova
quello che cerca. La macchina non poteva saperlo (*pista* è anche una pista da sci); **noi sì**: quei
titoli vengono dai quindici SOP, che sono scritti in inglese, e l'originale è nella tabella di §2.

`TitoliUfficiali` mette i 26 titoli in memoria come **Human** prima di ogni giro: la macchina non li vede
nemmeno, la pagina di revisione non li elenca, e una correzione umana successiva resta l'unica cosa che
può cambiarli. ⚠️ Il seme guarda le impronte **umane**, non tutte: quando è nato, «Slopes» era **già** in
memoria, messo lì dal giro automatico — un seme che si fermasse davanti a qualunque voce esistente non
avrebbe corretto niente proprio dove serviva.

È il primo pezzo del glossario di fraseologia di `lavori-aperti §Q3`. Non chiude la domanda su **chi** lo
cura; toglie di mezzo il caso in cui la risposta giusta era già scritta e la stavamo buttando via.

### 8c. Trappola dell'attrezzo: `innerText` non vede dentro un `<details>` chiuso

Il primo giro di sonda ha riportato **24 sezioni su 26**, e mancavano proprio i due figli di «Aree di
lavoro». Non era un difetto: `regulated` è l'unica chiave che nasce **chiusa** (`InitiallyCollapsedKeys`),
e `innerText` di un `<details>` chiuso è vuoto. Con `textContent` le sezioni erano 26.

⚠️ Vale come regola per le prossime verifiche: **`textContent` per contare, `innerText` per leggere quel
che l'utente vede**. Confonderli fa cercare un difetto che sta nell'attrezzo — è la stessa lezione di
`probe.js` e delle alfa non composte.

## 9. LIPI Rivolto, il primo SOP vero (slice 10)

`tools/Vipi.MilSopLoader` mette una trascrizione al posto giusto e dice che cosa resta fuori. Come
`Vipi.AgreementsToSections`: prova a vuoto per default, `--apply` per scrivere, non gira all'avvio.

```
dotnet run --project tools/Vipi.MilSopLoader -- --sqlite <file.db> --icao LIPI [--apply]
```

⚠️ **Non è un lettore di PDF, e non deve diventarlo.** Il contenuto è trascritto a mano per due ragioni che
nessun parser risolve: va **tradotto in italiano** (§1d), e metà di ciò che conta nei quindici SOP sono
**figure**.

### Che cosa dice la misura

| | |
|---|---|
| sezioni del profilo | 26 |
| trascritte con contenuto | **19** |
| vuote perché **contenitori** | 3 (`generaldata`, `groundprocedures`, `flightprocedures`) |
| vuote perché la **scheda la disegna la pagina** | 2 (`weather`, `transition`) |
| **nascoste** perché su questo campo non esistono | 2 (`qra`, `lowlevel`) |
| **incomplete**: nel PDF sono figure | 3 (`taxiing`, `arming`, `vfrjet`) |

⚠️ **Il rendiconto distingue i quattro motivi, e questa è la parte che conta**: una sezione vuota perché è un
contenitore, una vuota perché la scheda la disegna la pagina, una vuota perché il PDF ha un disegno e una
vuota per dimenticanza si assomigliano solo a chi non guarda. Contenitore e «resa dalla pagina» le dice il
**catalogo**, non un elenco scritto nello strumento.

⚠️ **Nascondere non è lasciare vuoto.** Il profilo semina tutto su tutti perché nascondere è un clic (§2), ma
una sezione vuota lasciata in vista dice al lettore «qui manca qualcosa» — che su Rivolto, dove QRA e bassa
quota non esistono, è **falso**.

⚠️ **Lo strumento non ripassa sopra a una sezione che ha già contenuto**: si ferma e lo dice. Il blocco
*segnaposto* delle sezioni rese dalla pagina non conta come contenuto — nasce vuoto alla creazione, e
scambiarlo per lavoro di qualcuno bloccherebbe proprio `frequencies` e `runways`, che hanno più da dire.

### Il verdetto sul profilo

**Il profilo regge un SOP vero**: ogni pezzo di testo dell'originale ha trovato una chiave, e nessuna chiave
è rimasta senza spiegazione. Il conto delle sei sezioni riusate (§2) si è verificato in concreto: la scheda
di `frequencies` la disegna la pagina dalle posizioni IVAO e i blocchi portano i tre CRC/GCI/AEW che il
catalogo settori non ha; `runways` porta le coordinate delle soglie.

### Che cosa ha trovato, di nuovo, la lettura in inglese

Il giro di traduzione ha fatto **119 frasi nuove, 0 scartate**: il protettore ha retto su coordinate,
frequenze, canali TACAN e nominativi veri. Ma la pagina inglese ha mostrato due cose:

1. ⚠️ **Un `**` orfano stampato a schermo.** «• A `**`nord`**` del campo» → «• To the north`**` of the
   field». I marcatori non si proteggono — provato, il motore infila le parole dentro i tag — quindi ogni
   tanto ne perde uno. `TranslationText.RiparaGrassetto` toglie i marcatori quando non tornano: fra un
   grassetto perso e due asterischi a schermo si sceglie il grassetto perso. **Misurato: 1 voce su 246** —
   troppo poco per una riparazione retroattiva, abbastanza per non lasciarla capitare ancora.
2. ⚠️ **Le intestazioni delle tabelle erano tutte sbagliate**: «Pista» → *Track*, «Piazzale» →
   *Forecourt*, «Stand» → *Booth*, «Rilevamento» → *Detection*, «Quota» → ***Share***, «Ente» →
   *Institution*. Non sono sfumature: sono le colonne che un controllore legge per trovare il dato.
   `TitoliUfficiali.Termini` le semina come **Human** (28 voci), e funziona perché la memoria è per
   **segmento intero** — una cella di tabella *è* un segmento. Rimisurato dopo: *Facility · Callsign ·
   Airport · Navaid · Bearing · Distance · Runway · Threshold coordinates · Squadron · OAT callsign · Apron ·
   Stand · Used by · Point · Reference · Altitude*, tutte giuste.

   ⚠️ **Su una parola in mezzo a una frase non funziona**, e si vede: restano «the **camp**» per «il campo» e
   «the **cocking** and disarming positions» per «armamento/disarmo». Quella è la parte aperta del glossario
   (`lavori-aperti §Q3`), e nel frattempo il badge «traduzione non revisionata» lo dice a chi legge — che è
   il motivo per cui il badge esiste.

### 8d (trovato con la slice 10) — «nascosta» non valeva per le SOTTO-sezioni

Nascondere `qra` e `lowlevel` su Rivolto non ha nascosto niente: uscivano nel documento lo stesso. La
regola stava **solo** su `DocumentSectionsView`, cioè solo sulle sezioni **radice**; `SectionNode`, che rende
le figlie, `IsHidden` non lo guardava affatto.

⚠️ **Difetto latente come 8a, e per lo stesso motivo**: nessuna famiglia aveva sotto-sezioni nascondibili —
la vLOA ne ha due, fisse. Il vSOP militare ne ha **venti**, e su Rivolto due vanno nascoste davvero.

Chiuso propagando `IsDraft` lungo `DocumentSectionsView → SectionBody → SectionNode`. ⚠️ Il default è
`false`, cioè «vista pubblica»: chi dimentica di passarlo **nasconde**, non pubblica per sbaglio. Le due
prove nuove in `ParitaQuattroDocumentiTests` girano su tutti i profili e sono state **verificate rosse**
prima della correzione — sei profili su sei.

### 8e (il peggiore) — «MARTE» tornava «MARS», «CHI» tornava «WHO»

⚠️ **Non è una traduzione brutta: è un dato falso.** Sono nomi di punti significativi in un piano di volo, e
un pilota che pianifica *WHO* non trova niente.

**Perché il protettore non li ha fermati.** La regola sulle sigle maiuscole si applicava solo dove c'è della
prosa attorno (`eProsa = HaMinuscole(testo)`), e in una **cella** che è *solo* «MARTE» di minuscole non ce
n'è. La condizione giusta non è «ci sono minuscole» ma **«è una parola sola»**: «REVIEW CYCLE», che di
parole ne ha due, resta traducibile.

Una parola sola tutta maiuscola va nel segnaposto **vuoto** — la forma dei dati personali — e da lì
`TextProtector.SoloSegnaposti` ferma il segmento **prima** della rete: non c'è più niente da tradurre.
⚠️ Senza quel secondo passo il segmento partirebbe lo stesso, tornerebbe cambiato, il ripristino lo
scarterebbe, **e questo a ogni giro per sempre** — con un contatore «scartati» che sale e non vuol dire
niente.

**Misurato su LIPI**: 28 segmenti su 218 erano identificatori puri (callsign, stand, punti, `S1→S15`). Solo
due erano tornati sbagliati, ma adesso nessuno dei 28 parte più — e sono anche caratteri risparmiati.

✅ **Riprovato a schermo**: nella pagina inglese i punti sono `BRAVO`, `MARTE`, `NAXAV`, `CHI`, `RON`, `VIC`
— gli stessi dell'italiano, uno per uno.

⚠️ **Il prezzo, detto**: una cella che fosse una parola sola scritta in maiuscolo *e* da tradurre — «NOTE»
come intestazione — resta in italiano. È il prezzo giusto: una parola non tradotta **si vede**, un nome di
punto cambiato no.

### Che cosa resta a una persona

- Le **figure**: apron flow, area di manovra, armamento/disarmo, circuiti e porte VFR (§7.3).
- La **rilettura della trascrizione**: la prima stesura è mia, non di un controllore militare.
- Gli **altri quattordici SOP**: ognuno è un file come `SopLipi.cs`, e la parte che conta non è scriverlo —
  è rileggerlo con qualcuno che conosca il campo.
