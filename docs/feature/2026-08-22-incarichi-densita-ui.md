# Incarichi — densità e QoL delle due pagine (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/vsop/admin/tasks` e `/vsop/tasks`. Seconda carta del giro: **la
> forma**. La sostanza sta nella gemella [`2026-08-22-incarichi-cosa-sono.md`](2026-08-22-incarichi-cosa-sono.md).
> Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il punto di partenza, misurato

⚠️ La ricognizione dava **entrambe a 900**: erano misure a tabella **vuota** — nel DB di sviluppo `EditorTasks`
ha zero righe. Rimisurate il 22 agosto riempiendola (12 incarichi = uso normale, 60 = un ciclo accumulato;
sei persone, perché il riepilogo per editore cresce col numero di **persone**):

| Pagina | Con 12 | Con 60 | Cosa cresce |
|---|---:|---:|---|
| Incarichi admin `/vsop/admin/tasks` | **1 813** | **4 764** | la tabella: 64px a riga, e non ha tetto |
| Incarichi utente `/vsop/tasks` | 900 (con 2 propri) | **1 562** (con 12 propri) | le schede kanban nelle cinque colonne |

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
