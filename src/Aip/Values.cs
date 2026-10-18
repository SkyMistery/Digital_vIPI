namespace Aip;

/// <summary>
/// Un punto in gradi decimali (WGS-84, nord ed est positivi). L'AIP lo stampa in gradi, primi e secondi:
/// la conversione la fa l'importer, una volta; qui i numeri tornano identici dal JSON.
/// </summary>
public readonly record struct Position(double Lat, double Lon);

/// <summary>Una distanza come la stampa l'AIP: «1.0 NM», «5.0 KM», «300.0 M».</summary>
public readonly record struct Distance(double Value, DistanceUnit Unit)
{
    public const double MetriPerMiglio = 1852.0;

    public double ToNauticalMiles() => Unit switch
    {
        DistanceUnit.Nm => Value,
        DistanceUnit.Km => Value * 1000.0 / MetriPerMiglio,
        DistanceUnit.M => Value / MetriPerMiglio,
        _ => throw new ArgumentOutOfRangeException(nameof(Unit), Unit, null),
    };
}

public enum DistanceUnit
{
    Nm,
    Km,
    M,
}

/// <summary>
/// Un limite verticale. 🔴 <see cref="Text"/> è quello <b>stampato</b>, sempre presente: le quote stanno in
/// piedi come nel PDF (§M regole 8 e 9), e chi le confronta guarda prima il testo. Valore, unità e riferimento
/// li riempie l'importer quando il testo è di una forma che conosce; altrimenti restano vuoti e il testo basta.
/// </summary>
public sealed record VerticalLimit
{
    /// <summary>Come lo scrive l'AIP: <c>FL 195</c>, <c>2500 FT AMSL</c>, <c>GND</c>, <c>UNL</c>.</summary>
    public required string Text { get; init; }

    public VerticalReference Reference { get; init; }

    /// <summary>Il numero (195 per <c>FL 195</c>, 2500 per <c>2500 FT AMSL</c>); vuoto per GND e UNL.</summary>
    public double? Value { get; init; }

    public VerticalUnit? Unit { get; init; }
}

public enum VerticalReference
{
    /// <summary>Il testo non è di una forma nota: vale solo <see cref="VerticalLimit.Text"/>.</summary>
    Unknown,

    /// <summary>Il suolo (GND, SFC).</summary>
    Ground,

    /// <summary>Illimitato (UNL).</summary>
    Unlimited,

    /// <summary>Sul livello del mare (AMSL, MSL).</summary>
    Msl,

    /// <summary>Sul suolo (AGL).</summary>
    Agl,

    /// <summary>Livello di volo, sull'isobara standard.</summary>
    FlightLevel,
}

public enum VerticalUnit
{
    Ft,
    M,
    Fl,
}
