# Procedure non trovate negli accordi (S5) — carta

> Assegnato dall'integratore il 23 settembre 2026 (`docs/filoni/da-fare.md`, sezione Sito). Filone sito, **S5**
> (S4 è la larghezza delle colonne delle tabelle, fatta lo stesso giorno). Carta breve: tre decisioni del
> committente PRIMA del codice. ✅ **Decise il 23 settembre 2026** (§0), il resto della carta è il ragionamento. ✅ **Fatto** lo stesso giorno
> (stato in `docs/filoni/sito.md`).

## 0. Decisioni del committente (contratto)

- **D1 — ciclo: ENTRANTE, ma l'anticipo è di 2–3 giorni, non settimane.** Regola:
  | la procedura… | avviso |
  |---|---|
  | manca OGGI e manca nell'ENTRANTE | sempre |
  | c'è oggi, manca nell'entrante (sparisce col ciclo che viene) | solo negli ultimi **3 giorni** prima del cambio ciclo |
  | manca oggi, c'è nell'entrante (nuova, scelta dai suggerimenti) | mai |
  I 3 giorni sono UNA costante nominata («2–3» del committente: si prende il margine largo).
- **D2 — tasto sulla barra**: terzo tasto diagnostico nell'editor dei trasferimenti, «⚠ Procedure non trovate (n)»,
  sempre presente e spento a zero, elenco accordo · sezione · clausola · nome scritto, clic → clausola. (Opzione A.)
- **D3 — riga fissa** nell'avviso sulle copie pubbliche congelate. (Opzione A.)

## 1. Il difetto

Fra i punti di una clausola di trasferimento si può scrivere una SID o una STAR (`ProceduraNeiPunti.E`: 2–5
lettere, cifra, lettera). In lettura il nome segue l'archivio per **radice** (`ProceduraNeiPunti.ConNomiDiOggi`,
chiamata dalle due porte di `AgreementEditingService`): ERIKA 1A → ERIKA 2A esce da sé. Se invece il sectorfile
cambia il nome davvero (ERIKA 1A → ERIKA 1B, o un altro punto), la radice non si trova più e l'accordo mostra il
vecchio nome **senza dirlo a nessuno** — nell'editor, nelle vIPI ACC/APP, nella vLOA e nel ponte.

Nei documenti lo stesso caso è già segnalato (`ControlloProcedureCitate.Controlla` → avviso in cima a
`DocumentSectionsEditor`). Per gli accordi il controllo non c'è.

## 2. Che cosa si fa (non in discussione)

- Per ogni clausola con una procedura fra i punti: si cerca il nome negli scali della sezione, nel verso del
  flusso (`ProceduraNeiPunti.Versi`: arrivi → STAR, partenze → SID, altri → SID poi STAR). **Non trovata** =
  nessuno scalo della sezione ha quella radice in nessuno dei versi provati.
- L'avviso dice: **dove** (accordo, sezione, clausola), il **nome scritto**, e che in pagina **esce così com'è**.
- La regola vive in `Vipi.Application` accanto a `ProceduraNeiPunti` (funzione pura: accordi + `NomiProcedura`
  → elenco), letta dalle stesse tabelle che già si derivano (`ProceduraNeiPunti.TabelleCitate`): nessuna query
  in più per gli ACC senza procedure fra i punti, cioè quasi tutti.
- Niente correzione automatica: il nome nuovo non si indovina (la radice è cambiata). Si corregge a mano,
  dall'avviso si arriva alla clausola.

## 3. Le tre decisioni

### D1 — Il ciclo del confronto: oggi o ENTRANTE?

La lettura usa le procedure in vigore **oggi**; i suggerimenti del form (S3, `ElencoAsync`) quelle del ciclo
**entrante**. Il caso che le separa: ciò che si sceglie dai suggerimenti oggi può non esistere ancora oggi.

| | Confronto con OGGI | Confronto con l'ENTRANTE |
|---|---|---|
| STAR scelta dai suggerimenti, valida dal ciclo che viene | ⚠️ «non trovata» fino al cambio ciclo: falso allarme | ✅ nessun avviso |
| Procedura che sparisce col ciclo che viene | nessun avviso fino al giorno del cambio | ✅ avviso **prima**, quando c'è tempo per correggere |
| Coerenza con ciò che la pagina mostra oggi | ✅ identica | quasi: dal giorno del cambio in poi coincide |

**Proposta: l'ENTRANTE**, come i suggerimenti. L'editor serve a preparare quel che verrà pubblicato. Fra oggi e
il cambio ciclo, una procedura che il ciclo che viene toglie viene segnalata in anticipo. Il costo: per quei
pochi giorni l'avviso segnala un nome che la pagina mostra ancora correttamente. L'avviso lo dice («dal ciclo
del 1° ottobre»).

### D2 — Solo nell'editor, o anche un elenco d'insieme?

L'editor dei trasferimenti (`/services/vsop/admin/transfers`) lavora già **per ACC**: tutti gli accordi di un
ACC in una pagina, con due tasti diagnostici sulla barra, «⚠ Da rivedere (n)» e «◎ Lacune (n)», che
attraversano tutti gli accordi. Il terzo avviso ci sta di natura.

- **A (proposta)**: un terzo tasto sulla stessa barra, «⚠ Procedure non trovate (n)», sempre presente e spento a
  zero, come gli altri due. Si apre un elenco: accordo · sezione · clausola · nome scritto, e un clic porta alla
  clausola. È già l'«elenco d'insieme» dell'ACC.
- **B**: A, più un segno sulla singola clausola (nella riga dell'albero), per chi ci arriva senza passare dal tasto.
- **C**: A o B, più un elenco di **tutti gli ACC** insieme (pagina o riquadro di diagnostica). Serve solo se si
  vuole controllare il paese in un colpo solo, per esempio dopo un cambio di ciclo.

### D3 — Le copie pubbliche congelate

In vIPI ACC, APP e vLOA la sezione «Coordinamenti» della copia pubblicata è **congelata**: un nome corretto
nell'editor arriva lì solo ripubblicando.

- **A (proposta)**: l'avviso lo dice in una riga fissa («Le vIPI ACC/APP e le vLOA già pubblicate mostrano il
  nome vecchio fino alla prossima pubblicazione»).
- **B**: l'avviso nomina **quali** documenti pubblicati citano quella clausola. È più utile ma costa di più:
  bisogna leggere le copie congelate.
- **C**: niente riga, avviso solo sull'editor.

## 4. Verifica prevista

- Test della funzione pura: nome trovato, radice cambiata, verso sbagliato (una SID scritta in un arrivo), più
  scali nella sezione, ciclo entrante contro oggi.
- Prova dal vivo su una copia del DB: si rinomina a mano una procedura in archivio, e l'avviso compare
  nell'editor dei trasferimenti con il conteggio giusto; si corregge la clausola e l'avviso sparisce.
