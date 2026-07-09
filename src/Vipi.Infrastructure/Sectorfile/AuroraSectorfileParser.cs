using System.Globalization;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Parser puro (nessun I/O) del sectorfile Aurora della divisione IT: navaid (itfix/itvor) e SID per-aeroporto.
/// Formato SID (semicolon): <c>ICAO;pista[:pista…];CODICE;labelLat;labelLon;type;fixTransition;RNAV;</c>.
/// Il CODICE è <c>SID</c> o <c>SID-TRANS</c>; il fix di partenza è il prefisso troncato del codice (ultime 2
/// char = designatore cifra+lettera) da completare via navaid o alias.
/// </summary>
public static class AuroraSectorfileParser
{
    /// <summary>Indice unico NOME→(lat,lon) unendo itfix (coord col 1-2) e itvor (coord col 2-3). I fix vincono sui VOR.</summary>
    public static IReadOnlyDictionary<string, (double Lat, double Lon)> ParseNavaids(string? fixText, string? vorText)
    {
        var map = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        // VOR prima, così i fix (aggiunti dopo) hanno precedenza sui nomi omonimi.
        foreach (var (name, lat, lon) in ParseNavaidLines(vorText, latCol: 2, lonCol: 3)) map[name] = (lat, lon);
        foreach (var (name, lat, lon) in ParseNavaidLines(fixText, latCol: 1, lonCol: 2)) map[name] = (lat, lon);
        return map;
    }

    private static IEnumerable<(string Name, double Lat, double Lon)> ParseNavaidLines(string? text, int latCol, int lonCol)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var c = line.Split(';');
            if (c.Length <= lonCol) continue;
            var name = c[0].Trim();
            if (name.Length == 0) continue;
            yield return (name, ParseCoord(c[latCol]), ParseCoord(c[lonCol]));
        }
    }

    /// <summary>Coord Aurora "N046.02.37.000" / "E011.07.48.000" → gradi decimali. 0 se non parsabile.</summary>
    private static double ParseCoord(string? s)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length < 2) return 0;
        var hemi = char.ToUpperInvariant(s[0]);
        var body = s[1..].Split('.');
        if (body.Length < 3) return 0;
        if (!int.TryParse(body[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deg)) return 0;
        int.TryParse(body[1], out var min);
        double sec = 0;
        if (body.Length >= 4) double.TryParse(body[2] + "." + body[3], NumberStyles.Float, CultureInfo.InvariantCulture, out sec);
        else double.TryParse(body[2], NumberStyles.Float, CultureInfo.InvariantCulture, out sec);
        var val = deg + min / 60.0 + sec / 3600.0;
        return hemi is 'S' or 'W' ? -val : val;
    }

    /// <summary>Parsa un file <c>&lt;icao&gt;.sid</c> in una lista di <see cref="SourceSid"/> risolti.</summary>
    public static IReadOnlyList<SourceSid> ParseSids(
        string icao, string? sidFile,
        IReadOnlyDictionary<string, (double Lat, double Lon)> navIndex,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        var result = new List<SourceSid>();
        if (string.IsNullOrEmpty(sidFile)) return result;
        icao = icao.Trim().ToUpperInvariant();

        foreach (var raw in sidFile.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var c = line.Split(';');
            if (c.Length < 3) continue;

            var code = c[2].Trim();
            if (code.Length == 0) continue;
            var runwaysField = c[1].Trim();
            var transition = c.Length > 6 ? Blank(c[6]) : null;
            var rnav = c.Length > 7 && c[7].Trim() == "1";

            // Codice = SID o SID-TRANS: il fix di partenza si estrae dalla sola parte SID.
            var sidPart = code.Split('-')[0].Trim();
            var (prefix, letter) = SplitDesignator(sidPart);
            var (fix, needsReview) = ResolveFix(prefix, navIndex, aliasMap);

            var runways = runwaysField.Length == 0
                ? new List<string?> { null }
                : runwaysField.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(r => (string?)r).ToList();
            if (runways.Count == 0) runways.Add(null);

            foreach (var rwy in runways)
            {
                var stableKey = string.Join('|', icao, fix.ToUpperInvariant(), letter.ToUpperInvariant(),
                    (transition ?? "").ToUpperInvariant(), (rwy ?? "").ToUpperInvariant());
                result.Add(new SourceSid(
                    Icao: icao, Runway: rwy, Fix: fix, Name: code, Transition: transition,
                    Type: rnav ? "RNAV" : "CONV", StableKey: stableKey, NeedsFixReview: needsReview));
            }
        }
        return result;
    }

    // Designatore = ultime 2 char (cifra+lettera); il resto è il prefisso fix troncato. La lettera è l'ultimo char.
    private static (string Prefix, string Letter) SplitDesignator(string sidCode)
    {
        if (sidCode.Length <= 2) return (sidCode, sidCode.Length > 0 ? sidCode[^1..] : "");
        return (sidCode[..^2], sidCode[^1..]);
    }

    // Risoluzione: match esatto → alias autoritativo → UNICO nome che inizia col prefisso → altrimenti (ambiguo o
    // nessuno) grezzo + NeedsFixReview. L'ambiguità (più candidati) NON si indovina: va risolta con un alias.
    private static (string Fix, bool NeedsReview) ResolveFix(
        string prefix,
        IReadOnlyDictionary<string, (double, double)> navIndex,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        if (prefix.Length == 0) return (prefix, true);

        // (1) match esatto (il prefisso È già un fix/VOR, es. OST).
        foreach (var name in navIndex.Keys)
            if (string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase)) return (name, false);

        // (2) alias autoritativo (scavalca l'ambiguità).
        if (aliasMap.TryGetValue(prefix, out var aliased) && !string.IsNullOrWhiteSpace(aliased))
            return (aliased, false);

        // (3) UNICO nome che inizia col prefisso (il fix reale è più lungo del troncato). Se più di uno → ambiguo.
        string? only = null;
        var multiple = false;
        foreach (var name in navIndex.Keys)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (only is null) only = name;
                else { multiple = true; break; }
            }
        if (only is not null && !multiple) return (only, false);

        // (4) ambiguo o nessun match → da verificare a mano.
        return (prefix, true);
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
