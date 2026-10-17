# Pacchetto 1.60.0 — solo i file cambiati

> **Timbro:** `1.60.0 · 3ea92b2` (10 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.59.0** (`fc442f2`, online dal 10 ottobre). È una **MINOR con UNA migrazione additiva**, e si consegna
> da sola via FTP: il database si aggiorna da sé all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **10 file**, tutti in **radice**. Niente `en/`, niente `wwwroot`.

> ⚠️ **La migrazione** (`CentroDelleSessioniAtc`) aggiunge due colonne vuote alla tabella `AtcSessions`: `Latitude` e
> `Longitude`. Non toglie e non rinomina niente. È stata provata su SQLite (qui, sul pacchetto) e il suo schema su
> MariaDB è verificato dalla CI; **non** è stata provata su una copia del database di produzione. `AtcSessions` è la
> tabella dell'archivio, che cresce ogni giorno: se l'avvio dopo il carico dura più del solito, è lei.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

**S102 — l'archivio ATC dice dove sta la postazione.** Chiesto dal validatore dei tour.

`GET /vsop/api/v1/atc/sessions` dà, in coda a ogni riga, due campi nuovi:

```
"latitude": 52.30806, "longitude": 4.76417
```

- sono gradi decimali, e sono il **centro** della postazione come lo dà IVAO: un **punto**, non l'area (per un CTR
  può stare lontano dal bordo);
- valgono **`null`** quando non si sa: sulle righe scritte **prima** di questo carico, su quelle dello storico e su
  qualche connessione che IVAO dà senza posizione. **Non si riempiono all'indietro**;
- arrivano sempre in coppia: o tutte e due, o nessuna;
- gli altri campi e i filtri non cambiano: chi legge l'archivio oggi continua a funzionare.

Nessuna pagina del sito cambia.

## I 10 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto. Tutti in radice, in quest'ordine,
ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Hosting` e `Vipi.Ui` (sorgente invariato: l'indirizzo dell'archivio consegna le righe così
come arrivano, e la pagina staff le legge soltanto), `en/Vipi.Ui.resources.dll` (nessuna frase cambiata), tutto
`wwwroot` con `Vipi.Host.staticwebassets.endpoints.json`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`,
`deps.json`, `runtimeconfig.json`, `appsettings.json`: controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: la migrazione aggiunge soltanto, e la 1.59.0 gira sullo schema
nuovo ignorando le due colonne.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.60.0 · 3ea92b2`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`, oppure la migrazione si è fermata: scaricate `diagnostica/` e non
  riavviate);
- **l'archivio**, con una chiave API, qualche minuto dopo l'avvio (il tempo di un giro del lettore IVAO):
  `https://atc.it.ivao.aero/vsop/api/v1/atc/sessions?open=true` — le sessioni aperte devono avere `latitude` e
  `longitude` con dei numeri. Se i due campi **non ci sono affatto**, non è partito il pacchetto nuovo; se ci sono ma
  sono tutti `null` anche sulle sessioni aperte dopo il carico, è un guasto da segnalare.

## Dopo il carico

- **Dire al validatore dei tour** che può provare: i campi si chiamano `latitude` e `longitude`; mancano sulle righe
  vecchie, quindi il suo ripiego (ICAO dal callsign) gli serve ancora per quelle.
- Restano i gesti della 1.58.0, se non ancora fatti: accendere «L'elenco è completo» sui settori d'area di **LIMM**
  (finché no, `/vsop/health` dice `Degraded`), Torino–Genova, Padova e Bologna, e ripubblicare le vIPI ACC.
- Restano da confermare le scelte del Sito su S100/S101 (1.59.0), e i gesti su LIRE/LIBG che aspettano il SOD.
