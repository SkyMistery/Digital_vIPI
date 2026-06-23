# Sector map — FIR Roma (LIRR)

Sorgente di verità leggibile della mappa **posizioni ↔ settori ↔ gerarchia ↔ regole** della FIR pilota Roma
(PIANO §11/§17.1). Questo dato è **manuale** (le API IVAO non espongono la gerarchia operativa) ed è la base
della logica di nascondimento per AoR. Implementato nel seed `RomaStructureSeed`.

> ⚠️ Dato **strutturale di esempio** per F1b/F2 (validazione modello + logica). I valori reali (frequenze,
> settori, split) vanno rivisti dagli editor in fase di data-entry.

## Posizioni

| Callsign | Tipo | Kind | Settori posseduti (default) | Genitore top-down |
|---|---|---|---|---|
| `LIRR_NE_CTR` | CTR | Acc | NE | — (radice) |
| `LIRR_EW_CTR` | CTR | Acc | EW | — (radice) |
| `LIRR_SU_CTR` | CTR | Acc | SU, ES | — (radice) |
| `LIRR_ES_CTR` | CTR | Acc | ES | SU |
| `LIRR_TS_CTR` | CTR | Acc | TS | NE |
| `LIRP_APP` | APP | Airport | PISA | NE *(Standalone)* |
| `LIRP_TWR` | TWR | Airport | PISA_TWR | LIRP_APP |
| `LIRF_TWR` | TWR | Airport | — | NE |

`Sector.Key` è prefissata per FIR: `LIRR-NE`, `LIRR-EW`, `LIRR-SU`, `LIRR-ES`, `LIRR-TS`, `LIRR-PISA`, `LIRR-PISA_TWR`.

## Gerarchia (HierarchyRelation, padre → figlio)

```
LIRR_NE_CTR ──< LIRR_TS_CTR
            ├─< LIRP_APP ──< LIRP_TWR
            └─< LIRF_TWR
LIRR_SU_CTR ──< LIRR_ES_CTR
LIRR_EW_CTR  (radice senza figli)
```

## Regole di unificazione (UnificationRule)

| Nome | Priority | Condizione (online) | Assegnazione | Effetto |
|---|---|---|---|---|
| Split SU/ES | 10 | `LIRR_ES_CTR` | `LIRR-ES → LIRR_ES_CTR` | Quando ES è online, il settore ES passa a `LIRR_ES_CTR`; altrimenti SU copre SU+ES. |

Formato JSON nel DB: `ConditionJson = {"online":["LIRR_ES_CTR"]}`, `AssignmentJson = {"LIRR-ES":"LIRR_ES_CTR"}`.

## Scenari verificati (test d'integrazione)

`tests/Vipi.Infrastructure.Tests/RomaAorIntegrationTests` carica questo seed dal DB e verifica gli stati AoR:

- **S1** solo `LIRR_NE_CTR` online → tutti i settori `Covered`.
- **S2** `LIRP_APP` online → `LIRR-PISA` e `LIRR-PISA_TWR` `Online`, `LIRR-NE` `Covered`.
- **S4** `LIRR_ES_CTR` online → split attivo: `LIRR-ES` `Online`, `LIRR-SU` `Covered`.
- **S5** `LIRR_TS_CTR` online → `LIRR-TS` `Online`, `LIRR-NE` `Covered`.
- **S6** `LIRP_TWR` online, `LIRP_APP` offline → `LIRR-PISA_TWR` `Online`, `LIRR-PISA` resta `Covered` su NE (gestione del "buco" intermedio top-down).
