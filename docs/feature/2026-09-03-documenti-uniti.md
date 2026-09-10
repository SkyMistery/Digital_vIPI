# Documenti uniti — una pagina, un editor, una pubblicazione — carta (3 settembre 2026)

> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Ramo `documenti-uniti`, da `main` (`cd1bc5c7`).
> Stato: ✅ **eseguita** — §1-§9 chiuse, verificate dal vivo.
>
> ⚠️ **L'ordine e' cambiato in corsa, e vale la pena dirlo**: il piano metteva l'editor unico prima
> della pubblicazione accoppiata. Il **comando** dell'unione viene prima di tutti e due, perche' senza
> non c'e' modo di CREARE un'unione — quindi niente da verificare dal vivo, su niente.

## La domanda

> «Deve essere possibile unire il documento di un APP con quello di un aeroporto — per esempio la vSOP di
> LIBV con quella dell'APP. Per l'esattezza: vIPI d'aeroporto con vIPI di APP non remotizzato, e vSOP
> d'aeroporto con vIPI di APP non remotizzato. E deve essere possibile **indipendentemente dal tipo di
> documento**. Inoltre deve essere possibile scegliere se unire le vIPI con le vSOP anche per gli aeroporti
> con *military presence*, oltre a quelli *military only* com'è ora.»
>
> E, precisando: *«Unire vuol dire mettere in una pagina sola due documenti, ma deve permettere all'editor di
> scegliere quale viene prima e quale dopo, e il meccanismo di release deve passare per un solo click: se due
> documenti sono uniti, la release pianificata o fatta su uno pubblica anche l'altro.»*

## §0 — Cosa c'era già, e cosa si è misurato

| Pezzo | Dove | Stato |
|---|---|---|
| Resa delle sezioni, riusabile N volte | `DocumentSectionsView` (`Profile` + `DerivedContent` sono parametri) | ✅ non ha stato: montarlo due volte è già supportato |
| Indice con intestazione propria | `DocumentToc.HeaderLabel` | ✅ un indice per membro, impilati |
| N editor di sezioni in una pagina | `AccEditorPage` (`RootSections="blockSection.Children"`) | ✅ **il pattern dell'editor unico esiste già** |
| Identità di un documento (famiglia, chiave, ACC, lock) | `IReleaseTarget.TryDescribe` → `ManagedDoc` | ✅ ma solo per l'elenco **intero** |
| Pubblicazione | `IReleaseService.PublishAsync` / `PublishNowAsync` | ✅ per **un** bersaglio |
| Un concetto di «documenti che si leggono insieme» | — | ❌ non esiste |

**⚠️ Il fatto misurato che ha deciso il modello.** Interrogando `src/Vipi.Host/vipi.db` in sola lettura:
**LIBV Gioia del Colle ha DUE APP non remotizzati** — `LIBV_APP` e `LIBV_G_APP` — e così LIBN, LIPE, LIRM,
LIRS. **L'unione non è una coppia, è un elenco ordinato**: due colonne su `Document` non reggerebbero un caso
che è già in archivio.

Documenti utili alle prove: **LIBA** (vIPI d'aeroporto #26 + `LIBA_APP` #3) è l'unica coppia aeroporto+APP già
scritta; **LIMN Cameri** (#28 civile + #29 militare) e **LIMS Piacenza** (#30 + #31) sono i campi misti con
tutte e due le edizioni — cioè la seconda richiesta.

## §0-bis — La posizione che il codice teneva, e perché si cambia

`src/Vipi.Application/Routing/MilDocRoutes.cs` diceva, per iscritto:

> «**Non è la stessa pagina con un parametro.** Le due edizioni hanno release, cicli AIRAC e contenuti
> indipendenti: condividere l'indirizzo vorrebbe dire che un collegamento salvato da qualcuno porta a un
> documento diverso a seconda di come è stato costruito.»

Quella regola **resta in vigore**, e l'unione non la viola:

- non è *un parametro*: è un **atto editoriale esplicito e reversibile**, registrato in archivio;
- i cicli AIRAC dei membri smettono di essere indipendenti **perché qualcuno ha deciso che lo smettano** — è
  il senso della pubblicazione accoppiata, ed è ciò che è stato chiesto;
- un collegamento salvato **continua a portare allo stesso contenuto**: la pagina del membro reindirizza a
  quella unita, ancorata al suo gruppo.

La §1b della [carta dei vSOP militari](2026-08-27-vsop-militari.md) scartava `Document.MilitaryTwinOf` e una
tabella `DocumentBinding` proprio per tenere i cicli indipendenti. Quella scelta era giusta **come default** e
resta il default: l'unione è l'eccezione che qualcuno chiede, campo per campo.

## §1 — Il modello ✅

Due entità in `src/Vipi.Domain/Entities/DocumentUnion.cs`, migrazioni `DocumentiUniti` nelle **due** serie:

```
DocumentUnion          Id · CreatedUtc · CreatedByUserId
DocumentUnionMember    Id · UnionId (FK cascade) · DocumentId (FK, indice UNICO) · Order
```

- **Indice unico su `DocumentId`**: un documento sta in al più **una** unione. Guardia, non speranza.
- Il membro con `Order` minore è l'**ospite**: pagina ed editor dell'unione vivono al suo indirizzo.
- **Nessun `ReleaseTargetType`, nessun `SectionProfile`, nessun `DocumentEdition` nuovo.** L'unione è una
  *relazione*: è ciò che la rende indipendente dal tipo senza toccare i sei descrittori di release, le sei
  rotte e i cinque provider di congelamento.
- ⚠️ **Il legame è verso `Document.Id`, non verso `TargetKey`.** La chiave di release è un *puntatore* e viene
  riscritta (`EfCallsignRenameService`, `RepointKeyAsync`): un'unione agganciata a quella si romperebbe alla
  prima rinomina di callsign.
- ⚠️ **Niente `RowVersion`**, e non è una svista: si tocca dall'editor, sotto il lock, un redattore alla volta.
  È la decisione del 14 agosto 2026, presidiata da `ConcorrenzaOttimisticaTests`.

**Le famiglie ammesse**, con il perché di ogni assenza (`DocumentUnionService.FamiglieAmmesse`):
`Airport`, `AirportMil`, `App`. Fuori restano `AccVipi` (è l'unica **a blocchi**, non passa da
`DocumentSectionsView`), `Vloa` (il suo viewer disegna da sé le due direzioni: il corpo non è ancora
montabile altrove) e ⚠️ **`AppMil`, che non ha un `IFrozenSectionProvider`** — un membro senza provider si
pubblicherebbe **senza congelare niente e senza protestare**, perché `FrozenSectionRegistry` per un tipo non
registrato risponde `Empty`. È il difetto già pagato con `AirportMil`.

**Una risoluzione sola, non una sesta scritta a mano.** `IDocumentAdminRepository` guadagna
`DescribeAsync(ids)`, che risolve l'identità dei soli id chiesti con **gli stessi** descrittori e **lo stesso**
insieme di `Include` di `ListAsync` — le due strade passano ora per un `DescriviAsync` privato.
⚠️ La ragione non è l'eleganza: quell'insieme di `Include` **è** la correttezza del risultato, un `Include`
mancante non dà errore ma fa sparire il documento in silenzio (è successo con `MilAirport`), e due copie della
query sono due posti in cui può divergere.

`TidyAsync` — che chiude le unioni rimaste con meno di due membri — gira **all'avvio**
(`VipiModuleExtensions.TidyVipiDocumentUnions`) e dopo ogni rimozione di membro.

**Reti**: `DocumentUnionRepositoryTests` (10, SQLite in memoria: ordine, indice unico, ricompattamento,
cascata, `Tidy` idempotente) e `DocumentUnionServiceTests` (11, puri: guardie, famiglie ammesse, candidati,
«leggere non chiede permessi»).

## §2 — I corpi dei viewer diventano componenti ✅

Da `AppnPage`, `AeroportoPage` e `MilDocumentPage` escono, per famiglia, un **caricatore**
(`*MemberLoader`, tutto quel che stava in `OnParametersSetAsync`) e un **componente-corpo**
(`*DocumentBody.razor`, le sezioni più il corpo derivato per chiave). Le pagine tengono il chrome.
Meccanico: la resa non cambia di una virgola, e la prova è che i 1147 test UI e i 289 E2E restano verdi
senza un ritocco.

⚠️ **`AirportMemberLoader` non si registra in DI**: `AeroportoPage` è `OwningComponentBase` per un motivo
misurato — sette morti con «A second operation was started» il 24 agosto — e il caricatore si costruisce dal
suo scope con `ActivatorUtilities`.

⚠️ L'ancora è **pubblica** sui tre componenti-corpo: `DocumentToc` vive nella cella sorella e deve ricevere
la stessa.

Estratti anche due aiutanti che erano scritti identici in quattro pagine: `SezioniDocumentali` (`ConSezioni`
+ la lettura del payload, ora con il `catch (InvalidOperationException)` che mancava sulle piste cotte) e
`MilProfiloTabelle` — le colonne delle tabelle militari stavano sulla **pagina del viewer** e l'editor le
citava da lì, un legame che si è rotto alla prima cosa spostata.

## §3 — La pagina unita ✅

`UnionLoader` prepara gli **altri** membri (ognuno col caricatore della sua famiglia) e ne consegna un
`RenderFragment` già confezionato — ⚠️ **non** un «tipo di famiglia» che poi ogni pagina switcherebbe:
aggiungere una famiglia deve costare **un caso lì dentro**, non un ramo in ogni pagina che ospita un'unione.
`UnionToc` impila un indice per membro (`HeaderLabel`), `UnionBodies` i corpi con l'intestazione del
documento. Un solo `PrintMeta`, quello dell'ospite; tre colonne come sempre.

⚠️ I membri si caricano **in sequenza**, mai in parallelo: due catene sullo stesso `DbContext` danno
«A second operation was started».

⚠️ L'ancora del gruppo è `doc-{DocumentId}` — sull'**id del documento** e non sulla posizione: l'ordine si
cambia con due frecce, e un'ancora che cambia con l'ordine è un collegamento salvato che un giorno porta
altrove.

⚠️ **L'ospite si riconosce da famiglia E chiave insieme** (`UnionView.IsHostTarget`): un aeroporto e il suo
vSOP militare hanno la **stessa** chiave di release (l'ICAO) e si distinguono per il tipo — è il fatto su
cui poggiano le due edizioni con cicli indipendenti. Confrontare la sola chiave farebbe disegnare alla
pagina civile l'unione del militare.
⚠️ Le sezioni con la **stessa chiave** nei due documenti restano **tutte e due**, distinte dal gruppo: chi non
le vuole le nasconde (`DocumentSection.IsHidden` esiste già). Decisione del committente.
🔴 **La lingua della PAGINA è dell'ospite; quella del CONTENUTO è di ogni membro.** Un documento a
lingua bloccata chiama `ReadingLanguageContext.Fissa`, che non ha un blocco che lo chiuda e vale per il
**resto della richiesta**: la sua stessa documentazione dice che regge perché una pagina documentale mostra
**un documento solo**, e l'unione ha rotto quella premessa senza che nessuno rileggesse quella riga. Trovato
in supervisione: con N membri, l'**ultimo caricato** con la lingua bloccata decideva la lingua delle
etichette e della prosa generata di **tutta** la pagina — ospite compreso — in base all'**ordine di
caricamento**, e nessun errore lo diceva. Ora i membri si caricano con `fissaLaPagina: false`; il loro
contenuto resta nella loro lingua perché traduzione, titoli di catalogo e derivate ricevono il codice come
**argomento**, non dal contesto. Rete: `LinguaDellaPaginaUnitaTests`.

⚠️ `?as=rel:{id}` nomina **una** release, quella dell'ospite: gli altri membri mostrano la **propria** release
dello **stesso ciclo**. E il degrado di un'anteprima non autorizzata deve restare quello di oggi — pubblica
**con `_useFrozen = true`**, o il congelamento AIRAC si aggira dall'indirizzo.

## §4 — Il redirect ⛔ SUPERATA dalla §13 (10 settembre 2026): il rimando non esiste più

La vista **pubblica** di un membro non-ospite rimanda alla pagina unita, ancorata al suo gruppo. Precedente
esatto: `ReleasePreviewPage` — `NavigateTo(url, replace: true)` **senza `@rendermode`**, così diventa una vera
302 che il browser segue prima di disegnare.
⚠️ **Solo la vista pubblica**: editor e anteprime `?as=` di ogni membro restano al loro indirizzo.

🔴 **E solo se l'ospite ha qualcosa DA MOSTRARE.** Trovato in supervisione: unire un APP già
**pubblicato** sotto una vIPI d'aeroporto ancora **in bozza** è un gesto di due clic, e il rimando mandava
chi apriva l'APP su una pagina che dice «niente da mostrare» — un documento in vigore tolto dal web da un
gesto editoriale che non lo riguardava. Ora il rimando chiede prima: ospite **non nascosto** e con una
release **in vigore adesso**, perché la visibilità pubblica È la release effettiva. Se non ce l'ha, il
membro resta dov'è e si legge da sé: non è la pagina unita, ma è vero. Rete: `RimandoAllOspiteTests`.

## §5a — Il comando dell'unione ✅

`UnionPanel` sta nei tre editor, **sopra** il pannello di pubblicazione: e' cio' che decide QUANTI
documenti quel tasto pubblichera'. Elenco numerato dei membri (l'ordine e' proprio la cosa che si sta
decidendo), pastiglia «ospite», frecce, «togli», «sciogli», e la tendina «unisci a…» con i candidati
dello **stesso scalo** in cima — senza recinti per ACC: «indipendentemente dal tipo di documento» vuol
dire anche senza un recinto che qualcuno dovra' scavalcare.

⚠️ **Scope proprio** (`OwningComponentBase`): qui si SCRIVE. ⚠️ Non e' il caso di `ReleasePanel`, che il
contesto non lo isola **apposta** — la' il publish e' un'operazione sola composta con il
`BeforePublishAsync` della pagina. ⚠️ E la guardia di `OnParametersSetAsync` sta **prima dell'await**:
lo scope proprio protegge dagli altri, non da se' stessi.

⚠️ L'errore si **mostra**: una `ValidationException` qui dice cose che chi ha premuto deve sapere —
«questo documento e' gia' unito ad altri» col NOME di quali.

## §5b — I corpi degli editor, e l'editor unico ✅

**L'estrazione**, a diff minimo: le tre pagine restano gusci sottili e il corpo editoriale passa in
`Components/Doc/{App,Mil,Airport}SectionsEditor.razor`. Il flag `Chrome` spegne testata, indice e rail
quando il componente e' un MEMBRO.

⚠️ **Il vincolo che decide la forma**: `DocumentSectionsEditor` **si costruisce la propria griglia**
(`ed-layout` + `EditorToc`). Montarne uno per membro darebbe N griglie e N indici, uno sotto l'altro. La
pagina ospite possiede la griglia e monta i figli con `ShowToc="false"` — e' il pattern che la vIPI ACC usa
gia' per i suoi blocchi.

**L'orchestrazione**: `UnionMembersEditor` monta i membri dentro l'`AfterSections` dell'ospite (quello slot e'
reso DENTRO la colonna centrale: fuori, i corpi finirebbero larghi quanto la pagina). I membri si
**registrano** via `IMembroEditor` invece di essere presi con `@ref` — `@ref` vuole il tipo concreto, e le
famiglie sono tre; cosi' l'ospite ne comanda N senza sapere quale sia quale, e una quarta famiglia costa un
caso nello switch invece di un ramo in ogni pagina.

⚠️ **Il lock si prende su TUTTI in un gesto, o su nessuno.** Se anche uno solo e' tenuto da un altro, quelli
gia' presi si **rilasciano** e si dice **chi** lo tiene. Mezzo lock preso e' peggio di nessun lock: chi crede
di star modificando due documenti ne starebbe modificando uno, e lo scoprirebbe al salvataggio.
⚠️ `PrendiLockAsync` torna il **nome** e non un booleano, perche' e' quello che va detto: «non puoi
modificare, lo tiene Tizio» e' una risposta, «non puoi modificare» e' un muro.

⚠️ Sull'aeroporto `RilasciaLockAsync` passa da `FineModificaAsync` e non dal guscio: li' uscire **salva**
quel che e' in sospeso (i tre editor dei dati dello scalo hanno buffer a salvataggio esplicito), e mollare il
lock senza quel passo butterebbe via quello che si stava scrivendo.

**Le reti che sono andate rosse, e dicevano il vero.** Le invarianti seguono chi le porta:
`ScopeDellEditingTests` (chi possiede lo scope), `DatiDelloScaloMilitareTests` (tutte quelle di §AS),
`GerarchiaTitoliTests` (la testata) — con esenzioni **motivate** e una rete nuova che pretende la testata nei
componenti: il titolo non e' sparito, ha cambiato file.

### ⚠️ Le tre cose che ha trovato la verifica dal vivo, e nessun test vedeva

1. **L'indice unito restava con le sole voci dell'ospite** — otto invece di diciotto, e nessun errore. Tre
   cause in fila, ognuna nascosta dalla precedente: le voci si **tiravano** con un `@ref` (assegnato *dopo*
   il render, mentre i membri si registrano *durante*); si spingevano **una volta sola**, quando il documento
   del membro **non e' ancora caricato** e le sue sezioni sono zero; e la `.Concat` che le univa **non era
   mai stata applicata** — lo script che la metteva era morto prima, su un altro errore, e il parametro
   esisteva senza che nessuno lo leggesse. ⚠️ **Un parametro dichiarato e mai letto non da' nessun segnale**:
   compila, si passa, e non fa niente.
2. Dopo l'estrazione, `AeroportoEditorPage` citava se stessa in un `DotNetObjectReference<>`: un rename alla
   cieca ha poi riscritto anche la **frase** del commento in testa, che e' diventata «sta fuori da se'
   stesso». ⚠️ Un rename globale non distingue il codice dalla prosa.
3. La conferma vera che l'invariante del lock funziona: con un membro bloccato da un altro, il messaggio
   nomina chi lo tiene e **in archivio l'ospite resta senza lock**.

🔴 **E l'ultima domanda prima di pubblicare si fa a TUTTI.** Il pannello di pubblicazione sta solo
sull'ospite, e con lui la sua `BeforePublishAsync`: l'avviso «sezioni non salvate» di una vIPI d'aeroporto
**non veniva chiesto** quando quell'aeroporto era un MEMBRO, e la sua fotografia usciva senza le modifiche
aperte — in silenzio, e la fotografia è quel che il pubblico legge. Ora `IMembroEditor` porta la guardia
(con un default «vai», per le famiglie che non hanno niente da chiedere) e ognuno dei tre ospiti compone la
propria con quella dei membri. ⚠️ Si chiede **attraverso l'interfaccia**: un membro di default non si vede
dal tipo concreto, e due delle tre pagine non compilavano affatto. Rete: `GuardiaDiPrepubblicazioneTests`,
sul **sorgente** — una guardia che nessuno passa non fallisce nessun render, non fa semplicemente niente.

**Verificato dal vivo su LIBA**: una griglia sola, il gruppo `doc-3` «Amendola Approach» con le sue dieci
sezioni, l'indice con **tutti e due** i documenti raggruppati, i **due lock presi insieme** (documenti 26 e 3,
a millisecondi di distanza), e il rifiuto pulito quando uno e' occupato.

## §6 — La pubblicazione accoppiata ✅

L'accoppiamento sta **dentro** `PublishAsync` / `PublishNowAsync`: chi pubblica un documento unito pubblica
tutti i membri, e non c'e' una seconda porta da ricordarsi di chiamare.

🔴 **C'era, ed e' durata mezza giornata.** La prima stesura aggiungeva `PublishUnionAsync` /
`PublishUnionNowAsync` accanto a quelle normali, e faceva passare `ReleasePanel` di li'. La supervisione del
3 settembre ha trovato che **l'elenco di governo continuava a chiamare quelle normali**: mostrava la
pastiglia «uniti: 2» e ne pubblicava **uno**. Nessun errore, nessun rosso — il documento che si aveva in
mano usciva pubblicato davvero, e l'altro restava indietro di un ciclo.

⚠️ **La lezione non e' «aggiornare il chiamante»**, e' che due porte per lo stesso gesto, di cui una
sola sicura, sono un invito a chiamare quella sbagliata. `CancelReleaseAsync` era gia' accoppiata dentro
di se' e infatti da quella pagina funzionava: l'asimmetria fra le due era il difetto. Oggi sono tre porte
con la stessa sicurezza, e il chiamante non ha una scelta da azzeccare.

`BersagliUnitiAsync` dice **prima** quanti documenti quel tasto tocchera' e **chi ne tiene il lock**, e il
pannello lo mostra. ⚠️ Un esito che tace meta' del lavoro e' peggio di nessun esito, e qui la meta' taciuta
sarebbe un altro documento pubblicato.

- ⚠️ **I cancelli PRIMA, tutti, e fuori dalla transazione**: un permesso negato o un lock altrui non sono
  scritture da annullare, e scoprirli a meta' elenco vorrebbe dire aver gia' fotografato qualcuno. Un lock
  altrui su **un solo** membro ferma **tutta** la pubblicazione, e non scrive niente. Un test lo pinna.
- ⚠️ **Tutto in `IUnitOfWork.ExecuteInTransactionAsync`, la pianificata compresa**: `SaveReleaseAsync` fa un
  `SaveChanges` per chiamata e `VersionNumber` e' `max+1` letto in memoria sotto un indice UNICO.
- ⚠️ **In sequenza, mai in parallelo**: la cattura apre `ShapeReleaseContext.Capturing`, che NON e'
  annidabile, e `ReadingLanguageContext.Rendering` con la lingua sorgente di QUEL membro.
- ⚠️ **UN solo `now`** per tutti nella «pubblica ora»: chiederlo dentro il ciclo darebbe date efficaci
  diverse di qualche millisecondo, e la selezione della release effettiva ordina proprio per quella.
- Le **due semantiche restano diverse** anche unite: la pianificata non promuove la bozza, la «pubblica ora»
  si', per ogni membro.
- **Annullare si accoppia**: `CancelReleaseAsync` porta via anche le sorelle dello **stesso ciclo**, nella
  stessa transazione. ⚠️ Di ogni membro la **piu' recente** di quel ciclo, non tutte: portarsi via le
  superate cancellerebbe storia che nessuno ha chiesto di cancellare. ⚠️ E un membro che a quel ciclo non ha
  pubblicato non ha niente da annullare — puo' essere entrato nell'unione dopo.

**Reti**: `PubblicazioneAccoppiataTests` (9, su LIMN Cameri: due edizioni, stessa chiave, tipi diversi).

## §7 — Il governo ✅

Dalla supervisione dei vSOP militari: *il documento era agganciato al motore di **lettura** e non a quello
di **governo***. Qui la stessa domanda ha avuto **tre** risposte, e due sono «niente da fare» con una ragione:

1. **L'elenco unificato** (`/services/vsop/versions`) mostra una pastiglia 🔗 «uniti: N» sulle righe dei
   documenti in un'unione. ⚠️ Da li' si **pubblica**, e chi preme deve sapere PRIMA quanti documenti sta per
   mandare fuori — e da li' si pubblicano davvero **tutti**, perche' l'accoppiamento sta nella porta (§6) e
   non in questa pagina. 🔴 Per mezza giornata non e' stato vero: la pastiglia diceva 2 e il tasto ne
   mandava fuori 1. Le appartenenze si leggono in **una** query (`IDocumentUnionService.TutteAsync`), non una
   per riga: quell'elenco ha già pagato due volte il difetto N+1.
2. **L'eliminazione** scioglie l'unione **subito**, non al prossimo avvio: la cascata della FK toglie già la
   riga di appartenenza, ma l'unione rimasta con un membro solo è una pagina che unisce sé stessa e un
   redirect che non ha dove mandare. Rete: `Eliminare_un_membro_SCIOGLIE_l_unione_subito`.
3. **Ricerca, «Novità» e impatti non si toccano**, ed è una decisione, non una dimenticanza: quei tre
   producono l'URL del documento con `DocRoutes`, e la **vista pubblica di un membro reindirizza già** alla
   pagina unita (§4). Renderli «consapevoli dell'unione» vorrebbe dire tre punti di chiamata in più da tenere
   d'accordo, per ottenere quello che un rimando fa da solo. ⚠️ **Un redirect al posto di N chiamanti**: se
   un giorno il rimando cambia, cambia in un posto.

## §9c — Le sei cose piccole della supervisione (3 settembre 2026) ✅

Nessuna di queste faceva cadere niente; cinque su sei erano gesti che a volte non facevano nulla, che è la
categoria peggiore da lasciare in giro.

| Cosa | Perché |
|---|---|
| Le frecce ↑↓ si spengono sulla **posizione**, non su `Order` | La cascata della FK non rinumera: con le posizioni 0 e 2 l'ultimo membro teneva la freccia «giù» accesa su un gesto che non faceva niente. E `CompattaAsync` chiude il buco all'avvio |
| Il gruppo di trascinamento sull'**id del documento**, non sul titolo | Due membri omonimi avrebbero condiviso il gruppo, e una sezione si sarebbe potuta trascinare da un documento all'altro. Il repository la rifiuta comunque, ma un gesto che a volte non si può fare è peggio di uno che non si può fare mai |
| L'indice unito confronta **etichette**, non solo ancore | **Rinominare** una sezione di un membro non rinfrescava il menu: l'ancora non cambia. La card prendeva il nome nuovo e l'indice restava col vecchio |
| ⚠️ Il commento di `UnionToc` diceva `s-{Id}` | Non è mai stato vero: l'ancora è `SectionView.Id` nudo. **Il codice era giusto e la prosa no**, ed è la peggiore delle due da sbagliare — chi costruisce un'ancora leggendo il commento ottiene un link che non fa niente, senza errore |
| Un modo solo di costruire i caricatori | Tre famiglie, tre modi (DI, `ActivatorUtilities`, non registrato): la strada per cui il quarto viene preso dal posto sbagliato. Ora tutti e tre con `CreateInstance`, che funziona registrato o no |
| La violazione dell'indice unico si **racconta** | Fra il controllo «sei già unito?» e la scrittura c'è una finestra: due redattori nello stesso istante e chi arriva secondo vedeva la `DbUpdateException` nuda — proprio ciò che il controllo anticipato doveva risparmiargli. ⚠️ Una transazione non basterebbe: l'indice è il guardiano, e due transazioni concorrenti lo violano lo stesso |

E in stampa `.union-part-h` porta `break-after: avoid`: l'intestazione di un membro non si separa dal suo
contenuto, cosa che su una pagina lunga il doppio del solito capita.

⚠️ **Due prove sono nate sbagliate, e per la stessa ragione**: confrontavano una parola italiana
(«ricarica») e la stringa `"it"`, mentre l'host dei test gira in **inglese** — sarebbero state verdi solo su
una macchina italiana. Un messaggio bilingue non si controlla per parola: si controlla che il dettaglio
tecnico non sia arrivato fino a chi legge.

## §9d — Le prove a schermo della supervisione (3 settembre 2026) ✅

Guidate con la skill `verifica-live` su una **copia del `vipi.db` reale** (migrazione applicata: `Applying
migration '20260903092733_DocumentiUniti'`), porta 5034, Edge via `puppeteer-core`. Il `vipi.db` del
progetto è rimasto intatto. **Zero errori di pagina** in tutta la sessione.

Il banco di prova è stato scelto per quello che aveva già in archivio: **LIBA** ha la vIPI d'aeroporto
**senza nessuna release** e l'APP **pubblicato** — cioè esattamente il difetto §3 — e **LIMN Cameri** è il
campo misto con le due edizioni.

| Difetto | Che cosa si è visto |
|---|---|
| **§1** — pubblicazione accoppiata | Da `/services/vsop/lirr/versions`, «Publish now» sulla riga dell'**APP**: release **57** (`Airport/LIBA` v1) e **58** (`App/LIBA_APP` v3), ciclo **2609**, `ReleaseEffectiveUtc` **identico** (`14:22:08.5445568`), tutte e due `Effective`, le precedenti `Superseded`, e **le bozze di entrambi** promosse a `Published`. Prima ne usciva **uno** |
| **§1** sull'asse misto | Stessa cosa da LIMN: release **59** (`AirportMil`) e **60** (`Airport`), ciclo 2609, stessa data efficace |
| **§2** — lingua | vSOP ospite non bloccato + vIPI civile membro **bloccata in inglese**, pagina a `culture=it`: le intestazioni restano **TIPO / COORDINATE / FREQUENZA**, e `lang` sui corpi è `["it","en"]` — **due lingue in una pagina**. ⚠️ **Controllo**: bloccando l'**ospite** la pagina gira davvero (`TYPE / COORDINATES / FREQUENCY`), quindi la sonda è sensibile e il verso giusto funziona ancora |
| **§3** — rimando | Con l'ospite **senza release**, la pagina dell'APP **resta a casa sua** (10 voci d'indice) mentre quella dell'ospite dice *«No airport vIPI published for LIBA»* — il vicolo cieco in cui mandava i lettori. Dopo la pubblicazione il rimando **riparte**: `/apps/vipi?app=LIBA_APP` → `/airports?icao=LIBA#doc-3`, 19 voci, **zero ancore cieche**, **un solo** `.print-meta` |
| **§7** — lock | Con i lock di **tutti** i membri presi da me (verificati freschi in archivio): **zero** pastiglie ambra. Passando il lock del membro a un altro: pastiglia col suo nome, «Edit» rifiuta dicendo **chi**, e l'ospite resta **senza lock in archivio** — mezzo lock non preso |
| **§8** — domanda dell'annullamento | Sulla stessa timeline: al **2609** (dove il compagno ha pubblicato) dice *«all 2 releases of that cycle are cancelled»*; al **2608** (dove non ha pubblicato) quella frase **non c'è** |
| **§9** — frecce | ↑ del primo e ↓ dell'ultimo **spente**; spostando il membro in cima diventa **ospite**, le frecce si rispengono ai nuovi bordi e **il rimando si gira**: ora è la pagina militare a portare a quella civile, `#doc-29`, 34 voci, zero ancore cieche |

### 🔴 Un difetto NUOVO, trovato guidando

Da `/services/vsop/{acc}/versions` la pubblicazione ora esce accoppiata — giusto — ma la domanda diceva
ancora *«Publish right away… **The document** becomes public immediately»*, al **singolare**, mentre ne
mandava fuori due. ⚠️ **È la terza volta in questo giro che lo stesso conteggio sbaglia**: la domanda
dell'annullamento prima sottostimava, poi sovrastimava, e qui taceva del tutto. Corretto: la domanda e il
titolo del «pubblica al ciclo» passano da `AvvisoUnione(d)`, che conta da `Unito(d)`.

### ✅ E anche la §5, al secondo tentativo — con la domanda giusta all'attrezzo

Al primo giro la §5 non era uscita: nessun modo di scrivere in una tabella faceva salire `_dirtySections`.
La prova di **controllo** diceva che sbagliava l'attrezzo (sull'ospite, dove quella guardia esiste da prima
di questo lavoro, il contatore restava a zero identicamente) ma non diceva *cosa*.

🔴 **La domanda giusta era: esiste un gesto che sporca in modo DETERMINISTICO?** Sì: «+ Row» chiama
`OnChanged` direttamente, senza passare da nessun evento del browser. Premuto: **`Save all (1)`**. Quindi il
collegamento c'era e a non arrivare era la *scrittura* — né gli eventi fabbricati né i tasti veri col blur
attraversano il binding di quel campo dal driver.

⚠️ **La lezione non è «l'attrezzo sbagliava»** — quella si sapeva già. È che davanti a un gesto che non
fa niente conviene cercare **l'altro gesto che fa la stessa cosa per una strada più corta**: separa
«l'attrezzo non arriva» da «il collegamento non c'è» in un colpo, mentre la prova di controllo li lascia
tutti e due in piedi.

Con quel gesto la §5 si guida, e va nei due versi:

| Passo | Esito |
|---|---|
| «+ Row» nelle quote di transizione **del membro**, poi «Publish now» **dall'ospite** | *«Unsaved changes in: Quote di transizione. They will not enter the release. Continue?»* — la domanda viene dal MEMBRO, sollevata dal tasto dell'ospite |
| Rifiutando | **nessuna** release creata (16 prima, 16 dopo): il no di un membro ferma tutta l'unione |
| Accettando | la coppia **75/76**, stesso ciclo, `ReleaseEffectiveUtc` identico |

ℹ️ E un pezzo di prova arrivato per caso: i sei tentativi falliti avevano pubblicato sei volte, e in
archivio sono **sei coppie perfette, mai un orfano**.

## §8 — La seconda richiesta: vIPI + vSOP sui campi con presenza militare ✅

È lo **stesso meccanismo**, senza codice in più: su un campo misto si uniscono la vIPI civile e il vSOP, e la
pagina unita legge come un documento completo — cioè come oggi legge il vSOP di un campo *solo* militare, che
di quel campo è l'unico documento. Su LIMN e LIMS i due documenti esistono già.

⚠️ **Le due guardie gemelle §11b non si toccano**: su un campo misto la vIPI civile viene prima del vSOP, su un
campo solo militare la civile non nasce. L'unione **presuppone** che i documenti esistano; non è il posto da
cui cambiare chi può nascere.

## §9 — Le verifiche dal vivo (3 settembre 2026) ✅

Guidata su **LIBA Amendola** (ACC LIRR) con la skill `verifica-live`: copia del `vipi.db`, porta 5034, Edge
via `puppeteer-core`. Il DB del progetto e' rimasto intatto (`git status` muto).

**Quel che ha confermato**

| Passo | Esito |
|---|---|
| Pannello nell'editor, documento non unito | «This document is read on its own» |
| Candidati | `Amendola Approach — LIBA_APP (same airfield)` **in cima**, gli altri dopo, senza recinti per ACC |
| Unione | due membri, l'ospite marcato |
| Pagina unita | **due indici impilati** intestati coi titoli dei documenti; gruppo `doc-3` con dieci sezioni; **tre colonne** (`248px 857px 308px`); **un solo** `.print-meta` |
| Ancore | indice `#s-706` ↔ corpo `s-706`: combaciano |
| Redirect | `/apps/vipi?app=LIBA_APP` → `/airports?icao=LIBA#doc-3` |
| Anteprima del membro | `?as=draft` dell'APP **resta dov'e'** |
| «Pubblica ora» | due release, ciclo **2609**, `ReleaseEffectiveUtc` **identico**: `2026-09-03 10:28:00.9619002` su tutte e due |
| Pianificata | due release, ciclo **2610**, stessa data efficace, tutte e due `Scheduled` |
| Annullamento | un clic, e la coppia del 2610 sparisce **tutta** |
| Scioglimento | unione via, la pagina dell'APP smette di reindirizzare, l'aeroporto torna a un indice solo |

**⚠️ E le DUE cose che ha trovato**, che i test verdi non vedevano — tutt'e due nel pannello di release:

1. **La domanda prima di annullare MENTIVA.** Diceva «il pubblico torna alla precedente», al singolare,
   mentre ne toglieva due. Chiedere «annullo questa?» per poi toglierne due e' la stessa categoria
   dell'esito che tace meta' del lavoro, **ma peggiore**: qui la meta' taciuta e' una pubblicazione che
   sparisce. Ora la domanda conta.
2. **Il pannello non rileggeva l'unione nata nella stessa pagina.** La sua memoizzazione e' su
   `(bersaglio, chiave)`, e quelle non cambiano quando si unisce un documento: subito dopo aver unito
   continuava a dire «questo documento e' solo». Ora l'host alza una `Revisione` che entra nella chiave.

⚠️ **E una terza, di convenzione**: avevo scritto `string.Format(L[chiave].Value, n)`, che **non**
interpola — il secondo indexer, `L[chiave, n]`, e' l'unico che formatta. E' la stessa lezione che il test
del numero di versione (`Rel_VersionLabel`) aveva gia' messo per iscritto. Il localizzatore finto dei test
la rende visibile; in produzione l'argomento sarebbe sparito **in silenzio**.

⚠️ **Trappola dell'attrezzo, per il prossimo**: l'interfaccia dell'app in questa verifica era in
**INGLESE**, e il primo giro di script cercava il tasto «Unisci» — non lo trovava, il tasto restava spento e
sembrava che l'unione non nascesse. Quando un gesto «non fa niente», il primo sospetto va al **selettore**.

### §9b — Il caso MISTO e PUBBLICATO: LIMN Cameri

⚠️ **La regola pagata due volte sui vSOP militari**: quando si prova una famiglia gemella di un'altra, il
caso di prova si sceglie **misto e PUBBLICATO** — corto e in bozza nasconde i difetti che contano. LIBA (§9)
e' aeroporto + APP; **LIMN Cameri** e' l'altro asse, quello che la seconda richiesta chiede davvero: presenza
militare senza essere solo-militare, vSOP **gia' pubblicato** con release effettiva al 2608, vIPI civile in
bozza e **senza** release.

⚠️ **La migrazione e' stata applicata su una COPIA DEL `vipi.db` REALE**, non su un database vuoto da
`EnsureCreated`: e' la regola del runbook, e qui l'ha superata (`Applying migration
'20260903092733_DocumentiUniti'`).

| Passo | Esito |
|---|---|
| Candidati sul vSOP | `vIPI — LIMN Cameri — LIMN (same airfield)`: ⚠️ **stessa chiave, tipo diverso**, e la tendina non offre se stesso |
| Unione | vSOP **ospite**, vIPI civile membro |
| Editor unito | **una** griglia; indice con i due gruppi (26 sezioni militari + le 8 civili sotto «VIPI — LIMN CAMERI»); il pannello **settori ATC** del membro resta nel suo gruppo |
| «Pubblica ora» | release **57** (`AirportMil/LIMN` v3) e **58** (`Airport/LIMN` v1), ciclo **2609**, `ReleaseEffectiveUtc` **identico**, tutte e due `Effective` |
| Promozione | ⚠️ **entrambi** i documenti passano a `Published`: la civile era in bozza, e la «pubblica ora» accoppiata ha promosso la bozza **di ogni membro** |
| Pagina pubblica unita | due indici, gruppo `doc-28`, tre colonne, **un solo** `.print-meta`, e **34 voci d'indice con ZERO ancore senza bersaglio** |
| Redirect | `/airports?icao=LIMN` → `/mil?icao=LIMN#doc-28` — sul campo misto la pagina civile porta alla vSOP unita |
| Anteprima `?as=rel:57` (2609) | banner del 2609, e il membro mostra **la sua** release di quel ciclo |
| Anteprima `?as=rel:48` (2608) | banner del 2608, e il membro — che a quel ciclo **non aveva pubblicato** — ricade sulla **pubblica**, che e' la verita' |
| Elenco di governo | tutte e due le righe con la pastiglia 🔗 **«joined: 2»** |

Zero errori di pagina in tutta la sessione. Il `vipi.db` del progetto è rimasto intatto.

⚠️ **Ed è esattamente la seconda richiesta, vista a schermo**: su un campo con presenza militare il vSOP
diventa il documento **completo** dello scalo — come lo sono oggi quelli dei campi *solo* militari — e la
pagina civile ci porta invece di vivere per conto suo.
## §10 — La chip di lettura è dell'UNIONE, non dell'ospite (7 settembre 2026) ✅

Segnalato dal committente: unendo due documenti di cui **solo il secondo** ha sezioni marcate pilota/ATC, i
tre comandi *Tutto · Pilota · ATC* **sparivano dal viewer**.

**La causa.** La chip è **una sola per pagina** e la disegna l'**ospite**, che si chiedeva «*io* ho sezioni
marcate?» (`_doc.HaMarcate`) invece di «ce n'è qualcuna **in questa pagina**?». Ogni caricatore di famiglia il
suo `HaMarcate` lo calcolava già (`AppMemberLoader`, `AirportMemberLoader`, `MilMemberLoader`), ma
`MembroUnito` **non lo portava fuori**: il valore esisteva e non arrivava a chi doveva deciderne.

⚠️ **Nessun errore, nessun rosso, e il filtro funzionava lo stesso**: `?vista=atc` scritto a mano filtrava
anche il membro, perché `Vista` ai membri arriva (`AltriMembriAsync`). Mancava il solo modo di **chiederlo con
un clic** — la stessa forma dei tre difetti seri della supervisione: una cosa **falsa a schermo**.

**Il rimedio.** `MembroUnito` porta `HaMarcate`, e la domanda si fa all'unione intera con
`MembroUnito.QualcunoHaMarcate(ospite, altri)` — ⚠️ **una funzione sola e non tre condizioni copiate**: i
chiamanti sono `AeroportoPage`, `AppnPage` e `MilDocumentPage`, e la stessa condizione scritta tre volte è la
prima a divergere. La vIPI **ACC** e la **vLOA** restano fuori: non sono famiglie unibili, e la loro chip
guarda il proprio documento e basta.

⚠️ **Il filtro vale su tutta la pagina unita**, ospite compreso: in vista ATC un ospite tutto «per tutti»
resta **intero** sotto un membro filtrato. È la regola di `AudienceFilter` — le sezioni non marcate non si
filtrano mai — e qui è quella giusta: la pagina unita è **un** documento per chi la legge.

**Le reti.** In `DocumentiUnitiTests`: le marcate di un membro tengono la chip accesa; senza marcate da
nessuna parte resta spenta; quelle dell'ospite bastano da sole. ⚠️ E una **guardia sul sorgente** delle tre
pagine (`Ogni_pagina_che_OSPITA_chiede_la_chip_all_unione_intera`), perché le prime tre non vedrebbero una
pagina che torna a chiedere `_doc.HaMarcate`: **provata rimettendo il difetto**, e va rossa.

### ✅ Provato a schermo il 7 settembre, con il difetto RIMESSO e ritolto

Banco su copia del `vipi.db` reale: **LIBA** — ospite la vIPI d'aeroporto (doc 26, **bozza**, `?as=draft`),
membro `LIBA_APP` (doc 3). Marcata **una sola** sezione, «Separations», e **sul membro**; l'ospite tutte «per
tutti». È esattamente il caso segnalato: le marcate solo sul **secondo**.

| Passo | Esito |
|---|---|
| Pagina unita, vista «tutto» | chip **presente**, tre voci, «Everything» attiva; indice a due gruppi |
| Vista **ATC** | «ATC» attiva; «Separations» c'è, col badge *ATC only* |
| Vista **pilota** | «Pilot» attiva; «Separations» **sparisce dal corpo E dall'indice**, badge a zero |
| ⚠️ Controllo: tolta la marcatura al membro | chip **spenta**, unione ancora disegnata — non è una chip che sta sempre accesa |
| 🔴 Controllo vero: **difetto rimesso** nel codice e ricompilato | chip **assente** con il badge ATC ancora a schermo: il sintomo segnalato, riprodotto |
| Rimedio rimesso, ricompilato | chip di nuovo presente, stesso DB e stessa pagina |

⚠️ **Il controllo che conta è il quinto**: gli altri dicono che la pagina si comporta bene, non che sia
*questa* riga a farlo. Rimettere il difetto e vederlo tornare è l'unico passo che lega il sintomo al rimedio —
e costa due ricompilazioni.

⚠️ **L'app girava in INGLESE**, e la prima asserzione cercava «Separazioni»: rispondeva «assente» in tutt'e tre
le viste. È la trappola dell'attrezzo già scritta in §9d — quando un gesto «non fa niente», il primo sospetto
va al **selettore**, non al codice.

## §11 — Le sezioni in comune: da quale documento spariscono (7 settembre 2026) ✅

Chiesto dal committente: unendo la vIPI d'aeroporto di uno scalo e il suo vSOP militare la pagina **ripete**
METAR, frequenze, piste, quote di transizione. All'unione il sistema deve **chiedere** da quale documento
nasconderle, e nasconderle come farebbe una persona col tasto «nascondi».

**La forma decisa** (tre domande al committente, tre risposte):

1. **Documenti + caselle**: si spuntano i documenti **da cui le sezioni spariscono**, e sotto l'elenco delle
   sezioni con le caselle già proposte.
   🔴 **La polarità è stata girata l'8 settembre**, e la lezione vale oltre questa scheda. La prima stesura
   chiedeva **chi le TIENE** — la stessa scheda letta al contrario — e il committente l'ha scoperto chiedendo
   conferma: «se seleziono vIPI vengono nascoste le sezioni della vIPI?». No: teneva. La sua richiesta
   iniziale diceva *«se seleziono vipi nasconde metar, frequenze, ecc delle vipi»*, e nella domanda che gli
   avevo fatto la polarità era cambiata **dentro un'opzione che parlava d'altro** (il *modo*: lato + caselle).
   ⚠️ **Una domanda cambia UNA cosa sola**: se ne cambia due, la risposta ne conferma una e l'altra passa
   senza che nessuno l'abbia decisa. Con tre membri (LIBV ha due APP) le caselle reggono lo stesso: se ne
   spuntano due.
   ⚠️ E spuntarli **tutti** si può — «quel dato qui non lo vogliamo» — ma la sezione sparisce dalla pagina
   unita per intero: la scheda lo **dice**, non lo vieta.
2. **La validità sta in elenco ma non spuntata**: è comune per *chiave*, non per significato — dice ciclo e
   release **di quel documento**, e in un'unione sono due.
3. **Il comando resta**, non solo appena si unisce: le sezioni si aggiungono dopo, e una comune che nasce
   domani oggi non esiste.

**Che cosa è «in comune»**: la **chiave di catalogo**, a qualunque profondità. Nel vSOP le frequenze si
chiamano «Frequenze ATC/CRC» e stanno **dentro** «Dati generali»; nella vIPI si chiamano «Frequenze» e stanno
in cima. Stessa chiave `frequencies`, e un confronto per titolo — o sui soli primi livelli — non troverebbe
niente proprio nel caso per cui la scheda esiste. Le sezioni **libere** restano fuori per costruzione: la
loro chiave nasce unica.

⚠️ **Chi NON è spuntato si MOSTRA, non si lascia com'è.** Senza, cambiare idea lascerebbe nascoste tutt'e
due le copie: la seconda scelta nasconde l'altro e non rimette il primo.

Codice: `SezioniComuni` (puro) · `IEditingService.SezioniComuniAsync` / `ApplicaSezioniComuniAsync` ·
la scheda in `UnionPanel`. Il flag è lo stesso di `SetSectionHiddenAsync`, e ogni scrittura passa dalla
**stessa porta**: autorizzazione e lock per documento, sezione per sezione.

### 🔴 Le due cose che ha trovato la prova a schermo, invisibili ai test

1. **Un membro unito a modifica GIÀ APERTA nasceva senza lock.** `ModificaTutti` era passato prima che
   esistesse: da lì in poi ogni scrittura su di lui — la scheda, o i suoi stessi campi — cadeva con «il
   documento è bloccato da un altro redattore», che è **falso**. ⚠️ E il rimedio ovvio era sbagliato:
   prendere il lock alla **registrazione** non fa niente, perché lì il documento del membro non è ancora
   caricato e `PrendiLockAsync` torna `null` — cioè «preso» — **senza prendere niente**. Un no-op che si
   dichiara riuscito è peggio di un errore. Ora il lock si prende quando il membro è **pronto**
   (`UnionMembersEditor.AssicuraLockAsync`, da `MembroCambiato` e da `OnAfterRenderAsync`).
2. **Riaprendo la scheda, la proposta tornava sempre la stessa**: chi premeva senza guardare **ribaltava** la
   scelta di prima — «22 sezioni cambiate» invece di nessuna. Ora la fa lo **stato**
   (`SezioniComuni.DoveNascondere`: chi ne ha già nascosta almeno una; a stato vergine, tutti tranne l'ospite).

E una terza, di robustezza: `EditConflictException` nel pannello ora si **mostra**; prima faceva cadere il
circuito, e a schermo diventava «Attempting to reconnect».

### ✅ Provato a schermo — LIMS Piacenza (civile + militare, tutt'e due in bozza)

| Passo | Esito |
|---|---|
| Unisco la vIPI col vSOP dello stesso scalo | la scheda si apre **da sola** |
| L'elenco | 12 voci: METAR, quote di transizione, frequenze, piste, procedure generali, carte (5) — spuntate — e **validità non spuntata** |
| «Nascondi quelle della vIPI» → nascondi | «Done: **11** sections changed»; in archivio **11 nascoste sulla vIPI, 0 sul vSOP**, e nell'editor le sue sezioni portano la pastiglia «Hidden» |
| Riapro | ogni riga porta «già nascosta», e la proposta torna **la vIPI**, cioè chi le ha nascoste |
| Premo di nuovo senza toccare niente | «**0** sections changed» — prima del rimedio erano 22, cioè il ribaltamento |

⚠️ **Trappola dell'attrezzo, pagata due volte**: sostituendo la copia del `vipi.db` **il `-wal` vecchio va
cancellato**. Lasciato lì, SQLite lo riapplica sul file nuovo e il banco «pulito» riparte con lo stato del
giro prima — due misure buttate. E il controllo «il DB del progetto è intatto» **non si fa con `git status`**:
quel file è in `.gitignore`, quindi git tace comunque. Si interroga l'archivio.

## §12 — Invertire l'ordine sposta l'OSPITE, e adesso lo dice (7 settembre 2026) ⛔ SUPERATA dalla §13: la causa è stata tolta

Segnalato dal committente: unito il vSOP di LICA con la sua vIPI **dall'editor del vSOP**, poi **invertito
l'ordine**, poi uscito e rientrato nell'editor del vSOP — «la vIPI non risulta essere lì».

**Non si era perso niente.** L'unione era intatta e il pannello continuava a elencare tutt'e due i documenti,
con la pastiglia «ospite» sulla vIPI. Quel che si era spostato era il **posto di lavoro**: l'editor unito
vive all'indirizzo dell'**ospite** (`UnionMembersEditor` si monta solo se `_altri.Count > 0`, e quella lista
è vuota per chi ospite non è), quindi il corpo della vIPI aveva smesso di comparire nella pagina del vSOP ed
era ricomparso in quella dell'aeroporto. **Riprodotto a schermo** su LIMS: prima dell'inversione
`section.union-part` c'era, dopo no.

⚠️ **Il difetto vero era il silenzio.** Il primo dell'elenco è l'ospite, e le due frecce lo spostano come
qualunque altro membro: un gesto da due pixel cambia dove vivono la pagina unita, l'editor e il pannello di
pubblicazione, e nessuno lo diceva né prima né dopo.

**Il rimedio, due righe e un link:**

- chi **non** è l'ospite ora lo legge in testa al pannello, col **link all'editor dell'ospite**
  (`IDocKindRoutes.EditorUrl`, ⚠️ con l'ACC **dell'ospite**: due documenti uniti possono stare su ACC
  diversi);
- sotto l'elenco, in modifica: «⚠️ il primo dell'elenco è l'ospite: spostandolo si sposta anche la pagina
  unita, l'editor unito e il pannello di pubblicazione».

⚠️ E la **scheda delle sezioni in comune** ora si offre **solo all'ospite**: scrive sui documenti degli altri
membri, e i loro lock li tiene l'editor dell'ospite — altrove cadrebbe con «bloccato da un altro redattore».

🔴 **Non si redirige, ed è la stessa decisione di sempre**: gli editor e le anteprime restano dove sono
(§4). Un membro si redige da casa sua; quel che si sposta è l'editor **unito**.

Reti: due guardie sul sorgente del pannello in `DocumentiUnitiTests` — il rimando c'è ed è un link, e la
scheda sta dentro `@if (IsEditing && SonoOspite)`.

## §13 — La PORTA decide l'ordine: niente più ospite (10 settembre 2026) ✅

**Chiesto dal committente.** Unendo la vIPI d'aeroporto, il vSOP e l'APP dello stesso scalo, l'ordine dei
documenti nella pagina non deve essere uno solo: deve dipendere **da dove si è entrati**.

| Entri da | La pagina mostra |
|---|---|
| vIPI d'aeroporto | vIPI · vSOP · APP |
| vSOP | vSOP · vIPI · APP |
| APP | APP · vIPI · vSOP |

**La regola, in una riga:** il primo è **il documento della porta**; gli altri seguono nell'**ordine
memorizzato**, senza di lui. Le due frecce ↑↓ continuano a decidere quell'ordine — è la scelta del
committente fra ordine memorizzato e priorità cablata per famiglia, e regge anche i **DUE APP di LIBV**,
dove una priorità per famiglia non saprebbe quale mettere prima.

### Che cosa muore

Il concetto di **ospite** esce dal disegno. Non si sposta: **muore**, perché non aveva altri clienti.

- **§4, il redirect**: cancellato. `UnionLoader.IndirizzoDellOspiteAsync` non esiste più, e con lui se ne
  vanno due problemi che erano **suoi** e non del dominio: la guardia «l'ospite deve avere qualcosa in
  pubblico» (un APP pubblicato spariva dal web sotto una vIPI in bozza) e l'ACC dell'ospite (due membri su
  ACC diversi davano un indirizzo che non esiste). Chi entra da una porta **resta a quella porta**.
- **§12, l'avviso «non sei l'ospite»**: cancellato insieme alla causa. Le frecce non spostano più il posto
  di lavoro — spostano solo l'ordine dei **secondi** — quindi non c'è più niente da avvisare. Via
  `Union_NotHost`, `Union_OpenHost`, `Union_Host`, `Union_HostHint`, `Union_FirstIsHost`.
- **`UnionView.Host` / `IsHostDocument` / `IsHostTarget` / `UnionMemberView.IsHost`**: cancellati.
  ⚠️ Verificato prima di toglierli che **nessuno** li chiamasse fuori dal disegno: `ReleaseService` accoppia
  la pubblicazione dentro `PublishAsync` e non ha mai saputo che cosa fosse un ospite.

### Che cosa nasce, ed è poco

`UnionView.Di(type, key)` — il membro di questa famiglia **e** questa chiave. ⚠️ **Tutte e due**, per la
stessa ragione per cui le voleva `IsHostTarget`: un aeroporto e il suo vSOP militare hanno la **stessa**
chiave di release (l'ICAO) e si distinguono solo per il tipo.

`UnionLoader.AltriMembriAsync` **non cambia di una riga**: escludeva già l'id che le si passa e teneva
l'ordine memorizzato. Le pagine le davano `unione.Host.DocumentId`; adesso le danno **il proprio**. La
funzione che serviva c'era già — vedi [[gesto-piu-corto]].

### Le conseguenze, dichiarate

- ⚠️ **L'URL pubblica non è più unica.** Lo stesso contenuto vive a N indirizzi, in N ordini. È un attrezzo
  interno e non un sito da indicizzare; se un giorno servisse, il `<link rel="canonical">` va al **primo
  memorizzato**, che è l'unico ordine che non dipende da chi guarda.
- ⚠️ **Il costo si sposta.** Prima un membro non-ospite costava una 302; adesso ogni porta carica **N**
  documenti in sequenza. Su LIBV sono quattro documenti su quattro indirizzi. È il costo che la pagina
  dell'ospite paga da sempre — cambia che ora lo pagano tutte.
- ⚠️ **L'editor ha N porte invece di una.** Il lock si prende su **tutti** i membri da qualunque porta: il
  primo che entra li prende, il secondo legge «tenuto da Tizio». La meccanica è quella del §5b, invariata —
  cambia solo da quanti indirizzi la si può avviare.
- ✅ **Ricerca, «Novità» e impatti non si toccano**, e adesso è vero due volte: producono l'URL del membro
  con `DocRoutes`, e quell'URL disegna l'unione con **lui** primo. Prima ci arrivavano per rimbalzo.
- ✅ **La scheda delle sezioni in comune** si offre da **ogni** porta. Non cambia che cosa scrive: «chi
  tiene» è **stato memorizzato**, non dipende da dove si guarda. Cambia solo in che punto della pagina
  compare la sezione tenuta.

Reti: `RimandoAllOspiteTests` **rinominato** in `OgniPortaDisegnaLUnioneTests` — non ci si aspetta più un
rimando, ci si aspetta che **nessuna** porta rimandi e che ognuna metta sé stessa in testa.

## Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` verde sui **due TFM**, 0 avvisi.
- `dotnet test` verde **contando i progetti**, non dall'exit code.
- Verifica live (skill `verifica-live`, copia del DB, porta libera): unione su **LIBA**; pubblicazione
  dall'unione → **due** `DocReleases` con lo stesso ciclo **e** la stessa data efficace; poi una
  **pianificata**; poi **annullarla**; poi **LIMN** (misto e pubblicato); poi **sciogliere**.
- ⚠️ Il caso di prova si sceglie **misto e PUBBLICATO**: corto e in bozza nasconde i difetti che contano.
