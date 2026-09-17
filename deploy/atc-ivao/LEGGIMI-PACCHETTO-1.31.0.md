# Pacchetto 1.31.0 — solo i file cambiati

> **Timbro:** `1.31.0 · 68265f9` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.30.4** (`a10d350`, online dal 17 settembre). **MINOR, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **13 file**: 9 in radice, 1 in `en/`, 3 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

**Spazi aerei dell'AIP: una tabella sotto la mappa AoR** (§A65). Per gli avvicinamenti (e i blocchi della vIPI ACC)
agganciati ai volumi del KMZ — oggi LIBA, LIPH, LIPY, LICR, LIEE, LIRZ, LIBP — sotto la mappa compare la tabella
**nome · base · tetto · classe · note**. Nome e quote vengono dal file; **classe e note le scrive chi aggiorna il
documento** (in modifica: una tendina A–G e un campo di testo). La classe va scritta a mano perché il KMZ non la dà
su 113 CTR su 114.

## I 13 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
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

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i tre file di `wwwroot` **e** `endpoints.json` (viaggiano insieme), poi
   le frasi inglesi, poi ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Hosting.dll` e `Vipi.Infrastructure.MySqlMigrations.dll` (codice non
cambiato: le impronte diverse sono solo ricompilazione), il resto di `wwwroot`, `Vipi.Host.deps.json` e
`Vipi.Host.runtimeconfig.json`: identici a 1.30.4, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.31.0 · 68265f9`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- `services/vsop/libb/apps/editor?app=LIBP_APP`, sezione AOR: sotto la mappa la tabella **Spazi aerei (AIP)** con
  PESCARA CTR Z1…Z5; con «Modifica» la classe si sceglie e la nota si scrive, e dopo un ricarico restano.
  ⚠️ Prima **ricaricare la pagina** (Ctrl+F5): il foglio di stile nuovo arriva solo così.

## ⚠️ Perché il pubblico la veda: ripubblicare LIBA_APP

La tabella fa parte della mappa AoR, e **una release congela la mappa**. Sulla copia di produzione del 17-set:

- **LIBA_APP** ha una release (11-set) con l'AoR congelata **senza** tabella → il pubblico la vede solo dopo averlo
  **ripubblicato**. Conviene prima scrivere classe e note, poi pubblicare.
- **LIPH, LIPY, LICR, LIEE, LIRZ, LIBP** non hanno nessuna release: la tabella si vede subito in editor e in
  anteprima, e in pubblico dalla loro prima pubblicazione.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17-set in MariaDB 11.4.10: timbro
`1.31.0 · commit 68265f9`, `pacchetto-verifica.js` **10/10**. Sullo stesso binario, editor LIBP_APP: 5 righe, classe e
nota scritte, rilette dopo il ricarico e nel lettore in bozza; nessun errore in console. Pubblicato LIBP_APP sulla copia:
lo snapshot porta le righe e il lettore **pubblico** mostra la tabella. LIPR_APP (senza aggancio): nessuna tabella.
⚠️ Il ramo della vIPI ACC non è provato dal vivo: in produzione nessun settore di ACC è agganciato.
