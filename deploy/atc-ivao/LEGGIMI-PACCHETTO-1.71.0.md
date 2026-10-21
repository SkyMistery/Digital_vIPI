# Pacchetto 1.71.0 — solo i file cambiati

> **Timbro:** `1.71.0 · 21af7c1` (21 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.70.0** (`2a21316`, online dal 21 ottobre). È una **MINOR con una migrazione DISTRUTTIVA**
> (`TogliNoteTrasferimenti`: toglie `CoordinationAgreements.Note` e `AgreementSections.Description`). La applica il
> sito da solo al primo avvio. Al primo avvio il sito porta anche le note delle LVP, delle regole piste e delle aree
> nei blocchi dei documenti. Nessun segreto nuovo, nessuna configurazione da toccare.
> **25 file**: 1 in **`en/`**, 9 in **`wwwroot/_content/Vipi.Ui/`** e 15 in **radice**.

> 🔴 **NON SI TORNA INDIETRO con le rinomine.** Dopo il primo avvio le due colonne dei trasferimenti non ci sono più,
> e la 1.70.0 le cerca. Per tornare indietro serve la copia del database fatta prima.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Prima di caricare

- **Una copia fresca del database** (Diagnostica → copia del database). È l'unica strada per tornare indietro.
- Le note e le descrizioni dei trasferimenti **si perdono apposta** (chiesto dal committente): sulla copia di
  produzione del 21 ottobre erano 0 note e 5 descrizioni, elencate in `docs/feature/2026-10-21-via-le-note.md` §4.
  Le due frasi di Milano vanno riscritte a mano dove servono.

## Che cos'è

- **S121 — allegati nei callout**: un callout può portare allegati della biblioteca.
- **S122 — link nel testo**: `[testo](https://…)` dappertutto, col tasto «Link» nella barra e sotto le tabelle.
- **S123a e S123b — le note diventano blocchi**: la nota delle LVP, le note delle regole piste e le note delle tabelle
  delle aree passano, al primo avvio, in paragrafi dei documenti. Le colonne vecchie restano vuote e si toglieranno in
  una consegna successiva.
- **S123c — via nota e descrizione dei trasferimenti** (non erano mai uscite in un documento); via anche
  `description` dall'API dei settori.
- **S124**: la «Tecnica operativa» del radar di torre nasce aperta.
- S125 e S126 riguardano solo gli strumenti di prova: nel sito non cambia niente.

## I 25 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (9)**:

```
vipi-editor.js   vipi-editor.js.br   vipi-editor.js.gz
vipi-print.css   vipi-print.css.br   vipi-print.css.gz
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (15)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                       ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← la migrazione: NON dimenticarlo
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I sette assiemi vanno **insieme**, e i file di `wwwroot` vanno **con** l'indice
`Vipi.Host.staticwebassets.endpoints.json`, o il sito chiede nomi che non esistono.

🔴 **Senza `Vipi.Infrastructure.MySqlMigrations.dll`** le colonne restano nel database e il modello non le conosce: la
Diagnostica dice `Schema` diverso da 0.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta
di compilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: controllato per
impronta, confrontando con la 1.70.0 che gira, e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.71.0 · 21af7c1`** nel piè di pagina (staff);
- 🔴 in Diagnostica **`Schema` = `0`**: dice che la migrazione è passata;
- da fuori l'indirizzo del foglio di stile dev'essere `vipi-theme.css?v=78b8ae45` (se la pagina appare senza stile, è
  il CSS vecchio in cache o non caricato con l'indice);
- in uno scalo con le LVP scritte, la nota è ora un **paragrafo** della sezione LVP;
- la pagina **Trasferimenti** si apre e non chiede più nota né descrizione.

## Dopo il carico

- Riscrivere a mano, dove servono, le due frasi di Milano che stavano nelle descrizioni dei trasferimenti.
- Restano dalle versioni di prima: il permesso «Settori» e la tecnica a blocchi (1.69), la sezione «Radar di torre» e
  la pastiglia nella vista live (1.68), le richieste aperte e le sezioni da scrivere (1.67), il convertitore sul limite
  di Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono,
  la prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova ({WN0} e {WS0}), Padova e Bologna,
  ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
