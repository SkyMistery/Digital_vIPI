# Pacchetto 1.77.0 — solo i file cambiati

> **Timbro:** `1.77.0 · 5135ae6` (4 novembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.76.0** (`cae2b84`, online dal 28 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **9 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 5 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S134 — i documenti con sezioni vuote, in «Sezioni da scrivere».** In cima alla pagina admin
  (`/services/vsop/admin/required-sections`), sopra le tabelle dove si segna se una sezione è facoltativa, l'elenco
  dei documenti che hanno ancora sezioni da scrivere vuote, col loro numero. La riga si apre e dice quali; il nome del
  documento apre l'editor, «scrivi →» porta dritto alla sezione. Segnata o tolta una facoltativa, l'elenco si rilegge
  subito. Il conto è lo stesso della scheda del documento (1.76.0).

## I 9 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (5)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                       ← insieme ai file di wwwroot
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`,
`Vipi.Hosting`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta di
compilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: controllato per impronta,
confrontando con la 1.76.0 che gira, e col `git diff` (cambia solo `Vipi.Ui`).

**Per tornare indietro** bastano le rinomine: nessuna migrazione.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.77.0 · 5135ae6`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- la pagina **Sezioni da scrivere** (`/services/vsop/admin/required-sections`): in cima «Documenti con sezioni vuote»,
  una riga per documento;
- se la pagina appare senza stile, è il CSS vecchio in cache o non caricato con l'indice: da fuori l'indirizzo
  dev'essere `vipi-theme.css?v=bb96699c`.

## Dopo il carico

- Guardare sui dati veri «Sezioni da scrivere» e la pagina Documenti (1.76.0): tutte e due provate solo su dati
  inventati.
- Restano dalle versioni di prima: la scheda dell'API con una chiave vera (1.74), scrivere il testo comune delle torri
  radar e rileggerne la traduzione (1.72–1.73); le due frasi di Milano dei trasferimenti da riscrivere (1.71); la
  sezione «Radar di torre» nasce nascosta: si mostra scalo per scalo (1.68); gli elenchi di Torino–Genova ({WN0} e
  {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
