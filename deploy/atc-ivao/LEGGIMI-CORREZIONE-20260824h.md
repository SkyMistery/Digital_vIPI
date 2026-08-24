# vIPI — correzione del 24 agosto 2026, sera (pacchetto «h»)

**Va sopra il pacchetto «g»**, che è online. Sono **tre file**, ed è **soli file**. Niente database, niente
`wwwroot`, niente cartella `segreti` da toccare: quella è a posto e non si tocca.

> ## Perché esiste questo pacchetto
>
> Poco dopo il caricamento di «g» il sito si è piantato: caricamento infinito e poi timeout, mentre
> `it.ivao.aero` restava su. **Adesso sappiamo perché**, e non per deduzione — c'è scritto. Il registro
> degli errori aggiunto da «g» ha raccolto **92 richieste fallite fra le 16:55 e le 17:07**, e quello è
> stato il primo caso in cui un guasto ha lasciato una riga invece di un'impressione.

---

## Che cosa era successo, in ordine

### Fase 1 — 16:55÷16:57, la corsa (78 righe, un solo utente collegato)

Tutte le 78 righe dicono la stessa cosa:

```
System.InvalidOperationException: A second operation was started on this context instance
   at ...EfStationDirectory.ListAccs()      ← l'elenco delle ACC della barra
   at ...StationResolver.get_Accs()
   at ...SopLayout.BuildRenderTree()        ← dentro il disegno della pagina
```

L'elenco delle ACC (quello dei tasti **LIBB LIMM LIPP LIRR** in alto) si caricava **mentre la pagina veniva
disegnata**. Nello stesso momento la barra stava già aspettando un'altra risposta dal database — quella che
decide se accendere il tasto «Modifica» — e le due domande finivano **insieme sulla stessa connessione**.
Il database ne accetta una per volta: la seconda muore, e con lei la pagina.

**A chi capitava, esattamente.** Non era casuale: dipendeva da chi eravate.

| Chi | Cosa succedeva |
|---|---|
| Visitatore non collegato | la barra non fa quella domanda → **mai** |
| **Voi, da admin** | la risposta arriva subito, senza database → **mai** |
| **Un socio senza incarichi** | la risposta richiede il database → **ogni volta** |

Ed è la conferma nei numeri: delle 78 righe della corsa, **tutte** vengono dallo stesso socio; l'utenza
admin che navigava nella stessa finestra non ne ha prodotta **nessuna**. Ecco perché il difetto lo vedeva
lui e a voi non capitava mai — e perché «riprova» non avrebbe mai risolto niente.

⚠️ **È la spiegazione vera del difetto che aveva visto il socio stamattina, e corregge quella che vi avevo
dato.** Avevo attribuito il guasto alla domanda sul tasto «Modifica». Non è lei a rompersi: è lei ad
**aprire la finestra**, e a morire è l'elenco delle ACC. La correzione di stamattina non poteva prenderlo —
ecco perché è ricapitato.

### Fase 2 — 16:59÷17:07, il database non risponde (11 righe, anche visitatori anonimi)

Le altre 11 righe sono di un altro tipo — **timeout** — e colpiscono anche chi non è collegato. Lì il
database non rispondeva più, ed è la parte che avete visto come **caricamento infinito**: ogni richiesta
riprovava per qualche secondo prima di arrendersi.

ℹ️ Le due fasi sono consecutive e la prima è una causa **plausibile** della seconda (78 richieste fallite in
due minuti e mezzo, ognuna con una domanda lasciata a metà). Ma plausibile non vuol dire misurato: per dirlo
servirebbero i log di MariaDB, che non abbiamo. Quello che è certo è la Fase 1, ed è quella che questo
pacchetto chiude.

---

## Che cosa cambia

1. **L'elenco delle ACC si legge prima di disegnare la pagina**, non durante. Le due domande non possono più
   incontrarsi. (La funzione che serviva a questo esisteva già nel programma dal mese scorso: non la
   chiamava nessuno.)
2. **Se l'elenco non si legge**, la barra esce senza i collegamenti alle ACC e **la pagina si apre lo
   stesso**. Prima era una pagina morta.
3. **La registrazione degli accessi non può più far cadere una richiesta.** Era protetta a metà: un guasto
   lì usciva, il programma provava a mostrare la pagina d'errore, quella parte girava di nuovo e falliva una
   seconda volta — così non usciva **nemmeno la pagina d'errore**.

---

## Che cosa caricare

**Tre file**, tutti nella **radice** dell'applicazione (nessuna sottocartella), in `solo-3-file-h/`:

| File | Dimensione attesa |
|---|---|
| `Vipi.Host.dll` | **76.288 byte** |
| `Vipi.Hosting.dll` | **54.784 byte** |
| `Vipi.Ui.dll` | **1.868.800 byte** |

Totale **1.999.872 byte** — due megabyte, il caricamento più piccolo finora. Tutto il resto è identico a
quello che è già sul server.

## Come si carica

Come «g»: nomi veri, si sovrascrive.

1. FileZilla in **binario**.
2. Rinominate i tre file attuali aggiungendo `.vecchio` (è il rollback pronto).
3. Caricate i tre nuovi.
4. Controllate i byte con la tabella qui sopra.
5. `restart.txt` dentro `tmp/`, **e aprite il sito una volta** — Passenger se ne accorge alla richiesta dopo.

> ⚠️ Sono due megabyte e il sito può fare qualche errore mentre salgono: fatelo in un momento tranquillo.

## Come si vede che è andata

| Controllo | Cosa deve dire |
|---|---|
| `…/diagnostica/avvio-diagnostica.txt` (**Ctrl+F5**) | `Pacchetto «h»`, con l'ora di adesso |
| `…/diagnostica/avvio-errore.txt` | **404** |
| La barra, da admin | targhetta `h · ca4da81` |
| **La prova vera** | serve un **socio SENZA incarichi** che faccia l'accesso e apra `/services`. ⚠️ **Provarlo da admin non prova niente**: l'admin non incappava nel difetto nemmeno prima (vedi la tabella «a chi capitava») |
| `…/diagnostica/errori-richieste.txt` | non deve crescere. ⚠️ Le 92 righe di stasera **restano** nel file: guardate l'ora, non la presenza |

ℹ️ Se volete ripartire da un registro pulito, cancellate `diagnostica/errori-richieste.txt` via FTP: si
ricrea da solo alla prima richiesta che fallisce. Se resta assente, non ne è fallita nessuna.

---

Compilato con gli avvisi trattati come errori: **0 avvisi**, **1954 test verdi** su net8 (3672 contando
anche il giro su net10). Fra questi due nuovi che tengono ferma proprio questa correzione: uno pretende che
l'elenco delle ACC sia già stato letto quando la pagina viene disegnata — se qualcuno lo rimette nel disegno,
il test torna rosso.

⚠️ Come i precedenti, **mai eseguito su Linux**: compilato in modo incrociato da Windows.
