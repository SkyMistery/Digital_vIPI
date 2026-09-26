# Pacchetto 1.44.1 — solo i file cambiati

> **Timbro:** `1.44.1 · ce8a59f` (23 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.44.0** (`3561423`, online dal 23 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **10 file**: 7 in **radice** e 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Se se ne carica uno senza l'altro, il sito chiede nomi che non esistono.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica e nei due minuti dopo il riavvio.

---

## Che cos'è

- **Tabelle nell'editor: la linea fra le colonne si vede sempre**, non solo passandoci sopra col mouse.
- **Le STAR nelle frasi dei trasferimenti.** In un arrivo, una STAR fra i punti ora si dice «autorizzato **alla
  STAR** ERIKA 1A» (in inglese «cleared **via the** ERIKA 1A **arrival**»). Fix e SID restano «via PISIP». Con fix e
  STAR insieme: «via PISIP o alla STAR ERIKA 1A». Vale per le vIPI ACC e APP, la vLOA e il ponte. Le copie
  pubbliche dei documenti mostrano la frase nuova dopo la prossima pubblicazione («Coordinamenti» è congelata).

## I 10 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice (7)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Ui` (cambia solo il suo foglio di stile, non il codice), `Vipi.Infrastructure`,
`Vipi.Infrastructure.MySqlMigrations`, `Vipi.Domain` e gli altri assiemi: differiscono solo perché ricompilati.
Restano fuori anche `en/Vipi.Ui.resources.dll` (nessuna frase dell'interfaccia cambiata), `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.44.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato):

- il timbro **`1.44.1 · ce8a59f`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nell'editor di un documento con una tabella: la linea fra le colonne si vede senza passarci sopra;
- nell'editor dei trasferimenti, l'anteprima di un arrivo con una STAR fra i punti (per esempio LIRN, ERIKA 1A):
  la frase dice «autorizzato alla STAR ERIKA 1A».

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
