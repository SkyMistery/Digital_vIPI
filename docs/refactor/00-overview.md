# Refactor — Overview 🟢

> Documento madre della revisione strutturale (post round ~23-34). Coordina i 9 doc
> di area. **Stato: i 9 doc del refactor sono chiusi ✅** (01–09 eseguiti, 2026-07). Residui di
> pulizia tracciati nei singoli doc (es. rimozione `*ProfileModels`, create airport unico).
> **Estensione post-refactor:** doc 10 (snapshot totale + `RenderMode`) — **eseguito ✅** (S0→S7, 2026-07-19), su branch `refactor/10-snapshot-totale`, verify live confermata; **in attesa di merge su main**.
>
> **Estensione post-audit:** doc 11 (uniformità dei tre documenti: vIPI ACC / vIPI APP / vLOA) — **eseguito ✅** (P1→P9, 2026-07-30), branch `fix/uniformita-tre-documenti`, suite 663 verde, verifica live confermata dall'owner.

## Perché

Le aggiunte dei round ~23-34 (versioning AIRAC, vLOA confinanti, import SID, ACC/APP
data-driven, unificazione editor) sono state fatte "a piani" senza rivedere la
struttura complessiva. Sintomi accumulati:

- **Duplicazione manual-vs-auto** dei pipeline di import.
- **Due modelli di documento** divergenti che coesistono (classico vs profile).
- **Logica di pubblicazione** con lo stesso switch a 4 vie ripetuto in ~6 punti.
- **File multi-classe** diffusi (contro la regola "un tipo per file").
- **Registry di sezione per-tipo** invece che condivisi (dolore del punto 12).

Obiettivo: ridisegnare area per area applicando Clean Architecture (già in uso),
un tipo per file, porte/adapter puliti, commenti che citano i doc per nome/§.

## Legenda stato

🟢 Analizzato (Stato+Problemi completi) · 🟡 Target in bozza · 🔴 Da rifare · ✅ Refactor fatto

## Indice dei doc di area

| # | File | Copre | Stato |
|---|------|-------|-------|
| 00 | questo | Overview, DAG, principi, glossario | 🟢 |
| — | [REFACTOR-PROCESS.md](REFACTOR-PROCESS.md) | Runbook: come eseguire 1 refactor (Fase 0→4) | 🟢 |
| 01 | [01-import-infra-condivisa.md](01-import-infra-condivisa.md) | Infra import condivisa (L0) | ✅ |
| 02 | [02-import-acc-e-settori.md](02-import-acc-e-settori.md) | Import ACC + subcenter (1+2) | ✅ |
| 03 | [03-import-aeroporti-e-settori.md](03-import-aeroporti-e-settori.md) | Import aeroporti + posizioni (3+4) | ✅ |
| 04 | [04-import-github.md](04-import-github.md) | Import SID da GitHub (11) | ✅ (parte → doc 08) |
| 05 | [05-import-confinanti.md](05-import-confinanti.md) | Import ACC/settori confinanti (5+6) | ✅ (gen vLOA → doc 08) |
| 06 | [06-gerarchia.md](06-gerarchia.md) | Gerarchia albero copertura (7) | ✅ |
| 07 | [07-trasferimenti.md](07-trasferimenti.md) | Trasferimenti (8) | ✅ |
| 08 | [08-modello-documento-ed-editing.md](08-modello-documento-ed-editing.md) | Modello documento + editing (9+12) | ✅ (08a–08i completi; residuo opzionale: create airport via use-case unico) |
| 09 | [09-flusso-pubblicazione.md](09-flusso-pubblicazione.md) | Pubblicazione DocRelease (10) | ✅ |
| 10 | [10-snapshot-totale-e-rendermode.md](10-snapshot-totale-e-rendermode.md) | Snapshot totale + `RenderMode` per sezione (estende 09) | ✅ eseguito (S0→S7), verify live ok, da mergiare |
| 11 | [11-uniformita-tre-documenti.md](11-uniformita-tre-documenti.md) | Uniformità vIPI ACC / vIPI APP / vLOA fra editor, bozza e pubblica (estende 08-10) | ✅ eseguito (P1→P9), suite 663, verify live ok |
| 12 | [12-vista-live-unificata.md](12-vista-live-unificata.md) | Vista live: due pagine gemelle → una, keyed sul callsign + registry per tipo di ente | ✅ eseguito, suite 702, verify live su 12 postazioni |
| 13 | [13-audit-tre-documenti.md](13-audit-tre-documenti.md) | Audit dei tre documenti: catalogo fonte unica anche di «chi rende il corpo» e «obbligatoria», vLOA dal catalogo, gate pubblico su ricerca/«Cosa è cambiato», pannello release unico, una sola resa per sezione comune | ✅ eseguito (S1→S19), suite 1391, verify live ok |
| 14 | [14-quattro-documenti.md](14-quattro-documenti.md) | I **quattro** documenti (l'aeroporto entra nel confronto): guardia release nel servizio, ciclo AIRAC via dalla vLOA, snapshot una volta per pagina, guscio unico degli editor, ciclo unico dei viewer, sezioni dal catalogo alla nascita, un enum solo — **+ prove di parità sui cinque profili** | ✅ **CHIUSO** (P1→P8 + §3i + editor SID), suite 5714, verifica live sull'app vera quattro volte |
| 15 | [15-shape-del-settore-una-porta-sola.md](15-shape-del-settore-una-porta-sola.md) | La **shape di un settore** — anello **e** quote insieme — da colonna a **elenco di pezzi con una fonte**, letta da **una porta sola**: l'aggancio all'AIP vale per tutti i motori, non per due su sei | 🟡 in esecuzione (S0 fatta) |

## Il grafo delle dipendenze (perché i 12 punti non sono indipendenti)

I 12 punti proposti sono una buona mappa tematica ma non un grafo di dipendenze.
Ci sono **due assi** che si toccano solo a render-time:

- **Asse A — struttura/dati**: import → gerarchia → trasferimenti.
- **Asse B — documenti**: creazione → editing → pubblicazione.

I documenti **derivano** AoR/trasferimenti live via service; non li possiedono.
Quindi l'asse B dipende dall'asse A solo quando renderizza, non quando è editato.

```
L0  INFRA IMPORT CONDIVISA (cross-cuts 1-6, 11)                    → doc 01
    GatedImportLoop · IvaoApiClient · IAccDirectory / IAirportDirectory
    · ISectorProjectionService.SyncFromCatalogsAsync · ImportPolicy/StateStore
        │
L1  CATALOGHI
    [1+2] ACC + subcenter        (AccAdminService / AccImportHostedService)   → doc 02
    [3+4] aeroporti + posizioni  (StructureEditingService / AirportSectorImporter) → doc 03
    [11]  SID GitHub             (SidImporter / AuroraSidProvider)            → doc 04
    [5+6] ACC + settori esteri   (NeighbourImportService)                    → doc 05
          └─ dipende da [1+2]: adiacenza geometrica vs settori domestici
        │
L2  [7] GERARCHIA — consuma TUTTI i cataloghi 1-6, albero cross-ACC          → doc 06
        │
L3  [8] TRASFERIMENTI — cammina l'albero via ITopologyProvider → dipende da 7 → doc 07

─── asse B (documenti) ───
LB1 [9]+[12] MODELLO DOCUMENTO — create + edit, strettamente accoppiati       → doc 08
      Root cause del casino:
        classico  Document + DocumentVersion + enum BlockSection   (vLOA, Airport)
        profile   JSON blob + registry per-tipo AppSections/AccSections (ACC, APP)
LB2 [10] PUBBLICAZIONE — spina DocRelease generica + 4 innesti,               → doc 09
      ma switch 4-vie duplicato ~6× + legacy DocumentVersion coesiste
      → dipende da LB1
```

### Risposta agli esempi dell'utente
- **"3 non dipende da 5"** → vero ✓ (sorgenti/repo diversi; condividono solo porte + `SyncFromCatalogsAsync`).
- **"6 non dipende da 7"** → falso ✗ — 6 alimenta 7: l'adiacenza dei confinanti produce
  i callsign esteri che l'albero di copertura consuma (`ListConfiningForeignCallsignsAsync`).

## Principi guida del refactor

1. **Un tipo per file.** DTO/record/interfacce estratti dai file multi-classe.
   Eccezione tollerata: piccoli record strettamente locali a un servizio, se il doc
   di area lo giustifica.
2. **Clean Architecture rigorosa.** Domain non conosce EF; Application definisce le
   porte; Infrastructure le implementa; UI non salta i service. Le eccezioni di
   validazione seguono la convenzione `Vipi.Application.*.ValidationException`
   (mai DataAnnotations — altrimenti la UI non cattura → crash).
3. **DRY sui pipeline.** Un solo corpo di import per categoria; manual e auto lo
   invocano, non lo ri-scrivono.
4. **Polimorfismo, non switch.** Il flusso di pubblicazione deve estendersi a un
   nuovo tipo di documento registrando un descrittore, senza toccare 6 switch.
5. **Sezioni condivise.** Le definizioni di sezione (AoR, Freq, Coord…) sono
   dichiarate una volta e applicate a tutti i documenti; il singolo documento
   sceglie *quali* mostrare e con quali dati, non *come sono fatte*.
6. **Tracciamento.** Ogni refactor aggiorna il suo doc di area (stato ✅ + note) e,
   se cambia lo schema/rotte, i doc autorevoli `spec/modello-dati.md`,
   `spec/mappa-pagine.md`, `history/rounds.md`.

## Glossario

| Termine | Significato |
|---|---|
| **Catalogo** | Anagrafica importata da sorgente esterna: `Acc`, `AccSector`, `Airport`, `AirportSector`. |
| **Proiezione** | Ricalcolo dei `Sector` operativi dai cataloghi (`SyncFromCatalogsAsync`, "Round 20", fonte unica). |
| **Profile** | Documento data-driven salvato come JSON blob (`AccProfile`, `AppProfile`, `AirportProfile`). |
| **Classic doc** | Documento ad albero `Document → DocumentVersion → DocumentSection → ContentBlock` (vLOA). |
| **DocRelease** | Snapshot editoriale AIRAC, entità unica per tutti i tipi (`ReleaseTargetType`). |
| **Confinante** | ACC estero geometricamente adiacente a un settore domestico (genera vLOA). |
| **Derivato** | Sezione il cui contenuto è calcolato live al render (AoR/Freq/Coord), mai congelato nel payload. |

## Findings trasversali (dalla mappatura codice)

1. **Duplicazione manual-vs-auto**: pipeline 1,2,4,11 implementate 2 volte — use-case
   Application (con authz) + `*HostedService` Infra (senza authz), corpi quasi identici.
2. **1+2 e 5+6 sono singole pipeline**, non 4 punti: studiare/rifattorizzare a coppie.
3. **Due modelli documento** = causa del punto 12; definizioni di sezione non condivise.
4. **Switch pubblicazione duplicato ~6×** (snapshot, auth ACC, diff, admin-list, URL, viewer).
5. **File multi-classe**: `NeighbourImportService.cs` (9 tipi), `IvaoApiClient.cs` (12),
   `Documents.cs` (8), `ReleaseService.cs`, `AppSections.cs`, `AccProfileModels.cs`, ecc.

## Ordine di studio / refactor

`00 → 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09` — bottom-up. La base (infra import)
prima, così gli strati sopra si appoggiano su fondamenta pulite. Documenti (08-09) per
ultimi perché sono l'asse più incasinato ma anche il più isolato dal resto.
