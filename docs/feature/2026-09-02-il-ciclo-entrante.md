# Il ciclo entrante — quattro cose che aspettavano il rollover per accorgersi di esistere

**2 settembre 2026.** Carta di lavoro, §AW. Segnalazione del committente: *«è uscito il ciclo 2609 ma il
sito non ha pubblicato le SID previste per quel ciclo»*.

---

## Parte 0 — Il fatto, misurato

Prima cosa da mettere per iscritto, perché tutto il resto ci poggia sopra: **il 2 settembre 2026 il ciclo
corrente è 2608**, e **2609 entra in vigore il 3 settembre alle 00:00Z**. Girata la matematica di
`AiracService` sull'orologio vero:

```
2608  2026-08-06   ← corrente il 2 settembre
2609  2026-09-03   ← il giorno dopo la segnalazione
2610  2026-10-01
```

Quel che è uscito il 1 settembre non è il *ciclo*: è il **sectorfile**. Misurato sulla sorgente vera —
`GET https://api.github.com/repos/ivao-italy/it-aurora-sector` risponde `"pushed_at":
"2026-09-01T12:55:01Z"`. La divisione scrive i dati del ciclo entrante **prima** che il ciclo entri, che è
esattamente il comportamento previsto.

Quindi il selettore che offre **2609** come primo ciclo pubblicabile è **corretto** e non è un
«pubblica ora»: `UpcomingCycles` salta di proposito il corrente (`ReleaseService.cs`), e l'opzione porta
scritta la sua data efficace. Su quel punto non c'è niente da riparare.

**Ma la segnalazione ha ragione lo stesso**, e per quattro motivi indipendenti che si sommavano.

---

## Parte 1 — Perché le SID non escono da sole

La catena, per intero:

1. La sezione `sids` di una vIPI d'aeroporto è **`Frozen`**. La pagina pubblica legge lo **snapshot**
   della release effettiva, non il vivo (`AirportFrozenSectionProvider`, `AeroportoPage.razor`).
2. **Nessun processo pubblica al rollover.** Una release *schedulata* diventa effettiva da sé — il
   confronto è per data (`EfReleaseRepository.GetEffectiveAsync`) e non serve nessun giro — ma
   **qualcuno deve averla creata**.
3. Quindi il 3 settembre il sito mostra ancora le SID congelate nella release del 2608, finché una
   persona non pubblica.

Il flusso giusto **esiste già** ed è quello previsto dalla carta delle SID: pubblicare **al ciclo 2609**
*prima* del 3 settembre. Lo snapshot si congela con `ShapeReleaseContext` aperto su 2609, quindi la
cattura include le SID che entrano a quel ciclo, e la release entra in vigore da sola.

> ⚠️ Salvo che **fino a §AW2 non era vero fino in fondo**: il buffer di un ciclo si sommava una seconda
> volta dentro la derivazione, e la release programmata al 2609 usciva **senza** le SID del 2609. Vedi lì.

**Il difetto non è nel meccanismo: è che nessuno viene avvisato di azionarlo.** E le tre cose che avrebbero
dovuto avvisare guardano tutte all'orologio sbagliato.

---

## §AW1 — La deriva guarda anche al ciclo entrante

`ImpactDriftUseCase` è il rivelatore calcolato che apre le righe «da ripubblicare». Costruisce lo snapshot
di confronto con `_airac.GetCycle(DateTime.UtcNow)` — **il ciclo di oggi**.

Incrociato con l'attesa delle SID (`SidRow.IsPublicAt`: una SID importata compare solo dal ciclo **da cui
vale**) il risultato è che il giro **non può vedere** quel che sta per cambiare:

| quando | ciclo | la deriva vede | l'admin sa |
|---|---|---|---|
| 1 set, sectorfile aggiornato | 2608 | niente (`IsPublicAt` le nasconde) | niente |
| 2 set, giro notturno | 2608 | niente | **niente** |
| 3 set, rollover | 2609 | le SID nuove | **il giorno dopo, a ciclo già in vigore** |

L'avviso arriva **sempre in ritardo di un ciclo**, per costruzione. È la riga che manca nella lista, ed è
il cuore della segnalazione.

**La mossa.** `DriftFromEffectiveAsync` prende un parametro `alCiclo` (null = corrente, cioè il
comportamento di sempre): il motore già sa farlo, perché `BuildSnapshotJsonAsync` prende il ciclo come
argomento e apre `ShapeReleaseContext` su quello — è la stessa porta che serve l'anteprima di release.

Il giro lo chiama **due volte** e nasce un `ImpactKind` nuovo, `ReleaseDriftNextCycle`.

⚠️ **Si apre solo se la deriva di oggi è vuota.** Un documento già indietro *adesso* ha già la sua riga
«da ripubblicare»: una seconda riga che dice «e sarà indietro anche domani» è rumore su una lista che vive
di essere corta.

⚠️ **Nuova severità `DaPreparare`**, fra `DaRipubblicare` e `DaRileggere`. Non è la stessa urgenza: qui non
è rotto niente e nessuno sta leggendo una copia sbagliata — c'è del lavoro con una **scadenza**. Metterla
nella stessa banda avrebbe mescolato «il pubblico legge il falso» con «prepara il prossimo AIRAC».

L'azione che la chiude resta **Ripubblica**: il fatto diventa falso quando si schedula la release. Un ✓
non si offre — il giro la riaprirebbe stanotte, ed è la regola già scritta in `WorkMapping`.

---

## §AW2 — Il ciclo lo **dichiara la sorgente**, e nessuno glielo chiedeva

`SidImporter` timbrava `SourceAiracCycle = _airac.GetCycle(DateTime.UtcNow)`, cioè **il ciclo in cui era
capitato di girare**, e la riga usciva al ciclo **dopo**. Ma il giro è ogni 24 ore, con `bootDelay` e
ritentativi: quando cade è un dettaglio d'esercizio, non un fatto sui dati.

Il modo di fallire è brutale e **muto**:

- sectorfile aggiornato l'1 settembre, giro che passa il 2 → pubblico il **3 settembre**. ✔
- lo stesso file, ma il giro passa il 3 alle 02:00 (app riavviata, sorgente lenta, un ritentativo
  slittato) → pubblico il **1º ottobre**. ✘ **Un mese di ritardo, e nessuno lo vede.**

La stessa riga di dati prendeva due destini a seconda dell'ora in cui era passato un job. Non è un buffer
prudente: è un lancio di dado.

### La scoperta: il sectorfile dichiara il proprio ciclo

Cercando dove leggere una data affidabile è saltato fuori che **la domanda giusta ha già una risposta
scritta**. Il repo Aurora tiene `SectorFiles/Include/IT/CHANGELOG/<ciclo>.txt`, uno per AIRAC. Misurato il
2 settembre 2026, il file più alto era **`2608.txt`**, e comincia così:

```
**AIRAC A2608 IN VIGORE DAL 06/08/2026
```

Il **6 agosto 2026** è esattamente la data che calcola `AiracService` per il 2608. La sorgente dice il ciclo
*e* la sua data efficace, e per un anno il ciclo lo abbiamo **indovinato** dall'orologio di un job.

> 🔴 **E dice anche un'altra cosa, che è la risposta alla segnalazione: `2609.txt` NON C'ERA.** Il 2
> settembre la sorgente non aveva ancora i dati del ciclo entrante — il commit dell'1 settembre era
> «bugfix settore dinamico LIBV_APP», una correzione dentro il 2608. Il sito non pubblicava le SID del
> 2609 perché **non esistevano ancora**. Il changelog del 2608, per confronto, era stato scritto il **25
> luglio**, dodici giorni prima dell'entrata in vigore: è quello il ritmo con cui la divisione lavora.

### Che cosa cambia

`SourceAiracCycle` smette di voler dire «il ciclo in cui l'ho prelevata» e vuol dire **«il ciclo dal quale
la riga è in vigore»**; `IsPublicAt` confronta con `>=` invece che con `>`. Il buffer non sparisce: smette
di essere sommato **a valle** da chiunque legga, e viene deciso **una volta sola**, in
`SidStampCycle.Scegli`, che è pura e si verifica senza rete:

1. **il ciclo dichiarato** dalla sorgente;
2. il ciclo **successivo** a quello in cui la sorgente è cambiata l'ultima volta (dalla API dei commit —
   ⚠️ misurato: `raw.githubusercontent.com` manda `ETag` ma **non** `Last-Modified`, la via degli header non
   esiste);
3. il ciclo **successivo** all'ultimo giro riuscito, e in mancanza anche di quello, ad adesso — cioè
   esattamente il comportamento di prima.

⚠️ **La colonna tiene il nome che aveva.** Rinominarla è una migrazione senza guadagno, e il significato
nuovo è scritto sull'entità e su `IsPublicAt`. È un debito dichiarato, non dimenticato.

⚠️ **I ripieghi sbagliano per eccesso di fretta, ed è voluto.** Al gradino 3, se l'ultimo giro riuscito è
di tre giorni fa e nel frattempo il ciclo è girato, si parte dal ciclo **vecchio** e la riga esce
**prima**. Il cambiamento era osservabile in quella finestra e noi non abbiamo guardato: il ritardo è
nostro e non deve diventare un ritardo del dato. Il verso opposto — nasconderla per un mese — è il difetto
che si sta chiudendo. Chi vuole trattenerne una ha già `ForcePublished` per riga.

⚠️ **`ContentUnchanged` non si tocca.** Il valore nuovo vale per le righe **nuove o cambiate**; quelle
identiche conservano il primo, che è la regola pagata nell'audit del 24 luglio (senza, il re-timbro le
rinascondeva a ogni giro). È anche ciò che limita il rischio del cambio di significato: una riga il cui
contenuto non si muove non cambia ciclo, e «in vigore adesso» per lei è vero.

⚠️ **Una chiamata per giro, e in cache anche il «non lo so».** Il giro chiama `ImportAsync` una volta per
aeroporto — decine — e la risposta non dipende dall'ICAO: la quota anonima di GitHub è sessanta all'ora, e
un 403 richiesto trentanove volte dà trentanove 403. La fetta sta in `SectorfileCache`, che il giro
d'import già invalida a ogni passata.

⚠️ **Non solleva mai.** Un import che cade perché una API di contorno ha dato 403 sarebbe un danno molto
più grande della domanda a cui non si è saputo rispondere.

### La ricaduta che vale più di tutte

Il buffer era sommato **due volte**: una nel timbro, una nella derivazione. Per questo una release
programmata al ciclo entrante **non conteneva le SID di quel ciclo** — uscivano a quello dopo. Ora
l'anteprima e lo snapshot del 2609 contengono ciò che entra al 2609, che è quel che chi prepara un AIRAC si
aspetta di leggere.

## §AW3 — «Prossimo AIRAC»: una sezione, non una lista nuova

Manca il posto in cui uno sguardo solo dice «al ciclo entrante cambiano questi documenti, e per questi una
release è già schedulata».

⚠️ **Non nasce una pagina nuova, e nemmeno una lista nuova**: la §1 del `FEATURE-PROCESS` e la carta
*«da fare: una lista sola»* dicono di estendere. Le righe di lavoro le porta già §AW1 nella lista che c'è;
qui serve solo il **quadro di insieme** con il gesto in blocco, e sta come sezione di **Versioni**
(`/services/vsop/versions`), che è la pagina che già mostra il ciclo corrente e già inietta
`IReleaseService`.

Contiene: ciclo corrente, ciclo entrante con data efficace e **quanti giorni mancano**; per ogni documento
pubblicato, se ha già una release schedulata a quel ciclo, e — per quelli che non l'hanno — che cosa
cambierebbe. Più un tasto **«schedula tutti al ciclo entrante»**.

⚠️ **Il gesto in blocco rispetta i lock e i permessi uno per uno**: passa dallo stesso
`IReleaseService.PublishAsync` di un pubblica singolo, che è già il posto dove quelle due domande si fanno.
Un ciclo che salta un documento lo **dice**, invece di riuscire a metà in silenzio.

---

## §AW4 — Gli stati delle release invecchiano da soli, e nessuno li risveglia

`RecomputeStatuses` gira **solo in scrittura**: `SaveReleaseAsync`, `CancelAsync`, `PruneReleasesAsync`.
Quest'ultimo è chiamato da `PruneAllAsync`, che gira **una volta sola all'avvio**.

Su un processo che resta su per settimane — ed è il caso: Plesk lo spegne per inattività, non per anzianità
— al rollover la release del 2608 resta marcata `Effective` e quella del 2609 resta `Scheduled`, per
sempre.

**Che cosa NON rompe** (misurato leggendo i chiamanti, non dedotto): la visibilità è salva. Sia
`GetEffectiveAsync` sia `ListAsync` ordinano per **data** ed escludono solo le `Superseded`, quindi
scelgono la release giusta; e le etichette a schermo guardano `IsEffectiveNow` **prima** dello stato. Il
pubblico vede la cosa giusta.

**Che cosa rompe**: la **retention**. `PruneReleasesAsync` pota le `Superseded` oltre soglia, e su un
processo lungo non ce ne sono mai di nuove — le release superate si accumulano insieme ai loro payload,
che sono il pezzo grosso della tabella. Lo sweep esiste già ed è idempotente: gli manca solo di girare.

**La mossa.** `ReleaseSweepHostedService`, 24 ore, dallo stesso `GatedImportLoop` degli altri giri (salta
il primo se fresco, ritenta corto se fallisce, registra l'esito) con categoria sua. Non è un import e non
compare in Sorgenti — si legge in Diagnostica, come il giro della deriva.

⚠️ **Non si aggiunge al giro della deriva**, benché la cadenza sia la stessa: quel giro ha un nome che dice
che cosa fa, e appendergli una potatura lo renderebbe un nome falso. Un file in più costa meno di un nome
che mente.

---

## Ordine dei passi

| # | Passo | Perché in questo ordine |
|---|---|---|
| 1 | §AW4 — lo sweep giornaliero | Isolato, nessuna dipendenza, chiude un accumulo che cresce ogni giorno |
| 2 | §AW2 — il timbro dalla sorgente | Sotto tutto: senza, §AW1 e §AW3 misurano un dato che può essere sbagliato di un mese |
| 3 | §AW1 — la deriva al ciclo entrante | Porta la riga nella lista |
| 4 | §AW3 — la sezione «Prossimo AIRAC» | Legge quel che i tre passi prima hanno reso vero |

## Verifica — che cosa ha detto

**Test**: 32 nuovi, suite intera verde sui due TFM (**9820**, E2E compresi), build Release
`--no-incremental` con 0 avvisi. ⚠️ **Nessuna migrazione**: `ImpactKind` è un enum e il valore nuovo si
**appende in coda**, così gli ordinali già scritti in archivio non si spostano — la finestra cieca al 16
settembre regge.

**Dal vivo** (Edge + puppeteer su una copia del `vipi.db`, aspettando il **circuito** e non il DOM del
prerender). La sezione esce giusta — «AIRAC 2609, in vigore dal 03 Sep 2026, fra un giorno» contro «AIRAC
2608» in testata — e il gesto in blocco funziona: cliccato, **16 release programmate**, il pannello si
rilegge da sé e dice «tutti a posto». Console pulita, nessun 4xx, nessun letterale Razor.

E ha trovato **tre cose che nessun test poteva dire**:

1. **«in 1 days».** Il singolare capita *proprio* il giorno prima del cambio, cioè l'unico in cui quella
   riga la legge qualcuno. Chiave sua.
2. **«vIPI Brindisi— LIBB».** Lo spazio fra un'espressione e un `@if` Razor lo mangia il compilatore: serve
   un carattere vero. Trappola già nota in questo progetto, e ripagata.
3. 🔴 **Due documenti che il pubblico legge restavano fuori da tutto.** Una release **programmata** non
   promuove la bozza a versione pubblicata — è voluto, ed è scritto in `PublishAsync` — quindi un documento
   pubblicato *solo* per schedulazione resta `Status = Draft` **per sempre**, pur essendo in vigore. Col
   cancello su `IsPublished`, la vIPI di **Milano** (in vigore al 2608) e **Catania Radar** (2607) non erano
   guardate né dal quadro né dal **giro della deriva**, che quel cancello ce l'ha da sempre: **due su
   diciassette**. E il difetto si **alimentava da sé** — programmare al ciclo entrante è proprio il gesto
   che §AW3 insegna, quindi più lo si usava, più documenti uscivano dal controllo.

   Il cancello è ora `ManagedDoc.VaTenutoAggiornato` — *non nascosto* **e** *(pubblicato **o** in vigore)* —
   in un posto solo. ⚠️ **Serve l'OR in tutt'e due i versi**: un documento pubblicato le cui release stanno
   sotto una **chiave vecchia** non ne ha una effettiva, ed è esattamente il caso C6 che la deriva ripara;
   togliendolo dal cancello, quel guasto non lo vedrebbe più nessuno. Rimisurato dal vivo: da **14 a 16**
   documenti, e l'unico rimasto fuori è il **nascosto**.

**Più una guardia resa meno fragile.** `FiltroPerTipoCompletoTests` cercava la *prima* occorrenza di
`KindFilters`, che è l'uso nel markup centosessanta righe più su, e da lì il primo `};` è di un membro
qualunque scritto in mezzo: è bastato aggiungere un metodo con un'espressione `switch` perché accusasse la
pagina di aver perso **tutti e cinque** i filtri, che erano al loro posto. Ora cerca la dichiarazione.
