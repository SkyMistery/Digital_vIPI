# 14 — I quattro documenti: un motore solo 🟡

> **Stato: IN ESECUZIONE** (2026-08-27, branch `refactor/14-quattro-documenti`).
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

### 3f. Una porta sola per «assicurami il documento» (🔸9, ⚠️5)

`IReleaseTarget` guadagna il gemello di `ResolveDocumentIdAsync`:

```csharp
Task<int> EnsureDocumentIdAsync(string key, CancellationToken ct = default);
```

I quattro descrittori la implementano delegando al proprio servizio; la nascita del documento
d'aeroporto lascia `EfAirportRepository` e passa da `EnsureVipiDocumentAsync` come le altre. Gli array
`LiveKeys` perdono il chiamante: le sezioni con corpo dalla pagina le dice il catalogo.

### 3g. L'aeroporto rientra nel modello (🔸11, ⚠️6)

Tre mosse indipendenti: i cinque componenti di sezione prendono `Editing` e i frammenti inline
spariscono; l'elenco esce dalla rotta del documento; il selettore di pista diventa un'isola
interattiva e il documento pubblico torna SSR statico.

### 3h. Pulizia (🔸10, 🔸12, 🔸13)

`ManagedDocKind` sparisce a favore di `ReleaseTargetType`. Il registro rotte impara `?as=draft` e le
pagine gli chiedono le URL. I commenti che mentono se ne vanno.

---

## 4. Passi di migrazione

| # | Passo | Chiude | Rischio |
|---|---|---|---|
| P1 | La guardia della release sale nel servizio | ⚠️2 | basso |
| P2 | La vLOA smette di piantare il ciclo AIRAC | ⛔1 | basso |
| P3 | Lo snapshot si legge una volta per pagina | ⚠️3 | basso |
| P4 | `DocumentEditorHost`: il guscio dell'editor | 🔸8 ⚠️7 | medio |
| P5 | `DocumentSectionsView`: il ciclo del viewer | ⚠️4 | medio |
| P6 | `EnsureDocumentIdAsync`: una porta sola | 🔸9 ⚠️5 | medio |
| P7 | L'aeroporto rientra nel modello | 🔸11 ⚠️6 | alto |
| P8 | Pulizia: enum, rotte, commenti | 🔸10 🔸12 🔸13 | medio |

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
  `ToggleAllSections` va **tolto**, non spostato.
