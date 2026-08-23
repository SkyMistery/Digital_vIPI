# vIPI — correzione del 23 agosto 2026 (pacchetto «b»)

**Leggete questo foglio, non `LEGGIMI-AGGIORNAMENTO.md`.** Quello descrive la consegna del 23 agosto,
che comprendeva la sostituzione del database. Questa è la correzione che le va sopra, ed è **soli file**.

> ## ⛔ Il database NON si tocca
>
> Niente `.sql`, niente `DROP DATABASE`, niente import. Lo schema non è cambiato: la correzione è tutta
> nel codice. Se il `.sql` del 23 agosto è già stato importato, sta bene com'è.
>
> ⚠️ Se qualcuno riesegue per abitudine i passi di `LEGGIMI-AGGIORNAMENTO.md`, **cancella il database**.
> Quel foglio resta nel pacchetto solo come storia della consegna precedente.

---

## Che cosa corregge

Sulla pagina pubblica di un aeroporto poteva comparire il documento dell'**Avvicinamento** al posto di
quello dell'aeroporto: stesso ICAO, documento sbagliato. L'editor mostrava quello giusto, il pubblico no.

La causa: un APP non remotizzato (per esempio `LIBA_APP`) è registrato come settore *d'aeroporto* e porta
l'ICAO, pur avendo un documento tutto suo. Cinque punti del programma cercavano «il documento di LIBA»
senza distinguere i due, e quando esistevano entrambi vinceva quello che il database restituiva per primo.
Il punto che faceva più danno era la **pubblicazione**: la release dell'aeroporto fotografava il documento
dell'APP.

Sono i **30 aeroporti che hanno un APP non remotizzato** (LIRP, LIEE, LIPS, LIPE, LICC, LIBP, LIBA, LIPA,
LIRM e altri): il difetto scattava appena si pubblicava il documento d'aeroporto di uno di quelli.

---

## La procedura, in tre passi

Restano valide le due impostazioni di FileZilla di [`LEGGIMI-FTP.md`](LEGGIMI-FTP.md): **trasferimento
binario** e **bit di esecuzione** su `Vipi.Host` e `createdump`.

### 1. Le tre cose da NON cancellare

Come sempre, si sovrascrive **senza svuotare** la cartella dell'applicazione:

| Cosa | Dove |
|---|---|
| `appsettings.Production.json` | radice dell'applicazione — contiene la password del database |
| `vipi-keys/` | `…/public_atc/vipi-keys` — le chiavi che firmano le sessioni |
| `tmp/` | `…/public_atc/tmp` — serve per il riavvio |

⚠️ La cartella `deploy/` del pacchetto **non va caricata**: sono tre file di riferimento che su
Plesk+Passenger non servono.

### 2. Caricare

Rispetto al pacchetto già caricato il 23 agosto cambiano **19 file su 421**, tutte nostre librerie:

```
Vipi.Application.dll     Vipi.AuroraBridge.Contracts.dll   Vipi.AuroraProfiles.dll
Vipi.Domain.dll          Vipi.Host.dll                     Vipi.Hosting.dll
Vipi.Infrastructure.dll  Vipi.Infrastructure.MySqlMigrations.dll
Vipi.Ui.dll              en/Vipi.Ui.resources.dll          + i 9 .pdb corrispondenti
```

La correzione vera è in `Vipi.Infrastructure.dll`; le altre cambiano perché ricompilate insieme.
`wwwroot`, `content` e le librerie di sistema sono **identiche** a quelle già sul server.

Si può quindi caricare **solo quei 19 file** — un minuto invece di 421 — oppure tutto il pacchetto:
il risultato è lo stesso.

### 3. Riavviare

Da FileZilla: caricate un file `restart.txt` qualsiasi, anche vuoto, dentro `tmp/`, sovrascrivendo quello
che c'è. Da shell: `touch tmp/restart.txt`.

---

## Dopo il riavvio: una verifica sul database (in sola lettura)

Serve a scoprire se il difetto ha già lasciato una **pubblicazione sbagliata** in archivio. Non modifica
niente.

```sql
SELECT Id, TargetKey, VersionNumber, Status,
       JSON_UNQUOTE(JSON_EXTRACT(PayloadJson,'$.Doc.Title')) AS titolo_pubblicato
FROM DocReleases
WHERE TargetType = 'Airport'
ORDER BY TargetKey, VersionNumber;
```

Ogni riga deve leggere **`vIPI — <ICAO> …`**. Se una porta il nome di un Avvicinamento — «… Approach»,
«… Radar» — quella pubblicazione contiene il documento sbagliato.

**Come si rimedia:** si apre l'editor di quell'aeroporto e si **ripubblica**. Nessuna modifica a mano al
database: nello stesso ciclo AIRAC la pubblicazione vecchia passa da sola a «superata».

ℹ️ Nel database consegnato il 23 agosto **non c'era nessuna riga sbagliata** (verificate tutte e 35 le
pubblicazioni del file). Se dal 23 agosto in poi nessuno ha pubblicato un documento d'aeroporto dal sito,
la verifica uscirà pulita.

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC (LIRR, LIMM, LIBB) |
| Una pagina pubblica d'aeroporto | mostra il documento dell'**aeroporto**, non quello dell'Avvicinamento |
| Il login IVAO | entra, e in alto compare il vostro nome |
| `diagnostica/avvio-errore.txt` | **non deve esistere** |

Compilato con gli avvisi trattati come errori: **0 avvisi**, **3601 test verdi**.

⚠️ Come i precedenti, questo pacchetto **non è mai stato eseguito su Linux**: è compilato in modo
incrociato da Windows.
