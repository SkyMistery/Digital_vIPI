# Audit prestazioni — 27 agosto 2026, sera

**Ramo:** `prestazioni` (da `riordino-e-aree`) · **Stato:** ✅ **eseguito lo stesso giorno**, dieci commit,
**fuso in `main` `8e5f640`** insieme a `riordino-e-aree`; entrambi i rami cancellati, locale e su origin.
Punto di ritorno: `main-prima-del-merge-20260827` (`963e9aa`). Suite **5746 → 5981**, build Release della
soluzione intera **0 avvisi**, **nessuna migrazione nuova**.

Revisione della **responsività e del tempo di risposta del sito tenendo conto dell'ambiente di produzione**
— Plesk + Passenger, una sola istanza senza backplane, MariaDB sulla stessa macchina, Cloudflare davanti,
aggiornamento **via FTP** da una persona.

**Metodo.** Nessuna lettura a occhio: l'applicazione è stata **compilata in Release, avviata su una copia
del `vipi.db` reale e cronometrata** — pagine col client HTTP, query contate dal log di EF, byte contati con
le intestazioni di un browser vero, mappe verificate in Edge. Ogni intervento è un commit con la sua misura
prima/dopo e i suoi test. Dove la misura ha **ribaltato** l'ipotesi, l'intervento non è stato fatto e la
misura è finita **nel codice**.

---

## Esito in una riga

Lo **stato stazionario era già sano** — trenta richieste concorrenti sulla vIPI ACC pubblica: p50 16 ms, p90
34 ms, con un database piccolo (320 settori, 93 aeroporti, 18 documenti) e gli indici al loro posto. Il costo
stava **altrove**: nei byte spediti, nell'avvio e in ciò che impediva a qualunque cache di aiutare.

    prima visita   336 192  ->  113 052 byte     -66%
    avvio            465    ->      153 query,  e zero UPDATE inutili

> ⚠️ **Il filo che lega i difetti: quattro su otto sono default del framework mai scritti.** Il livello di
> compressione (`Fastest`, che per Brotli è la qualità 1), il livello di log (`Information`, che per EF
> significa il testo di ogni query su disco), il `@rendermode` messo su una pagina che non ha comandi, e un
> `DateTime.UtcNow` scritto per abitudine dove serviva il timbro della sorgente. Nessuno di questi somiglia
> a un difetto: non danno errore, e tre su quattro rendono la configurazione *più* ricca a leggerla.

---

## Riepilogo

| # | Voce | Esito | Commit |
|---|---|---|---|
| P1 | **Brotli faceva PEGGIO di gzip**: livelli lasciati al default | ✅ CSS 120 601 → 85 051 · HTML −48% | `965cbad` |
| P2 | L'**SQL di ogni query** finiva nei log di produzione | ✅ 1 MB → 2,6 KB ogni 210 pagine | `ea98d7f` |
| P3 | Il **44% dei byte di CSS/JS erano commenti**; nessuna minificazione | ✅ asset per pagina 274 031 → 102 845 | `16f7b0e` |
| P4 | **ReadyToRun** per l'avvio a freddo | ❌ **scartato su misura** · + cronometro d'avvio | `fca8a44` |
| P5 | Circuiti Blazor aperti da pagine **senza un comando** | ✅ hub e anteprima release resi statici | `4e95a5c` |
| P6 | **312 UPDATE a ogni avvio** — e il timbro d'import mentiva | ✅ 465 → 153 query, 0 UPDATE | `52d014e` |
| P7 | Le letture pubbliche **non le poteva tenere nessuna cache** | ✅ `public, max-age=60`, niente cookie | `55a56dc` |
| P8 | Quattro moduli JS pesanti su **ogni** pagina | ✅ −13 029 byte compressi per pagina | `e9d27a1` |
| P9 | **N+1**: l'elenco aeroporti, otto query per scalo | ✅ 36 → 15, e ora costante | `0b0216d` |
| P10 | I poligoni AoR spediti **due volte** | ❌ **scartato su misura** | `6965181` |

---

## P1 — Attivare Brotli faceva scaricare più byte che non averlo

I due provider erano registrati, l'header `Content-Encoding: br` arrivava, nessun errore da nessuna parte. Ma
il default di ASP.NET per **entrambi** è `CompressionLevel.Fastest`, che per Brotli vuol dire **qualità 1** —
il livello più basso che il formato ha. E Brotli è registrato per primo, quindi vince la negoziazione con
ogni browser moderno.

```
                 grezzo     servito (br)   gzip
vipi-theme.css   295 571      120 601     101 217      <- br PEGGIO di gzip
HTML vIPI ACC    294 776       62 161      50 018      <- br PEGGIO di gzip
```

Cioè: **~24% di byte in più** di quanti se ne sarebbero scaricati lasciando solo gzip.

Cura: i livelli si scrivono (`VipiStartup`). `Optimal` e non `SmallestSize`: la qualità 11 su 300 KB costa
centinaia di millisecondi di CPU **a ogni richiesta** — quella si paga a build, ed è P3.

⚠️ Sui JavaScript già densi la qualità 4 resta un capello sopra gzip-6 (1,3% su `vipi-ui.js`). Il test lo
tollera al 2%, che è un decimo dello scarto del difetto che chiude.

## P2 — In produzione EF scriveva il testo di ogni query su disco

`appsettings.Production.json` non ha una sezione `Logging`, quindi valeva quella del file base — dove c'era
`Default: Information` e **la categoria di EF non era nominata**. Il difetto stava nel posto in cui non si
guarda.

Stesso binario, stesso database, 210 aperture di una pagina da 174 query:

```
Warning           2 614 byte di log
Information   1 036 781 byte di log        quattrocento volte tanto
```

⚠️ **Una prima misura diceva anche «+45% sul tempo di risposta». Rimisurata alternando i due processi,
l'ordine si inverte da un giro all'altro: era rumore della macchina.** Il commento nel codice ora lo dice.
Questa modifica vale per il disco e l'I/O, non per i millisecondi.

Il rumore si toglie **nominando la categoria**, non alzando `Default`: le nostre righe (import, manutenzioni
d'avvio) restano a `Information`, e su un host dove i log del processo non li legge nessuno sono l'unico
racconto che resta. In sviluppo l'SQL torna visibile, perché è lì che contarne le query è il modo normale di
accorgersi di un N+1.

## P3 — Il 44% dei byte di CSS e JavaScript erano commenti

Contati: **218 905 su 500 367**. Non è un difetto dei commenti, che in questo codice sono la parte migliore:
è che appartengono al **sorgente**, e finivano nel browser perché fra i due non c'era nessun passaggio. Il
solo `vipi-theme.css` era 293 KB di cui 134 di commento.

Ora c'è: **`tools/Vipi.Assets`**, chiamato dal publish di `Vipi.Host`, toglie commenti e spazi e lascia
accanto a ogni file di testo la variante `.br` e `.gz` già compressa alla **qualità 11** — quella che a
richiesta non ci si può permettere. `AssetPrecompressi` la consegna al posto della compressione al volo.

```
_content/Vipi.Ui/vipi-theme.css   120 601 -> 22 664     -81%
_content/Vipi.Ui/vipi-ui.js        23 236 ->  4 909     -79%
TOTALE asset per pagina           274 031 -> 102 845    -62%
```

Ciò che resta è quasi tutto `blazor.web.js` (53 452 B): **non è un file fisico in `wwwroot`**, lo serve il
middleware di Blazor, e la precompressione non lo raggiunge.

**Tre scelte, con il perché:**

- **NUglify e non un giro di espressioni regolari.** Sul JavaScript un `//` dentro una stringa o dentro
  un'espressione regolare letterale (`/https?:\/\//`) diventa «da qui è commento», e il file esce
  sintatticamente valido con dentro un'altra cosa.
- **NUglify e non esbuild/terser**: la CI non ha Node e chi costruisce il pacchetto lo fa con
  `dotnet publish`. Una toolchain in più è una ragione in più per cui il pacchetto non si costruisce.
- ⚠️ **NON si rinominano le variabili locali.** È la trasformazione che rende di più ed è anche l'unica che
  può cambiare il comportamento di un programma corretto. Misurata su questi file: **3 524 byte su 57 920**,
  il 2%. Non è un prezzo che valga un guasto visibile solo in produzione, mesi dopo.

⚠️ **Il publish si FERMA se un file non è minificabile.** JavaScript e CSS non li compila nessuno, quindi un
errore di sintassi lì dentro non lo vede nessun altro passo della build: il primo a incontrarlo sarebbe chi
apre la pagina.

⚠️ **Qui si aggiorna via FTP, file per file.** Caricare un `.css` nuovo lasciando il `.br` vecchio avrebbe
servito il contenuto **vecchio** a tutti, per sempre, senza un errore da nessuna parte. Invece di scriverlo
in un avviso: l'applicazione **confronta le date** e ignora la variante stantia, tornando alla compressione
al volo. Qualche byte, invece di un guasto muto.

## P4 ❌ — ReadyToRun non paga, e la misura dice perché

Sembrava la riga ovvia da scrivere: Passenger spegne il processo per inattività, quindi il primo visitatore
dopo la pausa paga l'avvio intero. Provato, **sei avvii per parte**, stessa macchina e stesso database:

```
senza ReadyToRun   avvio 1 656 ms (mediana)   otto pagine a freddo 1 732 ms
con   ReadyToRun   avvio 1 621 ms (mediana)   otto pagine a freddo 1 503 ms
pacchetto           122 MB   ->   151 MB
```

Il 2% sull'avvio è dentro il rumore, e il **minimo assoluto** delle due configurazioni sulle otto pagine è lo
stesso (1 287 contro 1 280 ms). In cambio il pacchetto cresce di **29 MB** su un deploy che va **solo via
FTP**: costo certo, guadagno incerto.

Al suo posto è entrata la cosa che mancava davvero — **sapere dove va il tempo**. Un cronometro delle fasi
(`StartupDiagnostics.CronometroAvvio`) scrive in coda a `diagnostica/avvio-diagnostica.txt`:

```
CreateBuilder ................     46 ms
registrazioni dei servizi ....     41 ms
builder.Build ................     15 ms
migrazione del database ......    537 ms   <---
manutenzioni d'avvio .........    621 ms   <---
resto della pipeline .........     26 ms
TOTALE .......................  1 286 ms
```

**Millecento millisecondi su milletrecento sono DATABASE, non compilazione**: esattamente il lavoro che
ReadyToRun non può toccare. Il commento in `Vipi.Host.csproj` dice che l'assenza di `PublishReadyToRun` è una
scelta e non una dimenticanza, coi numeri accanto — è l'unica cosa che impedisce di riprovarlo fra sei mesi
partendo dalla stessa ipotesi.

## P5 — L'ingresso del sito apriva un circuito per ogni visitatore

`/services/vsop` dichiarava `@rendermode InteractiveServer` e **non ha un solo comando dentro**: conta gli
ATC una volta al render e basta. Ogni visitatore apriva un WebSocket e uno stato lato server per niente — su
una sola istanza, senza backplane, con venticinque circuiti trattenuti in tutto.

Stessa cosa per `/services/vsop/release/{id}`, che è un **redirect**: senza circuito, `NavigateTo` torna a
essere una risposta **302** che il browser segue prima di disegnare qualunque cosa.

⚠️ **Correzione all'analisi.** Avevo elencato anche `ChangedPage` fra le pagine «senza comandi»: è **falso**.
I suoi due filtri sono `<Chip OnActivate="…">`, cioè callback su un componente **figlio**, che un grep di
`@onclick` sul solo file di pagina non vede. Il circuito lì serve e resta; lo stesso per `VloaEditorPage`,
che ospita `<VloaEditor>`.

Quell'errore è diventato **la forma del test**: `CircuitiGiustificatiTests` controlla ogni pagina interattiva
e **scende di un livello** nei componenti che usa, perché un controllo che non scendesse direbbe di togliere
il circuito a una pagina che ne ha bisogno — più danno del difetto che cerca.

## P6 — Il timbro d'import di un settore era l'ora del riavvio

La proiezione dei settori scriveva `sector.ImportedAtUtc = DateTime.UtcNow` su ogni riga. Gira a **ogni
avvio**, quindi EF marcava come modificati tutti i settori: **312 UPDATE su 465 query d'avvio**, ogni volta,
senza che nulla fosse cambiato.

Ma il costo non è la parte peggiore. Quel campo lo interroga la **regola D8** delle eliminazioni — «la
sorgente lo manda ancora?» — e con `UtcNow` la risposta era «sì, perché abbiamo riavviato»: un settore
sparito dalla sorgente a luglio tornava fresco a ogni riavvio. `EfDeletionRepository` lo sapeva già e ci
girava intorno; il suo commento dice, testuale, che quel timbro «dice quando è nato lo specchio, non quando
la sorgente ha parlato l'ultima volta».

⚠️ **La correzione ovvia — «timbra solo se qualcosa è cambiato» — sarebbe stata SBAGLIATA**: `Consentita`
risponde `timbro < penultimo`, quindi un settore stabile avrebbe smesso di essere timbrato e sarebbe
diventato **eliminabile** mentre la sorgente lo manda ancora. La strada giusta era far dire al campo quello
che il suo nome promette: il timbro della **riga di catalogo**.

```
giro 1 (una volta sola, i timbri si allineano)   465 query   312 UPDATE
giro 2 e successivi                              153 query     0 UPDATE
```

## P7 — Le letture pubbliche non le poteva tenere nessuna cache

Una richiesta **anonima** a un documento pubblico — cioè a una copia **congelata**, che cambia solo quando
qualcuno ripubblica — rispondeva così:

```
Cache-Control: no-cache, no-store, max-age=0
Set-Cookie: .AspNetCore.Antiforgery.…
```

Le due righe insieme dicono a ogni cache del mondo «non tenermi». Davanti al sito c'è Cloudflare, e il giorno
della pubblicazione AIRAC è l'unica cosa che sta fra i lettori e un processo solo.

Il cookie lo emette l'endpoint dei Razor Component, sempre, perché un modulo che rende moduli non sa se
dentro ci sarà un form. **Qui dentro si sa**: in tutta l'interfaccia non esiste un `<form method="post">` né
un `<EditForm>` — l'unico form è la ricerca in barra, che è `method="get"` — e login e logout sono GET.

⚠️ È un'affermazione **sul codice di oggi**, non una proprietà eterna: la tiene ferma un test che diventa
rosso il giorno in cui un form compare, con scritto che **non si aggiusta il test**.

La decisione ha **sette clausole**, ognuna col suo test, perché sbagliare qui non produce un errore: produce
**la pagina di un altro**. Restano fuori le schermate di amministrazione, gli editor, il live, la ricerca, i
«cambiati», login/logout, le anteprime `?as=` (materiale di lavorazione), chi è entrato e chiunque porti un
cookie.

⚠️ Il controllo sul cookie è una rete **in più** rispetto a quello sull'identità, e serve davvero: in
sviluppo l'identità è finta e non passa dal `ClaimsPrincipal`, quindi «non autenticato» da solo direbbe di sì
anche per l'admin di sviluppo.

🔵 **Manca un passo che non è codice**: la **Cache Rule su Cloudflare**, che di suo le pagine HTML non le
tiene. Istruzioni in [`deploy/atc-ivao/LEGGIMI-DEPLOY.md`](../../deploy/atc-ivao/LEGGIMI-DEPLOY.md).

## P8 — Quattro moduli pesanti su ogni pagina

Mappe AoR, carte delle minime, viewer 3D e tour stavano nel `<body>` di **ogni** pagina: **13 029 byte
compressi** spediti a chi apre la ricerca, gli incarichi, un elenco, la guida, il login o l'hub, per servire
le sole schermate che hanno una mappa o uno stage 3D. Ora li carica `vipi-boot.js` quando la pagina mostra
qualcosa su cui possano lavorare.

⚠️ **Il criterio è il DOM, non l'indirizzo.** Un elenco di percorsi sarebbe una seconda copia della tabella
delle rotte da tenere allineata per sempre; il bersaglio invece è la cosa stessa su cui il modulo lavora.

⚠️ **`vipi-editor.js` e `vipi-media.js` NON sono in quella lista**, ed è una scelta: il codice C# li chiama
**per nome** (`vipiSetDirty`, `vipiEditorSections`, `vipiAirportEditorInit`, `vipiMedia.osserva`), e un
modulo che arrivasse un istante dopo la chiamata sarebbe un guasto silenzioso. Valgono 1 653 byte compressi
in due: non è un prezzo che valga quel rischio.

**Verificato in un browser vero** (Edge — lo script resta in
`.claude/skills/verifica-live/lazy-verifica.js`):

```
guida, hub            nessuno dei quattro scaricato
vIPI ACC              aor + mva + aor3d + leaflet; 77 tessere, 200 poligoni, 167 chip
guida -> vIPI ACC     il modulo arriva DOPO la navigazione «enhanced», 6 contenitori mappa
```

L'ultimo è il caso difficile: è il momento in cui un caricamento condizionale sbagliato **non fa niente e non
lo dice**.

## P9 — N+1: l'elenco degli aeroporti

Per calcolare la pista consigliata accanto a ogni scalo, l'elenco caricava il profilo **intero** di ogni
aeroporto, uno alla volta: livelli di transizione, SID, link-frequenze — roba che quell'elenco non guarda —
con **otto query a testa, in fila**.

```
elenco aeroporti LIBB (tre scali)   36 query -> 15
```

E soprattutto: il numero di query **non dipende più dal numero di aeroporti**. Erano 8 per scalo; con quindici
scali erano centoventi andate e ritorno una dietro l'altra. Ora sono due letture, sempre.

⚠️ **Sulla Struttura settori l'accorpamento è PARZIALE: 173 → 167.** Il grosso (~150) sta in
`ListOrphansAsync`, che per ogni orfano cerca i documenti che lo citano e chi ne blocca la rimozione — due
chiamate da una decina di query ciascuna. **Non sono state accorpate di proposito**: sono il percorso che
decide se un settore si può eliminare, e riscriverle in versione massiva è un lavoro con i suoi test e la sua
verifica, non una cosa da fare di sfuggita mentre si sistema il peso delle pagine. È una pagina di sola
amministrazione e a caldo costa trenta millisecondi. Il conto è scritto **accanto al ciclo**, perché il giorno
in cui gli orfani si moltiplicano si sappia già dove guardare. → `docs/lavori-aperti.md` §O1.

## P10 ❌ — I poligoni AoR non viaggiano due volte per niente

L'analisi diceva: «i poligoni sono spediti due volte (SVG di ripiego + JSON per Leaflet), più 13,6 KB di soli
`&quot;` — vale −60 KB». Quella conclusione guardava i byte **grezzi**, ed è sbagliata.

```
com'è oggi                                 grezzo 294 122   br 32 445
coordinate arrotondate a 5 decimali        grezzo 288 161   br 34 426   PEGGIO
JSON fuori dagli attributi (niente &quot;) grezzo 274 571   br 34 035   PEGGIO
```

Le ripetizioni — le virgolette escapate, le cifre decimali lunghe, gli stessi numeri presenti sia nell'SVG sia
nel JSON — sono esattamente ciò che un compressore mangia meglio. **Toglierle rompe le ripetizioni e fa
uscire più byte.** La copia dei poligoni, isolata, costa **770 byte compressi**.

Dove stanno davvero i byte di quella pagina:

```
SVG di ripiego     9 230 br
data-mva           5 112 br
commenti Blazor    1 256 br
data-sectors         770 br
```

I 9 230 dell'SVG sono il prezzo di ciò che fa: mostrare l'area **prima** che Leaflet arrivi — e da quando
Leaflet si carica su richiesta (P8) quella finestra è più lunga, non più corta. Le sue coordinate, poi, sono
già a 1–2 decimali: non c'è grasso da togliere.

Nessuna modifica. La misura è entrata nel codice accanto a `AorBlock.BuildSvg`, con scritto anche quale
sarebbe la strada giusta se un domani si volesse davvero recuperare quei 9 KB: **far disegnare il ripiego al
JavaScript dal JSON che già c'è**, non spedire meno dati.

---

## ⚠️ Il metodo — quattro test verdi che non dimostravano niente

È la parte che è costata di più, e vale oltre questo audit.

1. **`LivelliDiLogTests`** — senza un `ILoggerProvider` registrato, `IsEnabled` risponde **no a ogni
   livello**: i due test che chiedevano «≥ Warning» passavano perché «mai» è ≥ Warning, e i due che
   chiedevano «≤ Information» fallivano. Due verdi che non provavano nulla.
2. **`SectorProjectionTimbroTests`** — guardava il change-tracker **dopo** la chiamata, ma
   `SyncFromCatalogsAsync` **salva al proprio interno**: a quel punto le entità sono già tornate
   `Unchanged`, comunque siano andate le cose. Ora ascolta l'evento `SavingChanges`.
3. **`PisteMassiveTests`** — contava le query con `SavingChanges`, che **in lettura non scatta mai**:
   confrontava zero con zero. Ora un `DbCommandInterceptor` vero, e il test **pretende di aver visto almeno
   una query** prima di confrontarne il numero.
4. **`CircuitiGiustificatiTests`** — falliva sul proprio stesso commento: in un commento Razor la chiocciola
   si raddoppia, e `@@rendermode` contiene `@rendermode`.

⚠️ **E un errore di verifica, non di test.** `grep -c "^Failed!"` diceva «0 falliti» mentre un progetto **non
compilava**: senza build non c'è riga di esito. Da qui in avanti la verifica è `dotnet build Vipi.slnx`
**prima**, e poi il conto dei **progetti con esito (15)**, non dei falliti.

⚠️ **Una misura singola non è una misura.** Vedi P2: un numero che faceva comodo, non riprodotto.

---

## Verifica

- Build Release della **soluzione intera**: **0 errori, 0 avvisi**.
- Suite: **5 981** su 15 progetti con esito (era 5 746 su `main` prima del giro).
- **Nessuna migrazione nuova**: restano diciannove in coda al cutover MariaDB.
- Publish reale ripetuto: `Vipi.Assets` riporta 18 file minificati (−267 529 B) e 21 precompressi
  (1 010 917 → 462 572 B).
- Browser vero (Edge): mappe, chip e navigazione «enhanced» — vedi P8.
- Avvio a regime dal pacchetto pubblicato: **153 query, 0 UPDATE**.

## Cosa resta

| Voce | Blocco |
|---|---|
| **Cache Rule su Cloudflare** — senza, il bordo non tiene l'HTML e metà di P7 resta inespressa | 🔴 committente |
| **`passenger_min_instances ≥ 1`** e **`proxy_read_timeout ≥ 100s`** nelle direttive nginx di Plesk (`nginx-vipi.conf` **non viene caricato** lì) | 🔴 committente |
| `ListOrphansAsync`: ~150 query per otto orfani (§O1) | 🟢 |
| `blazor.web.js` (53 452 B br) non si precomprime: non è un file fisico | ⚪ per costruzione |

⚠️ **Al primo avvio dopo il deploy** la proiezione allinea i timbri dei settori **una volta sola** (312
UPDATE); dal secondo in poi tace. È previsto.
