# Pacchetto 1.42.1 — solo i file cambiati

> **Timbro:** `1.42.1 · a66f25e` (22 settembre 2026). Lo vedono gli amministratori nella barra in alto. Se la
> barra è stretta, sta nella riga `Versione` della **Diagnostica** e in quella di
> `diagnostica/avvio-diagnostica.txt`.

> **Su 1.42.0** (`c1aaf4b`, online dal 21 settembre). **PATCH, NESSUNA migrazione**: niente database, niente
> segreti nuovi, nessuna configurazione da toccare. Si consegna via FTP.
> **4 file, tutti in radice.** Nessun file di `wwwroot`.
>
> ⚠️ **Il caricamento si fa come sempre**: si carica col **nome finto** e poi si **rinomina**. Se si sovrascrive
> un `.dll` mentre l'applicazione gira, il file si tronca sotto il processo e il processo muore subito. La
> procedura completa è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

- **I chief d'ACC entrano nell'elenco degli staffisti.** Finora l'elenco accoglieva solo i codici `IT-…`, e i
  chief d'ACC hanno il codice dell'ACC (`LIBB-CH`, `LIPP-CH`): potevano già modificare (erano Redattori), ma
  non comparivano in Diagnostica («Chi può editare»), nel selettore dei permessi e negli altri elenchi dello
  staff. Visto il 22 settembre con due chief appena nominati.
- Entrano anche i codici d'ACC che non danno permessi (per esempio `LIRR-CHA1`): compaiono nell'elenco e si
  possono **promuovere a mano** dal selettore dei permessi. I codici del quartier generale IVAO (per esempio
  `HPM`) restano fuori.
- Non c'è niente da migrare: ognuno compare **al primo accesso** dopo il carico (al più cinque minuti dopo,
  se era già dentro).

## I 4 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In radice (4)**, in quest'ordine:

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra, ogni `.pdb` col suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. si riavvia: `tmp/restart.txt` **e poi si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Restano fuori** `en/Vipi.Ui.resources.dll` (nessuna frase cambiata), gli altri assiemi, `deps.json`,
`runtimeconfig.json`, `appsettings*.json`, `Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot`: sono
identici a 1.42.0, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro **`1.42.1 · a66f25e`**, e in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**;
- quando i due chief nuovi hanno riaperto il sito da loggati, in «Chi può editare» compaiono con livello
  **Redattore**.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico

Niente da ripubblicare. Chi ha solo un codice d'ACC senza permessi (`…-CHA1`) si promuove a mano da
`services/vsop/admin/permissions`, se deve modificare.
