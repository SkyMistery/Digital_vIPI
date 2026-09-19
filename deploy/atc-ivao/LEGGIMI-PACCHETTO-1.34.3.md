# Pacchetto 1.34.3 — solo i file cambiati

> **Timbro:** `1.34.3 · f95d923` (19 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.34.2** (`277b89c`, online dal 18 settembre). **PATCH, NESSUNA migrazione**: niente database, niente
> segreti nuovi. Si consegna via FTP. **4 file**, tutti in radice.

---

## Che cos'è

1. **Il login parte una volta sola** (§A78). Un clic su «Login» avviava due giri verso IVAO (la navigazione interna
   del sito chiedeva l'indirizzo in sottofondo e poi lo richiedeva a pagina piena): a volte il ritorno falliva per
   «nonce» e l'utente vedeva la pagina d'errore, e al ricarico risultava dentro. Ora i link di login e di logout
   escono dalla navigazione interna.

## I 4 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** i 4 file col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `en/Vipi.Ui.resources.dll` (frasi ferme: diverso solo per ricompilazione), gli altri assiemi,
tutto `wwwroot`, `endpoints.json`, `deps.json`, `runtimeconfig.json`, `appsettings.json`: identici a 1.34.2,
controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.34.3 · f95d923`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- **esci e rientra** col tasto «Login» in alto (dopo Ctrl+F5): si deve arrivare dentro senza la pagina d'errore. Nel
  `richieste-AAAA-MM-GG.tsv` del giorno, prima del `/signin-oidc` deve esserci **una sola** riga `/services/vsop/auth/login`.
