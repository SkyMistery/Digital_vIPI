# Pacchetto 1.46.2 — solo i file cambiati

> **Timbro:** `1.46.2 · f30c036` (25 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.46.1** (`3a8a3f1`, online dal 24 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare, niente in `wwwroot`.
> **7 file**: 6 in **radice** e 1 in **`en/`**.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **Coordinamenti su più aeroporti: la frase li nomina tutti.** Un accordo scritto per due aeroporti (per esempio
  LICC e LICZ) diceva nel testo solo il primo: «con destinazione Catania Fontanarossa LICC». Ora dice «con
  destinazione Catania Fontanarossa LICC e Sigonella LICZ».
- Nella tabella dei coordinamenti la colonna **«Anche per»** diventa **«Per»**, e ogni riga dice per quali aeroporti
  vale.
- ⚠️ Le versioni **già pubblicate** dei documenti tengono la frase di quando sono state pubblicate: il testo nuovo
  compare nell'editor subito, e in pubblico alla **prossima pubblicazione** del documento.

## I 7 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `en/` (1)** e **in radice (6)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
en/Vipi.Ui.resources.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto (quello di `en/` dentro la cartella `en/`);
2. si rinominano nell'ordine qui sopra: ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** tutti gli altri assiemi (anche `Vipi.Hosting.dll` e `Vipi.Infrastructure.dll`), `deps.json`,
`runtimeconfig.json`, `appsettings.json`, `Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a
1.46.1, controllato per impronta e per uso.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei
documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la
riga è cambiata.

Col login da amministratore:

- il timbro **`1.46.2 · f30c036`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nell'**editor** di un documento con un accordo su più aeroporti (quello dell'APP di Catania che aveva mostrato il
  difetto), sezione Coordinamenti: la frase nomina **tutti** gli aeroporti, e la colonna si chiama **«Per»**.

Da fuori, per chi verifica (⚠️ dal 24 settembre Edge in modalità automatica non parte su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
