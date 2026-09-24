# Pacchetto 1.46.1 — solo i file cambiati

> **Timbro:** `1.46.1 · 3a8a3f1` (25 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.46.0** (`d54dbb0`, online dal 24 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare, niente in `wwwroot`.
> **4 file**, tutti in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **«Re-import da IVAO» nell'editor dei vSOP militari non fa più cadere la pagina.** Il messaggio di esito chiede
  anche il numero delle procedure SID/STAR, ma l'editor militare non le importava e non passava quel numero: la
  pagina si bloccava («errore, ricaricare») a ogni pressione del tasto (2 volte in produzione il 23 settembre). Ora
  il tasto importa anche le SID/STAR, come quello dell'editor aeroporto e come prometteva già la sua descrizione.

## I 4 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In radice (4)**, in quest'ordine:

```
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** tutti gli altri assiemi, `en/`, `deps.json`, `runtimeconfig.json`, `appsettings.json`,
`Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.46.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei
documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la
riga è cambiata.

Col login da amministratore:

- il timbro **`1.46.1 · 3a8a3f1`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- facoltativo: in un vSOP militare di uno scalo **solo militare** (per esempio LIBG), «Modifica» → «Re-import da
  IVAO» → deve comparire «Import da IVAO completato: … piste, settori …, N procedure SID/STAR» e la pagina resta
  viva. ⚠️ Il gesto **scrive** davvero (piste, settori e SID dello scalo dalla sorgente).

Da fuori, per chi verifica (⚠️ dal 24 settembre Edge in modalità automatica non parte su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
