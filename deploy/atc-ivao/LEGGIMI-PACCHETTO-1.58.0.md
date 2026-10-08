# Pacchetto 1.58.0 — solo i file cambiati

> **Timbro:** `1.58.0 · 2ecbdd6` (8 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.57.0** (`397b7a1`, online dal 7 ottobre). È una **MINOR con UNA migrazione additiva**, e si consegna
> da sola via FTP: il database si aggiorna da sé all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **15 file**: 1 in **`en/`** e 14 in **radice**. Questa volta **niente in `wwwroot`**.

> ## ⚠️ Dopo il carico `/vsop/health` dice ancora `Degraded`: è atteso
>
> Questo pacchetto nasce dal falso «Trasferimento senza ripiego» di Milano (`LIMM_WS2_CTR → LIMM_ES2_CTR`) che tiene
> la salute in `Degraded` dalla 1.57.0. **Caricarlo non lo spegne.** Lo spegne un gesto in Struttura, dopo il carico:
> dichiarare **completo** l'elenco delle configurazioni dei settori d'area di LIMM (vedi «Dopo il carico»). Finché
> quella casella è spenta il rilievo resta, e `Degraded` non è un guasto del pacchetto.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

**S99 — le configurazioni possibili di un gruppo di settori, dichiarate in Struttura.**

- Nella pagina **Struttura** c'è una sezione nuova, «Configurazioni possibili»: per ogni gruppo (i settori d'area di
  un ACC, oppure un ente APP con le sue posizioni) l'elenco delle configurazioni, cioè di quali settori possono
  essere aperti insieme. Si scrivono lì, sotto il lock della struttura.
- Ogni elenco ha la casella **«L'elenco è completo»**. Spenta, l'elenco è una lista di esempi e non vincola niente.
  Accesa, dice che fuori da quelle configurazioni non si apre: la Diagnostica e la scala di risalita, chiuso un
  settore, chiudono anche quelli che senza di lui non possono stare aperti.
- Sotto ogni elenco si leggono le **conseguenze ricavate** («apre solo con», «mai insieme a», «può stare aperto da
  solo»): si leggono prima di accendere la casella, ed è lì che si vede un elenco a cui manca una riga.
- La sezione **Configurazioni** dei documenti (vIPI ACC, gruppi APP, vIPI APP) legge l'elenco dalla Struttura: non
  si scrive più nel documento.
- Il **banco di prova** prende gli scenari pronti dalla Struttura.

**La migrazione** (`ConfigurazioniPossibili`): crea la tabella `SectorConfigurationSets` e un indice. Non toglie e
non rinomina niente.

**Al primo avvio gira un travaso**, una volta sola: copia in Struttura gli elenchi che oggi stanno scritti nei
documenti. **Arrivano tutti spenti**: dopo il carico niente si comporta diversamente da prima, finché qualcuno non
accende una casella.

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

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (non dipendono dagli altri progetti; cambiati
solo per la ricompilazione), `Vipi.Host.staticwebassets.endpoints.json` e tutto `wwwroot` (nessun asset cambia),
`deps.json`, `runtimeconfig.json`, `appsettings.json`: identici a 1.57.0, controllato per impronta e col `git diff`.

**Per tornare indietro** bastano le due rinomine: la migrazione aggiunge soltanto, e la 1.57.0 non legge la tabella
nuova. Gli elenchi restano scritti anche nei documenti, dove la 1.57.0 li cerca.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.58.0 · 2ecbdd6`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`: senza, la tabella non nasce e la sezione nuova resta vuota senza
  dare altri segnali);
- la pagina **Struttura**: in fondo c'è «Configurazioni possibili»; aperta e scelto **LIMM**, i settori d'area
  mostrano le **quattro** configurazioni già scritte nella vIPI di Milano, con la dicitura che l'elenco **non
  vincola** (è il travaso: arrivano spente);
- `https://atc.it.ivao.aero/vsop/health/ready` risponde **`Healthy`**; `/vsop/health` risponde ancora **`Degraded`**
  (vedi sopra).

## Dopo il carico

I gesti sono del committente, in Struttura, col lock:

1. **LIMM, settori d'area**: accendere «L'elenco è completo». È il gesto che toglie il rilievo «Trasferimento senza
   ripiego» e riporta `/vsop/health` a `Healthy` (la pagina di salute legge il report da una cache di un paio di
   minuti).
2. **Torino–Genova** (`LIMF_WW0_APP`): all'elenco mancano le configurazioni **{WN0}** e **{WS0}** da soli. Prima si
   aggiungono, poi si accende.
3. **Padova** (settori d'area) e **Bologna** (`LIPE_W_APP`): si possono accendere così come sono.
4. **Roma** (settori d'area) e **Venezia** (`LIPZ_SE0_APP`): **lasciare spente**, o riscrivere l'elenco. Sulle
   sessioni vere dell'ultimo anno stanno in una configurazione scritta il 2% del tempo: accese così, direbbero che EW
   non apre senza NE1 e SU.
5. **Ripubblicare le vIPI ACC**: la sezione Configurazioni ora nasce dalla Struttura.

Restano i gesti su LIRE/LIBG, che aspettano il SOD.
