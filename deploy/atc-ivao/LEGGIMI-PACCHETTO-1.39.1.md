# Pacchetto 1.39.1 — solo i file cambiati

> **Timbro:** `1.39.1 · 4df91c0` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.39.0** (`1b25c4f`, online dal 21 settembre). **PATCH, NESSUNA migrazione**: niente database, niente
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

- **Sommario: ogni documento si chiude per intero.** Nelle pagine con più documenti uniti (per esempio vIPI e
  vSOP MIL di uno scalo) e nelle vIPI di ACC (settori di aerovia, gruppi APP remotizzati), il titolo di ogni
  gruppo nel sommario ha la freccetta ▸: un clic chiude o riapre tutte le voci di quel documento o blocco. I
  gruppi partono aperti. Un documento da solo ha il sommario di prima. Vale anche negli editor.

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

⚠️ **Restano fuori** `en/Vipi.Ui.resources.dll` (nessuna frase cambiata: è diverso solo perché ricompilato), gli
altri assiemi, `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: sono identici a
1.39.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato):

- il timbro **`1.39.1 · 4df91c0`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- la vIPI di **Brindisi** (`services/vsop/libb/vipi`): nel sommario, un clic su «SETTORI DI AEROVIA» chiude
  tutte le sue voci, e un secondo clic le riapre. La pagina non si sposta;
- uno scalo con documenti uniti (es. LIBV, `services/vsop/libb/airports?icao=LIBV`): la stessa cosa sul titolo
  di ciascun documento.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
node .claude/skills/verifica-live/gruppi-toc-verifica.js /services/vsop/libb/vipi
```

(da Git Bash: `MSYS_NO_PATHCONV=1` davanti, o l'indirizzo diventa un percorso di Windows.)

---

## Dopo il carico

Niente da ripubblicare: il sommario si disegna a ogni richiesta, e non sta nella copia pubblica.
Restano valide le cose da fare di 1.39.0 (ripubblicare le vIPI ACC e gli scali ripubblicati dopo 1.35.0).

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Questo pacchetto è compilato per **net8**, come tutti i precedenti. Il salto a net10 è **1.40.0**, e viaggia
**da solo**: se dopo il carico qualcosa si rompe, deve avere una causa sola.
