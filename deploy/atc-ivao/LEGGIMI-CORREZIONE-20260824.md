# vIPI — correzione del 24 agosto 2026 (pacchetto «c»)

**Leggete questo foglio, non `LEGGIMI-AGGIORNAMENTO.md`.** Quello descrive la consegna del 23 agosto, con
la sostituzione del database. Questa correzione va sopra, ed è **soli file**.

> ## ⛔ Il database NON si tocca
>
> Niente `.sql`, niente `DROP DATABASE`, niente import. Lo schema non cambia.

---

## Che cosa corregge

**Il login che finiva su una pagina d'errore muta.** Quando il ritorno da IVAO non si concludeva, il sito
mostrava *«An error occurred while processing your request.»* — che non diceva niente a chi legge e non
lasciava niente a noi: il guasto del 23 agosto si è dovuto ricostruire senza una riga di log.

Ora ogni guasto del login:

- **finisce nei log del server**, con la ragione, sotto la voce `Vipi.Auth.Ivao`;
- se **eravate già dentro** (il cookie dura 7 giorni), vi riporta alla pagina dove stavate andando invece
  di mostrarvi un errore — era il caso in cui «al refresh risultava loggato»;
- se **non c'era una sessione**, apre una pagina che dice cosa è successo e offre un «riprova ad accedere».

⚠️ La pagina **non** rimanda da sola al login, ed è voluto: se il rifiuto viene da IVAO, il rimbalzo
automatico diventerebbe un anello infinito. Il secondo tentativo lo si chiede con un clic.

ℹ️ Il vecchio sintomo — *«errore al login, ma al refresh sono dentro»* — resta possibile solo per chi non è
più uscito dal sito dal 15 agosto. Si consuma da sé: basta **uscire e rientrare** una volta.

---

## La procedura, in tre passi

Valgono le due impostazioni di FileZilla di [`LEGGIMI-FTP.md`](LEGGIMI-FTP.md): **trasferimento binario** e
**bit di esecuzione** su `Vipi.Host` e `createdump`.

### 1. Le tre cose da NON cancellare

Si sovrascrive **senza svuotare** la cartella dell'applicazione:

| Cosa | Dove |
|---|---|
| `appsettings.Production.json` | radice dell'applicazione — contiene la password del database |
| `vipi-keys/` | `…/public_atc/vipi-keys` — le chiavi che firmano le sessioni |
| `tmp/` | `…/public_atc/tmp` — serve per il riavvio |

⚠️ **`vipi-keys/` più che mai.** Se quella cartella sparisce, **ogni** login del sito fallisce — e con questa
versione si vedrà scritto nei log come motivo `correlazione`. È la prima cosa da guardare se dopo
l'aggiornamento non entra più nessuno.

⚠️ La cartella `deploy/` del pacchetto **non va caricata**.

### 2. Caricare

Rispetto al pacchetto «b» cambiano **due file soli**, perché la correzione vive tutta nel modulo di login:

```
Vipi.Host.dll
Vipi.Host.pdb
```

Tutto il resto — le altre librerie, `wwwroot`, `content`, i file di sistema — è **byte per byte identico** a
quanto è già sul server. Si possono quindi caricare **solo quei due file**, oppure tutto il pacchetto: il
risultato è lo stesso.

### 3. Riavviare

Un file `restart.txt` qualsiasi, anche vuoto, dentro `tmp/` — sovrascrivendo quello che c'è. Da shell:
`touch tmp/restart.txt`.

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC (LIRR, LIMM, LIBB) |
| Il login IVAO | entra, e in alto compare il vostro nome |
| Una pagina pubblica d'aeroporto | mostra il documento dell'aeroporto, non quello dell'Avvicinamento |
| `diagnostica/avvio-errore.txt` | **non deve esistere** |

Se un giorno un accesso non riesce, la pagina ora mostra **un codice** (`correlazione`, `nonce`, `portale`,
`sconosciuto`): mandatecelo, insieme all'ora. Con quello e la riga `Vipi.Auth.Ivao` nei log si capisce in
un minuto ciò che il 23 agosto è costato una serata.

Compilato con gli avvisi trattati come errori: **0 avvisi**, **3621 test verdi**.

⚠️ Come i precedenti, questo pacchetto **non è mai stato eseguito su Linux**: è compilato in modo
incrociato da Windows.
