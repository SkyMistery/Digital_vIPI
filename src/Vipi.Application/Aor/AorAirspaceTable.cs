using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Aor;

/// <summary>
/// Le righe della tabella «spazi aerei» sotto l'AoR: i volumi dell'AIP agganciati ai settori del documento, con
/// la classe e la nota scritte a mano (carta <c>docs/feature/2026-09-17-tabella-spazi-aerei-nell-aor.md</c>).
///
/// <para>⚠️ <b>Le righe sono i pezzi che la mappa disegna</b>, presi dalla stessa risposta della porta unica
/// (<see cref="ISectorShapeResolver"/>): solo quelli con <see cref="ShapeSource.Aip"/>. Leggere gli agganci una
/// seconda volta vorrebbe dire due verità sullo stesso settore il giorno che la precedenza cambia.</para>
///
/// <para>⚠️ <b>Solo i settori del documento</b>, non le shape extra (decisione del committente, 17-set-2026): chi
/// chiama passa quei soli callsign.</para>
///
/// PURA: nessun I/O.
/// </summary>
public static class AorAirspaceTable
{
    public static IReadOnlyList<AorAirspaceRow> Build(
        IEnumerable<string> callsigns, IReadOnlyDictionary<string, SectorShape> shapes,
        IReadOnlyDictionary<string, AorAirspaceEdit>? edits)
    {
        var rows = new List<AorAirspaceRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cs in callsigns)
        {
            if (!shapes.TryGetValue(cs, out var shape) || shape.Source != ShapeSource.Aip) continue;
            foreach (var p in shape.Parts)
            {
                // ⚠️ Lo stesso volume sotto due settori di un blocco è UNA riga: la nota è del volume, non del settore.
                if (string.IsNullOrWhiteSpace(p.SourceRef) || !seen.Add(p.SourceRef)) continue;
                AorAirspaceEdit? edit = null;
                edits?.TryGetValue(p.SourceRef, out edit);
                rows.Add(new AorAirspaceRow(
                    p.SourceRef, p.Name ?? NameFromKey(p.SourceRef), p.BaseRaw, p.TopRaw,
                    AorCustomizationCleaner.CleanClass(p.AirspaceClass), AorCustomizationCleaner.CleanClass(edit?.Class),
                    string.IsNullOrWhiteSpace(edit?.Note) ? null : edit!.Note));
            }
        }
        return rows;
    }

    /// <summary>Il nome dalla chiave naturale <c>FAMIGLIA|NOME|BASE|TETTO</c>, se il pezzo non lo porta.</summary>
    private static string NameFromKey(string key)
    {
        var parts = key.Split('|');
        return parts.Length >= 2 ? parts[1] : key;
    }
}
