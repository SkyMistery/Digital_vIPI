# Pacchetto 1.51.0 — solo i file cambiati

> **Timbro:** `1.51.0 · b57fe44` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.50.0** (`e7742ff`, online dal 30 settembre). ⚠️ **La 1.50.1 non si carica**: è confluita qui dentro.
> È una **MINOR con UNA migrazione ADDITIVA** (`ProfiliEvento`: due tabelle nuove e un indice; niente tolto o
> rinominato). Si consegna da sola via FTP, il database si aggiorna da sé all'avvio, e **il rollback a due rinomine
> resta valido**. Nessun segreto nuovo, nessuna configurazione da toccare.
> **22 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`** e 15 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S58** — il vAWOS è **ancorato alla finestra** (in Edge con 1.50.0 scorreva ancora); al posto della parola
  «Segnala» una **bandierina ⚑** con suggerimento su ogni titolo di sezione (sotto-sezioni e vIPI ACC comprese); nel
  modulo delle richieste uno **switch Errore/Suggerimento**.
- **S59 — profili per l'evento**: pagina pubblica **`/services/event`** che lo staff accende (interruttore, date
  facoltative): file per postazione fino a 3 MB e link a Google Drive. Quando si vede, nell'hub compare la sezione
  **«Evento in corso»**; la scheda «Pacchetto dell'evento» per lo staff sta sempre nella sezione staff.
- **S60** — nel piè di pagina il link a **IVAO Italia** (`https://it.ivao.aero/`).
- **S61** — pagina **`/services/cookies`** con i soli cookie tecnici (niente banner) e il link «Cookie» nel piè di
  pagina.
- **S62** — le richieste dal campo si possono **eliminare** (solo Admin); le **chiuse** si cancellano da sole **tre
  mesi** dopo la chiusura, nel giro notturno. Le aperte restano.

## I 22 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)**:

```
vipi-awos.css    vipi-awos.css.br    vipi-awos.css.gz
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (15)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato), `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.50.0 o cambiati solo per la
ricompilazione, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.51.0 · b57fe44`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- `https://atc.it.ivao.aero/services/event` si apre (spenta dice che non c'è un evento in corso); da staff, nell'hub,
  la scheda «Pacchetto dell'evento» / **«Event package»** (prova che `en/` è arrivato);
- nel piè di pagina i link **IVAO Italia** e **Cookie**; `https://atc.it.ivao.aero/services/cookies` si apre;
- una vIPI pubblicata: la **bandierina ⚑** accanto ai titoli, anche delle sotto-sezioni;
- un vAWOS in Edge: il quadro sta nella finestra e **non scorre**;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

Cancellare le richieste di prova: da Admin, in `/services/vsop/requests`, «Elimina». I gesti di 1.48.0 non ancora
fatti (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE dopo il primo import) e quelli
di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md).
