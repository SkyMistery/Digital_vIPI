# Pacchetto 1.46.5 — solo i file cambiati

> **Timbro:** `1.46.5 · e24557e` (25 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.46.4** (`60e782a`, online dal 25 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Niente in `wwwroot`.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **Statistiche ATC: un nominativo doppio non ferma più il giro.** A volte la fotografia IVAO porta la stessa
  postazione due volte (una sessione vecchia rimasta appesa accanto a quella nuova, l'ultima volta LIRF_TW1_APP).
  Il giro del minuto si fermava intero con «An item with the same key», e quel minuto di traffico andava perso
  per tutti. Ora il traffico va alla sessione connessa per ultima.
- **Documenti uniti: «Pubblica versione» e «Scarta bozza» valgono per tutta l'unione.** Dalla pagina Versioni
  di un documento unito si pubblicano (o si scartano) insieme le bozze di tutti i membri. Prima di scrivere si
  prendono i lock di tutti: se un membro è in modifica da un collega non esce niente, e la pagina lo dice.
- **«Modifica» non resta spento.** Nei cinque editor il tasto restava spento per un lock di un collega letto
  all'apertura della pagina, anche dopo che il collega aveva finito. Ora si può premere: se il lock c'è ancora,
  lo dice e basta.

## I 7 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `en/` (1)** — si carica e rinomina **per primo**:

```
en/Vipi.Ui.resources.dll
```

**In radice (6)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `en/`, poi ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per
   ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** tutti gli altri assiemi (Domain e Infrastructure compresi: sorgente invariato, cambia solo
l'impronta della ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json`, **tutto `wwwroot`** e
`Vipi.Host.staticwebassets.endpoints.json`: identici a 1.46.4, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei
documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la
riga è cambiata.

Col login da amministratore:

- il timbro **`1.46.5 · e24557e`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- l'interfaccia in **inglese** mostra le frasi nuove (è la prova che `en/` è arrivato): in Versioni di un
  documento unito compare «Joined document: publishing or discarding this draft applies to the drafts of all N
  documents in the union» (in italiano: «Documento unito: pubblicare o scartare questa bozza vale per…»);
- **da provare a mano, su Catania** (in locale non si è potuto): Versioni → «Pubblica versione» pubblica anche i
  membri; «Scarta bozza» li scarta tutti; «Modifica» si preme anche se alla prima apertura un collega aveva il
  lock;
- nei prossimi `avvisi-log.txt` non compare più la firma `f45fedc38cea` («An item with the same key» dalle
  statistiche ATC).

Da fuori, per chi verifica (⚠️ dal 24 settembre Edge in modalità automatica non parte su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
