# Pacchetto 1.43.1 — solo i file cambiati

> **Timbro:** `1.43.1 · a8a1cea` (23 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.43.0** (`54355eb`, online dal 23 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare, nessun file in `wwwroot`.
> **6 file**, tutti in **radice**.
>
> 🔴 **Va caricata PRIMA del 1° ottobre.** La correzione delle STAR fra i punti dei trasferimenti serve proprio fino
> a quella data; dopo il problema sparisce da solo.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Le STAR fra i punti dei trasferimenti.** Scrivendo un accordo di coordinamento, fra i punti suggeriti non
  comparivano le STAR (per esempio «ERIKA» a LIRN non proponeva ERIKA 1A). Le STAR del sectorfile sono timbrate col
  ciclo del 1° ottobre, e i suggerimenti guardavano solo al ciclo di oggi. Ora guardano al ciclo **entrante**. Vale
  anche per il selettore «Cita» dell'editor.
- ⚠️ Non cambia, di proposito: le sezioni «STAR» dei documenti aeroporto restano vuote fino al 1° ottobre, poi si
  riempiono da sole.
- **Sezioni in comune, «Hide»: si aggiornano anche i documenti uniti.** Nascondendo una sezione dalle sezioni in
  comune, i documenti membri dell'unione restavano com'erano a schermo finché non si ricaricava la pagina. Ora si
  aggiornano subito, negli editor di APP, aeroporto e vSOP militare.

## I 6 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In radice (6)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Domain`, `Vipi.Hosting` e
gli altri assiemi: il loro codice non è cambiato e differiscono solo perché ricompilati. Restano fuori anche
`en/Vipi.Ui.resources.dll` (nessuna frase cambiata), `deps.json`, `runtimeconfig.json`, `appsettings.json`,
`Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.43.0, controllato per impronta.

ℹ️ Durante le rinomine il processo vecchio può scrivere qualche errore `BadImageFormatException` nel log: carica
assiemi già sostituiti. Sparisce col riavvio. Meglio non usare l'editor mentre si carica.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro **`1.43.1 · a8a1cea`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- un accordo di coordinamento con LIRN fra gli arrivi (per esempio LIBB_ES → LIRR_TS, sezione Arrivi LIRN): nel
  campo dei punti, scrivendo «ERIK», compaiono **ERIKA 1A** ed **ERIKA 1C**;
- in un editor con un'unione (APP, aeroporto o vSOP MIL): «Shared sections…», «Hide» su una sezione di un membro →
  il membro si aggiorna **senza ricaricare** la pagina.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
