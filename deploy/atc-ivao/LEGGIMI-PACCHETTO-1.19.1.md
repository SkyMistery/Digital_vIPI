# Pacchetto 1.19.1 — solo i file cambiati

> **Timbro:** `1.19.1 · c58ad07d` (10 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.19.0.** **7 file** — il pacchetto più piccolo da mesi.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE MIGRAZIONE, NIENTE `wwwroot`
>
> **Il database non si tocca.** Nessuna colonna nuova, nessuna tabella: lo schema resta esattamente quello
> di 1.19.0, e `Vipi.Infrastructure.MySqlMigrations.dll` **non è in questo pacchetto** perché non c'è niente
> da migrare.
>
> **E non c'è nessun file di `wwwroot`.** Nessun `.css` e nessun `.js` è stato toccato — verificato
> confrontando l'impronta sha256 dei **54** file di `_content/Vipi.Ui` con quelli di 1.19.0: identici, e
> identico anche `Vipi.Host.staticwebassets.endpoints.json`. Quindi qui **non** vale l'avvertimento delle
> altre volte sui compressi `.br`/`.gz` e sull'indice: non c'è niente da tenere insieme.
>
> C'è invece **`en/Vipi.Ui.resources.dll`**: le frasi inglesi hanno una riga in più (la segnalazione nuova
> del convertitore). Senza, quella frase si legge in italiano anche a chi usa l'inglese.
>
> **I sette file:**
>
> ```
> Vipi.Application.dll     Vipi.Application.pdb
> Vipi.Host.dll            Vipi.Host.pdb
> Vipi.Ui.dll              Vipi.Ui.pdb
> en/Vipi.Ui.resources.dll
> ```
>
> ⚠️ I `.pdb` servono davvero: senza, le voci di `diagnostica/errori-richieste.txt` perdono il **numero di
> riga**, ed è metà di quello che serve per capire un guasto.

---

## Che cosa portano questi sette file

### 🔴 1. Un login che va storto ora lascia una riga nel registro

Era la segnalazione del 10 settembre: *«uno staffista ha fatto il login verso le 13:00 e gli è uscita la
pagina d'errore»*. Aperto `diagnostica/errori-richieste.txt`: per quell'ora **non c'era niente**.

**E il file era quello giusto.** Fresco (impronta diversa dalla copia della sera prima) e la cartella
scrivibile — `avvii.txt` e `avvio-diagnostica.txt` avevano scritto le loro righe nella stessa cartella lo
stesso giorno. La riga non mancava: **non era mai stata chiesta**.

Il perché sono due strade che portano alla **stessa** pagina, e nessuna delle due scriveva:

1. **Il guasto del giro di login con IVAO era «gestito».** Veniva raccontato solo ai log del processo — che
   su questo server **non li legge nessuno**, perché non c'è né shell né pannello. Il registro degli errori
   è nato ad agosto proprio per un login rotto, ed era rimasto l'unico guasto che non ci finiva dentro.
2. **La pagina d'errore si raggiunge anche a piedi.** È un indirizzo come un altro: `/Error`. Se qualcuno ci
   arriva per un link o per il tasto «indietro», la pagina è **identica** a quella di un guasto vero. Da
   fuori, «la riga non si è scritta» e «non c'era niente da scrivere» non si distinguono — e portano a due
   indagini opposte.

**Da adesso** il registro dice l'una e l'altra cosa: un login fallito ci finisce col suo motivo (il portale
ha detto no / lo stato del giro non è tornato / il *nonce* / sconosciuto), e una pagina d'errore raggiunta
senza nessun guasto lascia una riga che comincia con `NOTA` e lo dichiara.

⚠️ **Nel file non finisce niente di segreto**, e c'è stata cura: l'indirizzo da cui si arrivava viene
scritto **senza la parte dopo il `?`**, perché sul ritorno del login lì dentro c'è una credenziale — e
questo file si spedisce per email.

🔴 **Del guasto del 10 settembre non sapremo il motivo**: è passato prima di questa rete. Se ricapita, la
riga c'è.

### 🟢 2. Il convertitore di coordinate legge quel che esce dall'AIP

Due segnalazioni, tutt'e due incollando dall'AIP italiana in `/services/coordinates`:

**a) I secondi scritti con due apostrofi.** Dall'AIP le coordinate escono così:

```
41°07'24''N,018°52'12''E
```

I secondi non hanno la virgoletta `"`, hanno **due apostrofi**. Il convertitore non li riconosceva: quel
pezzo smetteva di essere una coordinata e diventava un'**etichetta**, cioè il punto **spariva** — e niente
sembrava rotto, c'erano solo meno punti di quelli incollati. Ora si leggono, e con loro anche gli apostrofi
curvi di Word (`’’`), i simboli tipografici (`′ ″`) e la `º` che certi PDF usano al posto del grado.

**b) Una riga bianca fra un vertice e l'altro.** Sempre dall'AIP, un'area viene fuori così — dieci vertici,
con una riga vuota in mezzo a ciascuno:

```
40°20'00"N 008°10'00"E;

40°20'00"N 008°15'00"E;

39°45'20"N 008°43'10"E;
```

Il convertitore lo leggeva come **dieci aree da un punto**: dieci puntini sulla mappa e nessun poligono. La
riga vuota separa due aree — regola giusta, e resta — ma **un punto solo non è un'area**. Ora, quando
nessun blocco arriva a due punti, quelle righe vuote si trattano per quel che erano: spaziatura.

⚠️ **Due elenchi veri separati da una riga bianca restano due aree**, come prima. E il convertitore **lo
dice** quando riunisce i blocchi: compare la riga «righe vuote ignorate», così non è una magia silenziosa.

---

## Dopo il caricamento

1. Aprite `https://atc.it.ivao.aero/services/vsop` e controllate il timbro in alto: **`1.19.1 · c58ad07d`**.
2. ⚠️ **Non fermatevi al timbro.** Provate la **Ricerca** (`/services/vsop/search`, due lettere): passa dal
   server, e un caricamento incompleto darebbe un sito che si vede intero e non risponde a niente — col
   timbro giusto lo stesso.
3. **La prova vera di questo pacchetto** è il convertitore, e si fa in trenta secondi da amministratore:
   `/services/coordinates`, incollate l'area di dieci vertici qui sopra **con le righe vuote** e i secondi
   scritti con due apostrofi. Deve uscire **un'area sola da dieci vertici**, chiusa, disegnata sulla mappa —
   e sotto la riga «righe vuote ignorate».
4. Del punto 1 non c'è niente da guardare adesso: si vedrà **la prossima volta** che un login va storto, e
   si guarda in `diagnostica/errori-richieste.txt` cercando un codice che comincia con `login-`.
