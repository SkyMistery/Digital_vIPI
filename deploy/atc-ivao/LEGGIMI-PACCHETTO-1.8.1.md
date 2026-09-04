# Pacchetto 1.8.1 — solo i file cambiati

> **Timbro:** `1.8.1 · fa1685f` (4 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.8.0**, che avete caricato poche ore fa. **4 file** — il pacchetto più piccolo da mesi.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, NIENTE `wwwroot`, NIENTE FRASI
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare.
>
> **E nessun foglio di stile, nessun JavaScript, nessun indice degli asset**: la trappola di 1.8.0 — i
> quattro file di `wwwroot` che dovevano viaggiare insieme — qui **non esiste**. Non è una supposizione: le
> impronte `sha256` di `vipi-theme.css` e di `Vipi.Host.staticwebassets.endpoints.json` sono state
> confrontate con la copia dentro 1.8.0 e sono **identiche**.
>
> **E nessuna frase nuova**: nessun file di traduzione è cambiato, quindi `en/Vipi.Ui.resources.dll` resta
> fuori.
>
> Quattro file, quattro rinomine. Si carica quando volete.

---

## Perché esiste questo pacchetto

**Lo ha chiesto la vostra diagnostica**, non un'idea nostra. Nel file `errori-richieste.txt` che ci avete
mandato dopo il caricamento di 1.8.0 c'erano **quindici richieste finite in errore**. Quattro le chiudeva già
1.8.0. Le altre no, ed erano tutte lo **stesso difetto in tre posti diversi** — due dei quali capitati su
1.8.0 stessa poche ore prima.

## Che cosa succedeva, in parole

Quando aprite l'editor di un aeroporto, di un vSOP militare, o l'elenco dei vSOP militari, la pagina e il suo
contenuto **cominciano a leggere il database nello stesso istante**. Ma il collegamento al database, per il
modo in cui è fatto un sito come questo, è **uno solo per sessione**: due letture insieme su quel
collegamento e il programma si ferma.

Il risultato, per chi lo subiva, era una pagina che **cadeva mentre si apriva**: il messaggio di
riconnessione, e il lavoro non salvato in pericolo.

**Quante volte è successo, nella finestra che abbiamo guardato:**

| quando | dove |
|---|---|
| 07:22 | l'elenco dei **vSOP militari** |
| 08:15 e 10:16 | l'editor di un **aeroporto** |
| 12:06 | l'editor di un **vSOP militare** |

⚠️ **Non capita sempre**, ed è la cosa che rende questi difetti fastidiosi: dipende da chi finisce di leggere
per primo, quindi dalla velocità del database in quel momento. La stessa pagina si apre bene novantanove
volte e cade la centesima.

## Che cosa cambia per chi usa il sito

**Niente, a vedersi.** Nessuna schermata cambia, nessun comando si sposta, nessuna parola è diversa. Le tre
pagine adesso leggono il database su un collegamento **proprio**, invece di contendersi quello della
sessione.

> ℹ️ Se qualcuno vi aveva segnalato «l'editor dell'aeroporto ogni tanto si ricarica da solo» o «l'elenco dei
> militari a volte dà errore», è questo, ed è chiuso.

## ⚠️ Una cosa che va detta: non sono le uniche tre pagine fatte così

Cercando queste tre le abbiamo contate tutte: **ventisei** pagine del sito hanno la stessa forma, e in linea
di principio lo stesso rischio. Non è un'emergenza — di quelle ventisei, in un giorno intero di registro, ne
sono cadute **tre** — ma è una cosa vera che è giusto sapere.

Abbiamo messo un controllo automatico che le tiene contate e **impedisce che ne nascano di nuove** senza che
nessuno se ne accorga. Convertirle tutte tocca metà del sito: è un lavoro da decidere insieme, non da
infilare in un pacchetto di correzione.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-4-file-1.8.1/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **qui c'è il timbro `1.8.1`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: le tre corrette |

ℹ️ **Non c'è nient'altro**, e non è una dimenticanza: `Vipi.Application`, `Vipi.Infrastructure`,
`Vipi.Hosting`, le frasi inglesi, `wwwroot` e l'indice degli asset **non sono cambiati**. Le impronte
`sha256` dei quattro file sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

## L'ordine

1. **Caricate tutti e quattro col nome finto** (`.new` in fondo).
2. **Rinominate**: prima i due `.pdb`, poi i due `.dll`.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i tre controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.8.1 · fa1685f`**.
>
> ℹ️ Se trovate `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo. Se trovate
> `diagnostica/arresto-errore.txt`, **andate avanti**: quello dice che è morto un processo *precedente*
> chiudendo, ed è normale su questo hosting.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.
>
> ⚠️ Questo è il controllo che conta: il selettore della lingua, lo zoom e il tema funzionano **anche** su un
> sito caricato a metà, perché non passano dal server. La Ricerca sì.

> ### C · Le tre pagine corrette
>
> Aprite, una dopo l'altra: l'**elenco dei vSOP militari** (`/services/vsop/mil`), l'**editor di un
> aeroporto** e l'**editor di un vSOP militare**. Devono aprirsi e riempirsi senza il messaggio di
> riconnessione.
>
> ℹ️ Il gesto che le faceva cadere è **andare e tornare in fretta** fra l'editor e l'elenco: se volete
> provarci davvero, fatelo tre o quattro volte di fila.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non tocca.

---

## E quel che ci serve ancora da voi

**`diagnostica/errori-richieste.txt` fra qualche giorno.** È l'unico modo di sapere se queste tre corse sono
davvero sparite: qui non si riproducono — il database di sviluppo è su un file locale e risponde in
millisecondi, mentre da voi c'è MySQL in rete — quindi la prova non è nostra, è vostra.

| che cosa trovate | vuol dire |
|---|---|
| nessun «A second operation…» | le tre corse sono chiuse |
| ancora «A second operation…», ma su **altre** pagine | è una delle ventisei, e sappiamo quali guardare |
| ancora sulle **stesse** tre | la cura non basta, e ce lo dice il nome della pagina |

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**; **10 305 test** verdi su **quindici** assiemi (nove progetti sui due
  runtime), **E2E compresi** (300). Fra questi, **5 nuovi** per il controllo delle ventisei pagine;
- ⚠️ e il file provato è **esattamente** quello che si spedisce: `Vipi.Ui.dll` del pacchetto di prova e
  quello dentro lo zip hanno la **stessa impronta** `sha256`;
- il pacchetto **avviato davvero** dalla sua cartella e guidato in un browser: dieci controlli verdi, il
  circuito si apre, la **Ricerca** risponde, la console del browser è pulita;
- le **tre pagine corrette** aperte una per una sul pacchetto: circuito su, nessuna riconnessione, console
  pulita, contenuto pieno;
- e il gesto che le faceva cadere — **quattro andirivieni** fra editor ed elenco — senza un circuito caduto;
- il controllo nuovo **provato che morde**: tolta una pagina dall'elenco, il test cade e stampa il nome e i
  servizi. Un controllo che non si è mai visto fallire non prova niente.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **La corsa non si riproduce qui.** In sviluppo il database è un file locale e la finestra è di
   millisecondi; da voi è MySQL in rete. Quel che le prove dimostrano è che le tre pagine **funzionano** con
   il collegamento nuovo — che la corsa sia sparita lo dirà `errori-richieste.txt` fra qualche giorno.
2. **Le altre ventitré pagine restano come sono.** Il controllo nuovo le tiene contate; convertirle è un
   lavoro a parte.
3. **Sul processo che si spegne ogni cinquanta secondi questo pacchetto non fa niente.** Quella resta la
   strada del pannello dell'hosting, dopo il 16 settembre.
