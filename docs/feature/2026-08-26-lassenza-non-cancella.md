# L'assenza non cancella la presenza

> 26 agosto 2026 · ramo `identita-settori`
>
> La sorgente risponde `regionMapPolygon: []`, e gli upsert lo prendevano per una forma scrivendolo sopra
> quella che avevamo: **83 poligoni a zero in un solo import**, misurato sul database vero.
>
> ⚠️ **Le shape delle TWR non sono mai state di IVAO**, ed è voluto: sono nostre — GitHub più il cerchio di
> ripiego — ed è per questo che quella catena esiste. La correzione serve a **proteggerle**, non a
> recuperare qualcosa dalla sorgente. Vedi §5 per la parte che invece è cambiata davvero.

## 1. Il difetto

Trovato per strada mentre si misurava altro ([identità dei settori](2026-08-26-identita-dei-settori.md) §7).
`/v2/ATCPositions/{compose}` e `/v2/subcenters/{compose}` rispondono `regionMapPolygon: []` — **tutte e 229**
le righe italiane, verificate contro l'API vera. Per le TWR è la normalità (§5); il difetto è che quel vuoto
veniva scritto **sopra** le shape che avevamo.

Gli upsert dei cataloghi assegnavano senza guardare:

```csharp
row.RegionMapPolygon = p.RegionMapPolygon;   // ← "[]"
row.IsShapeSynthetic = false;
```

Misurato applicando l'import vero, con i payload veri, a una copia del `vipi.db` di produzione:

```
PRIMA   reali=66  sintetici=17  vuoti=59
DOPO    reali=0   sintetici=0   vuoti=142
```

**83 poligoni su 83.** I 66 «reali» non venivano da IVAO ma da **GitHub** (`twrs.tfl`, via
`GithubTowerShapeService`); i 17 sintetici erano i cerchi di ripiego a 5 NM. Nessuno dei due è una shape
della sorgente: sono il ripiego che esiste **perché** la sorgente non le dà.

### Perché non se n'era accorto nessuno

Nel **giro notturno** l'ordine è import → GitHub → cerchi: quel che l'import cancella, i due ripieghi lo
rimettono qualche secondo dopo. Il danno si vedeva solo altrove — e gli **altri tre chiamanti di
`ImportAsync` non rieseguono i ripieghi**:

| Chiamante | Ripieghi dopo? |
|---|---|
| `AirportSectorImportHostedService` (giro 24h) | ✅ GitHub + cerchi |
| `AirportSectorService.ImportFromSourceAsync` (bottone dell'editor aeroporto) | ❌ |
| `AirportImportUseCase` (massivo di `/admin/airports`) | ❌ |
| `StructureEditingService` («Genera documenti») | ❌ |

Cioè: premere «importa dalla sorgente» nell'editor di un aeroporto **toglieva l'area alla sua TWR fino al
giorno dopo**. E se GitHub fosse irraggiungibile durante il giro notturno, i 66 poligoni reali diventerebbero
cerchi di ripiego per quel ciclo.

⚠️ La regola c'era già, scritta due volte — `EfAccAdminRepository` sulle aree regolamentate («preserva shape
se il dettaglio manca») e `EfNeighbourRepository` («preserva un poligono già incollato») — ma espressa come
`is not null`, e un array vuoto quel controllo lo passa benissimo.

## 2. La regola

**L'assenza non sovrascrive la presenza.** Quel che la sorgente non manda, o manda vuoto, non è un ordine
di cancellare.

`PolygonGeometry.IsEmptyShape` risponde a una domanda sola: *la sorgente mi ha dato qualcosa?* Vero per
campo assente e per un contenitore vuoto (`[]`, `{}`, `null`); falso per tutto il resto.

⚠️ **Chiede se è vuoto, non se è valido, e la differenza è voluta.** La prima versione usava
`ParsePoints(...).Count >= 3` — cioè un **validatore**. È sbagliato: il giorno in cui la sorgente manda una
forma che questo parser non sa ancora leggere, un upsert-validatore la butterebbe via *in silenzio* tenendosi
quella vecchia. Giudicare se una shape si disegna è compito di chi la disegna (`AorPolygonProjector.Project`
e i ripieghi TWR), e quelli hanno già il loro piano B. Un test presidia la scelta: un poligono di due punti
non si proietta ma **non è un'assenza**.

Applicata nei tre upsert che ricevono shape da fuori — `EfAirportSectorRepository`, `EfAccAdminRepository`,
`EfNeighbourRepository` — sia in aggiornamento sia in inserimento.

Nota su `IsShapeSynthetic`: si azzera **solo** quando arriva una shape vera. Un cerchio di ripiego resta
marcato sintetico, così `GithubTowerShapeService` può ancora rimpiazzarlo con un poligono reale.

## 3. Le righe già sporche

In archivio c'erano **207 valori vuoti** dagli import precedenti: 148 `AccSectors` e 59 `AirportSectors`.
Non sono innocui — `HasPolygon` guarda `!= null`, quindi a schermo dicevano «ha un poligono», e le letture
dell'AoR se li portavano dietro.

Migrazione `ShapeVuoteANull` (×2, SQLite e MySQL), provata su copia della produzione:

```
AccSectors      vuoti 148 → 0     poligoni veri 5  → 5     righe 153 → 153
AirportSectors  vuoti  59 → 0     poligoni veri 83 → 83    righe 192 → 192
```

Nessun `Down`: il valore di prima era un'assenza scritta in tre modi diversi, e reinventarne uno rimetterebbe
in circolo l'ambiguità che la migrazione toglie.

## 4. Verifica

Stesso banco di prova del difetto — import vero, payload veri, copia della produzione:

```
PRIMA   reali=66  sintetici=17  vuoti=59
DOPO    reali=66  sintetici=17  vuoti=59      ← nulla si muove
```

Più quattro casi di regressione in `AirportSectorImportTests` (shape vuota non cancella né il poligono
GitHub né il cerchio; una shape vera **sì** sovrascrive il ripiego; una riga nuova con shape vuota nasce
senza shape) e i casi di `IsEmptyShape` in `ShapeVuotaTests`.

⚠️ Un test esistente è cambiato: `GithubTowerShapeServiceTests` asseriva `Assert.Equal("[]", …)` su un
aeroporto che il filtro non doveva toccare. Presidiava il **modo in cui l'assenza era scritta in colonna**,
non il fatto che GitHub non l'avesse toccato. Ora dice `Assert.Null`, cioè quel che ha sempre voluto dire.

## 5. Che cosa è davvero cambiato, e che cosa no

⚠️ La prima stesura di questa carta diceva «IVAO ha smesso di mandare i poligoni». **Per le TWR è falso**, e
il committente l'ha corretto: quelle shape non sono mai arrivate dalla sorgente, ed è esattamente il motivo
per cui esistono `GithubTowerShapeService` e il cerchio da 5 NM. Il confronto col backup del 17 agosto lo
conferma — le TWR sono **stabili**, e i loro poligoni sono sempre stati nostri:

| | 17 agosto | oggi |
|---|---|---|
| TWR da GitHub (`IsShapeSynthetic=false`) | 68 | 66 |
| TWR col cerchio di ripiego (`=true`) | 16 | 17 |

**Quel che invece è cambiato non riguarda le torri.** Nello stesso periodo:

| | 17 agosto | oggi |
|---|---|---|
| **APP** d'aeroporto con poligono vero | **59** | **0** |
| **CTR/FSS** italiani con poligono vero | **27** | **0** |

Le righe ci sono ancora — è la colonna a essere stata svuotata. E quei poligoni potevano venire **solo dalla
sorgente**: per gli `AirportSectors` gli unici altri scrittori sono `SetRealShapeAsync` e
`SetSyntheticShapeAsync`, che lavorano su `ListTwrShapesAsync`, cioè **solo TWR**.

⚠️ **Per APP e CTR non esiste nessun ripiego.** Le TWR si sono salvate perché GitHub le rimette; quelle no.

### Dove pesca webeye — e perché la risposta chiude la questione

La mappa di IVAO i confini li disegna, quindi da qualche parte li prende. Il suo bundle
(`webeye.ivao.aero/assets/shapes.*.js`) legge **lo stesso identico campo** che leggiamo noi,
`regionMapPolygon`, e ha **lo stesso ripiego**: se è vuoto disegna un cerchio attorno all'aeroporto —
40 km per le TWR, 50 per le DEP, 60 per le APP.

Dal bundle sono usciti due endpoint che non conoscevamo, ed è la parte utile:

```
GET /v2/specialAreas/all?now=true&mapType=regionMapPolygon
GET /v2/ATCPositions/all?mapType=regionMapPolygon
GET /v2/subcenters/all?mapType=regionMapPolygon
```

⚠️ `mapType` sceglie **quale** dei due campi torna (`regionMap` o `regionMapPolygon`), non se torna pieno:
provati entrambi, più `all` e `polygon` (→ `400`).

E il verdetto:

| Chiamata | Risposta |
|---|---|
| `/v2/subcenters/all?mapType=regionMapPolygon` | 1491 elementi, **0 con poligono** |
| `/v2/ATCPositions/all?mapType=regionMapPolygon` | 11 850 elementi, **0 con poligono** |
| `/v2/specialAreas/all?now=true&mapType=regionMapPolygon` | 2309 aree, **0 con poligono** |
| idem, **senza token**, con `Origin`/`Referer` di webeye — cioè la chiamata *esatta* che fa la loro mappa | 2309 aree, **0 con poligono** |

**Non è una scelta e non è un permesso che ci manca: il dato è vuoto alla sorgente, per tutti.** In questo
momento anche la mappa ufficiale di IVAO sta disegnando cerchi al posto dei confini.

ⓘ Gli endpoint `/all` restano un guadagno da tenere a mente a prescindere: oggi l'anagrafica costa **una
chiamata per posizione** (192 per l'Italia, più 37 per i subcenter), e lì stanno tutte in una.

### Che fare

Con la correzione applicata il danno non si allarga più, e se le shape tornassero l'upsert le riprenderebbe
da sé. Nel frattempo restano due strade, indipendenti:

1. **Ripristino dal backup del 25 agosto** — `tools/ripristino-shape/ripristina-poligoni.sql`, 196 poligoni
   recuperabili (137 ACC + 59 APP), TWR escluse.
2. **Il ripiego permanente dal sectorfile Aurora** — `DYNAMIC_SEC/` sul repo `ivao-italy/it-aurora-sector`
   contiene **112 blocchi** CTR/APP/MIL/FSS nello stesso formato di `twrs.tfl`, che già parsiamo. Due
   ostacoli misurati: **233 righe sono nomi di punto** (`TUFTE;TUFTE;`) da risolvere col catalogo navaid, e
   **un'intestazione può portare più callsign** (`LIBB_ES_CTR LIBB_EU_CTR`) — oggi il parser li tratterebbe
   come una chiave sola.
