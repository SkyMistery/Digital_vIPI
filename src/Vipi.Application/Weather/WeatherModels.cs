namespace Vipi.Application.Weather;

/// <summary>Vento decodificato. <see cref="Variable"/>=VRB (direzione non significativa). Velocità/raffica in kt.</summary>
public sealed record ParsedWind(int? DirectionDeg, bool Variable, int SpeedKt, int? GustKt, bool Calm)
{
    /// <summary>Etichetta leggibile, es. "160° / 12 kt", "VRB / 3 kt", "Calmo".</summary>
    public string Label =>
        Calm ? "Calmo"
        : Variable ? $"VRB / {SpeedKt} kt"
        : $"{DirectionDeg:000}° / {SpeedKt} kt";
}

/// <summary>Strato di nubi: copertura (FEW/SCT/BKN/OVC) + base in piedi + eventuale tipo (CB/TCU).</summary>
public sealed record CloudLayer(string Cover, int BaseFt, string? Type)
{
    public string Label => $"{Cover} {BaseFt}{(Type is null ? "" : " " + Type)}";
}

/// <summary>METAR decodificato. Campi null = non presenti/non riconosciuti (token grezzo resta nel raw).</summary>
public sealed record ParsedMetar(
    string Raw,
    string? Station,
    string? TimeRaw,
    ParsedWind? Wind,
    string? Visibility,
    IReadOnlyList<CloudLayer> Clouds,
    string? Weather,
    int? QnhHpa,
    int? TempC,
    int? DewpointC,
    string? Trend,
    bool HasRain,
    bool HasSnow)
{
    public string CloudsLabel => Clouds.Count == 0 ? "—" : string.Join(" · ", Clouds.Select(c => c.Label));
}

/// <summary>Tipo di gruppo di variazione TAF.</summary>
public enum TafChangeKind { Base, Becmg, Tempo, From, Prob }

/// <summary>Segmento TAF (Base o variazione). <see cref="PeriodRaw"/> = periodo grezzo (es. "1918/1920").</summary>
public sealed record TafSegment(
    TafChangeKind Kind,
    string? PeriodRaw,
    int? Probability,
    ParsedWind? Wind,
    string? Visibility,
    IReadOnlyList<CloudLayer> Clouds,
    string? Weather,
    string Raw);

/// <summary>TAF decodificato: stazione, periodo di validità grezzo, segmenti (Base + variazioni).</summary>
public sealed record ParsedTaf(
    string Raw,
    string? Station,
    string? ValidityRaw,
    IReadOnlyList<TafSegment> Segments);

/// <summary>
/// Formatta i periodi TAF grezzi (ddHH) in forma leggibile "DD-MM HH:MM UTC" per la parte SPIEGATA (non la stringa
/// grezza). Il mese non è nel TAF → dedotto dalla data di riferimento (ora corrente) gestendo il cambio mese.
/// </summary>
public static class TafPeriod
{
    /// <summary>"2112/2212" → "21-07 12:00 → 22-07 12:00 UTC"; "2112" (FM/PROB) → "21-07 12:00 UTC". Null/illeggibile ⇒ grezzo.</summary>
    public static string? Format(string? period, DateTime referenceUtc)
    {
        if (string.IsNullOrWhiteSpace(period)) return period;
        var slash = period.IndexOf('/');
        if (slash > 0)
        {
            var a = FormatPoint(period[..slash], referenceUtc);
            var b = FormatPoint(period[(slash + 1)..], referenceUtc);
            return a is null || b is null ? period : $"{a} → {b} UTC";
        }
        var p = FormatPoint(period, referenceUtc);
        return p is null ? period : $"{p} UTC";
    }

    // ddHH → "DD-MM HH:MM". Ora TAF ammette 24 = fine giornata (→ 00:00 del giorno dopo).
    private static string? FormatPoint(string ddhh, DateTime referenceUtc)
    {
        if (ddhh.Length != 4 || !int.TryParse(ddhh[..2], out var day) || !int.TryParse(ddhh[2..], out var hour)
            || day is < 1 or > 31 || hour is < 0 or > 24)
            return null;
        if (ResolveDate(day, referenceUtc) is not DateTime date) return null;
        var when = date.AddHours(hour);   // hour 24 → 00:00 giorno successivo
        return $"{when:dd-MM} {when:HH:mm}";
    }

    // Sceglie l'anno/mese in cui cade il giorno-del-mese indicato, più vicino alla data di riferimento (gestisce fine mese).
    private static DateTime? ResolveDate(int day, DateTime reference)
    {
        var refDate = reference.Date;
        foreach (var off in new[] { 0, 1, -1 })
        {
            var m = refDate.AddMonths(off);
            if (day <= DateTime.DaysInMonth(m.Year, m.Month))
            {
                var cand = new DateTime(m.Year, m.Month, day, 0, 0, 0, DateTimeKind.Utc);
                if (Math.Abs((cand - refDate).TotalDays) <= 20) return cand;
            }
        }
        var clamped = Math.Min(day, DateTime.DaysInMonth(reference.Year, reference.Month));
        return new DateTime(reference.Year, reference.Month, clamped, 0, 0, 0, DateTimeKind.Utc);
    }
}
