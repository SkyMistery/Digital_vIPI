# Regole di densità e uso per le pagine admin (19-22 agosto 2026)

> **A cosa serve.** Fra il 16 e il 20 agosto sette pagine admin sono state rifatte nella forma —
> [accordi](../feature/2026-08-19-accordi-densita-ui.md), [struttura](../feature/2026-08-19-struttura-densita-ui.md),
> [ACC](../feature/2026-08-19-acc-admin-densita-ui.md), [aeroporti](../feature/2026-08-19-aeroporti-densita-ui.md),
> [editor aeroporto](../feature/2026-08-20-editor-aeroporto-densita-ui.md),
> [editor ACC](../feature/2026-08-20-editor-acc-densita-ui.md),
> [confinanti](../feature/2026-08-20-confinanti-densita-ui.md)
> — e il 21 agosto **versioni** in due giri: prima la parte **lock e azioni**
> ([carta](../feature/2026-08-21-versioni-lock-e-azioni.md)), che non era di forma ma di sostanza
> (§18, regole 95-105), poi la **densità** ([carta](../feature/2026-08-21-versioni-densita-ui.md), §19,
> regole 106-116); il 22 agosto **permessi** ([carta](../feature/2026-08-22-permessi-densita-ui.md),
> §20, regole 117-124) — e ogni giro ha lasciato una regola pagata a caro prezzo,
> spesso da un difetto visto solo **misurando**. Questo foglio le raccoglie perché le pagine ancora da fare del
> ramo di modifica partano da lì invece di ripagarle.
>
> Non è un regolamento di stile: è l'elenco delle cose che, saltate, sono già costate un giro di correzioni.
> Ogni voce dice **cosa fare** e **perché** — il perché serve a capire quando la regola **non** si applica.

## 0. Il perno

**Ogni fascia tolta in testa diventa contenuto visibile.** Le pagine admin sono elenchi lunghi (152 settori,
250 nodi, 8878px di pagina) e tutto ciò che sta sopra il contenuto lo paga il contenuto. È la ragione di quasi
tutte le regole che seguono; quando una di esse sembra pedante, è perché il suo costo si vede solo a schermo
pieno di righe.

## 1. Testata

1. **Una riga sola**: `titolo (+conteggio) · «?» · comandi di pagina · —— · barra del lock`.
   Classi già pronte: `.st-head` (struttura, ACC), `.xt-head` (accordi).
   Il **lock è uno stato, non una sezione**: sta in riga, non in fascia per conto suo.
2. **I margini di fascia della `.lockbar` si azzerano nel CSS della testata, non nel componente.**
   `EditLockBar` la usano anche pagine dove la fascia è ancora la forma giusta (nuovo documento).
3. **In testata i comandi di PAGINA**, nella riga-titolo quelli della tabella. Il criterio è *su cosa agiscono*:
   l'import della pagina ACC porta ACC **e** settori, quindi non appartiene alla tabella sotto cui stava.
4. **Testata appiccicata: opt-in** (`.st-head.sticky`), solo dove la pagina scorre davvero **e** i comandi di
   scrittura stanno lassù. Quote: sotto la topbar (62px), `z-index` sotto di lei e sopra le righe, sfondo pieno,
   margini negativi per coprire il respiro laterale del `.wrap` (altrimenti si vedono le righe scorrere di fianco).
5. **L'esito dell'operazione è un chip in testata** (`.st-msg`), fra i comandi che l'hanno provocato e il lock —
   non una fascia sopra il contenuto. Misurato: con e senza messaggio la prima riga della tabella resta a `y=306`;
   prima il messaggio spingeva in giù la tabella su cui si stava lavorando.
6. **Un titolo di sezione che ripete il titolo di pagina sparisce**, e il suo conteggio sale accanto al titolo.

## 2. La prosa diventa un «?»

7. Sottotitoli e paragrafi esplicativi **sempre a schermo** diventano un `HelpHint`: il testo si **riusa
   identico**, cambia solo *dove* si legge. Tre righe di prosa costano tre righe a chiunque, per sempre; il «?»
   le costa a chi le chiede.
8. Il testo tolto porta via la sua chiave da **entrambi** i resx; il testo spostato **tiene la stessa chiave**.
9. Testo lungo → `ExtraClass="wide"` (360px): a 270 un paragrafo diventa una colonna di dodici righe.
10. I «?» si aprono **solo al clic**, mai al passaggio del mouse. Regola generale del progetto.
11. Il popover **si apre dove c'è posto**: `placeHelpPop` (`vipi-ui.js`) misura all'apertura e ribalta. Non
    serve più decidere `left` a mano — e chi lo ha deciso resta rispettato (il JS usa classi proprie).
12. Ogni «?» punta a una **sezione della Guida**; se non esiste, si crea in `GuidaPage` **e** si registra in
    `GuideSearchCatalog`, altrimenti la ricerca globale non la trova.

## 3. Altezza e densità

13. L'altezza «schermo meno ciò che sta sopra» **non è esprimibile in CSS: si misura** (`vipiFitViewport`) e si
    **rimisura a ogni render** — sopra ci sono cose che compaiono e spariscono da sole (avvisi, messaggi, lock).
14. Il `calc()` nel foglio di stile è il **valore di partenza** prima che il JS misuri, **non la verità**.
    (Storico: una stima fissa di 250px contro un vero di 398 faceva scorrere la pagina di 148.)
15. Sotto la soglia di usabilità (**320px**) l'altezza fissa **sparisce**: un riquadro alto quanto lo schermo
    dentro una pagina che scorre sono due barre di scorrimento annidate.
16. **Un solo pavimento**: la `min-height` del CSS deve valere quanto il `fitMin` del JS. Due pavimenti diversi
    per la stessa cosa sono un pavimento sbagliato — a zoom 120% vinceva quello del CSS e la pagina scorreva di 21px.
17. Se una fascia è appiccicata, **chi sta sotto deve conoscerne l'altezza**: `vipiStickyOffset` la misura e la
    scrive in una variabile CSS **sullo scope della pagina** (`.wrap`), non su `<html>` — cambiando pagina il
    valore deve sparire con l'elemento, non restare buono per una pagina che non c'entra.
18. Dentro un riquadro incastonato scorre **solo il corpo**: intestazione, filtri e tasti restano fermi.
    `min-height:0` sui figli flex è obbligatorio, altrimenti il figlio si rifiuta di rimpicciolirsi e il
    riquadro cresce oltre l'altezza misurata.

## 4. Lo zoom di pagina — due unità di misura

19. `vipi-zoom.js` applica `zoom` su `<html>`. Da lì convivono **due spazi**: `getBoundingClientRect()` e
    `window.innerHeight` parlano in **pixel di finestra**, mentre `top:` e `height:` **scritti** in CSS sono in
    **unità di layout** (= pixel di finestra / zoom). **Misurare nell'uno e scrivere nell'altro è un difetto**:
    misurato, 17,6px di buco a zoom 1.2 e −12px (elemento nascosto) a 0.8. Si divide per `rootZoom()`.
20. **Arrotondare per difetto** quando un buco è peggio di una sovrapposizione: un pixel di sovrapposizione non
    si vede, un pixel di buco lascia passare le righe.
21. Cambiare zoom **non** fa scattare né un render Blazor né un `resize`: chi misura va svegliato
    (`vipiApplyZoom` emette un `resize`).
22. Confrontare va bene finché le due grandezze parlano la stessa lingua: `rect` contro `clientWidth` regge
    anche sotto zoom. È solo ciò che si **scrive** che va convertito.

## 5. Tabelle lunghe

23. **`thead` appiccicato** (`.res-table.sticky-head`) quando la tabella è lunga o ci si **scrive dentro**: con
    152 righe e due caselle identiche da 70px («Lim. inf.» e «Lim. sup.») l'intestazione serve proprio quando è
    fuori schermo. Il bordo perso da `border-collapse` si ridisegna con `box-shadow: inset`.
24. **La riga intera è il comando di selezione**: cliccata sceglie, ricliccata lascia, un'altra **sposta** la
    scelta. Uno alla volta — è una scelta, non un insieme di spunte.
25. I tasti dentro la riga **fermano la propagazione** (`@onclick:stopPropagation`): chi preme «Nascondi» sta
    facendo un'altra cosa.
26. Da tastiera: `role="button"`, `tabindex="0"`, `aria-pressed`, e `Invio`/`Spazio` fanno quello che fa il clic.
27. Il fondo della riga scelta **deve battere l'hover** della tabella, altrimenti la riga si «spegne» appena ci
    passi sopra col mouse.
28. La selezione è uno **stato proprio**, non il codice scritto nella casella di ricerca: chi sceglie non deve
    ritrovarselo nel campo di testo né perdere la scelta scrivendoci sopra. I filtri convivono.
29. **Un filtro nato da un clic in un'altra tabella va detto dove se ne vedono le conseguenze** (chip
    «solo LIBB ✕» nella barra dei settori) — altrimenti restano 4 righe su 152 e non si sa perché — ed è anche
    il modo di toglierlo senza risalire.
30. **Contatori onesti**: «N di TOT» quando un filtro è attivo, il totale secco quando non lo è. Un pill che
    dice sempre il totale mentre la tabella mostra altro è un numero che mente.

## 6. Comandi che non saltano

31. Chip e tasti **sempre presenti, spenti a zero**. Se compaiono col contatore, la riga si accorcia sotto il
    puntatore proprio mentre ci si sta mirando.
32. **Chip in gruppo**: così la barra va a capo in **un punto solo**, sempre lo stesso. Sciolti, va a capo
    l'ultimo e quale sia cambia con la larghezza — sembra rotta, non stretta.
33. **Etichette brevi = nomi, non troncamenti** (un tasto con l'etichetta tagliata è un altro tasto). Il
    dettaglio va nel `title`. Misurato: sei tasti da 825px scesi a 595 senza tagliare una parola.
34. Prima di stringere, **misurare quanto occupa ogni pezzo**: la riga sta o non sta per una ragione precisa,
    e di solito il pezzo più largo non è quello che si sospetta (era la frase del lock, 647px → 289).

## 7. Scritture che si accumulano

35. Se si scrive in **più celle prima di confermare**, servono tre cose insieme: **stato «sporco» visibile**,
    **salva-tutti col conteggio**, e un ricarico che **riapplica i pendenti**. Senza la terza, salvare una riga
    cancella in silenzio le altre — è il difetto reale trovato in `AccAdminPage` (`ReloadAsync` rifaceva i
    dizionari dal DB e le celle compilate tornavano indietro senza un avviso).
36. Una riga che **fallisce resta pendente**: sparire dal contatore senza essere stata scritta è la stessa perdita.
37. **Il posto da cui si conferma deve stare a schermo mentre si compila.** È la ragione della testata
    appiccicata: chi riempie le celle in fondo non deve risalire 8000px per premere «Salva».
38. Sporco = **diverso dal salvato**, non «toccato»: riscrivere lo stesso valore non deve accendere il contatore.
39. Singolare e plurale nei messaggi. «1 settori salvati» è il genere di dettaglio che fa sembrare la pagina
    scritta da nessuno.

## 8. Icone, campi, nomi

40. **Emoji colorate → `Icon`** (`eye`, `eye-off`, `check-circle`, `refresh`, `x`, …); i glifi monocromatici
    (`✕ ✎ ⟳ ⊞`) restano testo. Se nel set non c'è l'equivalente, si tiene l'emoji e lo si dice.
41. Campi filtro con `.htree-search` / `.htree-select`: la lente è **disegnata nel campo**, non scritta dentro
    il segnaposto.
42. **Quando un meccanismo sparisce, sparisce anche il nome.** `.detail-sticky` è morta insieme allo sticky che
    descriveva: un nome che racconta un meccanismo che non c'è più mente a chi legge fra sei mesi.
43. Chiavi nuove **sempre IT+EN**, nello stesso giro.

## 9. CSS: due trappole che si ripagano

44. Le regole delle pagine di struttura vanno scritte con **`.struct` davanti** e verificate sul **valore
    calcolato**: contro `.res-table`/`.inline-form` una regola da due classi perde **in silenzio**, e il difetto
    si vede solo misurando in pagina.
45. Le classi che mette il **JS** sono **proprie** (`help-flip`, `help-up`, non `left`): una classe messa a mano
    da chi ha scritto la pagina è una decisione, e toglierla d'ufficio la cancella.

## 10. Metodo

46. **Carta prima del codice** ([FEATURE-PROCESS](../FEATURE-PROCESS.md)), una slice per commit,
    `dotnet build Vipi.slnx -c Release --no-incremental` verde su **entrambi** i TFM (gli avvisi sono errori e
    `dotnet test` non li vede) più `dotnet test`.
47. **Verifica guidata con numeri**, non «sembra a posto»: larghezze, quote, buchi, conteggi — e gli screenshot
    vanno **guardati**, non solo prodotti. Metà dei difetti di questi giri non aveva un'asserzione che li cercasse.
48. **Provare gli assetti**: 1600 / 1440 / 1280 / 1024 px, **IT ed EN**, zoom da 0.8 a 1.5. Il buco sopra
    l'intestazione, il lock che andava a capo e il riquadro tarato male sono usciti tutti e tre solo lì.
49. Quando il difetto è in un **componente condiviso**, contarlo su **tutte** le pagine che lo montano prima di
    dire che è un caso isolato: il «?» del lock fuori schermo erano quattro pagine, non una.
50. **Misurare batte stimare, sempre.** Tre volte in questi giri il numero ha ribaltato la carta: la testata che
    «doveva» starci in riga e non ci stava, il chip a zero che «non si vedeva» e invece era verde, il riquadro
    che «era a posto» e sforava di 21px.

## 11. Quello che ha lasciato il giro Aeroporti

51. **`thead` fermo dentro un riquadro che scorre: `top:0`.** La regola 23 vale per una **pagina** che scorre
    (`top:calc(62px + var(--st-head-h))`, la quota della topbar). Dentro un `.st-scroll` lo sticky è relativo
    al **contenitore**: la stessa regola lascerebbe l'intestazione sospesa 62px sotto il bordo del pannello.
    Variante pronta: `.struct .st-scroll .res-table.sticky-head thead th{top:0}`.
52. **Un ciclo lungo dice a che punto è e si lascia fermare.** Novanta chiamate alla sorgente in fila con solo
    uno spinner sono indistinguibili da un'applicazione bloccata. Il chip di testata conta (`12/92`), conta i
    falliti e porta **Interrompi**; l'esito dice quanti non sono stati provati. ⚠️ Dentro un gestore di evento
    Blazor rende **una volta sola, alla fine**: il render intermedio va sollecitato (`StateHasChanged()` +
    `await Task.Yield()`), altrimenti l'avanzamento si vede a lavoro finito — cioè mai.
53. **Una colonna di trattini non è una colonna.** «Assegnazione» era vuota per costruzione (i già assegnati
    sono nascosti) e occupava un quarto del pannello: esiste solo quando c'è qualcosa da dirci. Vale anche per
    l'etichetta di un tasto in colonna: a 1280 «Nascondi» spingeva il cestino **oltre il bordo**, e il nome sta
    benissimo nel `title` se gli altri tasti della colonna sono già icone.
54. **Il tasto che va a capo per primo è quello in fondo**: la ✕ del «deseleziona» è salita accanto al
    contatore che azzera. Misurato: la barra stava in 700px dentro 697 — andava a capo per **tre pixel**, e una
    riga in più di barra è una riga in meno di tabella. Prima di stringere, misurare (regola 34) vale anche
    quando manca pochissimo.

## 12. Quello che ha lasciato il giro Editor aeroporto

55. **`min-width:0` sui figli di una griglia**, come il `min-height:0` nei flex (regola 18). Senza, la colonna
    non si stringe sotto il proprio contenuto e una tabella larga fa scorrere in orizzontale **la pagina**
    invece del suo riquadro.
56. **L'ordine nel foglio conta quanto il peso.** Una regola di pagina scritta *prima* di quella del layout
    condiviso perde a parità di specificità, e perde **in silenzio**: il collasso a una colonna sotto i 1080px
    era scritto da mesi e non si applicava perché `.ed-layout.with-rail` sta più in basso — a 1024 la colonna
    centrale restava 391px. Le regole nuove vanno **in coda**, o con una classe in più.
57. **Un riquadro dentro una pagina che scorre ha un TETTO, non un'altezza misurata.** `vipiFitViewport`
    misura «viewport meno ciò che sta sopra» e vale quando la pagina **non** scorre (la sua quota di partenza
    dipende dallo scorrimento). Dentro un editor che scorre, il riquadro è alto quanto gli serve fino a un
    `max-height`. ⚠️ Un `overflow:auto` **senza** altezza non è un riquadro: è un contenitore di scorrimento
    che non scorre mai in verticale, e lo `sticky` dentro non si aggancia a niente.
58. **Un insieme non può significare due cose.** «Toccata» e «scelta» erano lo stesso `HashSet`: chi
    modificava una cella si ritrovava la riga fra le selezionate, e chi ne sceglieva cinquanta da pubblicare
    le vedeva marcate come modificate. Due stati = due insiemi, due colori, due contatori, due tasti.
59. **Le colonne strizzate si pagano in altezza.** Con `table-layout:auto` in un pannello stretto i chip si
    impilano in verticale e il nome va a capo per sillabe: misurato, **128px per riga** invece di 45 — su 206
    righe sono 17 000px di pagina. Larghezze per classe semantica, `nowrap`, e chi non ci sta scorre.

## 13. Larghezze di colonna: come si decidono

60. **Le percentuali sono un gioco a somma zero.** Ogni punto dato a una colonna lo toglie a un'altra, che poi
    taglia il suo valore («CON…» al posto di «CONV»). La forma che regge: **pixel misurati** per ogni colonna
    che ha un contenuto misurabile, e **una sola colonna senza larghezza** — quella di prosa — che con
    `table-layout:fixed` si prende tutto lo spazio che avanza. Il `min-width` della tabella è la somma delle
    fisse più il pavimento dell'elastica. Misurato sull'editor aeroporto: Condition da 61 a 118px in larghezza
    normale e a **574** a larghezza piena, con zero celle tagliate.
61. **Misurare col font, non a occhio**: `canvas.measureText` con il `font` calcolato della cella, sui valori
    **veri del DB** (non su quelli di esempio). Contare anche ciò che non è testo: le **gronde** del campo
    (9px per lato), le **frecce** del campo numerico, e la **freccina del `datalist`** dei campi con `list=`
    (~16px — a 47px il segnaposto «CONV» si leggeva «C» e la pista «35» spariva).
62. **Anche le intestazioni si tagliano**, e prima di tagliarle si cerca il **nome più corto che resta un
    nome**: `FIX`, `RWY`, `Stato` — non `Punto (FIX)`, `Runway`, `Pubblicazione` troncati. Solo dopo si taglia
    coi puntini, col nome per esteso nel `title`. Una colonna che si intitola «R…» non è una colonna.
63. **Una tabella lunga è la pagina**: se il minimo che non taglia niente supera il riquadro, meglio far
    scorrere il riquadro (con l'intestazione ferma, e un tasto che allarga) che mozzare dieci campi. La scelta
    va **dichiarata**, non subìta: sull'editor aeroporto sono 84px di scorrimento che il tasto ⤢ azzera.

## 14. Gesti: doppio clic, scorciatoie, azioni di gruppo

64. **Il doppio clic è una scorciatoia, mai l'unica via.** Ogni gesto rapido ha un tasto equivalente nella
    barra e una via da tastiera (Shift+clic, che il browser consegna anche come Shift+Invio sui bottoni).
    Altrimenti la funzione la conosce solo chi l'ha scritta.
65. **Il doppio clic sta sulle celle NON scrivibili.** Dentro un campo il doppio clic seleziona la parola ed è
    il gesto di chi sta scrivendo: rubarglielo è un difetto. Dove ogni cella è un campo (le SID manuali) il
    gesto vive sulla casella di scelta.
66. ⚠️ **Il doppio clic arriva DOPO due clic singoli.** I due toggle si annullano fra loro, quindi la regola
    del doppio clic si applica sullo stato di partenza — ma si vede un **lampeggio**. Toglierlo significa
    ritardare *ogni* clic singolo di ~250ms: su una tabella dove si clicca a centinaia il rimedio è peggio.
67. **Un'azione di gruppo dice sempre cosa farà e su quante righe**, nell'etichetta: «Applica «4000» alle 15
    scelte». E se la selezione contiene righe **nascoste da un filtro**, l'esito le conta a parte
    («6 non sono a schermo»): scrivere su righe che non si vedono, senza dirlo, è una sorpresa.
68. **Quello che si può fare in una tabella si fa anche nell'altra.** Due tabelle gemelle nella stessa pagina
    con gesti diversi costringono a ricordare *dove* si è invece di *cosa* si sta facendo: le SID manuali hanno
    preso la selezione che avevano solo le importate.

## 15. Ricognizione: chi aderisce e chi no (19-22 agosto 2026)

Misurato guidando tutte le pagine di lavoro a **1600×900, in italiano**, sul DB di sviluppo (l'altezza dipende
dai dati: in produzione i numeri saranno altri, l'ordine di grandezza no). «Fasce» = callout ed EditLockBar
messi come striscia sopra il contenuto; «tabelle» = righe di corpo, `*` = intestazione appiccicata.

### Già a norma — nove pagine, e sono loro ad aver prodotto le regole

| Pagina | Rotta | Altezza | Note |
|---|---|---:|---|
| Accordi di coordinamento | `/vsop/admin/trasferimenti` | 900 | testata in riga, altezza misurata, colonne fisse |
| Struttura | `/vsop/admin/sectorstructure` | 900 | testata in riga, due pannelli con il solo corpo che scorre |
| ACC | `/vsop/admin/acc` | 8714 | testata appiccicata, `thead` fermo su entrambe le tabelle (28 e 152 righe) |
| Aeroporti | `/vsop/admin/airports` | 900 | **da 13 745**: due pannelli misurati, `thead` fermo dentro lo scroller, azioni di gruppo con avanzamento ([carta](../feature/2026-08-19-aeroporti-densita-ui.md)) |
| Editor aeroporto | `/vsop/{acc}/airports/editor` | 4 913 | **da 31 286** su LIRF (206 SID): riquadro col tetto e `thead` fermo, riga 128→45px, larghezza piena, modificata≠scelta ([carta](../feature/2026-08-20-editor-aeroporto-densita-ui.md)). L'altezza non dipende più dai dati |
| Editor ACC | `/vsop/{acc}/editor` | 5 595 | **da 9 690 in MODIFICA** (in lettura erano 6 466): blocchi collassabili con fisarmonica, testata in riga, prosa nei «?», riga frequenze 60→43px ([carta](../feature/2026-08-20-editor-acc-densita-ui.md)). Tutto compresso: 1 468 |
| Confinanti (vLOA) | `/vsop/admin/confinanti` | 900 | **da 2 515 chiusa** (aperta era molto peggio: il dettaglio srotolava tabella + due mappe dentro la riga): due pannelli misurati, dettaglio a destra, una mappa sola, colonne misurate col font, import con avanzamento e Interrompi ([carta](../feature/2026-08-20-confinanti-densita-ui.md)) |
| Versioni | `/vsop/versioni` | 900 | **da 1 664**: due pannelli misurati col dettaglio a destra (era dentro l'elenco), chip dei filtri che **contano** al posto della fascia di riepilogo, azioni nel pannello, riga 118→63px ([carta](../feature/2026-08-21-versioni-densita-ui.md) — la parte lock e azioni è la [sua](../feature/2026-08-21-versioni-lock-e-azioni.md)) |
| Permessi | `/vsop/admin/permessi` | 900 | **da 2 449** (misurata con 16 grant: a tabella vuota diceva 1 346): barra admin al posto delle sei card, una riga per persona, concessione e revoca nel pannello ([carta](../feature/2026-08-22-permessi-densita-ui.md)) |

L'altezza 900 delle prime due **è** il viewport: la pagina non scorre, il riquadro sì.

### Da rifare, in ordine di guadagno

| # | Pagina | Rotta | Altezza | Cosa le manca (misurato) |
|---:|---|---|---:|---|
| 1 | **Sorgenti** | `/vsop/admin/sorgenti` | 1 235 | Sottotitolo, 8 paragrafi d'aiuto, nessun «?», 2 callout in fascia, tabelle corte (5 e 6 righe: qui il `thead` fermo **non** serve). |
| 2 | **Audit** | `/vsop/admin/audit` | 1 166 | Sottotitolo; tabella da 20 righe, **sotto la soglia** in cui l'intestazione appiccicata si ripaga. Poco da fare: il «?» e basta. |
| 3 | **Diagnostica** | `/vsop/admin/diagnostica` | 900 | Sottotitolo, 2 fasce, nessun «?». |
| 4 | **Nuovo documento** | `/vsop/editor/newdoc` | 957 | Sottotitolo, 8 paragrafi d'aiuto, 2 callout in fascia. Il **lock in fascia qui va bene**: la pagina è corta e la fascia è la forma giusta — è la ragione per cui i margini si azzerano nel CSS della testata e non nel componente. |
| 5 | **Incarichi** / **Incarichi admin** | `/vsop/tasks`, `/vsop/admin/tasks` | 900 | Corte: solo sottotitolo → «?» e il messaggio che non spinge. |
| 6 | **Editor APP**, **Editor vLOA** | `/vsop/{acc}/apps/editor`, `/vsop/{acc}/vloa/editor` | 900 | Corte con i dati di sviluppo; da rimisurare su un documento vero prima di decidere. |

### Fuori ambito: le viste pubbliche

`/vsop`, `/vsop/{acc}`, i viewer (vIPI ACC, aeroporto, APP, vLOA), gli elenchi pubblici, `/vsop/changed`,
`/vsop/search`, `/vsop/live`, `/vsop/guida`, l'anteprima release, l'AoR 3D. Lì il contenuto **è** la pagina e si
legge scorrendo: la densità non è un problema da risolvere. Di queste regole valgono solo due:
- le **emoji che sono comandi** diventano `Icon` (quelle che sono **contenuto** — 🌦 🌧 🛫 nel viewer aeroporto —
  restano: sono la cosa, non un pulsante);
- i «?» che si aprono dove c'è posto, che ormai è automatico per tutti.

### Come si legge questa tabella

Il numero grande (l'altezza) dice **quanto** si guadagna, non **da dove** cominciare: su Aeroporti il primo
intervento è il `thead` appiccicato (due tabelle da 92 e 221 righe in cui si scrive), non la testata. La regola
d'ordine è: **prima ciò che serve a chi sta lavorando in fondo alla pagina** (intestazioni che restano, il posto
da cui si conferma), poi ciò che libera spazio (prosa nei «?», fasce), poi la forma (icone, campi, nomi).

## 16. Quello che ha lasciato il giro Editor ACC

69. **Si misura la pagina COME SI USA.** La ricognizione aveva pesato l'editor ACC in lettura (6 466px); in
    **modifica** — che è il solo motivo per cui ci si va — sono 9 690. Ogni comando in più e ogni picker che
    compare solo in scrittura sta nel numero che conta, e non nell'altro.
70. **Un contenitore che si chiude vale più di dieci ritocchi dentro.** Un blocco della vIPI ACC aperto sono
    dieci sezioni e una mappa: chiuderlo toglie ~3 700px, cioè più di tutto il resto del giro messo insieme.
    Con la **fisarmonica** (aprendone uno gli altri si chiudono) la pagina non torna a crescere da sola.
71. ⚠️ **L'evento `toggle` arriva DOPO.** Aprire in gruppo e poi spegnere una bandiera «non fare la
    fisarmonica» non funziona: quando l'evento arriva, la bandiera è già spenta — misurato, «espandi tutto»
    apriva **un** blocco. Si **marchia l'elemento** (un marchio = un evento, consumato dal gestore).
72. **Chi porta a schermo un elemento deve misurare DOPO che il layout si è assestato**, e ritentare: aprire un
    `<details>` mette in coda un `toggle` che può cambiare l'altezza di ciò che sta **sopra** (la fisarmonica,
    una mappa che si inizializza). Misurato: bersaglio a −249px, cioè fuori schermo di sopra; con una sola
    correzione tardiva atterrava a 25px, cioè **sotto la top-bar**. Ne servono due.
73. ⚠️ **Mai `scrollIntoView` dentro un gestore di `scroll`.** È un cane che si morde la coda, e con un
    bersaglio **appiccicato** non finisce mai: il tour di onboarding puntava l'indice `position:sticky` e
    scorreva la pagina da solo — 263 chiamate, 3 268px, su ogni editor, alla prima visita. Si scorre quando
    **cambia il passo**, non quando cambia lo scorrimento.
74. **Un «?» CHIUSO occupa spazio.** Il popover è `position:absolute`: non spinge niente in flusso, ma il suo
    box resta nell'**area scorribile**. A 1280 quello di un rail arrivava a 1 305px e la pagina scorreva di
    lato. Chiuso non deve esistere (`.help-hint:not([open]) .help-pop{display:none}`).
75. **Una classe morta nel foglio è un difetto che aspetta.** La riga della tabella frequenze era alta 60px
    perché `.freq-edit` — scritta apposta nel giro APP — non la applicava nessuno. Prima di scrivere una regola
    nuova, cercare se esiste già e **chi la usa**: `grep` sulla classe, non solo sul foglio.
76. **La lingua si vede solo guidando nell'altra lingua.** Le frasi scritte a mano dentro un componente non
    danno errore, non rompono i test e in italiano non si notano: in inglese la pagina diventa **mista**. Un
    giro di verifica va fatto con `Accept-Language: en`.
77. **Il testo d'aiuto di una sezione appartiene alla riga-titolo della sezione**, non al componente del corpo:
    messo lì da una mappa sola (`DocumentSectionsEditor`), lo prendono **tutti** gli editor che montano quel
    componente, invece di doverlo rifare per pagina.
78. ⚠️ **Una classe non puo' significare due cose, e chi arriva dopo nel foglio vince.** `.sector-pick` era due
    cose insieme: l'elenco di **chip in riga** (`display:flex`, riga 1352) e il **menu a tendina** del picker a
    digitazione (`position:absolute`, riga 1844) — e il secondo era scritto **senza il suo contenitore**. Stando
    piu' in basso vinceva su tutti gli altri usi: frequenze collegate, settori del gruppo APP, settori aperti di
    una configurazione, aree regolamentate diventavano un pannello **largo quanto la finestra**, sovrapposto al
    documento, con la sua barra di scorrimento — e i chip **sparivano** da dove dovevano essere. È la regola 44
    pagata due volte: la cura è scrivere `.sector-pick-wrap .sector-pick`, cioè dire **in quale forma** si sta
    parlando. Prima di aggiungere una regola a una classe: `grep` di **chi la usa**, non del solo foglio.
79. **Due editor gemelli devono avere la stessa forma per lo stesso gesto** (regola 68 anche per i contenitori):
    l'elenco «collega frequenza» era un menu a tendina sull'editor APP (`.app-linkpick`) e chip in riga su
    quello ACC, perche' li' il contenitore mancava.


## 17. Quello che ha lasciato il giro Confinanti

80. **Un dettaglio che si apre DENTRO la tabella sposta la tabella.** La riga espansa (`colspan`) con la sua
    tabella e le sue due mappe faceva saltare in giù di ~700px la riga successiva, cioè proprio quella che si
    stava per guardare. In un pannello a fianco la tabella non si muove e le coppie si verificano in fila. È
    la regola 5 («l'esito non spinge il contenuto») applicata al dettaglio invece che al messaggio.
81. **Scegliere una riga non deve chiamare la sorgente.** I dati che la riga già porta si mostrano subito; il
    ricalcolo che riscarica dalla rete resta un **gesto esplicito**, con Interrompi. Caricare in automatico a
    ogni clic significa bombardare la sorgente mentre si scorre un elenco.
82. **Due disegni della stessa cosa sono uno di troppo.** SVG proiettato a mano e mappa geografica
    disegnavano le stesse shape con gli stessi colori, 320px l'uno. Ne resta uno, e il testo dell'altro
    (`Conf_MapHint`) esce da entrambi i resx col disegno che descriveva.
83. ⚠️ **Un contenitore idempotente va RICREATO, non riusato.** `.aor-leaflet` si inizializza una volta sola
    (`data-init`): riusandolo per un'altra coppia resta la mappa di prima. Serve `@key` sull'identità del
    dato. Vale per ogni innesto JS dentro un ramo Blazor che cambia contenuto senza cambiare forma.
84. ⚠️ **Lo spazio fra un'espressione e un `@if` Razor lo mangia il compilatore.** Misurato: il titolo usciva
    «…vLOA33» attaccato. Serve un carattere vero (`&nbsp;`), non uno spazio nel sorgente.
85. ⚠️ **`@bind` da solo scrive al BLUR.** Un tasto la cui accensione dipende dal campo resta spento finché
    non se ne esce: chi ha appena finito di compilare lo trova spento **proprio mentre lo punta**, e il primo
    clic serve solo a fare il blur. I campi che governano un tasto vogliono `@bind:event="oninput"`.
86. **Quello che si scrive si valida mentre si scrive**, e **con la soglia vera del service**. Un secondo
    giudice con una regola propria direbbe «va bene» a un valore che il salvataggio poi rifiuta: qui il
    conteggio dei vertici passa dallo stesso `PolygonGeometry` del service.
87. **Il min-width di una tabella a colonne fisse è la somma delle fisse PIÙ il pavimento dell'elastica.**
    Con la sola prima somma l'elastica si schiaccia sotto la leggibilità invece di far scorrere il riquadro —
    misurato a 1024, la colonna del nome finiva a **2px**.
88. **Due colonne che sembrano gemelle possono non esserlo.** «Home» è testo nudo (60px), «Foreign» è una
    pill col suo padding (78): con una classe sola a 74 la pill andava a capo e la riga cresceva da 39 a
    48px — su 33 righe, 300px. Il padding di ciò che sta **dentro** la cella si conta (regola 61).
89. **Una soglia di media query scelta a occhio sbaglia.** `max-width:1400` faceva stare la barra in una riga
    a 1400 e in due a 1440. Con `flex:1` il campo si riprende lo spazio quando c'è: meglio nessuna soglia.
90. ⚠️ **Un `catch (Exception)` inghiotte anche la cancellazione.** Diventava un warning («import ACC fallito
    (A task was canceled)») e il lavoro proseguiva: chi premeva Interrompi vedeva un elenco di guai al posto
    dell'esito. `catch (OperationCanceledException) { throw; }` prima del ramo generico, sempre.
91. ⚠️ **Nei test non usare `Progress<T>`**: posta sul `SynchronizationContext` e in un test non ce n'è uno,
    quindi le callback arrivano sul thread pool **dopo** le asserzioni. Serve un `IProgress` sincrono.
92. **Un test sulla cancellazione può passare per caso.** Cancellare da dentro un lambda sperando che l'altra
    chiamata non sia partita non prova niente: con sei in volo entrambe superano il guard d'ingresso prima
    che la cancellazione arrivi. La sorgente finta deve **onorare il token**, come fa un client vero.
93. **Un messaggio non promette più di quanto il codice garantisca.** «Niente è stato scritto» era falso:
    l'upsert dei candidati sta fuori dalla transazione del catalogo. Un esito vago è meglio di uno preciso e
    sbagliato.
94. **Chi scrive prende il lock.** Questa pagina materializzava settori esteri e generava documenti senza
    prendere il lock che le altre quattro pagine di struttura prendono da sempre. Prima di aggiungere un
    comando di scrittura a una pagina: **guardare cosa fanno le sorelle**.

## 18. Quello che ha lasciato il giro Versioni (parte lock e azioni)

95. ⚠️ **Un tasto spento non è una guardia.** L'elenco è una fotografia: chi arriva da un'altra scheda, o con
    la lista caricata dieci minuti fa, preme lo stesso. Il divieto vive nel **service**; `disabled` è cortesia
    che spiega *perché*, nel `title`. Qui si eliminava un documento **mentre un'altra persona lo editava**.
96. **Chi mostra un lock ne mostra anche la SCADENZA.** Il lock del `Document` dura 30 minuti e **non ha
    heartbeat** (a differenza di `EditResourceLock`: 3 minuti + battito): si rinnova al salvataggio e si libera
    con «Fine modifica». «Bloccato» senza un'ora non dice se aspettare o andare a prendere un caffè — e senza
    un **force-unlock** per gli admin la pagina si auto-inchioda per mezz'ora quando qualcuno chiude la scheda.
97. **Due conferme in fila non sono il doppio della sicurezza: sono rumore.** Qui l'eliminazione ne chiedeva
    due, e il testo utile (titolo del documento, «rimuove versioni e release») stava nella **seconda**, un
    `window.confirm` nativo — che per giunta blocca il circuito Blazor e manda in stallo la verifica live.
    Una conferma, **in linea**, e porta la conseguenza scritta.
98. **La conferma va dove l'azione è irreversibile o esce dal binario, non dove sembra grave.** «Nascondi»
    (cambia la visibilità pubblica) e «Pubblica ora» (scavalca il ciclo AIRAC) chiedono; «Mostra» no, non
    distrugge niente. Il criterio è la conseguenza, non la parola sul tasto.
99. ⚠️ **Il gate del markup e quello del service devono dire la stessa cosa.** Qui il markup mostrava
    hide/delete ai soli `IsAdmin` mentre il servizio autorizzava per **grant ACC**: chi il permesso ce l'aveva
    non vedeva i tasti che poteva premere. Un gate più stretto dell'altro non è prudenza, è un bug silenzioso.
100. ⚠️ **Una conferma in linea entra DENTRO la riga e se la prende.** ~500px di prompt in una riga `flex`:
    senza un pavimento il titolo col suo `flex:1` si comprime finché non va a capo **una parola per riga**, e
    le pill con lui. Riga che può andare a capo + `min-width` sul blocco del titolo + `white-space:nowrap`
    sulle pill. Il pavimento si **misura sul caso più carico**, non si sceglie: 260px sembrava giusto e
    mandava a capo lo stesso la riga con sette elementi.
101. ⚠️ **Un badge lungo manda a capo la colonna dei tasti.** «🔒 Stai modificando · lock fino alle 00:48» =
    210px; «🔒 Tu · 00:48» = 89px. La forma distesa vive nel **tooltip**, dove non costa larghezza. È la
    regola 54 pagata di nuovo, su un elemento nuovo.
102. ⚠️ **Due chiavi con la stessa traduzione sfuggono al test dei resx.** `Ver_NoReleaseBadge` valeva
    «No release» in **tutti e due** i file: la guardia confronta le **chiavi**, e le chiavi c'erano entrambe.
    Le stringhe nuove in inglese vanno rilette **in pagina italiana**, non solo aggiunte ai due file.
103. **Un fatto, un posto.** `HasEffectiveRelease` (bool, dal repo) e il riepilogo release (che la pagina si
    ricaricava da sé) erano lo stesso fatto con due query identiche. Il bool ora è **calcolato** dai cicli che
    l'elenco già porta. ⚠️ Ma «senza release» e «programmata e non ancora in vigore» restano **due stati**:
    comprimerli in un bool avrebbe perso l'informazione (`HasAnyRelease`).

104. ⚠️ **Un elenco è una fotografia: il presupposto di un'azione può essere caduto.** Il divieto sta nel
    service (regola 95), ma la **domanda** non va posta se la risposta sarà comunque un rifiuto. Si rilegge
    **il solo dato che si sta per toccare, nel momento in cui lo si tocca** — non tutta la lista, non a
    intervalli: un ricarico periodico costa una query su tutto per un dato che cambia poche volte al giorno.
    Il gancio è `InlineConfirm.CanOpenAsync`. ⚠️ È un `Func<Task<bool>>` e **non** un `EventCallback`, perché
    serve la **risposta**: un EventCallback non ne restituisce, e leggere `Disabled` dopo l'`await` non
    funziona — i parametri di un componente arrivano al render **successivo** del genitore, non al ritorno
    della chiamata.
105. **Un callout con un titolo solo mente su metà dei suoi casi.** «Operazione non consentita» copriva anche
    il conflitto di lock, che non è un permesso negato: porta a un'altra reazione (aspetto un minuto, non
    chiedo un grant). Il titolo si sceglie **nel ramo `catch`**, dove si sa già che errore è.

## 19. Quello che ha lasciato il giro Versioni (parte densità)

106. **Un riepilogo che conta e dei filtri che nominano sono lo stesso fatto.** La fascia diceva «3 in vigore ·
     2 programmate · 1 senza release»; i chip dei filtri dicevano le stesse parole **senza** i numeri. Il
     rimedio non è tenerli allineati: è **un posto solo** — i chip, coi numeri dentro (regola 103 applicata a
     una fascia invece che a una query).
107. ⚠️ **Il numero sul chip conta ESATTAMENTE ciò che il chip mostra una volta cliccato.** Si scrive con la
     stessa condizione del filtro, non con una classificazione «pulita»: un documento nascosto **con** una
     bozza aperta sta in due chip, e va bene. Un chip che dice 3 e ne mostra 2 è un numero che mente — e lo si
     verifica cliccandoli **tutti**, non leggendo il codice.
108. ⚠️ **Un chip a zero non è sempre una buona notizia.** `button.sh-chip:disabled` è **verde** perché su
     Confinanti zero significa «coda vuota, bene». Qui «0 in vigore» è un guaio, non un successo: nella pagina
     il chip spento è **neutro**. Prima di riusare uno stato visivo condiviso, chiedersi che cosa significa
     **zero** in questa pagina.
109. **Quando i filtri diventano quattro gruppi, quelli che sono ELENCHI diventano menu.** Stato e release sono
     pochi e si contano → chip. Tipo e ACC sono elenchi che crescono → `.htree-select`. Misurato: come chip,
     quei due gruppi chiedevano più larghezza di tutto il resto della barra.
110. **Le azioni di una riga possono vivere nel pannello del dettaglio.** Spostandole, la riga torna a essere
     **identità e stato** (icona, titolo, ambito, pill) e la conferma in linea smette di comprimerla: è il
     modo strutturale di chiudere la regola 100, invece di misurare l'ennesimo pavimento. Riga 118 → 63px.
111. ⚠️ **In una riga «testo + gruppo di tasti», a andare a capo dev'essere il GRUPPO DI TASTI.** Con
     `flex-wrap` sul contenitore e nient'altro, è il testo a scendere sotto l'etichetta di sinistra e ogni riga
     guadagna una riga di vuoto. Serve `flex-wrap:nowrap` sul gruppo **più** un pavimento (`flex:1 1 200px`)
     sul blocco di testo. Misurato: riga release 175 → 139px.
112. **Il titolo di sezione dentro un pannello stretto non si comprime** (`flex:none;white-space:nowrap`):
     altrimenti va a capo mentre di fianco resta spazio vuoto.
113. **La prosa di sezione dentro un pannello costa il doppio che in pagina**: lì lo spazio è già stato
     spartito fra due riquadri. Il «?» nella riga-titolo della sezione (regola 7) vale più che altrove.
114. ⚠️ **Anche un sottotitolo può descrivere un meccanismo che non c'è più.** `Ver_HistorySubtitle` parlava dei
     «profili che non hanno versioni» — storage droppato dal doc 08. La regola 42 vale per i nomi **e** per i
     testi a schermo: quello lo legge anche chi non apre il codice.
115. **Le etichette sono un insieme, non stringhe indipendenti**: «in modifica» minuscola in mezzo a sei
     maiuscole si vede, e «Schedulate» non è italiano quando la pill accanto dice «programmata». Si rileggono
     **in fila**, come le vede chi guarda la barra.
116. **Un separatore appeso al nulla** («vIPI Aeroporto ·» per un documento senza ambito) è un difetto di dati
     che si vede solo con i valori veri: il separatore appartiene al pezzo che lo segue, non alla riga.

## 20. Quello che ha lasciato il giro Permessi

117. ⚠️ **Una pagina misurata sui DATI SBAGLIATI mente.** La ricognizione dava Permessi a 1 346px: era la
     misura con la tabella **vuota**, perché il DB di sviluppo non ha grant. Con 16 permessi scritti nella
     copia sono **2 449** (2 623 in inglese) — da ultima della lista a prima. La regola 69 («si misura la
     pagina come si usa») vale anche per **quanti dati** ha dentro: prima di misurare, riempirla.
118. **Un elenco si organizza intorno alla DOMANDA della pagina.** Qui la domanda è «chi può cosa» e le righe
     erano i *grant* ordinati per ACC: la stessa persona compariva in due punti lontani. Una riga per persona
     coi chip degli ACC risponde in un colpo. ⚠️ Il raggruppamento è una **vista**: la revoca continua a
     viaggiare per `Id` del grant, o si è aggiunto un modello gemello (pre-flight 1).
119. **Chi paga il clic è il gesto RARO.** Il form «Concedi» stava sempre aperto e si prendeva metà larghezza
     per un gesto che si fa una volta al mese, mentre guardare chi ha i permessi è quotidiano: il raro va in
     un tasto di testata. E il caso davvero comune — **un secondo ACC a chi c'è già** — diventa un menu dentro
     il pannello della persona, senza ridigitare l'identificativo.
120. ⚠️ **Un elenco di scorciatoie va dentro il ramo autorizzato.** Spostando le card in una barra è facile
     lasciarla fuori dall'`@if` dei permessi: chi non è admin si ritrova un elenco di porte chiuse. Dove
     stavano le card, sta la barra.
121. ⚠️ **`@bind` a un valore che non è fra le opzioni non ne sceglie nessuna**: la casella nasce vuota, sembra
     rotta, e il tasto accanto sembra non fare niente. Il valore iniziale si prende **dalle opzioni vere**, e
     si ricalcola quando le opzioni cambiano (qui: dopo ogni concessione).
122. **Il segno «+» sta nell'icona o nell'etichetta, non in tutte e due**: «+ + Concedi» è quello che succede
     quando una stringa di resx se lo porta dietro dai tempi in cui l'icona non c'era.
123. **Due elenchi della stessa cosa divergono.** Le pagine admin erano elencate in due posti — sei card qui,
     quattro link nella barra di Struttura — e **nessuno dei due era completo**: Audit, Incarichi e Diagnostica
     si raggiungevano solo dalle card; Aeroporti, Sorgenti e Confinanti solo da Struttura. Un componente
     (`AdminNav`) e una lista sola: aggiungere una pagina admin è una riga.
124. **Un VID non è un nome.** «Concesso da 704798» non dice niente a nessuno: i VID mostrati si risolvono col
     roster (`GetDisplayNamesAsync`), che è già in casa — e il nome della persona si prende dal roster anche
     quando il grant fu scritto senza, invece di mostrare un trattino.

## Dove sta la roba

| Cosa | Dove |
|---|---|
| Testata in riga | `.st-head` / `.xt-head` (`vipi-theme.css`), esito `.st-msg` |
| Altezza misurata | `vipiFitViewport(selettore, collapseBelow)` — `vipi-ui.js` |
| Quota di una fascia appiccicata | `vipiStickyOffset(selettore, nomeVar, ambito)` → variabile CSS sul `.wrap` |
| Fattore di zoom | `rootZoom()` — `vipi-ui.js` |
| «?» che si apre dove c'è posto | `placeHelpPop` + `toggle` in cattura — `vipi-ui.js` |
| Intestazioni che restano | `.res-table.sticky-head` |
| Cella modificata e non salvata | `.lim-dirty` |
| Riga scegliibile / scelta | `.acc-pick` / `.acc-pick.picked` |
| Chip di stato | `.sh-chip` (`.warn`, `.on`, `:disabled`), gruppo `.sb-chips` |
| Barra dei filtri | `.struct-bar` + `.htree-search` / `.htree-select` |
| Riquadro col tetto (editor che scorrono) | `.ed-pane` (`max-height`), `thead` con `top:0` dentro lo scroller |
| Riga-titolo di sezione con «?» e comandi | `.ed-h3` |
| Larghezza piena (indice e rail via) | `.ed-layout.sid-wide` |
| Colonne SID misurate + Condition elastica | `.sid-edit` (base), `.sid-imported` / `.sid-manual` (larghezze) |
| Riga modificata e non salvata | `.row-dirty` (giallo) contro `.row-sel` (blu) |
| Elenco + dettaglio a fianco, misurati | `.conf-layout` (griglia 1.35/1) + due `.st-pane` |
| …lo stesso su Versioni e Permessi | `.ver-layout` / `.perm-layout` (stessa griglia, stesso `vipiFitViewport`) |
| Barra fra le pagine admin | `AdminNav` (`Components/AdminNav.razor`) + `.admin-nav` — l'elenco sta lì, non nelle pagine |
| Testata del pannello di destra + riga azioni | `.ver-detail-head` / `.ver-acts` (fermi: scorre solo `.st-scroll`) |
| Riga scegliibile fuori da una tabella | `.doc-rowi.acc-pick` (+ `.picked`, `.row-off`) |
| Colonne misurate col font | `.conf-table` (`table-layout:fixed`), `.c-home/.c-fgn/.c-name/.c-num/.c-flag/.c-state` |
| Blocco della vIPI ACC che si chiude | `details.acc-block` + fisarmonica in `vipi-editor.js` |
| Espandi/comprimi tutto (ogni editor) | `vipiEditorSections` — sezioni bespoke, blocchi ACC e card `.cb` |
| Portare un'ancora a schermo | `scrollAfterLayout` — `vipi-ui.js` (due rAF + correzioni) |
| Aiuto della sezione | mappa `HelpByKey` in `DocumentSectionsEditor` |
| Larghezza piena (chiavi condivise) | `Ed_Wide` / `Ed_Narrow` / `Ed_WideTitle` / `Ed_NarrowTitle` |

## Esempi misurati (le carte)

- [Accordi di coordinamento — densità](../feature/2026-08-19-accordi-densita-ui.md): la prima testata in riga,
  le colonne fisse, l'altezza misurata sopra tre colonne.
- [Struttura — densità](../feature/2026-08-19-struttura-densita-ui.md): prosa nei «?», barra unica, chip che non
  saltano, pannelli con il solo corpo che scorre.
- [Pagina ACC — densità](../feature/2026-08-19-acc-admin-densita-ui.md): intestazioni che restano, la perdita
  dei limiti, la scelta per riga, lo zoom, i «?» fuori schermo.
- [Aeroporti — densità](../feature/2026-08-19-aeroporti-densita-ui.md): due pannelli misurati al posto di una
  pagina da 13 745px, il `thead` fermo dentro lo scroller, l'avanzamento vero delle azioni di gruppo e i
  falliti che restano selezionati.
- [Editor ACC — densità](../feature/2026-08-20-editor-acc-densita-ui.md): la pagina misurata **in modifica**
  (9 690px), i blocchi che si chiudono a fisarmonica, la prosa di sezione nella riga-titolo, e i due difetti
  vecchi trovati guidandola — il tour che scorreva da solo e il «?» chiuso che allargava la pagina.
- [Permessi — densità](../feature/2026-08-22-permessi-densita-ui.md): la pagina che misurata **vuota**
  mentiva di mille pixel, le sei card di navigazione diventate una barra sola e completa, e l'elenco
  riorganizzato intorno alla domanda della pagina («chi può cosa») invece che intorno alla tabella.
- [Versioni — densità](../feature/2026-08-21-versioni-densita-ui.md): il riepilogo che spariva perché i chip
  hanno cominciato a contare, le azioni salite dalla riga al pannello (118→63px), e i sei difetti visti
  guardando gli screenshot — fra cui un sottotitolo che parlava di uno storage droppato da tre settimane.
- [Confinanti — densità](../feature/2026-08-20-confinanti-densita-ui.md): il dettaglio che esce dalla tabella
  e va in un pannello a fianco, una mappa sola al posto di due, le colonne misurate col font, l'import che
  dice a che punto è; e sei difetti che nessuna asserzione cercava — fra cui il `catch` che inghiottiva la
  cancellazione, trovato dal test e non guidando.
- [Editor aeroporto — densità](../feature/2026-08-20-editor-aeroporto-densita-ui.md): la tabella da 206 righe
  con la riga da 128px, il riquadro col tetto, la larghezza piena e i due stati (modificata / scelta) che
  erano diventati uno; poi le larghezze misurate col font con Condition elastica, e i tre gesti (chip a scala,
  scelta di gruppo col doppio clic, «applica alle scelte»).
