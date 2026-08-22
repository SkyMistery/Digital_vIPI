# Incarichi — cosa sono davvero (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/services/vsop/admin/tasks` e `/services/vsop/tasks`. Prima carta del giro: **la
> sostanza**. La forma sta nella gemella [`2026-08-22-incarichi-densita-ui.md`](2026-08-22-incarichi-densita-ui.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## La domanda delle due pagine

Admin: «**chi sta facendo cosa, e cosa è in ritardo**». Utente: «**cosa devo fare io**».

Quattordicesima e quindicesima pagina del ramo, e **sesta volta di fila** che sotto la densità c'è un difetto
di sostanza. Qui non è uno: sono **dodici** — dieci trovati leggendo il codice, due (N11 e N12) trovati
**guardando la pagina**, e il secondo dei due è il peggiore di tutti.

## Cosa ho trovato

### ⚠️ N1 — Si crea un incarico assegnato a NESSUNO

`AdminTasksPage` nasce con `_assignee = 0`, e l'opzione «Seleziona» **vale `0`**. Né la pagina né
`EditorTaskService.CreateAsync` (che valida solo il titolo) impediscono di premere «Crea» così: nasce un
`EditorTask` con `AssigneeUserId = 0`.

Quell'incarico **non è di nessuno**: non compare in `ListMineAsync` di nessun utente (nessuno ha VID 0),
si vede solo nell'elenco admin — e non è riassegnabile, perché la riassegnazione non esiste in UI (N2).
L'unica uscita è cancellarlo e rifarlo.

Cura: guardia nel service (`AssigneeUserId <= 0` ⇒ `ValidationException`), tasto spento finché non si sceglie
una persona, e **il perché accanto al tasto** (regola 170). La guardia sta nel service perché la porta non può
essere più stretta della serratura (regola 166) — e nemmeno più larga.

### ⚠️ N2 — `AssignAsync` esiste, è autorizzato, e non lo chiama nessuno

`IEditorTaskService.AssignAsync` («Riassegna, solo admin») è implementato, ha la sua `EnsureAdmin`, e in tutto
il repository **non ha un solo chiamante**: né UI né test. Un incarico dato alla persona sbagliata si può solo
eliminare e ricreare — perdendo la data di creazione, lo stato raggiunto e (dopo N7) la sua storia nel registro.

È il gemello del difetto «un valore d'enum che nessuno scrive» del giro Audit (`HierarchyChange`): un pezzo di
contratto che sembra funzionalità e non lo è. Cura: la riassegnazione va **nel pannello di destra** della
pagina admin, sull'incarico scelto.

### ⚠️ N3 — «Apri documento» non apre il documento, per 3 tipi su 4

`TaskDocLink.LinkFor` costruisce il link vero **solo** per `AccVipi`; per `Airport`, `App` e `Vloa`
restituisce `/services/vsop/versions`. Il tasto dice «Apri documento» e porta a un elenco.

La ragione è che la chiave di release non porta l'ACC — ma l'ACC l'applicazione lo sa già:
`IReleaseRepository.GetAuthAccCodeAsync(type, key)` è **la stessa domanda** che il service fa in
`EnsureCanEditTargetAsync` per autorizzare. E l'URL lo sa costruire `IDocRoutesRegistry.For(target).EditorUrl(…)`,
che `VersioniPage` usa già.

`TaskDocLink` è quindi un **secondo formattatore per lo stesso dato**, rotto in tre casi su quattro
(regola 139). Cura: un read-model di pagina (regola 167) risolve i bersagli **una volta per pagina** — non una
query per riga (regola 136) — e `TaskDocLink` si **sostituisce**, non si cancella.

### N4 — Il link al documento manca dove serve di più

La card utente ha il tasto (rotto, N3). La **tabella admin** ha solo l'etichetta di testo: chi assegna e
controlla non ha modo di aprire il documento di cui parla la riga. Quello che si fa in una pagina si fa
anche nell'altra (regola 68).

### ⚠️ N5 — Elimina: due politiche per lo stesso atto

Admin elimina con `InlineConfirm`. La pagina utente elimina **al primo clic**, da un cestino in mezzo a
quattro tasti di avanzamento larghi uguali. Stesso atto irreversibile, due politiche — e quella senza conferma
è nella pagina dove i tasti sono più fitti.

### ⚠️ N6 — L'elenco si muove sotto le mani

`ListAllAsync`/`ListByAssigneeAsync` ordinano per `Status == Done`, poi `UpdatedUtc` **discendente**.
`UpdateStatusAsync` riscrive `UpdatedUtc`. Quindi: si cambia lo stato di una riga in mezzo alla tabella, la
pagina ricarica, e **quella riga salta in cima** mentre sotto il puntatore ne arriva un'altra. Con una
`<select>` che scrive subito, senza conferma e senza undo, il clic successivo finisce sulla riga sbagliata.

Cura: ordine **stabile** e leggibile (in ritardo → priorità → scadenza AIRAC → titolo), che non dipende
dall'ultimo tocco; la riga appena cambiata resta dov'è, evidenziata.

⚠️ E il non-evento non si scrive (regola 138): rimettere lo stato che c'è già oggi riscrive `UpdatedUtc`,
riordina l'elenco, e dopo N7 lascerebbe una riga di registro che dice «non è cambiato niente».

### ⚠️ N7 — Nessuno sa chi ha fatto cosa

Tre dati esistono dal primo giorno e **non li legge nessuno**: `CreatedByUserId`, `UpdatedUtc`, `CompletedUtc`
(regola 146 — un campo che nessuno legge non è una traccia). Chi riceve un incarico non vede da chi, chi
controlla non vede quando è stato completato. I VID che si vedono sono nudi, senza nome (regola 124).

E fuori dalla pagina non resta niente: **l'audit non registra gli incarichi**. Assegnare lavoro, cambiarne lo
stato ed eliminarlo sono, dopo il giro Sorgenti, gli ultimi atti amministrativi muti.

**Decisione (committente, 22 agosto): si registra TUTTO** — creazione, cambio di stato, riassegnazione,
eliminazione. Forma:

| Atto | `EntityType` / `EntityId` | Azione | Dettagli |
|---|---|---|---|
| Crea | `EditorTask` / id | `Create` | `Title`, `AssigneeUserId`, `AssigneeName`, `Priority`, `Due`, `Target` |
| Cambia stato | `EditorTask` / id | `Update` | `Title`, `Da`, `A` (stati) |
| Riassegna | `EditorTask` / id | `Update` | `Title`, `DaUserId`/`DaNome`, `AUserId`/`ANome` |
| Elimina | `EditorTask` / id | `Delete` | `Title`, `AssigneeUserId`, `AssigneeName`, `Stato` |

Vincoli che il giro Audit ha già pagato:

- si scrive da **`AuditScribe`**, nella stessa transazione dell'atto, e **prima** della cancellazione, quando
  il titolo è ancora leggibile (regola 136: il nome accanto all'Id — un incarico eliminato non ha più nessun
  posto da cui recuperare il titolo);
- **il non-evento non si scrive** (regola 138): stesso stato ⇒ nessuna riga e nessun `UpdatedUtc` riscritto;
- la frase la fa `AuditNarrator`, **un formattatore solo** (regola 139): famiglia nuova `Incarico`, chip nella
  pagina Audit, pill blu (rossa sull'eliminazione);
- ⚠️ **è la famiglia più prolifica del registro**: un incarico attraversa quattro stati, e il registro cresce
  per sempre (regola 133). Il chip per famiglia serve proprio a poterla escludere; la misura della pagina
  Audit va **rifatta** con gli incarichi dentro.

### ⚠️ N8 — Errori in italiano fisso, e una voce di menu non tradotta

I messaggi che l'utente legge quando qualcosa va storto sono stringhe italiane nel service
(«Titolo obbligatorio.», «Solo un admin può assegnare incarichi ad altri.», «Puoi aggiornare solo i tuoi
incarichi.»): in pagina inglese si leggono in italiano. È la localizzazione a metà di Diagnostica
(regola 160), qui su tutte le vie d'errore.

E in `SopLayout` la voce di topbar è **scritta a mano in italiano** —
`<a href="/services/vsop/tasks" title="I miei incarichi">… Incarichi</a>` — unica accanto a `L["Chrome_Editor"]`.
Chiavi nuove sempre IT+EN, nello stesso giro (regola 43).

### N9 — Zero test

In tutta la suite `EditorTask` compare **una** volta, in `IndexedStringLengthTests` (lunghezza delle colonne
indicizzate). Le tre regole d'autorizzazione scritte nei commenti del service — un non-admin assegna solo a
sé, aggiorna solo i propri, elimina solo quelli che ha creato — non sono provate da niente. I test vanno
scritti **prima** di toccare il service (test-first sul cuore, FEATURE-PROCESS).

### N10 — `ListMineAsync` con `?? 0`

`var uid = _authz.CurrentUserId ?? 0` elenca, senza identità, gli incarichi del VID 0 — che con N1 possono
esistere davvero. Le pagine schermano, ma la regola sta nel service: senza identità l'elenco è **vuoto**.

### ⚠️ N11 — Il sottotitolo prometteva un gesto che non esiste

`Tasks_Subtitle` diceva «Ciclo AIRAC corrente {0} · **trascina lo stato per aggiornare l'avanzamento**». Il
trascinamento non è mai esistito: lo stato si è sempre cambiato coi tasti della scheda. Sesta pagina del ramo
in cui la prosa promette ciò che il codice non fa — e questa mandava a cercare un gesto che non c'è, che è
peggio di non dire niente.

### ⚠️ N12 — La chiave che si sceglie non è la chiave che ritrova (trovato guidando)

Il difetto peggiore del giro, e **nessuna asserzione lo cercava**: si è visto sullo schermo, come una riga che
diceva «il documento collegato non esiste più» su un documento che era lì.

`AdminTasksPage.DocKey` fabbricava la chiave del bersaglio: per la vIPI ACC scriveva `$"{acc}|"` — l'ACC più
una barra, col «root primario» lasciato vuoto. La chiave vera, quella che scrive `AccVipiReleaseTarget`, è
`{acc}|{callsign del settore primario}`: **verificato sui dati veri**, `LIBB|LIBB_ES_CTR`, non `LIBB|`.

Finché il link si costruiva a mano (N3) il difetto era invisibile: `TaskDocLink` non consultava nessun elenco,
spezzava la stringa sulla barra, prendeva l'ACC e componeva `/services/vsop/{acc}/editor`. Funzionava **per caso**.
Appena il link ha cominciato a risolversi contro l'elenco vero, la chiave non ha più combaciato con niente.

⚠️ La cura non è correggere la formula: è **togliere la formula**. Le chiavi vengono da chi le possiede
(`IEditorTaskLinksService.OpzioniAsync`), cioè dallo stesso elenco che poi le ritrova — chi sceglie e chi
cerca non possono leggere due posti diversi. È la regola 143 («un gate per categoria, non uno per chiamante»)
e la 163 («due porte che creano la stessa cosa hanno due politiche») nella loro terza forma.

E guidando la stessa tendina è saltata fuori una seconda cosa: fra le opzioni compariva **`Airport:` con la
chiave vuota**. Un incarico creato su quella non si sarebbe risolto mai — un collegamento che nasce già rotto.
Una tendina è una comodità, e una comodità non deve mentire.

## Fuori ambito, dichiarato

- **Nessuna archiviazione** degli incarichi conclusi: la crescita senza tetto si chiude col filtro per stato
  (default «non conclusi») nella carta gemella. Una colonna nuova è una migrazione sullo schema del
  committente, e il ramo ha già il deploy fermo sulla conversione MariaDB: si rivede dopo il cutover.
- **Nessun badge** col numero di incarichi aperti sul tasto in topbar: sarebbe una query su **ogni** pagina,
  sullo stesso `DbContext` di circuito (regola 126).
- Un incarico che l'admin assegna a chi **non ha il grant** per quel documento resta possibile (la guardia
  `EnsureCanEditTargetAsync` vale per i non-admin): è una scelta, non un difetto — un admin assegna a chi
  vuole. Non si aggiunge un divieto.

## Slice — tutte fatte

1. **Test-first**: caratterizzazione di `EditorTaskService` (autorizzazioni, ritardo, cicli AIRAC). 24 test.
2. N1 + N10: guardia sull'assegnatario, elenco vuoto senza identità, tasto spento col perché accanto.
3. N6: ordine stabile nel repository, non-evento senza scrittura. 11 test su DB vero.
4. N7: `AuditScribe` sui quattro atti + famiglia `Incarico` in `AuditNarrator` + chip nella pagina Audit.
5. N3 + N4: read-model dei bersagli, `TaskDocLink` **sostituito**, link in entrambe le pagine.
6. N5: conferma sull'eliminazione anche nella pagina utente.
7. N8 + N11: chiavi IT+EN per gli errori del service e per la voce di topbar; sottotitolo che dice il vero.
8. N2: riassegnazione nel pannello, che la carta gemella crea.
9. N12: le chiavi vengono dall'elenco che le ritrova; chiavi vuote e documenti nascosti fuori dalle opzioni.

## Cosa lascia questo giro

⚠️ **Un difetto invisibile può essere tenuto in vita da un secondo difetto.** N12 esisteva dal primo giorno e
non si vedeva perché N3 lo copriva: il link sbagliato non consultava l'elenco, quindi la chiave sbagliata non
incontrava mai la chiave giusta. **Riparare una cosa ne scopre un'altra**, e la seconda va cercata guidando,
non dedotta — la carta iniziale non poteva contenerla.

⚠️ **La `ValidationException` porta ora una chiave** accanto al messaggio grezzo, e `ServiceErrorNarrator` la
traduce (terzo narratore della famiglia, dopo quello degli eventi e quello dei rilievi). È **facoltativa**:
gli altri service la prendono quando qualcuno li tocca. Non è un cantiere aperto, è un posto pronto.

⚠️ **L'audit ha una famiglia nuova, ed è la più prolifica che abbia mai avuto**: un incarico attraversa
quattro stati e ogni passaggio è una riga. La misura di `/services/vsop/admin/audit` va rifatta con gli incarichi
dentro — il chip di famiglia esiste proprio per poterla mettere da parte.
