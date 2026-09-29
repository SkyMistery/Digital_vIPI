using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>Scrive un'<see cref="ImpostazioneDelProfilo"/>: <c>Chiave=Valore</c> (lotto «Subito» slice 11d).</summary>
public sealed class ProfiloSaver : IFileSaver<ImpostazioneDelProfilo>
{
    public IReadOnlyList<string> Serialize(ImpostazioneDelProfilo record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return [record.Chiave + "=" + record.Valore];
    }

    public string GetIdentifier(ImpostazioneDelProfilo record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Sezione + "/" + record.Chiave;
    }
}
