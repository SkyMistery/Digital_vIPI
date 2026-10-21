# Il radar di torre — sì o no, e la tecnica operativa dello scalo

**18 ottobre 2026.** Carta di lavoro (S115).

> ⚠️ **Dal 21 ottobre 2026 (S118) la tecnica operativa è fatta di blocchi**, non più un testo a righe
> dell'anagrafica: §7. Quello che i §2–§4 dicono della tecnica («una volta sola nell'anagrafica», «testo a righe»,
> «non si traduce», «niente formattazione», la conferma del ritorno a «non indicato») vale fino a quel giorno. Il
> sì/no resta com'era.

---

## 1. La domanda

Il committente (18 ottobre): serve una sezione, nelle vIPI e nei vSOP, che dica se la **torre è dotata di radar**.
Se non lo è, si scrive solo questo. Se lo è, sotto c'è almeno una sotto-sezione con la **tecnica operativa** di
quello scalo. Sugli scali che hanno vIPI e vSOP dev'essere **una sezione unica**, come le Piste.

L'idea che gli è stata portata: «La specifica torre radar sì/no la metterei abbastanza in alto nella pagina. La
gestione del traffico potrebbe stare bene in mezzo a Piste e SID, perché sono collegate di fatto: scegli la SID in base
alle condizioni della pista scelta per le partenze».

## 2. Che cosa è stato deciso (committente, 18 ottobre)

| Bivio | Deciso |
|---|---|
| Dove | **Una sezione sola, fra Piste e SID**. Così resta abbastanza in alto (quinta voce della vIPI) e la tecnica sta dove la si usa. L'alternativa era spezzarla, col sì/no più in alto e la tecnica fra Piste e SID |
| La tecnica | **Nasce chiusa** — ⚠️ dal 21 ottobre 2026 **aperta** (§9) |
| Dove sta il testo | **Una volta sola, nell'anagrafica dello scalo**: «la torre di uno scalo militare e civile è SEMPRE una sola». vIPI e vSOP leggono lo stesso testo |
| I colori (dopo averla vista sulla copia di produzione) | La frase nei **callout del sito**: verde (`success`) con la torre radar, rosso (`danger`) senza, giallo (`warning`) se non indicato |
| Visibile? | **Nasce nascosta**, in tutti i documenti — «poi le mostriamo man mano che le sistemiamo». Come le STAR: anche nei documenti già scritti, dove la porta la manutenzione d'avvio |
| Il titolo | **«Radar di torre»**, non «Torre radar». In inglese resta «Tower radar» (scelta del filone) |
| Nella vista live | Accanto all'ICAO della **vista rapida aeroporto** una pastiglia «Radar di torre ↗» se la torre ne è dotata; cliccata apre la vIPI/vSOP **in una scheda nuova**, alla sezione |

Le scelte del filone, che non cambiano il senso della domanda:

- **Tre stati, non due**: *sì*, *no*, *non indicato*. «Non indicato» è lo stato di tutti gli scali che ci sono già, e
  non vale «no» — è la stessa regola delle LVP («il silenzio non è una dichiarazione»). Nel documento dice «Non è
  indicato se la torre è dotata di radar».
- **La tecnica c'è solo col sì.** Col *no* e col *non indicato* la sotto-sezione non c'è: né nel corpo né
  nell'indice.
- **Passare da sì a no non cancella il testo**: resta da parte e torna col sì. Tornare a *non indicato* invece lo
  cancella, e se c'è un testo l'editor chiede conferma.
- **Il testo è a righe** (un'area di testo, non l'editor a blocchi): gli a-capo arrivano a schermo, la formattazione
  no. Tetto di 8000 caratteri, detto dal servizio con una frase.
- Si possono aggiungere **sotto-sezioni libere** sotto la torre radar, come sotto ogni sezione: quelle restano anche
  senza radar.

## 3. Come è fatto

| Pezzo | Dove |
|---|---|
| Il dato | Due colonne nullable su `Airports`: `RadarTower` (sì/no, `null` = non indicato) e `RadarTowerTechnique` (testo, senza misura apposta: su MySQL un varchar lungo peserebbe sul limite di riga della tabella). Migrazione `TorreRadar`, additiva, sui due provider |
| Le sezioni | `radartower` («Torre radar», derivata, resa dalla pagina) con la figlia `radartower:technique` («Tecnica operativa», resa dalla pagina dalla vista del padre). **Stessa chiave** nei due profili (`SectionCatalog.TorreRadarDescrittore`) |
| Vista e release | `AirportRadarTowerView`, **una per le due sezioni**, congelata una volta sola sotto `radartower` (`AirportFrozenSectionProvider`). La figlia è *editoriale* nel catalogo per la stessa ragione delle coordinate delle soglie: un secondo interruttore Live/Frozen potrebbe mettere il sì/no e la tecnica su due cicli diversi |
| La regola «tecnica solo col sì» | `TorreRadar.Filtra`, sulla **lista** delle sezioni che il lettore riceve (`AirportMemberLoader`, `MilMemberLoader`): l'indice si rende dalla stessa lista, e un titolo nell'indice senza la sezione nel corpo sarebbe un collegamento a vuoto |
| Nasce chiusa | `SectionCatalog.IsInitiallyCollapsed`. ⚠️ `SectionNode` rendeva **ogni** sotto-sezione del documento aperta, scritto a mano: ora guarda il catalogo anche lì (la stampa le apre tutte da sé) |
| Lettura | `AirportRadarTower` (le due parti), nei corpi della vIPI e del vSOP |
| Scrittura | `AirportRadarTowerEditor` (le due parti mutano lo stesso oggetto e salvano la riga intera), porta `IAirportEditingService.SaveRadarTowerAsync` col lock del documento. Nel vSOP si scrive **solo su un campo senza vIPI civile**, come LVP e regole piste: altrove c'è il rimando all'editor civile |
| Unioni | Stessa chiave, dato dell'anagrafica: la scheda «sezioni in comune» la trova come una. **Non** la propone spuntata, come le STAR: nasce nascosta, e spuntarla vorrebbe dire mostrarla nel documento che la tiene. Nasconderla da un documento si porta via la tecnica, che c'è anche nell'altro |
| Documenti già scritti | La sezione la aggiunge `AddMissingCatalogSections` all'avvio, nel posto giusto (fra Piste e SID; nel vSOP fra Regole piste e SID dei Dati generali), **nascosta**. La riceve anche la versione pubblicata: nascosta, al pubblico non cambia niente |
| Sezioni da scrivere (S111) | La tecnica **non conta**: editoriale nel catalogo, ma il testo sta nell'anagrafica (`SezioniDaScrivere`, come le soglie) |
| Titoli | «Radar di torre» → «Tower radar», «Tecnica operativa» → «Operating technique» (catalogo e `TitoliUfficiali`) |
| La marca nella vista live | `AirportQuickPanel` la cerca nel documento **pubblicato** che il suo tasto apre (la vIPI, o il vSOP se nella vIPI la sezione non si vede): c'è solo se la torre ha il radar **e** la sezione si vede, anche nelle madri (`TorreRadar.Ancora`) — una sezione nascosta non ha un'ancora a cui arrivare. Il sì/no è quello della vista del documento, congelata con la release. Nella postazione d'aeroporto la pastiglia sta nella testata accanto all'ICAO (`LivePage`, che la riceve dal pannello); nella vista d'area, dove quella testata non c'è, il pannello la mette fra i suoi tasti, accanto a vAWOS |

## 4. Che cosa non fa

- **La tecnica non si traduce**: è testo d'anagrafica, come la nota delle LVP, e un lettore inglese la legge in
  italiano. Le testate e le frasi fisse («La torre è dotata di radar») sono tradotte.
- **Niente formattazione** nella tecnica (grassetti, elenchi, tabelle): è testo a righe. Se servisse, il passo è
  portarla nei blocchi — ma allora il testo starebbe nel documento e non sarebbe più uno per i due.
- Nessuna marca della torre radar nella vista rapida, nel vAWOS o nell'API pubblica degli aeroporti: non chiesto.

## 5. Esito

- Test: App 3390 → 3415, Infra 2229 → 2232, Ui 2087 → 2100 (Hosting 93, Domain 167 ed E2E 508 invariati), sui due
  framework.
- Prova a schermo: §6.

## 6. Prova a schermo

Host locale su un database **nuovo** (migrazione `TorreRadar` applicata all'avvio), con un ACC e due scali inventati:
LIRQ civile e LIPI solo militare. Nessuna copia di database vero.

- **Editor della vIPI (LIRQ)**: «Torre radar» fra Piste e SID, con l'interruttore Congelata/Live; «Tecnica operativa»
  chiusa; si parte da «non indicato». Col *sì* compare l'area di testo; due righe scritte arrivano in archivio con
  l'a-capo. Senza radar l'area non c'è e la frase dice perché.
- **Anteprima bozza (LIRQ)**: col *sì* «La torre è dotata di radar.» e la tecnica chiusa, nell'indice fra Piste e SID,
  con le due righe a capo (tema scuro). Col *no* solo «La torre non è dotata di radar.»: la tecnica sparisce dal corpo e
  dall'indice, e il testo resta in archivio.
- **vSOP (LIPI, senza vIPI civile)**: la sezione nei Dati generali fra Regole piste e SID; il *sì* si scrive dal suo
  editor; l'anteprima mostra la frase e la tecnica chiusa.
- **Sulla copia di produzione del 18 ottobre** (1.67.0, MariaDB privato): la migrazione `TorreRadar` passa sui dati
  veri, e l'avvio aggiunge la sezione a 68 documenti (bozze e pubblicate), nascosta, con la tecnica sotto. Visto dal
  committente col suo login; da lì sono venuti i colori e il «nasce nascosta».
- **I colori**, su un database nuovo coi due scali inventati: giallo «non indicato» (LIRQ appena nata), rosso «no»
  (LIRQ), verde «sì» con la tecnica (LIPI, vSOP), nei temi scuro e chiaro. Una vIPI appena nata ha la torre radar
  nascosta.
- **La marca nella vista live**, su un database nuovo con la postazione inventata LIRQ_TWR e la vIPI pubblicata con la
  sezione visibile e il sì: «Radar di torre ↗» verde accanto a «LIRQ», scheda nuova, e il collegamento atterra sulla
  sezione del documento pubblico. Non vista a schermo: la variante nella vista d'area (fra i tasti del pannello).
- **Non visto a schermo**: l'unione vIPI + vSOP (provata con i test di `SezioniComuni`), la conferma del ritorno a
  «non indicato» (un dialogo del browser: provata in bUnit), il telefono, la pagina pubblica dopo una pubblicazione
  (la cattura alla release è provata nei test).

## 7. 21 ottobre: la tecnica operativa a blocchi (S118)

Il committente: «nella sezione tecniche operative della torre radar non ci deve essere un paragrafo fisso, ma ci deve
essere la possibilità solo di inserire paragrafi, callout ecc, come per il resto. Per migrare i dati già inseriti
mettili in un paragrafo».

| Pezzo | Com'è adesso |
|---|---|
| Il catalogo | `radartower:technique` passa da «resa dalla pagina» (`H`) a **blocchi** (`D`): editor e lettura sono quelli comuni delle sezioni scritte (paragrafo, callout, tabella, immagine, allegato). Il padre `radartower` resta della pagina, col solo sì/no |
| Il dato | `RadarTowerRow` è il solo sì/no. La colonna `Airports.RadarTowerTechnique` **non si scrive più**: la legge solo la manutenzione d'avvio. Si toglie con una migrazione in una consegna successiva, quando quella è passata in produzione |
| Il trasloco | `IDocumentMaintenance.TecnicaRadarNeiBlocchiAsync`, all'avvio dopo `AddMissingCatalogSections`: (1) toglie dalle sezioni della tecnica i **segnaposto** delle sezioni rese dalla pagina (un blocco senza testo né JSON, che l'editor a blocchi mostrerebbe come una tabella senza colonne), in ogni versione come per le «minima»; (2) il testo dell'anagrafica diventa **un paragrafo** della sezione nella vIPI **e** nel vSOP dello scalo — tutti e due lo mostravano — nell'ultima versione e nella pubblicata corrente; (3) svuota la colonna. Gli a-capo restano (in un paragrafo un a-capo solo è un a capo). Una sezione con blocchi già scritti non riceve niente. Uno scalo col testo e nessuna sezione dove metterlo lo tiene, e la passata dopo ci riprova |
| Le release già uscite | La pagina pubblica si legge dalla **fotografia della release**, dove la sezione ha solo il segnaposto e il testo sta nella vista congelata (`AirportRadarTowerView.Technique`). `TorreRadar.Filtra` ne fa un paragrafo se la sezione non ha blocchi scritti: il pubblico continua a leggere il testo fino alla pubblicazione seguente. Le release nuove hanno la vista col solo sì/no |
| Senza il «sì» | Come prima: la tecnica non c'è né nel corpo né nell'indice. I blocchi restano nel documento e tornano col «sì»; l'editor del sì/no lo dice sotto le tre scelte. Tornare a «non indicato» **non chiede più conferma**: non porta via niente |
| Unioni | Ogni documento ha ora i suoi blocchi, come le altre sezioni scritte: in un'unione vIPI + vSOP la tecnica c'è due volte finché la scheda «sezioni in comune» non ne nasconde una (il trasloco ha messo lo stesso paragrafo nei due) |
| Traduzione | I blocchi passano dal traduttore come ogni sezione scritta: la tecnica ora si legge anche in inglese |
| Sezioni da scrivere | La tecnica **continua a non contare**: c'è solo col «sì», e il conto non sa il sì/no |

Scelte del filone: il trasloco in tutti e due i documenti (non solo nella vIPI civile, dove si scriveva sui campi
misti), perché il vSOP mostrava lo stesso testo; e la lettura delle release vecchie, invece di riscrivere le
fotografie già pubblicate.

## 8. 21 ottobre: via la colonna (S120)

La 1.69.0, col trasloco, è online dal 21 ottobre; lo stesso giorno il committente: «togli adesso la colonna
RadarTowerTechnique con una migrazione».

- **Migrazione `TogliTecnicaRadar`**, sui due provider: solo `DropColumn` di `Airports.RadarTowerTechnique`.
  ⚠️ **Distruttiva.** Prima di lanciarla in produzione la colonna dev'essere vuota:
  `SELECT Icao FROM Airports WHERE RadarTowerTechnique IS NOT NULL` non deve dare righe. Una riga rimasta è uno scalo
  il cui testo non ha trovato una sezione dove andare (il trasloco lascia la colonna piena apposta): quel testo va
  copiato a mano nella sezione prima della migrazione.
- **Via anche il trasloco** (`TecnicaRadarNeiBlocchiAsync`) e i suoi test: ha già girato in produzione con la 1.69.0, e
  senza la colonna non ha più niente da leggere. I segnaposto che toglieva non nascono più (la sezione non è più resa
  dalla pagina).
- **Resta** la lettura delle release vecchie (`TorreRadar.Filtra` e `AirportRadarTowerView.Technique`): il testo di
  quelle release sta nella loro fotografia, non nella colonna.

## 9. 21 ottobre: la tecnica nasce aperta (S124)

Il committente: «nelle vIPI e SOP vorrei che la sezione tecniche operative sia aperta di default». La tecnica operativa
esce da `SectionCatalog.InitiallyCollapsedKeys`: si apre come le altre sotto-sezioni, nel documento e nell'editor. Chiusa
aveva senso per un testo lungo fisso; ora è fatta di blocchi (§7), scritti per essere letti. `SectionNode` continua a
chiedere al catalogo, per la prossima sotto-sezione che lo vorrà.
