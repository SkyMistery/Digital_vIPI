# A che punto è la traduzione: il meccanismo che lo dice

**4 settembre 2026** · richiesta del committente. Carta di progetto — **niente codice ancora scritto**.

> **In una riga.** Oggi lo stato della traduzione si conosce **un documento per volta e solo aprendolo**
> nella lingua di lettura; questo meccanismo lo calcola per **tutti** i documenti in una passata sola
> (misurata: **45 ms** sull'intero `vipi.db`), e lo dice con **due percentuali, non una** — quel che vede
> chi scrive (bozza contro memoria viva) e quel che vede **chi legge** (release efficace contro il
> congelato). E a chi ha appena finito di scrivere dice **quanto deve aspettare**: ≤ 15 minuti, all'orologio
> e non a stima (§4-bis). Nessuna tabella nuova.

## 1. Che cosa si sa oggi, e da dove

| Domanda | Chi risponde oggi | Limite |
|---|---|---|
| «Questo documento è tradotto?» | il **gettone** sotto il titolo (`TranslationNotice`) + `PrintMeta` | un documento per volta, e solo **aprendolo nella lingua di lettura** |
| «Quali frasi di questo documento mancano?» | il pannello **Traduzione** dell'editor (`DocumentTranslationReview`) | un documento per volta, elenco di righe: il totale si conta **a occhio** |
| «Come è stata resa questa frase?» | il **Registro** (`/services/vsop/admin/glossary`) | è orientato alla **frase**, non al documento: da lì non si risale a «quali documenti sono indietro» |
| «Il giro sta andando avanti?» | **solo il log del server** (`TranslationFillReport` esce in un `LogInformation`) | non c'è nessuna pagina che lo mostri; in produzione vuol dire aprire i file di diagnostica |
| «Quanto manca in tutto?» | **nessuno** | — |

⚠️ **Manca la domanda del committente per intero.** «A che punto è la traduzione» è una domanda sulla
**divisione**, e ogni risposta che esiste oggi è su **un** documento o su **una** frase.

## 2. Che cosa dice il dato vero (misurato il 4 settembre 2026 sul `vipi.db` di sviluppo)

Gate «misura prima di costruire» — sonda temporanea, `Microsoft.Data.Sqlite` in sola lettura più i veri
`TextSegmenter` / `TranslationText` / `TextProtector`, cioè la **stessa** segmentazione del corpus.

- **26 documenti**, 42 versioni, **696 titoli di sezione** e **218 blocchi**; memoria a **313 voci**
  (`it→en`: 71 umane + 34 macchina; `en→it`: 135 umane + 73 macchina).
- Una passata su **tutto**: leggere memoria, titoli e blocchi, segmentare, impronte, incrocio →
  **45 ms in tutto**. Non serve nessuna cache furba, nessuna tabella di stato: **si calcola**.
- Sui documenti in versione corrente: **281 segmenti, 0 mancanti**. Dal lato della **memoria** la divisione
  è a posto.
- 🔴 **E dal lato di chi legge no, e nessuno può vederlo.** Tutte e **17** le release **efficaci** portano
  `Doc.Language: null` **e** `Doc.Translations: null` — AccVipi, Airport, AirportMil, App, Vloa, senza
  eccezioni. Il congelamento riparato il 31 agosto ([[lingua-bloccata]]) vale dalla **prossima**
  pubblicazione: le release in vigore oggi non congelano niente, e quel che il pubblico legge viene **al
  100% dalla memoria viva**. È esattamente lo stato che una pagina di stato deve saper mostrare — e che
  oggi non compare da nessuna parte.

⚠️ **La lezione è nel numero, non nell'opinione**: una passata intera costa meno di mezzo decimo di secondo,
quindi **ogni** forma «lazy per riga» qui sarebbe una scelta peggiore e più complicata. È la stessa
conclusione di `EfTranslatableCorpus` («nessuna coda, per scelta») e di `DoveSiUsanoAsync` (una lettura per
**lotto**, non per riga).

## 3. La forma: un read-model calcolato, zero entità nuove

`IStatoTraduzione` → per documento un `StatoTraduzioneDocumento`. **Non** una tabella: un calcolo, come il
corpus. Una tabella sarebbe il secondo posto dove sapere una cosa, e si disallineerebbe al primo documento
eliminato — è la ragione già scritta e già pagata.

⚠️ **Pre-flight 1 — nessun modello gemello.** `TranslationCoverage(Segmenti, Tradotti, Riletti)` esiste già
ed è **esattamente** questa domanda: il read-model la **riusa**, non ne scrive una seconda accanto. Ci
aggiunge solo due cose che oggi non ha: **la divisione di ciò che manca** e **l'asse bozza/pubblicato**.

### 3.1 Due percentuali, non una

Per ogni documento, per la lingua di lettura, si dicono **due** copertine:

- **bozza** — i segmenti della versione di lavoro contro la **memoria viva**: quel che chi scrive sta per
  pubblicare;
- **pubblicato** — i segmenti dello **snapshot della release efficace** contro **congelato ∪ memoria**, con
  la stessa preferenza di `DocumentTranslator.NoteAsync`: quel che un lettore vede **adesso**.

🔴 **Le due non si fondono in un numero solo, mai.** «Bozza 100%, pubblicato 40%» è il guasto §Q18 in
persona — chi scrive prosa nuova e pubblica subito congela una traduzione incompleta — e una media
direbbe «70%», che è un numero che non descrive niente e non fa agire nessuno.

### 3.2 Quel che manca si divide in tre, o l'avviso diventa cieco

Un contatore di «mancanti» che non può arrivare a zero è un allarme che si impara a saltare — è già
successo col riquadro d'avviso ([[avviso-di-simulazione]], §AT). Quindi il mancante si classifica:

| Classe | Come si riconosce | Che cosa vuol dire a chi guarda |
|---|---|---|
| **in attesa** | non in memoria, e `TextProtector.Protect(...).Safe` | il giro lo prende entro **15 minuti**: non c'è niente da fare |
| **a mano** | non in memoria, e il protettore lo **rifiuta** (dato personale) | 🔴 **nessuna macchina lo farà mai**: vuole una persona |
| **rifiutato** | torna rotto dal motore a ogni giro | pagato e buttato, ogni quarto d'ora |

⚠️ **Le prime due sono gratis**: il protettore è deterministico, locale e senza rete — nella sonda il
verdetto sui 281 segmenti non si è nemmeno misurato.
⚠️ **La terza NO, e la carta lo dice invece di fingere**: i segmenti tornati rotti non si salvano da nessuna
parte (§Q16 «Aperto»), quindi «rifiutato» **non è calcolabile** senza un contatore suo, cioè **schema** —
e lo schema non si consegna prima del 16 settembre ([[finestra-cieca-al-16-settembre]]). Fino ad allora la
colonna **non esiste**: quei segmenti compaiono come «in attesa», che è la verità dal punto di vista di chi
guarda (il giro li riprenderà) e non promette una precisione che non c'è.

### 3.3 Lo stato in una parola, e i tre vuoti che non si somigliano

`StatoTraduzione` per documento: `Bloccata` · `NellaSuaLingua` · `NonCominciata` · `AChiazze` ·
`CompletaDaRileggere` · `Completa`.

⚠️ **I tre vuoti restano tre**, come già in `RevisioneDocumento`: «niente da tradurre», «lo stai leggendo
nella sua lingua» e «lingua **bloccata**» non si dicono con le stesse parole, o chi guarda pensa che la
pagina sia rotta. Un documento a lingua bloccata **non è allo 0%**: è fuori dal giro per decisione
editoriale, e sta fuori dal corpus per la stessa ragione.

### 3.4 Che cosa NON conta come mancante

- 🔴 **I titoli di catalogo** (`TitoliDiCatalogo`, [[titoli-di-catalogo-bilingui]]): si risolvono **dove si
  legge**, non sono segmenti e non stanno in memoria. Contarli manderebbe **ogni** vIPI d'aeroporto in
  rosso per sempre, e sarebbe un rosso falso.
- **Il titolo del documento**: non si traduce (R4), non è nel corpus, non è nel conto.
- **I testi fuori dai documenti** — descrizioni e attivazioni delle aree regolamentate (**230 righe, 9 + 6
  distinte**) e le intro di pagina — **non si attribuiscono a un documento**: comparirebbero N volte.
  Vanno in **una riga a parte**, «fuori dai documenti», che è la loro verità.

## 4. Dove si vede — una fonte, quattro sedi

1. **`/services/vsop/admin/glossary`** — terza sezione richiudibile, **«Documenti»**: la tabella di stato.
   È la sede giusta perché quella pagina è **già** la casa admin della traduzione (glossario + registro), e
   la domanda «a che punto siamo» si fa lì, non in una dodicesima voce di barra.
   Colonne: documento · famiglia · lingua · **bozza** · **pubblicato** · da rileggere · a mano · stato;
   ordinabile per «quanto manca», che è l'ordine con cui si lavora.
2. **Il giro, in chiaro**: sopra la tabella, l'esito dell'**ultimo giro di riempimento** (quando, quanti
   aggiunti, quanti mancano, **quale motore ha risposto**, caratteri scartati). Oggi quel rapporto esiste
   già intero (`TranslationFillReport`) e **finisce solo nel log**: portarlo a schermo è il pezzo più
   economico di tutta la carta, ed è quello che risponde a «sta andando avanti?».
3. **`/services/vsop/versions`** — una pastiglia per riga, **dalla stessa passata** (una query per tutte le
   righe, mai una per riga).
4. **Il pannello Traduzione dell'editor** — la barra dei totali in cima all'elenco che già c'è, con
   **l'attesa** (§4-bis); e la **pubblicazione**, che oggi «avvisa e non blocca», dice finalmente il
   numero: «pubblicando adesso congeli **12 frasi su 40** non tradotte».

⚠️ **Per il lettore pubblico non cambia niente**: il gettone sotto il titolo dice già quel che deve dire.
Questa è una pagina di **chi cura** i documenti.

## 4-bis. «Ho appena finito di scrivere: quanto ci vuole?»

Domanda del committente, 4 settembre. È **la** domanda di chi scrive, e la prima stesura di questa carta
rispondeva male («nessuna stima di tempo»). Letto il giro, quella cautela non era giustificata.

- ⚠️ **Il giro non ha un tetto di lotto**: `TranslationFillUseCase` spedisce in **una** chiamata *tutti* i
  segmenti mancanti. Non c'è nessuna coda che si smaltisce a pezzi, quindi il numero di frasi appena
  scritte **non allunga l'attesa**.
- Il periodo è fisso, **15 minuti** (`TranslationFillHostedService.Periodo`); la chiamata al motore sono
  secondi. Dopo un riavvio dell'host il primo giro parte con **bootDelay 120 s**, e il gate di
  `GatedImportLoop` non lo rifà se la categoria è ancora fresca.
- ⚠️ **L'ora dell'ultimo giro è già salvata** — `IImportStateStore`, chiave `ImportCategories.Translation` —
  e **non la mostra nessuna pagina**: quella riga sta fuori dall'elenco Sorgenti per scelta dichiarata
  («non è un import»). Quindi «prossimo giro fra ~N minuti» è `ultimo + 15 min`: **una query, zero schema**,
  ed è un orologio, non una stima.

Da cui la risposta, che è una tabella e non un numero:

| Caso | Quanto ci vuole | Come si riconosce |
|---|---|---|
| frase normale | **≤ 15 min** (in media ~7) | non in memoria, e il protettore la lascia passare |
| il **protettore** la rifiuta (dato personale dentro) | 🔴 **mai**: nessuna macchina la vedrà | `Protect(...).Safe == false` |
| torna **rotta** dal motore | 🔴 **mai**: ripagata e ributtata ogni 15 min | oggi non è calcolabile (§7) |
| tetto di spesa finito, motore giù, interruttore spento | **ferma**, e oggi lo dice solo il log | l'esito dell'ultimo giro (sede 2) |

🔴 **È per questo che il mancante si divide** (§3.2): «mancano 8» senza dire quali sono in attesa e quali
vogliono una persona è un numero che non fa fare niente a nessuno. In cima al pannello dell'editor la frase
è una sola: «**8 frasi nuove · il giro passa fra ~6 min · 1 vuole te**».

⚠️ **E non bisogna aspettare per pubblicare**, che è l'altra metà della domanda. Dal §Q18 il congelato si
sovrappone alla memoria **«dove», non «se»**: le frasi che la release non porta le riempie la memoria viva.
Si pubblica, il giro passa, il lettore le vede. (Oggi a maggior ragione: le 17 release efficaci non
congelano niente affatto — §2.)

🟡 **Deciso a metà, e la decisione è del committente**: un tasto **«traduci ora»** sul documento. Si può
fare senza rompere la regola «non si traduce al salvataggio» — quella regola esiste perché un disservizio
di rete non deve impedire di **salvare**, e un tasto premuto da una persona non è una dipendenza del
salvataggio; e i trigger manuali sono **già** indipendenti dal gate (commento di `GatedImportLoop`). Ma
**spende**, quindi va deciso a chi si dà e con quale tetto. Finché non è deciso, resta fuori dalle slice.

## 5. Pre-flight

1. **Modello** — nessuna entità, nessuna tabella, nessuna migrazione: si estende `TranslationCoverage` e si
   aggiunge un read-model calcolato. «Dove si sa quanto è tradotto un documento» resta **un** posto.
2. **Dispatch** — le sei famiglie hanno snapshot di forma diversa. La domanda «dammi i segmenti di questo
   snapshot» va sul **descrittore di famiglia già esistente** (`IReleaseTarget` / `IDocKindRoutes`), non in
   un `switch (TargetType)` nuovo: sarebbe il terzo, cioè il caso che la Regola del 2 vieta.
3. **Ingressi + verifica** — ingresso: la sezione «Documenti» del Registro, raggiungibile dalla barra admin
   che c'è già. Verifica **live**: `LIBC` (Airport, efficace, `Language: null`) deve mostrare
   **pubblicato < bozza**; poi si ripubblica e la stessa riga deve passare a congelato pieno. È la prova
   che distingue questo meccanismo da un contatore qualsiasi — e nessun test la sostituisce, perché il
   difetto §Q18 era **verde nei test**.
4. **Propagazione** — non si rimuove né si rinomina niente. Si aggiornano a fine giro
   `docs/lavori-aperti.md`, `docs/design/regole-lingua.md` (se la sezione «Documenti» diventa citabile) e
   le memorie [[documenti-bilingue]] e [[fraseologia-e-traduzioni-una-pagina]].

## 6. Le slice

| # | Slice | Che cosa chiude |
|---|---|---|
| 1 | `IStatoTraduzione` + record, sul **solo** asse bozza, con test sul cuore deterministico | il calcolo, senza UI |
| 2 | L'asse **pubblicato** (snapshot + congelato ∪ memoria) via descrittore di famiglia | la seconda percentuale, che è il motivo della carta |
| 3 | La divisione del mancante (**in attesa** / **a mano**) col protettore | l'allarme che può arrivare a zero |
| 4 | La sezione «Documenti» del Registro | la sede |
| 5 | L'**ultimo giro** a schermo (`TranslationFillReport` fuori dal log) | «sta andando avanti?» |
| 5-bis | **L'attesa nel pannello dell'editor** (§4-bis): «N frasi nuove · il giro passa fra ~M min · K vogliono te», da `IImportStateStore` + `Periodo` | «quanto ci vuole per quel che ho appena scritto» |
| 6 | Pastiglia su `/versions` + numero nella pubblicazione | le altre due sedi |
| 7 | Verifica live su LIBC, con traccia | la prova |

⚠️ La **5-bis** è la slice che risponde alla domanda di chi scrive, e non dipende dalle altre: `Periodo` è
una costante e l'ultimo giro è già in `IImportStateStore`. Si può anticipare se serve prima.

## 7. Quel che resta fuori, dichiarato

- **«rifiutato»** come classe propria: vuole un contatore, cioè schema → **dopo il 16 settembre** (§Q16).
  Fino ad allora quei segmenti si vedono come «in attesa».
- **Nessuna previsione oltre il prossimo giro** («mancano ~2 giri»): il quando del **prossimo** è un
  orologio (§4-bis), il quando del secondo sarebbe una promessa — dipende dal tetto di spesa e dalla catena
  dei motori. Si dice il fatto, non l'oroscopo.
- **Nessuna azione dalla pagina di stato** (niente «traduci ora» **lì**): è una pagina che **dice**, e la
  correzione ha già la sua sede nel pannello del documento e nel Registro. Il tasto sul **documento** è
  un'altra cosa e resta 🟡 da decidere (§4-bis).

Vedi [[documenti-bilingue]], [[lingua-bloccata]], [[spesa-di-traduzione]],
[[fraseologia-e-traduzioni-una-pagina]], [[titoli-di-catalogo-bilingui]].
