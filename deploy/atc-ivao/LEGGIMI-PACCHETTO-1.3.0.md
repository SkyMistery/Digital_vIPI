# Pacchetto 1.3.0 — solo i file cambiati

> **Timbro:** `1.3.0 · 1ade0db` (31 agosto 2026). È quel che compare nella barra in alto agli amministratori,
> e nella prima riga di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.2.0**, che è quello attualmente sul server. **22 file, 14,9 MB.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante — è
> successo la notte del 23→24 agosto e il sito è rimasto giù. La procedura per esteso è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md); qui c'è **solo che cosa** caricare, e
> le cose che un controllo normale non prenderebbe.

---

> ## 🔴 ANCHE QUESTO PACCHETTO TOCCA IL DATABASE. VA CARICATO QUANDO C'È ANCORA QUALCUNO.
>
> Come 1.2.0, qui dentro c'è **una modifica alla struttura del database** (una *migrazione*). Non c'è niente
> da importare a mano: **la applica l'applicazione da sola**, al primo avvio dopo il caricamento.
>
> È **più piccola** di quella di 1.2.0: aggiunge **una sola colonna** a una tabella che c'è già
> (`Documents`), con un valore predefinito. Non cancella e non rinomina niente, e i documenti esistenti
> nascono tutti col comportamento di prima.
>
> **Perché allora l'avviso.** Perché una modifica al database, una volta partita, **non si annulla da
> sola**, e l'unica rete è **il ripristino di una copia**. Chi amministra il database di Ivao.It è via fino
> al **16 settembre**: fuori da una finestra in cui è raggiungibile, quella rete non c'è.
>
> ### Quindi, in ordine:
>
> 1. **Fate fare una copia di sicurezza del database** a chi lo amministra, *prima* del riavvio.
> 2. Caricate i 22 file (caricarli non fa partire niente).
> 3. Riavviate, e **fate subito i tre controlli** in fondo a questo foglio.
> 4. ⚠️ **Se qualcosa non torna, ditelo subito**, finché chi amministra il database è raggiungibile.
>
> ℹ️ Se in questo momento quella rete non c'è, **non caricate**: aspettate. Il pacchetto non scade, e
> caricarlo senza copia di sicurezza è un rischio che nessuno ha bisogno di correre.

---

## Che cosa cambia

**1. Un documento si può leggere in UNA lingua sola.**
Fino a ieri ogni documento si offriva in italiano e in inglese, e la traduzione automatica riempiva i buchi.
Adesso su un documento si può **bloccare la lingua**: chi lo apre lo legge in quella, chiunque sia e
qualunque lingua abbia scelto per il sito. ⚠️ Il blocco **spegne** la traduzione di quel documento, non la
fa: serve quando il testo è già scritto nella lingua giusta e tradurlo peggiorerebbe le cose.
**È da qui che arriva la migrazione**: la colonna nuova è l'interruttore.

**2. La vista 3D delle aree regolamentate ora dice il NOME dell'area.**
Nella legenda e sulle etichette dei volumi compariva il codice interno (`LI-R301B`) invece del nome. Le
targhette accanto alla mappa il nome lo dicevano già: era solo il 3D a non averlo.

**3. La pagina «Spazi aerei» (per gli amministratori) è allineata al resto del sito.**
Tabelle con le colonne della larghezza giusta, il pulsante «Carica file» che non mostra più il selettore di
sistema accanto a sé, e — richiesta esplicita — **i riquadri si aprono e si chiudono**, ricordando come li
avete lasciati. Il riquadro lungo del confronto radioassistenze **nasce chiuso**.

**4. Le «Regole piste» si compilano anche col tema scuro.**
I campi erano bianchi con il testo bianco sopra: si scriveva alla cieca. Ora seguono il tema come tutti gli
altri campi del sito.

ℹ️ E una cosa che era già possibile e forse non si sapeva: la sezione **«Regole piste» si può nascondere**
dal documento pubblicato (il pulsante con l'occhio, nell'editor) **senza spegnerla**. La pista in uso, i
simboli di decollo/atterraggio e la SID iniziale continuano a essere calcolati dalle regole: sparisce solo
la tabella, che di solito non interessa a chi legge.

**5. L'anteprima di una pubblicazione programmata mostra le SID di QUEL ciclo.**
Se un documento è programmato per il ciclo 2609 e nel frattempo arrivano SID nuove, l'anteprima mostrava
la tabella di **oggi**: quelle righe comparivano poi da sole al cambio di ciclo. Adesso l'anteprima mostra
il documento **come sarà quando quel ciclo entrerà in vigore**.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-22-file-1.3.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP, e `solo-22-file-1.3.0/` ha la stessa struttura: si può trascinare rispettando i percorsi.

| # | File | Che cos'è |
|---|---|---|
| 1 | `Vipi.Host.dll` | il sito ⚠️ **è qui che sta il timbro `1.3.0`** |
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
| 13 | `Vipi.Hosting.dll` | l'avvio e le manutenzioni ⚠️ **nuovo rispetto a 1.2.0**, che non lo conteneva |
| 14 | `Vipi.Hosting.pdb` | idem |
| 15 | `en/Vipi.Ui.resources.dll` | le frasi in inglese ⚠️ **è dentro la cartella `en`**, non alla radice |
| 16 | `Vipi.Host.staticwebassets.endpoints.json` | l'indice degli asset **(insieme)** |
| 17 | `wwwroot/_content/Vipi.Ui/vipi-theme.css` | il foglio di stile **(insieme)** |
| 18 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.br` | la sua copia compressa **(insieme)** |
| 19 | `wwwroot/_content/Vipi.Ui/vipi-theme.css.gz` | idem **(insieme)** |
| 20 | `wwwroot/_content/Vipi.Ui/vipi-aor3d.js` | il codice della vista 3D **(insieme)** |
| 21 | `wwwroot/_content/Vipi.Ui/vipi-aor3d.js.br` | la sua copia compressa **(insieme)** |
| 22 | `wwwroot/_content/Vipi.Ui/vipi-aor3d.js.gz` | idem **(insieme)** |

> ### ⚠️ I file di `wwwroot` e l'indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` è l'elenco che dice al sito **con quale nome** chiedere ogni
> file di `wwwroot`, impronta compresa. Caricare l'indice senza i file (o i file senza l'indice) fa chiedere
> al sito nomi che non esistono: pagine senza stile, o senza comportamento. È già successo il 24 agosto.
> Sono marcati **(insieme)**: o si caricano tutti, o nessuno.

ℹ️ Le impronte `sha256` di tutti e ventidue sono in `IMPRONTE.txt`, dentro la cartella del pacchetto: se un
caricamento va a metà, sono il modo di scoprirlo **prima** di riavviare.

ℹ️ **Gli altri file del sito non cambiano** e non vanno ricaricati. In particolare
`wwwroot/_content/Vipi.Ui/vipi-boot.js` (arrivato con 1.2.0) e
`wwwroot/_content/Vipi.Ui/vipi-riconnessione.js` (arrivato con 1.1.0, **obbligatorio**) sono **identici** e
restano dove sono. ⚠️ **Non cancellateli**: senza il secondo, il sito si vede intero e non risponde a
niente.

## L'ordine

1. **La copia di sicurezza del database** (riquadro rosso in testa). Questo passo viene prima di tutto.
2. **Caricate tutto col nome finto** (`.new` in fondo: `Vipi.Host.dll.new`, e così via). I file dentro
   `wwwroot/` non hanno bisogno del nome finto — nessuno li tiene aperti — ma non fa danno.
3. **Rinominate**, dal più profondo al più superficiale: prima i file di `wwwroot/`, poi l'indice
   `staticwebassets`, poi i `.dll`. ⚠️ I `.dll` per ultimi: appena il processo riparte, deve trovare
   `wwwroot` già a posto.
4. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta** — è la richiesta che fa accorgere
   Passenger del file. ⚠️ È **a questo avvio** che la migrazione viene applicata: metteteci qualche secondo
   in più di pazienza.
5. **Fate i tre controlli** qui sotto.

## I tre controlli, in un minuto

> ### A · È partita la versione nuova, e la migrazione è passata
>
> Aprite `diagnostica/avvio-diagnostica.txt`. La prima riga deve avere **l'ora di adesso**, e la riga
> `Versione` deve dire **`1.3.0 · 1ade0db`**. Poco più sotto, «Durata delle fasi d'avvio» ha una voce
> **«migrazione del database»**: se l'avvio è arrivato in fondo e il file c'è, la migrazione è andata.
>
> ⚠️ Se invece compare un file `diagnostica/avvio-errore.txt`, **fermatevi e mandatecelo**: è il caso in cui
> serve il ripristino.

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

> ### C · Il foglio di stile nuovo è arrivato
>
> Aprite una qualunque pagina del sito **col tema scuro** e guardate che lo sfondo sia scuro davvero. Poi,
> se avete i permessi, aprite l'editor di un aeroporto alla sezione **«Regole piste»**: i campi devono
> avere lo sfondo scuro e il testo chiaro. Se sono bianchi, `vipi-theme.css` non è salito — o è salito
> senza il suo indice (vedi il riquadro «insieme»).

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto **non le tocca**: non c'è nessun file con quei nomi qui dentro. Se un programma FTP vi
propone di sincronizzare cartelle intere, **non fatelo** — caricate i file elencati e basta.

ℹ️ Non va cancellato nemmeno `diagnostica/`. Anzi: `avvii.txt` e `errori-richieste.txt` sono i due file da
mandarci se qualcosa non torna. Nessuno dei due contiene password.

## Se qualcosa va storto

**Il codice** torna indietro con le rinomine al contrario: i file di prima sono ancora sul server col nome
`.old` (li lascia la procedura del foglio FTP). ⚠️ Stessa regola: prima i `.dll`, poi `wwwroot`, poi
riavvio.

⚠️ **Il database no.** Tornare a 1.2.0 dopo che la migrazione è passata lascia la colonna nuova dov'è —
innocua, perché il codice vecchio semplicemente la ignora — ma se il problema fosse *nella* migrazione,
l'unica strada è il **ripristino della copia**. È il motivo per cui questo pacchetto si carica quando c'è
ancora qualcuno raggiungibile.
