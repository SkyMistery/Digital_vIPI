# Pacchetto 1.76.0 — solo i file cambiati

> **Timbro:** `1.76.0 · cae2b84` (28 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.75.0** (`1aa329c`, online dal 26 ottobre). È una **MINOR con due migrazioni ADDITIVE**
> (`AggiungiConfrontiDocumenti` crea la tabella `DocumentComparisons`, `AggiungiSezioniFacoltative` crea la tabella
> `OptionalSections`; nessuna colonna toccata nelle tabelle che ci sono), e si consegna da sola via FTP: le migrazioni le
> applica il sito da solo al primo avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **17 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S133 — lo stato dei documenti**, più semplice da leggere:
  - il **confronto fra copie guarda il testo**: prima contava i blocchi, e una frase riscritta in un paragrafo che c'era
    già non risultava un cambiamento. La tabella delle differenze mostra le righe tolte e aggiunte;
  - la **scheda del documento** (da completare, rispetto al pubblico, prossima programmata, da rivedere) negli editor,
    sopra le Versioni, e nel pannello della pagina Documenti, al posto del riquadro «Sezioni da scrivere»;
  - la **pagina Documenti diventa il cruscotto del lavoro**: chip che contano e filtrano (da fare, da completare, da
    pubblicare, programmata indietro, da rivedere) e quattro colonne fisse in fondo a ogni riga;
  - le **sezioni facoltative**: pagina admin «Sezioni da scrivere» (`/services/vsop/admin/required-sections`), un
    interruttore «Facoltativa» per tipo di documento e sezione; vuota, una facoltativa non conta più fra le «da scrivere».

⚠️ **Al primo giro dopo l'avvio la pagina Documenti mostrerà PIÙ righe «da ripubblicare» di oggi.** È voluto: ogni
documento il cui testo è cambiato dall'ultima pubblicazione senza che cambiassero i blocchi prima non si vedeva. La
colonna «Da pubblicare» si riempie al primo giro della deriva dopo l'avvio (o aprendo la scheda di un documento).

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
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← le migrazioni: NON dimenticarlo
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I sei assiemi vanno **insieme**: le tabelle nuove sono dichiarate in `Vipi.Domain`, usate in
`Vipi.Application`, scritte in `Vipi.Infrastructure` (con le sue migrazioni) e mostrate da `Vipi.Ui`.

🔴 **Senza `Vipi.Infrastructure.MySqlMigrations.dll` il sito SEMBRA funzionare**: le tabelle non nascono e l'errore esce
solo quando qualcuno apre la pagina Documenti o un editor. Il controllo è `Schema = 0` qui sotto.

⚠️ **Restano fuori** `Vipi.Hosting`, `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato: cambia
solo l'impronta di compilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`:
controllato per impronta, confrontando con la 1.75.0 che gira, e col `git diff`.

**Per tornare indietro** bastano le rinomine: le due tabelle nuove restano nel database e la 1.75.0 le ignora.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.76.0 · cae2b84`** nel piè di pagina (staff);
- 🔴 in Diagnostica **`Schema` = `0`**: dice che le due migrazioni sono passate e le tabelle ci sono;
- la **pagina Documenti** (`/services/vsop/versions`): le chip in alto e le quattro colonne in fondo alle righe;
- la pagina **Sezioni da scrivere** (`/services/vsop/admin/required-sections`) si apre;
- se la pagina appare senza stile, è il CSS vecchio in cache o non caricato con l'indice: da fuori l'indirizzo
  dev'essere `vipi-theme.css?v=7f7eb20c`.

## Dopo il carico

- Guardare la pagina Documenti sui dati veri (mai vista lì: provata solo su dati inventati), e decidere se qualche
  sezione va segnata «Facoltativa».
- Restano dalle versioni di prima: la scheda dell'API con una chiave vera (1.74), scrivere il testo comune delle torri
  radar e rileggerne la traduzione (1.72–1.73); le due frasi di Milano dei trasferimenti da riscrivere (1.71); la
  sezione «Radar di torre» nasce nascosta: si mostra scalo per scalo (1.68); gli elenchi di Torino–Genova ({WN0} e
  {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
