# Pacchetto 1.30.2 — solo i file cambiati

> ✅ **CARICATO il 17 settembre 2026.** `Schema 0` confermato dal committente; da fuori `pacchetto-verifica.js`
> pubblico tutto verde, Ricerca compresa.

> **Timbro:** `1.30.2 · 76aceb3` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.30.1** (`be9a612`, online dal 16 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **8 file**: 5 in **radice**, 3 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

1. **Diagnostica a zoom alto** (§A54). Su uno schermo 2560x1600 con zoom 175% la colonna di destra tagliava la
   scheda della copia del database, e il tasto per scaricarla spariva. Ora la colonna scorre invece di tagliare.
2. **Il guasto dell'import del 16 settembre, 20:44Z** (§A55). Due processi del sito vivi insieme (Passenger ne
   aveva tenuto su uno della versione prima) hanno riscritto nello stesso istante i pezzi di forma degli stessi
   settori: l'import è caduto, e con lui tutti i ripieghi delle forme di quel giro. Ora, se un altro processo arriva
   prima, il sito rilegge e riprova; chi ha salvato non cade più.
3. **Le traduzioni Azure** (§A56). La risorsa nuova `ivao-it-translator` accetta la sua chiave **solo sul suo
   endpoint**, che fino a 1.30.1 non si poteva configurare. **Serve un passo sul server**, qui sotto.

## I 8 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-theme.css
wwwroot/_content/Vipi.Ui/vipi-theme.css.br
wwwroot/_content/Vipi.Ui/vipi-theme.css.gz
Vipi.Host.staticwebassets.endpoints.json
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i tre del foglio di stile **insieme** all'`endpoints.json`, poi i
   `.pdb`, poi i `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). I tre file del foglio di stile e
l'`endpoints.json` vanno **insieme**: l'indice dice con che nome il sito chiede ogni file.

⚠️ **Restano fuori** gli altri assiemi (codice non cambiato: diversi solo per l'identificativo di compilazione),
`Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`, identici a 1.30.1.

## 🔴 Il passo sul server per le traduzioni — DOPO il caricamento

Nel file `.json` della cartella `segreti`, nella sezione `Translation` → `Azure`, accanto alla chiave nuova e alla
regione, si aggiunge l'endpoint:

```json
"Azure": {
  "ApiKey": "…la chiave nuova…",
  "Region": "italynorth",
  "BaseUrl": "https://ivao-it-translator.cognitiveservices.azure.com/"
}
```

⚠️ **Dopo** il caricamento, non prima: con 1.30.1 quella riga non funziona (risponderebbe 404). Il riavvio non
serve a parte: la chiave si legge a ogni avvio, e il processo riparte da solo entro un minuto.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.30.2 · 76aceb3`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**, e la scheda della copia del database **col suo tasto**;
- dopo il passo delle traduzioni, in `diagnostica/avvio-diagnostica.txt` la riga
  `Translation:Azure:ApiKey … (regione: italynorth; endpoint: https://ivao-it-translator.cognitiveservices.azure.com/)`,
  e in `avvisi-log.txt` **nessuna** nuova riga `AuthFailed. azure: HTTP 401`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17 settembre (06:28Z) in MariaDB 11.4.10:
timbro `1.30.2 · commit 76aceb3`, `pacchetto-verifica.js` **tutto verde** (Ricerca compresa); Diagnostica a
1463x914 CSS (2560x1600 al 175%) con la scheda della copia **intera**; traduzione **vera** con la chiave nuova
sull'endpoint della risorsa: «1 nuove», nessun 401. ⚠️ Restano da sistemare, come prima, i segmenti che Azure
restituisce rotti e che si ripagano a ogni giro («… IAFs ILS14 …», «37th WING A/A TRAINING AREA»).
