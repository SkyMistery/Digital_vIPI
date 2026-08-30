# vIPI — aggiornare atc.it.ivao.aero via FTP, senza pannello

> **Questa è la procedura buona per `atc.it.ivao.aero`.** Sostituisce i passi di caricamento di
> [`LEGGIMI-FTP.md`](LEGGIMI-FTP.md) e di [`LEGGIMI-AGGIORNAMENTO.md`](LEGGIMI-AGGIORNAMENTO.md), che
> davano per scontate due cose che su quel server **non ci sono**: una shell e l'accesso al pannello Plesk.
>
> Riguarda **solo i file**. Se un pacchetto porta anche un `.sql`, il suo foglio di correzione lo dice.

## La regola, in una riga

**Non si sovrascrive mai un file dell'applicazione mentre l'applicazione gira. Si carica con un altro nome,
e poi si rinomina.**

---

## Perché, se no, il sito va giù

Il 23→24 agosto 2026 il sito è rimasto **completamente irraggiungibile** dopo aver caricato due soli file su
un'applicazione in funzione. Il pacchetto era sano; il modo di caricarlo, no.

Un processo .NET tiene le proprie librerie `.dll` **mappate in memoria**. L'FTP, per sovrascrivere un file,
lo **tronca sul posto** e lo riscrive da zero. Sotto la mappatura il contenuto sparisce: il processo vivo
muore all'istante, senza poter eseguire più niente — nemmeno scrivere la propria diagnostica. Passenger
prova a rigenerarlo e trova un file scritto a metà.

Rinominare, invece, **non tocca il file mappato**: il processo vivo continua a leggere la propria copia,
mentre sul percorso compare il file nuovo. Nessun troncamento, nessun momento in cui il file esiste a metà.

ℹ️ Nota per chi conosce l'errore `ETXTBSY` («file in uso»): il kernel protegge **solo** l'eseguibile in
esecuzione (`Vipi.Host`, `createdump`). Le `.dll` non hanno nessuna protezione — si lasciano sovrascrivere
in silenzio. Cioè: l'unico file che *non si può* rovinare è quello che vi dà errore. Quelli che *si possono*
rovinare non dicono niente.

---

## Prima volta soltanto: creare `tmp/`

Passenger riavvia l'applicazione quando cambia la **data** di `tmp/restart.txt` nella radice
dell'applicazione. Se quella cartella non esiste, **quel passo non fa niente** — ed è la situazione in cui il
server è stato fino al 24 agosto 2026.

In FileZilla, dentro `/var/www/vhosts/it.ivao.aero/public_atc/`: tasto destro nel riquadro remoto →
**Crea cartella** → `tmp`. Dentro, caricate un `restart.txt` vuoto (un file di testo vuoto va benissimo:
conta la data, non il contenuto).

⚠️ Senza `tmp/` il sito **riparte lo stesso**, ma quando gli pare: Passenger genera i processi a richiesta e
spegne quelli inattivi dopo circa cinque minuti. Ecco perché finora «dopo l'upload è sempre ripartito da
solo». Il punto di `restart.txt` non è **far** ripartire l'applicazione: è farla ripartire **quando decidete
voi**, cioè a trasferimento finito invece che nel mezzo.

---

## La procedura

### 1. FileZilla in binario

**Trasferimento → Tipo di trasferimento → Binario.** Non «Auto». Il perché è in
[`LEGGIMI-FTP.md`](LEGGIMI-FTP.md) §2.

### 2. Le quattro cose da NON cancellare

Si sovrascrive **senza svuotare** la cartella dell'applicazione:

| Cosa | Dove | Se sparisce |
|---|---|---|
| `segreti/` | `…/public_atc/segreti` | **la password del database e le credenziali IVAO**. Senza, il sito non raggiunge l'archivio: riparte su uno SQLite vuoto e *sembra che i dati siano spariti*. ⚠️ Il file dentro ha un nome scelto da voi e scritto da nessuna parte: se lo perdete, non lo ricostruiamo noi |
| `appsettings.Production.json` | radice dell'applicazione | dice quale motore usare (`MySql`), su quale nome risponde il sito e dove sta il key-ring. Senza, l'applicazione ricade sui default e parte su uno SQLite vuoto, con lo stesso sintomo |
| `vipi-keys/` | `…/public_atc/vipi-keys` | sono le chiavi che firmano le sessioni: **ogni** login fallisce |
| `tmp/` | `…/public_atc/tmp` | serve per il riavvio |

> ⚠️ **Fino al 30 agosto 2026 questa tabella ne elencava tre, e `segreti/` non c'era.** La cartella è nata
> il 24 agosto ([`LEGGIMI-SEGRETI.md`](LEGGIMI-SEGRETI.md)) e questo foglio non è stato aggiornato: chi lo
> avesse seguito alla lettera per un pacchetto intero avrebbe potuto perdere la password del database
> **senza che niente lo avvertisse**. E il sintomo è il peggiore possibile — il sito riparte, sembra
> funzionare, e l'archivio è vuoto.
>
> La riga di `appsettings.Production.json` diceva anche «contiene la password del database»: **non è più
> vero dal 24 agosto**, la password sta in `segreti/`. Il file resta comunque da non cancellare, per le
> ragioni scritte nella tabella.

⚠️ La cartella `deploy/` del pacchetto **non va caricata**.

### 3. Caricare col nome finto

Caricate i file del pacchetto **aggiungendo `.nuovo`** in fondo al nome. Esempio, per la correzione del
24 agosto (un file solo):

```
Vipi.Host.dll   →   caricato sul server come   Vipi.Host.dll.nuovo
```

Potete rinominarli sul vostro PC prima di caricarli, oppure sul server subito dopo: l'importante è che sul
percorso definitivo **non arrivi mai** un file mentre l'applicazione lo sta usando.

Con quel nome il runtime li ignora del tutto: questo passo si fa **ad applicazione accesa**, senza fretta e
senza rischi. Il sito continua a funzionare normalmente.

### 4. Misurare, prima di toccare qualsiasi cosa

F5 nel riquadro remoto, e confrontate la **dimensione in byte** di ogni `.nuovo` con quella del file
corrispondente sul vostro PC. Devono essere **identiche**.

Se una non corrisponde, ricaricate quel file. **Non proseguite** finché non corrispondono tutte: fin qui non
avete toccato niente, il sito sta ancora girando sulla versione di prima.

ℹ️ Ogni foglio di correzione riporta la dimensione attesa dei file che consegna.

### 5. Lo scambio: due rinomine per file

Nel riquadro remoto, tasto destro → Rinomina. Per ogni file, nell'ordine:

1. `Vipi.Host.dll` → `Vipi.Host.dll.vecchio`
2. `Vipi.Host.dll.nuovo` → `Vipi.Host.dll`

Sono operazioni istantanee, e il processo in funzione non se ne accorge.

⚠️ **Lasciate i `.vecchio` sul server.** Sono il rollback, già pronto e già verificato: se qualcosa non va,
si torna indietro con due rinomine e senza ricaricare niente. Si cancellano al giro dopo, quando il nuovo ha
dimostrato di funzionare.

### 6. Riavviare

Caricate di nuovo un `restart.txt` vuoto dentro `tmp/`, sovrascrivendo quello che c'è. Poi aprite
`https://atc.it.ivao.aero/services/vsop`: è la richiesta che fa nascere il processo nuovo.

### 7. Verificare che sia partita **la versione nuova**

⚠️ **Che il sito risponda non basta**: potrebbe essere il processo di prima, ancora vivo, o uno rigenerato
sui file vecchi. La prova sta in un file.

Scaricate `diagnostica/avvio-diagnostica.txt` dalla radice dell'applicazione e leggete la **prima riga**:

| Cosa dice | Significa |
|---|---|
| data e ora di **adesso** | l'applicazione è ripartita, ed è la versione nuova |
| una data **vecchia** | non è ripartita: sta ancora girando quella di prima |

Controllate anche che **non esista** `diagnostica/avvio-errore.txt`. Se c'è, scaricatelo e mandatecelo: la
prima riga dice la causa.

ℹ️ Se `avvio-errore.txt` c'è ma porta una data **vecchia**, è il residuo di un guasto già risolto:
leggetelo, poi **cancellatelo**. Finché resta lì, questo controllo darà sempre un falso allarme. (Sul server
ce n'era uno del 16 agosto 2026, rimasto per otto giorni.)

### 8. I controlli finali

| Cosa aprite | Cosa deve succedere |
|---|---|
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC: LIRR, LIMM, LIBB |
| Il login IVAO | entrate, e in alto compare il vostro nome |
| `https://atc.it.ivao.aero/services/vsop/search`, scrivendo `LI` nel campo | la riga sotto il campo deve **cambiare** in «N risultati per LI». Se resta «Digita almeno 2 caratteri», il sito è quello mezzo caricato — vedi l'avviso qui sotto |
| `https://atc.it.ivao.aero/_content/Vipi.Ui/vipi-riconnessione.js` | deve comparire del testo, non un **404** |

> ### ⚠️ Il guasto che si vede solo provando la Ricerca
>
> Dal pacchetto del 31 agosto 2026 la pagina **non avvia più Blazor da sola**: lo fa un file nostro,
> `wwwroot/_content/Vipi.Ui/vipi-riconnessione.js`, e serve per poter scrivere i tempi di riconnessione.
>
> Se un caricamento porta i `.dll` **senza** i file di `wwwroot` — o scambia l'indice
> `Vipi.Host.staticwebassets.endpoints.json` senza i `.js` che nomina, ed è già successo il 24 agosto —
> il sito **si vede intero e non risponde a niente**: nessun errore in pagina, nessuna riga nei log, solo
> tasti che non fanno nulla. Un controllo che guardi soltanto se le pagine si aprono **non lo vede**.
>
> ⚠️ **E non basta «premere un tasto qualsiasi»**: il selettore della lingua, lo zoom e il tema
> chiaro/scuro sono collegamenti e codice che vive dentro la pagina, e **funzionano lo stesso** su un
> sito morto. Il controllo che distingue davvero i due casi è la **Ricerca**, qui sopra: quella la
> calcola il server a ogni lettera che si scrive. Provata in tutt'e due i modi il 31 agosto 2026 —
> col pacchetto completo la riga cambia, col file mancante no.

### 9. Quanto spesso riparte il processo — `diagnostica/avvii.txt`

Questo file non serve all'aggiornamento: serve **dopo**. Passenger spegne l'applicazione quando per un po'
nessuno la usa e la rigenera alla richiesta successiva — è normale, e l'unica conseguenza è che chi aveva
una pagina aperta la vede ricaricarsi da sola. Il file scrive **una riga per avvio e una per arresto**, in
coda, e permette di distinguere quel caso da un'applicazione che invece **si rompe**:

| Cosa leggete | Significa |
|---|---|
| poche righe, nelle ore vuote, ognuna con «il precedente si era spento in modo ordinato» | è Passenger: fisiologico, non c'è niente da fare |
| tante righe, o raggruppate nelle ore di punta | qualcosa si rompe: mandatecelo |
| una riga `AVVIO` con «⚠ il processo precedente NON si è spento in modo ordinato» | il processo di prima è **morto male** — crash, memoria esaurita, oppure una `.dll` sovrascritta mentre girava (vedi la regola in cima a questo foglio). Mandateci anche `avvio-errore.txt` |

⚠️ **Una riga `AVVIO` «non ordinato» subito dopo un aggiornamento è attesa e non è un guasto**: è
l'applicazione vecchia che è stata sostituita. Contano quelle che arrivano **nei giorni dopo**.

ℹ️ Il file si pota da solo e non supera qualche centinaio di kilobyte: **non va cancellato**, ma se lo
cancellate non si rompe niente — ricomincia da capo, e si perde solo la storia.

---

## Se qualcosa va storto: il rollback

È già tutto sul server, sono due rinomine al contrario per ogni file:

1. `Vipi.Host.dll` → `Vipi.Host.dll.fallito`
2. `Vipi.Host.dll.vecchio` → `Vipi.Host.dll`

Poi `restart.txt` in `tmp/` e riaprite il sito. Tornate esattamente alla situazione di prima.

⚠️ **Tenete da parte i `.fallito`** e diteci che c'è stato un rollback: quei file, insieme a
`diagnostica/avvio-errore.txt`, sono ciò che ci fa capire il guasto. Senza, si ricostruisce a indovinare —
ed è quello che il 23 agosto è costato una serata.

---

## Se invece si può scompattare uno zip

Se il pannello o il file manager sa **estrarre uno `.zip`** direttamente sul server, quella resta la strada
migliore per i pacchetti interi: un trasferimento invece di 420, e nessuna modalità di trasferimento da
sbagliare. Anche in quel caso, però:

1. estraete in una cartella **a parte** (per esempio `nuovo/`), mai sopra l'applicazione viva;
2. togliete `deploy/`;
3. rinominate: `public_atc/wwwroot` → `wwwroot.vecchio`, poi `nuovo/wwwroot` → `wwwroot`, e così via per
   ogni cartella e per i file di radice;
4. `restart.txt`, e il passo 7 qui sopra.

La regola non cambia: **rinominare, non sovrascrivere.**
