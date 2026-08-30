# Pacchetto 1.1.0 — solo i file cambiati

> **Aggiornamento del 31 agosto 2026.** Sostituisce **`j`** (30 agosto), che è quello attualmente sul
> server. **Il database non si tocca**: nessuna migrazione, nessun `.sql`, niente da chiedere a Ivao.It.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante — è
> successo la notte del 23→24 agosto e il sito è rimasto giù. La procedura per esteso è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md); qui c'è **solo che cosa** caricare, e
> le due cose nuove da sapere.

## Che cosa cambia, per chi usa il sito

**1. Il sito non resta più piantato su «Attempting to reconnect to the server…».**
Quel riquadro compare quando si stacca il collegamento fra la pagina e il server. Succede in due modi, e
finora venivano trattati allo stesso: se è un buco di rete, la pagina si riaggancia e si ritrova esattamente
com'era; se invece il processo è stato spento — cosa che il server fa **da solo** quando per un po' nessuno
usa il sito — non c'è più niente a cui riagganciarsi, e prima restava un messaggio in inglese con un tasto
da premere. Adesso in quel caso **la pagina si ricarica da sola**, in un paio di secondi, e il riquadro è in
italiano.

In più, finché qualcuno tiene una scheda aperta, il sito **manda un segnale ogni due minuti e mezzo** che
impedisce al server di spegnere il processo: chi sta lavorando non si vede più staccare.

**2. Le sezioni introduttive in cima agli elenchi**, con i PDF della biblioteca (§AC).

**3. L'editor dell'aeroporto non butta più via il lavoro**: «Fine modifica» salva invece di uscire in
silenzio, e l'avviso «stai per perdere le modifiche» adesso copre tutti i campi, non uno su tre (§AD).

## ⚠️ Le due cose nuove da sapere

> ### 1. Dopo il caricamento, premete UN TASTO
>
> Da questo pacchetto la pagina **non avvia più da sola** la parte interattiva del sito: lo fa un file
> nostro, `wwwroot/_content/Vipi.Ui/vipi-riconnessione.js`.
>
> Se quel file — o l'indice `Vipi.Host.staticwebassets.endpoints.json` — non arriva, **il sito si vede
> intero e non risponde a niente**: nessun errore in pagina, nessuna riga nei log, solo tasti che non fanno
> nulla. Aprire una pagina e vederla comparire **non basta** per accorgersene.
>
> Perciò, come ultimo controllo: aprite una pagina e **premete un tasto qualsiasi** (il selettore della
> lingua in alto va benissimo). Se risponde, il caricamento è completo.

> ### 2. I file di `wwwroot` e l'indice viaggiano INSIEME
>
> `Vipi.Host.staticwebassets.endpoints.json` è l'elenco che dice al sito **con quale nome** chiedere ogni
> file di `wwwroot`, impronta compresa. Caricare l'indice senza i file (o i file senza l'indice) fa chiedere
> al sito nomi che non esistono: pagine senza stile, o senza comportamento. È già successo il 24 agosto.
>
> Nella tabella qui sotto sono marcati con **(insieme)**: o si caricano tutti, o nessuno.

## I file da caricare

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP. La cartella del pacchetto ha la stessa struttura: si può trascinare rispettando i percorsi.

<!-- TABELLA-FILE -->

## L'ordine

1. **Caricate tutto col nome finto** (`.new` in fondo: `Vipi.Host.dll.new`, e così via). I file dentro
   `wwwroot/` non hanno bisogno del nome finto — nessuno li tiene aperti — ma non fa danno.
2. **Rinominate**, dal più profondo al più superficiale: prima i file di `wwwroot/`, poi l'indice
   `staticwebassets`, poi i `.dll`. ⚠️ I `.dll` per ultimi: appena il processo riparte, deve trovare
   `wwwroot` già a posto.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta** — è la richiesta che fa accorgere
   Passenger del file.
4. **Controllate** che sia partita la versione nuova: `diagnostica/avvio-diagnostica.txt`, prima riga, con
   l'ora di adesso; e nella barra in alto (da amministratore) il timbro dice **`1.1.0`**.
5. **Premete un tasto** (vedi l'avviso 1).

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto **non le tocca**: non c'è nessun file con quei nomi qui dentro. Se un programma FTP vi
propone di sincronizzare cartelle intere, **non fatelo** — caricate i file elencati e basta.

## Un file nuovo che comparirà da solo

`diagnostica/avvii.txt`. Ci scriviamo una riga ogni volta che il sito parte e una ogni volta che si ferma,
per poter rispondere a «quanto spesso succede?» invece di tirare a indovinare. **Non va cancellato**, ma se
lo cancellate non si rompe niente. Come si legge è spiegato in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md), passo 9.

## Se qualcosa va storto

Il rollback sono le rinomine al contrario: i file di prima sono ancora sul server col nome `.old` (li lascia
la procedura del foglio FTP). ⚠️ Vale la stessa regola: prima i `.dll`, poi `wwwroot`, poi riavvio.

E se il sito non riparte, mandateci `diagnostica/avvio-errore.txt` (se c'è) e
`diagnostica/avvio-diagnostica.txt`: nessuno dei due contiene password.
