# Regole di densità e uso per le pagine admin (19 agosto 2026)

> **A cosa serve.** Fra il 16 e il 19 agosto tre pagine admin sono state rifatte nella forma —
> [accordi](../feature/2026-08-19-accordi-densita-ui.md), [struttura](../feature/2026-08-19-struttura-densita-ui.md),
> [ACC](../feature/2026-08-19-acc-admin-densita-ui.md) — e ogni giro ha lasciato una regola pagata a caro prezzo,
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

## Esempi misurati (le tre carte)

- [Accordi di coordinamento — densità](../feature/2026-08-19-accordi-densita-ui.md): la prima testata in riga,
  le colonne fisse, l'altezza misurata sopra tre colonne.
- [Struttura — densità](../feature/2026-08-19-struttura-densita-ui.md): prosa nei «?», barra unica, chip che non
  saltano, pannelli con il solo corpo che scorre.
- [Pagina ACC — densità](../feature/2026-08-19-acc-admin-densita-ui.md): intestazioni che restano, la perdita
  dei limiti, la scelta per riga, lo zoom, i «?» fuori schermo.
