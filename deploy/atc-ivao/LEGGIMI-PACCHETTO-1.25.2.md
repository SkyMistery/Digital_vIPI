# Pacchetto 1.25.2 — solo i file cambiati

> **Timbro:** `1.25.2 · a6367d4` (13 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.25.1.** **2 file**, tutti e due in **radice**. ✅ **Nessuna migrazione**, niente
> `wwwroot`, niente sottocartelle.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

Una correzione sola, e **da fuori non si vede niente**. Una delle modifiche di 1.25.1 — dire a Cloudflare
che il file principale del motore della pagina (`blazor.web.js`) si può tenere un giorno — **era online ma
non arrivava a Cloudflare**, che continuava a chiedere quel file al vostro server a ogni pagina aperta.
Con questo pacchetto smette.

## I 2 file, e l'ordine

```
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti e due** col nome finto;
2. si rinomina prima il `.pdb`, poi il `.dll`;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.
Poi, col login da amministratore, il timbro in barra: **`1.25.2 · a6367d4`**.

ℹ️ Al primo avvio l'applicazione rifà una volta le riconciliazioni interne dei documenti, come dopo ogni
versione nuova: è previsto.

---

# 🔴 E restano le stesse tre cose del pannello

Sono le stesse di 1.25.1, per esteso — con i `curl` di verifica — in
[`LEGGIMI-PACCHETTO-1.25.1.md`](LEGGIMI-PACCHETTO-1.25.1.md) e in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md):
la **Cache Rule su Cloudflare**, le **due direttive nginx** per i file statici, e
**`passenger_min_instances ≥ 1`**.

⚠️ **Sull'ultima c'è una cosa nuova, misurata il 13 settembre.** Una delle modifiche di 1.25.1 tiene in
memoria per un minuto le pagine pubbliche già preparate, così il secondo lettore non le fa ricostruire.
Provata sullo stesso binario funziona; **sul vostro server non serve mai**: dodici richieste alla stessa
pagina, dodici pagine ricostruite da capo. La spiegazione più probabile è quella già misurata a inizio
settembre — il processo viene fermato e riavviato di continuo, con una vita media di circa 46 secondi — e
una memoria di un minuto su un processo che ne vive 46 non fa in tempo a servire. Quindi quell'impostazione
non riguarda più solo l'attesa del primo visitatore: **senza, una delle modifiche già caricate non lavora**.
