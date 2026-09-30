# Pacchetto 1.53.0 — solo i file cambiati

> **Timbro:** `1.53.0 · d4bbebd` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.52.0** (`1a72e24`, online dal 30 settembre). È una **MINOR senza migrazioni**: si consegna da sola via
> FTP, il database non cambia, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo, nessuna
> configurazione da toccare.
> **11 file**: 1 in **`en/`** e 10 in **radice**. Niente in `wwwroot`.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S64 — API degli aeroporti** per gli altri programmi: `GET /vsop/api/v1/airports` (elenco), `/{icao}` (scheda),
  `/{icao}/sids` e `/{icao}/stars` (anche `?runway=`). È la vista pubblica del documento, dietro **chiave API
  obbligatoria** col permesso nuovo **«Aeroporti»** (pagina Chiavi API dell'Admin). Header `X-Api-Key: vipi_…` oppure
  `Authorization: Bearer vipi_…`.
- Il nome dei **vSOP militari** perde «MIL» per intero (si vedeva anche nella tendina del vAWOS).

## Gli 11 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (10)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Hosting.pdb                        Vipi.Hosting.dll                         ← le API nuove
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.AuroraBridge.Contracts`,
`Vipi.AuroraProfiles` (sorgente invariato, cambiati solo per la ricompilazione), `deps.json`, `runtimeconfig.json`,
`appsettings.json`, `Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.52.0, controllato per
impronta e col `git diff`. Le `const` nuove (`ApiEndpoints.Aeroporti`, la radice delle API) sono usate solo da
`Vipi.Hosting`, che è dentro.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.53.0 · d4bbebd`** nel piè di pagina; in Diagnostica `Schema` resta **`0`**;
- `https://atc.it.ivao.aero/vsop/api/v1/airports` **senza chiave** risponde **401** (prova che `Vipi.Hosting` è
  arrivato: con la 1.52.0 era 404);
- nella pagina Chiavi API dell'Admin compare il permesso **«Aeroporti»**; con una chiave che l'ha, la stessa URL
  risponde 200 con l'elenco degli scali;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti di 1.48.0 su LIRE/LIBG (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE),
che aspettano il SOD.
