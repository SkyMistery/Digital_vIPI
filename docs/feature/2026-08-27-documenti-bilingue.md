# Documenti bilingue — carta 🟣

> Si scrive in **italiano**, si legge in italiano **o** in inglese. La traduzione la fa una macchina,
> la corregge chi rivede il documento, e la correzione vale per tutti.
>
> Indipendente dai [vSOP militari](2026-08-27-vsop-militari.md): si può fare prima, dopo o in parallelo.

## 0. Il costo, misurato

| Corpus | Caratteri |
|---|---|
| Il `vipi.db` di oggi, 18 documenti | **23.344** (8.557 `Body` + 9.715 `BodyJson` + 5.072 titoli) |
| I 15 SOP militari trascritti per intero | **179.864**, ma ~metà sono coordinate/frequenze/identificatori → **~85k** reali |
| *(scala opposta)* IPI Roma ACC vero di ENAV | **2.783.011** |

La **prima traduzione di tutto** sta in un solo mese di piano gratuito DeepL (500k), con margine largo.
Dopo si paga solo il delta, e col dedup (§1) il delta è piccolo. L'ultima riga non è roba nostra — è lì
per dire quanto in fretta un corpus del genere sfonderebbe qualunque piano, se un giorno lo si
trascrivesse davvero.

## 1. Modello — la traduzione è una MEMORIA, non un campo del documento

La tentazione è `BodyEn`, `TitleEn`, `DescriptionEn`. Sono **19 campi editoriali** nel dominio oggi, e
ogni campo nuovo di domani ne vorrebbe una. È la domanda 1 del pre-flight che dice di no.

```csharp
public class TranslationUnit
{
    public int Id { get; set; }
    public string SourceLang { get; set; } = "it";
    public string TargetLang { get; set; } = "en";
    public string SourceHash { get; set; } = default!;   // sha256 del testo NORMALIZZATO ← la chiave
    public string SourceText { get; set; } = default!;
    public string TargetText { get; set; } = default!;
    public TranslationOrigin Origin { get; set; }         // Machine | Human
    public string? Engine { get; set; }                   // "deepl"
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
}
// indice unico (SourceLang, TargetLang, SourceHash)
```

Quattro proprietà vengono **gratis** da questa forma:

1. **Incrementale.** Cambio una frase → solo quella manca in cache. Non serve nessun meccanismo separato
   di «cosa è cambiato»: l'hash *è* il meccanismo.
2. **Dedup.** La stessa frase in cinquanta documenti = **una** chiamata. Sui SOP, pieni di boilerplate
   («Follow the altitude constraints described on the pictures unless instructed differently by ATC»),
   è il risparmio grosso.
3. **La correzione umana vale ovunque e per sempre.** `Origin = Human` non si sovrascrive **mai** dalla
   macchina, nemmeno se il motore cambia versione.
4. **Sopravvive a tutto.** Rinomini la sezione, sposti il blocco, ripubblichi: il testo è lo stesso, la
   traduzione c'è.

Il documento resta **uno**. Nessun documento gemello in inglese — quello drifta, sempre, ed è di nuovo la
domanda 1.

## 2. Segmentazione — la parte difficile, non l'API

Non si manda un blocco intero.

- **`Body` (Markdown)** → un segmento per **paragrafo / voce d'elenco**. Troppo grande e ritraduci tutto
  per una virgola; troppo piccolo e il motore perde il contesto e traduce peggio. Il paragrafo è il
  compromesso.
- **`BodyJson` (tabella)** → **cella per cella**, e solo le celle che sono prosa. Mandare il JSON intero
  distruggerebbe la struttura.
- **Titoli** (`Document.Title`, `DocumentSection.Title`) → un segmento ciascuno.

La normalizzazione prima dell'hash (spazi, a-capo, virgolette tipografiche) decide quanto il dedup
morde. Va scritta una volta e testata, perché due normalizzazioni diverse = due cache che non si parlano.

## 3. Il protettore — e la regola su VID e nomi

### 3a. Identificatori: si proteggono

Non si traducono: callsign (`LIPP_MIL_CTR`), ICAO (`LIRF`), punti (`QUIESA`, `RPN1`), navaid
(`RIV`, `CH 37X`), frequenze, livelli (`FL75`), piste (`16R`), squawk.

Meccanica: una regex li avvolge in un tag prima dell'invio, e il motore lo lascia stare
(`tag_handling=xml` su DeepL, `textType=html` su Azure).

**⚠️ Correzione del 27 agosto 2026, dopo la prima chiamata al servizio vero.** La carta diceva che i
segnaposto dovessero essere **vuoti** — `<x id="0"/>` — per poter affermare che nulla viaggia. Misurato
contro Azure Translator, quella scelta costa la frase:

| Forma inviata | Che cosa torna |
|---|---|
| `Contatta <x id="0"/> sulla <x id="1"/> e riporta sottovento.` | «Contact X **on and** Y bring it back downwind» — ordine rotto |
| `Contatta <x id="0">LIRF_TWR</x> sulla <x id="1">118.1</x> …` | «Contact LIRF_TWR **on** 118.1 **and** bring it back downwind» — come il testo non protetto |

Senza l'ancora, il motore perde l'ordine delle parole. Quindi le due categorie **si separano**, ed è la
distinzione che questa carta faceva già a parole prima che il codice la collassasse:

- **identificatori pubblici** (callsign, ICAO, punti, frequenze, livelli, piste) → viaggiano **dentro** il
  tag. Sono pubblici, e al motore servono per capire la frase;
- **dati personali** (VID, nomi del roster) → tag **vuoto**, il valore non lascia il processo. Lì il prezzo
  sulla qualità si paga volentieri: sono pochi segmenti, e sono proprio quelli che vogliono comunque una
  persona.

⚠️ Il cambio ha introdotto un difetto, trovato da un test e non a runtime: da quando il valore è visibile
dentro il tag, **una regola successiva può matchare lì dentro** e annidare un secondo segnaposto nel primo
(«SQUAWK» dentro `<x>SQUAWK 7000</x>`). Le sostituzioni si applicano solo **fuori** dai segnaposto già
piazzati.

### 3b. ⚠️ VID e nomi di persona NON escono di qui — decisione del committente

I dati pubblici si possono mandare a un servizio esterno. **VID e nomi utente no, mai.**

Non è una nota di policy: è un **cancello nel codice**, e va scritto come tale.

**Cosa è già salvo per costruzione.** Il timbro «chi ha pubblicato» — nome, posizione staff e VID — lo
rende `ValidityStamp.razor`, che è un **componente**: quel testo non esiste come stringa da tradurre e
non passa mai dal traduttore. Stessa cosa per `VidLink`/`VidText`. ✅

**Dov'è il buco vero, misurato.** La sezione `validity` è `HostAndBlocks`: sotto la scheda derivata
restano i **blocchi editoriali**, ed è lì che si scrive il **firmatario** di una vLOA — un nome, a mano,
in un campo che *è* testo da tradurre. Quello a DeepL ci finirebbe.

**Le tre difese, in ordine:**

1. **Strutturale.** Nomi e VID devono essere **derivati**, non digitati. Il timbro lo è già; dove serve
   un firmatario, si punta a una persona, non si scrive una stringa.
2. **Protettore.** Il pattern VID e i nomi del **roster staff** (`StaffMember.DisplayName`, già in casa)
   entrano nella stessa macchina del §3a: sostituiti da segnaposto prima dell'invio, ripristinati dopo.
   Il dato non lascia mai il processo.
3. **Fail closed.** Un segmento che dopo la protezione contiene ancora una sequenza tipo-VID **non si
   manda**: si marca «da tradurre a mano» e si mostra all'editor. Rifiutare è sicuro; ripulire in
   silenzio no, perché il testo cambierebbe sotto e la traduzione tornerebbe disallineata.

**Prova, non promessa:** un test che manda in pasto al protettore ogni segmento del `vipi.db` reale e
asserisce che nessun payload in uscita contiene un VID o un nome del roster. È la differenza fra una
regola scritta e una regola che tiene.

⚠️ **Da non fare**: loggare il payload in uscita. Un log di diagnostica che registra i testi inviati
riapre da solo il buco che il protettore chiude.

## 4. La prosa scriptata: giusto non mandarla — ma non è gratis

✅ **Fatto il 28 agosto 2026 — e si è rivelato molto più piccolo del preventivo.**

La carta diceva «portare i generatori a resx: lavoro meccanico ma vasto». Misurando prima di toccare, il
lavoro grosso **era già stato fatto**: le frasi di coordinamento — la prosa generata più complessa del
prodotto — passano da `ICoordinationSentenceTemplate`, un template caricato da file e sovrascrivibile a
caldo, e **`CoordinationSentenceTemplate.English` esisteva già**.

Quindi non mancava la traduzione: mancava **chi la sceglie**. Il template lo decideva la **famiglia** del
documento e non chi legge — la vLOA sempre inglese, l'ACC sempre italiano. Il difetto visibile era che un
lettore italiano apriva una vLOA tradotta e ci trovava dentro i coordinamenti ancora in inglese: la
schermata mezza tradotta che questa carta esiste per evitare.

**Che cosa è cambiato davvero.**

- `ReadingLanguageContext`, sullo stesso pattern di `ShapeReleaseContext` — «in che lingua sto
  componendo». Fuori da una cattura è la lingua **dell'interfaccia**, cioè la stessa chip della barra che
  decide tutto il resto: due sorgenti di verità sulla lingua darebbero di nuovo una schermata a metà.
- `CoordinationSentenceTemplate.For(lingua, italianoCorrente)`. ⚠️ Il ramo italiano prende il template
  **del file**, non una costante: la divisione lo personalizza, e prenderlo da una costante avrebbe fatto
  sparire quelle personalizzazioni proprio nel momento in cui si comincia a scegliere per lingua.
- **La prosa congelata guarda la lingua, il resto no.** `FrozenSections.GetProsa` scarta il congelato
  quando la lingua non combacia, e allora si ricompone live — nella lingua giusta. ⚠️ Solo la prosa:
  AoR, frequenze e minime sono numeri, geometrie e callsign, e scartarle mostrerebbe al lettore l'AoR **di
  oggi** invece di quella dell'AIRAC pubblicato, cioè romperebbe la promessa della release per risolvere
  un problema che quelle sezioni non hanno.
- Il congelamento dichiara la lingua **sorgente del documento**: senza, prenderebbe la cultura del
  circuito di chi ha premuto Pubblica, e la stessa release direbbe cose diverse a seconda di chi l'ha fatta.

**La prosa generata si SCEGLIE, non si traduce**, e vale la pena scriverlo: quelle frasi le scrive il
nostro codice, e di entrambe le versioni possediamo l'originale. Mandarle a un motore vorrebbe dire pagare
per tradurre una cosa che sappiamo già dire, e accettarne la fraseologia invece della nostra — proprio
mentre il resto della carta spiega che la fraseologia automatica è il rischio numero uno.

### ✅ I testi scritti a mano DENTRO le sezioni derivate — fatto il 28 agosto 2026

Le sezioni derivate non sono pura macchina: interpolano prosa umana che vive nell'**anagrafica**, non in un
documento. Misurato sul `vipi.db` reale, e il dato ribalta le priorità della carta:

| Campo | Righe | Caratteri |
|---|---:|---:|
| `SpecialAreas.ActivationDetails` | 230 | **19.159** |
| `SpecialAreas.Description` | 230 | **15.501** |
| tutti gli altri tredici campi messi insieme | 27 | **396** |

**35.056 caratteri**, cioè più dell'intero corpus editoriale — e il **99% in due colonne sole**. Gli altri
tredici campi che questa carta elencava sono rumore: quattro dei quali (le colonne delle clausole di
coordinamento) nel database <b>non esistono nemmeno</b>.

⚠️ **E il dedup rende il pezzo quasi gratuito**: 230 aree, ma appena **9 descrizioni e 6 attivazioni
distinte**. Quindici segmenti in tutto, una chiamata al motore, una volta sola.

**La lingua sorgente non è quella del documento.** Una descrizione d'area appartiene alla **sorgente** —
IVAO, che scrive in inglese («Reserved and designated for exclusive use by SO flights only») — e la stessa
area compare identica in una vIPI italiana e in una vLOA inglese. Entra quindi nel giro `en`, qualunque
documento poi la mostri.

`TranslationLookup` è **scoped** e carica la coppia di lingue **una volta per richiesta**: chi proietta
scopre i testi che gli servono strada facendo, quindi non può passare un elenco di impronte prima. Con 90
righe in memoria, una lettura sola costa meno di una query per area su 230 aree.

⚠️ **Il nome dell'area non si traduce mai.** «LI-R59 Capo Frasca» è un identificatore: tradurlo renderebbe
irriconoscibile la stessa area fra la carta e il documento, che è peggio che lasciarla in inglese.

## 5. Chi rivede, e quanto lontano arriva una correzione

**Rivede chiunque riveda il documento** — decisione del committente. Il correttore sta nell'editor,
accanto a ogni segmento tradotto, e vale il permesso che già serve per editare quel documento.
`ReviewedByUserId` + `ReviewedUtc` registrano chi e quando.

### Che cosa corregge, esattamente

Il testo **inglese**, a mano, e la sua versione sostituisce quella della macchina. Esempio: l'editor
scrive «Contatta la torre riportando sottovento», la macchina rende «Contact the tower reporting
downwind», il controllore riscrive «Contact tower, report downwind». Da lì in poi è quella.

⚠️ **La correzione non si attacca al documento: si attacca alla FRASE ITALIANA**, tramite il suo hash.
Quindi se qualcuno domani cambia la frase italiana — anche una parola sola — l'hash cambia, la voce non
c'è più e si riparte dalla macchina. **La correzione si perde, ed è giusto**: è un'altra frase, e la
vecchia resa potrebbe non valere più.

### ✅ L'invariante: le due lingue dicono la stessa cosa

**Quel che è scritto in italiano c'è in inglese, e viceversa.** Decisione del committente, 27 agosto
2026, e non è un limite del modello da compensare: è il **requisito** che il modello serve.

Un documento operativo che dice una cosa a un lettore e un'altra a un altro non è bilingue, è **rotto** —
e il guasto sarebbe silenzioso, perché nessuno legge le due lingue insieme.

Ne discende, e va rifiutato in codice, non solo scoraggiato:

- non esiste un «testo solo inglese» né una nota «solo per i piloti stranieri»: si scrive in italiano e
  si traduce;
- chi rivede corregge **come si dice** una frase, mai **cosa dice**. Cambiare il contenuto è un'edit del
  sorgente italiano, che passa dall'editor e dalla release come qualunque altra modifica;
- questa è la ragione per cui la memoria di traduzione batte due documenti veri: due documenti si
  possono disallineare, e prima o poi lo fanno. Qui la divergenza non è un rischio da sorvegliare — è
  **irrappresentabile**.

⚠️ **Il raggio d'azione va detto a chi corregge.** Poiché la memoria è indicizzata sull'hash, una
correzione fatta dall'editor di Roma tocca la stessa frase nel documento di Milano. È il superpotere
della forma e insieme il suo trabocchetto. Due mitigazioni, e bastano:

- prima di salvare, il correttore vede **«questa correzione tocca N documenti»**, con l'elenco;
- il raggio è **limitato dal congelamento** (§6): gli altri documenti cambiano solo alla loro prossima
  ripubblicazione, quando il loro editor vede comunque il diff. Nessuno si ritrova il pubblico cambiato
  sotto.

### La marcatura, finché nessuno ha riletto

Una traduzione macchina non revisionata su un documento operativo è un rischio, e il rischio non è nei
termini tecnici — è nella **fraseologia**.

✅ **Misurato, non temuto** (Azure, 27 agosto 2026): «Contatta LIRF_TWR sulla 118.1 e riporta sottovento»
torna **«Contact LIRF_TWR on 118.1 and bring it back downwind»**. Gli identificatori sono intatti, la
grammatica è giusta, e *«bring it back downwind»* **non è fraseologia**: la forma standard è «report
downwind». Plausibile, comprensibile, sbagliato — e nessuno se ne accorge leggendo. È la dimostrazione che
il glossario e la rilettura non sono un accessorio della funzione: sono la funzione.

- La vista EN nasce marcata **«traduzione automatica, non revisionata»**, e il badge sparisce **per
  sezione** man mano che qualcuno la spunta. `Origin` per segmento lo dice già.
- **Glossario DeepL** it→en di fraseologia della divisione: è la funzione giusta dell'API per questo
  caso. Va costruito e curato da un controllore, non da chi scrive il codice.

## 6. Quando si traduce

**Non sincrono al salvataggio**: bloccherebbe l'editor su I/O di rete, e un disservizio DeepL bloccherebbe
il *salvataggio*, che è inaccettabile.

1. Il salvataggio **accoda** i segmenti mancanti (dopo lookup in cache: quasi sempre pochi o zero).
2. Un giro schedulato svuota la coda — l'infrastruttura dei giri c'è già.
3. L'editor mostra «N segmenti mancanti», e chi vuole forza il giro.
4. **La pubblicazione avvisa, ma NON blocca.** ⚠️ *Correzione del 28 agosto 2026, in fase di esecuzione.*
   La carta diceva «non si pubblica in EN con buchi». Scritto così è sbagliato, e sbagliato in un modo che
   fa danno: bloccare la pubblicazione perché la traduzione è indietro rende **il documento italiano
   ostaggio di un servizio esterno**. Il testo italiano *è* il documento; la traduzione è un servizio.

   L'avviso c'è, e sta dove l'editor guarda davvero prima di pubblicare — l'**anteprima bozza**, che monta
   lo stesso `TranslationNotice` della vista pubblica e dice «4 frasi su 28 non sono ancora tradotte».
   Non è stato aggiunto un secondo meccanismo nel pannello di rilascio: sarebbe stata la stessa
   informazione in due posti, cioè il modo in cui due racconti divergono.

5. **Lo snapshot di release congela anche la traduzione.** ✅ Fatto: le traduzioni viaggiano **dentro
   `RawDocument`**, non nell'involucro della release, così arrivano da sole a chiunque legga il documento —
   vista pubblica, anteprima e bozza — senza plumbing per ogni percorso.

   ⚠️ **Non è cautela: è l'unico modo di limitare il raggio d'azione di una correzione.** La memoria è
   indicizzata sulla FRASE. Senza fotografia, chi corregge una resa su un documento cambierebbe l'inglese
   **già pubblicato** di ogni altro documento che contiene quella frase — sotto gli occhi di chi lo sta
   leggendo, e senza che il suo editor abbia pubblicato niente. Congelata, la correzione arriva agli altri
   alla **loro** prossima ripubblicazione, quando il loro editor guarda il diff.

   ⚠️ `RawDocument.Language` è **nullable**, e non per pigrizia: gli snapshot pubblicati prima di questa
   funzione non la portano, e un default farebbe dire a una vLOA — che nasce in inglese — di essere
   italiana. Il viewer tradurrebbe testo inglese come se fosse italiano.

   ⚠️ Il congelato si dichiara **non riletto**: lo snapshot porta il testo, non chi lo ha scritto.
   Sbagliare per eccesso di cautela costa un avviso di troppo; sbagliare al contrario vuol dire dichiarare
   riletta una frase che nessuno ha mai guardato, su un documento operativo.

## 7. Lingua sorgente contro lingua di lettura

`Document.Language` smette di significare «la lingua» e diventa **la lingua sorgente**. Il lettore
sceglie la sua, e la scelta deve essere **lo stesso controllo** della lingua dell'interfaccia — uno, non
due: un documento inglese dentro un'interfaccia italiana è una schermata mezza tradotta.

⚠️ Trappola già pagata: `/_blazor` non porta `?culture=`. Senza cookie il circuito ridisegna nella lingua
del browser.

⚠️ La vLOA nasce `En`. Per lei l'italiano è il **bersaglio**, non la sorgente: la stessa macchina, con
`SourceLang`/`TargetLang` invertiti. Nessun caso speciale, purché non si dia mai per scontato che la
sorgente sia l'italiano.

## 8. Slice di esecuzione

| # | Slice | Verde su |
|---|---|---|
| 1 | `TranslationUnit` + migrazioni + normalizzazione e hash (cuore puro, test-first) | test |
| 2 | Segmentatore Markdown / tabella / titoli, senza rete | test |
| 3 | Protettore identificatori **+ VID/roster** + il test sul `vipi.db` reale (§3b) | test |
| 4 | Client DeepL dietro interfaccia + finto per i test; glossario; chiave nella cartella segreti | test |
| 5 | Coda + giro schedulato + stato «N mancanti» nell'editor | live |
| 6 | Lettura bilingue: selettore unico lingua UI+documento, cookie, badge «non revisionata» | live |
| 7 | Correttore nell'editor + avviso «tocca N documenti» + `Origin = Human` | live |
| 8 | Cancello alla pubblicazione + congelamento della traduzione nello snapshot | test |
| 9 | Generatori derivati da stringhe cablate a resx (**il pezzo grosso**, meccanico) | test |
| 10 | I ~15 campi editoriali dentro le derivate (§4) nella memoria | test |

Le slice 1-3 non toccano la rete e valgono da sole: se ci si ferma lì, si è comunque costruito il
segmentatore e il cancello sui dati personali.

## 9. Da verificare, non da assumere

1. **Termini del piano gratuito DeepL API**: richiede carta, ha limiti d'uso, e **la ritenzione dei dati
   sul piano gratuito non è quella del piano a pagamento**. Da leggere prima di impegnarsi, non dopo.
2. **IVAO HQ**: mandare i testi a un terzo è trattamento esterno. I documenti sono pubblici, quindi è
   poco delicato, ma HQ ha già posto un vincolo contrattuale sui PDF — meglio chiedere prima.
3. **Il glossario di fraseologia ha bisogno di un nome**, non di un ruolo. Senza qualcuno che lo curi, la
   §5 resta una buona intenzione.

## Che cosa ha insegnato il primo documento VERO (28 agosto 2026)

Il bilingue era chiuso e provato. Poi è arrivato un SOP militare vero — testo denso di identificatori,
tabelle, coordinate — e ha trovato **tre** cose che il corpus di prova non aveva.

| | Che cosa | Dove sta la correzione |
|---|---|---|
| ⚠️⚠️ | **«MARTE» → *MARS*, «CHI» → *WHO*.** Una cella che è *solo* un identificatore non ha minuscole, e la regola sulle sigle maiuscole si applicava solo «se c'è prosa attorno». La condizione giusta è **«è una parola sola»** | `TextProtector.UnaParolaSolaMaiuscola` + `SoloSegnaposti`, che ferma il segmento **prima** della rete |
| ⚠️ | **Un `**` orfano stampato a schermo**: i marcatori non si proteggono (il motore infila le parole nei tag), quindi ogni tanto ne perde uno | `TranslationText.RiparaGrassetto`: se non tornano, si tolgono tutti — un grassetto perso si nota meno di due asterischi |
| ⚠️ | **Le intestazioni delle tabelle erano tutte sbagliate**: «Pista» → *Track*, «Quota» → *Share*, «Piazzale» → *Forecourt* | `TitoliUfficiali.Termini`, seminati come **Human**: la memoria è per segmento intero, e una cella *è* un segmento |

⚠️ **Il filo comune**: nessuna delle tre si vedeva sul corpus di prova, perché quello era fatto di **prosa**.
Un documento tecnico è fatto per metà di **celle**, e una cella si comporta in modo diverso da una frase —
non ha minuscole attorno, non ha contesto, e vale come dato e non come testo.

**Misura**: 28 segmenti su 218 di quel documento sono identificatori puri. Adesso non partono più, e sono
anche caratteri risparmiati.

## Che cosa ha insegnato la prima pagina PUBBLICA (28 agosto 2026)

La slice 6 era segnata «live» e lo era davvero — ma su **due** viewer su cinque. Chiesto perché la vIPI di
Crotone (`/services/vsop/libb/airports?icao=LIBC`) non mostrava traccia di traduzione, la risposta non era
nei dati: `DocumentTranslator` era iniettato **solo** in `MilDocumentPage` e `VloaListPage`. L'aeroporto,
l'APP non remotizzato e la vIPI ACC non lo chiamavano affatto. Nessun test poteva accorgersene, perché il
traduttore da solo funzionava.

| | Che cosa | Dove sta la correzione |
|---|---|---|
| ⚠️⚠️ | **Tre viewer su cinque non traducevano.** Il documento restava in italiano dentro un'interfaccia inglese, senza avviso: per il prodotto era «niente da tradurre» | `AeroportoPage`, `AppnPage`, `AccVipiPage`: traduzione + `<TranslationNotice>`, come le altre due |
| ⚠️⚠️ | **La vIPI ACC non è un `DocumentView`**: vive a blocchi, e il traduttore di documento non la sa leggere | `AccVipiTranslator` sopra `DocumentTranslator.PreparaAsync`: la memoria, la copertura e la preferenza per le congelate restano una implementazione sola |
| ⚠️ | **La lingua sorgente era CABLATA nella pagina** («it» il militare, «en» la vLOA): un secondo posto che dichiara la lingua, e che può contraddire il documento | `DocumentTranslator.CodiceSorgente(view.Language, predefinita)`: la famiglia dice solo in che lingua **nasce**, per gli snapshot salvati prima del campo |
| ⚠️ | **Titoli tradotti al 100% e testate ancora italiane.** Il viewer d'aeroporto non mostra il titolo del DOCUMENTO ma quello del **catalogo**, che è una stringa italiana cablata: «Regole piste» in mezzo alla prosa inglese, e la copertura diceva «completa» perché quei titoli al traduttore non erano mai passati davanti | `TranslatedDocument.Pass` + `SectionHeading` che traduce anche il titolo di catalogo, con la stessa impronta |

⚠️ **Il filo comune**: quattro difetti su quattro erano **davanti agli occhi e invisibili ai test**. Il
traduttore, la memoria, la copertura e il congelamento avevano ognuno i suoi test verdi; quello che mancava
era **chi chiama chi**, e quello si vede solo aprendo la pagina. Vale la regola già scritta per le
regressioni Blazor: la verifica è guidare il flusso reale, non la suite.

**Verificato il 28 agosto 2026** su copia del `vipi.db` (host su :5199, `?culture=`): aeroporto LIBC,
vIPI ACC LIBB, APP LIBA_APP — avviso e testate in inglese; vLOA LDZO — tradotta in italiano e originale in
inglese, che è il verso opposto e la prova che la sorgente arriva dal documento.

⚠️ **Quello che la pagina vera ha fatto vedere, e non è codice**: la memoria contiene rese **plausibili e
sbagliate** — «Regole piste» → *Slope rules*, «Minime di vettoramento» → *Minimum vectoring*. Il badge le
dichiara non riviste, ed è esattamente il lavoro che la §5 aspetta da una persona con un nome.

### E poi mancava il COMANDO (28 agosto 2026, sera)

Agganciate le cinque pagine, la domanda successiva è stata: «sono sulla vIPI di Crotone e ancora non si
può passare da italiano a inglese». Aveva ragione, e non era un residuo delle pagine: **il selettore di
lingua non esisteva**. La slice 6 dichiarava «selettore unico lingua UI+documento, cookie, badge»; erano
stati fatti il cookie (`CultureCookieMiddleware`), la risoluzione per richiesta e il badge — la lingua si
poteva chiedere solo **scrivendo `?culture=` nell'indirizzo**.

| | Che cosa | Dove sta la correzione |
|---|---|---|
| ⚠️⚠️ | **Nessun controllo per cambiare lingua** in tutta l'interfaccia | Gruppo `IT | EN` in barra + le due voci nel «☰», entrambi LINK: il chrome è SSR statico e cambiare lingua è ricaricare questa pagina chiedendola in un'altra lingua — funziona a JavaScript spento |
| ⚠️ | **Le lingue servite e le chiavi di query erano scritte in due file privati** (`VipiModuleExtensions`, `CultureCookieMiddleware`) che la UI non può vedere: il selettore avrebbe fatto una terza copia | `LinguaDiLettura` in `Vipi.Application.Content`, e i due file dell'hosting ora leggono di lì |
| ⚠️⚠️ | **Indice in italiano e testate in inglese** sulla stessa pagina: `AirportLegacySections.ForView` riporta ogni sezione di catalogo al suo titolo CABLATO, quindi **buttava via il titolo appena tradotto** | Le sezioni si ripassano dalla stessa passata dopo `ForView` (zero query, la memoria è già in mano); `TitleOf`/`SectionHeading` spariscono, erano il secondo posto che rileggeva il catalogo |

⚠️ **Due chip e non un tasto che gira** come quello del tema: su un tasto solo non si sa se «EN» è la lingua
in cui sei o quella in cui andresti, e a differenza del tema qui l'errore non si vede finché la pagina non
si è già ricaricata. Costa ~30px in barra, che la misura trova; dallo scaglione `tb-4` il gruppo esce di
riga e la scelta resta nel «☰», come zoom e badge.

⚠️ **Il link riparte dall'indirizzo vero**, query compresa: un `?culture=en` fisso su
`/airports?icao=LIBC` avrebbe riportato all'elenco degli aeroporti. Cambiare lingua deve cambiare la lingua
e basta. Ed è un **percorso assoluto senza schema né host**, perché in produzione davanti c'è Cloudflare.

**Verificato guidando il browser** (finestra 1440, host su :5199 su copia del `vipi.db`): la barra non
sfora, «IT» è segnato, il clic su «EN» resta su LIBC e traduce indice e testate, il cookie regge il
ricarico senza `?culture=`. Cinque test E2E nuovi (`SelettoreLinguaTests`) più uno di caratterizzazione su
`ForView`, che è il posto dove la traduzione si perdeva.

⚠️ **Quello che a schermo si vede ancora in italiano, e non è questa funzione**: le etichette del riquadro
meteo («VENTO», «VISIBILITÀ», «NUBI», «aggiornato»), il badge «Live · non connesso» e il testo delle regole
pista scritto a mano nell'anagrafica. Le prime due sono **stringhe cablate nei componenti** — è la slice 9,
«generatori derivati da stringhe cablate a resx», che resta aperta; la terza è prosa d'anagrafica che
entrerà in memoria al prossimo giro di riempimento.

### E infine le due cose che ha chiesto il committente (28 agosto, sera tardi)

| | Che cosa | Perché |
|---|---|---|
| **«MRVA»** al posto di «Minime di vettoramento», uguale nelle due lingue | È la sigla con cui la si chiama in frequenza e sulle carte: come «SID» o «AOR» non si traduce. Il motore rendeva il titolo con *Minimum vectoring* — giusto a metà, e comunque non la sigla |
| Il **correttore delle traduzioni dentro l'editor** | Chi scrive un documento è l'unico che sa se «riporta sottovento» è diventato *report downwind* o *bring it back downwind*. Il Registro admin elenca le frasi di tutta la divisione: è il posto per un giro di revisione, non per chi ha appena scritto |

⚠️ **Il titolo di una sezione di catalogo sta NEL DOCUMENTO**, non nel catalogo: cambiare `SectionCatalog`
vale per i documenti nuovi e sui documenti già scritti non cambia niente. Serve un passo d'avvio
(`RenameMinimaSectionsAsync`, 19 sezioni sul `vipi.db` di prova) che rinomina **solo i titoli vecchi** — un
nome scelto da un editore è una scelta e non si sovrascrive. Le release già pubblicate restano com'erano
finché non si ripubblica.

⚠️ Nel correttore, tre vincoli che non sono dettagli: si corregge **come** si dice e mai **cosa** (il testo
sorgente è la chiave della memoria e lì non si tocca); il permesso è quello del **documento** e non l'admin
(ridire in un'altra lingua quel che un documento afferma è un atto editoriale su quel documento); e il
**titolo del documento non è fra le frasi**, o si inviterebbe a correggere una cosa che il viewer ignora.

⚠️ `IDocumentForReview` è la faccia stretta dell'editing — «dammi il documento in lavorazione» e nient'altro.
Far dipendere il correttore da tutto `IEditingService` vorrebbe dire dargli in mano l'editing intero per una
lettura, e obbligare ogni suo test a implementare trenta metodi che non chiamerà mai.

**Stato**: ramo `bilingue-tutte-le-pagine` (`2af3a39`), sei commit, spinto e non fuso. Suite verde su net8 e
net10, build Release senza avvisi. Il seguito sta in `docs/lavori-aperti.md` §Q-bis.
