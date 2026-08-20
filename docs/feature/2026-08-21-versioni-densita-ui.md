# Versioni — densità e uso (21 agosto 2026)

> Ottava pagina del ramo di modifica, **prima** della lista «da rifare» di
> [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md) §15: **1 664px** misurati a 1600×900, in
> italiano, con l'elenco **chiuso**. Ma la pagina si usa **aperta** (regola 69): una riga espansa srotola
> versioni, storia e release **dentro l'elenco**, e «Espandi tutti» le apre tutte insieme.
>
> La parte **lock e azioni** — che non era di forma ma di sostanza — è già stata fatta lo stesso giorno:
> [`2026-08-21-versioni-lock-e-azioni.md`](2026-08-21-versioni-lock-e-azioni.md) (regole 95-105). Questa carta
> è il seguito che restava: **la densità**.

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo. `ManagedDoc` resta l'unica sede della riga d'elenco; il documento
   **scelto** è stato di pagina (una chiave `RowKey`), non entità. Nessuna nuova query: i conteggi dei chip
   escono dall'elenco che la pagina ha già in mano (regola 103, «un fatto un posto»).
2. **Dispatch** — nessuno `switch(tipo)` nuovo: quelli per `ManagedDocKind` (icona, etichetta) restano dove
   sono, uno solo per fatto; le rotte continuano a passare da `IDocRoutesRegistry`.
3. **Ingressi + verifica** — la pagina si raggiunge dalla topbar («Editor») e dal breadcrumb admin; il «?»
   punta a una sezione della Guida **che va creata** (`#versioni`) e registrata in `GuideSearchCatalog`,
   altrimenti la ricerca globale non la trova (regola 12). Verifica: live con `verifica-live` a
   1600/1440/1280/1024, **IT ed EN**, zoom 0.8→1.5, e con un lock **altrui** scritto nel DB della copia
   (ricetta nell'[handoff](../history/handoff-densita-ui.md)).
4. **Propagazione** — spariscono «Espandi tutti / Comprimi tutti» con le chiavi `Ver_ExpandAll` /
   `Ver_CollapseAll`, e la riga di prosa del conteggio con `Ver_OfTotal` / `Ver_DocsWord` /
   `Ver_FilteredSuffix`: il testo tolto porta via la sua chiave da **entrambi** i resx (regola 8). Le chiavi
   dei testi **spostati** nel «?» (`Ver_Subtitle`) restano identiche.

## Cosa non va, misurato

| | Difetto | Regola |
|---|---|---|
| 1 | Testata in due pezzi: `h2` + **sottotitolo sempre a schermo**, e **nessun «?»** in tutta la pagina | 1, 6, 7 |
| 2 | **3 callout in fascia** sopra il contenuto: errore, esito, e il riepilogo di campagna | 5 |
| 3 | 4 paragrafi `muted`/`help` + 4 `span.muted` sempre a schermo | 7 |
| 4 | I filtri sono `Chip` **sciolti** con lo stile in linea, **non** `.sh-chip` in gruppo, e **non contano** | 30, 31, 32 |
| 5 | ⚠️ **Quattro** gruppi di filtri (tipo, stato, release, ACC): la barra è cresciuta dopo il giro lock | 32 |
| 6 | Il conteggio filtrato è una **riga di prosa** invece di un pill accanto al titolo | 1, 30 |
| 7 | Il **riepilogo di campagna** (in vigore / programmata / senza release / in modifica) è una fascia che dice **gli stessi numeri** che i filtri dovrebbero portare | 103 |
| 8 | Il dettaglio si apre **dentro l'elenco**: la riga sotto salta in giù, ed è quella che si stava guardando | 80 |
| 9 | «Espandi tutti» apre **tutte** le righe: N×3 query e una pagina che non finisce più | 0 |
| 10 | `.wrap` a **1 100px**: la pagina di lavoro sta in due terzi di schermo mentre le righe vanno a capo | — |
| 11 | ⚠️ Una riga resta alta **118px** invece di 67: lock altrui **più** diritti da admin = sette elementi più il force-unlock | 100, 101 |
| 12 | ~**27 blocchi di stile in linea**: la forma della pagina non è in nessun foglio | 44 |

## La forma nuova

```
.wrap.struct
 └ .doc-head.st-head       titolo + pill «N di TOT» · pill AIRAC ciclo · «?» · [↻ Aggiorna] [+ Nuovo documento] · chip esito/errore
 └ .ver-layout             griglia 1.35fr / 1fr, altezza MISURATA (vipiFitViewport, collapseBelow 900)
    ├ .panel.st-pane       SINISTRA — l'elenco dei documenti
    │   ├ .struct-bar      ricerca · chip stato/release CHE CONTANO · chip tipo · chip ACC · ✕
    │   └ .st-scroll       le righe (solo il corpo scorre)
    └ .panel.st-pane       DESTRA — il documento scelto
        ├ testata          titolo · tipo · ambito · pill di stato e lock · azioni (editor, nascondi, elimina, sblocca)
        └ .st-scroll       versioni · storia modifiche · release (+ pubblica al ciclo / ora)
```

**Perché il dettaglio a destra** (regola 80). È la stessa lezione di Confinanti, pagata lì su 33 coppie: una
riga espansa **sposta** la riga successiva, cioè proprio quella che si stava per guardare. Qui il dettaglio è
più pesante di quello di Confinanti — versioni, storia e release, ognuno un elenco — e c'era pure un tasto per
aprirli **tutti insieme**.

**«Espandi tutti» sparisce, e non è una perdita.** Esisteva solo perché il dettaglio stava nell'elenco: con un
pannello a fianco si guarda un documento alla volta, che è come si lavora (si pubblica una release, si scarta
una bozza). Apriva anche N×3 chiamate — versioni, release e storia per ogni riga — per un elenco che se ne fa
niente.

**Il riepilogo di campagna e i filtri sono lo stesso fatto** (regola 103). La fascia diceva «3 in vigore ·
2 programmate · 1 senza release · 1 in modifica»; i chip dei filtri dicevano le stesse quattro parole **senza**
i numeri. Restano i chip, **coi numeri dentro**: la fascia sparisce, i filtri iniziano a contare, e il ciclo
corrente — l'unica cosa della fascia che non fosse un doppione — diventa un pill accanto al titolo.

**Le azioni salgono nel pannello.** Nascondi, elimina, apri editor e force-unlock stavano **dentro la riga**,
e con essi la conferma in linea: è la ragione della riga a 118px (difetto 11) e del pavimento misurato della
regola 100. Nel pannello del documento scelto hanno una riga tutta per loro; la riga d'elenco resta identità e
stato — icona, titolo, tipo · ambito, pill, lock.

**Le emoji di stato restano** (🟢 🕒 🕓 ⚠️ 🔒). Sono **vocabolario**, non comandi: la regola 40 salva le emoji
che sono comandi, e il set di pallini colorati che le sostituirebbe è deferito in `piano-ux-hardening`. Stanno
nel markup, non nei resx — quando arriveranno i pallini si tolgono senza toccare le traduzioni.

## Slice

1. **Carta** (questo file).
2. **Testata in riga**: `.st-head`, sottotitolo nel «?» (sezione di Guida `#versioni` + `GuideSearchCatalog`),
   pill «N di TOT» e pill del ciclo, i due callout d'esito/errore in chip `.st-msg`.
3. **Barra dei filtri**: `.struct-bar` + `.sh-chip` in gruppo **coi conteggi**; il riepilogo di campagna
   sparisce; la riga di prosa del conteggio sparisce.
4. **Layout di lavoro**: `.wrap.struct` + `.ver-layout` a due pannelli misurati; il dettaglio esce dall'elenco
   e va a destra con le azioni; «Espandi tutti» sparisce.
5. **Stili in linea → classi**, verifica live guidata (assetti, lingue, zoom, lock altrui), e chiusura:
   regole §19, ricognizione §15, handoff, memoria.

## Verifica

Guidata con la skill `verifica-live` sulla copia del DB: 1600/1440/1280/1024 × IT/EN × zoom 0.8→1.5, con
`Accept-Language` **scritto a mano** (senza, il browser headless parla la lingua del sistema e la prova «in
italiano» verifica l'inglese). Casi da guidare, non solo misurare: documento con e senza release, bozza aperta,
documento nascosto, lock **mio**, lock **altrui** con e senza diritti da admin, e i filtri che portano l'elenco
a zero.
