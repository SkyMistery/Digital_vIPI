# Pacchetto 1.47.0 — solo i file cambiati

> **Timbro:** `1.47.0 · 067a737` (29 settembre 2026). Lo vedono gli amministratori nella barra in alto; se la barra
> è stretta sta nella riga `Versione` della **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.46.5** (`e24557e`, online dal 25 settembre). È una **MINOR con TRE migrazioni ADDITIVE**
> (`AliasPerScalo`, `ProcedureSostituite`, `SectorfileDifferito`): aggiungono colonne e una tabella, l'unica cosa
> tolta è un indice. Si consegna da sola via FTP, il database si aggiorna da sé all'avvio, e **il rollback a due
> rinomine resta valido** (non è un pacchetto «NON SI TORNA INDIETRO»). Nessun segreto nuovo, nessuna configurazione
> da toccare.
> **52 file**: 1 in **`en/`**, 36 in **`wwwroot/_content/Vipi.Ui/`**, 15 in **radice**.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina. Se si sovrascrive un `.dll` mentre
> l'applicazione gira, il file si tronca sotto il processo e il processo muore. La procedura è in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57** (a quell'ora l'hosting chiude i processi).
>
> ⏰ **Va online PRIMA del 1 ottobre 2026, 00:00 UTC.** Se arriva dopo, su LIBV_APP la release programmata #187
> (ciclo 2610) prende il posto della #423 in vigore: prima di quell'ora va annullata la #187, o LIBV_APP
> ripubblicata al 2610 (U-009). Con 1.47.0 online prima, la #187 diventa «superata» da sola.

---

## Che cos'è

La **revisione totale 3** del sito (26–29 settembre): 256 voci registrate, corrette a lotti — registro in
`docs/history/audit-2026-09-26-revisione-totale-3.md`, dettaglio voce per voce in `docs/filoni/sito.md` S9–S40
(§A130). In breve:

- **pubblicazione e release**: una release programmata vecchia non torna più in vigore sopra una più recente
  (U-009); «Pubblica ora» non crea release vuote; avvisi quando dati del sectorfile entrano solo al ciclo dopo;
- **scritture che si perdevano**: doppi clic, salvataggi sovrapposti, lock e documenti uniti;
- **il circuito che cade** su molte pagine staff (doppio clic, operazioni in fila);
- **import da IVAO e dal sectorfile**: i giri non si segnano verdi senza aver letto; radioassistenze, MRVA e
  torri dal sectorfile; SID col prefisso giusto (alias per scalo);
- **vista rapida, METAR, vAWOS, LVP** letti correttamente; statistiche ATC ricalcolate;
- **superficie pubblica**: tetti al Profile Swapper e ai circuiti anonimi (503 oltre 200), archivio ATC chiuso
  a chi non ha chiave (nessuna chiave emessa: non ferma nessuno);
- **login IVAO**: il «sign-in expired along the way» al primo tentativo si recupera da solo (S29).

## I 52 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (36)** — dodici file, ognuno **con il suo `.br` e `.gz`**:
   `vipi-aor.js`, `vipi-aor3d.css`, `vipi-aor3d.js`, `vipi-awos.css`, `vipi-awos.js`, `vipi-boot.js`,
   `vipi-media.js`, `vipi-mva.js`, `vipi-swapper.css`, `vipi-theme.css`, `vipi-tour.js`, `vipi-zoom.js`
3. **in radice, subito dopo `wwwroot`**: `Vipi.Host.staticwebassets.endpoints.json` — ⚠️ viaggia **insieme** ai
   file di `wwwroot`: è l'indice con cui il sito chiede ogni asset.
4. **in radice (14)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Domain.pdb                         Vipi.Domain.dll
Vipi.Application.pdb                    Vipi.Application.dll
Vipi.Infrastructure.pdb                 Vipi.Infrastructure.dll
Vipi.Infrastructure.MySqlMigrations.pdb Vipi.Infrastructure.MySqlMigrations.dll   ← porta le 3 migrazioni
Vipi.Hosting.pdb                        Vipi.Hosting.dll
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**, altrimenti Passenger non se ne accorge.

⚠️ **Il primo avvio è più lento del solito** (anche un paio di minuti): applica le tre migrazioni e fa, una volta
sola, le passate `StarCiviliLive`, `PubblicoDiCatalogo`, `StoricoStatistiche` e le riconciliazioni documentali
della build nuova. Non si riavvia di nuovo nel frattempo.

⚠️ **Restano fuori** gli altri assiemi (`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`: sorgente
invariato, cambia solo l'impronta della ricompilazione), `deps.json`, `runtimeconfig.json`, `appsettings.json` e
il resto di `wwwroot`: identici a 1.46.5, controllato per impronta.

## Il controllo dopo il riavvio

⚠️ Il controllo è **la Ricerca che TROVA**: `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**:
devono comparire **dei documenti** (vIPI Roma, LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un
**guasto**, anche se la riga è cambiata.

Col login da amministratore:

- il timbro **`1.47.0 · 067a737`**;
- in `services/vsop/admin/diagnostics` la riga **`Schema` = `0`**: è la prova che le tre migrazioni sono entrate
  (se non è 0, il `Vipi.Infrastructure.MySqlMigrations.dll` non è arrivato);
- l'interfaccia in **inglese** mostra le frasi nuove (prova che `en/` è arrivato): `services/vsop/screens` dice
  «Generated from the application's routes: N addresses…» (in italiano «Indice generato dalle rotte
  dell'applicazione…»);
- LIBV_APP, pannello release: la #187 è **superata**, in vigore resta la #423 (o la più recente);
- il pannello release di un altro documento qualsiasi si apre e mostra la release in vigore giusta;
- nel log del giorno le passate d'avvio dicono di aver finito, senza errori.

Da fuori (⚠️ Edge automatico su questa macchina non parte: se lo script si ferma con «Failed to launch the browser
process» la verifica si fa a mano come sopra):

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

## Dopo il caricamento: i gesti a mano (del committente)

1. **Spazi aerei**: ricaricare `it.kmz` (U-119): i 32 volumi con «&apos;» nel nome si correggono solo rileggendo
   l'AIP.
2. **LIRS / LIRL** (documenti uniti): sciogliere l'unione o riapplicare la scheda «in comune», poi ripubblicare.
3. **LICT**: al primo giro delle radioassistenze la riga `TRP|VHF|25X` torna modificabile; le tabelle degli
   alternati militari di LICT che la citano vanno ripuntate su `TRP|VHF|`.
4. **U-105**, con «Sposta in…»: vIPI LIBB, blocco Brindisi CS0 (tre libere IFR dentro la IFR di catalogo) e Perugia
   Approach (una sola «Gestione del traffico»); il registro d'avvio elenca i documenti da sistemare.
5. **Torri**: al primo giro dei settori d'aeroporto le torri con l'anello di `twrs.tfl` passano alla forma del
   sectorfile (sulla copia 66 su 68): un'occhiata alle due che restano.
6. **Primo import SID**: le chiavi si riscrivono col prefisso grezzo — è atteso.
7. **U-094**, da decidere: recuperare le 198 sessioni ATC saltate (`TrafficFilledUtc` a NULL) — gesto sui dati veri.
8. **Deriva**: qualche documento può chiedere di ripubblicare (frequenze dei settori disattivati U-150, AoR degli
   scali con ATZ in AGL U-217): è il segnale giusto, non un guasto.
9. **Ivao.it**: se ospita vIPI, il suo host imposta `MaximumReceiveMessageSize` = 512 KB (U-016,
   `docs/guide/integration.md`).
10. **Login**: nel registro dei login la riga «Nonce» dice se un fallimento era un cookie perso (recuperato) o un
    nonce diverso (lato IVAO) — da guardare al prossimo scarico.
