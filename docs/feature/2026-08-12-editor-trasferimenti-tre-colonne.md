# Feature — Editor trasferimenti: tre colonne, vista elenco, stato in URL, editing in cella

Data: 2026-08-12 · Stato: **CHIUSO — suite 2384 verde, Release 0 warning su entrambi i TFM,
✅ verifica live eseguita** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue [varianti a livelli](2026-08-12-varianti-a-livelli.md) e
[trasferimenti ACC↔APP](2026-08-11-trasferimenti-acc-app.md) · branch `feature/trasferimenti-acc-app`.

> ⚠️ **Le colonne sono DUE dal 3 settembre 2026.** La terza — il pannello della riga — è diventata una
> finestra in [la scheda della clausola diventa una finestra](2026-09-03-trasferimenti-scheda-clausola-finestra.md): nella colonna il suo corpo aveva
> 348px di larghezza per 896 di contenuto, e allargare il contenitore non bastava (misurato: a 828px
> scendeva a 860, il 4%). Con lei sono spariti `.xt-noPanel`, il salto del pannello sotto le altre colonne
> allo scaglione 1200, e `vipiRevealPanel`. Tutto il resto di questa carta — navigatore, riquadro di
> lavoro, vista a elenco, stato in URL, scrittura in cella — vale ancora.

## Obiettivo

`/services/vsop/admin/transfers` funziona ma **non è spedita**. Il committente la usa e la definisce «scomoda e poco
fluida». Non è una questione di gusto: la pagina ha attriti che si contano nel codice, e ognuno costa gesti a
ogni riga scritta.

**Cosa rallenta, misurato sul sorgente prima della modifica** (`AdminTrasferimentiPage.razor`, 1955 righe):

| # | Attrito | Dove |
|---|---|---|
| 1 | La pagina rende **tutti** i gruppi dell'ACC con le loro tabelle, uno sotto l'altro | il `foreach` a tre livelli, righe 240-525 |
| 2 | Tre livelli di collasso (settore ▸ aeroporto ▸ gruppo) e `CollapseAll()` a ogni cambio ACC: **3 clic** per arrivare a una riga, ogni volta | `_collapsedSec/_collapsedApt/_collapsedFlow`, `OnAccChanged` |
| 3 | Lista e pannello nello **stesso scorrimento di pagina**: sotto 1080 px il pannello finisce dopo tutta la lista, e ci vuole `vipiRevealPanel` come pezza | `.xfe-layout`, `OnAfterRenderAsync` |
| 4 | Si scrive **solo** dal pannello: cambiare un CoP costa apri riga → pannello → Salva → chiude → riapri la prossima | `PointPanel()`, `SavePanel()` |
| 5 | Filtri e nodi aperti muoiono con F5; nessun link condivisibile a un gruppo | nessuno stato in URL |
| 6 | Il filtro «senza ricevente» **non filtra nulla**: `_noReceiverOnly` si accende ma `FilteredFlows()` non lo legge mai | riga 681 vs 844-858 — **difetto**, non attrito |

Il 6 è un bug vero, trovato leggendo: il tasto c'è, si illumina, e non fa niente.

## Decisione

Si adotta il pacchetto «rivoluzione» proposto al committente e da lui scelto: **R1 + R3 + V5 + R2**.
Non è un ritocco: cambia la forma della pagina.

- **R1 — tre colonne.** Navigatore (albero) · riquadro di lavoro · pannello riga. Ogni colonna scorre per
  conto proprio, dentro l'altezza dello schermo.
- **R3 — vista elenco.** Interruttore Albero ⇄ Elenco: l'elenco mostra **tutte** le righe dell'ACC in una
  tabella sola, con settore/aeroporto/tipo come colonne. Per la revisione l'albero è il nemico.
- **V5 — stato in URL.** ACC, vista, gruppo, riga e filtri in querystring; le preferenze di vista in
  `localStorage`. F5 non riporta a zero, e un gruppo si può linkare.
- **R2 — editing in cella.** CoP, livello e ricevente si scrivono **nella tabella**. Il pannello resta per
  faccetta, condizione e azioni sulla riga.

## Pre-flight — 4 domande

### 1. Modello — «aggiungo un concetto o ne esiste già uno?»

**Nessuna entità, nessun DTO, nessuna colonna.** Questa carta non tocca né dominio né persistenza: `ITransferService`
resta identico. Tutto ciò che si aggiunge è **stato di vista** nella pagina.

Un concetto di vista nuovo c'è, ed è uno solo:

```
XferView   Tree | Flat      // come si guarda l'elenco
```

Non affianca niente: **sostituisce** il collasso a tre livelli come modo di «restringere ciò che guardo».
Coerentemente **`_collapsedFlow` sparisce** — nel navigatore il gruppo è una foglia, non ha figli da nascondere
(→ domanda 4).

L'unica aggiunta fuori dalla pagina è l'**inverso di una funzione che c'è già**:
`LevelFormatting.TryParse` accanto a `LevelFormatting.Format`, nello stesso file. Non è un secondo modello del
livello: è la lettura della stessa scrittura, e la sua correttezza si enuncia come round-trip.

### 2. Dispatch — «sto per switchare su un tipo che switcho già altrove?»

Un solo `switch` nuovo, sul contenuto del riquadro di lavoro (niente scelto · gruppo · nuovo gruppo · elenco),
in **un solo posto**. Regola del 2 non scatta: nessun registro, sarebbe over-engineering per quattro rami che
vivono in una funzione.

⚠️ **Un duplicato che invece scatta**: `ParseQuery` esiste già in `VersioniPage` e questa pagina sarebbe la
seconda. Va **estratta** in un helper condiviso (`Vipi.Ui.QueryStringUtil`) e `VersioniPage` va portata su
quello nello stesso giro — non lasciata indietro.

### 3. Ingressi + verifica — «come ci arriva l'utente e come lo verifico?»

**Ingresso invariato**: `/services/vsop/admin/transfers`, dalla pagina Struttura. Nessun catch-22: il primo gruppo si
crea dal tasto «+ Gruppo» del navigatore, che è visibile anche quando non c'è nessun gruppo — ed è esattamente
il caso da guardare, perché con l'albero vuoto la colonna 1 non ha nodi.

**Verifica**: skill `/verifica-live` sull'ACC reale del `vipi.db`, guidando i quattro gesti che la carta promette
di rendere spediti — scegliere un gruppo, scrivere una riga in cella, passare a Elenco e tornare, ricaricare la
pagina e ritrovarsi dov'era.

### 4. Propagazione — «questa modifica rimuove o rinomina qualcosa?»

**Sì**, e va chiuso nello stesso giro:

| Cosa sparisce | Chi lo cita |
|---|---|
| `_collapsedFlow` + `ToggleFlow` | la pagina; `ExpandTo`/`CollapseAll`/`ExpandAll` cambiano significato |
| «espandi/comprimi tutto» sui gruppi | chiavi resx `Xfer_ExpandAll` / `Xfer_CollapseAll` — restano ma valgono sull'albero, non sui gruppi |
| `vipiRevealPanel` | il pannello non è più in fondo alla pagina: la funzione resta usata **solo** sotto la soglia a colonna singola — ⚠️ **rimossa** il 3 settembre 2026 con la terza colonna |
| `ParseQuery` in `VersioniPage` | sostituito dall'helper condiviso |

Nessuna memoria del progetto descrive l'impaginazione di questa pagina in modo che questa modifica renda falso;
la memoria `trasferimenti-acc-app-carta` cita `.xfe-layout` e **va aggiornata** perché il layout diventa a tre
colonne.

## Il layout

```
┌───────────────┬────────────────────────────────┬──────────────┐
│ NAVIGATORE    │ RIQUADRO DI LAVORO             │ PANNELLO     │
│               │                                │              │
│ ▾ LIRR_CTR    │  LIRR_CTR · LIRF · Arrivi      │ Riga: VALMA  │
│   ▾ ✈ LIRF    │  [clona][modifica][elimina]    │              │
│     • Arrivi ●│  ┌──────────────────────────┐  │ CoP  [VALMA] │
│     • Partenze│  │ CoP  Liv  Ricev.  Cond.  │  │ Ricev[LIRF_A]│
│   ▸ ✈ LIRN    │  │ VALMA FL150 LIRF_APP …   │  │ ▸ Livello    │
│ ▸ LIRR_W_CTR  │  │ ELB   FL130 LIRF_APP …   │  │ ▸ Trasferim. │
│               │  └──────────────────────────┘  │ ▸ Condizione │
│ [+ Gruppo]    │  [+ riga][copia da…]           │ [Salva]      │
└───────────────┴────────────────────────────────┴──────────────┘
    260px                   1fr                       380px
```

**Ogni colonna scorre da sola**, dentro `calc(100vh - …)`. È la differenza che toglie l'attrito 3: il pannello
non finisce mai «dopo la lista», perché non c'è più una lista sopra di lui.

Sotto **1200 px** le tre colonne diventano due (navigatore + riquadro, pannello sotto); sotto **900** una sola,
col navigatore che si richiude in un selettore. Le colonne che collassano perdono l'altezza fissa: un riquadro
alto 100vh dentro una pagina che scorre è una trappola, ed è il motivo per cui `vipiRevealPanel` resta.

> ⚠️ **Non più.** Con la scheda diventata finestra le colonne sono due già in partenza, lo scaglione 1200
> non ha più niente da riorganizzare, e quello che conta è **900**: lì le due si impilano e l'altezza fissa
> sparisce — la trappola descritta qui sopra resta vera, solo a una soglia sola.

### Il riquadro di lavoro

Un `switch` su cosa si sta facendo:

| Stato | Cosa mostra |
|---|---|
| niente scelto | cosa fare (come il pannello vuoto) |
| gruppo scelto | intestazione del gruppo + azioni + tabella delle sue righe + piede «aggiungi/copia da» |
| nuovo gruppo | il form del gruppo nuovo (oggi in cima alla pagina) |
| vista elenco | tutte le righe filtrate dell'ACC |

I form di gruppo (nuovo, modifica, clona, copia-da) **smettono di comparire dentro l'elenco spostando tutto
sotto**: hanno un posto fisso, che è la testata del riquadro.

## Vista elenco (R3)

Stesse righe, senza albero: `Settore · Aeroporto · Tipo · CoP · Livello · Ricevente · Condizione`. Ordinabile
per intestazione, filtrabile con gli stessi filtri della barra.

Due regole che nascono dal modello e non dal gusto:

- **Il trascinamento è spento in elenco.** L'ordine è la struttura dell'outline *dentro un flusso*; trascinare
  fra flussi diversi scriverebbe un ordine che non significa niente. È la stessa ragione per cui è già spento
  con l'ordinamento non-manuale.
- **Il blocco di varianti resta leggibile**: la velatura e la guida verticale valgono per righe consecutive
  dello **stesso flusso e stesso gruppo** — due gruppi con lo stesso numero in flussi diversi non sono lo
  stesso gruppo (invariante già vero nella pagina, che qui va portato dentro la vista piatta).

E il **difetto 6 si chiude qui**: i due filtri diagnostici («senza ricevente», «da rivedere») filtrano le
**righe** in elenco, non solo i gruppi in albero, e `_noReceiverOnly` entra finalmente in `FilteredFlows()`.

## Stato in URL (V5)

```
/services/vsop/admin/transfers?acc=LIRR&vista=elenco&gruppo=42&riga=317&q=VALMA&tipo=Arrival&rev=1&norx=1&ord=cop
```

Regola di divisione, e non è arbitraria:

- **in URL** ciò che identifica *cosa sto guardando* — ACC, vista, gruppo, riga, filtri: è quello che ha senso
  mandare a un collega e ritrovare dopo F5;
- **in `localStorage`** ciò che è *come mi piace guardare* — anteprime accese, ordinamento: è preferenza della
  persona, non del contenuto, e in un link condiviso sarebbe rumore.

`vipiStoreGet`/`vipiStoreSet` esistono già in `vipi-editor.js` e non sono mai stati usati da Razor: qui trovano
il primo chiamante.

⚠️ **Trappola JS-interop**: `localStorage` si legge solo dopo il primo render (non c'è JS durante il
prerender). Le preferenze si applicano in `OnAfterRenderAsync(firstRender)` con un `StateHasChanged`, mai in
`OnInitializedAsync` — altrimenti il circuito esplode o la preferenza si perde in silenzio.

## Editing in cella (R2)

Tre celle si scrivono direttamente: **CoP**, **Livello**, **Ricevente**. Le altre no — condizione e faccetta
sono composte da più campi, e comprimerle in una cella è esattamente l'errore che la pagina ha già fatto una
volta (la riga che diventava «una fila di sei controlli senza etichetta»).

| Tasto | Cosa fa |
|---|---|
| clic sulla cella | apre l'editor in cella |
| `Invio` | salva e scende alla stessa cella della riga sotto |
| `Tab` | salva e passa alla cella dopo nella stessa riga |
| `Esc` | annulla, la cella torna com'era |
| clic fuori | salva |

**Il livello si scrive come si legge.** La colonna mostra `LevelText`, che è
`LevelFormatting.Format(...)`: «FL150», «FL130-», «2500 ft», «FL280+ ↑ (dispari)», o un testo libero.
Serve l'inverso, e la sua correttezza è una proprietà, non un elenco di casi:

> per ogni livello rappresentabile, `Format(Parse(Format(x))) == Format(x)`.

Round-trip **sul testo** e non sui campi, e la differenza ha un caso solo: `TransferVerticalState.Level` non
lascia segno nel testo (nessuna freccia). Chi salva una cella deve quindi **conservarlo** — vedi sotto.

Ciò che non è riconoscibile come livello **non è un errore**: diventa il livello «speciale» (testo libero), che
è già una forma prevista dal modello (`LevelConstraint.Special`). Scrivere «per aerovia» nella cella deve
funzionare.

⚠️ **Salvare una cella salva la riga.** `UpdatePointAsync` vuole l'input completo, e per CoP e ricevente
**propaga al gruppo di varianti** (è l'identità dell'accordo). Quindi la cella parte dalla riga esistente e
cambia un campo solo — e chi scrive un CoP su una variante lo scrive su tutte, come già oggi dal pannello.
La copia riga→input non può essere `FromRow`: quella è la copia *per clonare*, e lascia indietro `IsGroupWide`
apposta. Serve una copia distinta, e le due vanno chiamate in modo che si capisca quale fa cosa.

## Componenti

La pagina è a 1955 righe e questa carta le aggiungerebbe. Si estraggono due componenti:

| Componente | Cosa sa |
|---|---|
| `XferNavigator.razor` | l'albero settore ▸ aeroporto ▸ gruppo, il gruppo scelto, i conteggi, gli avvisi che risalgono, il tasto «+ Gruppo» |
| `XferRowsTable.razor` | la tabella delle righe — **una sola**, usata sia dal gruppo che dalla vista elenco (le colonne di contesto compaiono solo in elenco) |
| il pannello | **resta nella pagina**: legge e scrive `PointForm`, che è stato della pagina, e spostarlo sarebbe plumbing senza guadagno |

Una tabella e non due: le due viste mostrano le stesse righe con lo stesso significato (blocchi, rientro,
anteprima, azioni), e scriverla due volte vorrebbe dire correggere due volte ogni difetto di lettura — che è
il debito che i nove doc refactor hanno appena finito di pagare.

I componenti **non calcolano**: ricevono `XferTableRow` / `XferNavSector` già composti. Chi li compone è la
pagina, perché è lei a sapere se sta scorrendo un flusso o attraversandoli tutti — e da lì in giù il codice che
disegna è uno solo.

## Test

- `LevelFormattingTests` — round-trip `Format(Parse(s)) == s` su tutte le combinazioni di
  vincolo × unità × parità × stato verticale, generate (102 casi), più i casi che *non* vengono da `Format`:
  testo libero, vuoto, trattino, spazi, minuscole, numero nudo, testo che finisce con «-».
- Un test documenta il **limite**: `TransferVerticalState.Level` non sopravvive al giro, ed è la ragione per
  cui la cella lo conserva.
- La pagina non ha test (è Razor): la copertura sta nel round-trip e nella verifica live.
- Nessun test esistente è cambiato: il servizio non si tocca.

## ✅ Verifica live — eseguita il 12 agosto 2026

Su copia del `vipi.db` reale, ACC **LIBB**: 36 gruppi, 78 righe, 5 settori mittenti. Edge + puppeteer-core,
lock di struttura preso, tutto **misurato** a schermo.

| Cosa | Esito |
|---|---|
| Tre colonne a 1700×1000 | `paginaScorre = false`; navigatore 2078 px di albero dentro 487, elenco 6246 dentro 538 |
| Navigatore | avvisi che risalgono (● su `LDZO_CTR`, `LIBB_ES_CTR`), conteggi per nodo, «+ Gruppo» nel piede |
| Scelta gruppo | URL `?acc=LIBB&gruppo=25`; testata «LDZO_CTR · Arrivals · ✈ LIBD — Bari Palese» + tre azioni |
| Vista elenco | 78 righe, colonne Settore/Aeroporto/Tipo, **zero maniglie di trascinamento**; URL `vista=Flat` |
| **Filtro «senza ricevente»** | 78 righe → **1**, e quella riga è davvero senza ricevente — il difetto §6 è chiuso sul dato reale |
| Scrittura in cella | campo aperto **già a fuoco**; `AIOSA` → `ZZTEST` con Invio, e il campo si riapre da solo su `BEVIS`, la riga sotto |
| Esc | chiude senza scrivere |
| Frase derivata | si riscrive col valore nuovo («…stabile a livello 230 su **ZZTEST**») |
| F5 | riapre ACC, vista, righe e gruppo dall'URL |
| Pannello | testata «Edit row · LAAA_CTR · LIBD · Arrivals», **piede visibile** e Salva presente; URL `…&riga=63` |
| Guardie | nessun errore di pagina, nessun letterale Razor, nessuna eccezione in console |

### Tre difetti trovati **solo** a schermo

1. **La pagina scorreva di 148 px.** Il `calc(100vh - 250px)` era una stima; il valore vero è **398**, perché
   sopra la griglia stanno barra dell'applicazione, briciole, testata, barra ACC, barra dei filtri e — a volte —
   l'avviso del lock. In CSS puro non si esprime: `N` è proprio ciò che non si sa. Lo misura `vipiFitViewport`,
   chiamato **a ogni render** (l'avviso del lock compare e sparisce da solo, e la misura cambia con lui).
2. **Il navigatore si srotolava a schermo stretto**: 2174 px di albero da scorrere prima di arrivare al lavoro.
   Tetto `42vh` sotto i 1200. Il riquadro di lavoro invece resta libero — e per lasciarlo libero *davvero*
   serve `align-items:start`, senza il quale si allineava all'altezza del navigatore incastonato e finiva
   incastonato anche lui: 378 px per una tabella da 2082.
3. **Aprire una cella spostava le colonne di un centinaio di pixel.** In una tabella a colonne automatiche un
   `<input>` porta la propria larghezza **intrinseca** (venti caratteri) e `width:100%` non la riduce:
   `size="8"`. Scostamento massimo da ~100 px a 21.

Nessuno dei tre era visibile alla suite, e il primo non era nemmeno esprimibile senza misurare.

## Esito — scostamenti dalla carta e cose imparate

**Due componenti e non tre.** La carta ne prometteva tre; il pannello è rimasto nella pagina. Estrarlo avrebbe
significato passargli `PointForm`, `_condRunways`, `_areas`, `_preview`, il localizzatore e otto callback per
guadagnare righe di file e nient'altro: non è condiviso con nessuno, e la ragione per cui la tabella *doveva*
uscire — due viste che la rendono — su di lui non vale.

**`Parse` e non `TryParse`.** La carta diceva `TryParse`, cioè una lettura che può fallire. Scrivendola è
venuto fuori che **non deve poter fallire**: ciò che non è un livello è il livello «speciale», che il modello
già prevede. Un `bool` di ritorno avrebbe costretto ogni chiamante a decidere cosa fare di «per aerovia» — che
è un livello valido.

**Il caso che la carta aveva previsto si è rivelato l'unico.** «Una casella scrive solo ciò che mostra» sembrava
un principio; nel dato reale ha un solo effetto, ed è `TransferVerticalState.Level`. Averlo cercato prima ha
evitato una cancellazione silenziosa che nessun test avrebbe visto — e il test che lo documenta è scritto come
limite, non come funzione, perché è quello che è.

**Coda dall'uso, in due passaggi: l'albero parte chiuso — ma solo sui settori.**

Lo avevo lasciato tutto aperto ragionando che un albero chiuso «non fa capire che dentro c'è il lavoro».
All'uso vero è il contrario, e la misura lo dice: aperto, i cinque settori di LIBB srotolano trentuno aeroporti
e trentasei gruppi — **2078 px** da scorrere dentro 487 visibili per trovare il ramo che serve.

Il primo tentativo ha chiuso **tutto**, e sbagliava di un livello: due clic per vedere un gruppo (settore, poi
aeroporto). Lo stato giusto è **settori chiusi, aeroporti aperti dentro** — un clic, e i gruppi di quel settore
sono lì. Provato a schermo: apertura 0 aeroporti e 0 gruppi in 487 px; un clic su `LDZO_CTR` → 3 aeroporti e
**3 gruppi**.

Il tasto ⊘ continua a chiudere *tutto*, aeroporti compresi: «chiudi tutto» deve voler dire tutto, e non lo stato
di partenza. Le due strade che devono restare aperte lo restano: un **link a un gruppo** apre il proprio
percorso (`ExpandTo`), un **filtro attivo** apre tutto (`_filtering`, 6 aeroporti e 6 gruppi su «PAPIZ»).

**Un dato reale che si legge male, e non è di questo giro**: la riga `BEVIS` mostra livello `— (dispari)`, cioè
un suffisso di parità appeso a un livello assente. È `Format` che si comporta così da sempre; il round-trip lo
regge (si rilegge identico), ma a schermo è una frase monca. Vale la pena guardarlo, in un altro giro.

**Un vertical morto trovato per strada**: `ITransferService.MovePointToEndAsync` (con repository e test) non ha
**nessun** chiamante dall'interfaccia — il wrapper nella pagina era già morto prima di questa carta ed è stato
tolto. Il metodo di servizio resta: rimuoverlo è un'altra decisione, e non di questa scheda.

## Fuori scopo

- **R4 (incolla massivo / quick-add testuale)**: vale se il lavoro davanti è *riempire*; qui è *rifinire*.
- **Undo (V8)**: richiede un ripristino lato servizio, che è un'altra carta.
- **Bulk esteso (V7)**: la selezione multipla resta sul solo ricevente in questo giro.
- **Palette Ctrl+K (V6)**: la ricerca globale esiste già; una seconda sarebbe un secondo modello di navigazione.
