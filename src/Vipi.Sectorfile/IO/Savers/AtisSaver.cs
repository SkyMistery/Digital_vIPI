using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises an <see cref="AtisData"/> to its single .atis template line (placeholders verbatim).
/// One template per file, so the identifier is the constant "ATIS".
/// </summary>
public sealed class AtisSaver : IFileSaver<AtisData>
{
    public IReadOnlyList<string> Serialize(AtisData record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new[] { record.Template };
    }

    public string GetIdentifier(AtisData record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return "ATIS";
    }
}
