# Pacchetto 1.26.0 — solo i file cambiati

> **Timbro:** `1.26.0 · cad6698` (13 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **CUMULATIVO rispetto a 1.25.2**: contiene tutto 1.25.4 (e quindi 1.25.3). Si carica **sia che 1.25.4 sia
> già su, sia che no**: se 1.25.4 non è ancora stata caricata, **si salta** e si carica direttamente questo.
> **15 file**: 14 in **radice** e 1 nella sottocartella **`en/`**. Niente `wwwroot`.
>
> ⚠️ **Due migrazioni ADDITIVE**, che partono da sole all'avvio. Per questo è una MINOR e per questo il file
> da non dimenticare è **`Vipi.Infrastructure.MySqlMigrations.dll`**: senza, il sito parte con un modello che
> si aspetta una tabella che non c'è.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- 🔴 **Le API del sito non sono più anonime** (decisione del committente). Nasce la pagina **Chiavi API**
  (`/services/vsop/admin/api-keys`), visibile solo a **IT-DIR, IT-ADIR, IT-WM, IT-AWM** e ai fondatori: si
  crea una chiave per ogni programma che legge l'archivio ATC o usa il bridge Aurora, la si copia (si vede una
  volta sola) e la si revoca quando serve.
  ✅ **Per ora non si ferma nessuno**: l'archivio `/vsop/api/v1/atc/sessions` risponde ancora a chi non porta
  una chiave (`Api:RichiediChiave` spento). Si chiude dopo, con una riga di configurazione, quando il
  validatore dei tour avrà la sua chiave.
- **Ricerca e «cambiati»** leggono la release pubblicata in vigore, non più il lavoro in corso.
- **Gli import dalle sorgenti IVAO** che falliscono a metà non cancellano più dati e non si segnano come fatti.
- **Etichette delle clausole degli accordi** fino a 500 caratteri (prima 80/200: il testo lungo veniva rifiutato).
- Più tutto **1.25.4**: chi perde un incarico IVAO perde il livello entro quattro ore, la lingua delle pagine
  pubbliche non passa da un lettore all'altro, 404 veri, cookie solo HTTPS, tetti dei servizi separati.
- Più tutto **1.25.3**: la correzione di sicurezza sulle pagine pubbliche, ed **eliminare riservato agli
  amministratori**.

### Le due migrazioni

| | Cosa fa sul database |
|---|---|
| `20260913110951_EtichetteClausolePiuLarghe` | allarga cinque colonne di `AgreementClauses` (a `varchar(500)`). Solo allargamento: stessa nullabilità, stessa collation. La tabella ha poche decine di righe |
| `20260913122752_ChiaviApi` | crea la tabella nuova `ApiClients` e un indice unico. Non tocca nient'altro |

## I 15 file, e l'ordine

```
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto (quello di `en/` dentro la cartella `en/`);
2. si rinomina nell'ordine qui sopra: prima ogni `.pdb`, poi il suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge. La
   prima apertura può metterci qualche secondo in più: le migrazioni girano lì.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.26.0 · cad6698`**;
- `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`** (zero migrazioni in sospeso);
- chi è IT-DIR/ADIR/WM/AWM vede la voce **Chiavi API** nella barra admin; gli altri amministratori no.

E da fuori, tre `curl` in sola lettura:

```sh
# l'archivio senza chiave risponde ancora (passaggio): 200
curl -s -o /dev/null -w "%{http_code}\n" "https://atc.it.ivao.aero/vsop/api/v1/atc/sessions?limit=1"

# con una chiave inventata: 401 (la porta nuova è arrivata)
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer vipi_inventata" "https://atc.it.ivao.aero/vsop/api/v1/atc/sessions?limit=1"

# un ACC inventato: 404 (da 1.25.4)
curl -s -o /dev/null -w "%{http_code}\n" https://atc.it.ivao.aero/services/vsop/xx-inventato
```

---

# Dopo: chiudere le API, senza fermare nessuno

1. Dalla pagina **Chiavi API** si crea la chiave del **validatore dei tour** (API: *Archivio ATC*) e la si
   consegna a chi lo gestisce. Deve mandarla come `Authorization: Bearer vipi_…` oppure `X-Api-Key: vipi_…`.
2. Si aspetta che nella pagina, accanto a quella chiave, compaia **«ultimo uso»**.
3. Solo allora, nella configurazione del sito: **`Api__RichiediChiave=true`** (variabile d'ambiente, o
   `"Api": { "RichiediChiave": true }` nel file dei segreti). Riavvio. Da lì chi non porta la chiave riceve 401.

Il bridge Aurora, se un giorno si accende, chiede la chiave da subito.

# Il pannello: come da 1.25.4

- ✅ **Le direttive nginx** per i file statici.
- ✅ **`passenger_min_instances ≥ 1`**.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).
