# Pacchetto 1.56.0 — solo i file cambiati

> **Timbro:** `1.56.0 · 2285a80` (1 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.55.1** (`5493151`, online dal 1 ottobre). È una **MINOR con QUATTRO migrazioni ADDITIVE**
> (`ApertureDocumenti` e `AccountEventoInUso`: due tabelle nuove; `VidAccountEvento` e `VidSvuotaEvento`: due colonne in
> `EventKits`; niente tolto o rinominato). Si consegna da sola via FTP, il
> database si aggiorna da sé all'avvio, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo, nessuna
> configurazione da toccare.
> **17 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S92 — lingua per documento nell'editor dell'unione**: nel pannello di pubblicazione di un documento unito c'è una
  riga «Lingua di pubblicazione» per ogni membro (LIRP: la vIPI bilingue, il vSOP solo in inglese). Un documento da
  solo non cambia.
- **S93 — aperture dei documenti**: il sito conta quante volte si apre ogni documento pubblico (una riga per documento
  e per giorno; niente bozze, anteprime, editor). Sulla pagina dell'ACC i tre di ogni scheda sono: prima gli «in
  evidenza» scelti a mano, poi i più aperti negli ultimi **90 giorni**. Il numero accanto alla voce lo vede **solo lo
  staff**.
- **S94** — su `/services` la scheda esterna **«Discord di divisione»**, accanto a Prenotazioni ATC; nella tabella
  delle SID, sugli schermi stretti, «Transition» diventa «Tran.».
- **S95 — controllare con un account dell'evento**: lo staff scrive in `/services/event` i VID degli account
  dell'evento; in `/services`, accanto ai profili, «Controlli con un account dell'evento?» → la vista live usa quel VID
  (in lista e fra gli online) fino a fine evento, anche dopo un riavvio del sito. La lista dei VID si cancella da sola
  7 giorni dopo la fine dell'evento (o alla data scritta dallo staff).

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
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta le migrazioni
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Hosting`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato, cambiati
solo per la ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a
1.55.1, controllato per impronta e col `git diff`. La costante nuova (`ApertureDocumenti.Finestra`, 90 giorni) è usata
solo da Application, Infrastructure e Ui, tutti dentro.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.56.0 · 2285a80`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- editor di LIRP, pannello di pubblicazione: una riga di lingua per la vIPI e una per il vSOP;
- aprite due o tre vIPI, poi la pagina dell'ACC: da staff accanto alle voci compare il numero di aperture;
- `/services/event` (staff): c'è il campo dei VID dell'evento; in `/services` la scheda «Discord di divisione»;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti su LIRE/LIBG, che aspettano il SOD.
