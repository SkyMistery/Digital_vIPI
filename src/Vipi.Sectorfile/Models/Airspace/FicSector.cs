namespace Vipi.Sectorfile.Models;

/// <summary>
/// FIC (Flight Information Centre) shape from DYNAMIC_SEC/*fic.tfl. Extends <see cref="TflSector"/>
/// with the two extra fields FIC files carry (ShapeLabel, IsFssPerimeter), so it remains a
/// <c>TflSector</c> (the geometry/header fields are identical) while FicParser/FicSaver handle
/// the extra fields. Two categories share the file, distinguished by FillColor:
///   - FSS sector perimeter: FillColor = "CTR" AND SectorCode ends with "FSS".
///   - Geographic reference shapes (lakes, terrain): FillColor = "&lt;FIR&gt;FIC", Flags = 0,
///     carrying section comments such as //GARDA, //LAGO DI COMO.
/// </summary>
public sealed class FicSector : TflSector
{
    /// <summary>
    /// true → FSS perimeter (FillColor == "CTR" AND SectorCode ends with "FSS");
    /// false → geographic reference shape.
    /// </summary>
    public bool IsFssPerimeter
        => string.Equals(FillColor?.Trim(), "CTR", StringComparison.OrdinalIgnoreCase)
           && SectorCode is not null
           && SectorCode.EndsWith("FSS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Optional; derived from the // section comment immediately preceding the block
    /// (e.g. "GARDA", "LAGO DI COMO").
    /// </summary>
    public string? ShapeLabel { get; set; }
}
