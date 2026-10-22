# Pacchetto 1.73.0 — solo i file cambiati

> **Timbro:** `1.73.0 · f7baf05` (22 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.72.0** (`0dc563d`, online dal 22 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Nessun file di `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S128 — il testo comune delle torri radar si traduce dove si vede.** Le frasi del testo comune (S127) compaiono nel
  pannello **«Traduzione»** dell'editor di ogni vIPI/vSOP italiano che ha la «Tecnica operativa»: in coda, segnate
  **«testo comune»**. Le corregge **solo un admin**, e la correzione vale per tutte le torri. Prima le traduceva la
  macchina e nessun posto le mostrava.

## I 7 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (6)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application` e `Vipi.Ui` vanno **insieme**: la riga del pannello ha un campo nuovo, scritto nel
primo e letto dal secondo.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Hosting`,
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta di compilazione),
`deps.json`, `runtimeconfig.json`, `appsettings.json` e tutto `wwwroot` (il foglio di stile non cambia): controllato
per impronta, confrontando con la 1.72.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le rinomine: nessuna migrazione. Le correzioni già fatte al testo comune restano nel
database come traduzioni.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.73.0 · f7baf05`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- da fuori il foglio di stile resta `vipi-theme.css?v=e393251a` (non cambia con questo pacchetto);
- da admin, nell'editor di uno scalo con torre radar e testo comune scritto: nel pannello «Traduzione», in fondo, le
  frasi segnate «testo comune».

## Dopo il carico

- Rileggere la traduzione inglese del testo comune dal pannello «Traduzione» (da admin).
- Restano dalle versioni di prima: scrivere il testo comune vero e ripubblicare gli scali che «Da fare» elenca (1.72);
  le due frasi di Milano dei trasferimenti da riscrivere (1.71), la nota LVP come paragrafo, allegati e link (1.71, mai
  visti col login), il permesso «Settori» e la tecnica a blocchi (1.69), la sezione «Radar di torre» e la pastiglia
  nella vista live (1.68), le richieste aperte e le sezioni da scrivere (1.67), il convertitore sul limite di
  Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono, la
  prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova ({WN0} e {WS0}), Padova e Bologna,
  ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
