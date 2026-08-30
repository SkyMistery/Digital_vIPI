# vIPI — aggiornamento del 23 agosto 2026

> ## ⛔ Questo foglio è STORIA: la consegna del 23 agosto è già stata fatta
>
> Se state installando il pacchetto **«b»**, la procedura buona è
> [`LEGGIMI-CORREZIONE-20260823b.md`](LEGGIMI-CORREZIONE-20260823b.md): sono **soli file**, il database
> **non si tocca**.
>
> ⚠️ I passi qui sotto cominciano con un `DROP DATABASE`. Rieseguirli oggi **cancella l'archivio**.
>
> ⚠️ **Anche il modo di caricare i file, qui sotto, non va più seguito.** Dice di sovrascrivere il
> contenuto della cartella e, se un file si rifiuta, di fermare l'applicazione dal pannello Plesk. La prima
> cosa ha buttato giù il sito la notte del 23→24 agosto 2026 — sovrascrivere una `.dll` mentre
> l'applicazione gira la tronca sotto il processo, che muore all'istante; la seconda non è eseguibile,
> perché su questo server non c'è accesso al pannello. La procedura buona è
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md): si carica col nome finto e si rinomina.

Il sito è **già in produzione** su `https://atc.it.ivao.aero` dal 16 agosto. Questo foglio è per
**aggiornarlo**, non per installarlo da capo: la prima installazione è in
[`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md), che resta valido per tutto il resto (configurazione, redirect
IVAO, impostazioni di MariaDB).

> ## ⚠️ Questo aggiornamento cambia anche il DATABASE, e i due pezzi non sono separabili
>
> Il modello dei **coordinamenti** è cambiato: un accordo per coppia di enti, e il traffico dentro le
> sezioni. Il codice nuovo **non sa più leggere** l'archivio vecchio, e l'archivio vecchio non si converte
> da solo all'avvio senza rischio.
>
> Per questo insieme al pacchetto vi arriva un file **`.sql`**, e il database **si sostituisce**: si
> svuota e si ricarica. **Non è un aggiornamento incrementale.**
>
> ⚠️ **Tutto ciò che è stato scritto direttamente sul sito di produzione dal 16 agosto in poi va perso.**
> Se in quelle giornate qualcuno ha modificato documenti, pubblicato release o importato dati **dal sito**,
> fermatevi e ditecelo prima di procedere: si fa in un altro modo.

---

## ⛔ Le quattro cose da NON cancellare

L'aggiornamento sovrascrive i file dell'applicazione. Dentro la stessa cartella ce ne sono **quattro** che
non vengono da noi e che, se spariscono, non si recuperano:

| Cosa | Dove | Se sparisce |
|---|---|---|
| **`segreti/`** | `…/public_atc/segreti` | contiene la **password del database** e le credenziali IVAO. Senza, il sito riparte lo stesso — **su un database SQLite vuoto**, e sembra che si siano persi tutti i dati. ⚠️ Il file dentro ha un nome scelto da voi e scritto da nessuna parte: se lo perdete non lo ricostruiamo noi |
| **`appsettings.Production.json`** | nella radice dell'applicazione | dice quale motore usare (`MySql`), su quale nome risponde il sito e dove sta il key-ring. Senza, l'applicazione ricade sui default e parte su uno SQLite vuoto, con lo stesso sintomo |
| **`vipi-keys/`** | `…/public_atc/vipi-keys` | sono le chiavi che firmano le sessioni: perderle **slogga tutti**, una volta sola |
| **`tmp/`** | `…/public_atc/tmp` | è la cartella con cui si riavvia Passenger (`restart.txt`) |

> ⚠️ **Fino al 30 agosto 2026 questa tabella ne elencava tre, e `segreti/` non c'era** — né qui né in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). La cartella è nata il 24 agosto
> ([`LEGGIMI-SEGRETI.md`](LEGGIMI-SEGRETI.md)) e i fogli d'aggiornamento non sono stati rifatti: la voce
> «password del database» è rimasta attaccata al file sbagliato per sei giorni. Chi avesse seguito la
> tabella alla lettera avrebbe protetto il file che *non* ha più la password e lasciato scoperto quello
> che ce l'ha.

ℹ️ Nel pacchetto **non c'è** `appsettings.Production.json`, apposta: c'è
`deploy/appsettings.Production.json.esempio`, che serve solo da riferimento — ed è **nella cartella
`deploy/`**, cioè fra le cose che non vanno caricate (vedi il passo 2). Il vostro resta dov'è e non va
toccato — con **una** eccezione, qui sotto.

⚠️ **L'unica riga da controllare nel vostro `appsettings.Production.json`** è `KeyRingPath`: deve puntare
alla cartella che esiste davvero, cioè
`"/var/www/vhosts/it.ivao.aero/public_atc/vipi-keys"`. Se ci fosse ancora `/var/lib/vipi/keys`
il sito non ripartirebbe — ma se sta girando adesso, è già giusta e non c'è niente da fare.

---

## La procedura, in cinque passi

L'ordine **non è indifferente**: i file si caricano per primi perché Passenger continua a servire la
versione vecchia finché non gli si dice di ripartire. Così la finestra in cui codice e database non si
capiscono dura secondi, non minuti.

### 1. Backup — prima di toccare qualunque cosa

```sh
# il database, come sta adesso
mysqldump -u itivao_atc -p --no-tablespaces --single-transaction --hex-blob itivao_atc \
  > backup-itivao_atc-prima-del-23-agosto.sql
```

E, via FTP, scaricate una copia di:

- `appsettings.Production.json`
- l'intera cartella `vipi-keys/`

Sono due file e una cartella piccola: è il paracadute che rimette tutto com'era se qualcosa va storto.

### 2. Caricare i file nuovi

Come la prima volta — vale tutto [`LEGGIMI-FTP.md`](LEGGIMI-FTP.md): **trasferimento binario**, e il bit di
esecuzione da rimettere su `Vipi.Host` e `createdump`.

Si sovrascrive il contenuto della cartella dell'applicazione **senza svuotarla prima** (vedi il riquadro
qui sopra: svuotandola sparirebbero configurazione e chiavi).

⚠️ **Se un file si rifiuta di essere sovrascritto** (`ETXTBSY`, «file in uso»): è l'applicazione viva che lo
tiene. Fermatela dal pannello Plesk (impostazioni .NET/Passenger del dominio), caricate, e ripartite col
passo 5.

⚠️ **La cartella `deploy/` NON va caricata.** Sono tre file di riferimento — l'unit systemd, la conf
nginx e `appsettings.Production.json.esempio` — che su Plesk+Passenger non servono a niente e che sul
server sarebbero solo superficie in più: il `.esempio` non contiene segreti, ma descrive nome del database,
nome dell'utente e percorso delle chiavi, e **non finisce per `.json`**, quindi la regola che nega
`appsettings*.json` non lo copre. Se dall'installazione di agosto c'è già una `deploy/` sul server,
si può cancellare.

ℹ️ Da caricare sono quindi **418 file**, in tre cartelle (`wwwroot`, `content`, `en`) più la radice. Se il
pannello sa scompattare uno `.zip`, caricate quello e poi togliete `deploy/`: è un trasferimento invece di
418, e non ci sono modalità di trasferimento da sbagliare.

### 3. Sostituire il database

⚠️ **Il `.sql` va importato in un database VUOTO.** Contiene la creazione delle tabelle: caricato sopra
quelle che ci sono già, si ferma al primo `CREATE TABLE` con «table already exists» e lascia l'archivio a
metà.

```sh
# 1. si svuota: si cancella e si ricrea, così non resta niente di vecchio
mysql -u itivao_atc -p -e "DROP DATABASE itivao_atc; CREATE DATABASE itivao_atc;"

# 2. si carica il nuovo
mysql -u itivao_atc -p itivao_atc < vipi-atc-it-ivao-aero-2026-08-23.sql
```

Da phpMyAdmin: selezionate il database → **Operazioni → Elimina il database**, ricreatelo con lo stesso
nome, poi **Importa** il file. ⚠️ Il file è **3,1 MB**: se phpMyAdmin ha un limite di caricamento più
basso, va per riga di comando.

ℹ️ Se l'utente `itivao_atc` non può fare `DROP DATABASE`, si svuota tabella per tabella — oppure ce lo dite
e vi mandiamo il `.sql` con i `DROP TABLE` in testa.

Il file porta con sé lo schema, i dati **e** la storia delle migrazioni: al riavvio l'applicazione trova
tutto a posto e **non applica niente**.

### 4. Controllare che le due cose importanti ci siano ancora

Prima di riavviare, via FTP: `appsettings.Production.json` c'è, e `vipi-keys/` contiene i suoi `.xml`.
Trenta secondi che valgono un pomeriggio.

### 5. Riavviare

```sh
# dalla cartella dell'applicazione
touch tmp/restart.txt
```

Da FileZilla: si carica un file `restart.txt` qualsiasi (anche vuoto) dentro `tmp/`, sovrascrivendo quello
che c'è.

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC (LIRR, LIMM, LIBB) |
| Il login IVAO | entra, e in alto compare il vostro nome |
| Una pagina di coordinamenti di un vIPI ACC | le tabelle ci sono e non sono vuote |
| `diagnostica/avvio-errore.txt` | **non deve esistere**. Se c'è, la prima riga dice la causa |

⚠️ **Se il sito non parte**, la pagina d'errore di Passenger è la diagnostica migliore che abbiate: mostra
utente, cartella, variabili e l'ultimo output del processo. Mandatecela così com'è — insieme a
`diagnostica/avvio-diagnostica.txt`, che dice con quale configurazione ha provato a partire e **non
contiene segreti** (di ogni valore dice *se* c'è, non quale).

---

## Cosa c'è dentro, rispetto al pacchetto del 15 agosto

- **Coordinamenti a sezioni** — un accordo per coppia di enti, sempre bidirezionale; arrivi, partenze e i
  due versi dei sorvoli stanno nelle sezioni dentro l'accordo. È il cambiamento che porta con sé il
  database.
- **Servizi ATC** — il sito è diventato il contenitore degli strumenti: c'è un hub `/services` e il vIPI
  vive sotto `/services/vsop`. Il primo strumento integrato è l'**Aurora Profile Swapper**.
- **Brand IVAO e tema chiaro/scuro**, scelto dall'utente dalla barra in alto.
- **Densità e telefono** — 17 pagine ripassate; le pagine pubbliche si leggono da 375px in su.
- **Vista live** — i coordinamenti stanno in una schermata invece che in 2835 pixel di scorrimento.
- **Audit frontend/UI del 23 agosto** — gerarchia dei titoli, Leaflet caricato solo dove serve, e 249
  selettori CSS che non potevano applicarsi a niente, tolti.

Compilato con gli avvisi trattati come errori: **0 avvisi**, **3595 test verdi**.

⚠️ Come i precedenti, **questo pacchetto non è mai stato eseguito su Linux**: è compilato in modo
incrociato da Windows. Il `.sql`, invece, è stato provato per davvero — reimportato in un database vuoto e
riletto dall'applicazione, con i conteggi confrontati tabella per tabella.
