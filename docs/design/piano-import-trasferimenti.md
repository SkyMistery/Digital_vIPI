# Importare i trasferimenti — le tabelle degli IPI dentro gli accordi

> Carta prima del codice ([FEATURE-PROCESS](../FEATURE-PROCESS.md)). Decisa il **3 settembre 2026**.
> Stato: 🟡 **da eseguire**. Sorella minore di [piano-import-tabelle](piano-import-tabelle.md), che ha già
> costruito la pipeline: qui non si riscrive nessuno stadio, si aggiunge **che cosa** sa leggere e **dove**
> sa scrivere.
>
> ⚠️ La carta è in **due parti**. La **A** non tocca lo schema e si può fare subito. La **B** (procedure
> agganciate, catalogo STAR) vuole tabelle nuove e **aspetta il 16 settembre 2026** — vedi §B0.

## Context

Gli accordi si compilano una clausola alla volta mentre le tabelle **esistono già**, scritte da qualcun
altro, negli IPI e nei briefing veri. Misurato sui tre documenti in `vIPI word/` (3 settembre 2026,
estraendo le tabelle dai `.docx`):

| documento | tabelle | di trasferimento | righe |
|---|---|---|---|
| `vIPI_Brindisi_ACC_2502` | 17 | 10 | 75 |
| `Briefing LIBB_ACC_Settore_CS0` (2501 e 2502) | 10 | 5 | 41 |
| `IPI ROMA ACC 0412` | 92 | 87 | ~330 |

**~450 righe**, e nel solo Roma **33 forme d'intestazione distinte**. Le famiglie che contano:

| righe | intestazione | dove |
|---|---|---|
| 75 | `CONDIZIONI \| ROTTA ATS \| COP \| ENTE \| FL` | vIPI Brindisi, tutte e dieci le tabelle |
| 73 | `FIX \| FL \| NOTE` | Roma |
| 48 | `FIX\|FL\|NOTE ‖ FIX\|FL\|NOTE` (due tabelle **affiancate** in una sola) | Roma |
| 41 | `COP \| AUTORIZZAZIONI \| FL` | briefing CS0 |
| 32 | `FL \| DEST` | Roma |
| 32 | `FL \| DEST \| DEP` | Roma |
| 24 | `FIX \| FL \| NEXT \| COND` | Roma |
| — | `COP(J) \| FL \| DEST \| NEXT`, `FIX \| FL \| RWY \| NEXT`, `COP \| RWY 05 \| RWY 23` | Roma |

**Trentatré forme non si scrivono in codice, si mappano.** È il motivo per cui la rimappatura a mano di
`ImportaTabella` — che c'è già — è il pezzo che regge tutto questo lavoro: una forma nuova costa una
tendina, non una `SpecImport`.

### La misura che decide la fetta A1

Le **494 celle FL** dei tre documenti, passate alla grammatica di oggi (`LevelFormatting.Parse`):

| esito | celle |
|---|---|
| livello vero (`FL210`, `250+`, `140`) | 272 |
| sola parità (`Pari`, `Dispari`) | 52 |
| **testo libero** (`Special`) | **170** |

⚠️ Misura ottenuta **replicando** la grammatica di `Parse`, non eseguendola: il primo passo della fetta A1
è un test che la riconferma sul codice vero. Le famiglie dentro quelle 170:

```
 72  'FL130 o' · 'FL 130 o' · 'FL90 o -'         → «o inferiore» come lo scrivono i documenti
 29  'TO COO' · 'COO' · 'Da Coordinare'          → testo libero legittimo, da uniformare
 20  'PARI340' · 'FL210- Dispari'                → parità fuori dalle parentesi (+ a-capo in cella)
 11  'FL100 o RFL'                                → testo libero legittimo
 10  '140*' · 'FL130 o - **'                      → marcatori di nota attaccati al livello
  6  '210 (BIKTU)' · '170 (TO COO CON LICT_APP)'  → livello più un inciso
```

Tre regole di normalizzazione (**o inferiore** · **marcatori** · **parità ovunque**) ne recuperano ~100:
da **324/494 (66%)** a **~430/494 (87%)**. Le altre colonne, misurate allo stesso modo:

- **ENTE / NEXT / Settore** — 235 celle, 90 distinte: `ES`, `TW1`, `NE` (codici), `LDZO_CTR` (callsign),
  `Roma Radar` (nome), `LGGG_W/LGGG` e `US/TS/NE/US0` (liste), `ES (EU abv FL325)` ×24 (codice + inciso).
- **DEST / DEP** — 218 celle, 48 distinte, di cui ~60 in **forma compressa**: `LIRN/RI` ×16, `LICC/CB/CZ`,
  `LIBR/G/N`, `LIRF/A/U/E`.
- **CONDIZIONI / NOTE** — 135 celle, 66 distinte: la famiglia `DEST./ARR./DEP. XXXX`, poi prosa
  (`Verso Nord`, `ARR LIRF 34`, richiami a paragrafi).

## Pre-flight — le 4 domande

**1. Modello.** Nessun modello nuovo per gli accordi: si scrive in `CoordinationAgreement` /
`AgreementSection` / `AgreementClause` così come sono, e **nella parte A non c'è nessuna migrazione**. La
parte B aggiunge un catalogo (`AirportStar`) che è il gemello dichiarato di `AirportSid`: perché sia un
catalogo e non un flag sta in §B2.
⚠️ Nessun **secondo** meccanismo d'incolla: `ClausePaste` e il suo `PasteForm` **spariscono** dentro
`ImportaTabella` (fetta A6). Due caselle che fanno la stessa cosa erano già il difetto che la carta
precedente ha chiuso una volta.

**2. Dispatch.** Nessuno `switch` nuovo per-tabella: le colonne portano il loro `TipoCella` e la catena non
sa che tabella sta leggendo. Si aggiungono **due tipi di cella** (`Settore`, e in parte B `Procedura`), che
è esattamente il punto d'estensione previsto — nessun `switch` esistente da toccare.

**3. Ingressi + verifica.** L'ingresso di oggi sta in fondo a **quattro clic non ovvi**: ACC dalla barra
(`.xt-bar button`) → controparte, che nasce chiusa (`.xt-nav-sec`) → accordo (`.xt-nav-flow`) → sezione, la
cui levetta è `.xt-dirtoggle` e il cui corpo esiste solo da aperta. Ma una tabella vera **non sta dentro
una sezione**: ne attraversa parecchie. Quindi ingresso nuovo **a livello di controparte**, raggiungibile in
un clic dalla barra ACC. Verifica: si guidano i tre documenti veri (§Verifica), non solo i test.

**4. Propagazione.** La fetta A6 **rimuove** `ClausePaste`, `PastedClause`, `PasteForm` e le chiavi
`Xfer_Paste*`: nello stesso giro vanno aggiornati i test (`AgreementFillingTests`), i commenti che li
citano, `docs/design/piano-import-tabelle.md` (dove `ClausePaste` è descritto come spec viva) e le memorie.

## Le decisioni del committente (3 settembre 2026)

1. **La `ROTTA ATS` si ignora.** `FRA`, `FRA/Z924`, `SID`, `L995/Q772` non entrano da nessuna parte. Con lei
   sparisce l'unico caso «dato senza casa», e la mappatura resta pulita.
2. **La lista degli enti si riduce al primo.** `US/TS/NE/US0` → l'accordo è con `US`; il resto **non si
   scrive da nessuna parte**. Resta visibile nell'**anteprima**, perché dire che cosa si è letto non è
   scriverlo.
   ⚠️ Non è una perdita: la catena di ripiego fra enti è già un dato del sistema — la gerarchia di
   copertura, che la vista live risolve da sé. Copiarla dentro una clausola sarebbe una seconda verità.
3. **L'import propone, non crea.** L'albero del piano nasce **tutto non spuntato**: nessun accordo, nessuna
   sezione, nessuna clausola nasce senza un clic. Vale anche per l'accordo che «ovviamente» manca.
4. **Niente `.docx`.** Si copia **una tabella per volta** dalla clipboard: il `text/html` che Word ci mette
   dentro è già la tabella vera, e il rumore del resto del documento non entra invece di dover essere
   filtrato. (Era già fuori perimetro nella carta precedente, per la stessa ragione.)
5. **Le procedure si agganciano** — `EKMUR 3C` legato alla STAR invece che copiato. È la **parte B**.

---

# Parte A — l'import (nessuna migrazione)

## A1. Che cosa dice una riga vera, e dove va

| colonna reale | esempi | destinazione |
|---|---|---|
| `COP` / `FIX` | `EKMUR`, `Y01-Y12`, `ALL`, `GATE 1/2` | `AgreementClause.Cops` (elenco, `CopList`) |
| `FL` | `FL210- Dispari`, `250+`, `FL130 o -` | livello + vincolo + parità, via normalizzatore → `LevelFormatting.Parse` |
| `ENTE` / `NEXT` / `Settore` | `ES`, `TW1`, `LDZO_CTR`, `US/TS/NE/US0` | **quale accordo**: è il lato opposto. Primo ente, gli altri via (decisione 2) |
| `CONDIZIONI` con prefisso | `DEST. LIBD`, `ARR. LIBR`, `DEP. LIBD` | `Kind` + `Airports` **della sezione** |
| `CONDIZIONI` senza prefisso | `Verso Nord`, `DA ROMA ES` | `ConditionCustomLabel` |
| `CONDIZIONI` vuota | | `Kind = Overflight` |
| `DEST` / `DEP` | `LIRN/RI/RM`, `LIRF/A/U/E` | `Airports` della sezione (gruppo espanso) + `Kind` |
| `RWY` | `16L/R`, `ALL RWYs` | `ConditionLabel` (+ `ConditionRefId` se è **una** pista risolta) |
| `NOTE` | `ARR LFMN LFMD LFMF` | `ConditionCustomLabel` |
| note a piè (`*`, `**`) | `ATC discretion`, `FL150 quando attiva la R310` | legenda a parte → appesa alla condizione delle **sole righe marcate** |
| `AUTORIZZAZIONI` | `EKMUR 3C**` | parte A: testo dentro `Cops`. Parte B: **aggancio** alla procedura |
| `ROTTA ATS` | `FRA/Z924` | ignorata (decisione 1) |

⚠️ **Quel che non ha casa non sparisce**: finisce in `ConditionCustomLabel` o nella `Description` della
sezione, e l'anteprima lo mostra. L'unica eccezione è la `ROTTA ATS`, che è una decisione dichiarata.

## A2. Il salto vero: la riga porta cose che stanno SOPRA la riga

Il modello è `accordo (una coppia di enti) → sezione (tipo · verso · aeroporti) → clausole`. Ma in una
tabella vera **ogni riga** porta ente, DEST/DEP e tipo: dati che nel modello vivono sulla sezione e
sull'accordo. Una tabella di Roma da dodici righe è **tre accordi e cinque sezioni**, non «dodici clausole
in una sezione».

Perciò l'incolla di oggi — dentro una sezione, con il ricevente già fissato — copre solo il caso più
povero, e la novità di questa carta è **una sola**: un import che **si apre più in alto** e produce un
**piano**.

```
righe lette  →  gruppi (ente · tipo · verso · aeroporti)  →  albero da spuntare  →  scrittura
                 ↑ una riga sta in un gruppo solo            ↑ tutto spento all'inizio
```

Il verso non si indovina dal titolo del paragrafo: lo **propone** `SectionDirection.Propose` dagli
aeroporti — che è già il modo in cui nasce una sezione a mano — e resta ribaltabile per gruppo col `⇄` che
c'è già.

Gli attrezzi per scrivere ci sono tutti: `FindByPairAsync`, `AddAgreementAsync`, `AddSectionAsync`,
`AddClauseAsync`. Questa carta non ne aggiunge.

## A3. Le fette

Ognuna si chiude da sola, con la sua verifica.

| # | fetta | che cosa consegna |
|---|---|---|
| **A1** | `LetturaTrasferimenti` (puro) | livello tollerante · prefissi `DEST./ARR./DEP.` · espansione `LIRN/RI/RM` · lista enti · marcatori di nota. Test-first, zero UI. Baseline attesa: 324/494 → ~430/494 |
| **A2** | `TipoCella.Settore` | risolutore sul catalogo settori: codice, callsign o nome; ambiguo → candidati; sconosciuto → riga fuori col perché |
| **A3** | `Griglia`, quattro comandi | riempi-in-giù (celle unite di Word) · taglio della tabella **affiancata** (48 righe in Roma) · unpivot delle colonne-pista (`COP \| RWY 05 \| RWY 23`) · **a-capo dentro cella conservato** |
| **A4** | `SpecTabelle.TrasferimentiDocumento` | condizioni · punti · autorizzazione · ente · aeroporto · pista · livello · note. **Tutte opzionali**: la forma mista è una mappatura, non una spec nuova |
| **A5** | `PianoImportTrasferimenti` (puro) | righe → gruppi → per ciascuno: accordo esistente (`FindByPairAsync`) o **proposto**, sezione esistente o **proposta**, N clausole. Ogni riga fuori porta il suo motivo |
| **A6** | un solo incolla | `ImportaTabella` sostituisce `PasteForm`; `ClausePaste` diventa una spec di quello (propagazione, pre-flight §4) |
| **A7** | l'albero del piano | ingresso a livello di controparte, tutto non spuntato, e un tasto che dice i numeri **prima** di scrivere («N clausole in M sezioni, 2 accordi nuovi») |
| **A8** | legenda note a piè | seconda casella; i marcatori si staccano dalle celle e si legano alle righe |
| **A9** | verifica live | i tre documenti veri, guidati (§Verifica) |

⚠️ **A3 tocca `Griglia`, che è di tutti.** L'a-capo dentro una cella oggi diventa **uno spazio**
(`TabellaHtml.Testo`), e `PARI⏎340` si perde. Cambiarlo per tutti muoverebbe l'import degli alternati e
delle tabelle militari: il break si conserva **su richiesta della spec**, e chi non lo chiede legge come
prima. Prezzo dichiarato: una `Griglia` con due modi.

## A4. Le regole che reggono (e perché)

- **Prima del tasto non si scrive niente.** L'anteprima non è un passaggio da saltare — qui vale il doppio,
  perché una tabella d'IPI produce **accordi**, non righe.
- **Non si crea niente sui cataloghi.** Un ente o uno scalo sconosciuto tiene la riga fuori e si segnala:
  l'import di *un* documento non aggiunge righe a un'anagrafica che è di *tutti*.
- **Il valore vince dal catalogo**: il nome dello scalo lo mette l'archivio, non la cella incollata.
- **Un codice ambiguo chiede quale**, e i candidati portano la loro identità — sceglierne uno basta a
  scriverlo.
- **La virgola non separa le colonne** (`Griglia.Leggi(t, virgola: false)`): separa già i punti dentro la
  cella.
- **I gruppi si propongono, non si impongono** (decisione 3).
- **Una tabella per volta**: chi incolla ha già scelto quale, ed è quello il filtro del rumore
  (decisione 4).

---

# Parte B — le procedure agganciate (dal 16 settembre 2026)

## B0. ⚠️ Perché aspetta

Siamo dentro la **finestra cieca 31 agosto → 16 settembre 2026**: il database di produzione **non si
consegna** e **nessuno può ripristinarlo**. Lo schema non è congelato — `Database.Migrate()` gira
all'avvio, le migrazioni additive si spediscono, e la rete `MigrazioniDellaFinestraCiecaTests` permette
proprio le `CreateTable` con default veri — ma **una tabella nuova più tre colonne su una tabella viva,
dentro una finestra senza ripristino, non è il rischio giusto** per un guadagno che può aspettare tredici
giorni.

**La parte A non è bloccata, e ad aspettare non si perde niente**: l'aggancio è un confronto
**testo → catalogo**, e può girare **dopo** sulle clausole già importate, con la stessa anteprima da
approvare (fetta B5). Importare oggi `EKMUR 3C` come testo non preclude l'aggancio di domani.

## B1. Il problema, misurato

`EKMUR 3C` oggi è **testo congelato**. Quando l'AIRAC la porta a `EKMUR 4C` il documento continua a dire
`3C` — e non lo dice a nessuno. Le tabelle che citano designator di procedura sono **41 righe su ~450** (i
due briefing `COP | AUTORIZZAZIONI | FL`): il guadagno immediato è piccolo, e va detto. Quello vero è la
**manutenzione**, e cresce con ogni vIPI d'arrivo che verrà scritta.

## B2. Le STAR non esistono, e la sorgente ce le ha

`AirportSid` c'è: **1.269 procedure da 56 file `.sid`**, con importer, gate AIRAC, merge che preserva le
righe manuali, lock per-ICAO, editor ed esposizione nel documento d'aeroporto. Di **STAR** nel modello ci
sono **zero occorrenze**.

La sorgente le ha (`STATO_SECTORFILE_ITALIANO.md` §5 e §6.1): file **`.str`, 90 file / 1.511 righe / 89
aeroporti coperti**, stessa intestazione dei `.sid`.

⚠️ **Con una trappola documentata: 339 di quelle 1.511 non sono STAR.** Sono voci dell'hack `MAPS` — le
shape di CTR e ATZ vivono dentro i file «STAR» — e si riconoscono dal campo pista, che vale `MAPS` o
`MAPS:07`. Il filtro va dove si legge il file, non a valle.

**Perché un catalogo e non un flag su `AirportSid`** (pre-flight §1): SID e STAR hanno lo stesso *formato*
ma non sono la stessa cosa — una parte da un fix e sale, l'altra arriva a un fix e scende; i consumatori
sono diversi (partenze vs arrivi); e un flag su un'entità che si chiama `Sid` sarebbe un nome che mente. Si
**condivide il codice** (`SplitDesignator`, `ResolveFix`, la `StableKey`, il merge, il gate del ciclo), non
la tabella.

**Costo onesto**: il verticale SID è **~25 file veri** più i test — entità, repo, provider, parser,
importer, derivazione, due componenti, editor, viewer, DI, policy d'import. Un catalogo STAR è *quello*,
non una colonna. In cambio le STAR mancano anche ai **documenti d'aeroporto**, dove le SID si vedono: il
catalogo si ripaga lì, e l'aggancio diventa la colonna in più su un catalogo che avresti comunque.

⚠️ **Primo passo, prima di scrivere il parser**: scaricare tre `.str` veri e **contare**. La guardia del
parser SID è `c.Length < 3`, e sotto ogni intestazione ci sono le righe di **tracciato** (coordinate): che
vengano già saltate è da **misurare**, non da presumere.

## B3. Come si aggancia

**Per `StableKey`, mai per Id.** `AirportSid.StableKey` = `ICAO|fix|lettera|transition|pista`, **esclusa la
cifra di revisione**, ed è nata esattamente per sopravvivere ai reimport: se `EKMUR 3C` diventa `EKMUR 4C`
la chiave non si muove e il coordinamento segue. La strada per Id è già stata pagata una volta — le
clausole `#38`/`#39` puntano a piste re-importate con altri Id (`ConditionRefId` 215/216).

⚠️ **La `StableKey` non è unica per aeroporto, e non va resa tale**: un `.sid` può contenere due revisioni
della stessa procedura (`ROBO1H` e `ROBO2H`) — 20 coppie su 1478 righe sul DB di sviluppo — e l'indice
unico, provato una volta, **fa fallire la migrazione su dati veri**. Quindi il legame risolve a un
**insieme**, e serve una regola di scelta **scritta**: revisione più alta, a parità `Priority`. Mai un
`Single()`.

**Dove si scrive**: tre colonne nullable su `AgreementClause` —

```
ProcedureKind   Sid | Star | (null)
ProcedureIcao   l'aeroporto della procedura
ProcedureKey    la StableKey
```

`Cops` resta il **testo scritto** e fa da ripiego; quando il link risolve, **vince il link**.

⚠️ Una clausola aggancia **una** procedura: l'import fa quindi **una clausola per procedura**, e non prova
a metterne due in un elenco di `Cops`.

## B4. Che cosa vuol dire davvero «si aggiorna da solo»

Quattro cose, e tre sono trappole già pagate altrove:

1. **L'editor e le viste derivate seguono l'AIRAC subito.**
2. ⚠️ **Il documento pubblicato no.** Le release sono fotografie: `Cops` è una stringa già scritta dentro lo
   snapshot, e cambia **alla prossima release**. È esattamente ciò che è successo con la prosa dei
   coordinamenti (33 tabelle nel pubblico, 39 nell'editor), e va detto a chi si aspetta di vedere il
   cambiamento comparire da sé.
3. ⚠️ **Perciò il pezzo utile non è il link: è la segnalazione.** «Questa procedura è cambiata, la citano N
   clausole in questi documenti» va nel read-model degli **impatti**, che esiste già. Senza, l'aggiornamento
   è invisibile, non lo ripubblica nessuno, e il meccanismo vale zero.
4. ⚠️ **Il link si risolve al ciclo che si sta rendendo**, non a `UtcNow`: l'anteprima di una release al
   2610 deve vedere le procedure del 2610. La tubatura c'è già ed è parametrica
   (`DeriveAsync(icao, atCycle)`); usarla è obbligatorio, non un di più.

E il caso brutto — **procedura sparita** (ritirata, o fix rinominato): la clausola tiene il testo congelato
e si accende in lista impatti. **Mai una cella vuota**: un dato che sparisce senza dirlo è peggio di un
dato vecchio.

## B5. Le fette della parte B

| # | fetta | che cosa consegna |
|---|---|---|
| **B0** | misura sul `.str` vero | tre file scaricati e contati: quante righe, quante `MAPS`, come sono fatte le righe di tracciato. **Prima** del parser |
| **B1** | catalogo STAR | `AirportStar` + `ParseStars` (condiviso col `.sid`) + filtro `MAPS` + importer + gate del ciclo + policy d'import |
| **B2** | le STAR nel documento d'aeroporto | dove ci sono già le SID. È qui che il catalogo si ripaga |
| **B3** | il legame | tre colonne nullable, risoluzione al ciclo, regola di scelta sulla chiave ripetuta, ripiego sul testo |
| **B4** | `TipoCella.Procedura` nell'import | la colonna `AUTORIZZAZIONI` si aggancia invece di essere copiata; l'anteprima mostra il designator **risolto** |
| **B5** | la passata «aggancia le procedure» | sulle clausole **già scritte**: confronto testo → catalogo, con la stessa anteprima da approvare |
| **B6** | gli impatti | procedura cambiata o sparita → voci nella lista di ciò che va rivisto |

## Fuori perimetro

`.docx` letto dal server · OCR · estrazione PDF lato server · scrittura senza anteprima approvata · la
`ROTTA ATS` · copiare la catena di ripiego degli enti dentro le clausole · un indice unico sulla
`StableKey` · qualunque migrazione distruttiva prima del 16 settembre.

## Verifica

Non basta `dotnet test`: si prova **guidando** (skill `verifica-live`, host sulla **5035** — la 5034 è del
committente — e ogni prova di scrittura su una **copia** del DB nello scratchpad).

1. **vIPI Brindisi, «Traffici in uscita» verso Roma** (10 righe, `US/TS/NE/US0`): deve proporre più accordi,
   uno per primo-ente, e le due sezioni `DEST. LIRN/I` con gli scali espansi.
2. **vIPI Brindisi, tabella Zagabria** (12 righe, otto con `CONDIZIONI` vuota): le prime otto devono
   diventare **sorvoli**, le ultime quattro arrivi a `LIBD`/`LIBR`.
3. **Briefing CS0, `LIBD RWY 07`** (8 righe con `*`, `**`, `***`, `****`): la legenda deve legarsi alle
   righe giuste, e `FL130 o -` diventare un livello vero, non testo.
4. **Roma, `FIX | FL | RWY | NEXT`** (celle unite verticali): riempi-in-giù, poi tre clausole per fix con la
   pista in condizione.
5. **Roma, le due tabelle affiancate**: il taglio, e due piani distinti.
6. **Il piano non spuntato**: premere «importa» senza spuntare niente non deve scrivere **niente**.
7. **Parte B**: cambiare a mano la revisione di una STAR in catalogo e vedere il coordinamento seguirla
   **nell'editor** e **non** nel documento pubblicato — che è il comportamento giusto, e va guardato in
   faccia invece che scoperto dopo.

⚠️ E le due misure da rifare **prima e dopo**: quante delle 494 celle FL si leggono come livello, e quante
delle ~450 righe arrivano a essere clausole. Un import che non si misura non si sa se serve.
