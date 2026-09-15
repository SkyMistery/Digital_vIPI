# Pacchetto 1.27.0 — solo i file cambiati

> **Timbro:** `1.27.0 · f6cbea3` (15 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.26.1** (`fae666e`, online dal 14 settembre). **MINOR con TRE migrazioni ADDITIVE** — solo colonne
> nuove, nessuna tolta o rinominata: il pacchetto si consegna da solo via FTP, il database si aggiorna
> all'avvio.
> **28 file**: 15 in **radice**, 1 in **`en/`**, 12 in **`wwwroot/_content/Vipi.Ui/`**.
>
> 🔴 **Il file da non dimenticare è `Vipi.Infrastructure.MySqlMigrations.dll`.** Senza, le colonne nuove non
> nascono e il codice le pretende: le pagine toccate andrebbero in errore. La prova col login è la riga
> **`Schema` = `0`** (sotto).
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **vAWOS**
  - scali a **due e tre piste** (Malpensa, Fiumicino…) con l'impianto dei quadri originali: colonna dello
    scalo a sinistra e un blocco per pista, tutti della stessa altezza;
  - la **freccia** va nel senso di marcia della pista in uso (con arrivi e partenze su testate opposte, segue le
    partenze); le **testate** sono sempre arancioni;
  - senza RVR nel METAR le celle dicono **P2000**;
  - il **vento** ha piccole variazioni attorno al METAR, una ogni 45–200 secondi;
  - **chi ha scelto la pista** («from rule…», «from wind») lo vede solo lo staff;
  - regole piste e minimi LVP sono quelli della **versione pubblicata** dello scalo.
- **Regole piste, editor**: il banco di prova dice, per ogni regola e per ogni pista, **tailwind e vento
  traverso** e perché la regola si applica o no.
- **Hub `/services`**: scheda «Vedi chi è online ora» verso THE EYE (sito esterno, scheda nuova).
- **SID**: si possono **nascondere** al pubblico, e punto e transition di una SID importata si **correggono a
  mano**; «GOLF» di Decimomannu e le SID commentate non entrano più come SID.
- **APP non remotizzato**: indice nuovo con **«Gestione del traffico»** (IFR, VFR) e **«Tecnica operativa»**.
- **Tutti i documenti**: sotto-sezioni e blocchi si **alternano** nel corpo di una sezione.
- **Stazione METAR di riferimento**: uno scalo senza METAR proprio (LIRJ) usa quello di un altro campo.

## Le tre migrazioni

Tutte `AddColumn`, applicate da sole all'avvio:

| Migrazione | Che cosa aggiunge |
|---|---|
| `20260914043303_SidNascosteECorrette` | `AirportSids`: `IsHidden` (false di default), `FixOverride`, `TransitionOverride` |
| `20260915122709_SottosezioniFraIBlocchi` | `DocumentSections.BodyPosition` (vuota = come prima) |
| `20260915194926_StazioneMeteoDiRiferimento` | `Airports.MetarStationIcao` (vuota = lo scalo stesso) |

## I 28 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (12)** — si caricano e rinominano **per primi**, tutti:

```
vipi-awos.css    vipi-awos.css.br    vipi-awos.css.gz
vipi-awos.js     vipi-awos.js.br     vipi-awos.js.gz
vipi-editor.js   vipi-editor.js.br   vipi-editor.js.gz
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

**In radice e in `en/` (16)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← 🔴 le migrazioni
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

⚠️ **Restano fuori** `Vipi.AuroraProfiles.dll` e `Vipi.AuroraBridge.Contracts.dll` (codice non cambiato), e
`Vipi.Host.deps.json` / `Vipi.Host.runtimeconfig.json`, identici a quelli sul server.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

- **vAWOS** (`/services/vawos`, uno scalo qualsiasi): il quadro si riempie e dopo un minuto si aggiorna da
  solo. Da anonimo la riga «RWY IN USE» dice **solo** le piste, senza «from…».
- **vAWOS di Malpensa o Fiumicino**: colonna a sinistra e un blocco per pista.
- **`/services`**: fra gli strumenti c'è «Vedi chi è online ora».

Col login da amministratore:

- il timbro in barra: **`1.27.0 · f6cbea3`**;
- 🔴 `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`**: vuol dire che le tre migrazioni sono
  entrate. Se non è 0, manca quasi certamente `Vipi.Infrastructure.MySqlMigrations.dll`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
node .claude/skills/verifica-live/awos-verifica.js
```

---

## Dopo il carico: cose da sapere e da fare

- **Gli APP già pubblicati** tengono l'indice vecchio finché non si **ripubblicano**. ⚠️ Se un APP pubblicato
  aveva una tabella nella sezione VFR, in pubblico sparisce subito e torna alla ripubblicazione: conviene
  ripubblicare gli APP non remotizzati appena possibile.
- **LIRJ**: scrivere la stazione METAR di riferimento (LIRS) nell'editor dello scalo, sezione «METAR & TAF».
- **vAWOS e documenti**: una regola piste o un minimo LVP modificati **non** cambiano più il quadro finché il
  documento non si pubblica — tranne se la sezione è in **Live**, dove il cambio è immediato come nel documento.
- **SID «GOLF»** di LIED e le SID commentate di LIPB/LIRP spariscono al prossimo import di quegli scali.
- **Il bug «devo cliccare più volte sul suggerimento»** (editor SID) non si è riprodotto: c'è un rimedio, va
  fatto riprovare a chi l'ha segnalato.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Come per 1.26.1: il pacchetto è parziale e non può aggiornare il runtime (tre patch di sicurezza indietro). Lo
sistemerà il passaggio a **.NET 10**, che sarà **1.28.0** (carico completo) — il numero 1.27.0 lo prende
questo pacchetto, partito prima.

# Il pannello: come da 1.25.4

- ✅ **Le direttive nginx** per i file statici.
- ✅ **`passenger_min_instances ≥ 1`**.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).
- ⚠️ Il server risulta **Debian 11**, fuori dal supporto LTS dal 31 agosto 2026.
