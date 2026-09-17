# Pacchetto 1.30.3 — solo i file cambiati

> **Timbro:** `1.30.3 · b5be0ff` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.30.2** (`76aceb3`, online dal 17 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **2 file**, tutti e due in **radice**.

---

## Che cos'è

**Il registro del giorno in `diagnostica/`** (§A59). Due famiglie di file nuove, **un file per giorno** (giorno UTC),
che il sito tiene **sette giorni** e poi cancella da solo:

- **`richieste-AAAA-MM-GG.tsv`**: una riga per ogni richiesta servita — ora, processo, versione, pagina (`rotta`, es.
  `/services/vsop/{Acc}`), percorso, esito, millisecondi, se chi chiedeva era autenticato (0/1, **mai il VID**). Le
  stringhe di query non vengono mai scritte; i ping `/vsop/health` e i file statici non contano.
- **`log-AAAA-MM-GG.txt`**: le righe informative del sito (poll IVAO, import, manutenzioni, traduzioni), una per voce.

Ogni file ha un **tetto di 5 MB**: oltre, una riga `# troncato:` e il resto di quel giorno non si scrive. Il volume
previsto è intorno a mezzo mega al giorno.

⚠️ Per `GET /_blazor` (esito 101) e `GET /vsop/live/atc` i millisecondi sono **quanto è rimasta aperta la pagina**, non
un tempo di risposta.

Si leggono con `python tools/registro-del-giorno.py <cartella diagnostica>`. Come gli altri file di `diagnostica/`,
restano fuori dal repo.

## I 2 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti e due** col nome finto;
2. si rinomina prima il `.pdb`, poi il `.dll`;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** tutti gli altri assiemi (codice non cambiato), `wwwroot`, `Vipi.Host.staticwebassets.endpoints.json`,
`Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.30.2, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.30.3 · b5be0ff`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`** (nessuna migrazione: resta com'era).

Via FTP, qualche minuto dopo il riavvio: in `diagnostica/` devono esserci **`richieste-2026-09-1X.tsv`** e
**`log-2026-09-1X.txt`** con la data del giorno, e dentro il `.tsv` la colonna `versione` deve dire `1.30.3 · b5be0ff`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17-set in MariaDB 11.4.10: timbro
`1.30.3 · commit b5be0ff`, `pacchetto-verifica.js` **10/10**. I due file sono nati con le righe attese — rotte
`/services/vsop/{Acc}` e `/services/vsop/{Acc}/editor`, la Ricerca, i circuiti `/_blazor` 101 e lo stream
`/vsop/live/atc` con la vita della pagina, nessuna query, nessun ping — e lo script li legge.
ℹ️ In locale la colonna `autenticato` è rimasta 0: l'utente di sviluppo non passa dal cookie di login. Online, con un
login vero, deve comparire 1 sulle richieste dello staff.
