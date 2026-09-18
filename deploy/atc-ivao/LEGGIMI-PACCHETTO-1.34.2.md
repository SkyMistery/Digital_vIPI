# Pacchetto 1.34.2 — solo i file cambiati

> **Timbro:** `1.34.2 · 277b89c` (18 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.34.1** (`902e41e`, online dal 18 settembre). **PATCH, NESSUNA migrazione**: niente database, niente
> segreti nuovi. Si consegna via FTP. **6 file**, tutti in radice.

---

## Che cos'è

1. **Meno spesa di traduzione.** Le descrizioni delle tre aree D del 37° Stormo (Marettimo, Cielo campo, Mazara)
   tornavano rotte da Azure a ogni giro e si ripagavano: 497 caratteri ogni quarto d'ora. Ora l'italiano è in
   memoria come traduzione umana, e non si chiedono più. (Il testo di LIBN_APP, 385 caratteri, si sistema a mano dal
   pannello traduzioni dell'editor di Lecce Approach.)
2. **I guasti dei ripieghi delle shape si vedono.** Se GitHub, il sectorfile, gli ATZ o il cerchio falliscono
   durante l'import dei settori, ora esce un avviso in `avvisi-log.txt` invece di niente.
3. **`/vsop/ping`** (il segnale delle pagine aperte) non entra più nel registro del giorno né negli ultimi avvisi.

## I 6 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** i 6 file col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Hosting.dll`, `Vipi.Ui.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`
e gli altri assiemi (diversi solo per ricompilazione, sorgente invariato), tutto `wwwroot`, `endpoints.json`,
`deps.json`, `runtimeconfig.json`, `appsettings.json`: identici a 1.34.1, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.34.2 · 277b89c`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- al primo giro di traduzione dopo il riavvio (entro un quarto d'ora), nel `log-AAAA-MM-GG.txt`:
  «Descrizioni di aree IVAO messe in memoria: 3»; dal giro dopo quelle tre non si scartano più.
