using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>Scrive un <see cref="CampoDellAtis"/>: <c>Etichetta;[SEGNAPOSTO];</c></summary>
public sealed class FdsSaver : IFileSaver<CampoDellAtis>
{
    public IReadOnlyList<string> Serialize(CampoDellAtis record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return [$"{record.Etichetta};[{record.Segnaposto}];"];
    }

    public string GetIdentifier(CampoDellAtis record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Segnaposto;
    }
}
