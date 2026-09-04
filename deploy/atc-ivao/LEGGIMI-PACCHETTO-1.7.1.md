# Pacchetto 1.7.1 — solo i file cambiati

> **Timbro:** `1.7.1 · f4d7347c` (4 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.7.0**, che è quello attualmente sul server. **9 file** — la metà di 1.7.0.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, NIENTE `wwwroot`
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare.
>
> **E nessun foglio di stile, nessun JavaScript, nessun indice degli asset.** Non è una supposizione: le
> impronte `sha256` di `vipi-theme.css`, `vipi-print.css`, dei loro `.br`/`.gz` e di
> `Vipi.Host.staticwebassets.endpoints.json` sono state confrontate con la copia dentro il pacchetto 1.7.0 e
> sono **identiche**. Quindi la trappola del 24 agosto — `wwwroot` e il suo indice che si separano — qui non
> esiste proprio.
>
> È il pacchetto più piccolo e più semplice da mesi. Si carica quando volete.

---

## Perché esiste questo pacchetto

Una segnalazione vostra, su **LIPR (Rimini)**, e in una riga: *«ho aggiunto TORA e LDA alle piste, poi sono
cambiate sul database di IVAO; quando ho importato quelle nuove non sono state sovrascritte ma si sono
**aggiunte** a quelle esistenti»*.

Guardando l'editor di LIPR si vedevano **quattro** piste al posto di due:

| Runway | Lunghezza | TORA | LDA |
|---|---|---|---|
| 13 | 2962 | vuoto | vuoto |
| 31 | 2962 | vuoto | vuoto |
| 12 | 2962 | vuoto | vuoto |
| 30 | 2962 | vuoto | vuoto |

IVAO ha **ri-denominato** Rimini per deriva magnetica: 13/31 sono diventate 12/30. E dietro quella schermata
c'erano **due difetti diversi**, che si erano solo incontrati sullo stesso aeroporto. Provando il pacchetto
ne è saltato fuori un **terzo**, più vecchio e più largo di tutt'e due — il punto 4 qui sotto.

---

## Che cosa cambia per chi usa il sito

### 1 · Le piste ora si **sostituiscono**, non si accumulano

Quando si ri-preleva un aeroporto da IVAO, le piste che l'anagrafica **non nomina più** venivano lasciate in
archivio per sempre, e quelle nuove si aggiungevano in fondo. Bastava una ri-denominazione per ritrovarsi il
doppio delle piste.

Adesso il prelievo **riconcilia**: aggiorna quelle che ci sono, aggiunge quelle nuove e **toglie quelle
sparite**.

> ### ⚠️ Ma una pista con dati scritti a mano NON si cancella da sola. Mai.
>
> Se su una pista sparita c'era **TORA, LDA, procedure di avvicinamento, circuiti o circling**, quella riga
> **resta**. L'editor ve la mostra in cima con un avviso — *«Piste non più presenti nell'anagrafica IVAO:
> 13, 31»* — e la riga porta la scritta **«non più in sorgente»**.
>
> Sta a voi: riportate i dati sulle piste nuove, poi togliete la riga vecchia con la **✕**.
>
> Una pista **senza niente scritto sopra** se ne va invece da sola, in silenzio: non c'è nulla da perdere.

### 2 · La ✕ adesso c'è anche quando le piste vengono da IVAO

Prima il tasto per togliere una pista compariva **solo** spegnendo «Piste» in «Sorgenti dati» — e quella
scelta è **globale**: per ripulire un aeroporto bisognava sbloccarli tutti. Adesso la ✕ c'è sempre.

ℹ️ **Aggiungere** una pista a mano resta invece bloccato, ed è voluto: se togliete per sbaglio una pista che
IVAO ha ancora, il prelievo successivo **la rimette**. Una pista inventata a mano no.

### 3 · 🔴 I TORA e gli LDA non si perdono più

Questo è il difetto che vi ha fatto perdere il lavoro, ed era il più insidioso: **non era l'import a
cancellarli, era l'editor a buttarli via**.

La tabella delle piste si salvava **solo col bottone «Salva piste»**. Premendo «Re-importa da IVAO», il
programma chiedeva conferma per le SID non salvate — ma **non** per le piste — e subito dopo ricaricava tutto
dal database. Quel che era stato scritto e non ancora salvato spariva **senza una parola**.

Adesso:

- il **re-prelievo chiede conferma** per qualunque sezione lasciata a metà, elencandola;
- e soprattutto la tabella delle piste **si salva da sola a ogni modifica**: uscite da una casella, è già in
  archivio.

### 4 · 🔴 E provando il pacchetto è saltato fuori il difetto più grave

Non lo cercava nessuno, e c'era da mesi. In **tutti e quattro** i pannelli dello scalo — Piste, Quote di
transizione, Regole piste, Frequenze — il programma **non si accorgeva** che avevate scritto qualcosa.

Conseguenze, tutte già in produzione oggi:

- il contatore **«Salva tutto»** restava a **(0)** anche con del lavoro dentro;
- l'avviso del browser «ci sono modifiche non salvate» **non compariva mai** su quei quattro pannelli:
  cambiavate pagina e il lavoro se ne andava in silenzio;
- e il salvataggio automatico del punto 3 sarebbe **nato morto**.

È corretto in tutti e quattro. ℹ️ Restano fuori di proposito i campi del **pannello di prova** delle regole
piste (direzione del vento, nodi, pista bagnata): quella è una simulazione, non contenuto da salvare, e
segnalarla come «da salvare» farebbe suonare l'avviso su un documento che nessuno ha toccato.

> ### ⚠️ Perché questo conta più di quanto sembri, sul vostro server
>
> Il processo dell'applicazione si rigenera **ogni cinquanta secondi circa** — è la cosa che stiamo ancora
> misurando col punto qui sotto. Non serviva nessun bottone per perdere il lavoro: bastava che il processo
> morisse mentre stavate scrivendo. Con il salvataggio automatico, quel rischio sparisce.

---

## ⚠️ Una cosa da rifare a mano, e non possiamo farla noi

**I TORA e gli LDA di LIPR sono persi.** Vanno riscritti.

LIPR non ha una vIPI **pubblicata**, quindi non esiste nemmeno una versione fotografata da cui ripescarli:
quei valori non sono più da nessuna parte. ⚠️ **Fatelo dopo aver caricato questo pacchetto**, o il
salvataggio automatico non c'è ancora a proteggerli.

Sugli altri aeroporti, se trovate l'avviso «non più in sorgente», seguite il punto 1: prima spostate i dati,
poi togliete la riga.

---

## ⏳ E resta in piedi quel che vi avevamo chiesto con 1.7.0

**`diagnostica/avvii.txt` riscaricato qualche ora dopo.** Quel file adesso scrive, su ogni riga di `ARRESTO`,
quante richieste ha servito il processo e chi gli ha chiesto di spegnersi — è arrivato con 1.7.0 e non
l'abbiamo ancora visto.

| quel che si legge in `avvii.txt` | vuol dire | che si fa |
|---|---|---|
| `richieste 6`, `12`, `20`… | le richieste **arrivano**, e muore lo stesso | si guarda nel **pannello** dell'hosting |
| `richieste 1` o `richieste 0` | le richieste **non arrivano** al processo | si cambia l'indirizzo che il keep-alive interroga |

ℹ️ Quel che abbiamo già potuto leggere nel registro di stamattina: **489 avvii in dieci ore** (uno ogni
settanta secondi circa) e **484 arresti su 489 avvenuti in modo ORDINATO**. Non è un guasto
dell'applicazione: qualcuno la sta spegnendo apposta, e ci aspettiamo che la strada sia il pannello.

---

## I file da caricare

Nello zip ci sono **due cartelle**, e vanno tenute distinte:

| | |
|---|---|
| `solo-9-file-1.7.1/` | **quel che si carica.** Dentro non c'è niente da leggere: solo i file e le loro impronte |
| `docs/` | **quel che si legge** — questo foglio e gli altri. Sul server non servono a nessuno: **non caricateli** |

Tutti i percorsi sono **relativi alla cartella dell'applicazione** (`public_atc`), che è anche la radice
dell'FTP.

| # | File | Che cos'è |
|---|---|---|
| 1-2 | `Vipi.Host.dll` · `.pdb` | il sito ⚠️ **qui c'è il timbro `1.7.1`** |
| 3-4 | `Vipi.Ui.dll` · `.pdb` | le pagine: la ✕, l'avviso delle piste sparite, il salvataggio automatico |
| 5-6 | `Vipi.Application.dll` · `.pdb` | le regole: che cosa si può togliere e che cosa no |
| 7-8 | `Vipi.Infrastructure.dll` · `.pdb` | il database: la riconciliazione delle piste |
| 9 | `en/Vipi.Ui.resources.dll` | le frasi inglesi (ci sono sei voci nuove) |

ℹ️ **Non ci sono** `Vipi.Hosting.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`, `wwwroot` né l'indice degli
asset: niente di tutto ciò è cambiato. Le impronte `sha256` dei nove file sono in `IMPRONTE.txt`, dentro la
cartella del pacchetto.

## L'ordine

1. **Caricate tutti e nove col nome finto** (`.new` in fondo).
2. **Rinominate**: prima `en/Vipi.Ui.resources.dll`, poi i quattro `.pdb`, e **per ultimi i quattro `.dll`**.
3. **Riavviate** con `tmp/restart.txt`, **poi aprite il sito una volta**.
4. **Fate i tre controlli** qui sotto.

## I controlli, in due minuti

> ### A · È partita la versione nuova
>
> `diagnostica/avvio-diagnostica.txt`: prima riga con **l'ora di adesso**, riga `Versione` con
> **`1.7.1 · f4d7347`**.
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

> ### C · Le piste di LIPR
>
> Aprite `services/vsop/lipp/airports/editor?icao=LIPR`, sezione **Piste**. Devono esserci ancora **quattro**
> righe — 13, 31, 12, 30 — perché il caricamento **da solo non tocca i dati**: la pulizia avviene al
> **prossimo prelievo** da IVAO.
>
> Premete **«Re-importa da IVAO»** e guardate: 13 e 31 devono **sparire** (sono vuote, non c'è niente da
> salvare) e devono restare **12 e 30**. Poi scriveteci i TORA e gli LDA: escono dalla casella e sono già
> salvati, senza premere nulla.

## Le quattro cose che NON vanno cancellate

`segreti/` · `appsettings.Production.json` · `vipi-keys/` · `tmp/`

Questo pacchetto non le tocca. Se un programma FTP propone di sincronizzare cartelle intere, **non fatelo**.

## Se qualcosa va storto

Le rinomine al contrario: i file di prima sono ancora sul server col nome `.old`. ⚠️ Prima i `.dll`, poi i
`.pdb`, poi le frasi inglesi, poi riavvio. **Nessuna conseguenza sul database**, che questo pacchetto non
tocca.

---

## Che cosa è stato provato prima di spedire

Sul **pacchetto pubblicato**, non sul codice sorgente.

- build in Release, **0 avvisi**; **10 285 test** verdi su **quindici** assiemi (nove progetti sui due
  runtime), **E2E compresi** (300). Fra questi, 23 nuovi scritti apposta per questo giro;
- il pacchetto **avviato davvero** dalla sua cartella e guidato in un browser: il JavaScript minificato
  arriva su una riga sola, il circuito si apre, la **Ricerca** risponde (**50 risultati** per «LI»), e il
  timbro nel file di diagnostica dice la versione giusta;
- la **✕** sulle piste c'è, una per riga, con le piste bloccate dalla sorgente;
- e la prova che conta sul salvataggio automatico, fatta **sul database** e non a schermo: scritto `1700` in
  una casella TORA di LIBR e **uscito dalla casella senza premere niente**, il registro mostra le scritture e
  la riga torna dall'archivio col valore dentro. ⚠️ **È questa prova che ha scoperto il difetto del punto 4**:
  al primo giro le scritture erano **zero**.

🔴 **Quel che NON è stato provato, e va detto chiaro.**

1. **La riconciliazione non è stata provata sul vostro database**, ma su una copia e su archivi di prova. È
   un'operazione che **toglie righe**, quindi la rete che conta è un'altra: se IVAO non risponde o non manda
   piste, il programma **non tocca niente** — zero piste dalla sorgente vuol dire «nessun cambio», mai
   «l'aeroporto non ha più piste». Questa regola ha un test dedicato, ed è la stessa lezione per cui a
   giugno un aggiornamento «pulito» aveva azzerato 83 poligoni di aree regolamentate.
2. **L'avviso delle piste sparite si vede solo subito dopo il prelievo**, non riaprendo la pagina il giorno
   dopo. È una scelta: riconoscerle a freddo richiederebbe una colonna nuova nel database, cioè una
   migrazione — e finché chi amministra il database è via, non spediamo migrazioni che non siano
   indispensabili. La ✕ invece resta disponibile sempre.
3. **Sul processo che si spegne ogni cinquanta secondi questo pacchetto non fa niente.** Aspettiamo ancora
   `avvii.txt`.
4. **Il salvataggio automatico è provato sulle piste**, che è il pannello da cui è nata la segnalazione.
   Sugli altri tre è provata la **notifica** (il programma si accorge che avete scritto), non un
   salvataggio automatico: lì il bottone «Salva» resta il gesto giusto — ma adesso, se cambiate pagina senza
   premerlo, l'avviso compare.
