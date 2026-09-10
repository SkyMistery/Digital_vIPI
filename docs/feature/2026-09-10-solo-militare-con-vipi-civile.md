# Campo «solo militare» con una vIPI civile: dirlo, e dire come uscirne 🟢

**Chiesto dal committente il 10 settembre 2026**, prima di caricare 1.20.0: *«se un aeroporto per cui
esistono già vIPI e vSOP viene marcato come military only, cosa succede?»*

## Che cosa succede oggi — misurato, non dedotto

**Niente si rompe, e niente aiuta.** Il toggle fa un controllo solo — «deve avere presenza militare» — e poi
scrive il flag. Da lì in poi:

| | |
|---|---|
| La vIPI civile | **Resta**: viva, apribile, modificabile, **pubblicata** |
| L'elenco pubblico degli aeroporti | Non filtra: il campo c'è, con la pastiglia «solo militare» accanto |
| La pagina della vIPI civile | Si apre, e mostra «solo militare» **accanto al proprio titolo** — si contraddice da sola |
| L'editor civile | Si apre: `EnsureDocumentAsync` blocca la **nascita**, non l'apertura. È scritto e voluto |
| L'editor militare | Rimanda i dati di scalo all'editor civile, **correttamente** (`ScaloSenzaCivile` = solo militare **E** senza civile) |
| Diagnostica | **Nessun controllo.** Cercato: zero |
| Chi avvisa | **Nessuno**, né prima né dopo il clic |

✅ **La parte difficile è già giusta**: il rischio ovvio — i dati dello scalo senza porta di scrittura —
**non si verifica**, perché §AS aveva già stabilito che la domanda è «solo militare **E** senza civile».

🔴 **Il problema è uno stato che si contraddice, e che nessuno racconta.** È la forma che questo progetto ha
già pagato tre volte: nessun errore, nessun rosso, e una cosa **falsa a schermo**.

⚠️ **E c'è un secondo modo di arrivarci che nessuno digita**: il giro dell'anagrafica fa
`if (!apt.HasMilitaryPresence) apt.IsMilitaryOnly = false;`. Se IVAO smette di dichiarare la presenza
militare, «solo militare» **si spegne da solo**, in silenzio. Un rilievo che guarda lo **stato** prende anche
questo; un avviso al solo momento del clic no. Sono due presidi diversi, e servono tutt'e due.

## Le due verifiche chieste dal committente, e che cosa hanno detto

### 1. Se vIPI e vSOP sono UNITI

✅ **Nascondere la vIPI la toglie anche dalla pagina unita.** Il caricatore del membro passa dalla via
pubblica, che filtra `!d.IsHidden` e pretende una release in vigore: nascosta ⇒ `null` ⇒ `AltriMembriAsync`
la **salta**. Nell'**editor** il membro continua a vedersi, ed è giusto — un documento nascosto si deve poter
redigere e sciogliere.

🔴 **Ma nascondere non basta: l'unione continua a pubblicarla.** `BersagliUnitiAsync` **non guarda**
`IsHidden`. Su un campo solo militare, con la vIPI civile nascosta **e ancora unita**, ogni pubblicazione del
vSOP crea una release **anche per lei** — invisibile a tutti. Non fa danno; è rumore che si accumula
nell'elenco delle release e nella retention, e nessuno lo spiega.

⚠️ **La via d'uscita è quindi DUE gesti, non uno: nascondere _e_ sciogliere l'unione.** Ed è la ragione per
cui il rilievo deve dirlo: chi ne fa uno solo crede di aver finito.

🔴 **Quel che NON si fa: far saltare i membri nascosti alla pubblicazione accoppiata.** Sembra la cura, ma
cambierebbe il senso di «pubblica l'unione» per **tutti** gli altri casi, per rimediare a uno stato che non
dovrebbe esistere. Si dice di sciogliere.

⚠️ **Terza conseguenza, da §CR (1.20.0)**: prima la pagina della vIPI civile **reindirizzava** al vSOP, e chi
arrivava dal vecchio indirizzo finiva comunque su qualcosa. Adesso non reindirizza più — e se la vIPI è
nascosta quell'indirizzo dice «niente da mostrare». È corretto per un documento nascosto, ma è un
cambiamento rispetto a prima.

### 2. Spostare sezioni fra due documenti uniti

**No, e c'è una guardia scritta** in `EfEditingRepository.MoveSectionToParentAsync`:

> ⚠️ il padre nuovo deve stare nella **STESSA versione**. Una sezione non cambia mai documento, **e fra i
> membri di un documento unito nemmeno**.

Due limiti, non uno: **fra** documenti è impossibile per costruzione (l'unione è una relazione di
*presentazione*, non una fusione di contenuto); **dentro** un documento si spostano solo le sezioni
**libere**, perché una di catalogo ha una posizione standard e altrove sarebbe muta.

✅ UI e motore concordano: nell'editor unito ogni membro monta il proprio albero, quindi il menu «Sposta
in…» non offre mai una destinazione dell'altro documento. Non è una mossa offerta e poi rifiutata.

⚠️ L'unica scrittura che attraversa i membri è un'altra cosa: la scheda **«sezioni in comune»**, che
**nasconde** le copie ripetute scegliendo chi le tiene. Nasconde, non sposta.

🔴 **Conseguenza sul commento di `AirportEditingService`**, che oggi dice che la via d'uscita è «spostarne il
contenuto, poi eliminarlo»: **quello spostamento non esiste come gesto.** È copia-e-incolla a mano, blocco
per blocco. Il commento va corretto, o promette un comando che non c'è — ed è il debito peggiore secondo il
`FEATURE-PROCESS`: non il codice sbagliato, ma il **record rimasto vero a metà**.

## La decisione del committente

**La vIPI civile RESTA finché qualcuno non la nasconde o la elimina.** Niente automatismi: «solo militare» è
un giudizio editoriale, e la vIPI può contenere lavoro vero. Quindi si **dice** e si **guida**, non si agisce
al posto di chi decide.

## Che cosa si fa

### 1. Il toggle dice che cosa comporta, e chiede conferma

Sulla pastiglia militare — che **è** il comando — la conferma compare **solo quando c'è davvero una vIPI
civile** (`AirportAdminRow.DocumentId is not null`, che la riga porta già). Negli altri casi il clic resta
secco com'è oggi: una conferma che chiede sempre è una conferma che nessuno legge.

⚠️ **La conferma non dice lo stato esatto** (pubblicata? nascosta? unita?): quella è la domanda del rilievo,
che ha il dataset intero. Qui si dice la cosa che chi preme deve sapere **prima**: *quel documento resta
com'è, e toglierlo è un gesto a parte*.

### 2. Un rilievo in Diagnostica, che guarda lo STATO

Categoria **«vIPI civile su campo solo militare»**, area *Dati*. Due casi, due messaggi:

| Stato | Severità | Che cosa dice |
|---|---|---|
| La vIPI civile è **visibile al pubblico** (pubblicata, non nascosta, aeroporto non nascosto) | **Warning** | «il campo è solo militare ma la sua vIPI civile è ancora online: nascondila» — e **se è unita**, aggiunge «e sciogli l'unione» |
| La vIPI civile è **nascosta ma ancora UNITA** al vSOP | **Warning** | «ogni pubblicazione del vSOP pubblica anche lei: sciogli l'unione» |
| La vIPI civile è nascosta e non unita | *(niente)* | È la via d'uscita già percorsa: un rilievo qui sarebbe un avviso sul caso normale |

🔴 **La terza riga è la più importante del disegno.** «Un avviso che scatta sul caso normale non è un
avviso» — questa pagina l'ha già imparato due volte (il cruscotto delle lacune, e lo scalo coperto da un
ente non ricevente). Chi ha fatto tutti e due i gesti non deve vedere niente.

### 3. Il rilievo dice i DUE gesti, non uno

È il punto che la verifica ha aggiunto: l'unione va sciolta, o la pubblicazione accoppiata continua a girare
su un documento che nessuno vede.

### 4. Il commento che promette un comando assente

`AirportEditingService`: «la via d'uscita (spostarne il contenuto, poi eliminarlo) passa proprio da lì» →
va detto che lo spostamento **fra documenti non esiste**, e che il contenuto si ricopia a mano.

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — nessun concetto nuovo, nessuna colonna, **nessuna migrazione**. Una riga di dataset nuova
   per il report e una conferma in pagina. Lo stato che si racconta esiste già tutto in archivio.
2. **Dispatch** — nessuno `switch` nuovo: il report è un elenco di controlli e se ne aggiunge uno.
3. **Ingressi + verifica** — il gesto sta dove sta già (la pastiglia in *Aeroporti*), il rilievo dove stanno
   gli altri (*Diagnostica*). Verifica **dal vivo**: un campo con vIPI civile pubblicata, uno con la vIPI
   nascosta e ancora unita, e uno a posto — che **non deve dire niente**.
4. **Propagazione** — questo giro non rimuove né rinomina codice; corregge un **commento** che afferma una
   cosa falsa (punto 4). Vanno aggiornate anche la memoria e la carta dei vSOP militari se citano quella via
   d'uscita.

## Definition of Done

- [ ] `dotnet build Vipi.slnx -c Release --no-incremental` verde sui due TFM, 0 avvisi.
- [ ] Suite verde contando i progetti.
- [ ] 🔴 **Il caso «tutto a posto» non produce nessun rilievo**, e c'è un test che lo pretende: è la metà che
      distingue un presidio da un allarme che suona sempre.
- [ ] Il caso «nascosta ma unita» produce **un solo** rilievo, quello giusto.
- [ ] La conferma sul toggle **non compare** dove non c'è una vIPI civile.
- [ ] Verifica live: i tre casi a schermo, in italiano e in inglese.
- [ ] Carta, memoria e il commento di `AirportEditingService` aggiornati.
