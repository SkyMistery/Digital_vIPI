using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>Scrive una <see cref="RottaVfr"/>: una riga per punto, <c>Numero ; Lat ; Lon ;</c></summary>
public sealed class VrtSaver : IFileSaver<RottaVfr>
{
    public IReadOnlyList<string> Serialize(RottaVfr record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Punti.Select(p => record.Numero + ";" + p.Riga()).ToArray();
    }

    public string GetIdentifier(RottaVfr record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Numero;
    }
}
