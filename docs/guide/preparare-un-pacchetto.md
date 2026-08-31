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

🔒 **Dentro la finestra cieca** (nessuno amministra il database di Ivao.It) un MAJOR **non si spedisce**, e
una migrazione nuova nemmeno: in produzione `Database.Migrate()` gira all'avvio, da solo, su DDL non
transazionale, senza nessuno che possa ripristinare. Presidio: `MigrazioniDellaFinestraCiecaTests`.

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
- **la Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare;
- il processo **ucciso e riavviato** → la pagina si ricarica da sola;
- `diagnostica/avvio-diagnostica.txt` dice la **versione giusta**.

### 7. Il foglio per chi carica

`deploy/atc-ivao/LEGGIMI-PACCHETTO-<versione>.md`, e dentro **non basta l'elenco dei file**: ci vanno le
cose che un controllo normale non prende. Per 1.1.0 erano due — non riavviare finché il database del 30 non
è dentro, e il controllo che distingue un sito vivo da uno mezzo caricato.

⚠️ **Il controllo finale non è «la pagina si apre».** E non è nemmeno «premete un tasto»: il selettore
della lingua è un `<a>`, lo zoom e il tema sono JavaScript di pagina, e **funzionano lo stesso** su un sito
in cui Blazor non è mai partito. Va scelto un comando che passa dal **server** — oggi la Ricerca — e va
**provato nei due modi** prima di scriverlo nel foglio.

### 8. Scrivere dove si è arrivati

`docs/lavori-aperti.md` (una voce §A per consegna: cosa c'è dentro, sha256, cosa resta da fare),
`HANDOFF.md`, e le memorie. ⚠️ Il foglio col timbro finisce nel commit **dopo** quello che ha timbrato il
binario: il timbro nasce dal commit al momento del publish, e va scritto quale.

## Le trappole, in fila

| | |
|---|---|
| `main` non è ciò che gira | si parte dal ramo della consegna online |
| il diff per impronta gonfia | gli assiemi ricompilati differiscono per l'MVID: comanda `git diff -- src` |
| `wwwroot` e l'indice | viaggiano **insieme**, o il sito chiede nomi che non esistono |
| i `.md` | non stanno con i file da caricare: nello zip sono un ramo a parte |
| i segreti | non entrano in nessun pacchetto. Vanno da soli in `public_atc/segreti/` |
| il timbro | nasce dal commit: si pubblica **dopo** aver committato |
| la prova | si fa sul pacchetto pubblicato, non sul sorgente |
| il riavvio | `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge |
