# L'intro di pagina: sezioni editabili in cima a un elenco — 30 agosto 2026

> **Da dove nasce:** richiesta del committente sull'elenco dei vSOP militari (`/services/vsop/mil`) —
> «in alto una sezione intro dove mettere alcuni PDF, come se fosse un documento, usando la funzione link
> delle sezioni».
> **Ramo:** `intro-di-pagina`, da `main`. **Nessuna migrazione**, e non per prudenza: il contenitore c'è già.
> **Tradotta**, su richiesta esplicita del SOD.

## 1. Il vincolo di partenza: niente schema nuovo

Siamo nella **finestra cieca fino al 16 settembre** (vedi `docs/lavori-aperti.md`): il pacchetto in
produzione gira `Migrate()` all'avvio, e una migrazione sbagliata è il sito giù senza modo di rientrare.
Una funzione che si può fare **senza DDL** in questa finestra vale il doppio di una che non si può.

## 2. Dove si salva, e perché lì

`SharedBlocks`. La tabella esiste dall'`InitialCreate` — quindi **è già in produzione, su MariaDB e su
SQLite** — e **oggi nessuno la scrive** (`EfMediaMaintenance`: «oggi NESSUNO li crea»). È contenuto
**senza padrone**, chiavato su una stringa unica: esattamente la forma che serve a una zona di pagina, che
un padrone non ce l'ha — non è di un aeroporto, non è di un settore, non è di un documento.

- `Key` = `page-intro:mil`. Il prefisso non è decorativo: la seconda pagina che vorrà un'intro registra una
  chiave, **non un secondo meccanismo**.
- `BodyJson` = le sezioni. `Title` = l'etichetta che lo staff legge nell'editor. `Format` non si usa.
- ⚠️ **Niente release, niente ciclo AIRAC, niente freeze.** Non è un `Document`, e non deve diventarlo: un
  `Document` senza aeroporto e senza settori **cade fuori da tutti i descrittori** (`IReleaseTarget`), quindi
  sarebbe invisibile all'elenco admin, al motore di release e agli impatti — irraggiungibile in silenzio, che
  è il guasto già pagato col catch-all dell'aeroporto. L'intro è **contenuto di contorno**: si pubblica quando
  si salva. Contenuto normativo qui **non ci va**, e la carta lo dice per iscritto.

## 3. Il modello: nessun modello nuovo

Il pezzo si compone di tre cose che ci sono già, e ne aggiunge **zero**:

| Cosa serve | Cosa si usa | Da dove viene |
|---|---|---|
| editare i blocchi | `DocumentBlocksEditor` | ha già `+ Allegato`, `+ Immagine`, tabella, callout |
| scegliere il PDF | `AttachmentBlockEditor` + `AttachmentLink` | biblioteca allegati, modo Link/Embedded |
| rendere le sezioni | `SectionNode` / `SectionBody` / `BlockRenderer` | gli stessi dei cinque viewer |
| tradurre | `DocumentTranslator.TranslateAsync` | la macchina di sempre, su una `DocumentView` costruita al volo |

Salvato c'è **un modello solo**: quello che lo staff scrive (`PageIntroSection` = titolo + `ExtraBlock`).
La `DocumentView` è una **proiezione**, calcolata a ogni resa e mai salvata — la stessa regola per cui
`Sector` è una proiezione dei cataloghi.

- ⚠️ `ExtraBlock` è l'involucro degli extra d'aeroporto, che **sta venendo ritirato** verso sezioni di
  documento vere (`EfDocumentMaintenance`). Riusarlo qui è una scelta, non una distrazione: l'editor lo parla
  già, e ciò che il ritiro condanna è il **secondo storage editoriale di un documento**, non un formato di
  blocchi per contenuto che un documento non è. Il giorno che `ExtraBlock` sparisce, questo pezzo si converte
  con lo stesso mapper che converte gli extra.

## 4. La traduzione

Su richiesta esplicita del SOD l'intro **si legge in inglese** come tutto il resto.

Due agganci, e sono i due che esistono già per i testi fuori dai documenti (le aree regolamentate, carta
documenti-bilingue §4):

1. **Corpus** — `EfTranslatableCorpus` scandisce anche `SharedBlocks` nel giro `it`, così il riempimento
   automatico ogni quarto d'ora porta in memoria le frasi dell'intro. Senza questo pezzo la pagina chiederebbe
   alla memoria delle frasi che nessuno le ha mai messo dentro, e resterebbe italiana **senza che nulla
   protesti**.
2. **Resa** — la pagina costruisce la `DocumentView` e la passa a `DocumentTranslator.TranslateAsync` con
   sorgente `It`. Quel che manca resta in italiano, come ovunque: un testo a chiazze si legge, uno coi buchi mente.

⚠️ **L'intro nasce in italiano**, e non c'è una colonna che lo dica: `SharedBlock` non ha una lingua. È una
scelta dichiarata qui — la divisione scrive in italiano — e sta in **un posto solo** (`PageIntro.Sorgente`),
non ripetuta a ogni chiamante.

## 5. Ingressi (pre-flight §3)

- **Raggiungere**: la zona sta in cima a `/services/vsop/mil`, sopra la testata. Se non c'è contenuto, per il
  pubblico **non si rende niente** — non un contenitore vuoto.
- **Creare**: niente catch-22 possibile, perché non c'è nessuna lista da cui l'intro debba comparire. Allo
  **staff** (`IsEditor`) la zona si mostra sempre, anche vuota, con «Aggiungi sezione».
- **Lock**: `EditResourceLock` con chiave `page-intro:mil`, come `structure` e `newdoc` — due editori sulla
  stessa intro sono l'ultimo che salva che vince, e qui vincerebbe in silenzio.

## 6. Verifica

Guidando il flusso vero, non solo i test: creare una sezione, metterci un blocco allegato che punta a un PDF
della biblioteca, salvare, ricaricare **da sloggato**, e rileggere la pagina in inglese.
