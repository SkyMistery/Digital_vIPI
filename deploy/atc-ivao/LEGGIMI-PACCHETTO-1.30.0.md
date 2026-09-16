# Pacchetto 1.30.0 — solo i file cambiati

> **Timbro:** `1.30.0 · d083a15` (16 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.29.0** (`49a2dd5`, online dal 16 settembre). **MINOR, con UNA migrazione che TOGLIE una colonna.**
> Il pacchetto si consegna da solo via FTP: la migrazione la applica il sito al primo avvio.
> **15 file**: 14 in **radice**, 1 in **`en/`**. Nessun file di `wwwroot`.

---

# 🔴 PRIMA DI CARICARE: scaricare una copia del database

È la prima migrazione **distruttiva** dopo il 16 settembre: toglie la colonna `Airports.IsMilitaryOnly`, in
pensione dall'11 settembre (il suo posto l'ha preso la categoria dello scalo).

- **Perché è sicura:** misurato sulla copia di produzione di oggi, la colonna diceva esattamente quel che dice la
  categoria su tutti i 93 aeroporti, e nessuna riga era rimasta da travasare. La migrazione è stata applicata su
  quella stessa copia, ripristinata in un MariaDB 11.4.10 identico al vostro, dal pacchetto che state caricando.
- **Perché serve comunque la copia:** una volta tolta la colonna, **1.29.0 non riparte** su questo database —
  la legge. Se si dovesse tornare indietro, si torna indietro **con la copia**, non col solo codice.

Dalla Diagnostica (`services/vsop/admin/diagnostics`) → **«Scarica la copia»**, poi sul proprio computer:

```
dotnet run --project tools/Vipi.DbBackup -- verifica <file scaricato>
```

deve dire **INTERA** e `Versione del sito . 1.29.0 · 49a2dd5`. Solo dopo si carica.

---

## Che cos'è

- **Via la colonna in pensione `Airports.IsMilitaryOnly`** (§A49). A schermo non cambia niente: la categoria
  dello scalo la sostituiva già da 1.22.0.
- **La forma dei settori, prima fase del trasloco** (§A50). La forma di un settore (il poligono e le sue quote)
  oggi sta in colonne del catalogo; il suo posto definitivo è la tabella dei **pezzi di forma**, che sa tenere
  più zone con bande diverse. Questa consegna **copia** le colonne nei pezzi — a ogni salvataggio e a ogni avvio —
  ma tutte le pagine continuano a leggere le colonne: **a schermo non cambia niente**, apposta.
  - Al primo avvio il log dice «Allineati i pezzi di forma di **N** settori»: sulla copia di oggi erano **280**.
    Ai riavvii successivi la riga non compare più.
  - In Diagnostica nasce un rilievo nuovo, **«Pezzi di forma disallineati»**: deve restare **assente**. È la
  condizione per la seconda fase (le pagine che leggono i pezzi), che arriverà in un pacchetto successivo.

## I 15 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll   ← 🔴 porta la migrazione: senza, il sito non parte
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinomina nell'ordine qui sopra: ogni `.pdb` e il suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Sovrascrivere un `.dll` mentre
l'applicazione gira lo tronca sotto il processo. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

🔴 **Il file che sembra di troppo e non lo è: `Vipi.Domain.dll`.** L'aeroporto ha perso un campo, e gli altri
assiemi sono compilati contro la versione nuova. E come sempre **`Vipi.Host.dll`** porta il timbro.

⚠️ **Restano fuori** `Vipi.AuroraProfiles.dll` e `Vipi.AuroraBridge.Contracts.dll` (codice non cambiato), tutto
`wwwroot` e `Vipi.Host.staticwebassets.endpoints.json` (identici a 1.29.0, controllato per impronta), e
`Vipi.Host.deps.json` / `Vipi.Host.runtimeconfig.json`.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.30.0 · d083a15`**;
- `services/vsop/admin/diagnostics`: riga **`Schema` = `0`** — è la prova che la migrazione è passata (se il
  `.dll` delle migrazioni non fosse arrivato, il sito non partirebbe o quel numero non sarebbe zero);
- nella stessa pagina, **nessun rilievo «Pezzi di forma disallineati»**. ▶ Da riguardare **dopo qualche giorno**
  di import veri: è la condizione per la fase successiva.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella su una copia **fresca** della produzione di oggi, ripristinata in
MariaDB 11.4.10: migrazione applicata (la colonna non c'è più), 280 settori allineati (264 forme
dell'anagrafica, 16 cerchi di ripiego, i 13 pezzi dell'AIP intatti), timbro `1.30.0 · commit d083a15`,
`pacchetto-verifica.js` **10/10** (Ricerca e editor compresi), Diagnostica con `Schema 0`, `Avvio 0` e nessun
disallineamento.

---

## Se qualcosa va storto

- **Il sito non riparte dopo il carico:** la causa più probabile è un file non arrivato o non rinominato —
  in particolare `Vipi.Infrastructure.MySqlMigrations.dll` o `Vipi.Domain.dll`. `diagnostica/avvio-errore.txt`
  lo dice.
- **Tornare a 1.29.0:** non basta ricaricare i file vecchi, perché la colonna non c'è più. Si ripristina la copia
  scaricata prima del carico (in un database **vuoto**, col sito alla versione **1.29.0**), come descritto nel
  foglio di 1.29.0.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Il pacchetto è parziale e non può aggiornarlo. Lo sistemerà il passaggio a **.NET 10**, che ora sarà **1.31.0**
(carico completo): il numero 1.30.0 lo prende questo pacchetto.

# Il pannello

- `passenger_min_instances` **non si può avere** (risposta di Ivao.It, 16 settembre). Il processo continua a
  fermarsi pochi secondi dopo l'ultima richiesta; il sito è fatto per sopportarlo.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).
- ⚠️ Il server risulta **Debian 11**, fuori dal supporto LTS dal 31 agosto 2026.
