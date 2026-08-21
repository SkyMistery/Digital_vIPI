# Aeroporti (admin) — due pannelli misurati, intestazioni ferme, avanzamento vero (19 agosto 2026)

Quarto giro della famiglia, dopo [accordi](2026-08-19-accordi-densita-ui.md),
[struttura](2026-08-19-struttura-densita-ui.md) e [ACC](2026-08-19-acc-admin-densita-ui.md):
`/vsop/admin/airports`. Le [regole](../design/regole-ui-pagine-admin.md) la indicano come **il caso peggiore
del ramo** — e la ricognizione lo conferma col numero.

## Il difetto misurato (1600×900, IT, copia del DB di sviluppo)

| Cosa | Prima |
|---|---:|
| Altezza pagina | **13 745px** (viewport 900) |
| Prima riga di tabella | y **485** (breadcrumb 20 + testata 65 con sottotitolo + **lockbar in fascia** 59) |
| Tabella «Assegnati» | 92 righe, h 5 666, **niente `thead` fermo** |
| Tabella «Anagrafica IVAO» | 221 righe, h 13 107, **niente `thead` fermo** |
| Salto alla **prima spunta** | **+91px** (la `bulk-bar` compare sotto il puntatore) |
| `select` in pagina / nodi DOM | **223** / **11 648** |
| Righe già assegnate nell'anagrafica | **92 su 221** (verdi: rumore) |
| Campi filtro `.htree-*` / «?» di pagina | **0** / **0** |

Nelle righe si **scrive** (la ACC di competenza è una `select` per riga): l'intestazione delle colonne serve
proprio quando è fuori schermo. E il pannello di sinistra finisce a y 6 180 mentre quello di destra continua
fino a 13 674: metà dello scorrimento è vuoto a sinistra.

## Cosa cambia

1. **Due pannelli misurati, non una pagina che scorre.** `.topo-layout` prende l'altezza da
   `vipiFitViewport('.topo-layout', 900)` — rimisurata a **ogni** render, perché sopra compaiono e spariscono
   da sole la barra del lock e il chip d'esito. I due `.panel` diventano `.st-pane` e scorre **solo il corpo**
   (`.st-scroll`, `min-height:0` obbligatorio sui figli flex): titolo, filtri e barra delle azioni restano
   fermi. `min-height` del CSS = `fitMin` del JS (320): due pavimenti diversi sono un pavimento sbagliato.
   Sotto 900px di larghezza la griglia torna a una colonna e l'altezza fissa **sparisce** (`collapseBelow`).
2. **`thead` fermo su entrambe le tabelle** (`.res-table.sticky-head`). Dentro uno scroller lo sticky è
   relativo al **contenitore**, non alla finestra: serve la variante `top:0` (la regola generale porta
   `top:calc(62px + var(--st-head-h))`, giusta per una pagina che scorre, sbagliata dentro un riquadro).
3. **Testata in una riga** (`.st-head`): `Aeroporti · «?» · Auto-assegna · Re-importa tutti · —— · esito ·
   lock`. Sottotitolo via (il testo lo dice il «?», identico), lock in riga, callout d'esito → chip `.st-msg`
   con la ✕. «Auto-assegna» è comando di **pagina**: tocca l'anagrafica *e* l'elenco assegnati.
4. **Avanzamento vero sulle azioni lunghe.** «Re-importa tutti» sono **92 chiamate HTTP in sequenza**: prima
   c'era solo lo spinner. Ora il chip di testata dice `12 / 92`, conta i falliti mentre vanno, e porta
   **Interrompi**: chi si accorge che la sorgente risponde male non deve aspettare le altre ottanta. Vale per
   tutte le azioni di gruppo (sposta, genera documenti, elimina, assegna), che sono cicli identici.
5. **La barra delle azioni non salta più**: c'è sempre, spenta a zero, **fuori** dal corpo che scorre. Prima
   compariva alla prima spunta (+91px sotto il puntatore) e stava *dentro* l'area che scorre, quindi chi
   selezionava in fondo risaliva per confermare.
6. **Chi fallisce resta selezionato.** Le quattro azioni di gruppo facevano `Clear()` in ogni caso: la riga
   fallita spariva dalla selezione, e sparire senza essere stata scritta è la stessa perdita di prima.
7. **Filtri e conteggi**: `.struct-bar` con `.htree-search`/`.htree-select` (la lente è disegnata nel campo),
   pill «N di TOT» quando un filtro è attivo, totale secco quando non lo è.
8. **Chip di triage a sinistra** — `nascosti 18` · `no settori 8` · `no TWR 10`: sono uno **stato** che prima
   si leggeva solo scorrendo 92 righe. Cliccati filtrano, sempre presenti, spenti a zero.
9. **L'anagrafica IVAO mostra i non assegnati.** 92 righe verdi su 221 erano rumore: il chip
   «già assegnati (92)» le riporta, spento di default.
10. **Via l'assegnazione per riga.** La cella «ACC… + assegna» duplicava esattamente la barra di gruppo:
    −221 `select` e −221 bottoni. Assegnare un aeroporto solo costa un gesto in più (spunta), ed è
    l'eccezione: il flusso vero è «auto-assegna» più qualche gruppo. Spariscono con lei `_assignAcc`,
    `SetAssignAcc`, `Assign` e le chiavi `Apt_AccOption`/`Apt_Assign` da **entrambi** i resx.
11. **Emoji colorate → `Icon`**: 👁 → `eye`, 🙈 → `eye-off`, ⚠ → `warning`, 🔒 → `key`, ⚠️ → `warning`.
    Per 🚫 («nascosto: nessun settore») nel set **non c'è** l'equivalente: diventa `eye-off` in pill neutra,
    con il perché nel `title`. `✎ ↻ ⟳` restano testo: sono glifi monocromatici.
12. **Singolare e plurale** nei messaggi con contatore: «Spostati 1 aeroporti» era il genere di dettaglio che
    fa sembrare la pagina scritta da nessuno.
13. **Una sezione di Guida sua** (`#admin-aeroporti`), registrata in `GuideSearchCatalog` — altrimenti la
    ricerca globale non la trova — con le chiavi nuove in IT **ed** EN nello stesso giro.

## Pre-flight

- **Modello**: nessun concetto nuovo. Si tolgono stati locali (`_assignAcc`), se ne aggiungono di UI
  (`_stateFilter`, `_showAssignedIvao`, `_prog`). Nessuna entità, nessuna tabella.
- **Dispatch**: nessuno switch per tipo. Il ciclo delle azioni di gruppo è **uno solo** (`RunBulkOver`), non
  cinque copie: era già ripetuto quattro volte, ora conta e riporta in un posto solo.
- **Ingressi + verifica**: la pagina si raggiunge da Struttura; nessun catch-22 (l'anagrafica è la sorgente,
  l'auto-assegna crea il primo aeroporto). Verifica guidando il flusso reale con puppeteer/Edge, con **numeri**:
  altezza pagina, y della prima riga, `thead` fermo durante lo scorrimento, salto alla selezione, tick
  dell'avanzamento.
- **Propagazione**: la rimozione dell'assegnazione per riga porta via metodi, campi e chiavi resx nello stesso
  giro. Il foglio delle regole va aggiornato nella ricognizione (Aeroporti esce dalla lista «da rifare»).

## Slice

| # | Slice | Esito atteso |
|---:|---|---|
| 1 | Pannelli misurati + `thead` fermo + corpo che scorre | 13 745px → ~900 |
| 2 | Testata in riga, esito in chip, lock in riga, comandi di pagina | prima riga più in alto, niente fascia |
| 3 | Prosa → «?» + sezione Guida + catalogo ricerca | 0 paragrafi sempre a schermo |
| 4 | Filtri `.htree-*`, conteggi onesti, chip di triage, chip «già assegnati» | 221 → 129 righe utili |
| 5 | Azioni di gruppo: barra ferma, falliti che restano, avanzamento vero, plurali | salto 91 → 0 |
| 6 | Icone, pill in classi, nomi e chiavi morte | nessuna emoji-comando |

## Verifica

`dotnet build Vipi.slnx -c Release --no-incremental` su **entrambi** i TFM (gli avvisi sono errori) +
`dotnet test`: **verdi**, 0 avvisi, 2577 test (un fallimento isolato di `Vipi.AuroraBridge.Tests` non si è
riprodotto né da solo né alla ripetizione della suite: è instabile, e non tocca nulla di questo giro).

Poi guida live con Edge+puppeteer sul flusso reale (copia del DB di sviluppo, `/vsop/admin/airports`).

### Prima → dopo, misurato a 1600×900 in italiano

| Cosa | Prima | Dopo |
|---|---:|---:|
| Altezza pagina | 13 745px | **900** (la pagina non scorre: scorrono i pannelli) |
| Prima riga di tabella | y 485 | y **~380** (testata 51px in una riga, senza fascia del lock) |
| `thead` durante lo scorrimento | via a dieci righe | **fermo**: dopo 1500px di scorrimento resta a `gap=0` dal bordo del pannello |
| Salto alla prima spunta | +91px | **0** |
| Righe nell'anagrafica | 221 (92 verdi) | **129** utili, le altre dietro al chip |
| `select` in pagina / nodi DOM | 223 / 11 648 | **95 / 7 179** |
| Prosa sempre a schermo / «?» | 1 paragrafo / 0 | **0 / 3** (pagina, anagrafica, lock) |
| Conteggi | «92» e «221» fissi | «92» e «**129 di 221**»; col chip «no TWR»: «**10 di 92**» |

### Assetti e zoom

1600 / 1440 / 1280 / 1024, IT ed EN, zoom 0.8 → 1.5: la pagina **non scorre mai** e i pannelli **non hanno
scorrimento orizzontale** (`scrollWidth - clientWidth = 0` ovunque). Due difetti trovati **solo guardando gli
screenshot**, non dai numeri:

- a 1280 la colonna dei tasti finiva **oltre il bordo** del pannello (si arrivava al cestino solo scorrendo in
  orizzontale): «Nascondi/Mostra» è diventato icona sola come gli altri due della colonna, col nome nel
  `title` e nell'`aria-label`;
- la colonna «Assegnazione» era una **colonna di trattini** larga un quarto del pannello, perché i già
  assegnati sono nascosti: ora esiste solo quando il chip li riporta.

La barra delle azioni sta in **una riga** a 1600 e 1440 (47px) e in due sotto (84px): il taglio è stabile per
larghezza, e la ✕ del «deseleziona» è salita accanto al contatore che azzera — in fondo era lei ad andare a
capo per prima.

### L'avanzamento, guidato davvero

Preso il lock e premuto «Re-importa tutti», il chip di testata è stato campionato ogni 900ms:
`4/92 → 9/92 → 13/92 → … → 48/92`. Premuto **Interrompi**, l'esito è stato
«Re-import da IVAO completato per 49 aeroporti. Interrotto: 43 non provati.» — e il chip di avanzamento è
sparito lasciando quello d'esito.

### I falliti restano

Selezionato **LIBC** (1 settore) e premuto «Elimina»: l'esito dice «Eliminati 0 aeroporti, **1 non eliminato**
(settori collegati).», il contatore resta a «1 selez.» e la casella di LIBC è ancora spuntata. Prima la riga
spariva dalla selezione senza essere stata cancellata — e il singolare è quello giusto.
