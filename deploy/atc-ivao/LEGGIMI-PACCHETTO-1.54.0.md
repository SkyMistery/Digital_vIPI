# Pacchetto 1.54.0 — solo i file cambiati

> **Timbro:** `1.54.0 · 919b965` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.53.0** (`d4bbebd`, online dal 30 settembre). È una **MINOR con TRE migrazioni ADDITIVE**
> (`RegistroAccessi`: tabella `AccessiAlSito`; `NomeBreveAccessi`: una colonna; `PaginaDelleRichieste`: colonna
> `FieldRequests.PageUrl`; niente tolto o rinominato). Si consegna da sola via FTP, il database si aggiorna da sé
> all'avvio, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo, nessuna configurazione da toccare.
> **22 file**: 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`** e 15 in **radice**.
>
> 🔴 **DA QUESTA VERSIONE IL SITO SI LEGGE SOLO DOPO IL LOGIN IVAO.** Senza login si vede solo la porta di
> `/services` con «Entra con IVAO» (e la pagina dei cookie); vIPI, vSOP, Ricerca e il resto chiedono il login, con
> qualunque account IVAO. Restano aperti il login, le sonde (`/vsop/health`), le API con chiave (`/vsop/api/…`) e il
> ponte RFO (`/api/rfo/…`). I link già condivisi portano alla porta. **Per riaprire** senza ricaricare niente:
> variabile d'ambiente `VipiAuth__LoginObbligatorio=false` e riavvio.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S65 — vista live**: compaiono i campi che hanno solo il vSOP; uno scalo non risulta più «delegato» a chi lo
  guarda (il caso LIMF_WW0_APP); il pannello rapido mostra le SID del documento pubblicato.
- **S66 — pagina Chiavi API**: la sezione «Indirizzi delle API» per chi integra; le spunte dei permessi al loro posto.
- **S67 — login obbligatorio** (vedi il riquadro rosso qui sopra).
- **S68 — registro degli accessi**: chi entra nel sito, conservato 12 mesi, visibile solo agli admin in
  **`/services/stats/logins`**; nome breve («Mario R.») per le classifiche; informativa `/services/cookies` aggiornata
  e leggibile senza login.
- **S70 — «I miei dati»** (`/services/my-data`): ogni membro vede che cosa il sito tiene di lui e da dove chiederne
  la cancellazione (link nel piè di pagina e dall'informativa).
- **S71 — titolo di un documento** si cambia dall'elenco Documenti, tasto «Titolo» (almeno Editor, nessun lock
  altrui, finisce nell'audit).
- **S72** nuova veste delle statistiche · **S73** pagina dell'ACC al buio sistemata · **S74** il riassunto mensile ATC
  si conserva dieci anni · **S75** Ricerca senza pezzi di JSON negli estratti e in ordine di importanza (Guida in coda)
  · **S76** scheda «Prenotazioni ATC e FRA» in `/services` verso `https://atc.ivao.aero/` · **S77** bandierina
  «Segnala un problema su questa pagina» in barra e nel ☰, con la pagina salvata nella richiesta.
- **S78** — le richieste dal campo **aperte** per VID salgono da 5 a **10**.
- **S79 — disconnessioni**: il browser segnala quando la connessione cade (file `diagnostica/disconnessioni-*.tsv`,
  scheda nuova in **Diagnostica**); sulle pagine statiche niente riquadro di riconnessione ma un avviso discreto; alla
  ricarica si torna al punto di lettura.
- (S69: solo test.)

## I 22 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (6)**:

```
vipi-riconnessione.js   vipi-riconnessione.js.br   vipi-riconnessione.js.gz    ← avvia la pagina
vipi-theme.css          vipi-theme.css.br          vipi-theme.css.gz
```

3. **in radice (15)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta le tre migrazioni
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro, cancello)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato, cambiati solo per la
ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.53.0,
controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa**: il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.54.0 · 919b965`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- **da una finestra anonima**: `https://atc.it.ivao.aero/services` mostra solo «Entra con IVAO»;
  `https://atc.it.ivao.aero/services/vsop/search` non si apre senza login; `/services/cookies` si apre;
- col login: `/services/my-data` mostra la vostra riga; da admin `/services/stats/logins` elenca l'accesso appena
  fatto; la bandierina «Segnala» in barra;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**; `/vsop/api/v1/airports` senza chiave **401**.

## Restano

- Correggere il titolo di vIPI e vSOP MIL di **LIML** («MIlano Linate») col tasto «Titolo», poi **ripubblicarli**:
  pagina pubblica e Ricerca mostrano il titolo della release.
- I gesti di 1.48.0 su LIRE/LIBG, che aspettano il SOD.
