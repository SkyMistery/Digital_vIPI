# Lingua bloccata — carta ✅

> Un documento può dichiarare che **si legge in una lingua sola**: quella in cui è scritto. Il sito resta
> bilingue, il documento no.
>
> Estende [documenti bilingue](2026-08-27-documenti-bilingue.md), non la sostituisce: là si scrive in una
> lingua e si legge in due, qui si dice «questo no».

## 0. La domanda

«Un vSOP solo in inglese, anche nel sito italiano.» Il caso reale: un documento redatto in inglese —
perché così lo vuole chi lo firma, o perché il pubblico è internazionale — che **non deve** comparire
tradotto a macchina in una pagina italiana.

Due bivi, decisi dal committente prima di scrivere:

| Bivio | Deciso |
|---|---|
| Che cosa resta nella lingua del sito | **Solo il chrome** (barra, menu, briciola, tasti). Il documento — titoli di sezione e sottosezione, intestazioni di tabella, contenuti — sta tutto nella sua lingua |
| Che vuol dire «bloccato» | **La lingua in cui il documento è scritto.** Il blocco *spegne* la traduzione, non la fa: un documento italiano non diventa inglese bloccandolo, va scritto in inglese |

Conseguenza da dire in faccia: **zero caratteri pagati** al motore per un documento bloccato, e zero rese
plausibili-e-sbagliate. È il lato buono della decisione.

## 1. Modello — un flag, non una seconda lingua

```csharp
public class Document
{
    public Language Language { get; set; }          // c'era già: la lingua in cui si REDIGE
    public bool LanguageLocked { get; set; }        // nuovo: si serve SEMPRE in Language
}
```

Domanda 1 del pre-flight (modello gemello): **no**. La lingua sorgente esiste già ed è una sola
(`Document.Language`); qui si aggiunge se **seguire il lettore** o no. Un secondo campo «lingua di
pubblicazione» sarebbe un posto che può contraddire il primo — lo stesso errore che il 28 agosto aveva
cablato «it»/«en» dentro le pagine invece di chiederlo al documento.

`false` per tutti i documenti esistenti = comportamento di oggi, invariato. Migrazione additiva
(`bool NOT NULL DEFAULT 0`) su entrambi i provider: passa la guardia della
[finestra cieca](../lavori-aperti.md) — niente `Drop`/`Rename`/`AlterColumn`/`Sql`.

⚠️ Qui il default `false` è quello **giusto**, ed è l'eccezione alla trappola nota: un `bool NOT NULL`
aggiunto dopo il primo deploy nasce `false` ovunque, e per un flag *opt-in* è esattamente ciò che serve.

## 2. ⚠️ Il canale su cui viaggia era rotto — misurato, non dedotto

`EfContentRepository.BuildRawFromVersionAsync` costruisce il `RawDocument` dello snapshot di release e
**non copia `Language`**. Sul `vipi.db` vero:

```
grep -a -o '"Language":[^,}]*' vipi.db  →  13 × "Language":null
grep -a -o '"Translations":[^,]*'       →  13 × "Translations":null
```

Confermato su una seconda copia (`vipi.db.bak-pre-consegna-20260830`: 5 su 5). Due conseguenze che nessuno
aveva visto perché **non danno errore**:

1. **Il congelamento delle traduzioni non è mai scattato.** `ReleaseService.ConTraduzioniCongelateAsync`
   esce subito se `raw.Language is null` → nessuna release ha mai portato una traduzione congelata. Tutta
   la §6 della carta bilingue — scritta, testata, discussa — in produzione non gira. I test la coprono dal
   lato del **lettore** (`TraduzioniCongelateTests` costruisce le viste a mano), mai dal lato di chi
   **scatta la fotografia**.
2. **La prosa derivata si congela sempre in italiano.** `linguaSorgente` cade su `"it"` per default, quindi
   la cattura di una vLOA — che nasce inglese — è andata in italiano per ogni release pubblicata.

Il blocco lingua deve viaggiare sullo stesso canale del `Language`. Ripararlo non è deviazione di rotta: è
la precondizione. Si ripara qui, con la misura in mano.

## 3. Dove si decide la lingua — un posto solo

```csharp
// LinguaDiLettura
public static string DelLettore();                                       // la chip in barra
public static string PerIlDocumento(bool bloccato, Language? sorgente, Language predefinita);
```

`PerIlDocumento` è l'unica funzione che il prodotto interroga: bloccato ⇒ la lingua del documento,
altrimenti quella di chi legge. Le sei pagine che oggi scrivono
`CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` la chiamano invece di ripetersi.

⚠️ Il blocco si legge **dal documento vivo, non dallo snapshot**. Accendere il blocco deve valere subito su
un documento già pubblicato: è una regola di *servizio* («questo si legge in inglese»), non un contenuto
congelato. Un ciclo AIRAC dura quattro settimane e nessuno aspetta la ripubblicazione per smettere di
mostrare una traduzione automatica che non vuole. Nello snapshot il flag ci va lo stesso — così la
fotografia si descrive da sé e chi la legge fra un anno sa com'era — ma a decidere è il documento.

## 4. I quattro strati della prosa

La lingua che si vede in pagina esce da **quattro posti diversi**. Un blocco che ne prende tre lascia una
schermata mezza tradotta, che è il difetto che questa funzione esiste per evitare.

| # | Strato | Da dove esce | Come segue il blocco |
|---|---|---|---|
| 1 | Titoli di sezione e sottosezione, corpi, **celle di tabella** | DB, memoria a impronta | `DocumentTranslator`: sorgente == bersaglio ⇒ torna l'originale **senza toccare il database** (già codificato) |
| 2 | Prosa generata dal backend (derivate, frasi di coordinamento) | codice | `ReadingLanguageContext` |
| 3 | **Intestazioni delle tabelle derivate**, etichette, chip | `SharedResource.resx`, `L["…"]` in **126 razor** | localizzatore avvolto in DI |
| 4 | Titoli di sezione **di catalogo** | `SectionCatalog`, 72 descrittori italiani cablati | `SectionDescriptor.TitleEn` |

### Strato 3 — il localizzatore avvolto, e IL CONFINE

Modificare 126 file razor sarebbe il modo sbagliato: la prossima pagina scritta se ne dimenticherebbe, e
nessuno se ne accorgerebbe (una tabella con l'intestazione nella lingua sbagliata non è un errore, è una
sfumatura). Si avvolge **la registrazione**:

```
IStringLocalizer<SharedResource>  →  LocalizzatoreDiLingua
    ctx.Fissata is null ? inner[chiave] : ResourceManager.GetString(chiave, cultura)
```

Precedente identico già in casa: `Vipi.Ui/EnglishStrings.cs` legge dal `ResourceManager` con la cultura
scritta a mano, perché `IStringLocalizer` risolve sempre sulla cultura corrente — che è esattamente ciò che
qui va ignorato.

⚠️ **Lettura sincrona, nessun `await` dentro**: la cultura non si sposta e non ci sono continuazioni che
possano vederla a metà.

⚠️ **E il chrome NON si salva da sé.** Questa carta lo dava per fatto — «il layout rende prima del corpo» —
e a schermo era falso: su LIBD bloccato in inglese la pagina mostrava «Print / SUMMARY / LINKS» dentro un
sito italiano. Il secondo tentativo, accendere la lingua nel componente del corpo per lasciare fuori
l'arredamento, ha prodotto una pagina **a chiazze**: «Ciclo AIRAC» italiano accanto a «Print» inglese, e un
callout «Nota» rimasto italiano dentro un documento inglese.

⚠️ **In Blazor una pagina si rende PIÙ VOLTE**, e l'ordine fra genitore e figli non è una leva su cui
appoggiare una regola di prodotto. Il confine è **esplicito**, e sta scritto in un posto solo
(`StringheDelSito`):

> **Dentro una pagina documentale, `L` è la lingua del DOCUMENTO e `Sito` è quella di chi guarda.**

Nelle cinque pagine viewer quasi tutto è arredamento — il tasto «Stampa», la colonna di destra, l'indice, la
fascia dell'anteprima — e passa a `Sito`; restano a `L` le poche stringhe che appartengono al documento (le
intestazioni delle tabelle di un vSOP, «(live · NOAA)» accanto alla testata METAR, il titolo di un blocco
della vIPI ACC).

⚠️ **Le ISOLE non vedono niente di tutto questo.** Un componente con `@rendermode` vive in un **circuito
suo**, con uno scope di DI suo: la lingua imposta alla richiesta della pagina lì non arriva. Sono due, e
stanno tutte e due dentro un documento — il riquadro METAR e la tabella SID: ricevono la lingua come
**parametro** e risolvono le stringhe a mano. ⚠️ Non si impone al contesto del circuito: in Blazor Server
uno scoped vive quanto il **circuito**, non quanto la pagina, e resterebbe acceso sulle pagine visitate dopo.

### Strato 4 — il catalogo diventa bilingue

`SectionDescriptor` prende un `TitleEn`, come `GuideSearchCatalog.Entry` ha `TitleIt`/`TitleEn`: stesso
strato, stesso modo, nessun meccanismo nuovo. Chiude anche un difetto **già aperto** della carta bilingue
(§Q18a): su una vIPI d'aeroporto le testate di catalogo non si traducevano mai, perché non sono segmenti
del documento e il traduttore non le vede passare. A schermo: pagina inglese con indice «Quote di
transizione / Frequenze / Piste».

✅ E le etichette dei **callout** («Nota», «Attenzione», «Importante») erano **letterali italiani** dentro
un componente del corpo: si vedevano già prima di questa carta, su qualunque lettura in inglese. Ora vengono
dai resx.

⚠️ `EfDocumentMaintenance.ReconcileCookedSections` **riscrive** i titoli delle sezioni d'aeroporto col
titolo di catalogo a ogni avvio. Il titolo memorizzato resta quello italiano di catalogo; è
`AirportLegacySections.ForView` a scegliere la lingua a view-time. Una sola verità, applicata dove si
legge.

## 5. Che cosa vede chi legge

- **L'avviso «tradotto a macchina» non compare**: la copertura di un documento bloccato è `Nessuna`, e
  `TranslationNotice` è già muto a zero segmenti. Non è un caso da trattare: è la forma che si spegne da sé.
- **Una nota nuova**: «Questo documento è pubblicato solo in inglese» / «This document is published in
  English only», nella lingua di chi legge — è il chrome che parla, non il documento.
- **`lang` sul contenitore del documento**, non sulla pagina: `<div lang="en">` attorno al documento è la
  verità (il chrome è italiano, il documento no), ed è ciò che serve a chi usa un lettore di schermo.
- **`hreflang` resta**: la *pagina* esiste davvero in tutte e due le lingue — cambia il chrome. È il
  documento a non cambiare, e per quello parla la nota.

## 6. Chi lo accende, e che cosa costa

- L'interruttore sta in **`ReleasePanel`**, accanto alla pubblicazione: la richiesta è «la lingua
  dell'editor in pubblicazione», e lì la si guarda. Permesso = quello del documento; voce di audit.
- La **lingua del documento** diventa scegliibile lì stesso. Oggi è cablata (`EditingService`:
  `Vloa → En`, tutto il resto `It`) e un vSOP inglese non si poteva nemmeno dichiarare.
- `EfTranslatableCorpus` **esclude i documenti bloccati**: non si pagano caratteri per prosa che nessuno
  vedrà mai tradotta. (Una frase presente *anche* in un documento non bloccato si traduce lo stesso — la
  memoria è indicizzata sulla frase, non sul documento.)
- `ReleaseService` non congela traduzioni per un documento bloccato: non c'è niente da proteggere.
- Il pannello «Traduzione» dell'editor sparisce sui bloccati: un giro di revisione senza niente da rivedere.

## 7. Ingressi e verifica (pre-flight §3)

**Ingresso**: `ReleasePanel` di ognuno dei quattro editor documentali — nessun catch-22, l'interruttore sta
su documenti che esistono già.

**Verifica**: sito in italiano, documento bloccato in inglese, **a schermo** su copia del `vipi.db`
(`.db` **e** `-wal`, o è un database di ore prima). Si guardano tutti e quattro gli strati nella stessa
schermata: indice, testate di sezione, intestazioni di una tabella derivata, celle di una tabella
editoriale. Più il contrario: documento non bloccato, che deve continuare a tradursi come ieri.

## 8. Slice

| # | Slice | Stato |
|---|---|---|
| 1 | Carta | ✅ |
| 2 | Modello + migrazione (due provider) + snapshot, **incluso il `Language` perduto** (§2) | 🟡 |
| 3 | Lingua di lettura in un posto solo + strati 1–2 sulle cinque pagine | 🟡 |
| 4 | Strato 3 — localizzatore avvolto | 🟡 |
| 5 | Strato 4 — catalogo bilingue (chiude §Q18a) | 🟡 |
| 6 | Nota al lettore + `lang` sul contenitore | 🟡 |
| 7 | Interruttore in `ReleasePanel` + lingua scegliibile + audit | 🟡 |
| 8 | Corpus, congelamento, pannello di revisione | 🟡 |
| 9 | Test + `dotnet build -c Release --no-incremental` + verifica live | ✅ |

Tutte ✅. Suite: **9192 verdi** su entrambi i TFM, `dotnet build Vipi.slnx -c Release --no-incremental`
pulita.

## 9. Che cosa ha detto lo schermo

Guidato con Edge su copia del `vipi.db` (LIBD, ACC LIBB), bloccando e sbloccando **dall'editor vero**:

| | sito IT | sito EN |
|---|---|---|
| **bloccato (EN)** | documento tutto inglese — indice, testate, intestazioni di tabella, chip METAR («WIND / VISIBILITY / CLOUDS»), chip SID, callout «Note», prosa derivata; chrome italiano — «Stampa», «SOMMARIO», «RIEPILOGO», «Ciclo AIRAC»; avviso «Documento in una lingua sola» **in italiano** | tutto inglese, stesso avviso in inglese |
| **sbloccato** | documento italiano, nessun avviso: com'era ieri | documento tradotto a macchina, con l'avviso di sempre |

⚠️ Tre difetti li ha trovati **solo lo schermo**, e nessuno di loro faceva cadere un test: il chrome che
seguiva il documento, il callout «Nota», e le due isole rimaste nella lingua di chi guarda.
