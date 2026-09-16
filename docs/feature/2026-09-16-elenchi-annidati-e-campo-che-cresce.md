# Elenchi annidati e campo che cresce col testo (16 settembre 2026)

> Stato: ✅ **ESEGUITA il 16 settembre 2026**, 📦 **in 1.28.0**. **Nessuna migrazione.**
> Continua [2026-09-09-testo-ricco-nell-editor.md](2026-09-09-testo-ricco-nell-editor.md), di cui supera la
> decisione 5 («niente elenchi annidati»).

**La richiesta del committente**, per i campi di testo degli editor:

1. il campo si adatta alla lunghezza del testo, tenendo la maniglia per regolarlo a mano;
2. elenchi puntati e numerati **annidati fino a cinque livelli**, ogni livello col suo simbolo — numerati
   `1` → `a` → `I` …, puntati pallino → trattino … — scritti `-`, `--`, … e `1)`, `-1)`, …;
3. (aggiunta) un puntato dentro un numerato **e viceversa**.

Decisioni del committente sul piano: il campo si adatta nei campi di prosa più note delle aree e
descrizione degli incarichi; tetto al **60%** dello schermo; **Invio** continua l'elenco.

## 1. Misura prima di costruire

- Renderer unico (`MarkdownLite`, righe → HTML), campo unico (`RichTextArea`, 7 posti), barra e gesti in
  `vipi-editor.js`. Nessun altro consumatore del formato.
- `vipi.db` locale: **84 corpi** di prosa, **zero** righe `--`, `-1)`, elenchi o voci rientrate. La sintassi
  nuova non cambia il significato di niente di scritto. ⚠️ È una copia vecchia: la produzione non si vede.
- Traduzione: il corpo si spezza sulle righe vuote e il paragrafo partiva **coi marcatori dentro**.

## 2. Sintassi

| livello | puntato | numerato | simbolo numerato | simbolo puntato |
|---|---|---|---|---|
| 1 | `- voce` | `1) voce` | 1. | • |
| 2 | `-- voce` | `-1) voce` | a. | – |
| 3 | `--- voce` | `--1) voce` | I. | ◦ |
| 4 | `---- voce` | `---1) voce` | i. | ▪ |
| 5 | `----- voce` | `----1) voce` | A. | · |

- Restano validi a primo livello `* voce`, `+ voce`, `• voce`, `1. voce`.
- **Lo spazio dopo il marcatore è obbligatorio**: `---`, `--nota`, `-5 gradi` restano testo.
- Il **tipo si decide per voce**: i livelli si mescolano. Allo stesso livello un cambio di tipo chiude un
  elenco e ne apre un altro dentro la stessa voce genitrice.
- Gli spazi in testa **non** contano: il livello lo dicono i trattini.
- Oltre il quinto si resta al quinto; un salto (1 → 3) si aggancia al livello subito sotto.
- Il numero conta sulla prima voce di un elenco (`start`), a ogni livello.

## 3. Com'è fatta

- **`VoceDiElenco`** (`Vipi.Application/Content`) — la porta sola della sintassi. La leggono il renderer e il
  protettore della traduzione.
- **`MarkdownLite.Elenco`** — pila di elenchi aperti; `<ul>`/`<ol>` annidati **dentro** `<li>`; classe
  `md-list md-lN`.
- **CSS** — `ol.md-lN` / `ul.md-lN` con `list-style-type` per livello e tipo (non da selettori di antenati,
  che sbaglierebbero sui misti). Trattino e punto come stringhe con escape (`"\2013  "`, `"\B7  "`). In
  stampa `break-inside:avoid` solo sulle voci **senza** annidati.
- **Traduzione** — `TextProtector.Protect` toglie i marcatori riga per riga **prima** di tutto il resto
  (`ProtectedText.Marcatori`); `TryRestore(tradotto, ProtectedText, …)` li rimette e **scarta** se il motore
  ha cambiato il numero di righe. Non un segnaposto: il motore sposta le parole dentro i tag (misurato sui
  grassetti), e un `--` spedito può tornare «–». Il **sorgente in memoria non cambia** → impronte uguali,
  nessuna traduzione da rispendere. Effetto collaterale buono: `- MARTE` torna una parola sola maiuscola e
  non parte più.
- **Editor** (`vipi-editor.js`) —
  - `vipiMdList` tiene i livelli e scrive `1)`; `vipiMdRientro(el, ±1)`; rinumerazione dell'elenco contiguo
    (il numero di partenza delle righe **non toccate** si rispetta);
  - **Tab / Maiusc+Tab** si consumano **solo se spostano una voce**: su un capoverso, al 5° livello o al 1°
    con Maiusc, il tasto cambia campo come sempre — nessuna trappola da tastiera;
  - **Invio** su una voce apre la successiva (numero +1); su una voce vuota risale di un livello, dal primo
    esce dall'elenco; col cursore dentro il marcatore è un a capo normale;
  - `RichTextArea`: tasti **⇤ ⇥** (`Rta_Outdent`, `Rta_Indent`) con la scorciatoia nel titolo.
- **Campo che cresce** — `textarea[data-adatta]`: `RichTextArea`, note di `MilWorkingAreas`, descrizione in
  `AdminTasksPage`. Misura con `height:auto` (rispetta `rows`), tetto 60% di `innerHeight`, poi scorre
  dentro; l'altezza trascinata a mano (`pointerup`) diventa il minimo; lo scorrimento della pagina si
  ripristina dopo la misura. Si misura al digitare, sui nodi nuovi (MutationObserver), all'apertura di
  una sezione (`toggle`) e al ridimensionamento. Stato in proprietà JS, non in `data-*`.

## 4. Presidi

- `MarkdownLiteTests` — 11 nuovi: annidati puntati e numerati, i due misti, cambio di tipo allo stesso
  livello, salto di livello, tetto a 5, `start` annidato, trattini senza spazio, chiusura di tutti i
  livelli, un simbolo CSS per livello e tipo.
- `TextProtectorTests` (4) e `TranslationFillUseCaseTests` (2) — marcatori che non partono, tornano al loro
  posto nel giro vero, righe fuse → scartata, `- MARTE` non parte.
- `ElenchiNellEditorTests` — **le regex JS sono testualmente quelle C#**, i marcatori che il JS scrive si
  leggono al loro livello, tasti di rientro (verso, fuoco), `data-adatta`.
- Prova da banco in Node dei gesti (25 casi: tasti elenco, Tab/Maiusc+Tab, Invio, rinumerazione, cursore).
  Non è nel repo: non c'è un esecutore JS, e non se ne introduce uno per questo.

## 5. Da verificare dal vivo

- ✅ **Provato sul pacchetto 1.28.0** con `testo-verifica.js`: Tab/Maiusc+Tab veri, ⇤ ⇥, Invio che continua e
  che esce, Tab su un capoverso che cambia campo, e un secondo livello fatto col Tab che **sopravvive al
  ricarico** e si rende `<ul class="md-list md-l2">` dentro la voce.
- ▶ Resta: scrivere un elenco a 3 livelli a mano, salvare, **ricaricare** (il `change` sintetico, vedi §CL).
- Simboli dei cinque livelli nel documento e in stampa.
- Campo che cresce: dentro una sezione chiusa che si apre; dopo aver trascinato la maniglia; a fine pagina
  (nessun salto di scorrimento).
- Firefox e Safari: `list-style-type` a stringa.
