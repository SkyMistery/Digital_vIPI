# Pacchetto 1.42.0 — solo i file cambiati

> **Timbro:** `1.42.0 · c1aaf4b` (21 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.41.1** (`bd668a7`, online dal 21 settembre). **MINOR, NESSUNA migrazione**: niente database, niente
> segreti nuovi, nessuna configurazione da toccare. Si consegna via FTP.
> **7 file**: 6 in **radice** e 1 in **`en/`**. Nessun file di `wwwroot`: `endpoints.json` resta com'è.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

**Il convertitore di coordinate legge i limiti laterali dell'AIP** (Aurora Sector Lab, fase F1). In
`/services/coordinates` si incolla il testo come sta nel PDF — in inglese, in italiano o nella forma bilingue di
ENR 2.1.1.1 — e il convertitore riconosce archi, cerchi e «point of origin»:

- l'**arco** diventa vertici lungo il cerchio e passa esattamente per i due punti dichiarati; il **centro** non è
  più un vertice (prima lo era, e l'area usciva storta);
- il **cerchio** (*circular area … within a 1.0 NM radius*) diventa un anello, col raggio in NM, M o KM;
- **densità degli archi** (0,5–10 punti per grado, 1 di base), che compare solo se ci sono archi o cerchi;
- una riga di **conto** (archi, cerchi, punti) e, sulla mappa, le **crocette dei centri** (spente di base);
- cinque **segnalazioni nuove**, in italiano e inglese: raggio incoerente, tratto non disegnabile (confine,
  costa, fiume), parole non riconosciute, arco incompleto, troppi punti;
- e una correzione che vale per tutti i testi: l'emisfero **staccato** (`24" N`) non diventa più il nome
  dell'area.

La Guida ha il paragrafo «Un'area dall'AIP, archi compresi», e la ricerca lo trova con «arco», «aip», «raggio».

## I 7 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `en/` (1)**, da caricare e rinominare **per primo**:

```
en/Vipi.Ui.resources.dll
```

**In radice (6)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinominano nell'ordine qui sopra: prima `en/`, poi ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per
   ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** gli altri assiemi, `deps.json`, `runtimeconfig.json`, `appsettings*.json`,
`Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: identici a 1.41.1, o diversi solo per il timbro
di compilazione su sorgente fermo — controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore (serve lo staff di divisione):

- il timbro **`1.42.0 · c1aaf4b`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- `services/coordinates`: incollare

  ```
  44°51'24" N 008°14'57" E
  then arc of circle in clockwise direction radius 17 NM centred on
  44°55'29" N 007°51'43" E
  till point
  44°41'08" N 008°04'34" E
  ```

  deve comparire il campo **«Densità degli archi»**, la riga **«Archi convertiti: 1 · cerchi: 0 · punti: 45.»**,
  **nessuna** segnalazione, e sulla mappa un arco (non un triangolo). Con «Mostra i centri di archi e cerchi» la
  crocetta compare a nord-ovest.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico

Niente da ripubblicare. ⚠️ Da dire agli AOD, non al sito: in `italy.restrict` l'arco di **R47** (Rieti) arriva a
12,51 NM dal centro, l'AIP dice 20 km = 10,80 NM (carta F1 §10.2).
