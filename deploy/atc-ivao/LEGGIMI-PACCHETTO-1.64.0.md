# Pacchetto 1.64.0 — solo i file cambiati

> **Timbro:** `1.64.0 · 1ad98d0` (16 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.63.0** (`099b0df`, online dal 16 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Niente `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

**S109 — «Tutti i settori» della mappa AOR non è una configurazione.**

Nasce dalla richiesta di rivedere le configurazioni possibili di **Milano APP** («con ANE ecc.»). Sulla copia di
produzione non mancava niente: quel blocco non ha mai avuto configurazioni. Quella che si vedeva era «Tutti i
settori», la scelta che la mappa AOR si dà da sola quando un gruppo non ha un elenco.

- In **Struttura → Configurazioni possibili**, sotto un gruppo **senza elenco**, una nota lo dice: finché lì non si
  scrive un elenco, la mappa AOR del documento offre una sola scelta, «Tutti i settori», che accende tutto il gruppo.
- **«Tutti i settori» si traduce**: in inglese la mappa dice «All sectors». Prima restava in italiano.

Il resto non cambia: per avere le configurazioni di Milano APP nella mappa si scrive l'elenco in Struttura.

## I 7 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (6)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll                ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application` e `Vipi.Ui` vanno **insieme**: condividono una costante nuova.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Hosting`
(sorgente invariato, e la costante nuova non la usano), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`,
`deps.json`, `runtimeconfig.json`, `appsettings.json` e tutto `wwwroot` con l'indice degli asset: controllato per
impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e niente viene scritto.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.64.0 · 1ad98d0`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- **Struttura → Configurazioni possibili → Milano**: sotto un gruppo senza elenco (per esempio Milano APP) la nota su
  «Tutti i settori»;
- la **vIPI di Milano in inglese** (EN in alto): nella mappa AOR la scelta si chiama «All sectors».

## Dopo il carico

- Se per Milano APP servono delle configurazioni vere, si scrivono in Struttura: la nota spiega da dove veniva quella
  che si vedeva.
- Restano dalle versioni di prima: il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live
  dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova (aggiungere {WN0} e
  {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
