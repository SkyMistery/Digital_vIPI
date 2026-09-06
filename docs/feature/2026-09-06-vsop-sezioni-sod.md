# vSOP militari — l'indice che chiede il SOD (6 settembre 2026)

> Stato: ✅ **ESEGUITA il 6 settembre 2026**, ramo `vsop-sezioni-sod`. Consegnata con **1.13.0**.
> Gemella di [2026-08-27-vsop-militari.md](2026-08-27-vsop-militari.md), che ha creato il profilo.

Il SOD ha mandato l'indice che i vSOP devono avere. Confrontato con il profilo `AirportMil` di oggi:
**nessuna sezione dei quindici PDF è caduta** dalla sua proposta — quindi non si toglie niente per
allinearsi. Si aggiunge, si sposta una tabella che già c'è, e si toglie l'unica sezione che avevamo
inventato noi.

## 1. Che cosa cambia

### 1a. Dodici sezioni nuove

| Chiave | Titolo | Padre |
|---|---|---|
| `runways:thresholds` | Coordinate delle soglie | `runways` |
| `airportlayout` | Planimetria dell'aeroporto | `generaldata` |
| `parkings:apronflow` | Flusso di rullaggio sui piazzali | `parkings` |
| `arrivalrestrictions` | Restrizioni all'arrivo | `flightprocedures` |
| `circuitrestrictions` | Restrizioni di circuito | `flightprocedures` |
| `vfrjet:points` | Punti significativi VFR | `vfrjet` |
| `departureprocedures` | Procedure di partenza | `operationaltechnique` |
| `departureprocedures:vfr` · `:ifr` | VFR · IFR | `departureprocedures` |
| `arrivalprocedures` | Procedure di arrivo | `operationaltechnique` |
| `arrivalprocedures:vfr` · `:ifr` | VFR · IFR | `arrivalprocedures` |

⚠️ **Chiavi proprie, mai `vfr`/`ifr` nudi.** `vfr` ha già un mestiere (la sezione VFR di un profilo di
posizione) e dentro un profilo una chiave compare una volta sola — lo pretende
`SectionCatalogTests.Nessuna_chiave_e_ripetuta`. Stesso motivo per cui le carte sono `charts:*`.

⚠️ **`operationaltechnique` è una chiave UNIVERSALE** (sta in ACC, APP, vLOA, aeroporto): i suoi quattro
discendenti si scrivono nel registro `AirportMil`, non altrove. `Children` è per profilo, quindi gli altri
quattro documenti non li vedono — e c'è un test che lo dimostra.

⚠️ **Profondità 3, cioè il limite.** `arrivalprocedures:vfr` sta a `Depth == 3` e `DocumentSection.MaxDepth`
vale 3. Ci sta esatto, senza margine: il prossimo che vuole annidare sotto quelle non può, e deve saperlo
prima di provarci.

### 1b. QRA / Scramble esce

Non è nei quindici PDF — l'avevamo aggiunta noi il 27 agosto, e il SOD non la vuole. Esce dal catalogo, e
dai documenti già scritti la toglie un passo di manutenzione (§3b).

### 1c. Le coordinate delle soglie diventano una sezione

Oggi sono la **seconda tabella dentro «Piste»** (`MilRunwayThresholds`, montata dal `case "runways"`).
Diventano la sotto-sezione `runways:thresholds`, resa dalla pagina con la stessa tabella e lo stesso dato.

⚠️ **`SectionKind.Editorial` benché la tabella sia palesemente derivata**, e non è una svista: la
derivazione è quella di `runways` — `AirportSectionProjection.Runways` — e la release la congela **lì**,
sotto la chiave `runways` (`AirportViewDerivationService`: `frozen.Get<AirportRunwaysView>("runways")`).
Dichiararla `Derived` prometterebbe un secondo congelamento che nessuno esegue, e peggio: darebbe due
interruttori Live/Frozen sulla stessa tabella, che possono contraddirsi — la stessa pista fotografata a due
cicli diversi, una sopra l'altra. Il corpo lo disegna la pagina (`SectionBodySource.Host`); il dato resta uno.

### 1d. `[PILOTS]` diventa un default di catalogo

Il SOD marca dodici sezioni come «per i piloti». `SectionAudience` esiste dal 27 agosto, ma nessuna sezione
è mai **nata** con un pubblico: il campo prendeva il default della colonna (`Both`). Ora il descrittore lo
porta, e lo scrivono i due posti che creano sezioni — la nascita e l'aggiunta a posteriori.

Le dodici: `diversion` · `runways:thresholds` · `callsigns` · `parkings` · `parkings:apronflow` ·
`enginestart` · `arming` · `takeoff` · `arrivalrestrictions` · `vfrjet:points` · `ifrsignificant` ·
`lowlevel`.

⚠️ **Marcare `Pilots` NASCONDE alla vista ATC**, e si porta dietro i figli (`AudienceFilter`). Con questi
default un controllore che apre `?vista=atc` non vede più parcheggi, nominativi e alternati. È la decisione
del committente del 6 settembre 2026, presa sapendolo: chi vuole vederle apre «Tutti», che è la vista di
default — il filtro è opt-in, e nessuno ci finisce dentro per caso.

## 2. Il conto

Sezioni del profilo `AirportMil`: **32 → 43** (32 − 1 QRA + 12).

## 3. I documenti già in produzione

Il catalogo decide la struttura **solo alla nascita** (`DocumentBirth`). Quindi ogni cambio qui sopra vale
per i vSOP nuovi e per nessun altro, finché non lo si porta a mano.

### 3a. Le dodici nuove: nessun codice

`AddMissingCatalogSectionsAsync` copre già i vSOP militari, scende nelle sotto-sezioni e inserisce nella
posizione che il catalogo prevede, rinumerando i fratelli. Va solo **provato fino alla profondità 3**: il
ramo più fondo che abbia mai attraversato finora era il 2.

### 3b. `RemoveMilQraSectionsAsync` — nuovo, prudente

- niente blocchi con contenuto → la sezione si elimina;
- contenuto scritto → **diventa sezione libera** (`custom:{guid}`), titolo invariato.

Nessuno perde quel che ha scritto. È il precedente di `airportextra` in
`ReconcileAirportSectionKeysAsync`: quando una chiave di catalogo sparisce, il testo che ci stava dentro
diventa una sezione libera, non un buco.

### 3c. `ApplyCatalogAudienceDefaultsAsync` — nuovo, one-shot

Marca `Pilots` le sezioni preesistenti che il catalogo adesso vuole tali, **solo se stanno ancora a
`Both`**.

⚠️ **`Both` significa due cose e non si distinguono**: «mai toccata» e «qualcuno ha deciso così». Non c'è
una colonna che le separi, e aggiungerla vorrebbe dire una migrazione EF dentro la finestra cieca del 16
settembre — per un caso che forse non esiste (la marcatura a mano è di dieci giorni fa). Si accetta: chi
avesse scelto `Both` su una di quelle dodici se lo rivede ribaltato, e lo rimette con un clic. Va nel
runbook, non lasciato scoprire.

### 3d. Le release pubblicate non si toccano

Regola di sempre (doc 13 §9). Il pubblico continua a leggere lo snapshot vecchio — indice vecchio, QRA
compresa — finché quel vSOP non viene ripubblicato.

## 4. Perché è spedibile adesso

**Nessuna migrazione EF.** `DocumentSection.Audience` è in produzione dal dump del 30 agosto, e tutto il
resto è catalogo, viste e manutenzione all'avvio. La finestra cieca al 16 settembre
(`MigrazioniDellaFinestraCiecaTests`) non ha niente da dire su questo lavoro.

## 5. Le due domande al SOD — chiuse il 6 settembre 2026

- ✅ **«Airport layout» e la carta d'aerodromo sono DUE sezioni distinte, e le vuole tutte e due.** Non è
  un doppione: `charts:aerodrome` è l'**allegato** — la carta AIP dello scalo — e `airportlayout` è la
  **descrizione a parole** che i SOP scrivono. Restano dove sono, una nei dati generali e una fra le carte.
- ✅ **Le marcature `[PILOTS]` sono sue**, e le vuole con l'effetto che hanno: nella vista ATC quelle
  dodici sezioni **non si vedono**. Chi controlla e le vuole leggere apre «Tutti», che è la vista di
  default.
- L'ordine delle procedure di volo è **il suo**: `takeoff` → `arrivalrestrictions` → `circuitrestrictions`
  → `sfo` → `commfail` → `gca` → `vfrjet` → `ifrsignificant` → `gat`.
