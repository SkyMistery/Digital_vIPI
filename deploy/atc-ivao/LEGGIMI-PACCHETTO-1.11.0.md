# Pacchetto 1.11.0 — solo i file cambiati

> **Timbro:** `1.11.0 · 4b35946` (6 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.
>
> ⚠️ **Se avevate già scaricato uno zip 1.11.0 il 5 settembre, buttatelo**: quello diceva
> `1.11.0 · 072cb13` e non conteneva la correzione della testata (qui sotto). Vale questo, con questo
> timbro.

> **Sostituisce 1.10.0.** **18 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete, anche dentro la finestra cieca fino al 16.
>
> ## ⚠️ C'È `wwwroot`: SETTE FILE CHE VIAGGIANO INSIEME
>
> È l'unica trappola vera del pacchetto, ed è la stessa del 24 agosto. Questi sette devono arrivare
> **tutti**, nello stesso caricamento:
>
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css` + `.br` + `.gz`
> - `wwwroot/_content/Vipi.Ui/vipi-media.js` + `.br` + `.gz`
> - `Vipi.Host.staticwebassets.endpoints.json`
>
> L'ultimo è l'**indice**: dice al sito con che nome chiedere ogni foglio di stile e ogni script. Caricarne
> uno senza l'indice — o l'indice senza gli altri — fa chiedere al browser nomi che sul server non esistono,
> e la pagina esce **senza grafica** o con l'editor che non risponde.
>
> ## ⚠️ E ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le frasi della maniglia delle immagini, dell'avviso «la sorgente
> mette questo aeroporto sotto un altro ACC» e della nuova riga «da rivedere». Senza quel file la parte nuova
> si vedrebbe in italiano anche a chi legge in inglese.

---

## ✨ LA COSA NUOVA: le immagini si ridimensionano

Fino a 1.10.0 ogni foto messa in un documento si rendeva **a tutta colonna**: uno schema piccolo veniva
ingrandito fino al bordo, una foto verticale spingeva il testo di una pagina intera.

Adesso, nell'editor, l'immagine ha una **maniglia** nell'angolo in basso a destra: **si trascina** e la
figura si stringe, con una pastiglia che mostra la percentuale. Le frecce della tastiera la muovono di 5
punti per volta.

- La misura è una **percentuale della colonna**, non dei pixel: la stessa immagine si legge su un monitor, su
  un telefono e su un A4, e solo un rapporto vale in tutti e tre.
- Le immagini già dentro i documenti **non cambiano**: restano a tutta larghezza finché qualcuno non le
  stringe.
- **I documenti già pubblicati non cambiano affatto**: una release è congelata, e la larghezza nuova entra
  alla prossima pubblicazione.

---

## 🔴 UNA PERDITA DI DATI CHE È STATA CHIUSA — e cosa NON si può recuperare

È il difetto che avete segnalato: *«ho fatto una tabella e sotto un'immagine; chiuso l'editing, la tabella
spariva»*. Non era momentaneo, ed è più serio di come sembrava: quel contenuto **veniva sovrascritto**.

Succedeva nelle sezioni che hanno **una scheda disegnata dal sito più i blocchi scritti a mano** — nel vSOP
militare sono Radioassistenze, Frequenze ATC/CRC, Aeroporti alternati, Piste, Nominativi, Parcheggi e Aree di
lavoro. Se in una di quelle sezioni si scriveva una tabella (o si metteva un'immagine) **prima** che la
scheda avesse salvato qualcosa di suo, al primo salvataggio della scheda il suo contenuto veniva scritto
**sopra** quello di chi redige.

**Da 1.11.0 non succede più.** ⚠️ Ma quel che è già stato sovrascritto **non torna**: il contenuto vecchio
non c'è più da nessuna parte. Se vi ricordate di tabelle sparite in quelle sezioni, vanno riscritte.

---

## ⚠️ AEROPORTO CHE CAMBIA ACC: tre cose che si vedono

Alla domanda «se IVAO sposta un aeroporto da un centro all'altro, che succede?» la risposta era: **niente, e
nessuno lo dice**. Ora:

### 1. Nella pagina **Gestione aeroporti** può comparire un avviso giallo

Dice, per ogni scalo: *«qui sotto LIBB, nella sorgente sotto LIRR»*. **Non sposta niente da sé** — spostare
un aeroporto cambia gli elenchi di due centri e stacca i collegamenti fra le posizioni, ed è una decisione
di chi amministra. Lo spostamento si fa con la **tendina dell'ACC** nella riga, come sempre.

### 2. Lo spostamento adesso **tiene**

Prima lo spostamento veniva **annullato da solo** dopo qualche ora: il giro notturno riportava le posizioni
al centro di prima. Se in passato avete spostato un aeroporto e ve lo siete ritrovato com'era, era questo.

### 3. Dopo uno spostamento compaiono righe «da rivedere»

Sulla vIPI del centro che perde lo scalo, su quella del centro che lo prende e sui documenti vicini. Dicono:
*«l'aeroporto LIBD è passato da LIBB a LIRR: la copertura raccontata qui non è più quella»*. Non è un guasto:
è un promemoria da spuntare quando avete riletto. La copia **già pubblicata** resta com'è finché non la
ripubblicate — è congelata, ed è giusto così.

➕ E due dettagli: i link vecchi (`/services/vsop/libb/airports?icao=…`) ora **rimandano al centro giusto**
invece di mostrare quello sbagliato, e l'ordine «in evidenza» **non segue** lo scalo che cambia centro — lo
rimette il centro nuovo, se lo vuole.

---

## ✨ LA TESTATA DEI DOCUMENTI: i tre tasti a destra, e si vede quale è scelto

Sopra ogni documento ci sono tre tasti che scelgono **che cosa leggere**: `Everything`, `Pilot`, `ATC`.

**Erano tutti e tre uguali.** Il tasto scelto veniva marcato nel codice della pagina, ma nel foglio di stile
non c'era **nessuna regola** che gli desse un aspetto diverso: aprendo il filtro «piloti», il documento si
filtrava davvero, ma niente diceva **quale dei tre** fosse attivo. Adesso il tasto scelto è **blu pieno con
la scritta bianca**, in tema chiaro e in tema scuro.

E la fila dei tre è stata spostata: sta **a destra** della testata, sotto i tasti «Stampa» ed «Editor», ed è
**alta quanto le due righe** che le stanno accanto — il sottotitolo e l'avviso rosso. Prima era appesa in
coda al sottotitolo, piccola, in mezzo al testo. Su telefono la testata torna a una colonna sola e i tre
tasti vanno a capo interi.

⚠️ **Cambia in tutti e cinque i tipi di documento**: vIPI di centro, vIPI d'aeroporto, vIPI di APP, vLOA e
vSOP militare. Non cambia **niente** di ciò che i documenti dicono: è solo il comando che si vede meglio.

⚠️ **Sulla carta stampata non cambia nulla**: quei tasti sul foglio non ci sono mai stati (sono un comando,
non contenuto) e la pastiglia «pilota»/«ATC» accanto ai titoli di sezione continua a stamparsi come prima.

### E sul vSOP militare: il collegamento alla vIPI civile si è spostato

Era una riga di testo **sotto** l'avviso rosso, e si leggeva come una nota. Ora è un **tasto**, in cima a
destra, **a sinistra di «Stampa»**. Compare come prima **solo** dove la vIPI civile esiste davvero.

---

## I 18 file

| Dove | File |
|---|---|
| radice | `Vipi.Host.dll` + `.pdb`, `Vipi.Ui.dll` + `.pdb`, `Vipi.Application.dll` + `.pdb`, `Vipi.Infrastructure.dll` + `.pdb`, `Vipi.Domain.dll` + `.pdb` |
| radice | `Vipi.Host.staticwebassets.endpoints.json` |
| `en/` | `Vipi.Ui.resources.dll` |
| `wwwroot/_content/Vipi.Ui/` | `vipi-theme.css` + `.br` + `.gz`, `vipi-media.js` + `.br` + `.gz` |

Le impronte `sha256` di tutti e 18 sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

---

## Dopo il caricamento, la verifica in quattro righe

1. La barra in alto (da amministratore) dice **`1.11.0 · 4b35946`**.
2. `diagnostica/avvio-diagnostica.txt` dice la stessa versione.
3. 🔴 **La Ricerca risponde** — `/services/vsop/search`, si scrivono due lettere e si preme **Invio**: la
   pagina deve tornare con i risultati. È il controllo che conta, perché passa dal **server**: un sito
   caricato a metà si vede intero, ha il timbro giusto e non risponde a niente. Il selettore della lingua,
   il tema e lo zoom **non** servono a provarlo: funzionano anche a sito morto.
4. Aprite un documento qualsiasi: i tre tasti `Everything / Pilot / ATC` devono stare **a destra**, alti
   quanto le due righe accanto, e quello scelto **blu pieno**. Se sono piccoli e in coda al sottotitolo,
   `wwwroot` è arrivato a metà — il foglio di stile nuovo non c'è.
