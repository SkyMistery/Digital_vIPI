# Analisi 25 agosto 2026 — che cosa succede se un dato importato viene eliminato dal DB

> **Stato:** analisi **chiusa**. Sette rilievi, **quattro** risolti nello stesso giro (voce `E11` in
> `lavori-aperti.md`, carta [documenti da rivedere](../feature/2026-08-25-documenti-da-rivedere.md)), **uno**
> aperto come debito (`C6`), **due** ancora da fare (§8).
>
> Nasce da una domanda del committente: *«rivedi il sistema di gestione delle informazioni importate: cosa
> succede se un dato viene eliminato dal DB?»*. È il documento d'origine di tutto il lavoro del 25→26 agosto.

## §1 — Com'è fatto il giro degli import

Sette giri periodici, un motore solo (`GatedImportLoop`), stato per categoria in `ImportState`, policy
opt-out globale in `ImportPolicy`. La catena è sempre la stessa:

```
sorgente → catalogo (AccSector / AirportSector / SpecialArea / Airport) → proiezione (Sector) → documenti → release
```

**La regola dichiarata in tre punti del codice è: upsert, mai prune.**
`EfAirportSectorRepository.cs:8-12` — «Niente cancellazioni (i settori spariti dalla sorgente restano;
l'admin li nasconde)»; idem `EfAccAdminRepository.cs:10-13`. L'unica potatura vera è quella delle **aree
regolamentate** (`EfAccAdminRepository.cs:174`), che pota i **legami** area↔ACC e cancella l'area solo quando
resta senza nessun ente.

## §2 — Dato sparito dalla SORGENTE

| Cosa | Effetto |
|---|---|
| ACC, aeroporto, postazione, subcenter | resta in archivio per sempre; l'admin nasconde a mano |
| Postazione sparita → `Sector` proiettato | disattivato, non cancellato |
| Area regolamentata | prune vero, con la guardia «fetch fallita → niente prune» |
| Shape mancante o doppia | nessuna cancellazione, ma il settore non attribuisce traffico → rilievo in diagnostica |

Questa metà regge: prune per-ACC protetto dagli errori transitori, legami e orfane in **una** `SaveChanges`.

## §3 — Dato cancellato dal NOSTRO DB

Qui stava il problema. Per entità, misurato:

| Entità | Risorge? | Quando | Cosa NON torna |
|---|---|---|---|
| `Acc` | sì, se la sorgente lo elenca | ≤24h | `IsHidden`, `IsForeign`, `SpecialAreasEnabled` (torna **true**) |
| `Airport` | sì — `AutoAssign` è additiva | ≤24h | **`DocumentId`**, `ParentCallsign`, `IsHidden`, `FeaturedRank`, regole pista, livelli TL, link frequenze, sezioni extra |
| `AirportSector` / `AccSector` | sì, upsert per callsign | ≤24h | `IsHidden` (**torna visibile**), limiti admin, `IsPrimary`, `IsAccApp`, shape sintetica |
| `Sector` (proiezione) | sì, al primo sync | subito | Id nuovo, **`DocumentId` null**, nome personalizzato |
| `SpecialArea` | sì, se l'ACC è abilitato | ≤24h | niente: è tutta di sorgente |
| `AirportSid` | sì | ≤ ciclo SID | `Priority`, `ForcePublished`, e il **timbro AIRAC viene rifatto** col ciclo corrente |
| `AtcSession` / traffico | **no** | mai | tutto: il giro ripassa solo `AtcHistoryRefreshDays` = **2 giorni**, e non c'è trigger manuale |
| `MediaAsset` | no | mai | immagini rotte nei documenti |
| `ImportPolicy` (riga) | sì, come **default** | subito | la scelta dell'admin: torna «tutto importato», in silenzio |
| `ImportState` (riga) | sì | al boot | vedi §5 |

## §4 — Gli effetti a valle

**Pubblico.** Le release congelano struttura e sezioni `Frozen` (default), quindi il *contenuto* pubblicato
regge. Ma **la risoluzione del bersaglio è viva**: `AirportReleaseTarget` legge `Airport.DocumentId`,
`AccVipiReleaseTarget` cerca un CTR radice `IsActive && DocumentId != null`. Cancella la riga importata e la
release resta in archivio senza risolvere più il documento: **pagina muta**. Eccezione: la sezione SID nasce
`Live`, quindi cambia in pubblico senza ripubblicare — ma il suo contenuto è cadenzato dall'AIRAC
(`SidRow.IsPublicAt`), quindi è un cambio programmato.

**Statistiche.** Chiavi stringa, nessuna FK: sessioni e traffico sopravvivono alla cancellazione dei
cataloghi. Vale anche il contrario, ed è progettato bene: la potatura del traffico tiene i contatori
denormalizzati, quindi i numeri storici non cambiano.

**Cache.** `IStationCatalogVersion.Bump()` scatta solo dagli import: una `DELETE` a mano non lo alza, e i
circuiti Blazor aperti continuano a mostrare il dato cancellato fino al riavvio.

## §5 — I sette rilievi

| | Rilievo | Esito |
|---|---|---|
| 1 | **Aeroporto o settore cancellato = documento sganciato per sempre**: la riga torna, `DocumentId` no, e nessun rilievo lo segnala | ✅ **chiuso** (E11): la proiezione non recide più, e l'impatto `BrokenTarget` lo dice |
| 2 | **`ImportPolicy` cancellata → regime «tutto importato» muto** (`EfImportPolicyStore.cs:28`): il primo giro dopo sovrascrive TA e piste messe a mano | 🟢 **aperto**, §8 |
| 3 | **Le cancellazioni strutturali non sono auditate** (ACC, aeroporto, settore) | 🟢 **aperto**, §8 |
| 4 | **`ImportState` è un interruttore travestito da metadato**: cancellare `SpecialAreaForeignOptOut` ri-spegne tutti gli ACC esteri e ricancella le loro aree; cancellare `AtcHistory` fa ripartire un backfill di 365 giorni (~220 chiamate) | 📋 **documentato** qui e nei commenti di `ImportCategories`; non è un difetto da riparare ma una trappola da conoscere |
| 5 | **Il tappo degli esteri è one-shot, e gli esteri nuovi nascono accesi**: `EfNeighbourRepository` crea l'`Acc` senza toccare `SpecialAreasEnabled`, default `true` | 🟢 **aperto**, §8 |
| 6 | **Sessioni ATC cancellate = perse**: finestra di ripasso di 2 giorni, nessun bottone di backfill | 📋 documentato (§3) |
| 7 | **`Restrict` senza pre-check**: `DeleteSectorAsync` guarda figli e torre, non accordi/parti/blocchi → `DbUpdateException` grezza in faccia all'admin | ✅ **chiuso** (E11): la rimozione di un orfano rifiuta con una frase che dice **chi** lo trattiene |

## §6 — Il difetto trovato progettando il rimedio

Scrivendo la carta è saltato fuori un guasto **indipendente** da tutto questo: la chiave di release di una
vIPI ACC è `{acc}|{callsign del settore primario}` e quella di un APP **è** il callsign
(`AccVipiReleaseTarget.cs:57`). Se il callsign si sposta, le release restano scritte sotto la vecchia, il
pubblico non le trova, e il documento pubblicato **va muto**. Latente al 25 agosto (le chiavi in archivio
combaciavano), aperto come **`C6`** in `lavori-aperti.md`.

## §7 — E la domanda del giorno dopo: la rinomina

*«Se `LIRN_US0_APP` diventa `LIRN_US1_APP`, che succede?»* — misurato: **peggio** della cancellazione, perché
non sparisce niente. Restano due settori attivi con la stessa shape, il documento sul fantasma, e nessun
rivelatore se ne accorge. Trovato anche un caso reale in archivio (`LIED_G_APP`, fermo da diciannove giorni).
Chiuso nello stesso giro col **timbro** `ImportedAtUtc`: carta §16.

## §8 — Che cosa resta da fare

Tre voci piccole e indipendenti, tutte con il loro punto esatto nel codice:

1. **La policy cancellata non deve tacere.** `EfImportPolicyStore.GetInfoAsync` già distingue «decisa da
   qualcuno» da «nata dai default» (`UpdatedUtc == null`): basta un rilievo di diagnostica quando almeno una
   categoria risulta manuale **e** nessuno l'ha decisa. Senza, il primo giro dopo una `DELETE` sovrascrive
   TA e piste editate a mano, e nessuno sa perché.
2. **Audit delle cancellazioni strutturali.** `StructureEditingService.cs:127/144/297` (ACC, aeroporto,
   settore) non scrivono niente nel registro, mentre l'eliminazione di un **documento** sì dal 22 agosto.
   Serve `AuditAction.Delete` con ICAO/callsign nei dettagli, scritto **prima** della cancellazione — dopo,
   il nome non è più leggibile. È il buco 5 dell'audit del 22 agosto, chiuso solo per `SetParentAsync`.
3. **ACC estero nuovo con le aree spente.** Una riga in `EfNeighbourRepository` (`SpecialAreasEnabled = false`
   alla creazione di un `Acc` con `IsForeign = true`): il tappo one-shot del 3 agosto vale solo per quelli
   che c'erano allora, e un estero creato oggi si porta dentro le sue aree al primo giro delle 24h.

## §9 — Le trappole da ricordare, che non sono difetti

- **Il backup.** Ogni riga «no» nella colonna *risorge?* di §3 è definitiva. La domanda «chi fa il backup di
  `itivao_atc`» è aperta da agosto (`lavori-aperti` §A9), e finché la risposta è riferita e non confermata va
  trattata come «non esiste».
- **Le migrazioni sostituiscono, non conservano.** La consegna del 23 agosto **sostituisce** il database
  invece di migrarlo: chi cancella dati contando su un rollback della migrazione si sbaglia.
- **Copiare un `vipi.db` vuol dire copiare tre file** — `.db`, `-wal`, `-shm` — o SQLite lo dichiara
  *malformed*: il database è in WAL e metà delle scritture recenti stanno lì.
