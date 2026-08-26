# L'identità di un settore non è il suo nome

> 26 agosto 2026 · ramo `identita-settori`
>
> Un settore rinominato dalla sorgente oggi non viene rinominato: nasce due volte. Questa carta lega
> l'identità dei cataloghi all'**id numerico che IVAO manda già**, e riduce il callsign a ciò che è —
> un attributo che può cambiare.

## 1. Il guasto, in tre righe

I cataloghi sono chiavati sul callsign (`AccSector.ComposePosition`, `AirportSector.ComposePosition`,
indice unico) e l'import è **puramente additivo**: non pota mai. La proiezione
(`EfSectorProjectionService`) ricostruisce i `Sector` cercandoli per `Callsign`.

Quindi, quando IVAO rinomina `LIRN_US0_APP` in `LIRN_US1_APP`:

- il catalogo **aggiunge** una riga e tiene la vecchia (il suo `ImportedAtUtc` smette di avanzare);
- la proiezione crea un `Sector` **nuovo**, con un `Id` nuovo e vuoto;
- il vecchio `Sector` resta **attivo** — la sua riga di catalogo è ancora lì e non è nascosta — e si
  porta dietro il documento, l'AoR, gli accordi, la vLOA, i figli.

Il risultato è due settori dove ce n'è uno, e tutto il lavoro editoriale appeso al fantasma.
`StaleCatalogRow` lo descriveva già, per intero, senza poterlo risolvere.

## 2. Quello che la sorgente manda davvero (misurato il 26 agosto 2026)

`/v2/centers/{ACC}/subcenters` e `/v2/airports/{ICAO}/ATCPositions` portano un **id numerico**
già nella LISTA, non solo nel dettaglio:

```json
{"id":1174,"centerId":"LIRR","composePosition":"LIRR_NE_CTR","middleIdentifier":"NE", … }
{"id":3954,"airportId":"LIRF","composePosition":"LIRF_DEL","position":"DEL","order":0, … }
```

Quattro fatti, tutti verificati contro l'API vera:

| Fatto | Misura |
|---|---|
| L'id **non è indirizzabile** | `/v2/subcenters/1171` → `400 "Callsign is in the wrong format"`. L'URL vuole il callsign. Non ci serve: l'id ci serve come **ancora**, e nella lista c'è già. |
| Gli id vivono **per tipo** | subcenter 1171–3916, ATCPosition 3398–24707; zero collisioni sulle 229 righe italiane. La chiave resta comunque `(tipo, id)`: due sequenze distinte non promettono di non incrociarsi. |
| IVAO **aggiorna in posto** | 145 posizioni su 192 hanno `updatedAt ≠ createdAt`. `LIRQ_TWR` è id 3956, creato 2020-08-06 e aggiornato 2026-04-27: sei anni, stesso id. |
| L'elenco è **completo** | 37 subcenter e 192 posizioni live corrispondono esattamente ai 37 e 192 in archivio. Zero scarti in entrambi i versi. |

### Il caso vero, di quattro giorni fa

Confrontando `vipi.db.bak-pre-travaso-20260817` col live sono uscite due differenze reali:

```
1174  LIRR_NE_CTR    creato 2020-07-25   freq 124.2   "Roma Radar"
3916  LIRR_NE1_CTR   creato 2026-08-22   freq 124.2   "Roma Radar"   ← nuovo
```

**Non è una rinomina: è uno sdoppiamento.** Id nuovo, il vecchio vivo accanto.

Ed è la ragione per cui l'euristica non basta: stesso ACC, stessa posizione `CTR`, **stessa frequenza**,
stesso `atcCallsign`. Se `LIRR_NE_CTR` fosse andato in silenzio per due giri,
`FindRenameCandidateAsync` avrebbe trovato un candidato solo — perfetto, e sbagliato — e avrebbe
proposto di spostarci sopra il documento. Con l'id la domanda non si pone: 3916 non l'avevamo mai
visto, quindi è nato.

(L'altra differenza è una cancellazione vera: `/v2/ATCPositions/LIED_G_APP` → **404**.)

### Quello che la sorgente NON ci dà

- `parentSubcenterId` / `parentAtcPositionId` esistono come campi ma sono **vuoti su tutte e 229** le
  righe italiane. La gerarchia di copertura resta **nostra**.
- `/v2/centers` risponde `id: "LIRR"`, cioè il codice stesso: per gli **ACC** non esiste un surrogato.
  Va bene: il codice di un ACC non è un nome, è il suo ICAO, e non cambia.

## 3. Le tre classi di riferimento

La distinzione che decide cosa toccare e cosa no:

**(a) Riferimenti alla SORGENTE** — `ComposePosition` sui due cataloghi, `Airport.Icao`, `Acc.Code`.
Qui il callsign è lo specchio di ciò che IVAO dice. **Non si tocca.**

**(b) Riferimenti NOSTRI** — dati editoriali, scelte di persone. Devono puntare a un `Id`. Quasi tutti
già lo fanno (`CoordinationAgreement.SideA/BSectorId`, `DocumentParty.SectorId`,
`ContentBlock.Scope/From/ToSectorId`, `AirportFrequencyLink.SourceSectorId`, `Sector.ParentSectorId`,
`Sector.DocumentId`). Restano fuori, e la spazzata sul database vero (§5) dice quali sono per intero:

- `AccSector.ParentCallsign`, `AirportSector.ParentCallsign`, `Airport.ParentCallsign` — indicizzati,
  **senza FK** perché la catena attraversa i due cataloghi;
- `DocRelease.TargetKey` e `EditorTask.TargetKey` — `"{accCode}|{rootCallsign}"`, callsign, o ICAO;
- `DocumentImpact.SourceKey`, per le righe ancora aperte;
- i puntatori dentro `ContentBlock.BodyJson` — `Callsigns`, `MemberCallsigns`, `FreqLinkCallsigns`,
  `OpenCallsigns`: la configurazione dell'AoR e dei gruppi APP, 35 righe sul database vero.

**(c) Riferimenti STORICI** — `AtcSession.Callsign`, `AtcMonthRollup.Callsign`, le release già
pubblicate, i matcher Aurora. Qui il callsign **è il dato**, non un puntatore: dice «quella sera quel
tizio era connesso con quel nominativo». Non si converte in Id — si **risolve** con un alias.

## 4. Il disegno

### Strato 1 — I cataloghi si ancorano all'id

`AccSector.IvaoId` e `AirportSector.IvaoId` (`int?`, indice unico dove valorizzato). L'upsert cambia
chiave: cerca per `IvaoId`, e **`ComposePosition` diventa un attributo aggiornabile**.

Da qui una rinomina è un `UPDATE`. Non c'è da rilevarla con un'euristica, non c'è da proporla, non c'è
da chiedere a nessuno: semplicemente non produce più un fantasma.

**Il backfill è sicuro adesso**: il match per callsign è oggi 37/37 e 192/192, quindi il primo giro
assegna tutti gli id senza ambiguità e **senza rilevare nessuna rinomina**. Chi non prende un id sono
le righe `IsManual` — che la sorgente non ha mai mandato — e per loro `IvaoId is null` diventa
l'invariante vera al posto di un flag da ricordare.

### Strato 2 — La rinomina è un'operazione sola

Un motore solo, `ICallsignRenameService`, sullo stampo di `IDeletionService`. Quando l'upsert vede
`IvaoId` noto con `ComposePosition` diverso, chiama lui, e lui in **una transazione**:

1. riscrive `ComposePosition` sulla riga di catalogo;
2. riscrive `Sector.Callsign` **tenendo l'`Id`** → accordi, vLOA, blocchi, figli, documento, AoR,
   `FeaturedRank` non si accorgono di niente;
3. riscrive i tre `ParentCallsign` che puntavano al vecchio;
4. riscrive le chiavi di release e degli incarichi (`DocRelease.TargetKey`, `EditorTask.TargetKey`) nelle
   due forme, callsign nudo e `{acc}|{callsign}`;
5. riscrive la `SourceKey` delle segnalazioni **aperte**, e i puntatori dentro `ContentBlock.BodyJson`;
6. scrive l'alias;
7. apre una segnalazione (`ImpactKind.SectorRenamed`), perché una rinomina silenziosa resta una cosa che
   una persona deve sapere: i **collegamenti** sono a posto, il **testo** può ancora nominare il vecchio.

L'inventario dei posti da riscrivere non è a occhio: viene da una spazzata su ogni colonna testuale del
`vipi.db` vero, ed è nel commento di `EfCallsignRenameService`.

La proiezione **non cambia**: quando gira, il callsign è già allineato ovunque, e il suo upsert per
callsign ritrova lo stesso `Sector`.

⚠️ **Il caso di collisione.** Se il nominativo di destinazione è già di un'altra riga di catalogo o di un
altro `Sector`, la rinomina **non si applica**: si riferisce e basta (`RenameOutcome.Refused`), e le altre
del giro proseguono. Applicarla violerebbe l'indice unico a metà import, e scegliere chi dei due cede il
nome vuol dire scegliere quale documento perdere — che è una decisione di una persona, non di un giro
notturno. Non può capitare al primo giro (il backfill non rileva rinomine); può capitare a un archivio che
porta già un fantasma da prima di questa carta, o su uno scambio di nominativi fra due settori.

### Strato 3 — L'alias, per lo storico

`CallsignAlias` (callsign vecchio → `SectorId`, tipo di catalogo, da quando): serve **solo** allo
storico del punto (c). Non è un terzo meccanismo di risoluzione: è una tabella che risponde a una
domanda sola, «di chi era questo nominativo», e ha un lettore solo — `CallsignHistory`, che traduce in
**lettura**. La usa `ByPositionAsync`, perché una postazione rinominata a giugno non deve comparire come due
righe che si dividono le ore. ⚠️ Lì il taglio ai primi N esce dal database quando ci sono alias in archivio:
due metà che si fondono entrano fra le prime solo **dopo** essere state sommate.

Il meccanismo dei due giri (`PrevSuccessUtc` + `ImportedAtUtc`) e le pagine «Da sistemare»/«Orfani»
restano dove sono, ma smettono di dover indovinare le rinomine: gli restano le **sparizioni vere**, come
`LIED_G_APP`. Non si è aggiunta nessuna potatura automatica dei cataloghi: eliminare resta di
`IDeletionService`, con le sue protezioni e una persona che decide.

**Muore l'euristica**: `FindRenameCandidateAsync` e la parte «proponi la rinomina» di
`StaleCatalogRow` non hanno più ragione di esistere, e vanno via nello stesso giro — il record rimasto
vero a metà è il debito peggiore.

## 5. Verifica live (26 agosto 2026)

Eseguita col **codice vero**, sui **payload veri** appena scaricati dall'API, contro una **copia del
`vipi.db` di produzione** (migrazioni applicate, 9,7 MB). L'unico pezzo non esercitato è il trasporto HTTP,
verificato a parte interrogando l'API in diretta (§2).

**Backfill** — `37/37` subcenter e `192/192` posizioni prendono l'id al primo giro, e **zero alias**: nessuna
rinomina inventata, che è l'unico momento in cui questo poteva andare storto per tutti insieme.

**Rinomina** di `LIRR_NE_CTR` → `LIRR_NEZ_CTR`, stesso id 1174:

```
PRIMA   sectorId=22   figli=6  accordi=1  padri=6  blocchi=5   sessioni=582
DOPO    sectorId=22   figli=6  accordi=1  padri=6  blocchi=5   ← identità intatta
alias   LIRR_NE_CTR → LIRR_NEZ_CTR (ivaoId 1174, settore 22)
righe di catalogo LIRR: 13 (erano 13)      ← nessun fantasma
settori col vecchio nome: 0
sessioni ATC col vecchio nome: 582         ← la storia non si tocca
proiezione dopo: 1 settore col nome nuovo, attivo   ← non si inventa niente
statistica: col vecchio nome assente, col nuovo 534 sessioni
```

I **5 blocchi** sono configurazioni AoR vere che nominavano il settore: senza la riscrittura del JSON
sarebbero rimasti a puntare a un nominativo che non risponde più. Le 534 sessioni contro 582 sono il filtro
delle connessioni-lampo (&lt; 60 s), che è di `Contate` e non c'entra con la rinomina.

## 6. Pre-flight (FEATURE-PROCESS)

1. **Modello** — `IvaoId` è un attributo dei cataloghi esistenti, non un'entità gemella. `CallsignAlias`
   è nuova, ma non duplica nulla: `StaleCatalogRow` è un read-model calcolato, non uno storico.
2. **Dispatch** — lo `switch` subcenter/atcposition **esiste già** in tre punti (proiezione,
   `ListStaleCatalogRowsAsync`, `FindRenameCandidateAsync`); questo giro ne toglie uno e non ne aggiunge.
   Un registry sui cataloghi sarebbe un altro lavoro.
3. **Ingressi + verifica** — nessun ingresso UI nuovo (la rinomina è automatica), ma una **segnalazione**
   nella casella: automatica non vuol dire invisibile. Verifica: rinomina simulata sul `vipi.db` reale.
4. **Propagazione** — muoiono `FindRenameCandidateAsync` e il commento sulla rinomina in
   `StaleCatalogRow`; va aggiornata anche la memoria `documenti-da-rivedere-impatti`.

## 7. Fuori tema, trovato per strada

`regionMapPolygon` è **`[]` su tutte e 229** le righe live, mentre in archivio ci sono **66 poligoni TWR
reali** con `IsShapeSynthetic = 0`. L'upsert li assegna senza condizioni
(`EfAirportSectorRepository.cs`, `row.RegionMapPolygon = p.RegionMapPolygon`), quindi il prossimo import
di quegli aeroporti ci scrive sopra `"[]"`. **Non toccato qui**: è un altro problema e merita la sua carta.
