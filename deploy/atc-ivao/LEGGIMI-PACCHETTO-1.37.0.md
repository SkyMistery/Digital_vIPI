# Pacchetto 1.37.0 — solo i file cambiati

> **Timbro:** `1.37.0 · 2ad1790` (21 settembre 2026). È quel che compare nella barra in alto agli
> amministratori — se la barra è stretta, nella riga `Versione` della **Diagnostica** — e nella riga
> `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.36.0** (`0493ef5`, online dal 21 settembre). **MINOR, NESSUNA migrazione**: il pacchetto si
> consegna da solo via FTP, il database non si tocca. Nessun segreto nuovo, nessuna configurazione da toccare.
> **14 file**: 7 in **radice**, 1 in **`en/`**, 6 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ✅ **Nessuna migrazione**, quindi la copia di sicurezza del database non è obbligatoria (resta buona
> abitudine) e `Vipi.Infrastructure.MySqlMigrations.dll` **non** è nel pacchetto.
>
> ⚠️ **I file di `wwwroot` viaggiano insieme** a `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con
> che nome il sito chiede ogni file. Caricarne uno senza l'altro fa chiedere nomi che non esistono.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

Cinque cose segnalate dal campo dopo 1.36.0.

- **Le linguette «2D map / 3D view» della AoR non nascono più nude.** Ogni tanto, aprendo un documento,
  comparivano come due bottoni grigi invece che come le due linguette: il loro stile arrivava in un foglio
  caricato **dopo** il primo disegno. Ora sta nel foglio principale.
- **La Diagnostica a fisarmonica.** Le quattro schede di destra — *Chi può editare*, *Documenti da rivedere*,
  *Copia del database*, *Immagini dei documenti* — si aprono e si chiudono, e **aprirne una chiude le altre**.
  Il numero accanto al titolo resta visibile anche a scheda chiusa.
- **«Re-import from IVAO» importa anche le procedure.** Il tasto dice «tutto ciò che questo aeroporto prende
  dalla sorgente», ma faceva solo piste e settori: le SID e le STAR arrivavano solo dal tasto dentro la
  tabella delle SID, che sugli arrivi non c'è. Ora porta anche SID e STAR, e l'esito lo dice. (LICB, per
  esempio, ha quattro STAR alla sorgente.) Sulla tabella degli arrivi il tasto «aggiungi» dice ora
  **«+ STAR»** e non più «+ SID».
- **«Cita» sa citare il codice di una postazione.** Accanto alla chip **ATC**, che scrive il nominativo
  (`[[ATC LIRR_NE]]` → *Roma Radar*), c'è la chip **POS**, che scrive il codice (`[[POS LIRR_NE]]` →
  *LIRR_NE*). Tutti e due avvisano in testata all'editor se la postazione sparisce dal catalogo. Le chip del
  selettore sono ora sette.
- **La Diagnostica dice quanto ci mette.** Accanto all'ora del controllo compaiono quattro tempi —
  `controlli · admin · impatti · giri`. Servono a capire **dove** sta la lentezza di quella pagina, che il
  registro conferma (circa 3,8 secondi contro i 0,3 delle altre): da qui non si poteva misurare. E la tabella
  «Chi può editare» non si calcola più due volte a ogni apertura.

## I 14 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (6)** — si caricano e rinominano **per primi**, tutti e sei:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
vipi-aor3d.css   vipi-aor3d.css.br   vipi-aor3d.css.gz
```

**In radice e in `en/` (8)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto, ognuno nella sua cartella;
2. si rinomina nell'ordine qui sopra: prima `wwwroot` e l'indice, poi ogni `.pdb` e il suo `.dll`,
   `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`,
`Vipi.Hosting`, `Vipi.AuroraProfiles` e `Vipi.AuroraBridge.Contracts`: il loro codice non è cambiato e
differiscono solo per ricompilazione (controllato progetto per progetto che nessuno nomini un tipo cambiato).
Fuori anche `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a 1.36.0,
controllato per impronta — 466 file confrontati, 440 identici.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore, **dopo Ctrl+F5** (il foglio di stile è cambiato):

- il timbro **`1.37.0 · 2ad1790`**, e `services/vsop/admin/diagnostics` con la riga **`Schema` = `0`**;
- nella stessa Diagnostica: le quattro schede a destra si piegano, e **accanto all'ora ci sono i quattro
  tempi**. 🔴 **Quei quattro numeri, presi un paio di volte, sono la cosa da mandare**: dicono dove sta la
  lentezza di quella pagina;
- un documento con la AoR (es. la vIPI di Brindisi): le linguette 2D/3D sono linguette;
- in un editor, il tasto **«Cita»** mostra sette chip: `SID STAR FREQ ATC POS RWY FIX`.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da sapere

- **Le linguette nude erano intermittenti**: comparivano solo con la rete lenta o la cache fredda. Non vederle
  una volta non prova niente; la prova vera è stata fatta **bloccando** il foglio che le vestiva prima, e
  restano a posto.
- ▶ **La pagina `sector-structure` ha un difetto noto e non corretto in questa consegna**: chiede due volte il
  database per ogni settore orfano. Quanto costi dipende da quanti orfani ci sono — lo si vede in quella
  pagina.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Questo pacchetto è compilato per **net8**, come tutti i precedenti. Il salto a net10 slitta a **1.38.0**.
