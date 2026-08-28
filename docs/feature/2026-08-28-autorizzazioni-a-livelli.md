# Le autorizzazioni a livelli: un numero al posto di un interruttore (28 agosto 2026)

> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Sostituisce la decisione del 22 agosto sera
> («lo staff di divisione è admin, tutto» — memoria `staff-code-reali`, riflessa in
> `DivisionOptions.AdminRolePatterns`), che resta valida come **storia** e non più come regola.
> **Stato: slice 0 e 1 chiuse (28 agosto 2026, notte).** Il modello esiste ed è provato; niente è ancora
> cablato, il prodotto si comporta esattamente come prima. Ramo `autorizzazioni-a-livelli`, aperto da
> `main` dopo la fusione del glossario di fraseologia.

## 1. Perché

Il prodotto ha **un interruttore solo**: `IEditAuthorizationService.IsAdmin`, chiamato in **160 punti
su 46 file**. O sei admin — e allora vedi e modifichi tutto, dalle sorgenti di import ai permessi degli
altri, passando per le statistiche personali di chiunque — oppure sei un socio qualunque e vedi le pagine
pubbliche. In mezzo non c'è niente.

Sotto l'interruttore c'è un secondo meccanismo, la **concessione per ACC** (`EditGrant`,
`/services/vsop/admin/permissions`): un non-admin con una concessione modifica i documenti di quella ACC.
Nato per dare l'editing a chi non era staff, non è mai stato il meccanismo principale — e in produzione
su `atc.it.ivao.aero` **le concessioni sono già state cancellate tutte** dal committente.

Il risultato è che oggi, in produzione, valgono queste due regole e nient'altro:

| chi | oggi |
|---|---|
| `^IT-[A-Z0-9]+$` — **tutto** lo staff di divisione, jolly | admin pieno |
| `^LI[A-Z0-9]+-(CH\|ACH)$` — i chief e vice-chief d'ACC | admin pieno |
| tutti gli altri, staff IVAO di altre divisioni compreso | solo pagine pubbliche |

Due cose non vanno. La prima: un `IT-T01` (staff tecnico) e un `IT-FOC` (Flight Operations) possono
cancellare un documento e ridistribuire i permessi, che non è il loro mestiere. La seconda, opposta:
**non c'è nessun modo di dare a una persona meno di tutto** — per esempio le sole statistiche di
divisione, che è la cosa che allo staff serve più spesso.

## 2. Cosa cambia, in una riga

L'autorizzazione smette di essere un booleano e diventa **un numero ordinato**: cinque livelli cumulativi,
un confronto `>=` a ogni cancello.

## 3. Le decisioni del committente (28 agosto 2026, notte)

| | scelta |
|---|---|
| **Livelli** | cinque, **cumulativi**: chi sta sopra ha tutte le prerogative di chi sta sotto |
| **Editor** | edita **tutto**, non solo la sua ACC — «il CH di Roma può dare una mano a quello di Milano» |
| **Concessioni per ACC** | **eliminate**, entità compresa (in produzione erano già cancellate) |
| **Statistiche personali altrui** | le vede **tutto lo staff italiano**, non i soli admin |
| **`IT-WM`** | admin, come `IT-DIR` |
| **Fondatore** | Admin sempre, per VID, indipendentemente dalla posizione staff; in `appsettings.json` |

I cinque livelli:

| n | livello | chi è | cosa apre in più |
|---|---|---|---|
| 0 | **User** | chiunque, anche anonimo | le pagine e i documenti pubblici |
| 1 | **IvaoStaff** | ha una posizione staff IVAO, **di qualunque divisione** | *niente, per ora* |
| 2 | **DivisionStaff** | posizione `IT-…` | statistiche di divisione + statistiche personali di chiunque |
| 3 | **Editor** | chief e vice-chief d'ACC italiani | struttura, ACC, aeroporti, confinanti, trasferimenti, documenti, «Da sistemare», traduzioni, glossario |
| 4 | **Admin** | otto codici di direzione + i fondatori | sorgenti, incarichi, audit, diagnostica, permessi |

⚠️ **Il livello 1 oggi non fa niente**, ed è voluto: è un'etichetta, non un permesso. Serve perché uno
staffista di un'altra divisione **si distingua** nell'elenco dei permessi, così che promuoverlo a mano sia
una scelta e non una scoperta. Il giorno che qualcosa gli si vuole aprire, il livello c'è già.

⚠️ **I chief sono anche membri della divisione italiana**, quindi un `LIRR-CH` deve vedere le statistiche
di divisione. Non serve scriverlo da nessuna parte: Editor (3) ≥ DivisionStaff (2), e l'ordinamento lo
risolve da solo. È esattamente il motivo per cui i livelli sono **cumulativi** e non un insieme di flag.

## 4. Il modello

```csharp
public enum VipiRole { User = 0, IvaoStaff = 1, DivisionStaff = 2, Editor = 3, Admin = 4 }
```

Un `RoleResolver` **puro** — niente IO, niente DB, tutto testabile per tabella di verità — porta le
posizioni staff al livello:

| esito | regola |
|---|---|
| **Admin** | `^IT-(DIR\|ADIR\|WM\|AWM\|AOC\|AOAC\|SOC\|SOAC)$` |
| **Editor** | `^LI[A-Z0-9]+-(CH\|ACH)$` |
| **DivisionStaff** | `^IT-[A-Z0-9]+$` |
| **IvaoStaff** | almeno una posizione staff, qualunque essa sia |
| **User** | nessuna posizione |

Si valuta dall'alto e vince il primo che risponde: una persona con più posizioni prende **la più alta**.

✅ **`IT-AWM` è dentro**, confermato dal committente il 28 agosto notte: era stato proposto da chi scrive
per simmetria con `ADIR`/`AOAC`/`SOAC`, visto che il committente aveva nominato il solo `IT-WM`.

⚠️ **L'elenco puntuale torna, e con lui torna il suo difetto.** Il 22 agosto il jolly era stato scelto
apposta perché *«un ruolo nuovo della divisione non nasca escluso»*. Con l'elenco puntuale un ruolo di
direzione nuovo — poniamo `IT-ATOC` — nasce **DivisionStaff**, non Admin. È il compromesso accettato: il
danno di un admin di troppo è peggiore di quello di un admin di meno, **ora che esiste la promozione a
mano** che il 22 agosto non c'era. Il difetto non sparisce, si sposta su qualcosa che si ripara in trenta
secondi da dentro il prodotto.

### Il fondatore

`Auth:FounderVids` — elenco di VID che sono Admin comunque. Non è un vezzo: è **l'antidoto al blocco**.
Oggi, se i pattern sbagliassero, «nessuno è admin» sarebbe irreparabile *da dentro* — perché per
assegnare permessi bisogna essere admin. Con un VID nell'`appsettings.json` la porta si riapre sempre.

✅ **Il VID è `704798`**, in `src/Vipi.Host/appsettings.json`, sezione `Auth`, con accanto il commento che
dice perché esiste. Un VID ≤ 0 non vale mai come fondatore: una lista mal configurata non deve poter
promuovere l'anonimo.

## 5. L'override per VID, e il pavimento

Entità nuova, una riga per persona promossa:

```csharp
class RoleOverride { int UserId; VipiRole Level; int GrantedByUserId; DateTime GrantedAtUtc; string? Note; }
```

**La regola è una sola:** `Effettivo = max(DaStaff, Override)`.

Il «non si declassa sotto il livello garantito dalla posizione staff» **non è un controllo**: è ciò che
`max` fa già. Un declassamento sotto il pavimento è un no-op silenzioso — e siccome i no-op silenziosi
sono bugie, la pagina mostra i livelli sotto il pavimento **disabilitati**, con scritto accanto il codice
staff che li garantisce. Il declassamento serve, ma serve solo a **togliere una promozione**.

Due guardie che il `max` non copre:

- **nessuno declassa sé stesso** (è il modo esatto in cui ci si chiude fuori);
- **nessuno declassa un fondatore** (sarebbe comunque un no-op, ma deve dirlo).

Ogni scrittura passa da `AuditLog`, come le altre.

## 6. Il regalo: le concessioni muoiono, e con loro una corsa

Questa è la parte che **toglie** codice invece di aggiungerne, e va detta per prima perché cambia il segno
del lavoro.

Se l'Editor edita tutto, i cinque metodi dell'autorizzazione che oggi interrogano il database —
`CanEditAccAsync`, `CanEditDocumentAsync`, `CanEditAnythingAsync`, `EnsureCanEditAccAsync`,
`EnsureCanEditDocumentAsync` — diventano `Role >= Editor`: **sincroni, zero query**. Spariscono
`EditGrant`, `IEditGrantRepository`, `EfEditGrantRepository`, la tabella e il picker ACC.

E sparisce `HasAnyGrantAsync` chiamato dal layout, che è **la prima query di ogni pagina per un utente
loggato**: cioè la causa prima delle corse sul `DbContext` di circuito documentate nelle memorie
`corse-dbcontext-diagnosi` e `barra-non-affonda-la-pagina`. Non si mitiga: non c'è più.

⚠️ **Ma l'override è in banca dati, e rifarebbe il danno.** Se il livello si risolvesse con una `SELECT`
per richiesta, avremmo tolto una query dal layout per rimetterne un'altra nello stesso posto. Quindi:
la tabella `RoleOverride` — poche decine di righe, sempre — si tiene **intera in memoria** in un servizio
singleton, invalidato alla scrittura. Il livello resta a **zero query per richiesta**, come oggi `IsAdmin`.

## 7. La mappa dei cancelli

| pagine | oggi | domani |
|---|---|---|
| `sector-structure`, `acc`, `airports`, `neighbours`, `transfers`, `pending`, `translations`, `glossary` | Admin | **Editor** |
| `versions` (i documenti) e gli editor (ACC, APP, aeroporto, militare, vLOA) | Chiunque / Admin ∪ concessione | **Editor** |
| `sources`, `tasks`, `audit`, `diagnostics`, `permissions` | Admin | **Admin** |
| `/services/stats/division`, `/stats/user/{vid}`, `/stats/session/{id}`, copertura aeroporti | Admin | **DivisionStaff** |

`AdminNav` ha già l'enum `Chi` accanto a ogni voce: diventa un `VipiRole`, e **resta una riga per voce**.
È il punto in cui questa feature costa poco proprio perché quel componente era stato scritto bene.

⚠️ Non basta cambiare la barra. Una pagina aperta all'Editor il cui **servizio** continua a chiamare
`EnsureAdmin()` mostra il link e poi nega: il cancello va spostato **in tutte e due** le sedi, ed è per
questo che i predicati diventano due (`IsAdmin`, `IsEditor`) e non uno rinominato.

## 8. Pre-flight — le quattro domande

**1. Modello.** Non si affianca niente: `VipiRole` **sostituisce** il booleano, `RoleOverride`
**sostituisce** `EditGrant`. Fra sei mesi «dove si decide chi può cosa» ha una risposta sola,
`RoleResolver` + `RoleOverride`.

**2. Dispatch.** Nessuno `switch (livello)`: l'enum è ordinato apposta perché ogni cancello sia un `>=`.
Un livello nuovo in mezzo si inserisce cambiando i numeri, senza toccare i confronti.

**3. Ingressi e verifica.** L'ingresso è `/services/vsop/admin/permissions`, che esiste già e cambia
contenuto (da «VID + ACC» a «VID → livello»). Niente catch-22: il fondatore è Admin da config, quindi
la prima promozione è sempre possibile anche su un database vuoto. Verifica: si guida il flusso reale con
un utente finto per livello (la skill `verifica-live` sa già fabbricarlo).

**4. Propagazione.** Questa modifica **rimuove**. Vanno nello stesso giro: `EditGrant` e tutto il suo
corredo, `CurrentUser.CanEdit` (già codice morto oggi, nessun uso), `DivisionOptions.AdminRolePatterns`
col suo commento sul jolly, la scheda «Chi può editare» della diagnostica, la Guida in-app, e le memorie
`staff-code-reali` e `staff-roster-design`.

## 9. Le slice

| # | slice | verde a fine slice |
|---|---|---|
| ✅ 0 | fusione del glossario, ramo nuovo | build Release su entrambi i TFM: 0 avvisi |
| ✅ 1 | `VipiRole` + `RoleResolver` puro + test di tabella | **47 test nuovi verdi**, niente cablato |
| 2 | `RoleOverride` + migrazione **doppia** (SQLite e MySql) + cache singleton | test di persistenza |
| 3 | il servizio: `Role`, `IsEditor`, `EnsureAtLeast`; `IsAdmin` **conserva il significato**; muoiono `AdminStaffCodes` e le due liste legacy di `DivisionOptions` | suite verde senza toccare i 160 usi |
| 4 | morte delle concessioni: entità, repo, metodi async → sincroni | suite verde, meno codice |
| 5 | i cancelli: `AdminNav` + le ~30 chiamate che scendono a Editor + le stats a DivisionStaff | test per rotta |
| 6 | `/admin/permissions` riscritta | verifica live |
| 7 | diagnostica, Guida, documenti, memorie | tracciamento coerente |

La slice 3 è la chiave dell'ordine: siccome `IsAdmin` continua a voler dire `Role >= Admin`, **i 160 usi
non si toccano in blocco**. Si toccano solo quelli che devono scendere, nella slice 5, uno a uno e con la
suite a fare da rete.

⚠️ **Due migrazioni, due insiemi.** SQLite e MySql hanno cartelle separate e la stessa migrazione prende
**due identificativi diversi**: è la trappola già presa (`audit-2026-08-25`). Con questa la coda al cutover
MariaDB passa da ventuno a **ventidue**.

## 10. Cosa può andare storto

- **Gente che perde l'editing.** Tutti gli `IT-` fuori dagli otto codici: `IT-T01`, `IT-T03`, `IT-FOC`,
  `IT-FOAC`, `IT-AOA1`… Gli `AOA1`/`AOA2` (assistenti Ops) oggi editano e domani no. È l'effetto voluto,
  ma è la telefonata che arriverà: la risposta è una promozione a mano, trenta secondi.
- **Nessuno perde una concessione**, perché in produzione non ce ne sono più: chi editava lo faceva da
  admin. Il travaso è pulito, ed è il momento giusto per farlo.
- **Un cancello dimenticato in un servizio** mentre la pagina si apre: la difesa è il test per rotta
  della slice 5, che chiede un 403 al livello immediatamente sotto.
- **La cache degli override che non si invalida**: una promozione che «non fa effetto» finché non si
  riavvia. Test dedicato nella slice 2.

## 11. Le due decisioni che mancavano — ✅ chiuse il 28 agosto, notte

- **VID del fondatore: `704798`**, in `appsettings.json`.
- **`IT-AWM` è admin**, dentro l'elenco degli otto.

## 12. Che cosa è entrato con la slice 1

`VipiRole` (in `Vipi.Domain/Enums.cs`, coi valori numerici **espliciti**: finiranno in banca dati) e
`RoleResolver` (in `Vipi.Application/Auth/`), **puro** — niente IO, niente orologio — con 47 test di
tabella di verità. I codici provati sono **quelli veri** osservati ai login del 9 agosto, non esempi
inventati: metà di quei test riguarda gente che esiste.

Quattro cose imparate scrivendolo, che non erano nella carta:

- **I pattern vanno ancorati, e va provato che lo siano.** Senza `^…$` un `IT-DIRETTIVO` inventato
  diventerebbe direttore della divisione. Tre test esistono solo per questo.
- **L'ordine di valutazione è la regola, non i pattern.** Un `IT-DIR` combacia **anche** col pattern dello
  staff di divisione: è il fatto che l'admin si valuti per primo a renderlo admin. Un ordine sbagliato
  declasserebbe la direzione in silenzio, e i pattern sembrerebbero giusti.
- **L'ordine dell'enum è un contratto**, e ha un test suo. Se qualcuno rinumerasse, ogni `Role >= X` del
  prodotto resterebbe compilabile cambiando significato.
- **Le liste di autorizzazione stanno nella sezione `Auth`, non in `Division`.** `Division` dice *qual è*
  la divisione (codice, prefissi ICAO); `Auth` dice *a chi* quei codici danno un permesso. ⚠️ Per una
  slice le due liste vecchie di `DivisionOptions` (`AdminRolePatterns` col jolly, `AdminAccRolePatterns`)
  **convivono** con le nuove: sono ancora quelle che `AdminStaffCodes` dà all'autorizzazione vera, ed è
  l'unico modo perché la slice 1 non cambi il comportamento del prodotto. **Muoiono nella slice 3**, e se
  sopravvivessero sarebbero esattamente il modello gemello che il pre-flight vieta.
