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

## 10. Supervisione del 29 agosto 2026 — dove questa carta e il codice non dicevano la stessa cosa

Carta dell'audit: [`../history/audit-2026-08-29-vsop-militari-relazione.md`](../history/audit-2026-08-29-vsop-militari-relazione.md).
**Tredici voci, tredici chiuse** — dieci dalla lettura, tre dalla verifica a schermo. Qui restano solo le
**righe di questa carta** che l'audit ha dovuto correggere: chi la rilegge deve trovarle emendate dove sono
scritte, non solo altrove.

| § | Diceva | Va letto così |
|---|---|---|
| **§4** | «l'identità di una release è `(TargetType, TargetKey)` … è già soddisfatto dallo schema: **zero lavoro**» | Vero per le **release**, falso per le **derivate**: senza un `IFrozenSectionProvider` per `AirportMil` pubblicare non congelava nulla, e la vista leggeva lo snapshot della release **civile**. Zero lavoro sullo schema, due mezze giornate sul motore |
| **§2** | «cambio una frequenza nel catalogo → cambia in **entrambi** i documenti … alla ripubblicazione se Frozen» | Il motore lo faceva; **la casella degli impatti no**. Chi doveva ripubblicare non veniva avvisato, perché il reverse-lookup guardava solo `Airport.DocumentId` |
| **§3** | «**Etichetta prima, filtro dopo**» + «Quinto flag … su **tutti** i documenti» | L'etichetta non era stata resa da nessuna pagina (`AudienceBadge`: zero usi), e il filtro stava su due viewer su cinque. Ora il badge è nel componente condiviso e la chip su tutte le famiglie |
| **§7.1a** | i militari hanno «`DescribeOrder` **più basso di tutti** (0)» | Era un **pareggio** con `VloaReleaseTarget`, anche lui a 0. Ora i civili partono da 1 |
| **§5** | «Testata del documento civile → scheda «vSOP militare» … **E il ritorno**» | Il ritorno c'era ma **senza gate**: sui quattro campi solo militari portava a «documento non disponibile» |
| **§1c** | «`SectionProfile.AppMil` … la nascita costa zero» | Il **profilo** costa zero; il **documento** non esiste. `AppMilDocRoutes` dichiarava due pagine mai scritte: ora torna `null`, e sul file c'è l'elenco di che cosa serve quando si faranno |

⚠️ **E una cosa che questa carta non poteva sapere**: la prova a schermo della slice 10 è stata fatta su
**LIPI Rivolto perché era il SOP più corto**, e per di più **in bozza**. Rivolto è **solo militare** — nessun
documento civile — ed è precisamente il campo su cui quattro difetti su quattro sono invisibili; la bozza, per
di più, salta il ramo che risolve il bersaglio di release. La prossima volta che si prova una famiglia
**gemella** di un'altra, il caso di prova si sceglie **misto e PUBBLICATO**.

### §9 — quel che la slice 10 non poteva vedere

Il «verdetto sul profilo» del §9 resta valido: ogni pezzo di testo dei SOP ha trovato una chiave. Ma il §9 è
stato scritto guardando **una bozza**, e tre cose si vedono solo su un documento **pubblicato**:

| | |
|---|---|
| il viewer pubblico | non apriva affatto un vSOP militare pubblicato: il bersaglio di release non si risolveva sul legame militare, e la pagina diceva «nessun vSOP militare pubblicato» |
| «Frequenze ATC/CRC», «Piste», «Quote di transizione» | uscivano come **titoli vuoti**: il corpo derivato lo disegnavano solo le sezioni **radice**, e nel profilo militare quelle tre sono **figlie** |
| le venti sezioni figlie | il catalogo non le trovava affatto (`Find` si fermava al primo livello) → non «rese dalla pagina» e non «di catalogo», nemmeno nell'editor |

Dettaglio in §J dell'audit. ⚠️ Il conto delle sezioni del §9 («2 rese dalla pagina») era giusto sulla carta e
falso a schermo: `transition` è resa dalla pagina, e a schermo non rendeva niente.

## 11. 29 agosto 2026 — l'edizione giusta per il campo, e le tre colonne

Tre richieste, e una verifica che ne è venuta fuori. La prima era un difetto visibile; le altre due sono la
regola che finora stava solo nella testa di chi scriveva i documenti.

### 11a. Il vSOP militare era l'unico documento senza le due colonne laterali

`MilDocumentPage.razor` apriva un `<div class="wrap reading-cap">` e ci metteva dentro il corpo, e basta.
Gli altri quattro viewer — `AccVipiPage`, `AeroportoPage`, `AppnPage`, `VloaDocumentView` — usano tutti
`doc-layout`: indice a sinistra (`<aside class="toc">`), testo al centro, collegamenti a destra
(`<aside class="doc-rail">`).

Su un vSOP militare, che ha **nove sezioni radice e venti figlie**, voleva dire scorrere per trovare le
frequenze, e non avere il ponte verso l'edizione civile se non nella testata.

⚠️ **`reading-cap` è caduto insieme**: `.wrap:has(.doc-layout){max-width:none}` toglie già il tetto al
contenitore, e la classe restava a dichiarare un limite che non vale più. Due regole che dicono cose diverse
sullo stesso elemento sono un difetto che aspetta chi ne cambierà una.

Il rail porta le stesse voci dell'aeroporto civile con la meta cambiata: ciclo AIRAC **del documento
mostrato** (non quello di oggi), ATC online sul campo (ATIS escluso, come sul civile), e i collegamenti —
casa, elenco militare, ACC, e il ponte verso la vIPI civile **solo se esiste**.

**Presidio**: `DueColonneSuOgniDocumentoTests` chiede a tutti e cinque i viewer, per nome, di avere le tre
colonne. È un test sul **sorgente** e non un render, perché il rail compare solo sopra i 1500px e l'indice è
sticky: un test di render proverebbe il CSS, non la pagina.

### 11b. Quale edizione può esistere su quale campo — §5-bis

Due guardie **gemelle**, e vanno lette insieme perché una senza l'altra è sbagliata:

| campo | vIPI civile | vSOP militare |
|---|---|---|
| **solo militare** (Aviano, Ghedi, Decimomannu, Rivolto) | **non nasce** — non c'è traffico civile da descrivere | nasce **subito**, senza prerequisiti |
| **misto** (Pisa, Linate, Ciampino) | nasce normalmente | nasce **solo dopo** la civile |

Il perché della seconda riga: su uno scalo misto il vSOP militare dice **cosa cambia** rispetto alla vIPI
civile — quale parte del sedime, quali frequenze, quali procedure sono le altre. Senza la civile non c'è il
«rispetto a cosa».

⚠️ **Basta che la vIPI civile ESISTA, anche solo in bozza.** Pretenderla pubblicata bloccherebbe il lavoro
parallelo sulle due edizioni, che è il caso normale su uno scalo appena aperto.

⚠️ **La prima guardia blocca la NASCITA, non l'apertura.** Se una vIPI civile su un campo solo militare
esiste già — creata prima di questa regola, o perché il campo è stato marcato dopo — l'editor continua ad
aprirla: rifiutare renderebbe illeggibile un documento che c'è, e la via d'uscita (spostarne il contenuto,
poi eliminarlo) passa proprio da lì.

**Dove stanno, e perché lì.**

| guardia | dove |
|---|---|
| niente vIPI civile sui campi solo militari | `AirportEditingService.EnsureDocumentAsync` |
| niente vSOP militare prima della civile, sui misti | `EfMilitaryDocumentService.CreaAsync` |

⚠️ **Nei servizi, non nelle tendine.** Una tendina filtra, non autorizza: chi conosce l'indirizzo
dell'editor ci arriva lo stesso — è il difetto già pagato su `/services/vsop/versions` il 21 agosto 2026. E
la vIPI d'aeroporto è particolarmente esposta, perché `EnsureDocumentAsync` è chiamato dall'**apertura**
della pagina: bastava arrivare all'URL perché il documento nascesse.

⚠️ **Si legge dal DATABASE, non da `IStationResolver.Airport`.** Quella cache è `scoped`, cioè vive quanto
il **circuito**, e una guardia che decide se un documento può nascere non può rispondere su un'anagrafica
vecchia di ore. La cache resta buona per le etichette in testata, che se sbagliano mostrano un pill di
troppo. La lettura nuova è `IAirportRepository.GetMilitaryStateAsync` → `AirportMilitaryState`, e porta i
quattro campi **insieme** perché insieme si decide.

**Le strade, dopo.**

- «Nuovo documento» → scheda **Aeroporto**: la tendina dichiara «solo militare» sulla riga (l'informazione
  che cambia quale documento nascerà deve arrivare **prima** della scelta, non come sorpresa dopo). Sui
  campi solo militari il tasto **crea il vSOP e ci porta** — ed è l'unica famiglia in cui quella pagina
  scrive davvero oltre alla vLOA, perché l'editor militare non crea niente all'apertura. Sotto, una riga
  dice cosa nascerà.
- Editor della vIPI civile → tasto **«Crea il vSOP militare»** nel rail, su ogni campo con presenza
  militare. È il punto in cui si è già: l'alternativa era ricordarsi che esiste `/services/vsop/mil` e
  cercarci dentro il proprio scalo.
- `/services/vsop/mil` → sui campi misti **senza** civile il tasto «Crea» non c'è: al suo posto c'è «Prima
  la vIPI civile», che porta all'editor civile. Un tasto che fallisce sempre insegna solo a non premerlo.
- Editor civile aperto su un campo **solo militare senza civile** → **reindirizza** all'editor militare,
  invece di lasciare a schermo un errore che non dice dove andare.
- Generazione in blocco dei documenti d'aeroporto (`StructureEditingService`) → i campi solo militari si
  **saltano** con un motivo scritto, non falliscono: un'eccezione in un giro su tutti gli aeroporti di una
  ACC farebbe perdere anche il lavoro già buono.

⚠️ **Il ponte civile → militare ora si vede anche in BOZZA, ma solo allo staff.** Il vSOP militare nasce da
lì e nasce in bozza: col gate «pubblicato» chi l'aveva appena creato non avrebbe avuto modo di tornarci —
il ponte sarebbe comparso solo dopo la pubblicazione, cioè quando non serve più. Al pubblico il gate resta
«pubblicato».

### 11c. La verifica sui duplicati, su tutte e cinque le famiglie

Domanda: creando un documento che **esiste già**, si crea un doppione o si finisce sull'esistente?

| famiglia | come | esito |
|---|---|---|
| vIPI ACC | `AccDocumentService.EnsureAsync` | idempotente sul `DocumentId` del settore primario ✅ |
| APP non remotizzato | `AppDocumentService.EnsureAsync` | idem ✅ |
| vIPI d'aeroporto | `EfAirportRepository.EnsureDocumentAsync` | idempotente su `Airport.DocumentId` ✅ |
| vSOP militare | `EfMilitaryDocumentService.CreaAsync` | ritorna l'id esistente ✅ |
| **vLOA** | `EditingService.CreateDocumentAsync` | rifiuta e offre «Apri esistente» ✅ — **ma vedi sotto** |

**Il buco trovato.** Le due porte che creano una vLOA facevano la stessa domanda in due modi diversi:

- «Nuovo documento» confronta la **coppia di ACC** (`FindVloaIdByPairAsync`), e lascia scegliere
  **qualunque** settore d'area come Home;
- la generazione da «ACC confinanti» (`EfNeighbourRepository.MaterializeAndCreateVloaAsync`) confrontava i
  **SectorId**, e il suo Home lo sceglie da sé — la radice dell'albero ACC.

Su una ACC con più settori d'area bastava che la prima vLOA fosse nata dall'altra porta su un settore
diverso: il confronto per `SectorId` non la trovava, e nasceva la **seconda vLOA sulla stessa coppia**.

Da lì in poi le due non si vedono più fra loro: `FindVloaIdByPairAsync` — che è come l'editor e il pubblico
trovano la vLOA di una coppia — fa `FirstOrDefault` per codice ACC e ne apre una **senza un criterio**.
L'altra resta invisibile pur potendo avere release pubblicate. È esattamente il difetto che il commento in
`EditingService` descriveva… per l'altra porta.

Ora entrambe confrontano la **coppia di ACC**. ⚠️ La **direzione** continua a contare: LIRR→DTTC e
DTTC→LIRR sono due vLOA legittime, una per lato, e c'era già un test a difenderlo — passare da «se ne creano
infinite» a «la seconda non si crea mai» sarebbe stato un difetto peggiore.

**Presidio**: `VloaUnaPerCoppiaTests.La_generazione_da_confinanti_RIUSA_la_vLOA_creata_su_un_ALTRO_settore_dello_stesso_ACC`.
Il test verifica **anche** che il settore scelto sia diverso dalla radice, invece di sperarci: con lo stesso
settore non proverebbe niente e resterebbe verde per sempre.

### 11d. Trappole incontrate

| | |
|---|---|
| `Lingua()` sceglie sulla **cultura corrente**, e nella suite è l'**inglese** | un'asserzione sulla parola italiana del messaggio passava solo per caso di ambiente |
| `AeroportoPage` è **due schermate in una** | l'elenco degli aeroporti un `reading-cap` ce l'ha, a ragione: il test sul tetto di lettura guarda il contenitore **più vicino** a `doc-layout`, non tutto il file |
| l'indice a sinistra usa `s.Id` come ancora | è il default di `DocumentSectionsView`: se le due si scostano, i link dell'indice non fanno niente — **senza errori** |

### 11e. Il ponte al civile, e i vSOP orfani nati prima della regola (29 agosto, dopo la prova a schermo)

La prova dal vivo su `/services/vsop/limm/mil?icao=LIML` ha trovato due cose, e la seconda spiega la prima.

**Il ponte era gated su «pubblicata», e doveva esserlo su «esiste».** `HasPublishedCivilAsync` è diventato
`GetCivilEditionAsync` → `CivilEdition(Esiste, Pubblicata, SoloMilitare)`: al **pubblico** il ponte si accende
solo se la civile è pubblicata (un collegamento a un documento invisibile è un vicolo cieco), allo **staff**
anche se è solo una **bozza** — la civile può essere appena nata, e un ponte che compare solo dopo la
pubblicazione compare quando non serve più. È la stessa correzione già fatta nel verso opposto in §11a.

⚠️ **Tre risposte e non un booleano**, perché servono tutte e tre e vanno lette nello stesso istante: le due
sopra più `SoloMilitare`, che dice se l'assenza del civile è **la regola** o **un difetto**. Su un ICAO
sconosciuto `SoloMilitare` torna **falso**: dire «a norma» di un campo di cui non si sa niente sarebbe
rispondere a una domanda che non è stata posta.

**E il difetto vero: su LIML il ponte mancava perché la vIPI civile non c'è affatto.** Misurato in archivio:

| campo | presenza mil. | solo mil. | vIPI civile | vSOP militare |
|---|---|---|---|---|
| LIBG Grottaglie | sì | **sì** | — | #24, pubblicato |
| LIBN Lecce Galatina | sì | **sì** | — | #27, bozza |
| **LIML Linate** | sì | **no** | **manca** | **#25, PUBBLICATO** |
| LIMN Cameri | sì | no | #28 | #29 |

LIML è uno scalo **misto** con un vSOP militare **pubblicato** e nessuna vIPI civile: esattamente ciò che
§11b vieta. Il documento è del **28 agosto**, cioè di prima della guardia.

⚠️ **Una guardia nuova non ripara il passato, e tacere lascia il difetto dov'è.** Il caso ora si **dice**, a
chi può rimediare e con il tasto che rimedia: un callout sul viewer militare (`MancaCivile`, solo per lo
staff) e una pill rossa sulla riga dell'elenco nazionale, entrambi collegati all'editor della vIPI civile —
che è anche ciò che la fa nascere. Non si crea niente da soli: creare un documento al posto di una persona è
la stessa categoria di errore che si è appena chiusa.

⚠️ **Quello che NON è un difetto**: LIBG e LIBN sono marcati `IsMilitaryOnly`, quindi la civile non deve
esistere e la guardia li lascia passare — è la seconda metà della regola. Se un campo così ha in realtà
traffico civile, la correzione è togliergli la spunta «solo militare» in Struttura, e da quel momento
l'avviso comparirà anche su di lui.

### 11f. Il filtro «Tipo» di `/services/vsop/versions` non aveva i militari

Trovato a schermo il 29 agosto. Quando è nata la famiglia, l'elenco imparò a **mostrare** il documento
militare — icona `shield`, etichetta, riga — ma il **menu Tipo** restò ai quattro tipi civili: i vSOP
militari si vedevano e non si potevano **isolare**, in una pagina che serve proprio a isolare.

⚠️ È la stessa forma dei difetti di §10: *il documento era agganciato al motore di lettura e non a quello di
governo*. Qui in scala ridotta — mostrare è lettura, filtrare è governo — e nessun compilatore lega un
elenco di filtri all'enum che filtra. A schermo la mancanza non salta all'occhio: un menu con cinque voci
sembra completo quanto uno con sei.

⚠️ **`AppMil` NON è stato aggiunto, ed è la cosa giusta**: quel documento non esiste, nessuna porta lo crea e
le sue rotte tornano `null` (§1c). Una voce di filtro che non può che dare zero righe è la stessa promessa
falsa che `MilDocRoutes` ha già pagato una volta.

**Presidio**: `FiltroPerTipoCompletoTests` — per ogni descrittore di rotta, se ha un `PublicUrl` allora deve
comparire fra i filtri, e se torna `null` allora **non** deve comparire. Chi «esiste davvero» lo dice il
descrittore, non un elenco scritto nel test. L'etichetta del filtro viene da `KindLabel`: due elenchi che
nominano le stesse cose in due modi diversi si scostano al primo che si tocca.

### 11g. Il quarto riquadro sulla pagina di una ACC

`/services/vsop/{acc}` offriva tre famiglie — Aeroporti, APP, vLOA — e i vSOP militari di quei campi non
c'erano: per arrivarci bisognava tornare all'ingresso e passare dall'elenco nazionale. Ora c'è la quarta
scheda, con lo **stesso gate** delle altre tre (release effettiva e non nascosto) e sullo **stesso** elenco
`managed`, quindi **senza una query in più**.

⚠️ **Compare solo se ce n'è almeno uno.** Le altre tre restano anche vuote perché sono le famiglie che *ogni*
ACC ha; l'edizione militare no — su un ACC senza campi militari una scheda «nessun vSOP militare» direbbe a
tutti i lettori che manca qualcosa che non deve esserci.

⚠️ **Un campo misto compare in DUE schede, e non è un doppione**: sono due documenti dello stesso scalo, con
release e cicli AIRAC indipendenti. La vIPI civile si raggiunge da «Aeroporti», il vSOP militare dalla scheda
nuova — la stessa separazione che hanno i due elenchi.

⚠️ **«Vedi tutti» porta all'elenco NAZIONALE**, non a uno per-ACC: quello per-ACC non esiste e non deve
esistere (§5 — un pilota cerca «Ghedi», non «LIMM»). Quello nazionale è comunque raggruppato per ACC, quindi
chi arriva di lì ritrova il suo gruppo.

⚠️ **Il filtro è `IsHidden`, non `IsPublic`.** Il secondo pretende almeno un settore, e un campo solo
militare può benissimo non averne: con `IsPublic` questa scheda avrebbe nascosto documenti che l'elenco
nazionale mostra. Verificato in archivio: LIMS ha **zero settori**.

Il nome dello scalo viene da `data.Airports` e non dal titolo del documento — quello è «vSOP MIL — LIML
MIlano Linate», e stamparlo in una riga che ha già la sua targa ICAO ripeterebbe l'ICAO due volte.

**Colore**: `--cat-mil`, verde oliva. Non `--cat-vloa` schiarito: due verdi vicini si leggono come lo stesso
verde, che è il difetto che quell'insieme di colori deve evitare per mestiere. ⚠️ L'inchiostro va definito in
**tutti e tre** i blocchi di tema — chiaro, `prefers-color-scheme: dark` **e** `[data-theme="dark"]` — o metà
dei lettori scuri prende quello chiaro.

### 11h. La card su `/services` — la decisione di §5 è stata ribaltata, e come

⚠️ **§5 aveva deciso il contrario**: *«niente card su `/services`: i vSOP militari non sono un servizio, sono
una parte della vSOP, e `ServicesHomeTests` lo pretende»*. Il 29 agosto 2026 il committente ha chiesto lo
stesso di metterla lì, e ha ragione sul fatto che conta: **chi cerca un vSOP militare non sa che deve prima
entrare nella documentazione civile per trovarlo**. La regola di §5 era giusta sull'architettura e sbagliata
sul lettore.

Le due cose stanno insieme perché il collegamento è **marcato per quello che è**: `a.choice.shortcut` dice
che quella scheda **non è un servizio** ma una porta dentro a uno, e la rete che pretende un solo segmento
sotto `/services` continua a valere su tutte le altre.

⚠️ **Marcare invece di allargare.** Senza quel segno, `Ogni_servizio_e_figlio_diretto_di_services` sarebbe
stato semplicemente **cancellato** per far entrare un'eccezione — e da lì in poi nessuno avrebbe più notato
un servizio annidato per sbaglio. Una regola che si toglie per far passare un caso non protegge più
nemmeno gli altri. Due reti nuove la tengono in piedi: la scorciatoia deve comunque stare **dentro**
`/services/` (l'hub non è un elenco di segnalibri per il resto del sito), e le scorciatoie si **contano** —
se un giorno fossero metà dell'hub, la regola resterebbe verde senza provare più niente.

**L'ordine dell'hub**, chiesto dal committente e ora presidiato da un test: vSOP civili → **vSOP militari** →
statistiche ATC → Aurora Profile Swapper → *riga «Staff di divisione»* → convertitore di coordinate. Non è
alfabetico né storico: va **dal documento allo strumento**. Chi arriva la prima volta cerca un documento.

⚠️ **La riga di sezione dice anche a CHI appartiene quel che sta sotto.** Prima la scheda del convertitore
stava nella stessa griglia delle altre e sembrava uno strumento come gli altri: chi la vedeva non aveva modo
di sapere che gli altri non la vedono. Il cancello resta in **due sedi** — la sezione si nasconde *e* la
pagina rifiuta chi scrive l'indirizzo a mano.

⚠️ Sotto un titolo di sezione le schede diventano `<h3>`: il tag dice la **struttura**, `.h-card` porta la
**misura**, quindi il disegno non cambia.

## 12. 29 agosto 2026, notte — le tabelle del vSOP militare (§X2)

Otto richieste del committente. Sette diventano **tabelle** dentro le sezioni che oggi sono prosa libera, una
è l'indice; l'ottava — il BOAT — è stata **ritirata dal committente** dopo un ricontrollo («è più complicato
di quanto mi aspettassi»), e si riprende separatamente.

**Il filo**: le sezioni del SOP che *contengono un elenco* — radioassistenze, alternati, nominativi,
parcheggi — oggi sono paragrafi in cui l'elenco è scritto a mano ogni volta, e ogni campo lo scrive a modo
suo. Una tabella con le colonne giuste non è impaginazione: è la differenza fra un dato e una frase che lo
contiene.

### 12a. Il payload di una sezione non scendeva nei figli — **chiuso**

⚠️ **Il blocco tecnico che stava davanti a tutto, e non si vedeva dalla carta.**
`EfEditingRepository.GetSectionBlockJsonAsync` / `SaveSectionBlockJsonAsync` — le due porte da cui passa il
contenuto strutturato di una sezione (le configurazioni di un APP, la selezione delle aree) — cercavano la
sezione con `ParentSectionId == null`, cioè **solo fra le radici**.

Nel profilo `AirportMil` **venti sezioni su ventisei sono figlie**, e ci stanno dentro *tutte* le tabelle
chieste: «Radioassistenze» e «Nominativi» sotto «Dati generali», «Parcheggi» sotto «Procedure di terra».
Su quelle il salvataggio sollevava **«Sezione assente»** e la lettura tornava `null`.

⚠️ È la **terza volta** che la stessa assunzione si presenta con un vestito diverso: `SectionCatalog.Find`
non scendeva nei figli (§V), il corpo derivato lo disegnavano solo le radici (§V, verifica a schermo), e ora
il payload. **La regola da portarsi via**: quando una famiglia introduce un annidamento che le altre non
hanno, non basta correggere il punto che si è rotto — vanno cercate *tutte* le query che dicono
`ParentSectionId == null` o `Depth == 0`, perché sono la stessa assunzione scritta in posti diversi.

**E un secondo difetto, latente, trovato mentre si chiudeva il primo.** Il payload viveva per convenzione nel
`BodyJson` del **primo blocco** della sezione — regola scritta in cinque file diversi. Regge finché la
sezione ha un blocco solo, ed è il caso di tutte le derivate delle altre famiglie. **Non regge sulle sezioni
militari**, che `MilSopLoader` riempie di prosa: lì il payload convive con i paragrafi, e «il primo» diventa
«quello che oggi sta in cima». Chi avesse scritto una premessa sopra la tabella non avrebbe visto un errore —
avrebbe visto **la tabella svuotarsi**, e il salvataggio successivo avrebbe scritto il JSON *dentro il blocco
di prosa*.

La regola ora è la stessa in lettura e in scrittura, ed è una domanda sola:

- **lettura** (`SectionPayload.Read`, nuovo, unico punto per i cinque chiamanti): il primo blocco che un
  payload **ce l'ha**. Un blocco di prosa non ne ha, quindi i due non si confondono, e su una sezione con un
  blocco solo la risposta è identica a prima.
- **scrittura**: quel blocco se esiste; altrimenti un blocco **senza prosa** — il segnaposto che
  `AggiungiPlaceholderSeServe` mette alla nascita sulle sezioni rese dalla pagina, e riusarlo tiene il conto
  dei blocchi identico a prima su tutte le famiglie; altrimenti se ne crea uno **in coda**.
  ⚠️ *In coda*, non a `Order = 1`: su una sezione che ha già la prosa dei SOP quell'ordine è occupato, e due
  blocchi con lo stesso ordine si dispongono come capita.

⚠️ **Un blocco di prosa non si tocca mai.** È l'invariante che rende sicuro tutto il resto della sezione 12:
le sette tabelle nuove abitano le stesse sezioni del testo già caricato dai quindici PDF.

Reti: `SectionBlockJson_Trova_Anche_Le_Sezioni_Annidate` — che verifica anche che la sezione sia davvero a
profondità 1, così il test non diventa verde per il motivo sbagliato il giorno che il profilo cambia — e
`SectionBlockJson_Non_Tocca_La_Prosa_E_Non_Si_Perde_Sotto_Un_Paragrafo`. Verdi: 989 + 1 555 + 852 su net8
(Infrastructure, Application, Ui).

### 12b. Le decisioni del committente, prese prima di scrivere

1. **Le radioassistenze diventano un'anagrafica condivisa.** Quel che si scrive nella tabella di un campo si
   memorizza, e quella radioassistenza esce uguale ovunque. ⚠️ E la sorgente quel dato **ce l'ha già**: il
   parser del sectorfile legge `AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;54Y;` e **butta via frequenza e
   canale**, tenendo solo nome e coordinate. Misurato il 29 agosto sul repo `ivao-italy/it-aurora-sector`:
   **128 VOR, 30 NDB, e 26 col canale** — che sono i VORTAC/TACAN, cioè proprio i militari. L'esempio che il
   committente ha scritto a mano, `MNL - CH 99Y (115.25)`, è alla lettera la riga 85 di `itvor.vor`.
   ⚠️ **ILS e TACAN puro non sono nel sectorfile**: quelle righe saranno sempre «nostre».
2. **La fonte vince sempre.** Un campo che viene dalla sorgente **non è modificabile**: il tentativo di
   correzione a mano non va a buon fine. La provenienza è **per campo**, non per riga — su MNL frequenza e
   canale sono della fonte, su un ILS sono nostri, e una colonna sola sulla riga mentirebbe su metà dei
   campi. ⚠️ Vale la regola già pagata cara: **l'assenza non cancella** — un import che trova il campo vuoto
   non scrive il vuoto sopra il nostro.
3. **Identità di una radioassistenza: `codice + tipo`.** Due `DEC`, uno VOR e uno NDB, sono due righe.
4. **Concorrenza: vince chi arriva per ultimo**, e tutto finisce nel registro d'audit. ⚠️ Con due
   precisazioni che il committente ha accettato: si scrivono **i campi toccati, non la riga** (altrimenti chi
   cambia la frequenza e chi cambia le coordinate si sovrascrivono a vicenda *senza aver toccato la stessa
   cosa*, e il registro direbbe una cosa falsa), e il registro porta il valore **vecchio e nuovo** — «Tizio ha
   modificato MNL» non permette né di accorgersi né di rimettere a posto.
   ⚠️ **Il lock del documento qui non protegge niente**: due persone su due SOP diversi hanno ognuna il lock
   del proprio documento e scrivono sulla stessa radioassistenza. Il lock è del documento, l'anagrafica è di
   tutti.
5. **Rilevamento e distanza degli alternati si scrivono a mano**, non si calcolano dalle coordinate: sono i
   valori del SOP, e nessuno sa come li abbiano ricavati. Calcolarli darebbe numeri veri e **diversi dal PDF**.

### 12c. Le coordinate delle soglie pista ci sono, e non le memorizziamo

Verificato **sul filo** il 29 agosto, `GET /v2/airports/LIPI/runways`:

```json
{"id":11609,"airportIcao":"LIPI","runway":"RW06","length":8383,"bearing":57,
 "latitude":45.9735305556,"longitude":13.0350638889,"elevation":162,"width":44}
```

Una riga **per soglia**, con latitudine, longitudine **ed elevazione**. `RunwayDto` ne mappa quattro campi su
otto: il dato arriva e si perde in traduzione. Servono una migrazione (le in coda al cutover MariaDB
diventano **ventiquattro**) e un `SaveRunwaysAsync` che le **preservi** nel merge editoriale.

⚠️ **L'elevazione si prende adesso**: viaggia nella stessa risposta, i SOP la stampano, e prenderla dopo
sarebbe una seconda migrazione per un campo che era già nella busta.

⚠️ **La tabella nasce vuota su tutti i campi.** L'import piste è **per-aeroporto e non automatico**: finché
non si ri-importa da IVAO, le soglie non ci sono. Va nel piano, non scoperto a schermo.

### 12d. Ordine dei lavori

`S0` payload nei figli (**fatto**) → `S1` navigazione con le sotto-sezioni → `S2a` anagrafica radioassistenze
→ `S2` Radioassistenze → `S3` Aeroporti alternati → `S4` coordinate soglia → `S5`/`S6`/`S7` nominativi,
parcheggi, attività delle aree → reti e lingua → verifica a schermo.

⚠️ **Il caso di prova è LIMN Cameri**: campo **misto** e nato nell'ordine giusto. La regola l'abbiamo già
pagata due volte — su Rivolto, che è solo militare, metà dei difetti è invisibile.
