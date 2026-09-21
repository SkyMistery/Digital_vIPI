# Pacchetto 1.38.0 — solo i file cambiati

> **Timbro:** `1.38.0 · d267c1d` (21 settembre 2026). È quel che compare nella barra in alto agli
> amministratori — se la barra è stretta, nella riga `Versione` della **Diagnostica** — e nella riga
> `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.37.0** (`2ad1790`, online dal 21 settembre). **MINOR, NESSUNA migrazione del database**: il
> pacchetto si consegna da solo via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **15 file**: 11 in **radice**, 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **Niente migrazione, ma all'avvio l'applicazione SISTEMA i documenti**: aggiunge le sezioni nuove alle
> vIPI di ACC e sposta il VFR dei gruppi APP (vedi sotto). È un passo automatico e ripetibile, ma scrive nel
> database: **fate una copia di sicurezza del database prima del carico**, come per una migrazione.
> `Vipi.Infrastructure.MySqlMigrations.dll` **non** è nel pacchetto.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **vIPI di ACC: SCCAM e FIC.** I settori militari (parte di mezzo del callsign con `MIL`) e i settori FSS
  escono dall'AoR in cima e hanno ciascuno la sua sezione con la sua mappa: **«SCCAM»** prima delle aree
  regolamentate, **«FIC»** dopo.
- **vIPI di ACC: il gruppo APP come l'APP non remotizzato.** Nel blocco di ogni gruppo APP arrivano
  **«Gestione del traffico»**, con **IFR** e **VFR** a blocchi (prosa, tabelle, callout), sopra i
  Coordinamenti, e **«Tecnica operativa»** subito sotto. Il VFR non è più la tabella fissa: quel che c'era
  scritto viene travasato in un testo e una tabella, niente si perde.
- **Trasferimenti: SID e STAR fra i punti.** Nel campo dei punti si può scrivere una procedura (i
  suggerimenti propongono le STAR sugli arrivi e le SID sulle partenze dello scalo della sezione). La frase
  diventa **«autorizzato via BANAV 9A»** e il luogo di trasferimento, se non se ne sceglie un altro, è **«al
  confine dell'AoR»**. Il nome **segue l'archivio** come nelle tabelle e nelle citazioni: scritta «BANA9A»
  esce «BANAV 9A», e quando la procedura cambia con un nuovo ciclo la frase dice il nome nuovo da sola.
- **«Cita» sa citare un'area regolamentata.** Ottava chip, **AREA**: `[[AREA 1242]]` → *LI R49B - Zita*, col
  nome di oggi dall'import IVAO. Se l'area sparisce, l'editor lo segnala in testata.
- **«Cita» scrive dove sta il cursore** anche dopo un clic fuori dal campo (prima finiva in coda).
- **La Diagnostica è più veloce**: dei 708 ms di «controlli» misurati dopo 1.37.0, la gran parte era rileggere
  i poligoni dei settori a ogni controllo. Il `title` della riga dei tempi dice ora quanto costa ogni controllo.

## I 15 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-editor.js   vipi-editor.js.br   vipi-editor.js.gz
```

**In radice e in `en/` (12)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinomina nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` e il suo `.dll`,
   `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.AuroraProfiles` e
`Vipi.AuroraBridge.Contracts`: il loro codice non è cambiato e differiscono solo per ricompilazione. Fuori
anche `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.37.0,
controllato per impronta — 466 file confrontati, 443 identici.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (lo script dell'editor è cambiato):

- il timbro **`1.38.0 · d267c1d`**, e `services/vsop/admin/diagnostics` con la riga **`Schema` = `0`**;
- nel log del giorno (`diagnostica/log-*.txt`), all'avvio, due righe: «*Spostato il VFR sotto «Gestione del
  traffico» in N …*» e «*Aggiunte N sezioni di catalogo mancanti …*»;
- l'**editor** di una vIPI di ACC (es. Brindisi): nel blocco Aerovia «SCCAM» e «FIC» attorno alle aree
  regolamentate; nel gruppo APP «Gestione del traffico» con IFR e VFR;
- in un editor, il tasto **«Cita»** mostra otto chip: `SID STAR FREQ ATC POS RWY FIX AREA`;
- una clausola di trasferimento di un **arrivo**: scrivendo le prime lettere nei punti compaiono le STAR dello
  scalo (in prova locale le STAR non c'erano: questa è la prima volta che si vedono).

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da fare

- 🔴 **Ripubblicare le quattro vIPI di ACC.** SCCAM, FIC e la «Gestione del traffico» dei gruppi APP sono
  nella **bozza** dal primo avvio, ma la copia pubblica è una fotografia: le mostra solo dalla release
  successiva. Lo stesso vale per i documenti che portano i trasferimenti con una SID o una STAR.
- Nelle sezioni «Gestione del traffico» dei gruppi APP, **IFR** e **Tecnica operativa** nascono vuote: vanno
  scritte, come si è fatto per gli APP non remotizzati.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Questo pacchetto è compilato per **net8**, come tutti i precedenti. Il salto a net10 slitta a **1.39.0**.
