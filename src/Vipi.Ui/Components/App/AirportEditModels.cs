using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;

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

/// <summary>Esito della validazione delle regole piste: gli errori impediscono il salvataggio, gli avvisi no.</summary>
public sealed record AirportRuleIssues(
    IReadOnlyList<AirportTlIssue> Errors, IReadOnlyList<AirportTlIssue> Warnings);

/// <summary>Le regole della tabella «Regole piste». Cuore deterministico.</summary>
public static class AirportRuleValidation
{
    /// <param name="knownIdents">Le piste che lo scalo ha davvero: una regola può nominarne una che non
    /// esiste — un refuso, o una pista tolta dopo — ed è un avviso, non un errore, perché la regola resta
    /// salvabile e va corretta da chi sa quale intendeva.</param>
    public static AirportRuleIssues Issues(IReadOnlyList<RuleEdit> rows, IEnumerable<string> knownIdents)
    {
        var errors = new List<AirportTlIssue>();
        var warnings = new List<AirportTlIssue>();
        var note = knownIdents.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var n = i + 1;

            // Una regola che non nomina nessuna pista non sceglie niente: è l'unico caso che blocca il salvataggio.
            if (r.Dep.Count == 0 && r.Arr.Count == 0)
                errors.Add(new AirportTlIssue("Ape_IssueRuleNoRw", new object[] { n }));

            foreach (var id in r.Dep.Concat(r.Arr).Where(id => !note.Contains(id)).Distinct())
                warnings.Add(new AirportTlIssue("Ape_IssueRuleUnknownRw", new object[] { n, id }));

            // Mezza finestra oraria non è una finestra: «dalle 06:00» senza un «fino a» non si sa dove finisce.
            if (r.TimeFrom is not null ^ r.TimeTo is not null)
                warnings.Add(new AirportTlIssue("Ape_IssueRuleTimeWin", new object[] { n }));
        }
        return new AirportRuleIssues(errors, warnings);
    }
}

/// <summary>
/// La conversione di una regola dalla forma d'editor a quella del dominio. ⚠️ Stava scritta DUE volte nella
/// pagina, campo per campo — una per il pannello di prova (<c>RunwayRuleEval</c>) e una per il salvataggio
/// (<c>RunwayRuleRow</c>) — e le due copie potevano divergere: la prova avrebbe detto una cosa e il salvato
/// un'altra, che su una regola di scelta pista è il difetto peggiore possibile.
/// </summary>
public static class AirportRuleMapping
{
    public static string JoinCsv(HashSet<string> set) => string.Join(",", set);
    public static int? TimeToMin(TimeOnly? t) => t is TimeOnly v ? v.Hour * 60 + v.Minute : null;

    /// <summary>Finestra stagionale ricorrente: in DB è MMDD. Vale solo se ci sono ENTRAMBI, mese e giorno.</summary>
    public static int? CombineMd(int? month, int? day) =>
        month is int m && day is int d ? m * 100 + d : null;

    public static RunwaySurface Surface(string? s) => s switch
    {
        "dry" => RunwaySurface.Dry,
        "wet" => RunwaySurface.Wet,
        _ => RunwaySurface.Any,
    };

    public static DateParity Parity(string? s) => s switch
    {
        "even" => DateParity.Even,
        "odd" => DateParity.Odd,
        _ => DateParity.Any,
    };

    /// <summary>La regola come la valuta il dominio: è ciò su cui gira il pannello di prova.</summary>
    public static RunwayRuleEval ToEval(RuleEdit r) => new(
        JoinCsv(r.Dep), JoinCsv(r.Arr), r.Name, r.Note, r.MaxTail, r.MaxCross, Surface(r.Surface),
        TimeToMin(r.TimeFrom), TimeToMin(r.TimeTo), r.DaysMask == 0 ? null : r.DaysMask, Parity(r.Parity),
        CombineMd(r.DateFromMonth, r.DateFromDay), CombineMd(r.DateToMonth, r.DateToDay));

    /// <summary>La regola come si salva. Stessi campi di <see cref="ToEval"/>, in un altro record.</summary>
    public static RunwayRuleRow ToRow(RuleEdit r) => new(
        0, JoinCsv(r.Dep), JoinCsv(r.Arr), r.Name, r.MaxTail, r.MaxCross, Surface(r.Surface), r.Note,
        TimeToMin(r.TimeFrom), TimeToMin(r.TimeTo), r.DaysMask == 0 ? null : r.DaysMask, Parity(r.Parity),
        CombineMd(r.DateFromMonth, r.DateFromDay), CombineMd(r.DateToMonth, r.DateToDay));
}

/// <summary>Che cosa si sta guardando nell'elenco delle SID importate: il testo cercato, la pista scelta fra
/// le chip, e se si vogliono solo quelle da rivedere.</summary>
public sealed record SidFiltro(string? Cerca = null, string? Pista = null, bool SoloDaRivedere = false);

/// <summary>
/// Filtri e regole delle SID. Cuore deterministico: è ciò che decide quali procedure un editore VEDE, e una
/// riga che sparisce da un filtro sbagliato è una riga che nessuno corregge.
/// </summary>
public static class AirportSidRules
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Le SID importate che passano il filtro.
    /// </summary>
    /// <param name="ignoraPista">Vero quando si stanno CONTANDO le piste da offrire nelle chip: se si
    /// applicasse anche il filtro pista, l'elenco delle chip conterrebbe solo quella già scelta e non si
    /// potrebbe più cambiarla. È il motivo per cui questo parametro esiste.</param>
    public static IEnumerable<ImportedSidEdit> Importate(
        IEnumerable<ImportedSidEdit> tutte, SidFiltro filtro, bool ignoraPista = false)
    {
        var q = tutte;
        if (filtro.SoloDaRivedere) q = q.Where(e => e.NeedsReview);

        var s = (filtro.Cerca ?? "").Trim();
        if (s.Length > 0)
            q = q.Where(e => e.Fix.Contains(s, OIC) || e.Name.Contains(s, OIC) || (e.Runway?.Contains(s, OIC) ?? false));

        if (!ignoraPista && filtro.Pista is { Length: > 0 } rw)
            q = q.Where(e => string.Equals(e.Runway, rw, OIC));

        return q;
    }

    /// <summary>Le piste presenti fra le SID importate che passano gli ALTRI filtri, col loro conteggio.</summary>
    public static IReadOnlyList<(string Ident, int Count)> PisteImportate(
        IEnumerable<ImportedSidEdit> tutte, SidFiltro filtro) =>
        Importate(tutte, filtro, ignoraPista: true)
            .Where(e => !string.IsNullOrWhiteSpace(e.Runway))
            .GroupBy(e => e.Runway!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count()))
            .ToList();

    /// <summary>Le SID scritte a mano che corrispondono al testo cercato.</summary>
    public static IEnumerable<SidEdit> Manuali(IEnumerable<SidEdit> tutte, string? cerca)
    {
        var q = (cerca ?? "").Trim();
        if (q.Length == 0) return tutte;
        return tutte.Where(s =>
            (s.Fix?.Contains(q, OIC) ?? false)
            || (s.Name?.Contains(q, OIC) ?? false)
            || (s.Runway?.Contains(q, OIC) ?? false));
    }

    /// <summary>Vero se il token compare nella lista separata da virgole (le transizioni, le categorie).</summary>
    public static bool HasTok(string? csv, string tok) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(tok, StringComparer.OrdinalIgnoreCase);

    /// <summary>Problemi delle SID scritte a mano: FIX o nome mancante, pista che non c'è, righe doppie.</summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<SidEdit> rows, IEnumerable<string> knownIdents)
    {
        var w = new List<AirportTlIssue>();
        var note = knownIdents.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var viste = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var s = rows[i];
            var n = i + 1;
            var fix = (s.Fix ?? "").Trim();
            var nome = (s.Name ?? "").Trim();
            var rw = (s.Runway ?? "").Trim();

            if (fix.Length == 0 || nome.Length == 0)
                w.Add(new AirportTlIssue("Ape_IssueSidMissing", new object[] { n }));

            if (rw.Length > 0 && !note.Contains(rw))
                w.Add(new AirportTlIssue("Ape_IssueSidUnknownRw", new object[] { n, rw }));

            // ⚠️ La chiave del duplicato è FIX + nome + PISTA: la stessa procedura su due piste diverse è
            // legittima, ed è anzi il caso normale.
            if (fix.Length > 0 && nome.Length > 0 && !viste.Add($"{fix}|{nome}|{rw}"))
                w.Add(new AirportTlIssue("Ape_IssueSidDup", new object[] { n, fix, nome, rw }));
        }
        return w;
    }
}
