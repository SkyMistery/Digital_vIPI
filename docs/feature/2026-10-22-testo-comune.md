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
