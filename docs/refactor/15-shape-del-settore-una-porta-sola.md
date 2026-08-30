# 15 — La shape di un settore: una porta sola 🟡

> **Cosa**: la forma di un settore — **anello e quote insieme** — smette di essere una colonna che tiene un
> anello solo e diventa un **elenco di pezzi con una fonte**. Chiunque la voglia leggere passa da **una
> porta**, e non esiste più un motore che disegni il confine dell'AIP e attribuisca il traffico su quello di
> IVAO.
>
> **Perché adesso**: l'aggancio agli spazi aerei dell'AIP (carta
> [2026-08-29-spazi-aerei-dal-kmz.md](../feature/2026-08-29-spazi-aerei-dal-kmz.md), §AA) è onorato da **due
> motori su sei**. Non è un difetto isolato: è il modello che lo permette.
>
> **Regola d'oro di questa carta**, e va letta prima di tutto il resto:
> **un import scrive solo i pezzi della PROPRIA fonte, e non cancella mai quelli di un'altra.**
> Il giorno che cade, l'unbind smette di essere reversibile e nessun test se ne accorge — è la trappola delle
> shape vuote del 26 agosto con un vestito nuovo. Per questo in §3d non è una raccomandazione ma una **firma
> di metodo**.

**Stato:** 🟡 **in esecuzione** · target approvato dall'owner il 30 agosto 2026 · **S0→S10 fatte**, resta S11 e la verifica dal vivo (§5).

---

## 1. Stato rilevato

### 1a. Dove vivono le forme, oggi

| Cosa | Dove | Note |
|---|---|---|
| Forma IVAO di un settore ACC | `AccSectors.RegionMapPolygon` + `LowerLimit`/`UpperLimit` | **un anello**, quote come `int?` grezzi |
| Forma IVAO di una posizione d'aeroporto | `AirportSectors.<idem>` | idem, più `IsShapeSynthetic` (cerchio TWR) |
| Gate AIRAC | `RegionMapPolygonInForce`, `ShapeAiracCycle`, `ShapeForcePublished`, `ShapeSource` | due colonne gemelle: la corrente e quella **ancora in vigore** |
| Spazi aerei dell'AIP | `AirspaceVolumes` (+ `AirspaceImports`) | anello 2D, `BaseFeet`/`TopFeet` **e** il datum (`Gnd`/`Amsl`/`Agl`/`FlightLevel`) |
| L'aggancio settore → volumi | `SectorAirspaceBindings` | chiave naturale `FAMIGLIA|NOME|BASE|TETTO` + ordinale + `Position` |
| Proiezione | `Sectors` | **non** porta la shape: la legge dai cataloghi |

`ShapeSource` (enum in `Vipi.Domain/Enums.cs`) vale già `Source` · `Sectorfile` · `Synthetic` · `Aip`.

### 1b. Chi legge la forma, e chi onora l'aggancio

| Motore | File | Onora l'aggancio |
|---|---|---|
| AoR del vIPI ACC | `AccDerivationService.DeriveAorViewAsync` | ✅ sì |
| AoR del vIPI APP | `AppDocumentService.GetAorViewAsync` | ✅ sì |
| Viewer 3D | `AccAor3d.razor` (banda dai due sopra) | eredita |
| Mappa della vLOA | `VloaDerivationService` | ❌ no |
| Attribuzione del traffico | `SectorVolumeMap` → `AtcTrafficRecorder` | ❌ no |
| Confinanti | `NeighbourAdjacencyComputer` | ❌ no |
| Pagina pubblica spazi aerei | `AirspaceMap` (i settori sovrapposti) | ❌ no |
| Struttura ACC / editor aeroporto | `AccAdminPage`, `AeroportoEditorPage` | ❌ no |

### 1c. Le cifre — contate sul `vipi.db` reale, 30 agosto 2026

- **153** righe `AccSectors` (**142** con poligono) · **192** `AirportSectors` (**142** con poligono).
- **1 536** volumi in `AirspaceVolumes`, **tutti** del caricamento in vigore.
- **2** righe d'aggancio, su **1** callsign: `LIBA_APP` → `AMENDOLA CTR Z1` e `Z2`.
- **13** `AirportSectors` con `ShapeSource='Aip'`: le torri il cui ATZ è stato **scritto nella colonna** (S4 di §AA).
- **0** righe con una shape in attesa del gate (`RegionMapPolygonInForce` nullo ovunque, `ShapeAiracCycle` pure).
- Quote: **138 su 153** settori ACC non hanno tetto; **50 su 192** posizioni d'aeroporto non hanno né base né
  tetto; `LimitsFromSource` è **falso su tutte e 192**.
- **33** `NeighbourCandidates` · **21 993** sessioni ATC, **1 853** righe di traffico.
- **115** riferimenti a `RegionMapPolygon` in **36** file.

---

## 2. Problemi

### ⛔1 — Laterale e verticale possono venire da fonti diverse

Nei quattro motori che non conoscono l'aggancio, un settore agganciato **disegna** il confine dell'AIP nel
documento e **rivendica** il traffico dentro il poligono di IVAO. Sono due verità sullo stesso oggetto, e
quella sbagliata non produce nessun errore: produce numeri.

### ⛔2 — La colonna tiene un anello, quindi le quote diventano un inviluppo

`LIBA_APP`, misurato: IVAO dice `0 → 19500`. L'AIP dice **Z1** `GND → FL105` e **Z2** `7000 FT AMSL → FL195`.
L'inviluppo (base minima, tetto massimo) dà `GND → FL195`, cioè **esattamente il monoblocco di IVAO**: il
viewer 3D disegna un parallelepipedo unico, e il volume rivendica il cielo sopra Z1 e quello sotto Z2 che il
CTR **non ha**. Con sette zone (Catania) il difetto cresce con la differenza fra le bande.

### ⚠️3 — Per 13 torri l'AIP scrive già nella colonna, e non si torna indietro

Le ATZ di S4 sostituiscono il cerchio di ripiego **scrivendo** `RegionMapPolygon` e marcando
`ShapeSource='Aip'`. Non c'è nessuna riga d'aggancio: la forma di prima non esiste più, e «togliere l'ATZ»
vuol dire ri-generare un cerchio, non ripristinare. È lo stesso motivo per cui **un ICAO con più di un ATZ si
salta** (Guidonia, Torino Aeritalia).

### ⚠️4 — Il gate AIRAC vive su due colonne gemelle

`RegionMapPolygon` (la corrente, che può essere in attesa) + `RegionMapPolygonInForce` (quella che intanto
resta pubblicata) funziona **per una forma sola**. Con N pezzi servirebbero due elenchi paralleli: è il punto
in cui il modello attuale smette di poter crescere.

### ⚠️5 — Le adiacenze sono calcolate e salvate

`NeighbourCandidates` si riempie a un giro d'import (`ImportAndComputeAsync`). Una forma che cambia oggi
propaga sui confinanti **al prossimo giro (24 h) o alla pressione del tasto**. Vale già adesso, senza aggancio.

### ⚠️6 — Le statistiche non sanno con quale forma hanno contato

`AtcSessionTraffic` non porta la fonte della forma. Quando l'attribuzione passerà all'AIP, i numeri di prima e
di dopo non saranno confrontabili, e nel grafico resterà un gradino che nessuno saprà spiegare.

### 🔸7 — Niente impedisce a un import di cancellare i pezzi di un'altra fonte

Oggi non serve una guardia perché la colonna è una sola. Nel modello a pezzi, senza guardia, il primo import
che fa «cancella tutto e riscrivi» rende l'unbind irreversibile **in silenzio**.

---

## 3. Architettura target 🟢

### 3a. `SectorShapeParts`: la forma è un elenco di pezzi con una fonte

```
SectorShapeParts
  Id                int      PK
  Catalog           enum     SourceCatalog (Subcenter | AirportPosition)
  SectorId          int      id nel catalogo: l'indirizzo vero (i callsign si rinominano)
  Callsign          string   maiuscolo, come si cerca e come si mostra
  Source            enum     ShapeSource — RIUSATO, non un gemello (Source | Sectorfile | Synthetic | Aip)
  State             enum     ShapePartState: InForce | Pending
  Ordinal           int      ordine di disegno dentro la fonte
  PolygonJson       string   un anello, forma `regionMapPolygon`
  BaseFeet          int?     null = suolo
  TopFeet           int?     null = illimitato
  BaseDatum         enum     AirspaceDatum — RIUSATO
  TopDatum          enum     AirspaceDatum
  BaseRaw           string   come lo dice la fonte: «GND», «7000 FT AMSL», «FL105»
  TopRaw            string
  AiracCycle        string?  non nullo = in attesa del ciclo (solo su Pending)
  ForcePublished    bool     pubblica in anticipo, per decisione umana
  SourceRef         string?  la chiave naturale del volume AIP, o null
  WrittenUtc        datetime
  indice unico: (Catalog, SectorId, Source, State, Ordinal)
```

⚠️ **Le quote stanno DENTRO il pezzo.** È tutto il progetto: chi ha in mano un anello ha già in mano le sue
quote, e «prendere il laterale da una fonte e il verticale da un'altra» non è una cosa da evitare — è una cosa
che **non si può scrivere**.

### 3b. Le generazioni al posto delle colonne gemelle (⚠️4)

Il gate AIRAC diventa lo `State`: la fonte scrive un insieme `Pending` col suo `AiracCycle`; alla maturazione
l'insieme `InForce` di **quella fonte** viene sostituito dal `Pending`. Un insieme per (fonte, stato): niente
colonne parallele, e N pezzi sono gratis. `PromoteDueShapesAsync` promuove **insiemi** invece di righe.

### 3c. La porta di lettura, unica

```csharp
public interface ISectorShapeResolver
{
    Task<IReadOnlyDictionary<string, SectorShape>> ResolveAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default);
}

public sealed record SectorShapePart(
    string PolygonJson, int? BaseFeet, int? TopFeet,
    AirspaceDatum BaseDatum, AirspaceDatum TopDatum, string BaseRaw, string TopRaw);

public sealed record SectorShape(
    string Callsign, ShapeSource Source, IReadOnlyList<SectorShapePart> Parts,
    IReadOnlyList<string> UncoveredKeys);
```

Tre regole, in un posto solo:

1. **La precedenza**, quattro gradini — ⚠️ **corretta in esecuzione da un test rosso**: la prima stesura metteva
   i pezzi dell'AIP sopra il catalogo, e così l'ATZ automatica avrebbe scavalcato il **sectorfile**, che è fonte
   primaria per decisione del committente (*«l'AIP solo se non ce l'hai»*, 29 agosto 2026).

   | # | Gradino | Perché lì |
   |---|---|---|
   | 1 | **L'aggancio all'AIP**, risolto sul caricamento in vigore | è il gesto di una **persona** |
   | 2 | Una shape **vera** del catalogo (sectorfile o anagrafica) | fonte primaria |
   | 3 | I **pezzi in archivio** (`SectorShapeParts`: oggi l'ATZ automatica) | ripiego |
   | 4 | Il **cerchio sintetico** da 5 NM in colonna | ripiego dell'ultimo minuto |

   «Vera» vuol dire *non `IsShapeSynthetic`*. Il gate AIRAC vale sui gradini 2 e 4: durante il congelamento di
   una release danno la geometria in vigore **a quel ciclo**, non l'ultima disegnata.
2. **L'assenza non cancella**: un gradino che non dà niente fa scendere a quello sotto, mai a «nessuna forma».
   Un aggancio scoperto lascia `UncoveredKeys` pieno e la pagina lo dice.
3. **Le quote seguono l'anello**, sempre. Nessun inviluppo: chi ha bisogno di una banda sola (la legenda, o
   l'ordinamento fra due volumi che si contendono un aeroplano) se la calcola sapendo che sta approssimando.

### 3d. La porta di scrittura non sa esprimere la violazione (🔸7)

```csharp
Task ReplacePartsAsync(SourceCatalog catalog, int sectorId, ShapeSource source, ShapePartState state,
                       IReadOnlyList<SectorShapePart> parts, CancellationToken ct = default);
Task ClearPartsAsync(SourceCatalog catalog, int sectorId, ShapeSource source, CancellationToken ct = default);
```

- La cancellazione interna è **sempre** `WHERE SectorId = @id AND Source = @source AND State = @state`.
- `parts` **vuota non cancella niente** e torna «sorgente muta» (la regola del 26 agosto, dentro il metodo).
- Svuotare è un gesto **separato ed esplicito**, che chiamano solo l'unbind e la pagina che elimina.
- Il repository è l'unico che tocca la tabella: violare la regola d'oro richiede **un metodo nuovo**, che si
  vede in revisione.

### 3e. Geometria a N pezzi

`SectorVolume` (un anello + una banda) diventa `SectorVolumeSet`: N coppie (anello, banda), `Contains` = dentro
**uno qualunque**; l'adiacenza dei confinanti è vera se **un pezzo qualunque** è adiacente. Puro,
deterministico, test-first.

### 3f. L'aggancio resta il puntatore

Nessun terzo stato: `SectorAirspaceBindings` continua a dire **quali volumi** un settore mostra, e i pezzi
`Aip` ne sono la materializzazione. Unbind = si cancellano le righe d'aggancio **e** i pezzi `Aip` di quel
settore; i pezzi delle altre fonti non sono mai stati toccati, quindi il ripristino è immediato e non richiede
nessun re-import.

### 3g. Quando il cambio diventa vero

| Consumatore | Ritardo |
|---|---|
| AoR ACC/APP, vLOA, `/services/airspace`, Struttura, editor | al primo render |
| Traffico | **≤ 60 s**: la cache del catalogo (`AtcTrafficRecorder.CatalogTtl`, un'ora) viene **invalidata** alla scrittura |
| Confinanti | **secondi**: bind e unbind ricalcolano le adiacenze **dal catalogo estero già in archivio**, senza richiamare IVAO |
| Documenti pubblicati | alla **ripubblicazione** (la release congela): un aggancio alza una riga in «Documenti da rivedere» |

### 3h. Il traffico si timbra (⚠️6)

`AtcSessionTraffic` porta la fonte della forma con cui è stato attribuito, e la pagina statistiche lo dice. Un
gradino nei numeri deve essere **spiegabile**, non misterioso.

### 3i. Il datum in un posto solo

`Gnd`/`Amsl`/`Agl`/`FlightLevel` → FL avviene **nel risolutore**, mai nei motori. Regola dichiarata: **AGL si
tratta come AMSL**, perché il terreno non ce l'abbiamo; sta scritto qui, e il testo della fonte resta visibile
(`7000 FT AMSL`) accanto al numero.

### 3j. Cosa NON cambia

Gli import (ACC, aeroporti, sectorfile Aurora, GitHub) continuano a scrivere **la propria fonte**; la release
continua a congelare; la vIPI e la vLOA restano documenti derivati. Cambia **da dove leggono**, non chi sono.

---

## 4. Passi di migrazione

| # | Slice | Migrazioni | Quando |
|---|---|---|---|
| S0 | Questa carta + riscrittura della §6-bis di §AA, che oggi dice l'opposto | — | prima della consegna |
| S1 | Test di **caratterizzazione** sui quattro motori, coi dati del `vipi.db` vero | — | prima |
| S2 | `SectorShapeParts` + `ShapePartState` + repository con le due firme di §3d + i **tre test della regola d'oro** | **1** | prima |
| S3 | L'**ATZ delle torri** smette di abitare la colonna e diventa un insieme di pezzi `Aip` (⚠️3 chiuso): reversibile, N zone, e con le **quote** che il cerchio non aveva. ⚠️ Le colonne del catalogo restano dove sono fino a S11, e il risolutore le legge di lì | — | prima |
| S4 | `SectorVolumeSet`: geometria a N pezzi, pura, test-first | — | prima |
| S5 | `ISectorShapeResolver` + implementazione EF | — | prima |
| S6 | AoR ACC + APP passano dal risolutore (comportamento invariato, un percorso solo invece di due) | — | prima |
| S7 | vLOA dal risolutore | — | prima |
| S8 | ✅ UI: provenienza a schermo (`ShapeSourcePill`) e limiti scavalcati sbiaditi | — | fatta |
| S9 | ✅ **Traffico**: risolutore + N pezzi + `ShapeChangeStamp` (≤ 60 s) + timbro della fonte sulla tratta | **1** | fatta |
| S10 | ✅ **Confinanti**: adiacenza su un pezzo qualunque, dedup per coppia, ricalcolo dall'archivio al bind/unbind | — | fatta |
| S11 | Le colonne gemelle del gate escono di scena — ⚠️ **non è una slice, è un capitolo**: vedi §4-bis | **2** | **dopo la consegna** |

### Stato dell'esecuzione — 30 agosto 2026

✅ **S0 → S10 fatte**, ramo `shape-una-porta-sola`, un commit per slice. 🟡 Resta la sola **S11** (le colonne
gemelle del gate escono di scena), che va **un rilascio dopo** — e la **verifica dal vivo** su LIBA e LICC.

**Tutti e sei i motori passano dalla porta unica.** La tabella §1b si legge adesso così:

| Motore | Onora l'aggancio |
|---|---|
| AoR vIPI ACC · AoR vIPI APP · viewer 3D | ✅ S6, con la banda **per pezzo** |
| Mappa della vLOA | ✅ S7 |
| Attribuzione del traffico | ✅ S9, e ogni tratta scrive **con quale forma** è stata contata |
| Confinanti | ✅ S10, adiacente se lo è **un pezzo**, e si ricalcola al bind/unbind |
| ATZ delle torri | ✅ S3, non abita più la colonna: è reversibile e prende **tutte** le zone |
| Struttura ACC · editor aeroporto | ✅ S8, la provenienza si **vede** e i limiti scavalcati si sbiadiscono |

Quel che è cambiato rispetto alla carta, e perché:

- **§3c**: la precedenza è di **quattro** gradini, non tre (vedi sopra). L'ha imposta un test rosso, non un
  ripensamento: `Il_Sectorfile_Si_Riprende_Una_Torre_Che_Laip_Aveva_Riempito`.
- **S3**: l'ATZ scrive **pezzi**, non un aggancio. Un aggancio è la scelta di una persona e sta al primo
  gradino; l'ATZ automatica è un ripiego e deve stare sotto il sectorfile.
- **La tabella `SectorShapeParts` ha già un abitante** (l'ATZ). Le colonne del catalogo restano al loro posto
  fino a S11: fino ad allora il risolutore le legge come quarto e secondo gradino.
- **`AppAorPolygon`** ha guadagnato `LowerFl`/`UpperFl` — in coda e facoltativi, perché gli snapshot di
  release già congelati non li hanno e devono continuare a leggersi — e il viewer 3D estrude **ogni anello**
  alla sua quota (`ringFl`).

⚠️ **S9 e S10 stanno dopo la consegna del 1° settembre** per una ragione sola: cambiano **dati derivati** e
sbagliano **senza che si veda a schermo**. Un settore che rivendica meno cielo produce numeri più bassi, e
sembrano solo un mese fiacco.

⚠️ Le migrazioni in coda al cutover MariaDB sono passate da **34** a **36** (`PezziDiForma` in S2,
`FormaCheHaContato` in S9); la S11 sarà la trentasettesima e va **un rilascio dopo**, mai insieme.

⚠️ **La trappola del valore di partenza, evitata sul filo**: EF genera `defaultValue: ""` per una colonna
`enum`-a-stringa, e le **1 853** tratte già in archivio sarebbero diventate illeggibili al primo caricamento.
Tutt'e due le migrazioni portano `Source` scritto a mano — che è anche la verità: sono state contate con la
forma dell'anagrafica. È la gemella della trappola del `bool` che nasce `false` ovunque.

---

## 4-bis. S11 — le colonne gemelle, e perché non è una slice

**Misurato il 30 agosto 2026, non stimato.** Le colonne del catalogo spariscono solo se **ogni** fonte scrive
i pezzi: finché una sola continua a scrivere `RegionMapPolygon`, il risolutore deve continuare a leggerlo, e
la coppia `RegionMapPolygon`/`RegionMapPolygonInForce` resta dov'è.

### I siti di scrittura — otto, contati

| # | Chi scrive | Dove | Diventa |
|---|---|---|---|
| 1 | Import ACC (anagrafica IVAO) | `EfAccAdminRepository` (upsert, 4 punti) | pezzi `Source` |
| 2 | Import posizioni d'aeroporto | `EfAirportSectorRepository.ImportForAirportAsync` + `AirportSectorImporter` | pezzi `Source` |
| 3 | Sectorfile Aurora | `EfSectorShapeRepository.ApplyShapeAsync` | pezzi `Sectorfile`, **`Pending` + ciclo** se la geometria è nuova |
| 4 | Torri da GitHub (`twrs.tfl`) | `EfAirportSectorRepository.SetRealShapeAsync` | pezzi `Sectorfile` |
| 5 | Cerchio di ripiego 5 NM | `EfAirportSectorRepository.SetSyntheticShapeAsync` | pezzi `Synthetic` |
| 6 | Settori esteri (import confinanti) | `EfNeighbourRepository.PersistForeignCatalogAsync` | pezzi `Source` |
| 7 | Poligono incollato a mano (candidato) | `EfNeighbourRepository.SetPolygonAsync` | resta dov'è: è del **candidato**, non di un settore |
| 8 | ATZ dell'AIP | `AtzTowerShapeService` | ✅ **già fatto in S3** |

### Le letture che vanno spostate

- `EfAccDerivationRepository.SectorPolygonsRawByCallsignAsync` — ⚠️ **è la lettura del congelamento di
  release**: decide quale geometria finisce dentro un documento **pubblicato**. È il punto più delicato di
  tutta la carta, e sbagliarlo non si vede a schermo: si vede quando qualcuno pubblica.
- `ShapeAiracGate` + `ShapeGateNotice` + `EfShapeGateRepository` — il gate diventa lo `State` (§3b), e
  `PromoteDueShapesAsync` promuove **insiemi** invece di righe.
- `SectorShapeFallbackService` e `ReleaseService`, che citano il gate.

### L'ordine, e le due migrazioni

1. **Backfill**: ogni riga di catalogo con una shape diventa un insieme di pezzi della sua fonte
   (`ShapeSource` ce l'ha già in colonna) — e la geometria in attesa diventa un insieme `Pending`.
2. Le otto scritture passano alla porta dei pezzi, **una per volta**, con la sua verifica.
3. Le letture passano ai pezzi; il risolutore perde il gradino del catalogo e `SinteticheAsync`.
4. **Una release dopo**, la migrazione che **droppa** le colonne. Mai insieme al resto: è la regola già
   pagata con `AirportExtraSection`.

⚠️ **Perché non è stata fatta insieme alle altre**: tocca il percorso che decide cosa contiene un documento
pubblicato, il giorno prima della consegna del 1° settembre. Le altre dieci slice si vedono a schermo e sono
state verificate dal vivo; questa no. Si esegue **dopo** la consegna, con la stessa disciplina: una slice per
sito, e una pubblicazione vera guardata nella copia del `vipi.db`.

## 5. Impatto e verifica

### ✅ Verifica dal vivo — eseguita il 30 agosto 2026

App vera su una **copia** del `vipi.db` (skill `verifica-live`), guidata con Edge+puppeteer. Otto controlli.

| # | Cosa | Esito |
|---|---|---|
| 1 | `LIBA_APP`, agganciato a due zone | **2 anelli**, `ringFl [[0,105],[70,195]]` contro l'inviluppo `[0,195]`: **due gradini**, non un blocco |
| 2 | `LICC_APP` agganciato **dal vivo** alle sue sette zone, dalla pagina | **7 anelli**, `[[0,35],[0,35],[35,195],[45,195],[130,195],[20,195],[65,115]]` |
| 3 | **Sgancio** di `LIBA_APP` dalla pagina | 1 anello, `[[0,195]]` — la forma di IVAO al primo render, **senza ri-importare niente** |
| 4 | **Il traffico**: `LIRR_EW_CTR` (in frequenza, 4 aerei) agganciato alla **FIR ROMA** `GND→FL195` alle 14:06:57Z | vedi sotto |
| 5 | **Confinanti**: ricalcolo dall'archivio | due giri, `27 domestici × 21 ACC esteri → 33 coppie`; i **33** candidati invariati |
| 6 | **Struttura ACC**, la colonna della provenienza | 153 righe: **142 «IVAO» + 11 «nessuna»**, esattamente le cifre di §1c |
| 7 | **Editor aeroporto** di LIBA | `LIBA_APP` → **«AIP · 2»** con le due caselle dei limiti **barrate e sbiadite**; `LIBA_TWR` → «IVAO», caselle normali |
| 8 | **Un documento già PUBBLICATO** (`LIBA_APP` v1, congelato *prima* di questa carta) | 200, disegna, `ringFl [[0,195]]`: lo snapshot vecchio non ha le bande per poligono e **ricade sull'inviluppo**, come previsto |

⚠️ **Il controllo 4 è quello che vale il capitolo.** Entro **un solo giro del poller** le tratte nuove sono
uscite col timbro `Aip`, e sono i quattro voli **sotto** FL195 (16, 2 613, 91, 3 652 ft). I quattro
**sopra** — 39 081, 43 006, 21 661, 28 049 ft — hanno smesso di essere rivendicati: il settore ha tetto
**UNL** nell'anagrafica, la FIR si ferma a FL195. È il difetto di ⚠1, visto dal verso giusto, sui dati veri
della rete. E prova insieme le tre cose: i pezzi con la loro banda, il timbro in archivio e il gettone
(≤ 60 s invece di «fra zero e sessanta minuti»).

⚠️ **Quel che la verifica ha corretto**: la pastiglia della provenienza andava **a capo** («AIP · 2» su due
righe) e alzava la riga della tabella. Si vede solo guardando lo screenshot, ed è il motivo per cui il
runbook chiede di guardarli.

⚠️ **Non verificato dal vivo**: la conversione delle **13 torri ATZ** (S3). Il giro d'import delle posizioni
d'aeroporto — che è chi la esegue — non è partito in questa sessione, e forzarlo avrebbe voluto dire una
ri-lettura completa dell'anagrafica IVAO. La coprono nove test, fra cui lo sgancio che **riporta il cerchio**;
resta da **guardare al primo deploy**: le 13 righe con `ShapeSource='Aip'` in colonna devono diventare pezzi,
e la colonna deve tornare libera.

**Rollback**: fino a S8 compresa nessun dato di IVAO viene toccato — si torna indietro cancellando i pezzi
`Aip`. Da S9 in poi il rollback non riscrive le tratte già attribuite: è voluto, e il timbro di §3h dice sempre
con quale forma sono state contate.

**Definition of Done**: quella del [FEATURE-PROCESS](../FEATURE-PROCESS.md), più le tre voci di questa carta —
i test della regola d'oro (§3d), la riga del rapporto di consistenza «settori con aggancio attivo e zero
pezzi», e la §6-bis di §AA riscritta nello stesso giro.
