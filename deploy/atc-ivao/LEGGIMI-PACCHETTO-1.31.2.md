# Pacchetto 1.31.2 — solo i file cambiati

> ✅ **CARICATO il 17 settembre 2026.** Timbro e `Schema 0` confermati dal committente; lo stile dei campi torna col resto della UI.

> **Timbro:** `1.31.2 · 2c64512` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.31.1** (`ab20c46`, online dal 17 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **8 file**: 5 in radice, 3 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

**I campi degli editor con lo stile del browser portati allo stile di casa** (§A67). Segnalato sul campo note della
tabella *Spazi aerei (AIP)*; cercati poi in tutti e cinque gli editor misurando lo stile di ogni campo (566). Erano sette
punti — grigi nel tema scuro, angoli vivi, carattere Arial o freccia del sistema operativo:

- note e classe della tabella **Spazi aerei (AIP)** (APP e ACC);
- note delle tabelle **Aree di lavoro** e **Bassa quota (BOAT)** del vSOP militare;
- filtro per ente delle **Aree regolamentate**;
- stazione METAR, ricerca frequenze e regole avanzate delle piste (aeroporto e militare);
- tendine di aggiunta e di scelta nelle **Radioassistenze** e negli **Alternati** del vSOP militare;
- la freccia di tutte le tendine nelle tabelle degli editor (e delle pagine *Struttura* e *Aeroporti*).

Nella tabella *Spazi aerei* il nome non va più a capo sillaba per sillaba in modifica. Nessun dato cambia.

## Gli 8 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-theme.css
wwwroot/_content/Vipi.Ui/vipi-theme.css.br
wwwroot/_content/Vipi.Ui/vipi-theme.css.gz
Vipi.Host.staticwebassets.endpoints.json
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i tre file di `wwwroot` **e** `endpoints.json` (viaggiano insieme), poi
   ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Application.dll` ed `en/Vipi.Ui.resources.dll` (impronte diverse solo per ricompilazione:
codice e frasi invariati), gli altri assiemi, il resto di `wwwroot`, `Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`:
identici a 1.31.1, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.31.2 · 2c64512`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- **dopo Ctrl+F5** (il foglio di stile nuovo arriva solo così), `services/vsop/libb/apps/editor?app=LIBP_APP` → *Modifica* →
  sezione AOR: la nota e la classe della tabella *Spazi aerei* hanno bordo arrotondato e fondo scuro come gli altri campi,
  la tendina ha la freccia, il nome sta su una riga.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17-set in MariaDB 11.4.10: timbro
`1.31.2 · commit 2c64512`, `pacchetto-verifica.js` **10/10**. Sullo stesso binario (CSS minificato) `campi-verifica.js` sui
cinque editor in modifica — ACC LIBB, APP LIBP_APP, aeroporto LIBP, militare LIBA, vLOA LIBB↔LGGG, 566 campi — in tema
scuro **e** chiaro: **0** campi con lo stile del browser (erano 7 punti prima). Tendina della classe con la freccia;
`spazi-aerei-verifica.js` ancora verde (4 poligoni su 6, chip che non riaccende). Nessun errore in console.
