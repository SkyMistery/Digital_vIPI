# STAR dal sectorfile, e che altro può seguire la sorgente (20 settembre 2026)

> Stato: 🟡 **slice 1 fatta** — il parser delle STAR c'è ed è misurato sui file veri. Il resto (entità, import,
> editor, tabella pubblica, riferimenti nel testo) è **in attesa di una decisione**, §3.

**La richiesta del committente (20 settembre 2026):** ora che il riferimento alle SID nel testo funziona
(§A73, [carta del 18 settembre](2026-09-18-riferimenti-sid-nel-testo.md)), estenderlo alle **STAR** — «vedi se
riesci a montare un parser che tiri fuori le STAR, quelle che hanno una pista o più piste, **non le MAPS**» —
e poi alle **piste** e alle **frequenze**. Più: che altro potrebbe adottare lo stesso meccanismo.

## 1. Misurato sui file veri (90 `.str` della divisione, copia di lavoro del 20 settembre)

Il file `<icao>.str` ha **lo stesso formato** del `.sid`: `ICAO;pista[:pista…];CODICE;labelLat;labelLon;tipo;transition;RNAV;`.
Non contiene però solo STAR — un quarto delle sue etichette sono voci del **menu mappe** (`STATO_SECTORFILE_ITALIANO.md` §6.1).

| Misura | Valore |
|---|---|
| File `.str` | 90 |
| **Righe STAR estratte** | **645** (865 contando le piste separate, come le SID) |
| Aeroporti con almeno una STAR | **54** |
| RNAV / convenzionali | 522 / 343 (per pista) |
| Punto da rivedere a mano | 136 su 865 = **15,7 %** (si abbatte con gli alias: `ELKA`, `PIMO`, `NEVN`, `MARE`, `DOGU`, `LUMA`, `KAPP`, `PERO`, `LOME` coprono la metà) |
| Scartato perché non è una STAR | 339 voci `MAPS` + attese (tipo 2) + IAP (tipo 3) + FAP (tipo 4) + CTR/ATZ (tipi 1 e 5) |

Gli scali più carichi: LIRF 43, LIBD 38, LIPO 23, LICJ 22, LICA 21, LICZ 21.

## 2. La regola: due filtri, nessuno dei due sul nome

Una riga del `.str` è una STAR quando:

1. **ha almeno una pista vera** nel campo 2. I gettoni si separano su `:` e si tengono solo quelli di forma
   pista (due cifre più `L`/`R`/`C`). Questo butta le voci di menu (`MAPS`, `MAPS:07`) e le piste finte
   dell'ICAO militare `LIZZ` (`BULL`, `AAR`, `AEW`). ⚠️ `MAPS` può stare **dentro** l'elenco —
   `LIPA;05:MAPS;ROSK1E;…` è una STAR vera che è anche voce di menu: si butta il gettone, non la riga.
2. **ha il tipo vuoto** (campo 6). È il campo che dice che cosa disegna la riga: `1` CTR, `2` attesa
   (`HLD-ELVAD`), `3` IAP (`RNP25`, `VOR22L`), `4` FAP, `5` ATZ. **Tutte queste convivono con una pista vera
   nel campo 2**: senza questo secondo filtro entrerebbero 485 righe che non sono procedure d'arrivo. Si
   tollera `0` perché è il «niente» che i `.sid` scrivono nella stessa colonna.

Il **nome non filtra niente**: i militari scrivono `TANGO REC`, `HITACX14L(ATC)`, `VOR-TAG` — una ventina di
righe su 645 — e sono STAR quanto `ELKA3A`. Il punto irrisolto si **segnala** (`NeedsFixReview`), come per le SID.

Il resto — completamento del punto troncato (`GILI3A` → `GILIO`) dal catalogo navaid, alias autoritativi,
designatore, `StableKey`, espansione per pista — è **identico alle SID**, e infatti è lo stesso codice.

## 3. La decisione aperta: una colonna, non una tabella gemella

Il gate «modello gemello» di [FEATURE-PROCESS](../FEATURE-PROCESS.md) dice: *mai affiancare un secondo modello
a uno esistente per la stessa cosa*. SID e STAR hanno gli stessi campi (scalo, pista, punto, nome, transition,
tipo, revisione, priorità, nascosta, ciclo AIRAC, correzioni a mano) e la stessa vita (import, merge per
`StableKey`, pubblicazione differita al ciclo, editor, tabella congelata).

**Raccomandazione:** una colonna `Kind` (`Sid`/`Star`) su `AirportSid`, **non** una tabella `AirportStar`.
- costa: una migrazione (colonna con default `Sid`), e ogni query esistente sulle SID va filtrata `Kind = Sid`
  — censimento **intero** prima di toccare, mai un `grep` troncato da `head`;
- risparmia: un importer, un merge, un editor, una derivazione, una sezione pubblica, un meccanismo di
  riferimento, una migrazione di congelamento — tutti in **doppia copia** per sempre, se si separano.
- il DTO di sorgente è **già** unificato in questa slice: `SourceProcedure` (era `SourceSid`) con `ProcedureKind`.

Se la decisione è «tabella a parte», questa carta va riscritta prima di scrivere codice: è il bivio.

### Slice previste (dopo la decisione)

1. ✅ **parser** — `AuroraSectorfileParser.ParseStars` + `SourceProcedure.Kind` + 8 test (questa slice).
2. **sorgente e import** — `<icao>.str` nel provider, `Kind` nell'entità e nel merge, policy e ciclo AIRAC
   come le SID (una STAR nuova esce al ciclo che il changelog dichiara).
3. **editor** — la tabella STAR accanto a quella SID nell'editor aeroporto, stessi gesti (priorità, nascondi,
   correggi il punto, crea alias).
4. **sezione pubblica** — la tabella STAR nel documento d'aeroporto, con il congelamento alla release.
5. **riferimento nel testo** — `[[STAR LIRF ELKA3A]]`, sulle stesse cinque slice già pagate per le SID:
   il codice di `RiferimentiSid` è **già generico sulla radice del nome**, cambia il gettone e la tabella da cui
   si leggono i nomi.

## 4. Che altro può seguire la sorgente (proposte, da scegliere)

Il meccanismo è sempre lo stesso: **nel testo si scrive un riferimento stabile, in pagina esce il dato di oggi**,
e l'editor avvisa quando un riferimento non si risolve più. Vale la pena solo dove il dato **cambia** e dove il
testo lo **ripete**.

| Proposta | Gettone | Che cosa esce | Perché conviene |
|---|---|---|---|
| **Frequenze** | `[[FREQ LIRF_TWR]]` | `118.700`, o il nominativo + la frequenza | La più forte: le frequenze si citano in prosa dappertutto («contatta la TWR 118.700») e cambiano senza che nessuno riapra i documenti. La sorgente c'è già (`Sector.DefaultFrequency`, catalogo importato). |
| **Piste** | `[[RWY LIRF 16L]]` | l'ident di oggi | Le piste si rinominano per la deriva magnetica (16L → 17L, già successo in Italia). Il riferimento non «aggiorna» il testo da solo con la stessa sicurezza delle SID — 16L e 17L non hanno una radice comune — ma **l'avviso in editor** («la pista 16L non esiste più a LIRF») vale da solo. |
| **Nominativi ATC** | `[[ATC LIRR_CTR]]` | il callsign + il nome del settore | I settori si rinominano e si dividono; i documenti li citano a mano. |
| **Punti e radioassistenze** | `[[FIX OST]]` | il nome, e su richiesta frequenza/canale (`OST 114.90`) | Il catalogo navaid porta già frequenza e canale; oggi si ricopiano a mano nelle tabelle. |
| **Quote di transizione** | `[[TA LIRF]]` / `[[TL LIRF]]` | l'altitudine/il livello di oggi | Un numero solo, ripetuto in più sezioni e in più documenti. |
| **Aree speciali** | `[[AREA LI R49]]` | il nome e i limiti | Cambiano per NOTAM/AIRAC; oggi si citano a mano. |

Fuori dal meccanismo, per scelta: il METAR e la pista in uso (sono **già** dinamici, non citati), il glossario
(ha la sua strada), i coordinamenti (vivono in tabelle proprie, non in prosa).

## 5. Verifica

- 8 test nuovi in `AuroraStarParserTests` (righe reali di `lirf.str`, `lipa.str`, `lizz.str`, `lipc.str`).
- Misura dal vivo sui 90 `.str` veri della copia di lavoro del sectorfile: 54 scali, 865 righe, 136 da
  rivedere, 522 RNAV — prova usa-e-getta, non committata, rifattibile in due minuti.
- `dotnet build Vipi.slnx -c Release --no-incremental` verde su entrambi i TFM; suite Infrastructure 1560 verde.
