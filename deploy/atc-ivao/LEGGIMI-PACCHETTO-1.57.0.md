# Pacchetto 1.57.0 — solo i file cambiati

> **Timbro:** `1.57.0 · 397b7a1` (7 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.56.0** (`2285a80`, online dal 1 ottobre). È una **MINOR con QUATTRO migrazioni**, e si consegna da
> sola via FTP: il database si aggiorna da sé all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **19 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 15 in **radice**.

> ## 🔴 Questa volta una migrazione CANCELLA una tabella
>
> È la prima consegna con una migrazione **distruttiva**. Che cosa fanno le quattro, nell'ordine:
>
> | Migrazione | Che cosa fa |
> |---|---|
> | `SezioniCondivise` | crea la tabella `AgreementSectionShares` |
> | `ViaLeRegoleDiUnificazione` | **cancella la tabella `UnificationRules`** — in produzione ha **zero righe**: nessun editor l'ha mai scritta |
> | `ClausoleCondivise` | crea `AgreementClauseShares` e cancella `AgreementSectionShares`, appena nata due righe sopra (in produzione non è mai esistita) |
> | `OrdineDelleClausole` | aggiunge la colonna `AgreementSections.ClauseOrder` |
>
> Tutte e quattro sono state provate su una copia della produzione. Due cose cambiano rispetto al solito:
>
> 1. **PRIMA di caricare, scaricate una copia del database** da Diagnostica (`.sql.gz`) e tenetela da parte.
> 2. **Il rollback a due rinomine NON basta più da solo.** La 1.56.0 legge `UnificationRules`: rimessi i file vecchi,
>    quella tabella non c'è più e la pagina Struttura e i coordinamenti darebbero errore. Per tornare davvero indietro
>    servono le due rinomine **e** il ripristino del database dalla copia (lo fa chi amministra il database di Ivao.It).

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S97 — coordinamenti e struttura**:
  - un **motore solo** per decidere chi tiene chi (la tabella delle configurazioni e i trasferimenti dicono la stessa
    cosa); le vecchie «regole di unificazione», mai usate, non ci sono più;
  - il **banco di prova** nella pagina Struttura;
  - una clausola si **sposta** da un accordo a un altro;
  - le clausole si **condividono fra più accordi**: «⛓ Condividi…» sulla riga, sulla sezione o sull'accordo intero —
    modificarla la cambia in tutti, ✕ la toglie da quell'accordo, «✂ Stacca» ne fa una copia;
  - ogni sezione ha un **ordine dichiarato** delle clausole: a mano, alfabetico per punto, per quota — vale nella
    pagina, nei documenti e nella vista live;
  - l'avviso **«quota di un altro settore»** nel cruscotto delle lacune, col tasto ⇢ che prepara lo spostamento.
- **S98 — un import mai riuscito non è più «l'anno 1»**: con un database nuovo e la sorgente IVAO irraggiungibile la
  pagina Struttura dava errore 500; lo stesso difetto fermava Pendenti, il giro notturno della deriva e lo storico ATC.

## I 19 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (15)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                                        ← insieme ai file di wwwroot
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta le migrazioni
Vipi.Hosting.pdb                        Vipi.Hosting.dll                          ← ricompilato, vedi sotto
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). Questa volta vale doppio: finché non riparte, il processo vecchio cerca una tabella che il nuovo ha tolto.

ℹ️ **`Vipi.Hosting` c'è anche se il suo sorgente non è cambiato**: si appoggia alle porte degli accordi di
`Vipi.Application`, che con S97 cambiano parecchio. Spedirlo ricompilato costa due rinomine; lasciare il binario vecchio
contro un'Application nuova sarebbe un rischio senza motivo.

⚠️ **Restano fuori** `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles` (non dipendono dagli altri progetti; cambiati
solo per la ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto di `wwwroot`: identici a
1.56.0, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.57.0 · 397b7a1`** nel piè di pagina; in Diagnostica **`Schema` = `0`** (se non è
  0 manca `Vipi.Infrastructure.MySqlMigrations.dll`, oppure una migrazione si è fermata a metà: scaricate
  `diagnostica/` e non riavviate);
- la pagina **Struttura** di un ACC si apre, col banco di prova;
- la pagina dei **coordinamenti**: su una clausola c'è «⛓»; il cruscotto delle lacune mostra la voce «quota di un
  altro settore» (attese 3 sui dati veri: Zagabria su AIOSA, Milano su NELAB);
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Dopo il carico

- **Ripubblicare le vIPI ACC**: la tabella delle configurazioni ora nasce dal motore unico. Sulla copia di produzione
  cambia una sola tabella, LIMM «Conf 2 b» (ES5 passa da WS2 a WS5).
- Le condivisioni di prova fatte in locale non esistono in produzione: quelle vere (per esempio Trapani verso
  `LIRR_SU` e `LIRR_MIL`) vanno fatte lì.
- Restano i gesti su LIRE/LIBG, che aspettano il SOD.
