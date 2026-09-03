# Pacchetto 1.6.1 — solo i file cambiati

> **Timbro:** `1.6.1 · 6ffbe23a` (3 settembre 2026, sera). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.6.0**, che è quello attualmente sul server. **7 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, e niente `wwwroot`
>
> **Nessuna migrazione**: non c'è niente da concordare con chi amministra il database, nessuna copia di
> sicurezza, nessuna finestra da aspettare. Si carica quando volete.
>
> E **nessun foglio di stile o JavaScript** è stato toccato, quindi non c'è nemmeno l'indice degli asset —
> con esso sparisce la trappola del 24 agosto. Il caricamento è il più semplice possibile: sette file, tutti
> nella cartella dell'applicazione tranne uno.

---

## Perché va caricato

🔴 **Viene dalla diagnostica che ci avete mandato**: nell'ora successiva al caricamento di 1.6.0,
`errori-richieste.txt` ha registrato **nove richieste finite in errore**, tutte con lo stesso messaggio —
*«A second operation was started on this context instance»* — e una degenerata in
*«MySqlProtocolException: Packet received out-of-order»*, cioè una connessione al database corrotta.

Sono **pagine d'errore vere**, viste da chi stava usando il sito.

**Che cos'era.** Due componenti dell'editor prendevano dal **circuito** (cioè da una connessione al
database condivisa con tutto il resto della sessione) un servizio che scrive:

- l'**editor del vSOP militare**, nel punto in cui *crea* il documento se manca;
- l'**editor dell'APP non remotizzato**, nel punto in cui risolve il documento.

Basta che una qualsiasi altra parte della pagina legga nello stesso istante, e le due operazioni si pestano.

⚠️ **Non è un difetto di 1.6.0**: c'era anche prima, con le stesse identiche righe — l'abbiamo verificato
sulla storia del codice. Si è visto adesso perché la diagnostica di 1.6.0 è stata guardata.

## E una cosa in più, piccola

I **giri periodici fermi** ora compaiono anche nella **Diagnostica**, non solo nella pagina Sorgenti.

Il segnale c'era già — un giro il cui ultimo successo è più vecchio del doppio della sua cadenza — ma si
vedeva solo aprendo Sorgenti. ⚠️ Il 2 settembre la Diagnostica diceva **«Avvio 0»** mentre metà dei giri
periodici non partiva: uno zero che rassicura sul contrario di quel che succede.

> ### ℹ️ Aspettatevi più righe nella Diagnostica, ed è normale
>
> Alla prima apertura dopo questo aggiornamento la Diagnostica mostrerà **parecchi rilievi nell'area
> «Avvio»**, uno per ogni giro periodico fermo. **Non è una regressione**: è la verità che prima non si
> vedeva. Se dopo qualche giorno sono spariti, vuol dire che il processo del sito ora resta acceso.

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-7-file-1.6.1/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **è qui che sta il timbro `1.6.1`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: è qui che stanno le due correzioni |
| 5-6 | `Vipi.Application.dll` · `.pdb` | le regole: il controllo nuovo della Diagnostica |
| 7 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`** |

> ### ⚠️ `Vipi.Host.dll` c'è anche se il sito non è cambiato
>
> Nessun sorgente di quel progetto è stato toccato, ma il **timbro** (versione + commit) è scritto dentro
> quel file. Senza, la barra direbbe ancora `1.6.0 · 2a7d86fd` su binari 1.6.1 — un pacchetto che non si può
> più rintracciare, che è il problema per cui il timbro esiste.

ℹ️ **Non ci sono i file delle migrazioni** (`Vipi.Infrastructure*.dll`): non sono cambiati, e questo
pacchetto non tocca il database. Le impronte sono state **confrontate** con il pacchetto 1.6.0, non dedotte:
tutti e sette risultano diversi, niente da scartare.

ℹ️ Le impronte `sha256` di tutti e sette sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo).
2. **Rinominate**, dal più profondo al più superficiale: prima `en/Vipi.Ui.resources.dll`, poi i `.pdb`, e
   **per ultimi i `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.6.1 · 6ffbe23`**. Se compare `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.

> ### C · La correzione — ed è quella che conta di più
>
> Aprite l'**editor di un vSOP militare** e l'**editor di un APP non remotizzato**, e in tutti e due premete
> **✎ Modifica**. Devono aprirsi e restare vivi. Se compare la barra in basso «Qualcosa è andato storto…»,
> ricaricate e **mandateci `diagnostica/errori-richieste.txt`**.
>
> ⚠️ **E fra qualche giorno, il controllo che vale davvero**: in `errori-richieste.txt` **non devono più
> comparire** le righe con `CreaAsync` e `ResolveForDocumentAsync`. Quella è la prova, e non si può avere
> subito.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi il
resto, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non tocca — e nessun limite
nuovo al tornare indietro.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release sui due runtime, **0 avvisi**; **10 013 test** verdi su **otto** progetti, **E2E
  compresi** (289);
- il pacchetto **avviato davvero** e guidato in un browser:

| | |
|---|---|
| il timbro | **`1.6.1 · 6ffbe23`**, e **nessun** `avvio-errore.txt` |
| il circuito | si apre, col JavaScript **minificato** (2 400 byte su una riga) |
| la **Ricerca** | `LI` → **«50 results for LI»**, la pagina passa da 162 a 6 351 caratteri |
| **editor militare** | si apre (29 sezioni), **✎ Modifica** entra, barra d'errore nascosta |
| **editor APP** | si apre (16 sezioni), **✎ Modifica** entra, barra d'errore nascosta |
| la pagina unita | 34 voci d'indice, **zero ancore senza bersaglio** |
| console, rete | **nessun errore**, **nessun 4xx** |

🔴 **Quel che NON è stato provato, e va detto chiaro: la corsa non si riproduce qui.** In sviluppo il
database è SQLite e la finestra fra le due operazioni è di millisecondi; su MariaDB, con la latenza vera, si
apre. Quel che le prove qui sopra dimostrano è che **spostare il servizio non ha rotto niente** — che la
correzione funzioni lo dirà solo `errori-richieste.txt` fra qualche giorno.

ℹ️ È lo stesso motivo per cui la difesa vera non è un test di comportamento ma una **guardia strutturale**:
un controllo che legge il codice e rifiuta un servizio preso dal circuito. Quella guardia esisteva già ma
guardava un nome solo, ed è per questo che non ha visto questi due. Ora ne guarda otto.
