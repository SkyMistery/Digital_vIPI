using Vipi.Application.Content;

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


/// <summary>Un problema trovato in una tabella d'editor, in forma da tradurre: la chiave di
/// risorsa e i suoi argomenti. ⚠️ Separato dal testo apposta — così la regola si può provare senza montare un
/// localizzatore, ed è la parte che vale la pena provare.</summary>
public sealed record AirportTlIssue(string Key, object[] Args);

/// <summary>
/// Le regole della tabella dei livelli di transizione. È il <b>cuore deterministico</b> dell'editor: nessun
/// I/O, nessuna UI, solo righe che entrano e problemi che escono — l'invariante #8 del runbook di refactor
/// chiede esattamente questo prima di spezzare un componente.
/// </summary>
public static class AirportTlValidation
{
    /// <summary>Righe senza livello, estremi invertiti, fasce che si accavallano.</summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<TlEdit> rows)
    {
        var w = new List<AirportTlIssue>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i].Level))
                w.Add(new AirportTlIssue("Ape_IssueTlMissing", new object[] { i + 1 }));

            // Estremo assente = fascia APERTA da quel lato: «fino a 1013» e «da 1014 in su» si scrivono così.
            var aLo = rows[i].From ?? int.MinValue;
            var aHi = rows[i].To ?? int.MaxValue;
            if (aLo > aHi) w.Add(new AirportTlIssue("Ape_IssueQnhOrder", new object[] { i + 1 }));

            for (var j = i + 1; j < rows.Count; j++)
            {
                var bLo = rows[j].From ?? int.MinValue;
                var bHi = rows[j].To ?? int.MaxValue;
                if (aLo <= bHi && bLo <= aHi)
                    w.Add(new AirportTlIssue("Ape_IssueQnhOverlap", new object[] { i + 1, j + 1 }));
            }
        }
        return w;
    }
}

/// <summary>Le regole della tabella piste. Cuore deterministico, come <see cref="AirportTlValidation"/>.</summary>
public static class AirportRunwayValidation
{
    /// <summary>
    /// Identificativi ripetuti. ⚠️ Le righe SENZA identificativo si saltano e non sono un errore: sono la
    /// riga appena aggiunta, che non si è ancora finito di scrivere — segnalarla vorrebbe dire mostrare un
    /// avviso rosso a chi ha appena premuto «+ Pista».
    /// </summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<RwEdit> rows)
    {
        var w = new List<AirportTlIssue>();
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var id = (r.Ident ?? "").Trim();
            if (id.Length == 0) continue;
            if (!visti.Add(id)) w.Add(new AirportTlIssue("Ape_IssueRwDup", new object[] { id }));
        }
        return w;
    }
}

/// <summary>
/// Il picker delle frequenze collegabili: filtro e nomi delle posizioni. Cuore deterministico, provabile
/// senza montare niente.
/// </summary>
public static class AirportFrequencyPicker
{
    /// <summary>
    /// Le frequenze che corrispondono a quel che si sta scrivendo — callsign, frequenza o ICAO — al più 50.
    /// ⚠️ Il tetto non è cosmetico: il catalogo collegabile è l'intera divisione, e senza si disegnerebbero
    /// centinaia di righe dentro un menù a tendina a ogni tasto premuto.
    /// </summary>
    public static IEnumerable<LinkableFrequencyRow> Filtra(IEnumerable<LinkableFrequencyRow> tutte, string? cerca)
    {
        var q = (cerca ?? "").Trim();
        return tutte.Where(f =>
            q.Length == 0
            || f.Callsign.Contains(q, StringComparison.OrdinalIgnoreCase)
            || f.FrequencyMhz.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (f.Icao?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(50);
    }

    /// <summary>Il nome per esteso di una posizione ATC. Sconosciuta = si scrive com'è arrivata.</summary>
    public static string NomePosizione(string? position) => (position ?? "").Trim().ToUpperInvariant() switch
    {
        "ATIS" => "ATIS",
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        _ => position ?? "—",
    };
}
