# Nuovo documento — cosa crea davvero (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/vsop/editor/newdoc`. Prima carta del giro: **la sostanza**.
> La forma sta nella gemella [`2026-08-22-newdoc-densita-ui.md`](2026-08-22-newdoc-densita-ui.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## La domanda della pagina

«**Voglio un documento nuovo: di che tipo, e su cosa?**»

Quattro pagine su quattro, in questo ramo, hanno nascosto un difetto di sostanza dietro la densità. Questa
lo nasconde nel **nome**: si chiama «Nuovo documento», e per **tre tipi su quattro non crea niente** — apre
l'editor, che crea *se serve* (`EnsureVipiDocumentAsync`, idempotente). Il quarto, la vLOA, crea davvero.
E crea male.

## Cosa ho trovato

### ⚠️ N1 — Da qui si creano vLOA **duplicate**; da Confinanti no

Le due porte che creano una vLOA hanno **due politiche diverse**:

| Da dove | Cosa fa | Idempotente? |
|---|---|---|
| `/vsop/admin/confinanti` → `EfNeighbourRepository` | «se esiste già una vLOA Home↔Neighbour, **riusala**» (commento nel codice, passo 4) | **sì** |
| `/vsop/editor/newdoc` → `EfEditingRepository.CreateDocumentAsync` | aggiunge le `Parties` e crea, sempre | **no** |

Per la vIPI il guard c'è (`Il settore {callsign} è già descritto da un altro documento`), perché lo scope
passa da `Sector.DocumentId`, che è uno-a-molti. La vLOA non ha quel vincolo: le sue parti stanno in
`DocumentParty`, e niente impedisce due documenti con la stessa coppia.

⚠️ **E il resto dell'applicazione non sa gestirne due.** `FindVloaIdByPairAsync` fa
`.FirstOrDefaultAsync()`: con due vLOA LIRR↔LFFF l'editor per coppia ne apre **una**, senza un criterio.
L'altra resta invisibile — ma esiste, può avere release pubblicate, e comparirà negli elenchi.

### ⚠️ N2 — La vLOA creata da qui nasce **fuori catalogo**

`SectionCatalog` dichiara per il profilo `Vloa` **sette sezioni** con titoli e ordine del documento reale
(doc 13 §3c): `purpose`, `aor`, `frequencies`, `operationaltechnique`, `coordination`, `regulated`,
`validity`. Da Confinanti la vLOA nasce con quelle (`VloaStructureSeeder.Seed(… VloaSections.Canonical(…))`).

Da qui nasce con **una sezione sola**, «Scopo e validità», e con `SectionKeys.NewCustom()` — una **chiave
libera**, che non è nessuna delle sette. Il documento è formalmente valido e sostanzialmente monco: le
sezioni obbligatorie non ci sono, e quella che c'è non è nel catalogo che decide chi rende il corpo.

La pagina lo **dice**, e questa è la parte peggiore: *«La vLOA nasce vuota (una sezione iniziale); poi la
riempi nell'editor.»* Un difetto documentato non smette di essere un difetto — e qui è documentato come se
fosse una scelta, mentre la stessa cosa fatta dall'altra porta viene bene.

### ⚠️ N3 — La porta è chiusa a chi ha la chiave

La pagina è dietro `Authz.IsAdmin`. I servizi che chiama autorizzano per **grant di ACC**
(`EnsureCanEditAccAsync`). Quindi il responsabile di LIRR — che *può* creare e pubblicare i documenti di
LIRR — non vede questa pagina, ma se arriva all'URL dell'editor il documento glielo si crea lo stesso.
È lo stesso difetto già chiuso su Versioni il 21 agosto: «il markup mostrava hide/delete ai soli admin
mentre il servizio autorizza per grant ACC».

### N4 — Il lock vale per un quarto della pagina, ma sta sopra tutta

`EditLockBar` con `ResourceLockKeys.NewDoc` sta in cima, sopra le quattro schede. Ma `_canEdit` disabilita
**solo** «Crea e apri editor» della vLOA: i tre «Apri editor →» funzionano in sola lettura. Il che è anche
corretto — aprire un editor non è un atto che vada serializzato — ma la barra promette il contrario.

### N5 — Per tre tipi su quattro non dice se il documento **esiste già**

Scelgo «vIPI ACC» → Roma e il tasto dice «Apri editor →». Se il documento di Roma esiste, lo apro; se non
esiste, lo creo. **La pagina non distingue i due casi**, e si chiama «Nuovo documento». Chi la usa per la
prima volta non sa se sta per creare qualcosa o aprire il lavoro di qualcun altro. Il dato c'è
(`IEditingService.ListDocumentsAsync`) e non lo guarda nessuno.

## Cosa faccio

Quattro slice, un commit ciascuna, `dotnet build -c Release --no-incremental` (0 avvisi) + `dotnet test` su
**entrambi** i TFM a ogni commit.

### 1. Una coppia, una vLOA
`CreateDocumentAsync` diventa idempotente per coppia come lo è già `EfNeighbourRepository`: se la coppia
Home↔Neighbour ha già un documento, **non ne crea un secondo**. ⚠️ Ma non lo «riusa in silenzio» come fa
l'import: qui c'è una persona davanti, e va **detto** — il servizio lancia una `ValidationException` che
nomina il documento esistente, e la pagina offre di aprirlo. Riusare zitti significherebbe che chi ha
scritto un titolo nuovo lo vede sparire senza spiegazione.

Test: due creazioni sulla stessa coppia ⇒ la seconda non scrive e dice quale documento c'è già; coppie
diverse restano indipendenti; ⚠️ e la **direzione conta** (LIRR→LFFF non è LFFF→LIRR: sono due vLOA legittime,
una per lato).

### 2. La vLOA nasce con la sua struttura, da qualunque porta
Il seed canonico esce da `EfNeighbourRepository` e diventa il modo in cui **nasce una vLOA**, punto. La
sezione «Scopo e validità» a chiave libera sparisce: era il segno che questa porta non conosceva il catalogo.

⚠️ Rischio dichiarato: `VloaSections.Canonical` vuole `homeCode`, `foreignCode`, `foreignName` e il ciclo
AIRAC. Da questa pagina ho i primi due e il nome ACC; il ciclo lo dà `IAiracCalendar`, che il repository ha
già. Nessun dato nuovo da chiedere all'utente.

### 3. Chi può editare un ACC può creare i suoi documenti
La pagina passa da `IsAdmin` a **«ha almeno un grant, o è admin»**, e i menu a tendina mostrano **solo gli
ACC su cui si può lavorare**. ⚠️ Non è un allargamento di permessi: è allineare la porta alla serratura, che
è già `EnsureCanEditAccAsync`. Un admin continua a vedere tutto.

### 4. Il tasto dice se crea o se apre
Per ACC / APP / Aeroporto la pagina legge i documenti esistenti (una query, all'apertura) e il tasto diventa
**«Crea e apri editor»** o **«Apri editor →»** secondo il caso, con accanto — quando esiste — lo stato del
documento (bozza / pubblicato) e chi lo sta editando, se qualcuno lo tiene.

⚠️ **Non un divieto**: aprire un documento che esiste è esattamente ciò che si vuole quasi sempre. È
un'etichetta che smette di mentire.

## Cosa NON faccio, e perché

- **Non unifico questa pagina con Confinanti.** Fanno due cose diverse: là si genera in blocco dalle coppie
  confermate, qui si crea la singola a mano (anche per una coppia non confinante — è il caso d'uso che
  giustifica la pagina). Quello che va unificato è **come nasce una vLOA**, ed è la slice 2.
- **Non tolgo il lock.** Serializzare la creazione ha senso (due persone che creano la stessa vLOA nello
  stesso momento), e la barra in fascia qui è la forma giusta: la pagina è corta. Quello che si sistema è
  che **dica a cosa si riferisce** — ed è forma, quindi sta nell'altra carta.
- **Non ripulisco le vLOA duplicate già in archivio.** Nel DB di sviluppo non ce ne sono; in produzione va
  guardato prima, ed è un'operazione sui dati del committente, non un giro di UI. Lo dichiaro come cosa da
  verificare.
- **Non cambio `FindVloaIdByPairAsync`.** Con la slice 1 il duplicato non nasce più; ordinare il
  `FirstOrDefault` per rendere deterministico un caso che non deve esistere sarebbe curare il sintomo.

## Rischi

- La slice 2 cambia **cosa contiene** una vLOA appena creata. Chi ne aveva create di vuote da qui non le
  vede cambiare (si tocca solo la nascita), ma i test che si aspettano «una sezione» vanno rivisti: è il
  primo posto dove guardare se qualcosa diventa rosso.
- La slice 3 fa vedere la pagina a più persone. È l'intento, e la serratura non cambia — ma va detto: da
  quel commit un responsabile d'ACC vede «Nuovo documento» nel menu di Documenti.
