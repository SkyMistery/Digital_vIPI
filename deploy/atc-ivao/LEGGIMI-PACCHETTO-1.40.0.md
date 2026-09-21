# Pacchetto 1.40.0 — COMPLETO: il passaggio a .NET 10

> **Timbro:** `1.40.0 · fb19094` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.39.1** (`4df91c0`, online dal 21 settembre). **MINOR, NESSUNA migrazione**, nessun segreto nuovo,
> nessuna configurazione da toccare. **Nessuna funzione nuova**: dentro c'è solo il cambio di runtime, apposta,
> così se dopo il carico qualcosa non va la causa è una sola.
>
> 🔴 **Questa volta NON è una lista di file: è l'applicazione INTERA** — **480 file**, 136 MB. Cambia il runtime
> .NET che viaggia dentro il pacchetto (da 8 a **10.0.12**): `libcoreclr.so`, `System.Private.CoreLib.dll` e
> ogni assieme del framework. Un carico a metà lascerebbe sul server un runtime 8 con assiemi 10, o il
> contrario, e il processo non partirebbe con un messaggio che non somiglia alla causa.
>
> ✅ **Sul server non si installa niente.** Passenger continua a lanciare `dotnet Vipi.Host.dll` col `dotnet`
> che c'è (8.0.28): il pacchetto è autosufficiente e porta il suo runtime. Provato in un container col solo
> runtime 8, e il glibc di Debian 11 lo regge.
>
> ⚠️ **Il caricamento cambia forma, ma la regola resta la stessa**: nessun file dell'applicazione viene
> sovrascritto mentre gira. Invece di rinominare i file uno per uno (sono 480) si **spostano** in blocco da
> una cartella all'altra **sul server**. Per il server spostare è rinominare: non tronca niente, ed è
> istantaneo.

---

## Che cos'è

- Il sito passa da **.NET 8** a **.NET 10**. .NET 8 esce dal supporto il **10 novembre 2026**: da quel giorno
  il server web, il login IVAO e le chiavi delle sessioni non riceverebbero più correzioni di sicurezza.
- Chi usa il sito non vede niente di diverso. Le sessioni restano valide: chi è dentro resta dentro.
- In `diagnostica/avvio-diagnostica.txt` c'è una riga nuova, **`Runtime .NET`**, che dice quale runtime sta
  girando davvero. È la prova del carico (sotto).

---

## Prima di cominciare

1. **Copia di sicurezza del database** dalla Diagnostica (`services/vsop/admin/diagnostics`, scheda della
   copia). Non ci sono migrazioni, ma è un cambio grosso e costa un minuto.
2. **Un momento tranquillo.** Fra il passo 4 e il passo 6 passa un minuto o due in cui il sito può rispondere
   con un errore. Va bene la sera, non durante un evento.
3. FileZilla in **binario** (Trasferimento → Tipo di trasferimento → Binario).

## Le cose che NON si spostano mai

Nella radice dell'applicazione (`/var/www/vhosts/it.ivao.aero/public_atc/`) queste restano dove sono, sempre:

| Cosa | Perché |
|---|---|
| `segreti/` | password del database e credenziali IVAO |
| `appsettings.Production.json` | dice quale database usare |
| `appsettings.json` | resta quello che c'è: è identico, byte per byte, a quello di 1.39.1, e per questo **non è nel pacchetto** |
| `vipi-keys/` | le chiavi delle sessioni: senza, ogni login fallisce |
| `tmp/` | serve per il riavvio |
| `diagnostica/` | i registri del sito |
| le cartelle `nuovo-1.40.0/` e `vecchio-1.39.1/` | quelle del carico, qui sotto |

⚠️ Se nella radice vedete **qualcos'altro che non riconoscete** e che non è un file dell'applicazione
(`.dll`, `.so`, gli altri `.json`, `.pdb`, `wwwroot/`, `en/`, `content/`, `Vipi.Host`, `createdump`, i `.vecchio` dei
carichi di prima): **lasciatelo dov'è** e ditecelo.

---

## La procedura

### 1. Caricare il pacchetto in una cartella a parte — a sito acceso

Nella radice create la cartella **`nuovo-1.40.0`** e caricate lì dentro **tutto il contenuto** della cartella
`completo-1.40.0/` dello zip (sottocartelle comprese: `wwwroot/`, `en/`, `content/`). `IMPRONTE.txt` può
andare anche lui, è innocuo.

Il sito continua a girare sulla versione di prima: quella cartella non la guarda nessuno.

### 2. Controllare che sia arrivato tutto

F5 nel riquadro remoto, dentro `nuovo-1.40.0/`. Le dimensioni **in byte** di questi file devono essere
identiche:

| File | Byte |
|---|---|
| `Vipi.Host.dll` | 136 192 |
| `Vipi.Infrastructure.MySqlMigrations.dll` | 2 595 328 |
| `libcoreclr.so` | 7 110 952 |
| `System.Private.CoreLib.dll` | 15 576 872 |
| `Vipi.Host.runtimeconfig.json` | 570 |
| `wwwroot/_framework/blazor.web.js` | 200 645 |

E la coda di FileZilla deve dire **zero trasferimenti falliti**. Se uno è fallito, ricaricatelo. Fin qui non
avete toccato niente.

### 3. Creare la cartella per la versione di adesso

Nella radice create la cartella **`vecchio-1.39.1`**.

### 4. Mettere da parte 1.39.1 — da qui in poi, senza pause

Nella radice selezionate **tutti i file e le cartelle dell'applicazione**, cioè tutto **tranne** le cose della
tabella «NON si spostano» qui sopra (Ctrl+clic per togliere quelle dalla selezione). Trascinate la selezione
**sopra la cartella `vecchio-1.39.1`**.

FileZilla sposta i file sul server, senza scaricarli: ci vogliono pochi secondi. Il processo che gira non se ne
accorge subito, perché i file che ha già aperto restano suoi.

✅ Controllo: nella radice restano **solo** le cose della tabella.

### 5. Mettere al loro posto i file di 1.40.0

Entrate in `nuovo-1.40.0/`, selezionate **tutto** (Ctrl+A) e trascinate sulla voce **`..`** in cima
all'elenco: i file salgono nella radice.

✅ Controllo: `nuovo-1.40.0/` è **vuota**, e nella radice ci sono di nuovo `Vipi.Host.dll`, `wwwroot/` e gli
altri.

### 6. Riavviare

Caricate un `restart.txt` vuoto dentro `tmp/`, sovrascrivendo quello che c'è. Poi aprite
`https://atc.it.ivao.aero/services/vsop`: è la richiesta che fa nascere il processo nuovo. **La prima apertura
può durare una decina di secondi.**

---

## Il controllo dopo il riavvio

**Primo, dal file** — scaricate `diagnostica/avvio-diagnostica.txt`:

- la prima riga ha la data e l'ora di **adesso**;
- `Versione ... 1.40.0 · commit fb19094`;
- 🔴 **`Runtime .NET ... .NET 10.0.12`**. Se dice `8.0.…`, o la riga non c'è, è ripartita la versione di prima;
- **non** deve esistere `diagnostica/avvio-errore.txt` con la data di oggi. Se c'è, mandatecelo e tornate
  indietro (sotto).

**Poi, dal sito:**

- **la Ricerca**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete `LI` — **la riga sotto il campo
  deve cambiare**. È il controllo che distingue un sito vivo da uno caricato a metà;
- col login da amministratore: il timbro **`1.40.0 · fb19094`** e in `services/vsop/admin/diagnostics` la
  riga **`Schema` = `0`**;
- il login IVAO: uscite e rientrate.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Se qualcosa non va: tornare a 1.39.1

Due spostamenti e un riavvio, senza ricaricare niente:

1. create la cartella **`rotto-1.40.0`** e spostateci dentro i file dell'applicazione della radice (la stessa
   selezione del passo 4);
2. entrate in `vecchio-1.39.1/`, selezionate tutto e trascinate su **`..`**;
3. `tmp/restart.txt` e aprite il sito.

Poi mandateci `diagnostica/avvio-errore.txt` (se c'è) e `diagnostica/avvisi-log.txt`.

## Dopo qualche giorno

Se 1.40.0 gira senza problemi, `vecchio-1.39.1/` e la cartella vuota `nuovo-1.40.0/` si possono cancellare.
**Non prima**: `vecchio-1.39.1/` è il ritorno indietro già pronto.

ℹ️ Dal pacchetto dopo si torna alla solita **lista corta** di file cambiati, caricati col nome finto e
rinominati ([`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md)).
