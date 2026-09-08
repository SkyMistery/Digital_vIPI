# Pacchetto 1.16.0 — solo i file cambiati

> **Timbro:** `1.16.0 · a8b54c8` (8 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.15.2**, online dall'8 settembre. **16 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> Nessuna migrazione, nessuna tabella e nessuna colonna nuova: tutto quel che si aggiunge vive dentro il
> testo dei documenti. Si carica quando volete, anche dentro la finestra cieca fino al 16.
>
> ## 🔴 QUESTA VOLTA `wwwroot` C'È, E I FILE VIAGGIANO INSIEME
>
> Cambiano **due** file di `wwwroot` — `vipi-theme.css` e `vipi-ui.js` — e ognuno va caricato **coi suoi
> `.br` e `.gz`**, **insieme** a `Vipi.Host.staticwebassets.endpoints.json`. Quell'indice dice al sito con
> che nome chiedere ogni file: caricarne uno e non l'altro fa chiedere nomi che non esistono, e la pagina
> esce senza stile. È il difetto del 24 agosto.
>
> ⚠️ E qui non è una formalità: **la finestra che ingrandisce le immagini vive dentro `vipi-ui.js`**. Senza
> quel file il clic sull'immagine semplicemente non fa niente, e nessun errore lo dice.
>
> ## ⚠️ Ci sono frasi nuove
>
> `en/Vipi.Ui.resources.dll` entra: ci sono etichette nuove (le note delle aree, i poligoni di tiro, la
> finestra delle immagini, l'opzione del convertitore). Senza quel file, chi legge in inglese le vedrebbe
> in italiano.

---

## 🔴 Tre difetti segnalati, e adesso chiusi

### 1. Nell'editor SID la casella «APP» non si poteva spuntare

Nella colonna *Initial climb* comparivano **tre puntini** al posto della casella. Non era la casella: era
la colonna, troppo stretta per il campo **più** la casella, e quel che avanzava veniva tagliato via.

Adesso il campo si stringe da sé per far posto alla casella, che **non può più sparire**. E c'è una cosa in
più che prima non c'era: **spuntando «APP» il campo dei piedi si spegne** e il valore si cancella — se la
quota la assegna l'avvicinamento, una quota fissa scritta accanto direbbe due cose insieme.

### 2. Nella tabella delle configurazioni un settore incluso spariva

Sul documento di Milano, la configurazione che apre la sola `LIMF_WW0_APP` diceva che accorpa `LIMF_WN0_APP`
ma **non** `LIMJ_WS0_APP`, benché nell'albero fossero tutt'e due figli di WW0.

Dipendeva dall'**ordine** in cui i settori sono elencati nella sezione: un figlio elencato *dopo* il padre
cancellava il risultato giusto. Adesso l'ordine non conta più.

ℹ️ **Da guardare dopo il caricamento**: aprite quella sezione e verificate che la configurazione elenchi
tutt'e tre. Non serve toccare niente — è una derivazione, si ricalcola da sé.

### 3. Nel convertitore di coordinate mancava l'ultima riga del poligono

Convertendo un'area, la riga finale — quella uguale alla prima, che **chiude** il poligono — non veniva
scritta, e **webeye rifiutava la forma**.

Adesso c'è, e sotto le opzioni compare una casella **«Ripeti il primo punto in fondo (chiusura)»**, già
spuntata. Chi incolla nel sectorfile, dove quella riga è di troppo, la toglie con un clic.

⚠️ Una **costa** (`COAST`) non viene mai chiusa: è una linea aperta, e chiuderla inventerebbe un tratto che
non esiste.

---

## 🟢 Tre cose nuove

### Le aree di lavoro del vSOP militare: quindici attività, e le note

Nella tabella sotto la mappa, i tipi di attività erano due (`A/A`, `A/G`). Adesso sono **quindici**: `EW`,
`AEW`, `RPA`, `AAR`, `HAAR`, `CAP`, `CAS`, `ASW`, `SFO`, `TEST FLIGHTS`, `TRAINING`, `PARA`, `LOW LEVEL`.

⚠️ **Il doppio clic non prende più tutte le attività**: con due voci era una scorciatoia, con quindici
scriverebbe un'area dove si fa *tutto*. Adesso si accendono una per una.

E c'è una **colonna «Note»**, libera, una per area: serve a spiegare una procedura particolare, cioè quel
che i gettoni non sanno dire. Chi non ha niente da scrivere la lascia vuota.

ℹ️ **Quel che era già scritto si rilegge**: le attività salvate prima restano dov'erano.

### I poligoni di tiro si riconoscono a colpo d'occhio

Le aree che la sorgente IVAO marca come *weapon range* adesso hanno una **targhetta** e la riga **tinta**
nella tabella, e sulla mappa sono **arancioni** invece del colore del loro tipo — perché nei dati veri stanno
sotto R, D e TRA insieme, e il colore del tipo non le distingueva.

ℹ️ Si vede anche nelle **vIPI civili** (ACC e APP): è la stessa mappa.

### Cliccando un'immagine si apre a dimensioni originali

In qualunque documento, il clic su un'immagine la riapre in una finestra sopra la pagina, **grande com'è
davvero**, con la misura in pixel e un tasto «Adatta allo schermo». Si chiude con `Esc`, con la ✕ o
cliccando fuori.

Serviva da quando le immagini si possono stringere: una carta al 30% della colonna non si legge.

ℹ️ Nell'**editor** il clic non apre niente, ed è voluto: lì trascinare l'immagine è il gesto che ne cambia
la larghezza.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni: un caricamento incompleto dà un sito che si vede intero e non risponde a niente.

1. **La Ricerca**: si scrive nel campo in alto e si preme **Invio**, oppure si apre direttamente
   `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire **«N risultati per li»** con
   l'elenco sotto. È il controllo che conta, perché passa dal **server**.
   ⚠️ Non basta scrivere nel campo e guardare: quel campo **naviga**, non filtra la pagina sotto. Se si
   resta fermi a guardarlo si conclude che la ricerca non risponde anche quando risponde — misurato
   provando questo stesso pacchetto.
2. **Un'immagine in un documento**: cliccarla deve aprire la finestra. Se non succede, manca
   `vipi-ui.js` o il suo indice.
3. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori, manca `vipi-theme.css` o l'indice.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

---

## I 16 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Application.dll` + `.pdb` | il motore: configurazioni, aree, attività e note, convertitore |
| `Vipi.Infrastructure.dll` + `.pdb` | la lettura e la scrittura di quel che sta nel database |
| `Vipi.Ui.dll` + `.pdb` | le pagine e gli editor |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
| `en/Vipi.Ui.resources.dll` | le frasi inglesi |
| `Vipi.Host.staticwebassets.endpoints.json` | l'indice dei file di `wwwroot` — **va insieme ai due qui sotto** |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` (+ `.br`, `.gz`) | lo stile |
| `wwwroot/_content/Vipi.Ui/vipi-ui.js` (+ `.br`, `.gz`) | l'interattività delle pagine di lettura, **finestra delle immagini compresa** |
