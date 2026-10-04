# Pacchetto 1.50.1 — solo i file cambiati

> ⛔ **MAI CARICATO**: il committente ha scelto di far entrare S58 nella **1.51.0** insieme a S59–S62. Questo foglio
> resta come fotografia; si carica [`LEGGIMI-PACCHETTO-1.51.0.md`](LEGGIMI-PACCHETTO-1.51.0.md).

> **Timbro:** `1.50.1 · e666236` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.50.0** (`e7742ff`, online dal 30 settembre). È una **PATCH senza migrazioni**: si consegna da sola via
> FTP, il rollback a due rinomine resta valido. Nessun segreto nuovo, nessuna configurazione da toccare.
> **12 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`** e 5 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è (Sito S58)

- **vAWOS ancorato alla finestra**: con 1.50.0 in Edge il quadro scorreva ancora; ora è fissato ai bordi della finestra.
- **Bandierina ⚑ al posto della parola «Segnala»**, con un suggerimento al passaggio del mouse, su **ogni** titolo di
  sezione: anche le sotto-sezioni e la vIPI ACC.
- Nel modulo delle richieste, uno **switch Errore/Suggerimento**.

## I 12 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)**:

```
vipi-awos.css    vipi-awos.css.br    vipi-awos.css.gz
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (5)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json          ← insieme ai file di wwwroot
Vipi.Ui.pdb                 Vipi.Ui.dll
Vipi.Host.pdb               Vipi.Host.dll        ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** tutti gli altri assiemi (sorgente invariato: cambiano solo per la ricompilazione), `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.50.1 · e666236`** nel piè di pagina; in Diagnostica `Schema` = `0`;
- una vIPI pubblicata (per esempio `…/libb/airports?icao=LIBC`): accanto ai titoli, anche delle sotto-sezioni, la
  **bandierina ⚑** e non più la parola; il suggerimento in inglese dice «Report an error or suggest a change in this
  section» (prova che `en/` è arrivato);
- il modulo delle richieste ha lo switch **Errore/Suggerimento**;
- un vAWOS in Edge: il quadro sta nella finestra e **non scorre**;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti di 1.48.0 non ancora fatti (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE
dopo il primo import) e quelli di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md).
