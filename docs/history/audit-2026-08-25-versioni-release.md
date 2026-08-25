# Audit versioni & release — 25 agosto 2026

**Ramo:** `audit-versioni-release` (da `main` 8e5f457) · **Stato:** ✅ **eseguito lo stesso giorno**, otto commit.
Suite dopo l'esecuzione: vedi «Verifica» in fondo. Build Release a zero avvisi sui due TFM.

Revisione scrupolosa, chiesta dal committente, di **tutta la gestione delle versioni dei documenti**: i due
binari (`DocumentVersion` bozza→pubblicata→archiviata e `DocRelease` snapshot AIRAC per bersaglio),
`ReleaseService`, `EfReleaseRepository`, `EfEditingRepository`, `EfDocumentAdminRepository`, i gate di
visibilità pubblica (`PublicDocumentGate`, predicati di `EfContentRepository`, `AccDocumentService`),
`ReleasePanel`, `VersioniPage`, i viewer con `?as=draft|rel:{id}`, la retention e la pulizia immagini.

**Metodo.** Lettura del sorgente sul ramo di lavoro, poi esecuzione su ramo pulito da `main`: ogni difetto
chiuso porta un test di caratterizzazione che prima falliva (o non esisteva), e ogni slice è un commit con
build verde. Dove un'affermazione è solo dedotta e non misurata, lo dico.

**Esito in una riga.** L'impianto del doc 10 (bozza sempre viva + release che congela una fotografia,
visibilità pubblica = release effettiva) è solido, transazionale dove conta, e ben coperto di test. Il filo
comune dei difetti trovati è un altro, ed è già noto a questo progetto:

> ⚠️ **Sei difetti su otto sono regole già scritte, applicate a metà.** Il gate `IsHidden` c'era in tre tipi
> su quattro; il lock di editing lo pretendeva il publish-versione ma non il publish-release *della stessa
> pagina*; l'invariante «una release per ciclo» era imposto e poi annullato **nella stessa funzione**;
> l'indice unico sul progressivo c'era per le versioni e mancava per le release; la pulizia immagini sapeva
> *leggere* i payload di release ma nessuno la chiamava quando una release spariva; e la UI catturava
> un'eccezione di authz che il servizio non lanciava. Ognuna di queste regole aveva un punto del codice che
> le sfuggiva perché arrivato da un'altra strada.

---

## Riepilogo per gravità

| # | Difetto | Gravità | Commit |
|---|---|---|---|
| V1 | vIPI ACC **nascosta** ma servita per intero all'URL diretto | **Alta** (gate di visibilità) | `d989194` |
| V2 | «Pubblica ora» **scavalca il lock** e promuove la bozza di un altro editor a metà lavoro | **Alta** (workflow) | `eab966d` |
| V3 | Ripubblicare allo stesso ciclo futuro lascia **due «Programmata» gemelle** | Media | `d716bc9` |
| V4 | Il **diff della release in vigore** dice «nessuna release in vigore», tutte le voci «Aggiunta»; e `DiffAsync` era l'unica lettura senza authz | Media | `d30d750` |
| V5 | La **retention pota per stato**, ma gli stati invecchiano da soli: le superate-per-tempo sfuggono allo sweep di boot | Media | `caada92` |
| V6 | Le **immagini citate solo da release rimosse** (potatura, annullo, delete documento) non venivano mai liberate | Media | `e99c2a2` |
| V7 | Il progressivo di release (`max+1` in memoria) **senza indice unico**: due publish concorrenti = due «rilascio #7» silenziosi | Bassa | `985f890` |
| V8 | Tre frasi UI false: chip «Pubblici», prompt d'annullo sulle superate, autore «VID 0» | Bassa | `430998e` |

---

## V1 — Un documento nascosto è nascosto anche all'URL diretto, pure se è l'ACC

`Document.IsHidden` promette («reversibile: i loader pubblici lo escludono») e tre famiglie su quattro
mantengono: il predicato `!d.IsHidden` in `EfContentRepository.LoadVipiAsync` copre aeroporto, APP e vLOA
(col test `HiddenApp_WithEffectiveRelease_StaysHidden`), e `PublicDocumentGate` copre landing, ricerca e
«Cosa è cambiato». La vIPI ACC no: `AccDocumentService.LoadForViewAsync` non ha quel predicato — risolve
l'identità e va dritta alla release effettiva. Nascondere la vIPI ACC da `/services/vsop/versions` (il
tasto c'è, e accetta ogni `ManagedDocKind`) la toglieva da card e ricerca ma
**`/services/vsop/{acc}/vipi` continuava a servire lo snapshot completo**.

**Fix.** Il flag viaggia con l'identità (`AccDocumentIdentity.IsDocumentHidden`, letto dal `Sector.Document`
nel resolver) e la vista pubblica risponde `null` prima di aprire la release. Ne beneficiano anche la vista
live e l'AoR 3D, che passano da `LoadForViewAsync`. Sistemato anche il commento d'interfaccia rimasto vero a
metà (parlava ancora del fallback alla versione pubblicata e del guscio sintetico, rimossi col doc 10).
Test: `LoadForView_HiddenDocument_IsNotServedToPublic`.

## V2 — Il lock di editing vale anche per le release

Il publish-versione dell'editor pretende il lock (`EditingService.EnsureLockAsync`), e su `VersioniPage` il
tasto «Pubblica» di una versione lo acquisisce prima. Ma `PublishNowAsync`/`PublishAsync` delle **release**
controllavano solo l'authz ACC — un'asimmetria *nella stessa pagina*. Scenario concreto: l'editor A scrive
col lock; l'editor B (stesso ACC) preme «Pubblica ora» → lo snapshot congela la bozza di A **a metà**
(`WorkingVersionIdAsync` fotografa la bozza più recente), la promozione gliela trasforma in `Published`
sotto le dita, e i suoi salvataggi successivi vengono rifiutati da `RequireDraftAsync`. Nessun errore da
nessuna parte.

**Fix.** Entrambe le pubblicazioni rifiutano se il lock è di un altro (guard *inverso*: il pannello release
non ha il ciclo di vita del lock, quindi non lo pretende per sé — pretende che non sia altrui), con una
`ValidationException` che dice chi e fino a quando, catturata sia da `ReleasePanel` sia da `VersioniPage`.
«Pubblica ora» a cose fatte **rilascia** l'eventuale lock del chiamante, come fa il publish-versione. Il
guard sta fuori dalla transazione, come l'authz: rifiutare non è una scrittura. Test:
`Publish_Rifiutato_Se_Il_Documento_E_In_Modifica_Da_Un_Altro`.

## V3 — «Una release per ciclo» vale anche per i cicli futuri

`SaveReleaseAsync` marcava `Superseded` le release non-superate dello stesso ciclo… e tre righe dopo
`RecomputeStatuses` rimetteva `Scheduled` a ogni riga con data futura. Ripubblicare allo stesso ciclo
schedulato lasciava **due «🕒 Programmata» gemelle** in timeline, indistinguibili se non per «rel. vN»,
finché una pubblicazione successiva non ricalcolava. Il contenuto pubblico restava giusto solo grazie al
tiebreak `VersionNumber` di `GetEffectiveAsync`.

**Fix.** La regola vive in un posto solo: `RecomputeStatuses` elegge per ogni ciclo la release più recente
(`VersionNumber` più alto) e supera le altre; fra le vincitrici sceglie l'effettiva per data. La marcatura
esplicita — mezza morta e mezza smentita — è rimossa. Nota: l'annullo della vincitrice fa **risorgere** la
gemella superata dello stesso ciclo (il ricalcolo di `CancelAsync` la rielegge), che è il comportamento
sensato. Test: `Republish_SameFutureCycle_SupersedesTheOlderScheduled`.

## V4 — Il diff confronta con la release precedente, e lo dice

La baseline del diff era «la release in vigore ORA, esclusa quella in esame». Proprio per la release **in
vigore** — il diff più richiesto — diventava `null`, e la UI mostrava «Confronto con: *stato attuale
(nessuna release in vigore)*» con **tutte** le sezioni «Aggiunta», anche alla decima pubblicazione.

**Fix.** La baseline è la release immediatamente **precedente** nella storia del bersaglio (data efficace,
poi progressivo): il diff risponde «cosa ha cambiato questa pubblicazione» — per l'effettiva, per le
schedulate (ciò che il pubblico vedrà appena prima), per le superate (storia). Il ripiego a schermo dice
«prima pubblicazione (nessuna release precedente)», che ora è vero. `DiffAsync` guadagna anche l'**authz
ACC** delle altre letture di release (`GetPreviewAsync`/`GetLocationAsync` l'avevano): la prova che mancasse
per svista e non per scelta è che ReleasePanel e VersioniPage catturavano già `EditNotAllowedException`
attorno a questa chiamata — una cattura che non poteva scattare. Test nel flusso generico (prima release
senza baseline; seconda identica → baseline sì, zero righe).

## V5 — La potatura ricalcola gli stati prima di guardare

Gli stati in DB si ricalcolano solo a salvataggio/annullo. Fra un salvataggio e l'altro **invecchiano da
soli**: quando una schedulata entra in vigore col passare del tempo, la vecchia riga resta marcata
`Effective`. La visualizzazione non sbaglia (usa `IsEffectiveNow`, calcolato al momento), ma la retention
potava **per stato** → lo sweep di boot mancava a ogni giro le righe superate-per-tempo dall'ultima
pubblicazione. **Fix:** `PruneReleasesAsync` ricalcola con `RecomputeStatuses` prima di potare — retention
vera a ogni giro, e stati riallineati al fatto come effetto collaterale. Test:
`Prune_Recomputes_Stale_Statuses_Before_Pruning`.

## V6 — Anche le release liberano le foto

La pulizia immagini sapeva **leggere** i payload delle release (quarta sorgente di `ReferencedShasAsync`,
con tanto di test sullo snapshot vero) ma nessuno la **chiamava** quando una release spariva: potatura
retention, annullo, e la cancellazione documento — che porta via versioni e blocchi via cascade EF, senza
la scansione sha di `EliminaVersioneAsync` — lasciavano nel deposito per sempre le foto citate solo lì. Si
scoprivano soltanto dall'analisi manuale in admin.

**Fix.** `PruneReleasesAsync` e `CancelAsync` raccolgono gli sha dai payload **prima** di cancellare;
`DeleteAsync` (documento) li raccoglie da payload di release e blocchi immagine di tutte le versioni. In
coda decide sempre `DeleteOrphansAsync`, che ricontrolla ogni sorgente: una foto citata anche da una bozza
o da un'altra release resta dov'è — stesso anello prudente di `EfEditingRepository`. Test:
`Prune_And_Cancel_Free_Images_Cited_Only_By_Removed_Releases`, `DeleteDocument_Frees_Images_Cited_Only_By_It`
(quest'ultimo è anche il **primo test in assoluto** su `DeleteAsync` col cascade completo).

## V7 — L'indice unico che le versioni avevano già

`SaveReleaseAsync` assegna il progressivo con `max+1` letto in memoria. `DocumentVersions` ha l'indice unico
`(DocumentId, VersionNumber)` dal primo giorno; `DocReleases` aveva lo **stesso indice ma non-unico**: due
pubblicazioni concorrenti sullo stesso bersaglio avrebbero prodotto due «rilascio #7» in silenzio. Ora
`(TargetType, TargetKey, VersionNumber)` è unico: il caso raro diventa un conflitto rumoroso da ritentare.

Migrazione `UniqueReleaseNumberPerTarget` emessa **due volte** (SQLite-flavored + MySql) come da runbook, e
**letta**: drop+create dello stesso indice, identica nei due provider, nessun cambio spurio.
⚠️ **Al deploy**: se il DB di produzione avesse già duplicati da un race passato, la migrazione si ferma lì.
Prima di applicarla vale la SELECT di controllo:
```sql
SELECT TargetType, TargetKey, VersionNumber, COUNT(*) FROM DocReleases
GROUP BY TargetType, TargetKey, VersionNumber HAVING COUNT(*) > 1;
```
⚠️ L'ambiente di prova su Postgres (Render/Neon) non applica migrazioni: il `PostgresSchemaReconciler` cura
il drift di **colonne**, non di indici — lì l'indice unico non nasce, ed è accettato (è un hardening).

## V8 — Tre frasi che smettono di mentire

1. **Chip «Pubblici»** su `/services/vsop/versions`: contava `!IsHidden && !HasDraft`, che non è la
   visibilità pubblica (quella la decide la release in vigore, e il chip «In vigore» c'è già, un fatto in un
   posto). Ora si chiama **«Pubblicati»** e il `title` dice dove sta la visibilità vera.
2. **Annullo di una release superata**: la domanda diceva «il pubblico tornerà alla precedente», che per una
   superata è falso — non cambia nulla a schermo, si cancella storia. Due domande per due atti
   (`Rel_CancelPromptOld`/`Rel_CancelTitleOld`), stessa coppia in `ReleasePanel` e `VersioniPage`;
   `Rel_CancelConfirm`, rimasto senza lettori, è rimosso.
3. **Autore delle release di backfill** (`CreatedByUserId=0`, boot): la riga diceva «VID 0», che non è
   nessuno. Ora dice **«sistema»** (`Common_SystemAuthor`, IT+EN), in `ReleasePanel.Chi` e
   `VersioniPage.Author`.

---

## Osservazioni SENZA esecuzione (da sapere, non da «aggiustare» oggi)

- **O1 — La chiave di release ACC è legata al catalogo.** `TargetKey = "{acc}|{rootCallsign}"`: se un
  import cambiasse il callsign del CTR radice primario, le release esistenti resterebbero sotto la vecchia
  chiave — la vIPI ACC sparirebbe dal pubblico finché non si ripubblica, e le release orfane non verrebbero
  **mai** potate (`PruneAllAsync` passa solo dai bersagli attuali). Analogo per un ricollegamento di
  `Airport.DocumentId` a un documento nuovo: la chiave è l'ICAO, quindi il documento nuovo **eredita in
  pubblico lo snapshot del vecchio** finché non ripubblica. È coerente con la regola già scritta nelle
  memorie («le release già scritte vanno RIPUBBLICATE» dopo operazioni strutturali), ma nessuna UI lo
  segnala: se capita di nuovo, il primo posto dove guardare è questo.
- **O2 — La release schedulata fotografa la BOZZA al momento della schedulazione** (per disegno, doc 10:
  lo stato live È la bozza sempre aperta). Il lock di V2 impedisce di fotografare la bozza *di un altro*;
  schedulare la **propria** bozza incompleta resta possibile e legittimo — la fotografia è di quel momento,
  non del giorno del ciclo. Anche l'output Frozen delle sezioni derivate è di quel momento.
- **O3 — Payload corrotto in anteprima**: `GetPreviewAsync` con JSON illeggibile ritorna `Doc=null` e i
  viewer ripiegano in silenzio sulla vista pubblica (`TryPreviewAsync`). Un payload rotto oggi non si vede
  da nessuna parte; se mai servisse, il posto per dirlo è il banner di anteprima.
- **O4 — Un documento in sola bozza con release schedulata diventa pubblico al ciclo** senza mai essere
  stato «pubblicato» come versione (la visibilità è la release, doc 10 §S6b — deliberato). Il documento
  resta `Draft` nelle liste admin: non è un'incoerenza, ma va saputo leggendo i due stati fianco a fianco.

## Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` → **0 avvisi, 0 errori** (i due TFM; avvisi=errori da
  `Directory.Build.props`).
- Suite completa `dotnet test Vipi.slnx` dopo l'ultimo commit: **3.686 test verdi su 14 assiemi** (net8 e
  net10, E2E inclusi), zero rossi. `Vipi.Infrastructure.Tests` porta **+6 test di caratterizzazione** nuovi
  (gate nascosto ACC, lock sulle release, ciclo doppio, retention con ricalcolo, immagini da release,
  delete documento).
- Verifica live **non eseguita in questo giro** (i difetti sono tutti in Application/Infrastructure con test
  di caratterizzazione; le tre modifiche UI sono di solo testo/condizione su componenti già verificati).
  Alla prossima sessione live vale la pena guidare: nascondi vIPI ACC → URL diretto; doppia schedulazione
  stesso ciclo → timeline; diff della release in vigore.
