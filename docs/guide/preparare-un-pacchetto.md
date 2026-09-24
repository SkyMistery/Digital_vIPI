# Preparare un pacchetto di consegna 🟢

> **La regola, ogni volta che si consegna.** Vale per `atc.it.ivao.aero`, che si aggiorna via **FTP** e
> dove non c'è né shell né pannello. Lo script che fa i passi meccanici è
> [`tools/prepara-pacchetto.ps1`](../../tools/prepara-pacchetto.ps1); qui c'è **l'ordine** e — soprattutto —
> le cose che nessuno script può decidere al posto di chi consegna.
>
> Nasce il 31 agosto 2026, dopo che il file dei **segreti di produzione** era finito dentro lo zip da
> spedire. Non era la prima volta (24 agosto). Un runbook non serve a ricordare: serve perché la volta in
> cui ci si dimentica è quella in cui si è di fretta.

## Prima di tutto: si consegna da soli?

La domanda che decide tutto il resto, e la risposta sta in **`Directory.Build.props`**, accanto al numero:

| | |
|---|---|
| **PATCH** `1.0.x` | solo correzioni: nessuna migrazione, nessuna pagina o sezione nuova |
| **MINOR** `1.x.0` | funzionalità nuove, e/o migrazioni **additive** |
| **MAJOR** `x.0.0` | ⚠️ il pacchetto **non si consegna da solo**: serve sostituire il database, o il codice nuovo non sa leggere l'archivio che c'è in produzione |

⚠️ Il maggiore **non è un giudizio sull'importanza**: è la risposta a «basta l'FTP, o serve anche il
database?». È l'unica domanda che qui costa una consegna coordinata con Ivao.It, e il 23 agosto 2026 è
costata una serata.

⚠️ **E il numero si ridecide quando cambia il CONTENUTO, non quando si decide di spedire.** Il 2 settembre
2026 il pacchetto si chiamava «1.3.1» da quando conteneva i soli §AO–§AR; poi ci sono entrati §AS, §AT e
§AU — un pannello, sei comandi, una porta d'ingresso nuova ai documenti. Una PATCH è «solo correzioni,
nessuna pagina o sezione nuova»: è partito **1.4.0**. Un numero che promette una regola vale finché la
regola si applica davvero.

⚠️ **E poi si ripuliscono i doc dal numero vecchio.** Quel giorno erano rimaste tre righe che dicevano
«non è in produzione, serve il pacchetto 1.3.1» su lavori che nel frattempo erano online: le voci **datate**
sono fotografie e non si toccano, ma una riga che afferma un fatto **al presente** è quella su cui qualcuno
prende una decisione fra sei mesi.

🔓 **La finestra cieca è CHIUSA dal 16 settembre 2026** (dal 31 agosto nessuno amministrava il database di
Ivao.It: un MAJOR non si spediva, e un test fermava le migrazioni distruttive). Il test è stato **cancellato**,
non spostato in avanti con le date: tenuto, sarebbe diventato una regola permanente travestita da eccezione.
⚠️ **Resta vero il fatto da cui nasceva**: in produzione `Database.Migrate()` gira all'avvio, da solo, su DDL
non transazionale. Una migrazione distruttiva si spedisce **con gli occhi aperti**: chi amministra il database
lo sa prima, e c'è una copia fresca — da 1.29.0 la scarica un Admin dalla Diagnostica
(`tools/Vipi.DbBackup verifica` per controllarla).

🔴 **Una migrazione ADDITIVA invece si spedisce, e queste due righe dicevano il contrario.** Fino al
3 settembre 2026 qui c'era scritto «e una migrazione nuova nemmeno», nominando quel test come presidio. Il
test però non dice quello: vieta le sole operazioni **distruttive** (`DropTable`, `DropColumn`,
`RenameColumn`, `RenameTable`, `AlterColumn`, `Sql`), e la sua stessa documentazione spiega che il punto
«non è impedirla, è che non possa succedere per distrazione».

⚠️ **E la pratica lo conferma**: `20260831014248_CatenaDiRipiego` e `20260831153616_LinguaBloccata` sono
già in produzione da **1.3.0** (31 agosto), cioè dentro la finestra — verificato guardando dentro il
`Vipi.Infrastructure.MySqlMigrations.dll` di quel pacchetto. Una regola che il progetto non ha mai seguito,
scritta in un runbook che si apre quando si è di fretta, è peggio di nessuna regola: si scavalca, e con lei
si scavalca l'abitudine di leggerlo.

**La domanda vera, prima di spedire uno schema**, resta una sola: *quella migrazione può lasciare il database
in uno stato da cui l'applicazione non riparte?* Additiva = no. Distruttiva = forse, e allora prima la copia.

## Il ramo: si parte da quel che GIRA, non da `main`

⚠️ **`main` non è il codice online.** Il pacchetto `j` del 30 agosto è stato costruito da
`consegna-db-20260830`, un ramo che per un giorno è esistito solo in locale. Un ramo di consegna aperto da
`main` avrebbe **riportato indietro** il sito.

```
git log --oneline <commit-del-pacchetto-online>..HEAD    # cosa aggiungo davvero
git diff --name-only <commit-del-pacchetto-online> HEAD -- src
```

La seconda riga serve due volte: dice se ci sono migrazioni nuove (la domanda qui sopra) e **quali progetti
sono cambiati davvero**, che è quello che decide i file del pacchetto incrementale.

## I passi

### 1. Verde su tutto, prima di pubblicare

```powershell
dotnet build Vipi.slnx -c Release --no-incremental   # 0 avvisi: qui gli avvisi SONO errori
dotnet test Vipi.slnx -c Release
```
⚠️ **Un riepilogo si legge intero**: `dotnet test` esce 0 anche quando un progetto non ha compilato e
sparisce dall'elenco. Contare gli assiemi, non fidarsi del colore.

### 2. Ruotare la consegna di prima

```powershell
.\tools\prepara-pacchetto.ps1 -Azione Ruota -SoloProva   # guarda cosa sposterebbe
.\tools\prepara-pacchetto.ps1 -Azione Ruota
```
Quel che era in `publish/` finisce in `publish_old/<data>/`, **con i suoi `docs/`**. ⚠️ Quei documenti non
si aggiornano mai più: sono la fotografia di cosa avevamo detto di fare allora, ed è l'unico modo di
rispondere fra sei mesi a «ma io ho seguito il foglio».

### 3. Pubblicare

```powershell
dotnet publish src\Vipi.Host\Vipi.Host.csproj -c Release -r linux-x64 --self-contained true `
    -o artifacts\publish\linux-x64-<data>
```
L'ottimizzatore degli asset gira da sé al publish (minifica CSS/JS e lascia i `.br`/`.gz`). ⚠️ **Il timbro
nasce dal commit**: si pubblica dopo aver committato, o il pacchetto dice una versione che non si può
rintracciare.

### 4. Scegliere i file, e non lasciarli scegliere al diff

Il confronto per impronta con il publish precedente dà **più file di quelli che servono**: gli assiemi
ricompilati differiscono per l'MVID anche quando il loro codice non è cambiato. La lista vera la dà il
`git diff` del passo precedente:

- i `.dll` dei **progetti cambiati davvero**, e i loro `.pdb` (senza, `errori-richieste.txt` perde il
  numero di riga);
- `en/Vipi.Ui.resources.dll` se sono cambiate le frasi;
- ⚠️ i file di `wwwroot` **con i loro `.br` e `.gz`**, e **insieme** a
  `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con che nome il sito chiede ogni asset, e
  scambiarne uno solo fa chiedere nomi che non esistono. È il difetto del 24 agosto.

Ogni `.dll` in più è una rinomina in più su un file che il processo tiene aperto: non è prudenza, è rischio.

🔴 **Ma il `git diff` NON vede le costanti.** Una `const` C# non si legge a runtime dall'assieme che la dichiara:
il compilatore ne **copia il valore** dentro ogni assieme che la usa. Se una `const` cambia in un progetto, i
progetti che la usano vanno spediti **anche col sorgente invariato**, o in produzione convivono il valore nuovo
(nell'assieme che la dichiara) e il vecchio (cablato negli altri).

Il 16 settembre 2026, consegna **1.28.0**: `DocumentSection.MaxDepth` (Domain) passava da 3 a 5, e il controllo
che rifiuta una sotto-sezione troppo profonda sta in `EfEditingRepository` (Infrastructure, sorgente **non**
toccato). Col solo Domain nuovo, il difetto per cui la modifica esisteva sarebbe rimasto online. Per ogni `const`
cambiata nel diff:

```
git diff <commit-online> HEAD -- src | grep -E '^[-+].*\bconst\b'
grep -rn "NomeDellaCostante" src --include=*.cs --include=*.razor    # chi la usa, progetto per progetto
```

🔴 **Prima di pubblicare un pacchetto net10: `dotnet --version` deve dire l'SDK di `global.json`** (e
quell'SDK deve essere l'ultimo della sua riga: https://dotnet.microsoft.com/download/dotnet/10.0). La patch del
runtime che va in produzione è quella dell'SDK che pubblica — con un SDK di tre mesi prima il pacchetto porta
un runtime senza tre patch di sicurezza, ed è successo il 13 settembre 2026 (ADR-0007 §D4-quater, punto 5).

🔴 **Eccezione: il primo pacchetto dopo il salto a net10 (L13, ADR-0007 §D4-quater) NON è una lista di
file.** (✅ Fatto con **1.40.0** il 21 settembre 2026: cartella `completo-1.40.0`, carico per spostamento di
cartelle sul server, vedi `deploy/atc-ivao/LEGGIMI-PACCHETTO-1.40.0.md`. Vale di nuovo al prossimo cambio di
runtime.) Cambia il runtime intero — `libcoreclr.so`, `libhostpolicy.so`, `System.Private.CoreLib.dll` e ogni
assieme del framework — più i pacchetti ri-risolti. Un carico parziale lascerebbe sul server un runtime 8 con
assiemi 10, o il contrario: il processo non parte, e il messaggio non somiglia alla causa. Si carica il
**publish completo**, con il vecchio zip pronto per tornare indietro, rispettando le quattro cose che l'FTP
non deve cancellare (`segreti/`, `appsettings.Production.json`, `vipi-keys/`, `tmp/`). Dal pacchetto dopo si
torna alla lista corta. ⚠️ I file di prima che net10 non produce più restano sul server: innocui, perché il
`deps.json` nuovo non li nomina, ma vanno elencati nel foglio.

⚠️ **Il diff sceglie, le impronte VERIFICANO.** Le due cose non si sostituiscono: il `git diff` dice quali
*progetti* guardare, poi si confronta lo `sha256` di ogni candidato con **la copia dentro il pacchetto
precedente** (`artifacts/publish_old/<data>/solo-N-file-<versione>/`) e si tiene solo ciò che è cambiato
davvero. Su 1.4.1 quel confronto ha tolto due file su undici: `vipi-ui.js` e `vipi-print.css` erano
**identici**. ⚠️ Il satellite `en/Vipi.Ui.resources.dll` fa il contrario — cambia impronta a ogni
ricompilazione anche a frasi ferme — quindi lì comanda il diff: si spedisce solo se un `.resx` è cambiato.

```powershell
.\tools\prepara-pacchetto.ps1 -Azione Impronte -Pacchetto solo-N-file-<versione> -Versione <versione> -Elenco elenco.txt
```

### 5. Lo zip

```powershell
.\tools\prepara-pacchetto.ps1 -Azione Zip -Pacchetto solo-N-file-<versione> -Versione <versione>
```

Lo zip esce con **due rami paralleli**: `solo-N-file-<versione>/` (si carica) e `docs/` (si legge). I fogli
li ricopia da `deploy/atc-ivao/`, che resta la sorgente — una copia sola invecchia da sola.

> ### 🔴 Le due reti, e perché ci sono
>
> **Lo zip si costruisce dall'elenco dichiarato, mai camminando la cartella.** Il 31 agosto 2026 nella
> cartella dei file da caricare era comparso il file dei **segreti** di produzione — connection string con
> la password, `ClientSecret` di IVAO — e camminando la cartella era finito dentro il file che si spedisce.
> Quel file è protetto **solo** dal nome non indovinabile: dentro un allegato non è protetto da niente.
> ⚠️ La stessa cosa sta in `publish_old/20260824-i/solo-4-file-i/`, del 24 agosto.
>
> 1. quel che sta nella cartella e **nessuno ha dichiarato** viene elencato e lasciato fuori;
> 2. i file **di testo** dichiarati vengono guardati dentro: `ConnectionStrings`, `ClientSecret`,
>    `Password=`, `ApiKey`, una chiave privata → **il pacchetto si ferma**.
>
> ⚠️ La seconda rete guarda **solo i file di testo**, e non è pigrizia: la prima stesura leggeva ogni file
> sotto il mezzo mega e accusava `Vipi.Host.dll`, dove «ClientSecret» compare perché è il **nome** di una
> chiave di configurazione scritta nel codice. Un allarme che suona a ogni consegna su un file che
> dev'esserci è il modo in cui si smette di leggere gli allarmi.
>
> ### 🔴 E quando la rete suona su un falso allarme, la domanda non è «come la zittisco»
>
> Il 2 settembre 2026, consegna **1.5.0**: la rete ha fermato il pacchetto su `appsettings.json`, che
> contiene la parola `ClientSecret`. Guardato con i propri occhi, come dice la riga qui sopra:
> `ClientId`, `ClientSecret` e le due `ApiKey` sono stringhe **vuote** — ci sono solo i **nomi** delle
> chiavi. Falso allarme, della famiglia già nota.
>
> **Ma la mossa giusta non era forzare: era chiedersi se quel file servisse.** Non serviva — le due chiavi
> nuove avevano gli stessi valori dei default scritti nel codice — ed è **rimasto fuori**: da 14 file a 13.
> Un file in meno, una rinomina in meno su un file che il processo tiene aperto, la configurazione del
> server non toccata, e nessuna rete scavalcata.
>
> **Regola:** davanti a un allarme, prima di decidere *come* zittirlo si decide **se quel file deve
> esserci**. Quasi sempre la risposta toglie il problema invece di aggirarlo.

### 6. Provare il PACCHETTO, non il sorgente

⚠️ **Sono due cose diverse.** Nel publish il JavaScript passa per l'ottimizzatore che lo **minifica**, e da
`1.1.0` uno di quei file è l'unico che avvia Blazor: una minificazione che ne cambiasse il comportamento
darebbe un sito che si vede e non risponde, e i test non lo vedrebbero mai.

```powershell
dotnet publish src\Vipi.Host\Vipi.Host.csproj -c Release -r win-x64 --self-contained true -o <scratchpad>\pubwin
```
Si avvia l'exe **dalla sua cartella** (la content root è la directory corrente: da altrove la pagina esce
senza CSS né JS) e si guida con Edge — skill `verifica-live`. Cosa guardare, oltre alla schermata:

- il file minificato **arriva** e il circuito si apre;
- **la Ricerca trova**: `/services/vsop/search`, si scrive **`LIRF`** e deve uscire **almeno un documento**
  (le voci della Guida, col libro, non contano: rispondono anche a database vuoto). ⚠️ Non basta che la riga sotto
  il campo cambi: anche «0 results for …» è una riga che cambia, e per settimane questo controllo è stato verde su una
  ricerca che a schermo diceva zero (S8, 24 settembre 2026). `pacchetto-verifica.js` lo fa da sé: il termine è
  `TERMINE` in cima allo script, col suo perché;
- il processo **ucciso e riavviato** → la pagina si ricarica da sola;
- `diagnostica/avvio-diagnostica.txt` dice la **versione giusta**.

### 6-bis. E dopo il caricamento, rifare la stessa prova su PRODUZIONE

Lo stesso driver, puntato fuori. Da anonimo l'editor non si raggiunge, quindi si salta:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

⚠️ **Non fermarsi al timbro.** `avvio-diagnostica.txt` dice quale versione è partita, non che il sito
**funzioni**: da 1.1.0 un caricamento incompleto dà un sito che si vede intero e non risponde a niente, e
il timbro lì sarebbe giusto lo stesso. Il controllo che conta è **la Ricerca**, perché passa dal server — e deve
**trovare** (`LIRF` → almeno un documento), non solo rispondere.

⚠️ **Se il controllo dice «termine di prova da cambiare»**, il sito ha risposto: è `LIRF` che non si trova fra i
documenti pubblici. Se i documenti di Roma e di LIRF sono ancora pubblicati è la ricerca a non trovare (difetto
da aprire), altrimenti si sceglie un altro termine e si cambia `TERMINE` nello script (o `TERMINE=… node …`).

⚠️ **E un `200` su una pagina riservata non vuol dire che sia aperta.** In questo prodotto i cancelli si
**disegnano** — «Accesso riservato» nel corpo, e nessun dato — non si restituiscono come stato HTTP. Chi
controlla una pagina di staff da fuori deve guardare il **corpo**, non il codice di risposta: fermarsi al
200 fa gridare a una falla che non c'è. Misurato su `/services/vsop/airspace` il 31 agosto 2026.

ℹ️ Va fatto **mentre chi ha caricato è ancora al telefono**: è il momento in cui un file dimenticato si
rimette in trenta secondi.

### 7. Il foglio per chi carica

`deploy/atc-ivao/LEGGIMI-PACCHETTO-<versione>.md`, e dentro **non basta l'elenco dei file**: ci vanno le
cose che un controllo normale non prende. Per 1.1.0 erano due — non riavviare finché il database del 30 non
è dentro, e il controllo che distingue un sito vivo da uno mezzo caricato.

⚠️ **Il controllo finale non è «la pagina si apre».** E non è nemmeno «premete un tasto»: il selettore
della lingua è un `<a>`, lo zoom e il tema sono JavaScript di pagina, e **funzionano lo stesso** su un sito
in cui Blazor non è mai partito. Va scelto un comando che passa dal **server** — oggi la Ricerca — e va
**provato nei due modi** prima di scriverlo nel foglio.

La frase del foglio, da copiare così dal 24 settembre 2026 (prima diceva «due lettere, la riga deve cambiare»):

> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI
> Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

### 8. Scrivere dove si è arrivati

`docs/lavori-aperti.md` (una voce §A per consegna: cosa c'è dentro, sha256, cosa resta da fare),
`HANDOFF.md`, e le memorie. ⚠️ Il foglio col timbro finisce nel commit **dopo** quello che ha timbrato il
binario: il timbro nasce dal commit al momento del publish, e va scritto quale.

## Le trappole, in fila

| | |
|---|---|
| `main` non è ciò che gira | si parte dal ramo della consegna online |
| il diff per impronta gonfia | gli assiemi ricompilati differiscono per l'MVID: comanda `git diff -- src` |
| il diff non vede le `const` | il valore si COPIA negli assiemi che la usano: si spediscono anche col sorgente invariato |
| `wwwroot` e l'indice | viaggiano **insieme**, o il sito chiede nomi che non esistono |
| i `.md` | non stanno con i file da caricare: nello zip sono un ramo a parte |
| i segreti | non entrano in nessun pacchetto. Vanno da soli in `public_atc/segreti/` |
| la rete che suona | prima di chiedersi **come** zittirla, ci si chiede **se quel file deve esserci** |
| il timbro | nasce dal commit: si pubblica **dopo** aver committato |
| la prova | si fa sul pacchetto pubblicato, non sul sorgente |
| il riavvio | `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge |
| il timbro non basta | dice quale versione è partita, non che il sito risponda: il controllo è **la Ricerca** |
| la Ricerca che «risponde» | «0 results for …» è una riga che cambia: si pretende **almeno un documento** per `LIRF` |
| un 200 su una pagina riservata | i cancelli si **disegnano**: si guarda il corpo, non lo stato HTTP |
| la Guida | sta DENTRO `Vipi.Ui.dll` (`GuidaPage.razor`): si scrive **prima** del publish. Su 1.34.0 il primo publish ne era senza, e si è rifatto tutto (timbro, impronte, zip, prova) |
