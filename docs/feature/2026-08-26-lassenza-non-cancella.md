# L'assenza non cancella la presenza

> 26 agosto 2026 · ramo `identita-settori`
>
> IVAO ha smesso di mandare i poligoni e risponde `regionMapPolygon: []`. Gli upsert lo prendevano per una
> forma e la scrivevano sopra quella che avevamo: **83 poligoni a zero in un solo import**, misurato sul
> database vero.

## 1. Il difetto

Trovato per strada mentre si misurava altro ([identità dei settori](2026-08-26-identita-dei-settori.md) §7).
`/v2/ATCPositions/{compose}` e `/v2/subcenters/{compose}` rispondono `regionMapPolygon: []` — **tutte e 229**
le righe italiane, verificate contro l'API vera.

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
`GithubTowerShapeService`); i 17 sintetici erano i cerchi di ripiego a 5 NM.

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

## 5. Quel che resta aperto

**Perché IVAO non manda più le shape** non lo sappiamo: potrebbe essere una scelta, un guasto, o una
migrazione in corso da loro. Con questa correzione la cosa non ci fa più danno, ma vale la pena chiederlo —
se le shape tornassero, l'upsert le riprenderebbe da sé senza che si tocchi niente.
