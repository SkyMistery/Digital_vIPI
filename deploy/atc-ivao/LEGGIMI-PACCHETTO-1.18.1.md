# Pacchetto 1.18.1 — solo i file cambiati

> **Timbro:** `1.18.1 · ba16e1c` (9 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.18.0**, online da questo pomeriggio. **6 file** — il pacchetto più piccolo da mesi.
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
> ## 🟢 E niente `wwwroot`, per la prima volta da un po'
>
> Nessun foglio di stile, nessun JavaScript, **e nessun `endpoints.json`**. Non è una dimenticanza: le due
> cartelle pubblicate sono state confrontate **per impronta, intere** — su 90 file gli asset diversi sono
> **zero**, e l'indice è **identico**. Un file in meno da caricare è un rischio in meno.
>
> ## ℹ️ E nemmeno il file delle frasi inglesi
>
> Nessun testo è cambiato, quindi `en/Vipi.Ui.resources.dll` resta fuori.

---

## Che cosa correggono questi sei file

Tutt'e due le cose che avete visto **unendo il terzo documento** (`LIBV_APP`) alla vIPI e al vSOP di Gioia
del Colle.

### 🟢 1. La pagina non si blocca più

Era il difetto grosso: unito il terzo documento, **la pagina smetteva di rispondere a qualunque comando** —
niente «sciogli l'unione», niente «togli un documento», niente. Non serviva nemmeno insistere: era finita lì.

Che cosa succedeva davvero, in una riga: due letture dal database partivano **insieme** invece che una dopo
l'altra, e quando è capitato in mezzo a un ridisegno della pagina, il ridisegno è morto a metà. Da quel
momento il collegamento fra il vostro browser e il server **non era più allineato**, e ogni clic cadeva nel
vuoto. Non è che il tasto fosse rotto: non c'era più nessuno che lo ascoltasse.

⚠️ **Perché con tre documenti e non con due.** È una questione di tempi. Con più membri la pagina si
ridisegna più spesso, e sul database del server le letture durano abbastanza da sovrapporsi. Sulla copia di
prova, che è molto più veloce, finivano prima di darsi fastidio — ed è esattamente il motivo per cui in
locale non si vedeva.

ℹ️ **Riguarda tutti e cinque gli editor**, non solo quello unito: la parte corretta è comune. Dove non
c'erano unioni il difetto era molto più raro, ma la strada era la stessa.

### 🟢 2. La scheda «sezioni in comune» non propone più cose che non sono ripetizioni

Aggiungendo l'APP, la scheda si riapriva e proponeva — **già spuntate** — sezioni che con l'APP non hanno
niente a che vedere.

Il motivo: la scheda confrontava le sezioni **per nome interno**, e un APP e la vIPI di un aeroporto ne
hanno tre uguali di nome: **Frequenze**, **Procedure generali**, **Validità e revisione**. Ma le frequenze
di un APP sono quelle **dell'avvicinamento**, e quelle dell'aeroporto sono **del campo**: non è la stessa
cosa scritta due volte. Chi premeva «applica» senza guardare non toglieva un doppione — **nascondeva
contenuto vero**.

Adesso al confronto partecipano **solo i documenti che descrivono lo stesso luogo**: la vIPI d'aeroporto e
il vSOP militare di quello scalo. Quindi:

- fra le caselle «nascondi in» **l'APP non compare più**, perché non c'è niente da nascondere;
- unendo vIPI + vSOP dello stesso campo la scheda **funziona esattamente come prima**;
- **la scheda non si apre più da sola quando non ha niente da chiedere.** Il suo tasto resta: chi la vuole
  aprire la apre.

ℹ️ Vale anche per i **due APP di Gioia** (`LIBV_APP` e `LIBV_G_APP`): settori diversi, frequenze diverse,
nessuna ripetizione da togliere.

---

## ⚠️ Una cosa da dire, perché non si legga il contrario

**Questi due difetti non li ha portati 1.18.0**, e non è un'opinione: nel file di diagnostica che ci avete
mandato l'ultima riga è delle **14:11:39**, e 1.18.0 è partita alle **14:13:21**. Tutto quello che c'è
dentro è di **prima** del caricamento. Il codice corretto qui 1.18.0 non l'aveva nemmeno toccato: erano lì
da prima, e si vedevano solo unendo tre documenti.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni.

1. **La Ricerca**: si apre `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire
   **«N risultati per li»** con l'elenco sotto. È il controllo che conta, perché passa dal **server**.
2. **L'unione a tre** (serve il login), che è il motivo di questo pacchetto: si rifà il gesto di stamattina
   — vIPI + vSOP di Gioia, poi si aggiunge `LIBV_APP`. Devono succedere **due** cose: la scheda delle
   sezioni in comune **non si apre da sola**, e la pagina **continua a rispondere** — «sciogli l'unione» e
   «togli un documento» devono funzionare.
3. **La scheda dove serve ancora** (serve il login): su un'unione di **soli** vIPI + vSOP dello stesso
   scalo, il tasto delle sezioni in comune deve aprire la scheda con METAR, frequenze, piste — come prima.
4. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori qualcosa è andato storto nel
   caricamento, perché questo pacchetto **non tocca** nessun foglio di stile.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

⚠️ **Il prossimo `errori-richieste.txt` è la prova che conta**, e non è a schermo: le voci
**«A second operation was started»** che nominano `AppSectionsEditor` devono **sparire**. Mandatecelo
quando potete — è l'unico modo di sapere se la correzione ha chiuso davvero, perché il difetto non si
riproduce sul banco di prova.

---

## I 6 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Application.dll` + `.pdb` | chi decide quali documenti si confrontano davvero fra loro |
| `Vipi.Ui.dll` + `.pdb` | gli editor che non fanno più partire due letture insieme, e la scheda delle sezioni in comune |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
