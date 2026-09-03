# Documenti uniti — una pagina, un editor, una pubblicazione — carta (3 settembre 2026)

> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Ramo `documenti-uniti`, da `main` (`cd1bc5c7`).
> Stato: 🟡 **in esecuzione** — §1 chiusa.

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

## §2 — I corpi dei viewer diventano componenti ⏳

Da `AppnPage`, `AeroportoPage` e `MilDocumentPage` si estrae il corpo in `Components/Doc/*DocumentBody.razor`.
Meccanico: la pagina monta il componente e la resa non cambia di una virgola.

## §3 — La pagina unita ⏳

Indici impilati (uno per membro, con `HeaderLabel`), corpi in ordine, intestazione per membro, **un solo**
`PrintMeta`, tre colonne (`DueColonneSuOgniDocumentoTests`).
⚠️ Le sezioni con la **stessa chiave** nei due documenti restano **tutte e due**, distinte dal gruppo: chi non
le vuole le nasconde (`DocumentSection.IsHidden` esiste già). Decisione del committente.
⚠️ `?as=rel:{id}` nomina **una** release, quella dell'ospite: gli altri membri mostrano la **propria** release
dello **stesso ciclo**. E il degrado di un'anteprima non autorizzata deve restare quello di oggi — pubblica
**con `_useFrozen = true`**, o il congelamento AIRAC si aggira dall'indirizzo.

## §4 — Il redirect ⏳

La vista **pubblica** di un membro non-ospite rimanda alla pagina unita, ancorata al suo gruppo. Precedente
esatto: `ReleasePreviewPage` — `NavigateTo(url, replace: true)` **senza `@rendermode`**, così diventa una vera
302 che il browser segue prima di disegnare.
⚠️ **Solo la vista pubblica**: editor e anteprime `?as=` di ogni membro restano al loro indirizzo.

## §5 — I corpi degli editor, e l'editor unico ⏳

Stessa estrazione per gli editor, e poi una istanza per membro — **il pattern della vIPI ACC**.
⚠️ Ogni componente-membro è `OwningComponentBase` con il **proprio scope**: gli editor scrivono, e prenderli
da `@inject` significa prenderli dal `DbContext` **del circuito** (il difetto già pagato tre volte).
⚠️ **Il lock si prende su TUTTI i membri in un gesto**: se anche uno solo è tenuto da un altro, non se ne
prende **nessuno** e la pagina dice chi lo tiene. Mezzo lock preso è peggio di nessun lock.

## §6 — La pubblicazione accoppiata ⏳

`PublishUnionAsync(unionId, cycle, note)` e `PublishUnionNowAsync(unionId, note)`: **un giro sopra** le porte
che ci sono, non un secondo motore.

- ⚠️ **Tutto in `IUnitOfWork.ExecuteInTransactionAsync`, la pianificata compresa.** Oggi la transazione
  avvolge solo `PublishNowAsync`, e basta perché il bersaglio è uno. Con N bersagli no:
  `SaveReleaseAsync` fa `SaveChangesAsync` per chiamata e `VersionNumber` è `max+1` **letto in memoria** sotto
  un **indice unico** — un secondo membro che collide lascerebbe il primo pubblicato da solo.
- ⚠️ **`ShapeReleaseContext.Capturing` NON è annidabile** (`Dispose` azzera): i membri si catturano **in
  sequenza**. Stessa cosa per `ReadingLanguageContext.Rendering`, che va aperto con la lingua **sorgente di
  quel membro**.
- ⚠️ Le **due semantiche restano diverse**: la pianificata non promuove la bozza, la «pubblica ora» sì.
- **Annullare si accoppia come pubblicare**: `CancelReleaseAsync` su una release di un membro annulla anche le
  sorelle dello **stesso ciclo**, e la conferma dice quante ne toglie.

## §7 — Il governo ⏳

La lezione di §V dei vSOP militari — *agganciato al motore di lettura e non a quello di governo* — vale
identica: elenco versioni, eliminazione (che **scioglie** l'unione), ricerca, «Novità», impatti.
⚠️ La verifica si fa col `grep`: contare i lettori di `DocumentUnionMember` contro quelli del legame che imita.

## §8 — La seconda richiesta: vIPI + vSOP sui campi con presenza militare ⏳

È lo **stesso meccanismo**, senza codice in più: su un campo misto si uniscono la vIPI civile e il vSOP, e la
pagina unita legge come un documento completo — cioè come oggi legge il vSOP di un campo *solo* militare, che
di quel campo è l'unico documento. Su LIMN e LIMS i due documenti esistono già.

⚠️ **Le due guardie gemelle §11b non si toccano**: su un campo misto la vIPI civile viene prima del vSOP, su un
campo solo militare la civile non nasce. L'unione **presuppone** che i documenti esistano; non è il posto da
cui cambiare chi può nascere.

## Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` verde sui **due TFM**, 0 avvisi.
- `dotnet test` verde **contando i progetti**, non dall'exit code.
- Verifica live (skill `verifica-live`, copia del DB, porta libera): unione su **LIBA**; pubblicazione
  dall'unione → **due** `DocReleases` con lo stesso ciclo **e** la stessa data efficace; poi una
  **pianificata**; poi **annullarla**; poi **LIMN** (misto e pubblicato); poi **sciogliere**.
- ⚠️ Il caso di prova si sceglie **misto e PUBBLICATO**: corto e in bozza nasconde i difetti che contano.
