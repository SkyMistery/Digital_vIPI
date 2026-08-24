# Coordinamenti — il lato di CHI RICEVE ✅

> Stato: **FATTO il 24 agosto 2026** — cinque commit sul ramo `coordinamenti-lato-ricevente`, verificato live.
> Fratello di [2026-08-23-live-coordinamenti-a-colonne.md](2026-08-23-live-coordinamenti-a-colonne.md) e della
> memoria `coordinamenti-lettura`.
>
> **Esito.** `dotnet build Vipi.slnx -c Release --no-incremental` verde su entrambi i TFM (0 avvisi);
> 1925 test verdi su net8. Fixture: 72 righe entranti (primo giro) + 10 righe con faccetta trasferimento
> (secondo giro, §4.3-bis). Verifica live sul `vipi.db` di sviluppo (copia), vIPI ACC di Brindisi: **39 tabelle**,
> 23 con colonna «PROSSIMO» e 13 con «DA», **zero incoerenze** fra l'intestazione e il verbo della frase;
> **4 nodi misti** tagliati in due (Zagabria, Grecia, Tirana, Beograd — tutti sotto «Sorvoli»).
> Il viewer pubblico è rimasto **identico** (33 tabelle, tutte «PROSSIMO», zero «riceve da»): legge lo
> snapshot, vedi §6.1.

## 1. Il difetto, in una frase

Un accordo si scrive **una volta sola**, dal lato di chi cede. Il documento di chi **riceve** lo mostra con le
parole (e con l'intestazione di colonna) di chi cede: legge come se fosse il documento dell'altro.

### Prova sul dato reale (fixture `real-coordination.approved.txt`, blocco LIBB)

```
<< LIBD_CS0_APP | LDZO_CTR (Ctr) | Arrival | LIBD
   cop=AIOSA liv=FL230 next=LDZO_CTR …
   «Zagreb Radar trasferisce a Brindisi Radar CS0 il traffico con destinazione Bari Palese LIBD stabile a livello 230 su AIOSA.»
```

Nel documento di `LIBD_CS0_APP` questa riga dice:

1. **la frase** — soggetto è Zagabria, non noi. Deve essere «*Brindisi Radar CS0 riceve da Zagreb Radar il
   traffico con destinazione Bari Palese LIBD stabile a livello 230 su AIOSA.*»;
2. **la colonna** — la cella porta `LDZO_CTR`, cioè **chi consegna**, sotto l'intestazione `Prossimo`
   (`AppCoord_Next`), che significa il contrario. Deve dire «Da».

Le due cose hanno la **stessa causa**: la direzione della riga esiste in derivazione
(`CoordinationEntry.IsIncoming`) e **si perde prima** di arrivare alla riga di tabella e alla frase.

## 2. Il vincolo che decide il disegno: le tabelle sono MISTE

Non basta un interruttore per tabella. `CoordinationDerivation.BuildAccTree` raggruppa per
`settore → ACC della controparte → aeroporto/tipo`: **la direzione non è una chiave di raggruppamento**, quindi
uscenti ed entranti finiscono nella stessa tabella. Misurato sulla fixture:

| Tabella (blocco LIBB) | righe entranti | righe uscenti |
|---|---|---|
| settore `ES` · acc `Zagreb-LDZO` · extra **Sorvoli** | 8 | 6 |
| settore `ES` · acc `Greece-LGGG` · aeroporto `LIBD` | arrivi 1 | partenze 1 |

→ **la direzione va portata sulla RIGA**, non sulla tabella. Ogni disegno «cambio l'intestazione della tabella
in base al tipo di flusso» è la stessa scorciatoia che c'è già oggi in `AppCoordinationView` (arrivi ⇒ «Da»,
riga 41) e che funziona per caso solo fra APP e ACC.

**La decisione del committente**: quando un nodo è misto, si **spezza in due tabelle** — non si mescolano le
due direzioni con un marcatore per riga. Conseguenza che semplifica tutto il resto: **ogni tabella resa è
omogenea**, quindi l'intestazione di colonna torna a bastare da sola e non serve nessuna freccia per riga.

## 3. Pre-flight (FEATURE-PROCESS, 4 domande)

1. **Modello** — nessun concetto nuovo: `IsIncoming` **esiste già** su `CoordinationEntry`. Si smette di
   buttarlo via. Nessun modello gemello.
2. **Dispatch** — nessuno switch nuovo per-tipo. La scelta del template è **un ternario dentro il composer**,
   nell'unico punto che già sceglie fra `Template` e `TemplateCleared`.
3. **Ingressi + verifica** — nessun ingresso UI nuovo (niente da creare: è una lettura). Verifica live: aprire
   la vIPI APP di `LIBD_CS0_APP` e la vIPI ACC di `LIBB` e leggere la tabella Sorvoli · Zagreb, che è mista.
4. **Propagazione** — nulla viene rimosso né rinominato. Cade **un** commento diventato falso
   (`AppCoordinationView.razor:38`) e **un** parametro diventa inutile (`LastColHeader`, vedi §4.4).

## 4. Il piano

### 4.0 Regola unica

> **La direzione è un attributo della RIGA.** La derivazione la conosce; la riga la porta; la frase, il taglio
> in tabelle e l'intestazione la leggono da lì. Nessun consumatore la ri-deduce dal tipo di flusso o dai
> callsign.

### 4.1 Dato — `AppCoordRow.IsIncoming` (additivo)

`src/Vipi.Application/Content/AppModels.cs`

```csharp
/// <summary>La riga è ciò che ENTRA nell'ente del documento: il counterpart (Next) è chi CONSEGNA, non chi riceve.
/// false = riga uscente (comportamento storico, e valore di una release congelata prima del 24 agosto 2026).</summary>
public bool IsIncoming { get; init; }
```

⚠️ **Additivo e basta**, per la ragione già scritta a `AppModels.cs:69`: `AccCoordination`/`AppCoordination`
finiscono serializzate dentro le release congelate. Un campo nuovo si deserializza a `false` sulle release
vecchie → **restano identiche a oggi**, che è quello che vogliamo (§6).

### 4.2 Derivazione — smettere di buttare via `IsIncoming`

`CoordinationDerivation.cs`

- `ToRow(…, string? lead = null, bool isIncoming = false)` → coda facoltativa: la chiamata vLOA
  (`VloaDerivationService.cs:223`) non si tocca.
- `Build`: passo 1 → `isIncoming: false`; passo 2 (riga 158) → `isIncoming: true`.
- I due locali `Compose`/`Lead` ricevono lo stesso flag e lo girano al composer.

### 4.3 Frase — due template nuovi (più uno)

`CoordinationSentenceTemplate` (`ICoordinationSentenceTemplate.cs`) + binding in
`CoordinationSentenceOptions.cs` + `content/coordination-sentence.json`.

**Gli slot NON cambiano di significato**: `{owner}` resta *chi cede*, `{target}` resta *chi riceve*. A cambiare
è solo l'**ordine delle parole** nel template. Scelto così perché la regola dei codici di posizione
(`OmitTargetCode`, `BuildData`) è asimmetrica fra i due slot: scambiare gli argomenti al chiamante la
cambierebbe di significato in silenzio.

**Forma confermata dal committente** — dalla riga vera «*TS1 EXE riceve da ES0 EXE il traffico in salita per
FL240.*»: cambia **solo la testa** della frase, la coda (aeroporto · stato · livello · punto · condizione ·
velocità · comunicazioni) resta **parola per parola** quella dei trasferimenti di oggi.

| campo | italiano |
|---|---|
| `TemplateReceive` | `{target} riceve da {owner} il traffico {airport} {stato} {fl} su {point}.` |
| `TemplateLeadReceive` | `{target} riceve da {owner} il traffico {airport} secondo la tabella seguente:` |
| `TemplateClearedReceive` | `{target} riceve da {owner} il traffico {airport} autorizzato via {point} {fl}, trasferito {handoff} {handoffLevel} {stato}.` |

### 4.3-bis Anche la frase USCENTE con faccetta cambia forma (secondo giro, stesso giorno)

Chiesto dal committente dopo aver letto il primo giro: «*come quella di chi riceve ma con «trasferisce a», così
hanno lo stesso formato*». Aveva ragione, e il difetto era più vecchio di questo lavoro.

`TemplateCleared` girava il **verbo principale** — «{owner} **autorizza** il traffico {airport} via {point}
{fl} **e lo trasferisce a** {target} …» — mentre la forma breve dice «{owner} **trasferisce a** {target} il
traffico {airport} …». Dentro la **stessa tabella**, due righe che dicono lo stesso accordo si aprivano quindi
in due modi diversi, a seconda che portassero o no la faccetta: il lettore doveva ricostruire da capo chi fosse
il soggetto.

| campo | prima | ora |
|---|---|---|
| `TemplateCleared` | `{owner} autorizza il traffico {airport} via {point} {fl} e lo trasferisce a {target} {handoff} {handoffLevel} {stato}.` | `{owner} trasferisce a {target} il traffico {airport} autorizzato via {point} {fl}, {handoff} {handoffLevel} {stato}.` |

Le **quattro** forme (× direzione, × faccetta) hanno ora la **stessa testa e la stessa coda**: a cambiare è solo
il verbo, che è l'unica cosa che deve cambiare.

```
X trasferisce a Y  il traffico …                                     su PUNTO.
X trasferisce a Y  il traffico … autorizzato via P a livello N,             al confine dell'AoR passando FL50.
Y riceve da X      il traffico …                                     su PUNTO.
Y riceve da X      il traffico … autorizzato via P a livello N, trasferito  al confine dell'AoR passando FL50.
```

⚠️ **«trasferito» c'è solo nel verso entrante.** Là il verbo principale è «riceve» e senza quella parola il
luogo resterebbe appeso; nel verso uscente il verbo è già «trasferisce», e «trasferisce … trasferito»
balbetta. È l'unica asimmetria fra le quattro forme, ed è voluta.

⚠️ **Questo giro TOCCA le righe uscenti**, al contrario del primo: 10 frasi nella fixture (5 IT + 5 EN della
vLOA), tutte e sole quelle con la faccetta. Anche queste si vedono solo **dopo la ripubblicazione** (§6.1).

Reso vero, misurato a schermo sul nodo `ES › Brindisi-LIBB › Bari Palese LIBD`:

> «Brindisi Radar ES trasferisce a Brindisi Radar CS0 il traffico con destinazione Bari Palese LIBD
> autorizzato via TESTO a livello 120 o livello inferiore, al confine dell'AoR passando FL50.»

Reso atteso sulla riga della fixture:

> «Brindisi Radar CS0 riceve da Zagreb Radar il traffico con destinazione Bari Palese LIBD stabile a livello 230 su AIOSA.»

…e i gemelli inglesi su `CoordinationSentenceTemplate.English` (`{target} receives from {owner} the traffic
{airport} …`), perché il template inglese **esiste già** ed è quello delle vLOA: lasciarli fuori vorrebbe dire
una frase italiana dentro un documento bilaterale il giorno in cui una vLOA userà il verso entrante.

Nel composer, la scelta del template diventa una tabella a due dimensioni (faccetta × direzione) nell'unico
punto che già sceglieva:

```csharp
var t = (hasHandoff, d.IsIncoming) switch
{
    (true,  true)  => tpl.TemplateClearedReceive,
    (true,  false) => tpl.TemplateCleared,
    (false, true)  => tpl.TemplateReceive,
    (false, false) => tpl.Template,
};
```

`ComposeLead` fa lo stesso fra `TemplateLead` e `TemplateLeadReceive`.
`CoordinationSentenceData.IsIncoming` (default `false`) e `CoordinationSentences.Compose/ComposeLead` ricevono
`bool isIncoming = false` **in coda**: tutte le chiamate esistenti (editor `CoordinationPreviewContext`, vLOA,
28 chiamate nei test) restano valide e invariate.

> L'anteprima dell'editor (`CoordinationPreviewContext`) **resta uscente**: lì si sta scrivendo l'accordo dal
> lato di chi cede, ed è la prospettiva giusta.

### 4.4 Il taglio in due tabelle — dentro `CoordTable`, non nelle viste

Il taglio si fa **in un posto solo**: `CoordTable.razor`. Riceve già tutte le righe e il titolo, e serve tutti e
tre i consumatori (vIPI ACC, vIPI APP, vLOA). Farlo nelle viste vorrebbe dire scriverlo due volte e mezzo, e la
tabella era stata unificata proprio per non tenere d'accordo a mano due copie.

Il corpo attuale (blocco `<details class="coord-prose">` + `<table>`) si estrae in un `RenderFragment` locale
che prende `(righe, titolo)`. Poi:

```razor
@if (anyIn && anyOut) { @Section(out-rows, Titolo(uscenti)); @Section(in-rows, Titolo(entranti)); }
else                  { @Section(Rows, Title); }
```

**Il taglio scatta SOLO se il nodo è davvero misto.** Un nodo omogeneo — la stragrande maggioranza — resta
**una tabella sola, identica al byte a oggi**, titolo compreso: cambia al più la parola dell'intestazione.

| caso | tabelle | titolo | intestazione `c-next` |
|---|---|---|---|
| solo uscenti | 1 *(invariata)* | `Arrivi` *(invariato)* | `Prossimo` / `Next` *(invariato)* |
| solo entranti | 1 | `Arrivi` | `Da` / `From` |
| **misto** | 2 | `Arrivi · che cediamo` + `Arrivi · che riceviamo` | `Prossimo`, poi `Da` |

Poiché ogni tabella resa è ora omogenea, **l'intestazione si calcola dalle righe della sezione**
(`rows.Any(r => r.IsIncoming) ? Da : Prossimo`) e **non serve nessuna freccia per riga**.

Ogni sezione ricalcola **le proprie** colonne opzionali (`hasCond`, `hasHandoff`, `hasSpeed`, …): una colonna
che riempiono solo le righe entranti non deve comparire vuota nella tabella delle uscenti.

#### I titoli, senza rubare spazio

Il vincolo del committente è che i titoli **non occupino righe in più del necessario**. Tre scelte, in ordine
di risparmio:

1. **Il titolo sta già dove costa zero.** `CoordTable` lo rende **dentro il `<summary>` di `.coord-prose`**,
   sulla stessa riga dell'invito «▸ Testo esteso (N frasi)» — la scelta del 23 agosto che ha tolto una riga per
   tabella. Il titolo lungo entra lì e **non aggiunge nessuna riga**.
2. **Nessun `<details>` in più.** La direzione **non** diventa un livello di annidamento: sarebbe un
   `<summary>` in più per nodo, cioè esattamente la riga che stiamo cercando di non spendere. Le due tabelle
   sono sorelle dentro lo stesso nodo aeroporto/sorvoli che c'è già.
3. **Il costo reale del taglio è UNA riga**, e solo sui nodi misti: la seconda riga `<summary>`. È il minimo
   possibile senza nascondere l'informazione.

Stessa classe di oggi (`.coord-kind`), quindi **nessuna regola CSS nuova e nessuna misura da rifare**
(memoria `titoli-tag-vs-misura`: la misura la porta la classe, non il tag).

**Le parole**: `· che cediamo` / `· che riceviamo`, invariabili apposta — «ceduti/cedute» concorderebbe col
genere e darebbe «Partenze · cedute» accanto ad «Arrivi · ceduti», cioè due parole diverse per la stessa cosa.
Dove il titolo manca (nodi «Sorvoli», che passano `Title=null` perché il nome è già nel `<summary>` del nodo)
diventano da sole: `Che cediamo` / `Che riceviamo`.

Chiavi risorsa nuove: `Coord_WeHandOver`, `Coord_WeReceive` (IT/EN; EN «we hand over» / «we receive» per la
vLOA). `AppCoord_From` e `AppCoord_Next` esistono già.

**`LastColHeader` si rimuove**: era il parametro con cui il chiamante indovinava. Toglie anche il commento
falso di `AppCoordinationView.razor:38-41` e sistema di riflesso le tabelle verso torri / verso APP, che oggi
scrivono «Prossimo» anche su ciò che entra. (Propagazione, domanda 4: la rimozione tocca `CoordTable.razor`,
`AppCoordinationView.razor`, e il commento a `AccCoordinationView.razor:97`.)

### 4.5 Prosa capofila — cade da sé

`CoordTable.Lead()` oggi prende **la prima riga che ha una frase**: in una tabella mista annuncerebbe una sola
delle due direzioni e mentirebbe sull'altra. Col taglio del §4.4 **ogni sezione è omogenea**, quindi la
capofila resta **una sola per tabella** e il metodo non cambia — va solo reso per sezione invece che per
componente.

> Limite **preesistente e fuori perimetro**: se sotto uno stesso ACC ci sono due settori esteri diversi
> (`LGGG_W_CTR` e `LGGG_E_CTR`), la capofila ne nomina uno solo. Non lo introduce questo giro.

## 5. Passi (una slice per commit, build verde a ogni passo)

| # | passo | tocca |
|---|---|---|
| 1 | test di caratterizzazione **prima**: riga entrante in `CoordinationDerivationTests` + un caso `CoordTable` misto (2 tabelle, 2 intestazioni) | `tests/` |
| 2 | `AppCoordRow.IsIncoming` + `ToRow`/`Build` lo passano | `AppModels.cs`, `CoordinationDerivation.cs` |
| 3 | i tre template nuovi (IT + EN) + binding options + json | `ICoordinationSentenceTemplate.cs`, `CoordinationSentenceOptions.cs`, `content/coordination-sentence.json` |
| 4 | composer: scelta template per (faccetta × direzione), `Compose`/`ComposeLead` col flag | `CoordinationSentenceComposer.cs` |
| 5 | **meccanico**: estrarre il corpo di `CoordTable` in un `RenderFragment` per sezione, nessun cambio di reso | `CoordTable.razor` |
| 6 | **logica**: taglio in due sui nodi misti, titoli, intestazione dalle righe, via `LastColHeader` | `CoordTable.razor`, `AppCoordinationView.razor`, `AccCoordinationView.razor`, `.resx` ×2 |
| 7 | ri-approvazione fixture + verifica live | `real-coordination.approved.txt` |

Passi 5 e 6 separati apposta: meccanico e comportamento in commit distinti (FEATURE-PROCESS, post-flight).

**Come è andata.** `2ed4a52` (passi 1–4 + fixture), `54b4cc9` (passo 5, meccanico), `8c7b49b` (passo 6),
`6ad66df` (carta). Il passo 7 non ha richiesto correzioni. Un quinto commit ha poi riscritto anche la frase
USCENTE con faccetta, su richiesta del committente: §4.3-bis.

Quello che la verifica live ha reso, sul nodo misto `ES › Zagreb-LDZO › Sorvoli`:

```
Che cediamo    · Testo esteso (5 frasi)
  «Brindisi Radar ES trasferisce a Zagreb Radar il traffico stabile per un livello dispari su TORPO.»
  COP     LIVELLO    PROSSIMO
  TORPO   dispari    LDZO_CTR      … 5 righe

Che riceviamo  · Testo esteso (6 frasi)
  «Brindisi Radar ES riceve da Zagreb Radar il traffico stabile per un livello pari su TORPO.»
  COP     LIVELLO    DA
  TORPO   pari       LDZO_CTR      … 6 righe
```

TORPO compare in tutt'e due, con livelli complementari (dispari in uscita, pari in entrata): è il caso che
sotto un'unica intestazione «Prossimo» si leggeva come una contraddizione invece che come un accordo.

### Due attrezzi da rifare, se si torna qui

La verifica live è costata due giri per colpa della sonda, non del codice — vale la pena scriverlo:

- **L'ultima `<th>` NON è la colonna della controparte.** Con «Anche per» o «Condizione» accese, quelle le
  stanno dopo, e la sonda riportava `ALSO FOR` dove la colonna diceva `DA`. Si legge `thead th.c-next`, che è
  una classe semantica proprio per questo.
- **Nell'editor lo scroll della finestra non porta il nodo dove il `clip` se lo aspetta**: il documento scorre
  dentro un pannello di altezza misurata (`vipiFitViewport`). Lo scatto si fa sull'**elemento**
  (`elementHandle.screenshot()`), che se ne occupa da sé.
- E `details.help-hint` sono `<details>` anche loro: `document.querySelectorAll('details').open = true` apre
  pure i «?», e il loro pannello copre la pagina. Si richiudono **solo quelli** — chiudere ogni `details`
  non-coord nasconde l'albero, e su un elemento nascosto `innerText` torna vuoto: il nodo non si trova più.

## 6. ⚠️ Le tre trappole di questo giro

### 6.1 ✅ Confermata a schermo

1. **Le release congelate non cambiano da sole.** La pagina pubblica legge lo snapshot: `Sentence` e
   `LeadSentence` sono **stringhe già scritte** dentro la release, e `IsIncoming` si deserializza `false`. I
   documenti già pubblicati continueranno a dire «Zagreb Radar trasferisce a…» finché non si **ripubblica**.
   Da mettere in chiaro col committente prima di dichiarare chiuso il lavoro. (Stessa trappola già pagata:
   memoria `coordinamenti-lettura`, e `app-standalone-ombra-aeroporto` — «le release già scritte vanno
   RIPUBBLICATE».)
   **Misurato il 24 agosto**: aperte fianco a fianco le due pagine della stessa vIPI ACC di Brindisi, il
   viewer pubblico rende 33 tabelle **tutte** con «PROSSIMO» e **zero** frasi «riceve da»; l'editor, che
   deriva live, ne rende 39 con 13 «DA» e 4 nodi tagliati in due. **La differenza è tutta la ripubblicazione.**
2. **`dotnet test --artifacts-path` fa sparire le fixture** (63 casi → 13): la ri-approvazione del passo 7 va
   fatta **senza** quel flag, o si approva il vuoto. (memoria `coordinamenti-lettura`)
3. **`dotnet build` verde su ENTRAMBI i TFM**: gli avvisi sono errori e `dotnet test` non usa quel flag →
   `dotnet build Vipi.slnx -c Release --no-incremental`. Le librerie toccate sono **net8**: niente C#13+.
   (memorie `audit-2026-08-11-esito`, `multitarget-net8-embedding`)

## 7. Cosa NON si tocca

- **vLOA** (`VloaDerivationService`): è bilaterale e costruisce **due alberi separati** H2F/F2H, ognuno reso
  dalla prospettiva di chi cede. Corretto com'è; passa `IsIncoming: false` su entrambi e resta così — nessuna
  sua tabella sarà mai mista, quindi nessuna si spezza.
- **Editor** (`XferRowsTable`, `CoordinationPreviewContext`, «Settore ricevente»): si scrive l'accordo dal lato
  di chi cede, e lì «ricevente» è la parola giusta.
- **Vista live** (`TransfersLive`): fuori perimetro, direzione già esplicita per colonna.
- La struttura dell'albero `BuildAccTree`: la direzione **non** diventa una chiave di raggruppamento. Il taglio
  vive in `CoordTable`, dove costa un `<summary>` sui soli nodi misti; nell'albero costerebbe un livello di
  `<details>` su **tutti** i nodi.

## 9. Quello che la verifica live NON ha potuto provare

`AppCoordinationView` — la vista dei coordinamenti degli **APP non remotizzati** — è stata toccata (via il
parametro `LastColHeader`, rimosso) ma **non esercitata su dati veri**: nel `vipi.db` di sviluppo gli unici
documenti APP sono Amendola (`LIBA_APP`) e Pescara (`LIBP_APP`), e **nessuno dei due ha accordi**. Le loro
pagine si aprono senza errori e senza tabelle. La regola che quel parametro nascondeva ora sta in `CoordTable`
ed è coperta dai test bUnit; ma a schermo, con dati veri, l'ha vista solo la vista ACC.

Nota sul caso citato dal committente (`LIBD_CS0_APP riceve i traffici da LIBB_ES_CTR`): l'accordo c'è
(`CoordinationAgreements` #2), ma **oggi non ha una pagina dove comparire** — `LIBD_CS0_APP` è
`Kind=Airport`, `IsPrimary=0`, `DocumentId=NULL`, quindi non è né un blocco della vIPI ACC (che porta il solo
`LIBB_ES_CTR`) né il primario del documento LIBD, che è `LIBD_TWR`. È una questione di **struttura dei
settori**, indipendente da questo giro, e va guardata a parte.
