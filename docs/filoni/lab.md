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
  ✅ D8-D10 decise come proposte. ✅ **slice 1** (gemelli nel motore + `Core/Copie`, regola `CopieDiverse`: 17 chiavi
  sul fork). ✅ **slice 2** (propagazione: una voce, più diff, annulla insieme, «allinea anche questo»). ✅ **slice 3** (tag fra virgolette, `composta`,
  `<br>` nel modello: 545/545 e 2782/2782 sul fork). ✅ **slice 4** (rigenerazione, `intere=si` = D8 rivista
  per mappa, regole `CompostaConProceduraAssente`/`CompostaNonAllineata`, STAR dei `.str` modificabili; `lime.str` sul
  fork: STAR spostata → −2 +2). ▶ **slice 5** = scheda «Composta da» con le caselle, mappa nuova, forma scelta da sola. 🟡 Dire al committente: `limf.sid:28` `LIMF18;…` (manca un `;`). ⚠️ Codice comune: `Vipi.Sectorfile/Validazione`
  (`CopieGemelle.cs`, `Regola.CopieDiverse`), `IO/Metadati.cs` (virgolette, `composta`, `intere`), `IO/MappeComposte.cs`, `Regola.Composta*`, `StrRecord`/`StrParser`/`StrSaver`
  (`IniziaUnTratto`), `tools/Vipi.SectorfileProva` (sezione 5b).
- Prove sul fork: eseguibile in `D:\Programmazione\IVAO_Test\SectorLab-prova\`, clone
  `D:\Programmazione\IVAO_Test\it-aurora-sector-test` (può avere modifiche delle prove: `git checkout -- .`).
- Conteggi del filone: `tests/conteggi/Vipi.SectorLab.Tests.txt` e, se si tocca il motore, `Vipi.Sectorfile.Tests.txt`.
