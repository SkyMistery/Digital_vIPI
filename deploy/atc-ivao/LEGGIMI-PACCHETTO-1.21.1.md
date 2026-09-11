# Pacchetto 1.21.1 — solo i file cambiati

> **Timbro:** `1.21.1 · 034f9187` (11 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.21.0.** **4 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 IL PIÙ PICCOLO DA MESI
>
> **Nessuna migrazione**, nessuna pagina nuova, nessun `.css` o `.js`, nessuna frase nuova: niente
> `wwwroot`, niente satellite inglese. Solo `Vipi.Ui` (dove sta la correzione) e `Vipi.Host` (dove sta il
> numero di versione), coi loro `.pdb`.
>
> ⚠️ **Per ogni file, le due rinomine una SUBITO dopo l'altra** (`.dll` → `.vecchio`, poi `.nuovo` →
> `.dll`), e solo dopo si passa al file seguente. Fra le due il file **non c'è**, e qui il processo si
> riavvia da solo circa ogni cinquanta secondi: stamattina, caricando 1.21.0, un riavvio è caduto proprio
> in quel buco e l'avvio è fallito (`avvio-errore.txt` delle 06:56:55, `Vipi.Infrastructure` non trovato).
> Dodici secondi dopo è ripartito da solo, quindi nessun danno — ma è un buco che si può non aprire.
> L'ordine **fra** i file invece qui è indifferente.

---

## Che cosa porta

### 🔴 L'editor APP che cadeva aprendolo

L'errore che dal 7 settembre compariva in `errori-richieste.txt` aprendo l'editor di un avvicinamento
(`AppSectionsEditor`, e l'indice delle sezioni con «documento=NON caricato»). **Adesso ha un colpevole**: la
diagnostica aggiunta in 1.18.2 ha fatto vedere che, mentre la pagina si disegnava, il documento risultava
presente a una riga e vuoto due righe dopo. Lo svuotava un secondo caricamento, partito su un altro thread.

La correzione fa ripartire quel caricamento **sullo stesso thread che disegna**, così le due cose non si
intrecciano più. Vale per tutti gli editor dei documenti, e per le sei pagine che aspettano il proprio
caricamento prima di chiudersi.

⚠️ **In locale questo guasto non si riproduce**: è una corsa, e sul banco il database risponde prima che la
seconda operazione parta. È provato da un test costruito apposta, che fallisce sul codice di prima e passa su
questo. **La prova vera sarà il prossimo `errori-richieste.txt` scaricato dopo una giornata di lavoro**: le
voci `AppSectionsEditor` e «documento=NON caricato» non devono più comparire.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.21.1 · 034f9187`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. Col login, **apri un editor APP** (per esempio quello di Gioia): si apre, e prendendo il lock le sezioni
   restano al loro posto.
4. Fra qualche giorno, **`errori-richieste.txt`**: nessuna voce nuova con `AppSectionsEditor` o «documento=NON
   caricato».
