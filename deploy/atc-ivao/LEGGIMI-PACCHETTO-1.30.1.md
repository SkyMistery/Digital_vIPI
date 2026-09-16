# Pacchetto 1.30.1 — solo i file cambiati

> **Timbro:** `1.30.1 · be9a612` (16 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.30.0** (`d083a15`, online dal 16 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **2 file**, tutti e due in **radice**.

---

## Che cos'è

**Un file di diagnostica in più: `diagnostica/avvisi-log.txt`** (§A52). Ogni avviso o errore che il sito scrive nel
suo log — che su questo hosting non legge nessuno — finisce lì, insieme a quel che stava succedendo:

- **le ultime 10 richieste servite** (ora, metodo, percorso, esito, durata). Le stringhe di query non vengono mai
  scritte; i ping `/vsop/health`, i file statici e la meccanica interna delle pagine interattive non contano;
- **le ultime 10 righe informative del sito** (import, poll IVAO, manutenzioni).

Lo stesso avviso si scrive intero **una volta al giorno**; le ripetizioni sono righe `ANCORA`, al massimo una
all'ora. Gli errori che hanno già lo stack in `errori-richieste.txt` qui portano solo il contesto e il rimando.
Tetto 512 kB, poi `avvisi-log-precedenti.txt`.

ℹ️ **Se il file non c'è, è una buona notizia**: nasce al primo avviso. Sulla copia di produzione, un minuto di lavoro
normale non ne ha prodotto nessuno.

⚠️ Come `errori-richieste.txt`, **può contenere VID** (in alcuni messaggi): si tratta allo stesso modo.

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
`Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.30.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.30.1 · be9a612`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`** (nessuna migrazione: resta com'era) e ancora nessun rilievo
  «Pezzi di forma disallineati».

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione in MariaDB 11.4.10: timbro `1.30.1 · commit
be9a612`, `pacchetto-verifica.js` **10/10**; un avviso provocato apposta (copia del database chiesta «da un altro
sito») ha scritto la voce con le richieste vere — home, ricerca, editor, circuiti — e nessuna stringa di query.
⚠️ La prima prova di questo pacchetto aveva mostrato i dieci posti occupati dalla meccanica delle pagine interattive:
corretto e ripubblicato, per questo il commit del timbro non è quello del numero.
