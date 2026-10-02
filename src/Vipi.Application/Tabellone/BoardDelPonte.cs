using System.Globalization;
using System.Text.Json;

namespace Vipi.Application.Tabellone;

/// <summary>
/// La voce per il tabellone nel documento del ponte RFO (regola SYN-TABELLONE del Gate Manager, dal 2 ottobre 2026):
///
/// <code>
/// "board":   { "arr:AZA1": "509", "dep:AZA2": "509" },
/// "boardAt": "2026-10-03T12:00:02Z"
/// </code>
///
/// <para>Il ponte non apre mai il documento (lo salva così com'è): lo apre questo, in sola lettura, e solo queste due
/// voci. Un documento senza <c>board</c> (Gate Manager di prima, o nessuna postazione che dà la clearance) non è un
/// errore: non c'è lo stand del Gate Manager, e basta.</para>
/// </summary>
public static class BoardDelPonte
{
    public sealed record Board(IReadOnlyDictionary<string, string> Stand, DateTimeOffset? Al);

    public static Board? Leggi(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var stand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("board", out var b) && b.ValueKind == JsonValueKind.Object)
                foreach (var voce in b.EnumerateObject())
                    if (voce.Value.ValueKind == JsonValueKind.String && voce.Value.GetString() is { Length: > 0 } s)
                        stand[voce.Name.Trim()] = s.Trim();

            DateTimeOffset? al = null;
            if (doc.RootElement.TryGetProperty("boardAt", out var a) && a.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(a.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t))
                al = t;

            return new Board(stand, al);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
