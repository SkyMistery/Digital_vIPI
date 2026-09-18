# F1 — Archi, cerchi e frasi dell'AIP nel convertitore di coordinate (18 settembre 2026)

> **Stato: CARTA — nessuna riga di codice.** Prima fase «sul sito» di Aurora Sector Lab
> ([carta madre](2026-09-18-aurora-sector-lab.md), §7). Estende il convertitore
> ([`2026-08-29-convertitore-coordinate.md`](2026-08-29-convertitore-coordinate.md)), non lo affianca.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). 🔴 **Nessun dato di vIPI si tocca**: niente migrazioni,
> niente tabelle, flusso pacchetti normale.

## §0 — La domanda

Il committente, 17 settembre: `/services/coordinates` deve aiutare l'AOD a scrivere il sector partendo dal testo
dell'AIP, **archi compresi**:

```
44°51'24" N 008°14'57" E
then arc of circle in clockwise direction radius 17 NM centred on
44°55'29" N 007°51'43" E
till point
44°41'08" N 008°04'34" E
```

Oggi il parser lavora **riga per riga** e prende da ogni riga le coordinate che trova. Su questo testo:
- la riga del **centro** (`44°55'29" N 007°51'43" E`) diventa un **vertice** — l'area esce storta;
- la riga dell'arco produce un «angolo spaiato» (il `17` del raggio è letto come un numero), `till point` una
  «riga non letta».

Le segnalazioni ci sono, ma **nessuna dice il vero problema**, e la forma sulla mappa è sbagliata. È un difetto,
oltre che una funzione mancante. (Da confermare col primo test di caratterizzazione, slice 1.)

## §1 — La grammatica, dai PDF veri

Misurata in F0 su ENR 5.1.x e 2.1.1.4.1 (carta madre §5) e allargata oggi su ENR 2.1.1.1, 2.1.2 e 5.3.
`<C>` = una coordinata in **qualunque** forma che il convertitore già legge (`44°51'24"N`, `44°51'24''N`,
`452630N 0091640E`…): il lettore nuovo **non** ha un suo riconoscitore di coordinate.

**Inglese** (ENR 5.1, CTR):
```
<C> then arc of circle in clockwise direction radius 17 NM centred on <C> till point <C>
<C> then arc of circle in anti-clockwise direction radius 5.0 NM centred on <C> till point of origin.
Circular area centered on <C> within a 1.0 NM radius.            (raggio anche «300.0 M», «5.0 KM»)
<C> ... to point of origin.
<C> Italian northern geographical border till point <C>
<C> line at 500 m from coast to point of origin.
```

**Italiano, in forma bilingue mescolata** (ENR 2.1.1.1, tabella TMA/CTA — l'unico italiano trovato nei 154 PDF):
```
453030N 0091225E quindi arco di cerchio in senso antiorario di raggio/then arc of circle in anti-clockwise
direction radius 5.0 NM centrato in/centered on 452630N 0091640E fino al punto/till point 452335N 0091054E,
quindi linea congiungente i punti ...
... in direzione WEST lungo il fiume Po fino a/then WEST direction along Po river until 451052N 0081539E - ...
```

Varianti da reggere, tutte viste: `centred`/`centered`/`centrato su`/`centrato in`/`centrato/ centered on`;
`clockwise`/`orario`, `anti-clockwise`/`antiorario`; raggio in `NM`, `KM`, `M`; separatori `;` `,` `-` fra i
vertici; parole connettive da ignorare (`then`, `quindi`, `poi`, `linea congiungente i punti`, `then line joining
points`); la frase **va a capo ovunque** (anche fra numero e unità: `17.0  NM`). Un poligono può avere **più archi**
(Cagliari CTR: 17 e 25 NM; LI R503/A: 16 e 18,5 NM) e **più tratti di confine**.

## §2 — Le misure che decidono (sector-lab-f0\pdf-aip\raggi.py)

Su **64 archi veri** (128 estremi) di ENR 5.1.x e Milano CTA, distanza inizio→centro e fine→centro contro il
raggio dichiarato:

| | mediana | 95° percentile | massimo |
|---|---|---|---|
| ellissoide WGS84 (geographiclib) | 10 m | 46 m | **91 m** (LI R38, R = 15 NM) |
| sfera | 10 m | 65 m | **97 m** |

Le coordinate AIP sono arrotondate al **secondo** (~30 m): lo scarto è rumore di arrotondamento.
Conseguenze, scritte qui perché non si ridecidano a occhio:
1. **La sfera basta** (differenza con l'ellissoide ≤ 50 m, sotto la precisione del dato). Nessuna libreria nuova.
2. **Avviso «raggio incoerente» oltre 0,1 NM (185 m)**: passano tutti i 128 estremi veri; un centro sbagliato o
   un incolla tagliato sbagliano di miglia, non di metri.
3. **Il raggio si interpola** fra la distanza vera dell'inizio e quella della fine, lungo l'angolo: l'arco passa
   **esattamente** per i due punti dichiarati. Usare il raggio dichiarato lascerebbe uno scalino fino a 91 m agli
   estremi.

## §3 — Il motore

**Un lettore nuovo, dentro `Vipi.Application/Coordinates`, puro e senza I/O**: `AipGeometryReader`.

1. **Riconoscimento**: il testo contiene almeno una frase del vocabolario (`arc of circle`, `arco di cerchio`,
   `circular area`, `point of origin`…). Se no, il parser di oggi resta **identico** — è la garanzia per tutti gli
   ingressi che già funzionano (§10, domanda 4).
2. **Lettura a flusso, non a righe**: il testo si normalizza (a capo → spazio, i segni di `NormalizzaSegni`) e
   si legge come sequenza di **coordinate** e **frasi**. Le coordinate le riconosce il codice che c'è già
   (`ProvaToken`/`ProvaCoppia`, esposti come `internal`: slice 0, meccanica).
3. **Il vocabolario è una TABELLA** (frase IT/EN → significato), non una catena di `if`: aggiungere una variante
   vista in un PDF = aggiungere una riga.
4. **Uscita = quella di oggi**: `CoordinateReadResult` con le sue `CoordinateArea`. Un'area finisce a
   `point of origin` / `punto di origine` o col cerchio; più aree nello stesso testo = più aree (il selettore del
   convertitore c'è già). Nome: `Area 1`, `Area 2` (gli identificativi tipo `LI R48` sono F6).
5. **Geometria**: arco e cerchio sul **cerchio massimo** della sfera (punto a distanza e rotta dati), densità
   **regolabile**, di base **1 punto per grado** (decisione 13 della carta madre), raggio interpolato (§2.3).
   Verso orario/antiorario dal testo. Un cerchio = anello chiuso di 360/densità punti.
6. **Cosa NON si disegna, e si dice**: confine di stato, costa, fiume, «linea a 500 m dalla costa». Il lettore
   unisce i due capi con una **retta provvisoria** e lo segnala con la frase originale: la geometria vera sta in
   `GEO/itgeo.geo`, ed è lavoro del Lab (F6), non del sito.

**Segnalazioni nuove** (codici in `CoordinateIssueKind`; i testi li scrive la UI, IT+EN):

| Codice | Quando |
|---|---|
| `RaggioIncoerente` | un estremo dista dal centro più di 0,1 NM oltre il raggio dichiarato (dettaglio: di quanto) |
| `TrattoNonDisegnabile` | confine, costa, fiume: unito con una retta, dettaglio = la frase |
| `FraseNonRiconosciuta` | 🔴 la **guardia** di F0: testo che non è né coordinata né frase del vocabolario. In F0 gli identificativi fuori standard (`EUC 60`, `Zona '29'`) **saldavano** un'area alla precedente senza errore |
| `ArcoIncompleto` | manca il centro, il raggio o il punto d'arrivo |

⚠️ **Nulla si scarta in silenzio** (regola del convertitore, §7 della sua carta): vale anche per le frasi.

## §4 — La pagina

Minimo indispensabile, nessuna sezione nuova:
- **Densità degli archi** (punti per grado, 1 di base; campo che compare **solo** se l'ingresso ha archi o cerchi —
  regola del convertitore: un campo che non cambia niente di ciò che si vede non c'è).
- Una riga di conto accanto a quella che c'è: «**2 archi e 1 cerchio convertiti · 184 punti**».
- **Sulla mappa, i centri** degli archi come crocette (spente di base): chi controlla vede subito un centro messo
  male. ⚠️ Se complica `AccAor`, si toglie: non è la funzione, è un aiuto.
- Le segnalazioni nuove nella lista che c'è, coi testi nei due `.resx`.
- **Guida** (`GuidaPage` + `GuideSearchCatalog`): come si incolla un'area dall'AIP.

Uscite (DB IVAO, sectorfile a punti/segmenti, DMS puntato/compatto): **invariate**. L'arco esce come i vertici
che lo approssimano.

## §5 — Cosa F1 NON fa

- Non legge i PDF: si incolla il testo (la lettura dei PDF è F6, nell'app).
- Non disegna confini e coste (F6, da `itgeo.geo`).
- Non riconosce gli identificativi delle aree (`LI R48 A`): F6, con la guardia.
- Non scrive `//@` né tocca il sector: è il sito.
- Non salva niente: resta un attrezzo senza stato.

## §6 — Le slice

| # | cosa | dove | prova |
|---|---|---|---|
| 0 | `ProvaToken`/`ProvaCoppia`/`NormalizzaSegni` diventano `internal` e riusabili — **nessun cambio di comportamento** | Application | i test del convertitore verdi **senza modifiche** |
| 1 | Caratterizzazione: il testo del committente **oggi** (centro come vertice) | Tests | il test fissa il difetto; la slice 3 lo ribalta |
| 2 | Geometria: arco e cerchio sul cerchio massimo, raggio interpolato, densità, `RaggioIncoerente` | Application | arco di 90° a 1 pt/° = 91 punti; gli estremi **esatti**; LI R38 non avvisa, un centro spostato di 1 NM sì |
| 3 | `AipGeometryReader`: vocabolario EN, flusso, aree a `point of origin`, cerchio | Application | l'esempio del committente; Cagliari CTR (due archi); LI R503/A |
| 4 | Italiano e bilingue mescolato; coordinate compatte; separatori `-` `,` `;` | Application | il blocco di ENR 2.1.1.1 del §1, alla lettera |
| 5 | Confine/costa/fiume (`TrattoNonDisegnabile`), `FraseNonRiconosciuta`, `ArcoIncompleto` | Application | Zone '18' Monte Bianco (due tratti di confine); `EUC 60` e `Zona '29'` segnalati |
| 6 | Aggancio in `CoordinateParser.Parse` (un ramo, nel posto dove si decide già il formato) | Application | tutti i test del convertitore verdi; il ramo nuovo scatta **solo** col vocabolario |
| 7 | Pagina: densità, riga di conto, testi IT+EN, crocette dei centri | Ui | bUnit: senza archi il campo densità non c'è |
| 8 | Guida | Ui | il servizio si trova cercandolo |
| 9 | **Verifica live** | — | traccia qui sotto |

Un commit per slice; `dotnet build Vipi.slnx -c Release --no-incremental` verde **su entrambi i TFM** a ogni commit.

**Fuori dal repo, a ogni slice del motore**: `sector-lab-f0\pdf-aip\raggi.py` allargato a far passare **tutti i
64 archi** dal lettore nuovo (via un piccolo eseguibile di prova) e a confrontare i vertici con `italy.*` del
sector. ⚠️ I testi AIP interi **non** entrano nel repo (pubblico, copyright ENAV): nei test solo i pochi esempi
del §1.

## §7 — Pre-flight, le quattro domande

1. **Modello**: nessuna entità, nessun tipo punto nuovo. L'uscita è la `CoordinateArea` di oggi. Si aggiungono
   **valori** all'enum delle segnalazioni, non un secondo modello di segnalazione.
2. **Dispatch**: il formato si decide in **un** posto (`CoordinateParser.Parse`: KML, JSON, righe); l'AIP è un
   ramo in più **lì**. Le frasi sono una **tabella**, non uno switch: aggiungere una variante non tocca codice.
3. **Ingressi + verifica**: stessa pagina, stesso campo di incolla. Si verifica incollando l'esempio del
   committente, il blocco italiano di ENR 2.1.1.1 e Cagliari CTR, e guardando la forma sulla mappa contro
   `italy.restrict` del sector.
4. **Propagazione**: nessuna rimozione né rinomina. ⚠️ L'unico rischio è di **comportamento**: un testo che oggi
   si legge bene non deve cambiare. Per questo il ramo scatta solo col vocabolario (§3.1) e la slice 6 si prova
   sull'intera suite del convertitore.

## §8 — Trappole note

- ⚠️ **Parole comuni nel vocabolario**: `then`, `point`, `from` compaiono anche nei commenti degli AOD. Il ramo
  AIP si accende solo con le frasi **lunghe** (`arc of circle`, `point of origin`, `circular area`), mai con una
  parola sola.
- ⚠️ **Le unità si leggono solo al loro posto**: il raggio è sempre `numero + unità` subito dopo `radius` /
  `di raggio` / `within a`. Altrove `M` e `NM` non vogliono dire niente (e il `17` del raggio non è un angolo:
  è proprio l'errore di oggi).
- ⚠️ **Tetti**: `MaxRighe` (5 000) resta; in più un tetto ai **punti generati** (densità massima 10 pt/°, e un
  totale per ingresso), perché il testo — e i punti — passano dal circuito Blazor.
- ⚠️ `Assert.Equal(a, b, 0)` non è una tolleranza (trappola già pagata nel convertitore).
- ⚠️ Stringhe in **tutti e due** i `.resx`; `dotnet test` esce 0 anche rotto: il verde si legge contando i progetti.
- 🔴 Modifiche ai sorgenti **solo con l'editor**: in F0-bis un heredoc ha scritto `\b` e `\f` come caratteri di
  controllo veri dentro le regex, che non trovavano più niente senza errore.

## §9 — Definition of done

- [ ] Slice 0-8 fatte, un commit ciascuna, build Release verde sui due TFM, suite verde contando i progetti.
- [ ] I test del convertitore di prima **verdi senza modifiche** (salvo quello di caratterizzazione, ribaltato
      di proposito in slice 3 e scritto nel commit).
- [ ] I 64 archi veri passano dal lettore senza `RaggioIncoerente`; fuori repo, confronto coi contorni di `italy.*`.
- [ ] **Verificato live** (skill `verifica-live`): l'esempio del committente, Cagliari CTR, il blocco italiano di
      ENR 2.1.1.1 — con traccia qui sotto.
- [ ] Nessun dato di vIPI toccato; nessuna migrazione.

## §10 — Verifica live

*(da scrivere a lavoro fatto)*
