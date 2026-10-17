using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Scrive un <see cref="Simbolo"/> (lotto «Subito» slice 17a): il nome nel commento sopra (<c>//FIX vuoto</c>) e la riga
/// dei pixel. Un nome senza <c>//</c> resta com'era finché non lo si corregge; un simbolo senza nome è la sola riga.
/// </summary>
public sealed class SymSaver : IFileSaver<Simbolo>
{
    public IReadOnlyList<string> Serialize(Simbolo record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.Nome))
            return [record.Pixel];
        return [record.NomeSenzaBarre ? record.Nome : "//" + record.Nome, record.Pixel];
    }

    public string GetIdentifier(Simbolo record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return string.IsNullOrWhiteSpace(record.Nome) ? "SIMBOLO" : record.Nome;
    }
}
