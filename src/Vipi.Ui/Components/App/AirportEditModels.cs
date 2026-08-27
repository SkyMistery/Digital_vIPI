namespace Vipi.Ui.Components.App;

// I modelli di SCRITTURA delle sezioni dell'aeroporto (doc 14 §3g).
//
// ⚠️ Perché esistono accanto alle viste di lettura (`AirportTlRowView`, `AirportRunwayRowView`, …), e perché
// NON sono un difetto: le due forme sono davvero diverse. La lettura è una proiezione già formattata — la
// fascia QNH è la stringa «1014 – 1030», l'initial climb è una quota o un livello a seconda della TA — perché
// dev'essere serializzabile per il congelamento della release. La scrittura ha i campi separati e mutabili,
// perché è ciò che un `<input>` sa legare.
//
// È per questo che qui NON si applica il modello «un componente, due modi» che vale per AppSeparations o
// AppFrequencies: là lettura e scrittura sono la stessa riga. Il difetto vero, e quello che questo giro
// chiude, era un altro — questi editor stavano scritti DENTRO la pagina, 523 righe di marcatura che nessun
// test poteva montare.
//
// Erano classi private annidate in AeroportoEditorPage: pubbliche qui perché i componenti d'editor, che
// stanno fuori dalla pagina, devono poterle ricevere come parametro.

/// <summary>Riga in scrittura della tabella dei livelli di transizione: la fascia QNH ha estremi separati
/// (<c>From</c>/<c>To</c>, null = aperta), che in lettura diventano una stringa sola.</summary>
public sealed class TlEdit { public int Id; public int? From; public int? To; public string? Level; }

/// <summary>Riga in scrittura.</summary>
public sealed class RwEdit { public int Id; public string? Ident; public int? LengthM; public int? Bearing; public string? Tora; public string? Lda; public string? App; public string? Patterns; public string? Circling; }
/// <summary>Riga in scrittura.</summary>
public sealed class RuleEdit
{
    public string? Name;
    public HashSet<string> Dep = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Arr = new(StringComparer.OrdinalIgnoreCase);
    public int MaxTail = 5;                                    // vento in coda massimo (kt)
    public int? MaxCross;                                      // vento al traverso massimo (kt); null = nessun vincolo
    public string Surface = "any";                             // "any"|"dry"|"wet"
    public string? Note;
    public TimeOnly? TimeFrom; public TimeOnly? TimeTo;        // finestra oraria UTC (avanzate)
    public int DaysMask;                                       // bit0=Lun … bit6=Dom; 0 = tutti (avanzate)
    public string Parity = "";                                 // ""|"even"|"odd" (avanzate)
    public int? DateFromDay; public int? DateFromMonth;        // finestra stagionale ricorrente: giorno+mese, nessun anno (avanzate)
    public int? DateToDay; public int? DateToMonth;
}
    // Stessi campi editoriali delle importate: la quota «da concordare con l'APP» e la priorità fra le SID
    // dello stesso punto esistono già sull'entità (una tabella sola per manuali e importate), erano solo
    // scoperte dall'editor.
/// <summary>Riga in scrittura.</summary>
public sealed class SidEdit
{
    public string? Runway; public string? Fix; public string? Name; public string? Transition;
    public string? InitialClimb; public bool InitialClimbByApp; public string? Type;
    public string? Cat; public string? Wtc; public string? Condition; public int? Priority;
    // Stato di pubblicazione calcolato al caricamento con la regola del dominio (SidRow.IsPublicAt).
    public bool IsPublicNow = true; public string PublishFromCycle = "—";
}
/// <summary>Riga in scrittura.</summary>
public sealed class ImportedSidEdit
{
    public int Id; public string Fix = ""; public string Name = ""; public string? Runway; public string? Transition; public string? Type;
    public int? Priority; public bool ForcePublished; public bool NeedsReview; public bool IsPublicNow; public string PublishFromCycle = "—";
    public string? FixOverride; public bool CreateAlias;
    // Arricchimenti editoriali sovrapposti alla riga di sorgente (persistiti con la riga, preservati al reimport).
    public string? InitialClimb; public bool InitialClimbByApp; public string? Cat; public string? Wtc; public string? Condition;
}

