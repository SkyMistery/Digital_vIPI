# Pacchetto 1.41.0 — solo i file cambiati

> **Timbro:** `1.41.0 · 235a15d` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.40.0** (`fb19094`, net10, online dal 21 settembre). **MINOR, NESSUNA migrazione**: niente database,
> niente segreti nuovi, nessuna configurazione da toccare. Si consegna via FTP.
> **Stesso runtime di 1.40.0 (.NET 10.0.12): si torna alla lista corta di file**, niente spostamento di cartelle.
> **13 file**: 9 in **radice**, 1 in **`en/`** e 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Se se ne carica uno senza l'altro, il sito chiede nomi che non esistono.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Documenti collegati** (§A109). Nel riquadro di sinistra di ogni documento pubblico, **sopra il Sommario**,
  c'è il blocco «Documenti collegati». Mostra solo documenti pubblici, sempre in quest'ordine: vIPI ACC →
  documenti APP → documenti d'aeroporto.
  - **vIPI ACC**: due gruppi **chiusi**, «APP» (gli APP non remotizzati con un documento) e «Aeroporti» (tutti
    gli scali sotto l'ACC; la vIPI se c'è, altrimenti il vSOP).
  - **vIPI di un APP non remotizzato**: la vIPI dell'ACC e, per ogni scalo sotto l'APP, vIPI e vSOP.
  - **vIPI o vSOP di uno scalo**: la vIPI dell'ACC, poi l'APP che lo controlla, poi l'altra edizione dello
    scalo. Se l'APP è **remotizzato**, il link si chiama per esempio `LIRR vIPI · LIRN_US0_APP` e apre la vIPI
    dell'ACC **sulla sezione di quell'APP**. Se l'APP subito sopra non ha un documento pubblico, si sale al
    successivo (LIBN → LIBN_G_APP senza documento → **LIBN_APP**).
  - **vLOA**: la vIPI dell'ACC italiano coinvolto.
  - In una pagina unita, un documento che sta già nella pagina diventa un salto alla sua sezione.
- **`blazor.web.js` con l'impronta** (§A108): il browser e Cloudflare lo tengono in cache per un anno, e quando
  il file cambia cambia anche l'indirizzo. Non si vede a schermo.

## I 13 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)**, da caricare e rinominare **per primi**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In `en/` (1)**:

```
Vipi.Ui.resources.dll      (una frase nuova: «Related documents»)
```

**In radice (9)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi `en/`, poi ogni `.pdb` col suo
   `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** gli altri assiemi, `Vipi.Infrastructure.MySqlMigrations.dll` (nessuna migrazione),
`deps.json`, `runtimeconfig.json`, `appsettings*.json` e il resto di `wwwroot`, compreso `_framework/`: sono
identici a 1.40.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato):

- il timbro **`1.41.0 · 235a15d`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- la vIPI di **Brindisi** (`services/vsop/libb/vipi`): in cima al riquadro di sinistra «DOCUMENTI COLLEGATI»
  con i gruppi chiusi «APP» (LIBN_APP, LIBV_APP) e «Aeroporti»; sotto una riga, «SOMMARIO»;
- il vSOP di **LIBN** (`services/vsop/libb/mil?icao=LIBN`): `LIBB vIPI` e `LIBN_APP`;
- uno scalo sotto un APP remotizzato, per esempio **LIBD** (`services/vsop/libb/airports?icao=LIBD`):
  `LIBB vIPI` e `LIBB vIPI · LIBD_CS0_APP`, che apre la vIPI di Brindisi sul gruppo di quell'APP.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

(da Git Bash: `MSYS_NO_PATHCONV=1` davanti, o l'indirizzo diventa un percorso di Windows.) E per §A108: la
pagina chiede `/_framework/blazor.web.js?v=<8 caratteri>`, e `curl -I` su quell'indirizzo risponde
`cache-control: public, max-age=31536000, immutable`.

---

## Dopo il carico

**Niente da ripubblicare perché i link compaiono**: per le release di prima i collegamenti si calcolano dalla
struttura di adesso. Quando un documento viene ripubblicato, la sua struttura di collegamenti si **congela**
nella release; chi dei candidati si vede lo decide sempre la pagina, quindi uno scalo pubblicato dopo compare
da solo. Serve ripubblicare solo se cambia la **struttura** (uno scalo spostato, un documento creato dopo).

⚠️ I collegamenti vedono solo documenti **pubblici**: finché la vIPI di LIRR, LIMM o LIPP non ha una release in
vigore, gli scali di quegli ACC non hanno il link all'ACC — è giusto così.

Restano valide le cose da fare di prima (ripubblicare le vIPI ACC e gli scali).
