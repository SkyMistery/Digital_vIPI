# 14 — I quattro documenti: un motore solo 🟡

> **Stato: CHIUSO** — otto passi su otto, la domanda che P7 aveva lasciato aperta (§3i), e l'editor SID. (2026-08-27, branch `refactor/14-quattro-documenti`).
> Seguito di [11 — Uniformità dei tre documenti](11-uniformita-tre-documenti.md) e
> [13 — Audit dei tre documenti](13-audit-tre-documenti.md). Quelli guardavano **tre** famiglie;
> la vIPI d'aeroporto è entrata nel catalogo delle sezioni solo il 26 agosto
> ([carta](../feature/2026-08-26-aeroporto-a-sezioni.md)), e questo giro è il primo che le guarda
> **tutte e quattro**.
>
> Rilievo d'origine: audit di supervisione del 27 agosto 2026, su `main @ fbac773` (albero unico
> dopo la fusione di tutti i rami). Piano approvato dal committente per intero.

---

## 1. Stato rilevato

### 1a. Quello che regge già

Non è un referto di sole rotture, e la proporzione conta: **lo strato profondo rispetta la direttiva.**

- `SectionCatalog` è una fonte unica vera per tutte e quattro le famiglie, con invarianti **provate
  su tutti i profili** (`SectionCatalogTests`) — ed è per questo che quella parte non diverge.
- `IReleaseTarget` isola le sole tre cose per-tipo (risolvere la chiave, autorizzare, descrivere):
  i motori generici non hanno uno `switch`.
- I quattro editor montano `ReleasePanel` con **gli stessi parametri** e la stessa ancora
  `sec-versioni`; bozza, lock e `DocReviewBar` ci sono su tutti e quattro.
- Il trascinamento delle sezioni c'è su **tutte e quattro** (`DocumentSectionsEditor` accende
  `OnReorder` per chiunque monti il suo indice).

### 1b. Dove è rimasta la divergenza

Si è ritirata **verso l'alto** (il guscio delle pagine) e **verso il basso** (le porte d'ingresso).

| Asse | vIPI ACC | vIPI APP | vLOA | vIPI aeroporto |
|---|---|---|---|---|
| Chi rende il corpo | catalogo | catalogo | **catena di `if`** | catalogo |
| Guardia release↔documento | nel service | in pagina | in pagina (copia) | in pagina (copia) |
| Creazione del documento | `EnsureAsync` | `EnsureAsync` | `CreateDocument`+seeder | **nel repository EF** |
| Guscio dell'editor | copia | copia | copia | copia |
| Ciclo sezioni nel viewer | `AccSectionBody` | inline | inline | inline |
| Componenti di sezione | uno, flag `Editing` | uno, flag `Editing` | — | **lettura e scrittura separate** |
| Resa pubblica | SSR statico | SSR statico | SSR statico | **circuito per lettore** |
| Chiavi di traduzione | 43 | 18 | 36 | **384, tre prefissi** |

### 1c. Le cifre (contate con script, non stimate)

- **16** membri privati con lo stesso nome nei quattro editor (+11 in tre su quattro).
- `GuardCore` **83–93%** di righe identiche su tutte le coppie; `FinishEditing` 100% su tre.
- **3** copie byte per byte di `TryPreviewAsync`.
- **5** enum paralleli per quattro famiglie.
- **117** URL di documento scritte a mano, con `IDocRoutesRegistry` già in casa.
- **0,5 MB** di snapshot letti e deserializzati a ogni apertura della vIPI ACC pubblica *(misurato)*.
- **2180** righe l'editor aeroporto, contro 714 · 529 · 487.

---

## 2. Problemi

`⛔` difetto visibile a un lettore · `⚠️` incoerenza o fragilità · `🔸` debito strutturale.

### ⛔1 — Sulla vLOA il ciclo AIRAC è scritto due volte, e i due numeri non coincidono

`VloaSections.BlocksFor("validity")` pianta una tabella *Effective from — AIRAC ####* col ciclo **del
giorno della creazione**, che non si aggiorna mai; sopra, `ValidityStamp` mostra il ciclo **della
release che si sta guardando**. Il catalogo dichiara `validity` come `HostAndBlocks`, quindi il viewer
rende tutti e due.

**Misurato sul `vipi.db` di sviluppo:** le quattro vLOA in archivio portano `AIRAC 2607`; la vLOA
`LIBB ↔ LDZO` (doc 10) ha una release in vigore al ciclo **2608**. La sua pagina dice 2608 sopra e
2607 sotto.

### ⚠️2 — La guardia «questa release è di questo documento» è in quattro posti, e non in quello giusto

`ReleaseService.GetPreviewAsync` ha già tipo e chiave della release in mano e non li confronta con
niente: autorizza soltanto. Il confronto vive in **tre copie byte per byte** nelle pagine (APP, vLOA,
aeroporto) e in una **quarta forma** dentro `AccDocumentService.LoadForReleaseAsync`. Una pagina nuova
che se ne dimenticasse mostrerebbe, sotto l'URL di un documento, il contenuto di un altro.

### ⚠️3 — Lo snapshot di release si rilegge da capo una volta per sezione

`FrozenSectionReader` chiama `LoadEffectivePayloadAsync` a **ogni** `GetFrozen*Async`: una query che
riporta l'intero `PayloadJson` e una deserializzazione completa, per sezione. Nessuno dei quattro
servizi di derivazione carica lo snapshot una volta sola.

**Misurato:** vIPI ACC di LIBB = 2 blocchi × 4 sezioni = **8** letture da 62 KB → **0,5 MB** per
render. vLOA più grande **221 KB** × 3 sezioni → **0,6 MB**.

### ⚠️4 — Il viewer della vLOA è l'unico che non chiede al catalogo chi rende il corpo

Catena di `else if (_derive && s.SectionKey == "aor" | "frequencies" | "coordination")`. Era uno dei
sei punti censiti dal doc 13 §1b: sugli altri cinque il rimedio ha tenuto, qui no. Aggiungere una
sezione derivata al profilo vLOA la farebbe comparire nell'editor e **non** nel pubblicato.

### ⚠️5 — Due liste `LiveKeys` scritte a mano e divergenti

`AccDocumentService` 5 chiavi, `AppDocumentService` 8. La domanda è quella a cui risponde
`SectionCatalog.IsHostRendered`, per profilo, con i test di invariante addosso.

### ⚠️6 — L'aeroporto è l'unico documento pubblico che apre un circuito per ogni lettore

`AeroportoPage` è `InteractiveServer` per un solo comando del documento (il selettore di pista delle
SID) più due chip che appartengono all'elenco. Il progetto ha già il modello dell'isola interattiva
(`LiveBadge`), unico esemplare.

### ⚠️7 — `ToggleAllSections`: lo stesso gesto, due guardie diverse

Due editor con `if (_jsReady)`, due con `catch (Exception) { }` — che ingoia in silenzio qualunque
guasto, non solo il caso previsto. Viola l'invariante #7 del runbook.

### 🔸8 — I quattro editor sono quattro copie dello stesso guscio

`LoadAsync · StartEditing · FinishEditing · Guard · GuardCore · ToggleAllSections ·
ReleasePreviewUrl · IsRenderModeToggleable · _doc · _docId · _editing · _error · _lock · _save ·
_dismissSaved · _wide` — sedici nomi uguali, corpi 83–100% identici.

### 🔸9 — Quattro porte per «assicurami il documento», e una nello strato sbagliato

`AccDocumentService.EnsureAsync`, `AppDocumentService.EnsureAsync`,
`AirportEditingService.EnsureDocumentAsync` (che delega dritto al repository EF), e la vLOA che non ne
ha. `EfAirportRepository.EnsureDocumentAsync` ricostruisce la nascita di documento+versione+sezioni
che `EfEditingRepository.EnsureVipiDocumentAsync` fa già: due implementazioni della stessa nascita,
che nessun test confronta.

### 🔸10 — Cinque enum per quattro famiglie

`ReleaseTargetType` e `ManagedDocKind` hanno gli stessi quattro valori con nomi diversi
(`App`/`AppVipi`, `Airport`/`AirportVipi`). `SectionProfile` ha una ragione vera per essere a parte
(l'ACC ha due profili in un documento solo); `DocumentType` è il discriminatore persistito.

### 🔸11 — Le sezioni dell'aeroporto si leggono con un componente e si scrivono con un altro

Cinque componenti di sola lettura + cinque frammenti `RenderFragment` **inline nella pagina** (523
righe). Il modello del progetto è «un componente, due modi» (`AppSeparations`, `AppConfigurations`,
`AppFrequencies`, `AppVfr` col parametro `Editing`).

### 🔸12 — Il registro delle rotte c'è e le pagine non lo usano

**117** URL scritte a stringa; il registro non conosce l'anteprima bozza `?as=draft`, che è la forma
che le pagine usano di più.

### 🔸13 — Commenti che dicono il falso

`IContentRepository`: due `<summary>` orfani di metodi rimossi, il primo appoggiato al metodo
successivo. `DocumentSectionsEditor`: «Null = documento senza catalogo (l'aeroporto)» mentre
l'aeroporto passa `SectionProfile.Airport` dal 26 agosto.

---

## 3. Architettura target 🟢

### 3a. La guardia della release è del servizio (⚠️2)

`GetPreviewAsync` **pretende** il bersaglio atteso:

```csharp
Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType,
                                      string expectedKey, CancellationToken ct = default);
```

Ritorna `null` quando la release non è di quel documento. Non è un parametro opzionale: la firma non
si può soddisfare senza dire di che documento si sta parlando, quindi la guardia non si può
dimenticare. Le tre `TryPreviewAsync` di pagina spariscono.

### 3b. La vLOA non pianta più il ciclo AIRAC a mano (⛔1)

Dal contenuto iniziale esce la riga *Effective from*: la dice la scheda. Restano le due cose che
nessuno può derivare — ciclo di revisione concordato e firmatario. I documenti già in archivio si
correggono con un **passo d'avvio idempotente**, non a mano: la riga si toglie solo se il testo è
esattamente quello seminato (`Effective from` + `AIRAC ` + ciclo), così un testo modificato
dall'editore non viene toccato.

### 3c. Lo snapshot si legge una volta per pagina (⚠️3)

`IFrozenSectionReader` guadagna `LoadAsync(type, key)` che ritorna un **lotto** già deserializzato
(`FrozenSections`), con `Get<T>(sectionId)` e `Get<T>(sectionKey)` in memoria. I quattro servizi di
derivazione lo chiamano una volta e interrogano il lotto. I metodi singoli restano per i chiamanti
occasionali, implementati sopra il lotto.

### 3d. Il guscio dell'editor è uno (🔸8, ⚠️7)

`DocumentEditorHost`: un componente non visuale (`ComponentBase` senza markup) che possiede
`_doc/_editing/_error/_lock/_save`, `StartEditing`, `FinishEditing`, `Guard`, `ToggleAllSections`,
`ReleasePreviewUrl`. I quattro editor lo tengono come campo e gli delegano. Resta a loro solo ciò che
è per-tipo: quale documento caricare e quali sezioni derivate disegnare.

⚠️ **Non** un componente-contenitore con `ChildContent`: i quattro editor hanno layout diversi
(l'ACC costruisce la propria griglia, gli altri montano `DocumentSectionsEditor`), e infilarli in un
guscio visuale comune sarebbe un secondo refactor travestito da primo.

### 3e. Il ciclo delle sezioni del viewer è uno (⚠️4)

`DocumentSectionsView`: itera le sezioni, applica le regole comuni — nascosta fuori dal pubblico e
marcata in bozza, nasce chiusa se lo dice il catalogo, sotto-sezioni prima/dopo, blocchi propri se la
sezione li tiene — e cede il posto a un `RenderFragment<SectionView>` per il solo corpo derivato.
Prende il `SectionProfile` come parametro: è così che la vLOA entra nel catalogo senza una riga
dedicata.

### 3f. Le sezioni alla nascita le dice il catalogo (⚠️5, e metà di 🔸9)

`EnsureVipiDocumentAsync` e `EnsureVipiDocumentTreeAsync` non ricevono più **due** elenchi scritti a mano —
le sezioni e le «chiavi live» — ma un `SectionProfile`. Da lì il repository prende le sezioni nel loro
ordine, decide chi riceve il blocco placeholder (`IsHostRendered`) e con che `RenderMode` nasce
(`IsAlwaysLive`). Gli array `LiveKeys` perdono il chiamante e spariscono: erano cinque chiavi sull'ACC e
otto sull'APP, per la stessa domanda. `VipiBlockSpec` porta il profilo del blocco invece della sua lista.

⚠️ **Quel che era scritto qui e NON si fa: `IReleaseTarget.EnsureDocumentIdAsync`.** L'idea era un gemello
di `ResolveDocumentIdAsync` che creasse il documento se non c'è. **Crea un ciclo di dipendenze** e non
sarebbe partito nemmeno in DI: un descrittore che dipendesse dal servizio della sua famiglia
(`AccVipiReleaseTarget` → `IAccDocumentService`) chiuderebbe l'anello passando per
`IReleaseRepository` → `IReleaseTargetRegistry` → `IEnumerable<IReleaseTarget>`. Aggirarlo con una
risoluzione pigra sarebbe stato nascondere il ciclo, non toglierlo — e soprattutto **nessun chiamante
di oggi ne ha bisogno**: ogni editor chiede il documento al servizio della propria famiglia, che lo
garantisce da sé. La porta unica sarebbe stata speculativa.

Resta vero il rilievo 🔸9 nella sua parte sostanziale — la nascita del documento d'aeroporto sta dentro
`EfAirportRepository` invece che dove nascono gli altri tre — e si chiude in **P7**, dove sta il resto del
lavoro sull'aeroporto.

### 3g. L'aeroporto rientra nel modello (🔸11, ⚠️6, 🔸9) — ✅ **FATTO**

⚠️ **Due assunzioni di questa carta sono cadute eseguendo, e vale la pena leggerle prima del resto.**

**1. «I cinque componenti prendono il flag `Editing`.»** Falso, e non per tre su cinque come avevo poi
corretto: per **nessuno**. Lettura e scrittura di queste sezioni hanno forme di dati genuinamente diverse —
la lettura è una proiezione già formattata (la fascia QNH è la stringa «1014 – 1030») perché dev'essere
serializzabile per il congelamento della release; la scrittura ha i campi separati e mutabili perché è ciò
che un `<input>` sa legare. Il modello «un componente, due modi» di `AppSeparations` funziona **perché là
lettura e scrittura sono la stessa riga**.

Quindi non applicarlo qui non era il difetto. Il difetto era un altro: **523 righe di editor scritte dentro
la pagina**, che nessun test poteva montare. Sono uscite come componenti **accanto** a quelli di lettura.

**2. «Separare la rotta e togliere il circuito sono la stessa mossa.»** Falso. Il render mode si dichiara sul
**componente**: bastava che le due parti interattive diventassero **isole**, come `LiveBadge`. Niente rotta
nuova, niente redirezioni, e gli indirizzi pubblici — che stanno nei preferiti di chi controlla e nei
messaggi su Discord — restano quelli.

**Che cosa è stato fatto**

| | |
|---|---|
| Modelli di scrittura | fuori dalla pagina, pubblici (`AirportEditModels.cs`) |
| `AirportTransitionEditor`, `AirportRunwaysEditor`, `AirportFrequenciesEditor` | componenti |
| `AirportRunwayRulesEditor` | componente, col suo banco di prova |
| Cuori deterministici | `AirportTlValidation`, `AirportRunwayValidation`, `AirportFrequencyPicker`, `AirportRuleValidation`, `AirportRuleMapping`, `AirportSidRules` |
| `AirportListPanel`, `AirportSids` | **isole** interattive |
| `AeroportoPage` | **SSR statica**, come gli altri tre |
| `DocumentBirth` | la nascita del documento è una sola |

**Editor 2180 → 1001 righe. Viewer 594 → 464. Suite 5568 → 5714.**

⚠️ **Il difetto trovato estraendo, e vale più del riordino**: la conversione di una regola pista verso il
dominio stava scritta **due volte**, campo per campo, a quattrocento righe di distanza — una per il banco di
prova, una per il salvataggio. Due copie che potevano divergere in silenzio: il banco avrebbe detto «con
vento da 200° vince la #2» e il documento pubblicato ne avrebbe applicata un'altra. Su una regola che dice
quale pista è in uso è il difetto peggiore possibile.

⚠️ **Sette frasi in italiano cablato** (quattro nel banco di prova, tre negli avvisi SID) hanno finalmente le
chiavi, in tutt'e due le lingue. Trasportare un difetto mentre si sposta il codice sarebbe stato il modo più
facile di perderlo.

✅ **Anche l'editor SID è uscito** (era l'ultimo pezzo dichiarato «resta»): 252 righe di marcatura e sessanta
membri in `AirportSidsEditor`. **Editor 2180 → 1001 righe.**

⚠️ **Il difetto trovato spostandolo.** Il blocco delle SID manuali marcava la sezione `"SID"` — con la S
maiuscola — mentre la chiave vera è `"sids"`: una stringa che non corrisponde a nessun caso dello smistamento
del salvataggio, quindi **«Salva tutto» la saltava in silenzio** (nemmeno fra le saltate: la `switch` cade su
`null` e fa `continue` prima del conteggio) e la sezione restava per sempre fra le non salvate.

Il confine, misurato: dei trentatré metodi, **ventotto erano puri o di vista** e sono entrati. Restano alla
pagina le sole cose che toccano il database, chieste con quattro callback.

⚠️ Due conseguenze del cambio di proprietà dello stato, che sono la parte da non riscoprire:
- la **selezione** e le righe non salvate le azzerava `LoadAsync` della pagina, che quello stato lo
  possedeva. Ora lo possiede il componente e se ne accorge da sé guardando se i buffer sono gli stessi:
  senza, la selezione parlerebbe di righe che non esistono più;
- il «Re-import da IVAO» della pagina chiede conferma se ci sono righe importate non salvate. Il conteggio è
  del componente, che lo **comunica**: una proiezione in sola lettura, non una seconda proprietà dello stesso
  stato.
- ✅ La domanda che questo passo aveva lasciato aperta — il significato di `CurrentVersionId` — è **chiusa
  in §3i**.

### 3h. Pulizia (🔸10, 🔸12, 🔸13)

`ManagedDocKind` sparisce a favore di `ReleaseTargetType`. Il registro rotte impara `?as=draft` e le
pagine gli chiedono le URL. I commenti che mentono se ne vanno.

---

### 3i. «Versione pubblicata corrente» vuol dire una cosa sola ✅

⚠️ **Il quadro era diverso da come questa carta l'aveva scritto.** Non era «l'aeroporto contro le altre
tre»: anche la **vLOA generata da «ACC confinanti»** puntava `CurrentVersionId` alla bozza appena creata.
**Due porte su quattro.**

E il campo **non ha due significati da conciliare**. `PublishAsync` lo scrive sulla versione pubblicata,
l'eliminazione lo azzera: vuol dire *«la pubblicata»*. Le due nascite che lo puntavano a una bozza
scrivevano una cosa falsa — un documento mai pubblicato che dichiarava di avere una versione pubblicata.

⚠️ **Il motivo per cui sembrava una decisione, e non lo era.** L'unico lettore che dava al campo l'altro
significato — «la versione su cui lavorare», in `CurrentSidsSectionAsync` — serviva a un **congelamento SID
dedicato** che dal 26 agosto 2026 fa il toggle dell'editor condiviso. Aveva come soli chiamanti **quattro
righe di test**: era codice morto sopravvissuto alla migrazione al catalogo. Tolto quello, la seconda scuola
di pensiero non esisteva più.

**Che cosa si è fatto**

1. le due porte smettono di impostarlo: nasce `null` in tutte e quattro;
2. via il congelamento SID dedicato (contratto, servizio, repository) — il test che lo copriva **non** è
   stato cancellato ma riscritto sulla superficie che resta, perché provava due cose che valgono: le SID
   nascono Live, e una seconda `Ensure` non rigenera niente;
3. una **riconciliazione d'avvio** idempotente azzera il puntatore dove indica una versione non pubblicata.
   ⚠️ Guarda lo stato della **versione**, non del documento: un documento archiviato che ha davvero
   pubblicato qualcosa tiene il suo puntatore.

⚠️ **Perché valeva la pena, visto che non si vedeva.** Ogni lettore pubblico ha un secondo cancello più
forte (release effettiva, stato `Published`), ed è per questo che nessuno se n'era accorto. Il rischio è il
**prossimo** lettore: chi si fida del nome del campo e non mette il secondo cancello si porta a casa una
bozza.

**`NascitaDocumentoParitaTests`** pone la stessa domanda a tutte e **quattro** le porte di nascita — «un
documento appena nato non ha una versione pubblicata» — più la metà che manca: che dopo `PublishAsync` il
campo ci sia e sia quello giusto. Senza quella, la prima si soddisferebbe non scrivendolo mai.

**Prova sui dati veri** (copia del `vipi.db`, e poi attraverso l'avvio vero dell'applicazione): **1**
puntatore azzerato — *vIPI LIBA Amendola*, mai pubblicata — i 12 corretti intatti, seconda passata **0**, e
le **28 versioni** tutte al loro posto.

## 4. Passi di migrazione

| # | Passo | Chiude | Rischio | Stato |
|---|---|---|---|---|
| P1 | La guardia della release sale nel servizio | ⚠️2 | basso | ✅ |
| P2 | La vLOA smette di piantare il ciclo AIRAC | ⛔1 | basso | ✅ |
| P3 | Lo snapshot si legge una volta per pagina | ⚠️3 | basso | ✅ |
| P4 | `DocumentEditorHost`: il guscio dell'editor | 🔸8 ⚠️7 | medio | ✅ |
| P5 | `DocumentSectionsView`: il ciclo del viewer | ⚠️4 | medio | ✅ |
| P6 | Le sezioni alla nascita le dice il catalogo | ⚠️5 | medio | ✅ |
| P7 | L'aeroporto rientra nel modello | 🔸11 ⚠️6 🔸9 | alto | ✅ |
| P8 | Pulizia: enum, rotte, commenti | 🔸10 🔸12 🔸13 | medio | ✅ |
| P9 | «Versione pubblicata» ha un significato solo (§3i) | — | basso | ✅ |

Ogni passo è un commit, con build verde. P4 e P5 portano con sé le **prove di parità** (§5).

---

## 5. Impatto / Verifica

- **Baseline:** 5432 casi verdi sui due TFM (2835 su net8), build 0 avvisi, `main @ fbac773`.
- **Prove di parità** — la cosa che vale più degli otto passi. Una classe sola che pone alle quattro
  famiglie le stesse domande di comportamento:
  1. una sezione nascosta non compare nel pubblico, e compare marcata in bozza;
  2. l'anteprima di una release che appartiene a un altro documento viene **rifiutata**;
  3. il documento nasce con le sezioni del catalogo del suo profilo, nello stesso stato;
  4. la sezione «validity» ha scheda e blocchi in tutte e quattro.
- **Verifica sui dati veri:** copia del `vipi.db` di sviluppo (mai l'originale), con i conteggi prima
  e dopo su `DocReleases`, `DocumentSections`, `ContentBlocks`.
- **Misura di P3:** le letture su `DocReleases` per render della vIPI ACC di LIBB passano da 8 a 1.
- **Invariante #7 del runbook:** nessun `catch { }` silenzioso introdotto; quello esistente in
  `ToggleAllSections` è stato **tolto**, non spostato.

---

## 6. Esito

**Suite: 5432 → 5714 casi verdi** sui due TFM, build `0 avvisi`. Sette passi su otto eseguiti, un commit
per passo.

### Verifica sul flusso reale (Fase 3 del runbook)

⚠️ **Fatta due volte**: dopo i primi sette passi, e di nuovo dopo P7.

**Dopo P7**, sull'applicazione vera con una copia del `vipi.db`:
- i quattro documenti pubblici rispondono 200 e la pagina d'aeroporto **non è più un circuito**: le isole
  dichiarate nel markup sono due (la sua e `LiveBadge` della barra), contro l'unica delle altre tre — cioè
  la pagina in sé non lo è più;
- il documento di LIBR rende tutte le sue sezioni e la tabella SID;
- **i cuori estratti girati sull'archivio vero**: 3 regole piste su 2 scali (nessun problema), i livelli di
  transizione di **93 scali** (nessun problema), e l'invariante delle chip verificata su **367 SID reali** di
  tre aeroporti — filtrando per una pista le chip restano tutte, che è la regola che rende usabile il
  pannello.


L'applicazione **vera**, pubblicata in Release e avviata su una **copia** del `vipi.db` di sviluppo
(l'originale non è stato toccato):

- all'avvio il log dice *«Tolte 8 righe "Effective from — AIRAC" scritte a mano nelle vLOA»* — P2 attraverso
  il percorso d'avvio vero, non un test;
- i **quattro** documenti pubblici rispondono 200 con contenuto vero: `/libb/vipi` (230 KB),
  `/libb/apps/vipi?app=LIBA_APP`, `/libb/vloa?acc=LDZO`, `/libb/airports?icao=LIBR`;
- **la guardia di P1 provata dal vivo**: la release di `LICC_APP` aperta sotto l'indirizzo di `LIBA_APP`
  mostra Amendola, senza banner d'anteprima e senza una sola traccia del documento sbagliato.

### ⚠️ Quel che solo la verifica live poteva trovare

**Il difetto ⛔1 resta a schermo sui documenti già pubblicati, e la correzione non basta da sola.** La
pagina pubblica legge lo **snapshot della release**, e la riconciliazione corregge il *documento*, non la
fotografia già scattata: sulla vLOA `LIBB ↔ LDZO` il timbro dice `2608` e la tabella dello snapshot dice
ancora `AIRAC 2607`.

**Non si riscrivono gli snapshot**, ed è una scelta: una release «congela davvero» (doc 10), e
riscriverne il payload a posteriori cambierebbe quel che un ciclo passato ha detto — un precedente peggiore
del difetto. La strada giusta è **ripubblicare**, e il progetto ha già il meccanismo che lo chiede: il giro
notturno `ImpactDriftUseCase` confronta il pubblicato col documento e apre `ReleaseDrift`, che compare in
«Da fare» col tasto **Ripubblica** e si chiude da sé alla ripubblicazione. Verificato che funziona già su
questa famiglia: i documenti vLOA 8 e 11 hanno la segnalazione aperta in archivio.

🔵 **Da dire al committente:** le quattro vLOA vanno **ripubblicate** perché la correzione arrivi al
pubblico. Sono quattro clic, e la lista «Da fare» le indicherà da sola dopo il primo giro notturno.
