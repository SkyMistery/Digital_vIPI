using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vipi.Application.Content;
using Vipi.Application.Weather;

namespace Vipi.Ui.Shared;

/// <summary>
/// Formattazione e parsing condivisi dalle viste aeroporto: il documento completo
/// (<c>AeroportoPage</c>) e il pannello rapido (<c>AirportQuickPanel</c>) mostrano gli stessi dati
/// operativi — initial climb, tabella dei livelli di transizione, pista consigliata — e ne
/// tenevano una copia per componente. Logica pura: nessuno stato, nessun IO, testabile.
/// </summary>
public static class AirportViewFormat
{
    private const string Dash = "—";

    // Quota iniziale + eventuale nota: "5000", "5,000 ft", "9000 (to be coordinated with APP)".
    private static readonly Regex ClimbPattern = new(@"^([\d,]+)\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Initial climb reso in piedi se la quota è a/sotto la transition altitude, in livello di volo se sopra
    /// (es. TA 6000: 5000 → «5000 ft», 9000 → «FL90»). Le note testuali sono preservate, e un valore senza
    /// quota numerica (es. «to be coordinated with APP») torna invariato. TA sconosciuta ⇒ sempre in piedi.
    /// </summary>
    public static string InitialClimb(string? raw, int? transitionAltitudeFt)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0 || s == Dash) return Dash;

        var m = ClimbPattern.Match(s);
        if (!m.Success || !int.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var feet))
            return s;

        var note = m.Groups[2].Value.Trim();
        var label = transitionAltitudeFt is int ta && feet > ta
            ? $"FL{(int)Math.Round(feet / 100.0)}"
            : $"{feet} ft";
        return note.Length == 0 ? label : $"{label} {note}";
    }

    /// <summary>
    /// Vero se <paramref name="qnh"/> ricade nell'intervallo testuale della riga TL. Formati riconosciuti:
    /// «1014 – 1030» (range), «≥ 1031» / «&gt;= 1031», «≤ 984» / «&lt;= 984», «&gt; 1031», «&lt; 984».
    /// Riga senza numeri ⇒ nessuna corrispondenza.
    /// </summary>
    public static bool QnhRowMatches(string? range, int qnh)
    {
        var text = range ?? "";
        var nums = Regex.Matches(text, @"\d+")
            .Select(m => int.TryParse(m.Value, out var v) ? v : (int?)null)
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        if (nums.Count == 0) return false;

        if (text.Contains('≥') || text.Contains(">=")) return qnh >= nums[0];
        if (text.Contains('≤') || text.Contains("<=")) return qnh <= nums[0];
        if (text.Contains('>')) return qnh > nums[0];
        if (text.Contains('<')) return qnh < nums[0];
        return nums.Count >= 2 && qnh >= Math.Min(nums[0], nums[1]) && qnh <= Math.Max(nums[0], nums[1]);
    }

    /// <summary>Tabella dei livelli di transizione: intestazioni + righe (intervallo QNH, livello).</summary>
    public sealed record TransitionLevelTable(
        IReadOnlyList<string> Columns,
        IReadOnlyList<(string Range, string Level)> Rows)
    {
        public static readonly TransitionLevelTable Empty =
            new(Array.Empty<string>(), Array.Empty<(string, string)>());
    }

    /// <summary>
    /// Parsa il blocco tabella dei livelli di transizione dal suo JSON. JSON assente o malformato ⇒ tabella
    /// vuota: la sezione TL è informativa e non deve far cadere il render. Le righe con meno di due celle
    /// vengono scartate (serve almeno intervallo + livello).
    /// <para>
    /// Ritorna sia colonne sia righe perché i due consumatori ne usano porzioni diverse: il documento rende
    /// anche le intestazioni, il pannello rapido solo le righe. Prima erano due parser separati, e quello del
    /// pannello ignorava <c>columns</c> — divergenza silenziosa se il formato del blocco cambia.
    /// </para>
    /// </summary>
    public static TransitionLevelTable ParseTransitionLevels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return TransitionLevelTable.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // JSON valido ma non un oggetto (es. un array in radice): TryGetProperty lancerebbe
            // InvalidOperationException, che NON è una JsonException — prima sfuggiva al catch e faceva
            // cadere il render della pagina aeroporto.
            if (root.ValueKind != JsonValueKind.Object) return TransitionLevelTable.Empty;

            var columns = root.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array
                ? cols.EnumerateArray().Select(c => c.GetString() ?? "").ToList()
                : new List<string>();

            var rows = new List<(string Range, string Level)>();
            if (root.TryGetProperty("rows", out var rs) && rs.ValueKind == JsonValueKind.Array)
                foreach (var row in rs.EnumerateArray())
                    if (row.TryGetProperty("cells", out var cells)
                        && cells.ValueKind == JsonValueKind.Array && cells.GetArrayLength() >= 2)
                        rows.Add((cells[0].GetString() ?? "", cells[1].GetString() ?? ""));

            return new TransitionLevelTable(columns, rows);
        }
        catch (JsonException)
        {
            return TransitionLevelTable.Empty;
        }
    }

    /// <summary>Regola pista dal DTO editoriale al modello del motore di valutazione (<see cref="RunwaySuggestion"/>).</summary>
    public static RunwayRuleEval MapRule(RunwayRuleRow r) =>
        new(r.DepRunways, r.ArrRunways, r.Name, r.Note, r.MaxTailwindKt, r.MaxCrosswindKt, r.Surface,
            r.TimeFromLocalMin, r.TimeToLocalMin, r.DaysOfWeekMask, r.DateParity,
            r.DateFromMonthDay, r.DateToMonthDay);
}
