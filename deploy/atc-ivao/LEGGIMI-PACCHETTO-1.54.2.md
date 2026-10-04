# Pacchetto 1.54.2 — solo i file cambiati

> **Timbro:** `1.54.2 · 1a843c2` (30 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.54.1** (`cab7337`, online dal 30 settembre). È una **PATCH senza migrazioni**: solo interfaccia. Si
> consegna da sola via FTP, il database non cambia, e **il rollback a due rinomine resta valido**. Nessun segreto
> nuovo, nessuna configurazione da toccare.
> **9 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 5 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S81 — vista live**: la finestra **Trasferimenti** si apre solo allo **staff di divisione**; per gli altri resta
  una riga con l'etichetta «In sviluppo», che non si apre.

## I 9 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (5)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** tutti gli altri assiemi (Domain, Application, Infrastructure, MySqlMigrations, Hosting, Aurora*:
sorgente invariato, cambiati solo per la ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il
resto di `wwwroot`: identici a 1.54.1, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.54.2 · 1a843c2`** nel piè di pagina; in Diagnostica `Schema` resta **`0`**;
- vista live: da staff di divisione la finestra Trasferimenti si apre; da membro normale c'è solo «In sviluppo»;
- in inglese le frasi nuove sono tradotte (prova che `en/` è arrivato);
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti su LIRE/LIBG, che aspettano il SOD.
