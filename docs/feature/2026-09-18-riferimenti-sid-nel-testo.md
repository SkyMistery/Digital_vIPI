# Riferimenti SID nel testo: il nominativo si aggiorna da solo (18 settembre 2026)

> Stato: 🟡 **slice 1 in main** (18 settembre 2026): risoluzione e disegno. Slice 2-5 da fare (§A73).

**La richiesta del committente:** nei testi dei documenti (prosa e celle delle tabelle) si citano SID per
procedure particolari. Quando la SID si aggiorna dal sectorfile su GitHub (`OST1E` → `OST2E`), il nome citato
deve aggiornarsi da solo. **Prima le SID**; le STAR dopo, in un filone a parte (in vIPI oggi non esistono).

Decisioni del committente (18 settembre 2026): solo SID per ora · **anche nelle celle delle tabelle** · sì alla
conversione dei testi già scritti, se possibile.

## 1. Misurato sul dato vero (copia di produzione `vipi_1330`, 18 settembre)

- **1469 righe SID**, 1467 importate, **1258 nomi distinti**.
- **Citazioni esatte oggi: pochissime.** Solo la vSOP MIL LIBV (`CDC6A`, `CDC6B`, `VIE6A`: una tabella e un
  paragrafo). Poi forme **compatte** che nessun riconoscimento automatico può convertire da solo: `CDC6A/B`,
  `DOGUS5A/B`, `ROZHU5A/5B`, e `SID GOLF1` dove l'archivio ha `GOLF 1` (LIPL). ⇒ La funzione serve soprattutto
  a ciò che si scriverà; la conversione dell'esistente è piccola ma deve **elencare** le forme compatte, non
  indovinarle.
- **Una SID sono più righe, una per pista**: `CDC6A` c'è per 14L **e** per 14R, con due `StableKey` diverse
  (la chiave contiene la pista). Citare «CDC6A» non deve obbligare a scegliere una pista.
- **I nomi**: 1003 della forma semplice `ABC1D`; gli altri sono composti (`BRL1Z-ARL1K`, `ABS6A LOG7A`,
  `SALENTO5A`) o militari senza revisione (`GOLF 1`, `FRASCA DEP16`, `TACAN3`, `OMNI`).

## 2. La scelta che regge tutto: la chiave è la RADICE DEL NOME, non la `StableKey`

La radice è il nome con **la cifra di revisione di ogni pezzo sostituita da `?`**: `OST1E` → `OST?E`,
`BRL1Z-ARL1K` → `BRL?Z-ARL?K`. Pezzo = 2-7 lettere, una cifra, una lettera (`SALENTO5A` → `SALENTO?A`). Un nome
senza pezzi di quella forma (`GOLF 1`, `OMNI`) è una radice **esatta**: non ha revisioni.

Perché non la `StableKey` (i quattro motivi sono tutti misurati o letti nel codice):

1. contiene la **pista** → un riferimento per pista, e la stessa SID citata due volte in due modi;
2. contiene il **fix del parser**: un `SidFixAlias` nuovo o una correzione al parser la cambia, e il
   riferimento resta orfano (`FixOverride` invece no);
3. le SID **manuali** hanno `StableKey = null` → non si potrebbero citare;
4. **non è unica** (due revisioni dello stesso file hanno la stessa chiave).

Sulla copia di produzione le radici sono **1258, ambigua UNA sola**: `LIBG ROBO?H` = `ROBO1H` e `ROBO5H`, due
righe vive nello stesso file. Regola: vince la riga **pubblica al ciclo** e non nascosta; se ne restano più
d'una, la cifra più alta, e l'editor lo segnala.

## 3. Il formato nel testo

```
[[SID LIRF OST1E]]
```

- **ICAO + ultimo nome visto.** L'ultimo nome serve a tre cose: è **leggibile** nel campo di chi scrive; è il
  **ripiego** quando la SID non si trova più (niente colonna, niente migrazione); da lui si calcola la radice.
- Si risolve **al momento di mostrare**, dopo la traduzione, alla radice `LIRF OST?E` → `OST2E`.
- Il testo salvato **non cambia** quando la SID si aggiorna: la memoria di traduzione è indicizzata
  sull'impronta del segmento sorgente, quindi **nessun carattere si ripaga** al motore. Risolvere prima della
  traduzione, invece, cambierebbe l'impronta a ogni revisione.
- Nella pagina esce il **nome e basta**, come testo normale, in prosa e nelle celle (`TableBlock` le rende come
  testo semplice: dopo la sostituzione non serve altro).
- ✅ **Il nome si scrive COMPLETO** (committente, 18 settembre 2026): punto per esteso + designatore, `BANAV 9A`
  e non il codice troncato `BANA9A`. Il punto è quello della tabella, cioè quello **effettivo** (la correzione
  a mano, se c'è: LIRF «SIV» → `SOSIV 1E`). `RiferimentiSid.NomeEsteso`. Solo per la forma semplice: un
  composto (`BRL1Z-ARL1K`) e un nome militare (`GOLF 1`) non hanno un punto unico e restano come sono.
  ⚠️ La **chiave** resta il codice: il nome completo è solo ciò che si scrive. Dove i nomi non sono risolti
  (anteprime dell'editor finché non c'è la slice 3, ricerca) esce il codice dell'ultimo nome visto.

## 4. Da dove si prende il nome — DECISO dal committente (18 settembre 2026)

**La pubblica guarda la pubblica, l'editor guarda l'editor.** Il riferimento segue il Live/Freeze **della
tabella SID dello scalo citato**, non quello della sezione in cui sta il testo.

| chi guarda | nome preso da |
|---|---|
| editor e bozza | anagrafica viva, ciclo corrente (come la tabella SID dell'editor) |
| pagina pubblica | la tabella SID **pubblica** dello scalo citato: se la sua sezione `sids` è **Live**, derivata ora; se è **Freeze**, le righe congelate nella release in vigore di quello scalo |
| anteprima di una release | per lo scalo del documento, la stessa tabella SID dell'anteprima (ciclo della release); per gli altri scali, la loro pubblica |
| scalo senza vIPI pubblicata o senza sezione `sids` | anagrafica viva, ciclo corrente |

Nascoste (`IsHidden`) e non ancora in vigore (`IsPublicAt`) non contano, perché non sono nella tabella.

**Perché non «segue la sezione del testo»** (proposta del committente, scartata insieme a lui): l'interruttore
Live/Freeze esiste solo sulle sezioni **derivate** (`SectionCatalog.IsRenderModeToggleable`). Le editoriali, dove
sta quasi ogni citazione, sono sempre congelate nello snapshot. Il pubblico avrebbe detto `OST1E` mentre la
tabella SID, che nasce Live, diceva `OST2E`.

✅ **La radice sul nome paga due volte:** le righe congelate (`AirportSidRowView`) non portano né `StableKey` né
`Id`, ma portano il **nome**, e dal nome si calcola la radice. Quindi **nessuna voce nuova in `FrozenSections`**:
si confronta il riferimento con la tabella che il pubblico vede già.

## 5. Dove si aggancia (mappa del codice, 18 settembre)

**Non c'è un punto unico che costruisce i `BlockView`**: quattro produttori (`VipiViewService.Map`,
`AccDocumentAssembler.ToSectionView`, `DocumentTranslator.TraduciBlocco`, `PageIntro.Vista`), e la traduzione li
ricostruisce. La risoluzione va quindi **dopo la traduzione, nei caricatori**, con **una funzione sola**:

- `RiferimentiSid` (Application, puro): trova i riferimenti in `Body` e nelle stringhe di `BodyJson` (celle,
  note), raccoglie gli ICAO, sostituisce con una mappa radice → nome. Nessun IO.
- `ISidReferenceResolver` (Application): con gli ICAO del documento fa **una** query (gemella di
  `IAirportProfileReader.ListRunwayDataAsync`: una `ListSidsAsync(icaos)` nuova, **non** allargare quella
  esistente) e applica la regola del §2 alla fonte decisa nel §4 (per il pubblico: la tabella SID pubblica dello scalo citato).
- Chiamata nei caricatori, dopo la traduzione: `AirportMemberLoader`, `MilMemberLoader`, `AppMemberLoader`,
  `VloaListPage`, `AccVipiPage`, `PageIntroZone`. E nelle **anteprime dell'editor**
  (`DocumentSectionsEditor`, `DocumentBlocksEditor`), che oggi rendono il testo grezzo.
- Regola del 2: sei posti chiamano **la stessa** funzione; nessuno `switch` per tipo di documento.

**Fuori perimetro, dichiarato:** i payload militari strutturati (`MilTablePayload`: callsign, parcheggi), resi da
componenti propri e non da `TableBlock`. Un riferimento scritto lì uscirebbe grezzo: l'editor non lo offre.

## 6. Traduzione

`TextProtector.Protect`: regola nuova **subito dopo** `TogliMarcatori`, prima di ogni altra. Il riferimento si
deposita come `<x/>` **vuoto** (stile `Intraducibile`): il motore non lo vede, e una cella che contiene solo un
riferimento non parte nemmeno. Senza regola, `SiglaMaiuscola` proteggerebbe `LIRF` e `OST1E` a pezzi e
parentesi e `SID` andrebbero al motore.

Nel pannello delle traduzioni da rivedere, chi corregge a mano può rovinare il riferimento: al salvataggio si
controlla che i riferimenti della resa siano **gli stessi** del sorgente.

⚠️ **Trovato strada facendo, fuori da questa carta:** i link `[testo](allegato:slug)` **non sono protetti** nella
traduzione. Il motore li riceve grezzi e potrebbe tradurre «allegato», rompendo il link in silenzio. Voce
separata in §A73; la regola nuova la può coprire con la stessa riga.

## 7. Editor

- **Prosa** (`RichTextArea`): tasto **«SID»** nella barra → piccolo elenco con ricerca (SID dello scalo del
  documento per primo, poi qualunque ICAO) → inserisce il riferimento dov'è il cursore con `vipiInserisci`, che
  già manda il `change` sintetico (§CL, `2026-09-09-testo-ricco-nell-editor.md`: senza, il testo non torna nel modello).
- **Celle**: oggi sono `<input>` semplici, senza barra. Un tasto **«SID»** nella barra della tabella agisce
  sull'**ultima cella che ha avuto il fuoco** (un tasto per cella affollerebbe la tabella). `vipiInserisci` sa
  già scrivere in un `input`.
- Il modello da copiare per l'elenco è la ricerca di `AttachmentBlockEditor` (con il trucco `@key="_gen"`) e il
  filtro `SidFiltro` di `AirportSidsEditor`.

## 8. Riferimento che non si trova più

Nella pagina esce l'**ultimo nome visto**, senza segni per il lettore. L'editor elenca, in cima al documento,
i riferimenti che non si risolvono al ciclo corrente («`LIRF OST?E`: nessuna SID pubblica»), calcolati al
volo: niente tabella, niente `DocumentImpact` persistito per ora.

## 9. Ricerca

`IndiceDelleRelease.TestiDi` indicizza il testo grezzo: gli estratti mostrerebbero `[[SID LIRF OST1E]]` e
cercando `OST2E` non si troverebbe. Si sostituisce il riferimento col nome **prima** di indicizzare.

## 10. Conversione dei testi già scritti

Nell'editor, a richiesta: «Cerca SID citate». Propone i nomi **esatti** che corrispondono a una SID dello scalo
del documento (poi di qualunque scalo, se il nome è unico), **uno per uno con conferma**. Le forme compatte
(`CDC6A/B`, `ROZHU5A/5B`) e quelle che non corrispondono (`SID GOLF1` ≠ `GOLF 1`) si **elencano** come «da
sistemare a mano», senza toccarle.

## 11. Slice

| # | Cosa | Peso |
|---|---|---|
| 1 | Formato, radice, `RiferimentiSid` puro + test (radici, ambigua, composti, militari) · `ListSidsAsync` · resolver · chiamata nei 6 caricatori e nelle anteprime · ricerca | medio |
| 2 | Traduzione: regola in `TextProtector` + controllo sul pannello di revisione (+ `allegato:`) | piccolo |
| 3 | Editor: tasto SID in prosa e in tabella, elenco con ricerca | medio |
| 4 | Riferimenti non trovati in cima all'editor | piccolo |
| 5 | Conversione dell'esistente | medio |

**Nessuna migrazione.** Tre o quattro sessioni. Pacchetto MINOR, solo Application + Infrastructure + Ui.

## 12. Verifica

- Test puri su `RiferimentiSid` e sulla regola di protezione, con **mutazione** (tolta la regola → la cella va al
  motore).
- Dal vivo su copia del DB: inserire `OST1E` in un paragrafo e in una cella di LIRF, **cambiare a mano il nome
  della riga** in `OST2E` (simula il reimport), ricaricare pagina pubblica, anteprima di release, editor e
  versione inglese: deve dire `OST2E` ovunque e il testo salvato deve essere identico a prima.
- Prova che distingue: la stessa prova sul commit di prima deve mostrare il
  riferimento grezzo.

## 13. Le STAR, dopo

Stesso formato (`[[STAR LIRF …]]`) e stessa radice, quando esisteranno: entità, parser `.str`, import. Il
formato dei `.str` Aurora non è ancora stato letto (`lirf.str` comincia con blocchi MAPS). Carta a parte.
