# Pacchetto 1.25.3 — solo i file cambiati

> **Timbro:** `1.25.3 · e3092ea` (13 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.25.2.** **7 file**: 6 in **radice** e 1 nella sottocartella **`en/`**. ✅ **Nessuna
> migrazione**, niente `wwwroot`.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

🔴 **Una correzione di sicurezza, da caricare appena possibile.** Alcune pagine pubbliche rimettevano nella
pagina, senza neutralizzarlo, un pezzo dell'indirizzo. Un link costruito apposta, aperto da un
amministratore o da un editor, poteva eseguire codice nel suo browser con la sua sessione. Con questo
pacchetto quel testo esce come testo. È il primo lotto della revisione del 13 settembre
(`docs/history/audit-2026-09-13-revisione-totale-2.md`, voci T-001 e T-003).

E una regola che cambia, decisa dal committente: **eliminare** (settori, aeroporti, ACC, aree, confinanti,
documenti) torna **riservato agli amministratori** (T-012). Chi è editor vede il cestino spento, e passandoci
sopra legge perché.

## I 7 file, e l'ordine

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto (quello di `en/` dentro la cartella `en/`);
2. si rinomina nell'ordine qui sopra: prima ogni `.pdb`, poi il suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.
Poi, col login da amministratore, il timbro in barra: **`1.25.3 · e3092ea`**.

ℹ️ Al primo avvio l'applicazione rifà una volta le riconciliazioni interne dei documenti, come dopo ogni
versione nuova: è previsto.

---

# 🔴 Cambia un consiglio dei fogli precedenti: la Cache Rule e `passenger_min_instances` ASPETTANO

I fogli di 1.25.1 e 1.25.2 chiedevano tre cose nel pannello: la **Cache Rule su Cloudflare**, le **due
direttive nginx** per i file statici e **`passenger_min_instances ≥ 1`**.

⚠️ **Le direttive nginx vanno bene. Le altre due, per ora, no.** La revisione del 13 settembre ha
riprodotto un difetto (T-011): la memoria delle pagine pubbliche distingue i visitatori per cookie ma **non
per lingua del browser**. Oggi si vede poco solo perché il processo riparte ogni ~46 secondi e la memoria non
fa in tempo a servire. Appena il processo resta vivo, o appena Cloudflare comincia a tenere le pagine, il
primo visitatore con il browser in inglese deciderebbe la lingua della pagina anche per quelli dopo, e
viceversa.

Quindi: **nginx sì; Cache Rule e `passenger_min_instances` dopo il pacchetto che chiude T-011.** Lo diremo
nel foglio di quel pacchetto.
