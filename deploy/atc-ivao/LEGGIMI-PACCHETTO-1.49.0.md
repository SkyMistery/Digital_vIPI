# Pacchetto 1.49.0 — solo i file cambiati

> **Timbro:** `1.49.0 · 89bfb04` (29 settembre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.48.0** (`e292a1e`, online dal 29 settembre). È una **MINOR con UNA migrazione** (`EntiGruppiAcc`: un
> indice composto al posto di quello su `AccId`; niente tabelle né colonne tolte o rinominate). Si consegna da sola via
> FTP, il database si aggiorna da sé all'avvio, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo,
> nessuna configurazione da toccare.
> **15 file**: 1 in **`en/`** e 14 in **radice**. Niente in `wwwroot`.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **Pagina «Enti ATC»** in amministrazione (`/services/vsop/admin/units`): gli enti e le loro posizioni in un posto
  solo.
- **«Sostituisci con…»** dagli orfani della Struttura: quando IVAO toglie una posizione e ne crea una nuova al suo
  posto, tutto (accordi, blocchi, figli, documento, gruppi APP) passa alla nuova in un gesto.
- **Enti anche per i gruppi APP della vIPI ACC.**

## I 15 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (14)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta la migrazione
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (sorgente invariato), `deps.json`,
`runtimeconfig.json`, `appsettings.json`, **tutto `wwwroot`** e `Vipi.Host.staticwebassets.endpoints.json`: identici a
1.48.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti**, non solo la riga «N risultati».

- col login da staff: il timbro **`1.49.0 · 89bfb04`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`);
- `https://atc.it.ivao.aero/services/vsop/admin/units` si apre e mostra gli enti;
- in inglese, negli orfani della Struttura, il tasto **«Replace with…»** (prova che `en/` è arrivato);
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti di 1.48.0 non ancora fatti (LIRE_APP → ente con LIRE_TWR principale; ripubblicare vIPI/vSOP di LIBG e LIRE
dopo il primo import) e quelli di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md).
