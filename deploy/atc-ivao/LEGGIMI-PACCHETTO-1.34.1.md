# Pacchetto 1.34.1 — solo i file cambiati

> **Timbro:** `1.34.1 · 902e41e` (18 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.34.0** (`9d3530e`). 🔴 **Se 1.34.0 non è ancora online, si carica PRIMA 1.34.0** (zip in
> `artifacts/publish_old/20260918c/`), poi questo. **PATCH, NESSUNA migrazione**: niente database, niente segreti
> nuovi. Si consegna via FTP. **4 file**, tutti in radice.

---

## Che cos'è

1. **La frase di coordinamento nomina TUTTI i punti della clausola** (§A75). Una riga con «BUDIN, ANC» dava una
   frase sola con «su BUDIN»; ora dice «su BUDIN o ANC» (in inglese «over BUDIN or ANC»), e con più di due punti
   «DINOB, RUTOM, LORNO o BELIX». Vale per vIPI, vLOA e per l'anteprima dell'editor accordi. Un punto solo: invariato.

⚠️ Le sezioni di coordinamento **congelate** nei documenti già pubblicati cambiano solo quando il documento si
**ripubblica**; le bozze e le sezioni vive cambiano subito.

## I 4 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** i 4 file col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Hosting.dll`, `Vipi.Infrastructure.dll`, `Vipi.Ui.dll` e gli altri
assiemi (impronte diverse solo per ricompilazione, sorgente invariato), tutto `wwwroot`, `endpoints.json`,
`en/Vipi.Ui.resources.dll`: identici a 1.34.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.34.1 · 902e41e`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- la bozza di una vIPI ACC con una clausola a più punti (es. LIRR, verso Trapani): la frase dice
  «via KAPIL, MEGAN, PAN, PIVOP o ADUKA».
