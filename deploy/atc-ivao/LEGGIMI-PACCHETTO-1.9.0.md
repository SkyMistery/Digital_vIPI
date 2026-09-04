# Pacchetto 1.9.0 — solo i file cambiati

> **Timbro:** `1.9.0 · f75e2e0` (4 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.8.1.** **15 file.**
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
> ## ⚠️ MA QUESTA VOLTA C'È `wwwroot`, E I QUATTRO FILE VIAGGIANO INSIEME
>
> È l'unica trappola vera del pacchetto, ed è la stessa del 24 agosto. Questi quattro devono arrivare
> **tutti**, nello stesso caricamento:
>
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css`
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css.br`
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css.gz`
> - `Vipi.Host.staticwebassets.endpoints.json`
>
> L'ultimo è l'**indice**: dice al sito con che nome chiedere ogni foglio di stile. Caricare il foglio senza
> l'indice — o l'indice senza il foglio — fa chiedere al browser un nome che sul server non esiste, e la
> pagina esce **senza grafica**.
>
> ## ⚠️ E ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono 53 frasi nuove (italiano e inglese). Senza quel file la parte nuova
> si vedrebbe in italiano anche a chi legge in inglese.

---

## Che cosa c'è dentro, in due parti

### 1. «A che punto è la traduzione» — la domanda che finora non aveva risposta

Fino a oggi lo stato della traduzione si poteva sapere **un documento per volta, e solo aprendolo** nella
lingua di lettura. Adesso c'è una tabella che lo dice per tutti.

Sta in **Fraseologia e traduzioni** (`/services/vsop/admin/glossary`), come terza sezione, e per ogni
documento dice **due** percentuali:

| colonna | che cosa dice |
|---|---|
| **Bozza** | quel che state per pubblicare |
| **Pubblicato** | quel che un lettore vede **adesso** |

⚠️ **Non sono la stessa cosa, e non si fondono in un numero solo.** Un documento può essere tradotto al 100%
nella bozza e al 40% in quel che il pubblico legge: è quel che succede pubblicando prosa nuova prima che il
giro di traduzione sia passato. Una media direbbe «70%», che non descrive nessuno dei due.

Nella stessa sezione, sopra la tabella: **quando è passato l'ultimo giro**, quanto manca al prossimo, e
com'è andato (quante frasi nuove, quante erano già in memoria, quante sono tornate rotte). Quel resoconto
esisteva già, ma finiva **solo nel registro del server** — cioè per saperlo bisognava scaricare i file di
diagnostica.

### 2. «Quanto ci vuole?» — e il tasto per non aspettare

Dentro l'editor, nel pannello **Traduzione**, ora c'è scritto quante frasi mancano e **fra quanto passa il
giro** — al massimo un quarto d'ora. Non è una stima: il giro spedisce tutte le frasi mancanti in una volta
sola, quindi il numero di frasi appena scritte non allunga l'attesa.

E accanto c'è **«Traduci ora»**, se non volete aspettare: chiede al motore le frasi mancanti **di quel
documento soltanto**. Lo può premere chi può scrivere il documento.

ℹ️ **Non serve aspettare il giro per pubblicare.** Le frasi che la pubblicazione non ha ancora tradotto non
restano bloccate: si leggeranno tradotte appena il giro sarà passato, senza ripubblicare. Il pannello di
pubblicazione ora lo dice.

### 3. Il riquadro «ricarica la pagina» che compariva nell'area riservata

Segnalato da voi oggi. **Non era il sito che si spegneva**: nella finestra della vostra segnalazione il
processo è rimasto acceso 26 minuti e poi 86. A cadere era il **collegamento** della pagina, e a farlo
cadere era codice nostro. Tre cause, tutte corrette:

- l'editor smaltiva un semaforo interno **mentre un caricamento era ancora in corso** — cinque volte in
  un'ora;
- l'editor dei vSOP militari leggeva l'anagrafica delle radioassistenze sul collegamento **condiviso** con
  la pagina, e le due letture si pestavano i piedi;
- e una terza famiglia di errori era solo **rumore** che riempiva `errori-richieste.txt` nascondendo quelli
  veri.

⚠️ **Il processo che si spegne ogni ~50 secondi resta**: è un'altra cosa, e la strada è il pannello
dell'hosting, dopo il 16 settembre. Ma non era quello a rompervi la sessione di oggi.

---

## Come si controlla che sia andata

> ### A · La versione
>
> `diagnostica/avvio-diagnostica.txt`, riga `Versione`: deve dire **`1.9.0 · f75e2e0`**.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.
>
> ⚠️ Questo è il controllo che conta: il selettore della lingua, lo zoom e il tema funzionano **anche** su un
> sito caricato a metà, perché non passano dal server. La Ricerca sì.

> ### C · La grafica è arrivata
>
> Una pagina qualsiasi del sito deve avere **i suoi colori e i suoi riquadri**. Se esce come testo nudo su
> fondo bianco, sono i quattro file di `wwwroot` che non sono arrivati insieme: ricaricateli tutti e quattro.

> ### D · La parte nuova
>
> `https://atc.it.ivao.aero/services/vsop/admin/glossary`, in fondo alla pagina: la sezione **Documenti**.
> Apritela — nasce chiusa — e deve riempirsi con l'elenco dei documenti e due percentuali per riga.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi i quattro di `wwwroot` **insieme**, poi riavvio. **Nessuna conseguenza sul database**, che questo
pacchetto non tocca.

---

## Una cosa che la tabella nuova vi dirà subito, e che va decisa da voi

Appena aprite la sezione **Documenti** vedrete, accanto alla colonna «Pubblicato», la pastiglia **«non
congelata»** su **tutte** le release in vigore. Non è un guasto di questo pacchetto: è una fotografia.

Vuol dire che, per quei documenti, la traduzione che il pubblico legge **non è quella fissata al momento
della pubblicazione** ma quella corrente. Conseguenza pratica: correggere una frase in un documento cambia
quella stessa frase anche negli altri documenti pubblicati che la contengono, sotto gli occhi di chi li sta
leggendo.

**Si chiude ripubblicando** quei documenti, quando vi torna comodo. Non è urgente e non rompe niente: è una
scelta editoriale, e adesso finalmente **si vede**.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**; **10 377 test** verdi su **quindici** assiemi (nove progetti sui due
  runtime), **E2E compresi** (300);
- la parte nuova **guidata in un browser** su una copia del vostro database: la sezione Documenti che si
  riempie, la riga dell'attesa nell'editor, il tasto «Traduci ora» premuto davvero (una frase tradotta, e la
  spesa registrata col suo nome), la pastiglia su `/services/vsop/versions`;
- e la correzione del riquadro «ricarica la pagina» ha un **controllo che morde**: rimesso il difetto, il
  test cade. Un controllo che non si è mai visto fallire non prova niente.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **Di 1.8.1 non abbiamo ancora un dato.** Il file `errori-richieste.txt` che ci avete mandato si ferma un
   minuto prima che 1.8.1 partisse: tutti gli errori là dentro sono di **prima**. Quello che ci serve resta
   lo stesso — **`errori-richieste.txt` fra qualche giorno**.
2. **Il processo che si spegne ogni ~50 secondi** non lo tocca questo pacchetto.
3. **Le altre pagine interattive** restano come sono: sono contate, e convertirle è un lavoro a parte.
