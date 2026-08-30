# L'intro di pagina: sezioni editabili in cima a un elenco — 30 agosto 2026

> **Da dove nasce:** richiesta del committente sull'elenco dei vSOP militari (`/services/vsop/mil`) —
> «in alto una sezione intro dove mettere alcuni PDF, come se fosse un documento, usando la funzione link
> delle sezioni».
> **Ramo:** `intro-di-pagina`, da `main`. **Nessuna migrazione**, e non per prudenza: il contenitore c'è già.
> **Tradotta**, su richiesta esplicita del SOD.
> **Stato: fatta e verificata dal vivo** il 30 agosto 2026. **29 test nuovi** (14 sul modello, 12 sul
> deposito e sul corpus, 4 sulla zona a schermo). Release verde su tutti e due i TFM.

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

## 4-bis. «Fine modifica» salva — e il difetto che ha scoperto

Chiesto dal committente il 30 agosto sera: «voglio che sia segnalato che ho modificato senza salvare, o,
ancora meglio, che Fine modifica salvi direttamente».

⚠️ **La seconda metà non era una comodità: era un difetto.** Il rilascio del lock faceva **rileggere da
archivio**, quindi chi scriveva una sezione e premeva «Fine modifica» — che è la strada naturale per «ho
finito» — perdeva tutto **in silenzio**. Non dava nessun errore: dava una pagina che tornava com'era.

E la prima metà è vera comunque: **un tasto «Salva» spento non è un avviso**. Dice che non c'è niente da
salvare, cioè l'opposto di quel che succede.

Quindi tutt'e due:

1. finché c'è da salvare, la testata porta la pastiglia **«Modifiche non salvate»** (`st-msg warn`, la stessa
   del resto del prodotto);
2. **«Fine modifica» salva** prima di lasciare il lock, e la pastiglia **«Salvato» sopravvive all'uscita** —
   un salvataggio che non si vede è indistinguibile da uno che non è avvenuto.

L'aggancio è un parametro nuovo e **additivo** su `EditLockBar`: `BeforeRelease`. Chi non lo passa non cambia
di una virgola.

- ⚠️ **Solo sul tasto**, non su ogni strada che toglie il lock. Scadenza e sblocco forzato di un admin
  significano «questo lock non è più tuo»: salvare lì vorrebbe dire **scrivere sopra al lavoro di chi ce
  l'ha adesso**. Premere il tasto è l'unico caso in cui l'intenzione è dichiarata.
- ⚠️ **Se il salvataggio fallisce, il lock non si rilascia.** Lasciarlo andare è il modo più rapido di
  perdere il lavoro appena scritto: chi ha scritto non avrebbe più il permesso di riprovare.
- ⚠️ Non si è scelta la strada «chiedi cosa fare» (salva / esci senza salvare): qui **non c'è una bozza**, e
  l'alternativa al salvataggio è esattamente la perdita silenziosa di prima. Chi si pente ha ancora la
  sezione a schermo e il cestino accanto.

## 5. Ingressi (pre-flight §3)

- **Raggiungere**: la zona sta in cima a `/services/vsop/mil`, sopra la testata. Se non c'è contenuto, per il
  pubblico **non si rende niente** — non un contenitore vuoto.
- **Creare**: niente catch-22 possibile, perché non c'è nessuna lista da cui l'intro debba comparire. Allo
  **staff** (`IsEditor`) la zona si mostra sempre, anche vuota, con «Aggiungi sezione».
- **Lock**: `EditResourceLock` con chiave `page-intro:mil`, come `structure` e `newdoc` — due editori sulla
  stessa intro sono l'ultimo che salva che vince, e qui vincerebbe in silenzio.

## 6. Verifica

Guidando il flusso vero, non solo i test: creare una sezione, metterci un blocco allegato che punta a un PDF
della biblioteca, salvare, ricaricare, e rileggere la pagina in inglese.

**Fatto il 30 agosto 2026** su una copia del `vipi.db` reale, con Edge:

| passo | esito |
|---|---|
| staff, intro vuota | la zona si vede solo a lui, con «Sola lettura · Inizia modifica» |
| lock preso | compaiono «Aggiungi sezione» e «Salva» |
| sezione + paragrafo + **blocco allegato** | la tendina offre la biblioteca vera (`MIL abbriviation`) |
| salvato e ricaricato | «Documenti generali» si legge, e il link è **la nostra rotta**: `/vsop/files/mil-abbriviation` |
| riletto in inglese | «General documents», «Read before…» — tradotto |

Zero errori di pagina, zero letterali Razor, nessuna richiesta in 4xx. In archivio: **una riga** in
`SharedBlocks`, e le migrazioni applicate restano **111** — nessuna nuova.
