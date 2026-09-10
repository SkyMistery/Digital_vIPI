# Il ripiego di un settore sovrapposto si risolve sul PUNTO, non sul padre

**10 settembre 2026.** Carta di lavoro. Un settore che copre **tutto** un ACC — `MIL`, e con lui `FSS` —
non è un sottoalbero di nessuno: sotto di lui ci sono `ES2` **e** `WS2`, e oltre il confine `LIPP`. Un
`ParentCallsign` è **un** puntatore, e non può nominarli tutti. Oggi `LIMM_MIL_CTR` chiuso manda il traffico
sempre a `LIMM_WS2_CTR`, da qualunque cedente e per qualunque punto — giusto per metà dei casi, sbagliato
per l'altra metà, **senza dirlo**.

Questa carta aggiunge un bersaglio di ripiego che non è un callsign ma una **domanda**: «chi copre questo
punto, adesso, a questa quota». Non è un secondo albero: è lo **stesso** albero, interrogato dall'altro capo.

Segue e non sostituisce [`2026-08-31-ricaduta-verticale-e-cicli.md`](2026-08-31-ricaduta-verticale-e-cicli.md),
che ha dato alla ricaduta la **quota**. Qui le si dà il **luogo**.

---

## Parte 1 — Il fatto, dalla produzione

Letto su `atc.it.ivao.aero` il 9 settembre 2026 (sessione del committente; pagine Struttura, ACC,
Trasferimenti).

### L'albero di LIMM, dai rientri veri

```
LIMM_WS2_CTR  ← radice
├─ LIMM_ES2_CTR
│  ├─ LIMM_ES5_CTR        + riga dichiarata: FL325–UNL → LIMM_WS5_CTR
│  ├─ LIMP_APP            → LIMP
│  └─ LIPX_ES0_APP
│     ├─ LIPL_G_APP       → LIPL (Ghedi)
│     └─ LIPO, LIPX
├─ LIMM_FSS               → LIMW
├─ LIMM_MIL_CTR
├─ LIMM_WS5_CTR
├─ LIMC_ANE_APP
│  ├─ LIMC_ANW_APP        → LIMC, LIMN (Cameri)
│  ├─ LIMC_ASW_APP → LIMC_MAR_APP
│  ├─ LIME_ADE_APP → LIME
│  └─ LIML_LAR_APP → LIML
└─ LIMF_WW0_APP
   ├─ LIMF_WN0_APP → LIMA, LIMF, LIMZ
   └─ LIMJ_WS0_APP → LIMG, LIMJ
```

### Le catene, lette una per una dal pannello

| settore | catena in produzione |
|---|---|
| `LIMM_MIL_CTR` | → `LIMM_WS2_CTR` (PADRE). **Nient'altro.** |
| `LIMM_ES5_CTR` | passo 1: **FL325–UNL → `LIMM_WS5_CTR`** (riga scritta) *e* `LIMM_ES2_CTR` (PADRE); passo 2: `LIMM_WS2_CTR` |
| `LIMM_WS5_CTR` | → `LIMM_WS2_CTR` (PADRE) |
| `LIMM_ES2_CTR` | → `LIMM_WS2_CTR` (PADRE) |
| `LIMC_ANE_APP` | → `LIMM_WS2_CTR` (PADRE) |
| `LIPX_ES0_APP` | → `LIMM_ES2_CTR` → `LIMM_WS2_CTR` |

### Gli accordi

**4 accordi, ~15 clausole, tutte su LIMF/LIMZ. Zero clausole con `MIL`.** «Lacune: 37». I casi di cui parla
questa carta **non sono ancora scritti**: non c'è niente da migrare, si progetta libero.

I CoP reali di quelle clausole: `NELAB, VOG, ITCAP, LAGEN, EGHIN, KUMIN, VEROB, SIRLO, MMP, ASTIG, IXUSA,
KUKEV, TESTO, TOP` — **tutti fix o VOR**, cioè tutti punti con una posizione. Il rinvio, su Milano, non
nascerebbe cieco.

### Tre difetti trovati per strada (si chiudono a mano, non dipendono da questa carta)

1. 🔴 **`LIRR_MIL_CTR` è una RADICE in produzione.** Il MIL di Roma chiuso manda su **UNICOM**, non sul
   civile. Stessa cosa per `LIRR_FSS` e `LIRR_PLN_FSS`.
2. Roma ha **cinque radici** (`EW`, `NE1`, `SU`, più i due FSS): `LIRR_NE1_CTR` chiuso non ricade su
   nessuno. Da guardare col committente — è una scelta, non un difetto deducibile.
3. ⚠️ **Il `vipi.db` di sviluppo diverge dalla produzione** proprio sul caso che conta: da noi
   `LIMM_ES5_CTR` è figlio di **WS5** (in produzione di **ES2**), e l'unica riga di ripiego locale è
   `LIMM_WS5_CTR → LIMM_ES2_CTR sopra 32 500 000 ft` — tre ordini di grandezza sbagliati. **Ogni prova
   scritta sul DB locale su questo caso sta provando una struttura che non esiste.** Da riallineare prima
   di scrivere il primo test.

---

## Parte 2 — Le due famiglie, e quale è già risolta

I casi elencati dal committente si dividono in due, e la seconda ha **una** causa.

### Famiglia «dipende dalla QUOTA» — già risolta, non si tocca

I cinque assetti d'ACC di Milano funzionano **oggi**, verificati contro l'albero e le righe di produzione:

| assetto | come lo produce il sistema | esito |
|---|---|---|
| WS2 solo → 0–UNL su tutto | ES2→WS2, WS5→WS2, ES5→(WS5 offline)→ES2→WS2 | ✅ |
| WS2+ES2, tutt'e due 0–UNL | ES5 sale a **ES2** (padre), WS5 sale a WS2 | ✅ **solo perché ES5 è figlio di ES2** |
| WS2+WS5 → 0–FL325 / FL325–UNL | ES5 sale a WS5 per la **riga FL325–UNL**, non al padre | ✅ |
| WS2+ES2+WS5 | idem, l'alto est va a WS5 | ✅ |
| tutt'e quattro | banale | ✅ |
| ES2 chiuso → tutto a WS2 | catena di ES2 = [WS2] | ✅ |

⚠️ **Due di questi cinque poggiano su UNA riga** (`LIMM_ES5_CTR`, FL325–UNL → WS5). Chi la cancella non
vede nessun errore: il traffico continua a ricadere, sul settore sbagliato. → rilievo, Parte 8.

Nota sul meccanismo, che oggi funziona per costruzione: al passo 1 di ES5 ci sono **due** voci — la riga
(→ WS5) e il padre (→ ES2) — e le righe si consultano prima del padre. Con WS5 chiuso ed ES2 aperto l'alto
est va a ES2; con tutt'e due aperti va a WS5. È esattamente la visita in ampiezza di `FallbackChain.Cammina`.

### Famiglia «dipende da DOVE» — non risolta

| caso | oggi |
|---|---|
| Ghedi, MIL chiuso → **ES2** | ✗ dà WS2 |
| Ghedi, MIL+ES2 chiusi, verso Milano → WS2 | ✅ per caso |
| Ghedi verso **Padova** → `LIPP_MIL`, poi `LIPP_CE1` | ✗ inesprimibile |
| LIMN via ANE, MIL chiuso, **verso est** → ES2 | ✗ dà WS2 |
| LIMN via ANE, MIL chiuso, **verso ovest** → WS2 | ✅ per caso |

Causa unica: **MIL è un settore sovrapposto**. Copre tutto l'ACC SFC–UNL, cioè più del suo stesso padre.
Il `ParentCallsign` non è impostato male — è **inesprimibile**.

---

## Parte 3 — Perché il CEDENTE non è la risposta (opzione scartata)

La prima idea era: «quando un sovrapposto è chiuso, la catena **riparte dal cedente**». Copre Ghedi
(`LIPX_ES0_APP` → ES2 → WS2) e sembra elegante.

**Cade su ANE, e cade per una ragione strutturale.** `LIMC_ANE_APP` ha per padre `LIMM_WS2_CTR`: dal
**medesimo** cedente, alla **medesima** quota, un punto a est vuole ES2 e uno a ovest vuole WS2. Nessun
meccanismo basato sul cedente può distinguerli, perché il cedente è lo stesso.

Su Ghedi funzionava solo per **coincidenza**: ES0 sta sotto ES2.

⚠️ Corollario da tenere: **niente colonna `OriginCallsign` su `SectorFallback`.** Sarebbe un secondo modo di
dire la stessa cosa, che diverge dal primo appena la struttura cambia — la domanda 1 del pre-flight.

---

## Parte 4 — Il rinvio geometrico

### 4a. Il bersaglio che non è un callsign

`SectorFallback` oggi dice «→ `LIMM_ES2_CTR`»: un **nome**. Il rinvio dice «→ chi copre questo punto»: una
**domanda**, che si risolve al momento, sul punto di trasferimento che si sta guardando.

Su `LIMM_MIL_CTR` si scrive **una riga**:

```
LIMM_MIL_CTR
  0   ogni quota  →  ⟨la copertura di questo punto⟩
  —   (coda implicita: il padre, LIMM_WS2_CTR)
```

Cinque gesti in tutto per i cinque MIL d'ACC italiani. Niente per aeroporto, niente per clausola.

Modello (`Vipi.Domain/Entities/Anagrafica.cs`):

```csharp
public enum FallbackTargetKind { Callsign, Coverage }

public class SectorFallback
{
    public int Id { get; set; }
    public string SectorCallsign { get; set; } = default!;
    public int Order { get; set; }
    public FallbackTargetKind TargetKind { get; set; }   // NUOVO, default Callsign
    public string? TargetCallsign { get; set; }          // diventa nullable: null quando è un rinvio
    public int? BaseFeet { get; set; }
    public int? TopFeet { get; set; }
}
```

Migrazione **additiva** sui due provider, `TargetKind` con default `Callsign`. A colonna nuova e nessun
rinvio scritto il comportamento è **identico riga per riga** a quello di oggi — la stessa disciplina della
tabella che nasce vuota. Lo schema non è congelato dalla finestra cieca; il **database non si consegna**.

⚠️ **Scartata la scorciatoia** di infilare un valore magico in `TargetCallsign` (`«⟨copertura⟩»`, che nessun
callsign può essere) per non migrare: funziona, e mente a chi fra sei mesi legge una colonna che si chiama
«callsign». Un `enum` accanto costa una migrazione additiva e dice la verità.

La **fascia resta** e conserva il significato: `⟨copertura⟩ sopra FL325` è una riga legittima.

### 4b. Chi risolve — i motori esistono già

Non si scrive geometria nuova. Questo gira in produzione per le **statistiche**, ed è puro:

- `SectorVolume.Contains(lat, lon, altitudeFt)` (`Stats/SectorVolume.cs:109`) — dentro **un pezzo**,
  poligono + banda del pezzo. Il prefiltro bbox sta in `PolygonGeometry.Contains`
  (`Aor/PolygonGeometry.cs:215`): costa poco.
- `SectorVolumeMap.BuildClaims(settori, online)` — da «chi è in frequenza» a «quali volumi rivendica ognuno».
- `TrafficAttribution.AttributeClaim(claims, lat, lon, altitudeFt, phase)` — quale sessione online possiede
  quel punto. Il suo commento nomina già il nostro caso: un volo su Roma cadeva dentro **sei** settori,
  `LIRR_MIL_CTR` e `LIRR_FSS` compresi.
- I volumi arrivano da `ISectorVolumeCatalog` / `EfSectorVolumeCatalog`, che legge l'albero **proiettato**
  (`Sector.ParentSectorId`) e le forme dalla porta unica `ISectorShapeResolver`.

Per un CoP la fase è `FlightPhase.Airborne`: APP e CTR rivendicano, DEL e GND no
(`Stats/FlightPhase.cs:48`). Esattamente giusto.

**La quota da passare è quella AL TRASFERIMENTO**: `HandoffLevelValue` se la riga porta la faccetta,
altrimenti `LevelValue`. Non il livello autorizzato — su «autorizzato FL160, trasferito passando FL110» il
settore che raccoglie è quello di FL110.

### 4c. Come si incastra nella camminata attuale

`FallbackChain.Cammina` espande un fronte: per ogni `x`, le sue righe dichiarate, poi il padre. Cambia
**una** cosa: quando una riga è un rinvio, si chiede a un risolutore di trasformarla in zero o più callsign.

```csharp
public static IReadOnlyList<string> Candidates(
    string sector,
    int? levelFeet,
    IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> declared,
    Func<string, string?> parentOf,
    Func<IReadOnlyList<string>>? resolveCoverage = null)   // NUOVO, opzionale
```

`null` ⇒ un rinvio produce **niente** e la catena prosegue sul padre come sempre. Nessun chiamante è
obbligato a saperne, e i siti che un punto non ce l'hanno non cambiano di una riga.

Il risolutore lo costruisce il **chiamante**, che il punto ce l'ha in mano:

```csharp
var claims = ClaimsPerQuestaRisoluzione(volumi, online);   // UNA VOLTA per richiesta, non per punto

IReadOnlyList<string> Coverage(TransferPointRow p, string cedente, string riceventeNominale)
{
    if (PosizioneDi(p.Cop) is not (double lat, double lon)) return [];   // punto senza posizione
    if (QuotaAlTrasferimento(p) is not int ft) return [];                // «as coordinated»

    var ammessi = Ammessi(claims, cedente, riceventeNominale);
    var vinta = TrafficAttribution.AttributeClaim(ammessi, lat, lon, ft, FlightPhase.Airborne);
    return vinta is null ? [] : [vinta.Value.SessionCallsign];
}
```

⚠️ Il rinvio restituisce **un candidato che è online per costruzione** — `BuildClaims` parte da chi è in
frequenza. Non è «un altro nome da provare»: è già la risposta. Se nessuno copre il punto torna vuoto e la
camminata prosegue sul padre, cioè sul comportamento di oggi.

### 4d. Il rapporto con la gerarchia: non la scavalca, la interroga dall'altro capo

La gerarchia risponde a **«chi c'è sopra di me»**: un puntatore per nodo, e risalirla è camminare *in su da
un callsign*. È **esatta** ogni volta che un settore è davvero un sottoalbero — `LIPL_G_APP` dentro
`LIPX_ES0_APP` dentro `LIMM_ES2_CTR`.

Il rinvio non introduce un secondo albero: fa la domanda opposta **sullo stesso albero**.

> Di tutti i settori che contengono questo punto a questa quota, qual è il **più specifico** fra quelli
> online?

E «il più specifico» lo decide `TrafficAttribution.IsMoreSpecific`, nell'ordine: fase di volo →
**profondità nell'albero** → banda più stretta → box più piccolo → alfabeto. La profondità **è**
`ParentCallsign`. Il collasso su chi è online **è** la gerarchia. La geometria decide **solo in quale ramo
ti trovi**, che è precisamente il pezzo che un padre singolo non sa dire.

| | domanda | percorso | dati |
|---|---|---|---|
| catena di oggi | chi c'è sopra il **settore** | in su, da un callsign | `ParentCallsign` + righe |
| rinvio | chi c'è sotto il **punto** | in giù, da una posizione | gli stessi, più il poligono |

Stessi dati. Nessun terzo modello.

### 4e. Le tre esclusioni che servono

**1. Filtro di RANGO — la più importante: senza, la risposta è sbagliata quasi sempre.**

Quasi ogni CoP sta dentro un APP **e** dentro l'ACC. Un APP è più profondo, quindi vincerebbe: il rinvio
consegnerebbe il traffico a un avvicinamento di terzi che quel punto ce l'ha solo perché gli passa sopra.
Per le **statistiche** è giusto (l'aereo lì c'è davvero); per un **trasferimento** no — il poligono di un
APP è spazio aereo, non titolarità del flusso: un APP è delegato per i *suoi* arrivi e partenze, non per
chi transita.

Regola: il rinvio guarda solo i candidati di rango **pari o superiore** al ricevente nominale, dove il rango
viene da `SectorType` (`Vipi.Domain/Enums.cs:4` — `Del, Gnd, Twr, ITwr, App, Ctr`):

```
Del 0 · Gnd 1 · Twr/ITwr 2 · App 3 · Ctr 4
rango(candidato) ≥ rango(ricevente nominale)
```

`LIMM_MIL_CTR` è `Ctr` ⇒ **solo `Ctr`**: tutti gli APP escono *prima* che la profondità conti.

⚠️ Verificato: nella proiezione un **FSS è tipato `Ctr`** (`LIMM_FSS` → `Ctr` nella tabella `Sectors`;
l'enum un valore `Fss` non ce l'ha). Quindi «solo Ctr» include gli FSS, che è quel che si vuole.

**2. Il CEDENTE e il suo dominio escono.** Un trasferimento «al confine dell'AoR» avviene *dentro* il
settore che cede: il CoP di Ghedi sta dentro `LIPX_ES0_APP` (0–19500). Chi consegna non può essere chi
raccoglie. Si taglia con `Topology.DomainOf(cedente)`, che c'è già. ⚠️ Serve **anche** col filtro di rango:
quando il cedente è un CTR (ES2 che cede a MIL) il rango non lo esclude.

**3. Il RICEVENTE NOMINALE è già escluso** da `vistiPrima`: quando si arriva al rinvio, MIL è stato guardato
e scartato.

**Spareggio in più, solo per il rinvio:** a pari specificità, preferire il candidato **dello stesso ACC del
ricevente nominale**. Morde solo dove due poligoni si sovrappongono davvero sul confine FIR (i due MIL sono
tutt'e due SFC–UNL: a parità di profondità e banda oggi deciderebbe l'area del bounding box, cioè il caso).
Con lo spareggio il traffico resta in LIMM finché il punto è in LIMM, e passa a LIPP quando è davvero oltre.

### 4f. La preferenza militare viene gratis

«Verso Padova: `LIPP_MIL_CTR` se aperto, altrimenti `LIPP_CE1_CTR`» non ha bisogno di niente. In produzione
`LIPP_MIL_CTR` è **figlio** di `LIPP_CE1_CTR`: profondità 1 contro 0. Il punto è dentro tutt'e due e vince
il più profondo ⇒ MIL se online, CE1 se no.

Lo stesso spareggio protegge il caso civile: con MIL chiuso il suo volume viene rivendicato dal suo
proprietario, ma con la **profondità di MIL** e la **banda di MIL** — SFC–UNL, più larga di ES2 — quindi su
un punto a est ES2 vince a parità di profondità.

Cioè: la collocazione attuale di MIL nell'albero **non è sbagliata**. Le si stava facendo la domanda
sbagliata.

### 4g. ⚠️ Il vincolo che tiene i due meccanismi allineati

`SectorVolumeMap.BuildClaims` collassa su chi è online con `CoverageResolver.Owners`, che conosce **solo
`ParentCallsign`** — le righe di ripiego **no**. Riusandolo così com'è, con ES5 chiuso a FL350 la **catena**
risponde WS5 (per la riga dichiarata) e la **geometria** risponde ES2 (per il padre): due risposte diverse
alla stessa domanda. È il difetto «due alberi» che questa base di codice ha già pagato una volta,
ricostruito in un posto nuovo.

Quindi: **`BuildClaims` riceve il collassatore come parametro.**

- il **rinvio** gli passa `FallbackChain` (righe + padre): geometria e catena rispondono uguale;
- le **statistiche** continuano a passare `CoverageResolver.Owners`, e questa resta una differenza
  **dichiarata**, non un incidente — la carta del 31 agosto l'aveva già messa fuori di proposito. Un test
  fissa la differenza, così resta deliberata.

Ricorsione chiusa da una regola sola: **dentro la risoluzione di un rinvio, i rinvii non si consultano.**
Un giro, e termina.

---

## Parte 5 — I CoP che non sono punti

`Y10`, `Y11`, `Y01-Y12`: la risposta non è «manca dal catalogo», è che **non sono punti**. Il codice lo sa
già — da `Content/NavaidCheck.cs`:

> il CoP è testo libero con validazione soft, **di proposito**: l'archivio contiene intervalli di aerovie
> (`Y01-Y12`), STAR (`TOPNO 3A`), `ALL`, `ALL to GR`. Misurato sull'archivio reale: **52 token CoP su 62
> sono verificabili**.

`Y01-Y12` è **un tratto di confine**, non una posizione: attraversa ES2 *e* WS2. Non è un dato mancante, è
una domanda che non si può porre.

Contratto del rinvio, esplicito:

1. `NavaidCheck.IsCheckable` fa da guardia: 2–5 lettere sole. `Y10` la passa, `Y01-Y12` no, `ALL to GR` no.
2. Non verificabile, **oppure** verificabile ma assente dal catalogo, **oppure** senza quota al
   trasferimento ⇒ **il rinvio non risponde**. La catena prosegue sul padre, cioè come oggi.
3. **E lo dice**: «ripiego geometrico non applicabile: il punto non ha una posizione», accanto alla riga.
   Stessa scelta già presa per `FallbackRow.AppliesAt(null)`, stessa ragione: un rinvio muto è
   indistinguibile da un guasto.

Le due uscite:

- **è un tratto di confine** → la risposta si **scrive**. Una riga dichiarata con un callsign batte sempre
  il rinvio. Sono poche righe, e sono precisamente quelle dove la scelta deve essere umana, perché il tratto
  sta a cavallo di due settori e nessun calcolo può sceglierne uno;
- **è un punto vero che il sectorfile non manda** → si dà la posizione **a mano**. L'anagrafica `Navaids` lo
  prevede: `CoordinatesOrigin` distingue `Source` da manuale, e la sorgente vince **campo per campo**, solo
  sui campi che manda. Una coordinata scritta una volta, e ogni clausola che usa quel CoP acquista il rinvio.

⚠️ `CopList.Parse` separa **per virgola**: `Y01-Y12` è **un** token, non due. Chi conta i punti non deve
provare a spezzarlo.

---

## Parte 6 — Il prerequisito: le coordinate dei fix

Misurato sul `vipi.db`: la tabella `Navaids` ha **149 righe, solo `VHF` e `NDB`, zero `Fix`**, e è **giusto così**. I CoP di
Milano sono in maggioranza fix di 5 lettere (`NELAB, ITCAP, LAGEN, EGHIN, KUMIN, VEROB, SIRLO, ASTIG,
IXUSA, KUKEV`), che **in archivio non ci sono**: le loro coordinate stanno in `NavaidCatalog`, che arriva da
`INavaidSource` via **HTTP dal sectorfile** (`Infrastructure/DependencyInjection.cs:307`) e viene usato
all'**import**, non a ogni richiesta.

🔴 **E i fix in anagrafica NON ci devono andare.** `NavaidImporter` lo dice a chiare lettere, ed è una
decisione presa, non una svista: «i *fix* sono punti di riporto: non hanno frequenza, non hanno canale e non
sono radioassistenze — metterli qui riempirebbe l'anagrafica di tremila righe che nessuna tabella di SOP
citerà mai, e la tendina da cui si sceglie diventerebbe inservibile». La prima stesura di questa carta
proponeva esattamente quello: **corretta il 10 settembre 2026, prima di scrivere codice.**

**Non serve persistere niente.** Il catalogo punti sta già dietro una cache **singleton**
(`Sectorfile.SectorfileCache`, registrata così apposta perché gli adapter sono transient), e
`NavaidCatalog.TryGetPoint(nome, out punto)` **esiste già**. Chiamarlo una volta per richiesta non è una
scaricata dalla rete per richiesta.

Quindi la slice si riduce a un **compositore**, `ICopPositions` / `CopPositionsProvider`: una fotografia
`nome → posizione` che mette in fila le due anagrafiche che ci sono già —

1. **l'anagrafica delle radioassistenze** (`INavaidCatalog`): VOR e NDB, e le coordinate **scritte a mano**;
2. **il catalogo punti del sectorfile** (`INavaidSource`): i fix.

⚠️ **L'ordine è la sola decisione**: vince la **prima** occorrenza, quindi l'anagrafica sta davanti. È il
modo in cui una coordinata corretta a mano scavalca quella della sorgente — se vincesse l'ultima, quella
valvola non si aprirebbe mai, e una correzione scritta e mai applicata è peggio di nessuna valvola.

⚠️ Sorgente muta (GitHub giù, `RawBaseUrl` vuoto) ⇒ restano i soli VOR/NDB: **degradazione dichiarata**, non
un guasto — chi risolve perde una risposta e lo dice.

**Nessuna tabella nuova, nessuna migrazione, nessun giro d'import toccato.**

---

## Parte 7 — Cosa si vede a schermo

«Non è mai il sistema a decidere una ricaduta da sé» resta in piedi, a due condizioni **vincolanti**:

1. **Una riga con un callsign scritto batte sempre il rinvio.** Il rinvio sta in coda, il caso particolare
   davanti.
2. **La risoluzione si mostra.** Nella tabella Trasferimenti, accanto al ricevente:
   `MIL chiuso → ES2 (copertura di NELAB a FL140)`. In Struttura, sul pannello del settore, la voce
   `⟨copertura del punto⟩` come passo della catena, più una casella **«prova un punto»** che dice cosa
   risolverebbe. Un rinvio che risolve in silenzio è indistinguibile da un guasto — è il difetto già pagato
   col banner che ridipingeva il vecchio.

---

## Parte 8 — I rilievi di consistenza (rete, non contorno)

- **CoP senza posizione**, contati per ACC: dice **quanto è cieco** il rinvio *prima* di accenderlo, invece
  di scoprirlo un punto alla volta.
- **Settore sovrapposto con un padre più basso di sé** (`MIL` SFC–UNL sotto un padre 0–FL325): è il rilievo
  che avrebbe pescato questo caso da solo.
- **Settore alto la cui catena non contiene nessun settore alto**: la rete sulla riga di `LIMM_ES5_CTR`, che
  oggi porta due dei cinque assetti e che cancellandola non dà nessun errore.
- **La proiezione e i cataloghi dicono lo stesso albero**: la `Depth` della geometria viene da
  `Sector.ParentSectorId`, la catena da `EffectiveHierarchy`. Devono coincidere — la proiezione nasce dai
  cataloghi — ma sono **due letture**. Senza questo rilievo «due alberi» torna per la terza volta.

---

## Parte 9 — Il banco di prova

| caso | atteso | chi lo produce |
|---|---|---|
| Ghedi, MIL chiuso | `LIMM_ES2_CTR` | rinvio: CoP a est; i due APP fuori per rango |
| Ghedi, MIL+ES2 chiusi, verso Milano | `LIMM_WS2_CTR` | rinvio: ES2 offline ⇒ il suo volume è di WS2 |
| Ghedi verso Padova, MIL chiuso | `LIPP_MIL_CTR`, poi `LIPP_CE1_CTR` | rinvio + profondità |
| **LIMN via ANE verso est, MIL chiuso** | `LIMM_ES2_CTR` | rinvio |
| **LIMN via ANE verso ovest, MIL chiuso** | `LIMM_WS2_CTR` | rinvio |
| i 5 assetti WS2/ES2/WS5/ES5 | come li ha scritti il committente | catena di oggi, **invariata** |
| CoP `Y01-Y12` / `ALL` | il padre, come oggi, **e lo dice** | il rinvio non risponde |
| CoP dentro un APP di terzi | mai l'APP | filtro di rango |

⚠️ I due in grassetto sono il banco che conta: **stesso cedente, stessa quota, due risposte.** Nessun
meccanismo basato sul cedente li passa.

---

## Parte 10 — La scala di risalita (chiesta il 10 settembre, a lavoro fatto)

Da quando la risposta dipende da chi è online, l'admin non ha più modo di **guardare** una configurazione:
può solo aspettare di vederla sbagliare. Il committente chiede un attrezzo: seleziono un coordinamento,
premo un tasto, e il sistema mi dice come risalirebbe.

### Non un nome: tutta la discesa

Un nome solo sarebbe vero per i cinque minuti in cui lo guardi, e verrebbe letto come una proprietà della
configurazione. Quindi si mostra la **scala**: chi lo prende, e poi — chiuso quello — chi lo prende ancora,
fino a UNICOM.

```
GHE · FL140 · ricevente scritto: LIMM_MIL_CTR
  1  LIMM_MIL_CTR    il ricevente scritto
  2  LIMM_ES2_CTR    copertura del punto — GHE cade nella sua area
  3  LIMM_WS2_CTR    copertura del punto — con ES2 chiuso
  4  UNICOM          esauriti tutti
```

Ogni gradino porta il **perché**: «il ricevente scritto», «padre», «riga dichiarata FL325–UNL»,
«copertura del punto». È la differenza fra sapere dove finisce il traffico e sapere se la configurazione fa
quello che si voleva.

### 🔴 La scala NON è la lista dei candidati

`FallbackChain.Candidates` scioglie il rinvio **una volta** e poi cammina l'albero. Il sistema vero, a ogni
richiesta, lo **richiede** con l'insieme di chi è online in quel momento. Quindi la simulazione fedele si fa
per **eliminazione**: risolvi con tutti aperti, chiudi il vincitore, richiedi.

Su Milano le due strade danno lo stesso esito — il padre di ES2 è WS2, che è anche la risposta geometrica —
e su un albero diverso no. Una scala costruita con una sola chiamata sarebbe **giusta per caso**, cioè il
genere di attrezzo che convince che il dato sia a posto.

### Il tetto sta sui PUNTI, non sulle clausole

Una clausola porta più CoP, e ogni punto ha la **sua** scala — è il senso della cosa. Un tetto contato sulle
righe prometterebbe dieci e ne consegnerebbe trenta. **Dieci punti**, una costante sola col motivo accanto.

⚠️ E il tasto spento dice il **numero vero** — «13 punti su 10: seleziona meno righe» — non un «troppi»
generico: è la regola che ci siamo già dati per i tasti spenti.

### Le scale identiche si raggruppano

Con dieci righe la maggior parte delle scale coincide. Elencarle una per una sarebbe il muro che il tetto
serve a evitare, e nasconderebbe l'unica cosa che si vuole vedere. Un blocco per scala **distinta**, con
sotto i punti che la seguono:

```
LIMM_MIL_CTR → LIMM_ES2_CTR → LIMM_WS2_CTR → UNICOM      7 punti: GHE · NELAB · …
LIMM_MIL_CTR → LIMM_WS2_CTR → UNICOM                     2 punti: TOP · VOG
nessuna scala — il rinvio non risponde                   1 punto: Y01-Y12 (non è un punto)
```

⚠️ Il raggruppamento si fa **dopo** aver risolto, non prima: due punti diversi possono dare la stessa scala,
e non lo si sa finché non si chiede.

### ✅ Provata dal vivo — 10 settembre 2026

Selezionate due clausole dello **stesso** flusso (cedente `LIPX_ES0_APP`, ricevente `LIMM_MIL_CTR`), il
pannello ha reso due blocchi:

```
LIMM_MIL_CTR SCRITTO → LIMM_ES2_CTR COPERTURA → LIMM_WS2_CTR COPERTURA → UNICOM      1 punto: GHE
LIMM_MIL_CTR SCRITTO → LIMM_WS2_CTR COPERTURA → UNICOM                               1 punto: TOP
```

E con una clausola da nove punti il raggruppamento ha fatto il suo lavoro — **otto** insieme, e **uno** da
solo che se ne va a Padova:

```
LIMM_MIL_CTR → LIMM_ES2_CTR → LIMM_WS2_CTR → UNICOM
   8 punti: BSM · BRL · IPR · LIN · NOV · SRN · TZO · CAM

LIMM_MIL_CTR → LIMM_ES2_CTR → LIMM_WS2_CTR → LIPP_MIL_CTR → LIPP_CE1_CTR → LIPP_NE3_CTR → UNICOM
   1 punto: VIL
```

⚠️ Quel secondo blocco **nessuno l'ha configurato**: `VIL` sta vicino al confine con Padova, e la geometria
ci ha portato il traffico da sé, col MIL di Padova prima del suo civile. È il caso che il committente aveva
descritto a parole il 9 settembre, comparso da solo su dati reali.

Il tetto: con 12 punti selezionati il tasto si spegne e dice **«12 points selected, the maximum is 10:
select fewer rows»** — i numeri veri, e contati sui punti (tre clausole da un punto più una da nove).

⚠️ Un difetto di resa preso lì: **«1 points»**. Singolare e plurale sono **due chiavi**, non una desinenza
incollata — in inglese la parola che cambia non è in fondo. È la lezione di `XferLabels`, e l'avevo appena
riletta. 🔴 E la correzione non si è vista al primo riavvio perché avevo riavviato **senza ripubblicare**:
la trappola «il processo, non il file», pagata un'altra volta.

### E il rilievo che le guarda tutte

Il pannello serve a chi ha un sospetto. Il rilievo serve a non doverne avere uno: percorre la scala di
**ogni** clausola e segnala quelle che **al secondo gradino finiscono su UNICOM** — cioè i trasferimenti
che, chiuso il ricevente, non hanno nessuno.

⚠️ Non duplica «CoP senza posizione» (Parte 8): quello dice che il rinvio **non potrà rispondere**, questo
dice che **la catena non porta da nessuna parte**. Sono due domande, e la seconda è vera anche su un punto
collocato benissimo.

🔴 **«Finisce su UNICOM» da solo NON è un difetto**, e questa è la correzione più importante del giro dal
vivo. Sopra un ACC non c'è niente per costruzione: la radice di Brindisi e le radici estere finiscono su
UNICOM ed è **giusto** così. Il primo giro ne ha segnalati **otto su LIBB, e sei erano ACC esteri** — un
avviso che grida su dati corretti si impara a ignorare, e allora smette di servire anche quando ha ragione.

Il difetto è un altro: **quel punto lo copre qualcun altro, e la catena non ci arriva**. Si chiede
richiudendo il solo ricevente e ridomandando alla geometria; se nessun altro copre, il ricevente è davvero
il tetto e non c'è niente da dire. È il caso di `LIRR_MIL_CTR`, sovrapposto ai civili di Roma e però radice.

⚠️ Se il CoP non ha una posizione, questo rilievo **tace** — e non è un buco: senza la posizione non si può
sapere se qualcun altro copra, e dirlo sarebbe indovinare. Quella metà la coprono gli altri due rilievi:
«ricaduta che non copre la quota» lavora sulla **struttura** e non ha bisogno di posizioni, «CoP senza
posizione» dice **quanti** punti sono ciechi. I tre insieme dicono tutto; ognuno da solo, no.

---

## Pre-flight (FEATURE-PROCESS)

**1. Modello — aggiungo un concetto o ne esiste già uno?**
Estendo `SectorFallback`, non affianco. **Niente `OriginCallsign`** (Parte 3): sarebbe un secondo modo di
dire «chi raccoglie», e i due divergerebbero. Chi fra sei mesi cerca «dove si salva il ripiego» trova **un**
posto. Il rinvio non è un'entità: è un **valore** del bersaglio.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?**
`TargetKind` ha due valori. **Un solo punto instrada** (`FallbackChain.Cammina`, che chiama il risolutore);
validazione e resa lo guardano per **etichettare**, non per decidere il percorso. Un registry per due valori
sarebbe over-engineering — ma la regola resta scritta qui: se compare un terzo valore, si estrae il
descrittore **prima** di aggiungere il terzo `if`.

**3. Ingressi + verifica.**
Ingresso: l'editor delle righe di ripiego esiste già nel pannello Struttura; si aggiunge
`⟨copertura del punto⟩` alla tendina del bersaglio. Nessun catch-22: la riga si scrive su un settore che
c'è già. Verifica: vista live di LIMM guidata con `Ivao__FakeOnlineCallsigns`, gli otto casi del banco,
con traccia.

**4. Propagazione — rimuovo o rinomino qualcosa?**
Sì: `FallbackRowEdit.TargetCallsign` cambia forma (nullable + `TargetKind`). Nello **stesso giro**:
`ISectorFallbackService` e i suoi commenti, `FallbackChain` (il commento «chi riceve» non è più solo un
callsign), la carta del 31 agosto (la frase «le statistiche restano fuori di proposito» va **precisata**:
restano fuori, ma il collassatore ora è un parametro), la memoria `ricaduta-verticale-e-cicli`, e
`docs/index.md` + `docs/lavori-aperti.md`.

---

## Esecuzione — slice

Ognuna è un commit, build verde, e ha valore da sola.

| # | slice | stato |
|---|---|---|
| 1 | **Riallineato il `vipi.db` di sviluppo** alla produzione: padre di ES5 → ES2, riga `FL325–UNL → WS5` con le quote giuste | ✅ fatta (backup `vipi.db.bak-pre-rinvio-geometrico-20260910`) |
| 2 | **`ICopPositions`**: la fotografia `nome → posizione`, anagrafica + catalogo punti | ✅ fatta, 5 test. 🔴 La prima stesura voleva persistere i fix: `NavaidImporter` lo **vieta**, con la sua ragione |
| 3 | **`BuildClaims` prende il collassatore come parametro**; statistiche invariate | ✅ fatta, 4 test che **fissano la differenza**, così resta deliberata |
| 4 | **`FallbackTargetKind` + migrazione additiva** (due provider) + validazione | ✅ fatta. 🔴 Due presidi l'hanno corretta: vedi sotto |
| 5 | **Il risolutore puro** (rango, cedente, spareggio di centro, «non risponde») | ✅ fatta, 20 test — il banco della Parte 9, con due mutazioni scritte nei test |
| 6 | **Innesto nei due siti col punto**; gli altri due invariati | ✅ fatta |
| 7 | **A schermo**: editor, catena, riga live | ✅ fatta, 12 chiavi in due lingue |
| 8 | **I rilievi** della Parte 8 | ✅ fatti **tre** (uno ne assorbe due), 8 test |
| 9 | **I dati in produzione**: le righe sui MIL; i tre padri di Roma | ▶ **da fare a mano**, dopo la consegna, e da verificare **da fuori** |

### Quel che hanno detto i presidi (slice 4)

🔴 **`MigrazioniDellaFinestraCiecaTests` ha fermato un `AlterColumn`.** Rendere `TargetCallsign` nullable
riscrive la tabella su MariaDB, e fino al 16 settembre le migrazioni girano **da sole** all'avvio in
produzione, senza nessuno che possa ripristinare. La colonna resta **NOT NULL** e un rinvio porta la
**stringa vuota**: a dire perché è vuota è `TargetKind`, non il campo. La migrazione è un `AddColumn` e
basta, su tutt'e due i provider.

⚠️ **`IndexedStringLengthTests` ha chiesto la lunghezza di `TargetKind`**: ha un valore di default, e su
MySQL una colonna con default non può essere `longtext`.

Nessuno dei due l'ha visto una rilettura: li ha visti la suite.

---

## Verifica live — 10 settembre 2026

Guidata la vista live su **Milano vera** (`vipi.db` di sviluppo copiato, `Ivao__FakeOnlineCallsigns` =
`LIMM_ES2_CTR, LIMM_WS2_CTR`, MIL chiuso, una riga di rinvio su `LIMM_MIL_CTR`, un flusso
`LIPX_ES0_APP → LIMM_MIL_CTR` con due punti reali: `GHE` a est e `TOP` a ovest).

### 🔴 Il difetto che i test non vedevano: l'FSS raccoglieva

Il **primo** giro ha risposto `LIMM_WS2_CTR` dove doveva rispondere `LIMM_ES2_CTR`.

La causa non era la geometria: `LIMM_FSS` è **SFC–FL195**, cioè una banda **più stretta** di quella di ES2
(SFC–FL325), alla **stessa profondità**. A parità di profondità il tie-break sceglie la banda più stretta,
quindi vinceva l'FSS — e, essendo chiuso, il traffico finiva al suo proprietario, WS2.

⚠️ **Il filtro di rango non poteva vederlo**: `SectorType` un valore `Fss` non ce l'ha, e nella proiezione
un FSS è tipato `Ctr`. Il suffisso del callsign è l'unico dato che lo dice — e il progetto già lo legge così
(`ForeignSectorCallsign`, `FrequencyPositions`).

**La regola aggiunta**: un trasferimento fra enti di **controllo** non si delega a un servizio informazioni.
Se il ricevente nominale è a sua volta un FSS, invece, è legittimo. La fixture dei test ora ha l'FSS con la
banda vera, e la mutazione è scritta dentro il test.

### ✅ La coppia che conta, dal vivo

Stesso flusso, **stesso cedente** (`LIPX_ES0_APP`), **stessa quota** (FL140), **stesso ricevente nominale**
(`LIMM_MIL_CTR`, chiuso):

```
— GHE  FL140   LIMM_ES2_CTR   coverage
  TOP  FL140   LIMM_WS2_CTR   coverage
```

Due riceventi diversi, e a deciderli è **il punto**. È il banco che nessun meccanismo basato sul cedente
può passare.

### ✅ E il pannello Struttura lo disegna

Su `LIMM_MIL_CTR`, in inglese (la cultura di UI di questa macchina):

```
1   any level → the point coverage  ?
    any level → LIMM_WS2_CTR  PARENT
```

Il rinvio sta **davanti** al padre, e si mostra **senza nome**: quella pagina i punti non li ha.

---

⚠️ **Le slice 1 e 9 sono dati, non codice**, e la 9 tocca la **produzione**: si fa a mano, dopo che il resto
è online, e si verifica **da fuori**.

### I quattro siti che chiamano `FallbackChain`

| sito | ha un punto? | cambia |
|---|---|---|
| `Content/AgreementEditingService.cs:51` — per punto, vista live | ✅ `p.Cop` + quota | passa il risolutore |
| `Content/TransferMatcher.cs:388` (`ResolveHandler`) | ✅ | passa il risolutore |
| `Content/AgreementEditingService.cs:47` — proprietario del flusso | ✗ nessun punto | **invariato** |
| `Content/TransferMatcher.cs:170` (`IsCoveredBy`) | ✗ quota di crociera, non un punto | **invariato** |
| `Ui/Pages/StrutturaPage.razor:1299` (`Sequence`) | ✗ disegno | mostra la voce senza risolverla |

---

## Costi, rischi, e cosa resta fuori

- **Prestazioni**: `BuildClaims` **una volta per richiesta**, non per punto; `Contains` ha il prefiltro bbox.
  Una vista live con ~50 punti e ~200 rivendicazioni sta nell'ordine dei millisecondi — **ma va misurato dal
  vivo**, non stimato.
- **Punti sul confine**: un CoP esattamente sulla linea fra ES2 e WS2 risolve in modo deterministico ma
  arbitrario. Si **vede** (Parte 7) e si corregge con una riga scritta.
- **Poligoni assenti**: 11 settori ACC e 50 d'aeroporto non hanno forma, quindi non rivendicano e non
  possono vincere un rinvio. È la scelta già presa altrove: «non ho una forma» non è «prendo tutto il cielo».
- **Le statistiche non cambiano numeri** (slice 3): la differenza resta dichiarata.
- **I tratti di confine restano dichiarativi**: un `Y01-Y12` non avrà mai una risposta calcolata, e va bene
  così.
- **Fuori da questa carta**: le cinque radici di Roma (scelta del committente), e il ripiego per **clausola**
  — l'alternativa esatta ma cara, che ripete in centinaia di righe quel che il poligono già sa. Si riapre
  solo se il rinvio si dimostrasse cieco su troppi CoP, e il rilievo della Parte 8 è il modo per saperlo.
