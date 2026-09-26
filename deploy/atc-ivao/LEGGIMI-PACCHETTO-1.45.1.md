# Pacchetto 1.45.1 — solo i file cambiati

> **Timbro:** `1.45.1 · cb62ebc` (24 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.45.0** (`7bd3bda`, online dal 24 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare, niente in `wwwroot`.
> **4 file**, tutti in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **La Ricerca si aggiorna anche senza tastiera.** Il conteggio dei risultati partiva solo al rilascio di un tasto:
  incollando una parola, scegliendola dall'autocompletamento del browser o scrivendola da uno script, sotto il campo
  restava il conteggio vecchio («0 risultati per …») anche se la parola nuova trovava dei documenti. Ora la
  ricerca parte a ogni cambio del testo.

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
`Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.45.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivere **LIRF** → devono comparire dei
**documenti** (il 24 settembre erano 13). «0 risultati per LIRF» = qualcosa non va.

Col login da amministratore:

- il timbro **`1.45.1 · cb62ebc`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nella Ricerca, **incollare** «Brindisi» (non scriverlo): il conteggio deve cambiare da solo.

Da fuori, per chi verifica (⚠️ il 24 settembre Edge in modalità automatica non partiva su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
