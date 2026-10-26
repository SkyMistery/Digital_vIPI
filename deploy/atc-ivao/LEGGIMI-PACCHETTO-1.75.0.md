# Pacchetto 1.75.0 — solo i file cambiati

> **Timbro:** `1.75.0 · 1aa329c` (26 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.74.0** (`4fdc3ac`, online dal 23 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Nessun file di `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S132 — l'API aeroporti dice la pista in uso adesso.** La scheda di uno scalo
  (`GET /vsop/api/v1/airports/{icao}`) porta un campo in più, `runwayInUse`:
  - `departure` e `arrival`: le piste in uso (elenchi: una regola può darne più d'una);
  - `source`: `rule` (l'ha decisa una regola dello scalo, e `rule` dice quale), `wind` (nessuna regola vale: la pista
    col vento più in prua) oppure `none` (non si sa: niente METAR o vento non noto, elenchi vuoti);
  - `metar` e `metarObservedUtc`: il bollettino usato e la sua ora.
  È **lo stesso calcolo della pagina del documento**, non il quadro vAWOS (che mette prima l'ATIS di chi è online).
  Nessun campo tolto: chi legge l'API oggi non si rompe. Nella pagina delle chiavi API cambia la descrizione della
  scheda.

## I 7 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (6)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Hosting.pdb           Vipi.Hosting.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`,
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta di compilazione),
`deps.json`, `runtimeconfig.json`, `appsettings.json` e tutto `wwwroot`: controllato per impronta, confrontando con la
1.74.0 che gira, e col `git diff`. Il calcolo della pista in uso sta in `Vipi.Application`, che è già online invariato.

**Per tornare indietro** bastano le rinomine: nessuna migrazione.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.75.0 · 1aa329c`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- da fuori il foglio di stile resta `vipi-theme.css?v=e393251a` (non cambia con questo pacchetto);
- con una chiave col permesso «Aeroporti»: la scheda di uno scalo pubblicato ha `runwayInUse`, con le stesse piste che
  la pagina del documento marca 🛫/🛬.

## Dopo il carico

- Chi usa l'API aeroporti può leggere il campo nuovo: se serve, avvisarlo.
- Restano dalle versioni di prima: la scheda dell'API con una chiave vera (1.74), scrivere il testo comune delle torri
  radar e rileggerne la traduzione (1.72–1.73); le due frasi di Milano dei trasferimenti da riscrivere (1.71), la nota
  LVP come paragrafo, allegati e link (1.71, mai visti col login), il permesso «Settori» e la tecnica a blocchi (1.69),
  la sezione «Radar di torre» e la pastiglia nella vista live (1.68), le richieste aperte e le sezioni da scrivere
  (1.67), il convertitore sul limite di Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in
  Sorgenti, la vista live dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di
  Torino–Genova ({WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
