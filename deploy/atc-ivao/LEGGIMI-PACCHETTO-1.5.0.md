# Pacchetto 1.5.0 — solo i file cambiati

> **Timbro:** `1.5.0 · a071575` (2 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella prima riga di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.4.1**, che è quello attualmente sul server. **13 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: non c'è niente da concordare con chi amministra il database, nessuna copia di
> sicurezza da fare, nessuna finestra da aspettare. Si carica quando volete.
>
> ⚠️ **Ma c'è una funzionalità nuova**, ed è per quello che il numero sale a **1.5.0** e non a 1.4.2: sulla
> pagina «Bozze e versioni» compare una sezione che prima non c'era. Vedi il punto 4.

---

## Che cosa cambia

### La parte che ripara — ed è la ragione per cui questo pacchetto va caricato

**1. L'editor non muore più aprendo un blocco «Allegato».**
Dalla diagnostica che ci avete mandato: **quindici errori**, tre tentativi diversi, e tutti e quindici dallo
**stesso punto** — il riquadro che sceglie un allegato dentro un documento. Ogni volta che la pagina si
ridisegnava, quel riquadro rileggeva l'elenco degli allegati **senza aspettare** la lettura precedente:
quattro letture insieme sulla stessa connessione al database, che si ammazzano a vicenda e si portano via la
pagina. Ora la lettura è **una sola**, e chi arriva dopo aspetta quella.

E se quella lettura non riesce, adesso **lo dice** invece di far morire la pagina: compare una riga d'errore
sopra la tendina. ⚠️ In quel caso **non** compare più il suggerimento «la biblioteca è vuota, caricane uno»,
che mandava a caricare un allegato che c'era già: una tendina vuota per un guasto e una vuota davvero sono
due cose diverse.

**2. Lo stesso difetto stava in altri due punti, e non erano protetti affatto.**
Il riquadro **«Validità e revisione»** (che sta in *ogni* documento, e in una vIPI ACC anche una volta per
blocco) e la resa delle **vLOA** rileggevano il database a **ogni** ridisegno della pagina. Non è mai
esploso in produzione, ma era lo stesso meccanismo: adesso leggono una volta e rileggono solo se cambia
davvero quel che stanno guardando.

### La parte nuova — preparare il ciclo AIRAC senza doverselo ricordare

**3. Le SID escono al ciclo giusto.**
Fino a ieri il ciclo di una SID importata dipendeva **dall'ora in cui era passato il giro automatico**:
stesso file del sectorfile, ma se il giro capitava qualche ora dopo il cambio ciclo la SID restava nascosta
**per un mese intero**, senza che niente lo dicesse. Adesso il ciclo lo **dichiara la sorgente** — il
sectorfile tiene un elenco delle sue versioni AIRAC — e non c'è più niente da indovinare.

ℹ️ Nell'editor dell'aeroporto la colonna a destra delle SID importate continua a dire da quale ciclo una
riga uscirà: adesso quel numero è quello vero.

**4. La sezione «Prossimo AIRAC», su «Bozze e versioni».** *(la novità che fa il 1.5.0)*
In cima alla pagina compare un riquadro **chiuso** che, aperto, dice: qual è il ciclo AIRAC che sta per
entrare, **quando** entra e quanti giorni mancano, quanti documenti hanno già una pubblicazione programmata
a quel ciclo e **quanti no**. E c'è un tasto che le programma **tutte insieme**.

ℹ️ Una pubblicazione programmata entra in vigore **da sola** alla data del ciclo: non serve tornare sul sito
il giorno del cambio. E la fotografia che salva è quella **di quel ciclo**, quindi contiene già le SID e i
confini che entrano allora.

⚠️ Se un documento è occupato da un altro redattore, o non avete i permessi su quella ACC, viene **saltato e
detto** — il giro non si ferma sugli altri.

**5. La lista «Da fare» avvisa PRIMA del cambio ciclo.**
Prima segnalava «c'è da ripubblicare» solo **il giorno dopo** che il ciclo era cambiato, cioè sempre tardi.
Adesso compare una riga **«da preparare»** mentre c'è ancora il tempo di programmare la pubblicazione.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-13-file-1.5.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1 | `Vipi.Host.dll` | il sito ⚠️ **è qui che sta il timbro `1.5.0`** |
| 2 | `Vipi.Host.pdb` | la sua mappa di debug: serve a far uscire il **numero di riga** negli errori |
| 3 | `Vipi.Ui.dll` | le pagine — la correzione dei tre riquadri e la sezione «Prossimo AIRAC» |
| 4 | `Vipi.Ui.pdb` | idem |
| 5 | `Vipi.Application.dll` | le regole: il ciclo entrante, la lista «da fare», il ciclo delle SID |
| 6 | `Vipi.Application.pdb` | idem |
| 7 | `Vipi.Infrastructure.dll` | il database e le sorgenti esterne (il changelog del sectorfile) |
| 8 | `Vipi.Infrastructure.pdb` | idem |
| 9 | `Vipi.Domain.dll` | i tipi di base (la voce nuova della lista «da fare») |
| 10 | `Vipi.Domain.pdb` | idem |
| 11 | `Vipi.Hosting.dll` | il montaggio dei servizi |
| 12 | `Vipi.Hosting.pdb` | idem |
| 13 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`** |

> ### 🟢 Questa volta NIENTE `wwwroot`, e quindi niente indice degli asset
>
> Nessun foglio di stile e nessun JavaScript sono stati toccati, quindi non ci sono né i file di `wwwroot`
> né `Vipi.Host.staticwebassets.endpoints.json` — e con essi sparisce la trappola del 24 agosto (l'indice
> caricato senza il suo file, o viceversa). Il caricamento è più semplice del solito.

> ### ℹ️ `appsettings.json` NON è nel pacchetto, ed è voluto
>
> Nel nostro codice quel file è cambiato (due chiavi nuove sotto `Sectorfile`), ma **resta fuori**: i valori
> nuovi sono **identici** a quelli che il programma userebbe comunque, quindi senza di lui non cambia
> niente. Così il pacchetto non tocca la configurazione che avete sul server — e una rinomina in meno.

ℹ️ **Non ci sono `Vipi.Infrastructure.MySqlMigrations.dll` né l'indice degli asset: non sono cambiati.** Le
impronte sono state **confrontate** con il pacchetto precedente, non dedotte. Ogni file in più è una
rinomina in più su un file che il processo tiene aperto: non è prudenza, è rischio.

ℹ️ Le impronte `sha256` di tutti e tredici sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo).
2. **Rinominate**, dal più profondo al più superficiale: prima `en/Vipi.Ui.resources.dll`, poi i `.pdb`,
   e **per ultimi i `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.5.0 · a071575`**. Se compare `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`**: la riga sotto il campo deve
> **cambiare**. ⚠️ Non usate lingua, zoom o tema: funzionano anche a sito morto.

> ### C · La correzione è arrivata
>
> Aprite un documento in modifica, andate su una sezione con un blocco **«Allegato»** e apritene uno.
> La tendina deve comparire con l'elenco, e la pagina deve continuare a rispondere.
> Se invece compare la barra in basso «Qualcosa è andato storto…», ricaricate e **mandateci
> `diagnostica/errori-richieste.txt`**.

> ### D · La novità si vede
>
> Aprite **«Bozze e versioni»** (`/services/vsop/versions`). Sotto il titolo deve esserci un riquadro
> chiuso **«Prossimo AIRAC»** con accanto il ciclo che sta per entrare e la sua data. Apritelo: deve dire
> quanti documenti hanno già una pubblicazione programmata a quel ciclo e quanti no.
>
> ℹ️ Il tasto «Programma i mancanti» **scrive**: premetelo solo se volete davvero programmare le
> pubblicazioni. Per il controllo basta vedere il riquadro.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi il
resto, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non tocca.

⚠️ Due note sul tornare indietro, tutt'e due piccole e tutt'e due da sapere:

- resta quella di 1.4.0: una **distanza di aeroporto alternato scritta coi decimali** non è leggibile dai
  binari di prima, e quella tabella sparirebbe a schermo;
- e una nuova: le **SID importate da questa versione** portano scritto il ciclo *da cui valgono* invece di
  quello in cui sono state prese. Binari vecchi le leggerebbero con la regola vecchia e ne mostrerebbero
  una in meno per un ciclo. **Nessun dato si perde**, e al giro d'import successivo si risistema.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente — sono due cose diverse: nel pacchetto il JavaScript
passa per l'ottimizzatore che lo **minifica**, e uno di quei file è l'unico che avvia il sito.

- build in Release sui due runtime, **0 avvisi**; **9836 test** verdi su **quindici** progetti, **E2E
  compresi** (289);
- ogni correzione ha il suo test, e ogni test è stato provato **rosso** rimettendo il difetto: senza quella
  prova un test così non guarda niente;
- il pacchetto **avviato davvero** (l'eseguibile, dalla sua cartella) e guidato in un browser:

| | |
|---|---|
| il timbro in barra | **`1.5.0 · a071575`**, e lo stesso in `diagnostica/avvio-diagnostica.txt` |
| il circuito | si apre, **col JavaScript minificato** (2 400 byte, 200) |
| la **Ricerca** | `LI` → **«50 results for LI»**, la pagina passa da 161 a 6 228 caratteri |
| «Prossimo AIRAC» | c'è, ed è **chiusa di suo**: «AIRAC 2609, in vigore dal 03 Sep 2026, fra un giorno, 16 da programmare» |
| vIPI d'aeroporto | si apre, il riquadro «Validità e revisione» rende le sue tre righe |
| vLOA | si apre |
| editor d'aeroporto | si apre, riquadro «Validità e revisione» a posto |
| console, rete, barra d'errore | **nessun errore**, nessun 4xx, la barra rossa resta nascosta |

⚠️ **Quel che NON è stato guidato dal vivo, e va detto:** il **blocco allegato** — cioè proprio il
riquadro che in produzione faceva morire l'editor. Nel database di sviluppo **nessun documento ne contiene
uno**, quindi a schermo non c'era niente da aprire. Quella correzione è coperta da **tre test** che
riproducono la corsa (una lettura trattenuta che conta quante ne partono), tutti e tre provati **rossi** col
codice di prima; e gli **altri due riquadri** con lo stesso difetto — «Validità e revisione» e la vLOA —
sono stati aperti davvero, qui sopra.

ℹ️ Per lo stesso motivo il **controllo C** di questo foglio è quello che vale di più: è l'unico posto in
cui quel riquadro si vede con dei dati veri.

ℹ️ **Non è stata rifatta** la prova del riavvio (processo ucciso → la pagina si ricarica da sola): questo
pacchetto non tocca niente della riconnessione, verificata a 1.4.1.
