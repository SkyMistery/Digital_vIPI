# Audit 29 agosto 2026 — i vSOP militari e la loro relazione col civile 🟢

> Supervisione della carta [`../feature/2026-08-27-vsop-militari.md`](../feature/2026-08-27-vsop-militari.md)
> **contro il codice**, fatta da fuori: cosa la carta prometteva, cosa il codice fa, e dove le due cose non
> dicono la stessa cosa. **Tredici voci, tredici chiuse.** Suite: **3 841** test su net8, **nove progetti su
> nove** (E2E compresi) e altrettanti su net10; build Release `--no-incremental` a zero avvisi.
>
> ⚠️ **Dieci le ha trovate la lettura, TRE lo schermo** — e le tre dello schermo sono le peggiori: un vSOP
> militare pubblicato non si apriva affatto, e su quello che si apriva mancavano tre tabelle su tre. Vedi §J.
> È la conferma del punto §0: il caso di prova sbagliato nasconde tutto.

## 0. Il filo — un documento agganciato alla lettura e non al governo

Le prime quattro voci sono la **stessa** dimenticanza, e vale la pena dirla prima dell'elenco.

Il documento militare era stato agganciato al **motore di lettura** — viewer, editor, catalogo delle
sezioni, traduzione, descrittori di release — e **non** al **motore di governo**: elenco unificato dei
documenti, casella degli impatti, rivelatore di drift, protezioni all'eliminazione, congelamento della
release. La carta descrive minuziosamente il primo e dà per scontato il secondo; i test seguivano la carta,
quindi erano verdi.

Il segno misurabile era uno: **`MilDocumentId` — il legame fra le due edizioni — aveva quattro lettori in
tutto il repository.** `DocumentId` ne ha decine. Ogni voce da A a D è un posto in cui qualcuno leggeva
`DocumentId` e nessuno leggeva il gemello.

⚠️ **E la prova a schermo del 28 non poteva accorgersene**: è stata fatta su **LIPI Rivolto, che è solo
militare**. È esattamente il campo in cui il congelamento sbagliato degrada in «live» senza rumore, non
c'è un documento civile con cui confondersi, e il campo non ha ancora una release da elencare. Un caso di
prova scelto perché era **il più corto** ha nascosto quattro difetti su quattro.

---

## A. Il congelamento della release non esisteva, per l'edizione militare

Due metà rotte, ognuna sufficiente da sola.

**Non si catturava niente alla pubblicazione.** `AddVipiApplication` registrava quattro
`IFrozenSectionProvider` — vLOA, APP, ACC, aeroporto. Nessuno per `AirportMil`. E
`FrozenSectionRegistry.CaptureAsync` su un tipo non registrato torna `Empty` **in silenzio**: non è un
errore, è un dizionario vuoto.

**E la lettura puntava al gemello civile.** `AirportViewDerivationService.ResolveForViewAsync` aveva
`ReleaseTargetType.Airport` **cablato**, e `MilDocumentPage` la chiamava con `_useFrozen: true` nella vista
pubblica. Quindi:

| campo | che cosa mostrava il vSOP militare pubblico |
|---|---|
| **misto** (Pisa — ed è fra i quindici SOP veri) | la fotografia della release **civile**, timbrata al ciclo AIRAC civile. Ripubblicare il militare non la cambiava; ripubblicare il **civile** la cambiava sotto il militare |
| **solo militare** (Rivolto, Aviano, Ghedi, Decimomannu) | nessuna release civile da leggere → si ricadeva **sempre live**, e il congelamento era un no-op permanente |

⚠️ **E l'editor prometteva il contrario**: `MilEditorPage` espone il toggle Live/Frozen su `frequencies`,
`runways` e `transition` (sono derivate e non sempre-live, quindi `IsRenderModeToggleable` è vero). L'editore
lo lasciava su Frozen — il default alla nascita — convinto di aver fissato la tabella.

**Chiuso così.** Il provider prende la famiglia dal costruttore e si registra **due volte**: un motore di
proiezione solo, due catture. Riscriverlo in una classe militare a parte avrebbe creato due mappe destinate
a divergere, con una delle due sbagliata senza che nessuno se ne accorga. E
`ResolveForViewAsync` ha ora un parametro `edizione` **obbligatorio e senza default**: un default sarebbe
«civile», cioè la risposta giusta per il chiamante che c'era e sbagliata in silenzio per quello nuovo — la
forma esatta del difetto.

Prove: `AirportFrozenAndViewTests` (cinque nuove, fra cui una che mette per iscritto che un tipo **senza**
provider non protesta) e `RegistrazioniPerFamigliaTests`, che conta le famiglie che congelano.

---

## B. Il documento militare era invisibile ai tre elenchi generici

`AirportMilReleaseTarget.TryDescribe` legge `doc.MilAirport?.Icao` e risponde `false` se è vuoto — con tanto
di avviso nel file: *«⚠️ Richiede `.Include(d => d.MilAirport)` a monte»*. Quell'Include non era stato messo
da nessuna parte:

| repository | `.Include(d => d.Airport)` | `.Include(d => d.MilAirport)` |
|---|---|---|
| `EfDocumentAdminRepository.ListAsync` | sì | **no** |
| `EfChangesRepository.ListChangedAsync` | sì | **no** |
| `EfSearchRepository.SearchAsync` | sì | **no** |

Tutte e tre `AsNoTracking()`: nessun fixup di navigazione poteva ripararlo per caso, quindi il difetto era
**deterministico**. Il descrittore militare rifiutava, i quattro civili rifiutavano per `Edition`, e
`Describe` tornava `null`: il documento non finiva in errore, **non finiva proprio**.

Sparivano con lui: la **ricerca** del sito, le **«Novità»** del ciclo, e — attraverso l'elenco unificato —
`VersioniPage`, `ReleasePreviewPage`, la tendina degli **incarichi**, l'anteprima dell'**eliminazione** e il
rivelatore di **drift** (`ImpactDriftUseCase`), che gira proprio su `IDocumentAdminRepository`. Nel frattempo
il documento restava **pubblicabile** dal suo editor, perché `ReleasePanel` prende bersaglio e chiave diretti:
si poteva pubblicare qualcosa che nessuna pagina di governo elencava.

⚠️ **Perché la suite non lo vedeva**: l'aiutante condiviso `TestReleaseTargets` era cablato sui **quattro**
descrittori civili. Un aiutante di test che non conosce una famiglia è **una rete col buco disegnato dentro**.
Ora sono sei, e con essi tutta la suite di Infrastructure.

⚠️ **`MilSectors` NON è stato incluso**, ed è una scelta scritta: nessuna porta crea documenti `AppMil`
(voce F), e un include di **collezione** su tre query calde si paga. Va aggiunto insieme alla pagina.

Prova: `DocumentAdminRepositoryTests.IL_DOCUMENTO_MILITARE_NON_SPARISCE_DALL_ELENCO`, **verificata rossa**
togliendo l'Include.

---

## C. Gli impatti a monte non arrivavano al militare

È la promessa esplicita del §2 della carta: *«cambio una frequenza nel catalogo → la tabella derivata cambia
in entrambi i documenti — alla vista se Live, alla ripubblicazione se Frozen»*. Il **motore** la manteneva:
le due edizioni derivano dalla stessa anagrafica. Ma il meccanismo che dice all'editore «ripubblica» è la
casella degli impatti, e `EfDocumentImpactRepository` risolveva i documenti **solo** per `Sector.DocumentId`
e `Airport.DocumentId`.

- Cambio una posizione su **Pisa** → impatto aperto sulla vIPI civile, **niente** sul SOP militare che deriva
  la stessa tabella frequenze.
- Su un campo **solo militare** il lookup non tornava una riga sbagliata: tornava **zero**.

⚠️ Un'eccezione funzionava **per caso**: gli impatti d'**area** cercano i documenti per
`DocumentSection.SectionKey == "regulated"`, quindi il militare ci rientrava già. Cioè: le aree di lavoro
segnalavano, le frequenze no — il tipo di asimmetria che nessuno indovina leggendo il codice.

Chiuso aggiungendo il gemello nei due lookup per ICAO e nel «vicinato» (dove `a.DocumentId != null` escludeva
in blocco i campi solo militari). Prove: tre in `DocumentImpactLookupTests`, **verificate rosse**, fra cui una
che chiede che allargare il lookup **non** allarghi il rumore — il difetto da cui nacque la riscrittura del
25 agosto.

---

## D. Le protezioni all'eliminazione non guardavano il legame militare

`DeletionRules.PerAeroporto` bloccava se `f.DocumentId is not null`, e `AirportFacts` non portava affatto
`MilDocumentId`. Su un campo **solo militare** `DocumentId` è nullo: **l'aeroporto si eliminava**, e il vSOP
militare restava orfano — senza `MilAirport` nessun descrittore lo riconosce più — con le sue righe in
`DocReleases` ancora in tabella sull'ICAO. E l'anteprima, che è il cuore della carta sull'eliminazione, non lo
nominava nemmeno.

Chiuso con un secondo blocco e i due campi in `AirportFacts`. Prove: due, fra cui quella dello scalo **misto**,
che deve trattenere **due** documenti con **due** nomi.

---

## E. «Etichetta prima, filtro dopo»: l'etichetta non c'era, il filtro era su 2 viewer su 5

Il §3 della carta dichiara il filtro pilota/ATC **su tutti i documenti**, e mette l'etichetta **prima** del
filtro. Nel codice:

- **`AudienceBadge` non era usato da nessuna pagina.** Gli unici riferimenti nel repository erano i suoi
  stessi test. La classe `aud-badge` non aveva nemmeno una regola CSS — e `aud-chip` neppure.
- **La chip e il filtro** c'erano su `MilDocumentPage` e sulla vLOA. Mancavano su aeroporto, vIPI ACC e APP.
- **Ma il selettore nell'editor è quello condiviso**: si poteva marcare «ATC» una sezione della vIPI
  d'aeroporto, il valore veniva salvato, propagato fino a `SectionView.Audience`… e il viewer lo ignorava.

Chiuso in tre mosse:

1. il badge nel **componente condiviso** (`DocumentSectionsView` **e** `SectionNode`), quindi in tutte le
   famiglie e anche sulle **sotto-sezioni** — nel vSOP militare venti sezioni su ventisei sono figlie;
2. la chip e il filtro su aeroporto, ACC e APP;
3. il CSS mancante, e `.aud-chip` fra gli elementi che **non si stampano** (il badge invece sì: è contenuto).

⚠️ La vIPI **ACC** è l'unica famiglia a blocchi: le sue sezioni sono `AccBlockSection`, non `SectionView`, e
quel record il destinatario non lo portava affatto. Ora lo porta, e `AudienceFilter` espone due porte
pubbliche — `Mostra` e `FiltraFigli` — perché la regola resti **una**: riscriverla nella pagina sarebbe stata
la quinta copia di una condizione di tre righe, e la prima a divergere.

Prove: tre nuove in `ParitaQuattroDocumentiTests`, che girano su **tutti e sei** i profili (è precisamente
ciò che quella classe prometteva a chi aggiungesse una famiglia), più le porte nuove in `AudienceFilterTests`
e il passaggio del flag in `AccDocumentAssemblerTests`.

---

## F. `AppMil` era dichiarato e non esisteva

`AppMilDocRoutes` restituiva `/services/vsop/{acc}/mil/apps` e `.../mil/apps/editor`: **nessuna delle due
pagine esiste**. Innocuo oggi — nessun servizio crea un documento `AppMil` — ma è **la stessa forma del
difetto che la carta stessa racconta al §6**, quando `MilDocRoutes` dichiarava un `EditorUrl` verso una
pagina mai scritta, lasciata in piedi sull'altra metà.

⚠️ **Non si è scritta la pagina**: il contenuto vero dell'APP militare è una decisione del committente che
non è stata presa, e inventarla qui sarebbe stato allargare il lavoro invece di correggerlo. Si è tolta **la
bugia**: le quattro rotte tornano `null`, che il contratto prevede e i chiamanti già trattano (la vLOA lo fa
da sempre quando manca il vicino). Il descrittore resta registrato, così `DocRoutes.For(AppMil)` non esplode.

Sul file è scritto **che cosa serve il giorno in cui le pagine si scriveranno**: le quattro rotte,
l'`.Include(d => d.MilSectors)` nei tre elenchi, un `IFrozenSectionProvider` per `AppMil`, e la voce nel
conteggio delle famiglie.

E soprattutto: **una rete generale**. `RotteDeiDocumentiEsistonoTests` prende ogni indirizzo non nullo di
ogni `IDocKindRoutes` e chiede che una pagina risponda a quel percorso. I descrittori vivono in
`Vipi.Application` e le pagine in `Vipi.Ui`: nessun compilatore lega le due cose, e un indirizzo sbagliato si
vede solo cliccandoci sopra. È il test che sarebbe servito il 28.

---

## G. Le quattro minori

1. **`DescribeOrder` non era «più basso di tutti».** I due militari stavano a 0, ma anche
   `VloaReleaseTarget`: un **pareggio**, non una precedenza. Non faceva danno — la vLOA rifiuta per
   `doc.Type` prima di guardare l'edizione — ma la prima delle due mani della difesa §7.1 era più debole di
   come la carta la racconta, e il test lo confrontava **solo col catch-all**. I civili partono da 1, nello
   stesso ordine relativo; il test ora confronta i militari con **tutti** i civili.
2. **Il ponte fra le edizioni era gated in un verso solo.** Civile → militare sì; militare → civile
   **sempre**, e sui campi solo militari — quelli che un vSOP ce l'hanno davvero — portava al callout
   «documento non disponibile». Ora c'è `HasPublishedCivilAsync`, gemello di `HasPublishedAsync`, e il gate
   è scritto una volta per tutti e due i versi.
3. **`VersioniPage` stampava «AirportMil».** Il ramo finale del `switch` dà il nome dell'enum: ora i tipi
   militari hanno un nome scritto per un lettore e lo **scudo** della card su `/services/vsop`.
4. **La ricerca non aveva un taglio militare.** I documenti entravano solo in «Tutto» ed erano esclusi da
   «Aeroporti» (quel taglio guarda il tipo di release, non l'ICAO). Aggiunto `SearchScope.Mil`, **in coda**
   all'enum.

E un commento stantio: il catalogo diceva ancora «Ventiquattro sezioni». Sono ventisei, e a contarle è un
test, non un commento.

---

## H. Che cosa questa correzione lascia scritto per la prossima famiglia

Tre cose, e nessuna è sui vSOP militari.

1. **Un registro risolto per tipo che non trova il tipo risponde vuoto, non sbaglia.** Vale per
   `FrozenSectionRegistry` e per chiunque lo imiti. L'unica difesa è **contare le famiglie**, ed è quello che
   fa `RegistrazioniPerFamigliaTests`.
2. **Un aiutante di test cablato su un elenco è un elenco da aggiornare.** `TestReleaseTargets` conosceva
   quattro descrittori su sei, e per questo l'intera suite di Infrastructure non ha mai visto un documento
   militare attraversare un elenco generico.
3. **Un legame nuovo va cercato con `grep`, non con la memoria.** Il difetto A–D si sarebbe visto in un
   minuto contando i lettori di `MilDocumentId` contro quelli di `DocumentId`: **quattro contro decine**. È
   la stessa domanda della «propagazione» del `FEATURE-PROCESS`, fatta su una **colonna** invece che su un
   metodo.

## I. Quel che era a posto, e va detto

`Edition` in un posto solo con il default `Civil` **esplicito** in tutt'e due le migrazioni; le colonne
gemelle con l'indice unico dalla parte giusta e le FK a `SetNull`; i valori nuovi **in coda** all'enum con un
test che asserisce gli ordinali uno per uno; la difesa a due mani contro il catch-all, con il controllo su
`Edition` come prima riga di **tutti e quattro** i descrittori civili e il test che lo pretende; le
ventisei sezioni con l'annidamento vero e `AppMil` che **rimanda** al profilo civile invece di ricopiarlo; la
nascita in italiano; il gate pubblico nel **servizio** e non nella pagina. La parte che la carta descrive è
fatta come la carta la descrive.

---

## J. Le tre voci che ha trovato lo SCHERMO (29 agosto, sera)

Le prime dieci le ha trovate la lettura. Poi si è fatta la verifica di §V1 — pubblicare **tutti e due** i
documenti di un campo **misto** e guardarli — e sono uscite altre tre voci. Sono le peggiori del giro, e
nessuna era visibile leggendo: tutte e tre stanno nell'incontro fra due cose che, da sole, funzionavano.

Scenario: **LIML Linate**, scalo civile con presenza militare. vSOP militare già pubblicato il 28 agosto;
vIPI civile creata e pubblicata adesso; poi una frequenza cambiata nel catalogo e **solo il civile**
ripubblicato.

### J1. Un vSOP militare PUBBLICATO diceva «nessun vSOP militare pubblicato»

Il difetto più grosso di tutto l'audit, e il più facile da non vedere.

`EfContentRepository.ResolveReleaseTargetAsync` è una **quinta** risoluzione del bersaglio di release scritta
a mano — necessaria, perché lì il documento arriva da una query senza `Include` e i descrittori decidono
guardando le navigazioni. Guardava `Sector.DocumentId` e poi `Airport.DocumentId`: per un documento militare
tutt'e due rispondono `null`, perché il suo legame è `MilDocumentId`. Bersaglio sconosciuto → lo snapshot
della release **non veniva nemmeno cercato** → il percorso pubblico concludeva «nessuna release effettiva» e
tornava `null`.

⚠️ **Quindi il viewer pubblico dei vSOP militari non ha mai funzionato su un documento pubblicato.** Non si
era visto perché l'unico documento militare mai guardato a schermo era **LIPI Rivolto in BOZZA**, e la bozza
prende l'altro ramo (`ignoreRelease`), che il bersaglio non lo chiede.

Chiuso guardando l'**edizione prima di tutto il resto**, e leggendo le colonne gemelle.
Prove: `ContentReleaseVisibilityTests`, due — quella che apre e quella che chiede che senza release resti
chiuso — **verificate rosse**.

### J2. Nel documento aperto, tre tabelle su tre erano titoli VUOTI

Aperta la pagina, «Frequenze ATC/CRC», «Piste» e «Quote di transizione» c'erano come **titoli senza corpo**.

Il corpo derivato lo disegnava soltanto `DocumentSectionsView`, cioè soltanto le sezioni **radice**. Su
quattro famiglie su cinque non si vede: le loro derivate stanno tutte al primo livello. Nel vSOP militare
quelle tre sono **figlie** di «Dati generali» — e i blocchi di una sezione derivata sono vuoti per
costruzione, quindi non usciva niente affatto.

Chiuso facendo scendere profilo e corpo derivato lungo `DocumentSectionsView → SectionBody → SectionNode`.
⚠️ I due parametri sono **opzionali**: chi non li passa — la vIPI ACC, che è a blocchi — non cambia di una
virgola. E c'è una prova che il corpo esca **una volta sola**: la doppia resa su questo componente è già
successa (§8a della carta).

### J3. E la causa di J2: il catalogo non trovava le sezioni annidate

`SectionCatalog.Find` cercava solo nel **primo livello** del profilo (più il `ChildRegistry`, che ha le due
voci della vLOA). Sulle venti sezioni figlie del profilo militare rispondeva `null`, e da lì:

- `IsHostRendered` **falso** su `frequencies`, `runways`, `transition` — che sono rese dalla pagina;
- `IsFixed` **falso** su tutte e venti: venti sezioni di **catalogo** scambiate per sezioni libere, anche
  nell'editor, dove `IsFixed` decide se una sezione si può cancellare o rinominare.

Chiuso facendo scendere la ricerca nei figli. ⚠️ **Misurato prima di toccarla**: gli unici descrittori con
figli sono i quattro contenitori di `AirportMil`, quindi per gli altri profili la discesa non cambia nulla —
e c'è un test che lo **pretende**, così chi aggiungesse un profilo annidato passa di qui.

### Che cosa dice la misura, alla fine

| | |
|---|---|
| vSOP militare pubblicato il 28 agosto | `FrozenSections` **vuoto** — pubblicare non congelava niente |
| lo stesso, ripubblicato dopo la correzione | congela `frequencies`, `runways`, `transition` |
| vIPI civile dello stesso scalo | congela le **sue** quattro (`runwayrules` in più: il profilo militare non ce l'ha) |

E la prova dell'indipendenza, sullo stesso scalo e nello stesso istante — frequenza della torre cambiata nel
catalogo, **solo il civile** ripubblicato:

```
vIPI civile   LIML_TWR  118.999   ← ripubblicata: vede il catalogo nuovo
vSOP militare LIML_TWR  118.100   ← non ripubblicata: tiene il SUO snapshot
```

Prima della correzione la riga di sotto avrebbe detto `118.999`, perché leggeva la fotografia dell'altro
documento.

### E il resto di §V1, verificato a schermo

- il documento militare compare in **Versioni** (`vSOP MIL — LIML · Military vSOP · LIML`, e **non**
  «AirportMil» grezzo), nella **ricerca** col taglio nuovo (`Nominativi` → 2 risultati, percorso
  `vSOP MIL — LIML › Dati generali › Nominativi`) e nelle **«Novità»** del ciclo;
- i **ponti** fra le edizioni: due su Linate (campo misto), **nessuno** su Grottaglie (solo militare) —
  prima ce n'era uno che portava a «documento non disponibile»;
- il **badge** e il filtro `?vista=` su un documento **civile**: marcate «Regole piste» come ATC e «SID»
  come pilota, la chip compare, i badge compaiono, e le tre viste tolgono e rimettono le sezioni giuste.

### ⚠️ Una cosa vista di striscio, e che NON è di questo audit

Nella pagina inglese della vIPI **civile** d'aeroporto il titolo «Regole piste» esce **«Slope rules»**. È la
stessa resa sbagliata che il 28 agosto si era chiusa per i titoli del profilo **militare** seminandoli come
umani (`TitoliUfficiali`): il seme copre i ventisei titoli militari, **non** quelli del catalogo civile.
Non l'ho toccata — è il glossario, non la relazione fra le due edizioni — ma è scritta come lavoro aperto
(§V4), perché «Slope rules» è un titolo che un controllore non trova.

⚠️ **Misurata prima di lasciarla lì**, interrogando la memoria di traduzione su tutti i titoli del catalogo
civile: **su tredici ne sbaglia UNA**. «Aree regolamentate», «Configurazioni», «Coordinamenti»,
«Frequenze», «Separazioni», «Separazioni radar» la macchina le rende bene; le sigle (AOR, SID, VFR) le lascia
stare — ma **per caso**, perché sono `Machine` e nessuno le protegge (`MRVA` è `Human` proprio perché una
volta è tornata «Minimum vectoring»). Quindi §V4 è **una riga**, più una decisione sulle sigle: il conto sta
lì, per non farla sembrare più grossa di com'è.
