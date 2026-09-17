# Pacchetto 1.30.4 — solo i file cambiati

> **Timbro:** `1.30.4 · a10d350` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.30.3** (`b5be0ff`, online dal 17 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **10 file**: 7 in radice, 3 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

Due correzioni, tutte e due trovate oggi dai file di `diagnostica/`.

1. **Radioassistenze: scegliere il tipo dal suggerimento non fa più morire la pagina** (§A61). Segnalato da un admin
   alle 12:12: nella colonna *Tipo*, scegliendo «ILS» dall'elenco, compariva «Something went wrong». Scegliere da un
   elenco di suggerimenti mandava due salvataggi insieme, e il secondo trovava il database ancora occupato dal primo.
   Vale per l'editor del vSOP militare e per la pagina *Radioassistenze* dell'amministrazione.
2. **Il giro delle traduzioni fa due passate invece di otto** (§A62). La configurazione raddoppiava la lista delle
   lingue (`it, en, it, en`): ogni quarto d'ora ogni verso si traduceva quattro volte, e i due segmenti che Azure
   restituisce rotti si ripagavano quattro volte — 3 528 caratteri a giro invece di 882. Nessuna traduzione cambia.

## I 10 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-editor.js
wwwroot/_content/Vipi.Ui/vipi-editor.js.br
wwwroot/_content/Vipi.Ui/vipi-editor.js.gz
Vipi.Host.staticwebassets.endpoints.json
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i tre file di `wwwroot` **e** `endpoints.json` (viaggiano insieme), poi
   ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** gli altri assiemi (codice non cambiato), `en/Vipi.Ui.resources.dll` (nessuna frase cambiata), il
resto di `wwwroot`, `Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.30.3, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.30.4 · a10d350`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- **chi aveva segnalato il guasto riprova**: *Radioassistenze* (o l'editor del vSOP militare), colonna *Tipo*, scegliere
  «ILS» **dall'elenco dei suggerimenti**. Il valore resta, la pagina continua a rispondere. ⚠️ Prima di riprovare
  **ricaricare la pagina** (Ctrl+F5): il file `vipi-editor.js` nuovo arriva solo così.

Via FTP, dopo il prossimo giro di traduzioni (ogni quarto d'ora, il primo due minuti dopo l'avvio): in
`diagnostica/log-AAAA-MM-GG.txt` ogni giro ha **due** righe «Traduzione it→en / en→it (azure)», non otto.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17-set in MariaDB 11.4.10: timbro
`1.30.4 · commit a10d350`, `pacchetto-verifica.js` **10/10**. Il guasto di §A61 riprodotto con una sonda nuova
(`ils-verifica.js`: cinque coppie di `change` sul *Tipo* dell'anagrafica) **sui due codici**: su 1.30.3 la barra d'errore
di Blazor e lo **stesso stack di produzione** («A second operation was started», `AdminNavaidsPage.Scrivi`); su 1.30.4
nessun errore, `errori-richieste.txt` non nasce, valore scritto e riletto.
⚠️ La metà JavaScript (il `change` del browser che si mangia) non si prova da un browser pilotato: il menu nativo dei
suggerimenti non si comanda. Per questo la riprova dell'admin conta. §A62 è provato col legame vero di
`appsettings.json` in un test; dal vivo si vede solo con la chiave di Azure, cioè online.
