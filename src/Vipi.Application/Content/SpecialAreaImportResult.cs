namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import aree speciali su tutti gli ACC: conteggi aggregati + eventuali ACC saltati
/// (<see cref="Failures"/>), che il chiamante Infra logga come warning.
/// </summary>
public sealed record SpecialAreaImportResult(
    int Created, int Updated, int Removed, IReadOnlyList<SpecialAreaImportFailure> Failures);
