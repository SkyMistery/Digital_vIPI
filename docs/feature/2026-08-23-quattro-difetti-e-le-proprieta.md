# Quattro difetti chiusi, e le proprietà dell'AoR

**23 agosto 2026 — ✅ chiuso, verifica live eseguita.**

Giro di chiusura su voci che stavano in `lavori-aperti.md` da giorni: i **due difetti** annotati mentre si
chiudeva la topbar, il **test ballerino** del bridge Aurora, la **decisione** sui codici admin, più la voce
🟢 dei **test property-based** sull'AoR. Nessuna rotta nuova, nessuna migrazione: la coda ferma per il
cutover MariaDB non si allunga.

Il filo che li lega non era previsto: **tre difetti su quattro erano stati diagnosticati male**, e in tutti e
tre i casi la diagnosi scritta costava meno di quella misurata. È la parte che vale la pena ricordare.

---

## 1. Admin: lo staff di divisione è admin, tutto

**Decisione del committente** del **22 agosto (sera)**, non tecnica: la voce E4 la chiedeva dal 9 agosto.

I codici veri visti ai login erano otto; l'elenco puntuale in `Division:AdminRolePatterns`
(`DIR`, `ADIR`, `WM`, `AWM`, `AOC`, `AOAC`, `AOA\d+`) ne copriva quattro e ne lasciava fuori quattro —
`IT-SOC`, `IT-T01`, `IT-FOC`, `IT-FOAC`. Il default diventa il **jolly** `[A-Z0-9]+`, cioè `^IT-[A-Z0-9]+$`.

Il punto non sono i quattro codici: è che **ogni ruolo nuovo della divisione nasceva escluso**, in silenzio, e
se ne accorgeva solo chi restava fuori — che per definizione non può rimediare da dentro, perché distribuire
i permessi richiede di essere admin.

⚠️ **Il jolly non allarga oltre la divisione.** Un codice `{Code}-{ruolo}` lo assegna il portale IVAO solo al
proprio staff, e il prefisso resta la barriera: `DE-DIR` non è admin qui. Il lato **chief ACC**
(`^LI[A-Z0-9]+-CH$`) non cambia e resta **l'unica ipotesi non verificata**: un codice del genere non è mai
comparso in un login vero.

⚠️ **Il rilievo «nessun admin fra gli staffisti conosciuti» ora suona molto più di rado**, ed è voluto: con un
jolly, per non avere nessun admin i codici devono essere *malformati* o di un'altra divisione — cioè il
guasto vero, non una lista incompleta. I test lo dicono esplicitamente (`ITQUALCOSA`, non `IT-QUALCOSA`).

Toccati: `DivisionOptions`, `AdminStaffCodes`, `AdminCoverageService` (il commento che chiamava «ipotesi»
anche il lato divisione), `docs/guide/config.md`, ADR-0004, `HANDOFF.md` §6.

---

## 2. Le tabelle del viewer a zoom alto — e il colpevole era un altro

La voce del 22 agosto diceva: *«il colpevole è `table.sid-table` col suo `min-width:720px`»*. **Rimisurato: no.**
La tabella SID sta già dentro un `<div style="overflow:auto">` e a zoom 1.4 scorre correttamente
(820 unità di contenuto in 497 di contenitore, nessuno sforo).

Misura vera, viewer aeroporto LIBD, finestra 1280 con zoom 1.4 — **914 unità di layout**:

| Elemento | Serve | Ha | Contenitore |
|---|---|---|---|
| `table.rwy-table` | 570 | 497 | `.cb-body` (`overflow:visible`) |
| `details.block.cb#a-piste` | 594 | 545 | colonna di `.apt-2col` |
| `div.apt-2col` | 626 | 547 | corpo del documento |
| `div.wrap` | **949** | **914** | → **35 unità di sforo**, 48px a schermo |

Il colpevole è `.rwy-table`, che **non dichiara nessun minimo**: ne pretende 570 per il proprio contenuto. È
il caso già scritto nel commento del foglio («`.rwy-table` non dichiara niente e pretende 542 per il suo
CONTENUTO») — ma la cura di allora viveva dentro `@media (max-width:900px)`.

### ⚠️ Perché la media query non poteva funzionare, e vale oltre questo caso

Lo zoom di questa applicazione **non è quello del browser**: è `document.documentElement.style.zoom`, scritto
da `vipi-zoom.js`. Le media query **non lo vedono**: valutano la finestra (1280) mentre il layout ha 914. Una
soglia di viewport è **cieca allo zoom** — è la stessa diagnosi che aveva portato la topbar a misurarsi da
sola, e la conclusione è la stessa: **la soglia non si sposta, si toglie.**

Tre regole, nessun numero magico:

```css
.wrap *:has(> table):not(.st-scroll){overflow-x:auto;min-width:0}
.wrap .acc-block-h,.wrap h2,.wrap h3{overflow-wrap:anywhere}
.apt-2col{grid-template-columns:repeat(2,minmax(0,1fr))}  /* + .apt-2col>*{min-width:0} */
```

- **Il contenitore diretto di una tabella scorre sempre.** Quando la tabella ci sta — cioè quasi sempre — non
  cambia niente e non compare nessuna barra.
- **`minmax(0,1fr)` da solo non basta**: azzera il minimo della *traccia*, non quello dell'*elemento*. Senza
  `min-width:0` sui figli, il pavimento resta il min-content del blocco e la riga sfonda comunque.
- **L'`overflow-wrap` dei titoli esce dalla media query**: il minimo di un titolo è la sua parola più lunga,
  che in unità di layout non si accorcia mai. A zoom 1.8 «Livelli di transizione» ne pretende 177 in una
  colonna che ne ha 82 — ed era l'ultimo sforo rimasto.
- La regola a 900px **resta**: sotto quella soglia la cura del telefono è più larga (minimi azzerati,
  `overflow-wrap` sulle celle, indice a `34vh`) ed era stata verificata a 375/390. Il giro è **additivo**.

### Verifica

144 combinazioni guidate con Edge (6 pagine × 6 larghezze × 4 zoom), `scrollWidth − clientWidth` su ognuna:
il **viewer aeroporto passa da 48px di sforo a 0 su tutti gli zoom fino a 1.8**, e a zoom 1 la pagina è
identica a prima (screenshot confrontati, due colonne al loro posto).

⚠️ **Misurare sotto zoom**: `scrollWidth`/`clientWidth` stanno in unità di layout, `getBoundingClientRect()` e
`innerWidth` in pixel di finestra. Mescolarli dà tabelle di numeri che non tornano — è successo al primo giro
di misura, e ha fatto sembrare colpevole la topbar.

---

## 3. La lingua non arrivava al circuito

Con browser in inglese, `/services/vsop?culture=it` scriveva «Documentazione operativa» nel prerender e
subito dopo il circuito `InteractiveServer` ridisegnava **«Operational documentation»**.

**Perché.** In Blazor Server le richieste sono **due**: il documento, che porta `?culture=it` e vince con la
stringa di query, e la connessione **`/_blazor`** che apre il circuito, che quella stringa non ce l'ha e
ricade su `Accept-Language`. Il circuito nasce con quella cultura e la tiene per tutta la vita. Il chrome
resta giusto perché è SSR statico — ed è per questo che non se n'era accorto nessuno.

**Cura.** `CultureCookieMiddleware` (in `Vipi.Hosting`, montato **subito dopo** `UseRequestLocalization`)
scrive il cookie standard di `CookieRequestCultureProvider`, leggendo la cultura **già risolta** da
`IRequestCultureFeature` invece di rifare il parse: cookie e pagina non possono divergere.

⚠️ **Solo su richiesta esplicita** (`?culture=` / `?ui-culture=`). Scrivere il cookie anche quando la lingua
arriva da `Accept-Language` congelerebbe per un anno una scelta che l'utente non ha mai fatto, e cambiare
lingua al browser non avrebbe più effetto. Due test E2E, uno per verso.

Verificato guidando Edge con `Accept-Language: en-US`: prima `it → en` fra prerender e circuito, ora `it` in
tutti e due; senza `?culture=` resta inglese e **nessun cookie** viene scritto.

---

## 4. Il test ballerino del bridge non era un test ballerino

`AuroraClientTests.Richieste_in_sequenza_non_si_mescolano` falliva solo nella corsa completa, con «Nessuna
risposta a #TRPOS entro 15000 ms», e passava da solo in 65 ms. Inseguito dall'11 al 22 agosto come problema
di **tempi** — «il thread-pool sotto carico», «la prova dipende dai tempi del socket» — e due volte la cura
proposta è stata allargare l'attesa.

**Era una corsa dentro il client.** `SendAsync` si connetteva **prima** di prendere il turno:

1. due invii lanciati insieme trovavano entrambi «non connesso» — l'assegnazione avviene *dopo*
   `ConnectAsync`, che cede il controllo — e aprivano **un socket a testa**; il secondo, nascendo, chiudeva
   il primo;
2. peggio: `stream` e canale delle righe si leggevano in **due istruzioni separate**, quindi un invio poteva
   scrivere su un socket e aspettare la risposta sul canale **dell'altro**. Nessuna delle due arrivava a
   destinazione: **silenzio fino alla scadenza** — cioè esattamente l'errore osservato, che sembrava lentezza
   e non lo era.

Ora la connessione è **un oggetto solo** (`Connessione`: socket + flusso + canale + ciclo di lettura), letto
in un colpo, e si apre **dentro** il turno; `EnsureConnectedAsync` pubblica prende il turno a sua volta.

**Visto fallire e visto passare.** Col client di prima: **200 giri su 200** aprivano due connessioni, e la
prova ci metteva **3 minuti e 10** (ogni giro pagava una scadenza intera). Col client nuovo: **133 ms**.

⚠️ **Il test lo vede solo se i due invii partono davvero insieme** — due thread e un cancelletto che li
rilascia insieme. Chiamati in sequenza sullo stesso thread, su loopback la prima connessione fa in tempo a
stabilirsi e il secondo invio la trova pronta: **il difetto sparisce, e il test passa col codice rotto.** È
esattamente il motivo per cui per undici giorni è sembrato un problema di calendario.

---

## 5. Le proprietà dell'AoR (voce 🟢 di E5)

La geometria dell'AoR è **l'unico punto del prodotto dove il dominio è continuo**: un poligono è una lista di
coppie di reali, e qualunque elenco di esempi ne copre una fetta arbitraria — di solito quella a cui pensava
chi ha scritto il codice. Sei proprietà con **CsCheck 4.8** (pacchetto senza dipendenze, MIT):

| Proprietà | Che cosa protegge |
|---|---|
| Nessun punto disegnato esce dal riquadro | un punto fuori dal viewBox non dà errore: **sparisce** |
| Il lato lungo è sempre 400 | la scala resta **uniforme**, cioè le forme non si deformano |
| Spostare tutte le longitudini non cambia il disegno | la forma non dipende da dove sta sul meridiano |
| Il rapporto fra i lati è quello dell'estensione proiettata | «il settore ha la forma giusta», provato senza guardare |
| `ProjectShared` di un poligono solo == `Project` | due funzioni che devono accordarsi, o la stessa AoR esce con due scale diverse |
| Meno di tre punti ⇒ `null` | la UI ci conta per mostrare il segnaposto |

⚠️ **E hanno trovato subito un record che diceva il falso.** Il commento di `AorPolygonProjector` dichiarava
coppie `[lat,lon]`; il formato IVAO `regionMapPolygon` mette la **longitudine prima** (lo fa
`PolygonGeometry.ParsePoints`, e i test esistenti lo sapevano). Chi ne avesse ricavato una fixture avrebbe
scritto un poligono **ruotato di 90°** — e la proiezione non se ne lamenta: **disegna**. Commento corretto,
con l'indicazione di dove sta la verità.

⚠️ **Sono test non deterministici per costruzione**, e va saputo: i casi cambiano a ogni giro, quindi un rosso
può comparire su un codice fermo da settimane. **Non è un test da rilanciare finché passa** — è un
controesempio che prima non era stato pescato. Il seed sta nel messaggio (`-e CsCheck_Seed=…`), si riproduce
esatto e si congela in un test a esempio accanto agli altri in `AorPolygonProjectorTests`.

---

## 6. Cancello

- `dotnet build Vipi.slnx -c Release --no-incremental` → **0 avvisi**, due TFM.
- `dotnet test Vipi.slnx -c Release` → **3325 verdi** su net8 **e** net10, E2E compresi.
- ⚠️ Suite e build eseguite in **Release**: i `bin/Debug` erano bloccati dall'app di sviluppo in esecuzione
  (`Vipi.Host` in ascolto sul 5034). Stessa trappola della corsa precedente, stessa uscita.
- Verifica live su una **copia** del `vipi.db` (mai quello di progetto), app pubblicata in scratchpad e
  guidata con Edge + puppeteer-core: vedi la skill `verifica-live`.

## 7. Resta aperto

- **`BANA5Z` di LIBD (pista 25) ha `InitialClimb = "500"`** → resa «500 ft», mentre tutte le altre BANAV
  stanno a 5000/9000. **Decisione editoriale**, non di codice. Nello stesso giro è emerso che **`BANA8A` —
  la SID che tutti i documenti indicavano come sbagliata — è già a `9000`** nel `vipi.db` di sviluppo: va
  rifatta **in produzione**, dove nessuno l'ha guardata. ⚠️ Il valore **non arriva dal sectorfile**
  (`libd.sid` non porta la quota iniziale): lo scrivono a mano gli editori, quindi nessun import lo
  ricontrolla e nessun import lo sovrascrive.
- **390px con zoom ≥ 1.25** (elenco aeroporti, landing, ricerca, «cosa è cambiato»): sforano, ed è
  **preesistente** — misurato identico prima e dopo questo giro. Là il layout ha 312 unità o meno, sotto il
  pavimento dichiarato di 375 in `docs/design/regole-ui-pagine-admin.md`.
- **Il blocco al deploy non è cambiato**: la MariaDB di produzione va convertita agli accordi a sezioni
  (E6-bis §9), o `AgreementSectionsFinalize` fallisce all'avvio.
