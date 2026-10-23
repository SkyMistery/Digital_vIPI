# Pacchetto 1.74.0 — solo i file cambiati

> **Timbro:** `1.74.0 · 4fdc3ac` (23 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.73.0** (`f7baf05`, online dal 22 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Nessun file di `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S129 — l'API aeroporti dice anche la torre radar e le regole di scelta della pista.** La scheda di uno scalo
  (`GET /vsop/api/v1/airports/{icao}`) porta due campi in più:
  - `radarTower`: `true` / `false`, oppure `null` se non è indicato;
  - `runwayRules`: le regole del documento, ognuna con le sue soglie (vento in coda e al traverso, superficie, orari,
    giorni, stagione) e le piste di decollo e atterraggio.
  I campi seguono la release pubblicata, come il resto della scheda. Nessun campo tolto: chi legge l'API oggi non si
  rompe. Nella pagina delle chiavi API cambia la descrizione della scheda.

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
1.73.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le rinomine: nessuna migrazione.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.74.0 · 4fdc3ac`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- da fuori il foglio di stile resta `vipi-theme.css?v=e393251a` (non cambia con questo pacchetto);
- con una chiave col permesso «Aeroporti»: la scheda di uno scalo pubblicato ha `radarTower` e `runwayRules`.

## Dopo il carico

- Chi usa l'API aeroporti può leggere i due campi nuovi: se serve, avvisarlo.
- Restano dalle versioni di prima: scrivere il testo comune delle torri radar e rileggerne la traduzione (1.72–1.73);
  le due frasi di Milano dei trasferimenti da riscrivere (1.71), la nota LVP come paragrafo, allegati e link (1.71, mai
  visti col login), il permesso «Settori» e la tecnica a blocchi (1.69), la sezione «Radar di torre» e la pastiglia
  nella vista live (1.68), le richieste aperte e le sezioni da scrivere (1.67), il convertitore sul limite di
  Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono, la
  prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova ({WN0} e {WS0}), Padova e Bologna,
  ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
