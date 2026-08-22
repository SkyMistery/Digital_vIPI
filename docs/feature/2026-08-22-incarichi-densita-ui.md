# Incarichi — densità e QoL delle due pagine (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/services/vsop/admin/tasks` e `/services/vsop/tasks`. Seconda carta del giro: **la
> forma**. La sostanza sta nella gemella [`2026-08-22-incarichi-cosa-sono.md`](2026-08-22-incarichi-cosa-sono.md).
> Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il punto di partenza, misurato

⚠️ La ricognizione dava **entrambe a 900**: erano misure a tabella **vuota** — nel DB di sviluppo `EditorTasks`
ha zero righe. Rimisurate il 22 agosto riempiendola (12 incarichi = uso normale, 60 = un ciclo accumulato;
sei persone, perché il riepilogo per editore cresce col numero di **persone**):

| Pagina | Con 12 | Con 60 | Cosa cresce |
|---|---:|---:|---|
| Incarichi admin `/services/vsop/admin/tasks` | **1 813** | **4 764** | la tabella: 64px a riga, e non ha tetto |
| Incarichi utente `/services/vsop/tasks` | 900 (con 2 propri) | **1 562** (con 12 propri) | le schede kanban nelle cinque colonne |

A 1280×800 la pagina utente **scorre già con quattro** incarichi (854 su 800).

## Admin — cosa cambia

1. **Il form «Nuovo incarico» esce dalla cima.** 242px di modulo + 34 di titolo di sezione = **276px prima di
   vedere un solo incarico**, per il gesto **raro**: si crea di tanto in tanto, si guarda l'elenco sempre.
   Va in un tasto di testata e si apre nel pannello di destra (regola 119, com'è già per «Concedi» in Permessi).
2. **Elenco a sinistra, incarico scelto a destra** (`.perm-layout`, `vipiFitViewport`). Qui il pannello è
   **giustificato dall'azione** (regola 140): sull'incarico scelto si cambia stato, si **riassegna** (N2), si
   apre il documento, si elimina. La descrizione e le date escono dalla riga e vanno lì.
3. **Riga da 64 a ~40px**: titolo su una riga, descrizione nel pannello (e nel `title`), colonne per classe
   semantica misurate col font sui valori veri (regole 60-62).
4. **`thead` fermo** (`.res-table.sticky-head` con `top:0` dentro lo scroller, regola 51): a 60 incarichi la
   tabella è alta 3 730px, oggi è una `<table>` nuda.
5. **Filtri che contano** (`.struct-bar` + `.sh-chip` in gruppo, regole 30-32): un chip per stato
   (Da fare · In corso · In revisione · Bloccato · Fatto), un chip **In ritardo**, la ricerca, il filtro per
   ciclo AIRAC. Sempre presenti, spenti a zero, e il contatore in testata dice «N di TOT» quando si filtra.
   ⚠️ **Default: non conclusi.** È la cura della crescita senza tetto senza inventare un'archiviazione — i
   «Fatto» restano a un clic (decisione del committente, 22 agosto).
6. **«Avanzamento per editor» non sotto 1 500px.** Sei schede da 244px per **due numeri ciascuna**, oggi sotto
   la tabella: con 20 staffisti è una parete che non vede nessuno (stesso difetto della card immagini in
   Diagnostica, regola 161). Diventa una riga di chip per persona in cima all'elenco, che **filtra**.
7. **`.wrap` a 1 200px → larghezza piena**: sotto i 1 200 la barra admin va su **due righe** (87px invece di
   55; §21). Con la barra su una riga sola sono 32px restituiti al contenuto.
8. **La prosa nel «?»**: il sottotitolo in fascia diventa `HelpHint` (regole 7-9), con sezione nuova nella
   Guida e voce in `GuideSearchCatalog` (regola 12) — oggi le due pagine non hanno **nessun** «?».
9. **L'esito è un chip in testata** (`.st-msg`), non due callout sopra il contenuto che spingono giù la
   tabella su cui si sta lavorando (regola 5).

## Utente — cosa cambia

10. **Kanban a tre colonne + due chiuse** (decisione del committente): Da fare · In corso · In revisione a
    schermo, **Fatto** e **Bloccato** come intestazioni collassate col conteggio, apribili. Cinque colonne da
    230px a 1280 sono quattro incarichi e la pagina scorre.
11. **Una card più corta**: oggi ogni card porta **quattro** tasti «→ stato» (tutti gli stati tranne il
    proprio). Diventano un avanzamento primario («→ In corso», il passo che si fa davvero) più un menu per
    gli altri; il cestino si stacca dalla fila (e chiede conferma, N5).
12. **Chi me l'ha assegnato, e quando**: la card lo dice (N7), col nome risolto dal roster e non col VID nudo
    (regola 124).
13. **La briciola resta.** Non c'è barra admin, è una pagina d'utente, e dopo il giro Nuovo documento sappiamo
    che lì la briciola è l'**unica** risalita (regola 132).
14. **Il «?»** anche qui, con la sua sezione di Guida.

## Com'è andata: i numeri, misurati

Guidate entrambe con `verifica-live` (Edge + puppeteer-core, DB copiato e **riempito**), a
**1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom **0.8 → 1.5**, con **12** e con **60** incarichi.

| Pagina | Prima (12) | Prima (60) | **Dopo** |
|---|---:|---:|---:|
| Incarichi admin `/services/vsop/admin/tasks` | 1 813 | 4 764 | **il viewport**, con 12 e con 60 |
| Incarichi utente `/services/vsop/tasks` | 1 562 | — | **il viewport**, con 12 propri e con 10 |

Entrambe restano il viewport a **tutti e quattro** gli assetti, in **tutte e due** le lingue, da zoom 0.8 a
1.25. ⚠️ **A zoom 1.5 sotto i 1 440px scorrono**, ed è il comportamento della **famiglia**, non di queste
pagine: misurato, Permessi e Audit danno gli **stessi identici numeri** (1 196 a 1280×800, 1 148 a 1024×768).
È il pavimento condiviso — sotto i 320px di altezza utile l'altezza fissa sparisce per scelta (regola 15),
perché un riquadro più basso è inutilizzabile e due barre annidate sono peggio di una pagina che scorre.
Prima di dire «l'ho rotto io», si misurano le gemelle.

## Tre cose che la misura non avrebbe trovato da sola

1. ⚠️ **`vipiFitViewport` misura fin dove arriva il riquadro, e non vede cosa gli sta SOTTO.** Sulla pagina
   utente sotto il tabellone ci sono le due colonne chiuse: da qui il terzo argomento **`reserveSel`**,
   facoltativo, che toglie dallo spazio l'altezza di quello che indica. E sotto ancora c'erano i **70px** di
   respiro del `.wrap`: **52px** esatti di scorrimento, **gli stessi di Audit** — la regola (141) era già
   scritta e non l'avevo applicata alla mia pagina.
2. ⚠️ **`grid-template-rows:minmax(0,1fr)`.** Senza, la riga implicita di una griglia si dimensiona sul
   **contenuto** e ignora l'altezza misurata: il tabellone cresceva lo stesso (952 su 900). È la regola del
   `min-width:0` sui figli di griglia (55), sull'asse verticale — e vale per **ogni** riquadro misurato che
   sia una griglia, non solo per questo.
3. ⚠️ **Estendere un selettore condiviso è un'operazione, non una riscrittura.** Togliendo `.task-layout` dal
   selettore che porta `display:grid` per dargli colonne diverse, la pagina ha smesso di essere una griglia e
   i due pannelli si sono impilati — con le altezze che tornavano plausibili e sbagliate. L'override va in
   coda e porta **solo** ciò che cambia.

## Quello che ha visto l'occhio, e nessuna misura

- **Tutti e undici i titoli uscivano troncati** mentre cinque colonne misurabili si prendevano 636px. Il
  titolo **è** la colonna di prosa (regola 60), e la griglia vuole **1.9/1**, non 1.35/1 come Permessi: lì la
  riga è «nome + chip», qui è una frase.
- La scadenza tagliava **«AIRAC 2606 ⚠»** proprio sul segno del ritardo — cioè sulla cosa che quella colonna
  esiste per dire.
- Il chip **«In ritardo»** con la classe `warn` sembrava **già acceso**: si leggeva come un filtro attivo che
  nessuno aveva chiesto. Il colore va sul numero, non sullo stato.
- La **ricerca** schiacciata al suo minimo dai sei chip: «Cerca per titolo, person…».
- La tendina **«Altro stato…»** larga metà della scheda, per il gesto che si fa di rado, accanto
  all'avanzamento che è il passo che si fa davvero.

## E i gesti, guidati

Scelta della riga, cambio di stato (**la riga non salta più**: verificato, resta in posizione 2 prima e
dopo), riassegnazione, tasto «Crea» spento finché mancano titolo o persona, e il link che apre **l'editor
giusto** — `/services/vsop/libb/editor` → «Editor vIPI Brindisi», non l'elenco dei documenti.

## Come si verifica

Riempire `EditorTasks` nella **copia** del DB prima di misurare (12 e 60 incarichi, sei persone, un titolo
lungo, una descrizione lunga, incarichi in ritardo e conclusi), poi guidare entrambe le pagine a
**1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom **0.8 → 1.5**, con la skill `verifica-live`:

- l'altezza resta il viewport con 12 e con 60 incarichi, e con i chip in ogni combinazione;
- il `thead` resta fermo dopo migliaia di px di scorrimento interno;
- la pagina utente non scorre a 1280×800 con 12 incarichi propri;
- ⚠️ **si guardano gli screenshot**, non solo le misure: metà dei difetti dei giri precedenti non aveva
  un'asserzione che li cercasse.
- ⚠️ La misura della pagina **Audit** si rifà: la famiglia `Incarico` (carta gemella, N7) è la più prolifica
  che il registro abbia mai avuto.

## Slice

1. Testata in riga (titolo + conteggio + «?» + comandi + `.st-msg`), `.wrap` a larghezza piena, barra su una riga.
2. `.perm-layout`: elenco a sinistra, pannello a destra con le azioni (e la riassegnazione, N2 della gemella).
3. Tabella: `thead` fermo, riga a ~40px, colonne misurate.
4. Filtri: chip per stato + «in ritardo» + ricerca + AIRAC, default «non conclusi», contatori onesti.
5. Avanzamento per editore: da griglia di card a chip che filtrano.
6. Pagina utente: tre colonne + due chiuse, card corta, conferma sull'eliminazione.
7. Guida: sezioni `#admin-incarichi` e `#incarichi`, `GuideSearchCatalog`, «?» in entrambe le pagine.
