# Lavori assegnati ai filoni — la coda

> Scrive **solo l'integratore**, su richiesta del committente. Ogni agente la legge all'apertura: quando prende un
> lavoro, lo porta nel SUO file (`docs/filoni/<filone>.md`) col suo numero (sito: S4, S5…) e qui lo segna «preso».
> Così l'integratore non tocca mai il file di un filone mentre l'agente ci sta scrivendo.

## Sito

### La Ricerca non trova niente — ✅ preso dal sito come **S7** il 24 settembre 2026 (vedi [`sito.md`](sito.md)): partiva solo coi tasti

**Cosa si è visto** (`docs/lavori-aperti.md` §A122). In produzione, da anonimo, `/services/vsop/search` risponde
«0 results» a qualunque parola: LIRF, PISIP, Brindisi, radar. In locale, sulla stessa copia del DB di sviluppo e da
amministratore, **1.44.1 e 1.45.0 danno lo stesso** «0 results for Brindisi»: il difetto c'era già prima della
1.45.0. Non si sa da quando.

**Da fare.**
1. Capire perché. La catena: `SearchPage.razor` → `SearchService` (sotto 2 caratteri non cerca; aggiunge la Guida
   solo sul filtro «Tutti») → `EfSearchRepository`: documenti con `CurrentVersionId`, filtrati per scope e
   `AccCode`, testo cercato nelle **release in vigore** (`ReleaseInVigore.TesteAsync`) attraverso l'indice
   `IndiceDelleRelease`. Candidati da guardare per primi: l'indice (vuoto? mai costruito dopo un riavvio?), il
   filtro su `AccCode` (gli aeroporti e i vSOP militari ce l'hanno?), la testa della release in vigore.
   `git log` sui file della catena per trovare quando ha smesso (T-042 «ricerca e cambiati leggono la release in
   vigore», `3d5e3a71`, è il primo sospettato). Provare prima di cambiare: un test che oggi fallisce.
2. Correggere, con la sua controprova (il test rosso sul commit di prima).

**Da decidere col committente solo se la causa lo chiede** (per esempio: che cosa deve trovare un anonimo).

### Il controllo di consegna deve pretendere un risultato — ✅ preso dal sito come **S8** il 24 settembre 2026 (vedi [`sito.md`](sito.md))

**Cosa.** `.claude/skills/verifica-live/pacchetto-verifica.js` e il passo «la Ricerca» dei fogli
`deploy/atc-ivao/LEGGIMI-PACCHETTO-*.md` controllano solo che **la riga sotto il campo cambi**. Anche «0 results
for …» è una riga che cambia: per settimane la verifica di ogni consegna è stata verde su una ricerca che non trovava
niente.

**Da fare.** Cercare un termine che c'è di sicuro nei documenti pubblici (scelto e scritto nello script, con il
perché) e pretendere **almeno un risultato**; se il termine un giorno sparisce, il controllo deve dirlo come «termine
di prova da cambiare», non come «sito rotto». Aggiornare la frase nel foglio del prossimo pacchetto e nel runbook
`docs/guide/preparare-un-pacchetto.md` (§6, §6-bis, §7). Si fa **dopo** la correzione qui sopra, o il controllo nuovo
nasce rosso.

⚠️ Collegato: `pacchetto-verifica.js` oggi non parte perché Edge in modalità automatica esce subito («Failed to
launch the browser process, Code: 0», 24-set). Se lo trovi ancora così, dillo nel tuo file: non è un difetto del
codice.

### ✅ Avviso «procedura non trovata» nell'editor degli accordi — FATTO e online in 1.44.0 (§A120) · preso dal sito come **S5** il 23 settembre 2026 (S4 era già la larghezza delle colonne) · carta [`2026-09-23-procedure-non-trovate-negli-accordi.md`](../feature/2026-09-23-procedure-non-trovate-negli-accordi.md)

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
