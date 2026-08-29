# Il convertitore di coordinate — carta (29 agosto 2026)

> **Stato: 🟠 DA APPROVARE** — nessun codice scritto. **Seconda stesura**, dopo le correzioni del committente
> del 29 agosto: l'uscita sectorfile è **l'elenco dei punti** (i segmenti sono l'opzione), entra
> **KML/KMZ in ingresso**, e le aree si **scelgono da un selettore**.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Riusa: `AuroraSectorfileParser.TryParseDms` (il DMS lo sappiamo già leggere), `AorPolygonProjector` +
> `AccAor`/`AccAor3d` + `vipi-aor.js` per la mappa (**nessun motore di mappa nuovo**), `CircleShapeBuilder`,
> il cancello a livelli di [`2026-08-28-autorizzazioni-a-livelli.md`](2026-08-28-autorizzazioni-a-livelli.md).

## §0 — La domanda

> «Un nuovo servizio, accessibile solo agli utenti `divisionalstaff` o superiori, ma raggiungibile dalla
> pagina `/services` (quindi come servizio indipendente dagli altri su cui poi costruiremo qualcosa di più
> ampio). Deve permettere di convertire un elenco di coordinate in **qualsiasi** formato in coordinate per il
> **DB di IVAO** o per il **sectorfile**. E poi una mappa per visualizzare le coordinate appena convertite.
> In ingresso quanti più formati possibile, con riconoscimento automatico; in uscita solo i due proposti, con
> la possibilità di scegliere.»

E le correzioni della stessa sera:

> «Mi serve che le coordinate date siano convertite nel formato, ad esempio, di p1. Poi in aggiunta possiamo
> fare che se si seleziona sectorfile come modalità richiesta si può scegliere se ottenere solo l'elenco dei
> punti o i segmenti.» · «Forse anche conversione da KML/KMZ ci potrebbe servire.» · «Inserisci il selettore
> delle aree e permettimi di selezionare le aree (rendilo user friendly però).»

## §1 — I formati in gioco, misurati sull'esempio del committente

L'esempio è la stessa area (R14A) scritta nei due modi.

**DB IVAO** — un **vertice** per riga, `lat:lon` in gradi decimali:

```
42.00777778:11.96833333
41.99055556:11.98333333
41.94472222:11.98888889
41.91666667:11.95833333
41.975:11.92
```

**Sectorfile Aurora, a segmenti** (`italy.restrict`) — un **segmento** per riga, DMS puntato, anello **chiuso**:

```
N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;
…
N041.58.30.000;E011.55.12.000;N042.00.28.000;E011.58.06.000;RESTRICT;R14A;
```

**Sectorfile Aurora, a punti** — un **vertice** per riga, ed è **l'uscita che serve al committente**:

```
N042.00.28.000;E011.58.06.000;
N041.59.26.000;E011.59.00.000;
N041.56.41.000;E011.59.20.000;
N041.55.00.000;E011.57.30.000;
N041.58.30.000;E011.55.12.000;
```

⚠️ Questa terza forma **non me la sono inventata**: è quella dei `.vfi` e dei blocchi shape, e il nostro
parser la legge già — `AuroraSectorfileParser`, riga 270: *«DMS, un vertice per riga; il blocco chiude su riga
vuota o sull'header»*. Quindi è una forma **vera** del sectorfile, non una comodità nostra.

**Verificato a mano**: `N042.00.28.000` = 42 + 0/60 + 28/3600 = `42.00777778`; `E011.58.06.000` =
11 + 58/60 + 6/3600 = `11.96833333`. Stessi cinque vertici, stesso ordine.

### ⚠️ Vertici contro segmenti: la differenza che decide la conversione

**Il DB e l'elenco punti elencano VERTICI; la forma a segmenti elenca SEGMENTI.** Verso i segmenti non è una
conversione riga-per-riga:

| verso | cosa succede |
|---|---|
| vertici → segmenti | 5 vertici diventano **5 righe**: `(p1,p2) (p2,p3) (p3,p4) (p4,p5) (p5,p1)` — l'ultima **chiude l'anello**, e va generata: nell'elenco non c'è. Servono inoltre due campi che i vertici **non hanno**: il `TIPO` (`RESTRICT`) e il `NOME` (`R14A`). |
| segmenti → vertici | si prende il **primo punto di ogni riga** e si buttano i duplicati della catena. Se l'ultima riga chiude sull'anello, il vertice di chiusura **non** si riscrive. |

⚠️ Convertendo **da** segmenti, se la catena non è continua (fine riga *n* ≠ inizio riga *n+1*) il servizio lo
**dice**, non lo aggiusta: segmenti scollegati non sono un poligono, ed è più probabile che sia un incolla
parziale che un'intenzione.

### La forma di scrittura, dedotta dagli esempi

- **DB**: gradi decimali, punto decimale, `:` come separatore, **zeri finali tagliati** (`41.975`, non
  `41.97500000`). Arrotondamento a **8 decimali** (≈ 1 mm: è ciò che scrive IVAO).
- **Sectorfile**: `Hddd.mm.ss.fff`, **tre** cifre di gradi anche in latitudine (`N042`), tre di millisecondi,
  `;` fra i campi e **`;` anche in fondo alla riga**.
- Sempre `InvariantCulture`: è un formato macchina, non un testo tradotto. La virgola decimale italiana qui
  sarebbe un guasto, non una localizzazione.

## §2 — Perimetro e cancello

- Rotta: **`/services/coordinates`** (segmento inglese, come `/services/profile-swapper` e `/services/stats`).
- Cancello: **`VipiRole.DivisionStaff`**, cioè `Authz.IsDivisionStaff` — e siccome i livelli sono
  **cumulativi**, Editor e Admin entrano senza una riga in più.
- ⚠️ **Il cancello sta in due sedi** (regola imparata il 29 agosto): la **scheda** nell'hub `/services` si
  mostra solo a chi ha il livello, **e** la pagina rifiuta chi arriva con l'indirizzo scritto a mano,
  col riquadro rosso `Common_AccessReserved` che usano già `AdminTasksPage` e le altre.
  Qui la seconda sede è la pagina stessa: **il motore è puro e senza I/O**, non c'è un servizio da chiudere.
  Se un giorno nascerà un **endpoint** (§8), il cancello va messo lì lo stesso giorno.
- ⚠️ `ServicesHome` è **SSR statico** e resta tale: `IEditAuthorizationService` si inietta anche lì (lo fa già
  `SopHome`) e non costa **nessuna** query — il livello è in memoria.
- La pagina invece è **`@rendermode InteractiveServer`**: ha campi, pulsanti e un caricamento file.

## §3 — L'ingresso: quanti formati, e come si riconoscono

Riconoscimento **per riga e per token**, dal più specifico al più generico. La riga si spezza sui separatori
(`;` `,` `:` `|` tab, spazi), si scartano i campi non numerici finali (`RESTRICT`, `R14A`, `COAST`,
`BUILDING`…) e i commenti (`//`, `#`).

| # | forma | esempio | note |
|---|---|---|---|
| 1 | DMS Aurora **puntato** | `N041.59.26.000` | già letto da `TryParseDms` |
| 2 | DMS Aurora **compatto** | `N0463144000` | `.geo`/`.mva`/`.vfi`; si legge **da destra** |
| 3 | DMS coi **simboli** | `41°59'26.5"N`, `N41 59 26` | °'" opzionali, emisfero prima o dopo |
| 4 | **DM** (gradi + primi decimali) | `41°59.433'N`, `N4159.433` | la forma delle carte aeronautiche |
| 5 | **ARINC/ICAO** a larghezza fissa | `4159N01159E`, `411500N0115730E` | `DDMM`/`DDMMSS`, emisfero infisso |
| 6 | decimale **con segno** | `-11.98333333` | S/W negativi |
| 7 | decimale **con emisfero** | `41.9906N`, `N41.9906` | |
| 8 | coppia **DB IVAO** | `42.00777778:11.96833333` | il formato d'uscita è anche d'ingresso |
| 9 | coppia **CSV/Google Maps** | `41.990556, 11.983333` | l'incolla più comune |
| 10 | **sectorfile a punti** | `N…;E…;` | la forma di §1, un vertice per riga |
| 11 | **sectorfile a segmenti** | `N…;E…;N…;E…;RESTRICT;R14A;` | 4 coordinate + 1-2 campi di coda |
| 12 | **JSON / GeoJSON** | `[[11.968,42.007],…]` | ⚠️ **longitudine prima**: regola IVAO `regionMapPolygon`, scritta in `PolygonGeometry.ParsePoints` |
| 13 | **KML / KMZ** | file da Google Earth | §3-bis |

### ⚠️ Le tre ambiguità, e come si sciolgono

1. **Chi viene prima nella coppia.** Testo → **latitudine prima** (DB IVAO, Google Maps, sectorfile).
   JSON e KML → **longitudine prima** (GeoJSON/OGC/IVAO). Sono due convenzioni vere entrambe, e la differenza
   sta nel contenitore, non nel gusto.
2. **Il primo numero è maggiore di 90.** Non può essere una latitudine: si **scambia** e lo si **dice** in
   diagnostica. Silenziosamente sarebbe un poligono ruotato di 90°, e la proiezione non se ne lamenta —
   disegna (è successo davvero, vedi il commento in `AorPolygonProjector`).
3. **Fuori dall'Italia.** Non è un errore — l'attrezzo serve anche per un confinante — ma se **tutti** i punti
   cadono fuori da un riquadro largo attorno alla FIR italiana, un avviso giallo lo segnala. Sbagliare un
   emisfero è l'errore più facile e il più difficile da vedere su un elenco di numeri.

## §3-bis — KML e KMZ

**In ingresso soltanto.** In uscita restano i due formati chiesti (deciso il 29 agosto).

- **KML**: XML. Si leggono i `<coordinates>` dentro `Polygon`/`LineString`/`Point` di ogni `<Placemark>`.
  Le terne sono **`lon,lat,alt`**: longitudine prima, **quota ignorata** (il nostro dominio è 2D: le bande FL
  stanno altrove, in `AorFlBand`).
- **KMZ**: è uno **zip**. Si apre in memoria con `System.IO.Compression` — lo stesso già usato dal Profile
  Swapper — e si legge `doc.kml`, o in sua assenza la **prima** voce `*.kml`.
- ⚠️ **Il poligono col buco** (`innerBoundaryIs`): si tiene il **contorno esterno** e si **avvisa** che il buco
  è stato scartato (deciso dal committente). Un buco silenziosamente perso è una zona che sembra vietata e non
  lo è.
- ⚠️ **Il file non tocca mai il disco**, come per lo swapper: arriva al server, si elabora in memoria, si
  scorda. La frase sta in pagina, perché è una promessa e va scritta.
- ⚠️ **Tre tetti**, perché uno zip arriva da fuori: dimensione del file caricato, dimensione **decompressa**
  e numero di voci. Uno zip che si dichiara piccolo e si apre enorme è il più vecchio dei trucchi, e qui il
  file lo carica uno staffista ma il codice non lo sa.

## §4 — L'uscita: due formati, e per il sectorfile due forme

Due chip: **DB IVAO** · **Sectorfile**.

**DB IVAO** — una sola opzione: la precisione (6 o 8 decimali, default **8**).

**Sectorfile** — un sotto-interruttore, e i campi che appaiono dipendono da lui:

| forma | com'è | campi |
|---|---|---|
| **Elenco punti** *(default)* | `N042.00.28.000;E011.58.06.000;` — un vertice per riga | *(nessuno)* |
| **Segmenti** | `latA;lonA;latB;lonB;TIPO;NOME;` | Tipo (default `RESTRICT`) · Nome · Chiudi l'anello (default sì) |

⚠️ **Tipo e nome esistono solo per i segmenti**, e quando la forma è «elenco punti» **spariscono**: un campo
che non ha effetto su ciò che si vede è peggio di un campo assente. Il **nome** si pre-riempie da solo con
quello dell'area scelta nel selettore (§5) quando c'è.

Comune alle due forme: la **forma DMS**, puntata (default) o **compatta** (`N0413728965`, quella di `itgeo.geo`).

Sotto ogni uscita: **Copia** e **Scarica `.txt`**.

## §5 — Il selettore delle aree

Un ingresso può contenere **più aree**: un KML con più `<Placemark>`, un pezzo di `.restrict` con più nomi nel
6° campo. Il selettore è **una riga di chip**, le stesse della mappa AoR, e segue tre regole:

1. **Con una sola area il selettore non c'è.** Niente da scegliere, niente da cliccare: si converte e basta.
   È la regola che lo rende *user friendly*, e vale più di ogni altra cosa in questo paragrafo.
2. **Con più aree, tutte accese all'apertura**, chip per nome, più `Tutte · Nessuna` e il conto
   («**3 di 7 accese**»). Spegnere una chip la toglie **dalla mappa e dall'uscita**, insieme.
3. **Un'uscita per area**, ognuna col suo pulsante Copia, più un «copia tutto» che le concatena separate da una
   riga vuota. ⚠️ **Non invento un separatore**: il formato DB non ha commenti, e scriverci dentro `// R14A`
   produrrebbe un testo che IVAO non accetta. Le aree restano distinte perché stanno in **riquadri** distinti.

Il nome dell'area viene dal `<name>` del Placemark o dal 6° campo del sectorfile; se manca, `Area 1`, `Area 2`.

⚠️ Il meccanismo è **uno solo** per KML e per sectorfile: è la stessa domanda («quante aree ci sono qui
dentro?») e merita una risposta sola. Non è la funzione che il committente aveva scartato il 29 sera — quella
era «incolla `italy.restrict` intero e scegli», con le sue 2 222 righe e il ragionamento più ampio che ci sta
dietro. Qui il selettore compare **solo se l'ingresso porta davvero più aree**.

## §6 — La mappa: nessun motore nuovo

Le aree convertite diventano «settori» e si danno ad `AccAor`, che porta con sé mappa 2D, vista 3D, chip,
fondi Esri e stampa. È la traduzione che ha già funzionato per le **aree regolamentate**
([`2026-08-27-aree-regolamentate-una-mappa.md`](2026-08-27-aree-regolamentate-una-mappa.md)):

```
punti → JSON [[lon,lat],…] → AorPolygonProjector.Project → AppAorPolygon
      → AccSectorAor(Label = nome dell'area, Color = un colore del tema)
      → AccAorView → <AccAor />
```

⚠️ Le chip del selettore (§5) **sono** le chip della mappa: un elenco, non due. Accendere l'area la accende in
entrambi i posti, perché è la stessa area.

⚠️ **Il riquadro va rifatto quando cambia il dato**: `@key` sulla firma dei punti, come fa `AccAor` oggi
(`MapSignature()`), o Leaflet resta inizializzato sul poligono vecchio (`data-init` è idempotente **apposta**).

⚠️ **`AorPolygonProjector` vuole almeno 3 punti** — sotto, torna `null`. Convertire **un solo punto** è però il
caso d'uso più comune di tutti: con 1 o 2 punti si disegna un **cerchietto** attorno a ognuno con
`CircleShapeBuilder.Build(lat, lon, 0.3 NM)`, che esiste già per le TWR senza poligono. Tre righe, niente di
nuovo.

### Le due shape a confronto

Un interruttore **«mostra la riconversione»** aggiunge alla mappa, per ogni area accesa, una **seconda** forma:
quella ottenuta riconvertendo l'uscita all'indietro. Stesso colore, tratteggiata. Se le due non si
sovrappongono **esattamente**, la conversione ha perso qualcosa e si vede a occhio prima ancora di leggere il
numero di §7. Spento di default: a regime le due forme coincidono e la seconda è rumore.

## §7 — La diagnostica: nessuna riga persa in silenzio

Sotto l'uscita, una riga di conto: **«letti 5 punti su 5 righe»**. Ogni riga non letta compare con il suo
**numero di riga** e il testo originale. Un convertitore che ne scarta tre su venti senza dirlo è peggio di
uno che rifiuta tutto.

**L'andata e ritorno**: dopo la conversione si riconverte all'indietro e si mostra l'**errore massimo in
metri**. Sul giro DB → sectorfile → DB dev'essere zero a meno del millisecondo (≈ 3 cm): è la prova che il
committente può fare in un colpo d'occhio, senza fidarsi di me. Il gemello visivo è §6.

**Il righello dell'anello**, accanto: quanti punti, anello chiuso sì/no, perimetro in NM, area in NM². Con un
errore di incolla il perimetro diventa assurdo e si vede subito. La distanza `PolygonGeometry` ce l'ha già.

**Tre gesti sull'elenco**: inverti l'ordine (orario ↔ antiorario), ruota il primo vertice, togli il vertice di
chiusura ripetuto. Sono i tre ritocchi che oggi si fanno a mano in Blocco note.

⚠️ **Un tetto all'ingresso** (5 000 righe) come il `MaxRigheDiff` del Profile Swapper: qui il testo attraversa
il **circuito** Blazor, e un incolla di `itgeo.geo` intero sono decine di migliaia di righe.

## §8 — Cosa questo servizio NON fa (e perché lo scrivo)

- **Non scrive niente in banca dati.** È un attrezzo senza stato: entra testo, esce testo. Nessuna migrazione,
  nessuna tabella, nessuna delle 23 migrazioni in coda si allunga.
- **Non produce KML, GeoJSON, CSV.** Il committente ha detto **due** uscite, e il KML è solo un ingresso.
  Il GeoJSON esiste solo *dentro*, per parlare col proiettore della mappa.
- **Non ha un endpoint HTTP.** Se domani servisse (per il Bridge Aurora, per esempio), nasce con il suo
  cancello: vedi §2.
- **Non salva l'area fra le aree regolamentate, e non ricorda nulla fra una visita e l'altra.** Sono i due
  pezzi del «ragionamento più ampio» che il committente ha rinviato: non si accennano nemmeno in pagina.

## §9 — Le slice

| # | cosa | dove | prova |
|---|---|---|---|
| 1 | `TryParseDms` trasloca in `Application/Coordinates`, `AuroraSectorfileParser` delega | Application + Infrastructure | i 4 test esistenti restano verdi **senza modifiche** |
| 2 | `CoordinateParser`: le forme 1-12 + le 3 ambiguità + la diagnostica per riga | Application | un caso per formato, più i casi storti |
| 3 | `CoordinateWriter`: DB, elenco punti, segmenti, DMS puntato/compatto | Application | **l'esempio del committente nei tre versi, alla lettera** |
| 4 | Il lettore KML/KMZ: Placemark, buco scartato, i tre tetti dello zip | Application | un KML di Google Earth vero, uno con buco, uno zip finto |
| 5 | Pagina, rotta, cancello, scheda nell'hub, stringhe IT+EN | Ui | bUnit: due livelli, due `TestContext` (⚠️ bUnit congela il contenitore al primo render) |
| 6 | Il selettore delle aree (assente con una sola) + un'uscita per area | Ui | bUnit: una area → niente chip; tre aree → tre riquadri |
| 7 | La mappa (`AccAor`), il caso 1-2 punti, il confronto tratteggiato | Ui | a schermo |
| 8 | Righello, andata e ritorno, i tre gesti sull'elenco | Application + Ui | a schermo, con l'errore in metri a zero |
| 9 | Guida (`GuidaPage` + `GuideSearchCatalog`) e `HelpHint` | Ui | il servizio esiste anche per chi lo cerca |
| 10 | **Verifica live** con l'esempio R14A e un KMZ vero | — | traccia nella carta |

Un commit per slice, `dotnet build Vipi.slnx -c Release --no-incremental` verde **su entrambi i TFM**.

## §10 — Pre-flight, le quattro domande

1. **Modello**: nessuna entità nuova, nessun tipo punto nuovo (si usa la tupla `(double Lat, double Lon)` di
   `PolygonGeometry`). Nessun modello gemello del DMS: quello che c'è **trasloca**, non si duplica.
2. **Dispatch**: il riconoscimento del formato è **un** `switch` in **un** posto; la scrittura è **un** altro.
   Non c'è un secondo punto nel prodotto che decida «che formato è questa coordinata»: registry =
   over-engineering. ⚠️ Il KML **non** è un terzo dispatch: produce aree e punti, e da lì in poi è la stessa
   strada di tutti gli altri.
3. **Ingressi + verifica**: si arriva dalla scheda in `/services`; si verifica incollando i cinque vertici del
   committente e guardando tornare le cinque righe di `italy.restrict`, **carattere per carattere**.
4. **Propagazione**: `TryParseDms` cambia casa → aggiornare i `<see cref>`, i commenti e i quattro test che lo
   nominano, **nello stesso commit**.

## §11 — Le trappole note, prima di cominciare

- ⚠️ **`Assert.Equal(a, b, 0)` non è una tolleranza**: sui confronti di gradi si usa una tolleranza esplicita.
- ⚠️ **Attributo componente `string` senza `@` = letterale.** `View="..."` ≠ `View="@..."`.
- ⚠️ **La pagina interattiva non si re-inizializza**: se un domani arrivano parametri nella query, si legge in
  `OnParametersSetAsync`, non in `OnInitializedAsync`.
- ⚠️ **`InputFile`**: `input.files` si svuota e lo stream va letto **asincrono in memoria** — le due trappole
  già pagate coi blocchi immagine (`image-blocks-design`).
- ⚠️ **Le stringhe vanno in tutti e due i `.resx`** (IT + EN), e il titolo del servizio segue
  `docs/design/regole-lingua.md`.
- ⚠️ **La testata sta in una riga** e l'altezza si misura: vale anche qui
  (`docs/design/regole-ui-pagine-admin.md`, larghezza minima 1024 per le pagine di staff).
- ⚠️ **`dotnet test` esce 0 anche rotto**: «verde» si legge **contando i progetti** (nove).

## §12 — Definition of done

- [ ] Le classi pure coi loro test; l'esempio R14A verificato **nei tre versi**, carattere per carattere.
- [ ] `TryParseDms` in un posto solo, i suoi quattro test verdi senza modifiche.
- [ ] KMZ vero di Google Earth letto; buco scartato **con avviso**; i tre tetti dello zip provati.
- [ ] Cancello in pagina **e** scheda nascosta nell'hub; provato con un'identità `User` e una `DivisionStaff`
      (`DevIdentityOptions`, sezione `DevIdentity`).
- [ ] Una sola area → **nessun** selettore. Più aree → chip, conto, un riquadro d'uscita per area.
- [ ] Mappa che si ridisegna al cambio dei punti (`@key`), che c'è anche con un punto solo, e che sa mostrare
      la riconversione a confronto.
- [ ] Stringhe IT+EN, voce di Guida, `HelpHint`.
- [ ] `dotnet build Vipi.slnx -c Release --no-incremental` verde su net8 **e** net10; suite ≥ 3 841 su nove
      progetti.
- [ ] Verifica live con traccia, `docs/lavori-aperti.md` aggiornato.
