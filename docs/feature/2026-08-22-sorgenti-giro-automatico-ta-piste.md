# Sorgenti — il giro automatico di TA e Piste, e cosa la pagina non elenca (carta, 22 agosto 2026)

> Pagina `/services/vsop/admin/sources`. Seguito di
> [`2026-08-22-sorgenti-cosa-fa-la-policy.md`](2026-08-22-sorgenti-cosa-fa-la-policy.md), di cui **ribalta
> la slice 6 (§S6)**: lì TA e Piste sono state *dichiarate* «su richiesta» perché era la verità; qui si
> discute se debbano smettere di esserlo.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).

## Le domande

1. TA e Piste sono **su richiesta**: non sarebbe meglio un giro ogni 24 ore come le altre?
2. La pagina è **l'elenco di tutto ciò che viene importato**?

Risposta breve: **(1) sì, e costa poco**; **(2) no — mancano tre cose, due delle quali girano già da sole.**

> **Terza domanda, arrivata dopo la carta**: «fai anche per gli aeroporti la stessa cosa, ogni 24 ore come il
> resto». Risposta in **§C5**: fatto — e da lì in poi **nessuna** riga della pagina resta «su richiesta».

## Parte A — com'è fatta oggi la macchina degli import (misurato, non stimato)

### Le cinque categorie con una spunta + l'anagrafica ACC

| Riga in pagina | Chiave `ImportState` | Chi la gira | Cadenza | Sorgente |
|---|---|---|---|---|
| Anagrafica ACC | `Acc` | `AccImportHostedService` | `Ivao:AccImportHours` = 24h | IVAO `/v2/centers` + `/subcenters` |
| Transition Altitude | — | nessuno | **su richiesta** | IVAO `/v2/airports` (cache 12h) |
| Piste | — | nessuno | **su richiesta** | IVAO `/v2/airports/{icao}/runways` |
| Settori | `AirportSector` | `AirportSectorImportHostedService` | `Ivao:AirportSectorImportHours` = 24h | IVAO `/v2/ATCPositions/{compose}` |
| SID | `Sid` | `SidImportHostedService` | `Sectorfile:ImportHours` = 24h | GitHub Aurora (raw) |
| Aree regolamentate | `SpecialArea` | `SpecialAreaImportHostedService` | `Ivao:AccImportHours` = 24h | IVAO `/v2/centers/{icao}/specialAreas` |

Il gate della policy sta **nel corpo condiviso auto/manual** (`AirportSectorImporter`, `SidImporter`,
`SpecialAreaImportUseCase`, `SourceMergeInputs`), mai nel chiamante: è la regola chiusa il 22 agosto e vale
anche per ciò che si aggiunge qui.

### Da dove arrivano davvero TA e Piste

Un punto solo: `SourceMergeInputs.ReadAsync(policy, icao, …)` → `IAirportRepository.MergeFromSourceAsync`.
Lo chiamano **due** percorsi, entrambi manuali:

- `AirportEditingService.ReimportFromSourceAsync` — «Reimporta» nell'editor aeroporto, e il massivo
  «Reimporta tutti» di `/services/vsop/admin/airports`;
- `StructureEditingService.GenerateAirportDocumentCoreAsync` — «Genera documenti».

Quindi: **se nessuno preme un bottone, TA e piste non si aggiornano mai.** Una TA cambiata in AIRAC resta
vecchia a tempo indefinito, e nessuna riga della pagina lo segnala (la pill dice «su richiesta», che è vero
e inutile: non dice *quanto è vecchio* il dato rispetto alla sorgente).

### Il costo di un giro giornaliero, misurato sul `vipi.db` di sviluppo

```
Airports                                  = 92
Airports con TransitionAltitudeFt NULL    = 21
AirportRunways                            = 210
```

- **TA**: **1** chiamata HTTP per giro (`/v2/airports` paginato) — già in cache di processo 12h, condivisa.
- **Piste**: **1 chiamata per aeroporto** = **92/giro**.

Metro di paragone: il giro Settori fa già `1 + N_postazioni` chiamate per ognuno dei 92 aeroporti, cioè
diverse centinaia al giorno. Aggiungere 93 chiamate/giorno è un **+15/20% su un giro che c'è già**, una
volta al giorno. Non è un problema di carico.

### Cosa scrive `MergeFromSourceAsync` (per sapere cosa si automatizza)

- `TransitionAltitudeFt = ta` quando la TA arriva (mai a `null`: assenza = nessun cambio);
- per ogni pista di sorgente: aggiorna `LengthM`/`Bearing` se l'ident esiste, **aggiunge** se non esiste —
  **non cancella mai** una pista sparita dalla sorgente;
- `EnsureDefaultTransitionLevels` + `RecomputeDefaultBandLevels`: ricalcola i TL delle fasce **default**
  sulla TA nuova, lascia intatte le fasce personalizzate.

⚠️ Il **documento** non viene rigenerato. Vale già per Settori e SID (doc 03 §4.3: import e generazione sono
scollegati) e resta così: la pagina pubblica legge lo snapshot, quindi il dato nuovo si vede al prossimo
«Genera documenti» / rilascio. Automatizzare *anche* la rigenerazione è un'altra decisione, e tocca la
semantica di rilascio: **fuori da questo giro**.

### Perché non è distruttivo

Le due direzioni della policy restano quelle di sempre:

- **da sorgente** (spunta on): TA e piste sono già in **sola lettura** negli editor
  (`SetTransitionAltitudeAsync` e `SaveRunwaysAsync` lanciano `ValidationException`). Il giro automatico
  riscrive ciò che nessuno può aver scritto a mano. Non c'è lavoro editoriale da perdere.
- **manuale** (spunta off): `SourceMergeInputs` passa `null` / lista vuota, il merge legge «nessun cambio».
  Il giro automatico **non tocca niente**, esattamente come il bottone oggi.

Le colonne editoriali delle piste (TORA/LDA/APP/patterns/circling) non sono toccate dal merge in nessun caso.

## Parte B — cosa la pagina **non** elenca

Le sei righe sono «le categorie con una policy» + l'anagrafica ACC. Non sono «tutto ciò che entra». Manca:

### B1. Anagrafica aeroporti — importata, **solo su richiesta**, e non nominata

> ⚠️ **Deciso diversamente, stesso giorno.** Sotto (in «Cosa NON faccio») era scritto di lasciarla a mano
> perché crea entità. Il committente ha chiesto l'opposto — *«ogni 24 ore come il resto, così siamo sempre
> sicuri che in un giorno sia tutto up to date»* — ed è stata automatizzata: vedi **§C5**.

`AirportImportUseCase.RunAsync` (bottone «Assegna aeroporti noti» su `/services/vsop/admin/airports`) legge
`/v2/airports`, assegna alla ACC gli aeroporti nuovi e **importa subito il catalogo settori** di ognuno.
È l'unico modo in cui un aeroporto nuovo della divisione entra nel sito — e la pagina delle sorgenti non lo
menziona. È l'esatto gemello del difetto già corretto per l'anagrafica ACC (che una riga ce l'ha).

### B2. Shape TWR da GitHub + cerchi sintetici — girano ogni 24h dentro il giro Settori

Nel `ImportOnceAsync` di `AirportSectorImportHostedService`, dopo l'import: `IGithubTowerShapeService.ApplyAsync`
(poligoni reali da `DYNAMIC_SEC/twrs.tfl`) e `ITowerShapeFallbackService.ApplyAsync` (cerchio 5 NM sintetico).
Due sorgenti diverse — IVAO e GitHub — sotto un'unica riga che si chiama «Settori» e la cui descrizione parla
solo di postazioni e frequenze. Chi guarda una AoR sbagliata di una TWR non ha modo di sapere da questa
pagina che quel poligono viene da GitHub.

### B3. Catalogo punti (fix/VOR/NDB) — c'è il bottone, non la riga

Deciso così nel giro precedente e **resta così**: non tocca il database, vive in memoria, serve a suggerire e
a marcare i nomi. Ha già il suo tasto «Ricarica catalogo» e la spiegazione nel «?». Nota però che si
rinfresca **di fatto ogni 24h**, perché `SidImportHostedService` invalida `SectorfileCache` a ogni giro.

### Cosa non è un import, e non deve entrare nell'elenco

- **ATC online** (`AtcPollingHostedService`, 60s) e **meteo NOAA**: dato vivo in cache, non scrive nulla.
- **Roster staffisti** (`StaffRosterVerificationService`, 24h): sincronizza persone, non dati aeronautici;
  la sua pagina è Permessi.
- **`SpecialAreaForeignOptOut`** e **`TransferFlowsToAgreements`**: righe di `ImportStates` che sono
  segnaposti di riconciliazioni one-shot, non import. Già escluse (l'elenco è un'allowlist, non un dump).

## Parte C — cosa propongo di fare

Cinque slice, un commit ciascuna, `dotnet build Vipi.slnx -c Release --no-incremental` (0 avvisi) +
`dotnet test` su entrambi i TFM a ogni commit.

### C1. Il giro giornaliero di TA e Piste

**Un use case, non due.** `IAirportDataImportUseCase` in `Vipi.Application.Content`: legge la policy, se
**entrambe** le categorie sono escluse esce subito; altrimenti per ogni ICAO in
`IAirportSectorRepository.ListAirportIcaosAsync` chiama lo **stesso** `SourceMergeInputs.ReadAsync` +
`MergeFromSourceAsync` che usa il bottone. Zero secondo percorso: è la regola «un gate per categoria, non uno
per chiamante» applicata al motore.

- Fallimento **per-aeroporto** = `LogWarning` e si prosegue (lezione dell'import SID: a Debug un import rotto
  è rimasto invisibile per cicli interi). Fallimento **globale** (credenziali assenti, sorgente giù) =
  eccezione → `GatedImportLoop` scrive `MarkFailure` e riprova a 1h.
- `icaos.Count == 0` ⇒ ritorna `false`: non si consuma il gate quando gli aeroporti non ci sono ancora.

**Hosted service** `AirportDataImportHostedService` (Infrastructure/Ivao), `GatedImportLoop`,
`bootDelay: 50s` — dopo Acc (15s), Sid (30s), AirportSector (40s), SpecialArea (45s), perché lavora su
aeroporti che i giri precedenti hanno creato.

**Una chiave sola**: `ImportCategories.AirportData`, condivisa dalle righe TA e Piste.
⚠️ È onesto proprio perché il gate di policy sta nel merge: se l'admin esclude solo le Piste, la riga Piste
dice «Esclusa» (la policy vince, regola già presidiata dai test) e la riga TA continua a raccontare il suo
giro. Un errore a livello di loop è per definizione globale, quindi riguarda davvero entrambe.

**Cadenza**: nuova opzione `Ivao:AirportDataImportHours`, default **24**, con lo stesso `Math.Max(1, …)` che
`ImportSchedule.PeriodOf` deve applicare **identico** (se le due letture divergono la pagina annuncia un giro
che non esiste — è già scritto nel commento di `ImportSchedule` e va rispettato).

### C2. La pagina smette di dire «su richiesta» per TA e Piste

- `ImportOverviewService.Righe`: le due righe prendono `ImportCategories.AirportData` al posto di `""`.
- `ImportSchedule.PeriodOf`: nuovo case; il commento `_ => null // TA e Piste…` **va riscritto**, altrimenti
  resta un commento che mente (regola di propagazione, pre-flight §4).
- Test `ImportOverviewTests.Le_categorie_senza_giro_automatico_lo_dichiarano` **si ribalta**: diventa
  «TA e Piste dichiarano il loro giro», più il caso «Piste escluse ⇒ Esclusa mentre TA resta Aggiornata»,
  che è l'invariante nuova della chiave condivisa.
- `docs/guide/config.md`: la nuova opzione.

### C3. Le due righe che mancano

- **Anagrafica aeroporti**: riga senza spunta come quella ACC (`Categoria = null`), stato «su richiesta»,
  link a `/services/vsop/admin/airports`. ⚠️ Serve una seconda riga con `Categoria = null`: oggi
  `ImportOverviewRow` distingue l'anagrafica ACC **solo** dal `null`, e la pagina la nomina con
  `L["Sorg_AccLabel"]` in un `switch` che cade nel `_ =>`. Va introdotto un campo esplicito
  (`string NameKey`/`DescKey` nella riga, o un piccolo descrittore) invece di un secondo `null` ambiguo —
  altrimenti è il classico dispatch che si spacca al terzo caso.
- **Shape TWR**: *non* una riga nuova (non hanno una policy propria e girano dentro Settori), ma la
  descrizione della riga «Settori» dice anche «+ poligoni TWR da GitHub, cerchio 5 NM dove mancano».
  Una riga = una policy; la provenienza doppia si racconta nella descrizione, che è già il posto dove la
  pagina dice *quali colonne la sorgente possiede*.

### C4. Propagazione (stesso giro, non «dopo»)

- Guida in-app `GuidaPage`, sezione `admin-sorgenti`, **IT + EN**: il punto «Transition Altitude e Piste non
  hanno un giro automatico» diventa falso e va riscritto.
- `docs/feature/2026-08-22-sorgenti-cosa-fa-la-policy.md` §S6: nota di superamento con link a questa carta.
- `docs/index.md` + `docs/history/rounds.md` secondo la convenzione del giro.
- Memoria: aggiornare/creare la voce sulle sorgenti con la cadenza nuova e la chiave `AirportData`.

### C5. L'anagrafica aeroporti diventa un giro (aggiunta su richiesta del committente)

`AirportDirectoryImportHostedService` gira `IAirportImportUseCase` — lo **stesso** core del bottone «Assegna
aeroporti noti» — con `GatedImportLoop`, chiave `ImportCategories.AirportDirectory` e cadenza
`Ivao:AirportDirectoryImportHours` (default 24).

**Ordine, non estetica**: `bootDelay` **25 s**, subito dopo gli ACC (15 s) e **prima** di SID (30 s), settori
(40 s) e TA/piste (50 s). Un aeroporto si assegna a una ACC che deve già esistere, e i tre giri che vengono
dopo iterano gli aeroporti che questo ha creato: nell'ordine sbagliato uno scalo nuovo resterebbe senza
settori e senza piste fino al giorno seguente.

**Perché è sicuro**, misurato in `EfStructureEditingRepository.AutoAssignAirportsAsync`: è **additivo** —
salta gli ICAO già in archivio, non rimuove e non riassegna. Un aeroporto tolto dall'anagrafica della sorgente
resta dov'è (e deve: sopra ci può stare del lavoro editoriale), e si toglie a mano.

⚠️ **Resta l'unico giro che crea**, ed è l'unica cosa che lo distingue dalle altre sei righe: la sua
descrizione a video lo dice (*«è l'unico giro che crea: aggiunge, non toglie mai»*), perché un giro che crea
non deve essere una sorpresa per chi guarda la pagina fra sei mesi.

Il fallimento dell'import settori di un aeroporto **appena assegnato** è un warning, non un errore del giro:
quell'aeroporto è comunque nato, e il giro dei settori ci ripassa quindici minuti dopo.

Test: la riga non dice più «su richiesta» ma dichiara cadenza e prossimo giro; e un test nuovo pretende che
**nessuna** delle sette righe resti senza cadenza — è il posto in cui una riga aggiunta domani senza giro si
fa notare subito.

## Cosa NON faccio, e perché

- **Non rigenero i documenti in automatico.** Import e generazione sono scollegati per scelta (doc 03 §4.3);
  legarli qui vorrebbe dire far muovere lo snapshot pubblico senza che nessuno lo rilasci.
- ~~**Non rendo automatica l'assegnazione degli aeroporti.**~~ **Ribaltato dal committente lo stesso giorno**
  (vedi §C5): la decisione era sua, e la richiesta è che in un giorno tutto sia aggiornato. Resta vero il
  motivo per cui era stata messa da parte — **crea** entità — e per questo la riga lo dichiara a video.
- **Non aggiungo un «importa adesso» su questa pagina.** I trigger stanno dove l'oggetto vive; la riga porta
  già il link.
- **Non tocco `GatedImportLoop`.** Marcare il successo con la categoria esclusa è corretto per il gate: è il
  racconto a doversi correggere, e si corregge dove si racconta.

## Rischi

- **La TA in produzione si muove da sola.** Oggi in dev 21 aeroporti su 92 hanno `TransitionAltitudeFt` a
  `NULL`: al primo giro si popoleranno, e `RecomputeDefaultBandLevels` ricalcolerà i TL di fascia default.
  È l'effetto voluto, ma è un cambiamento **visibile** su un dato operativo: da guardare in
  `/services/vsop/admin/sources` **prima** del deploy — e la riga «deciso da / mai deciso» è lì apposta.
- **La policy in produzione potrebbe non essere quella che si crede.** In dev la tabella `ImportPolicies` è
  **vuota** (nessuna riga: i valori a video vengono dai default delle colonne). Se in produzione qualcuno ha
  escluso Piste o TA, il giro nuovo non farà niente per quella categoria — corretto, ma va **visto** prima,
  non dedotto.
- **Piste sparite dalla sorgente non spariscono dal sito.** Il merge non cancella. Con un giro giornaliero
  la cosa non peggiora, ma diventa permanente invece che occasionale: se un domani serve il prune, è una
  decisione a sé (come il prune delle aree, che invece c'è).
