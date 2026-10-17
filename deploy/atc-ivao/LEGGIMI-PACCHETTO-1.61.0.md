# Pacchetto 1.61.0 — solo i file cambiati

> **Timbro:** `1.61.0 · 89bd1da` (12 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.60.0** (`3ea92b2`, online dal 10 ottobre). È una **MINOR con UNA migrazione additiva**, e si consegna
> da sola via FTP: il database si aggiorna da sé all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **17 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 13 in **radice**.

> ## ⚠️ Da questa versione il sito legge una cosa in più da IVAO
>
> **I NOTAM della divisione**: 50 secondi dopo l'avvio e poi **ogni 30 minuti**, da
> `GET /v2/divisions/IT/notams/all` — un indirizzo pubblico di IVAO, **senza token**. Non serve configurare niente:
> i valori stanno nel codice (`Ivao:NotamImportMinutes`, minimo 5; `Ivao:DivisionNotamsPathFormat`).
> Se IVAO non risponde, i NOTAM già letti restano e il sito funziona come prima.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S103 — i NOTAM della divisione in testa ai documenti pubblici.** Un riquadro «NOTAM» in cima al documento, con
  numero, validità («in vigore» o «in arrivo») e il testo che si apre:
  - sulla **vIPI d'aeroporto**, sul **vSOP** e sulla vIPI **APP** quando il NOTAM nomina quello scalo;
  - sulla **vIPI ACC** quando nomina l'ACC, o lo scalo di un suo APP;
  - da **sette giorni prima** dell'inizio, e fino alla scadenza;
  - **solo nella vista pubblica** (non nell'editor), e **non sulle vLOA**.
- **S104 — i NOTAM nella vista live.** In testata una chip rossa **«! NOTAM»**: un clic apre il riquadro in fondo
  alla pagina. C'è **solo se il documento della postazione è pubblicato**.
- In **Sorgenti** (`/services/vsop/admin/sources`) c'è una riga nuova, «NOTAM», in fondo.

**La migrazione** (`NotamDellaDivisione`): crea la tabella `Notams` con un indice. Non toglie e non rinomina niente.

## I 17 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (13)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Hosting` (sorgente invariato, e non usa niente di quel che cambia),
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto
di `wwwroot`: controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: la migrazione aggiunge soltanto, e la 1.60.0 gira sullo schema
nuovo ignorando la tabella `Notams`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.61.0 · 89bd1da`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`: senza, la tabella non nasce e i NOTAM non compaiono mai, senza
  altri segnali);
- **un paio di minuti dopo l'avvio**, in **Sorgenti**: la riga «NOTAM» dice **aggiornata**. Se dice che non è mai
  riuscita, il sito non raggiunge quell'indirizzo di IVAO: scaricate `diagnostica/` e ditelo;
- la **vIPI ACC di Milano** (pubblica): in testa c'è il riquadro **NOTAM**. Il 12 ottobre IVAO ne dà quattro
  (IT-B002/25, IT-A003/25, IT-A002/26 e ITW004/26, scaduto), e a Milano se ne vedono due: IT-A002/26 e IT-A003/25.
  Gli altri documenti attesi: le vIPI di LIRQ, LIPC, LIPE, LIPK, LIPR, LIMG, LIMJ e le vIPI ACC di LIPP e LIRR — dove
  sono pubblicate;
- la **vista live** di una postazione di Milano (`/services/vsop/live/LIMM_WS2_CTR`): la chip rossa «! NOTAM» in
  testata, e un clic apre il riquadro;
- se il riquadro appare senza colori o storto, è il CSS vecchio in cache o `vipi-theme.css` non caricato con l'indice:
  da fuori l'indirizzo dev'essere `vipi-theme.css?v=c3d2feac`.

## Dopo il carico

- **Da guardare, e dire se va**: S103 e S104 sono stati fusi dopo le schermate del Sito, non dopo una prova a schermo
  del committente. Non visti da nessuno: la chip che compare da sola mentre la vista live è aperta, e la vista live
  dal telefono.
- Restano dalla 1.60.0: la prova del validatore dei tour sull'archivio ATC.
- Restano dalla 1.58.0: gli elenchi di Torino–Genova (aggiungere {WN0} e {WS0}), Padova e Bologna, e ripubblicare le
  vIPI ACC. Restano i gesti su LIRE/LIBG, che aspettano il SOD.
