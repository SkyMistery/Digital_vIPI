# Fraseologia e traduzioni: una pagina sola, una ricerca vera, «dove si usa»

**1 settembre 2026** · richiesta del committente su `/services/vsop/admin/translations` e
`/services/vsop/admin/glossary`.

> **In una riga.** Le due pagine diventano una; la ricerca interroga il **database** e non il lotto già a
> schermo; ogni riga dice **dove si usa** e apre l'elenco; il registro dice finalmente **quante righe ci
> sono** e come vederle tutte.

## 1. Perché una pagina sola

Il glossario dice **come si rende una formula**; il registro dice **come è stata resa una frase**. Non sono
due lavori: si scrive una voce di glossario ed è nel registro che si va a vedere se è servita a qualcosa.
Passare da un indirizzo all'altro ricaricava tutto, **coppia di lingue compresa** — e le due pagine avevano
ciascuna la propria tendina, con default opposti (`it→en` il glossario, `en→it` il registro).

Ora sono due **sezioni richiudibili** della stessa pagina — glossario sopra, registro sotto, tutt'e due
aperte all'arrivo — con **una** tendina di direzione che le comanda entrambe.

⚠️ **Il vecchio indirizzo resta vivo**: `/services/vsop/admin/translations` è una seconda rotta della stessa
pagina. Sta nei preferiti di chi rivede e nei commenti dei pannelli di documento; una rotta in più costa una
riga, un 404 costa una segnalazione. Nella barra admin la voce è **una** (17 → 16; per l'Editor 12 → 11):
due voci per un indirizzo solo sarebbero due modi di dire la stessa cosa in una barra che esiste per dire
dove si è.

⚠️ **Il prezzo dichiarato della tendina unica**: le due direzioni non hanno lo stesso peso — misurate, nel
`vipi.db` di sviluppo, **22 formule `it→en` contro 1** e **176 frasi `en→it` contro 98**. Con una tendina
sola, una delle due sezioni è sempre la più magra. È la verità del dato, non un difetto della pagina, e le
pastiglie dei conteggi la dicono senza aprire niente.

## 2. La ricerca sta sul database, e non è un dettaglio

Il registro mostra **cento righe per volta** e fino a oggi **non lo diceva**: chi cercava una frase oltre la
centesima concludeva che non ci fosse.

⚠️ Per questo la ricerca è **una clausola della query**, non un filtro sulle righe caricate: un filtro sul
lotto direbbe «non c'è» di una frase che sta alla riga 101 — cioè mentirebbe **esattamente** nel caso in cui
la ricerca serve. Si cerca **nei due lati** (la frase e la sua resa): chi rivede ricorda a volte l'una e a
volte l'altra.

⚠️ E il conteggio del piede — il **M** di «N di M» — esce dalla **stessa** query dell'elenco
(`PerRevisione`): due filtri scritti due volte divergono, e il piede prometterebbe righe che non si possono
scorrere.

⚠️ L'ordinamento è **totale** (`ThenBy(Id)`): «carica altre» è uno `Skip`, e con una chiave d'ordine
ambigua il secondo lotto salta una riga e ne ripete un'altra. Presidiato da un test, perché su una pagina
piccola non si vede.

La ricerca parte mentre si scrive, ma **strozzata**: il giro successivo annulla quello di prima. Senza, una
parola di otto lettere sarebbe otto giri sul database — e l'ultimo non è detto che arrivi per ultimo.

## 3. «Dove si usa»

Due livelli, come il glossario stesso: **una formula vive nelle frasi, una frase vive nei documenti.**

| riga | pastiglia | il pannello apre |
|---|---|---|
| voce di glossario | «N frasi» | le frasi di memoria che contengono la formula, con la loro resa, l'origine e quanti documenti le contengono |
| frase del registro | «N doc» | i documenti che la contengono, con **dove** (prosa · tabella · titolo) e il collegamento all'editor |

⚠️ **Il corpus si legge UNA volta per lotto di frasi.** `DocumentiToccatiAsync` leggeva il corpus editoriale
intero — 499 campi per 23 344 caratteri, misurati — per rispondere di **una** frase: chiamarlo cento volte,
una per riga a schermo, sarebbe cento letture dello stesso corpus. `DoveSiUsanoAsync` chiede per un
**lotto**, ed è la ragione per cui la pastiglia col numero si può mostrare **in elenco** invece che solo
aprendo una riga. Il vecchio conteggio ora **poggia su di lei**: il numero e l'elenco vengono dallo stesso
conto, o un giorno la pastiglia direbbe «2» e il pannello aprirebbe tre righe.

⚠️ **Una riga per documento**, anche se la frase ci sta in tre posti: la domanda di chi corregge è «quali
documenti tocco», non «quante volte».

⚠️ **Le frasi senza nessun documento compaiono lo stesso, con zero.** Non è un caso di scuola: una frase in
memoria che nessun documento contiene più è un testo che è cambiato lasciando indietro la sua traduzione, ed
è la cosa che chi rivede vuole sapere **prima** di spenderci tempo. La pagina la dice: «nessun documento».

⚠️ **Le frasi di una formula comprendono anche quelle umane**, e non è la stessa domanda di
`ContaConLaFormulaAsync` — che guarda le sole automatiche perché chiede «quante si **rifarebbero**». Qui si
chiede **dove compare**, e una frase corretta a mano la contiene esattamente come una tradotta dalla
macchina. Due domande, due metodi, un test che le tiene distinte.

⚠️ Il collegamento all'editor lo compone il **registro delle rotte per tipo**, non la pagina: qui serve solo
l'incrocio fra l'id del documento e il suo bersaglio di release. L'elenco dei documenti si carica alla
**prima** apertura di un pannello, non a ogni caricamento della pagina.

## 4. Le comodità

- **«N di M» e «carica altre»** — il registro dice quante righe ci sono e come vederle. Le righe nuove si
  **aggiungono**: ricaricare da capo perderebbe il posto in cui si stava leggendo, che su una revisione a
  lotti è tutto quel che si ha.
- **Una tendina di direzione sola** (§1).
- **Tastiera.** Nel glossario **Invio** salva (il campo è di una riga, l'a-capo non ci sta). Nel registro
  **Ctrl+Invio**, perché lì il campo è un'area di testo e una resa può contenere un a-capo — mangiarlo
  sarebbe peggio del gesto risparmiato. **Esc** chiude in tutt'e due, e svuota la ricerca.
- **Il testo cercato è marcato** nei risultati. ⚠️ Si costruisce a mano con `AddContent`, non componendo
  HTML: qui dentro passano frasi scritte da persone, e una `MarkupString` le renderebbe eseguibili.

## 5. Verifica

Build Release **0 avvisi** su tutt'e due i TFM, suite verde, **15 test nuovi**
(`RicercaEDoveSiUsaTests`).

**Dal vivo** (Edge+CDP, copia del `vipi.db`):

- il vecchio indirizzo `/admin/translations` apre la pagina fusa, con le due sezioni;
- le sezioni si chiudono e si riaprono (`aria-expanded` e chevron seguono);
- ricerca «runway»: **2 formulas · 0 phrases**, due `<mark>` a schermo, e la pulizia rimette tutto;
- «dove si usa» nel registro: `vIPI — LIBC Crotone · prose` con il collegamento vero
  `/services/vsop/libb/airports/editor?icao=LIBC`;
- «carica altre» su EN→IT senza filtro: **100 of 176 → Load 76 more → 176 of 176, zero duplicati**;
- tastiera: Esc chiude, Ctrl+Invio salva («Correction saved…»);
- console pulita, nessun letterale Razor non valutato.

⚠️ La pastiglia «N frasi» del glossario è stata provata **inserendo una frase apposta** nella copia del
database: nel dato di sviluppo **nessuna** delle 98 frasi `it→en` contiene una delle 22 formule, quindi la
strada sarebbe rimasta non percorsa. Misurato prima di concludere che fosse un difetto.

## 6. Le tre aggiunte del giro dopo

Richieste dal committente subito dopo, e tutt'e tre fatte. Due sono quel che sembravano; la prima no.

### 6a. Il filtro per origine è UN comando a tre stati, non un secondo interruttore

⚠️ **Misurato prima di costruirlo**: nel `vipi.db` reale ci sono **192 righe umane, tutte riviste** e **82
automatiche, tutte mai riviste**. **Zero** miste — e non per caso: `SaveHumanAsync` è l'unico che scrive
`ReviewedUtc`, e nello stesso gesto ribalta `Origin` a `Human`. «Solo da rileggere» e «solo automatiche»
erano **la stessa domanda**, e metterle come due comandi sarebbe stato un secondo interruttore per lo stesso
stato.

Quindi il booleano diventa **tre chip col loro conteggio** — *tutte 99 · macchina 41 · persona 58* — e si
guadagna lo stato che prima **non si poteva chiedere**: *solo le corrette da una persona*, che serve a
rileggere il lavoro di qualcun altro o a copiare una resa già decisa. Il filtro è sulla colonna `Origin`,
che è quella che porta il significato che si legge a schermo.

⚠️ I conteggi delle chip rispettano la **ricerca** ma non l'origine scelta: dicono «quante ce n'è di ognuna
fra quelle che stai cercando». Se rispettassero anche l'origine, due chip su tre direbbero sempre zero.

### 6b. «Vedi» porta al punto, non al documento

Il pannello ora dice anche **§ la sezione**, e offre due strade: **vedi →** apre l'anteprima bozza
**all'ancora della sezione** (`s-{id}`, la stessa che usano l'indice e i deep-link di tutte le famiglie), e
**modifica →** apre l'editor come prima.

⚠️ **La trappola, trovata dal vivo e non dai test**: gli id di sezione sono **per versione**. La stessa
«Remarks» di LIBC è la **611** nella pubblicata e la **651** nella bozza: il primo collegamento apriva la
pagina giusta su un'ancora che lì dentro **non esiste**, e la pagina restava in cima senza dire niente.

Ora, fra due occorrenze nello stesso documento, vince quella della **versione corrente**; e se la frase sta
**solo** in una versione vecchia l'ancora **non si offre** — il titolo della sezione sì, perché dice dove
guardare. ⚠️ Il **conto** dei documenti resta su **tutte** le versioni: è la portata del corpus, e cambiarla
farebbe dire a questa domanda una cosa diversa da quella che dice la memoria. Due test lo presidiano.

### 6c. Il glossario si ordina

Un interruttore accanto al titolo: **recenti** (default, quel che serve subito dopo aver scritto una voce) o
**A→Z** (quando si controlla se una formula c'è già). ⚠️ Alfabetico per `SourceKey`, cioè la formula in
minuscolo: sul testo così com'è stato battuto, «Riporta» finirebbe prima di «attendi» — un ordine deciso dal
tasto maiuscolo.

**Verifica dal vivo delle tre**: chip *all 99 · machine 41 · human 58* con la colonna Origin coerente a ogni
scelta (41 sole «Machine», 58 sole «Person», 99 miste); l'ordine del glossario passa da
*libera la pista · circuito di traffico · punto attesa* ad *allinea e attendi · armamento e disarmo ·
attendi a punto attesa*; e «vedi →» porta a `…?as=draft#s-651`, dove l'ancora **esiste** e contiene la
frase. **9 test nuovi** (24 in tutto in `RicercaEDoveSiUsaTests`).

## 7. Cosa resta fuori davvero

- Un collegamento alla **cella** esatta di una tabella: si arriva alla sezione, e dentro la sezione la frase
  si trova a occhio.
- L'ordinamento del **registro**: resta «le mai riviste per prime», che è l'ordine del lavoro.
