# Pacchetto 1.44.0 — solo i file cambiati

> **Timbro:** `1.44.0 · 3561423` (23 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.43.1** (`a8a1cea`, online dal 23 settembre). È una **MINOR, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **14 file**: 8 in **radice** (uno in `en/`) e 6 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Se se ne carica uno senza l'altro, il sito chiede nomi che non esistono.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica.

---

## Che cos'è

- **Larghezza delle colonne delle tabelle.** Nell'editor, sotto il nome di ogni colonna di una tabella c'è un
  campo «%»: vuoto = automatica, un numero = quella percentuale. Si può anche **trascinare il bordo**
  dell'intestazione col mouse; doppio clic sul bordo = torna automatica. Le tabelle già scritte restano come sono.
  Se le larghezze non si possono rispettare alla lettera (somma diversa da 100), un avviso sotto la tabella lo dice.
- **Procedure non trovate negli accordi.** Nell'editor dei trasferimenti c'è un tasto nuovo sulla barra,
  «⚠ Procedure non trovate (n)»: elenca le SID/STAR scritte fra i punti che negli scali della sezione non esistono
  più (accordo, sezione, clausola, nome scritto) e porta alla clausola. Una procedura che sparisce col ciclo che
  viene si segnala solo negli ultimi 3 giorni prima del cambio; una nuova del ciclo entrante non si segnala mai.
  L'avviso ricorda che le copie pubbliche di vIPI ACC/APP e vLOA vanno **ripubblicate** per mostrare il nome nuovo.

## I 14 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (6)** — si caricano e rinominano **per primi**:

```
vipi-editor.js   vipi-editor.js.br   vipi-editor.js.gz
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice (8)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
en/Vipi.Ui.resources.dll                   ← nella cartella en/ (frasi inglesi nuove)
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi `en/`, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Domain`, `Vipi.Hosting` e
gli altri assiemi: il loro codice non è cambiato e differiscono solo perché ricompilati. Restano fuori anche
`deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.43.1, controllato per
impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (script e stile dell'editor sono cambiati):

- il timbro **`1.44.0 · 3561423`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nell'editor di un documento con una tabella: sotto il nome di una colonna c'è il campo «%»; trascinando il bordo
  dell'intestazione la colonna si allarga e il numero segue;
- nell'editor dei trasferimenti di un ACC: sulla barra c'è «⚠ Procedure non trovate (n)».

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
