# Catalogo dei punti — suggerire i fix e vedere i typo (22 agosto 2026)

> Ramo `catalogo-punti-suggerimenti`. Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> Tocca l'editor aeroporto (`/services/vsop/{acc}/airports/editor`), gli accordi di coordinamento
> (`/services/vsop/admin/transfers`) e le sorgenti (`/services/vsop/admin/sources`).

## La domanda

«Chi scrive un punto a mano — il fix di una SID, un CoP di una clausola — lo batte **a memoria**. Possiamo
dargli i nomi veri, e dirgli quando ne ha scritto uno che non esiste?»

I nomi c'erano già: `AuroraSectorfileParser` scarica `itfix.fix` e `itvor.vor` da GitHub per completare il
fix troncato dentro un codice SID (`ALAX7G` → `ALAXI`). Erano un `HashSet<string>` sepolto in un adapter di
Infrastructure. Questo giro li fa uscire.

## Perché vale, misurato prima di scrivere codice

Contati sul `vipi.db` di sviluppo con il catalogo vero alla mano:

| Dove | Token verificabili | Fuori catalogo |
|---|---|---|
| CoP delle clausole (`AgreementClauses.Cops`) | 52 su 62 | **1** — `BESIV` |
| Transition delle SID importate | 442 su 446 | 0 |
| Fix delle SID importate | 1417 su 1490 | 332 — **tutti** già marcati `NeedsFixReview` |

`BESIV` non esiste nel sectorfile. A una lettera di distanza c'è `BEKIV`. È un typo vero, trovato al primo
colpo, e non è un problema estetico: `AgreementPoints` confronta i punti dei due versi **per stringa**, quindi
quel nome storto compare fra i «punti presenti in un verso solo» del cruscotto delle lacune — un errore di
scrittura che si presenta come un'asimmetria dell'archivio.

La terza riga ha cambiato il progetto: sul tabellone delle SID importate le due condizioni («fuori catalogo» e
«fix da verificare») coincidono **riga per riga, 332 su 332**. Là il triangolo c'è già: aggiungerne un secondo
sarebbe stato un segno di troppo. Lì è entrato il **solo** suggerimento, che è quello che mancava.

## Pre-flight (le quattro domande)

1. **Modello** — nessun gemello: `NavaidCatalog` tiene INSIEME le due forme dello stesso caricamento
   (elenco ordinato con la natura di ogni punto, e insieme dei soli nomi). Prima sarebbero stati due
   caricamenti dello stesso file, con due momenti d'aggiornamento diversi — cioè la possibilità che
   l'editor consideri sbagliato un fix che l'import considera giusto.
2. **Dispatch** — nessuno switch nuovo. `INavaidSource` sta accanto a `ISidProvider` e `ITowerShapeSource`,
   stessa forma, stessa registrazione.
3. **Ingressi + verifica** — nessun oggetto nuovo da creare, quindi nessun catch-22. La verifica è a schermo:
   §Verifica live.
4. **Propagazione** — `ParseNavaids` cambia firma e tipo di ritorno; i due chiamanti e i due test sono
   aggiornati nello stesso giro. Niente resta a metà.

## Le tre decisioni che contano

### 1. Elenco nativo nelle tabelle, picker nei pannelli

Non è una doppia strada: è la strada che queste pagine avevano già. La tabella delle SID usa
`<input list=...>` per gli identificativi pista (`ape-rwy-idents`); il pannello degli accordi usa
`TypeaheadPicker` sei volte.

Il motivo per cui non si può usare il picker nelle tabelle è **fisico**: il picker apre un riquadro in
posizione assoluta, e la tabella vive dentro un contenitore che scorre — sulle ultime righe il riquadro
verrebbe tagliato dal bordo. L'elenco nativo lo disegna il browser fuori dalla pagina.

Il motivo per cui non si può usare l'elenco nativo nel campo dei CoP è altrettanto fisico: quel campo è un
**elenco separato da virgola**, e il browser filtra sul valore INTERO del campo — dentro «VALMA, EL» non
troverebbe mai niente. Da qui la modalità `TokenList` del picker: la scelta completa l'ultima voce invece di
riscrivere la riga, e il come sta in `CopList`, dove sta già il formato di quell'elenco.

### 2. Il datalist è un componente con `ShouldRender` bloccato

Sono ~1400 voci. La pagina che le ospita si ridisegna a ogni tasto scritto nella casella di ricerca delle SID
(`@bind:event="oninput"`): un ciclo scritto nella pagina rifarebbe 1400 nodi d'albero per ogni tasto.
`NavaidDatalist` si disegna quando l'elenco cambia davvero — cioè una volta, quando il catalogo arriva.

### 3. Il giudizio è fatto per metà di casi in cui deve tacere

`NavaidCheck` giudica **solo** ciò che ha forma di nome di punto: da 2 a 5 lettere e nient'altro. Un CoP è
testo libero e lo è di proposito — `Y01-Y12`, `TOPNO 3A`, `ALL to GR` sono dati corretti che il catalogo non
può giudicare. E con la sorgente muta (`NavaidCatalog.Empty`) **niente** è sconosciuto: segnare tutto
trasformerebbe un GitHub irraggiungibile in una pagina piena di avvisi falsi.

## Freschezza

`SectorfileCache` non scadeva mai. Finché conteneva solo dati d'import andava bene — il ciclo delle 24h li
rileggeva comunque. Ora la legge anche chi **scrive**, e un fix pubblicato oggi non può restare invisibile ai
suggerimenti fino al riavvio dell'applicazione. Quindi: `Invalidate()` all'inizio di ogni ciclo d'import, e
un tasto **Ricarica i punti** in `/services/vsop/admin/sources` per non aspettare le ventiquattro ore.

Il tasto sta lì anche se il catalogo **non è una categoria d'import**: non ha una spunta e non tocca il
database. Ma quella è la pagina di chi guarda la sorgente, ed è l'unico posto da cui la domanda «rileggila
adesso» ha senso.

## Verifica live

Procedura: skill `verifica-live`, con una deroga — `Sectorfile__RawBaseUrl` va lasciato **acceso**, altrimenti
il catalogo è vuoto e non si verifica niente. Nella pagina degli accordi serve prima **prendere il lock** di
struttura: senza, i tasti di riga sono spenti e il pannello non si apre.

Provato guidando Edge:

- editor `LIBD`: elenco presente, **1385 voci**, le tre nature; nel campo FIX di una SID manuale «BESIV»
  prende il bordo giallo e il suggerimento, «BEKIV» resta pulito;
- editor `LIRF`: **146** campi di correzione fix, tutti agganciati all'elenco;
- accordi `LIBB`: la riga «AIOSA, GISAM, BESIV» è sottolineata, e il suggerimento nomina BESIV; nel pannello
  la scrittura di «, BEK» propone BEKAN · BEKIV · BIBEK · OLBEK · UMBEK, e la scelta scrive
  «AIOSA, GISAM, BESIV, BEKAN» — **completa la voce, non riscrive la riga**.

### Due difetti che solo lo schermo ha trovato

**Le righe di commento erano nel catalogo.** `itvor.vor` e `itndb.ndb` portano righe in stile C
(`//++++VOR ESTERNI(servono per le AEROVIE)++++GEBNI`). Senza punto e virgola, finivano nel catalogo INTERE
come se fossero nomi di punto. Sulla completion delle SID non si vedeva — nessun prefisso di codice SID
inizia per barra — ma erano le prime tre voci dell'elenco a discesa. Il difetto era lì da sempre; è bastato
guardare l'elenco per la prima volta.

**La freccia dell'elenco si mangia l'ultima lettera.** Chromium disegna una freccia DENTRO i campi con
`list=`, e si prende ~11px di larghezza utile. Misurato con «ALAXI» dentro: il campo ne offre 60, il testo ne
chiede 60, il testo più la freccia ne chiede 71. È esattamente il costo che il segno di «fuori catalogo» aveva
evitato tingendo il campo invece di aggiungergli un'icona — rientrato dalla finestra.

⚠️ E qui la misura ha ribaltato l'attribuzione: **RWY** («34R»: 50 utili contro 58) e **TYPE** («RNAV»: 66
contro 69) avevano già l'elenco, quindi erano **già tagliate in `main`**. Allargare le colonne sarebbe costato
+24px a una tabella che misura 1068 di minimo; via la freccia, invece, e tutt'e cinque i campi tornano interi.

⚠️ `!important` serve davvero: su questo pseudo-elemento lo stile del browser vince su quello dell'autore.
Provato a schermo — la stessa regola senza `!important` lascia il taglio a 71px con **qualunque** selettore
(corto, con l'attributo, o con `.struct .res-table.sid-edit` davanti). Non è la specificità a decidere, quindi
alzarla non serve a niente. È l'eccezione alla regola di specificità scritta in `regole-ui-pagine-admin`.

## Cosa NON è entrato, e perché

- **Nessuna tabella `Navaid` in database.** Il catalogo vive in memoria: niente migrazione, e il deploy è già
  fermo in attesa del cutover MariaDB con migrazioni in coda. Se la sorgente tace si perdono i suggerimenti,
  non si rompe niente.
- **Nessuna coordinata.** `TryParseDms` c'è già e il record cresce di due campi il giorno che servisse — per
  esempio per verificare che un CoP stia davvero **sul confine** fra i due enti dell'accordo, o per vederlo
  sulla mappa. Oggi leggerle costerebbe 1400 conversioni per un dato che nessuno guarda.
- **Nessuna normalizzazione delle maiuscole.** Il confronto le ignora già; forzarle nel campo dal browser
  litigherebbe con `@bind`, e farlo al salvataggio è un cambio al **dato** che questo giro non chiedeva.
- **Aerovie (`AIRWAY/`).** Suggerire i fix lungo una aerovia: interessante, fuori perimetro.

## Cosa resta da fare

- **`BESIV` in archivio è ancora lì.** Il giro lo *segnala*; correggerlo è una decisione editoriale (BEKIV?
  un punto estero non elencato?) e la prende chi conosce l'accordo LIBB_ES_CTR ⇄ LDZO_CTR.
- **Rileggere i CoP degli altri ACC** con la pagina aperta: il conteggio qui sopra è del DB di sviluppo, che
  ha 52 clausole. In produzione ce ne sono di più.
