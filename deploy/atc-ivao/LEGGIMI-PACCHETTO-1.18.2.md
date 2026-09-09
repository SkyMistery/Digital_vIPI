# Pacchetto 1.18.2 — solo i file cambiati

> **Timbro:** `1.18.2 · 578300a` (9 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.18.1.** **8 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> Nessuna migrazione, nessuna tabella e nessuna colonna nuova. Si carica quando volete, anche dentro la
> finestra cieca fino al 16.
>
> ## 🔴 MA QUESTA VOLTA C'È `wwwroot`, ed è la cosa da non sbagliare
>
> A differenza di 1.18.1, qui **un file di JavaScript è cambiato**: `vipi-ui.js`. Va caricato **con i suoi
> due compressi** (`.br` e `.gz`) **e insieme** a `Vipi.Host.staticwebassets.endpoints.json`.
>
> ⚠️ **Perché insieme:** quell'indice dice al sito con che nome chiedere ogni file. Caricarne uno solo dei
> due fa chiedere al browser nomi che non esistono.
>
> 🔴 **E se il JavaScript non arriva, il pacchetto sembra funzionare lo stesso**: il timbro è giusto, tre
> correzioni su quattro ci sono, e **il rimbalzo alla home resta identico a prima**. È l'errore facile da
> fare in questa consegna, e non darebbe nessun segnale.

---

## Che cosa correggono questi otto file

Tutt'e quattro le cose nascono dalle **due segnalazioni sull'unione di Gioia del Colle**. ⚠️ E nessuno dei
due sintomi aveva la causa che sembrava avere: è per questo che i difetti sono quattro e non due.

### 🟢 1. Non si viene più buttati alla home spostando un documento

Era la segnalazione: *«dopo che sposto un documento verso l'alto o il basso nell'unione vengo rimandato
direttamente alla home senza che clicchi nulla»*.

In una riga: nel menu delle sezioni ogni voce è un collegamento **interno alla pagina**. Il sito ha una
protezione che gli impedisce di essere trattato come un indirizzo vero — ma quella protezione **si sfilava
proprio quando la sezione non era ancora comparsa a schermo**, e in quel momento il collegamento veniva
letto come «vai alla pagina iniziale».

✅ **Misurato**: sul pacchetto di prima, il clic su una voce la cui sezione non c'è ancora porta da
`/services/vsop/libb/airports/editor` a **`/services`**. Sul pacchetto nuovo la pagina **resta dov'è**.

⚠️ **Perché succedeva riordinando i membri di un'unione.** Riordinando, i documenti si ricaricano e per
qualche istante una sezione può non essere ancora disegnata. Da 1.18.1 quel momento **dura di più**, non di
meno: i documenti uniti si caricano **uno per volta** invece che tutti insieme — è la correzione che ha
tolto i blocchi di pagina, e il prezzo pagato è proprio quello.

ℹ️ **Ed è anche la risposta a «il caricamento quando viene unito non è fluido»**: non è un difetto nuovo, è
quel caricamento in fila. Con tre documenti i tempi si sommano invece di sovrapporsi.

Adesso un collegamento interno che non trova la sua sezione **non fa niente**: la pagina resta dov'è.

### 🟢 2. La ✕ per togliere un documento ora apre la sua conferma

Era l'altra segnalazione: *«la clicco e non mi apre nemmeno la conferma»*.

Il tasto non era rotto: era **spento**. Il pannello dell'unione si spegne da solo mentre sta lavorando — è
giusto, evita due comandi insieme — e si riaccende quando ha finito. Ma se il lavoro non finiva **mai**,
non si riaccendeva mai: nessun errore, nessun messaggio, tutti i comandi dell'unione grigi.

Adesso quell'attesa ha un **limite di trenta secondi**. Scaduto, il comando **dice che non è riuscito** e i
tasti tornano attivi: si riprova, o si ricarica la pagina. Un errore visibile invece di un pannello morto.

### 🟢 3. La pagina non se ne va più da sola durante un ricarico

Un caso raro e diverso dal primo: su un campo **solo militare senza vIPI civile**, l'editor d'aeroporto è
fatto per mandarvi all'edizione militare — che è giusto **quando si entra**. Quel rimando però veniva
rivalutato **a ogni ricarico**, quindi anche in mezzo a un lavoro sull'unione.

Adesso si decide **una volta sola, all'ingresso**, e un documento montato **dentro** l'unione di un altro
non può più portare via la pagina di chi lo ospita.

### 🟡 4. Un guasto che finora non lasciava tracce ora le lascia

Nel registro di produzione c'erano otto errori di disegno della pagina che, quando capitano, **fanno
smettere di rispondere tutto** — lo stesso effetto dei difetti qui sopra, ma per un'altra ragione.

⚠️ **Questa non è una correzione**: quel guasto non è ancora spiegato. Quel che cambia è che d'ora in poi
**si porta dietro il contesto** invece di lasciare una riga muta. Se ricapita, il prossimo
`errori-richieste.txt` dirà finalmente perché — e a quel punto si chiude.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni.

1. **La Ricerca**: si apre `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire
   **«N risultati per li»** con l'elenco sotto. È il controllo che conta, perché passa dal **server**.
2. 🔴 **Il gesto di questa consegna** (serve il login): si apre l'editor unito di **Gioia del Colle**, e si
   sposta un membro **su e giù** con le frecce. La pagina **deve restare dov'è**. Poi la **✕** su un
   membro: deve aprire la **conferma dentro la riga**.
3. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori, qualcosa è andato storto nel
   caricamento degli asset.
4. 🔴 **Un menu delle sezioni**: si clicca una voce e la pagina deve **scorrere** a quella sezione,
   restando sullo stesso indirizzo. È il controllo che dice che il JavaScript nuovo è arrivato **e
   funziona** — se finite sulla pagina dei servizi, il file non è stato caricato.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

⚠️ **Il prossimo `errori-richieste.txt` è la prova che conta**, e non è a schermo. Mandatecelo quando
potete: è l'unico modo di sapere se l'errore di disegno del punto 4 ricapita, e stavolta arriverebbe **con
la sua spiegazione**.

---

## Gli 8 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Ui.dll` + `.pdb` | gli editor, il pannello dell'unione, l'attesa col limite e la rete diagnostica |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
| `Vipi.Host.staticwebassets.endpoints.json` | 🔴 l'indice degli asset: va **insieme** al file qui sotto |
| `wwwroot/_content/Vipi.Ui/vipi-ui.js` (+ `.br`, `.gz`) | 🔴 il collegamento interno che non porta più alla home |

⚠️ `Vipi.Application.dll` e `Vipi.Infrastructure.dll` **non** sono in questo pacchetto, ed è corretto:
nessuno dei due è cambiato, e nessuna firma condivisa è stata toccata.
