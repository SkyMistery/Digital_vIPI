# Pacchetto 1.46.0 — solo i file cambiati

> **Timbro:** `1.46.0 · d54dbb0` (25 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.45.1** (`cb62ebc`, online dal 24 settembre). È una **MINOR, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare, niente in `wwwroot`.
> **9 file**: 8 in **radice** e 1 in **`en/`**.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **Documenti uniti: il lock vale per tutti e due.** Entrando in modifica su un documento unito (per esempio un
  vIPI civile e il suo MIL), l'editor ci entra solo se il lock è davvero **nostro**; se un collega ne tiene uno, si
  resta in lettura invece di lavorare su un documento che non si potrà salvare. Vale anche per il documento singolo.
- **«Da sistemare»** (`admin/pending`) è diviso in sezioni richiudibili, con **«Documenti da rivedere» in cima**.
- **Diagnostica → «Chi può editare»**: la colonna «Vale admin» diventa **«Concesso da»** — dice se il permesso
  viene dai codici staff, dal fondatore o da una promozione a mano.
- **Il MIL_CTR raccoglie solo il traffico militare.** Un settore civile che chiude non ricade più sul MIL solo
  perché la geometria lo sovrappone. L'APP di uno scalo «Solo militare» ha invece una riga **automatica** verso il MIL
  che ha il suo stesso padre, e poi il padre civile: nella Struttura compare come «automatico · MIL».

## I 9 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `en/` (1)** e **in radice (8)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
en/Vipi.Ui.resources.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto (quello di `en/` dentro la cartella `en/`);
2. si rinominano nell'ordine qui sopra: ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** tutti gli altri assiemi (compreso `Vipi.Infrastructure.MySqlMigrations.dll`: nessuna
migrazione nuova), `deps.json`, `runtimeconfig.json`, `appsettings.json`,
`Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.45.1, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei
documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la
riga è cambiata.

Col login da amministratore:

- il timbro **`1.46.0 · d54dbb0`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nella stessa Diagnostica, la tabella «Chi può editare» ha la colonna **«Concesso da»** (è la prova che
  `Vipi.Ui.dll` e le frasi inglesi sono arrivati insieme: in inglese deve dire «Granted by», non una chiave);
- `services/vsop/admin/pending` si apre con **«Documenti da rivedere»** come prima sezione.

Da fuori, per chi verifica (⚠️ dal 24 settembre Edge in modalità automatica non parte su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
