# Editor ACC — densità e uso (CARTA, 20 agosto 2026)

Sesto giro della famiglia, dopo [accordi](2026-08-19-accordi-densita-ui.md),
[struttura](2026-08-19-struttura-densita-ui.md), [ACC admin](2026-08-19-acc-admin-densita-ui.md),
[aeroporti](2026-08-19-aeroporti-densita-ui.md) e [editor aeroporto](2026-08-20-editor-aeroporto-densita-ui.md):
`/vsop/{acc}/editor` — `src/Vipi.Ui/Pages/AccEditorPage.razor`. Prima voce della lista «da rifare» nelle
[regole](../design/regole-ui-pagine-admin.md) (6 466px misurati in ricognizione).

**Questa è la carta, non il resoconto**: i numeri del «dopo» si scrivono dopo averli misurati.

## 1. Cosa c'è oggi (letto nel codice, non ancora misurato)

| # | Cosa | Dove | Regola |
|---:|---|---|---|
| a | Testata su **due righe**: `.doc-head` con `<h2>` e sotto il sottotitolo `Acc_Subtitle` («Settori di aerovia + gruppi APP»), che ripete quello che dice il pangrattato | `AccEditorPage.razor:38-42` | 1, 6, 7 |
| b | **Nessun «?» di pagina** e nessuna pill di stato in testata (la pill Bozza/Pubblicata sta solo nel rail) | idem | 7, 12 |
| c | **Errore in fascia** sopra il contenuto: spinge in giù tutto il documento proprio mentre ci si lavora | `:51` | 5 |
| d | **Lock altrui in fascia**, e la stessa informazione è già un badge nel rail | `:46-50` + `:322-327` | 1, 5 |
| e | I **blocchi** (Aerovia, gruppi APP) sono `<div class="acc-block">` **non collassabili**: dentro ognuno 9 (Aerovia) o 10 (gruppo APP) sezioni di primo livello, tutte aperte tranne `regulated` | `:62-104`, `SectionCatalog.cs` | 0 |
| f | Il TOC **non ha Espandi/Comprimi tutto** (l'editor aeroporto sì, stesso componente `EditorToc.Footer`) | `:55` | 68 |
| g | `vipiEditorSections` apre/chiude **solo** `details.ed-sec`: non conosce le card `details.block.cb` di questo editor | `vipi-editor.js:37` | — |
| h | **Prosa sempre a schermo**: `Aor_ExtraShapesHelp`, `Aor_ColorsHelp`, `Acc_LinkedFreqsHint` sulla pagina; `AppFreq_EditHelp`, `AppCfg_EditHelp`, `Acc_AllAreasAuto` + i «CountHint» nei componenti condivisi. Moltiplicata **per blocco** | `:496,502,536`; `AppFrequencies:82`, `AppConfigurations:9`, `RegulatedAreasEditor:31,88,156` | 7, 8, 9 |
| i | **Nessuna tabella con `thead` fermo**: frequenze, `cfg-table` delle configurazioni, coordinamenti e VFR sono `<table>` nudi | componenti `App/*` | 23, 51, 57 |
| j | **Emoji comandi**: 💾 ✎ 🔒 ✓ nel rail; 👁 🙈 ¶ ⤒ ⤓ nell'editor di sezioni condiviso | `:318-334`, `DocumentSectionsEditor:255-283` | 40 |
| k | **AoR: 340px di mappa fissi per blocco**, più chip settori, chip configurazione e due righe di prosa | `AccAor.razor:22` | 0 |
| l | Stringhe **scritte a mano in italiano** nel viewer AoR («Mappa 2D», «Configurazione:», «azzera», «Shape AOR non disponibile», le due frasi di spiegazione) e `"Nuovo gruppo APP"` nella pagina | `AccAor.razor`, `AccEditorPage:286` | 43 |

Quello che invece **è già a norma** e non va toccato: TOC e rail sono appiccicati (`.editor-toc-side` / `.ed-rail`,
`top:70px`), quindi chi lavora in fondo ha i comandi a schermo; le àncore hanno il loro `scroll-margin-top`; le
mappe dentro un `<details>` chiuso sono già gestite (`vipi-aor.js:134`, `invalidateSize` all'apertura) —
**collassare non rompe le mappe, le rende più leggere**.

## 2. Giro 0 — misurato (1600×900, copia del DB di sviluppo)

| Pagina | In lettura | **In modifica** |
|---|---:|---:|
| `/vsop/libb/editor` | 6 466px | **9 690px** |
| `/vsop/limm/editor` | 5 020px | **8 155px** |

**Il primo numero della ricognizione era quello sbagliato**: l'editor si guarda poco e si *modifica*, e in
modifica la pagina cresce del 50%. Ripartizione in modifica (LIBB, blocco Aerovia = 4 513px):

| Sezione | In lettura | In modifica | Cosa la gonfia |
|---|---:|---:|---|
| AOR | 587 | **1 074** | mappa 446px + picker shape extra + colori + due prose |
| Frequenze | 643 | **1 040** | 12 righe da ~60px + prosa + riquadro «collega frequenza» |
| Coordinamenti | 545 | 551 | già a `<details>` annidati con Espandi/Comprimi propri |
| Configurazioni | 189 | 506 | |
| Separazioni radar | 207 | 228 | |
| Procedure generali | 90 | 269 | |
| Minime / Validità / Nuova sezione | 90 | **139** ciascuna | una sezione **vuota** costa 139px di soli «+ Paragrafo/Callout/Tabella/Immagine» |
| Aree regolamentate | 88 | 93 | nasce chiusa (catalogo) — è la prova che il collasso funziona |

I due blocchi di LIBB fanno 4 513 + 3 744 = 8 257px degli 9 690 totali: **la pagina è i blocchi**, e un blocco
chiuso vale quanto dieci sezioni e una mappa in meno. Testata 84px, TOC 798 (21 voci), rail 221, release 974.

Trovato guardando gli screenshot (regola 47) e non previsto dalla carta:

- **La riga della tabella frequenze è alta ~60px** (12 righe = 761px in modifica). È la regola 59 in piccolo.
- Una sezione **senza contenuto** non è gratis: 139px per la sola barra dei «+».
- In **EN** la pagina è **mista**: i testi localizzati passano all'inglese e restano in italiano le frasi
  scritte a mano dentro `AccAor` («Accendi/spegni i settori sopra la mappa…», «Configurazione:», «azzera»),
  «Trasferimenti dei settori del blocco…» e i titoli di sezione che vengono dal DB (quelli sono dato, non lingua).
- I coordinamenti **hanno già** i loro «Espandi tutto / Comprimi tutto» annidati: i tasti nuovi del TOC devono
  convivere con quelli, non sostituirli.

## 3. Le slice (una per commit)

**Ordine**: prima ciò che serve a chi lavora in fondo alla pagina, poi ciò che libera spazio, poi la forma.

### S1 — Testata in una riga
`.doc-head.st-head`: `✎ Editor vIPI {ACC}` · «?» di pagina · pill Bozza/Pubblicata. Il sottotitolo `Acc_Subtitle`
**non si riscrive**: stessa chiave, dentro il corpo dell'`HelpHint` (regola 8). Fascia errore → `.editor-toast`
fisso, come l'editor aeroporto (`AeroportoEditorPage:807`); fascia lock altrui → via, resta il badge del rail
(regola 5 — e la stessa cosa dichiarata due volte è già un difetto suo).

### S2 — Il blocco si chiude, e si chiudono tutti insieme
`.acc-block` diventa un `<details>` con persistenza (`data-persist="blk-{id}"`, il meccanismo che c'è già in
`CollapsibleBlock`), il titolo del gruppo nel `summary` e — in modifica — i comandi del gruppo (rinomina, ✕) che
**non** collassano (`preventDefault`, come già fa `SectionHeader`). In fondo al TOC i due tasti
**⊞ Espandi tutto / ⊟ Comprimi tutto**, identici a quelli dell'editor aeroporto (regola 68), e
`vipiEditorSections` generalizzato a `details.ed-sec, .ed-layout details.cb` — **un solo** helper, non un secondo.
È la slice che vale di più: un gruppo APP chiuso sono dieci sezioni e una mappa in meno.

### S3 — La prosa diventa «?»
Testi **riusati identici**, stesse chiavi (regola 8), `ExtraClass="wide"` dove sono paragrafi (regola 9):
- sulla pagina: `Aor_ExtraShapesHelp`, `Aor_ColorsHelp` (le due `<h4>` diventano righe `.ed-h3` col «?»),
  `Acc_LinkedFreqsHint`;
- nei componenti **condivisi**: `AppFreq_EditHelp` (`AppFrequencies`), `AppCfg_EditHelp` (`AppConfigurations`),
  `Acc_AllAreasAuto` (`RegulatedAreasEditor`).

⚠️ Regola 49: quei tre componenti li montano anche **editor APP**, **editor vLOA**, **AppnPage**, **LivePage** e
`AccSectionBody` (viste pubbliche). Il «?» va bene ovunque, ma va **contato su tutte** prima di dire che è fatto —
e nei viewer pubblici la prosa che è *contenuto* resta contenuto (fuori ambito, in coda alle regole).

### S4 — Le tabelle lunghe (dopo la misura del giro 0)
Per le tabelle che sul DB vero superano la soglia: `.ed-pane` (tetto `max-height:min(64vh,660px)`, regola 57 —
**non** `vipiFitViewport`: questa pagina scorre) con `thead` fermo a `top:0` (regola 51) e classi
`.res-table.sticky-head`. Candidate in ordine: **coordinamenti**, **frequenze**, `cfg-table` delle configurazioni.
Se col tetto una tabella taglia ancora le colonne, larghezze **misurate col font** (regole 60-62), non percentuali.
Le tabelle che restano corte davvero **non si toccano**: sotto le ~20 righe lo sticky non si ripaga (regola 23 e
riga «Audit» della ricognizione).

### S5 — Icone, nomi, lingua
Emoji comandi → `Icon` (💾 → `save`/`check-circle` come nel rail dell'aeroporto, 🔒 → `lock`, 👁/🙈 → `eye`/`eye-off`);
i glifi monocromatici (✎ ✕ ⊞ ⊟ ¶ ⤒ ⤓) restano testo. 🔴 Live / 🧊 Congelata **restano**: sono vocabolario di stato,
come le emoji della pagina Versioni, finché non c'è il set di pallini (rinviato in `piano-ux-hardening`).
Nello stesso giro le stringhe italiane scritte a mano di `AccAor` e `"Nuovo gruppo APP"` prendono la loro chiave
IT+EN (regola 43). ⚠️ `DocumentSectionsEditor` è condiviso da tre editor: si conta su tutti e tre.

### S6 — Guida e ricerca
Ogni «?» nuovo punta a una sezione vera: `#editor-acc` (pagina) e le àncore per AoR extra/colori, frequenze
collegate, configurazioni, aree regolamentate. Sezioni create in `GuidaPage` **e** registrate in
`GuideSearchCatalog`, altrimenti la ricerca globale non le trova (regola 12). Chiavi nuove sempre IT+EN.

### S7 — Carta chiusa e regole aggiornate
Questo file si completa coi numeri prima→dopo; nelle [regole](../design/regole-ui-pagine-admin.md) l'editor ACC
passa da «da rifare #1» a «già a norma», e ciò che il giro insegna diventa una voce nuova.

## 4. Quello che qui NON si applica (e perché)

- **Testata appiccicata (regola 4)**: no. I comandi di scrittura stanno nel rail, che è già appiccicato — come
  sull'editor aeroporto. Una fascia appiccicata in più toglierebbe righe senza dare niente.
- **Altezza misurata con `vipiFitViewport` (regola 13)**: no. È una pagina che **scorre**; qui vale la 57 (tetto).
- **Salva-tutto e stato «sporco» (regole 35-38)**: no. Questo editor **salva a ogni gesto** (`Guard` → `Save…` →
  `LoadAsync`): non esiste una scrittura pendente da perdere, e un contatore di modifiche sarebbe un comando che
  non fa niente. ⚠️ Il rovescio, da verificare nel giro 0: ogni gesto ricarica il documento **e** ri-deriva quattro
  viste per blocco (`RefreshBlockDerived`) — se sul DB vero si sente, è un difetto di reattività da annotare, non
  da risolvere in questo giro.

## 5. Proposte in più — decise il 20 agosto

1. ❌ **Un blocco alla volta** (chip-filtro in testata): **non si fa**. Il blocco si comprime e basta: la
   compressione è già la risposta all'altezza, un filtro sarebbe un secondo modo di nascondere lo stesso
   contenuto — due meccanismi per la stessa cosa.
2. ✅ **⤢ Larghezza piena** riusando `.ed-layout.sid-wide` (indice e rail via) quando si lavora su una tabella
   larga: il codice esiste già per l'editor aeroporto, qui è una classe. → **S8**
3. ✅ **Accordion**: aprire un blocco chiude i fratelli (visto che la 1 non si fa, è lei a tenere corta la
   pagina). Resta possibile aprirne più d'uno con «Espandi tutto». → dentro **S2**
4. ✅ **Il lock in chiaro** in testata (`🔒 fino alle HH:mm`) invece del solo `title` nel rail. → dentro **S1**

### S8 — ⤢ Larghezza piena
Tasto in testata che toglie indice e rail (`.ed-layout.sid-wide`, già in `vipi-theme.css:1986`) per lavorare le
tabelle larghe. ⚠️ Regola 56: la classe vince perché sta **in coda** al foglio — le regole nuove restano lì.

## 6. Verifica (regole 46-49)

- `dotnet build Vipi.slnx -c Release --no-incremental` verde su **entrambi** i TFM + `dotnet test`.
- Guidata sul flusso reale: aprire l'editor di un ACC vero, entrare in modifica, collassare/espandere, salvare una
  sezione, verificare che il collasso persista e che le mappe si ridisegnino all'apertura.
- Assetti 1600 / 1440 / 1280 / 1024, **IT ed EN**, zoom 0.8→1.5 (regola 48).
- Numeri prima→dopo e screenshot **guardati**, non solo prodotti (regola 47).
- Componenti condivisi: contare l'effetto su editor APP, editor vLOA, AppnPage, LivePage e viste pubbliche.
