namespace Aip;

/// <summary>
/// Un settore della carta delle altitudini minime di sorveglianza ATC di uno scalo (ATC SMAC, la carta 8 di AD 2: carta F6,
/// «finiamo con l'importer»): la quota minima com'è stampata (<c>2000FT</c>, <c>FL100</c>, <c>TRL</c>), il nome del
/// settore quando la carta lo dà (<c>NORTH-EAST</c>), la forma, la pagina. È quel che il sector tiene nel <c>.mva</c>.
/// </summary>
public sealed record MinimumAltitudeArea
{
    /// <summary>Lo scalo della carta: <c>LICA</c>.</summary>
    public required string Aerodrome { get; init; }

    /// <summary>La quota minima: il testo com'è stampato, e quando si legge il valore.</summary>
    public required VerticalLimit Minimum { get; init; }

    /// <summary>Il nome del settore accanto alla quota (<c>NORTH-EAST</c>), dove c'è.</summary>
    public string? Name { get; init; }

    /// <summary>I rimandi alle note della carta accanto alla quota: <c>(5)</c>.</summary>
    public string? NoteRefs { get; init; }

    public required Geometry Geometry { get; init; }

    public AipPage? Page { get; init; }
}
