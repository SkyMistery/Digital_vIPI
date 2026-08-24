# vIPI — aggiornamento del 24 agosto 2026 (pacchetto «f»)

> ## ⛔ SUPERATO — non caricate questo
>
> Il pacchetto «f» non è mai stato caricato: al suo posto c'è
> [`LEGGIMI-CORREZIONE-20260824g.md`](LEGGIMI-CORREZIONE-20260824g.md), che contiene tutto quello che c'era
> qui più la versione in barra. Questo foglio resta solo perché il suo racconto del difetto è ancora quello
> giusto.

**Va sopra il pacchetto «e»**, che è già sul server. Sono **quattro file**, ed è **soli file**.

> ## ⛔ Il database NON si tocca
>
> Niente `.sql`, niente import. **Lo schema non cambia**: dal pacchetto «e» a questo non è stata aggiunta
> nessuna migrazione (verificato).

> ## 🔴 Due cose, e la seconda non è un caricamento
>
> **1.** I quattro file (§ *Che cosa caricare*). Chiude l'errore che ha visto il socio.
> **2.** La password fuori dal file che si scarica (§ *La cosa importante*). **Non serve FileZilla per
> portare file nuovi: serve creare una cartella e modificare un file che è già lì.** È la parte che vale di
> più, ed è indipendente dalla prima: si può fare anche dopo, ma va fatta.

---

## Che cosa cambia

### 1. La pagina «Error.» che ha visto il socio

Chi entrava per la prima volta — o comunque **chi era collegato e non è né admin né redattore** — poteva
vedere la pagina d'errore su `/services`, mentre lo stesso indirizzo, **senza aver fatto l'accesso**,
funzionava benissimo. Non era il login: il login era andato, era la pagina dopo a morire.

La barra in alto, per decidere se accendere il tasto **«Modifica»**, chiedeva al database **l'elenco di
tutti i documenti e poi, per ognuno, altre due domande** — a ogni pagina, e solo per gli utenti collegati.
Bastava un singhiozzo del database (una connessione caduta, il riavvio dell'applicazione) perché quella
domanda fallisse, e con lei **cadeva tutta la pagina**. Chi non era collegato non se ne accorgeva, ed è il
motivo per cui il sito sembrava a posto a tutti tranne che a lui.

Adesso è **una domanda sola**, e se fallisce resta spento il tasto — la pagina si apre lo stesso.

### 2. Se una pagina muore, adesso lo dice, e lascia una riga

La pagina d'errore era quella di serie di Microsoft: inglese, senza marchio, e tre paragrafi che spiegano
come si accende la «modalità di sviluppo». Ora è una pagina nostra, in italiano, che dice che cosa fare e
mostra un **codice**.

Quel codice si ritrova in **`diagnostica/errori-richieste.txt`**, un file nuovo accanto ai due dell'avvio,
insieme all'ora, all'indirizzo della pagina, al **VID** di chi ha ricevuto l'errore e al motivo tecnico per
esteso. ⚠️ **Se ricapita, scaricate quel file e mandatelo**: è l'unica cosa che permette di dire *perché*
invece di *forse*. Oggi quella riga non esisteva, e la causa si è dovuta dedurre.

ℹ️ Nel file non finiscono mai password, cookie, né la parte dell'indirizzo dopo il `?` (sul ritorno del
login quella parte è una credenziale). Si può spedire così com'è.

### 3. La password può uscire dal file che si scarica

Vedi la sezione qui sotto: è la parte che conta di più.

---

## 🔴 La cosa importante: `appsettings.Production.json` è scaricabile da chiunque

Oggi, indagando l'errore, ho misurato una cosa che non c'entrava con la segnalazione:

```
https://atc.it.ivao.aero/appsettings.Production.json   →  200 OK
```

Quel file contiene la **password del database** e le **credenziali IVAO**, e lo scarica chiunque digiti
quell'indirizzo. Non è un difetto dell'applicazione: sul vostro server la cartella dell'applicazione è
**anche** la cartella pubblica del sito, e il server web serve quei file da sé, prima ancora di passare la
richiesta a vIPI. Per lo stesso motivo si scaricano `appsettings.json`, i file `.dll` e quelli di
`diagnostica/`. (Le cartelle non si possono **elencare**: un file si prende solo indovinandone il nome esatto.)

**La riparazione vera è una sola** — che la cartella pubblica del sito non sia la cartella
dell'applicazione — e la può fare solo chi ha il pannello del server. Finché non è possibile, questo
pacchetto porta il rimedio che si può fare **solo con l'FTP**: *se il file non si può nascondere, si svuota*.

### I cinque passi (dieci minuti, si fanno una volta sola)

1. **Create la cartella `segreti`** dentro `public_atc`, allo stesso livello di `appsettings.Production.json`.
2. **Dentro, un file `.json` con un nome scelto da voi.** ⚠️ *Il nome è la protezione*: dev'essere
   impossibile da indovinare, e non va scritto in nessun posto che finisca sul server.
   - ✅ `k7f3a91c4e8b2.json` — ❌ `segreti.json`, `password.json`, `config.json` (sono i primi che si provano)
3. **Contenuto** — sono le stesse righe che oggi stanno in `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "Vipi": "Server=localhost;Port=3306;Database=itivao_atc;User Id=itivao_atc;Password=LA-PASSWORD-VERA;MaximumPoolSize=20;ConnectionIdleTimeout=60;DefaultCommandTimeout=30"
  },
  "VipiAuth": { "ClientId": "…", "ClientSecret": "…" },
  "Ivao":     { "ClientId": "…", "ClientSecret": "…" }
}
```

4. **Togliete gli stessi valori da `appsettings.Production.json`** (scaricatelo, modificatelo, ricaricatelo).
   Della connection string si toglie **solo** il pezzo `Password=…;`: server, database e utente possono
   restare. ⚠️ **Questo è il passo che chiude la falla**: finché la password sta anche là, spostarla non è
   servito a niente.
5. **Riavviate** (`tmp/restart.txt`) e aprite `diagnostica/avvio-diagnostica.txt`: deve dire
   `Cartella «segreti» ....... 1 file letti`, e più sotto `ClientSecret ... valorizzato`.

⚠️ **Se la password non arriva, il sito non parte — ed è voluto.** Senza, vIPI ripiegherebbe su un archivio
vuoto e il sito tornerebbe su **con l'aria di aver perso tutti i dati**: il modo peggiore di sbagliare. Il
motivo esatto lo trovate in `diagnostica/avvio-errore.txt` (vale anche se nel file JSON c'è una virgola di
troppo: lì dentro c'è scritto quale riga).

ℹ️ **Quello che resta scaricabile lo resta**: `.dll`, `appsettings.json`, i file di `diagnostica/`. Non ci
sono credenziali, ma è una mappa del server. Se volete togliere il facile: i `LEGGIMI-*.md` dei vecchi
pacchetti sono anch'essi sul server e si possono **cancellare**, non servono a far girare niente.

---

## Che cosa caricare

**Quattro file**, tutti nella **radice** dell'applicazione (nessuna sottocartella), nella cartella
`solo-4-file-f/`:

| File | Dimensione attesa |
|---|---|
| `Vipi.Application.dll` | **1.299.968 byte** |
| `Vipi.Host.dll` | **73.216 byte** |
| `Vipi.Infrastructure.dll` | **2.929.664 byte** |
| `Vipi.Ui.dll` | **1.868.288 byte** |

Totale **6.171.136 byte**. Tutto il resto è identico a quello che è già sul server.

ℹ️ **Niente `wwwroot`, niente indice `staticwebassets` questa volta**: nessun file di `wwwroot` è cambiato,
quindi la trappola del pacchetto «e» — l'indice e i `.js` da scambiare insieme — qui non si presenta.
I `.pdb` non servono a far girare niente; se li volete stanno nel pacchetto intero `linux-x64-20260824-f/`.

---

## Come si carica

Come avete chiesto, i file hanno il **nome vero**: si sovrascrivono quelli sul server, senza il giro del
`.nuovo`.

> ### ⚠️ Sovrascrivere un file mentre l'applicazione gira la fa morire
>
> È quello che ha buttato giù il sito il 23→24 agosto. Con questa procedura **è previsto**: mentre i quattro
> file salgono, il sito può rispondere con errori, e chi sta scrivendo in quel momento perde quello che non
> ha salvato. Si riprende da solo appena il caricamento è finito e l'applicazione riparte.
>
> Quindi: **fatelo in un momento tranquillo**, non durante un evento, e non fermatevi a metà. Sono sei
> megabyte: dura meno di un minuto.
>
> Se preferite la strada che non fa cadere il sito nemmeno per un secondo, è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md): si carica col nome finto e si rinomina
> alla fine. Con soli quattro file sono otto rinomine.

1. FileZilla in **binario** (Trasferimento → Tipo di trasferimento → **Binario**, non «Auto»).
2. **Prima, il salvagente**: rinominate sul server i quattro file attuali aggiungendo `.vecchio`
   (`Vipi.Host.dll` → `Vipi.Host.dll.vecchio`, e così per gli altri tre). Le rinomine sono istantanee e vi
   lasciano il rollback pronto: al contrario, si torna a prima senza ricaricare niente.
3. Caricate i quattro file di `solo-4-file-f/` nella radice dell'applicazione.
4. **Misurate**: F5 nel riquadro remoto e confrontate i byte con la tabella qui sopra. Se anche uno solo non
   corrisponde, ricaricate quello prima di riavviare.
5. Riavvio: file `restart.txt` vuoto dentro `tmp/`.
6. Aprite `https://atc.it.ivao.aero/services`.

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `diagnostica/avvio-diagnostica.txt` | la prima riga porta **data e ora di adesso**: è l'unica prova che sia ripartita la versione nuova |
| `diagnostica/avvio-errore.txt` | **non deve esistere**. Se c'è con la data di adesso, leggetelo: dice esattamente che cosa manca |
| `https://atc.it.ivao.aero/services` | si apre, sia da collegati sia da non collegati |
| Un socio **senza incarichi** che entra | vede l'elenco dei servizi, non la pagina d'errore |
| Voi, da admin | in barra c'è ancora il tasto **Modifica** |
| Dopo il passo 4 dei segreti | `https://atc.it.ivao.aero/appsettings.Production.json` si scarica ancora, ma **non contiene più la password** |

⚠️ **Restano da ripubblicare i documenti** per le frasi dei coordinamenti: è il punto rimasto in sospeso dal
pacchetto «e», e non c'entra con questo caricamento.

---

Compilato con gli avvisi trattati come errori: **0 avvisi**, **1943 test verdi** su net8 — quello che gira sul vostro server (3661 contando anche il giro su net10).

⚠️ Come i precedenti, questo pacchetto **non è mai stato eseguito su Linux**: è compilato in modo incrociato
da Windows.
