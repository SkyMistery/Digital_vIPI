# Pacchetto 1.47.2 — solo i file cambiati

> **Timbro:** `1.47.2 · 5bc663d` (29 settembre 2026). ⚠️ **Da questa versione il timbro non è più nella barra in
> alto**: sta nel **piè di pagina**, visibile a tutto lo staff. Resta anche nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.47.1** (`c41e6e7`, online dal 29 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da sola
> via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **12 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`**, 5 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **Il piè di pagina del sito**: chi siamo, l'avviso che niente qui vale per il volo reale, i collegamenti a IVAO
  (termini, privacy, regolamenti) e, per lo staff, la versione.
- **La scheda di uno scalo con vIPI e vSOP**: un clic fuori dalle due voci apre il documento della sua categoria —
  la vIPI se lo scalo è civile, la vSOP se è militare.

## I 12 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)**: `vipi-print.css`, `vipi-theme.css`, ognuno **con il suo `.br` e `.gz`**;
3. **in radice, subito dopo**: `Vipi.Host.staticwebassets.endpoints.json` — ⚠️ viaggia **insieme** a `wwwroot`;
4. **in radice (4)**, ogni `.pdb` col suo `.dll`:

```
Vipi.Ui.pdb     Vipi.Ui.dll
Vipi.Host.pdb   Vipi.Host.dll      ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** gli altri assiemi (compreso `Vipi.Application`: cambia solo un commento, quindi solo l'impronta
della ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.47.1.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati».

- in fondo a ogni pagina c'è il **piè di pagina**; in inglese dice «Part of the International Virtual Aviation
  Organisation» (prova che `en/` è arrivato);
- col login da staff: nel piè di pagina il timbro **`1.47.2 · 5bc663d`**; in Diagnostica `Schema` = `0`;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**;
- la scheda di uno scalo con vIPI e vSOP (per esempio uno scalo militare con presenza civile): il clic fuori dalle due
  voci apre il documento della categoria.

## Restano

- il login col VID 704798 al primo clic (la correzione è già in 1.47.1);
- Azure Translator 401 dal 27-set (chiave nei segreti);
- i gesti di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md) non ancora fatti.
