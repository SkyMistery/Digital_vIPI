# Indice della documentazione

Mappa di tutti i documenti del progetto, con scopo e stato. Entry point in root: **`../README.md`** (cos'è + build) e **`../HANDOFF.md`** (stato corrente + come riprendere).

**Stato:** 🟢 Autorevole (corrente) · 🔵 Reference (config/integrazione) · 🟣 Design · ⚪ Storico.

## Ordine di lettura consigliato (nuova chat)
1. `../README.md` — cos'è il progetto, architettura, build/run.
2. `../HANDOFF.md` — stato corrente e come riprendere il lavoro.
3. `history/rounds.md` — cosa è cambiato round per round.
4. `spec/modello-dati.md` + `spec/logica-aor.md` — modello dati e logica AoR (cuore testato).
5. `spec/mappa-pagine.md` — rotte del sito.
6. `adr/` — decisioni architetturali (in ordine 0001 → 0006).

## Specifiche tecniche — `spec/` 🟢
| File | Scopo |
|---|---|
| [spec/modello-dati.md](spec/modello-dati.md) | Schema dati / entità EF Core. La **§9** è autorevole; §3–§5 sono storiche (pre-Round 5/13). |
| [spec/logica-aor.md](spec/logica-aor.md) | Logica di visibilità AoR + scenari di test S1–S10 (implementata e testata). |
| [spec/mappa-pagine.md](spec/mappa-pagine.md) | Gerarchia rotte `/vsop` + tabella rotte→file→accesso. |
| [spec/pagine-disabilitate.md](spec/pagine-disabilitate.md) | Pagine spente (rotta rimossa, codice intatto) + come riattivarle. |

## Guide operative — `guide/` 🔵
| File | Scopo |
|---|---|
| [guide/integration.md](guide/integration.md) | Come agganciare il modulo (RCL) a un sito host ASP.NET Core. |
| [guide/config.md](guide/config.md) | Reference completa della configurazione runtime (Division/DataSource/Ivao/Auth/segreti/policy import). |

## Reference — `reference/` 🔵
| File | Scopo |
|---|---|
| [reference/sector-map.md](reference/sector-map.md) | Mappa settori della ACC pilota Roma (seed di test/fixture). |

## Design — `design/` 🟣
| File | Scopo |
|---|---|
| [design/piano-vipi-tool.md](design/piano-vipi-tool.md) | Documento di design strategico di base (requisiti, roadmap). Parti superate dai round successivi. |

## Decisioni architetturali — `adr/` 🟢
| File | Scopo |
|---|---|
| [adr/adr-0001-scelte-architetturali-fondanti.md](adr/adr-0001-scelte-architetturali-fondanti.md) | Fondamenti (stack, layer, persistenza, AoR, polling). Emendato da ADR-0002. |
| [adr/adr-0002-integrazione-e-autenticazione-portabile.md](adr/adr-0002-integrazione-e-autenticazione-portabile.md) | Integrazione come RCL + identità portabile (`ICurrentUserProvider`, scenari A/B/C). |
| [adr/adr-0003-trasporto-live-sse.md](adr/adr-0003-trasporto-live-sse.md) | Trasporto push live = SSE. |
| [adr/adr-0004-configurazione-divisione-e-admin.md](adr/adr-0004-configurazione-divisione-e-admin.md) | Configurazione divisione + derivazione codici admin. |
| [adr/adr-0005-superficie-modulo-e-isolamento.md](adr/adr-0005-superficie-modulo-e-isolamento.md) | Superficie del modulo + isolamento CSS/JS. |
| [adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md](adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md) | Indipendenza dalla sorgente + policy di import (+ nota Round 20: fonte unica). |

## Storia — `history/` ⚪
| File | Scopo |
|---|---|
| [history/rounds.md](history/rounds.md) | **Changelog cronologico** dei round (R5→R22). |
| [history/handoff-round5.md](history/handoff-round5.md) | Handoff di chiusura del Round 5 (fusione Settore/Posizione). |
| [history/handoff-round22.md](history/handoff-round22.md) | Handoff di sessione Round 22 (shape tonda TWR + coord aeroporto + rifiniture trasferimenti/AOR). |
| [history/piano-round20.md](history/piano-round20.md) | Piano esecutivo del Round 20 (fonte unica cataloghi). |
| [history/review-flusso-gap.md](history/review-flusso-gap.md) | Analisi flusso vs documenti (decisioni round 4). |

---
**Nota:** i commenti nel codice sorgente (`.cs`/`.razor`) citano i documenti per **nome e sezione** in forma informale (es. «modello-dati §9.12», «ADR-0001 D5»), non come link a percorso.
