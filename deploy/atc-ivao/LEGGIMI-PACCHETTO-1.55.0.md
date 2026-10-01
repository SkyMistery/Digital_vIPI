# Pacchetto 1.55.0 — solo i file cambiati

> **Timbro:** `1.55.0 · 395bce9` (1 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.54.3** (`b6ae28b`, online dal 1 ottobre). È una **MINOR senza migrazioni**: si consegna da sola via
> FTP, il database non cambia, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo, nessuna
> configurazione da toccare.
> **22 file**: 1 in **`en/`**, 12 in **`wwwroot/_content/Vipi.Ui/`** e 9 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S83 — SID e STAR nella barra di ricerca**, con la stessa vista della pagina; sopra le SID una nota radio con un
  esempio preso dalla prima SID dello scalo.
- **S84 — vista live compatta su una riga**; l'avviso diventa **«ONLY FOR SIMULATION: DO NOT USE FOR REAL LIFE
  OPERATIONS»** ovunque; l'ora Z nella barra per tutti; impaginazione per telefono e tablet.
- **S85** riquadro «Importante» con l'ottagono «!» · **S86** riconnessione discreta anche sulle pagine pubbliche
  interattive · **S87** tasto vSOP accanto a Stampa nella vIPI d'aeroporto.
- **S88–S90 — ricerca rapida nella vista live**: scali, aree, postazioni e frequenze, punti di SID/STAR,
  trasferimenti, radioassistenze; anche su TWR/GND/DEL in testata.

## I 22 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (12)**:

```
vipi-aor.js             vipi-aor.js.br             vipi-aor.js.gz
vipi-riconnessione.js   vipi-riconnessione.js.br   vipi-riconnessione.js.gz    ← avvia la pagina
vipi-ui.js              vipi-ui.js.br              vipi-ui.js.gz
vipi-theme.css          vipi-theme.css.br          vipi-theme.css.gz
```

3. **in radice (9)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Hosting`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.AuroraBridge.Contracts`,
`Vipi.AuroraProfiles` (sorgente invariato, cambiati solo per la ricompilazione), `deps.json`, `runtimeconfig.json`,
`appsettings.json` e il resto di `wwwroot`: identici a 1.54.3, controllato per impronta e col `git diff`. Le interfacce
nuove (`IProcedureCercabili`, `IRicercaLive`) stanno in Application e sono implementate dentro il pacchetto.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata. Provate
anche una SID (es. il nome di una SID di Fiumicino): deve uscire fra i risultati.

- col login da staff: il timbro **`1.55.0 · 395bce9`** nel piè di pagina; in Diagnostica `Schema` resta **`0`**;
- vista live: la barra compatta e la ricerca rapida (scrivete due lettere di uno scalo);
- l'avviso in fondo dice «… REAL LIFE OPERATIONS»;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti su LIRE/LIBG, che aspettano il SOD.
