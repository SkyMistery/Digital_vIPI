# Pacchetto 1.63.0 — solo i file cambiati

> **Timbro:** `1.63.0 · 099b0df` (16 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.62.0** (`f1aeaf5`, online dal 12 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **11 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 7 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

**S108 — nel banco di prova di Struttura si aprono e si chiudono anche gli enti degli altri centri.**

Scelto un ACC, il banco mostra anche i tasti degli enti **di altri centri** che compaiono nei suoi accordi:

- i capi degli accordi (chi riceve o cede dall'altra parte);
- tutta la loro catena di ripiego;
- chi raccoglie da fuori un settore dell'ACC;
- il gruppo intero di un centro che ha delle configurazioni dichiarate, coi suoi tasti.

Così si può provare, per esempio, che cosa succede ai trasferimenti di Brindisi se Atene Ovest chiude.
**«Tutti chiusi» chiude solo l'ACC scelto**; l'avviso «Non prevista» guarda solo i settori dell'ACC. Il motore che
decide i trasferimenti **non è toccato**.

ℹ️ Nello stesso giro, **fuori dal pacchetto**: la CI controlla di nuovo l'immagine Docker (era rossa dal 16 ottobre
per un SDK più nuovo dentro l'immagine). Non cambia niente sul server.

## Gli 11 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (7)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                ← insieme ai file di wwwroot
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll                ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application` e `Vipi.Ui` vanno **insieme**: il banco di prova cambia firma fra i due, e uno nuovo
con l'altro vecchio dà errore sulla pagina Struttura.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations` e `Vipi.Hosting`
(sorgente invariato, e nessuno usa il banco di prova), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`,
`deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e il banco non scrive niente.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.63.0 · 099b0df`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- **Struttura → Banco di prova**, scelto un ACC (per esempio Brindisi): oltre ai suoi settori ci sono i tasti degli
  enti degli altri centri (Roma, Atene, Zagabria…). Con tutto aperto, chiuso un ente di un altro centro, il conto
  «Cambiano mano» sale; «Tutti chiusi» spegne i soli settori dell'ACC;
- se il banco appare senza stile, è il CSS vecchio in cache o non caricato con l'indice: da fuori l'indirizzo
  dev'essere `vipi-theme.css?v=a1b78434`.

## Dopo il carico

- Da guardare su **Milano**, con le clausole vere: in locale l'ho provato su Brindisi.
- Restano dalle versioni di prima: la riga «NOTAM» in Sorgenti da confermare, la vista live dal telefono, la prova
  del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova (aggiungere {WN0} e {WS0}), Padova e
  Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
