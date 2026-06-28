using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.Application.Weather;

/// <summary>
/// Decoder METAR/TAF best-effort (formato ICAO/IVAO). Non valida: estrae ciò che riconosce e ignora il resto.
/// Pensato per la vista vIPI aeroporto (vento per pista suggerita, QNH per transition level, timeline TAF).
/// </summary>
public static partial class MetarParser
{
    [GeneratedRegex(@"^(VRB|\d{3})(\d{2,3})(?:G(\d{2,3}))?(KT|MPS)$")]
    private static partial Regex WindRe();
    [GeneratedRegex(@"^([A-Z]{2,3})(\d{3})(CB|TCU)?$")]
    private static partial Regex CloudRe();
    [GeneratedRegex(@"^M?(\d{2})/M?(\d{2})$")]
    private static partial Regex TempRe();
    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex VisMetersRe();
    [GeneratedRegex(@"^\d{4}/\d{4}$")]
    private static partial Regex PeriodRe();
    [GeneratedRegex(@"^\d{6}Z$")]
    private static partial Regex TimeRe();

    private static readonly HashSet<string> CloudCovers = new() { "FEW", "SCT", "BKN", "OVC" };
    private static readonly HashSet<string> ChangeTokens = new() { "BECMG", "TEMPO", "NOSIG" };

    public static ParsedMetar ParseMetar(string raw)
    {
        var tokens = Tokenize(raw, out var station, out var timeRaw, isTaf: false);

        ParsedWind? wind = null;
        string? vis = null, weather = null, trend = null;
        var clouds = new List<CloudLayer>();
        int? qnh = null, temp = null, dew = null;
        var wxParts = new List<string>();
        bool rain = false, snow = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];

            if (wind is null && WindRe().Match(t) is { Success: true } wm) { wind = ParseWind(wm); continue; }
            if (t is "CAVOK") { vis ??= ">10 km"; continue; }
            if (vis is null && VisMetersRe().IsMatch(t)) { vis = FormatVisMeters(t); continue; }

            if (CloudRe().Match(t) is { Success: true } cm && CloudCovers.Contains(cm.Groups[1].Value))
            { clouds.Add(ParseCloud(cm)); continue; }
            if (t is "NSC" or "NCD" or "SKC" or "CLR") continue;

            if (t.StartsWith('Q') && t.Length == 5 && int.TryParse(t.AsSpan(1), out var q)) { qnh = q; continue; }
            if (t.StartsWith('A') && t.Length == 5 && int.TryParse(t.AsSpan(1), out var inHg))
            { qnh ??= (int)Math.Round(inHg / 100.0 * 33.8639); continue; }

            if (temp is null && TempRe().Match(t) is { Success: true } tm)
            { temp = SignedTemp(t, tm.Groups[1].Value); dew = SignedTemp(t[(t.IndexOf('/') + 1)..], tm.Groups[2].Value); continue; }

            if (t is "NOSIG") { trend = "NOSIG"; continue; }

            var wx = DecodeWeather(t);
            if (wx is not null) { wxParts.Add(wx); ClassifyPrecip(t, ref rain, ref snow); }
        }

        if (wxParts.Count > 0) weather = string.Join(", ", wxParts);
        return new ParsedMetar(raw.Trim(), station, timeRaw, wind, vis, clouds, weather, qnh, temp, dew, trend, rain, snow);
    }

    public static ParsedTaf ParseTaf(string raw)
    {
        var tokens = Tokenize(raw, out var station, out var _, isTaf: true);

        // Validità = primo token periodo (es. 1912/2018), prima di qualunque BECMG/TEMPO/FM.
        string? validity = null;
        var segments = new List<TafSegment>();
        var current = new List<string>();
        TafChangeKind kind = TafChangeKind.Base;
        string? period = null;
        int? prob = null;

        void Flush()
        {
            if (current.Count == 0 && kind == TafChangeKind.Base) return;
            segments.Add(BuildSegment(kind, period, prob, current));
            current = new List<string>();
            period = null; prob = null;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];

            if (validity is null && PeriodRe().IsMatch(t)) { validity = t; continue; }

            if (t is "BECMG" or "TEMPO")
            {
                Flush();
                kind = t == "BECMG" ? TafChangeKind.Becmg : TafChangeKind.Tempo;
                if (i + 1 < tokens.Count && PeriodRe().IsMatch(tokens[i + 1])) { period = tokens[++i]; }
                continue;
            }
            if (t.StartsWith("FM") && t.Length == 8)
            {
                Flush();
                kind = TafChangeKind.From; period = t[2..];
                continue;
            }
            if (t.StartsWith("PROB") && t.Length == 6)
            {
                Flush();
                kind = TafChangeKind.Prob; prob = int.Parse(t[4..]);
                if (i + 1 < tokens.Count && PeriodRe().IsMatch(tokens[i + 1])) period = tokens[++i];
                continue;
            }
            current.Add(t);
        }
        Flush();

        return new ParsedTaf(raw.Trim(), station, validity, segments);
    }

    // ---- helpers ----

    private static TafSegment BuildSegment(TafChangeKind kind, string? period, int? prob, List<string> tokens)
    {
        ParsedWind? wind = null;
        string? vis = null, weather = null;
        var clouds = new List<CloudLayer>();
        var wxParts = new List<string>();

        foreach (var t in tokens)
        {
            if (wind is null && WindRe().Match(t) is { Success: true } wm) { wind = ParseWind(wm); continue; }
            if (t is "CAVOK") { vis ??= ">10 km"; continue; }
            if (vis is null && VisMetersRe().IsMatch(t)) { vis = FormatVisMeters(t); continue; }
            if (CloudRe().Match(t) is { Success: true } cm && CloudCovers.Contains(cm.Groups[1].Value))
            { clouds.Add(ParseCloud(cm)); continue; }
            if (t is "NSC" or "NCD" or "SKC") continue;
            var wx = DecodeWeather(t);
            if (wx is not null) wxParts.Add(wx);
        }
        if (wxParts.Count > 0) weather = string.Join(", ", wxParts);
        return new TafSegment(kind, period, prob, wind, vis, clouds, weather, string.Join(' ', tokens));
    }

    private static List<string> Tokenize(string raw, out string? station, out string? timeRaw, bool isTaf)
    {
        station = null; timeRaw = null;
        var parts = (raw ?? "").Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        var i = 0;
        if (i < parts.Count && parts[i] is "METAR" or "TAF" or "SPECI") i++;
        if (isTaf && i < parts.Count && (parts[i] is "AMD" or "COR")) i++;
        if (i < parts.Count && Regex.IsMatch(parts[i], "^[A-Z]{4}$") && parts[i] is not "CAVOK") { station = parts[i]; i++; }
        if (i < parts.Count && TimeRe().IsMatch(parts[i])) { timeRaw = parts[i]; i++; }
        return parts.Skip(i).TakeWhile(p => p != "=").Select(p => p.TrimEnd('=')).Where(p => p.Length > 0).ToList();
    }

    private static ParsedWind ParseWind(Match m)
    {
        var dir = m.Groups[1].Value;
        var variable = dir == "VRB";
        var speed = int.Parse(m.Groups[2].Value);
        int? gust = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : null;
        var mps = m.Groups[4].Value == "MPS";
        if (mps) { speed = (int)Math.Round(speed * 1.94384); if (gust is int g) gust = (int)Math.Round(g * 1.94384); }
        var calm = !variable && dir == "000" && speed == 0;
        return new ParsedWind(variable ? null : int.Parse(dir), variable, speed, gust, calm);
    }

    private static CloudLayer ParseCloud(Match m) =>
        new(m.Groups[1].Value, int.Parse(m.Groups[2].Value) * 100, m.Groups[3].Success ? m.Groups[3].Value : null);

    private static int SignedTemp(string token, string digits) =>
        token.StartsWith('M') ? -int.Parse(digits) : int.Parse(digits);

    private static string FormatVisMeters(string t) =>
        t == "9999" ? ">10 km" : $"{int.Parse(t, CultureInfo.InvariantCulture)} m";

    /// <summary>Segna pioggia/neve dai codici del gruppo di tempo (RA/DZ→pioggia, SN/SG→neve), incluso SH/FZ/TS+code.</summary>
    private static void ClassifyPrecip(string token, ref bool rain, ref bool snow)
    {
        if (token.Contains("RA", StringComparison.Ordinal) || token.Contains("DZ", StringComparison.Ordinal)) rain = true;
        if (token.Contains("SN", StringComparison.Ordinal) || token.Contains("SG", StringComparison.Ordinal)) snow = true;
    }

    /// <summary>Decodifica un gruppo di tempo presente (RA/SHRA/TS/BR…). Ritorna null se non è meteo significativo.</summary>
    private static string? DecodeWeather(string t)
    {
        var s = t;
        var intensity = "";
        if (s.StartsWith('-')) { intensity = "leggera "; s = s[1..]; }
        else if (s.StartsWith('+')) { intensity = "forte "; s = s[1..]; }
        else if (s.StartsWith("VC")) { intensity = "in prossimità "; s = s[2..]; }

        if (s.Length is 0 or > 6 || s.Length % 2 != 0) return null;

        var sb = new List<string>();
        for (var k = 0; k + 2 <= s.Length; k += 2)
        {
            var code = s.Substring(k, 2);
            if (!WxCodes.TryGetValue(code, out var word)) return null;
            sb.Add(word);
        }
        return sb.Count == 0 ? null : intensity + string.Join(" ", sb);
    }

    private static readonly Dictionary<string, string> WxCodes = new()
    {
        ["RA"] = "pioggia", ["SN"] = "neve", ["DZ"] = "pioviggine", ["GR"] = "grandine", ["GS"] = "gragnola",
        ["SG"] = "neve granulosa", ["PL"] = "granuli di ghiaccio", ["IC"] = "aghi di ghiaccio",
        ["SH"] = "rovescio", ["TS"] = "temporale", ["FZ"] = "congelantesi", ["FG"] = "nebbia",
        ["BR"] = "foschia", ["HZ"] = "caligine", ["FU"] = "fumo", ["DU"] = "polvere", ["SA"] = "sabbia",
        ["MI"] = "sottile", ["BC"] = "banchi", ["PR"] = "parziale", ["DR"] = "scaccia-basso",
        ["BL"] = "sollevata", ["SQ"] = "groppo", ["FC"] = "tromba", ["PO"] = "vortici di polvere",
        ["VA"] = "cenere vulcanica", ["SS"] = "tempesta di sabbia", ["DS"] = "tempesta di polvere",
        ["UP"] = "precipitazione sconosciuta",
    };
}
