# Pacchetto 1.39.0 — solo i file cambiati

> **Timbro:** `1.39.0 · 1b25c4f` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.38.0** (`d267c1d`, online dal 21 settembre). È una **MINOR con UNA migrazione del database**,
> che tocca solo dati, non la struttura. Il pacchetto si consegna da solo via FTP. Nessun segreto nuovo,
> nessuna configurazione da toccare.
> **12 file**: 9 in **radice** e 3 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **Copia di sicurezza del database prima del carico.** Al primo avvio la migrazione `StarNascosteDiDefault`
> nasconde tutte le sezioni «STAR» degli aeroporti:
> `UPDATE DocumentSections SET IsHidden = 1 WHERE SectionKey = 'stars'`. Gira **una volta sola**.
>
> 🔴 **`Vipi.Infrastructure.MySqlMigrations.dll` è DENTRO e non va dimenticato.** Senza, il sito parte lo stesso
> e sembra a posto, ma le STAR restano visibili. Nessun errore lo segnalerebbe.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Se se ne carica uno senza l'altro, il sito chiede nomi che non esistono.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **Mappe a pezzi arrivando da un'altra pagina: corretto.** Si apriva un documento partendo da un'altra pagina
  del sito e le mappe uscivano con le tessere in fila e senza i settori, finché non si ricaricava. Il foglio di
  stile delle mappe spariva cambiando pagina. Ora è caricato su tutte le pagine.
- **vIPI di ACC: MIL e FSS fuori dalle configurazioni.** Stanno già nelle sezioni SCCAM e FIC, quindi l'editor
  non li propone più fra i settori da aprire. Non compaiono più nemmeno nelle tabelle d'accorpamento né fra le
  chip della mappa in cima. Le configurazioni scritte prima li perdono da sole alla lettura.
- **Colori sulle mappe**: ogni settore con `MIL` nel nome è **verde**, qualunque sia il tipo (CTR, APP, TWR).
  I settori **FSS** sono **petrolio**. Un colore scelto a mano nell'editor vince ancora.
- **Aeroporti: le sezioni «STAR» nascono nascoste.** Per mostrarle, dall'editor dello scalo si toglie il
  «nascosta» e si pubblica.

## I 12 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (3)** — si caricano e rinominano **per primi**:

```
vipi-aor.js   vipi-aor.js.br   vipi-aor.js.gz
```

**In radice (9)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← la migrazione
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` col suo `.dll`, e
   `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Ui`, `Vipi.Hosting`, `Vipi.Domain` e gli altri assiemi: il loro codice non è cambiato
e differiscono solo perché ricompilati. Restano fuori anche `deps.json`, `runtimeconfig.json`, `appsettings.json`
e il resto di `wwwroot`: sono identici a 1.38.0, controllato per impronta. Il foglio `leaflet.css` è già sul
server da tempo: adesso lo chiede anche l'intestazione di ogni pagina.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (lo script delle mappe è cambiato):

- il timbro **`1.39.0 · 1b25c4f`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- nel log del giorno (`diagnostica/log-*.txt`), all'avvio, la riga
  «*Applying migration '…_StarNascosteDiDefault'*»;
- l'**editor** di uno scalo (es. LIBD): la sezione **STAR** c'è ed è segnata nascosta; nella bozza compare con
  «🚫 nascosta (non pubblica)»;
- da una pagina qualunque (per esempio l'elenco dei vSOP), aprire un documento con una mappa **senza
  ricaricare**. Le mappe devono uscire intere, con i settori disegnati;
- l'**editor** di una vIPI di ACC (es. Brindisi): nelle «Configurazioni» fra i settori da aprire non ci sono
  più `…_MIL_CTR` né `…_FSS`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da fare

- 🔴 **Ripubblicare le vIPI di ACC.** La copia pubblica è una fotografia: senza ripubblicare, l'AoR in cima
  mostra ancora MIL e FSS e i colori vecchi. Per chi ancora non l'ha fatto dopo 1.38.0, valgono anche SCCAM,
  FIC e «Gestione del traffico».
- 🔴 **Ripubblicare gli aeroporti ripubblicati dopo 1.35.0**: nella loro copia pubblica la STAR è ancora
  visibile, e sparisce solo alla prossima pubblicazione.
- Dove le STAR servono davvero, si accendono dall'editor e si pubblica.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Questo pacchetto è compilato per **net8**, come tutti i precedenti. Il salto a net10 slitta a **1.40.0**.
