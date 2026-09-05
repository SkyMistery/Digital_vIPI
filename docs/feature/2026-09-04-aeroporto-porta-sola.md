# L'aeroporto entra nella regola: una porta sola per salvare, e i campi vivi solo col lock — 4 settembre 2026

> Richiesta del committente: *«vorrei che il punto di salvataggio dei documenti di aeroporto sia uno, come
> negli altri documenti. E voglio poter scrivere nei campi solo se il documento è in editing e quindi con
> lock attivo. (Ora tipo le SID si possono editare senza prendere il lock, oppure le frequenze hanno un
> loro tasto a parte.)»*

Tre decisioni prese con lui prima di scrivere una riga:

| | Scelta |
|---|---|
| Salvataggio | **A ogni gesto**, una porta sola. Spariscono tutti i tasti «Salva» e i pallini di sporco. |
| Lock | Vincola **solo l'uomo**: gli import di sfondo continuano a scrivere senza chiedere niente. |
| Ambito | Il **Document dell'aeroporto**: due scali restano lavorabili in parallelo da due persone. |

## 0. Il confine che cade

Il 26 agosto 2026 l'editor d'aeroporto ha adottato bozza+lock montando `DocumentSectionsEditor`
(carta `2026-08-26-aeroporto-a-sezioni.md` §1b). Ma il confine fu tracciato a metà, e sta scritto
in `AirportSectionsEditor.razor`:

> *«Sotto lock c'è il DOCUMENTO — sezioni, ordine, nascondi, Live/Frozen, blocchi. I dati strutturati
> (regole, TA/TL, piste, SID, link, limiti settori) restano a salvataggio diretto ACC-gated: li scrivono
> anche i servizi d'import che girano in background, e quelli un lock non possono prenderlo.»*

La premessa è vera, la conclusione no. **Che i job non possano prendere un lock non dice niente su che cosa
debba fare una persona**: sono due porte diverse, e i job passano dal repository, non dal service che usa
l'editor. Il confine è caduto qui.

## 1. Che cosa c'era, contato

Nove tasti «Salva» dove gli altri quattro editor ne hanno **zero**:

| Dove | Tasto | Chiave |
|---|---|---|
| Quote di transizione | Salva TA | `Ape_SaveTa` |
| Quote di transizione | Salva tabella | `Ape_SaveTable` |
| Piste | Salva piste | `Ape_SaveRunways` |
| Regole piste | Salva regole | `Ape_SaveRules` |
| Frequenze | Salva link | `Ape_SaveLinks` |
| SID manuali | Salva SID manuali | `Ape_SaveManualSids` |
| SID importate | Salva modificate | `Ape_SidSaveDirty` |
| Settori ATC | ✓ per riga | `AccAdmin_SaveLimits` |
| Testata | Salva tutto (N) + Ctrl+S | `Ape_SaveAllN` |

E tre buffer che il server non ha: `_dirtySections`, `_dirtyLimiti`, `_sidNonSalvate`. La verifica del
30 agosto (`lavori-aperti.md` §AD) li trovò già; il rimedio fu una toppa — salvare all'uscita, più una
guardia `beforeunload` — non la rimozione della causa. `DocumentSectionsEditor.IsSectionDirty` esiste
**solo** per questo editor, e per lo stesso motivo.

⚠️ E il cancello dei campi non era il lock: era `_canEdit`, cioè il **ruolo** sulla ACC. Il lock disabilitava
il tasto Salva; i campi restavano vivi. Si poteva digitare in tutta la pagina senza aver premuto ✎Modifica.

## 2. Il modello da copiare — esiste già, in tre editor

Da `VloaEditor`/`AppSectionsEditor`/`AccEditorPage`, senza inventare niente:

1. **La porta unica è `DocumentEditorShell.GuardAsync`**: badge Saving/Saved, tornello `InFilaAsync` che
   serializza i gesti, scala di `catch` che traduce `EditConflictException`/`ValidationException` in un
   avviso a schermo. Ogni salvataggio è `_shell.GuardAsync(async () => { … })`. Niente debounce.
2. **Il cancello è un `bool` solo**, `_shell.IsEditing` (lock mio + bozza aperta), passato ai componenti come
   `Editing` e consumato come **ramo di render**, non come `disabled`:
   `@if (Editing) { …input… } else { …lettura… }`.

I service prendono già la collezione intera (`SaveRunwaysAsync(icao, rows)`): «salva a ogni gesto» vuol dire
riscrivere la collezione della sezione a ogni gesto. **Nessuna firma nuova.**

## 3. La guardia vera sta nel service, non nel bottone

Un tasto `disabled` non è una guardia — la regola è di `2026-08-21-versioni-lock-e-azioni.md`. La garanzia
entra **nella porta che c'è già**, `AirportEditingService.EnsureCanEditAsync`, che da «ruolo» diventa
«ruolo **+** lock». Tre livelli, non uno:

| Chi scrive | Che cosa gli si chiede |
|---|---|
| Una persona dall'editor (TA, TL, piste, regole, SID, SID importata, link frequenze; limiti/nascondi/principale/ACC dei settori) | **Il lock è mio** |
| Un comando in blocco dall'admin (`ReimportFromSourceAsync`, import settori) | **Nessun altro tiene il lock** — la pagina admin lavora su N scali e il lock del singolo documento non ce l'ha (`AeroportiPage`) |
| Un job di sfondo (`AirportDataImportUseCase`, `SidImporter`, hosted service, `StructureEditingService`) | **Niente**: passano da `EfAirportRepository`/`IAirportSectorImporter`, che non si toccano |

⚠️ **Qual è «il documento dello scalo»**: `Airport.DocumentId` se c'è, **altrimenti quello militare**. Non è
un dettaglio: su un campo solo militare **senza vIPI civile** i dati dello scalo si scrivono dall'editor
vSOP (`MilSectionsEditor.ScaloSenzaCivile`), che tiene il lock del *suo* documento. I due id li dà già
`GetMilitaryStateAsync` → `AirportMilitaryState(…, DocumentId, MilDocumentId)`: con questa regola l'editor
militare passa senza plumbing aggiuntivo, e senza di essa si sarebbe rotto in silenzio.

`EnsureDocumentAsync` resta libero: lo chiama l'apertura dell'editor, prima che un lock esista.

## 4. L'editor militare, nello stesso giro

`MilSectionsEditor` monta gli **stessi tre** sotto-editori (quote di transizione, piste, frequenze) e ha i
suoi buffer. Cambiando i componenti condivisi deve seguire: stessa porta unica, `Editing="_shell.IsEditing"`
al posto di `CanEdit="_canEditScalo"`. Il suo commento — *«la scrittura segue il ruolo e non il lock, sono
due cose che si possiedono separatamente»* — diventa falso con la guardia lato server, e va riscritto nello
stesso giro (gate #4 di `FEATURE-PROCESS.md`).

## 5. Il compromesso, dichiarato

Con «salva a ogni gesto» una riga **incompleta non si salva**: le validazioni alzano `ValidationException`,
il guard la mostra come avviso, il database resta all'ultimo stato valido. Chi esce lasciando una riga a metà
la perde.

È il prezzo della porta unica, ed è ciò che fanno già gli altri quattro editor. In cambio sparisce l'unico
caso in cui oggi si perde lavoro **valido e già digitato** — che è il difetto vero.

## 6. Le fette

| # | Che cosa |
|---|---|
| S1 | I sotto-editori passano da `CanEdit` a `Editing` e ramificano lettura/scrittura (meccanico) |
| S2 | Salvataggio a ogni gesto nell'aeroporto: un metodo per sezione dentro `_shell.GuardAsync`; via buffer e tasti |
| S3 | Via la guardia `beforeunload`, Ctrl+S e `IsSectionDirty` (unico consumatore) |
| S4 | Stesso giro sull'editor militare |
| S5 | La guardia vera nel service (+ «non bloccato da un altro» per i reimport) |
| S6 | Chiavi i18n morte fuori dai due `.resx` |
| S7 | Propagazione: commenti, carte del 26 e del 21 agosto, `lavori-aperti.md`, memorie |

## 7. Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` e `dotnet test`.
- Test nuovi sul modello di `DocumentAdminLockGuardTests`: scrittura d'aeroporto **senza** lock e **col lock
  di un altro** devono fallire, col lock mio deve passare; `ReimportFromSourceAsync` deve passare senza lock
  e fallire col lock altrui.
- **Dal vivo**, che è dove si vedono le regressioni di binding Blazor: editor senza ✎Modifica → i campi sono
  testo; con Modifica → input; cambia frequenza, pista, SID, limite di settore → «Salvato» a ogni gesto e i
  valori reggono al ricarico; seconda sessione → chip rosso col nome; «Fine modifica» esce senza chiedere
  niente; Ctrl+S non fa più niente e chiudere la scheda non chiede conferma.
- Un campo solo militare senza vIPI civile, dall'editor vSOP: stessi gesti, stesso esito.

## 8. Le code del 5 settembre

✅ **Fuso in `main`** (`8b3130ad`), ramo cancellato. Build Release `no-incremental` e 5.423 test verdi sul
merge. ✅ **In produzione dal 5 settembre 2026** con il pacchetto **1.10.0** (`1.10.0 · 99f33f0`).

**La verifica live è stata fatta** (LIBD, copia del DB): senza lock zero campi e zero tasti «Salva»; col lock
TORA, una SID manuale nuova e un limite di settore **riletti dal database** dopo il ricarico; Ctrl+S inerte,
uscire non chiede niente. ⚠️ Due «KO» erano della **sonda**: contare gli `<input>` non dice se sono spenti, e
`innerText` non contiene il `value` di un campo.

⚠️ **E un difetto che questa carta ha prodotto**, corretto il giorno dopo (`lavori-aperti.md` §BP): la nota
«quota da concordare con l'APP» **scavalcava la colonna Cat.** nella tabella delle SID importate.
`td.col-climb` e `td.col-cond` non erano fra le celle che si tagliano — fino a ieri contenevano **solo un
campo**, largo per costruzione. *Quando una cella comincia a portare testo dove prima portava un campo, le
regole scritte per il campo vanno rilette.* Nello stesso giro la frase è stata abbreviata («to coord with
APP») in editor **e** lettore, e messa in **una costante sola**
(`AirportSidDerivationService.NotaClimbApp`): prima erano due stesure, e infatti se ne accorciò una sola.
