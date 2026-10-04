# Pacchetto 1.47.3 — solo i file cambiati

> **Timbro:** `1.47.3 · de51b76` (29 settembre 2026), nel **piè di pagina** (visibile allo staff), nella riga
> `Versione` della **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.47.2** (`5bc663d`, online dal 29 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da sola
> via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **9 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`**, 5 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

Il piè di pagina della 1.47.2, sistemato:

- i link legali (termini, privacy, regolamenti) portano alla **wiki di IVAO**: le pagine su ivao.aero rispondevano 404;
- lo **stesso colore della barra in alto**;
- la riga **«Realizzato da Carmine (704798)»**, col link al profilo IVAO.

## I 9 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**: `vipi-theme.css` **con il suo `.br` e `.gz`**;
3. **in radice, subito dopo**: `Vipi.Host.staticwebassets.endpoints.json` — ⚠️ viaggia **insieme** a `wwwroot`;
4. **in radice (4)**, ogni `.pdb` col suo `.dll`:

```
Vipi.Ui.pdb     Vipi.Ui.dll
Vipi.Host.pdb   Vipi.Host.dll      ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** tutti gli altri assiemi (sorgente invariato), `deps.json`, `runtimeconfig.json`,
`appsettings.json` e il resto di `wwwroot`: identici a 1.47.2.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti**, non solo la riga «N risultati».

- il piè di pagina ha il **colore della barra in alto** e la riga «Realizzato da Carmine (704798)»;
- un link legale del piè di pagina apre la **wiki di IVAO** (non più un 404);
- in inglese il piè di pagina dice «Part of the International Virtual Aviation Organisation» (prova che `en/` è
  arrivato);
- col login da staff: il timbro **`1.47.3 · de51b76`** nel piè di pagina; in Diagnostica `Schema` = `0`;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.
