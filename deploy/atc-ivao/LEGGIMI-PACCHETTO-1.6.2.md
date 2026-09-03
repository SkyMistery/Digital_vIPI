# Pacchetto 1.6.2 — solo i file cambiati

> **Timbro:** `1.6.2 · 84b0f4c7` (3 settembre 2026, sera). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.6.1**, che è quello attualmente sul server. **4 file** — il pacchetto più piccolo finora.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, niente `wwwroot`, niente frasi
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete.
>
> **Nessun foglio di stile o JavaScript**, quindi nemmeno l'indice degli asset — con esso sparisce la
> trappola del 24 agosto. E **nessuna frase nuova**: la cartella `en` non si tocca.

---

## Perché va caricato

🔴 **Viene dalla diagnostica che ci avete mandato dopo 1.6.1.** Due cose, e la prima riguarda proprio il
foglio che avete seguito per caricare 1.6.1.

### 1 · «L'avvio è FALLITO» era scritto su uno **spegnimento**

Nella diagnostica c'era un `avvio-errore.txt` che diceva **«vIPI — l'avvio è FALLITO»**. Il foglio di 1.6.1
diceva: *«Se compare `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo»*. Consiglio giusto, file che
mentiva.

**Che cos'era davvero**, letto in `avvii.txt`: il processo era acceso da **un'ora e cinquanta minuti**, aveva
servito richieste tutto il tempo, ed è morto **chiudendo**. Non era 1.6.1 che non partiva: era il processo
**precedente** che finiva male.

⚠️ **Il motivo è strutturale**: la riga che avvia il sito **resta ferma fino allo spegnimento**, quindi
qualunque guasto della chiusura torna indietro dallo stesso punto da cui tornerebbe un avvio fallito, e chi
scriveva il file non aveva modo di distinguerli.

**Da adesso i due casi hanno due file:**

| file | vuol dire | vi dovete fermare? |
|---|---|---|
| `diagnostica/avvio-errore.txt` | il sito **non è partito** | **sì**, e mandatecelo |
| `diagnostica/arresto-errore.txt` | era partito, è morto **chiudendo** | **no** |

> ### ℹ️ `arresto-errore.txt` è normale su questo hosting, e non è un allarme
>
> Il vostro server **spegne il sito da solo** quando nessuno lo usa: è il suo funzionamento, non un guasto.
> Se ogni tanto quello spegnimento va storto, ora lo trovate scritto lì dentro — con la frase «il sito ERA
> partito». Mandatecelo pure quando capita, lo guardiamo con calma: **non fermate il caricamento per lui**.

### 2 · Sette pezzi dell'editor che potevano far cadere la pagina

È la **coda di 1.6.1**. Là ne erano stati sistemati due, e ve l'avevamo scritto. Ne restavano sette, noti,
lasciati indietro per non allargare in una sera una correzione mirata a tre pagine di produzione:

| pagina | quanti |
|---|---|
| l'**editor dell'aeroporto** | 3 |
| la pagina **«Nuovo documento»** | 1 |
| l'elenco **«Bozze & versioni»** | 3 |

Sono lo stesso identico difetto delle nove pagine d'errore di 1.6.1: due operazioni che si pestano sulla
stessa connessione al database, e la pagina muore. ⚠️ **Non è un difetto di 1.6.1**: c'era da prima, con le
stesse righe.

Ora **non ne resta nessuno**, e il controllo automatico che li cerca non ha più eccezioni.

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-4-file-1.6.2/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP. **Sono tutti e quattro lì dentro, nessuna sottocartella.**

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **è qui che stanno i due file di diagnostica, e il timbro `1.6.2`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: è qui che stanno le sette correzioni |

ℹ️ **Non c'è `Vipi.Application.dll`** (c'era in 1.6.1) e **non c'è `en/Vipi.Ui.resources.dll`**: nessuno dei
due è cambiato. Le impronte `sha256` dei quattro sono in `IMPRONTE.txt`, dentro la cartella del pacchetto, e
sono state **confrontate** con quelle di 1.6.1 — tutti e quattro risultano diversi, niente da scartare.

## L'ordine

1. **Caricate tutti e quattro col nome finto** (`.new` in fondo).
2. **Rinominate**: prima i due `.pdb`, poi **per ultimi i due `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.6.2 · 84b0f4c`**.
>
> ⚠️ **E qui cambia una cosa rispetto ai fogli di prima**: se trovate `diagnostica/avvio-errore.txt`,
> fermatevi e mandatecelo. Se invece trovate `diagnostica/arresto-errore.txt`, **andate avanti** — mandatelo
> quando avete tempo. ℹ️ Il vecchio `avvio-errore.txt` di ieri sera, se è ancora lì, **leggetelo e
> cancellatelo**: è il residuo del guasto che questo pacchetto corregge, e finché resta darà sempre un
> falso allarme.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.

> ### C · Le tre pagine corrette
>
> Aprite, da amministratore, e guardate che si riempiano:
>
> | pagina | cosa deve comparire |
> |---|---|
> | **Bozze & versioni** | l'elenco dei documenti, il ciclo AIRAC in alto e il riquadro «Prossimo AIRAC» |
> | **Editor di un aeroporto** | le sezioni, la tabella dei settori ATC, il tasto **✎ Modifica** |
> | **Nuovo documento** | scelto «Aeroporto» e un ACC, la tendina degli aeroporti si riempie |
>
> Se compare la barra in basso «Qualcosa è andato storto…», ricaricate e mandateci
> `diagnostica/errori-richieste.txt`.

> ### ⚠️ E fra qualche giorno, il controllo che vale davvero
>
> In `errori-richieste.txt` **non devono più comparire** righe con
> *«A second operation was started on this context instance»*. Quella è la prova, e non si può avere subito.
>
> ℹ️ Vale anche il controllo di 1.6.1: le righe con `CreaAsync` e `ResolveForDocumentAsync` devono essere
> sparite.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non tocca — e nessun limite
nuovo al tornare indietro.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release sui due runtime, **0 avvisi**; **10 015 test** verdi su **nove** progetti su nove, **E2E
  compresi** (294);
- il pacchetto **avviato davvero** e guidato in un browser:

| | |
|---|---|
| il timbro | **`1.6.2 · 84b0f4c`**, e **nessuno** dei due file di guasto |
| il circuito | si apre, col JavaScript **minificato** (2 124 byte su una riga) |
| la **Ricerca** | `LI` → **«50 results for LI»**, la pagina passa da 216 a 6 359 caratteri |
| **Bozze & versioni** | 11 documenti, `AIRAC 2609`, riquadro «Prossimo AIRAC» |
| **editor aeroporto** | 8 sezioni, **3** settori ATC, **✎ Modifica** presente |
| **Nuovo documento** | 8 aeroporti nella tendina, 3 dei quali solo militari |
| la barra d'errore | **mai comparsa** in nessuna delle prove |

**E ogni pezzo spostato è stato fatto SCRIVERE**, con la scrittura verificata **nel database** e non a
schermo: una release pianificata al ciclo 2610, una regola pista nuova, un settore nascosto, e un vSOP
militare creato da zero su un campo che non aveva nessun documento.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **La corsa non si riproduce qui.** In sviluppo il database è SQLite e la finestra fra le due operazioni è
   di millisecondi; sul vostro MariaDB, con la latenza vera, si apre. Quel che le prove dimostrano è che
   **spostare i sette servizi non ha rotto niente**; che la correzione funzioni lo dirà
   `errori-richieste.txt` fra qualche giorno.
2. **`arresto-errore.txt` non è stato visto nascere**, perché per farlo comparire servirebbe uno spegnimento
   che va storto, e non si fabbrica a comando. Quel che è provato — con un test che avvia il sito vero e
   fallisce se qualcuno toglie la riga — è che **dopo l'avvio il sito sa di essere partito**, che è l'unica
   cosa da cui dipende quale dei due file viene scritto.
