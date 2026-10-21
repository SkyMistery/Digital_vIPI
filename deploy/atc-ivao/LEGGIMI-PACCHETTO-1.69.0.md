# Pacchetto 1.69.0 — solo i file cambiati

> **Timbro:** `1.69.0 · 9af1c14` (21 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.68.0** (`811010f`, online dal 20 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Al primo avvio il sito fa da solo un **trasloco di dati** (la tecnica operativa del radar di torre passa
> dall'anagrafica ai blocchi dei documenti), senza cambiare lo schema. Nessun segreto nuovo, nessuna configurazione da
> toccare. **13 file**: 1 in **`en/`** e 12 in **radice**. Nessun file di `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S119 — API dei settori.** Una porta nuova per gli altri programmi: `GET /vsop/api/v1/accs` (i centri),
  `…/accs/{acc}/sectors` (i settori della Struttura: tipo, scalo, frequenza, padre, fascia verticale, ripiego) e
  `…/accs/{acc}/coordinations` (i coordinamenti; con `?online=` dice chi li ha davvero, dati gli enti aperti).
  Serve una chiave col permesso nuovo **«Settori»**, da emettere nella pagina delle chiavi API. Senza chiave: 401.
- **S118 — la «Tecnica operativa» del Radar di torre è fatta di blocchi** (paragrafi, callout, tabelle…) come le altre
  sezioni scritte, e si traduce. Il testo scritto finora nell'anagrafica diventa, al primo avvio, un paragrafo nei
  documenti dello scalo; la colonna dell'anagrafica si svuota (resta nello schema, si toglierà in una consegna dopo).
  Le release già pubblicate continuano a mostrare il testo di prima fino alla pubblicazione seguente.

## I 13 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (12)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Hosting.pdb                        Vipi.Hosting.dll        ← l'API nuova
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll           ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I sei assiemi vanno **insieme**: il permesso nuovo sta in `Vipi.Domain`, la porta dell'API in
`Vipi.Application` e `Vipi.Infrastructure`, le rotte in `Vipi.Hosting`, il trasloco in `Vipi.Infrastructure`.

⚠️ **Restano fuori** `Vipi.Infrastructure.MySqlMigrations` (nessuna migrazione: cambia solo il lock dei pacchetti),
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta di compilazione),
`Vipi.Host.deps.json` (perde solo la riga di un riferimento diretto a SQLite tolto il 20 ottobre; la libreria è la
stessa), `runtimeconfig.json`, `appsettings.json` e tutto `wwwroot`: controllato per impronta, confrontando con la
1.68.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le rinomine, ma ⚠️ **dopo il primo avvio della 1.69.0 la tecnica operativa è già nei
blocchi**: la 1.68.0 la cerca nella colonna dell'anagrafica, che è vuota, e quindi non la mostra (il testo non è perso,
è nei documenti, e torna visibile rimettendo la 1.69.0). Nient'altro cambia.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.69.0 · 9af1c14`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- la pagina **Chiavi API** (Admin) mostra il permesso **«Settori»** fra quelli spuntabili;
- da fuori, senza chiave, `https://atc.it.ivao.aero/vsop/api/v1/accs` risponde **401**;
- in uno scalo dove la tecnica era scritta, la sezione «Radar di torre» → «Tecnica operativa» ha un **paragrafo** con
  quel testo.

## Dopo il carico

- Per usare l'API: emettere una chiave col solo permesso «Settori» per chi la integra.
- Restano dalle versioni di prima: la sezione «Radar di torre» e la pastiglia nella vista live (1.68, mai viste col
  login), le richieste aperte e le sezioni da scrivere (1.67), il convertitore sul limite di Torino–Genova, il banco su
  Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono, la prova del validatore dei
  tour sull'archivio ATC, gli elenchi di Torino–Genova ({WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC.
  LIRE/LIBG aspettano il SOD.
