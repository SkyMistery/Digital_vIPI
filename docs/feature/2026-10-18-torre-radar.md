# La torre radar — sì o no, e la tecnica operativa dello scalo

**18 ottobre 2026.** Carta di lavoro (S115).

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
| La tecnica | **Nasce chiusa** |
| Dove sta il testo | **Una volta sola, nell'anagrafica dello scalo**: «la torre di uno scalo militare e civile è SEMPRE una sola». vIPI e vSOP leggono lo stesso testo |
| I colori (dopo averla vista sulla copia di produzione) | La frase nei **callout del sito**: verde (`success`) con la torre radar, rosso (`danger`) senza, giallo (`warning`) se non indicato |
| Visibile? | **Nasce nascosta**, in tutti i documenti — «poi le mostriamo man mano che le sistemiamo». Come le STAR: anche nei documenti già scritti, dove la porta la manutenzione d'avvio |

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
| Titoli | «Torre radar» → «Tower radar», «Tecnica operativa» → «Operating technique» (catalogo e `TitoliUfficiali`) |

## 4. Che cosa non fa

- **La tecnica non si traduce**: è testo d'anagrafica, come la nota delle LVP, e un lettore inglese la legge in
  italiano. Le testate e le frasi fisse («La torre è dotata di radar») sono tradotte.
- **Niente formattazione** nella tecnica (grassetti, elenchi, tabelle): è testo a righe. Se servisse, il passo è
  portarla nei blocchi — ma allora il testo starebbe nel documento e non sarebbe più uno per i due.
- Nessuna marca della torre radar nella vista rapida, nel vAWOS o nell'API pubblica degli aeroporti: non chiesto.

## 5. Esito

- Test: App 3390 → 3411, Infra 2229 → 2232, Ui 2087 → 2100 (Hosting 93, Domain 167 ed E2E 508 invariati), sui due
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
- **Non visto a schermo**: l'unione vIPI + vSOP (provata con i test di `SezioniComuni`), la conferma del ritorno a
  «non indicato» (un dialogo del browser: provata in bUnit), il telefono, la pagina pubblica dopo una pubblicazione
  (la cattura alla release è provata nei test).
