# Review A80 — STAR dal sectorfile e riferimenti nel testo (20 settembre 2026)

> **Per chi rivede a mente fresca.** Questo foglio è il punto d'ingresso: dice che cosa è cambiato, **perché**,
> dove guardare per primo e che cosa è già stato provato — così la review spende il suo tempo sui rischi veri e
> non a ricostruire il contesto.
>
> Nulla di tutto questo è ancora **consegnato**: sta in `main`, e la consegna ha un avvertimento suo (§7).

## 1. Che cosa è stato chiesto

Il committente, il 20 settembre: «ora che il riferimento alle SID nel testo funziona, vedi se riesci a montare
un parser che tiri fuori le **STAR** — quelle che hanno una pista o più piste, **non le MAPS** — poi le piste,
le frequenze, e dimmi che altro potrebbe usare lo stesso meccanismo».

Deciso strada facendo, dal committente: **una tabella sola** per SID e STAR (rinominandola, «così da poter
mettere tutto dentro») e **tutti e quattro** i riferimenti proposti — frequenze, piste, nominativi ATC, punti.

## 2. I commit, in ordine

| Commit | Che cosa |
|---|---|
| `91210100` | parser delle STAR dai `.str` (+ `SourceProcedure` con `Kind`) |
| `ea6c029f` | `AirportSid` → `AirportProcedure` + colonna `Kind`, **due migrazioni** |
| `de087174` | import `.str` nello stesso giro delle SID |
| `73ea867a` | editor: la tabella STAR sotto quella SID |
| `438fcf80` | sezione «STAR» nel documento (catalogo, derivazione, congelamento) |
| `0d50f4dc` | `[[STAR …]]` nel testo |
| `722fc68e` | 🔴 correzione: la traduzione lasciava partire i riferimenti degli arrivi |
| `9dc2ab9a` | `[[FREQ …]]` e `[[ATC …]]` + **una porta sola** per la sostituzione |
| `b272587c` | `[[RWY …]]`, `[[FIX …]]` e il selettore a quattro chip |
| `65016abc` | lavori aperti |

Carte: [STAR](../feature/2026-09-20-star-e-altri-riferimenti.md) ·
[riferimenti ai dati](../feature/2026-09-20-riferimenti-ai-dati.md) ·
(precedente) [riferimenti SID](../feature/2026-09-18-riferimenti-sid-nel-testo.md).

## 3. Le sei decisioni che reggono tutto

1. **Una tabella per SID e STAR** (`AirportProcedures`, colonna `Kind`), non un catalogo gemello. Il gate
   «modello gemello» lo impone, e il rename toglie l'obiezione che teneva in piedi l'alternativa nel
   [piano import trasferimenti §B2](../design/piano-import-trasferimenti.md) — «un flag su un'entità che si
   chiama `Sid` è un nome che mente».
2. **Il `.str` non è un file di STAR**: un quarto è menu mappe. Due filtri, **nessuno sul nome** — almeno un
   gettone di forma pista nel campo 2, tipo vuoto nel campo 6.
3. **Il verso sta nel riferimento**, e le due famiglie si cercano in due tabelle diverse: a LIBD `BANAV 5Z` è
   una partenza e `BANAV 1F` un arrivo.
4. **Niente «ultimo valore visto»** nei gettoni dei dati: la chiave è stabile, e il ripiego è la chiave stessa.
5. **Una porta sola** per la sostituzione (`Riferimenti.Sostituisci`) e **una** per la risoluzione
   (`IRiferimentiResolver`): i punti di resa sono cinque, e un meccanismo che entra per conto suo ne dimentica
   uno.
6. **Sorgente muta ≠ dato sparito**: `ValoriDato` sa quali famiglie ha davvero guardato, e non genera avvisi
   per quelle che non ha potuto guardare.

## 4. Dove guardare per primo (i rischi, in ordine)

1. 🔴 **Le due migrazioni** `20260920115016` (SQLite) e `20260920115024` (MySQL): corpo **scritto a mano**.
   Lo scaffolding proponeva `DropTable` + `CreateTable` — le ~1470 righe SID di produzione buttate, col
   database che risultava «aggiornato». Da rileggere: il rename dell'indice e della chiave esterna su MySQL, e
   il `Down` che cancella le STAR **prima** del rename.
2. 🔴 **`PostgresSchemaReconciler.TabelleRinominate`**: su quel percorso non ci sono migrazioni, e una tabella
   rinominata sarebbe una tabella nuova e vuota. La lista è l'unica memoria del cambio: non va potata.
3. 🔴 **I filtri `Kind`** in `EfAirportRepository` (nove letture) e in `SaveSidsAsync`: una dimenticanza fa
   uscire un arrivo dove il lettore si aspetta una partenza, o cancella le manuali dell'altro verso.
4. 🔴 **La protezione dalla traduzione** (`TextProtector.ProteggiRiferimenti`): la guardia veloce deve restare
   `RiferimentiProcedura.Contiene` / `RiferimentiDato.Contiene`, mai una `Contains` cablata. È il difetto
   `722fc68e`, muto coi test verdi.
5. ⚠️ **Il valore in cascata** `RiferimentiRisolti`: cambiarne il tipo è una regressione **silenziosa** (il
   compilatore tace, il parametro resta null, il riferimento esce col ripiego). Chi tocca `BlockRenderer` o i
   caricatori deve guardare `RiferimentiRenderTests`.
6. ⚠️ **Il gate AIRAC**: una STAR appena importata non è pubblica finché il ciclo non raggiunge quello che la
   sorgente dichiara. In review è facile scambiarlo per un difetto («la sezione dice: nessuna STAR»).

## 5. Che cosa è già stato provato

**Suite**: verde su entrambi i TFM — Application 2727, Ui 1641, Infrastructure 1574 (net8) / 1565 (net10),
E2E 401, Domain 152, Hosting 68, Assets 61, AuroraBridge 80, AuroraProfiles 65.
`dotnet build Vipi.slnx -c Release --no-incremental` verde (avvisi = errori).

**Dal vivo**, su copie (mai sui dati veri):

| Prova | Esito |
|---|---|
| Parser sui 90 `.str` veri | 645 STAR / 54 scali, 136 col punto da rivedere, 522 RNAV |
| Import dalla sorgente vera (GitHub) | LIRF 206 SID + 86 STAR, LIBD 39+38, LIPO 22+30, ciclo `2610`, secondo giro idempotente |
| Migrazione su copia di **produzione** (MariaDB `vipi_1330`) | 1469 righe intatte, tutte `Kind='Sid'`, indice e FK rinominati; **dietrofront** provato |
| Migrazione su copia del `vipi.db` di sviluppo (SQLite) | 1469 righe intatte |
| Editor (Edge+puppeteer, LIBD) | 38 STAR, colonne senza *Initial climb*, nessun id doppio |
| Sezione pubblica (bozza LIBD) | «STAR» nell'indice fra SID e General procedures, colonne giuste |
| `[[STAR …]]` (`sid-verifica.js`) | tutto verde: chip, riferimento al cursore, sopravvive al ricarico, «Then BANAV 5Z e poi BANAV 1F» |
| `[[FREQ]]`/`[[ATC]]` (`dato-verifica.js`) | «Su 118.300 con Bari Tower», editor **e** documento |
| Selettore + avviso (`selettore-verifica.js`) | chip `SID STAR FREQ ATC`, LIBD per primo, avviso «RWY LIBD 99X — is no longer in the archive» |

## 6. Che cosa NON è stato fatto, e perché

- **La conversione dell'esistente** resta sulle sole SID: i documenti scritti finora non citano STAR (in vIPI
  non esistevano) e le frequenze già scritte non si convertono a tappeto — un `118.700` nel testo può essere un
  esempio o la frequenza di un altro ente.
- **Quote di transizione e aree speciali** come riferimenti: proposte, non scelte.
- **Le classi CSS `sidref-*`** tengono il nome storico: toccarle vuol dire toccare CSS, JS e script di verifica
  insieme, e il guadagno sarebbe solo estetico.
- **Nessun pacchetto**: vedi qui sotto.

## 7. Alla consegna

🔴 È la **prima consegna con una migrazione dal 1.27.0**. Quindi:

- **non è una PATCH**: serve una versione nuova;
- **copia di sicurezza del database prima del carico**, e la riga della migrazione nel foglio del pacchetto
  (come nel `LEGGIMI-PACCHETTO-1.27.0`);
- dopo il carico, la sezione «STAR» compare nei documenti **pubblicati** solo dalla **prossima release** di
  ciascuno: è lo snapshot che fa il suo mestiere, non un difetto;
- le STAR importate restano in attesa del **ciclo d'entrata** dichiarato dalla sorgente.
