# Pacchetto 1.17.0 — solo i file cambiati

> **Timbro:** `1.17.0 · 30ca658f` (9 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.16.1**, online dall'8 settembre sera. **11 file.**
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
> ## ⚠️ `wwwroot`: UN file, ma va coi suoi
>
> Cambia **solo** `vipi-theme.css`, e va caricato **coi suoi `.br` e `.gz`**, **insieme** a
> `Vipi.Host.staticwebassets.endpoints.json`. Quell'indice dice al sito con che nome chiedere ogni file:
> caricarne uno e non l'altro fa chiedere nomi che non esistono, e la pagina esce senza stile.
>
> ℹ️ **`vipi-ui.js` NON c'è, ed è giusto**: è identico a quello di 1.16.1 — verificato per impronta, non a
> memoria.
>
> ## ⚠️ Ci sono frasi cambiate
>
> `en/Vipi.Ui.resources.dll` entra: la conferma del tasto nuovo ha tre frasi. Senza quel file, chi legge in
> inglese vedrebbe la domanda di conferma **senza testo**.
>
> ## ℹ️ `Vipi.Infrastructure.dll` NON c'è, e stavolta è giusto così
>
> Nelle ultime tre consegne c'era. Qui non è stato toccato niente in quel pezzo, e ogni file in più è una
> rinomina in più su un file che il processo tiene aperto.

---

## 🟢 La novità che si vede: il ✕ sulla riga di un coordinamento

Nella pagina **Trasferimenti**, sulla riga di ogni clausola, dopo i tasti `⑂ ↳ ⧉ ✎` adesso c'è un **✕**.
Prima per eliminare una riga bisognava **aprirla** nel pannello di destra, premere il ✕ lì e richiudere:
tre gesti per uno.

⚠️ **Il ✕ si porta via anche le eccezioni di quella riga**, e la domanda di conferma **lo dice e le conta**:
«Eliminare la riga e le sue **2** eccezioni?». Non è una scorciatoia: un'eccezione esiste **solo** rispetto
alla riga da cui pende. Lasciandola indietro non resterebbe una riga in più — resterebbe una clausola
**qualunque**, che nel documento pubblicato varrebbe **sempre**. Sarebbe l'accordo a cambiare, in silenzio.

ℹ️ Una riga che **scavalca le alternative** (vale per tutto il gruppo) non se ne va con nessuna di loro:
cade solo se si elimina lei.

ℹ️ **Si può annullare**: dopo l'eliminazione compare «Annulla», e rimette **tutte** le righe, non solo la
prima.

ℹ️ Anche il ✕ del **pannello** adesso si comporta così: prima cancellava la sola riga, e due tasti che
fanno la stessa cosa con due regole diverse sono il modo in cui prima o poi si sbaglia.

---

## 🟢 Il resto: tre cose che non si vedono, e che vengono dal registro degli errori

Questo pacchetto nasce dal file di diagnostica che ci avete mandato l'8 sera. Diceva che le correzioni
caricate poche ore prima **non avevano chiuso** il problema, e diceva anche dove.

**1. Le pagine adesso aspettano prima di chiudere.** Quando si cambia pagina mentre una lettura dal database
è ancora in volo, la pagina si portava via la connessione **sotto** quella lettura. L'effetto arrivava
addosso a **un altro utente**, non a chi se n'era andato: un errore in una pagina che non c'entrava niente.
Adesso la pagina chiude la porta e **aspetta** chi è ancora dentro (al massimo quindici secondi).

**2. La pagina dei Permessi** faceva la stessa cosa, e in un pomeriggio è finita due volte nel registro.
Adesso lavora su una connessione sua.

**3. L'errore intermittente dell'editor APP** non è ancora chiuso, e va detto: capita di rado, la pagina si
ricarica e non si perde niente. Quello che questo pacchetto aggiunge è che **la prossima volta l'errore dirà
dov'era** — finora arrivava muto, ed è la ragione per cui è ancora aperto.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni.

1. **La Ricerca**: si apre `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire
   **«N risultati per li»** con l'elenco sotto. È il controllo che conta, perché passa dal **server**.
2. **Il ✕ sulla riga** (serve il login): Trasferimenti → un ACC → un accordo → si prende la modifica, e su
   una riga qualsiasi della tabella devono esserci **cinque** tasti, l'ultimo dei quali `✕`. Premendolo si
   apre la domanda **dentro la riga**, per intero, senza uscire dal bordo della tabella.
3. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori, manca `vipi-theme.css` o l'indice.
4. **Il sito in inglese** (`EN` in alto): la domanda di conferma del punto 2 deve avere il testo. Se è vuota,
   il `.dll` delle frasi non è arrivato.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

⚠️ **Il prossimo `errori-richieste.txt` è la prova che conta**, e non è a schermo: se le correzioni hanno
funzionato, le voci `ObjectDisposedException` devono **calare**. Mandatecelo quando potete.

---

## Gli 11 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Application.dll` + `.pdb` | quali righe pendono da una riga: quel che il ✕ si porta via |
| `Vipi.Ui.dll` + `.pdb` | il tasto ✕ e la sua conferma, le pagine che aspettano, i Permessi |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
| `en/Vipi.Ui.resources.dll` | le frasi inglesi (le tre della conferma) |
| `Vipi.Host.staticwebassets.endpoints.json` | l'indice dei file di `wwwroot` — **va insieme ai tre qui sotto** |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` (+ `.br`, `.gz`) | lo stile, con la conferma che sta dentro la riga |
