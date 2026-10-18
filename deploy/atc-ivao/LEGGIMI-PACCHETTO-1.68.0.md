# Pacchetto 1.68.0 — solo i file cambiati

> **Timbro:** `1.68.0 · 811010f` (18 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.67.0** (`c41681d`, online dal 18 ottobre). È una **MINOR con una migrazione ADDITIVA** (`TorreRadar`:
> due colonne nullable nella tabella `Airports`), e si consegna da sola via FTP: la migrazione la applica il sito da
> solo al primo avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **17 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S115 — «Radar di torre».** Nelle vIPI d'aeroporto e nei vSOP una sezione nuova, fra Piste e SID (nel vSOP nei
  Dati generali), con tre stati: sì / no / non indicato, e una figlia «Tecnica operativa» che c'è solo col sì. Il dato
  sta nell'**anagrafica** dello scalo (la torre di uno scalo civile e militare è una sola). Nella **vista live**,
  accanto all'ICAO, la pastiglia «Radar di torre ↗» quando il radar c'è e la sezione è visibile nel pubblicato.
  ⚠️ La sezione **nasce NASCOSTA** in tutti i documenti, anche in quelli già pubblicati: al pubblico non cambia niente
  finché qualcuno non la mostra.
- **S114 — sezioni da scrivere.** Una sezione senza testo proprio ma con sottosezioni visibili scritte non è più
  contata come «da scrivere».

## I 17 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (13)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                       ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← la migrazione: NON dimenticarlo
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I sei assiemi vanno **insieme**: il dato nuovo è dichiarato in `Vipi.Domain`, letto in
`Vipi.Application`, scritto in `Vipi.Infrastructure` (con la sua migrazione) e mostrato da `Vipi.Ui`.

🔴 **Senza `Vipi.Infrastructure.MySqlMigrations.dll` il sito SEMBRA funzionare**: la colonna non nasce e l'errore
esce solo quando qualcuno apre uno scalo. Il controllo è `Schema = 0` qui sotto.

⚠️ **Restano fuori** `Vipi.Hosting`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia
solo l'impronta di compilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`:
controllato per impronta, confrontando con la 1.67.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le due rinomine: le due colonne nuove restano nel database, vuote, e la 1.67.0 le
ignora.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.68.0 · 811010f`** nel piè di pagina (staff);
- 🔴 in Diagnostica **`Schema` = `0`**: dice che la migrazione è passata e le due colonne ci sono;
- l'**editor di uno scalo** (per esempio LIRF): fra Piste e SID la sezione «Radar di torre», nascosta, con «Non
  indicato»;
- se la pagina appare senza stile, è il CSS vecchio in cache o non caricato con l'indice: da fuori l'indirizzo
  dev'essere `vipi-theme.css?v=2d51a0f6`.

## Dopo il carico

- Per usarla: scrivere in anagrafica sì/no (e la tecnica) per gli scali con la torre radar, e mostrare la sezione nei
  documenti dove serve.
- Restano dalle versioni di prima: le richieste aperte e le sezioni da scrivere (1.67, mai viste col login), il
  convertitore sul limite di Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la
  vista live dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova
  (aggiungere {WN0} e {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
