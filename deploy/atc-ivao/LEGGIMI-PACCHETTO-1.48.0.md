# Pacchetto 1.48.0 — solo i file cambiati

> **Timbro:** `1.48.0 · e292a1e` (29 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.47.3** (`de51b76`, online dal 29 settembre). È una **MINOR con UNA migrazione ADDITIVA**
> (`EntiAtc`: due tabelle e cinque indici nuovi, niente tolto né rinominato). Si consegna da sola via FTP, il database
> si aggiorna da sé all'avvio, e **il rollback a due rinomine resta valido** (non è un pacchetto «NON SI TORNA
> INDIETRO»). Nessun segreto nuovo, nessuna configurazione da toccare.
> **19 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`**, 15 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **Enti ATC** (S49–S52): la vIPI di un APP appartiene a un **ente**, non più a un nominativo IVAO. L'editor APP ha
  la scheda «Ente» con le posizioni (principale, aggiungi, togli); «Remotizza» sposta la vIPI APP di un ente dentro
  la vIPI dell'ACC; la vIPI APP deriva frequenze, AoR e configurazioni da **tutte** le posizioni dell'ente.
- **La vIPI e il vSOP sono dello scalo** (S48): le posizioni che IVAO non manda più escono da sole. Al primo import
  escono **LIBG_TWR** e **LIRE_TWR** (su IVAO non esistono più: a LIBG l'APP fa anche da torre). Il badge «no TWR»
  della pagina Aeroporti ora lo spiega.
- **vAWOS in uno schermo** (S47): la stessa pagina con una pista come con tre.

## I 19 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**: `vipi-awos.css` **con il suo `.br` e `.gz`**;
3. **in radice, subito dopo**: `Vipi.Host.staticwebassets.endpoints.json` — ⚠️ viaggia **insieme** a `wwwroot`;
4. **in radice (14)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. Il primo avvio applica la migrazione e sgancia i settori
che non servono più (sulla copia: 70 settori su 46 scali): un po' più lento del solito, non si riavvia di nuovo nel
frattempo.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato), `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.47.3.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti**, non solo la riga «N risultati».

- col login da staff: il timbro **`1.48.0 · e292a1e`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (prova che
  la migrazione è entrata: se non è 0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- in inglese l'editor di una vIPI APP ha la scheda **«Unit»** («The APP vIPI belongs to the unit, not to an IVAO
  callsign…») — prova che `en/` è arrivato;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**;
- il vAWOS di uno scalo con una pista e di uno con più piste si apre nello stesso schermo.

## Dopo il caricamento: i gesti a mano (del committente)

1. **LIRE (Pratica)**: editor vIPI APP `LIRE_APP` → Modifica → **Ente**: aggiungi `LIRE_TWR`, rendila principale,
   togli `LIRE_APP`.
2. **LIBG e LIRE**: dopo il primo import notturno escono `LIBG_TWR` e `LIRE_TWR`; ripubblicare le vIPI/vSOP di LIBG e
   LIRE (la sezione Frequenze è congelata: la deriva le segnala).
3. ⚠️ **Non spuntare «remotizzato» su un APP con vIPI a mano**: da questa versione si usa «Remotizza» dall'editor
   APP, che sposta la vIPI nella vIPI dell'ACC.
4. Restano quelli di prima: login col VID 704798 al primo clic, Azure Translator 401 (segreti), i gesti di
   [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md).
