# Pacchetto 1.4.1 — solo i file cambiati

> **Timbro:** `1.4.1 · 42f23f9` (2 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella prima riga di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.4.0**, che è quello attualmente sul server. **9 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, E NIENTE FUNZIONALITÀ NUOVE
>
> È una **correzione**: nessuna migrazione, nessuna pagina o sezione nuova. Si carica quando volete e si
> torna indietro senza conseguenze — a differenza di 1.4.0, qui non c'è nemmeno la nota sui decimali.

---

## Che cosa cambia

**1. L'editor non muore più mentre si aggiunge qualcosa.** *(la correzione)*
Segnalazione dal sito vero: aggiungendo una **sotto-sezione** compariva «A second operation was started on
this context» e la pagina smetteva di rispondere. Non era un difetto di quel tasto: erano **due
caricamenti della stessa pagina** che partivano insieme — il gesto ne fa partire uno, il ridisegno che
segue ne fa partire un altro — e due letture insieme sulla stessa connessione al database si ammazzano a
vicenda. Ora sulla pagina **passa una operazione per volta**.

**2. Quando qualcosa va storto, adesso lo si vede.** *(la parte che mancava)*
Fino a ieri, se la pagina di un documento moriva così, **non compariva niente**: la pagina restava a
schermo, i pulsanti smettevano di funzionare e basta. È la forma in cui è arrivata la seconda segnalazione
di stasera — «clicco su un pulsante qualsiasi e non succede nulla». Il messaggio c'era, ma solo sulle
pagine di servizio: **le pagine dei documenti non l'avevano**. Adesso ce l'hanno tutte, e dice anche la
cosa che serve sapere: **il lavoro salvato non si perde, basta ricaricare**.

**3. E quei guasti finiscono nel registro.** *(per noi)*
Un errore che avviene dentro una pagina interattiva non passava dal registro degli errori: in
`diagnostica/` non restava **niente**. Adesso ci finisce, con lo stack e con la fotografia di che cosa
stava usando il database in quel momento — che è l'unica cosa che permette di capire una collisione invece
di indovinarla.

ℹ️ **Se il problema si ripresenta**, quello che serve è: `diagnostica/errori-richieste.txt`. Da oggi c'è.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-9-file-1.4.1/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1 | `Vipi.Host.dll` | il sito ⚠️ **è qui che sta il timbro `1.4.1`**, e la nuova registrazione dei guasti |
| 2 | `Vipi.Host.pdb` | la sua mappa di debug: serve a far uscire il **numero di riga** negli errori |
| 3 | `Vipi.Ui.dll` | le pagine — è qui che stanno la correzione e la barra del messaggio |
| 4 | `Vipi.Ui.pdb` | idem |
| 5 | `en/Vipi.Ui.resources.dll` | le frasi in inglese (il messaggio nuovo) ⚠️ **è dentro la cartella `en`** |
| 6 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset **(insieme)** |
| 7 | `wwwroot/_content/Vipi.Ui/vipi-theme.css` | il foglio di stile **(insieme)** |
| 8 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.br` | la sua copia compressa **(insieme)** |
| 9 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.gz` | idem **(insieme)** |

> ### ⚠️ I file di `wwwroot` e l'indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` dice al sito **con quale nome** chiedere ogni file di
> `wwwroot`, impronta compresa. Caricare l'indice senza il file (o il file senza l'indice) fa chiedere al
> sito un nome che non esiste: pagine senza stile. È già successo il 24 agosto.

ℹ️ **Rispetto a 1.4.0 non ci sono `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Hosting`,
`Vipi.Domain`, né `vipi-ui.js` e `vipi-print.css`: non sono cambiati.** Le impronte sono state
**confrontate** con il pacchetto precedente, non dedotte. Ogni file in più è una rinomina in più su un
file che il processo tiene aperto: non è prudenza, è rischio.

ℹ️ Le impronte `sha256` di tutti e nove sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo).
2. **Rinominate**, dal più profondo al più superficiale: prima i tre file di `wwwroot/`, poi l'indice
   `staticwebassets`, poi `en/`, poi i `.dll`. ⚠️ I `.dll` per ultimi.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i tre controlli** qui sotto.

## I tre controlli, in un minuto

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.4.1 · 42f23f9`**. Se compare `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`**: la riga sotto il campo deve
> **cambiare**. ⚠️ Non usate lingua, zoom o tema: funzionano anche a sito morto.

> ### C · La correzione è arrivata
>
> Aprite l'editor di un vSOP, entrate in modifica e premete **«+ sotto-sezione» tre volte di seguito**: ne
> devono comparire tre, senza che la pagina smetta di rispondere e senza che il badge resti su
> «Salvataggio…».
>
> Se invece **compare una barra in basso** con «Qualcosa è andato storto…», quella è la novità numero 2 che
> fa il suo lavoro: ricaricate la pagina e **mandateci `diagnostica/errori-richieste.txt`**, che adesso
> contiene la spiegazione.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi
`wwwroot`, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non tocca.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente:

- build in Release sui due runtime, **0 avvisi**; **5085 test** verdi su nove progetti, **E2E compresi** (288);
- il pacchetto **avviato davvero** e guidato in un browser: timbro `1.4.1 · 42f23f9`, **Ricerca che
  risponde**, e «+ sotto-sezione» premuto **tre volte di fila** sull'editor ACC — tre sotto-sezioni, badge
  mai inchiodato, nessun errore in console;
- la barra del messaggio **esiste** ora anche sulle pagine dei documenti, e sta **nascosta** finché non
  serve (verificato nel DOM: c'era da capire proprio quello).
