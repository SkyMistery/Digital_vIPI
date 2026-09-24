# Il MIL_CTR raccoglie solo il traffico militare — carta (24 settembre 2026)

> **Stato: ✅ eseguita il 24 settembre 2026** (§2), provata dal vivo, sul ramo `dafare/raggruppa` (filone [lista-da-fare](../filoni/lista-da-fare.md)).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Tocca il ripiego di [rinvio geometrico](2026-09-10-rinvio-geometrico.md)
> e il rilievo «Trasferimento senza ripiego» della Diagnostica.

## La domanda

In Diagnostica compariva, su Milano:

> Chiuso il ricevente il traffico va su UNICOM, ma quel punto lo copre qualcun altro: manca un ripiego
> (LIMM_WS2_CTR → LIMM_MIL_CTR).

`LIMM_WS2_CTR` è la **radice** di Milano. Chiuso lui la catena finisce su UNICOM, e il rinvio geometrico trova
`LIMM_MIL_CTR`, che è SFC–UNL su tutto l'ACC. Il committente spiega perché è un falso rilievo, e fissa la regola
(vale per **tutti** i MIL_CTR):

> I militari controllano solo i traffici militari: LIMM_MIL può essere aperto anche senza il WS2 aperto, ma vede solo
> i trasferimenti che sono settati verso di lui, non anche quelli previsti verso il WS2. E assorbe anche tutti gli APP
> MIL che sono figli di suo padre.

Decisioni prese con lui:

- **D1. APP militare** = l'APP (o DEP) di uno scalo con categoria **Solo militare** (`AirportCategory.MilitaryOnly`).
  Non gli APP «G» degli scali misti, e non il nome.
- **D2. Solo lo stesso padre**: l'APP militare è assorbito dal MIL_CTR suo **fratello** (stesso `ParentCallsign`).
  Un APP militare più in basso nell'albero segue l'albero civile come oggi.
- **D3. Il padre civile resta**: se l'APP militare e il MIL_CTR sono chiusi entrambi, il traffico va al padre civile e
  poi su fino a UNICOM. E il padre, se è aperto, continua a vedere i trasferimenti scritti verso di lui, anche da e per
  quello scalo: il MIL si aggiunge alla catena **prima** del padre, non lo sostituisce.
- **D4. Il MIL_CTR non raccoglie mai per ripiego il traffico di un ente civile**: né per riga, né per geometria. Riceve
  i trasferimenti scritti verso di lui, più gli APP militari fratelli (D2).

## §1 — Pre-flight

1. **Modello.** Nessun concetto nuovo salvato. D2 è una **riga di ripiego implicita** (`FallbackRow.Automatica`):
   non si scrive in `SectorFallbacks`, si calcola da categoria dello scalo e albero, e si aggiunge alle righe
   dichiarate nei posti che le leggono per risolvere (la topologia e la Diagnostica). Così la catena resta **una**:
   risolutore, scala della Diagnostica e disegno in Struttura passano tutti da `FallbackChain`.
2. **Dispatch.** «È militare?» ha già una regola sola, `AccFamigliaAorRegola` (MIL in un pezzo di mezzo del
   callsign): si riusa. Il filtro D4 sta accanto a quello dell'FSS in `CoverageFallback.Resolve`, con lo stesso
   verso: un ricevente militare può farsi raccogliere da un militare, uno civile no.
3. **Ingressi e verifica.** Ricaduta dei trasferimenti (`TopologyBuilder`), Diagnostica (`EfConsistencyReportRepository`),
   Struttura (catena del settore scelto). Test puri su regola e catena; prova dal vivo sulla Diagnostica.
4. **Propagazione.** Non toglie né rinomina niente.

## §2 — Eseguito (24 settembre 2026)

- `RipiegoMilitare` (`Vipi.Application/Content`): `Militare`/`MilCtr` (dal nome, con la regola di
  `AccFamigliaAorRegola`), `Fratelli` (APP militare → MIL_CTR con lo stesso padre) e `ConAutomatiche` (la riga in coda
  alle scritte, senza fascia, mai doppia).
- `FallbackRow.Automatica` e `FallbackStep.Automatica`: la catena sa dire quali voci non le ha scritte nessuno.
- `RipieghiMilitariQuery` (`Vipi.Infrastructure/Persistence`): una lettura sola dall'albero **proiettato** (settori
  attivi, categoria dello scalo), usata da `TopologyBuilder` (ricaduta dei trasferimenti e rinvio della Diagnostica),
  da `EfConsistencyReportRepository` e da `ISectorFallbackService.RipiegoAutomaticoAsync` (Struttura).
- `CoverageFallback.Resolve`: accanto al filtro dell'FSS, un ente militare raccoglie solo se il ricevente è militare.
- Struttura: nella catena del settore la voce automatica porta «automatico · MIL», col perché nel suggerimento.
  L'editor dei ripieghi non la mostra (non si scrive e non si toglie).

Test: `RipiegoMilitareTests` (8), `CoverageFallbackTests` +1 (rosso sul codice di prima),
`RipiegoMilitareTopologiaTests` (2, su SQLite). Application 2965 → 2974, Infrastructure 1614 → 1616.

**Prova dal vivo** (copia del 15 settembre): in Struttura la catena di `LIBG_APP` è «LIBB_MIL_CTR — automatico · MIL»
e poi «LIBB_ES_CTR — padre». Il rilievo «Trasferimento senza ripiego» sulla copia locale non c'era nemmeno prima (i
punti di Milano lì non hanno coordinate): il caso WS2 → MIL lo fissa il test del rinvio.
