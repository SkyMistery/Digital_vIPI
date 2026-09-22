using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .atis files. Each non-comment, non-blank line is a raw ATIS text template (placeholders in
/// square brackets are preserved verbatim — TEST_MATRIX §14). Files normally hold a single template;
/// any leading <c>//</c> comment becomes the record's LeadingComments.
/// </summary>
public sealed class AtisParser : LineRecordParser<AtisData>
{
    public AtisParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out AtisData record)
    {
        record = default!;

        // No disabled-record concept: a // line is a comment, not a template.
        if (isDisabled || content.Length == 0)
        {
            return false;
        }

        record = new AtisData
        {
            Template = content,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
