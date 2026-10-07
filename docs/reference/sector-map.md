# Sector map — ACC Roma (LIRR)

Sorgente di verità leggibile della mappa **settori ↔ contenimento ↔ regole** della ACC pilota Roma
(PIANO §11/§17.1). Dal round 5 **posizione e settore sono la stessa entità** (`Sector`): ogni settore
è un callsign apribile su IVAO e, al tempo stesso, un volume di spazio aereo. Il contenimento top-down è
un **albero a padre singolo** (`Sector.ParentSectorId`) ed è la base della logica di nascondimento per AoR.

> 🪵 **Round 20 — questa mappa descrive il seed `RomaStructureSeed`**, usato come **fixture di test** (settori
> `IsProjected=false`, mai toccati dalla sync). **In produzione** i `Sector` sono una **proiezione** dei cataloghi
> importati (`AccSector`/`AirportSector`): `Type`/`Kind`/frequenza/`AirportId` e il **`ParentSectorId`** sono
> derivati automaticamente da `SyncFromCatalogsAsync`, e il padre arriva dal **`ParentCallsign`** del catalogo
> (gerarchia per callsign, cross-ACC, editabile in `/services/vsop/admin/sector-structure`). Vedi `../spec/modello-dati.md` §9.12.
> Non si editano più i settori a mano. La struttura logica qui sotto (contenimento → AoR top-down) resta valida.

> ⚠️ Dato **strutturale di esempio** per F1b/F2 (validazione modello + logica). I valori reali (frequenze,
> settori, split) arrivano dai cataloghi importati.

## Settori (entità unificata)

| Callsign | Tipo | Kind | Aeroporto | Padre (contenimento) |
|---|---|---|---|---|
| `LIRR_NE_CTR` | CTR | Acc | — | — (radice) |
| `LIRR_EW_CTR` | CTR | Acc | — | — (radice) |
| `LIRR_SU_CTR` | CTR | Acc | — | — (radice) |
| `LIRR_ES_CTR` | CTR | Acc | — | `LIRR_SU_CTR` |
| `LIRR_TS_CTR` | CTR | Acc | — | `LIRR_NE_CTR` |
| `LIRP_APP` | APP | Airport | LIRP | `LIRR_NE_CTR` *(Standalone)* |
| `LIRP_TWR` | TWR | Airport | LIRP | `LIRP_APP` |
| `LIRF_TWR` | TWR | Airport | LIRF | `LIRR_NE_CTR` |

L'identificatore unico del settore è il **`Callsign`** (non più una `Key` separata): è ciò che riporta
l'ATC online di IVAO ed è la chiave dell'ownership/stato calcolati dall'AoR.

## Contenimento (Sector.ParentSectorId, padre → figli)

```
LIRR_NE_CTR ──< LIRR_TS_CTR
            ├─< LIRP_APP ──< LIRP_TWR
            └─< LIRF_TWR
LIRR_SU_CTR ──< LIRR_ES_CTR
LIRR_EW_CTR  (radice senza figli)
```

Ogni settore **possiede sé stesso** di default; quando un figlio è offline, il suo spazio ricade
top-down sul primo antenato online (o su P). Lo **split SU/ES** è quindi pura gerarchia (`LIRR_ES_CTR`
figlio di `LIRR_SU_CTR`).

## Quel che l'albero non sa dire: la catena di ripiego

Un settore assegnato a chi **non** è un suo antenato — due settori alti che si sostituiscono a vicenda, come
`LIMM_ES5_CTR` e `LIMM_WS5_CTR` — si scrive con una **riga di ripiego** in Struttura: «da quota a quota →
settore», guardata prima del padre. La stessa catena la leggono trasferimenti, mappa AoR e tabella delle
configurazioni (`FallbackChain`, carta `../feature/2026-10-04-copertura-unica.md`).

> ⛔ **Le `UnificationRule` non esistono più** (6 ottobre 2026, migrazione `ViaLeRegoleDiUnificazione`). Erano
> un motore senza editor, con zero righe in sviluppo e in produzione: un secondo modo, invisibile, di dire chi
> tiene chi. Carta `../feature/2026-10-06-sezioni-condivise.md` §7.

## Scenari verificati (test d'integrazione)

`tests/Vipi.Infrastructure.Tests/RomaAorIntegrationTests` carica questo seed dal DB e verifica gli stati AoR
(chiavi = callsign):

- **S1** solo `LIRR_NE_CTR` online → tutti i settori `Covered`.
- **S2** `LIRP_APP` online → `LIRP_APP` e `LIRP_TWR` `Online`, `LIRR_NE_CTR` `Covered`.
- **S4** `LIRR_ES_CTR` online → `LIRR_ES_CTR` `Online`, `LIRR_SU_CTR` `Covered` (puro contenimento).
- **S5** `LIRR_TS_CTR` online → `LIRR_TS_CTR` `Online`, `LIRR_NE_CTR` `Covered`.
- **S6** `LIRP_TWR` online, `LIRP_APP` offline → `LIRP_TWR` `Online`, `LIRP_APP` resta `Covered` su NE.
