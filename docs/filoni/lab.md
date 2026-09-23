# Filone Sector Lab — stato

> Scrive **solo** l'agente del Lab (cartella `vipi-lab`, ramo `lab/f3`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Storia fino al 23 settembre 2026:
> `docs/lavori-aperti.md` §A71, §A113, §A115, §A116.

## Dove siamo — 23 settembre 2026

- **F3** (l'app, carta [`2026-09-22-f3-l-app.md`](../feature/2026-09-22-f3-l-app.md)): slice 0-10 fatte, prove a
  mano del committente sul fork. Resta la **slice 11** (consegna: workflow `sectorlab-v*`, zip + SHA-256, avviso
  versione, scheda `/services`). Il sito è fuso e online in 1.43.0: la slice 11 non aspetta più nessuno, ma la
  scheda `/services` entra nel sito → la consegna del sito la fa l'integratore.
- **F3-bis** (carta [`2026-09-23-f3-bis-copie-e-mappe-composte.md`](../feature/2026-09-23-f3-bis-copie-e-mappe-composte.md),
  approvata): ✅ **slice 0 = misure** fatta (carta §8 «Traccia»). Gemelli 3/13/1, più chiavi ripetute nello stesso
  file; **58** aggregati (non ~20), 16 con tratti disallineati; regola dei troncati «primo punto già disegnato»
  (102/139); il `<br>` sulle righe per nome si perde nel modello (da sistemare in slice 3-4). D7 chiusa (`1` = RNAV).
  🟡 **D8-D10 (carta §5) da far decidere al committente prima della slice 1.**
- Prove sul fork: eseguibile in `D:\Programmazione\IVAO_Test\SectorLab-prova\`, clone
  `D:\Programmazione\IVAO_Test\it-aurora-sector-test` (può avere modifiche delle prove: `git checkout -- .`).
- Conteggi del filone: `tests/conteggi/Vipi.SectorLab.Tests.txt` e, se si tocca il motore, `Vipi.Sectorfile.Tests.txt`.
