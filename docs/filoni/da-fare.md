# Lavori assegnati ai filoni — la coda

> Scrive **solo l'integratore**, su richiesta del committente. Ogni agente la legge all'apertura: quando prende un
> lavoro, lo porta nel SUO file (`docs/filoni/<filone>.md`) col suo numero (sito: S4, S5…) e qui lo segna «preso».
> Così l'integratore non tocca mai il file di un filone mentre l'agente ci sta scrivendo.

## Sito

### Avviso «procedura non trovata» nell'editor degli accordi — ⏳ da prendere (assegnato il 23 settembre 2026)

**Cosa.** Una SID o una STAR scritta fra i punti di un trasferimento segue l'archivio per **radice** del nome
(`ProceduraNeiPunti.ConNomiDiOggi`, [`ProceduraNeiPunti.cs`](../../src/Vipi.Application/Content/ProceduraNeiPunti.cs)):
ERIKA 1A → ERIKA 2A esce da sé. Ma se il sectorfile cambia il nome davvero (ERIKA 1A → ERIKA 1B, o un altro punto), la
radice non corrisponde più e nell'accordo resta il vecchio nome **in silenzio**. Per le SID citate nei documenti
l'editor lo dice già (`RiferimentiProcedura.Controlla` → `ProceduraDaRivedere.NonTrovata`, mostrato in
`DocumentSectionsEditor`); per gli accordi quel controllo **non esiste**.

**Da fare.** Nell'editor dei trasferimenti (`AdminTrasferimentiPage`) segnalare le clausole che hanno fra i punti una
procedura (forma `ProceduraNeiPunti.E`) che negli scali della sezione, nel verso del flusso (`ProceduraNeiPunti.Versi`),
non si trova più: dove (accordo, sezione, clausola), il nome scritto, e che nella pagina esce così com'è.

**Da decidere col committente** (carta breve prima del codice):
- il ciclo del confronto: oggi o ENTRANTE? La lettura guarda a oggi, i suggerimenti (S3) all'entrante: una STAR del
  ciclo che viene risulterebbe «non trovata» fino al cambio ciclo se si confronta con oggi;
- solo nell'editor, o anche un elenco d'insieme (tutti gli accordi di un ACC) come «Cerca SID scritte a mano»;
- ⚠️ le copie pubbliche di vIPI ACC/APP/vLOA hanno «Coordinamenti» congelata: il nome nuovo arriva lì solo
  ripubblicando. L'avviso può dirlo.
