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
⚠️ `?as=rel:{id}` nomina **una** release, quella dell'ospite: gli altri membri mostrano la **propria** release
dello **stesso ciclo**. E il degrado di un'anteprima non autorizzata deve restare quello di oggi — pubblica
**con `_useFrozen = true`**, o il congelamento AIRAC si aggira dall'indirizzo.

## §4 — Il redirect ✅

La vista **pubblica** di un membro non-ospite rimanda alla pagina unita, ancorata al suo gruppo. Precedente
esatto: `ReleasePreviewPage` — `NavigateTo(url, replace: true)` **senza `@rendermode`**, così diventa una vera
302 che il browser segue prima di disegnare.
⚠️ **Solo la vista pubblica**: editor e anteprime `?as=` di ogni membro restano al loro indirizzo.

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
## Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` verde sui **due TFM**, 0 avvisi.
- `dotnet test` verde **contando i progetti**, non dall'exit code.
- Verifica live (skill `verifica-live`, copia del DB, porta libera): unione su **LIBA**; pubblicazione
  dall'unione → **due** `DocReleases` con lo stesso ciclo **e** la stessa data efficace; poi una
  **pianificata**; poi **annullarla**; poi **LIMN** (misto e pubblicato); poi **sciogliere**.
- ⚠️ Il caso di prova si sceglie **misto e PUBBLICATO**: corto e in bozza nasconde i difetti che contano.
