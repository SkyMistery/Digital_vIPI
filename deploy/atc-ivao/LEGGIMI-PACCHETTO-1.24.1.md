# Pacchetto 1.24.1 — solo i file cambiati

> **Timbro:** `1.24.1 · f8d7a75` (12 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.24.0.** **9 file**. **Nessuna migrazione**: il database non si tocca.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🔴 QUESTA VOLTA C'È UN FILE IN UNA SOTTOCARTELLA
>
> **`en/Vipi.Ui.resources.dll`** va dentro la cartella **`en/`**, non accanto agli altri: è il file delle
> frasi inglesi, e quest'aggiornamento ne porta 33 nuove. Messo in radice non serve a niente e l'inglese
> resta quello di prima — senza nessun errore da nessuna parte.
>
> ## 🔴 IL FILE DA NON DIMENTICARE
>
> **`Vipi.Host.dll`** porta il **timbro**, anche se il suo codice non è cambiato.
>
> ## L'ORDINE
>
> 1. prima si caricano **tutti e 9** i file col nome finto (`en/Vipi.Ui.resources.dll` nella sua cartella);
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, lasciando **`Vipi.Host.dll` per ultimo**.
>
> ⚠️ `Vipi.Ui.dll` e `Vipi.Application.dll` viaggiano **insieme**, e stavolta più strettamente del solito:
> il secondo ha **tolto** una proprietà e ne ha **cambiato il tipo** a un'altra, e il primo le usa. Se il
> processo si riavviasse con uno solo dei due, le pagine dei documenti d'aeroporto cadrebbero finché non
> arriva l'altro — dura il tempo delle rinomine, ed è per questo che si fanno una subito dopo l'altra.

---

## Che cosa portano questi nove file

### 🟢 1. Il METAR tradotto parla la lingua della pagina

**Segnalato da te.** In una vIPI o in un vSOP aperti **in inglese** il riquadro METAR restava in italiano:
«leggera pioggia», «foschia», «Calmo». Le etichette — *Wind*, *Visibility*, *Clouds* — erano tradotte da
sempre, erano i **valori** a non esserlo.

Adesso si traducono anche quelli, in tutte e due le lingue, e **seguono il documento** quando il documento ha
la lingua bloccata (un vSOP bloccato in inglese resta inglese anche nel meteo, a chiunque lo apra).

| | italiano | inglese |
|---|---|---|
| vento fermo | Calmo | Calm |
| `-SHRA BR FZFG` | leggera rovescio pioggia, foschia, congelantesi nebbia | light shower rain, mist, freezing fog |

Il resto del riquadro — gradi, nodi, hPa, `BKN 1200`, `>10 km` — non ha una lingua e non cambia.

### 🟢 2. La provenienza del METAR ora è roba da staff

Quando NOAA non risponde, il METAR arriva da una **scorta** (IVAO, poi VATSIM) e accanto al bollettino
compare una pastiglia col nome di chi l'ha dato. Da adesso quella pastiglia la vedono **lo staff di divisione
e chi sta sopra**, come il numero della regola pista che sta vincendo.

Non è il bollettino a essere riservato — quello resta **intero per chiunque**. È il fatto che la scorta stia
lavorando: un'informazione sullo stato del servizio, che a chi non lo gestisce somiglia a un guasto.

ℹ️ **Nella maggior parte dei giorni quella pastiglia non c'è per nessuno**: compare solo quando NOAA tace.

### 🟢 3. Una scorta lenta non si porta più via le altre

Le tre sorgenti (NOAA → IVAO → VATSIM) avevano **un solo** tetto d'attesa da spartirsi: la prima che si
piantava se lo prendeva tutto, e la terza — che il bollettino ce l'aveva — non veniva nemmeno interrogata. Il
riquadro diceva «METAR non disponibile» mentre un METAR c'era.

Adesso ogni sorgente ha il suo tetto, e una che si rompe resta un problema suo. Si vede solo nei giorni
storti, che sono esattamente quelli per cui la catena esiste.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.24.1 · f8d7a75`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. **Una vIPI d'aeroporto in inglese** (per esempio `/services/vsop/libb/airports?icao=LIBD&culture=en`):
   nel riquadro METAR le voci sono *Wind / Visibility / Clouds* **e i valori sono inglesi**. Se il METAR del
   momento è un CAVOK col vento da 160° non c'è molto da leggere: in quel caso basta che `Wind` dica
   `160° / 12 kt` e che non compaia nessuna parola italiana.
4. **La stessa pagina in italiano** (`&culture=it`): tutto come prima.
5. **Un vSOP militare** (per esempio `/services/vsop/libb/mil?icao=LIBG`): stesso riquadro, stesso
   comportamento.

⚠️ **Non c'è niente di nuovo da vedere nelle pagine pubbliche**: questa consegna non aggiunge sezioni né
documenti. Se il METAR del giorno è sereno e NOAA risponde, da fuori 1.24.1 e 1.24.0 si somigliano molto — la
differenza si vede aprendo una pagina **in inglese** con del tempo brutto in giro.
