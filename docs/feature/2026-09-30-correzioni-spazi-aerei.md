# Correzioni a mano degli spazi aerei (30 settembre 2026)

> **Online in 1.52.0** (S63, §A139). Provata a schermo sulla copia del DB, anche con un KMZ caricato dalla pagina;
> in produzione resta da vedere la prima correzione vera e il primo file dell'AIP caricato dopo.

**Chiesto dal committente**: in `/services/vsop/admin/airspace` rendere modificabili **tipo, base, tetto e
classe** di un volume. Quando si carica un file nuovo, le incongruenze si segnalano nella pagina, con la
possibilità di marcarle come a posto e farle sparire.

## 1. Perché non si scrive sulla riga del volume

L'idea di partenza era «l'indice è il nome: basta non toccare quello». **Non è così.** L'identità di un volume
(`AirspaceVolume.NaturalKey`) è `FAMIGLIA|NOME|BASE|TETTO`, perché il nome da solo nel file non è unico
(`GRAZZANISE CTR Z2` compare due volte con bande diverse). Gli agganci settore → volumi citano quella chiave.
Due conseguenze:

- correggere tipo, base o tetto **sulla riga** cambierebbe la chiave, e gli agganci dei settori resterebbero
  scoperti;
- ogni caricamento **rifà tutte le righe** dal file: una correzione scritta lì sparirebbe al caricamento dopo.

## 2. Com'è fatto

- **Tabella a parte**, `AirspaceVolumeCorrections` (migrazione `CorrezioniSpaziAerei`, SQLite e MySQL, solo
  additiva: una tabella e un indice unico su chiave + ordinale). Una riga per volume, che cita la **chiave del
  file**. Un campo corretto è un campo non nullo. La classe ha il suo segno (`ClassCorrected`), perché anche
  «nessuna classe» è una correzione. Accanto ci sono **i valori che il file aveva** quando la correzione è stata
  fatta (`FileFamily`, `FileClass`, `FileBaseRaw`, `FileTopRaw`).
- **Si salvano solo i campi diversi dal file.** Se chi corregge riscrive i valori del file, la correzione si
  toglie da sola. `2500 FT AMSL` e `2500FT AMSL` sono la stessa quota: il confronto passa da
  `AirspaceLevelParser`.
- **Si sovrappone in lettura**, in due posti soli: `EfAirspaceCatalog` (elenchi, conteggi per famiglia, volumi per
  id) ed `EfSectorAirspaceBindings` (la risoluzione degli agganci, cioè mappa, 3D e stampa). Le regole sono pure,
  in `Vipi.Application/Airspace/AirspaceCorrections.cs`. `AirspaceVolumeRow.IsCorrected` dice che i valori non sono
  quelli del file; `NaturalKey` resta quella del file.
- Il **filtro per famiglia** vale sul tipo corretto: un CTR che il file chiama CTA compare fra i CTR, anche per
  il ripiego delle ATZ sulle torri (`AtzTowerShapeService`, che riprende le quote corrette al giro successivo).
- Correggere **tocca il gettone delle forme** (`ShapeChangeStamp`): mappa e 3D ridisegnano subito.
- **Chiede l'Editor**, come gli altri gesti della pagina, dentro il catalogo e non solo sul bottone.

## 3. Dopo un caricamento nuovo

Per ogni correzione si cerca il volume nel file in vigore: **prima per chiave**, poi, se la chiave non c'è più
(il file ha cambiato tipo, base o tetto), **per nome**. Il nome vale solo se nel file è unico e nessun'altra
correzione ha già preso quel volume per chiave. Scegliere a caso fra due omonimi sposterebbe la correzione sul
volume sbagliato. Poi si confrontano **solo i campi corretti** con quel che il file diceva allora:

| Che cosa è successo | «Va bene» | «Prendi il file» |
|---|---|---|
| **Il file ha cambiato un campo corretto** | la correzione resta e si riallinea al file nuovo: i campi corretti restano quelli voluti, gli altri seguono il file | toglie la correzione |
| **Il file ora dice già quel che dice la correzione** | toglie la correzione, che non serve più | — |
| **Il volume non c'è più** | toglie la correzione, che non ha più niente da correggere | — |

Un campo **non** corretto che cambia nel file non è un'incongruenza: segue il file, come ha sempre fatto.

**Ritrovato per nome** vuol dire che la chiave è cambiata, e gli agganci citano ancora la vecchia (quindi sono
scoperti, e il blocco degli agganci lo dice già). Confermando, con «Va bene» o con «Prendi il file», **gli agganci
passano alla chiave nuova** e i confinanti si rifanno come dopo un aggancio. Se un settore aveva già anche il volume
nuovo, si toglie solo il doppione.

La revisione **si calcola ogni volta** dal file in vigore: niente elenco salvato che possa andare fuori sincrono.
Vale anche rimettendo in vigore un caricamento vecchio.

## 4. La pagina

- Nella tabella dei volumi, una **matita** in fondo a ogni riga apre sotto un modulo: tipo (tendina), classe
  (A–G o vuota), base, tetto, «Salva», «Annulla» e, se il volume è già corretto, «Torna al file» (con conferma).
  Il nome non si tocca.
- Un volume corretto porta la pastiglia **«corretto»**. Il suo `title` dice che cosa diceva il file. Un filtro
  «Corretti a mano» li mette in fila.
- Il blocco **«Da controllare dopo il caricamento»** sta in cima e c'è solo quando serve.

- Mentre un gesto lavora, la riga in testata dice **che cosa** sta facendo («Aggiorno gli agganci e ricalcolo i
  confinanti...»): un gesto che sposta agganci rifà i confinanti, e sulla copia ci mette 15–25 secondi.

## 5. Che cosa resta fuori

- La **categoria** alla lettera del file (`Control Traffic Region`) non si corregge: è testo del file, e il tipo è
  quel che conta.
- Il **contorno** non si corregge qui: quello resta del file (o del sectorfile).
