# Pacchetto 1.23.0 — solo i file cambiati

> **Timbro:** `1.23.0 · da243f29` (11 settembre 2026, notte). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.22.1.** **9 file**. **Nessuna migrazione**: il database non si tocca.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟡 QUESTA VOLTA C'È `wwwroot`, E VIAGGIA INSIEME AL SUO INDICE
>
> `vipi-theme.css` è cambiato (le schede degli aeroporti, i filtri, le due porte di `/services`): va caricato
> **coi suoi `.br` e `.gz`** e **insieme** a `Vipi.Host.staticwebassets.endpoints.json`, o il sito chiede
> nomi che non esistono. Sono gli **unici** asset diversi: gli altri sono stati confrontati per impronta e sono
> identici.
>
> C'è anche **`en/Vipi.Ui.resources.dll`**: le frasi inglesi sono cambiate.
>
> ## 🔴 IL FILE DA NON DIMENTICARE
>
> **`Vipi.Host.dll`** porta il **timbro**, anche se il suo codice non è cambiato.
>
> ## L'ORDINE
>
> 1. prima si caricano **tutti e 9** i file col nome finto;
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, lasciando **`Vipi.Host.dll` per ultimo**.
>
> Qui nessun assieme cambia firma verso gli altri: un riavvio che cadesse a metà delle rinomine darebbe al
> massimo, per un minuto, le pagine nuove col foglio di stile vecchio (o viceversa). Si rimette da sé a rinomine
> finite.

---

## Che cosa portano questi nove file

### 🟢 1. Gli aeroporti di ogni ACC, con i vSOP militari dentro

**Chiesto da te.**

- **Pagina dell'ACC** (`/services/vsop/<acc>`): il riquadro «vSOP militari» **non c'è più**. Il riquadro
  «Aeroporti» conta anche i campi che hanno solo il vSOP.
- **Elenco aeroporti** (`/services/vsop/<acc>/airports`): compare **ogni scalo con almeno un documento
  pubblicato**, anche quelli «solo militare».
  - Sopra, i filtri: **Tutti** e le **quattro categorie**. Una categoria a zero è grigia e spenta.
  - Uno scalo con **vIPI e vSOP** ha due voci **accanto al nome**: «vIPI civile» e «vSOP militare».
  - Uno scalo con un solo documento resta una scheda tutta cliccabile.
- **Il vSOP militare aperto da qui si apre in vista ATC.** La scelta «Tutto / Pilota / ATC» in testa al
  documento resta. Dall'elenco nazionale dei vSOP si apre ancora in vista pilota.

⚠️ Un documento pubblicato **fuori categoria** (per esempio una vIPI su un campo «solo militare») resta
raggiungibile finché non lo nascondi: è la stessa regola di 1.22.0, e la Diagnostica lo segnala.

### 🟢 2. La pagina `/services`, per pubblico

**Chiesto da te.** Niente più banner «Servizi ATC». In alto due schede grandi, ognuna col suo pubblico:

| Scritta sopra | Scheda | Porta a |
|---|---|---|
| **Per i controllori del traffico aereo** | vSOP — documentazione operativa | `/services/vsop` |
| **Per i piloti militari** | Documentazione militare (in verde oliva) | `/services/vsop/mil` |

Sotto, **«Strumenti per controllori»**: Le mie statistiche ATC e Aurora Profile Swapper. La sezione dello staff
resta com'era. Da `/services/vsop` è sparita la scheda «vSOP militari»: i controllori li trovano negli
Aeroporti della loro ACC.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.23.0 · da243f2`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. **`/services`** (anche da anonimo): niente banner, le due schede grandi con «PER I CONTROLLORI DEL TRAFFICO
   AEREO» e «PER I PILOTI MILITARI». Il tasto «Apri» della scheda militare è **verde oliva**: se è blu, il
   foglio di stile nuovo non è arrivato — mancano `vipi-theme.css` (coi `.br`/`.gz`) o l'indice degli endpoint.
4. **`/services/vsop/limm/airports`** (o un'altra ACC con campi militari): i filtri per categoria, e i campi
   solo militari nell'elenco. Un clic su un vSOP apre il documento con «ATC» selezionato.
5. Una pagina in **inglese** (`EN` in alto): su `/services` si legge «FOR AIR TRAFFIC CONTROLLERS». Se esce in
   italiano, il satellite `en/Vipi.Ui.resources.dll` non è arrivato.
