using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="FicSector"/> block. The header/geometry are identical to a
/// <see cref="TflSector"/> (delegated to <see cref="TflSaver.SerializeBlock"/>); the FIC ShapeLabel
/// lives in the record's LeadingComments, which the orchestrator writes separately.
/// </summary>
public sealed class FicSaver : IFileSaver<FicSector>
{
    public IReadOnlyList<string> Serialize(FicSector record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return TflSaver.SerializeBlock(record);
    }

    public string GetIdentifier(FicSector record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.SectorCode;
    }
}
