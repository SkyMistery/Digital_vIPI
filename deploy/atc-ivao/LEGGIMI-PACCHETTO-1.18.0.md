# Pacchetto 1.18.0 — solo i file cambiati

> **Timbro:** `1.18.0 · eff062e` (9 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.17.0**, online dal 9 settembre mattina. **19 file.**
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
> ## ⚠️ `wwwroot`: TRE file, e ognuno va coi suoi
>
> Cambiano `vipi-theme.css`, `vipi-print.css` e `vipi-editor.js`. Ciascuno va caricato **coi suoi `.br` e
> `.gz`**, e tutti e tre **insieme** a `Vipi.Host.staticwebassets.endpoints.json`. Quell'indice dice al sito
> con che nome chiedere ogni file: caricarne uno e non l'altro fa chiedere nomi che non esistono, e la
> pagina esce senza stile.
>
> ℹ️ Nessun altro asset entra, e non è una deduzione: le due cartelle pubblicate sono state confrontate
> **per impronta, intere** — su 90 file gli unici diversi sono questi tre.
>
> ## ⚠️ Ci sono frasi cambiate
>
> `en/Vipi.Ui.resources.dll` entra: i cinque tasti nuovi della barra hanno le loro descrizioni, e ci sono le
> frasi di §CI e §CK. Senza quel file, chi legge in inglese vedrebbe **tasti senza spiegazione**.
>
> ## ℹ️ `Vipi.Infrastructure.dll` c'è, e stavolta è giusto
>
> Nel pacchetto precedente non c'era. Qui le classi che leggono e scrivono le aree hanno guadagnato la
> **chiave di sezione**, che è ciò che permette a due visualizzatori di aree di stare nello stesso
> documento senza pestarsi.

---

## 🟢 1. La barra per scrivere il testo

**Dove:** in **ogni** campo di testo libero dei documenti — il paragrafo, il riquadro Callout, la nota sotto
un allegato, e l'introduzione VFR dell'APP.

Sopra il campo adesso c'è una fila di cinque tasti:

| | |
|---|---|
| **G** | grassetto — `Ctrl+B` |
| **C** | corsivo — `Ctrl+I` |
| **S** | sottolineato — `Ctrl+U` |
| **•** | elenco puntato |
| **1.** | elenco numerato |

Si seleziona il testo e si preme il tasto. **Premendolo una seconda volta si toglie** quello che ha messo,
come in un elaboratore di testi. Per gli elenchi basta che il cursore tocchi le righe: le marca tutte
quelle che la selezione sfiora, e premuto di nuovo le smarca.

La barra è **sempre lì**, appena smorzata, e si accende quando si entra nel campo. Non compare e non
sparisce: se comparisse, tutto quello che sta sotto scivolerebbe in giù ogni volta.

ℹ️ **Si può continuare a scrivere a mano**, e chi lo faceva già non deve cambiare niente:
`**grassetto**`, `*corsivo*`, `__sottolineato__`; una voce di elenco è una riga che comincia con `- ` oppure
con `1. `. Tutto è scritto anche nella **Guida**, alla voce «I blocchi».

## 🟢 2. Gli elenchi adesso si vedono come elenchi

Prima una riga che cominciava con un trattino restava una riga con un trattino. Adesso diventa un **elenco
vero**, coi pallini rientrati — sullo schermo e **anche sulla carta**, dove una voce non si spezza più a
metà fra due pagine.

ℹ️ **Gli elenchi che avete già scritto a mano coi «•» diventano elenchi veri da soli**: non c'è niente da
riscrivere.

## ⚠️ 3. Due difetti del testo che nessuno aveva segnalato

Sono venuti fuori rifacendo il pezzo che legge il testo, e valgono per **tutti** i documenti già scritti:

- **Due capoversi separati da una riga vuota potevano uscire attaccati**, in un blocco solo. Dipendeva da
  come il computer di chi scriveva segnava la fine della riga: succedeva battendo il testo su Windows, cioè
  quasi sempre. Adesso non succede più.
- **Un asterisco dimenticato** metteva in corsivo tutto quello che veniva dopo, fino al successivo — anche
  per righe e righe. Adesso al massimo se ne perde la sua riga.

ℹ️ **Una segnalazione resta aperta**, e va detto invece di lasciarla intendere: «in alcuni documenti il testo
va tutto su una riga sola», vista in una sezione personalizzata della vIPI di **LIMC**. **Non siamo riusciti
a riprodurla**: il pezzo che manda a capo è stato provato eseguendolo, il testo nel database ha i suoi a
capo, la traduzione li conserva, e non c'è nessuna regola di stile che li tolga. Quello che questo pacchetto
aggiunge è che ora quel percorso è **presidiato da venti controlli automatici**: se ricapita, sapremo dove
guardare. Se la rivedete, serve sapere **quale pagina, quale blocco e in che lingua**.

## 🟢 4. Le frequenze si riordinano in tutti gli editor

Prima si potevano trascinare **solo** nelle vIPI di ACC e negli APP. Adesso anche nelle **vLOA**, negli
**aeroporti** e nei **vSOP militari**: si prende la maniglia `⠿` e si trascina, o si usano `↑`/`↓`.

⚠️ Nella **vLOA** l'ordine si applica **dentro ciascun lato**: le due tabelle («IT · LIRR» e l'estera) restano
separate, perché una frequenza estera sotto il titolo italiano non sarebbe un difetto di aspetto — sarebbe
un documento che dice il falso.

⚠️ Negli **aeroporti** e nei **militari** si riordinano **solo le righe collegate a quel documento**:
l'anagrafica generale delle frequenze non si tocca da lì.

## 🟢 5. Il tasto «Add» del glossario adesso dice perché è spento

Segnalato: *«non mi fa cliccare su Add… forse è protetto dal lock, ma non c'è il tasto per sbloccarlo»*.

**Quella pagina non ha nessun lock.** Il tasto è spento perché manca qualcosa alla voce: uno dei due lati,
meno di quattro caratteri, una formula già presente, oppure un carattere che non può starci. Il motivo
c'era, ma stava scritto **sotto** il modulo — e un tasto grigio senza spiegazione **accanto** non si legge
come una regola: si legge come un permesso che manca.

Adesso il motivo compare **passandoci sopra col mouse**, in ogni caso, modulo vuoto compreso. Stessa cosa
sul «Salva» della riga che si sta correggendo.

## 🟢 6. Le aree BOAT hanno un visualizzatore loro

Nei vSOP militari, dentro **«Aree di lavoro»**, la sotto-sezione **«Bassa quota (BOAT)»** adesso disegna
mappa, elenco e tabella per conto suo, con una selezione di aree indipendente da quella del padre.

🔴 **Nasce vuota, e lo sappiamo.** Nel catalogo IVAO ci sono **241** aree speciali e **zero** BOAT; nei dati
dell'AIP, 3 072 volumi e **zero** BOAT. Le aree BOAT oggi stanno solo dentro i PDF dei SOP. Il
visualizzatore è pronto: si riempirà il giorno che quelle aree esisteranno da qualche parte. Non è un
difetto — è stato misurato **prima** di costruirlo, e la scelta è stata di farlo lo stesso.

---

## Il controllo dopo il caricamento

⚠️ **Il timbro non basta.** `diagnostica/avvio-diagnostica.txt` dice quale versione è partita, non che il
sito funzioni.

1. **La Ricerca**: si apre `https://atc.it.ivao.aero/services/vsop/search?q=li`. Deve comparire
   **«N risultati per li»** con l'elenco sotto. È il controllo che conta, perché passa dal **server**.
2. **La barra del testo** (serve il login): si apre un documento in modifica e si guarda un blocco di
   paragrafo. Sopra il campo devono esserci **cinque tasti**: `G C S • 1.`. Si scrive una parola, la si
   seleziona, si preme **G** — nel campo deve comparire `**parola**`.
3. **L'elenco si vede** (serve il login): sempre in quel campo, si scrivono tre righe, si selezionano tutte e
   si preme **•**. Si esce dalla modifica: sotto devono comparire **tre pallini rientrati**, non tre trattini.
4. **Una pagina qualsiasi con lo stile giusto**: se esce senza colori, manca un foglio o l'indice.
5. **Il sito in inglese** (`EN` in alto): passando sopra i tasti della barra deve comparire la descrizione
   (**Bold**, **Italic**, …). Se non compare, il `.dll` delle frasi non è arrivato.

⚠️ E dopo aver toccato i file: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne
accorge.

⚠️ **Il prossimo `errori-richieste.txt` resta la prova che conta** per le correzioni di 1.17.0, e non è a
schermo: le voci `ObjectDisposedException` devono **calare**. Mandatecelo quando potete.

---

## I 19 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

| | |
|---|---|
| `Vipi.Application.dll` + `.pdb` | l'ordine delle frequenze della vLOA, e le aree lette per chiave di sezione |
| `Vipi.Infrastructure.dll` + `.pdb` | chi scrive le aree sul database, ora sapendo **quale** sezione |
| `Vipi.Ui.dll` + `.pdb` | la barra del testo, gli elenchi, il riordino, il glossario, le aree BOAT |
| `Vipi.Host.dll` + `.pdb` | l'avvio, e il **timbro** della versione |
| `en/Vipi.Ui.resources.dll` | le frasi inglesi (le descrizioni dei cinque tasti, e le altre nuove) |
| `Vipi.Host.staticwebassets.endpoints.json` | l'indice dei file di `wwwroot` — **va insieme ai nove qui sotto** |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` (+ `.br`, `.gz`) | la barra, gli elenchi, le maniglie del riordino |
| `wwwroot/_content/Vipi.Ui/vipi-print.css` (+ `.br`, `.gz`) | sulla carta, una voce d'elenco non si spezza fra due pagine |
| `wwwroot/_content/Vipi.Ui/vipi-editor.js` (+ `.br`, `.gz`) | i gesti dei cinque tasti e le scorciatoie da tastiera |
