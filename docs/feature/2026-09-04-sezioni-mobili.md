# Sezioni e sotto-sezioni che si muovono davvero — 4 settembre 2026

> Richiesta del committente: *«permettere alle sezioni e alle sottosezioni di essere mosse all'interno
> delle sezioni di appartenenza o del documento senza problemi; poter nascondere le singole sottosezioni;
> permettere alle sottosezioni custom di essere portate anche sopra il contenuto principale della
> sottosezione (tipo sopra la mappa in AOR); e un meccanismo per spostare le sottosezioni in sezioni
> diverse. Su tutti e cinque gli editor.»*

Le cinque famiglie: vIPI ACC, vIPI APP, vLOA, vIPI d'aeroporto, vSOP militare.

## 0. Che cosa c'era già — misurato sul codice, non ricordato

Tre richieste su quattro **hanno già il tasto**. Vale la pena scriverlo, perché il lavoro vero è un altro
e perché due delle tre hanno un difetto che le rende **vere a metà**:

| Richiesta | Dov'è | Stato |
|---|---|---|
| ↑/↓ su sezioni **e** sotto-sezioni, dentro il gruppo | `DocumentSectionsEditor.SectionHeader` (le frecce ci sono in tutt'e due i rami, libera e di catalogo) + `EfEditingRepository.MoveSectionAsync` | c'era |
| Nascondere una **singola sotto-sezione** | l'occhio è nell'header condiviso a **ogni** profondità; `SectionNode` la toglie dal pubblico | c'era, con un buco (§2) |
| Sotto-sezione **sopra il corpo** («⤒ sopra il corpo», `BeforeParentBody`, doc 11 §3g) | il comando c'è su ogni sotto-sezione; i viewer di primo livello lo rispettano | c'era, con un buco (§1) |
| Spostare una sotto-sezione **in un'altra sezione** | — | **non c'era**, ed era vietato apposta |

⚠️ Il divieto non era una dimenticanza: `MoveSectionBeforeAsync` **pretende un fratello** perché
«riordinare non deve diventare riparentare in silenzio». Questa carta apre quella porta — **la stessa
porta**, non una accanto: la riparentazione entra nel repository che già governa le mutazioni di sezione,
sotto le stesse guardie di bozza e di lock.

## 1. 🔴 Il difetto che rende falso l'editor: `SectionNode` e il corpo reso dalla pagina

`SectionNode.Corpo()` rende `@DerivedContent(Section)` e **poi** un `SectionBody` con `Slot=All`. Cioè:
per una **sotto-sezione il cui corpo lo disegna la pagina**, le figlie marcate «sopra il corpo» escono
**sotto** la scheda. L'editor le mostra sopra.

Non è teoria: nel vSOP militare `frequenze`, `piste` e `quote di transizione` sono **figlie** di «Dati
generali» e sono rese dalla pagina. Chi ci mette una nota sopra la tabella la vede sopra mentre scrive e
sotto una volta pubblicata.

⚠️ È la stessa trappola del **doc 11 §8** — l'editor che dice una cosa e il documento pubblicato un'altra —
già pagata sulle due direzioni dei coordinamenti della vLOA. `DocumentSectionsView` (radici) e
`AccSectionBody` (blocchi ACC) i tre slot li fanno giusti: **il terzo lettore no**.

**Cura**: `SectionNode` rende Before → corpo derivato → blocchi propri (solo se `KeepsOwnBlocks`) → After,
la stessa sequenza dei due fratelli maggiori.

## 2. Il buco dell'anteprima bozza sulla vIPI ACC

`AccVipiPage` e `AccSectionBody` non passano **mai** `IsDraft`. Le radici hanno il filtro consapevole
(`_mode.Kind == PreviewKind.Draft || !s.IsHidden`), ma le **sotto-sezioni** scendono in `SectionBody` col
default `false`: una sotto-sezione nascosta **sparisce anche dall'anteprima di bozza**, invece di comparire
con la pill «nascosta».

⚠️ Il default `false` è quello giusto (chi dimentica **nasconde**, non pubblica per sbaglio): il difetto è
che nessuno lo passa. Si passa.

## 3. La riparentazione — regole decise

Chiesto al committente, tre decisioni:

1. **Si spostano solo le sezioni LIBERE.** Una sezione di catalogo ha una posizione standard — è quella che
   la pill dello scostamento conta — e portarla in un altro gruppo la renderebbe muta. Libera =
   `SectionCatalog.Find(profilo, chiave) is null`.
2. **Nella vIPI ACC si resta dentro il blocco.** Il blocco *è* il gruppo: le destinazioni sono le sezioni
   dell'albero che quell'editor sta mostrando (`RootSections`/`AddParentId` lo dicono già).
3. **Due gesti**: un menu «Sposta in…» (preciso, da tastiera e da tocco) e il **trascinamento** nel
   menu-sezioni (veloce). Il menu regge da solo se il trascinamento dà problemi.

⚠️ **La garanzia sta nel motore, non nel tasto**: le stesse domande che spengono la voce di menu le rifà il
repository. Un elenco di destinazioni sbagliato deve dare una mossa **rifiutata**, non un documento storto.

### Le guardie, tutte e cinque

| Guardia | Perché |
|---|---|
| Stessa **versione** | una sezione non cambia mai documento; fra i membri di un documento unito nemmeno |
| **Bozza** (`RequireDraftAsync`) | come ogni altra mutazione di sezione |
| Non dentro il **proprio sottoalbero** | è il ciclo: un padre figlio di sé stesso sparisce dall'albero e non torna |
| **Profondità** del sottoalbero ≤ `DocumentSection.MaxDepth` (3) | si misura il **sottoalbero**, non la sola sezione mossa: una figlia con figlie ne porta due |
| Solo sezioni **libere** | decisione 1 |

⚠️ `Depth` è una **colonna**, non un calcolo: va riscritta su **tutto** il sottoalbero, ricorsivamente.
Chi la lascia indietro ottiene sotto-sezioni che si rendono al livello sbagliato e un indice che non rientra.

⚠️ E l'`Order` è una **posizione fra fratelli**: il gruppo che perde la sezione si **richiude**, quello che
la riceve la **inserisce**. L'algoritmo esiste già collaudato in
`EfDocumentMaintenance.ReparentMilParkingsAsync` (§BC): questa carta lo generalizza e lo mette dove
appartiene, invece di ricopiarlo.

## 4. Le otto fette

| Fetta | Che cosa |
|---|---|
| **S0** | Questa carta + §BE in `lavori-aperti.md` |
| **S1** | `SectionNode` a tre slot (§1) |
| **S2** | `IsDraft` scende in ACC fino alle sotto-sezioni (§2) |
| **S3** | Motore: `MoveSectionToParentAsync` + le cinque guardie |
| **S4** | `SectionMoveTargets` (funzione pura) + menu «Sposta in…» nell'header |
| **S5** | Le figlie si trascinano nel menu-sezioni; il drop fuori gruppo riparenta |
| **S6** | Spareggio stabile nell'ordinamento letto dall'editor |
| **S7** | Propagazione e verifica: snapshot, traduzione, diff, i18n, live sulle cinque famiglie |

## 5. Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo: `DocumentSection.ParentSectionId`/`Order`/`Depth` esistono e sono
   già versionati e copiati in bozza. Nessuna colonna, **nessuna migrazione** (conta: siamo nella finestra
   cieca fino al 16 settembre).
2. **Dispatch** — nessuno `switch` nuovo. Le due domande («è libera?», «dove può andare?») stanno in **un**
   posto ciascuna: `SectionCatalog.Find` e `SectionMoveTargets`.
3. **Ingressi + verifica** — ingresso: il menu «Sposta in…» nell'header, in modifica, a ogni profondità, su
   tutti e cinque gli editor (montano lo stesso componente). Verifica: le cinque famiglie guidate a schermo
   su copia del DB, e il trascinamento provato **col browser che lo fa davvero** (⚠️ eventi fabbricati a
   mano hanno già nascosto un trascinamento rotto per un giorno: `Input.setInterceptDrags`, headful).
4. **Propagazione** — niente si rimuove né si rinomina; si toglie però un **divieto** citato in tre commenti
   (`IEditingRepository.MoveSectionBeforeAsync`, `EditorTocProjection`, `UnionMembersEditor.VociIndice`).
   Quei commenti vanno riscritti nello stesso giro: dicono «non si riparenta» e diventerebbero falsi.

## 6. Quel che questa carta NON fa

- **Non tocca le release pubblicate.** Le correzioni di §1 e §2 valgono sui documenti di lavoro e sulle
  release **ripubblicate**: uno snapshot è la fotografia di allora e non si riscrive mai.
- **Non sposta niente fra documenti**, nemmeno fra i membri di un documento unito.
- **Non sposta le sezioni di catalogo** (decisione 1). Se servirà, è un'altra carta: prima va deciso che
  cosa dice la pill dello scostamento per una sezione fissa fuori dal suo gruppo.

## 7. Diario di esecuzione

- **S0** ✅ carta scritta, ramo `sezioni-mobili` (`c3e9ee36`).
- **S1** ✅ `SectionNode` rende i tre slot attorno alla scheda della pagina.
  Tre test bUnit nuovi (`SectionNodeSlotsTests`): ordine dei tre slot, ogni figlia **una volta sola**, e la
  figlia nascosta che resta fuori dal pubblico anche stando «sopra».
  ⚠️ **Provato per mutazione**: rimesso il vecchio `SectionNode`, il test dell'ordine diventa **rosso** — gli
  altri due restavano verdi anche col difetto, e da soli non avrebbero provato niente. Suite UI 1224 verde.
- **S2** ✅ `IsDraft` scende dalla `AccVipiPage` all'`AccSectionBody` e da lì alle quattro chiamate a
  `SectionBody` (i due slot delle sezioni strutturate, il corpo di «Validità e revisione» e quello delle
  sezioni libere). ⚠️ `LivePage` monta lo stesso componente e **non** lo passa: la vista operativa non è un
  documento in bozza, e il default `false` è la risposta giusta lì.
  Tre test bUnit (`AccDraftHiddenChildTests`), provati per mutazione: tolto il passaggio del parametro — non
  il parametro — il test della bozza diventa **rosso**. Suite UI 1227 verde.
- **S3** ✅ `MoveSectionToParentAsync` su `IEditingRepository`/`IEditingService`, con le cinque guardie e la
  doppia rinumerazione. Dieci test (`SezioniRiparentateTests`) su SQLite in memoria, uno per guardia più il
  gesto intero, la profondità riscritta sul sottoalbero e il ritorno alla radice.
  Note di esecuzione:
  · **«libera» si chiede alla CHIAVE**, non al profilo (`SectionKeys.IsCustom`): il repository non conosce il
    profilo del documento, e la UI userà la stessa funzione — una porta sola.
  · **Il riferimento fuori gruppo si RIFIUTA**, non si accoda: `MoveSectionBeforeAsync` in quel caso tace, ma
    lì tacere vuol dire «non ti muovo», qui vorrebbe dire «ti metto dove non hai chiesto».
  · `RowVersion` si rigenera sulle righe toccate — su `DocumentSection` è un token di concorrenza dichiarato,
    e la riparentazione di §BC faceva già così.
  · ⚠️ Il motore **non sa** che cosa sia una radice per una famiglia: nella vIPI ACC le radici sono i blocchi.
    Che quella destinazione non venga offerta lo garantisce la UI (S4), ed è scritto sull'interfaccia.
  Build Release dell'intera soluzione verde, 0 avvisi.
- **S4** ✅ `SectionMoveTargets` (funzione pura, `Vipi.Application.Content`) + il menu «⇵ Sposta in…»
  nell'intestazione di sezione, a ogni profondità, su tutti e cinque gli editor — montano lo stesso componente.
  · L'elenco esclude sé stessa, il **proprio sottoalbero**, il **padre attuale** (là dentro ci si muove con le
    frecce) e ogni destinazione senza profondità residua per il sottoalbero che la sezione porta con sé.
  · **«Primo livello» non è la radice del documento**: è il padre delle radici mostrate — per la vIPI ACC il
    **blocco** (`AddParentId`). Portare una sezione alla radice del documento, là, vorrebbe dire farne un blocco.
  · Il clic sposta **in coda** al gruppo nuovo: dove sta è una domanda, in che ordine è un'altra, e la seconda
    ha già le frecce e il trascinamento.
  · Otto test sulla funzione pura (`SectionMoveTargetsTests`) e quattro sul menu montato
    (`MenuSpostaInTests`), che provano la catena intera fino alla chiamata al servizio.
  · Nato qui `EditingServiceStub` nei test UI: base con tutti i metodi che **sollevano**, così un componente
    che chiamasse quel che non deve fa cadere il test invece di passare in silenzio.
  · Tre chiavi i18n nuove, IT+EN (`Dse_MoveTo`, `Dse_MoveToTitle`, `Dse_MoveToTop`).
  Suite UI 1231 verde, build Release intera verde.

