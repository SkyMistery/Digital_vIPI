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
cattura include le SID nuove (timbrate 2608, che al 2609 diventano pubbliche) e la release entra in vigore
da sola.

**Il difetto non è nel meccanismo: è che nessuno viene avvisato di azionarlo.** E le tre cose che avrebbero
dovuto avvisare guardano tutte all'orologio sbagliato.

---

## §AW1 — La deriva guarda anche al ciclo entrante

`ImpactDriftUseCase` è il rivelatore calcolato che apre le righe «da ripubblicare». Costruisce lo snapshot
di confronto con `_airac.GetCycle(DateTime.UtcNow)` — **il ciclo di oggi**.

Incrociato col buffer delle SID (`SidRow.IsPublicAt`: una SID importata compare solo dal ciclo
**successivo** al prelievo) il risultato è che il giro **non può vedere** quel che sta per cambiare:

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

## §AW2 — Il timbro del ciclo viene dalla sorgente, non dall'orologio del giro

`SidImporter` timbra `SourceAiracCycle = _airac.GetCycle(DateTime.UtcNow)`, cioè **il ciclo in cui è
capitato di girare**. Il giro è ogni 24 ore, con `bootDelay` e ritentativi: quando cade è un dettaglio
d'esercizio, non un fatto sui dati.

Il modo di fallire è brutale e **muto**:

- sectorfile aggiornato l'1 settembre, giro che passa il 2 → timbro **2608** → pubbliche dal **3 settembre**. ✔
- lo stesso sectorfile, ma il giro passa il 3 alle 02:00 (app riavviata, sorgente lenta, un ritentativo
  slittato) → timbro **2609** → pubbliche dal **1º ottobre**. ✗ **Un mese di ritardo, e nessuno lo vede.**

La stessa riga di dati prende due destini diversi a seconda dell'ora in cui è passato un job. Questo non è
un buffer prudente: è un lancio di dado.

**La mossa.** Il timbro diventa *il ciclo in vigore quando la **sorgente** è cambiata*, che è un fatto sul
dato e non sull'esercizio. Nuova porta `ISidSourceStamp` (Application) con un'unica domanda: «quando è
cambiata l'ultima volta la cartella dei file `.sid`?». L'adattatore `GitHubSidSourceStamp` la risponde con
**una** chiamata per giro:

```
GET /repos/{owner}/{repo}/commits?path={dir}&per_page=1  →  commit.committer.date
```

Misurato: la API risponde e il dato è utile — `lirf.sid` risulta toccato l'ultima volta il **22 giugno
2026**, cioè per il 2609 quel file **non è cambiato affatto**. ⚠️ `raw.githubusercontent.com` **non manda
`Last-Modified`** (solo `ETag`): la via degli header non esiste, la API è l'unica sorgente di quella data.

**Tre gradini, e la caduta è dichiarata:**

1. la data dell'ultimo cambiamento in sorgente → il ciclo in vigore a quella data;
2. se la sorgente non risponde (rete, quota, formato): l'**ultimo giro riuscito** di categoria `Sid`, che
   è già in archivio (`IImportStateStore`) e non costa niente;
3. se non c'è nemmeno quello (primo avvio): il ciclo corrente, cioè il comportamento di prima.

⚠️ **Il ripiego 2 sbaglia per eccesso di fretta, ed è voluto.** Se l'ultimo giro riuscito è di tre giorni
fa e nel frattempo il ciclo è girato, si timbra il ciclo **vecchio** — cioè le SID escono **prima**. Il
cambiamento era osservabile in quella finestra e noi non abbiamo guardato: il ritardo è nostro, e non deve
diventare un ritardo del dato. Il verso opposto — nascondere per un mese — è il difetto che stiamo
chiudendo. Chi vuole l'opposto ha già `ForcePublished` per riga.

⚠️ **Il timbro non può stare nel futuro.** Se la sorgente dichiarasse una data avanti rispetto a
`UtcNow` (orologi, fusi, un commit datato male) il ciclo si taglia a quello corrente: una SID timbrata a un
ciclo che non è ancora arrivato sarebbe invisibile fino a lì, che è il difetto di prima al contrario.

⚠️ **`ContentUnchanged` non si tocca.** Il timbro nuovo vale per le righe **nuove o cambiate**; quelle
identiche conservano il ciclo del primo prelievo, che è la regola pagata nell'audit del 24 luglio (senza,
il re-timbro le rinascondeva a ogni giro).

---

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

## Verifica

- **Test** sul cuore deterministico: la scelta del ciclo di timbro (i tre gradini + il taglio sul futuro),
  la deriva al ciclo entrante e la regola «non due righe sullo stesso documento», la severità nuova.
- **Dal vivo**: la sezione «Prossimo AIRAC» su Versioni con il database di sviluppo, e il gesto in blocco
  guidato a schermo — le regressioni di binding Blazor sono silenziose coi test verdi.
- ⚠️ **Nessuna migrazione**: `ImpactKind` è un enum e il valore nuovo si **appende in coda**, così gli
  ordinali già scritti in archivio non si spostano. La finestra cieca al 16 settembre regge.
