# Pacchetto 1.46.4 — solo i file cambiati

> **Timbro:** `1.46.4 · 60e782a` (25 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.46.3** (`de5af3a`, online dal 25 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **8 file**: 3 in **`wwwroot/_content/Vipi.Ui/`** e 5 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare
> l'editor mentre si carica, e **evitare di caricare verso hh:55–57** (a quell'ora l'hosting chiude i processi).

---

## Che cos'è

- **Documenti uniti: dopo «Pubblica» si rileggono anche i membri.** Negli editor con un'unione (aeroporto, APP,
  vSOP militare) dopo la pubblicazione i documenti uniti restavano «in modifica» su una versione che non era più
  una bozza, e il gesto successivo finiva in «Modifica consentita solo su una versione in bozza» (segnalato su
  Catania).
- **Icone di accesso e uscita distinguibili.** La porta sta sempre a destra: entrando la freccia finisce dentro,
  uscendo parte da dentro ed esce a sinistra.
- **Diagnostica → «Chi può editare» scorre** invece di tagliare la tabella dopo poche righe.
- **Diagnostica degli arresti più precisa** (serve a capire perché il sito «va giù»):
  - ogni 5 minuti il log del giorno scrive quanta **memoria** usa il processo, e la riga ARRESTO porta uso e picco;
  - `avvio-diagnostica.txt` dice il **tetto di memoria** visto dal runtime;
  - `avvii.txt` scrive il **pid** su AVVIO e ARRESTO, e una riga **SEGNALE** quando il sistema chiede di fermarsi;
  - il verdetto «non si è spento in modo ordinato» guarda il processo giusto: non accusa più un processo che è
    ancora acceso accanto a un altro.

## Gli 8 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice (5)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ `vipi-theme.css` e `Vipi.Host.staticwebassets.endpoints.json` **viaggiano insieme**: l'indice dice con che nome
il sito chiede il foglio di stile. Uno senza l'altro fa chiedere un nome che non esiste, e la pagina esce senza
stile.

⚠️ **Restano fuori** tutti gli altri assiemi, `en/` (nessuna frase cambiata), `deps.json`, `runtimeconfig.json`,
`appsettings.json` e il resto di `wwwroot`: identici a 1.46.3, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra, e non basta che la riga sotto il campo cambi.** Il controllo è **la
Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei
documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la
riga è cambiata.

Col login da amministratore:

- il timbro **`1.46.4 · 60e782a`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- la pagina ha il suo **stile** (è la prova che CSS e indice sono arrivati insieme), e la tabella «Chi può
  editare» della Diagnostica scorre fino all'ultima riga;
- nel prossimo scarico: `avvio-diagnostica.txt` ha la riga **«Memoria vista dal runtime»**, e le righe di
  `avvii.txt` dalla 1.46.4 in poi portano **`pid`**.

Da fuori, per chi verifica (⚠️ dal 24 settembre Edge in modalità automatica non parte su questa macchina: se lo
script si ferma con «Failed to launch the browser process», la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```
