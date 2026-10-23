# Pacchetto 1.72.0 — solo i file cambiati

> **Timbro:** `1.72.0 · 0dc563d` (22 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.71.0** (`21af7c1`, online dal 21 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **15 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 11 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S127 — testo comune delle torri radar.** In testa alla «Tecnica operativa» di **ogni** scalo con torre radar
  compaiono gli stessi due callout, scritti **una volta sola**. Si modificano **solo da admin**, da qualunque scalo con
  la torre radar (riquadro grigio in testa alla sezione). Il testo si **congela con la release**: quando cambia,
  «Da fare» elenca gli scali da ripubblicare.
- ⚠️ Prima di scriverlo: se in qualche scalo quei callout erano già stati copiati a mano nella tecnica operativa,
  vanno tolti da lì, o si vedrebbero due volte.

## I 15 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (11)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                       ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll          ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ I cinque assiemi vanno **insieme**, e `vipi-theme.css` va **con** l'indice
`Vipi.Host.staticwebassets.endpoints.json`.

⚠️ **Restano fuori** `Vipi.Hosting`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.AuroraBridge.Contracts`,
`Vipi.AuroraProfiles` (sorgente invariato: cambia solo l'impronta di compilazione), `deps.json`, `runtimeconfig.json`,
`appsettings.json` e il resto di `wwwroot`: controllato per impronta, confrontando con la 1.71.0 che gira, e col
`git diff`.

**Per tornare indietro** bastano le rinomine: nessuna migrazione. Il testo comune, se già scritto, resta nel database
e la 1.71.0 non lo mostra.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.72.0 · 0dc563d`** nel piè di pagina (staff);
- in Diagnostica **`Schema` = `0`** (nessuna migrazione: deve restare zero);
- da fuori l'indirizzo del foglio di stile dev'essere `vipi-theme.css?v=e393251a`;
- da admin, nell'editor di uno scalo con torre radar, il riquadro del testo comune in testa alla «Tecnica operativa».

## Dopo il carico

- Scrivere il testo comune vero (da admin, in un qualunque scalo con torre radar), poi ripubblicare gli scali che
  «Da fare» elenca.
- Restano dalle versioni di prima: le due frasi di Milano dei trasferimenti da riscrivere (1.71), la nota LVP come
  paragrafo, allegati e link (1.71, mai visti col login), il permesso «Settori» e la tecnica a blocchi (1.69), la
  sezione «Radar di torre» e la pastiglia nella vista live (1.68), le richieste aperte e le sezioni da scrivere (1.67),
  il convertitore sul limite di Torino–Genova, il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la
  vista live dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova ({WN0} e
  {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
