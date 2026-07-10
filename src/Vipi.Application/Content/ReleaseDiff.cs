namespace Vipi.Application.Content;

/// <summary>Riepilogo differenze di una release rispetto a quella in vigore (o allo stato live se nessuna effettiva).</summary>
public sealed record ReleaseDiff(bool HasBaseline, string BaselineLabel, IReadOnlyList<ReleaseDiffRow> Rows)
{
    public static ReleaseDiff Empty { get; } = new(false, "", Array.Empty<ReleaseDiffRow>());
}
