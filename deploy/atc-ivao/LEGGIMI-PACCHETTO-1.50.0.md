# Pacchetto 1.50.0 — solo i file cambiati

> **Timbro:** `1.50.0 · e7742ff` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.49.0** (`89bfb04`, online dal 30 settembre). È una **MINOR con UNA migrazione ADDITIVA**
> (`RichiesteDalCampo`: una tabella nuova `FieldRequests` e una colonna nuova `FromRequestId` sugli incarichi; niente
> tolto o rinominato). Si consegna da sola via FTP, il database si aggiorna da sé all'avvio, e **il rollback a due
> rinomine resta valido**. Nessun segreto nuovo, nessuna configurazione da toccare.
> **20 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **Richieste dal campo** (S56): chi è entrato con IVAO segnala un errore o manda un suggerimento su un documento.
  Accanto al titolo di ogni sezione principale, nella vista pubblica, c'è il link **«Segnala»** (non esce in stampa);
  porta alla pagina nuova **`/services/vsop/requests`**, col documento e la sezione già scelti. Lì l'utente legge
  anche le sue richieste e le risposte. Lo staff trova la coda nella stessa pagina e le richieste nuove in
  **«Da fare»**; prende in carico (nasce un incarico legato) e chiude con una risposta obbligatoria — chiudere la
  richiesta chiude anche l'incarico. Limiti: 5 aperte e 10 al giorno per persona, 2000 caratteri.
- **vAWOS** (S57): il quadro sta nell'altezza visibile anche in Edge (prima scorreva) e il tasto **TEST METAR** non c'è
  più sul quadro.

## I 20 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)** — i due file del vAWOS con le loro copie compresse:

```
vipi-awos.css   vipi-awos.css.br   vipi-awos.css.gz
vipi-awos.js    vipi-awos.js.br    vipi-awos.js.gz
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

⚠️ **Restano fuori** `Vipi.Hosting` (cambiato solo un commento), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`
(sorgente invariato), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.49.0
o cambiati solo per la ricompilazione, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.50.0 · e7742ff`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- una vIPI pubblicata (per esempio `…/libb/airports?icao=LIBC`): accanto ai titoli delle sezioni il link
  **«Segnala»**, che apre `/services/vsop/requests` col documento e la sezione già scelti; in inglese è **«Report»**
  e la pagina si chiama **«Field requests»** (prova che `en/` è arrivato);
- un vAWOS: il quadro sta nella finestra senza scorrere, e non ha più il tasto TEST METAR (prova che `wwwroot` e
  l'indice degli asset sono arrivati insieme);
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti di 1.48.0 non ancora fatti (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE
dopo il primo import) e quelli di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md).
