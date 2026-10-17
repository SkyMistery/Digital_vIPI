# Pacchetto 1.62.0 — solo i file cambiati

> **Timbro:** `1.62.0 · f1aeaf5` (12 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.61.0** (`89bd1da`, online dal 12 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **16 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`** e 9 in **radice**.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S105 — nella vIPI di ACC il NOTAM di uno scalo sta dentro il blocco del suo APP.** In testa al documento restano
  i soli NOTAM che nominano l'ACC; quello che riguarda lo scalo di un APP compare **anche** dentro il blocco di
  quell'APP. La vista live di un APP che sta nella vIPI di ACC mostra quelli del suo blocco.
- **S106 — la sezione NOTAM negli editor e in fondo alla vista live.**
  - negli **editor** (aeroporto, vSOP, APP, vIPI ACC), in testa alla colonna delle sezioni, una nota «NOTAM dal
    vivo» e sotto il riquadro com'è adesso — o «Adesso non ce n'è nessuno». Non si scrive: è lì per far vedere che
    cosa leggerà il pubblico, come il meteo;
  - nella **vista live** la sezione «NOTAM» c'è **sempre**, in fondo e chiusa, col conto; la chip rossa in testata
    la apre e porta la pagina lì. Senza NOTAM la chip non c'è e la sezione dice che non ce ne sono.
- **S107 — il banco di prova di Struttura mostra tutti i trasferimenti.**
  - i tasti delle configurazioni sono **una riga per gruppo** e cambiano i **soli** settori del loro gruppo: lo
    scenario è la combinazione;
  - la tabella dei trasferimenti ha **una riga per ogni clausola**, con chi cede e chi riceve *scritto* e *davvero*, e
    l'**esito**: come scritto, raccolto da un altro, a sé stesso, non coperto. Prima si vedevano solo i punti che
    cambiavano mano, e un trasferimento come `LIMM_WS2_CTR → LIMJ_WS0_APP` «non compariva»;
  - un filtro a tre tasti: **Tutte**, **Cambiano mano**, **Non coperte**, ognuno col suo conto.

  Il motore che decide i trasferimenti **non è toccato**: cambia che cosa il banco fa vedere.

## I 16 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
vipi-ui.js       vipi-ui.js.br       vipi-ui.js.gz
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
con 1.54.2). ⚠️ Questa volta `Vipi.Application`, `Vipi.Infrastructure` e `Vipi.Ui` vanno **tutti e tre**: fra loro
cambiano delle firme (il banco di prova e la lettura dei NOTAM), e uno nuovo con un altro vecchio dà errore sulla
pagina Struttura e sui documenti.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure.MySqlMigrations` e `Vipi.Hosting` (sorgente invariato, e non
usano niente di quel che cambia), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e niente di nuovo viene scritto nel database.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.62.0 · f1aeaf5`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- la **vIPI ACC di Milano**: in testa IT-A002/26 e IT-A003/25; IT-A002/26 **anche** più giù, dentro il blocco
  dell'APP di Genova (se LIMJ sta in un gruppo APP di LIMM);
- la **vista live** `/services/vsop/live/LIMM_WS2_CTR`: in fondo la sezione «NOTAM» chiusa col conto; la chip rossa in
  testata la apre e la pagina scende fin lì. Su una postazione senza NOTAM: niente chip, e la sezione col conto 0;
- un **editor** (per esempio la vIPI ACC di Milano): in testa alle sezioni la nota «NOTAM dal vivo» e il riquadro;
- **Struttura → Banco di prova**, scelto un ACC: sotto, la tabella con **tutte** le clausole e la colonna «Esito», e
  i tre tasti «Tutte», «Cambiano mano», «Non coperte» coi conti. Chiuso un settore, i conti cambiano;
- se la chip non apre la sezione o il banco appare senza stile, sono gli asset vecchi in cache o non caricati con
  l'indice: da fuori gli indirizzi devono essere `vipi-theme.css?v=c4d0d06a` e `vipi-ui.js?v=ff9de306`.

## Dopo il carico

- **Da guardare**: lo scorrimento della pagina quando si preme la chip, e la vista live dal telefono — sul telefono
  non l'ha provata nessuno.
- Se nel banco manca ancora un trasferimento che dovrebbe esserci, serve il **nome dell'accordo**: è il seguito di
  S107.
- Restano dalla 1.61.0: confermare che in Sorgenti la riga «NOTAM» dice «aggiornata».
- Restano dalla 1.60.0 e dalla 1.58.0: la prova del validatore dei tour sull'archivio ATC; gli elenchi di
  Torino–Genova (aggiungere {WN0} e {WS0}), Padova e Bologna, e ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
