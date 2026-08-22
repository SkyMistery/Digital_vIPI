# Feature — Trasferimenti ACC↔APP: autorizzazione e trasferimento separati, varianti per condizione, velocità

Data: 2026-08-11 · Stato: **CHIUSO — codice, suite 2173 verde, build Release 0 warning su entrambi i TFM,
✅ verifica live eseguita** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Contesto: [refactor 07 — trasferimenti](../refactor/07-trasferimenti.md) (l'area), `docs/spec/modello-dati.md`
§9.20 e **§9.20-bis** (schema autorevole).

> **Scostamenti dalla carta, decisi in esecuzione.** Sono quattro, tutti annotati anche nel punto in cui
> capitano:
> 1. **Una migrazione, non tre.** Le tre migrazioni previste (handoff · velocità · varianti) toccano la stessa
>    tabella nello stesso commit: emetterle separate avrebbe significato tre `ALTER TABLE` su MariaDB e tre
>    snapshot per un cambio atomico. Una sola, `AddTransferHandoffSpeedAndVariants`.
> 2. **Niente `VariantOrder`.** L'ordine è l'`Order` che già esiste. Due ordinamenti sulla stessa collezione
>    possono contraddirsi, e il secondo sarebbe stato l'unico a poter mentire.
> 3. **Un gruppo APP nuovo** (`TowardApps`) nella vIPI APP: allargando il passo 2 è emerso che i coordinamenti
>    con un altro APP non avevano un cesto dove finire e sparivano dal documento — anche quando l'APP ne era
>    proprietario. Vedi §«Derivazione».
> 4. **`TransferPointInput` e `TransferPointRow` diventano `record`.** Con venticinque campi, chi ne varia uno
>    deve poter scrivere `with`.

## Obiettivo

Il modello dei trasferimenti descrive **un evento con un livello**: una riga `TransferPoint` dice *un* CoP,
*un* livello (valore + vincolo + parità + stato verticale) e *un* ricevente. Per un accordo ACC↔ACC basta,
perché i due eventi coincidono: al CoP il traffico è a quel livello e lì passa il controllo.

Un accordo ACC→APP sono **due eventi distinti**, e il modello non ha gli slot per tenerli separati:

| | ACC↔ACC | ACC→APP |
|---|---|---|
| dove entra | CoP | punto/STAR d'ingresso (es. CHI) |
| a che livello è autorizzato | = livello del CoP | FL160 o superiore |
| dove passa il controllo | stesso CoP | confine dell'AoR, o un altro fix |
| a che livello è quando passa | = lo stesso | passando FL110 in discesa |

Frase bersaglio, portata dal committente:

> Padova Military autorizza il traffico con destinazione Aviano LIPA via CHI a FL160 o superiore e lo
> trasferisce ad Aviano Approach al confine dell'AoR passando FL110 in discesa, a 250 kt o inferiore.

Oggi **non è esprimibile**. Non è un problema di resa: è il modello.

Insieme si chiudono altre tre cose emerse discutendo, che vivono sulla stessa riga e non conviene aprire due volte:

2. **Velocità assente.** Le tabelle ACC/APP reali portano quasi sempre una restrizione di velocità al
   trasferimento. Nel modello non esiste alcun campo.
3. **Varianti scollegate.** «FL80 con pista 16R, FL110 altrimenti» oggi sono due righe indipendenti: nessun
   legame, nessuna precompilazione, e il lettore non capisce che sono alternative dello stesso accordo. Il
   problema **peggiora** con i campi nuovi: oggi duplicare una riga costa 4 campi, dopo ne costa ~10 per
   cambiarne uno.
4. **La sezione estesa non mostra tutto.** `CoordinationDerivation.Build` passo 2 accetta solo
   `Kind == Arrival && ownerType == Ctr`: la vIPI di un ACC **non mostra le partenze** che un APP gli consegna.
   Decisione del committente: nella versione estesa deve esserci tutto ciò che entra o esce da un ente.

## Misura sul dato reale

Fatta sulla copia di `src/Vipi.Host/vipi.db` dell'11 agosto (**DB di sviluppo, non la produzione MariaDB**:
i numeri servono per l'ordine di grandezza e per i casi di prova, non per dimensionare la migrazione).

| Misura | Valore |
|---|---|
| Flussi / righe | 36 / 73 |
| Righe con ricevente APP | **16** (tutte `Arrival`) |
| Righe con ricevente CTR | 56 |
| Condizioni compilate | 3 in tutto (2 pista, 1 area, **0 personalizzate**) |
| Gruppi di varianti impliciti (stesso flusso + CoP + ricevente) | **1**, di 2 righe |
| Flussi posseduti da un APP (oggi invisibili all'ACC) | 3 (2 arrivi + 1 sorvolo) |

Due cose che la misura ha detto e che vanno usate:

- **Un gruppo di varianti implicito c'è già.** Righe 76 e 77, flusso 45, arrivi LIBD da `LIBB_ES_CTR` verso
  `LIBD_CS0_APP`: `BIRSU FL150 stabile con pista 07` e `BIRSU FL130 in discesa con pista 25 e LI R403B
  attiva`. Sono la stessa riga con condizioni diverse, ma il modello non ha modo di dirlo: appaiono come due
  accordi separati che si ripetono. È il caso di prova naturale per il gruppo varianti.

  > ⚠️ **Correzione (verifica live dell'11 agosto).** Questa scheda ha sostenuto per qualche ora che quelle
  > due righe fossero **senza condizione**, e che quindi il lettore non sapesse quale applicare. Era falso:
  > l'avevo dedotto da un conteggio aggregato senza guardare le colonne della riga, e le due condizioni del
  > DB sono proprio le loro. Il caso resta valido come prova del **legame** fra varianti; non lo è mai stato
  > come prova di un'ambiguità.

- **La condizione personalizzata non è mai stata usata** (0 righe su 73), pur essendo il campo che serve alle
  esigenze APP («condizionato dal traffico di un altro aeroporto»). Il campo c'è; è l'ergonomia che manca, ed
  è coerente con il fatto che le esigenze APP finora non erano esprimibili.

## Decisioni del committente

Prese nella discussione dell'11 agosto, prima di scrivere codice:

1. **Trasferimento del controllo e delle comunicazioni = due colonne distinte.**
2. **Fraseologia del livello al trasferimento: «passando FL110»** è la forma di riferimento.
3. **Il punto di trasferimento può essere: confine dell'AoR, un fix, o testo libero.**
4. **La condizione resta pista / aree attive / personalizzata** (le tre dimensioni di oggi restano e valgono
   anche per le varianti).
5. **APP→TWR resta com'è.** Il modello a un evento è sufficiente per quel salto.
6. **Le righe esistenti si rivedono a mano**, con un filtro nell'editor che le elenchi.
7. **Velocità: sì**, forma `valore + ≤ / ≥ / =`. Se non serve resta vuota.
8. **Flag «salvo diverso coordinamento»: no.** È una frase che si scrive una volta sola nella prosa del
   documento, non un attributo di riga.
9. **Varianti: opzione A** (gruppo come chiave sulla riga) **più la riga «altrimenti»**.

## Pre-flight — 4 domande

**1. Modello — «aggiungo un concetto o ne esiste già uno?»**

Nessuna entità nuova, nessun modello gemello. Tutto sta su `TransferPoint`, che è già il posto unico dove
vive una riga di coordinamento: si **estende**. In particolare **non** si crea la tabella figlia
`TransferPointVariant` (era l'opzione B, valutata e scartata: vedi «Alternativa scartata»).

I campi esistenti non cambiano nome né tipo; cambia — e va scritta nei commenti e nella spec — la loro
**semantica esplicita**: `Cop` è il punto/rotta d'**ingresso**, e il blocco `Level*` è il livello
**autorizzato**. Finora erano entrambe le cose perché i due eventi coincidevano.

**2. Dispatch — «sto per switchare su un tipo che switcho già altrove?»**

Il nuovo `TransferHandoffKind` si consuma in **un solo** posto reale, il composer della frase
(`CoordinationSentenceComposer`), che risolve la clausola da template. Le viste non switchano sul tipo: le
colonne compaiono **per presenza di dati**, come già fa `hasCond` in `AppCoordinationView.razor:99`. Niente
registry: sarebbe over-engineering su uno switch solo.

⚠️ Il punto dove la Regola del 2 va sorvegliata è un altro: la scelta «tabella ACC↔ACC / tabella ACC→APP»
**non deve** diventare uno switch su `SectorType.App`. Sarebbe un ramo per-tipo replicato in ACC, APP, vLOA e
stampa. La regola è una sola e vale ovunque: *una colonna si mostra se almeno una riga la compila.*

**3. Ingressi + verifica**

Ingressi già esistenti: `/services/vsop/admin/transfers` per l'editor, le sezioni Coordinamenti di vIPI ACC / vIPI
APP / vLOA per la lettura. Nessun catch-22: non nasce un tipo nuovo da raggiungere, si estendono righe che
l'utente già crea. L'unico ingresso nuovo è il **filtro «da rivedere»** dentro la pagina che c'è già.

Verifica: skill `/verifica-live` su copia del DB, ricostruendo l'accordo Padova→Aviano dell'esempio e
leggendolo in vIPI ACC, vIPI APP, vLOA (inglese) e stampa. Più il caso reale BIRSU come regressione.

**4. Propagazione — «questa modifica rimuove o rinomina qualcosa?»**

Non rimuove né rinomina: è additiva. Ma **allarga il passo 2 della derivazione**, che è un cambio di
comportamento su codice condiviso da ACC, APP e vLOA. La lista completa sta in «Propagazione».

> ⚠️ **La parte «varianti» di questa scheda è superata dal giorno dopo.** Alla prima lettura il committente
> ha rovesciato tre cose: lo standard va in **testa** e non in fondo, le alternative sono **pari-grado** (pista
> 07 e pista 25, nessuna subordinata all'altra) e i livelli possono essere **più di due**. `IsOtherwise` è
> stato sostituito da `VariantDepth` + `IsGroupWide` prima del merge, quindi a costo di dati zero. Dove qui si
> legge «negli altri casi» e «capofila + subordinate», vale
> [`2026-08-12-varianti-a-livelli.md`](2026-08-12-varianti-a-livelli.md) e `modello-dati.md` §9.20-ter.
> Il resto della scheda — faccetta trasferimento, velocità, derivazione estesa, tabella condivisa — è invariato.

## Modello

Tutti i campi nuovi sono **opzionali**. Riga con tutti i campi nuovi a zero/null ⇒ comportamento identico a
oggi, frase identica a oggi. È l'invariante che rende sicura la migrazione delle 73 righe esistenti.

### Faccetta trasferimento (controllo)

```
TransferHandoffKind { Unspecified = 0, Point, AorBoundary, Custom }

HandoffKind            TransferHandoffKind   // 0 = il trasferimento coincide con l'ingresso (comportamento attuale)
HandoffLabel           string?               // il fix, o il testo libero; vuoto per AorBoundary
HandoffLevelValue      int?
HandoffLevelUnit       LevelUnit             // riuso dell'enum esistente
HandoffLevelConstraint LevelConstraint       // riuso; Exact ⇒ «passando FL110» (forma di riferimento, decisione 2)
```

Lo **stato verticale** al trasferimento («in discesa») è già `TransferPoint.VerticalState` e resta lì: descrive
il transito, che è esattamente il momento del trasferimento. Non si duplica.

`HandoffKind = Unspecified` è lo zero dell'enum **e** significa «come prima»: è la ragione per cui qui l'enum è
sicuro dove un bool non lo sarebbe. Il rischio noto è il flag *opt-out* che nasce `false`
ovunque — migrazione **e** reconciler Postgres, vedi la coda di
[aree regolamentate hardening](2026-08-03-aree-regolamentate-hardening.md) e il caso `ImportSids`. Qui lo zero è
il valore corretto per tutte le righe preesistenti, quindi il rischio non si presenta.

### Faccetta trasferimento (comunicazioni) — decisione 1

```
CommsHandoffKind   TransferHandoffKind   // stesso enum
CommsHandoffLabel  string?
```

Vuoto ⇒ le comunicazioni passano dove passa il controllo (nessuna clausola nella frase, nessuna colonna).

### Velocità — decisione 7

```
SpeedConstraint { Unspecified = 0, AtOrBelow, AtOrAbove, Exact }

SpeedValue       int?              // nodi IAS, unità implicita
SpeedConstraint  SpeedConstraint   // 0 = nessuna restrizione
```

Enum **dedicato**, non il riuso di `LevelConstraint`: quello porta un valore `Special` («per aerovia») che per
una velocità non vuol dire niente, e un tipo che si chiama `Level` su un campo di velocità mente a chi legge.

### Varianti — decisione 9

```
VariantGroup  int?    // null = riga singola; intero progressivo per flusso
VariantOrder  int
IsOtherwise   bool    // la riga «negli altri casi»; false per tutte le righe esistenti (corretto)
```

I dati restano **piatti e completi**: una variante porta l'intero payload, non un delta. Niente ereditarietà
di campo, che introdurrebbe l'ambiguità «null = eredita» contro «null = non specificato» — ambiguità reale,
perché `LevelValue` è già nullable con un significato suo. L'eredità sta **nell'editor** (la variante nasce
copiata), il delta sta **nel rendering** (si scrive solo ciò che cambia), il dato resta piatto e sopravvive
agli snapshot pubblicati.

Vincoli, validati nel service Application con `Vipi.Application.*.ValidationException` (mai DataAnnotations:
la UI non le cattura → crash del circuito):

- righe con lo stesso `VariantGroup` stanno nello stesso flusso e condividono `Cop` e `NextSectorId`;
- **al più una** riga `IsOtherwise` per gruppo;
- una riga `IsOtherwise` non porta condizione (le tre colonne condizione devono essere vuote): è il complemento
  delle altre, non una condizione in più;
- `IsOtherwise` implica `VariantGroup` non nullo.

Controllo **non bloccante** nell'editor: gruppo di varianti senza riga «altrimenti» ⇒ avviso. Non è un errore
(un gruppo può coprire esaustivamente i casi), ma è il buco che oggi lascia il lettore senza istruzioni — vedi
il caso BIRSU misurato sopra.

### Migrazioni

Tre, separate e additive, ognuna col suo commit: `AddTransferPointHandoff`, `AddTransferPointSpeed`,
`AddTransferPointVariantGroup`. Nessun backfill: gli zeri sono già i valori giusti.

⚠️ Da emettere **due volte** (SQLite di sviluppo e MariaDB di produzione) e da provare su una **copia del
`vipi.db` reale** prima del push — vedi [ADR-0007](../adr/adr-0007-produzione-persistenza-e-scala.md) §D4-ter e
la lezione dell'audit del 30 luglio. La catena delle migrazioni va aggiornata in `docs/spec/modello-dati.md` §9.20.

## Frase

Bersaglio, dai pezzi:

> `{owner}` **autorizza** il traffico `{airport}` via `{cop}` `{fl_autorizzato}` e lo **trasferisce** a
> `{target}` `{handoff}` `{fl_trasferimento}` `{stato}`, `{velocità}`, `{comunicazioni}`. `{condizione}`

Meccanica: le clausole nuove si **appendono** come già fa `AppendCondition`, non via placeholder, così i
template personalizzati caricati da `content/coordination-sentence.json` — che quei placeholder non hanno —
continuano a funzionare. L'unica eccezione è il **verbo iniziale**: «autorizza … e lo trasferisce» non è una
coda, è una forma diversa del template. Serve una chiave `templateCleared` accanto a `template`, scelta quando
`HandoffKind != Unspecified`; se un file personalizzato non la porta, si ricade sul `template` classico più la
coda. Nessun file esistente si rompe.

Chiavi nuove nel template, **in italiano e in inglese** (la vLOA usa `CoordinationSentenceTemplate.English`):
`templateCleared`, `handoff` (`point` / `aorBoundary` / `custom`), `handoffLevel` (`passing` / `at` / `orBelow`
/ `orAbove`), `speed` (`atOrBelow` / `atOrAbove` / `exact`), `comms`, `variant.otherwise`.

⚠️ L'ordine delle parole appartiene al **template**, non al codice del composer. È già costato una correzione
(la parità in inglese resa con l'ordine italiano, «at level 260 even»): la regola vale identica per «passando
FL110» / «passing FL110» e per la velocità.

## Viste

Tabella dei coordinamenti, colonne **per presenza di dati**:

`Via / CoP · Autorizzato · Trasferimento · Liv. trasferimento · Comunicazioni · Velocità · Ricevente · Condizione`

Su un blocco ACC↔ACC che non compila nulla di nuovo, la tabella resta **identica a oggi**.

Gruppo di varianti: riga capogruppo con le celle comuni scritte una volta (`rowspan`), varianti rientrate sotto
con **solo il delta** più la loro condizione. La riga `IsOtherwise` è sempre l'ultima del gruppo,
indipendentemente da `VariantOrder`, con «negli altri casi» al posto della condizione.

CSS: le larghezze stanno per **classe semantica** (`vipi-theme.css:385-387`, `.coord-table th.c-*`) proprio
perché le colonne opzionali cadono in posizioni diverse da una tabella all'altra. Le classi nuove vanno
**misurate** sulla stampa reale, non indovinate — vedi
[stampa dei documenti](2026-07-30-stampa-documenti.md), che quelle larghezze le ha già dovute rifare una volta.

## Derivazione — la sezione estesa mostra tutto (decisione del committente)

`CoordinationDerivation.Build` (`CoordinationDerivation.cs:78-99`), passo 2 «entranti»: il filtro
`flow.Kind != Arrival` e `ownerType != Ctr` cade. Diventa: *qualunque flusso non posseduto dal blocco il cui
`next` è un settore del blocco*.

Tre conseguenze da guardare **prima** di stimare, non durante:

1. `CoordinationEntry.CounterpartType` può ora essere `App`/`Twr` anche fra gli entranti. `BuildAccTree`
   raggruppa per `AccOf(e)` = l'ACC del counterpart: per un counterpart APP quell'ACC va verificato che sia
   quello giusto e non «ACC» di fallback.
2. Il bucketing dell'APP («verso ACC» / «verso torri» / sorvoli) assume oggi che l'entrante sia un CTR.
   Con il filtro allargato entra anche altro.
3. **Doppioni**: se ACC e APP hanno entrambi scritto la propria riga per lo stesso accordo, ora compaiono
   tutte e due. Raccomandazione: **non** deduplicare — sono due dichiarazioni distinte, e nasconderne una
   nasconderebbe anche una divergenza — ma **segnalarle** nel report «da rivedere». Da confermare guardando
   il risultato reale, non a tavolino.

## Report «righe da rivedere» (decisione 6)

Filtro nell'editor `/services/vsop/admin/transfers`, accanto a quelli che ci sono già (testo, tipo): righe con
ricevente di tipo `App` e `HandoffKind == Unspecified`, con il conteggio in testata. Sul DB di sviluppo sono
**16**; sulla produzione va rimisurato. È l'unico modo perché la revisione manuale delle righe esistenti si
possa chiudere invece di disperdersi.

In coda, il **preset di riga ACC→APP** (idea 5): quando il ricevente scelto è un APP, la riga nuova nasce già
con la faccetta trasferimento aperta invece che vuota.

## Propagazione — dove il campo nuovo deve arrivare, o si rompe in silenzio

| Punto | Cosa cambia | Rischio |
|---|---|---|
| `CoordinationSentenceComposer` | clausole nuove + `templateCleared` | — |
| `content/coordination-sentence.json` | chiavi IT **e** gemelle inglesi | la vLOA è già stata dimenticata una volta |
| `AccCoordinationView` / `AppCoordinationView` | colonne per presenza dati, gruppi varianti | — |
| `VloaDerivationService` | verifica esplicita, non per analogia | **passò già a vuoto** (manca `flow.Kind`) |
| `vipi-theme.css` + `vipi-print.css` | classi `c-*` nuove, larghezze misurate | stampa a 8 colonne |
| `TransfersLive.razor:54` | mostra un solo `LevelText` | **da decidere**: raccomando il livello di trasferimento |
| `TransferMatcher` | la graduatoria pesa il livello (`ParityOk`, ecc.) su «un punto, un livello» | **il punto meno chiaro**: da leggere prima di stimare |
| `TransferResolveContract.CandidateLevel` | quale livello finisce nell'etichetta quota di Aurora | contratto verso un tool esterno |
| snapshot / sezioni congelate | restano com'erano, giustamente | da dire, non da fare |
| `docs/spec/modello-dati.md` §9.20, `docs/refactor/07-trasferimenti.md`, `history/rounds.md`, memorie | schema e semantica nuovi | DoD |

## Slice di esecuzione

Una per commit, `dotnet build` verde a ogni passo, storia bisezionabile.

1. **Schema.** Tre migrazioni + `TransferPointInput`/`TransferPointRow`/repo + test di repository. Nessun
   cambio visibile: la suite deve restare verde senza toccare un solo test esistente.
2. **Composer.** Prima i **test di caratterizzazione** sulle frasi attuali (bloccano la regressione), poi le
   clausole nuove e il template IT/EN.
3. **Editor.** Campi nuovi, «+ variante» con precompilazione, «altrimenti», validazioni e avviso non bloccante.
4. **Viste** ACC / APP / vLOA + CSS misurato su schermo e stampa.
5. **Derivazione** passo 2 allargato + test, con i tre punti di sopra risolti.
6. **Report «da rivedere»** + preset di riga ACC→APP.
7. **Propagazione**: decisione su live/matcher/bridge, doc, spec, `rounds.md`, memorie.

## Test

- `CoordinationSentenceComposer`: caratterizzazione **prima**, poi i casi nuovi (autorizzato+trasferimento,
  comunicazioni separate, velocità, «altrimenti», e le gemelle inglesi).
- `CoordinationDerivationTests`: partenze entranti, counterpart APP, doppioni.
- Repository: gruppo varianti, vincolo `Cop`/`next` condiviso, unicità di «altrimenti».
- Regressione dal dato vero: le righe 76/77 (BIRSU) diventano un gruppo di varianti con «altrimenti».

⚠️ Le frasi vanno **lette rese**, non solo asserite. Un test aveva *fotografato* il difetto della parità
inglese invece di impedirlo, ed è la ragione per cui non è mai emerso dalla suite.

## ✅ Verifica live — eseguita l'11 agosto 2026

Skill `/verifica-live`, su copia del `vipi.db` reale (il DB del progetto è rimasto intatto: 73 righe, nessuna
colonna nuova, timestamp invariato). Quattro giri guidati con Edge, con gli screenshot **guardati** e non solo
le asserzioni sul DOM.

**Confermato a schermo:**

1. **L'accordo a due eventi si scrive e si legge.** Sul caso BIRSU: «Brindisi Radar ES **autorizza** il traffico
   con destinazione Bari Palese LIBD via BIRSU a livello 150 o livello inferiore pari **e lo trasferisce** a
   Brindisi Radar CS0 al confine dell'AoR passando FL110 con pista 07 in uso.» Su un accordo estero (LGKR,
   Corfù) con tutte le code: «… al confine dell'AoR passando FL80 in discesa, a 250 kt o inferiore,
   comunicazioni su KRK.»
2. **Gruppo di varianti**: CoP e ricevente scritti una volta sola in `rowspan`, le alternative rientrate sotto
   con il solo delta, «negli altri casi» in fondo. L'avviso sul gruppo senza «altrimenti» compare e non blocca.
3. **Colonne per presenza di dati**: nella vIPI ACC, 2 tabelle su 35 hanno le colonne nuove; le altre 33 sono
   rimaste a `CoP · Livello · Prossimo`. Nella vLOA, 1 su 9. È l'invariante che protegge i documenti esistenti.
4. **vLOA in inglese**: `VIA · CLEARED · TRANSFER · TRANSFER LEVEL · COMMUNICATIONS · SPEED · NEXT`, e la frase
   «… clears the traffic … and transfers it to Kerkira Approach at the AoR boundary **passing FL80** descending,
   at 250 kt or less, communications over KRK.»
5. **La sezione estesa mostra ciò che entra da un APP**: i 3 flussi di `LYTV_APP` (Tivat) compaiono ora nel
   documento di LIBB — «Tivat Approach trasferisce a Brindisi Radar ES …», sorvolo compreso. Prima non
   comparivano da nessuna parte.
6. **Bucketing APP** (su `LIBN_APP`, con flussi costruiti apposta): i tre gruppi «verso ACC», «verso torri» e
   **«verso altri APP»** — quest'ultimo è il gruppo nuovo, e prima quei coordinamenti sparivano in silenzio.
   La partenza verso la torre, prima scartata, c'è.
7. **Stampa**: misurata a **larghezza carta** (760px, non alla finestra da 1700). La tabella a sei colonne sta
   in 636px, nessuno scorrimento orizzontale, nessuna cella che va a capo.

**Tre difetti trovati proprio qui, invisibili alla suite:**

- **Il vincolo del livello di trasferimento partiva da «o superiore».** Aprendo una riga che la faccetta non
  aveva mai usato, l'editor caricava il valore di riposo della colonna (`AtOrAbove`, lo zero dell'enum) come se
  fosse una scelta di qualcuno: la prima faccetta scritta diceva «al confine dell'AoR **a FL110 o superiore**»
  invece di «passando FL110». Ora il form parte da `Exact` quando la faccetta è vuota.
- **«Negli altri casi» era detto in due lingue nella stessa schermata.** La cella veniva dalle risorse
  dell'interfaccia e la frase dal template: con la UI in inglese si leggeva «in all other cases» nella tabella e
  «negli altri casi» nella riga di prosa due centimetri sopra. Ora la cella arriva dal **template**, come la
  frase — è contenuto, non chrome.
- **Una testata mezza tradotta**: `AccCoordinationView` passava «Prossimo» scritto a mano, e con l'interfaccia in
  inglese usciva `VIA · CLEARED · TRANSFER · TRANSFER LEVEL · **PROSSIMO** · CONDITION`.

Più una misura di CSS: la tinta di fondo delle righe-variante era al 14% di `--line` e **a schermo non si
vedeva**. Portata al 55%, con un filetto sopra la capofila che dice dove comincia il blocco.

## Esito — cosa ha detto l'esecuzione

Sette slice, suite da 2111 a **2173** test verdi, `dotnet build -c Release --no-incremental` **0 warning**.

**Quattro difetti veri trovati strada facendo**, tre dei quali nessun test avrebbe visto:

1. **Lo scaffolding EF proponeva `defaultValue: ""` per cinque colonne enum.** Quegli enum vivono su colonna
   testuale: le 73 righe in archivio sarebbero nate con una stringa vuota e la **prima lettura sarebbe andata
   in eccezione**. Chiuso dichiarando i default nel modello (`HasDefaultValue`), che copre in un colpo la
   migrazione **e** il `PostgresSchemaReconciler` del deploy Render, che ha lo stesso buco per la sua strada.
2. **Una guardia esistente ha fermato il giro**: `IndexedStringLengthTests` ha visto che una stringa con
   DEFAULT su MySQL nasce `longtext`, e `longtext` un default non può averlo. Le cinque colonne sono entrate in
   `MySqlStringLengths.Map`. La guardia era però **cieca a metà**: cercava le colonne solo dentro il
   `CREATE TABLE`, e le nostre arrivano con un `ALTER TABLE ADD`. Estesa — falliva sulla propria ricerca, non
   sul difetto che presidia.
3. **`DissolveIfAloneAsync` interrogava il database prima della `SaveChanges`**, quindi vedeva ancora nel gruppo
   la riga appena sfilata e non lo scioglieva mai. Preso dal test, corretto leggendo lo stato in memoria.
4. **Il mittente perdeva il proprio codice di posizione quando non era un CTR.** Regola scritta quando il
   mittente era sempre un CTR; da quando la sezione estesa mostra ciò che entra da un APP, la frase diventava
   «Roma Radar trasferisce a Roma Radar TS» — due enti diversi, stesso nome. Ora la regola è simmetrica ai due
   lati. **Nessun test esistente si è rotto**, il che dice anche quanto era coperto quel ramo.

**Un rischio della carta si è sgonfiato leggendo il codice.** `TransferMatcher` sembrava il punto più esposto
(«pesa la graduatoria su un punto, un livello»): letto per intero, il livello non entra in **nessun** criterio
di punteggio — il punteggio guarda CoP, aeroporto/tipo, parità, pista e stazione ricevente. Il livello serve
solo a comporre l'etichetta quota, che ora prende quello **al trasferimento** quando c'è. Il lavoro era una
riga, non una revisione.

**Una duplicazione chiusa nello stesso giro.** La tabella dei coordinamenti viveva due volte, quasi identica, in
`AccCoordinationView` e `AppCoordinationView`; con otto colonne condizionali e i gruppi di varianti sarebbero
state due verità da tenere d'accordo a mano. Estratta in `CoordTable.razor`, che le rende entrambe (e la vLOA).
Stessa cosa per la costruzione della riga, ora in `CoordinationDerivation.ToRow`, usata anche dalla vLOA — che
prima se la costruiva a mano ed è già stata dimenticata una volta.

## Alternativa scartata — tabella figlia `TransferPointVariant`

Modello più puro: `TransferPoint` = identità dell'accordo (via + ricevente), figli 1..N = payload operativo.
Concettualmente corretto — la condizione qualifica il livello, non l'accordo.

Scartata **per ora**: sposterebbe ogni campo esistente in un'altra tabella nello stesso giro in cui se ne
aggiungono nove di nuovi, toccando insieme `TransferMatcher`, `TransferResolveContract`, `TransfersLive`, la
vLOA e gli snapshot. Doppia chirurgia, un solo collaudo. L'opzione A dà lo stesso risultato a schermo con una
frazione della superficie di rottura, e se un domani serve la tabella figlia, `VariantGroup` è già la chiave
pronta da promuovere.

## Fuori scopo, deciso

- **APP→TWR**: resta il modello a un evento (decisione 5).
- **Flag «salvo diverso coordinamento»**: prosa nel documento, una volta sola (decisione 8).
- **Import delle STAR**: il campo «via» resta testo libero. Oggi dal sectorfile Aurora si importano solo i SID
  (`AddSidImport`, round 34); il giorno che arrivano le STAR diventa un picker, senza cambiare lo schema.
- **Unità della velocità**: nodi IAS impliciti, nessuna colonna unità.
