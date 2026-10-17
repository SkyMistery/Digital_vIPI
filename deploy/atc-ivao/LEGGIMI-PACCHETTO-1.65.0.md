# Pacchetto 1.65.0 — solo i file cambiati

> **Timbro:** `1.65.0 · 38a26f3` (17 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.63.0** (`099b0df`, online dal 16 ottobre). La **1.64.0 non è mai stata caricata**: questo pacchetto la
> contiene, quindi si carica **solo questo** e la 1.64.0 si butta. È una **MINOR senza migrazioni**, e si consegna da
> sola via FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **7 file**: 1 in **`en/`** e 6 in **radice**. Niente `wwwroot`.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S110 — il convertitore di coordinate non perde più l'arco con «centred on:».** Quando il testo AIP scrive
  «centred on:» con i due punti attaccati (succedeva su un limite laterale di Torino–Genova), l'arco restava senza
  centro e spariva. Ora i due punti e il punto in fondo a una parola non disturbano più. Nello stesso giro il conto
  «righe lette» è giusto: prima diceva «3 di 14» su un testo con dodici righe di coordinate.
- **S109 — «Tutti i settori» della mappa AOR non è una configurazione** (era la 1.64.0). In Struttura, sotto un
  gruppo senza elenco di configurazioni, una nota spiega che la mappa AOR offre da sola «Tutti i settori»; e in
  inglese quella scelta ora si chiama «All sectors».

## I 7 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **in radice (6)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Application.pdb       Vipi.Application.dll
Vipi.Ui.pdb                Vipi.Ui.dll
Vipi.Host.pdb              Vipi.Host.dll                ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2). ⚠️ `Vipi.Application` e `Vipi.Ui` vanno **insieme**: condividono una costante nuova (S109).

⚠️ **Restano fuori** `Vipi.Domain`, `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations`, `Vipi.Hosting`
(sorgente invariato), `Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`,
`appsettings.json` e tutto `wwwroot` con l'indice degli asset: controllato per impronta, confrontando con la 1.63.0
che gira, e col `git diff`.

**Per tornare indietro** bastano le due rinomine: non c'è migrazione e niente viene scritto.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.65.0 · 38a26f3`** nel piè di pagina (staff); in Diagnostica **`Schema` = `0`** (non c'è migrazione:
  deve restare 0 com'era);
- il **convertitore** (`/services/coordinates`): incollato il testo del limite di Torino–Genova con «centred on:», sotto
  «Che cosa ho letto» deve dire **1 arco convertito** e righe lette **12 di 14**;
- **Struttura → Configurazioni possibili → Milano**: sotto un gruppo senza elenco (Milano APP) la nota su «Tutti i
  settori»; la vIPI di Milano **in inglese**: nella mappa AOR «All sectors».

## Dopo il carico

- Per Milano APP: se servono configurazioni vere, si scrivono in Struttura.
- Restano dalle versioni di prima: il banco su Milano con le clausole vere, la riga «NOTAM» in Sorgenti, la vista live
  dal telefono, la prova del validatore dei tour sull'archivio ATC, gli elenchi di Torino–Genova (aggiungere {WN0} e
  {WS0}), Padova e Bologna, ripubblicare le vIPI ACC. LIRE/LIBG aspettano il SOD.
