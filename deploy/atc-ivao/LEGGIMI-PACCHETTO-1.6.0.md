# Pacchetto 1.6.0 — solo i file cambiati

> **Timbro:** `1.6.0 · 2a7d86fd` (3 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.5.0**, che è quello attualmente sul server. **25 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟠 QUESTA VOLTA C'È UNA MIGRAZIONE — e va letta prima di caricare
>
> Il pacchetto porta **una** migrazione del database: `20260903092755_DocumentiUniti`. **Gira da sola al
> primo avvio** dopo il caricamento — non c'è niente da lanciare a mano.
>
> **Che cosa fa, per intero:** crea **due tabelle nuove** (`DocumentUnions`, `DocumentUnionMembers`) e
> **due indici** su di esse. Quattro operazioni, **tutte in aggiunta**. Non tocca nessuna tabella
> esistente: non cancella colonne, non rinomina niente, non riscrive righe.
>
> 🟢 **Perché si può fare adesso**, anche se fino al 16 settembre nessuno può rimettere in piedi il
> database: il rischio di quella finestra è una migrazione che *si ferma a metà e lascia lo schema
> inservibile* — il DDL di MariaDB non è transazionale. Una `CREATE TABLE` che fallisse lascerebbe
> semplicemente la tabella non creata, e i binari di prima continuerebbero a girare senza accorgersene.
> ⚠️ E non sarebbe la prima: **due migrazioni della stessa finestra sono già in produzione da 1.3.0**
> (31 agosto).
>
> ℹ️ Una copia di sicurezza del database prima di caricare **non serve**, ma se la fate non fa male.

---

## Che cosa cambia

### 1. Unire due documenti — la funzionalità nuova

Su uno stesso scalo si possono **unire** la vIPI d'aeroporto, il vSOP militare e la vIPI di un APP non
remotizzato: si **leggono in una pagina sola**, si **redigono da un editor solo** e si **pubblicano con un
clic**. Vale anche sui campi con **presenza militare**, dove il vSOP diventa così il documento completo
dello scalo — che è quel che era stato chiesto.

- il comando sta nell'**editor** del documento che fa da **ospite**, nel riquadro **«Documenti uniti»**,
  e si accende **solo in modifica**;
- gli altri membri, in pubblico, **reindirizzano** alla pagina unita: un collegamento salvato continua a
  portare allo stesso contenuto;
- **pubblicare uno pubblica tutti**, allo stesso ciclo AIRAC, e annullare una release annulla anche le
  sorelle di quel ciclo. Il pannello lo dice **prima**, con quanti sono;
- **«Modifica» prende il lock su tutti i membri**: se anche uno solo è tenuto da un altro, non se ne prende
  nessuno e la pagina dice chi lo tiene;
- si **scioglie** quando si vuole, e non si perde niente di quel che c'è scritto.

⚠️ Le sezioni che i due documenti hanno in comune — METAR, piste, quote di transizione — **restano tutte e
due**, ognuna sotto l'intestazione del suo documento. È voluto: le frequenze di un vSOP militare e quelle di
un avvicinamento non sono la stessa cosa. Quelle che sono davvero doppie si **nascondono**, col tasto che
nasconde una sezione.

La **Guida** in-app ha una sezione nuova, «Unire due documenti», in italiano e in inglese.

### 2. La riga inglese delle SID nella Guida

Della coppia italiano/inglese era stata aggiornata solo l'italiana: la versione inglese diceva ancora che le
SID si importano «al primo prelievo del ciclo AIRAC», cioè il meccanismo che 1.5.0 aveva già cambiato. Era la
coda dichiarata di 1.5.0, e viaggia qui.

### 3. Le correzioni della supervisione

Il lavoro è stato riletto da capo prima di spedirlo, e ne sono uscite quindici correzioni. Le tre che si
sarebbero viste:

- l'elenco **«Bozze e versioni»** mostrava la pastiglia «uniti: 2» e poi pubblicava **un** documento solo;
- su una pagina unita, un documento a **lingua bloccata** tingeva della propria lingua **tutta** la pagina;
- un documento **pubblicato** unito sotto uno **non pubblicato** spariva dal web.

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-25-file-1.6.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **è qui che sta il timbro `1.6.0`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: la pagina unita, l'editor unito, la Guida |
| 5-6 | `Vipi.Application.dll` · `.pdb` | le regole: le unioni, la pubblicazione accoppiata |
| 7-8 | `Vipi.Infrastructure.dll` · `.pdb` | il database |
| 9-10 | `Vipi.Infrastructure.MySqlMigrations.dll` · `.pdb` | ⚠️ **le migrazioni di produzione — è qui che sta quella nuova** |
| 11-12 | `Vipi.Domain.dll` · `.pdb` | i tipi di base |
| 13-14 | `Vipi.Hosting.dll` · `.pdb` | il montaggio dei servizi |
| 15 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`** |
| 16 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset |
| 17-25 | `wwwroot/_content/Vipi.Ui/` — `vipi-theme.css`, `vipi-print.css`, `vipi-ui.js`, e per ognuno il `.br` e il `.gz` | gli stili e il JavaScript |

> ### ⚠️ `wwwroot` e il suo indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` dice **con che nome** il sito chiede ogni file di `wwwroot`.
> Caricare l'indice senza i file, o i file senza l'indice, fa chiedere al sito nomi che non esistono: le
> pagine escono **senza stili**. È il difetto del 24 agosto 2026. **O tutti e dieci, o nessuno.**

> ### ⚠️ `Vipi.Host.dll` c'è anche se il sito non è cambiato
>
> Nessun sorgente di quel progetto è stato toccato, ma il **timbro** (versione + commit) è scritto dentro
> quel file. Senza, la barra direbbe ancora `1.5.0 · a071575` su binari 1.6.0 — un pacchetto che non si può
> più rintracciare, che è il problema per cui il timbro esiste.

ℹ️ **`appsettings.json` NON è nel pacchetto**, ed è voluto: non è cambiato, e un file in meno è una rinomina
in meno su un file che il processo tiene aperto.

ℹ️ Le impronte `sha256` di tutti e venticinque sono in `IMPRONTE.txt`, dentro la cartella del pacchetto. Non
sono state dedotte: ogni file è stato **confrontato con la copia dentro l'ultimo pacchetto che lo
conteneva** — 1.5.0 per la maggior parte, **1.3.0** per le migrazioni, 1.4.x per gli asset.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo).
2. **Rinominate**, dal più profondo al più superficiale: prima `wwwroot/_content/Vipi.Ui/*` e
   `en/Vipi.Ui.resources.dll`, poi `Vipi.Host.staticwebassets.endpoints.json`, poi i `.pdb`, e **per ultimi
   i `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**: è a quell'avvio che gira la
   migrazione.
4. **Fate i controlli** qui sotto — il **controllo A** questa volta conta più del solito.

## I controlli, in due minuti

> ### A · La migrazione è andata (il più importante di questo pacchetto)
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.6.0 · 2a7d86f`**. ⚠️ **Se compare `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo**: è lì
> che finirebbe un errore della migrazione.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati. ⚠️ Non usate lingua,
> zoom o tema per provare: funzionano anche a sito morto.

> ### C · Gli stili sono arrivati interi
>
> Una pagina qualsiasi deve avere **l'aspetto di sempre**. Se esce come testo nudo senza colori, l'indice
> degli asset e i file di `wwwroot` non sono stati caricati insieme: ricaricate i dieci file mancanti.

> ### D · La funzionalità nuova
>
> Aprite l'editor di un documento (aeroporto, APP o vSOP militare) e premete **✎ Modifica**. Sotto le
> sezioni, sopra il riquadro delle versioni, deve esserci **«Documenti uniti»** con un campo di ricerca e
> una tendina. ℹ️ Non serve unire niente per il controllo: basta che il riquadro ci sia e la tendina si
> riempia.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi
l'indice degli asset e `wwwroot`, poi il resto, poi riavvio.

⚠️ **Sul database**: le due tabelle nuove **restano**, e non danno fastidio a nessuno — i binari di prima non
sanno che esistono e le ignorano. Non vanno cancellate a mano. L'unica cosa che si perderebbe tornando
indietro sono le **unioni** eventualmente create nel frattempo: le pagine tornerebbero separate, e i
documenti restano tutti al loro posto.

Restano i due limiti di prima, tutti e due piccoli: la **distanza di un alternato scritta coi decimali**
(1.4.0) e le **SID col ciclo d'entrata** (1.5.0).

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente — sono due cose diverse: nel pacchetto il JavaScript
passa per l'ottimizzatore che lo **minifica**, e uno di quei file è l'unico che avvia il sito.

- build in Release sui due runtime, **0 avvisi**; **9989 test** verdi su **otto** progetti (quindici righe
  di riepilogo: sette progetti girano su due runtime), **E2E compresi** (289);
- il pacchetto **avviato davvero** (l'eseguibile, dalla sua cartella) e guidato in un browser:

| | |
|---|---|
| il timbro | **`1.6.0 · 2a7d86f`** in `diagnostica/avvio-diagnostica.txt`, e **nessun** `avvio-errore.txt` |
| il circuito | si apre, **col JavaScript minificato** (`vipi-riconnessione.js`: 2 400 byte **su una riga**, 200) |
| la **Ricerca** | `LI` → **«50 results for LI»**, la pagina passa da 162 a 6 351 caratteri |
| la Guida | la sezione **«Unire due documenti» / «Joining two documents»** c'è in tutte e due le lingue |
| la pagina unita | LIMN Cameri: due indici, **34 voci**, **zero ancore senza bersaglio**, un solo piè di stampa |
| il **riavvio** | processo ucciso → la pagina se ne accorge; riavviato → torna viva **da sola** |
| console, rete, barra d'errore | **nessun errore**, **nessun 4xx**, la barra rossa resta nascosta |

⚠️ **Quel che NON è stato provato, e va detto:**

- **la migrazione su MariaDB vera.** Lo sviluppo gira su SQLite, e non c'è un MariaDB su cui applicarla: la
  migrazione MySQL è stata **letta** (quattro operazioni, tutte in aggiunta) ed è il gemello di quella
  SQLite, che invece è stata applicata davvero su una **copia del database reale**. È lo stesso regime con
  cui sono uscite tutte le migrazioni di questo progetto.
- **il caricamento in due tempi**: se `wwwroot` e il suo indice finiscono sul server in momenti diversi, fra
  i due il sito esce senza stili. È esattamente il controllo C.
