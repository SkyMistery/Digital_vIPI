# Pacchetto 1.66.0 — solo i file cambiati

> **Timbro:** `1.66.0 · d2abdc4` (18 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.65.0** (`38a26f3`, online dal 17 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **9 file**: 1 in **`en/`** e 8 in **radice**. Niente `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

**S111 — le sezioni da scrivere.**

- In **ogni editor** (vIPI d'aeroporto, vSOP, APP, vIPI ACC, vLOA), sopra le Versioni, un riquadro «Sezioni da
  scrivere» dice quante sezioni **editoriali** del documento hanno qualcosa e quali sono ancora **vuote**, con
  l'elenco. Si aggiorna a «Fine modifica».
- Nella pagina **Versioni**, accanto a un documento compare una pastiglia **solo se manca qualcosa**, e nel suo
  pannello il riquadro in forma compatta.
- Contano solo le sezioni che scrive una persona: quelle che si riempiono dai dati (AOR, frequenze, SID, meteo…) e
  quelle **nascoste** no. La vIPI di ACC si conta **per blocco** (settori d'area, ogni gruppo APP…).
- Solo lettura: non cambia niente nei documenti.

## I 9 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (8)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Infrastructure.pdb    Vipi.Infrastructure.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll                ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application`, `Vipi.Infrastructure` e `Vipi.Ui` vanno **insieme**: il servizio nuovo è dichiarato
nel primo, scritto nel secondo e usato dal terzo.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Hosting` (sorgente invariato),
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`, `appsettings.json` e tutto
`wwwroot` con l'indice degli asset: controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e niente viene scritto.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.66.0 · d2abdc4`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- l'**editor della vIPI di Milano**: sopra le Versioni il riquadro «Sezioni da scrivere», col conto «N con qualcosa ·
  M vuote, su T» e i nomi delle sezioni vuote, blocco per blocco;
- la pagina **Versioni**: la pastiglia accanto ai documenti a cui manca qualcosa.

## Dopo il carico

- Il riquadro è un promemoria: dice dove scrivere, non scrive niente. Se un conto sembra sbagliato (una sezione che
  per voi è «di dati» contata fra le vuote, o il contrario), serve il nome del documento e della sezione.
- Restano dalle versioni di prima: il convertitore sul limite di Torino–Genova, il banco su Milano con le clausole
  vere, la riga «NOTAM» in Sorgenti, la vista live dal telefono, la prova del validatore dei tour sull'archivio ATC,
  gli elenchi di Torino–Genova (aggiungere {WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG
  aspettano il SOD.
