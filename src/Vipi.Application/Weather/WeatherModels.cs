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
