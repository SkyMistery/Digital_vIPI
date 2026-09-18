# Pacchetto 1.33.0 — solo i file cambiati

> **Timbro:** `1.33.0 · 7d25267` (18 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.32.0** (`c1eddc9`, online dal 17 settembre). **MINOR, UNA migrazione ADDITIVA** (`PonteRfo`: due tabelle
> NUOVE, `rfo_shared_state` e `rfo_shared_state_history`; nessuna tabella esistente toccata). Si consegna via FTP; la
> migrazione parte da sola all'avvio. **18 file**: 15 in radice, 3 in `wwwroot/_content/Vipi.Ui/`.
>
> 🔴 **Serve PRIMA dell'evento RFO di LIRN del 19 settembre**, e serve anche **un file di segreti nuovo** (sotto).

---

## Prima di caricare

⚠️ **Scaricare una copia del database** dalla Diagnostica (`services/vsop/admin/diagnostics` → copia del database) e
tenerla. La migrazione crea soltanto due tabelle nuove e vuote: provata su una copia fresca della produzione.

## Che cos'è

1. **Ponte RFO Gate Manager** (§A72). Le copie di «RFO Gate Manager» delle postazioni ATC di un evento RFO tengono
   sincronizzato un documento per evento passando dal sito:
   `https://atc.it.ivao.aero/api/rfo/events/{eventId}/state` (GET e PUT, chiave nell'header `x-api-key`).
   Il sito non interpreta il contenuto; ogni scrittura riuscita resta anche in `rfo_shared_state_history`.
2. **«Mai usare» come chip rosse** (§A69) nella tabella piste degli editor, al posto delle caselle.

## Il file dei segreti — NUOVO, senza non entra nessuno

Le chiavi del ponte **non stanno nel pacchetto**: vanno in un file `.json` nuovo dentro **`public_atc/segreti/`**
(la stessa cartella dell'altro file dei segreti), con un nome a scelta non indovinabile. Contenuto:

```json
{
  "Rfo": {
    "Chiavi": {
      "lirn-20260919": {
        "Chiave": "<la chiave data in chat, 47 caratteri, comincia con rfo_>",
        "Eventi": "lirn-20260919, prova-ponte-rfo"
      }
    }
  }
}
```

- `Eventi`: gli eventi che la chiave apre, separati da virgola; `*` = tutti. `prova-ponte-rfo` serve alla prova da fuori.
- ⚠️ Si può mettere anche **dentro** il file dei segreti che c'è già, come sezione in più: basta che il JSON resti valido.
  Un file nuovo è più semplice da togliere dopo l'evento.
- Una chiave sotto i 32 caratteri **non apre niente**, di proposito.
- La chiave **non va in nessun file che si carica col pacchetto** e non va nel repo.

## I 18 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-theme.css
wwwroot/_content/Vipi.Ui/vipi-theme.css.br
wwwroot/_content/Vipi.Ui/vipi-theme.css.gz
Vipi.Host.staticwebassets.endpoints.json
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si carica il **file dei segreti** in `segreti/`;
2. si caricano **tutti** i 18 file col nome finto;
3. si rinominano nell'ordine qui sopra: prima i tre file di `wwwroot` **e** `endpoints.json` (viaggiano insieme), poi ogni
   `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
4. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

🔴 **`Vipi.Infrastructure.MySqlMigrations.dll` è il file da non dimenticare**: senza, le tabelle non nascono e il ponte
risponde 500 alla prima chiamata. Il controllo che lo prova è `Schema = 0` qui sotto.
🔴 **`Vipi.Hosting.dll` questa volta C'È** (l'endpoint vive lì): senza, `/api/rfo/…` non esiste.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts.dll`, `Vipi.AuroraProfiles.dll` (impronte diverse solo per
ricompilazione), `en/Vipi.Ui.resources.dll` (nessuna frase cambiata), il resto di `wwwroot`, `Vipi.Host.deps.json` e
`Vipi.Host.runtimeconfig.json`: identici a 1.32.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.33.0 · 7d25267`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`** — dice che le tabelle nuove ci sono;
- dopo Ctrl+F5, un editor d'aeroporto → Piste: colonna **Mai usare** a chip rosse.

**Il ponte, da fuori** — sull'evento di **prova**, mai su `lirn-20260919` (le postazioni leggerebbero come vere le
decisioni finte lasciate dalla prova). Con la chiave in `$KEY`:

```bash
URL=https://atc.it.ivao.aero/api/rfo/events/prova-ponte-rfo/state
curl -i -H "x-api-key: $KEY" "$URL"                                        # 404 la prima volta
curl -i -X PUT -H "x-api-key: $KEY" -H 'If-Match: "0"' -H "Content-Type: application/json" \
     -d '{"updatedBy":"TEST","data":{"pins":{"A":"14"}}}' "$URL"            # 200, "version":1, ETag "1"
curl -i -H "x-api-key: $KEY" -H 'If-None-Match: "1"' "$URL"                 # 304
curl -i -X PUT -H "x-api-key: $KEY" -H 'If-Match: "0"' -H "Content-Type: application/json" \
     -d '{"updatedBy":"TEST","data":{"pins":{"B":"22"}}}' "$URL"            # 409 con la busta alla versione 1
curl -i -X PUT -H "x-api-key: $KEY" -H "Content-Type: application/json" -d '{"data":{}}' "$URL"   # 428
curl -i "$URL"                                                              # 401
curl -i -H "x-api-key: $KEY" https://atc.it.ivao.aero/api/rfo/events/lirn-20260919/state   # 404 (mai scritto)
```

⚠️ Se una di queste risponde **403 con una pagina HTML** (e non un 403 vuoto), è **Cloudflare** che sfida chi non è un
browser: le postazioni resterebbero col chip rosso. Va aperta un'eccezione per `/api/rfo/*` prima dell'evento.

Da fuori, per chi verifica il resto del sito:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella su una copia **fresca** della produzione (17-set) in MariaDB 11.4.10 senza
la migrazione: `Applying migration '20260918085406_PonteRfo'` all'avvio, timbro `1.33.0 · commit 7d25267`,
`pacchetto-verifica.js` **10/10**. Sul ponte: i curl del contratto esatti (404, 200 v1 ETag "1", 304, 409 con la busta
v1, 428, 401) e **30 coppie di PUT lanciate in parallelo con lo stesso `If-Match`: 30 volte un 200 e un 409, mai due
200**. `avvisi-log.txt` non è nato (nessun avviso).
⚠️ Un primo 1.33.0 (`f43229b`) non è mai uscito da qui: la stessa prova aveva mostrato un ERRORE di EF in
`avvisi-log.txt` sul 409 di una creazione (passo 4), corretto in `7d25267`. È in
`artifacts/publish_old/20260918a-non-spedito/`.
