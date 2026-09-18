# Pacchetto 1.34.0 — solo i file cambiati

> **Timbro:** `1.34.0 · 9d3530e` (18 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.33.0** (`7d25267`, online dal 18 settembre). **MINOR, NESSUNA migrazione**: niente database, niente
> segreti nuovi. Si consegna via FTP. **16 file**: 10 in radice (uno in `en/`), 6 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

1. **SID citate nel testo** (§A73). Nei documenti (prosa, callout, celle delle tabelle) una SID si cita col tasto
   **«SID»** dell'editor: nel testo salvato resta un riferimento, e la pagina mostra il nome **di oggi, completo**
   («BANAV 9A»). Quando la SID si aggiorna dal sectorfile, il nome nel documento segue da solo — nessuno deve
   riscriverlo, e la traduzione inglese non si rifà.
   - La pagina pubblica prende il nome dalla **tabella SID pubblica** dello scalo citato (congelata o viva, come
     la tabella stessa); l'editor e la bozza dall'anagrafica di oggi.
   - In cima all'editor: **«SID citate da ricontrollare»** (quelle che non si trovano più) e il tasto
     **«Cerca SID scritte a mano»**, che propone di convertire le SID già scritte nei testi.
   - I link agli allegati nel testo sono ora protetti dalla traduzione automatica.
   - La **Guida** (`services/vsop/guide`, editor dei blocchi) spiega il tasto e i due aiuti, in italiano e in inglese.
2. **Le callout con titolo tornano nell'editor**: prima non si vedevano (né modifica né cancella) pur uscendo nel
   documento pubblicato.

## I 16 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-editor.js
wwwroot/_content/Vipi.Ui/vipi-editor.js.br
wwwroot/_content/Vipi.Ui/vipi-editor.js.gz
wwwroot/_content/Vipi.Ui/vipi-theme.css
wwwroot/_content/Vipi.Ui/vipi-theme.css.br
wwwroot/_content/Vipi.Ui/vipi-theme.css.gz
Vipi.Host.staticwebassets.endpoints.json
en/Vipi.Ui.resources.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** i 16 file col nome finto;
2. si rinominano nell'ordine qui sopra: prima i sei file di `wwwroot` **e** `endpoints.json` (viaggiano insieme),
   poi `en/Vipi.Ui.resources.dll`, poi ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

🔴 **`vipi-editor.js` e `endpoints.json` vanno insieme**: il tasto «SID» vive in quel file, e l'indice dice con che
nome il sito lo chiede. Uno senza l'altro dà un editor in cui il tasto non fa niente.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Hosting.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`,
`Vipi.AuroraBridge.Contracts.dll`, `Vipi.AuroraProfiles.dll` (impronte diverse solo per ricompilazione, sorgente
invariato), il resto di `wwwroot`, `Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.33.0,
controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.34.0 · 9d3530e`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`** (nessuna migrazione: deve restare com'era);
- dopo **Ctrl+F5**, un editor d'aeroporto in modifica: nella barra di un paragrafo c'è il tasto **«SID»** in fondo;
  premuto, si apre «Cita una SID» con le SID dello scalo, e la scelta entra nel testo come `[[SID …]]`;
- in cima all'editor, sempre in modifica, il tasto **«Cerca SID scritte a mano»**.

## Da dire a chi scrive i documenti

- **La vSOP militare di LIBV cita SID con revisioni vecchie**: `ROBOT6A/B` (oggi `ROBO5A`/`ROBO5B`) e `DOGUS5A/B`
  (oggi `DOGU6A`/`DOGU5B`), più `CDC6A/B`, `VIE6A/B`, `VICTOR6A/B` scritte in forma compatta. Il pannello «Cerca SID
  scritte a mano» le elenca: vanno riscritte una per una col tasto «SID».
- Le SID citate prima di questo pacchetto (scritte a mano) restano come sono finché non si convertono.
