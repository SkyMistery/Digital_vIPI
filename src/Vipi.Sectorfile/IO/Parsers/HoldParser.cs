using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge il <c>.hold</c> (<c>[HOLDENR]</c>, carta F2 slice 6), un'attesa per riga:
/// <c>Nome ; Lat ; Lon ; Descrizione ;</c> — vedi <see cref="Attesa"/>. Senza lettore in A.
/// Un'attesa non ha stato disattivato: una riga <c>//</c> è sempre un commento.
/// </summary>
public sealed class HoldParser : LineRecordParser<Attesa>
{
    public HoldParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Attesa record)
    {
        record = default!;
        if (isDisabled)
        {
            return false;
        }

        string[] parts = content.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        // L'ultima riga di HOLDENR.hold non ha il `;` finale (`HLD-OZE;…;OZE/196L-FL135`): è un'attesa lo stesso.
        if (n < 4 || parts[0].Trim().Length == 0 || !Punto.TryLeggi(parts[1], parts[2], out var posizione))
        {
            return false;
        }

        record = new Attesa
        {
            Nome = parts[0].Trim(),
            Posizione = posizione,
            Descrizione = parts[3].Trim(),
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
