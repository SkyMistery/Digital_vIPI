namespace Vipi.Application.Content;

/// <summary>Riepilogo differenze di una release rispetto a quella in vigore (o allo stato live se nessuna effettiva).</summary>
/// <param name="BaselineCycle">Ciclo AIRAC della release di confronto; null = nessuna release in vigore. L'etichetta
/// da mostrare la compone la UI: qui non si producono frasi (doc 13 §3k).</param>
public sealed record ReleaseDiff(bool HasBaseline, string? BaselineCycle, IReadOnlyList<ReleaseDiffRow> Rows)
{
    public static ReleaseDiff Empty { get; } = new(false, null, Array.Empty<ReleaseDiffRow>());
}
