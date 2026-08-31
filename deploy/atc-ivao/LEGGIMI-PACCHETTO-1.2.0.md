# Pacchetto 1.2.0 — solo i file cambiati

> **Timbro:** `1.2.0 · 9d5d902` (31 agosto 2026). È quel che compare nella barra in alto agli amministratori,
> e nella prima riga di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.1.0**, che è quello attualmente sul server. **20 file, 4,36 MB.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante — è
> successo la notte del 23→24 agosto e il sito è rimasto giù. La procedura per esteso è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md); qui c'è **solo che cosa** caricare, e
> le cose che un controllo normale non prenderebbe.

---

> ## 🔴 QUESTO PACCHETTO TOCCA IL DATABASE. VA CARICATO STASERA.
>
> A differenza di 1.1.0, qui dentro c'è **una modifica alla struttura del database** (una *migrazione*).
> Non c'è niente da importare a mano: **la applica l'applicazione da sola**, al primo avvio dopo il
> caricamento. Aggiunge una tabella e non tocca né cancella niente di quel che c'è.
>
> **Perché allora l'avviso.** Perché quella modifica, una volta partita, **non si annulla da sola**: il
> database non sa tornare indietro di un passo. L'unica rete è **il ripristino di una copia**, e chi
> amministra il database ci ha detto che può ancora farlo **entro stasera**.
>
> ### Quindi, in ordine:
>
> 1. **Fate fare una copia di sicurezza del database** a chi lo amministra, *prima* del riavvio.
> 2. Caricate i 20 file (caricarli non fa partire niente).
> 3. Riavviate, e **fate subito i tre controlli** in fondo a questo foglio.
> 4. ⚠️ **Se qualcosa non torna, ditelo STASERA**, finché chi amministra il database è raggiungibile.
>    Domani il ripristino non c'è più, e da lì in avanti si può solo andare avanti.
>
> ℹ️ Se stasera non ce la fate, **non caricate**: aspettate. Il pacchetto non scade, e caricarlo domani
> senza quella rete è un rischio che nessuno ha bisogno di correre. ⚠️ Tenete però presente che finché non
> sale, il difetto qui sotto (§1) resta.

---

## Che cosa cambia

**1. 🔴 Il sito smette di cadere da solo ogni due-tre ore.**
Il 31 agosto il processo del sito è **morto due volte** — alle 10:57 e alle 13:05 — e chi stava usando il
sito in quel momento ha visto la pagina «*This page did not open*» o il riquadro della riconnessione. La
causa era **nostra, e in un posto imbarazzante**: lo strumento che avevamo aggiunto il 24 agosto *per
capire* certi errori teneva un elenco che cresceva a ogni singola operazione sul database e non lo svuotava
mai. In poche ore diventava abbastanza grande da esaurire la memoria. È stato riscritto.

**2. L'errore «A second operation was started…» sui documenti.**
Compariva mentre si lavorava a un documento, anche essendo l'unica persona su quel documento — e infatti
non era un conflitto fra persone: era il pannello delle traduzioni che ricaricava il documento **due volte
a ogni ridisegno della pagina**, dandosi fastidio da solo. Adesso legge una volta, in disparte, e solo
quando cambia davvero qualcosa.

**3. La pagina d'ingresso non muore più per un intoppo momentaneo.**
`/services/vsop` rispondeva con la pagina d'errore se l'elenco delle ACC non si riusciva a leggere. Adesso
in quel caso esce lo stesso, con un avviso al posto delle schede.

**4. Le novità della struttura dei settori** (la ricaduta verticale, i cicli nell'albero di copertura) e il
giro di rifiniture su sette pagine: Struttura, Confinanti, Fraseologia, Radioassistenze, «Da fare», Spazi
aerei. È da qui che arriva la migrazione.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-20-file-1.2.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP, e `solo-20-file-1.2.0/` ha la stessa struttura: si può trascinare rispettando i percorsi.

| # | File | Che cos'è |
|---|---|---|
| 1 | `Vipi.Host.dll` | il sito |
| 2 | `Vipi.Host.pdb` | la sua mappa di debug: serve a far uscire il **numero di riga** in `diagnostica/errori-richieste.txt` |
| 3 | `Vipi.Ui.dll` | le pagine |
| 4 | `Vipi.Ui.pdb` | idem |
| 5 | `Vipi.Application.dll` | la logica |
| 6 | `Vipi.Application.pdb` | idem |
| 7 | `Vipi.Domain.dll` | i dati e le loro regole |
| 8 | `Vipi.Domain.pdb` | idem |
| 9 | `Vipi.Infrastructure.dll` | il database ⚠️ **è qui dentro che c'è la migrazione** |
| 10 | `Vipi.Infrastructure.pdb` | idem |
| 11 | `Vipi.Infrastructure.MySqlMigrations.dll` | la stessa migrazione, nella forma che capisce MariaDB ⚠️ **va con il file 9** |
| 12 | `Vipi.Infrastructure.MySqlMigrations.pdb` | idem |
| 13 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`**, non alla radice |
| 14 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset **(insieme)** |
| 15 | `wwwroot/_content/Vipi.Ui/vipi-boot.js` | il codice di pagina **(insieme)** |
| 16 | `wwwroot/_content/Vipi.Ui/vipi-boot.js.br` | la sua copia compressa **(insieme)** |
| 17 | `wwwroot/_content/Vipi.Ui/vipi-boot.js.gz` | idem **(insieme)** |
| 18 | `wwwroot/_content/Vipi.Ui/vipi-theme.css` | il foglio di stile **(insieme)** |
| 19 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.br` | la sua copia compressa **(insieme)** |
| 20 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.gz` | idem **(insieme)** |

> ### ⚠️ I file di `wwwroot` e l'indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` è l'elenco che dice al sito **con quale nome** chiedere ogni
> file di `wwwroot`, impronta compresa. Caricare l'indice senza i file (o i file senza l'indice) fa chiedere
> al sito nomi che non esistono: pagine senza stile, o senza comportamento. È già successo il 24 agosto.
> Sono marcati **(insieme)**: o si caricano tutti, o nessuno.

ℹ️ Le impronte `sha256` di tutti e venti sono in `IMPRONTE.txt`, dentro la cartella del pacchetto: se un
caricamento va a metà, sono il modo di scoprirlo **prima** di riavviare.

ℹ️ **Gli altri 440 file del sito non cambiano** e non vanno ricaricati. In particolare
`wwwroot/_content/Vipi.Ui/vipi-riconnessione.js` — quello **obbligatorio** arrivato con 1.1.0 — è
**identico** e resta dov'è. ⚠️ **Non cancellatelo**: senza, il sito si vede intero e non risponde a niente.

## L'ordine

1. **La copia di sicurezza del database** (riquadro rosso in testa). Questo passo viene prima di tutto.
2. **Caricate tutto col nome finto** (`.new` in fondo: `Vipi.Host.dll.new`, e così via). I file dentro
   `wwwroot/` non hanno bisogno del nome finto — nessuno li tiene aperti — ma non fa danno.
3. **Rinominate**, dal più profondo al più superficiale: prima i file di `wwwroot/`, poi l'indice
   `staticwebassets`, poi i `.dll`. ⚠️ I `.dll` per ultimi: appena il processo riparte, deve trovare
   `wwwroot` già a posto.
4. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta** — è la richiesta che fa accorgere
   Passenger del file. ⚠️ È **a questo avvio** che la migrazione viene applicata: metteteci qualche secondo
   in più di pazienza, l'avvio dura circa dieci secondi invece di sette.
5. **Fate i tre controlli** qui sotto.

## I tre controlli, in un minuto

> ### A · È partita la versione nuova, e la migrazione è passata
>
> Aprite `diagnostica/avvio-diagnostica.txt`. La prima riga deve avere **l'ora di adesso**, e la riga
> `Versione` deve dire **`1.2.0 · 9d5d902`**. Poco più sotto, «Durata delle fasi d'avvio» ha una voce
> **«migrazione del database»**: se l'avvio è arrivato in fondo e il file c'è, la migrazione è andata.
>
> ⚠️ Se invece compare un file `diagnostica/avvio-errore.txt`, **fermatevi e mandatecelo**: è il caso in cui
> serve il ripristino, ed è per quello che stasera c'è chi può farlo.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> Aprite `https://atc.it.ivao.aero/services/vsop/search` e scrivete **`LI`** nel campo di ricerca. La riga
> sotto il campo deve **cambiare** da «Digita almeno 2 caratteri» a «*N* risultati per LI».
>
> ⚠️ **Non usate il selettore della lingua, né i tasti dello zoom, né il tema chiaro/scuro**: sono
> collegamenti e codice che vive nella pagina, e **funzionano lo stesso** anche quando il sito è morto. La
> Ricerca è l'unico controllo di cui si sa che distingue davvero i due casi, ed è stato provato in
> tutt'e due i modi.
>
> Se resta «Digita almeno 2 caratteri» mentre nel campo c'è scritto qualcosa, aprite
> `https://atc.it.ivao.aero/_content/Vipi.Ui/vipi-riconnessione.js`: deve comparire una paginata di testo che
> comincia con `(function(){"use strict";`. Se dà **404**, quel file è stato cancellato per sbaglio e va
> rimesso — è nel pacchetto **1.1.0**, non in questo.

> ### C · La pagina d'ingresso mostra le ACC
>
> Aprite `https://atc.it.ivao.aero/services/vsop`. Devono comparire le schede delle ACC. Se al loro posto
> c'è un riquadro giallo che dice «Elenco delle ACC non disponibile», ricaricate una volta: se resta,
> ditecelo — ma la pagina esce lo stesso, ed è la novità §3.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto **non le tocca**: non c'è nessun file con quei nomi qui dentro. Se un programma FTP vi
propone di sincronizzare cartelle intere, **non fatelo** — caricate i file elencati e basta.

ℹ️ Non va cancellato nemmeno `diagnostica/`. Anzi: `errori-richieste.txt` e `avvii.txt` sono i due file da
mandarci se qualcosa non torna. Nessuno dei due contiene password.

## Se qualcosa va storto

**Il codice** torna indietro con le rinomine al contrario: i file di prima sono ancora sul server col nome
`.old` (li lascia la procedura del foglio FTP). ⚠️ Stessa regola: prima i `.dll`, poi `wwwroot`, poi
riavvio.

⚠️ **Il database no.** Tornare a 1.1.0 dopo che la migrazione è passata lascia la tabella nuova dov'è —
innocua, perché il codice vecchio semplicemente la ignora — ma se il problema fosse *nella* migrazione,
l'unica strada è il **ripristino della copia**. È il motivo per cui questo pacchetto si carica stasera e
non domani.
