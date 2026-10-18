# Pacchetto 1.67.0 — solo i file cambiati

> **Timbro:** `1.67.0 · c41681d` (18 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.65.0** (`38a26f3`, online dal 17 ottobre). La **1.66.0 non è mai stata caricata**: questo pacchetto la
> contiene, quindi si carica **solo questo** e la 1.66.0 si butta. È una **MINOR senza migrazioni**, e si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **13 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 9 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S112 e S113 — quante richieste dal campo sono ancora aperte.** Le «aperte» sono quelle nuove o prese in carico;
  quando sono zero il numero non compare.
  - nella **barra dello staff**, accanto a «Campo richieste»;
  - per gli **admin**, sull'angolo della **chiave** in alto: il numero porta alle richieste, la chiave resta ai
    permessi; nel menù ☰ c'è la voce «Campo richieste» col numero.
- **S111 — le sezioni da scrivere** (era la 1.66.0). In ogni editor, sopra le Versioni, un riquadro dice quante sezioni
  editoriali del documento hanno qualcosa e quali sono ancora vuote; nella pagina **Versioni** una pastiglia accanto
  ai documenti a cui manca qualcosa. Le sezioni che si riempiono dai dati e quelle nascoste non contano; la vIPI di
  ACC si conta per blocco. Solo lettura.

## I 13 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (9)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                ← insieme ai file di wwwroot
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Infrastructure.pdb    Vipi.Infrastructure.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll                ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application`, `Vipi.Infrastructure` e `Vipi.Ui` vanno **insieme**: i due servizi nuovi sono
dichiarati nel primo, scritti nel secondo e usati dal terzo.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Hosting` (sorgente invariato),
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto
di `wwwroot`: controllato per impronta, confrontando con la 1.65.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e niente viene scritto.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.67.0 · c41681d`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- se ci sono richieste dal campo aperte: il numero accanto a «Campo richieste» nella barra dello staff e sulla chiave
  in alto, uguale a quante nella pagina delle richieste sono «nuova» o «presa in carico»;
- l'**editor della vIPI di Milano**: sopra le Versioni il riquadro «Sezioni da scrivere»;
- se la pagina appare senza stile, è il CSS vecchio in cache o non caricato con l'indice: da fuori l'indirizzo
  dev'essere `vipi-theme.css?v=e5905e88`.

## Dopo il carico

- Restano dalle versioni di prima: il convertitore sul limite di Torino–Genova, il banco su Milano con le clausole
  vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono, la prova del validatore dei tour sull'archivio ATC,
  gli elenchi di Torino–Genova (aggiungere {WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG
  aspettano il SOD.
