using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Documento modificato/pubblicato nel ciclo AIRAC corrente, con delta di conteggio rispetto alla versione precedente.</summary>
public sealed class ChangeRow
{
    public required string DocTitle { get; init; }
    public required DocumentType Type { get; init; }
    public required string FirCode { get; init; }
    public required string Url { get; init; }
    public required int VersionNumber { get; init; }
    public string? Note { get; init; }
    public required int PublishedByVid { get; init; }
    public required DateTime PublishedUtc { get; init; }
    public required int PrevBlocks { get; init; }
    public required int CurrBlocks { get; init; }
    public required int PrevSections { get; init; }
    public required int CurrSections { get; init; }

    public bool IsNew => VersionNumber == 1;
}
