namespace Aip;

/// <summary>
/// I controlli che valgono per ogni Stato, su un documento già letto: quel che il JSON da solo non impedisce.
/// Non corregge niente; dice che cosa e dove. L'importer lo chiama prima di consegnare, il Lab prima di usare.
/// </summary>
public static class AipCheck
{
    public static IReadOnlyList<AipProblem> Run(AipDocument document)
    {
        var problemi = new List<AipProblem>();

        if (string.IsNullOrWhiteSpace(document.State))
            problemi.Add(new AipProblem(AipProblemKind.MissingState, null));

        var chiavi = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in document.Airspaces)
        {
            if (string.IsNullOrWhiteSpace(area.Designator))
                problemi.Add(new AipProblem(AipProblemKind.MissingDesignator, area.Key));
            else if (!chiavi.Add(area.Key))
                problemi.Add(new AipProblem(AipProblemKind.DuplicateKey, area.Key));

            if (string.IsNullOrWhiteSpace(area.Upper.Text) || string.IsNullOrWhiteSpace(area.Lower.Text))
                problemi.Add(new AipProblem(AipProblemKind.MissingLimitText, area.Key));

            ControllaGeometria(area, problemi);
        }

        return problemi;
    }

    private static void ControllaGeometria(Airspace area, List<AipProblem> problemi)
    {
        switch (area.Geometry)
        {
            case CircleGeometry cerchio:
                ControllaPunto(cerchio.Center, area, problemi);
                if (!(cerchio.Radius.Value > 0))
                    problemi.Add(new AipProblem(AipProblemKind.NonPositiveRadius, area.Key));
                break;

            case RingGeometry anello:
                // Un arco «till point of origin» chiude un anello anche da solo; fatto di sole rette, ne servono tre.
                var haCurve = anello.Segments.Any(s => s is not LineSegment);
                if (anello.Segments.Count == 0 || (!haCurve && anello.Segments.Count < 3))
                    problemi.Add(new AipProblem(AipProblemKind.TooFewSegments, area.Key, anello.Segments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                foreach (var tratto in anello.Segments)
                {
                    ControllaPunto(tratto.From, area, problemi);
                    if (tratto is ArcSegment arco)
                    {
                        ControllaPunto(arco.Center, area, problemi);
                        if (arco.Radius is { } raggio && !(raggio.Value > 0))
                            problemi.Add(new AipProblem(AipProblemKind.NonPositiveRadius, area.Key));
                    }
                }
                break;

            default:
                problemi.Add(new AipProblem(AipProblemKind.MissingGeometry, area.Key));
                break;
        }
    }

    private static void ControllaPunto(Position p, Airspace area, List<AipProblem> problemi)
    {
        if (!(p.Lat is >= -90 and <= 90) || !(p.Lon is >= -180 and <= 180))
            problemi.Add(new AipProblem(AipProblemKind.OutOfRange, area.Key,
                FormattableString.Invariant($"{p.Lat} {p.Lon}")));
    }
}

/// <summary>Un problema, legato all'area (<paramref name="Key"/>, <see cref="Airspace.Key"/>; null = il documento).</summary>
public sealed record AipProblem(AipProblemKind Kind, string? Key, string? Detail = null);

/// <summary>⚠️ Un codice, non un messaggio: il testo lo scrive chi lo mostra, nella sua lingua.</summary>
public enum AipProblemKind
{
    MissingState,
    MissingDesignator,
    DuplicateKey,
    MissingLimitText,
    MissingGeometry,
    TooFewSegments,
    NonPositiveRadius,
    OutOfRange,
}
