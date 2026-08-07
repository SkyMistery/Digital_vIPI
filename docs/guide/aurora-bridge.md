# vIPI Aurora Bridge — guida all'uso

Piccolo programma che sta accanto ad Aurora: selezioni un aeromobile, lui legge la vIPI e ti dice **a che
livello va ceduto al prossimo ente**, e su tua richiesta scrive quel livello nell'etichetta quota del tag.

Non decide niente al posto tuo: propone, motiva, e scrive **solo** quando premi tu.

---

## 1. Prima di partire

**In Aurora:** PVD → `F7` → *Other* → **3rd Party Software Access = YES**.

> ⚠️ Se il tool dice «Aurora non raggiungibile» pur avendo il flag già su YES, **rimettilo a NO e poi di nuovo
> a YES nella sessione in corso**. Il valore salvato nel profilo da solo non apre la porta: va applicato con
> Aurora aperta. È la trappola più comune.

Serve anche essere **connessi alla rete**: senza sessione ATC, Aurora non apre il servizio.

## 2. Avvio

Doppio clic su `VipiAuroraBridge.exe`. Non richiede installazione né .NET: è tutto dentro l'eseguibile.

La finestra nasce **sempre in primo piano** (si spegne col 📌), perché è pensata per stare in un angolo
sopra la PVD.

## 3. Cosa vedi

| Zona | Cosa dice |
|---|---|
| Barra blu | stato di Aurora e la tua postazione |
| Riquadro «Traffico selezionato» | callsign, rotta, livello di crociera, quota, e se il traffico è **assunto** |
| Striscia gialla | avvisi: condizioni non verificabili, sito irraggiungibile, livelli mancanti |
| Elenco | i punti di trasferimento candidati, dal più probabile in giù |

Ogni candidato mostra il **CoP**, il **livello** come sta scritto nella vIPI, **a chi va ceduto adesso**
(risalendo la gerarchia: se il settore nominale è chiuso compare chi lo copre, o `UNICOM`), l'esito della
**condizione** (pista/area) e — riga piccola — **perché** quel candidato sta lì.

Il pulsante a destra scrive il livello. Se è spento, sopra c'è scritto **il motivo**.

## 4. Scrivere il livello

Tre modi, tutti espliciti:

- **clic** sul pulsante del candidato che vuoi;
- **scorciatoia globale** (di serie `Ctrl+Alt+L`): scrive il **primo** candidato, senza toccare il mouse.
  Se il primo non è scrivibile, la scorciatoia **si ferma e ti dice perché** — non ripiega di nascosto su un
  altro livello, che sarebbe diverso da quello che ti aspetti;
- **Pulisci tag**: svuota l'etichetta quota del traffico selezionato.

Dopo la scrittura il tag si aggiorna **al giro radar successivo** (uno-due secondi): non è un blocco, è come
funziona Aurora.

## 5. Quando NON si può scrivere

| Messaggio | Perché | Cosa fare |
|---|---|---|
| «Traffico non assunto: Aurora rifiuta la scrittura» | Aurora consente di scrivere l'etichetta **solo** sul traffico che hai assunto | assumi il traffico, oppure lascia perdere |
| «Livello non scrivibile: manca il valore nella vIPI» | quel punto di trasferimento ha il vincolo ma non il livello (tipico dei sorvoli non compilati) | va colmato nell'editor della vIPI; **se il sorvolo è senza quota, il tool non scrive nulla** |
| «Nessun traffico selezionato» | non hai selezionato nulla in Aurora | seleziona un aeromobile |

Il tool riempie l'**etichetta quota** del tag. **Non** riempie il campo XFL nativo di Aurora: il protocollo
3rd-party lo espone in sola lettura e non esiste alcun comando per scriverlo.

## 6. Impostazioni (⚙)

| Voce | A cosa serve |
|---|---|
| **Sito** | da dove arrivano i dati vIPI. Si cambia per puntare a un'istanza locale in prova |
| **Postazione** | forza la postazione di cui applicare le regole. Vuoto = quella connessa in Aurora. Serve quando il callsign connesso non è un settore del sito (addestramento, callsign fuori standard) |
| **Scorciatoia** | combinazione globale, es. `Ctrl+Alt+L`. Serve almeno un modificatore. Se un altro programma la usa già, il tool lo dice all'avvio |

Sito, postazione e scorciatoia si applicano **al riavvio** del tool.

> La «Postazione» cambia solo **quali regole di trasferimento** vengono applicate. Chi può scrivere nel tag
> dipende sempre dalla connessione vera di Aurora, non da questo campo.

## 7. Se qualcosa non va

I file stanno in `%LOCALAPPDATA%\VipiAuroraBridge`:

- `settings.json` — le impostazioni (cancellalo per ripartire dai valori di serie);
- `bridge.log` — cosa è stato scritto e quando, più gli errori. È la prima cosa da guardare (e da allegare)
  quando qualcosa non torna;
- `cache\` — l'ultima risposta valida del sito per ogni contesto.

**«Sito irraggiungibile: sto mostrando l'ultima risposta valida»**: il portale non risponde e il tool sta
proponendo dati già visti per quello stesso volo. Sono ancora utili, ma non sono freschi: la striscia gialla
resta accesa finché il sito non torna.

## 8. Limiti noti

- Si scrive **solo** sul traffico assunto (limite di Aurora, non del tool).
- L'**XFL** non è scrivibile da nessun programma di terze parti (vedi §5).
- Le condizioni di **area attiva** e quelle **personalizzate** non sono verificabili in automatico: il tool le
  segnala con `?` e le lascia al tuo giudizio. Le **piste in uso** invece le legge da Aurora e le verifica.
- La **scorciatoia globale** funziona su Windows. Su macOS resta il pulsante nella finestra.

---

*Dettagli tecnici, protocollo e decisioni di progetto: [`../design/piano-aurora-bridge.md`](../design/piano-aurora-bridge.md).
Contratto dell'API: [`../reference/api-aurora-bridge.md`](../reference/api-aurora-bridge.md).*
