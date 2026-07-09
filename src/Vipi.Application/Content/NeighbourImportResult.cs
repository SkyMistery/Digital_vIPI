namespace Vipi.Application.Content;

/// <summary>Esito dell'import+calcolo adiacenza degli ACC confinanti.</summary>
public sealed record NeighbourImportResult(
    int CountriesQueried, int ForeignAccsFetched, int CandidatesCreated, int CandidatesUpdated,
    IReadOnlyList<string> Warnings);
