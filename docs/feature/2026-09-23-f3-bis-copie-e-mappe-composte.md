# F3-bis — Le copie che restano uguali e le mappe composte (23 settembre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md) (§7 fasi, §8.2 tag `//@`).
> Carta di F3: [`2026-09-22-f3-l-app.md`](2026-09-22-f3-l-app.md) (l'app su cui si costruisce).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). 🟡 **Da far leggere al committente prima della slice 0.**

## §0 — La domanda

Dalle prove a mano di F3, due richieste del committente:

1. **Le copie restano uguali.** Uno scalo, una pista, una frequenza stanno scritti due volte: nel file nazionale
   (`itap.ap`, `itrw.rw`, `itfreq.frq`) e in quello della FIR (`lirr.ap`, `lirr.rw`, `lirr.frq`…). Si cambiano **una
   volta** e il Lab porta la stessa modifica su tutte le copie.
2. **Le mappe composte.** Le mappe `MAPS` che raccolgono delle procedure (`STAR RNAV(ALL)`: attivata, mostra quelle
   STAR) oggi si **ricopiano a mano** e restano indietro quando una procedura cambia. L'AOD **sceglie** quali procedure
   ci entrano; l'elenco sta nei **metadati** della mappa, e il Lab la **rigenera** da solo.

## §1 — Le misure (fork `SkyMistery/it-aurora-sector-test`, 23 settembre)

| Famiglia | Record | In più di un file | Copie **già diverse** oggi |
|---|---|---|---|
| Scali `.ap` (`itap.ap` + 4 FIR) | 131 ICAO | **128** | **4**: `LIBA`, `LICK`, `LIDW`, `LIZZ` |
| Piste `.rw` (`itrw.rw` + 4 FIR) | 255 righe `MAPS` + le piste | quasi tutte | **14** coppie scalo/pista |
| Frequenze `.frq` (`itfreq.frq` + 4 FIR) | 200 posizioni | **200** | **2**: `LIMF_WN0_APP`, `LIMM_WS5_CTR` |

- Le mappe `MAPS`: nei `.rw` ogni scalo ha una pseudo-pista `MAPS` (255 righe, tutte a coordinate zero); il contenuto
  sta nei `.str`/`.sid` (88 file), righe `ICAO;MAPS;<nome>;…` seguite dai punti (`LIME CTR`, `RWY10`,
  `STAR RNAV(ALL)`). Mappe che sono **aggregati di procedure** fatti a mano: ~20 (`STAR RNAV(ALL)` ×7,
  `STAR VOR(ALL)`, `STAR RNAV1(ALL)`, `STAR30(ALL)`, `STAR-HITAC-21R(ALL)`…).
- I tag `//@` ci sono già (F2 slice 7, `IO/Metadati.cs`): `//@NOME chiave=valore …` sopra il record, **agganciato al
  nome**. ⚠️ Il nome si legge fino al primo spazio: `STAR RNAV(ALL)` oggi **non si può dichiarare** (§5, D4).

## §2 — Cosa si fa

### 2.1 Le copie gemelle

- **Famiglie**: i file dello stesso formato in `OTHER/` — `.ap`, `.rw`, `.frq`. Due record sono **gemelli** se hanno la
  stessa **chiave**: l'ICAO per gli scali, scalo + coppia di piste per i `.rw` (`MAPS` compresa), il codice della
  posizione per le frequenze. Si ricavano dai file aperti, non da un elenco scritto a mano (un file di FIR nuovo
  entra da solo).
- **Una modifica di campo si propaga**: cambiando un campo di un record, il Lab applica lo stesso cambio ai gemelli
  **che in quel campo avevano lo stesso valore** di partenza. È **una voce sola** nel pannello («LIRF Elevation 13 → 14,
  anche in `itap.ap`»), con i diff di tutti i file, e si annulla tutta insieme.
- **Un gemello già diverso non si tocca** in silenzio: il pannello lo dice («in `lirr.ap` LIBA ha un altro valore: non
  cambiato») e l'AOD decide (§5, D2).
- **Il validatore** ha una regola nuova, *copie diverse* (avviso): oggi 4 + 14 + 2. Nel pannello dei problemi, il clic
  porta al record, e da lì si può allineare.

### 2.2 Le mappe composte

- **La dichiarazione**, nei metadati del record `MAPS`, con la grammatica di F2:
  `//@"STAR RNAV(ALL)" composta=ODINA4E,OGVON1E,25:NENI5A` (D4, D5).
- **La rigenerazione**: il corpo della mappa = i tracciati delle procedure elencate, nell'ordine dell'elenco, ognuna
  come tratto a sé (il modo in cui gli aggregati di oggi separano le procedure si **misura** nella slice 0 e si
  rispetta, D7). Si rigenera quando una procedura elencata cambia: la mappa entra nelle **stesse** modifiche in
  sospeso, col suo diff, e si salva insieme.
- **Nella scheda** del record `MAPS`: «Composta da», l'elenco delle procedure **dello stesso file `.str`** (D5-D6) con una
  casella ciascuna. Spuntare e togliere riscrive il tag e rigenera la mappa. Una mappa nuova: «+ Record come questo»
  su una `MAPS`, poi si spuntano le procedure.
- **Il validatore**: *procedura elencata che non c'è* (errore), *mappa composta non allineata alle sue procedure*
  (avviso: qualcuno l'ha cambiata a mano, o una procedura è cambiata fuori dal Lab).
- ⚠️ Rovescia per le sole mappe composte la decisione F3 §9.4 («F3 non scrive tag»): l'ha chiesto il committente, e
  non ha bisogno di vIPI (a differenza dell'initial climb, che resta in F7).

## §3 — Cosa F3-bis NON fa

- **Aggiungere o togliere** uno scalo, una pista, una frequenza **in tutta la famiglia** (D3): il Lab dice solo «c'è
  anche in `itap.ap`».
- Le altre chiavi `//@` (`initialclimb`, `fix`, `source`): restano F7.
- Mappe composte di **altro** che procedure (settori, aree): non richiesto.

## §4 — Le slice

| # | cosa | prova |
|---|---|---|
| 0 | **Misure**: gemelli e divergenze per famiglia sull'albero vero; **com'è fatto un aggregato di oggi** (come separa le procedure, se coincide con l'unione delle procedure che nomina) | numeri nel §8 |
| 1 | **Motore**: famiglie e gemelli (`Core`), regola *copie diverse* nel validatore | test sui campioni; 4/14/2 sul fork |
| 2 | **Propagazione** dei campi ai gemelli: una voce, più diff, annulla insieme; il gemello diverso non si tocca | «cambio LIRF in `lirr.ap`» = −1 +1 in due file |
| 3 | **Grammatica** dei nomi con spazi nei tag (D4) e chiave `composta` nel catalogo (motore, `Metadati`) | lettura/scrittura, riscrittura identica di tutto l'albero |
| 4 | **Rigenerazione** delle mappe composte, e dopo ogni modifica di una procedura elencata; regole del validatore | una STAR spostata → la mappa segue, stesso salvataggio |
| 5 | **Scheda**: «Composta da» con le caselle, mappa nuova | bUnit; a schermo sul fork |
| 6 | Prove a mano del committente, Aurora compresa; chiusura | |

Un commit per slice, come F3. La **slice 11 di F3** (consegna) viene dopo, e porta anche questo.

## §5 — Da decidere col committente prima della slice 0 (proposta fra parentesi)

✅ **Decise dal committente il 23 settembre**: D1, D2, D3, D4 come proposte. **D5-D7 modificate**: le procedure di una
mappa composta stanno **nello stesso file `.str`** della mappa (niente SID, niente altri file), e la mappa rigenerata
si scrive **in quel `.str`**. L'elenco resta com'era proposto (nomi separati da virgola, `25:NENI5A` per una pista
sola). Aggiornati di conseguenza §2.2 e le slice. ❓ Resta aperta la domanda sull'ultimo campo `1` (D7).

- **D1 — Quando**: (**adesso, prima della consegna di F3**: la consegna aspetta comunque che il lavoro sul sito sia
  fuso in `main`, e gli AOD ricevono il Lab con le copie allineate).
- **D2 — Gemello già diverso**: (**non si tocca**, lo si dice, e un tasto «allinea anche questo» lo porta al valore
  nuovo) — oppure si sovrascrive sempre.
- **D3 — Aggiungere/togliere nella famiglia**: (**no in F3-bis**, solo l'avviso) — oppure sì, e in quale file di FIR
  (dall'ICAO? da `LIRR` dentro `lirr.ap`?).
- **D4 — Nome con spazi nel tag**: (`//@"STAR RNAV(ALL)" composta=…`, fra virgolette) — oppure si aggancia la
  dichiarazione alla riga `MAPS` sotto senza ripetere il nome.
- **D5 — L'elenco**: (nomi separati da virgola; `25:NENI5A` sceglie la procedura di **una** pista, il nome da solo le
  prende tutte).
- **D6 — Da dove**: (SID **e** STAR dello **stesso scalo**) — oppure solo le procedure dello stesso file.
- **D7 — Come si scrive la mappa rigenerata**: (come gli aggregati di oggi, misurati nella slice 0). ❓ Il committente
  sa dire che cosa fa in Aurora l'ultimo campo `1` di `LIME;MAPS;STAR RNAV(ALL);;;;;1;` (ce l'hanno anche alcune SID:
  `LIRF;25;EKLO8R;;;;;1;`)?

## §8 — Definition of done e traccia

- [ ] Slice 0-6, un commit ciascuna, build Release verde, suite verde contando i progetti.
- [ ] Sul fork: una modifica a uno scalo in `lirr.ap` esce anche in `itap.ap`, e Aurora li legge uguali.
- [ ] Una STAR spostata sposta anche la sua mappa composta, nello stesso salvataggio; Aurora la mostra.
