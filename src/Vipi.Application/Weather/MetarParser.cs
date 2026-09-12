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
    /// <summary>Settore di variabilità del vento, token a sé: <c>200V280</c>.</summary>
    [GeneratedRegex(@"^(\d{3})V(\d{3})$")]
    private static partial Regex WindVarRe();
    /// <summary>RVR: <c>R16R/0350U</c>, <c>R07/P2000</c>, <c>R25/M0050D</c>. Il <c>/…V…</c> variabile: si tiene il primo valore.</summary>
    [GeneratedRegex(@"^R(\d{2}[LRC]?)/([PM]?)(\d{3,4})(?:V[PM]?\d{3,4})?(?:FT)?([UDN]?)$")]
    private static partial Regex RvrRe();
    /// <summary>Visibilità verticale in centinaia di piedi: <c>VV002</c>. <c>VV///</c> = c'è, ma non misurata.</summary>
    [GeneratedRegex(@"^VV(\d{3}|///)$")]
    private static partial Regex VertVisRe();

    private static readonly HashSet<string> CloudCovers = new() { "FEW", "SCT", "BKN", "OVC" };
    private static readonly HashSet<string> ChangeTokens = new() { "BECMG", "TEMPO", "NOSIG" };

    public static ParsedMetar ParseMetar(string raw)
    {
        var tokens = Tokenize(raw, out var station, out var timeRaw, isTaf: false);

        ParsedWind? wind = null;
        string? vis = null, trend = null;
        var clouds = new List<CloudLayer>();
        int? qnh = null, temp = null, dew = null, visM = null, vertVis = null;
        var wxParts = new List<WeatherGroup>();
        var rvr = new List<RunwayVisualRange>();
        bool rain = false, snow = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];

            if (wind is null && WindRe().Match(t) is { Success: true } wm) { wind = ParseWind(wm); continue; }
            // Il settore di variabilità è un token SUO, e arriva dopo il vento: si applica al vento già letto.
            if (WindVarRe().Match(t) is { Success: true } vm && wind is not null)
            { wind = wind with { VarFromDeg = int.Parse(vm.Groups[1].Value), VarToDeg = int.Parse(vm.Groups[2].Value) }; continue; }
            if (RvrRe().Match(t) is { Success: true } rm) { rvr.Add(ParseRvr(rm)); continue; }
            if (VertVisRe().Match(t) is { Success: true } vvm)
            { if (vvm.Groups[1].Value != "///") vertVis = int.Parse(vvm.Groups[1].Value) * 100; continue; }
            // CAVOK e 9999 sono il FONDO SCALA del bollettino (10 km), non una misura: chi confronta con una
            // soglia deve poterli trattare come «sopra a tutto» senza sapere quale dei due era scritto.
            if (t is "CAVOK") { vis ??= ">10 km"; visM ??= 10000; continue; }
            if (vis is null && VisMetersRe().IsMatch(t))
            { vis = FormatVisMeters(t); var m4 = int.Parse(t, CultureInfo.InvariantCulture); visM = m4 >= 9999 ? 10000 : m4; continue; }

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

        return new ParsedMetar(raw.Trim(), station, timeRaw, wind, vis, clouds, wxParts, qnh, temp, dew, trend, rain, snow,
            visM, rvr, vertVis);
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
        string? vis = null;
        var clouds = new List<CloudLayer>();
        var wxParts = new List<WeatherGroup>();

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
        return new TafSegment(kind, period, prob, wind, vis, clouds, wxParts, string.Join(' ', tokens));
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

    private static RunwayVisualRange ParseRvr(Match m)
    {
        var modificatore = m.Groups[2].Value switch { "P" => RvrModifier.Above, "M" => RvrModifier.Below, _ => RvrModifier.Exact };
        var tendenza = m.Groups[4].Value switch { "U" => RvrTendency.Up, "D" => RvrTendency.Down, "N" => RvrTendency.Steady, _ => RvrTendency.None };
        return new RunwayVisualRange(m.Groups[1].Value, int.Parse(m.Groups[3].Value), modificatore, tendenza);
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

    /// <summary>
    /// Decodifica un gruppo di tempo presente (RA/SHRA/TS/BR…) nei suoi <b>codici</b>. Ritorna null se non è
    /// meteo significativo.
    ///
    /// <para>⚠️ <b>Qui non si traduce</b>, e fino al 12 settembre 2026 si traduceva: la tabella dei codici
    /// teneva le parole in italiano («pioggia», «foschia») e la pagina le mostrava tali e quali anche in
    /// inglese. Questo strato non può saperlo: la lingua di chi legge sta nella richiesta, e quella di un
    /// documento <b>bloccato</b> sta nel documento. Le parole stanno nei <c>.resx</c>, chiavi <c>Wx_RA</c>…,
    /// e le mette <c>Vipi.Ui.Shared.WxText</c>.</para>
    /// </summary>
    private static WeatherGroup? DecodeWeather(string t)
    {
        var s = t;
        var intensity = WxIntensity.Moderate;
        if (s.StartsWith('-')) { intensity = WxIntensity.Light; s = s[1..]; }
        else if (s.StartsWith('+')) { intensity = WxIntensity.Heavy; s = s[1..]; }
        else if (s.StartsWith("VC")) { intensity = WxIntensity.Vicinity; s = s[2..]; }

        if (s.Length is 0 or > 6 || s.Length % 2 != 0) return null;

        var codes = new List<string>();
        for (var k = 0; k + 2 <= s.Length; k += 2)
        {
            var code = s.Substring(k, 2);
            if (!WxCodes.Contains(code)) return null;
            codes.Add(code);
        }
        return codes.Count == 0 ? null : new WeatherGroup(t, intensity, codes);
    }

    /// <summary>
    /// I codici di tempo presente riconosciuti. È anche il <b>filtro</b>: un token che contiene un codice
    /// sconosciuto non è meteo e resta fuori (torna nel raw, che si mostra sempre per intero).
    /// </summary>
    private static readonly HashSet<string> WxCodes = new(StringComparer.Ordinal)
    {
        "RA", "SN", "DZ", "GR", "GS", "SG", "PL", "IC",
        "SH", "TS", "FZ", "FG", "BR", "HZ", "FU", "DU", "SA",
        "MI", "BC", "PR", "DR", "BL", "SQ", "FC", "PO",
        "VA", "SS", "DS", "UP",
    };
}
