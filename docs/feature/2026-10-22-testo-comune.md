# Testo comune delle torri radar — carta (22 ottobre 2026)

**S127**, filone Sito. Al committente hanno chiesto di mettere in **tutte** le «Tecniche operative» delle torri radar
la stessa parte: un callout rosso («È fatto obbligo la visione del documento linkato qui sotto…», con l'allegato
«Linee Guida radar di torre») e uno verde (i cinque usi consentiti del radar alla TWR, «LA TORRE NON FORNISCE
VETTORAMENTO»). Il committente: «ragioniamoci prima su», e «non saprei come non scassare tutto».

## 1. Deciso

| Bivio | Deciso |
|---|---|
| Copia nei documenti o testo condiviso | **Condiviso**: si scrive una volta, la sezione lo mostra da sé in testa. Due callout copiati in ogni documento sarebbero N copie che divergono, e uno scalo che passa a «sì» domani non li avrebbe |
| Pubblicazione | **Congelato** (committente, 22 ottobre): ogni documento tiene il testo comune della sua pubblicazione. Quando il testo comune cambia, gli scali con torre radar finiscono in «Da rivedere» finché non si ripubblicano |
| Chi lo modifica | **Solo gli admin** (scelta del filone, detta al committente): cambia il testo di tutte le torri insieme |
| Dove sta nella sezione | **In testa**, fisso: l'editor dello scalo non lo cancella né lo sposta. Sotto restano i blocchi dello scalo, come oggi |

## 2. Com'è fatto

- **Dove vive**: una riga di `SharedBlocks`, chiave `section-common:radartower:technique` (`TestoComune.Chiave`). I
  blocchi sono gli `ExtraBlock` delle intro di pagina: paragrafo, callout (con allegati, S121), tabella, immagine,
  allegato. Stesso editor (`DocumentBlocksEditor`), stessa proiezione in `BlockView` (`PageIntro.Blocco`). Nessuna
  tabella nuova, nessuna migrazione.
- **Congelato con la release**: il testo comune viaggia nella vista derivata della sezione «Radar di torre»
  (`AirportRadarTowerView.Comune`), che la release congela già (`AirportFrozenSectionProvider`). Una release scattata
  prima di oggi non lo porta (`Comune = null`) e **non lo mostra**: lo mostrerà dalla pubblicazione seguente. La
  bozza e l'anteprima leggono quello di adesso (`AirportViewDerivationService`, ramo live).
- **Nella pagina**: i caricatori (`AirportMemberLoader`, `MilMemberLoader`) mettono i blocchi comuni **in testa** alla
  sezione «Tecnica operativa» (`TorreRadar.ConTestoComune`) **prima della traduzione**: la pagina inglese li traduce
  come il resto, e indice, stampa e unione li vedono senza codice nuovo. Id negativi: nessun blocco vero li ha.
- **Il catalogo non cambia**: la sezione resta a soli blocchi (`D`). Farla diventare «scheda + blocchi» avrebbe
  rimesso il segnaposto nelle sezioni già scritte e cambiato il conteggio dei blocchi di ogni documento pubblicato.
- **Nell'editor**: in testa alla sezione, in grigio, «Testo comune a tutte le torri radar». Per un admin c'è la barra
  del lock (`editor:section-common:radartower:technique`) e lo si modifica lì, da qualunque scalo: la stessa zona delle
  intro di pagina (`TestoComuneZone`, gemella di `PageIntroZone`). L'editor del documento la riceve da un parametro
  nuovo, `SectionPrelude`, che la pagina riempie solo per questa sezione.
- **«Da rivedere»** (nel riquadro del documento; nella lista generale è «Da fare», alla voce «da ripubblicare»): tipo
  nuovo `ImpactKind.CommonTextChanged`, **calcolato** e **da ripubblicare**. La deriva che c'è
  (`ReleaseDrift`) conta i blocchi delle sezioni e non vede un testo cambiato: questa guarda il testo comune congelato
  nella release in vigore e quello di adesso. Si apre solo dove il lettore lo vede: torre con radar **sì** e sezione
  visibile nella copia pubblicata. Si chiude da sola alla pubblicazione (`RunForDocumentAsync`) e al giro notturno; al
  salvataggio del testo comune il giro di questo tipo parte subito, così gli scali compaiono senza aspettare la notte.
- **Traduzione, allegati, immagini**: il giro della traduzione raccoglie anche queste frasi (`EfTranslatableCorpus`,
  come le intro); l'uso degli allegati e la pulizia delle immagini leggono già tutti i `SharedBlocks`.

## 3. Prova a schermo (22 ottobre, DB nuovo, LIRQ con torre radar «sì»)

- Editor: il riquadro grigio in testa alla tecnica operativa; preso il lock, scritti due callout (rosso e verde),
  salvati. Sotto, un paragrafo dello scalo.
- 🔴 **Trovato e corretto**: nell'anteprima il paragrafo dello scalo stava **fra** i due callout comuni. Il corpo della
  sezione (`SectionBody.Blocchi`) leggeva come «sconosciuto» ogni `Order` ≤ 0 e lo sostituiva con la posizione nella
  lista: gli ordini negativi del testo comune diventavano 1 e 2, e il paragrafo (ordine 1) finiva in mezzo. Ora è
  «sconosciuto» solo lo 0. Test `TestoComuneZoneTests.Nella_pagina_…`, rosso sulla regola di prima.
- Pubblicato; cambiato il testo comune → «Salvato · 1 documenti da ripubblicare in «Da fare»». La pagina pubblica
  mostrava ancora il testo vecchio (congelato nella release, visto anche nel JSON), la lista «Da fare» la riga
  «republish». Ripubblicato → riga chiusa da sola, pagina col testo nuovo.
- Nella lista «Da fare» il tasto «Che cosa è cambiato» non c'è per questa riga: il confronto dei blocchi direbbe
  «nessuna differenza» (`WorkItemRow.HaDettaglio`).
- **Non visti**: il vSOP militare (stesso codice, caricatore gemello), la pagina inglese tradotta (traduzione spenta in
  locale), un'unione vIPI + vSOP, l'allegato dentro il callout comune.

## 4. Da sapere

- Chi aveva già copiato a mano quei callout in uno scalo li vedrà **due volte** finché non li toglie: da guardare sulla
  copia di produzione prima della consegna.
- Il testo comune congelato si traduce con la memoria **viva** delle traduzioni: la fotografia delle traduzioni
  della release copre i segmenti del documento, non quelli della vista derivata.
- Nelle «sezioni da scrivere» la tecnica operativa resta fuori com'era (`SezioniDaScrivere`): la parte comune non la
  fa contare come scritta.
- Generico per chiave di sezione: un'altra sezione con una parte comune è una chiave in più e un posto dove la vista
  derivata la congela.

## 5. Dove si traduce (S128, 22 ottobre 2026)

Il committente, dopo la 1.72.0: «la parte comune di torre radar non sapevo dove si traducesse». La traduceva la
macchina (§2, `EfTranslatableCorpus`), ma **nessun posto la mostrava**: il pannello «Traduzione» dell'editor elenca le
frasi del documento, e il testo comune nel documento non c'è; il Registro la teneva mescolata alle frasi di tutta la
divisione.

| Deciso | |
|---|---|
| Dove | Nel pannello **«Traduzione»** dell'editor di ogni vIPI/vSOP che ha la «Tecnica operativa» del radar di torre: le frasi del testo comune in coda, segnate **«testo comune»** (`RigaDaRivedere.Comune`, `DocumentTranslationReview`) |
| Chi le corregge | Solo un **admin**, come il testo stesso: la resa vale per tutte le torri radar. Il servizio lo pretende sulla frase (`CorreggiAsync`); il pannello agli altri la mostra senza il tasto, e dice perché |
| Il conto | Aprendo una frase comune non si contano i documenti (direbbe zero: la frase non sta in nessun documento): si dice che la correzione vale per tutte le torri radar |

- **Stessa segmentazione** del giro che traduce (`DocumentTranslator.SegmentiSezione` su `TestoComune.ComeSezione`): con
  un'altra, le impronte non coinciderebbero e il pannello direbbe «manca» a frasi già tradotte.
- **Solo nei documenti italiani**: il testo comune è scritto in italiano, e la memoria si legge con la lingua del
  documento. In un documento inglese non compare; la resa è una sola, e si rivede da un qualunque documento italiano.
- **Anche** dove la torre è senza radar o la sezione è nascosta: chi scrive lo scalo può volerlo rileggere, e la resa è
  la stessa per tutte.
- Una frase che il documento ha **già** resta del documento (una riga sola, la corregge l'Editor come sempre).
- **Prova a schermo** (DB nuovo, LIRQ con radar «sì», testo comune di due frasi, editor in inglese): le due frasi in
  fondo al pannello, segnate; aperta da admin, la nota «vale per tutte le torri radar»; salvata → nella memoria come
  traduzione umana it→en; l'anteprima inglese la mostra in testa alla «Operating technique». Non visto a schermo: il
  pannello di chi non è admin (provato in bUnit), il vSOP.
- La Guida, alla voce «Testo comune», dice dove si rilegge.
