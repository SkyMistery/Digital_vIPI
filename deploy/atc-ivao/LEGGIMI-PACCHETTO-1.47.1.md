# Pacchetto 1.47.1 — solo i file cambiati

> **Timbro:** `1.47.1 · c41e6e7` (29 settembre 2026). Lo vedono gli amministratori nella barra in alto; se la barra
> è stretta sta nella riga `Versione` della **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.47.0** (`067a737`, online dal 29 settembre). È una **PATCH, NESSUNA migrazione**: si consegna da sola
> via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **13 file**: 6 in **`wwwroot/_content/Vipi.Ui/`** e 7 in **radice**. Niente in `en/` (nessuna frase nuova).
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina. La procedura è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **`/vsop/health` torna `Healthy`.** L'unico errore era il rilievo «Trasferimento senza ripiego» su LIMM
  (`LIMM_WS2_CTR` → `LSAG_TST_CTR`): il controllo contava come ripiego un settore di Ginevra. Scelta del committente:
  Ginevra gestisce lo spazio svizzero, WS2 quello italiano più Lugano — il rilievo non conta più i settori di un ACC
  estero.
- **Login IVAO al primo accesso.** Quando la pagina di consenso di IVAO perde il nonce, il sito rifà il giro una
  volta da solo invece di mostrare «The sign-in expired along the way» (come l'hub, PR 174).
- **Sei punti della vSOP**: «Cosa è cambiato» nella sezione Staff; «Nessun documento» sbagliato nell'elenco MIL;
  barra in alto con i tasti della stessa altezza e la lente che porta alla ricerca; Documenti collegati che non
  puntano più a pagine chiuse; Guida diversa per ruolo.

## I 13 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`wwwroot/_content/Vipi.Ui/` (6)**: `vipi-theme.css`, `vipi-ui.js`, ognuno **con il suo `.br` e `.gz`**;
2. **in radice, subito dopo**: `Vipi.Host.staticwebassets.endpoints.json` — ⚠️ viaggia **insieme** a `wwwroot`;
3. **in radice (6)**, ogni `.pdb` col suo `.dll`:

```
Vipi.Application.pdb   Vipi.Application.dll
Vipi.Ui.pdb            Vipi.Ui.dll
Vipi.Host.pdb          Vipi.Host.dll      ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**.

⚠️ **Restano fuori** gli altri assiemi (Domain, Infrastructure, MySqlMigrations, Hosting, AuroraBridge.Contracts,
AuroraProfiles: sorgente invariato, cambia solo l'impronta della ricompilazione), `en/`, `deps.json`,
`runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.47.0.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati».

- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`** (non più `Degraded`);
- col login: il timbro **`1.47.1 · c41e6e7`** e `Schema` = `0`;
- la barra in alto: tasti della stessa altezza, la lente apre la ricerca;
- **login col VID 704798** (consenso a IVAO revocato apposta il 29-set): deve entrare **al primo clic**; nel registro
  dei login «Secondo giro: False» e «si riparte una volta». ⚠️ Non entrare in produzione con quel VID PRIMA del
  caricamento: brucerebbe la prova.

## Restano (dal foglio 1.47.0)

I gesti del committente di [`LEGGIMI-PACCHETTO-1.47.0.md`](LEGGIMI-PACCHETTO-1.47.0.md) non ancora fatti; in più:
**Azure Translator risponde 401 dal 27-set 09:18Z** — è la chiave nei segreti, non il codice (memoria
`azure-endpoint-della-risorsa`: endpoint della risorsa, prova con curl prima).
