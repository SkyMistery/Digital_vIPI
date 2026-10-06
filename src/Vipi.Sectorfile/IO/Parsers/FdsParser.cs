using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge i <c>.fds</c> (<c>[ATISFIELD]</c>, lotto «Subito» slice 18a): <c>Etichetta;[SEGNAPOSTO];</c> per riga. Una
/// riga senza il segnaposto fra parentesi quadre non è un campo e resta com'è.
/// </summary>
public sealed class FdsParser : LineRecordParser<CampoDellAtis>
{
    public FdsParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out CampoDellAtis record)
    {
        record = default!;
        string[] parts = content.Split(';');
        if (isDisabled || parts.Length < 2 || parts[1].Trim() is not ['[', .. { Length: > 0 } nome, ']'])
        {
            return false;
        }

        record = new CampoDellAtis
        {
            Etichetta = parts[0],   // verbatim
            Segnaposto = nome.Trim(),
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
