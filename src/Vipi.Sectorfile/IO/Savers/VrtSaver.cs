using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Scrive una <see cref="RottaVfr"/>: una riga per punto, <c>Numero ; Lat ; Lon ;</c> — e <c>; 1 ;</c> in coda (4°
/// campo riservato, 5° militare) su ogni riga di una rotta militare (slice 16a).
/// </summary>
public sealed class VrtSaver : IFileSaver<RottaVfr>
{
    public IReadOnlyList<string> Serialize(RottaVfr record)
    {
        ArgumentNullException.ThrowIfNull(record);
        string coda = record.Militare ? ";1;" : string.Empty;
        return record.Punti.Select(p => record.Numero + ";" + p.Riga() + coda).ToArray();
    }

    public string GetIdentifier(RottaVfr record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Numero;
    }
}
