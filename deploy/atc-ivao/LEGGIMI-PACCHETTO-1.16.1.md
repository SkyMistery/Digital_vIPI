# Pacchetto 1.16.1 — solo i file cambiati

> **Timbro:** `1.16.1 · 6c1108f` (8 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.16.0**, online dall'8 settembre. **13 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> Nessuna migrazione, nessuna tabella e nessuna colonna nuova. Si carica quando volete, anche dentro la
> finestra cieca fino al 16.
>
> ## ⚠️ `wwwroot`: UN file, ma va coi suoi
>
> Cambia **solo** `vipi-theme.css`, e va caricato **coi suoi `.br` e `.gz`**, **insieme** a
> `Vipi.Host.staticwebassets.endpoints.json`. Quell'indice dice al sito con che nome chiedere ogni file:
> caricarne uno e non l'altro fa chiedere nomi che non esistono, e la pagina esce senza stile.
>
> ℹ️ **`vipi-ui.js` NON c'è, ed è giusto**: è identico a quello di 1.16.0 — verificato per impronta, non a
> memoria. La finestra che ingrandisce le immagini è già online e non si tocca.
>
> ## ⚠️ Ci sono frasi cambiate
>
> `en/Vipi.Ui.resources.dll` entra: il tasto in barra si chiama diversamente e c'è un'etichetta nuova per il
> METAR. Senza quel file, chi legge in inglese vedrebbe il tasto **senza scritta**.

---

## 🔴 Il difetto che ha morso subito: «+ riga» cancellava le aree scelte

Nella sezione **«Aree di lavoro»** dei vSOP militari, sotto la tabella vera ne compariva **una seconda,
vuota**, con l'intestazione `＋` e un tasto **«+ riga»**. Premerlo **cancellava tutte le aree selezionate**,
senza un errore e senza che nulla lo dicesse.

Quella seconda tabella non era una tabella: era la **selezione delle aree** che l'editor scambiava per un
contenuto scritto a mano. Adesso non compare più, e il tasto non c'è.

ℹ️ **Riguardava anche le vIPI di ACC e APP**: stessa causa, stesso rimedio.

⚠️ **Chi l'ha già premuto ha perso quella selezione**: il dato è stato riscritto, non nascosto. Si rimette
con le chip — un minuto — e da questo pacchetto in poi non può più succedere.

---

## 🟢 Il METAR ha due sorgenti di scorta

Quando **NOAA non risponde**, il METAR adesso arriva lo stesso: si prova **IVAO**, e se non ce l'ha nemmeno
lui si prova **VATSIM**. Ci si ferma alla prima che risponde.

Accanto alla pastiglia verde `LIVE` compare **il nome della sorgente** (`IVAO` o `VATSIM`) **solo** quando il
METAR non viene da NOAA: se non c'è nessuna scritta, è la sorgente di sempre.

ℹ️ **Il TAF resta solo di NOAA**, e non è una dimenticanza: un servizio TAF gratuito e indipendente da NOAA
non esiste. Può quindi capitare di vedere il METAR (da IVAO o VATSIM) e il TAF non disponibile: è normale.

---

## 🟢 Il tasto in barra si chiama «Documenti»

In alto a destra il tasto che porta all'elenco di tutti i documenti si chiamava **«Editor»**, e si confondeva
con l'editor del singolo documento — che è un'altra cosa, sta dentro la pagina e si chiama **«Modifica»**.

Adesso si chiama **«Documenti»** e ha l'icona di un foglio invece della matita. Va nello stesso posto di
prima: niente cambia nell'uso.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni.

1. **La Ricerca**: si scrive nel campo in alto e si preme **Invio**, oppure si apre direttamente
   `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire **«N risultati per li»** con
   l'elenco sotto. È il controllo che conta, perché passa dal **server**.
   ⚠️ Non basta scrivere nel campo e guardare: quel campo **naviga**, non filtra la pagina sotto.
2. **Il tasto in alto a destra**: deve dire **«Documenti»** (o mostrare l'icona di un foglio, se la barra è
   stretta). Se dice ancora «Editor», il `.dll` delle frasi non è arrivato.
3. **Una sezione «Aree di lavoro»** di un vSOP militare, in modifica: sotto la tabella **non** ci deve più
   essere una seconda tabella vuota col tasto «+ riga».
4. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori, manca `vipi-theme.css` o l'indice.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

---

## I 13 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Application.dll` + `.pdb` | il bollettino meteo e la sua sorgente |
| `Vipi.Infrastructure.dll` + `.pdb` | le tre sorgenti METAR e la catena fra loro |
| `Vipi.Ui.dll` + `.pdb` | il payload che non è più una tabella da scrivere, e il tasto «Documenti» |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
| `en/Vipi.Ui.resources.dll` | le frasi inglesi |
| `Vipi.Host.staticwebassets.endpoints.json` | l'indice dei file di `wwwroot` — **va insieme ai tre qui sotto** |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` (+ `.br`, `.gz`) | lo stile, con la pastiglia della sorgente METAR |
