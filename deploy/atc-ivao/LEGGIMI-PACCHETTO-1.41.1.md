# Pacchetto 1.41.1 — solo i file cambiati

> **Timbro:** `1.41.1 · bd668a7` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.41.0** (`235a15d`, online dal 21 settembre). **PATCH, NESSUNA migrazione**: niente database, niente
> segreti nuovi, nessuna configurazione da toccare. Si consegna via FTP.
> **8 file**: 5 in **radice** e 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Se se ne carica uno senza l'altro, il sito chiede nomi che non esistono.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **I documenti collegati si spostano nella colonna di DESTRA**, in un riquadro «Documenti collegati» subito
  sotto «Link». Il riquadro di sinistra torna a essere solo il «Sommario», com'era prima di 1.41.0. I link sono
  gli stessi di 1.41.0. Nella vIPI ACC ci sono i gruppi «APP» e «Aeroporti», chiusi e con il numero di voci.
- ⚠️ La colonna di destra compare solo quando la finestra è larga almeno **1500 px**. Sotto quella larghezza il
  riquadro non si vede, come «Riepilogo» e «Link».

## Gli 8 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)**, da caricare e rinominare **per primi**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice (5)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `en/Vipi.Ui.resources.dll` (nessuna frase cambiata), gli altri assiemi, `deps.json`,
`runtimeconfig.json`, `appsettings*.json` e il resto di `wwwroot`: sono identici a 1.41.0, controllato per
impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato), a finestra larga:

- il timbro **`1.41.1 · bd668a7`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- la vIPI di **Brindisi** (`services/vsop/libb/vipi`): a destra, sotto «Link», il riquadro «Documenti
  collegati» con «APP (2)» e «Aeroporti (…)» chiusi; a sinistra il solo «Sommario»;
- il vSOP di **LIBN** (`services/vsop/libb/mil?icao=LIBN`): nel riquadro, `LIBB vIPI` e `LIBN_APP`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico

Niente da ripubblicare.
