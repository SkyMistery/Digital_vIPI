# Regole della lingua (28 agosto 2026)

> Gemello di [regole-brand](regole-brand.md) e [regole-ui-pagine-admin](regole-ui-pagine-admin.md): quelle
> governano colori e densità, questa governa **che cosa cambia quando si cambia lingua**.
>
> **Perché esiste.** Dal 28 agosto il sito si legge in due lingue e la barra ha il selettore. Ma «bilingue»
> non vuol dire «tutto si traduce»: alcune cose sono **nomi**, e un nome che cambia con la lingua di chi
> guarda non è più un nome. Senza queste righe scritte, la prossima persona che passa di qui legge le
> eccezioni come dimenticanze e le «corregge» — un marchio tradotto sembra un miglioramento, finché non si
> sa perché stava fermo.

## Le sei regole

| | Regola | Dove sta |
|---|---|---|
| **R1** | Il **marchio** è sempre «ATC Services» | `SopLayout.razor` |
| **R2** | Il **sottotitolo del marchio** è sempre «IVAO Italy», e viene dalla **configurazione** (`Division:Name`), non dalle risorse | `SopLayout.razor`, `DivisionOptions` |
| **R3** | La **briciola di pane** è **tutta in inglese**, in entrambe le versioni del sito | `EnglishStrings`, 29 briciole |
| **R4** | Il **titolo di un documento** non si traduce mai, e non si manda nemmeno al motore | `DocumentTranslator`, `EfTranslatableCorpus` |
| **R5** | Gli **indirizzi** non si localizzano | nessuna rotta per lingua, ed è così da sempre |
| **R6** | **Tutto il resto** segue la lingua scelta nella barra | i `.resx`, e la memoria di traduzione per i documenti |
| **R7** | Le stringhe **dell'applicazione** si traducono **a mano** nei `.resx`; al motore automatico va **solo la prosa dei documenti** | `SharedResource.resx` / `.en.resx` |

## Il perché, regola per regola

### R1-R2 — il marchio è un nome proprio
Fino al 28 agosto il marchio diceva `Services_Title`: «Servizi ATC» in italiano, «ATC Services» in inglese.
Cambiava nome col cambiare della lingua. Chi cerca questo sito lo cerca in un modo solo.

⚠️ `Services_Title` **resta** dov'è usato come *titolo della pagina hub*: quello è un titolo, non il
marchio, e segue la lingua. Due cose diverse che condividevano una stringa.

Il sottotitolo viene dalla configurazione e non dalle risorse perché è il nome della **divisione**: una
divisione diversa che monta questo modulo deve vedere il proprio, non «Italy» scritto a mano.

### R3 — la briciola di pane in inglese, sempre
`Home › LIBB › Airports › vIPI — LIBC Crotone` — anche dentro una pagina italiana.

⚠️ **È la regola che sorprende**, ed è una decisione del committente: la briciola dice *dove sei nel sito*,
e i posti hanno un nome solo. Chi legge in italiano vede l'unica riga della pagina che non segue la sua
lingua; è voluto.

⚠️ **Le stringhe non si duplicano.** La briciola chiede **le stesse chiavi** al **solito resx**, tramite
`EnglishStrings` — un `ResourceManager` con la cultura scritta a mano. La tentazione era scrivere
«Airports», «Apps», «Home» come letterali dentro le briciole: sarebbero diventati un vocabolario parallelo,
con «Aeroporti» rinominato da una parte e non dall'altra.

L'ultima voce della briciola è il titolo del documento, e la copre R4.

### R4 — il titolo del documento è il suo nome
«vIPI — LIBC Crotone» è quello che sta nell'elenco, nella briciola, nel PDF stampato e in bocca a chi lo
cita in frequenza.

Due conseguenze pratiche:
- il traduttore **non lo tocca** e **non lo conta** nella copertura (se lo contasse, un documento tutto
  tradotto direbbe per sempre «manca una frase», e l'avviso diventerebbe rumore);
- il corpus **non lo raccoglie**: non si spendono caratteri di franchigia per una risposta che nessuno
  mostrerà.

⚠️ Le righe già in memoria per i titoli restano lì, inerti. Non serve una migrazione: nessuno le legge più.

### R6 — e quello che segue la lingua
Interfaccia dai `.resx` (2455 chiavi, italiano e inglese allineate 1:1); **prosa dei documenti** dalla
memoria di traduzione ([carta bilingue](../feature/2026-08-27-documenti-bilingue.md)); **prosa generata dal
codice** dalle risorse, scegliendo il template e non traducendo l'uscita (`ReadingLanguageContext`).

### R7 — a mano quello che non cambia mai
Un'etichetta di interfaccia la si scrive una volta e resta lì per anni: mandarla a un motore automatico
costa franchigia, e restituisce una resa che nessuno ha scelto. La prosa dei documenti è l'opposto: la
scrive lo staff, cambia a ogni ciclo AIRAC, e nessuno può ritradurla ogni volta a mano.

## Le trappole già pagate

- ⚠️ **Cambiare lingua deve RICARICARE la pagina.** La navigazione «enhanced» di `blazor.web.js` non
  ricarica il documento: sostituisce il DOM e **riusa il circuito già aperto**, la cui cultura è quella di
  quando è nato. Visto a schermo su LIBC: pagina inglese col riquadro METAR ancora «VENTO / VISIBILITÀ /
  NUBI» e il badge «Live · non connesso». I link del selettore portano `data-enhance-nav="false"`.
- ⚠️ **Un solo selettore.** La Guida aveva il suo, `?lang=it|en`, con una chip IT/EN accanto al titolo: due
  comandi sulla stessa schermata che dicevano cose diverse — `?lang=` non scriveva il cookie, quindi
  bastava andare altrove per ritrovare l'altra lingua. Ora `?lang=` **reindirizza** a `?culture=`.
- ⚠️ **Un titolo di catalogo non passa dal traduttore.** `AirportLegacySections.ForView` riporta ogni
  sezione di catalogo al suo titolo cablato: se lo si chiama **dopo** aver tradotto, butta via la
  traduzione. Il viewer d'aeroporto ripassa le sezioni dalla stessa passata.
- ⚠️ **`IStringLocalizer` non sa leggere in un'altra lingua**: risolve sempre sulla cultura corrente. Per
  R3 serve il `ResourceManager`.

### Il testo che nasce nel BACKEND

Non passa dalle risorse (che vivono in `Vipi.Ui`, e l'applicazione non può dipendere dalla UI): dove serve,
porta con sé **le due lingue** e la sceglie chi sa chi sta leggendo. Tre punti, un solo schema:

| Che cosa | Chi sceglie la lingua |
|---|---|
| Le frasi di **coordinamento** nei documenti | `ICoordinationSentenceTemplate`, sulla famiglia del documento |
| Il **catalogo di ricerca della Guida** (l'unico testo di backend che vede il pubblico) | `SearchService`, da `ReadingLanguageContext` |
| I **messaggi a chi modifica** — 100 errori di validazione e 25 motivi di blocco all'eliminazione | `Messaggio.Lingua(it, en)`, dalla cultura della richiesta |

⚠️ `Messaggio.Lingua` legge la **cultura ambientale** e non si fa passare la lingua di firma in firma: fra
chi la conosce (la richiesta) e chi compone il messaggio (un servizio in fondo a una catena di chiamate) ci
sono cinque o sei firme che dovrebbero portarsi dietro un parametro che riguarda uno solo dei loro
chiamanti. E un messaggio d'errore non finisce mai in uno snapshot congelato, che è l'unico caso in cui la
cultura ambientale non basterebbe.

⚠️ **Nei test la cultura si FISSA.** La lingua che esce dipende dalla cultura della macchina: un test che
asserisce il testo italiano senza fissarla passa in Italia e cade su una macchina inglese. Si usa
`CulturaDiProva.Italiana()` / `.Inglese()`. Non è una fragilità nuova: è una vecchia che adesso si vede.

⚠️ Nel catalogo della Guida le **parole chiave non si sdoppiano per lingua**: chi legge in inglese cercherà
comunque «frequenze», e chi legge in italiano cercherà «runway». Chi cerca vuole trovare, non essere
coerente.

## Le eccezioni, dichiarate

Sono poche, e stanno scritte qui perché una misura con delle eccezioni non dichiarate è una misura che
mente:

- **`ScreensIndex`** (`/services/vsop/screens`): le etichette dei collegamenti restano in italiano. È
  l'indice delle schermate per la **verifica live**, cioè un attrezzo — venti chiavi di risorsa per una
  pagina che non ha lettori.
- **`AccCoordinationView`**: le due frasi di contorno hanno la loro coppia it/en scritta in linea, perché
  lì la lingua la decide il **documento** (una vLOA parla inglese), non chi guarda. Quando la §4 della
  carta bilingue arriverà anche lì, passeranno dalle risorse come tutto il resto.
- **Log e messaggi di diagnostica**: restano in italiano. Non li legge un utente, li legge chi tiene su il
  sito.

## Come si verifica

- `RegoleLinguaTests` (E2E): la briciola servita in `?culture=it` e in `?culture=en` è **identica** su tre
  pagine; il marchio dice «ATC Services» e «IVAO Italy» in entrambe; `?lang=` della Guida reindirizza.
- `SelettoreLinguaTests` (E2E): il selettore c'è, segna la lingua corrente, non perde la query e non somma
  due `culture=`.
- `DocumentTranslatorTests`: il titolo del documento non si traduce e non conta nella copertura.
- **A schermo**: cambiare lingua su una pagina con isole interattive (l'aeroporto ha il meteo) e guardare
  che cambino anche quelle. È l'unico modo di vedere la trappola del circuito.
