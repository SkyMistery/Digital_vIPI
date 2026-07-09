using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Esito (puro) del calcolo adiacenza import: candidati aggregati per coppia ACC, catalogo degli ACC esteri
/// confinanti da persistire (solo quelli con ≥1 settore adiacente) e l'elenco delle singole adiacenze (per log).
/// </summary>
public sealed record NeighbourComputeResult(
    IReadOnlyList<NeighbourCandidateUpsert> Candidates,
    IReadOnlyList<ForeignAccImport> ForeignCatalog,
    IReadOnlyList<NeighbourHit> Hits);
