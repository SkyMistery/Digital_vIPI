# Regole di densità e uso per le pagine admin (19-23 agosto 2026) — 224 voci in 31 gruppi

> ⚠️ **I colori e i font non stanno qui: stanno in [regole-brand](regole-brand.md).** Dal 22 agosto il
> foglio non contiene piu' colori letterali fuori dalla scala di brand, e c'e' un tema scuro: una
> regola nuova che scrive un `#rrggbb` lo rompe in silenzio.
>
> **A cosa serve.** Fra il 16 e il 20 agosto sette pagine admin sono state rifatte nella forma —
> [accordi](../feature/2026-08-19-accordi-densita-ui.md), [struttura](../feature/2026-08-19-struttura-densita-ui.md),
> [ACC](../feature/2026-08-19-acc-admin-densita-ui.md), [aeroporti](../feature/2026-08-19-aeroporti-densita-ui.md),
> [editor aeroporto](../feature/2026-08-20-editor-aeroporto-densita-ui.md),
> [editor ACC](../feature/2026-08-20-editor-acc-densita-ui.md),
> [confinanti](../feature/2026-08-20-confinanti-densita-ui.md)
> — e il 21 agosto **versioni** in due giri: prima la parte **lock e azioni**
> ([carta](../feature/2026-08-21-versioni-lock-e-azioni.md)), che non era di forma ma di sostanza
> (§18, regole 95-105), poi la **densità** ([carta](../feature/2026-08-21-versioni-densita-ui.md), §19,
> regole 106-116); il 22 agosto **permessi** e **audit** ([carta](../feature/2026-08-22-permessi-densita-ui.md),
> §20, regole 117-124), e a chiudere il ramo **incarichi** in due giri
> ([carte](../feature/2026-08-22-incarichi-cosa-sono.md) e
> [densità](../feature/2026-08-22-incarichi-densita-ui.md), §26, regole 171-182), e infine gli **editor APP e
> vLOA** ([carte](../feature/2026-08-22-editori-app-vloa-cosa-fanno.md) e
> [densità](../feature/2026-08-22-editori-app-vloa-densita-ui.md), §27, regole 183-192), che chiudono la
> ricognizione; poi il **chrome** — topbar, pannello release e il menu «+ Blocco»
> ([carte](../feature/2026-08-22-topbar-larghezza-e-lingua.md) — poi [rifatta a misura](../feature/2026-08-22-topbar-misurata.md) — e
> [release](../feature/2026-08-22-pannello-release.md), §28, regole 193-204); e infine il **telefono** sulle
> pagine pubbliche ([carta](../feature/2026-08-22-telefono-pagine-pubbliche.md), §29, regole 205-212)
> — e ogni giro ha lasciato una regola pagata a caro prezzo,
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
22-bis. ⚠️ **Le media query non vedono questo zoom** (23 agosto 2026). `zoom` sta sull'`<html>`, e una `@media`
    valuta la **finestra**: a 1280 con zoom 1.4 il layout ha **914 unità** e `@media (max-width:900px)` non
    scatta. Quindi **una soglia di viewport non può governare un difetto che compare a zoom alto**: non si
    sposta, si **toglie**. È la stessa diagnosi della topbar (§30), ritrovata sulle tabelle del viewer.
22-quater. ⚠️ **E dal 27 agosto 2026 le pagine di lavoro non hanno più una sola `@media` di larghezza**: lo
    scaglione lo sceglie `vipiFitPanes` misurando il riquadro, e scrive sul `.wrap` le classi cumulative
    `pw-1200` / `pw-1180` / `pw-1080` / `pw-900` / `pw-760`. Una regola che deve valere «da qui in giù» si
    scrive `.struct.pw-900 .qualcosa{…}`, mai dentro una `@media`.
    ⚠️ **La `@container` — la cura del viewer — qui NON si può**: `container-type` porta con sé
    `contain:layout`, che rende il riquadro contenitore anche per i `position:fixed`, e il `DeleteDialog`
    (`.del-card`, centrato sullo schermo) vive dentro la riga di una tabella.
    ⚠️ E il riquadro va riagganciato quando Blazor lo **rifà**: su una pagina interattiva quello del primo
    disegno non è quello che si vede.
22-ter. Per misurare sotto zoom: `scrollWidth` e `clientWidth` stanno **entrambi** in unità di layout ed è la
    coppia da usare; `getBoundingClientRect().width` e `innerWidth` stanno in pixel di finestra. Mescolarli
    dà tabelle di numeri che non tornano — al primo giro di misura ha fatto sembrare colpevole la topbar,
    che non c'entrava (tolta dal DOM, lo sforo restava identico).

## 5. Tabelle lunghe

22-quater. **Una tabella che non ci sta scorre dentro il suo contenitore, a qualunque larghezza** — non sotto
    una soglia. `.wrap *:has(> table){overflow-x:auto;min-width:0}`: quando la tabella ci sta, cioè quasi
    sempre, non compare nessuna barra. ⚠️ **Il colpevole non è quasi mai quello che dichiara un `min-width`**:
    a zoom 1.4 sul viewer aeroporto sforava `.rwy-table`, che non dichiara niente e pretende **570 unità** per
    il suo contenuto, mentre `.sid-table` col suo `min-width:720px` stava già dentro un contenitore che scorre.
    Si misura, non si deduce dal foglio.
22-quinquies. In una griglia a colonne uguali servono **due** cose: `repeat(N,minmax(0,1fr))` **e**
    `min-width:0` sui figli. Il primo azzera il minimo della **traccia**, il secondo quello dell'**elemento**;
    da solo, il primo lascia il pavimento al min-content del blocco e la riga sfonda comunque (`.apt-2col`).
22-sexies. Il minimo di un titolo è la sua **parola più lunga**, che in unità di layout non si accorcia mai:
    `overflow-wrap:anywhere` sui titoli vale **sempre**, non dentro una media query. A zoom 1.8 «Livelli di
    transizione» pretende 177 unità in una colonna che ne ha 82.

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
46. **Un VID che si vede è un link al profilo IVAO** — mai un numero nudo. Si scrive col componente
    `VidLink` (`Vid`, più `Nome` se lo si conosce, più `SoloNumero` nelle colonne che hanno già «VID» in
    intestazione), che è **l'unico posto** dove sta l'indirizzo `ivao.aero/Member.aspx?Id=<VID>`.
    ⚠️ Il perimetro è **dove il VID si VEDE**, non dove lo si potrebbe ricavare: dove a schermo c'è il nome
    (Registro, `ReleasePanel`, Incarichi) resta il nome, perché la colonna è larga quanto un nome e
    appenderci «(VID 123456)» su cinquecento righe la taglia. Lì il link compare solo sul ripiego «VID …»,
    quello che scatta quando il roster il nome non ce l'ha.
    ⚠️ Non è premibile dentro un `<button>` (una chip) né dentro un `<option>`: lì il markup non entra, e
    il numero resta testo.
46-bis. **E se il VID sta DENTRO una frase** — le frasi del narratore del Registro, «Deciso da …»,
    «Assegnato da …» — il componente è `VidText`, che prende la frase **già composta** e taglia sulla forma
    «VID 1234567». ⚠️ Non si spezza la chiave di traduzione in pezzi, e non si passa per `MarkupString`:
    quelle frasi portano dentro titoli e note scritti da persone. Trovato dalla verifica live — nove VID
    muti sul solo Registro, e nessun test li guardava.
    Carta: [VID → profilo](../feature/2026-08-25-vid-porta-sul-profilo-ivao.md).

## 9. CSS: tre trappole che si ripagano

44. Le regole delle pagine di struttura vanno scritte con **`.struct` davanti** e verificate sul **valore
    calcolato**: contro `.res-table`/`.inline-form` una regola da due classi perde **in silenzio**, e il difetto
    si vede solo misurando in pagina.
45. Le classi che mette il **JS** sono **proprie** (`help-flip`, `help-up`, non `left`): una classe messa a mano
    da chi ha scritto la pagina è una decisione, e toglierla d'ufficio la cancella.
45-bis. Un campo con `list=` **non è largo quanto sembra**: Chromium disegna la freccia dell'elenco DENTRO il
    campo e si prende ~11px di larghezza utile — in una colonna stretta, l'ultima lettera. Si toglie con
    `input::-webkit-calendar-picker-indicator{display:none !important}`, e qui `!important` **serve davvero**:
    su questo pseudo-elemento lo stile del browser vince su quello dell'autore, quindi la regola 44 non
    aiuta — alzare la specificità non cambia niente (provato con selettore corto, con l'attributo e con
    `.struct .res-table.sid-edit` davanti: 71px in tutt'e tre i casi). Misurato con `scrollWidth` contro
    `clientWidth`, non a occhio.

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

### Già a norma — diciassette pagine: TUTTE. Sono loro ad aver prodotto le regole

| Pagina | Rotta | Altezza | Note |
|---|---|---:|---|
| Accordi di coordinamento | `/services/vsop/admin/transfers` | 900 | testata in riga, altezza misurata, colonne fisse |
| Struttura | `/services/vsop/admin/sector-structure` | 900 | testata in riga, due pannelli con il solo corpo che scorre |
| ACC | `/services/vsop/admin/acc` | 8714 | testata appiccicata, `thead` fermo su entrambe le tabelle (28 e 152 righe) |
| Aeroporti | `/services/vsop/admin/airports` | 900 | **da 13 745**: due pannelli misurati, `thead` fermo dentro lo scroller, azioni di gruppo con avanzamento ([carta](../feature/2026-08-19-aeroporti-densita-ui.md)) |
| Editor aeroporto | `/services/vsop/{acc}/airports/editor` | 4 913 | **da 31 286** su LIRF (206 SID): riquadro col tetto e `thead` fermo, riga 128→45px, larghezza piena, modificata≠scelta ([carta](../feature/2026-08-20-editor-aeroporto-densita-ui.md)). L'altezza non dipende più dai dati |
| Editor ACC | `/services/vsop/{acc}/editor` | 5 595 | **da 9 690 in MODIFICA** (in lettura erano 6 466): blocchi collassabili con fisarmonica, testata in riga, prosa nei «?», riga frequenze 60→43px ([carta](../feature/2026-08-20-editor-acc-densita-ui.md)). Tutto compresso: 1 468 |
| Confinanti (vLOA) | `/services/vsop/admin/neighbours` | 900 | **da 2 515 chiusa** (aperta era molto peggio: il dettaglio srotolava tabella + due mappe dentro la riga): due pannelli misurati, dettaglio a destra, una mappa sola, colonne misurate col font, import con avanzamento e Interrompi ([carta](../feature/2026-08-20-confinanti-densita-ui.md)) |
| Versioni | `/services/vsop/versions` | 900 | **da 1 664**: due pannelli misurati col dettaglio a destra (era dentro l'elenco), chip dei filtri che **contano** al posto della fascia di riepilogo, azioni nel pannello, riga 118→63px ([carta](../feature/2026-08-21-versioni-densita-ui.md) — la parte lock e azioni è la [sua](../feature/2026-08-21-versioni-lock-e-azioni.md)) |
| Permessi | `/services/vsop/admin/permissions` | 900 | **da 2 449** (misurata con 16 concessioni: a tabella vuota diceva 1 346): barra admin al posto delle sei card, una riga per persona ([carta](../feature/2026-08-22-permessi-densita-ui.md)). ⚠️ Dal 29 agosto 2026 la pagina assegna **livelli**, non concessioni per ACC: stessa forma a due pannelli |
| Audit | `/services/vsop/admin/audit` | 900 | **da 13 293** misurata con 248 righe (la ricognizione diceva 1 556 con 28, ed era il numero **col tetto**): un pannello misurato col `thead` fermo, ogni riga una frase al posto del JSON, periodo al posto del tetto muto ([carte](../feature/2026-08-22-audit-cosa-registra.md) e [densità](../feature/2026-08-22-audit-densita-ui.md)). Resta 900 con 500 righe e da zoom 0.8 a 1.5 |
| Sorgenti | `/services/vsop/admin/sources` | 900 | **da 1 252**, ma il numero non era il problema: la pagina prometteva «l'import non la tocca più» ed era **falso per Settori, TA e Piste** (gate assenti o solo in un chiamante). Una tabella al posto di due, chi ha deciso la policy, il cambio nel registro ([carte](../feature/2026-08-22-sorgenti-cosa-fa-la-policy.md) e [densità](../feature/2026-08-22-sorgenti-densita-ui.md)). `max-height` e non `height`: il contenuto è corto e fisso. Poi **sette righe invece di sei** e il giro giornaliero di TA e piste ([carta](../feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md)): il tetto regge, perché misura il contenuto |
| Diagnostica | `/services/vsop/admin/diagnostics` | 900 | **da 1 349** misurata con otto rilievi (la ricognizione diceva 900 col report **vuoto**), e resta 900 con **76**: due colonne, riquadro misurato col `thead` fermo, chip per **area**, «Dove si ripara» ([carte](../feature/2026-08-22-diagnostica-cosa-afferma.md) e [densità](../feature/2026-08-22-diagnostica-densita-ui.md)). ⚠️ Qui `height` e non `max-height`: il contenuto è più alto dello schermo per mestiere |
| Nuovo documento | `/services/vsop/editor/new-document` | 900 | **da 957** sulla scheda vLOA (le altre tre erano già 900 — ⚠️ su una pagina a schede si misura **ogni scheda**): campi in griglia, tasto **sotto** i campi che gli servono, schede riordinate, barra admin **senza** voce nell'elenco ([carte](../feature/2026-08-22-newdoc-cosa-crea.md) e [densità](../feature/2026-08-22-newdoc-densita-ui.md)). `max-height`: corto e fisso a zoom 1, non a 1.25 |
| Incarichi admin | `/services/vsop/admin/tasks` | 900 | **da 1 813 con 12 incarichi e 4 764 con 60** (la ricognizione diceva 900: `EditorTasks` è **vuota** nel DB di sviluppo — terza volta): elenco+dettaglio con le azioni nel pannello, `thead` fermo, chip che contano col **default «non conclusi»** al posto di un'archiviazione, avanzamento per editore da parete di card a chip che filtrano ([carte](../feature/2026-08-22-incarichi-cosa-sono.md) e [densità](../feature/2026-08-22-incarichi-densita-ui.md)). Resta il viewport con 60 |
| Incarichi (utente) | `/services/vsop/tasks` | 900 | **da 1 562 con 12 propri** (a 1280×800 scorreva con **quattro**): tre colonne a schermo e due chiuse col conteggio, card con un avanzamento invece di quattro tasti, `vipiCapViewport` **con riserva** per le colonne chiuse. La briciola resta: è una pagina d'utente |
| Editor APP | `/services/vsop/{acc}/apps/editor` | 3 350 **in modifica**, 1 654 compresso | **da 3 540** (la ricognizione diceva 900: era una misura in LETTURA sui dati di sviluppo): testata in riga col lock, «?», espandi/comprimi, larghezza piena, «+ Blocco» al posto di quattro tasti per sezione ([carte](../feature/2026-08-22-editori-app-vloa-cosa-fanno.md) e [densità](../feature/2026-08-22-editori-app-vloa-densita-ui.md)) |
| Editor vLOA | `/services/vsop/{acc}/vloa/editor` | 4 242 **in modifica**, 1 359 compresso | **da 4 351**, con 177px di fasce in testa: il callout bilaterale nel «?», e ⚠️ i chip che SCRIVEVANO nel documento diventati un elenco, perché stavano sopra i chip che non scrivono (stesse carte) |

L'altezza 900 delle prime due **è** il viewport: la pagina non scorre, il riquadro sì.

### Da rifare: **nessuna**

Il ramo ha chiuso **tutte** le pagine di lavoro della ricognizione. L'ultima coppia — gli editor APP e vLOA —
è caduta il 22 agosto, e ⚠️ **il loro «900» era una stima**: misurati davvero, e **in modifica**, erano 3 540
e 4 351.

⚠️ **La larghezza a cui vale questa tabella**: le pagine admin e gli editor si usano **da 1024 in su**
(desktop o tablet orizzontale), ed è lì che sono misurate — 1600 / 1440 / 1280 / 1024, IT ed EN. Sotto quella
soglia non sono supportate, per scelta: il telefono riguarda le **pagine pubbliche** (§29).

⚠️ **E dal 22 agosto non resta aperto nemmeno il chrome**: la topbar sta a 1024 (§28) e il pannello release
è passato da 974 a 420px. L'unica cosa rimasta è l'**archiviazione degli incarichi**, che vuole una
migrazione e aspetta il cutover MariaDB.

⚠️ **Il metro «sottotitolo sì/no» non si misura con `.doc-head .muted`**: su Struttura quel selettore pesca
«Sola lettura» della barra del lock e risponde «c'è un sottotitolo» su una pagina che non ce l'ha. I «?» si
contano bene (`.help-hint`), i sottotitoli si guardano.

### Fuori ambito: le viste pubbliche

`/services/vsop`, `/services/vsop/{acc}`, i viewer (vIPI ACC, aeroporto, APP, vLOA), gli elenchi pubblici, `/services/vsop/changed`,
`/services/vsop/search`, `/services/vsop/live`, `/services/vsop/guide`, l'anteprima release, l'AoR 3D. Lì il contenuto **è** la pagina e si
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
    hide/delete ai soli `IsAdmin` mentre il servizio autorizzava per **concessione d'ACC**: chi il permesso ce l'aveva
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
125. **Una barra di navigazione si filtra da sé, non con un `@if` copiato undici volte.** `AdminNav` sta ora
     in cima a *tutte* le pagine admin, e ogni voce si porta dietro la propria regola d'accesso
     (`Chi.Admin` / `Chi.Chiunque`). Le pagine la scrivono nuda — `<AdminNav />` — perché la regola 120
     («un elenco di scorciatoie va dentro il ramo autorizzato») è vera ma non si difende ripetendo il
     cancello in undici punti: si difende mettendola **una volta** accanto alla voce. Il giorno che una
     pagina cambia autorizzazione, si cambia una riga. ⚠️ Se all'utente resta **una voce sola** — che è poi
     la pagina in cui è già — la barra non si rende affatto: una voce non è una navigazione.
126. ⚠️ **Una barra che sta su undici pagine non può interrogare la banca dati.** Girerebbe mentre ognuna di
     quelle pagine carica i propri dati, sullo stesso `DbContext` di circuito: è la ricetta esatta per
     «A second operation was started on this context». Le regole d'accesso della barra si rispondono con
     `IsAdmin`, che il servizio risolve una volta per scope. Se una voce avesse bisogno di un dato dal DB,
     la risposta è cambiare la regola, non aggiungere una query.
127. **La barra va FUORI dalla testata, anche quando la testata è appiccicata.** Su ACC (`.st-head.sticky`)
     dentro la testata resterebbe a schermo per sempre, raddoppiando l'altezza della fascia fissa — e
     `vipiStickyOffset`, che misura quella fascia per incollarci sotto il `thead`, spingerebbe giù di
     altrettanto le intestazioni di tutte le tabelle. Fuori, scorre via verso l'alto e passa **sotto** la
     testata, che ha fondo pieno e `z-index` più alto. Vale identico da sopra il titolo: misurato dopo lo
     spostamento, `--st-head-h` resta 72px e la barra a `bottom:-765` dopo 900px di scorrimento.
128. **Quando arriva l'elenco completo, i link sparsi in testata sono doppioni e vanno via.** Con la barra su
     ogni pagina se ne sono tolti sette: cinque in Struttura (ACC, Aeroporti, Confinanti, Trasferimenti,
     Sorgenti — sotto ~1280px mandavano la testata a capo, quindi toglierli restituisce una riga intera) e
     due «← Struttura» in ACC e Confinanti. In testata restano solo i comandi **di quella pagina**.
     ⚠️ E «di quella pagina» va preso alla lettera, anche quando il comando non è una navigazione: «Nuovo
     documento» stava in testata a Struttura, che i documenti non li elenca e non li tiene. Un documento si
     crea da **Documenti**, dove il tasto c'era già — quello di Struttura era solo un secondo ingresso
     tenuto in vita dall'abitudine. La testata di Struttura ora non ha comandi: solo lo stato e il lock.
129. ⚠️ **Un'etichetta sbagliata si vede, un URL sbagliato no**: porta a una pagina bianca, e solo a chi ci
     clicca sopra. La rete è un test che confronta ogni voce con le `RouteAttribute` vere dell'assembly
     (`AdminNavTests.Ogni_voce_punta_a_una_rotta_che_esiste`) — e deve aggiungere a mano la voce corrente,
     che un href non ce l'ha.
130. **Un'etichetta che ripete il contesto non è un'etichetta, è rumore.** La barra portava davanti la scritta
     «Admin:». Compariva su undici pagine per dire una cosa che si vede da sola — sei nell'area staff, e le
     voci dicono già quali pagine sono. Tolta: il nome della barra resta nell'`aria-label`, dove serve
     davvero, cioè a chi la incontra senza il colpo d'occhio.
131. **La barra sta SOPRA il titolo, e prende il posto della briciola di pane.** Due mosse che sono una sola.
     Sopra e non sotto perché **un titolo deve toccare il contenuto che intitola**: da sotto, la barra si
     infilava fra l'H2 e la prima sezione e li staccava, e il titolo finiva a fare da didascalia alla barra
     invece che alla pagina. E al posto della briciola perché la briciola faceva già quel lavoro, peggio:
     ⚠️ ogni suo anello portava dove porta la barra (o il logo in topbar, per «Home»); segnava la pagina
     corrente in grassetto come la barra la segna in blu; **inventava una gerarchia** — «Home › Staff Area ›
     Structure › Airports», ma Aeroporti sotto Struttura non ci sta, sono pagine sorelle; ed era già
     divergente da sé, con «Admin» che puntava a `/services/vsop` in tre pagine e a `/services/vsop/admin/permissions` in una
     quarta, e profondità da due a quattro anelli per pagine dello stesso rango. Costava 38px misurati su
     tutte e undici, che sulle sei pagine ad altezza misurata erano righe e nodi.
132. ⚠️ **Una briciola di pane si toglie solo dove qualcos'altro la sostituisce.** Sulle pagine pubbliche
     (`/services/vsop/{acc}/airports?icao=…` e sorelle) resta, e deve restare: lì non c'è nessuna barra, e
     «Home › LIBB › Airports › LIBD» è l'unico modo di risalire. La regola non è «le briciole sono rumore»,
     è «due elenchi della stessa cosa divergono» (regola 123) — e senza il secondo elenco non c'è niente da
     togliere.

## 21. Quello che ha lasciato il giro della barra admin (22 agosto)

Non è il giro di **una** pagina: cambia la testa di **tutte e undici**. Regole 125-132.

- **La barra sta in `AdminNav`, e con lei la regola d'accesso di ogni voce** (`Chi.Admin` / `Chi.Chiunque`).
  Le pagine scrivono `<AdminNav />` nuda. Cambiare chi entra in una pagina è **una riga**, non undici `@if`.
- **Sopra il titolo, al posto della briciola di pane.** Il titolo torna a toccare il contenuto che intitola, e
  il terzo elenco degli stessi link sparisce (−38px su tutte e undici).
- **Niente etichetta «Admin:»** davanti alle voci: il nome della barra vive nell'`aria-label`.
- **Sette link tolti dalle testate** (cinque in Struttura, due «← Struttura» in ACC e Confinanti) e con loro
  «Nuovo documento», che non era di Struttura: la testata di Struttura non ha più comandi.
- ⚠️ **Tre pagine mandavano la barra a capo**: Audit e Diagnostica (`.wrap` a 1 100px) e Incarichi admin
  (1 200px). 87px invece di 55. Non è un difetto — quelle pagine scorrono — ma il `max-width` è una scelta di
  larghezza di lettura, e chi lo cambia deve sapere che cambia anche questo. **Audit e Incarichi admin sono
  uscite da questo elenco il 22 agosto**: il loro giro ha tolto il `max-width`, e la barra sta in una riga.
  Resta **Diagnostica**.
- **Rete**: `AdminNavTests` (5 casi), fra cui il confronto di ogni voce con le `RouteAttribute` vere
  dell'assembly — un'etichetta sbagliata si vede, un URL sbagliato no.

## 22. Quello che ha lasciato il giro Audit (22 agosto)

Decima pagina, e come su Versioni la **sostanza è venuta prima della forma**: aprendo `/services/vsop/admin/audit`
per il `thead` appiccicato si è scoperto che il registro non registrava l'eliminazione di un documento,
attribuiva la revoca di un permesso alla persona sbagliata, e prometteva nel sottotitolo una categoria di
eventi che nessuno aveva mai scritto. Carte:
[cosa registra](../feature/2026-08-22-audit-cosa-registra.md) e
[densità](../feature/2026-08-22-audit-densita-ui.md). Regole **133-142**.

133. ⚠️ **Una misura è una fotografia, e una pagina che accumula la smentisce da sola.** La ricognizione dava
     Audit a 1 166px, tre giorni dopo erano 1 556 senza che nessuno l'avesse toccata, e **con un registro
     vero (248 righe) sono 13 293**. Le altre pagine crescono quando cresce il lavoro; un registro cresce
     **per sempre**, perché non si accorcia mai. Su queste pagine la misura si rifà, non si cita — e si rifà
     **con i dati** (regola 117).
134. ⚠️ **Un tetto muto è peggio di un elenco lungo.** Il lettore tagliava a 200 righe senza dirlo: chi
     guardava vedeva un elenco che sembrava completo. Il tetto resta (è una difesa della query) ma il filtro
     diventa il **periodo**, e quando il tetto morde la pagina lo **dichiara** con i due numeri.
135. **Il dato grezzo non si butta e non si mette in colonna.** Il JSON dei dettagli era la colonna più larga
     della tabella e non lo leggeva nessuno. Ora la colonna porta la **frase** e il JSON sta nel `title`
     della cella: resta consultabile senza costare larghezza a ogni riga.
136. ⚠️ **Un registro deve restare vero quando l'entità di cui parla non esiste più.** Per questo la riga
     porta il **nome** accanto all'Id (titolo del documento, callsign del nodo, nome di chi teneva il lock):
     «eliminato il documento 7» non distingue una pulizia da un incidente, e dopo la cancellazione il titolo
     non è più recuperabile da nessuna parte. Vale al momento della **scrittura**, non della lettura — ma chi
     legge fa la sua parte: i titoli che le righe vecchie non portano si risolvono con **una** query per
     pagina (mappa Id→titolo), mai una per riga, e ⚠️ **la mappa non vince sul titolo scritto nella riga**:
     se il documento è stato rinominato, il registro racconta il passato, non il presente.
137. ⚠️ **Il vocabolario vecchio si legge, non si riscrive.** La revoca di un permesso è stata `Archive` fino
     al 22 agosto e `Delete` dopo; la chiave dell'ACC nei dettagli è stata `acc` e poi `Acc`. Chi rende gli
     eventi accetta **entrambe** le forme e dice la stessa frase: una migrazione dei dati storici di un
     registro sarebbe la cosa più sbagliata da fare proprio su un registro.
138. **Il non-evento non si scrive.** Nascondere ciò che è già nascosto, rimettere lo stesso padre, forzare
     un lock che non c'è: nessuna riga. Su un elenco che cresce per sempre, le righe che dicono «non è
     cambiato niente» sono l'unico modo garantito di renderlo illeggibile.
139. **Un formattatore per un tipo di dato, non uno per pagina.** La stessa riga di audit era resa in due
     modi da due pagine, ed **entrambi** erano rotti a modo loro (JSON crudo su Audit; su Versioni un parser
     per chiavi che nessuno scrive, che ritornava sempre vuoto). `AuditNarrator` è condiviso: due pagine che
     mostrano lo stesso fatto non possono più divergere. Il parser morto non si è cancellato, si è
     **sostituito** — cancellarlo avrebbe lasciato il buco senza chiudere la causa.
140. **Elenco+dettaglio non è la risposta a ogni tabella.** Su Confinanti, Versioni e Permessi il pannello a
     destra serve perché il dettaglio è un oggetto su cui si **agisce**. Qui la riga è già tutto il fatto e
     il registro non si modifica: un pannello sarebbe stato un terzo di schermo speso per rileggere la riga.
     **Il pannello si giustifica con l'azione, non con l'abitudine.**
141. ⚠️ **`vipiFitViewport` misura fin dove arriva il riquadro, non cosa gli sta sotto.** Col padding di fondo
     del `.wrap` la pagina restava 52px più alta del viewport e scorreva per niente. Serve la stessa regola
     già scritta per gli altri layout: `.wrap.struct:has(.audit-pane){padding-bottom:18px}`.
142. ⚠️ **Provando lo zoom, usare la funzione della pagina** (`vipiSetZoom`), non `style.zoom` scritto a mano:
     a mano non scatta il `resize`, quindi il riquadro non rimisura e **l'attrezzo denuncia un difetto che
     non c'è**. Stessa famiglia della regola delle due unità: `scrollHeight` (unità di layout) non si
     confronta con `innerHeight` (px di finestra) — il confronto giusto è con `clientHeight`.


## 23. Quello che ha lasciato il giro Sorgenti (22 agosto)

Undicesima pagina, e la terza di fila in cui **la sostanza è venuta prima della forma**: aprendo
`/services/vsop/admin/sources` per la densità è saltato fuori che la promessa scritta nella pagina — «escludi una
categoria e l'import non la tocca più» — era falsa per **due categorie su cinque**. Carte:
[cosa fa la policy](../feature/2026-08-22-sorgenti-cosa-fa-la-policy.md) e
[densità](../feature/2026-08-22-sorgenti-densita-ui.md). Regole **143-152**.

143. ⚠️ **Un gate per categoria, non uno per chiamante.** Il gate dei Settori non c'era in nessuno dei
     quattro import (job 24h, bottone dell'editor, massivo, «Genera documenti»); quello di TA e Piste c'era
     nel reimport dell'editor e **non** nella generazione documento, che chiama lo stesso merge. Il gate va
     nel **corpo condiviso** auto/manual e prima della fetch — è la stessa lezione già scritta per le aree
     regolamentate, applicata a metà. Gemella della regola 139: come il formattatore, anche la **decisione**
     sta in un posto solo.
144. ⚠️ **Una pagina che descrive un meccanismo va riletta contro il meccanismo.** Il testo diceva «non la
     tocca più» ed era vero per SID e Aree, falso per Settori, TA e Piste. Prima di rendere bella la pagina
     si verifica che **dica la verità**: la prosa che promette è codice non eseguibile, e invecchia senza
     avvisare.
145. **L'atto che cambia il regime di scrittura di tutta l'applicazione va nel registro.** Cambiare le
     spunte decide quali dati la sorgente può sovrascrivere: era l'ultimo atto amministrativo muto dopo il
     giro Audit. Nella riga stanno **solo le categorie cambiate**, divise per verso — e il non-evento non si
     scrive (regola 138): un salvataggio che non cambia niente non lascia riga **e** non riscrive «deciso
     da X» su una decisione presa da qualcun altro.
146. ⚠️ **Un default di colonna non è una decisione, e dal valore non si distingue.** `ImportSids` è nato
     `false` su un DB già popolato: in produzione «SID escluse» può essere una scelta dell'admin o l'effetto
     di una migration, e **il flag non lo dice**. La cura non è indovinare: è mostrare **chi** ha deciso e
     **quando**, e dichiarare in pagina che `UpdatedByUserId = 0` significa «nessuno l'ha mai salvata».
     Vale per ogni tabella di configurazione con un autore che nessuno legge.
147. ⚠️ **Il verde non si regala: la scelta vince sullo stato.** `GatedImportLoop` marca il successo quando
     il run non lancia eccezioni, e con la categoria esclusa il run esce subito **senza fare niente**: la
     tabella mostrava «ok, oggi» per un import che per scelta non importa nulla. E la stessa bugia si
     ripresenta appena la si sposta di una cella — una categoria esclusa che annuncia il **prossimo giro**,
     una ferma che annuncia un prossimo **già passato**, una esclusa che mostra l'**errore** di un giro che
     non la riguarda più. Il gate non era sbagliato: era sbagliato il **racconto**, e si corregge dove si
     racconta.
148. **Un elenco intitolato «X» non contiene ciò che X non è.** Nella tabella degli stati comparivano
     `SpecialAreaForeignOptOut` e `TransferFlowsToAgreements`, che sono segnaposti «già fatto» e non import.
     La tabella che li ospita è comoda; il fatto che li ospiti non li rende della stessa specie.
149. ⚠️ **Due tabelle della stessa cosa hanno due vocabolari, sempre.** Sopra «Settori», sotto
     `AirportSector`; sopra «da sorgente / manuale», sotto «ok / errore»; e il join lo doveva fare a mente
     chi legge — sbagliandolo. Una riga per cosa, e le etichette da **un** posto solo
     (`ImportCategoryLabels`, condiviso con il narratore degli eventi). Gemella della regola sui due elenchi
     di pagine admin che divergevano.
150. ⚠️ **`max-height` o `height`? Dipende da cosa c'è dentro, e la differenza si vede a occhio.**
     `vipiFitViewport` (`height`) è giusto dove il contenuto è più alto dello schermo **per mestiere**: là
     stirare il riquadro e far scorrere l'interno è tutto guadagno. È sbagliato dove il contenuto è corto e
     **fisso**: qui sei righe lasciavano mezzo riquadro di bianco. Senza misura affatto, però, a 1024×768 e
     da zoom 1.25 la pagina tornava a scorrere. Da qui `vipiCapViewport`, che scrive **`max-height`**: alto
     quanto il contenuto quando ci sta, e dentro scorre solo quando non ci starebbe. ⚠️ «La pagina non
     scorre» non è l'obiettivo — l'obiettivo è che **ciò che si guarda stia a schermo**.
151. ⚠️ **Due tasti con la stessa parola a due centimetri l'uno dall'altro.** Con la conferma in linea aperta,
     l'«Annulla» del componente stava accanto all'«Annulla» della pagina, e i due fanno cose diverse: uno
     chiude la domanda, l'altro butta le modifiche. Nessuna misura lo trova; si vede **guardando la
     schermata**. Chi mette un tasto accanto a un `InlineConfirm` gli dà un nome che il componente non usa.
152. ⚠️ **`.se-row input{flex:1}` è la regola dei CAMPI DI TESTO, e si applica anche alle checkbox.** Le
     caselle delle sei righe si allargavano a riempire la cella, e a schermo non erano incolonnate — lo
     scostamento dipendeva dalla lunghezza della parola accanto. Un'altra faccia di «una classe non può
     significare due cose» (regola del `.sector-pick`): `.se-row` è la riga di un **form**, e una cella di
     tabella non è un form.


## 24. Quello che ha lasciato il giro Diagnostica (22 agosto)

Dodicesima pagina, e la **quarta di fila** in cui la sostanza è venuta prima della forma. Il giro precedente
aveva lasciato scritto «prima di renderla bella, verificare che dica il vero», e qui la verifica ha trovato
il difetto peggiore del ramo: **la pagina che diagnostica i guasti moriva se ne aveva uno**. Carte:
[cosa afferma](../feature/2026-08-22-diagnostica-cosa-afferma.md) e
[densità](../feature/2026-08-22-diagnostica-densita-ui.md). Regole **153-162**.

153. ⚠️ **Chi raccoglie i guasti degli altri deve proteggersi dai propri.** Le cinque parti del report
     giravano in fila senza protezione: una sonda che lanciava uccideva il circuito Blazor, e — peggio — il
     guasto di **una** cancellava il lavoro di **tutte** le altre, perché il report è una lista sola
     costruita in ordine. Un problema del server di database nascondeva una pista orfana già trovata. La
     lezione stava scritta nella stessa cartella (`StartupMaintenanceReport`: «un guasto non deve uccidere
     il giro, ma non deve nemmeno restare zitto») e non era applicata a sé.
154. **Un guasto di sonda è un rilievo, e porta l'area del pezzo che non è riuscito.** Altrimenti i conteggi
     per area direbbero «0 rilievi di schema» proprio quando la sonda dello schema non ha guardato niente. E
     il testo del rilievo dice la cosa che serve sapere: **in quest'area l'assenza di rilievi non vuol dire
     che vada tutto bene**.
155. ⚠️ **«La sonda è rotta» non è «il sito è giù».** Un guasto del report faceva uscire l'health check
     `Unhealthy` con lo stack: un monitor lo legge come «il sito non c'è» e sveglia qualcuno di notte. È
     `Degraded` con un messaggio leggibile — le condizioni critiche restano quelle della sonda `ready`.
156. ⚠️ **Un sottotitolo che promette MENO di quello che la pagina mostra è un difetto come quello che ne
     promette di più.** Qui diceva «incongruenze dei soft-ref» e nella stessa tabella comparivano schema,
     impostazioni del server, guasti d'avvio e «nessuno può editare» — che è il rilievo più grave che
     l'applicazione sappia produrre. È Audit al contrario, e costa uguale.
157. **Di chi è il problema è un dato, non una sfumatura del testo.** `ConsistencyArea` in cinque valori,
     **obbligatoria e senza default**: un default farebbe nascere un controllo nuovo nell'area sbagliata in
     silenzio. È anche l'unità giusta per i chip — le categorie sono tredici e crescono con ogni controllo,
     le aree sono cinque e rispondono alla domanda che si fa chi legge.
158. **Chi produce il rilievo è l'unico che sa dove si ripara.** La rotta sta sul rilievo (`Where`), non in
     una mappa categoria→rotta lato pagina: quella sarebbe un secondo posto da tenere allineato, e un
     controllo nuovo nascerebbe muto senza che il compilatore lo dica. ⚠️ E `null` è una risposta: server,
     schema e configurazione si correggono **fuori** dall'applicazione — in particolare «nessuno può
     editare» non manda alla pagina dei permessi, che è esattamente la porta chiusa di cui parla.
159. ⚠️ **Il testo di un rilievo va scritto due volte, e non è una ridondanza.** Grezzo per l'health check e
     i log — dove una lingua d'interfaccia non esiste e il rilievo nasce anche fuori da una richiesta HTTP —
     e come **chiave** per chi lo mostra. Localizzare alla scrittura non si può. Il patto del narratore è
     quello di `AuditNarrator`: **chiave sconosciuta ⇒ testo grezzo**, mai il nome della chiave a video (il
     localizzatore, quando non trova, restituisce la chiave *come valore*: senza il controllo su
     `ResourceNotFound` a schermo compare `Diag_Msg_Qualcosa`).
160. ⚠️ **La localizzazione a metà si vede.** Tradotti categoria e dettaglio, il **bersaglio** restava in
     italiano: «severe | Broken hierarchy | *Settore ACC* LGGG_W_CTR». Metà dei bersagli non è un
     identificatore ma una frase. Regola pratica: quello che va tradotto è ciò che è **prosa**; ciò che è un
     identificatore (`sql_mode`, `Documents.Title`) resta, perché tradurlo è inventargli un secondo nome.
     Stessa trappola in piccolo negli **argomenti**: un argomento è un valore, non una chiave — passare il
     nome di un pezzo come argomento lo fa comparire grezzo dentro una frase tradotta.
161. **Un pannello di destra si giustifica anche con «sono tre domande», non solo con l'azione.** La regola
     140 diceva che il dettaglio a destra vuole un'azione; qui a destra non c'è il dettaglio di una riga di
     sinistra, ci sono **due argomenti diversi**. Il motivo vero è misurato: la card delle immagini stava
     sotto 1 349px e ci sarebbe restata per sempre — più rilievi ci sono, più è lontana, ed è proprio quando
     si scorre meno.
162. ⚠️ **Due schede affiancate con due pesi tipografici diversi si leggono come due livelli**, e non lo
     sono. Quando un componente condiviso finisce accanto a un altro, la sua testata va allineata a quella
     del vicino — non lasciata com'era quando stava da solo in fondo alla pagina.


## 25. Quello che ha lasciato il giro Nuovo documento (22 agosto)

Tredicesima pagina, e la **quinta di fila** con un difetto di sostanza sotto la densità. Qui il difetto era
nel **nome**: la pagina si chiama «Nuovo documento» e per tre tipi su quattro non crea niente. Carte:
[cosa crea](../feature/2026-08-22-newdoc-cosa-crea.md) e
[densità](../feature/2026-08-22-newdoc-densita-ui.md). Regole **163-170**.

163. ⚠️ **Due porte che creano la stessa cosa hanno due politiche, sempre.** La vLOA si genera da
     «ACC confinanti» (idempotente per parti: «se esiste già, riusala») e si crea da «Nuovo documento» (che
     ne faceva sempre una nuova). Il contratto dichiarava «una sola vLOA per coppia» dal primo giorno e
     nessuno lo imponeva. ⚠️ E il resto dell'applicazione **non sa gestirne due**: la ricerca per coppia fa
     `FirstOrDefault`, quindi l'editor ne apre una senza un criterio e l'altra resta invisibile pur potendo
     avere release pubblicate. Gemella della regola 143 (un gate per categoria, non uno per chiamante).
164. **Rifiutare non è «riusare in silenzio».** L'import può riusare zitto — non c'è nessuno davanti. Una
     pagina no: chi ha appena scritto un titolo deve sapere **perché** non è stato usato, e il messaggio deve
     **nominare** quello che c'è già. E dire di no senza dire dove è mezza risposta: accanto al rifiuto ci va
     il link. ⚠️ La direzione conta: A→B e B→A sono due documenti legittimi, e confonderli sarebbe un difetto
     peggiore del duplicato, perché toglierebbe un documento vero.
165. ⚠️ **Un documento nasce con la struttura del suo catalogo, da qualunque porta.** Da qui la vLOA nasceva
     con **una** sezione a chiave *libera*, mentre dall'altra porta nasceva con le sette del profilo: usciva
     un documento **fuori catalogo**, con le obbligatorie assenti e l'unica presente sconosciuta a chi decide
     chi rende il corpo. La pagina lo **dichiarava** perfino — «la vLOA nasce vuota» — e un difetto
     documentato resta un difetto: la prosa che descrive un difetto lo rende accettabile, non lo cura.
166. ⚠️ **La porta non può essere più stretta della serratura.** Pagina dietro `IsAdmin`, servizi
     autorizzati per **grant di ACC**: il responsabile di un ACC non vedeva la pagina ma poteva creare lo
     stesso andando all'URL dell'editor. È la regola 95 (Versioni) in un altro punto. Il filtro delle tendine
     si fa con la **stessa** domanda che poi rifiuterebbe (`CanEditAccAsync`), non deducendola dai grant: due
     letture della stessa regola divergono.
167. **Un read-model di pagina è la risposta a «questa pagina ha bisogno di elenchi che sono admin-only».**
     Allentare gli elenchi globali per far entrare un ruolo cambierebbe i permessi anche delle pagine dove si
     **scrive**. Un servizio che filtra per chi guarda li lascia stretti. ⚠️ Filtra, non autorizza: una
     tendina è una comodità, non una guardia (regola 96).
168. **Un'etichetta che mente costa più di un'etichetta lunga.** «Crea e apri editor» su un bersaglio che ha
     già il documento diceva il falso; ora il tasto dice «Apri» quando apre. Non è un divieto — aprire ciò
     che c'è è quasi sempre ciò che si vuole — è un'etichetta che smette di mentire. Il dato («ha già un
     documento») viene da **chi lo possiede**, non da una seconda lettura dell'elenco documenti.
169. ⚠️ **Su una pagina a schede si misura OGNI scheda.** «La pagina» è la scheda che si apre per prima, e
     non è detto che sia quella che pesa. Qui erano 957 la vLOA e 900 le altre tre. E si misura anche in due
     **stati**: vuota e con un bersaglio scelto, perché è allora che compaiono le tendine dipendenti.
170. ⚠️ **Il tasto va DOVE FINISCE quello che gli serve.** Stava dopo il titolo e sopra i quattro menu
     obbligatori: si leggeva come se «titolo + Crea» bastasse, e chi lo premeva otteneva un errore che la
     pagina causava con la propria disposizione. Nessuna misura lo trova — si vede guardando la schermata, o
     confrontando il bordo del tasto con quello dei campi. E il **perché** di un tasto spento gli sta
     accanto, non solo in una barra in cima.


## 26. Quello che ha lasciato il giro Incarichi (22 agosto 2026)

Quattordicesima e quindicesima pagina — due, non una — e la **sesta di fila** con un difetto di sostanza
sotto la densità. Qui i difetti erano **dodici**, e i due peggiori li ha trovati la **verifica live**, non la
lettura del codice: un sottotitolo che prometteva un gesto inesistente, e una chiave fabbricata che non
ritrovava niente. Carte: [cosa sono](../feature/2026-08-22-incarichi-cosa-sono.md) e
[densità](../feature/2026-08-22-incarichi-densita-ui.md). Regole **171-182**.

171. ⚠️ **Un valore che la tendina usa come «non ho scelto» non può essere un valore valido.** L'opzione
     «Seleziona» valeva `0`, e `0` finiva nel database come assegnatario: nasceva un incarico **di nessuno** —
     invisibile a chi dovrebbe farlo, e non riassegnabile perché la riassegnazione non era in UI. La guardia
     sta nel **service** (la porta non può essere più larga della serratura, regola 166 al contrario), e il
     tasto è spento col **perché accanto** (regola 170). Con `@bind:event="oninput"`, o il tasto sembra spento
     per sempre a chi ha appena finito di digitare.
172. ⚠️ **Un metodo di servizio senza chiamanti non è funzionalità: è una promessa.** `AssignAsync` era
     implementato, autorizzato e documentato, e non lo invocava **nessuno** — né UI né test. Un incarico dato
     alla persona sbagliata si poteva solo cancellare e rifare. È il gemello di `HierarchyChange`, il valore
     d'enum che nessuno scriveva (giro Audit): prima di credere a un'interfaccia, cercarne i chiamanti.
173. ⚠️ **Un elenco su cui si agisce riga per riga non può riordinarsi per effetto dell'azione.** Si ordinava
     per `UpdatedUtc` discendente e il cambio di stato riscrive proprio `UpdatedUtc`: la riga toccata
     **saltava in cima** e sotto il puntatore ne arrivava un'altra. Con una tendina che scrive al primo
     cambio, senza conferma e senza undo, il clic successivo finisce sull'incarico sbagliato. L'ordine dev'essere
     **stabile**: proprietà del dato (ritardo, priorità, scadenza, titolo), mai l'ultimo tocco.
174. ⚠️ **Gli enum persistiti come TESTO si ordinano come PAROLE.** `ThenByDescending(t => t.Priority)` su una
     colonna `varchar(32)` ordina «High, Low, Normal»: il risultato non sembra sbagliato, sembra **casuale**.
     Il rango si scrive a mano nella query; il confronto per uguaglianza resta leggibile in SQL.
175. **Il non-evento non si scrive, e non tocca nemmeno l'orologio.** Rimettere lo stato che c'è già, o
     riassegnare alla stessa persona, non lascia riga di audit **e** non riscrive `UpdatedUtc` — che è il dato
     con cui si capisce se un incarico è fermo. La regola 138 vale anche per i timestamp, non solo per il registro.
176. ⚠️ **Chi sceglie una chiave e chi la ritrova devono leggere lo stesso elenco.** La tendina si costruiva
     la chiave del bersaglio (`$"{acc}|"` per la vIPI ACC) mentre la chiave vera è `{acc}|{callsign primario}`:
     l'incarico nasceva puntando a un documento **inesistente**. La cura non è correggere la formula, è
     **togliere la formula** — le chiavi vengono da chi le possiede (un read-model), perché una formula
     duplicata diverge il giorno che l'originale cambia. Terza forma delle regole 143 e 163.
177. ⚠️ **Un difetto invisibile può essere tenuto in vita da un secondo difetto.** La chiave sbagliata (176)
     non si vedeva perché il link non consultava nessun elenco: componeva l'URL spezzando la stringa, e
     funzionava **per caso**. Riparare una cosa ne scopre un'altra — e la seconda non poteva stare nella carta
     iniziale. Dopo una riparazione si **riguarda**, non si dà per chiuso.
178. **In una tendina non si offre ciò che non si potrà ritrovare.** Fra le opzioni compariva un documento con
     la **chiave vuota**: un collegamento che nasce già rotto. Una tendina è una comodità, e una comodità non
     deve mentire (corollario della regola 96, «una tendina non è una guardia»).
179. ⚠️ **`vipiFitViewport` non vede cosa sta SOTTO il riquadro.** Su Audit erano i 18px di padding e si sono
     chiusi nel foglio di stile (regola 141); dove sotto c'è **contenuto** — le due colonne chiuse in fondo a
     «I miei incarichi» — il foglio non basta, perché quell'altezza dipende da quante ce ne sono. Da qui il
     terzo argomento **`reserveSel`**, facoltativo. ⚠️ E il padding del `.wrap` resta comunque da chiudere:
     **52px**, gli stessi identici di Audit, su una pagina nuova che non l'aveva applicato.
180. ⚠️ **Una griglia col tetto vuole `grid-template-rows:minmax(0,1fr)`.** Senza, la riga implicita si
     dimensiona sul **contenuto** e ignora l'altezza misurata: il riquadro cresce lo stesso e la pagina scorre
     (952 su 900). È la regola 55 (`min-width:0` sui figli di griglia) sull'asse verticale, e vale per **ogni**
     riquadro misurato che sia una griglia.
181. ⚠️ **Estendere un selettore condiviso è un'operazione, non una riscrittura.** Togliendo la classe nuova
     dal selettore che porta `display:grid`, per darle colonne diverse, la pagina ha smesso di essere una
     griglia e i due pannelli si sono impilati — con altezze **plausibili e sbagliate**. L'override va in coda
     e porta **solo** ciò che cambia.
182. **Prima di dire «l'ho rotto io», misurare le gemelle.** A zoom 1.5 sotto i 1 440px queste pagine
     scorrono: sembrava un difetto del giro, ed è il comportamento della **famiglia** — Permessi e Audit danno
     gli **stessi identici numeri** (1 196 e 1 148). È il pavimento condiviso (regola 15). Un numero brutto su
     una pagina sola è un difetto; lo stesso numero su tre pagine è una scelta di progetto, e va **dichiarata**
     invece che ricorretta di nascosto.

⚠️ **E la lezione trasversale: metà di questi li ha visti l'occhio.** Nessuna misura trova undici titoli
troncati, una scadenza tagliata proprio sul segno di ritardo, un chip che *sembra* acceso senza esserlo, una
ricerca schiacciata a 150px o un menù largo metà scheda per il gesto che si fa di rado. Sesta pagina di fila
in cui gli screenshot vanno **guardati**, non solo prodotti.


## 27. Quello che ha lasciato il giro Editor APP e vLOA (22 agosto 2026)

Sedicesima e diciassettesima pagina — **le ultime della ricognizione**, e le uniche due che non avevano mai
avuto un giro. Il lavoro non è stato inventare una forma: è stato **portarle su quella già pagata** dall'editor
ACC, e il prezzo è stato scoprire che il componente **condiviso** sotto aveva difetti che nessuno dei giri
precedenti aveva misurato. Carte: [cosa fanno](../feature/2026-08-22-editori-app-vloa-cosa-fanno.md) e
[densità](../feature/2026-08-22-editori-app-vloa-densita-ui.md). Regole **183-192**.

183. ⚠️ **Un editor si misura in tre stati, non in uno: lettura, modifica, e COMPRESSO.** «Quanto è alto tutto
     aperto» non è la domanda di chi lo usa: la domanda è **quanto costa arrivare alla sezione che serve**.
     Misurato: APP 3 350 aperto e **1 654** compresso, vLOA 4 242 e **1 359**. Su una pagina che scorre per
     mestiere il comando «comprimi tutto» vale più di qualunque fascia tolta — e su queste due **non c'era**.
184. ⚠️ **Una sezione CHIUSA ha un'altezza, e va misurata.** La riga-titolo (titolo più fino a cinque comandi)
     andava a capo, e ogni sezione chiusa misurava **92px invece di ~50**: su dieci sezioni sono 900px di sole
     intestazioni, pagati **dopo** aver premuto «Comprimi tutto», cioè proprio quando si è chiesto di non
     vederle. In una riga «prosa + comandi» a cedere è la **prosa**: il titolo tronca, i comandi restano nomi
     interi (regole 33 e 111).
185. ⚠️ **Il pezzo più largo di una testata è quasi sempre il LOCK.** A 1024 la riga andava a capo per **nove**
     pixel; misurati i pezzi (regola 34), il colpevole era il chip «Stai modificando · lock fino alle 21:06»,
     **266px**. Il chip dice l'**ora**, la frase intera sta nel `title`. È esattamente la stessa cura del giro
     Versioni (la frase del lock, 647 → 289): quando una regola si ripaga due volte, la si applica a **tutti**
     i posti che hanno quella forma, non solo a quello che si sta guardando.
186. **Uno stato si dice UNA volta.** Salendo la pill di versione in testata, la copia nel rail è rimasta: a
     schermo «Bozza v2» compariva due volte a venti centimetri di distanza. Chi sposta uno stato **toglie**
     da dove stava — altrimenti non l'ha spostato, l'ha duplicato.
187. ⚠️ **Toccare un componente condiviso è una decisione che va misurata su TUTTI i suoi host.** Il «+ Blocco»
     al posto dei quattro tasti sotto ogni sezione ha restituito ~450px anche all'**editor ACC** (5 595 →
     5 144) senza riaprirne il giro — ma quello stesso cambio poteva romperlo. Prima si chiede, poi si misura
     ogni pagina che lo monta: qui erano quattro.
188. ⚠️ **Un difetto si vede quando cambia il modo di leggerlo.** Nell'elenco AoR quattro settori si chiamavano
     tutti «Athinai Radar»: il difetto c'era anche prima, quando erano chip, e **nessuno l'aveva visto** perché
     una fila di chip non la si legge come un elenco di scelte. Il nome è per chi legge, l'**identificatore** è
     quello che distingue: dove si sceglie, servono entrambi.
189. ⚠️ **Una fila di chip che SCRIVE non può somigliare a una fila di chip che GUARDA.** Nella sezione AoR
     della vLOA i due usi stavano uno sopra l'altro, stessa classe `.aor-chip`, e uno dei due **persiste nel
     documento**. Chi scrive diventa un elenco con caselle, con l'etichetta che dice quanti elementi sono
     dentro; i chip restano una cosa sola in tutta l'applicazione. Terza forma della regola del `.sector-pick`,
     e la peggiore, perché qui i due gesti sono **adiacenti**.
190. **Il testo di un aiuto invecchia come il codice.** Le sezioni di Guida di questi due editor esistevano, e
     dicevano «6 sezioni fisse» dove oggi ne ho misurate **undici**. È la prosa-che-promette-il-falso già
     trovata in cinque sottotitoli, ma in un posto dove nessuno passa a controllarla: **chi tocca una pagina
     rilegge la sua voce di Guida**.
191. ⚠️ **Aggiungere una sezione di Guida può essere un DOPPIONE, e lo dice solo la rete.** Le ancore
     `#editor-app` e `#editor-vloa` esistevano già: le mie erano un secondo blocco con lo stesso `id`, e a
     fermarmi è stato `GuideSearchTests.Catalog_anchors_are_unique`. Una voce nuova si **sostituisce** a quella
     vecchia; affiancarla lascia due sezioni che divergono e un deep-link che atterra su quella sbagliata.
192. **Una sezione nascosta nasce chiusa.** È esclusa dal documento: pagava l'altezza intera, solo attenuata, e
     su un editor da undici sezioni ognuna di quelle era una schermata da scorrere per arrivare al lavoro vero.

⚠️ **E la lezione trasversale del ramo, che qui si chiude:** queste due pagine erano **le ultime due
misurate a occhio** («900, non verificato»), e i numeri veri erano 3 540 e 4 351. Un numero che la
ricognizione stessa dichiara «non verificato» **è una stima**, e una stima in una tabella di misure si legge
come una misura. Meglio una casella vuota.


## 28. Quello che ha lasciato il giro del chrome: topbar, pannello release, menu «+ Blocco» (22 agosto 2026)

Non sono pagine: sono i **tre pezzi condivisi** che ogni giro aveva incontrato e rimandato, più una
regressione introdotta dal giro precedente e trovata dal committente guardando lo schermo. Carte:
[topbar](../feature/2026-08-22-topbar-larghezza-e-lingua.md) e
[pannello release](../feature/2026-08-22-pannello-release.md). Regole **193-204**.

193. ⚠️ **Un menu che si apre dentro un contenitore con `overflow:hidden` viene RITAGLIATO, e spostarlo di
     lato non basta.** Il menu «+ Blocco» cadeva verso il basso: 172px di altezza, **165 invisibili**. E
     `overflow:hidden` taglia in **tutte** le direzioni — aprirlo a destra restando `position:absolute` lo
     avrebbe lasciato tagliato vicino al fondo della card. La cura che risolve alla radice è aprirlo **in
     linea**: niente assoluto, niente `z-index`, niente da ritagliare, e se non ci sta va a capo **dentro il
     flusso** (la card cresce, non taglia).
194. ⚠️ **Una media query si scrive sopra l'assetto da far stare, non sotto.** La soglia della topbar era
     1000px e l'assetto da supportare è **1024**: non scattava affatto, e la barra restava a 1161. Il numero
     nel `max-width` non è «la larghezza che voglio ottenere», è «la larghezza sotto la quale la regola vale».
195. ⚠️ **Il `nowrap` non crea difetti: li rivela.** A 1440 la topbar sembrava stare, e ci stava andando a capo
     **dentro i suoi pezzi** — marchio e badge spezzati su due righe. `scrollWidth` misura il **bordo**, non
     l'interno: **una barra che sta perché il suo contenuto si spezza non sta**. Vietato il wrap, il difetto
     vero è venuto fuori (1513 minimi contro 1440).
196. ⚠️ **Uno spazio libero non è spazio disponibile finché non ci si è messo dentro quello che dovrebbe
     starci.** Misurati 306px liberi a 1280 avevo abbassato una soglia per riaprire la ricerca: quel numero
     era preso con la ricerca **chiusa**, e riaprendola si tornava a sforare di 31px. Si misura lo stato in
     cui la pagina si troverà, non quello in cui l'ho fotografata.
197. ⚠️ **In flexbox i margini `auto` assorbono lo spazio libero PRIMA di `flex-grow`.** Il campo di ricerca
     aveva `flex:1` e `.right` un `margin-left:auto`: il campo restava al suo minimo a **ogni** assetto, anche
     a 1600, e il segnaposto usciva troncato. Serve una `flex-basis` **dichiarata**. ⚠️ E **non**
     `flex-shrink:0`: provato, e faceva sforare 1600 e 1440 di 80-104px — un campo deve poter cedere, è il
     **testo** che si accorcia.
198. **Una barra di navigazione si comprime a scaglioni, per priorità, e niente sparisce.** Cede prima ciò che
     non è né un comando né uno stato che cambia (il badge staff), poi il **nome** dei comandi frequenti (che
     restano, come icone), poi ciò che non serve a ogni pagina (la ricerca, che si riapre al clic). ⚠️ E gli
     **`aria-label` restano interi** dove il testo se ne va: un tasto che diventa un'icona non diventa muto —
     è la regola 33 applicata all'accessibilità, e senza contesto visivo che compensi.
199. ⚠️ **Il chrome è il posto peggiore dove cablare una stringa.** Quindici fra `title`, `aria-label` e
     `placeholder` erano in italiano dentro la topbar: non su tre pagine come il «?» dell'anteprima, ma su
     **tutte**, comprese quelle pubbliche che un pilota straniero legge in inglese. Un `aria-label` cablato è
     il caso peggiore del caso peggiore.
200. ⚠️ **Un componente CONDIVISO non acquisisce dipendenze obbligatorie per una comodità.** `@inject` del
     roster nel pannello release lo ha reso **non montabile** per chiunque non l'avesse registrato, e ha spento
     **18 test in un colpo**. Dare un nome a un VID è un di più: si risolve dal service provider (`GetService`,
     che torna null), e senza restano i VID. Il costo di una dipendenza nuova lo pagano **tutti** gli host.
201. ⚠️ **Una `}` di troppo scarta UNA regola, e il sintomo somiglia a un problema di specificità.** Una graffa
     in più chiudeva il foglio in anticipo, e il parser CSS scartava **solo la prima regola dopo**: le regole
     *figlie* funzionavano, la madre no, e il valore calcolato restava quello vecchio. **Contare le graffe
     costa un secondo e va fatto prima di ogni ipotesi sulla cascata.**
202. **Il `confirm()` nativo non torna, nemmeno nei componenti condivisi.** Il pannello release lo usava ancora
     per annullare — l'atto più irreversibile che faccia, su un documento **già pubblicato**. Blocca il
     circuito Blazor e mette il testo utile in una finestrella di sistema. Quando una regola si chiude su una
     pagina (Versioni, 21 agosto) si cerca **chi altro** ha quella forma.
203. **Su un elenco che è STORIA, in evidenza sta solo ciò che è vivo.** Nel pannello release, di dieci
     release nove erano superate: restano la **in vigore** e le **programmate** (che sono lo stato del
     documento, e non si collassano mai) più le tre più recenti; il resto in un «altre N» che si apre.
204. ⚠️ **La retention cambia la natura di un problema, e va cercata prima di descriverlo.** Avevo scritto che
     il pannello release «cresce» come il registro di audit: ⚠️ **falso** — `KeepSupersededWithinCycles = 13`
     lo ferma a ~13 righe. Era densità, non rischio. Prima di dire «cresce senza tetto», cercare il tetto.


## 29. Quello che ha lasciato il giro del telefono (22 agosto 2026)

Il committente ha chiesto il telefono dopo il giro della topbar. Perimetro: **le pagine pubbliche, per
intero** — chi apre la vIPI dal telefono **consulta**, non scrive.

⚠️ **Il perimetro d'uso, dichiarato**: le pagine **pubbliche** si usano **da 375px in su** (telefono
compreso); **admin ed editor da 1024 in su** — desktop o **tablet orizzontale** — dove sono **verificati** e
dove **vanno bene come sono**. Non è una rinuncia: è la larghezza a cui quelle pagine sono state misurate,
IT ed EN, in tutti i giri di questo ramo. Carta:
[telefono](../feature/2026-08-22-telefono-pagine-pubbliche.md). Regole **205-212**.

205. ⚠️ **Su mobile il segnale NON è `scrollWidth` contro `clientWidth`.** Un browser mobile, quando il
     contenuto pretende più dello schermo, **non fa scorrere**: allarga il layout viewport e **rimpicciolisce
     tutto**. Misurato: `scrollLeftMax` **0** e `innerWidth` **648** su uno schermo da 375. Il difetto non è
     «la pagina scorre», è «il telefono ha dovuto rimpicciolire», e lo si legge in **`innerWidth`**.
206. ⚠️ **Cercare «chi sfora» dopo che il viewport si è allargato non trova NIENTE.** Una volta allargato,
     tutti gli elementi ci stanno dentro: sulla pagina di ricerca l'elenco degli elementi oltre il bordo era
     **vuoto** mentre il layout era 569. Il colpevole si trova **confrontando la pagina con e senza
     contenuto** (`/services/vsop/search` da solo: 375; con i risultati: 569), non cercando chi sborda.
207. ⚠️ **Una traccia `1fr` ha `min-width:auto`, cioè il min-content del suo contenuto.** Il collasso a una
     colonna dei layout documento usava `grid-template-columns:1fr`, e la colonna **non scendeva sotto 592px
     nemmeno imponendo `width:340px` al layout**. Serve `minmax(0,1fr)` più `min-width:0` sui figli. È la
     regola 55 — già scritta per gli editor — applicata al collasso, dove mancava: da sola vale 894 → 375.
208. **Una larghezza minima ha due sorgenti, e vanno curate tutte e due.** `.sid-table` porta un
     `min-width:720px` **dichiarato**, `.rwy-table` non dichiara niente e pretende 542 per il suo
     **contenuto**: azzerare il minimo non basta se la tabella non può scorrere, e farla scorrere non basta
     se il minimo resta scritto.
209. ⚠️ **Un elenco di contenitori si dimentica sempre il prossimo.** La regola sulle tabelle era scritta per
     `.doc-layout` e la pagina di **ricerca** — stesse tabelle, altro contenitore — restava larga. Si prende
     **tutto il contenuto** e si **esclude** dove non serve (`.st-scroll`, dove la tabella scorre già), non
     il contrario.
210. **Il testo che viene dai DATI va a capo dove capita.** Gli estratti di ricerca riportano testo del
     documento, dove capitano sequenze senza spazi (coordinate, elenchi di fix) che con `overflow-wrap`
     normale **non si spezzano** e allargano tutto. Vale per ogni testo di provenienza esterna: `anywhere`.
211. **Una barra che non si comprime più cambia FORMA.** Sotto i 900px non c'era più niente da togliere in
     riga (i soli codici ACC sono 263px su 375 di schermo): restano marchio, ricerca e «☰», e il resto vive
     nel menù. `<details>` **nativo** — il layout è SSR statico, e un menù che dipendesse dal circuito non
     funzionerebbe proprio sulle pagine pubbliche, che sono quelle che si guardano dal telefono.
212. ⚠️ **Un'assunzione scritta in una carta resta un'assunzione.** Avevo fissato la soglia a 700 scrivendo
     che «il tablet verticale sta comodo con la barra in riga»: misurato, **non stava** — a 768 la barra
     restava 959 e sforava di 191px. La carta serve a decidere prima, non a sostituire la misura dopo.

⚠️ **E una cosa che NON ha risolto niente**, scritta perché non se ne prenda il merito: i **16px sui campi**.
Introdotti convinti che lo zoom automatico al fuoco fosse la causa della pagina larga; il numero non si è
mosso di un pixel. La regola resta (evita lo zoom al fuoco su iOS), ma la causa era un'altra — e attribuire
una guarigione alla cura sbagliata è il modo migliore per ripetere l'errore.


## 30. Quello che ha lasciato la topbar misurata (22 agosto 2026)

Carta: [topbar misurata](../feature/2026-08-22-topbar-misurata.md). Il giro precedente aveva dato alla barra
tre media query; questo le ha tolte, e le cinque regole qui sotto valgono ben oltre la barra.

213. ⚠️ **Una media query misura la FINESTRA, non il pezzo che deve starci.** Sono cose diverse ogni volta che
     la larghezza del pezzo dipende da qualcosa che la finestra non conosce: login, lunghezza di una stringa
     che viene dai dati, numero di elementi di un catalogo, lingua, **zoom di pagina**. La topbar dipendeva da
     tutte e cinque: tarata su una configurazione, era giusta soltanto in quella — il committente la vedeva
     rotta a 1940 dove la misura di taratura diceva 1385. Dove la larghezza è **contenuto-dipendente**, la
     soglia va misurata a ogni giro, non scritta nel foglio.
214. ⚠️ **La misura del fit e quella dell'isteresi devono stare nella stessa unità.** Sotto zoom
     `bar.clientWidth` (unità di layout) e `documentElement.clientWidth` (px di finestra) **divergono**: a
     1920 con zoom 1.4 la barra ha 1371 e `documentElement` dice ancora 1920. Confrontandoli fra loro
     l'isteresi era diventata un **cricchetto** — saliva di scaglione e non scendeva più.
215. **Un'isteresi frena ciò che si trascina, non ciò che cambia da solo.** Frenare sempre è un difetto: a
     larghezza ferma, allungare una stringa faceva salire lo scaglione e nulla lo faceva più tornare giù,
     perché il margine si misura sulla larghezza e la larghezza non cambiava. Un calo dovuto al **contenuto**
     non ha nessun bordo da frenare.
216. ⚠️ **Se un gradino è più alto di quanto serva, non è una scaletta.** «La ricerca si chiude» e «le
     etichette spariscono» stavano nello stesso scaglione: 500px in un colpo, e a 1440 la barra passava dallo
     sfondare all'essere mezza vuota, con un buco di 700px in mezzo — verde alla misura e brutta a vedersi.
     Separati, la ricerca resta aperta a 1366 e 1440. **Il numero dice se sta; solo lo screenshot dice se va
     bene.**
217. ⚠️ **Un attrezzo di misura sbagliato denuncia, e sembra il prodotto.** Contare le righe di una barra
     confrontando i `top` dei figli è sbagliato — `align-items:center` dà `top` diversi a pezzi di altezza
     diversa **stando in riga** — e `getBoundingClientRect()` dice «visibile» dentro un `<details>` chiuso,
     dove Chrome usa `content-visibility` e `innerText` torna vuoto (11 link «senza etichetta» che le avevano
     tutte). Prima di credere a un difetto trovato da uno script, provare lo script su un caso sano.


## 31. Quello che ha lasciato l'audit frontend/UI (23 agosto 2026)

Carta: [audit frontend/UI](../history/audit-2026-08-23-frontend-ui.md). Sette regole, e le prime tre valgono
per **tutte** le pagine, non solo per le admin.

218. ⚠️ **Il tag di un titolo dice la STRUTTURA, non la misura.** È la causa di venti pagine che
     saltavano un gradino: un titolo di sezione era `<h3>` perché 28px è la misura giusta, non perché stesse
     al terzo livello. Le due cose si separano — il tag per la gerarchia, la misura a una classe
     (`.page-h1` 32px, `.h-sect` 28px, `.h-card` 24px) o al selettore di contesto che già c'era. La testata
     di pagina è **sempre** `<h1 class="page-h1">`, e `GerarchiaTitoliTests` lo pretende.
219. ⚠️ **Una regola legata al TAG si spegne in silenzio quando il tag cambia.** Il foglio ne aveva venti
     (`.apt-card h4`, `.nav-head h4`, `.section-title h3`, `.guida-toc h3`, `.swap-card h4`…). Quelle che un
     ritaggio può toccare vanno scritte **indifferenti al tag** — `.apt-card :is(h2,h3,h4)` — che a
     specificità identica costa zero e toglie il problema anche al prossimo giro.
220. ⚠️ **Un comando dev'essere un `<button>`, anche quando l'interattività non passa da Blazor.** La
     regola era già scritta in `Chip.razor`, ma il blocco AoR le è sfuggito per un anno perché le sue chip le
     piloterà `vipi-aor.js` e non il circuito: markup a mano, quindi nessuno l'ha ricollegato. Con
     `aria-pressed` scritto **insieme** alla classe `.on`, da un posto solo — il tag porta i tasti e il ruolo,
     `aria-pressed` porta lo stato, che altrimenti resta solo il colore.
221. ⚠️ **Una live region va resa PRIMA del messaggio che deve annunciare.**
     `@if (_msg is not null) { <span role="status"> }` non viene letto: uno screen reader annuncia i
     cambiamenti *dentro* una regione che stava già lì. Si usa `LiveRegion.razor`, che è sempre reso e ha
     `display:contents` — le testate sono flex con `gap:10px`, e un elemento in più, anche largo zero, ci
     lascerebbe un vuoto permanente.
222. **L'opacità di uno stato «spento» va sulla GRAFICA, non sull'elemento intero.** Le chip AoR spente
     applicavano `opacity:.45` a tutto, testo compreso: `--ink` al 45% su fondo chiaro fa **3,3:1**, sotto il
     4,5:1 che un testo da 13px pretende. Ora l'opacità resta sullo swatch e il testo spento prende
     `--ink-soft`, che il foglio ha già misurato a 5,89:1.
223. **Un campo che azzera il proprio `outline` deve dare l'anello al CONTENITORE.** `.searchbar` e
     `.sid-search` lo azzeravano apposta — la cornice che si vede è il riquadro attorno — ma senza
     sostituto, cioè il fuoco da tastiera lì era invisibile. C'è anche un pavimento `:focus-visible` per
     tutto il modulo, a specificità zero così le regole puntuali continuano a vincere.
224. ⚠️ **Quando si ritagga in massa, la prova che il disegno non cambia si MISURA.** 216 titoli su 15
     pagine, in chiaro e in compatta, fotografati prima e dopo (misura, peso, colore, famiglia, margini). Il
     primo giro ha trovato **sei regressioni vere** che sarebbero passate: un colore scivolato su 19 titoli,
     la compatta che rimpiccioliva chi non doveva, il peso da 700 a 800, e una pagina promossa **due volte**
     perché compariva in due liste di lavoro. Lo script sta nello scratchpad della verifica live (`titoli.js`,
     due fasi `prima`/`dopo` con la differenza stampata).

## Dove sta la roba

| Cosa | Dove |
|---|---|
| Testata in riga | `.st-head` / `.xt-head` (`vipi-theme.css`), esito `.st-msg` |
| Altezza misurata, contenuto più alto dello schermo | `vipiFitViewport(sel, collapseBelow)` — scrive `height`: il riquadro si stira e dentro scorre |
| Altezza misurata, contenuto corto e fisso | `vipiCapViewport(sel, collapseBelow)` — scrive `max-height`: alto quanto il contenuto, scorre solo se non ci sta (regola 150) |
| Riserva per ciò che sta SOTTO il riquadro misurato | terzo argomento `reserveSel` di `vipiFitViewport`/`vipiCapViewport` — facoltativo (regola 179) |
| Altezza misurata per una **griglia di colonne** | `vipiCapInner(sel, collapseBelow)` — scrive `--vipi-inner-h` sul contenitore, dividendo lo spazio per le RIGHE della griglia; il tetto lo mette il CSS sui figli. ⚠️ Le altre due non servono: un figlio di griglia non si accorcia per il `max-height` del padre — ritaglia. Uso: colonne dei coordinamenti live (`#xl-cols` / `.xl-kcol`) |
| Aggiungere un blocco a una sezione (tutti gli editor) | `details.blk-add` in `DocumentSectionsEditor` + delega `wireBlockMenu` in `vipi-ui.js` (regole 187 e 193 — si apre IN LINEA, mai in `position:absolute`) |
| Scaglioni di compressione della topbar | classi `.topbar.tb-1…tb-4` in coda a `vipi-theme.css`, messe da `vipiFitTopbar` in `vipi-ui.js` — ⚠️ **non** sono media query, e non devono tornare a esserlo (regola 213) |
| Telefono e tablet verticale (pagine pubbliche) | la barra: `tb-4`, scelto dalla misura. Il resto: `@media (max-width: 900px)` in coda — tabelle che scorrono, testo che va a capo (§29) |
| Riga della storia release | `#p-release .rel-row` — ⚠️ l'id serve: `.rel-row` nuda perde contro `.ver-row` |
| Servizio facoltativo in un componente condiviso | `IServiceProvider` + `GetService` (regola 200), mai `@inject` |
| Riga-titolo di una sezione negli editor | `.dse-head` — il titolo tronca, i comandi no (regola 184) |
| «Anteprima bozza» col suo «?» | `DraftPreviewLink` — uno per tutti e tre gli editor, chiavi `Ed_PreviewHelp*` |
| Comandi in coda al TOC / larghezza piena (host che montano il TOC condiviso) | parametri `TocFooter` e `Wide` di `DocumentSectionsEditor` |
| Scelta che PERSISTE, accanto a chip che non persistono | `.vloa-aor-pick` — elenco con caselle, mai chip (regola 189) |
| Testo di un rifiuto di un service | `ServiceErrorNarrator.Testo(ex, L)` — `ValidationException` porta `Key` accanto al messaggio grezzo; chiave ignota ⇒ testo grezzo |
| Documenti collegabili a un incarico, con la chiave che li ritrova | `IEditorTaskLinksService.OpzioniAsync` — mai fabbricare la chiave in pagina (regola 176) |
| Elenco + dettaglio degli incarichi | `.task-layout` (griglia 1.9/1, il titolo è la colonna di prosa) + `vipiFitViewport` |
| Etichette delle categorie di import | `ImportCategoryLabels` — condivise fra la pagina Sorgenti e `AuditNarrator` |
| Testo dei rilievi di diagnostica | `ConsistencyNarrator` — chiave ⇒ traduzione, chiave ignota ⇒ testo grezzo (regola 159) |
| Elenchi filtrati per chi guarda | un **read-model di pagina** (`NewDocumentOptionsService`, `ImportOverviewService`) — regola 167 |
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
| Frase leggibile di un evento di audit | `AuditNarrator` (`Vipi.Ui/AuditNarrator.cs`) — famiglia, pill, bersaglio, frase; **condiviso** fra Audit e la storia di Versioni |
| Scrittura nel registro di audit | `AuditScribe.Write` (`Vipi.Infrastructure/Persistence`) — un solo punto, encoder JSON rilassato, nessun `SaveChanges` suo |
| Barra fra le pagine admin | `AdminNav` (`Components/AdminNav.razor`) + `.admin-nav` — sopra il titolo, dove stava la briciola; elenco **e regola d'accesso** stanno lì, non nelle pagine |
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
- [Catalogo dei punti](../feature/2026-08-22-catalogo-punti-suggerimenti.md): i campi punto suggeriscono e
  segnano i nomi inesistenti; il segno **tinge il campo** invece di aggiungergli un'icona, perché un'icona
  costerebbe 14 dei 76px della colonna — cioè la quinta lettera. E la freccia dell'elenco nativo si prendeva
  quegli stessi pixel da un'altra parte (regola 45-bis), tagliando anche RWY e TYPE, che erano già rotte.
- [Il VID è una porta sul profilo IVAO](../feature/2026-08-25-vid-porta-sul-profilo-ivao.md): un componente
  solo per quindici punti, le tre forme in cui un VID compare a schermo, e le tre trappole che il componente
  chiude una volta per tutte — la risalita del clic dentro una riga che è già un comando, il render mode che
  su metà di quelle pagine **non c'è** (SSR statico: un `@onclick` non farebbe nulla, in silenzio) e la
  specificità che in stampa fa vincere `.vid-link` su `.vipi-root a`.
