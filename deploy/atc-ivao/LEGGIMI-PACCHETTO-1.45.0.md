# Pacchetto 1.45.0 — solo i file cambiati

> **Timbro:** `1.45.0 · 7bd3bda` (24 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.44.1** (`ce8a59f`, online dal 23 settembre). È una **MINOR con UNA migrazione del database**,
> **additiva**: aggiunge due colonne vuote (`CauseKey`, `CauseArgsJson`) alla tabella `DocumentImpacts`. Non tocca
> nessun dato. Il pacchetto si consegna da solo via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **17 file**: 14 in **radice** (uno in `en/`) e 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **Copia di sicurezza del database prima del carico** (Diagnostica → copia del database). La migrazione non
> cancella niente, ma è la regola per ogni consegna con migrazione.
>
> 🔴 **`Vipi.Infrastructure.MySqlMigrations.dll` è DENTRO e non va dimenticato.** Senza, le due colonne non nascono e
> la «causa» delle righe da ripubblicare non si salva: il sito parte lo stesso, ma `Schema` in Diagnostica non è 0.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è — la lista «Da fare» (pagina «Da sistemare»)

- **Si legge per cambiamento.** Prima c'era una riga per ogni documento toccato; ora le righe che nascono dalla
  stessa modifica (stesso tipo, stesse sezioni, stesso giorno) stanno insieme, con un «✓ segna tutte rilette (N)».
  Restano le viste **per documento** ed **elenco**; la scelta si ricorda.
- **La riga dice il perché e da quanto.** Le righe «da ripubblicare» portano la **causa** (per esempio «modificati i
  trasferimenti») e l'età. La deriva si ricalcola **pochi minuti dopo** un salvataggio, non solo al giro periodico.
- **Alla pubblicazione**: nel pannello di pubblicazione di un documento, una casella «segna rilette anche queste N»
  chiude insieme le righe dello stesso documento; nella riga c'è «cosa è cambiato».
- **Incarichi**: un incarico preso in carico si chiude da solo quando la sua segnalazione si chiude.

## I 17 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice (14)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
en/Vipi.Ui.resources.dll                   ← nella cartella en/ (frasi inglesi nuove)
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← la migrazione
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi `en/`, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Hosting` e gli altri assiemi: il loro codice non è cambiato e differiscono solo perché
ricompilati. Restano fuori anche `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`:
identici a 1.44.1, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato):

- il timbro **`1.45.0 · 7bd3bda`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nel log del giorno (`diagnostica/log-*.txt`) oppure in `avvio-diagnostica.txt` («migrazione del database»):
  la migrazione `…_CausaDelleSegnalazioni` passata all'avvio;
- `services/vsop/admin/pending` («Da sistemare»): in alto i tre tasti **Per cambiamento · Per documento · Elenco**;
  cambiando vista la pagina non si ricarica e non esce nessun errore.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
