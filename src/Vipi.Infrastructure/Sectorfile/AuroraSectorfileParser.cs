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
    /// <summary>Insieme dei NOMI navaid (fix + VOR) unendo itfix e itvor. Le coordinate non servono alla completion
    /// dei fix SID (solo i nomi), quindi non vengono parsate.</summary>
    public static IReadOnlySet<string> ParseNavaids(string? fixText, string? vorText)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ParseNavaidNames(vorText)) names.Add(name);
        foreach (var name in ParseNavaidNames(fixText)) names.Add(name);
        return names;
    }

    private static IEnumerable<string> ParseNavaidNames(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var name = line.Split(';', 2)[0].Trim();
            if (name.Length != 0) yield return name;
        }
    }

    /// <summary>Parsa un file <c>&lt;icao&gt;.sid</c> in una lista di <see cref="SourceSid"/> risolti.</summary>
    public static IReadOnlyList<SourceSid> ParseSids(
        string icao, string? sidFile,
        IReadOnlySet<string> navNames,
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
            var (fix, needsReview) = ResolveFix(prefix, navNames, aliasMap);

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
        IReadOnlySet<string> navNames,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        if (prefix.Length == 0) return (prefix, true);

        // (1) match esatto O(1) (il prefisso È già un fix/VOR, es. OST). Set case-insensitive: i nomi Aurora sono
        // maiuscoli come i codici SID, quindi il prefisso porta già la grafia canonica.
        if (navNames.Contains(prefix)) return (prefix, false);

        // (2) alias autoritativo (scavalca l'ambiguità).
        if (aliasMap.TryGetValue(prefix, out var aliased) && !string.IsNullOrWhiteSpace(aliased))
            return (aliased, false);

        // (3) UNICO nome che inizia col prefisso (il fix reale è più lungo del troncato). Se più di uno → ambiguo.
        string? only = null;
        var multiple = false;
        foreach (var name in navNames)
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
