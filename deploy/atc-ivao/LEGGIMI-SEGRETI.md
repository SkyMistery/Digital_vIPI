# I segreti fuori dal file che si scarica

> Vale **solo** per `atc.it.ivao.aero`. In sviluppo e su Render non serve: là la cartella
> dell'applicazione non è il document root, e `appsettings` non è scaricabile.

## Il fatto, misurato il 24 agosto 2026

Sul server la cartella dell'applicazione **è anche il document root del sito**: il server davanti serve i
file da sé, prima di passare la richiesta all'applicazione. Misurato con `curl -I`:

| URL | esito |
|---|---|
| `https://atc.it.ivao.aero/appsettings.Production.json` | **200** — dentro ci sono password del database e credenziali IVAO |
| `/appsettings.json`, `/diagnostica/avvio-diagnostica.txt` | 200 |
| `/Vipi.Host`, `/Vipi.Host.dll`, `/Vipi.Host.pdb` | 200 |
| `/vipi-keys/`, `/diagnostica/`, `/deploy/` | 404 — **niente elenco cartelle** |

**Non è un difetto dell'applicazione, e l'applicazione non può ripararlo**: nessuna riga può intercettare
una richiesta che il server davanti soddisfa da solo. La riparazione vera è una sola — che il document root
non sia la cartella dell'applicazione — e va chiesta a chi ha il pannello.

Quello che segue **non è la riparazione**: è ciò che si può fare avendo **solo l'FTP**. Sposta i segreti da
«scaricabili con un indirizzo scritto nel nostro repository» a «scaricabili da chi indovina un nome che
nessuno conosce». È la stessa protezione che regge oggi il key-ring, `vipi-keys/key-{guid}.xml`.

## Che cosa fare, in cinque minuti

**1. Create la cartella `segreti` accanto a `Vipi.Host`** (dentro `public_atc`, allo stesso livello di
`appsettings.Production.json`). ⚠️ **Tutto minuscolo**: su Linux `Segreti` e `segreti` sono due cartelle
diverse. Va bene anche `secrets` — sono i due soli nomi che l'applicazione cerca.

**2. Dentro, mettete UN file `.json` con un nome scelto da voi.** Il nome è la protezione: non deve essere
indovinabile e non va scritto da nessuna parte che finisca sul server.

- ✅ va bene: `k7f3a91c4e8b2.json`, `mario-ha-scelto-questo-nome-lungo-e-strano-42.json`
- ❌ non va: `segreti.json`, `password.json`, `config.json`, `produzione.json` — sono le prime che si provano

**3. Contenuto del file** (è configurazione normale: vince su `appsettings.Production.json`):

```json
{
  "ConnectionStrings": {
    "Vipi": "Server=localhost;Port=3306;Database=itivao_atc;User Id=itivao_atc;Password=LA-PASSWORD-VERA;MaximumPoolSize=20;ConnectionIdleTimeout=60;DefaultCommandTimeout=30"
  },
  "VipiAuth": { "ClientId": "…", "ClientSecret": "…" },
  "Ivao":     { "ClientId": "…", "ClientSecret": "…" }
}
```

**4. Togliete gli stessi valori da `appsettings.Production.json`.** ⚠️ Questo è il passo che chiude la
falla: finché la password resta anche là, spostarla non è servito a niente. Della connection string si
toglie **solo** il pezzo `Password=…;` — il resto (server, database, utente, pool) può restare.

**5. Riavviate** con `tmp/restart.txt` e controllate `diagnostica/avvio-diagnostica.txt`: deve dire
`Cartella «segreti» ....... 1 file letti` e, più sotto, `ClientId/ClientSecret ... valorizzato`.

## Le tre cose che vanno sapute

⚠️ **Se la password non arriva, il sito non parte** — ed è voluto. Senza connection string l'applicazione
ripiegherebbe su un file SQLite vuoto: il sito tornerebbe su con l'aria di **aver perso tutti i dati**, che
è il modo peggiore di sbagliare. Il motivo è scritto per esteso in `diagnostica/avvio-errore.txt`.

⚠️ **Il nome del file non viene mai scritto nella diagnostica.** `avvio-diagnostica.txt` è a sua volta
scaricabile: dice *quanti* file ha letto, mai *quali*. Per la stessa ragione non mettetelo in una email che
gira, e se pensate che sia trapelato, cambiate nome al file: costa un rinomina via FTP.

⚠️ **Quello che resta scaricabile lo resta.** Le `.dll`, i `.pdb`, `appsettings.json`, i file di
`diagnostica/` (che dal 24 agosto contengono anche gli stack trace delle richieste fallite e il VID di chi
le ha subite). Non ci sono credenziali, ma è una mappa del server. Chiudere davvero la faccenda resta un
lavoro di hosting: `docs/lavori-aperti.md` §A13.

> ### ⚠️ Due cose che ingannano, viste sul campo il 24 agosto 2026
>
> **1. `restart.txt` non riavvia da solo.** Passenger se ne accorge alla **richiesta successiva**: dopo
> averlo caricato, **aprite il sito una volta**, altrimenti l'applicazione resta quella di prima e sembra
> che il riavvio non abbia funzionato.
>
> **2. Il browser vi mostra la diagnostica di prima.** È un file di testo e viene messo in cache: ricaricate
> con **Ctrl+F5**, o aggiungete qualcosa in fondo all'indirizzo (`…/avvio-diagnostica.txt?x=1`). Il modo
> sicuro di leggerlo è guardare **l'ora nella prima riga**: se non è cambiata, state leggendo la copia
> vecchia — non c'è nessun guasto da cercare.
