# Pacchetto 1.52.0 — solo i file cambiati

> **Timbro:** `1.52.0 · 1a72e24` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.51.0** (`b57fe44`, online dal 30 settembre). È una **MINOR con UNA migrazione ADDITIVA**
> (`CorrezioniSpaziAerei`: una tabella nuova e un indice unico; niente tolto o rinominato). Si consegna da sola via
> FTP, il database si aggiorna da sé all'avvio, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo,
> nessuna configurazione da toccare.
> **17 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S63 — correzioni a mano degli spazi aerei**: in `/services/vsop/admin/airspace` si correggono **tipo, classe,
  base e tetto** dei volumi che il file dell'AIP sbaglia. Le correzioni stanno in una tabella a parte, citano il
  volume del file e si sovrappongono in lettura (catalogo e agganci dei settori): un nuovo caricamento non le
  cancella. Dopo un caricamento la pagina mostra **«Da controllare»** quando il file ha cambiato un campo corretto,
  dice già la stessa cosa o il volume non c'è più: «Va bene» o «Prendi il file» chiudono la voce.
- Durante i gesti lunghi (ricalcolo dei confinanti, 15–25 secondi) la pagina **dice che cosa sta facendo**.

## I 17 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (13)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** `Vipi.Hosting`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato,
cambiati solo per la ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json`, `vipi-awos.css` e il
resto di `wwwroot`: identici a 1.51.0, controllato per impronta e col `git diff`. L'unica interfaccia cambiata
(`IAirspaceCatalog`) è implementata solo in `Vipi.Infrastructure`, che è dentro.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.52.0 · 1a72e24`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- `https://atc.it.ivao.aero/services/vsop/admin/airspace` (Admin): su un volume si apre la correzione di tipo,
  classe, base e tetto; in inglese le etichette nuove sono tradotte (prova che `en/` è arrivato);
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti di 1.48.0 su LIRE/LIBG (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE),
che aspettano il SOD.
