# Pacchetto 1.8.0 — solo i file cambiati

> **Timbro:** `1.8.0 · 8dc05f4` (4 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.7.1**, che è quello attualmente sul server. **11 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si può caricare anche adesso, mentre chi amministra il database è via.
>
> ## ⚠️ MA QUESTA VOLTA C'È `wwwroot`, E VIAGGIA CON IL SUO INDICE
>
> Il foglio di stile `vipi-theme.css` è cambiato. Quindi entrano **quattro** file che in 1.7.1 non c'erano:
> il foglio, le sue due copie compresse (`.br` e `.gz`) e **`Vipi.Host.staticwebassets.endpoints.json`**.
>
> 🔴 **Quei quattro si caricano tutti o nessuno.** L'indice dice al sito con che nome chiedere ogni file:
> scambiarne uno solo fa chiedere nomi che non esistono, e il sito esce **senza grafica**. È il difetto del
> 24 agosto, ed è l'unica trappola vera di questo pacchetto.

---

## Perché esiste questo pacchetto

Porta online **due giri di lavoro** che sono fermi da qualche giorno.

Il primo (**§BJ**) è una correzione nata leggendo la vostra diagnostica: il pannello **«Importa tabella»**
prendeva il collegamento al database dalla sessione del browser invece di aprirne uno suo, e con due
ricostruzioni ravvicinate mandava la pagina in errore. Quattro richieste in errore, lette nel registro del
3 settembre.

Il secondo (**§BK**) sono **cinque cose che avete chiesto voi**, più **due difetti** che sono saltati fuori
facendole — e uno dei due è più importante di tutte e cinque le richieste.

---

## 🔴 Il difetto che nessuno cercava: una pista notturna si spegneva a mezzanotte

Nelle **regole piste** si può scrivere una finestra oraria. Se scrivete **«lunedì, dalle 22:00 alle 05:00»**
intendete una notte sola: da lunedì sera fino alle cinque di **martedì** mattina.

Non funzionava così. Alle 03:00 di martedì il programma guardava **che giorno è oggi** — martedì — vedeva che
la regola dice «lunedì», e **smetteva di applicarla**. La regola si spegneva a mezzanotte, cioè a metà della
notte che descrive.

Adesso una finestra che scavalca la mezzanotte **appartiene al giorno in cui è cominciata**. E vale allo
stesso modo per i giorni pari/dispari e per i periodi stagionali: «le notti dei giorni dispari» non si spezza
più a mezzanotte.

> ⚠️ **Questo cambia il comportamento di regole già scritte e pubblicate**, ma **soltanto** di quelle che
> hanno l'orario di fine **prima** di quello d'inizio. Tutte le altre si comportano esattamente come prima.
>
> Vale la pena riguardare le vostre regole notturne dopo il caricamento: adesso fanno quello che dicono.

---

## Che cosa cambia per chi usa il sito

### 1 · Il banco di prova delle regole piste prova anche il **momento**

Nel pannello «Regole piste» c'è il banco che dice quale regola vince con un dato vento. Ora accanto al vento
c'è un campo **data e ora**, con un tasto **«Adesso»** e il giorno scritto per esteso.

Serviva: prima orario, giorni della settimana, pari/dispari e stagione erano **gli unici campi della regola
che non si potevano provare**. Si scrivevano e si aspettava lunedì notte per sapere se erano giusti.

ℹ️ E accanto all'orario di fine compare la scritta **«giorno dopo»** quando la finestra scavalca la
mezzanotte. Prima non lo diceva nessuno, e non era ovvio.

### 2 · Il numero della regola attiva lo vede solo chi le regole le scrive

Nella pagina di un aeroporto, sotto il titolo «Piste», c'era scritto:

> *verde gli arrivi e blu le partenze consigliati **dalla regola attiva «#2»**. Vento 10° / 8 kt.*

Da adesso quel **«dalla regola attiva «#2»»** — e la nota della regola, e il segno «adesso» nella tabella
delle regole — li vede solo chi è **staff di divisione** o più. A tutti gli altri resta:

> *verde gli arrivi e blu le partenze consigliati. **Vento 10° / 8 kt.***

⚠️ **Il vento resta a tutti**, e anzi si legge meglio: adesso ha lo stesso corpo, lo stesso colore e lo
stesso peso dei numeri della tabella qui sotto — prima era grigio piccolo come una didascalia, ed è invece
il dato che si va a cercare.

ℹ️ **La tabella delle regole resta pubblica**: è contenuto del documento. A sparire è solo il collegamento
col momento — «sta vincendo la #2» —, che per chi non scrive le regole è un numero che non dice niente.

### 3 · La colonna **WTC** non va più a capo

Nella tabella delle SID, una procedura valida per tutte le categorie di scia scrive `L, M, H, S`. Quella
colonna era stretta e il valore andava a capo su due righe — **anche su uno schermo da 1920**, perché a quella
larghezza la colonna del documento è già al suo minimo. Ora la colonna è più larga, e i cinque punti
percentuali li ha ridati «Condition», che è l'unica colonna di prosa e resta comunque la più larga.

### 4 · Cambiare il filtro delle SID **lascia cadere le righe scelte**

Nell'editor delle SID importate, i chip in alto filtrano per pista. La selezione però **sopravviveva** al
cambio di chip: la barra dei comandi («pubblica le scelte», «applica alle scelte») continuava ad agire su
righe che non erano più a schermo. Si pubblicava al buio, con un contatore che diceva 39 mentre a video ce
n'erano 3.

Adesso cambiare un chip azzera la selezione.

> ℹ️ **La ricerca no**, e non è una dimenticanza: si scrive un carattere per volta, e azzererebbe la
> selezione a ogni tasto.
>
> ℹ️ **E le righe che avete MODIFICATO e non ancora salvato non si toccano**: quelle restano, col loro
> colore. Buttare via lavoro non salvato cambiando vista è esattamente il difetto che vi era costato i TORA
> e gli LDA di Rimini.

### 5 · La biblioteca degli allegati si sfoglia per **cartelle**

Con centoventuno documenti in archivio l'elenco usciva piatto, nell'ordine in cui era stato caricato, e
mancava il filtro che serve davvero: **la sigla del perimetro**. «Gli allegati di un ACC» si poteva chiedere,
«quelli di Milano» no.

Adesso:

- una riga di **chip nuovi** con le sigle presenti (LIRR, LIMM, LIMC…) e quante voci ciascuna;
- l'elenco esce **ordinato** — perimetro, poi tipo, poi titolo — e non più a caso;
- le voci sono **raccolte per perimetro**, con una testata che si **clicca per richiudere**, più un tasto
  «apri / chiudi tutto».

> ℹ️ **Non sono cartelle da riempire, e apposta.** Non c'è nessuna cartella da scegliere quando si carica un
> allegato: il raggruppamento si **ricava** dai campi che la voce ha già (ambito + sigla). Vuol dire che
> nessuno può archiviare un documento nella cartella sbagliata, perché non c'è nessuna scelta da sbagliare.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-11-file-1.8.0/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **qui c'è il timbro `1.8.0`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: il banco di prova, i chip degli allegati, le SID, la legenda delle piste |
| 5-6 | `Vipi.Application.dll` · `.pdb` | le regole: il giorno operativo delle piste, i filtri della biblioteca, l'import tabelle |
| 7 | `en/Vipi.Ui.resources.dll` | le frasi inglesi (sette voci nuove) |
| 8 | `Vipi.Host.staticwebassets.endpoints.json` | 🔴 **l'indice degli asset**: va con i tre file qui sotto |
| 9 | `wwwroot/_content/Vipi.Ui/vipi-theme.css` | il foglio di stile |
| 10-11 | `…/vipi-theme.css.br` · `.gz` | le sue due copie compresse, che il server serve al posto suo |

ℹ️ **Non ci sono** `Vipi.Infrastructure.dll`, `Vipi.Hosting.dll` né
`Vipi.Infrastructure.MySqlMigrations.dll`: nessuno dei due giri ha toccato quelle parti. Le impronte
`sha256` degli undici file sono in `IMPRONTE.txt`, dentro la cartella del pacchetto.

## L'ordine

1. **Caricate tutti e undici col nome finto** (`.new` in fondo).
2. **Rinominate**, in quest'ordine:
   - prima i **tre file di `wwwroot`** (`vipi-theme.css`, `.br`, `.gz`);
   - poi **`Vipi.Host.staticwebassets.endpoints.json`**;
   - poi `en/Vipi.Ui.resources.dll`;
   - poi i tre `.pdb`;
   - e **per ultimi i tre `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i quattro controlli** qui sotto.

> ⚠️ **L'ordine dei primi due passi non è pignoleria.** Fra la rinomina del foglio di stile e quella
> dell'indice il sito chiede un file col nome vecchio: sono pochi secondi in cui una pagina può uscire senza
> grafica. Facendoli di fila, e riaprendo il sito dopo il riavvio, non se ne accorge nessuno.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.8.0 · 8dc05f4`**.
>
> ℹ️ Se trovate `diagnostica/avvio-errore.txt`, fermatevi e mandatecelo. Se trovate
> `diagnostica/arresto-errore.txt`, **andate avanti**: quello dice che è morto un processo *precedente*
> chiudendo, ed è normale su questo hosting.

> ### B · Il sito risponde davvero (non solo «si vede»)
>
> `https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LI`** nel campo **della pagina** (non in
> quello della barra in alto): la riga sotto deve **cambiare** e dire quanti risultati.
>
> ⚠️ Questo è il controllo che conta: il selettore della lingua, lo zoom e il tema funzionano **anche** su un
> sito caricato a metà, perché non passano dal server. La Ricerca sì.

> ### C · 🔴 La grafica c'è (è il controllo di questo pacchetto)
>
> Aprite una qualunque pagina del sito e guardate che sia **disegnata**: sfondo scuro o chiaro, barra in
> alto blu, riquadri con i bordi. Se la pagina esce **bianca con testo nero incolonnato**, l'indice degli
> asset e il foglio di stile si sono separati: ricontrollate che tutti e quattro i file del punto 8-11
> siano stati rinominati.

> ### D · Gli allegati sono raccolti per perimetro
>
> Aprite `services/vsop/admin/attachments`: l'elenco deve uscire **a gruppi**, con una riga di testata per
> perimetro («Divisione», «ACC · LIRR»…) e il numero di voci accanto. Cliccando una testata il gruppo si
> richiude. Sopra la tabella c'è una riga di chip con le sigle.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi le frasi inglesi, **poi l'indice degli asset insieme ai tre file di `wwwroot`**, poi riavvio.
**Nessuna conseguenza sul database**, che questo pacchetto non tocca.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**; **10 300 test** verdi su **quindici** assiemi (nove progetti sui due
  runtime), **E2E compresi** (300). Fra questi, **21 nuovi** scritti per questo giro;
- il pacchetto **avviato davvero** dalla sua cartella e guidato in un browser: dieci controlli verdi, il
  JavaScript minificato arriva su una riga sola, il circuito si apre, la **Ricerca** risponde, il foglio di
  stile è in vigore e la console del browser è pulita;
- ⚠️ e il foglio di stile provato è **lo stesso file** che si spedisce: impronta `sha256` confrontata fra la
  copia usata nella prova e quella dentro il pacchetto;
- i cinque punti guidati a schermo **con due identità diverse** — da amministratore e da utente sotto lo
  staff di divisione — per vedere le due facce del punto 2. Con una regola notturna attiva: da
  amministratore la legenda dice «dalla regola attiva «#1»», dall'altra identità dice solo «consigliati» e
  il vento;
- la colonna WTC **misurata**: «L, M, H, S» chiede 56 px, la colonna nuova ne offre 91 e sta su una riga.
  ⚠️ E la **controprova**, rimettendo la larghezza di prima: 40 px offerti, e il valore torna su due righe;
- i chip delle SID: 39 righe scelte, un clic sul chip pista, **zero** righe scelte.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **Il segno «adesso» nella tabella delle regole non si è potuto vedere sparire**, perché quella tabella
   viene dalla versione **congelata** del documento e nessun aeroporto della nostra copia di prova ha regole
   pubblicate. Il cancello è lo stesso della frase qui sopra — che invece è stata vista con i propri occhi in
   tutt'e due gli stati — e che senza numero il segno non venga disegnato lo tiene una prova automatica.
2. **Il raggruppamento degli allegati è stato guardato su UN solo perimetro**, perché la copia di prova ha
   un allegato solo. Il comportamento a centoventuno voci — l'ordine, i conteggi dei chip, i gruppi — è
   coperto da otto prove automatiche, non dallo schermo. È il punto su cui vale la pena guardare per primi
   dopo il caricamento.
3. **Il difetto delle regole notturne è corretto e provato**, ma **quali** delle vostre regole cambino
   comportamento non lo sappiamo: dipende da quante hanno l'orario di fine prima di quello d'inizio.
4. **Sul processo che si spegne ogni cinquanta secondi questo pacchetto non fa niente.** Quella resta la
   strada del pannello dell'hosting, dopo il 16 settembre.
