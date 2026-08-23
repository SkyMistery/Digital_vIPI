# Caricare vIPI via FTP/SFTP (FileZilla)

Questa pagina serve **solo** se i file arrivano sul server con FileZilla invece che come zip scompattato da
console. Il resto della procedura — configurazione, database, servizio, proxy — resta quello di
[`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).

> ⚠️ **FileZilla mette i file sul server, non fa partire l'applicazione.** vIPI è un eseguibile Linux
> self-contained: dopo l'upload qualcuno con accesso alla shell (o un pannello che sappia farlo) deve
> rendere eseguibile `Vipi.Host`, installare il servizio `systemd` e configurare nginx. Se quell'accesso non
> c'è, il caricamento è **metà dell'opera**: si vedono i file e non si vede il sito.

---

## 1. Prima di aprire FileZilla

**Se sul server si può scompattare uno zip** — shell, oppure un file manager del pannello con «Estrai» —
caricate **solo il file `.zip`** e scompattatelo là. È un trasferimento invece di 407, non ci sono modalità
di trasferimento da sbagliare, e i permessi dentro l'archivio si conservano. Tutto il resto di questa pagina
serve solo quando quella strada non c'è.

**Se il sito è già in piedi** (aggiornamento, non prima installazione), la regola è una sola:
**non si sovrascrive un file mentre l'applicazione gira.**

Dove c'è una shell, si ferma il servizio prima (`sudo systemctl stop vipi`). Dove non c'è, si carica col
nome finto e si rinomina: è la procedura di
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md), e funziona ad applicazione accesa.

⚠️ **Su `atc.it.ivao.aero` non c'è né systemd né l'accesso al pannello Plesk**: il sito gira con Plesk +
Phusion Passenger, e chi lo aggiorna ha **soltanto l'FTP**. La procedura buona per quel server è quindi
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md) — non `LEGGIMI-AGGIORNAMENTO.md`, che
descrive la consegna del 23 agosto e dice «fermate l'applicazione dal pannello», cosa che lì **non si può
fare**. Di questo foglio restano validi i due punti che contano davvero, il **trasferimento binario** e il
**bit di esecuzione**.

⚠️ **Sovrascrivere una `.dll` di un processo vivo non dà nessun errore, e butta giù il sito.** `ETXTBSY`
protegge solo l'eseguibile in esecuzione; le librerie si lasciano troncare in silenzio, e il processo che le
ha mappate in memoria muore all'istante. È successo il 23→24 agosto 2026: il perché esteso sta nel foglio
qui sopra.

---

## 2. Le due impostazioni di FileZilla che contano

### Trasferimento binario

**Trasferimento → Tipo di trasferimento → Binario.** Non lasciate «Auto».

In modalità ASCII il client «corregge» i fine riga: su file di testo è innocuo, su un eseguibile e sulle
librerie `.so` significa **byte cambiati dentro il codice macchina**. Il pacchetto ne contiene decine, e
`Vipi.Host` e `createdump` **non hanno estensione** — cioè sono proprio i file su cui l'euristica di «Auto»
può decidere male (nelle impostazioni, sotto *Trasferimenti → Tipi di file*, esiste apposta l'opzione «tratta
i file senza estensione come file ASCII»). Il guasto non si vede caricando: si vede al primo avvio, con un
errore che non parla di FTP.

ℹ️ Su **SFTP** (porta 22, `sftp://`) il problema non esiste: quel protocollo trasferisce sempre in binario e
l'impostazione viene ignorata. Se potete scegliere, scegliete SFTP.

### Più trasferimenti in parallelo

**Modifica → Impostazioni → Trasferimenti → Numero massimo di trasferimenti simultanei: 4–8.** Il default è
2, e con 407 file la latenza di apertura di ogni connessione pesa più del contenuto.

Nella stessa pagina, se la connessione cade a metà: alzate il **timeout** (Connessione → Timeout) da 20 a
120 secondi.

---

## 3. Cosa si carica, e dove

Il contenuto della cartella pubblicata va **dentro** `/opt/vipi` (o la cartella scelta come
`WorkingDirectory` in `vipi.service`), **mantenendo le sottocartelle**: `content/`, `en/`, `deploy/`.

Trascinate la **cartella intera** dal riquadro di sinistra a quello di destra: FileZilla ricrea l'albero da
sé. Trascinare i singoli file «appiattisce» tutto in una cartella sola, e in quel modo il sito non parte.

⚠️ **Non caricate un `appsettings.Production.json` vuoto sopra uno già compilato.** Quel file contiene la
password del database e le chiavi IVAO: se lo sovrascrivete a ogni aggiornamento, l'applicazione riparte —
ma **su SQLite, con un database vuoto**, e sembra che il sito abbia perso tutti i contenuti. In un
aggiornamento, escludetelo dall'upload o rimettetelo subito dopo.

⚠️ **Non cancellate la cartella `vipi-keys`.** Su `atc.it.ivao.aero` sta **dentro** la cartella
dell'applicazione (`.../public_atc/vipi-keys`), in mezzo ai file che si sovrascrivono, perché l'accesso FTP
è confinato lì e non permette di crearla al livello superiore, che sarebbe il posto giusto. Contiene le
chiavi che firmano i cookie di sessione: **sovrascriverla non fa danno, cancellarla sì** — tutti gli utenti
collegati vengono disconnessi e devono rientrare. Una volta sola, nessun dato perso, ma è evitabile. Se
aggiornate cancellando prima la cartella remota, salvatela e rimettetela.

---

## 4. Dopo l'upload: i permessi

L'FTP **non trasporta il bit di esecuzione**. Su ogni file caricato che deve girare va rimesso a mano: in
FileZilla, tasto destro sul file nel riquadro remoto → **Permessi file…** → valore numerico.

| File | Permessi | Perché |
|---|---|---|
| `Vipi.Host` | `755` | è l'eseguibile; senza il bit x `systemd` risponde `Permission denied` |
| `createdump` | `755` | serve solo alla diagnostica dei crash, ma tanto vale |
| `appsettings.Production.json` | `600` | contiene password e segreti IVAO |

⚠️ Se il server è FTP puro e non accetta `SITE CHMOD`, la voce «Permessi file…» non compare o fallisce: in
quel caso il `chmod` lo deve fare chi ha la shell. Non c'è modo di aggirarlo dal client.

---

## 5. Verificare che sia arrivato tutto

Alla fine del trasferimento:

1. la scheda **«Trasferimenti falliti»** in basso dev'essere **vuota**. Se non lo è: tasto destro →
   *Ripristina e riaccoda tutti*, e ripetete finché non si svuota;
2. **Visualizza → Confronto directory** (con «confronta dimensione file» attivo) affianca locale e remoto e
   colora ciò che non combacia: è il modo più rapido per accorgersi di un file troncato;
3. il conto tondo di questa build: **407 file, 113 MB**.

---

## 6. Solo dopo, l'avvio

Da qui si torna a [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md): riempire `appsettings.Production.json` (passo 2),
installare `deploy/vipi.service` e `deploy/nginx-vipi.conf` (passo 4), registrare i redirect OIDC (passo 5).

Se qualcosa non parte, il primo posto da guardare è `diagnostica/avvio-errore.txt` accanto all'eseguibile —
e per i guasti tipici di un caricamento via FTP, questi sono i sintomi:

| Sintomo | Causa quasi certa |
|---|---|
| **Il sito va giù durante o subito dopo l'upload, e `diagnostica/` non dice niente di nuovo** | una `.dll` è stata **sovrascritta ad applicazione viva**: il processo è morto senza poter scrivere. Vedi [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md) |
| `Permission denied` all'avvio del servizio | manca il bit di esecuzione su `Vipi.Host` (§4) |
| `Exec format error` / `cannot execute binary file` | trasferimento avvenuto in ASCII (§2) |
| `An assembly specified in the application dependencies manifest was not found` | un file non è arrivato, o è arrivato troncato (§5) |
| Il sito parte ma è **vuoto**, e prima aveva i contenuti | `appsettings.Production.json` sovrascritto: l'app è ripartita su SQLite (§3) |
| Le pagine si aprono e non rispondono ai clic | non è l'FTP: sono i WebSocket non inoltrati dal proxy (LEGGIMI-DEPLOY, passo 4) |
