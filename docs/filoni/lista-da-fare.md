# Filone «Da fare»: raggruppare per cambiamento 🟡

> Aperto il 23 settembre 2026. Cartella `vipi-dafare`, ramo `dafare/raggruppa`. Lo scrive solo questo filone.

Il committente ha chiesto di rivedere il meccanismo che dice che cosa è cambiato e quali documenti ripubblicare:
la lista «Da fare» è un elencone per documento, e la vuole compattata **per cambiamento** — si apre il
cambiamento e si vedono tutti i documenti su cui intervenire. Analisi di partenza: verifica dell'11 settembre
(due difetti + migliorie), rifatta il 23 settembre sul codice di `6b5b438d`.

## Ordine deciso dal committente

| # | Cosa | Stato |
|---|---|---|
| 1 | Difetti: (a) l'incarico «preso in carico» si chiude con la sua segnalazione; (b) la frase di una segnalazione si aggiorna quando il fatto si ripresenta | ✅ |
| 2 | Vista «per cambiamento» (default) · per documento · elenco, con ✓ di gruppo. Zero migrazioni | ✅ carta [2026-09-23-da-fare-per-cambiamento](../feature/2026-09-23-da-fare-per-cambiamento.md) |
| 3 | Deriva ricalcolata poco dopo un salvataggio, con la causa vera (migrazione). Giro misurato: ~2 s | ✅ carta §4 |
| 4 | Casella «segna rilette anche queste N» nel pannello di pubblicazione · dettaglio del cambiamento nella riga · età della riga | — |

Scartato dal committente: il contatore sull'avatar.

⚠️ **Per l'integratore**: il punto 3 porta una **migrazione** (`CausaDelleSegnalazioni`, SQLite + MariaDB: due
colonne nullable su `DocumentImpacts`) e un **servizio in background nuovo** (`DerivaDopoLeModificheHostedService`),
più un interceptor montato su tutti e tre i provider (`SegnalaModificheInterceptor`). Codice in comune toccato:
`Vipi.Application/Content` (WorkItem, WorkListService, WorkGrouping, ImpactDriftUseCase, DocumentImpactService,
ModificheInAttesa), `Vipi.Application/DependencyInjection.cs`, `Vipi.Infrastructure/DependencyInjection.cs`.

## 1 — Difetti (fatto)

- `EfDocumentImpactRepository.ClearAsync` e `ClearBySourceAsync` chiudono (Done) gli incarichi con
  `FromImpactId` sulla riga chiusa, nello stesso salvataggio, con voce di registro `Motivo = SegnalazioneChiusa`.
  La porta è il repository perché da lì passa **ogni** chiusura (✓, riconciliazione, ripubblicazione).
- `PruneClearedBeforeAsync` (giro notturno) risana gli incarichi già rimasti orfani prima della correzione.
- `RaiseAsync` su una riga già aperta riscrive argomenti, `IsPublicNow` e chiave se sono cambiati; identità ed
  età (`RaisedUtc`) restano.
- 4 test in `DocumentImpactLookupTests`, rossi sul codice di prima. Infrastructure 1604 → 1608.

Codice in comune toccato: `Vipi.Infrastructure/Persistence/EfDocumentImpactRepository.cs`.
