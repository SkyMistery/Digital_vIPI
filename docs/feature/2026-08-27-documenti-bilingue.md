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

Meccanica: regex che li avvolge in tag prima dell'invio; DeepL li lascia stare (`tag_handling=xml` +
`ignore_tags`) e tornano intatti. È meccanico e affidabile.

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

I generatori delle sezioni derivate hanno le stringhe **cablate**: italiano per ACC/APP, inglese per la
vLOA. Sono **due percorsi di codice paralleli**, ed è già costato una divergenza documentata
(`VloaSections` contro il registro del catalogo). Portarli a resx è lavoro meccanico ma vasto — e
**cancella quella duplicazione**. Guadagno collaterale reale, non un effetto secondario.

### ⚠️ Le sezioni derivate interpolano testo scritto a mano

Non sono pura macchina. Misurato sul dominio, ~15 campi di prosa umana dentro sezioni «derivate»:

`AgreementSection.Description` (la frase capofila delle tabelle di coordinamento) ·
`CoordinationAgreement.Note` · `CoordinationClause.HandoffLabel` / `ConditionCustomLabel` /
`ConditionLabel` / `LevelSpecial` · `SpecialArea.Description` / `ActivationDetails` ·
`AirportRunway.AppProcedures` / `Patterns` / `Circling` · `AirportRunwayRule.Name` ·
`AirportSid.Condition` · `AirportExtraSection.Title` / `Body`.

Se non passano dalla memoria di traduzione, **l'inglese esce a chiazze**: script tradotto, con buchi in
italiano dentro. È il difetto che farebbe sembrare rotta tutta la funzione.

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
termini tecnici — è nella **fraseologia**. «Riporta sottovento» diventerà qualcosa di plausibile che non
è la forma standard, e *plausibile ma sbagliato* è peggio di *assente*, perché nessuno se ne accorge.

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
4. **La pubblicazione è il cancello**: non si pubblica in EN con buchi. Un documento pubblico mezzo
   tradotto è peggio di uno solo in italiano.
5. **Lo snapshot di release congela anche la traduzione.** Senza, l'inglese pubblicato cambierebbe da
   sotto ogni volta che qualcuno corregge la memoria. Coerente con `RenderMode.Frozen`, e §5 ci si appoggia.

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
