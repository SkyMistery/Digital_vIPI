# vIPI — correzione del 24 agosto 2026, sera tardi (pacchetto «i»)

**Va sopra il pacchetto «h»**, che è online. **Quattro file**, soli file. Niente database, niente `wwwroot`,
la cartella `segreti` non si tocca.

> ## Perché c'è un altro pacchetto dopo «h»
>
> Perché «h» **non era la fine**, e lo sappiamo perché avete azzerato il registro degli errori e quello si è
> riempito di nuovo: **sette richieste** fra le 17:43:59 e le 17:44:17, tutte dello stesso socio, tutte con
> lo stesso errore di prima — ma **dentro le pagine**, non più nella barra.
>
> | Pagina | |
> |---|---|
> | `/services/vsop/libb`, `/lirr` | ×3 |
> | `/…/libb/airports`, `/…/limm/airports` | ×4 |
>
> `/services` era a posto: quella parte «h» l'ha chiusa davvero.

---

## Che cosa cambia

### 1. Le due pagine lavorano su una connessione loro

Il guasto è sempre lo stesso: due domande al database che si accavallano sulla stessa connessione, e la
seconda muore. Finora abbiamo tolto **una** delle due domande dalla mischia. Ora le due pagine che
comparivano nel registro — l'elenco di una ACC e la pagina di un aeroporto — **usano una connessione tutta
loro**: nessuno può passargli davanti, chiunque sia l'altro.

⚠️ **Ed è proprio il punto: chi fosse l'altro non lo so ancora.** Lo stack dell'errore dice chi è morto, mai
chi stava già correndo. Ho provato tre modi di riprodurlo in laboratorio e nessuno ha funzionato, quindi
invece di inseguire un sospetto ho tolto la possibilità che la collisione avvenga. È lo stesso rimedio che
sei componenti del sito usano già dall'audit di luglio.

### 2. Il registro, la prossima volta, dirà anche chi c'era già

Questa è la parte che avete chiesto, ed è quella che chiude il cerchio per il futuro. Da adesso ogni voce
dell'errore porta anche **che cosa era aperto in quel momento**, con il nome della funzione che l'aveva
chiesto:

```
⚠️ Al momento del guasto, 17:44:10 UTC, erano aperte:
   da 42 ms · SELECT "e"."Id" … FROM "EditGrants" AS "e" …
      ↑ Vipi.Application.Auth.EditAuthorizationService.CanEditAnythingAsync ← Vipi.Ui.Shared.SopLayout…
```

Con quella riga, «non so chi fosse l'altra» diventa un fatto, e la prossima correzione non è più un
tentativo.

ℹ️ Non rallenta il sito in modo percepibile (meno di un millisecondo su una pagina intera) e **non registra
mai i valori** delle query: solo il testo del comando, tagliato. Il file resta spedibile per email.

---

## Che cosa caricare

**Quattro file**, tutti nella **radice** dell'applicazione, in `solo-4-file-i/`:

| File | Dimensione attesa |
|---|---|
| `Vipi.Application.dll` | **1.307.136 byte** |
| `Vipi.Host.dll` | **76.288 byte** |
| `Vipi.Infrastructure.dll` | **2.932.224 byte** |
| `Vipi.Ui.dll` | **1.869.312 byte** |

Totale **6.184.960 byte**.

## Come si carica

Uguale a «h»: nomi veri, si sovrascrive.

1. FileZilla in **binario**.
2. Rinominate i quattro file attuali aggiungendo `.vecchio` (rollback pronto).
3. Caricate i quattro nuovi e **confrontate i byte** con la tabella.
4. `restart.txt` dentro `tmp/`, **e aprite il sito una volta**.

> ⚠️ Sei megabyte: il sito può fare qualche errore mentre salgono. Momento tranquillo.

## Come si vede che è andata

| Controllo | Cosa deve dire |
|---|---|
| `…/diagnostica/avvio-diagnostica.txt` (**Ctrl+F5**) | `Pacchetto «i»`, con l'ora di adesso |
| `…/diagnostica/avvio-errore.txt` | **404** |
| La barra, da admin | targhetta `i · 73172a0` |
| **La prova vera** | un **socio senza incarichi** che apre `/services/vsop/libb` e la pagina di un aeroporto — sono i due indirizzi che stasera morivano. ⚠️ Da admin non prova niente: l'admin non ci cascava nemmeno prima |
| `…/diagnostica/errori-richieste.txt` | **azzeratelo di nuovo prima della prova** (cancellatelo via FTP): così «il file esiste» torna a voler dire «è successo qualcosa» |

Se ricapita, quel file adesso contiene anche **chi era l'altra operazione**: mandatemelo e la prossima
diagnosi non sarà un'ipotesi.

---

Compilato con gli avvisi trattati come errori: **0 avvisi**, **1956 test verdi** su net8 (3674 con net10).
⚠️ Come i precedenti, **mai eseguito su Linux**: compilato in modo incrociato da Windows.
