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
| I **messaggi a chi modifica** — errori di validazione e motivi di blocco all'eliminazione | `Messaggio.Lingua(it, en)`, dalla cultura della richiesta |
| Le **due pagine d'errore** — `PaginaErrore` e `IvaoLoginFailurePage` | `Messaggio.Lingua(it, en)`, e `lang` da `Messaggio.Codice` |

⚠️ **Le pagine d'errore erano un'eccezione non dichiarata**, corretta il 28 agosto 2026: `lang="it"`, testo
italiano e una riga inglese in grigio in fondo. Sono le pagine che un lettore inglese vede *proprio quando*
qualcosa si è rotto — il momento peggiore per non capire che cosa c'è scritto — e non ricadono sotto
l'eccezione «log e diagnostica», che vale per ciò che un utente non legge mai.

⚠️ Non passano dalle **risorse** e non è una svista: quelle vivono in `Vipi.Ui`, e il senso di quelle due
pagine è dipendere dal minor numero di pezzi possibile — devono reggere quando è il layout condiviso ad
aver lanciato, o quando l'autenticazione è rotta.

⚠️ **`lang` esce dallo stesso posto del testo** (`Messaggio.Codice`). Con due letture separate della
cultura, un giorno una pagina direbbe `lang="it"` con dentro l'inglese — e per un lettore di schermo, o per
il traduttore automatico del browser, quella riga è l'unica cosa che dice in che lingua è scritta la pagina.

### Quali messaggi del backend hanno due lingue, e quali no

Il confine lo dice **il tipo dell'eccezione**, ed era già così prima che qualcuno lo scrivesse:

| Tipo | Chi lo legge | Lingua |
|---|---|---|
| `ValidationException`, `EditConflictException` | una **persona**, in cima all'editor | **due** (`Lingua(it, en)`) |
| `InvalidOperationException`, `KeyNotFoundException` | la pagina d'errore e il registro | **italiano** (l'eccezione dichiarata più sotto) |

«Sezione 41 inesistente» non dice niente a nessuno: è una invariante, e non si traduce. «Proietta i
settori prima di generare la vLOA» è un'istruzione, e si traduce — anche quando per ragioni storiche viaggia
dentro una `InvalidOperationException` (sono quattro, e sono marcate nel codice).

⚠️ **C'è una guardia**, `MessaggiAChiModificaTests`, e il controllo è **strutturale**: non prova a
indovinare se una stringa è italiana — un elenco di parole sbaglia in tutti e due i versi, e infatti la
scansione a parole con cui è cominciata questa passata ne aveva mancati quattro («Intervallo QNH invertito
(From > To).» non ha né accenti né parole funzione italiane). Pretende che l'argomento sia `Lingua(...)`,
che è l'unica forma che porta due lingue.

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

### Le SIGLE non si traducono

«MRVA», «SID», «AOR», «METAR & TAF»: si chiamano così in frequenza e sulle carte, e una sigla tradotta non
è più quella sigla. La sezione delle minime di vettoramento **si chiama MRVA** in tutte e due le lingue —
prima diceva «Minime di vettoramento», che il motore rendeva «Minimum vectoring»: giusto a metà, e comunque
non la sigla.

⚠️ **Il titolo di una sezione di catalogo sta NEL DOCUMENTO**, non nel catalogo: cambiare `SectionCatalog`
vale per i documenti nuovi, e su quelli già scritti non cambia niente. Serve un passo di manutenzione
all'avvio (`RenameMinimaSectionsAsync`), e **le release già pubblicate restano com'erano** finché non si
ripubblica — la regola di ogni altra correzione editoriale.

### Chi scrive corregge la sua traduzione

Nell'editor c'è il pannello **Traduzione**: le frasi di *quel* documento, con la loro resa nella lingua
scelta in barra, e la correzione sul posto.

⚠️ Il **Registro** (`/services/vsop/admin/translations`) resta dov'è e non è un doppione: quello elenca le
frasi di **tutta la divisione** in ordine di quanto sono state riviste — è il posto per chi fa un giro di
revisione. Il pannello è per chi ha appena scritto un documento e vuole sapere **come viene letto**: chi
scrive conosce la fraseologia del suo scalo, ed è l'unico che può dire se «riporta sottovento» è diventato
«report downwind» o «bring it back downwind».

⚠️ **Si corregge COME si dice, mai COSA si dice.** Il testo sorgente è la chiave della memoria e lì non è
modificabile: cambiare quel che il documento afferma è un'edit del documento, e passa dall'editor e dalla
release. ⚠️ E la correzione **tocca la frase, non il documento** — vale per ogni documento che contiene
quella frase, per questo il conto si mostra *prima* di salvare.

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
  sito. ⚠️ **Il confine è «lo legge un utente?», non «è testo tecnico?»**: le due pagine d'errore
  sembravano cadere qui e non ci cadevano affatto — le legge un utente, ed è l'unica cosa che legge quando
  la pagina che voleva non c'è. Sono passate alle due lingue il 28 agosto 2026.
- **La Guida** (`GuidaPage.razor`): i **corpi dei capitoli** portano le due lingue **in linea**
  (`@T("italiano","english")`), non nei `.resx`. È la più grande delle eccezioni — un centinaio di righe —
  e sta qui perché il resx è il posto sbagliato per quel contenuto: sono **paragrafi HTML** di quindici
  righe l'uno, con `<ul>`, `<code>` e `<b>` dentro. Nel resx sarebbero valori giganti su una riga sola, in
  un file dove tutto il resto è un'etichetta da tre parole, e nessuno li riuscirebbe più a rileggere in
  parallelo per controllare che le due lingue dicano la stessa cosa — che è esattamente il controllo che
  su una guida serve.
  ⚠️ I **titoli** dei capitoli invece **non** stanno lì: vengono da `GuideSearchCatalog`, che è anche
  quello che li mostra nei risultati di ricerca. Fino al 28 agosto 2026 stavano in tutti e due i posti e
  gli **inglesi divergevano in 11 casi su 38** («Live status and online ATC» nella Guida, «Live status and
  ATC online» nella ricerca): chi cercava leggeva un titolo e ne apriva un altro. È il **vocabolario
  parallelo** che R3 vieta per la briciola di pane, entrato dalla porta di servizio — e nessuna delle due
  copie era sbagliata da sola, che è il motivo per cui nessuno lo vedeva.

## Come si verifica

- `RegoleLinguaTests` (E2E): la briciola servita in `?culture=it` e in `?culture=en` è **identica** su tre
  pagine; il marchio dice «ATC Services» e «IVAO Italy» in entrambe; `?lang=` della Guida reindirizza.
- `SelettoreLinguaTests` (E2E): il selettore c'è, segna la lingua corrente, non perde la query e non somma
  due `culture=`.
- `DocumentTranslatorTests`: il titolo del documento non si traduce e non conta nella copertura.
- **A schermo**: cambiare lingua su una pagina con isole interattive (l'aeroporto ha il meteo) e guardare
  che cambino anche quelle. È l'unico modo di vedere la trappola del circuito.
