using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>Scrive un'<see cref="Attesa"/> come riga del <c>.hold</c>: <c>Nome ; Lat ; Lon ; Descrizione ;</c></summary>
public sealed class HoldSaver : IFileSaver<Attesa>
{
    public IReadOnlyList<string> Serialize(Attesa record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new[] { record.Nome + ";" + record.Posizione.Riga() + record.Descrizione + ";" };
    }

    public string GetIdentifier(Attesa record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Nome;
    }
}
