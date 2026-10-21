# Pacchetto 1.70.0 — solo i file cambiati

> **Timbro:** `1.70.0 · 2a21316` (21 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.69.0** (`9af1c14`, online dal 21 ottobre). È una **MINOR con una migrazione DISTRUTTIVA**
> (`TogliTecnicaRadar`: toglie la colonna `RadarTowerTechnique` dalla tabella `Airports`). La applica il sito da solo
> al primo avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **12 file**, tutti in **radice**. Niente in `en/` né in `wwwroot`.

> 🔴 **NON SI TORNA INDIETRO con le rinomine.** Dopo il primo avvio la colonna non c'è più, e la 1.69.0 la cerca: un
> ritorno alla 1.69.0 darebbe errori su ogni scalo. Per tornare indietro serve la copia del database fatta prima.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Prima di caricare: due cose

1. **La colonna dev'essere vuota.** Sul database di produzione:

   ```sql
   SELECT Icao FROM Airports WHERE RadarTowerTechnique IS NOT NULL;
   ```

   deve dare **nessuna riga**. La 1.69.0 l'ha svuotata al primo avvio portando i testi nei documenti. Una riga
   rimasta è uno scalo il cui testo non ha trovato la sua sezione: va copiato a mano nella «Tecnica operativa» del
   documento **prima** del carico, perché la migrazione lo cancella.
2. **Una copia fresca del database** (Diagnostica → copia del database, o chi amministra il database). È l'unica
   strada per tornare indietro.

## Che cos'è

- **S120 — via la colonna della tecnica operativa.** Dalla 1.69.0 la «Tecnica operativa» del Radar di torre è fatta
  di blocchi nel documento; la colonna dell'anagrafica era vuota e non la leggeva più nessuno. Via anche il trasloco
  d'avvio della 1.69.0, che ha già fatto il suo lavoro. Per chi usa il sito non cambia niente.

## I 12 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto. In radice, in quest'ordine, ogni
`.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← la migrazione: NON dimenticarlo
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Host.pdb                           Vipi.Host.dll           ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I sei assiemi vanno **insieme**: il modello senza la colonna sta in `Vipi.Domain` e
`Vipi.Infrastructure`, la migrazione in `Vipi.Infrastructure.MySqlMigrations`.

🔴 **Senza `Vipi.Infrastructure.MySqlMigrations.dll`** la colonna resta nel database e il modello non la conosce:
il sito funziona ma la Diagnostica dice `Schema` diverso da 0. Il controllo è qui sotto.

⚠️ **Restano fuori** `Vipi.Ui` ed `en/Vipi.Ui.resources.dll` (sorgente e frasi invariati: cambia solo l'impronta di
compilazione), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`,
`appsettings.json` e tutto `wwwroot`: controllato per impronta, confrontando con la 1.69.0 che gira, e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.70.0 · 2a21316`** nel piè di pagina (staff);
- 🔴 in Diagnostica **`Schema` = `0`**: dice che la migrazione è passata e la colonna non c'è più;
- l'**editor di uno scalo** con la tecnica scritta si apre, e la «Tecnica operativa» mostra ancora il suo paragrafo.

## Dopo il carico

- Restano dalle versioni di prima: il permesso «Settori» e la tecnica a blocchi (1.69, mai visti col login), la
  sezione «Radar di torre» e la pastiglia nella vista live (1.68), le richieste aperte e le sezioni da scrivere
  (1.67), il convertitore sul limite di Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in
  Sorgenti, la vista live dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di
  Torino–Genova ({WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
