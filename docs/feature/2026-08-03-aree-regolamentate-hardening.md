# Feature — Aree regolamentate: interruttore, import incrementale, dangling, appartenenza e opt-in per ACC

Data: 2026-08-03 · Stato: **FATTO — codice chiuso, ⏳ verifica live da fare** (suite 951 verde, build 0 warning) ·
Branch `feature/aree-speciali-hardening`, 9 commit · Nato da un'analisi in tre punti (§1-3), esteso in giornata con
il picker scopribile, l'appartenenza multi-ACC e l'opt-in per ACC delle aree estere (§4-6) ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Contesto: [refactor 02](../refactor/02-import-acc-e-settori.md) (il use-case di import), [ADR-0006](../adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md) (policy opt-out).

## Obiettivo

Un'analisi del percorso «aree speciali» (sorgente → DB → documento → viewer) ha lasciato tre punti aperti. Sono
indipendenti fra loro e si chiudono insieme perché toccano lo stesso pezzo di dominio.

1. **Nessun interruttore.** Le aree regolamentate erano l'unica categoria di dati di sorgente fuori dalla
   `ImportPolicy`: si importavano sempre. Un giro con dati sbagliati **pota** le righe buone (il prune per-ACC
   cancella ciò che la sorgente non espone più) e l'admin non aveva modo di fermarlo.
2. **N+1 a ogni giro.** Per ogni area di ogni ACC si scaricava il dettaglio, solo per rileggere una shape identica.
3. **Riferimenti che spariscono in silenzio.** La selezione salvata in un documento cita le aree per `IvaoId` senza
   FK. Se il prune ne cancella una, `SpecialAreaProjection` la salta (`continue`) e l'area sparisce dal documento:
   nessun errore, nessuna segnalazione, e chi apre l'editor vede solo un id nudo.

Fuori scopo, deciso: il campo **`SpecialArea.Range`** è importato e mai letto. Lasciato com'è — toglierlo è una
rimozione di colonna, con la sua propagazione; qui si stava aggiungendo, non potando.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova. Una colonna sulla `ImportPolicy` che già esiste ed è il posto unico della
provenienza dati (estendere, non affiancare). Il legame documento↔area resta dov'è — dentro il `BodyJson` della
sezione `regulated`, non una tabella di join: sopravvive agli snapshot pubblicati, che è il motivo per cui i
soft-ref del progetto sono soft (audit 22 lug, Fase 2).

**2. Dispatch.** Nessuno `switch` nuovo. Al contrario: i lettori del `BodyJson` `regulated` erano **due** con
comportamenti diversi (l'assembler ACC leggeva anche l'array legacy, l'APP no) e diventano uno,
`RegulatedSelectionJson`.

**3. Ingressi + verifica.** Ingressi già esistenti: `/vsop/admin/sorgenti` per l'interruttore,
`/vsop/admin/diagnostica` (e health check) per il nuovo rilievo, l'editor del documento per le aree sparite.
Nessun catch-22: non si crea niente di nuovo da raggiungere. Verifica: test sui casi elencati sotto + giro reale
sull'editor e sulla pagina sorgenti.

**4. Propagazione.** `IAccDirectory.GetSpecialAreasAsync` cambia firma (parametro `skipDetailIds`): aggiornati
client reale e i tre fake nei test. `AccDocumentAssembler.ParseRegulated` **sparisce**, sostituito dal lettore
condiviso — nessun commento o `<see cref>` lo cita più. Doc autorevoli: `spec/modello-dati.md` §9.21-9.22,
ADR-0006 (elenco categorie), `rounds.md`.

## Design

### 1. Interruttore — categoria `SpecialAreas` nella policy

`ImportCategory.SpecialAreas` + `ImportPolicy.ImportSpecialAreas` (default `true`, opt-out come le altre).

Il gate sta in **`SpecialAreaImportUseCase.RunAsync`**, non nell'hosted service: quel use-case è il corpo condiviso
fra job automatico e bottone «Importa da sorgente» di `/vsop/admin/accs` (decisione D2 del doc refactor 02, «manual
= auto, stesso stato DB»). Nell'hosted service il bottone lo scavalcherebbe.

Esce **prima della fetch e prima del prune** — è il punto che conta: categoria esclusa significa *congelamento*,
non «importa a vuoto e cancella tutto».

Sfumatura di semantica, scritta anche nel commento dell'entità: per le altre categorie `false` = «lo gestisco a
mano», ma le aree regolamentate non sono editabili da nessuna pagina. Qui `false` = «tieni quelle che hai». La
pagina lo dice a video: provenienza «❄ Congelate: restano quelle già in archivio» invece di «✎ Manuale».

#### La trappola del default (trovata strada facendo)

`dotnet ef migrations add` genera `defaultValue: false` per un bool nuovo. Per un flag **opt-out** è il valore
sbagliato: su un DB dove la riga di policy esiste già, la categoria nasce **spenta**. E su Postgres non basta
correggere la migration — lo schema di Neon si allinea con `PostgresSchemaReconciler` (EnsureCreated, ADR-0007),
che aggiungeva ogni colonna NOT NULL con un default neutro per tipo store, cioè `false`.

Quindi il default sta **nel modello** (`HasDefaultValue(true)`) e il reconciler lo legge (`BackfillLiteral`) invece
di indovinarlo. Lo stesso difetto era già in casa: **`ImportSids`**, aggiunto l'8 luglio con `defaultValue: false`.
La migration nuova gli rimette il default corretto per il futuro.

> ⚠ **Da controllare in produzione**: se la riga `ImportPolicies` esisteva prima dell'8 luglio, `ImportSids` può
> essere rimasto a `false` — cioè l'import SID è fermo da allora. Non lo si può ribaltare da codice: `false` è
> indistinguibile da una scelta deliberata dell'admin. Si guarda in `/vsop/admin/sorgenti`.

### 2. Import incrementale della shape

L'elenco paginato porta già tutti i metadati (nome, tipo, quote, attivazione). Il dettaglio serve **solo** per
`regionMapPolygon`, che è la parte più stabile del dato: la geometria di un'area cambia con l'AIP, non col giro
giornaliero.

```
GetSpecialAreasAsync(accIcao, skipDetailIds, ct)
        ▲
        └── ListAreasWithFreshShapeAsync(acc, now-30gg)   // shape presente E importata di recente
```

Per gli id in `skipDetailIds` il client non chiama il dettaglio e restituisce shape `null`; l'upsert
(`if (a.RegionMapPolygon is not null)`) la legge come «tieni quella che hai» — comportamento che c'era già, qui
diventa il caso normale. Il client **non conosce il DB**: il set glielo passa il use-case (invariante #6, porte).

Un'area **senza** shape resta richiesta a ogni giro finché la shape non arriva: è il caso che si vuole risolvere,
non uno da mettere a riposo.

Scartato: `ETag`/`If-None-Match`. `IvaoHttp.GetStringAsync` butta via la response, servirebbe uno store per URL, e
non è dato che l'API IVAO emetta quegli header — costo certo, beneficio da verificare.

### 3. Riferimenti dangling — rilevare, non vincolare

Due segnalazioni, nessun vincolo nuovo.

**Diagnostica.** Nuovo rilievo «Area regolamentata dangling» (Warning) in `ConsistencyReportService.Analyze`, che
resta una **funzione pura**: il repository carica il JSON grezzo (`RegulatedRefRow`) e il parse avviene
nell'analisi. Il dataset porta anche `SpecialAreaIds`, gli id realmente esistenti.

Si guarda la sola **versione di lavoro** di ogni documento (bozza più recente > pubblicata corrente > ultima): le
versioni storiche sono congelate per definizione, segnalarle sarebbe rumore su qualcosa che nessuno può correggere.

Le aree del proprio ACC in **automatico** non possono essere dangling: lì non ci sono id salvati, c'è la lista viva.

**Editor.** `RegulatedAreasEditor` marca «⚠ non più disponibile» le aree che il picker non risolve più, con la ✕
per toglierle. Prima mostrava l'id nudo come se fosse un nome.

**Nessun guard nel prune**, deliberatamente: se l'area non esiste più a monte, tenerla in DB perché un documento la
nomina significa servire dato morto. La linea del progetto sui soft-ref è *rileva, non vincolare*
(`ConsistencyReportService`, commento di testa).

## Passi

1. Policy: enum + colonna + migration + store + gate nel use-case + riga in `/vsop/admin/sorgenti` (+ it/en).
   Default nel modello e `BackfillLiteral` nel reconciler.
2. Import incrementale: firma della porta, client, metodo di repository, use-case; fake dei test allineati.
3. Dangling: `RegulatedSelectionJson` condiviso, dataset + `Analyze`, caricamento EF della versione di lavoro,
   marcatura nell'editor.
4. Doc: questa carta, `spec/modello-dati.md` §9.21-9.22, ADR-0006, `rounds.md`, memoria.

## Casi che i test fissano

- policy di default → le aree si importano (`Default_policy_imports_areas`);
- categoria esclusa → **zero fetch** e l'area già in archivio **resta**, benché la sorgente non la esponga più;
- ACC la cui fetch fallisce → nessun prune di quell'ACC (regressione già coperta, ri-fissata qui);
- secondo giro con shape fresche → il dettaglio è saltato per tutte, e la shape salvata sopravvive;
- area **senza** shape → non viene saltata;
- id selezionato che non sta più nei cataloghi → un rilievo con quell'id, e senza quelli buoni;
- selezione in automatico → nessun rilievo, anche a catalogo vuoto;
- selezione in **formato array legacy** → letta come manuale (prima l'APP la leggeva vuota);
- documento con bozza e versione pubblicata → si legge la **bozza**.

## Seguito, stesso giorno: il picker non pescava, e l'appartenenza era sbagliata

Provando la selezione cross-ACC su una vIPI di APP sono venute fuori altre due cose, una di forma e una di sostanza.

### 4. Il picker nascondeva ciò che aveva

Le aree di altri ACC si potevano già scegliere (`ExtraIds`), ma i candidati comparivano **solo digitando** ed erano
tagliati a 12 senza dirlo. Con ~800 aree in archivio, qualunque ricerca mostrava dodici righe qualsiasi e sembrava
che la propria non ci fosse.

Aggiunti: **tendina per ACC** col conteggio per ente, elenco visibile anche senza cercare, **contatore**
(«Mostrate 20 di 99: restringi con l'ACC o la ricerca»), elenco scorrevole. Vale per entrambi gli editor, ACC e
APP: il componente è condiviso.

### 5. Un'area può appartenere a più ACC — e noi ne tenevamo uno solo

Il caso che l'ha svelato: **id 8870, «LI R49A/B/C/D/E/F - Zita»**, che su IVAO sta nell'elenco di LIRR *e* in
quello del militare LIZZ. Da noi risultava solo di LIZZ.

Il motivo: `IvaoId` è unico e `CenterId` era una colonna sola, quindi ogni ACC che elencava l'area **riscriveva**
l'appartenenza — vinceva l'ultimo in ordine alfabetico (`ListAccsAsync` ordina per codice). LIZZ viene dopo LIRR,
ed è pure un ente nascosto: l'area spariva dalle «aree proprie» di Roma senza che nulla lo segnalasse.

Le 15 aree di LIZZ sono tutte di questa specie: R21 Sara, R49 Zita, STAR1-10, Donald, Eolia, East/West Sardinia.

**Modello nuovo** (SPEC §9.23): entità di legame `SpecialAreaCenter (IvaoId, CenterId)`, `SpecialArea.CenterId`
rimossa. Import additivo, prune per legame, area cancellata solo quando resta senza enti. Nei picker «proprie» e
«di altri ACC» si decidono sui legami, e la riga mostra tutti gli enti.

**Backfill doppio, e non è pignoleria**: la migration serve SQLite, ma in produzione lo schema lo allinea
`PostgresSchemaReconciler`, che le migration non le esegue. Quindi il travaso vive anche in
`ISpecialAreaMaintenance`, al boot — e lì tocca pure **droppare** la colonna storica: NOT NULL e ormai fuori dal
modello, farebbe fallire ogni inserimento di area nuova.

Il backfill recupera **una sola** appartenenza per area, l'unica che il vecchio modello sapeva tenere. Le altre
tornano col primo import: dopo il deploy conviene premere «Importa da sorgente» invece di aspettare il giro
automatico.

Verificato sulla copia del `vipi.db` reale: 993 aree → 993 legami, nessuna orfana, shape intatte, colonna storica
sparita.

Test aggiunti: area elencata da due ACC → una riga e due legami, propria per entrambi e in «altri ACC» per
nessuno; prune di un ente lascia l'area all'altro; quando la molla anche l'ultimo, l'area sparisce.

### 6. Le aree estere si importano solo se le chiedi

Ultimo pezzo, chiesto per **alleggerire**: in archivio c'erano 993 aree, di cui **763 estere**. Gli ACC esteri li
materializzano le vLOA (`Acc.IsForeign`), e l'import ciclava su tutti — LFZZ 359 aree, LYBA 145, DAAA 70 —
ri-scaricandole ogni 24h per servire quasi nulla.

**`Acc.SpecialAreasEnabled`** (default `true`): il giro periodico tocca solo gli ACC abilitati. Per un ente spento
c'è **«Importa aree»** nella sua riga di `/vsop/admin/accs`: scarica subito (`RunForAccAsync` ignora il flag — è
l'atto con cui lo accendi) e, se trova qualcosa, lo abilita. Da lì in poi si aggiorna ogni 24h come gli italiani.

L'abilitazione avviene **solo se la fetch ha prodotto qualcosa**: un ACC acceso con la fetch fallita entrerebbe nel
giro periodico senza aree, e nessuno saprebbe perché.

**«Escludi aree»** fa il contrario e libera l'archivio: toglie i legami di quell'ACC, e le aree che nessun altro
ente elenca spariscono. Quelle condivise (es. una R nazionale che sta anche sul militare italiano) restano.

**Riconciliazione one-shot** al boot per lo stato esistente: spegne tutti gli esteri e libera le loro aree. Gira
**una volta sola**, con segnaposto in `ImportState` — senza, ogni riavvio ricancellerebbe le aree di un ente estero
appena riabilitato a mano, e sarebbe un bug fastidioso da diagnosticare.

Provato sulla copia del `vipi.db` reale: **993 aree → 230**, tutte italiane e invariate (LIRR 99, LIBB 65, LIMM 27,
LIPP 24, LIZZ 15), 763 legami liberati, nessuna area orfana, seconda esecuzione a 0.

> ⚠️ Le aree estere che un documento citava diventano **dangling**: la diagnostica le segnala e l'editor le marca
> (§3). Se serviva davvero un'area francese, si riaccende LFMM con «Importa aree» e torna al suo posto.

## Stato alla chiusura

| Passo | Esito |
|---|---|
| §1 Interruttore di categoria | fatto — riga in `/vsop/admin/sorgenti`, gate nel use-case |
| §2 Shape incrementale | fatto — dettaglio saltato se la shape è in archivio da meno di 30 giorni |
| §3 Riferimenti dangling | fatto — rilievo in diagnostica + marcatura nell'editor |
| §4 Picker scopribile | fatto — filtro per ACC, conteggio, elenco scorrevole (forma singolare compresa) |
| §5 Appartenenza multi-ACC | fatto — `SpecialAreaCenter`, migration provata su copia del DB reale |
| §6 Aree estere su richiesta | fatto — `Acc.SpecialAreasEnabled`, one-shot al boot, 993 → 230 aree |
| Verifica live | **da fare** — quattro punti qui sotto |

**Verifica live, cosa guardare:**
1. `/vsop/admin/sorgenti`: togliere «Aree regolamentate», lanciare l'import, controllare che le aree **restino**
   (non deve potare); rimetterla e verificare che riprenda.
2. Editor di un documento con un'area cancellata a mano dal DB: deve comparire «⚠ non più disponibile» e il
   rilievo in `/vsop/admin/diagnostica`.
3. Dopo un import: la **R49 «Zita»** deve stare fra le aree *proprie* sia di LIRR sia di LIZZ.
4. `/vsop/admin/accs`: «Importa aree» su un ACC estero → lo accende e ne mostra il conteggio; «Escludi aree» →
   torna a «non importate» e libera l'archivio.

## Non-obiettivi

Rimozione della colonna `Range`; parallelismo sulle chiamate di dettaglio (da valutare solo se il primo import a
freddo risulta lento); auto-correzione dei riferimenti dangling; policy per-ACC invece che globale; **fusione delle
aree che la sorgente duplica** con id diversi sotto due centri (8 casi, tutti francesi: `LF R 55 A` su LFXV e
LFZZ…) — lì è IVAO a tenerne due schede, e unirle sarebbe una nostra invenzione.
