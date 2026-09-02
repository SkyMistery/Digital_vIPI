# Pacchetto 1.4.0 — solo i file cambiati

> **Timbro:** `1.4.0 · 669762f` (2 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella prima riga di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.3.0**, che è quello attualmente sul server. **21 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante — è
> successo la notte del 23→24 agosto e il sito è rimasto giù. La procedura per esteso è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md); qui c'è **solo che cosa** caricare, e
> le cose che un controllo normale non prenderebbe.

---

> ## 🟢 QUESTO PACCHETTO NON TOCCA IL DATABASE
>
> A differenza di 1.2.0 e 1.3.0, qui dentro **non c'è nessuna migrazione**: la struttura del database non
> cambia di una virgola. Non serve una copia di sicurezza concordata, non serve aspettare che chi
> amministra il database sia raggiungibile, non c'è nessuna finestra da prendere.
>
> Si carica, si riavvia, si fanno i tre controlli in fondo.
>
> ⚠️ **Una sola avvertenza, e riguarda il tornare indietro.** Dentro c'è una tabella dei vSOP militari — gli
> **aeroporti alternati** — la cui *distanza* prima si scriveva solo intera e adesso accetta i decimali
> (`72.2 NM`). Il numero sta dentro il documento, non in una colonna del database, quindi da caricare non
> cambia niente. Ma se qualcuno scrivesse un decimale e **poi** si tornasse a 1.3.0, la versione vecchia non
> saprebbe leggerlo e **quella tabella sparirebbe a schermo**, senza messaggi. Non è un guasto: è il prezzo
> del ritorno indietro, e si paga solo se qualcuno ha già scritto un decimale.

---

## Che cosa cambia

**1. Le tabelle dei documenti si possono IMPORTARE invece di ridigitarle.** *(la novità grossa)*
In ogni tabella dell'editor — quelle libere, «Nominativi», «Parcheggi», «Aeroporti alternati» — accanto a
«+ riga» c'è **⤓ Importa**. Si incolla una tabella presa da Excel, da un CSV, da una pagina web o da un
PDF, oppure si carica un file `.csv`/`.xlsx`; il sistema la legge e **mostra un'anteprima**, riga per riga,
con scritto che cosa ha capito. **Finché non si preme «Importa» non viene scritto niente.**

Le celle che citano qualcosa dei nostri cataloghi — un aeroporto, una radioassistenza — vengono **risolte**:
il nome dello scalo lo mette l'archivio, non il testo incollato. Un codice che non conosciamo **non viene
inventato**: la riga resta fuori, in rosso, e si vede il perché. Un codice che appartiene a più impianti
non viene rifiutato: **si chiede quale**, con una tendina.

**2. Da una tabella si può anche USCIRE, e si può copiarla da un altro documento.**
`⤒ Esporta CSV` scarica la tabella per sistemarla nel foglio di calcolo e reimportarla; la tendina
**«Da un altro documento…»** prende la stessa tabella già scritta nel vSOP di un altro campo — sui quindici
SOP militari, che hanno le stesse sezioni, è il modo più rapido di partire.

**3. «ONLY FOR SIMULATION» è ovunque, anche sulla carta stampata.**
L'avviso compare su tutte le schermate dei documenti e **a piè di ogni foglio stampato**: nessuna pagina
uscita da qui può più essere scambiata per documentazione reale.

**4. I titoli delle sezioni seguono la lingua del DOCUMENTO.**
Un documento bloccato in inglese mostrava le testate in italiano a chi aveva il sito in italiano. Ora la
lingua del documento comanda anche sui titoli.

**5. L'avviso «tradotta a macchina» non si mangia più mezza schermata.**
Era un riquadro a piena larghezza in cima al documento: adesso è un gettone, sulla stessa riga
dell'avviso di simulazione.

**6. La pagina non si blocca più su «Fine modifica».**
Una segnalazione dal sito vero: chiudendo la modifica la pagina poteva restare inchiodata su
«Salvataggio…», con il lavoro **già salvato** ma la schermata da ricaricare. Chiuso, insieme alla causa —
sei pagine dell'editor che si contendevano la stessa connessione al database.

**7. Sui campi SOLO militari i dati dello scalo si scrivono nel loro vSOP.**
Le sezioni derivate rimandavano «all'editor dell'aeroporto», che su quei campi **non esiste**: era un giro
chiuso. Adesso quei dati si scrivono lì, nel vSOP militare.

**8. I nostri cataloghi e il sectorfile di Aurora vengono confrontati da soli.**
Due elenchi indipendenti descrivono le stesse cose — frequenze, aeroporti, piste — e nessuno li guardava
insieme. Ora c'è una pagina, per amministratori, che dice dove non vanno d'accordo.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-21-file-1.4.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP, e `solo-21-file-1.4.0/` ha la stessa struttura: si può trascinare rispettando i percorsi.

| # | File | Che cos'è |
|---|---|---|
| 1 | `Vipi.Host.dll` | il sito ⚠️ **è qui che sta il timbro `1.4.0`** |
| 2 | `Vipi.Host.pdb` | la sua mappa di debug: serve a far uscire il **numero di riga** in `diagnostica/errori-richieste.txt` |
| 3 | `Vipi.Ui.dll` | le pagine — **è qui che sta l'import delle tabelle** |
| 4 | `Vipi.Ui.pdb` | idem |
| 5 | `Vipi.Application.dll` | la logica: lettura dei file incollati, `.csv`, `.xlsx` |
| 6 | `Vipi.Application.pdb` | idem |
| 7 | `Vipi.Infrastructure.dll` | l'accesso al database e il confronto col sectorfile |
| 8 | `Vipi.Infrastructure.pdb` | idem |
| 9 | `Vipi.Hosting.dll` | l'avvio e le manutenzioni |
| 10 | `Vipi.Hosting.pdb` | idem |
| 11 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`**, non alla radice |
| 12 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset **(insieme)** |
| 13 | `wwwroot/_content/Vipi.Ui/vipi-theme.css` | il foglio di stile **(insieme)** |
| 14 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.br` | la sua copia compressa **(insieme)** |
| 15 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.gz` | idem **(insieme)** |
| 16 | `wwwroot/_content/Vipi.Ui/vipi-print.css` | il foglio della **stampa** **(insieme)** |
| 17 | `wwwroot/_content/Vipi.Ui/vipi-print.css.br` | la sua copia compressa **(insieme)** |
| 18 | `wwwroot/_content/Vipi.Ui/vipi-print.css.gz` | idem **(insieme)** |
| 19 | `wwwroot/_content/Vipi.Ui/vipi-ui.js` | il comportamento delle pagine **(insieme)** |
| 20 | `wwwroot/_content/Vipi.Ui/vipi-ui.js.br` | la sua copia compressa **(insieme)** |
| 21 | `wwwroot/_content/Vipi.Ui/vipi-ui.js.gz` | idem **(insieme)** |

> ### ⚠️ I file di `wwwroot` e l'indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` è l'elenco che dice al sito **con quale nome** chiedere ogni
> file di `wwwroot`, impronta compresa. Caricare l'indice senza i file (o i file senza l'indice) fa chiedere
> al sito nomi che non esistono: pagine senza stile, o senza comportamento. È già successo il 24 agosto.
> Sono marcati **(insieme)**: o si caricano tutti, o nessuno.

ℹ️ Le impronte `sha256` di tutti e ventuno sono in `IMPRONTE.txt`, dentro la cartella del pacchetto: se un
caricamento va a metà, sono il modo di scoprirlo **prima** di riavviare.

ℹ️ **Rispetto a 1.3.0 mancano quattro file, ed è voluto**: `Vipi.Domain.dll`/`.pdb` e
`Vipi.Infrastructure.MySqlMigrations.dll`/`.pdb` **non sono cambiati** e restano dove sono. Ogni `.dll` in
più è una rinomina in più su un file che il processo tiene aperto: non è prudenza, è rischio.

ℹ️ **Gli altri file del sito non cambiano.** In particolare
`wwwroot/_content/Vipi.Ui/vipi-riconnessione.js` (arrivato con 1.1.0, **obbligatorio**) e
`vipi-boot.js` (1.2.0) sono **identici** e restano dove sono. ⚠️ **Non cancellateli**: senza il primo, il
sito si vede intero e non risponde a niente.

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo: `Vipi.Host.dll.new`, e così via). I file dentro
   `wwwroot/` non hanno bisogno del nome finto — nessuno li tiene aperti — ma non fa danno.
2. **Rinominate**, dal più profondo al più superficiale: prima i file di `wwwroot/`, poi l'indice
   `staticwebassets`, poi i `.dll`. ⚠️ I `.dll` per ultimi: appena il processo riparte, deve trovare
   `wwwroot` già a posto.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta** — è la richiesta che fa accorgere
   Passenger del file.
4. **Fate i tre controlli** qui sotto.

ℹ️ Niente copia di sicurezza del database, niente attesa: questo pacchetto non lo tocca.

## I tre controlli, in un minuto

> ### A · È partita la versione nuova
>
> Aprite `diagnostica/avvio-diagnostica.txt`. La prima riga deve avere **l'ora di adesso**, e la riga
> `Versione` deve dire **`1.4.0 · 669762f`**.
>
> ⚠️ Se invece compare un file `diagnostica/avvio-errore.txt`, **fermatevi e mandatecelo**.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> Aprite `https://atc.it.ivao.aero/services/vsop/search` e scrivete **`LI`** nel campo di ricerca. La riga
> sotto il campo deve **cambiare** da «Digita almeno 2 caratteri» a «*N* risultati per LI».
>
> ⚠️ **Non usate il selettore della lingua, né i tasti dello zoom, né il tema chiaro/scuro**: sono
> collegamenti e codice che vive nella pagina, e **funzionano lo stesso** anche quando il sito è morto. La
> Ricerca è l'unico controllo di cui si sa che distingue davvero i due casi.
>
> Se resta «Digita almeno 2 caratteri» mentre nel campo c'è scritto qualcosa, aprite
> `https://atc.it.ivao.aero/_content/Vipi.Ui/vipi-riconnessione.js`: deve comparire una paginata di testo che
> comincia con `(function(){"use strict";`. Se dà **404**, quel file è stato cancellato per sbaglio e va
> rimesso — è nel pacchetto **1.1.0**, non in questo.

> ### C · Lo stile e il comportamento nuovi sono arrivati
>
> Se avete i permessi, aprite l'editor di un vSOP militare, entrate in modifica e cercate il tasto
> **⤓ Importa** accanto a «+ Aggiungi riga»: premendolo si apre un riquadro con una casella grande e la
> riga di comandi (Carica un file · Da un altro documento… · due caselle da spuntare), **tutti della stessa
> altezza e in italiano**.
>
> Se il tasto «Carica un file» esce come un bottone grigio di sistema con scritto «Choose File», è
> `vipi-theme.css` a non essere salito. Se la casella grande c'è ma **il tasto Tab la fa uscire** invece di
> scrivere una tabulazione, è `vipi-ui.js` a non essere salito — o è salito senza il suo indice (vedi il
> riquadro «insieme»).

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto **non le tocca**: non c'è nessun file con quei nomi qui dentro. Se un programma FTP vi
propone di sincronizzare cartelle intere, **non fatelo** — caricate i file elencati e basta.

ℹ️ Non va cancellato nemmeno `diagnostica/`. Anzi: `avvii.txt` e `errori-richieste.txt` sono i due file da
mandarci se qualcosa non torna. Nessuno dei due contiene password.

## Se qualcosa va storto

Il codice torna indietro con le rinomine al contrario: i file di prima sono ancora sul server col nome
`.old` (li lascia la procedura del foglio FTP). ⚠️ Stessa regola: prima i `.dll`, poi `wwwroot`, poi
riavvio.

⚠️ **E qui il ritorno indietro è pulito**, perché non c'è nessuna migrazione da disfare — con l'unica
eccezione scritta nel riquadro in testa: la distanza di un aeroporto alternato scritta **con i decimali**
non è leggibile da 1.3.0.

---

## Che cosa è stato provato prima di spedire

Su **questo** pacchetto — non sul codice sorgente, che è un'altra cosa: nel pacchetto il JavaScript è
minificato.

- build in Release sui due runtime, **0 avvisi** (qui gli avvisi sono errori);
- **5068 test** verdi su nove progetti, **E2E compresi** (276);
- il pacchetto pubblicato **avviato davvero** e guidato in un browser: il timbro dice `1.4.0 · 669762f`, la
  **Ricerca risponde**, il riquadro d'import si apre e il tasto Tab scrive la tabulazione **col JavaScript
  minificato**.
