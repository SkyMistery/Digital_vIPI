# Pacchetto 1.7.0 — solo i file cambiati

> **Timbro:** `1.7.0 · bf249155` (4 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.6.2**, che è quello attualmente sul server. **18 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete.
>
> ⚠️ **Ma questa volta c'è `wwwroot`**, e con esso l'**indice degli asset**. I due viaggiano **insieme**:
> vedi il riquadro più sotto. E ci sono le **frasi inglesi** (`en/`), che in 1.6.2 non c'erano.

---

## Che cosa cambia per chi usa il sito

### 1 · Le sezioni dei documenti si **muovono** davvero

Nei cinque editor, una sezione **libera** (quelle che aggiungete voi, non quelle del catalogo) ora può
**cambiare posto nell'albero**: c'è una tendina **«⇵ Sposta in…»** accanto al titolo, e nel menu-sezioni di
sinistra si può **trascinare** una sotto-sezione su un'altra per spostarla lì.

Nello stesso lavoro sono stati corretti due difetti **vecchi**, che c'erano da prima:

- una sotto-sezione marcata **«sopra il contenuto»** usciva **sotto** quando il corpo lo disegna la pagina
  (le frequenze, le piste, le quote di transizione del vSOP militare): l'editor mostrava una cosa e il
  documento pubblicato un'altra;
- nella **vIPI ACC** una sotto-sezione **nascosta** spariva anche dall'**anteprima di bozza**, cioè proprio
  dove si lavora. Ora si vede, marcata «nascosta», e resta fuori dal pubblico.

> ### ⚠️ I documenti già pubblicati non cambiano da soli
>
> Le due correzioni qui sopra valgono sui documenti di **lavoro**. Una release è una **fotografia**, e non
> si riscrive mai: un documento pubblicato mostrerà la correzione **solo dopo una nuova pubblicazione**.
> Non c'è niente da fare al caricamento — è una cosa da sapere, non da eseguire.

### 2 · La testata dei documenti si compatta

Sopra ogni documento c'erano titolo, un blocco di tre bottoni e un riquadro pieno per la lingua: ora sono
**due righe**. Vale su tutte e cinque le famiglie. ⚠️ È il motivo per cui questo pacchetto porta i **fogli di
stile**.

### 3 · Due sezioni nuove nei documenti di scalo

Al **primo avvio** l'applicazione sistema da sé i documenti già scritti:

- **«Carte aeroportuali»** con Aerodromo, Carte di avvicinamento strumentale, SID, STAR, VFR, subito prima
  di «Validità e revisione» — su vIPI d'aeroporto e vSOP militare;
- i **Parcheggi** passano da «Procedure di terra» a «Dati generali».

ℹ️ Succede da solo, una volta, ed è **idempotente**: al secondo avvio non c'è più niente da fare. Sulla
nostra copia del vostro database ha aggiunto **84 sezioni** (14 documenti × 6) e nient'altro.

### 4 · «Da un altro documento…» nell'import tabelle

Non era rotto: **taceva**. Se il documento scelto non ha righe in quella tabella — ed è il caso normale
finché i vSOP sono da riempire — non succedeva niente e non compariva nessun messaggio. Ora lo dice.

### 5 · 🔴 La misura che ci serve per il sito che si spegne

**Questa è la parte che vi chiediamo di guardare dopo.** Il registro `diagnostica/avvii.txt` adesso scrive,
su ogni riga di `ARRESTO`, **quante richieste ha servito quel processo** e **chi** gli ha chiesto di
spegnersi:

```
2026-09-04 06:06:07Z  ARRESTO  acceso per 00:02:44   richieste 17, ultima 7s fa, svegliato da /vsop/health · fermato da SIGTERM
```

Perché serve: il processo si spegne **ogni cinquanta secondi circa**, e il keep-alive che lo interroga ogni
dieci secondi lo **riaccende** ma non lo tiene su. Le due spiegazioni possibili portano a due rimedi
opposti, e **da fuori non si distinguono** (in tutti e due i casi chi bussa vede una risposta buona):

| quel che si legge in `avvii.txt` | vuol dire | che si fa |
|---|---|---|
| `richieste 6`, `12`, `20`… | le richieste **arrivano**, e muore lo stesso | non è inattività: si guarda nel **pannello** dell'hosting (memoria, ricambio del processo) |
| `richieste 1` o `richieste 0` | le richieste **non arrivano** al processo | il keep-alive parla con qualcos'altro (cache o web server): si cambia l'indirizzo che interroga |

⏳ **Che cosa ci serve**: `diagnostica/avvii.txt` **riscaricato qualche ora dopo** il caricamento. Non serve
altro, e non c'è niente da fare a mano.

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-18-file-1.7.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **qui c'è il timbro `1.7.0`, e il registro degli avvii nuovo** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: la tendina «Sposta in…», la testata compatta, l'import |
| 5-6 | `Vipi.Application.dll` · `.pdb` | le regole: dove può andare una sezione, il catalogo con le carte |
| 7-8 | `Vipi.Infrastructure.dll` · `.pdb` | il database: il motore che sposta, e le due sistemazioni all'avvio |
| 9-10 | `Vipi.Hosting.dll` · `.pdb` | il montaggio dei servizi |
| 11 | `en/Vipi.Ui.resources.dll` | le frasi inglesi (ci sono voci nuove) |
| 12 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset |
| 13-18 | `wwwroot/_content/Vipi.Ui/` — `vipi-theme.css`, `vipi-print.css`, e per ognuno il `.br` e il `.gz` | gli stili |

> ### ⚠️ `wwwroot` e il suo indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` dice **con che nome** il sito chiede ogni file di `wwwroot`.
> Caricare l'indice senza i file, o i file senza l'indice, fa chiedere al sito nomi che non esistono: le
> pagine escono **senza stili**. È il difetto del 24 agosto 2026. **O tutti e sette, o nessuno.**

ℹ️ **Non c'è `Vipi.Infrastructure.MySqlMigrations.dll`**: nessuna migrazione è stata toccata. E **non c'è
il JavaScript**: `vipi-ui.js` e gli altri sono risultati **identici** a quelli di 1.6.2 — confrontati per
impronta, non dedotti. Le impronte `sha256` dei diciotto file sono in `IMPRONTE.txt`, dentro la cartella del
pacchetto.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo).
2. **Rinominate**, dal più profondo al più superficiale: prima `wwwroot/_content/Vipi.Ui/*` e
   `en/Vipi.Ui.resources.dll`, poi `Vipi.Host.staticwebassets.endpoints.json`, poi i `.pdb`, e **per ultimi
   i `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**: è a quell'avvio che girano le due
   sistemazioni dei documenti.
4. **Fate i controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.7.0 · bf24915`**.
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

> ### C · Gli stili sono arrivati interi
>
> Aprite un documento qualsiasi (per esempio la vIPI di un ACC): sopra il titolo devono esserci **due righe**
> e non tre. Se la pagina esce **senza stili**, è mancato uno dei sette file di `wwwroot` o l'indice: si
> ricarica quel gruppo per intero.

> ### D · Le carte aeroportuali ci sono
>
> Aprite un aeroporto o un vSOP militare: nell'indice, prima di «Validità e revisione», deve comparire
> **«Carte aeroportuali»** con dentro Aerodromo, Carte di avvicinamento strumentale, SID, STAR, VFR. E nei
> vSOP militari i **Parcheggi** stanno adesso sotto **«Dati generali»**.

> ### ⏳ E fra qualche ora, la cosa che ci serve davvero
>
> Riscaricate **`diagnostica/avvii.txt`** e mandatecelo. È la misura del punto 5: senza quel numero, sul
> sito che si spegne ogni cinquanta secondi possiamo solo tirare a indovinare.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi `wwwroot` e l'indice **insieme**, poi riavvio. **Nessuna conseguenza sul database**, che questo
pacchetto non tocca.

ℹ️ Le due sistemazioni dei documenti (carte aeroportuali, parcheggi) **restano** anche tornando indietro:
sono dati, non codice. Non danno fastidio a 1.6.2 — sono sezioni in più, vuote.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**; **10 138 test** verdi su **quindici** assiemi (nove progetti sui due
  runtime), **E2E compresi** (300);
- il pacchetto **avviato davvero** dalla sua cartella e guidato in un browser, **dieci controlli su dieci
  verdi**: il JavaScript minificato arriva (2 400 caratteri su una riga), il circuito si apre, la **Ricerca**
  risponde, l'editor ACC si apre col pannello traduzioni, il foglio di stile è in vigore, console pulita;
- le **novità** provate sullo stesso pacchetto, a **1280 px** di larghezza: la tendina «Sposta in…» offre
  **32 destinazioni** e sta **dentro** la card, la riga dei comandi **non sfora** (0 px), e «Carte
  aeroportuali» con le sue raccolte è nell'indice — cioè la sistemazione all'avvio ha fatto il suo lavoro;
- il **timbro** letto nel file di diagnostica del pacchetto: `1.7.0 · bf24915`.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **La misura del punto 5 non si prova da qui.** Che la riga `ARRESTO` porti i numeri è provato da un test
   che accende il sito vero, gli manda una richiesta e rilegge la riga scritta; ma **quanto valgono quei
   numeri sul vostro server** è esattamente ciò che nessuno sa — è la domanda per cui il pacchetto esiste.
2. **Le due sistemazioni dei documenti sono state provate su una copia del vostro database**, non sul
   vostro. Sono additive (aggiungono sezioni, spostano un padre) e idempotenti, ma il primo avvio vero
   resta il primo avvio vero.
