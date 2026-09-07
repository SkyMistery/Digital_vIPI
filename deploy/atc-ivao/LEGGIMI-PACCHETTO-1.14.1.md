# Pacchetto 1.14.1 — solo i file cambiati

> **Timbro:** `1.14.1 · 27bd6616` (7 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.13.0** — non 1.14.0. **7 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 📌 Perché 1.14.1 e non 1.14.0
>
> **1.14.0 non è mai stata caricata.** Era pronta, aveva il suo foglio e il suo timbro, e nel frattempo ci è
> entrata una correzione. Il numero segue il **contenuto**, non la data in cui si decide di spedire: due zip
> diversi timbrati tutti e due `1.14.0` sarebbero esattamente l'ambiguità che il timbro esiste per impedire.
>
> ⚠️ **Quindi questo pacchetto contiene TUTTO 1.14.0 più la correzione**, e il suo foglio
> ([`LEGGIMI-PACCHETTO-1.14.0.md`](LEGGIMI-PACCHETTO-1.14.0.md)) resta valido per la parte che descrive: lo
> **stato della traduzione nell'editor** e i **link dei vSOP militari in vista pilota**. Vanno letti
> entrambi, e il controllo finale di quel foglio va fatto lo stesso.
>
> ℹ️ Lo zip `vipi-1.14.0-solo-file-cambiati.zip`, se ce l'avete già, **si butta**: è incompleto.

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete, anche dentro la finestra cieca fino al 16.
>
> ## 🟢 E NIENTE `wwwroot`
>
> Nessun foglio di stile e nessuno script cambiano: la trappola dei file che «viaggiano insieme»
> **non si applica** a questo pacchetto.
>
> ℹ️ Verificato **con le impronte**, non a memoria: confrontando l'intera cartella pubblicata con quella di
> 1.13.0 — **460 file per parte** — le uniche differenze sono negli assiemi. Nessun file di `wwwroot`, e
> nemmeno l'indice degli asset.
>
> ## ⚠️ Ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le righe del cruscotto della traduzione (già di 1.14.0). Senza quel
> file chi legge in inglese le vedrebbe in italiano.
>
> ## ℹ️ E mancano quattro progetti che «risultano cambiati»
>
> Cambiati davvero sono **due**: `Vipi.Ui` (da 1.14.0) e `Vipi.Infrastructure` (la correzione qui sotto).
> Più `Vipi.Host`, che porta sempre il timbro. `Vipi.Application`, `Vipi.Hosting`, `Vipi.Domain`,
> `Vipi.AuroraProfiles`, `Vipi.AuroraBridge.Contracts` e `Vipi.Infrastructure.MySqlMigrations` **non**
> entrano: le loro impronte cambiano a ogni ricompilazione (è l'identificativo interno dell'assieme), ma il
> codice è lo stesso di 1.13.0. Ogni file in più è una rinomina in più su un file che il processo tiene
> aperto: non è prudenza, è rischio.

---

## ✨ LA COSA NUOVA DI QUESTO PACCHETTO: le piste tornano lunghe quanto sono

La sorgente IVAO ha cambiato l'unità della **lunghezza pista**: prima la mandava in **piedi**, adesso in
**metri**. Il programma la convertiva, e con la sorgente nuova quella conversione **accorciava ogni pista a
un terzo** — una pista di 2 990 m sarebbe uscita da 911.

Da questo pacchetto la misura si prende com'è.

> ### 🔴 QUESTO PACCHETTO VA CARICATO **PRIMA** DEL PROSSIMO RE-IMPORT
>
> **Misurato sull'archivio di sviluppo il 7 settembre**: le lunghezze scritte oggi sono **giuste** — LIRF
> 16L/34R 3 902 m, LIMC 17L/35R 3 920 m, LIPZ 04R/22L 3 300 m, che sono le misure vere delle carte. Vuol
> dire che **nessun giro è ancora passato con la sorgente nuova**.
>
> Quindi questo pacchetto **non ripara: previene.** Con il programma vecchio, il primo re-import dopo il
> cambio della sorgente avrebbe riscritto ogni pista d'Italia a **un terzo** — 3 902 sarebbe diventato
> **1 189** — e nessun errore lo avrebbe annunciato: solo numeri più piccoli, plausibili a occhio.
>
> ⚠️ **Se invece un giro è già passato** (in produzione, dove non ho potuto guardare), le lunghezze sono già
> quelle piccole: allora il re-import dopo questo pacchetto le **rimette a posto**. Il modo di saperlo è
> guardare un numero prima di caricare, ed è il controllo 3 in fondo a questo foglio.
>
> **In tutt'e due i casi**, dopo il caricamento conviene far ripassare il giro dell'anagrafica aeroporti —
> dalla pagina **Sorgenti dati** (`/services/vsop/admin/sources`) o aspettando quello automatico — così le
> lunghezze vengono riscritte dalla sorgente nuova e si vede che sono quelle giuste.
>
> ⚠️ E **non si correggono a mano**: con la policy «Piste» accesa il programma rifiuta i cambi di lunghezza
> sulle piste di sorgente, ed è giusto così — sarebbe lavoro buttato al primo re-import.

ℹ️ Il campo della sorgente **non ha cambiato nome quando ha cambiato unità**: si chiama `length` come prima.
È il motivo per cui questa correzione, da oggi, ha un suo test — prima quel pezzo di programma non ne aveva
nessuno.

---

## I file

Tutti in `httpdocs/app/` (o dove sta l'applicazione), rispettando le sottocartelle.

```
Vipi.Infrastructure.dll     Vipi.Infrastructure.pdb
Vipi.Ui.dll                 Vipi.Ui.pdb
Vipi.Host.dll               Vipi.Host.pdb
en/Vipi.Ui.resources.dll
```

Le impronte `sha256` di tutti e sette stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

---

## Il controllo finale — e non è «la pagina si apre»

⚠️ Il selettore della lingua, lo zoom e il tema **funzionano anche su un sito in cui Blazor non è mai
partito**: non provano niente. Serve un comando che passa dal **server**.

1. **La Ricerca risponde.** `https://atc.it.ivao.aero/services/vsop/search` → si scrivono due lettere → la
   riga sotto il campo deve cambiare. Se resta ferma, il caricamento è incompleto: rifarlo.
2. **La versione è quella giusta.** `diagnostica/avvio-diagnostica.txt`, riga `Versione`: deve dire
   **`1.14.1`**.
3. **Le piste** (il controllo di questo pacchetto), e conviene farlo **due volte: prima e dopo**.
   Si apre **LIRF** e si guarda la sezione Piste: la 16L/34R deve dire **circa 3 900 m** (nell'archivio
   sono 3 902).
   - **Prima** di caricare: se dice già **~1 189**, un giro è passato con la sorgente nuova e il programma
     vecchio — le lunghezze sono da rimettere a posto, e il re-import dopo il caricamento le rimette.
   - **Dopo** il caricamento e il re-import: deve dire **~3 900**. Se dopo il re-import dicesse ~1 189, il
     file `Vipi.Infrastructure.dll` non è stato caricato: si rifà.
4. **La cosa nuova di 1.14.0 c'è.** Si apre un documento in **modifica**, barra della lingua su **IT**, e si
   apre il blocco «Traduzione»: deve comparire la riga con le due percentuali e il tasto «Traduci ora».
   Se dice solo «stai leggendo nella lingua in cui questo documento è scritto», sta girando la versione
   vecchia.

ℹ️ I punti 3 e 4 vanno fatti **mentre chi ha caricato è ancora al telefono**: è il momento in cui un file
dimenticato si rimette in trenta secondi.
