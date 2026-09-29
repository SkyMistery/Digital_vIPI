using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge i <c>.cpdlc</c> (<c>[CPDLC]</c>, lotto «Subito» slice 11c): un <see cref="MessaggioCpdlc"/> per riga,
/// <c>Comando;Risposta;Gruppo;</c> e i tredici campi dei valori. Senza stato disattivato: una riga <c>//</c> è un commento.
/// </summary>
public sealed class CpdlcParser : LineRecordParser<MessaggioCpdlc>
{
    public CpdlcParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out MessaggioCpdlc record)
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

        if (n < 3 || parts[0].Trim().Length == 0)
        {
            return false;
        }

        record = new MessaggioCpdlc
        {
            Comando = parts[0],
            Risposta = parts[1],
            Gruppo = parts[2],
            Source = new SourceRef(source, lineNumber),
        };
        for (int i = 3; i < n; i++)
        {
            record.Valori.Add(parts[i]);   // verbatim
        }

        return true;
    }
}

/// <summary>Legge i <c>.cpdlcnames</c> (<c>[CPDLCNAMES]</c>, slice 11c): <c>GROUP.ID;NOME;</c> per riga.</summary>
public sealed class CpdlcNamesParser : LineRecordParser<NomeDelGruppoCpdlc>
{
    public CpdlcNamesParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out NomeDelGruppoCpdlc record)
    {
        record = default!;
        string[] parts = content.Split(';');
        if (isDisabled || parts.Length < 2 || !parts[0].Trim().StartsWith("GROUP.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        record = new NomeDelGruppoCpdlc
        {
            Gruppo = parts[0].Trim()["GROUP.".Length..],
            Nome = parts[1],
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
