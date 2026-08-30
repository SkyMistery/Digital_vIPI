# «Attempting to reconnect to the server…» — quattro mosse

**31 agosto 2026.** Eseguita, ramo `riconnessione-circuito`. Build Release 0 avvisi, suite verde.

## Il fatto

Sul sito vero capita di vedere, mentre si sta leggendo o si sta scrivendo, un riquadro nero in inglese
che dice **«Attempting to reconnect to the server…»**. Non vuol dire che il sito sia giù: vuol dire che è
morto il **circuito**, cioè lo stato che il server tiene in memoria per quella singola pagina aperta.

Le cause che producono quel riquadro sono due, e chiedono rimedi **opposti**:

| | Che cos'è successo | Cosa serve |
|---|---|---|
| **1. Buco di rete** | il WiFi salta, il telefono cambia cella, il portatile si risveglia | **riprovare**: il circuito di là c'è ancora e si ritrova la pagina esatta |
| **2. Il processo è morto** | Passenger l'ha spento per inattività (o è crollato) e ne è nato un altro | **ricaricare**: quel circuito non esiste più da nessuna parte |

Il difetto di com'era prima è che il **caso 2 finiva nel comportamento del caso 1**: si riprovava, si
falliva, e restava sullo schermo un messaggio in inglese con un tasto da premere. Il caso 2, su questo
hosting, è anche quello **più probabile**: Plesk + Passenger spegne il processo quando per un po' nessuno
chiede niente e lo rigenera alla richiesta successiva (scoperto il 23→24 agosto, vedi
`host-reale-plesk-passenger`).

⚠️ **«Più probabile» non è «misurato».** È il motivo per cui la prima delle quattro mosse non tocca il
sintomo: conta.

## Le quattro mosse

### 1. Contare i riavvii — `diagnostica/avvii.txt`

`avvio-diagnostica.txt` esiste da agosto e **non poteva rispondere**: viene *riscritto* a ogni avvio,
quindi dice quando è ripartito l'ultimo processo e mai quanti ce ne sono stati. Tre riavvii al giorno
(inattività notturna, fisiologico) e quaranta (qualcosa che si rompe) producevano lì lo stesso file.

`RegistroAvvii` scrive **in coda**, una riga per avvio e una per arresto:

```
2026-08-31 03:12:07Z  AVVIO    1.4.2+ab12cd3             (il precedente si era spento in modo ordinato 04:41:52 fa)
2026-08-31 07:54:33Z  ARRESTO  acceso per 04:42:26
2026-08-31 09:01:10Z  AVVIO    1.4.2+ab12cd3             ⚠ il processo precedente NON si è spento in modo ordinato — era partito 01:06:37 prima (crash, memoria esaurita, o una dll sovrascritta via FTP)
```

⚠️ **La distinzione che vale il file è la terza riga.** Uno spegnimento per inattività è *ordinato*:
Passenger manda il segnale, l'host chiude, e la riga `ARRESTO` fa in tempo a essere scritta. Un crash o
un'uccisione secca no. Quindi **un AVVIO preceduto da un altro AVVIO è un processo morto male**, e la riga
lo dice a parole invece di lasciarlo dedurre. È l'unico caso in cui vale la pena aprire `avvio-errore.txt`
e `errori-richieste.txt`.

Il file si pota da solo a 1 000 righe quando ne supera 2 000 (~180 KB: quanto si scarica volentieri via
FTP), e non solleva mai — vale la regola di `StartupDiagnostics`: un problema nel raccontare l'avvio non
deve diventare un avvio fallito.

### 2. Ricaricare da soli quando riconnettersi è impossibile

`blazor.web.js` ora parte con **`autostart="false"`** e a chiamare `Blazor.start` è
`vipi-riconnessione.js`, che è l'unico modo di scrivere i tempi di riconnessione. Da lì:

- **55 tentativi ogni 5 secondi** (≈ 4 min 35 s), tarati sulla finestra in cui il server trattiene i
  circuiti staccati (§4). Riprovare più a lungo vuol dire ritentare quando di là non c'è più niente;
  riprovare meno vuol dire arrendersi mentre ci sarebbe ancora.
- il riquadro è **nostro**, in italiano e in inglese, dentro il tema (`App.razor` + `.vipi-rec` in
  `vipi-theme.css`). Blazor non lo disegna: gli mette e gli toglie delle classi.
- **sullo stato `rejected` si ricarica da soli.** È il server che risponde «quel circuito non lo
  conosco», cioè il caso 2 detto dalla sorgente e non indovinato. Chi sta leggendo vede mezzo secondo di
  ricaricamento invece di un messaggio d'errore — e la ricarica è anche la richiesta che risveglia
  Passenger.
- **sullo stato `failed` no.** Tentativi finiti senza mai raggiungere il server: quasi sempre è la rete
  dell'utente che non c'è, e una pagina ricaricata senza rete è una pagina di errore del browser. Lì
  restano la frase e il tasto.
- massimo **3 ricariche automatiche in un minuto**, poi il riquadro si arrende e chiede all'utente: senza
  questo conteggio, un server che rifiuta il circuito *anche* a pagina nuova farebbe un ciclo che non si
  riesce nemmeno a leggere.

⚠️ **`autostart="false"` e `vipi-riconnessione.js` sono una cosa sola.** Se un pacchetto portasse il primo
senza il secondo — dimenticato in un caricamento FTP, o l'indice `staticwebassets` scambiato senza i `.js`,
che è già successo — il sito si vedrebbe **intero e non risponderebbe a niente**, senza un errore in
pagina. Il presidio è `RiconnessioneTests.Chi_spegne_lavvio_automatico_deve_riaccenderlo`, che guarda
l'HTML servito e l'**ordine** dei due tag.

### 3. Il colpetto che tiene sveglio il processo — `/vsop/ping`

Ogni scheda **visibile** chiede `/vsop/ping` ogni due minuti e mezzo. A Passenger, per non spegnere,
serve *una richiesta qualsiasi*: l'endpoint risponde `204` e basta.

⚠️ **Non è una sonda e non deve diventarlo.** `/vsop/health/ready` fa due query: usarlo qui vorrebbe dire
tenere sveglio il processo pagandolo in interrogazioni al database ogni due minuti e mezzo **per scheda
aperta** — risolvere un problema comprandone un altro.

ℹ️ Solo a scheda visibile: una scheda dimenticata in fondo alla barra non deve tenere acceso un processo
per giorni, e i browser strozzano comunque i timer in secondo piano. Al ritorno sulla scheda si bussa
subito, così la prima cosa che l'utente fa non paga l'avvio.

**Complemento fuori dal codice, non fatto qui:** un pinger esterno (UptimeRobot o simile) ogni 5 minuti
sullo stesso indirizzo terrebbe il processo caldo *anche quando non c'è nessuno* — e direbbe a noi quando
il sito è giù davvero. Va deciso con Ivao.It. Vedi `lavori-aperti.md`.

### 4. I tempi del canale, dai due capi

| | Prima | Ora | Perché |
|---|---|---|---|
| `DisconnectedCircuitRetentionPeriod` | 2 min | **5 min** | è la finestra in cui un buco di rete si ricuce **con lo stato intatto**; a 2 minuti metà dei tentativi non trovava più niente |
| `ClientTimeoutInterval` | 30 s (default) | **60 s** | un telefono che cambia cella sta zitto più di trenta secondi, e quella pausa costava una pagina |
| `KeepAliveInterval` | 15 s (default) | **15 s**, scritto | è il traffico che impedisce a Cloudflare o a nginx di chiudere un WebSocket aperto su una pagina ferma |
| `HandshakeTimeout` | 15 s (default) | **30 s** | il primo visitatore dopo una pausa di Passenger la paga mentre il processo si sta ancora avviando |

⚠️ Gli stessi due numeri stanno **dai due capi**: `ATTESA_SERVER_MS` e `POLSO_MS` in
`vipi-riconnessione.js`. Se divergono, vince il più impaziente.

⚠️ La retention **non serve a niente quando muore il processo**: i circuiti trattenuti muoiono con lui.
Vale per i buchi di rete, che sono l'altra metà dei casi. Alzare `DisconnectedCircuitMaxRetained` (25)
sarebbe stato il rimedio sbagliato allo stesso sintomo.

## Cosa NON è stato fatto

- **Meno circuiti in giro.** Una pagina SSR statica non ha circuito e quel riquadro non può vederlo. Le
  pagine pubbliche lo sono già (doc 14); resta da rivedere quali schermate admin abbiano davvero bisogno di
  `InteractiveServer`. È un lavoro a parte, e va fatto **dopo** aver letto `avvii.txt`.
- **Il pinger esterno** (§3): decisione di Ivao.It, non nostra.

## Verifica

- Suite verde; 19 test nuovi fra `RegistroAvviiTests` e `RiconnessioneTests`.
- ✅ **Verifica dal vivo eseguita il 31 agosto 2026**, Edge guidato con puppeteer-core su una copia del DB
  (skill `verifica-live`), script `riconn-verifica.js`. Il processo è stato **ucciso e riavviato** mentre
  una pagina era aperta:

| Controllo | Esito |
|---|---|
| `blazor.web.js` con `autostart="false"` e il nostro file caricato dopo | ✅ |
| Il circuito si apre lo stesso — cioè `Blazor.start` l'ha chiamato il nostro file | ✅ `/_blazor/negotiate` osservata, zero marcatori Blazor rimasti nel DOM |
| I numeri veri in pagina (`window.vipiRiconnessione`) | ✅ 55 tentativi, 5 000 ms, colpetto 150 000 ms |
| Il riquadro c'è ed è **nascosto** a pagina sana | ✅ `display: none` |
| `/vsop/ping` | ✅ `204`, `cache-control: no-store` |
| **Processo ucciso e rinato → la pagina si ricarica da sola** | ✅ **in 10 secondi**, senza toccare niente |

⚠️ **La verifica ha trovato due cose che i test non vedevano:**

1. **`/vsop/ping` rispondeva `405` a `HEAD`** — provato con `curl -I`. I servizi di sorveglianza esterni
   (§3, il pinger che resta da decidere) bussano in HEAD per default: sarebbe stato un «non funziona» che
   somiglia a un guasto nostro. Ora l'endpoint accetta GET **e** HEAD, con un test suo.
2. **«Retrying: /»** — i due contatori li riempie Blazor *solo mentre sta riprovando*, e prima nel markup
   resta una barra sola in mezzo alla frase. Nascosta con `:has(#components-reconnect-current-attempt:empty)`;
   dove `:has` non arrivasse, si torna a vedere la barra, che è il peggio che possa succedere.

ℹ️ Nella prova le frasi sono uscite **in inglese**: Edge manda `Accept-Language: en-US` e la pagina lo
rispetta. È il comportamento giusto — l'italiano è coperto dal test con `?culture=it`.

## Dove sta

| Cosa | File |
|---|---|
| Registro degli avvii | `src/Vipi.Host/RegistroAvvii.cs`, chiamato in `VipiStartup` (avvio + `ApplicationStopping`) |
| Avvio di Blazor, riquadro, colpetto | `src/Vipi.Ui/wwwroot/vipi-riconnessione.js` |
| Markup del riquadro | `src/Vipi.Host/Components/App.razor` (`#components-reconnect-modal`) |
| Aspetto e stati | `src/Vipi.Ui/wwwroot/vipi-theme.css`, blocco «Riconnessione del circuito» |
| Frasi | `SharedResource[.en].resx`, chiavi `Reconnect_*` |
| Endpoint del colpetto | `src/Vipi.Hosting/VipiModuleExtensions.cs` (`/vsop/ping`), esente in `LegacyRoutes` |
| Tempi del circuito e del canale | `src/Vipi.Host/VipiStartup.cs` |
| Test | `tests/Vipi.E2E.Tests/RegistroAvviiTests.cs`, `RiconnessioneTests.cs` |
