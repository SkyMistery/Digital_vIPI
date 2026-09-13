# Pacchetto 1.26.1 — solo i file cambiati

> **Timbro:** `1.26.1 · fae666e` (14 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.26.0** (`cad6698`, online dal 13 settembre). **PATCH**: nessuna migrazione, nessuna pagina nuova.
> **40 file**: 16 in **radice**, 1 in **`en/`**, 24 in **`wwwroot/_content/Vipi.Ui/`**.
>
> ⚠️ **Questa volta ci sono file di `wwwroot`**, e viaggiano **insieme** a
> `Vipi.Host.staticwebassets.endpoints.json`: l'indice dice con che nome il sito chiede ogni file. Caricarne
> uno senza l'altro fa chiedere nomi che non esistono (il difetto del 24 agosto).
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

Il seguito della revisione totale del 13 settembre: correzioni, nessuna funzione nuova. Quelle che si vedono:

- **L'editor non si blocca più** quando due gesti si sovrappongono o la connessione cade e riprende; la
  **Ricerca** pubblica regge chi digita veloce.
- **Le sezioni strutturate** di APP, ACC e vSOP militare si salvano solo col lock di chi edita.
- **vAWOS**: le procedure LVP si chiudono quando il tempo migliora, e il quadro non inventa più
  tendenze fuori dall'osservazione.
- **Mappe e vista 3D** non si duplicano più navigando fra le pagine.
- **Vista live**: se il flusso IVAO si ferma, la pagina lo dice («scaduto») invece di mostrare dati vecchi.
- **Statistiche ATC**: i minuti di traffico si contano sul tempo vero, e un fermo lungo non lascia buchi.
- **Import** di tabelle e sectorfile più robusti su file malformati.
- **L'aiuto dell'editor aeroporto, la Guida e il tour** descrivono il salvataggio com'è: ogni gesto scrive da
  solo, non c'è più «Salva tutto».
- **Radioassistenze, spazi aerei, glossario, alias SID, impostazioni delle statistiche**: il permesso si
  controlla anche dietro la pagina, non solo col bottone.

## I 40 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In `wwwroot/_content/Vipi.Ui/` (24)** — si caricano e rinominano **per primi**, tutti:

```
vipi-aor.js      vipi-aor.js.br      vipi-aor.js.gz
vipi-aor3d.js    vipi-aor3d.js.br    vipi-aor3d.js.gz
vipi-awos.js     vipi-awos.js.br     vipi-awos.js.gz
vipi-editor.js   vipi-editor.js.br   vipi-editor.js.gz
vipi-live.js     vipi-live.js.br     vipi-live.js.gz
vipi-mva.js      vipi-mva.js.br      vipi-mva.js.gz
vipi-tour.js     vipi-tour.js.br     vipi-tour.js.gz
vipi-ui.js       vipi-ui.js.br       vipi-ui.js.gz
```

**In radice e in `en/` (16)**, in quest'ordine:

```
Vipi.Host.staticwebassets.endpoints.json   ← subito dopo wwwroot
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.AuroraProfiles.pdb
Vipi.AuroraProfiles.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
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

⚠️ **Restano fuori** `Vipi.Infrastructure.MySqlMigrations.dll` e `Vipi.AuroraBridge.Contracts.dll`: il loro
codice non è cambiato. E **resta fuori il runtime .NET**, vedi in fondo.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

- **vAWOS** (`/services/vawos`, uno scalo qualsiasi): il quadro si riempie e dopo un minuto si aggiorna da
  solo. È il controllo che dice che i JS nuovi e l'indice sono arrivati **insieme**.
- **Mappe**: la pagina AoR di un'ACC mostra la mappa, e tornandoci dalla barra non ne compaiono due.

Col login da amministratore:

- il timbro in barra: **`1.26.1 · fae666e`**;
- `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`** (non ci sono migrazioni nuove: deve restare 0).

---

# ⚠️ Da sapere: il runtime .NET del server è 8.0.28

Ogni pacchetto da agosto è **parziale**: porta i nostri file e lascia sul server il runtime .NET del primo
carico completo, cioè **8.0.28**. Oggi .NET 8 è a **8.0.31**: tre aggiornamenti di sicurezza che il sito non
ha. Un pacchetto parziale non può rimediare (il runtime è fatto di centinaia di file che vanno cambiati tutti
insieme).

▶ Rimedia il **prossimo pacchetto, 1.27.0**: porta il sito su **.NET 10** (che resta supportato fino al 2028,
mentre .NET 8 finisce il 10 novembre 2026) ed è un **carico completo**, con questo pacchetto pronto per
tornare indietro. Arriva dopo che 1.26.1 ha girato qualche giorno senza problemi.

# Il pannello: come da 1.25.4

- ✅ **Le direttive nginx** per i file statici.
- ✅ **`passenger_min_instances ≥ 1`**.
- ⚠️ **La Cache Rule di Cloudflare** con la condizione `Cookie contains ".AspNetCore.Culture"`: regola e perché
  in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).
- 🆕 **Da dire a chi gestisce il server**: risulta **Debian 11**, uscito dal supporto LTS il 31 agosto 2026.
  Non blocca niente del sito, ma il sistema non riceve più aggiornamenti di sicurezza.
