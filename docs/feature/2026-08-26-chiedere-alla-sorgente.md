# Chiedere alla sorgente, invece di aspettarla — carta (26 agosto 2026, sera)

> **Stato: ✅ ESEGUITA il 26 agosto 2026**, sul ramo `statistiche-atc`. Build Release pulita sui due TFM
> (0 avvisi), test verdi — net8 **2586**, net10 **2348** — e provata a schermo (§8).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Estende [«Eliminare, con le protezioni»](2026-08-26-eliminare-con-le-protezioni.md), di cui tocca la sola
> regola **D8**. Le altre sette restano dove sono, e §3 dice perché non è una concessione ma il punto.

## La domanda

Del committente, la stessa sera:

> «Rivediamo il sistema di eliminazione. Vero che devi aspettare le interrogazioni fatte, però secondo me
> possiamo pensare a un meccanismo per cui, se lo vuoi eliminare, nel pannello di eliminazione, di chiamare
> le API per quello specifico elemento per vedere se rispondono.»

Ha ragione, e la ragione è più profonda di una comodità: **i due giri sono un modo di dedurre l'assenza dal
silenzio, e il silenzio esiste solo perché nessuno ha chiesto.** Se si chiede e la sorgente risponde,
l'assenza smette di essere un'ipotesi sul passato e diventa una constatazione del presente. Non è aggirare
la protezione: è ottenere per via diretta ciò che la protezione ottiene per via indiziaria.

## §1 — Perché serve adesso, e non «prima o poi»

Tre casi veri, in ordine di urgenza.

**Il DB che si sta per ripulire.** Quando il committente svuota l'archivio, con lui se ne va `ImportState`.
Da quel momento `GetPrevSuccessAsync` risponde `null` per ogni categoria, e
`SogliaEliminazione.MotivoDelRifiuto` dice a chiunque provi a cancellare qualcosa: *«la sorgente è stata
interrogata con successo meno di due volte»*. Con i giri a 24 ore e la distanza minima di un'ora fra due
successi, **per due giri interi non si può eliminare niente**: proprio nella finestra in cui si sta
sistemando l'archivio e il tasto elimina serve di più. Chiedere in puntuale non ha bisogno di storia.

**Il fantasma della rinomina.** Una riga di catalogo rinominata non fa sparire il vecchio callsign: restano
due settori attivi e un documento appeso a quello che non c'è più, e l'unico segno è il timbro
`ImportedAtUtc` che smette di scorrere. Oggi lo si ripulisce dopo due giri; con una domanda, lo stesso
giorno in cui ci si accorge.

**La risposta «c'è ancora», che oggi costa 48 ore.** Il verdetto opposto vale quanto quello che sblocca: chi
vuole eliminare un settore che la sorgente manda ancora lo scopre oggi *aspettando due giri per non vederlo
sparire*. In puntuale lo sa in duecento millisecondi — e può anche sapere **come** lo manda (un `centerId`
diverso: il settore non è sparito, ha cambiato ente).

## §2 — Le tre trappole, e perché non è «chiama l'API e guarda se torna null»

### T1 — `null` non vuol dire 404

`IvaoHttp.GetJsonAsync` e `GetStringAsync` (`IvaoHttp.cs:52-65`) ritornano `null` per **qualunque** risposta
non-2xx. Oggi `IAirportDirectory.GetByIcaoAsync` che torna `null` significa indifferentemente «404»,
«401 token scaduto», «403 scope mancante», «429», «502 IVAO giù». Va benissimo per riempire il nome di un
aeroporto in un editor — è *best-effort* per costruzione, e lo dichiara. È disastroso per autorizzare una
cancellazione: **un'ora storta della sorgente diventerebbe il permesso di svuotare il catalogo**, cioè
esattamente ciò che D8 esiste per impedire.

→ Serve una porta che guardi lo **status**. Le porte anagrafiche non possono dirlo, e allargarle
trasformerebbe ogni loro chiamante in un gestore di errori HTTP.

### T2 — L'elenco vuoto è la stessa ambiguità, servita più in fretta

D8 nasce da una frase precisa (`SogliaEliminazione.cs:7-12`): *«un giro può riuscire e tornare vuoto per un
ente: una risposta a zero elementi non è un errore»*. Una domanda puntuale che chiede
`/v2/centers/LIRR/subcenters` e riceve `200 []` **ricrea identica quella trappola**. Crederle qui
rifarebbe in venti secondi l'errore che la regola dei due giri impiega due giorni a evitare.

→ Il verdetto «assente» non può essere «non l'ho trovato». Deve essere **una prova positiva**: la sorgente
risponde, **nomina altro**, e questo non c'è.

### T3 — La cache risponde al posto della sorgente

`IvaoAirportClient.GetByIcaoAsync:68` legge `_airportCache.TryGetSingle` prima di uscire in rete. Una
verifica che risponde dalla memoria di stamattina non è una verifica.

→ L'adapter nuovo non passa da nessuna cache.

## §3 — Le decisioni

**P1 — Il verdetto ha tre valori, non due.**

| verdetto | che cos'è | che cosa fa |
|---|---|---|
| **Assente** | la sorgente ha risposto, ha nominato altro, e questo non c'è | scioglie **la sola D8** |
| **Presente** | la sorgente lo manda ancora | non scioglie niente, e **chiude la questione subito** |
| **Non si sa** | errore, timeout, credenziali mancanti, elenco vuoto, corpo illeggibile | non scioglie niente, e dice perché |

Il terzo non è un dettaglio implementativo: è ciò che rende il meccanismo utilizzabile per autorizzare una
cancellazione. Senza, «non lo so» si travestirebbe da «non c'è».

**P2 — Due chiamate, non una.** Ogni «assente» poggia sulla **puntuale** (il dettaglio dell'elemento) e su
una **controprova** (l'elenco del contenitore). Dove l'elenco contiene anche la risposta diretta — i
subcenter di un ACC, le postazioni di un aeroporto — la controprova *è* la prova: «ecco i sette che ho, e
questo non c'è». Dove l'elenco è paginato e non lo si può scorrere tutto (gli aeroporti di un paese), la
controprova è di **vitalità**: dimostra che l'endpoint risponde e che il token ha lo scope, cioè che il 404
di prima è un «non ce l'ho» e non un «non ti conosco».

**P3 — Se le due risposte litigano, vince la prudenza.** Dettaglio 404 ma l'elenco lo nomina → **presente**.
Due risposte in disaccordo non sono una prova d'assenza, e davanti a un dubbio non si cancella.

**P4 — Scioglie D8 e nient'altro.** La sorgente ha voce sulla **sua** anagrafica, non sulle nostre scelte
editoriali. Un accordo di coordinamento, un documento all'ultimo aggancio, una torre senza il suo scalo, una
ACC ancora piena: restano dove sono anche quando IVAO giura che il settore non esiste più. Non è cautela in
più — è la divisione di competenza fra chi possiede il dato e chi possiede il documento.

**P5 — La prova si rifà al momento del `DELETE`.** Il verdetto mostrato nella finestra autorizza il
**tasto**, non la cancellazione: fra i due c'è il tempo che l'utente impiega a leggere, e in quel tempo un
import può aver rimesso in archivio ciò che la sorgente aveva appena smesso di mandare. Quindi
`EliminaAsync` prende un **ordine di chiedere**, non un verdetto — chi chiama non può passare una risposta
già presa. È la stessa ragione per cui il piano si ricalcola invece di fidarsi di quello mostrato.

**P6 — Niente stato persistente.** Valutata e scartata l'alternativa: una tabellina
`(tipo, chiave, assente-alle, chi, cosa ha risposto)` che sopravvive al ricarico e si auto-invalida quando
un import ritimbra la riga. Costa una tabella e una migrazione, e soprattutto reintroduce ciò che P5
elimina — un verdetto vecchio di cui fidarsi. Ci si torna solo se la pagina «Da sistemare» dovrà **mostrare**
quali righe la sorgente ha già smentito.

**P7 — Il tasto compare solo quando D8 blocca**, e mai in automatico all'apertura. La Struttura elenca 320
settori: una verifica a ogni finestra aperta sarebbe una raffica di chiamate a IVAO per niente.

## §4 — Gli endpoint, che c'erano già

| bersaglio | puntuale | controprova | dove sta il path |
|---|---|---|---|
| settore di ACC | `/v2/subcenters/{callsign}` | `/v2/centers/{ACC}/subcenters` | `IvaoOptions.cs:41,44` |
| postazione di scalo | `/v2/ATCPositions/{callsign}` | `/v2/airports/{ICAO}/ATCPositions` | `IvaoOptions.cs:47` |
| aeroporto | `/v2/airports/{ICAO}` | `/v2/airports?page=1&countryId=IT` (vitalità) | `IvaoOptions.cs:35` |
| ACC | — | `/v2/centers?countryId=IT`, che è insieme domanda e controprova | `IvaoOptions.cs:38` |

**Chi non si chiede, e perché.** Documenti, candidati confinanti e **aree regolamentate** non hanno D8 fra i
loro blocchi (`DeletionRules.PerArea` non ne ha nessuno): una porta che sa chiederne l'esistenza non
sbloccherebbe niente, e sarebbe codice che nessuno esercita. Un documento è **nostro**: nessuna sorgente lo
rivendica.

**L'ACC per l'elenco e non per il dettaglio**: `/v2/centers/{ICAO}` non esiste fra i path configurati, ma
`IAccDirectory.GetCentersByCountryAsync` scorre già tutte le pagine **e lancia** sia sull'errore HTTP sia
sull'elenco vuoto (`IvaoAccClient.cs:47,88`) — che è precisamente la distinzione che serve qui. Riusarla
evita di riscrivere la paginazione, e il «lancia» diventa «non si sa».

## §5 — Che cosa si vede

Nella finestra di eliminazione, **sotto** l'elenco dei blocchi e solo se fra loro c'è quello della sorgente:

```
Non si può eliminare
  • LIRR_W_CTR non si può eliminare: la sorgente la manda ancora (vista l'ultima volta il 2026-08-26 10:12Z)

[ Chiedi alla sorgente adesso ]  invece di aspettare due giri d'import
```

e dopo il clic, un riquadro col verdetto — verde, rosso o giallo secondo i tre valori:

```
La sorgente non ce l'ha più
LIRR_W_CTR non c'è più: LIRR ne elenca 7 e questo non è fra loro
```

Il tasto «Elimina» si riabilita solo col verde. Il riquadro sta **fuori** dal callout dei blocchi: quello è
`display:flex`, e una riga in più dentro sarebbe diventata una terza colonna accanto al titolo e all'elenco.

## §6 — Il registro

Un'eliminazione autorizzata da una domanda scrive nell'audit **anche le tracce della domanda**:

```json
{"Callsign":"LIRR_W_CTR","Name":"Roma Ovest","Type":"Ctr","Kind":"Acc","AirportIcao":null,
 "ProvaSorgente":"GET /v2/subcenters/LIRR_W_CTR → 404; GET /v2/centers/LIRR/subcenters → 200, 7 elementi"}
```

Senza, il registro mostrerebbe fra sei mesi una cancellazione che le protezioni vietavano, e nessun modo di
sapere perché è passata. ⚠️ Il campo compare **solo** quando c'è stata una domanda: il dettaglio si scrive
in due forme, non in una con un campo a `null` — quel registro lo si legge anche in SQL, di fretta, davanti
a un incidente.

## §7 — I pezzi

| pezzo | dove | che cos'è |
|---|---|---|
| la porta, il verdetto a tre valori, il null-object | `Vipi.Application/Abstractions/ISourcePresenceProbe.cs` | **nuovo** |
| l'adapter IVAO, status-aware, senza cache | `Vipi.Infrastructure/Ivao/IvaoSourcePresenceProbe.cs` | **nuovo** |
| `provaDiAssenza` nella soglia | `Vipi.Application/Content/SogliaEliminazione.cs` | parametro, default `false` |
| `DallaSorgente` sul blocco, `LaSorgenteTrattiene` sul piano | `Vipi.Application/Content/DeletionRules.cs` | la finestra riconosce D8 dal **piano**, non dalla frase |
| `VerificaAllaSorgenteAsync`, l'ordine di richiedere | `Vipi.Application/Content/DeletionService.cs` | + la traduzione bersaglio → indirizzo nella sorgente |
| le tracce nell'audit | `Vipi.Infrastructure/Persistence/EfDeletionRepository.cs` | `ApplyAsync(…, provaSorgente, …)` |
| il tasto e il riquadro | `Vipi.Ui/Components/DeleteDialog.razor` + `vipi-theme.css` | 5 chiavi × 2 lingue |

**Perché una porta nuova e non un allargamento** (pre-flight §1, «estendi o sostituisci, mai affiancare»):
non è un quarto client anagrafico né un secondo modo di leggere la stessa cosa. Le porte esistenti
rispondono «che cos'è questo elemento»; questa risponde «c'è ancora, e sei sicuro?» — e la seconda domanda
richiede di guardare lo status, che la prima ha deliberatamente buttato via. `SourceProbeTarget` non è un
gemello di `DeletionTarget` per lo stesso motivo: uno indirizza il **nostro** archivio (Id, chiavi di
catalogo), l'altro ciò che **la sorgente** espone (callsign, e il contenitore come pezzo dell'URL).

**Dispatch** (pre-flight §2): lo `switch` sul tipo di bersaglio nella sorgente sta in **un solo** posto —
l'adapter. Un registry sarebbe over-engineering per quattro casi stabili.

## §8 — La prova

**Test** — 50 nuovi, tutti verdi (net8 **2586**, net10 **2348**; Release, 0 avvisi sui due TFM):

- `SourcePresenceProbeTests` (21, `Vipi.Infrastructure.Tests`) — il filo. Fissa che 401/403/429/502/503 sono
  **«non si sa»** e mai «assente»; che un elenco vuoto non prova niente; che due risposte in disaccordo
  danno «presente»; che una rete caduta è un verdetto e non un'eccezione.
- `DeletionRulesTests` (+6) — la prova scioglie D8 e **nient'altro**: accordo, ultimo aggancio, torre e ACC
  piena reggono anche col verdetto in mano.
- `SogliaEliminazioneTests` (+4) — la constatazione batte la deduzione, e vale anche senza nessuna storia.
- `DeletionProbeTests` (12, `Vipi.Application.Tests`) — a chi si chiede e con quale indirizzo (subcenter
  sotto l'ACC, postazioni sotto l'ICAO); che a una riga manuale non si chieda niente; e **P5**: la domanda
  si rifà al `DELETE`, e se la sorgente cambia idea non si cancella.
- `DeleteDialogSourceTests` (7, `Vipi.Ui.Tests`) — il tasto compare solo col blocco della sorgente; il
  verdetto si legge; confermando si passa l'**ordine di richiedere**, non il verdetto.

**A schermo** — §8 bis qui sotto.

## §8 bis — Verificato a schermo, contro IVAO vero

Guidata la Struttura in locale (Edge + puppeteer-core, copia del `vipi.db` reale, credenziali IVAO dei
user-secrets: **le chiamate sono uscite davvero**). Nessun errore di console, nessun 4xx, nessuna chiave
`Del_*` non tradotta, nessuna espressione Razor rimasta letterale.

**Il ramo che non sblocca.** `LIBB_ES_CTR`, bloccato da D8 («interrogata con successo meno di due volte») e
da sette accordi di coordinamento. Il tasto compare, la risposta arriva — *«The source still has it —
LIBB_ES_CTR c'è ancora: la sorgente lo manda»* — e «Elimina» **resta spento**: il verdetto non tocca gli
accordi (P4), e questo è il caso in cui oggi si sarebbero aspettati due giri per scoprire un nulla di fatto.

**Il ramo che sblocca.** Interrogati **tutti e nove** gli orfani della Struttura, uno per uno:

| orfano | verdetto della sorgente | «Elimina» |
|---|---|---|
| LIBB_EU_CTR, LIRO_CRC_CTR, LIVK_CRC_CTR, LIVK_RCC_CTR, LIZZ_AAR/AEW/JTA/NVY_CTR (8) | c'è ancora | spento |
| **LIED_G_APP** | *«non c'è più: LIED ne elenca 3 e questo non è fra loro»* | **acceso** |

⚠️ **E il rilievo che ne esce vale più del tasto.** Otto orfani su nove IVAO li **manda ancora**: sono
orfani perché qualcuno li ha nascosti nel *nostro* catalogo, non perché la sorgente li abbia tolti. Fino a
oggi quella distinzione non era leggibile da nessuna parte — la sezione «Orfani» li mostra tutti uguali — e
per farla servivano due giri d'import. Uno solo, `LIED_G_APP` (Decimo Precision), è sparito davvero: la
controprova ha risposto `200` con le tre postazioni superstiti di LIED, che è esattamente la forma di prova
che §3/P2 pretende.

**Una nota di lingua**: il titolo del verdetto è tradotto, la frase no («The source no longer has it» +
«LIED_G_APP non c'è più: …»). È come si comporta già tutto il resto della finestra — le frasi del piano
nascono in italiano dentro `DeletionRules`, e solo le intestazioni passano dal `.resx`. Non è una
regressione di questo giro, ma è il posto dove si vedrà se un giorno si decide di tradurre i piani.

## §9 — Che cosa resta

- **L'azione di gruppo sugli aeroporti** (`AeroportiPage.razor:619`) elimina in blocco senza offrire la
  domanda: chi la usa passa dalla regola dei due giri come prima. Voluto — una raffica di verifiche
  puntuali su N scali è esattamente la raffica che §3/P7 evita — ma va detto, perché il tasto singolo e
  quello di gruppo si comportano diversamente sullo stesso oggetto.
- **La pagina «Da sistemare» non mostra i verdetti**: per farlo servirebbe P6, cioè lo stato persistente.
  Si valuta solo se il committente chiede di *vedere in elenco* che cosa la sorgente ha già smentito.
- **Nessuna strozzatura sui clic**: il tasto è manuale, admin-only, un elemento per volta, e ogni clic sono
  al massimo due chiamate. Se un giorno diventasse un'azione ripetuta, servirà un intervallo minimo per
  chiave.
