using System.Globalization;

namespace Vipi.AuroraBridge.Core;

/// <summary>Flight Plan Record di Aurora (risposta a <c>#FP</c>), campi 1..15.
/// ⚠️ L'ordine reale dei campi 7-8 è invertito rispetto alla wiki: arriva prima la regola di volo (I/V/Y/Z)
/// e poi il tipo di volo (S/N/G/M/X) — verificato in F0 su traffico reale.</summary>
public sealed record FlightPlanRecord(
    string? Departure,
    string? Arrival,
    string? Alternate,
    string? EstimatedDepartureTime,
    string? AircraftType,
    string? WakeTurbulence,
    string? FlightRules,
    string? FlightType,
    string? Equipment,
    string? CruisingAltitudeRaw,
    string? CruisingSpeedRaw,
    string? Endurance,
    string? EstimatedFlightTime,
    string? Route,
    string? Remarks)
{
    /// <summary>Livello di crociera in FL quando il formato ICAO lo esprime come livello (<c>F330</c> → 330).
    /// Le quote in piedi (<c>A050</c>) e i formati metrici (<c>S1130</c>, <c>M0840</c>) NON sono FL: null.</summary>
    public int? CruiseFlightLevel =>
        CruisingAltitudeRaw is { Length: > 1 } raw && (raw[0] is 'F' or 'f') &&
        int.TryParse(raw.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fl)
            ? fl
            : null;
}

/// <summary>Traffic Position Record di Aurora (risposta a <c>#TRPOS</c>), campi 1..21. Ordine confermato in F0.</summary>
public sealed record TrafficPositionRecord(
    int? Heading,
    int? Track,
    int? AltitudeFt,
    int? SpeedKt,
    double? Latitude,
    double? Longitude,
    string? SquawkSet,
    string? SquawkLabel,
    string? WaypointLabel,
    string? AltitudeLabel,
    string? SpeedLabel,
    string? AssumedStation,
    string? NextStation,
    bool OnGround,
    bool IsSelected,
    bool WasSelected,
    string? CurrentGate,
    string? Voice,
    string? TransferFlightLevel,
    int? VerticalSpeedFpm,
    string? AssignedGate)
{
    /// <summary>Vero se il traffico è assunto dalla postazione indicata: senza assunzione Aurora RIFIUTA la
    /// scrittura dell'etichetta (<c>Traffic not assumed.</c>, scoperto in F0).</summary>
    public bool IsAssumedBy(string? callsign) =>
        !string.IsNullOrWhiteSpace(callsign) && !string.IsNullOrWhiteSpace(AssumedStation) &&
        AssumedStation!.Trim().Equals(callsign!.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Piste in uso di un aeroporto controllato (da <c>#CTRLRWY</c>).</summary>
public sealed record RunwayConfiguration(string Icao, IReadOnlyList<string> Departure, IReadOnlyList<string> Arrival);

/// <summary>
/// Traduzione dei record ASCII di Aurora in oggetti. Puro: nessun IO, così i formati reali osservati in F0
/// restano coperti da test senza bisogno di Aurora accesa.
/// </summary>
public static class AuroraRecords
{
    /// <summary>Campi di una risposta, già privati del prefisso comando. Un record vuoto dà lista vuota.</summary>
    public static IReadOnlyList<string> Fields(string payload) =>
        string.IsNullOrEmpty(payload) ? Array.Empty<string>() : payload.Split(';');

    public static FlightPlanRecord? ParseFlightPlan(IReadOnlyList<string> fields)
    {
        // fields[0] = callsign, poi i 15 campi del record.
        if (fields.Count < 2) return null;
        string? At(int i) => Empty(fields, i + 1);

        return new FlightPlanRecord(
            At(0), At(1), At(2), At(3), At(4), At(5), At(6), At(7),
            At(8), At(9), At(10), At(11), At(12), At(13), At(14));
    }

    public static TrafficPositionRecord? ParseTrafficPosition(IReadOnlyList<string> fields)
    {
        if (fields.Count < 2) return null;
        string? At(int i) => Empty(fields, i + 1);

        return new TrafficPositionRecord(
            Int(At(0)), Int(At(1)), Int(At(2)), Int(At(3)),
            Double(At(4)), Double(At(5)),
            At(6), At(7), At(8), At(9), At(10), At(11), At(12),
            Flag(At(13)), Flag(At(14)), Flag(At(15)),
            At(16), At(17), At(18), Int(At(19)), At(20));
    }

    /// <summary>«ICAO;dep;arr;ICAO;dep;arr;…», con più piste separate da «:» e campi vuoti se non configurate.</summary>
    public static IReadOnlyList<RunwayConfiguration> ParseControlledRunways(IReadOnlyList<string> fields)
    {
        var result = new List<RunwayConfiguration>();
        for (var i = 0; i + 2 < fields.Count; i += 3)
        {
            var icao = Empty(fields, i);
            if (string.IsNullOrWhiteSpace(icao)) continue;
            result.Add(new RunwayConfiguration(icao!.Trim().ToUpperInvariant(),
                Runways(Empty(fields, i + 1)), Runways(Empty(fields, i + 2))));
        }
        return result;
    }

    /// <summary>«FIX:ETO;FIX:ETO;…». L'ETO è <c>HHMM</c>, oppure «-» per i punti già passati (<c>#TRPATHA</c>).</summary>
    public static IReadOnlyList<(string Fix, string? Eto)> ParseTrafficPath(IReadOnlyList<string> fields)
    {
        var result = new List<(string, string?)>();
        // fields[0] = callsign.
        for (var i = 1; i < fields.Count; i++)
        {
            var raw = fields[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parts = raw.Split(':');
            var fix = parts[0].Trim();
            if (fix.Length == 0) continue;

            var eto = parts.Length > 1 ? parts[1].Trim() : null;
            result.Add((fix, string.IsNullOrEmpty(eto) || eto == "-" ? null : eto));
        }
        return result;
    }

    /// <summary>Elenco di callsign separati da «;» (risposte <c>#TR</c>, <c>#CTRL</c>), senza vuoti.</summary>
    public static IReadOnlyList<string> ParseList(IReadOnlyList<string> fields) =>
        fields.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();

    private static IReadOnlyList<string> Runways(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw!.Split(':', StringSplitOptions.RemoveEmptyEntries)
                  .Select(r => r.Trim().ToUpperInvariant())
                  .Where(r => r.Length > 0)
                  .ToList();

    private static string? Empty(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count && !string.IsNullOrWhiteSpace(fields[index]) ? fields[index].Trim() : null;

    private static int? Int(string? raw) =>
        raw is not null && int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? Double(string? raw) =>
        raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool Flag(string? raw) => raw == "1";
}
