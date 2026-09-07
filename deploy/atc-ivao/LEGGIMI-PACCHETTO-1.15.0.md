# Pacchetto 1.15.0 — solo i file cambiati

> **Timbro:** `1.15.0 · 31fd4194` (7 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.14.2**, online dal pomeriggio del 7. **28 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione, nessuna tabella e nessuna colonna nuova**: niente da concordare con chi amministra
> il database, nessuna copia di sicurezza, nessuna finestra da aspettare. Si carica quando volete, anche
> dentro la finestra cieca fino al 16.
>
> ## 🔴 MA QUESTA VOLTA I FILE VANNO CARICATI **TUTTI INSIEME**
>
> È la differenza vera fra questo pacchetto e i precedenti, e vale la pena leggerla prima di cominciare.
>
> Dentro c'è una ripulitura del codice che ha reso **privati 74 tipi** che prima erano visibili fra un
> pezzo e l'altro dell'applicazione. Un pezzo **vecchio** lasciato accanto a uno **nuovo** non dà nessun
> errore mentre si carica: **esplode quando qualcuno apre la pagina**, perché a quel punto cerca un tipo
> che non c'è più.
>
> In pratica: se il caricamento si interrompe a metà, **non riavviate**. Finite di caricare i 28 file, poi
> rinominate, poi riavviate. Se qualcosa va storto a metà strada, è più sicuro rimettere i file di 1.14.2
> (stanno nel pacchetto precedente) che restare mescolati.
>
> ## ⚠️ CI SONO FOGLI DI STILE, E VIAGGIANO CON IL LORO INDICE
>
> Cambiano **quattro** file di `wwwroot` (`vipi-theme.css`, `vipi-print.css`, `vipi-aor3d.css`,
> `vipi-tour.js`), ognuno **con i suoi `.br` e `.gz`**, e insieme a
> **`Vipi.Host.staticwebassets.endpoints.json`**. Quell'indice dice al sito **con che nome** chiedere ogni
> foglio: caricarne uno senza l'altro fa chiedere nomi che non esistono, e la pagina esce senza grafica.
> Sono già tutti e tredici nell'elenco: basta non saltarne nessuno.
>
> ## ⚠️ Ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra. Senza quel file, chi legge in inglese vedrebbe in italiano le etichette
> nuove.

---

## ✨ LE TRE COSE DI QUESTO PACCHETTO

### 1. Unendo due documenti dello stesso scalo, si sceglie chi tiene le sezioni in comune

Unendo la **vIPI di un aeroporto** e il suo **vSOP militare**, la pagina ripeteva le stesse sezioni due
volte: METAR, frequenze, piste, quote di transizione, procedure generali, carte. Due volte lo stesso dato,
e spesso scritto bene una volta sola.

Adesso, appena si uniscono, si apre una scheda: **«Sezioni in comune»**. Si sceglie **quale documento le
tiene**, e nell'altro vengono **nascoste** — esattamente come farebbe una persona premendo «nascondi»
sezione per sezione, e si rimettono allo stesso modo.

- La scheda **resta disponibile**: il tasto «Sezioni in comune…» sta nel pannello dell'unione, perché una
  sezione nuova può nascere anche domani.
- ℹ️ **«Validità e revisione» è in elenco ma non spuntata**, di proposito: dice il ciclo AIRAC e la release
  **di quel documento**, e in un'unione sono due.
- ℹ️ Riaprendo la scheda, ogni riga dice **«già nascosta»**, e la proposta tiene conto di com'è adesso: chi
  riapre e conferma **non ribalta** la scelta di prima.

### 2. Accordi di coordinamento: la copia di una riga si fa **dalla riga**

Nella pagina dei trasferimenti, ogni riga di clausola ha ora **quattro** tasti: `⑂` alternativa, `↳`
eccezione, **`⧉` copia questa riga**, `✎` apri nel pannello. Prima, per duplicare una riga bisognava
aprirla nel pannello di destra.

⚠️ **Non è il `⧉` del pannello** (che ora si legge `⧉⑂`): quello copia **tutto il gruppo di varianti**, e
su una riga senza varianti non faceva niente. Quello sulla riga copia **la riga**, condizione compresa, e
la mette subito sotto.

### 3. Il filtro «Tutto · Pilota · ATC» non sparisce più nelle pagine unite

Se le sezioni marcate per il pilota o per il controllore stavano **solo sul secondo** documento di
un'unione, i tre comandi **non comparivano**. Il filtro funzionava lo stesso scrivendo l'indirizzo a mano:
mancava il modo di chiederlo con un clic.

---

## 🧹 E sotto: la revisione del 6-7 settembre

Trentuno correzioni su trentatré, più la ripulitura dei tipi di cui sopra. Le poche che si vedono da fuori:

- un **APP disattivato** non è più raggiungibile dal pubblico;
- la **biblioteca degli allegati** ha lo stesso cancello che hanno le altre pagine riservate;
- il **convertitore di coordinate** non tronca più in silenzio un anello scritto male, e i primi oltre 59
  non entrano più;
- il **livello di crociera** si dichiara in FL e viene controllato.

---

## ✅ Che cosa guardare dopo aver caricato

Il timbro dice quale versione è partita, **non** che il sito funzioni: la prova che conta è la **Ricerca**
(`/services/vsop/search`, due lettere nel campo in alto, la riga sotto deve cambiare), perché passa dal
server.

Poi, per questo pacchetto:

1. **Una pagina qualsiasi con la grafica giusta.** È il controllo dei fogli di stile: se l'indice e i fogli
   non sono arrivati insieme, la pagina esce **senza grafica** e si vede subito.
2. **Timbro**: `diagnostica/avvio-diagnostica.txt` deve dire `1.15.0 · 31fd4194`.
3. Con occhi da **amministratore**, se avete un minuto: aprite l'editor di un aeroporto che ha anche il
   vSOP militare, entrate in modifica, unite i due documenti e guardate che compaia la scheda **«Sezioni in
   comune»**. È la cosa nuova più visibile del pacchetto.
