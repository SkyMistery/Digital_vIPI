# Pacchetto 1.15.1 — solo i file cambiati

> **Timbro:** `1.15.1 · 68e71bfa` (7 settembre 2026, notte). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.15.0**, online da poche ore. **7 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, NIENTE `wwwroot`
>
> Nessuna migrazione, nessuna tabella e nessuna colonna nuova: si carica quando volete, anche dentro la
> finestra cieca fino al 16. E nessun foglio di stile cambia — la trappola dei file che «viaggiano insieme»
> **non si applica** a questo pacchetto.
>
> ## 🟢 E QUESTA VOLTA **NON** SERVE CARICARE TUTTO INSIEME
>
> Il foglio di 1.15.0 diceva il contrario, e per quel pacchetto era giusto: lì c'era una ripulitura che
> aveva reso privati 74 tipi fra un pezzo e l'altro dell'applicazione, quindi un pezzo vecchio accanto a uno
> nuovo esplodeva. **Qui no**: la visibilità non cambia, e sul server girano già tutti i pezzi di 1.15.0.
> Restano comunque **7 file**, quindi la differenza è teorica — ma se il caricamento si interrompe, non c'è
> lo stesso rischio.
>
> ## ⚠️ Ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: le etichette della scheda sono cambiate (sotto il perché) e il rimando
> all'ospite è nuovo. Senza quel file, chi legge in inglese le vedrebbe in italiano.

---

## 🔴 LA COSA IMPORTANTE: la scheda «Sezioni in comune» era girata al contrario

**Se avete già usato quella scheda con 1.15.0, controllate che cosa ha nascosto.**

In 1.15.0 la scheda chiedeva **quale documento TIENE** le sezioni comuni: selezionando la vIPI, le sezioni
restavano visibili nella vIPI e sparivano dal **vSOP**. Era l'opposto di come la richiesta era stata fatta —
e di come la legge chiunque: *seleziono la vIPI → nascondo quelle della vIPI*.

**Adesso è così**: si spuntano i **documenti da cui le sezioni spariscono**.

```
Sezioni in comune
Nascondi le sezioni comuni di:
  [x] vIPI — LICA        [ ] vSOP MIL — LICA
  [x] METAR & TAF   [x] Frequenze   [x] Piste   …   [ ] Validità e revisione
        [ Nascondi ]  [ Chiudi ]
```

- Con **tre** documenti uniti se ne possono spuntare due.
- Spuntarli **tutti** si può — «quel dato qui non lo vogliamo» — ma la scheda avvisa: quella sezione
  sparisce dalla pagina unita **per intero**.
- ℹ️ **Niente è perso in nessun caso**: «nascosto» è lo stesso stato del tasto «nascondi» dell'editor, e si
  toglie allo stesso modo, sezione per sezione. Se con 1.15.0 avete nascosto il documento sbagliato, riaprite
  la scheda, spuntate quello giusto e premete: le sezioni dell'altro tornano visibili da sole.

## ✨ E l'altra correzione: invertendo l'ordine, adesso si capisce dove è finito tutto

Nel pannello dell'unione le due frecce spostano i membri. Spostando il **primo** cambia l'**ospite**, e con
lui si sposta **l'editor unito**: i corpi degli altri documenti smettono di comparire in quella pagina e
ricompaiono in quella del nuovo ospite. Non si perdeva niente, ma non lo diceva nessuno — e sembrava che
l'unione fosse sparita.

Adesso, se il documento che state aprendo **non** è l'ospite, in cima al pannello c'è scritto qual è, con il
**link per andarci**. E in modifica, sotto l'elenco: «⚠️ il primo dell'elenco è l'ospite».

ℹ️ Cambiando il primo cambia anche l'**indirizzo pubblico** della pagina unita: l'altro ci reindirizza.

---

## ✅ Che cosa guardare dopo aver caricato

1. **La Ricerca**: due lettere nel campo in alto, la riga sotto deve cambiare. È l'unico controllo che passa
   dal **server**.
2. **Timbro**: `diagnostica/avvio-diagnostica.txt` deve dire `1.15.1 · 68e71bfa`.
3. Con occhi da **amministratore**: aprite l'editor di uno scalo unito, aprite «Sezioni in comune…» e
   guardate la prima riga — deve dire **«Nascondi le sezioni comuni di:»** con le **caselle**. Se dice «Le
   tiene:» con i pallini, sta girando ancora 1.15.0.
