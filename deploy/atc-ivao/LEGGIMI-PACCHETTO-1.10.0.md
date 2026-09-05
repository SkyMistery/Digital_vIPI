# Pacchetto 1.10.0 — solo i file cambiati

> **Timbro:** `1.10.0 · bdd81e9` (5 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.9.0.** **16 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete, anche dentro la finestra cieca fino al 16.
>
> ## ⚠️ C'È `wwwroot`, E STAVOLTA I FILE CHE VIAGGIANO INSIEME SONO SETTE
>
> È l'unica trappola vera del pacchetto, ed è la stessa del 24 agosto. Questi sette devono arrivare
> **tutti**, nello stesso caricamento:
>
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css` + `.br` + `.gz`
> - `wwwroot/_content/Vipi.Ui/vipi-editor.js` + `.br` + `.gz`
> - `Vipi.Host.staticwebassets.endpoints.json`
>
> L'ultimo è l'**indice**: dice al sito con che nome chiedere ogni foglio di stile e ogni script. Caricarne
> uno senza l'indice — o l'indice senza gli altri — fa chiedere al browser nomi che sul server non esistono,
> e la pagina esce **senza grafica** o con l'editor che non risponde.
>
> ## ⚠️ E ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le frasi del selettore di postazione e della ricerca SID (italiano
> e inglese). Senza quel file la parte nuova si vedrebbe in italiano anche a chi legge in inglese.

---

## 🔴 DUE COSE CHE SEMBRERANNO GUASTI, E NON LO SONO

Vanno dette a chi usa il sito **prima** che ve le segnali, perché sono due cambiamenti voluti che al primo
colpo si leggono come rotture.

### 1. L'editor d'aeroporto **non ha più i tasti «Salva»**

Erano **nove**, più «Salva tutto (N)» e Ctrl+S. Adesso **ogni gesto scrive da solo**: si cambia un valore e
il valore è salvato, come negli altri documenti.

E in cambio i campi **si scrivono solo dopo aver premuto «Modifica»**. Prima si poteva digitare in tutta la
pagina senza aver preso il documento in mano, e ci si accorgeva del problema solo al salvataggio.

⚠️ Chi apre quell'editor cercando il tasto «Salva» penserà che sia rotto. **Non lo è: il tasto non serve
più.**

### 2. La **vista live di un collega non si apre più**

Fino a 1.9.0 `/services/vsop/live/<callsign>` era **aperta a chiunque**, anche a chi non aveva fatto
l'accesso: bastava scrivere un callsign nell'indirizzo.

Adesso:

| chi | che cosa apre |
|---|---|
| un controllore qualsiasi | **solo la propria** postazione. Qualunque altro callsign lo riporta in silenzio alla sua (`/services/vsop/live`) |
| staff di divisione in su | **qualsiasi** postazione, e c'è un **campo di ricerca** in alto per sceglierla — anche una postazione spenta, resa come se fosse aperta |

⚠️ Chi aveva nei preferiti il link al live di un collega verrà rimandato alla propria pagina, **senza
messaggio**. È voluto, non è un guasto.

---

## Che cosa c'è dentro, in tre parti

### 1. L'editor d'aeroporto: una porta sola, e i campi vivi solo col lock

Oltre a quanto detto sopra: la guardia che decide se si può scrivere adesso sta **nel servizio**, non nel
bottone. Prima il cancello non era il lock del documento ma il **ruolo sulla ACC**: due persone potevano
scrivere sullo stesso scalo credendo di averlo in mano.

⚠️ Conta doppio su questo hosting: il processo si rigenera ogni ~50 secondi, e un salvataggio che aspettava
un bottone si perdeva insieme al circuito.

### 2. La vista live: chiusa a chi passa, e scelta da chi assiste

Oltre al cancello: da **staff di divisione** in su, il campo in alto apre **qualsiasi** postazione dei
cataloghi. Serve per assistere qualcuno guardando quel che vede lui, e funziona anche su una postazione
**spenta** — la pagina si compone dai cataloghi, non dalla connessione.

ℹ️ La pastiglia in alto adesso è **verde solo se quella postazione è davvero aperta**; altrimenti è grigia e
dice «chiuso». Prima era verde sempre.

### 3. La vista rapida d'aeroporto: si cercano le SID

Nel riquadro «vista rapida» (quello che si apre dalla vista live) c'è un **campo di ricerca**: si scrive un
punto, un codice SID o una transition e la tabella si stringe a quelle righe. Con il conteggio accanto e una
✕ per rimettere tutto.

E la tabella ora porta **Transition** e **Condition**, come il documento completo dell'aeroporto.

🔴 **E c'è una correzione che vale più di tutto il resto di questo punto.** Quella tabella era costruita per
conto suo, in modo diverso dal documento: la nota **«to coord with APP»** sulla quota iniziale **non
compariva mai**. Su Bari (LIBD) sono **7 righe su 20** che la portano. Chi leggeva la vista rapida vedeva una
quota da rispettare dove il documento dice «da concordare con l'avvicinamento».

ℹ️ La colonna **Condition** oggi esce vuota («—») su tutti gli scali: è un campo che si compila
nell'editor, e nessuno l'ha ancora compilato. Il documento completo la mostra vuota allo stesso modo.

---

## Dopo il caricamento

> ### A · Riavvio
>
> `tmp/restart.txt` (toccatelo o ricaricatelo) **e poi aprite il sito una volta**: senza una visita,
> Passenger non si accorge del segnale.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.
>
> ⚠️ Questo è il controllo che conta: il selettore della lingua, lo zoom e il tema funzionano **anche** su un
> sito caricato a metà, perché non passano dal server. La Ricerca sì.

> ### C · La grafica è arrivata
>
> Una pagina qualsiasi deve avere **i suoi colori e i suoi riquadri**. Se esce come testo nudo su fondo
> bianco, sono i sette file di `wwwroot` che non sono arrivati insieme: ricaricateli tutti e sette.

> ### D · Le parti nuove
>
> 1. **Vista live**: aprite `https://atc.it.ivao.aero/services/vsop/live`. Se siete staff di divisione, in
>    alto c'è il campo «Apri un'altra postazione…»: scriveteci `LIRR` e sceglietene una — la pagina deve
>    cambiare indirizzo e riempirsi.
> 2. **Vista rapida**: nella stessa pagina aprite un aeroporto e provate il campo di ricerca sopra la
>    tabella SID; la tabella deve stringersi mentre scrivete.
> 3. **Editor d'aeroporto**: apritene uno, premete «Modifica», cambiate un valore e **ricaricate la pagina
>    senza salvare**. Il valore dev'esserci ancora.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi i sette di `wwwroot` **insieme**, poi riavvio. **Nessuna conseguenza sul database**, che questo
pacchetto non tocca.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**, sui due runtime; **5 438 test** verdi su **quattordici** assiemi, **E2E
  compresi** (300);
- le due parti nuove **guidate in un browser** su una copia del vostro database, a **tre identità diverse**
  (un utente qualsiasi scollegato, un utente collegato a una postazione, uno staff di divisione): il
  rimando funziona, il selettore apre le postazioni, la ricerca SID stringe la tabella;
- e la ricerca è stata provata anche **cancellando in fretta**, che è il modo in cui si è scoperto che il
  campo perdeva i tasti.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **Di 1.8.1 e 1.9.0 non abbiamo ancora un dato dal vivo.** Quel che ci serve resta lo stesso:
   **`diagnostica/errori-richieste.txt` fra qualche giorno**.
2. **Il processo che si spegne ogni ~50 secondi** non lo tocca questo pacchetto: resta la strada del
   pannello dell'hosting, dopo il 16 settembre.
3. **Le 17 release in vigore senza traduzione congelata** restano come sono: si chiude ripubblicando, ed è
   una scelta vostra.
