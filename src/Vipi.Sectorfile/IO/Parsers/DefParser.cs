using System.Drawing;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses a .def colour-definition file into a <see cref="ColorPalette"/>. Each non-comment line is
///   NAME ; colour ;
/// where the colour is any form of the IVAO sector manual (<see cref="ColoreDelSector"/>: <c>#RRGGBB</c>,
/// <c>#AARRGGBB</c>, <c>R,G,B</c>, <c>%R:G:B</c> — slice 4 of the «Subito» batch; before it only <c>#RRGGBB</c>).
/// Whitespace around the separator is ignored. Unlike the <see cref="IFileParser{T}"/> family, .def
/// is never edited by this application, so it is not round-tripped: <see cref="DefParser"/> builds a
/// palette directly rather than producing chunks (SRS FR-LOAD-05 / TEST_MATRIX §24).
/// </summary>
public sealed class DefParser
{
    private readonly IWarningCollector _warnings;

    public DefParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ColorPalette Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ColorPalette Parse(FileReadResult read, string source)
    {
        var palette = new ColorPalette();

        for (int i = 0; i < read.Lines.Count; i++)
        {
            string trimmed = read.Lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = trimmed.Split(';');
            if (parts.Length < 2 || parts[0].Trim().Length == 0)
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    "Skipping malformed colour definition", i + 1, read.Lines[i]);
                continue;
            }

            if (!ColoreDelSector.TryLeggi(parts[1], out Color color))
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    "Unparseable colour value", i + 1, read.Lines[i]);
                continue;
            }

            palette.Add(new ColorDefinition(parts[0].Trim(), color));   // last-write-wins
        }

        return palette;
    }
}
