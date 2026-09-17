# Pacchetto 1.32.0 — solo i file cambiati

> ✅ **CARICATO il 17 settembre 2026.** Timbro e `Schema 0` confermati dal committente; colonna «Mai usare» presente in vIPI e vSOP.

> **Timbro:** `1.32.0 · c1eddc9` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.31.2** (`2c64512`, online dal 17 settembre). **MINOR, UNA migrazione ADDITIVA**
> (`PisteMaiUsarePerVerso`: due colonne booleane in `AirportRunways`, che nascono a falso). Si consegna via FTP;
> la migrazione parte da sola all'avvio. **17 file**: 14 in radice, 1 in `en/`, 3 in `wwwroot/_content/Vipi.Ui/`.

---

## Prima di caricare

⚠️ **Scaricare una copia del database** dalla Diagnostica (`services/vsop/admin/diagnostics` → copia del database) e
tenerla. La migrazione **aggiunge soltanto** due colonne e non tocca dati esistenti — provata su una copia fresca della
produzione: 212 piste, tutte a falso, nessun comportamento cambiato finché qualcuno non spunta una casella — ma è pur sempre
una modifica allo schema, e in produzione gira da sola all'avvio.

## Che cos'è

**Soglie «mai in partenza» e «mai in arrivo»** (§A68). Nella tabella **Piste** degli editor d'aeroporto e del vSOP militare,
colonna **Mai usare**, due caselle per soglia: **DEP** e **ARR**. Quando **nessuna regola pista vale**, il ripiego sul vento
non sceglie mai quella soglia in quel verso — una pista usata solo per atterrare si marca DEP.

- Le **regole** che nominano la soglia restano valide; l'editor delle regole mostra un avviso.
- Il lettore del documento **non vede** le caselle; vede l'effetto sulla pista in uso.
- Vale sulla pista in uso del documento, del **vAWOS**, della **vista rapida** e dell'**elenco aeroporti** dopo aver
  pubblicato, come le regole: una casella spuntata e non pubblicata non cambia niente in pubblico.
- **Vista rapida ed elenco aeroporti** decidono ora la pista consigliata sulle regole del **documento pubblicato**, come già
  il vAWOS: prima leggevano le regole dell'editor anche se non pubblicate.
- La **Guida** spiega la colonna e la tabella «Spazi aerei (AIP)» sotto la mappa AoR (arrivata con 1.31.x).

## I 17 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-theme.css
wwwroot/_content/Vipi.Ui/vipi-theme.css.br
wwwroot/_content/Vipi.Ui/vipi-theme.css.gz
Vipi.Host.staticwebassets.endpoints.json
en/Vipi.Ui.resources.dll
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i tre file di `wwwroot` **e** `endpoints.json` (viaggiano insieme), poi le
   frasi inglesi, poi ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

🔴 **`Vipi.Infrastructure.MySqlMigrations.dll` è il file da non dimenticare**: senza, le colonne non nascono e il sito
**sembra funzionare** finché qualcuno non apre la tabella piste. Il controllo che lo prova è `Schema = 0` qui sotto.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Hosting.dll` e gli altri assiemi (impronte diverse solo per ricompilazione: codice invariato), il
resto di `wwwroot`, `Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.31.2, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.32.0 · c1eddc9`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`** — dice che le due colonne nuove ci sono;
- **dopo Ctrl+F5**, un editor d'aeroporto → *Modifica* → sezione Piste: colonna **Mai usare** con DEP e ARR per ogni soglia;
  nelle Regole piste il banco di prova, con un vento che favorisce una soglia marcata DEP, propone l'altra in partenza.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella su una copia **fresca** della produzione del 17-set in MariaDB 11.4.10 (senza la
migrazione): `Applying migration '20260917150418_PisteMaiUsarePerVerso'` all'avvio, 212 piste tutte a falso, timbro
`1.32.0 · commit c1eddc9`, `pacchetto-verifica.js` **10/10**, Diagnostica **`Schema 0`**. Sullo stesso binario
`mai-usare-verifica.js` su LIBD (vento 070/12): 07 marcata DEP → banco «DEP 25 · ARR 07», caselle rilette dopo il ricarico;
pubblicato → vAWOS (API, METAR di prova) «DEP 25 · ARR 07». Poi, nell'anagrafica **viva**, entrambe le soglie escluse in
tutti e due i versi (dai vivi uscirebbe «—»): col vento vero 080/13 vAWOS, **elenco aeroporti** («Departures 25 · Arrivals
07») e **vista rapida** («25 dep · 07 arr») seguono il pubblicato. `campi-verifica.js` sugli editor aeroporto e militare
(sul primo giro del pacchetto): nessun campo con lo stile del browser. Nessun errore in console.
⚠️ Un primo pacchetto 1.32.0 (`eb3547e`, zip `30c80b3e…`) era pronto ma **non è mai stato caricato**: superato da questo,
che aggiunge vista rapida ed elenco sul pubblicato. È in `artifacts/publish_old/20260917g-non-spedito/`.
